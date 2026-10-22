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
        internal static GeoRectangle2 Rectangle(IEnumerable<GeoPoint2> points, Tolerance tolerance) => Rectangle(points, tolerance, null);

        /// <summary>
        /// Gets the rectangle of least area round points, the first found of any as small, and, where a list is given for them, every
        /// rectangle tried, one along each edge of their hull, that is as small to rounding, in the order tried.
        /// </summary>
        private static GeoRectangle2 Rectangle(IEnumerable<GeoPoint2> points, Tolerance tolerance, List<GeoRectangle2> tried)
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
            var areas = tried != null ? new double[directions.Count] : null;

            for (int i = 0; i < directions.Count; i++)
            {
                double area = Spans(corners, directions[i], out GeoVector2 along, out GeoVector2 across, out double minS, out double maxS, out double minT, out double maxT);

                if (areas != null)
                {
                    areas[i] = area;
                }

                if (area < least || double.IsPositiveInfinity(least))
                {
                    least = area;
                    best = Around(along, across, minS, maxS, minT, maxT);
                }
            }

            // Those as small as the least to rounding, which the box weighs by their sides; any larger loses to the least.
            for (int i = 0; areas != null && i < directions.Count; i++)
            {
                if (!(Math.Abs(areas[i] - least) > 1E-9 * Math.Max(Math.Abs(areas[i]), Math.Abs(least))))
                {
                    Spans(corners, directions[i], out GeoVector2 along, out GeoVector2 across, out double minS, out double maxS, out double minT, out double maxT);
                    tried.Add(Around(along, across, minS, maxS, minT, maxT));
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the area of the rectangle round some corners along a direction, and how far they reach along it and across it.
        /// </summary>
        private static double Spans(IReadOnlyList<GeoPoint2> corners, GeoVector2 direction, out GeoVector2 along, out GeoVector2 across, out double minS, out double maxS, out double minT, out double maxT)
        {
            double length = direction.Length;
            along = length > 0.0 ? direction.Multiply(1.0 / length) : new GeoVector2(1.0, 0.0);
            across = new GeoVector2(-along.Y, along.X);
            minS = double.PositiveInfinity;
            maxS = double.NegativeInfinity;
            minT = double.PositiveInfinity;
            maxT = double.NegativeInfinity;

            foreach (GeoPoint2 corner in corners)
            {
                double s = corner.X * along.X + corner.Y * along.Y;
                double t = corner.X * across.X + corner.Y * across.Y;

                minS = Math.Min(minS, s);
                maxS = Math.Max(maxS, s);
                minT = Math.Min(minT, t);
                maxT = Math.Max(maxT, t);
            }

            return (maxS - minS) * (maxT - minT);
        }

        /// <summary>
        /// Gets the rectangle a direction and the reach of some corners along it and across it make.
        /// </summary>
        private static GeoRectangle2 Around(GeoVector2 along, GeoVector2 across, double minS, double maxS, double minT, double maxT)
        {
            double s0 = (minS + maxS) * 0.5;
            double t0 = (minT + maxT) * 0.5;
            var centre = new GeoPoint2(along.X * s0 + across.X * t0, along.Y * s0 + across.Y * t0);

            return new GeoRectangle2(centre, maxS - minS, maxT - minT, Math.Atan2(along.Y, along.X));
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
            var tried = new List<GeoRectangle2>();

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

                // Every rectangle on the face, each along an edge of its hull, is a box to compare: two on one face as large tie
                // as two on two faces do.
                tried.Clear();
                Rectangle(flat, tolerance, tried);
                double depth = maxN - minN;

                foreach (GeoRectangle2 base_ in tried)
                {
                    double volume = base_.Width * base_.Height * depth;

                    if (best == null || Smaller(volume, base_.Width, base_.Height, depth, least, best))
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
            }

            return best;
        }

        /// <summary>
        /// Whether the box standing on a face of the hull is to be taken over the best so far, standing on the same face or another:
        /// smaller beyond rounding, or as large to rounding and thinner, its sides compared least first, then the middle and then the
        /// longest; the first found where every side ties.
        /// </summary>
        /// <remarks>
        /// Two boxes as large are as good a fit, and the thickness a clash reads from the box is its least side: a prism 40
        /// long on a right triangle with legs 18 and 24 fits a box 18 by 24 by 40 along its legs and one 14.4 by 30 by 40
        /// along its long side, both 17 280, and the one found first, 18 thick, was taken, though the prism fits in 14.4.
        /// Two rectangles on one face as large are compared so too, and a body fits the same box however it is turned: the
        /// tetrahedron on (0, 0, 0), (24, 0, 0), (0, 18, 0) and (0, 0, 40) fits 13.548779 by 27.340984 by 46.647615, where
        /// turned it was given 13.548779 by 30 by 42.513057, as large, the one of the two on its long face found first.
        /// </remarks>
        private static bool Smaller(double volume, double width, double height, double depth, double least, GeoObb3 best)
        {
            if (Math.Abs(volume - least) > 1E-9 * Math.Max(Math.Abs(volume), Math.Abs(least)))
            {
                return volume < least;
            }

            double[] sides = { width, height, depth };
            double[] bests = { best.SizeX, best.SizeY, best.SizeZ };
            Array.Sort(sides);
            Array.Sort(bests);

            for (int i = 0; i < 3; i++)
            {
                if (sides[i] != bests[i])
                {
                    return sides[i] < bests[i];
                }
            }

            return false;
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
