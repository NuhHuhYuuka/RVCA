using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecurityData.Models
{
    public class EncryptionResult
    {
        public string CipherText { get; set; }
        public string Nonce { get; set; }
        public string Tag { get; set; }
    }
}