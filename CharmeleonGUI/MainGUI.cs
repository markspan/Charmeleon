using System.Drawing.Drawing2D;
using System.Text.Json;

namespace Charmeleon
{
    public partial class MainGUI : Form
    {
        IImpedanceDriver? _driver = null;
        readonly bool _demo = false;
        int _demoValue = 255;
        WebServer?  _webServer       = null;
        double[] _lastKOhm = Enumerable.Repeat(-1.0, 64).ToArray();

        int centerX = 0, centerY = 0, formHeight = 0, formWidth = 0, fullRadius = 0;
        bool isDark = false;

        // Electrode state â€” drawn directly in OnPaint, not added as Controls.
        public Dictionary<string, ElectrodeState> electrodes = new();
        public Dictionary<string, (double Angle, double Radius)> electrodePositions = new();

        public Dictionary<string, Point> AUXPositions = new()
        {
            { "Left", new Point() }, { "Right", new Point() },
            { "Top",  new Point() }, { "Bottom", new Point() }
        };

        readonly Dictionary<string, (double X, double Y)> _auxFractions = new()
        {
            { "Left",   (0.85, 0.80) },
            { "Right",  (0.85, 0.72) },
            { "Top",    (0.90, 0.72) },
            { "Bottom", (0.90, 0.80) }
        };

        // Inline channel-edit textbox (one at a time, managed by the form).
        string?  _editingName = null;
        TextBox? _editBox     = null;

        // ------------------------------------------------------------------ //
        //  Constructor
        // ------------------------------------------------------------------ //

        public MainGUI(IImpedanceDriver? driver, bool demo)
        {
            _driver = driver;
            _demo   = demo;

            InitializeComponent();

            formHeight = ClientSize.Height;
            formWidth  = ClientSize.Width;
            fullRadius = (int)(formHeight * 0.43);
            centerX    = formWidth  / 2;
            centerY    = formHeight / 2;

            ElectrodeState.ApplyScale(Math.Max(24, Math.Min(80, (int)(fullRadius / 5.0 * 0.828))));
            SetAuxiliaryPositions();
            AddAuxiliaryElectrodes();
            DoubleBuffered = true;
            DrawColorMapBox(ElectrodeState.ColorMap);
            LoadConfig("Resources/1020Layout.json");

            if (_demo)
            {
                ElectrodeState.maxChannel = 64;
                Text      = "Charmeleon  [DEMO - no amplifier]";
                KeyPreview = true;
            }
            else
            {
                ElectrodeState.maxChannel = _driver!.ChannelCount;
                Text = "Charmeleon";
            }

            RefreshTimer.Interval = 300;
            RefreshTimer.Tick    += Redraw_Callback;
            RefreshTimer.Start();

            _webServer = new WebServer();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RefreshTimer.Stop();
            _webServer?.Dispose();
            _driver?.Dispose();
            base.OnFormClosed(e);
        }

        // ------------------------------------------------------------------ //
        //  Impedance refresh
        // ------------------------------------------------------------------ //

