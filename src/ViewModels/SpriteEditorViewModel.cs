using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using GamebuinoAKA.IDE.Models;
using GamebuinoAKA.IDE.Services;
using DBitmap = System.Drawing.Bitmap;
using DColor = System.Drawing.Color;
using DRectangle = System.Drawing.Rectangle;

namespace GamebuinoAKA.IDE.ViewModels
{
    /// <summary>
    /// Éditeur de sprites orienté « image de travail » : on importe une image
    /// (souvent une planche), on sélectionne une portion, on rogne, on réduit à
    /// la taille cible, on retouche éventuellement au pixel, et la conversion en
    /// code C++ n'a lieu qu'à la fin (bouton Convertir / Exporter).
    /// </summary>
    public class SpriteEditorViewModel : ObservableObject
    {
        private readonly AssetService _assetService;

        // Écran AKA 320×240. Au-delà, l'aperçu du code est désactivé (sinon l'UI gèle).
        private const long ScreenPixels = 320L * 240L;
        private const int PreviewCodeCharCap = 200_000;

        private DBitmap? _working;   // image de travail (source de vérité)
        private DBitmap? _original;  // pour « Rétablir »

        private ColorFormat _fmt;
        private ushort _transparentKey;

        public SpriteEditorViewModel(AssetService assetService, SettingsService settings)
        {
            _assetService = assetService;
            _fmt = settings.Settings.DefaultColorFormat;
            _transparentKey = settings.Settings.DefaultTransparentKey;

            PaintPixelRelayCommand = new RelayCommand<Point>(p => PaintPixel((int)p.X, (int)p.Y));
            SelectFromCanvasCommand = new RelayCommand<Rect>(OnCanvasSelection);
            SelectColorCommand = new RelayCommand<ushort>(c => { SelectedColor = c; PaintingTransparent = false; });

            ImportImageCommand = new AsyncRelayCommand(ImportImageAsync);
            ImportSpritesheetCommand = new AsyncRelayCommand(ImportSpritesheetAsync);
            CropCommand = new RelayCommand(CropToSelection, () => HasImage && HasSelection);
            ResizeCommand = new RelayCommand(ResizeToTarget, () => HasImage);
            RevertCommand = new RelayCommand(RevertToOriginal, () => _original != null);
            ConvertCommand = new AsyncRelayCommand(ConvertAsync, () => HasImage);
            ExportCppCommand = new AsyncRelayCommand(ExportCppAsync, () => HasImage);
            SaveProjectCommand = new RelayCommand(SaveProject, () => HasImage);
            OpenProjectCommand = new RelayCommand(OpenProject);
            PickTransparentFromSelectedCommand = new RelayCommand(PickTransparentFromSelected);
        }

        // ── État image / affichage ───────────────────────────────────────────────

        private WriteableBitmap? _previewBitmap;
        public WriteableBitmap? PreviewBitmap
        {
            get => _previewBitmap;
            set => SetProperty(ref _previewBitmap, value);
        }

        public bool HasImage => _working != null;

        private int _zoom = 4;
        public int Zoom { get => _zoom; set => SetProperty(ref _zoom, value); }
        public int[] ZoomLevels { get; } = new[] { 1, 2, 4, 8, 16 };

        private string _dimensions = "—";
        public string Dimensions { get => _dimensions; set => SetProperty(ref _dimensions, value); }

        // ── Sélection ──────────────────────────────────────────────────────────────

        private bool _selectionMode;
        public bool SelectionMode { get => _selectionMode; set => SetProperty(ref _selectionMode, value); }

        private Rect _selection;
        public Rect Selection
        {
            get => _selection;
            set
            {
                if (SetProperty(ref _selection, value))
                {
                    OnPropertyChanged(nameof(SelX)); OnPropertyChanged(nameof(SelY));
                    OnPropertyChanged(nameof(SelW)); OnPropertyChanged(nameof(SelH));
                    OnPropertyChanged(nameof(HasSelection));
                    CropCommand.NotifyCanExecuteChanged();
                }
            }
        }
        public bool HasSelection => _selection.Width >= 1 && _selection.Height >= 1;

        public int SelX { get => (int)_selection.X; set => SetSelection(value, SelY, SelW, SelH); }
        public int SelY { get => (int)_selection.Y; set => SetSelection(SelX, value, SelW, SelH); }
        public int SelW { get => (int)_selection.Width; set => SetSelection(SelX, SelY, value, SelH); }
        public int SelH { get => (int)_selection.Height; set => SetSelection(SelX, SelY, SelW, value); }

