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
    /// <remarks>
    /// Besides the head-map electrodes there are other channels (EOG, ECG, GSR, ...), drawn in a cluster
    /// at the right; each sits on an EEG, BIP or AUX channel, and they can be added, renamed, moved and
    /// removed. Problems that would spoil a recording (two electrodes on one channel, a channel the
    /// amplifier does not have) are outlined in red and listed at the bottom of the window.
    /// </remarks>
    public partial class MainGUI : Form
    {
        readonly IImpedanceDriver? _driver;
        readonly bool _demo;
        readonly StartOptions _options;
        int _demoValue = 255;                 // impedance shown for every active electrode in demo mode
        WebServer? _webServer;

        /// <summary>The amplifier's channels, or a generous stand-in in demo mode.</summary>
        readonly AmplifierLayout _layout;
        /// <summary>Index into the driver's impedance array of each channel that has an impedance.</summary>
        readonly Dictionary<ChannelAddress, int> _impedanceIndex = new();

        // Head-map geometry, recomputed on construction and on resize.
        int _centerX, _centerY, _formHeight, _formWidth, _fullRadius;
        bool _isDark;

        // Electrode data. Head-map electrodes keep a polar position; the other channels
        // (EOG, ECG, ...) keep a fractional screen position, by key.
        readonly Dictionary<string, ElectrodeState> _electrodes = new();
        readonly Dictionary<string, (double Angle, double Radius)> _electrodePositions = new();
        readonly Dictionary<string, Point> _auxPositions = new();
        readonly Dictionary<string, (double X, double Y)> _auxFractions = new();

        /// <summary>Where the other channels sit when a layout has none: the markers of Charmeleon before 2.2.</summary>
        static readonly (string Name, double X, double Y)[] _defaultAux =
        [
            ("Left", 0.85, 0.80), ("Right", 0.85, 0.72), ("Top", 0.90, 0.72), ("Bottom", 0.90, 0.80),
        ];

        // Inline channel-edit textbox (one at a time, managed by the form).
        string? _editingName;
        TextBox? _editBox;

        // Mouse: a press on an electrode acts on release, unless it became a drag of an other channel.
        string? _pressedName;
        Point _pressPoint;
        bool _dragging;

        // The montage file shown, and whether it has changes that are not saved.
        string? _currentFile;
        bool _dirty;
        bool _savedThisSession;
        /// <summary>The amplifier the montage on screen was saved with, if its file said.</summary>
        AmplifierLayout? _montageAmplifier;
        /// <summary>What was wrong in the montage file itself, shown with the other problems.</summary>
        IReadOnlyList<string> _fileProblems = [];
        /// <summary>
        /// The electrodes' order in the montage file. Saving keeps it, as readers keep the first of two electrodes
        /// on one channel; the dictionary of electrodes does not keep an order once electrodes were removed.
        /// </summary>
        readonly List<string> _order = [];

        // The status bar at the bottom, which lists the problems; a click shows them all
        readonly StatusStrip _statusStrip = new() { SizingGrip = false };
        readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        List<string> _problems = [];

        /// <summary>Radial spacing between the concentric 10-20 guide rings.</summary>
        double RingUnit => _fullRadius / 5.0;

        /// <summary>Electrode diameter derived from the head radius, clamped to 24-80 px.</summary>
        int ComputeElectrodeSize() => Math.Max(24, Math.Min(80, (int)(_fullRadius / 5.0 * 0.828)));

        // ------------------------------------------------------------------ //
        //  Construction / teardown
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Builds the window for the given amplifier driver, or for demo mode when
        /// <paramref name="driver"/> is null, showing the montage <paramref name="options"/> names
        /// if any, else the default 10/20 layout.
        /// </summary>
        public MainGUI(IImpedanceDriver? driver, bool demo, StartOptions options)
        {
            _driver = driver;
            _demo = demo;
            _options = options;
            _layout = _demo || _driver == null ? AmplifierLayout.Demo : _driver.Layout;
            if (_driver != null)
                for (int i = 0; i < _driver.ImpedanceChannels.Count; i++)
                    _impedanceIndex.TryAdd(_driver.ImpedanceChannels[i], i);

            InitializeComponent();
            AddMenuItems();
            _statusStrip.Items.Add(_statusLabel);
            _statusLabel.Click += (_, _) => ShowProblems();
            Controls.Add(_statusStrip);
            RecomputeGeometry();

            HeadMapView.SetElectrodeSize(ComputeElectrodeSize());
            HeadMapView.MaxChannel = _layout.EegChannels;
            DoubleBuffered = true;
            if (_demo) KeyPreview = true;      // so the arrow keys reach ProcessCmdKey

            // Next to the executable, not in the working directory, which is wherever Charmeleon was started from
            LoadConfig(Path.Combine(AppContext.BaseDirectory, "Resources", "1020Layout.json"), remember: false);
            if (_auxPositions.Count == 0)
                foreach (var (name, x, y) in _defaultAux) AddAux(name, name, x, y, ChannelAddress.None, active: false);

            // When the montage can not be read, LoadConfig says so and the default layout stays
            if (_options.Montage != null) LoadConfig(_options.Montage);
            DrawColorMapBox(HeadMapView.ColorMap);
            UpdateTitle();

            RefreshTimer.Interval = 300;
            RefreshTimer.Tick += Redraw_Callback;
            RefreshTimer.Start();

            _webServer = new WebServer();
        }

        /// <summary>The menu items not in the designer file: Save, Save As and Add Other Channel.</summary>
        void AddMenuItems()
        {
            openMontageToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            saveMontageToolStripMenuItem.Text = "Save Montage As...";
            saveMontageToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;

            var save = new ToolStripMenuItem("Save Montage", null, (_, _) => SaveMontage(saveAs: false)) { ShortcutKeys = Keys.Control | Keys.S };
            var addOther = new ToolStripMenuItem("Add Other Channel...", null, (_, _) => AddOtherChannel(null));
            int at = setupToolStripMenuItem.DropDownItems.IndexOf(saveMontageToolStripMenuItem);
            setupToolStripMenuItem.DropDownItems.Insert(at, save);
            setupToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            setupToolStripMenuItem.DropDownItems.Add(addOther);
        }

        /// <summary>Asks to save unsaved changes, or saves them without asking when the caller asked for that.</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CommitActiveEdit();
            if (_dirty && !e.Cancel)
            {
                if (_options.SaveOnClose && _currentFile != null)
                {
                    // No dialog: the program that started Charmeleon closes it and ends it if it lingers
                    try { SaveTo(_currentFile); }
                    catch (Exception ex) { Console.Error.WriteLine($"Could not save {_currentFile}: {ex.Message}"); }
                }
                else
                {
                    var answer = MessageBox.Show(this, $"Save the changes to {DisplayName()}?", "Charmeleon",
                        MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (answer == DialogResult.Cancel || (answer == DialogResult.Yes && !SaveMontage(saveAs: false)))
                        e.Cancel = true;
                }
            }
            base.OnFormClosing(e);
        }

        /// <summary>Stops the timer, releases the web server and amplifier, and writes the result file.</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RefreshTimer.Stop();
            _webServer?.Dispose();
            _driver?.Dispose();
            WriteResult();
            base.OnFormClosed(e);
        }

        /// <summary>
        /// Writes <see cref="StartOptions.ResultFile"/>, if asked for: <c>{"File": ..., "Saved": ..., "Unsaved": ...}</c>,
        /// the montage file Charmeleon ended with (null for none), whether it was saved during this run, and
        /// whether changes were left unsaved. The OpenViBE UG acquisition server reads it to take the montage over.
        /// </summary>
        public void WriteResult()
        {
            if (string.IsNullOrEmpty(_options.ResultFile)) return;
            try
            {
                var result = new { File = _currentFile, Saved = _savedThisSession, Unsaved = _dirty };
                File.WriteAllText(_options.ResultFile, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { Console.Error.WriteLine($"Could not write {_options.ResultFile}: {ex.Message}"); }
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
            double[]? impedances = null;
            if (!_demo)
            {
                if (_driver == null) return;
                impedances = _driver.GetImpedancesKOhm();
            }

            foreach (var (_, el) in _electrodes)
            {
                // In demo mode every channel shows the demo value
                bool measured = _demo || (el.Address.IsAssigned && _impedanceIndex.ContainsKey(el.Address));
                el.HasImpedance = measured;
                double value = 0;
                if (_demo) value = _demoValue;
                else if (measured && impedances != null && _impedanceIndex[el.Address] < impedances.Length) value = impedances[_impedanceIndex[el.Address]];
                el.Value = el.IsActive && measured ? (int)Math.Min(255, value) : 0;
            }

            ImpedanceSource.Update(_electrodes, _electrodePositions, _auxFractions);
            Invalidate();
        }

        /// <summary>Demo mode: Up/Down arrows sweep the impedance shown on every active electrode.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_demo && _editBox == null)
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
            // The head map fills the window above the status bar
            _formHeight = ClientSize.Height - _statusStrip.Height;
            _formWidth = ClientSize.Width;
            _fullRadius = (int)(_formHeight * 0.43);
            _centerX = _formWidth / 2;
            _centerY = _formHeight / 2;
        }

        /// <summary>Places the other channels at their fractional screen positions.</summary>
        void SetAuxiliaryPositions()
        {
            foreach (var name in _auxPositions.Keys.ToList())
            {
                var (fx, fy) = _auxFractions.TryGetValue(name, out var f) ? f : (0.85, 0.80);
                _auxPositions[name] = new Point((int)(_formWidth * fx), (int)(_formHeight * fy));
                if (_electrodes.TryGetValue(name, out var el)) el.Center = _auxPositions[name];
            }
        }

        /// <summary>Adds an other channel under <paramref name="key"/>, at a fractional screen position.</summary>
        void AddAux(string key, string label, double x, double y, ChannelAddress channel, bool active)
        {
            _auxFractions[key] = (x, y);
            _auxPositions[key] = new Point((int)(_formWidth * x), (int)(_formHeight * y));
            _electrodes[key] = new ElectrodeState { LabelText = label, Address = channel, IsActive = active, Center = _auxPositions[key] };
        }

        /// <summary>
        /// Rescales and repositions the head-map electrodes from their polar
        /// positions, creating any that do not exist yet. Other channels are untouched.
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
        //  Problems
        // ------------------------------------------------------------------ //

        /// <summary>
        /// What would spoil a recording with the montage on screen, each as a sentence, and the electrodes
        /// concerned: two active electrodes on one channel or with one name, an active electrode on a
        /// channel the amplifier does not have, and a montage made for another amplifier.
        /// </summary>
        (List<string> Sentences, HashSet<string> Electrodes) FindProblems()
        {
            var sentences = new List<string>(_fileProblems);
            var concerned = new HashSet<string>();
            sentences.AddRange(FindClashes(concerned));
            var active = _electrodes.Where(kv => kv.Value.IsActive && kv.Value.Address.IsAssigned).ToList();
            foreach (var group in active.GroupBy(kv => kv.Value.LabelText, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            {
                sentences.Add($"{group.Key} names {group.Count()} electrodes.");
                concerned.UnionWith(group.Select(kv => kv.Key));
            }
            if (!_demo)
            {
                foreach (var (key, el) in active.Where(kv => !_layout.Has(kv.Value.Address)))
                {
                    string hint = _auxPositions.ContainsKey(key) && el.Kind == ChannelKind.Eeg ? " Set it to a BIP or AUX channel?" : "";
                    sentences.Add($"{el.LabelText} is on {el.Address}, but the amplifier has {_layout.Count(el.Kind)} {KindName(el.Kind)} channels.{hint}");
                    concerned.Add(key);
                }
                if (_montageAmplifier != null && _montageAmplifier with { Type = _layout.Type } != _layout)
                    sentences.Add($"This montage was made for {(_montageAmplifier.Type.Length > 0 ? _montageAmplifier.Type : "an amplifier")} with "
                                  + $"{_montageAmplifier.Describe()}; this one has {_layout.Describe()}.");
            }
            return (sentences, concerned);
        }

        /// <summary>
        /// The channels with more than one active electrode, which would record only one of them, each as a
        /// sentence; adds the electrodes concerned to <paramref name="concerned"/>.
        /// </summary>
        List<string> FindClashes(HashSet<string> concerned)
        {
            var clashes = new List<string>();
            var groups = _electrodes.Where(kv => kv.Value.IsActive && kv.Value.Address.IsAssigned)
                                    .GroupBy(kv => kv.Value.Address).Where(g => g.Count() > 1);
            foreach (var group in groups)
            {
                var names = group.Select(kv => kv.Value.LabelText).ToList();
                string list = string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
                clashes.Add($"{list} are {(names.Count == 2 ? "both" : "all")} on {group.Key}.");
                concerned.UnionWith(group.Select(kv => kv.Key));
            }
            return clashes;
        }

        static string KindName(ChannelKind kind) => kind == ChannelKind.Eeg ? "EEG" : ChannelAddress.Prefix(kind);

        // ------------------------------------------------------------------ //
        //  Drawing
        // ------------------------------------------------------------------ //

        /// <summary>Paints the head outline, guide rings, ears, electrodes, colour-scale labels and problems.</summary>
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

            var (problems, concerned) = FindProblems();
            DrawElectrodes(g, concerned);
            ShowProblemCount(problems);

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
        /// (grey when inactive, hatched when the amplifier measures none on its channel),
        /// the value or name inside, the label beneath, and a red ring when it has a problem.
        /// </summary>
        void DrawElectrodes(Graphics g, HashSet<string> concerned)
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
            using var problemPen = new Pen(Color.Red, 3);

            foreach (var (name, el) in _electrodes)
            {
                var circle = new Rectangle(el.Center.X - cr, el.Center.Y - cr, cd, cd);
                bool measured = el.IsActive && el.HasImpedance;
                if (measured)
                    using (var b = new SolidBrush(HeadMapView.ColorMap[el.Value])) g.FillEllipse(b, circle);
                else if (el.IsActive)
                    using (var b = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.Silver, Color.WhiteSmoke)) g.FillEllipse(b, circle);
                else
                    using (var b = new SolidBrush(Color.LightGray)) g.FillEllipse(b, circle);
                g.DrawEllipse(Pens.Black, circle);
                if (concerned.Contains(name))
                    g.DrawEllipse(problemPen, Rectangle.Inflate(circle, 3, 3));

                // Inside the circle: electrode name in channel view, else the impedance value.
                // Also while the channel editor is open, so that it is clear which electrode it edits.
                string inner = HeadMapView.ShowChannels
                    ? el.LabelText
                    : !el.HasImpedance && el.IsActive ? "n/a" : (el.Value == 255 ? "Inf" : el.Value.ToString());
                Color tc = (measured && el.Value > 220) ? Color.White : ForeColor;
                using var innerFont = FitFont(g, inner,
                    HeadMapView.ShowChannels ? lblFontSz : impFontSz,
                    HeadMapView.ShowChannels ? FontStyle.Regular : FontStyle.Bold, cd * 0.9f);
                using (var b = new SolidBrush(tc))
                    g.DrawString(inner, innerFont, b, circle, fmt);

                if (el.Editing) continue;   // the channel editor's textbox covers the label beneath

                // Beneath the circle: the channel in channel view, else the name. The other channels
                // sit in a spaced-out cluster, so their labels get the full font size and a wide box;
                // packed head-map labels auto-fit to the circle width instead.
                string below = HeadMapView.ShowChannels ? el.Address.ToString() : el.LabelText;
                bool isAux = _auxPositions.ContainsKey(name);
                float belowW = isAux ? cd * 3f : cd;
                var lblRect = new RectangleF(el.Center.X - belowW / 2f, el.Center.Y + cr + 3, belowW, 18);
                using var belowFont = FitFont(g, below, lblFontSz, FontStyle.Regular, isAux ? cd * 3f : cd * 0.9f);
                using (var b = new SolidBrush(ForeColor))
                    g.DrawString(below, belowFont, b, lblRect, fmt);
            }
        }

        /// <summary>Shows the first problem and how many there are in the status bar, or that there are none.</summary>
        void ShowProblemCount(List<string> problems)
        {
            if (problems.SequenceEqual(_problems)) return;
            _problems = problems;
            if (problems.Count == 0)
            {
                _statusLabel.Text = "No problems found";
                _statusLabel.ForeColor = ForeColor;
                _statusLabel.ToolTipText = "";
                return;
            }
            _statusLabel.Text = problems.Count == 1 ? problems[0] : $"{problems.Count} problems: {problems[0]}  (click to see all)";
            _statusLabel.ForeColor = _isDark ? Color.FromArgb(255, 140, 140) : Color.FromArgb(190, 0, 0);
            _statusLabel.ToolTipText = string.Join(Environment.NewLine, problems);
        }

        /// <summary>Lists every problem in a message box.</summary>
        void ShowProblems()
        {
            if (_problems.Count == 0) return;
            MessageBox.Show(this, string.Join(Environment.NewLine + Environment.NewLine, _problems), $"{_problems.Count} problem{(_problems.Count == 1 ? "" : "s")} in {DisplayName()}",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        //  Mouse handling
        // ------------------------------------------------------------------ //

        /// <summary>The electrode under <paramref name="point"/>, if any.</summary>
        string? HitTest(Point point)
        {
            int r = HeadMapView.ElectrodeSize / 2;
            foreach (var (name, el) in _electrodes)
            {
                double dx = point.X - el.Center.X;
                double dy = point.Y - el.Center.Y;
                if (dx * dx + dy * dy <= (r + 4) * (r + 4)) return name;
            }
            return null;
        }

        /// <summary>Left button: remembers the electrode pressed; right button: the context menu.</summary>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            CommitActiveEdit();
            if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu(e.Location);
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            _pressedName = HitTest(e.Location);
            _pressPoint = e.Location;
            _dragging = false;
        }

        /// <summary>Drags an other channel to a new place once the mouse has moved a few pixels.</summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_pressedName == null || e.Button != MouseButtons.Left || !_auxPositions.ContainsKey(_pressedName)) return;
            if (!_dragging && Math.Abs(e.X - _pressPoint.X) + Math.Abs(e.Y - _pressPoint.Y) < 5) return;

            _dragging = true;
            double fx = Math.Clamp(e.X / (double)Math.Max(1, _formWidth), 0.02, 0.98);
            double fy = Math.Clamp(e.Y / (double)Math.Max(1, _formHeight), 0.05, 0.95);
            _auxFractions[_pressedName] = (Math.Round(fx, 3), Math.Round(fy, 3));
            SetAuxiliaryPositions();
            Invalidate();
        }

        /// <summary>
        /// A click (no drag) on an electrode: in channel view, clicking the label opens the
        /// channel editor; otherwise it toggles the electrode active/inactive.
        /// </summary>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || _pressedName == null) return;
            string name = _pressedName;
            _pressedName = null;
            if (_dragging)
            {
                _dragging = false;
                MarkDirty();
                return;
            }
            if (!_electrodes.TryGetValue(name, out var el)) return;

            int r = HeadMapView.ElectrodeSize / 2;
            bool inLabel = e.Y > el.Center.Y + r - 6;
            if (HeadMapView.ShowChannels && inLabel)
                ShowChannelEditor(name, el);
            else
            {
                el.IsActive = !el.IsActive;
                MarkDirty();
            }
            Invalidate();
        }

        /// <summary>
        /// Right click: on an electrode, rename it or change its channel, and remove an other channel;
        /// anywhere, add an other channel there.
        /// </summary>
        void ShowContextMenu(Point point)
        {
            var menu = new ContextMenuStrip();
            string? name = HitTest(point);
            if (name != null && _electrodes.TryGetValue(name, out var el))
            {
                menu.Items.Add($"Rename {el.LabelText}...", null, (_, _) => EditElectrode(name, askChannel: false));
                menu.Items.Add("Channel...", null, (_, _) => EditElectrode(name, askChannel: true));
                if (_auxPositions.ContainsKey(name))
                    menu.Items.Add($"Remove {el.LabelText}", null, (_, _) => RemoveAux(name));
                menu.Items.Add(new ToolStripSeparator());
            }
            menu.Items.Add("Add Other Channel Here...", null, (_, _) => AddOtherChannel(point));
            menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
            menu.Show(this, point);
        }

        /// <summary>The channels an electrode can be put on: the amplifier's BIP and AUX channels, then its EEG channels.</summary>
        IEnumerable<string> ChannelChoices()
        {
            for (int i = 1; i <= _layout.BipolarChannels; i++) yield return "BIP" + i;
            for (int i = 1; i <= _layout.AuxiliaryChannels; i++) yield return "AUX" + i;
            for (int i = 1; i <= _layout.EegChannels; i++) yield return i.ToString();
        }

        /// <summary>
        /// Asks for a name and channel and adds an other channel (EOG, ECG, ...) at <paramref name="point"/>, or, when
        /// there is none (the Setup menu) or it lies on the head, at the next free place in the cluster at the right.
        /// </summary>
        void AddOtherChannel(Point? point)
        {
            using var dialog = new PromptDialog("Add Other Channel", "", ChannelChoices(), _layout.BipolarChannels > 0 ? "BIP1" : "");
            ApplyTheme(dialog, _isDark);
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.NameText.Length == 0) return;
            if (!ChannelAddress.TryParse(dialog.ChannelText, out var channel))
            {
                MessageBox.Show(this, $"\"{dialog.ChannelText}\" is not a channel: give a number for an EEG channel, or BIP or AUX and a number.",
                    "Add Other Channel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Where the context menu was opened, if that is beside the head and inside the drawing area
            var (x, y) = point is { } p && IsFreePlace(p) ? (p.X / (double)_formWidth, p.Y / (double)_formHeight) : FreeAuxSlot();
            string key = UniqueKey(dialog.NameText);
            AddAux(key, dialog.NameText, Math.Round(x, 3), Math.Round(y, 3), channel, active: true);
            MarkDirty();
            Invalidate();
        }

        /// <summary>True for a point beside the head, below the menu bar and above the status bar.</summary>
        bool IsFreePlace(Point point)
        {
            bool onHead = Math.Pow(point.X - _centerX, 2) + Math.Pow(point.Y - _centerY, 2) < Math.Pow(_fullRadius + HeadMapView.ElectrodeSize / 2.0, 2);
            int top = menuStrip1.Bottom;
            return !onHead && point.X >= 0 && point.X < _formWidth && point.Y > top + HeadMapView.ElectrodeSize / 2 && point.Y < _formHeight;
        }

        /// <summary>A free place in the cluster of other channels, to the right of the head.</summary>
        (double X, double Y) FreeAuxSlot()
        {
            foreach (double y in new[] { 0.72, 0.80, 0.88, 0.64, 0.56, 0.48 })
                foreach (double x in new[] { 0.85, 0.90, 0.95, 0.80 })
                    if (!_auxFractions.Values.Any(f => Math.Abs(f.X - x) < 0.03 && Math.Abs(f.Y - y) < 0.03))
                        return (x, y);
            return (0.9, 0.5);
        }

        /// <summary><paramref name="name"/>, or with a number added when an electrode already has that key.</summary>
        string UniqueKey(string name)
        {
            string key = name;
            for (int i = 2; _electrodes.ContainsKey(key); i++) key = $"{name} {i}";
            return key;
        }

        /// <summary>Renames an electrode, or changes its channel, through a dialog.</summary>
        void EditElectrode(string key, bool askChannel)
        {
            if (!_electrodes.TryGetValue(key, out var el)) return;
            using var dialog = new PromptDialog(askChannel ? $"Channel of {el.LabelText}" : "Rename", el.LabelText,
                askChannel ? ChannelChoices() : null, el.Address.ToString());
            ApplyTheme(dialog, _isDark);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (dialog.NameText.Length > 0) el.LabelText = dialog.NameText;
            if (askChannel)
            {
                if (ChannelAddress.TryParse(dialog.ChannelText, out var channel)) el.Address = channel;
                else MessageBox.Show(this, $"\"{dialog.ChannelText}\" is not a channel: give a number for an EEG channel, or BIP or AUX and a number.",
                    "Channel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            MarkDirty();
            Invalidate();
        }

        /// <summary>Removes an other channel.</summary>
        void RemoveAux(string key)
        {
            _electrodes.Remove(key);
            _auxPositions.Remove(key);
            _auxFractions.Remove(key);
            MarkDirty();
            Invalidate();
        }

        /// <summary>Opens a small textbox over an electrode to edit its channel: a number for EEG, or BIP1, AUX2, ...</summary>
        void ShowChannelEditor(string name, ElectrodeState el)
        {
            int s = HeadMapView.ElectrodeSize;
            int r = s / 2;
            var box = new TextBox
            {
                Text = el.Address.ToString(),
                Location = new Point(el.Center.X - r, el.Center.Y + r - 4),
                Size = new Size(s, 20),
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 6,
                CharacterCasing = CharacterCasing.Upper
            };
            box.KeyDown += (_, ke) =>
            {
                if (ke.KeyCode == Keys.Enter) { CommitActiveEdit(); ke.SuppressKeyPress = true; }
                else if (ke.KeyCode == Keys.Escape) { box.Text = el.Address.ToString(); CommitActiveEdit(); ke.SuppressKeyPress = true; }
            };
            box.KeyPress += (_, ke) => { if (!char.IsLetterOrDigit(ke.KeyChar) && ke.KeyChar != (char)Keys.Back && ke.KeyChar != ' ') ke.Handled = true; };
            box.Leave += (_, _) => CommitActiveEdit();
            _editingName = name;
            el.Editing = true;
            _editBox = box;
            Controls.Add(box);
            box.BringToFront();
            box.Focus();
            box.SelectAll();
            Invalidate();
        }

        /// <summary>Commits and closes the inline channel editor, if one is open; text that is not a channel is ignored.</summary>
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
                // Not capped to the amplifier's channels: a channel it does not have is shown as a problem
                // instead, so that a typing error is seen rather than silently changed
                if (ChannelAddress.TryParse(box.Text, out var channel) && channel != el.Address)
                {
                    el.Address = channel;
                    MarkDirty();
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
        /// Loads a montage from <paramref name="fileName"/>, or prompts for one when it is empty.
        /// Replaces the head-map electrodes, and the other channels when the file has any, and
        /// applies each electrode's label, channel and active state.
        /// </summary>
        /// <param name="remember">False for the default layout, which is not "the file" Save writes to.</param>
        void LoadConfig(string fileName, bool remember = true)
        {
            if (fileName == "")
            {
                using var dlg = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Open Montage" };
                if (_currentFile != null) dlg.InitialDirectory = Path.GetDirectoryName(_currentFile);
                if (dlg.ShowDialog() != DialogResult.OK) return;
                fileName = dlg.FileName;
            }

            Montage montage;
            try { montage = MontageFile.Load(fileName); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                MessageBox.Show($"Error loading montage: {ex.Message}", "Open Montage", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RemoveHeadMapElectrodes();
            bool hasAux = montage.Electrodes.Any(kv => kv.Value.X.HasValue && kv.Value.Y.HasValue && !kv.Value.Angle.HasValue);
            if (hasAux) RemoveAuxElectrodes();

            foreach (var (name, cfg) in montage.Electrodes)
            {
                if (cfg.Angle.HasValue && cfg.Radius.HasValue)
                    _electrodePositions[name] = (cfg.Angle.Value, cfg.Radius.Value);
                else if (cfg.X.HasValue && cfg.Y.HasValue)
                    AddAux(name, cfg.LabelText, cfg.X.Value, cfg.Y.Value, ChannelAddress.None, cfg.IsActive);
            }
            AddElectrodes();

            foreach (var (name, cfg) in montage.Electrodes)
                if (_electrodes.TryGetValue(name, out var el))
                {
                    el.LabelText = cfg.LabelText;
                    // "Channel" (BIP1, AUX2) takes precedence over HardwareChannel, the EEG channel
                    el.Address = ChannelAddress.TryParse(cfg.Channel, out var channel) && !string.IsNullOrWhiteSpace(cfg.Channel)
                        ? channel
                        : new ChannelAddress(ChannelKind.Eeg, Math.Max(0, cfg.HardwareChannel));
                    // Honour the montage's own active flag (as the create-montage path does);
                    // do not override it with a MaxChannel range test, which forced every AUX
                    // marker (channels above the EEG count) inactive on load.
                    el.IsActive = cfg.IsActive;
                }

            if (remember)
            {
                _currentFile = Path.GetFullPath(fileName);
                _montageAmplifier = montage.Amplifier;
                _fileProblems = montage.Problems;
                _order.Clear();
                _order.AddRange(montage.Electrodes.Select(kv => kv.Key));
                _dirty = false;
            }
            UpdateTitle();
            Invalidate();
        }

        /// <summary>Removes every head-map electrode, keeping the other channels and clearing positions.</summary>
        void RemoveHeadMapElectrodes()
        {
            foreach (var name in _electrodes.Keys.Except(_auxPositions.Keys).ToList())
                _electrodes.Remove(name);
            _electrodePositions.Clear();
        }

        /// <summary>Removes every other channel.</summary>
        void RemoveAuxElectrodes()
        {
            foreach (var name in _auxPositions.Keys.ToList()) _electrodes.Remove(name);
            _auxPositions.Clear();
            _auxFractions.Clear();
        }

        /// <summary>The montage on screen, as it is written to a file.</summary>
        IEnumerable<KeyValuePair<string, ElectrodeControlData>> CurrentMontage()
        {
            // In the file's order, then the electrodes added since
            var keys = _order.Where(_electrodes.ContainsKey).Concat(_electrodes.Keys.Except(_order)).ToList();
            foreach (var key in keys)
            {
                var el = _electrodes[key];
                bool isAux = _auxPositions.ContainsKey(key);
                _auxFractions.TryGetValue(key, out var frac);
                _electrodePositions.TryGetValue(key, out var pos);
                var exact = isAux ? null : MontageGenerator.ExactSphericalPosition(key);
                yield return new(key, new ElectrodeControlData
                {
                    IsActive = el.IsActive,
                    LabelText = el.LabelText,
                    // A BIP or AUX channel is in Channel; HardwareChannel 0 keeps older readers off the EEG channels
                    HardwareChannel = el.Kind == ChannelKind.Eeg ? el.HardwareChannel : 0,
                    Channel = el.Kind == ChannelKind.Eeg || !el.Address.IsAssigned ? null : el.Address.ToString(),
                    Angle = isAux ? null : pos.Angle,
                    Radius = isAux ? null : pos.Radius,
                    X = isAux ? frac.X : null,
                    Y = isAux ? frac.Y : null,
                    Theta = exact?.Theta,
                    Phi = exact?.Phi,
                });
            }
        }

        /// <summary>
        /// Setup, Save Montage (Ctrl+S) and Save Montage As (Ctrl+Shift+S). Refuses a montage with two
        /// electrodes on one channel, which records only one of them; asks about other problems.
        /// </summary>
        /// <returns>True when the montage was saved.</returns>
        bool SaveMontage(bool saveAs)
        {
            CommitActiveEdit();
            var (problems, _) = FindProblems();
            var clashes = FindClashes([]);
            if (clashes.Count > 0)
            {
                MessageBox.Show(this, "Each channel can carry one electrode only:\n\n" + string.Join("\n", clashes),
                    "Save Montage", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (problems.Count > 0 && MessageBox.Show(this, string.Join("\n", problems) + "\n\nSave anyway?", "Save Montage",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return false;

            string? path = saveAs ? null : _currentFile;
            if (path == null)
            {
                using var dlg = new SaveFileDialog { Filter = "JSON Files (*.json)|*.json", Title = "Save Montage", DefaultExt = "json" };
                if (_currentFile != null)
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(_currentFile);
                    dlg.FileName = Path.GetFileName(_currentFile);
                }
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                path = dlg.FileName;
            }

            try { SaveTo(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // E.g. one of the montages of the library, which its installer makes read-only
                MessageBox.Show(this, $"Could not save {path}: {ex.Message}\n\nSave it under another name with Save Montage As.",
                    "Save Montage", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            return true;
        }

        /// <summary>Writes the montage on screen to <paramref name="path"/>, which then becomes the current file.</summary>
        void SaveTo(string path)
        {
            MontageFile.Save(path, CurrentMontage(), _demo ? null : _layout);
            _currentFile = Path.GetFullPath(path);
            _montageAmplifier = _demo ? _montageAmplifier : _layout;
            _fileProblems = [];
            _dirty = false;
            _savedThisSession = true;
            UpdateTitle();
        }

        /// <summary>Notes that the montage on screen differs from its file.</summary>
        void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            UpdateTitle();
        }

        /// <summary>The current file's name, or "the montage" when there is none.</summary>
        string DisplayName() => _currentFile != null ? Path.GetFileName(_currentFile) : "the montage";

        /// <summary>"Charmeleon - ca-208.json*": the file shown and whether it has unsaved changes.</summary>
        void UpdateTitle()
        {
            string title = "Charmeleon";
            if (_currentFile != null || _dirty) title += " - " + (_currentFile != null ? Path.GetFileName(_currentFile) : "untitled") + (_dirty ? "*" : "");
            if (_demo) title += "  [DEMO - no amplifier]";
            Text = title;
        }

        /// <summary>Setup, Open Montage.</summary>
        void openMontageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_dirty && MessageBox.Show(this, $"Discard the changes to {DisplayName()}?", "Open Montage",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            LoadConfig("");
        }

        /// <summary>Setup, Save Montage As.</summary>
        void saveMontageToolStripMenuItem_Click(object sender, EventArgs e) => SaveMontage(saveAs: true);

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
        /// carrying its hardware channels and active state onto the electrodes. The
        /// other channels are kept.
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
                    el.Address = new ChannelAddress(ChannelKind.Eeg, cfg.HardwareChannel);
                    el.IsActive = cfg.IsActive;
                }

            // A new montage: not the file that was open, so Save asks where it goes
            _order.Clear();
            _order.AddRange(layout.Keys);
            _currentFile = null;
            _montageAmplifier = null;
            _fileProblems = [];
            _dirty = true;
            UpdateTitle();
            Invalidate();
        }

        // ------------------------------------------------------------------ //
        //  Colour scale and menu handlers
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Sizes, positions and paints the vertical colour-scale bar, at the right above where the other channels
        /// start out. A fixed place, not the other channels' own: one added or dragged elsewhere would take it along.
        /// </summary>
        void DrawColorMapBox(Color[] colorMap)
        {
            var positions = _defaultAux.Select(a => new Point((int)(_formWidth * a.X), (int)(_formHeight * a.Y))).ToList();
            int auxX1 = positions.Min(p => p.X);
            int auxX2 = positions.Max(p => p.X);
            int auxCX = Math.Min((auxX1 + auxX2) / 2, Math.Max(0, _formWidth - 60));
            int auxY1 = positions.Min(p => p.Y);

            int cmWidth = 40;
            int cmHeight = Math.Max(64, (auxY1 - 16) * 2 / 3);
            pictureBox1.Size = new Size(cmWidth, cmHeight);
            pictureBox1.Location = new Point(auxCX, Math.Max(24, (auxY1 - cmHeight) / 2));

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
            pictureBox1.Image?.Dispose();
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
