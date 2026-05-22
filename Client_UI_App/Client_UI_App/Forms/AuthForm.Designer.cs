#nullable disable
namespace Client_UI_App.Forms
{
    partial class AuthForm
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
            lblTitle          = new Label();
            lblUsername       = new Label();
            txtUsername       = new TextBox();
            lblEmail          = new Label();
            txtEmail          = new TextBox();
            lblPassword       = new Label();
            txtPassword       = new TextBox();
            btnPrimary        = new Button();
            lnkForgotPassword = new LinkLabel();
            lnkToggleMode     = new LinkLabel();
            lblStatus         = new Label();
            pnlMain           = new Panel();

            pnlMain.SuspendLayout();
            this.SuspendLayout();

            // ── Bảng màu Dark Theme ───────────────────────────────────
            var clrBgForm   = Color.FromArgb(18,  18,  24);
            var clrBgPanel  = Color.FromArgb(28,  28,  38);
            var clrBgInput  = Color.FromArgb(42,  42,  56);
            var clrTextMain = Color.FromArgb(220, 220, 230);
            var clrTextHint = Color.FromArgb(140, 140, 160);
            var clrAccPink  = Color.FromArgb(220,  60, 120);
            var clrAccBlue  = Color.FromArgb(0,   120, 212);
            var clrLink     = Color.FromArgb(100, 160, 240);

            // ── pnlMain ───────────────────────────────────────────────
            pnlMain.BackColor   = clrBgPanel;
            pnlMain.BorderStyle = BorderStyle.None;
            pnlMain.Location    = new Point(30, 20);
            pnlMain.Size        = new Size(360, 375);

            // ── lblTitle ──────────────────────────────────────────────
            lblTitle.Text      = "Uiti-chan Chat";
            lblTitle.Font      = new Font("Segoe UI", 20F, FontStyle.Bold);
            lblTitle.ForeColor = clrAccPink;
            lblTitle.BackColor = clrBgPanel;
            lblTitle.Location  = new Point(0, 10);
            lblTitle.Size      = new Size(360, 50);
            lblTitle.TextAlign = ContentAlignment.MiddleCenter;

            // ── lblUsername ───────────────────────────────────────────
            lblUsername.Text      = "Tên đăng nhập";
            lblUsername.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblUsername.ForeColor = clrTextHint;
            lblUsername.BackColor = clrBgPanel;
            lblUsername.Location  = new Point(10, 80);
            lblUsername.Size      = new Size(340, 18);

            // ── txtUsername ───────────────────────────────────────────
            txtUsername.Font        = new Font("Segoe UI", 11F);
            txtUsername.Location    = new Point(10, 100);
            txtUsername.Size        = new Size(340, 28);
            txtUsername.BackColor   = clrBgInput;
            txtUsername.ForeColor   = clrTextMain;
            txtUsername.BorderStyle = BorderStyle.FixedSingle;

            // ── lblEmail (signup only, hidden by default) ─────────────
            lblEmail.Text      = "Gmail";
            lblEmail.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblEmail.ForeColor = clrTextHint;
            lblEmail.BackColor = clrBgPanel;
            lblEmail.Location  = new Point(10, 145);
            lblEmail.Size      = new Size(340, 18);
            lblEmail.Visible   = false;

            // ── txtEmail (signup only, hidden by default) ─────────────
            txtEmail.Font        = new Font("Segoe UI", 11F);
            txtEmail.Location    = new Point(10, 165);
            txtEmail.Size        = new Size(340, 28);
            txtEmail.BackColor   = clrBgInput;
            txtEmail.ForeColor   = clrTextMain;
            txtEmail.BorderStyle = BorderStyle.FixedSingle;
            txtEmail.Visible     = false;

            // ── lblPassword ───────────────────────────────────────────
            lblPassword.Text      = "Mật khẩu";
            lblPassword.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblPassword.ForeColor = clrTextHint;
            lblPassword.BackColor = clrBgPanel;
            lblPassword.Location  = new Point(10, 145);
            lblPassword.Size      = new Size(340, 18);

