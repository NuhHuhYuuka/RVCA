using AForge.Video;
using AForge.Video.DirectShow;
using System;
using System.Drawing;

namespace Client_UI_App.Services
{
    // Bắt khung hình webcam dùng AForge DirectShow (Phase 4 video call)
    // FrameCaptured fires trên capture thread → caller phải marshal sang UI nếu cần.
    internal sealed class VideoCaptureService : IDisposable
    {
        private VideoCaptureDevice? _device;
        private bool _disposed;

        public event Action<Bitmap>? FrameCaptured;

        public static string[] GetCameraNames()
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            var names   = new string[devices.Count];
            for (int i = 0; i < devices.Count; i++)
                names[i] = devices[i].Name;
            return names;
        }

        public bool IsRunning => _device?.IsRunning == true;

        public void Start(int deviceIndex = 0)
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0)
                throw new InvalidOperationException("Không tìm thấy webcam.");
            if (deviceIndex >= devices.Count) deviceIndex = 0;

            _device = new VideoCaptureDevice(devices[deviceIndex].MonikerString);

            // Chọn resolution 320×240; fallback về resolution nhỏ nhất để giảm bandwidth
            if (_device.VideoCapabilities.Length > 0)
            {
                VideoCapabilities best = _device.VideoCapabilities[0];
                foreach (var cap in _device.VideoCapabilities)
                {
                    if (cap.FrameSize.Width == 320 && cap.FrameSize.Height == 240)
                    { best = cap; break; }
                    if (cap.FrameSize.Width  < best.FrameSize.Width &&
                        cap.FrameSize.Width  >= 160)
                        best = cap;
                }
                _device.VideoResolution = best;
            }

            _device.NewFrame += OnNewFrame;
            _device.Start();
        }

        public void Stop()
        {
            if (_device == null) return;
            _device.NewFrame -= OnNewFrame;
            if (_device.IsRunning)
            {
                _device.SignalToStop();
                _device.WaitForStop();
            }
            _device = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        private void OnNewFrame(object sender, NewFrameEventArgs e)
        {
            if (_disposed) return;
            // Clone vì AForge tái dùng buffer frame sau khi handler return
            var bmp = (Bitmap)e.Frame.Clone();
            FrameCaptured?.Invoke(bmp);
        }
    }
}
