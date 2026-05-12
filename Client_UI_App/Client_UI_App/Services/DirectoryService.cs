using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Xử lý toàn bộ giao tiếp với Load Balancer và Directory Server
    // Wire protocol: StreamWriter.WriteLine / StreamReader.ReadLine (text, pipe-delimited)
    internal static class DirectoryService
    {
        // ⚠️ Cấu hình đọc từ appsettings.json — đổi IP ở đó, không cần rebuild
        private static string LbIp        => AppConfig.LbIp;
        private static int    LbPort      => AppConfig.LbPort;
        private static int[]  AllDirPorts => AppConfig.DirPorts;

        // ── Bước 1: Xin vé từ Load Balancer, nhận Port của Directory Server ──
        public static async Task<int> GetDirectoryPortAsync()
        {
            using TcpClient lb = new();
            await lb.ConnectAsync(LbIp, LbPort);

            NetworkStream stream = lb.GetStream();
            byte[] buf = new byte[32];
            int n = await stream.ReadAsync(buf.AsMemory(0, buf.Length));
            string portStr = Encoding.UTF8.GetString(buf, 0, n).Trim();
            return int.Parse(portStr);
        }

        // ── Đăng ký tài khoản mới ──
        // Gửi: SIGNUP|username|password
        public static async Task<(bool success, string message)> SignupAsync(
            int dirPort, string username, string password)
        {
            using TcpClient client = new();
            await client.ConnectAsync(LbIp, dirPort);

            await using NetworkStream stream = client.GetStream();
            using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"SIGNUP|{username}|{password}");

            string response = await reader.ReadLineAsync() ?? string.Empty;
            string[] parts  = response.Split('|');
            bool   ok  = parts[0] == "SIGNUP_SUCCESS";
            string msg = parts.Length > 1 ? parts[1] : response;
            return (ok, msg);
        }

        // Lấy IP LAN thực của máy này (không phải loopback)
        // Dùng "UDP trick": giả kết nối tới 8.8.8.8 để OS chọn interface ra ngoài
        private static string GetLocalLanIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Address.ToString();
            }
            catch { return "127.0.0.1"; }
        }

        // ── Đăng nhập và lấy danh sách user online ──
        // Gửi: LOGIN|username|password|myLanIp:myListeningPort
        // Server đã hỗ trợ sẵn format "IP:Port" qua fullAddressOverride
        public static async Task<(bool success, string message, List<string> onlineUsers)> LoginAsync(
            int dirPort, string username, string password, int myListeningPort)
        {
            using TcpClient client = new();
            await client.ConnectAsync(LbIp, dirPort);

            await using NetworkStream stream = client.GetStream();
            using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            string localIp = GetLocalLanIp();
            await writer.WriteLineAsync($"LOGIN|{username}|{password}|{localIp}:{myListeningPort}");

            string response = await reader.ReadLineAsync() ?? string.Empty;
            string[] parts  = response.Split('|');

            if (parts[0] == "SUCCESS")
            {
                var users = (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                    ? new List<string>(parts[1].Split(','))
                    : new List<string>();
                return (true, "Đăng nhập thành công!", users);
            }

            string errMsg = parts.Length > 1 ? parts[1] : "Đăng nhập thất bại.";
            return (false, errMsg, new List<string>());
        }

        // ── Lấy IP:Port của một user đang online ──
        // Gửi: GETUSER|username
        public static async Task<(bool found, string ipPort)> GetUserAsync(int dirPort, string username)
        {
            using TcpClient client = new();
            await client.ConnectAsync(LbIp, dirPort);

            await using NetworkStream stream = client.GetStream();
            using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"GETUSER|{username}");

            string response = await reader.ReadLineAsync() ?? string.Empty;
            string[] parts  = response.Split('|');

            if (parts[0] == "GETUSER_SUCCESS" && parts.Length > 1)
                return (true, parts[1]);
            return (false, string.Empty);
        }

        // ── Làm mới danh sách user online (query song song cả 2 server, merge kết quả) ──
        public static async Task<List<string>> GetOnlineUsersAsync(int _)
        {
            var tasks   = AllDirPorts.Select(QueryListFromPortAsync);
            var results = await Task.WhenAll(tasks);

            return results
                .SelectMany(u => u)
                .Distinct()
                .OrderBy(u => u)
                .ToList();
        }

        // Helper: LIST_USERS trực tiếp tới 1 port cụ thể (bỏ qua LB)
        private static async Task<List<string>> QueryListFromPortAsync(int port)
        {
            try
            {
                using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, port, cts.Token);

                await using NetworkStream stream = client.GetStream();
                using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync("LIST_USERS");

                string response = await reader.ReadLineAsync() ?? string.Empty;
                string[] parts  = response.Split('|');

                if (parts[0] == "LIST_SUCCESS" && parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                    return parts[1].Split(',').ToList();
            }
            catch { /* Server chưa chạy hoặc không phản hồi — bỏ qua */ }

            return new List<string>();
        }

        // ── Đăng xuất ──
        public static async Task LogoutAsync(int dirPort, string username)
        {
            try
            {
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, dirPort);

                await using NetworkStream stream = client.GetStream();
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync($"LOGOUT|{username}");
                await Task.Delay(300);
            }
            catch { /* Bỏ qua lỗi khi đóng app */ }
        }

        // ══════════════════════════════════════════════════════════════
        //  NHÓM (GROUP) — broadcast cả 2 server để đồng bộ dữ liệu
        // ══════════════════════════════════════════════════════════════

        // Tạo Group ID ngẫu nhiên 6 ký tự (client sinh để đồng bộ 2 server)
        public static string GenerateGroupId()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // bỏ ký tự dễ nhầm
            byte[] bytes = RandomNumberGenerator.GetBytes(6);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }

        // ── Tạo nhóm mới (broadcast cả 2 server) ──
        // Gửi: CREATE_GROUP|groupId|groupName|creator
        public static async Task<string> CreateGroupAsync(string groupId, string groupName, string creator)
        {
            var tasks = AllDirPorts.Select(port =>
                SendGroupCommandAsync(port, $"CREATE_GROUP|{groupId}|{groupName}|{creator}"));
            await Task.WhenAll(tasks);
            return groupId;
        }

        // ── Tham gia nhóm (thử từng server, broadcast cho server còn lại) ──
        // Gửi: JOIN_GROUP|groupId|username
        // Nhận: GROUP_JOIN_SUCCESS|groupName|creator|members
        public static async Task<(bool success, string groupName, string creator, List<string> members)> JoinGroupAsync(
            string groupId, string username)
        {
            foreach (int port in AllDirPorts)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    using TcpClient client = new();
                    await client.ConnectAsync(LbIp, port, cts.Token);

                    await using NetworkStream stream = client.GetStream();
                    using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                    await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                    await writer.WriteLineAsync($"JOIN_GROUP|{groupId}|{username}");
                    string response = await reader.ReadLineAsync() ?? "";
                    string[] parts  = response.Split('|', 4);

                    if (parts[0] == "GROUP_JOIN_SUCCESS")
                    {
                        string name    = parts.Length > 1 ? parts[1] : groupId;
                        string creator = parts.Length > 2 ? parts[2] : "";
                        var    members = parts.Length > 3 && !string.IsNullOrEmpty(parts[3])
                            ? parts[3].Split(',').ToList()
                            : new List<string>();

                        // Broadcast JOIN tới server còn lại (fire-and-forget)
                        int otherPort = AllDirPorts.First(p => p != port);
                        _ = SendGroupCommandAsync(otherPort, $"JOIN_GROUP|{groupId}|{username}");

                        return (true, name, creator, members);
                    }
                }
                catch { /* Thử server tiếp theo */ }
            }
            return (false, "", "", new List<string>());
        }

        // ── Rời nhóm (broadcast cả 2 server) ──
        // Gửi: LEAVE_GROUP|groupId|username
        public static async Task LeaveGroupAsync(string groupId, string username)
        {
            var tasks = AllDirPorts.Select(port =>
                SendGroupCommandAsync(port, $"LEAVE_GROUP|{groupId}|{username}"));
            await Task.WhenAll(tasks);
        }

        // ── Lấy danh sách thành viên (query cả 2 server, merge) ──
        // Gửi: GET_GROUP_MEMBERS|groupId
        public static async Task<(bool found, string groupName, List<string> members)> GetGroupMembersAsync(string groupId)
        {
            var tasks   = AllDirPorts.Select(port => QueryGroupMembersFromPortAsync(port, groupId));
            var results = await Task.WhenAll(tasks);

            var foundResult = results.FirstOrDefault(r => r.found);
            if (!foundResult.found) return (false, "", new List<string>());

            var allMembers = results
                .Where(r => r.found)
                .SelectMany(r => r.members)
                .Distinct()
                .OrderBy(m => m)
                .ToList();
            return (true, foundResult.groupName, allMembers);
        }

        // ── Lấy danh sách nhóm của user (query cả 2 server, merge) ──
        // Gửi: LIST_MY_GROUPS|username
        public static async Task<List<(string Id, string Name, string Creator)>> GetMyGroupsAsync(string username)
        {
            var tasks   = AllDirPorts.Select(port => QueryMyGroupsFromPortAsync(port, username));
            var results = await Task.WhenAll(tasks);

            return results
                .SelectMany(g => g)
                .GroupBy(g => g.Id)
                .Select(g => g.First())
                .OrderBy(g => g.Name)
                .ToList();
        }

        // ── Đổi tên nhóm (chỉ creator mới được) ──
        // Gửi: RENAME_GROUP|groupId|newName|requester
        public static async Task<(bool success, string newNameOrError)> RenameGroupAsync(
            string groupId, string newName, string requester)
        {
            foreach (int port in AllDirPorts)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    using TcpClient client = new();
                    await client.ConnectAsync(LbIp, port, cts.Token);

                    await using NetworkStream stream = client.GetStream();
                    using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                    await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                    await writer.WriteLineAsync($"RENAME_GROUP|{groupId}|{newName}|{requester}");
                    string response = await reader.ReadLineAsync() ?? "";
                    string[] parts  = response.Split('|', 2);

                    if (parts[0] == "RENAME_SUCCESS")
                    {
                        // Broadcast sang server còn lại (fire-and-forget)
                        int otherPort = AllDirPorts.First(p => p != port);
                        _ = SendGroupCommandAsync(otherPort, $"RENAME_GROUP|{groupId}|{newName}|{requester}");
                        return (true, parts.Length > 1 ? parts[1] : newName);
                    }
                    if (parts[0] == "RENAME_NOT_AUTHORIZED")
                        return (false, "Chỉ người tạo nhóm mới được đổi tên.");
                }
                catch { /* thử server tiếp theo */ }
            }
            return (false, "Không kết nối được server.");
        }

        // Helper: gửi lệnh group tới 1 port, không cần đọc phản hồi
        private static async Task SendGroupCommandAsync(int port, string command)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, port, cts.Token);

                await using NetworkStream stream = client.GetStream();
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(command);
                await Task.Delay(100);
            }
            catch { /* Server có thể đang offline */ }
        }

        // Helper: GET_GROUP_MEMBERS từ 1 port
        private static async Task<(bool found, string groupName, List<string> members)>
            QueryGroupMembersFromPortAsync(int port, string groupId)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, port, cts.Token);

                await using NetworkStream stream = client.GetStream();
                using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync($"GET_GROUP_MEMBERS|{groupId}");
                string response = await reader.ReadLineAsync() ?? "";
                string[] parts  = response.Split('|', 3);

                if (parts[0] == "GROUP_MEMBERS")
                {
                    string name    = parts.Length > 1 ? parts[1] : groupId;
                    var    members = parts.Length > 2 && !string.IsNullOrEmpty(parts[2])
                        ? parts[2].Split(',').ToList()
                        : new List<string>();
                    return (true, name, members);
                }
            }
            catch { }
            return (false, "", new List<string>());
        }

        // Helper: LIST_MY_GROUPS từ 1 port — format: groupId:groupName:creator
        private static async Task<List<(string Id, string Name, string Creator)>> QueryMyGroupsFromPortAsync(int port, string username)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, port, cts.Token);

                await using NetworkStream stream = client.GetStream();
                using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync($"LIST_MY_GROUPS|{username}");
                string response = await reader.ReadLineAsync() ?? "";
                string[] parts  = response.Split('|', 2);

                if (parts[0] == "MY_GROUPS" && parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                {
                    return parts[1].Split(',')
                        .Select(g => g.Split(':', 3))
                        .Where(g => g.Length >= 2)
                        .Select(g => (Id: g[0], Name: g[1], Creator: g.Length > 2 ? g[2] : ""))
                        .ToList();
                }
            }
            catch { }
            return new List<(string, string, string)>();
        }
    }
}
