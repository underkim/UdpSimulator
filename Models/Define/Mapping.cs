namespace UdpSimulator.Models
{
    public class Mapping
    {
        public MappingType Type { get; set; }
        public List<EnumMapping> Values { get; set; }
        public List<RangeMapping> Ranges { get; set; }
        public string Formula { get; set; }
        public string Unit { get; set; } = "";

        public Mapping()
        {
            Type = MappingType.Formula;
            Values = new();
            Ranges = new();
            Formula = "raw";
            Unit = "";

        }
    }
}
