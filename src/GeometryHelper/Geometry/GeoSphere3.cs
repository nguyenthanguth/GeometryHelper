using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// Represents a solid ball in space, defined by its centre and its radius.
    /// <para>
    /// A ball is read as filled, as a solid is and as the disc of a circle is: a point inside it is nought from it, and
    /// <see cref="SignedDistanceTo(GeoPoint3)"/> keeps the depth of that point, negative, for the ones that ask how deep. Where
    /// only the shell matters, <see cref="DistanceToSurface(GeoPoint3)"/> measures to it from either side.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ball is a point grown by its radius, so its gap to any shape is the gap between its centre and that shape less the radius,
    /// and nought where the shape reaches into it: a ball of radius 50 about (0, 0, 100) is 50 from the floor and touches it
    /// when its centre is lowered to 50, and a ball of radius 10 about (200, 0, 0) is 140 from the ball of radius 50 about the
    /// origin, 200 less 50 less 10, whichever way round it is asked. Every query is answered from the centre, so it is exact to
    /// the rounding of the shape's own distance to a point.
    /// </para>
    /// <para>
    /// Where its surface is crossed is a question about the surface, and answered with points: a line through the middle of the
    /// ball crosses it twice, a plane through it cuts a circle, and a line or a curve that lies wholly inside crosses it nowhere
    /// though it collides with the ball. A body is asked whether it touches the ball and how far from it; where their surfaces
    /// cross is the clash's question, asked of <see cref="ToSolid(double)"/>.
    /// </para>
    /// </remarks>
    public readonly partial struct GeoSphere3 : IEquatable<GeoSphere3>
    {
        /// <summary>
        /// Gets the centre of the ball.
        /// </summary>
        public GeoPoint3 Center { get; }

        /// <summary>
        /// Gets the radius of the ball.
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// Initializes a new ball.
        /// </summary>
        /// <param name="center">The centre point.</param>
        /// <param name="radius">The radius; it must be a positive number.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not positive, or is not a number or infinite.</exception>
        public GeoSphere3(GeoPoint3 center, double radius)
        {
            Guard.Positive(radius, nameof(radius), "A sphere must have a positive radius.");

            Center = center;
            Radius = radius;
        }

        /// <summary>
        /// Creates a copy of this ball.
        /// </summary>
        /// <remarks>
        /// A ball is a readonly struct, so plain assignment already produces an independent copy and this method is not needed to
        /// avoid sharing. It exists so that every geometry type offers the same way to ask for a copy.
        /// </remarks>
        public GeoSphere3 Clone() => new GeoSphere3(Center, Radius);

        /// <summary>
        /// Moves the ball by a vector.
        /// </summary>
        /// <param name="vector">How far to move it, and which way.</param>
        /// <returns>The ball in its new place.</returns>
        public GeoSphere3 Translate(GeoVector3 vector) => new GeoSphere3(Center.Add(vector), Radius);

        /// <summary>
        /// Applies a transformation to this ball.
        /// </summary>
        /// <param name="transform">The transformation to apply.</param>
        /// <returns>The ball with its centre moved as a point and its radius scaled by the transformation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the transformation would turn the ball into an ellipsoid, or flatten it.
        /// </exception>
        /// <remarks>
        /// A ball survives a transformation only when every direction is stretched by the same amount: a turn, a shift, a mirror or
        /// an even scaling. Scaled by 3 along X alone it would be an ellipsoid, which this library has no type for, so the
        /// attempt is refused, as it is for a circle, rather than answered with a ball of some averaged radius.
        /// </remarks>
        public GeoSphere3 TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            return transform.Transform(this);
        }

        /// <summary>
        /// Gets the diameter of the ball.
        /// </summary>
        public double Diameter => Radius * 2.0;

        /// <summary>
        /// Gets the volume of the ball, four thirds of pi times the cube of the radius.
        /// </summary>
        /// <remarks>
        /// A ball of radius 50 holds 523 598.8 cubic units.
        /// </remarks>
        public double Volume => 4.0 / 3.0 * Math.PI * Radius * Radius * Radius;

        /// <summary>
        /// Gets the area of the surface of the ball, four times pi times the square of the radius.
        /// </summary>
        /// <remarks>
        /// A ball of radius 50 has a surface of 31 415.9 square units.
        /// </remarks>
        public double SurfaceArea => 4.0 * Math.PI * Radius * Radius;

        /// <summary>
        /// Gets the axis-aligned bounding box enclosing this ball: the centre less and plus the radius along every axis.
        /// </summary>
        public GeoAabb3 GetAabb()
        {
            GeoVector3 extent = new GeoVector3(Radius, Radius, Radius);

            return new GeoAabb3(Center.Subtract(extent), Center.Add(extent));
        }

        #region Queries

        /// <summary>
        /// Locates a point relative to this ball, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Locates a point relative to this ball, within a tolerance.
        /// </summary>
        /// <returns>Inside, on the surface where the point is within the point tolerance of it, or outside.</returns>
        /// <remarks>
        /// For the ball of radius 50 about the origin, (30, 0, 0) is inside, (50.0005, 0, 0) is on the surface at the default
        /// tolerance, and (60, 0, 0) is outside.
        /// </remarks>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance) => Sphere3.Locate(this, point, tolerance);

        /// <summary>
        /// Checks whether this ball holds a point, on its surface included, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Contains(point, Tolerance.Global);

        /// <summary>
        /// Checks whether this ball holds a point, on its surface included, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Locate(point, tolerance) != PointLocation.OutSide;

        /// <summary>
        /// Checks whether a point lies on the surface, using the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Checks whether a point lies on the surface, within a tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => Locate(point, tolerance) == PointLocation.OnSide;

        #endregion

        #region Equality

        /// <summary>
        /// Determines whether another ball has exactly the same centre and radius.
        /// </summary>
        public bool Equals(GeoSphere3 other) => Center.Equals(other.Center) && Radius.Equals(other.Radius);

        /// <summary>
        /// Determines whether the specified object is equal to the current ball.
        /// </summary>
        public override bool Equals(object obj) => obj is GeoSphere3 other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Center.GetHashCode();
                hashCode = (hashCode * 397) ^ Radius.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Compares whether this ball equals another using the default tolerance.
        /// </summary>
        public bool IsEqualTo(GeoSphere3 other) => IsEqualTo(other, Tolerance.Global);

        /// <summary>
        /// Compares whether this ball equals another within a tolerance.
        /// </summary>
        public bool IsEqualTo(GeoSphere3 other, Tolerance tolerance)
        {
            return Center.IsEqualTo(other.Center, tolerance) && Math.Abs(Radius - other.Radius) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks if two balls have exactly the same centre and radius.
        /// </summary>
        public static bool operator ==(GeoSphere3 left, GeoSphere3 right) => left.Equals(right);

        /// <summary>
        /// Checks if two balls differ in centre or radius.
        /// </summary>
        public static bool operator !=(GeoSphere3 left, GeoSphere3 right) => !left.Equals(right);

        #endregion

        /// <summary>
        /// Gets whether this is a shape a constructor could have made: the centre is a valid point and the radius a positive number.
        /// </summary>
        /// <remarks>
        /// A value made by a constructor always is. A <c>default</c> one, an element of a new array, has no radius and answers
        /// questions without complaint all the same.
        /// </remarks>
        public bool IsValid => Center.IsValid && Radius > 0.0 && Guard.IsFinite(Radius);

        /// <summary>
        /// Returns a string that represents the current ball.
        /// </summary>
        public override string ToString() => $"Sphere3(Center: {Center}, Radius: {Radius:0.###})";

        #region Approximating by flat faces

        /// <summary>
        /// Approximates the ball as a closed body of triangles, none of them further from the sphere than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a face and the sphere, in drawing units. Zero picks 0.2 % of the radius.</param>
        /// <returns>A closed body whose faces face outwards, every corner of them on the sphere.</returns>
        /// <exception cref="ArgumentException">Thrown when the ball is not valid, as a <c>default</c> one is.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a finite number, or needs more than 1 048 576 triangles.</exception>
        /// <remarks>
        /// <para>
        /// An icosahedron of twenty triangles, each cut into n by n smaller ones by lines parallel to its sides and every corner
        /// pushed out to the sphere, with the least n for which the sag of every triangle, the gap between its plane and the
        /// sphere at the nearest, is within the tolerance: a ball of radius 50 cut within 0.1 is 20 by 13 by 13, 3 380 triangles,
        /// and within 0.01 it is 20 by 39 by 39, 30 420, none of them further than that from the sphere. The corners lie on the
        /// sphere, so the body lies inside it and holds a little less than <see cref="Volume"/>: 0.036 % less at 0.01. A tolerance
        /// that needs more than 1 048 576 triangles is refused, as for the chords of a circle: radius 50 within 0.0001 would need
        /// three million. A ball of a radius below about 0.01 has triangles thinner than the point tolerance, and its body may be one
        /// the library calls open at the default tolerance.
        /// </para>
        /// <para>
        /// A ball is clashed with a body through this one: <c>ball.ToSolid(0.01)</c> goes where any solid goes, and its clash is
        /// as exact as the tolerance. Whether the ball touches a body, and how far apart they are, are exact without it
        /// (<see cref="CollidesWith(GeoSolid3)"/> and <see cref="DistanceTo(GeoSolid3)"/>).
        /// </para>
        /// </remarks>
        public GeoSolid3 ToSolid(double chordTolerance) => new GeoSolid3(Sphere3.FacesOf(this, chordTolerance));

        /// <summary>
        /// Breaks the surface of the ball into triangles, none of them further from the sphere than a chord tolerance.
        /// </summary>
        /// <param name="chordTolerance">The largest gap allowed between a triangle and the sphere, in drawing units. Zero picks 0.2 % of the radius.</param>
        /// <returns>The triangles of <see cref="ToSolid(double)"/>, counter-clockwise seen from outside, in a fixed order.</returns>
        /// <exception cref="ArgumentException">Thrown when the ball is not valid, as a <c>default</c> one is.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the tolerance is negative or not a finite number, or needs more than 1 048 576 triangles.</exception>
        public GeoTriangle3[] TriangulateSurface(double chordTolerance) => Sphere3.Triangulate(this, chordTolerance);

        #endregion
    }
}
