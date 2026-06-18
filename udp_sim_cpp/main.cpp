#include "udp_sim.hpp"

#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>
#include <thread>
#include <atomic>
#include <mutex>
#include <chrono>
#include <csignal>
#include <cstring>
#include <iomanip>
#include <algorithm>
#include <random>
#include <getopt.h>

// ─── Terminal colors ──────────────────────────────────────────────────────────

#define CLR_RESET  "\033[0m"
#define CLR_RED    "\033[31m"
#define CLR_GREEN  "\033[32m"
#define CLR_YELLOW "\033[33m"
#define CLR_CYAN   "\033[36m"
#define CLR_BOLD   "\033[1m"
#define CLR_DIM    "\033[2m"

// ─── Globals ──────────────────────────────────────────────────────────────────

static std::atomic<bool> g_quit{false};
static std::mutex        g_print_mu;
static bool              g_verbose = false;
static bool              g_logging = true;   // real-time RX/TX print on/off

static void sig_handler(int) { g_quit = true; }

// ─── Auto-send state ──────────────────────────────────────────────────────────

enum class PayloadMode {
    Fixed,    // fixed byte pattern
    Counter,  // 4-byte seq number prepended / overwritten at offset
    Random,   // re-randomize every burst
};

struct AutoSend {
    std::atomic<bool>    active{false};
    std::atomic<int>     interval_ms{1000};
    PayloadMode          mode = PayloadMode::Fixed;
    std::vector<uint8_t> payload;
    std::atomic<uint32_t> counter{0};
    int                  counter_offset = 0;  // byte offset in payload for counter
    std::mutex           mu;
    std::thread          thread;
};

// ─── Print helpers ────────────────────────────────────────────────────────────

static void print_hex_ascii(const std::vector<uint8_t>& data, const char* color)
{
    constexpr int W = 16;
    for (size_t i = 0; i < data.size(); i += W) {
        std::cout << CLR_DIM << "  "
                  << std::hex << std::setw(4) << std::setfill('0') << i
                  << "  " << CLR_RESET;

        std::cout << color;
        for (int j = 0; j < W; ++j) {
            if (i + j < data.size())
                std::cout << std::hex << std::setw(2) << std::setfill('0')
                          << static_cast<int>(data[i + j]) << ' ';
            else
                std::cout << "   ";
            if (j == 7) std::cout << ' ';
        }
        std::cout << CLR_RESET;

        std::cout << ' ' << CLR_BOLD << '|';
        for (int j = 0; j < W && i + j < data.size(); ++j) {
            uint8_t c = data[i + j];
            std::cout << (char)(c >= 32 && c < 127 ? c : '.');
        }
        int printed = static_cast<int>(std::min((size_t)W, data.size() - i));
        for (int j = printed; j < W; ++j) std::cout << ' ';
        std::cout << '|' << CLR_RESET << '\n';
    }
    std::cout << std::dec;
}

static void print_packet_oneline(const Packet& p)
{
    const char* color = p.is_tx ? CLR_GREEN : CLR_CYAN;
    const char* label = p.is_tx ? "TX" : "RX";
    std::string peer  = p.is_tx
        ? (p.dst_ip + ':' + std::to_string(p.dst_port))
        : (p.src_ip + ':' + std::to_string(p.src_port));

    std::cout << color << CLR_BOLD << '[' << label << ']' << CLR_RESET
              << CLR_DIM << ' ' << format_time(p.timestamp) << CLR_RESET
              << "  " << std::left  << std::setw(22) << peer
              << "  " << std::right << std::setw(5)  << p.data.size() << " bytes"
              << "  " << to_hex(p.data, 16)
              << '\n';
}

static void print_packet_detail(const Packet& p)
{
    const char* color = p.is_tx ? CLR_GREEN : CLR_CYAN;
    const char* label = p.is_tx ? "TX" : "RX";
    std::string src   = p.src_ip + ':' + std::to_string(p.src_port);
    std::string dst   = p.dst_ip + ':' + std::to_string(p.dst_port);

    std::cout << color << CLR_BOLD
              << "┌─[" << label << "] " << format_time(p.timestamp)
              << "  " << src << " → " << dst
              << "  " << p.data.size() << " bytes"
              << CLR_RESET << '\n';
    print_hex_ascii(p.data, color);
    std::cout << color << CLR_BOLD << "└" << std::string(60, '-') << CLR_RESET << '\n';
}

