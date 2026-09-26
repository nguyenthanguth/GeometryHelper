using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Extending and trimming the curved shapes in space: an arc, and a chain whose end leg may be one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a segment could be extended or trimmed, which left the one case a reinforcing schedule asks about
    /// every day — lengthening a bar's end leg for anchorage — with no answer but rebuilding the chain by hand.
    /// </para>
    /// <para>
    /// An arc is extended along itself: the radius and the centre stay, the sweep grows, and the distance asked
    /// for is arc length and not chord. That is exact, and it is the only reading that leaves the curve where it
    /// was. A whole turn is the limit, so an extension that would pass it is refused rather than wrapped.
    /// </para>
    /// <para>
    /// A chain is extended by its <b>end leg</b>, along that leg: a straight leg carries on straight, a curved
    /// one carries on round, keeping its radius. Every other leg is untouched, so the bends and their radii
    /// survive. A chain is extended <b>outwards only</b>; shortening one belongs to the cutting family, which
    /// keeps the bends, and <c>TryTrimTo</c> below is that cut with the end named rather than the piece.
    /// </para>
    /// </remarks>
    public static partial class Lengthen3
    {
        private const double FullTurn = 2.0 * Math.PI;

        #region Extending and trimming an arc

        /// <summary>
        /// Lengthens an arc along itself at one end.
        /// </summary>
        public static GeoArc3 Extend(GeoArc3 arc, double distance, LineEnd end) => Extend(arc, distance, end, Tolerance.Global);

        /// <summary>
        /// Lengthens an arc along itself at one end, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="distance">How much arc length to add; a negative distance takes it away.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The arc with that end moved, keeping its centre and radius.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the distance is not a finite number, when it would leave no arc at all, or when it would
        /// carry the arc past a whole turn.
        /// </exception>
        public static GeoArc3 Extend(GeoArc3 arc, double distance, LineEnd end, Tolerance tolerance)
        {
            ValidateEnd(end);
            RequireFinite(distance, nameof(distance));

            double sweep = arc.SweptAngle + Direction(arc) * distance / arc.Radius;

            if (!TryReswept(arc, end, sweep, tolerance, out GeoArc3 result))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(distance), "That distance leaves no arc, or carries it past a whole turn.");
            }

            return result;
        }

        /// <summary>
        /// Lengthens an arc along itself at both ends at once.
        /// </summary>
        public static GeoArc3 Extend(GeoArc3 arc, double startDistance, double endDistance)
            => Extend(arc, startDistance, endDistance, Tolerance.Global);

        /// <summary>
        /// Lengthens an arc along itself at both ends at once, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="startDistance">How much arc length to add before its start.</param>
        /// <param name="endDistance">How much to add after its end.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The arc with both ends moved, keeping its centre and radius.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when either distance is not a finite number, when together they would leave no arc, or when
        /// they would carry it past a whole turn.
        /// </exception>
        public static GeoArc3 Extend(GeoArc3 arc, double startDistance, double endDistance, Tolerance tolerance)
        {
            RequireFinite(startDistance, nameof(startDistance));
            RequireFinite(endDistance, nameof(endDistance));

            double direction = Direction(arc);
            double back = direction * startDistance / arc.Radius;
            double sweep = arc.SweptAngle + back + direction * endDistance / arc.Radius;

            if (!Holds(arc, sweep, tolerance))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endDistance), "Those distances leave no arc, or carry it past a whole turn.");
            }

            return Swept(arc, arc.StartAngle - back, sweep);
        }

        /// <summary>
        /// Lengthens or shortens an arc at one end until it is a given arc length.
        /// </summary>
        public static GeoArc3 ExtendToLength(GeoArc3 arc, double length, LineEnd end) => ExtendToLength(arc, length, end, Tolerance.Global);

        /// <summary>
        /// Lengthens or shortens an arc at one end until it is a given arc length, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="length">The arc length it should end up with, measured along the curve.</param>
        /// <param name="end">Which end to move; the other stays where it is.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The arc of that length, keeping its centre and radius.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the length is not a positive finite number, or is longer than the whole circle.
        /// </exception>
        public static GeoArc3 ExtendToLength(GeoArc3 arc, double length, LineEnd end, Tolerance tolerance)
        {
            ValidateEnd(end);
            RequireFinite(length, nameof(length));

            if (length <= tolerance.EqualPoint)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "An arc must be longer than the point tolerance.");
            }

            double sweep = Direction(arc) * length / arc.Radius;

            if (!TryReswept(arc, end, sweep, tolerance, out GeoArc3 result))
            {
                throw new ArgumentOutOfRangeException(nameof(length), "That length is more than the whole circle.");
            }

            return result;
        }

        /// <summary>
        /// Lengthens an arc at one end until it reaches a point on its circle.
        /// </summary>
        public static bool TryExtendTo(GeoArc3 arc, GeoPoint3 point, LineEnd end, out GeoArc3 result)
            => TryExtendTo(arc, point, end, out result, Tolerance.Global);

        /// <summary>
        /// Lengthens an arc at one end until it reaches a point on its circle, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="point">The point to reach; it has to lie on the arc's own circle.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="result">The arc reaching the point, or the arc unchanged.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the arc was lengthened to the point; otherwise, false.</returns>
        /// <remarks>
        /// Only a lengthening counts: a point the arc already covers is not reached by extending it, and
        /// <see cref="TryTrimTo(GeoArc3, GeoPoint3, LineEnd, out GeoArc3)"/> is the other direction. A point off the
        /// circle is refused rather than being drawn in to the nearest place on it.
        /// </remarks>
        public static bool TryExtendTo(GeoArc3 arc, GeoPoint3 point, LineEnd end, out GeoArc3 result, Tolerance tolerance)
        {
            ValidateEnd(end);
            result = arc;

            if (!OnCircle(arc, point, tolerance))
            {
                return false;
            }

            double sweep = SweepTo(arc, point, end);

            return Math.Abs(sweep) > Math.Abs(arc.SweptAngle) + tolerance.EqualAngleRad
                && TryReswept(arc, end, sweep, tolerance, out result);
        }

        /// <summary>
        /// Shortens an arc at one end back to a point on it.
        /// </summary>
        public static bool TryTrimTo(GeoArc3 arc, GeoPoint3 point, LineEnd end, out GeoArc3 result)
            => TryTrimTo(arc, point, end, out result, Tolerance.Global);

        /// <summary>
        /// Shortens an arc at one end back to a point on it, within a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="point">The point to stop at; it has to lie on the arc itself.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="result">The shortened arc, or the arc unchanged.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the arc was shortened to the point; otherwise, false.</returns>
        public static bool TryTrimTo(GeoArc3 arc, GeoPoint3 point, LineEnd end, out GeoArc3 result, Tolerance tolerance)
        {
            ValidateEnd(end);
            result = arc;

            if (!arc.IsPointOn(point, tolerance))
            {
                return false;
            }

            double sweep = SweepTo(arc, point, end);

            return Math.Abs(sweep) < Math.Abs(arc.SweptAngle) - tolerance.EqualAngleRad
                && TryReswept(arc, end, sweep, tolerance, out result);
        }

        #endregion

        #region Extending and trimming a straight chain

        /// <summary>
        /// Lengthens a chain along its end leg.
        /// </summary>
        public static GeoPolyline3 Extend(GeoPolyline3 chain, double distance, LineEnd end) => Extend(chain, distance, end, Tolerance.Global);

        /// <summary>
        /// Lengthens a chain along its end leg, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="distance">How far to carry the end leg on; it has to be positive.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain with that end carried on, every other leg untouched.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a positive finite number.</exception>
        /// <exception cref="ArgumentException">Thrown when the end leg has no length to carry on along.</exception>
        public static GeoPolyline3 Extend(GeoPolyline3 chain, double distance, LineEnd end, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            ValidateEnd(end);
            RequireOutwards(distance, tolerance);

            var vertices = new List<GeoPoint3>(chain.Vertices);
            int tip = end == LineEnd.End ? vertices.Count - 1 : 0;
            int inner = end == LineEnd.End ? vertices.Count - 2 : 1;

            vertices[tip] = Carried(vertices[inner], vertices[tip], distance, tolerance);

            return new GeoPolyline3(vertices);
        }

        /// <summary>
        /// Lengthens a chain along its end leg until the whole chain is a given length.
        /// </summary>
        public static GeoPolyline3 ExtendToLength(GeoPolyline3 chain, double length, LineEnd end) => ExtendToLength(chain, length, end, Tolerance.Global);

        /// <summary>
        /// Lengthens a chain along its end leg until the whole chain is a given length, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="length">The length the whole chain should end up with, measured along it.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain of that length.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is not longer than the chain already is.</exception>
        public static GeoPolyline3 ExtendToLength(GeoPolyline3 chain, double length, LineEnd end, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            RequireFinite(length, nameof(length));

            return Extend(chain, length - chain.Length, end, tolerance);
        }

        /// <summary>
        /// Shortens a chain at one end back to a point on it.
        /// </summary>
        public static bool TryTrimTo(GeoPolyline3 chain, GeoPoint3 point, LineEnd end, out GeoPolyline3 result)
            => TryTrimTo(chain, point, end, out result, Tolerance.Global);

        /// <summary>
        /// Shortens a chain at one end back to a point on it, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="point">The point to stop at; it has to lie on the chain.</param>
        /// <param name="end">Which end to give up.</param>
        /// <param name="result">The shortened chain, or the chain unchanged.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain was shortened; otherwise, false.</returns>
        /// <remarks>
        /// This is the cut the splitting family makes, with the end named rather than the piece: the piece kept
        /// is the one holding the other end, so every leg and bend between them survives.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryTrimTo(GeoPolyline3 chain, GeoPoint3 point, LineEnd end, out GeoPolyline3 result, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            ValidateEnd(end);
            result = chain;

            if (!Splition3.TrySplitBy(chain, point, out GeoPolyline3[] pieces, tolerance) || pieces.Length < 2)
            {
                return false;
            }

            result = end == LineEnd.End ? pieces[0] : pieces[pieces.Length - 1];

            return true;
        }

        #endregion

        #region Extending and trimming a curved chain

        /// <summary>
        /// Lengthens a curved chain along its end leg, round if that leg curves.
        /// </summary>
        public static GeoPolylineArc3 Extend(GeoPolylineArc3 chain, double distance, LineEnd end) => Extend(chain, distance, end, Tolerance.Global);

        /// <summary>
        /// Lengthens a curved chain along its end leg, round if that leg curves, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="distance">How far to carry the end leg on, measured along it; it has to be positive.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain with that end carried on; a curved end leg keeps its radius and gains sweep.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance is not a positive finite number.</exception>
        public static GeoPolylineArc3 Extend(GeoPolylineArc3 chain, double distance, LineEnd end, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            ValidateEnd(end);
            RequireOutwards(distance, tolerance);

            var edges = new List<GeoEdge3>(chain.GetEdges());
            int at = end == LineEnd.End ? edges.Count - 1 : 0;
            GeoEdge3 leg = edges[at];

            if (leg.IsArc)
            {
                edges[at] = new GeoEdge3(Extend(leg.ToArc(), distance, end, tolerance));
            }
            else if (end == LineEnd.End)
            {
                edges[at] = new GeoEdge3(leg.StartPoint, Carried(leg.StartPoint, leg.EndPoint, distance, tolerance));
            }
            else
            {
                edges[at] = new GeoEdge3(Carried(leg.EndPoint, leg.StartPoint, distance, tolerance), leg.EndPoint);
            }

            return new GeoPolylineArc3(edges);
        }

        /// <summary>
        /// Lengthens a curved chain along its end leg until the whole chain is a given length.
        /// </summary>
        public static GeoPolylineArc3 ExtendToLength(GeoPolylineArc3 chain, double length, LineEnd end) => ExtendToLength(chain, length, end, Tolerance.Global);

        /// <summary>
        /// Lengthens a curved chain along its end leg until the whole chain is a given length, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="length">The length the whole chain should end up with, measured along its arcs.</param>
        /// <param name="end">Which end to move.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The chain of that length.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is not longer than the chain already is.</exception>
        public static GeoPolylineArc3 ExtendToLength(GeoPolylineArc3 chain, double length, LineEnd end, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            RequireFinite(length, nameof(length));

            return Extend(chain, length - chain.Length, end, tolerance);
        }

        /// <summary>
        /// Shortens a curved chain at one end back to a point on it.
        /// </summary>
        public static bool TryTrimTo(GeoPolylineArc3 chain, GeoPoint3 point, LineEnd end, out GeoPolylineArc3 result)
            => TryTrimTo(chain, point, end, out result, Tolerance.Global);

        /// <summary>
        /// Shortens a curved chain at one end back to a point on it, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="point">The point to stop at; it has to lie on the chain.</param>
        /// <param name="end">Which end to give up.</param>
        /// <param name="result">The shortened chain, or the chain unchanged.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain was shortened; otherwise, false.</returns>
        /// <remarks>
        /// The cut keeps the arcs: a bend the cut falls inside comes back as two arcs of the same radius, and
        /// only the piece holding the other end is kept.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryTrimTo(GeoPolylineArc3 chain, GeoPoint3 point, LineEnd end, out GeoPolylineArc3 result, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            ValidateEnd(end);
            result = chain;

            if (!ArcChain3.TrySplitBy(chain, point, out GeoPolylineArc3[] pieces, tolerance) || pieces.Length < 2)
            {
                return false;
            }

            result = end == LineEnd.End ? pieces[0] : pieces[pieces.Length - 1];

            return true;
        }

        #endregion

        #region Working the sweep out

        /// <summary>
        /// Which way the arc travels: 1 counter-clockwise, -1 clockwise.
        /// </summary>
        private static double Direction(GeoArc3 arc) => arc.SweptAngle < 0.0 ? -1.0 : 1.0;

        /// <summary>
        /// The same arc with a new start angle and a new signed sweep.
        /// </summary>
        private static GeoArc3 Swept(GeoArc3 arc, double startAngle, double sweep)
            => new GeoArc3(arc.Center, arc.Normal, arc.Radius, startAngle, startAngle + sweep, sweep < 0.0);

        /// <summary>
        /// Determines whether a sweep still draws an arc: something is left of it, it has not passed a whole
        /// turn, and it has not turned back the other way.
        /// </summary>
        private static bool Holds(GeoArc3 arc, double sweep, Tolerance tolerance)
        {
            return Math.Abs(sweep) > tolerance.EqualAngleRad
                && Math.Abs(sweep) < FullTurn - tolerance.EqualAngleRad
                && (sweep < 0.0) == (arc.SweptAngle < 0.0);
        }

        /// <summary>
        /// Moves one end of an arc to leave the given sweep, keeping the other end where it is.
        /// </summary>
        private static bool TryReswept(GeoArc3 arc, LineEnd end, double sweep, Tolerance tolerance, out GeoArc3 result)
        {
            result = arc;

            if (!Holds(arc, sweep, tolerance))
            {
                return false;
            }

            result = end == LineEnd.End
                ? Swept(arc, arc.StartAngle, sweep)
                : Swept(arc, arc.StartAngle + arc.SweptAngle - sweep, sweep);

            return true;
        }

        /// <summary>
        /// The sweep the arc would have if the named end sat at a point, travelling the way the arc travels.
        /// </summary>
        private static double SweepTo(GeoArc3 arc, GeoPoint3 point, LineEnd end)
        {
            double direction = Direction(arc);
            double angle = AngleOf(arc, point);

            return end == LineEnd.End
                ? direction * ForwardSweep(arc.StartAngle, angle, direction)
                : direction * ForwardSweep(angle, arc.StartAngle + arc.SweptAngle, direction);
        }

        /// <summary>
        /// How far there is to travel from one angle to another the way the arc goes, from nought to a whole turn.
        /// </summary>
        private static double ForwardSweep(double from, double to, double direction)
            => WrapTurn(direction > 0.0 ? to - from : from - to);

        /// <summary>
        /// An angle brought into nought to a whole turn.
        /// </summary>
        private static double WrapTurn(double angle)
        {
            double wrapped = angle % FullTurn;

            return wrapped < 0.0 ? wrapped + FullTurn : wrapped;
        }

        /// <summary>
        /// Determines whether a point lies on the whole circle the arc is part of, reached or not.
        /// </summary>
        private static bool OnCircle(GeoArc3 arc, GeoPoint3 point, Tolerance tolerance)
        {
            // On the circle means in the arc's plane as well as at its radius: a point the same distance from
            // the centre but off the plane is on a sphere, not on this circle.
            return Containment3.IsPointOn(arc.GetPlane(), point, tolerance)
                && Math.Abs(arc.Center.DistanceTo(point) - arc.Radius) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// The angle of a point about the arc's centre, read the way the arc reads its own angles.
        /// </summary>
        private static double AngleOf(GeoArc3 arc, GeoPoint3 point)
        {
            // The arc measures its angles from the axes of its own plane, so the point is read in that frame
            // rather than against the world axes.
            GeoPoint2 local = PlanarMap.ProjectToPoint2(PlanarMap.FrameOf(arc.GetPlane()), point);

            return Math.Atan2(local.Y, local.X);
        }

        /// <summary>
        /// The tip of a leg carried on beyond itself by a distance.
        /// </summary>
        private static GeoPoint3 Carried(GeoPoint3 inner, GeoPoint3 tip, double distance, Tolerance tolerance)
        {
            GeoVector3 along = inner.GetVectorTo(tip);
            double length = along.Length;

            if (length <= tolerance.EqualPoint)
            {
                throw new ArgumentException("The end leg of the chain has no length to carry on along.", "chain");
            }

            return tip.Add(along.Multiply(distance / length));
        }

        /// <summary>
        /// A chain is carried outwards only; the cutting family shortens one, and keeps its bends doing it.
        /// </summary>
        private static void RequireOutwards(double distance, Tolerance tolerance)
        {
            RequireFinite(distance, "distance");

            if (distance <= tolerance.EqualPoint)
            {
                throw new ArgumentOutOfRangeException(
                    "distance",
                    "A chain is carried outwards only. Use the splitting family to shorten one, which keeps its bends.");
            }
        }

        #endregion
    }
}
