using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.ViewModels
{
    public class NewProjectViewModel : ObservableObject
    {
        private readonly TemplateService _templateService;
        private readonly SettingsService _settingsService;

        private string _projectName = string.Empty;
        public string ProjectName
        {
            get => _projectName;
            set
            {
                SetProperty(ref _projectName, value);
                CreateProjectCommand.NotifyCanExecuteChanged();
            }
        }

        private string _selectedTemplate = "empty";
        public string SelectedTemplate
        {
            get => _selectedTemplate;
            set => SetProperty(ref _selectedTemplate, value);
        }

        private string _destinationFolder = string.Empty;
        public string DestinationFolder
        {
            get => _destinationFolder;
            set
            {
                SetProperty(ref _destinationFolder, value);
                CreateProjectCommand.NotifyCanExecuteChanged();
            }
        }

        private bool _isCreating;
        public bool IsCreating
        {
            get => _isCreating;
            set
            {
                SetProperty(ref _isCreating, value);
                CreateProjectCommand.NotifyCanExecuteChanged();
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public string[] AvailableTemplates { get; } = new[] { "empty", "hello-world", "game-template" };

        public IRelayCommand CreateProjectCommand { get; }
        public ICommand BrowseDestinationCommand { get; }

        public NewProjectViewModel(TemplateService templateService, SettingsService settingsService)
        {
            _templateService = templateService;
            _settingsService = settingsService;
            DestinationFolder = settingsService.Settings.WorkspaceFolder;

            CreateProjectCommand = new AsyncRelayCommand(CreateProjectAsync, CanCreate);
            BrowseDestinationCommand = new RelayCommand(BrowseDestination);
        }

        private void BrowseDestination()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Choisir le dossier de destination",
                SelectedPath = DestinationFolder,
                UseDescriptionForTitle = true
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                DestinationFolder = dlg.SelectedPath;
        }

        private async Task CreateProjectAsync()
        {
            StatusMessage = string.Empty;
            HasError = false;

            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                StatusMessage = "Le nom du projet est requis.";
                HasError = true;
                return;
            }

            var targetDir = Path.Combine(DestinationFolder, ProjectName);
            if (Directory.Exists(targetDir))
            {
                StatusMessage = $"Un dossier « {ProjectName} » existe déjà à cet emplacement.";
                HasError = true;
                return;
            }

            IsCreating = true;
            try
            {
                await _templateService.CreateProjectAsync(ProjectName, SelectedTemplate, DestinationFolder);
                _settingsService.AddRecentProject(targetDir);
                StatusMessage = $"Projet « {ProjectName} » créé avec succès !";

                var main = App.Services.GetRequiredService<MainViewModel>();
                main.NavigateToProjectsCommand.Execute(null);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
                HasError = true;
            }
            finally
            {
                IsCreating = false;
            }
        }

        private bool CanCreate() =>
            !IsCreating
            && !string.IsNullOrWhiteSpace(ProjectName)
            && !string.IsNullOrWhiteSpace(DestinationFolder);
    }
}
