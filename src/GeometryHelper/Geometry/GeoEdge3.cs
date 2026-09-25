using System;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// One piece of a chain in space: a straight segment, or an arc bulging in a plane of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The twin of <see cref="GeoEdge2"/>, with the one thing space needs that the plane does not. A bulge is
    /// the tangent of a quarter of the swept angle, the number AutoCAD keeps against a vertex, and in the
    /// plane it is enough on its own. In space it is not: a chord and a bulge are satisfied by an arc in any
    /// of the infinitely many planes through that chord, so the plane is carried as well, as
    /// <see cref="Normal"/>.
    /// </para>
    /// <para>
    /// A positive bulge sweeps counter-clockwise about <see cref="Normal"/>, which is what a positive bulge
    /// means in the plane too, and that bows the curve towards the chord crossed with the normal. So a chord
    /// running along X with a normal along Z bows towards minus Y, the same way
    /// <see cref="GeoArc2.FromBulge"/> puts it.
    /// </para>
    /// <para>
    /// A bulge of nought is a straight segment and the normal is then ignored, which is why
    /// <see cref="IsArc"/> is worth asking before <see cref="ToArc"/>.
    /// </para>
    /// </remarks>
    public readonly struct GeoEdge3 : IEquatable<GeoEdge3>
    {
        /// <summary>
        /// Gets the point the edge starts from.
        /// </summary>
        public GeoPoint3 StartPoint { get; }

        /// <summary>
        /// Gets the point the edge ends at.
        /// </summary>
        public GeoPoint3 EndPoint { get; }

        /// <summary>
        /// Gets the bulge: the tangent of a quarter of the swept angle, nought for a straight segment.
        /// </summary>
        public double Bulge { get; }

        /// <summary>
        /// Gets the normal of the plane the arc bulges in, of no length at all for a straight segment.
        /// </summary>
        public GeoVector3 Normal { get; }

        /// <summary>
        /// Initializes a straight edge between two points.
        /// </summary>
        public GeoEdge3(GeoPoint3 startPoint, GeoPoint3 endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
            Bulge = 0.0;
            Normal = new GeoVector3(0.0, 0.0, 0.0);
        }

        /// <summary>
        /// Initializes an edge between two points, bulging by a given amount in a given plane.
        /// </summary>
        /// <param name="startPoint">The point the edge starts from.</param>
        /// <param name="endPoint">The point the edge ends at.</param>
        /// <param name="bulge">The bulge; nought for a straight segment, in which case the normal is ignored.</param>
        /// <param name="normal">The normal of the plane the arc bulges in; it need not be of unit length.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the bulge is not a number.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a bulge that is not nought comes with a normal of no length, or with one the chord does
        /// not lie square to, since neither describes a plane the arc could bulge in.
        /// </exception>
        public GeoEdge3(GeoPoint3 startPoint, GeoPoint3 endPoint, double bulge, GeoVector3 normal)
            : this(startPoint, endPoint, bulge, normal, Tolerance.Global)
        {
        }

        /// <summary>
        /// Initializes an edge between two points, bulging by a given amount in a given plane, within a tolerance.
        /// </summary>
        /// <param name="startPoint">The point the edge starts from.</param>
        /// <param name="endPoint">The point the edge ends at.</param>
        /// <param name="bulge">The bulge; nought for a straight segment, in which case the normal is ignored.</param>
        /// <param name="normal">The normal of the plane the arc bulges in; it need not be of unit length.</param>
        /// <param name="tolerance">The tolerance, which decides how square to the chord the normal must be.</param>
        public GeoEdge3(GeoPoint3 startPoint, GeoPoint3 endPoint, double bulge, GeoVector3 normal, Tolerance tolerance)
        {
            if (double.IsNaN(bulge) || double.IsInfinity(bulge))
            {
                throw new ArgumentOutOfRangeException(nameof(bulge), "A bulge has to be a number.");
            }

            StartPoint = startPoint;
            EndPoint = endPoint;

            if (bulge == 0.0)
            {
                Bulge = 0.0;
                Normal = new GeoVector3(0.0, 0.0, 0.0);
                return;
            }

            if (!normal.TryGetNormal(out GeoVector3 unit, tolerance))
            {
                throw new ArgumentException("An arc needs a plane to bulge in, so its normal cannot be of no length.", nameof(normal));
            }

            GeoVector3 chord = startPoint.GetVectorTo(endPoint);

            if (!chord.TryGetNormal(out GeoVector3 along, tolerance))
            {
                throw new ArgumentException("Two points in the same place have no chord to bulge.", nameof(endPoint));
            }

            if (!unit.IsPerpendicularTo(along, tolerance))
            {
                throw new ArgumentException("The chord of an arc lies in its plane, so the normal has to be square to it.", nameof(normal));
            }

            Bulge = bulge;
            Normal = unit;
        }

        /// <summary>
        /// Initializes an edge from an arc in space.
        /// </summary>
        /// <param name="arc">The arc; a whole turn cannot be held as one edge, because its ends meet.</param>
        /// <exception cref="ArgumentException">Thrown when the arc closes on itself, leaving no chord.</exception>
        public GeoEdge3(GeoArc3 arc)
        {
            GeoVector3 chord = arc.StartPoint.GetVectorTo(arc.EndPoint);

            if (!chord.TryGetNormal(out _))
            {
                throw new ArgumentException("An arc sweeping a whole turn has no chord, so it is not one edge of a chain.", nameof(arc));
            }

            StartPoint = arc.StartPoint;
            EndPoint = arc.EndPoint;
            Bulge = Math.Tan(arc.SweptAngle * 0.25);
            Normal = arc.Normal.Normalize();
        }

        /// <summary>
        /// Gets a value indicating whether the edge curves.
        /// </summary>
        public bool IsArc => Bulge != 0.0;

        /// <summary>
        /// Gets the length of the edge, measured along the arc where it curves.
        /// </summary>
        public double Length => IsArc ? ToArc().Length : StartPoint.DistanceTo(EndPoint);

        /// <summary>
        /// Gets the straight segment between the two ends, whether or not the edge curves.
        /// </summary>
        public GeoLine3 GetChord() => new GeoLine3(StartPoint, EndPoint);

        /// <summary>
        /// Gets the edge as a straight segment.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the edge curves, so a segment would not be it.</exception>
        public GeoLine3 ToLine()
        {
            if (IsArc)
            {
                throw new InvalidOperationException("This edge curves, so a straight segment is not it. Ask GetChord for the line between its ends.");
            }

            return new GeoLine3(StartPoint, EndPoint);
        }

        /// <summary>
        /// Gets the edge as an arc in space.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the edge is straight, so there is no arc to give.</exception>
        /// <remarks>
        /// The bulge is read in the plane the normal names: the chord is laid along the X axis of a frame
        /// standing on the start point, the arc is built there exactly as it would be in the plane, and the
        /// three points that describe it are lifted back. So a curve in space and the same curve laid flat
        /// come out with the same radius and the same swept angle.
        /// </remarks>
        public GeoArc3 ToArc()
        {
            if (!IsArc)
            {
                throw new InvalidOperationException("This edge is a straight segment, so there is no arc to give.");
            }

            GeoCoordinateSystem3 frame = Frame();
            GeoArc2 flat = GeoArc2.FromBulge(new GeoPoint2(0.0, 0.0), new GeoPoint2(GetChord().Length, 0.0), Bulge);

            return GeoArc3.FromThreePoints(
                frame.ToGlobal(Lift(flat.StartPoint)),
                frame.ToGlobal(Lift(flat.MidPoint)),
                frame.ToGlobal(Lift(flat.EndPoint)));
        }

        /// <summary>
        /// Gets the point a given way along the edge, from nought at the start to one at the end.
        /// </summary>
        public GeoPoint3 GetPointAtParameter(double parameter)
        {
            if (IsArc)
            {
                return ToArc().GetPointAtParameter(parameter);
            }

            double held = parameter < 0.0 ? 0.0 : parameter > 1.0 ? 1.0 : parameter;

            return StartPoint.Add(StartPoint.GetVectorTo(EndPoint).Multiply(held));
        }

        /// <summary>
        /// Gets how far along the edge the point nearest another one sits, from nought to one.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets how far along the edge the point nearest another one sits, from nought to one, within a tolerance.
        /// </summary>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance)
        {
            if (IsArc)
            {
                return ToArc().GetParameterAtPoint(point, tolerance);
            }

            double length = StartPoint.DistanceTo(EndPoint);

            if (length <= tolerance.EqualPoint)
            {
                return 0.0;
            }

            GeoVector3 along = StartPoint.GetVectorTo(EndPoint);
            double reach = StartPoint.GetVectorTo(point).DotProduct(along) / (length * length);

            return reach < 0.0 ? 0.0 : reach > 1.0 ? 1.0 : reach;
        }

        /// <summary>
        /// Gets the point of the edge nearest another point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point of the edge nearest another point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance)
        {
            return IsArc
                ? ToArc().GetClosestPointOnBoundary(point, tolerance)
                : GetPointAtParameter(GetParameterAtPoint(point, tolerance));
        }

        /// <summary>
        /// Gets the distance from the edge to a point.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));

        /// <summary>
        /// Says where a point sits relative to the edge; a curve never answers Inside.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Says where a point sits relative to the edge, within a tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance)
            => DistanceTo(point, tolerance) <= tolerance.EqualPoint ? PointLocation.OnSide : PointLocation.OutSide;

        /// <summary>
        /// Determines whether a point lies on the edge.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on the edge, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Gets the smallest axis-aligned box holding the edge.
        /// </summary>
        /// <remarks>
        /// An arc is boxed by the arc and not by its chord, so a bulge sticking out past its ends is inside
        /// the box that comes back.
        /// </remarks>
        public GeoAabb3 GetAabb() => IsArc ? ToArc().GetAabb() : GeoAabb3.FromPoints(new[] { StartPoint, EndPoint });

        /// <summary>
        /// Gets the plane the arc bulges in.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the edge is straight, which lies in no one plane.</exception>
        public GeoPlane3 GetPlane()
        {
            if (!IsArc)
            {
                throw new InvalidOperationException("A straight segment lies in any number of planes, so it names none.");
            }

            return new GeoPlane3(StartPoint, Normal);
        }

        /// <summary>
        /// Gets the edge walked the other way, drawing the same curve.
        /// </summary>
        /// <remarks>
        /// The bulge turns over and the plane stays where it was, exactly as <see cref="GeoEdge2.Reverse"/>
        /// turns over a bulge in the plane. Turning the normal over as well would put the arc back on the
        /// side it started from, which draws the mirror of the curve rather than the curve.
        /// </remarks>
        public GeoEdge3 Reverse()
            => IsArc ? new GeoEdge3(EndPoint, StartPoint, -Bulge, Normal) : new GeoEdge3(EndPoint, StartPoint);

        /// <summary>
        /// Creates a copy of this edge.
        /// </summary>
        public GeoEdge3 Clone() => IsArc ? new GeoEdge3(StartPoint, EndPoint, Bulge, Normal) : new GeoEdge3(StartPoint, EndPoint);

        /// <summary>
        /// Moves the edge by a vector.
        /// </summary>
        public GeoEdge3 Translate(GeoVector3 vector)
            => IsArc
                ? new GeoEdge3(StartPoint.Add(vector), EndPoint.Add(vector), Bulge, Normal)
                : new GeoEdge3(StartPoint.Add(vector), EndPoint.Add(vector));

        /// <summary>
        /// Applies a transformation to the edge.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <returns>The edge in its new place.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <remarks>
        /// The normal is carried by the transformation as well, so an edge turned about any axis keeps
        /// bulging the way it did. A transformation that mirrors turns the plane over, which is why the
        /// normal is taken from where the ends and the middle of the arc land rather than rotated on its own.
        /// </remarks>
        public GeoEdge3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            GeoPoint3 start = transform.Transform(StartPoint);
            GeoPoint3 end = transform.Transform(EndPoint);

            if (!IsArc)
            {
                return new GeoEdge3(start, end);
            }

            GeoPoint3 middle = transform.Transform(GetPointAtParameter(0.5));

            return new GeoEdge3(GeoArc3.FromThreePoints(start, middle, end));
        }

        /// <summary>
        /// Splits the edge at a point along it, when that point is neither of its ends.
        /// </summary>
        /// <param name="parameter">How far along to cut, from nought at the start to one at the end.</param>
        /// <param name="first">The piece up to the cut.</param>
        /// <param name="second">The piece after it.</param>
        /// <returns>true if the cut fell inside the edge; otherwise, false, and neither piece is set.</returns>
        /// <remarks>
        /// Two arcs of the same radius come out of cutting an arc, so the pieces put back end to end draw
        /// exactly what went in.
        /// </remarks>
        public bool TrySplitAtParameter(double parameter, out GeoEdge3 first, out GeoEdge3 second)
        {
            if (double.IsNaN(parameter) || parameter <= 0.0 || parameter >= 1.0)
            {
                first = default(GeoEdge3);
                second = default(GeoEdge3);
                return false;
            }

            GeoPoint3 at = GetPointAtParameter(parameter);

            if (!IsArc)
            {
                first = new GeoEdge3(StartPoint, at);
                second = new GeoEdge3(at, EndPoint);
                return true;
            }

            double sweep = 4.0 * Math.Atan(Bulge);

            first = new GeoEdge3(StartPoint, at, Math.Tan(sweep * parameter * 0.25), Normal);
            second = new GeoEdge3(at, EndPoint, Math.Tan(sweep * (1.0 - parameter) * 0.25), Normal);
            return true;
        }

        /// <summary>
        /// Determines whether this edge holds the same ends and the same bulge in the same plane as another.
        /// </summary>
        public bool IsEqualTo(GeoEdge3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this edge holds the same ends and the same bulge in the same plane as another, within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoEdge3 other, Tolerance tolerance)
        {
            if (!StartPoint.IsEqualTo(other.StartPoint, tolerance) || !EndPoint.IsEqualTo(other.EndPoint, tolerance))
            {
                return false;
            }

            if (IsArc != other.IsArc)
            {
                return false;
            }

            if (!IsArc)
            {
                return true;
            }

            return Math.Abs(Bulge - other.Bulge) <= tolerance.EqualVector
                && Normal.IsEqualTo(other.Normal, tolerance);
        }

        /// <summary>
        /// Indicates whether the current edge is equal to another edge.
        /// </summary>
        public bool Equals(GeoEdge3 other)
            => StartPoint.Equals(other.StartPoint)
               && EndPoint.Equals(other.EndPoint)
               && Bulge.Equals(other.Bulge)
               && Normal.Equals(other.Normal);

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoEdge3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StartPoint.GetHashCode();
                hash = (hash * 397) ^ EndPoint.GetHashCode();
                hash = (hash * 397) ^ Bulge.GetHashCode();
                return (hash * 397) ^ Normal.GetHashCode();
            }
        }

        /// <summary>
        /// Determines whether two edges are the same.
        /// </summary>
        public static bool operator ==(GeoEdge3 left, GeoEdge3 right) => left.Equals(right);

        /// <summary>
        /// Determines whether two edges differ.
        /// </summary>
        public static bool operator !=(GeoEdge3 left, GeoEdge3 right) => !left.Equals(right);

        /// <summary>
        /// Returns a string describing the edge.
        /// </summary>
        public override string ToString()
            => IsArc
                ? string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "GeoEdge3[{0} -> {1}, bulge {2:0.###} about ({3:0.###}, {4:0.###}, {5:0.###})]",
                    StartPoint, EndPoint, Bulge, Normal.X, Normal.Y, Normal.Z)
                : string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "GeoEdge3[{0} -> {1}]",
                    StartPoint, EndPoint);

        /// <summary>
        /// Gets the frame the bulge is read in: standing on the start point, X along the chord, Z the normal.
        /// </summary>
        private GeoCoordinateSystem3 Frame()
        {
            GeoVector3 along = StartPoint.GetVectorTo(EndPoint);

            return new GeoCoordinateSystem3(StartPoint, along, Normal.CrossProduct(along));
        }

        /// <summary>
        /// Lifts a point of the flat arc into the frame the bulge was read in.
        /// </summary>
        private static GeoPoint3 Lift(GeoPoint2 point) => new GeoPoint3(point.X, point.Y, 0.0);
    }
}
