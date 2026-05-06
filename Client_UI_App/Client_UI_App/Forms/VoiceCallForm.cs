using Client_UI_App.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Client_UI_App.Forms
{
    internal partial class VoiceCallForm : Form
    {
        private readonly string           _peerName;
        private readonly VoiceCallService _svc;
        private readonly bool             _isBotCall;
        private System.Windows.Forms.Timer? _timer;
        private int _secondsElapsed;

        // Fired khi người dùng cúp máy (truyền peerName để MainChatForm gửi VOICE_HANGUP)
        public event Action<string>? HangupRequested;

        private readonly string _initialStatus;

        public VoiceCallForm(string peerName, VoiceCallService svc,
                             string initialStatus = "Đang kết nối...",
                             bool isBotCall = false)
        {
            _peerName      = peerName;
            _svc           = svc;
            _initialStatus = initialStatus;
            _isBotCall     = isBotCall;
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            lblPeerName.Text = _peerName;
            lblStatus.Text   = _initialStatus;

            // Load avatar peer
            Bitmap? bmp = _peerName == "UitiChan"
                ? (AvatarService.LoadBitmap(AvatarService.BotAvatarPath)
                   ?? AvatarService.CreateInitialsBitmap("U", 80))
                : AvatarService.CreateInitialsBitmap(_peerName, 80);
            AvatarService.ApplyCircularClip(picAvatar);
            picAvatar.Image = bmp;

            // Hiện subtitle panel và mở rộng form khi gọi bot
            if (_isBotCall)
            {
                pnlSubtitle.Visible = true;
                this.ClientSize     = new System.Drawing.Size(
                    this.ClientSize.Width, this.ClientSize.Height + pnlSubtitle.Height);
            }

            _svc.CallConnected   += OnCallConnected;
            _svc.CallEnded       += OnCallEnded;
            _svc.MicLevelChanged += OnMicLevel;
            _svc.SpkLevelChanged += OnSpkLevel;

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += OnTimerTick;
        }

        // Thêm dòng subtitle (gọi từ background task đọc VOICE_CAPTION)
        public void AddSubtitle(string speaker, string text)
        {
            if (IsDisposed || !_isBotCall) return;
            if (InvokeRequired) { Invoke(() => AddSubtitle(speaker, text)); return; }

            Color clr = speaker == "UitiChan"
                ? Color.FromArgb(200, 80, 130)
                : Color.FromArgb(80, 140, 220);

            rtbSubtitle.SelectionStart  = rtbSubtitle.TextLength;
            rtbSubtitle.SelectionLength = 0;
            rtbSubtitle.SelectionColor  = Color.FromArgb(100, 100, 115);
            rtbSubtitle.AppendText($"[{DateTime.Now:HH:mm}]  ");
            rtbSubtitle.SelectionColor  = clr;
            rtbSubtitle.AppendText($"{speaker}: {text}\n");
            rtbSubtitle.ScrollToCaret();
        }

        private void OnTimerTick(object? s, EventArgs e)
        {
            _secondsElapsed++;
            lblTimer.Text = $"{_secondsElapsed / 60:D2}:{_secondsElapsed % 60:D2}";
        }

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
                HangupRequested?.Invoke(_peerName);
                Close();
            });
        }

        private void OnMicLevel(float level)
        {
            if (IsDisposed) return;
            Invoke(() => pbMic.Value = Math.Min((int)(level * 1000), 1000));
        }

        private void OnSpkLevel(float level)
        {
            if (IsDisposed) return;
            Invoke(() => pbSpk.Value = Math.Min((int)(level * 1000), 1000));
        }

        private void btnMute_Click(object? sender, EventArgs e)
        {
            _svc.IsMuted  = !_svc.IsMuted;
            btnMute.Text      = _svc.IsMuted ? "🎤  Bật mic" : "🔇  Tắt mic";
            btnMute.BackColor = _svc.IsMuted
                ? Color.FromArgb(160, 50, 50)
                : Color.FromArgb(50, 50, 72);
        }

        private void btnHangup_Click(object? sender, EventArgs e)
        {
            HangupRequested?.Invoke(_peerName);
            Close();
        }

        private void VoiceCallForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _svc.CallConnected   -= OnCallConnected;
            _svc.CallEnded       -= OnCallEnded;
            _svc.MicLevelChanged -= OnMicLevel;
            _svc.SpkLevelChanged -= OnSpkLevel;
        }
    }
}
