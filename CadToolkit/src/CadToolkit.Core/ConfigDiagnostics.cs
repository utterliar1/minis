using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CadToolkit.Core
{
    public enum ConfigDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public class ConfigDiagnosticIssue
    {
        public ConfigDiagnosticSeverity Severity;
        public string Code;
        public string Message;
        public int LineNumber;
        public string Section;
        public bool CanFix;
    }

    public class ConfigDiagnosticResult
    {
        public string Path;
        public List<ConfigDiagnosticIssue> Issues = new List<ConfigDiagnosticIssue>();
        public string RepairedText;
        public bool HasChanges;
        public string BackupPath;

        public bool HasErrors
        {
            get
            {
                foreach (var issue in Issues)
                    if (issue.Severity == ConfigDiagnosticSeverity.Error) return true;
                return false;
            }
        }
    }

    public static class ConfigDiagnostics
    {
        public static ConfigDiagnosticResult Analyze(string text, string path)
        {
            return new ConfigDiagnosticResult { Path = path, RepairedText = text ?? "" };
        }

        public static ConfigDiagnosticResult Repair(string text, string path)
        {
            return Analyze(text, path);
        }

        public static ConfigDiagnosticResult AnalyzeFile(string path)
        {
            string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
            return Analyze(text, path);
        }

        public static ConfigDiagnosticResult RepairFile(string path)
        {
            string text = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
            var result = Repair(text, path);
            return result;
        }
    }
}
