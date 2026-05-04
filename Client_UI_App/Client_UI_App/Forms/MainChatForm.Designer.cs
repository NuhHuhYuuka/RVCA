#nullable disable
namespace Client_UI_App.Forms
{
    partial class MainChatForm
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
            // ── Khai báo controls ──────────────────────────────────────
            pnlLeft         = new Panel();
            lblOnlineTitle  = new Label();
            listBoxUsers    = new ListBox();
            btnRefreshUsers = new Button();
            pnlGroupButtons = new Panel();
            btnCreateGroup  = new Button();
            btnJoinGroup    = new Button();
            lblGroupsTitle  = new Label();
            listBoxGroups   = new ListBox();

            // Peer info area
            pnlPeerInfo     = new Panel();
            picPeerAvatar   = new PictureBox();
            lblPeerInfo     = new Label();
            btnCall         = new Button();
            btnVideoCall    = new Button();

            // User profile bar (bottom of sidebar)
            pnlUserProfile  = new Panel();
            picMyAvatar     = new PictureBox();
            lblMyUsername   = new Label();

            pnlRight       = new Panel();
            rtbChat        = new RichTextBox();
            pnlBottom      = new Panel();
            txtMessage     = new TextBox();
            btnSendFile    = new Button();
            btnSend        = new Button();

            pnlStatus      = new Panel();
            lblStatus      = new Label();

            pnlLeft.SuspendLayout();
            pnlGroupButtons.SuspendLayout();
            pnlPeerInfo.SuspendLayout();
            pnlUserProfile.SuspendLayout();
            pnlRight.SuspendLayout();
            pnlBottom.SuspendLayout();
            pnlStatus.SuspendLayout();
            this.SuspendLayout();

            // ── Bảng màu Dark Theme ────────────────────────────────────
            var clrBgDeep      = Color.FromArgb(18,  18,  24);
            var clrBgLeft      = Color.FromArgb(24,  24,  34);
            var clrBgRight     = Color.FromArgb(20,  20,  30);
            var clrBgInput     = Color.FromArgb(38,  38,  52);
            var clrBgBottom    = Color.FromArgb(26,  26,  36);
            var clrBgStatus    = Color.FromArgb(16,  16,  22);
            var clrBgList      = Color.FromArgb(30,  30,  42);
            var clrUserProfile = Color.FromArgb(20,  20,  30);
            var clrTextMain    = Color.FromArgb(220, 220, 230);
            var clrTextHint    = Color.FromArgb(130, 130, 155);
            var clrTextStatus  = Color.FromArgb(160, 160, 180);
            var clrAccBlue     = Color.FromArgb(0,   120, 212);
            var clrAccGreen    = Color.FromArgb(0,   160, 110);
            var clrAccPink     = Color.FromArgb(200,  50, 110);

            // ════════════════════════════════════════════════════════════
            //  Panel trái — Danh sách online + Nhóm + Peer info + User profile
            // ════════════════════════════════════════════════════════════
            pnlLeft.BackColor = clrBgLeft;
            pnlLeft.Dock      = DockStyle.Left;
            pnlLeft.Width     = 265;
            pnlLeft.Padding   = new Padding(0);

            // ── Tiêu đề "Người dùng Online" ───────────────────────────
            lblOnlineTitle.Text      = "  Người dùng Online";
            lblOnlineTitle.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblOnlineTitle.ForeColor = clrAccPink;
            lblOnlineTitle.BackColor = clrBgLeft;
            lblOnlineTitle.Dock      = DockStyle.Top;
            lblOnlineTitle.Height    = 34;
            lblOnlineTitle.TextAlign = ContentAlignment.MiddleLeft;

            listBoxUsers.Dock                  = DockStyle.Top;
            listBoxUsers.Height                = 150;
            listBoxUsers.Font                  = new Font("Segoe UI", 14F);
            listBoxUsers.BorderStyle           = BorderStyle.None;
            listBoxUsers.SelectionMode         = SelectionMode.One;
            listBoxUsers.BackColor             = clrBgList;
            listBoxUsers.ForeColor             = clrTextMain;
            listBoxUsers.Padding               = new Padding(4, 0, 0, 0);
            listBoxUsers.DrawMode              = DrawMode.OwnerDrawFixed;
            listBoxUsers.ItemHeight            = 32;
            listBoxUsers.SelectedIndexChanged += listBoxUsers_SelectedIndexChanged;
            listBoxUsers.DrawItem             += listBoxUsers_DrawItem;

