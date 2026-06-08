using System;
using System.IO;

namespace BlockBrowser
{
    public static class VersionNameGenerator
    {
        public static string CreateVersionCopyName(string relativePath, string user, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentException("Relative path is required.", "relativePath");

            string dir = Path.GetDirectoryName(relativePath);
            string name = Path.GetFileNameWithoutExtension(relativePath);
            string ext = Path.GetExtension(relativePath);
            string safeUser = string.IsNullOrEmpty(user) ? "user" : user.Trim();

            foreach (char c in Path.GetInvalidFileNameChars())
                safeUser = safeUser.Replace(c, '_');

            string fileName = string.Format("{0}_{1}_{2}{3}", name, safeUser, utcNow.ToString("yyyyMMdd"), ext);
            return string.IsNullOrEmpty(dir) ? fileName : Path.Combine(dir, fileName);
        }
    }
}
