using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using CadToolkit.Core;

#if AUTOCAD
using Autodesk.AutoCAD.Runtime;
#elif GSTARCAD
using GrxCAD.Runtime;
#elif ZWCAD
using ZwSoft.ZwCAD.Runtime;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        [CommandMethod("CT_CONFIGMAINTAIN")]
        public void ConfigMaintain()
        {
            EnsureInit();
            try
            {
                using (var form = new ConfigMaintenanceForm(Config.ConfigPath))
                {
                    form.ShowDialog();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("配置维护打开失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("ConfigMaintain failed: " + ex);
            }
        }

        [CommandMethod("CT_CONFIGCHECK")]
        public void ConfigCheck()
        {
            EnsureInit();
            try
            {
                using (var form = new ConfigCheckForm(Config.ConfigPath))
                {
                    form.ShowDialog();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("配置体检失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("ConfigCheck failed: " + ex);
            }
        }

        class ConfigMaintenanceForm : Form
        {
            readonly string _path;
            readonly Label _status;

            public ConfigMaintenanceForm(string path)
            {
                _path = path;
                Text = "配置维护";
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                AutoScaleMode = AutoScaleMode.None;
                ClientSize = new Size(UiScale(500), UiScale(252));
                BackColor = Color.FromArgb(246, 248, 252);

                var title = new Label();
                title.Text = "配置维护";
                title.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
                title.ForeColor = Color.FromArgb(35, 50, 70);
                title.SetBounds(UiScale(18), UiScale(14), UiScale(220), UiScale(28));
                Controls.Add(title);

                var hint = new Label();
                hint.Text = "用于打开、定位和修复当前 CadToolkit.ini。修复前会自动备份。";
                hint.Font = new Font("Microsoft YaHei UI", 8.5f);
                hint.ForeColor = Color.FromArgb(90, 105, 125);
                hint.SetBounds(UiScale(20), UiScale(44), UiScale(456), UiScale(22));
                Controls.Add(hint);

                var pathLabel = new Label();
                pathLabel.Text = "当前配置：";
                pathLabel.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
                pathLabel.ForeColor = Color.FromArgb(55, 70, 90);
                pathLabel.SetBounds(UiScale(20), UiScale(78), UiScale(80), UiScale(22));
                Controls.Add(pathLabel);

                var pathBox = new TextBox();
                pathBox.Text = _path;
                pathBox.ReadOnly = true;
                pathBox.BorderStyle = BorderStyle.FixedSingle;
                pathBox.Font = new Font("Microsoft YaHei UI", 9f);
                pathBox.SetBounds(UiScale(100), UiScale(76), UiScale(376), UiScale(24));
                Controls.Add(pathBox);

                AddButton(UiScale(20), UiScale(118), "打开配置", "用默认编辑器打开当前 CadToolkit.ini", OpenConfigFile);
                AddButton(UiScale(260), UiScale(118), "打开目录", "打开配置所在文件夹", OpenConfigDirectory);
                AddButton(UiScale(20), UiScale(162), "配置体检", "打开中文体检报告窗口", RunConfigCheck);
                AddButton(UiScale(260), UiScale(162), "自动修复", "补缺项并自动备份原配置", RepairConfig);

                _status = new Label();
                _status.Text = File.Exists(_path) ? "配置文件存在，可以维护。" : "配置文件不存在，建议先运行配置体检。";
                _status.Font = new Font("Microsoft YaHei UI", 8.5f);
                _status.ForeColor = Color.FromArgb(95, 105, 115);
                _status.SetBounds(UiScale(20), UiScale(208), UiScale(330), UiScale(24));
                Controls.Add(_status);

                var close = new Button();
                close.Text = "关闭";
                close.DialogResult = DialogResult.Cancel;
                close.SetBounds(UiScale(380), UiScale(206), UiScale(96), UiScale(28));
                close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                Controls.Add(close);
                CancelButton = close;
            }

            void AddButton(int left, int top, string title, string description, System.Action action)
            {
                var button = new Button();
                button.Text = title + "\r\n" + description;
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(204, 214, 226);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 235, 249);
                button.BackColor = Color.White;
                button.Font = new Font("Microsoft YaHei UI", 9f);
                button.SetBounds(left, top, UiScale(216), UiScale(34));
                button.Click += delegate { action(); };
                Controls.Add(button);
            }

            void OpenConfigFile()
            {
                try
                {
                    if (!File.Exists(_path))
                    {
                        MessageBox.Show("配置文件不存在：" + _path, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Process.Start(new ProcessStartInfo { FileName = _path, UseShellExecute = true });
                    _status.Text = "已打开当前 CadToolkit.ini。";
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("打开配置失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            void OpenConfigDirectory()
            {
                try
                {
                    string directory = Path.GetDirectoryName(_path);
                    if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    {
                        MessageBox.Show("配置目录不存在：" + directory, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
                    _status.Text = "已打开配置目录。";
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("打开目录失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            void RunConfigCheck()
            {
                try
                {
                    using (var form = new ConfigCheckForm(_path))
                    {
                        form.ShowDialog(this);
                    }
                    _status.Text = "配置体检已关闭。";
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("配置体检失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            void RepairConfig()
            {
                try
                {
                    if (MessageBox.Show("将自动修复缺失基础项和官方命令，修复前会自动备份原配置。继续？", "配置维护", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                        return;

                    var result = ConfigDiagnostics.RepairFile(_path);
                    string message = string.IsNullOrEmpty(result.BackupPath)
                        ? "配置已检查，未生成备份。"
                        : "配置已修复，备份文件：" + result.BackupPath;
                    _status.Text = "自动修复完成。";
                    MessageBox.Show(message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("自动修复失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        class ConfigCheckForm : Form
        {
            readonly string _path;
            readonly TextBox _report;
            readonly Button _repair;

            public ConfigCheckForm(string path)
            {
                _path = path;
                Text = "配置体检";
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(UiScale(760), UiScale(540));
                MinimumSize = new Size(UiScale(640), UiScale(420));
                AutoScaleMode = AutoScaleMode.None;

                _report = new TextBox();
                _report.Multiline = true;
                _report.ReadOnly = true;
                _report.ScrollBars = ScrollBars.Both;
                _report.WordWrap = false;
                _report.Font = new Font("Microsoft YaHei UI", 9f);
                _report.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                _report.SetBounds(UiScale(12), UiScale(12), ClientSize.Width - UiScale(24), ClientSize.Height - UiScale(62));
                Controls.Add(_report);

                var copy = new Button();
                copy.Text = "复制报告";
                copy.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                copy.SetBounds(ClientSize.Width - UiScale(300), ClientSize.Height - UiScale(40), UiScale(88), UiScale(28));
                copy.Click += delegate { CopyReport(); };
                Controls.Add(copy);

                _repair = new Button();
                _repair.Text = "自动修复";
                _repair.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                _repair.SetBounds(ClientSize.Width - UiScale(204), ClientSize.Height - UiScale(40), UiScale(88), UiScale(28));
                _repair.Click += delegate { RepairAndRefresh(); };
                Controls.Add(_repair);

                var close = new Button();
                close.Text = "关闭";
                close.DialogResult = DialogResult.Cancel;
                close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                close.SetBounds(ClientSize.Width - UiScale(108), ClientSize.Height - UiScale(40), UiScale(96), UiScale(28));
                close.Click += delegate { Close(); };
                Controls.Add(close);
                CancelButton = close;

                RefreshReport(ConfigDiagnostics.AnalyzeFile(_path));
            }

            void CopyReport()
            {
                try
                {
                    Clipboard.SetText(_report.Text ?? "");
                    MessageBox.Show("已复制报告。", "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("复制报告失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            void RepairAndRefresh()
            {
                try
                {
                    var result = ConfigDiagnostics.RepairFile(_path);
                    RefreshReport(result);
                    string message = string.IsNullOrEmpty(result.BackupPath)
                        ? "自动修复完成。"
                        : "自动修复完成，修复前配置已备份。";
                    MessageBox.Show(message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("自动修复失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            void RefreshReport(ConfigDiagnosticResult result)
            {
                _report.Text = ConfigDiagnostics.FormatReport(result);
                _repair.Enabled = result != null && HasFixableIssue(result);
            }

            static bool HasFixableIssue(ConfigDiagnosticResult result)
            {
                foreach (ConfigDiagnosticIssue issue in result.Issues)
                {
                    if (issue.CanFix)
                        return true;
                }

                return false;
            }
        }
    }
}
