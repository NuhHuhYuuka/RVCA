using Client_UI_App.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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

        // ── Voice channel ─────────────────────────────────────────────
        private GroupVoiceService?  _voiceService;
        private readonly HashSet<string> _voiceMembers = new();
        // JOIN info từ peer trước khi mình vào voice — dùng khi mình join sau
        private readonly Dictionary<string, (string ip, int udpPort, int tcpPort)> _pendingVoiceJoins = new();
        // REPLY info từ peer khi mình không trong voice — buffer để xử lý lần join tiếp
        private readonly Dictionary<string, (string ip, int udpPort)> _pendingVoiceReplies = new();

        // ── Voice channel form ────────────────────────────────────────
        private GroupVoiceForm?      _voiceForm;

        // ── Video channel ─────────────────────────────────────────────
        private GroupVideoService?   _videoService;
        private VideoCaptureService? _videoCapture;
        private GroupVideoForm?      _videoForm;
        private readonly HashSet<string> _videoMembers = new();
        // JOIN info từ peer trước khi mình vào video — dùng khi mình join sau
        private readonly Dictionary<string, (string ip, int audioPort, int videoPort, int tcpPort)> _pendingVideoJoins = new();
        // REPLY info từ peer khi mình không trong video — buffer để xử lý lần join tiếp
        private readonly Dictionary<string, (string ip, int audioPort, int videoPort)> _pendingVideoReplies = new();

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
            P2PListenerService.GroupRenamed         += OnGroupRenamed;
            P2PListenerService.GroupVoiceJoined     += OnGroupVoiceJoined;
            P2PListenerService.GroupVoiceReplied    += OnGroupVoiceReplied;
            P2PListenerService.GroupVoiceLeft       += OnGroupVoiceLeft;
            P2PListenerService.GroupVideoJoined     += OnGroupVideoJoined;
            P2PListenerService.GroupVideoReplied    += OnGroupVideoReplied;
            P2PListenerService.GroupVideoLeft       += OnGroupVideoLeft;

            _ = RefreshMembersAsync();
        }

        private void GroupChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            P2PListenerService.GroupMessageReceived -= OnGroupMessage;
            P2PListenerService.GroupFileReceived    -= OnGroupFile;
            P2PListenerService.GroupRenamed         -= OnGroupRenamed;
            P2PListenerService.GroupVoiceJoined     -= OnGroupVoiceJoined;
            P2PListenerService.GroupVoiceReplied    -= OnGroupVoiceReplied;
            P2PListenerService.GroupVoiceLeft       -= OnGroupVoiceLeft;
            P2PListenerService.GroupVideoJoined     -= OnGroupVideoJoined;
            P2PListenerService.GroupVideoReplied    -= OnGroupVideoReplied;
            P2PListenerService.GroupVideoLeft       -= OnGroupVideoLeft;

            _voiceForm?.Close();
            _voiceForm = null;
            _voiceService?.Stop();
            _voiceService = null;

            _videoCapture?.Dispose();
            _videoCapture = null;
            _videoService?.Stop();
            _videoService = null;
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
            if (IsImageFile(fileName))
            {
                if (InvokeRequired) Invoke(() => AppendImageToRtb(savePath));
                else AppendImageToRtb(savePath);
            }
        }

        private static bool IsImageFile(string name)
        {
            string ext = Path.GetExtension(name).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
        }

        private void AppendImageToRtb(string imagePath)
        {
            try
            {
                byte[] raw = File.ReadAllBytes(imagePath);
                using var ms   = new MemoryStream(raw);
                using var orig = Image.FromStream(ms);
                int w = Math.Min(orig.Width, 220);
                int h = orig.Width > 0 ? (int)(orig.Height * ((double)w / orig.Width)) : 120;
                if (w <= 0 || h <= 0) return;
                using var thumb = new Bitmap(w, h);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(orig, 0, 0, w, h);
                }
                bool wasReadOnly = rtbChat.ReadOnly;
                rtbChat.ReadOnly = false;
                rtbChat.SelectionStart = rtbChat.TextLength;
                Clipboard.SetImage(thumb);
                rtbChat.Paste();
                rtbChat.AppendText("\n");
                rtbChat.ReadOnly = wasReadOnly;
                Clipboard.Clear();
                rtbChat.ScrollToCaret();
            }
            catch { }
        }

        // ── Nhận thông báo đổi tên nhóm real-time ────────────────────
        private void OnGroupRenamed(string groupId, string newName)
        {
            if (groupId != _groupId) return;
            _groupName = newName;
            void Update() => this.Text = $"Nhóm: {_groupName}  [{_groupId}]";
            if (InvokeRequired) Invoke(Update); else Update();
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
                    _groupName = result;
                    this.Text  = $"Nhóm: {_groupName}  [{_groupId}]";
                    SetStatus($"Đã đổi tên thành \"{_groupName}\"", Color.SeaGreen);

                    // Broadcast real-time tới các thành viên đang mở nhóm
                    var endpoints = await ResolveOnlineMembersAsync();
                    if (endpoints.Count > 0)
                        _ = GroupChatService.BroadcastRenameAsync(_groupId, _groupName, _myUsername, endpoints);
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
            List<(string name, string ip, int port)> endpoints)
        {
            try
            {
                // Tra cứu IP thật của bot — query cả 2 server
                var (botFound, ipPort) = await DirectoryService.GetUserAsync("UitiChan");
                if (!botFound)
                {
                    SetStatus("UitiChan không online.", Color.OrangeRed);
                    return;
                }
                string[] botParts = ipPort.Split(':');
                string   botIp    = botParts[0];
                int      botPort  = int.TryParse(botParts.Length > 1 ? botParts[1] : "0", out int bp) ? bp : 5555;

                SetStatus("Đang chờ UitiChan trả lời...", Color.DodgerBlue);
                var (textResp, _) = await P2PChatService.SendMessageAsync(botIp, botPort, message);

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

        // ══════════════════════════════════════════════════════════════
        //  VOICE CHANNEL
        // ══════════════════════════════════════════════════════════════

        private async void btnVoice_Click(object sender, EventArgs e)
        {
            if (_voiceService != null) await LeaveVoiceAsync();
            else await JoinVoiceAsync();
        }

        private async Task JoinVoiceAsync()
        {
            btnVoice.Enabled = false;
            SetStatus("Đang tham gia voice channel...", Color.DodgerBlue);
            try
            {
                _voiceService = new GroupVoiceService();
                await _voiceService.StartAsync();
                int myUdp = _voiceService.LocalUdpPort;

                _voiceMembers.Add(_myUsername);
                UpdateVoiceButton();
                string myVoiceIp = DirectoryService.GetIpFacingServer();
                AppendChat($"[Voice] Bạn đã tham gia voice channel (UDP :{myUdp}, IP:{myVoiceIp})", Color.FromArgb(0, 200, 150));
                if (!_voiceService.HasAudio)
                    AppendChat("[Voice] ⚠️ Không khởi được thiết bị audio — chỉ nhận (không mic/loa)", Color.OrangeRed);

                ShowVoiceForm();

                // Kết nối ngay với các peer đã join trước khi mình vào (dùng info đã cache)
                foreach (var (peerName, (peerIp, peerUdp, peerTcp)) in _pendingVoiceJoins)
                {
                    _voiceService.AddPeer(peerName, peerIp, peerUdp);
                    _voiceForm?.AddMember(peerName);
                    AppendChat($"[Voice] Kết nối {peerName} → {peerIp}:{peerUdp}", Color.FromArgb(0, 180, 130));
                    _ = GroupChatService.SendVoiceReplyAsync(
                        peerIp, peerTcp, _groupId, _myUsername, myUdp, _myUsername, peerName);
                }
                _pendingVoiceJoins.Clear();

                // Kết nối với các peer đã reply khi mình chưa trong voice (join/leave trước đó)
                foreach (var (peerName, (peerIp, peerUdp)) in _pendingVoiceReplies)
                {
                    if (!_voiceService.HasPeer(peerName))
                    {
                        _voiceService.AddPeer(peerName, peerIp, peerUdp);
                        _voiceForm?.AddMember(peerName);
                        AppendChat($"[Voice] ↺ Cached reply {peerName} → {peerIp}:{peerUdp}", Color.FromArgb(0, 180, 130));
                    }
                }
                _pendingVoiceReplies.Clear();

                var endpoints = await ResolveOnlineMembersAsync();
                AppendChat($"[Voice] Broadcast JOIN → {endpoints.Count} thành viên: {string.Join(", ", endpoints.Select(e => e.name))}", Color.FromArgb(100, 100, 140));
                if (endpoints.Count > 0)
                    await GroupChatService.BroadcastVoiceJoinAsync(
                        _groupId, _myUsername, myUdp,
                        P2PListenerService.ListeningPort, endpoints);

                SetStatus($"🎙️ Voice  ({_voiceMembers.Count} người)", Color.FromArgb(0, 200, 150));

                // Retry sau 3s và 6s — phòng relay chậm hoặc peer chưa join kịp
                var capSvc = _voiceService;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (IsDisposed) return;
                    BeginInvoke(() => _ = RetryVoiceAnnounceAsync(capSvc, myUdp));
                });
                _ = Task.Run(async () =>
                {
                    await Task.Delay(6000);
                    if (IsDisposed) return;
                    BeginInvoke(() => _ = RetryVoiceAnnounceAsync(capSvc, myUdp));
                });
            }
            catch (Exception ex)
            {
                _voiceService?.Stop();
                _voiceService = null;
                _voiceMembers.Remove(_myUsername);
                UpdateVoiceButton();
                SetStatus($"Lỗi voice: {ex.Message}", Color.Crimson);
            }
            finally { btnVoice.Enabled = true; }
        }

        private async Task LeaveVoiceAsync()
        {
            btnVoice.Enabled = false;
            try
            {
                _voiceForm?.Close();
                _voiceForm = null;
                _voiceService?.Stop();
                _voiceService = null;
                _voiceMembers.Clear();
                _pendingVoiceJoins.Clear();
                _pendingVoiceReplies.Clear();
                UpdateVoiceButton();
                AppendChat("[Voice] Bạn đã rời voice channel", Color.FromArgb(200, 100, 100));

                var endpoints = await ResolveOnlineMembersAsync();
                if (endpoints.Count > 0)
                    await GroupChatService.BroadcastVoiceLeaveAsync(_groupId, _myUsername, endpoints);

                SetStatus($"{_members.Count} thành viên", Color.SeaGreen);
            }
            catch (Exception ex) { SetStatus($"Lỗi: {ex.Message}", Color.Crimson); }
            finally { btnVoice.Enabled = true; }
        }

        // Nhận GROUP_VOICE_JOIN — peer vừa tham gia, cần reply UDP port cho họ
        private void OnGroupVoiceJoined(string groupId, string peerName, string peerIp,
                                        int peerUdpPort, int peerTcpPort)
        {
            if (groupId != _groupId || peerName == _myUsername) return;
            if (InvokeRequired)
            {
                Invoke(() => OnGroupVoiceJoined(groupId, peerName, peerIp, peerUdpPort, peerTcpPort));
                return;
            }

            _voiceMembers.Add(peerName);
            UpdateVoiceButton();
            AppendChat($"[Voice] {peerName} tham gia → {peerIp}:{peerUdpPort}", Color.FromArgb(0, 200, 150));
            _voiceForm?.AddMember(peerName);

            // Nếu mình đang trong voice channel → thêm peer và reply UDP port
            if (_voiceService != null)
            {
                _voiceService.AddPeer(peerName, peerIp, peerUdpPort);
                _ = GroupChatService.SendVoiceReplyAsync(
                    peerIp, peerTcpPort, _groupId, _myUsername, _voiceService.LocalUdpPort,
                    _myUsername, peerName);
            }
            else
            {
                // Lưu lại để AddPeer ngay khi mình join voice
                _pendingVoiceJoins[peerName] = (peerIp, peerUdpPort, peerTcpPort);
            }
        }

        // Nhận GROUP_VOICE_REPLY — peer đang trong channel, xác nhận UDP port của họ
        private void OnGroupVoiceReplied(string groupId, string peerName, string peerIp, int peerUdpPort)
        {
            if (groupId != _groupId || peerName == _myUsername) return;
            if (InvokeRequired)
            {
                Invoke(() => OnGroupVoiceReplied(groupId, peerName, peerIp, peerUdpPort));
                return;
            }

            _voiceMembers.Add(peerName);
            UpdateVoiceButton();
            AppendChat($"[Voice] {peerName} reply → {peerIp}:{peerUdpPort}", Color.FromArgb(0, 200, 150));
            _voiceForm?.AddMember(peerName);

            if (_voiceService != null)
                _voiceService.AddPeer(peerName, peerIp, peerUdpPort);
            else
                _pendingVoiceReplies[peerName] = (peerIp, peerUdpPort);
        }

        // Nhận GROUP_VOICE_LEAVE — peer rời channel
        private void OnGroupVoiceLeft(string groupId, string peerName)
        {
            if (groupId != _groupId) return;
            if (InvokeRequired) { Invoke(() => OnGroupVoiceLeft(groupId, peerName)); return; }

            _voiceMembers.Remove(peerName);
            _voiceService?.RemovePeer(peerName);
            // Không xóa _pendingVoiceJoins — LEAVE có thể đến trễ hơn JOIN mới do relay thứ tự không đảm bảo
            _voiceForm?.RemoveMember(peerName);
            UpdateVoiceButton();
            AppendChat($"[Voice] {peerName} đã rời voice channel", Color.FromArgb(200, 100, 100));

            if (_voiceMembers.Count == 0)
                SetStatus($"{_members.Count} thành viên", Color.SeaGreen);
            else
                SetStatus($"🎙️ Voice  ({_voiceMembers.Count} người)", Color.FromArgb(0, 200, 150));
        }

        // Retry broadcast JOIN sau delay — xử lý trường hợp relay chậm hoặc peer chưa ready
        private async Task RetryVoiceAnnounceAsync(GroupVoiceService capSvc, int myUdp)
        {
            if (_voiceService != capSvc || _voiceService == null) return;

            // Xử lý pending joins mới (peer join trễ gửi JOIN sau lần đầu chúng ta broadcast)
            var pending = new Dictionary<string, (string ip, int udpPort, int tcpPort)>(_pendingVoiceJoins);
            _pendingVoiceJoins.Clear();
            foreach (var (peerName, (peerIp, peerUdp, peerTcp)) in pending)
            {
                if (!_voiceService.HasPeer(peerName))
                {
                    _voiceService.AddPeer(peerName, peerIp, peerUdp);
                    _voiceForm?.AddMember(peerName);
                    AppendChat($"[Voice] ↺ Kết nối {peerName} → {peerIp}:{peerUdp}", Color.FromArgb(0, 180, 130));
                    _ = GroupChatService.SendVoiceReplyAsync(
                        peerIp, peerTcp, _groupId, _myUsername, myUdp, _myUsername, peerName);
                }
            }

            // Re-broadcast JOIN lần 2 — đảm bảo peer nhận được dù relay lần đầu thất bại
            var refreshed = await ResolveOnlineMembersAsync();
            if (_voiceService != capSvc || refreshed.Count == 0) return;
            AppendChat($"[Voice] ↺ Re-broadcast JOIN → {refreshed.Count} thành viên", Color.FromArgb(80, 80, 120));
            await GroupChatService.BroadcastVoiceJoinAsync(
                _groupId, _myUsername, myUdp, P2PListenerService.ListeningPort, refreshed);
        }

        private async Task RetryVideoAnnounceAsync(GroupVideoService capSvc, int myAudio, int myVideo)
        {
            if (_videoService != capSvc || _videoService == null) return;

            var pending = new Dictionary<string, (string ip, int audioPort, int videoPort, int tcpPort)>(_pendingVideoJoins);
            _pendingVideoJoins.Clear();
            foreach (var (peerName, (peerIp, peerAudio, peerVideo, peerTcp)) in pending)
            {
                if (!_videoService.HasPeer(peerName))
                {
                    _videoService.AddPeer(peerName, peerIp, peerAudio, peerVideo);
                    _videoForm?.AddPeerTile(peerName);
                    AppendChat($"[Video] ↺ Kết nối {peerName} → {peerIp} audio:{peerAudio} video:{peerVideo}", Color.FromArgb(80, 130, 200));
                    _ = GroupChatService.SendVideoReplyAsync(
                        peerIp, peerTcp, _groupId, _myUsername,
                        myAudio, myVideo, _myUsername, peerName);
                }
            }

            var refreshed = await ResolveOnlineMembersAsync();
            if (_videoService != capSvc || refreshed.Count == 0) return;
            AppendChat($"[Video] ↺ Re-broadcast JOIN → {refreshed.Count} thành viên", Color.FromArgb(80, 80, 120));
            await GroupChatService.BroadcastVideoJoinAsync(
                _groupId, _myUsername, myAudio, myVideo,
                P2PListenerService.ListeningPort, refreshed);
        }

        private void ShowVoiceForm()
        {
            if (_voiceService == null) return;
            if (_voiceForm != null && !_voiceForm.IsDisposed) { _voiceForm.BringToFront(); return; }

            _voiceForm = new GroupVoiceForm(_myUsername, _voiceService);
            _voiceForm.LeaveRequested += async () => await LeaveVoiceAsync();
            _voiceForm.FormClosed     += (_, _) => { _voiceForm = null; };

            foreach (string m in _voiceMembers)
                if (m != _myUsername) _voiceForm.AddMember(m);

            // FindForm() trả về MainChatForm (top-level owner) — giữ voice form luôn trên cùng
            _voiceForm.Show(FindForm());
            _voiceForm.BringToFront();
        }

        private void UpdateVoiceButton()
        {
            bool inVoice = _voiceService != null;
            btnVoice.Text      = inVoice
                ? $"🔴 Rời Voice  ({_voiceMembers.Count})"
                : "🎙️ Voice";
            btnVoice.BackColor = inVoice
                ? Color.FromArgb(150, 40, 40)
                : Color.FromArgb(40, 100, 60);
            btnVoice.FlatAppearance.MouseOverBackColor = inVoice
                ? Color.FromArgb(180, 50, 50)
                : Color.FromArgb(50, 130, 80);
        }

        // ══════════════════════════════════════════════════════════════
        //  VIDEO CHANNEL
        // ══════════════════════════════════════════════════════════════

        private async void btnVideo_Click(object sender, EventArgs e)
        {
            if (_videoService != null) await LeaveVideoAsync();
            else await JoinVideoAsync();
        }

        private async Task JoinVideoAsync()
        {
            btnVideo.Enabled = false;
            SetStatus("Đang tham gia video channel...", Color.DodgerBlue);
            try
            {
                _videoService = new GroupVideoService();
                await _videoService.StartAsync();

                _videoCapture = TryStartCamera();

                _videoMembers.Add(_myUsername);
                UpdateVideoButton();
                string myVideoIp = DirectoryService.GetIpFacingServer();
                AppendChat($"[Video] Bạn đã tham gia video channel (audio:{_videoService.LocalAudioPort} video:{_videoService.LocalVideoPort}, IP:{myVideoIp})", Color.FromArgb(80, 150, 230));
                if (!_videoService.HasAudio)
                    AppendChat("[Video] ⚠️ Không khởi được thiết bị audio — chỉ video (không mic/loa)", Color.OrangeRed);

                ShowVideoForm();

                // Kết nối ngay với các peer đã join trước khi mình vào (dùng info đã cache)
                foreach (var (peerName, (peerIp, peerAudio, peerVideo, peerTcp)) in _pendingVideoJoins)
                {
                    _videoService.AddPeer(peerName, peerIp, peerAudio, peerVideo);
                    _videoForm?.AddPeerTile(peerName);
                    AppendChat($"[Video] Kết nối {peerName} → {peerIp} audio:{peerAudio} video:{peerVideo}", Color.FromArgb(80, 150, 230));
                    _ = GroupChatService.SendVideoReplyAsync(
                        peerIp, peerTcp, _groupId, _myUsername,
                        _videoService.LocalAudioPort, _videoService.LocalVideoPort,
                        _myUsername, peerName);
                }
                _pendingVideoJoins.Clear();

                // Kết nối với các peer đã reply khi mình chưa trong video
                foreach (var (peerName, (peerIp, peerAudio, peerVideo)) in _pendingVideoReplies)
                {
                    if (!_videoService.HasPeer(peerName))
                    {
                        _videoService.AddPeer(peerName, peerIp, peerAudio, peerVideo);
                        _videoForm?.AddPeerTile(peerName);
                        AppendChat($"[Video] ↺ Cached reply {peerName} → {peerIp} audio:{peerAudio} video:{peerVideo}", Color.FromArgb(80, 150, 230));
                    }
                }
                _pendingVideoReplies.Clear();

                var endpoints = await ResolveOnlineMembersAsync();
                AppendChat($"[Video] Broadcast JOIN tới {endpoints.Count} endpoint: {string.Join(", ", endpoints.Select(e => e.name))}", Color.FromArgb(100, 100, 140));
                if (endpoints.Count > 0)
                    await GroupChatService.BroadcastVideoJoinAsync(
                        _groupId, _myUsername,
                        _videoService.LocalAudioPort, _videoService.LocalVideoPort,
                        P2PListenerService.ListeningPort, endpoints);

                SetStatus($"📹 Video  ({_videoMembers.Count} người)", Color.FromArgb(80, 150, 230));

                // Retry sau 3s và 6s — phòng relay chậm hoặc peer chưa join kịp
                var capSvcV   = _videoService;
                int myAudio   = _videoService.LocalAudioPort;
                int myVideo   = _videoService.LocalVideoPort;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (IsDisposed) return;
                    BeginInvoke(() => _ = RetryVideoAnnounceAsync(capSvcV, myAudio, myVideo));
                });
                _ = Task.Run(async () =>
                {
                    await Task.Delay(6000);
                    if (IsDisposed) return;
                    BeginInvoke(() => _ = RetryVideoAnnounceAsync(capSvcV, myAudio, myVideo));
                });
            }
            catch (Exception ex)
            {
                _videoCapture?.Dispose(); _videoCapture = null;
                _videoService?.Stop();    _videoService = null;
                _videoMembers.Remove(_myUsername);
                UpdateVideoButton();
                SetStatus($"Lỗi video: {ex.Message}", Color.Crimson);
            }
            finally { btnVideo.Enabled = true; }
        }

        private async Task LeaveVideoAsync()
        {
            btnVideo.Enabled = false;
            try
            {
                _videoForm?.Close();
                _videoForm = null;

                _videoCapture?.Dispose(); _videoCapture = null;

                _videoService?.Stop(); _videoService = null;
                _videoMembers.Clear();
                _pendingVideoJoins.Clear();
                _pendingVideoReplies.Clear();
                UpdateVideoButton();
                AppendChat("[Video] Bạn đã rời video channel", Color.FromArgb(200, 100, 100));

                var endpoints = await ResolveOnlineMembersAsync();
                if (endpoints.Count > 0)
                    await GroupChatService.BroadcastVideoLeaveAsync(_groupId, _myUsername, endpoints);

                SetStatus($"{_members.Count} thành viên", Color.SeaGreen);
            }
            catch (Exception ex) { SetStatus($"Lỗi: {ex.Message}", Color.Crimson); }
            finally { btnVideo.Enabled = true; }
        }

        private void ShowVideoForm()
        {
            if (_videoForm != null && !_videoForm.IsDisposed) { _videoForm.BringToFront(); return; }

            _videoForm = new GroupVideoForm(_myUsername, _videoService!, _videoCapture);
            _videoForm.LeaveRequested += async () => await LeaveVideoAsync();
            _videoForm.FormClosed     += (_, _) => { _videoForm = null; };

            // Thêm tile cho các peer đang trong channel
            foreach (string m in _videoMembers)
                if (m != _myUsername) _videoForm.AddPeerTile(m);

            _videoForm.Show(FindForm());
            _videoForm.BringToFront();
        }

        // Peer vừa JOIN video channel
        private void OnGroupVideoJoined(string groupId, string peerName, string peerIp,
                                        int peerAudio, int peerVideo, int peerTcpPort)
        {
            if (groupId != _groupId || peerName == _myUsername) return;
            if (InvokeRequired)
            {
                Invoke(() => OnGroupVideoJoined(groupId, peerName, peerIp, peerAudio, peerVideo, peerTcpPort));
                return;
            }

            _videoMembers.Add(peerName);
            UpdateVideoButton();
            AppendChat($"[Video] {peerName} tham gia → {peerIp} audio:{peerAudio} video:{peerVideo}", Color.FromArgb(80, 150, 230));
            _videoForm?.AddPeerTile(peerName);

            if (_videoService != null)
            {
                _videoService.AddPeer(peerName, peerIp, peerAudio, peerVideo);
                _ = GroupChatService.SendVideoReplyAsync(
                    peerIp, peerTcpPort, _groupId, _myUsername,
                    _videoService.LocalAudioPort, _videoService.LocalVideoPort,
                    _myUsername, peerName);
            }
            else
            {
                // Lưu lại để AddPeer ngay khi mình join video
                _pendingVideoJoins[peerName] = (peerIp, peerAudio, peerVideo, peerTcpPort);
            }
        }

        // Nhận REPLY — peer đang trong channel
        private void OnGroupVideoReplied(string groupId, string peerName, string peerIp,
                                         int peerAudio, int peerVideo)
        {
            if (groupId != _groupId || peerName == _myUsername) return;
            if (InvokeRequired)
            {
                Invoke(() => OnGroupVideoReplied(groupId, peerName, peerIp, peerAudio, peerVideo));
                return;
            }

            _videoMembers.Add(peerName);
            UpdateVideoButton();
            AppendChat($"[Video] {peerName} reply → {peerIp} audio:{peerAudio} video:{peerVideo}", Color.FromArgb(80, 150, 230));
            _videoForm?.AddPeerTile(peerName);

            if (_videoService != null)
                _videoService.AddPeer(peerName, peerIp, peerAudio, peerVideo);
            else
                _pendingVideoReplies[peerName] = (peerIp, peerAudio, peerVideo);
        }

        // Peer rời video channel
        private void OnGroupVideoLeft(string groupId, string peerName)
        {
            if (groupId != _groupId) return;
            if (InvokeRequired) { Invoke(() => OnGroupVideoLeft(groupId, peerName)); return; }

            _videoMembers.Remove(peerName);
            _videoService?.RemovePeer(peerName);
            // Không xóa _pendingVideoJoins — LEAVE có thể đến trễ hơn JOIN mới do relay thứ tự không đảm bảo
            _videoForm?.RemovePeerTile(peerName);
            UpdateVideoButton();
            AppendChat($"[Video] {peerName} đã rời video channel", Color.FromArgb(200, 100, 100));

            SetStatus(_videoMembers.Count > 0
                ? $"📹 Video  ({_videoMembers.Count} người)"
                : $"{_members.Count} thành viên",
                _videoMembers.Count > 0 ? Color.FromArgb(80, 150, 230) : Color.SeaGreen);
        }

        private void UpdateVideoButton()
        {
            bool inVideo = _videoService != null;
            btnVideo.Text      = inVideo
                ? $"🔴 Rời Video  ({_videoMembers.Count})"
                : "📹 Video";
            btnVideo.BackColor = inVideo
                ? Color.FromArgb(150, 40, 40)
                : Color.FromArgb(40, 70, 120);
            btnVideo.FlatAppearance.MouseOverBackColor = inVideo
                ? Color.FromArgb(180, 50, 50)
                : Color.FromArgb(55, 95, 155);
        }

        private static VideoCaptureService? TryStartCamera()
        {
            try { var s = new VideoCaptureService(); s.Start(); return s; }
            catch { return null; }
        }

        // ── Resolve IP:Port cho tất cả thành viên (trừ mình) ─────────
        private async Task<List<(string name, string ip, int port)>> ResolveOnlineMembersAsync()
        {
            var (found, _, members) = await DirectoryService.GetGroupMembersAsync(_groupId);
            if (!found) return new();

            _members = members;

            var endpoints = new List<(string, string, int)>();

            foreach (string member in members)
            {
                if (member == _myUsername) continue;

                var (userFound, ipPort) = await DirectoryService.GetUserAsync(member);
                if (!userFound || string.IsNullOrEmpty(ipPort)) continue;

                string[] ipParts = ipPort.Split(':');
                if (ipParts.Length != 2 || !int.TryParse(ipParts[1], out int port)) continue;
                endpoints.Add((member, ipParts[0], port));
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
