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

        // Sự kiện đổi tên nhóm real-time: (groupId, newName)
        public static event Action<string, string>? GroupRenamed;

        // Typing indicator: (senderName)
        public static event Action<string>?         TypingReceived;

        // Avatar nhận từ peer: (username)
        public static event Action<string>?         AvatarReceived;

        // Group voice channel: (groupId, username, senderIp, udpPort, senderTcpPort)
        public static event Action<string, string, string, int, int>? GroupVoiceJoined;
        // Group voice reply: (groupId, username, senderIp, udpPort)
        public static event Action<string, string, string, int>?      GroupVoiceReplied;
        // Group voice leave: (groupId, username)
        public static event Action<string, string>?                   GroupVoiceLeft;

        // Voice signaling 1:1: (callerName, callerUdpPort, callerIp, callerTcpPort)
        public static event Action<string, string, string, int>? IncomingVoiceCall;
        // Voice signaling: (peerName, answererUdpPort, answererIp)
        public static event Action<string, string, string>? VoiceCallAnswered;
        // Voice signaling: (peerName)
        public static event Action<string>?         VoiceCallRejected;
        // Voice signaling: (peerName)
        public static event Action<string>?         VoiceCallHungUp;

        // Video signaling 1:1: (callerName, callerAudioPort, callerVideoPort, callerIp, callerTcpPort)
        public static event Action<string, string, string, string, int>? IncomingVideoCall;
        // Video signaling: (peerName, answererAudioPort, answererVideoPort, answererIp)
        public static event Action<string, string, string, string>? VideoCallAnswered;
        // Video signaling: (peerName)
        public static event Action<string>?                 VideoCallRejected;
        // Video signaling: (peerName)
        public static event Action<string>?                 VideoCallHungUp;

        // Group video channel: (groupId, username, senderIp, audioUdpPort, videoUdpPort, senderTcpPort)
        public static event Action<string, string, string, int, int, int>? GroupVideoJoined;
        // Group video reply:   (groupId, username, senderIp, audioUdpPort, videoUdpPort)
        public static event Action<string, string, string, int, int>?      GroupVideoReplied;
        // Group video leave:   (groupId, username)
        public static event Action<string, string>?                        GroupVideoLeft;

        // ── Inject một message đến từ relay server (thay thế kết nối P2P trực tiếp) ──
        // senderIp: public IP của người gửi (dùng cho audio/video UDP endpoint)
        public static void InjectRelayedLine(string senderIp, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _ = Task.Run(() => ProcessRelayedLine(senderIp, line));
        }

        private static void ProcessRelayedLine(string senderIp, string line)
        {
            if (line.StartsWith("CHAT|"))
            {
                string[] p = line.Split('|', 3);
                if (p.Length == 3) MessageReceived?.Invoke(p[1], p[2]);
            }
            else if (line.StartsWith("GROUP_MSG|"))
            {
                string[] p = line.Split('|', 5);
                if (p.Length == 5) GroupMessageReceived?.Invoke(p[1], p[2], p[3], p[4]);
            }
            else if (line.StartsWith("GROUP_RENAME|"))
            {
                string[] p = line.Split('|', 3);
                if (p.Length == 3) GroupRenamed?.Invoke(p[1], p[2]);
            }
            else if (line.StartsWith("GROUP_VOICE_JOIN|"))
            {
                // GROUP_VOICE_JOIN|groupId|username|udpPort|senderTcpPort
                string[] p = line.Split('|', 5);
                if (p.Length == 5
                    && int.TryParse(p[3], out int udpPort)
                    && int.TryParse(p[4], out int tcpPort))
                    GroupVoiceJoined?.Invoke(p[1], p[2], senderIp, udpPort, tcpPort);
            }
            else if (line.StartsWith("GROUP_VOICE_REPLY|"))
            {
                // GROUP_VOICE_REPLY|groupId|username|udpPort
                string[] p = line.Split('|', 4);
                if (p.Length == 4 && int.TryParse(p[3], out int udpPort))
                    GroupVoiceReplied?.Invoke(p[1], p[2], senderIp, udpPort);
            }
            else if (line.StartsWith("GROUP_VOICE_LEAVE|"))
            {
                string[] p = line.Split('|', 3);
                if (p.Length == 3) GroupVoiceLeft?.Invoke(p[1], p[2]);
            }
            else if (line.StartsWith("GROUP_VIDEO_JOIN|"))
            {
                // GROUP_VIDEO_JOIN|groupId|username|audioPort|videoPort|senderTcpPort
                string[] p = line.Split('|', 6);
                if (p.Length == 6
                    && int.TryParse(p[3], out int aPort)
                    && int.TryParse(p[4], out int vPort)
                    && int.TryParse(p[5], out int tcpPort))
                    GroupVideoJoined?.Invoke(p[1], p[2], senderIp, aPort, vPort, tcpPort);
            }
            else if (line.StartsWith("GROUP_VIDEO_REPLY|"))
            {
                // GROUP_VIDEO_REPLY|groupId|username|audioPort|videoPort
                string[] p = line.Split('|', 5);
                if (p.Length == 5
                    && int.TryParse(p[3], out int aPort)
                    && int.TryParse(p[4], out int vPort))
                    GroupVideoReplied?.Invoke(p[1], p[2], senderIp, aPort, vPort);
            }
            else if (line.StartsWith("GROUP_VIDEO_LEAVE|"))
            {
                string[] p = line.Split('|', 3);
                if (p.Length == 3) GroupVideoLeft?.Invoke(p[1], p[2]);
            }
            else if (line.StartsWith("TYPING|"))
            {
                string[] p = line.Split('|', 2);
                if (p.Length == 2) TypingReceived?.Invoke(p[1]);
            }
            else if (line.StartsWith("VOICE_OFFER|"))
            {
                // VOICE_OFFER|callerName|callerUdpPort|callerTcpPort
                string[] p = line.Split('|', 4);
                if (p.Length == 4 && int.TryParse(p[3], out int tcp))
                    IncomingVoiceCall?.Invoke(p[1], p[2], senderIp, tcp);
            }
            else if (line.StartsWith("VOICE_ANSWER|"))
            {
                string[] p = line.Split('|', 3);
                if (p.Length == 3) VoiceCallAnswered?.Invoke(p[1], p[2], senderIp);
            }
            else if (line.StartsWith("VOICE_REJECT|"))
            {
                string[] p = line.Split('|', 2);
                if (p.Length == 2) VoiceCallRejected?.Invoke(p[1]);
            }
            else if (line.StartsWith("VOICE_HANGUP|"))
            {
                string[] p = line.Split('|', 2);
                if (p.Length == 2) VoiceCallHungUp?.Invoke(p[1]);
            }
            else if (line.StartsWith("VIDEO_OFFER|"))
            {
                // VIDEO_OFFER|callerName|audioPort|videoPort|callerTcpPort
                string[] p = line.Split('|', 5);
                if (p.Length == 5 && int.TryParse(p[4], out int tcp))
                    IncomingVideoCall?.Invoke(p[1], p[2], p[3], senderIp, tcp);
            }
            else if (line.StartsWith("VIDEO_ANSWER|"))
            {
                string[] p = line.Split('|', 4);
                if (p.Length == 4) VideoCallAnswered?.Invoke(p[1], p[2], p[3], senderIp);
            }
            else if (line.StartsWith("VIDEO_REJECT|"))
            {
                string[] p = line.Split('|', 2);
                if (p.Length == 2) VideoCallRejected?.Invoke(p[1]);
            }
            else if (line.StartsWith("VIDEO_HANGUP|"))
            {
                string[] p = line.Split('|', 2);
                if (p.Length == 2) VideoCallHungUp?.Invoke(p[1]);
            }
            else if (line.StartsWith("BOT_RESPONSE|"))
            {
                // BOT_RESPONSE|sessionId|encryptedText
                string[] p = line.Split('|', 3);
                if (p.Length == 3) P2PChatService.CompleteBotRelayResponse(p[1], p[2]);
            }
            else if (line.StartsWith("FILE_RELAY|"))
            {
                // FILE_RELAY|senderName|fileName|sha256|base64Data
                string[] p = line.Split('|', 5);
                if (p.Length == 5)
                {
                    try
                    {
                        byte[] data      = Convert.FromBase64String(p[4]);
                        string saveDir   = FileTransferService.GetReceiveFolder();
                        string savePath  = Path.Combine(saveDir, p[2]);
                        File.WriteAllBytes(savePath, data);
                        string actualSha = FileTransferService.ComputeSha256(savePath);
                        if (actualSha.Equals(p[3], StringComparison.OrdinalIgnoreCase))
                            FileReceived?.Invoke(p[1], p[2], savePath);
                        else
                            try { File.Delete(savePath); } catch { }
                    }
                    catch { }
                }
            }
            else if (line.StartsWith("GROUP_FILE_RELAY|"))
            {
                // GROUP_FILE_RELAY|groupId|groupName|senderName|fileName|sha256|base64Data
                string[] p = line.Split('|', 7);
                if (p.Length == 7)
                {
                    try
                    {
                        byte[] data      = Convert.FromBase64String(p[6]);
                        string saveDir   = FileTransferService.GetReceiveFolder();
                        string savePath  = Path.Combine(saveDir, p[4]);
                        File.WriteAllBytes(savePath, data);
                        string actualSha = FileTransferService.ComputeSha256(savePath);
                        if (actualSha.Equals(p[5], StringComparison.OrdinalIgnoreCase))
                            GroupFileReceived?.Invoke(p[1], p[2], p[3], p[4], savePath);
                        else
                            try { File.Delete(savePath); } catch { }
                    }
                    catch { }
                }
            }
        }

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
                // Lấy IP nguồn — dùng cho group voice để biết peer ở đâu
                string remoteIp = ((System.Net.IPEndPoint?)client.Client.RemoteEndPoint)
                                  ?.Address.ToString() ?? "127.0.0.1";

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
                else if (firstLine.StartsWith("GROUP_RENAME|"))
                {
                    // GROUP_RENAME|groupId|newName
                    string[] parts = firstLine.Split('|', 3);
                    if (parts.Length == 3)
                        GroupRenamed?.Invoke(parts[1], parts[2]);
                }
                else if (firstLine.StartsWith("GROUP_FILE_INIT|"))
                {
                    await HandleGroupFileTransferAsync(firstLine, reader);
                }
                else if (firstLine.StartsWith("TYPING|"))
                {
                    string[] parts = firstLine.Split('|', 2);
                    if (parts.Length == 2)
                        TypingReceived?.Invoke(parts[1]);
                }
                else if (firstLine.StartsWith("AVATAR_PUSH|"))
                {
                    // AVATAR_PUSH|username|base64PNG
                    string[] parts = firstLine.Split('|', 3);
                    if (parts.Length == 3)
                    {
                        try
                        {
                            byte[] pngBytes = Convert.FromBase64String(parts[2]);
                            AvatarService.SaveUserAvatarFromBytes(parts[1], pngBytes);
                            AvatarReceived?.Invoke(parts[1]);
                        }
                        catch { }
                    }
                }
                else if (firstLine.StartsWith("GROUP_VOICE_JOIN|"))
                {
                    // GROUP_VOICE_JOIN|groupId|username|udpPort|senderTcpPort
                    string[] parts = firstLine.Split('|', 5);
                    if (parts.Length == 5
                        && int.TryParse(parts[3], out int udpPort)
                        && int.TryParse(parts[4], out int tcpPort))
                        GroupVoiceJoined?.Invoke(parts[1], parts[2], remoteIp, udpPort, tcpPort);
                }
                else if (firstLine.StartsWith("GROUP_VOICE_REPLY|"))
                {
                    // GROUP_VOICE_REPLY|groupId|username|udpPort
                    string[] parts = firstLine.Split('|', 4);
                    if (parts.Length == 4 && int.TryParse(parts[3], out int udpPort))
                        GroupVoiceReplied?.Invoke(parts[1], parts[2], remoteIp, udpPort);
                }
                else if (firstLine.StartsWith("GROUP_VOICE_LEAVE|"))
                {
                    // GROUP_VOICE_LEAVE|groupId|username
                    string[] parts = firstLine.Split('|', 3);
                    if (parts.Length == 3)
                        GroupVoiceLeft?.Invoke(parts[1], parts[2]);
                }
                else if (firstLine.StartsWith("VOICE_OFFER|"))
                {
                    // VOICE_OFFER|callerName|callerUdpPort|callerTcpPort
                    string[] parts = firstLine.Split('|', 4);
                    if (parts.Length == 4 && int.TryParse(parts[3], out int callerTcp))
                        IncomingVoiceCall?.Invoke(parts[1], parts[2], remoteIp, callerTcp);
                }
                else if (firstLine.StartsWith("VOICE_ANSWER|"))
                {
                    string[] parts = firstLine.Split('|', 3);
                    if (parts.Length == 3)
                        VoiceCallAnswered?.Invoke(parts[1], parts[2], remoteIp);
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
                else if (firstLine.StartsWith("VIDEO_OFFER|"))
                {
                    // VIDEO_OFFER|callerName|audioUdpPort|videoUdpPort|callerTcpPort
                    string[] parts = firstLine.Split('|', 5);
                    if (parts.Length == 5 && int.TryParse(parts[4], out int callerTcp))
                        IncomingVideoCall?.Invoke(parts[1], parts[2], parts[3], remoteIp, callerTcp);
                }
                else if (firstLine.StartsWith("VIDEO_ANSWER|"))
                {
                    // VIDEO_ANSWER|callerName|audioUdpPort|videoUdpPort
                    string[] parts = firstLine.Split('|', 4);
                    if (parts.Length == 4)
                        VideoCallAnswered?.Invoke(parts[1], parts[2], parts[3], remoteIp);
                }
                else if (firstLine.StartsWith("VIDEO_REJECT|"))
                {
                    string[] parts = firstLine.Split('|', 2);
                    if (parts.Length == 2)
                        VideoCallRejected?.Invoke(parts[1]);
                }
                else if (firstLine.StartsWith("VIDEO_HANGUP|"))
                {
                    string[] parts = firstLine.Split('|', 2);
                    if (parts.Length == 2)
                        VideoCallHungUp?.Invoke(parts[1]);
                }
                else if (firstLine.StartsWith("GROUP_VIDEO_JOIN|"))
                {
                    // GROUP_VIDEO_JOIN|groupId|username|audioPort|videoPort|senderTcpPort
                    string[] parts = firstLine.Split('|', 6);
                    if (parts.Length == 6
                        && int.TryParse(parts[3], out int aPort)
                        && int.TryParse(parts[4], out int vPort)
                        && int.TryParse(parts[5], out int tcpPort))
                        GroupVideoJoined?.Invoke(parts[1], parts[2], remoteIp, aPort, vPort, tcpPort);
                }
                else if (firstLine.StartsWith("GROUP_VIDEO_REPLY|"))
                {
                    // GROUP_VIDEO_REPLY|groupId|username|audioPort|videoPort
                    string[] parts = firstLine.Split('|', 5);
                    if (parts.Length == 5
                        && int.TryParse(parts[3], out int aPort)
                        && int.TryParse(parts[4], out int vPort))
                        GroupVideoReplied?.Invoke(parts[1], parts[2], remoteIp, aPort, vPort);
                }
                else if (firstLine.StartsWith("GROUP_VIDEO_LEAVE|"))
                {
                    // GROUP_VIDEO_LEAVE|groupId|username
                    string[] parts = firstLine.Split('|', 3);
                    if (parts.Length == 3)
                        GroupVideoLeft?.Invoke(parts[1], parts[2]);
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
            if (totalChunks <= 0) return;

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
            else
            {
                try { File.Delete(savePath); } catch { }
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
            if (totalChunks <= 0) return;

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
            else
            {
                try { File.Delete(savePath); } catch { }
            }
        }
    }
}
