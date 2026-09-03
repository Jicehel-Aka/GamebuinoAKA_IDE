using System;
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
    public class TilemapEditorViewModel : ObservableObject
    {
        private readonly AssetService _assetService;

        private TilemapAsset? _currentTilemap;
        public TilemapAsset? CurrentTilemap
        {
            get => _currentTilemap;
            set => SetProperty(ref _currentTilemap, value);
        }

        private WriteableBitmap? _tilesetBitmap;
        public WriteableBitmap? TilesetBitmap
        {
            get => _tilesetBitmap;
            set => SetProperty(ref _tilesetBitmap, value);
        }

        private WriteableBitmap? _mapPreviewBitmap;
        public WriteableBitmap? MapPreviewBitmap
        {
            get => _mapPreviewBitmap;
            set => SetProperty(ref _mapPreviewBitmap, value);
        }

        private int _selectedTileIndex;
        public int SelectedTileIndex
        {
            get => _selectedTileIndex;
            set => SetProperty(ref _selectedTileIndex, value);
        }

        private int _activeLayer;
        public int ActiveLayer
        {
            get => _activeLayer;
            set => SetProperty(ref _activeLayer, value);
        }

        private string _statusMessage = "Importez un tileset pour commencer";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _exportedCode = string.Empty;
        public string ExportedCode
        {
            get => _exportedCode;
            set => SetProperty(ref _exportedCode, value);
        }

        private int _mapColumns = 20;
        public int MapColumns
        {
            get => _mapColumns;
            set => SetProperty(ref _mapColumns, value);
        }

        private int _mapRows = 15;
        public int MapRows
        {
            get => _mapRows;
            set => SetProperty(ref _mapRows, value);
        }

        private int _tileWidth = 16;
        public int TileWidth
        {
            get => _tileWidth;
            set => SetProperty(ref _tileWidth, value);
        }

        private int _tileHeight = 16;
        public int TileHeight
        {
            get => _tileHeight;
            set => SetProperty(ref _tileHeight, value);
        }

        public ICommand PaintTileRelayCommand { get; }
        public ICommand SelectTileCommand { get; }
        public ICommand ImportTilesetCommand { get; }
        public ICommand ApplyMapSizeCommand { get; }
        public ICommand ExportCodeCommand { get; }

        public string[] LayerNames { get; } = new[] { "Background", "Foreground" };

        public TilemapEditorViewModel(AssetService assetService)
        {
            _assetService = assetService;
            PaintTileRelayCommand = new RelayCommand<(int col, int row)>(t => PaintTile(t.col, t.row));
            SelectTileCommand = new RelayCommand<int>(idx => SelectedTileIndex = idx);
            ImportTilesetCommand = new AsyncRelayCommand(ImportTilesetAsync);
            ApplyMapSizeCommand = new RelayCommand(ApplyMapSize);
            ExportCodeCommand = new RelayCommand(ExportCode);
        }

        private async Task ImportTilesetAsync()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importer un tileset",
                Filter = "Images PNG|*.png"
            };
            if (dlg.ShowDialog() != true) return;

            StatusMessage = "Chargement du tileset...";
            try
            {
                CurrentTilemap = await _assetService.ImportTilesetAsync(
                    dlg.FileName, TileWidth, TileHeight);
                CurrentTilemap.MapColumns = MapColumns;
                CurrentTilemap.MapRows = MapRows;
                CurrentTilemap.InitializeLayers();
                RenderTileset();
                RenderMapPreview();
                StatusMessage = $"Tileset chargé : {CurrentTilemap.TilesetColumns}×{CurrentTilemap.TilesetRows} tuiles";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        private void ApplyMapSize()
        {
            if (CurrentTilemap is null) return;
            CurrentTilemap.MapColumns = MapColumns;
            CurrentTilemap.MapRows = MapRows;
            CurrentTilemap.InitializeLayers();
            RenderMapPreview();
        }

        public void PaintTile(int col, int row)
        {
            if (CurrentTilemap is null) return;
            if (col < 0 || row < 0 || col >= CurrentTilemap.MapColumns || row >= CurrentTilemap.MapRows) return;

            var layer = ActiveLayer == 0 ? CurrentTilemap.BackgroundLayer : CurrentTilemap.ForegroundLayer;
            layer[row * CurrentTilemap.MapColumns + col] = (byte)SelectedTileIndex;
            RenderMapPreview();
        }

        private void ExportCode()
        {
            if (CurrentTilemap is null) return;
            ExportedCode = _assetService.ExportTilemapToCpp(CurrentTilemap);

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exporter la tilemap en C++",
                Filter = "C++ Header|*.h",
                FileName = (CurrentTilemap.Name ?? "tilemap") + "_map"
            };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, ExportedCode);
            StatusMessage = $"Exporté dans {dlg.FileName}";
        }

        private void RenderTileset()
        {
            if (CurrentTilemap?.TilesetPixels is null) return;
            var tw = CurrentTilemap.TilesetColumns * CurrentTilemap.TileWidth;
            var th = CurrentTilemap.TilesetRows * CurrentTilemap.TileHeight;
            TilesetBitmap = Rgb565ToBitmap(CurrentTilemap.TilesetPixels, tw, th);
        }

        private void RenderMapPreview()
        {
            if (CurrentTilemap?.TilesetPixels is null) return;

            int mapW = CurrentTilemap.MapPixelWidth;
            int mapH = CurrentTilemap.MapPixelHeight;
            int tw = CurrentTilemap.TileWidth;
            int th = CurrentTilemap.TileHeight;
            int tsW = CurrentTilemap.TilesetColumns * tw;

            var pixels = new int[mapW * mapH];

            for (int layer = 0; layer < 2; layer++)
            {
                var data = layer == 0
                    ? CurrentTilemap.BackgroundLayer
                    : CurrentTilemap.ForegroundLayer;

                for (int row = 0; row < CurrentTilemap.MapRows; row++)
                {
                    for (int col = 0; col < CurrentTilemap.MapColumns; col++)
                    {
                        int tileIdx = data[row * CurrentTilemap.MapColumns + col];
                        if (tileIdx == 0 && layer == 1) continue;

                        int srcTileCol = tileIdx % CurrentTilemap.TilesetColumns;
                        int srcTileRow = tileIdx / CurrentTilemap.TilesetColumns;

                        for (int py = 0; py < th; py++)
                        {
                            for (int px = 0; px < tw; px++)
                            {
                                int srcX = srcTileCol * tw + px;
                                int srcY = srcTileRow * th + py;
                                int srcIdx = srcY * tsW + srcX;
                                if (srcIdx >= CurrentTilemap.TilesetPixels.Length) continue;

                                var c = AssetService.Rgb565ToColor(CurrentTilemap.TilesetPixels[srcIdx]);
                                int dstX = col * tw + px;
                                int dstY = row * th + py;
                                pixels[dstY * mapW + dstX] = (c.R << 16) | (c.G << 8) | c.B;
                            }
                        }
                    }
                }
            }

            var bmp = new WriteableBitmap(mapW, mapH, 96, 96, PixelFormats.Bgr32, null);
            bmp.WritePixels(new Int32Rect(0, 0, mapW, mapH), pixels, mapW * 4, 0);
            MapPreviewBitmap = bmp;
        }

        private static WriteableBitmap Rgb565ToBitmap(ushort[] data, int w, int h)
        {
            var pixels = new int[w * h];
            for (int i = 0; i < pixels.Length && i < data.Length; i++)
            {
                var c = AssetService.Rgb565ToColor(data[i]);
                pixels[i] = (c.R << 16) | (c.G << 8) | c.B;
            }
            var bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            bmp.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
            return bmp;
        }
    }
}
