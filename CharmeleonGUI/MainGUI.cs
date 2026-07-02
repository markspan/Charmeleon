using System.Drawing.Drawing2D;
using System.Text.Json;

namespace Charmeleon
{
    /// <summary>
    /// Main window. Draws the head map, runs the refresh timer that reads live
    /// impedances from the amplifier (or the demo value), handles electrode
    /// clicks and channel editing, and loads/saves montages. Electrodes are
    /// painted directly in <see cref="OnPaint"/> rather than being child controls,
    /// which gives true transparency with no overlapping rectangles.
    /// </summary>
    public partial class MainGUI : Form
    {
        readonly IImpedanceDriver? _driver;
        readonly bool _demo;
        int _demoValue = 255;                 // impedance shown for every active electrode in demo mode
        WebServer? _webServer;
        readonly double[] _lastKOhm = Enumerable.Repeat(-1.0, 64).ToArray();  // per-channel, -1 = inactive/none

        // Head-map geometry, recomputed on construction and on resize.
        int _centerX, _centerY, _formHeight, _formWidth, _fullRadius;
        bool _isDark;

        // Electrode data. Head-map electrodes keep a polar position; the four AUX
        // markers (Left/Right/Top/Bottom) keep a fractional screen position.
        readonly Dictionary<string, ElectrodeState> _electrodes = new();
        readonly Dictionary<string, (double Angle, double Radius)> _electrodePositions = new();
        readonly Dictionary<string, Point> _auxPositions = new()
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
        string? _editingName;
        TextBox? _editBox;

        /// <summary>Radial spacing between the concentric 10-20 guide rings.</summary>
        double RingUnit => _fullRadius / 5.0;

        /// <summary>Electrode diameter derived from the head radius, clamped to 24-80 px.</summary>
        int ComputeElectrodeSize() => Math.Max(24, Math.Min(80, (int)(_fullRadius / 5.0 * 0.828)));

        // ------------------------------------------------------------------ //
        //  Construction / teardown
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Builds the window for the given amplifier driver, or for demo mode when
        /// <paramref name="driver"/> is null.
        /// </summary>
        public MainGUI(IImpedanceDriver? driver, bool demo)
        {
            _driver = driver;
            _demo = demo;

            InitializeComponent();
            RecomputeGeometry();

            HeadMapView.SetElectrodeSize(ComputeElectrodeSize());
            SetAuxiliaryPositions();
            AddAuxiliaryElectrodes();
            DoubleBuffered = true;
            DrawColorMapBox(HeadMapView.ColorMap);
            LoadConfig("Resources/1020Layout.json");

            if (_demo)
            {
                HeadMapView.MaxChannel = 64;
                Text = "Charmeleon  [DEMO - no amplifier]";
                KeyPreview = true;             // so the arrow keys reach ProcessCmdKey
            }
            else
            {
                HeadMapView.MaxChannel = _driver!.ChannelCount;
                Text = "Charmeleon";
            }

            RefreshTimer.Interval = 300;
            RefreshTimer.Tick += Redraw_Callback;
            RefreshTimer.Start();

            _webServer = new WebServer();
        }

        /// <summary>Stops the timer and releases the web server and amplifier on close.</summary>
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

        /// <summary>
        /// Timer tick (about 3 Hz): reads the latest impedances (or the demo value),
        /// updates every electrode and the Web View snapshot, then repaints.
        /// </summary>
        void Redraw_Callback(object? source, EventArgs e)
        {
            double[] impedances;
            if (_demo)
                impedances = Enumerable.Repeat((double)_demoValue, _lastKOhm.Length).ToArray();
            else if (_driver != null)
                impedances = _driver.GetImpedancesKOhm();
            else
                return;

            foreach (var (_, el) in _electrodes)
            {
                int ch = el.HardwareChannel - 1;
                bool live = ch >= 0 && ch < impedances.Length && el.IsActive;
                el.Value = live ? (int)Math.Min(255, impedances[ch]) : 0;
                if (ch >= 0 && ch < _lastKOhm.Length)
                    _lastKOhm[ch] = live ? impedances[ch] : -1;
            }

            ImpedanceSource.Update(_lastKOhm, _electrodes, _electrodePositions, _auxFractions);
            Invalidate();
        }

