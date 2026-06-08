using System.IO;

namespace BlockBrowser
{
    public static class LibraryNameRules
    {
        public static bool IsSafeLibraryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string trimmed = name.Trim();
            if (trimmed == "." || trimmed == "..") return false;

            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in trimmed)
            {
                foreach (char invalidChar in invalid)
                {
                    if (c == invalidChar) return false;
                }
            }

            return true;
        }
    }
}
