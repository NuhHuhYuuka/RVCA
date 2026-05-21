#nullable disable
namespace Client_UI_App.Forms
{
    partial class GroupVoiceForm
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
            flowMembers = new System.Windows.Forms.FlowLayoutPanel();
            pnlCtrl     = new System.Windows.Forms.Panel();
            lblMic      = new System.Windows.Forms.Label();
            pbMic       = new System.Windows.Forms.ProgressBar();
            btnMute     = new System.Windows.Forms.Button();
            btnLeave    = new System.Windows.Forms.Button();

            pnlCtrl.SuspendLayout();
            this.SuspendLayout();

            var clrBg   = Color.FromArgb(12,  12,  18);
            var clrCard = Color.FromArgb(22,  22,  32);
            var clrText = Color.FromArgb(220, 220, 235);
            var clrRed  = Color.FromArgb(210,  50,  60);

            // ── flowMembers ───────────────────────────────────────────
            flowMembers.Dock          = System.Windows.Forms.DockStyle.Fill;
            flowMembers.BackColor     = clrBg;
            flowMembers.AutoScroll    = true;
            flowMembers.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            flowMembers.WrapContents  = false;
            flowMembers.Padding       = new System.Windows.Forms.Padding(8);

            // ── pnlCtrl (bottom bar) ──────────────────────────────────
            pnlCtrl.Dock      = System.Windows.Forms.DockStyle.Bottom;
            pnlCtrl.Height    = 56;
            pnlCtrl.BackColor = clrCard;

            lblMic.AutoSize  = false;
            lblMic.Text      = "🎤";
            lblMic.Size      = new Size(26, 22);
            lblMic.Location  = new Point(10, 17);
            lblMic.Font      = new Font("Segoe UI", 10F);
            lblMic.ForeColor = Color.FromArgb(120, 120, 148);
            lblMic.BackColor = clrCard;

            pbMic.Location  = new Point(38, 21);
            pbMic.Size      = new Size(140, 14);
            pbMic.Minimum   = 0;
            pbMic.Maximum   = 1000;
            pbMic.Style     = System.Windows.Forms.ProgressBarStyle.Continuous;
            pbMic.ForeColor = Color.FromArgb(80, 200, 140);
            pbMic.BackColor = Color.FromArgb(38, 38, 55);

            btnMute.Text      = "🔇 Tắt mic";
            btnMute.Location  = new Point(190, 13);
            btnMute.Size      = new Size(120, 30);
            btnMute.Font      = new Font("Segoe UI", 10F);
            btnMute.BackColor = Color.FromArgb(50, 50, 72);
            btnMute.ForeColor = clrText;
            btnMute.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnMute.FlatAppearance.BorderSize = 0;
            btnMute.Cursor    = System.Windows.Forms.Cursors.Hand;
            btnMute.Click    += btnMute_Click;

            btnLeave.Text      = "📵 Rời Voice";
            btnLeave.Location  = new Point(318, 13);
            btnLeave.Size      = new Size(120, 30);
            btnLeave.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnLeave.BackColor = clrRed;
            btnLeave.ForeColor = Color.White;
            btnLeave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnLeave.FlatAppearance.BorderSize = 0;
            btnLeave.Cursor    = System.Windows.Forms.Cursors.Hand;
            btnLeave.Click    += btnLeave_Click;

            pnlCtrl.Controls.AddRange(new System.Windows.Forms.Control[]
            {
                lblMic, pbMic, btnMute, btnLeave
            });

            // ── Form ──────────────────────────────────────────────────
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor           = clrBg;
            this.ClientSize          = new Size(460, 320);
            this.MinimumSize         = new Size(440, 260);
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text                = "🎙️ Voice Channel";

            this.Controls.Add(flowMembers);
            this.Controls.Add(pnlCtrl);

            this.FormClosing += GroupVoiceForm_FormClosing;

            pnlCtrl.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.FlowLayoutPanel flowMembers;
        private System.Windows.Forms.Panel           pnlCtrl;
        private System.Windows.Forms.Label           lblMic;
        private System.Windows.Forms.ProgressBar     pbMic;
        private System.Windows.Forms.Button          btnMute;
        private System.Windows.Forms.Button          btnLeave;
    }
}
