using System.Collections.Generic;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class TextPromptDialog : Form
    {
        private readonly TextBox _textBox;
        private readonly ComboBox _comboBox;

        public string Value { get; private set; }

        private TextPromptDialog(string title)
        {
            Value = "";
            Text = title ?? "";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var promptLabel = new Label
            {
                Text = title ?? "",
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 6)
            };

            _textBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
            _comboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                Margin = new Padding(0, 0, 0, 12),
                Visible = false
            };

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            var okButton = new Button { Text = "确定", DialogResult = DialogResult.OK, AutoSize = true };
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(okButton);

            layout.Controls.Add(promptLabel, 0, 0);
            layout.Controls.Add(_textBox, 0, 1);
            layout.Controls.Add(_comboBox, 0, 1);
            layout.Controls.Add(buttonPanel, 0, 2);
            Controls.Add(layout);

            AcceptButton = okButton;
            CancelButton = cancelButton;
            FormClosing += OnFormClosing;
        }

        public static TextPromptDialog ForTextInput(string prompt, string defaultValue)
        {
            var dialog = new TextPromptDialog(prompt);
            dialog._textBox.Text = defaultValue ?? "";
            dialog._textBox.SelectAll();
            return dialog;
        }

        public static TextPromptDialog ForComboInput(string title, IEnumerable<string> values)
        {
            var dialog = new TextPromptDialog(title);
            dialog._textBox.Visible = false;
            dialog._comboBox.Visible = true;
            foreach (string value in values ?? new string[0])
            {
                dialog._comboBox.Items.Add(value);
            }
            if (dialog._comboBox.Items.Count > 0) dialog._comboBox.SelectedIndex = 0;
            return dialog;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK) return;
            Value = _comboBox.Visible ? (_comboBox.Text ?? "").Trim() : (_textBox.Text ?? "");
        }
    }
}
