using System.Net;
using System.Net.Sockets;
using UdpSimulator.Models;

namespace UdpSimulator.Services
{
    public class UdpService : IDisposable
    {
        public event Action<PacketLogEntry>? PacketReceived;

        private UdpClient?  _client;
        private UdpConfig?  _config;
        private bool        _running;
        private ulong       _txCount, _rxCount, _txBytes, _rxBytes;

        public ulong TxCount => _txCount;
        public ulong RxCount => _rxCount;
        public ulong TxBytes => _txBytes;
        public ulong RxBytes => _rxBytes;

        public bool Start(UdpConfig cfg)
        {
            Stop();
            _config = cfg;

            try
            {
                _client = new UdpClient();
                _client.Client.SetSocketOption(SocketOptionLevel.Socket,
                                               SocketOptionName.ReuseAddress, true);
                _client.Client.Bind(new IPEndPoint(IPAddress.Parse(cfg.LocalIp), cfg.LocalPort));
                _client.Client.ReceiveTimeout = 200;
            }
            catch { return false; }

            _running = true;
            if (cfg.Mode != UdpMode.SendOnly)
                Task.Run(ReceiveLoop);

            return true;
        }

        public void Stop()
        {
            _running = false;
            _client?.Close();
            _client?.Dispose();
            _client = null;
        }

        public bool Send(byte[] data)
        {
            if (_client == null || _config == null) return false;
            if (_config.Mode == UdpMode.ReceiveOnly) return false;
            try
            {
                var ep = new IPEndPoint(IPAddress.Parse(_config.RemoteIp), _config.RemotePort);
                int sent = _client.Send(data, data.Length, ep);
                _txCount++;
                _txBytes += (ulong)sent;
                return true;
            }
            catch { return false; }
        }

        private async Task ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    var result = await _client!.ReceiveAsync();
                    _rxCount++;
                    _rxBytes += (ulong)result.Buffer.Length;

                    var entry = new PacketLogEntry
                    {
                        Timestamp = DateTime.Now,
                        Direction = "RX",
                        Peer      = result.RemoteEndPoint.ToString(),
                        Size      = result.Buffer.Length,
                        Data      = result.Buffer,
                    };
                    PacketReceived?.Invoke(entry);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut) { }
                catch (ObjectDisposedException) { break; }
                catch { /* absorb other errors */ }
            }
        }

        public void Dispose() => Stop();
    }
}
