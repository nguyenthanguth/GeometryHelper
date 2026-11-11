using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The menus of the window made from the items a view model gives (<see cref="CadMenuItemViewModel"/>): the right-click
    /// menu of the DRAWINGS list, the OSNAP menu of the status bar and the Shift+right-click menu of the canvas, dark as the
    /// window, each object snap with its marker beside it.
    /// </summary>
    public static class CadMenus
    {
        private static ResourceDictionary _theme;

        /// <summary>Makes a context menu of items in the window's dark look (GhContextMenu, GhMenuItem), each item's tooltip shown even while it is off.</summary>
        /// <param name="items">The items.</param>
        /// <returns>The menu, not yet open.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the items are null.</exception>
        public static ContextMenu Make(IEnumerable<CadMenuItemViewModel> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            _theme = _theme
                ?? new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/GeometryHelper.Dxf.Viewer.Wpf;component/Themes/Dark.xaml",
                        UriKind.Absolute
                    ),
                };
            var menu = new ContextMenu { Style = (Style)_theme["GhContextMenu"] };
            Fill(menu, items);
            return menu;
        }

        /// <summary>Adds the items to a menu or to an item's own menu, as "Point Filters" opens the six filters.</summary>
        private static void Fill(ItemsControl menu, IEnumerable<CadMenuItemViewModel> items)
        {
            var itemStyle = (Style)_theme["GhMenuItem"];
            var submenuStyle = (Style)_theme["GhSubmenuItem"];
            var separatorStyle = (Style)_theme["GhMenuSeparator"];

            foreach (CadMenuItemViewModel item in items)
            {
                if (item.IsSeparator)
                {
                    menu.Items.Add(new Separator { Style = separatorStyle });
                    continue;
                }

                // The marker beside the words, as AutoCAD's OSNAP menu draws it; the tick in the gutter before both.
                object header = item.Header;

                if (item.Marker != CadSnapKind.None)
                {
                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    row.Children.Add(
                        new Image
                        {
                            Source = MarkerImage(item.Marker),
                            Width = 16,
                            Height = 16,
                            Margin = new Thickness(0, 0, 8, 0),
                            Opacity = item.IsEnabled ? 1.0 : 0.45,
                        }
                    );
                    row.Children.Add(
                        new TextBlock { Text = item.Header, VerticalAlignment = VerticalAlignment.Center }
                    );
                    header = row;
                }

                var menuItem = new MenuItem
                {
                    Style = item.Items.Count > 0 ? submenuStyle : itemStyle,
                    Header = header,
                    Command = item.Command,
                    IsEnabled = item.IsEnabled,
                    IsChecked = item.IsChecked,
                    ToolTip = item.ToolTip,
                    InputGestureText = item.Shortcut,
                };
                Fill(menuItem, item.Items);
                menu.Items.Add(menuItem);
            }
        }

        /// <summary>Gets a snap kind's marker as a picture 16 pixels across, drawn as the canvas draws it on a dark background.</summary>
        /// <param name="kind">The snap kind.</param>
        /// <returns>The picture; an empty one for a kind with no marker.</returns>
        public static ImageSource MarkerImage(CadSnapKind kind)
        {
            var visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                // A clear square keeps the picture 16 across whatever the marker's size.
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 16, 16));
                CadCanvas.DrawMarker(dc, kind, new Point(8, 8), CadCanvas.MarkerPen(0x000000));
            }

            var image = new DrawingImage(visual.Drawing);
            image.Freeze();
            return image;
        }
    }

    /// <summary>Turns a <see cref="CadSnapKind"/> into its marker as a picture, as Drafting Settings shows beside each snap.</summary>
    public sealed class SnapMarkerConverter : IValueConverter
    {
        /// <summary>Turns a snap kind into its marker.</summary>
        /// <param name="value">The snap kind; anything else gives none.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>The picture, or null.</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is CadSnapKind kind ? CadMenus.MarkerImage(kind) : null;

        /// <summary>Not supported: the kind is read, never written back.</summary>
        /// <param name="value">Not used.</param>
        /// <param name="targetType">Not used.</param>
        /// <param name="parameter">Not used.</param>
        /// <param name="culture">Not used.</param>
        /// <returns>Nothing; it throws.</returns>
        /// <exception cref="NotSupportedException">Always.</exception>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
