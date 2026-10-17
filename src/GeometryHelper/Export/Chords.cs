using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;

namespace GeometryHelper.Export
{
    /// <summary>
    /// How the writers that have no curves cut a curve into chords, and what they refuse.
    /// <para>
    /// The library's own cuts stop at 4 096 pieces a turn, which is short of a tolerance on a large radius: a circle of
    /// radius 50 000 cut within 0.01 strays 0.0147. The counts worked out here are the library's wherever it meets the
    /// tolerance, and more where it does not, up to <see cref="Most"/> for one curve.
    /// </para>
    /// <para>
    /// The points are placed by the same arithmetic as the library's <c>ToPolyline(int)</c>, but are not handed to a
    /// <see cref="GeoPolyline2"/> or <see cref="GeoPolygon2"/>, whose constructors drop a point within the point tolerance of
    /// the one before: a circle of radius 1 cut within 1E-8 has chords of 1.3E-4, and every one of them is kept here.
    /// </para>
    /// </summary>
    internal static class Chords
    {
        /// <summary>The most chords one curve is cut into; a tolerance that needs more is refused.</summary>
        internal const int Most = 1048576;

        /// <summary>Refuses a chord tolerance that is not a finite number above nought.</summary>
        internal static void Require(double chordTolerance)
            => Guard.Positive(chordTolerance, nameof(chordTolerance), "A chord tolerance has to be a finite number above nought.");

        /// <summary>Refuses a ray with no direction, as a <c>default</c> one has.</summary>
        internal static void Require(GeoRay2 ray)
        {
            double length = ray.Direction.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A ray needs a direction of non-zero length.", nameof(ray));
            }
        }

