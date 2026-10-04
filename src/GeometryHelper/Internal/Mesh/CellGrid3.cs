using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Lays the cells of a grid through a body and cuts the body into them.
    /// <para>
    /// Along each axis the cells are laid as its <see cref="CellAxis"/> says, measured between the body's furthest corners
    /// along it, or from the placement's origin. A box whose sides the grid's axes run along is cut by arithmetic, every cell
    /// a box. Any other body is cut by planes: along X first, the rest of the body cut off cell by cell, then each slab along
    /// Y, and each bar along Z, so that every cut is made through as little of the body as can be. A cut coming within the
    /// snap distance of a corner of the piece it cuts is moved onto that corner, so that no slice thinner than that is cut
    /// off: within it of the piece's far side it is not made at all, and the slice stays with the cell beside, nor where,
    /// moved onto a corner, it would take off less than four point tolerances. A piece standing wholly on one side of a cut
    /// goes to that side, however thin. What a cell holds in several pieces is split into them.
    /// </para>
    /// </summary>
    internal static class CellGrid3
    {
        /// <summary>
        /// The most cells a grid may lay through a body: more is taken as cells far smaller than were meant.
        /// </summary>
        public const long MaxCells = 1000000;

        #region Laying out

        /// <summary>
        /// The cells along one axis: where each starts and ends, measured along the axis.
        /// </summary>
        internal sealed class Lines
        {
            public Lines(double[] starts, double[] ends)
            {
                Starts = starts;
                Ends = ends;
            }

            public double[] Starts { get; }

            public double[] Ends { get; }

            public int Count => Starts.Length;

            public Lines Shifted(double by)
            {
                var starts = new double[Starts.Length];
                var ends = new double[Ends.Length];

                for (int i = 0; i < starts.Length; i++)
                {
                    starts[i] = Starts[i] - by;
                    ends[i] = Ends[i] - by;
                }

                return new Lines(starts, ends);
            }
        }

        /// <summary>
        /// Lays the cells along one axis over a span of the body.
        /// </summary>
        /// <param name="axis">How the axis is divided.</param>
        /// <param name="min">Where the body starts along the axis.</param>
        /// <param name="max">Where the body ends along the axis.</param>
        /// <param name="joint">The gap between two cells; nought for none.</param>
        /// <param name="anchored">Whether a cell starts at nought, the anchor being the origin asked for.</param>
        /// <param name="tolerance">The tolerance, whose point a cell has to be larger than.</param>
        /// <param name="name">The axis, for a message.</param>
        /// <exception cref="ArgumentException">Thrown when a cell is no larger than the point tolerance.</exception>
        internal static Lines Lay(CellAxis axis, double min, double max, double joint, bool anchored, Tolerance tolerance, string name)
        {
            if (axis.IsWhole)
            {
                return new Lines(new[] { min }, new[] { max });
            }

            if (axis.Count > 1)
            {
                int n = axis.Count;

                // Counted before anything is laid out for them: the most an int holds would not fit in memory.
                if (n > MaxCells)
                {
                    throw new ArgumentException($"{n} cells along the {name} axis are more than the {MaxCells} cells a grid may have.", "options");
                }

                double size = (max - min - (n - 1) * joint) / n;

                if (!(size > tolerance.EqualPoint))
                {
                    throw new ArgumentException($"Divided into {n}, the body leaves cells of {size} along its {name} axis, no larger than the point tolerance.", "options");
                }

                var starts = new double[n];
                var ends = new double[n];
                double pitch = size + joint;

                for (int i = 0; i < n; i++)
                {
                    starts[i] = min + i * pitch;
                }

                for (int i = 0; i < n; i++)
                {
                    ends[i] = i == n - 1 ? max : joint == 0.0 ? starts[i + 1] : starts[i] + size;
                }

                return new Lines(starts, ends);
            }

            return LayBySize(axis.Size, axis.Alignment, min, max, joint, anchored, tolerance, name);
        }

        private static Lines LayBySize(double size, GridAlignment alignment, double min, double max, double joint, bool anchored, Tolerance tolerance, string name)
        {
            if (!(size > tolerance.EqualPoint))
            {
                throw new ArgumentException($"A cell of {size} along the {name} axis is no larger than the point tolerance.", "options");
            }

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

            // Counted before they are laid out: past 2^53 cells a cell's number less one is the number itself, and the steps
            // below would never end.
            if ((max - min) / pitch > MaxCells + 2.0)
            {
                throw new ArgumentException($"More than {MaxCells} cells along the {name} axis, which are more than a grid may have; the cells are far smaller than the body.", "options");
            }

            // The cells k that reach into the span: start + k pitch before its end, and start + k pitch + size past its
            // start.
            double low = Math.Floor((min - size - start) / pitch) + 1.0;
            double high = Math.Ceiling((max - start) / pitch) - 1.0;

            while (start + (low - 1.0) * pitch + size > min)
            {
                low -= 1.0;
            }

            while (start + low * pitch + size <= min)
            {
                low += 1.0;
            }

            while (start + (high + 1.0) * pitch < max)
            {
                high += 1.0;
            }

            while (start + high * pitch >= max)
            {
                high -= 1.0;
            }

            double count = Math.Max(0.0, high - low + 1.0);

            if (count > MaxCells)
            {
                throw new ArgumentException($"{count} cells along the {name} axis are more than the {MaxCells} cells a grid may have; the cells are far smaller than the body.", "options");
            }

            var starts = new double[(int)count];
            var ends = new double[starts.Length];

            for (int i = 0; i < starts.Length; i++)
            {
                starts[i] = start + (low + i) * pitch;

                // Without joints a cell ends where the next starts, the same number, so that neighbours share the plane.
                ends[i] = joint == 0.0 ? start + (low + i + 1) * pitch : starts[i] + size;
            }

            return new Lines(starts, ends);
        }

        /// <summary>
        /// The grid through a body: its frame, with its origin where the first cells start, the cells along each axis
        /// measured from there, and the body's span along each axis.
        /// </summary>
        private sealed class Layout
        {
            public GeoCoordinateSystem3 Frame;
            public Lines[] Lines;
            public double[] Min;
            public double[] Max;
            public double Joint;
        }

        private static readonly string[] AxisNames = { "X", "Y", "Z" };

        /// <summary>
        /// Lays the grid through a body's corners.
        /// </summary>
        private static Layout Lay(IReadOnlyList<GeoPoint3> points, GeoCoordinateSystem3 axes, CellOptions3 options, GeoPoint3? origin, Tolerance tolerance)
        {
            // A joint no wider than the tolerance is none: the cells meet.
            double joint = options.Joint > tolerance.EqualPoint ? options.Joint : 0.0;
            var directions = new[] { axes.XAxis, axes.YAxis, axes.ZAxis };
            GeoPoint3 reference = points[0];

            if (origin.HasValue)
            {
                // The origin moved by whole cells and joints to the cell nearest the body, so that the body is measured from
                // near it.
                reference = origin.Value;

                for (int a = 0; a < 3; a++)
                {
                    CellAxis axis = options.AxisAt(a);

                    if (axis.Size > 0.0)
                    {
                        double pitch = axis.Size + joint;
                        double steps = Math.Round(reference.GetVectorTo(points[0]).DotProduct(directions[a]) / pitch);
                        reference = reference.Add(directions[a].Multiply(steps * pitch));
                    }
                }
            }

            var min = new double[3];
            var max = new double[3];

            for (int a = 0; a < 3; a++)
            {
                min[a] = double.MaxValue;
                max[a] = double.MinValue;
            }

            foreach (GeoPoint3 point in points)
            {
                GeoVector3 offset = reference.GetVectorTo(point);

                for (int a = 0; a < 3; a++)
                {
                    double d = offset.DotProduct(directions[a]);
                    min[a] = Math.Min(min[a], d);
                    max[a] = Math.Max(max[a], d);
                }
            }

            var lines = new Lines[3];
            double cells = 1.0;

            for (int a = 0; a < 3; a++)
            {
                lines[a] = Lay(options.AxisAt(a), min[a], max[a], joint, origin.HasValue, tolerance, AxisNames[a]);
                cells *= lines[a].Count;
            }

            if (cells > MaxCells)
            {
                throw new ArgumentException(
                    $"A grid of {lines[0].Count} by {lines[1].Count} by {lines[2].Count} cells through the body is more than the {MaxCells} cells a grid may have; the cells are far smaller than the body.",
                    "options");
            }

            // The frame's origin is where the first cells start, and everything is measured from there.
            var first = new double[3];

            for (int a = 0; a < 3; a++)
            {
                first[a] = lines[a].Count > 0 ? lines[a].Starts[0] : min[a];
            }

            GeoPoint3 corner = reference.Add(directions[0].Multiply(first[0])).Add(directions[1].Multiply(first[1])).Add(directions[2].Multiply(first[2]));
            var layout = new Layout { Frame = axes.WithOrigin(corner), Lines = new Lines[3], Min = new double[3], Max = new double[3], Joint = joint };

            for (int a = 0; a < 3; a++)
            {
                layout.Lines[a] = lines[a].Shifted(first[a]);
                layout.Min[a] = min[a] - first[a];
                layout.Max[a] = max[a] - first[a];
            }

            return layout;
        }

        /// <summary>
        /// How many point tolerances from the far side of a part a cut has to stand to be made: less takes off a piece at the
        /// scale of the tolerance.
        /// </summary>
        private const double Tip = 4.0;

        /// <summary>
        /// How many point tolerances past a cut the cut refused may leave a part of the piece standing, as the point of a
        /// needle, for the piece to stay whole without a word.
        /// </summary>
        /// <remarks>
        /// A needle is thinner than the tolerance for as many tolerances of its point as it tapers slowly: the face of an L
        /// turned 0.002 off the grid left a sliver along a line of cells tapering at one in five hundred, and a cut across
        /// it 0.42 from its point, within a thousandth, could not be made.
        /// </remarks>
        private const double Needle = 1000.0;

        /// <summary>
        /// How near a corner a cut is moved onto it: the options' distance, or the point tolerance if more. Between cells a
        /// joint apart, the point tolerance alone: the cuts bound the joints, and moved onto a corner within the joint they
        /// would carry a cell into it.
        /// </summary>
        private static double SnapDistance(CellOptions3 options, Layout layout, Tolerance tolerance)
            => layout.Joint > 0.0 ? tolerance.EqualPoint : Math.Max(options.SnapDistance, tolerance.EqualPoint);

        /// <summary>
        /// How near the far side of a part a cut is not made, the slice staying on the part: the snap distance, or four point
        /// tolerances if more. Between cells a joint apart, the point tolerance alone: a slice past a joint is the cell
        /// beyond's, and kept on the part before it, it would go with the joint.
        /// </summary>
        private static double Reach(CellOptions3 options, Layout layout, Tolerance tolerance)
            => layout.Joint > 0.0 ? tolerance.EqualPoint : Math.Max(SnapDistance(options, layout, tolerance), Tip * tolerance.EqualPoint);

        /// <summary>
        /// How near a side of a part a cut moved onto a corner is not made: four point tolerances, or between cells a joint
        /// apart the point tolerance, as <see cref="Reach"/> says.
        /// </summary>
        private static double TipOf(Layout layout, Tolerance tolerance) => (layout.Joint > 0.0 ? 1.0 : Tip) * tolerance.EqualPoint;

        private static GeoCellGrid3 Empty(Layout layout, Tolerance tolerance)
            => new GeoCellGrid3(layout.Frame, Starts(layout), Ends(layout), new GeoCell3[0], layout.Joint, tolerance);

        private static double[][] Starts(Layout layout) => new[] { layout.Lines[0].Starts, layout.Lines[1].Starts, layout.Lines[2].Starts };

        private static double[][] Ends(Layout layout) => new[] { layout.Lines[0].Ends, layout.Lines[1].Ends, layout.Lines[2].Ends };

        #endregion

        #region Boxes

        /// <summary>
        /// Whether a frame's axes each run along one of a box's, so that every cell of the grid is a box.
        /// </summary>
        public static bool RunsAlong(GeoCoordinateSystem3 axes, GeoObb3 box)
        {
            foreach (GeoVector3 axis in new[] { axes.XAxis, axes.YAxis, axes.ZAxis })
            {
                bool along = false;

                for (int a = 0; a < 3 && !along; a++)
                {
                    along = Math.Abs(axis.DotProduct(box.GetAxisAt(a))) >= 1.0 - 1E-12;
                }

                if (!along)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Cuts a box into the cells of a grid whose axes run along its sides, by arithmetic: every cell is a box.
        /// </summary>
        public static GeoCellGrid3 OfBox(GeoObb3 box, GeoCoordinateSystem3 axes, CellOptions3 options, GeoPoint3? origin, Tolerance tolerance)
        {
            if (box.IsDegenerate(tolerance))
            {
                // A flat box holds nothing, and lays no cells.
                var none = new double[0];
                return new GeoCellGrid3(axes.WithOrigin(box.Center), new[] { none, none, none }, new[] { none, none, none }, new GeoCell3[0], 0.0, tolerance);
            }

            Layout layout = Lay(box.GetCorners(), axes, options, origin, tolerance);

            double reach = Reach(options, layout, tolerance);

            // Along each axis, what of each cell the box holds, cut as a body's piece is cut, one cut after another along the
            // axis: a cut within reach of a side of what is left of the box is not made, and the slice stays with the cell
            // beside. The box's section is the same whatever the cuts along the other axes, so each axis is cut on its own.
            var low = new double[3][];
            var high = new double[3][];

            for (int a = 0; a < 3; a++)
            {
                low[a] = new double[layout.Lines[a].Count];
                high[a] = new double[layout.Lines[a].Count];
                Spans(layout.Lines[a], layout.Min[a], layout.Max[a], reach, tolerance.EqualPoint, low[a], high[a]);
            }

            var cells = new List<GeoCell3>();
            Lines xs = layout.Lines[0], ys = layout.Lines[1], zs = layout.Lines[2];

            for (int k = 0; k < zs.Count; k++)
            {
                if (!(high[2][k] > low[2][k]))
                {
                    continue;
                }

                for (int j = 0; j < ys.Count; j++)
                {
                    if (!(high[1][j] > low[1][j]))
                    {
                        continue;
                    }

                    for (int i = 0; i < xs.Count; i++)
                    {
                        if (!(high[0][i] > low[0][i]))
                        {
                            continue;
                        }

                        GeoObb3 whole = GeoCellGrid3.BoxOf(layout.Frame, xs.Starts[i], xs.Ends[i], ys.Starts[j], ys.Ends[j], zs.Starts[k], zs.Ends[k]);
                        GeoObb3 material = GeoCellGrid3.BoxOf(layout.Frame, low[0][i], high[0][i], low[1][j], high[1][j], low[2][k], high[2][k]);
                        bool isWhole = Same(low[0][i], xs.Starts[i], tolerance) && Same(high[0][i], xs.Ends[i], tolerance)
                            && Same(low[1][j], ys.Starts[j], tolerance) && Same(high[1][j], ys.Ends[j], tolerance)
                            && Same(low[2][k], zs.Starts[k], tolerance) && Same(high[2][k], zs.Ends[k], tolerance);
                        double volume = (high[0][i] - low[0][i]) * (high[1][j] - low[1][j]) * (high[2][k] - low[2][k]);

                        cells.Add(new GeoCell3(
                            i,
                            j,
                            k,
                            0,
                            whole,
                            null,
                            material,
                            isWhole,
                            volume,
                            new[] { low[0][i], low[1][j], low[2][k] },
                            new[] { high[0][i], high[1][j], high[2][k] }));
                    }
                }
            }

            return new GeoCellGrid3(layout.Frame, Starts(layout), Ends(layout), cells.ToArray(), layout.Joint, tolerance);
        }

        /// <summary>
        /// Where each cell of a line takes a box's span from and to along the axis, cut one after another as
        /// <see cref="Along"/> cuts a piece: a cell that takes nothing ends where it starts.
        /// </summary>
        /// <param name="lines">The cells along the axis.</param>
        /// <param name="min">Where the box starts along the axis.</param>
        /// <param name="max">Where it ends.</param>
        /// <param name="reach">How near a side of what is left a cut is not made.</param>
        /// <param name="near">The point tolerance, within which a slice either side of a cut is as thick as the other.</param>
        /// <param name="low">Where each cell's part starts.</param>
        /// <param name="high">Where it ends.</param>
        private static void Spans(Lines lines, double min, double max, double reach, double near, double[] low, double[] high)
        {
            // What is left of the box: from here to its far side, while anything is.
            double from = min;
            bool left = true;

            for (int i = 0; i < lines.Count; i++)
            {
                low[i] = high[i] = left ? from : max;

                if (!left)
                {
                    continue;
                }

                // What lies before the cell, in the joint before it or before the first, is no cell's.
                if (i == 0 || lines.Starts[i] != lines.Ends[i - 1])
                {
                    int before = Decide(lines.Starts[i], from, max, reach, near);

                    if (before < 0)
                    {
                        left = false;
                        low[i] = high[i] = max;
                        continue;
                    }

                    if (before == 0)
                    {
                        from = lines.Starts[i];
                    }
                }

                int end = Decide(lines.Ends[i], from, max, reach, near);
                low[i] = from;
                high[i] = end < 0 ? max : end == 0 ? lines.Ends[i] : from;

                if (end < 0)
                {
                    left = false;
                }
                else if (end == 0)
                {
                    from = lines.Ends[i];
                }
            }
        }

        /// <summary>
        /// How a cut at a place meets what is left of a box from one side to the other, as <see cref="Split"/> meets a piece:
        /// -1 when all of it goes below, 1 when all of it goes above, and 0 when the cut is made.
        /// </summary>
        private static int Decide(double at, double lo, double hi, double reach, double near)
        {
            if (at <= lo)
            {
                return 1;
            }

            if (at >= hi)
            {
                return -1;
            }

            double over = hi - at;
            double under = at - lo;

            if (over <= reach || under <= reach)
            {
                return over <= reach && (under > reach || under >= over - near) ? -1 : 1;
            }

            return 0;
        }

        private static bool Same(double a, double b, Tolerance tolerance) => Math.Abs(a - b) <= tolerance.EqualPoint;

        /// <summary>
        /// A box as a closed body of its six faces, each wound outwards, built without a tolerance refusing a thin one.
        /// </summary>
        public static GeoSolid3 BoxSolid(GeoObb3 box)
        {
            GeoPoint3[] c = box.GetCorners();
            double sx = box.SizeX, sy = box.SizeY, sz = box.SizeZ;

            GeoFace3 Face(GeoVector3 normal, double area, params GeoPoint3[] corners)
                => new GeoFace3(GeoPolygon3.FromValidated(corners, normal, area));

            return new GeoSolid3(
                Face(box.AxisZ.Negate(), sx * sy, c[0], c[3], c[2], c[1]),
                Face(box.AxisZ, sx * sy, c[4], c[5], c[6], c[7]),
                Face(box.AxisY.Negate(), sx * sz, c[0], c[1], c[5], c[4]),
                Face(box.AxisY, sx * sz, c[2], c[3], c[7], c[6]),
                Face(box.AxisX, sy * sz, c[1], c[2], c[6], c[5]),
                Face(box.AxisX.Negate(), sy * sz, c[3], c[0], c[4], c[7]));
        }

        #endregion

        #region Bodies

        /// <summary>
        /// A part of the body on its way to a cell, with where it stands in the grid and the cuts bounding it.
        /// </summary>
        private sealed class Piece
        {
            public Piece(GeoSolid3 solid, double[] low, double[] high, int i, int j, int k)
            {
                Solid = solid;
                Low = low;
                High = high;
                I = i;
                J = j;
                K = k;
            }

            public GeoSolid3 Solid { get; }

            public double[] Low { get; }

            public double[] High { get; }

            public int I { get; }

            public int J { get; }

            public int K { get; }

            public Piece With(GeoSolid3 solid, int axis, int index, double low, double high)
            {
                var lows = (double[])Low.Clone();
                var highs = (double[])High.Clone();
                lows[axis] = low;
                highs[axis] = high;
                return new Piece(solid, lows, highs, axis == 0 ? index : I, axis == 1 ? index : J, axis == 2 ? index : K);
            }
        }

        /// <summary>
        /// The grid being cut: its layout, the snap distance, the tolerance.
        /// </summary>
        private sealed class Cutting
        {
            public Layout Layout;
            public double Snap;
            public double Reach;
            public double Tip;
            public Tolerance Tolerance;
            public GeoVector3[] Directions;
        }

        /// <summary>
        /// Cuts a body into the cells of a grid by planes.
        /// </summary>
        /// <param name="gross">The body as given, whose corners the grid is laid between.</param>
        /// <param name="material">The body with its openings cut in, which is cut; null when they take all of it.</param>
        /// <param name="openings">Openings still to cut into each cell they meet, which the whole body would not take.</param>
        /// <param name="axes">The grid's axes.</param>
        /// <param name="options">How the axes are divided.</param>
        /// <param name="origin">Where a cell starts, or null for the alignments.</param>
        /// <param name="tolerance">The tolerance the body is cut within.</param>
        public static GeoCellGrid3 OfSolid(GeoSolid3 gross, GeoSolid3 material, IReadOnlyList<GeoSolid3> openings, GeoCoordinateSystem3 axes, CellOptions3 options, GeoPoint3? origin, Tolerance tolerance)
        {
            Layout layout = Lay(Corners(gross), axes, options, origin, tolerance);

            if (material == null)
            {
                return Empty(layout, tolerance);
            }

            var cutting = new Cutting
            {
                Layout = layout,
                Snap = SnapDistance(options, layout, tolerance),
                Reach = Reach(options, layout, tolerance),
                Tip = TipOf(layout, tolerance),
                Tolerance = tolerance,
                Directions = new[] { layout.Frame.XAxis, layout.Frame.YAxis, layout.Frame.ZAxis },
            };

            var none = new[] { double.NaN, double.NaN, double.NaN };
            List<Piece> slabs = Along(new Piece(material, none, (double[])none.Clone(), 0, 0, 0), 0, cutting);
            var bars = new List<Piece>[slabs.Count];
            var threads = new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };

            Run(slabs.Count, threads, s =>
            {
                var found = new List<Piece>();

                foreach (Piece bar in Along(slabs[s], 1, cutting))
                {
                    found.AddRange(Along(bar, 2, cutting));
                }

                bars[s] = found;
            });

            var pieces = new List<Piece>();

            foreach (List<Piece> found in bars)
            {
                pieces.AddRange(found);
            }

            var cells = new List<GeoCell3>[pieces.Count];

            Run(pieces.Count, threads, p => cells[p] = Cells(pieces[p], openings, layout, tolerance));

            var all = new List<GeoCell3>();

            foreach (List<GeoCell3> found in cells)
            {
                all.AddRange(found);
            }

            all.Sort((a, b) =>
            {
                int byK = a.K.CompareTo(b.K);

                if (byK != 0)
                {
                    return byK;
                }

                int byJ = a.J.CompareTo(b.J);
                return byJ != 0 ? byJ : a.I.CompareTo(b.I) != 0 ? a.I.CompareTo(b.I) : a.Piece.CompareTo(b.Piece);
            });

            return new GeoCellGrid3(layout.Frame, Starts(layout), Ends(layout), all.ToArray(), layout.Joint, tolerance);
        }

        /// <summary>
        /// The corners of a body, holes included.
        /// </summary>
        private static List<GeoPoint3> Corners(GeoSolid3 solid)
        {
            var corners = new List<GeoPoint3>();

            foreach (GeoFace3 face in solid.Faces)
            {
                corners.AddRange(face.Boundary.Vertices);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    corners.AddRange(hole.Vertices);
                }
            }

            return corners;
        }

        /// <summary>
        /// Runs work over a range on the threads allowed, handing back the first failure as it was thrown.
        /// </summary>
        private static void Run(int count, ParallelOptions threads, Action<int> work)
        {
            Exception failed = null;

            Parallel.For(0, count, threads, n =>
            {
                try
                {
                    work(n);
                }
                catch (Exception exception)
                {
                    Interlocked.CompareExchange(ref failed, exception, null);
                }
            });

            if (failed != null)
            {
                ExceptionDispatchInfo.Capture(failed).Throw();
            }
        }

        /// <summary>
        /// Cuts a piece into the cells along one axis, dropping what lies in the joints and beyond the cells.
        /// </summary>
        private static List<Piece> Along(Piece piece, int axis, Cutting cutting)
        {
            var found = new List<Piece>();
            Lines lines = cutting.Layout.Lines[axis];
            GeoSolid3 rest = piece.Solid;
            double low = piece.Low[axis];

            if (lines.Count == 1 && lines.Starts[0] <= cutting.Layout.Min[axis] && lines.Ends[0] >= cutting.Layout.Max[axis])
            {
                // A whole axis, or one cell reaching past the body both ways: nothing to cut.
                found.Add(piece.With(rest, axis, 0, low, piece.High[axis]));
                return found;
            }

            for (int i = 0; i < lines.Count && rest != null; i++)
            {
                // What lies before the cell, in the joint before it or before the first, is no cell's.
                if (i == 0 || lines.Starts[i] != lines.Ends[i - 1])
                {
                    Cut before = Split(rest, axis, lines.Starts[i], cutting, Kept.Above);
                    rest = before.Above;

                    if (before.Made)
                    {
                        low = before.At;
                    }

                    if (rest == null)
                    {
                        break;
                    }
                }

                // What lies past the cell is the next one's, or in the joint after it or past the last, no cell's.
                bool gap = i == lines.Count - 1 || lines.Starts[i + 1] != lines.Ends[i];
                Cut end = Split(rest, axis, lines.Ends[i], cutting, gap ? Kept.Below : Kept.Both);

                if (end.Below != null)
                {
                    found.Add(piece.With(end.Below, axis, i, low, end.Made ? end.At : double.NaN));
                }

                rest = end.Above;

                if (end.Made)
                {
                    low = end.At;
                }
            }

            return found;
        }

        /// <summary>
        /// Which side of a cut a cell takes: both, or only the one above or below, the other lying in a joint, before the
        /// first cell or past the last.
        /// </summary>
        private enum Kept
        {
            Both,
            Above,
            Below,
        }

        /// <summary>
        /// What a cut leaves: the part below the plane and the part above, either null when the piece lies wholly on the
        /// other side, whether a cut was made, and where it stands once snapped.
        /// </summary>
        private struct Cut
        {
            public GeoSolid3 Below;
            public GeoSolid3 Above;
            public bool Made;
            public double At;
        }

        /// <summary>
        /// Cuts a piece by the plane square to an axis at a place along it, the place snapped to a corner of the piece within
        /// the snap distance.
        /// </summary>
        /// <param name="piece">The piece.</param>
        /// <param name="axis">The axis the plane stands square to.</param>
        /// <param name="at">Where the plane stands along it.</param>
        /// <param name="cutting">The grid being cut.</param>
        /// <param name="kept">Which side a cell takes, where a piece the cut cannot be made through is kept.</param>
        private static Cut Split(GeoSolid3 piece, int axis, double at, Cutting cutting, Kept kept)
        {
            GeoVector3 direction = cutting.Directions[axis];
            GeoPoint3 origin = cutting.Layout.Frame.Origin;
            double lowest = double.MaxValue;
            double highest = double.MinValue;
            double nearest = double.NaN;
            double gap = cutting.Snap;
            var corners = Corners(piece);

            foreach (GeoPoint3 corner in corners)
            {
                double d = origin.GetVectorTo(corner).DotProduct(direction);
                lowest = Math.Min(lowest, d);
                highest = Math.Max(highest, d);

                // The nearest corner within the snap distance, the higher of two as near, as a box's cut goes to its far side.
                double off = Math.Abs(d - at);

                if (off < gap || (off == gap && !(d <= nearest)))
                {
                    gap = off;
                    nearest = d;
                }
            }

            // A plane at or past a side of the piece leaves all of it on the other.
            if (at <= lowest)
            {
                return new Cut { Above = piece };
            }

            if (at >= highest)
            {
                return new Cut { Below = piece };
            }

            // Within the snap distance of the piece's far side, or four point tolerances, the cut would take off no more than
            // a slice, which stays on: a tip a hair past the plane is a piece the size of the tolerance, which the cut cannot
            // keep, and a cell no one wants. A piece that thin both ways goes whole to the side holding more of it, the one
            // below where they hold as much within the point tolerance, as a plate on the line through its middle does: the
            // rounding of where its corners were cut would otherwise choose.
            double over = highest - at;
            double under = at - lowest;

            if (over <= cutting.Reach || under <= cutting.Reach)
            {
                return over <= cutting.Reach && (under > cutting.Reach || under >= over - cutting.Tolerance.EqualPoint) ? new Cut { Below = piece } : new Cut { Above = piece };
            }

            if (!double.IsNaN(nearest))
            {
                at = nearest;
            }

            // Moved onto a corner, the cut can come within four point tolerances of a side, a corner a hair past the one it
            // went to, as a sloping end leaves: the slice there stays on too.
            if (highest - at <= cutting.Tip)
            {
                return new Cut { Below = piece };
            }

            if (at - lowest <= cutting.Tip)
            {
                return new Cut { Above = piece };
            }

            var plane = new GeoPlane3(origin.Add(direction.Multiply(at)), direction);

            if (Splition3.TrySplitCell(piece, plane, out GeoSolid3 above, out GeoSolid3 below, cutting.Tolerance))
            {
                return new Cut { Below = below, Above = above, Made = true, At = at };
            }

            // The point of a needle past the plane, however deep, can be thinner across than the point tolerance where the
            // plane meets it: the cut can keep no piece of it, and the piece stays whole on the side holding the rest.
            double past = Math.Min(highest - at, at - lowest);

            if (past <= Needle * cutting.Tolerance.EqualPoint)
            {
                return highest - at < at - lowest ? new Cut { Below = piece } : new Cut { Above = piece };
            }

            // A cut the body would not take, which a closed body should never give: the piece goes whole to the side of its
            // middle, or where one side is a joint or past the cells to the side a cell takes, so that nothing of the body is
            // lost, and the grid says so.
            bool toBelow = kept == Kept.Both ? origin.GetVectorTo(piece.GrossCentroid).DotProduct(direction) < at : kept == Kept.Below;
            GeometryHelperLog.Warn($"Cells: a cut square to the grid's {AxisNames[axis]} axis at {at} through a piece of the body could not be made; the piece is kept whole in the cell {(toBelow ? "below" : "above")}.");

            return toBelow ? new Cut { Below = piece } : new Cut { Above = piece };
        }

        /// <summary>
        /// A piece of the body with the openings it meets cut into it: null when they take all of it, and the piece as it is,
        /// warned of, where they cannot be cut into it or leave it open.
        /// </summary>
        private static GeoSolid3 CutIn(GeoSolid3 piece, IReadOnlyList<GeoSolid3> openings, Tolerance tolerance)
        {
            if (openings.Count == 0)
            {
                return piece;
            }

            GeoAabb3 box = piece.GetAabb();
            var meeting = new List<GeoSolid3>();

            foreach (GeoSolid3 opening in openings)
            {
                if (opening.GetAabb().CollidesWith(box, tolerance))
                {
                    meeting.Add(opening);
                }
            }

            if (meeting.Count == 0)
            {
                return piece;
            }

            bool cut = Boolean3.TryCutOpenings(piece, meeting, out GeoSolid3 material, tolerance, out Exception failure);

            if (failure == null && !cut)
            {
                return null;
            }

            if (failure == null && material.IsClosed(tolerance))
            {
                return material;
            }

            GeometryHelperLog.Warn("Cells: the openings a cell meets could not be cut into it; the cell keeps their material.", failure);
            return piece;
        }

        /// <summary>
        /// The cells a piece makes: one for each part of it that does not touch the others, numbered in turn up Z, along Y
        /// and along X by their middles.
        /// </summary>
        private static List<GeoCell3> Cells(Piece piece, IReadOnlyList<GeoSolid3> openings, Layout layout, Tolerance tolerance)
        {
            GeoSolid3 material = CutIn(piece.Solid, openings, tolerance);

            if (material == null)
            {
                return new List<GeoCell3>();
            }

            GeoSolid3[] shells = Boolean3.SplitShells(material, tolerance);
            var parts = new List<(GeoSolid3 Solid, GeoPoint3 Middle)>(shells.Length);

            foreach (GeoSolid3 shell in shells)
            {
                parts.Add((shell, layout.Frame.ToLocal(shell.GrossCentroid)));
            }

            parts.Sort((a, b) =>
            {
                int byZ = a.Middle.Z.CompareTo(b.Middle.Z);

                if (byZ != 0)
                {
                    return byZ;
                }

                int byY = a.Middle.Y.CompareTo(b.Middle.Y);
                return byY != 0 ? byY : a.Middle.X.CompareTo(b.Middle.X);
            });

            Lines xs = layout.Lines[0], ys = layout.Lines[1], zs = layout.Lines[2];
            double x0 = xs.Starts[piece.I], x1 = xs.Ends[piece.I];
            double y0 = ys.Starts[piece.J], y1 = ys.Ends[piece.J];
            double z0 = zs.Starts[piece.K], z1 = zs.Ends[piece.K];
            GeoObb3 whole = GeoCellGrid3.BoxOf(layout.Frame, x0, x1, y0, y1, z0, z1);
            double boxVolume = (x1 - x0) * (y1 - y0) * (z1 - z0);
            double boxArea = 2.0 * ((x1 - x0) * (y1 - y0) + (y1 - y0) * (z1 - z0) + (z1 - z0) * (x1 - x0));
            var low = new[] { x0, y0, z0 };
            var high = new[] { x1, y1, z1 };
            var cells = new List<GeoCell3>(parts.Count);

            for (int p = 0; p < parts.Count; p++)
            {
                double volume = parts[p].Solid.GrossVolume;

                // The body fills the cell but for a skin no thicker than the point tolerance: every face it has there lies on
                // a side of the cell, as a box's whole cell is cut by none of its sides. The volume alone would let through a
                // hole or a pocket taking less than the skin's.
                bool isWhole = parts.Count == 1
                    && Math.Abs(boxVolume - volume) <= tolerance.EqualPoint * boxArea
                    && OnSides(parts[p].Solid, layout.Frame, low, high, tolerance);

                cells.Add(new GeoCell3(piece.I, piece.J, piece.K, p, whole, parts[p].Solid, null, isWhole, volume, (double[])piece.Low.Clone(), (double[])piece.High.Clone()));
            }

            return cells;
        }

        /// <summary>
        /// Whether every face of a body lies on a side of a cell, within the point tolerance.
        /// </summary>
        private static bool OnSides(GeoSolid3 solid, GeoCoordinateSystem3 frame, double[] low, double[] high, Tolerance tolerance)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                bool on = false;

                for (int a = 0; a < 3 && !on; a++)
                {
                    on = Lies(face, frame, a, low[a], tolerance) || Lies(face, frame, a, high[a], tolerance);
                }

                if (!on)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a face lies in the plane square to an axis at a place along it, within the point tolerance; its holes lie
        /// in its plane.
        /// </summary>
        private static bool Lies(GeoFace3 face, GeoCoordinateSystem3 frame, int axis, double at, Tolerance tolerance)
        {
            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                GeoPoint3 local = frame.ToLocal(corner);
                double d = axis == 0 ? local.X : axis == 1 ? local.Y : local.Z;

                if (Math.Abs(d - at) > tolerance.EqualPoint)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
