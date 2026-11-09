using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The panels of a ribbon tab, left to right, shrunk as AutoCAD's are when the window narrows: the <see cref="Steps"/>
    /// are taken in turn until the panels fit the width the strip has, and given back in reverse as it widens. Home at 1 600
    /// pixels takes seven: View, Clipboard, Utilities and Groups collapsed, then Properties, Layers and Draw medium.
    /// </summary>
    public sealed class RibbonStrip : Panel
    {
        /// <summary>Identifies the <see cref="Steps"/> property.</summary>
        public static readonly DependencyProperty StepsProperty = DependencyProperty.Register(
            nameof(Steps), typeof(string), typeof(RibbonStrip), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, e) => ((RibbonStrip)d)._steps = Parse((string)e.NewValue)));

        // The widths of each panel at each size, measured at the strip's present width; looked up, never enumerated.
        private readonly Dictionary<RibbonPanel, double[]> _widths = new Dictionary<RibbonPanel, double[]>();
        private IReadOnlyList<KeyValuePair<string, RibbonPanelSize>> _steps = Array.Empty<KeyValuePair<string, RibbonPanelSize>>();
        private double _lastWidth = double.NaN;
        private double _lastHeight = double.NaN;

        /// <summary>
        /// Gets or sets the panel-size policy of the tab: the steps that shrink its panels, in the order they are taken, each a
        /// panel's title and the size it goes to, parted by semicolons, as "View:Collapsed; Properties:Medium". A panel named in
        /// no step never shrinks.
        /// </summary>
        public string Steps
        {
            get => (string)GetValue(StepsProperty);
            set => SetValue(StepsProperty, value);
        }

        /// <summary>Gets how many of the steps the last measure took: 0 where every panel is full.</summary>
        public int StepsTaken { get; private set; }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            var child = new Size(double.PositiveInfinity, availableSize.Height);
            var panels = new List<RibbonPanel>();

            foreach (UIElement element in InternalChildren)
            {
                if (element is RibbonPanel panel)
                {
                    panels.Add(panel);
                }
            }

            // A new width closes the drop-downs, as AutoCAD's do when the window is resized, and measures each panel's sizes
            // anew; at the same width a size not shown keeps the width measured, so that the panels are not redrawn in every
            // size at each measure.
            if (!availableSize.Width.Equals(_lastWidth) || !availableSize.Height.Equals(_lastHeight))
            {
                foreach (RibbonPanel panel in panels)
                {
                    panel.IsDropDownOpen = false;
                }

                _widths.Clear();
                _lastWidth = availableSize.Width;
                _lastHeight = availableSize.Height;
            }

            var sizes = new Dictionary<RibbonPanel, RibbonPanelSize>();
            int taken = 0;

            for (int k = 0; k <= _steps.Count; k++)
            {
                if (k > 0)
                {
                    RibbonPanel named = Find(panels, _steps[k - 1].Key);

                    if (named == null)
                    {
                        continue;
                    }

                    sizes[named] = _steps[k - 1].Value;
                }

                taken = k;

                if (double.IsPositiveInfinity(availableSize.Width) || Total(panels, sizes, child) <= availableSize.Width + 0.01)
                {
                    break;
                }
            }

            StepsTaken = taken;
            double width = 0.0;
            double height = 0.0;

            foreach (UIElement element in InternalChildren)
            {
                if (element is RibbonPanel panel)
                {
                    RibbonPanelSize size = sizes.TryGetValue(panel, out RibbonPanelSize s) ? s : RibbonPanelSize.Full;

                    if (panel.Size != size)
                    {
                        panel.IsDropDownOpen = false;
                        panel.Size = size;
                    }
                }

                element.Measure(child);
                width += element.DesiredSize.Width;
                height = Math.Max(height, element.DesiredSize.Height);
            }

            return new Size(width, height);
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            double left = 0.0;

            foreach (UIElement element in InternalChildren)
            {
                element.Arrange(new Rect(left, 0.0, element.DesiredSize.Width, finalSize.Height));
                left += element.DesiredSize.Width;
            }

            return finalSize;
        }

        private static RibbonPanel Find(List<RibbonPanel> panels, string title)
        {
            foreach (RibbonPanel panel in panels)
            {
                if (string.Equals(panel.Title, title, StringComparison.OrdinalIgnoreCase))
                {
                    return panel;
                }
            }

            return null;
        }

        private static IReadOnlyList<KeyValuePair<string, RibbonPanelSize>> Parse(string steps)
        {
            var parsed = new List<KeyValuePair<string, RibbonPanelSize>>();

            foreach (string step in (steps ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = step.LastIndexOf(':');

                if (colon <= 0 || !Enum.TryParse(step.Substring(colon + 1).Trim(), true, out RibbonPanelSize size))
                {
                    throw new FormatException("A step of a ribbon strip is a panel's title and a size, as \"View:Collapsed\": " + step.Trim());
                }

                parsed.Add(new KeyValuePair<string, RibbonPanelSize>(step.Substring(0, colon).Trim(), size));
            }

            return parsed;
        }

        /// <summary>The width of the panels at the sizes given, the others full and every other child as it is.</summary>
        private double Total(List<RibbonPanel> panels, Dictionary<RibbonPanel, RibbonPanelSize> sizes, Size child)
        {
            double total = 0.0;

            foreach (UIElement element in InternalChildren)
            {
                if (element is RibbonPanel panel)
                {
                    RibbonPanelSize size = sizes.TryGetValue(panel, out RibbonPanelSize s) ? s : RibbonPanelSize.Full;
                    total += WidthOf(panel, size, child);
                }
                else
                {
                    element.Measure(child);
                    total += element.DesiredSize.Width;
                }
            }

            return total;
        }

        /// <summary>
        /// A panel's width at a size: measured where it is drawn at that size, else the width measured at it before at this
        /// width of the strip, else measured by drawing it at that size.
        /// </summary>
        private double WidthOf(RibbonPanel panel, RibbonPanelSize size, Size child)
        {
            if (!_widths.TryGetValue(panel, out double[] known))
            {
                known = new[] { double.NaN, double.NaN, double.NaN };
                _widths[panel] = known;
            }

            if (panel.Size != size && !double.IsNaN(known[(int)size]))
            {
                return known[(int)size];
            }

            panel.Size = size;
            panel.Measure(child);
            known[(int)size] = panel.DesiredSize.Width;
            return known[(int)size];
        }
    }
}
