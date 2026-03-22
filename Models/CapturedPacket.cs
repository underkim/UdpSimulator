namespace UdpSimulator.Models
{
    public class CapturedPacket
    {
        public DateTime Timestamp { get; set; }
        public string SrcIp { get; set; } = "";
        public int SrcPort { get; set; }
        public string DstIp { get; set; } = "";
        public int DstPort { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public int Size => Data.Length;
        public PacketDirection Direction { get; set; }
        public string HexData => BitConverter
            .ToString(Data).Replace("-", " ");
    }
}