        private void SetSelection(int x, int y, int w, int h)
        {
            if (_working != null)
            {
                x = Math.Max(0, Math.Min(x, _working.Width - 1));
                y = Math.Max(0, Math.Min(y, _working.Height - 1));
                w = Math.Max(1, Math.Min(w, _working.Width - x));
                h = Math.Max(1, Math.Min(h, _working.Height - y));
            }
            Selection = new Rect(x, y, w, h);
        }

        private void OnCanvasSelection(Rect r)
        {
            StatusMessage = $"Sélection : {(int)r.Width}×{(int)r.Height} à ({(int)r.X},{(int)r.Y})";
        }

        // ── Taille cible ───────────────────────────────────────────────────────────

        private int _targetWidth = 32;
        public int TargetWidth { get => _targetWidth; set => SetProperty(ref _targetWidth, Math.Max(1, value)); }

        private int _targetHeight = 32;
        public int TargetHeight { get => _targetHeight; set => SetProperty(ref _targetHeight, Math.Max(1, value)); }

        private bool _smoothResize = true;
        public bool SmoothResize { get => _smoothResize; set => SetProperty(ref _smoothResize, value); }

        // ── Planche (grille fixe) ────────────────────────────────────────────────────

        private bool _isSheet;

        private int _frameWidth = 16;
        public int FrameWidth
        {
            get => _frameWidth;
            set { if (SetProperty(ref _frameWidth, Math.Max(1, value))) OnPropertyChanged(nameof(FrameInfo)); }
        }

        private int _frameHeight = 16;
        public int FrameHeight
        {
            get => _frameHeight;
            set { if (SetProperty(ref _frameHeight, Math.Max(1, value))) OnPropertyChanged(nameof(FrameInfo)); }
        }

        /// <summary>Résumé de la découpe en frames (affiché sous les champs).</summary>
        public string FrameInfo
        {
            get
            {
                if (!_isSheet || _working is null || FrameWidth <= 0 || FrameHeight <= 0)
                    return "1 frame (image entière)";
                int cols = _working.Width / FrameWidth;
                int rows = _working.Height / FrameHeight;
                int n = Math.Max(1, cols * rows);
                var warn = (_working.Width % FrameWidth != 0 || _working.Height % FrameHeight != 0)
                    ? "  ⚠ non divisible" : "";
                return $"{cols}×{rows} = {n} frames de {FrameWidth}×{FrameHeight}{warn}";
            }
        }

        // ── Couleurs / transparence ─────────────────────────────────────────────────

        private ushort _selectedColor = 0xFFFF;
        public ushort SelectedColor { get => _selectedColor; set => SetProperty(ref _selectedColor, value); }

        private bool _paintingTransparent;
        public bool PaintingTransparent { get => _paintingTransparent; set => SetProperty(ref _paintingTransparent, value); }

        private bool _useTransparency = true;
        public bool UseTransparency { get => _useTransparency; set => SetProperty(ref _useTransparency, value); }

        public string TransparentKeyHex => "0x" + _transparentKey.ToString("X4");

        public ObservableCollection<ushort> Palette { get; } = new ObservableCollection<ushort>(new ushort[]
        {
            0x0000, 0xFFFF, 0xF800, 0x07E0, 0x001F,
            0xFFE0, 0xF81F, 0x07FF, 0x8000, 0x0400,
            0x0010, 0x8410, 0xC618, 0x7BEF, 0x39E7, 0xFFD5
        });

        // ── Code / statut ───────────────────────────────────────────────────────────

        private string _exportedCode = string.Empty;
        public string ExportedCode { get => _exportedCode; set => SetProperty(ref _exportedCode, value); }

        private string _statusMessage = "Importez une image, sélectionnez une portion, rognez/réduisez, puis Convertir.";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // ── Commandes ─────────────────────────────────────────────────────────────

