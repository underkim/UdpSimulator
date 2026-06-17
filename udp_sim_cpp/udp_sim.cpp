#include "udp_sim.hpp"

#include <sys/socket.h>
#include <arpa/inet.h>
#include <netinet/ip.h>
#include <unistd.h>

#include <sstream>
#include <iomanip>
#include <cstring>
#include <algorithm>

// ─── UdpSocket ────────────────────────────────────────────────────────────────

UdpSocket::UdpSocket(const UdpConfig& cfg) : cfg_(cfg) {}

UdpSocket::~UdpSocket() { stop(); }

bool UdpSocket::start(RxCallback cb)
{
    fd_ = socket(AF_INET, SOCK_DGRAM, 0);
    if (fd_ < 0) return false;

    int yes = 1;
    setsockopt(fd_, SOL_SOCKET, SO_REUSEADDR, &yes, sizeof(yes));

    int sndbuf = cfg_.mtu * 64;
    setsockopt(fd_, SOL_SOCKET, SO_SNDBUF, &sndbuf, sizeof(sndbuf));

    // Disable path-MTU probing so the kernel fragments oversized packets
    // instead of returning EMSGSIZE.
    int pmtu = IP_PMTUDISC_DONT;
    setsockopt(fd_, IPPROTO_IP, IP_MTU_DISCOVER, &pmtu, sizeof(pmtu));

    sockaddr_in local{};
    local.sin_family = AF_INET;
    local.sin_port   = htons(static_cast<uint16_t>(cfg_.local_port));
    inet_pton(AF_INET, cfg_.local_ip.c_str(), &local.sin_addr);

    if (bind(fd_, reinterpret_cast<sockaddr*>(&local), sizeof(local)) < 0) {
        close(fd_);
        fd_ = -1;
        return false;
    }

    // Short read timeout so recv_loop can check running_ periodically.
    timeval tv{};
    tv.tv_usec = 200'000;
    setsockopt(fd_, SOL_SOCKET, SO_RCVTIMEO, &tv, sizeof(tv));

    running_ = true;
    rx_cb_   = std::move(cb);

    if (cfg_.mode != UdpMode::SendOnly)
        recv_thread_ = std::thread(&UdpSocket::recv_loop, this);

    return true;
}

void UdpSocket::stop()
{
    running_ = false;
    if (recv_thread_.joinable()) recv_thread_.join();
    if (fd_ >= 0) { close(fd_); fd_ = -1; }
}

bool UdpSocket::send(const std::vector<uint8_t>& data)
{
    if (fd_ < 0 || cfg_.mode == UdpMode::RecvOnly) return false;

    sockaddr_in remote{};
    remote.sin_family = AF_INET;
    remote.sin_port   = htons(static_cast<uint16_t>(cfg_.remote_port));
    inet_pton(AF_INET, cfg_.remote_ip.c_str(), &remote.sin_addr);

    ssize_t sent = sendto(fd_,
                          data.data(), data.size(), 0,
                          reinterpret_cast<sockaddr*>(&remote), sizeof(remote));
    if (sent < 0) return false;

    ++tx_count_;
    tx_bytes_ += static_cast<uint64_t>(sent);
    return true;
}

void UdpSocket::recv_loop()
{
    uint8_t    buf[65536];
    sockaddr_in from{};
    socklen_t   from_len = sizeof(from);

    while (running_) {
        ssize_t n = recvfrom(fd_, buf, sizeof(buf), 0,
                             reinterpret_cast<sockaddr*>(&from), &from_len);
        if (n <= 0) continue; // timeout or error

        Packet p;
        p.timestamp = std::chrono::system_clock::now();

        char ip_str[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &from.sin_addr, ip_str, sizeof(ip_str));
        p.src_ip   = ip_str;
        p.src_port = ntohs(from.sin_port);
        p.dst_ip   = cfg_.local_ip;
        p.dst_port = cfg_.local_port;
        p.data     = std::vector<uint8_t>(buf, buf + n);
        p.is_tx    = false;

        ++rx_count_;
        rx_bytes_ += static_cast<uint64_t>(n);

        if (rx_cb_) rx_cb_(p);
    }
}

// ─── PacketLog ────────────────────────────────────────────────────────────────

PacketLog::PacketLog(size_t max) : max_(max) {}

void PacketLog::push(const Packet& p)
{
    std::lock_guard<std::mutex> lk(mu_);
    if (log_.size() >= max_) log_.pop_front();
    log_.push_back(p);
}

std::vector<Packet> PacketLog::snapshot() const
{
    std::lock_guard<std::mutex> lk(mu_);
    return {log_.begin(), log_.end()};
}

void PacketLog::clear()
{
    std::lock_guard<std::mutex> lk(mu_);
    log_.clear();
}

size_t PacketLog::size() const
{
    std::lock_guard<std::mutex> lk(mu_);
    return log_.size();
}

// ─── Utilities ────────────────────────────────────────────────────────────────

std::vector<uint8_t> parse_hex(const std::string& s)
{
    std::string digits;
    for (char c : s)
        if (std::isxdigit(static_cast<unsigned char>(c)))
            digits += c;

    if (digits.size() % 2 != 0) digits = "0" + digits;

    std::vector<uint8_t> result;
    result.reserve(digits.size() / 2);
    for (size_t i = 0; i < digits.size(); i += 2)
        result.push_back(static_cast<uint8_t>(
            std::stoul(digits.substr(i, 2), nullptr, 16)));

    return result;
}

std::string to_hex(const std::vector<uint8_t>& data, size_t max_bytes)
{
    std::ostringstream oss;
    size_t limit = (max_bytes > 0 && max_bytes < data.size()) ? max_bytes : data.size();

    for (size_t i = 0; i < limit; ++i) {
        if (i > 0) oss << ' ';
        oss << std::hex << std::uppercase
            << std::setw(2) << std::setfill('0')
            << static_cast<int>(data[i]);
    }

    if (max_bytes > 0 && data.size() > max_bytes)
        oss << " ...(" << (data.size() - max_bytes) << " more)";

    return oss.str();
}

std::string format_time(const std::chrono::system_clock::time_point& tp)
{
    auto t  = std::chrono::system_clock::to_time_t(tp);
    auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
                  tp.time_since_epoch()) % 1000;
    std::tm tm{};
    localtime_r(&t, &tm);

    std::ostringstream oss;
    oss << std::put_time(&tm, "%H:%M:%S")
        << '.' << std::setw(3) << std::setfill('0') << ms.count();
    return oss.str();
}
