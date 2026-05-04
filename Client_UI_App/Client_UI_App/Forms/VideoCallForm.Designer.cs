#nullable disable
namespace Client_UI_App.Forms
{
    partial class VideoCallForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // ── Controls ────────────────────────────────────────────────
            lblStatus       = new System.Windows.Forms.Label();
            pnlCaption      = new System.Windows.Forms.Panel();
            rtbCaption      = new System.Windows.Forms.RichTextBox();
            pnlVideoArea    = new System.Windows.Forms.Panel();
            picRemote       = new System.Windows.Forms.PictureBox();
            pnlLocalOverlay = new System.Windows.Forms.Panel();
            picLocal        = new System.Windows.Forms.PictureBox();
            lblNoCam        = new System.Windows.Forms.Label();
            pnlCtrl         = new System.Windows.Forms.Panel();
            lblPeerName     = new System.Windows.Forms.Label();
            lblTimer        = new System.Windows.Forms.Label();
            lblMicIcon      = new System.Windows.Forms.Label();
            pbMic           = new System.Windows.Forms.ProgressBar();
            lblSpkIcon      = new System.Windows.Forms.Label();
            pbSpk           = new System.Windows.Forms.ProgressBar();
            btnMute         = new System.Windows.Forms.Button();
            btnToggleCam    = new System.Windows.Forms.Button();
            btnHangup       = new System.Windows.Forms.Button();

            pnlCaption.SuspendLayout();
            pnlVideoArea.SuspendLayout();
            pnlLocalOverlay.SuspendLayout();
            pnlCtrl.SuspendLayout();
            this.SuspendLayout();

            // ── Bảng màu ────────────────────────────────────────────────
            var clrBg      = Color.FromArgb(12,  12,  18);
            var clrCard    = Color.FromArgb(22,  22,  32);
            var clrText    = Color.FromArgb(220, 220, 235);
            var clrHint    = Color.FromArgb(120, 120, 148);
            var clrGreen   = Color.FromArgb(0,   200, 120);
            var clrRed     = Color.FromArgb(210,  50,  60);
            var clrMicBar  = Color.FromArgb(80,  200, 140);
            var clrSpkBar  = Color.FromArgb(80,  150, 230);
            var clrBlue    = Color.FromArgb(0,   100, 200);

            // ── pnlCaption: subtitle bot call (Dock=Bottom, ẩn mặc định) ──
            pnlCaption.Dock      = DockStyle.Bottom;
            pnlCaption.Height    = 130;
            pnlCaption.BackColor = Color.FromArgb(14, 14, 22);
            pnlCaption.Visible   = false;

            rtbCaption.Dock        = DockStyle.Fill;
            rtbCaption.ReadOnly    = true;
            rtbCaption.BackColor   = Color.FromArgb(14, 14, 22);
            rtbCaption.ForeColor   = Color.FromArgb(200, 200, 220);
            rtbCaption.Font        = new Font("Segoe UI", 13F);
            rtbCaption.BorderStyle = BorderStyle.None;
            rtbCaption.ScrollBars  = RichTextBoxScrollBars.Vertical;
            rtbCaption.Padding     = new Padding(6);
            pnlCaption.Controls.Add(rtbCaption);

            // ── lblStatus: Dock=Top ──────────────────────────────────────
            lblStatus.Dock      = DockStyle.Top;
            lblStatus.Height    = 28;
            lblStatus.Text      = "Đang kết nối...";
            lblStatus.Font      = new Font("Segoe UI", 11F, FontStyle.Italic);
            lblStatus.ForeColor = clrHint;
            lblStatus.BackColor = clrBg;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;

            // ── pnlCtrl: Dock=Bottom ─────────────────────────────────────
            pnlCtrl.Dock      = DockStyle.Bottom;
            pnlCtrl.Height    = 84;
            pnlCtrl.BackColor = clrCard;

            // Absolute layout bên trong pnlCtrl (form fixed 800px wide)
            lblPeerName.AutoSize  = false;
            lblPeerName.Location  = new Point(12, 8);
            lblPeerName.Size      = new Size(210, 26);
            lblPeerName.Font      = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblPeerName.ForeColor = clrText;
            lblPeerName.BackColor = clrCard;
            lblPeerName.Text      = "...";

