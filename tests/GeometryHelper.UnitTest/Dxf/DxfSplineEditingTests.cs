using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The editing of a <see cref="DxfSpline"/> as AutoCAD's: its NURBS and fit data as values, control and fit points put in and
    /// taken out, the degree raised, rebuilt, made to pass a point, cut, offset, run on past its ends and made a polyline.
    /// </summary>
    public class DxfSplineEditingTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>A cubic of four points from (0, 0, 0) to (30, 0, 0), over 0 to 1.</summary>
        private static DxfSpline Bezier() => new DxfSpline(3, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(20, 10, 0), new GeoPoint3(30, 0, 0) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null, 0.0, 0.0);

        /// <summary>The fixture's cubic of five points over 0 to 1 (R2000, handle 70), and its drawing.</summary>
        private static DxfSpline Fixture(out DxfDatabase db)
        {
            db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            return ByHandle<DxfSpline>(db, "70");
        }

        private static void SameCurve(DxfSpline expected, DxfSpline actual, Tolerance tolerance)
        {
            for (int i = 0; i <= 40; i++)
            {
                double t = expected.StartParam + (expected.EndParam - expected.StartParam) * i / 40;
                Assert.True(expected.GetPointAtParameter(t).IsEqualTo(actual.GetPointAtParameter(t), tolerance), "At " + t + ": " + expected.GetPointAtParameter(t) + " against " + actual.GetPointAtParameter(t));
            }
        }

        [Fact]
        public void TheNurbsAndFitDataGivenTheirOwnValues_LeaveTheFileByteForByte()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018, DxfReaderRoundTripTests.OneOfEach().ToString() })
            {
                SettingWhatItHasChangesNothing<DxfSpline>(text, spline =>
                {
                    spline.NurbsData = spline.NurbsData;
                    spline.FitData = spline.FitData;
                    spline.ElevateDegree(spline.Degree);
                });
            }
        }

        [Fact]
        public void TheNurbsData_IsTheRecordsDataInOneValue_AndSetAtOnce()
        {
            DxfSpline spline = Fixture(out DxfDatabase db);
            DxfNurbsData data = spline.NurbsData;

            Assert.Equal(3, data.Degree);
            Assert.False(data.Rational);
            Assert.Equal(spline.ControlPoints, data.GetControlPoints());
            Assert.Equal(spline.Knots, data.GetKnots());
            Assert.Empty(data.GetWeights());
            Assert.True(data.IsEqualTo(spline.NurbsData));

            var rational = new DxfNurbsData(2, true, false, false, new[] { new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0) }, new[] { 0.0, 0, 0, 1, 1, 1 }, new[] { 1.0, Math.Sqrt(0.5), 1.0 }, 1E-10, 1E-10);
            spline.NurbsData = rational;

            DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), "70");
            Assert.True(again.NurbsData.IsEqualTo(rational, Tight));
            Assert.True(again.GetPointAtParameter(0.5).IsEqualTo(new GeoPoint3(Math.Sqrt(50), Math.Sqrt(50), 0), Tight));
            Assert.Throws<ArgumentException>(() => spline.NurbsData = new DxfNurbsData(3, false, false, false, new[] { new GeoPoint3(0, 0, 0) }, new[] { 0.0 }, null, 0, 0));
        }

        [Fact]
        public void TheFitData_IsWrittenWithTheSplineThroughItsPoints_ItsKnotsTheChords()
        {
            // Through (0, 0, 0), (30, 40, 0) and (60, 0, 0): chords of 50 and 50, so the spline runs from 0 to 100.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfSpline spline = ByHandle<DxfSpline>(db, "6F");
            var points = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(30, 40, 0), new GeoPoint3(60, 0, 0) };

            spline.FitData = new DxfFitData(points, 3, 0.0);

            DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), "6F");
            Assert.Equal(3, again.NumFitPoints);
            Assert.Equal(DxfSplineType.FitPoints, again.Type);
            Assert.True(again.NumControlPoints > 0);
            Assert.Equal(0.0, again.StartParam);
            Assert.Equal(100.0, again.EndParam, 12);

            foreach (GeoPoint3 point in points)
            {
                Assert.True(again.GetClosestPointTo(point, false).IsEqualTo(point, Tight));
            }

            DxfFitData data = again.FitData;
            Assert.Equal(2, again.Degree);
            Assert.False(data.TangentsExist);
            Assert.Equal(DxfKnotParameterizationEnum.Chord, data.KnotParam);
            Assert.Throws<NotSupportedException>(() => again.FitData = new DxfFitData(points, 3, 0.0) { KnotParam = DxfKnotParameterizationEnum.Uniform });
            Assert.Throws<ArgumentException>(() => again.FitData = new DxfFitData(new[] { points[0] }, 3, 0.0));
        }

        [Fact]
        public void FitPointsPutInMovedAndTakenOut_FitTheSplineAnew_ThroughEachOfThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfSpline spline = ByHandle<DxfSpline>(db, "6F");
            int count = spline.NumFitPoints;

            spline.InsertFitPointAt(1, new GeoPoint3(3, 3, 0));
            spline.SetFitPointAt(0, new GeoPoint3(-1, 0, 0));
            Assert.Equal(count + 1, spline.NumFitPoints);
            spline.RemoveFitPointAt(count);
            Assert.Equal(count, spline.NumFitPoints);

            DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), "6F");

            foreach (GeoPoint3 point in again.FitPoints)
            {
                Assert.True(again.GetClosestPointTo(point, false).IsEqualTo(point, Tight));
            }

            Assert.Equal(Bits(new GeoPoint3(-1, 0, 0)), Bits(again.GetFitPointAt(0)));
            Assert.Throws<InvalidOperationException>(() => Bezier().InsertFitPointAt(0, new GeoPoint3(0, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => again.RemoveFitPointAt(count));
        }

        [Fact]
        public void TheFitDataUpdated_IsThePointsOfTheCurveAtItsKnots_ItsControlPointsKept()
        {
            DxfSpline spline = Fixture(out _);
            IReadOnlyList<GeoPoint3> control = spline.ControlPoints;

            spline.UpdateFitData();

            Assert.Equal(control, spline.ControlPoints);
            Assert.Equal(3, spline.NumFitPoints);
            Assert.True(spline.GetFitPointAt(1).IsEqualTo(spline.GetPointAtParameter(spline.Knots[4]), Tight));
            Assert.Equal(1.0, spline.StartFitTangent.Length, 12);
            Assert.True(spline.FitData.TangentsExist);
        }

        [Fact]
        public void TheDegreeRaised_KeepsTheCurveExactly()
        {
            // A cubic of 4 points raised to 4 has 5 points and the knots 0 five times and 1 five times.
            DxfSpline bezier = Bezier();
            DxfSpline raised = Bezier();

            raised.ElevateDegree(4);

            Assert.Equal(4, raised.Degree);
            Assert.Equal(5, raised.NumControlPoints);
            Assert.Equal(new[] { 0.0, 0, 0, 0, 0, 1, 1, 1, 1, 1 }, raised.Knots.ToArray());
            SameCurve(bezier, raised, Tight);

            // The fixture's cubic of one inner knot raised by two: the inner knot stands three times.
            DxfSpline spline = Fixture(out _);
            DxfSpline before = Fixture(out _);
            spline.ElevateDegree(5);
            Assert.Equal(5, spline.Degree);
            Assert.Equal(3, spline.Knots.Count(k => k == before.Knots[4]));
            Assert.Equal(9, spline.NumControlPoints);
            SameCurve(before, spline, Tight);
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.ElevateDegree(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.ElevateDegree(11));
        }

        [Fact]
        public void AControlPointPutIn_IsWhereItsKnotWorksMost_AndTakenOutAgain()
        {
            DxfSpline spline = Bezier();

            spline.InsertControlPointAt(0.5, new GeoPoint3(15, 20, 0), 1.0);

            Assert.Equal(5, spline.NumControlPoints);
            Assert.Equal(Bits(new GeoPoint3(15, 20, 0)), Bits(spline.GetControlPointAt(2)));
            Assert.Equal(new[] { 0.0, 0, 0, 0, 0.5, 1, 1, 1, 1 }, spline.Knots.ToArray());
            Assert.False(spline.IsRational);

            spline.RemoveControlPointAt(2);
            Assert.Equal(4, spline.NumControlPoints);
            Assert.Equal(new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, spline.Knots.ToArray());
            SameCurve(Bezier(), spline, Tight);

            spline.InsertControlPointAt(0.25, new GeoPoint3(5, 5, 5), 2.0);
            Assert.True(spline.IsRational);
            Assert.Equal(2.0, spline.WeightAt(2));
            Assert.Throws<InvalidOperationException>(() => Bezier().RemoveControlPointAt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Bezier().InsertControlPointAt(1.0, new GeoPoint3(0, 0, 0), 1.0));
        }

        [Fact]
        public void APointAndADirectionGiven_AreWhereTheSplinePassesThem()
        {
            DxfSpline spline = Fixture(out _);
            var point = new GeoPoint3(5, 5, 1);
            var direction = new GeoVector3(20, -10, 3);

            spline.ModifyPositionAndTangent(0.3, point, direction);

            Assert.True(spline.GetPointAtParameter(0.3).IsEqualTo(point, Tight));
            Assert.True(spline.GetFirstDerivative(0.3).IsEqualTo(direction, new Tolerance(1E-7, 1E-7)));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.ModifyPositionAndTangent(2.0, point, direction));
        }

        [Fact]
        public void ASplineRebuilt_RunsAlongTheOldCurve_OverTheSameDomain()
        {
            // The fixture's cubic of 5 points rebuilt as a cubic of 20 keeps within 0.003 of it; as a quadratic of 12,
            // within 0.05 of a curve 13 long; with its own counts it has its own degree and domain.
            DxfSpline before = Fixture(out _);
            DxfSpline same = Fixture(out _);
            DxfSpline dense = Fixture(out _);
            DxfSpline fewer = Fixture(out _);

            same.Rebuild(3, 5);
            dense.Rebuild(3, 20);
            fewer.Rebuild(2, 12);

            Assert.Equal(3, same.Degree);
            Assert.Equal(5, same.NumControlPoints);
            Assert.Equal(0.0, same.StartParam);
            Assert.Equal(1.0, same.EndParam);
            Assert.Equal(12, fewer.NumControlPoints);
            Assert.False(dense.HasFitData);
            Assert.True(before.StartPoint.IsEqualTo(fewer.StartPoint, Tight));
            Assert.True(before.EndPoint.IsEqualTo(fewer.EndPoint, Tight));

            for (int i = 0; i <= 50; i++)
            {
                GeoPoint3 on = dense.GetPointAtParameter(i / 50.0);
                Assert.True(before.GetClosestPointTo(on, false).DistanceTo(on) < 0.004, "Rebuilt of 20 points, the spline strays " + before.GetClosestPointTo(on, false).DistanceTo(on));
                GeoPoint3 near = fewer.GetPointAtParameter(i / 50.0);
                Assert.True(before.GetClosestPointTo(near, false).DistanceTo(near) < 0.05);
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => same.Rebuild(3, 3));
        }

        [Fact]
        public void ASplineCut_IsItsPartsExactly_AClosedOneRoundItsStart()
        {
            DxfSpline spline = Fixture(out _);
            spline.Layer = "0";
            IReadOnlyList<DxfCurve> pieces = spline.GetSplitCurves(new[] { 0.6, 0.3, 0.3, 1.0 });

            Assert.Equal(3, pieces.Count);
            var first = (DxfSpline)pieces[0];
            var last = (DxfSpline)pieces[2];
            Assert.Equal(0.0, first.StartParam);
            Assert.Equal(0.3, first.EndParam);
            Assert.Equal(0.6, last.StartParam);
            Assert.True(first.GetPointAtParameter(0.2).IsEqualTo(spline.GetPointAtParameter(0.2), Tight));
            Assert.True(last.GetPointAtParameter(0.8).IsEqualTo(spline.GetPointAtParameter(0.8), Tight));
            Assert.True(((DxfSpline)pieces[1]).StartPoint.IsEqualTo(spline.GetPointAtParameter(0.3), Tight));
            Assert.Null(first.Handle);

            // A circle of radius 10 made a spline, cut at a quarter and a half turn: the quarter, and the rest round its start.
            DxfSpline circle = new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10).Spline;
            IReadOnlyList<DxfCurve> parts = circle.GetSplitCurves(new[] { Math.PI / 2, Math.PI });
            Assert.Equal(2, parts.Count);
            Assert.True(parts[1].StartPoint.IsEqualTo(new GeoPoint3(-10, 0, 0), Tight));
            Assert.True(parts[1].EndPoint.IsEqualTo(new GeoPoint3(0, 10, 0), Tight));
            Assert.True(parts[1].GetPointAtParameter(2 * Math.PI).IsEqualTo(new GeoPoint3(10, 0, 0), Tight));
            Assert.Single(circle.GetSplitCurves(new[] { 1.0 }));
        }

        [Fact]
        public void ASplineOffset_IsTheCurveAtTheDistance_ToTheRightOfItsWay()
        {
            // A circle of radius 10 running counter-clockwise: to its right is outwards.
            DxfSpline circle = new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10).Spline;
            var outer = (DxfSpline)Assert.Single(circle.GetOffsetCurves(2.0));
            var inner = (DxfSpline)Assert.Single(circle.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(0, 0, -1), 2.0));

            for (int i = 0; i <= 30; i++)
            {
                double t = 2 * Math.PI * i / 30;
                Assert.True(Math.Abs(outer.GetPointAtParameter(t).DistanceTo(new GeoPoint3(0, 0, 0)) - 12.0) <= Tolerance.Default.EqualPoint);
                Assert.True(Math.Abs(inner.GetPointAtParameter(t).DistanceTo(new GeoPoint3(0, 0, 0)) - 8.0) <= Tolerance.Default.EqualPoint);
            }

            Assert.Equal(circle.StartParam, outer.StartParam);
            Assert.Equal(circle.EndParam, outer.EndParam);
            Assert.True(outer.Closed);
            Assert.Throws<ArgumentException>(() => circle.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(1, 0, 0), 2.0));
            var twisted = new DxfSpline(3, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 1), new GeoPoint3(1, 1, 0), new GeoPoint3(0, 1, 1) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null, 0, 0);
            Assert.Throws<InvalidOperationException>(() => twisted.GetOffsetCurves(1.0));
        }

        [Fact]
        public void ASplineRunOn_KeepsItsCurve_AndCarriesItsEndOn()
        {
            DxfSpline bezier = Bezier();
            DxfSpline longer = Bezier();

            longer.Extend(1.5);

            Assert.Equal(1.5, longer.EndParam);
            Assert.Equal(0.0, longer.StartParam);
            SameCurve(bezier, longer, Tight);
            Assert.True(longer.EndPoint.IsEqualTo(new GeoPoint3(45, -22.5, 0), Tight), longer.EndPoint.ToString());

            longer.Extend(-0.5);
            Assert.Equal(-0.5, longer.StartParam);
            Assert.True(longer.GetPointAtParameter(0.7).IsEqualTo(bezier.GetPointAtParameter(0.7), Tight));

            // A straight spline run on to a point on its line.
            var line = new DxfSpline(1, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, new[] { 0.0, 0, 10, 10 }, null, 0, 0);
            line.Extend(false, new GeoPoint3(15, 0, 0));
            Assert.True(line.EndPoint.IsEqualTo(new GeoPoint3(15, 0, 0), Tight));
            Assert.Equal(15.0, line.EndParam, 9);
            Assert.Throws<ArgumentException>(() => line.Extend(false, new GeoPoint3(20, 1, 0)));
            Assert.Throws<ArgumentException>(() => line.Extend(false, new GeoPoint3(5, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.Extend(5.0));
            Assert.Throws<InvalidOperationException>(() => new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10).Spline.Extend(7.0));
        }

        [Fact]
        public void ASplineWithACorner_IsOffsetPieceByPiece_ItsCornerRunOnAsAPolylinesIs()
        {
            // The L of degree 2 from (0, 0) along x to (10, 0), then up to (10, 10), its corner the knot 1 standing twice: offset
            // by 1 to its right, it runs along y = -1 to (11, -1), up x = 11, within the point tolerance all along.
            var l = new DxfSpline(2, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 5, 0), new GeoPoint3(10, 10, 0) }, new[] { 0.0, 0, 0, 1, 1, 2, 2, 2 }, null, 0, 0);
            var offset = (DxfSpline)Assert.Single(l.GetOffsetCurves(1.0));
            double worst = 0.0;

            for (int i = 0; i <= 400; i++)
            {
                GeoPoint3 q = offset.GetPointAtParameter(offset.StartParam + (offset.EndParam - offset.StartParam) * i / 400);
                double along = Math.Min(Segment(q, 0, -1, 11, -1), Segment(q, 11, -1, 11, 10));
                worst = Math.Max(worst, along);
            }

            Assert.True(worst <= Tolerance.Default.EqualPoint, "The offset strays " + worst);
            Assert.True(offset.StartPoint.IsEqualTo(new GeoPoint3(0, -1, 0), Tight));
            Assert.True(offset.EndPoint.IsEqualTo(new GeoPoint3(11, 10, 0), Tight));
            Assert.True(offset.GetClosestPointTo(new GeoPoint3(11, -1, 0), false).IsEqualTo(new GeoPoint3(11, -1, 0), Tight));

            // Offset into the inside of the corner by 1: the pieces cut back to where they cross, at (9, 1).
            var inner = (DxfSpline)Assert.Single(l.GetOffsetCurves(-1.0));
            Assert.True(inner.GetClosestPointTo(new GeoPoint3(9, 1, 0), false).IsEqualTo(new GeoPoint3(9, 1, 0), new Tolerance(1E-6, 1E-6)));
            Assert.True(inner.EndPoint.IsEqualTo(new GeoPoint3(9, 10, 0), Tight));
        }

        [Fact]
        public void ARationalSplineRunOnUntilAWeightReachesNought_IsRefused()
        {
            // The weights 1, 5, 0.01 carried on to twice the span: the last point's weight would be below nought.
            var spline = new DxfSpline(2, true, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(5, 5, 0), new GeoPoint3(10, 0, 0) }, new[] { 0.0, 0, 0, 1, 1, 1 }, new[] { 1.0, 5.0, 0.01 }, 0, 0);

            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => spline.Extend(2.0));
            Assert.Contains("weight", refused.Message);
            Assert.Equal(1.0, spline.EndParam);
            spline.Extend(1.0005);
            Assert.Equal(1.0005, spline.EndParam);
        }

        [Fact]
        public void APolylineOfTheHighestPrecision_IsHeldToAFloor_OfAMillionthOfTheSize()
        {
            DxfSpline spline = Bezier();
            DxfCurve fine = spline.ToPolylineWithPrecision(99, false, true);
            DxfCurve floor = spline.ToPolylineWithPrecision(30, false, true);

            Assert.True(((DxfPolyline)fine).NumberOfVertices < 2000, "Precision 99 gave " + ((DxfPolyline)fine).NumberOfVertices + " vertices.");
            Assert.Equal(((DxfPolyline)floor).NumberOfVertices, ((DxfPolyline)fine).NumberOfVertices);
        }

        private static double Segment(GeoPoint3 q, double ax, double ay, double bx, double by)
            => new GeoLine3(new GeoPoint3(ax, ay, 0), new GeoPoint3(bx, by, 0)).GetClosestPointOnBoundary(q).DistanceTo(q);

        [Fact]
        public void ASplineMadeAPolyline_RunsAlongIt_OfArcsWhereItIsFlat()
        {
            // A quarter of a circle of radius 100 made a spline, given 3 vertices and arcs: two arcs of radius 100.
            DxfSpline quarter = new DxfArc(new GeoPoint3(0, 0, 0), 100, 0, Math.PI / 2).Spline;
            var polyline = Assert.IsType<DxfPolyline>(quarter.ToPolyline(3));

            Assert.Equal(3, polyline.NumberOfVertices);
            Assert.True(polyline.GetPoint3dAt(1).IsEqualTo(new GeoPoint3(100 * Math.Sqrt(0.5), 100 * Math.Sqrt(0.5), 0), Tight));
            Assert.Equal(Math.Tan(Math.PI / 16), polyline.GetBulgeAt(0), 9);
            Assert.Equal(Math.PI * 50, polyline.Length, 6);

            DxfSpline spline = Fixture(out _);
            var lines = Assert.IsType<DxfPolyline3d>(spline.ToPolylineWithPrecision(20, false, true));
            double size = Tolerance.Default.EqualPoint;

            for (int i = 0; i + 1 < lines.Count(); i++)
            {
                GeoPoint3 middle = lines.GetPointAtParameter(i + 0.5);
                Assert.True(spline.GetClosestPointTo(middle, false).DistanceTo(middle) < size, "The segment " + i + " strays " + spline.GetClosestPointTo(middle, false).DistanceTo(middle));
            }

            DxfSpline flat = Bezier();
            Assert.IsType<DxfPolyline2d>(flat.ToPolyline(true, false));
            var lw = Assert.IsType<DxfPolyline>(flat.ToPolyline());
            Assert.True(lw.EndPoint.IsEqualTo(new GeoPoint3(30, 0, 0), Tight));
            Assert.Throws<ArgumentOutOfRangeException>(() => flat.ToPolylineWithPrecision(100));
            Assert.Throws<ArgumentOutOfRangeException>(() => flat.ToPolyline(1));
        }

        [Fact]
        public void ASplineEdited_InADrawing_IsItsOwnRecordWrittenAnew()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfSpline spline = ByHandle<DxfSpline>(db, "70");

            spline.ElevateDegree(4);
            spline.InsertControlPointAt(0.7, new GeoPoint3(8, 2, 0), 1.0);
            spline.Extend(1.2);

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "70");
            DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(saved), "70");
            Assert.Equal(4, again.Degree);
            Assert.Equal(1.2, again.EndParam);
            Assert.True(again.NurbsData.IsEqualTo(spline.NurbsData, Tight));
        }

        /// <summary>A cubic of 203 control points (i * 100, a random height to 100), seed 7, over the knots 0 to 200.</summary>
        private static DxfSpline LongWave()
        {
            var random = new Random(7);
            var points = Enumerable.Range(0, 203).Select(i => new GeoPoint3(i * 100, random.NextDouble() * 100, 0)).ToList();
            var knots = new List<double> { 0, 0, 0 };
            knots.AddRange(Enumerable.Range(0, 201).Select(k => (double)k));
            knots.AddRange(new double[] { 200, 200, 200 });
            return new DxfSpline(3, false, false, false, points, knots, null, 0.0, 0.0);
        }

        [Fact]
        public void APolylineOfACountOfVerticesAlongALongSpline_HasThemEvenlyAlongItsLength()
        {
            DxfSpline spline = LongWave();
            double length = spline.GetDistanceAtParameter(spline.EndParam);

            var polyline = (DxfPolyline)spline.ToPolyline(100, false, true);

            Assert.Equal(100, polyline.NumberOfVertices);

            for (int i = 0; i < 100; i++)
            {
                double along = spline.GetDistAtPoint(polyline.GetPoint3dAt(i));
                Assert.True(Math.Abs(along - length * i / 99) <= Tolerance.Default.EqualPoint, i + ": " + along + " against " + (length * i / 99));
            }
        }

        [Fact]
        public void ASplineRebuiltOfThousandsOfControlPoints_KeepsToTheCurve()
        {
            DxfSpline spline = LongWave();
            DxfSpline rebuilt = LongWave();

            rebuilt.Rebuild(3, 3000);

            Assert.Equal(3000, rebuilt.NumControlPoints);

            for (int i = 0; i <= 400; i++)
            {
                GeoPoint3 point = spline.GetPointAtParameter(i * 0.5);
                Assert.True(rebuilt.GetClosestPointTo(point, false).DistanceTo(point) <= 0.01, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }
}
