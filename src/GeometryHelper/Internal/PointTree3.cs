using System;
using System.Collections.Generic;

namespace GeometryHelper.Core
{
    /// <summary>
    /// A set of points of space laid out in a tree of boxes, so that the ones lying near a stretch of a line are found
    /// without looking at the others.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The points are cut in two at the middle of their longest side, over and over, until a box holds eight or fewer. A
    /// stretch of line is then taken down the tree, into a box only if the stretch comes within the reach of it, so that a
    /// thousand points far from the line cost the one look at the box that holds them. This is what the edges of a fan
    /// need: sorted along an axis, all the edges of a cone of 16 000 sides overlap the others at the apex and are tried
    /// against each other, 768 million pairs; asked which points lie along each edge, each is asked of the few near it.
    /// </para>
    /// </remarks>
    internal sealed class PointTree3
    {
        /// <summary>How many points a box holds before it is cut in two.</summary>
        private const int LeafSize = 8;

        private readonly double[] _x;
        private readonly double[] _y;
        private readonly double[] _z;

        /// <summary>The numbers of the points, in the order the boxes lay them out: each box holds a run of them.</summary>
        private readonly int[] _order;

        private readonly List<Box> _boxes = new List<Box>();

        private struct Box
        {
            public double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

            /// <summary>The run of <see cref="_order"/> the box holds, from the first to the one after the last.</summary>
            public int From, To;

            /// <summary>The two boxes it was cut into, or -1 for a box that holds its points itself.</summary>
            public int Left, Right;
        }

        /// <summary>
        /// Lays the points out in a tree.
        /// </summary>
        /// <param name="x">The X of each point; a point is known by its place in the arrays.</param>
        /// <param name="y">The Y of each point.</param>
        /// <param name="z">The Z of each point.</param>
        public PointTree3(double[] x, double[] y, double[] z)
        {
            _x = x;
            _y = y;
            _z = z;
            _order = new int[x.Length];

            for (int i = 0; i < _order.Length; i++)
            {
                _order[i] = i;
            }

            if (_order.Length > 0)
            {
                Build(0, _order.Length);
            }
        }

        /// <summary>
        /// Orders points by one coordinate, the lower number first where they stand level, so that no two are alike.
        /// </summary>
        private sealed class ByCoordinate : IComparer<int>
        {
            private readonly double[] _values;

            public ByCoordinate(double[] values)
            {
                _values = values;
            }

            public int Compare(int a, int b)
            {
                int byValue = _values[a].CompareTo(_values[b]);
                return byValue != 0 ? byValue : a.CompareTo(b);
            }
        }

        private int Build(int from, int to)
        {
            var box = new Box
            {
                MinX = double.PositiveInfinity,
                MinY = double.PositiveInfinity,
                MinZ = double.PositiveInfinity,
                MaxX = double.NegativeInfinity,
                MaxY = double.NegativeInfinity,
                MaxZ = double.NegativeInfinity,
                From = from,
                To = to,
                Left = -1,
                Right = -1,
            };

            for (int i = from; i < to; i++)
            {
                int p = _order[i];
                box.MinX = Math.Min(box.MinX, _x[p]);
                box.MaxX = Math.Max(box.MaxX, _x[p]);
                box.MinY = Math.Min(box.MinY, _y[p]);
                box.MaxY = Math.Max(box.MaxY, _y[p]);
                box.MinZ = Math.Min(box.MinZ, _z[p]);
                box.MaxZ = Math.Max(box.MaxZ, _z[p]);
            }

            int index = _boxes.Count;
            _boxes.Add(box);

            if (to - from <= LeafSize)
            {
                return index;
            }

            double sx = box.MaxX - box.MinX, sy = box.MaxY - box.MinY, sz = box.MaxZ - box.MinZ;
            double[] along = sx >= sy && sx >= sz ? _x : sy >= sz ? _y : _z;

            Array.Sort(_order, from, to - from, new ByCoordinate(along));

            int middle = from + (to - from) / 2;
            box.Left = Build(from, middle);
            box.Right = Build(middle, to);
            _boxes[index] = box;

            return index;
        }

        /// <summary>
        /// Finds the points lying within a reach of a stretch of a line whose projections fall on the stretch.
        /// </summary>
        /// <param name="ox">The X of a point of the line.</param>
        /// <param name="oy">The Y of the point.</param>
        /// <param name="oz">The Z of the point.</param>
        /// <param name="ux">The X of the unit vector the line runs along.</param>
        /// <param name="uy">The Y of the unit vector.</param>
        /// <param name="uz">The Z of the unit vector.</param>
        /// <param name="from">How far along the line, from its point, the stretch starts; may be negative.</param>
        /// <param name="to">How far along it ends.</param>
        /// <param name="reach">How far off the line a point may lie.</param>
        /// <param name="found">The numbers of the points, in no particular order; cleared first.</param>
        public void Near(double ox, double oy, double oz, double ux, double uy, double uz, double from, double to, double reach, List<int> found)
        {
            found.Clear();

            if (_boxes.Count == 0)
            {
                return;
            }

            // The stretch itself, which a box is asked about with its sides grown by the reach: a point within the reach of the
            // stretch lies in a box that meets it so, and the reach round the stretch is no wider than that.
            double ax = ox + from * ux, ay = oy + from * uy, az = oz + from * uz;
            double dx = (to - from) * ux, dy = (to - from) * uy, dz = (to - from) * uz;
            double reachSquared = reach * reach;

            var stack = new int[64];
            int top = 0;
            stack[top++] = 0;

            while (top > 0)
            {
                Box box = _boxes[stack[--top]];

                if (!MeetsFirst(ax, dx, box.MinX - reach, box.MaxX + reach, out double low, out double high)
                    || !Meets(ay, dy, box.MinY - reach, box.MaxY + reach, ref low, ref high)
                    || !Meets(az, dz, box.MinZ - reach, box.MaxZ + reach, ref low, ref high))
                {
                    continue;
                }

                if (box.Left >= 0)
                {
                    stack[top++] = box.Right;
                    stack[top++] = box.Left;
                    continue;
                }

                for (int i = box.From; i < box.To; i++)
                {
                    int p = _order[i];
                    double wx = _x[p] - ox, wy = _y[p] - oy, wz = _z[p] - oz;
                    double along = wx * ux + wy * uy + wz * uz;

                    if (along < from || along > to)
                    {
                        continue;
                    }

                    double cx = wy * uz - wz * uy, cy = wz * ux - wx * uz, cz = wx * uy - wy * ux;

                    if (cx * cx + cy * cy + cz * cz <= reachSquared)
                    {
                        found.Add(p);
                    }
                }
            }
        }

        /// <summary>
        /// Whether a segment, running from its start by a step, passes through the slab between two heights along one axis,
        /// taking the whole of the segment, the parameters 0 to 1, as what is left to pass through it.
        /// </summary>
        private static bool MeetsFirst(double start, double step, double low, double high, out double from, out double to)
        {
            from = 0.0;
            to = 1.0;
            return Meets(start, step, low, high, ref from, ref to);
        }

        /// <summary>
        /// Whether what is left of a segment, the parameters from <paramref name="from"/> to <paramref name="to"/>, passes
        /// through the slab between two heights along one axis; and what it leaves of it.
        /// </summary>
        private static bool Meets(double start, double step, double low, double high, ref double from, ref double to)
        {
            if (step == 0.0)
            {
                return start >= low && start <= high;
            }

            double a = (low - start) / step;
            double b = (high - start) / step;

            from = Math.Max(from, Math.Min(a, b));
            to = Math.Min(to, Math.Max(a, b));

            return from <= to;
        }
    }
}