            btnRefreshUsers.Text      = "⟳  Làm mới";
            btnRefreshUsers.Font      = new Font("Segoe UI", 13F);
            btnRefreshUsers.Dock      = DockStyle.Top;
            btnRefreshUsers.Height    = 38;
            btnRefreshUsers.BackColor = Color.FromArgb(40, 40, 58);
            btnRefreshUsers.ForeColor = Color.FromArgb(130, 180, 230);
            btnRefreshUsers.FlatStyle = FlatStyle.Flat;
            btnRefreshUsers.FlatAppearance.BorderColor        = Color.FromArgb(60, 60, 80);
            btnRefreshUsers.FlatAppearance.BorderSize         = 1;
            btnRefreshUsers.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 78);
            btnRefreshUsers.Cursor    = Cursors.Hand;
            btnRefreshUsers.Click    += btnRefreshUsers_Click;

            var sep1 = new Label { Height = 7, Dock = DockStyle.Top, BackColor = clrBgLeft };
            var div1 = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Color.FromArgb(55, 55, 72) };
            var sep2 = new Label { Height = 7, Dock = DockStyle.Top, BackColor = clrBgLeft };

            // ── Tiêu đề "Nhóm của tôi" ────────────────────────────────
            lblGroupsTitle.Text      = "  Nhóm của tôi";
            lblGroupsTitle.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblGroupsTitle.ForeColor = clrAccPink;
            lblGroupsTitle.BackColor = clrBgLeft;
            lblGroupsTitle.Dock      = DockStyle.Top;
            lblGroupsTitle.Height    = 34;
            lblGroupsTitle.TextAlign = ContentAlignment.MiddleLeft;

            listBoxGroups.Dock                  = DockStyle.Top;
            listBoxGroups.Height                = 140;
            listBoxGroups.Font                  = new Font("Segoe UI", 14F);
            listBoxGroups.BorderStyle           = BorderStyle.None;
            listBoxGroups.SelectionMode         = SelectionMode.One;
            listBoxGroups.BackColor             = clrBgList;
            listBoxGroups.ForeColor             = clrTextMain;
            listBoxGroups.Padding               = new Padding(4, 0, 0, 0);
            listBoxGroups.DoubleClick          += listBoxGroups_DoubleClick;

            // Hàng nút nhóm: [+ Tạo] [→ Tham gia]
            pnlGroupButtons.Dock      = DockStyle.Top;
            pnlGroupButtons.Height    = 42;
            pnlGroupButtons.BackColor = clrBgLeft;

            btnCreateGroup.Text      = "+ Tạo";
            btnCreateGroup.Font      = new Font("Segoe UI", 13F);
            btnCreateGroup.Dock      = DockStyle.Left;
            btnCreateGroup.Width     = 108;
            btnCreateGroup.BackColor = clrAccGreen;
            btnCreateGroup.ForeColor = Color.White;
            btnCreateGroup.FlatStyle = FlatStyle.Flat;
            btnCreateGroup.FlatAppearance.BorderSize         = 0;
            btnCreateGroup.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 185, 128);
            btnCreateGroup.Cursor    = Cursors.Hand;
            btnCreateGroup.Click    += btnCreateGroup_Click;

            btnJoinGroup.Text      = "→ Tham gia";
            btnJoinGroup.Font      = new Font("Segoe UI", 13F);
            btnJoinGroup.Dock      = DockStyle.Fill;
            btnJoinGroup.BackColor = Color.FromArgb(50, 80, 130);
            btnJoinGroup.ForeColor = Color.White;
            btnJoinGroup.FlatStyle = FlatStyle.Flat;
            btnJoinGroup.FlatAppearance.BorderSize         = 0;
            btnJoinGroup.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 100, 160);
            btnJoinGroup.Cursor    = Cursors.Hand;
            btnJoinGroup.Click    += btnJoinGroup_Click;

            pnlGroupButtons.Controls.Add(btnJoinGroup);
            pnlGroupButtons.Controls.Add(btnCreateGroup);

            var sep3 = new Label { Height = 7, Dock = DockStyle.Top, BackColor = clrBgLeft };
            var div2 = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Color.FromArgb(55, 55, 72) };
            var sep4 = new Label { Height = 7, Dock = DockStyle.Top, BackColor = clrBgLeft };

            // ── Peer info panel (avatar + tên peer + nút gọi) ─────────
            pnlPeerInfo.Dock      = DockStyle.Top;
            pnlPeerInfo.Height    = 68;
            pnlPeerInfo.BackColor = clrBgLeft;
            pnlPeerInfo.Padding   = new Padding(0);

            var pnlPeerAvatarWrap = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 52,
                BackColor = clrBgLeft
            };
            picPeerAvatar.Size      = new Size(34, 34);
            picPeerAvatar.Location  = new Point(9, 9);
            picPeerAvatar.SizeMode  = PictureBoxSizeMode.StretchImage;
            picPeerAvatar.BackColor = Color.FromArgb(50, 50, 72);
            pnlPeerAvatarWrap.Controls.Add(picPeerAvatar);

            lblPeerInfo.Text      = "Chọn user để bắt đầu chat";
            lblPeerInfo.Font      = new Font("Segoe UI", 13F, FontStyle.Italic);
            lblPeerInfo.ForeColor = clrTextHint;
            lblPeerInfo.BackColor = clrBgLeft;
            lblPeerInfo.Dock      = DockStyle.Fill;
            lblPeerInfo.TextAlign = ContentAlignment.MiddleLeft;
            lblPeerInfo.Padding   = new Padding(4, 0, 4, 0);

            btnCall.Text      = "📞";
            btnCall.Font      = new Font("Segoe UI", 16F);
            btnCall.Dock      = DockStyle.Right;
            btnCall.Width     = 56;
            btnCall.BackColor = Color.FromArgb(0, 140, 90);
            btnCall.ForeColor = Color.White;
            btnCall.FlatStyle = FlatStyle.Flat;
            btnCall.FlatAppearance.BorderSize         = 0;
            btnCall.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 168, 110);
            btnCall.Cursor    = Cursors.Hand;
            btnCall.Visible   = false;
            btnCall.Click    += btnCall_Click;

            btnVideoCall.Text      = "📹";
            btnVideoCall.Font      = new Font("Segoe UI", 16F);
            btnVideoCall.Dock      = DockStyle.Right;
            btnVideoCall.Width     = 56;
            btnVideoCall.BackColor = Color.FromArgb(0, 80, 160);
            btnVideoCall.ForeColor = Color.White;
            btnVideoCall.FlatStyle = FlatStyle.Flat;
            btnVideoCall.FlatAppearance.BorderSize         = 0;
            btnVideoCall.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 100, 190);
            btnVideoCall.Cursor    = Cursors.Hand;
            btnVideoCall.Visible   = false;
            btnVideoCall.Click    += btnVideoCall_Click;

            // LIFO: Fill đầu tiên; Right docked theo thứ tự: btnVideoCall (trái) → btnCall (phải)
            pnlPeerInfo.Controls.Add(lblPeerInfo);
            pnlPeerInfo.Controls.Add(btnVideoCall);
            pnlPeerInfo.Controls.Add(btnCall);
            pnlPeerInfo.Controls.Add(pnlPeerAvatarWrap);

            // ── User profile bar (đáy sidebar, giống Discord) ─────────
            var divProfile = new Panel
            {
                Height    = 1,
                Dock      = DockStyle.Top,
                BackColor = Color.FromArgb(35, 35, 50)
            };

            pnlUserProfile.Dock      = DockStyle.Top;
            pnlUserProfile.Height    = 74;
            pnlUserProfile.BackColor = clrUserProfile;
            pnlUserProfile.Padding   = new Padding(0);

            var pnlMyAvatarWrap = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 56,
                BackColor = clrUserProfile
            };
            picMyAvatar.Size      = new Size(40, 40);
            picMyAvatar.Location  = new Point(8, 9);
            picMyAvatar.SizeMode  = PictureBoxSizeMode.StretchImage;
            picMyAvatar.BackColor = Color.FromArgb(50, 50, 72);
            picMyAvatar.Cursor    = Cursors.Hand;
            picMyAvatar.Click    += picMyAvatar_Click;

            var toolTip = new ToolTip();
            toolTip.SetToolTip(picMyAvatar, "Click để đổi ảnh đại diện");
            pnlMyAvatarWrap.Controls.Add(picMyAvatar);

            lblMyUsername.Text      = "...";
            lblMyUsername.Font      = new Font("Segoe UI", 14F, FontStyle.Bold);
            lblMyUsername.ForeColor = clrTextMain;
            lblMyUsername.BackColor = clrUserProfile;
            lblMyUsername.Dock      = DockStyle.Fill;
            lblMyUsername.TextAlign = ContentAlignment.MiddleLeft;
            lblMyUsername.Padding   = new Padding(2, 0, 0, 0);

            pnlUserProfile.Controls.Add(lblMyUsername);
            pnlUserProfile.Controls.Add(pnlMyAvatarWrap);

            // ── Thêm vào pnlLeft (LIFO Dock=Top) ─────────────────────
            pnlLeft.Controls.Add(pnlUserProfile);   // bottom
            pnlLeft.Controls.Add(divProfile);
            pnlLeft.Controls.Add(pnlPeerInfo);
            pnlLeft.Controls.Add(sep4);
            pnlLeft.Controls.Add(div2);
            pnlLeft.Controls.Add(sep3);
            pnlLeft.Controls.Add(pnlGroupButtons);
            pnlLeft.Controls.Add(listBoxGroups);
            pnlLeft.Controls.Add(lblGroupsTitle);
            pnlLeft.Controls.Add(sep2);
            pnlLeft.Controls.Add(div1);
            pnlLeft.Controls.Add(sep1);
            pnlLeft.Controls.Add(btnRefreshUsers);
            pnlLeft.Controls.Add(listBoxUsers);
            pnlLeft.Controls.Add(lblOnlineTitle);   // top

            // ════════════════════════════════════════════════════════════
            //  Panel dưới — TextBox nhập + Nút Gửi
            // ════════════════════════════════════════════════════════════
            pnlBottom.Dock      = DockStyle.Bottom;
            pnlBottom.Height    = 84;
            pnlBottom.BackColor = clrBgBottom;
            pnlBottom.Padding   = new Padding(8, 8, 8, 8);

            btnSend.Text      = "Gửi ▶";
            btnSend.Font      = new Font("Segoe UI", 14F, FontStyle.Bold);
            btnSend.Dock      = DockStyle.Right;
            btnSend.Width     = 105;
            btnSend.BackColor = clrAccBlue;
            btnSend.ForeColor = Color.White;
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.FlatAppearance.BorderSize         = 0;
            btnSend.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 145, 235);
            btnSend.Cursor    = Cursors.Hand;
            btnSend.Click    += btnSend_Click;

            btnSendFile.Text      = "📎 File";
            btnSendFile.Font      = new Font("Segoe UI", 13F);
            btnSendFile.Dock      = DockStyle.Right;
            btnSendFile.Width     = 88;
            btnSendFile.BackColor = Color.FromArgb(50, 50, 70);
            btnSendFile.ForeColor = Color.FromArgb(180, 190, 210);
            btnSendFile.FlatStyle = FlatStyle.Flat;
            btnSendFile.FlatAppearance.BorderColor        = Color.FromArgb(70, 70, 95);
            btnSendFile.FlatAppearance.BorderSize         = 1;
            btnSendFile.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 90);
            btnSendFile.Cursor    = Cursors.Hand;
            btnSendFile.Click    += btnSendFile_Click;

            txtMessage.Font            = new Font("Segoe UI", 18F);
            txtMessage.Dock            = DockStyle.Fill;
            txtMessage.BackColor       = clrBgInput;
            txtMessage.ForeColor       = clrTextMain;
            txtMessage.BorderStyle     = BorderStyle.None;
            txtMessage.Multiline       = true;
            txtMessage.AcceptsReturn   = false;
            txtMessage.ScrollBars      = ScrollBars.None;
            txtMessage.PlaceholderText = "Nhắn tin...";
            txtMessage.KeyDown        += txtMessage_KeyDown;
            txtMessage.TextChanged    += txtMessage_TextChanged;

            pnlBottom.Controls.Add(txtMessage);
            pnlBottom.Controls.Add(btnSendFile);
            pnlBottom.Controls.Add(btnSend);

            // ════════════════════════════════════════════════════════════
            //  Panel phải — Khung chat
            // ════════════════════════════════════════════════════════════
            pnlRight.Dock      = DockStyle.Fill;
            pnlRight.BackColor = clrBgRight;

            rtbChat.Dock        = DockStyle.Fill;
            rtbChat.ReadOnly    = true;
            rtbChat.BackColor   = clrBgRight;
            rtbChat.ForeColor   = clrTextMain;
            rtbChat.Font        = new Font("Segoe UI", 16F);
            rtbChat.BorderStyle = BorderStyle.None;
            rtbChat.ScrollBars  = RichTextBoxScrollBars.Vertical;
            rtbChat.DetectUrls  = false;
            rtbChat.Padding     = new Padding(8);

            pnlRight.Controls.Add(rtbChat);
            pnlRight.Controls.Add(pnlBottom);

            // ════════════════════════════════════════════════════════════
            //  Status bar
            // ════════════════════════════════════════════════════════════
            pnlStatus.Dock      = DockStyle.Bottom;
            pnlStatus.Height    = 32;
            pnlStatus.BackColor = clrBgStatus;
            pnlStatus.Padding   = new Padding(10, 4, 10, 0);

            lblStatus.Text         = "Chờ kết nối P2P...";
            lblStatus.Font         = new Font("Segoe UI", 12F);
            lblStatus.ForeColor    = clrTextStatus;
            lblStatus.BackColor    = clrBgStatus;
            lblStatus.Dock         = DockStyle.Fill;
            lblStatus.AutoEllipsis = true;

            pnlStatus.Controls.Add(lblStatus);

            // ════════════════════════════════════════════════════════════
            //  Form
            // ════════════════════════════════════════════════════════════
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode       = AutoScaleMode.Font;
            this.BackColor           = clrBgDeep;
            this.ClientSize          = new Size(960, 660);
            this.MinimumSize         = new Size(800, 540);
            this.StartPosition       = FormStartPosition.CenterScreen;
            this.Text                = "Uiti-chan Chat";

            this.Controls.Add(pnlRight);
            this.Controls.Add(pnlStatus);
            this.Controls.Add(pnlLeft);

            this.Load        += MainChatForm_Load;
            this.FormClosing += MainChatForm_FormClosing;

            pnlLeft.ResumeLayout(false);
            pnlGroupButtons.ResumeLayout(false);
            pnlPeerInfo.ResumeLayout(false);
            pnlUserProfile.ResumeLayout(false);
            pnlRight.ResumeLayout(false);
            pnlBottom.ResumeLayout(false);
            pnlStatus.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        // ── Control fields ─────────────────────────────────────────────
        private Panel       pnlLeft;
        private Label       lblOnlineTitle;
        private ListBox     listBoxUsers;
        private Button      btnRefreshUsers;
        private Panel       pnlGroupButtons;
        private Button      btnCreateGroup;
        private Button      btnJoinGroup;
        private Label       lblGroupsTitle;
        private ListBox     listBoxGroups;

        private Panel       pnlPeerInfo;
        private PictureBox  picPeerAvatar;
        private Label       lblPeerInfo;
        private Button      btnCall;
        private Button      btnVideoCall;

        private Panel       pnlUserProfile;
        private PictureBox  picMyAvatar;
        private Label       lblMyUsername;

        private Panel       pnlRight;
        private RichTextBox rtbChat;
        private Panel       pnlBottom;
        private TextBox     txtMessage;
        private Button      btnSendFile;
        private Button      btnSend;

        private Panel       pnlStatus;
        private Label       lblStatus;
    }
}
