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
            pnlLeft          = new Panel();
            lblOnlineTitle   = new Label();
            listBoxUsers     = new ListBox();
            btnRefreshUsers  = new Button();
            lblPeerInfo      = new Label();

            pnlRight       = new Panel();
            rtbChat        = new RichTextBox();
            pnlBottom      = new Panel();
            txtMessage     = new TextBox();
            btnSendFile    = new Button();
            btnSend        = new Button();

            pnlStatus      = new Panel();
            lblStatus      = new Label();

            pnlLeft.SuspendLayout();
            pnlRight.SuspendLayout();
            pnlBottom.SuspendLayout();
            pnlStatus.SuspendLayout();
            this.SuspendLayout();

            // ── Bảng màu Dark Theme ────────────────────────────────────
            var clrBgDeep    = Color.FromArgb(18,  18,  24);   // nền form
            var clrBgLeft    = Color.FromArgb(24,  24,  34);   // panel trái
            var clrBgRight   = Color.FromArgb(20,  20,  30);   // vùng chat
            var clrBgInput   = Color.FromArgb(38,  38,  52);   // ô nhập liệu
            var clrBgBottom  = Color.FromArgb(26,  26,  36);   // bottom bar
            var clrBgStatus  = Color.FromArgb(16,  16,  22);   // status bar
            var clrBgList    = Color.FromArgb(30,  30,  42);   // listbox
            var clrTextMain  = Color.FromArgb(220, 220, 230);  // chữ chính
            var clrTextHint  = Color.FromArgb(130, 130, 155);  // label gợi ý
            var clrTextStatus= Color.FromArgb(160, 160, 180);  // status text
            var clrAccBlue   = Color.FromArgb(0,   120, 212);  // nút Gửi / Đăng nhập
            var clrAccGreen  = Color.FromArgb(0,   160, 110);  // nút Kết nối P2P
            var clrAccPink   = Color.FromArgb(200,  50, 110);  // tiêu đề

            // ════════════════════════════════════════════════════════════
            //  Panel trái – Danh sách online & thiết lập P2P
            // ════════════════════════════════════════════════════════════
            pnlLeft.BackColor = clrBgLeft;
            pnlLeft.Dock      = DockStyle.Left;
            pnlLeft.Width     = 240;
            pnlLeft.Padding   = new Padding(8);

            lblOnlineTitle.Text      = "Người dùng Online";
            lblOnlineTitle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblOnlineTitle.ForeColor = clrAccPink;
            lblOnlineTitle.BackColor = clrBgLeft;
            lblOnlineTitle.Dock      = DockStyle.Top;
            lblOnlineTitle.Height    = 24;
            lblOnlineTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblOnlineTitle.Padding   = new Padding(2, 0, 0, 0);

            listBoxUsers.Dock                  = DockStyle.Top;
            listBoxUsers.Height                = 190;
            listBoxUsers.Font                  = new Font("Segoe UI", 10F);
            listBoxUsers.BorderStyle           = BorderStyle.None;
            listBoxUsers.SelectionMode         = SelectionMode.One;
            listBoxUsers.BackColor             = clrBgList;
            listBoxUsers.ForeColor             = clrTextMain;
            listBoxUsers.SelectedIndexChanged += listBoxUsers_SelectedIndexChanged;

            btnRefreshUsers.Text      = "⟳ Làm mới danh sách";
            btnRefreshUsers.Font      = new Font("Segoe UI", 8.5F);
            btnRefreshUsers.Dock      = DockStyle.Top;
            btnRefreshUsers.Height    = 28;
            btnRefreshUsers.BackColor = Color.FromArgb(40, 40, 58);
            btnRefreshUsers.ForeColor = Color.FromArgb(130, 180, 230);
            btnRefreshUsers.FlatStyle = FlatStyle.Flat;
            btnRefreshUsers.FlatAppearance.BorderColor     = Color.FromArgb(60, 60, 80);
            btnRefreshUsers.FlatAppearance.BorderSize      = 1;
            btnRefreshUsers.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 55, 78);
            btnRefreshUsers.Cursor    = Cursors.Hand;
            btnRefreshUsers.Click    += btnRefreshUsers_Click;

            var sep1 = new Label
            {
                Height    = 8,
                Dock      = DockStyle.Top,
                BackColor = clrBgLeft
            };

            var divider = new Panel
            {
                Height    = 1,
                Dock      = DockStyle.Top,
                BackColor = Color.FromArgb(55, 55, 72)
            };

            var sep2 = new Label
            {
                Height    = 8,
                Dock      = DockStyle.Top,
                BackColor = clrBgLeft
            };

            // Label hiển thị trạng thái kết nối hiện tại
            lblPeerInfo.Text      = "Chọn user để bắt đầu chat";
            lblPeerInfo.Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic);
            lblPeerInfo.ForeColor = clrTextHint;
            lblPeerInfo.BackColor = clrBgLeft;
            lblPeerInfo.Dock      = DockStyle.Top;
            lblPeerInfo.Height    = 36;
            lblPeerInfo.TextAlign = ContentAlignment.MiddleLeft;
            lblPeerInfo.Padding   = new Padding(4, 0, 4, 0);

            // Thứ tự thêm Dock=Top: LIFO (thêm sau hiện trên)
            pnlLeft.Controls.Add(lblPeerInfo);
            pnlLeft.Controls.Add(sep2);
            pnlLeft.Controls.Add(divider);
            pnlLeft.Controls.Add(sep1);
            pnlLeft.Controls.Add(btnRefreshUsers);
            pnlLeft.Controls.Add(listBoxUsers);
            pnlLeft.Controls.Add(lblOnlineTitle);

            // ════════════════════════════════════════════════════════════
            //  Panel dưới – TextBox nhập + Nút Gửi
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
            txtMessage.PlaceholderText = "Nhập tin nhắn... (Enter để gửi)";
            txtMessage.KeyDown        += txtMessage_KeyDown;

            // Thứ tự Add quyết định vị trí: add sau = dock phải hơn
            // Kết quả: [txtMessage (Fill)][btnSendFile][btnSend (phải nhất)]
            pnlBottom.Controls.Add(txtMessage);
            pnlBottom.Controls.Add(btnSendFile);
            pnlBottom.Controls.Add(btnSend);

            // ════════════════════════════════════════════════════════════
            //  Panel phải – Khung chat
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

            lblStatus.Text         = "Chờ kết nối P2P...";
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
            this.ClientSize          = new Size(920, 620);
            this.MinimumSize         = new Size(760, 500);
            this.StartPosition       = FormStartPosition.CenterScreen;
            this.Text                = "Uiti-chan Chat";

            // Thứ tự: Left → Status → Fill (Right)
            this.Controls.Add(pnlRight);
            this.Controls.Add(pnlStatus);
            this.Controls.Add(pnlLeft);

            this.Load        += MainChatForm_Load;
            this.FormClosing += MainChatForm_FormClosing;

            pnlLeft.ResumeLayout(false);
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
        private Label       lblPeerInfo;

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
