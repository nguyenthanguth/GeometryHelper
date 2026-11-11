using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The ties of associative dimensions made by the commands other than DIMLINEAR, and by snaps other than the ends of a
    /// line, as AutoCAD 2021 writes them and follows them: an intersection, the quadrants of a circle, DIM, DIMCONTINUE,
    /// DIMBASELINE, QDIM, DIMORDINATE and DIMANGULAR of two lines, the edits TRIM, EXTEND and LENGTHEN, perpendiculars and
    /// tangents at a first origin, the segments of polylines, and angles of three points. The numbers are
    /// those AutoCAD 2021's console gave for the same drawings.
    /// </summary>
    public class DimensionAssociativeTiesTests
    {
        private static readonly ObjectSnap Snaps = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint | CadSnapModes.Intersection | CadSnapModes.Quadrant };

        private static void Snap(CadCommandSession session, GeoPoint2 cursor)
        {
            CadSnapResult result = Snaps.Snap(session.Document.GetPickIndex(session.Document.ModelSpace), cursor, 2.0);
            Assert.NotEqual(CadSnapKind.None, result.Kind);
            Assert.True(session.Feed(result, false));
        }

        private static string LineAt(CadDocument document, double x1, double y1)
            => All<DxfLine>(document.ModelSpace).Single(l => l.StartPoint.DistanceTo(new GeoPoint3(x1, y1, 0)) < 1E-9).Handle;

        [Fact]
        public void ADimensionSnappedWhereTwoLinesCross_IsTiedToBoth_AndFollowsTheCrossingWhenOneMoves()
        {
            // AutoCAD 2021: the crossing of (0, 0)-(100, 100) and (0, 100)-(100, 0) tied as Intersection, the later line first
            // (331) and the earlier second (332); the later moved 20 to the right, the point went to (60, 60).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 100), Enter, "LINE", P(0, 100), P(100, 0), Enter);
            string first = LineAt(document, 0, 0);
            string second = LineAt(document, 0, 100);
            Run(session, "DIMLINEAR");
            Snap(session, P(50.3, 50.2));
            Snap(session, P(99.6, 0.3));
            Run(session, P(80, -30));
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));

            DxfOsnapPointRef crossing = dimension.DimAssoc.PointReferences.First();
            Assert.Equal(DxfOsnapType.Intersection, crossing.OsnapType);
            Assert.Equal(second, crossing.ObjectHandle);
            Assert.Equal(first, crossing.IntersectionObjectHandle);
            AssertReopens(document);

            document.Selection.Set(new[] { second });
            Run(session, "MOVE", P(0, 0), P(20, 0));

            dimension = Entity<DxfRotatedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(60, 60, 0), dimension.XLine1Point);
            Near(new GeoPoint3(120, 0, 0), dimension.XLine2Point);
            Assert.Equal(60, dimension.Measurement, 9);
        }

        [Fact]
        public void ADimensionOfTwoQuadrants_IsDrawnOutWithTheCircleScaled()
        {
            // AutoCAD 2021: quadrants at 180 and 0 degrees of a circle of 30, scaled twice about its center: (-60, 0) to (60, 0).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(0, 0), "30");
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "DIMLINEAR");
            Snap(session, P(-29.7, 0.3));
            Snap(session, P(29.7, -0.3));
            Run(session, P(0, 50));
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.Equal(new[] { DxfOsnapType.Quadrant, DxfOsnapType.Quadrant }, dimension.DimAssoc.PointReferences.Select(r => r.OsnapType));
            document.Selection.Set(new[] { circle });

            Run(session, "SCALE", P(0, 0), "2");

            dimension = Entity<DxfRotatedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(-60, 0, 0), dimension.XLine1Point, 1E-9);
            Near(new GeoPoint3(60, 0, 0), dimension.XLine2Point, 1E-9);
        }

        /// <summary>Three lines end to end along x, 0 to 100, 100 to 200, 200 to 300, and a DIMLINEAR snapped to 0 and 100.</summary>
        private static CadDocument Row(out CadCommandSession session, out DxfRotatedDimension first)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "LINE", P(100, 0), P(200, 0), Enter, "LINE", P(200, 0), P(300, 0), Enter, "DIMLINEAR");
            Snap(session, P(0.3, 0.3));
            Snap(session, P(99.8, 0.3));
            Run(session, P(50, -20));
            first = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            return document;
        }

        [Fact]
        public void DimContinue_TiesItsFirstPointToWhatTheLastOnesSecondWasTiedTo_AndItsSecondToItsSnap()
        {
            CadDocument document = Row(out CadCommandSession session, out DxfRotatedDimension first);
            DxfOsnapPointRef from = first.DimAssoc.PointReferences[1];

            Run(session, "DIMCONTINUE");
            Snap(session, P(199.7, 0.3));
            Run(session, Enter, Enter);

            DxfRotatedDimension next = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != first.Handle);
            DxfOsnapPointRef[] tied = next.DimAssoc.PointReferences.ToArray();
            Assert.Equal(2, tied.Length);
            Assert.Equal(0, tied[0].PointIndex);
            Assert.Equal(from.ObjectHandle, tied[0].ObjectHandle);
            Assert.Equal(from.OsnapType, tied[0].OsnapType);
            Assert.Equal(1, tied[1].PointIndex);

            // The end it was snapped to dragged on: the second dimension's end follows.
            Assert.True(document.GripDrag(tied[1].ObjectHandle, tied[1].OsnapType == DxfOsnapType.StartPoint ? 0 : 1, P(250, 0)).Succeeded);
            Near(new GeoPoint3(250, 0, 0), Entity<DxfRotatedDimension>(document, next.Handle).XLine2Point);
        }

        [Fact]
        public void DimBaseline_TiesItsFirstPointToWhatTheBasesFirstWasTiedTo()
        {
            CadDocument document = Row(out CadCommandSession session, out DxfRotatedDimension first);
            DxfOsnapPointRef from = first.DimAssoc.PointReferences[0];

            Run(session, "DIMBASELINE");
            Snap(session, P(299.7, 0.3));
            Run(session, Enter, Enter);

            DxfRotatedDimension next = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != first.Handle);
            Assert.Equal(from.ObjectHandle, next.DimAssoc.PointReferences[0].ObjectHandle);
            Assert.Equal(DxfOsnapType.Endpoint, next.DimAssoc.PointReferences[1].OsnapType);

            Assert.True(document.GripDrag(LineAt(document, 0, 0), 0, P(-10, 0)).Succeeded);
            Near(new GeoPoint3(-10, 0, 0), Entity<DxfRotatedDimension>(document, next.Handle).XLine1Point);
            Assert.Equal(310, Entity<DxfRotatedDimension>(document, next.Handle).Measurement, 9);
        }

        [Fact]
        public void Qdim_TiesEachDimensionToTheVerticesOfThePolyline_AsAutoCadWritesThem()
        {
            // AutoCAD 2021's QDIM of a rectangle 100 by 50: the dimension of its bottom tied to the starts of segments 1 and 2.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PLINE", P(0, 0), P(100, 0), P(100, 50), P(0, 50), "C");
            DxfPolyline polyline = Assert.Single(All<DxfPolyline>(document.ModelSpace));

            Run(session, "QDIM", new[] { polyline.Handle }, Enter, P(50, -30));

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Assert.All(dimension.DimAssoc.PointReferences, r => Assert.Equal(DxfOsnapType.StartPoint, r.OsnapType));
            Assert.Equal(new[] { 1, 2 }, dimension.DimAssoc.PointReferences.Select(r => r.GsMarker));
            document.Selection.Set(new[] { polyline.Handle });

            Run(session, "MOVE", P(0, 0), P(0, 10));

            Near(new GeoPoint3(0, 10, 0), Entity<DxfRotatedDimension>(document, dimension.Handle).XLine1Point);
            Near(new GeoPoint3(100, 10, 0), Entity<DxfRotatedDimension>(document, dimension.Handle).XLine2Point);
        }

        [Fact]
        public void Dim_TiesThePointsItWasSnappedTo_AndALinePicked_ToTheLinesEnds()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "LINE", P(0, 100), P(80, 100), Enter, "DIM");
            Snap(session, P(0.3, 0.3));
            Snap(session, P(99.7, 0.3));
            Run(session, P(50, 30), P(40, 100), P(40, 130), Enter);

            var dimensions = All<DxfRotatedDimension>(document.ModelSpace);
            Assert.Equal(2, dimensions.Count);
            Assert.Equal(new[] { DxfOsnapType.StartPoint, DxfOsnapType.Endpoint }, dimensions[0].DimAssoc.PointReferences.Select(r => r.OsnapType));
            Assert.Equal(new[] { DxfOsnapType.StartPoint, DxfOsnapType.Endpoint }, dimensions[1].DimAssoc.PointReferences.Select(r => r.OsnapType));
            Assert.All(dimensions[1].DimAssoc.PointReferences, r => Assert.Equal(LineAt(document, 0, 100), r.ObjectHandle));
        }

        [Fact]
        public void AnOrdinateOfTheEndOfALine_FollowsIt_ItsLeaderEndMovedAlongTheAxisMeasuredAlone()
        {
            // AutoCAD 2021: the ordinate of X of (100, 0), its leader to (100, -40), the line moved by (10, 5): the point at
            // (110, 5), the leader's end at (110, -40).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "DIMORDINATE");
            Snap(session, P(99.7, 0.3));
            Run(session, P(100, -40));
            DxfOrdinateDimension ordinate = Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace));
            Assert.Equal(1, Assert.Single(ordinate.DimAssoc.PointReferences).PointIndex);
            document.Selection.Set(new[] { LineAt(document, 0, 0) });

            Run(session, "MOVE", P(0, 0), P(10, 5));

            ordinate = Entity<DxfOrdinateDimension>(document, ordinate.Handle);
            Near(new GeoPoint3(110, 5, 0), ordinate.DefiningPoint);
            Near(new GeoPoint3(110, -40, 0), ordinate.LeaderEndPoint);
            Near(new GeoPoint3(0, 0, 0), ordinate.Origin);
            Assert.Equal(110, ordinate.Measurement, 9);
        }

        [Fact]
        public void AnAngleOfTwoLines_IsTiedToTheirEnds_AndKeepsItsArcsShareOfTheAngle_AsAutoCadKeepsIt()
        {
            // AutoCAD 2021: the angle of (0, 0)-(100, 0) and (0, 0)-(80, 60), its arc through (71.1327, 15.496); the first line
            // moved up 10, the arc point went to (84.466, 25.496); the second turned 20 degrees about (0, 0), to (68.8527, 23.65).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "LINE", P(0, 0), P(80, 60), Enter);
            string first = All<DxfLine>(document.ModelSpace)[0].Handle;
            string second = All<DxfLine>(document.ModelSpace)[1].Handle;

            Run(session, "DIMANGULAR", new[] { first }, new[] { second }, P(71.1327, 15.4962));

            DxfLineAngularDimension2 angle = Assert.Single(All<DxfLineAngularDimension2>(document.ModelSpace));
            Assert.Equal(4, angle.DimAssoc.PointReferences.Count);
            AssertReopens(document);
            string before = document.Database.ToText();
            document.Selection.Set(new[] { first });

            Run(session, "MOVE", P(0, 0), P(0, 10));

            Near(new GeoPoint3(84.466, 25.496, 0), Entity<DxfLineAngularDimension2>(document, angle.Handle).ArcPoint, 1E-3);
            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
            document.Selection.Set(new[] { second });

            Run(session, "ROTATE", P(0, 0), "20");

            Near(new GeoPoint3(68.8527, 23.65, 0), Entity<DxfLineAngularDimension2>(document, angle.Handle).ArcPoint, 1E-2);
        }

        /// <summary>A line from (0, 0) to (100, 0) with a DIMLINEAR snapped to its ends, an upright at x = 70 and one at x = 150.</summary>
        private static CadDocument Walls(out CadCommandSession session, out string dimension)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "LINE", P(70, -20), P(70, 20), Enter, "LINE", P(150, -20), P(150, 20), Enter, "DIMLINEAR");
            Snap(session, P(0.3, 0.3));
            Snap(session, P(99.7, 0.3));
            Run(session, P(50, 30));
            dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            return document;
        }

        [Fact]
        public void Trim_ThenExtend_TheDimensionOfTheLineFollowsItsEnd_AsAutoCadFollowsIt()
        {
            // AutoCAD 2021: trimmed at x = 70 the dimension measured 70; extended to x = 150, 150.
            CadDocument document = Walls(out CadCommandSession session, out string dimension);

            Run(session, "TRIM", P(90, 0), Enter);

            Assert.Equal(70, Entity<DxfRotatedDimension>(document, dimension).Measurement, 9);
            Assert.NotNull(Entity<DxfRotatedDimension>(document, dimension).DimAssoc);

            Run(session, "EXTEND", P(65, 0), Enter);

            Assert.Equal(150, Entity<DxfRotatedDimension>(document, dimension).Measurement, 9);
            Near(new GeoPoint3(150, 0, 0), Entity<DxfRotatedDimension>(document, dimension).XLine2Point);
        }

        [Fact]
        public void Lengthen_TheDimensionOfTheLineFollowsItsEnd_AsAutoCadFollowsIt()
        {
            // AutoCAD 2021: LENGTHEN DElta 20 of a line of 100 tied at its ends, the dimension measured 120.
            CadDocument document = Walls(out CadCommandSession session, out string dimension);

            Run(session, "LENGTHEN", "DE", "20", P(95, 0), Enter);

            Assert.Equal(120, Entity<DxfRotatedDimension>(document, dimension).Measurement, 9);
            Near(new GeoPoint3(120, 0, 0), Entity<DxfRotatedDimension>(document, dimension).XLine2Point);
        }

        /// <summary>Feeds the point one snap mode gives from the prompt's base point, or the point it snaps from, as a click with it would.</summary>
        private static void SnapFrom(CadCommandSession session, CadSnapModes mode, GeoPoint2 cursor)
        {
            var snaps = new ObjectSnap(Tight) { Modes = mode };
            CadSnapResult result = snaps.Snap(session.Document.GetPickIndex(session.Document.ModelSpace), cursor, 2.0, session.Prompt.BasePoint, session.Prompt.SnapFrom);
            Assert.NotEqual(CadSnapKind.None, result.Kind);
            Assert.True(session.Feed(result, false));
        }

        [Fact]
        public void APerpendicularSnap_IsTiedWithThePointItWasDrawnFrom_AndFollowsTheLineMoved_AsAutoCad()
        {
            // AutoCAD 2021: DIMALIGNED _end at (0, 0) of (0, 0)-(60, 0), _per to (100, -20)-(100, 60): the foot (100, 0) tied as
            // Perpendicular of parameter 0.25 with the start of the first line as its last point; the upright line moved 20 to
            // the right, the foot went to (120, 0).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(60, 0), Enter, "LINE", P(100, -20), P(100, 60), Enter);
            string level = LineAt(document, 0, 0);
            string upright = LineAt(document, 100, -20);
            Run(session, "DIMALIGNED");
            SnapFrom(session, CadSnapModes.Endpoint, P(0.3, 0.2));
            SnapFrom(session, CadSnapModes.Perpendicular, P(100.3, 0.4));
            Run(session, P(50, -30));
            var dimension = Assert.Single(All<DxfAlignedDimension>(document.ModelSpace));
            Near(new GeoPoint3(100, 0, 0), dimension.XLine2Point);

            DxfOsnapPointRef foot = dimension.DimAssoc.PointReferences[1];
            Assert.Equal(DxfOsnapType.Perpendicular, foot.OsnapType);
            Assert.Equal(upright, foot.ObjectHandle);
            Assert.Equal(0.25, foot.NearParameter, 9);
            Assert.Equal(DxfOsnapType.StartPoint, foot.LastPointReference.OsnapType);
            Assert.Equal(level, foot.LastPointReference.ObjectHandle);
            AssertReopens(document);

            document.Selection.Set(new[] { upright });
            Run(session, "MOVE", P(0, 0), P(20, 0));

            dimension = Entity<DxfAlignedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(0, 0, 0), dimension.XLine1Point);
            Near(new GeoPoint3(120, 0, 0), dimension.XLine2Point);
            Assert.Equal(120, dimension.Measurement, 9);
            Assert.NotNull(dimension.DimAssoc);
        }

        [Fact]
        public void ATangentSnap_IsTiedToTheCircle_AndFollowsItMoved_ToThePointOfTangencyOnTheSameSide_AsAutoCad()
        {
            // AutoCAD 2021: DIMALIGNED _end at (300, 100), _tan to the circle of 25 about (200, 0): the point (185.726, 20.524)
            // tied as Tangent of parameter 2.17848; the circle moved 10 down, the point went to (184.593, 9.688).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, 0), "25", "LINE", P(300, 100), P(320, 120), Enter);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "DIMALIGNED");
            SnapFrom(session, CadSnapModes.Endpoint, P(300.2, 100.3));
            SnapFrom(session, CadSnapModes.Tangent, P(185.9, 20.8));
            Run(session, P(280, 160));
            var dimension = Assert.Single(All<DxfAlignedDimension>(document.ModelSpace));
            Near(new GeoPoint3(185.726, 20.524, 0), dimension.XLine2Point, 1E-3);

            DxfOsnapPointRef point = dimension.DimAssoc.PointReferences[1];
            Assert.Equal(DxfOsnapType.Tangent, point.OsnapType);
            Assert.Equal(circle, point.ObjectHandle);
            Assert.Equal(2.17848, point.NearParameter, 5);
            Assert.Equal(DxfOsnapType.StartPoint, point.LastPointReference.OsnapType);

            document.Selection.Set(new[] { circle });
            Run(session, "MOVE", P(0, 0), P(0, -10));

            dimension = Entity<DxfAlignedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(300, 100, 0), dimension.XLine1Point);
            Near(new GeoPoint3(184.593, 9.688, 0), dimension.XLine2Point, 1E-3);
        }

        [Fact]
        public void APerpendicularAtTheFirstOrigin_IsDrawnFromTheLastPoint_AndKeepsItsParameterWhenTheLineMoves()
        {
            // AutoCAD 2021: LASTPOINT (37, 250), DIMLINEAR _per to (100, 200)-(100, 280), then (50, 300): the first origin
            // (100, 250) tied as Perpendicular of parameter 0.625, its last point (37, 250) tied to nothing; the line moved by
            // (20, 7), the origin went to (120, 257).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(100, 200), P(100, 280), Enter, "LINE", P(0, 250), P(37, 250), Enter);
            string upright = LineAt(document, 100, 200);
            Run(session, "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Perpendicular, P(100.3, 260));
            Run(session, P(50, 300), P(70, 350));
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Near(new GeoPoint3(100, 250, 0), dimension.XLine1Point);

            DxfOsnapPointRef foot = Assert.Single(dimension.DimAssoc.PointReferences);
            Assert.Equal(0, foot.PointIndex);
            Assert.Equal(DxfOsnapType.Perpendicular, foot.OsnapType);
            Assert.Equal(upright, foot.ObjectHandle);
            Assert.Equal(0.625, foot.NearParameter, 9);
            Assert.Equal(DxfOsnapType.None, foot.LastPointReference.OsnapType);
            Assert.Null(foot.LastPointReference.ObjectHandle);
            Near(new GeoPoint3(37, 250, 0), foot.LastPointReference.Point);
            AssertReopens(document);

            document.Selection.Set(new[] { upright });
            Run(session, "MOVE", P(0, 0), P(20, 7));

            dimension = Entity<DxfRotatedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(120, 257, 0), dimension.XLine1Point);
            Near(new GeoPoint3(50, 300, 0), dimension.XLine2Point);
            Assert.Equal(70, dimension.Measurement, 9);
        }

        [Fact]
        public void ATangentAtTheFirstOrigin_IsDrawnFromTheLastPoint_AndDrawnAgainFromItWhenTheCircleMoves()
        {
            // AutoCAD 2021: LASTPOINT (137, 423), DIMALIGNED _tan to the circle of 25 about (200, 300), then (150, 200): the
            // first origin (219.822227, 315.234149) tied as Tangent of parameter 0.655261 with (137, 423) as its last point; the
            // circle moved by (20, 7), the origin went to (237.466992, 324.885865), the tangent from (137, 423) again.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, 300), "25", "LINE", P(100, 423), P(137, 423), Enter);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "DIMALIGNED");
            SnapFrom(session, CadSnapModes.Tangent, P(220, 315));
            Run(session, P(150, 200), P(150, 250));
            var dimension = Assert.Single(All<DxfAlignedDimension>(document.ModelSpace));
            Near(new GeoPoint3(219.822227, 315.234149, 0), dimension.XLine1Point, 1E-6);

            DxfOsnapPointRef touch = Assert.Single(dimension.DimAssoc.PointReferences);
            Assert.Equal(DxfOsnapType.Tangent, touch.OsnapType);
            Assert.Equal(circle, touch.ObjectHandle);
            Assert.Equal(0.655261, touch.NearParameter, 6);
            Near(new GeoPoint3(137, 423, 0), touch.LastPointReference.Point);

            document.Selection.Set(new[] { circle });
            Run(session, "MOVE", P(0, 0), P(20, 7));

            Near(new GeoPoint3(237.466992, 324.885865, 0), Entity<DxfAlignedDimension>(document, dimension.Handle).XLine1Point, 1E-6);
        }

        [Fact]
        public void APerpendicularPastTheEndOfItsLine_DrawnFromAPointTiedToNothing_GoesToTheFootFromTheOrigin_AsAutoCad()
        {
            // AutoCAD 2021 wrote a DIMLINEAR whose first origin was drawn from (37, 123) square to (100, -20)-(100, 60) as
            // Perpendicular of parameter 1.7875 at (100, 123), its point 10 written (0, 0, 0), its second origin (50, 77) and its
            // line at y = -60; the line moved by (20, 7), the origin went to (120, 0), the foot from the origin of the world,
            // and the dimension line to y = -137, as far below the lower origin as it was before.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(100, -20), P(100, 60), Enter, "DIMLINEAR", P(100, 123), P(50, 77), P(70, -60));
            string upright = LineAt(document, 100, -20);
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            var foot = new DxfOsnapPointRef(0, DxfOsnapType.Perpendicular, upright, 0, 1.7875, new GeoPoint3(0, 0, 0), null);
            dimension.SetDimAssoc(new[] { foot.WithLastPoint(new GeoPoint3(37, 123, 0)) });
            document.Selection.Set(new[] { upright });

            Run(session, "MOVE", P(0, 0), P(20, 7));

            dimension = Entity<DxfRotatedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(120, 0, 0), dimension.XLine1Point);
            Near(new GeoPoint3(50, 77, 0), dimension.XLine2Point);
            Assert.Equal(-137, dimension.DimLinePoint.Y, 9);
            Assert.Equal(70, dimension.Measurement, 9);
        }

        /// <summary>
        /// A light polyline from (0, 200) to (100, 200), round a half circle to (100, 300), and back to (0, 300), and a line
        /// drawn after it from (20, 280) to (20, 320), as AutoCAD's console drew them.
        /// </summary>
        private static CadDocument Hook(out CadCommandSession session, out string polyline, out string line)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "PLINE", P(0, 200), P(100, 200), "A", P(100, 300), "L", P(0, 300), Enter, "LINE", P(20, 280), P(20, 320), Enter);
            polyline = Assert.Single(All<DxfPolyline>(document.ModelSpace)).Handle;
            line = LineAt(document, 20, 280);
            return document;
        }

        private static void AssertTie(DxfOsnapPointRef reference, int index, DxfOsnapType type, string handle, int marker, double parameter)
        {
            Assert.Equal(index, reference.PointIndex);
            Assert.Equal(type, reference.OsnapType);
            Assert.Equal(handle, reference.ObjectHandle);
            Assert.Equal(marker, reference.GsMarker);
            Assert.Equal(parameter, reference.NearParameter, 5);
        }

        [Fact]
        public void TheSegmentsOfAPolyline_AreTiedByTheirMarkers_AsAutoCadWritesThem_AndFollowedWhenItMoves()
        {
            // AutoCAD 2021, for the drawing of Hook: the ends tied as StartPoint of marker 1 and Endpoint of marker 2, parameter
            // 7.85398 (the angle on from the half circle's start); a middle of marker 1 at 0.5 and the center of marker 2 at
            // 4.71239; a nearest point of marker 3 at 0.7; the crossing with the line as Intersection of the line (331) and the
            // polyline (332) with 92 = 3; a tangent from (200, 320) of marker 2 at 5.74515; a perpendicular from (30, 150) of
            // marker 1 at 0.3. The polyline moved by (10, 5): the points where AutoCAD 2021 put them.
            CadDocument document = Hook(out CadCommandSession session, out string polyline, out string line);
            Run(session, "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Endpoint, P(0.3, 200.2));
            SnapFrom(session, CadSnapModes.Endpoint, P(99, 300.2));
            Run(session, P(50, 350), "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Midpoint, P(50.3, 200.2));
            SnapFrom(session, CadSnapModes.Center, P(100.2, 250.1));
            Run(session, P(50, 150), "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Nearest, P(30, 300.3));
            SnapFrom(session, CadSnapModes.Endpoint, P(0.3, 300.2));
            Run(session, P(15, 330), "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Intersection, P(20.2, 300.3));
            SnapFrom(session, CadSnapModes.Endpoint, P(20.2, 319.8));
            Run(session, P(-20, 310), "DIMALIGNED", P(200, 320));
            SnapFrom(session, CadSnapModes.Tangent, P(143, 224));
            Run(session, P(250, 250), "DIMALIGNED", P(30, 150));
            SnapFrom(session, CadSnapModes.Perpendicular, P(70, 200.2));
            Run(session, P(0, 170));
            List<DxfDimension> dimensions = All<DxfDimension>(document.ModelSpace);
            Assert.Equal(6, dimensions.Count);

            IReadOnlyList<DxfOsnapPointRef> ends = dimensions[0].DimAssoc.PointReferences;
            AssertTie(ends[0], 0, DxfOsnapType.StartPoint, polyline, 1, 0.0);
            AssertTie(ends[1], 1, DxfOsnapType.Endpoint, polyline, 2, 7.85398);
            IReadOnlyList<DxfOsnapPointRef> middle = dimensions[1].DimAssoc.PointReferences;
            AssertTie(middle[0], 0, DxfOsnapType.Midpoint, polyline, 1, 0.5);
            AssertTie(middle[1], 1, DxfOsnapType.Center, polyline, 2, 4.71239);
            AssertTie(dimensions[2].DimAssoc.PointReferences[0], 0, DxfOsnapType.Nearest, polyline, 3, 0.7);
            AssertTie(dimensions[2].DimAssoc.PointReferences[1], 1, DxfOsnapType.Endpoint, polyline, 3, 1.0);
            DxfOsnapPointRef crossing = dimensions[3].DimAssoc.PointReferences[0];
            AssertTie(crossing, 0, DxfOsnapType.Intersection, line, 0, 0.5);
            Assert.Equal(polyline, crossing.IntersectionObjectHandle);
            Assert.Contains("332\n" + polyline + "\n 74\n2\n 92\n3\n", document.Database.ToText());
            DxfOsnapPointRef touch = Assert.Single(dimensions[4].DimAssoc.PointReferences);
            AssertTie(touch, 1, DxfOsnapType.Tangent, polyline, 2, 5.74515);
            Near(new GeoPoint3(200, 320, 0), touch.LastPointReference.Point);
            DxfOsnapPointRef foot = Assert.Single(dimensions[5].DimAssoc.PointReferences);
            AssertTie(foot, 1, DxfOsnapType.Perpendicular, polyline, 1, 0.3);
            AssertReopens(document);
            Assert.Equal(0, document.Database.Audit(false).Count);

            document.Selection.Set(new[] { polyline });
            Run(session, "MOVE", P(0, 0), P(10, 5));

            dimensions = All<DxfDimension>(document.ModelSpace);
            Near(new GeoPoint3(10, 205, 0), ((DxfRotatedDimension)dimensions[0]).XLine1Point);
            Near(new GeoPoint3(110, 305, 0), ((DxfRotatedDimension)dimensions[0]).XLine2Point);
            Near(new GeoPoint3(60, 205, 0), ((DxfRotatedDimension)dimensions[1]).XLine1Point);
            Near(new GeoPoint3(110, 255, 0), ((DxfRotatedDimension)dimensions[1]).XLine2Point);
            Near(new GeoPoint3(40, 305, 0), ((DxfRotatedDimension)dimensions[2]).XLine1Point);
            Near(new GeoPoint3(10, 305, 0), ((DxfRotatedDimension)dimensions[2]).XLine2Point);
            Near(new GeoPoint3(20, 305, 0), ((DxfRotatedDimension)dimensions[3]).XLine1Point);
            Near(new GeoPoint3(154.392998, 231.994311, 0), ((DxfAlignedDimension)dimensions[4]).XLine2Point, 1E-6);
            Near(new GeoPoint3(40, 205, 0), ((DxfAlignedDimension)dimensions[5]).XLine2Point);
            Assert.All(dimensions, d => Assert.NotNull(d.DimAssoc));
        }

        [Fact]
        public void AStretchedVertexOfAPolyline_KeepsItsArcsHeight_AndTheTiesOnTheArcFollow_AsAutoCad()
        {
            // AutoCAD 2021: the vertex (100, 300) of Hook stretched up 20, the half circle kept its height of 50 off its
            // chord, now of 120: its center went to (89, 260), the nearest point at 7.39033 to (116.280029, 314.560059).
            CadDocument document = Hook(out CadCommandSession session, out string polyline, out _);
            Run(session, "DIMLINEAR");
            SnapFrom(session, CadSnapModes.Midpoint, P(50.3, 200.2));
            SnapFrom(session, CadSnapModes.Center, P(100.2, 250.1));
            Run(session, P(50, 150), "DIMANGULAR", Enter);
            SnapFrom(session, CadSnapModes.Endpoint, P(0, 300));
            SnapFrom(session, CadSnapModes.Midpoint, P(50.3, 200.2));
            SnapFrom(session, CadSnapModes.Nearest, P(122.36068, 294.72136));
            Run(session, P(60, 260));
            string center = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            string angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace)).Handle;

            Run(session, "STRETCH", P(105, 310), P(95, 295), Enter, P(0, 0), P(0, 20));

            Assert.Equal(100.0 / 120.0, Entity<DxfPolyline>(document, polyline).GetBulgeAt(1), 9);
            Near(new GeoPoint3(89, 260, 0), Entity<DxfRotatedDimension>(document, center).XLine2Point, 1E-9);
            Near(new GeoPoint3(116.280029, 314.560059, 0), Entity<DxfPoint3AngularDimension>(document, angle).XLine2Point, 1E-6);
        }

        [Fact]
        public void AnAngleOfThreePoints_TiesItsEndsDrawnFromTheVertex_WithTheVertexAsTheirLastPoint_AsAutoCad()
        {
            // AutoCAD 2021: DIMANGULAR, the vertex _end at (300, 0) of (300, 0)-(380, 0), the first end _per to (100, -20)-
            // (100, 60) at (100, 0), parameter 0.25, the second _tan to the circle of 25 about (400, 200) at (376.529514,
            // 208.610243), parameter 2.78998, each with the vertex's StartPoint as its last point. The upright and the circle
            // moved by (20, 7): the ends at (120, 0) and (397.179819, 217.209767); then the line of the vertex moved by (10, 10):
            // the vertex at (310, 10), the ends at (120, 10) and (396.956203, 216.694506).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(100, -20), P(100, 60), Enter, "LINE", P(300, 0), P(380, 0), Enter, "CIRCLE", P(400, 200), "25");
            string upright = LineAt(document, 100, -20);
            string level = LineAt(document, 300, 0);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "DIMANGULAR", Enter);
            SnapFrom(session, CadSnapModes.Endpoint, P(300.3, 0.2));
            SnapFrom(session, CadSnapModes.Perpendicular, P(100.2, 40));
            SnapFrom(session, CadSnapModes.Tangent, P(410, 222));
            Run(session, P(330, 30));
            var angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));
            Near(new GeoPoint3(376.529514, 208.610243, 0), angle.XLine2Point, 1E-6);

            IReadOnlyList<DxfOsnapPointRef> ties = angle.DimAssoc.PointReferences;
            AssertTie(ties[0], 0, DxfOsnapType.Perpendicular, upright, 0, 0.25);
            AssertTie(ties[1], 1, DxfOsnapType.Tangent, circle, 0, 2.78998);
            AssertTie(ties[2], 2, DxfOsnapType.StartPoint, level, 0, 0.0);
            Assert.All(new[] { ties[0], ties[1] }, t => Assert.Equal(level, t.LastPointReference.ObjectHandle));
            Assert.All(new[] { ties[0], ties[1] }, t => Assert.Equal(DxfOsnapType.StartPoint, t.LastPointReference.OsnapType));
            AssertReopens(document);
            Assert.Equal(0, document.Database.Audit(false).Count);

            document.Selection.Set(new[] { upright, circle });
            Run(session, "MOVE", P(0, 0), P(20, 7));

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(120, 0, 0), angle.XLine1Point);
            Near(new GeoPoint3(397.179819, 217.209767, 0), angle.XLine2Point, 1E-6);

            document.Selection.Set(new[] { level });
            Run(session, "MOVE", P(0, 0), P(10, 10));

            angle = Entity<DxfPoint3AngularDimension>(document, angle.Handle);
            Near(new GeoPoint3(310, 10, 0), angle.CenterPoint);
            Near(new GeoPoint3(120, 10, 0), angle.XLine1Point);
            Near(new GeoPoint3(396.956203, 216.694506, 0), angle.XLine2Point, 1E-6);
        }

        [Fact]
        public void AVertexSnappedPerpendicular_IsDrawnFromTheLastPoint_AndKeepsItsParameter_AsAutoCad()
        {
            // AutoCAD 2021: LASTPOINT (437, 23), DIMANGULAR, the vertex _per to (500, -20)-(500, 60): (500, 23), parameter
            // 0.5375, its last point (437, 23) tied to nothing; the line moved by (20, 7), the vertex went to (520, 30).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(500, -20), P(500, 60), Enter, "LINE", P(400, 23), P(437, 23), Enter);
            string upright = LineAt(document, 500, -20);
            Run(session, "DIMANGULAR", Enter);
            SnapFrom(session, CadSnapModes.Perpendicular, P(500.2, 10));
            Run(session, P(600, 23), P(600, 123), P(560, 60));
            var angle = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));

            DxfOsnapPointRef vertex = Assert.Single(angle.DimAssoc.PointReferences);
            AssertTie(vertex, 2, DxfOsnapType.Perpendicular, upright, 0, 0.5375);
            Near(new GeoPoint3(437, 23, 0), vertex.LastPointReference.Point);

            document.Selection.Set(new[] { upright });
            Run(session, "MOVE", P(0, 0), P(20, 7));

            Near(new GeoPoint3(520, 30, 0), Entity<DxfPoint3AngularDimension>(document, angle.Handle).CenterPoint);
        }

        [Fact]
        public void AnOrdinateSnappedPerpendicular_IsDrawnFromTheLastPoint_AndTiedWithNoLastPoint_AsAutoCad()
        {
            // AutoCAD 2021: LASTPOINT (37, 23), DIMORDINATE _per to (100, -20)-(100, 60), Xdatum to (100, -60): the feature
            // (100, 23) tied as Perpendicular of parameter 0.5375 and no last point; the line moved by (20, 7), the feature went
            // to (120, 30) and the leader's end to (120, -60).
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(100, -20), P(100, 60), Enter, "LINE", P(0, 23), P(37, 23), Enter);
            string upright = LineAt(document, 100, -20);
            Run(session, "DIMORDINATE");
            SnapFrom(session, CadSnapModes.Perpendicular, P(100.2, 10));
            Run(session, "X", P(100, -60));
            var ordinate = Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace));
            Near(new GeoPoint3(100, 23, 0), ordinate.DefiningPoint);

            DxfOsnapPointRef feature = Assert.Single(ordinate.DimAssoc.PointReferences);
            AssertTie(feature, 1, DxfOsnapType.Perpendicular, upright, 0, 0.5375);
            Assert.Null(feature.LastPointReference);

            document.Selection.Set(new[] { upright });
            Run(session, "MOVE", P(0, 0), P(20, 7));

            ordinate = Entity<DxfOrdinateDimension>(document, ordinate.Handle);
            Near(new GeoPoint3(120, 30, 0), ordinate.DefiningPoint);
            Near(new GeoPoint3(120, -60, 0), ordinate.LeaderEndPoint);
        }
    }
}
