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
            lblUrl      = new Label();
            lblHint     = new Label();
            btnClose    = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();

            pictureBox1.Location = new Point(12, 12);
            pictureBox1.Size     = new Size(220, 220);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabStop  = false;

            lblHint.AutoSize = true;
            lblHint.Location = new Point(244, 12);
            lblHint.Size     = new Size(260, 15);
            lblHint.Text     = "Scan with any phone or tablet on the lab network:";

            lblUrl.AutoSize  = true;
            lblUrl.Font      = new Font("Segoe UI", 10F, FontStyle.Bold);
            lblUrl.Location  = new Point(244, 38);
            lblUrl.MaximumSize = new Size(260, 0);

            btnClose.Text     = "Close";
            btnClose.Location = new Point(366, 196);
            btnClose.Size     = new Size(80, 28);
            btnClose.Click   += (s, e) => Close();

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(528, 244);
            Controls.Add(pictureBox1);
            Controls.Add(lblHint);
            Controls.Add(lblUrl);
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
        private Label lblUrl;
        private Label lblHint;
        private Button btnClose;
    }
}
