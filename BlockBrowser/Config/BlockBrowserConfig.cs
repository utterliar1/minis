using System;
using System.Collections.Generic;
using System.IO;

namespace BlockBrowser
{
    public sealed class BlockBrowserConfig
    {
        public BlockBrowserConfig()
        {
            RecentBlocks = new List<string>();
            ProtectedLocalCategories = new List<string>();
        }

        public string LibraryPath { get; set; }
        public string NasLibraryPath { get; set; }
        public string LocalMirrorPath { get; set; }
        public List<string> ProtectedLocalCategories { get; private set; }
        public bool PreferLocalWhenNasUnavailable { get; set; }
        public bool AllowNasSync { get; set; }
        public LibraryMode CurrentLibraryMode { get; set; }
        public string SyncUserName { get; set; }
        public int ThumbSize { get; set; }
        public double InsertScale { get; set; }
        public double InsertRotation { get; set; }
        public int FormWidth { get; set; }
        public int FormHeight { get; set; }
        public List<string> RecentBlocks { get; private set; }

        public static BlockBrowserConfig CreateDefault(string pluginRoot)
        {
            string libraryPath = Path.Combine(pluginRoot ?? "", "我的常用块");
            var config = new BlockBrowserConfig
            {
                LibraryPath = libraryPath,
                NasLibraryPath = libraryPath,
                LocalMirrorPath = libraryPath,
                PreferLocalWhenNasUnavailable = true,
                AllowNasSync = false,
                CurrentLibraryMode = LibraryMode.Local,
                SyncUserName = Environment.UserName,
                ThumbSize = 128,
                InsertScale = 1.0,
                InsertRotation = 0,
                FormWidth = 1000,
                FormHeight = 650
            };
            config.ProtectedLocalCategories.Add("个人块");
            return config;
        }

        public BlockBrowserConfig Clone()
        {
            var clone = new BlockBrowserConfig
            {
                LibraryPath = LibraryPath,
                NasLibraryPath = NasLibraryPath,
                LocalMirrorPath = LocalMirrorPath,
                PreferLocalWhenNasUnavailable = PreferLocalWhenNasUnavailable,
                AllowNasSync = AllowNasSync,
                CurrentLibraryMode = CurrentLibraryMode,
                SyncUserName = SyncUserName,
                ThumbSize = ThumbSize,
                InsertScale = InsertScale,
                InsertRotation = InsertRotation,
                FormWidth = FormWidth,
                FormHeight = FormHeight
            };

            clone.RecentBlocks.AddRange(RecentBlocks);
            clone.ProtectedLocalCategories.AddRange(ProtectedLocalCategories);
            return clone;
        }
    }
}
