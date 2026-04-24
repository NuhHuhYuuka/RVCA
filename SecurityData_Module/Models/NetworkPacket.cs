using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecurityData.Models
{
    public class NetworkPacket
    {
        // TEXT, FILE_META, FILE_CHUNK, FILE_END, KEY_EXCHANGE
        public string Type { get; set; }

        public string Sender { get; set; }
        public string Receiver { get; set; }

        // Text message
        public string EncryptedContent { get; set; }
        public string Nonce { get; set; }   // Base64
        public string Tag { get; set; }     // Base64

        // Key exchange
        public string PublicKey { get; set; } // Base64

        // File transfer
        public string TransferId { get; set; }
        public string FileName { get; set; }
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public byte[] ChunkData { get; set; }
        public long FileSize { get; set; }
        public string Hash { get; set; }
    }
}