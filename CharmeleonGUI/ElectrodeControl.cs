using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CharmeleonGUI
{
    public partial class ElectrodeControl : UserControl
    {
        private bool isActive = true;
        private bool editState = false;
        public static bool viewHWChannel = false;
        private int value = 0;
        private string labelText = "Label";
        public int hardwareChannel = 0;
        private int maxChannel = 255;
        private TextBox? editBox = null;
        public static Color[] ColorMap = File.Exists("Resources/heat.map") ? LoadColorMapFromFile("Resources/heat.map") : GenerateColorMap();

        private static ElectrodeControl? currentlyEditing = null;
        private static TextBox? activeEditBox = null;

        public ElectrodeControl()
        {
            this.Size = new Size(50, 70);
            this.DoubleBuffered = true;
            this.Click += (s, e) => ToggleActiveState();
            InitTextBox();
        }

        [Category("Appearance")]
        public bool IsActive
        {
            get => isActive;
            set { isActive = value; Invalidate(); }
        }

        [Category("Behavior")]
        public bool EditState
        {
            get => editState;
            set { editState = value; Invalidate(); }
        }

        [Category("Data")]
        public int Value
        {
            get => value;
            set { this.value = Math.Max(0, Math.Min(255, value)); Invalidate(); }
        }

        [Category("Appearance")]
        public string LabelText
        {
            get => labelText;
            set { labelText = value; Invalidate(); }
        }

        [Category("Data")]
        public int HardwareChannel
        {
            get => hardwareChannel;
            set { hardwareChannel = Math.Max(0, Math.Min(maxChannel, value)); Invalidate(); }
        }

        [Category("Data")]
        public int MaxChannel
        {
            get => maxChannel;
            set { maxChannel = Math.Max(1, value); Invalidate(); }
        }

        private void InitTextBox()
        {
            editBox = new TextBox { Visible = false, TextAlign = HorizontalAlignment.Center };
            editBox.Leave += (s, e) => SaveHardwareChannel();  // Save the value on losing focus
            editBox.KeyPress += (s, e) =>
            {
                // Only allow digits and Backspace
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back && e.KeyChar != (char)Keys.Enter)
                    e.Handled = true;

                // If Enter key is pressed, finalize editing
                if (e.KeyChar == (char)Keys.Enter)
                {
                    SaveHardwareChannel();
                    // After saving, toggle back to display mode
                    EditState = false;
                    editBox.Visible = false;
                    Invalidate(); // Refresh to show updated label
                }
            };
            Controls.Add(editBox);
        }

        private void ToggleActiveState()
        {
            IsActive = !IsActive;
        }

        private void ShowEditBox()
        {
            // Close previous one if it exists
            if (currentlyEditing != null && currentlyEditing != this)
            {
                currentlyEditing.HideEditBox();
            }

            // Also remove any lingering edit box
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



        private void SaveHardwareChannel()
        {
            if (int.TryParse(editBox.Text, out int newChannel))
            {
                HardwareChannel = newChannel;
            }
            editBox.Visible = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Circle Rectangle
            Rectangle circleRect = new Rectangle(10, 10, Width - 20, Width - 20);

            // Draw Circle
            Color fillColor = IsActive ? ColorMap[Value] : Color.LightGray;
            using (SolidBrush brush = new SolidBrush(fillColor))
            {
                g.FillEllipse(brush, circleRect);
            }

            // Draw Border
            using (Pen pen = new Pen(Color.Black, 1))
            {
                g.DrawEllipse(pen, circleRect);
            }

            // Draw Label
            string displayText = editState ? HardwareChannel.ToString() : (Value == 255 ? "Inf" : Value.ToString());
            DrawText(g, displayText, Font, ForeColor, circleRect, ContentAlignment.MiddleCenter);

            // Bottom Label
            if (!editState && !viewHWChannel)
                DrawText(g, LabelText, Font, ForeColor, new Rectangle(0, Width - 5, Width, 20), ContentAlignment.MiddleCenter);
            else if (!editBox.Visible)
                DrawText(g, HardwareChannel.ToString(), Font, ForeColor, new Rectangle(0, Width - 5, Width, 20), ContentAlignment.MiddleCenter);
            else
                DrawText(g, HardwareChannel.ToString(), Font, ForeColor, new Rectangle(0, Width - 5, Width, 20), ContentAlignment.MiddleCenter);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            // If user clicks on the label, enter edit state
            if (new Rectangle(0, Width - 5, Width, 20).Contains(e.Location))
            {
                // Enter edit mode
                EditState = true;
                ShowEditBox();
            }
        }

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
    public class ElectrodeControlData
    {  // This class is used to store the data for each electrode to make this serializeable.
        public bool IsActive { get; set; }
        //public int Value { get; set; }
        public required string LabelText { get; set; }
        public int HardwareChannel { get; set; }
    }
}
