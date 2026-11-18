using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// Typing in a closed or open font box finds a font by any of its names. The box's own text search only looks at one text
    /// of an entry; here what is typed is matched against the English name and the other names of a TrueType family (typing
    /// "dot" or "돋" finds DotumChe), the letters kept for a second after the last, as the box's own search keeps them.
    /// Set <see cref="IsEnabledProperty"/> on a ComboBox whose items are <see cref="FontItemViewModel"/>.
    /// </summary>
    public static class FontSearch
    {
        private static readonly TimeSpan Keep = TimeSpan.FromSeconds(1);

        /// <summary>The attached property that makes a font box find a font by any of its names as letters are typed.</summary>
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FontSearch),
            new PropertyMetadata(false, OnEnabledChanged)
        );

        private static readonly DependencyProperty TypedProperty = DependencyProperty.RegisterAttached(
            "Typed",
            typeof(string),
            typeof(FontSearch),
            new PropertyMetadata(string.Empty)
        );

        private static readonly DependencyProperty TypedAtProperty = DependencyProperty.RegisterAttached(
            "TypedAt",
            typeof(DateTime),
            typeof(FontSearch),
            new PropertyMetadata(DateTime.MinValue)
        );

        /// <summary>Gets whether the box finds a font by any of its names.</summary>
        /// <param name="element">The box.</param>
        /// <returns>Whether the search is on.</returns>
        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        /// <summary>Sets whether the box finds a font by any of its names.</summary>
        /// <param name="element">The box.</param>
        /// <param name="value">Whether the search is on.</param>
        public static void SetIsEnabled(DependencyObject element, bool value) =>
            element.SetValue(IsEnabledProperty, value);

        private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (!(element is ComboBox box))
            {
                return;
            }

            box.PreviewTextInput -= OnTextInput;

            if ((bool)e.NewValue)
            {
                box.PreviewTextInput += OnTextInput;
            }
        }

        private static void OnTextInput(object sender, TextCompositionEventArgs e)
        {
            var box = (ComboBox)sender;

            if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            string before =
                now - (DateTime)box.GetValue(TypedAtProperty) <= Keep
                    ? (string)box.GetValue(TypedProperty)
                    : string.Empty;
            string typed = before + e.Text;
            FontItemViewModel found = Find(box, typed);

            // Letters that start no name begin again with the last one, as the box's own search does.
            if (found == null && before.Length > 0)
            {
                typed = e.Text;
                found = Find(box, typed);
            }

            box.SetValue(TypedProperty, found == null ? string.Empty : typed);
            box.SetValue(TypedAtProperty, now);

            if (found != null && !ReferenceEquals(found, box.SelectedItem))
            {
                box.SelectedItem = found;
            }

            e.Handled = true;
        }

        /// <summary>The first font whose name or other name starts with what was typed, in the order of the list.</summary>
        private static FontItemViewModel Find(ComboBox box, string typed)
        {
            foreach (object item in box.Items)
            {
                if (item is FontItemViewModel font && font.Matches(typed))
                {
                    return font;
                }
            }

            return null;
        }
    }
}
