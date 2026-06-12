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
        private void ShowSyncCenterDialog()
        {
            using (var dlg = new SyncCenterDialog(
                () => BlockLibrary.PreviewLocalSync(),
                () => BlockLibrary.SyncSafeUploadsToNas(),
                BlockLibrary.SyncLogPath))
            {
                dlg.ShowDialog(this);
                _lblStatus.Text = GetActiveLibraryStatus();
            }
        }

        private void ShowStatusDiagnosticsDialog()
        {
            try
            {
                string report = StatusDiagnosticsService.FormatReport(
                    BlockLibrary.AppVersion,
                    BlockLibrary.PlatformName,
                    BlockLibrary.CurrentLibraryMode,
                    BlockLibrary.ActiveLibrary,
                    BlockLibrary.LibraryPath,
                    BlockLibrary.NasLibraryPath,
                    BlockLibrary.LocalMirrorPath,
                    Directory.Exists(BlockLibrary.NasLibraryPath ?? ""),
                    Directory.Exists(BlockLibrary.LocalMirrorPath ?? ""),
                    BlockLibrary.AllowNasSync,
                    CountLocalChanges(),
                    CountThumbnailCacheFiles(),
                    BlockLibrary.SyncUserName,
                    BlockLibrary.LocalJournalPath,
                    BlockLibrary.ThumbnailCachePath);
                using (var dlg = new StatusDiagnosticsDialog(report))
                {
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("状态诊断失败: " + ex.Message, "块浏览器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int CountLocalChanges()
        {
            return ChangeJournal.Load(BlockLibrary.LocalJournalPath).Count;
        }

        private int CountThumbnailCacheFiles()
        {
            string cachePath = BlockLibrary.ThumbnailCachePath;
            if (string.IsNullOrEmpty(cachePath) || !Directory.Exists(cachePath)) return 0;
            return Directory.GetFiles(cachePath, "*.png", SearchOption.AllDirectories).Length;
        }
    }
}