        public ICommand PaintPixelRelayCommand { get; }
        public ICommand SelectFromCanvasCommand { get; }
        public ICommand SelectColorCommand { get; }
        public ICommand ImportImageCommand { get; }
        public ICommand ImportSpritesheetCommand { get; }
        public IRelayCommand CropCommand { get; }
        public IRelayCommand ResizeCommand { get; }
        public IRelayCommand RevertCommand { get; }
        public IRelayCommand ConvertCommand { get; }
        public IRelayCommand ExportCppCommand { get; }
        public IRelayCommand SaveProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand PickTransparentFromSelectedCommand { get; }

        // ── Import ──────────────────────────────────────────────────────────────────

        private async Task ImportImageAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importer une image (ou une planche)",
                Filter = "Images|*.png;*.bmp;*.jpg;*.jpeg"
            };
            if (dlg.ShowDialog() != true) return;

            StatusMessage = "Chargement…";
            try
            {
                var bmp = await Task.Run(() => _assetService.LoadArgb(dlg.FileName));
                SetWorking(bmp, keepOriginal: true);
                SelectionMode = true; // prêt à sélectionner une zone tout de suite
                Selection = default;
                ExportedCode = "/* Travaillez l'image (sélection, rogner, réduire), puis « Convertir » pour générer le code. */";
                StatusMessage = $"Image {bmp.Width}×{bmp.Height} chargée. Glisse sur l'image pour sélectionner un sprite, puis « Rogner ».";
            }
            catch (Exception ex)
            {
                Log.Error("Import image échoué.", ex);
                StatusMessage = $"Erreur d'import : {ex.Message}";
            }
        }

        private async Task ImportSpritesheetAsync()
        {
            if (FrameWidth <= 0 || FrameHeight <= 0)
            {
                StatusMessage = "Renseigne d'abord la taille de frame (Largeur/Hauteur).";
                return;
            }
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importer une planche (grille fixe)",
                Filter = "Images|*.png;*.bmp;*.jpg;*.jpeg"
            };
            if (dlg.ShowDialog() != true) return;

            StatusMessage = "Chargement de la planche…";
            try
            {
                var bmp = await Task.Run(() => _assetService.LoadArgb(dlg.FileName));
                SetWorking(bmp, keepOriginal: true);   // remet _isSheet à false
                _isSheet = true;                       // ...puis on l'active
                OnPropertyChanged(nameof(FrameInfo));
                ExportedCode = "/* Planche importée. « Convertir » génère un tableau unique + _width/_height/_frames. */";
                StatusMessage = $"Planche {bmp.Width}×{bmp.Height} — {FrameInfo}.";
            }
            catch (Exception ex)
            {
                Log.Error("Import spritesheet échoué.", ex);
                StatusMessage = $"Erreur d'import : {ex.Message}";
            }
        }

        private (int fw, int fh, int count) CurrentFrameInfo()
        {
            if (_isSheet && _working != null && FrameWidth > 0 && FrameHeight > 0)
            {
                int cols = _working.Width / FrameWidth;
                int rows = _working.Height / FrameHeight;
                return (FrameWidth, FrameHeight, Math.Max(1, cols * rows));
            }
            return (_working?.Width ?? 0, _working?.Height ?? 0, 1);
        }

        // ── Rogner / réduire / rétablir ──────────────────────────────────────────────

        private void CropToSelection()
        {
            if (_working is null || !HasSelection) return;
            try
            {
                var r = new DRectangle(SelX, SelY, SelW, SelH);
                var cropped = _assetService.Crop(_working, r);
                SetWorking(cropped, keepOriginal: false);
                Selection = default;
                StatusMessage = $"Rogné à {cropped.Width}×{cropped.Height}.";
            }
            catch (Exception ex)
            {
                Log.Error("Rognage échoué.", ex);
                StatusMessage = $"Erreur de rognage : {ex.Message}";
            }
        }

        private void ResizeToTarget()
        {
            if (_working is null) return;
            try
            {
                var resized = _assetService.Resize(_working, TargetWidth, TargetHeight, SmoothResize);
                SetWorking(resized, keepOriginal: false);
                StatusMessage = $"Réduit à {resized.Width}×{resized.Height} ({(SmoothResize ? "lissé" : "au plus proche")}).";
            }
            catch (Exception ex)
            {
                Log.Error("Redimensionnement échoué.", ex);
                StatusMessage = $"Erreur de redimensionnement : {ex.Message}";
            }
        }

        private void RevertToOriginal()
        {
            if (_original is null) return;
            SetWorking((DBitmap)_original.Clone(), keepOriginal: false);
            Selection = default;
            StatusMessage = "Image d'origine rétablie.";
        }

        // ── Conversion / export (à la fin) ──────────────────────────────────────────

        private async Task ConvertAsync()
        {
            if (_working is null) return;
            long px = (long)_working.Width * _working.Height;
            if (px > ScreenPixels)
            {
                ExportedCode =
                    $"/* Image {_working.Width}x{_working.Height} ({px:N0} px) : au-dela de l'ecran AKA (320x240).\n" +
                    "   Reduis d'abord a une taille raisonnable avant de convertir. */";
                StatusMessage = "Image trop grande : réduis-la avant de convertir.";
                return;
            }
            StatusMessage = "Conversion…";
            var snapshot = (DBitmap)_working.Clone();
            try
            {
                var (fw, fh, count) = CurrentFrameInfo();
                var code = await Task.Run(() =>
                {
                    var sprite = _assetService.BuildSprite(snapshot, "sprite", _fmt, _transparentKey, UseTransparency, fw, fh, count);
                    return _assetService.ExportSpriteToCpp(sprite);
                });
                ExportedCode = code.Length > PreviewCodeCharCap
                    ? code.Substring(0, PreviewCodeCharCap) + "\n\n/* ... apercu tronque. « Exporter C++ » ecrit le fichier complet. */"
                    : code;
                StatusMessage = $"Converti : {_working.Width}×{_working.Height}, {(_fmt == ColorFormat.Bgr565Aka ? "BGR565" : "RGB565")}.";
            }
            catch (Exception ex)
            {
                Log.Error("Conversion échouée.", ex);
                StatusMessage = $"Erreur de conversion : {ex.Message}";
            }
            finally { snapshot.Dispose(); }
        }

        private async Task ExportCppAsync()
        {
            if (_working is null) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exporter en C++",
                Filter = "C++ Header|*.h|C++ Source|*.cpp",
                FileName = "sprite"
            };
            if (dlg.ShowDialog() != true) return;

            StatusMessage = "Génération du fichier…";
            var snapshot = (DBitmap)_working.Clone();
            var name = Path.GetFileNameWithoutExtension(dlg.FileName);
            var (fw, fh, count) = CurrentFrameInfo();
            try
            {
                var code = await Task.Run(() =>
                {
                    var sprite = _assetService.BuildSprite(snapshot, name, _fmt, _transparentKey, UseTransparency, fw, fh, count);
                    return _assetService.ExportSpriteToCpp(sprite);
                });
                await Task.Run(() => File.WriteAllText(dlg.FileName, code));
                StatusMessage = $"Exporté dans {dlg.FileName}";
            }
            catch (Exception ex)
            {
                Log.Error("Export C++ échoué.", ex);
                StatusMessage = $"Erreur export : {ex.Message}";
            }
            finally { snapshot.Dispose(); }
        }

        private void SaveProject()
        {
            if (_working is null) return;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Enregistrer le projet sprite (ré-éditable)",
                Filter = $"Projet sprite (*{AssetService.SpriteProjectExt})|*{AssetService.SpriteProjectExt}",
                FileName = "sprite" + AssetService.SpriteProjectExt
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var (fw, fh, count) = CurrentFrameInfo();
                var sprite = _assetService.BuildSprite(_working,
                    Path.GetFileNameWithoutExtension(dlg.FileName), _fmt, _transparentKey, UseTransparency, fw, fh, count);
                _assetService.SaveSprite(sprite, dlg.FileName);
                StatusMessage = $"Projet enregistré : {dlg.FileName}";
            }
            catch (Exception ex)
            {
                Log.Error("Sauvegarde projet sprite échouée.", ex);
                StatusMessage = $"Erreur d'enregistrement : {ex.Message}";
            }
        }

        private void OpenProject()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Ouvrir un projet sprite",
                Filter = $"Projet sprite (*{AssetService.SpriteProjectExt})|*{AssetService.SpriteProjectExt}"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var sprite = _assetService.LoadSprite(dlg.FileName);
                if (sprite.PixelData is null) throw new InvalidDataException("Données pixel absentes.");
                _fmt = sprite.ColorFormat;
                _transparentKey = sprite.TransparentKey;
                UseTransparency = sprite.UseTransparency;
                OnPropertyChanged(nameof(TransparentKeyHex));
                var bmp = _assetService.UnpackToBitmap(sprite.PixelData, sprite.Width, sprite.Height,
                    sprite.ColorFormat, sprite.UseTransparency, sprite.TransparentKey);
                SetWorking(bmp, keepOriginal: true);
                FrameWidth = sprite.FrameWidth > 0 ? sprite.FrameWidth : sprite.Width;
                FrameHeight = sprite.FrameHeight > 0 ? sprite.FrameHeight : sprite.Height;
                _isSheet = sprite.FrameCount > 1
                    || sprite.FrameWidth < sprite.Width || sprite.FrameHeight < sprite.Height;
                OnPropertyChanged(nameof(FrameInfo));
                StatusMessage = $"Projet ouvert : {sprite.Name} ({sprite.Width}×{sprite.Height}).";
            }
            catch (Exception ex)
            {
                Log.Error("Ouverture projet sprite échouée.", ex);
                StatusMessage = $"Erreur d'ouverture : {ex.Message}";
            }
        }

        private void PickTransparentFromSelected()
        {
            var col = AssetService.Unpack(SelectedColor, ColorFormat.Rgb565Std);
            _transparentKey = AssetService.Pack(DColor.FromArgb(col.R, col.G, col.B), _fmt);
            OnPropertyChanged(nameof(TransparentKeyHex));
            StatusMessage = $"Couleur-clé de transparence = {TransparentKeyHex}";
        }

        // ── Peinture pixel ───────────────────────────────────────────────────────────

        public void PaintPixel(int x, int y)
        {
            if (_working is null || SelectionMode) return;
            if (x < 0 || y < 0 || x >= _working.Width || y >= _working.Height) return;

            DColor c = PaintingTransparent
                ? DColor.FromArgb(0, 0, 0, 0)
                : ColorFromSelected();

            _working.SetPixel(x, y, c);
            PaintPreviewPixel(x, y, c);
        }

        private DColor ColorFromSelected()
        {
            var wc = AssetService.Unpack(SelectedColor, ColorFormat.Rgb565Std);
            return DColor.FromArgb(255, wc.R, wc.G, wc.B);
        }

        // ── Rendu ────────────────────────────────────────────────────────────────────

        private void SetWorking(DBitmap bmp, bool keepOriginal)
        {
            _working?.Dispose();
            _working = bmp;
            _isSheet = false; // toute nouvelle image de travail repart en frame unique
            if (keepOriginal)
            {
                _original?.Dispose();
                _original = (DBitmap)bmp.Clone();
            }
            TargetWidth = bmp.Width;
            TargetHeight = bmp.Height;
            Dimensions = $"{bmp.Width}×{bmp.Height}";
            Zoom = FitZoom(bmp.Width, bmp.Height);
            PreviewBitmap = ToWriteable(bmp);
            Selection = default;

            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(FrameInfo));
            CropCommand.NotifyCanExecuteChanged();
            ResizeCommand.NotifyCanExecuteChanged();
            RevertCommand.NotifyCanExecuteChanged();
            ConvertCommand.NotifyCanExecuteChanged();
            ExportCppCommand.NotifyCanExecuteChanged();
            SaveProjectCommand.NotifyCanExecuteChanged();
        }

        private static int FitZoom(int w, int h)
        {
            int m = Math.Max(w, h);
            if (m > 256) return 1;
            if (m > 96) return 2;
            if (m > 48) return 4;
            if (m > 24) return 8;
            return 16;
        }

        private static WriteableBitmap ToWriteable(DBitmap bmp)
        {
            // Passage par un PNG en mémoire : conversion fiable (alpha préservé),
            // sans dépendre du stride/format de LockBits.
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var decoder = new PngBitmapDecoder(ms,
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            var pbgra = new FormatConvertedBitmap(frame, PixelFormats.Pbgra32, null, 0);
            // Pas de Freeze : la preview doit rester modifiable (retouche pixel).
            return new WriteableBitmap(pbgra);
        }

        private void PaintPreviewPixel(int x, int y, DColor c)
        {
            if (PreviewBitmap is null) return;
            // Pbgra32 : octets B,G,R,A. Pour alpha 0/255, premultiplié = direct.
            var buf = new byte[] { c.B, c.G, c.R, c.A };
            PreviewBitmap.WritePixels(new Int32Rect(x, y, 1, 1), buf, 4, 0);
        }
    }
}
