namespace CharmeleonGUI
{
    partial class MainGUI
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainGUI));
            statusStrip1 = new StatusStrip();
            menuStrip1 = new MenuStrip();
            setupToolStripMenuItem = new ToolStripMenuItem();
            openMontageToolStripMenuItem = new ToolStripMenuItem();
            saveMontageToolStripMenuItem = new ToolStripMenuItem();
            exceptionsToolStripMenuItem = new ToolStripMenuItem();
            viewChannelsToolStripMenuItem = new ToolStripMenuItem();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // statusStrip1
            // 
            statusStrip1.Location = new Point(0, 808);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1125, 22);
            statusStrip1.TabIndex = 1;
            statusStrip1.Text = "statusStrip1";
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { setupToolStripMenuItem, exceptionsToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1125, 24);
            menuStrip1.TabIndex = 2;
            menuStrip1.Text = "menuStrip1";
            // 
            // setupToolStripMenuItem
            // 
            setupToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openMontageToolStripMenuItem, saveMontageToolStripMenuItem });
            setupToolStripMenuItem.Name = "setupToolStripMenuItem";
            setupToolStripMenuItem.Size = new Size(49, 20);
            setupToolStripMenuItem.Text = "Setup";
            // 
            // openMontageToolStripMenuItem
            // 
            openMontageToolStripMenuItem.Name = "openMontageToolStripMenuItem";
            openMontageToolStripMenuItem.Size = new Size(154, 22);
            openMontageToolStripMenuItem.Text = "Open Montage";
            // 
            // saveMontageToolStripMenuItem
            // 
            saveMontageToolStripMenuItem.Name = "saveMontageToolStripMenuItem";
            saveMontageToolStripMenuItem.Size = new Size(154, 22);
            saveMontageToolStripMenuItem.Text = "Save Montage";
            // 
            // exceptionsToolStripMenuItem
            // 
            exceptionsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { viewChannelsToolStripMenuItem });
            exceptionsToolStripMenuItem.Name = "exceptionsToolStripMenuItem";
            exceptionsToolStripMenuItem.Size = new Size(76, 20);
            exceptionsToolStripMenuItem.Text = "Exceptions";
            // 
            // viewChannelsToolStripMenuItem
            // 
            viewChannelsToolStripMenuItem.Name = "viewChannelsToolStripMenuItem";
            viewChannelsToolStripMenuItem.Size = new Size(151, 22);
            viewChannelsToolStripMenuItem.Text = "View Channels";
            // 
            // MainGUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1125, 830);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Name = "MainGUI";
            Text = "Charmeleon";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private StatusStrip statusStrip1;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem setupToolStripMenuItem;
        private ToolStripMenuItem openMontageToolStripMenuItem;
        private ToolStripMenuItem saveMontageToolStripMenuItem;
        private ToolStripMenuItem exceptionsToolStripMenuItem;
        private ToolStripMenuItem viewChannelsToolStripMenuItem;
    }
}
