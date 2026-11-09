using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The selections by a polygon and by a fence: WPolygon, CPolygon, Fence and the lasso.</summary>
    public sealed partial class PickIndex
    {
        /// <summary>
        /// Gets the entities drawn wholly inside a polygon, as AutoCAD's WPolygon takes them, and a lasso drawn clockwise: every
        /// item of the entity inside it and none crossing its edges. A line from (10, 10) to (20, 10) lies in the square from
        /// (0, 0) to (30, 30); a line without end never does.
        /// </summary>
        /// <param name="ring">The corners of the polygon, closed back to the first.</param>
        /// <returns>The handles, in ordinal order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the corners are null.</exception>
        public IReadOnlyList<string> WindowPolygon(IReadOnlyList<GeoPoint2> ring) => Polygon(ring, false);

        /// <summary>
        /// Gets the entities a polygon touches, as AutoCAD's CPolygon takes them, and a lasso drawn counter-clockwise: those an
        /// item of which lies inside it or crosses its edges, and a fill it lies inside.
        /// </summary>
        /// <param name="ring">The corners of the polygon, closed back to the first.</param>
        /// <returns>The handles, in ordinal order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the corners are null.</exception>
        public IReadOnlyList<string> CrossingPolygon(IReadOnlyList<GeoPoint2> ring) => Polygon(ring, true);

        /// <summary>
        /// Gets the entities a fence crosses, as AutoCAD's Fence takes them: those an item of which meets one of the fence's
        /// segments, touching it counted. A fence from (0, 5) to (30, 5) takes a line from (10, 0) to (10, 10), not one from
        /// (10, 6) to (20, 6). A fence of fewer than two points takes nothing.
        /// </summary>
        /// <param name="fence">The points of the fence, not closed.</param>
        /// <returns>The handles, in ordinal order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        public IReadOnlyList<string> Fence(IReadOnlyList<GeoPoint2> fence)
        {
            if (fence == null)
            {
                throw new ArgumentNullException(nameof(fence));
            }

            var found = new List<string>();

            if (fence.Count < 2)
            {
                return found;
            }

            DisplayBox bounds = Bounds(fence);
            var handles = new SortedSet<string>(StringComparer.Ordinal);

            foreach (DisplayItem2 item in ItemsNear(bounds))
            {
                handles.Add(item.Handle);
            }

            foreach (string handle in handles)
            {
                foreach (int i in IndicesOf(handle))
                {
                    if (Meets(_list.Items[i], fence, bounds))
                    {
                        found.Add(handle);
                        break;
                    }
                }
            }

            return found;
        }

        /// <summary>The entities a polygon takes: those whose every item lies inside it, or for a crossing those an item of which it touches; ordinal order.</summary>
        private IReadOnlyList<string> Polygon(IReadOnlyList<GeoPoint2> ring, bool crossing)
        {
            if (ring == null)
            {
                throw new ArgumentNullException(nameof(ring));
            }

            var found = new List<string>();

            if (ring.Count < 3)
            {
                return found;
            }

            var handles = new SortedSet<string>(StringComparer.Ordinal);

            foreach (DisplayItem2 item in ItemsNear(Bounds(ring)))
            {
                handles.Add(item.Handle);
            }

            foreach (string handle in handles)
            {
                bool any = false;
                bool all = true;

                foreach (int i in IndicesOf(handle))
                {
                    DisplayItem2 item = _list.Items[i];
                    bool inside = Inside(item, ring, out bool touches);
                    any |= touches;
                    all &= inside;
                }

                if (crossing ? any : all)
                {
                    found.Add(handle);
                }
            }

            return found;
        }

        /// <summary>Whether an item lies wholly inside a ring: every vertex inside and no edge crossing the ring; and whether it touches the ring at all.</summary>
        private static bool Inside(DisplayItem2 item, IReadOnlyList<GeoPoint2> ring, out bool touches)
        {
            if (item is DisplayRay2 ray)
            {
                // A line without end is never wholly inside; it touches the ring where its part across the ring's box,
                // grown so that its ends lie outside the ring, crosses an edge.
                DisplayBox around = Bounds(ring);
                around = around.Inflate(Math.Max(1.0, Math.Max(around.Width, around.Height)));
                touches = ray.TryClip(around, out double x0, out double y0, out double x1, out double y1) && Crosses(ring, x0, y0, x1, y1);
                return false;
            }

            bool inside = true;
            touches = item is DisplayFill2 fill && fill.Contains(ring[0].X, ring[0].Y);

            foreach (KeyValuePair<double[], bool> chain in Chains(item))
            {
                double[] xy = chain.Key;
                int n = xy.Length / 2;

                for (int k = 0; k < n; k++)
                {
                    bool vertexIn = Contains(ring, xy[2 * k], xy[2 * k + 1]);
                    touches |= vertexIn;
                    inside &= vertexIn;
                    int next = k + 1 < n ? k + 1 : chain.Value && n > 2 ? 0 : -1;

                    if (next >= 0 && Crosses(ring, xy[2 * k], xy[2 * k + 1], xy[2 * next], xy[2 * next + 1]))
                    {
                        touches = true;
                        inside = false;
                    }
                }
            }

            return inside;
        }

        /// <summary>Whether an item meets a fence: a segment of its outline meeting a segment of the fence, touching counted; a line without end clipped to the fence's box first.</summary>
        private static bool Meets(DisplayItem2 item, IReadOnlyList<GeoPoint2> fence, DisplayBox bounds)
        {
            if (item is DisplayRay2 ray)
            {
                DisplayBox around = bounds.Inflate(Math.Max(1.0, Math.Max(bounds.Width, bounds.Height)));
                return ray.TryClip(around, out double x0, out double y0, out double x1, out double y1) && MeetsFence(fence, x0, y0, x1, y1);
            }

            foreach (KeyValuePair<double[], bool> chain in Chains(item))
            {
                double[] xy = chain.Key;
                int n = xy.Length / 2;

                if (n == 1 && MeetsFence(fence, xy[0], xy[1], xy[0], xy[1]))
                {
                    return true;
                }

                for (int k = 0; k + 1 < n || (chain.Value && n > 2 && k < n); k++)
                {
                    int next = k + 1 < n ? k + 1 : 0;

                    if (MeetsFence(fence, xy[2 * k], xy[2 * k + 1], xy[2 * next], xy[2 * next + 1]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether a segment meets one of the segments of a fence, an end on a segment or two segments along one line counted.</summary>
        private static bool MeetsFence(IReadOnlyList<GeoPoint2> fence, double ax, double ay, double bx, double by)
        {
            for (int i = 1; i < fence.Count; i++)
            {
                GeoPoint2 c = fence[i - 1];
                GeoPoint2 d = fence[i];

                if (Math.Max(ax, bx) < Math.Min(c.X, d.X) || Math.Min(ax, bx) > Math.Max(c.X, d.X) || Math.Max(ay, by) < Math.Min(c.Y, d.Y) || Math.Min(ay, by) > Math.Max(c.Y, d.Y))
                {
                    continue;
                }

                double d1 = Side(c.X, c.Y, d.X, d.Y, ax, ay);
                double d2 = Side(c.X, c.Y, d.X, d.Y, bx, by);
                double d3 = Side(ax, ay, bx, by, c.X, c.Y);
                double d4 = Side(ax, ay, bx, by, d.X, d.Y);

                // Of opposite sides or on the line, each of the other; two segments along one line meet where their boxes do.
                if (!(d1 > 0.0 && d2 > 0.0) && !(d1 < 0.0 && d2 < 0.0) && !(d3 > 0.0 && d4 > 0.0) && !(d3 < 0.0 && d4 < 0.0))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The outline of an item as chains of points, each with whether it closes: an arc flattened, a fill's rings, the box of a text or a viewport.</summary>
        private static IEnumerable<KeyValuePair<double[], bool>> Chains(DisplayItem2 item)
        {
            switch (item)
            {
                case DisplayPolyline2 chain:
                    yield return new KeyValuePair<double[], bool>(chain.Xy.ToArray(), chain.Closed);
                    break;
                case DisplaySegments2 segments:
                    for (int i = 0; i + 3 < segments.Xy.Count; i += 4)
                    {
                        yield return new KeyValuePair<double[], bool>(new[] { segments.Xy[i], segments.Xy[i + 1], segments.Xy[i + 2], segments.Xy[i + 3] }, false);
                    }

                    break;
                case DisplayArc2 arc:
                    yield return new KeyValuePair<double[], bool>(arc.Flatten(Math.Max(arc.Radius * 1E-3, 1E-9)), false);
                    break;
                case DisplayFill2 fill:
                    foreach (double[] ring in fill.Rings)
                    {
                        yield return new KeyValuePair<double[], bool>(ring, true);
                    }

                    break;
                default:
                    DisplayBox box = item.Bounds;

                    if (!box.IsEmpty)
                    {
                        yield return new KeyValuePair<double[], bool>(new[] { box.MinX, box.MinY, box.MaxX, box.MinY, box.MaxX, box.MaxY, box.MinX, box.MaxY }, true);
                    }

                    break;
            }
        }

        /// <summary>Whether a point lies inside a ring, by the even-odd rule.</summary>
        private static bool Contains(IReadOnlyList<GeoPoint2> ring, double x, double y)
        {
            bool inside = false;

            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                GeoPoint2 a = ring[i];
                GeoPoint2 b = ring[j];

                if ((a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Whether a segment crosses an edge of a ring, each through the other's line.</summary>
        private static bool Crosses(IReadOnlyList<GeoPoint2> ring, double ax, double ay, double bx, double by)
        {
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                GeoPoint2 c = ring[j];
                GeoPoint2 d = ring[i];

                if (Math.Max(ax, bx) < Math.Min(c.X, d.X) || Math.Min(ax, bx) > Math.Max(c.X, d.X) || Math.Max(ay, by) < Math.Min(c.Y, d.Y) || Math.Min(ay, by) > Math.Max(c.Y, d.Y))
                {
                    continue;
                }

                double d1 = Side(c.X, c.Y, d.X, d.Y, ax, ay);
                double d2 = Side(c.X, c.Y, d.X, d.Y, bx, by);
                double d3 = Side(ax, ay, bx, by, c.X, c.Y);
                double d4 = Side(ax, ay, bx, by, d.X, d.Y);

                if (((d1 > 0.0 && d2 < 0.0) || (d1 < 0.0 && d2 > 0.0)) && ((d3 > 0.0 && d4 < 0.0) || (d3 < 0.0 && d4 > 0.0)))
                {
                    return true;
                }
            }

            return false;
        }

        private static double Side(double ax, double ay, double bx, double by, double px, double py) => (bx - ax) * (py - ay) - (by - ay) * (px - ax);

        private static DisplayBox Bounds(IReadOnlyList<GeoPoint2> points)
        {
            DisplayBox box = DisplayBox.Empty;

            foreach (GeoPoint2 point in points)
            {
                box = box.Add(point.X, point.Y);
            }

            return box;
        }
    }
}
