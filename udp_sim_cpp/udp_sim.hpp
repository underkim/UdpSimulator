#pragma once

#include <string>
#include <vector>
#include <functional>
#include <atomic>
#include <thread>
#include <mutex>
#include <deque>
#include <chrono>
#include <cstdint>

// ─── Data types ───────────────────────────────────────────────────────────────

struct Packet {
    std::chrono::system_clock::time_point timestamp;
    std::string src_ip;
    int         src_port = 0;
    std::string dst_ip;
    int         dst_port = 0;
    std::vector<uint8_t> data;
    bool is_tx = false;
};

enum class UdpMode { SendOnly, RecvOnly, Bidirectional };

struct UdpConfig {
    std::string local_ip    = "0.0.0.0";
    int         local_port  = 12345;
    std::string remote_ip   = "127.0.0.1";
    int         remote_port = 12346;
    UdpMode     mode        = UdpMode::Bidirectional;
    int         mtu         = 1500;
};

// ─── UdpSocket ────────────────────────────────────────────────────────────────

class UdpSocket {
public:
    using RxCallback = std::function<void(const Packet&)>;

    explicit UdpSocket(const UdpConfig& cfg);
    ~UdpSocket();

    bool start(RxCallback cb);
    void stop();
    bool send(const std::vector<uint8_t>& data);

    bool     is_running()   const { return running_; }
    int      max_payload()  const { return cfg_.mtu - 20 - 8; } // IP hdr + UDP hdr
    uint64_t tx_count()     const { return tx_count_; }
    uint64_t rx_count()     const { return rx_count_; }
    uint64_t tx_bytes()     const { return tx_bytes_; }
    uint64_t rx_bytes()     const { return rx_bytes_; }

private:
    void recv_loop();

    UdpConfig          cfg_;
    int                fd_ = -1;
    std::atomic<bool>  running_{false};
    std::thread        recv_thread_;
    RxCallback         rx_cb_;

    std::atomic<uint64_t> tx_count_{0}, rx_count_{0};
    std::atomic<uint64_t> tx_bytes_{0}, rx_bytes_{0};
};

// ─── PacketLog ────────────────────────────────────────────────────────────────

class PacketLog {
public:
    explicit PacketLog(size_t max = 500);

    void                 push(const Packet& p);
    std::vector<Packet>  snapshot() const;
    void                 clear();
    size_t               size() const;

private:
    mutable std::mutex  mu_;
    std::deque<Packet>  log_;
    size_t              max_;
};

// ─── Utilities ────────────────────────────────────────────────────────────────

std::vector<uint8_t> parse_hex(const std::string& s);
std::string          to_hex(const std::vector<uint8_t>& data, size_t max_bytes = 0);
std::string          format_time(const std::chrono::system_clock::time_point& tp);
