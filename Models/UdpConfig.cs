namespace UdpSimulator.Models
{
    public class UdpConfig
    {


        public string LocalIp { get; set; }
        public int LocalPort { get; set; }
        public string RemoteIp { get; set; }
        public int RemotePort { get; set; }
        public UdpMode Mode { get; set; }

        public UdpConfig()
        {
            LocalIp = "0.0.0.0";
            LocalPort = 0;
            RemoteIp = "127.0.0.1";
            RemotePort = 0;
            Mode = UdpMode.Bidirectional;
        }
    }
}
