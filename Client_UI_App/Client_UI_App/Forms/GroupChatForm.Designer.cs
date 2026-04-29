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
            btnRefreshMembers = new Button();
            btnRenameGroup  = new Button();
            btnLeaveGroup   = new Button();

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
            pnlLeft.Width     = 200;
            pnlLeft.Padding   = new Padding(8, 6, 8, 6);

            // "ID Nhóm"
            lblGroupIdTitle.Text      = "ID Nhóm";
            lblGroupIdTitle.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblGroupIdTitle.ForeColor = clrAccPink;
            lblGroupIdTitle.BackColor = clrBgLeft;
            lblGroupIdTitle.Dock      = DockStyle.Top;
            lblGroupIdTitle.Height    = 20;
            lblGroupIdTitle.TextAlign = ContentAlignment.MiddleLeft;

            // Row: [ID Label] [Copy Button]
            pnlGroupIdRow.BackColor = clrBgLeft;
            pnlGroupIdRow.Dock      = DockStyle.Top;
            pnlGroupIdRow.Height    = 30;

            lblGroupId.Font      = new Font("Consolas", 12F, FontStyle.Bold);
            lblGroupId.ForeColor = Color.FromArgb(100, 200, 255);
            lblGroupId.BackColor = clrBgLeft;
            lblGroupId.Dock      = DockStyle.Fill;
            lblGroupId.TextAlign = ContentAlignment.MiddleLeft;
            lblGroupId.Text      = "------";

            btnCopyId.Text      = "📋";
            btnCopyId.Font      = new Font("Segoe UI", 9F);
            btnCopyId.Dock      = DockStyle.Right;
            btnCopyId.Width     = 30;
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
            lblMembersTitle.Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            lblMembersTitle.ForeColor = clrAccPink;
            lblMembersTitle.BackColor = clrBgLeft;
            lblMembersTitle.Dock      = DockStyle.Top;
            lblMembersTitle.Height    = 20;
            lblMembersTitle.TextAlign = ContentAlignment.MiddleLeft;

            listBoxMembers.Dock        = DockStyle.Top;
            listBoxMembers.Height      = 180;
            listBoxMembers.Font        = new Font("Segoe UI", 9.5F);
            listBoxMembers.BorderStyle = BorderStyle.None;
            listBoxMembers.BackColor   = clrBgList;
            listBoxMembers.ForeColor   = clrTextMain;

            btnRefreshMembers.Text      = "⟳ Làm mới";
            btnRefreshMembers.Font      = new Font("Segoe UI", 8.5F);
            btnRefreshMembers.Dock      = DockStyle.Top;
            btnRefreshMembers.Height    = 26;
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
            btnRenameGroup.Font      = new Font("Segoe UI", 8.5F);
            btnRenameGroup.Dock      = DockStyle.Top;
            btnRenameGroup.Height    = 26;
            btnRenameGroup.BackColor = Color.FromArgb(60, 80, 120);
            btnRenameGroup.ForeColor = Color.White;
            btnRenameGroup.FlatStyle = FlatStyle.Flat;
            btnRenameGroup.FlatAppearance.BorderSize = 0;
            btnRenameGroup.FlatAppearance.MouseOverBackColor = Color.FromArgb(75, 100, 150);
            btnRenameGroup.Cursor    = Cursors.Hand;
            btnRenameGroup.Click    += btnRenameGroup_Click;

            btnLeaveGroup.Text      = "Rời nhóm";
            btnLeaveGroup.Font      = new Font("Segoe UI", 8.5F);
            btnLeaveGroup.Dock      = DockStyle.Top;
            btnLeaveGroup.Height    = 26;
            btnLeaveGroup.BackColor = clrAccRed;
            btnLeaveGroup.ForeColor = Color.White;
            btnLeaveGroup.FlatStyle = FlatStyle.Flat;
            btnLeaveGroup.FlatAppearance.BorderSize = 0;
            btnLeaveGroup.FlatAppearance.MouseOverBackColor = Color.FromArgb(210, 70, 70);
            btnLeaveGroup.Cursor    = Cursors.Hand;
            btnLeaveGroup.Click    += btnLeaveGroup_Click;

            // Thứ tự Add Dock=Top: LIFO (thêm sau = hiện trên)
            pnlLeft.Controls.Add(btnLeaveGroup);
            pnlLeft.Controls.Add(sep4);
            pnlLeft.Controls.Add(div2);
            pnlLeft.Controls.Add(sep3);
            pnlLeft.Controls.Add(btnRenameGroup);
            pnlLeft.Controls.Add(btnRefreshMembers);
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
            pnlBottom.Height    = 50;
            pnlBottom.BackColor = clrBgBottom;
            pnlBottom.Padding   = new Padding(8, 7, 8, 7);

            btnSend.Text      = "Gửi ▶";
            btnSend.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnSend.Dock      = DockStyle.Right;
            btnSend.Width     = 90;
            btnSend.BackColor = clrAccBlue;
            btnSend.ForeColor = Color.White;
            btnSend.FlatStyle = FlatStyle.Flat;
            btnSend.FlatAppearance.BorderSize         = 0;
            btnSend.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 145, 235);
            btnSend.Cursor    = Cursors.Hand;
            btnSend.Click    += btnSend_Click;

            btnSendFile.Text      = "📎 File";
            btnSendFile.Font      = new Font("Segoe UI", 9F);
            btnSendFile.Dock      = DockStyle.Right;
            btnSendFile.Width     = 72;
            btnSendFile.BackColor = Color.FromArgb(50, 50, 70);
            btnSendFile.ForeColor = Color.FromArgb(180, 190, 210);
            btnSendFile.FlatStyle = FlatStyle.Flat;
            btnSendFile.FlatAppearance.BorderColor     = Color.FromArgb(70, 70, 95);
            btnSendFile.FlatAppearance.BorderSize      = 1;
            btnSendFile.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 65, 90);
            btnSendFile.Cursor    = Cursors.Hand;
            btnSendFile.Click    += btnSendFile_Click;

            txtMessage.Font            = new Font("Segoe UI", 11F);
            txtMessage.Dock            = DockStyle.Fill;
            txtMessage.BackColor       = clrBgInput;
            txtMessage.ForeColor       = clrTextMain;
            txtMessage.BorderStyle     = BorderStyle.None;
            txtMessage.PlaceholderText = "Nhập tin nhắn nhóm... (Enter để gửi)";
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
            rtbChat.Font        = new Font("Segoe UI", 10F);
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
            pnlStatus.Height    = 26;
            pnlStatus.BackColor = clrBgStatus;
            pnlStatus.Padding   = new Padding(10, 4, 10, 0);

            lblStatus.Text         = "Đang tải...";
            lblStatus.Font         = new Font("Segoe UI", 8.5F);
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
        private Button      btnRefreshMembers;
        private Button      btnRenameGroup;
        private Button      btnLeaveGroup;

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
