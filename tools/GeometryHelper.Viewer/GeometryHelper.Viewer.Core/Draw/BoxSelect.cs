using System;
using System.Collections.Generic;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// How a rectangle dragged over the view selects.
    /// </summary>
    public enum BoxMode
    {
        /// <summary>
        /// Dragged from left to right: an item is selected when all of it is inside the rectangle.
        /// </summary>
        Window,

        /// <summary>
        /// Dragged from right to left: an item is selected when any of it is inside the rectangle or crosses it.
        /// </summary>
        Crossing,
    }

    /// <summary>
    /// Selects the items inside or touching a rectangle of the screen. It works on what is drawn (the points, the chords of the curves,
    /// the triangles and the labels) in double precision, so that a curve is judged to the chord tolerance of its drawing, a tenth of
    /// the point tolerance of the scene.
    /// </summary>
    public static class BoxSelect
    {
        /// <summary>
        /// Gets how a drag selects: from left to right a window, from right to left a crossing.
        /// </summary>
        /// <param name="startX">Where the drag started across the picture.</param>
        /// <param name="endX">Where it ended.</param>
        public static BoxMode ModeOf(double startX, double endX) => endX >= startX ? BoxMode.Window : BoxMode.Crossing;

        /// <summary>
        /// Finds the items a rectangle selects.
        /// </summary>
        /// <param name="primitives">What the scene is drawn with.</param>
        /// <param name="map">The view.</param>
        /// <param name="x1">One corner of the rectangle across the picture, in pixels.</param>
        /// <param name="y1">That corner down the picture.</param>
        /// <param name="x2">The opposite corner across the picture.</param>
        /// <param name="y2">That corner down the picture.</param>
        /// <param name="mode">Whether an item has to be wholly inside, or only touch.</param>
        /// <param name="visible">Tells whether an item, by its index, is shown; null for all.</param>
        /// <returns>
        /// The indexes of the items, in ascending order. An item with nothing to draw is never selected; in perspective, a part of an item
        /// behind the near plane is not in the picture, so a window leaves an item that reaches behind it out, and a crossing judges only
        /// the part in front.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives or the map is null.</exception>
        public static List<int> Select(Primitives primitives, ScreenMap map, double x1, double y1, double x2, double y2, BoxMode mode, Func<int, bool> visible)
        {
            if (primitives == null)
            {
                throw new ArgumentNullException(nameof(primitives));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            var rect = new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));
            var selected = new List<int>();

            for (int i = 0; i < primitives.Items.Count; i++)
            {
                if (visible != null && !visible(i))
                {
                    continue;
                }

                ItemPrimitives p = primitives.Items[i];

                if (mode == BoxMode.Window ? AllInside(p, map, rect) : AnyTouching(p, map, rect))
                {
                    selected.Add(i);
                }
            }

            return selected;
        }

        private readonly struct Rect
        {
            internal Rect(double left, double top, double right, double bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            internal double Left { get; }

            internal double Top { get; }

            internal double Right { get; }

            internal double Bottom { get; }

            internal bool Contains(double x, double y) => x >= Left && x <= Right && y >= Top && y <= Bottom;
        }

        private static bool AllInside(ItemPrimitives p, ScreenMap map, Rect rect)
        {
            bool any = false;

            bool Inside(Vec3 v)
            {
                any = true;
                return map.TryProject(v, out double x, out double y, out _) && rect.Contains(x, y);
            }

            foreach (Vec3 v in p.Points)
            {
                if (!Inside(v))
                {
                    return false;
                }
            }

            foreach (DrawLabel label in p.Labels)
            {
                if (!Inside(label.At))
                {
                    return false;
                }
            }

            foreach (DrawPolyline line in p.Polylines)
            {
                foreach (Vec3 v in line.Points)
                {
                    if (!Inside(v))
                    {
                        return false;
                    }
                }
            }

            foreach (DrawTriangle t in p.Triangles)
            {
                if (!Inside(t.A) || !Inside(t.B) || !Inside(t.C))
                {
                    return false;
                }
            }

            return any;
        }

        private static bool AnyTouching(ItemPrimitives p, ScreenMap map, Rect rect)
        {
            foreach (Vec3 v in p.Points)
            {
                if (map.TryProject(v, out double x, out double y, out _) && rect.Contains(x, y))
                {
                    return true;
                }
            }

            foreach (DrawLabel label in p.Labels)
            {
                if (map.TryProject(label.At, out double x, out double y, out _) && rect.Contains(x, y))
                {
                    return true;
                }
            }

            foreach (DrawPolyline line in p.Polylines)
            {
                int count = line.Points.Length;
                int segments = line.Closed ? count : count - 1;

                for (int s = 0; s < segments; s++)
                {
                    if (map.ClipSegment(line.Points[s], line.Points[(s + 1) % count], out Vec3 from, out Vec3 to)
                        && map.TryProject(from, out double ax, out double ay, out _)
                        && map.TryProject(to, out double bx, out double by, out _)
                        && SegmentTouches(ax, ay, bx, by, rect))
                    {
                        return true;
                    }
                }
            }

            foreach (DrawTriangle t in p.Triangles)
            {
                if (TriangleTouches(t, map, rect))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets whether a triangle, cut to the part in front of the near plane, overlaps the rectangle on the screen.
        /// </summary>
        private static bool TriangleTouches(DrawTriangle t, ScreenMap map, Rect rect)
        {
            var corners = new List<Vec3>(4) { t.A, t.B, t.C };

            if (!map.Orthographic)
            {
                // Cut the triangle as a polygon by the near plane and then by the far plane: the corners between them, and where each edge that
                // crosses a plane meets it.
                double clipFar = map.Far * (1.0 - ScreenMap.DepthSlack);
                corners = ClipPolygon(corners, v => map.DepthOf(v) - map.ClipDepth);
                corners = ClipPolygon(corners, v => clipFar - map.DepthOf(v));
            }
            if (corners.Count < 3)
            {
                return false;
            }

            var xs = new double[corners.Count];
            var ys = new double[corners.Count];

            for (int i = 0; i < corners.Count; i++)
            {
                if (!map.TryProject(corners[i], out xs[i], out ys[i], out _))
                {
                    return false;
                }

                if (rect.Contains(xs[i], ys[i]))
                {
                    return true;
                }
            }

            for (int i = 0; i < corners.Count; i++)
            {
                int j = (i + 1) % corners.Count;

                if (SegmentTouches(xs[i], ys[i], xs[j], ys[j], rect))
                {
                    return true;
                }
            }

            // No corner inside and no edge across: the rectangle is inside the triangle or apart from it.
            return PolygonHolds(xs, ys, rect.Left, rect.Top);
        }

        /// <summary>
        /// Keeps the part of a convex polygon where a distance is not negative: the corners that are, and where each edge across it meets it.
        /// </summary>
        private static List<Vec3> ClipPolygon(List<Vec3> corners, Func<Vec3, double> distance)
        {
            var kept = new List<Vec3>(corners.Count + 1);

            for (int i = 0; i < corners.Count; i++)
            {
                Vec3 a = corners[i];
                Vec3 b = corners[(i + 1) % corners.Count];
                double da = distance(a);
                double db = distance(b);

                if (da >= 0.0)
                {
                    kept.Add(a);
                }

                if ((da >= 0.0) != (db >= 0.0))
                {
                    kept.Add(a + (b - a) * (da / (da - db)));
                }
            }

            return kept;
        }

        /// <summary>
        /// Gets whether a convex polygon holds a point, whichever way round it is wound.
        /// </summary>
        private static bool PolygonHolds(double[] xs, double[] ys, double px, double py)
        {
            bool positive = false;
            bool negative = false;

            for (int i = 0; i < xs.Length; i++)
            {
                int j = (i + 1) % xs.Length;
                double cross = (xs[j] - xs[i]) * (py - ys[i]) - (ys[j] - ys[i]) * (px - xs[i]);

                if (cross > 0.0)
                {
                    positive = true;
                }
                else if (cross < 0.0)
                {
                    negative = true;
                }
            }

            return !(positive && negative);
        }

        /// <summary>
        /// Gets whether a segment on the screen meets the rectangle, by clipping it against the four sides (Liang and Barsky).
        /// </summary>
        private static bool SegmentTouches(double x1, double y1, double x2, double y2, Rect rect)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            double t0 = 0.0;
            double t1 = 1.0;
            double[] p = { -dx, dx, -dy, dy };
            double[] q = { x1 - rect.Left, rect.Right - x1, y1 - rect.Top, rect.Bottom - y1 };

            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0.0)
                {
                    if (q[i] < 0.0)
                    {
                        return false;
                    }

                    continue;
                }

                double r = q[i] / p[i];

                if (p[i] < 0.0)
                {
                    if (r > t1)
                    {
                        return false;
                    }

                    t0 = Math.Max(t0, r);
                }
                else
                {
                    if (r < t0)
                    {
                        return false;
                    }

                    t1 = Math.Min(t1, r);
                }
            }

            return t0 <= t1;
        }
    }
}
