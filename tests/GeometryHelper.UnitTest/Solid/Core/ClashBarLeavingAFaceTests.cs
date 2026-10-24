using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A bar whose centre line comes within the tolerance of a face only toward the end of a piece whose middle stands off
    /// the part runs inside for as long as it stays within it, as one lying along the face does. It used to count none of
    /// such a run.
    /// </summary>
    public class ClashBarLeavingAFaceTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static ClashResult One(GeoPoint3[] points, double radius, GeoSolid3 part)
            => Clash3.Find(new[] { new ClashBar(new GeoPolyline3(points), radius) }, new[] { part }, ClashOptions.Default, Tol).Single();

        [Fact]
        public void ABarRisingOffTheTopOfAStep_RunsInsideFromTheEdgeToWhereItLeavesTheTolerance()
        {
            // A part 1 000 by 200, 100 high over x < 500 and 50 beyond, its top in z = 100 ending at the edge x = 0 and the
            // step x = 500. The bar comes from x = -247.57 at 0.00062 over the top and rises to 0.00135 over it at x = 667.19:
            // it crosses the edge 0.00082 over it and leaves the tolerance of a thousandth about 225 further on, before the
            // step. The middle of its piece past the edge stands 0.00109 over the top, and it read nought.
            var a = new GeoPoint3(-247.57400385370454, 12.598145924233094, 100.0006244368319);
            var b = new GeoPoint3(667.1920034769438, 7.578539824119975, 100.00135132409922);
            var profile = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1000, 0), new GeoPoint2(1000, 50), new GeoPoint2(500, 50), new GeoPoint2(500, 100), new GeoPoint2(0, 100));
            GeoSolid3 part = GeoSolid3.Extrude(profile, new GeoCoordinateSystem3(new GeoPoint3(0, 200, 0), GeoVector3.XAxis, GeoVector3.ZAxis), 200, Tol);

            ClashResult clash = One(new[] { a, b }, 15.723443401848638, part);

            double length = a.DistanceTo(b);
            double atEdge = -a.X / (b.X - a.X);
            double leaves = (100.001 - a.Z) / (b.Z - a.Z);
            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.True(System.Math.Abs(clash.LengthInside - (leaves - atEdge) * length) <= 1E-6, $"{clash.LengthInside} against {(leaves - atEdge) * length}");
            Assert.True(System.Math.Abs(clash.Depth - 15.723443401848638) <= Tol.EqualPoint, $"{clash.Depth}");
        }

        [Fact]
        public void ABarRisingOffTheTopOfABoxFromItsEdge_RunsInsideUntilItLeavesTheTolerance()
        {
            // Along X over the middle of the top of a box 1 000 by 200 by 100, from x = -250 at 0.0007 over it to x = 750 at
            // 0.0015 over it: it crosses the edge x = 0 0.0009 over the top and leaves the tolerance of a thousandth at
            // x = 125. It read nought.
            GeoSolid3 part = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 200, 100)).ToObb().ToSolid();
            var a = new GeoPoint3(-250, 100, 100.0007);
            var b = new GeoPoint3(750, 100, 100.0015);

            ClashResult clash = One(new[] { a, b }, 8.0, part);

            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.True(System.Math.Abs(clash.LengthInside - 125.0 * a.DistanceTo(b) / 1000.0) <= 1E-6, $"{clash.LengthInside}");
        }

        [Fact]
        public void ABarDippingThroughASideAtAShallowSlant_RunsInsideForAllItsRunWithinTheTolerance()
        {
            // Along the side y = 0 of a box 1 000 by 200 by 100, at mid height: from 0.003 off it at x = 400 into the box,
            // 0.0005 deep at x = 500, and out again to 0.003 off at x = 600. Each half comes within the tolerance of a
            // thousandth 4/7 of the way along, at x = 457.14 and 542.86, so the bar runs within it for 85.71. The crossing of
            // the face cuts each half where its middle stands off the face, and only the 14.29 of each half past it counted.
            GeoSolid3 part = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 200, 100)).ToObb().ToSolid();
            var points = new[] { new GeoPoint3(400, -0.003, 50), new GeoPoint3(500, 0.0005, 50), new GeoPoint3(600, -0.003, 50) };

            ClashResult clash = One(points, 8.0, part);

            double half = points[0].DistanceTo(points[1]);
            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.True(System.Math.Abs(clash.LengthInside - 2.0 * half * 3.0 / 7.0) <= 1E-6, $"{clash.LengthInside} against {2.0 * half * 3.0 / 7.0}");
        }

        [Theory]
        [InlineData(800.0)]
        [InlineData(1000.0)]
        [InlineData(1600.0)]
        [InlineData(2000.0)]
        public void ABarLeavingTheToleranceAndComingBack_RunsInsideOnlyWhereItIsWithinIt(double end)
        {
            // A part 3 000 long and 200 wide, its top 100 high up to x = 200, rising 0.004 to x = 400 and level beyond. The
            // bar rises from 0.0006 over the top at x = 10, 1E-5 for each unit: within the tolerance of a thousandth up to
            // x = 50, off it to x = 350, within it over the rise up to x = 450, and off it beyond. 140 of it lies within the
            // tolerance; the stretch off it between the two runs counted as well, 440, or the run over the rise not at all.
            var profile = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(3000, 0), new GeoPoint2(3000, 100.004), new GeoPoint2(400, 100.004), new GeoPoint2(200, 100), new GeoPoint2(0, 100));
            GeoSolid3 part = GeoSolid3.Extrude(profile, new GeoCoordinateSystem3(new GeoPoint3(0, 200, 0), GeoVector3.XAxis, GeoVector3.ZAxis), 200, Tol);
            var a = new GeoPoint3(10, 100, 100.0005 + 1E-5 * 10);
            var b = new GeoPoint3(end, 100, 100.0005 + 1E-5 * end);

            ClashResult clash = One(new[] { a, b }, 8.0, part);

            double expected = 140.0 * a.DistanceTo(b) / (b.X - a.X);
            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.True(System.Math.Abs(clash.LengthInside - expected) <= 1E-3, $"{clash.LengthInside} against {expected}");
        }

        [Fact]
        public void ABarOverAFlangeThroughTheLegOfAnAngle_RunsInsideThroughTheLegAndWhereItComesWithinTheTolerance()
        {
            // An angle 1 000 long with a flange 20 thick and a leg 20 thick and 300 high, 200 wide, moved a million and more
            // from the origin. The bar runs along the flange from 0.0017 over it at x = -210.9 to 0.0007 over it at x = 318.7,
            // through the leg from x = 0 to 20, measured to its face x = 0 as a bar crossing a face square on is, within the
            // tolerance of its face x = 20 for a thousandth past it, as the piece beyond lies within the tolerance of the
            // flange at its middle, and over the flange within the tolerance from x = 150 on: 188.68. It read 318.67, the
            // stretch off the tolerance from the leg to x = 150 counted too.
            var shift = new GeoVector3(1E6, 2E6, 50);
            var profile = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(1000, 0), new GeoPoint2(1000, 20), new GeoPoint2(20, 20), new GeoPoint2(20, 300), new GeoPoint2(0, 300));
            GeoSolid3 part = GeoSolid3.Extrude(profile, new GeoCoordinateSystem3(new GeoPoint3(0, 200, 0), GeoVector3.XAxis, GeoVector3.ZAxis), 200, Tol)
                .TransformBy(GeoTransform3.Translation(shift));
            var a = new GeoPoint3(-210.93807327791026, 22.271431927695602, 20.001705857237898);
            var b = new GeoPoint3(318.6714404302983, 23.470882968265038, 20.000670116042333);

            ClashResult clash = One(new[] { a.Add(shift), b.Add(shift) }, 8.0, part);

            double perX = a.DistanceTo(b) / (b.X - a.X);
            double overFlange = a.X + (b.X - a.X) * (a.Z - 20.001) / (a.Z - b.Z);
            double expected = perX * (20.001 + (b.X - overFlange));
            Assert.Equal(ClashKind.Hard, clash.Kind);
            Assert.True(System.Math.Abs(clash.LengthInside - expected) <= 1E-3, $"{clash.LengthInside} against {expected}");
        }
    }
}
