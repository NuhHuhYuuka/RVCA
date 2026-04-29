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
    //   CHAT|senderName|plainMessage                              → backward-compat plaintext
    //   E2E_INIT|senderName|pubKeyBase64                         → ECDH handshake → CHAT_E2E
    //   FILE_INIT|senderName|fileName|totalChunks|sha256         → file transfer chunked
    //   GROUP_MSG|groupId|groupName|sender|message               → tin nhắn nhóm
    //   GROUP_FILE_INIT|groupId|groupName|sender|fileName|n|sha  → file trong nhóm
    internal static class P2PListenerService
    {
        private static TcpListener?             _listener;
        private static CancellationTokenSource? _cts;
        private static string                   _myUsername = "";

        public static int ListeningPort { get; private set; }

        // Sự kiện: (senderName, plainMessage)
        public static event Action<string, string>?                MessageReceived;

        // Sự kiện: (senderName, fileName, localSavePath)
        public static event Action<string, string, string>?        FileReceived;

        // Sự kiện nhóm: (groupId, groupName, sender, message)
        public static event Action<string, string, string, string>? GroupMessageReceived;

        // Sự kiện file nhóm: (groupId, groupName, sender, fileName, localSavePath)
        public static event Action<string, string, string, string, string>? GroupFileReceived;

        // Voice signaling: (callerName, callerUdpPort)
        public static event Action<string, string>? IncomingVoiceCall;
        // Voice signaling: (peerName, answererUdpPort)
        public static event Action<string, string>? VoiceCallAnswered;
        // Voice signaling: (peerName)
        public static event Action<string>?         VoiceCallRejected;
        // Voice signaling: (peerName)
        public static event Action<string>?         VoiceCallHungUp;

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
                    await HandleFileTransferAsync(firstLine, reader, isGroup: false);
                }
                else if (firstLine.StartsWith("GROUP_MSG|"))
                {
                    // GROUP_MSG|groupId|groupName|sender|message
                    // Split tối đa 5 phần để message chứa '|' vẫn nguyên
                    string[] parts = firstLine.Split('|', 5);
                    if (parts.Length == 5)
                        GroupMessageReceived?.Invoke(parts[1], parts[2], parts[3], parts[4]);
                }
                else if (firstLine.StartsWith("GROUP_FILE_INIT|"))
                {
                    await HandleGroupFileTransferAsync(firstLine, reader);
                }
                else if (firstLine.StartsWith("VOICE_OFFER|"))
                {
                    string[] parts = firstLine.Split('|', 3);
                    if (parts.Length == 3)
                        IncomingVoiceCall?.Invoke(parts[1], parts[2]);
                }
                else if (firstLine.StartsWith("VOICE_ANSWER|"))
                {
                    string[] parts = firstLine.Split('|', 3);
                    if (parts.Length == 3)
                        VoiceCallAnswered?.Invoke(parts[1], parts[2]);
                }
                else if (firstLine.StartsWith("VOICE_REJECT|"))
                {
                    string[] parts = firstLine.Split('|', 2);
                    if (parts.Length == 2)
                        VoiceCallRejected?.Invoke(parts[1]);
                }
                else if (firstLine.StartsWith("VOICE_HANGUP|"))
                {
                    string[] parts = firstLine.Split('|', 2);
                    if (parts.Length == 2)
                        VoiceCallHungUp?.Invoke(parts[1]);
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

        // ── Nhận file transfer cá nhân (chunked, SHA-256 verify) ─────
        private static async Task HandleFileTransferAsync(
            string initLine, StreamReader reader, bool isGroup)
        {
            // initLine: FILE_INIT|senderName|fileName|totalChunks|sha256
            string[] parts = initLine.Split('|', 5);
            if (parts.Length < 5) return;

            string senderName  = parts[1];
            string fileName    = parts[2];
            int    totalChunks = int.Parse(parts[3]);
            string expectedSha = parts[4];

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
                    File.Delete(savePath);
            }
        }

        // ── Nhận file transfer nhóm (chunked, SHA-256 verify) ────────
        private static async Task HandleGroupFileTransferAsync(string initLine, StreamReader reader)
        {
            // GROUP_FILE_INIT|groupId|groupName|sender|fileName|totalChunks|sha256
            string[] parts = initLine.Split('|', 7);
            if (parts.Length < 7) return;

            string groupId     = parts[1];
            string groupName   = parts[2];
            string senderName  = parts[3];
            string fileName    = parts[4];
            int    totalChunks = int.Parse(parts[5]);
            string expectedSha = parts[6];

            string saveFolder = FileTransferService.GetReceiveFolder();
            string savePath   = Path.Combine(saveFolder, fileName);

            if (File.Exists(savePath)) File.Delete(savePath);

            int received = 0;
            while (received < totalChunks)
            {
                string? line = await reader.ReadLineAsync();
                if (line is null) break;

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
                    GroupFileReceived?.Invoke(groupId, groupName, senderName, fileName, savePath);
                else
                    File.Delete(savePath);
            }
        }
    }
}
