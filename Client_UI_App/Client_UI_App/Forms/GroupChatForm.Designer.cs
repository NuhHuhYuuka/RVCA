#nullable disable
namespace Client_UI_App.Forms
{
    partial class GroupChatForm
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
            lblGroupIdTitle = new Label();
            pnlGroupIdRow   = new Panel();
            lblGroupId      = new Label();
            btnCopyId       = new Button();
            lblMembersTitle = new Label();
            listBoxMembers  = new ListBox();
            pnlLeftActions  = new Panel();
            btnRefreshMembers = new Button();
            btnRenameGroup  = new Button();
            btnLeaveGroup   = new Button();
            btnVoice        = new Button();
            btnVideo        = new Button();

            pnlRight    = new Panel();
            rtbChat     = new RichTextBox();
            pnlBottom   = new Panel();
            txtMessage  = new TextBox();
            btnSendFile = new Button();
            btnSend     = new Button();

            pnlStatus   = new Panel();
            lblStatus   = new Label();

            pnlLeft.SuspendLayout();
            pnlGroupIdRow.SuspendLayout();
            pnlLeftActions.SuspendLayout();
            pnlRight.SuspendLayout();
            pnlBottom.SuspendLayout();
            pnlStatus.SuspendLayout();
            this.SuspendLayout();

            // ── Bảng màu Dark Theme (giống MainChatForm) ──────────────
            var clrBgDeep    = Color.FromArgb(18,  18,  24);
            var clrBgLeft    = Color.FromArgb(24,  24,  34);
            var clrBgRight   = Color.FromArgb(20,  20,  30);
            var clrBgInput   = Color.FromArgb(38,  38,  52);
            var clrBgBottom  = Color.FromArgb(26,  26,  36);
            var clrBgStatus  = Color.FromArgb(16,  16,  22);
            var clrBgList    = Color.FromArgb(30,  30,  42);
            var clrTextMain  = Color.FromArgb(220, 220, 230);
            var clrTextHint  = Color.FromArgb(130, 130, 155);
            var clrTextStatus= Color.FromArgb(160, 160, 180);
            var clrAccBlue   = Color.FromArgb(0,   120, 212);
            var clrAccPink   = Color.FromArgb(200,  50, 110);
            var clrAccRed    = Color.FromArgb(180,  50,  50);

            // ════════════════════════════════════════════════════════════
            //  Panel trái — Group ID + Thành viên
            // ════════════════════════════════════════════════════════════
            pnlLeft.BackColor = clrBgLeft;
            pnlLeft.Dock      = DockStyle.Left;
            pnlLeft.Width     = 225;
            pnlLeft.Padding   = new Padding(8, 6, 8, 6);

            // "ID Nhóm"
            lblGroupIdTitle.Text      = "ID Nhóm";
            lblGroupIdTitle.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblGroupIdTitle.ForeColor = clrAccPink;
            lblGroupIdTitle.BackColor = clrBgLeft;
            lblGroupIdTitle.Dock      = DockStyle.Top;
            lblGroupIdTitle.Height    = 30;
            lblGroupIdTitle.TextAlign = ContentAlignment.MiddleLeft;

            // Row: [ID Label] [Copy Button]
            pnlGroupIdRow.BackColor = clrBgLeft;
            pnlGroupIdRow.Dock      = DockStyle.Top;
            pnlGroupIdRow.Height    = 46;

            lblGroupId.Font      = new Font("Consolas", 16F, FontStyle.Bold);
            lblGroupId.ForeColor = Color.FromArgb(100, 200, 255);
            lblGroupId.BackColor = clrBgLeft;
            lblGroupId.Dock      = DockStyle.Fill;
            lblGroupId.TextAlign = ContentAlignment.MiddleLeft;
            lblGroupId.Text      = "------";

