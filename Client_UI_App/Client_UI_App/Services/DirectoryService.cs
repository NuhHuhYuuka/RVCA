using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Xử lý toàn bộ giao tiếp với Load Balancer và Directory Server
    // Wire protocol: StreamWriter.WriteLine / StreamReader.ReadLine (text, pipe-delimited)
    internal static class DirectoryService
    {
        private const string LbIp   = "127.0.0.1";
        private const int    LbPort = 9000;

        // Danh sách tất cả Directory Server ports — dùng để merge LIST_USERS
        private static readonly int[] AllDirPorts = { 8888, 8889 };

        // ── Bước 1: Xin vé từ Load Balancer, nhận Port của Directory Server ──
        // Load Balancer gửi raw UTF-8 bytes (KHÔNG có newline)
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

        // ── Bước 2a: Đăng ký tài khoản mới ──
        // Gửi: SIGNUP|username|password
        // Nhận: SIGNUP_SUCCESS|msg  hoặc  SIGNUP_FAILED|msg
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
            bool ok  = parts[0] == "SIGNUP_SUCCESS";
            string msg = parts.Length > 1 ? parts[1] : response;
            return (ok, msg);
        }

        // ── Bước 2b: Đăng nhập và lấy danh sách user online ──
        // Gửi: LOGIN|username|password|myListeningPort
        // Nhận: SUCCESS|user1,user2,...  hoặc  LOGIN_FAILED|msg
        public static async Task<(bool success, string message, List<string> onlineUsers)> LoginAsync(
            int dirPort, string username, string password, int myListeningPort)
        {
            using TcpClient client = new();
            await client.ConnectAsync(LbIp, dirPort);

            await using NetworkStream stream = client.GetStream();
            using StreamReader  reader = new(stream, Encoding.UTF8, leaveOpen: true);
            await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

            await writer.WriteLineAsync($"LOGIN|{username}|{password}|{myListeningPort}");

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
        // Nhận: GETUSER_SUCCESS|IP:Port  hoặc  GETUSER_NOTFOUND
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
        // Mỗi Directory Server instance có ConcurrentDictionary riêng
        // → phải hỏi cả 2 để có danh sách đầy đủ
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
        // Gửi: LOGOUT|username
        public static async Task LogoutAsync(int dirPort, string username)
        {
            try
            {
                using TcpClient client = new();
                await client.ConnectAsync(LbIp, dirPort);

                await using NetworkStream stream = client.GetStream();
                await using StreamWriter writer = new(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };

                await writer.WriteLineAsync($"LOGOUT|{username}");
                await Task.Delay(300); // Đảm bảo gói tin bay đi trước khi đóng
            }
            catch { /* Bỏ qua lỗi khi đóng app */ }
        }
    }
}
