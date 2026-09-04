using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace GamebuinoAKA.IDE.Controls
{
    public partial class PixelCanvas : UserControl
    {
        public static readonly DependencyProperty SpriteSourceProperty =
            DependencyProperty.Register(nameof(SpriteSource), typeof(WriteableBitmap),
                typeof(PixelCanvas), new PropertyMetadata(null, OnVisualChanged));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(int),
                typeof(PixelCanvas), new PropertyMetadata(4, OnVisualChanged));

        public static readonly DependencyProperty PaintCommandProperty =
            DependencyProperty.Register(nameof(PaintCommand), typeof(ICommand),
                typeof(PixelCanvas), new PropertyMetadata(null));

        // Mode sélection rectangulaire (sinon : peinture).
        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(nameof(SelectionMode), typeof(bool),
                typeof(PixelCanvas), new PropertyMetadata(false));

        // Sélection en COORDONNÉES IMAGE (X,Y,Width,Height). Two-way par défaut.
        public static readonly DependencyProperty SelectionProperty =
            DependencyProperty.Register(nameof(Selection), typeof(Rect),
                typeof(PixelCanvas),
                new FrameworkPropertyMetadata(default(Rect),
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnVisualChanged));

        // Commande appelée quand une sélection au glisser est terminée (reçoit un Rect image).
        public static readonly DependencyProperty SelectCommandProperty =
            DependencyProperty.Register(nameof(SelectCommand), typeof(ICommand),
                typeof(PixelCanvas), new PropertyMetadata(null));

        public WriteableBitmap? SpriteSource
        {
            get => (WriteableBitmap?)GetValue(SpriteSourceProperty);
            set => SetValue(SpriteSourceProperty, value);
        }
        public int Zoom
        {
            get => (int)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }
        public ICommand? PaintCommand
        {
            get => (ICommand?)GetValue(PaintCommandProperty);
            set => SetValue(PaintCommandProperty, value);
        }
        public bool SelectionMode
        {
            get => (bool)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }
        public Rect Selection
        {
            get => (Rect)GetValue(SelectionProperty);
            set => SetValue(SelectionProperty, value);
        }
        public ICommand? SelectCommand
        {
            get => (ICommand?)GetValue(SelectCommandProperty);
            set => SetValue(SelectCommandProperty, value);
        }

        private bool _dragging;
        private int _startX, _startY;

        public PixelCanvas()
        {
            InitializeComponent();
            PART_Image.MouseLeftButtonDown += OnMouseDown;
            PART_Image.MouseMove += OnMouseMove;
            PART_Image.MouseLeftButtonUp += OnMouseUp;
        }

        private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PixelCanvas c) c.UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (SpriteSource is null)
            {
                PART_Image.Source = null;
                PART_Image.Width = double.NaN;
                PART_Image.Height = double.NaN;
                PART_SelRect.Visibility = Visibility.Collapsed;
                return;
            }

            int z = Math.Max(1, Zoom);
            PART_Image.Source = SpriteSource;
            PART_Image.Width = SpriteSource.PixelWidth * z;
            PART_Image.Height = SpriteSource.PixelHeight * z;
            UpdateSelectionOverlay();
        }

        private void UpdateSelectionOverlay()
        {
            var sel = Selection;
            if (SpriteSource is null || sel.Width < 1 || sel.Height < 1)
            {
                PART_SelRect.Visibility = Visibility.Collapsed;
                return;
            }
            int z = Math.Max(1, Zoom);
            Canvas.SetLeft(PART_SelRect, sel.X * z);
            Canvas.SetTop(PART_SelRect, sel.Y * z);
            PART_SelRect.Width = sel.Width * z;
            PART_SelRect.Height = sel.Height * z;
            PART_SelRect.Visibility = Visibility.Visible;
        }

        private bool TryImagePoint(MouseEventArgs e, out int px, out int py)
        {
            px = py = 0;
            if (SpriteSource is null) return false;
            int z = Math.Max(1, Zoom);
            var pos = e.GetPosition(PART_Image);
            px = (int)(pos.X / z);
            py = (int)(pos.Y / z);
            if (px < 0) px = 0; if (py < 0) py = 0;
            if (px >= SpriteSource.PixelWidth) px = SpriteSource.PixelWidth - 1;
            if (py >= SpriteSource.PixelHeight) py = SpriteSource.PixelHeight - 1;
            return true;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!TryImagePoint(e, out int px, out int py)) return;

            if (SelectionMode)
            {
                _dragging = true;
                _startX = px; _startY = py;
                Selection = new Rect(px, py, 1, 1);
                PART_Image.CaptureMouse();
                UpdateSelectionOverlay();
            }
            else
            {
                Paint(px, py);
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (!TryImagePoint(e, out int px, out int py)) return;

            if (SelectionMode && _dragging)
            {
                int x0 = Math.Min(_startX, px), y0 = Math.Min(_startY, py);
                int x1 = Math.Max(_startX, px), y1 = Math.Max(_startY, py);
                Selection = new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                UpdateSelectionOverlay();
            }
            else if (!SelectionMode)
            {
                Paint(px, py);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SelectionMode && _dragging)
            {
                _dragging = false;
                PART_Image.ReleaseMouseCapture();
                if (SelectCommand?.CanExecute(Selection) == true)
                    SelectCommand.Execute(Selection);
            }
        }

        private void Paint(int px, int py)
        {
            if (PaintCommand is null) return;
            var p = new Point(px, py);
            if (PaintCommand.CanExecute(p)) PaintCommand.Execute(p);
        }
    }
}
