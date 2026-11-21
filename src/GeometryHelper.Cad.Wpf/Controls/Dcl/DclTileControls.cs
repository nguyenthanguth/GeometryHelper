using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Cad.Dcl;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The WPF control of one DCL tile in a <see cref="DclDialogWindow"/>: made from the tile, placed at the rectangle the layout
    /// gave it, refreshed from the tile's values before each event is waited for, and reporting what the user does as
    /// <see cref="DclEvent"/>s. A user's change is put into the tile at once, so that <c>get_tile</c> in an action sees it; a
    /// change made by <see cref="Refresh"/> reports nothing.
    /// </summary>
    internal abstract class DclTileView
    {
        /// <summary>The colour of an error tile's text.</summary>
        protected static readonly Brush ErrorText = Frozen(0xFF, 0x8A, 0x7A);

        /// <summary>The colour of a text that is switched off.</summary>
        protected static readonly Brush DisabledText = Frozen(0x6B, 0x74, 0x80);

        /// <summary>The colour of the text of a dialog.</summary>
        protected static readonly Brush Text = Frozen(0xE6, 0xE9, 0xED);

        private protected DclTileView(DclTile tile, DclDialogWindow window)
        {
            Tile = tile;
            Window = window;
        }

        /// <summary>Gets the tile the view shows.</summary>
        internal DclTile Tile { get; }

        /// <summary>Gets the window the view is in.</summary>
        protected DclDialogWindow Window { get; }

        /// <summary>Gets the element that stands at the tile's rectangle.</summary>
        internal abstract FrameworkElement Element { get; }

        /// <summary>Gets the control that takes the keyboard focus for the tile; null where the tile takes none.</summary>
        internal virtual Control FocusTarget => Element as Control;

        /// <summary>Gets whether the tile is on the Tab order.</summary>
        internal virtual bool IsTabStop =>
            FocusTarget != null && !string.Equals(Tile.Get("is_tab_stop"), "false", StringComparison.OrdinalIgnoreCase);

        /// <summary>Makes the view for a tile; null for a tile that draws nothing (a cluster that is not boxed, a spacer).</summary>
        /// <param name="tile">The tile.</param>
        /// <param name="window">The window it will be in.</param>
        /// <returns>The view, or null.</returns>
        internal static DclTileView Create(DclTile tile, DclDialogWindow window)
        {
            switch (tile.Kind)
            {
                case "button":
                    return new ButtonView(tile, window);
                case "edit_box":
                    return new EditView(tile, window);
                case "popup_list":
                    return new PopupView(tile, window);
                case "list_box":
                    return new ListView(tile, window);
                case "toggle":
                    return new ToggleView(tile, window);
                case "radio_button":
                    return new RadioView(tile, window);
                case "text":
                    return new TextView(tile, window);
                case "slider":
                    return new SliderView(tile, window);
                case "image":
                    return new ImageView(tile, window);
                case "image_button":
                    return new ImageButtonView(tile, window);
                case "cluster":
                case "radio_cluster":
                    return tile.IsTrue("boxed") ? new BoxView(tile, window) : null;
                default:
                    return null;
            }
        }

        /// <summary>Puts the element at the tile's rectangle, its corner on whole pixels.</summary>
        /// <param name="rect">The rectangle from the top-left of the dialog's body.</param>
        internal virtual void Place(DclRect rect)
        {
            Canvas.SetLeft(Element, Math.Floor(rect.X));
            Canvas.SetTop(Element, Math.Floor(rect.Y));
            Element.Width = rect.Width;
            Element.Height = rect.Height;
        }

        /// <summary>Shows the tile's values in the control, without reporting anything.</summary>
        internal abstract void Refresh();

        /// <summary>
        /// Gives the label of a tile as WPF reads an access key: <c>&amp;H</c> becomes <c>_H</c>, a <c>_</c> is doubled, and a
        /// <c>mnemonic</c> names the letter that is underlined where the label has no <c>&amp;</c>.
        /// </summary>
        /// <param name="tile">The tile.</param>
        /// <returns>The label.</returns>
        internal static string AccessLabel(DclTile tile)
        {
            string label = tile.Get("label") ?? string.Empty;
            var text = new StringBuilder(label.Length + 2);
            bool marked = false;

            for (int i = 0; i < label.Length; i++)
            {
                char c = label[i];

                if (c == '&' && i + 1 < label.Length)
                {
                    i++;

                    if (label[i] == '&' || marked)
                    {
                        text.Append(label[i] == '_' ? "__" : label[i].ToString());
                    }
                    else
                    {
                        text.Append('_').Append(label[i]);
                        marked = true;
                    }
                }
                else if (c == '&')
                {
                    continue;
                }
                else
                {
                    text.Append(c == '_' ? "__" : c.ToString());
                }
            }

            string mnemonic = tile.Get("mnemonic");

            if (!marked && !string.IsNullOrEmpty(mnemonic))
            {
                int at = text.ToString().IndexOf(mnemonic[0].ToString(), StringComparison.OrdinalIgnoreCase);

                if (at >= 0)
                {
                    text.Insert(at, '_');
                }
            }

            return text.ToString();
        }

        /// <summary>Tells whether a label has an access key.</summary>
        /// <param name="tile">The tile.</param>
        /// <returns>True where its <see cref="AccessLabel"/> has a <c>_</c> that is not doubled.</returns>
        internal static bool HasAccessKey(DclTile tile)
        {
            string label = AccessLabel(tile);

            for (int i = 0; i < label.Length; i++)
            {
                if (label[i] == '_')
                {
                    if (i + 1 < label.Length && label[i + 1] == '_')
                    {
                        i++;
                    }
                    else
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Makes a text of the dialog: one line, in the dialog's colour, as high as a text tile.</summary>
        /// <param name="metrics">The metrics, for the line height.</param>
        /// <returns>The text.</returns>
        protected static TextBlock Line(DclMetrics metrics) =>
            new TextBlock
            {
                Foreground = Text,
                VerticalAlignment = VerticalAlignment.Center,
                LineHeight = metrics.CharHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                TextWrapping = TextWrapping.NoWrap,
            };

        /// <summary>Makes the Label in front of an edit box or a popup list, which gives its box the focus on its access key.</summary>
        /// <param name="tile">The tile, whose label it shows.</param>
        /// <param name="target">The control the access key goes to.</param>
        /// <returns>The label.</returns>
        protected static Label LabelOf(DclTile tile, Control target) =>
            new Label
            {
                Content = AccessLabel(tile),
                Padding = new Thickness(0),
                Foreground = Text,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Target = HasAccessKey(tile) ? target : null,
                Focusable = false,
            };

        /// <summary>Makes a grid for a tile that has a label before its box: the label's width, then the box.</summary>
        /// <param name="labelWidth">The width of the label with its gap.</param>
        /// <returns>The grid.</returns>
        protected static Grid LabelledGrid(double labelWidth)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return grid;
        }

        /// <summary>Gives a number of the tile's attribute as a whole number, the default where it has none.</summary>
        /// <param name="tile">The tile.</param>
        /// <param name="attribute">The attribute.</param>
        /// <param name="otherwise">The default.</param>
        /// <returns>The number.</returns>
        protected static int Integer(DclTile tile, string attribute, int otherwise) =>
            tile.GetInteger(attribute) ?? otherwise;

        /// <summary>Makes a frozen brush of a colour.</summary>
        /// <param name="red">Red.</param>
        /// <param name="green">Green.</param>
        /// <param name="blue">Blue.</param>
        /// <returns>The brush.</returns>
        protected static Brush Frozen(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        /// <summary>Makes a style the window's theme holds.</summary>
        /// <param name="key">The key in Themes/Dcl.xaml.</param>
        /// <returns>The style.</returns>
        protected Style Styled(string key) => (Style)Window.Theme[key];

        /// <summary>Gives the whole numbers of the tile's value, as the indices of a list.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The indices.</returns>
        protected static List<int> IndicesOf(string value) =>
            (value ?? string.Empty)
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Select(word =>
                    int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1
                )
                .Where(n => n >= 0)
                .ToList();

        /// <summary>Gives the text a list of indices is written as in a tile's value.</summary>
        /// <param name="indices">The indices.</param>
        /// <returns>They in ascending order, separated by single spaces.</returns>
        protected static string ValueOf(IEnumerable<int> indices) =>
            string.Join(" ", indices.OrderBy(n => n).Select(n => n.ToString(CultureInfo.InvariantCulture)));

        // ---- button ----

        private sealed class ButtonView : DclTileView
        {
            private readonly Button _button;

            internal ButtonView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                _button = new Button
                {
                    Style = Styled(tile.IsTrue("is_default") ? "DclPrimaryButton" : "DclButton"),
                    Content = AccessLabel(tile),
                    IsDefault = false,
                };
                _button.Click += OnClick;
            }

            internal override FrameworkElement Element => _button;

            internal override void Refresh() => _button.IsEnabled = Tile.IsEnabled;

            private void OnClick(object sender, RoutedEventArgs e)
            {
                if (Tile.Key == null)
                {
                    return;
                }

                // The point of the click in the button, whole pixels; the centre where the key was used.
                Point at = _button.IsMouseOver
                    ? Mouse.GetPosition(_button)
                    : new Point(_button.ActualWidth / 2, _button.ActualHeight / 2);
                Window.Raise(DclEvent.Tile(Tile.Key, "1", 1, (int)Math.Floor(at.X), (int)Math.Floor(at.Y)));
            }
        }

        // ---- image button and image ----

        private sealed class ImageButtonView : DclTileView
        {
            private readonly Button _button;
            private readonly DclPictureView _picture = new DclPictureView();

            internal ImageButtonView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                _button = new Button { Style = Styled("DclImageButton"), Content = _picture };
                _button.Click += (s, e) => Report(1);
                _button.MouseDoubleClick += (s, e) => Report(4);
            }

            internal override FrameworkElement Element => _button;

            internal override void Refresh()
            {
                _button.IsEnabled = Tile.IsEnabled;
                _picture.Set(Tile.Get("color"), Tile.Picture, Tile.IsFlipped);
            }

            private void Report(int reason)
            {
                if (Tile.Key == null)
                {
                    return;
                }

                Point at = _picture.IsMouseOver
                    ? Mouse.GetPosition(_picture)
                    : new Point(_picture.ActualWidth / 2, _picture.ActualHeight / 2);
                Window.Raise(DclEvent.Tile(Tile.Key, "1", reason, (int)Math.Floor(at.X), (int)Math.Floor(at.Y)));
            }
        }

        private sealed class ImageView : DclTileView
        {
            private readonly DclPictureView _picture = new DclPictureView();

            internal ImageView(DclTile tile, DclDialogWindow window)
                : base(tile, window) { }

            internal override FrameworkElement Element => _picture;

            internal override Control FocusTarget => null;

            internal override void Refresh() => _picture.Set(Tile.Get("color"), Tile.Picture, false);
        }

        // ---- edit box ----

        private sealed class EditView : DclTileView
        {
            private readonly Grid _root;
            private readonly Label _label;
            private readonly Border _frame;
            private readonly TextBox _text;
            private readonly PasswordBox _password;

            internal EditView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                double labelWidth = DclLayout.LabelWidth(tile, window.Metrics);
                _root = LabelledGrid(labelWidth);
                _frame = new Border { Style = Styled("DclEditFrame"), Padding = new Thickness(4, 0, 4, 0) };
                Grid.SetColumn(_frame, 1);
                Control input;

                if (tile.Get("password_char") != null)
                {
                    _password = new PasswordBox
                    {
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(0),
                        Foreground = Text,
                        CaretBrush = Text,
                        SelectionBrush = (Brush)window.Theme["GhAccent"],
                        VerticalContentAlignment = VerticalAlignment.Center,
                        MaxLength = Integer(tile, "edit_limit", 132),
                        FocusVisualStyle = null,
                    };
                    _password.PasswordChanged += (s, e) => Changed(_password.Password);
                    input = _password;
                }
                else
                {
                    _text = new TextBox
                    {
                        Style = (Style)window.Theme["GhTextBox"],
                        FontFamily = window.FontFamily,
                        FontSize = window.FontSize,
                        Padding = new Thickness(0),
                        MaxLength = Integer(tile, "edit_limit", 132),
                        SelectionOpacity = 0.4,
                        AcceptsReturn = false,
                    };
                    _text.TextChanged += (s, e) => Changed(_text.Text);
                    input = _text;
                }

                input.GotKeyboardFocus += OnGotFocus;
                input.LostKeyboardFocus += OnLostFocus;
                _frame.Child = input;
                Input = input;

                if (labelWidth > 0)
                {
                    _label = LabelOf(tile, input);
                    Grid.SetColumn(_label, 0);
                    _root.Children.Add(_label);
                }

                _root.Children.Add(_frame);
            }

            /// <summary>Gets the box the user types in.</summary>
            internal Control Input { get; }

            internal override FrameworkElement Element => _root;

            internal override Control FocusTarget => Input;

            private string Current => _text != null ? _text.Text : _password.Password;

            internal override void Refresh()
            {
                bool enabled = Tile.IsEnabled;
                _frame.IsEnabled = enabled;

                if (_label != null)
                {
                    _label.Foreground = enabled ? Text : DisabledText;
                }

                string value = Tile.Value ?? string.Empty;

                if (!string.Equals(Current, value, StringComparison.Ordinal))
                {
                    if (_text != null)
                    {
                        _text.Text = value;
                    }
                    else
                    {
                        _password.Password = value;
                    }
                }

                // mode_tile 3: the text of the box is selected and the box has the focus.
                if (Tile.Key != null && string.Equals(Window.Dialog.SelectTextKey, Tile.Key, StringComparison.Ordinal))
                {
                    Window.Dialog.SelectTextKey = null;
                    Input.Focus();
                    SelectAll();
                }
            }

            private void SelectAll()
            {
                if (_text != null)
                {
                    _text.SelectAll();
                }
                else
                {
                    _password.SelectAll();
                }
            }

            private void Changed(string text)
            {
                if (Window.IsRefreshing || Tile.Key == null)
                {
                    return;
                }

                // The text is the tile's value as it is typed, and the box owes its action when the focus leaves it (D340).
                Tile.Value = text;
                Window.Dialog.PendingEditKey = Tile.Key;
            }

            private void OnGotFocus(object sender, KeyboardFocusChangedEventArgs e)
            {
                Window.NoteFocus(this);

                // Tab, a mnemonic and the program select the text, as Windows does; the mouse puts the caret where it is clicked.
                if (Mouse.LeftButton != MouseButtonState.Pressed)
                {
                    SelectAll();
                }
            }

            private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e)
            {
                var gone = e.NewFocus as DependencyObject;

                if (
                    gone == null
                    || Tile.Key == null
                    || Window.IsReleased
                    || !string.Equals(Window.Dialog.PendingEditKey, Tile.Key, StringComparison.Ordinal)
                )
                {
                    return;
                }

                Window.Dialog.PendingEditKey = null;
                Window.Raise(DclEvent.Tile(Tile.Key, Current, 2));
            }
        }

        // ---- popup list ----

        private sealed class PopupView : DclTileView
        {
            private readonly Grid _root;
            private readonly Label _label;
            private readonly ComboBox _combo;

            internal PopupView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                double labelWidth = DclLayout.LabelWidth(tile, window.Metrics);
                _root = LabelledGrid(labelWidth);
                _combo = new ComboBox { Style = Styled("DclCombo"), IsEditable = false };
                Grid.SetColumn(_combo, 1);
                _combo.SelectionChanged += OnSelectionChanged;
                _combo.GotKeyboardFocus += (s, e) => Window.NoteFocus(this);

                if (labelWidth > 0)
                {
                    _label = LabelOf(tile, _combo);
                    Grid.SetColumn(_label, 0);
                    _root.Children.Add(_label);
                }

                _root.Children.Add(_combo);
            }

            internal override FrameworkElement Element => _root;

            internal override Control FocusTarget => _combo;

            internal override void Refresh()
            {
                bool enabled = Tile.IsEnabled;
                _combo.IsEnabled = enabled;

                if (_label != null)
                {
                    _label.Foreground = enabled ? Text : DisabledText;
                }

                if (!_combo.Items.Cast<object>().Select(item => (string)item).SequenceEqual(Tile.Items))
                {
                    _combo.Items.Clear();

                    foreach (string item in Tile.Items)
                    {
                        _combo.Items.Add(item);
                    }
                }

                List<int> chosen = IndicesOf(Tile.Value);
                int index = chosen.Count > 0 && chosen[0] < _combo.Items.Count ? chosen[0] : -1;

                if (_combo.SelectedIndex != index)
                {
                    _combo.SelectedIndex = index;
                }
            }

            private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (Window.IsRefreshing || Tile.Key == null || _combo.SelectedIndex < 0)
                {
                    return;
                }

                string value = _combo.SelectedIndex.ToString(CultureInfo.InvariantCulture);
                Tile.Value = value;
                Window.Raise(DclEvent.Tile(Tile.Key, value, 1));
            }
        }

        // ---- list box ----

        private sealed class ListView : DclTileView
        {
            private readonly Grid _root;
            private readonly TextBlock _label;
            private readonly ListBox _list;
            private readonly double[] _stops;
            private readonly bool _truncate;

            internal ListView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                _root = new Grid();
                string label = tile.Get("label");
                bool labelled = !string.IsNullOrEmpty(label);
                _root.RowDefinitions.Add(
                    new RowDefinition
                    {
                        Height = labelled ? new GridLength(window.Metrics.CharHeight) : new GridLength(0),
                    }
                );
                _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                if (labelled)
                {
                    _label = Line(window.Metrics);
                    _label.Text = DclLayout.StripAccess(label);
                    Grid.SetRow(_label, 0);
                    _root.Children.Add(_label);
                }

                _list = new ListBox
                {
                    Style = Styled("DclList"),
                    SelectionMode = tile.IsTrue("multiple_select") ? SelectionMode.Extended : SelectionMode.Single,
                };
                Grid.SetRow(_list, 1);

                // AutoCAD 2021 (GUI g1): a labelled list leaves one pixel under the list inside its tile (68 = 13 + 54 + 1)
                _list.Margin = new Thickness(0, 0, 0, labelled ? 1 : 0);
                _list.SelectionChanged += OnSelectionChanged;
                _list.MouseDoubleClick += OnDoubleClick;
                _list.GotKeyboardFocus += (s, e) => Window.NoteFocus(this);
                _root.Children.Add(_list);
                _stops = StopsOf(tile.Get("tabs"), window.Metrics.CharWidth);
                _truncate = tile.IsTrue("tab_truncate");
            }

            internal override FrameworkElement Element => _root;

            internal override Control FocusTarget => _list;

            /// <summary>Gives the tab stops of a <c>tabs</c> string in pixels; a first stop of 0 is the start of the text and no stop.</summary>
            internal static double[] StopsOf(string tabs, double charWidth)
            {
                List<double> stops = (tabs ?? string.Empty)
                    .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(word =>
                        double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double n)
                            ? n * charWidth
                            : double.NaN
                    )
                    .Where(n => !double.IsNaN(n))
                    .ToList();

                if (stops.Count > 0 && stops[0] <= 0)
                {
                    stops.RemoveAt(0);
                }

                return stops.ToArray();
            }

            internal override void Refresh()
            {
                bool enabled = Tile.IsEnabled;
                _list.IsEnabled = enabled;

                if (_label != null)
                {
                    _label.Foreground = enabled ? Text : DisabledText;
                }

                if (
                    !_list
                        .Items.Cast<object>()
                        .Select(item => ((FrameworkElement)item).Tag as string)
                        .SequenceEqual(Tile.Items)
                )
                {
                    _list.Items.Clear();

                    foreach (string item in Tile.Items)
                    {
                        _list.Items.Add(Row(item));
                    }
                }

                List<int> chosen = IndicesOf(Tile.Value).Where(n => n < _list.Items.Count).ToList();

                if (!ValueOf(_list.SelectedIndexes()).Equals(ValueOf(chosen), StringComparison.Ordinal))
                {
                    if (_list.SelectionMode == SelectionMode.Single)
                    {
                        _list.SelectedItem = chosen.Count > 0 ? _list.Items[chosen[0]] : null;
                    }
                    else
                    {
                        _list.SelectedItems.Clear();

                        foreach (int index in chosen)
                        {
                            _list.SelectedItems.Add(_list.Items[index]);
                        }
                    }

                    if (chosen.Count > 0)
                    {
                        _list.ScrollIntoView(_list.Items[chosen[0]]);
                    }
                }
            }

            // A row: its text, or its cells at the tab stops where it has tabs.
            private FrameworkElement Row(string item)
            {
                string[] cells = item.Split('\t');

                if (cells.Length == 1 || _stops.Length == 0)
                {
                    TextBlock one = Line(Window.Metrics);
                    one.Text = item;
                    one.Tag = item;
                    return one;
                }

                var row = new Canvas
                {
                    Height = Window.Metrics.CharHeight,
                    Tag = item,
                    Background = Brushes.Transparent,
                };

                for (int i = 0; i < cells.Length; i++)
                {
                    double at =
                        i == 0 ? 0
                        : i - 1 < _stops.Length ? _stops[i - 1]
                        : _stops[_stops.Length - 1] + (i - _stops.Length) * 8 * Window.Metrics.CharWidth;
                    TextBlock cell = Line(Window.Metrics);
                    cell.Text = cells[i];
                    Canvas.SetLeft(cell, at);

                    if (_truncate && i < cells.Length - 1)
                    {
                        double next = i < _stops.Length ? _stops[i] : at + 8 * Window.Metrics.CharWidth;
                        cell.Width = Math.Max(0, next - at);
                        cell.TextTrimming = TextTrimming.CharacterEllipsis;
                    }

                    row.Children.Add(cell);
                }

                return row;
            }

            private string Chosen() => ValueOf(_list.SelectedIndexes());

            private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (Window.IsRefreshing || Tile.Key == null)
                {
                    return;
                }

                Tile.Value = Chosen();
                Window.Raise(DclEvent.Tile(Tile.Key, Tile.Value, 1));
            }

            private void OnDoubleClick(object sender, MouseButtonEventArgs e)
            {
                var source = e.OriginalSource as DependencyObject;

                while (source != null && !(source is ListBoxItem) && !ReferenceEquals(source, _list))
                {
                    source = VisualTreeHelper.GetParent(source);
                }

                if (Tile.Key == null || !(source is ListBoxItem))
                {
                    return;
                }

                Tile.Value = Chosen();
                Window.Raise(DclEvent.Tile(Tile.Key, Tile.Value, 4));
            }
        }

        // ---- toggle and radio button ----

        private sealed class ToggleView : DclTileView
        {
            private readonly CheckBox _check;

            internal ToggleView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                _check = new CheckBox
                {
                    Style = Styled("DclCheck"),
                    Content = AccessLabel(tile),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _check.Click += OnClick;
            }

            internal override FrameworkElement Element => _check;

            internal override void Place(DclRect rect)
            {
                Canvas.SetLeft(_check, Math.Floor(rect.X));
                Canvas.SetTop(_check, Math.Floor(rect.Y));
                _check.MinWidth = rect.Width;
                _check.Height = rect.Height;
            }

            internal override void Refresh()
            {
                _check.IsEnabled = Tile.IsEnabled;
                bool on = string.Equals(Tile.Value, "1", StringComparison.Ordinal);

                if (_check.IsChecked != on)
                {
                    _check.IsChecked = on;
                }
            }

            private void OnClick(object sender, RoutedEventArgs e)
            {
                if (Tile.Key == null)
                {
                    return;
                }

                Tile.Value = _check.IsChecked == true ? "1" : "0";
                Window.Raise(DclEvent.Tile(Tile.Key, Tile.Value, 1));
            }
        }

        private sealed class RadioView : DclTileView
        {
            private readonly RadioButton _radio;

            internal RadioView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                DclTile cluster = DclDialog.ClusterOf(tile);
                _radio = new RadioButton
                {
                    Style = Styled("DclRadio"),
                    Content = AccessLabel(tile),
                    GroupName = window.GroupOf(cluster ?? tile.Parent),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                _radio.Click += OnClick;
            }

            internal override FrameworkElement Element => _radio;

            internal override void Place(DclRect rect)
            {
                Canvas.SetLeft(_radio, Math.Floor(rect.X));
                Canvas.SetTop(_radio, Math.Floor(rect.Y));
                _radio.MinWidth = rect.Width;
                _radio.Height = rect.Height;
            }

            internal override void Refresh()
            {
                _radio.IsEnabled = Tile.IsEnabled;
                bool on = string.Equals(Tile.Value, "1", StringComparison.Ordinal);

                if (_radio.IsChecked != on)
                {
                    _radio.IsChecked = on;
                }
            }

            private void OnClick(object sender, RoutedEventArgs e)
            {
                if (Tile.Key == null)
                {
                    return;
                }

                // The button chosen clears the others of its cluster in the dialog, as set_tile does.
                Window.Dialog.ApplyEvent(Tile, "1");
                Window.Raise(DclEvent.Tile(Tile.Key, "1", 1));
            }
        }

        // ---- text ----

        private sealed class TextView : DclTileView
        {
            private readonly TextBlock _text;

            internal TextView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                _text = Line(window.Metrics);

                if (tile.IsTrue("is_bold"))
                {
                    _text.FontWeight = FontWeights.SemiBold;
                }

                if (string.Equals(tile.Name, "errtile", StringComparison.Ordinal) || tile.Key == "error")
                {
                    _text.Foreground = ErrorText;
                    IsError = true;
                }
            }

            private bool IsError { get; }

            internal override FrameworkElement Element => _text;

            internal override Control FocusTarget => null;

            internal override void Refresh()
            {
                string shown = Tile.Value ?? Tile.Get("label") ?? string.Empty;

                if (!string.Equals(_text.Text, shown, StringComparison.Ordinal))
                {
                    _text.Text = shown;
                }

                _text.Foreground = Tile.IsEnabled ? (IsError ? ErrorText : Text) : DisabledText;
            }
        }

        // ---- slider ----

        private sealed class SliderView : DclTileView
        {
            private readonly Slider _slider;

            internal SliderView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                int minimum = Integer(tile, "min_value", 0);
                int maximum = Math.Max(minimum, Integer(tile, "max_value", 10000));
                _slider = new Slider
                {
                    Style = Styled("DclSlider"),
                    Minimum = minimum,
                    Maximum = maximum,
                    Orientation = string.Equals(tile.Get("layout"), "vertical", StringComparison.Ordinal)
                        ? Orientation.Vertical
                        : Orientation.Horizontal,
                    SmallChange = Math.Max(1, Integer(tile, "small_increment", 1)),
                    LargeChange = Math.Max(1, Integer(tile, "big_increment", Math.Max(1, (maximum - minimum) / 10))),
                    TickFrequency = 1,
                    IsSnapToTickEnabled = true,
                    IsMoveToPointEnabled = false,
                };
                _slider.ValueChanged += OnValueChanged;
                _slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnDragCompleted), true);
                _slider.GotKeyboardFocus += (s, e) => Window.NoteFocus(this);
            }

            internal override FrameworkElement Element => _slider;

            internal override void Refresh()
            {
                _slider.IsEnabled = Tile.IsEnabled;

                if (
                    int.TryParse(Tile.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && Math.Abs(_slider.Value - value) > 0.5
                )
                {
                    _slider.Value = value;
                }
            }

            private int Value => (int)Math.Round(_slider.Value);

            private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
            {
                if (Window.IsRefreshing || Tile.Key == null)
                {
                    return;
                }

                Tile.Value = Value.ToString(CultureInfo.InvariantCulture);
                bool dragging =
                    _slider.Template?.FindName("PART_Track", _slider) is Track track
                    && track.Thumb != null
                    && track.Thumb.IsDragging;
                Window.Raise(DclEvent.Tile(Tile.Key, Tile.Value, dragging ? 3 : 1));
            }

            private void OnDragCompleted(object sender, DragCompletedEventArgs e)
            {
                if (Tile.Key != null)
                {
                    Tile.Value = Value.ToString(CultureInfo.InvariantCulture);
                    Window.Raise(DclEvent.Tile(Tile.Key, Tile.Value, 1));
                }
            }
        }

        // ---- a boxed cluster ----

        private sealed class BoxView : DclTileView
        {
            private readonly Canvas _canvas = new Canvas { IsHitTestVisible = false };
            private readonly Border _frame;
            private readonly TextBlock _label;

            internal BoxView(DclTile tile, DclDialogWindow window)
                : base(tile, window)
            {
                _frame = new Border
                {
                    BorderBrush = (Brush)window.Theme["GhBorder"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                };
                _canvas.Children.Add(_frame);
                string label = DclLayout.StripAccess(tile.Get("label"));

                if (label.Trim().Length > 0)
                {
                    // The label stands on the frame's top line, which it breaks with a patch of the dialog's own colour.
                    _label = Line(window.Metrics);
                    _label.Text = label;
                    _label.Background = (Brush)window.Theme["GhPanel"];
                    _label.Padding = new Thickness(4, 0, 4, 0);
                    Canvas.SetLeft(_label, 8);
                    Canvas.SetTop(_label, 0);
                    _canvas.Children.Add(_label);
                }
            }

            internal override FrameworkElement Element => _canvas;

            internal override Control FocusTarget => null;

            internal override void Place(DclRect rect)
            {
                base.Place(rect);
                double top = Math.Floor(Window.Metrics.CharHeight / 2);
                Canvas.SetTop(_frame, top);
                _frame.Width = rect.Width;
                _frame.Height = Math.Max(0, rect.Height - top);
            }

            internal override void Refresh() { }
        }
    }

    /// <summary>The indices chosen in a list box, as the control holds them.</summary>
    internal static class DclListBoxExtensions
    {
        /// <summary>Gives the positions of the items chosen in a list box.</summary>
        /// <param name="list">The list box.</param>
        /// <returns>The indices, in the order of the items.</returns>
        internal static IEnumerable<int> SelectedIndexes(this ListBox list) =>
            list
                .SelectedItems.Cast<object>()
                .Select(item => list.Items.IndexOf(item))
                .Where(n => n >= 0)
                .OrderBy(n => n);
    }
}
