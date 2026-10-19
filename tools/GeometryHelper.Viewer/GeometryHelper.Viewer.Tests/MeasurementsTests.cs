using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class MeasurementsTests
    {
        private static string G10(double value) => value.ToString("G10", CultureInfo.InvariantCulture);

        [Fact]
        public void TheDistanceOfTwoPoints_IsTheLengthOfTheirDifference_AndItsPlanShareIsMeasuredToo()
        {
            // 3, 4 and 12 across, up and in height: 5 in plan, 13 in space.
            MeasureResult result = Measurements.Distance(new Vec3(1, 2, 3), new Vec3(4, 6, 15));

            Assert.Equal("distance", result.Kind);
            Assert.Equal(13.0, result.Value);
            Assert.Equal(5.0, result.SecondValue);
            Assert.Contains("Distance 13", result.Lines[0], StringComparison.Ordinal);
            Assert.Contains("dx 3, dy 4, dz 12", result.Lines[0], StringComparison.Ordinal);
            Assert.Contains("in plan 5", result.Lines[0], StringComparison.Ordinal);
        }

        [Fact]
        public void ADistanceFarFromTheOrigin_KeepsTheDigitsAFloatWouldLose()
        {
            // 0.0005 apart, 30 000 from the origin: a float holds neither end to a thousandth of a millimetre.
            Vec3 a = new Vec3(30000.1234567, -20000.7654321, 0.0);
            Vec3 b = new Vec3(30000.1234567 + 0.0005, -20000.7654321, 0.0);

            MeasureResult result = Measurements.Distance(a, b);

            Assert.Equal(0.0005, result.Value, 9);
            Assert.Contains(G10(result.Value), result.Lines[0], StringComparison.Ordinal);
        }

        [Fact]
        public void TheDistanceIsDrawnAsASegmentWithItsLengthWrittenAtTheMiddle()
        {
            MeasureResult result = Measurements.Distance(new Vec3(0, 0, 0), new Vec3(30, 40, 0));

            DrawPolyline line = Assert.Single(result.Drawing.Lines);
            Assert.Equal(new[] { new Vec3(0, 0, 0), new Vec3(30, 40, 0) }, line.Points);
            DrawLabel label = Assert.Single(result.Drawing.Labels);
            Assert.Equal("50", label.Text);
            Assert.Equal(new Vec3(15, 20, 0), label.At);
            Assert.Equal(0.0, label.Height);
            Assert.Equal(2, result.Drawing.Points.Count);
        }

        [Fact]
        public void AnAngleOfThreePoints_IsTheAngleAtTheMiddleOne_FromNoughtToAHalfTurn()
        {
            Assert.Equal(Math.PI / 2.0, Measurements.Angle(new Vec3(10, 0, 0), new Vec3(0, 0, 0), new Vec3(0, 10, 0)).Value, 15);
            Assert.Equal(Math.PI / 4.0, Measurements.Angle(new Vec3(1, 0, 0), new Vec3(0, 0, 0), new Vec3(1, 1, 0)).Value, 15);

            // The legs are 3 and 4 long in space; the angle is that of a 3-4-5 triangle's far corner, 36.87 degrees at the other end.
            MeasureResult three = Measurements.Angle(new Vec3(3, 0, 7), new Vec3(0, 0, 7), new Vec3(0, 4, 7));
            Assert.Equal(Math.PI / 2.0, three.Value, 15);

            MeasureResult triangle = Measurements.Angle(new Vec3(0, 0, 0), new Vec3(3, 0, 0), new Vec3(3, 4, 0));
            Assert.Equal(Math.PI / 2.0, triangle.Value, 15);
            MeasureResult corner = Measurements.Angle(new Vec3(3, 0, 0), new Vec3(0, 0, 0), new Vec3(3, 4, 0));
            Assert.Equal(Math.Atan2(4.0, 3.0), corner.Value, 15);
            Assert.Contains("deg", corner.Lines[0], StringComparison.Ordinal);
        }

        [Fact]
        public void AnAngleOfAHairLessThanAStraightLine_IsNotLostInTheArcCosine()
        {
            // 1E-9 short of straight: acos of the dot product would read it as a half turn.
            MeasureResult result = Measurements.Angle(new Vec3(1, 0, 0), new Vec3(0, 0, 0), new Vec3(-1, 1E-9, 0));

            Assert.Equal(Math.PI - 1E-9, result.Value, 15);
            Assert.Equal(0.0, Measurements.Angle(new Vec3(1, 0, 0), new Vec3(0, 0, 0), new Vec3(2, 0, 0)).Value);
        }

        [Fact]
        public void AnAngleIsDrawnAsItsTwoLegsAndAnArcAboutTheVertex_WithTheDegreesWritten()
        {
            MeasureResult result = Measurements.Angle(new Vec3(10, 0, 0), new Vec3(0, 0, 0), new Vec3(0, 20, 0));

            Assert.Equal(2, result.Drawing.Lines.Count);
            DrawPolyline arc = result.Drawing.Lines[1];
            double radius = 0.25 * 10.0;

            Assert.All(arc.Points, p => Assert.Equal(radius, p.Length, 12));
            Assert.Equal(new Vec3(radius, 0, 0), arc.Points.First());
            Assert.Equal(0.0, arc.Points.Last().X, 12);
            Assert.Equal(radius, arc.Points.Last().Y, 12);
            Assert.Equal("90 deg", Assert.Single(result.Drawing.Labels).Text);
        }

        [Fact]
        public void AnAngleWithAPointOnTheVertex_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => Measurements.Angle(new Vec3(1, 1, 1), new Vec3(1, 1, 1), new Vec3(2, 2, 2)));
        }

        [Fact]
        public void TheRadiusOfACircleAndOfAnArc_IsReadFromTheirNumbers()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoCircle2(new GeoPoint2(10, 20), 37.5))
                .Add(new GeoPolylineArc2(new[] { new GeoPoint2(0, 100), new GeoPoint2(100, 100), new GeoPoint2(100, 180) }, new[] { 0.5, 0.0, 0.0 })));

            SceneCurve circle = view.Geometry.Curves[0];
            MeasureResult round = Measurements.Radius(circle, new Vec3(10 + 37.5, 20, 0));
            Assert.Equal("radius", round.Kind);
            Assert.Equal(37.5, round.Value);
            Assert.Equal(2.0 * Math.PI * 37.5, round.SecondValue, 12);
            Assert.Contains("Radius 37.5", round.Lines[0], StringComparison.Ordinal);
            Assert.Contains("diameter 75", round.Lines[0], StringComparison.Ordinal);

            // A bulge of 0.5 over a chord of 100 is an arc of radius 62.5 turning through 4 atan 0.5 = 106.26 degrees.
            SceneCurve arc = view.Geometry.Curves.Single(c => c.Shape == CurveShape.Conic && c.ItemIndex == 1);
            MeasureResult bent = Measurements.Radius(arc, new Vec3(50, 75, 0));
            Assert.Equal(62.5, bent.Value, 12);
            Assert.Equal(62.5 * 4.0 * Math.Atan(0.5), bent.SecondValue, 12);
            Assert.Contains("swept", bent.Lines[2], StringComparison.Ordinal);
        }

        [Fact]
        public void TheRadiusIsDrawnFromTheCentreOutToTheCurve()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoCircle2(new GeoPoint2(0, 0), 40.0)));

            MeasureResult result = Measurements.Radius(view.Geometry.Curves[0], new Vec3(41, 0.5, 0));

            DrawPolyline line = Assert.Single(result.Drawing.Lines);
            Assert.Equal(new Vec3(0, 0, 0), line.Points[0]);
            Assert.Equal(40.0, line.Points[1].Length, 12);
            Assert.Equal("R 40", Assert.Single(result.Drawing.Labels).Text);
        }

        [Fact]
        public void AnEllipse_HasNoOneRadius_AndAsksForBothSemiAxes()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 0), 25.0, 10.0)));

            MeasureResult result = Measurements.Radius(view.Geometry.Curves[0], new Vec3(25, 0, 0));

            Assert.Equal(10.0, result.Value);
            Assert.Equal(25.0, result.SecondValue);
            Assert.Contains("no one radius", result.Lines[0], StringComparison.Ordinal);
        }

        [Fact]
        public void ASegment_HasNoRadius()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0))));

            Assert.Throws<ArgumentException>(() => Measurements.Radius(view.Geometry.Curves[0], new Vec3(5, 0, 0)));
            Assert.Throws<ArgumentNullException>(() => Measurements.Radius(null, new Vec3(5, 0, 0)));
        }

        [Fact]
        public void TheCurveNearestTheCursor_IsTheOneWhoseRadiusIsAsked_NotTheSegmentBeside()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(-100, 50), new GeoPoint2(100, 50)))
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 40.0)));

            (double x, double y) = view.Pixel(new Vec3(40, 0, 0));
            SceneCurve picked = PickEngine.PickConic(view.Geometry, view.Map, x + 2.0, y, null, out Vec3 at);

            Assert.Equal(1, picked.ItemIndex);
            Assert.Equal(40.0, at.Length, 12);
            Assert.Null(PickEngine.PickConic(view.Geometry, view.Map, view.Pixel(new Vec3(-60, 50, 0)).X, view.Pixel(new Vec3(-60, 50, 0)).Y, null, out _));
        }

        [Fact]
        public void TheLibraryDistanceOfTwoItems_IsAskedUnderTheSceneToleranceAndTheSegmentComesBackFromTheFirstToTheSecond()
        {
            var a = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var b = new GeoPolygon2(new GeoPoint2(30, 2), new GeoPoint2(40, 2), new GeoPoint2(40, 12), new GeoPoint2(30, 12));
            Scene scene = new SceneWriter().Add(a, name: "left").Add(b, name: "right").Scene;

            LibraryDistance forward = LibraryMeasure.Between(scene, 0, 1);
            LibraryDistance backward = LibraryMeasure.Between(scene, 1, 0);

            Assert.Equal(a.DistanceTo(b), forward.Distance.Value);
            Assert.Equal(20.0, forward.Distance.Value, 12);
            Assert.Equal(20.0, (forward.To.Value - forward.From.Value).Length, 12);
            Assert.Equal(10.0, forward.From.Value.X, 12);
            Assert.Equal(30.0, forward.To.Value.X, 12);
            Assert.Equal(30.0, backward.From.Value.X, 12);
            Assert.Equal(10.0, backward.To.Value.X, 12);
            Assert.Empty(forward.Notes);
            Assert.Equal("GeoPolygon2 'left'", forward.FirstName);
        }

        [Fact]
        public void TheLibraryDistance_IsAddedToAMeasurementWithItsSegmentDrawnApart()
        {
            var a = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            var b = new GeoLine3(new GeoPoint3(5, 3, 4), new GeoPoint3(5, 10, 4));
            Scene scene = new SceneWriter().Add(a).Add(b).Scene;

            MeasureResult measure = Measurements.Distance(new Vec3(0, 0, 0), new Vec3(5, 3, 4));
            measure.AddLibrary(LibraryMeasure.Between(scene, 0, 1));

            // The points are 7.07 apart; the lines, 5.
            Assert.Equal(Math.Sqrt(50.0), measure.Value, 12);
            Assert.Contains(measure.Lines, l => l.Contains("DistanceTo 5", StringComparison.Ordinal));
            Assert.Contains(measure.Lines, l => l.Contains("GetShortestLineTo 5 long", StringComparison.Ordinal));
            DrawPolyline library = Assert.Single(measure.Drawing.LibraryLines);
            Assert.Equal(new Vec3(5, 0, 0), library.Points[0]);
            Assert.Equal(new Vec3(5, 3, 4), library.Points[1]);
            Assert.Contains(measure.Drawing.Labels, l => l.Text == "library 5");
        }

        [Fact]
        public void TheLibraryIsAskedAtTheToleranceOfTheScene_NotTheOneOfTheProcess()
        {
            // A segment shorter than the point tolerance is read as its start point. At a tolerance of 1E-3 the 0.0004 segment is the point (0, 0);
            // at 1E-5 it is a segment, and the point above its end is 3 away.
            var shortLine = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0.0004, 0));
            var above = new GeoPoint2(0.0004, 3);

            Scene coarse = new SceneWriter().Add(shortLine).Add(above).Scene;
            Scene fine = new SceneWriter().Add(shortLine).Add(above).Scene;
            coarse.SetTolerance(new Tolerance(1E-3, 1E-5));
            fine.SetTolerance(new Tolerance(1E-5, 1E-5));

            double read = LibraryMeasure.Between(coarse, 0, 1).Distance.Value;
            double exact = LibraryMeasure.Between(fine, 0, 1).Distance.Value;

            Assert.Equal(3.0, exact, 12);
            Assert.NotEqual(exact, read);
            Assert.Equal(Math.Sqrt(9.0 + 0.0004 * 0.0004), read, 12);
        }

        [Fact]
        public void WhereTheLibraryHasNoCallBetweenTwoKinds_ThereIsANoteAndNoNumber()
        {
            Scene scene = new SceneWriter()
                .Add(new GeoPoint2(0, 0))
                .Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1)))
                .Text(new GeoPoint3(0, 0, 0), "label")
                .Scene;

            LibraryDistance mixed = LibraryMeasure.Between(scene, 0, 1);
            Assert.Null(mixed.Distance);
            Assert.Null(mixed.From);
            Assert.Contains(mixed.Notes, n => n.Contains("no DistanceTo between GeoPoint2 and GeoLine3", StringComparison.Ordinal));

            LibraryDistance label = LibraryMeasure.Between(scene, 0, 2);
            Assert.Null(label.Distance);
            Assert.NotEmpty(label.Notes);

            Assert.Throws<ArgumentOutOfRangeException>(() => LibraryMeasure.Between(scene, 0, 9));
        }
    }
}
