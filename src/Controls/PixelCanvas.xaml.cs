using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GamebuinoAKA.IDE.Controls
{
    
    public partial class PixelCanvas : UserControl
    {
        public static readonly DependencyProperty SpriteSourceProperty =
            DependencyProperty.Register(nameof(SpriteSource), typeof(WriteableBitmap),
                typeof(PixelCanvas), new PropertyMetadata(null, OnSpriteSourceChanged));
    
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(int),
                typeof(PixelCanvas), new PropertyMetadata(4, OnZoomChanged));
    
        public static readonly DependencyProperty PaintCommandProperty =
            DependencyProperty.Register(nameof(PaintCommand), typeof(ICommand),
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
    
        public PixelCanvas()
        {
            InitializeComponent();
            PART_Image.MouseLeftButtonDown += OnMousePaint;
            PART_Image.MouseMove += OnMousePaint;
        }
    
        private static void OnSpriteSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PixelCanvas c) c.UpdateDisplay();
        }
    
        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
                return;
            }
    
            int z = Math.Max(1, Zoom);
            PART_Image.Source = SpriteSource;
            PART_Image.Width = SpriteSource.PixelWidth * z;
            PART_Image.Height = SpriteSource.PixelHeight * z;
        }
    
        private void OnMousePaint(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (SpriteSource is null || PaintCommand is null) return;
    
            int z = Math.Max(1, Zoom);
            var pos = e.GetPosition(PART_Image);
            int px = (int)(pos.X / z);
            int py = (int)(pos.Y / z);
    
            if (px < 0 || py < 0 || px >= SpriteSource.PixelWidth || py >= SpriteSource.PixelHeight) return;
    
            if (PaintCommand.CanExecute(new Point(px, py)))
                PaintCommand.Execute(new Point(px, py));
        }
    }
}
