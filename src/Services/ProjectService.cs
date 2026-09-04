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

        /// <summary>
        /// Scanne le workspace : un dossier est un projet s'il contient un
        /// platformio.ini (PlatformIO) OU un CMakeLists.txt racine (ESP-IDF).
        /// </summary>
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
                    bool isPio = File.Exists(Path.Combine(dir, "platformio.ini"));
                    bool isIdf = File.Exists(Path.Combine(dir, "CMakeLists.txt"));
                    if (!isPio && !isIdf) continue;

                    var info = new DirectoryInfo(dir);
                    projects.Add(new GamebuinoProject
                    {
                        Name = info.Name,
                        FolderPath = dir,
                        LastModified = info.LastWriteTime,
                        CreatedAt = info.CreationTime,
                        BuildSystem = GamebuinoProject.DetectBuildSystem(dir, _settingsService.Settings.DefaultBuildSystem),
                        Template = DetectTemplate(dir)
                    });
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
                        BuildSystem = GamebuinoProject.DetectBuildSystem(path, _settingsService.Settings.DefaultBuildSystem),
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
            // ESP-IDF : app_main.cpp
            var appMain = Path.Combine(folder, "main", "app_main.cpp");
            if (File.Exists(appMain)) return "esp-idf";

            var mainCpp = Path.Combine(folder, "src", "main.cpp");
            if (!File.Exists(mainCpp)) return "empty";
            var content = File.ReadAllText(mainCpp);
            if (content.Contains("game.h")) return "game-template";
            if (content.Contains("Hello")) return "hello-world";
            return "empty";
        }
    }
}