        /// <summary>Demo mode: Up/Down arrows sweep the impedance shown on every active electrode.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_demo)
            {
                if (keyData == Keys.Up) { _demoValue = Math.Min(255, _demoValue + 1); return true; }
                if (keyData == Keys.Down) { _demoValue = Math.Max(0, _demoValue - 1); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------------ //
        //  Electrode layout helpers
        // ------------------------------------------------------------------ //

        /// <summary>Recomputes centre, radius and dimensions from the current client size.</summary>
        void RecomputeGeometry()
        {
            _formHeight = ClientSize.Height;
            _formWidth = ClientSize.Width;
            _fullRadius = (int)(_formHeight * 0.43);
            _centerX = _formWidth / 2;
            _centerY = _formHeight / 2;
        }

        /// <summary>Places the four AUX markers at their fractional screen positions.</summary>
        void SetAuxiliaryPositions()
        {
            foreach (var name in _auxPositions.Keys.ToList())
            {
                var (fx, fy) = _auxFractions.TryGetValue(name, out var f) ? f : (0.85, 0.80);
                _auxPositions[name] = new Point((int)(_formWidth * fx), (int)(_formHeight * fy));
                if (_electrodes.TryGetValue(name, out var el)) el.Center = _auxPositions[name];
            }
        }

        /// <summary>Creates the AUX electrode states if they do not exist yet.</summary>
        void AddAuxiliaryElectrodes()
        {
            foreach (var aux in _auxPositions)
                if (!_electrodes.ContainsKey(aux.Key))
                    _electrodes[aux.Key] = new ElectrodeState { LabelText = aux.Key, Center = aux.Value };
        }

        /// <summary>
        /// Rescales and repositions the head-map electrodes from their polar
        /// positions, creating any that do not exist yet. AUX markers are untouched.
        /// </summary>
        void AddElectrodes()
        {
            HeadMapView.SetElectrodeSize(ComputeElectrodeSize());

            foreach (var (name, (angle, radius)) in _electrodePositions)
            {
                double rad = angle * Math.PI / 180.0;
                int x = _centerX + (int)(Math.Cos(rad) * (radius * RingUnit));
                int y = _centerY - (int)(Math.Sin(rad) * (radius * RingUnit));
                if (_electrodes.TryGetValue(name, out var existing))
                    existing.Center = new Point(x, y);
                else
                    _electrodes[name] = new ElectrodeState { LabelText = name, Center = new Point(x, y) };
            }
        }

        // ------------------------------------------------------------------ //
        //  Drawing
        // ------------------------------------------------------------------ //

        /// <summary>Paints the head outline, guide rings, ears, electrodes and colour-scale labels.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Head circle and concentric guide rings
            using var dashPen = new Pen(Color.Gray, 1) { DashStyle = DashStyle.Dash };
            g.DrawEllipse(Pens.Black, _centerX - _fullRadius, _centerY - _fullRadius, _fullRadius * 2, _fullRadius * 2);
            foreach (int r in new[] { 4, 3, 2, 1 })
            {
                int cr = (int)(r * RingUnit);
                g.DrawEllipse(dashPen, _centerX - cr, _centerY - cr, 2 * cr, 2 * cr);
            }

            // Cross-hairs
            using var dotPen = new Pen(Color.Gray) { DashStyle = DashStyle.Dot };
            g.DrawLine(dotPen, 0, _centerY, (int)(_formHeight * 1.125), _centerY);
            g.DrawLine(dotPen, _centerX, 0, _centerX, _formHeight);

            // Ears: fixed to the sides of the head circle, centred on the horizontal midline
            int earW = _fullRadius / 7;
            int earH = _fullRadius / 4;
            g.DrawEllipse(Pens.Black, _centerX - _fullRadius - earW, _centerY - earH / 2, earW, earH);
            g.DrawEllipse(Pens.Black, _centerX + _fullRadius, _centerY - earH / 2, earW, earH);

            DrawElectrodes(g);

            // Colour-scale value labels down the left of the scale bar
            int[] labels = { 2, 5, 10, 20, 50, 100, 200, 256 };
            var p = pictureBox1.Location;
            float cmScale = pictureBox1.Height / 256f;
            using var labelBrush = new SolidBrush(SystemColors.ControlText);
            using var labelFont = new Font("Segoe UI", 8);
            var labelFmt = new StringFormat { Alignment = StringAlignment.Far };
            foreach (int y in labels)
                g.DrawString(y.ToString(), labelFont, labelBrush, p.X - 2, p.Y + y * cmScale, labelFmt);
        }

        /// <summary>
        /// Draws every electrode circle in one pass: fill colour from the impedance
        /// (grey when inactive), the value or name inside, and the label beneath.
        /// </summary>
        void DrawElectrodes(Graphics g)
        {
            int size = HeadMapView.ElectrodeSize;
            int cd = Math.Max(12, size - 20);   // circle diameter (matches the old UserControl margin)
            int cr = cd / 2;
            float impFontSz = Math.Max(6f, cd * 0.32f);
            float lblFontSz = Math.Max(5f, cd * 0.28f);
            using var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap   // a label is one line, never wrapped/hyphenated
            };

            foreach (var (name, el) in _electrodes)
            {
                var circle = new Rectangle(el.Center.X - cr, el.Center.Y - cr, cd, cd);
                var fill = el.IsActive ? HeadMapView.ColorMap[el.Value] : Color.LightGray;
                using (var b = new SolidBrush(fill)) g.FillEllipse(b, circle);
                g.DrawEllipse(Pens.Black, circle);

                if (el.Editing) continue;   // the inline textbox is showing over this circle

                // Inside the circle: electrode name in channel view, else the impedance value.
                string inner = HeadMapView.ShowChannels
                    ? el.LabelText
                    : (el.Value == 255 ? "Inf" : el.Value.ToString());
                Color tc = (el.IsActive && el.Value > 220) ? Color.White : ForeColor;
                using var innerFont = FitFont(g, inner,
                    HeadMapView.ShowChannels ? lblFontSz : impFontSz,
                    HeadMapView.ShowChannels ? FontStyle.Regular : FontStyle.Bold, cd * 0.9f);
                using (var b = new SolidBrush(tc))
                    g.DrawString(inner, innerFont, b, circle, fmt);

                // Beneath the circle: hardware channel in channel view, else the name.
                // The four AUX markers (Left/Right/Top/Bottom) sit in a spaced-out cluster, so
                // their names get the full font size and a wide box; packed head-map labels
                // auto-fit to the circle width instead.
                string below = HeadMapView.ShowChannels
                    ? el.HardwareChannel.ToString()
                    : el.LabelText;
                bool isAux = _auxPositions.ContainsKey(name);
                float belowW = isAux ? cd * 3f : cd;
                var lblRect = new RectangleF(el.Center.X - belowW / 2f, el.Center.Y + cr + 3, belowW, 18);
                using var belowFont = FitFont(g, below, lblFontSz, FontStyle.Regular, isAux ? cd * 3f : cd * 0.9f);
                using (var b = new SolidBrush(ForeColor))
                    g.DrawString(below, belowFont, b, lblRect, fmt);
            }
        }

