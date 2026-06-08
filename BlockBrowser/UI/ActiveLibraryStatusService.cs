namespace BlockBrowser
{
    public static class ActiveLibraryStatusService
    {
        public static string Format(ActiveLibraryResult active)
        {
            if (active != null && active.IsAvailable)
                return active.Kind == ActiveLibraryKind.Nas ? "NAS: " + active.ActivePath : "Local mirror: " + active.ActivePath;

            if (active != null && !string.IsNullOrEmpty(active.Message))
                return active.Message;

            return "就绪";
        }
    }
}
