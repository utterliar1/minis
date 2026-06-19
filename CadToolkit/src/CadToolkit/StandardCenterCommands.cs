using System.Drawing;
using System.Windows.Forms;

#if AUTOCAD
using Autodesk.AutoCAD.Runtime;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif GSTARCAD
using GrxCAD.Runtime;
using CadApp = GrxCAD.ApplicationServices.Application;
#elif ZWCAD
using ZwSoft.ZwCAD.Runtime;
using CadApp = ZwSoft.ZwCAD.ApplicationServices.Application;
#endif

namespace CadToolkit
{
    public partial class CadCommands
    {
        [CommandMethod("CT_STANDARDCENTER")]
        public void StandardCenter()
        {
            EnsureInit();
            if (!CheckDoc()) return;

            try
            {
                string commandName;
                using (var form = new StandardCenterForm())
                {
                    if (form.ShowDialog() != DialogResult.OK) return;
                    commandName = form.CommandName;
                }

                if (string.IsNullOrEmpty(commandName)) return;
                CadApp.DocumentManager.MdiActiveDocument.SendStringToExecute(commandName + " ", true, false, true);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("规范中心打开失败：" + ex.Message, "CadToolkit", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("StandardCenter failed: " + ex);
            }
        }

        class StandardCenterForm : Form
        {
            public string CommandName;

            public StandardCenterForm()
            {
                Text = "规范中心";
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                AutoScaleMode = AutoScaleMode.None;
                ClientSize = new Size(UiScale(430), UiScale(278));
                BackColor = Color.FromArgb(246, 248, 252);

                var title = new Label();
                title.Text = "规范中心";
                title.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
                title.ForeColor = Color.FromArgb(35, 50, 70);
                title.SetBounds(UiScale(18), UiScale(14), UiScale(260), UiScale(28));
                Controls.Add(title);

                var subtitle = new Label();
                subtitle.Text = "统一进入图层、文字和配置检查，后续规范功能也会放在这里。";
                subtitle.Font = new Font("Microsoft YaHei UI", 8.5f);
                subtitle.ForeColor = Color.FromArgb(90, 105, 125);
                subtitle.SetBounds(UiScale(20), UiScale(44), UiScale(390), UiScale(22));
                Controls.Add(subtitle);

                AddActionButton(UiScale(18), UiScale(78), "图层规范", "规范图层、预览迁移、处理白名单", "CT_LAYERSTANDARD");
                AddActionButton(UiScale(18), UiScale(132), "文字规范", "合并文字样式，按需处理块属性和块定义", "CT_TEXTSTYLESTANDARD");
                AddActionButton(UiScale(18), UiScale(186), "配置体检", "检查配置缺项、命令错位和映射错误", "CT_CONFIGCHECK");

                var close = new Button();
                close.Text = "关闭";
                close.DialogResult = DialogResult.Cancel;
                close.SetBounds(UiScale(318), UiScale(238), UiScale(92), UiScale(28));
                close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                Controls.Add(close);
                CancelButton = close;
            }

            void AddActionButton(int left, int top, string title, string description, string commandName)
            {
                var button = new Button();
                button.Text = title + "\r\n" + description;
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(204, 214, 226);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 235, 249);
                button.BackColor = Color.White;
                button.Font = new Font("Microsoft YaHei UI", 9f);
                button.SetBounds(left, top, UiScale(392), UiScale(44));
                button.Click += delegate
                {
                    CommandName = commandName;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                Controls.Add(button);
            }
        }
    }
}
