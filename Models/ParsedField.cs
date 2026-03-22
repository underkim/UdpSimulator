using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
