using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Bodies made by moving a flat profile: straight out, along a path, round an axis.
    /// </summary>
    /// <remarks>
    /// Every volume below is worked out exactly for the polygon the curve is cut into, so the comparisons are to
    /// rounding, not to an allowance for faceting.
    /// </remarks>
    public class MakingBodiesTests
    {
        private static double RegularArea(int sides, double radius) => 0.5 * sides * radius * radius * Math.Sin(2.0 * Math.PI / sides);

        private static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1)
            => new GeoPolygon2(new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1));

        private static GeoPolygon3 Square(double size, bool clockwise = false)
        {
            var corners = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(size, 0, 0), new GeoPoint3(size, size, 0), new GeoPoint3(0, size, 0) };
            return new GeoPolygon3(clockwise ? corners.Reverse() : corners);
        }

        [Fact]
        public void ASquareExtrudedStraightUpIsABox()
        {
            GeoSolid3 box = GeoSolid3.Extrude(Square(100), new GeoVector3(0, 0, 50));

            Assert.Equal(500000.0, box.Volume, 6);
            Assert.True(box.GetSignedVolume() > 0);
            Assert.True(box.IsClosed());
            Assert.Equal(PointLocation.Inside, box.Locate(new GeoPoint3(50, 50, 25)));
            Assert.Equal(PointLocation.OutSide, box.Locate(new GeoPoint3(50, 50, 55)));
        }

        [Fact]
        public void TheWayRoundTheProfileIsDrawnDoesNotMatter()
        {
            GeoSolid3 drawnDown = GeoSolid3.Extrude(Square(100, clockwise: true), new GeoVector3(0, 0, 50));
            GeoSolid3 pushedDown = GeoSolid3.Extrude(Square(100), new GeoVector3(0, 0, -50));

            Assert.Equal(500000.0, drawnDown.GetSignedVolume(), 6);
            Assert.Equal(500000.0, pushedDown.GetSignedVolume(), 6);
            Assert.Equal(PointLocation.Inside, pushedDown.Locate(new GeoPoint3(50, 50, -25)));
        }

        [Fact]
        public void ALeaningPrismKeepsTheVolumeOfItsHeight()
        {
            GeoSolid3 leaning = GeoSolid3.Extrude(Square(100), new GeoVector3(0, 30, 40));

            Assert.Equal(100.0 * 100 * 40, leaning.Volume, 6);
            Assert.True(leaning.IsClosed());
        }

        [Fact]
        public void AHoleInTheFaceRunsThroughTheBody()
        {
            var hole = new GeoPolygon3(new GeoPoint3(40, 40, 0), new GeoPoint3(60, 40, 0), new GeoPoint3(60, 60, 0), new GeoPoint3(40, 60, 0));
            GeoSolid3 plate = GeoSolid3.Extrude(new GeoFace3(Square(100), new[] { hole }), new GeoVector3(0, 0, 10));

            Assert.Equal((10000.0 - 400.0) * 10, plate.Volume, 6);
            Assert.True(plate.IsClosed());
            Assert.Equal(PointLocation.OutSide, plate.Locate(new GeoPoint3(50, 50, 5)));
            Assert.Equal(PointLocation.Inside, plate.Locate(new GeoPoint3(20, 20, 5)));
        }

        [Fact]
        public void ADirectionInThePlaneIsRefused()
        {
            Assert.Throws<ArgumentException>(() => GeoSolid3.Extrude(Square(100), new GeoVector3(10, 0, 0)));
        }

        [Fact]
        public void AProfileDrawnInItsOwnPlaneRunsAlongThatPlanesZ()
        {
            // The plane's X is the world's Y and its Y the world's Z, so its Z is the world's X.
            var placement = new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), GeoVector3.YAxis, GeoVector3.ZAxis);
            GeoSolid3 member = GeoSolid3.Extrude(Rectangle(0, 0, 40, 20), placement, -15);

            Assert.Equal(40.0 * 20 * 15, member.Volume, 6);

            GeoAabb3 box = member.GetAabb();
            Assert.True(box.Min.DistanceTo(new GeoPoint3(-5, 20, 30)) < 1E-9);
            Assert.True(box.Max.DistanceTo(new GeoPoint3(10, 60, 50)) < 1E-9);
        }

        [Fact]
        public void ARoundedPlateComesOutOfItsCurvedOutline()
        {
            GeoPolygonArc2 outline = new GeoPolygonArc2(Rectangle(0, 0, 100, 100)).Fillet(10);
            var placement = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);
            GeoSolid3 plate = GeoSolid3.Extrude(outline, placement, 20, 0.001);

            // The fillets are cut into chords a thousandth from the arc, which takes a sliver off the true area:
            // under two thirds of a thousandth times the arc length, for each fillet.
            double sliver = 4 * (2.0 / 3.0) * 0.001 * (Math.PI / 2 * 10);
            Assert.InRange(plate.Volume, (outline.Area - sliver) * 20, outline.Area * 20);
            Assert.True(plate.IsClosed());
        }

        [Fact]
        public void ACylinderIsAPrismOnARegularPolygon()
        {
            GeoSolid3 bolt = GeoSolid3.Cylinder(new GeoPoint3(10, 20, 30), new GeoPoint3(70, -10, 90), 8, 48);
            double length = new GeoPoint3(10, 20, 30).DistanceTo(new GeoPoint3(70, -10, 90));

            Assert.Equal(RegularArea(48, 8) * length, bolt.Volume, 6);
            Assert.True(bolt.IsClosed());
            Assert.Equal(PointLocation.Inside, bolt.Locate(new GeoPoint3(40, 5, 60)));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSolid3.Cylinder(GeoPoint3.Origin, new GeoPoint3(0, 0, 1), 1, 2));
            Assert.Throws<ArgumentException>(() => GeoSolid3.Cylinder(GeoPoint3.Origin, GeoPoint3.Origin, 1, 8));
        }

        [Fact]
        public void APipeAlongAStraightPathIsItsSectionTimesItsLength()
        {
            var path = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(600, 800, 0));
            GeoSolid3 bar = GeoSolid3.Pipe(path, 8, 0.01);
            int sides = Tessellation.SegmentsForChordTolerance(8, 2 * Math.PI, 0.01);

            Assert.Equal(RegularArea(sides, 8) * 1000, bar.Volume, 5);
            Assert.True(bar.IsClosed());
        }

        [Fact]
        public void APathTurningUnderHalfADegreeIsSweptWhole()
        {
            // Two straight runs of a metre, the second turned 0.3 degrees off the first, as the bar of a wide curve
            // is drawn: the sine of the turn, 0.005, is under the default vector tolerance, and the bend was refused
            // as having no axis to turn about.
            double turn = 0.3 * Math.PI / 180.0;
            var path = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0), new GeoPoint3(1000 + 1000 * Math.Cos(turn), 1000 * Math.Sin(turn), 0));
            GeoSolid3 bar = GeoSolid3.Pipe(path, 8, 0.1);
            int sides = Tessellation.SegmentsForChordTolerance(8, 2 * Math.PI, 0.1);

            Assert.True(bar.IsClosed());

            // With the section centred on the path, the mitre adds outside the bend what it takes from inside.
            Assert.Equal(RegularArea(sides, 8) * 2000, bar.Volume, 3);
        }

        [Fact]
        public void AMitredCornerKeepsTheVolumeOfThePathLength()
        {
            // With the section centred on the path, the wedge a mitre adds outside a bend is the wedge it takes
            // from inside.
            var path = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(500, 0, 0), new GeoPoint3(500, 400, 0), new GeoPoint3(500, 400, 300));
            GeoSolid3 bar = GeoSolid3.Sweep(Rectangle(-10, -5, 10, 5), path);

            Assert.Equal(20.0 * 10 * 1200, bar.Volume, 5);
            Assert.True(bar.IsClosed());
        }

        [Fact]
        public void ABentBarFollowsItsBends()
        {
            GeoPolylineArc3 centreLine = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 300, 0), new GeoPoint3(400, 300, 250)).Fillet(50);
            GeoSolid3 bar = GeoSolid3.Pipe(centreLine, 8, 0.01);
            int sides = Tessellation.SegmentsForChordTolerance(8, 2 * Math.PI, 0.01);
            double length = centreLine.ToPolyline3(0.01).Length;

            Assert.Equal(RegularArea(sides, 8) * length, bar.Volume, 4);
            Assert.True(bar.IsClosed());

            // The middle of every bend is inside the bar, and the corner the bend rounds off is not.
            Assert.Equal(PointLocation.Inside, bar.Locate(centreLine.GetPointAtDistance(centreLine.Length / 2)));
            Assert.Equal(PointLocation.OutSide, bar.Locate(new GeoPoint3(400, 0, 0)));
        }

        [Fact]
        public void TheSectionStandsTheWayItIsToldTo()
        {
            var path = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            GeoPolygon2 wide = Rectangle(-10, -5, 10, 5);

            GeoAabb3 upright = GeoSolid3.Sweep(wide, path).GetAabb();
            Assert.Equal(20.0, upright.Max.Y - upright.Min.Y, 9);
            Assert.Equal(10.0, upright.Max.Z - upright.Min.Z, 9);

            GeoAabb3 onItsSide = GeoSolid3.Sweep(wide, path, GeoVector3.YAxis).GetAabb();
            Assert.Equal(10.0, onItsSide.Max.Y - onItsSide.Min.Y, 9);
            Assert.Equal(20.0, onItsSide.Max.Z - onItsSide.Min.Z, 9);

            Assert.Throws<ArgumentException>(() => GeoSolid3.Sweep(wide, path, GeoVector3.XAxis));
        }

        [Fact]
        public void APathTurningBackOrWithNoLengthIsRefused()
        {
            GeoPolygon2 section = Rectangle(-1, -1, 1, 1);

            Assert.Throws<ArgumentException>(() => GeoSolid3.Sweep(section, new GeoPolyline3(GeoPoint3.Origin, new GeoPoint3(100, 0, 0), new GeoPoint3(0, 1, 0))));
            Assert.Throws<ArgumentException>(() => GeoSolid3.Sweep(section, new GeoPolyline3(GeoPoint3.Origin, new GeoPoint3(0, 0, 1E-6))));
        }

        [Fact]
        public void ARectangleTurnedAWholeTurnIsATube()
        {
            var placement = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);
            GeoSolid3 tube = GeoSolid3.Revolve(Rectangle(10, 0, 20, 30), placement, 2 * Math.PI, 0.01);
            int steps = Tessellation.SegmentsForChordTolerance(20, 2 * Math.PI, 0.01);

            Assert.Equal(30 * (RegularArea(steps, 20) - RegularArea(steps, 10)), tube.Volume, 5);
            Assert.True(tube.IsClosed());
            Assert.Equal(PointLocation.Inside, tube.Locate(new GeoPoint3(0, 15, 15)));
            Assert.Equal(PointLocation.OutSide, tube.Locate(new GeoPoint3(0, 15, 0)));
        }

        [Fact]
        public void APartTurnHasTwoEnds()
        {
            var placement = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);
            GeoSolid3 quarter = GeoSolid3.Revolve(Rectangle(10, 0, 20, 30), placement, Math.PI / 2, 0.01);
            int steps = Tessellation.SegmentsForChordTolerance(20, Math.PI / 2, 0.01);
            double wedge = Math.Sin(Math.PI / 2 / steps) / 2;

            Assert.Equal(30 * steps * wedge * (20 * 20 - 10 * 10), quarter.Volume, 5);
            Assert.True(quarter.IsClosed());
        }

        [Fact]
        public void AProfileOnTheAxisTurnsIntoASolidPier()
        {
            var placement = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);
            GeoSolid3 pier = GeoSolid3.Revolve(Rectangle(0, 0, 10, 30), placement, 2 * Math.PI, 0.01);
            int steps = Tessellation.SegmentsForChordTolerance(10, 2 * Math.PI, 0.01);

            Assert.Equal(30 * RegularArea(steps, 10), pier.Volume, 5);
            Assert.True(pier.IsClosed());
            Assert.Equal(PointLocation.Inside, pier.Locate(new GeoPoint3(0, 15, 0)));
        }

        [Fact]
        public void AProfileAcrossTheAxisOrAnAngleOutOfRangeIsRefused()
        {
            var placement = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);

            Assert.Throws<ArgumentException>(() => GeoSolid3.Revolve(Rectangle(-5, 0, 10, 30), placement, Math.PI, 0.01));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSolid3.Revolve(Rectangle(5, 0, 10, 30), placement, 7.0, 0.01));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSolid3.Revolve(Rectangle(5, 0, 10, 30), placement, 0.0, 0.01));
        }
    }
}
