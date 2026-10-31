namespace Charmeleon
{
    /// <summary>
    /// A small modal dialog that asks for a name and, optionally, a channel; WinForms has no input box of
    /// its own. Built in code, as it has no layout worth a designer file.
    /// </summary>
    sealed class PromptDialog : Form
    {
        readonly TextBox _name = new() { Width = 220 };
        readonly ComboBox _channel = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDown };

        /// <summary>The name typed, trimmed.</summary>
        public string NameText => _name.Text.Trim();

        /// <summary>The channel chosen or typed, trimmed; empty when the dialog asks for none.</summary>
        public string ChannelText => _channel.Text.Trim();

        /// <param name="title">The window title.</param>
        /// <param name="name">The name the field starts with.</param>
        /// <param name="channels">The channels to choose from, or null to ask for a name only.</param>
        /// <param name="channel">The channel the field starts with.</param>
        public PromptDialog(string title, string name, IEnumerable<string>? channels = null, string channel = "")
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            layout.Controls.Add(new Label { Text = "Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            layout.Controls.Add(_name, 1, 0);
            _name.Text = name;
            if (channels != null)
            {
                _channel.Items.AddRange(channels.Cast<object>().ToArray());
                _channel.Text = channel;
                layout.Controls.Add(new Label { Text = "Channel", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
                layout.Controls.Add(_channel, 1, 1);
            }

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.AddRange([cancel, ok]);
            layout.Controls.Add(buttons, 0, 2);
            layout.SetColumnSpan(buttons, 2);

            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
