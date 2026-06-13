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
        private void LoadData()
        {
            try
            {
                _thumbTimer.Stop();
                ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
                _cards.Clear();
                _flowBlocks.Controls.Clear();

                _lblStatus.Text = "加载中...";
                this.Refresh();

                RefreshCategories();

                LoadBlocks();
            }
            catch (System.Exception ex)
            {
                _lblStatus.Text = "错误: " + ex.Message;
            }
        }
        private void LoadBlocks()
        {
            _searchTimer.Stop();
            if (_txtSearch.Text.Length > 0) _txtSearch.Text = "";
            ShowBlocks(BlockLibrary.GetBlocks(_currentCategory));
        }

        private void ShowBlocks(List<BlockInfo> blocks)
        {
            _thumbTimer.Stop();
            _selectedBlock = null;
            _flowBlocks.SuspendLayout();
            _flowBlocks.Controls.Clear();
            _cards.Clear();

            string catKey = _currentCategory ?? "全部";
            if (!_categoryCards.ContainsKey(catKey))
            {
                // 首次访问该分类：创建卡片并缓存
                var newCards = new List<BlockThumbnailCard>();
                foreach (var block in blocks)
                {
                    var card = new BlockThumbnailCard(block, _thumbSize);
                    card.BlockClicked += (s, b) =>
                    {
                        int sy = 0;
                        try { sy = _flowBlocks.VerticalScroll.Value; } catch { }
                        _flowBlocks.SuspendLayout();
                        _selectedBlock = b;
                        foreach (var c2 in _cards)
                        {
                            if (c2.IsSelected && c2 != s) { c2.IsSelected = false; break; }
                        }
                        ((BlockThumbnailCard)s).IsSelected = true;
                        ShowBlockInfo(b);
                        _flowBlocks.ResumeLayout(false);
                        try { if (sy > 0 && _flowBlocks.VerticalScroll.Visible) _flowBlocks.VerticalScroll.Value = Math.Min(sy, _flowBlocks.VerticalScroll.Maximum); } catch { }
                    };
                    card.BlockDoubleClicked += (s, b) => { _selectedBlock = b; DoInsert(); };
                    string ck = ThumbnailMemoryCacheService.GetKey(block.FilePath, _thumbSize);
                    if (_thumbCache.ContainsKey(ck) && _thumbCache[ck] != null)
                    {
                        try { card.LoadThumbnail(new Bitmap(_thumbCache[ck])); }
                        catch { ResourceDisposalService.DisposeQuietly(_thumbCache[ck]); _thumbCache.Remove(ck); }
                    }
                    newCards.Add(card);
                }
                _categoryCards[catKey] = newCards;
            }

            // 从缓存取出卡片显示
            var cached = _categoryCards[catKey];
            _cards.AddRange(cached);
            foreach (var card in _cards) { card.IsSelected = false; _flowBlocks.Controls.Add(card); }
            _flowBlocks.ResumeLayout();

            _lblCount.Text = BlockFilterService.FormatCount(_cards.Count);
            _lblStatus.Text = GetActiveLibraryStatus();

            QueueVisibleMissingThumbnails();
        }
        private void DoFilter()
        {
            string kw = _txtSearch.Text;
            int visible = 0;
            int sy = 0;
            try { sy = _flowBlocks.VerticalScroll.Value; } catch { }
            _flowBlocks.SuspendLayout();
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card.IsDisposed) continue;
                bool match = BlockFilterService.Matches(card.Block, kw);
                card.Visible = match;
                if (match) visible++;
            }
            _flowBlocks.ResumeLayout();
            try { if (sy > 0 && _flowBlocks.VerticalScroll.Visible) _flowBlocks.VerticalScroll.Value = Math.Min(sy, _flowBlocks.VerticalScroll.Maximum); } catch { }
            _lblCount.Text = BlockFilterService.FormatCount(visible);
            QueueVisibleMissingThumbnails();
        }
        private void ShowBlockInfo(BlockInfo block)
        {
            _lblStatus.Text = BlockInfoStatusService.Format(block);
        }
    }
}
