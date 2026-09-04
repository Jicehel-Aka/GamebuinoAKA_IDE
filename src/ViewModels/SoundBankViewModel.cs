using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Models;
using GamebuinoAKA.IDE.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GamebuinoAKA.IDE.ViewModels
{
    public class SoundBankViewModel : ObservableObject
    {
        private readonly SoundBankService _bankService;
        private readonly ProjectService _projectService;
        private readonly SettingsService _settings;

        // ── Banque active ──────────────────────────────────────────────────────────
        private SoundBank _bank = new SoundBank();

        // ── Collections ───────────────────────────────────────────────────────────
        private ObservableCollection<SoundCategoryGroup> _categories = new();
        public ObservableCollection<SoundCategoryGroup> Categories
        {
            get => _categories;
            private set => SetProperty(ref _categories, value);
        }

        private ObservableCollection<SoundAsset> _filteredAssets = new();
        public ObservableCollection<SoundAsset> FilteredAssets
        {
            get => _filteredAssets;
            private set => SetProperty(ref _filteredAssets, value);
        }

        private ObservableCollection<GamebuinoProject> _availableProjects = new();
        public ObservableCollection<GamebuinoProject> AvailableProjects
        {
            get => _availableProjects;
            private set => SetProperty(ref _availableProjects, value);
        }

        // ── Sélection ─────────────────────────────────────────────────────────────
        private SoundAsset? _selectedAsset;
        public SoundAsset? SelectedAsset
        {
            get => _selectedAsset;
            set
            {
                SetProperty(ref _selectedAsset, value);
                StopPlayback();
                IsEditing = false;
                if (value is not null)
                    LoadEditFields(value);
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanPreview));
            }
        }

        public bool HasSelection => SelectedAsset is not null;
        public bool CanPreview => HasSelection && !string.IsNullOrEmpty(SelectedAsset?.PreviewWavPath)
                                  && File.Exists(SelectedAsset.PreviewWavPath);

        private string _selectedThemeFilter = string.Empty;
        public string SelectedThemeFilter
        {
            get => _selectedThemeFilter;
            set { SetProperty(ref _selectedThemeFilter, value); ApplyFilter(); }
        }

        private string _selectedTypeFilter = string.Empty;
        public string SelectedTypeFilter
        {
            get => _selectedTypeFilter;
            set { SetProperty(ref _selectedTypeFilter, value); ApplyFilter(); }
        }

        // ── Filtre / recherche ─────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }

        // ── Édition ───────────────────────────────────────────────────────────────
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        // Champs de l'éditeur liés au formulaire
        private string _editName = string.Empty;
        public string EditName { get => _editName; set => SetProperty(ref _editName, value); }

        private string _editDescription = string.Empty;
        public string EditDescription { get => _editDescription; set => SetProperty(ref _editDescription, value); }

        private string _editTheme = string.Empty;
        public string EditTheme { get => _editTheme; set => SetProperty(ref _editTheme, value); }

        private string _editTags = string.Empty;
        public string EditTags { get => _editTags; set => SetProperty(ref _editTags, value); }

        private string _editType = "SoundFx";
        public string EditType { get => _editType; set => SetProperty(ref _editType, value); }

        // ── État UI ────────────────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        private bool _isScanning;
        public bool IsScanning { get => _isScanning; set => SetProperty(ref _isScanning, value); }

        private bool _isPlaying;
        public bool IsPlaying { get => _isPlaying; set => SetProperty(ref _isPlaying, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // Thèmes disponibles pour les filtres
        private ObservableCollection<string> _availableThemes = new();
        public ObservableCollection<string> AvailableThemes
        {
            get => _availableThemes;
            private set => SetProperty(ref _availableThemes, value);
        }

        // ── Commandes ─────────────────────────────────────────────────────────────
        public ICommand PlayCommand { get; }
        public ICommand PlayAssetCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SelectAssetCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand SaveEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ImportFileCommand { get; }
        public ICommand ScanProjectCommand { get; }
        public ICommand AddToProjectCommand { get; }
        public ICommand OpenFileLocationCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearFilterCommand { get; }

        public SoundBankViewModel(SoundBankService bankService,
            ProjectService projectService, SettingsService settings)
        {
            _bankService = bankService;
            _projectService = projectService;
            _settings = settings;

            PlayCommand = new RelayCommand(PlaySelected, () => CanPreview);
            PlayAssetCommand = new RelayCommand<SoundAsset>(a =>
            {
                SelectedAsset = a;
                PlaySelected();
            });
            StopCommand = new RelayCommand(StopPlayback);
            SelectAssetCommand = new RelayCommand<SoundAsset>(a => SelectedAsset = a);
            EditCommand = new RelayCommand(BeginEdit, () => HasSelection);
            SaveEditCommand = new RelayCommand(SaveEdit, () => IsEditing);
            CancelEditCommand = new RelayCommand(CancelEdit, () => IsEditing);
            DeleteCommand = new RelayCommand(DeleteSelected, () => HasSelection);
            ImportFileCommand = new RelayCommand(ImportFile);
            ScanProjectCommand = new AsyncRelayCommand<GamebuinoProject>(ScanProjectAsync);
            AddToProjectCommand = new AsyncRelayCommand<GamebuinoProject>(AddToProjectAsync);
            OpenFileLocationCommand = new RelayCommand(OpenFileLocation, () => HasSelection);
            RefreshCommand = new RelayCommand(Reload);
            ClearFilterCommand = new RelayCommand(ClearFilter);

            Reload();
            _ = LoadProjectsAsync();
        }

        // ── Chargement ────────────────────────────────────────────────────────────

        private void Reload()
        {
            IsLoading = true;
            _bank = _bankService.LoadBank();
            RebuildCategories();
            ApplyFilter();
            IsLoading = false;
            StatusMessage = $"{_bank.Assets.Count} asset(s) dans la banque";
        }

        private async System.Threading.Tasks.Task LoadProjectsAsync()
        {
            var projects = await _projectService.ScanWorkspaceAsync();
            Application.Current.Dispatcher.Invoke(() =>
                AvailableProjects = new ObservableCollection<GamebuinoProject>(projects));
        }

        // ── Catégories ────────────────────────────────────────────────────────────

        private void RebuildCategories()
        {
            var themes = _bank.Assets
                .Select(a => string.IsNullOrEmpty(a.Theme) ? "Divers" : a.Theme)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            // Ajouter les thèmes personnalisés qui n'ont pas encore d'assets
            foreach (var ct in _bank.CustomThemes)
                if (!themes.Contains(ct)) themes.Add(ct);

            var groups = themes.Select(t => new SoundCategoryGroup
            {
                Theme = t,
                Assets = new ObservableCollection<SoundAsset>(
                    _bank.Assets.Where(a =>
                        (string.IsNullOrEmpty(a.Theme) ? "Divers" : a.Theme) == t)
                    .OrderBy(a => a.Name))
            }).ToList();

            Categories = new ObservableCollection<SoundCategoryGroup>(groups);

            // Mettre à jour la liste des thèmes pour les filtres
            var themesList = new List<string> { "(Tous)" };
            themesList.AddRange(themes);
            AvailableThemes = new ObservableCollection<string>(themesList);
        }

        // ── Filtre ────────────────────────────────────────────────────────────────

        private void ApplyFilter()
        {
            var q = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
            var themeFilter = SelectedThemeFilter;
            var typeFilter = SelectedTypeFilter;

            var filtered = _bank.Assets.AsEnumerable();

            if (!string.IsNullOrEmpty(q))
                filtered = filtered.Where(a =>
                    a.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    a.Theme.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    a.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    a.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    a.SourceProject.Contains(q, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(themeFilter) && themeFilter != "(Tous)")
                filtered = filtered.Where(a =>
                    (string.IsNullOrEmpty(a.Theme) ? "Divers" : a.Theme) == themeFilter);

            if (typeFilter == "Sons FX")
                filtered = filtered.Where(a => a.AssetType == SoundAssetType.SoundFx);
            else if (typeFilter == "Musiques")
                filtered = filtered.Where(a => a.AssetType == SoundAssetType.Music);

            FilteredAssets = new ObservableCollection<SoundAsset>(
                filtered.OrderBy(a => a.Theme).ThenBy(a => a.Name));
        }

        private void ClearFilter()
        {
            SearchText = string.Empty;
            SelectedThemeFilter = string.Empty;
            SelectedTypeFilter = string.Empty;
        }

        // ── Lecture audio ──────────────────────────────────────────────────────────

        private void PlaySelected()
        {
            if (SelectedAsset is null) return;
            _bankService.Play(SelectedAsset);
            IsPlaying = true;
            StatusMessage = $"▶ Lecture : {SelectedAsset.Name}";
        }

        private void StopPlayback()
        {
            _bankService.StopPlayback();
            IsPlaying = false;
            if (SelectedAsset is not null)
                StatusMessage = $"Sélectionné : {SelectedAsset.Name}";
        }

        // ── Édition ───────────────────────────────────────────────────────────────

        private void BeginEdit()
        {
            if (SelectedAsset is null) return;
            LoadEditFields(SelectedAsset);
            IsEditing = true;
        }

        private void LoadEditFields(SoundAsset asset)
        {
            EditName = asset.Name;
            EditDescription = asset.Description;
            EditTheme = asset.Theme;
            EditTags = asset.TagsDisplay;
            EditType = asset.AssetType == SoundAssetType.Music ? "Music" : "SoundFx";
        }

        private void SaveEdit()
        {
            if (SelectedAsset is null) return;

            SelectedAsset.Name = EditName.Trim();
            SelectedAsset.Description = EditDescription.Trim();
            SelectedAsset.Theme = EditTheme.Trim();
            SelectedAsset.Tags = EditTags
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            SelectedAsset.AssetType = EditType == "Music"
                ? SoundAssetType.Music : SoundAssetType.SoundFx;

            _bankService.UpdateAsset(SelectedAsset, _bank);
            IsEditing = false;
            Reload();
            StatusMessage = $"✅ Asset « {SelectedAsset.Name} » mis à jour.";
        }

        private void CancelEdit() => IsEditing = false;

        // ── Suppression ────────────────────────────────────────────────────────────

        private void DeleteSelected()
        {
            if (SelectedAsset is null) return;
            var res = MessageBox.Show(
                $"Supprimer « {SelectedAsset.Name} » de la banque ?\n(Le fichier source n'est pas supprimé.)",
                "Confirmer la suppression", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;

            _bankService.RemoveAsset(SelectedAsset, _bank);
            SelectedAsset = null;
            Reload();
            StatusMessage = "Asset supprimé de la banque.";
        }

        // ── Import ────────────────────────────────────────────────────────────────

        private void ImportFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importer un fichier audio",
                Filter = "Fichiers audio|*.wav;*.pmf;*.h|WAV|*.wav|PMF|*.pmf|Headers C|*.h|Tous|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            int added = 0;
            foreach (var file in dlg.FileNames)
            {
                if (_bank.Assets.Any(a =>
                    string.Equals(a.FilePath, file, StringComparison.OrdinalIgnoreCase)))
                    continue;
                _bankService.ImportFile(file, _bank);
                added++;
            }
            Reload();
            StatusMessage = added > 0
                ? $"✅ {added} fichier(s) importé(s) dans la banque."
                : "Aucun nouveau fichier importé (déjà présents).";
        }

        // ── Scan de projet ────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task ScanProjectAsync(GamebuinoProject? project)
        {
            if (project is null) return;
            IsScanning = true;
            StatusMessage = $"Scan de « {project.Name} »…";
            try
            {
                var newAssets = await _bankService.ScanProjectAsync(project, _bank);
                if (newAssets.Count == 0)
                {
                    StatusMessage = $"Aucun nouvel asset trouvé dans « {project.Name} ».";
                    return;
                }
                foreach (var a in newAssets)
                    _bank.Assets.Add(a);
                _bankService.SaveBank(_bank);
                Reload();
                StatusMessage = $"✅ {newAssets.Count} asset(s) importé(s) depuis « {project.Name} ».";
            }
            catch (Exception ex)
            {
                Log.Error("Scan audio échoué.", ex);
                StatusMessage = $"❌ Erreur lors du scan : {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        // ── Ajout dans un projet ───────────────────────────────────────────────────

        private async System.Threading.Tasks.Task AddToProjectAsync(GamebuinoProject? project)
        {
            if (project is null || SelectedAsset is null) return;
            if (!File.Exists(SelectedAsset.FilePath))
            {
                StatusMessage = "❌ Fichier source introuvable.";
                return;
            }
            try
            {
                var dest = await System.Threading.Tasks.Task.Run(
                    () => _bankService.AddToProject(SelectedAsset, project));
                StatusMessage = $"✅ Copié dans le projet « {project.Name} » : {Path.GetFileName(dest)}";
            }
            catch (Exception ex)
            {
                Log.Error("Ajout dans projet échoué.", ex);
                StatusMessage = $"❌ Erreur : {ex.Message}";
            }
        }

        // ── Ouvrir l'emplacement fichier ───────────────────────────────────────────

        private void OpenFileLocation()
        {
            if (SelectedAsset is null) return;
            var dir = Path.GetDirectoryName(SelectedAsset.FilePath);
            if (dir is not null && Directory.Exists(dir))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{SelectedAsset.FilePath}\"");
        }
    }

    // ── Modèle de groupe catégorie ─────────────────────────────────────────────────

    public class SoundCategoryGroup : ObservableObject
    {
        public string Theme { get; set; } = string.Empty;
        public ObservableCollection<SoundAsset> Assets { get; set; } = new();
        public int Count => Assets.Count;
    }
}
