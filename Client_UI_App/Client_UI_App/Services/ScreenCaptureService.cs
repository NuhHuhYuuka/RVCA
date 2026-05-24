using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client_UI_App.Services
{
    // Chụp màn hình để chia sẻ qua VideoCallService (thay thế webcam).
    // FrameCaptured fires trên thread pool — caller tự marshal sang UI nếu cần.
    internal sealed class ScreenCaptureService : IDisposable
    {
        private CancellationTokenSource? _cts;
        private bool _disposed;

        // Output 960×720: đọc rõ text màn hình hơn, vẫn fit trong UDP packet (~65KB max).
        // Quality 60 ở GroupVideoService giúp text sắc nét hơn so với q40 cũ.
        public static readonly Size FrameSize = new(960, 720);

        public event Action<Bitmap>? FrameCaptured;

        public void Start(int fps = 10)
        {
            if (_cts != null || _disposed) return;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => CaptureLoopAsync(_cts.Token, Math.Clamp(fps, 1, 20)));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private async Task CaptureLoopAsync(CancellationToken ct, int fps)
        {
            int intervalMs = 1000 / fps;

            while (!ct.IsCancellationRequested && !_disposed)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var bounds = Screen.PrimaryScreen?.Bounds
                                 ?? new Rectangle(0, 0, 1920, 1080);

                    // Chụp toàn bộ màn hình chính
                    using var full = new Bitmap(bounds.Width, bounds.Height);
                    using (var g = Graphics.FromImage(full))
                        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                    // Scale xuống 640×480 trước khi gửi
                    var scaled = new Bitmap(full, FrameSize);
                    FrameCaptured?.Invoke(scaled);
                }
                catch (OperationCanceledException) { break; }
                catch { /* GPU bận / màn hình lock — bỏ qua frame này */ }

                int delay = Math.Max(0, intervalMs - (int)sw.ElapsedMilliseconds);
                try { if (delay > 0) await Task.Delay(delay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
