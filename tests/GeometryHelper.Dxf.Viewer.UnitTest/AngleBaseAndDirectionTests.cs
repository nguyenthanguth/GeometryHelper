using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Typed and shown angles under ANGBASE 90 and ANGDIR 1 (nought to the north, clockwise), each number as AutoCAD 2021
    /// gave it in accoreconsole: a direction typed is counted from ANGBASE by ANGDIR, a turn typed only by ANGDIR, a
    /// &lt;&lt; or &lt;&lt;&lt; before the angle counts it from east and counter-clockwise, and what is kept in the drawing
    /// stays counter-clockwise from the X axis.
    /// </summary>
    public class AngleBaseAndDirectionTests
    {
        private static readonly double Root3 = Math.Sqrt(3.0);

        /// <summary>A blank drawing counting angles from the north, clockwise, written to whole degrees.</summary>
        private static CadDocument North()
        {
            CadDocument document = Blank();
            document.Database.Angbase = Math.PI / 2.0;
            document.Database.Angdir = true;
            document.Database.Auprec = 0;
            return document;
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A line from the origin 100 to the east, and its handle.</summary>
        private static string East(CadDocument document)
        {
            Start(document, "LINE", P(0, 0), P(100, 0), Enter);
            return All<DxfLine>(document.ModelSpace).Last().Handle;
        }

        [Fact]
        public void APolarPointTypedThirtyDegrees_LiesThirtyClockwiseOfNorth()
        {
            CadDocument document = North();

            Start(document, "LINE", P(0, 0), "@100<30", Enter);
            Near(new GeoPoint3(50, 50 * Root3, 0), Assert.Single(All<DxfLine>(document.ModelSpace)).EndPoint, 1E-9);

            Start(document, "LINE", "100<30", P(0, 0), Enter);
            Near(new GeoPoint3(50, 50 * Root3, 0), All<DxfLine>(document.ModelSpace).Last().StartPoint, 1E-9);
        }

        [Fact]
        public void TwoOrThreeAngleBracketsBeforeTheAngle_CountItFromEastCounterClockwise()
        {
            CadDocument document = North();

            Start(document, "LINE", P(0, 0), "@100<<30", Enter);
            Near(new GeoPoint3(50 * Root3, 50, 0), Assert.Single(All<DxfLine>(document.ModelSpace)).EndPoint, 1E-9);

            Start(document, "LINE", P(0, 0), "@100<<<30", Enter);
            Near(new GeoPoint3(50 * Root3, 50, 0), All<DxfLine>(document.ModelSpace).Last().EndPoint, 1E-9);
        }

        [Fact]
        public void ThreeAngleBrackets_ReadTheAngleInTheDrawingsUnits_TwoInDegrees()
        {
            CadDocument document = North();
            document.Database.Aunits = 2;

            // 100 grads are a quarter turn; with two brackets the 90 is degrees whatever AUNITS says.
            Start(document, "LINE", P(0, 0), "@100<<<100", Enter);
            Near(new GeoPoint3(0, 100, 0), Assert.Single(All<DxfLine>(document.ModelSpace)).EndPoint, 1E-9);

            Start(document, "LINE", P(0, 0), "@100<<90", Enter);
            Near(new GeoPoint3(0, 100, 0), All<DxfLine>(document.ModelSpace).Last().EndPoint, 1E-9);
        }

        [Fact]
        public void RotateByThirtyTyped_TurnsThirtyClockwise_TheBaseAngleLeftOut()
        {
            CadDocument document = North();
            string line = East(document);

            CadCommandSession session = Start(document, "ROTATE", new[] { line }, Enter, P(0, 0));
            Assert.Contains("Current positive angle in UCS:  ANGDIR=clockwise  ANGBASE=90", session.History);
            Assert.Equal("Specify rotation angle or [Copy/Reference] <90>:", session.PromptText);
            Run(session, "30");

            Near(new GeoPoint3(50 * Root3, -50, 0), Entity<DxfLine>(document, line).EndPoint, 1E-9);
        }

        [Fact]
        public void RotateByAPointToTheNorth_TurnsNothing_AndByOneToTheEastAQuarterClockwise()
        {
            CadDocument document = North();
            string line = East(document);

            Start(document, "ROTATE", new[] { line }, Enter, P(0, 0), P(0, 50));
            Near(new GeoPoint3(100, 0, 0), Entity<DxfLine>(document, line).EndPoint, 1E-9);

            Start(document, "ROTATE", new[] { line }, Enter, P(0, 0), P(50, 0));
            Near(new GeoPoint3(0, -100, 0), Entity<DxfLine>(document, line).EndPoint, 1E-9);
        }

        [Fact]
        public void RotateByReferenceFromNoughtToThirty_TurnsThirtyClockwise()
        {
            CadDocument document = North();
            string line = East(document);

            CadCommandSession session = Start(document, "ROTATE", new[] { line }, Enter, P(0, 0), "R");
            Assert.EndsWith("<90>:", session.PromptText);
            Run(session, "0", "30");

            Near(new GeoPoint3(50 * Root3, -50, 0), Entity<DxfLine>(document, line).EndPoint, 1E-9);
        }

        [Fact]
        public void AnArcOfAnIncludedAngleOfThirty_RunsClockwiseFromItsStart()
        {
            CadDocument document = North();

            Start(document, "ARC", "C", P(0, 0), P(100, 0), "A", "30");

            // Kept counter-clockwise, as DXF keeps every arc: from 330 degrees round to the start point at nought.
            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));
            Assert.Equal(330.0, arc.StartAngle * 180.0 / Math.PI, 9);
            Assert.Equal(0.0, Math.Sin(arc.EndAngle), 9);
            Assert.Equal(1.0, Math.Cos(arc.EndAngle), 9);
        }

        [Fact]
        public void AnXlineAtAnAngleOfThirty_RunsSixtyDegreesFromTheXAxis_AndOffersNinetyForEast()
        {
            CadDocument document = North();

            CadCommandSession session = Start(document, "XLINE", "A");
            Assert.Equal("Enter angle of xline (90):", session.PromptText);
            Run(session, "30", P(0, 0), Enter);

            DxfXline line = Assert.Single(All<DxfXline>(document.ModelSpace));
            Assert.Equal(0.5, line.UnitDir.X, 9);
            Assert.Equal(Root3 / 2.0, line.UnitDir.Y, 9);
        }

        [Fact]
        public void DistWritesTheAngleCountedFromNorthClockwise()
        {
            CadDocument document = North();

            Assert.Contains("Distance = 100.0000,  Angle in XY Plane = 90,  Angle from XY Plane = 90", Start(document, "DIST", P(0, 0), P(100, 0)).History);
            Assert.Contains("Distance = 100.0000,  Angle in XY Plane = 0,  Angle from XY Plane = 90", Start(document, "DIST", P(0, 0), P(0, 100)).History);
            Assert.Contains("Distance = 141.4214,  Angle in XY Plane = 45,  Angle from XY Plane = 90", Start(document, "DIST", P(0, 0), P(100, 100)).History);
        }

        [Fact]
        public void ListWritesAnArcFromNoughtToNinety_AsFromNinetyToNought()
        {
            CadDocument document = North();
            Start(document, "ARC", "C", P(0, 0), P(100, 0), P(0, 100));
            string arc = Assert.Single(All<DxfArc>(document.ModelSpace)).Handle;

            CadCommandSession session = Start(document, "LIST", new[] { arc }, Enter);

            Assert.Contains(session.History, l => l.Trim() == "start angle     90");
            Assert.Contains(session.History, l => l.Trim() == "end angle      0");
        }

        [Fact]
        public void PropertiesShowAndTakeADirectionCountedFromNorthClockwise_AndKeepItFromTheXAxis()
        {
            CadDocument document = North();
            Start(document, "ARC", "C", P(0, 0), P(100, 0), P(0, 100));
            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));

            string Row(string label) => CadProperties.Describe(document, null, new[] { arc.Handle }).Groups.SelectMany(g => g.Rows).Single(r => r.Label == label).Value;
            Assert.Equal("90", Row("Start angle"));
            Assert.Equal("0", Row("End angle"));

            Assert.True(CadPropertyEditor.Set(document, new[] { arc.Handle }, "Start angle", "60").Succeeded);
            Assert.Equal(30.0, Entity<DxfArc>(document, arc.Handle).StartAngle * 180.0 / Math.PI, 9);
            Assert.Equal("60", Row("Start angle"));
        }

        [Fact]
        public void WithAngbaseNoughtAndAnglesCounterClockwise_ATypedAngleIsFromEast_AsBefore()
        {
            CadDocument document = Blank();
            string line = East(document);

            Start(document, "LINE", P(0, 0), "@100<30", Enter);
            Near(new GeoPoint3(50 * Root3, 50, 0), All<DxfLine>(document.ModelSpace).Last().EndPoint, 1E-9);

            CadCommandSession session = Start(document, "ROTATE", new[] { line }, Enter, P(0, 0));
            Assert.Contains("Current positive angle in UCS:  ANGDIR=counterclockwise  ANGBASE=0", session.History);
            Run(session, "30");
            Near(new GeoPoint3(50 * Root3, 50, 0), Entity<DxfLine>(document, line).EndPoint, 1E-9);
        }

        [Fact]
        public void ToDirectionAndFromDirection_UndoEachOther()
        {
            double north = Math.PI / 2.0;

            Assert.Equal(Math.PI / 3.0, CadUnits.ToDirection(Math.PI / 6.0, north, true), 12);
            Assert.Equal(Math.PI / 6.0, CadUnits.FromDirection(Math.PI / 3.0, north, true), 12);
            Assert.Equal(north, CadUnits.FromDirection(0.0, north, true), 12);
            Assert.Equal(0.0, CadUnits.FromDirection(north, north, true), 12);
            Assert.Equal(3.0 * north, CadUnits.FromDirection(0.0, north, false), 12);
        }
    }
}
