using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// A level mark of a general arrangement view: a triangle standing on its point at the level, a line along its top
    /// running on to the right, and the elevation written on that line, in view millimetres at 1:1.
    /// </summary>
    /// <remarks>
    /// The top of a base plate 25 thick at the elevation 25 is marked "+25", the ground "±0", a footing's underside 150
    /// down "-150": the elevation in whole millimetres, with its sign. With a text 2.5 high at 1:50, the triangle is 125
    /// across and 100 high in the view and the text stands on its top line. A mark does not change once made.
    /// </remarks>
    public sealed class LevelMark
    {
        /// <summary>
        /// The attribute of an object a drawing reads its elevation from, where <see cref="SymbolSettings.LevelMarks"/> is on:
        /// "Elevation", the level of the object's top in model millimetres, written with a point for decimals.
        /// </summary>
        public const string ElevationAttribute = "Elevation";

        /// <summary>
        /// Makes a level mark.
        /// </summary>
        /// <param name="id">The id of the mark, unique in its view.</param>
        /// <param name="point">The point at the level the triangle's point stands on, in view millimetres.</param>
        /// <param name="elevation">The elevation written, in model millimetres.</param>
        /// <param name="textHeight">The height of the text in the view, in view millimetres.</param>
        /// <exception cref="ArgumentNullException">Thrown when the id is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the id is empty, the point is not of finite numbers, or the
        /// elevation is not a finite number.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is not a finite number above nought.</exception>
        public LevelMark(string id, GeoPoint2 point, double elevation, double textHeight)
        {
            Id = SymbolChecks.Text(id, nameof(id));
            Point = SymbolChecks.Finite(point, nameof(point));
            TextHeight = SymbolChecks.Positive(textHeight, nameof(textHeight));

            if (double.IsNaN(elevation) || double.IsInfinity(elevation))
            {
                throw new ArgumentException("The elevation has to be a finite number.", nameof(elevation));
            }

            Elevation = elevation;
            string rounded = DrawingDimension.Format(elevation, 1.0);
            Text = rounded == "0" ? "±0" : rounded[0] == '-' ? rounded : "+" + rounded;
        }

        /// <summary>Gets the id of the mark.</summary>
        public string Id { get; }

        /// <summary>Gets the point at the level, in view millimetres.</summary>
        public GeoPoint2 Point { get; }

        /// <summary>Gets the elevation, in model millimetres, every digit kept.</summary>
        public double Elevation { get; }

        /// <summary>Gets the text written: the elevation in whole millimetres with its sign, "+25", "±0" or "-150".</summary>
        public string Text { get; }

        /// <summary>Gets the height of the text in the view, in view millimetres.</summary>
        public double TextHeight { get; }

        /// <summary>
        /// Gets the box of the text, square to the view's axes: on the top line of the triangle, 0.2 of the height right of
        /// the triangle and above the line.
        /// </summary>
        public GeoRectangle2 TextBox
            => Box2.FromCorner(Point.X + 0.7 * TextHeight, Point.Y + 1.0 * TextHeight, SymbolDxf.TextWidth(Text, TextHeight), TextHeight);

        /// <summary>
        /// Writes the mark into the model space of a drawing: the triangle as a closed LWPOLYLINE, its top line as a LINE and
        /// the text as an MTEXT.
        /// </summary>
        /// <param name="database">The drawing to write into; what it holds already is left as it is.</param>
        /// <param name="placement">Where the view is drawn on the sheet, and at what scale.</param>
        /// <remarks>
        /// The triangle is as wide as the text is high and 0.8 of that high. Everything is on the layer
        /// <see cref="SymbolSettings.Layer"/>, made in colour 6 where the drawing has none, and tagged with
        /// <see cref="DrawingEntityTags"/>: the mark's id and the placement's view id.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public void WriteTo(DxfDatabase database, ViewPlacement2 placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            Write(database, placement, Id);
        }

        /// <summary>Writes the symbol as <see cref="WriteTo"/> does, its entities tagged with an id the caller gives.</summary>
        internal void Write(DxfDatabase database, ViewPlacement2 placement, string id)
        {
            SymbolDxf.SymbolLayer(database);
            double half = 0.5 * TextHeight, top = Point.Y + 0.8 * TextHeight;
            SymbolDxf.Outline(database, placement, new[] { Point, new GeoPoint2(Point.X + half, top), new GeoPoint2(Point.X - half, top) }, SymbolSettings.Layer, id);
            GeoRectangle2 box = TextBox;
            SymbolDxf.Line(database, placement, new GeoPoint2(Point.X - half, top), new GeoPoint2(box.Center.X + 0.5 * box.Width + 0.2 * TextHeight, top), SymbolSettings.Layer, id);
            SymbolDxf.Text(database, placement, box.Center, TextHeight, Text, SymbolSettings.Layer, id);
        }

        /// <inheritdoc/>
        public override string ToString() => $"Level mark {Id} {Text}";
    }
}
