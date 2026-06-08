using System.IO;

namespace BlockBrowser
{
    public class BlockWritePlan
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public string BlockName { get; set; }
        public string Category { get; set; }
        public string CategoryDirectory { get; set; }
        public string OutputPath { get; set; }
        public bool Exists { get; set; }
    }

    public static class BlockWriteService
    {
        public static BlockWritePlan PrepareSaveTarget(string libraryPath, string blockName, string category)
        {
            var plan = new BlockWritePlan
            {
                Message = "",
                BlockName = (blockName ?? "").Trim(),
                Category = (category ?? "").Trim(),
                CategoryDirectory = "",
                OutputPath = ""
            };

            if (string.IsNullOrEmpty(libraryPath))
            {
                plan.Message = "Library path is empty.";
                return plan;
            }

            if (!LibraryNameRules.IsSafeLibraryName(plan.BlockName)
                || !LibraryNameRules.IsSafeLibraryName(plan.Category))
            {
                plan.Message = "Name or category contains invalid characters.";
                return plan;
            }

            plan.CategoryDirectory = Path.Combine(libraryPath, plan.Category);
            Directory.CreateDirectory(plan.CategoryDirectory);
            plan.OutputPath = Path.Combine(plan.CategoryDirectory, plan.BlockName + ".dwg");
            plan.Exists = File.Exists(plan.OutputPath);
            plan.IsValid = true;
            return plan;
        }

        public static BlockInfo CreateSavedBlockInfo(string outputPath, string category)
        {
            return new BlockInfo
            {
                FilePath = outputPath,
                Category = category
            };
        }
    }
}
