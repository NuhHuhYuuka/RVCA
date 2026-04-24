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
            lblTitle    = new Label();
            lblUsername = new Label();
            txtUsername = new TextBox();
            lblPassword = new Label();
            txtPassword = new TextBox();
            btnLogin    = new Button();
            btnSignup   = new Button();
            lblStatus   = new Label();
            pnlMain     = new Panel();

            pnlMain.SuspendLayout();
            this.SuspendLayout();

            // ── Bảng màu Dark Theme ────────────────────────────────────
            var clrBgForm    = Color.FromArgb(18,  18,  24);   // nền form
            var clrBgPanel   = Color.FromArgb(28,  28,  38);   // panel trung tâm
            var clrBgInput   = Color.FromArgb(42,  42,  56);   // ô nhập
            var clrTextMain  = Color.FromArgb(220, 220, 230);  // chữ chính
            var clrTextHint  = Color.FromArgb(140, 140, 160);  // chữ gợi ý
            var clrAccPink   = Color.FromArgb(220,  60, 120);  // tiêu đề hồng
            var clrAccBlue   = Color.FromArgb(0,   120, 212);  // nút đăng nhập
            var clrAccPurple = Color.FromArgb(138,  43, 226);  // nút đăng ký

            // ── pnlMain ────────────────────────────────────────────────
            pnlMain.BackColor   = clrBgPanel;
            pnlMain.BorderStyle = BorderStyle.None;
            pnlMain.Location    = new Point(30, 20);
            pnlMain.Size        = new Size(360, 320);

            // ── lblTitle ───────────────────────────────────────────────
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
                if (e.KeyCode == Keys.Enter) btnLogin.PerformClick();
            };

            // ── btnLogin ──────────────────────────────────────────────
            btnLogin.Text      = "Đăng nhập";
            btnLogin.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnLogin.Location  = new Point(10, 215);
            btnLogin.Size      = new Size(160, 38);
            btnLogin.BackColor = clrAccBlue;
            btnLogin.ForeColor = Color.White;
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize  = 0;
            btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 140, 230);
            btnLogin.Cursor    = Cursors.Hand;
            btnLogin.Click    += btnLogin_Click;

            // ── btnSignup ─────────────────────────────────────────────
            btnSignup.Text      = "Đăng ký";
            btnSignup.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSignup.Location  = new Point(190, 215);
            btnSignup.Size      = new Size(160, 38);
            btnSignup.BackColor = clrAccPurple;
            btnSignup.ForeColor = Color.White;
            btnSignup.FlatStyle = FlatStyle.Flat;
            btnSignup.FlatAppearance.BorderSize  = 0;
            btnSignup.FlatAppearance.MouseOverBackColor = Color.FromArgb(160, 70, 240);
            btnSignup.Cursor    = Cursors.Hand;
            btnSignup.Click    += btnSignup_Click;

            // ── lblStatus ─────────────────────────────────────────────
            lblStatus.Text      = "Chờ đăng nhập...";
            lblStatus.Font      = new Font("Segoe UI", 9F);
            lblStatus.ForeColor = clrTextHint;
            lblStatus.BackColor = clrBgPanel;
            lblStatus.Location  = new Point(10, 268);
            lblStatus.Size      = new Size(340, 40);
            lblStatus.TextAlign = ContentAlignment.TopCenter;

            // ── pnlMain: thêm controls ────────────────────────────────
            pnlMain.Controls.Add(lblTitle);
            pnlMain.Controls.Add(lblUsername);
            pnlMain.Controls.Add(txtUsername);
            pnlMain.Controls.Add(lblPassword);
            pnlMain.Controls.Add(txtPassword);
            pnlMain.Controls.Add(btnLogin);
            pnlMain.Controls.Add(btnSignup);
            pnlMain.Controls.Add(lblStatus);

            // ── Form ──────────────────────────────────────────────────
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode       = AutoScaleMode.Font;
            this.BackColor           = clrBgForm;
            this.ClientSize          = new Size(420, 360);
            this.Controls.Add(pnlMain);
            this.FormBorderStyle     = FormBorderStyle.FixedSingle;
            this.MaximizeBox         = false;
            this.StartPosition       = FormStartPosition.CenterScreen;
            this.Text                = "Uiti-chan Chat  –  Đăng nhập / Đăng ký";

            pnlMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        // ── Control fields ────────────────────────────────────────────
        private Panel   pnlMain;
        private Label   lblTitle;
        private Label   lblUsername;
        private TextBox txtUsername;
        private Label   lblPassword;
        private TextBox txtPassword;
        private Button  btnLogin;
        private Button  btnSignup;
        private Label   lblStatus;
    }
}