        /// <summary>
        /// A "Segoe UI" font at <paramref name="baseSize"/>, shrunk just enough (floor 5pt) that
        /// <paramref name="text"/> renders within <paramref name="maxWidth"/> pixels, so long
        /// labels such as "PO10" fit inside the electrode without clipping or hyphenating.
        /// </summary>
        static Font FitFont(Graphics g, string text, float baseSize, FontStyle style, float maxWidth)
        {
            var font = new Font("Segoe UI", baseSize, style);
            if (string.IsNullOrEmpty(text)) return font;
            float w = g.MeasureString(text, font).Width;
            if (w <= maxWidth || w <= 0) return font;
            font.Dispose();
            return new Font("Segoe UI", Math.Max(5f, baseSize * maxWidth / w), style);
        }

        // ------------------------------------------------------------------ //
        //  Click handling
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Click on an electrode: in channel view, clicking the label opens the
        /// channel editor; otherwise it toggles the electrode active/inactive.
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            CommitActiveEdit();

            int r = HeadMapView.ElectrodeSize / 2;
            foreach (var (name, el) in _electrodes)
            {
                double dx = e.X - el.Center.X;
                double dy = e.Y - el.Center.Y;
                if (dx * dx + dy * dy <= (r + 4) * (r + 4))
                {
                    bool inLabel = e.Y > el.Center.Y + r - 6;
                    if (HeadMapView.ShowChannels && inLabel)
                        ShowChannelEditor(name, el);
                    else
                        el.IsActive = !el.IsActive;
                    Invalidate();
                    return;
                }
            }
        }

        /// <summary>Opens a small numeric textbox over an electrode to edit its hardware channel.</summary>
        void ShowChannelEditor(string name, ElectrodeState el)
        {
            int s = HeadMapView.ElectrodeSize;
            int r = s / 2;
            var box = new TextBox
            {
                Text = el.HardwareChannel.ToString(),
                Location = new Point(el.Center.X - r, el.Center.Y + r - 4),
                Size = new Size(s, 20),
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 3
            };
            box.KeyDown += (_, ke) => { if (ke.KeyCode == Keys.Enter) CommitActiveEdit(); };
            box.KeyPress += (_, ke) => { if (!char.IsDigit(ke.KeyChar) && ke.KeyChar != (char)Keys.Back) ke.Handled = true; };
            box.Leave += (_, _) => CommitActiveEdit();
            _editingName = name;
            el.Editing = true;
            _editBox = box;
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
            Invalidate();
        }

