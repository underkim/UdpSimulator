namespace UdpSimulator.Models
{
    public class PacketLogEntry
    {
        public DateTime   Timestamp  { get; init; }
        public string     Direction  { get; init; } = "";  // "TX" or "RX"
        public string     Peer       { get; init; } = "";  // ip:port
        public int        Size       { get; init; }
        public byte[]     Data       { get; init; } = Array.Empty<byte>();
        public string     HexPreview => BitConverter.ToString(Data[..Math.Min(16, Data.Length)]).Replace("-", " ")
                                        + (Data.Length > 16 ? " ..." : "");
        public string     HexFull    => BitConverter.ToString(Data).Replace("-", " ");
    }
}
