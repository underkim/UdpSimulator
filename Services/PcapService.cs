using System.Net;
using UdpSimulator.Models;

namespace UdpSimulator.Services
{
    /// <summary>
    /// Writes a minimal pcap file (LINKTYPE_RAW = 101) with synthesised
    /// IPv4/UDP headers so Wireshark can decode the payload directly.
    /// </summary>
    public class PcapService : IDisposable
    {
        private FileStream? _stream;
        private readonly object _lock = new();

        public string? FilePath { get; private set; }
        public bool    IsOpen   => _stream != null;

        public bool Open(string path)
        {
            lock (_lock)
            {
                Close();
                try
                {
                    _stream  = File.Open(path, FileMode.Create, FileAccess.Write);
                    FilePath = path;
                    WriteGlobalHeader();
                    return true;
                }
                catch { _stream = null; FilePath = null; return false; }
            }
        }

        public void Write(PacketLogEntry entry)
        {
            lock (_lock)
            {
                if (_stream == null) return;
                try
                {
                    var record = BuildRecord(entry);
                    _stream.Write(record);
                    _stream.Flush();
                }
                catch { /* non-fatal */ }
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                _stream?.Dispose();
                _stream  = null;
                FilePath = null;
            }
        }

        public void Dispose() => Close();

        // ── Private helpers ───────────────────────────────────────────────────

        private void WriteGlobalHeader()
        {
            // pcap global header: magic, version, zone, sigfigs, snaplen, linktype
            Span<byte> hdr = stackalloc byte[24];
            WriteU32LE(hdr, 0, 0xa1b2c3d4); // magic
            WriteU16LE(hdr, 4, 2);           // version major
            WriteU16LE(hdr, 6, 4);           // version minor
            WriteU32LE(hdr, 8,  0);          // thiszone
            WriteU32LE(hdr, 12, 0);          // sigfigs
            WriteU32LE(hdr, 16, 65535);      // snaplen
            WriteU32LE(hdr, 20, 101);        // LINKTYPE_RAW
            _stream!.Write(hdr);
        }

        private static byte[] BuildRecord(PacketLogEntry entry)
        {
            // Parse peer address (ip:port)
            ParsePeer(entry.Peer, out string peerIp, out int peerPort);
            string localIp = "0.0.0.0";

            uint srcIp = ToU32(entry.Direction == "TX" ? localIp  : peerIp);
            uint dstIp = ToU32(entry.Direction == "TX" ? peerIp   : localIp);
            ushort srcPort = (ushort)(entry.Direction == "TX" ? 0        : peerPort);
            ushort dstPort = (ushort)(entry.Direction == "TX" ? peerPort : 0);

            ushort udpLen  = (ushort)(8 + entry.Data.Length);
            ushort ipLen   = (ushort)(20 + udpLen);
            uint   capLen  = (uint)(20 + udpLen);

            var ts   = entry.Timestamp;
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            uint tsSec  = (uint)(ts.ToUniversalTime() - epoch).TotalSeconds;
            uint tsUsec = (uint)(ts.Millisecond * 1000);

            byte[] buf = new byte[16 + 20 + 8 + entry.Data.Length];
            int off = 0;

            // pcap packet record header
            WriteU32LE(buf, off, tsSec);  off += 4;
            WriteU32LE(buf, off, tsUsec); off += 4;
            WriteU32LE(buf, off, capLen); off += 4;
            WriteU32LE(buf, off, capLen); off += 4;

            // IPv4 header
            buf[off++] = 0x45; buf[off++] = 0x00;
            buf[off++] = (byte)(ipLen >> 8); buf[off++] = (byte)ipLen;
            off += 4; // id, flags/frag
            buf[off++] = 64;  // TTL
            buf[off++] = 17;  // UDP
            off += 2; // checksum (0)
            WriteU32BE(buf, off, srcIp); off += 4;
            WriteU32BE(buf, off, dstIp); off += 4;

            // UDP header
            buf[off++] = (byte)(srcPort >> 8); buf[off++] = (byte)srcPort;
            buf[off++] = (byte)(dstPort >> 8); buf[off++] = (byte)dstPort;
            buf[off++] = (byte)(udpLen  >> 8); buf[off++] = (byte)udpLen;
            off += 2; // checksum

            entry.Data.CopyTo(buf, off);
            return buf;
        }

        private static void ParsePeer(string peer, out string ip, out int port)
        {
            ip   = "0.0.0.0";
            port = 0;
            if (string.IsNullOrEmpty(peer)) return;
            int colon = peer.LastIndexOf(':');
            if (colon < 0) return;
            ip   = peer[..colon];
            int.TryParse(peer[(colon + 1)..], out port);
        }

        private static uint ToU32(string ip)
        {
            if (!IPAddress.TryParse(ip, out var addr)) return 0;
            var b = addr.GetAddressBytes();
            return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
        }

        private static void WriteU16LE(Span<byte> buf, int off, ushort v)
        { buf[off] = (byte)v; buf[off+1] = (byte)(v >> 8); }

        private static void WriteU32LE(Span<byte> buf, int off, uint v)
        { buf[off]=(byte)v; buf[off+1]=(byte)(v>>8); buf[off+2]=(byte)(v>>16); buf[off+3]=(byte)(v>>24); }

        private static void WriteU32LE(byte[] buf, int off, uint v)
        { buf[off]=(byte)v; buf[off+1]=(byte)(v>>8); buf[off+2]=(byte)(v>>16); buf[off+3]=(byte)(v>>24); }

        private static void WriteU32BE(byte[] buf, int off, uint v)
        { buf[off]=(byte)(v>>24); buf[off+1]=(byte)(v>>16); buf[off+2]=(byte)(v>>8); buf[off+3]=(byte)v; }
    }
}
