using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The canvas's part of the constraint bars, as AutoCAD 2021 draws them: for each object that is constrained and whose bar
    /// is shown (selected, or just constrained, or shown by CONSTRAINTBAR) a row of small light squares by it, one for each
    /// geometric constraint, each with the glyph of its kind; and the menu of an icon (Delete, Hide, Hide All) a right click on
    /// it opens, while no command runs.
    /// </summary>
    public sealed partial class CadCanvas
    {
        private const double BarIcon = 16.0;
        private const double BarGap = 2.0;
        private static readonly Pen BarEdge = Frozen(new Pen(Solid(0x7A7A7A), 1.0));
        private static readonly Pen BarGlyph = Frozen(
            new Pen(Solid(0x1F1F1F), 1.3)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            }
        );

        private readonly List<KeyValuePair<Rect, KeyValuePair<CadConstraintBar, CadConstraintIcon>>> _barIcons =
            new List<KeyValuePair<Rect, KeyValuePair<CadConstraintBar, CadConstraintIcon>>>();

        private void DrawConstraintBars(DrawingContext dc, CadView view, double scale)
        {
            _barIcons.Clear();
            CadDocument document = Document;
            CadSpace space = WorkSpace;

            if (document == null || space == null || Session == null)
            {
                return;
            }

            CadConstraintSettings settings = Session.Constraints;
            IReadOnlyList<CadConstraintBar> bars = document.ConstraintBars.BarsFor(
                space,
                document.Selection.Handles,
                settings
            );
            byte alpha = (byte)Math.Max(60, 255 - settings.Transparency * 2);
            var fill = new SolidColorBrush(Color.FromArgb(alpha, 0xE6, 0xE6, 0xE6));

            foreach (CadConstraintBar bar in bars)
            {
                Point at = ToScreen(view, new GeoPoint2(bar.X, bar.Y), scale);
                double x = at.X + 10.0;
                double y = at.Y - 10.0 - BarIcon;

                foreach (CadConstraintIcon icon in bar.Icons)
                {
                    var box = new Rect(x, y, BarIcon, BarIcon);
                    dc.DrawRoundedRectangle(fill, BarEdge, box, 2.0, 2.0);
                    System.Windows.Media.Geometry glyph = ConstraintGlyphs.Of(icon.Type);

                    if (glyph != null)
                    {
                        double k = (BarIcon - 4.0) / 24.0;
                        dc.PushTransform(new TranslateTransform(box.X + 2.0, box.Y + 2.0));
                        dc.PushTransform(new ScaleTransform(k, k));
                        dc.DrawGeometry(null, BarGlyph, glyph);
                        dc.Pop();
                        dc.Pop();
                    }

                    _barIcons.Add(
                        new KeyValuePair<Rect, KeyValuePair<CadConstraintBar, CadConstraintIcon>>(
                            box,
                            new KeyValuePair<CadConstraintBar, CadConstraintIcon>(bar, icon)
                        )
                    );
                    x += BarIcon + BarGap;
                }
            }
        }

        /// <summary>
        /// Opens the menu of the constraint icon under a point, as a right click on an icon of AutoCAD's constraint bar does: Delete
        /// takes the constraint away, Hide the bar of its object, Hide All every bar. False where no icon is there.
        /// </summary>
        private bool ConstraintBarMenu(Point at)
        {
            CadDocument document = Document;
            CadSpace space = WorkSpace;

            foreach (KeyValuePair<Rect, KeyValuePair<CadConstraintBar, CadConstraintIcon>> entry in _barIcons)
            {
                if (!entry.Key.Contains(at))
                {
                    continue;
                }

                CadConstraintBar bar = entry.Value.Key;
                CadConstraintIcon icon = entry.Value.Value;
                var items = new List<CadMenuItemViewModel> { new CadMenuItemViewModel("Delete " + icon.Label, () =>
                    {
                        CadEditResult result = document.ConstraintBars.Delete(space, icon.Index);

                        if (!result.Succeeded && result.Message != null)
                        {
                            Session?.Write(result.Message);
                        }

                        SelectionChanged?.Invoke(this, EventArgs.Empty);
                        InvalidateVisual();
                    }), new CadMenuItemViewModel("Hide", () =>
                    {
                        document.ConstraintBars.HideBars(new[] { bar.Handle });
                        InvalidateVisual();
                    }), new CadMenuItemViewModel("Hide All", () =>
                    {
                        document.ConstraintBars.HideBars(_barIcons.Select(b => b.Value.Key.Handle).Distinct());
                        InvalidateVisual();
                    }) };
                System.Windows.Controls.ContextMenu menu = CadMenus.Make(items);
                menu.PlacementTarget = this;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                menu.IsOpen = true;
                return true;
            }

            return false;
        }
    }

    /// <summary>The glyphs of the geometric constraints: strokes on a grid of 24, the same the Parametric tab's buttons draw.</summary>
    internal static class ConstraintGlyphs
    {
        private static readonly Dictionary<CadConstraintType, string> Paths = new Dictionary<CadConstraintType, string>
        {
            [CadConstraintType.Coincident] = "M12 12m-3 0a3 3 0 1 0 6 0a3 3 0 1 0-6 0M3 12h6M15 12h6",
            [CadConstraintType.Collinear] = "M3 8h8M13 16h8M3 8l18 8",
            [CadConstraintType.Concentric] =
                "M12 12m-8 0a8 8 0 1 0 16 0a8 8 0 1 0-16 0M12 12m-3 0a3 3 0 1 0 6 0a3 3 0 1 0-6 0",
            [CadConstraintType.Fix] = "M6 11h12v9H6zM8 11V8a4 4 0 0 1 8 0v3",
            [CadConstraintType.Parallel] = "M6 20 14 4M12 20 20 4",
            [CadConstraintType.Perpendicular] = "M4 20h16M12 20V5M12 15h5V20",
            [CadConstraintType.Horizontal] = "M3 12h18M7 8v8M17 8v8",
            [CadConstraintType.Vertical] = "M12 3v18M8 7h8M8 17h8",
            [CadConstraintType.Tangent] = "M12 15m-6 0a6 6 0 1 0 12 0a6 6 0 1 0-12 0M3 9h18",
            [CadConstraintType.Smooth] = "M3 18C9 18 9 6 15 6S21 6 21 6",
            [CadConstraintType.Symmetric] = "M12 3v18M8 7 4 12l4 5M16 7l4 5-4 5",
            [CadConstraintType.Equal] = "M5 9h14M5 15h14",
        };

        private static readonly Dictionary<CadConstraintType, System.Windows.Media.Geometry> Made =
            new Dictionary<CadConstraintType, System.Windows.Media.Geometry>();

        internal static System.Windows.Media.Geometry Of(CadConstraintType type)
        {
            lock (Made)
            {
                if (!Made.TryGetValue(type, out System.Windows.Media.Geometry geometry))
                {
                    geometry = Paths.TryGetValue(type, out string path)
                        ? System.Windows.Media.Geometry.Parse(path)
                        : null;
                    geometry?.Freeze();
                    Made[type] = geometry;
                }

                return geometry;
            }
        }
    }
}
