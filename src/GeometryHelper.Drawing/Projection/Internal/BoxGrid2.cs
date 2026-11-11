using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// A uniform grid over boxes of the view plane, finding the boxes a segment or a box may meet.
    /// </summary>
    /// <remarks>
    /// The cells are shaped to the boxes' extent, about one cell per box, so a frame long in X and short in Y gets long
    /// rows of short cells rather than one row of cells that each hold half the frame: no axis is swept on its own, and a
    /// body thin or long along either costs the same. A segment is looked up in the cells it runs through, not the cells of
    /// its box, so a long diagonal does not read the whole grid.
    /// </remarks>
    internal sealed class BoxGrid2
    {
        private readonly double _minX;
        private readonly double _minY;
        private readonly double _cellX;
        private readonly double _cellY;
        private readonly int _nx;
        private readonly int _ny;
        private readonly int[] _starts;
        private readonly int[] _items;
        private readonly int[] _stamps;
        private int _stamp;

        /// <summary>
        /// Builds the grid of boxes, each widened by a margin.
        /// </summary>
        internal BoxGrid2(double[] minX, double[] minY, double[] maxX, double[] maxY, int count, double margin)
            : this(minX, minY, maxX, maxY, count, margin, count) { }

        /// <summary>
        /// Builds the grid of boxes, each widened by a margin, its cells shaped as for a number of boxes over the same extent:
        /// the boxes left once others the same are dropped find in it what all of them would, less those dropped.
        /// </summary>
        internal BoxGrid2(
            double[] minX,
            double[] minY,
            double[] maxX,
            double[] maxY,
            int count,
            double margin,
            int shapedFor
        )
        {
            _stamps = new int[count];
            double x0 = double.PositiveInfinity,
                y0 = double.PositiveInfinity;
            double x1 = double.NegativeInfinity,
                y1 = double.NegativeInfinity;

            for (int i = 0; i < count; i++)
            {
                x0 = Math.Min(x0, minX[i] - margin);
                y0 = Math.Min(y0, minY[i] - margin);
                x1 = Math.Max(x1, maxX[i] + margin);
                y1 = Math.Max(y1, maxY[i] + margin);
            }

            if (count == 0)
            {
                x0 = y0 = 0.0;
                x1 = y1 = 1.0;
            }

            double width = Math.Max(x1 - x0, 1E-9);
            double height = Math.Max(y1 - y0, 1E-9);
            int target = Math.Max(1, shapedFor);
            _nx = (int)Math.Max(1.0, Math.Min(target, Math.Round(Math.Sqrt(target * width / height))));
            _ny = (int)Math.Max(1.0, Math.Min(target, Math.Round((double)target / _nx)));
            _minX = x0;
            _minY = y0;
            _cellX = width / _nx;
            _cellY = height / _ny;

            var counts = new int[_nx * _ny + 1];

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < count; i++)
                {
                    int i0 = ColumnOf(minX[i] - margin);
                    int i1 = ColumnOf(maxX[i] + margin);
                    int j0 = RowOf(minY[i] - margin);
                    int j1 = RowOf(maxY[i] + margin);

                    for (int j = j0; j <= j1; j++)
                    {
                        for (int c = i0; c <= i1; c++)
                        {
                            int cell = j * _nx + c;

                            if (pass == 0)
                            {
                                counts[cell + 1]++;
                            }
                            else
                            {
                                _items[counts[cell]++] = i;
                            }
                        }
                    }
                }

                if (pass == 0)
                {
                    for (int c = 1; c < counts.Length; c++)
                    {
                        counts[c] += counts[c - 1];
                    }

                    _starts = (int[])counts.Clone();
                    _items = new int[counts[counts.Length - 1]];
                }
            }
        }

        /// <summary>
        /// Lists the boxes in the cells a segment runs through, widened by a margin, each once, in the order of the grid.
        /// </summary>
        internal void QuerySegment(double ax, double ay, double bx, double by, double margin, List<int> found)
        {
            found.Clear();
            NextStamp();
            int i0 = ColumnOf(Math.Min(ax, bx) - margin);
            int i1 = ColumnOf(Math.Max(ax, bx) + margin);
            int j0 = RowOf(Math.Min(ay, by) - margin);
            int j1 = RowOf(Math.Max(ay, by) + margin);

            if ((long)(i1 - i0 + 1) * (j1 - j0 + 1) <= 16 || Math.Abs(bx - ax) <= 1E-12)
            {
                Collect(i0, i1, j0, j1, found);
                return;
            }

            double dx = bx - ax;
            double dy = by - ay;

            for (int c = i0; c <= i1; c++)
            {
                // The stretch of the segment over this column, widened by the margin, gives the rows it runs through.
                double cx0 = _minX + c * _cellX - margin;
                double cx1 = cx0 + _cellX + 2.0 * margin;
                double t0 = (cx0 - ax) / dx;
                double t1 = (cx1 - ax) / dx;
                double lo = Math.Max(0.0, Math.Min(t0, t1));
                double hi = Math.Min(1.0, Math.Max(t0, t1));

                if (lo > hi)
                {
                    continue;
                }

                double ya = ay + lo * dy;
                double yb = ay + hi * dy;
                int r0 = Math.Max(j0, RowOf(Math.Min(ya, yb) - margin));
                int r1 = Math.Min(j1, RowOf(Math.Max(ya, yb) + margin));
                Collect(c, c, r0, r1, found);
            }
        }

        /// <summary>
        /// Lists the boxes in the cells a box meets, each once, in the order of the grid.
        /// </summary>
        internal void QueryBox(double x0, double y0, double x1, double y1, List<int> found)
        {
            found.Clear();
            NextStamp();
            Collect(ColumnOf(x0), ColumnOf(x1), RowOf(y0), RowOf(y1), found);
        }

        private void Collect(int i0, int i1, int j0, int j1, List<int> found)
        {
            for (int j = j0; j <= j1; j++)
            {
                for (int c = i0; c <= i1; c++)
                {
                    int cell = j * _nx + c;

                    for (int k = _starts[cell]; k < _starts[cell + 1]; k++)
                    {
                        int item = _items[k];

                        if (_stamps[item] != _stamp)
                        {
                            _stamps[item] = _stamp;
                            found.Add(item);
                        }
                    }
                }
            }
        }

        private void NextStamp()
        {
            _stamp++;

            if (_stamp == int.MaxValue)
            {
                Array.Clear(_stamps, 0, _stamps.Length);
                _stamp = 1;
            }
        }

        private int ColumnOf(double x) => (int)Math.Max(0.0, Math.Min(_nx - 1, Math.Floor((x - _minX) / _cellX)));

        private int RowOf(double y) => (int)Math.Max(0.0, Math.Min(_ny - 1, Math.Floor((y - _minY) / _cellY)));
    }
}
