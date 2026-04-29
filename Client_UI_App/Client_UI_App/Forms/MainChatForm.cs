using Client_UI_App.Services;
using SecurityData.Models;
using SecurityData.Services;
using System.Text;
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
        // ── Identity ──────────────────────────────────────────────────
        private readonly string _username;
        private readonly int    _dirPort;

        // ── Trạng thái peer P2P hiện tại ─────────────────────────────
        private string _peerIp   = "";
        private int    _peerPort = 0;
        private bool   _p2pReady = false;
        private string _peerName = "";
        private bool   _isBotPeer = false;

        // ── Chat sessions (Discord-style: mỗi peer có hộp chat riêng) ─
        // Key = peerName thực sự (không có badge)
        private readonly List<string>                                                   _userList     = new();
        private readonly Dictionary<string, List<(string ts, string text, Color color)>> _chatSessions = new();
        private readonly Dictionary<string, int>                                        _unreadCounts = new();
        private string _currentChatPeer = "";
        private bool   _inUserSelection = false;   // re-entrancy guard tránh StackOverflow

        // ── Lưu trữ tin nhắn cục bộ (SQLite) ────────────────────────
        private readonly DatabaseService _db = new();

        // Màu dùng để nhận biết loại tin khi save/load DB
        private static readonly int _clrSentByMe = Color.FromArgb(80, 140, 220).ToArgb();
        private static readonly int _clrFile      = Color.FromArgb(255, 180, 50).ToArgb();
        private static readonly int _clrCrimson   = Color.Crimson.ToArgb();

        // ── Voice call ────────────────────────────────────────────────
        private VoiceCallService? _activecall;
        private VoiceCallForm?    _callForm;
        private string _callPeer    = "";
        private string _callPeerIp  = "";
        private int    _callPeerPort = 0;

        // Bot voice call — giữ TCP connection mở để nhận VOICE_CAPTION
        private System.Net.Sockets.TcpClient? _botCallTcp    = null;
        private StreamWriter?                  _botCallWriter = null;

        // ── Groups ────────────────────────────────────────────────────
        private readonly List<(string Id, string Name, string Creator)> _myGroups = new();
        private readonly Dictionary<string, GroupChatForm>   _openGroupForms = new();

        // ─────────────────────────────────────────────────────────────
        public MainChatForm(string username, int dirPort, List<string> onlineUsers)
        {
            _username = username;
            _dirPort  = dirPort;

            InitializeComponent();

            foreach (string u in onlineUsers)
            {
                _userList.Add(u);
                listBoxUsers.Items.Add(u);
            }

            // P2P events
            P2PListenerService.MessageReceived += OnIncomingMessage;
            P2PListenerService.FileReceived    += OnIncomingFile;

            // Voice signaling events
            P2PListenerService.IncomingVoiceCall += OnIncomingVoiceCall;
            P2PListenerService.VoiceCallAnswered += OnVoiceCallAnswered;
            P2PListenerService.VoiceCallRejected += OnVoiceCallRejected;
            P2PListenerService.VoiceCallHungUp   += OnVoiceCallHungUp;
        }

        private async void MainChatForm_Load(object sender, EventArgs e)
        {
            this.Text = $"Uiti-chan Chat  –  {_username}  (port {P2PListenerService.ListeningPort})";

            lblMyUsername.Text = _username;

            // Peer avatar mặc định
            picPeerAvatar.Image = AvatarService.CreateInitialsBitmap("?", 34);

            // ApplyCircularClip gọi sau Shown — lúc đó control đã có kích thước thật
            this.Shown += OnFirstShown;

            txtMessage.Focus();
            await LoadMyGroupsAsync();
        }

        private void OnFirstShown(object? sender, EventArgs e)
        {
            this.Shown -= OnFirstShown;
            AvatarService.ApplyCircularClip(picMyAvatar);
            AvatarService.ApplyCircularClip(picPeerAvatar);
            LoadMyAvatar();
        }

        // ══════════════════════════════════════════════════════════════
        //  AVATAR
        // ══════════════════════════════════════════════════════════════

        private void LoadMyAvatar()
        {
            string path = AvatarService.GetUserAvatarPath(_username);
            var bmp = AvatarService.LoadBitmap(path)
                      ?? AvatarService.CreateInitialsBitmap(_username, 40);
            var old = picMyAvatar.Image;
            picMyAvatar.Image = bmp;
            old?.Dispose();
        }

        private void LoadPeerAvatar(string peerName)
        {
            Bitmap? bmp;
            if (peerName == "UitiChan")
                bmp = AvatarService.LoadBitmap(AvatarService.BotAvatarPath)
                      ?? AvatarService.CreateInitialsBitmap("U", 34);
            else
                bmp = AvatarService.CreateInitialsBitmap(peerName, 34);

            var old = picPeerAvatar.Image;
            picPeerAvatar.Image = bmp;
            old?.Dispose();
        }

        private void picMyAvatar_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "Chọn ảnh đại diện",
                Filter = "Ảnh (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                AvatarService.SaveUserAvatar(_username, dlg.FileName);
                LoadMyAvatar();
                SetStatus("Đã cập nhật ảnh đại diện.", Color.SeaGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi cập nhật avatar: {ex.Message}", Color.Crimson);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CHAT SESSION — lưu trữ + hiển thị theo từng peer
        // ══════════════════════════════════════════════════════════════

        // Thêm tin nhắn vào session của peer (thread-safe qua Invoke)
        private void AddMessage(string peer, string text, Color color)
        {
            if (string.IsNullOrEmpty(peer)) return;

            if (InvokeRequired) { Invoke(() => AddMessage(peer, text, color)); return; }

            string ts = DateTime.Now.ToString("HH:mm");

            if (!_chatSessions.TryGetValue(peer, out var session))
            {
                session = new List<(string, string, Color)>();
                _chatSessions[peer] = session;
            }
            session.Add((ts, text, color));

            // Lưu vào DB (bỏ qua tin nhắn lỗi màu đỏ)
            if (color.ToArgb() != _clrCrimson)
            {
                try
                {
                    bool isMine = color.ToArgb() == _clrSentByMe;
                    _db.SaveMessage(new ChatMessage
                    {
                        Sender    = isMine ? _username : peer,
                        Receiver  = isMine ? peer : _username,
                        Content   = text,
                        IsFile    = color.ToArgb() == _clrFile,
                        Timestamp = DateTime.Now
                    });
                }
                catch { }
            }

            // Đảm bảo peer có trong list (cho tin nhắn đến bất ngờ)
            if (!_userList.Contains(peer))
            {
                _userList.Add(peer);
                listBoxUsers.Items.Add(peer);
            }

            if (peer == _currentChatPeer)
            {
                AppendEntryToRtb(ts, text, color);
            }
            else
            {
                _unreadCounts.TryGetValue(peer, out int n);
                _unreadCounts[peer] = n + 1;
                UpdateUserListItem(peer);
            }
        }

        // Helper cho các method gửi — luôn ghi vào peer hiện tại
        private void AppendChat(string text, Color color)
            => AddMessage(_currentChatPeer, text, color);

        // Render một entry vào RTB (phải gọi trên UI thread)
        private void AppendEntryToRtb(string ts, string text, Color color)
        {
            rtbChat.SelectionStart  = rtbChat.TextLength;
            rtbChat.SelectionLength = 0;
            rtbChat.SelectionColor  = Color.FromArgb(100, 100, 115);
            rtbChat.AppendText($"[{ts}]  ");
            rtbChat.SelectionColor  = color;
            rtbChat.AppendText(text + "\n");
            rtbChat.ScrollToCaret();
        }

        // Load lịch sử từ SQLite vào _chatSessions nếu chưa có trong RAM
        private void LoadHistoryFromDb(string peer)
        {
            try
            {
                var history = _db.GetHistory(peer);
                var session = new List<(string, string, Color)>();
                foreach (var msg in history)
                {
                    bool mine = msg.Sender == _username && msg.Receiver == peer;
                    bool recv = msg.Sender == peer     && msg.Receiver == _username;
                    if (!mine && !recv) continue;

                    Color clr = msg.IsFile
                        ? Color.FromArgb(255, 180, 50)
                        : mine
                            ? Color.FromArgb(80, 140, 220)
                            : peer == "UitiChan"
                                ? Color.FromArgb(200, 80, 130)
                                : Color.FromArgb(30, 180, 100);

                    session.Add((msg.Timestamp.ToString("HH:mm"), msg.Content ?? "", clr));
                }
                _chatSessions[peer] = session;
            }
            catch
            {
                _chatSessions[peer] = new();
            }
        }

        // Tải toàn bộ session của peer vào RTB
        private void LoadSessionToRtb(string peer)
        {
            rtbChat.Clear();
            if (!_chatSessions.TryGetValue(peer, out var session)) return;
            rtbChat.SuspendLayout();
            foreach (var (ts, text, color) in session)
                AppendEntryToRtb(ts, text, color);
            rtbChat.ResumeLayout();
        }

        // Cập nhật text hiển thị trong listbox (thêm/xóa badge chưa đọc)
        private void UpdateUserListItem(string username)
        {
            if (InvokeRequired) { Invoke(() => UpdateUserListItem(username)); return; }
            int idx = _userList.IndexOf(username);
            if (idx < 0) return;
            int unread = _unreadCounts.TryGetValue(username, out int n) ? n : 0;
            listBoxUsers.Items[idx] = unread > 0 ? $"● {username}  ({unread})" : username;
        }

        // ══════════════════════════════════════════════════════════════
        //  INCOMING MESSAGES (từ P2PListenerService)
        // ══════════════════════════════════════════════════════════════

        private void OnIncomingMessage(string sender, string message)
            => AddMessage(sender, $"{sender}: {message}", Color.FromArgb(30, 180, 100));

        private void OnIncomingFile(string sender, string fileName, string savePath)
            => AddMessage(sender, $"📁  {sender} gửi \"{fileName}\"  →  {savePath}",
                          Color.FromArgb(255, 180, 50));

        // ══════════════════════════════════════════════════════════════
        //  USER LIST — chọn + làm mới
        // ══════════════════════════════════════════════════════════════

        private async void listBoxUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inUserSelection) return;
            _inUserSelection = true;
            try { await HandleUserSelectionAsync(); }
            finally { _inUserSelection = false; }
        }

        private async Task HandleUserSelectionAsync()
        {
            int idx = listBoxUsers.SelectedIndex;
            if (idx < 0 || idx >= _userList.Count) return;

            string selected = _userList[idx];

            _p2pReady = false;
            btnCall.Visible = false;
            SetPeerInfo($"Đang kết nối tới {selected}...", clrConnecting);
            SetStatus($"Đang tra cứu {selected}...", Color.DodgerBlue);

            try
            {
                if (selected == "UitiChan")
                {
                    _peerIp   = "127.0.0.1";
                    _peerPort = 5555;
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
                        SetPeerInfo("Port không hợp lệ", clrDisconnected);
                        SetStatus("Port không hợp lệ.", Color.OrangeRed);
                        return;
                    }
                }

                _peerName  = selected;
                _isBotPeer = selected == "UitiChan";
                _p2pReady  = true;

                // Chuyển session chat
                _currentChatPeer    = selected;
                _unreadCounts[selected] = 0;
                UpdateUserListItem(selected);

                // Tải lịch sử từ DB nếu session chưa có trong RAM
                if (!_chatSessions.ContainsKey(selected))
                    LoadHistoryFromDb(selected);

                LoadSessionToRtb(selected);
                LoadPeerAvatar(selected);

                string modeLabel = _isBotPeer ? "Bot AI" : $"{_peerIp}:{_peerPort}";
                SetPeerInfo($"● {selected}  ({modeLabel})", clrConnected);
                SetStatus($"Sẵn sàng chat với {selected}", Color.SeaGreen);

                // Nút gọi hiện với cả bot và client
                btnCall.Visible = true;

                txtMessage.Focus();
            }
            catch (Exception ex)
            {
                SetPeerInfo("Lỗi kết nối", clrDisconnected);
                SetStatus($"Lỗi: {ex.Message}", Color.Crimson);
            }
        }

        private async void btnRefreshUsers_Click(object sender, EventArgs e)
        {
            try
            {
                int dirPort = await DirectoryService.GetDirectoryPortAsync();
                var users   = await DirectoryService.GetOnlineUsersAsync(dirPort);

                listBoxUsers.Items.Clear();
                _userList.Clear();
                foreach (string u in users)
                {
                    _userList.Add(u);
                    int unread = _unreadCounts.TryGetValue(u, out int n) ? n : 0;
                    listBoxUsers.Items.Add(unread > 0 ? $"● {u}  ({unread})" : u);
                }

                SetStatus($"{users.Count} user online  ({DateTime.Now:HH:mm:ss})", Color.SeaGreen);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi làm mới: {ex.Message}", Color.OrangeRed);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  GROUP MANAGEMENT
        // ══════════════════════════════════════════════════════════════

        private async Task LoadMyGroupsAsync()
        {
            try
            {
                var groups = await DirectoryService.GetMyGroupsAsync(_username);
                _myGroups.Clear();
                _myGroups.AddRange(groups);

                listBoxGroups.Items.Clear();
                foreach (var (id, name, _) in groups)
                    listBoxGroups.Items.Add($"{name}  [{id}]");
            }
            catch { }
        }

        private async void btnCreateGroup_Click(object sender, EventArgs e)
        {
            string groupName = ShowInputDialog("Tên nhóm:", "Tạo nhóm mới");
            if (string.IsNullOrWhiteSpace(groupName)) return;

            string groupId = DirectoryService.GenerateGroupId();
            SetStatus($"Đang tạo nhóm \"{groupName}\"...", Color.DodgerBlue);

            try
            {
                await DirectoryService.CreateGroupAsync(groupId, groupName.Trim(), _username);
                _myGroups.Add((groupId, groupName.Trim(), _username));
                listBoxGroups.Items.Add($"{groupName.Trim()}  [{groupId}]");
                SetStatus($"Đã tạo nhóm \"{groupName}\"  ID: {groupId}", Color.SeaGreen);
                OpenGroupChat(groupId, groupName.Trim(), _username);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tạo nhóm: {ex.Message}", Color.Crimson);
            }
        }

        private async void btnJoinGroup_Click(object sender, EventArgs e)
        {
            string groupId = ShowInputDialog("Nhập ID nhóm (6 ký tự):", "Tham gia nhóm");
            if (string.IsNullOrWhiteSpace(groupId)) return;

            groupId = groupId.Trim().ToUpperInvariant();
            SetStatus($"Đang tham gia nhóm [{groupId}]...", Color.DodgerBlue);

            try
            {
                var (success, groupName, members) = await DirectoryService.JoinGroupAsync(groupId, _username);

                if (!success)
                {
                    SetStatus($"Không tìm thấy nhóm [{groupId}].", Color.OrangeRed);
                    MessageBox.Show($"Không tìm thấy nhóm \"{groupId}\".", "Không tìm thấy nhóm",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!_myGroups.Exists(g => g.Id == groupId))
                {
                    _myGroups.Add((groupId, groupName, ""));
                    listBoxGroups.Items.Add($"{groupName}  [{groupId}]");
                }

                SetStatus($"Đã tham gia nhóm \"{groupName}\" ({members.Count} thành viên)", Color.SeaGreen);
                OpenGroupChat(groupId, groupName, "");
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tham gia nhóm: {ex.Message}", Color.Crimson);
            }
        }

        private void listBoxGroups_DoubleClick(object sender, EventArgs e)
        {
            int idx = listBoxGroups.SelectedIndex;
            if (idx < 0 || idx >= _myGroups.Count) return;
            var (groupId, groupName, creator) = _myGroups[idx];
            OpenGroupChat(groupId, groupName, creator);
        }

        private void OpenGroupChat(string groupId, string groupName, string creator)
        {
            if (_openGroupForms.TryGetValue(groupId, out GroupChatForm? existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                existing.Focus();
                return;
            }
            var form = new GroupChatForm(_username, groupId, groupName, creator);
            form.FormClosed += (_, _) => _openGroupForms.Remove(groupId);
            _openGroupForms[groupId] = form;
            form.Show(this);
        }

        // ══════════════════════════════════════════════════════════════
        //  CHAT CÁ NHÂN
        // ══════════════════════════════════════════════════════════════

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
                    SetStatus("Đang gửi...", Color.DodgerBlue);
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

            AppendChat($"Tôi:  📎 \"{fileName}\"  ({fileSize / 1024.0:F1} KB)", Color.FromArgb(80, 140, 220));

            SetButtonSend(false);
            btnSendFile.Enabled = false;

            try
            {
                var progress = new Progress<int>(pct =>
                    SetStatus($"Đang gửi \"{fileName}\"... {pct}%", Color.DodgerBlue));

                await P2PChatService.SendFileToClientAsync(_peerIp, _peerPort, _username, filePath, progress);
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

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                btnSend.PerformClick();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  VOICE CALL — 1:1
        // ══════════════════════════════════════════════════════════════

        private async void btnCall_Click(object? sender, EventArgs e)
        {
            if (!_p2pReady) return;
            if (_activecall != null)
            {
                SetStatus("Bạn đang trong một cuộc gọi khác.", Color.OrangeRed);
                return;
            }

            if (_isBotPeer)
            {
                await HandleBotCallAsync();
                return;
            }

            btnCall.Enabled = false;
            SetStatus($"Đang gọi {_peerName}...", Color.DodgerBlue);

            try
            {
                _activecall   = new VoiceCallService();
                _callPeer     = _peerName;
                _callPeerIp   = _peerIp;
                _callPeerPort = _peerPort;

                int localUdpPort = _activecall.PrepareUdp();
                await P2PChatService.SendVoiceSignalAsync(
                    _peerIp, _peerPort, $"VOICE_OFFER|{_username}|{localUdpPort}");

                ShowCallForm(_peerName, isOutgoing: true);
            }
            catch (Exception ex)
            {
                _activecall?.Stop();
                _activecall = null;
                SetStatus($"Lỗi gọi: {ex.Message}", Color.Crimson);
                btnCall.Enabled = true;
            }
        }

        // ── Voice call với Bot ────────────────────────────────────────
        private async Task HandleBotCallAsync()
        {
            btnCall.Enabled = false;
            SetStatus("Đang kết nối voice call với UitiChan...", Color.DodgerBlue);
            try
            {
                _activecall = new VoiceCallService();
                _callPeer   = "UitiChan";
                int localUdpPort = _activecall.PrepareUdp();

                // Kết nối TCP tới bot (port 5555)
                var botTcp    = new System.Net.Sockets.TcpClient();
                await botTcp.ConnectAsync("127.0.0.1", 5555);
                var botStream = botTcp.GetStream();
                var botWriter = new StreamWriter(botStream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
                var botReader = new StreamReader(botStream, System.Text.Encoding.UTF8);

                // Gửi VOICE_OFFER
                await botWriter.WriteLineAsync($"VOICE_OFFER|{_username}|{localUdpPort}");

                // Đọc VOICE_ACCEPT|botUdpPort
                string? acceptLine = await botReader.ReadLineAsync();
                if (acceptLine == null || !acceptLine.StartsWith("VOICE_ACCEPT|"))
                {
                    SetStatus("Bot không chấp nhận cuộc gọi.", Color.OrangeRed);
                    _activecall.Stop(); _activecall = null;
                    botTcp.Close(); btnCall.Enabled = true;
                    return;
                }

                int botUdpPort = int.Parse(acceptLine.Split('|')[1]);
                _activecall.SetRemoteEndpoint("127.0.0.1", botUdpPort);

                // Lưu lại để cleanup khi hang up
                _botCallTcp    = botTcp;
                _botCallWriter = botWriter;

                // Hiển thị form với subtitle panel
                ShowCallForm("UitiChan", isOutgoing: false, isBotCall: true);
                _activecall.StartAudio();

                // Background: đọc VOICE_CAPTION từ bot TCP và hiển thị subtitle
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            string? line = await botReader.ReadLineAsync();
                            if (line == null) break;
                            if (line.StartsWith("VOICE_CAPTION|"))
                                _callForm?.AddSubtitle("UitiChan", line[14..]);
                            else if (line.StartsWith("VOICE_CAPTION_USER|"))
                                _callForm?.AddSubtitle(_username, line[19..]);
                        }
                    }
                    catch { }
                    finally
                    {
                        _botCallTcp?.Close();
                        _botCallTcp    = null;
                        _botCallWriter = null;
                    }
                });

                SetStatus("Đang gọi UitiChan ♪", Color.SeaGreen);
            }
            catch (Exception ex)
            {
                _activecall?.Stop();
                _activecall    = null;
                _botCallTcp?.Close();
                _botCallTcp    = null;
                _botCallWriter = null;
                SetStatus($"Lỗi bot call: {ex.Message}", Color.Crimson);
                btnCall.Enabled = true;
            }
        }

        // Nhận VOICE_OFFER — fires on background thread
        private void OnIncomingVoiceCall(string callerName, string callerUdpPortStr)
        {
            if (InvokeRequired)
            {
                BeginInvoke(async () => await HandleIncomingCallAsync(callerName, callerUdpPortStr));
                return;
            }
            _ = HandleIncomingCallAsync(callerName, callerUdpPortStr);
        }

        private async Task HandleIncomingCallAsync(string callerName, string callerUdpPortStr)
        {
            var answer = MessageBox.Show(
                $"📞  Cuộc gọi thoại từ  {callerName}\n\nBạn có muốn trả lời không?",
                "Cuộc gọi đến",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            // Tìm IP:Port TCP của caller để gửi answer/reject về
            int dirPort = await DirectoryService.GetDirectoryPortAsync();
            var (found, ipPort) = await DirectoryService.GetUserAsync(dirPort, callerName);
            if (!found) { SetStatus("Không tìm thấy IP người gọi.", Color.OrangeRed); return; }

            string[] pp = ipPort.Split(':');
            if (pp.Length != 2 || !int.TryParse(pp[1], out int callerTcpPort)) return;
            string callerIp = pp[0];

            if (answer != DialogResult.Yes)
            {
                await P2PChatService.SendVoiceSignalAsync(callerIp, callerTcpPort,
                    $"VOICE_REJECT|{_username}");
                return;
            }

            if (_activecall != null)
            {
                await P2PChatService.SendVoiceSignalAsync(callerIp, callerTcpPort,
                    $"VOICE_REJECT|{_username}");
                SetStatus("Đang bận, không thể nhận cuộc gọi.", Color.OrangeRed);
                return;
            }

            if (!int.TryParse(callerUdpPortStr, out int callerUdpPort))
            {
                SetStatus("Signaling: UDP port không hợp lệ.", Color.Crimson);
                return;
            }

            try
            {
                _activecall   = new VoiceCallService();
                _callPeer     = callerName;
                _callPeerIp   = callerIp;
                _callPeerPort = callerTcpPort;

                int localUdpPort = _activecall.PrepareUdp();
                _activecall.SetRemoteEndpoint(callerIp, callerUdpPort);

                await P2PChatService.SendVoiceSignalAsync(callerIp, callerTcpPort,
                    $"VOICE_ANSWER|{_username}|{localUdpPort}");

                // Hiển thị form TRƯỚC khi StartAudio để form kịp subscribe CallConnected
                ShowCallForm(callerName, isOutgoing: false);
                _activecall.StartAudio();
            }
            catch (Exception ex)
            {
                _activecall?.Stop();
                _activecall = null;
                SetStatus($"Lỗi trả lời cuộc gọi: {ex.Message}", Color.Crimson);
            }
        }

        // Nhận VOICE_ANSWER — caller nhận UDP port của callee → SetRemoteEndpoint → StartAudio
        private void OnVoiceCallAnswered(string peerName, string answererUdpPortStr)
        {
            if (_activecall == null || peerName != _callPeer) return;
            try
            {
                if (!int.TryParse(answererUdpPortStr, out int answererUdpPort))
                {
                    if (InvokeRequired) Invoke(() => SetStatus("Signaling: UDP port không hợp lệ.", Color.Crimson));
                    else SetStatus("Signaling: UDP port không hợp lệ.", Color.Crimson);
                    return;
                }
                _activecall.SetRemoteEndpoint(_callPeerIp, answererUdpPort);
                _activecall.StartAudio();
            }
            catch (Exception ex)
            {
                if (InvokeRequired) Invoke(() => SetStatus($"Lỗi kết nối voice: {ex.Message}", Color.Crimson));
                else SetStatus($"Lỗi kết nối voice: {ex.Message}", Color.Crimson);
            }
        }

        private void OnVoiceCallRejected(string peerName)
        {
            if (InvokeRequired) { Invoke(() => OnVoiceCallRejected(peerName)); return; }
            _callForm?.Close();
            _callForm = null;
            _activecall?.Stop();
            _activecall     = null;
            btnCall.Enabled = _p2pReady;
            SetStatus($"{peerName} từ chối cuộc gọi.", Color.OrangeRed);
        }

        private void OnVoiceCallHungUp(string peerName)
        {
            if (InvokeRequired) { Invoke(() => OnVoiceCallHungUp(peerName)); return; }
            _callForm?.Close();
            _callForm = null;
            _activecall?.Stop();
            _activecall     = null;
            btnCall.Enabled = _p2pReady;
            SetStatus($"Cuộc gọi với {peerName} đã kết thúc.", Color.OrangeRed);
        }

        private void ShowCallForm(string peerName, bool isOutgoing, bool isBotCall = false)
        {
            if (_callForm != null && !_callForm.IsDisposed)
            {
                _callForm.BringToFront();
                return;
            }

            string initialStatus = isOutgoing ? "Đang gọi..." : "Đang kết nối...";
            _callForm = new VoiceCallForm(peerName, _activecall!, initialStatus, isBotCall);

            _callForm.HangupRequested += async _ =>
            {
                if (isBotCall)
                {
                    // Bot: gửi VOICE_HANGUP qua TCP connection đang mở
                    if (_botCallWriter != null)
                    {
                        try { await _botCallWriter.WriteLineAsync("VOICE_HANGUP"); } catch { }
                        _botCallTcp?.Close();
                        _botCallTcp    = null;
                        _botCallWriter = null;
                    }
                }
                else if (!string.IsNullOrEmpty(_callPeerIp) && _callPeerPort != 0)
                {
                    await P2PChatService.SendVoiceSignalAsync(
                        _callPeerIp, _callPeerPort, $"VOICE_HANGUP|{_username}");
                }

                _activecall?.Stop();
                _activecall = null;
                _callForm   = null;
                Invoke(() =>
                {
                    btnCall.Enabled = _p2pReady;
                    SetStatus("Cuộc gọi đã kết thúc.", Color.SeaGreen);
                });
            };

            _callForm.FormClosed += (_, _) => { _callForm = null; };
            _callForm.Show(this);
        }

        // ══════════════════════════════════════════════════════════════
        //  FORM LIFECYCLE
        // ══════════════════════════════════════════════════════════════

        private async void MainChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Hủy đăng ký events
            P2PListenerService.MessageReceived   -= OnIncomingMessage;
            P2PListenerService.FileReceived      -= OnIncomingFile;
            P2PListenerService.IncomingVoiceCall -= OnIncomingVoiceCall;
            P2PListenerService.VoiceCallAnswered -= OnVoiceCallAnswered;
            P2PListenerService.VoiceCallRejected -= OnVoiceCallRejected;
            P2PListenerService.VoiceCallHungUp   -= OnVoiceCallHungUp;

            // Kết thúc cuộc gọi đang active (nếu có)
            if (_activecall != null)
            {
                if (!string.IsNullOrEmpty(_callPeerIp) && _callPeerPort != 0)
                    await P2PChatService.SendVoiceSignalAsync(
                        _callPeerIp, _callPeerPort, $"VOICE_HANGUP|{_username}");
                _activecall.Stop();
            }

            // Đóng tất cả group forms
            foreach (var form in _openGroupForms.Values)
                if (!form.IsDisposed) form.Close();

            // Đăng xuất
            if (_dirPort != 0)
                await DirectoryService.LogoutAsync(_dirPort, _username);
        }

        // ══════════════════════════════════════════════════════════════
        //  AUDIO + HELPERS
        // ══════════════════════════════════════════════════════════════

        private void PlayAudio(byte[] wavBytes)
        {
            try
            {
                using MemoryStream ms     = new(wavBytes);
                using SoundPlayer  player = new(ms);
                player.Play();
            }
            catch { }
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

        // Input dialog nhỏ gọn
        private static string ShowInputDialog(string prompt, string title)
        {
            using Form dialog = new()
            {
                Text            = title,
                ClientSize      = new Size(360, 120),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterParent,
                MaximizeBox     = false,
                MinimizeBox     = false,
                BackColor       = Color.FromArgb(24, 24, 34)
            };

            var lbl = new Label
            {
                Text      = prompt,
                Location  = new Point(12, 16),
                Size      = new Size(336, 18),
                ForeColor = Color.FromArgb(200, 200, 220),
                Font      = new Font("Segoe UI", 9F)
            };
            var txt = new TextBox
            {
                Location    = new Point(12, 38),
                Size        = new Size(336, 28),
                BackColor   = Color.FromArgb(38, 38, 52),
                ForeColor   = Color.FromArgb(220, 220, 230),
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Segoe UI", 10F)
            };
            var btnOk = new Button
            {
                Text         = "OK",
                DialogResult = DialogResult.OK,
                Location     = new Point(180, 80),
                Size         = new Size(80, 26),
                BackColor    = Color.FromArgb(0, 120, 212),
                ForeColor    = Color.White,
                FlatStyle    = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            var btnCancel = new Button
            {
                Text         = "Hủy",
                DialogResult = DialogResult.Cancel,
                Location     = new Point(268, 80),
                Size         = new Size(80, 26),
                BackColor    = Color.FromArgb(50, 50, 70),
                ForeColor    = Color.FromArgb(200, 200, 220),
                FlatStyle    = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            dialog.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancel;

            return dialog.ShowDialog() == DialogResult.OK ? txt.Text : string.Empty;
        }

        // Màu cho lblPeerInfo
        private static readonly Color clrConnected    = Color.FromArgb(0,  200, 120);
        private static readonly Color clrConnecting   = Color.FromArgb(200, 180,  60);
        private static readonly Color clrDisconnected = Color.FromArgb(200,  70,  70);
    }
}
