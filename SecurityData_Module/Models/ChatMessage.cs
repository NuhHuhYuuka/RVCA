using System;

namespace SecurityData.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public string Sender { get; set; }
        public string Receiver { get; set; }

        // Lưu ciphertext trong DB local
        public string Content { get; set; }

        public DateTime Timestamp { get; set; }
        public bool IsFile { get; set; }

        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string TransferId { get; set; }

        // AES-GCM metadata
        public string Nonce { get; set; }
        public string Tag { get; set; }
    }
}