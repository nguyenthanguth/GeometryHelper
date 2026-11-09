using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The style boxes of the ribbon's Annotate tab: a list that opens where it is clicked, as a combo box of the ribbon does,
    /// drawn with no button of its own, so that the tab's buttons are the commands alone.
    /// </summary>
    public static class StyleBox
    {
        /// <summary>Whether a combo box opens its list when clicked on its box.</summary>
        public static readonly DependencyProperty OpensOnClickProperty =
            DependencyProperty.RegisterAttached("OpensOnClick", typeof(bool), typeof(StyleBox), new PropertyMetadata(false, OnOpensOnClickChanged));

        /// <summary>Gets whether a combo box opens its list when clicked.</summary>
        /// <param name="element">The combo box.</param>
        /// <returns>Whether it does.</returns>
        public static bool GetOpensOnClick(DependencyObject element) => (bool)element.GetValue(OpensOnClickProperty);

        /// <summary>Sets whether a combo box opens its list when clicked.</summary>
        /// <param name="element">The combo box.</param>
        /// <param name="value">Whether it does.</param>
        public static void SetOpensOnClick(DependencyObject element, bool value) => element.SetValue(OpensOnClickProperty, value);

        private static void OnOpensOnClickChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (element is ComboBox box)
            {
                box.PreviewMouseLeftButtonDown -= OnBoxDown;

                if ((bool)e.NewValue)
                {
                    box.PreviewMouseLeftButtonDown += OnBoxDown;
                }
            }
        }

        /// <summary>A click on the closed box opens its list; a click on the list's items, while it is open, is the list's.</summary>
        private static void OnBoxDown(object sender, MouseButtonEventArgs e)
        {
            var box = (ComboBox)sender;

            if (!box.IsDropDownOpen && box.IsEnabled)
            {
                box.IsDropDownOpen = true;
                e.Handled = true;
            }
        }
    }
}
