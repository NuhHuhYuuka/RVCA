using Client_UI_App.Forms;
using System.Text;

namespace Client_UI_App
{
    internal static class Program
    {
        // Ghi log vào %APPDATA%\ChatApp_crash.log — đọc file này sau khi app tắt
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChatApp_crash.log");

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // UI thread exception
            Application.ThreadException += (_, e) =>
            {
                WriteLog("UI_THREAD", e.Exception);
                MessageBox.Show(FormatEx(e.Exception),
                    "Crash (UI thread) — xem " + LogPath,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // Background thread exception — ghi file TRƯỚC khi process terminate
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                WriteLog("BACKGROUND_THREAD", ex);
                // Process sắp die — không dùng MessageBox, file log là đủ
            };

            Application.Run(new AuthForm());
        }

        private static void WriteLog(string source, Exception? ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss}  [{source}] ===");
                sb.AppendLine(ex?.GetType().FullName ?? "null");
                sb.AppendLine(ex?.Message ?? "");
                sb.AppendLine(ex?.StackTrace ?? "");
                if (ex?.InnerException != null)
                {
                    sb.AppendLine("--- InnerException ---");
                    sb.AppendLine(ex.InnerException.GetType().FullName);
                    sb.AppendLine(ex.InnerException.Message);
                    sb.AppendLine(ex.InnerException.StackTrace);
                }
                sb.AppendLine();
                File.AppendAllText(LogPath, sb.ToString());
            }
            catch { /* không làm gì nếu write file lỗi */ }
        }

        private static string FormatEx(Exception? ex)
            => ex is null ? "Unknown" : $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
    }
}
