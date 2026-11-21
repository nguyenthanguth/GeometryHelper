using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Cad.Dcl;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The window of one DCL dialog (D334), built in code from the dialog's tiles: a title bar with the dialog's <c>label</c>
    /// and a close box, and a body in which every tile is a control at the rectangle <see cref="DclLayout"/> gave it, in the
    /// dialog tokens of the app and the shapes of the Design artifact's DCL boards. It reports what the user does as
    /// <see cref="DclEvent"/>s through <see cref="Raised"/> and shows the tiles' values again on <see cref="Refresh"/>;
    /// <see cref="DclWindowHost"/> shows it, waits on it and takes it away.
    /// </summary>
    internal sealed class DclDialogWindow : Window
    {
        // The Segoe UI the boards are drawn in and the layout is measured in, 12 px.
        private static readonly FontFamily Dialog12 = new FontFamily("Segoe UI");
        private const double Dialog12Size = 12;
        private static readonly Typeface Face = new Typeface(
            Dialog12,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal
        );

        // The room the body leaves round the layout: AutoCAD's frame is 8 px at the sides and below (g1) and nothing above, where
        // the title bar is, so that with the border of the window the first tile stands 16 from the window's left edge.
        private static readonly Thickness BodyPadding = new Thickness(8, 0, 6, 6);
        private const double TitleHeight = 29;

        private readonly List<DclTileView> _views = new List<DclTileView>();
        private readonly Dictionary<DclTile, string> _groups = new Dictionary<DclTile, string>();
        private readonly Canvas _canvas = new Canvas();
        private readonly Border _root;
        private DclTileView _focused;
        private bool _isClosed;

        /// <summary>Makes the window of a dialog, not shown.</summary>
        /// <param name="dialog">The dialog, its values given (<see cref="DclDialog.Initialise"/>).</param>
        internal DclDialogWindow(DclDialog dialog)
        {
            Dialog = dialog;
            Metrics = CreateMetrics();
            Theme = new ResourceDictionary
            {
                Source = new Uri("/GeometryHelper.Cad.Wpf;component/Themes/Dcl.xaml", UriKind.Relative),
            };
            Resources.MergedDictionaries.Add(Theme);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Background = (Brush)Theme["GhPanel"];
            Title = Dialog.Root.Get("label") ?? dialog.Name ?? string.Empty;
            FontFamily = Dialog12;
            FontSize = Dialog12Size;
            Foreground = (Brush)Theme["GhText"];
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Cycle);

            IReadOnlyDictionary<DclTile, DclRect> placed = DclLayout.Arrange(dialog.Root, Metrics);
            DclRect body = placed[dialog.Root];
            _canvas.Width = Math.Ceiling(body.Width);
            _canvas.Height = Math.Ceiling(body.Height);
            _canvas.Background = Brushes.Transparent;
            KeyboardNavigation.SetTabNavigation(_canvas, KeyboardNavigationMode.Cycle);

            // Depth-first, so that the order of the controls is the tile order the Tab key follows, and a boxed cluster is
            // under its children.
            foreach (DclTile tile in dialog.Root.Descendants())
            {
                DclTileView view = DclTileView.Create(tile, this);

                if (view == null)
                {
                    continue;
                }

                view.Place(placed[tile]);

                if (view.FocusTarget != null)
                {
                    view.FocusTarget.IsTabStop = view.IsTabStop;
                }

                _canvas.Children.Add(view.Element);
                _views.Add(view);
            }

            _root = new Border
            {
                Background = (Brush)Theme["GhPanel"],
                BorderBrush = (Brush)Theme["GhBorder"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                UseLayoutRounding = true,
                SnapsToDevicePixels = true,
                Child = MakeFrame(),
            };
            Content = _root;

            PreviewKeyDown += OnPreviewKeyDown;
            Closing += OnClosing;
            Closed += (s, e) =>
            {
                _isClosed = true;

                if (!IsReleased)
                {
                    Raise(DclEvent.Escape());
                }
            };
            AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnAnyGotFocus), true);
            Loaded += (s, e) => ApplyFocus(true);
        }

        /// <summary>Gets the dialog the window shows.</summary>
        internal DclDialog Dialog { get; }

        /// <summary>Gets the metrics the layout was made with: the dialog font's text widths and AutoCAD 2021's sizes.</summary>
        internal DclMetrics Metrics { get; }

        /// <summary>Gets the styles of Themes/Dcl.xaml, with those of Dark.xaml.</summary>
        internal ResourceDictionary Theme { get; }

        /// <summary>Gets whether <see cref="Refresh"/> is putting values into the controls: what they report then is not the user's.</summary>
        internal bool IsRefreshing { get; private set; }

        /// <summary>Gets whether the host took the window away: closing it then is no act of the user.</summary>
        internal bool IsReleased { get; private set; }

        /// <summary>Gets every tile that has a control, in tile order.</summary>
        internal IReadOnlyList<DclTileView> Views => _views;

        /// <summary>Gets the body the tiles are placed on, whose size is the layout's.</summary>
        internal Canvas Body => _canvas;

        /// <summary>Gets the top of the window's content, the frame with its title bar, for a picture of it.</summary>
        internal FrameworkElement Chrome => _root;

        /// <summary>Occurs when the user acts: a tile used, Enter, Esc or the close box.</summary>
        internal event Action<DclEvent> Raised;

        /// <summary>
        /// Makes the metrics of the dialog windows: AutoCAD 2021's sizes (6 by 13, buttons 22 high and the others of D345),
        /// the width of a text measured in Segoe UI 12 px, and the numbers that fit what g1 measured with that font: 19 round
        /// a button's words (OK 49, Cancel 55), 7 between a label and its box (the label "Text:" 31 wide), a boxed cluster
        /// 5 inside its frame.
        /// </summary>
        /// <returns>The metrics.</returns>
        internal static DclMetrics CreateMetrics()
        {
            var widths = new Dictionary<string, double>(StringComparer.Ordinal);
            return new DclMetrics
            {
                TextWidth = text =>
                {
                    text = text ?? string.Empty;

                    if (!widths.TryGetValue(text, out double width))
                    {
                        width = text.Length == 0 ? 0 : Math.Ceiling(Measure(text));
                        widths[text] = width;
                    }

                    return width;
                },
                ButtonPaddingX = 19,
                LabelGap = 7,
                BoxBorder = 5,
            };
        }

        /// <summary>Measures a text in Segoe UI 12 px, spaces at its ends counted, before rounding.</summary>
        private static double Measure(string text)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Face,
                Dialog12Size,
                Brushes.Black,
                1.0
            );
            return formatted.WidthIncludingTrailingWhitespace;
        }

        /// <summary>Gives the view of the first tile with a key; null for none or a tile with no control.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The view.</returns>
        internal DclTileView ViewOf(string key) =>
            key == null
                ? null
                : _views.FirstOrDefault(view => string.Equals(view.Tile.Key, key, StringComparison.Ordinal));

        /// <summary>Gives the name of the group a radio cluster's buttons share in this window.</summary>
        /// <param name="cluster">The cluster, or the tile that holds a button that has none.</param>
        /// <returns>The group's name.</returns>
        internal string GroupOf(DclTile cluster)
        {
            if (cluster == null)
            {
                return "none";
            }

            if (!_groups.TryGetValue(cluster, out string group))
            {
                group = "dcl" + _groups.Count.ToString(CultureInfo.InvariantCulture);
                _groups[cluster] = group;
            }

            return group;
        }

        /// <summary>Reports what the user did, unless the window is only showing values.</summary>
        /// <param name="dclEvent">The act.</param>
        internal void Raise(DclEvent dclEvent)
        {
            if (!IsRefreshing && !IsReleased)
            {
                Raised?.Invoke(dclEvent);
            }
        }

        /// <summary>Notes which tile has the keyboard focus, in the dialog too (<see cref="DclDialog.FocusKey"/>).</summary>
        /// <param name="view">The view whose control took the focus.</param>
        internal void NoteFocus(DclTileView view)
        {
            _focused = view;

            if (view.Tile.Key != null)
            {
                Dialog.FocusKey = view.Tile.Key;
            }
        }

        /// <summary>
        /// Shows every tile's value as the dialog now holds it (a change the routine made with <c>set_tile</c>, <c>mode_tile</c>,
        /// <c>start_list</c> or the image functions), moves the focus where <c>mode_tile</c> 2 sent it, and where the window
        /// stands is told to the dialog for <c>done_dialog</c>.
        /// </summary>
        internal void Refresh()
        {
            IsRefreshing = true;

            try
            {
                foreach (DclTileView view in _views)
                {
                    view.Refresh();
                }
            }
            finally
            {
                IsRefreshing = false;
            }

            ApplyFocus(false);
            RememberPosition();
        }

        /// <summary>Tells the dialog where the window stands, in screen pixels, for <c>done_dialog</c>.</summary>
        internal void RememberPosition()
        {
            if (!IsVisible)
            {
                return;
            }

            Point corner = PointToScreen(new Point(0, 0));
            Dialog.PositionX = (int)Math.Round(corner.X);
            Dialog.PositionY = (int)Math.Round(corner.Y);
        }

        /// <summary>Takes the window away: the host is done with it, so closing it raises nothing.</summary>
        internal void Release()
        {
            IsReleased = true;

            if (!_isClosed)
            {
                RememberPosition();
                Close();
            }
        }

        /// <summary>
        /// Draws the window's content, its title bar included, to a PNG file; where the window is not shown it is laid out first.
        /// </summary>
        /// <param name="path">The file to write.</param>
        internal void RenderPng(string path)
        {
            _root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _root.Arrange(new Rect(_root.DesiredSize));
            _root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(
                (int)Math.Ceiling(_root.DesiredSize.Width),
                (int)Math.Ceiling(_root.DesiredSize.Height),
                96,
                96,
                PixelFormats.Pbgra32
            );
            bitmap.Render(_root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        // The title bar over the body.
        private FrameworkElement MakeFrame()
        {
            var title = new TextBlock
            {
                Text = Title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Theme["GhText"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var close = new Button
            {
                Style = (Style)Theme["GhTitleCloseButton"],
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
                Focusable = false,
            };
            close.Click += (s, e) => Raise(DclEvent.Escape());
            var bar = new Grid { Height = TitleHeight, Background = (Brush)Theme["GhHeader"] };
            bar.Children.Add(title);
            bar.Children.Add(close);
            bar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed && !(e.OriginalSource is Button))
                {
                    DragMove();
                }
            };

            var barFrame = new Border
            {
                Child = bar,
                CornerRadius = new CornerRadius(5, 5, 0, 0),
                ClipToBounds = true,
            };
            _canvas.Margin = BodyPadding;
            var stack = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(barFrame, Dock.Top);
            stack.Children.Add(barFrame);
            stack.Children.Add(_canvas);
            return stack;
        }

        // ---- focus ----

        private void OnAnyGotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!(e.NewFocus is DependencyObject now))
            {
                return;
            }

            foreach (DclTileView view in _views)
            {
                if (view.FocusTarget != null && IsInside(now, view.FocusTarget))
                {
                    NoteFocus(view);
                    return;
                }
            }
        }

        private static bool IsInside(DependencyObject child, DependencyObject ancestor)
        {
            for (DependencyObject at = child; at != null; at = VisualOrLogicalParent(at))
            {
                if (ReferenceEquals(at, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        private static DependencyObject VisualOrLogicalParent(DependencyObject child)
        {
            if (child is Visual || child is System.Windows.Media.Media3D.Visual3D)
            {
                DependencyObject parent = VisualTreeHelper.GetParent(child);

                if (parent != null)
                {
                    return parent;
                }
            }

            return LogicalTreeHelper.GetParent(child);
        }

        private void ApplyFocus(bool first)
        {
            DclTileView target = null;
            string key = Dialog.FocusKey;

            if (key != null && !ReferenceEquals(_focused, ViewOf(key)))
            {
                target = ViewOf(key);
            }

            if (target == null && first && _focused == null)
            {
                target = _views.FirstOrDefault(view => view.IsTabStop && view.Tile.IsEnabled);
            }

            Control control = target?.FocusTarget;

            if (control != null && control.IsEnabled && control.IsVisible)
            {
                control.Focus();
                Keyboard.Focus(control);
            }
        }

        // ---- keys and closing ----

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                return;
            }

            // A drop-down that is open takes Enter and Esc for itself.
            bool dropped = _views.Any(view => view.FocusTarget is ComboBox combo && combo.IsDropDownOpen);

            if (e.Key == Key.Enter && !dropped)
            {
                string key = _focused?.Tile.Key;
                Raise(DclEvent.Enter(key));
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && !dropped)
            {
                Raise(DclEvent.Escape());
                e.Handled = true;
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            // The close box of the system (Alt+F4) is Esc, as the box of the title bar is; only the host closes the window.
            if (!IsReleased)
            {
                e.Cancel = true;
                Raise(DclEvent.Escape());
            }
        }
    }
}
