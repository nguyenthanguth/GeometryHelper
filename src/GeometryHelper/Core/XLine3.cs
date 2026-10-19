using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// How an infinite line in space is cut into a segment to be asked of a shape with an end to it, and where a point, a
    /// plane, a ray or another line meets it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cut runs from the point at N - m to the point at F + m along the line, where N and F are the least and the
    /// greatest distance along it at which any point of the shape projects, and m = max(1, 100 · EqualPoint) is the margin.
    /// For a straight shape N and F are taken over its corners, for a body over the corners of its box; for a circle or an
    /// ellipse they are the centre brought onto the line less and plus the radius, or the major radius; for an arc they are
    /// its two ends and, where the arc holds them, the points of its circle furthest ahead and furthest behind along the line.
    /// The origin of the line is not part of the cut: a shape a million along it is asked as the stretch round the shape.
    /// </para>
    /// <para>
    /// Every point of the shape projects between N and F, so every point of the line outside the cut is further from every
    /// point of the shape than the nearer end of the cut is. This is the reasoning of <see cref="XLine2"/> in space, and its
    /// faults are the segment's: where the segment code answers rightly, the ends of the cut are never a crossing, a touching
    /// point or an end of a shortest segment, and the cut answers what the line would; where it does not, the line shares the
    /// fault. A NaN anywhere is carried into the cut, and a line of no direction, a <c>default</c> one, is cut to the single
    /// point of its origin.
    /// </para>
    /// <para>
    /// A plane has no end to cut at, and two lines or a line and a ray are answered in closed form: two lines in space come
    /// nearest where their common perpendicular stands, found from the closed form with its cross products taken to a rounding of their own, and are parallel
    /// by rounding on the same rule as in the plane (see <see cref="Placeable"/>).
    /// </para>
    /// </remarks>
    internal static class XLine3
    {
        /// <summary>
        /// The sine below which two directions of unit length are parallel by rounding alone: a crossing a micrometre off
        /// would lie a million kilometres along.
        /// </summary>
        private const double RoundingSine = 1E-15;

        /// <summary>
        /// The relative rounding of a double: the gap between one and the next number above it.
        /// </summary>
        private const double Epsilon = 2.2204460492503131E-16;

        /// <summary>
        /// The sine up to which two lines may be parallel by rounding alone; above it they are at a real angle and meet.
        /// </summary>
        private const double NearlyParallel = 1E-9;

        /// <summary>
        /// The sine below which a line starting within the planar tolerance of a plane reads as level with it: rounding, and no more.
        /// </summary>
        private const double LevelToRounding = 1E-12;

        /// <summary>
        /// Cuts a line from before the nearest to past the farthest distance along it.
        /// </summary>
        /// <param name="xline">The line.</param>
        /// <param name="nearest">A lower bound of how far along the line any point of the other shape projects.</param>
        /// <param name="farthest">An upper bound of how far along the line any point of the other shape projects.</param>
        /// <param name="tolerance">The tolerance whose point tolerance sets the margin outside the bounds.</param>
        /// <returns>The segment from the point at the nearest less the margin to the point at the farthest plus it.</returns>
        internal static GeoLine3 CutPast(GeoXLine3 xline, double nearest, double farthest, Tolerance tolerance)
        {
            double margin = Math.Max(1.0, 100.0 * tolerance.EqualPoint);

            return new GeoLine3(xline.GetPointAtDistance(nearest - margin), xline.GetPointAtDistance(farthest + margin));
        }

        /// <summary>
        /// Cuts a line past a segment.
        /// </summary>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoLine3 line, Tolerance tolerance)
        {
            double first = xline.GetDistanceAtPoint(line.StartPoint);
            double second = xline.GetDistanceAtPoint(line.EndPoint);

            return CutPast(xline, Math.Min(first, second), Math.Max(first, second), tolerance);
        }

        /// <summary>
        /// Cuts a line past an arc: its two ends, and the extreme of the circle along the line where the arc holds it.
        /// </summary>
        /// <remarks>
        /// Not the whole of its circle. An arc that is nearly straight lies on a circle of a radius of 1E11, and a cut that
        /// reached past that circle would be 1E11 long, with no digit left to tell the shape from its neighbours.
        /// </remarks>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoArc3 arc, Tolerance tolerance)
        {
            double first = xline.GetDistanceAtPoint(arc.StartPoint);
            double second = xline.GetDistanceAtPoint(arc.EndPoint);
            double nearest = Math.Min(first, second);
            double farthest = Math.Max(first, second);

            ArcReach(xline, arc, ref nearest, ref farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a circle.
        /// </summary>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoCircle3 circle, Tolerance tolerance)
            => CutPastRound(xline, circle.Center, circle.Radius, tolerance);

        /// <summary>
        /// Cuts a line past an ellipse, read by the sphere of its major radius.
        /// </summary>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoEllipse3 ellipse, Tolerance tolerance)
            => CutPastRound(xline, ellipse.Center, ellipse.MajorRadius, tolerance);

        /// <summary>
        /// Cuts a line past an axis-aligned box.
        /// </summary>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return CutPast(xline, 0.0, 0.0, tolerance);
            }

            Extent(xline, box.GetCorners(), out double nearest, out double farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past an oriented box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoObb3 box, Tolerance tolerance)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));

            Extent(xline, box.GetCorners(), out double nearest, out double farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a body: the corners of its box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));

            return CutPast(xline, solid.GetAabb(), tolerance);
        }

        /// <summary>
        /// Cuts a line past a triangle.
        /// </summary>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoTriangle3 triangle, Tolerance tolerance)
        {
            double a = xline.GetDistanceAtPoint(triangle.A);
            double b = xline.GetDistanceAtPoint(triangle.B);
            double c = xline.GetDistanceAtPoint(triangle.C);

            return CutPast(xline, Math.Min(Math.Min(a, b), c), Math.Max(Math.Max(a, b), c), tolerance);
        }

        /// <summary>
        /// Cuts a line past a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            Extent(xline, polygon.Vertices, out double nearest, out double farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoPolyline3 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            Extent(xline, polyline.Vertices, out double nearest, out double farthest);

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a face: its outline, and its holes too, though a hole lies within the outline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            Extent(xline, face.Boundary.Vertices, out double nearest, out double farthest);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                Extent(xline, hole.Vertices, out double holeNearest, out double holeFarthest);
                nearest = Math.Min(nearest, holeNearest);
                farthest = Math.Max(farthest, holeFarthest);
            }

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a curved loop: its corners, and the extreme along the line of every arc in it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoPolygonArc3 loop, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            Extent(xline, loop.Vertices, out double nearest, out double farthest);

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                GeoEdge3 edge = loop.GetEdgeAt(i);

                if (edge.IsArc)
                {
                    GeoArc3 arc = edge.ToArc();
                    ArcReach(xline, arc, ref nearest, ref farthest);
                }
            }

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Cuts a line past a curved chain: its corners, and the extreme along the line of every arc in it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        internal static GeoLine3 CutPast(GeoXLine3 xline, GeoPolylineArc3 chain, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            Extent(xline, chain.Vertices, out double nearest, out double farthest);

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                GeoEdge3 edge = chain.GetEdgeAt(i);

                if (edge.IsArc)
                {
                    GeoArc3 arc = edge.ToArc();
                    ArcReach(xline, arc, ref nearest, ref farthest);
                }
            }

            return CutPast(xline, nearest, farthest, tolerance);
        }

        /// <summary>
        /// Whether one line comes before another, by origin and then by direction: the order that lets a question about two
        /// lines be worked out once, from the one that comes first, whichever way round it is asked.
        /// </summary>
        internal static bool Precedes(GeoXLine3 first, GeoXLine3 second)
        {
            if (first.Origin.X != second.Origin.X) return first.Origin.X < second.Origin.X;
            if (first.Origin.Y != second.Origin.Y) return first.Origin.Y < second.Origin.Y;
            if (first.Origin.Z != second.Origin.Z) return first.Origin.Z < second.Origin.Z;
            if (first.Direction.X != second.Direction.X) return first.Direction.X < second.Direction.X;
            if (first.Direction.Y != second.Direction.Y) return first.Direction.Y < second.Direction.Y;

            return first.Direction.Z < second.Direction.Z;
        }

        /// <summary>
        /// Gets the point of a line nearest a point: its foot, the projection on the line.
        /// </summary>
        internal static GeoPoint3 Project(GeoXLine3 xline, GeoPoint3 point)
            => xline.GetPointAtDistance(xline.GetDistanceAtPoint(point));

        /// <summary>
        /// Finds where two lines in space come nearest, at any angle, and how far along each.
        /// </summary>
        /// <param name="xline1">The first line.</param>
        /// <param name="xline2">The second line.</param>
        /// <param name="tolerance">The tolerance whose point tolerance decides what a double can place.</param>
        /// <param name="alongFirst">How far along the first line the common perpendicular stands.</param>
        /// <param name="alongSecond">How far along the second line the common perpendicular stands.</param>
        /// <param name="gap">The length of the common perpendicular: how far apart the two lines are.</param>
        /// <remarks>
        /// Two lines in space meet only where they happen to, and are otherwise skew: as far apart as their common
        /// perpendicular is long. They have one unless they are parallel by rounding: a sine below 1E-15, or a sine of 1E-9 or
        /// less with the perpendicular further out than a double can place within the point tolerance (see
        /// <see cref="Placeable"/>); a NaN has none either. It is worked out from the one that comes first (see
        /// <see cref="Precedes"/>), so that asked either way round the gap is the same to the bit.
        /// </remarks>
        internal static bool TryBridge(GeoXLine3 xline1, GeoXLine3 xline2, Tolerance tolerance, out double alongFirst, out double alongSecond, out double gap)
        {
            if (Precedes(xline2, xline1))
            {
                bool bridged = Nearest(xline2.Origin, xline2.Direction, xline1.Origin, xline1.Direction, tolerance, out alongSecond, out alongFirst, out gap);

                return bridged;
            }

            return Nearest(xline1.Origin, xline1.Direction, xline2.Origin, xline2.Direction, tolerance, out alongFirst, out alongSecond, out gap);
        }

        /// <summary>
        /// Finds where a line and a ray come nearest, at any angle, and how far along each: the common perpendicular of the two
        /// lines, wherever the ray reaches it or not.
        /// </summary>
        internal static bool TryBridge(GeoXLine3 xline, GeoRay3 ray, Tolerance tolerance, out double alongLine, out double alongRay, out double gap)
            => Nearest(xline.Origin, xline.Direction, ray.Origin, ray.Direction, tolerance, out alongLine, out alongRay, out gap);

        /// <summary>
        /// Works out the common perpendicular of two lines given by a point and a direction.
        /// </summary>
        private static bool Nearest(GeoPoint3 origin1, GeoVector3 direction1, GeoPoint3 origin2, GeoVector3 direction2, Tolerance tolerance, out double along1, out double along2, out double gap)
        {
            along1 = 0.0;
            along2 = 0.0;
            gap = 0.0;

            double d1x = direction1.X, d1y = direction1.Y, d1z = direction1.Z;
            double d2x = direction2.X, d2y = direction2.Y, d2z = direction2.Z;
            double rx = origin1.X - origin2.X, ry = origin1.Y - origin2.Y, rz = origin1.Z - origin2.Z;

            // The cross product of the directions: both of unit length, its length is the sine of the angle between them. Each
            // component is the difference of two products worked out to a rounding of its own, so that two directions a millionth
            // of a radian apart still give their cross product to a part in 1E15, where the plain difference of two products near
            // one would give it to a part in 1E9.
            double nx = DifferenceOfProducts(d1y, d2z, d1z, d2y);
            double ny = DifferenceOfProducts(d1z, d2x, d1x, d2z);
            double nz = DifferenceOfProducts(d1x, d2y, d1y, d2x);
            double denominator = nx * nx + ny * ny + nz * nz;
            double sine = Math.Sqrt(denominator);

            // A sine this small is rounding, not an angle. Written as what has to hold, so that a NaN fails it.
            if (!(sine > RoundingSine))
            {
                return false;
            }

            // The feet of the common perpendicular, from the standard closed form, s = ((o2 - o1) x d2) . n / |n|^2 along the first
            // line and t = ((o2 - o1) x d1) . n / |n|^2 along the second, with each cross product and the sum of products taken to
            // a rounding of its own. The two are then as good as the offset and the sine allow (a rounding of the offset over the
            // sine), however small the sine, where moving the pair by the residual again and again lost every digit of them below
            // a sine of 1E-12 and left a segment between them a hair longer than the gap, or a crossing with a length.
            double wx = -rx, wy = -ry, wz = -rz;
            var normal = new GeoVector3(nx, ny, nz);
            double s = DotProduct(new GeoVector3(DifferenceOfProducts(wy, d2z, wz, d2y), DifferenceOfProducts(wz, d2x, wx, d2z), DifferenceOfProducts(wx, d2y, wy, d2x)), normal) / denominator;
            double t = DotProduct(new GeoVector3(DifferenceOfProducts(wy, d1z, wz, d1y), DifferenceOfProducts(wz, d1x, wx, d1z), DifferenceOfProducts(wx, d1y, wy, d1x)), normal) / denominator;

            // An origin that is not a number places no meeting, and neither does one that cannot be placed, measured along
            // the line that is further from it, so that asked either way round the answer is the same. Written as what has
            // to hold, so that a NaN fails it.
            if (!Guard.IsFinite(s) || !Guard.IsFinite(t) || !Placeable(sine, Math.Max(Math.Abs(s), Math.Abs(t)), origin1, origin2, tolerance))
            {
                return false;
            }

            along1 = s;
            along2 = t;

            // The length of the common perpendicular is the offset of the origins along the unit normal of the two. Taken from the
            // residual of the pair found it would be the difference of terms as large as the distances along (1E7 from a sine of
            // 1E-6 and a metre of offset), and carry their rounding.
            gap = Math.Abs(rx * nx + ry * ny + rz * nz) / sine;
            return true;
        }

        /// <summary>
        /// Gets a * b - c * d to a rounding of its own, however near the two products are to each other.
        /// </summary>
        /// <remarks>
        /// Each product is split into the double nearest it and the rounding left over, which Dekker's method finds without a
        /// fused multiply and add, and the two roundings are carried into the difference.
        /// </remarks>
        private static double DifferenceOfProducts(double a, double b, double c, double d)
        {
            TwoProduct(a, b, out double first, out double firstError);
            TwoProduct(c, d, out double second, out double secondError);

            return (first - second) + (firstError - secondError);
        }

        /// <summary>
        /// Gets the product of two numbers and the rounding it carries, exactly: the two add up to the product.
        /// </summary>
        private static void TwoProduct(double a, double b, out double product, out double error)
        {
            // Dekker's split, since netstandard2.0 has no fused multiply-add; it relies on double rounding (SSE2, x64), not x87 extended.
            product = a * b;
            Split(a, out double aHigh, out double aLow);
            Split(b, out double bHigh, out double bLow);
            error = ((aHigh * bHigh - product) + aHigh * bLow + aLow * bHigh) + aLow * bLow;
        }

        /// <summary>
        /// Splits a number into its high 26 bits and the rest, so that their products with another split number are exact.
        /// </summary>
        private static void Split(double a, out double high, out double low)
        {
            double scaled = 134217729.0 * a;

            high = scaled - (scaled - a);
            low = a - high;
        }

        /// <summary>
        /// Gets a sum of three products to a rounding of its own, however much of it cancels.
        /// </summary>
        /// <remarks>
        /// The dot product of two unit vectors a millionth of a radian from square is a millionth, which the plain sum would give
        /// to a part in 1E10; this gives it to a part in 1E15.
        /// </remarks>
        private static double DotProduct(GeoVector3 a, GeoVector3 b)
        {
            TwoProduct(a.X, b.X, out double sum, out double compensation);
            TwoProduct(a.Y, b.Y, out double product, out double error);
            TwoSum(sum, product, out sum, out double carry);
            compensation += carry + error;
            TwoProduct(a.Z, b.Z, out product, out error);
            TwoSum(sum, product, out sum, out carry);
            compensation += carry + error;

            return sum + compensation;
        }

        /// <summary>
        /// Gets the sum of two numbers and the rounding it carries, exactly: the two add up to the sum.
        /// </summary>
        private static void TwoSum(double a, double b, out double sum, out double error)
        {
            sum = a + b;

            double bVirtual = sum - a;

            error = (a - (sum - bVirtual)) + (b - bVirtual);
        }

        /// <summary>
        /// Finds where a line crosses a plane, at any angle, and how far along the line.
        /// </summary>
        /// <param name="xline">The line.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance whose point tolerance decides what a double can place.</param>
        /// <param name="along">How far along the line the plane stands.</param>
        /// <remarks>
        /// A line crosses a plane unless it is parallel to it by rounding: a sine below 1E-15, or a sine of 1E-9 or less with the
        /// crossing further out than a double can place within the point tolerance (see <see cref="Placeable"/>). A line that
        /// starts within the planar tolerance of the plane and is level with it to a sine of 1E-12 lies in it and crosses it
        /// nowhere in particular: it is within the planar tolerance of the plane, and names no place.
        /// </remarks>
        internal static bool TryCrossPlane(GeoXLine3 xline, GeoPlane3 plane, Tolerance tolerance, out double along)
        {
            along = 0.0;

            // The normal and the direction are of unit length, so the dot product is the sine of the angle between the line and
            // the plane.
            double climb = DotProduct(xline.Direction, plane.Normal);
            double rise = DotProduct(xline.Origin.GetVectorTo(plane.Origin), plane.Normal);

            // Written as what has to hold, so that a NaN fails it.
            if (!(Math.Abs(climb) > RoundingSine))
            {
                return false;
            }

            if (Math.Abs(rise) <= tolerance.EqualPlanar && !(Math.Abs(climb) > LevelToRounding))
            {
                return false;
            }

            double distance = rise / climb;

            if (!Guard.IsFinite(distance) || !Placeable(climb, Math.Abs(distance), xline.Origin, plane.Origin, tolerance))
            {
                return false;
            }

            along = distance;
            return true;
        }

        /// <summary>
        /// Whether two lines or a line and a plane that close to parallel have a meeting that can be placed: one that lies no
        /// further along than a double can put a point within the point tolerance, a point at that distance from a coordinate
        /// of that size being only known to a rounding of the larger.
        /// </summary>
        /// <remarks>
        /// The rule is for lines parallel by rounding, a sine of 1E-9 or less, such as a line rebuilt from two points of
        /// another; lines at any real angle meet or pass, whatever the tolerance, even where it is 1E-12 or nought. With a
        /// point tolerance of a thousandth and coordinates near nought the limit is 4.5E12 along.
        /// </remarks>
        private static bool Placeable(double sine, double along, GeoPoint3 first, GeoPoint3 second, Tolerance tolerance)
        {
            if (Math.Abs(sine) > NearlyParallel)
            {
                return !double.IsNaN(along);
            }

            double reach = along + Math.Max(Largest(first), Largest(second));

            // Written as what has to hold, so that a NaN fails it.
            return reach * Epsilon <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Gets the largest coordinate of a point in size; a NaN is carried.
        /// </summary>
        private static double Largest(GeoPoint3 point)
            => Math.Max(Math.Max(Math.Abs(point.X), Math.Abs(point.Y)), Math.Abs(point.Z));

        /// <summary>
        /// Gets how far along a line the nearest and the farthest of a set of points project; a NaN among them is carried.
        /// </summary>
        private static void Extent(GeoXLine3 xline, IReadOnlyList<GeoPoint3> points, out double nearest, out double farthest)
        {
            nearest = double.PositiveInfinity;
            farthest = double.NegativeInfinity;

            if (points.Count == 0)
            {
                nearest = 0.0;
                farthest = 0.0;
                return;
            }

            for (int i = 0; i < points.Count; i++)
            {
                double along = xline.GetDistanceAtPoint(points[i]);

                nearest = Math.Min(nearest, along);
                farthest = Math.Max(farthest, along);
            }
        }

        /// <summary>
        /// Cuts a line past a round shape: its centre brought onto the line, less and plus its radius.
        /// </summary>
        private static GeoLine3 CutPastRound(GeoXLine3 xline, GeoPoint3 center, double radius, Tolerance tolerance)
        {
            double along = xline.GetDistanceAtPoint(center);

            return CutPast(xline, along - radius, along + radius, tolerance);
        }

        /// <summary>
        /// Widens a stretch along a line to take in what an arc reaches between its ends: the point of its circle furthest
        /// ahead along the line, and the one furthest behind, each where the arc holds it.
        /// </summary>
        /// <remarks>
        /// The ends are the caller's: a loop and a chain have them as corners, and an arc made of a corner and a bulge of 1E-10
        /// has a centre a long way off that gives them only to within 1E-5. The circle runs furthest ahead where its
        /// direction square to the normal points along the line, by the radius times the length of the part of the line direction
        /// that lies in the plane of the circle; a line along the normal gives the circle no reach.
        /// </remarks>
        private static void ArcReach(GeoXLine3 xline, GeoArc3 arc, ref double nearest, ref double farthest)
        {
            arc.GetPlane().GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            double along = xline.GetDistanceAtPoint(arc.Center);
            double alongU = xline.Direction.DotProduct(uAxis);
            double alongV = xline.Direction.DotProduct(vAxis);
            double heading = Math.Atan2(alongV, alongU);
            double reach = arc.Radius * Math.Sqrt(alongU * alongU + alongV * alongV);

            if (Holds(arc, heading))
            {
                farthest = Math.Max(farthest, along + reach);
            }

            if (Holds(arc, heading + Math.PI))
            {
                nearest = Math.Min(nearest, along - reach);
            }
        }

        /// <summary>
        /// Whether an arc holds the point of its circle at an angle, reached from its start the way it sweeps.
        /// </summary>
        private static bool Holds(GeoArc3 arc, double angle)
        {
            double turn = 2.0 * Math.PI;
            double reached = (angle - arc.StartAngle) * Math.Sign(arc.SweptAngle);

            reached -= turn * Math.Floor(reached / turn);

            return reached <= Math.Abs(arc.SweptAngle);
        }
    }
}
