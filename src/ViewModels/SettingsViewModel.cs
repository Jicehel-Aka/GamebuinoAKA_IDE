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
        private readonly EspIdfService _espIdf;

        // ── Existant ─────────────────────────────────────────────────────────────
        private string _workspaceFolder = string.Empty;
        public string WorkspaceFolder { get => _workspaceFolder; set => SetProperty(ref _workspaceFolder, value); }

        private string _platformIOPath = string.Empty;
        public string PlatformIOPath { get => _platformIOPath; set => SetProperty(ref _platformIOPath, value); }

        private string _vSCodePath = string.Empty;
        public string VSCodePath { get => _vSCodePath; set => SetProperty(ref _vSCodePath, value); }

        private string _gamebuinoLibRepoUrl = string.Empty;
        public string GamebuinoLibRepoUrl { get => _gamebuinoLibRepoUrl; set => SetProperty(ref _gamebuinoLibRepoUrl, value); }

        private string _theme = "Dark";
        public string Theme { get => _theme; set => SetProperty(ref _theme, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private string _detectedPioVersion = string.Empty;
        public string DetectedPioVersion { get => _detectedPioVersion; set => SetProperty(ref _detectedPioVersion, value); }

        public string[] Themes { get; } = new[] { "Dark", "Light" };

        // ── Nouveau : ESP-IDF ────────────────────────────────────────────────────
        private string _idfPyPath = string.Empty;
        public string IdfPyPath { get => _idfPyPath; set => SetProperty(ref _idfPyPath, value); }

        private string _idfExportScript = string.Empty;
        public string IdfExportScript { get => _idfExportScript; set => SetProperty(ref _idfExportScript, value); }

        private string _idfSerialPort = string.Empty;
        public string IdfSerialPort { get => _idfSerialPort; set => SetProperty(ref _idfSerialPort, value); }

        private string _referenceGamebuinoComponentPath = string.Empty;
        public string ReferenceGamebuinoComponentPath
        {
            get => _referenceGamebuinoComponentPath;
            set => SetProperty(ref _referenceGamebuinoComponentPath, value);
        }

        // ── Nouveau : chaîne de build par défaut ─────────────────────────────────
        private BuildSystem _defaultBuildSystem = BuildSystem.EspIdf;
        public BuildSystem DefaultBuildSystem
        {
            get => _defaultBuildSystem;
            set
            {
                if (SetProperty(ref _defaultBuildSystem, value))
                {
                    OnPropertyChanged(nameof(IsDefaultPlatformIO));
                    OnPropertyChanged(nameof(IsDefaultEspIdf));
                }
            }
        }
        public bool IsDefaultPlatformIO
        {
            get => DefaultBuildSystem == BuildSystem.PlatformIO;
            set { if (value) DefaultBuildSystem = BuildSystem.PlatformIO; }
        }
        public bool IsDefaultEspIdf
        {
            get => DefaultBuildSystem == BuildSystem.EspIdf;
            set { if (value) DefaultBuildSystem = BuildSystem.EspIdf; }
        }

        // ── Nouveau : format couleur par défaut ──────────────────────────────────
        private ColorFormat _defaultColorFormat = ColorFormat.Bgr565Aka;
        public ColorFormat DefaultColorFormat
        {
            get => _defaultColorFormat;
            set
            {
                if (SetProperty(ref _defaultColorFormat, value))
                {
                    OnPropertyChanged(nameof(IsColorBgrAka));
                    OnPropertyChanged(nameof(IsColorRgbStd));
                }
            }
        }
        public bool IsColorBgrAka
        {
            get => DefaultColorFormat == ColorFormat.Bgr565Aka;
            set { if (value) DefaultColorFormat = ColorFormat.Bgr565Aka; }
        }
        public bool IsColorRgbStd
        {
            get => DefaultColorFormat == ColorFormat.Rgb565Std;
            set { if (value) DefaultColorFormat = ColorFormat.Rgb565Std; }
        }

        private string _defaultTransparentKeyHex = "0xF81F";
        public string DefaultTransparentKeyHex
        {
            get => _defaultTransparentKeyHex;
            set => SetProperty(ref _defaultTransparentKeyHex, value);
        }

        // ── Commandes ─────────────────────────────────────────────────────────────
        public ICommand SaveCommand { get; }
        public ICommand AutoDetectCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand BrowseWorkspaceFolderCommand { get; }
        public ICommand BrowsePlatformIOCommand { get; }
        public ICommand BrowseVSCodeCommand { get; }
        public ICommand BrowseIdfPyCommand { get; }
        public ICommand BrowseIdfExportScriptCommand { get; }
        public ICommand BrowseReferenceComponentCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand DeleteLogCommand { get; }

        /// <summary>Chemin du fichier journal, pour affichage.</summary>
        public string LogFilePath => Services.Log.LogFilePath;

        public SettingsViewModel(SettingsService settingsService,
            PlatformIOService platformIO, VSCodeService vscode, EspIdfService espIdf)
        {
            _settingsService = settingsService;
            _platformIO = platformIO;
            _vscode = vscode;
            _espIdf = espIdf;

            SaveCommand = new RelayCommand(Save);
            AutoDetectCommand = new AsyncRelayCommand(AutoDetectAsync);
            ResetCommand = new RelayCommand(Reset);
            BrowseWorkspaceFolderCommand = new RelayCommand(BrowseWorkspaceFolder);
            BrowsePlatformIOCommand = new RelayCommand(BrowsePlatformIO);
            BrowseVSCodeCommand = new RelayCommand(BrowseVSCode);
            BrowseIdfPyCommand = new RelayCommand(BrowseIdfPy);
            BrowseIdfExportScriptCommand = new RelayCommand(BrowseIdfExportScript);
            BrowseReferenceComponentCommand = new RelayCommand(BrowseReferenceComponent);
            OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
            DeleteLogCommand = new RelayCommand(DeleteLog);

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

            IdfPyPath = s.IdfPyPath;
            IdfExportScript = s.IdfExportScript;
            IdfSerialPort = s.IdfSerialPort;
            ReferenceGamebuinoComponentPath = s.ReferenceGamebuinoComponentPath;
            DefaultBuildSystem = s.DefaultBuildSystem;
            DefaultColorFormat = s.DefaultColorFormat;
            DefaultTransparentKeyHex = "0x" + s.DefaultTransparentKey.ToString("X4");
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

        private void BrowsePlatformIO() => BrowseExe("Chemin vers pio.exe", v => PlatformIOPath = v);
        private void BrowseVSCode() => BrowseExe("Chemin vers Code.exe", v => VSCodePath = v);

        private void BrowseIdfPy()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chemin vers idf.py (ou idf.py.exe)",
                Filter = "idf.py|idf.py;idf.py.exe|Tous|*.*"
            };
            if (dlg.ShowDialog() == true) IdfPyPath = dlg.FileName;
        }

        private void BrowseIdfExportScript()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Script d'environnement ESP-IDF (export.bat)",
                Filter = "Scripts|*.bat;*.ps1|Tous|*.*"
            };
            if (dlg.ShowDialog() == true) IdfExportScript = dlg.FileName;
        }

        private void BrowseReferenceComponent()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Dossier components/gamebuino de référence à copier",
                SelectedPath = ReferenceGamebuinoComponentPath,
                UseDescriptionForTitle = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                ReferenceGamebuinoComponentPath = dlg.SelectedPath;
        }

        private void OpenLogFolder()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Services.Log.LogFolder);
                if (System.IO.File.Exists(Services.Log.LogFilePath))
                    System.Diagnostics.Process.Start("explorer.exe",
                        $"/select,\"{Services.Log.LogFilePath}\"");
                else
                    System.Diagnostics.Process.Start("explorer.exe",
                        $"\"{Services.Log.LogFolder}\"");
            }
            catch (System.Exception ex)
            {
                Services.Log.Error("Ouverture du dossier de log impossible.", ex);
                StatusMessage = "Impossible d'ouvrir le dossier du journal.";
            }
        }

        private void DeleteLog()
        {
            var res = System.Windows.MessageBox.Show(
                "Supprimer le fichier journal ?",
                "Journal", System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (res != System.Windows.MessageBoxResult.Yes) return;

            StatusMessage = Services.Log.Clear()
                ? "Journal supprimé."
                : "Le journal n'a pas pu être supprimé (fichier peut-être en cours d'écriture).";
        }

        private static void BrowseExe(string title, System.Action<string> assign)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = "Exécutables|*.exe|Tous|*.*"
            };
            if (dlg.ShowDialog() == true) assign(dlg.FileName);
        }

        private async Task AutoDetectAsync()
        {
            StatusMessage = "Détection en cours...";

            // PlatformIO + VS Code
            PlatformIOPath = _platformIO.DetectPioPath();
            VSCodePath = _vscode.DetectVSCodePath();
            DetectedPioVersion = await _platformIO.GetVersionAsync();

            // ESP-IDF : idf.py / export.bat
            var (export, idfPy) = _espIdf.DetectInstall();
            if (!string.IsNullOrEmpty(export)) IdfExportScript = export;
            if (!string.IsNullOrEmpty(idfPy)) IdfPyPath = idfPy;

            // Port série : rempli seulement si un seul port (sinon on liste).
            var ports = _espIdf.DetectSerialPorts();
            string portStatus;
            if (ports.Length == 1) { IdfSerialPort = ports[0]; portStatus = ports[0]; }
            else if (ports.Length == 0) portStatus = "aucun port COM";
            else portStatus = "plusieurs ports : " + string.Join(", ", ports) + " (à choisir)";

            // Composant gamebuino de référence : lib embarquée en priorité,
            // sinon un projet existant du workspace contenant components/gamebuino.
            string refStatus;
            if (string.IsNullOrWhiteSpace(ReferenceGamebuinoComponentPath))
            {
                var bundled = TemplateService.GetBundledComponentPath();
                var found = bundled ?? ScanWorkspaceForComponent();
                if (found != null) { ReferenceGamebuinoComponentPath = found; refStatus = "lib trouvée"; }
                else refStatus = "lib non trouvée";
            }
            else refStatus = "lib déjà définie";

            var pioStatus = DetectedPioVersion != "Non détecté"
                ? $"PlatformIO {DetectedPioVersion}" : "PlatformIO non trouvé";
            var vsStatus = string.IsNullOrEmpty(VSCodePath) ? "VS Code non trouvé" : "VS Code OK";
            var idfStatus = string.IsNullOrEmpty(IdfExportScript) && string.IsNullOrEmpty(IdfPyPath)
                ? "ESP-IDF non trouvé" : "ESP-IDF OK";

            StatusMessage = $"{pioStatus}  |  {vsStatus}  |  {idfStatus}  |  Port : {portStatus}  |  Composant : {refStatus}";
        }

        /// <summary>Cherche un components/gamebuino dans les projets du workspace.</summary>
        private string? ScanWorkspaceForComponent()
        {
            var ws = _settingsService.Settings.WorkspaceFolder;
            if (string.IsNullOrEmpty(ws) || !System.IO.Directory.Exists(ws)) return null;
            try
            {
                foreach (var proj in System.IO.Directory.GetDirectories(ws))
                {
                    var comp = System.IO.Path.Combine(proj, "components", "gamebuino");
                    if (System.IO.File.Exists(System.IO.Path.Combine(comp, "CMakeLists.txt")))
                        return comp;
                }
            }
            catch { /* ignore */ }
            return null;
        }

        private void Save()
        {
            var s = _settingsService.Settings;
            s.WorkspaceFolder = WorkspaceFolder;
            s.PlatformIOPath = PlatformIOPath;
            s.VSCodePath = VSCodePath;
            s.GamebuinoLibRepoUrl = GamebuinoLibRepoUrl;
            s.Theme = Theme;

            s.IdfPyPath = IdfPyPath;
            s.IdfExportScript = IdfExportScript;
            s.IdfSerialPort = IdfSerialPort;
            s.ReferenceGamebuinoComponentPath = ReferenceGamebuinoComponentPath;
            s.DefaultBuildSystem = DefaultBuildSystem;
            s.DefaultColorFormat = DefaultColorFormat;
            s.DefaultTransparentKey = ParseHex(DefaultTransparentKeyHex, 0xF81F);

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
            IdfPyPath = defaults.IdfPyPath;
            IdfExportScript = defaults.IdfExportScript;
            IdfSerialPort = defaults.IdfSerialPort;
            ReferenceGamebuinoComponentPath = defaults.ReferenceGamebuinoComponentPath;
            DefaultBuildSystem = defaults.DefaultBuildSystem;
            DefaultColorFormat = defaults.DefaultColorFormat;
            DefaultTransparentKeyHex = "0x" + defaults.DefaultTransparentKey.ToString("X4");
            StatusMessage = "Valeurs réinitialisées (non sauvegardées).";
        }

        private static ushort ParseHex(string text, ushort fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            var t = text.Trim();
            if (t.StartsWith("0x") || t.StartsWith("0X")) t = t.Substring(2);
            return ushort.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : fallback;
        }
    }
}
