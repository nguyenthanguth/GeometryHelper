using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The smallest rectangle round points in the plane, and a small box round points in space.
    /// </summary>
    /// <remarks>
    /// A rectangle of least area round a convex polygon has a side along one of its edges, so trying every edge
    /// of the hull finds it exactly. In space the box is tried on every face of the hull, each face's rectangle
    /// found the same way: the smallest box of all for a block, a prism, or anything with a flat face to stand
    /// on, and close to it otherwise.
    /// </remarks>
    internal static class BoxFit
    {
        internal static GeoRectangle2 Rectangle(IEnumerable<GeoPoint2> points, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var all = new List<GeoPoint2>(points);

            if (all.Count == 0)
            {
                throw new ArgumentException("There are no points to fit round.", nameof(points));
            }

            IReadOnlyList<GeoPoint2> corners = ConvexHull2.TryOf(all, out GeoPolygon2 hull, tolerance) ? hull.Vertices : all;
            var directions = new List<GeoVector2>();

            if (hull != null)
            {
                for (int i = 0; i < corners.Count; i++)
                {
                    directions.Add(corners[i].GetVectorTo(corners[(i + 1) % corners.Count]));
                }
            }
            else
            {
                // All in one line, or all one point: the line through the two furthest apart.
                GeoPoint2 far = Farthest(all, all[0]);
                directions.Add(Farthest(all, far).GetVectorTo(far));
            }

            GeoRectangle2 best = default;
            double least = double.PositiveInfinity;

            foreach (GeoVector2 direction in directions)
            {
                double length = direction.Length;
                GeoVector2 along = length > 0.0 ? direction.Multiply(1.0 / length) : new GeoVector2(1.0, 0.0);
                var across = new GeoVector2(-along.Y, along.X);
                double minS = double.PositiveInfinity, maxS = double.NegativeInfinity;
                double minT = double.PositiveInfinity, maxT = double.NegativeInfinity;

                foreach (GeoPoint2 corner in corners)
                {
                    double s = corner.X * along.X + corner.Y * along.Y;
                    double t = corner.X * across.X + corner.Y * across.Y;

                    minS = Math.Min(minS, s);
                    maxS = Math.Max(maxS, s);
                    minT = Math.Min(minT, t);
                    maxT = Math.Max(maxT, t);
                }

                double area = (maxS - minS) * (maxT - minT);

                if (area < least || double.IsPositiveInfinity(least))
                {
                    least = area;
                    double s0 = (minS + maxS) * 0.5;
                    double t0 = (minT + maxT) * 0.5;
                    var centre = new GeoPoint2(along.X * s0 + across.X * t0, along.Y * s0 + across.Y * t0);

                    best = new GeoRectangle2(centre, maxS - minS, maxT - minT, Math.Atan2(along.Y, along.X));
                }
            }

            return best;
        }

        internal static GeoObb3 Box(IEnumerable<GeoPoint3> points, Tolerance tolerance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var all = new List<GeoPoint3>(points);

            if (all.Count == 0)
            {
                throw new ArgumentException("There are no points to fit round.", nameof(points));
            }

            var corners = new List<GeoPoint3>();
            var normals = new List<GeoVector3>();

            if (ConvexHull3.TryOf(all, out GeoSolid3 hull, tolerance))
            {
                foreach (GeoFace3 face in hull.Faces)
                {
                    normals.Add(face.Boundary.Normal);
                    corners.AddRange(face.Boundary.Vertices);
                }
            }
            else
            {
                corners = all;
                normals.Add(FlatNormal(all, tolerance));
            }

            GeoObb3 best = null;
            double least = double.PositiveInfinity;

            foreach (GeoVector3 normal in normals)
            {
                GetBasis(normal, out GeoVector3 u, out GeoVector3 v);

                var flat = new List<GeoPoint2>(corners.Count);
                double minN = double.PositiveInfinity, maxN = double.NegativeInfinity;

                foreach (GeoPoint3 corner in corners)
                {
                    var p = new GeoVector3(corner.X, corner.Y, corner.Z);
                    flat.Add(new GeoPoint2(p.DotProduct(u), p.DotProduct(v)));
                    double n = p.DotProduct(normal);
                    minN = Math.Min(minN, n);
                    maxN = Math.Max(maxN, n);
                }

                GeoRectangle2 base_ = Rectangle(flat, tolerance);
                double depth = maxN - minN;
                double volume = base_.Width * base_.Height * depth;

                if (best == null || volume < least)
                {
                    least = volume;
                    double cos = Math.Cos(base_.AngleRad);
                    double sin = Math.Sin(base_.AngleRad);
                    GeoVector3 axisX = u.Multiply(cos).Add(v.Multiply(sin));
                    GeoVector3 axisY = u.Multiply(-sin).Add(v.Multiply(cos));
                    GeoVector3 centre = u.Multiply(base_.Center.X).Add(v.Multiply(base_.Center.Y)).Add(normal.Multiply((minN + maxN) * 0.5));

                    best = new GeoObb3(new GeoPoint3(centre.X, centre.Y, centre.Z), base_.Width, base_.Height, depth, axisX, axisY);
                }
            }

            return best;
        }

        /// <summary>
        /// A normal to the plane points lie in when they span no volume; any normal to their line, or to nothing,
        /// when they span no area either.
        /// </summary>
        private static GeoVector3 FlatNormal(List<GeoPoint3> all, Tolerance tolerance)
        {
            GeoPoint3 a = Farthest(all, all[0]);
            GeoPoint3 b = Farthest(all, a);
            GeoVector3 along = a.GetVectorTo(b);

            if (!along.TryGetNormal(out GeoVector3 line, tolerance))
            {
                return GeoVector3.ZAxis;
            }

            GeoPoint3 c = all[0];
            double reach = -1.0;

            foreach (GeoPoint3 point in all)
            {
                double off = line.CrossProduct(a.GetVectorTo(point)).Length;

                if (off > reach)
                {
                    reach = off;
                    c = point;
                }
            }

            if (line.CrossProduct(a.GetVectorTo(c)).TryGetNormal(out GeoVector3 normal, tolerance))
            {
                return normal;
            }

            GetBasis(line, out GeoVector3 across, out _);
            return across;
        }

        private static GeoPoint2 Farthest(List<GeoPoint2> points, GeoPoint2 from)
        {
            GeoPoint2 best = from;
            double most = -1.0;

            foreach (GeoPoint2 point in points)
            {
                double d = from.DistanceTo(point);

                if (d > most)
                {
                    most = d;
                    best = point;
                }
            }

            return best;
        }

        private static GeoPoint3 Farthest(List<GeoPoint3> points, GeoPoint3 from)
        {
            GeoPoint3 best = from;
            double most = -1.0;

            foreach (GeoPoint3 point in points)
            {
                double d = from.DistanceTo(point);

                if (d > most)
                {
                    most = d;
                    best = point;
                }
            }

            return best;
        }

        private static void GetBasis(GeoVector3 axis, out GeoVector3 b1, out GeoVector3 b2)
        {
            GeoVector3 helper = Math.Abs(axis.X) < 0.9 ? GeoVector3.XAxis : GeoVector3.YAxis;

            b1 = helper.Subtract(axis.Multiply(helper.DotProduct(axis))).Normalize();
            b2 = axis.CrossProduct(b1);
        }
    }
}
