using System;
using System.Threading;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Poll relay_messages từ server mỗi 1.5s, inject vào P2PListenerService
    // Dùng khi 2 client ở khác router (NAT chặn P2P trực tiếp)
    internal static class RelayPollerService
    {
        private static CancellationTokenSource? _cts;
        private static string _username = "";

        public static void Start(string username)
        {
            if (_cts != null) return;
            _username = username;
            _cts      = new CancellationTokenSource();
            _ = Task.Run(() => PollLoopAsync(_cts.Token));
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private static async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var messages = await DirectoryService.PollAsync(_username);
                    foreach (var (_, senderIp, line) in messages)
                        P2PListenerService.InjectRelayedLine(senderIp, line);
                }
                catch { }

                try { await Task.Delay(1500, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