static void print_packet(const Packet& p)
{
    std::lock_guard<std::mutex> lk(g_print_mu);
    if (!g_logging) return;
    if (g_verbose && !p.is_tx)
        print_packet_detail(p);
    else
        print_packet_oneline(p);
}

// ─── Help / status / config ───────────────────────────────────────────────────

static void print_help()
{
    std::cout <<
        '\n' << CLR_BOLD "Commands:" CLR_RESET "\n"
        "\n" CLR_BOLD "  [Send]" CLR_RESET "\n"
        "  send <hex>            Send hex bytes         (e.g. send DE AD BE EF)\n"
        "  sendt <text>          Send ASCII text         (e.g. sendt hello)\n"
        "  sendf <path>          Send file contents as binary\n"
        "\n" CLR_BOLD "  [Auto-send]" CLR_RESET "\n"
        "  auto <ms>             Auto-send every <ms>ms (0 = stop)\n"
        "  payload <hex>         Set fixed payload (hex)\n"
        "  payloadt <text>       Set fixed payload (ASCII)\n"
        "  payloadr <n>          Set random payload: n bytes (re-randomized each burst)\n"
        "  counter [offset]      Enable counter mode: 4-byte LE seq# at <offset> (default 0)\n"
        "  counter off           Disable counter mode\n"
        "\n" CLR_BOLD "  [Log]" CLR_RESET "\n"
        "  log [n]               One-line list of last n packets (default 10)\n"
        "  show [n]              Hex+ASCII detail of last n packets (default 1)\n"
        "  dump [n]              Hex+ASCII detail of nth-last packet (default 1)\n"
        "  verbose [on|off]      Auto hex+ASCII dump on every RX\n"
        "  logon                 Resume real-time RX/TX printing\n"
        "  logoff                Pause  real-time RX/TX printing\n"
        "  logtx [n]             Show last n TX packets (default 10)\n"
        "  logrx [n]             Show last n RX packets (default 10)\n"
        "  logmax <n>            Set max packet log size (default 1000)\n"
        "  resend [n]            Resend nth-last packet (default 1)\n"
        "  clear                 Clear packet log\n"
        "\n" CLR_BOLD "  [Pcap]" CLR_RESET "\n"
        "  pcap <file.pcap>      Start capturing to file\n"
        "  pcap off              Stop capturing\n"
        "  pcap status           Show capture status\n"
        "\n" CLR_BOLD "  [Info]" CLR_RESET "\n"
        "  status                Show TX/RX stats\n"
        "  config                Show current config\n"
        "  help                  Show this help\n"
        "  quit / q              Exit\n\n";
}

static void print_status(const UdpConfig& cfg, const UdpSocket& sock,
                          const PacketLog& log, const PcapWriter& pcap,
                          const AutoSend& as)
{
    const char* mode_str =
        cfg.mode == UdpMode::SendOnly ? "send-only" :
        cfg.mode == UdpMode::RecvOnly ? "recv-only" : "bidirectional";

    std::cout <<
        '\n' << CLR_BOLD "─── Status ────────────────────────────────" CLR_RESET "\n"
        "  Local    : " << cfg.local_ip  << ':' << cfg.local_port  << '\n' <<
        "  Remote   : " << cfg.remote_ip << ':' << cfg.remote_port << '\n' <<
        "  Mode     : " << mode_str << '\n' <<
        "  TX       : " << sock.tx_count() << " pkts / " << sock.tx_bytes() << " bytes\n"
        "  RX       : " << sock.rx_count() << " pkts / " << sock.rx_bytes() << " bytes\n"
        "  Log      : " << log.size() << " packets\n"
        "  Logging  : " << (g_logging ? "ON" : "OFF (logon to resume)") << '\n';

    if (as.active) {
        std::string pm = as.mode == PayloadMode::Counter ? "counter"
                       : as.mode == PayloadMode::Random  ? "random" : "fixed";
        std::cout << "  Auto-send: ON  interval=" << as.interval_ms.load()
                  << "ms  mode=" << pm << '\n';
    } else {
        std::cout << "  Auto-send: OFF\n";
    }

    if (pcap.is_open())
        std::cout << "  Pcap     : " << pcap.path() << " (recording)\n";
    else
        std::cout << "  Pcap     : OFF\n";

    std::cout << CLR_BOLD "───────────────────────────────────────────" CLR_RESET "\n\n";
}