        /// <summary>Commits and closes the inline channel editor, if one is open.</summary>
        void CommitActiveEdit()
        {
            if (_editBox == null || _editingName == null) return;

            // Capture and clear the fields first. Controls.Remove below removes the
            // focused textbox, which fires its Leave event synchronously and re-enters
            // this method; clearing the fields up front makes that re-entry bail at the
            // guard above instead of dereferencing a box we are about to dispose.
            var box = _editBox;
            var name = _editingName;
            _editBox = null;
            _editingName = null;

            if (_electrodes.TryGetValue(name, out var el))
            {
                if (int.TryParse(box.Text, out int ch))
                {
                    ch = Math.Max(0, ch);
                    // Head-map electrodes are EEG (reference) channels, so they stay within the
                    // amplifier's reference count (MaxChannel). AUX markers map to BIP/AUX
                    // hardware channels above that range, so they are not capped to MaxChannel
                    // (the 3-digit textbox already bounds the input).
                    if (!_auxPositions.ContainsKey(name)) { ch = Math.Min(ch, HeadMapView.MaxChannel); }
                    el.HardwareChannel = ch;
                }
                el.Editing = false;
            }
            Controls.Remove(box);
            box.Dispose();
            Invalidate();
        }

        // ------------------------------------------------------------------ //
        //  Resize
        // ------------------------------------------------------------------ //

        /// <summary>Recomputes geometry and repositions everything when the window is resized.</summary>
        void MainGUI_Resize(object sender, EventArgs e)
        {
            RecomputeGeometry();
            SetAuxiliaryPositions();
            AddElectrodes();
            DrawColorMapBox(HeadMapView.ColorMap);
            Invalidate();
        }

        // ------------------------------------------------------------------ //
        //  Configuration load / save
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Loads a montage from <paramref name="fileName"/>, or prompts for one when
        /// it is empty. Replaces the head-map electrodes (AUX markers are kept) and
        /// applies each electrode's label, channel and active state.
        /// </summary>
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

            RemoveHeadMapElectrodes();

            foreach (var (name, cfg) in data)
            {
                if (_auxPositions.ContainsKey(name))
                {
                    if (cfg.X.HasValue && cfg.Y.HasValue)
                    {
                        _auxFractions[name] = (cfg.X.Value, cfg.Y.Value);
                        SetAuxiliaryPositions();
                    }
                }
                else if (cfg.Angle.HasValue && cfg.Radius.HasValue)
                    _electrodePositions[name] = (cfg.Angle.Value, cfg.Radius.Value);
            }
            AddElectrodes();

