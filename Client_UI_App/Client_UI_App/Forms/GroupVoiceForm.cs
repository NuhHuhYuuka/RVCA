using Client_UI_App.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Client_UI_App.Forms
{
    internal partial class GroupVoiceForm : Form
    {
        private readonly string            _myUsername;
        private readonly GroupVoiceService _svc;

        private readonly Dictionary<string, Label> _memberLabels = new();

        public event Action? LeaveRequested;

        public GroupVoiceForm(string myUsername, GroupVoiceService svc)
        {
            _myUsername = myUsername;
            _svc        = svc;
            InitializeComponent();
            Text = "🎙️ Voice Channel";
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            AddMember(_myUsername);
            _svc.MicLevelChanged  += OnMicLevel;
            _svc.PeerLevelChanged += OnPeerLevel;
        }

        // ── Thêm thành viên vào danh sách ────────────────────────────
        public void AddMember(string name)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(() => AddMember(name)); return; }
            if (_memberLabels.ContainsKey(name)) return;

            bool isSelf = name == _myUsername;
            int lblW = Math.Max(flowMembers.ClientSize.Width - 20, 180);
            var lbl = new Label
            {
                Text      = isSelf ? $"🎙️  {name}  (bạn)" : $"🎙️  {name}",
                AutoSize  = false,
                Size      = new Size(lblW, 32),
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Segoe UI", 10F, isSelf ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isSelf
                    ? Color.FromArgb(80, 200, 140)
                    : Color.FromArgb(200, 200, 220),
                BackColor = Color.FromArgb(28, 28, 42),
                Padding   = new Padding(8, 0, 0, 0),
                Margin    = new Padding(2)
            };
            _memberLabels[name] = lbl;
            flowMembers.Controls.Add(lbl);
        }

        // ── Xóa thành viên khỏi danh sách ────────────────────────────
        public void RemoveMember(string name)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(() => RemoveMember(name)); return; }
            if (!_memberLabels.TryGetValue(name, out var lbl)) return;
            flowMembers.Controls.Remove(lbl);
            lbl.Dispose();
            _memberLabels.Remove(name);
        }

        private void OnMicLevel(float level)
        {
            if (IsDisposed) return;
            try { Invoke(() => pbMic.Value = Math.Min((int)(level * 1000), 1000)); } catch { }
        }

        private void OnPeerLevel(string username, float level)
        {
            if (IsDisposed) return;
            if (!_memberLabels.TryGetValue(username, out var lbl)) return;
            bool speaking = level > 0.015f;
            try
            {
                BeginInvoke(() =>
                {
                    if (lbl.IsDisposed) return;
                    lbl.BackColor = speaking
                        ? Color.FromArgb(20, 60, 35)
                        : Color.FromArgb(28, 28, 42);
                    lbl.ForeColor = speaking
                        ? Color.FromArgb(80, 220, 120)
                        : Color.FromArgb(200, 200, 220);
                });
            }
            catch { }
        }

        private void btnMute_Click(object? sender, EventArgs e)
        {
            _svc.IsMuted   = !_svc.IsMuted;
            btnMute.Text   = _svc.IsMuted ? "🎤 Bật mic" : "🔇 Tắt mic";
            if (_svc.IsMuted) pbMic.Value = 0;
        }

        private void btnLeave_Click(object? sender, EventArgs e)
        {
            LeaveRequested?.Invoke();
            Close();
        }

        private void GroupVoiceForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _svc.MicLevelChanged  -= OnMicLevel;
            _svc.PeerLevelChanged -= OnPeerLevel;
        }
    }
}
