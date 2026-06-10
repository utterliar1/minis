using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlockBrowser
{
    public class InsertSettingsDialog : Form
    {
        private readonly NumericUpDown _numScale;
        private readonly NumericUpDown _numRotation;

        public InsertSettingsDialog(double insertScale, double insertRotationDegrees)
        {
            Text = "插入设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            var layout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Dock = DockStyle.Fill
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var valuePanel = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                RowCount = 1,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 14)
            };
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            valuePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

            _numScale = new NumericUpDown
            {
                Width = 120,
                Minimum = 0.001m,
                Maximum = 10000,
                DecimalPlaces = 3,
                Value = ClampDecimal((decimal)insertScale, 0.001m, 10000m, 1m),
                Increment = 0.1m,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 18, 0)
            };

            _numRotation = new NumericUpDown
            {
                Width = 110,
                Minimum = -360,
                Maximum = 360,
                DecimalPlaces = 1,
                Value = ClampDecimal((decimal)insertRotationDegrees, -360m, 360m, 0m),
                Increment = 5,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0)
            };

            AddLabel(valuePanel, 0, "插入比例:");
            valuePanel.Controls.Add(_numScale, 1, 0);
            AddLabel(valuePanel, 2, "旋转角度:");
            valuePanel.Controls.Add(_numRotation, 3, 0);

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            var btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.System,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(8, 0, 0, 0)
            };
            var btnOk = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.System,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(8, 0, 0, 0)
            };
            buttonPanel.Controls.Add(btnCancel);
            buttonPanel.Controls.Add(btnOk);

            layout.Controls.Add(valuePanel, 0, 0);
            layout.Controls.Add(buttonPanel, 0, 1);
            Controls.Add(layout);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        public double InsertScaleValue
        {
            get { return (double)_numScale.Value; }
        }

        public double InsertRotationDegreesValue
        {
            get { return (double)_numRotation.Value; }
        }

        private static void AddLabel(TableLayoutPanel panel, int column, string text)
        {
            panel.Controls.Add(new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 4, 8, 0)
            }, column, 0);
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max, decimal fallback)
        {
            if (value < min) return fallback;
            if (value > max) return max;
            return value;
        }
    }
}
