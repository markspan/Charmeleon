using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Text.Json;
using System.Timers;

namespace CharmeleonGUI
{
    /// <summary>
    /// Main GUI for displaying the electrode configuration and controlling the Refa amplifier.
    /// </summary>
    public partial class MainGUI : Form
    {
        // Instance of the amplifier driver
        RtDeviceDriver.RefaDriver? Amplifier = null;

        // Center and size of the form, used for drawing electrodes and circles
        int centerX = 0;
        int centerY = 0;
        int formHeight = 0;
        int formWidth = 0;
        static bool viewChannelNumbers = false;

        // Full radius of the display circle (90% of form height)
        int fullRadius = 0;

        // Dictionary holding references to all electrode controls
        public Dictionary<string, ElectrodeControl> electrodes = new();

        // Dictionary holding electrode positions (angles and radii)
        public Dictionary<string, (double Angle, double Radius)> electrodePositions = new()
        {
            // Define electrode positions (midline, C-line, outer circle, etc.)
            { "Nz", (90, 5) }, { "FPz", (90, 4) }, { "AFz", (90, 3) }, { "Fz", (90, 2) }, { "FCz", (90, 1) },
            { "Cz", (0, 0) }, { "CPz", (-90, 1) }, { "Pz", (-90, 2) }, { "POz", (-90, 3) },
            { "Oz", (-90, 4) }, { "Iz", (-90, 5) },
            { "T7",(180, 4) }, { "C5",(180, 3) }, { "C3",(180, 2) }, { "C1",(180, 1) },  { "C2",(0, 1) },
            { "C4",(0, 2) },  { "C6",(0, 3) },  { "T8",(0, 4) }, {"A2" , (0,5) }, { "A1", (180, 5) },
            {  "F9",(144, 5) }, {  "FT9",(162, 5) }, {  "TP9",(198, 5) }, {  "P9",(216, 5) },
            { "F10",( 36, 5) }, { "FT10",( 18, 5) }, { "TP10",( -18, 5) }, { "P10",( -36, 5) },
            { "Fp1",(108, 4) }, { "AF7",(126, 4) }, { "F7",(144, 4) }, { "FT7",(162, 4) },
            { "Fp2",( 72, 4) }, { "AF8",( 54, 4) }, { "F8",( 36, 4) }, { "FT8",( 18, 4) },
            { "O1", (-108, 4) }, { "PO7",(-126, 4) }, { "P7",(-144, 4) }, { "TP7",(-162, 4) },
            { "O2", ( -72, 4) }, { "PO8",( -54, 4) }, { "P8",( -36, 4) }, { "TP8",( -18, 4) },
            { "AF4", (  68, 3.2) }, { "F6", (  43, 3.1) }, { "F4", (  55, 2.5 )}, { "F2", (  70, 2.15) },
            { "AF3", ( 112, 3.2) }, { "F5", ( 137, 3.1) }, { "F3", ( 125, 2.5 )}, { "F1", ( 110, 2.15) },
            { "PO4", ( -68, 3.2) }, { "P6", ( -43, 3.1) }, { "P4", ( -55, 2.45)}, { "P2", ( -70, 2.15) },
            { "PO3", (-112, 3.2) }, { "P5", (-137, 3.1) }, { "P3", (-125, 2.45)}, { "P1", (-110, 2.15) },
            { "FC5",(158, 3.05) }, { "FC3",(151, 2.1) }, { "FC1",(135, 1.35) },
            { "CP5",(-158, 3.05)}, { "CP3",(-151, 2.1)}, { "CP1",(-135, 1.35)},
            { "FC6",(  22, 3.05)}, { "FC4",(  29, 2.1)}, { "FC2",( 45, 1.35) },
            { "CP6",( -22, 3.05)}, { "CP4",( -29, 2.1)}, { "CP2",( -45, 1.35)},
            { "PO9",(-126, 5) }, { "PO10",(-54, 5) },  { "I1", (-105, 5) },  { "I2", (-75, 5) }
        };

