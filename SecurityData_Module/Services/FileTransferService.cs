using SecurityData.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SecurityData.Services
{
    public class FileTransferService
    {
        public static IEnumerable<FileChunk> SplitFile(string filePath, int chunkSize = 64 * 1024)
        {
            var fileInfo = new FileInfo(filePath);
            int totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);
            string transferId = Guid.NewGuid().ToString();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[chunkSize];

                for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    int bytesRead = fs.Read(buffer, 0, chunkSize);
                    byte[] data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    yield return new FileChunk
                    {
                        TransferId = transferId,
                        FileName = fileInfo.Name,
                        ChunkIndex = chunkIndex,
                        TotalChunks = totalChunks,
                        Data = data,
                        IsLastChunk = chunkIndex == totalChunks - 1,
                        FileSize = fileInfo.Length,
                        Sha256 = chunkIndex == 0 ? ComputeSha256(filePath) : null
                    };
                }
            }
        }

        public static string ComputeSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                return BitConverter.ToString(sha256.ComputeHash(fs)).Replace("-", string.Empty);
            }
        }

        public static void AppendChunkToFile(string outputPath, byte[] data)
        {
            string folder = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            using (var fs = new FileStream(outputPath, FileMode.Append, FileAccess.Write))
            {
                fs.Write(data, 0, data.Length);
            }
        }

        public static string GetReceiveFolder()
        {
            string savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ChatApp_Files");

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            return savePath;
        }
    }
}