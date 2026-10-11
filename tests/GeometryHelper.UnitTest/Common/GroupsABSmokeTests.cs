using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// A handful of checks on the shortest lines in space where only the distance was answered, and on a face in the
    /// plane measured by its material. The full suites live elsewhere; these only show the wiring holds.
    /// </summary>
    public class GroupsABSmokeTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPolygon2 Square(double x0, double y0, double x1, double y1)
            => new GeoPolygon2(new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1));

        // The square 100 by 100 with a corner at the origin and a hole 40 by 40 in its middle.
        private static GeoFace2 Plate() => new GeoFace2(Square(0, 0, 100, 100), new[] { Square(30, 30, 70, 70) });

        [Fact]
        public void APointAboveAPolygonIsJoinedStraightDownAndTheLineTurnsRound()
        {
            var square = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
            var point = new GeoPoint3(50, 50, 30);

            GeoLine3 fromSquare = square.GetShortestLineTo(point, Tol);
            GeoLine3 fromPoint = point.GetShortestLineTo(square, Tol);

            Assert.True(fromSquare.StartPoint.IsEqualTo(new GeoPoint3(50, 50, 0), Tol));
            Assert.Equal(fromSquare.Reverse(), fromPoint);
            Assert.Equal(square.DistanceTo(point), fromSquare.Length, 12);
        }

        [Fact]
        public void ASegmentThroughAPlaneMeetsItInALineOfNoLength()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, 10), new GeoPoint3(0, 0, -10));

            GeoLine3 joining = line.GetShortestLineTo(GeoPlane3.XY, Tol);

            Assert.Equal(0.0, joining.Length);
            Assert.True(joining.StartPoint.IsEqualTo(GeoPoint3.Origin, Tol));
            Assert.Equal(joining.Reverse(), GeoPlane3.XY.GetShortestLineTo(line, Tol));
        }

        [Fact]
        public void AChainInsideABodyIsJoinedOutToTheNearestFace()
        {
            GeoSolid3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100)).ToObb().ToSolid();
            var chain = new GeoPolyline3(new GeoPoint3(50, 50, 10), new GeoPoint3(50, 50, 60), new GeoPoint3(50, 70, 60));

            GeoLine3 fromBox = box.GetShortestLineTo(chain, Tol);

            Assert.Equal(0.0, box.DistanceTo(chain, Tol));
            Assert.Equal(10.0, fromBox.Length, 9);
            Assert.Equal(fromBox.Reverse(), chain.GetShortestLineTo(box, Tol));
        }

        [Fact]
        public void TwoChainsAreAsFarApartAsTheirShortestLineIsLong()
        {
            var first = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var second = new GeoPolyline3(new GeoPoint3(50, 30, 40), new GeoPoint3(50, 30, 100));

            GeoLine3 joining = first.GetShortestLineTo(second, Tol);

            Assert.Equal(first.DistanceTo(second, Tol), joining.Length);
            Assert.Equal(50.0, joining.Length, 9);
        }

        [Fact]
        public void ACircleInAHoleIsAsFarAsTheRimOfTheHole()
        {
            var circle = new GeoCircle2(new GeoPoint2(50, 50), 10);

            Assert.Equal(10.0, Plate().DistanceTo(circle, Tol), 9);
            Assert.Equal(Plate().DistanceTo(circle, Tol), circle.DistanceTo(Plate(), Tol));
            Assert.Equal(0.0, Plate().DistanceTo(new GeoCircle2(new GeoPoint2(50, 50), 200), Tol));
        }

        [Fact]
        public void AFaceInTheHoleOfAnotherDoesNotCollideWithIt()
        {
            var inHole = new GeoFace2(Square(40, 40, 60, 60));
            var onMaterial = new GeoFace2(Square(5, 40, 25, 60));

            Assert.False(Plate().CollidesWith(inHole, Tol));
            Assert.False(inHole.CollidesWith(Plate(), Tol));
            Assert.True(Plate().CollidesWith(onMaterial, Tol));
            Assert.Equal(10.0, Plate().DistanceTo(inHole, Tol), 9);
            Assert.Equal(10.0, Plate().GetShortestLineTo(inHole, Tol).Length, 9);
        }

        [Fact]
        public void TwoFacesCrossWhereTheirRimsCross()
        {
            var across = new GeoFace2(Square(20, 40, 40, 60));

            GeoPoint2[] crossings = Plate().GetIntersections(across, Tol);

            Assert.Equal(2, crossings.Length);
            Assert.Contains(crossings, p => p.IsEqualTo(new GeoPoint2(30, 40), Tol));
            Assert.Contains(crossings, p => p.IsEqualTo(new GeoPoint2(30, 60), Tol));
        }

        [Fact]
        public void AnEllipseInAHoleIsMeasuredToTheRim()
        {
            var ellipse = new GeoEllipse2(new GeoPoint2(50, 50), new GeoVector2(1, 0), 15, 5);

            Assert.Equal(5.0, ellipse.DistanceTo(Plate(), Tol), 6);
            Assert.Throws<ArgumentNullException>(() => ellipse.DistanceTo((GeoFace2)null, Tol));
        }
    }
}
