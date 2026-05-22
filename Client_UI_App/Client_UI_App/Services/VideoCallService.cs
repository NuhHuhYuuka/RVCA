using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client_UI_App.Services
{
    // Video Call Service — ghép VoiceCallService (audio Opus UDP) với video JPEG UDP.
    //
    // Signaling (bên ngoài, qua TCP):
    //   VIDEO_OFFER|callerName|audioUdpPort|videoUdpPort
    //   VIDEO_ANSWER|callerName|audioUdpPort|videoUdpPort
    //   VIDEO_REJECT|callerName
    //   VIDEO_HANGUP|callerName
    //
    // Video packet format: [0x56 magic][len_4bytes_BE][jpeg_data]
    // Frame size: 320×240 JPEG quality 40 ≈ 5-15 KB — phù hợp UDP LAN.
    internal sealed class VideoCallService : IDisposable
    {
        private static readonly ImageCodecInfo _jpegCodec = FindJpegCodec();

        private UdpClient?               _videoRecv;
        private UdpClient?               _videoSend;
        private IPEndPoint?              _videoRemoteEp;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        // Audio được delegate hoàn toàn cho VoiceCallService
        public VoiceCallService Audio { get; } = new();

        public int AudioLocalPort => Audio.LocalUdpPort;
        public int VideoLocalPort { get; private set; }
        public bool IsMuted { get => Audio.IsMuted; set => Audio.IsMuted = value; }

        // Events proxy từ audio + event riêng cho video frame
        public event Action?         CallConnected   { add => Audio.CallConnected   += value; remove => Audio.CallConnected   -= value; }
        public event Action?         CallEnded       { add => Audio.CallEnded       += value; remove => Audio.CallEnded       -= value; }
        public event Action<float>?  MicLevelChanged { add => Audio.MicLevelChanged += value; remove => Audio.MicLevelChanged -= value; }
        public event Action<float>?  SpkLevelChanged { add => Audio.SpkLevelChanged += value; remove => Audio.SpkLevelChanged -= value; }
        public event Action<Bitmap>? RemoteFrameReceived;

        // ── Bước 1: chuẩn bị 2 UDP socket, lấy ports để gửi qua signaling ──
        public void Prepare()
        {
            Audio.PrepareUdp();
            _videoRecv    = new UdpClient(0);
            VideoLocalPort = ((IPEndPoint)_videoRecv.Client.LocalEndPoint!).Port;
        }

        // ── Bước 2: set endpoint sau khi nhận ports từ peer qua signaling ──
        public void SetRemoteEndpoint(string ip, int audioPort, int videoPort)
        {
            Audio.SetRemoteEndpoint(ip, audioPort);
            _videoRemoteEp = new IPEndPoint(IPAddress.Parse(ip), videoPort);
            _videoSend     = new UdpClient();
        }

        // ── Bước 3: bắt đầu audio + video receive loop ──────────────────
        public void Start()
        {
            if (_disposed) return;
            Audio.StartAudio();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => VideoReceiveLoopAsync(_cts.Token));
        }

        // Gọi từ VideoCaptureService.FrameCaptured — scale xuống 320×240, JPEG, gửi UDP
        public void SendVideoFrame(Bitmap bmp)
        {
            if (_videoSend == null || _videoRemoteEp == null || _disposed) return;
            try
            {
                Bitmap src     = bmp.Width == 320 && bmp.Height == 240
                    ? bmp
                    : new Bitmap(bmp, new Size(320, 240));

                using var ms   = new MemoryStream(16384);
                using var ep   = new EncoderParameters(1);
                ep.Param[0]    = new EncoderParameter(Encoder.Quality, 40L);
                src.Save(ms, _jpegCodec, ep);
                if (!ReferenceEquals(src, bmp)) src.Dispose();

                byte[] jpg = ms.ToArray();
                if (jpg.Length > 60000) return; // skip oversized (lỗi encoder)

                // [magic=0x56][len_hi][len_3][len_2][len_lo][jpg...]
                byte[] pkt = new byte[5 + jpg.Length];
                pkt[0] = 0x56;
                int len = jpg.Length;
                pkt[1] = (byte)(len >> 24);
                pkt[2] = (byte)(len >> 16);
                pkt[3] = (byte)(len >>  8);
                pkt[4] = (byte)(len      );
                Buffer.BlockCopy(jpg, 0, pkt, 5, jpg.Length);

                _videoSend.Send(pkt, pkt.Length, _videoRemoteEp);
            }
            catch { }
        }

        public void Stop()
        {
            if (_disposed) return;
            _disposed = true;
            _cts?.Cancel();
            Audio.Stop();
            try { _videoSend?.Close(); } catch { }
            try { _videoRecv?.Close(); } catch { }
            _videoSend = null;
            _videoRecv = null;
        }

        public void Dispose() => Stop();

        // ── Video receive loop ─────────────────────────────────────────
        private async Task VideoReceiveLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _videoRecv!.ReceiveAsync(ct);
                    byte[] data = result.Buffer;

                    if (data.Length < 6 || data[0] != 0x56) continue;

                    int jpgLen = (data[1] << 24) | (data[2] << 16)
                               | (data[3] <<  8) |  data[4];
                    if (jpgLen <= 0 || 5 + jpgLen > data.Length) continue;

                    // GDI+ giữ reference tới stream trong lifetime của Bitmap.
                    // Tạo bản sao độc lập để stream có thể được giải phóng an toàn.
                    Bitmap bmp;
                    using (var ms = new MemoryStream(data, 5, jpgLen))
                    using (var tmp = System.Drawing.Image.FromStream(ms))
                        bmp = new Bitmap(tmp);

                    RemoteFrameReceived?.Invoke(bmp);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        }

        private static ImageCodecInfo FindJpegCodec()
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (c.MimeType == "image/jpeg") return c;
            throw new InvalidOperationException("JPEG codec not found");
        }
    }
}
