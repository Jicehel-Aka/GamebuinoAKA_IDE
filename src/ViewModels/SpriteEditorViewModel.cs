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

namespace GamebuinoAKA.IDE.ViewModels
{
    public class SpriteEditorViewModel : ObservableObject
    {
        private readonly AssetService _assetService;

        private SpriteAsset? _currentSprite;
        public SpriteAsset? CurrentSprite
        {
            get => _currentSprite;
            set => SetProperty(ref _currentSprite, value);
        }

        private WriteableBitmap? _previewBitmap;
        public WriteableBitmap? PreviewBitmap
        {
            get => _previewBitmap;
            set => SetProperty(ref _previewBitmap, value);
        }

        private int _zoom = 4;
        public int Zoom
        {
            get => _zoom;
            set => SetProperty(ref _zoom, value);
        }

        private int _currentFrame;
        public int CurrentFrame
        {
            get => _currentFrame;
            set => SetProperty(ref _currentFrame, value);
        }

        private ushort _selectedColor = 0xFFFF;
        public ushort SelectedColor
        {
            get => _selectedColor;
            set => SetProperty(ref _selectedColor, value);
        }

        private string _exportedCode = string.Empty;
        public string ExportedCode
        {
            get => _exportedCode;
            set => SetProperty(ref _exportedCode, value);
        }

        private bool _isAnimating;
        public bool IsAnimating
        {
            get => _isAnimating;
            set => SetProperty(ref _isAnimating, value);
        }

        private string _statusMessage = "Importez une image pour commencer";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand PaintPixelRelayCommand { get; }
        public ICommand SelectColorCommand { get; }
        public ICommand ImportImageCommand { get; }
        public ICommand ImportSpritesheetCommand { get; }
        public ICommand ExportCodeCommand { get; }

        public int[] ZoomLevels { get; } = new[] { 1, 2, 4, 8 };

        public ObservableCollection<ushort> Palette { get; } = new ObservableCollection<ushort>(new ushort[]
        {
            0x0000, 0xFFFF, 0xF800, 0x07E0, 0x001F,
            0xFFE0, 0xF81F, 0x07FF, 0x8000, 0x0400,
            0x0010, 0x8410, 0xC618, 0x7BEF, 0x39E7, 0xFFD5
        });

        public SpriteEditorViewModel(AssetService assetService)
        {
            _assetService = assetService;
            PaintPixelRelayCommand = new RelayCommand<Point>(p => PaintPixel((int)p.X, (int)p.Y));
            SelectColorCommand = new RelayCommand<ushort>(color => SelectedColor = color);
            ImportImageCommand = new AsyncRelayCommand(ImportImageAsync);
            ImportSpritesheetCommand = new AsyncRelayCommand(ImportSpritesheetAsync);
            ExportCodeCommand = new RelayCommand(ExportCode);
        }

        private async Task ImportImageAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importer une image sprite",
                Filter = "Images|*.png;*.bmp;*.jpg;*.jpeg",
            };
            if (dlg.ShowDialog() != true) return;

            StatusMessage = "Import en cours...";
            try
            {
                CurrentSprite = await _assetService.ImportSpriteAsync(dlg.FileName);
                RenderPreview();
                ExportedCode = _assetService.ExportSpriteToCpp(CurrentSprite);
                StatusMessage = $"Sprite chargé : {CurrentSprite.Width}×{CurrentSprite.Height} px, {CurrentSprite.FrameCount} frame(s)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur d'import : {ex.Message}";
            }
        }

        private async Task ImportSpritesheetAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importer une spritesheet",
                Filter = "Images|*.png;*.bmp",
            };
            if (dlg.ShowDialog() != true) return;

            int frameW = 16, frameH = 16;

            StatusMessage = "Import spritesheet en cours...";
            try
            {
                CurrentSprite = await _assetService.ImportSpritesheetAsync(dlg.FileName, frameW, frameH);
                RenderPreview();
                ExportedCode = _assetService.ExportSpriteToCpp(CurrentSprite);
                StatusMessage = $"Spritesheet chargée : {CurrentSprite.FrameCount} frame(s) de {frameW}×{frameH}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        private void ExportCode()
        {
            if (CurrentSprite is null) return;
            ExportedCode = _assetService.ExportSpriteToCpp(CurrentSprite);

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exporter en C++",
                Filter = "C++ Header|*.h|C++ Source|*.cpp",
                FileName = CurrentSprite.Name + "_sprite"
            };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, ExportedCode);
            StatusMessage = $"Exporté dans {dlg.FileName}";
        }

        public void PaintPixel(int x, int y)
        {
            if (CurrentSprite?.PixelData is null) return;
            if (x < 0 || y < 0 || x >= CurrentSprite.Width || y >= CurrentSprite.Height) return;

            CurrentSprite.PixelData[y * CurrentSprite.Width + x] = SelectedColor;
            RenderPreview();
        }

        private void RenderPreview()
        {
            if (CurrentSprite?.PixelData is null) return;
            var w = CurrentSprite.Width;
            var h = CurrentSprite.Height;
            var bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            var pixels = new int[w * h];
            for (int i = 0; i < w * h; i++)
            {
                var c = AssetService.Rgb565ToColor(CurrentSprite.PixelData[i]);
                pixels[i] = (c.R << 16) | (c.G << 8) | c.B;
            }
            bmp.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
            PreviewBitmap = bmp;
        }
    }
}
