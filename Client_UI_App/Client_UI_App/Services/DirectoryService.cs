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
        // Gửi: SIGNUP|username|password|email
        public static async Task<(bool success, string message)> SignupAsync(
            int dirPort, string username, string password, string email = "")
        {
            using TcpClient client = new();
            await client.ConnectAsync(LbIp, dirPort);

            await using NetworkStream stream = client.GetStream();
            using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"SIGNUP|{username}|{password}|{email}");

            string response = await reader.ReadLineAsync() ?? string.Empty;
            string[] parts  = response.Split('|');
            bool   ok  = parts[0] == "SIGNUP_SUCCESS";
            string msg = parts.Length > 1 ? parts[1] : response;
            return (ok, msg);
        }

        // ── Gửi OTP về Gmail đã đăng ký ──
        // Gửi: FORGOT_PASSWORD|email
        public static async Task<(bool success, string message)> ForgotPasswordAsync(string email)
        {
            int dirPort = await GetDirectoryPortAsync();
            using TcpClient client = new();
            await client.ConnectAsync(LbIp, dirPort);

            await using NetworkStream stream = client.GetStream();
            using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"FORGOT_PASSWORD|{email}");

            string response = await reader.ReadLineAsync() ?? string.Empty;
            string[] parts  = response.Split('|');
            bool   ok  = parts[0] == "FORGOT_SUCCESS";
            string msg = parts.Length > 1 ? parts[1] : (ok ? "OTP đã được gửi." : "Thất bại.");
            return (ok, msg);
        }

        // ── Đặt lại mật khẩu bằng OTP ──
        // Gửi: RESET_PASSWORD|email|otp|newpassword
        public static async Task<(bool success, string message)> ResetPasswordAsync(
            string email, string otp, string newPassword)
        {
            int dirPort = await GetDirectoryPortAsync();
            using TcpClient client = new();
            await client.ConnectAsync(LbIp, dirPort);

            await using NetworkStream stream = client.GetStream();
            using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"RESET_PASSWORD|{email}|{otp}|{newPassword}");

            string response = await reader.ReadLineAsync() ?? string.Empty;
            string[] parts  = response.Split('|');
            bool   ok  = parts[0] == "RESET_SUCCESS";
            string msg = parts.Length > 1 ? parts[1] : (ok ? "Mật khẩu đã được đặt lại." : "Thất bại.");
            return (ok, msg);
        }

        // Lấy IP LAN thực của máy này (không phải loopback)
        // Ưu tiên UDP trick; fallback sang NetworkInterface nếu cần
        public static string GetLocalLanIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                string ip = ((System.Net.IPEndPoint)socket.LocalEndPoint!).Address.ToString();
                if (!ip.StartsWith("127.") && ip != "::1") return ip;
            }
            catch { }

            // Fallback: first non-loopback IPv4 on an active interface
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            return addr.Address.ToString();
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }

        // Lấy IP mà máy này dùng để kết nối server (Tailscale IP nếu dùng VPN)
        // UDP trick tới LbIp — nếu trả về loopback (máy chính là server), scan adapter Tailscale
        public static string GetIpFacingServer()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect(LbIp, 65530);
                string ip = ((System.Net.IPEndPoint)socket.LocalEndPoint!).Address.ToString();
                if (!ip.StartsWith("127.") && ip != "::1") return ip;
                // loopback → đây là máy chủ, cần scan Tailscale adapter trực tiếp
            }
            catch { }

            // Scan adapter Tailscale: ưu tiên theo tên, fallback CGNAT 100.64.0.0/10
            try
            {
                string? cgnatIp = null;
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    bool named = ni.Name.IndexOf("Tailscale", StringComparison.OrdinalIgnoreCase) >= 0
                              || ni.Description.IndexOf("Tailscale", StringComparison.OrdinalIgnoreCase) >= 0;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string addr = ua.Address.ToString();
                        if (addr.StartsWith("127.")) continue;
                        if (named) return addr;
                        if (cgnatIp == null && addr.StartsWith("100."))
                        {
                            string[] p = addr.Split('.');
                            if (p.Length == 4 && int.TryParse(p[1], out int b) && b >= 64 && b <= 127)
                                cgnatIp = addr;
                        }
                    }
                }
                if (cgnatIp != null) return cgnatIp;
            }
            catch { }

            return GetLocalLanIp();
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

        // ── Lấy IP:Port của một user — query song song cả 2 server ──
        // User có thể login vào bất kỳ server nào; query cả 2 đảm bảo tìm thấy.
        public static async Task<(bool found, string ipPort)> GetUserAsync(string username)
        {
            var tasks   = AllDirPorts.Select(port => GetUserFromPortAsync(port, username));
            var results = await Task.WhenAll(tasks);
            var match   = results.FirstOrDefault(r => r.found);
            return match.found ? match : (false, string.Empty);
        }

        private static async Task<(bool found, string ipPort)> GetUserFromPortAsync(int dirPort, string username)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, dirPort, cts.Token);

                await using NetworkStream stream = client.GetStream();
                using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync($"GETUSER|{username}");
                string response = await reader.ReadLineAsync() ?? string.Empty;
                string[] parts  = response.Split('|');

                if (parts[0] == "GETUSER_SUCCESS" && parts.Length > 1)
                    return (true, parts[1]);
            }
            catch { }
            return (false, string.Empty);
        }

        // Overload cũ (giữ để tương thích với code cũ nếu có)
        public static async Task<(bool found, string ipPort)> GetUserAsync(int dirPort, string username)
            => await GetUserFromPortAsync(dirPort, username);

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

        // ── Relay 1 dòng P2P qua server (dùng khi P2P trực tiếp bị NAT chặn) ──
        public static async Task<bool> RelayAsync(string fromUser, string toUser, string line)
        {
            try
            {
                int dirPort = await GetDirectoryPortAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, dirPort, cts.Token);

                await using NetworkStream stream = client.GetStream();
                using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync($"RELAY|{fromUser}|{toUser}|{line}");
                string? resp = await reader.ReadLineAsync();
                return resp == "RELAY_OK";
            }
            catch { return false; }
        }

        // ── Poll tất cả message relay chưa đọc của mình ──────────────
        // Poll song song cả AllDirPorts — relay message chỉ nằm trên 1 server
        // nên không bị duplicate; đảm bảo nhận được dù LB route tới server nào.
        public static async Task<List<(string from, string senderIp, string line)>> PollAsync(string username)
        {
            var tasks = AllDirPorts.Select(port => PollFromPortAsync(username, port));
            var results = await Task.WhenAll(tasks);
            var merged = new List<(string, string, string)>();
            foreach (var r in results) merged.AddRange(r);
            return merged;
        }

        private static async Task<List<(string from, string senderIp, string line)>> PollFromPortAsync(
            string username, int dirPort)
        {
            var result = new List<(string, string, string)>();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, dirPort, cts.Token);

                await using NetworkStream stream = client.GetStream();
                using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync($"POLL|{username}");
                string? header = await reader.ReadLineAsync();
                if (header == null || !header.StartsWith("POLL_RESULT|")) return result;
                if (!int.TryParse(header.Split('|')[1], out int count) || count <= 0) return result;

                for (int i = 0; i < count; i++)
                {
                    string? msgLine = await reader.ReadLineAsync();
                    if (msgLine == null) break;
                    // MSG|fromUser|senderIp|<content — may contain |>
                    string[] p = msgLine.Split('|', 4);
                    if (p.Length == 4 && p[0] == "MSG")
                        result.Add((p[1], p[2], p[3]));
                }
            }
            catch { }
            return result;
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
