using System;
using System.IO;
using System.Text.Json;

namespace Client_UI_App.Services
{
    // Đọc appsettings.json một lần duy nhất khi khởi động
    // Fallback về giá trị mặc định nếu file không tồn tại hoặc thiếu field
    internal static class AppConfig
    {
        public static string   LbIp        { get; }
        public static int      LbPort      { get; }
        public static int[]    DirPorts    { get; }

        static AppConfig()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            if (!File.Exists(path))
            {
                LbIp     = "127.0.0.1";
                LbPort   = 9000;
                DirPorts = new[] { 8888, 8889 };
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var server    = doc.RootElement.GetProperty("Server");

                LbIp   = server.TryGetProperty("LoadBalancerIp",   out var ip)   ? ip.GetString()!   : "127.0.0.1";
                LbPort = server.TryGetProperty("LoadBalancerPort",  out var port) ? port.GetInt32()   : 9000;

                if (server.TryGetProperty("DirectoryPorts", out var dp) && dp.ValueKind == JsonValueKind.Array)
                {
                    var list = new System.Collections.Generic.List<int>();
                    foreach (var elem in dp.EnumerateArray()) list.Add(elem.GetInt32());
                    DirPorts = list.ToArray();
                }
                else
                {
                    DirPorts = new[] { 8888, 8889 };
                }

            }
            catch
            {
                LbIp     = "127.0.0.1";
                LbPort   = 9000;
                DirPorts = new[] { 8888, 8889 };
            }
        }
    }
}
