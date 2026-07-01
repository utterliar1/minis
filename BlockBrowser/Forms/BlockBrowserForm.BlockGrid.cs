using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BlockBrowser
{
    public partial class BlockBrowserForm
    {
        private const int InitialCardBuildBatchSize = 18;
        private const int CardBuildBatchSize = 24;

        private async void LoadData()
        {
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            if (_initialLoadQueued)
            {
                _lblStatus.Text = "加载中...";
                return;
            }

            _initialLoadQueued = true;
            try
            {
                _thumbTimer.Stop();
                ResourceDisposalService.DisposeDictionaryValuesAndClear(_categoryCards);
                _cards.Clear();
                _flowBlocks.Controls.Clear();

                _lblStatus.Text = "加载中...";
                this.Refresh();
                _catBar.Controls.Clear();

                var categoriesTask = Task.Run(() => BlockLibrary.GetBrowsableCategories());
                var blocksTask = Task.Run(() => BlockLibrary.GetBlocks(_currentCategory));
                List<string> categories = await categoriesTask;
                List<BlockInfo> blocks = await blocksTask;
                if (IsDisposed) return;
                RefreshCategories(categories);
                ShowBlocks(blocks);
            }
            catch (System.Exception ex)
            {
                _lblStatus.Text = "错误: " + ex.Message;
            }
            finally
            {
                _initialLoadQueued = false;
            }
        }
        private async void LoadBlocks()
        {
            await LoadBlocksAsync();
        }

        private async Task LoadBlocksAsync()
        {
            _searchTimer.Stop();
            if (_txtSearch.Text.Length > 0) _txtSearch.Text = "";

            try
            {
                _lblStatus.Text = "加载中...";
                var blocks = await Task.Run(() => BlockLibrary.GetBlocks(_currentCategory));
                if (IsDisposed) return;
                ShowBlocks(blocks);
            }
            catch (System.Exception ex)
            {
                _lblStatus.Text = "错误: " + ex.Message;
            }
        }

        private void ShowBlocks(List<BlockInfo> blocks)
        {
            _thumbTimer.Stop();
            CancelPendingCardBuild();
            _selectedBlock = null;
            _flowBlocks.BeginBulkUpdate();
            _flowBlocks.SuspendLayout();
            _flowBlocks.Controls.Clear();
            _cards.Clear();

            string catKey = _currentCategory ?? "全部";
            if (!_categoryCards.ContainsKey(catKey))
            {
                BeginCardBuild(catKey, blocks ?? new List<BlockInfo>());
                return;
            }

            // 从缓存取出卡片显示
            var cached = _categoryCards[catKey];
            _cards.AddRange(cached);
            foreach (var card in _cards) { card.IsSelected = false; _flowBlocks.Controls.Add(card); }
            _flowBlocks.ResumeLayout();
            _flowBlocks.EndBulkUpdate();
            RefreshBlockGridPaint();

            _lblCount.Text = BlockFilterService.FormatCount(_cards.Count);
            _lblStatus.Text = GetActiveLibraryStatus();

            QueueVisibleMissingThumbnails();
        }

        private void BeginCardBuild(string catKey, List<BlockInfo> blocks)
        {
            _pendingCategoryKey = catKey;
            _pendingBlocks = blocks ?? new List<BlockInfo>();
            _pendingBuiltCards = new List<BlockThumbnailCard>();
            _pendingCardIndex = 0;
            _cardLoadVersion++;

            int warmCount = Math.Min(InitialCardBuildBatchSize, _pendingBlocks.Count);
            for (int i = 0; i < warmCount; i++)
            {
                _pendingBuiltCards.Add(CreateCard(_pendingBlocks[i]));
            }

            _cards.AddRange(_pendingBuiltCards);
            foreach (var card in _cards) { card.IsSelected = false; _flowBlocks.Controls.Add(card); }
            _flowBlocks.ResumeLayout();
            _flowBlocks.EndBulkUpdate();
            RefreshBlockGridPaint();

            _lblCount.Text = BlockFilterService.FormatCount(_pendingBlocks.Count);
            _lblStatus.Text = GetActiveLibraryStatus();

            if (_pendingBlocks.Count <= warmCount)
            {
                _categoryCards[catKey] = _pendingBuiltCards;
                _pendingBlocks = new List<BlockInfo>();
                _pendingBuiltCards = new List<BlockThumbnailCard>();
                QueueVisibleMissingThumbnails();
                return;
            }

            _pendingCardIndex = warmCount;
            if (_cardTimer == null)
            {
                _cardTimer = new System.Windows.Forms.Timer { Interval = 15 };
                _cardTimer.Tick += CardTimerTick;
            }
            _cardTimer.Stop();
            _cardTimer.Start();
            QueueVisibleMissingThumbnails();
        }

        private void CardTimerTick(object sender, EventArgs e)
        {
            if (_pendingBlocks == null || _pendingCardIndex >= _pendingBlocks.Count)
            {
                CompletePendingCardBuild();
                return;
            }

            int version = _cardLoadVersion;
            int added = 0;
            _flowBlocks.BeginBulkUpdate();
            _flowBlocks.SuspendLayout();
            while (added < CardBuildBatchSize && _pendingCardIndex < _pendingBlocks.Count)
            {
                if (version != _cardLoadVersion || IsDisposed)
                {
                    _flowBlocks.ResumeLayout(false);
                    _flowBlocks.EndBulkUpdate();
                    return;
                }
                var card = CreateCard(_pendingBlocks[_pendingCardIndex]);
                _pendingBuiltCards.Add(card);
                _cards.Add(card);
                _flowBlocks.Controls.Add(card);
                card.IsSelected = false;
                _pendingCardIndex++;
                added++;
            }
            _flowBlocks.ResumeLayout();
            _flowBlocks.EndBulkUpdate();
            RefreshBlockGridPaint();
            RequestVisibleThumbnailQueue();

            if (_pendingCardIndex >= _pendingBlocks.Count)
                CompletePendingCardBuild();
            else
                _lblStatus.Text = "加载中... " + _pendingCardIndex + "/" + _pendingBlocks.Count;
        }

        private void CompletePendingCardBuild()
        {
            if (_cardTimer != null) _cardTimer.Stop();
            if (!string.IsNullOrEmpty(_pendingCategoryKey))
                _categoryCards[_pendingCategoryKey] = _pendingBuiltCards;
            _pendingBlocks = new List<BlockInfo>();
            _pendingBuiltCards = new List<BlockThumbnailCard>();
            _pendingCategoryKey = "";
            _pendingCardIndex = 0;
            QueueVisibleMissingThumbnails();
        }

        private void CancelPendingCardBuild()
        {
            if (_cardTimer != null) _cardTimer.Stop();
            _cardLoadVersion++;
            if (_pendingBuiltCards != null)
            {
                ResourceDisposalService.DisposeAll(_pendingBuiltCards);
                _pendingBuiltCards.Clear();
            }
            _pendingBlocks = new List<BlockInfo>();
            _pendingCategoryKey = "";
            _pendingCardIndex = 0;
        }

        private BlockThumbnailCard CreateCard(BlockInfo block)
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
            return card;
        }

        private void RefreshBlockGridPaint()
        {
            if (_flowBlocks == null || _flowBlocks.IsDisposed) return;
            _flowBlocks.PerformLayout();
            _flowBlocks.Invalidate(true);
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
