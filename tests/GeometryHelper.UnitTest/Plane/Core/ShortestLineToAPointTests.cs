using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Common;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The arc and the face could give the shortest line to a point; the ten other shapes of the plane could
    /// not, nor could the point give it back. Each now does, with and without a tolerance, and each is held
    /// to the rule the library writes down for the face and in Projection2: the line runs from the point of
    /// the boundary <c>GetClosestPointOnBoundary</c> gives to the point itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// So a region and its <c>DistanceTo</c> part company inside on purpose, as Face2.GetShortestLineTo and
    /// Face2's distance do: <c>DistanceTo</c> reads a closed shape as filled and gives nought there, while the
    /// line goes out to the rim and is as long as the depth, <c>|SignedDistanceTo|</c>. Outside, the line is
    /// as long as <c>DistanceTo</c>; on the rim it has no length; a curve is only ever its path.
    /// </para>
    /// <para>
    /// Lengths are held to 1E-12 of the shape's size of 100, not of the distance: just off a rim the two
    /// readings differ by rounding of the size. The point 0.4 outside a rim is inside the tolerance of 1, so
    /// under that tolerance only the ends of its line are checked, not which reading its length follows.
    /// </para>
    /// </remarks>
    public class ShortestLineToAPointTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        /// <summary>No tolerance at all: the overload without one.</summary>
        private static readonly Tolerance? None = null;

        /// <summary>1E-12 of the size of every shape here, about 100.</summary>
        private const double Slack = 1E-10;

        private static readonly List<string> Names = new List<string>();

        private static readonly Dictionary<string, Action> Checks = Build();

        private enum Where
        {
            Inside,
            On,
            Outside,
            JustOutside,
        }

        public static TheoryData<string> Rows()
        {
            var data = new TheoryData<string>();

            foreach (string name in Names)
            {
                data.Add(name);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void TheLineRunsFromTheNearestPointOfTheBoundaryToThePoint(string row)
        {
            Checks[row]();
        }

        private static Dictionary<string, Action> Build()
        {
            var checks = new Dictionary<string, Action>(StringComparer.Ordinal);

            // The circle of radius 30 about (50, 25). Its centre is as near every point of the rim; the
            // closest point there is the one at angle nought, and the line must start on that same point.
            Add(checks, "circle", new GeoCircle2(P(50, 25), 30), true,
                new[]
                {
                    ("at the centre, as near the whole rim", P(50, 25), Where.Inside),
                    ("inside, 15 from the rim", P(60, 25), Where.Inside),
                    ("on the rim", P(80, 25), Where.On),
                    ("40 outside", P(120, 25), Where.Outside),
                    ("0.4 outside", P(80.4, 25), Where.JustOutside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => t == null ? s.GetClosestPointOnBoundary(p) : Projection2.ProjectToCircle(s, p, t.Value),
                (s, p, t) => s.DistanceTo(p),
                (s, p, t) => t == null ? s.SignedDistanceTo(p) : s.SignedDistanceTo(p, t.Value),
                (s, p) => s.IsPointOn(p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            // The ellipse 60 by 30 about (50, 25). Its centre gives the end of the minor axis, (50, 55); a point
            // inside on the major axis is as near two points of the rim, and gives the one on the positive side.
            // The point asks no ellipse: an ellipse is asked from the ellipse.
            Add(checks, "ellipse", new GeoEllipse2(P(50, 25), new GeoVector2(1, 0), 60, 30), true,
                new[]
                {
                    ("at the centre, as near both ends of the minor axis", P(50, 25), Where.Inside),
                    ("inside on the major axis, as near two points of the rim", P(70, 25), Where.Inside),
                    ("on the rim at the end of the major axis", P(110, 25), Where.On),
                    ("on the rim at the end of the minor axis", P(50, 55), Where.On),
                    ("40 outside", P(150, 25), Where.Outside),
                    ("0.4 outside", P(110.4, 25), Where.JustOutside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => s.GetClosestPointOnBoundary(p),
                (s, p, t) => t == null ? s.DistanceTo(p) : s.DistanceTo(p, t.Value),
                (s, p, t) => t == null ? s.SignedDistanceTo(p) : s.SignedDistanceTo(p, t.Value),
                (s, p) => s.IsPointOn(p, Strict),
                null);

            // A half circle of radius 50 about (50, 0) over the top, from (100, 0) to (0, 0). Its centre is as
            // near every point of it.
            Add(checks, "curved edge", new GeoEdge2(new GeoArc2(P(50, 0), 50, 0, Math.PI)), false,
                new[]
                {
                    ("at the centre, as near the whole arc", P(50, 0), Where.Inside),
                    // Within the tolerance of 1 of the centre the edge reads the point as at it, and gives the
                    // end (100, 0) rather than the top (50, 50): the line has to follow the tolerance it is given.
                    ("0.4 above the centre", P(50, 0.4), Where.Inside),
                    ("on the arc", P(50, 50), Where.On),
                    ("30 outside the arc", P(50, 80), Where.Outside),
                    ("0.4 outside the arc", P(50, 50.4), Where.JustOutside),
                    ("below an end, nearest that end", P(0, -30), Where.Outside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => t == null ? s.GetClosestPointOnBoundary(p) : s.GetClosestPointOnBoundary(p, t.Value),
                (s, p, t) => t == null ? s.DistanceTo(p) : s.DistanceTo(p, t.Value),
                null,
                (s, p) => s.IsPointOn(p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            Add(checks, "straight edge", new GeoEdge2(P(0, 0), P(100, 0)), false,
                new[]
                {
                    ("on it", P(30, 0), Where.On),
                    ("20 beside it", P(30, 20), Where.Outside),
                    ("0.4 beside it", P(30, -0.4), Where.JustOutside),
                    ("beyond an end", P(130, 40), Where.Outside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => t == null ? s.GetClosestPointOnBoundary(p) : s.GetClosestPointOnBoundary(p, t.Value),
                (s, p, t) => t == null ? s.DistanceTo(p) : s.DistanceTo(p, t.Value),
                null,
                (s, p) => s.IsPointOn(p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            Add(checks, "segment", new GeoLine2(P(0, 0), P(100, 50)), false,
                new[]
                {
                    ("on it", P(50, 25), Where.On),
                    ("on its end", P(100, 50), Where.On),
                    ("beside it", P(40, 60), Where.Outside),
                    ("0.4 beside it", P(50 - 0.4 * 0.2 * Math.Sqrt(5), 25 + 0.4 * 0.4 * Math.Sqrt(5)), Where.JustOutside),
                    ("beyond an end", P(130, 50), Where.Outside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => t == null ? s.GetClosestPointOnBoundary(p) : Projection2.ProjectToLine(s, p, t.Value),
                (s, p, t) => s.DistanceTo(p),
                null,
                (s, p) => s.IsPointOn(p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            // A U from (0, 0) to (100, 60) with a notch from (30, 20) to (70, 60) cut down into its top. The
            // point (15, 30) is 15 from both the outer side and the notch's side; (50, 50) in the notch is 20
            // from both its sides.
            Add(checks, "polygon", Notched(), true,
                new[]
                {
                    ("inside, as near two sides", P(15, 30), Where.Inside),
                    ("on a side", P(50, 0), Where.On),
                    ("on the notch's inner corner", P(70, 20), Where.On),
                    ("in the notch, as near both its sides", P(50, 50), Where.Outside),
                    ("10 below", P(50, -10), Where.Outside),
                    ("0.4 below", P(50, -0.4), Where.JustOutside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => t == null ? s.GetClosestPointOnBoundary(p) : Projection2.ProjectToPolygon(s, p, t.Value),
                (s, p, t) => s.DistanceTo(p),
                (s, p, t) => t == null ? s.SignedDistanceTo(p) : s.SignedDistanceTo(p, t.Value),
                (s, p) => Containment2.IsPointOn(s, p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            // The same U with its bottom bulging down to (50, -15).
            Add(checks, "curved loop", new GeoPolygonArc2(Notched().Vertices, new[] { 0.3 }), true,
                new[]
                {
                    ("inside, as near two sides", P(15, 30), Where.Inside),
                    ("on the bulge", P(50, -15), Where.On),
                    ("on the notch's inner corner", P(70, 20), Where.On),
                    ("in the notch, as near both its sides", P(50, 50), Where.Outside),
                    ("25 below the bulge", P(50, -40), Where.Outside),
                    ("0.4 below the bulge", P(50, -15.4), Where.JustOutside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => t == null ? s.GetClosestPointOnBoundary(p) : s.GetClosestPointOnBoundary(p, t.Value),
                (s, p, t) => t == null ? s.DistanceTo(p) : s.DistanceTo(p, t.Value),
                (s, p, t) => t == null ? s.SignedDistanceTo(p) : s.SignedDistanceTo(p, t.Value),
                (s, p) => s.IsPointOn(p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            // A V from (0, 0) up to (50, 50) and down to (100, 0); (50, 10) under its point is as near both legs.
            Add(checks, "polyline", new GeoPolyline2(P(0, 0), P(50, 50), P(100, 0)), false,
                new[]
                {
                    ("on a leg", P(25, 25), Where.On),
                    ("on its corner", P(50, 50), Where.On),
                    ("under the corner, as near both legs", P(50, 10), Where.Outside),
                    ("above the corner", P(50, 80), Where.Outside),
                    ("0.4 above the corner", P(50, 50.4), Where.JustOutside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => t == null ? s.GetClosestPointOnBoundary(p) : Projection2.ProjectToPolyline(s, p, t.Value),
                (s, p, t) => s.DistanceTo(p),
                null,
                (s, p) => s.IsPointOn(p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            // A half circle of radius 50 below (50, 0) from (0, 0) to (100, 0), then straight on to (150, 0).
            // The centre (50, 0) is as near the whole arc, and as near the straight piece's start.
            Add(checks, "curved chain", new GeoPolylineArc2(new[] { P(0, 0), P(100, 0), P(150, 0) }, new[] { 1.0, 0.0 }), false,
                new[]
                {
                    ("at the arc's centre, as near the whole arc", P(50, 0), Where.Inside),
                    // As for the edge: within the tolerance of 1 of the centre the chain gives the end (0, 0).
                    ("0.4 below the arc's centre", P(50, -0.4), Where.Inside),
                    ("on the arc", P(50, -50), Where.On),
                    ("on the straight piece", P(125, 0), Where.On),
                    ("30 below the arc", P(50, -80), Where.Outside),
                    ("0.4 below the arc", P(50, -50.4), Where.JustOutside),
                    ("10 above the straight piece", P(125, 10), Where.Outside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => t == null ? s.GetClosestPointOnBoundary(p) : s.GetClosestPointOnBoundary(p, t.Value),
                (s, p, t) => t == null ? s.DistanceTo(p) : s.DistanceTo(p, t.Value),
                null,
                (s, p) => s.IsPointOn(p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            // The rectangle 100 by 60 about (50, 30). Its centre is 30 from both the top and the bottom.
            Add(checks, "rectangle", new GeoRectangle2(P(50, 30), 100, 60), true,
                new[]
                {
                    ("at the centre, as near the top and the bottom", P(50, 30), Where.Inside),
                    ("inside, 20 from a side", P(20, 30), Where.Inside),
                    ("on a side", P(50, 0), Where.On),
                    ("on a corner", P(100, 60), Where.On),
                    ("30 outside a side", P(130, 30), Where.Outside),
                    ("outside a corner", P(110, 70), Where.Outside),
                    ("0.4 outside a side", P(100.4, 30), Where.JustOutside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => s.GetClosestPointOnBoundary(p),
                (s, p, t) => s.DistanceTo(p),
                (s, p, t) => t == null ? s.SignedDistanceTo(p) : s.SignedDistanceTo(p, t.Value),
                (s, p) => Containment2.IsPointOn(s.ToPolygon(), p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            // The triangle (0, 0), (100, 0), (0, 100). The point (20, 20) is 20 from both short sides.
            Add(checks, "triangle", new GeoTriangle2(P(0, 0), P(100, 0), P(0, 100)), true,
                new[]
                {
                    ("inside, as near both short sides", P(20, 20), Where.Inside),
                    ("on a side", P(50, 0), Where.On),
                    ("on a corner", P(100, 0), Where.On),
                    ("outside the long side", P(60, 60), Where.Outside),
                    ("outside a corner", P(110, -10), Where.Outside),
                    ("0.4 outside a side", P(50, -0.4), Where.JustOutside),
                },
                (s, p, t) => t == null ? s.GetShortestLineTo(p) : s.GetShortestLineTo(p, t.Value),
                (s, p, t) => s.GetClosestPointOnBoundary(p),
                (s, p, t) => s.DistanceTo(p),
                (s, p, t) => t == null ? s.SignedDistanceTo(p) : s.SignedDistanceTo(p, t.Value),
                (s, p) => Containment2.IsPointOn(s.ToPolygon(), p, Strict),
                (s, p, t) => t == null ? p.GetShortestLineTo(s) : p.GetShortestLineTo(s, t.Value));

            return checks;
        }

        /// <summary>
        /// Adds one row per point. Each quantity is read through one function taking the tolerance, null for
        /// the overload without one; a shape whose closest point or distance takes no tolerance reads none.
        /// </summary>
        /// <param name="isRegion">Whether the shape is closed, so that a point can be inside it.</param>
        /// <param name="depth">The signed distance of a region; null for a curve.</param>
        /// <param name="fromPoint">The point's line to the shape; null where the point asks no such shape.</param>
        private static void Add<T>(
            Dictionary<string, Action> checks,
            string shapeName,
            T shape,
            bool isRegion,
            (string Case, GeoPoint2 Point, Where Where)[] points,
            Func<T, GeoPoint2, Tolerance?, GeoLine2> line,
            Func<T, GeoPoint2, Tolerance?, GeoPoint2> closest,
            Func<T, GeoPoint2, Tolerance?, double> distance,
            Func<T, GeoPoint2, Tolerance?, double> depth,
            Func<T, GeoPoint2, bool> onBoundary,
            Func<T, GeoPoint2, Tolerance?, GeoLine2> fromPoint)
        {
            foreach ((string Case, GeoPoint2 Point, Where Where) c in points)
            {
                string name = typeof(T).Name + " (" + shapeName + "): " + c.Case;

                Names.Add(name);
                checks.Add(name, () =>
                {
                    foreach ((Tolerance? tolerance, bool loose) in new[] { (None, false), ((Tolerance?)Strict, false), ((Tolerance?)Loose, true) })
                    {
                        GeoLine2 found = line(shape, c.Point, tolerance);

                        // It lands on the point and leaves the boundary where the type's own closest point is.
                        MirrorTable.Same(c.Point, found.EndPoint);
                        MirrorTable.Same(closest(shape, c.Point, tolerance), found.StartPoint);
                        Assert.True(onBoundary(shape, found.StartPoint), name + ": the start is off the shape");

                        // Within the tolerance of 1 the point 0.4 out is as good as on the rim, so which reading
                        // its length follows is the tolerance's to decide, and not checked here.
                        if (!(c.Where == Where.JustOutside && loose))
                        {
                            Assert.Equal(Expected(c.Where, isRegion, distance(shape, c.Point, tolerance), depth?.Invoke(shape, c.Point, tolerance)), found.Length, Slack);
                        }

                        if (fromPoint != null)
                        {
                            MirrorTable.Same(found.Reverse(), fromPoint(shape, c.Point, tolerance));
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Gets the length the line must have: none on the rim, the depth inside a region, and the distance
        /// everywhere else, which for a curve is the distance to its path.
        /// </summary>
        private static double Expected(Where where, bool isRegion, double distance, double? depth)
        {
            if (where == Where.On)
            {
                return 0.0;
            }

            if (where == Where.Inside && isRegion)
            {
                return Math.Abs(depth.Value);
            }

            return distance;
        }

        #region A notched outline

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Notched() => new GeoPolygon2(
            P(0, 0), P(100, 0), P(100, 60), P(70, 60), P(70, 20), P(30, 20), P(30, 60), P(0, 60));

        [Fact]
        public void ATriangleIsReadByItsSidesForTheShortestLineToAPointWhateverTheTolerance()
        {
            // A sliver 100 long and 0.5 high, and a point 1 past its tip. Its nearest point is on its short side, (100, 0.25),
            // at any tolerance. At a tolerance of 1 the sliver used to be read as the segment of its hull, whose end
            // (100, 0.5) was the nearest point; before that, the form taking a tolerance read the process-wide one instead.
            // The triangle is now read by its own sides, however thin, and no tolerance decides it.
            var sliver = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 0.5));
            var point = new GeoPoint2(101, 0.25);
            var fine = new Tolerance(1E-3, 1E-5);
            var coarse = new Tolerance(1.0, 1E-5);

            Assert.True(sliver.GetShortestLineTo(point, fine).StartPoint.IsEqualTo(new GeoPoint2(100, 0.25), new Tolerance(1E-9, 1E-9)));
            Assert.True(sliver.GetShortestLineTo(point, coarse).StartPoint.IsEqualTo(new GeoPoint2(100, 0.25), new Tolerance(1E-9, 1E-9)));
            Assert.True(point.GetShortestLineTo(sliver, coarse).EndPoint.IsEqualTo(new GeoPoint2(100, 0.25), new Tolerance(1E-9, 1E-9)));
        }

        #endregion
    }
}
