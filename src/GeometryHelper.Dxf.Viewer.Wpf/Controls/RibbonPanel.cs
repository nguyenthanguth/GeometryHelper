using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The size a panel of the ribbon is drawn at, as AutoCAD's panels shrink when the window narrows.</summary>
    public enum RibbonPanelSize
    {
        /// <summary>Every button with its label.</summary>
        Full,

        /// <summary>Every button still there, the small ones drawn by their icons alone, the tooltip naming the command.</summary>
        Medium,

        /// <summary>One big button, the panel's icon, name and ▾, that drops the whole panel below it.</summary>
        Collapsed,
    }

    /// <summary>
    /// A panel of the ribbon, as AutoCAD's: its buttons over its title, drawn full, medium or collapsed as the
    /// <see cref="RibbonStrip"/> it is in has room for. Collapsed, it is one big button that drops the full panel below it
    /// with a title bar and a pin; Esc or a click elsewhere closes the drop-down unless it is pinned, and near the window's
    /// right edge it shifts left to stay inside. A panel with a <see cref="SlideOut"/> drops that below it the same way, its
    /// title the button that opens it.
    /// </summary>
    /// <remarks>
    /// The body stays in the panel while the drop-down is shut, hidden where the panel is collapsed, so that its buttons keep
    /// their bindings; it moves into the drop-down while that is open. The look is the implicit style of the workspace.
    /// </remarks>
    [ContentProperty(nameof(Body))]
    [TemplatePart(Name = "PART_Inline", Type = typeof(Decorator))]
    [TemplatePart(Name = "PART_Dropped", Type = typeof(Decorator))]
    [TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
    public class RibbonPanel : Control
    {
        /// <summary>Identifies the <see cref="Title"/> property.</summary>
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(RibbonPanel), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>Identifies the <see cref="Icon"/> property.</summary>
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(System.Windows.Media.Geometry), typeof(RibbonPanel), new PropertyMetadata(null));

        /// <summary>Identifies the <see cref="Header"/> property.</summary>
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            nameof(Header), typeof(object), typeof(RibbonPanel), new PropertyMetadata(null));

        /// <summary>Identifies the <see cref="Body"/> property.</summary>
        public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
            nameof(Body), typeof(UIElement), typeof(RibbonPanel), new PropertyMetadata(null, (d, e) => ((RibbonPanel)d).Place(e.OldValue as UIElement)));

        /// <summary>Identifies the <see cref="SlideOut"/> property.</summary>
        public static readonly DependencyProperty SlideOutProperty = DependencyProperty.Register(
            nameof(SlideOut), typeof(UIElement), typeof(RibbonPanel), new PropertyMetadata(null, (d, e) => ((RibbonPanel)d).Place(e.OldValue as UIElement)));

        /// <summary>Identifies the <see cref="Size"/> property.</summary>
        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(RibbonPanelSize), typeof(RibbonPanel),
            new FrameworkPropertyMetadata(RibbonPanelSize.Full, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, e) => ((RibbonPanel)d).Place(null)));

        /// <summary>Identifies the <see cref="IsDropDownOpen"/> property.</summary>
        public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
            nameof(IsDropDownOpen), typeof(bool), typeof(RibbonPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((RibbonPanel)d).OnDropDownChanged()));

        /// <summary>Identifies the <see cref="IsPinned"/> property.</summary>
        public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register(
            nameof(IsPinned), typeof(bool), typeof(RibbonPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        /// <summary>
        /// Identifies the attached property that tells the buttons of a panel it is drawn medium, inherited down the panel's
        /// body: the styles of the small buttons then draw their icons alone.
        /// </summary>
        public static readonly DependencyProperty IsMediumProperty = DependencyProperty.RegisterAttached(
            "IsMedium", typeof(bool), typeof(RibbonPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

        /// <summary>Identifies the <see cref="MinFullWidth"/> property.</summary>
        public static readonly DependencyProperty MinFullWidthProperty = DependencyProperty.Register(
            nameof(MinFullWidth), typeof(double), typeof(RibbonPanel), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>Identifies the <see cref="MinMediumWidth"/> property.</summary>
        public static readonly DependencyProperty MinMediumWidthProperty = DependencyProperty.Register(
            nameof(MinMediumWidth), typeof(double), typeof(RibbonPanel), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>Identifies the <see cref="MinCollapsedWidth"/> property.</summary>
        public static readonly DependencyProperty MinCollapsedWidthProperty = DependencyProperty.Register(
            nameof(MinCollapsedWidth), typeof(double), typeof(RibbonPanel), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        private Decorator _inline;
        private Decorator _dropped;
        private Popup _popup;
        private Window _window;

        static RibbonPanel()
        {
            FocusableProperty.OverrideMetadata(typeof(RibbonPanel), new FrameworkPropertyMetadata(false));
        }

        /// <summary>Gets or sets the panel's name, drawn under its buttons and on its collapsed button: Draw, Modify, Clipboard.</summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>Gets or sets the icon of the collapsed button, on the 24 by 24 grid of the icons.</summary>
        public System.Windows.Media.Geometry Icon
        {
            get => (System.Windows.Media.Geometry)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        /// <summary>Gets or sets what is drawn in place of the title, as Modify's title button with its menu; null for the title.</summary>
        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        /// <summary>Gets or sets the panel's buttons.</summary>
        public UIElement Body
        {
            get => (UIElement)GetValue(BodyProperty);
            set => SetValue(BodyProperty, value);
        }

        /// <summary>Gets or sets what the panel's slide-out drops below it while the panel is not collapsed, as Annotate's Dimensions ▾; null for none.</summary>
        public UIElement SlideOut
        {
            get => (UIElement)GetValue(SlideOutProperty);
            set => SetValue(SlideOutProperty, value);
        }

        /// <summary>Gets or sets the size the panel is drawn at; the <see cref="RibbonStrip"/> it is in sets it.</summary>
        public RibbonPanelSize Size
        {
            get => (RibbonPanelSize)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>Gets or sets whether the drop-down is open: the full panel below a collapsed one, or the slide-out.</summary>
        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        /// <summary>Gets or sets whether the drop-down is pinned open: Esc and a click elsewhere then leave it open.</summary>
        public bool IsPinned
        {
            get => (bool)GetValue(IsPinnedProperty);
            set => SetValue(IsPinnedProperty, value);
        }

        /// <summary>
        /// Gets or sets the least width of the panel drawn full, its line and padding in, as the design gives it, so that the
        /// panels shrink at the window widths the design gives whichever font draws the labels; Draw's is 342. 0 for none.
        /// </summary>
        public double MinFullWidth
        {
            get => (double)GetValue(MinFullWidthProperty);
            set => SetValue(MinFullWidthProperty, value);
        }

        /// <summary>Gets or sets the least width of the panel drawn medium, as the design gives it; Draw's is 278. 0 for none.</summary>
        public double MinMediumWidth
        {
            get => (double)GetValue(MinMediumWidthProperty);
            set => SetValue(MinMediumWidthProperty, value);
        }

        /// <summary>Gets or sets the least width of the panel collapsed, as the design gives it; View's is 50. 0 for none.</summary>
        public double MinCollapsedWidth
        {
            get => (double)GetValue(MinCollapsedWidthProperty);
            set => SetValue(MinCollapsedWidthProperty, value);
        }

        /// <summary>Gets whether an element is drawn in a medium panel.</summary>
        /// <param name="element">The element.</param>
        /// <returns>Whether it is.</returns>
        public static bool GetIsMedium(DependencyObject element) => (bool)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(IsMediumProperty);

        /// <summary>Sets whether an element and those under it are drawn in a medium panel.</summary>
        /// <param name="element">The element.</param>
        /// <param name="value">Whether they are.</param>
        public static void SetIsMedium(DependencyObject element, bool value) => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(IsMediumProperty, value);

        /// <inheritdoc/>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_inline != null)
            {
                _inline.Child = null;
            }

            if (_dropped != null)
            {
                _dropped.Child = null;
                _dropped.RemoveHandler(ButtonBase.ClickEvent, (RoutedEventHandler)OnDroppedClick);
            }

            _inline = GetTemplateChild("PART_Inline") as Decorator;
            _dropped = GetTemplateChild("PART_Dropped") as Decorator;
            _popup = GetTemplateChild("PART_Popup") as Popup;

            if (_dropped != null)
            {
                _dropped.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)OnDroppedClick);
            }

            if (_popup != null)
            {
                _popup.CustomPopupPlacementCallback = PlacePopup;
            }

            Place(null);
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size constraint)
        {
            Size measured = base.MeasureOverride(constraint);
            double least = Size == RibbonPanelSize.Full ? MinFullWidth : Size == RibbonPanelSize.Medium ? MinMediumWidth : MinCollapsedWidth;
            return new Size(Math.Max(measured.Width, least), measured.Height);
        }

        /// <summary>
        /// How far a drop-down is shifted along the window to stay inside it: none where it fits right of its panel's left
        /// edge, else left by what it would stick out, never past the window's left edge. A drop-down 290 wide under a panel
        /// at 1 400 of a window 1 600 wide shifts 90 to the left.
        /// </summary>
        internal static double ShiftToFit(double left, double width, double windowWidth)
        {
            double over = left + width - windowWidth;
            return over > 0.0 ? -Math.Min(over, Math.Max(0.0, left)) : 0.0;
        }

        /// <summary>The body in the panel or in the drop-down, the slide-out in the drop-down, as the size and the drop-down say.</summary>
        private void Place(UIElement old)
        {
            if (_inline == null || _dropped == null)
            {
                return;
            }

            bool collapsed = Size == RibbonPanelSize.Collapsed;
            UIElement inline = collapsed && IsDropDownOpen ? null : Body;
            UIElement dropped = collapsed ? (IsDropDownOpen ? Body : null) : SlideOut;

            if (old != null && _inline.Child == old)
            {
                _inline.Child = null;
            }

            if (old != null && _dropped.Child == old)
            {
                _dropped.Child = null;
            }

            if (_inline.Child != inline)
            {
                _inline.Child = null;
            }

            if (_dropped.Child != dropped)
            {
                _dropped.Child = null;
            }

            _inline.Child = inline;
            _dropped.Child = dropped;
            bool medium = Size == RibbonPanelSize.Medium;

            if (GetIsMedium(_inline) != medium)
            {
                // The buttons deep in the body change their look; the whole panel is measured again at once, so that the width
                // the strip reads next is the panel's at its new size.
                SetIsMedium(_inline, medium);
                InvalidateBelow(this);
            }
        }

        private static void InvalidateBelow(DependencyObject element)
        {
            if (element is UIElement ui)
            {
                ui.InvalidateMeasure();
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                InvalidateBelow(VisualTreeHelper.GetChild(element, i));
            }
        }

        private void OnDropDownChanged()
        {
            Place(null);

            // Esc, a click elsewhere in the window or the window losing the keyboard close it, unless it is pinned.
            Window window = IsDropDownOpen ? Window.GetWindow(this) : null;

            if (_window != null && _window != window)
            {
                _window.PreviewMouseDown -= OnWindowMouseDown;
                _window.PreviewKeyDown -= OnWindowKeyDown;
                _window.Deactivated -= OnWindowDeactivated;
            }

            if (window != null && _window != window)
            {
                window.PreviewMouseDown += OnWindowMouseDown;
                window.PreviewKeyDown += OnWindowKeyDown;
                window.Deactivated += OnWindowDeactivated;
            }

            _window = window;
        }

        private void Close()
        {
            if (!IsPinned)
            {
                IsDropDownOpen = false;
            }
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && IsDropDownOpen && !IsPinned)
            {
                IsDropDownOpen = false;
                e.Handled = true;
            }
        }

        private void OnWindowDeactivated(object sender, EventArgs e) => Close();

        /// <summary>A press in the window outside the panel and its drop-down closes it; one in a menu opened from it does not.</summary>
        private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;

            if (source == null || Within(source, this) || (_dropped != null && Within(source, _dropped)))
            {
                return;
            }

            // A press in another popup, as the menu of a split button of the drop-down, is not in the window.
            if (_window != null && !Within(source, _window))
            {
                return;
            }

            Close();
        }

        /// <summary>A command picked in the drop-down closes it, after the click is done with; an arrow or a toggle leaves it open.</summary>
        private void OnDroppedClick(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button button && button.ContextMenu == null && !IsPinned)
            {
                Dispatcher.BeginInvoke(new Action(Close));
            }
        }

        /// <summary>The drop-down under the panel, shifted left where it would leave the window on the right.</summary>
        private CustomPopupPlacement[] PlacePopup(Size popupSize, Size targetSize, Point offset)
        {
            double shift = 0.0;
            double scale = ActualWidth > 0.0 ? targetSize.Width / ActualWidth : 1.0;

            if (PresentationSource.FromVisual(this)?.RootVisual is FrameworkElement root && root.IsAncestorOf(this) && scale > 0.0)
            {
                double left = TransformToAncestor(root).Transform(new Point(0.0, 0.0)).X;
                shift = ShiftToFit(left, popupSize.Width / scale, root.ActualWidth);
            }

            return new[] { new CustomPopupPlacement(new Point(shift * scale, targetSize.Height), PopupPrimaryAxis.Horizontal) };
        }

        private static bool Within(DependencyObject element, DependencyObject ancestor)
        {
            for (DependencyObject at = element; at != null; at = at is Visual || at is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(at) : LogicalTreeHelper.GetParent(at))
            {
                if (at == ancestor)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
