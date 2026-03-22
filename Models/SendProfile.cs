using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace UdpSimulator.Models
{
    public class SendProfile
    {
        public string Name { get; set; }
        public List<SendField> Header { get; set; }
        public List<SendField> Data { get; set; }

        public SendProfile()
        {
            Name = "";
            Header = new List<SendField>();
            Data = new List<SendField>();
        }
    }
}