        /// <summary>Refuses a ray with no direction, as a <c>default</c> one has.</summary>
        internal static void Require(GeoRay3 ray)
        {
            double length = ray.Direction.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A ray needs a direction of non-zero length.", nameof(ray));
            }
        }

        /// <summary>The chords of an arc, from its start to its end, both exactly.</summary>
        internal static List<GeoPoint3> Of(GeoArc3 arc, double chordTolerance)
            => Along(arc, ForArc(arc.Radius, Math.Abs(arc.SweptAngle), chordTolerance, 1));

        /// <summary>The chords of an arc, from its start to its end, both exactly.</summary>
        internal static List<GeoPoint2> Of(GeoArc2 arc, double chordTolerance)
            => Along(arc, ForArc(arc.Radius, Math.Abs(arc.SweptAngle), chordTolerance, 1));

        /// <summary>The chords of a circle all the way round, the first point repeated at the end.</summary>
        internal static List<GeoPoint3> Of(GeoCircle3 circle, double chordTolerance)
        {
            int count = ForArc(circle.Radius, Math.PI * 2.0, chordTolerance, 3);
            var points = new List<GeoPoint3>(count + 1);

            for (int i = 0; i < count; i++)
            {
                points.Add(circle.GetPointAtParameter((double)i / count));
            }

            points.Add(points[0]);
            return points;
        }

        /// <summary>The corners of a circle all the way round, counter-clockwise, the first not repeated.</summary>
        internal static List<GeoPoint2> Of(GeoCircle2 circle, double chordTolerance)
        {
            int count = ForArc(circle.Radius, Math.PI * 2.0, chordTolerance, 3);
            var points = new List<GeoPoint2>(count);

            for (int i = 0; i < count; i++)
            {
                points.Add(circle.GetPointAtParameter((double)i / count));
            }

            return points;
        }

        /// <summary>
        /// The chords of an ellipse all the way round, the first point repeated at the end: the library's, spaced by the bend
        /// of the rim, while it has room for them and its chords are not shorter than the point tolerance can tell apart, and
        /// an even spread in the angle past that.
        /// </summary>
        internal static List<GeoPoint3> Of(GeoEllipse3 ellipse, double chordTolerance)
        {
            if (!Fine(ellipse.MajorRadius, ellipse.MinorRadius, chordTolerance))
            {
                GeoPolyline3 library = ellipse.ToPolylineByChordTolerance(chordTolerance);

                if (library.VertexCount - 1 < Tessellation.MaxSegmentsPerTurn)
                {
                    return new List<GeoPoint3>(library.Vertices);
                }
            }

            int count = EvenSpread(ellipse.MajorRadius, chordTolerance);
            var points = new List<GeoPoint3>(count + 1);

            for (int i = 0; i < count; i++)
            {
                points.Add(ellipse.GetPointAtParameter((double)i / count));
            }

            points.Add(points[0]);
            return points;
        }

        /// <summary>The corners of an ellipse all the way round, counter-clockwise, the first not repeated, as for the one in space.</summary>
        internal static List<GeoPoint2> Of(GeoEllipse2 ellipse, double chordTolerance)
        {
            if (!Fine(ellipse.MajorRadius, ellipse.MinorRadius, chordTolerance))
            {
                GeoPolygon2 library = ellipse.ToPolygonByChordTolerance(chordTolerance);

                if (library.VertexCount < Tessellation.MaxSegmentsPerTurn)
                {
                    return new List<GeoPoint2>(library.Vertices);
                }
            }

            int count = EvenSpread(ellipse.MajorRadius, chordTolerance);
            var points = new List<GeoPoint2>(count);

            for (int i = 0; i < count; i++)
            {
                points.Add(ellipse.GetPointAtParameter((double)i / count));
            }

            return points;
        }

        /// <summary>
        /// The points of a chain, each arc cut into chords, from the start of the first edge to the end of the last: the
        /// points <see cref="GeoPolylineArc3.ToPolyline3(double)"/> gives wherever the library's cut meets the tolerance, but that the
        /// end of an arc is the edge's own end point, and an arc too shallow to place a circle for is read as its chord.
        /// </summary>
        internal static List<GeoPoint3> Of(GeoPolylineArc3 chain, double chordTolerance)
        {
            var points = new List<GeoPoint3> { chain.GetEdgeAt(0).StartPoint };
            long total = 0;

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                GeoEdge3 edge = chain.GetEdgeAt(i);

                if (!edge.IsArc)
                {
                    points.Add(edge.EndPoint);
                    continue;
                }

                GeoArc3 arc = edge.ToArc();

                // An arc too shallow to place a circle for is read by its chord, as a straight edge is.
                if (!Placed(arc.Radius, arc.SweptAngle, arc.Center.X, arc.Center.Y, arc.Center.Z))
                {
                    points.Add(edge.EndPoint);
                    continue;
                }

                int count = ForArc(arc.Radius, Math.Abs(arc.SweptAngle), chordTolerance, 1);
                Add(ref total, count, chordTolerance);

                for (int j = 1; j < count; j++)
                {
                    points.Add(arc.GetPointAtParameter((double)j / count));
                }

                // The edge ends where the next begins, however the circle's angle rounds.
                points.Add(edge.EndPoint);
            }

            return points;
        }

        /// <summary>
        /// The points of a loop in space, each arc cut into chords, ending exactly where it began, which the end of the last
        /// arc, worked out from its angle, may miss by rounding.
        /// </summary>
        internal static List<GeoPoint3> Of(GeoPolygonArc3 loop, double chordTolerance)
        {
            GeoPolylineArc3 chain = loop.ToPolylineArc3();
            List<GeoPoint3> points = Of(chain, chordTolerance);
            points[points.Count - 1] = chain.GetEdgeAt(0).StartPoint;
            return points;
        }

        /// <summary>
        /// The points of a chain in the plane, each arc cut into chords, from the start of the first edge to the end of the
        /// last.
        /// </summary>
        internal static List<GeoPoint2> Of(IEnumerable<GeoEdge2> edges, double chordTolerance)
        {
            var points = new List<GeoPoint2>();
            long total = 0;

            foreach (GeoEdge2 edge in edges)
            {
                if (points.Count == 0)
                {
                    points.Add(edge.StartPoint);
                }

                if (!edge.IsArc)
                {
                    points.Add(edge.EndPoint);
                    continue;
                }

                GeoArc2 arc = edge.ToArc();

                // An arc too shallow to place a circle for is read by its chord, as a straight edge is.
                if (!Placed(arc.Radius, arc.SweptAngle, arc.Center.X, arc.Center.Y, 0.0))
                {
                    points.Add(edge.EndPoint);
                    continue;
                }

                int count = ForArc(arc.Radius, Math.Abs(arc.SweptAngle), chordTolerance, 1);
                Add(ref total, count, chordTolerance);

                for (int j = 1; j < count; j++)
                {
                    points.Add(arc.GetPointAtParameter((double)j / count));
                }

                // The edge ends where the next begins, however the circle's angle rounds.
                points.Add(edge.EndPoint);
            }

            return points;
        }

        /// <summary>
        /// Whether an arc's circle can be placed: a radius, a sweep and a centre that are numbers. The arc of a bulge of 1E-200
        /// has a radius of 1E202 and a centre that is not one.
        /// </summary>
        private static bool Placed(double radius, double sweep, double x, double y, double z)
            => Guard.IsFinite(radius) && Guard.IsFinite(sweep) && Guard.IsFinite(x) && Guard.IsFinite(y) && Guard.IsFinite(z);

        private static List<GeoPoint3> Along(GeoArc3 arc, int count)
        {
            var points = new List<GeoPoint3>(count + 1);

            for (int i = 0; i <= count; i++)
            {
                points.Add(arc.GetPointAtParameter((double)i / count));
            }

            return points;
        }

        private static List<GeoPoint2> Along(GeoArc2 arc, int count)
        {
            var points = new List<GeoPoint2>(count + 1);

            for (int i = 0; i <= count; i++)
            {
                points.Add(arc.GetPointAtParameter((double)i / count));
            }

            return points;
        }

        /// <summary>
        /// Whether the chords of the library's cut of an ellipse may be shorter than the point tolerance, so that it would drop
        /// some of them: its shortest are at the ends of the major axis, where the rim bends most, as long as the root of eight
        /// times the radius of that bend and the tolerance.
        /// </summary>
        private static bool Fine(double majorRadius, double minorRadius, double chordTolerance)
            => 2.0 * Math.Sqrt(2.0 * (minorRadius * minorRadius / majorRadius) * chordTolerance) < 10.0 * Tolerance.Global.EqualPoint;

        /// <summary>
        /// How many chords a circular arc is cut into so that none strays further than the tolerance: its sweep over the
        /// angle a chord may span, rounded up. The library's count is the least it gives, since it is right wherever its cap
        /// does not come first; the exact one is worked out from the arc sine, which keeps its digits at a tolerance far below
        /// the radius.
        /// </summary>
        private static int ForArc(double radius, double sweep, double chordTolerance, int fewest)
        {
            double share = chordTolerance / (2.0 * radius);
            double perChord = share >= 1.0 ? Math.PI * 2.0 : 4.0 * Math.Asin(Math.Sqrt(share));
            double exact = Math.Ceiling(sweep / perChord);

            if (exact > Most)
            {
                throw TooMany(chordTolerance);
            }

            int library = Tessellation.SegmentsForChordTolerance(radius, sweep, chordTolerance);
            return Math.Max(Math.Max(library, (int)exact), fewest);
        }

        /// <summary>
        /// How many chords of an even spread in the eccentric angle keep an ellipse within a tolerance: a chord over the angle
        /// h strays from the rim by at most h² / 8 of the largest acceleration along it, which is the major radius.
        /// </summary>
        private static int EvenSpread(double majorRadius, double chordTolerance)
        {
            double count = Math.Ceiling(Math.PI * 2.0 / Math.Sqrt(8.0 * chordTolerance / majorRadius));

            if (count > Most)
            {
                throw TooMany(chordTolerance);
            }

            return Math.Max((int)count, 3);
        }

        /// <summary>Adds the chords of one more curve to a chain's total, and refuses a total past <see cref="Most"/>.</summary>
        internal static void Add(ref long total, int count, double chordTolerance)
        {
            total += count;

            if (total > Most)
            {
                throw TooMany(chordTolerance);
            }
        }

        private static ArgumentOutOfRangeException TooMany(double chordTolerance)
            => new ArgumentOutOfRangeException(
                nameof(chordTolerance),
                chordTolerance,
                "A chord tolerance of " + chordTolerance.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                + " would cut the curve into more than " + Most + " chords; ask for a larger one.");
    }
}
