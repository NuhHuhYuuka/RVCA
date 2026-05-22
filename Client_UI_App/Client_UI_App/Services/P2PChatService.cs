using SecurityData.Services;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Xử lý giao tiếp P2P với Bot UitiChan và các Client khác
    //
    // Wire Protocol:
    //   Client → Bot    : StreamWriter.WriteLine(Base64-AES-CBC-encrypted)
    //   Bot    → Client : BinaryReader [string][int32][byte[]]
    //
    //   Client → Client (E2E):
    //     SEND: E2E_INIT|senderName|myPubKey  →  đợi  E2E_INIT_ACK|peerName|peerPubKey
    //           CHAT_E2E|senderName|cipherText|nonce|tag
    //     RECV: P2PListenerService.HandleE2EInitAsync (đối xứng)
    //
    //   Client → Client (File):
    //     FILE_INIT|senderName|fileName|totalChunks|sha256
    //     FILE_CHUNK|chunkIndex|base64Data   (lặp totalChunks lần)
    internal static class P2PChatService
    {
        public const string DefaultBotKey = "LTMCB_Secret_Key_2026";

        // Chỉ 1 request trực tiếp tới Bot cùng lúc — nhiều client có thể queue
        private static readonly SemaphoreSlim _botSemaphore = new(1, 1);

        // Pending relay bot requests: sessionId → TCS
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<(string text, byte[] audio)>>
            _botPending = new();

        // Gọi bởi P2PListenerService khi nhận BOT_RESPONSE từ relay poller
        internal static void CompleteBotRelayResponse(string sessionId, string encryptedText)
        {
            if (!_botPending.TryRemove(sessionId, out var tcs)) return;
            try
            {
                string text = BotCryptService.Decrypt(encryptedText, DefaultBotKey);
                tcs.TrySetResult((text, Array.Empty<byte>()));
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }

        // ── Gửi tin nhắn E2E encrypted đến Client thường ─────────────
        // Flow: E2E_INIT → E2E_INIT_ACK → CHAT_E2E (một kết nối duy nhất)
        public static async Task SendToClientAsync(
            string peerIp,
            int    peerPort,
            string senderName,
            string message)
        {
            using TcpClient client = new();
            client.SendTimeout    = 5_000;
            client.ReceiveTimeout = 5_000;
            await client.ConnectAsync(peerIp, peerPort);

            await using NetworkStream stream = client.GetStream();
            using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            // Tạo ephemeral ECDH key pair — forward secrecy
            using var keyEx = new KeyExchangeService();
            await writer.WriteLineAsync($"E2E_INIT|{senderName}|{keyEx.ExportPublicKey()}");

            string? ack = await reader.ReadLineAsync();

            if (string.IsNullOrEmpty(ack) || !ack.StartsWith("E2E_INIT_ACK|"))
            {
                // Peer cũ không hỗ trợ E2E → fallback plaintext
                await writer.WriteLineAsync($"CHAT|{senderName}|{message}");
                await Task.Delay(100);
                return;
            }

            // Derive session key từ public key của peer
            string[] ackParts  = ack.Split('|', 3);
            string   peerName  = ackParts[1];
            byte[]   sessionKey = keyEx.DeriveSessionKeyFromPeer(peerName, ackParts[2]);

            // Mã hóa AES-256-GCM rồi gửi
            var enc = SecurityService.Encrypt(message, sessionKey);
            await writer.WriteLineAsync($"CHAT_E2E|{senderName}|{enc.CipherText}|{enc.Nonce}|{enc.Tag}");
            await Task.Delay(100);
        }

        // ── Gửi file/ảnh đến Client thường (chunked 64KB P2P, relay fallback) ──
        // progress: 0–100 (phần trăm chunk đã gửi)
        public static async Task SendFileToClientAsync(
            string           peerIp,
            int              peerPort,
            string           senderName,
            string           filePath,
            IProgress<int>?  progress = null,
            string?          peerName = null)
        {
            var    fileInfo    = new FileInfo(filePath);
            const int chunkSize = 64 * 1024;
            string sha256      = FileTransferService.ComputeSha256(filePath);
            string fileName    = fileInfo.Name;

            bool ok = false;
            try
            {
                using var cts = new CancellationTokenSource(500);
                using TcpClient client = new();
                client.SendTimeout = 60_000;
                await client.ConnectAsync(peerIp, peerPort, cts.Token);

                await using NetworkStream stream = client.GetStream();
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                int totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);
                await writer.WriteLineAsync($"FILE_INIT|{senderName}|{fileName}|{totalChunks}|{sha256}");

                int idx = 0;
                foreach (var chunk in FileTransferService.SplitFile(filePath, chunkSize))
                {
                    string base64Data = Convert.ToBase64String(chunk.Data);
                    await writer.WriteLineAsync($"FILE_CHUNK|{idx}|{base64Data}");
                    progress?.Report((idx + 1) * 100 / totalChunks);
                    idx++;
                }
                await Task.Delay(200);
                ok = true;
            }
            catch { }

            if (!ok && peerName != null)
            {
                // Relay fallback: toàn bộ file base64 trong 1 relay message
                byte[] data = File.ReadAllBytes(filePath);
                string base64All = Convert.ToBase64String(data);
                await DirectoryService.RelayAsync(senderName, peerName,
                    $"FILE_RELAY|{senderName}|{fileName}|{sha256}|{base64All}");
                progress?.Report(100);
            }
            else if (!ok)
            {
                throw new IOException("Không thể kết nối tới peer để gửi file.");
            }
        }

        // ── Gửi avatar của mình tới peer (fire-and-forget, lỗi im lặng) ──
        public static async Task SendAvatarAsync(string peerIp, int peerPort, string username)
        {
            string avatarPath = AvatarService.GetUserAvatarPath(username);
            if (string.IsNullOrEmpty(avatarPath)) return;

            try
            {
                byte[] pngBytes  = File.ReadAllBytes(avatarPath);
                string base64Png = Convert.ToBase64String(pngBytes);

                using TcpClient client = new();
                client.SendTimeout = 5_000;
                await client.ConnectAsync(peerIp, peerPort);
                await using NetworkStream stream = client.GetStream();
                await using StreamWriter  writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync($"AVATAR_PUSH|{username}|{base64Png}");
                await Task.Delay(100);
            }
            catch { /* Peer offline hoặc lỗi — bỏ qua */ }
        }

        // ── Gửi 1 dòng TCP cho voice signaling (VOICE_OFFER/ANSWER/REJECT/HANGUP) ──
        public static async Task SendVoiceSignalAsync(string ip, int port, string line)
        {
            try
            {
                using TcpClient client = new();
                client.SendTimeout = 5_000;
                await client.ConnectAsync(ip, port);
                await using NetworkStream stream = client.GetStream();
                await using StreamWriter  writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(line);
                await Task.Delay(100);
            }
            catch { /* Peer offline hoặc từ chối kết nối */ }
        }

        // ── Gửi text message với relay fallback (khi P2P bị NAT chặn) ──
        // Thử E2E P2P trước (timeout 500ms); nếu fail → relay plaintext qua server
        public static async Task SendToClientWithRelayAsync(
            string peerIp, int peerPort,
            string senderName, string peerName,
            string message)
        {
            bool ok = false;
            try
            {
                using var cts = new CancellationTokenSource(500);
                using TcpClient client = new();
                client.SendTimeout    = 5_000;
                client.ReceiveTimeout = 5_000;
                await client.ConnectAsync(peerIp, peerPort, cts.Token);

                await using NetworkStream stream = client.GetStream();
                using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                using var keyEx = new KeyExchangeService();
                await writer.WriteLineAsync($"E2E_INIT|{senderName}|{keyEx.ExportPublicKey()}");

                string? ack = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(ack) || !ack.StartsWith("E2E_INIT_ACK|"))
                {
                    await writer.WriteLineAsync($"CHAT|{senderName}|{message}");
                }
                else
                {
                    string[] ackParts  = ack.Split('|', 3);
                    string   peerN     = ackParts[1];
                    byte[]   sessionKey = keyEx.DeriveSessionKeyFromPeer(peerN, ackParts[2]);
                    var enc = SecurityService.Encrypt(message, sessionKey);
                    await writer.WriteLineAsync($"CHAT_E2E|{senderName}|{enc.CipherText}|{enc.Nonce}|{enc.Tag}");
                }
                await Task.Delay(100);
                ok = true;
            }
            catch { }

            if (!ok)
                await DirectoryService.RelayAsync(senderName, peerName, $"CHAT|{senderName}|{message}");
        }

        // ── Gửi voice/video signaling với relay fallback ──────────────
        // Thử P2P TCP trước (timeout 2.5s); nếu fail → relay qua server
        public static async Task SendVoiceSignalWithRelayAsync(
            string ip, int port, string line,
            string fromUser, string toUser)
        {
            bool ok = false;
            try
            {
                using var cts    = new CancellationTokenSource(500);
                using TcpClient client = new();
                await client.ConnectAsync(ip, port, cts.Token);
                await using NetworkStream stream = client.GetStream();
                await using StreamWriter  writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(line);
                await Task.Delay(80);
                ok = true;
            }
            catch { }

            if (!ok)
                await DirectoryService.RelayAsync(fromUser, toUser, line);
        }

        // ── Gửi tin nhắn đến Bot với relay fallback ──────────────────
        // Thử direct TCP với 2s connect timeout; nếu fail → relay qua server (text-only, không audio)
        public static async Task<(string textResponse, byte[] audioData)> SendMessageWithRelayAsync(
            string peerIp, int peerPort,
            string fromUser, string peerName,
            string plainText, string secretKey = DefaultBotKey)
        {
            // Direct path với 2s connect timeout
            try
            {
                await _botSemaphore.WaitAsync();
                try
                {
                    return await Task.Run(async () =>
                    {
                        using TcpClient tcpClient = new();
                        tcpClient.ReceiveTimeout = 90_000;
                        using var connectCts = new CancellationTokenSource(2_000);
                        await tcpClient.ConnectAsync(peerIp, peerPort, connectCts.Token);

                        await using NetworkStream stream = tcpClient.GetStream();
                        string encryptedOut = BotCryptService.Encrypt(plainText, secretKey);
                        byte[] sendBytes = Encoding.UTF8.GetBytes(encryptedOut + "\n");
                        await stream.WriteAsync(sendBytes);

                        using BinaryReader br = new(stream, Encoding.UTF8, leaveOpen: true);
                        string encryptedIn = br.ReadString();
                        int    audioLen    = br.ReadInt32();
                        byte[] audio       = audioLen > 0 ? br.ReadBytes(audioLen) : Array.Empty<byte>();
                        return (BotCryptService.Decrypt(encryptedIn, secretKey), audio);
                    });
                }
                finally { _botSemaphore.Release(); }
            }
            catch { }

            // Relay fallback — bot sẽ poll, xử lý, và relay lại kết quả
            string sessionId = Guid.NewGuid().ToString("N")[..8];
            var tcs = new TaskCompletionSource<(string, byte[])>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _botPending[sessionId] = tcs;

            try
            {
                string encrypted = BotCryptService.Encrypt(plainText, secretKey);
                await DirectoryService.RelayAsync(fromUser, peerName,
                    $"BOT_REQUEST|{sessionId}|{fromUser}|{encrypted}");
                return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(90));
            }
            finally
            {
                _botPending.TryRemove(sessionId, out _);
            }
        }

        // ── Gửi tin nhắn đến Bot UitiChan (queue nếu nhiều client cùng nhắn) ──
        public static async Task<(string textResponse, byte[] audioData)> SendMessageAsync(
            string peerIp,
            int    peerPort,
            string plainText,
            string secretKey = DefaultBotKey)
        {
            // Chờ nếu đang có request Bot khác đang chạy
            await _botSemaphore.WaitAsync();
            try
            {
                return await Task.Run(async () =>
                {
                    using TcpClient tcpClient = new();
                    tcpClient.ReceiveTimeout = 60_000;
                    await tcpClient.ConnectAsync(peerIp, peerPort);

                    await using NetworkStream stream = tcpClient.GetStream();

                    // SEND: mã hóa AES-256-CBC rồi ghi qua StreamWriter
                    string encryptedOut = BotCryptService.Encrypt(plainText, secretKey);

                    // Ghi raw bytes (KHÔNG dùng StreamWriter) để tránh BOM (0xEF 0xBB 0xBF)
                    // mà StreamWriter(Encoding.UTF8) tự thêm vào đầu stream.
                    // BOM làm hỏng chuỗi Base64 → Bot.Decrypt thất bại.
                    byte[] sendBytes = Encoding.UTF8.GetBytes(encryptedOut + "\n");
                    await stream.WriteAsync(sendBytes);

                    // RECV: 3 lớp binary — BinaryReader.ReadString / ReadInt32 / ReadBytes
                    using BinaryReader br = new(stream, Encoding.UTF8, leaveOpen: true);
                    string encryptedIn = br.ReadString();
                    int    audioLen    = br.ReadInt32();
                    byte[] audio       = audioLen > 0 ? br.ReadBytes(audioLen) : Array.Empty<byte>();

                    string plainResponse = BotCryptService.Decrypt(encryptedIn, secretKey);
                    return (plainResponse, audio);
                });
            }
            finally
            {
                _botSemaphore.Release();
            }
        }
    }
}
