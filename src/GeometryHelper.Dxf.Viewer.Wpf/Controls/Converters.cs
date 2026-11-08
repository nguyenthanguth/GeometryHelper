using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>Shows an element for true and collapses it for false, or the other way round with <see cref="Invert"/>.</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>Gets or sets whether false shows the element and true collapses it.</summary>
        public bool Invert { get; set; }

        /// <summary>Turns a flag into a visibility.</summary>
        /// <param name="value">The flag; anything else counts as false.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Visible or Collapsed.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool on = value is bool flag && flag;
            return on != Invert ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Not supported: the flag is read, never written back.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Nothing; it throws.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Turns a colour given as 0xRRGGBB, as <see cref="CadLayerInfo.Rgb"/> gives it, into a brush: the swatch of a layer.</summary>
    public sealed class RgbBrushConverter : IValueConverter
    {
        /// <summary>Turns a colour into a frozen brush.</summary>
        /// <param name="value">The colour as an integer 0xRRGGBB; anything else gives a transparent brush.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>The brush.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is int rgb))
            {
                return Brushes.Transparent;
            }

            var brush = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            brush.Freeze();
            return brush;
        }

        /// <summary>Not supported: the colour is read, never written back.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Nothing; it throws.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
