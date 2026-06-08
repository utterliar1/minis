using System;

namespace BlockBrowser
{
    public enum BlockRenameAction
    {
        NoSelection,
        Cancel,
        InvalidName,
        TargetExists,
        Rename
    }

    public sealed class BlockRenamePlan
    {
        public BlockRenameAction Action { get; set; }
        public string OldName { get; set; }
        public string NewName { get; set; }
        public string OldPath { get; set; }
        public string NewPath { get; set; }
    }

    public static class BlockRenamePlanService
    {
        public static BlockRenamePlan CreatePlan(
            BlockInfo block,
            string requestedName,
            Func<string, bool> fileExists)
        {
            if (block == null)
            {
                return new BlockRenamePlan { Action = BlockRenameAction.NoSelection };
            }

            string oldName = block.Name;
            string newName = (requestedName ?? "").Trim();
            if (string.IsNullOrEmpty(newName) || newName == oldName)
            {
                return new BlockRenamePlan
                {
                    Action = BlockRenameAction.Cancel,
                    OldName = oldName,
                    NewName = newName,
                    OldPath = block.FilePath
                };
            }

            if (!BlockFileOperations.CanRenameBlock(block, newName, false))
            {
                return new BlockRenamePlan
                {
                    Action = BlockRenameAction.InvalidName,
                    OldName = oldName,
                    NewName = newName,
                    OldPath = block.FilePath
                };
            }

            string newPath = BlockFileOperations.GetRenameTargetPath(block, newName);
            if (fileExists != null && fileExists(newPath))
            {
                return new BlockRenamePlan
                {
                    Action = BlockRenameAction.TargetExists,
                    OldName = oldName,
                    NewName = newName,
                    OldPath = block.FilePath,
                    NewPath = newPath
                };
            }

            return new BlockRenamePlan
            {
                Action = BlockRenameAction.Rename,
                OldName = oldName,
                NewName = newName,
                OldPath = block.FilePath,
                NewPath = newPath
            };
        }
    }
}
