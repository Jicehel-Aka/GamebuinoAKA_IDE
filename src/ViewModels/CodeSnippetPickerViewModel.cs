using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Models;
using GamebuinoAKA.IDE.Services;

namespace GamebuinoAKA.IDE.ViewModels
{
    /// <summary>
    /// ViewModel du sélecteur de snippets, utilisé dans la page Nouveau Projet.
    /// Peut aussi servir de vue autonome dans la navigation (banque de fonctions).
    /// </summary>
    public class CodeSnippetPickerViewModel : ObservableObject
    {
        private readonly CodeSnippetService _service;

        // ── Catalogue ─────────────────────────────────────────────────────────────

        /// <summary>Système de build courant — filtre les snippets compatibles.</summary>
        private BuildSystem _buildSystem = BuildSystem.PlatformIO;
        public BuildSystem BuildSystem
        {
            get => _buildSystem;
            set { SetProperty(ref _buildSystem, value); Reload(); }
        }

        private ObservableCollection<SnippetCategoryGroup> _categories = new();
        public ObservableCollection<SnippetCategoryGroup> Categories
        {
            get => _categories;
            private set => SetProperty(ref _categories, value);
        }

        private ObservableCollection<SelectableSnippet> _filtered = new();
        public ObservableCollection<SelectableSnippet> Filtered
        {
            get => _filtered;
            private set => SetProperty(ref _filtered, value);
        }

        // ── Sélection courante (pour détail) ──────────────────────────────────────

        private SelectableSnippet? _focused;
        public SelectableSnippet? Focused
        {
            get => _focused;
            set
            {
                SetProperty(ref _focused, value);
                OnPropertyChanged(nameof(HasFocus));
                UpdatePreview();
            }
        }
        public bool HasFocus => Focused is not null;

        // ── Recherche ─────────────────────────────────────────────────────────────

        private string _search = string.Empty;
        public string Search
        {
            get => _search;
            set { SetProperty(ref _search, value); ApplyFilter(); }
        }

        private string _categoryFilter = string.Empty;
        public string CategoryFilter
        {
            get => _categoryFilter;
            set { SetProperty(ref _categoryFilter, value); ApplyFilter(); }
        }

        private ObservableCollection<string> _availableCategories = new();
        public ObservableCollection<string> AvailableCategories
        {
            get => _availableCategories;
            private set => SetProperty(ref _availableCategories, value);
        }

        // ── Preview ───────────────────────────────────────────────────────────────

        private string _previewCode = string.Empty;
        public string PreviewCode
        {
            get => _previewCode;
            private set => SetProperty(ref _previewCode, value);
        }

        // ── Résumé de la sélection ────────────────────────────────────────────────

        public int SelectedCount => _allSelectable.Count(s => s.IsSelected);

        public string SelectionSummary
        {
            get
            {
                var sel = _allSelectable.Where(s => s.IsSelected).ToList();
                if (sel.Count == 0) return "Aucune fonction sélectionnée — squelette de base uniquement.";
                return $"{sel.Count} fonction(s) sélectionnée(s) : " +
                       string.Join(", ", sel.Take(4).Select(s => s.Snippet.Name)) +
                       (sel.Count > 4 ? "…" : "");
            }
        }

        // ── Commandes ─────────────────────────────────────────────────────────────
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand FocusCommand { get; }

        // ── État interne ──────────────────────────────────────────────────────────
        private readonly List<SelectableSnippet> _allSelectable = new();

        public CodeSnippetPickerViewModel(CodeSnippetService service)
        {
            _service = service;

            SelectAllCommand   = new RelayCommand(SelectAll);
            DeselectAllCommand = new RelayCommand(DeselectAll);
            FocusCommand       = new RelayCommand<SelectableSnippet>(s => Focused = s);

            Reload();
        }

        // ── Chargement ────────────────────────────────────────────────────────────

