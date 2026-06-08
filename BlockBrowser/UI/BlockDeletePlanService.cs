using System;

namespace BlockBrowser
{
    public enum BlockDeleteAction
    {
        NoSelection,
        MissingFile,
        RecordLocalDeleteRequest,
        FileLocked,
        DeleteFile
    }

    public sealed class BlockDeletePlan
    {
        public BlockDeleteAction Action { get; set; }
        public string FilePath { get; set; }
        public string BlockName { get; set; }
    }

    public static class BlockDeletePlanService
    {
        public static BlockDeletePlan CreatePlan(
            BlockInfo block,
            ActiveLibraryResult activeLibrary,
            Func<string, bool> fileExists,
            Func<string, bool> canOpenForExclusiveWrite)
        {
            if (block == null)
            {
                return new BlockDeletePlan { Action = BlockDeleteAction.NoSelection };
            }

            string filePath = block.FilePath;
            string blockName = block.Name;
            if (string.IsNullOrEmpty(filePath) || (fileExists != null && !fileExists(filePath)))
            {
                return new BlockDeletePlan
                {
                    Action = BlockDeleteAction.MissingFile,
                    FilePath = filePath,
                    BlockName = blockName
                };
            }

            if (activeLibrary != null && activeLibrary.Kind == ActiveLibraryKind.LocalMirror)
            {
                return new BlockDeletePlan
                {
                    Action = BlockDeleteAction.RecordLocalDeleteRequest,
                    FilePath = filePath,
                    BlockName = blockName
                };
            }

            if (canOpenForExclusiveWrite != null && !canOpenForExclusiveWrite(filePath))
            {
                return new BlockDeletePlan
                {
                    Action = BlockDeleteAction.FileLocked,
                    FilePath = filePath,
                    BlockName = blockName
                };
            }

            return new BlockDeletePlan
            {
                Action = BlockDeleteAction.DeleteFile,
                FilePath = filePath,
                BlockName = blockName
            };
        }
    }
}
