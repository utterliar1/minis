using System.IO;

namespace BlockBrowser
{
    public class CategoryCreationResult
    {
        public bool IsValid { get; set; }
        public bool Created { get; set; }
        public string Category { get; set; }
        public string CategoryDirectory { get; set; }
        public string Message { get; set; }
    }

    public static class CategoryCreationService
    {
        public static CategoryCreationResult CreateCategory(string libraryPath, string category)
        {
            var result = new CategoryCreationResult
            {
                Category = (category ?? "").Trim(),
                CategoryDirectory = "",
                Message = ""
            };

            if (string.IsNullOrEmpty(libraryPath))
            {
                result.Message = "Library path is empty.";
                return result;
            }

            if (!LibraryNameRules.IsSafeLibraryName(result.Category))
            {
                result.Message = "Category contains invalid characters.";
                return result;
            }

            Directory.CreateDirectory(libraryPath);
            result.CategoryDirectory = Path.Combine(libraryPath, result.Category);
            result.Created = !Directory.Exists(result.CategoryDirectory);
            Directory.CreateDirectory(result.CategoryDirectory);
            result.IsValid = true;
            return result;
        }
    }
}
