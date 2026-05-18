using Client_UI_App.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Client_UI_App.Forms
{
    public partial class AuthForm : Form
    {
        public AuthForm()
        {
            InitializeComponent();
        }

        // ── Đăng nhập ──
        private async void btnLogin_Click(object sender, EventArgs e)
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
                // Bước 1: Xin vé
                int dirPort = await DirectoryService.GetDirectoryPortAsync();
                SetStatus($"Load Balancer → Directory Server port {dirPort}. Đang xác thực...", Color.DodgerBlue);

                // Khởi động P2P Listener trước khi đăng nhập để có port thật
                int myPort = P2PListenerService.Start(username);
                SetStatus($"P2P Listener đang chạy trên port {myPort}. Đang xác thực...", Color.DodgerBlue);

                // Bước 2: Đăng nhập (gửi port thật lên Directory Server)
                var (success, message, onlineUsers) = await DirectoryService.LoginAsync(
                    dirPort, username, password, myPort);

                if (success)
                {
                    SetStatus("Đăng nhập thành công! Đang mở cửa sổ chat...", Color.SeaGreen);

                    var mainForm = new MainChatForm(username, dirPort, onlineUsers);

                    // Khi MainChatForm đóng → dừng P2P listener, hiện lại AuthForm
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
                    // Đăng nhập thất bại → dừng listener
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

        // ── Đăng ký ──
        private async void btnSignup_Click(object sender, EventArgs e)
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
                SetStatus($"Đang đăng ký tài khoản trên Directory Server port {dirPort}...", Color.DodgerBlue);

                var (success, message) = await DirectoryService.SignupAsync(dirPort, username, password);

                if (success)
                    SetStatus($"✔  {message}  →  Bạn có thể đăng nhập.", Color.SeaGreen);
                else
                    SetStatus($"✘  {message}", Color.Crimson);
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

        // ── Helpers ──
        private void SetStatus(string text, Color color)
        {
            lblStatus.ForeColor = color;
            lblStatus.Text      = text;
        }

        private void SetBusy(bool busy)
        {
            btnLogin.Enabled  = !busy;
            btnSignup.Enabled = !busy;
            this.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }
    }
}