        void Redraw_Callback(object? source, EventArgs e)
        {
            if (_demo)
            {
                foreach (var (_, el) in electrodes)
                {
                    int ch = el.HardwareChannel - 1;
                    if (ch >= 0 && ch < _lastKOhm.Length)
                    {
                        if (el.IsActive) { el.Value = _demoValue; _lastKOhm[ch] = _demoValue; }
                        else             {                          _lastKOhm[ch] = -1; }
                    }
                }
                ImpedanceSource.Update(_lastKOhm, electrodes, electrodePositions, _auxFractions);
                Invalidate();
                return;
            }

            if (_driver == null) return;
            double[] impedances = _driver.GetImpedancesKOhm();
            foreach (var (_, el) in electrodes)
            {
                int ch = el.HardwareChannel - 1;
                if (ch >= 0 && ch < impedances.Length && el.IsActive)
                {
                    el.Value = (int)Math.Min(255, impedances[ch]);
                    if (ch < _lastKOhm.Length) _lastKOhm[ch] = impedances[ch];
                }
                else
                {
                    el.Value = 0;
                    if (ch >= 0 && ch < _lastKOhm.Length) _lastKOhm[ch] = -1;
                }
            }
            ImpedanceSource.Update(_lastKOhm, electrodes, electrodePositions, _auxFractions);
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_demo)
            {
                if (keyData == Keys.Up)   { _demoValue = Math.Min(255, _demoValue + 1); return true; }
                if (keyData == Keys.Down) { _demoValue = Math.Max(0,   _demoValue - 1); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ //
        //  Electrode layout helpers
        // ------------------------------------------------------------------ //

        void SetAuxiliaryPositions()
        {
            foreach (var name in AUXPositions.Keys.ToList())
            {
                var (fx, fy) = _auxFractions.TryGetValue(name, out var f) ? f : (0.85, 0.80);
                AUXPositions[name] = new Point((int)(formWidth * fx), (int)(formHeight * fy));
                if (electrodes.TryGetValue(name, out var el)) el.Center = AUXPositions[name];
            }
        }

        void AddAuxiliaryElectrodes()
        {
            foreach (var aux in AUXPositions)
                if (!electrodes.ContainsKey(aux.Key))
                    electrodes[aux.Key] = new ElectrodeState { LabelText = aux.Key, Center = aux.Value };
        }

        void AddElectrodes()
        {
            int elSize = Math.Max(24, Math.Min(80, (int)(fullRadius / 5.0 * 0.828)));
            ElectrodeState.ApplyScale(elSize);

            foreach (var (name, (angle, radius)) in electrodePositions)
            {
                double rad = angle * Math.PI / 180.0;
                int x = centerX + (int)(Math.Cos(rad) * (radius * (fullRadius / 5.0)));
                int y = centerY - (int)(Math.Sin(rad) * (radius * (fullRadius / 5.0)));
                if (electrodes.TryGetValue(name, out var existing))
                    existing.Center = new Point(x, y);
                else
                    electrodes[name] = new ElectrodeState { LabelText = name, Center = new Point(x, y) };
            }
        }

        // ------------------------------------------------------------------ //
        //  Drawing
        // ------------------------------------------------------------------ //

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Head circle and guide rings
            using var dashPen = new Pen(Color.Gray, 1) { DashStyle = DashStyle.Dash };
            g.DrawEllipse(Pens.Black, centerX - fullRadius, centerY - fullRadius, fullRadius * 2, fullRadius * 2);
            foreach (int r in new[] { 4, 3, 2, 1 })
            {
                int cr = (fullRadius * r) / 5;
                g.DrawEllipse(dashPen, centerX - cr, centerY - cr, 2 * cr, 2 * cr);
            }

            using var dotPen = new Pen(Color.Gray) { DashStyle = DashStyle.Dot };
            g.DrawLine(dotPen, 0, centerY, (int)(formHeight * 1.125), centerY);
            g.DrawLine(dotPen, centerX, 0, centerX, formHeight);

            // Ear ellipses â€” fixed to the sides of the head circle, centred on the horizontal midline
            int earW = fullRadius / 7;
            int earH = fullRadius / 4;
            g.DrawEllipse(Pens.Black, centerX - fullRadius - earW, centerY - earH / 2, earW, earH);
            g.DrawEllipse(Pens.Black, centerX + fullRadius,         centerY - earH / 2, earW, earH);

            // All electrodes in one pass â€” true transparency, no overlapping rectangles
            DrawElectrodes(g);

            // Colormap value labels
            int[] labelList = { 2, 5, 10, 20, 50, 100, 200, 256 };
            var P = pictureBox1.Location;
            float cmScale = pictureBox1.Height / 256f;
            using var lBrush = new SolidBrush(SystemColors.ControlText);
            using var lFont  = new Font("Segoe UI", 8);
            var lFmt = new StringFormat { Alignment = StringAlignment.Far };
            foreach (int y in labelList)
                g.DrawString(y.ToString(), lFont, lBrush, P.X - 2, P.Y + y * cmScale, lFmt);
        }

        void DrawElectrodes(Graphics g)
        {
            int s    = ElectrodeState.ScaledSize;
            int cd   = Math.Max(12, s - 20);   // circle diameter â€” matches old UserControl margin
            int cr   = cd / 2;
            float impFontSz = Math.Max(6f, cd * 0.32f);
            float lblFontSz = Math.Max(5f, cd * 0.28f);
            using var impFont = new Font("Segoe UI", impFontSz, FontStyle.Bold);
            using var lblFont = new Font("Segoe UI", lblFontSz);
            using var fmt     = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            foreach (var (_, el) in electrodes)
            {
                var circle = new Rectangle(el.Center.X - cr, el.Center.Y - cr, cd, cd);
                var fill   = el.IsActive ? ElectrodeState.ColorMap[el.Value] : Color.LightGray;
                using (var b = new SolidBrush(fill)) g.FillEllipse(b, circle);
                g.DrawEllipse(Pens.Black, circle);

                if (el.Editing) continue;

                string inner = ElectrodeState.viewHWChannel
                    ? el.LabelText
                    : (el.Value == 255 ? "Inf" : el.Value.ToString());
                Color tc = (el.IsActive && el.Value > 220) ? Color.White : ForeColor;
                using (var b = new SolidBrush(tc))
                    g.DrawString(inner, ElectrodeState.viewHWChannel ? lblFont : impFont, b, circle, fmt);

                string below = ElectrodeState.viewHWChannel
                    ? el.HardwareChannel.ToString()
                    : el.LabelText;
                var lblRect = new RectangleF(el.Center.X - cr, el.Center.Y + cr + 3, cd, 18);
                using (var b = new SolidBrush(ForeColor))
                    g.DrawString(below, lblFont, b, lblRect, fmt);
            }
        }

        // ------------------------------------------------------------------ //
        //  Click handling
        // ------------------------------------------------------------------ //

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            CommitActiveEdit();

            int r = ElectrodeState.ScaledSize / 2;
            foreach (var (name, el) in electrodes)
            {
                double dx = e.X - el.Center.X;
                double dy = e.Y - el.Center.Y;
                if (dx * dx + dy * dy <= (r + 4) * (r + 4))
                {
                    bool inLabel = e.Y > el.Center.Y + r - 6;
                    if (ElectrodeState.viewHWChannel && inLabel)
                        ShowChannelEditor(name, el);
                    else
                        el.IsActive = !el.IsActive;
                    Invalidate();
                    return;
                }
            }
        }

