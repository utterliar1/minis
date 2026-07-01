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
        private void DoInsert()
        {
            if (_selectedBlock == null) { MessageBox.Show("请先选择一个块。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            StopPanelBackgroundWork();
            SelectedInsertBlock = _selectedBlock;
            InsertScale = BlockLibrary.InsertScale;
            InsertRotation = BlockLibrary.InsertRotation;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void DoDelete()
        {
            if (_selectedBlock == null) { MessageBox.Show("请先选择一个块。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var dr = MessageBox.Show("确定删除此块文件？\n" + _selectedBlock.Name + "\n\n文件: " + _selectedBlock.FilePath, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr != DialogResult.Yes) return;
            try
            {
                var plan = BlockDeletePlanService.CreatePlan(
                    _selectedBlock,
                    BlockLibrary.ActiveLibrary,
                    BlockLibrary.AllowNasSync,
                    File.Exists,
                    BlockFileOperations.CanOpenForExclusiveWrite);
                string filePath = plan.FilePath;
                string name = plan.BlockName;
                if (plan.Action == BlockDeleteAction.MissingFile) { MessageBox.Show("文件不存在: " + filePath, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (plan.Action == BlockDeleteAction.RecordLocalDeleteRequest)
                {
                    BlockLibrary.RecordLocalChange(LocalChangeAction.DeleteRequest, BlockLibrary.ToLibraryRelativePath(filePath), "", null);
                    MessageBox.Show("已记录删除请求。回到 NAS 后请在同步界面确认删除。", "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (plan.Action == BlockDeleteAction.ReadOnlyNasBlocked) { MessageBox.Show("当前电脑未启用写入 NAS。请先更新本地图库后，在本地副本中操作。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (plan.Action == BlockDeleteAction.FileLocked) { MessageBox.Show("文件被占用，请关闭CAD中打开的此文件后重试。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                // Delete thumbnail cache
                BlockLibrary.RefreshThumbnail(_selectedBlock);
                // Delete the DWG file (移到回收站)
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(filePath,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                catch
                {
                    File.Delete(filePath);
                }
                // Remove from UI
                BlockThumbnailCard cardToRemove = CardCacheService.FindByPath(_cards, filePath) as BlockThumbnailCard;
                if (cardToRemove != null)
                {
                    _flowBlocks.Controls.Remove(cardToRemove);
                    _cards.Remove(cardToRemove);
                    cardToRemove.Dispose();
                }
                // Remove from category cache
                BlockThumbnailCard cached = CardCacheService.RemoveFirstByPath(_categoryCards, filePath);
                if (cached != null) cached.Dispose();
                // Remove from memory cache (remove all size variants)
                var keysToRemove = ThumbnailMemoryCacheService.FindKeysForPath(_thumbCache.Keys, filePath);
                foreach (var k in keysToRemove) { ResourceDisposalService.DisposeQuietly(_thumbCache[k]); _thumbCache.Remove(k); }
                // 从最近列表中移除
                BlockLibrary.RemoveRecentBlock(filePath);
                _selectedBlock = null;
                _lblStatus.Text = "已删除: " + name;
                int visible = CardCacheService.CountVisible(_cards);
                _lblCount.Text = BlockFilterService.FormatCount(visible);
            }
            catch (System.Exception ex) { MessageBox.Show("删除失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void DoRename()
        {
            if (_selectedBlock == null) { MessageBox.Show("请先选择一个块。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string oldName = _selectedBlock.Name;
            string newName = ShowInputDialog("输入新名称:", oldName);
            var plan = BlockRenamePlanService.CreatePlan(_selectedBlock, newName, File.Exists);
            if (plan.Action == BlockRenameAction.Cancel) return;
            if (plan.Action == BlockRenameAction.InvalidName)
            {
                MessageBox.Show("名称为空或包含非法字符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (plan.Action == BlockRenameAction.TargetExists) { MessageBox.Show("同名文件已存在: " + plan.NewName, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                // Rename via BlockLibrary (handles file move + cache rename)
                if (!BlockLibrary.RenameBlock(_selectedBlock, plan.NewName))
                {
                    MessageBox.Show("重命名失败。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                // Update memory cache key (oldPath_size -> newPath_size)
                ThumbnailMemoryCacheService.MovePathEntries(_thumbCache, plan.OldPath, plan.NewPath);
                _lblStatus.Text = "已重命名: " + plan.OldName + " -> " + plan.NewName;
                RefreshCards();
            }
            catch (System.Exception ex) { MessageBox.Show("重命名失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        internal string PendingCategory;
        internal string PendingBlockName;

        private void BtnAddToLib_Click(object sender, EventArgs e)
        {
            var categories = CategorySelectionService.GetUserCategories(BlockLibrary.GetCategories());
            string category = ShowCategoryDialog("选择分类", categories);
            if (category == null) return;
            string name = ShowInputDialog("输入块名称:");
            var plan = AddToLibraryRequestService.CreatePlan(category, name, BlockLibrary.IsSafeLibraryName);
            if (plan.Action == AddToLibraryRequestAction.Cancel) return;
            if (plan.Action == AddToLibraryRequestAction.InvalidName)
            {
                MessageBox.Show("分类或名称为空，或包含非法字符。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            PendingCategory = plan.Category;
            PendingBlockName = plan.BlockName;
            PendingCommand = plan.PendingCommand;
            StopPanelBackgroundWork();
            this.DialogResult = DialogResult.Abort;
            this.Close();
        }

        private void BtnExportBlock_Click(object sender, EventArgs e)
        {
            PendingCommand = "BBEXPORT";
            StopPanelBackgroundWork();
            this.DialogResult = DialogResult.Abort;
            this.Close();
        }
    }
}
