namespace UdpSimulator.Models
{
    public class ParsedField
    {
        public string Name { get; set; }
        public string RawValue { get; set; }
        public string ConvertedValue { get; set; }
        public string Unit { get; set; }

        public ParsedField()
        {
            Name = "";
            RawValue = "";
            ConvertedValue = "";
            Unit = "";
        }
    }
}
