using System;

namespace BlockBrowser
{
    public class SettingsUpdatePlan
    {
        public bool IsValid { get; set; }
        public bool RequiresDirectoryCreation { get; set; }
        public bool LibraryPathChanged { get; set; }
        public string LibraryPath { get; set; }
        public double InsertScale { get; set; }
        public double InsertRotationRadians { get; set; }
    }

    public static class SettingsUpdateService
    {
        public static SettingsUpdatePlan CreatePlan(
            string currentLibraryPath,
            string requestedLibraryPath,
            double insertScale,
            double insertRotationDegrees,
            Func<string, bool> directoryExists)
        {
            string path = (requestedLibraryPath ?? "").Trim();
            if (string.IsNullOrEmpty(path))
            {
                return new SettingsUpdatePlan
                {
                    IsValid = false,
                    LibraryPath = path,
                    InsertScale = insertScale,
                    InsertRotationRadians = insertRotationDegrees * Math.PI / 180.0
                };
            }

            bool exists = directoryExists == null || directoryExists(path);
            return new SettingsUpdatePlan
            {
                IsValid = true,
                RequiresDirectoryCreation = !exists,
                LibraryPathChanged = path != currentLibraryPath,
                LibraryPath = path,
                InsertScale = insertScale,
                InsertRotationRadians = insertRotationDegrees * Math.PI / 180.0
            };
        }
    }
}