        // Dictionary for auxiliary (EOG) channel positions
        public Dictionary<string, Point> AUXPositions = new()
        {
            { "Left",  new Point()},
            { "Right", new Point()},
            { "Top",   new Point()},
            { "Bottom",new Point()}
        };

        /// <summary>
        /// Initializes the MainGUI form.
        /// </summary>
        public MainGUI()
        {
            InitializeComponent();

            this.formHeight = this.ClientSize.Height;
            this.formWidth = this.ClientSize.Width;
            this.fullRadius = (int)(this.formHeight * 0.43);
            this.centerX = formHeight / 2;
            this.centerY = formHeight / 2;

            // Set initial positions for the auxiliary channels
            SetAuxiliaryPositions();

            // Add auxiliary channels to the GUI
            AddAuxiliaryElectrodes();

            // Enable double buffering to prevent flickering during redraws
            this.DoubleBuffered = true;

            // Add primary electrodes to the GUI
            AddElectrodes();

            //Draw the colormap
            DrawColorMapBox(ElectrodeControl.ColorMap);
            applyMontage(LoadElectrodeData("Resources/DefaultMontage.json"));

            // Initialize the Refa amplifier
            InitializeAmplifier();

            // Go into the main loop: do that in the timer.
            this.RefreshTimer = new System.Windows.Forms.Timer();
            this.RefreshTimer.Tick += Redraw_Callback;
            this.RefreshTimer.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void Redraw_Callback(object? source, EventArgs e)
        {
            if (Amplifier != null)
            {
                unsafe
                {
                    int* Impedances = (int*)Amplifier.getImpedances();
                    if (Impedances != null)
                    {
                        foreach ((string name, ElectrodeControl el) in electrodes)
                        {
                            el.Value = Impedances[el.hardwareChannel - 1];
                            el.Invalidate();
                        }
                    }
                    else
                    {
                        foreach ((string name, ElectrodeControl el) in electrodes)
                        {
                            el.Value = 0;
                            el.Invalidate();
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Draws the Legenda based on the current colours
        /// </summary>
        private void DrawColorMapBox(Color[] colorMap)
        {
            // Create a new Bitmap that is 100 pixels wide and 256 pixels tall
            int width = 100;
            int height = 512;
            pictureBox1.Size = new Size(width, height);

            Bitmap bmp = new Bitmap(width, height);

            // Use Graphics to draw on the bitmap
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                for (int y = 0; y < 256; y++)
                {
                    using (Pen pen = new Pen(colorMap[y]))
                    {
                        g.DrawLine(pen, 0, 1 + (y * 2), 50, 1 + (y * 2));
                        g.DrawLine(pen, 0, y * 2, 50, y * 2);
                    }
                }
            }
            // Assign the generated bitmap to a PictureBox or any other control
            pictureBox1.Image = bmp;
        }



        /// <summary>
        /// Sets the positions for the auxiliary (EOG) channels.
        /// </summary>
        private void SetAuxiliaryPositions()
        {
            AUXPositions["Left"] = new Point((int)(this.formWidth * .85), (int)(this.formHeight * .8));
            AUXPositions["Right"] = new Point((int)(this.formWidth * .85), (int)(this.formHeight * .72));
            AUXPositions["Top"] = new Point((int)(this.formWidth * .90), (int)(this.formHeight * .8));
            AUXPositions["Bottom"] = new Point((int)(this.formWidth * .90), (int)(this.formHeight * .72));
        }

        /// <summary>
        /// Adds auxiliary electrodes (EOG channels) to the GUI.
        /// </summary>
        private void AddAuxiliaryElectrodes()
        {
            foreach (var AUX in AUXPositions)
            {
                ElectrodeControl electrode = new ElectrodeControl
                {
                    LabelText = AUX.Key,
                    Location = AUX.Value,
                    Name = AUX.Key,
                    BackColor = Color.Transparent
                };
                this.Controls.Add(electrode);
                this.electrodes.Add(AUX.Key, electrode);
            }
        }

        /// <summary>
        /// Adds electrodes to the main GUI based on predefined positions.
        /// </summary>
        private void AddElectrodes()
        {
            foreach (var (name, (angle, radius)) in electrodePositions)
            {
                ElectrodeControl electrode = new ElectrodeControl();
                double radians = angle * Math.PI / 180.0;

                int x = this.centerX + (int)(Math.Cos(radians) * (radius * (this.fullRadius / 5.0)));
                int y = this.centerY - (int)(Math.Sin(radians) * (radius * (this.fullRadius / 5.0)));
                electrode.LabelText = name;
                electrode.Location = new Point(x - (electrode.Width / 2), y - (electrode.Height / 2));
                electrode.Name = name;
                electrode.BackColor = Color.Transparent;
                this.Controls.Add(electrode);
                this.electrodes.Add(name, electrode);
            }
        }

        /// <summary>
        /// Initializes the Refa amplifier by starting the device and setting it to impedance measurement mode.
        /// </summary>
        private void InitializeAmplifier()
        {
            Amplifier = new RtDeviceDriver.RefaDriver();

            try
            {
                Amplifier.Initialize();
                Amplifier.InitiateImpedanceDataMode();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message + Environment.NewLine + "Could you Check whether the Refa (Blue Box) is actually on?");
                throw;
            }
        }

        /// <summary>
        /// Paints the background and electrodes onto the GUI.
        /// </summary>
        /// <param name="e">The paint event arguments.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Pen innercirclePen = new Pen(Color.Gray, 1); // Blue color, 2px width
            innercirclePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            // Draw the outer circle to represent the electrode array
            g.DrawEllipse(Pens.Black, this.centerX - this.fullRadius, this.centerY - this.fullRadius, this.fullRadius * 2, this.fullRadius * 2);
            int circleRadiusn = (this.fullRadius * 4) / 5;
            g.DrawEllipse(innercirclePen, this.centerX - circleRadiusn, this.centerY - circleRadiusn, 2 * circleRadiusn, 2 * circleRadiusn);
            circleRadiusn = (this.fullRadius * 3) / 5;
            g.DrawEllipse(innercirclePen, this.centerX - circleRadiusn, this.centerY - circleRadiusn, 2 * circleRadiusn, 2 * circleRadiusn);
            circleRadiusn = (this.fullRadius * 2) / 5;
            g.DrawEllipse(innercirclePen, this.centerX - circleRadiusn, this.centerY - circleRadiusn, 2 * circleRadiusn, 2 * circleRadiusn);
            circleRadiusn = this.fullRadius / 5;
            g.DrawEllipse(innercirclePen, this.centerX - circleRadiusn, this.centerY - circleRadiusn, 2 * circleRadiusn, 2 * circleRadiusn);

            // Draw the cross (dotted grey lines)
            Pen dottedPen = new Pen(Color.Gray)
            {
                DashStyle = System.Drawing.Drawing2D.DashStyle.Dot
            };

            // Horizontal line (centered at the form)
            g.DrawLine(dottedPen, 0, this.centerY, (int)(formHeight * 1.125), this.centerY);
            // Vertical line (centered at the form)
            g.DrawLine(dottedPen, this.centerX, 0, this.centerX, formHeight);

            // Ears:
            Point L = this.electrodes["A1"].Location;
            Point R = this.electrodes["A2"].Location;
            int RadX = this.fullRadius / 14;
            int RadY = this.fullRadius / 7;

            g.DrawEllipse(Pens.Black, L.X - 20, L.Y - 20, 2 * RadX, 2 * RadY);
            g.DrawEllipse(Pens.Black, R.X + 18, R.Y - 20, 2 * RadX, 2 * RadY);

            int[] LabelList = { 2, 5, 10, 20, 50, 100, 200, 256 };
            Point P = this.pictureBox1.Location;

            foreach (int y in LabelList)
            {
                using (Brush brush = new SolidBrush(SystemColors.ControlText)) // standard label text color
                {
                    StringFormat format = new StringFormat();
                    format.Alignment = StringAlignment.Far; // Right align
                    g.DrawString(y.ToString(), new Font("Segoe UI", 8, FontStyle.Regular), brush, P.X - 5, P.Y + (y * 2), format);
                }
            }

        }

        /// <summary>
        /// Handles form resizing, adjusting electrode positions dynamically.
        /// </summary>
        /// <param name="sender">The sender of the resize event.</param>
        /// <param name="e">The resize event arguments.</param>
        private void MainGUI_Resize(object sender, EventArgs e)
        {
            this.formHeight = this.ClientSize.Height;
            this.formWidth = this.ClientSize.Width;
            this.fullRadius = (int)(this.formHeight * 0.43);
            this.centerX = formWidth / 2;
            this.centerY = formHeight / 2;

            // Reposition auxiliary electrodes
            SetAuxiliaryPositions();

            // Reposition primary electrodes
            AddElectrodes();
            this.Invalidate();
        }

        private void applyMontage(Dictionary<string, ElectrodeControlData> loadedData)
        {
            foreach (var kvp in loadedData)
            {
                string name = kvp.Key;
                ElectrodeControlData data = kvp.Value;

                // Try to find the control by name
                Control[] matches = this.Controls.Find(name, true);
                if (matches.Length > 0 && matches[0] is ElectrodeControl control)
                {
                    control.IsActive = data.IsActive;
                    // control.Value = data.Value;
                    control.LabelText = data.LabelText;
                    control.HardwareChannel = data.HardwareChannel;
                }
            }
        }
        private void openMontageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            applyMontage(LoadElectrodeData(""));
        }
        public Dictionary<string, ElectrodeControlData> LoadElectrodeData(string fileName)
        {
            if (fileName == "")
            {
                using OpenFileDialog dialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = "Load Electrode Configuration"
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                    fileName = dialog.FileName;
                else return new Dictionary<string, ElectrodeControlData>();
            }
            try
            {
                string json = File.ReadAllText(fileName);
                var deserializedData = JsonSerializer.Deserialize<Dictionary<string, ElectrodeControlData>>(json);
                return deserializedData ?? new Dictionary<string, ElectrodeControlData>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading montage: {ex.Message}");
                return new Dictionary<string, ElectrodeControlData>();
            }
        }

        private void saveMontageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Save controls
            var serializableData = electrodes.ToDictionary(
               kv => kv.Key,
               kv => new ElectrodeControlData
               {
                   IsActive = kv.Value.IsActive,
                   //Value = kv.Value.Value,
                   LabelText = kv.Value.LabelText,
                   HardwareChannel = kv.Value.HardwareChannel
               }
            );

            using SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Save Electrode Configuration"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string json = JsonSerializer.Serialize(serializableData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dialog.FileName, json);
            }
        }

        private void viewChannelsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            viewChannelNumbers = !viewChannelNumbers;
            ElectrodeControl.viewHWChannel = viewChannelNumbers;
            this.Invalidate();
        }

        private void aboutCharmeleonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (aboutForm about = new aboutForm())
            {
                about.ShowDialog();
            }
        }

        private void toggleThemeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleTheme(this);
        }

        bool isDark = false;

        void ToggleTheme(Control root)
        {
            isDark = !isDark;
            ApplyTheme(root, isDark);
        }

        void ApplyTheme(Control control, bool dark)
        {
            if (dark)
            {
                control.BackColor = Color.FromArgb(60, 60, 60); // Dark background
                control.ForeColor = Color.White;
            }
            else
            {
                control.BackColor = SystemColors.Control; // Default light bg
                control.ForeColor = SystemColors.ControlText;
            }

            foreach (Control child in control.Controls)
            {
                ApplyTheme(child, dark);
            }
        }

    }
}
