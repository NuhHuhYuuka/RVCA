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
        private readonly Dictionary<string, PictureBox> _tiles = new();

        // Frame throttle cho camera local
        private DateTime _lastLocalFrame = DateTime.MinValue;
        private const int LocalIntervalMs = 50;

        private bool _cameraOn = true;
        private volatile bool _selfFramePending;

        public event Action? LeaveRequested;

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

            _svc.FrameReceived   += OnRemoteFrame;
            _svc.MicLevelChanged += OnMicLevel;

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
            if (IsDisposed || !_cameraOn) { bmp.Dispose(); return; }

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

        // ── Tạo 1 tile (panel + picturebox + label) ───────────────────────
        private void AddTile(string name, bool isSelf)
        {
            const int W = 320, H = 240, LH = 22;

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
            _tiles[name] = pic;
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

        private void btnLeave_Click(object? sender, EventArgs e)
        {
            LeaveRequested?.Invoke();
            Close();
        }

        private void GroupVideoForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _svc.FrameReceived   -= OnRemoteFrame;
            _svc.MicLevelChanged -= OnMicLevel;

            if (_capture != null)
                _capture.FrameCaptured -= OnLocalFrame;

            // Dispose tất cả ảnh còn treo
            foreach (var pic in _tiles.Values)
            {
                pic.Image?.Dispose();
                pic.Image = null;
            }
        }
    }
}
