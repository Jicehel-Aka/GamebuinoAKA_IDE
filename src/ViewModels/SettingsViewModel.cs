using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Models;
using GamebuinoAKA.IDE.Services;

namespace GamebuinoAKA.IDE.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly PlatformIOService _platformIO;
        private readonly VSCodeService _vscode;

        private string _workspaceFolder = string.Empty;
        public string WorkspaceFolder
        {
            get => _workspaceFolder;
            set => SetProperty(ref _workspaceFolder, value);
        }

        private string _platformIOPath = string.Empty;
        public string PlatformIOPath
        {
            get => _platformIOPath;
            set => SetProperty(ref _platformIOPath, value);
        }

        private string _vSCodePath = string.Empty;
        public string VSCodePath
        {
            get => _vSCodePath;
            set => SetProperty(ref _vSCodePath, value);
        }

        private string _gamebuinoLibRepoUrl = string.Empty;
        public string GamebuinoLibRepoUrl
        {
            get => _gamebuinoLibRepoUrl;
            set => SetProperty(ref _gamebuinoLibRepoUrl, value);
        }

        private string _theme = "Dark";
        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _detectedPioVersion = string.Empty;
        public string DetectedPioVersion
        {
            get => _detectedPioVersion;
            set => SetProperty(ref _detectedPioVersion, value);
        }

        public string[] Themes { get; } = new[] { "Dark", "Light" };

        public ICommand SaveCommand { get; }
        public ICommand AutoDetectCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand BrowseWorkspaceFolderCommand { get; }
        public ICommand BrowsePlatformIOCommand { get; }
        public ICommand BrowseVSCodeCommand { get; }

        public SettingsViewModel(SettingsService settingsService,
            PlatformIOService platformIO, VSCodeService vscode)
        {
            _settingsService = settingsService;
            _platformIO = platformIO;
            _vscode = vscode;

            SaveCommand = new RelayCommand(Save);
            AutoDetectCommand = new AsyncRelayCommand(AutoDetectAsync);
            ResetCommand = new RelayCommand(Reset);
            BrowseWorkspaceFolderCommand = new RelayCommand(BrowseWorkspaceFolder);
            BrowsePlatformIOCommand = new RelayCommand(BrowsePlatformIO);
            BrowseVSCodeCommand = new RelayCommand(BrowseVSCode);

            LoadFromSettings();
            _ = DetectToolsAsync();
        }

        private void LoadFromSettings()
        {
            var s = _settingsService.Settings;
            WorkspaceFolder = s.WorkspaceFolder;
            PlatformIOPath = s.PlatformIOPath;
            VSCodePath = s.VSCodePath;
            GamebuinoLibRepoUrl = s.GamebuinoLibRepoUrl;
            Theme = s.Theme;
        }

        private async Task DetectToolsAsync()
        {
            DetectedPioVersion = await _platformIO.GetVersionAsync();
            if (string.IsNullOrEmpty(VSCodePath))
                VSCodePath = _vscode.DetectVSCodePath();
        }

        private void BrowseWorkspaceFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Dossier workspace des projets",
                SelectedPath = WorkspaceFolder,
                UseDescriptionForTitle = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                WorkspaceFolder = dlg.SelectedPath;
        }

        private void BrowsePlatformIO()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chemin vers pio.exe",
                Filter = "Exécutables|*.exe|Tous|*.*"
            };
            if (dlg.ShowDialog() == true)
                PlatformIOPath = dlg.FileName;
        }

        private void BrowseVSCode()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chemin vers Code.exe",
                Filter = "Exécutables|*.exe|Tous|*.*"
            };
            if (dlg.ShowDialog() == true)
                VSCodePath = dlg.FileName;
        }

        private async Task AutoDetectAsync()
        {
            StatusMessage = "Détection en cours...";
            // Detect concrete pio.exe path and show it in the field
            PlatformIOPath = _platformIO.DetectPioPath();
            VSCodePath = _vscode.DetectVSCodePath();
            DetectedPioVersion = await _platformIO.GetVersionAsync();

            var pioStatus = DetectedPioVersion != "Non détecté"
                ? $"PlatformIO {DetectedPioVersion}"
                : "PlatformIO non trouvé";
            var vsStatus = string.IsNullOrEmpty(VSCodePath) ? "VS Code non trouvé" : VSCodePath;
            StatusMessage = $"{pioStatus}  |  {vsStatus}";
        }

        private void Save()
        {
            var s = _settingsService.Settings;
            s.WorkspaceFolder = WorkspaceFolder;
            s.PlatformIOPath = PlatformIOPath;
            s.VSCodePath = VSCodePath;
            s.GamebuinoLibRepoUrl = GamebuinoLibRepoUrl;
            s.Theme = Theme;
            _settingsService.Save();
            StatusMessage = "Paramètres sauvegardés.";
        }

        private void Reset()
        {
            var defaults = new AppSettings();
            WorkspaceFolder = defaults.WorkspaceFolder;
            PlatformIOPath = defaults.PlatformIOPath;
            VSCodePath = defaults.VSCodePath;
            GamebuinoLibRepoUrl = defaults.GamebuinoLibRepoUrl;
            Theme = defaults.Theme;
            StatusMessage = "Valeurs réinitialisées (non sauvegardées).";
        }
    }
}
