using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GamebuinoAKA.IDE.Models;

namespace GamebuinoAKA.IDE.Services
{
    public class ProjectService
    {
        private readonly SettingsService _settingsService;

        public ProjectService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>Scans the workspace folder for Gamebuino projects (folders with platformio.ini).</summary>
        public async Task<List<GamebuinoProject>> ScanWorkspaceAsync()
        {
            var workspace = _settingsService.Settings.WorkspaceFolder;
            if (string.IsNullOrEmpty(workspace) || !Directory.Exists(workspace))
                return new List<GamebuinoProject>();

            return await Task.Run(() =>
            {
                var projects = new List<GamebuinoProject>();
                foreach (var dir in Directory.GetDirectories(workspace))
                {
                    var iniPath = Path.Combine(dir, "platformio.ini");
                    if (!File.Exists(iniPath)) continue;

                    var info = new DirectoryInfo(dir);
                    var project = new GamebuinoProject
                    {
                        Name = info.Name,
                        FolderPath = dir,
                        LastModified = info.LastWriteTime,
                        CreatedAt = info.CreationTime,
                        Template = DetectTemplate(dir)
                    };
                    projects.Add(project);
                }
                return projects.OrderByDescending(p => p.LastModified).ToList();
            });
        }

        public Task<List<GamebuinoProject>> GetRecentProjectsAsync()
        {
            return Task.Run(() =>
            {
                var recent = new List<GamebuinoProject>();
                foreach (var path in _settingsService.Settings.RecentProjects)
                {
                    if (!Directory.Exists(path)) continue;
                    var info = new DirectoryInfo(path);
                    recent.Add(new GamebuinoProject
                    {
                        Name = info.Name,
                        FolderPath = path,
                        LastModified = info.LastWriteTime,
                        CreatedAt = info.CreationTime,
                        Template = DetectTemplate(path)
                    });
                }
                return recent;
            });
        }

        public void DeleteProject(GamebuinoProject project)
        {
            if (Directory.Exists(project.FolderPath))
                Directory.Delete(project.FolderPath, recursive: true);
            _settingsService.Settings.RecentProjects.Remove(project.FolderPath);
            _settingsService.Save();
        }

        private static string DetectTemplate(string folder)
        {
            var mainCpp = Path.Combine(folder, "src", "main.cpp");
            if (!File.Exists(mainCpp)) return "empty";
            var content = File.ReadAllText(mainCpp);
            if (content.Contains("game.h")) return "game-template";
            if (content.Contains("Hello")) return "hello-world";
            return "empty";
        }
    }
}