            foreach (var (name, cfg) in data)
                if (_electrodes.TryGetValue(name, out var el))
                {
                    el.LabelText = cfg.LabelText;
                    el.HardwareChannel = cfg.HardwareChannel;
                    // Honour the montage's own active flag (as the create-montage path does);
                    // do not override it with a MaxChannel range test, which forced every AUX
                    // marker (channels above the EEG count) inactive on load.
                    el.IsActive = cfg.IsActive;
                }
        }

        /// <summary>Removes every head-map electrode, keeping the AUX markers and clearing positions.</summary>
        void RemoveHeadMapElectrodes()
        {
            foreach (var name in _electrodes.Keys.Except(_auxPositions.Keys).ToList())
                _electrodes.Remove(name);
            _electrodePositions.Clear();
        }

        /// <summary>Serialises the current electrode set (positions, channels, active state) to a JSON file.</summary>
        void saveMontageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var data = _electrodes.ToDictionary(kv => kv.Key, kv =>
            {
                bool isAux = _auxPositions.ContainsKey(kv.Key);
                _auxFractions.TryGetValue(kv.Key, out var frac);
                _electrodePositions.TryGetValue(kv.Key, out var pos);
                return new ElectrodeControlData
                {
                    IsActive = kv.Value.IsActive,
                    LabelText = kv.Value.LabelText,
                    HardwareChannel = kv.Value.HardwareChannel,
                    Angle = isAux ? null : pos.Angle,
                    Radius = isAux ? null : pos.Radius,
                    X = isAux ? frac.X : null,
                    Y = isAux ? frac.Y : null
                };
            });
            using var dlg = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Save Configuration" };
            if (dlg.ShowDialog() == DialogResult.OK)
                File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>Setup, Open Montage.</summary>
        void openMontageToolStripMenuItem_Click(object sender, EventArgs e) => LoadConfig("");

        // ------------------------------------------------------------------ //
        //  Create Montage
        // ------------------------------------------------------------------ //

        /// <summary>Setup, Create Montage, 10/20 Layout.</summary>
        void create1020ToolStripMenuItem_Click(object sender, EventArgs e)
            => ApplyGeneratedMontage(MontageGenerator.Generate1020(), "10/20");

        /// <summary>Setup, Create Montage, Equidistant Layout.</summary>
        void createEquidistantToolStripMenuItem_Click(object sender, EventArgs e)
            => ApplyGeneratedMontage(MontageGenerator.GenerateEquidistant(), "Equidistant");

        /// <summary>
        /// Applies a generated layout to the head map (optionally saving it first),
        /// carrying its hardware channels and active state onto the electrodes.
        /// </summary>
        void ApplyGeneratedMontage(Dictionary<string, ElectrodeControlData> layout, string name)
        {
            if (MessageBox.Show($"Save the {name} layout to a file?",
                    "Create Montage", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                MontageGenerator.SaveToFile(layout);

            RemoveHeadMapElectrodes();

            foreach (var (key, cfg) in layout)
            {
                if (_auxPositions.ContainsKey(key)) continue;
                if (cfg.Angle.HasValue && cfg.Radius.HasValue)
                    _electrodePositions[key] = (cfg.Angle.Value, cfg.Radius.Value);
            }
            AddElectrodes();

            // AddElectrodes only sets positions, so copy channels and active state too.
            foreach (var (key, cfg) in layout)
                if (_electrodes.TryGetValue(key, out var el))
                {
                    el.HardwareChannel = cfg.HardwareChannel;
                    el.IsActive = cfg.IsActive;
                }
            Invalidate();
        }

        // ------------------------------------------------------------------ //
        //  Colour scale and menu handlers
        // ------------------------------------------------------------------ //

        /// <summary>Sizes, positions and paints the vertical colour-scale bar next to the AUX cluster.</summary>
        void DrawColorMapBox(Color[] colorMap)
        {
            int auxX1 = _auxPositions.Values.Min(p => p.X);
            int auxX2 = _auxPositions.Values.Max(p => p.X);
            int auxCX = (auxX1 + auxX2) / 2;
            int auxY1 = _auxPositions.Values.Min(p => p.Y);

            int cmWidth = 40;
            int cmHeight = Math.Max(64, (auxY1 - 16) * 2 / 3);
            pictureBox1.Size = new Size(cmWidth, cmHeight);
            pictureBox1.Location = new Point(auxCX, (auxY1 - cmHeight) / 2);

            var bmp = new Bitmap(cmWidth, cmHeight);
            using (var g = Graphics.FromImage(bmp))
                for (int i = 0; i < 256; i++)
                {
                    int y0 = (int)(i * cmHeight / 256.0);
                    int y1 = (int)((i + 1) * cmHeight / 256.0);
                    using var pen = new Pen(colorMap[i]);
                    for (int py = y0; py < y1; py++)
                        g.DrawLine(pen, 0, py, cmWidth - 1, py);
                }
            pictureBox1.Image = bmp;
        }

        /// <summary>View, View Channels: toggles between impedance values and channel names.</summary>
        void viewChannelsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HeadMapView.ShowChannels = !HeadMapView.ShowChannels;
            Invalidate();
        }

        /// <summary>About, About Charmeleon.</summary>
        void AboutCharmeleonToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var about = new aboutForm();
            ApplyTheme(about, _isDark);
            about.ShowDialog();
        }

        /// <summary>View, Toggle Theme.</summary>
        void ToggleThemeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _isDark = !_isDark;
            ApplyTheme(this, _isDark);
        }

        /// <summary>Recursively applies the light or dark palette to a control tree.</summary>
        void ApplyTheme(Control c, bool dark)
        {
            if (c == null) return;
            c.BackColor = dark ? Color.FromArgb(80, 80, 80) : SystemColors.Control;
            c.ForeColor = dark ? Color.White : SystemColors.ControlText;
            foreach (Control child in c.Controls) ApplyTheme(child, dark);
        }

        /// <summary>Web, Web View: shows the QR / URL dialog, or an error if the server did not start.</summary>
        void webViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_webServer is { Started: false })
            {
                MessageBox.Show("Could not start the Web View server:\n" + _webServer.ErrorMessage +
                    "\n\nAnother application may already be using port 8765.",
                    "Web View", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            using var dlg = new WebDialog();
            ApplyTheme(dlg, _isDark);
            dlg.ShowDialog(this);
        }
    }
}
