using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The clip boundaries of block references and external references (XCLIP) as the presenter works them: the clip system a
    /// boundary is picked in, the boundary a clip turned inside out is kept as, and the rectangle a boundary of four corners is
    /// kept as, all as AutoCAD 2021 keeps them.
    /// </summary>
    public static class CadXClips
    {
        // How far a slit is opened, as AutoCAD opens one.
        private const double Slit = 4E-5;

        // How far the outer ring of an inverted clip lies beyond the block's extents, a share of each side.
        private const double Margin = 0.05;

        // The room the outer ring is given beyond a polygon that reaches it.
        private const double Room = 0.04;

        /// <summary>
        /// Gets the transformation that carries a point of the world into a user coordinate system, which a boundary's points are
        /// given in: the identity for the world's. A boundary of a clip is kept against it.
        /// </summary>
        /// <param name="ucs">The user coordinate system.</param>
        /// <returns>The transformation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the system is null.</exception>
        public static GeoTransform3 WorldToClip(CadUcs ucs)
        {
            if (ucs == null)
            {
                throw new ArgumentNullException(nameof(ucs));
            }

            GeoPoint3 o = ucs.Origin;
            GeoVector3 x = ucs.XAxis;
            GeoVector3 y = ucs.YAxis;
            GeoVector3 z = ucs.ZAxis;

            // The columns are the axes and the origin: the system's point into the world; the clip is the way back.
            var toWorld = new GeoTransform3(
                new[,]
                {
                    { x.X, y.X, z.X, o.X },
                    { x.Y, y.Y, z.Y, o.Y },
                    { x.Z, y.Z, z.Z, o.Z },
                    { 0.0, 0.0, 0.0, 1.0 },
                }
            );
            return toWorld.Inverse(new Tolerance(1E-12, 1E-12));
        }

        /// <summary>
        /// Gets the boundary a rectangle is kept as: its two opposite corners, as AutoCAD keeps a rectangle picked by Rectangular
        /// and a polyline of four corners square to the axes of the clip system; the polygon's own points otherwise.
        /// </summary>
        /// <param name="points">The points of a polygon, the last not repeating the first.</param>
        /// <param name="tolerance">How far a side may lie from an axis and be square to it.</param>
        /// <returns>Two points where the polygon is such a rectangle, else the points.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        public static IReadOnlyList<GeoPoint2> AsRectangle(IReadOnlyList<GeoPoint2> points, double tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count != 4)
            {
                return points;
            }

            bool horizontalFirst =
                Math.Abs(points[0].Y - points[1].Y) <= tolerance
                && Math.Abs(points[1].X - points[2].X) <= tolerance
                && Math.Abs(points[2].Y - points[3].Y) <= tolerance
                && Math.Abs(points[3].X - points[0].X) <= tolerance;
            bool verticalFirst =
                Math.Abs(points[0].X - points[1].X) <= tolerance
                && Math.Abs(points[1].Y - points[2].Y) <= tolerance
                && Math.Abs(points[2].X - points[3].X) <= tolerance
                && Math.Abs(points[3].Y - points[0].Y) <= tolerance;

            if (!horizontalFirst && !verticalFirst)
            {
                return points;
            }

            double x0 = Math.Min(Math.Min(points[0].X, points[1].X), Math.Min(points[2].X, points[3].X));
            double x1 = Math.Max(Math.Max(points[0].X, points[1].X), Math.Max(points[2].X, points[3].X));
            double y0 = Math.Min(Math.Min(points[0].Y, points[1].Y), Math.Min(points[2].Y, points[3].Y));
            double y1 = Math.Max(Math.Max(points[0].Y, points[1].Y), Math.Max(points[2].Y, points[3].Y));
            return new[] { new GeoPoint2(x0, y0), new GeoPoint2(x1, y1) };
        }

        /// <summary>
        /// Gets the boundary of a clip turned inside out (XCLIP's Invert clip) as AutoCAD keeps it: the points of the polygon with a
        /// slit opened from the vertex nearest the box to the box's edge, round the box, and back; what the ring holds is what is
        /// shown, the block outside the polygon. A rectangle's two corners are made the four of a polygon first. A polygon that
        /// reaches out of the box makes the box larger to hold it. The slit is 4E-5 wide.
        /// </summary>
        /// <param name="boundary">The polygon, or the two corners of a rectangle, in the clip coordinate system.</param>
        /// <param name="min">The least corner of the box that holds the block, in the clip system.</param>
        /// <param name="max">The greatest corner of that box.</param>
        /// <returns>The ring, the last point not repeating the first.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the boundary is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the boundary has fewer than two points.</exception>
        public static IReadOnlyList<GeoPoint2> Inverted(IReadOnlyList<GeoPoint2> boundary, GeoPoint2 min, GeoPoint2 max)
        {
            if (boundary == null)
            {
                throw new ArgumentNullException(nameof(boundary));
            }

            if (boundary.Count < 2)
            {
                throw new ArgumentException("A boundary has two points or more.", nameof(boundary));
            }

            var polygon = new List<GeoPoint2>(boundary);

            if (polygon.Count == 2)
            {
                GeoPoint2 a = polygon[0];
                GeoPoint2 b = polygon[1];
                polygon = new List<GeoPoint2>
                {
                    new GeoPoint2(a.X, a.Y),
                    new GeoPoint2(b.X, a.Y),
                    new GeoPoint2(b.X, b.Y),
                    new GeoPoint2(a.X, b.Y),
                };
            }

            double minX = Math.Min(min.X, max.X);
            double maxX = Math.Max(min.X, max.X);
            double minY = Math.Min(min.Y, max.Y);
            double maxY = Math.Max(min.Y, max.Y);
            double marginX = (maxX - minX) * Margin;
            double marginY = (maxY - minY) * Margin;
            minX -= marginX;
            maxX += marginX;
            minY -= marginY;
            maxY += marginY;
            double area = 0.0;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                area += polygon[j].X * polygon[i].Y - polygon[i].X * polygon[j].Y;

                // A polygon that reaches out of the box is held by a larger one.
                minX = Math.Min(minX, polygon[i].X - Room);
                maxX = Math.Max(maxX, polygon[i].X + Room);
                minY = Math.Min(minY, polygon[i].Y - Room);
                maxY = Math.Max(maxY, polygon[i].Y + Room);
            }

            // The vertex nearest an edge of the box, the last of those as near; the edge: 0 south, 1 east, 2 north, 3 west.
            int vertex = 0;
            int edge = 0;
            double nearest = double.MaxValue;

            for (int i = 0; i < polygon.Count; i++)
            {
                double[] away = { polygon[i].Y - minY, maxX - polygon[i].X, maxY - polygon[i].Y, polygon[i].X - minX };

                for (int e = 0; e < 4; e++)
                {
                    if (away[e] <= nearest)
                    {
                        nearest = away[e];
                        vertex = i;
                        edge = e;
                    }
                }
            }

            GeoPoint2 v = polygon[vertex];
            GeoPoint2 target =
                edge == 0 ? new GeoPoint2(v.X, minY)
                : edge == 1 ? new GeoPoint2(maxX, v.Y)
                : edge == 2 ? new GeoPoint2(v.X, maxY)
                : new GeoPoint2(minX, v.Y);

            // The slit's way, and across it, the first pass out set to one side by a hair.
            double dx =
                edge == 1 ? 1.0
                : edge == 3 ? -1.0
                : 0.0;
            double dy =
                edge == 2 ? 1.0
                : edge == 0 ? -1.0
                : 0.0;
            double ox = dy * Slit;
            double oy = -dx * Slit;

            // The corners of the box counter-clockwise from the south-west, and which way round the outside goes: against the
            // polygon, so that the slit and the box make a ring round it.
            GeoPoint2[] corners =
            {
                new GeoPoint2(minX, minY),
                new GeoPoint2(maxX, minY),
                new GeoPoint2(maxX, maxY),
                new GeoPoint2(minX, maxY),
            };
            bool outsideClockwise = area > 0.0;
            var ring = new List<GeoPoint2>(polygon.Count + 7);

            for (int i = 0; i < vertex; i++)
            {
                ring.Add(polygon[i]);
            }

            ring.Add(new GeoPoint2(v.X + ox, v.Y + oy));
            ring.Add(new GeoPoint2(target.X + ox, target.Y + oy));

            for (int k = 0; k < 4; k++)
            {
                int at = outsideClockwise ? (edge - k + 4) % 4 : (edge + 1 + k) % 4;
                ring.Add(corners[at]);
            }

            ring.Add(target);

            for (int i = vertex; i < polygon.Count; i++)
            {
                ring.Add(polygon[i]);
            }

            return ring;
        }

        /// <summary>
        /// Takes a boundary that <see cref="Inverted"/> made, or AutoCAD wrote for a clip turned inside out, back to its polygon:
        /// the seven points of the slit and the box round it left out. The slit is found wherever the ring starts: a point and
        /// the point a slit's width from it, a run of the four corners of an axis-aligned box between the slit's two passes, the
        /// passes ending on one side of the box and running square to it. A polygon of the clip, not a keyhole, is not one.
        /// </summary>
        /// <param name="boundary">The points of the boundary, in the clip coordinate system.</param>
        /// <param name="polygon">The polygon, in the order <see cref="Inverted"/> was given it where the slit lies within the ring,
        /// else from the point after the slit; empty where the boundary is not a keyhole.</param>
        /// <returns>true where the boundary is a keyhole.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the boundary is null.</exception>
        public static bool TryUninvert(IReadOnlyList<GeoPoint2> boundary, out IReadOnlyList<GeoPoint2> polygon)
        {
            if (boundary == null)
            {
                throw new ArgumentNullException(nameof(boundary));
            }

            polygon = new GeoPoint2[0];
            int n = boundary.Count;

            // A polygon of three points at least, and the seven of the slit.
            if (n < 10)
            {
                return false;
            }

            for (int k = 0; k < n; k++)
            {
                if (!IsSlitAt(boundary, k))
                {
                    continue;
                }

                var kept = new List<GeoPoint2>(n - 7);

                if (k + 7 <= n)
                {
                    for (int i = 0; i < k; i++)
                    {
                        kept.Add(boundary[i]);
                    }

                    for (int i = k + 7; i < n; i++)
                    {
                        kept.Add(boundary[i]);
                    }
                }
                else
                {
                    for (int i = 7; i < n; i++)
                    {
                        kept.Add(boundary[(k + i) % n]);
                    }
                }

                polygon = kept;
                return true;
            }

            return false;
        }

        /// <summary>Whether the seven points from index k, round the ring, are the slit and the box <see cref="Inverted"/> opens, and the point after them the vertex it leaves from.</summary>
        private static bool IsSlitAt(IReadOnlyList<GeoPoint2> ring, int k)
        {
            const double Eps = 1E-6;
            int n = ring.Count;
            GeoPoint2 a = ring[k];
            GeoPoint2 b = ring[(k + 1) % n];
            GeoPoint2 t = ring[(k + 6) % n];
            GeoPoint2 v = ring[(k + 7) % n];

            // The first pass out is a slit's width from the vertex, the second as far from the point it ends on, the same way.
            double wx = a.X - v.X;
            double wy = a.Y - v.Y;
            double width = Math.Sqrt(wx * wx + wy * wy);

            if (Math.Abs(width - Slit) > Eps || Math.Abs(b.X - t.X - wx) > Eps || Math.Abs(b.Y - t.Y - wy) > Eps)
            {
                return false;
            }

            // The pass runs across the slit's width.
            double px = t.X - v.X;
            double py = t.Y - v.Y;

            if (Math.Abs(px * wx + py * wy) > Eps * Math.Max(1.0, Math.Sqrt(px * px + py * py)))
            {
                return false;
            }

            // Four corners of a box, each side along an axis, the box with some width and height.
            var corners = new GeoPoint2[4];
            double minX = double.MaxValue,
                minY = double.MaxValue,
                maxX = double.MinValue,
                maxY = double.MinValue;

            for (int i = 0; i < 4; i++)
            {
                corners[i] = ring[(k + 2 + i) % n];
                minX = Math.Min(minX, corners[i].X);
                maxX = Math.Max(maxX, corners[i].X);
                minY = Math.Min(minY, corners[i].Y);
                maxY = Math.Max(maxY, corners[i].Y);
            }

            if (maxX - minX <= Eps || maxY - minY <= Eps)
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                GeoPoint2 from = corners[i];
                GeoPoint2 to = corners[(i + 1) % 4];
                bool atX = Math.Abs(from.X - minX) <= Eps || Math.Abs(from.X - maxX) <= Eps;
                bool atY = Math.Abs(from.Y - minY) <= Eps || Math.Abs(from.Y - maxY) <= Eps;
                bool alongX = Math.Abs(from.Y - to.Y) <= Eps && Math.Abs(from.X - to.X) > Eps;
                bool alongY = Math.Abs(from.X - to.X) <= Eps && Math.Abs(from.Y - to.Y) > Eps;

                if (!atX || !atY || alongX == alongY)
                {
                    return false;
                }
            }

            // The slit's passes end on one side of the box: the first pass on the side the first corner is on the way to, the
            // slit running square to that side.
            bool onVertical = Math.Abs(t.X - minX) <= Eps || Math.Abs(t.X - maxX) <= Eps;
            bool onHorizontal = Math.Abs(t.Y - minY) <= Eps || Math.Abs(t.Y - maxY) <= Eps;
            bool insideX = t.X >= minX - Eps && t.X <= maxX + Eps;
            bool insideY = t.Y >= minY - Eps && t.Y <= maxY + Eps;

            if (!insideX || !insideY || (!onVertical && !onHorizontal))
            {
                return false;
            }

            // Square to a vertical side the slit runs along x, its width along y; square to a horizontal one, the other way.
            bool alongXSlit = Math.Abs(py) <= Eps && Math.Abs(wx) <= Eps;
            bool alongYSlit = Math.Abs(px) <= Eps && Math.Abs(wy) <= Eps;
            return (onVertical && alongXSlit) || (onHorizontal && alongYSlit);
        }
    }
}
