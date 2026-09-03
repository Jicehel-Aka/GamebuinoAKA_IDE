using System;
using System.Diagnostics;
using System.IO;
using GamebuinoAKA.IDE.Models;

namespace GamebuinoAKA.IDE.Services
{
    public class VSCodeService
    {
        private readonly SettingsService _settingsService;

        public VSCodeService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public string DetectVSCodePath()
        {
            var configured = _settingsService.Settings.VSCodePath;
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
                return configured;

            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Microsoft VS Code", "Code.exe"),
                "code"
            };

            foreach (var c in candidates)
                if (c == "code" || File.Exists(c)) return c;

            return string.Empty;
        }

        public bool IsInstalled() => !string.IsNullOrEmpty(DetectVSCodePath());

        public void OpenProject(GamebuinoProject project)
        {
            var vscodePath = _settingsService.Settings.VSCodePath;
            if (string.IsNullOrEmpty(vscodePath))
                vscodePath = DetectVSCodePath();

            if (string.IsNullOrEmpty(vscodePath))
                throw new InvalidOperationException("VS Code introuvable. Vérifiez les paramètres.");

            Process.Start(new ProcessStartInfo
            {
                FileName = vscodePath,
                Arguments = $"\"{project.FolderPath}\"",
                UseShellExecute = false
            });
        }

        public void OpenFolder(string folderPath)
        {
            var vscodePath = DetectVSCodePath();
            if (string.IsNullOrEmpty(vscodePath))
                throw new InvalidOperationException("VS Code introuvable.");

            Process.Start(new ProcessStartInfo
            {
                FileName = vscodePath,
                Arguments = $"\"{folderPath}\"",
                UseShellExecute = false
            });
        }

        public string GetDisplayPath()
        {
            var p = DetectVSCodePath();
            if (string.IsNullOrEmpty(p)) return "Non détecté";
            if (p == "code") return "code (PATH)";
            return Path.GetFileName(p);
        }
    }
}
