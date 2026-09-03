using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Services;
using GamebuinoAKA.IDE.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly PlatformIOService _platformIO;
        private readonly VSCodeService _vscode;

        private object? _currentPage;
        public object? CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private string _statusMessage = "Prêt";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _platformIOVersion = "...";
        public string PlatformIOVersion
        {
            get => _platformIOVersion;
            set => SetProperty(ref _platformIOVersion, value);
        }

        private string _vSCodePath = "...";
        public string VSCodePath
        {
            get => _vSCodePath;
            set => SetProperty(ref _vSCodePath, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                SetProperty(ref _isBusy, value);
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
        public bool IsNotBusy => !IsBusy;

        public ICommand NavigateToHomeCommand { get; }
        public ICommand NavigateToProjectsCommand { get; }
        public ICommand NavigateToNewProjectCommand { get; }
        public ICommand NavigateToSpriteEditorCommand { get; }
        public ICommand NavigateToTilemapEditorCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }

        public MainViewModel(PlatformIOService platformIO, VSCodeService vscode)
        {
            _platformIO = platformIO;
            _vscode = vscode;

            NavigateToHomeCommand = new RelayCommand(NavigateToHome);
            NavigateToProjectsCommand = new RelayCommand(NavigateToProjects);
            NavigateToNewProjectCommand = new RelayCommand(NavigateToNewProject);
            NavigateToSpriteEditorCommand = new RelayCommand(NavigateToSpriteEditor);
            NavigateToTilemapEditorCommand = new RelayCommand(NavigateToTilemapEditor);
            NavigateToSettingsCommand = new RelayCommand(NavigateToSettings);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            NavigateToHome();
            PlatformIOVersion = await _platformIO.GetVersionAsync();
            VSCodePath = _vscode.GetDisplayPath();
        }

        private void NavigateToHome() =>
            CurrentPage = App.Services.GetRequiredService<HomeView>();
        private void NavigateToProjects() =>
            CurrentPage = App.Services.GetRequiredService<ProjectsView>();
        private void NavigateToNewProject() =>
            CurrentPage = App.Services.GetRequiredService<NewProjectView>();
        private void NavigateToSpriteEditor() =>
            CurrentPage = App.Services.GetRequiredService<SpriteEditorView>();
        private void NavigateToTilemapEditor() =>
            CurrentPage = App.Services.GetRequiredService<TilemapEditorView>();
        private void NavigateToSettings() =>
            CurrentPage = App.Services.GetRequiredService<SettingsView>();
    }
}
