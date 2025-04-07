using System.ComponentModel;

namespace CharmeleonGUI
{
    /// <summary>
    /// Represents a UI control for displaying and editing information related to a single electrode.
    /// Displays an impedance value via color, and allows label editing and hardware channel assignment.
    /// </summary>
    public partial class ElectrodeControl : UserControl
    {
        private bool isActive = true;
        private bool editState = false;
        public static bool viewHWChannel = false;
        private int value = 0;
        private string labelText = "Label";
        public int hardwareChannel = 0;
        public static int maxChannel = 256;
        private TextBox? editBox = null;

        /// <summary>
        /// The colormap used to map values to colors. Loaded from file if available, otherwise generated.
        /// </summary>
        public static Color[] ColorMap = File.Exists("Resources/heat.map") ? LoadColorMapFromFile("Resources/heat.map") : GenerateColorMap();

        private static ElectrodeControl? currentlyEditing = null;
        private static TextBox? activeEditBox = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ElectrodeControl"/> class.
        /// Sets up the visual layout and click interaction.
        /// </summary>
        public ElectrodeControl()
        {
            this.Size = new Size(50, 70);
            this.DoubleBuffered = true;
            this.Click += (s, e) => ToggleActiveState();
            InitTextBox();
        }

        /// <summary>
        /// Gets or sets whether the electrode is currently active (i.e., shown with color).
        /// </summary>
        [Category("Appearance")]
        public bool IsActive
        {
            get => isActive;
            set { isActive = value; Invalidate(); }
        }

        /// <summary>
        /// Gets or sets whether the control is in editing mode (showing editable hardware channel).
        /// </summary>
        [Category("Behavior")]
        public bool EditState
        {
            get => editState;
            set { editState = value; Invalidate(); }
        }

        /// <summary>
        /// Gets or sets the impedance value (0-255) for color mapping.
        /// </summary>
        [Category("Data")]
        public int Value
        {
            get => value;
            set { this.value = Math.Max(0, Math.Min(255, value)); Invalidate(); }
        }

        /// <summary>
        /// Gets or sets the textual label shown beneath the electrode.
        /// </summary>
        [Category("Appearance")]
        public string LabelText
        {
            get => labelText;
            set { labelText = value; Invalidate(); }
        }

        /// <summary>
        /// Gets or sets the hardware channel number associated with this electrode.
        /// </summary>
        [Category("Data")]
        public int HardwareChannel
        {
            get => hardwareChannel;
            set { hardwareChannel = Math.Max(0, Math.Min(maxChannel, value)); Invalidate(); }
        }

        /// <summary>
        /// Gets or sets the maximum valid hardware channel index.
        /// </summary>
        [Category("Data")]
        public int MaxChannel
        {
            get => maxChannel;
            set { maxChannel = Math.Max(1, value); Invalidate(); }
        }

        /// <summary>
        /// Initializes the editable textbox used for changing hardware channels.
        /// </summary>
        private void InitTextBox()
        {
            editBox = new TextBox { Visible = false, TextAlign = HorizontalAlignment.Center };
            editBox.Leave += (s, e) => SaveHardwareChannel();
            editBox.KeyPress += (s, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back && e.KeyChar != (char)Keys.Enter)
                    e.Handled = true;

                if (e.KeyChar == (char)Keys.Enter)
                {
                    SaveHardwareChannel();
                    EditState = false;
                    editBox.Visible = false;
                    Invalidate();
                }
            };
            Controls.Add(editBox);
        }

        /// <summary>
        /// Toggles the active/inactive state of the electrode when clicked.
        /// </summary>
        private void ToggleActiveState()
        {
            IsActive = !IsActive;
        }