static void print_config(const UdpConfig& cfg)
{
    const char* mode_str =
        cfg.mode == UdpMode::SendOnly ? "send-only" :
        cfg.mode == UdpMode::RecvOnly ? "recv-only" : "bidirectional";

    std::cout << '\n' << CLR_BOLD "─── Config ────────────────────────────────" CLR_RESET "\n"
              << "  local_ip    : " << cfg.local_ip    << '\n'
              << "  local_port  : " << cfg.local_port  << '\n'
              << "  remote_ip   : " << cfg.remote_ip   << '\n'
              << "  remote_port : " << cfg.remote_port << '\n'
              << "  mode        : " << mode_str        << '\n'
              << "  mtu         : " << cfg.mtu << " bytes"
              << "  (max UDP payload: " << cfg.mtu - 28 << " bytes)\n"
              << CLR_BOLD "───────────────────────────────────────────" CLR_RESET "\n\n";
}

static void show_log(const PacketLog& log, int n)
{
    auto pkts = log.snapshot();
    int  start = std::max(0, (int)pkts.size() - n);

    std::cout << '\n' << CLR_BOLD "─── Packet Log ("
              << (pkts.size() - start) << " of " << pkts.size()
              << ") ──────────────" CLR_RESET "\n";
    for (int i = start; i < (int)pkts.size(); ++i)
        print_packet(pkts[i]);
    std::cout << CLR_BOLD "────────────────────────────────────────────" CLR_RESET "\n\n";
}

// ─── Auto-send thread ─────────────────────────────────────────────────────────

static std::mt19937 g_rng{std::random_device{}()};

static void auto_send_thread(AutoSend& as, UdpSocket& sock,
                              PacketLog& log, PcapWriter& pcap,
                              const UdpConfig& cfg)
{
    while (as.active && !g_quit) {
        std::vector<uint8_t> payload;
        {
            std::lock_guard<std::mutex> lk(as.mu);
            payload = as.payload;

            if (as.mode == PayloadMode::Random) {
                std::uniform_int_distribution<int> dist(0, 255);
                for (auto& b : payload) b = static_cast<uint8_t>(dist(g_rng));

            } else if (as.mode == PayloadMode::Counter) {
                uint32_t seq = as.counter.fetch_add(1);
                int off = as.counter_offset;
                // Write 4 bytes little-endian into payload (pad if needed)
                if ((int)payload.size() < off + 4)
                    payload.resize(off + 4, 0x00);
                payload[off + 0] =  seq        & 0xFF;
                payload[off + 1] = (seq >>  8) & 0xFF;
                payload[off + 2] = (seq >> 16) & 0xFF;
                payload[off + 3] = (seq >> 24) & 0xFF;
            }
        }

        if (!payload.empty() && sock.send(payload)) {
            Packet p;
            p.timestamp = std::chrono::system_clock::now();
            p.src_ip    = cfg.local_ip;
            p.src_port  = cfg.local_port;
            p.dst_ip    = cfg.remote_ip;
            p.dst_port  = cfg.remote_port;
            p.data      = payload;
            p.is_tx     = true;
            log.push(p);
            if (pcap.is_open()) pcap.write(p);
            print_packet(p);
        }

        int ms = as.interval_ms.load();
        for (int i = 0; i < ms && as.active && !g_quit; i += 10)
            std::this_thread::sleep_for(
                std::chrono::milliseconds(std::min(10, ms - i)));
    }
}

// ─── Send helpers ─────────────────────────────────────────────────────────────

