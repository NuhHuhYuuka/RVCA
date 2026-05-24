using Client_UI_App.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Client_UI_App.Forms
{
    // Hiển thị video call nhóm — mỗi thành viên là 1 tile 320×240
    internal partial class GroupVideoForm : Form
    {
        private readonly string             _myUsername;
        private readonly GroupVideoService  _svc;
        private readonly VideoCaptureService? _capture;

        // tiles: key = peerName, value = PictureBox hiển thị
        private readonly Dictionary<string, PictureBox> _tiles      = new();
        private readonly Dictionary<string, Label>      _tileLabels = new();

        // Frame throttle cho camera local
        private DateTime _lastLocalFrame = DateTime.MinValue;
        private const int LocalIntervalMs = 50;

        private bool _cameraOn = true;
        private volatile bool _selfFramePending;

        // Screen sharing — khi bật, thay frame webcam bằng frame màn hình
        private ScreenCaptureService? _screenCapture;
        private bool _isScreenSharing;

        public event Action? LeaveRequested;
        // Fired khi user toggle share screen — GroupChatForm broadcast tới peer
        public event Action<bool>? PresentRequested;

        // Kích thước tile bình thường (webcam) vs khi presenter (share screen)
        // BigW/H = ScreenCaptureService.FrameSize → tile render 1:1 không bị downscale.
        private const int NormalW = 320, NormalH = 240;
        private const int BigW    = 960, BigH    = 720;
        private const int LH       = 22;

        public GroupVideoForm(string myUsername, GroupVideoService svc, VideoCaptureService? capture)
        {
            _myUsername = myUsername;
            _svc        = svc;
            _capture    = capture;
            InitializeComponent();
            Text = $"📹 Group Video";
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Tile của mình
            AddTile(_myUsername, isSelf: true);

            _svc.FrameReceived    += OnRemoteFrame;
            _svc.MicLevelChanged  += OnMicLevel;
            _svc.PeerLevelChanged += OnPeerLevel;

            if (_capture != null)
            {
                _capture.FrameCaptured += OnLocalFrame;
                btnCam.Enabled = true;
            }
            else
            {
                btnCam.Enabled = false;
            }
        }

        // ── Thêm tile khi peer join ───────────────────────────────────────
        public void AddPeerTile(string peerName)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(() => AddPeerTile(peerName)); return; }
            if (_tiles.ContainsKey(peerName)) return;
            AddTile(peerName, isSelf: false);
        }

        // ── Xóa tile khi peer leave ───────────────────────────────────────
        public void RemovePeerTile(string peerName)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(() => RemovePeerTile(peerName)); return; }
            if (!_tiles.TryGetValue(peerName, out var pic)) return;

            var container = pic.Parent;
            flowTiles.Controls.Remove(container!);
            _tiles.Remove(peerName);
            _tileLabels.Remove(peerName);
            container?.Dispose();
        }

        // ── Sự kiện từ GroupVideoService ─────────────────────────────────

        private void OnRemoteFrame(string peerName, Bitmap bmp)
        {
            // Auto-tạo tile nếu chưa có (peer join sau khi form mở)
            if (!_tiles.ContainsKey(peerName) && !IsDisposed)
                Invoke(() => AddTile(peerName, isSelf: false));

            if (!_tiles.TryGetValue(peerName, out var pic)) { bmp.Dispose(); return; }
            if (IsDisposed) { bmp.Dispose(); return; }

            BeginInvoke(() =>
            {
                if (IsDisposed) { bmp.Dispose(); return; }
                var old = pic.Image;
                pic.Image = bmp;
                old?.Dispose();
            });
        }

        private void OnLocalFrame(Bitmap bmp)
        {
            // Khi share màn hình thì bỏ qua frame webcam (screen frames thay thế)
            if (IsDisposed || !_cameraOn || _isScreenSharing) { bmp.Dispose(); return; }

            // Gửi tới các peer qua UDP
            _svc.SendVideoFrame(bmp);

            // Throttle preview để tránh spam BeginInvoke
            var now = DateTime.UtcNow;
            bool show = (now - _lastLocalFrame).TotalMilliseconds >= LocalIntervalMs
                        && !_selfFramePending;
            if (!show) { bmp.Dispose(); return; }
            _lastLocalFrame    = now;
            _selfFramePending  = true;

            var clone = (Bitmap)bmp.Clone();
            bmp.Dispose();

            BeginInvoke(() =>
            {
                _selfFramePending = false;
                if (IsDisposed) { clone.Dispose(); return; }
                if (!_tiles.TryGetValue(_myUsername, out var pic)) { clone.Dispose(); return; }
                var old = pic.Image;
                pic.Image = clone;
                old?.Dispose();
            });
        }

        private void OnMicLevel(float level)
        {
            if (IsDisposed) return;
            try { Invoke(() => pbMic.Value = Math.Min((int)(level * 1000), 1000)); } catch { }
        }

        private void OnPeerLevel(string username, float level)
        {
            if (IsDisposed) return;
            if (!_tileLabels.TryGetValue(username, out var lbl)) return;
            bool speaking = level > 0.015f;
            try
            {
                BeginInvoke(() =>
                {
                    if (lbl.IsDisposed) return;
                    lbl.BackColor = speaking
                        ? Color.FromArgb(20, 60, 35)
                        : Color.FromArgb(20, 20, 32);
                    lbl.ForeColor = speaking
                        ? Color.FromArgb(80, 220, 120)
                        : Color.FromArgb(80, 150, 230);
                });
            }
            catch { }
        }

        // ── Resize tile khi peer share/dừng screen — gọi từ GroupChatForm ─
        public void SetPresenter(string name, bool isPresenting)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(() => SetPresenter(name, isPresenting)); return; }
            if (!_tiles.TryGetValue(name, out var pic)) return;

            int W = isPresenting ? BigW : NormalW;
            int H = isPresenting ? BigH : NormalH;
            var tile = pic.Parent;
            if (tile == null) return;

            tile.Size = new Size(W, H + LH);
            pic.Size  = new Size(W, H);
            if (_tileLabels.TryGetValue(name, out var lbl))
            {
                lbl.Size     = new Size(W, LH);
                lbl.Location = new Point(0, H);
            }
        }

        // ── Tạo 1 tile (panel + picturebox + label) ───────────────────────
        private void AddTile(string name, bool isSelf)
        {
            int W = NormalW, H = NormalH;

            var tile = new Panel
            {
                Size      = new Size(W, H + LH),
                BackColor = Color.FromArgb(14, 14, 22),
                Margin    = new Padding(4)
            };

            var pic = new PictureBox
            {
                Size     = new Size(W, H),
                Location = new Point(0, 0),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            string label = isSelf ? $"{name}  (bạn)" : name;
            var lbl = new Label
            {
                Text      = label,
                Size      = new Size(W, LH),
                Location  = new Point(0, H),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = isSelf
                    ? Color.FromArgb(80, 200, 140)
                    : Color.FromArgb(80, 150, 230),
                BackColor = Color.FromArgb(20, 20, 32)
            };

            tile.Controls.Add(pic);
            tile.Controls.Add(lbl);
            flowTiles.Controls.Add(tile);
            _tiles[name]      = pic;
            _tileLabels[name] = lbl;
        }

        // ── Nút bấm ──────────────────────────────────────────────────────

        private void btnMute_Click(object? sender, EventArgs e)
        {
            _svc.IsMuted   = !_svc.IsMuted;
            btnMute.Text   = _svc.IsMuted ? "🎤 Bật mic" : "🔇 Tắt mic";
            if (_svc.IsMuted) pbMic.Value = 0;
        }

        private void btnCam_Click(object? sender, EventArgs e)
        {
            _cameraOn    = !_cameraOn;
            btnCam.Text  = _cameraOn ? "📷 Tắt cam" : "📷 Bật cam";
            if (!_cameraOn && _tiles.TryGetValue(_myUsername, out var pic))
            {
                var old = pic.Image;
                pic.Image = null;
                old?.Dispose();
            }
        }

        // ── Screen share ─────────────────────────────────────────────────
        private void btnScreen_Click(object? sender, EventArgs e)
        {
            if (!_isScreenSharing) StartScreenShare();
            else                   StopScreenShare();
        }

        private void StartScreenShare()
        {
            _isScreenSharing      = true;
            _svc.TargetFrameSize  = ScreenCaptureService.FrameSize;

            _screenCapture = new ScreenCaptureService();
            _screenCapture.FrameCaptured += OnScreenFrame;
            _screenCapture.Start();

            btnScreen.Text      = "🖥️ Dừng chia sẻ";
            btnScreen.BackColor = Color.FromArgb(180, 80, 50);

            // Phóng to tile của mình và clear preview cũ
            SetPresenter(_myUsername, true);
            if (_tiles.TryGetValue(_myUsername, out var pic))
            {
                var old = pic.Image;
                pic.Image = null;
                old?.Dispose();
            }

            // Báo peer: mình bắt đầu share → họ phóng to tile của mình ở UI họ
            PresentRequested?.Invoke(true);
        }

        private void StopScreenShare()
        {
            _isScreenSharing     = false;
            _svc.TargetFrameSize = new Size(320, 240);

            if (_screenCapture != null)
            {
                _screenCapture.FrameCaptured -= OnScreenFrame;
                _screenCapture.Stop();
                _screenCapture.Dispose();
                _screenCapture = null;
            }

            btnScreen.Text      = "🖥️ Chia sẻ MH";
            btnScreen.BackColor = Color.FromArgb(50, 50, 72);

            // Trở lại tile bình thường + báo peer
            SetPresenter(_myUsername, false);
            PresentRequested?.Invoke(false);
        }

        // Frame từ màn hình → gửi tới tất cả peer + hiển thị local preview
        private void OnScreenFrame(Bitmap bmp)
        {
            if (IsDisposed) { bmp.Dispose(); return; }

            _svc.SendVideoFrame(bmp);

            var now = DateTime.UtcNow;
            bool show = (now - _lastLocalFrame).TotalMilliseconds >= LocalIntervalMs
                        && !_selfFramePending;
            if (show)
            {
                _lastLocalFrame   = now;
                _selfFramePending = true;
                var clone = (Bitmap)bmp.Clone();
                BeginInvoke(() =>
                {
                    _selfFramePending = false;
                    if (IsDisposed) { clone.Dispose(); return; }
                    if (!_tiles.TryGetValue(_myUsername, out var pic)) { clone.Dispose(); return; }
                    var old = pic.Image;
                    pic.Image = clone;
                    old?.Dispose();
                });
            }

            bmp.Dispose();
        }

        private void btnLeave_Click(object? sender, EventArgs e)
        {
            LeaveRequested?.Invoke();
            Close();
        }

        private void GroupVideoForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _svc.FrameReceived    -= OnRemoteFrame;
            _svc.MicLevelChanged  -= OnMicLevel;
            _svc.PeerLevelChanged -= OnPeerLevel;

            if (_capture != null)
                _capture.FrameCaptured -= OnLocalFrame;

            if (_screenCapture != null)
            {
                _screenCapture.FrameCaptured -= OnScreenFrame;
                _screenCapture.Stop();
                _screenCapture.Dispose();
                _screenCapture = null;
            }

            // Dispose tất cả ảnh còn treo
            foreach (var pic in _tiles.Values)
            {
                pic.Image?.Dispose();
                pic.Image = null;
            }
        }
    }
}
