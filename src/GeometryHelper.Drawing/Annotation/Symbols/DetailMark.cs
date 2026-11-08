using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The mark of a detail in the view it is taken from: a circle or a rectangle round the area the detail shows, and its
    /// letter beside it at the upper right, in view millimetres at 1:1.
    /// </summary>
    /// <remarks>
    /// The detail B of a beam's end, a circle of radius 250 about (0, 0) in the view, drawn with a text 2.5 high at 1:20, has
    /// its letter 50 high in the view, its box's lower left corner 15 up and to the right of the circle's point at 45
    /// degrees, (191.8, 191.8). A mark does not change once made.
    /// </remarks>
    public sealed class DetailMark
    {
        private DetailMark(string id, string letter, DetailMarkShape shape, GeoRectangle2 bounds, double textHeight)
        {
            Id = SymbolChecks.Text(id, nameof(id));
            Letter = SymbolChecks.Text(letter, nameof(letter));
            TextHeight = SymbolChecks.Positive(textHeight, nameof(textHeight));
            Shape = shape;
            Bounds = bounds;
        }

        /// <summary>Gets the id of the mark.</summary>
        public string Id { get; }

        /// <summary>Gets the letter of the detail.</summary>
        public string Letter { get; }

        /// <summary>Gets the outline drawn round the area.</summary>
        public DetailMarkShape Shape { get; }

        /// <summary>
        /// Gets the area the detail shows, square to the view's axes, in view millimetres: for a circle, the square round it.
        /// </summary>
        public GeoRectangle2 Bounds { get; }

        /// <summary>Gets the height of the letter in the view, in view millimetres.</summary>
        public double TextHeight { get; }

        /// <summary>
        /// Gets the box of the letter, square to the view's axes: its lower left corner 0.3 of the height up and to the
        /// right of the circle's point at 45 degrees, or of the rectangle's upper right corner.
        /// </summary>
        public GeoRectangle2 LetterBox
        {
            get
            {
                double gap = 0.3 * TextHeight;
                double half = 0.5 * Bounds.Width;
                GeoPoint2 corner = Shape == DetailMarkShape.Circle
                    ? new GeoPoint2(Bounds.Center.X + half * Math.Sqrt(0.5), Bounds.Center.Y + half * Math.Sqrt(0.5))
                    : new GeoPoint2(Bounds.Center.X + half, Bounds.Center.Y + 0.5 * Bounds.Height);
                return Box2.FromCorner(corner.X + gap, corner.Y + gap, SymbolDxf.TextWidth(Letter, TextHeight), TextHeight);
            }
        }

        /// <summary>
        /// Makes the mark of a detail drawn as a circle.
        /// </summary>
        /// <param name="id">The id of the mark, unique in its view.</param>
        /// <param name="letter">The letter of the detail: "B".</param>
        /// <param name="centre">The centre of the circle, in view millimetres.</param>
        /// <param name="radius">The radius of the circle, in view millimetres.</param>
        /// <param name="textHeight">The height of the letter in the view, in view millimetres.</param>
        /// <returns>The mark.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the id or the letter is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the id or the letter is empty, or the centre is not of finite numbers.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius or the height is not a finite number above nought.</exception>
        public static DetailMark Circle(string id, string letter, GeoPoint2 centre, double radius, double textHeight)
        {
            SymbolChecks.Finite(centre, nameof(centre));
            SymbolChecks.Positive(radius, nameof(radius));
            return new DetailMark(id, letter, DetailMarkShape.Circle, new GeoRectangle2(centre, 2.0 * radius, 2.0 * radius), textHeight);
        }

        /// <summary>
        /// Makes the mark of a detail drawn as a rectangle.
        /// </summary>
        /// <param name="id">The id of the mark, unique in its view.</param>
        /// <param name="letter">The letter of the detail: "B".</param>
        /// <param name="area">The area, in view millimetres; it is taken square to the view's axes, round its corners.</param>
        /// <param name="textHeight">The height of the letter in the view, in view millimetres.</param>
        /// <returns>The mark.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the id or the letter is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the id or the letter is empty, or the area has no size or is not of
        /// finite numbers.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is not a finite number above nought.</exception>
        public static DetailMark Rectangle(string id, string letter, GeoRectangle2 area, double textHeight)
        {
            Box2.Bounds(area, out double x0, out double y0, out double x1, out double y1);

            if (!(x1 - x0 > 0.0) || !(y1 - y0 > 0.0) || double.IsInfinity(x1 - x0) || double.IsInfinity(y1 - y0))
            {
                throw new ArgumentException("The area has to have a size of finite numbers.", nameof(area));
            }

            return new DetailMark(id, letter, DetailMarkShape.Rectangle, Box2.FromCorner(x0, y0, x1 - x0, y1 - y0), textHeight);
        }

        /// <summary>
        /// Writes the mark into the model space of a drawing: the circle as a CIRCLE or the rectangle as a closed LWPOLYLINE,
        /// and the letter as an MTEXT.
        /// </summary>
        /// <param name="database">The drawing to write into; what it holds already is left as it is.</param>
        /// <param name="placement">Where the view is drawn on the sheet, and at what scale.</param>
        /// <remarks>
        /// Both are on the layer <see cref="SymbolSettings.Layer"/>, made in colour 6 where the drawing has none, and tagged
        /// with <see cref="DrawingEntityTags"/>: the mark's id and the placement's view id.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public void WriteTo(DxfDatabase database, ViewPlacement2 placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            SymbolDxf.SymbolLayer(database);

            if (Shape == DetailMarkShape.Circle)
            {
                var circle = new DxfCircle(placement.ToSheet(Bounds.Center), GeoVector3.ZAxis, 0.5 * Bounds.Width / placement.Scale) { Layer = SymbolSettings.Layer };
                database.ModelSpace.AppendEntity(circle);
                DrawingEntityTags.Tag(circle, Id, placement.ViewId);
            }
            else
            {
                SymbolDxf.Outline(database, placement, new[] { Bounds.LowerLeft, Bounds.LowerRight, Bounds.UpperRight, Bounds.UpperLeft }, SymbolSettings.Layer, Id);
            }

            SymbolDxf.Text(database, placement, LetterBox.Center, TextHeight, Letter, SymbolSettings.Layer, Id);
        }

        /// <inheritdoc/>
        public override string ToString() => $"Detail mark {Id} {Letter} ({Shape})";
    }
}
