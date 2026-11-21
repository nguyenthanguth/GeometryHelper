using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>A rectangle of a laid-out tile, in device-independent pixels from the dialog's top-left.</summary>
    internal struct DclRect
    {
        internal DclRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>Gets the left.</summary>
        internal double X { get; }

        /// <summary>Gets the top.</summary>
        internal double Y { get; }

        /// <summary>Gets the width.</summary>
        internal double Width { get; }

        /// <summary>Gets the height.</summary>
        internal double Height { get; }
    }

    /// <summary>Lays a dialog's tiles out as DCL does: rows and columns, minimum sizes, room to spare shared, alignment.</summary>
    internal static class DclLayout
    {
        /// <summary>
        /// Places every tile of a dialog, the dialog itself at its natural size (D310-D312). Each tile has a natural size:
        /// the content's, at least the <c>width</c> and <c>height</c> the tile names in character units. A row gives its
        /// children their natural widths and shares the room it has to spare equally among those that are not fixed, a column
        /// does the same with heights; across its direction a child that is not fixed fills the cluster and a fixed one keeps
        /// its natural size, placed by its <c>alignment</c>. A tile has a margin on each side (<see cref="DclMetrics.TileMarginX"/>
        /// and <see cref="DclMetrics.TileMarginY"/>, or none for <c>horizontal_margin = none</c> and <c>vertical_margin =
        /// none</c>; a spacer's too); the rectangle of a tile is what is left inside its margin.
        /// </summary>
        /// <param name="dialog">The dialog's top tile.</param>
        /// <param name="metrics">The character size, the text widths and the paddings.</param>
        /// <returns>
        /// Each tile's rectangle, looked up by the tile. A dialog of one button 49 by 22 is 63 by 36:
        /// the button's margin of 4 on each side and the dialog's of 3.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the dialog or the metrics are null.</exception>
        internal static IReadOnlyDictionary<DclTile, DclRect> Arrange(DclTile dialog, DclMetrics metrics)
        {
            if (dialog == null)
            {
                throw new ArgumentNullException(nameof(dialog));
            }

            if (metrics == null)
            {
                throw new ArgumentNullException(nameof(metrics));
            }

            var arranger = new Arranger(metrics);
            Size natural = arranger.Natural(dialog);
            arranger.Place(dialog, new DclRect(0, 0, natural.Width, natural.Height));
            return arranger.Placed;
        }

        /// <summary>
        /// Gives the width a label takes in front of an edit box or a popup list: the label's text without its access mark and
        /// the gap after it (none: the box starts where the label ends); 0 for a tile with no label. A window uses it to split the tile's rectangle into the label and the
        /// box.
        /// </summary>
        /// <param name="tile">The tile.</param>
        /// <param name="metrics">The text widths and the gap.</param>
        /// <returns>The label's width with the gap, in pixels.</returns>
        internal static double LabelWidth(DclTile tile, DclMetrics metrics)
        {
            string label = StripAccess(tile.Get("label"));
            return label.Length == 0 ? 0 : metrics.TextWidth(label) + metrics.LabelGap;
        }

        /// <summary>
        /// Takes the access mark out of a label: <c>&amp;Help</c> is <c>Help</c> and <c>&amp;&amp;</c> is one <c>&amp;</c>.
        /// </summary>
        /// <param name="label">The label as written, or null.</param>
        /// <returns>The label as it is shown; empty for null.</returns>
        internal static string StripAccess(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return string.Empty;
            }

            var text = new StringBuilder(label.Length);

            for (int i = 0; i < label.Length; i++)
            {
                if (label[i] == '&' && i + 1 < label.Length)
                {
                    i++;
                }
                else if (label[i] == '&')
                {
                    continue;
                }

                text.Append(label[i]);
            }

            return text.ToString();
        }

        private readonly struct Size
        {
            internal Size(double width, double height)
            {
                Width = width;
                Height = height;
            }

            internal double Width { get; }
            internal double Height { get; }
        }

        private sealed class Arranger
        {
            private readonly DclMetrics _metrics;
            private readonly Dictionary<DclTile, Size> _natural = new Dictionary<DclTile, Size>();

            internal Arranger(DclMetrics metrics)
            {
                _metrics = metrics;
            }

            internal Dictionary<DclTile, DclRect> Placed { get; } = new Dictionary<DclTile, DclRect>();

            /// <summary>The size of a tile without its margin.</summary>
            internal Size Natural(DclTile tile)
            {
                if (_natural.TryGetValue(tile, out Size known))
                {
                    return known;
                }

                double minWidth = (tile.GetReal("width") ?? 0) * _metrics.CharWidth;
                double minHeight = (tile.GetReal("height") ?? 0) * _metrics.CharHeight;
                Size size;

                switch (tile.Kind)
                {
                    case "dialog":
                    case "cluster":
                    case "radio_cluster":
                        size = ClusterSize(tile, minWidth, minHeight);
                        break;
                    case "text":
                    {
                        string shown = tile.Get("value");

                        if (string.IsNullOrEmpty(shown))
                        {
                            shown = tile.Get("label");
                        }

                        size = new Size(
                            Math.Max(_metrics.TextWidth(StripAccess(shown)), minWidth),
                            Math.Max(_metrics.CharHeight, minHeight)
                        );
                        break;
                    }
                    case "button":
                        // At least width x 6 + 1 where a width is given (a button of width 8 is 49).
                        size = new Size(
                            Math.Max(
                                _metrics.TextWidth(StripAccess(tile.Get("label"))) + _metrics.ButtonPaddingX,
                                tile.GetReal("width").HasValue ? minWidth + 1 : 0
                            ),
                            Math.Max(_metrics.ButtonHeight, minHeight)
                        );
                        break;
                    case "edit_box":
                    case "popup_list":
                    {
                        // The box is edit_width characters and its frame, or at least ten characters where the tile says none (or
                        // 0); it follows its label with no gap.
                        double editWidth = tile.GetReal("edit_width") ?? 0;
                        double box = (editWidth > 0 ? editWidth : 10) * _metrics.CharWidth + _metrics.EditBoxPadding;
                        size = new Size(
                            Math.Max(LabelWidth(tile, _metrics) + box, minWidth),
                            Math.Max(tile.Kind == "edit_box" ? _metrics.EditHeight : _metrics.PopupHeight, minHeight)
                        );
                        break;
                    }
                    case "list_box":
                        // Its label is inside its height: a list of height 6 with a label is a tile of 68, as without one.
                        size = new Size(
                            (tile.GetReal("width") ?? 10) * _metrics.CharWidth,
                            Math.Max(0, (tile.GetReal("height") ?? 10) * _metrics.ListLineHeight - _metrics.ListTrim)
                        );
                        break;
                    case "toggle":
                    case "radio_button":
                        size = new Size(
                            Math.Max(
                                _metrics.CheckBoxWidth + _metrics.TextWidth(StripAccess(tile.Get("label"))),
                                minWidth
                            ),
                            Math.Max(tile.Kind == "toggle" ? _metrics.ToggleHeight : _metrics.RadioHeight, minHeight)
                        );
                        break;
                    case "slider":
                        size = string.Equals(tile.Get("layout"), "vertical", StringComparison.Ordinal)
                            ? new Size(
                                Math.Max(_metrics.SliderThickness, minWidth),
                                (tile.GetReal("height") ?? 10) * _metrics.CharHeight
                            )
                            : new Size(
                                (tile.GetReal("width") ?? 10) * _metrics.CharWidth,
                                Math.Max(_metrics.SliderThickness, minHeight)
                            );
                        break;
                    case "image":
                    case "image_button":
                        size = ImageSize(tile);
                        break;
                    default:
                        // A spacer, and a tile of a kind a hand-built dialog made up: its width and height, 1 by 1 where absent.
                        size = new Size(
                            (tile.GetReal("width") ?? 1) * _metrics.CharWidth,
                            (tile.GetReal("height") ?? 1) * _metrics.CharHeight
                        );
                        break;
                }

                _natural[tile] = size;
                return size;
            }

            internal void Place(DclTile tile, DclRect area)
            {
                Placed[tile] = area;

                if (!IsCluster(tile) || tile.Children.Count == 0)
                {
                    return;
                }

                bool vertical = IsVertical(tile);
                Insets inset = InsetsOf(tile);
                double innerX = area.X + inset.Left;
                double innerY = area.Y + inset.Top;
                double innerWidth = Math.Max(0, area.Width - inset.Left - inset.Right);
                double innerHeight = Math.Max(0, area.Height - inset.Top - inset.Bottom);
                double innerMain = vertical ? innerHeight : innerWidth;
                double innerCross = vertical ? innerWidth : innerHeight;

                // Room to spare along the axis goes equally to the children that are not fixed along it.
                double used = 0;
                int stretching = 0;

                foreach (DclTile child in tile.Children)
                {
                    Size slot = SlotOf(child);
                    used += vertical ? slot.Height : slot.Width;

                    if (!IsFixed(child, tile, !vertical))
                    {
                        stretching++;
                    }
                }

                double extra = innerMain > used && stretching > 0 ? (innerMain - used) / stretching : 0;
                double position = vertical ? innerY : innerX;

                foreach (DclTile child in tile.Children)
                {
                    Size slot = SlotOf(child);
                    double main = (vertical ? slot.Height : slot.Width) + (IsFixed(child, tile, !vertical) ? 0 : extra);
                    double cross = vertical ? slot.Width : slot.Height;
                    double crossOffset = 0;

                    if (IsFixed(child, tile, vertical))
                    {
                        crossOffset = Align(child, tile, vertical, Math.Max(0, innerCross - cross));
                    }
                    else
                    {
                        cross = innerCross;
                    }

                    double x = vertical ? innerX + crossOffset : position;
                    double y = vertical ? position : innerY + crossOffset;
                    double width = vertical ? cross : main;
                    double height = vertical ? main : cross;
                    double marginX = MarginX(child);
                    double marginY = MarginY(child);

                    Place(
                        child,
                        new DclRect(
                            x + marginX,
                            y + marginY,
                            Math.Max(0, width - 2 * marginX),
                            Math.Max(0, height - 2 * marginY)
                        )
                    );
                    position += main;
                }
            }

            private Size ClusterSize(DclTile tile, double minWidth, double minHeight)
            {
                bool vertical = IsVertical(tile);
                Insets inset = InsetsOf(tile);
                double along = 0;
                double across = 0;

                foreach (DclTile child in tile.Children)
                {
                    Size slot = SlotOf(child);
                    along += vertical ? slot.Height : slot.Width;
                    across = Math.Max(across, vertical ? slot.Width : slot.Height);
                }

                double width = (vertical ? across : along) + inset.Left + inset.Right;
                double height = (vertical ? along : across) + inset.Top + inset.Bottom;

                if (IsBoxed(tile))
                {
                    // The frame has to be wide enough for its label.
                    width = Math.Max(
                        width,
                        _metrics.TextWidth(StripAccess(tile.Get("label"))) + 2 * _metrics.BoxBorder
                    );
                }

                return new Size(Math.Max(width, minWidth), Math.Max(height, minHeight));
            }

            private Size ImageSize(DclTile tile)
            {
                double? width = tile.GetReal("width");
                double? height = tile.GetReal("height");
                double aspect = tile.GetReal("aspect_ratio") ?? 0;
                double widthPixels;
                double heightPixels;

                // Width and height as given; one missing comes from the other through aspect_ratio, the height over the width
                // (width 10 and aspect_ratio 0.5 is 60 by 30, as AutoCAD 2021 draws it), and with neither the width is one character.
                if (height.HasValue && !width.HasValue)
                {
                    heightPixels = height.Value * _metrics.CharHeight;
                    widthPixels = aspect > 0 ? heightPixels / aspect : _metrics.CharWidth;
                }
                else
                {
                    widthPixels = (width ?? 1) * _metrics.CharWidth;
                    heightPixels =
                        height.HasValue ? height.Value * _metrics.CharHeight
                        : aspect > 0 ? widthPixels * aspect
                        : _metrics.CharHeight;
                }

                return new Size(widthPixels, heightPixels);
            }

            private Size SlotOf(DclTile tile)
            {
                Size natural = Natural(tile);
                return new Size(natural.Width + 2 * MarginX(tile), natural.Height + 2 * MarginY(tile));
            }

            private double MarginX(DclTile tile) => Margin(tile, "horizontal_margin", _metrics.TileMarginX);

            private double MarginY(DclTile tile) => Margin(tile, "vertical_margin", _metrics.TileMarginY);

            private static double Margin(DclTile tile, string attribute, double standard)
            {
                return string.Equals(tile.Get(attribute), "none", StringComparison.Ordinal) ? 0 : standard;
            }

            private static bool IsCluster(DclTile tile) =>
                tile.Kind == "dialog" || tile.Kind == "cluster" || tile.Kind == "radio_cluster";

            private static bool IsVertical(DclTile tile) =>
                tile.Kind == "dialog" || string.Equals(tile.Get("layout"), "vertical", StringComparison.Ordinal);

            private static bool IsBoxed(DclTile tile) => tile.Kind != "dialog" && tile.IsTrue("boxed");

            private Insets InsetsOf(DclTile tile)
            {
                if (tile.Kind == "dialog")
                {
                    return new Insets(
                        _metrics.DialogMargin,
                        _metrics.DialogMargin,
                        _metrics.DialogMargin,
                        _metrics.DialogMargin
                    );
                }

                if (IsBoxed(tile))
                {
                    return new Insets(
                        _metrics.BoxBorder,
                        _metrics.CharHeight + _metrics.BoxBorder / 2,
                        _metrics.BoxBorder,
                        _metrics.BoxBorder
                    );
                }

                return new Insets(0, 0, 0, 0);
            }

            /// <summary>
            /// Whether a child keeps its natural size along an axis: its own <c>fixed_width</c> or <c>fixed_height</c>, else the
            /// cluster's <c>children_fixed_width</c> or <c>children_fixed_height</c>.
            /// </summary>
            private static bool IsFixed(DclTile child, DclTile cluster, bool horizontal)
            {
                string own = horizontal ? "fixed_width" : "fixed_height";
                string inherited = horizontal ? "children_fixed_width" : "children_fixed_height";
                return child.Get(own) != null ? child.IsTrue(own) : cluster.IsTrue(inherited);
            }

            /// <summary>
            /// The offset of a fixed child across its cluster: <c>left</c>, <c>centered</c> or <c>right</c> in a column,
            /// <c>top</c>, <c>centered</c> or <c>bottom</c> in a row, from the child's <c>alignment</c> or the cluster's
            /// <c>children_alignment</c>; a column's child is at the left and a row's centred where neither says.
            /// </summary>
            private static double Align(DclTile child, DclTile cluster, bool vertical, double room)
            {
                string alignment = child.Get("alignment") ?? cluster.Get("children_alignment");

                if (string.Equals(alignment, "centered", StringComparison.Ordinal))
                {
                    return room / 2;
                }

                if (vertical)
                {
                    return string.Equals(alignment, "right", StringComparison.Ordinal) ? room : 0;
                }

                if (string.Equals(alignment, "top", StringComparison.Ordinal))
                {
                    return 0;
                }

                return string.Equals(alignment, "bottom", StringComparison.Ordinal) ? room : room / 2;
            }

            private readonly struct Insets
            {
                internal Insets(double left, double top, double right, double bottom)
                {
                    Left = left;
                    Top = top;
                    Right = right;
                    Bottom = bottom;
                }

                internal double Left { get; }
                internal double Top { get; }
                internal double Right { get; }
                internal double Bottom { get; }
            }
        }
    }
}
