using Client_UI_App.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Client_UI_App.Forms
{
    internal partial class VideoCallForm : Form
    {
        private readonly string              _peerName;
        private readonly VideoCallService    _svc;
        private readonly VideoCaptureService? _capture;
        private System.Windows.Forms.Timer?  _timer;
        private int  _secondsElapsed;
        private bool _cameraOn  = true;
        private bool _hangupFired;

        // Screen sharing
        private ScreenCaptureService? _screenCapture;
        private bool _isScreenSharing;

        // Frame throttle: không queue quá nhiều BeginInvoke khi fps cao
        private volatile bool _remoteFramePending;
        private DateTime _lastLocalSent = DateTime.MinValue;
        private const int LocalIntervalMs = 50; // 20fps max cho local preview

        public event Action<string>? HangupRequested;

        public VideoCallForm(string peerName, VideoCallService svc,
                             VideoCaptureService? capture,
                             string initialStatus = "Đang kết nối...")
        {
            _peerName = peerName;
            _svc      = svc;
            _capture  = capture;
            InitializeComponent();
            lblStatus.Text   = initialStatus;
            lblPeerName.Text = peerName;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Đặt overlay local cam ở góc dưới-phải pnlVideoArea
            RepositionLocalOverlay();

            _svc.CallConnected       += OnCallConnected;
            _svc.CallEnded           += OnCallEnded;
            _svc.MicLevelChanged     += OnMicLevel;
            _svc.SpkLevelChanged     += OnSpkLevel;
            _svc.RemoteFrameReceived += OnRemoteFrame;

            if (_capture != null)
                _capture.FrameCaptured += OnLocalFrame;
            else
            {
                btnToggleCam.Enabled = false;
                lblNoCam.Visible     = true;
            }

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += (_, _) =>
            {
                _secondsElapsed++;
                lblTimer.Text = $"{_secondsElapsed / 60:D2}:{_secondsElapsed % 60:D2}";
            };
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RepositionLocalOverlay();
        }

        private void RepositionLocalOverlay()
        {
            if (pnlLocalOverlay == null || pnlVideoArea == null) return;
            int x = pnlVideoArea.Width  - pnlLocalOverlay.Width  - 10;
            int y = pnlVideoArea.Height - pnlLocalOverlay.Height - 10;
            pnlLocalOverlay.Location = new Point(Math.Max(0, x), Math.Max(0, y));
        }

        // ── Sự kiện từ VideoCallService ───────────────────────────────

        private void OnCallConnected()
        {
            if (IsDisposed) return;
            Invoke(() =>
            {
                lblStatus.Text      = "Đã kết nối  ✔";
                lblStatus.ForeColor = Color.FromArgb(0, 200, 120);
                _timer?.Start();
            });
        }

        private void OnCallEnded()
        {
            if (IsDisposed) return;
            Invoke(() =>
            {
                FireHangup();
                Close();
            });
        }

        private void OnMicLevel(float level)
        {
            if (IsDisposed) return;
            try { Invoke(() => pbMic.Value = Math.Min((int)(level * 1000), 1000)); } catch { }
        }

        private void OnSpkLevel(float level)
        {
            if (IsDisposed) return;
            try { Invoke(() => pbSpk.Value = Math.Min((int)(level * 1000), 1000)); } catch { }
        }

        // Frame từ peer — hiển thị vào picRemote
        private void OnRemoteFrame(Bitmap bmp)
        {
            if (IsDisposed || _remoteFramePending) { bmp.Dispose(); return; }
            _remoteFramePending = true;
            BeginInvoke(() =>
            {
                _remoteFramePending = false;
                if (IsDisposed) { bmp.Dispose(); return; }
                var old = picRemote.Image;
                picRemote.Image = bmp;
                old?.Dispose();
            });
        }

        // Frame từ webcam local — gửi tới peer + hiển thị vào picLocal
        private void OnLocalFrame(Bitmap bmp)
        {
            // Suppress webcam khi đang share màn hình
            if (IsDisposed || !_cameraOn || _isScreenSharing) { bmp.Dispose(); return; }

            // Throttle local preview (không cần 30fps cho preview)
            var now = DateTime.UtcNow;
            bool showLocal = (now - _lastLocalSent).TotalMilliseconds >= LocalIntervalMs;
            if (showLocal) _lastLocalSent = now;

            // Gửi tới peer (synchronous JPEG encode + UDP send)
            _svc.SendVideoFrame(bmp);

            if (showLocal)
            {
                // Clone để display (bmp gốc đã được SendVideoFrame dùng xong)
                var clone = (Bitmap)bmp.Clone();
                BeginInvoke(() =>
                {
                    if (IsDisposed) { clone.Dispose(); return; }
                    var old = picLocal.Image;
                    picLocal.Image = clone;
                    old?.Dispose();
                });
            }

            bmp.Dispose();
        }

        // ── Nút bấm ──────────────────────────────────────────────────

        private void btnMute_Click(object? sender, EventArgs e)
        {
            _svc.IsMuted      = !_svc.IsMuted;
            btnMute.Text      = _svc.IsMuted ? "🎤  Bật mic" : "🔇  Tắt mic";
            btnMute.BackColor = _svc.IsMuted
                ? Color.FromArgb(160, 50, 50)
                : Color.FromArgb(50, 50, 72);
            if (_svc.IsMuted) pbMic.Value = 0;
        }

        private void btnToggleCam_Click(object? sender, EventArgs e)
        {
            _cameraOn         = !_cameraOn;
            btnToggleCam.Text = _cameraOn ? "📷  Tắt cam" : "📷  Bật cam";
            if (!_cameraOn)
            {
                var old = picLocal.Image;
                picLocal.Image = null;
                old?.Dispose();
            }
        }

        private void btnHangup_Click(object? sender, EventArgs e)
        {
            FireHangup();
            Close();
        }

        // ── Screen sharing ─────────────────────────────────────────────

        private void btnScreenShare_Click(object? sender, EventArgs e)
        {
            if (!_isScreenSharing)
                StartScreenShare();
            else
                StopScreenShare();
        }

        private void StartScreenShare()
        {
            _isScreenSharing = true;
            _svc.TargetFrameSize = ScreenCaptureService.FrameSize;

            _screenCapture = new ScreenCaptureService();
            _screenCapture.FrameCaptured += OnScreenFrame;
            _screenCapture.Start();

            btnScreenShare.Text      = "🖥️  Dừng chia sẻ";
            btnScreenShare.BackColor = Color.FromArgb(180, 80, 50);
            btnScreenShare.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 100, 70);

            // Xóa local preview — sẽ được thay bằng frame màn hình
            var old = picLocal.Image;
            picLocal.Image = null;
            old?.Dispose();
        }

        private void StopScreenShare()
        {
            _isScreenSharing = false;
            _svc.TargetFrameSize = new Size(320, 240);

            if (_screenCapture != null)
            {
                _screenCapture.FrameCaptured -= OnScreenFrame;
                _screenCapture.Stop();
                _screenCapture.Dispose();
                _screenCapture = null;
            }

            btnScreenShare.Text      = "🖥️  Chia sẻ màn hình";
            btnScreenShare.BackColor = Color.FromArgb(50, 50, 72);
            btnScreenShare.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 92);
        }

        // Frame từ màn hình — gửi tới peer + cập nhật local preview
        private void OnScreenFrame(Bitmap bmp)
        {
            if (IsDisposed) { bmp.Dispose(); return; }

            _svc.SendVideoFrame(bmp);

            var now = DateTime.UtcNow;
            bool show = (now - _lastLocalSent).TotalMilliseconds >= LocalIntervalMs;
            if (show)
            {
                _lastLocalSent = now;
                var clone = (Bitmap)bmp.Clone();
                BeginInvoke(() =>
                {
                    if (IsDisposed) { clone.Dispose(); return; }
                    var old = picLocal.Image;
                    picLocal.Image = clone;
                    old?.Dispose();
                });
            }

            bmp.Dispose();
        }

        // Hiển thị caption (dùng cho bot video call — giống VoiceCallForm.AddSubtitle)
        public void AddCaption(string speaker, string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(() => AddCaption(speaker, text)); return; }
            // Hiện caption panel nếu chưa show (bot call)
            if (!pnlCaption.Visible)
            {
                pnlCaption.Visible = true;
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width,
                    this.ClientSize.Height + pnlCaption.Height);
            }
            Color clr = speaker == "UitiChan"
                ? Color.FromArgb(200, 80, 130)
                : Color.FromArgb(80, 140, 220);
            rtbCaption.SelectionStart  = rtbCaption.TextLength;
            rtbCaption.SelectionLength = 0;
            rtbCaption.SelectionColor  = Color.FromArgb(100, 100, 115);
            rtbCaption.AppendText($"[{DateTime.Now:HH:mm}]  ");
            rtbCaption.SelectionColor  = clr;
            rtbCaption.AppendText($"{speaker}: {text}\n");
            rtbCaption.ScrollToCaret();
        }

        private void VideoCallForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            FireHangup();

            _svc.CallConnected       -= OnCallConnected;
            _svc.CallEnded           -= OnCallEnded;
            _svc.MicLevelChanged     -= OnMicLevel;
            _svc.SpkLevelChanged     -= OnSpkLevel;
            _svc.RemoteFrameReceived -= OnRemoteFrame;

            if (_capture != null)
                _capture.FrameCaptured -= OnLocalFrame;

            if (_screenCapture != null)
            {
                _screenCapture.FrameCaptured -= OnScreenFrame;
                _screenCapture.Stop();
                _screenCapture.Dispose();
                _screenCapture = null;
            }

            _timer?.Stop();
            _timer?.Dispose();

            // Dispose ảnh còn treo
            picRemote.Image?.Dispose();
            picRemote.Image = null;
            picLocal.Image?.Dispose();
            picLocal.Image = null;
        }

        private void FireHangup()
        {
            if (_hangupFired) return;
            _hangupFired = true;
            HangupRequested?.Invoke(_peerName);
        }
    }
}
