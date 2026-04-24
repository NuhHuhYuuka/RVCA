using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecurityData.Models
{
    public class KeyExchangePacket
    {
        public string Type { get; set; } = "KEY_EXCHANGE";
        public string Sender { get; set; }
        public string Receiver { get; set; }
        public string PublicKey { get; set; } // Base64
    }
}