            lblTimer.AutoSize  = false;
            lblTimer.Location  = new Point(230, 8);
            lblTimer.Size      = new Size(100, 26);
            lblTimer.Font      = new Font("Consolas", 14F, FontStyle.Bold);
            lblTimer.ForeColor = clrGreen;
            lblTimer.BackColor = clrCard;
            lblTimer.Text      = "00:00";

            lblMicIcon.AutoSize  = false;
            lblMicIcon.Location  = new Point(340, 6);
            lblMicIcon.Size      = new Size(26, 22);
            lblMicIcon.Text      = "🎤";
            lblMicIcon.Font      = new Font("Segoe UI", 10F);
            lblMicIcon.ForeColor = clrHint;
            lblMicIcon.BackColor = clrCard;

            pbMic.Location  = new Point(368, 10);
            pbMic.Size      = new Size(200, 14);
            pbMic.Minimum   = 0;
            pbMic.Maximum   = 1000;
            pbMic.Value     = 0;
            pbMic.Style     = ProgressBarStyle.Continuous;
            pbMic.ForeColor = clrMicBar;
            pbMic.BackColor = Color.FromArgb(38, 38, 55);

            lblSpkIcon.AutoSize  = false;
            lblSpkIcon.Location  = new Point(340, 28);
            lblSpkIcon.Size      = new Size(26, 22);
            lblSpkIcon.Text      = "🔊";
            lblSpkIcon.Font      = new Font("Segoe UI", 10F);
            lblSpkIcon.ForeColor = clrHint;
            lblSpkIcon.BackColor = clrCard;

            pbSpk.Location  = new Point(368, 32);
            pbSpk.Size      = new Size(200, 14);
            pbSpk.Minimum   = 0;
            pbSpk.Maximum   = 1000;
            pbSpk.Value     = 0;
            pbSpk.Style     = ProgressBarStyle.Continuous;
            pbSpk.ForeColor = clrSpkBar;
            pbSpk.BackColor = Color.FromArgb(38, 38, 55);

