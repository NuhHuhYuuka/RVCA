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
        private DatabaseService? _db;

        // Màu dùng để nhận biết loại tin khi save/load DB
        private static readonly int _clrSentByMe = Color.FromArgb(80, 140, 220).ToArgb();
        private static readonly int _clrFile      = Color.FromArgb(255, 180, 50).ToArgb();
        private static readonly int _clrCrimson   = Color.Crimson.ToArgb();

        // ── Typing indicator ─────────────────────────────────────────
        private DateTime _lastTypingSent = DateTime.MinValue;
        private System.Threading.Timer? _peerTypingClearTimer;

        // ── Voice call ────────────────────────────────────────────────
        private VoiceCallService? _activecall;
        private VoiceCallForm?    _callForm;
        private string _callPeer    = "";
        private string _callPeerIp  = "";
        private int    _callPeerPort = 0;

        // ── Video call ────────────────────────────────────────────────
        private VideoCallService?    _activeVideoCall;
        private VideoCallForm?       _videoCallForm;
        private VideoCaptureService? _videoCaptureService;
        private string _videoCallPeer    = "";
        private string _videoCallPeerIp  = "";
        private int    _videoCallPeerPort = 0;

        // Bot voice call — giữ TCP connection mở để nhận VOICE_CAPTION
        private System.Net.Sockets.TcpClient? _botCallTcp    = null;
        private StreamWriter?                  _botCallWriter = null;

        // ── Groups ────────────────────────────────────────────────────
        private readonly List<(string Id, string Name, string Creator)> _myGroups = new();
        private readonly Dictionary<string, GroupChatForm>   _openGroupForms = new();
        private Panel? _pnlGroupHost;         // Panel nhúng GroupChatForm vào pnlRight
        private bool   _suppressUserSelection; // Tránh re-entrancy khi ClearSelected

        // ── Hàng đợi tin nhắn offline (gửi lại khi peer online) ──────
        private readonly Dictionary<string, List<string>> _offlineQueue = new();

        // ─────────────────────────────────────────────────────────────
        public MainChatForm(string username, int dirPort, List<string> onlineUsers)
        {
            _username = username;
            _dirPort  = dirPort;

            try { _db = new DatabaseService(); } catch { /* SQLite unavailable — history disabled */ }

            InitializeComponent();

            foreach (string u in onlineUsers)
            {
                _userList.Add(u);
                listBoxUsers.Items.Add(u);
            }

            // P2P events
            P2PListenerService.MessageReceived += OnIncomingMessage;
            P2PListenerService.FileReceived    += OnIncomingFile;

            // Avatar exchange
            P2PListenerService.AvatarReceived    += OnAvatarReceived;

            // Typing + voice signaling events
            P2PListenerService.TypingReceived    += OnPeerTyping;
            P2PListenerService.IncomingVoiceCall += OnIncomingVoiceCall;
            P2PListenerService.VoiceCallAnswered += OnVoiceCallAnswered;
            P2PListenerService.VoiceCallRejected += OnVoiceCallRejected;
            P2PListenerService.VoiceCallHungUp   += OnVoiceCallHungUp;

            // Video call signaling events
            P2PListenerService.IncomingVideoCall += OnIncomingVideoCall;
            P2PListenerService.VideoCallAnswered += OnVideoCallAnswered;
            P2PListenerService.VideoCallRejected += OnVideoCallRejected;
            P2PListenerService.VideoCallHungUp   += OnVideoCallHungUp;
        }

        private async void MainChatForm_Load(object sender, EventArgs e)
        {
            this.Text = $"Uiti-chan Chat  –  {_username}  (port {P2PListenerService.ListeningPort})";

            lblMyUsername.Text = _username;

            // Peer avatar mặc định
            picPeerAvatar.Image = AvatarService.CreateInitialsBitmap("?", 34);

            // Panel nhúng GroupChatForm — Dock=Fill, ẩn mặc định
            _pnlGroupHost = new Panel
            {
                Dock      = DockStyle.Fill,
                Visible   = false,
                BackColor = Color.FromArgb(20, 20, 30)
            };
            pnlRight.Controls.Add(_pnlGroupHost);

            // ApplyCircularClip gọi sau Shown — lúc đó control đã có kích thước thật
            this.Shown += OnFirstShown;

            txtMessage.Focus();
            await LoadMyGroupsAsync();

            // Khám phá external IP qua STUN (fire-and-forget, không chặn load)
            _ = Task.Run(async () =>
            {
                try
                {
                    var (ip, port) = await StunService.GetExternalEndpointAsync();
                    this.Invoke(() =>
                        this.Text = $"Uiti-chan Chat  –  {_username}  (LAN:{P2PListenerService.ListeningPort}  WAN:{ip}:{port})");
                }
                catch { /* Không có internet hoặc STUN bị block — bỏ qua */ }
            });

            // Bắt đầu relay poller — nhận message/signaling qua server khi P2P bị NAT chặn
            RelayPollerService.Start(_username);
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
                // Thử load từ cache local (đã nhận qua AVATAR_PUSH) trước khi dùng initials
                bmp = AvatarService.LoadBitmap(AvatarService.GetUserAvatarPath(peerName))
                      ?? AvatarService.CreateInitialsBitmap(peerName, 34);

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
                _ = BroadcastAvatarToAllPeersAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi cập nhật avatar: {ex.Message}", Color.Crimson);
            }
        }

        // Gửi avatar mới tới tất cả user đang online (fire-and-forget)
        private async Task BroadcastAvatarToAllPeersAsync()
        {
            var targets = _userList.ToList();
            int sent = 0;
            foreach (string user in targets)
            {
                if (user == _username || user == "UitiChan") continue;
                try
                {
                    int dirPort = await DirectoryService.GetDirectoryPortAsync();
                    var (found, ipPort) = await DirectoryService.GetUserAsync(dirPort, user);
                    if (!found || string.IsNullOrEmpty(ipPort)) continue;

                    string[] parts = ipPort.Split(':');
                    if (parts.Length < 2 || !int.TryParse(parts[1], out int port)) continue;

                    await P2PChatService.SendAvatarAsync(parts[0], port, _username);
                    sent++;
                }
                catch { /* user offline hoặc lỗi mạng — bỏ qua */ }
            }
            _ = sent; // fire-and-forget, không cần thông báo thêm
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
                    _db?.SaveMessage(new ChatMessage
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
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
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
                if (_db == null) return;
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

        // Tải toàn bộ session của peer vào RTB (kể cả ảnh đã nhận)
        private void LoadSessionToRtb(string peer)
        {
            rtbChat.Clear();
            if (!_chatSessions.TryGetValue(peer, out var session)) return;
            rtbChat.SuspendLayout();
            foreach (var (ts, text, color) in session)
            {
                if (text.StartsWith("[IMAGE:") && text.EndsWith("]"))
                {
                    string imgPath = text[7..^1];
                    if (File.Exists(imgPath)) AppendImageToRtb(imgPath);
                }
                else
                    AppendEntryToRtb(ts, text, color);
            }
            rtbChat.ResumeLayout();
        }

        // Cập nhật badge chưa đọc — với OwnerDraw chỉ cần invalidate để redraw
        private void UpdateUserListItem(string username)
        {
            if (InvokeRequired) { Invoke(() => UpdateUserListItem(username)); return; }
            int idx = _userList.IndexOf(username);
            if (idx < 0) return;
            var rect = listBoxUsers.GetItemRectangle(idx);
            listBoxUsers.Invalidate(rect);
        }

        // OwnerDraw: vẽ chấm xanh status + tên user + badge chưa đọc
        private void listBoxUsers_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _userList.Count) return;

            e.DrawBackground();

            string username = _userList[e.Index];
            _unreadCounts.TryGetValue(username, out int unread);

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Chấm xanh — tất cả user trong list đều online
            const int dotSize = 9;
            int dotX = e.Bounds.X + 10;
            int dotY = e.Bounds.Y + (e.Bounds.Height - dotSize) / 2;
            using var dotBrush = new SolidBrush(Color.FromArgb(0, 200, 100));
            g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);

            // Tên user
            string displayText = unread > 0 ? $"{username}  ({unread})" : username;
            var textColor = unread > 0 ? Color.White : e.ForeColor;
            using var textBrush = new SolidBrush(textColor);
            var textRect = new Rectangle(e.Bounds.X + 26, e.Bounds.Y, e.Bounds.Width - 26, e.Bounds.Height);
            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            g.DrawString(displayText, e.Font!, textBrush, textRect, sf);

            e.DrawFocusRectangle();
        }

        // ══════════════════════════════════════════════════════════════
        //  INCOMING MESSAGES (từ P2PListenerService)
        // ══════════════════════════════════════════════════════════════

        // Nhận avatar từ peer → reload nếu đang xem peer đó
        private void OnAvatarReceived(string username)
        {
            if (username != _currentChatPeer) return;
            if (InvokeRequired) { Invoke(() => OnAvatarReceived(username)); return; }
            LoadPeerAvatar(username);
        }

        private void OnIncomingMessage(string sender, string message)
            => AddMessage(sender, $"{sender}: {message}", Color.FromArgb(30, 180, 100));

        private void OnIncomingFile(string sender, string fileName, string savePath)
        {
            AddMessage(sender, $"📁  {sender} gửi \"{fileName}\"  →  {savePath}",
                       Color.FromArgb(255, 180, 50));
            if (IsImageFile(fileName))
                AddImageToSession(sender, savePath);
        }

        // Lưu đường dẫn ảnh vào session và render ngay nếu đang xem peer đó
        private void AddImageToSession(string peer, string imagePath)
        {
            if (!_chatSessions.TryGetValue(peer, out var session))
            {
                session = new List<(string, string, Color)>();
                _chatSessions[peer] = session;
            }
            // Marker đặc biệt — LoadSessionToRtb sẽ detect và render ảnh
            session.Add(("", $"[IMAGE:{imagePath}]", Color.Transparent));

            if (peer == _currentChatPeer)
            {
                if (InvokeRequired) Invoke(() => AppendImageToRtb(imagePath));
                else AppendImageToRtb(imagePath);
            }
        }

        private static bool IsImageFile(string name)
        {
            string ext = Path.GetExtension(name).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
        }

        // Nhúng ảnh trực tiếp vào RTF bằng \pngblip — không dùng Clipboard để tránh race condition
        private void AppendImageToRtb(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath)) return;

                byte[] raw = File.ReadAllBytes(imagePath);
                using var ms   = new MemoryStream(raw);
                using var orig = Image.FromStream(ms);

                const int MaxW = 300;
                int w = Math.Min(orig.Width, MaxW);
                int h = orig.Width > 0 ? (int)(orig.Height * ((double)w / orig.Width)) : 120;
                if (w <= 0 || h <= 0) return;

                // Tạo thumbnail
                using var thumb = new Bitmap(w, h);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(orig, 0, 0, w, h);
                }

                // Encode sang PNG bytes rồi hex — nhúng thẳng vào RTF \pngblip
                using var pngMs = new MemoryStream();
                thumb.Save(pngMs, System.Drawing.Imaging.ImageFormat.Png);
                string hex = Convert.ToHexString(pngMs.ToArray());

                // \picwgoal / \pichgoal tính theo twips (1 px ≈ 15 twips ở 96 DPI)
                int wTwips = w * 15;
                int hTwips = h * 15;
                string rtf = $@"{{\rtf1 {{\pict\pngblip\picwgoal{wTwips}\pichgoal{hTwips} {hex}}}\par}}";

                bool wasReadOnly = rtbChat.ReadOnly;
                rtbChat.ReadOnly = false;
                rtbChat.SelectionStart = rtbChat.TextLength;
                rtbChat.SelectedRtf = rtf;
                rtbChat.ReadOnly = wasReadOnly;
                rtbChat.ScrollToCaret();
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════
        //  USER LIST — chọn + làm mới
        // ══════════════════════════════════════════════════════════════

        private async void listBoxUsers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inUserSelection || _suppressUserSelection) return;
            _inUserSelection = true;
            try { await HandleUserSelectionAsync(); }
            finally { _inUserSelection = false; }
        }

        private async Task HandleUserSelectionAsync()
        {
            int idx = listBoxUsers.SelectedIndex;
            if (idx < 0 || idx >= _userList.Count) return;

            DeactivateGroupHost(); // ẩn group chat khi chuyển sang peer

            string selected = _userList[idx];

            _p2pReady = false;
            btnCall.Visible = false;
            SetPeerInfo($"Đang kết nối tới {selected}...", clrConnecting);
            SetStatus($"Đang tra cứu {selected}...", Color.DodgerBlue);

            try
            {
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

                // Gửi lại tin nhắn đã lưu khi peer offline
                if (!_isBotPeer && _offlineQueue.TryGetValue(selected, out var pending) && pending.Count > 0)
                    _ = DrainOfflineQueueAsync(selected);

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

                // Gửi avatar của mình tới peer ngầm (không chờ kết quả)
                if (!_isBotPeer)
                    _ = P2PChatService.SendAvatarAsync(_peerIp, _peerPort, _username);

                // Không cho phép tự gọi cho chính mình
                bool isSelf = selected == _username;
                btnCall.Visible      = !isSelf;
                btnVideoCall.Visible = !isSelf && !_isBotPeer;

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
                    listBoxUsers.Items.Add(u);   // OwnerDraw tự vẽ badge
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
                var (success, groupName, creator, members) = await DirectoryService.JoinGroupAsync(groupId, _username);

                if (!success)
                {
                    SetStatus($"Không tìm thấy nhóm [{groupId}].", Color.OrangeRed);
                    MessageBox.Show($"Không tìm thấy nhóm \"{groupId}\".", "Không tìm thấy nhóm",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!_myGroups.Exists(g => g.Id == groupId))
                {
                    _myGroups.Add((groupId, groupName, creator));
                    listBoxGroups.Items.Add($"{groupName}  [{groupId}]");
                }

                SetStatus($"Đã tham gia nhóm \"{groupName}\" ({members.Count} thành viên)", Color.SeaGreen);
                OpenGroupChat(groupId, groupName, creator);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tham gia nhóm: {ex.Message}", Color.Crimson);
            }
        }

        private void listBoxGroups_Click(object sender, EventArgs e)
        {
            int idx = listBoxGroups.SelectedIndex;
            if (idx < 0 || idx >= _myGroups.Count) return;
            var (groupId, groupName, creator) = _myGroups[idx];
            OpenGroupChat(groupId, groupName, creator);
        }

        private void OpenGroupChat(string groupId, string groupName, string creator)
        {
            if (_pnlGroupHost == null) return;

            // Group này đang mở → chỉ activate lại (không tạo mới)
            if (_openGroupForms.TryGetValue(groupId, out GroupChatForm? existing) && !existing.IsDisposed)
            {
                ActivateGroupHost();
                return;
            }

            // Đóng group cũ (chỉ mở 1 group tại 1 thời điểm)
            foreach (var old in _openGroupForms.Values.ToList())
                if (!old.IsDisposed) old.Close();
            _openGroupForms.Clear();
            _pnlGroupHost.Controls.Clear();

            // Nhúng GroupChatForm vào pnlRight thay vì mở floating window
            var form = new GroupChatForm(_username, groupId, groupName, creator);
            form.TopLevel         = false;
            form.FormBorderStyle  = FormBorderStyle.None;
            form.Dock             = DockStyle.Fill;
            form.FormClosed += (_, _) =>
            {
                _openGroupForms.Remove(groupId);
                DeactivateGroupHost();
            };

            _openGroupForms[groupId] = form;
            _pnlGroupHost.Controls.Add(form);
            form.Show();
            ActivateGroupHost();
        }

        private void ActivateGroupHost()
        {
            if (_pnlGroupHost == null) return;
            // Deselect user list (dùng flag để tránh trigger HandleUserSelectionAsync)
            _suppressUserSelection = true;
            listBoxUsers.ClearSelected();
            _suppressUserSelection = false;
            // Reset trạng thái peer
            _p2pReady            = false;
            _peerName            = "";
            btnCall.Visible      = false;
            btnVideoCall.Visible = false;
            // Ẩn P2P chat, hiện group host
            rtbChat.Visible      = false;
            pnlBottom.Visible    = false;
            _pnlGroupHost.Visible = true;
        }

        private void DeactivateGroupHost()
        {
            if (_pnlGroupHost == null || !_pnlGroupHost.Visible) return;
            _pnlGroupHost.Visible = false;
            rtbChat.Visible       = true;
            pnlBottom.Visible     = true;
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
                    await P2PChatService.SendToClientWithRelayAsync(_peerIp, _peerPort, _username, _peerName, message);
                    SetStatus("Đã gửi", Color.SeaGreen);
                }
            }
            catch (Exception)
            {
                if (!_isBotPeer)
                {
                    if (!_offlineQueue.ContainsKey(_peerName))
                        _offlineQueue[_peerName] = new List<string>();
                    _offlineQueue[_peerName].Add(message);
                    AppendChat($"↻  Tin nhắn đã lưu, sẽ gửi khi {_peerName} online.", Color.FromArgb(180, 130, 50));
                    SetStatus($"{_peerName} không online — tin nhắn đã lưu hàng đợi.", Color.OrangeRed);
                }
                else
                {
                    AppendChat("[Lỗi gửi] Bot không phản hồi.", Color.Crimson);
                    SetStatus("Bot offline hoặc không phản hồi.", Color.OrangeRed);
                }
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

            if (IsImageFile(fileName))
                AddImageToSession(_currentChatPeer, filePath);

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

        // Gửi lại tin nhắn đã lưu khi peer online trở lại
        private async Task DrainOfflineQueueAsync(string peerName)
        {
            if (!_offlineQueue.TryGetValue(peerName, out var queue) || queue.Count == 0) return;

            var toSend = queue.ToList();
            queue.Clear();

            int sent = 0;
            foreach (string msg in toSend)
            {
                try
                {
                    await P2PChatService.SendToClientAsync(_peerIp, _peerPort, _username, msg);
                    sent++;
                }
                catch
                {
                    queue.AddRange(toSend.Skip(sent));
                    break;
                }
            }

            if (sent > 0 && !IsDisposed)
                Invoke(() => SetStatus($"Đã gửi lại {sent} tin nhắn đã lưu tới {peerName}", Color.SeaGreen));
        }

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                btnSend.PerformClick();
            }
        }

        private void txtMessage_TextChanged(object? sender, EventArgs e)
        {
            if (!_p2pReady || _isBotPeer || string.IsNullOrEmpty(_peerIp)) return;
            var now = DateTime.UtcNow;
            if ((now - _lastTypingSent).TotalSeconds < 2) return;
            _lastTypingSent = now;
            _ = P2PChatService.SendVoiceSignalAsync(_peerIp, _peerPort, $"TYPING|{_username}");
        }

        private void OnPeerTyping(string senderName)
        {
            if (senderName != _currentChatPeer) return;
            if (InvokeRequired) { Invoke(() => OnPeerTyping(senderName)); return; }

            SetStatus($"{senderName} đang nhập...", Color.FromArgb(100, 210, 100));
            _peerTypingClearTimer?.Dispose();
            _peerTypingClearTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (IsDisposed) return;
                    Invoke(() =>
                    {
                        if (lblStatus.Text.EndsWith("đang nhập..."))
                            SetStatus(_p2pReady ? $"Sẵn sàng chat với {_currentChatPeer}" : "Chờ kết nối P2P...",
                                      Color.FromArgb(160, 160, 180));
                    });
                }
                catch { }
            }, null, 3000, System.Threading.Timeout.Infinite);
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
                await P2PChatService.SendVoiceSignalWithRelayAsync(
                    _peerIp, _peerPort,
                    $"VOICE_OFFER|{_username}|{localUdpPort}|{P2PListenerService.ListeningPort}",
                    _username, _peerName);

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

                // Kết nối TCP tới bot (dùng IP/port từ Directory Server)
                var botTcp    = new System.Net.Sockets.TcpClient();
                await botTcp.ConnectAsync(_peerIp, _peerPort);
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
                _activecall.SetRemoteEndpoint(_peerIp, botUdpPort);

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
        private void OnIncomingVoiceCall(string callerName, string callerUdpPortStr, string callerIp, int callerTcpPort)
        {
            if (InvokeRequired)
            {
                BeginInvoke(async () => await HandleIncomingCallAsync(callerName, callerUdpPortStr, callerIp, callerTcpPort));
                return;
            }
            _ = HandleIncomingCallAsync(callerName, callerUdpPortStr, callerIp, callerTcpPort);
        }

        private async Task HandleIncomingCallAsync(string callerName, string callerUdpPortStr, string callerIp, int callerTcpPort)
        {
            var answer = MessageBox.Show(
                $"📞  Cuộc gọi thoại từ  {callerName}\n\nBạn có muốn trả lời không?",
                "Cuộc gọi đến",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                await P2PChatService.SendVoiceSignalWithRelayAsync(callerIp, callerTcpPort,
                    $"VOICE_REJECT|{_username}", _username, callerName);
                return;
            }

            if (_activecall != null)
            {
                await P2PChatService.SendVoiceSignalWithRelayAsync(callerIp, callerTcpPort,
                    $"VOICE_REJECT|{_username}", _username, callerName);
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

                await P2PChatService.SendVoiceSignalWithRelayAsync(callerIp, callerTcpPort,
                    $"VOICE_ANSWER|{_username}|{localUdpPort}", _username, callerName);

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
                    await P2PChatService.SendVoiceSignalWithRelayAsync(
                        _callPeerIp, _callPeerPort, $"VOICE_HANGUP|{_username}", _username, _callPeer);
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
        //  VIDEO CALL — 1:1 (kể cả Bot VTuber mode)
        // ══════════════════════════════════════════════════════════════

        private async void btnVideoCall_Click(object? sender, EventArgs e)
        {
            if (!_p2pReady) return;
            if (_activeVideoCall != null)
            {
                SetStatus("Bạn đang trong một video call khác.", Color.OrangeRed);
                return;
            }

            if (_isBotPeer)
            {
                await HandleBotVideoCallAsync();
                return;
            }

            btnVideoCall.Enabled = false;
            SetStatus($"Đang gọi video cho {_peerName}...", Color.DodgerBlue);

            try
            {
                _activeVideoCall  = new VideoCallService();
                _videoCallPeer    = _peerName;
                _videoCallPeerIp  = _peerIp;
                _videoCallPeerPort = _peerPort;

                _activeVideoCall.Prepare();

                _videoCaptureService = TryStartCamera();

                await P2PChatService.SendVoiceSignalWithRelayAsync(_peerIp, _peerPort,
                    $"VIDEO_OFFER|{_username}|{_activeVideoCall.AudioLocalPort}|{_activeVideoCall.VideoLocalPort}|{P2PListenerService.ListeningPort}",
                    _username, _peerName);

                ShowVideoCallForm(_peerName, isOutgoing: true);
            }
            catch (Exception ex)
            {
                _activeVideoCall?.Stop();
                _activeVideoCall = null;
                _videoCaptureService?.Dispose();
                _videoCaptureService = null;
                SetStatus($"Lỗi video call: {ex.Message}", Color.Crimson);
                btnVideoCall.Enabled = true;
            }
        }

        // ── Video call với Bot (VTuber mode) ─────────────────────────
        private async Task HandleBotVideoCallAsync()
        {
            btnVideoCall.Enabled = false;
            SetStatus("Đang kết nối video call với UitiChan (VTuber mode)...", Color.DodgerBlue);
            try
            {
                _activeVideoCall = new VideoCallService();
                _videoCallPeer   = "UitiChan";
                _activeVideoCall.Prepare();

                _videoCaptureService = TryStartCamera();

                var botTcp    = new System.Net.Sockets.TcpClient();
                await botTcp.ConnectAsync(_peerIp, _peerPort);
                var botStream = botTcp.GetStream();
                var botWriter = new StreamWriter(botStream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
                var botReader = new StreamReader(botStream, System.Text.Encoding.UTF8);

                await botWriter.WriteLineAsync(
                    $"VIDEO_OFFER|{_username}|{_activeVideoCall.AudioLocalPort}|{_activeVideoCall.VideoLocalPort}");

                string? acceptLine = await botReader.ReadLineAsync();
                if (acceptLine == null || !acceptLine.StartsWith("VIDEO_ACCEPT|"))
                {
                    SetStatus("Bot không chấp nhận video call.", Color.OrangeRed);
                    _activeVideoCall.Stop(); _activeVideoCall = null;
                    _videoCaptureService?.Dispose(); _videoCaptureService = null;
                    botTcp.Close(); btnVideoCall.Enabled = true;
                    return;
                }

                string[] ap = acceptLine.Split('|');
                if (ap.Length < 3 || !int.TryParse(ap[1], out int botAudio) || !int.TryParse(ap[2], out int botVideo))
                {
                    SetStatus("Bot trả signaling không hợp lệ.", Color.Crimson);
                    _activeVideoCall.Stop(); _activeVideoCall = null;
                    _videoCaptureService?.Dispose(); _videoCaptureService = null;
                    botTcp.Close(); btnVideoCall.Enabled = true;
                    return;
                }

                _activeVideoCall.SetRemoteEndpoint(_peerIp, botAudio, botVideo);
                _botCallTcp    = botTcp;
                _botCallWriter = botWriter;

                ShowVideoCallForm("UitiChan", isOutgoing: false);
                _activeVideoCall.Start();

                // Background: đọc VOICE_CAPTION từ bot TCP
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (true)
                        {
                            string? line = await botReader.ReadLineAsync();
                            if (line == null) break;
                            if (line.StartsWith("VOICE_CAPTION|"))
                                _videoCallForm?.AddCaption("UitiChan", line[14..]);
                            else if (line.StartsWith("VOICE_CAPTION_USER|"))
                                _videoCallForm?.AddCaption(_username, line[19..]);
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

                SetStatus("Đang video call UitiChan ♪", Color.SeaGreen);
            }
            catch (Exception ex)
            {
                _activeVideoCall?.Stop();
                _activeVideoCall = null;
                _videoCaptureService?.Dispose();
                _videoCaptureService = null;
                _botCallTcp?.Close();
                _botCallTcp    = null;
                _botCallWriter = null;
                SetStatus($"Lỗi bot video call: {ex.Message}", Color.Crimson);
                btnVideoCall.Enabled = true;
            }
        }

        // Nhận VIDEO_OFFER từ peer
        private void OnIncomingVideoCall(string callerName, string callerAudioPortStr, string callerVideoPortStr, string callerIp, int callerTcpPort)
        {
            if (InvokeRequired)
            {
                BeginInvoke(async () => await HandleIncomingVideoCallAsync(callerName, callerAudioPortStr, callerVideoPortStr, callerIp, callerTcpPort));
                return;
            }
            _ = HandleIncomingVideoCallAsync(callerName, callerAudioPortStr, callerVideoPortStr, callerIp, callerTcpPort);
        }

        private async Task HandleIncomingVideoCallAsync(string callerName, string callerAudioPortStr, string callerVideoPortStr, string callerIp, int callerTcpPort)
        {
            var answer = MessageBox.Show(
                $"📹  Video call từ  {callerName}\n\nBạn có muốn trả lời không?",
                "Video call đến",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                await P2PChatService.SendVoiceSignalWithRelayAsync(callerIp, callerTcpPort, $"VIDEO_REJECT|{_username}", _username, callerName);
                return;
            }

            if (_activeVideoCall != null)
            {
                await P2PChatService.SendVoiceSignalWithRelayAsync(callerIp, callerTcpPort, $"VIDEO_REJECT|{_username}", _username, callerName);
                SetStatus("Đang bận, không thể nhận video call.", Color.OrangeRed);
                return;
            }

            if (!int.TryParse(callerAudioPortStr, out int callerAudioPort) ||
                !int.TryParse(callerVideoPortStr, out int callerVideoPort))
            {
                SetStatus("Signaling: port không hợp lệ.", Color.Crimson);
                return;
            }

            try
            {
                _activeVideoCall   = new VideoCallService();
                _videoCallPeer     = callerName;
                _videoCallPeerIp   = callerIp;
                _videoCallPeerPort = callerTcpPort;

                _activeVideoCall.Prepare();
                _activeVideoCall.SetRemoteEndpoint(callerIp, callerAudioPort, callerVideoPort);

                _videoCaptureService = TryStartCamera();

                await P2PChatService.SendVoiceSignalWithRelayAsync(callerIp, callerTcpPort,
                    $"VIDEO_ANSWER|{_username}|{_activeVideoCall.AudioLocalPort}|{_activeVideoCall.VideoLocalPort}",
                    _username, callerName);

                ShowVideoCallForm(callerName, isOutgoing: false);
                _activeVideoCall.Start();
            }
            catch (Exception ex)
            {
                _activeVideoCall?.Stop();
                _activeVideoCall = null;
                _videoCaptureService?.Dispose();
                _videoCaptureService = null;
                SetStatus($"Lỗi nhận video call: {ex.Message}", Color.Crimson);
            }
        }

        // Nhận VIDEO_ANSWER — caller nhận ports của callee
        private void OnVideoCallAnswered(string peerName, string answererAudioPortStr, string answererVideoPortStr)
        {
            if (_activeVideoCall == null || peerName != _videoCallPeer) return;
            try
            {
                if (!int.TryParse(answererAudioPortStr, out int aPort) ||
                    !int.TryParse(answererVideoPortStr, out int vPort))
                {
                    if (InvokeRequired) Invoke(() => SetStatus("Signaling: port không hợp lệ.", Color.Crimson));
                    else SetStatus("Signaling: port không hợp lệ.", Color.Crimson);
                    return;
                }
                _activeVideoCall.SetRemoteEndpoint(_videoCallPeerIp, aPort, vPort);
                _activeVideoCall.Start();
            }
            catch (Exception ex)
            {
                if (InvokeRequired) Invoke(() => SetStatus($"Lỗi kết nối video: {ex.Message}", Color.Crimson));
                else SetStatus($"Lỗi kết nối video: {ex.Message}", Color.Crimson);
            }
        }

        private void OnVideoCallRejected(string peerName)
        {
            if (InvokeRequired) { Invoke(() => OnVideoCallRejected(peerName)); return; }
            _videoCallForm?.Close();
            _videoCallForm = null;
            _activeVideoCall?.Stop();
            _activeVideoCall = null;
            _videoCaptureService?.Dispose();
            _videoCaptureService = null;
            btnVideoCall.Enabled = _p2pReady;
            SetStatus($"{peerName} từ chối video call.", Color.OrangeRed);
        }

        private void OnVideoCallHungUp(string peerName)
        {
            if (InvokeRequired) { Invoke(() => OnVideoCallHungUp(peerName)); return; }
            _videoCallForm?.Close();
            _videoCallForm = null;
            _activeVideoCall?.Stop();
            _activeVideoCall = null;
            _videoCaptureService?.Dispose();
            _videoCaptureService = null;
            btnVideoCall.Enabled = _p2pReady;
            SetStatus($"Video call với {peerName} đã kết thúc.", Color.OrangeRed);
        }

        private void ShowVideoCallForm(string peerName, bool isOutgoing)
        {
            if (_videoCallForm != null && !_videoCallForm.IsDisposed)
            {
                _videoCallForm.BringToFront();
                return;
            }

            string status = isOutgoing ? "Đang gọi..." : "Đang kết nối...";
            _videoCallForm = new VideoCallForm(peerName, _activeVideoCall!, _videoCaptureService, status);

            // VideoCallForm.OnLocalFrame đã tự gọi _svc.SendVideoFrame — không subscribe thêm để tránh double-send

            _videoCallForm.HangupRequested += async _ =>
            {
                bool isBot = peerName == "UitiChan";
                if (isBot)
                {
                    if (_botCallWriter != null)
                    {
                        try { await _botCallWriter.WriteLineAsync("VIDEO_HANGUP"); } catch { }
                        _botCallTcp?.Close();
                        _botCallTcp    = null;
                        _botCallWriter = null;
                    }
                }
                else if (!string.IsNullOrEmpty(_videoCallPeerIp) && _videoCallPeerPort != 0)
                {
                    await P2PChatService.SendVoiceSignalWithRelayAsync(
                        _videoCallPeerIp, _videoCallPeerPort, $"VIDEO_HANGUP|{_username}", _username, _videoCallPeer);
                }

                if (_videoCaptureService != null)
                {
                    _videoCaptureService.Dispose();
                    _videoCaptureService = null;
                }
                _activeVideoCall?.Stop();
                _activeVideoCall = null;
                _videoCallForm   = null;
                Invoke(() =>
                {
                    btnVideoCall.Enabled = _p2pReady;
                    SetStatus("Video call đã kết thúc.", Color.SeaGreen);
                });
            };

            _videoCallForm.FormClosed += (_, _) => { _videoCallForm = null; };
            _videoCallForm.Show(this);
        }

        // Khởi động camera; trả null nếu không có camera (video call vẫn tiếp tục)
        private static VideoCaptureService? TryStartCamera()
        {
            try
            {
                var svc = new VideoCaptureService();
                svc.Start();
                return svc;
            }
            catch
            {
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  FORM LIFECYCLE
        // ══════════════════════════════════════════════════════════════

        private async void MainChatForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Hủy đăng ký events
            P2PListenerService.MessageReceived   -= OnIncomingMessage;
            P2PListenerService.FileReceived      -= OnIncomingFile;
            P2PListenerService.AvatarReceived    -= OnAvatarReceived;
            P2PListenerService.TypingReceived    -= OnPeerTyping;
            P2PListenerService.IncomingVoiceCall -= OnIncomingVoiceCall;
            _peerTypingClearTimer?.Dispose();
            P2PListenerService.VoiceCallAnswered -= OnVoiceCallAnswered;
            P2PListenerService.VoiceCallRejected -= OnVoiceCallRejected;
            P2PListenerService.VoiceCallHungUp   -= OnVoiceCallHungUp;
            P2PListenerService.IncomingVideoCall -= OnIncomingVideoCall;
            P2PListenerService.VideoCallAnswered -= OnVideoCallAnswered;
            P2PListenerService.VideoCallRejected -= OnVideoCallRejected;
            P2PListenerService.VideoCallHungUp   -= OnVideoCallHungUp;

            // Kết thúc cuộc gọi đang active (nếu có)
            if (_activecall != null)
            {
                if (!string.IsNullOrEmpty(_callPeerIp) && _callPeerPort != 0)
                    await P2PChatService.SendVoiceSignalWithRelayAsync(
                        _callPeerIp, _callPeerPort, $"VOICE_HANGUP|{_username}", _username, _callPeer);
                _activecall.Stop();
            }

            if (_activeVideoCall != null)
            {
                if (!string.IsNullOrEmpty(_videoCallPeerIp) && _videoCallPeerPort != 0)
                    await P2PChatService.SendVoiceSignalWithRelayAsync(
                        _videoCallPeerIp, _videoCallPeerPort, $"VIDEO_HANGUP|{_username}", _username, _videoCallPeer);
                if (_videoCaptureService != null)
                {
                    _videoCaptureService.FrameCaptured -= _activeVideoCall.SendVideoFrame;
                    _videoCaptureService.Dispose();
                }
                _activeVideoCall.Stop();
            }

            RelayPollerService.Stop();

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
