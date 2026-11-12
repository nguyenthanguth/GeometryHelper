using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The part of a clipped block reference that is shown (XCLIP): a ring in drawing units, and within another ring where the
    /// reference stands in a clipped block. Every item the reference draws carries it as <see cref="DisplayItem2.BlockClip"/>; the
    /// canvas draws the item inside the region alone, a pick finds it there, and a window or a crossing meets only the part of
    /// it there. A clip turned inside out is a ring that goes round the block's extents and in to the boundary by a slit, as
    /// AutoCAD keeps one, so that it is no different here.
    /// </summary>
    public sealed class DisplayClipRegion
    {
        private readonly double[] _ring;

        /// <summary>Makes a region.</summary>
        /// <param name="outline">The ring, in drawing units, its edges straight.</param>
        /// <param name="parent">The region the reference's own is within, or null.</param>
        /// <exception cref="ArgumentNullException">Thrown when the outline is null.</exception>
        public DisplayClipRegion(DisplayClip2 outline, DisplayClipRegion parent)
        {
            Outline = outline ?? throw new ArgumentNullException(nameof(outline));
            Parent = parent;
            _ring = outline.Flatten(1.0);
            double minX = double.MaxValue,
                minY = double.MaxValue,
                maxX = double.MinValue,
                maxY = double.MinValue;

            for (int i = 0; i + 1 < _ring.Length; i += 2)
            {
                minX = Math.Min(minX, _ring[i]);
                maxX = Math.Max(maxX, _ring[i]);
                minY = Math.Min(minY, _ring[i + 1]);
                maxY = Math.Max(maxY, _ring[i + 1]);
            }

            var own = new DisplayBox(minX, minY, maxX, maxY);

            if (parent != null)
            {
                DisplayBox outer = parent.Bounds;
                own = new DisplayBox(
                    Math.Max(own.MinX, outer.MinX),
                    Math.Max(own.MinY, outer.MinY),
                    Math.Min(own.MaxX, outer.MaxX),
                    Math.Min(own.MaxY, outer.MaxY)
                );
            }

            Bounds = own;
        }

        /// <summary>Gets the ring of this reference's clip.</summary>
        public DisplayClip2 Outline { get; }

        /// <summary>Gets the region of the clipped block this reference stands in; null for a reference of the space itself.</summary>
        public DisplayClipRegion Parent { get; }

        /// <summary>Gets the box that holds what is shown: the ring's box within its parent's; empty where the two do not meet.</summary>
        public DisplayBox Bounds { get; }

        /// <summary>Gets the points of the ring as x, y pairs, the last not repeating the first.</summary>
        public double[] Ring => _ring;

        /// <summary>Gets whether a point is in the region, inside its ring and its parent's; a point on an edge may fall either side.</summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>Whether it is.</returns>
        public bool Contains(double x, double y) => Outline.Contains(x, y) && (Parent == null || Parent.Contains(x, y));

        /// <summary>
        /// Gets whether a point is in the region or within a distance of it, as a pick asks for what lies under the pickbox: the
        /// point is inside every ring, or no farther than the distance from the edge of each it is outside.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="distance">The distance, in drawing units.</param>
        /// <returns>Whether it is.</returns>
        public bool Reaches(double x, double y, double distance) =>
            (Outline.Contains(x, y) || Outline.DistanceToEdge(x, y) <= distance)
            && (Parent == null || Parent.Reaches(x, y, distance));

        /// <summary>
        /// Gets whether the region meets a box: a vertex of the ring is in the box, a corner of the box is in the region, or an
        /// edge of the ring crosses the box; as a crossing selection, and the aperture of a snap, ask. Of nested rings the box
        /// is held to each in turn, which is a little more than is shown where the two cut one another.
        /// </summary>
        /// <param name="box">The box.</param>
        /// <returns>Whether it does.</returns>
        public bool Meets(DisplayBox box)
        {
            if (!Bounds.Intersects(box) || (Parent != null && !Parent.Meets(box)))
            {
                return false;
            }

            return RingMeets(box);
        }

        private bool RingMeets(DisplayBox box)
        {
            int n = _ring.Length / 2;

            for (int i = 0; i < n; i++)
            {
                if (box.Contains(_ring[2 * i], _ring[2 * i + 1]))
                {
                    return true;
                }
            }

            if (
                Outline.Contains(box.MinX, box.MinY)
                || Outline.Contains(box.MaxX, box.MinY)
                || Outline.Contains(box.MaxX, box.MaxY)
                || Outline.Contains(box.MinX, box.MaxY)
            )
            {
                return true;
            }

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (SegmentMeets(box, _ring[2 * j], _ring[2 * j + 1], _ring[2 * i], _ring[2 * i + 1]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the segment from one point to another passes through a box (Liang and Barsky).</summary>
        private static bool SegmentMeets(DisplayBox box, double x0, double y0, double x1, double y1)
        {
            double t0 = 0.0;
            double t1 = 1.0;
            double dx = x1 - x0;
            double dy = y1 - y0;
            double[] p = { -dx, dx, -dy, dy };
            double[] q = { x0 - box.MinX, box.MaxX - x0, y0 - box.MinY, box.MaxY - y0 };

            for (int k = 0; k < 4; k++)
            {
                if (p[k] == 0.0)
                {
                    if (q[k] < 0.0)
                    {
                        return false;
                    }

                    continue;
                }

                double t = q[k] / p[k];

                if (p[k] < 0.0)
                {
                    t0 = Math.Max(t0, t);
                }
                else
                {
                    t1 = Math.Min(t1, t);
                }

                if (t0 > t1)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Gets a region moved along X and Y, as the reference's items are moved by a paste's preview.</summary>
        /// <param name="dx">How far along X.</param>
        /// <param name="dy">How far along Y.</param>
        /// <returns>The moved region.</returns>
        public DisplayClipRegion Moved(double dx, double dy)
        {
            int n = Outline.Count;
            var xy = new double[2 * n];
            var bulges = new double[n];

            for (int i = 0; i < n; i++)
            {
                xy[2 * i] = Outline.X(i) + dx;
                xy[2 * i + 1] = Outline.Y(i) + dy;
                bulges[i] = Outline.Bulge(i);
            }

            return new DisplayClipRegion(new DisplayClip2(xy, bulges), Parent?.Moved(dx, dy));
        }
    }
}
