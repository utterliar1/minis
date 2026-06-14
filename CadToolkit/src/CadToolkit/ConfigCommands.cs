using System.Drawing;
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
                close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                close.SetBounds(ClientSize.Width - UiScale(108), ClientSize.Height - UiScale(40), UiScale(96), UiScale(28));
                close.Click += delegate { Close(); };
                Controls.Add(close);

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
