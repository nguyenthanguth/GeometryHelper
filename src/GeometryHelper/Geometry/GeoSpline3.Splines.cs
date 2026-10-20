using System;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a spline is from another spline in space, whether they touch, where they meet and the shortest segment between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two splines in space are asked about each other from either side with the same answer to the bit: the work is done from the one
    /// that comes first in a fixed order (degree, number of control points, then the control points, weights and knots), and the other side
    /// turns the shortest segment round and gives the same points in order along itself. The spline against itself, or against a copy,
    /// runs along it from start to end: it is nought from it, collides with it, and crosses it nowhere.
    /// </para>
    /// <para>
    /// The pieces of the two splines are cut in halves while their hulls, measured by their boxes and by the shadows of their control points
    /// on the chord of each part, on the two directions square to it and on the direction square to both chords, are near enough to
    /// matter and they are not yet straight; each pair that is straight to a thousandth of its size, and whose tangents can never run the
    /// same way, or that is straight to a billionth, is measured as two segments and brought to the real curves by Newton's method. For
    /// the nearest approach the parts are taken nearest bound first, and the cutting ends when the nearest bound left is no nearer than the
    /// best found less a hair (a billionth of the size of the shapes, and never more than a thousandth of the distance); the distance is exact
    /// to rounding where Newton's method settles, which it does wherever the curves cross or the nearest points are not parallel, and otherwise
    /// right to that hair. For the places two splines meet, a part is left when the hulls show no meeting in it within the point tolerance.
    /// </para>
    /// <para>
    /// Two splines that lie in one plane, every control point of both within a snap (1E-13 of the size of the numbers) of it, are worked out in
    /// that plane, as two splines of the plane are, and the points found are put back in space: the answers are the plane's, the offset of the
    /// one from the other having there the sign it has nowhere in space, and in a plane square to an axis they are the plane's to the bit. A
    /// tilted plane is laid flat by two directions of its own, so its answers are those of the plane turned, which for two curves that run a
    /// hair from each other the plane gives a place more or fewer for, as it does for the two turned in the plane.
    /// </para>
    /// <para>
    /// Two curves in space that share no plane seldom meet: a place is where the distance between them is least within the point tolerance, a
    /// crossing being a place where it comes to nought. Where two splines stay within the point tolerance of each other, and level (their
    /// distance changing by no more than half the tolerance) for a quarter of the size of the smaller, seen from both of them, they lie along
    /// each other, as two circles 0.0005 apart in two planes do: the distance is the gap and no less, and a place in the stretch is a least of
    /// the distance where the two would cross if brought into one plane. Seen along the gap at the least, the first stands on one side of the
    /// second just before it and on the other just after, clear by a hundredth of the tolerance before it goes back: a polyline spline and its
    /// copy moved 0.0005 along its plane and lifted 0.0002 off it come nearest, 0.0002 apart, at the three corners where the two in the plane
    /// cross, and a copy 0.0005 across the plane with a wobble of 0.0001, lifted off it, comes nearest where the wobble brings it nearest but
    /// stays on one side, and meets nowhere, as in the plane. A spline and its own copy moved less than the tolerance out of every plane meet at
    /// each least of their distance in the stretch (a twisted cubic of 600 points and its copy moved 0.0009, at 69), since seen along the gap a
    /// curve and its copy moved along its tangent cross where the curve bends. The stretch is looked at four times as often as it is in the plane, and
    /// closer where the first spline turns, at every join of its pieces and where every join of the other is nearest it. The cutting for the
    /// least distance is bounded: past 40 000 pairs of parts in one question the curves are looked at at 128 points and the least of them brought
    /// to the real curves, and the answer is the best found, the distance of a real pair of points, with the nearest bound left in one Debug line
    /// (<see cref="GeometryHelperLog"/>); the cutting for the places is bounded at two million pairs and logs one Debug line past it. Two parts
    /// that cannot meet are measured once both are no larger than the tolerance, so that two splines within the tolerance of each other round a
    /// turn tighter than a millimetre are not cut to a millionth.
    /// </para>
    /// <para>
    /// A touch of two splines in space that share no plane, tangent to each other, is found to a snap or two and not to the last digit: the
    /// nearest pairs of two curves that touch lie along a valley, which Newton's method follows until a step gains less than a snap, so a
    /// spline about 1 000 across touching a parabola reads 6E-11 to 1.7E-10 from it, where the snap is 1E-10, and may read as not nought. A
    /// crossing at an angle is nought.
    /// </para>
    /// </remarks>
    public sealed partial class GeoSpline3
    {
        #region Against another spline

        /// <summary>
        /// Calculates the shortest distance from this spline to another, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public double DistanceTo(GeoSpline3 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public double DistanceTo(GeoSpline3 other, Tolerance tolerance)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            return Spline3.DistanceTo(this, other);
        }

        /// <summary>
        /// Determines whether this spline touches another, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public bool CollidesWith(GeoSpline3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two come within the point tolerance of each other, exactly when <see cref="DistanceTo(GeoSpline3, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public bool CollidesWith(GeoSpline3 other, Tolerance tolerance) => DistanceTo(other, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Gets the points where this spline crosses another, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along this spline from its start.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSpline3 other) => GetIntersections(other, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, in order along this spline from its start: where they cross, where they touch without
        /// crossing, and where an end of one comes within the point tolerance of the other. The same points, bit for bit, as the other
        /// spline gives, in order along itself. Where the two lie along each other for a length no point is given along it but where they
        /// cross, and, for two that share no plane, where the distance is least and the two would cross if brought into one plane.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSpline3 other, Tolerance tolerance)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            return Spline3.GetIntersections(this, other, tolerance);
        }

        /// <summary>
        /// Tries to find where this spline crosses another, using the default tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public bool TryIntersectWith(GeoSpline3 other, out GeoPoint3[] intersections) => TryIntersectWith(other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public bool TryIntersectWith(GeoSpline3 other, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on another, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSpline3 other) => GetShortestLineTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of this spline to the point of the other nearest it, as long as <see cref="DistanceTo(GeoSpline3, Tolerance)"/>; of no length where the two cross. The other spline gives the same segment turned round.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSpline3 other, Tolerance tolerance)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            return Spline3.GetShortestLineTo(this, other);
        }

        #endregion
    }
}