static void do_send(UdpSocket& sock, PacketLog& log, PcapWriter& pcap,
                    const UdpConfig& cfg, std::vector<uint8_t> payload)
{
    if ((int)payload.size() > sock.max_payload())
        std::cout << CLR_YELLOW "Warning: payload " << payload.size()
                  << " bytes exceeds MTU payload limit " << sock.max_payload()
                  << " bytes — kernel will fragment.\n" CLR_RESET;

    if (sock.send(payload)) {
        Packet p;
        p.timestamp = std::chrono::system_clock::now();
        p.src_ip    = cfg.local_ip;
        p.src_port  = cfg.local_port;
        p.dst_ip    = cfg.remote_ip;
        p.dst_port  = cfg.remote_port;
        p.data      = std::move(payload);
        p.is_tx     = true;
        log.push(p);
        if (pcap.is_open()) pcap.write(p);
        print_packet(p);
    } else {
        std::cout << CLR_RED "Send failed.\n" CLR_RESET;
    }
}

// ─── CLI usage ────────────────────────────────────────────────────────────────

static void usage(const char* prog)
{
    std::cout <<
        "Usage: " << prog << " [OPTIONS]\n\n"
        "Options:\n"
        "  -l, --local-ip    <ip>    Local bind IP         (default: 0.0.0.0)\n"
        "  -p, --local-port  <port>  Local bind port       (default: 12345)\n"
        "  -r, --remote-ip   <ip>    Remote IP             (default: 127.0.0.1)\n"
        "  -q, --remote-port <port>  Remote port           (default: 12346)\n"
        "  -m, --mode        <mode>  send | recv | bidir   (default: bidir)\n"
        "  -d, --data        <hex>   Initial auto-send payload (hex)\n"
        "  -i, --interval    <ms>    Auto-send interval ms (0 = off, default: 0)\n"
        "  -M, --mtu         <bytes> MTU size              (default: 1500)\n"
        "  -c, --capture     <file>  Start pcap capture to file\n"
        "  -h, --help                Show this help\n\n"
        "Examples:\n"
        "  " << prog << " -l 0.0.0.0 -p 5000 -r 192.168.1.10 -q 5000\n"
        "  " << prog << " -p 5000 -r 192.168.1.10 -q 5000 -d DEADBEEF -i 100\n"
        "  " << prog << " -p 5000 -r 192.168.1.10 -q 5000 -M 9000 -c capture.pcap\n\n";
}

// ─── main ─────────────────────────────────────────────────────────────────────

