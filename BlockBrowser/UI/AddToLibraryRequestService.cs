using System;

namespace BlockBrowser
{
    public enum AddToLibraryRequestAction
    {
        Cancel,
        InvalidName,
        StartCommand
    }

    public sealed class AddToLibraryRequestPlan
    {
        public AddToLibraryRequestAction Action { get; set; }
        public string Category { get; set; }
        public string BlockName { get; set; }
        public string PendingCommand { get; set; }
    }

    public static class AddToLibraryRequestService
    {
        public static AddToLibraryRequestPlan CreatePlan(
            string category,
            string blockName,
            Func<string, bool> isSafeLibraryName)
        {
            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(blockName))
            {
                return new AddToLibraryRequestPlan { Action = AddToLibraryRequestAction.Cancel };
            }

            string trimmedCategory = category.Trim();
            string trimmedBlockName = blockName.Trim();
            if (isSafeLibraryName == null || !isSafeLibraryName(trimmedCategory) || !isSafeLibraryName(trimmedBlockName))
            {
                return new AddToLibraryRequestPlan
                {
                    Action = AddToLibraryRequestAction.InvalidName,
                    Category = trimmedCategory,
                    BlockName = trimmedBlockName
                };
            }

            return new AddToLibraryRequestPlan
            {
                Action = AddToLibraryRequestAction.StartCommand,
                Category = trimmedCategory,
                BlockName = trimmedBlockName,
                PendingCommand = "BBADD"
            };
        }
    }
}