            btnMute.Text      = "🔇  Tắt mic";
            btnMute.Location  = new Point(12, 46);
            btnMute.Size      = new Size(130, 32);
            btnMute.Font      = new Font("Segoe UI", 11F);
            btnMute.BackColor = Color.FromArgb(50, 50, 72);
            btnMute.ForeColor = clrText;
            btnMute.FlatStyle = FlatStyle.Flat;
            btnMute.FlatAppearance.BorderColor        = Color.FromArgb(70, 70, 100);
            btnMute.FlatAppearance.BorderSize         = 1;
            btnMute.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 92);
            btnMute.Cursor    = Cursors.Hand;
            btnMute.Click    += btnMute_Click;

            btnToggleCam.Text      = "📷  Tắt cam";
            btnToggleCam.Location  = new Point(150, 46);
            btnToggleCam.Size      = new Size(130, 32);
            btnToggleCam.Font      = new Font("Segoe UI", 11F);
            btnToggleCam.BackColor = Color.FromArgb(50, 50, 72);
            btnToggleCam.ForeColor = clrText;
            btnToggleCam.FlatStyle = FlatStyle.Flat;
            btnToggleCam.FlatAppearance.BorderColor        = Color.FromArgb(70, 70, 100);
            btnToggleCam.FlatAppearance.BorderSize         = 1;
            btnToggleCam.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 92);
            btnToggleCam.Cursor    = Cursors.Hand;
            btnToggleCam.Click    += btnToggleCam_Click;

            btnHangup.Text      = "📵  Cúp máy";
            btnHangup.Location  = new Point(640, 46);
            btnHangup.Size      = new Size(148, 32);
            btnHangup.Font      = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnHangup.BackColor = clrRed;
            btnHangup.ForeColor = Color.White;
            btnHangup.FlatStyle = FlatStyle.Flat;
            btnHangup.FlatAppearance.BorderSize         = 0;
            btnHangup.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 70, 80);
            btnHangup.Cursor    = Cursors.Hand;
            btnHangup.Click    += btnHangup_Click;

            pnlCtrl.Controls.AddRange(new Control[]
            {
                lblPeerName, lblTimer,
                lblMicIcon, pbMic, lblSpkIcon, pbSpk,
                btnMute, btnToggleCam, btnHangup
            });

            // ── pnlLocalOverlay: local camera preview (absolute in pnlVideoArea) ─
            pnlLocalOverlay.Size      = new Size(200, 150);
            pnlLocalOverlay.BackColor = Color.FromArgb(20, 20, 30);
            pnlLocalOverlay.BorderStyle = BorderStyle.FixedSingle;

            lblNoCam.Text      = "Không có camera";
            lblNoCam.Dock      = DockStyle.Fill;
            lblNoCam.Font      = new Font("Segoe UI", 10F, FontStyle.Italic);
            lblNoCam.ForeColor = clrHint;
            lblNoCam.BackColor = Color.FromArgb(20, 20, 30);
            lblNoCam.TextAlign = ContentAlignment.MiddleCenter;
            lblNoCam.Visible   = false;

            picLocal.Dock      = DockStyle.Fill;
            picLocal.SizeMode  = PictureBoxSizeMode.Zoom;
            picLocal.BackColor = Color.Black;

            pnlLocalOverlay.Controls.Add(picLocal);
            pnlLocalOverlay.Controls.Add(lblNoCam);

            // ── picRemote: video từ peer (Dock=Fill trong pnlVideoArea) ──
            picRemote.Dock      = DockStyle.Fill;
            picRemote.SizeMode  = PictureBoxSizeMode.Zoom;
            picRemote.BackColor = Color.Black;

            // ── pnlVideoArea: chứa picRemote + pnlLocalOverlay (overlay) ──
            pnlVideoArea.Dock      = DockStyle.Fill;
            pnlVideoArea.BackColor = Color.Black;

            // Thêm picRemote trước (z-order thấp), pnlLocalOverlay sau (hiện đè lên)
            pnlVideoArea.Controls.Add(picRemote);
            pnlVideoArea.Controls.Add(pnlLocalOverlay);

            // ── Form ────────────────────────────────────────────────────
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode       = AutoScaleMode.Font;
            this.BackColor           = clrBg;
            this.ClientSize          = new Size(800, 570);
            this.FormBorderStyle     = FormBorderStyle.Sizable;
            this.MinimumSize         = new Size(640, 460);
            this.StartPosition       = FormStartPosition.CenterParent;
            this.Text                = "Video Call";

            // Thêm theo thứ tự Dock: Bottom trước Fill, Top cuối
            this.Controls.Add(pnlVideoArea);
            this.Controls.Add(pnlCaption);
            this.Controls.Add(pnlCtrl);
            this.Controls.Add(lblStatus);

            this.FormClosing += VideoCallForm_FormClosing;

            pnlCaption.ResumeLayout(false);
            pnlVideoArea.ResumeLayout(false);
            pnlLocalOverlay.ResumeLayout(false);
            pnlCtrl.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        // ── Control fields ─────────────────────────────────────────────
        private System.Windows.Forms.Panel       pnlCaption;
        private System.Windows.Forms.RichTextBox rtbCaption;
        private System.Windows.Forms.Label       lblStatus;
        private System.Windows.Forms.Panel       pnlVideoArea;
        private System.Windows.Forms.PictureBox  picRemote;
        private System.Windows.Forms.Panel       pnlLocalOverlay;
        private System.Windows.Forms.PictureBox  picLocal;
        private System.Windows.Forms.Label       lblNoCam;
        private System.Windows.Forms.Panel       pnlCtrl;
        private System.Windows.Forms.Label       lblPeerName;
        private System.Windows.Forms.Label       lblTimer;
        private System.Windows.Forms.Label       lblMicIcon;
        private System.Windows.Forms.ProgressBar pbMic;
        private System.Windows.Forms.Label       lblSpkIcon;
        private System.Windows.Forms.ProgressBar pbSpk;
        private System.Windows.Forms.Button      btnMute;
        private System.Windows.Forms.Button      btnToggleCam;
        private System.Windows.Forms.Button      btnHangup;
    }
}
