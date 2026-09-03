using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Models;
using GamebuinoAKA.IDE.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.ViewModels
{
    public class HomeViewModel : ObservableObject
    {
        private readonly ProjectService _projectService;
        private readonly SettingsService _settingsService;

        private ObservableCollection<GamebuinoProject> _recentProjects = new ObservableCollection<GamebuinoProject>();
        public ObservableCollection<GamebuinoProject> RecentProjects
        {
            get => _recentProjects;
            set => SetProperty(ref _recentProjects, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand NewProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenRecentProjectCommand { get; }
        public ICommand CloneFromGitHubCommand { get; }

        public HomeViewModel(ProjectService projectService, SettingsService settingsService)
        {
            _projectService = projectService;
            _settingsService = settingsService;

            NewProjectCommand = new RelayCommand(NewProject);
            OpenProjectCommand = new RelayCommand(OpenProject);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenRecentProjectCommand = new RelayCommand<GamebuinoProject>(OpenRecentProject);
            CloneFromGitHubCommand = new RelayCommand(CloneFromGitHub);

            _ = LoadRecentProjectsAsync();
        }

        private async Task LoadRecentProjectsAsync()
        {
            IsLoading = true;
            var projects = await _projectService.GetRecentProjectsAsync();
            RecentProjects = new ObservableCollection<GamebuinoProject>(projects);
            IsLoading = false;
        }

        private void NewProject()
        {
            var main = App.Services.GetRequiredService<MainViewModel>();
            main.NavigateToNewProjectCommand.Execute(null);
        }

        private void OpenProject()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Ouvrir un projet Gamebuino AKA",
                Filter = "PlatformIO Project|platformio.ini",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() == true)
            {
                var folder = System.IO.Path.GetDirectoryName(dlg.FileName)!;
                _settingsService.AddRecentProject(folder);
                _ = LoadRecentProjectsAsync();
            }
        }

        private void OpenSettings()
        {
            var main = App.Services.GetRequiredService<MainViewModel>();
            main.NavigateToSettingsCommand.Execute(null);
        }

        private void OpenRecentProject(GamebuinoProject? project)
        {
            if (project is null) return;
            _settingsService.AddRecentProject(project.FolderPath);
            var vscode = App.Services.GetRequiredService<VSCodeService>();
            vscode.OpenProject(project);
        }

        private void CloneFromGitHub()
        {
            var main = App.Services.GetRequiredService<MainViewModel>();
            main.NavigateToProjectsCommand.Execute(null);
            var vm = App.Services.GetRequiredService<ProjectsViewModel>();
            vm.ShowCloneCommand.Execute(null);
        }
    }
}
