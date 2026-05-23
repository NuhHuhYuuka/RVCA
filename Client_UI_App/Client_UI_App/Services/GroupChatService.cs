using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SecurityData.Services;

namespace Client_UI_App.Services
{
    // Gửi tin nhắn / file tới tất cả thành viên trong một nhóm (P2P blast)
    // Mỗi thành viên nhận một kết nối TCP độc lập — fire-and-forget per peer
    internal static class GroupChatService
    {
        // ── Gửi tin nhắn văn bản tới tất cả thành viên nhóm ─────────
        // memberEndpoints: (memberName, ip, port) đã resolve trước
        public static async Task SendGroupMessageAsync(
            string                                      groupId,
            string                                      groupName,
            string                                      senderName,
            string                                      message,
            IEnumerable<(string name, string ip, int port)> memberEndpoints)
        {
            string line = $"GROUP_MSG|{groupId}|{groupName}|{senderName}|{message}";
            var tasks = new List<Task>();
            foreach (var (name, ip, port) in memberEndpoints)
                tasks.Add(SendLineAsync(ip, port, line, senderName, name));
            await Task.WhenAll(tasks);
        }

        // ── Gửi file tới tất cả thành viên nhóm (P2P chunked hoặc relay base64) ──
        public static async Task SendGroupFileAsync(
            string                                      groupId,
            string                                      groupName,
            string                                      senderName,
            string                                      filePath,
            IEnumerable<(string name, string ip, int port)> memberEndpoints,
            IProgress<int>?                             progress = null)
        {
            var fileInfo   = new FileInfo(filePath);
            const int chunkSize = 64 * 1024;
            int  totalChunks = (int)Math.Ceiling((double)fileInfo.Length / chunkSize);
            string sha256   = FileTransferService.ComputeSha256(filePath);
            string fileName = fileInfo.Name;
            string header   = $"GROUP_FILE_INIT|{groupId}|{groupName}|{senderName}|{fileName}|{totalChunks}|{sha256}";

            var endpointList = memberEndpoints.ToList();

            // Thử kết nối P2P song song với 500ms timeout
            var p2pClients = new List<(string name, TcpClient tcp, StreamWriter writer)>();
            var relayTargets = new List<string>();

            foreach (var (name, ip, port) in endpointList)
            {
                bool connected = false;
                try
                {
                    using var cts = new CancellationTokenSource(500);
                    var tcp = new TcpClient { SendTimeout = 60_000 };
                    await tcp.ConnectAsync(ip, port, cts.Token);
                    var sw = new StreamWriter(tcp.GetStream(), Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                    await sw.WriteLineAsync(header);
                    p2pClients.Add((name, tcp, sw));
                    connected = true;
                }
                catch { }

                if (!connected) relayTargets.Add(name);
            }

            // Gửi chunks tới các client P2P đã kết nối
            int idx = 0;
            if (p2pClients.Count > 0)
            {
                foreach (var chunk in FileTransferService.SplitFile(filePath, chunkSize))
                {
                    string line = $"FILE_CHUNK|{idx}|{Convert.ToBase64String(chunk.Data)}";
                    foreach (var (_, _, writer) in p2pClients)
                    {
                        try { await writer.WriteLineAsync(line); } catch { }
                    }
                    if (relayTargets.Count == 0)
                        progress?.Report((idx + 1) * 100 / totalChunks);
                    idx++;
                }
                foreach (var (_, tcp, writer) in p2pClients)
                {
                    try { await writer.DisposeAsync(); tcp.Close(); } catch { }
                }
            }

            // Relay fallback: gửi file nguyên vẹn base64 cho các thành viên không P2P được
            if (relayTargets.Count > 0)
            {
                byte[] data = File.ReadAllBytes(filePath);
                string base64All = Convert.ToBase64String(data);
                string relayLine = $"GROUP_FILE_RELAY|{groupId}|{groupName}|{senderName}|{fileName}|{sha256}|{base64All}";
                var relayTasks = relayTargets.Select(t =>
                    DirectoryService.RelayAsync(senderName, t, relayLine)).ToList();
                await Task.WhenAll(relayTasks);
                progress?.Report(100);
            }
        }

        // ── Broadcast tham gia voice channel ─────────────────────────
        public static Task BroadcastVoiceJoinAsync(
            string groupId, string username, int udpPort, int myTcpPort,
            IEnumerable<(string name, string ip, int port)> memberEndpoints)
        {
            string myIp = DirectoryService.GetIpFacingServer();
            string line = $"GROUP_VOICE_JOIN|{groupId}|{username}|{udpPort}|{myTcpPort}|{myIp}";
            return Task.WhenAll(memberEndpoints.Select(ep =>
                SendLineAsync(ep.ip, ep.port, line, username, ep.name)));
        }

        // Gửi GROUP_VOICE_REPLY trực tiếp về người vừa join (1 peer, không broadcast)
        public static Task SendVoiceReplyAsync(
            string peerIp, int peerTcpPort, string groupId, string username, int myUdpPort,
            string senderName = "", string targetName = "")
        {
            string myIp = DirectoryService.GetIpFacingServer();
            return SendLineAsync(peerIp, peerTcpPort,
                $"GROUP_VOICE_REPLY|{groupId}|{username}|{myUdpPort}|{myIp}", senderName, targetName);
        }

        // Broadcast rời voice channel
        public static Task BroadcastVoiceLeaveAsync(
            string groupId, string username,
            IEnumerable<(string name, string ip, int port)> memberEndpoints)
        {
            string line = $"GROUP_VOICE_LEAVE|{groupId}|{username}";
            return Task.WhenAll(memberEndpoints.Select(ep =>
                SendLineAsync(ep.ip, ep.port, line, username, ep.name)));
        }

        // ── Group Video channel ───────────────────────────────────────────
        public static Task BroadcastVideoJoinAsync(
            string groupId, string username,
            int audioPort, int videoPort, int myTcpPort,
            IEnumerable<(string name, string ip, int port)> memberEndpoints)
        {
            string myIp = DirectoryService.GetIpFacingServer();
            string line = $"GROUP_VIDEO_JOIN|{groupId}|{username}|{audioPort}|{videoPort}|{myTcpPort}|{myIp}";
            return Task.WhenAll(memberEndpoints.Select(ep =>
                SendLineAsync(ep.ip, ep.port, line, username, ep.name)));
        }

        // Reply trực tiếp về người vừa join
        public static Task SendVideoReplyAsync(
            string peerIp, int peerTcpPort, string groupId, string username,
            int audioPort, int videoPort,
            string senderName = "", string targetName = "")
        {
            string myIp = DirectoryService.GetIpFacingServer();
            return SendLineAsync(peerIp, peerTcpPort,
                $"GROUP_VIDEO_REPLY|{groupId}|{username}|{audioPort}|{videoPort}|{myIp}", senderName, targetName);
        }

        // Broadcast rời video channel
        public static Task BroadcastVideoLeaveAsync(
            string groupId, string username,
            IEnumerable<(string name, string ip, int port)> memberEndpoints)
        {
            string line = $"GROUP_VIDEO_LEAVE|{groupId}|{username}";
            return Task.WhenAll(memberEndpoints.Select(ep =>
                SendLineAsync(ep.ip, ep.port, line, username, ep.name)));
        }

        // Broadcast đổi tên nhóm tới tất cả thành viên online
        public static Task BroadcastRenameAsync(
            string groupId, string newName, string senderName,
            IEnumerable<(string name, string ip, int port)> memberEndpoints)
        {
            return Task.WhenAll(memberEndpoints.Select(ep =>
                SendLineAsync(ep.ip, ep.port, $"GROUP_RENAME|{groupId}|{newName}", senderName, ep.name)));
        }

        // Helper: gửi 1 dòng text tới peer — thử P2P 500ms, fallback relay qua server
        private static async Task SendLineAsync(string ip, int port, string line,
            string senderName = "", string targetName = "")
        {
            bool ok = false;
            try
            {
                using var cts = new CancellationTokenSource(500);
                using TcpClient client = new();
                client.SendTimeout = 5_000;
                await client.ConnectAsync(ip, port, cts.Token);

                await using NetworkStream stream = client.GetStream();
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(line);
                await Task.Delay(100);
                ok = true;
            }
            catch { }

            if (!ok && !string.IsNullOrEmpty(senderName) && !string.IsNullOrEmpty(targetName))
                await DirectoryService.RelayAsync(senderName, targetName, line);
        }
    }
}
