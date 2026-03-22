namespace UdpSimulator.Models
{
    public class ValueConfig
    {

        public ValueType Type { get; set; }
        public string Data { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }

        public ValueConfig()
        {
            Type = ValueType.Fixed;
            Data = "";
            Min = 0;
            Max = 100;
        }
    }
}
