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
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Turns the key of an icon of the icon set, as GhIconLayers, into its drawing: the icon of a row of the command line's
    /// AutoComplete list or of the application menu. A text that is no key of the set is read as path data, as
    /// "M4 4h16v16H4z"; null, or a text that reads as neither, gives nothing.
    /// </summary>
    public sealed class IconKeyConverter : IValueConverter
    {
        private static ResourceDictionary _icons;

        /// <summary>Finds the icon of a key.</summary>
        /// <param name="value">The key.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>The geometry, or null.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is string key) || key.Length == 0)
            {
                return null;
            }

            _icons = _icons
                ?? new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/GeometryHelper.Dxf.Viewer.Wpf;component/Themes/Icons.xaml",
                        UriKind.Absolute
                    ),
                };

            if (_icons.Contains(key))
            {
                return _icons[key];
            }

            try
            {
                return System.Windows.Media.Geometry.Parse(key);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>Not supported: the key is read, never written back.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Nothing; it throws.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
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
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Turns where a value lies along a picture, 0 at its start to 1 at its end, into the offset of its marker: the fraction
    /// times the length, plus a shift that centres the marker. With the parameter "218 -7", 0.5 gives 102.
    /// </summary>
    public sealed class FractionToOffsetConverter : IValueConverter
    {
        /// <summary>Turns a fraction into an offset.</summary>
        /// <param name="value">The fraction, a double; anything else gives 0.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">The length and the shift, invariant numbers apart by a space, as "218 -7".</param>
        /// <param name="culture">Not used.</param>
        /// <returns>The offset, a double.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string[] parts = (parameter as string ?? string.Empty).Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries
            );
            double length =
                parts.Length > 0
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double l)
                    ? l
                    : 0.0;
            double shift =
                parts.Length > 1
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double s)
                    ? s
                    : 0.0;
            return (value is double fraction ? fraction : 0.0) * length + shift;
        }

        /// <summary>Not supported: the offset is read, never written back.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Nothing; it throws.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <summary>
    /// Ticks a radio button whose parameter is the value of a text property, as the Paragraph dialog's Left, Centre, Right and
    /// Decimal; ticking it sets the property to its parameter.
    /// </summary>
    public sealed class TextEqualsConverter : IValueConverter
    {
        /// <summary>Whether the value is the parameter.</summary>
        /// <param name="value">The value.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">The text of the button.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>True where they are the same text.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

        /// <summary>The parameter where the button was ticked; nothing where it was cleared.</summary>
        /// <param name="value">Whether it is ticked.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">The text of the button.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>The parameter, or <see cref="Binding.DoNothing"/>.</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool on && on ? parameter : Binding.DoNothing;
    }
}
