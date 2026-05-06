#nullable disable
namespace Client_UI_App.Forms
{
    partial class VoiceCallForm
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
            // ── Controls ───────────────────────────────────────────────
            pnlSubtitle = new Panel();
            rtbSubtitle = new RichTextBox();

            pnlMain     = new Panel();
            pnlAvatar   = new Panel();
            picAvatar   = new PictureBox();
            lblPeerName = new Label();
            lblStatus   = new Label();
            lblTimer    = new Label();

            lblMicIcon  = new Label();
            pbMic       = new ProgressBar();
            lblSpkIcon  = new Label();
            pbSpk       = new ProgressBar();

            pnlButtons  = new Panel();
            btnMute     = new Button();
            btnHangup   = new Button();

            pnlSubtitle.SuspendLayout();
            pnlMain.SuspendLayout();
            pnlAvatar.SuspendLayout();
            pnlButtons.SuspendLayout();
            this.SuspendLayout();

            // ── Bảng màu ──────────────────────────────────────────────
            var clrBg       = Color.FromArgb(18,  18,  28);
            var clrCard     = Color.FromArgb(28,  28,  42);
            var clrTextMain = Color.FromArgb(220, 220, 235);
            var clrTextHint = Color.FromArgb(130, 130, 158);
            var clrGreen    = Color.FromArgb(0,   180, 110);
            var clrRed      = Color.FromArgb(210,  50,  60);
            var clrBlue     = Color.FromArgb(60,  130, 220);
            var clrMicBar   = Color.FromArgb(80,  200, 140);
            var clrSpkBar   = Color.FromArgb(80,  150, 230);

            // ── Subtitle panel (bot call only, Dock=Bottom) ───────────
            pnlSubtitle.Dock      = DockStyle.Bottom;
            pnlSubtitle.Height    = 160;
            pnlSubtitle.BackColor = Color.FromArgb(16, 16, 26);
            pnlSubtitle.Visible   = false;

            rtbSubtitle.Dock        = DockStyle.Fill;
            rtbSubtitle.ReadOnly    = true;
            rtbSubtitle.BackColor   = Color.FromArgb(16, 16, 26);
            rtbSubtitle.ForeColor   = Color.FromArgb(200, 200, 220);
            rtbSubtitle.Font        = new Font("Segoe UI", 15F);
            rtbSubtitle.BorderStyle = BorderStyle.None;
            rtbSubtitle.ScrollBars  = RichTextBoxScrollBars.Vertical;
            rtbSubtitle.Padding     = new Padding(6);
            pnlSubtitle.Controls.Add(rtbSubtitle);

            // ── pnlMain ───────────────────────────────────────────────
            pnlMain.Dock      = DockStyle.Fill;
            pnlMain.BackColor = clrCard;
            pnlMain.Padding   = new Padding(28, 16, 28, 12);

            // ── Avatar peer ───────────────────────────────────────────
            pnlAvatar.Dock      = DockStyle.Top;
            pnlAvatar.Height    = 100;
            pnlAvatar.BackColor = clrCard;

            picAvatar.Size      = new Size(80, 80);
            picAvatar.Location  = new Point(92, 10);  // (264-80)/2 = 92 centered
            picAvatar.SizeMode  = PictureBoxSizeMode.StretchImage;
            picAvatar.BackColor = Color.FromArgb(50, 50, 72);
            pnlAvatar.Controls.Add(picAvatar);

            // ── Tên peer ──────────────────────────────────────────────
            lblPeerName.AutoSize  = false;
            lblPeerName.Dock      = DockStyle.Top;
            lblPeerName.Height    = 46;
            lblPeerName.Font      = new Font("Segoe UI", 20F, FontStyle.Bold);
            lblPeerName.ForeColor = clrTextMain;
            lblPeerName.BackColor = clrCard;
            lblPeerName.TextAlign = ContentAlignment.MiddleCenter;

            // ── Trạng thái ────────────────────────────────────────────
            lblStatus.AutoSize  = false;
            lblStatus.Dock      = DockStyle.Top;
            lblStatus.Height    = 30;
            lblStatus.Font      = new Font("Segoe UI", 13F, FontStyle.Italic);
            lblStatus.ForeColor = clrTextHint;
            lblStatus.BackColor = clrCard;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;

            // ── Đồng hồ ───────────────────────────────────────────────
            lblTimer.AutoSize  = false;
            lblTimer.Dock      = DockStyle.Top;
            lblTimer.Height    = 50;
            lblTimer.Font      = new Font("Consolas", 28F, FontStyle.Bold);
            lblTimer.ForeColor = clrGreen;
            lblTimer.BackColor = clrCard;
            lblTimer.TextAlign = ContentAlignment.MiddleCenter;
            lblTimer.Text      = "00:00";

            // ── Khoảng cách ───────────────────────────────────────────
            var spacer1 = new Label { Dock = DockStyle.Top, Height = 18, BackColor = clrCard };

            // ── Mic level ─────────────────────────────────────────────
            lblMicIcon.AutoSize  = false;
            lblMicIcon.Dock      = DockStyle.Top;
            lblMicIcon.Height    = 30;
            lblMicIcon.Text      = "🎤  Microphone";
            lblMicIcon.Font      = new Font("Segoe UI", 13F);
            lblMicIcon.ForeColor = clrTextHint;
            lblMicIcon.BackColor = clrCard;
            lblMicIcon.TextAlign = ContentAlignment.MiddleLeft;

