using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GamebuinoAKA.IDE.Services;

namespace GamebuinoAKA.IDE.Converters
{
    
    /// <summary>bool → Visibility (true=Visible, false=Collapsed)</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            value is Visibility.Visible;
    }
    
    /// <summary>bool → bool (negation)</summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is bool b ? !b : false;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            value is bool b ? !b : false;
    }
    
    /// <summary>int == 0 → Visible, else Collapsed</summary>
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is int i && i == 0 ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotSupportedException();
    }
    
    /// <summary>not null → Visible</summary>
    public class NotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value is not null ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotSupportedException();
    }
    
    /// <summary>non-empty string → Visible</summary>
    public class NotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            !string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotSupportedException();
    }
    
    /// <summary>ushort RGB565 → System.Windows.Media.Color</summary>
    public class Rgb565ToColorConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is ushort rgb565)
            {
                var col = AssetService.Rgb565ToColor(rgb565);
                return Color.FromRgb(col.R, col.G, col.B);
            }
            return Colors.Black;
        }
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            throw new NotSupportedException();
    }
    
    /// <summary>Equality converter — returns true if value.ToString() == ConverterParameter</summary>
    public class EqualityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c) =>
            value?.ToString() == p?.ToString();
    
        public object ConvertBack(object value, Type t, object p, CultureInfo c) =>
            value is true ? p?.ToString() ?? string.Empty : Binding.DoNothing;
    }
}
