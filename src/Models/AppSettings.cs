using System;
using System.Collections.Generic;

namespace GamebuinoAKA.IDE.Models
{
    public class AppSettings
    {
        public string WorkspaceFolder { get; set; } = string.Empty;
        public string PlatformIOPath { get; set; } = string.Empty;
        public string VSCodePath { get; set; } = string.Empty;
        public string GamebuinoLibRepoUrl { get; set; } = "https://github.com/jmp42/Gamebuino_AKA_lib";
        public string Theme { get; set; } = "Dark";
        public List<string> RecentProjects { get; set; } = new List<string>();
        public int MaxRecentProjects { get; set; } = 10;
        public bool AutoDetectTools { get; set; } = true;

        public static string SettingsFilePath =>
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GamebuinoAKA",
                "settings.json");
    }
}
