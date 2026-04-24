using SecurityData.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Lắng nghe kết nối P2P đến từ các client khác
    //
    // Các loại tin nhắn được xử lý:
    //   CHAT|senderName|plainMessage              → backward-compat, plaintext
    //   E2E_INIT|senderName|pubKeyBase64          → handshake ECDH, sau đó đọc CHAT_E2E
    //   FILE_INIT|senderName|fileName|totalChunks|sha256 → nhận file chunked
    internal static class P2PListenerService
    {
        private static TcpListener?             _listener;
        private static CancellationTokenSource? _cts;
        private static string                   _myUsername = "";

        public static int ListeningPort { get; private set; }

        // Sự kiện: (senderName, plainMessage)
        public static event Action<string, string>? MessageReceived;

        // Sự kiện: (senderName, fileName, localSavePath)
        public static event Action<string, string, string>? FileReceived;

        // Khởi động listener trên port ngẫu nhiên (OS chọn)
        public static int Start(string username)
        {
            _myUsername = username;
            _cts        = new CancellationTokenSource();
            _listener   = new TcpListener(IPAddress.Any, 0);
            _listener.Start();
            ListeningPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Task.Run(() => AcceptLoop(_cts.Token));
            return ListeningPort;
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _listener?.Stop();
        }

        private static async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener!.AcceptTcpClientAsync(ct);
                    _ = Task.Run(() => HandlePeer(client), ct);
                }
                catch (OperationCanceledException) { break; }
                catch { /* Lỗi accept tạm thời — bỏ qua */ }
            }
        }

        private static async Task HandlePeer(TcpClient client)
        {
            try
            {
                await using NetworkStream stream = client.GetStream();
                using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                string? firstLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(firstLine)) return;

                if (firstLine.StartsWith("CHAT|"))
                {
                    // Backward compat: plaintext (không mã hóa)
                    string[] parts = firstLine.Split('|', 3);
                    if (parts.Length == 3)
                        MessageReceived?.Invoke(parts[1], parts[2]);
                }
                else if (firstLine.StartsWith("E2E_INIT|"))
                {
                    await HandleE2EInitAsync(firstLine, reader, writer);
                }
                else if (firstLine.StartsWith("FILE_INIT|"))
                {
                    await HandleFileTransferAsync(firstLine, reader);
                }
            }
            catch { /* Bỏ qua lỗi đọc */ }
            finally
            {
                client.Close();
            }
        }

        // ── Nhận tin nhắn E2E (ECDH ephemeral + AES-256-GCM) ────────
        private static async Task HandleE2EInitAsync(
            string initLine, StreamReader reader, StreamWriter writer)
        {
            // initLine: E2E_INIT|senderName|theirPubKeyBase64
            string[] parts = initLine.Split('|', 3);
            if (parts.Length < 3) return;

            string senderName  = parts[1];
            string theirPubKey = parts[2];

            // Tạo ephemeral ECDH key pair — forward secrecy cho mỗi kết nối
            using var keyEx = new KeyExchangeService();
            byte[] sessionKey = keyEx.DeriveSessionKeyFromPeer(senderName, theirPubKey);

            // Gửi lại public key của mình để sender tính cùng sessionKey
            await writer.WriteLineAsync($"E2E_INIT_ACK|{_myUsername}|{keyEx.ExportPublicKey()}");

            // Đọc tin nhắn mã hóa: CHAT_E2E|senderName|cipherText|nonce|tag
            string? chatLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(chatLine)) return;

            string[] chatParts = chatLine.Split('|', 5);
            if (chatParts.Length < 5 || chatParts[0] != "CHAT_E2E") return;

            string plainText = SecurityService.Decrypt(
                chatParts[2], chatParts[3], chatParts[4], sessionKey);

            MessageReceived?.Invoke(senderName, plainText);
        }

        // ── Nhận file transfer (chunked, SHA-256 verify) ─────────────
        private static async Task HandleFileTransferAsync(string initLine, StreamReader reader)
        {
            // initLine: FILE_INIT|senderName|fileName|totalChunks|sha256
            string[] parts = initLine.Split('|', 5);
            if (parts.Length < 5) return;

            string senderName   = parts[1];
            string fileName     = parts[2];
            int    totalChunks  = int.Parse(parts[3]);
            string expectedSha  = parts[4];

            string saveFolder = FileTransferService.GetReceiveFolder();
            string savePath   = Path.Combine(saveFolder, fileName);

            if (File.Exists(savePath)) File.Delete(savePath);

            int received = 0;
            while (received < totalChunks)
            {
                string? line = await reader.ReadLineAsync();
                if (line is null) break;

                // FILE_CHUNK|chunkIndex|base64Data
                string[] chunkParts = line.Split('|', 3);
                if (chunkParts.Length < 3 || chunkParts[0] != "FILE_CHUNK") break;

                byte[] data = Convert.FromBase64String(chunkParts[2]);
                FileTransferService.AppendChunkToFile(savePath, data);
                received++;
            }

            if (received == totalChunks)
            {
                string actualSha = FileTransferService.ComputeSha256(savePath);
                if (actualSha.Equals(expectedSha, StringComparison.OrdinalIgnoreCase))
                    FileReceived?.Invoke(senderName, fileName, savePath);
                else
                    File.Delete(savePath); // File bị hỏng → xóa
            }
        }
    }
}
