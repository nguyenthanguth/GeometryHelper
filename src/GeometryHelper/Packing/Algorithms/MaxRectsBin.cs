using System.Collections.Generic;

namespace GeometryHelper.Packing.Algorithms
{
    /// <summary>
    /// One bin packed by the maximal rectangles method: the space left free is kept as every largest rectangle it holds,
    /// which may overlap one another, and each box goes into the free rectangle that puts its top highest, then its left
    /// side furthest left. So the bin fills from its upper left corner, row by row as the eye reads, and a box too
    /// short for a row slips into the space beneath a taller one.
    /// </summary>
    /// <remarks>
    /// Coordinates are the bin's own, its lower left corner at nought. A box fits a free rectangle if it is no more than
    /// the slack wider or higher, so that sizes that ought to add up exactly still do in floating point: two boxes may
    /// then overlap, or the bin's edge be passed, by as much as the slack and no more.
    /// </remarks>
    internal sealed class MaxRectsBin
    {
        private readonly double _slack;
        private List<Free> _free = new List<Free>();

        /// <summary>
        /// Initializes an empty bin.
        /// </summary>
        /// <param name="width">The width of the bin.</param>
        /// <param name="height">The height of the bin.</param>
        /// <param name="slack">How much wider or higher than a free rectangle a box may be and still go in; nought or more.</param>
        internal MaxRectsBin(double width, double height, double slack)
        {
            Width = width;
            Height = height;
            _slack = slack;
            _free.Add(new Free(0.0, 0.0, width, height));
        }

        /// <summary>Gets the width of the bin.</summary>
        internal double Width { get; }

        /// <summary>Gets the height of the bin.</summary>
        internal double Height { get; }

        /// <summary>Gets how many free rectangles the bin keeps.</summary>
        internal int FreeCount => _free.Count;

        /// <summary>
        /// Puts a box in the bin where it goes highest, then furthest left, if it goes anywhere.
        /// </summary>
        /// <param name="width">The width of the box.</param>
        /// <param name="height">The height of the box.</param>
        /// <param name="x">Where the lower left corner of the box goes, across.</param>
        /// <param name="y">Where the lower left corner of the box goes, up.</param>
        /// <returns>False when no free rectangle takes the box; the bin is then as it was.</returns>
        internal bool TryPlace(double width, double height, out double x, out double y)
        {
            int best = -1;
            double bestTop = double.NegativeInfinity;
            double bestLeft = double.PositiveInfinity;
            for (int i = 0; i < _free.Count; i++)
            {
                Free free = _free[i];
                if (width <= free.Width + _slack && height <= free.Height + _slack
                    && (free.MaxY > bestTop || (free.MaxY == bestTop && free.MinX < bestLeft)))
                {
                    best = i;
                    bestTop = free.MaxY;
                    bestLeft = free.MinX;
                }
            }

            if (best < 0)
            {
                x = 0.0;
                y = 0.0;
                return false;
            }

            // Its top to the top of the free rectangle, its left side to the left.
            x = bestLeft;
            y = bestTop - height;
            Take(new Free(x, y, x + width, y + height));
            return true;
        }

        /// <summary>
        /// Takes a placed box out of the free space: each free rectangle it overlaps gives way to the largest rectangles
        /// left of it on each side of the box, and a rectangle another one holds is dropped.
        /// </summary>
        private void Take(Free placed)
        {
            var kept = new List<Free>(_free.Count + 4);
            var split = new List<Free>();
            foreach (Free free in _free)
            {
                if (placed.MinX >= free.MaxX || placed.MaxX <= free.MinX || placed.MinY >= free.MaxY || placed.MaxY <= free.MinY)
                {
                    kept.Add(free);
                    continue;
                }

                if (placed.MinX > free.MinX)
                {
                    Add(split, new Free(free.MinX, free.MinY, placed.MinX, free.MaxY));
                }

                if (placed.MaxX < free.MaxX)
                {
                    Add(split, new Free(placed.MaxX, free.MinY, free.MaxX, free.MaxY));
                }

                if (placed.MinY > free.MinY)
                {
                    Add(split, new Free(free.MinX, free.MinY, free.MaxX, placed.MinY));
                }

                if (placed.MaxY < free.MaxY)
                {
                    Add(split, new Free(free.MinX, placed.MaxY, free.MaxX, free.MaxY));
                }
            }

            // No rectangle kept whole holds another, as none did before; one split off can be held by one kept whole, or
            // by another split off, and of two the same the first stays.
            for (int i = 0; i < split.Count; i++)
            {
                bool held = false;
                for (int j = 0; j < split.Count && !held; j++)
                {
                    held = j != i && split[j].Holds(split[i]) && (j < i || !split[i].Holds(split[j]));
                }

                for (int j = 0; j < kept.Count && !held; j++)
                {
                    held = kept[j].Holds(split[i]);
                }

                if (!held)
                {
                    kept.Add(split[i]);
                }
            }

            _free = kept;
        }

        // A sliver no wider than the slack holds nothing worth the keeping.
        private void Add(List<Free> split, Free free)
        {
            if (free.Width > _slack && free.Height > _slack)
            {
                split.Add(free);
            }
        }

        /// <summary>
        /// A free rectangle, kept by its four edges so that splitting it again and again never moves them.
        /// </summary>
        private readonly struct Free
        {
            internal Free(double minX, double minY, double maxX, double maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            internal double MinX { get; }

            internal double MinY { get; }

            internal double MaxX { get; }

            internal double MaxY { get; }

            internal double Width => MaxX - MinX;

            internal double Height => MaxY - MinY;

            internal bool Holds(Free other)
                => other.MinX >= MinX && other.MinY >= MinY && other.MaxX <= MaxX && other.MaxY <= MaxY;
        }
    }
}
