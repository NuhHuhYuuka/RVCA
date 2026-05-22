using Client_UI_App.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Client_UI_App.Forms
{
    public partial class AuthForm : Form
    {
        private bool _isSignupMode = false;

        private static readonly Color _clrTextHint  = Color.FromArgb(140, 140, 160);
        private static readonly Color _clrAccBlue   = Color.FromArgb(0,   120, 212);
        private static readonly Color _clrAccPurple = Color.FromArgb(138,  43, 226);

        public AuthForm()
        {
            InitializeComponent();
        }

        // ── Mode switching ─────────────────────────────────────────────
        private void SwitchToLoginMode()
        {
            _isSignupMode = false;
            pnlMain.SuspendLayout();
            this.SuspendLayout();

            lblEmail.Visible = false;
            txtEmail.Visible = false;
            txtEmail.Text    = "";

            lblPassword.Location = new Point(10, 145);
            txtPassword.Location = new Point(10, 165);
            btnPrimary.Location  = new Point(10, 215);

            btnPrimary.Text      = "Đăng nhập";
            btnPrimary.BackColor = _clrAccBlue;
            btnPrimary.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 140, 230);

            lnkForgotPassword.Visible  = true;
            lnkForgotPassword.Location = new Point(10, 265);
            lnkToggleMode.Location     = new Point(10, 293);
            lnkToggleMode.Text         = "Chưa có tài khoản? Đăng ký ngay";
            lblStatus.Location         = new Point(10, 320);

            pnlMain.Size    = new Size(360, 375);
            this.ClientSize = new Size(420, 415);

            pnlMain.ResumeLayout(true);
            this.ResumeLayout(true);
            SetStatus("Chờ đăng nhập...", _clrTextHint);
        }

        private void SwitchToSignupMode()
        {
            _isSignupMode = true;
            pnlMain.SuspendLayout();
            this.SuspendLayout();

            lblEmail.Visible = true;
            txtEmail.Visible = true;

            lblPassword.Location = new Point(10, 210);
            txtPassword.Location = new Point(10, 230);
            btnPrimary.Location  = new Point(10, 280);

            btnPrimary.Text      = "Đăng ký";
            btnPrimary.BackColor = _clrAccPurple;
            btnPrimary.FlatAppearance.MouseOverBackColor = Color.FromArgb(160, 70, 240);

            lnkForgotPassword.Visible = false;
            lnkToggleMode.Location    = new Point(10, 330);
            lnkToggleMode.Text        = "Đã có tài khoản? Đăng nhập";
            lblStatus.Location        = new Point(10, 358);

            pnlMain.Size    = new Size(360, 400);
            this.ClientSize = new Size(420, 440);

            pnlMain.ResumeLayout(true);
            this.ResumeLayout(true);
            SetStatus("", _clrTextHint);
        }

        // ── Primary button: login or signup ────────────────────────────
        private async void btnPrimary_Click(object sender, EventArgs e)
        {
            if (_isSignupMode)
                await DoSignupAsync();
            else
                await DoLoginAsync();
        }

        private async System.Threading.Tasks.Task DoLoginAsync()
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetStatus("Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.", Color.OrangeRed);
                return;
            }

            SetBusy(true);
            SetStatus("Đang kết nối Load Balancer (port 9000)...", Color.DodgerBlue);

            try
            {
                int dirPort = await DirectoryService.GetDirectoryPortAsync();
                SetStatus($"Load Balancer → Directory Server port {dirPort}. Đang xác thực...", Color.DodgerBlue);

                int myPort = P2PListenerService.Start(username);
                SetStatus($"P2P Listener đang chạy trên port {myPort}. Đang xác thực...", Color.DodgerBlue);

                var (success, message, onlineUsers) = await DirectoryService.LoginAsync(
                    dirPort, username, password, myPort);

                if (success)
                {
                    SetStatus("Đăng nhập thành công! Đang mở cửa sổ chat...", Color.SeaGreen);

                    var mainForm = new MainChatForm(username, dirPort, onlineUsers);
                    mainForm.FormClosed += (_, _) =>
                    {
                        P2PListenerService.Stop();
                        this.Show();
                        SetBusy(false);
                        SetStatus("Đã đăng xuất. Chờ đăng nhập...", Color.Gray);
                    };

                    this.Hide();
                    mainForm.Show();
                }
                else
                {
                    P2PListenerService.Stop();
                    SetStatus($"✘  {message}", Color.Crimson);
                    SetBusy(false);
                }
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.InnerException?.Message
                          ?? ex.InnerException?.Message
                          ?? ex.Message;
                SetStatus($"Lỗi: {inner}", Color.Crimson);
                SetBusy(false);
            }
        }

        private async System.Threading.Tasks.Task DoSignupAsync()
        {
            string username = txtUsername.Text.Trim();
            string email    = txtEmail.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                SetStatus("Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.", Color.OrangeRed);
                return;
            }
            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            {
                SetStatus("Vui lòng nhập Gmail hợp lệ.", Color.OrangeRed);
                return;
            }

            SetBusy(true);
            SetStatus("Đang kết nối Load Balancer (port 9000)...", Color.DodgerBlue);

            try
            {
                int dirPort = await DirectoryService.GetDirectoryPortAsync();
                SetStatus($"Đang đăng ký tài khoản trên Directory Server port {dirPort}...", Color.DodgerBlue);

                var (success, message) = await DirectoryService.SignupAsync(dirPort, username, password, email);

                if (success)
                {
                    SwitchToLoginMode();
                    txtUsername.Text = username;
                    SetStatus($"✔  {message}  →  Bạn có thể đăng nhập.", Color.SeaGreen);
                }
                else
                {
                    SetStatus($"✘  {message}", Color.Crimson);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi kết nối: {ex.Message}", Color.Crimson);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ── Quên mật khẩu ─────────────────────────────────────────────
        private async void lnkForgotPassword_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string? email = AskInput("Quên mật khẩu", "Nhập địa chỉ Gmail đã đăng ký:");
            if (string.IsNullOrWhiteSpace(email)) return;

            SetBusy(true);
            SetStatus("Đang gửi OTP tới email...", Color.DodgerBlue);

            try
            {
                var (ok, msg) = await DirectoryService.ForgotPasswordAsync(email);
                if (!ok)
                {
                    SetStatus($"✘  {msg}", Color.Crimson);
                    SetBusy(false);
                    return;
                }

                SetStatus("Đã gửi OTP. Kiểm tra hộp thư Gmail của bạn.", Color.SeaGreen);

                string? otp = AskInput("Nhập mã OTP", "Nhập mã OTP 6 chữ số từ email:");
                if (string.IsNullOrWhiteSpace(otp))
                {
                    SetBusy(false);
                    SetStatus("Chờ đăng nhập...", Color.Gray);
                    return;
                }

                string? newPwd = AskInput("Mật khẩu mới", "Nhập mật khẩu mới:", password: true);
                if (string.IsNullOrWhiteSpace(newPwd))
                {
                    SetBusy(false);
                    SetStatus("Chờ đăng nhập...", Color.Gray);
                    return;
                }

                var (ok2, msg2) = await DirectoryService.ResetPasswordAsync(email, otp, newPwd);
                SetStatus(ok2 ? "✔  Đặt lại mật khẩu thành công! Đăng nhập lại." : $"✘  {msg2}",
                          ok2 ? Color.SeaGreen : Color.Crimson);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi: {ex.Message}", Color.Crimson);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ── Toggle login / signup mode ─────────────────────────────────
        private void lnkToggleMode_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_isSignupMode)
                SwitchToLoginMode();
            else
                SwitchToSignupMode();
        }

        // ── Helpers ───────────────────────────────────────────────────
        private void SetStatus(string text, Color color)
        {
            lblStatus.ForeColor = color;
            lblStatus.Text      = text;
        }

        private void SetBusy(bool busy)
        {
            btnPrimary.Enabled = !busy;
            this.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        private string? AskInput(string title, string prompt, bool password = false)
        {
            using var dlg = new Form
            {
                Text            = title,
                Size            = new Size(380, 170),
                StartPosition   = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                BackColor       = Color.FromArgb(28, 28, 38),
                ForeColor       = Color.FromArgb(220, 220, 230),
                MaximizeBox     = false,
                MinimizeBox     = false
            };

            var lbl = new Label
            {
                Text      = prompt,
                Location  = new Point(12, 15),
                Size      = new Size(338, 24),
                ForeColor = Color.FromArgb(220, 220, 230),
                BackColor = Color.Transparent
            };
            var txt = new TextBox
            {
                Location    = new Point(12, 44),
                Size        = new Size(338, 28),
                Font        = new Font("Segoe UI", 11F),
                BackColor   = Color.FromArgb(42, 42, 56),
                ForeColor   = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle
            };
            if (password) txt.PasswordChar = '●';

            var btnOk = new Button
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Location     = new Point(168, 85),
                Size         = new Size(88, 30),
                FlatStyle    = FlatStyle.Flat,
                BackColor    = Color.FromArgb(0, 120, 212),
                ForeColor    = Color.White,
                Cursor       = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text         = "Hủy",
                DialogResult = DialogResult.Cancel,
                Location     = new Point(268, 85),
                Size         = new Size(88, 30),
                FlatStyle    = FlatStyle.Flat,
                BackColor    = Color.FromArgb(80, 80, 100),
                ForeColor    = Color.White,
                Cursor       = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;

            return dlg.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
        }
    }
}
