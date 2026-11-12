using System;
using System.Collections.Generic;
using GeometryHelper.Clipper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// What the polygons of one packing share: the grid they are worked out on, the gaps they keep, and the no-fit
    /// polygons of each pair of them, which are made once and moved to wherever the pair stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two polygons keep a gap apart when each, grown by half of it, leaves the other alone. A polygon grown that way
    /// is broken into convex pieces, and the places where one polygon, held by a reference point at the lower left
    /// corner of its box, may not stand beside another are the union of the Minkowski sums of the pieces of one with
    /// the pieces of the other turned about the point: the no-fit polygon. Each sum is drawn in by a quarter of the
    /// tolerance, so that a polygon that fits a notch exactly, or the edge of the sheet, still does.
    /// </para>
    /// <para>
    /// The sums are made on whole steps of the grid, a tenth of the tolerance or finer, so that the free places, found
    /// with Clipper2, are exact for the pieces as they were rounded to it.
    /// </para>
    /// </remarks>
    internal sealed class PolygonContext
    {
        /// <summary>The gap between two polygons of one block, laid out together.</summary>
        internal const int Spacing = 0;

        /// <summary>The gap between two blocks.</summary>
        internal const int Group = 1;

        /// <summary>
        /// How far a sharp corner may reach, as a multiple of the offset: a corner sharper than a right angle is cut
        /// off square beyond twice the distance, so that a thin spike does not keep a view away from the sheet.
        /// </summary>
        private const double MiterLimit = 2.0;

        private static readonly OffsetOptions Corners = new OffsetOptions(OffsetJoin.Miter, MiterLimit);

        private readonly double[] _radius;
        private readonly long _shapeCount;
        private readonly Dictionary<long, NoFit> _noFits = new Dictionary<long, NoFit>();

        /// <summary>
        /// Initializes the context of a packing.
        /// </summary>
        /// <param name="step">The size of one step of the grid.</param>
        /// <param name="tolerance">The tolerance: the least gap that counts, and what a polygon may pass a free place by.</param>
        /// <param name="spacing">The gap between two polygons of one block, in the units of the polygons.</param>
        /// <param name="groupSpacing">The gap between two blocks, in the units of the polygons.</param>
        /// <param name="shapeCount">How many shapes there are.</param>
        internal PolygonContext(double step, Tolerance tolerance, double spacing, double groupSpacing, int shapeCount)
        {
            Step = step;
            Tolerance = tolerance;
            _radius = new[] { spacing * 0.5, groupSpacing * 0.5 };
            _shapeCount = Math.Max(1, shapeCount);
            Slack = Math.Max(1L, (long)(tolerance.EqualPoint * 0.25 / step));
            FitSlack = (long)(tolerance.EqualPoint * 0.5 / step);
        }

        /// <summary>Gets the size of one step of the grid.</summary>
        internal double Step { get; }

        /// <summary>Gets the tolerance.</summary>
        internal Tolerance Tolerance { get; }

        /// <summary>
        /// Gets how many steps a no-fit polygon is drawn in by, and the places a polygon may stand are let out past the
        /// sheet by.
        /// </summary>
        internal long Slack { get; }

        /// <summary>Gets how many steps wider or higher than a sheet a polygon may be and still go on it.</summary>
        internal long FitSlack { get; }

        /// <summary>
        /// Gets the convex pieces a shape is made of, grown by half of the gap of a kind.
        /// </summary>
        internal Paths64 PiecesOf(PolygonShape shape, int kind)
        {
            Paths64 pieces = shape.Pieces[kind];
            if (pieces != null)
            {
                return pieces;
            }

            if (kind == Group && _radius[Group] == _radius[Spacing])
            {
                pieces = PiecesOf(shape, Spacing);
            }
            else
            {
                pieces = BuildPieces(shape, _radius[kind]);
            }

            shape.Pieces[kind] = pieces;
            return pieces;
        }

        /// <summary>
        /// Gets the no-fit polygon of two shapes: where the reference point of the second may not stand, with the first's
        /// at nought, for the gap of a kind.
        /// </summary>
        internal NoFit GetNoFit(PolygonShape placed, PolygonShape moving, int kind)
        {
            long key = (placed.Id * _shapeCount + moving.Id) * 2 + kind;
            if (_noFits.TryGetValue(key, out NoFit found))
            {
                return found;
            }

            var paths = new Paths64();
            foreach (Path64 a in PiecesOf(placed, kind))
            {
                foreach (Path64 b in PiecesOf(moving, kind))
                {
                    Path64 sum = Minkowski(a, Negate(b));
                    paths.AddRange(Draw(sum, Slack));
                }
            }

            var made = new NoFit(paths);
            _noFits.Add(key, made);
            return made;
        }

        private Paths64 BuildPieces(PolygonShape shape, double radius)
        {
            var pieces = new Paths64();
            var face = new GeoFace2(new GeoPolygon2(shape.Local, shape.Local.Length));
            GeoFace2[] faces = radius > 0.0 ? face.Offset(radius, Corners, Tolerance) : new[] { face };
            foreach (GeoFace2 grown in faces)
            {
                foreach (GeoPolygon2 piece in grown.ToMesh(MeshOptions.Convex, Tolerance).GetFaces())
                {
                    var corners = new List<Point64>(piece.VertexCount);
                    foreach (GeoPoint2 vertex in piece.Vertices)
                    {
                        corners.Add(new Point64(vertex.X / Step, vertex.Y / Step));
                    }

                    Path64 hull = Hull(corners);
                    if (hull != null)
                    {
                        pieces.Add(hull);
                    }
                }
            }

            if (pieces.Count == 0)
            {
                // A shape too thin for the mesh to see is kept by the box round it.
                long grow = (long)Math.Ceiling(radius / Step);
                pieces.Add(
                    new Path64(4)
                    {
                        new Point64(-grow, -grow),
                        new Point64(shape.Width + grow, -grow),
                        new Point64(shape.Width + grow, shape.Height + grow),
                        new Point64(-grow, shape.Height + grow),
                    }
                );
            }

            return pieces;
        }

        /// <summary>
        /// The convex hull of points, counter-clockwise, with no point on a side but its corners; null when they do not
        /// enclose an area.
        /// </summary>
        internal static Path64 Hull(List<Point64> points)
        {
            points.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            var hull = new List<Point64>(points.Count * 2);
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int i = 0; i < points.Count; i++)
                {
                    Point64 point = points[pass == 0 ? i : points.Count - 1 - i];
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0.0)
                    {
                        hull.RemoveAt(hull.Count - 1);
                    }

                    hull.Add(point);
                }

                hull.RemoveAt(hull.Count - 1);
            }

            return hull.Count < 3 ? null : new Path64(hull);
        }

        private static double Cross(Point64 a, Point64 b, Point64 c) =>
            (double)(b.X - a.X) * (c.Y - a.Y) - (double)(b.Y - a.Y) * (c.X - a.X);

        private static Path64 Negate(Path64 path)
        {
            var turned = new Path64(path.Count);
            foreach (Point64 point in path)
            {
                turned.Add(new Point64(-point.X, -point.Y));
            }

            return turned;
        }

        /// <summary>
        /// The Minkowski sum of two convex paths that run counter-clockwise: the sides of both, merged in the order of their
        /// directions, from the lowest corner of each.
        /// </summary>
        internal static Path64 Minkowski(Path64 p, Path64 q)
        {
            int n = p.Count,
                m = q.Count;
            int i0 = Lowest(p),
                j0 = Lowest(q);
            var sum = new Path64(n + m);
            int i = 0,
                j = 0;
            for (int guard = 0; (i < n || j < m) && guard < n + m + 2; guard++)
            {
                Point64 a = p[(i0 + i) % n],
                    b = q[(j0 + j) % m];
                sum.Add(new Point64(a.X + b.X, a.Y + b.Y));
                Point64 a2 = p[(i0 + i + 1) % n],
                    b2 = q[(j0 + j + 1) % m];
                double cross = (double)(a2.X - a.X) * (b2.Y - b.Y) - (double)(a2.Y - a.Y) * (b2.X - b.X);
                if (cross >= 0.0 && i < n)
                {
                    i++;
                }

                if (cross <= 0.0 && j < m)
                {
                    j++;
                }
            }

            return sum;
        }

        private static int Lowest(Path64 path)
        {
            int lowest = 0;
            for (int i = 1; i < path.Count; i++)
            {
                if (path[i].Y < path[lowest].Y || (path[i].Y == path[lowest].Y && path[i].X < path[lowest].X))
                {
                    lowest = i;
                }
            }

            return lowest;
        }

        private static Paths64 Draw(Path64 sum, long slack)
        {
            var one = new Paths64 { sum };
            return Clipper2.InflatePaths(one, -slack, JoinType.Miter, EndType.Polygon, MiterLimit);
        }
    }

    /// <summary>
    /// A no-fit polygon of two shapes: the paths that bound the places the reference point of the second may not stand,
    /// with that of the first at nought, and the box round them.
    /// </summary>
    internal sealed class NoFit
    {
        internal NoFit(Paths64 paths)
        {
            Paths = paths;
            MinX = long.MaxValue;
            MinY = long.MaxValue;
            MaxX = long.MinValue;
            MaxY = long.MinValue;
            foreach (Path64 path in paths)
            {
                foreach (Point64 point in path)
                {
                    MinX = Math.Min(MinX, point.X);
                    MinY = Math.Min(MinY, point.Y);
                    MaxX = Math.Max(MaxX, point.X);
                    MaxY = Math.Max(MaxY, point.Y);
                }
            }
        }

        /// <summary>Gets the paths that bound the places.</summary>
        internal Paths64 Paths { get; }

        /// <summary>Gets the least X of the paths.</summary>
        internal long MinX { get; }

        /// <summary>Gets the least Y of the paths.</summary>
        internal long MinY { get; }

        /// <summary>Gets the greatest X of the paths.</summary>
        internal long MaxX { get; }

        /// <summary>Gets the greatest Y of the paths.</summary>
        internal long MaxY { get; }
    }
}