        public void Reload()
        {
            var all = _service.GetAll(_buildSystem);

            // Conserve l'état de sélection précédent par Id
            var prevSelected = new HashSet<string>(
                _allSelectable.Where(s => s.IsSelected).Select(s => s.Snippet.Id));

            _allSelectable.Clear();
            foreach (var s in all)
            {
                var ss = new SelectableSnippet(s);
                ss.IsSelected = prevSelected.Contains(s.Id);
                ss.PropertyChanged += (_, __) =>
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(SelectionSummary));
                    UpdatePreview();
                };
                _allSelectable.Add(ss);
            }

            var cats = new List<string> { "(Toutes)" };
            cats.AddRange(all.Select(s => s.Category).Distinct().OrderBy(c => c));
            AvailableCategories = new ObservableCollection<string>(cats);

            RebuildCategories();
            ApplyFilter();
        }

        private void RebuildCategories()
        {
            var groups = _allSelectable
                .GroupBy(s => s.Snippet.Category)
                .OrderBy(g => g.Key)
                .Select(g => new SnippetCategoryGroup
                {
                    Category = g.Key,
                    Snippets = new ObservableCollection<SelectableSnippet>(g)
                });
            Categories = new ObservableCollection<SnippetCategoryGroup>(groups);
        }

        private void ApplyFilter()
        {
            var q    = Search.Trim().ToLowerInvariant();
            var cat  = CategoryFilter;

            var source = _allSelectable.AsEnumerable();

            if (!string.IsNullOrEmpty(q))
                source = source.Where(s =>
                    s.Snippet.Name.Contains(q, System.StringComparison.OrdinalIgnoreCase) ||
                    s.Snippet.Summary.Contains(q, System.StringComparison.OrdinalIgnoreCase) ||
                    s.Snippet.Tags.Any(t => t.Contains(q, System.StringComparison.OrdinalIgnoreCase)) ||
                    s.Snippet.Category.Contains(q, System.StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(cat) && cat != "(Toutes)")
                source = source.Where(s => s.Snippet.Category == cat);

            Filtered = new ObservableCollection<SelectableSnippet>(
                source.OrderBy(s => s.Snippet.Category).ThenBy(s => s.Snippet.Name));
        }

        // ── Preview ───────────────────────────────────────────────────────────────

        private void UpdatePreview()
        {
            if (Focused is null)
            {
                PreviewCode = string.Empty;
                return;
            }
            // Affiche le code du snippet focalisé, marqueurs nettoyés pour la lisibilité
            PreviewCode = CleanMarkersForDisplay(Focused.Snippet.Code);
        }

        private static string CleanMarkersForDisplay(string code)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;
            var lines = code.Split('\n')
                .Select(l => l.TrimStart())
                .Where(l => !l.StartsWith("//@@"))
                .ToArray();
            return string.Join('\n', lines).Trim();
        }

        // ── Actions ───────────────────────────────────────────────────────────────

        private void SelectAll()
        {
            foreach (var s in _allSelectable) s.IsSelected = true;
        }

        private void DeselectAll()
        {
            foreach (var s in _allSelectable) s.IsSelected = false;
        }

        /// <summary>Retourne les snippets actuellement cochés.</summary>
        public IReadOnlyList<CodeSnippet> GetSelected() =>
            _allSelectable.Where(s => s.IsSelected).Select(s => s.Snippet).ToList();

        /// <summary>Génère le dictionnaire de fichiers pour le projet en cours de création.</summary>
        public Dictionary<string, string> GenerateFiles(string projectName) =>
            _service.GenerateFiles(GetSelected(), BuildSystem, projectName);
    }

    // ── Wrapper observable pour la case à cocher ──────────────────────────────────

    public class SelectableSnippet : ObservableObject
    {
        public CodeSnippet Snippet { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public SelectableSnippet(CodeSnippet snippet)
        {
            Snippet = snippet;
        }
    }

    // ── Groupe catégorie ───────────────────────────────────────────────────────────

    public class SnippetCategoryGroup : ObservableObject
    {
        public string Category { get; set; } = string.Empty;
        public ObservableCollection<SelectableSnippet> Snippets { get; set; } = new();
        public int Count => Snippets.Count;
    }
}
