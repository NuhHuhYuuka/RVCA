using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SecurityData.Services;

namespace Client_UI_App.Services
{
    // Gửi tin nhắn / file tới tất cả thành viên trong một nhóm (P2P blast)
    // Mỗi thành viên nhận một kết nối TCP độc lập — fire-and-forget per peer
    internal static class GroupChatService
    {
        // ── Gửi tin nhắn văn bản tới tất cả thành viên nhóm ─────────
        // memberEndpoints: danh sách (ip, port) của các thành viên đã resolve trước
        public static async Task SendGroupMessageAsync(
            string                           groupId,
            string                           groupName,
            string                           senderName,
            string                           message,
            IEnumerable<(string ip, int port)> memberEndpoints)
        {
            // GROUP_MSG|groupId|groupName|sender|message
            string line = $"GROUP_MSG|{groupId}|{groupName}|{senderName}|{message}";
            var tasks = new List<Task>();
            foreach (var (ip, port) in memberEndpoints)
                tasks.Add(SendLineAsync(ip, port, line));
            await Task.WhenAll(tasks);
        }

        // ── Gửi file tới tất cả thành viên nhóm (chunked 64KB) ───────
        public static async Task SendGroupFileAsync(
            string                           groupId,
            string                           groupName,
            string                           senderName,
            string                           filePath,
            IEnumerable<(string ip, int port)> memberEndpoints,
            IProgress<int>?                  progress = null)
        {
            var fileInfo   = new FileInfo(filePath);
            const int chunkSize = 64 * 1024;
            int  totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);
            string sha256   = FileTransferService.ComputeSha256(filePath);
            string fileName = fileInfo.Name;

            string header = $"GROUP_FILE_INIT|{groupId}|{groupName}|{senderName}|{fileName}|{totalChunks}|{sha256}";

            // Kết nối song song tới tất cả thành viên, gửi cùng chunks
            var clients = new List<(TcpClient tcp, StreamWriter writer)>();
            foreach (var (ip, port) in memberEndpoints)
            {
                try
                {
                    var tcp = new TcpClient { SendTimeout = 60_000 };
                    await tcp.ConnectAsync(ip, port);
                    var sw = new StreamWriter(tcp.GetStream(), Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                    await sw.WriteLineAsync(header);
                    clients.Add((tcp, sw));
                }
                catch { /* Thành viên offline — bỏ qua */ }
            }

            if (clients.Count == 0) return;

            int idx = 0;
            foreach (var chunk in FileTransferService.SplitFile(filePath, chunkSize))
            {
                string line = $"FILE_CHUNK|{idx}|{Convert.ToBase64String(chunk.Data)}";
                foreach (var (_, writer) in clients)
                {
                    try { await writer.WriteLineAsync(line); }
                    catch { /* Thành viên đã disconnect giữa chừng */ }
                }
                progress?.Report((idx + 1) * 100 / totalChunks);
                idx++;
            }

            // Đóng kết nối
            foreach (var (tcp, writer) in clients)
            {
                try { await writer.DisposeAsync(); tcp.Close(); }
                catch { }
            }
        }

        // ── Broadcast tham gia voice channel ─────────────────────────
        // Gửi GROUP_VOICE_JOIN tới tất cả thành viên để họ biết UDP port của mình
        public static Task BroadcastVoiceJoinAsync(
            string groupId, string username, int udpPort, int myTcpPort,
            IEnumerable<(string ip, int port)> memberEndpoints)
        {
            string line = $"GROUP_VOICE_JOIN|{groupId}|{username}|{udpPort}|{myTcpPort}";
            return Task.WhenAll(memberEndpoints.Select(ep => SendLineAsync(ep.ip, ep.port, line)));
        }

        // Gửi GROUP_VOICE_REPLY trực tiếp về người vừa join (1 peer, không broadcast)
        public static Task SendVoiceReplyAsync(
            string peerIp, int peerTcpPort, string groupId, string username, int myUdpPort)
        {
            return SendLineAsync(peerIp, peerTcpPort, $"GROUP_VOICE_REPLY|{groupId}|{username}|{myUdpPort}");
        }

        // Broadcast rời voice channel
        public static Task BroadcastVoiceLeaveAsync(
            string groupId, string username,
            IEnumerable<(string ip, int port)> memberEndpoints)
        {
            string line = $"GROUP_VOICE_LEAVE|{groupId}|{username}";
            return Task.WhenAll(memberEndpoints.Select(ep => SendLineAsync(ep.ip, ep.port, line)));
        }

        // ── Group Video channel ───────────────────────────────────────────
        // Broadcast tham gia video channel (audio+video ports)
        public static Task BroadcastVideoJoinAsync(
            string groupId, string username,
            int audioPort, int videoPort, int myTcpPort,
            IEnumerable<(string ip, int port)> memberEndpoints)
        {
            string line = $"GROUP_VIDEO_JOIN|{groupId}|{username}|{audioPort}|{videoPort}|{myTcpPort}";
            return Task.WhenAll(memberEndpoints.Select(ep => SendLineAsync(ep.ip, ep.port, line)));
        }

        // Reply trực tiếp về người vừa join
        public static Task SendVideoReplyAsync(
            string peerIp, int peerTcpPort, string groupId, string username,
            int audioPort, int videoPort)
        {
            return SendLineAsync(peerIp, peerTcpPort,
                $"GROUP_VIDEO_REPLY|{groupId}|{username}|{audioPort}|{videoPort}");
        }

        // Broadcast rời video channel
        public static Task BroadcastVideoLeaveAsync(
            string groupId, string username,
            IEnumerable<(string ip, int port)> memberEndpoints)
        {
            string line = $"GROUP_VIDEO_LEAVE|{groupId}|{username}";
            return Task.WhenAll(memberEndpoints.Select(ep => SendLineAsync(ep.ip, ep.port, line)));
        }

        // Broadcast đổi tên nhóm tới tất cả thành viên online
        public static Task BroadcastRenameAsync(
            string groupId, string newName, IEnumerable<(string ip, int port)> memberEndpoints)
        {
            var tasks = memberEndpoints.Select(ep =>
                SendLineAsync(ep.ip, ep.port, $"GROUP_RENAME|{groupId}|{newName}"));
            return Task.WhenAll(tasks);
        }

        // Helper: gửi 1 dòng text tới peer, đóng kết nối
        private static async Task SendLineAsync(string ip, int port, string line)
        {
            try
            {
                using TcpClient client = new();
                client.SendTimeout = 5_000;
                await client.ConnectAsync(ip, port);

                await using NetworkStream stream = client.GetStream();
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(line);
                await Task.Delay(100);
            }
            catch { /* Thành viên offline hoặc không thể kết nối */ }
        }
    }
}
