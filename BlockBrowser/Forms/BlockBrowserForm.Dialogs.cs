using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace BlockBrowser
{
    public partial class BlockBrowserForm
    {
        private string ShowInputDialog(string prompt)
        {
            return ShowInputDialog(prompt, "");
        }

        private string ShowInputDialog(string prompt, string defaultValue)
        {
            using (var form = TextPromptDialog.ForTextInput(prompt, defaultValue))
            {
                return form.ShowDialog(this) == DialogResult.OK ? form.Value : null;
            }
        }

        private string ShowCategoryDialog(string title, List<string> categories)
        {
            using (var form = TextPromptDialog.ForComboInput(title, categories))
            {
                return form.ShowDialog(this) == DialogResult.OK ? form.Value : null;
            }
        }

        private void ShowSettingsDialog()
        {
            using (var form = new SettingsDialog(
                BlockLibrary.NasLibraryPath,
                BlockLibrary.LocalMirrorPath,
                BlockLibrary.GetProtectedLocalCategoriesText(),
                BlockLibrary.CurrentLibraryMode))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    var plan = SettingsUpdateService.CreatePlan(
                        BlockLibrary.NasLibraryPath,
                        form.NasLibraryPathValue,
                        BlockLibrary.LocalMirrorPath,
                        form.LocalMirrorPathValue,
                        BlockLibrary.GetProtectedLocalCategoriesText(),
                        form.ProtectedLocalCategoriesValue,
                        BlockLibrary.CurrentLibraryMode,
                        form.CurrentLibraryModeValue,
                        BlockLibrary.InsertScale,
                        BlockLibrary.InsertRotation * 180.0 / Math.PI,
                        Directory.Exists);

                    if (!plan.IsValid) { MessageBox.Show("NAS 主图库路径和本地副本路径不能为空。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                    if (plan.RequiresLocalMirrorDirectoryCreation)
                    {
                        var dr = MessageBox.Show("本地副本目录不存在，是否创建？\n" + plan.LocalMirrorPath, "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (dr == DialogResult.Yes) { try { Directory.CreateDirectory(plan.LocalMirrorPath); } catch (Exception ex) { MessageBox.Show("创建失败: " + ex.Message); return; } }
                        else return;
                    }
                    BlockLibrary.InsertScale = plan.InsertScale;
                    BlockLibrary.InsertRotation = plan.InsertRotationRadians;
                    if (plan.NasLibraryPathChanged || plan.LocalMirrorPathChanged || plan.ProtectedLocalCategoriesChanged || plan.CurrentLibraryModeChanged)
                    {
                        BlockLibrary.NasLibraryPath = plan.NasLibraryPath;
                        BlockLibrary.LocalMirrorPath = plan.LocalMirrorPath;
                        BlockLibrary.SetProtectedLocalCategoriesFromText(plan.ProtectedLocalCategories);
                        BlockLibrary.CurrentLibraryMode = plan.CurrentLibraryMode;
                        BlockLibrary.RefreshActiveLibrary();
                        ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
                        LoadData();
                    }
                    BlockLibrary.SaveConfig();
                }
            }
        }

        private void ShowInsertSettingsDialog()
        {
            using (var form = new InsertSettingsDialog(
                BlockLibrary.InsertScale,
                BlockLibrary.InsertRotation * 180.0 / Math.PI))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    BlockLibrary.InsertScale = form.InsertScaleValue;
                    BlockLibrary.InsertRotation = form.InsertRotationDegreesValue * Math.PI / 180.0;
                    BlockLibrary.SaveConfig();
                    _lblStatus.Text = "插入设置已更新。";
                }
            }
        }
    }
}
