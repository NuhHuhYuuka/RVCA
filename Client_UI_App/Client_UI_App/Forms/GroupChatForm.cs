using Client_UI_App.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client_UI_App.Forms
{
    public partial class GroupChatForm : Form
    {
        private readonly string _myUsername;
        private readonly string _groupId;
        private string          _groupName;
        private readonly string _creator;   // người tạo nhóm (admin)

        // Danh sách thành viên đang được load (username → IP:Port hoặc "")
        private List<string> _members = new();

        public GroupChatForm(string myUsername, string groupId, string groupName, string creator = "")
        {
            _myUsername = myUsername;
            _groupId    = groupId;
            _groupName  = groupName;
            _creator    = creator;

            InitializeComponent();
        }

        private void GroupChatForm_Load(object sender, EventArgs e)
        {
            this.Text       = $"Nhóm: {_groupName}  [{_groupId}]";
            lblGroupId.Text = _groupId;

            // Chỉ admin (creator) mới thấy nút đổi tên
            btnRenameGroup.Visible = (_creator == _myUsername || string.IsNullOrEmpty(_creator));

            P2PListenerService.GroupMessageReceived += OnGroupMessage;
            P2PListenerService.GroupFileReceived    += OnGroupFile;

            _ = RefreshMembersAsync();
        }

        private void GroupChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            P2PListenerService.GroupMessageReceived -= OnGroupMessage;
            P2PListenerService.GroupFileReceived    -= OnGroupFile;
        }

        // ── Nhận tin nhắn nhóm từ listener ───────────────────────────
        private void OnGroupMessage(string groupId, string groupName, string sender, string message)
        {
            if (groupId != _groupId) return;
            AppendChat($"{sender}: {message}", Color.FromArgb(30, 180, 100));
        }

        // ── Nhận file nhóm từ listener ────────────────────────────────
        private void OnGroupFile(string groupId, string groupName, string sender, string fileName, string savePath)
        {
            if (groupId != _groupId) return;
            AppendChat($"📁  {sender} gửi \"{fileName}\"  →  {savePath}", Color.FromArgb(255, 180, 50));
        }

        // ── Làm mới danh sách thành viên ─────────────────────────────
        private async Task RefreshMembersAsync()
        {
            SetStatus("Đang tải danh sách thành viên...", Color.DodgerBlue);
            try
            {
                var (found, _, members) = await DirectoryService.GetGroupMembersAsync(_groupId);
                if (found)
                {
                    _members = members;
                    listBoxMembers.Invoke(() =>
                    {
                        listBoxMembers.Items.Clear();
                        foreach (string m in members)
                            listBoxMembers.Items.Add(m == _myUsername ? $"{m} (tôi)" : m);
                    });
                    SetStatus($"{members.Count} thành viên", Color.SeaGreen);
                }
                else
                {
                    SetStatus("Không tìm thấy nhóm.", Color.OrangeRed);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi: {ex.Message}", Color.Crimson);
            }
        }

        // ── Nút làm mới thành viên ────────────────────────────────────
        private async void btnRefreshMembers_Click(object sender, EventArgs e)
        {
            await RefreshMembersAsync();
        }

        // ── Sao chép Group ID vào clipboard ──────────────────────────
        private void btnCopyId_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(_groupId);
            SetStatus($"Đã sao chép ID nhóm: {_groupId}", Color.SeaGreen);
        }

        // ── Nút đổi tên nhóm (chỉ creator mới thấy) ─────────────────
        private async void btnRenameGroup_Click(object sender, EventArgs e)
        {
            string? newName = PromptInput(
                "Đổi tên nhóm",
                $"Tên mới cho nhóm \"{_groupName}\":",
                _groupName);

            if (string.IsNullOrWhiteSpace(newName) || newName == _groupName) return;

            SetStatus("Đang đổi tên nhóm...", Color.DodgerBlue);
            try
            {
                var (ok, result) = await DirectoryService.RenameGroupAsync(_groupId, newName, _myUsername);
                if (ok)
                {
                    _groupName   = result;
                    this.Text    = $"Nhóm: {_groupName}  [{_groupId}]";
                    SetStatus($"Đã đổi tên thành \"{_groupName}\"", Color.SeaGreen);
                }
                else
                {
                    SetStatus($"Thất bại: {result}", Color.Crimson);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi: {ex.Message}", Color.Crimson);
            }
        }

        // Helper: hiện input dialog đơn giản
        private static string? PromptInput(string title, string prompt, string defaultValue = "")
        {
            using var form  = new Form();
            using var label = new Label  { Text = prompt, Dock = DockStyle.Top, Height = 30, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            using var txt   = new TextBox { Text = defaultValue, Dock = DockStyle.Top };
            using var ok    = new Button  { Text = "OK",   DialogResult = DialogResult.OK,     Dock = DockStyle.Right, Width = 80 };
            using var cancel= new Button  { Text = "Huỷ", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 80 };

            var pnlBtns = new Panel { Dock = DockStyle.Bottom, Height = 36 };
            pnlBtns.Controls.Add(ok);
            pnlBtns.Controls.Add(cancel);

            form.Text            = title;
            form.ClientSize      = new System.Drawing.Size(360, 100);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition   = FormStartPosition.CenterParent;
            form.MaximizeBox     = false;
            form.MinimizeBox     = false;
            form.AcceptButton    = ok;
            form.CancelButton    = cancel;
            form.Controls.Add(pnlBtns);
            form.Controls.Add(txt);
            form.Controls.Add(label);

            txt.SelectAll();
            txt.Focus();

            return form.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
        }

        // ── Nút rời nhóm ─────────────────────────────────────────────
        private async void btnLeaveGroup_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show(
                $"Bạn có chắc muốn rời nhóm \"{_groupName}\"?",
                "Xác nhận rời nhóm",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetStatus("Đang rời nhóm...", Color.DodgerBlue);
            try
            {
                await DirectoryService.LeaveGroupAsync(_groupId, _myUsername);
                SetStatus("Đã rời nhóm.", Color.SeaGreen);
                await Task.Delay(500);
                this.Close();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi: {ex.Message}", Color.Crimson);
            }
        }

        // ── Gửi tin nhắn văn bản ─────────────────────────────────────
        private async void btnSend_Click(object sender, EventArgs e)
        {
            string message = txtMessage.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            txtMessage.Clear();
            SetButtonSend(false);
            AppendChat($"Tôi: {message}", Color.FromArgb(80, 140, 220));

            try
            {
                var endpoints = await ResolveOnlineMembersAsync();
                if (endpoints.Count == 0)
                {
                    SetStatus("Không có thành viên nào online để gửi.", Color.OrangeRed);
                    return;
                }

                SetStatus($"Đang gửi tới {endpoints.Count} thành viên...", Color.DodgerBlue);
                await GroupChatService.SendGroupMessageAsync(_groupId, _groupName, _myUsername, message, endpoints);
                SetStatus("Đã gửi", Color.SeaGreen);

                // @Uiti mention → hỏi bot, broadcast reply cho nhóm
                if (message.Contains("@Uiti", StringComparison.OrdinalIgnoreCase))
                    _ = HandleUitiMentionAsync(message, endpoints);
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

        // ── @Uiti mention: hỏi bot rồi broadcast reply vào nhóm ─────
        private async Task HandleUitiMentionAsync(
            string message,
            List<(string ip, int port)> endpoints)
        {
            try
            {
                SetStatus("Đang chờ UitiChan trả lời...", Color.DodgerBlue);
                var (textResp, _) = await P2PChatService.SendMessageAsync("127.0.0.1", 5555, message);

                // Hiển thị ngay cho người gọi
                AppendChat($"UitiChan: {textResp}", Color.FromArgb(200, 80, 130));

                // Broadcast reply cho tất cả thành viên khác
                if (endpoints.Count > 0)
                    await GroupChatService.SendGroupMessageAsync(
                        _groupId, _groupName, "UitiChan", textResp, endpoints);

                SetStatus("UitiChan đã trả lời", Color.SeaGreen);
            }
            catch
            {
                SetStatus("UitiChan không phản hồi (bot offline?)", Color.OrangeRed);
            }
        }

        // ── Gửi file / ảnh ───────────────────────────────────────────
        private async void btnSendFile_Click(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Chọn file để gửi nhóm",
                Filter = "Tất cả file (*.*)|*.*|Ảnh (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string filePath = dlg.FileName;
            string fileName = Path.GetFileName(filePath);
            long   fileSize = new FileInfo(filePath).Length;

            AppendChat($"Tôi: 📎 \"{fileName}\"  ({fileSize / 1024.0:F1} KB)", Color.FromArgb(80, 140, 220));

            SetButtonSend(false);
            btnSendFile.Enabled = false;

            try
            {
                var endpoints = await ResolveOnlineMembersAsync();
                if (endpoints.Count == 0)
                {
                    SetStatus("Không có thành viên nào online để gửi.", Color.OrangeRed);
                    return;
                }

                var progress = new Progress<int>(pct =>
                    SetStatus($"Đang gửi \"{fileName}\"... {pct}%", Color.DodgerBlue));

                await GroupChatService.SendGroupFileAsync(
                    _groupId, _groupName, _myUsername, filePath, endpoints, progress);

                SetStatus($"Đã gửi \"{fileName}\" tới nhóm", Color.SeaGreen);
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

        // ── Resolve IP:Port cho tất cả thành viên (trừ mình) ─────────
        private async Task<List<(string ip, int port)>> ResolveOnlineMembersAsync()
        {
            // Làm mới danh sách thành viên trước
            var (found, _, members) = await DirectoryService.GetGroupMembersAsync(_groupId);
            if (!found) return new List<(string, int)>();

            _members = members;

            var endpoints = new List<(string ip, int port)>();
            int dirPort   = await DirectoryService.GetDirectoryPortAsync();

            foreach (string member in members)
            {
                if (member == _myUsername) continue;

                var (userFound, ipPort) = await DirectoryService.GetUserAsync(dirPort, member);
                if (!userFound || string.IsNullOrEmpty(ipPort)) continue;

                string[] ipParts = ipPort.Split(':');
                if (ipParts.Length != 2 || !int.TryParse(ipParts[1], out int port)) continue;
                endpoints.Add((ipParts[0], port));
            }

            return endpoints;
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

        private void SetStatus(string text, Color? color = null)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(() => SetStatus(text, color));
                return;
            }
            lblStatus.ForeColor = color ?? Color.FromArgb(160, 160, 180);
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
    }
}
