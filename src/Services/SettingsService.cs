using System;
using System.IO;
using GamebuinoAKA.IDE.Models;
using Newtonsoft.Json;

namespace GamebuinoAKA.IDE.Services
{
    public class SettingsService
    {
        private AppSettings _settings = new AppSettings();

        public AppSettings Settings => _settings;

        public SettingsService()
        {
            Load();
        }

        public void Load()
        {
            var path = AppSettings.SettingsFilePath;
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    _settings = new AppSettings();
                }
            }
            else
            {
                _settings = new AppSettings();
            }

            // Default workspace
            if (string.IsNullOrEmpty(_settings.WorkspaceFolder))
            {
                _settings.WorkspaceFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "GamebuinoAKA");
            }
        }

        public void Save()
        {
            var path = AppSettings.SettingsFilePath;
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public void AddRecentProject(string folderPath)
        {
            _settings.RecentProjects.Remove(folderPath);
            _settings.RecentProjects.Insert(0, folderPath);
            while (_settings.RecentProjects.Count > _settings.MaxRecentProjects)
                _settings.RecentProjects.RemoveAt(_settings.RecentProjects.Count - 1);
            Save();
        }
    }
}