            pbMic.Dock      = DockStyle.Top;
            pbMic.Height    = 14;
            pbMic.Minimum   = 0;
            pbMic.Maximum   = 1000;
            pbMic.Value     = 0;
            pbMic.Style     = ProgressBarStyle.Continuous;
            pbMic.ForeColor = clrMicBar;
            pbMic.BackColor = Color.FromArgb(38, 38, 55);

            var spacer2 = new Label { Dock = DockStyle.Top, Height = 12, BackColor = clrCard };

            // ── Speaker level ─────────────────────────────────────────
            lblSpkIcon.AutoSize  = false;
            lblSpkIcon.Dock      = DockStyle.Top;
            lblSpkIcon.Height    = 30;
            lblSpkIcon.Text      = "🔊  Loa";
            lblSpkIcon.Font      = new Font("Segoe UI", 13F);
            lblSpkIcon.ForeColor = clrTextHint;
            lblSpkIcon.BackColor = clrCard;
            lblSpkIcon.TextAlign = ContentAlignment.MiddleLeft;

            pbSpk.Dock      = DockStyle.Top;
            pbSpk.Height    = 14;
            pbSpk.Minimum   = 0;
            pbSpk.Maximum   = 1000;
            pbSpk.Value     = 0;
            pbSpk.Style     = ProgressBarStyle.Continuous;
            pbSpk.ForeColor = clrSpkBar;
            pbSpk.BackColor = Color.FromArgb(38, 38, 55);

            var spacer3 = new Label { Dock = DockStyle.Top, Height = 20, BackColor = clrCard };

            // ── Hàng nút ──────────────────────────────────────────────
            pnlButtons.Dock      = DockStyle.Top;
            pnlButtons.Height    = 66;
            pnlButtons.BackColor = clrCard;

            btnMute.Text      = "🔇  Tắt mic";
            btnMute.Font      = new Font("Segoe UI", 13F);
            btnMute.Size      = new Size(120, 54);
            btnMute.Location  = new Point(0, 6);
            btnMute.BackColor = Color.FromArgb(50, 50, 72);
            btnMute.ForeColor = clrTextMain;
            btnMute.FlatStyle = FlatStyle.Flat;
            btnMute.FlatAppearance.BorderColor     = Color.FromArgb(70, 70, 100);
            btnMute.FlatAppearance.BorderSize      = 1;
            btnMute.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 92);
            btnMute.Cursor    = Cursors.Hand;
            btnMute.Click    += btnMute_Click;

            btnHangup.Text      = "📵  Cúp máy";
            btnHangup.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
            btnHangup.Size      = new Size(136, 54);
            btnHangup.Location  = new Point(128, 6);
            btnHangup.BackColor = clrRed;
            btnHangup.ForeColor = Color.White;
            btnHangup.FlatStyle = FlatStyle.Flat;
            btnHangup.FlatAppearance.BorderSize         = 0;
            btnHangup.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 70, 80);
            btnHangup.Cursor    = Cursors.Hand;
            btnHangup.Click    += btnHangup_Click;

            pnlButtons.Controls.Add(btnHangup);
            pnlButtons.Controls.Add(btnMute);

            // ── Thêm vào pnlMain (LIFO Dock=Top — thêm sau = hiện trên) ─
            pnlMain.Controls.Add(pnlButtons);
            pnlMain.Controls.Add(spacer3);
            pnlMain.Controls.Add(pbSpk);
            pnlMain.Controls.Add(lblSpkIcon);
            pnlMain.Controls.Add(spacer2);
            pnlMain.Controls.Add(pbMic);
            pnlMain.Controls.Add(lblMicIcon);
            pnlMain.Controls.Add(spacer1);
            pnlMain.Controls.Add(lblTimer);
            pnlMain.Controls.Add(lblStatus);
            pnlMain.Controls.Add(lblPeerName);
            pnlMain.Controls.Add(pnlAvatar);  // top

            // ── Form ──────────────────────────────────────────────────
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode       = AutoScaleMode.Font;
            this.BackColor           = clrBg;
            this.ClientSize          = new Size(320, 480);
            this.FormBorderStyle     = FormBorderStyle.FixedDialog;
            this.MaximizeBox         = false;
            this.MinimizeBox         = false;
            this.StartPosition       = FormStartPosition.CenterParent;
            this.Text                = "Voice Call";

            // Subtitle panel (Dock=Bottom) thêm trước pnlMain để layout đúng
            this.Controls.Add(pnlMain);
            this.Controls.Add(pnlSubtitle);

            this.FormClosing += VoiceCallForm_FormClosing;

            pnlSubtitle.ResumeLayout(false);
            pnlMain.ResumeLayout(false);
            pnlAvatar.ResumeLayout(false);
            pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        // ── Control fields ─────────────────────────────────────────────
        private Panel       pnlSubtitle;
        private RichTextBox rtbSubtitle;
        private Panel       pnlMain;
        private Panel       pnlAvatar;
        private PictureBox  picAvatar;
        private Label       lblPeerName;
        private Label       lblStatus;
        private Label       lblTimer;
        private Label       lblMicIcon;
        private ProgressBar pbMic;
        private Label       lblSpkIcon;
        private ProgressBar pbSpk;
        private Panel       pnlButtons;
        private Button      btnMute;
        private Button      btnHangup;
    }
}
