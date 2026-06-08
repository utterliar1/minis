using System;

namespace BlockBrowser
{
    public static class ActiveLibraryResolver
    {
        public static ActiveLibraryResult Resolve(SyncSettings settings, bool nasAvailable, bool localMirrorAvailable)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            if (settings.CurrentLibraryMode == LibraryMode.Nas)
            {
                return nasAvailable
                    ? Available(ActiveLibraryKind.Nas, settings.LibraryPath, "Using NAS library.")
                    : Unavailable(ActiveLibraryKind.Nas, settings.LibraryPath, "NAS library is unavailable.");
            }

            if (settings.CurrentLibraryMode == LibraryMode.Local)
            {
                return localMirrorAvailable
                    ? Available(ActiveLibraryKind.LocalMirror, settings.LocalMirrorPath, "Using local mirror.")
                    : Unavailable(ActiveLibraryKind.LocalMirror, settings.LocalMirrorPath, "Local mirror is unavailable.");
            }

            if (nasAvailable)
                return Available(ActiveLibraryKind.Nas, settings.LibraryPath, "Using NAS library.");

            if (settings.PreferLocalWhenNasUnavailable && localMirrorAvailable)
                return Available(ActiveLibraryKind.LocalMirror, settings.LocalMirrorPath, "NAS unavailable, using local mirror.");

            return Unavailable(ActiveLibraryKind.None, "", "No available library path.");
        }

        private static ActiveLibraryResult Available(ActiveLibraryKind kind, string path, string message)
        {
            return new ActiveLibraryResult
            {
                Kind = kind,
                ActivePath = path ?? "",
                Message = message,
                IsAvailable = true
            };
        }

        private static ActiveLibraryResult Unavailable(ActiveLibraryKind kind, string path, string message)
        {
            return new ActiveLibraryResult
            {
                Kind = kind,
                ActivePath = path ?? "",
                Message = message,
                IsAvailable = false
            };
        }
    }
}