        void ShowChannelEditor(string name, ElectrodeState el)
        {
            int s = ElectrodeState.ScaledSize;
            int r = s / 2;
            var box = new TextBox
            {
                Text      = el.HardwareChannel.ToString(),
                Location  = new Point(el.Center.X - r, el.Center.Y + r - 4),
                Size      = new Size(s, 20),
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 3
            };
            box.KeyDown  += (_, ke) => { if (ke.KeyCode == Keys.Enter) CommitActiveEdit(); };
            box.KeyPress += (_, ke) => { if (!char.IsDigit(ke.KeyChar) && ke.KeyChar != (char)Keys.Back) ke.Handled = true; };
            box.Leave    += (_, _)  => CommitActiveEdit();
            _editingName  = name;
            el.Editing    = true;
            _editBox      = box;
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
            Invalidate();
        }

        void CommitActiveEdit()
        {
            if (_editBox == null || _editingName == null) return;

            // Capture and clear the fields first. Controls.Remove below removes the
            // focused textbox, which fires its Leave event synchronously and re-enters
            // this method; clearing the fields up front makes that re-entry bail at the
            // guard above instead of dereferencing a box we are about to dispose.
            var box  = _editBox;
            var name = _editingName;
            _editBox     = null;
            _editingName = null;

            if (electrodes.TryGetValue(name, out var el))
            {
                if (int.TryParse(box.Text, out int ch))
                    el.HardwareChannel = Math.Max(0, Math.Min(ElectrodeState.maxChannel, ch));
                el.Editing = false;
            }
            Controls.Remove(box);
            box.Dispose();
            Invalidate();
        }