            btnCopyId.Text      = "📋";
            btnCopyId.Font      = new Font("Segoe UI", 13F);
            btnCopyId.Dock      = DockStyle.Right;
            btnCopyId.Width     = 40;
            btnCopyId.BackColor = Color.FromArgb(40, 40, 58);
            btnCopyId.ForeColor = clrTextMain;
            btnCopyId.FlatStyle = FlatStyle.Flat;
            btnCopyId.FlatAppearance.BorderSize = 0;
            btnCopyId.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 78);
            btnCopyId.Cursor    = Cursors.Hand;
            btnCopyId.Click    += btnCopyId_Click;

            pnlGroupIdRow.Controls.Add(lblGroupId);
            pnlGroupIdRow.Controls.Add(btnCopyId);

            var sep1 = new Label { Height = 6, Dock = DockStyle.Top, BackColor = clrBgLeft };
            var div1 = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Color.FromArgb(55, 55, 72) };
            var sep2 = new Label { Height = 6, Dock = DockStyle.Top, BackColor = clrBgLeft };

            // "Thành viên"
            lblMembersTitle.Text      = "Thành viên";
            lblMembersTitle.Font      = new Font("Segoe UI", 13F, FontStyle.Bold);
            lblMembersTitle.ForeColor = clrAccPink;
            lblMembersTitle.BackColor = clrBgLeft;
            lblMembersTitle.Dock      = DockStyle.Top;
            lblMembersTitle.Height    = 30;
            lblMembersTitle.TextAlign = ContentAlignment.MiddleLeft;

            listBoxMembers.Dock        = DockStyle.Fill;
            listBoxMembers.Font        = new Font("Segoe UI", 14F);
            listBoxMembers.BorderStyle = BorderStyle.None;
            listBoxMembers.BackColor   = clrBgList;
            listBoxMembers.ForeColor   = clrTextMain;

            // ── pnlLeftActions: dock bottom, chứa các nút hành động ─────
            // Chiều cao = btnRefresh(38) + sep(13) + btnRename(38) + btnVideo(42) + btnVoice(42) + btnLeave(38) = 211
            pnlLeftActions.Dock      = DockStyle.Bottom;
            pnlLeftActions.Height    = 211;
            pnlLeftActions.BackColor = clrBgLeft;

            btnRefreshMembers.Text      = "⟳ Làm mới";
            btnRefreshMembers.Font      = new Font("Segoe UI", 13F);
            btnRefreshMembers.Dock      = DockStyle.Top;
            btnRefreshMembers.Height    = 38;
            btnRefreshMembers.BackColor = Color.FromArgb(40, 40, 58);
            btnRefreshMembers.ForeColor = Color.FromArgb(130, 180, 230);
            btnRefreshMembers.FlatStyle = FlatStyle.Flat;
            btnRefreshMembers.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 80);
            btnRefreshMembers.FlatAppearance.BorderSize  = 1;
            btnRefreshMembers.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 78);
            btnRefreshMembers.Cursor    = Cursors.Hand;
            btnRefreshMembers.Click    += btnRefreshMembers_Click;

            var sep3 = new Label { Height = 6, Dock = DockStyle.Top, BackColor = clrBgLeft };
            var div2 = new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Color.FromArgb(55, 55, 72) };
            var sep4 = new Label { Height = 6, Dock = DockStyle.Top, BackColor = clrBgLeft };

            btnRenameGroup.Text      = "✏️ Đổi tên nhóm";
            btnRenameGroup.Font      = new Font("Segoe UI", 13F);
            btnRenameGroup.Dock      = DockStyle.Top;
            btnRenameGroup.Height    = 38;
            btnRenameGroup.BackColor = Color.FromArgb(60, 80, 120);
            btnRenameGroup.ForeColor = Color.White;
            btnRenameGroup.FlatStyle = FlatStyle.Flat;
            btnRenameGroup.FlatAppearance.BorderSize = 0;
            btnRenameGroup.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 100, 150);
            btnRenameGroup.Cursor    = Cursors.Hand;
            btnRenameGroup.Click    += btnRenameGroup_Click;

            btnLeaveGroup.Text      = "Rời nhóm";
            btnLeaveGroup.Font      = new Font("Segoe UI", 13F);
            btnLeaveGroup.Dock      = DockStyle.Top;
            btnLeaveGroup.Height    = 38;
            btnLeaveGroup.BackColor = clrAccRed;
            btnLeaveGroup.ForeColor = Color.White;
            btnLeaveGroup.FlatStyle = FlatStyle.Flat;
            btnLeaveGroup.FlatAppearance.BorderSize = 0;
            btnLeaveGroup.FlatAppearance.MouseOverBackColor = Color.FromArgb(210, 70, 70);
            btnLeaveGroup.Cursor    = Cursors.Hand;
            btnLeaveGroup.Click    += btnLeaveGroup_Click;

            btnVoice.Text      = "🎙️ Voice";
            btnVoice.Font      = new Font("Segoe UI", 13F);
            btnVoice.Dock      = DockStyle.Top;
            btnVoice.Height    = 42;
            btnVoice.BackColor = Color.FromArgb(40, 100, 60);
            btnVoice.ForeColor = Color.White;
            btnVoice.FlatStyle = FlatStyle.Flat;
            btnVoice.FlatAppearance.BorderSize = 0;
            btnVoice.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 130, 80);
            btnVoice.Cursor    = Cursors.Hand;
            btnVoice.Click    += btnVoice_Click;

            btnVideo.Text      = "📹 Video";
            btnVideo.Font      = new Font("Segoe UI", 13F);
            btnVideo.Dock      = DockStyle.Top;
            btnVideo.Height    = 42;
            btnVideo.BackColor = Color.FromArgb(40, 70, 120);
            btnVideo.ForeColor = Color.White;
            btnVideo.FlatStyle = FlatStyle.Flat;
            btnVideo.FlatAppearance.BorderSize = 0;
            btnVideo.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 95, 155);
            btnVideo.Cursor    = Cursors.Hand;
            btnVideo.Click    += btnVideo_Click;

            // LIFO (thêm sau = hiện trên) trong pnlLeftActions
            pnlLeftActions.Controls.Add(btnLeaveGroup);
            pnlLeftActions.Controls.Add(btnVoice);
            pnlLeftActions.Controls.Add(btnVideo);
            pnlLeftActions.Controls.Add(sep4);
            pnlLeftActions.Controls.Add(div2);
            pnlLeftActions.Controls.Add(sep3);
            pnlLeftActions.Controls.Add(btnRenameGroup);
            pnlLeftActions.Controls.Add(btnRefreshMembers);

            // pnlLeft: LIFO (thêm sau = hiện trên)
            // pnlLeftActions (Dock.Bottom) và listBoxMembers (Dock.Fill) xử lý bởi layout engine
            pnlLeft.Controls.Add(pnlLeftActions);
            pnlLeft.Controls.Add(listBoxMembers);
            pnlLeft.Controls.Add(lblMembersTitle);
            pnlLeft.Controls.Add(sep2);
            pnlLeft.Controls.Add(div1);
            pnlLeft.Controls.Add(sep1);
            pnlLeft.Controls.Add(pnlGroupIdRow);
            pnlLeft.Controls.Add(lblGroupIdTitle);

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
            btnSendFile.FlatAppearance.BorderColor     = Color.FromArgb(70, 70, 95);
            btnSendFile.FlatAppearance.BorderSize      = 1;
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
            txtMessage.PlaceholderText = "Nhắn tin tới nhóm...";
            txtMessage.KeyDown        += txtMessage_KeyDown;

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

            lblStatus.Text         = "Đang tải...";
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
            this.ClientSize          = new Size(820, 560);
            this.MinimumSize         = new Size(640, 420);
            this.StartPosition       = FormStartPosition.CenterScreen;
            this.Text                = "Nhóm";

            this.Controls.Add(pnlRight);
            this.Controls.Add(pnlStatus);
            this.Controls.Add(pnlLeft);

            this.Load        += GroupChatForm_Load;
            this.FormClosing += GroupChatForm_FormClosing;

            pnlLeft.ResumeLayout(false);
            pnlGroupIdRow.ResumeLayout(false);
            pnlLeftActions.ResumeLayout(false);
            pnlRight.ResumeLayout(false);
            pnlBottom.ResumeLayout(false);
            pnlStatus.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        // ── Control fields ─────────────────────────────────────────────
        private Panel       pnlLeft;
        private Label       lblGroupIdTitle;
        private Panel       pnlGroupIdRow;
        private Label       lblGroupId;
        private Button      btnCopyId;
        private Label       lblMembersTitle;
        private ListBox     listBoxMembers;
        private Panel       pnlLeftActions;
        private Button      btnRefreshMembers;
        private Button      btnRenameGroup;
        private Button      btnLeaveGroup;
        private Button      btnVoice;
        private Button      btnVideo;

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
