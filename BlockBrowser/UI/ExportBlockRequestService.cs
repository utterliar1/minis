using System;
using System.Collections.Generic;

namespace BlockBrowser
{
    public enum ExportBlockRequestAction
    {
        Cancel,
        InvalidCategory,
        Export
    }

    public sealed class ExportBlockRequestPlan
    {
        public ExportBlockRequestPlan()
        {
            SelectedBlocks = new List<string>();
        }

        public ExportBlockRequestAction Action { get; set; }
        public List<string> SelectedBlocks { get; private set; }
        public string Category { get; set; }
    }

    public static class ExportBlockRequestService
    {
        public static ExportBlockRequestPlan CreatePlan(
            IEnumerable<string> selectedBlocks,
            string category,
            Func<string, bool> isSafeLibraryName)
        {
            var plan = new ExportBlockRequestPlan();
            if (selectedBlocks != null)
            {
                foreach (string blockName in selectedBlocks)
                {
                    if (!string.IsNullOrEmpty(blockName))
                    {
                        plan.SelectedBlocks.Add(blockName);
                    }
                }
            }

            string trimmedCategory = (category ?? "").Trim();
            plan.Category = trimmedCategory;
            if (plan.SelectedBlocks.Count == 0 || string.IsNullOrEmpty(trimmedCategory))
            {
                plan.Action = ExportBlockRequestAction.Cancel;
                return plan;
            }

            if (isSafeLibraryName == null || !isSafeLibraryName(trimmedCategory))
            {
                plan.Action = ExportBlockRequestAction.InvalidCategory;
                return plan;
            }

            plan.Action = ExportBlockRequestAction.Export;
            return plan;
        }

        public static string FormatCompletion(int successCount, int failedCount)
        {
            return string.Format("导出完成: {0} 成功, {1} 失败", successCount, failedCount);
        }
    }
}
