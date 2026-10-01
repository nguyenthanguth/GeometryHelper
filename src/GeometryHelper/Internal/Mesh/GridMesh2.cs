using System;
using System.Collections.Generic;
using GeometryHelper.Clipper;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Planar;
using GeometryHelper.Meshing;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Lays the cells of a grid over the material of a face and keeps what of each the material holds.
    /// <para>
    /// The grid is laid along two axes turned by an angle, its cells a width by a height and a joint apart, standing
    /// where the alignments or an origin say. Row by row, the region is first cut to a band about the row, so that each
    /// cell is cut against the little of the region near it rather than all of it; a cell no edge of the band comes near
    /// is then wholly inside or wholly outside, and its middle tells which, so that only the cells along the boundary and
    /// the holes are cut at all. Cutting goes through <see cref="ClipperRegion"/>, which gives every corner its full
    /// precision back: the face's own corners, and the crossings of its edges with the cells' computed in double
    /// precision. A cut cell that holds a hole whole is split across, through the middle of the hole, so that no face
    /// has one.
    /// </para>
    /// </summary>
    internal static class GridMesh2
    {
        /// <summary>
        /// The most cells a grid may lay over the extent of a shape: more is taken as cells far smaller than were meant.
        /// </summary>
        public const long MaxCells = 4000000;

        /// <summary>
        /// Adds the cells of a grid over a face, each as much of it as the material holds.
        /// </summary>
        /// <param name="face">The face, straight-edged.</param>
        /// <param name="options">The cell size, joint, alignments and origin.</param>
        /// <param name="angleRad">The direction of the grid's first axis.</param>
        /// <param name="tolerance">The tolerance the region is cut within.</param>
        /// <param name="builder">Where the cells go.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when a cell is no larger than the point tolerance, or the grid would lay more than
        /// <see cref="MaxCells"/> cells over the shape.
        /// </exception>
        public static void Add(GeoFace2 face, MeshOptions options, double angleRad, Tolerance tolerance, MeshBuilder2 builder)
        {
            double width = options.CellWidth;
            double height = options.CellHeight;

            if (!(width > tolerance.EqualPoint) || !(height > tolerance.EqualPoint))
            {
                throw new ArgumentException("A grid cell has to be larger than the point tolerance both ways.", nameof(options));
            }

            // A joint no wider than the tolerance is none: the cells meet.
            double joint = options.Joint > tolerance.EqualPoint ? options.Joint : 0.0;
            var grid = new Grid(angleRad, options.Origin ?? face.Boundary[0]);

            double uMin = double.MaxValue, uMax = double.MinValue, vMin = double.MaxValue, vMax = double.MinValue;

            foreach (GeoPoint2 corner in face.Boundary.Vertices)
            {
                grid.Measure(corner, out double s, out double t);
                uMin = Math.Min(uMin, s);
                uMax = Math.Max(uMax, s);
                vMin = Math.Min(vMin, t);
                vMax = Math.Max(vMax, t);
            }

            if (!(uMax - uMin > tolerance.EqualPoint) || !(vMax - vMin > tolerance.EqualPoint))
            {
                return;
            }

            bool anchored = options.Origin.HasValue;
            // A cell reaching into the shape by however little is laid, so that the material is covered to its edge: the
            // cutting drops what is thinner than its rounding.
            Axis columns = Axis.Lay(uMin, uMax, width, joint, anchored, options.AlignU, 0.0);
            Axis rows = Axis.Lay(vMin, vMax, height, joint, anchored, options.AlignV, 0.0);

            if ((double)columns.Count * rows.Count > MaxCells)
            {
                throw new ArgumentException(
                    $"A grid of {columns.Count} by {rows.Count} cells over the shape is more than the {MaxCells} cells a mesh may have; the cells are far smaller than the shape.",
                    nameof(options));
            }

            if (columns.Count == 0 || rows.Count == 0)
            {
                return;
            }

            // Everything is laid out from the grid's anchor, as Clipper2 rounds to a fixed number of places; the cells
            // and the bands reach a cell past the shape at most.
            double extent = ClipperRegion.Extent(face.Boundary.Vertices, grid.Anchor);

            foreach (double s in new[] { columns.Start(0) - width, columns.End(columns.Count - 1) + width })
            {
                foreach (double t in new[] { rows.Start(0) - height, rows.End(rows.Count - 1) + height })
                {
                    GeoPoint2 far = grid.Local(s, t);
                    extent = Math.Max(extent, Math.Max(Math.Abs(far.X), Math.Abs(far.Y)));
                }
            }

            int precision = ClipperRegion.GetPrecision(extent);
            List<List<GeoPoint2>> region = ClipperRegion.RegionOf(face, grid.Anchor, precision, tolerance);

            if (region.Count == 0)
            {
                return;
            }

            var cutter = new Cutter(grid, width, height, precision, tolerance, builder);
            double near = 2.0 * Math.Max(tolerance.EqualPoint, 1E-9);
            var touched = new bool[columns.Count];

            for (int r = 0; r < rows.Count; r++)
            {
                double y0 = rows.Start(r);
                double y1 = rows.End(r);

                // Half a cell past the row each way, so that the band's own sides come near none of its cells.
                List<GeoPoint2> band = grid.Loop(uMin - width, uMax + width, y0 - 0.5 * height, y1 + 0.5 * height);
                List<LoopGroup> banded = ClipperRegion.ExecuteExact(ClipType.Intersection, region, new[] { band }, Clipper.FillRule.Positive, precision);

                if (banded.Count == 0)
                {
                    continue;
                }

                List<List<GeoPoint2>> strip = ClipperRegion.Flatten(banded);
                Array.Clear(touched, 0, touched.Length);
                Mark(strip, grid, columns, y0, y1, near, touched);

                for (int c = 0; c < columns.Count; c++)
                {
                    double x0 = columns.Start(c);
                    double x1 = columns.End(c);
                    List<GeoPoint2> cell = grid.Loop(x0, x1, y0, y1);

                    if (!touched[c])
                    {
                        // No edge comes near the cell, so all of it lies on one side of the boundary.
                        if (Winding(strip, grid.Local(0.5 * (x0 + x1), 0.5 * (y0 + y1))) > 0)
                        {
                            builder.Add(grid.World(cell), true);
                        }

                        continue;
                    }

                    cutter.Cut(strip, cell, x0, x1, y0, y1);
                }
            }
        }

        /// <summary>
        /// Marks the columns of a row that an edge of the region comes within a distance of.
        /// </summary>
        private static void Mark(List<List<GeoPoint2>> loops, Grid grid, Axis columns, double y0, double y1, double near, bool[] touched)
        {
            double low = y0 - near;
            double high = y1 + near;

            foreach (List<GeoPoint2> loop in loops)
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    grid.MeasureLocal(loop[i], out double sa, out double ta);
                    grid.MeasureLocal(loop[(i + 1) % loop.Count], out double sb, out double tb);

                    if (Math.Max(ta, tb) < low || Math.Min(ta, tb) > high)
                    {
                        continue;
                    }

                    double from, to;

                    if (ta == tb)
                    {
                        from = Math.Min(sa, sb);
                        to = Math.Max(sa, sb);
                    }
                    else
                    {
                        // The part of the edge within the row, by where along it the row's lines cross it.
                        double enter = (low - ta) / (tb - ta);
                        double leave = (high - ta) / (tb - ta);

                        if (enter > leave)
                        {
                            double swap = enter;
                            enter = leave;
                            leave = swap;
                        }

                        enter = Math.Max(enter, 0.0);
                        leave = Math.Min(leave, 1.0);

                        if (enter > leave)
                        {
                            continue;
                        }

                        double se = sa + enter * (sb - sa);
                        double sl = sa + leave * (sb - sa);
                        from = Math.Min(se, sl);
                        to = Math.Max(se, sl);
                    }

                    columns.Mark(from - near, to + near, touched);
                }
            }
        }

        /// <summary>
        /// How many times loops wind about a point, counter-clockwise counting up.
        /// </summary>
        private static int Winding(List<List<GeoPoint2>> loops, GeoPoint2 point)
        {
            int winding = 0;

            foreach (List<GeoPoint2> loop in loops)
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    GeoPoint2 a = loop[i];
                    GeoPoint2 b = loop[(i + 1) % loop.Count];
                    double side = (b.X - a.X) * (point.Y - a.Y) - (point.X - a.X) * (b.Y - a.Y);

                    if (a.Y <= point.Y)
                    {
                        if (b.Y > point.Y && side > 0.0)
                        {
                            winding++;
                        }
                    }
                    else if (b.Y <= point.Y && side < 0.0)
                    {
                        winding--;
                    }
                }
            }

            return winding;
        }

        /// <summary>
        /// The grid's two axes and the point they are measured from, which is also where the region is laid out from.
        /// </summary>
        private readonly struct Grid
        {
            private readonly double _ux;
            private readonly double _uy;

            public Grid(double angleRad, GeoPoint2 anchor)
            {
                _ux = Math.Cos(angleRad);
                _uy = Math.Sin(angleRad);
                Anchor = anchor;
            }

            public GeoPoint2 Anchor { get; }

            /// <summary>
            /// Where a point of the plane stands along the axes.
            /// </summary>
            public void Measure(GeoPoint2 point, out double s, out double t) => MeasureLocal(new GeoPoint2(point.X - Anchor.X, point.Y - Anchor.Y), out s, out t);

            /// <summary>
            /// Where a point laid out from the anchor stands along the axes.
            /// </summary>
            public void MeasureLocal(GeoPoint2 local, out double s, out double t)
            {
                s = local.X * _ux + local.Y * _uy;
                t = local.Y * _ux - local.X * _uy;
            }

            /// <summary>
            /// The point at a place along the axes, laid out from the anchor. The same place gives the same point bit for
            /// bit, so cells side by side share their corners exactly.
            /// </summary>
            public GeoPoint2 Local(double s, double t) => new GeoPoint2(s * _ux - t * _uy, s * _uy + t * _ux);

            /// <summary>
            /// The rectangle between two places along each axis, counter-clockwise, laid out from the anchor.
            /// </summary>
            public List<GeoPoint2> Loop(double s0, double s1, double t0, double t1)
                => new List<GeoPoint2> { Local(s0, t0), Local(s1, t0), Local(s1, t1), Local(s0, t1) };

            /// <summary>
            /// A loop laid out from the anchor, put back in the plane.
            /// </summary>
            public List<GeoPoint2> World(List<GeoPoint2> local)
            {
                var world = new List<GeoPoint2>(local.Count);

                foreach (GeoPoint2 point in local)
                {
                    world.Add(new GeoPoint2(point.X + Anchor.X, point.Y + Anchor.Y));
                }

                return world;
            }
        }

        /// <summary>
        /// The cells along one axis of a grid: where each starts and ends, measured from the anchor.
        /// </summary>
        private readonly struct Axis
        {
            private readonly double _first;
            private readonly double _pitch;
            private readonly double _size;
            private readonly bool _butted;

            private Axis(double first, double pitch, double size, bool butted, int count)
            {
                _first = first;
                _pitch = pitch;
                _size = size;
                _butted = butted;
                Count = count;
            }

            public int Count { get; }

            public double Start(int i) => _first + i * _pitch;

            /// <summary>
            /// Where a cell ends: where the next starts, the same number, when there are no joints between them.
            /// </summary>
            public double End(int i) => _butted ? _first + (i + 1) * _pitch : _first + i * _pitch + _size;

            /// <summary>
            /// Lays cells of a size a joint apart along an axis, as many as reach into a span by more than a distance.
            /// </summary>
            /// <param name="min">Where the span starts.</param>
            /// <param name="max">Where the span ends.</param>
            /// <param name="size">The size of a cell.</param>
            /// <param name="joint">The gap between two cells.</param>
            /// <param name="anchored">Whether a cell starts at nought, the anchor being the origin asked for.</param>
            /// <param name="alignment">Where the cells stand against the span when not anchored.</param>
            /// <param name="reach">How far a cell has to reach into the span to be laid.</param>
            public static Axis Lay(double min, double max, double size, double joint, bool anchored, GridAlignment alignment, double reach)
            {
                double pitch = size + joint;
                double middle = 0.5 * (min + max);
                double start;

                if (anchored)
                {
                    start = 0.0;
                }
                else if (alignment == GridAlignment.Start)
                {
                    start = min;
                }
                else if (alignment == GridAlignment.End)
                {
                    start = max - size;
                }
                else if (alignment == GridAlignment.CenterCell)
                {
                    start = middle - 0.5 * size;
                }
                else
                {
                    // The cell after the joint on the middle starts half a joint past it.
                    start = middle + 0.5 * joint;
                }

                // The cells k that reach into the span: start + k pitch before its end, and start + k pitch + size past
                // its start, each by more than the reach.
                double low = Math.Floor((min + reach - size - start) / pitch) + 1.0;
                double high = Math.Ceiling((max - reach - start) / pitch) - 1.0;

                while (start + (low - 1.0) * pitch + size > min + reach)
                {
                    low -= 1.0;
                }

                while (start + low * pitch + size <= min + reach)
                {
                    low += 1.0;
                }

                while (start + (high + 1.0) * pitch < max - reach)
                {
                    high += 1.0;
                }

                while (start + high * pitch >= max - reach)
                {
                    high -= 1.0;
                }

                double count = Math.Max(0.0, high - low + 1.0);

                if (count > MaxCells)
                {
                    count = MaxCells + 1;
                }

                return new Axis(start + low * pitch, pitch, size, joint == 0.0, (int)count);
            }

            /// <summary>
            /// Marks the cells that overlap a span.
            /// </summary>
            public void Mark(double from, double to, bool[] marks)
            {
                double first = Math.Floor((from - _first - _size) / _pitch);
                double last = Math.Floor((to - _first) / _pitch);
                int lo = (int)Math.Max(0.0, Math.Min(Count, first));
                int hi = (int)Math.Min(Count - 1, Math.Max(-1.0, last));

                while (lo < Count && End(lo) < from)
                {
                    lo++;
                }

                while (hi >= 0 && Start(hi) > to)
                {
                    hi--;
                }

                for (int i = lo; i <= hi; i++)
                {
                    marks[i] = true;
                }
            }
        }

        /// <summary>
        /// Cuts the cells an edge comes near against the region, and splits a cut cell holding a hole whole through it.
        /// </summary>
        private sealed class Cutter
        {
            private readonly Grid _grid;
            private readonly double _width;
            private readonly double _height;
            private readonly int _precision;
            private readonly Tolerance _tolerance;
            private readonly MeshBuilder2 _builder;

            public Cutter(Grid grid, double width, double height, int precision, Tolerance tolerance, MeshBuilder2 builder)
            {
                _grid = grid;
                _width = width;
                _height = height;
                _precision = precision;
                _tolerance = tolerance;
                _builder = builder;
            }

            public void Cut(List<List<GeoPoint2>> strip, List<GeoPoint2> cell, double x0, double x1, double y0, double y1)
            {
                List<LoopGroup> pieces = ClipperRegion.ExecuteExact(ClipType.Intersection, strip, new[] { cell }, Clipper.FillRule.Positive, _precision);

                // An edge that only came near, or ran along a side, or cut off no more than the tolerance, leaves the cell
                // whole; it keeps the shape the cutting gave it, which stays within the material and meets its neighbours.
                if (pieces.Count == 1 && pieces[0].Holes.Count == 0 && IsCell(pieces[0].Outer, cell))
                {
                    _builder.Add(_grid.World(pieces[0].Outer), true);
                    return;
                }

                foreach (LoopGroup piece in pieces)
                {
                    Add(piece, x0, x1, y0, y1);
                }
            }

            /// <summary>
            /// Adds a cut piece, split across through the middle of its first hole, and again, until no piece has one.
            /// </summary>
            private void Add(LoopGroup piece, double x0, double x1, double y0, double y1)
            {
                var pending = new Stack<LoopGroup>();
                pending.Push(piece);

                // Every split opens the hole it goes through, so a piece takes no more splits than it has holes. Should
                // the region's clean-up close one again, the piece is broken into triangles instead.
                int splits = 4 * piece.Holes.Count + 8;

                while (pending.Count > 0)
                {
                    LoopGroup next = pending.Pop();

                    if (next.Holes.Count == 0)
                    {
                        _builder.Add(_grid.World(next.Outer), false);
                        continue;
                    }

                    if (--splits < 0)
                    {
                        foreach (GeoTriangle2 triangle in Triangulation2.Triangulate(ClipperRegion.ToFaces(new List<LoopGroup> { next }, _grid.Anchor, false)[0], _tolerance))
                        {
                            _builder.Add(new[] { triangle.A, triangle.B, triangle.C }, false);
                        }

                        continue;
                    }

                    double from = double.MaxValue, to = double.MinValue;

                    foreach (GeoPoint2 point in next.Holes[0])
                    {
                        _grid.MeasureLocal(point, out double s, out double _);
                        from = Math.Min(from, s);
                        to = Math.Max(to, s);
                    }

                    double cut = 0.5 * (from + to);
                    var loops = new List<List<GeoPoint2>> { next.Outer };
                    loops.AddRange(next.Holes);

                    // The halves reach a cell past the piece on every other side, so that only the cut is new to it.
                    foreach ((double low, double high) in new[] { (x0 - _width, cut), (cut, x1 + _width) })
                    {
                        List<GeoPoint2> half = _grid.Loop(low, high, y0 - _height, y1 + _height);

                        foreach (LoopGroup part in ClipperRegion.ExecuteExact(ClipType.Intersection, loops, new[] { half }, Clipper.FillRule.Positive, _precision))
                        {
                            pending.Push(part);
                        }
                    }
                }
            }

            /// <summary>
            /// Whether a loop is the cell itself: every corner of it on the cell's sides, and every corner of the cell
            /// one of it, within the point tolerance.
            /// </summary>
            private bool IsCell(List<GeoPoint2> loop, List<GeoPoint2> cell)
            {
                foreach (GeoPoint2 point in loop)
                {
                    bool onSide = false;

                    for (int i = 0; i < 4 && !onSide; i++)
                    {
                        onSide = Distance(point, cell[i], cell[(i + 1) % 4]) <= _tolerance.EqualPoint;
                    }

                    if (!onSide)
                    {
                        return false;
                    }
                }

                foreach (GeoPoint2 corner in cell)
                {
                    bool found = false;

                    foreach (GeoPoint2 point in loop)
                    {
                        if (point.IsEqualTo(corner, _tolerance))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static double Distance(GeoPoint2 point, GeoPoint2 a, GeoPoint2 b)
            {
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                double lengthSquared = dx * dx + dy * dy;
                double t = lengthSquared > 0.0 ? Math.Max(0.0, Math.Min(1.0, ((point.X - a.X) * dx + (point.Y - a.Y) * dy) / lengthSquared)) : 0.0;
                double ox = a.X + t * dx - point.X;
                double oy = a.Y + t * dy - point.Y;
                return Math.Sqrt(ox * ox + oy * oy);
            }
        }
    }
}
