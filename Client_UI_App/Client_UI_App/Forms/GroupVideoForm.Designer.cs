#nullable disable
namespace Client_UI_App.Forms
{
    partial class GroupVideoForm
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
            flowTiles  = new System.Windows.Forms.FlowLayoutPanel();
            pnlCtrl    = new System.Windows.Forms.Panel();
            pbMic      = new System.Windows.Forms.ProgressBar();
            lblMic     = new System.Windows.Forms.Label();
            btnMute    = new System.Windows.Forms.Button();
            btnCam     = new System.Windows.Forms.Button();
            btnScreen  = new System.Windows.Forms.Button();
            btnLeave   = new System.Windows.Forms.Button();

            pnlCtrl.SuspendLayout();
            this.SuspendLayout();

            var clrBg   = Color.FromArgb(12,  12,  18);
            var clrCard = Color.FromArgb(22,  22,  32);
            var clrText = Color.FromArgb(220, 220, 235);
            var clrRed  = Color.FromArgb(210,  50,  60);

            // ── flowTiles ─────────────────────────────────────────────────
            flowTiles.Dock            = System.Windows.Forms.DockStyle.Fill;
            flowTiles.BackColor       = clrBg;
            flowTiles.AutoScroll      = true;
            flowTiles.FlowDirection   = System.Windows.Forms.FlowDirection.LeftToRight;
            flowTiles.WrapContents    = true;
            flowTiles.Padding         = new System.Windows.Forms.Padding(6);

            // ── pnlCtrl (bottom bar) ──────────────────────────────────────
            pnlCtrl.Dock      = System.Windows.Forms.DockStyle.Bottom;
            pnlCtrl.Height    = 56;
            pnlCtrl.BackColor = clrCard;
            pnlCtrl.Padding   = new System.Windows.Forms.Padding(8, 10, 8, 10);

            lblMic.AutoSize  = false;
            lblMic.Text      = "🎤";
            lblMic.Size      = new Size(26, 22);
            lblMic.Location  = new Point(10, 16);
            lblMic.Font      = new Font("Segoe UI", 10F);
            lblMic.ForeColor = Color.FromArgb(120, 120, 148);
            lblMic.BackColor = clrCard;

            pbMic.Location  = new Point(38, 20);
            pbMic.Size      = new Size(160, 14);
            pbMic.Minimum   = 0;
            pbMic.Maximum   = 1000;
            pbMic.Style     = System.Windows.Forms.ProgressBarStyle.Continuous;
            pbMic.ForeColor = Color.FromArgb(80, 200, 140);
            pbMic.BackColor = Color.FromArgb(38, 38, 55);

            btnMute.Text      = "🔇 Tắt mic";
            btnMute.Location  = new Point(210, 12);
            btnMute.Size      = new Size(120, 30);
            btnMute.Font      = new Font("Segoe UI", 10F);
            btnMute.BackColor = Color.FromArgb(50, 50, 72);
            btnMute.ForeColor = clrText;
            btnMute.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnMute.FlatAppearance.BorderSize = 0;
            btnMute.Cursor    = System.Windows.Forms.Cursors.Hand;
            btnMute.Click    += btnMute_Click;

            btnCam.Text      = "📷 Tắt cam";
            btnCam.Location  = new Point(338, 12);
            btnCam.Size      = new Size(120, 30);
            btnCam.Font      = new Font("Segoe UI", 10F);
            btnCam.BackColor = Color.FromArgb(50, 50, 72);
            btnCam.ForeColor = clrText;
            btnCam.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnCam.FlatAppearance.BorderSize = 0;
            btnCam.Cursor    = System.Windows.Forms.Cursors.Hand;
            btnCam.Click    += btnCam_Click;

            btnScreen.Text      = "🖥️ Chia sẻ MH";
            btnScreen.Location  = new Point(466, 12);
            btnScreen.Size      = new Size(130, 30);
            btnScreen.Font      = new Font("Segoe UI", 10F);
            btnScreen.BackColor = Color.FromArgb(50, 50, 72);
            btnScreen.ForeColor = clrText;
            btnScreen.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnScreen.FlatAppearance.BorderSize = 0;
            btnScreen.Cursor    = System.Windows.Forms.Cursors.Hand;
            btnScreen.Click    += btnScreen_Click;

            btnLeave.Text      = "📵 Rời Video";
            btnLeave.Location  = new Point(604, 12);
            btnLeave.Size      = new Size(130, 30);
            btnLeave.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnLeave.BackColor = clrRed;
            btnLeave.ForeColor = Color.White;
            btnLeave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnLeave.FlatAppearance.BorderSize = 0;
            btnLeave.Cursor    = System.Windows.Forms.Cursors.Hand;
            btnLeave.Click    += btnLeave_Click;

            pnlCtrl.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                lblMic, pbMic, btnMute, btnCam, btnScreen, btnLeave
            });

            // ── Form ──────────────────────────────────────────────────────
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor           = clrBg;
            this.ClientSize          = new Size(1024, 640);
            this.MinimumSize         = new Size(680, 480);
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text                = "📹 Group Video";

            this.Controls.Add(flowTiles);
            this.Controls.Add(pnlCtrl);

            this.FormClosing += GroupVideoForm_FormClosing;

            pnlCtrl.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.FlowLayoutPanel flowTiles;
        private System.Windows.Forms.Panel           pnlCtrl;
        private System.Windows.Forms.ProgressBar     pbMic;
        private System.Windows.Forms.Label           lblMic;
        private System.Windows.Forms.Button          btnMute;
        private System.Windows.Forms.Button          btnCam;
        private System.Windows.Forms.Button          btnScreen;
        private System.Windows.Forms.Button          btnLeave;
    }
}
