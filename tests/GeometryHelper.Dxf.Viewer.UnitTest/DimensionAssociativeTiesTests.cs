using System;
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
    /// DIMBASELINE, QDIM, DIMORDINATE and DIMANGULAR of two lines, and the edits TRIM, EXTEND and LENGTHEN. The numbers are
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
    }
}
