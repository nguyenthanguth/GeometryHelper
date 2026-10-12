using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.Ray2Shapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Every shape that asks a segment the five queries asks a ray the same ones (D5), and every query of the ray has a
    /// form without a tolerance and one with (D6). This holds each pair to both: asked from either side, the same
    /// collision, the same distance, the same crossings as a set, and the shortest line turned round; and each form
    /// without a tolerance answering, to the bit, what the form with <see cref="Tolerance.Global"/> answers.
    /// </summary>
    /// <remarks>
    /// The cases are a table, one row per shape and case, run under a tolerance of a thousandth and of 1. The rays are
    /// those of <see cref="Ray2AgainstShapesTests"/>: through the shape, beside it, starting past it and pointing away,
    /// starting inside or on it, and passing four tenths from a corner. Under the thousandth the shortest line is also as
    /// long as the distance and the collision is the distance being within the tolerance. Where a ray crosses a boundary
    /// more than once the shortest line may sit at any of the crossings, so there either side's is only held to having no
    /// length; where it crosses once or not at all, the line from the shape is the line from the ray turned round. The
    /// shortest line is held to that under the thousandth only: within a tolerance of 1 a segment touching a circle's
    /// tangent band is given a line of no length from one side and the gap from the other, as the segment's own two
    /// sides give it. The crossings of the two sides are held to the same set within the point tolerance, not closer: a
    /// crossing accepted within the tolerance of an end is placed on the side asking, as the segment's is.
    /// </remarks>
    public class Ray2FromEitherSideTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static readonly List<string> Names = new List<string>();

        private static readonly Dictionary<string, Action> Checks = Build();

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
        public void TheShapeAsksTheRayWhatTheRayAsksTheShape_AndTheFormWithoutAToleranceIsTheGlobalOne(string row)
        {
            Checks[row]();
        }

        #region The table

        /// <summary>One side's five queries, in both forms; a query D4 or D5 does not offer stays null.</summary>
        private sealed class Side<TA, TB>
        {
            internal Func<TA, TB, Tolerance, bool> Collides;
            internal Func<TA, TB, bool> CollidesPlain;
            internal Func<TA, TB, Tolerance, double> Distance;
            internal Func<TA, TB, double> DistancePlain;
            internal Func<TA, TB, Tolerance, GeoPoint2[]> Crossings;
            internal Func<TA, TB, GeoPoint2[]> CrossingsPlain;
            internal Func<TA, TB, Tolerance, (bool, GeoPoint2[])> Tried;
            internal Func<TA, TB, (bool, GeoPoint2[])> TriedPlain;
            internal Func<TA, TB, Tolerance, GeoLine2> Shortest;
            internal Func<TA, TB, GeoLine2> ShortestPlain;
        }

        private static (bool, GeoPoint2[]) One(bool hit, GeoPoint2 point) => (hit, hit ? new[] { point } : new GeoPoint2[0]);

        private static Dictionary<string, Action> Build()
        {
            var checks = new Dictionary<string, Action>(StringComparer.Ordinal);

            Add(checks, "Arc2", new[]
            {
                ("through", new GeoRay2(P(-100, 30), GeoVector2.XAxis), Arc(), true),
                ("beside", new GeoRay2(P(-100, -30), GeoVector2.XAxis), Arc(), false),
                ("behind", new GeoRay2(P(100, 0), GeoVector2.XAxis), Arc(), false),
                ("from the centre", new GeoRay2(P(0, 0), GeoVector2.YAxis), Arc(), false),
                ("bulging past its ends", new GeoRay2(P(-100, 0), GeoVector2.XAxis), RightHalf(), false),
                ("four tenths above", new GeoRay2(P(-100, 50.4), GeoVector2.XAxis), Arc(), false),
            },
            new Side<GeoRay2, GeoArc2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Tried = (r, s, t) => (r.TryIntersectWith(s, out GeoPoint2[] p, t), p), TriedPlain = (r, s) => (r.TryIntersectWith(s, out GeoPoint2[] p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoArc2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Tried = (s, r, t) => (s.TryIntersectWith(r, out GeoPoint2[] p, t), p), TriedPlain = (s, r) => (s.TryIntersectWith(r, out GeoPoint2[] p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Circle2", new[]
            {
                ("through", new GeoRay2(P(-100, 30), GeoVector2.XAxis), Circle(), true),
                ("beside", new GeoRay2(P(-100, 80), GeoVector2.XAxis), Circle(), false),
                ("behind", new GeoRay2(P(100, 0), GeoVector2.XAxis), Circle(), false),
                ("from the centre", new GeoRay2(P(0, 0), GeoVector2.XAxis), Circle(), false),
                ("a thousand along", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoCircle2(P(1000, 0), 50), true),
                ("four tenths above", new GeoRay2(P(-100, 50.4), GeoVector2.XAxis), Circle(), false),
            },
            new Side<GeoRay2, GeoCircle2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Tried = (r, s, t) => (r.TryIntersectWith(s, out GeoPoint2[] p, t), p), TriedPlain = (r, s) => (r.TryIntersectWith(s, out GeoPoint2[] p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoCircle2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Tried = (s, r, t) => (s.TryIntersectWith(r, out GeoPoint2[] p, t), p), TriedPlain = (s, r) => (s.TryIntersectWith(r, out GeoPoint2[] p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Edge2", new[]
            {
                ("straight, across", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoEdge2(P(50, -10), P(50, 10)), false),
                ("straight, beside", new GeoRay2(P(0, 20), GeoVector2.XAxis), new GeoEdge2(P(50, -10), P(50, 10)), false),
                ("straight, behind", new GeoRay2(P(60, 0), GeoVector2.XAxis), new GeoEdge2(P(50, -10), P(50, 10)), false),
                ("straight, four tenths past its end", new GeoRay2(P(0, 10.4), GeoVector2.XAxis), new GeoEdge2(P(50, -10), P(50, 10)), false),
                ("curved, through", new GeoRay2(P(-100, 30), GeoVector2.XAxis), new GeoEdge2(Arc()), true),
                ("curved, beside", new GeoRay2(P(-100, -30), GeoVector2.XAxis), new GeoEdge2(Arc()), false),
                ("curved, bulging past its ends", new GeoRay2(P(-100, 0), GeoVector2.XAxis), new GeoEdge2(RightHalf()), false),
            },
            new Side<GeoRay2, GeoEdge2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoEdge2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Face2", new[]
            {
                ("through", new GeoRay2(P(-50, 50), GeoVector2.XAxis), Plate(), true),
                ("beside", new GeoRay2(P(220, 0), V(-1, 1)), Plate(), false),
                ("behind", new GeoRay2(P(150, 50), GeoVector2.XAxis), Plate(), false),
                ("from the material", new GeoRay2(P(10, 50), GeoVector2.XAxis), Plate(), true),
                ("from the hole", new GeoRay2(P(50, 50), GeoVector2.XAxis), Plate(), true),
                ("four tenths past a corner", Grazing(P(100, 100), U(1, -1), U(1, 1), out _), Plate(), false),
            },
            new Side<GeoRay2, GeoFace2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Tried = (r, s, t) => (r.TryIntersectWith(s, out GeoPoint2[] p, t), p), TriedPlain = (r, s) => (r.TryIntersectWith(s, out GeoPoint2[] p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoFace2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Tried = (s, r, t) => (s.TryIntersectWith(r, out GeoPoint2[] p, t), p), TriedPlain = (s, r) => (s.TryIntersectWith(r, out GeoPoint2[] p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Line2", new[]
            {
                ("across", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoLine2(P(50, -10), P(50, 10)), false),
                ("beside", new GeoRay2(P(0, 20), GeoVector2.XAxis), new GeoLine2(P(50, -10), P(50, 10)), false),
                ("behind", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoLine2(P(-30, -10), P(-30, 10)), false),
                ("ending on it", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoLine2(P(50, 0), P(50, 10)), false),
                ("two thousandths behind the origin", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoLine2(P(-0.002, -10), P(-0.002, 10)), false),
                ("parallel five above", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoLine2(P(10, 5), P(60, 5)), false),
                ("far out at a shallow angle", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoLine2(P(0, 5), P(2000, -5)), false),
            },
            new Side<GeoRay2, GeoLine2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Tried = (r, s, t) => One(r.TryIntersectWith(s, out GeoPoint2 p, t), p), TriedPlain = (r, s) => One(r.TryIntersectWith(s, out GeoPoint2 p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoLine2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Tried = (s, r, t) => One(s.TryIntersectWith(r, out GeoPoint2 p, t), p), TriedPlain = (s, r) => One(s.TryIntersectWith(r, out GeoPoint2 p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Polygon2", new[]
            {
                ("through", new GeoRay2(P(-50, 50), GeoVector2.XAxis), Square(), true),
                ("beside", new GeoRay2(P(220, 0), V(-1, 1)), Square(), false),
                ("behind", new GeoRay2(P(150, 50), GeoVector2.XAxis), Square(), false),
                ("from inside", new GeoRay2(P(30, 40), GeoVector2.XAxis), Square(), false),
                ("four tenths past a corner", Grazing(P(100, 100), U(1, -1), U(1, 1), out _), Square(), false),
            },
            new Side<GeoRay2, GeoPolygon2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoPolygon2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "PolygonArc2", new[]
            {
                ("along it", new GeoRay2(P(-100, 25), GeoVector2.XAxis), Slot(), true),
                ("beside its end", new GeoRay2(P(260, -100), GeoVector2.YAxis), Slot(), false),
                ("behind", new GeoRay2(P(300, 25), GeoVector2.XAxis), Slot(), false),
                ("from inside", new GeoRay2(P(100, 25), GeoVector2.YAxis), Slot(), false),
                ("four tenths past its end", new GeoRay2(P(225.4, -100), GeoVector2.YAxis), Slot(), false),
            },
            new Side<GeoRay2, GeoPolygonArc2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Tried = (r, s, t) => (r.TryIntersectWith(s, out GeoPoint2[] p, t), p), TriedPlain = (r, s) => (r.TryIntersectWith(s, out GeoPoint2[] p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoPolygonArc2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Tried = (s, r, t) => (s.TryIntersectWith(r, out GeoPoint2[] p, t), p), TriedPlain = (s, r) => (s.TryIntersectWith(r, out GeoPoint2[] p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Polyline2", new[]
            {
                ("across the corner", new GeoRay2(P(20, -50), V(1, 1)), Ell(), true),
                ("past the outer corner", new GeoRay2(P(150, -100), V(1, 1)), Ell(), false),
                ("behind", new GeoRay2(P(-30, 50), -GeoVector2.XAxis), Ell(), false),
                ("from a point of it", new GeoRay2(P(50, 0), GeoVector2.YAxis), Ell(), false),
                ("four tenths past the outer corner", Grazing(P(100, 0), U(1, 1), U(1, -1), out _), Ell(), false),
            },
            new Side<GeoRay2, GeoPolyline2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Tried = (r, s, t) => (r.TryIntersectWith(s, out GeoPoint2[] p, t), p), TriedPlain = (r, s) => (r.TryIntersectWith(s, out GeoPoint2[] p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoPolyline2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Tried = (s, r, t) => (s.TryIntersectWith(r, out GeoPoint2[] p, t), p), TriedPlain = (s, r) => (s.TryIntersectWith(r, out GeoPoint2[] p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "PolylineArc2", new[]
            {
                ("through the tip", new GeoRay2(P(-100, 50), GeoVector2.XAxis), Hook(), false),
                ("beside the tip", new GeoRay2(P(200, -100), GeoVector2.YAxis), Hook(), false),
                ("behind", new GeoRay2(P(-30, -40), -GeoVector2.XAxis), Hook(), false),
                ("from the centre of its half circle", new GeoRay2(P(100, 50), GeoVector2.XAxis), Hook(), false),
                ("four tenths past the tip", new GeoRay2(P(150.4, -100), GeoVector2.YAxis), Hook(), false),
            },
            new Side<GeoRay2, GeoPolylineArc2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Tried = (r, s, t) => (r.TryIntersectWith(s, out GeoPoint2[] p, t), p), TriedPlain = (r, s) => (r.TryIntersectWith(s, out GeoPoint2[] p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoPolylineArc2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Tried = (s, r, t) => (s.TryIntersectWith(r, out GeoPoint2[] p, t), p), TriedPlain = (s, r) => (s.TryIntersectWith(r, out GeoPoint2[] p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Rectangle2", new[]
            {
                ("through", new GeoRay2(P(-50, 50), GeoVector2.XAxis), Rectangle(), true),
                ("beside", new GeoRay2(P(220, 0), V(-1, 1)), Rectangle(), false),
                ("behind", new GeoRay2(P(150, 50), GeoVector2.XAxis), Rectangle(), false),
                ("from inside", new GeoRay2(P(30, 40), GeoVector2.XAxis), Rectangle(), false),
                ("four tenths past a corner", Grazing(P(100, 100), U(1, -1), U(1, 1), out _), Rectangle(), false),
            },
            new Side<GeoRay2, GeoRectangle2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Tried = (r, s, t) => (r.TryIntersectWith(s, out GeoPoint2[] p, t), p), TriedPlain = (r, s) => (r.TryIntersectWith(s, out GeoPoint2[] p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoRectangle2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Tried = (s, r, t) => (s.TryIntersectWith(r, out GeoPoint2[] p, t), p), TriedPlain = (s, r) => (s.TryIntersectWith(r, out GeoPoint2[] p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Triangle2", new[]
            {
                ("through", new GeoRay2(P(-50, 20), GeoVector2.XAxis), Triangle(), true),
                ("beside", new GeoRay2(P(150, -50), GeoVector2.YAxis), Triangle(), false),
                ("behind", new GeoRay2(P(20, -30), -GeoVector2.YAxis), Triangle(), false),
                ("from inside", new GeoRay2(P(20, 20), V(1, 1)), Triangle(), false),
                ("four tenths past a corner", Grazing(P(100, 0), GeoVector2.YAxis, GeoVector2.XAxis, out _), Triangle(), false),
            },
            new Side<GeoRay2, GeoTriangle2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Crossings = (r, s, t) => r.GetIntersections(s, t), CrossingsPlain = (r, s) => r.GetIntersections(s),
                Tried = (r, s, t) => (r.TryIntersectWith(s, out GeoPoint2[] p, t), p), TriedPlain = (r, s) => (r.TryIntersectWith(s, out GeoPoint2[] p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoTriangle2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Crossings = (s, r, t) => s.GetIntersections(r, t), CrossingsPlain = (s, r) => s.GetIntersections(r),
                Tried = (s, r, t) => (s.TryIntersectWith(r, out GeoPoint2[] p, t), p), TriedPlain = (s, r) => (s.TryIntersectWith(r, out GeoPoint2[] p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Ray2", new[]
            {
                ("crossing square", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoRay2(P(50, -50), GeoVector2.YAxis), false),
                ("pointing away above", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoRay2(P(50, 30), GeoVector2.YAxis), false),
                ("diverging", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoRay2(P(20, 10), V(1, 1)), false),
                ("side by side", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoRay2(P(10, 5), GeoVector2.XAxis), false),
                ("facing each other along one line", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoRay2(P(100, 0), -GeoVector2.XAxis), true),
                ("facing away along one line", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoRay2(P(-10, 0), -GeoVector2.XAxis), false),
                ("two thousandths behind the origin", new GeoRay2(P(0, 0), GeoVector2.XAxis), new GeoRay2(P(-0.002, -5), GeoVector2.YAxis), false),
            },
            new Side<GeoRay2, GeoRay2>
            {
                Collides = (r, s, t) => r.CollidesWith(s, t), CollidesPlain = (r, s) => r.CollidesWith(s),
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Tried = (r, s, t) => One(r.TryIntersectWith(s, out GeoPoint2 p, t), p), TriedPlain = (r, s) => One(r.TryIntersectWith(s, out GeoPoint2 p), p),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoRay2, GeoRay2>
            {
                Collides = (s, r, t) => s.CollidesWith(r, t), CollidesPlain = (s, r) => s.CollidesWith(r),
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Tried = (s, r, t) => One(s.TryIntersectWith(r, out GeoPoint2 p, t), p), TriedPlain = (s, r) => One(s.TryIntersectWith(r, out GeoPoint2 p), p),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            Add(checks, "Point2", new[]
            {
                ("beside", new GeoRay2(P(0, 0), GeoVector2.XAxis), P(5, 3), false),
                ("behind", new GeoRay2(P(0, 0), GeoVector2.XAxis), P(-4, 3), false),
                ("on it", new GeoRay2(P(0, 0), GeoVector2.XAxis), P(7, 0), false),
            },
            new Side<GeoRay2, GeoPoint2>
            {
                Distance = (r, s, t) => r.DistanceTo(s, t), DistancePlain = (r, s) => r.DistanceTo(s),
                Shortest = (r, s, t) => r.GetShortestLineTo(s, t), ShortestPlain = (r, s) => r.GetShortestLineTo(s),
            },
            new Side<GeoPoint2, GeoRay2>
            {
                Distance = (s, r, t) => s.DistanceTo(r, t), DistancePlain = (s, r) => s.DistanceTo(r),
                Shortest = (s, r, t) => s.GetShortestLineTo(r, t), ShortestPlain = (s, r) => s.GetShortestLineTo(r),
            });

            return checks;
        }

        private static void Add<TB>(Dictionary<string, Action> checks, string partner, (string Case, GeoRay2 Ray, TB Shape, bool ManyCrossings)[] cases, Side<GeoRay2, TB> mine, Side<TB, GeoRay2> theirs)
        {
            foreach ((string Case, GeoRay2 Ray, TB Shape, bool ManyCrossings) c in cases)
            {
                string name = partner + ": " + c.Case;

                Names.Add(name);
                checks.Add(name, () => Check(c.Ray, c.Shape, c.ManyCrossings, mine, theirs));
            }
        }

        #endregion

        #region Judging a row

        private static void Check<TB>(GeoRay2 ray, TB shape, bool manyCrossings, Side<GeoRay2, TB> mine, Side<TB, GeoRay2> theirs)
        {
            foreach (Tolerance tolerance in new[] { Strict, Loose })
            {
                double distance = mine.Distance(ray, shape, tolerance);

                Assert.InRange(theirs.Distance(shape, ray, tolerance) - distance, -1E-9, 1E-9);

                if (mine.Collides != null)
                {
                    bool collides = mine.Collides(ray, shape, tolerance);

                    Assert.Equal(collides, theirs.Collides(shape, ray, tolerance));

                    if (tolerance.EqualPoint == Strict.EqualPoint)
                    {
                        Assert.Equal(distance <= tolerance.EqualPoint, collides);
                    }
                }

                if (mine.Crossings != null)
                {
                    AssertSamePoints(mine.Crossings(ray, shape, tolerance), theirs.Crossings(shape, ray, tolerance), tolerance.EqualPoint);
                }

                if (mine.Tried != null)
                {
                    (bool hit, GeoPoint2[] points) = mine.Tried(ray, shape, tolerance);
                    (bool hitBack, GeoPoint2[] pointsBack) = theirs.Tried(shape, ray, tolerance);

                    Assert.Equal(hit, hitBack);
                    AssertSamePoints(points, pointsBack, tolerance.EqualPoint);
                }

                if (tolerance.EqualPoint == Strict.EqualPoint)
                {
                    GeoLine2 line = mine.Shortest(ray, shape, tolerance);
                    GeoLine2 back = theirs.Shortest(shape, ray, tolerance);

                    Assert.InRange(line.Length - distance, -1E-9, 1E-9);
                    Assert.InRange(back.Length - distance, -1E-9, 1E-9);

                    if (!manyCrossings)
                    {
                        Assert.InRange(line.StartPoint.DistanceTo(back.EndPoint), 0.0, 1E-9);
                        Assert.InRange(line.EndPoint.DistanceTo(back.StartPoint), 0.0, 1E-9);
                    }
                }
            }

            AssertPlainIsGlobal(ray, shape, mine);
            AssertPlainIsGlobal(shape, ray, theirs);
        }

        private static void AssertPlainIsGlobal<TA, TB>(TA asked, TB argument, Side<TA, TB> side)
        {
            Tolerance global = Tolerance.Global;

            Assert.Equal(side.Distance(asked, argument, global), side.DistancePlain(asked, argument));
            Assert.Equal(side.Shortest(asked, argument, global), side.ShortestPlain(asked, argument));

            if (side.Collides != null)
            {
                Assert.Equal(side.Collides(asked, argument, global), side.CollidesPlain(asked, argument));
            }

            if (side.Crossings != null)
            {
                Assert.Equal(side.Crossings(asked, argument, global), side.CrossingsPlain(asked, argument));
            }

            if (side.Tried != null)
            {
                (bool hit, GeoPoint2[] points) = side.Tried(asked, argument, global);
                (bool hitPlain, GeoPoint2[] pointsPlain) = side.TriedPlain(asked, argument);

                Assert.Equal(hit, hitPlain);
                Assert.Equal(points, pointsPlain);
            }
        }

        private static void AssertSamePoints(GeoPoint2[] expected, GeoPoint2[] actual, double within = 1E-9)
        {
            Assert.Equal(expected.Length, actual.Length);

            foreach (GeoPoint2 point in expected)
            {
                Assert.Contains(actual, p => p.DistanceTo(point) <= within);
            }
        }

        #endregion

        #region The ellipse, asked from the ellipse only

        [Fact]
        public void TheOvalAsksARayAlongItsLongAxis_AndIsCrossedAtBothEnds()
        {
            // The oval 200 by 100 about the origin; the ray from (-200, 0) along X crosses it at (-100, 0) and (100, 0).
            var ray = new GeoRay2(P(-200, 0), GeoVector2.XAxis);
            GeoEllipse2 oval = Oval();

            AssertSamePoints(new[] { P(-100, 0), P(100, 0) }, oval.GetIntersections(ray, Strict));
            Assert.True(oval.TryIntersectWith(ray, out GeoPoint2[] found, Strict));
            AssertSamePoints(new[] { P(-100, 0), P(100, 0) }, found);
            Assert.True(oval.CollidesWith(ray, Strict));
            Assert.Equal(0.0, oval.DistanceTo(ray, Strict));
        }

        [Fact]
        public void TheOvalAsksARayThirtyAboveItsTop_AndItsLineLeavesTheRim()
        {
            // The top of the oval is (0, 50); the ray along y = 80 is 30 above it, and the line starts on the rim.
            var ray = new GeoRay2(P(-200, 80), GeoVector2.XAxis);
            GeoEllipse2 oval = Oval();
            GeoLine2 line = oval.GetShortestLineTo(ray, Strict);

            Assert.False(oval.CollidesWith(ray, Strict));
            Assert.Equal(30.0, oval.DistanceTo(ray, Strict), 9);
            Assert.InRange(line.StartPoint.DistanceTo(P(0, 50)), 0.0, 1E-6);
            Assert.InRange(line.EndPoint.DistanceTo(P(0, 80)), 0.0, 1E-6);
            Assert.Empty(oval.GetIntersections(ray, Strict));
        }

        [Fact]
        public void TheOvalAsksARayStartingPastItAndPointingAway_AndMeasuresToItsOrigin()
        {
            var ray = new GeoRay2(P(150, 0), GeoVector2.XAxis);
            GeoEllipse2 oval = Oval();
            GeoLine2 line = oval.GetShortestLineTo(ray, Strict);

            Assert.Equal(50.0, oval.DistanceTo(ray, Strict), 9);
            Assert.InRange(line.StartPoint.DistanceTo(P(100, 0)), 0.0, 1E-6);
            Assert.InRange(line.EndPoint.DistanceTo(P(150, 0)), 0.0, 1E-6);
            Assert.False(oval.TryIntersectWith(ray, out GeoPoint2[] found, Strict));
            Assert.Empty(found);
        }

        [Fact]
        public void TheOvalAsksARayFromItsCentre_IsNoughtFromIt_AndIsCrossedAtTheTop()
        {
            // The oval is filled for the distance, as the circle is; the ray leaves it at (0, 50). The cut reaches the
            // centre along the ray plus the long radius, so a ray along the long axis from the centre leaves at (100, 0).
            GeoEllipse2 oval = Oval();

            Assert.Equal(0.0, oval.DistanceTo(new GeoRay2(P(0, 0), GeoVector2.YAxis), Strict));
            Assert.True(oval.CollidesWith(new GeoRay2(P(0, 0), GeoVector2.YAxis), Strict));
            AssertSamePoints(new[] { P(0, 50) }, oval.GetIntersections(new GeoRay2(P(0, 0), GeoVector2.YAxis), Strict));
            AssertSamePoints(new[] { P(100, 0) }, oval.GetIntersections(new GeoRay2(P(0, 0), GeoVector2.XAxis), Strict));
        }

        [Fact]
        public void TheOvalsFormsWithoutATolerance_AnswerAsTheGlobalToleranceDoes()
        {
            GeoEllipse2 oval = Oval();

            foreach (GeoRay2 ray in new[] { new GeoRay2(P(-200, 0), GeoVector2.XAxis), new GeoRay2(P(-200, 80), GeoVector2.XAxis), new GeoRay2(P(150, 0), GeoVector2.XAxis) })
            {
                Assert.Equal(oval.CollidesWith(ray, Tolerance.Global), oval.CollidesWith(ray));
                Assert.Equal(oval.DistanceTo(ray, Tolerance.Global), oval.DistanceTo(ray));
                Assert.Equal(oval.GetIntersections(ray, Tolerance.Global), oval.GetIntersections(ray));
                Assert.Equal(oval.TryIntersectWith(ray, out GeoPoint2[] within, Tolerance.Global), oval.TryIntersectWith(ray, out GeoPoint2[] plain));
                Assert.Equal(within, plain);
                Assert.Equal(oval.GetShortestLineTo(ray, Tolerance.Global), oval.GetShortestLineTo(ray));
            }
        }

        #endregion
    }
}
