using System.IO;

namespace BlockBrowser
{
    public class BlockInfo
    {
        private string _name;
        private string _filePath;

        public string FilePath
        {
            get { return _filePath; }
            set
            {
                _filePath = value;
                _name = Path.GetFileNameWithoutExtension(value);
            }
        }

        public string Name { get { return _name ?? ""; } }
        public string Category { get; set; }
    }
}
