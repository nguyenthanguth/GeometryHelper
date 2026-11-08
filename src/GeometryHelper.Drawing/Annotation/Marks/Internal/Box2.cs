using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The tests marks and symbols are kept apart by: boxes square to the view's axes overlapping, a line crossing a box,
    /// and the point of a box nearest a point. A touch within the point tolerance is not an overlap.
    /// </summary>
    internal static class Box2
    {
        /// <summary>Whether two boxes overlap by more than the tolerance both ways.</summary>
        internal static bool Overlap(GeoRectangle2 a, GeoRectangle2 b, double tol)
        {
            Bounds(a, out double ax0, out double ay0, out double ax1, out double ay1);
            Bounds(b, out double bx0, out double by0, out double bx1, out double by1);
            return Math.Min(ax1, bx1) - Math.Max(ax0, bx0) > tol && Math.Min(ay1, by1) - Math.Max(ay0, by0) > tol;
        }

        /// <summary>Whether a line runs into a box further than the tolerance off its sides.</summary>
        internal static bool Crosses(GeoRectangle2 box, GeoPoint2 p, GeoPoint2 q, double tol)
        {
            Bounds(box, out double x0, out double y0, out double x1, out double y1);
            x0 += tol;
            y0 += tol;
            x1 -= tol;
            y1 -= tol;

            if (x0 >= x1 || y0 >= y1)
            {
                return false;
            }

            // Liang and Barsky: the part of the line from p to q inside the box shrunk by the tolerance.
            double dx = q.X - p.X, dy = q.Y - p.Y, t0 = 0.0, t1 = 1.0;
            return Clip(-dx, p.X - x0, ref t0, ref t1) && Clip(dx, x1 - p.X, ref t0, ref t1)
                && Clip(-dy, p.Y - y0, ref t0, ref t1) && Clip(dy, y1 - p.Y, ref t0, ref t1)
                && t0 <= t1;
        }

        /// <summary>The point of a box's outline nearest a point; for a point inside, the nearest point of its nearest side.</summary>
        internal static GeoPoint2 Nearest(GeoRectangle2 box, GeoPoint2 p)
        {
            Bounds(box, out double x0, out double y0, out double x1, out double y1);
            double x = Math.Min(Math.Max(p.X, x0), x1);
            double y = Math.Min(Math.Max(p.Y, y0), y1);

            if (x > x0 && x < x1 && y > y0 && y < y1)
            {
                double left = x - x0, right = x1 - x, down = y - y0, up = y1 - y;
                double least = Math.Min(Math.Min(left, right), Math.Min(down, up));

                if (least == left)
                {
                    x = x0;
                }
                else if (least == right)
                {
                    x = x1;
                }
                else if (least == down)
                {
                    y = y0;
                }
                else
                {
                    y = y1;
                }
            }

            return new GeoPoint2(x, y);
        }

        /// <summary>The least and most X and Y of a box, turned or not.</summary>
        internal static void Bounds(GeoRectangle2 box, out double x0, out double y0, out double x1, out double y1)
        {
            if (box.AngleRad == 0.0)
            {
                x0 = box.Center.X - box.Width * 0.5;
                x1 = box.Center.X + box.Width * 0.5;
                y0 = box.Center.Y - box.Height * 0.5;
                y1 = box.Center.Y + box.Height * 0.5;
                return;
            }

            x0 = y0 = double.PositiveInfinity;
            x1 = y1 = double.NegativeInfinity;

            foreach (GeoPoint2 corner in box.GetVertices())
            {
                x0 = Math.Min(x0, corner.X);
                y0 = Math.Min(y0, corner.Y);
                x1 = Math.Max(x1, corner.X);
                y1 = Math.Max(y1, corner.Y);
            }
        }

        /// <summary>A box square to the axes from its least corner and its size.</summary>
        internal static GeoRectangle2 FromCorner(double x, double y, double width, double height)
            => new GeoRectangle2(new GeoPoint2(x + width * 0.5, y + height * 0.5), width, height);

        /// <summary>A thin box round a line, as wide as twice a half-width, for keeping labels off it.</summary>
        internal static GeoPolygon2 Around(GeoPoint2 p, GeoPoint2 q, double half)
        {
            double dx = q.X - p.X, dy = q.Y - p.Y, length = Math.Sqrt(dx * dx + dy * dy);
            double ux = length > 0.0 ? dx / length : 1.0, uy = length > 0.0 ? dy / length : 0.0;
            double ax = -uy * half, ay = ux * half, bx = ux * half, by = uy * half;
            return new GeoPolygon2(
                new GeoPoint2(p.X - bx - ax, p.Y - by - ay),
                new GeoPoint2(q.X + bx - ax, q.Y + by - ay),
                new GeoPoint2(q.X + bx + ax, q.Y + by + ay),
                new GeoPoint2(p.X - bx + ax, p.Y - by + ay));
        }

        private static bool Clip(double p, double q, ref double t0, ref double t1)
        {
            if (p == 0.0)
            {
                return q >= 0.0;
            }

            double r = q / p;

            if (p < 0.0)
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

            return true;
        }
    }
}
