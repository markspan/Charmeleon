namespace Charmeleon
{
    partial class aboutForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            richTextBox1 = new RichTextBox();
            linkLabel1 = new LinkLabel();
            label1 = new Label();
            pictureBox1 = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();

            label1.AutoSize = true;
            label1.Font = new Font("Microsoft Sans Serif", 18F, FontStyle.Regular, GraphicsUnit.Point, 0);
            label1.Location = new Point(12, 12);
            label1.Text = "Charmeleon";

            richTextBox1.Location = new Point(12, 55);
            richTextBox1.Size = new Size(330, 124);
            richTextBox1.TabIndex = 1;
            richTextBox1.Text = "Charmeleon\r\n\r\nImpedance checking program\r\nFor use with EEGO and TMSi amplifiers\r\n\r\n(c) 2025 University of Groningen\r\nWritten by M.M. Span";
            richTextBox1.ReadOnly = true;
            richTextBox1.BorderStyle = BorderStyle.None;

            linkLabel1.AutoSize = true;
            linkLabel1.Location = new Point(12, 186);
            linkLabel1.Text = "github.com/markspan/Charmeleon";
            linkLabel1.Tag = "https://github.com/markspan/Charmeleon";
            linkLabel1.LinkClicked += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo((string)((LinkLabel)s!).Tag!) { UseShellExecute = true });

            pictureBox1.Location = new Point(355, 0);
            pictureBox1.Size = new Size(190, 190);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabStop = false;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(557, 222);
            Controls.Add(label1);
            Controls.Add(richTextBox1);
            Controls.Add(linkLabel1);
            Controls.Add(pictureBox1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "About Charmeleon";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private RichTextBox richTextBox1;
        private Label label1;
        private PictureBox pictureBox1;
        private LinkLabel linkLabel1;
    }
}


