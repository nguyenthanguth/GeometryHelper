using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// The review's findings on the flat shapes in space: a shallow crossing found however finely a side is cut, a region
    /// touching another whenever their distance reads nought, the plane's layout read with the tolerance given, a corner
    /// that is not a number carried into the line to a plane.
    /// </summary>
    public class FlatShapesInSpaceReviewTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPoint3 Pt(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPolygon3 Square(double x0, double y0, double x1, double y1, double z)
            => new GeoPolygon3(Pt(x0, y0, z), Pt(x1, y0, z), Pt(x1, y1, z), Pt(x0, y1, z));

        // Tilted by an angle about the line y = 50 at the given height: y = 50 stays where it is, the side towards +y rises.
        private static GeoPoint3 Tilt(GeoPoint3 p, double degrees, double height)
        {
            double a = degrees * Math.PI / 180.0;
            double dy = p.Y - 50.0;

            return Pt(p.X, 50.0 + dy * Math.Cos(a), height + dy * Math.Sin(a));
        }

        // The square 40 by 40 about (50, 50), tilted about y = 50 at the given height.
        private static GeoPoint3[] TiltedSquare(double degrees, double height)
            => new[] { Pt(30, 30, 0), Pt(70, 30, 0), Pt(70, 70, 0), Pt(30, 70, 0) }.Select(p => Tilt(p, degrees, height)).ToArray();

        // A regular polygon of the given number of corners and radius about (50, 50), tilted about y = 50.
        private static GeoPoint3[] TiltedRound(int corners, double radius, double degrees, double height)
            => Enumerable.Range(0, corners)
                .Select(i => Pt(50 + radius * Math.Cos(2 * Math.PI * i / corners), 50 + radius * Math.Sin(2 * Math.PI * i / corners), 0))
                .Select(p => Tilt(p, degrees, height))
                .ToArray();

        // Each side cut into the given number of collinear pieces.
        private static GeoPolygon3 Cut(GeoPoint3[] corners, int pieces)
        {
            var cut = new List<GeoPoint3>();

            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint3 from = corners[i];
                GeoPoint3 to = corners[(i + 1) % corners.Length];

                for (int k = 0; k < pieces; k++)
                {
                    cut.Add(from.Add(from.GetVectorTo(to).Multiply((double)k / pieces)));
                }
            }

            return new GeoPolygon3(cut);
        }

        private static void AssertSamePlaces(GeoPoint3[] expected, GeoPoint3[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            foreach (GeoPoint3 p in expected)
            {
                Assert.Contains(actual, q => q.IsEqualTo(p, Tol));
            }
        }

        [Fact]
        public void ASquareCutIntoEightyLevelPiecesASideCrossesAnotherAsItsUncutFormDoes()
        {
            GeoPolygon3 floor = Square(0, 0, 100, 100, 0);
            var whole = new GeoPolygon3(TiltedSquare(0.1, 0));
            GeoPolygon3 cut = Cut(TiltedSquare(0.1, 0), 80);

            Assert.True(floor.CollidesWith(whole, Tol));
            AssertSamePlaces(new[] { Pt(30, 50, 0), Pt(70, 50, 0) }, floor.GetIntersections(whole, Tol));

            Assert.True(floor.CollidesWith(cut, Tol));
            Assert.True(cut.CollidesWith(floor, Tol));
            AssertSamePlaces(floor.GetIntersections(whole, Tol), floor.GetIntersections(cut, Tol));
            AssertSamePlaces(whole.GetIntersections(floor, Tol), cut.GetIntersections(floor, Tol));
            Assert.Equal(0.0, floor.DistanceTo(cut, Tol));
            Assert.Equal(0.0, floor.GetShortestLineTo(cut, Tol).Length);
            Assert.Equal(0.0, cut.GetShortestLineTo(floor, Tol).Length);
        }

        [Fact]
        public void ARound256GonAtAWentiethOfADegreeCrossesATriangleASquareAndAFace()
        {
            var round = new GeoPolygon3(TiltedRound(256, 30, 0.05, 0));
            var triangle = new GeoTriangle3(Pt(0, 0, 0), Pt(200, 0, 0), Pt(0, 200, 0));
            GeoPolygon3 floor = Square(0, 0, 100, 100, 0);
            var plate = new GeoFace3(Square(0, 0, 100, 100, 0), new[] { Square(45, 10, 55, 20, 0) });

            Assert.True(triangle.CollidesWith(round, Tol));
            Assert.True(round.CollidesWith(floor, Tol));
            Assert.True(plate.CollidesWith(round, Tol));
            AssertSamePlaces(new[] { Pt(20, 50, 0), Pt(80, 50, 0) }, triangle.GetIntersections(round, Tol));
            AssertSamePlaces(new[] { Pt(20, 50, 0), Pt(80, 50, 0) }, floor.GetIntersections(round, Tol));
            AssertSamePlaces(new[] { Pt(20, 50, 0), Pt(80, 50, 0) }, plate.GetIntersections(round, Tol));
            Assert.Equal(0.0, triangle.GetShortestLineTo(round, Tol).Length);
            Assert.Equal(0.0, plate.GetShortestLineTo(round, Tol).Length);
        }

        [Fact]
        public void ARound256GonLyingAcrossABoxTopCrossesIt()
        {
            GeoSolid3 box = new GeoAabb3(Pt(0, 0, 0), Pt(100, 100, 100)).ToObb().ToSolid();
            var round = new GeoPolygon3(TiltedRound(256, 30, 0.05, 100));

            Assert.True(round.CollidesWith(box, Tol));
            AssertSamePlaces(new[] { Pt(20, 50, 100), Pt(80, 50, 100) }, round.GetIntersections(box, Tol));
        }

        [Fact]
        public void APlaneNamesWhereA256GonCrossesItWhereverItSaysTheyTouch()
        {
            var round = new GeoPolygon3(TiltedRound(256, 30, 0.05, 0));
            GeoPolygon3 tilted = Cut(TiltedSquare(0.1, 0), 80);

            Assert.True(GeoPlane3.XY.CollidesWith(round, Tol));
            AssertSamePlaces(new[] { Pt(20, 50, 0), Pt(80, 50, 0) }, GeoPlane3.XY.GetIntersections(round, Tol));
            AssertSamePlaces(new[] { Pt(30, 50, 0), Pt(70, 50, 0) }, GeoPlane3.XY.GetIntersections(tilted, Tol));
            Assert.Equal(0.0, round.GetShortestLineTo(GeoPlane3.XY, Tol).Length);
        }

        [Fact]
        public void APlateTiltedWithinTheToleranceTouchesAsItDoesLyingLevel()
        {
            GeoPolygon3 floor = Square(0, 0, 100, 100, 0);
            var tilted = new GeoPolygon3(Pt(25, 25, 0.0002), Pt(75, 25, 0.0002), Pt(75, 75, 0.0011), Pt(25, 75, 0.0011));
            GeoPolygon3 level = Square(25, 25, 75, 75, 0.0009);

            Assert.True(floor.CollidesWith(level, Tol));
            Assert.Equal(0.0, floor.DistanceTo(level, Tol));
            Assert.True(floor.CollidesWith(tilted, Tol));
            Assert.True(tilted.CollidesWith(floor, Tol));
            Assert.Equal(0.0, floor.DistanceTo(tilted, Tol));
            Assert.Equal(0.0002, floor.GetShortestLineTo(tilted, Tol).Length, 12);
        }

        [Fact]
        public void ARegionWhoseDistanceReadsNoughtTouchesWhicheverKindItIs()
        {
            // Tilted about a diagonal, so that no side runs level: the corner at (25, 25) stands 0.0005 up and every side
            // climbs 0.005 from it, which the segment test reads end-on only within the tolerance along the side.
            Func<double, double, double> h = (x, y) => 0.0005 + 0.0001 * ((x - 25) + (y - 25));
            var plate = new GeoPolygon3(Pt(25, 25, h(25, 25)), Pt(75, 25, h(75, 25)), Pt(75, 75, h(75, 75)), Pt(25, 75, h(25, 75)));
            var plateTriangle = new GeoTriangle3(Pt(25, 25, h(25, 25)), Pt(75, 25, h(75, 25)), Pt(25, 75, h(25, 75)));
            GeoPolygon3 floor = Square(0, 0, 100, 100, 0);
            var floorFace = new GeoFace3(floor, new[] { Square(40, 40, 60, 60, 0) });
            var floorTriangle = new GeoTriangle3(Pt(0, 0, 0), Pt(200, 0, 0), Pt(0, 200, 0));

            Assert.True(floor.CollidesWith(plate, Tol));
            Assert.True(plate.CollidesWith(floorFace, Tol));
            Assert.True(new GeoFace3(plate).CollidesWith(floorFace, Tol));
            Assert.True(plateTriangle.CollidesWith(floor, Tol));
            Assert.True(plateTriangle.CollidesWith(floorFace, Tol));
            Assert.True(floorTriangle.CollidesWith(plate, Tol));

            Assert.Equal(0.0, floor.DistanceTo(plate, Tol));
            Assert.Equal(0.0, plateTriangle.DistanceTo(floorFace, Tol));
            Assert.Equal(0.0005, floor.GetShortestLineTo(plate, Tol).Length, 12);
        }

        [Fact]
        public void ATriangleWithACornerWithinTheToleranceOfABodyTouchesItWhicheverCornerThatIs()
        {
            GeoSolid3 box = new GeoAabb3(Pt(0, 0, 0), Pt(100, 100, 100)).ToObb().ToSolid();

            // The second corner stands 0.0005 over the top, the others well clear, and every side comes at the top too
            // steeply to be read end-on within the tolerance along it: the distance reads nought, so the two touch.
            var triangle = new GeoTriangle3(Pt(150, 50, 150), Pt(50, 50, 100.0005), Pt(150, 60, 150));

            Assert.Equal(0.0, box.DistanceTo(triangle, Tol));
            Assert.True(triangle.CollidesWith(box, Tol));
            Assert.True(box.CollidesWith(triangle, Tol));
            Assert.False(new GeoTriangle3(Pt(150, 50, 150), Pt(50, 50, 100.004), Pt(150, 60, 150)).CollidesWith(box, Tol));
        }

        [Fact]
        public void TwoSquaresInOnePlaneSmallerThanTheGlobalToleranceAreLaidOutWithTheToleranceGiven()
        {
            var fine = new Tolerance(1e-7, 1e-9, 1e-6, 1e-7);
            double s = 0.0005;
            var a = new GeoPolygon3(new[] { Pt(0, 0, 0), Pt(s, 0, 0), Pt(s, s, 0), Pt(0, s, 0) }, fine);
            var b = new GeoPolygon3(new[] { Pt(s / 2, s / 2, 0), Pt(2 * s, s / 2, 0), Pt(2 * s, 2 * s, 0), Pt(s / 2, 2 * s, 0) }, fine);
            var holed = new GeoFace3(a, new[] { new GeoPolygon3(new[] { Pt(s / 8, s / 8, 0), Pt(s / 4, s / 8, 0), Pt(s / 4, s / 4, 0), Pt(s / 8, s / 4, 0) }, fine) });

            Assert.True(a.CollidesWith(b, fine));
            Assert.Equal(2, a.GetIntersections(b, fine).Length);
            Assert.Equal(0.0, a.DistanceTo(b, fine));
            Assert.True(holed.CollidesWith(b, fine));
            Assert.Equal(2, holed.GetIntersections(b, fine).Length);
            Assert.True(new GeoTriangle3(Pt(s / 2, s / 2, 0), Pt(2 * s, s / 2, 0), Pt(s / 2, 2 * s, 0)).CollidesWith(holed, fine));
        }

        [Fact]
        public void ATriangleWithACornerThatIsNotANumberIsJoinedToAPlaneByALineThatIsNotANumber()
        {
            var triangle = new GeoTriangle3(Pt(0, 0, 100), Pt(double.NaN, 0, 100), Pt(0, 100, 100));

            Assert.True(double.IsNaN(GeoPlane3.XY.DistanceTo(triangle, Tol)));
            Assert.True(double.IsNaN(triangle.GetShortestLineTo(GeoPlane3.XY, Tol).Length));
            Assert.True(double.IsNaN(GeoPlane3.XY.GetShortestLineTo(triangle, Tol).Length));
        }
    }
}
