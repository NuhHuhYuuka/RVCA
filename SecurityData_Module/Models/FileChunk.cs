using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecurityData.Models
{
    public class FileChunk
    {
        public string TransferId { get; set; }
        public string FileName { get; set; }
        public int ChunkIndex { get; set; }
        public int TotalChunks { get; set; }
        public byte[] Data { get; set; }
        public bool IsLastChunk { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; }
    }
}