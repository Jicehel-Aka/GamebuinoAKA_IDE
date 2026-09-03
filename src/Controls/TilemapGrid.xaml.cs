using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GamebuinoAKA.IDE.Controls
{
    
    /// <summary>
    /// Dual-mode control:
    ///   - Tileset picker mode  (TilesetSource set)  → shows the tileset with selection highlight
    ///   - Map editor mode      (MapPreviewSource set) → shows the map with grid overlay
    /// </summary>
    public partial class TilemapGrid : UserControl
    {
        // ── Dependency Properties ──────────────────────────────────────────────────
    
        public static readonly DependencyProperty TilesetSourceProperty =
            DependencyProperty.Register(nameof(TilesetSource), typeof(WriteableBitmap),
                typeof(TilemapGrid), new PropertyMetadata(null, OnRenderPropertyChanged));
    
        public static readonly DependencyProperty MapPreviewSourceProperty =
            DependencyProperty.Register(nameof(MapPreviewSource), typeof(WriteableBitmap),
                typeof(TilemapGrid), new PropertyMetadata(null, OnRenderPropertyChanged));
    
        public static readonly DependencyProperty TileWidthProperty =
            DependencyProperty.Register(nameof(TileWidth), typeof(int),
                typeof(TilemapGrid), new PropertyMetadata(16, OnRenderPropertyChanged));
    
        public static readonly DependencyProperty TileHeightProperty =
            DependencyProperty.Register(nameof(TileHeight), typeof(int),
                typeof(TilemapGrid), new PropertyMetadata(16, OnRenderPropertyChanged));
    
        public static readonly DependencyProperty MapColumnsProperty =
            DependencyProperty.Register(nameof(MapColumns), typeof(int),
                typeof(TilemapGrid), new PropertyMetadata(20, OnRenderPropertyChanged));
    
        public static readonly DependencyProperty MapRowsProperty =
            DependencyProperty.Register(nameof(MapRows), typeof(int),
                typeof(TilemapGrid), new PropertyMetadata(15, OnRenderPropertyChanged));
    
        public static readonly DependencyProperty SelectedTileIndexProperty =
            DependencyProperty.Register(nameof(SelectedTileIndex), typeof(int),
                typeof(TilemapGrid), new PropertyMetadata(0, OnRenderPropertyChanged));
    
        public static readonly DependencyProperty SelectTileCommandProperty =
            DependencyProperty.Register(nameof(SelectTileCommand), typeof(ICommand),
                typeof(TilemapGrid), new PropertyMetadata(null));
    
        public static readonly DependencyProperty PaintTileCommandProperty =
            DependencyProperty.Register(nameof(PaintTileCommand), typeof(ICommand),
                typeof(TilemapGrid), new PropertyMetadata(null));
    
        // ── Properties ────────────────────────────────────────────────────────────
    
        public WriteableBitmap? TilesetSource
        {
            get => (WriteableBitmap?)GetValue(TilesetSourceProperty);
            set => SetValue(TilesetSourceProperty, value);
        }
        public WriteableBitmap? MapPreviewSource
        {
            get => (WriteableBitmap?)GetValue(MapPreviewSourceProperty);
            set => SetValue(MapPreviewSourceProperty, value);
        }
        public int TileWidth { get => (int)GetValue(TileWidthProperty); set => SetValue(TileWidthProperty, value); }
        public int TileHeight { get => (int)GetValue(TileHeightProperty); set => SetValue(TileHeightProperty, value); }
        public int MapColumns { get => (int)GetValue(MapColumnsProperty); set => SetValue(MapColumnsProperty, value); }
        public int MapRows { get => (int)GetValue(MapRowsProperty); set => SetValue(MapRowsProperty, value); }
        public int SelectedTileIndex { get => (int)GetValue(SelectedTileIndexProperty); set => SetValue(SelectedTileIndexProperty, value); }
        public ICommand? SelectTileCommand { get => (ICommand?)GetValue(SelectTileCommandProperty); set => SetValue(SelectTileCommandProperty, value); }
        public ICommand? PaintTileCommand { get => (ICommand?)GetValue(PaintTileCommandProperty); set => SetValue(PaintTileCommandProperty, value); }
    
        // ── Constructor ───────────────────────────────────────────────────────────
    
        public TilemapGrid()
        {
            InitializeComponent();
            PART_Canvas.MouseLeftButtonDown += OnMousePaint;
            PART_Canvas.MouseMove += OnMousePaint;
            PART_Canvas.MouseLeftButtonDown += OnMouseSelectTile;
        }
    
        // ── Rendering ─────────────────────────────────────────────────────────────
    
        private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TilemapGrid g) g.Render();
        }
    
        private void Render()
        {
            PART_Canvas.Children.Clear();
    
            var source = MapPreviewSource ?? TilesetSource;
            if (source is null) return;
    
            // Background image
            var img = new System.Windows.Controls.Image
            {
                Source = source,
                Width = source.PixelWidth,
                Height = source.PixelHeight,
                Stretch = Stretch.None
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            PART_Canvas.Width = source.PixelWidth;
            PART_Canvas.Height = source.PixelHeight;
            PART_Canvas.Children.Add(img);
            Canvas.SetLeft(img, 0);
            Canvas.SetTop(img, 0);
    
            // Draw grid lines
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 100, 100, 180)), 0.5);
            for (int c = 0; c <= source.PixelWidth / TileWidth; c++)
            {
                var line = new Line
                {
                    X1 = c * TileWidth, Y1 = 0,
                    X2 = c * TileWidth, Y2 = source.PixelHeight,
                    Stroke = gridPen.Brush, StrokeThickness = 0.5
                };
                PART_Canvas.Children.Add(line);
            }
            for (int r = 0; r <= source.PixelHeight / TileHeight; r++)
            {
                var line = new Line
                {
                    X1 = 0, Y1 = r * TileHeight,
                    X2 = source.PixelWidth, Y2 = r * TileHeight,
                    Stroke = gridPen.Brush, StrokeThickness = 0.5
                };
                PART_Canvas.Children.Add(line);
            }
    
            // Highlight selected tile (tileset mode)
            if (TilesetSource != null)
            {
                int cols = TileWidth > 0 ? TilesetSource.PixelWidth / TileWidth : 1;
                int selCol = SelectedTileIndex % cols;
                int selRow = SelectedTileIndex / cols;
                var highlight = new Rectangle
                {
                    Width = TileWidth, Height = TileHeight,
                    Stroke = new SolidColorBrush(Color.FromRgb(124, 92, 216)),
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(60, 124, 92, 216))
                };
                PART_Canvas.Children.Add(highlight);
                Canvas.SetLeft(highlight, selCol * TileWidth);
                Canvas.SetTop(highlight, selRow * TileHeight);
            }
        }
    
        // ── Mouse input ───────────────────────────────────────────────────────────
    
        private void OnMousePaint(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || MapPreviewSource is null || PaintTileCommand is null) return;
            var pos = e.GetPosition(PART_Canvas);
            int col = (int)(pos.X / TileWidth);
            int row = (int)(pos.Y / TileHeight);
            if (PaintTileCommand.CanExecute((col, row)))
                PaintTileCommand.Execute((col, row));
        }
    
        private void OnMouseSelectTile(object sender, MouseButtonEventArgs e)
        {
            if (TilesetSource is null || SelectTileCommand is null) return;
            var pos = e.GetPosition(PART_Canvas);
            int col = (int)(pos.X / TileWidth);
            int row = (int)(pos.Y / TileHeight);
            int cols = TileWidth > 0 ? TilesetSource.PixelWidth / TileWidth : 1;
            int idx = row * cols + col;
            if (SelectTileCommand.CanExecute(idx))
                SelectTileCommand.Execute(idx);
        }
    }
}