        /// <summary>
        /// Shows the hardware channel textbox for inline editing.
        /// Also hides any other active editor in the UI.
        /// </summary>
        private void ShowEditBox()
        {
            if (currentlyEditing != null && currentlyEditing != this)
            {
                currentlyEditing.HideEditBox();
            }

            if (activeEditBox != null)
            {
                activeEditBox.Parent?.Controls.Remove(activeEditBox);
                activeEditBox = null;
            }

            TextBox textBox = new TextBox
            {
                Text = HardwareChannel.ToString(),
                Bounds = new Rectangle(0, Width - 5, Width, 20),
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 3
            };

            textBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (int.TryParse(textBox.Text, out int newValue))
                    {
                        HardwareChannel = Math.Max(0, Math.Min(MaxChannel, newValue));
                    }

                    Controls.Remove(textBox);
                    editState = false;
                    currentlyEditing = null;
                    activeEditBox = null;
                    Invalidate();
                }
            };

            Controls.Add(textBox);
            textBox.Focus();

            editState = true;
            currentlyEditing = this;
            activeEditBox = textBox;
        }

        /// <summary>
        /// Hides the currently visible hardware channel editor textbox.
        /// </summary>
        private void HideEditBox()
        {
            if (activeEditBox != null)
            {
                Controls.Remove(activeEditBox);
                activeEditBox = null;
            }

            editState = false;
            currentlyEditing = null;
            Invalidate();
        }

        /// <summary>
        /// Attempts to save the hardware channel from the textbox input.
        /// </summary>
        private void SaveHardwareChannel()
        {
            if (editBox == null) return;
            if (int.TryParse(editBox.Text, out int newChannel))
            {
                HardwareChannel = newChannel;
            }
            editBox.Visible = false;
        }

        /// <summary>
        /// Custom paint logic for drawing the electrode: filled circle, value text, and label.
        /// </summary>
        /// <param name="e">Paint event args.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Rectangle circleRect = new Rectangle(10, 10, Width - 20, Width - 20);
            Color fillColor = IsActive ? ColorMap[Value] : Color.LightGray;

            using (SolidBrush brush = new SolidBrush(fillColor))
                g.FillEllipse(brush, circleRect);

            using (Pen pen = new Pen(Color.Black, 1))
                g.DrawEllipse(pen, circleRect);

            string displayText = editState ? HardwareChannel.ToString() : (Value == 255 ? "Inf" : Value.ToString());
            DrawText(g, displayText, Font, ForeColor, circleRect, ContentAlignment.MiddleCenter);

            if (!editState && !viewHWChannel)
                DrawText(g, LabelText, Font, ForeColor, new Rectangle(0, Width - 5, Width, 20), ContentAlignment.MiddleCenter);
            else if (editBox != null && !editBox.Visible)
                    DrawText(g, HardwareChannel.ToString(), Font, ForeColor, new Rectangle(0, Width - 5, Width, 20), ContentAlignment.MiddleCenter);
            else
                DrawText(g, HardwareChannel.ToString(), Font, ForeColor, new Rectangle(0, Width - 5, Width, 20), ContentAlignment.MiddleCenter);
        }

        /// <summary>
        /// Detects clicks on the bottom label area and enters edit mode.
        /// </summary>
        /// <param name="e">Mouse event args.</param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (new Rectangle(0, Width - 5, Width, 20).Contains(e.Location))
            {
                EditState = true;
                ShowEditBox();
            }
        }

        /// <summary>
        /// Draws centered text within a specified rectangle.
        /// </summary>
        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rect, ContentAlignment alignment)
        {
            using (StringFormat format = new StringFormat())
            using (SolidBrush brush = new SolidBrush(color))
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                g.DrawString(text, font, brush, rect, format);
            }
        }

        /// <summary>
        /// Generates a fallback color map (gradient from green to red) for impedance values.
        /// </summary>
        /// <returns>Array of 256 colors.</returns>
        private static Color[] GenerateColorMap()
        {
            Color[] colors = new Color[256];
            colors[0] = Color.FromArgb(0, 1, 255 - 1, 0);
            for (int i = 1; i < 256; i++)
            {
                colors[i] = Color.FromArgb(255, i, 255 - i, 0);
            }
            return colors;
        }

        /// <summary>
        /// Loads a custom color map from a CSV-like file. Each line should be "R,G,B".
        /// </summary>
        /// <param name="filePath">Path to the color map file.</param>
        /// <returns>Array of 256 colors.</returns>
        private static Color[] LoadColorMapFromFile(string filePath)
        {
            Color[] colors = new Color[256];
            string[] lines = File.ReadAllLines(filePath);

            for (int i = 0; i < Math.Min(lines.Length, 256); i++)
            {
                string[] parts = lines[i].Split(',');
                if (parts.Length == 3 && int.TryParse(parts[0], out int r) &&
                    int.TryParse(parts[1], out int g) && int.TryParse(parts[2], out int b))
                {
                    colors[i] = Color.FromArgb(255, r, g, b);
                }
            }
            return colors;
        }
    }

    /// <summary>
    /// Serializable data structure for storing electrode settings outside the GUI.
    /// </summary>
    public class ElectrodeControlData
    {
        /// <summary>
        /// Gets or sets whether the electrode is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the label text.
        /// </summary>
        public required string LabelText { get; set; }

        /// <summary>
        /// Gets or sets the hardware channel index.
        /// </summary>
        public int HardwareChannel { get; set; }
    }
}

