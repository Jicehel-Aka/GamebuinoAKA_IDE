using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Models;
using GamebuinoAKA.IDE.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.ViewModels
{
    public class NewProjectViewModel : ObservableObject
    {
        private readonly TemplateService _templateService;
        private readonly SettingsService _settingsService;
        private readonly CodeSnippetService _snippetService;

        private string _projectName = string.Empty;
        public string ProjectName
        {
            get => _projectName;
            set { SetProperty(ref _projectName, value); CreateProjectCommand?.NotifyCanExecuteChanged(); }
        }

        private string _selectedTemplate = "empty";
        public string SelectedTemplate
        {
            get => _selectedTemplate;
            set => SetProperty(ref _selectedTemplate, value);
        }

        // ── Chaîne de build ───────────────────────────────────────────────────────
        private BuildSystem _buildSystem;
        public BuildSystem BuildSystem
        {
            get => _buildSystem;
            set
            {
                if (SetProperty(ref _buildSystem, value))
                {
                    OnPropertyChanged(nameof(IsPlatformIO));
                    OnPropertyChanged(nameof(IsEspIdf));
                    // Le template Arduino n'a pas de sens en ESP-IDF.
                    if (value == BuildSystem.EspIdf) SelectedTemplate = "esp-idf";
                    else if (SelectedTemplate == "esp-idf") SelectedTemplate = "empty";
                }
            }
        }

        public bool IsPlatformIO
        {
            get => BuildSystem == BuildSystem.PlatformIO;
            set { if (value) BuildSystem = BuildSystem.PlatformIO; }
        }

        public bool IsEspIdf
        {
            get => BuildSystem == BuildSystem.EspIdf;
            set { if (value) BuildSystem = BuildSystem.EspIdf; }
        }

        private string _destinationFolder = string.Empty;
        public string DestinationFolder
        {
            get => _destinationFolder;
            set { SetProperty(ref _destinationFolder, value); CreateProjectCommand?.NotifyCanExecuteChanged(); }
        }

        private bool _isCreating;
        public bool IsCreating
        {
            get => _isCreating;
            set { SetProperty(ref _isCreating, value); CreateProjectCommand?.NotifyCanExecuteChanged(); }
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

        // ── Onglet courant dans l'assistant ────────────────────────────────────────
        private int _activeTab;
        public int ActiveTab
        {
            get => _activeTab;
            set => SetProperty(ref _activeTab, value);
        }

        public string[] AvailableTemplates { get; } = new[] { "empty", "hello-world", "game-template" };

        public IRelayCommand CreateProjectCommand { get; }
        public ICommand BrowseDestinationCommand { get; }
        public ICommand GoToSnippetsCommand { get; }
        public ICommand BackToSetupCommand { get; }

        /// <summary>
        /// ViewModel du sélecteur de snippets, synchronisé avec le BuildSystem courant.
        /// La vue de création l'héberge directement (pas de navigation séparée).
        /// </summary>
        public CodeSnippetPickerViewModel SnippetPicker { get; }

        public NewProjectViewModel(TemplateService templateService,
            SettingsService settingsService, CodeSnippetService snippetService)
        {
            _templateService = templateService;
            _settingsService = settingsService;
            _snippetService  = snippetService;

            SnippetPicker = new CodeSnippetPickerViewModel(snippetService);

            // Créer les commandes AVANT de toucher aux propriétés : leurs setters
            // appellent CreateProjectCommand.NotifyCanExecuteChanged().
            CreateProjectCommand = new AsyncRelayCommand(CreateProjectAsync, CanCreate);
            BrowseDestinationCommand = new RelayCommand(BrowseDestination);
            GoToSnippetsCommand  = new RelayCommand(() => ActiveTab = 1);
            BackToSetupCommand   = new RelayCommand(() => ActiveTab = 0);

            _buildSystem = settingsService.Settings.DefaultBuildSystem;
            if (_buildSystem == BuildSystem.EspIdf) _selectedTemplate = "esp-idf";
            DestinationFolder = settingsService.Settings.WorkspaceFolder;

            // Synchronise le picker quand le build system change
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BuildSystem))
                    SnippetPicker.BuildSystem = BuildSystem;
            };
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
                await _templateService.CreateProjectAsync(
                    ProjectName, SelectedTemplate, DestinationFolder, BuildSystem);

                // Applique les fichiers générés par les snippets sélectionnés
                var snippetFiles = SnippetPicker.GenerateFiles(ProjectName);
                if (snippetFiles.Count > 0)
                    await ApplySnippetFilesAsync(targetDir, snippetFiles);

                _settingsService.AddRecentProject(targetDir);

                var selectedCount = SnippetPicker.GetSelected().Count;
                var kind = BuildSystem == BuildSystem.EspIdf ? "ESP-IDF" : "PlatformIO";
                StatusMessage = selectedCount > 0
                    ? $"Projet {kind} « {ProjectName} » créé avec {selectedCount} fonction(s) intégrée(s) !"
                    : $"Projet {kind} « {ProjectName} » créé avec succès !";

                if (BuildSystem == BuildSystem.EspIdf &&
                    string.IsNullOrWhiteSpace(_settingsService.Settings.ReferenceGamebuinoComponentPath))
                {
                    StatusMessage += " Pense à ajouter components/gamebuino (voir README du projet).";
                }

                App.Services.GetRequiredService<MainViewModel>()
                    .NavigateToProjectsCommand.Execute(null);
            }
            catch (Exception ex)
            {
                Log.Error($"Création de projet « {ProjectName} » échouée.", ex);
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

        /// <summary>
        /// Écrit les fichiers générés par les snippets dans le projet créé.
        /// Remplace les fichiers existants (squelette de base).
        /// </summary>
        private static async Task ApplySnippetFilesAsync(
            string projectDir, Dictionary<string, string> files)
        {
            foreach (var kv in files)
            {
                var path = Path.Combine(projectDir, kv.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, kv.Value);
            }
        }
    }
}
