using System;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    public sealed partial class GeoSolid3
    {
        #region Making bodies

        /// <summary>
        /// Makes the body a flat polygon sweeps out moving along a vector, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Extrude(GeoPolygon3 profile, GeoVector3 direction) => Extrude(profile, direction, Tolerance.Global);

        /// <summary>
        /// Makes the body a flat polygon sweeps out moving along a vector: a prism, upright or leaning.
        /// </summary>
        /// <param name="profile">The polygon; it is one end of the body.</param>
        /// <param name="direction">How far and which way the profile moves; any way out of its plane.</param>
        /// <param name="tolerance">The tolerance deciding whether the direction leaves the plane.</param>
        /// <returns>The body, wound outwards whichever way round the profile was drawn.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the direction lies in the plane of the profile.</exception>
        public static GeoSolid3 Extrude(GeoPolygon3 profile, GeoVector3 direction, Tolerance tolerance)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return Sweep3.Extrude(new GeoFace3(profile), direction, tolerance);
        }

        /// <summary>
        /// Makes the body a flat face sweeps out moving along a vector, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Extrude(GeoFace3 profile, GeoVector3 direction) => Extrude(profile, direction, Tolerance.Global);

        /// <summary>
        /// Makes the body a flat face sweeps out moving along a vector: each hole of the face runs through the
        /// body as a shaft.
        /// </summary>
        /// <param name="profile">The face; it is one end of the body.</param>
        /// <param name="direction">How far and which way the face moves; any way out of its plane.</param>
        /// <param name="tolerance">The tolerance deciding whether the direction leaves the plane.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the direction lies in the plane of the face.</exception>
        public static GeoSolid3 Extrude(GeoFace3 profile, GeoVector3 direction, Tolerance tolerance)
            => Sweep3.Extrude(profile, direction, tolerance);

        /// <summary>
        /// Makes the body a profile drawn in a plane sweeps out moving straight out of it, using the default
        /// tolerance.
        /// </summary>
        public static GeoSolid3 Extrude(GeoPolygon2 profile, GeoCoordinateSystem3 placement, double length)
            => Extrude(profile, placement, length, Tolerance.Global);

        /// <summary>
        /// Makes the body a profile drawn in a plane sweeps out moving straight out of it: a plate from its
        /// outline, a member from its section.
        /// </summary>
        /// <param name="profile">The profile, drawn in the XY plane of the placement.</param>
        /// <param name="placement">Where the profile stands; the body runs along its Z axis.</param>
        /// <param name="length">How far along Z; negative runs the other way.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the length is too short to sweep out any volume.</exception>
        public static GeoSolid3 Extrude(GeoPolygon2 profile, GeoCoordinateSystem3 placement, double length, Tolerance tolerance)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            return Sweep3.Extrude(new GeoFace2(profile), placement, length, tolerance);
        }

        /// <summary>
        /// Makes the body a face drawn in a plane sweeps out moving straight out of it, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Extrude(GeoFace2 profile, GeoCoordinateSystem3 placement, double length)
            => Extrude(profile, placement, length, Tolerance.Global);

        /// <summary>
        /// Makes the body a face drawn in a plane sweeps out moving straight out of it: each hole of the face runs
        /// through the body.
        /// </summary>
        /// <param name="profile">The face, drawn in the XY plane of the placement.</param>
        /// <param name="placement">Where the face stands; the body runs along its Z axis.</param>
        /// <param name="length">How far along Z; negative runs the other way.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the length is too short to sweep out any volume.</exception>
        public static GeoSolid3 Extrude(GeoFace2 profile, GeoCoordinateSystem3 placement, double length, Tolerance tolerance)
            => Sweep3.Extrude(profile, placement, length, tolerance);

        /// <summary>
        /// Makes the body a curved loop drawn in a plane sweeps out moving straight out of it, using the default
        /// tolerance.
        /// </summary>
        public static GeoSolid3 Extrude(GeoPolygonArc2 profile, GeoCoordinateSystem3 placement, double length, double chordTolerance)
            => Extrude(profile, placement, length, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Makes the body a curved loop drawn in a plane sweeps out moving straight out of it, its arcs cut into
        /// chords no further than a tolerance from them.
        /// </summary>
        /// <param name="profile">The loop, drawn in the XY plane of the placement.</param>
        /// <param name="placement">Where the loop stands; the body runs along its Z axis.</param>
        /// <param name="length">How far along Z; negative runs the other way.</param>
        /// <param name="chordTolerance">The largest gap allowed between a chord and its arc; nought picks one from the radius.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the length is too short to sweep out any volume.</exception>
        public static GeoSolid3 Extrude(GeoPolygonArc2 profile, GeoCoordinateSystem3 placement, double length, double chordTolerance, Tolerance tolerance)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            Guard.Finite(length, nameof(length), "A length has to be a number.");

            return Sweep3.Extrude(new GeoFace3(profile.ToPolygon3(placement, chordTolerance)), placement.ZAxis.Multiply(length), tolerance);
        }

        /// <summary>
        /// Makes a round bar or a bolt between two points, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Cylinder(GeoPoint3 start, GeoPoint3 end, double radius, int segments)
            => Cylinder(start, end, radius, segments, Tolerance.Global);

        /// <summary>
        /// Makes a round bar or a bolt between two points: a prism on a regular polygon with its corners on the
        /// circle.
        /// </summary>
        /// <param name="start">The centre of one end.</param>
        /// <param name="end">The centre of the other.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="segments">How many sides the section has; three or more.</param>
        /// <param name="tolerance">The tolerance deciding whether the two ends are one.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not positive or there are fewer than three sides.</exception>
        /// <exception cref="ArgumentException">Thrown when the two ends are the same point.</exception>
        public static GeoSolid3 Cylinder(GeoPoint3 start, GeoPoint3 end, double radius, int segments, Tolerance tolerance)
            => Sweep3.Cylinder(start, end, radius, segments, tolerance);

        /// <summary>
        /// Makes the body a profile sweeps out carried along a chain, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Sweep(GeoPolygon2 profile, GeoPolyline3 path) => Sweep(profile, path, Tolerance.Global);

        /// <summary>
        /// Makes the body a profile sweeps out carried along a chain, within a tolerance.
        /// </summary>
        /// <param name="profile">
        /// The section, drawn about the origin: its origin rides on the path, its X to the path's right and its Y
        /// up — the world's up where the path allows it.
        /// </param>
        /// <param name="path">The chain the section is carried along.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <remarks>
        /// The section stands square to the path at either end, and at each bend lies on the plane halving it,
        /// so the pieces meet mitred. It turns with the path about the axis of each bend and nothing else, so it
        /// does not twist along the way. A section larger than a bend is tight folds over itself there; that is
        /// not checked.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the profile or the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path has no length or turns back on itself.</exception>
        public static GeoSolid3 Sweep(GeoPolygon2 profile, GeoPolyline3 path, Tolerance tolerance)
            => Sweep3.Sweep(profile, Vertices(path), null, tolerance);

        /// <summary>
        /// Makes the body a profile sweeps out carried along a chain, standing a given way up, using the default
        /// tolerance.
        /// </summary>
        public static GeoSolid3 Sweep(GeoPolygon2 profile, GeoPolyline3 path, GeoVector3 up) => Sweep(profile, path, up, Tolerance.Global);

        /// <summary>
        /// Makes the body a profile sweeps out carried along a chain, standing a given way up at the start.
        /// </summary>
        /// <param name="profile">The section, drawn about the origin; its Y stands along <paramref name="up"/>.</param>
        /// <param name="path">The chain the section is carried along.</param>
        /// <param name="up">Which way the section's Y points at the start; any way not along the path.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile or the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path has no length or turns back on itself, or the up direction runs along it.</exception>
        public static GeoSolid3 Sweep(GeoPolygon2 profile, GeoPolyline3 path, GeoVector3 up, Tolerance tolerance)
            => Sweep3.Sweep(profile, Vertices(path), up, tolerance);

        /// <summary>
        /// Makes the body a profile sweeps out carried along a chain with bends, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Sweep(GeoPolygon2 profile, GeoPolylineArc3 path, double chordTolerance)
            => Sweep(profile, path, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Makes the body a profile sweeps out carried along a chain with bends, each bend cut into chords no
        /// further than a tolerance from it.
        /// </summary>
        /// <param name="profile">The section, drawn about the origin, its Y up where the path allows it.</param>
        /// <param name="path">The chain, such as the centre line of a bent bar.</param>
        /// <param name="chordTolerance">The largest gap allowed between a chord and its bend; nought picks one from the radius.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile or the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path has no length or turns back on itself.</exception>
        public static GeoSolid3 Sweep(GeoPolygon2 profile, GeoPolylineArc3 path, double chordTolerance, Tolerance tolerance)
            => Sweep3.Sweep(profile, Vertices(path, chordTolerance), null, tolerance);

        /// <summary>
        /// Makes the body a profile sweeps out carried along a chain with bends, standing a given way up, using
        /// the default tolerance.
        /// </summary>
        public static GeoSolid3 Sweep(GeoPolygon2 profile, GeoPolylineArc3 path, double chordTolerance, GeoVector3 up)
            => Sweep(profile, path, chordTolerance, up, Tolerance.Global);

        /// <summary>
        /// Makes the body a profile sweeps out carried along a chain with bends, standing a given way up at the
        /// start.
        /// </summary>
        /// <param name="profile">The section, drawn about the origin; its Y stands along <paramref name="up"/>.</param>
        /// <param name="path">The chain.</param>
        /// <param name="chordTolerance">The largest gap allowed between a chord and its bend; nought picks one from the radius.</param>
        /// <param name="up">Which way the section's Y points at the start; any way not along the path.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile or the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path has no length or turns back on itself, or the up direction runs along it.</exception>
        public static GeoSolid3 Sweep(GeoPolygon2 profile, GeoPolylineArc3 path, double chordTolerance, GeoVector3 up, Tolerance tolerance)
            => Sweep3.Sweep(profile, Vertices(path, chordTolerance), up, tolerance);

        /// <summary>
        /// Makes a round bar along a chain, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Pipe(GeoPolyline3 path, double radius, double chordTolerance)
            => Pipe(path, radius, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Makes a round bar along a chain, its section a polygon no further than a chord tolerance from the circle.
        /// </summary>
        /// <param name="path">The centre line.</param>
        /// <param name="radius">The radius of the bar.</param>
        /// <param name="chordTolerance">The largest gap allowed between the section and its circle; nought picks one from the radius.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not positive.</exception>
        /// <exception cref="ArgumentException">Thrown when the path has no length or turns back on itself.</exception>
        public static GeoSolid3 Pipe(GeoPolyline3 path, double radius, double chordTolerance, Tolerance tolerance)
            => Sweep3.Sweep(Sweep3.Circle(radius, chordTolerance), Vertices(path), null, tolerance);

        /// <summary>
        /// Makes a bent round bar along a chain with bends, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Pipe(GeoPolylineArc3 path, double radius, double chordTolerance)
            => Pipe(path, radius, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Makes a bent round bar along a chain with bends — a reinforcing bar from its centre line — the bends
        /// and the section each cut into chords no further than a tolerance from the curve.
        /// </summary>
        /// <param name="path">The centre line, bends and all.</param>
        /// <param name="radius">The radius of the bar.</param>
        /// <param name="chordTolerance">The largest gap allowed between a chord and its curve; nought picks one from the radius.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not positive.</exception>
        /// <exception cref="ArgumentException">Thrown when the path has no length or turns back on itself.</exception>
        public static GeoSolid3 Pipe(GeoPolylineArc3 path, double radius, double chordTolerance, Tolerance tolerance)
            => Sweep3.Sweep(Sweep3.Circle(radius, chordTolerance), Vertices(path, chordTolerance), null, tolerance);

        /// <summary>
        /// Makes the body a profile sweeps out turned round an axis, using the default tolerance.
        /// </summary>
        public static GeoSolid3 Revolve(GeoPolygon2 profile, GeoCoordinateSystem3 placement, double angleRad, double chordTolerance)
            => Revolve(profile, placement, angleRad, chordTolerance, Tolerance.Global);

        /// <summary>
        /// Makes the body a profile sweeps out turned round an axis: a ring, a boss, a round pier.
        /// </summary>
        /// <param name="profile">
        /// The profile, drawn in the XY plane of the placement with X the distance from the axis, nought or more,
        /// and Y the height along it.
        /// </param>
        /// <param name="placement">The axis is its Y axis, through its origin.</param>
        /// <param name="angleRad">How far the profile turns, positively about Y; a whole turn is two pi and leaves no ends.</param>
        /// <param name="chordTolerance">The largest gap allowed between a facet and the true surface; nought picks one from the radius.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The body, wound outwards.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the profile is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle is not above nought or is more than a whole turn.</exception>
        /// <exception cref="ArgumentException">Thrown when the profile reaches across the axis or lies along it.</exception>
        public static GeoSolid3 Revolve(GeoPolygon2 profile, GeoCoordinateSystem3 placement, double angleRad, double chordTolerance, Tolerance tolerance)
            => Sweep3.Revolve(profile, placement, angleRad, chordTolerance, tolerance);

        private static System.Collections.Generic.IReadOnlyList<GeoPoint3> Vertices(GeoPolyline3 path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return path.Vertices;
        }

        private static System.Collections.Generic.IReadOnlyList<GeoPoint3> Vertices(GeoPolylineArc3 path, double chordTolerance)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return path.ToPolyline3(chordTolerance).Vertices;
        }

        #endregion
    }
}