            // ── txtPassword ───────────────────────────────────────────
            txtPassword.Font         = new Font("Segoe UI", 11F);
            txtPassword.Location     = new Point(10, 165);
            txtPassword.Size         = new Size(340, 28);
            txtPassword.BackColor    = clrBgInput;
            txtPassword.ForeColor    = clrTextMain;
            txtPassword.PasswordChar = '●';
            txtPassword.BorderStyle  = BorderStyle.FixedSingle;
            txtPassword.KeyDown     += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) btnPrimary.PerformClick();
            };

            // ── btnPrimary ────────────────────────────────────────────
            btnPrimary.Text      = "Đăng nhập";
            btnPrimary.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnPrimary.Location  = new Point(10, 215);
            btnPrimary.Size      = new Size(340, 38);
            btnPrimary.BackColor = clrAccBlue;
            btnPrimary.ForeColor = Color.White;
            btnPrimary.FlatStyle = FlatStyle.Flat;
            btnPrimary.FlatAppearance.BorderSize = 0;
            btnPrimary.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 140, 230);
            btnPrimary.Cursor   = Cursors.Hand;
            btnPrimary.Click   += btnPrimary_Click;

            // ── lnkForgotPassword (login mode only) ───────────────────
            lnkForgotPassword.Text      = "Quên mật khẩu?";
            lnkForgotPassword.Font      = new Font("Segoe UI", 9F);
            lnkForgotPassword.ForeColor = clrLink;
            lnkForgotPassword.BackColor = clrBgPanel;
            lnkForgotPassword.Location  = new Point(10, 265);
            lnkForgotPassword.Size      = new Size(340, 20);
            lnkForgotPassword.TextAlign = ContentAlignment.MiddleCenter;
            lnkForgotPassword.LinkColor = clrLink;
            lnkForgotPassword.ActiveLinkColor  = Color.White;
            lnkForgotPassword.VisitedLinkColor = clrLink;
            lnkForgotPassword.LinkBehavior     = LinkBehavior.NeverUnderline;
            lnkForgotPassword.Cursor           = Cursors.Hand;
            lnkForgotPassword.LinkClicked      += lnkForgotPassword_LinkClicked;

            // ── lnkToggleMode ─────────────────────────────────────────
            lnkToggleMode.Text      = "Chưa có tài khoản? Đăng ký ngay";
            lnkToggleMode.Font      = new Font("Segoe UI", 9F);
            lnkToggleMode.ForeColor = clrLink;
            lnkToggleMode.BackColor = clrBgPanel;
            lnkToggleMode.Location  = new Point(10, 293);
            lnkToggleMode.Size      = new Size(340, 20);
            lnkToggleMode.TextAlign = ContentAlignment.MiddleCenter;
            lnkToggleMode.LinkColor = clrLink;
            lnkToggleMode.ActiveLinkColor  = Color.White;
            lnkToggleMode.VisitedLinkColor = clrLink;
            lnkToggleMode.LinkBehavior     = LinkBehavior.NeverUnderline;
            lnkToggleMode.Cursor           = Cursors.Hand;
            lnkToggleMode.LinkClicked      += lnkToggleMode_LinkClicked;

            // ── lblStatus ─────────────────────────────────────────────
            lblStatus.Text      = "Chờ đăng nhập...";
            lblStatus.Font      = new Font("Segoe UI", 9F);
            lblStatus.ForeColor = clrTextHint;
            lblStatus.BackColor = clrBgPanel;
            lblStatus.Location  = new Point(10, 320);
            lblStatus.Size      = new Size(340, 40);
            lblStatus.TextAlign = ContentAlignment.TopCenter;

            // ── pnlMain: thêm controls ────────────────────────────────
            pnlMain.Controls.Add(lblTitle);
            pnlMain.Controls.Add(lblUsername);
            pnlMain.Controls.Add(txtUsername);
            pnlMain.Controls.Add(lblEmail);
            pnlMain.Controls.Add(txtEmail);
            pnlMain.Controls.Add(lblPassword);
            pnlMain.Controls.Add(txtPassword);
            pnlMain.Controls.Add(btnPrimary);
            pnlMain.Controls.Add(lnkForgotPassword);
            pnlMain.Controls.Add(lnkToggleMode);
            pnlMain.Controls.Add(lblStatus);

            // ── Form ──────────────────────────────────────────────────
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode       = AutoScaleMode.None;
            this.BackColor           = clrBgForm;
            this.ClientSize          = new Size(420, 415);
            this.Controls.Add(pnlMain);
            this.FormBorderStyle     = FormBorderStyle.FixedSingle;
            this.MaximizeBox         = false;
            this.StartPosition       = FormStartPosition.CenterScreen;
            this.Text                = "Uiti-chan Chat  –  Đăng nhập / Đăng ký";

            pnlMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // ── Control fields ───────────────────────────────────────────
        private Panel     pnlMain;
        private Label     lblTitle;
        private Label     lblUsername;
        private TextBox   txtUsername;
        private Label     lblEmail;
        private TextBox   txtEmail;
        private Label     lblPassword;
        private TextBox   txtPassword;
        private Button    btnPrimary;
        private LinkLabel lnkForgotPassword;
        private LinkLabel lnkToggleMode;
        private Label     lblStatus;
    }
}
