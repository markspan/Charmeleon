namespace Charmeleon
{
    partial class WebDialog
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            pictureBox1 = new PictureBox();
            lblHint     = new Label();
            cboAddress  = new ComboBox();
            lblUrl      = new Label();
            lblNote     = new Label();
            btnClose    = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();

            pictureBox1.Location = new Point(12, 12);
            pictureBox1.Size     = new Size(248, 248);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabStop  = false;

            lblHint.AutoSize    = true;
            lblHint.Location    = new Point(272, 12);
            lblHint.MaximumSize = new Size(268, 0);
            lblHint.Text        = "Scan the QR code, or open this address in a browser on a device on the same network. If it does not connect, choose a different address:";

            cboAddress.DropDownStyle = ComboBoxStyle.DropDownList;
            cboAddress.Location      = new Point(272, 92);
            cboAddress.Size          = new Size(268, 23);

            lblUrl.AutoSize    = true;
            lblUrl.Font        = new Font("Segoe UI", 11F, FontStyle.Bold);
            lblUrl.Location    = new Point(272, 124);
            lblUrl.MaximumSize = new Size(268, 0);

            lblNote.AutoSize    = true;
            lblNote.ForeColor   = SystemColors.GrayText;
            lblNote.Location    = new Point(272, 158);
            lblNote.MaximumSize = new Size(268, 0);
            lblNote.Text        = "The first time each device connects, the browser warns that the certificate is not trusted. Choose Advanced, then Proceed, to continue. This is expected for a local self-signed certificate.";

            btnClose.Text     = "Close";
            btnClose.Location = new Point(460, 252);
            btnClose.Size     = new Size(80, 28);
            btnClose.Click   += (s, e) => Close();

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(552, 296);
            Controls.Add(pictureBox1);
            Controls.Add(lblHint);
            Controls.Add(cboAddress);
            Controls.Add(lblUrl);
            Controls.Add(lblNote);
            Controls.Add(btnClose);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Text            = "Web View";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private PictureBox pictureBox1;
        private Label lblHint;
        private ComboBox cboAddress;
        private Label lblUrl;
        private Label lblNote;
        private Button btnClose;
    }
}