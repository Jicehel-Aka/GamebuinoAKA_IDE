using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Models;
using GamebuinoAKA.IDE.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.ViewModels
{
    public class ProjectsViewModel : ObservableObject
    {
        private readonly ProjectService _projectService;
        private readonly BuildService _build;
        private readonly VSCodeService _vscode;
        private readonly SettingsService _settings;
        private readonly GitService _git;

        private ObservableCollection<GamebuinoProject> _projects = new ObservableCollection<GamebuinoProject>();
        public ObservableCollection<GamebuinoProject> Projects
        {
            get => _projects;
            set { SetProperty(ref _projects, value); ApplyFilter(); }
        }

        private ObservableCollection<GamebuinoProject> _filteredProjects = new ObservableCollection<GamebuinoProject>();
        public ObservableCollection<GamebuinoProject> FilteredProjects
        {
            get => _filteredProjects;
            set => SetProperty(ref _filteredProjects, value);
        }

        private GamebuinoProject? _selectedProject;
        public GamebuinoProject? SelectedProject
        {
            get => _selectedProject;
            set => SetProperty(ref _selectedProject, value);
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _outputLog = string.Empty;
        public string OutputLog
        {
            get => _outputLog;
            set => SetProperty(ref _outputLog, value);
        }

        private bool _showOutput;
        public bool ShowOutput
        {
            get => _showOutput;
            set => SetProperty(ref _showOutput, value);
        }

        private bool _showCloneDialog;
        public bool ShowCloneDialog
        {
            get => _showCloneDialog;
            set => SetProperty(ref _showCloneDialog, value);
        }

        private string _cloneUrl = string.Empty;
        public string CloneUrl
        {
            get => _cloneUrl;
            set
            {
                SetProperty(ref _cloneUrl, value);
                if (string.IsNullOrWhiteSpace(CloneFolderName))
                    CloneFolderName = GitService.ExtractRepoName(value);
            }
        }

        private string _cloneFolderName = string.Empty;
        public string CloneFolderName
        {
            get => _cloneFolderName;
            set => SetProperty(ref _cloneFolderName, value);
        }

        private CancellationTokenSource? _cts;

        public ICommand RefreshCommand { get; }
        public ICommand OpenInVSCodeCommand { get; }
        public ICommand BuildCommand { get; }
        public ICommand FlashCommand { get; }
        public ICommand MonitorCommand { get; }
        public ICommand CancelOperationCommand { get; }
        public ICommand NewProjectNavigateCommand { get; }
        public ICommand DeleteProjectCommand { get; }
        public ICommand OpenInExplorerCommand { get; }
        public ICommand ShowCloneCommand { get; }
        public ICommand CancelCloneCommand { get; }
        public ICommand CloneFromGitHubCommand { get; }
        public ICommand ToggleBuildSystemCommand { get; }

        public ProjectsViewModel(ProjectService projectService,
            BuildService build, VSCodeService vscode,
            SettingsService settings, GitService git)
        {
            _projectService = projectService;
            _build = build;
            _vscode = vscode;
            _settings = settings;
            _git = git;

            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            OpenInVSCodeCommand = new RelayCommand<GamebuinoProject>(OpenInVSCode);
            BuildCommand = new AsyncRelayCommand<GamebuinoProject>(BuildAsync);
            FlashCommand = new AsyncRelayCommand<GamebuinoProject>(FlashAsync);
            MonitorCommand = new AsyncRelayCommand<GamebuinoProject>(MonitorAsync);
            CancelOperationCommand = new RelayCommand(CancelOperation);
            NewProjectNavigateCommand = new RelayCommand(NewProjectNavigate);
            DeleteProjectCommand = new RelayCommand<GamebuinoProject>(DeleteProject);
            OpenInExplorerCommand = new RelayCommand<GamebuinoProject>(OpenInExplorer);
            ShowCloneCommand = new RelayCommand(ShowClone);
            CancelCloneCommand = new RelayCommand(CancelClone);
            CloneFromGitHubCommand = new AsyncRelayCommand(CloneFromGitHubAsync);
            ToggleBuildSystemCommand = new AsyncRelayCommand<GamebuinoProject>(ToggleBuildSystemAsync);

            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            IsLoading = true;
            var list = await _projectService.ScanWorkspaceAsync();
            Projects = new ObservableCollection<GamebuinoProject>(list);
            IsLoading = false;
        }

        private void ApplyFilter()
        {
            var q = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
            FilteredProjects = string.IsNullOrEmpty(q)
                ? Projects
                : new ObservableCollection<GamebuinoProject>(
                    Projects.Where(p => p.Name.ToLowerInvariant().Contains(q)));
        }

        private void OpenInVSCode(GamebuinoProject? project)
        {
            var p = project ?? SelectedProject;
            if (p is null) return;
            _settings.AddRecentProject(p.FolderPath);
            _vscode.OpenProject(p);
        }

        private async Task BuildAsync(GamebuinoProject? project)
        {
            var p = project ?? SelectedProject;
            if (p is null) return;
            await RunBuildOperationAsync(p, _build.BuildAsync);
        }

        private async Task FlashAsync(GamebuinoProject? project)
        {
            var p = project ?? SelectedProject;
            if (p is null) return;
            await RunBuildOperationAsync(p, _build.FlashAsync);
        }

        private async Task MonitorAsync(GamebuinoProject? project)
        {
            var p = project ?? SelectedProject;
            if (p is null) return;
            await RunBuildOperationAsync(p, _build.MonitorAsync);
        }

        private void CancelOperation() => _cts?.Cancel();

        /// <summary>Force la chaîne de build d'un projet et fige le choix (.aka-build).</summary>
        private async Task ToggleBuildSystemAsync(GamebuinoProject? project)
        {
            var p = project ?? SelectedProject;
            if (p is null) return;
            p.BuildSystem = p.IsEspIdf ? Models.BuildSystem.PlatformIO : Models.BuildSystem.EspIdf;
            p.WriteBuildMarker();          // écrit .aka-build pour rendre le choix persistant
            await RefreshAsync();          // relit la liste (le marqueur est prioritaire)
        }

        private void NewProjectNavigate() =>
            App.Services.GetRequiredService<MainViewModel>().NavigateToNewProjectCommand.Execute(null);

        private void OpenInExplorer(GamebuinoProject? project)
        {
            var p = project ?? SelectedProject;
            if (p is null || !Directory.Exists(p.FolderPath)) return;
            System.Diagnostics.Process.Start("explorer.exe", $"\"{p.FolderPath}\"");
        }

        private void DeleteProject(GamebuinoProject? project)
        {
            var p = project ?? SelectedProject;
            if (p is null) return;
            var res = MessageBox.Show(
                $"Supprimer le projet \"{p.Name}\" ?\nCette action est irréversible.",
                "Confirmer la suppression", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
            _projectService.DeleteProject(p);
            Projects.Remove(p);
            ApplyFilter();
        }

        private void ShowClone()
        {
            CloneUrl = string.Empty;
            CloneFolderName = string.Empty;
            ShowCloneDialog = true;
        }

        private void CancelClone() => ShowCloneDialog = false;

        private async Task CloneFromGitHubAsync()
        {
            if (string.IsNullOrWhiteSpace(CloneUrl)) return;

            ShowCloneDialog = false;
            OutputLog = string.Empty;
            ShowOutput = true;
            IsBusy = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var sb = new StringBuilder();
            try
            {
                var folderName = string.IsNullOrWhiteSpace(CloneFolderName)
                    ? GitService.ExtractRepoName(CloneUrl)
                    : CloneFolderName.Trim();

                var clonedPath = await _git.CloneAsync(CloneUrl, folderName,
                    line =>
                    {
                        sb.AppendLine(line);
                        Application.Current.Dispatcher.Invoke(() => OutputLog = sb.ToString());
                    }, _cts.Token);

                _settings.AddRecentProject(clonedPath);
                sb.AppendLine($"\n✅ Clonage terminé : {clonedPath}");
                Application.Current.Dispatcher.Invoke(() => OutputLog = sb.ToString());
                await RefreshAsync();
            }
            catch (OperationCanceledException)
            {
                sb.AppendLine("\n[Opération annulée]");
                Application.Current.Dispatcher.Invoke(() => OutputLog = sb.ToString());
            }
            catch (Exception ex)
            {
                Log.Error("Clonage GitHub échoué.", ex);
                sb.AppendLine($"\n❌ Erreur : {ex.Message}");
                Application.Current.Dispatcher.Invoke(() => OutputLog = sb.ToString());
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RunBuildOperationAsync(GamebuinoProject project,
            Func<GamebuinoProject, Action<string>?, CancellationToken, Task> operation)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            OutputLog = string.Empty;
            ShowOutput = true;
            IsBusy = true;

            var sb = new StringBuilder();
            var chain = project.IsEspIdf ? "ESP-IDF" : "PlatformIO";
            sb.AppendLine($"[{chain}] {project.Name}");
            Application.Current.Dispatcher.Invoke(() => OutputLog = sb.ToString());

            try
            {
                await operation(project, line =>
                {
                    sb.AppendLine(line);
                    Application.Current.Dispatcher.Invoke(() => OutputLog = sb.ToString());
                }, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                OutputLog += "\n[Opération annulée]";
            }
            catch (Exception ex)
            {
                Log.Error($"Opération de build échouée ({project.Name}).", ex);
                OutputLog += $"\n[Erreur] {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
