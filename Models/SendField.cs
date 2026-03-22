namespace UdpSimulator.Models
{
    public class SendField : Field
    {

        public ValueConfig Value { get; set; }

        public SendField()
        {
            Value = new ValueConfig();
        }

    }
}
