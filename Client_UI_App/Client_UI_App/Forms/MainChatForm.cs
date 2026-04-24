using Client_UI_App.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client_UI_App.Forms
{
    public partial class MainChatForm : Form
    {
        private readonly string _username;
        private readonly int    _dirPort;

        private string _peerIp   = "";
        private int    _peerPort = 0;
        private bool   _p2pReady = false;
        private string _peerName = "";
        private bool   _isBotPeer = false;

        public MainChatForm(string username, int dirPort, List<string> onlineUsers)
        {
            _username = username;
            _dirPort  = dirPort;

            InitializeComponent();

            foreach (string u in onlineUsers)
                listBoxUsers.Items.Add(u);

            P2PListenerService.MessageReceived += OnIncomingMessage;
            P2PListenerService.FileReceived    += OnIncomingFile;
        }

        private void MainChatForm_Load(object sender, EventArgs e)
        {
            this.Text = $"Uiti-chan Chat  –  {_username}  (port {P2PListenerService.ListeningPort})";
            txtMessage.Focus();
        }

        // ── Nhận tin nhắn đến từ client khác ──────────────────────────
        private void OnIncomingMessage(string sender, string message)
        {
            AppendChat($"{sender}: {message}", Color.FromArgb(30, 180, 100));
        }

        // ── Nhận file đến từ client khác ──────────────────────────────
        private void OnIncomingFile(string sender, string fileName, string savePath)
        {
            AppendChat(
                $"📁  {sender} gửi \"{fileName}\"  →  {savePath}",
                Color.FromArgb(255, 180, 50));
        }

        // ── Click user → tự động kết nối P2P ─────────────────────────
        private async void listBoxUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxUsers.SelectedItem is not string selected) return;

            // Reset trạng thái trong khi đang tra cứu
            _p2pReady = false;
            SetPeerInfo($"Đang kết nối tới {selected}...", clrConnecting);
            SetStatus($"Đang tra cứu {selected}...", Color.DodgerBlue);

            try
            {
                if (selected == "UitiChan")
                {
                    _peerIp    = "127.0.0.1";
                    _peerPort  = 5555;
                }
                else
                {
                    int dirPort = await DirectoryService.GetDirectoryPortAsync();
                    var (found, ipPort) = await DirectoryService.GetUserAsync(dirPort, selected);

                    if (!found)
                    {
                        SetPeerInfo($"{selected} không online", clrDisconnected);
                        SetStatus($"{selected} không tìm thấy hoặc đã offline.", Color.OrangeRed);
                        return;
                    }

                    string[] parts = ipPort.Split(':');
                    _peerIp   = parts[0];
                    _peerPort = int.TryParse(parts.Length > 1 ? parts[1] : "0", out int p) ? p : 0;

                    if (_peerPort == 0)
                    {
                        SetPeerInfo($"Port không hợp lệ", clrDisconnected);
                        SetStatus("Port không hợp lệ.", Color.OrangeRed);
                        return;
                    }
                }

                _peerName  = selected;
                _isBotPeer = selected == "UitiChan";
                _p2pReady  = true;

                string modeLabel = _isBotPeer ? "Bot AI" : $"{_peerIp}:{_peerPort}";
                SetPeerInfo($"● {selected}  ({modeLabel})", clrConnected);
                SetStatus($"Sẵn sàng chat với {selected}", Color.SeaGreen);
                txtMessage.Focus();
            }
            catch (Exception ex)
            {
                SetPeerInfo("Lỗi kết nối", clrDisconnected);
                SetStatus($"Lỗi: {ex.Message}", Color.Crimson);
            }
        }

        // ── Làm mới danh sách user online ────────────────────────────
        private async void btnRefreshUsers_Click(object sender, EventArgs e)
        {
            try
            {
                int dirPort = await DirectoryService.GetDirectoryPortAsync();
                var users   = await DirectoryService.GetOnlineUsersAsync(dirPort);

                listBoxUsers.Items.Clear();
                foreach (string u in users)
                    listBoxUsers.Items.Add(u);

                SetStatus($"{users.Count} user online  ({DateTime.Now:HH:mm:ss})", Color.SeaGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi làm mới: {ex.Message}", Color.OrangeRed);
            }
        }

        // ── Gửi tin nhắn văn bản ─────────────────────────────────────
        private async void btnSend_Click(object sender, EventArgs e)
        {
            if (!_p2pReady)
            {
                SetStatus("Chọn một user trong danh sách trước.", Color.OrangeRed);
                return;
            }

            string message = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            txtMessage.Clear();
            SetButtonSend(false);

            AppendChat($"Tôi:  {message}", Color.FromArgb(80, 140, 220));

            try
            {
                if (_isBotPeer)
                {
                    SetStatus($"Đang chờ phản hồi từ {_peerName}...", Color.DodgerBlue);

                    var (textResp, audioData) = await P2PChatService.SendMessageAsync(
                        _peerIp, _peerPort, message);

                    AppendChat($"{_peerName}: {textResp}", Color.FromArgb(200, 80, 130));

                    if (audioData.Length > 0)
                    {
                        SetStatus($"♪ Nhận audio {audioData.Length / 1024:N0} KB từ VoiceVox", Color.SeaGreen);
                        PlayAudio(audioData);
                    }
                    else
                    {
                        SetStatus("OK", Color.SeaGreen);
                    }
                }
                else
                {
                    SetStatus($"Đang gửi...", Color.DodgerBlue);
                    await P2PChatService.SendToClientAsync(_peerIp, _peerPort, _username, message);
                    SetStatus("Đã gửi", Color.SeaGreen);
                }
            }
            catch (Exception ex)
            {
                AppendChat($"[Lỗi gửi] {ex.Message}", Color.Crimson);
                SetStatus($"Lỗi: {ex.Message}", Color.Crimson);
            }
            finally
            {
                SetButtonSend(true);
                txtMessage.Focus();
            }
        }

        // ── Gửi file / ảnh ───────────────────────────────────────────
        private async void btnSendFile_Click(object sender, EventArgs e)
        {
            if (!_p2pReady)
            {
                SetStatus("Chọn một user trong danh sách trước.", Color.OrangeRed);
                return;
            }

            if (_isBotPeer)
            {
                SetStatus("Bot UitiChan không nhận file.", Color.Gray);
                return;
            }

            using var dlg = new OpenFileDialog
            {
                Title  = "Chọn file để gửi",
                Filter = "Tất cả file (*.*)|*.*|Ảnh (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string filePath = dlg.FileName;
            string fileName = Path.GetFileName(filePath);
            long   fileSize = new FileInfo(filePath).Length;

            AppendChat(
                $"Tôi:  📎 \"{fileName}\"  ({fileSize / 1024.0:F1} KB)",
                Color.FromArgb(80, 140, 220));

            SetButtonSend(false);
            btnSendFile.Enabled = false;

            try
            {
                var progress = new Progress<int>(pct =>
                    SetStatus($"Đang gửi \"{fileName}\"... {pct}%", Color.DodgerBlue));

                await P2PChatService.SendFileToClientAsync(
                    _peerIp, _peerPort, _username, filePath, progress);

                SetStatus($"Đã gửi \"{fileName}\"", Color.SeaGreen);
            }
            catch (Exception ex)
            {
                AppendChat($"[Lỗi gửi file] {ex.Message}", Color.Crimson);
                SetStatus($"Lỗi: {ex.Message}", Color.Crimson);
            }
            finally
            {
                SetButtonSend(true);
                btnSendFile.Enabled = true;
                txtMessage.Focus();
            }
        }

        // ── Enter để gửi ─────────────────────────────────────────────
        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                btnSend.PerformClick();
            }
        }

        // ── Đăng xuất khi đóng form ───────────────────────────────────
        private async void MainChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            P2PListenerService.MessageReceived -= OnIncomingMessage;
            P2PListenerService.FileReceived    -= OnIncomingFile;

            if (_dirPort != 0)
                await DirectoryService.LogoutAsync(_dirPort, _username);
        }

        // ── Phát audio WAV từ VoiceVox ────────────────────────────────
        private void PlayAudio(byte[] wavBytes)
        {
            try
            {
                using MemoryStream ms     = new(wavBytes);
                using SoundPlayer  player = new(ms);
                player.Play();
            }
            catch { /* Bỏ qua lỗi phát audio */ }
        }

        // ── Thread-safe UI helpers ────────────────────────────────────

        private void AppendChat(string text, Color color)
        {
            if (rtbChat.InvokeRequired)
            {
                rtbChat.Invoke(() => AppendChat(text, color));
                return;
            }

            rtbChat.SuspendLayout();
            rtbChat.SelectionStart  = rtbChat.TextLength;
            rtbChat.SelectionLength = 0;
            rtbChat.SelectionColor  = Color.FromArgb(100, 100, 115);
            rtbChat.AppendText($"[{DateTime.Now:HH:mm}]  ");
            rtbChat.SelectionColor  = color;
            rtbChat.AppendText(text + "\n");
            rtbChat.ScrollToCaret();
            rtbChat.ResumeLayout();
        }

        private void SetPeerInfo(string text, Color color)
        {
            if (lblPeerInfo.InvokeRequired)
            {
                lblPeerInfo.Invoke(() => SetPeerInfo(text, color));
                return;
            }
            lblPeerInfo.ForeColor = color;
            lblPeerInfo.Text      = text;
        }

        private void SetStatus(string text, Color? color = null)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(() => SetStatus(text, color));
                return;
            }
            lblStatus.ForeColor = color ?? Color.FromArgb(60, 60, 80);
            lblStatus.Text      = text;
        }

        private void SetButtonSend(bool enabled)
        {
            if (btnSend.InvokeRequired)
            {
                btnSend.Invoke(() => SetButtonSend(enabled));
                return;
            }
            btnSend.Enabled = enabled;
            this.Cursor = enabled ? Cursors.Default : Cursors.WaitCursor;
        }

        // Màu cho lblPeerInfo
        private static readonly Color clrConnected    = Color.FromArgb(0,  200, 120);
        private static readonly Color clrConnecting   = Color.FromArgb(200, 180,  60);
        private static readonly Color clrDisconnected = Color.FromArgb(200,  70,  70);
    }
}