        // ------------------------------------------------------------------ //
        //  Resize
        // ------------------------------------------------------------------ //

        void MainGUI_Resize(object sender, EventArgs e)
        {
            formHeight = ClientSize.Height;
            formWidth  = ClientSize.Width;
            fullRadius = (int)(formHeight * 0.43);
            centerX    = formWidth  / 2;
            centerY    = formHeight / 2;
            SetAuxiliaryPositions();
            AddElectrodes();
            DrawColorMapBox(ElectrodeState.ColorMap);
            Invalidate();
        }

        // ------------------------------------------------------------------ //
        //  Configuration load / save
        // ------------------------------------------------------------------ //

        void LoadConfig(string fileName)
        {
            if (fileName == "")
            {
                using var dlg = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Load Configuration" };
                if (dlg.ShowDialog() != DialogResult.OK) return;
                fileName = dlg.FileName;
            }
            Dictionary<string, ElectrodeControlData> data;
            try
            {
                data = JsonSerializer.Deserialize<Dictionary<string, ElectrodeControlData>>(
                    File.ReadAllText(fileName),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? new();
            }
            catch (Exception ex) { MessageBox.Show($"Error loading configuration: {ex.Message}"); return; }

            // Remove existing EEG electrodes (keep AUX)
            foreach (var name in electrodes.Keys.Except(AUXPositions.Keys).ToList())
                electrodes.Remove(name);
            electrodePositions.Clear();

            foreach (var (name, cfg) in data)
            {
                if (AUXPositions.ContainsKey(name))
                {
                    if (cfg.X.HasValue && cfg.Y.HasValue)
                    {
                        _auxFractions[name] = (cfg.X.Value, cfg.Y.Value);
                        SetAuxiliaryPositions();
                    }
                }
                else if (cfg.Angle.HasValue && cfg.Radius.HasValue)
                    electrodePositions[name] = (cfg.Angle.Value, cfg.Radius.Value);
            }
            AddElectrodes();

            foreach (var (name, cfg) in data)
            {
                if (electrodes.TryGetValue(name, out var el))
                {
                    el.LabelText        = cfg.LabelText;
                    el.HardwareChannel  = cfg.HardwareChannel;
                    el.IsActive         = cfg.HardwareChannel > 0 &&
                                          cfg.HardwareChannel <= ElectrodeState.maxChannel &&
                                          cfg.IsActive;
                }
            }
        }

        void openMontageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadConfig("");
        }

        void saveMontageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var data = electrodes.ToDictionary(kv => kv.Key, kv =>
            {
                bool isAux = AUXPositions.ContainsKey(kv.Key);
                _auxFractions.TryGetValue(kv.Key, out var frac);
                electrodePositions.TryGetValue(kv.Key, out var pos);
                return new ElectrodeControlData
                {
                    IsActive        = kv.Value.IsActive,
                    LabelText       = kv.Value.LabelText,
                    HardwareChannel = kv.Value.HardwareChannel,
                    Angle           = isAux ? null : pos.Angle,
                    Radius          = isAux ? null : pos.Radius,
                    X               = isAux ? frac.X : null,
                    Y               = isAux ? frac.Y : null
                };
            });
            using var dlg = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Save Configuration" };
            if (dlg.ShowDialog() == DialogResult.OK)
                File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }

        // ------------------------------------------------------------------ //
        //  Create Montage
        // ------------------------------------------------------------------ //

        void create1020ToolStripMenuItem_Click(object sender, EventArgs e)
            => ApplyGeneratedMontage(MontageGenerator.Generate1020(), "10/20");

