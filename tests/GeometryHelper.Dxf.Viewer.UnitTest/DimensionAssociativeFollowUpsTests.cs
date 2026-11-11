using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Associative dimensions as AutoCAD 2021 makes and follows them, beyond the ties of the earlier rounds: a perpendicular
    /// whose foot lies past the end of its line, the angle of a circle, the arc of an angle put a third of the way round, the
    /// words of a radius kept where they stand, and the groups 11 and 42 written as the dimensions follow. The numbers are
    /// those AutoCAD 2021's console gave for the same drawings.
    /// </summary>
    public class DimensionAssociativeFollowUpsTests
    {
        private static void SnapFrom(CadCommandSession session, CadSnapModes mode, GeoPoint2 cursor)
        {
            var snaps = new ObjectSnap(Tight) { Modes = mode };
            CadSnapResult result = snaps.Snap(
                session.Document.GetPickIndex(session.Document.ModelSpace),
                cursor,
                1.0,
                session.Prompt.BasePoint,
                session.Prompt.SnapFrom
            );
            Assert.NotEqual(CadSnapKind.None, result.Kind);
            Assert.True(session.Feed(result, false));
        }

        private static string Handle<T>(CadDocument document, Func<T, bool> which)
            where T : DxfEntity => All<T>(document.ModelSpace).Single(which).Handle;

        private static string Last(CadDocument document) => document.ModelSpace.Block.Last(e => !e.IsErased).Handle;

        private static double Written42(CadDocument document, string handle)
        {
            string[] lines = document.Database.ToText().Split('\n');

            for (int i = 0; i + 3 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 2].Trim() != "5" || lines[i + 3].Trim() != handle)
                {
                    continue;
                }

                for (int j = i + 2; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                {
                    if (lines[j].Trim() == "42")
                    {
                        return double.Parse(lines[j + 1].Trim(), CultureInfo.InvariantCulture);
                    }
                }
            }

            return double.NaN;
        }

        private static void Move(CadCommandSession session, string handle, double x, double y)
        {
            session.Document.Selection.Set(new[] { handle });
            Run(session, "MOVE", P(0, 0), P(x, y));
        }

        [Fact]
        public void APerpendicularSnap_GivesTheFootOnALineRunOnPastItsEnd_ButNoneOffTheRunOfAnArc_AsAutoCad()
        {
            // AutoCAD 2021: _per to (100, -20)-(100, 60) from (20, 120) gives (100, 120); to the quarter arc of 50 about
            // (300, 300) from (380, 250), whose feet lie off the arc, "No Perpendicular found for specified point."
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(100, -20), P(100, 60), Enter, "ARC", "C", P(300, 300), P(350, 300), P(300, 350));
            var snaps = new ObjectSnap(Tight) { Modes = CadSnapModes.Perpendicular };
            var index = document.GetPickIndex(document.ModelSpace);

            CadSnapResult foot = snaps.Snap(index, P(100.3, 10), 1.0, P(20, 120));
            CadSnapResult before = snaps.Snap(index, P(100.3, 10), 1.0, P(20, -50));
            CadSnapResult arc = snaps.Snap(index, P(335.36, 335.36), 1.0, P(380, 250));

            Assert.Equal(CadSnapKind.Perpendicular, foot.Kind);
            Near(P(100, 120), foot.Point);
            Near(P(100, -50), before.Point);
            Assert.NotEqual(CadSnapKind.Perpendicular, arc?.Kind ?? CadSnapKind.None);
        }

        [Fact]
        public void APerpendicularPastTheEndOfALine_IsTiedAtItsParameter_AndGoesToTheFootFromTheOriginWhenTheLineMoves_AsAutoCad()
        {
            // AutoCAD 2021: LASTPOINT (20, 120), DIMLINEAR _per to (100, -20)-(100, 60), then (40, 0) and (70, 160): the first
            // origin (100, 120) tied as Perpendicular of parameter 1.75 drawn from (20, 120); the line moved by (20, 7), the origin
            // went to (120, 0), the line of the dimension to y = 40, measuring 80.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(100, -20), P(100, 60), Enter, "POINT", P(20, 120));
            string upright = Handle<DxfLine>(document, l => true);
            Run(session, "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Perpendicular, P(100.3, 10));
            Run(session, P(40, 0), P(70, 160));
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Near(new GeoPoint3(100, 120, 0), dimension.XLine1Point);

            DxfOsnapPointRef foot = Assert.Single(dimension.DimAssoc.PointReferences);
            Assert.Equal(DxfOsnapType.Perpendicular, foot.OsnapType);
            Assert.Equal(upright, foot.ObjectHandle);
            Assert.Equal(1.75, foot.NearParameter, 9);
            Assert.Null(foot.LastPointReference.ObjectHandle);
            Near(new GeoPoint3(20, 120, 0), foot.LastPointReference.Point);
            AssertReopens(document);

            Move(session, upright, 20, 7);

            dimension = Entity<DxfRotatedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(120, 0, 0), dimension.XLine1Point);
            Assert.Equal(40, dimension.DimLinePoint.Y, 9);
            Assert.Equal(80, Written42(document, dimension.Handle), 9);
            Near(new GeoPoint3(80, 40, 0), dimension.TextPosition, 1E-6);
        }

        [Fact]
        public void ASecondOriginPerpendicularPastTheEndOfALine_DrawnFromATiedFirst_IsDrawnAgainFromIt_AsAutoCad()
        {
            // AutoCAD 2021: DIMLINEAR _end (0, 200) of a polyline, _per to (100, -20)-(100, 60): the second origin (100, 200)
            // tied as Perpendicular of parameter 2.75 with the polyline's start as its last point; the line moved by (20, 7), it
            // went to (120, 200), and to (120, 205) when the polyline moved by (10, 5).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(100, -20), P(100, 60), Enter);
            string upright = Last(document);
            Run(session, "PLINE", P(0, 200), P(100, 200), "A", P(100, 300), "L", P(0, 300), Enter);
            string polyline = Last(document);
            Run(session, "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Endpoint, P(0.3, 200.2));
            SnapFrom(session, CadSnapModes.Perpendicular, P(100.3, 10));
            Run(session, P(50, 240));
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Near(new GeoPoint3(100, 200, 0), dimension.XLine2Point);

            DxfOsnapPointRef foot = dimension.DimAssoc.PointReferences.Single(r => r.PointIndex == 1);
            Assert.Equal(2.75, foot.NearParameter, 9);
            Assert.Equal(DxfOsnapType.StartPoint, foot.LastPointReference.OsnapType);
            Assert.Equal(polyline, foot.LastPointReference.ObjectHandle);

            Move(session, upright, 20, 7);
            Near(new GeoPoint3(120, 200, 0), Entity<DxfRotatedDimension>(document, dimension.Handle).XLine2Point);

            Move(session, polyline, 10, 5);
            dimension = Entity<DxfRotatedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(10, 205, 0), dimension.XLine1Point);
            Near(new GeoPoint3(120, 205, 0), dimension.XLine2Point);
            Assert.Equal(110, Written42(document, dimension.Handle), 9);
        }

        [Fact]
        public void APerpendicularPastTheEndOfAPolylinesSegment_IsTiedToThatSegment_AsAutoCad()
        {
            // AutoCAD 2021: LASTPOINT (150, -450), DIMLINEAR _per to the first segment (0, -400)-(100, -400) of a polyline: the
            // origin (150, -400) tied as Perpendicular of marker 1 and parameter 1.5; the polyline moved by (10, 5), the origin
            // went to (0, -395), the foot from the origin of the world.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PLINE", P(0, -400), P(100, -400), "A", P(100, -300), "L", P(0, -300), Enter);
            string polyline = Last(document);
            Run(session, "POINT", P(150, -450), "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Perpendicular, P(50, -399.7));
            Run(session, P(150, -500), P(200, -450));
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Near(new GeoPoint3(150, -400, 0), dimension.XLine1Point);

            DxfOsnapPointRef foot = Assert.Single(dimension.DimAssoc.PointReferences);
            Assert.Equal(polyline, foot.ObjectHandle);
            Assert.Equal(1, foot.GsMarker);
            Assert.Equal(1.5, foot.NearParameter, 9);

            Move(session, polyline, 10, 5);

            Near(new GeoPoint3(0, -395, 0), Entity<DxfRotatedDimension>(document, dimension.Handle).XLine1Point);
        }

        [Fact]
        public void DimAngularOfACircle_TiesThePointPickedTheSecondPointAndTheCenter_AndPutsItsArcAThirdOfTheWayRound_AsAutoCad()
        {
            // AutoCAD 2021: DIMANGULAR of the circle of 25 about (200, 0) picked at (225, 0), _end (300, 40) of the line from
            // (260, 40), the arc through (290, 20): 13 (225, 0), 14 (223.211917, 9.284767) on the circle, 10 (291.454853,
            // 11.662324), 42 0.380506; tied as Nearest of the circle at 0, Nearest of the line at -0.919702, Center.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, 0), "25", "LINE", P(260, 40), P(300, 40), Enter);
            string circle = Handle<DxfCircle>(document, c => true);
            string line = Handle<DxfLine>(document, l => true);
            Run(session, "DIMANGULAR", P(225, 0));
            Assert.Equal("Specify second angle endpoint:", session.Prompt.Text);
            SnapFrom(session, CadSnapModes.Endpoint, P(299.8, 40.2));
            Run(session, P(290, 20));
            var angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));

            Near(new GeoPoint3(225, 0, 0), angle.XLine1Point, 1E-6);
            Near(new GeoPoint3(223.211917, 9.284767, 0), angle.XLine2Point, 1E-6);
            Near(new GeoPoint3(200, 0, 0), angle.CenterPoint);
            Near(new GeoPoint3(291.454853, 11.662324, 0), angle.ArcPoint, 1E-6);
            Assert.Equal(0.380506, Written42(document, angle.Handle), 6);
            DxfOsnapPointRef[] ties = angle.DimAssoc.PointReferences.OrderBy(r => r.PointIndex).ToArray();
            Assert.Equal(3, ties.Length);
            Assert.Equal(DxfOsnapType.Nearest, ties[0].OsnapType);
            Assert.Equal(circle, ties[0].ObjectHandle);
            Assert.Equal(0.0, ties[0].NearParameter, 9);
            Assert.Equal(DxfOsnapType.Nearest, ties[1].OsnapType);
            Assert.Equal(line, ties[1].ObjectHandle);
            Assert.Equal(-0.919702, ties[1].NearParameter, 6);
            Assert.Equal(DxfOsnapType.Center, ties[2].OsnapType);
            Assert.Equal(circle, ties[2].ObjectHandle);
            AssertReopens(document);
        }

        [Fact]
        public void TheCircleOfAnAngleMoved_CarriesItsPointAndCenter_AndDropsASecondPointPastItsLine_AsAutoCad()
        {
            // AutoCAD 2021: the angle above, the circle moved 10 down: 13 (225, -10), 14 kept at (223.211917, 9.284767), 10
            // (289.74479, 11.115699), 42 0.693249, the tie of the second point, past the end of its line, dropped.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, 0), "25", "LINE", P(260, 40), P(300, 40), Enter);
            string circle = Handle<DxfCircle>(document, c => true);
            Run(session, "DIMANGULAR", P(225, 0));
            SnapFrom(session, CadSnapModes.Endpoint, P(299.8, 40.2));
            Run(session, P(290, 20));
            string angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace)).Handle;

            Move(session, circle, 0, -10);

            var moved = Entity<DxfPoint3AngularDimension>(document, angle);
            Near(new GeoPoint3(225, -10, 0), moved.XLine1Point, 1E-6);
            Near(new GeoPoint3(223.211917, 9.284767, 0), moved.XLine2Point, 1E-6);
            Near(new GeoPoint3(289.74479, 11.115699, 0), moved.ArcPoint, 1E-6);
            Assert.Equal(0.693249, Written42(document, angle), 6);
            Assert.DoesNotContain(moved.DimAssoc.PointReferences, r => r.PointIndex == 1);
            Assert.Equal(2, moved.DimAssoc.PointReferences.Count);
        }

        [Fact]
        public void DimAngularOfACircle_BringsAPickOffItAndASecondPointSnappedToNothingOntoIt_AndTiesBothToIt_AsAutoCad()
        {
            // AutoCAD 2021: the circle picked at (200, 25.2), the second point (150, -50) with no snap, the arc through (120, 30):
            // 13 (200, 25), 14 (182.32233, -17.67767), the second tied as Nearest of the circle at 3.926991, 10 (139.58477,
            // 60.41523), 42 2.356194; the circle moved 10 down, 14 went to (182.32233, -27.67767) and 10 to (139.58477, 50.41523).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, 0), "25");
            string circle = Last(document);
            Run(session, "DIMANGULAR", P(200, 25.2), P(150, -50), P(120, 30));
            var angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));

            Near(new GeoPoint3(200, 25, 0), angle.XLine1Point, 1E-6);
            Near(new GeoPoint3(182.32233, -17.67767, 0), angle.XLine2Point, 1E-5);
            Near(new GeoPoint3(139.58477, 60.41523, 0), angle.ArcPoint, 1E-5);
            Assert.Equal(2.356194, angle.Measurement, 6);
            DxfOsnapPointRef second = angle.DimAssoc.PointReferences.Single(r => r.PointIndex == 1);
            Assert.Equal(circle, second.ObjectHandle);
            Assert.Equal(3.926991, second.NearParameter, 6);

            Move(session, circle, 0, -10);

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(182.32233, -27.67767, 0), angle.XLine2Point, 1E-5);
            Near(new GeoPoint3(139.58477, 50.41523, 0), angle.ArcPoint, 1E-5);
        }

        [Fact]
        public void TheSecondPointOfTheAngleOfACircle_FollowsItsLine_AndTheArcStaysInsideTheAngleAsTheLegsCross_AsAutoCad()
        {
            // AutoCAD 2021: the circle of 25 about (200, -200) picked at (200, -175), _nea to (180, -160)-(300, -160) at (250, -160),
            // the arc through (260, -150): the second point tied as Nearest of the line at 0.329348. The line moved 20 up: 14
            // (219.52172, -140), 10 (216.258795, -123.608563), 42 0.314559; then 30 to the left, the second leg crossing the
            // first: 14 (189.52172, -140), 10 (195.50133, -122.027172), 42 0.172894.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, -200), "25", "LINE", P(180, -160), P(300, -160), Enter);
            string line = Handle<DxfLine>(document, l => true);
            Run(session, "DIMANGULAR", P(200, -175));
            SnapFrom(session, CadSnapModes.Nearest, P(250, -160.2));
            Run(session, P(260, -150));
            var angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));
            Assert.Equal(0.329348, angle.DimAssoc.PointReferences.Single(r => r.PointIndex == 1).NearParameter, 6);
            Assert.Equal(0.896055, angle.Measurement, 6);

            Move(session, line, 0, 20);

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(219.52172, -140, 0), angle.XLine2Point, 1E-5);
            Near(new GeoPoint3(216.258795, -123.608563, 0), angle.ArcPoint, 1E-5);
            Assert.Equal(0.314559, Written42(document, angle.Handle), 6);

            Move(session, line, -30, 0);

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(189.52172, -140, 0), angle.XLine2Point, 1E-5);
            Near(new GeoPoint3(195.50133, -122.027172, 0), angle.ArcPoint, 1E-5);
            Assert.Equal(0.172894, Written42(document, angle.Handle), 6);
        }

        [Fact]
        public void TheSecondPointOfTheAngleOfACircle_SnappedToAnotherCircle_IsTiedToItsPointAtNought_AsAutoCad()
        {
            // AutoCAD 2021: the circle of 25 about (200, -200) picked at (175, -200), _cen of the circle of 10 about (300, -300):
            // the second point tied as Nearest of that circle at 0; the first circle moved by (10, 0), the point went to
            // (310, -300), and to (310, -280) with the second circle moved 20 up: 10 (137.656589, -277.887296), 42 2.466852.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, -200), "25");
            string first = Last(document);
            Run(session, "CIRCLE", P(300, -300), "10");
            string other = Last(document);
            Run(session, "DIMANGULAR", P(175, -200));
            SnapFrom(session, CadSnapModes.Center, P(290.2, -300));
            Run(session, P(130, -280));
            var angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));
            DxfOsnapPointRef second = angle.DimAssoc.PointReferences.Single(r => r.PointIndex == 1);
            Assert.Equal(other, second.ObjectHandle);
            Assert.Equal(0.0, second.NearParameter, 9);

            Move(session, first, 10, 0);
            Near(new GeoPoint3(310, -300, 0), Entity<DxfPoint3AngularDimension>(document, angle.Handle).XLine2Point);

            Move(session, other, 0, 20);

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(310, -280, 0), angle.XLine2Point);
            Near(new GeoPoint3(137.656589, -277.887296, 0), angle.ArcPoint, 1E-5);
            Assert.Equal(2.466852, Written42(document, angle.Handle), 6);
        }

        [Fact]
        public void TheSecondPointOfTheAngleOfACircle_TiedToAPolylineOrAPoint_IsDroppedWhenItMoves_ThePointStaying_AsAutoCad()
        {
            // AutoCAD 2021: the circle of 25 about (0, -200) picked at (0, -175), _end of the polyline's vertex (-60, -110): the
            // second point (-13.867505, -179.198743) tied as Nearest of segment 1 at 1.678419; the polyline moved 10 up, the tie
            // dropped and the point kept. A POINT snapped with _nod is tied at 0 and dropped the same way.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(0, -200), "25", "PLINE", P(-100, -120), P(-60, -110), P(-40, -160), Enter);
            string polyline = Last(document);
            Run(session, "POINT", P(-80, -260));
            string point = Last(document);
            Run(session, "DIMANGULAR", P(0, -175));
            SnapFrom(session, CadSnapModes.Endpoint, P(-60.2, -110.2));
            Run(session, P(-30, -150));
            string toPolyline = Last(document);
            Run(session, "DIMANGULAR", P(-25, -200));
            SnapFrom(session, CadSnapModes.Node, P(-80.2, -260.2));
            Run(session, P(-60, -240));
            string toPoint = Last(document);
            DxfOsnapPointRef segment = Entity<DxfPoint3AngularDimension>(document, toPolyline)
                .DimAssoc.PointReferences.Single(r => r.PointIndex == 1);
            Assert.Equal(polyline, segment.ObjectHandle);
            Assert.Equal(1, segment.GsMarker);
            Assert.Equal(1.678419, segment.NearParameter, 6);
            Assert.Equal(
                point,
                Entity<DxfPoint3AngularDimension>(document, toPoint)
                    .DimAssoc.PointReferences.Single(r => r.PointIndex == 1)
                    .ObjectHandle
            );

            Move(session, polyline, 0, 10);
            Move(session, point, 0, 10);

            foreach (string handle in new[] { toPolyline, toPoint })
            {
                var angle = Entity<DxfPoint3AngularDimension>(document, handle);
                Assert.Equal(new[] { 0, 2 }, angle.DimAssoc.PointReferences.Select(r => r.PointIndex).OrderBy(i => i));
            }

            Near(
                new GeoPoint3(-13.867505, -179.198743, 0),
                Entity<DxfPoint3AngularDimension>(document, toPolyline).XLine2Point,
                1E-5
            );
            Near(new GeoPoint3(-20, -215, 0), Entity<DxfPoint3AngularDimension>(document, toPoint).XLine2Point, 1E-6);
        }

        [Theory]
        [InlineData(100, 500, 0, 600, 50, 550, 61.237244, 535.355339, 1.570796)]
        [InlineData(100, 500, 0, 600, -50, 450, -70.710678, 500, 4.712389)]
        [InlineData(0, 600, 100, 500, 50, 550, 61.237244, 535.355339, 1.570796)]
        [InlineData(200, 500, 0, 550, 95, 520, 84.075859, 548.54122, 1.570796)]
        public void AnAngleOfThreePoints_PutsItsArcAThirdOfTheWayRoundFromTheLegItStartsAt_AsFarOutAsThePointGiven_AsAutoCad(
            double x1,
            double y1,
            double x2,
            double y2,
            double atX,
            double atY,
            double arcX,
            double arcY,
            double measured
        )
        {
            // AutoCAD 2021: DIMANGULAR of the vertex (0, 500) and the two points, the arc put at the point given: group 10 a
            // third of the way round from the leg the arc starts at, counter-clockwise, as far out as the point given.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DIMANGULAR", Enter, P(0, 500), P(x1, y1), P(x2, y2), P(atX, atY));
            var angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));

            Near(new GeoPoint3(arcX, arcY, 0), angle.ArcPoint, 1E-6);
            Assert.Equal(measured, Written42(document, angle.Handle), 6);
        }

        [Fact]
        public void AnAngleOfAnArcAndOfTwoLines_PutTheirArcsAThirdOfTheWayRound_AndKeepItAsTheyFollow_AsAutoCad()
        {
            // AutoCAD 2021: DIMANGULAR of the quarter arc of 50 about (300, 300), the arc through (330, 330): 10 (336.742346,
            // 321.213203); of (500, 0)-(600, 0) and (500, 0)-(560, 80) through (560, 30): 16 (563.902902, 20.406349); the end of
            // the second line stretched by (-40, 0): 16 (560.637063, 28.69053), 42 1.325818.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "ARC", "C", P(300, 300), P(350, 300), P(300, 350));
            Run(session, "LINE", P(500, 0), P(600, 0), Enter, "LINE", P(500, 0), P(560, 80), Enter);
            Run(session, "DIMANGULAR", P(300, 350), P(330, 330));
            Run(session, "DIMANGULAR", P(550, 0), P(530, 40), P(560, 30));

            var ofArc = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));
            var ofLines = Assert.Single(All<DxfLineAngularDimension2>(document.ModelSpace));
            Near(new GeoPoint3(336.742346, 321.213203, 0), ofArc.ArcPoint, 1E-6);
            Near(new GeoPoint3(563.902902, 20.406349, 0), ofLines.ArcPoint, 1E-6);

            Run(session, "STRETCH", P(565, 85), P(555, 75), Enter, P(0, 0), P(-40, 0));

            ofLines = Entity<DxfLineAngularDimension2>(document, ofLines.Handle);
            Near(new GeoPoint3(560.637063, 28.69053, 0), ofLines.ArcPoint, 1E-6);
            Assert.Equal(1.325818, Written42(document, ofLines.Handle), 6);
        }

        [Fact]
        public void AnAngleOfThreePointsWhoseLegMoves_KeepsItsArcAThirdOfTheWayRound_AsAutoCad()
        {
            // AutoCAD 2021: DIMANGULAR _end (0, 700) of a line to (100, 700), _end (100, 700), _end (0, 800) of an upright, the
            // arc through (60, 760); the upright's top stretched by (50, 0): 14 (50, 800), 10 (79.139726, 730.608884), 42
            // 1.107149, 11 at the middle of the arc.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 700), P(100, 700), Enter, "LINE", P(0, 700), P(0, 800), Enter);
            Run(session, "DIMANGULAR", Enter);
            SnapFrom(session, CadSnapModes.Endpoint, P(0.3, 700.2));
            SnapFrom(session, CadSnapModes.Endpoint, P(99.7, 700.2));
            SnapFrom(session, CadSnapModes.Endpoint, P(0.2, 799.7));
            Run(session, P(60, 760));
            var angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));
            Near(new GeoPoint3(73.484692, 742.426407, 0), angle.ArcPoint, 1E-6);

            Run(session, "STRETCH", P(5, 805), P(-5, 795), Enter, P(0, 0), P(50, 0));

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(50, 800, 0), angle.XLine2Point);
            Near(new GeoPoint3(79.139726, 730.608884, 0), angle.ArcPoint, 1E-6);
            Assert.Equal(1.107149, Written42(document, angle.Handle), 6);
            Near(new GeoPoint3(72.180115, 744.609764, 0), angle.TextPosition, 1E-3);
        }

        [Fact]
        public void ARadiusWhoseCircleIsScaled_KeepsItsWordsWhereTheyStand_AsAutoCad()
        {
            // AutoCAD 2021: DIMRADIUS of the circle of 40 about (500, 300) picked at (540, 300), its words put at (560, 320); the
            // circle scaled by 1.5 about its center: 42 60, group 11 where it was.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(500, 300), "40");
            string circle = Last(document);
            Run(session, "DIMRADIUS", P(540, 300), P(560, 320));
            var radius = Assert.Single(All<DxfRadialDimension>(document.ModelSpace));
            GeoPoint3 words = radius.TextPosition;
            document.Selection.Set(new[] { circle });

            Run(session, "SCALE", P(500, 300), "1.5");

            radius = Entity<DxfRadialDimension>(document, radius.Handle);
            Assert.Equal(60, Written42(document, radius.Handle), 9);
            Near(words, radius.TextPosition, 1E-9);
        }
    }
}
