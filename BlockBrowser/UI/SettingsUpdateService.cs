using System;

namespace BlockBrowser
{
    public class SettingsUpdatePlan
    {
        public bool IsValid { get; set; }
        public bool RequiresLocalMirrorDirectoryCreation { get; set; }
        public bool NasLibraryPathChanged { get; set; }
        public bool LocalMirrorPathChanged { get; set; }
        public bool ProtectedLocalCategoriesChanged { get; set; }
        public bool CurrentLibraryModeChanged { get; set; }
        public string NasLibraryPath { get; set; }
        public string LocalMirrorPath { get; set; }
        public string ProtectedLocalCategories { get; set; }
        public LibraryMode CurrentLibraryMode { get; set; }
        public double InsertScale { get; set; }
        public double InsertRotationRadians { get; set; }
    }

    public static class SettingsUpdateService
    {
        public static SettingsUpdatePlan CreatePlan(
            string currentNasLibraryPath,
            string requestedNasLibraryPath,
            string currentLocalMirrorPath,
            string requestedLocalMirrorPath,
            string currentProtectedLocalCategories,
            string requestedProtectedLocalCategories,
            LibraryMode currentLibraryMode,
            LibraryMode requestedLibraryMode,
            double insertScale,
            double insertRotationDegrees,
            Func<string, bool> directoryExists)
        {
            string nasPath = (requestedNasLibraryPath ?? "").Trim();
            string localPath = (requestedLocalMirrorPath ?? "").Trim();
            string protectedCategories = (requestedProtectedLocalCategories ?? "").Trim();
            if (string.IsNullOrEmpty(nasPath) || string.IsNullOrEmpty(localPath))
            {
                return new SettingsUpdatePlan
                {
                    IsValid = false,
                    NasLibraryPath = nasPath,
                    LocalMirrorPath = localPath,
                    ProtectedLocalCategories = protectedCategories,
                    CurrentLibraryMode = requestedLibraryMode,
                    InsertScale = insertScale,
                    InsertRotationRadians = insertRotationDegrees * Math.PI / 180.0
                };
            }

            bool localExists = directoryExists == null || directoryExists(localPath);
            return new SettingsUpdatePlan
            {
                IsValid = true,
                RequiresLocalMirrorDirectoryCreation = !localExists,
                NasLibraryPathChanged = nasPath != currentNasLibraryPath,
                LocalMirrorPathChanged = localPath != currentLocalMirrorPath,
                ProtectedLocalCategoriesChanged = protectedCategories != (currentProtectedLocalCategories ?? "").Trim(),
                CurrentLibraryModeChanged = requestedLibraryMode != currentLibraryMode,
                NasLibraryPath = nasPath,
                LocalMirrorPath = localPath,
                ProtectedLocalCategories = protectedCategories,
                CurrentLibraryMode = requestedLibraryMode,
                InsertScale = insertScale,
                InsertRotationRadians = insertRotationDegrees * Math.PI / 180.0
            };
        }
    }
}
