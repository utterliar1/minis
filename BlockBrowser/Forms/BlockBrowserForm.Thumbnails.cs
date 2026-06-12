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
        private List<BlockThumbnailCard> _pendingThumbCards = new List<BlockThumbnailCard>();
        private int _failCount;

        private bool HasThumbnail(BlockThumbnailCard card)
        {
            return ThumbnailMemoryCacheService.HasValue(_thumbCache, card.Block.FilePath, _thumbSize);
        }

        private void QueueVisibleMissingThumbnails()
        {
            _thumbTimer.Stop();
            var needLoad = _cards.Where(c => BlockFilterService.Matches(c.Block, _txtSearch.Text) && !HasThumbnail(c)).ToList();
            _failCount = 0;
            _pendingThumbCards = needLoad;
            _thumbIndex = 0;
            if (needLoad.Count > 0) _thumbTimer.Start();
        }

        // Load thumbnails one by one on UI thread (safe for GstarCAD API)
        private void ThumbTimerTick(object sender, EventArgs e)
        {
            for (int i = 0; i < ThumbnailLoadProgressService.DefaultBatchSize; i++)
            {
                if (ThumbnailLoadProgressService.IsComplete(_thumbIndex, _pendingThumbCards.Count))
                {
                    _thumbTimer.Stop();
                    _lblStatus.Text = GetActiveLibraryStatus();
                    return;
                }
                var card = _pendingThumbCards[_thumbIndex];
                _thumbIndex++;
                try
                {
                    if (card.IsDisposed) continue;
                    var img = BlockLibrary.GetThumbnail(card.Block, _thumbSize);
                    if (img != null)
                    {
                        string ck = ThumbnailMemoryCacheService.GetKey(card.Block.FilePath, _thumbSize);
                        if (!_thumbCache.ContainsKey(ck))
                            _thumbCache[ck] = img;
                        else
                            img.Dispose();
                        card.LoadThumbnail(new Bitmap(_thumbCache[ck]));
                    }
                }
                catch { _failCount++; }
            }
            if (!ThumbnailLoadProgressService.IsComplete(_thumbIndex, _pendingThumbCards.Count))
                _lblStatus.Text = ThumbnailLoadProgressService.FormatLoadingStatus(_thumbIndex, _pendingThumbCards.Count);
            else if (_failCount > 0)
                _lblStatus.Text = ThumbnailLoadProgressService.FormatFailedReadyStatus(_failCount);
        }

        private void RefreshCards()
        {
            _thumbTimer.Stop();
            // 清除内存缓存，因为尺寸变了
            ResourceDisposalService.DisposeDictionaryValuesAndClear(_thumbCache);
            // 清除分类卡片缓存，重建卡片
            ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
            _cards.Clear();
            _flowBlocks.Controls.Clear();
            ShowBlocks(BlockLibrary.GetBlocks(_currentCategory));
        }

        private void PrebuildVisibleThumbnails()
        {
            var needLoad = _cards.Where(c => c.Visible && !HasThumbnail(c)).ToList();
            if (needLoad.Count == 0)
            {
                _lblStatus.Text = "当前列表缩略图已就绪。";
                return;
            }

            _failCount = 0;
            _pendingThumbCards = needLoad;
            _thumbIndex = 0;
            _thumbTimer.Start();
            _lblStatus.Text = ThumbnailLoadProgressService.FormatLoadingStatus(0, needLoad.Count);
        }

        private void RebuildThumbnails()
        {
            var dr = MessageBox.Show(
                "这会清空缩略图缓存，并重新生成当前列表的缩略图。是否继续？",
                "重建缩略图",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (dr != DialogResult.Yes) return;

            try
            {
                _thumbTimer.Stop();
                ResourceDisposalService.DisposeDictionaryValuesAndClear(_thumbCache);
                string cachePath = BlockLibrary.ThumbnailCachePath;
                if (Directory.Exists(cachePath)) Directory.Delete(cachePath, true);
                RefreshCards();
                _lblStatus.Text = "缩略图缓存已重建。";
            }
            catch (Exception ex)
            {
                MessageBox.Show("重建缩略图失败: " + ex.Message, "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
