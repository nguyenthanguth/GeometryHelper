using System;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a spline is from another spline, whether they touch, where they cross and the shortest segment between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two splines are asked about each other from either side with the same answer to the bit: the work is done from the one
    /// that comes first in a fixed order (degree, number of control points, then the control points, weights and knots), and the
    /// other side turns the shortest segment round and gives the same points in order along itself. The spline against itself, or
    /// against a copy, runs along it from start to end: it is nought from it, collides with it, and crosses it nowhere.
    /// </para>
    /// <para>
    /// The pieces of the two splines are cut in halves while their hulls, measured by their boxes and by the shadows of their
    /// control points on the chord of each part and its square, are near enough to matter and they are not yet straight; each pair
    /// that is straight to a thousandth of its size, and whose tangents can never run the same way, or that is straight to a
    /// billionth, is measured as two segments and brought to the real curves by Newton's method. For the nearest approach the
    /// parts are taken nearest bound first, and the cutting ends when the nearest bound left is no nearer than the best found less
    /// a hair (a billionth of the size of the shapes, and never more than a thousandth of the distance); for the places two splines
    /// meet, a part is left when the hulls show no meeting in it within the point tolerance. The distance is exact to rounding where
    /// Newton's method settles, which it does wherever the curves cross or the nearest points are not parallel, and otherwise right
    /// to that hair. A crossing at any angle, however shallow, is found by the offset of the one curve from the other along its
    /// normal, which changes sign where they cross: two circles of radius 1 000 and 1 000.00002 with their centres 1E-4 apart cross at
    /// (-200, 979.8) and (-200, -979.8) at an angle of 1E-7, and a line 40 long that cuts a circle spline of radius 1 000 in a chord
    /// of 0.004 meets it twice, as the <see cref="GeoCircle2"/> does. Two meetings are one place only when they are within the
    /// point tolerance of each other.
    /// </para>
    /// <para>
    /// Where two splines stay within the point tolerance of each other, and level (their distance changing by no more than half the
    /// tolerance) for a quarter of the size of the smaller, seen from both of them, they lie along each other, as two circles
    /// 0.0005 apart do, or a spline and a curve fitted to it: no place is given in the stretch, since the places there would be where
    /// the rounding of the numbers has them cross, the distance is the gap and no less (0.0005 for the circles), and they collide.
    /// A stretch that either leaves by more than the tolerance, as a dart of a polyline 2E-4 off a line going out a unit and back
    /// does, is cut out and the stretch cut in two there, and the crossings at the dart's edges are given. A sign change of the
    /// offset inside a stretch, by more than a hundredth of the tolerance to each side, is a crossing and is given. A touch, whose
    /// distance goes from nought to the tolerance and past it within a few units, and a crossing at any angle, are not level, and are
    /// places. A spline fitted through 37 points of a quarter circle of radius 1 000 gives its true crossings against a
    /// <see cref="GeoArc2"/> of that arc, which is solved exactly (72 of them, the fit being off the arc a little between its
    /// points), and is along the arc spline of it (<see cref="FromArc(GeoArc2)"/>), where only the crossings that go more than a
    /// hundredth of the tolerance to each side are given (10, in the first and last tenth of the fit). The cutting for the least distance is
    /// bounded: past 40 000 pairs of parts in one question, the curves are looked at at 128 points and the least of them brought to the
    /// real curves, and the answer is the best found. What is promised then is that it is the distance of a real pair of points, so
    /// the truth lies between it and the nearest bound left, and one Debug line gives both (<see cref="GeometryHelperLog"/>); a
    /// meeting not yet found is not in the answer. Two circles of radius 100 000 and 100 000.0005, the second with its centre
    /// moved 2E-4 toward 45 degrees, are 0.00029999998 apart that way (0.0003 exact, 0.000296 the bound left), in 0.15 s. Short of
    /// the budget a crossing is never passed over for a nearer approach found first. The cutting for the places is bounded at two million pairs
    /// and logs one Debug line past it; nothing built in the tests reaches it, and two splines that stay within the tolerance of
    /// each other for a long stretch and are not level there, or hug each other for less than a quarter of the size at a distance
    /// that makes every part of the one a near match for hundreds of the other, could.
    /// </para>
    /// </remarks>
    public sealed partial class GeoSpline2
    {
        #region Against another spline

        /// <summary>
        /// Calculates the shortest distance from this spline to another, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public double DistanceTo(GeoSpline2 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this spline to another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="tolerance">The tolerance; not read, since a distance is measured rather than judged.</param>
        /// <returns>Nought only where they truly meet, a crossing or a touch found to rounding, otherwise the gap, measured exactly.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public double DistanceTo(GeoSpline2 other, Tolerance tolerance)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            return Spline2.DistanceTo(this, other);
        }

        /// <summary>
        /// Determines whether this spline touches another, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public bool CollidesWith(GeoSpline2 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Determines whether this spline touches another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the two come within the point tolerance of each other, exactly when <see cref="DistanceTo(GeoSpline2, Tolerance)"/> is no more than it; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public bool CollidesWith(GeoSpline2 other, Tolerance tolerance) => DistanceTo(other, tolerance) <= tolerance.EqualPoint;

        /// <summary>
        /// Gets the points where this spline crosses another, using the default tolerance.
        /// </summary>
        /// <returns>The points, in order along this spline from its start.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoSpline2 other) => GetIntersections(other, Tolerance.Global);

        /// <summary>
        /// Gets the points where this spline crosses another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="tolerance">The tolerance: places no further than its point tolerance apart are one place.</param>
        /// <returns>
        /// Every place where the two meet, once, in order along this spline from its start: where they cross, where they touch without
        /// crossing, and where an end of one comes within the point tolerance of the other. The same points, bit for bit, as the other
        /// spline gives, in order along itself. Where the two lie along each other for a length they are not crossing, and no point is given along it.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoSpline2 other, Tolerance tolerance)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            return Spline2.GetIntersections(this, other, tolerance);
        }

        /// <summary>
        /// Tries to find where this spline crosses another, using the default tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public bool TryIntersectWith(GeoSpline2 other, out GeoPoint2[] intersections) => TryIntersectWith(other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this spline crosses another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public bool TryIntersectWith(GeoSpline2 other, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on another, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoSpline2 other) => GetShortestLineTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on another, within a tolerance.
        /// </summary>
        /// <param name="other">The other spline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest points take none.</param>
        /// <returns>The segment from the point of this spline to the point of the other nearest it, as long as <see cref="DistanceTo(GeoSpline2, Tolerance)"/>; of no length where the two cross. The other spline gives the same segment turned round.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the other spline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoSpline2 other, Tolerance tolerance)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            return Spline2.GetShortestLineTo(this, other);
        }

        #endregion
    }
}
