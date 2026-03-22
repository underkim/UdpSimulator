namespace UdpSimulator.Models
{
    public class Field
    {

        public string Name { get; set; }
        public FieldType Type { get; set; }
        public int Size { get; set; }
        public string ByteOrder { get; set; }
        public Field()
        {
            Name = "";
            Type = FieldType.STRING;
            Size = 0;
            ByteOrder = "LITTLE";
        }

    }
}
