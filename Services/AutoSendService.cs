using UdpSimulator.Models;

namespace UdpSimulator.Services
{
    public enum AutoPayloadMode { Fixed, Counter, Random }

    public class AutoSendService : IDisposable
    {
        public event Action<PacketLogEntry>? PacketSent;

        private readonly UdpService  _udp;
        private readonly PcapService _pcap;

        private CancellationTokenSource? _cts;
        private Task?                    _task;

        public bool            IsRunning    { get; private set; }
        public int             IntervalMs   { get; set; } = 1000;
        public AutoPayloadMode PayloadMode  { get; set; } = AutoPayloadMode.Fixed;
        public byte[]          Payload      { get; set; } = new byte[] { 0x55, 0x44, 0x50, 0x53, 0x49, 0x4D, 0x00, 0x01 };
        public int             CounterOffset { get; set; } = 0;
        public uint            Counter      { get; private set; }

        private readonly Random _rng = new();

        public AutoSendService(UdpService udp, PcapService pcap)
        {
            _udp  = udp;
            _pcap = pcap;
        }

        public void Start(UdpConfig cfg)
        {
            Stop();
            Counter = 0;
            _cts  = new CancellationTokenSource();
            _task = Task.Run(() => RunLoop(cfg, _cts.Token));
            IsRunning = true;
        }

        public void Stop()
        {
            _cts?.Cancel();
            _task?.Wait(500);
            _cts?.Dispose();
            _cts  = null;
            _task = null;
            IsRunning = false;
        }

        private async Task RunLoop(UdpConfig cfg, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                byte[] payload = BuildPayload();
                if (_udp.Send(payload))
                {
                    var entry = new PacketLogEntry
                    {
                        Timestamp = DateTime.Now,
                        Direction = "TX",
                        Peer      = $"{cfg.RemoteIp}:{cfg.RemotePort}",
                        Size      = payload.Length,
                        Data      = payload,
                    };
                    _pcap.Write(entry);
                    PacketSent?.Invoke(entry);
                }

                try { await Task.Delay(IntervalMs, ct); }
                catch (TaskCanceledException) { break; }
            }
        }

        private byte[] BuildPayload()
        {
            byte[] data = (byte[])Payload.Clone();

            switch (PayloadMode)
            {
                case AutoPayloadMode.Random:
                    _rng.NextBytes(data);
                    break;

                case AutoPayloadMode.Counter:
                    uint seq = Counter++;
                    int  off = CounterOffset;
                    if (data.Length < off + 4) Array.Resize(ref data, off + 4);
                    data[off + 0] = (byte) seq;
                    data[off + 1] = (byte)(seq >>  8);
                    data[off + 2] = (byte)(seq >> 16);
                    data[off + 3] = (byte)(seq >> 24);
                    break;
            }
            return data;
        }

        public void Dispose() => Stop();
    }
}
