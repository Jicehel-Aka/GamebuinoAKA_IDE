using System;
using System.Collections.Generic;
using System.IO;

namespace GamebuinoAKA.IDE.Models
{
    public class GamebuinoProject
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;
        public string Template { get; set; } = "empty";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public List<string> RecentFiles { get; set; } = new List<string>();

        // Derived
        public string PlatformIniPath => Path.Combine(FolderPath, "platformio.ini");
        public string SrcPath => Path.Combine(FolderPath, "src");
        public bool IsValid => !string.IsNullOrEmpty(FolderPath)
                               && File.Exists(PlatformIniPath);
    }
}