int main(int argc, char* argv[])
{
    signal(SIGINT,  sig_handler);
    signal(SIGTERM, sig_handler);

    UdpConfig   cfg;
    std::string initial_hex;
    std::string initial_pcap;
    int         auto_interval = 0;

    const option long_opts[] = {
        {"local-ip",    required_argument, nullptr, 'l'},
        {"local-port",  required_argument, nullptr, 'p'},
        {"remote-ip",   required_argument, nullptr, 'r'},
        {"remote-port", required_argument, nullptr, 'q'},
        {"mode",        required_argument, nullptr, 'm'},
        {"data",        required_argument, nullptr, 'd'},
        {"interval",    required_argument, nullptr, 'i'},
        {"mtu",         required_argument, nullptr, 'M'},
        {"capture",     required_argument, nullptr, 'c'},
        {"help",        no_argument,       nullptr, 'h'},
        {nullptr, 0, nullptr, 0}
    };

    int opt, idx;
    while ((opt = getopt_long(argc, argv, "l:p:r:q:m:d:i:M:c:h", long_opts, &idx)) != -1) {
        switch (opt) {
        case 'l': cfg.local_ip    = optarg;            break;
        case 'p': cfg.local_port  = std::stoi(optarg); break;
        case 'r': cfg.remote_ip   = optarg;            break;
        case 'q': cfg.remote_port = std::stoi(optarg); break;
        case 'm':
            if      (std::string(optarg) == "send") cfg.mode = UdpMode::SendOnly;
            else if (std::string(optarg) == "recv") cfg.mode = UdpMode::RecvOnly;
            else                                    cfg.mode = UdpMode::Bidirectional;
            break;
        case 'd': initial_hex  = optarg;            break;
        case 'i': auto_interval = std::stoi(optarg); break;
        case 'M': cfg.mtu      = std::stoi(optarg); break;
        case 'c': initial_pcap = optarg;            break;
        case 'h': usage(argv[0]); return 0;
        default:  usage(argv[0]); return 1;
        }
    }

    // ── Start UDP ─────────────────────────────────────────────────────────────
    PacketLog  log;
    UdpSocket  sock(cfg);
    PcapWriter pcap;

    if (!initial_pcap.empty()) {
        if (pcap.open(initial_pcap))
            std::cout << CLR_YELLOW "Pcap capture: " << initial_pcap << CLR_RESET "\n";
        else
            std::cerr << CLR_RED "Warning: could not open pcap file: "
                      << initial_pcap << CLR_RESET "\n";
    }

    auto rx_cb = [&](const Packet& p) {
        log.push(p);
        if (pcap.is_open()) pcap.write(p);
        print_packet(p);
        std::lock_guard<std::mutex> lk(g_print_mu);
        std::cout << "> " << std::flush;
    };

    if (!sock.start(rx_cb)) {
        std::cerr << CLR_RED "Error: failed to bind "
                  << cfg.local_ip << ':' << cfg.local_port
                  << " — " << strerror(errno) << CLR_RESET "\n";
        return 1;
    }

    const char* mode_str =
        cfg.mode == UdpMode::SendOnly ? "send-only" :
        cfg.mode == UdpMode::RecvOnly ? "recv-only" : "bidirectional";

    std::cout << CLR_BOLD CLR_GREEN "UDP Simulator started\n" CLR_RESET
              << "  Local  : " << cfg.local_ip  << ':' << cfg.local_port  << '\n'
              << "  Remote : " << cfg.remote_ip << ':' << cfg.remote_port << '\n'
              << "  Mode   : " << mode_str << '\n'
              << "  MTU    : " << cfg.mtu << " bytes"
              << "  (max UDP payload: " << cfg.mtu - 28 << " bytes)\n"
              << "Type 'help' for commands.\n\n";

    // ── Auto-send setup ───────────────────────────────────────────────────────
    AutoSend as;
    as.payload = initial_hex.empty()
        ? std::vector<uint8_t>{0x55, 0x44, 0x50, 0x53, 0x49, 0x4D, 0x00, 0x01}
        : parse_hex(initial_hex);

    if (auto_interval > 0) {
        as.interval_ms = auto_interval;
        as.active      = true;
        as.thread = std::thread(auto_send_thread,
                                std::ref(as), std::ref(sock),
                                std::ref(log), std::ref(pcap), std::cref(cfg));
        std::cout << CLR_YELLOW "Auto-send started: interval=" << auto_interval
                  << "ms  payload=" << to_hex(as.payload, 0) << CLR_RESET "\n\n";
    }

    // ── Interactive loop ──────────────────────────────────────────────────────
    std::string line;
    while (!g_quit) {
        {
            std::lock_guard<std::mutex> lk(g_print_mu);
            std::cout << "> " << std::flush;
        }
        if (!std::getline(std::cin, line)) break;

        std::istringstream iss(line);
        std::string cmd;
        iss >> cmd;
        if (cmd.empty()) continue;

        // ── quit ──────────────────────────────────────────────────────────
        if (cmd == "quit" || cmd == "q" || cmd == "exit") {
            g_quit = true;

        // ── help ──────────────────────────────────────────────────────────
        } else if (cmd == "help" || cmd == "?") {
            print_help();

        // ── config / status ───────────────────────────────────────────────
        } else if (cmd == "config") {
            print_config(cfg);

        } else if (cmd == "status") {
            print_status(cfg, sock, log, pcap, as);

        // ── log commands ──────────────────────────────────────────────────
        } else if (cmd == "clear") {
            log.clear();
            std::cout << "Log cleared.\n";

        } else if (cmd == "log") {
            int n = 10; iss >> n;
            show_log(log, n);

        } else if (cmd == "show") {
            int n = 1; iss >> n;
            auto pkts = log.snapshot();
            if (pkts.empty()) { std::cout << "Log is empty.\n"; continue; }
            int start = std::max(0, (int)pkts.size() - n);
            std::lock_guard<std::mutex> lk(g_print_mu);
            for (int i = start; i < (int)pkts.size(); ++i)
                print_packet_detail(pkts[i]);

        } else if (cmd == "dump") {
            int n = 1; iss >> n;
            auto pkts = log.snapshot();
            if (pkts.empty()) { std::cout << "Log is empty.\n"; continue; }
            std::lock_guard<std::mutex> lk(g_print_mu);
            print_packet_detail(pkts[std::max(0, (int)pkts.size() - n)]);

        } else if (cmd == "logon") {
            g_logging = true;
            std::cout << CLR_YELLOW "Real-time log: ON\n" CLR_RESET;

        } else if (cmd == "logoff") {
            g_logging = false;
            std::cout << CLR_YELLOW "Real-time log: OFF (packets still counted/logged/pcap'd)\n" CLR_RESET;

        } else if (cmd == "logtx") {
            int n = 10; iss >> n; if (n <= 0) n = 10;
            auto pkts = log.snapshot();
            int shown = 0;
            for (int i = (int)pkts.size() - 1; i >= 0 && shown < n; --i)
                if (pkts[i].is_tx) { print_packet_oneline(pkts[i]); ++shown; }
            if (shown == 0) std::cout << CLR_YELLOW "No TX packets in log.\n" CLR_RESET;

        } else if (cmd == "logrx") {
            int n = 10; iss >> n; if (n <= 0) n = 10;
            auto pkts = log.snapshot();
            int shown = 0;
            for (int i = (int)pkts.size() - 1; i >= 0 && shown < n; --i)
                if (!pkts[i].is_tx) { print_packet_oneline(pkts[i]); ++shown; }
            if (shown == 0) std::cout << CLR_YELLOW "No RX packets in log.\n" CLR_RESET;

        } else if (cmd == "logmax") {
            int n = 0; iss >> n;
            if (n <= 0) { std::cout << CLR_RED "Usage: logmax <n>\n" CLR_RESET; }
            else {
                log.set_max(static_cast<size_t>(n));
                std::cout << CLR_YELLOW "Log max set to " << n << "\n" CLR_RESET;
            }

        } else if (cmd == "resend") {
            int n = 1; iss >> n; if (n <= 0) n = 1;
            auto pkts = log.snapshot();
            int idx = (int)pkts.size() - n;
            if (idx < 0 || pkts.empty())
                std::cout << CLR_RED "Packet not found.\n" CLR_RESET;
            else
                do_send(sock, log, pcap, cfg, pkts[idx].data);

        } else if (cmd == "verbose") {
            std::string val; iss >> val;
            g_verbose = (val != "off");
            std::cout << CLR_YELLOW "Verbose mode: " << (g_verbose ? "ON" : "OFF")
                      << CLR_RESET "\n";

        // ── send commands ─────────────────────────────────────────────────
        } else if (cmd == "send") {
            std::string rest; std::getline(iss, rest);
            auto payload = parse_hex(rest);
            if (payload.empty()) { std::cout << CLR_RED "No valid hex data.\n" CLR_RESET; continue; }
            do_send(sock, log, pcap, cfg, std::move(payload));

        } else if (cmd == "sendt") {
            std::string rest; std::getline(iss, rest);
            if (!rest.empty() && rest[0] == ' ') rest.erase(0, 1);
            if (rest.empty()) { std::cout << CLR_RED "No text provided.\n" CLR_RESET; continue; }
            do_send(sock, log, pcap, cfg, {rest.begin(), rest.end()});

        } else if (cmd == "sendf") {
            std::string path; iss >> path;
            if (path.empty()) { std::cout << CLR_RED "Usage: sendf <path>\n" CLR_RESET; continue; }
            std::ifstream f(path, std::ios::binary);
            if (!f) { std::cout << CLR_RED "Cannot open: " << path << "\n" CLR_RESET; continue; }
            std::vector<uint8_t> payload{
                std::istreambuf_iterator<char>(f),
                std::istreambuf_iterator<char>{}};
            if (payload.empty()) { std::cout << CLR_RED "File is empty.\n" CLR_RESET; continue; }
            std::cout << "Sending file: " << path << " (" << payload.size() << " bytes)\n";
            do_send(sock, log, pcap, cfg, std::move(payload));

        // ── auto-send payload ─────────────────────────────────────────────
        } else if (cmd == "payload") {
            std::string rest; std::getline(iss, rest);
            auto payload = parse_hex(rest);
            if (payload.empty()) { std::cout << CLR_RED "Invalid hex.\n" CLR_RESET; continue; }
            {
                std::lock_guard<std::mutex> lk(as.mu);
                as.payload = payload;
                as.mode = PayloadMode::Fixed;
            }
            std::cout << "Auto-send payload set (fixed): " << to_hex(payload, 0) << '\n';

        } else if (cmd == "payloadt") {
            std::string rest; std::getline(iss, rest);
            if (!rest.empty() && rest[0] == ' ') rest.erase(0, 1);
            {
                std::lock_guard<std::mutex> lk(as.mu);
                as.payload = {rest.begin(), rest.end()};
                as.mode = PayloadMode::Fixed;
            }
            std::cout << "Auto-send payload set (fixed): \"" << rest << "\"\n";

        } else if (cmd == "payloadr") {
            int n = 64; iss >> n;
            if (n <= 0 || n > 65000) { std::cout << CLR_RED "Invalid size.\n" CLR_RESET; continue; }
            {
                std::lock_guard<std::mutex> lk(as.mu);
                as.payload.assign(n, 0x00);
                as.mode = PayloadMode::Random;
            }
            std::cout << "Auto-send payload set (random): " << n << " bytes per burst\n";

        // ── counter mode ──────────────────────────────────────────────────
        } else if (cmd == "counter") {
            std::string arg; iss >> arg;
            if (arg == "off") {
                std::lock_guard<std::mutex> lk(as.mu);
                as.mode = PayloadMode::Fixed;
                std::cout << CLR_YELLOW "Counter mode disabled.\n" CLR_RESET;
            } else {
                int offset = 0;
                if (!arg.empty()) offset = std::stoi(arg);
                {
                    std::lock_guard<std::mutex> lk(as.mu);
                    as.mode = PayloadMode::Counter;
                    as.counter_offset = offset;
                    as.counter = 0;
                    // Ensure payload is large enough
                    if ((int)as.payload.size() < offset + 4)
                        as.payload.resize(offset + 4, 0x00);
                }
                std::cout << CLR_YELLOW "Counter mode enabled: 4-byte LE seq# at offset "
                          << offset << "  (reset to 0)\n" CLR_RESET;
            }

        // ── auto interval ─────────────────────────────────────────────────
        } else if (cmd == "auto") {
            int ms = 0; iss >> ms;
            if (ms <= 0) {
                as.active = false;
                if (as.thread.joinable()) as.thread.join();
                std::cout << CLR_YELLOW "Auto-send stopped.\n" CLR_RESET;
            } else {
                as.interval_ms = ms;
                if (!as.active) {
                    as.active = true;
                    if (as.thread.joinable()) as.thread.join();
                    as.thread = std::thread(auto_send_thread,
                                            std::ref(as), std::ref(sock),
                                            std::ref(log), std::ref(pcap),
                                            std::cref(cfg));
                }
                std::vector<uint8_t> cur;
                { std::lock_guard<std::mutex> lk(as.mu); cur = as.payload; }
                std::cout << CLR_YELLOW "Auto-send: interval=" << ms
                          << "ms  payload=" << to_hex(cur, 0) << CLR_RESET "\n";
            }

        // ── pcap capture ──────────────────────────────────────────────────
        } else if (cmd == "pcap") {
            std::string arg; iss >> arg;
            if (arg == "off") {
                pcap.close();
                std::cout << CLR_YELLOW "Pcap capture stopped.\n" CLR_RESET;
            } else if (arg == "status") {
                if (pcap.is_open())
                    std::cout << "Pcap: recording to " << pcap.path() << "\n";
                else
                    std::cout << "Pcap: not capturing.\n";
            } else if (!arg.empty()) {
                pcap.close();
                if (pcap.open(arg))
                    std::cout << CLR_YELLOW "Pcap capture started: " << arg << CLR_RESET "\n";
                else
                    std::cout << CLR_RED "Failed to open: " << arg << "\n" CLR_RESET;
            } else {
                std::cout << "Usage: pcap <file.pcap> | off | status\n";
            }

        } else {
            std::cout << CLR_RED "Unknown command: " << cmd
                      << ".  Type 'help'.\n" CLR_RESET;
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    as.active = false;
    if (as.thread.joinable()) as.thread.join();
    sock.stop();
    pcap.close();

    std::cout << "\nBye.\n";
    return 0;
}