        void createEquidistantToolStripMenuItem_Click(object sender, EventArgs e)
            => ApplyGeneratedMontage(MontageGenerator.GenerateEquidistant(), "Equidistant");

        void ApplyGeneratedMontage(Dictionary<string, ElectrodeControlData> layout, string name)
        {
            if (MessageBox.Show($"Save the {name} layout to a file?",
                    "Create Montage", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                MontageGenerator.SaveToFile(layout);

            foreach (var key in electrodes.Keys.Except(AUXPositions.Keys).ToList())
                electrodes.Remove(key);
            electrodePositions.Clear();

            foreach (var (key, cfg) in layout)
            {
                if (AUXPositions.ContainsKey(key)) continue;
                if (cfg.Angle.HasValue && cfg.Radius.HasValue)
                    electrodePositions[key] = (cfg.Angle.Value, cfg.Radius.Value);
            }
            AddElectrodes();

            // Carry the generated hardware channels and active state onto the
            // electrode states (AddElectrodes only sets positions, not channels).
            foreach (var (key, cfg) in layout)
                if (electrodes.TryGetValue(key, out var el))
                {
                    el.HardwareChannel = cfg.HardwareChannel;
                    el.IsActive        = cfg.IsActive;
                }
            Invalidate();
        }




        // ------------------------------------------------------------------ //
        //  UI helpers
        // ------------------------------------------------------------------ //

        void DrawColorMapBox(Color[] colorMap)
        {
            int auxX1 = AUXPositions.Values.Min(p => p.X);
            int auxX2 = AUXPositions.Values.Max(p => p.X);
            int auxCX = (auxX1 + auxX2) / 2;
            int auxY1 = AUXPositions.Values.Min(p => p.Y);

            int cmWidth  = 40;
            int cmHeight = Math.Max(64, (auxY1 - 16) * 2 / 3);
            pictureBox1.Size     = new Size(cmWidth, cmHeight);
            pictureBox1.Location = new Point(auxCX, (auxY1 - cmHeight) / 2);

            var bmp = new Bitmap(cmWidth, cmHeight);
            using (var g = Graphics.FromImage(bmp))
                for (int i = 0; i < 256; i++)
                {
                    int y0 = (int)(i       * cmHeight / 256.0);
                    int y1 = (int)((i + 1) * cmHeight / 256.0);
                    using var pen = new Pen(colorMap[i]);
                    for (int py = y0; py < y1; py++)
                        g.DrawLine(pen, 0, py, cmWidth - 1, py);
                }
            pictureBox1.Image = bmp;
        }

        void viewChannelsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ElectrodeState.viewHWChannel = !ElectrodeState.viewHWChannel;
            Invalidate();
        }

        void AboutCharmeleonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var about = new aboutForm();
            ApplyTheme(about, isDark);
            about.ShowDialog();
        }

        void ToggleThemeToolStripMenuItem_Click(object sender, EventArgs e) => ToggleTheme(this);

        void ToggleTheme(Control root) { isDark = !isDark; ApplyTheme(root, isDark); }

        void ApplyTheme(Control c, bool dark)
        {
            if (c == null) return;
            c.BackColor = dark ? Color.FromArgb(80, 80, 80) : SystemColors.Control;
            c.ForeColor = dark ? Color.White : SystemColors.ControlText;
            foreach (Control child in c.Controls) ApplyTheme(child, dark);
        }

        void webViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_webServer is { Started: false })
            {
                MessageBox.Show("Could not start the Web View server:\n" + _webServer.ErrorMessage +
                    "\n\nRun:  netsh http add urlacl url=http://*:8765/ user=Everyone",
                    "Web View", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            using var dlg = new WebDialog();
            ApplyTheme(dlg, isDark);
            dlg.ShowDialog(this);
        }

        void resizeToolStripMenuItem_Click(object sender, EventArgs e) => MainGUI_Resize(sender, e);
    }
}



