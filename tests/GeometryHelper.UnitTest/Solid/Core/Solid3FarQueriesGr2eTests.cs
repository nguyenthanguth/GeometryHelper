using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Where a point, a ray, a segment or a line stands against a body a hundred million and a thousand million out is
    /// answered as it is answered of the same body near the origin, moved there exactly, the probe moved with it. Where it
    /// stood, the material those questions read was cut and read within a trillionth of the coordinates, a thousandth a
    /// thousand million out: the ceilings of two openings up from the floor of a plate, 0.0009 apart, were cut in one plane,
    /// and rays from beside an L with openings a thousand million out, along the world's Y axis among them, or from beside its
    /// prepared form, were given other crossings. Each probe stands half the tolerance in or out from the middle of a
    /// triangle of the material near the origin.
    /// </summary>
    public class Solid3FarQueriesGr2eTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(1, 2, 3).Multiply(1 / Math.Sqrt(14));

        private static readonly GeoVector3 Along = new GeoVector3(3, 0, -1).Multiply(1 / Math.Sqrt(10));

        private static readonly GeoVector3 Across = Up.CrossProduct(Along);

        /// <summary>The tolerance a polygon is moved within: its corners kept apart, and flat as the default reads it.</summary>
        private static readonly Tolerance Moving = new Tolerance(1E-9, 1E-9, Math.PI / 180, 1E-3);

        private static readonly GeoVector3[] Three = { new GeoVector3(0.36, -0.48, 0.8), new GeoVector3(-0.6, 0.0, 0.8), new GeoVector3(0.0, 0.6, -0.8) };

        private static readonly GeoVector3[] Eight =
        {
            new GeoVector3(0.36, -0.48, 0.8), new GeoVector3(-0.6, 0.0, 0.8), new GeoVector3(0.0, 0.6, -0.8), new GeoVector3(0.8, 0.6, 0.0),
            new GeoVector3(-0.48, -0.36, -0.8), new GeoVector3(0.0, -1.0, 0.0), new GeoVector3(0.6, -0.8, 0.0), new GeoVector3(-0.8, 0.0, -0.6),
        };

        private static Tolerance Within(double tolerance) => new Tolerance(tolerance, 1E-5, Math.PI / 180, tolerance);

        /// <summary>Where the body's own frame stands: (0.62, -0.85, 0.25) times how far out, to the millimetre.</summary>
        private static GeoVector3 Offset(double far) => new GeoVector3(Math.Round(0.62 * far), Math.Round(-0.85 * far), Math.Round(0.25 * far));

        private static GeoPoint3 L(GeoVector3 at, double x, double y, double z)
            => GeoPoint3.Origin.Add(at).Add(Along.Multiply(x)).Add(Across.Multiply(y)).Add(Up.Multiply(z));

        private static GeoSolid3 Box(GeoVector3 at, double x0, double y0, double z0, double x1, double y1, double z1)
            => GeoSolid3.Extrude(
                new GeoPolygon3(L(at, x0, y0, z0), L(at, x1, y0, z0), L(at, x1, y1, z0), L(at, x0, y1, z0)), Up.Multiply(z1 - z0), Tolerance.Default);

        /// <summary>
        /// A plate 200 by 100 by 3 turned about in space, with an opening 40 by 40 up from its floor 2 deep and one beside it,
        /// overlapping it, 2.0009 deep, their ceilings 0.0009 apart.
        /// </summary>
        private static GeoSolid3 Plate(GeoVector3 at)
            => Box(at, 0, 0, 0, 200, 100, 3).WithOpenings(new[] { Box(at, 40, 30, -1, 80, 70, 2), Box(at, 60, 40, -1, 100, 80, 2.0009) });

        /// <summary>
        /// An L 100 by 80, its legs 20 wide, 50 high, turned about in space, with a pocket 20 by 10 up from the floor of one leg
        /// 30 deep and a hole 10 by 20 down through the other from its top to 20 over its floor.
        /// </summary>
        private static GeoSolid3 LWithOpenings(GeoVector3 at)
        {
            GeoSolid3 l = GeoSolid3.Extrude(
                new GeoPolygon3(L(at, 0, 0, 0), L(at, 100, 0, 0), L(at, 100, 20, 0), L(at, 20, 20, 0), L(at, 20, 80, 0), L(at, 0, 80, 0)),
                Up.Multiply(50),
                Tolerance.Default);

            return l.WithOpenings(new[] { Box(at, 50, 5, -1, 70, 15, 30), Box(at, 5, 40, 20, 15, 60, 55) });
        }

        /// <summary>The body far out moved back by the offset, corner by corner, exactly: every coordinate lies within twice it.</summary>
        private static GeoSolid3 Back(GeoSolid3 solid, GeoVector3 offset)
            => new GeoSolid3(
                solid.Faces.Select(f => new GeoFace3(Back(f.Boundary, offset), f.Holes.Select(h => Back(h, offset)).ToArray(), Moving)).ToArray(),
                solid.Openings.Select(o => Back(o, offset)).ToArray());

        private static GeoPolygon3 Back(GeoPolygon3 polygon, GeoVector3 offset)
            => new GeoPolygon3(polygon.Vertices.Select(v => v.Add(offset.Negate())).ToArray(), Moving);

        /// <summary>
        /// The probes: half the tolerance in and half out from the middle of each triangle of the material near the origin,
        /// each far out and moved back exactly.
        /// </summary>
        private static List<(GeoPoint3 Far, GeoPoint3 Near)> Probes(GeoSolid3 near, GeoVector3 offset, Tolerance tolerance)
        {
            Assert.True(near.TryCutOpenings(out GeoSolid3 material, tolerance));
            var probes = new List<(GeoPoint3, GeoPoint3)>();

            foreach (GeoTriangle3 triangle in material.TriangulateSurface(tolerance))
            {
                var middle = new GeoPoint3(
                    (triangle.A.X + triangle.B.X + triangle.C.X) / 3, (triangle.A.Y + triangle.B.Y + triangle.C.Y) / 3, (triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3);

                foreach (double side in new[] { 0.5, -0.5 })
                {
                    GeoPoint3 far = middle.Add(triangle.Normal.Multiply(side * tolerance.EqualPoint)).Add(offset);

                    probes.Add((far, far.Add(offset.Negate())));
                }
            }

            return probes;
        }

        /// <summary>A millionth: how far apart a gap measured far out and near the origin may come.</summary>
        private const double Gap = 1E-6;

        /// <summary>How far apart a crossing found far out, moved back, and one found near the origin may stand: twice a millionth, and the rounding of moving it out.</summary>
        private static double Place(double far) => 2E-6 + 8 * far * Math.Pow(2, -52);

        private static bool Same(GeoPoint3[] far, GeoPoint3[] near, GeoVector3 offset, double within)
            => far.Length == near.Length && far.Zip(near, (f, n) => f.Add(offset.Negate()).DistanceTo(n) <= within).All(x => x);

        public static TheoryData<double, double> PlateCases() => new TheoryData<double, double> { { 1E9, 1E-2 }, { 1E9, 1E-3 }, { 1E8, 1E-3 } };

        public static TheoryData<double, double> LCases() => new TheoryData<double, double> { { 1E9, 1E-2 }, { 1E9, 1E-3 } };

        [Theory]
        [MemberData(nameof(PlateCases))]
        public void APointBesideAPlateWithTwoOpeningsFarOut_IsLocatedAndMeasuredAsNearTheOrigin(double far, double within)
        {
            Tolerance tolerance = Within(within);
            GeoVector3 offset = Offset(far);
            GeoSolid3 there = Plate(offset);
            GeoSolid3 here = Back(there, offset);
            var wrong = new List<string>();

            foreach ((GeoPoint3 p, GeoPoint3 q) in Probes(here, offset, tolerance))
            {
                if (there.Locate(p, tolerance) != here.Locate(q, tolerance)) wrong.Add("Locate " + q);
                if (Math.Abs(there.DistanceTo(p, tolerance) - here.DistanceTo(q, tolerance)) > Gap) wrong.Add("DistanceTo " + q);
                if (Math.Abs(there.SignedDistanceTo(p, tolerance) - here.SignedDistanceTo(q, tolerance)) > Gap) wrong.Add("SignedDistanceTo " + q);
                if (Math.Abs(p.DistanceTo(there.GetClosestPointOnBoundary(p, tolerance)) - q.DistanceTo(here.GetClosestPointOnBoundary(q, tolerance))) > Gap) wrong.Add("GetClosestPointOnBoundary " + q);
                if (Math.Abs(there.GetShortestLineTo(p, tolerance).Length - here.GetShortestLineTo(q, tolerance).Length) > Gap) wrong.Add("GetShortestLineTo " + q);
            }

            Assert.Empty(wrong);
        }

        [Theory]
        [MemberData(nameof(PlateCases))]
        public void ASegmentFromBesideAPlateWithTwoOpeningsFarOut_IsGivenTheCrossingsItIsGivenNearTheOrigin(double far, double within)
        {
            Tolerance tolerance = Within(within);
            GeoVector3 offset = Offset(far);
            GeoSolid3 there = Plate(offset);
            GeoSolid3 here = Back(there, offset);
            var wrong = new List<string>();

            foreach ((GeoPoint3 p, GeoPoint3 q) in Probes(here, offset, tolerance))
            {
                foreach (GeoVector3 direction in Three)
                {
                    var a = new GeoLine3(p, p.Add(direction.Multiply(30)));
                    var b = new GeoLine3(q, q.Add(direction.Multiply(30)));

                    if (a.CollidesWith(there, tolerance) != b.CollidesWith(here, tolerance)) wrong.Add("CollidesWith " + q);
                    if (!Same(a.GetIntersections(there, tolerance), b.GetIntersections(here, tolerance), offset, Place(far))) wrong.Add("GetIntersections " + q);
                }
            }

            Assert.Empty(wrong);
        }

        [Theory]
        [MemberData(nameof(PlateCases))]
        public void ALineThroughBesideAPlateWithTwoOpeningsFarOut_IsGivenTheCrossingsItIsGivenNearTheOrigin(double far, double within)
        {
            Tolerance tolerance = Within(within);
            GeoVector3 offset = Offset(far);
            GeoSolid3 there = Plate(offset);
            GeoSolid3 here = Back(there, offset);
            var wrong = new List<string>();

            foreach ((GeoPoint3 p, GeoPoint3 q) in Probes(here, offset, tolerance))
            {
                foreach (GeoVector3 direction in Three)
                {
                    var a = new GeoXLine3(p, direction);
                    var b = new GeoXLine3(q, direction);

                    if (a.CollidesWith(there, tolerance) != b.CollidesWith(here, tolerance)) wrong.Add("CollidesWith " + q);
                    if (!Same(a.GetIntersections(there, tolerance), b.GetIntersections(here, tolerance), offset, Place(far))) wrong.Add("GetIntersections " + q);
                }
            }

            Assert.Empty(wrong);
        }

        [Theory]
        [MemberData(nameof(LCases))]
        public void ARayFromBesideAnLWithOpeningsFarOut_MeetsItWhereItMeetsItNearTheOrigin(double far, double within)
        {
            Tolerance tolerance = Within(within);
            GeoVector3 offset = Offset(far);
            GeoSolid3 there = LWithOpenings(offset);
            GeoSolid3 here = Back(there, offset);
            var wrong = new List<string>();

            foreach ((GeoPoint3 p, GeoPoint3 q) in Probes(here, offset, tolerance))
            {
                foreach (GeoVector3 direction in Eight)
                {
                    var a = new GeoRay3(p, direction);
                    var b = new GeoRay3(q, direction);

                    if (a.CollidesWith(there, tolerance) != b.CollidesWith(here, tolerance)) wrong.Add("CollidesWith " + q);
                    if (!Same(a.GetIntersections(there, tolerance), b.GetIntersections(here, tolerance), offset, Place(far))) wrong.Add("GetIntersections " + q);
                }
            }

            Assert.Empty(wrong);
        }

        [Theory]
        [MemberData(nameof(LCases))]
        public void APreparedLWithOpeningsFarOut_AnswersAPointAndARayAsNearTheOrigin(double far, double within)
        {
            Tolerance tolerance = Within(within);
            GeoVector3 offset = Offset(far);
            GeoSolid3 here = Back(LWithOpenings(offset), offset);
            var there = new GeoPreparedSolid3(LWithOpenings(offset), tolerance);
            var prepared = new GeoPreparedSolid3(here, tolerance);
            var wrong = new List<string>();

            foreach ((GeoPoint3 p, GeoPoint3 q) in Probes(here, offset, tolerance))
            {
                if (there.Locate(p, tolerance) != prepared.Locate(q, tolerance)) wrong.Add("Locate " + q);
                if (Math.Abs(there.SignedDistanceTo(p, tolerance) - prepared.SignedDistanceTo(q, tolerance)) > Gap) wrong.Add("SignedDistanceTo " + q);

                foreach (GeoVector3 direction in Eight)
                {
                    var a = new GeoRay3(p, direction);
                    var b = new GeoRay3(q, direction);

                    if (there.CollidesWith(a, tolerance) != prepared.CollidesWith(b, tolerance)) wrong.Add("CollidesWith " + q);
                    if (!Same(there.GetIntersections(a, tolerance), prepared.GetIntersections(b, tolerance), offset, Place(far))) wrong.Add("GetIntersections " + q);
                }
            }

            Assert.Empty(wrong);
        }

        [Fact]
        public void APreparedBodyFarOut_KeepsItsMaterialSurfaceAndIndexWhereItStands()
        {
            // The frame is the prepared body's own: what it hands out, and what it weighs another body by, is where it stands.
            GeoVector3 offset = Offset(1E9);
            var prepared = new GeoPreparedSolid3(LWithOpenings(offset), Tolerance.Default);

            Assert.Equal(PointLocation.Inside, prepared.Locate(L(offset, 10, 10, 10), Tolerance.Default));
            Assert.InRange(prepared.Box.Min.Y - offset.Y, -200.0, 200.0);
            Assert.All(prepared.Surface, t => Assert.InRange(t.A.Y - offset.Y, -200.0, 200.0));
            Assert.All(prepared.Material.Faces, f => Assert.InRange(f.Boundary.Vertices[0].Z - offset.Z, -200.0, 200.0));
        }
    }
}
