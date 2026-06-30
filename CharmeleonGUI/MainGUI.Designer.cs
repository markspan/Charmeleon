namespace Charmeleon
{
    partial class MainGUI
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainGUI));
            statusStrip1 = new StatusStrip();
            menuStrip1 = new MenuStrip();
            setupToolStripMenuItem = new ToolStripMenuItem();
            openMontageToolStripMenuItem = new ToolStripMenuItem();
            saveMontageToolStripMenuItem = new ToolStripMenuItem();
            createMontageToolStripMenuItem = new ToolStripMenuItem();
            create1020ToolStripMenuItem = new ToolStripMenuItem();
            createEquidistantToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            viewChannelsToolStripMenuItem = new ToolStripMenuItem();
            toggleThemeToolStripMenuItem = new ToolStripMenuItem();
            broadcastToolStripMenuItem = new ToolStripMenuItem();
            webViewToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            aboutCharmeleonToolStripMenuItem = new ToolStripMenuItem();
            RefreshTimer = new System.Windows.Forms.Timer(components);
            pictureBox1 = new PictureBox();
            menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // statusStrip1
            // 
            statusStrip1.Location = new Point(0, 963);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1203, 22);
            statusStrip1.TabIndex = 1;
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { setupToolStripMenuItem, viewToolStripMenuItem, broadcastToolStripMenuItem, aboutToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1203, 24);
            menuStrip1.TabIndex = 2;
            // 
            // setupToolStripMenuItem
            // 
            setupToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openMontageToolStripMenuItem, saveMontageToolStripMenuItem, new ToolStripSeparator(), createMontageToolStripMenuItem });
            //
            // createMontageToolStripMenuItem
            //
            createMontageToolStripMenuItem.Name = "createMontageToolStripMenuItem";
            createMontageToolStripMenuItem.Text = "Create Montage";
            createMontageToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { create1020ToolStripMenuItem, createEquidistantToolStripMenuItem });
            //
            // create1020ToolStripMenuItem
            //
            create1020ToolStripMenuItem.Name = "create1020ToolStripMenuItem";
            create1020ToolStripMenuItem.Text = "10/20 Layout";
            create1020ToolStripMenuItem.Click += create1020ToolStripMenuItem_Click;
            //
            // createEquidistantToolStripMenuItem
            //
            createEquidistantToolStripMenuItem.Name = "createEquidistantToolStripMenuItem";
            createEquidistantToolStripMenuItem.Text = "Equidistant Layout";
            createEquidistantToolStripMenuItem.Click += createEquidistantToolStripMenuItem_Click;
            setupToolStripMenuItem.Name = "setupToolStripMenuItem";
            setupToolStripMenuItem.Size = new Size(49, 20);
            setupToolStripMenuItem.Text = "Setup";
            // 
            // openMontageToolStripMenuItem
            // 
            openMontageToolStripMenuItem.Name = "openMontageToolStripMenuItem";
            openMontageToolStripMenuItem.Size = new Size(154, 22);
            openMontageToolStripMenuItem.Text = "Open Montage";
            openMontageToolStripMenuItem.Click += openMontageToolStripMenuItem_Click;
            // 
            // saveMontageToolStripMenuItem
            // 
            saveMontageToolStripMenuItem.Name = "saveMontageToolStripMenuItem";
            saveMontageToolStripMenuItem.Size = new Size(154, 22);
            saveMontageToolStripMenuItem.Text = "Save Montage";
            saveMontageToolStripMenuItem.Click += saveMontageToolStripMenuItem_Click;
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { viewChannelsToolStripMenuItem, toggleThemeToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(44, 20);
            viewToolStripMenuItem.Text = "View";
            // 
            // viewChannelsToolStripMenuItem
            // 
            viewChannelsToolStripMenuItem.Name = "viewChannelsToolStripMenuItem";
            viewChannelsToolStripMenuItem.Size = new Size(151, 22);
            viewChannelsToolStripMenuItem.Text = "View Channels";
            viewChannelsToolStripMenuItem.Click += viewChannelsToolStripMenuItem_Click;
            // 
            // toggleThemeToolStripMenuItem
            // 
            toggleThemeToolStripMenuItem.Name = "toggleThemeToolStripMenuItem";
            toggleThemeToolStripMenuItem.Size = new Size(151, 22);
            toggleThemeToolStripMenuItem.Text = "Toggle Theme";
            toggleThemeToolStripMenuItem.Click += ToggleThemeToolStripMenuItem_Click;
            // 
            // broadcastToolStripMenuItem
            // 
            broadcastToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { webViewToolStripMenuItem });
            broadcastToolStripMenuItem.Name = "broadcastToolStripMenuItem";
            broadcastToolStripMenuItem.Size = new Size(71, 20);
            broadcastToolStripMenuItem.Text = "Web";
            //
            // webViewToolStripMenuItem
            //
            webViewToolStripMenuItem.Name = "webViewToolStripMenuItem";
            webViewToolStripMenuItem.Text = "Web View...";
            webViewToolStripMenuItem.Click += webViewToolStripMenuItem_Click;
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutCharmeleonToolStripMenuItem });
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(52, 20);
            aboutToolStripMenuItem.Text = "About";
            // 
            // aboutCharmeleonToolStripMenuItem
            // 
            aboutCharmeleonToolStripMenuItem.Name = "aboutCharmeleonToolStripMenuItem";
            aboutCharmeleonToolStripMenuItem.Size = new Size(152, 22);
            aboutCharmeleonToolStripMenuItem.Text = "About Charmeleon";
            aboutCharmeleonToolStripMenuItem.Click += AboutCharmeleonToolStripMenuItem_Click;
            // 
            // RefreshTimer
            // 
            RefreshTimer.Interval = 300;
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(1063, 48);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(50, 512);
            pictureBox1.TabIndex = 3;
            pictureBox1.TabStop = false;
            // 
            // MainGUI
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1183, 887);
            Controls.Add(pictureBox1);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MainMenuStrip = menuStrip1;
            Name = "MainGUI";
            Text = "Charmeleon";
            Resize += MainGUI_Resize;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        private StatusStrip statusStrip1;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem setupToolStripMenuItem;
        private ToolStripMenuItem openMontageToolStripMenuItem;
        private ToolStripMenuItem saveMontageToolStripMenuItem;
        private ToolStripMenuItem viewToolStripMenuItem;
        private ToolStripMenuItem viewChannelsToolStripMenuItem;
        private ToolStripMenuItem toggleThemeToolStripMenuItem;
        private ToolStripMenuItem broadcastToolStripMenuItem;
        private ToolStripMenuItem webViewToolStripMenuItem;
        private System.Windows.Forms.Timer RefreshTimer;
        private PictureBox pictureBox1;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem aboutCharmeleonToolStripMenuItem;
        private ToolStripMenuItem createMontageToolStripMenuItem;
        private ToolStripMenuItem create1020ToolStripMenuItem;
        private ToolStripMenuItem createEquidistantToolStripMenuItem;
    }
}



