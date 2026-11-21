using System.Collections.Generic;

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
        /// Places every tile of a dialog, the dialog itself at its natural size. Until part P of L5 writes the layout, each
        /// tile is at the top-left with its <c>width</c> and <c>height</c> attributes (1 where absent) in character units.
        /// </summary>
        /// <param name="dialog">The dialog's top tile.</param>
        /// <param name="metrics">The character size and the text widths.</param>
        /// <returns>Each tile's rectangle.</returns>
        internal static IReadOnlyDictionary<DclTile, DclRect> Arrange(DclTile dialog, DclMetrics metrics)
        {
            var placed = new Dictionary<DclTile, DclRect>();
            Place(dialog, metrics, placed);

            foreach (DclTile tile in dialog.Descendants())
            {
                Place(tile, metrics, placed);
            }

            return placed;
        }

        private static void Place(DclTile tile, DclMetrics metrics, Dictionary<DclTile, DclRect> placed)
        {
            double width = (tile.GetReal("width") ?? 1) * metrics.CharWidth;
            double height = (tile.GetReal("height") ?? 1) * metrics.CharHeight;
            placed[tile] = new DclRect(0, 0, width, height);
        }
    }
}
