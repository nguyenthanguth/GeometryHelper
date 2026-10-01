using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// Breaking the closed shapes of the plane into the faces of a <see cref="GeoMesh2"/>: triangles, the cells of a grid,
    /// strips or convex pieces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every shape is first read as a face with straight edges: a loop with arcs flattened by the chord tolerance, a circle
    /// by the same, a polygon crossing itself as the region <see cref="GeoPolygon2.MakeValid()"/> reads. Its material is
    /// then broken up as <see cref="MeshOptions.Kind"/> says, within the tolerance, and the pieces joined: points the same
    /// but for rounding are one vertex, the shape's own corners kept exactly, and a corner of a face standing on the side of
    /// the face across from it is made one of that face's corners too, so that the faces meet edge to edge.
    /// </para>
    /// <para>
    /// A grid and strips run along the X axis unless an angle is given, and along a rectangle's own width; a grid stands
    /// against the shape as its alignments say, measured between the shape's furthest corners along each axis, or with a
    /// cell starting at its origin. The shapes' own <c>ToMesh</c> members come here.
    /// </para>
    /// </remarks>
    public static class Mesh2
    {
        #region Shapes

        /// <summary>
        /// Breaks a polygon into faces as the options say, using the default tolerance.
        /// </summary>
        public static GeoMesh2 ToMesh(GeoPolygon2 polygon, MeshOptions options) => ToMesh(polygon, options, Tolerance.Global);

        /// <summary>
        /// Breaks a polygon into faces as the options say, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon; one crossing itself is read as <see cref="GeoPolygon2.MakeValid()"/> reads it.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when the polygon encloses no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape or the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a grid's cell is no larger than the point tolerance, or the grid would lay more cells over the shape
        /// than a mesh may have.
        /// </exception>
        public static GeoMesh2 ToMesh(GeoPolygon2 polygon, MeshOptions options, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            Check(options);
            return Mesh(new GeoFace2(polygon), options, options.AngleRad ?? 0.0, tolerance);
        }

        /// <summary>
        /// Breaks a face into faces with no hole as the options say, using the default tolerance.
        /// </summary>
        public static GeoMesh2 ToMesh(GeoFace2 face, MeshOptions options) => ToMesh(face, options, Tolerance.Global);

        /// <summary>
        /// Breaks a face into faces with no hole as the options say, within a tolerance.
        /// </summary>
        /// <param name="face">The face; its holes are left open.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when the face encloses no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape or the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a grid's cell is no larger than the point tolerance, or the grid would lay more cells over the shape
        /// than a mesh may have.
        /// </exception>
        public static GeoMesh2 ToMesh(GeoFace2 face, MeshOptions options, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            Check(options);
            return Mesh(face, options, options.AngleRad ?? 0.0, tolerance);
        }

        /// <summary>
        /// Breaks a loop with arcs into faces as the options say, using the default tolerance.
        /// </summary>
        public static GeoMesh2 ToMesh(GeoPolygonArc2 loop, MeshOptions options) => ToMesh(loop, options, Tolerance.Global);

        /// <summary>
        /// Breaks a loop with arcs into faces as the options say, within a tolerance, each arc flattened by the options'
        /// chord tolerance first.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh of the flattened loop; one with no faces when it encloses no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape or the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a grid's cell is no larger than the point tolerance, or the grid would lay more cells over the shape
        /// than a mesh may have.
        /// </exception>
        public static GeoMesh2 ToMesh(GeoPolygonArc2 loop, MeshOptions options, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            Check(options);
            return Mesh(new GeoFace2(loop.Flatten(options.ChordTolerance)), options, options.AngleRad ?? 0.0, tolerance);
        }

        /// <summary>
        /// Breaks a rectangle into faces as the options say, using the default tolerance.
        /// </summary>
        public static GeoMesh2 ToMesh(GeoRectangle2 rectangle, MeshOptions options) => ToMesh(rectangle, options, Tolerance.Global);

        /// <summary>
        /// Breaks a rectangle into faces as the options say, within a tolerance, a grid and strips running along its own
        /// width unless the options give an angle.
        /// </summary>
        /// <param name="rectangle">The rectangle.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when the rectangle has no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a grid's cell is no larger than the point tolerance, or the grid would lay more cells over the shape
        /// than a mesh may have.
        /// </exception>
        /// <remarks>
        /// A grid of a rectangle along its own sides, starting at its lower left corner, lays its cells from there: with
        /// no joint, and a width and height that divide by the cell's, that is <see cref="Divide(GeoRectangle2, int, int)"/>.
        /// </remarks>
        public static GeoMesh2 ToMesh(GeoRectangle2 rectangle, MeshOptions options, Tolerance tolerance)
        {
            Check(options);

            if (!(rectangle.Width > tolerance.EqualPoint) || !(rectangle.Height > tolerance.EqualPoint))
            {
                return Empty(options.Kind, tolerance);
            }

            var corners = new GeoPolygon2(new[] { rectangle.LowerLeft, rectangle.LowerRight, rectangle.UpperRight, rectangle.UpperLeft }, 4);
            return Mesh(new GeoFace2(corners), options, options.AngleRad ?? rectangle.AngleRad, tolerance);
        }

        /// <summary>
        /// Breaks a disc into faces as the options say, using the default tolerance.
        /// </summary>
        public static GeoMesh2 ToMesh(GeoCircle2 circle, MeshOptions options) => ToMesh(circle, options, Tolerance.Global);

        /// <summary>
        /// Breaks a disc into faces as the options say, within a tolerance, its rim flattened by the options' chord
        /// tolerance first.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>
        /// The mesh of the polygon <see cref="GeoCircle2.ToPolygonByChordTolerance(double)"/> gives; its triangles fanned from
        /// the center, as <see cref="GeoCircle2.TriangulateSurface(double, Tolerance)"/> fans them. None when the radius is
        /// nought.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a grid's cell is no larger than the point tolerance, or the grid would lay more cells over the shape
        /// than a mesh may have.
        /// </exception>
        public static GeoMesh2 ToMesh(GeoCircle2 circle, MeshOptions options, Tolerance tolerance)
        {
            Check(options);

            if (!(circle.Radius > tolerance.EqualPoint))
            {
                return Empty(options.Kind, tolerance);
            }

            GeoPolygon2 rim = circle.ToPolygonByChordTolerance(options.ChordTolerance);

            if (options.Kind != MeshKind.Triangles)
            {
                return Mesh(new GeoFace2(rim), options, options.AngleRad ?? 0.0, tolerance);
            }

            var builder = new MeshBuilder2(tolerance, Scale(rim.Vertices), 2.0 * circle.Radius);
            builder.Seed(rim.Vertices);

            foreach (GeoTriangle2 triangle in Triangulation2.Fan(circle.Center, rim.Vertices, tolerance))
            {
                builder.Add(new[] { triangle.A, triangle.B, triangle.C }, false);
            }

            return builder.Build(MeshKind.Triangles);
        }

        /// <summary>
        /// The options a kind of mesh stands for on its own.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for a grid, which needs the size of its cells.</exception>
        internal static MeshOptions OptionsFor(MeshKind kind)
        {
            switch (kind)
            {
                case MeshKind.Triangles:
                    return MeshOptions.Triangles;
                case MeshKind.Convex:
                    return MeshOptions.Convex;
                case MeshKind.Grid:
                    throw new ArgumentException("A grid needs the size of its cells: pass MeshOptions.Grid(cellWidth, cellHeight).", nameof(kind));
                default:
                    return new MeshOptions(kind);
            }
        }

        #endregion

        #region Rectangles

        /// <summary>
        /// Divides a rectangle into equal rectangles along its own sides.
        /// </summary>
        /// <param name="rectangle">The rectangle.</param>
        /// <param name="columns">How many across its width.</param>
        /// <param name="rows">How many across its height.</param>
        /// <returns>
        /// The rectangles, turned as it is, row by row from its lower side and each row from its left: the one at column
        /// <c>c</c> and row <c>r</c> is at index <c>r * columns + c</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the columns or the rows are fewer than one.</exception>
        public static GeoRectangle2[] Divide(GeoRectangle2 rectangle, int columns, int rows)
        {
            if (columns < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), columns, "A rectangle divides into one column at the least.");
            }

            if (rows < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), rows, "A rectangle divides into one row at the least.");
            }

            double width = rectangle.Width / columns;
            double height = rectangle.Height / rows;
            double cos = Math.Cos(rectangle.AngleRad);
            double sin = Math.Sin(rectangle.AngleRad);
            var cells = new GeoRectangle2[columns * rows];

            for (int r = 0; r < rows; r++)
            {
                double y = (r + 0.5) * height - 0.5 * rectangle.Height;

                for (int c = 0; c < columns; c++)
                {
                    double x = (c + 0.5) * width - 0.5 * rectangle.Width;
                    var center = new GeoPoint2(rectangle.Center.X + x * cos - y * sin, rectangle.Center.Y + x * sin + y * cos);
                    cells[r * columns + c] = new GeoRectangle2(center, width, height, rectangle.AngleRad);
                }
            }

            return cells;
        }

        #endregion

        private static void Check(MeshOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
        }

        /// <summary>
        /// The largest coordinate among some points, which sets how far rounding can move one.
        /// </summary>
        private static double Scale(IEnumerable<GeoPoint2> points)
        {
            double scale = 0.0;

            foreach (GeoPoint2 point in points)
            {
                scale = Math.Max(scale, Math.Max(Math.Abs(point.X), Math.Abs(point.Y)));
            }

            return scale;
        }

        private static GeoMesh2 Empty(MeshKind kind, Tolerance tolerance) => new GeoMesh2(kind, new GeoPoint2[0], new int[0][], new bool[0], tolerance);

        /// <summary>
        /// Breaks the material of a straight-edged face up as the options say.
        /// </summary>
        private static GeoMesh2 Mesh(GeoFace2 face, MeshOptions options, double angleRad, Tolerance tolerance)
        {
            // The pieces are laid out from the boundary's first corner, or a grid's origin, and reach a cell past the shape.
            GeoPoint2 from = options.Kind == MeshKind.Grid && options.Origin.HasValue ? options.Origin.Value : face.Boundary[0];
            double reach = options.Kind == MeshKind.Grid ? 2.0 * (options.CellWidth + options.CellHeight + options.Joint) : 0.0;
            var builder = new MeshBuilder2(tolerance, Scale(face.Boundary.Vertices), ClipperRegion.Extent(face.Boundary.Vertices, from) + reach);
            builder.Seed(face.Boundary.Vertices);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                builder.Seed(hole.Vertices);
            }

            switch (options.Kind)
            {
                case MeshKind.Grid:
                    GridMesh2.Add(face, options, angleRad, tolerance, builder);
                    break;

                case MeshKind.Strips:
                    AddStrips(face, angleRad, tolerance, builder);
                    break;

                default:
                    foreach (GeoTriangle2 triangle in Triangulation2.Triangulate(face, tolerance))
                    {
                        builder.Add(new[] { triangle.A, triangle.B, triangle.C }, false);
                    }

                    break;
            }

            return builder.Build(options.Kind);
        }

        /// <summary>
        /// Cuts the material of a face into strips along a direction, laid on the plane z = 0 of space and cut as
        /// <see cref="GeoFace3.TriangulateSurface(Tolerance)"/> cuts a face it cannot clip ears from, the pieces kept whole.
        /// </summary>
        private static void AddStrips(GeoFace2 face, double angleRad, Tolerance tolerance, MeshBuilder2 builder)
        {
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            // The strip lines run along the frame's Y axis, so it is the direction asked for; X stands across it, to the
            // right, which keeps Z pointing up and the pieces counter-clockwise.
            var frame = new GeoCoordinateSystem3(
                new GeoPoint3(face.Boundary[0].X, face.Boundary[0].Y, 0.0),
                new GeoVector3(sin, -cos, 0.0),
                new GeoVector3(cos, sin, 0.0));

            foreach (GeoFace3 lifted in Triangulation2.Lift(face, tolerance))
            {
                foreach (GeoPoint3[] piece in StripTriangulation.Trapezoids(lifted, frame, tolerance))
                {
                    var corners = new List<GeoPoint2>(4);

                    foreach (GeoPoint3 corner in piece)
                    {
                        corners.Add(Triangulation2.Drop(corner));
                    }

                    builder.Add(corners, false);
                }
            }
        }
    }
}
