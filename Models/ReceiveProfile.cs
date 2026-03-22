using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdpSimulator.Models
{
    public class ReceiveProfile
    {


        public string Name { get; set; }
        public List<ReceiveField> Header { get; set; }
        public List<ReceiveField> Data { get; set; }

        public ReceiveProfile()
        {
            Name = "";
            Header = new List<ReceiveField>();
            Data = new List<ReceiveField>();

        }

    }
}
