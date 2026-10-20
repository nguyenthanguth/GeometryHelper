using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// What a ball in space is asked: how far its centre stands from a shape, where its surface is crossed by a line or a
    /// curve, and the circle two surfaces share.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ball is a point grown by its radius, so the gap between it and any shape is the gap between its centre and that shape
    /// less the radius, and nought where the shape reaches into it. The shape's own distance to a point does the measuring (a
    /// closed shape is read as filled, a curve as a curve, a disc as a disc), and the shortest segment leaves the surface of the
    /// ball for the point of the shape nearest its centre.
    /// </para>
    /// <para>
    /// A line, a ray or a segment crosses the surface where the quadratic of the distance from the centre has its roots. The
    /// foot of the centre on the line is found first and the roots stand either side of it by the half chord, the square root of
    /// (r - rho)(r + rho) where rho is the distance from the centre to the line, so that a line grazing the surface loses no
    /// digits to a difference of squares. A curve in a plane is cut by the circle the plane makes with the surface, and the
    /// crossing of two curves in one plane is the library's own.
    /// </para>
    /// </remarks>
    internal static partial class Sphere3
    {
        /// <summary>
        /// How two surfaces, or a surface and a plane, meet.
        /// </summary>
        internal enum Meeting
        {
            /// <summary>They share nothing within the point tolerance.</summary>
            None,

            /// <summary>They touch at one point, within the point tolerance.</summary>
            Point,

            /// <summary>They cross in a circle.</summary>
            Circle
        }

        /// <summary>
        /// Gets how far a point is from the centre of a ball.
        /// </summary>
        internal static double FromCenter(GeoSphere3 sphere, GeoPoint3 point) => Distance3.DistanceTo(sphere.Center, point);

        /// <summary>
        /// Gets the gap between a ball and a shape from the distance of the shape from the centre: nought where the shape reaches
        /// the ball.
        /// </summary>
        internal static double Reach(GeoSphere3 sphere, double distanceFromCenter) => Math.Max(0.0, distanceFromCenter - sphere.Radius);

        /// <summary>
        /// Gets the shortest segment from the surface of a ball to a shape, given the point of the shape nearest the centre.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="nearest">The point of the shape nearest the centre; the centre itself where the shape holds it.</param>
        /// <param name="reach">The gap between the ball and the shape, which is nought where they meet.</param>
        /// <returns>
        /// The segment from the point of the surface that faces <paramref name="nearest"/> to it, or a segment of no length at
        /// <paramref name="nearest"/> where the shape reaches into the ball.
        /// </returns>
        internal static GeoLine3 Join(GeoSphere3 sphere, GeoPoint3 nearest, double reach)
        {
            GeoVector3 toNearest = sphere.Center.GetVectorTo(nearest);
            double far = toNearest.Length;

            if (!(reach > 0.0) || !(far > sphere.Radius))
            {
                return new GeoLine3(nearest, nearest);
            }

            return new GeoLine3(sphere.Center.Add(toNearest.Multiply(sphere.Radius / far)), nearest);
        }

        /// <summary>
        /// Gets the shortest segment from the surface of one ball to the surface of another. It is worked out from the ball that comes
        /// first (see <see cref="Precedes"/>) and turned round for the other, so that asked either way round it is the same to the bit.
        /// </summary>
        internal static GeoLine3 Join(GeoSphere3 sphere, GeoSphere3 other)
        {
            return Precedes(other, sphere) ? JoinFromTheFirst(other, sphere).Reverse() : JoinFromTheFirst(sphere, other);
        }

        private static GeoLine3 JoinFromTheFirst(GeoSphere3 first, GeoSphere3 second)
        {
            GeoVector3 between = first.Center.GetVectorTo(second.Center);
            double far = between.Length;

            if (!(far > 0.0))
            {
                return new GeoLine3(first.Center, first.Center);
            }

            GeoVector3 toward = between.Multiply(1.0 / far);

            if (far > first.Radius + second.Radius)
            {
                return new GeoLine3(first.Center.Add(toward.Multiply(first.Radius)), second.Center.Subtract(toward.Multiply(second.Radius)));
            }

            // They meet: the stretch of the line between the centres that both hold, from the near end of the second to the far end of
            // the first or of the second, whichever is nearer, and the point halfway along it.
            double low = Math.Max(-first.Radius, far - second.Radius);
            double high = Math.Min(first.Radius, far + second.Radius);
            GeoPoint3 middle = first.Center.Add(toward.Multiply(0.5 * (low + high)));

            return new GeoLine3(middle, middle);
        }

        /// <summary>
        /// Says where a point is: inside the surface, on it within the point tolerance, or outside.
        /// </summary>
        internal static PointLocation Locate(GeoSphere3 sphere, GeoPoint3 point, Tolerance tolerance)
        {
            double off = FromCenter(sphere, point) - sphere.Radius;

            if (Math.Abs(off) <= tolerance.EqualPoint)
            {
                return PointLocation.OnSide;
            }

            return off < 0.0 ? PointLocation.Inside : PointLocation.OutSide;
        }

        /// <summary>
        /// Gets the distance of a point from the surface, negative inside, nought where <see cref="Locate"/> says it is on it.
        /// </summary>
        internal static double SignedDistanceTo(GeoSphere3 sphere, GeoPoint3 point, Tolerance tolerance)
        {
            double off = FromCenter(sphere, point) - sphere.Radius;

            return Math.Abs(off) <= tolerance.EqualPoint ? 0.0 : off;
        }

        /// <summary>
        /// Gets the point of the surface nearest a point: along the line from the centre through it, and straight up the first axis
        /// from the centre for the centre itself, every direction being as near.
        /// </summary>
        internal static GeoPoint3 ClosestOnSurface(GeoSphere3 sphere, GeoPoint3 point)
        {
            GeoVector3 outward = sphere.Center.GetVectorTo(point);
            double far = outward.Length;

            if (!(far > 0.0))
            {
                return sphere.Center.Add(new GeoVector3(sphere.Radius, 0.0, 0.0));
            }

            return sphere.Center.Add(outward.Multiply(sphere.Radius / far));
        }

        /// <summary>
        /// Gets whether one ball comes before another, by centre and then by radius: the order that lets a question about two balls
        /// be worked out once, from the one that comes first, whichever way round it is asked.
        /// </summary>
        internal static bool Precedes(GeoSphere3 first, GeoSphere3 second)
        {
            if (first.Center.X != second.Center.X) return first.Center.X < second.Center.X;
            if (first.Center.Y != second.Center.Y) return first.Center.Y < second.Center.Y;
            if (first.Center.Z != second.Center.Z) return first.Center.Z < second.Center.Z;

            return first.Radius < second.Radius;
        }

        /// <summary>
        /// Finds how a plane meets the surface of a ball.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance whose point tolerance says how near to tangent is touching.</param>
        /// <param name="circle">The circle the plane cuts, with the normal of the plane, when they cross.</param>
        /// <param name="touch">The foot of the centre on the plane, when they touch at a point.</param>
        internal static Meeting CutByPlane(GeoSphere3 sphere, GeoPlane3 plane, Tolerance tolerance, out GeoCircle3 circle, out GeoPoint3 touch)
        {
            circle = default(GeoCircle3);
            touch = default(GeoPoint3);

            double height = plane.SignedDistanceTo(sphere.Center);
            double depth = sphere.Radius - Math.Abs(height);

            if (!(depth >= -tolerance.EqualPoint))
            {
                return Meeting.None;
            }

            GeoPoint3 foot = sphere.Center.Subtract(plane.Normal.Multiply(height));

            if (depth > tolerance.EqualPoint)
            {
                double radius = Math.Sqrt((sphere.Radius - Math.Abs(height)) * (sphere.Radius + Math.Abs(height)));

                if (radius > 0.0)
                {
                    circle = new GeoCircle3(foot, plane.Normal, radius);
                    return Meeting.Circle;
                }
            }

            touch = foot;
            return Meeting.Point;
        }

        /// <summary>
        /// Finds how the surfaces of two balls meet. The answer is worked out from the ball that comes first (see
        /// <see cref="Precedes"/>), so that asked either way round it is the same to the bit, but for the normal of the circle,
        /// which points from the ball asked towards the other.
        /// </summary>
        internal static Meeting CutBySphere(GeoSphere3 sphere, GeoSphere3 other, Tolerance tolerance, out GeoCircle3 circle, out GeoPoint3 touch)
        {
            return Precedes(other, sphere)
                ? CutFromTheFirst(other, sphere, true, tolerance, out circle, out touch)
                : CutFromTheFirst(sphere, other, false, tolerance, out circle, out touch);
        }

        private static Meeting CutFromTheFirst(GeoSphere3 first, GeoSphere3 second, bool turned, Tolerance tolerance, out GeoCircle3 circle, out GeoPoint3 touch)
        {
            circle = default(GeoCircle3);
            touch = default(GeoPoint3);

            GeoVector3 between = first.Center.GetVectorTo(second.Center);
            double far = between.Length;
            double tol = tolerance.EqualPoint;
            double rim = first.Radius + second.Radius;
            double gap = Math.Abs(first.Radius - second.Radius);

            // Balls on one centre share the whole surface or none of it: no circle, no point.
            if (!(far > tol) || !(far <= rim + tol) || !(far >= gap - tol))
            {
                return Meeting.None;
            }

            GeoVector3 toward = between.Multiply(1.0 / far);

            // How far from the first centre the plane of the two surfaces stands: (d^2 + r1^2 - r2^2) / 2d, as d / 2 plus the
            // difference of the squares over twice d.
            double along = 0.5 * (far + (first.Radius - second.Radius) * (first.Radius + second.Radius) / far);
            GeoPoint3 middle = first.Center.Add(toward.Multiply(along));
            double radius = Math.Sqrt(Math.Max(0.0, (first.Radius - along) * (first.Radius + along)));

            bool outside = Math.Abs(far - rim) <= tol;

            if (outside || Math.Abs(far - gap) <= tol)
            {
                // Touching along the line of centres: the middle of the two surface points there, so that the point is within the
                // tolerance of both surfaces. Where the radical plane stands is no guide, for balls nearly the same size inside one
                // another it is far from both.
                double onFirst = outside || first.Radius >= second.Radius ? first.Radius : -first.Radius;
                double onSecond = outside || first.Radius < second.Radius ? far - second.Radius : far + second.Radius;

                touch = first.Center.Add(toward.Multiply(0.5 * (onFirst + onSecond)));
                return Meeting.Point;
            }

            if (!(radius > tol))
            {
                touch = middle;
                return Meeting.Point;
            }

            // The normal of a circle is normalized as it is made, and a vector and its negative normalize to each other's negative to the
            // bit, so the circle asked from the second ball comes out the first's with its normal turned round exactly.
            circle = new GeoCircle3(middle, turned ? toward.Negate() : toward, radius);
            return Meeting.Circle;
        }

        /// <summary>
        /// Appends the points where a line, a ray or a segment crosses the surface, in order along it.
        /// </summary>
        /// <param name="found">The list the points are added to, each left out where it falls within the point tolerance of the last.</param>
        /// <param name="sphere">The ball.</param>
        /// <param name="origin">A point of the line.</param>
        /// <param name="direction">The direction of the line, of unit length.</param>
        /// <param name="from">How far along the line it starts, minus infinity for a line that has no start.</param>
        /// <param name="to">How far along it ends, plus infinity for a line that has no end.</param>
        /// <param name="tolerance">The tolerance whose point tolerance decides what is a grazing touch and what is on the surface.</param>
        /// <remarks>
        /// The line touches where it passes within the point tolerance outside the surface, at its foot, and where its two
        /// crossings are no further apart than the point tolerance, at the middle of them. A crossing a hair beyond an end is moved
        /// onto it, and an end within the point tolerance of the surface is a crossing of its own where no root lies near it, so a
        /// segment that stops a thousandth short of the surface touches it, as it collides with it.
        /// </remarks>
        internal static void Cross(List<GeoPoint3> found, GeoSphere3 sphere, GeoPoint3 origin, GeoVector3 direction, double from, double to, Tolerance tolerance)
        {
            double tol = tolerance.EqualPoint;
            GeoVector3 offset = sphere.Center.GetVectorTo(origin);
            double foot = -offset.DotProduct(direction);
            double rho = offset.Add(direction.Multiply(foot)).Length;
            var local = new List<GeoPoint3>(4);

            double first = double.NaN;
            double second = double.NaN;

            if (rho <= sphere.Radius + tol)
            {
                double half = Math.Sqrt(Math.Max(0.0, (sphere.Radius - rho) * (sphere.Radius + rho)));

                if (rho > sphere.Radius || 2.0 * half <= tol)
                {
                    first = foot;
                }
                else
                {
                    first = foot - half;
                    second = foot + half;
                }
            }

            bool startsAt = !double.IsInfinity(from) && Math.Abs(FromCenter(sphere, At(origin, direction, from)) - sphere.Radius) <= tol;
            bool endsAt = !double.IsInfinity(to) && Math.Abs(FromCenter(sphere, At(origin, direction, to)) - sphere.Radius) <= tol;

            if (startsAt)
            {
                AddDistinct(local, At(origin, direction, from), tol);
            }

            foreach (double root in new[] { first, second })
            {
                if (root >= from - tol && root <= to + tol)
                {
                    AddDistinct(local, At(origin, direction, Math.Max(from, Math.Min(to, root))), tol);
                }
            }

            if (endsAt)
            {
                AddDistinct(local, At(origin, direction, to), tol);
            }

            foreach (GeoPoint3 point in local)
            {
                AddDistinct(found, point, tol);
            }
        }

        /// <summary>
        /// Appends the points where a segment crosses the surface; a segment no longer than the point tolerance is its start.
        /// </summary>
        internal static void Cross(List<GeoPoint3> found, GeoSphere3 sphere, GeoLine3 line, Tolerance tolerance)
        {
            double length = line.Length;

            if (length > tolerance.EqualPoint)
            {
                Cross(found, sphere, line.StartPoint, line.StartPoint.GetVectorTo(line.EndPoint).Multiply(1.0 / length), 0.0, length, tolerance);
            }
            else if (Math.Abs(FromCenter(sphere, line.StartPoint) - sphere.Radius) <= tolerance.EqualPoint)
            {
                AddDistinct(found, line.StartPoint, tolerance.EqualPoint);
            }
        }

        /// <summary>
        /// Gets the points where a chain of segments crosses the surface, in order along it, a corner on the surface once.
        /// </summary>
        internal static GeoPoint3[] Cross(GeoSphere3 sphere, IReadOnlyList<GeoPoint3> vertices, bool closed, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();
            int edges = closed ? vertices.Count : vertices.Count - 1;

            for (int i = 0; i < edges; i++)
            {
                Cross(found, sphere, new GeoLine3(vertices[i], vertices[(i + 1) % vertices.Count]), tolerance);
            }

            return Finish(found, closed, tolerance);
        }

        /// <summary>
        /// Closes the list of crossings of a loop: its last point is its first again where the loop crosses at its start.
        /// </summary>
        internal static GeoPoint3[] Finish(List<GeoPoint3> found, bool closed, Tolerance tolerance)
        {
            if (closed && found.Count > 1 && Distance3.DistanceTo(found[0], found[found.Count - 1]) <= tolerance.EqualPoint)
            {
                found.RemoveAt(found.Count - 1);
            }

            return found.ToArray();
        }

        /// <summary>
        /// Appends a point unless it lies within the point tolerance of the last one in the list.
        /// </summary>
        internal static void AddDistinct(List<GeoPoint3> points, GeoPoint3 point, double tolerance)
        {
            if (points.Count == 0 || Distance3.DistanceTo(points[points.Count - 1], point) > tolerance)
            {
                points.Add(point);
            }
        }

        private static GeoPoint3 At(GeoPoint3 origin, GeoVector3 direction, double distance) => origin.Add(direction.Multiply(distance));
    }
}
