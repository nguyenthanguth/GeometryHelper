using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// XLINE Offset and XLINE Ang Reference against AutoCAD 2021's console: the prompts are those it wrote ("Specify offset
    /// distance or [Through] &lt;Through&gt;:", "Select a line object:", "Specify side to offset:"), and the xline it listed for
    /// a line from (0, 0) to (10, 5) offset 5 to its left is based at (-2.2361, 4.4721) and runs along (0.8944, 0.4472).
    /// </summary>
    public class XLineOffsetTests
    {
        private static string Add(CadDocument document, DxfEntity entity)
        {
            string handle = null;
            document.ApplyEdit("draw", null, s =>
            {
                s.Append(document.ModelSpace, entity);
                handle = entity.Handle;
            });
            return handle;
        }

        private static void Way(double x, double y, double z, GeoVector3 actual)
        {
            Assert.Equal(x, actual.X, 9);
            Assert.Equal(y, actual.Y, 9);
            Assert.Equal(z, actual.Z, 9);
        }

        private static DxfLine Slope() =>
            new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(10, 5, 0) };

        private static DxfPolyline Elbow()
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(0, 10), 0, 0, 0);
            polyline.AddVertexAt(1, P(20, 10), 0, 0, 0);
            polyline.AddVertexAt(2, P(20, 20), 0, 0, 0);
            return polyline;
        }

        [Fact]
        public void OffsetByAdistance_AsksAutoCadsPrompts_AndMakesTheXlineAutoCadListed()
        {
            CadDocument document = Blank();
            Add(document, Slope());
            CadCommandSession session = Session(document);

            Run(session, "XLINE");
            Assert.Equal("Specify a point or [Hor/Ver/Ang/Bisect/Offset]:", session.PromptText);
            Run(session, "O");
            Assert.Equal("Specify offset distance or [Through] <Through>:", session.PromptText);
            Run(session, "5");
            Assert.Equal("Select a line object:", session.PromptText);
            Run(session, P(5, 2.5));
            Assert.Equal("Specify side to offset:", session.PromptText);
            Run(session, P(5, 10));
            Assert.Equal("Select a line object:", session.PromptText);

            DxfXline xline = Assert.Single(All<DxfXline>(document.ModelSpace));
            Near(new GeoPoint3(-2.2360679774997898, 4.4721359549995796, 0), xline.BasePoint, 1E-9);
            Way(0.89442719099991586, 0.44721359549995793, 0, xline.UnitDir);

            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void TheSideDecidesWhichWayTheXlineStands_AndAPointOnTheLineMakesNone()
        {
            CadDocument document = Blank();
            Add(document, Slope());
            CadCommandSession session = Session(document);

            Run(session, "XLINE", "O", "5", P(5, 2.5), P(5, -10));
            Near(
                new GeoPoint3(2.2360679774997898, -4.4721359549995796, 0),
                All<DxfXline>(document.ModelSpace)[0].BasePoint,
                1E-9
            );

            Run(session, P(5, 2.5), P(5, 2.5));
            Assert.Single(All<DxfXline>(document.ModelSpace));
            Assert.Equal("Select a line object:", session.PromptText);
        }

        [Fact]
        public void ASegmentOfAPolyline_IsOffsetAlongItself_AndAnOtherObjectIsNoUsableObject()
        {
            CadDocument document = Blank();
            Add(document, Elbow());
            Add(document, new DxfCircle { Center = new GeoPoint3(50, 50, 0), Radius = 3 });
            CadCommandSession session = Session(document);

            Run(session, "XLINE", "O", "5", P(20, 15), P(18, 15));

            DxfXline xline = Assert.Single(All<DxfXline>(document.ModelSpace));
            Near(new GeoPoint3(15, 10, 0), xline.BasePoint, 1E-9);
            Way(0, 1, 0, xline.UnitDir);

            Run(session, P(50, 53));
            Assert.Contains("No usable object selected", session.History);
            Assert.Equal("Select a line object:", session.PromptText);
        }

        [Fact]
        public void Through_MakesTheXlineAlongTheLineObjectThroughThePointPicked_AndEnterEndsEachAsk()
        {
            CadDocument document = Blank();
            Add(document, Slope());
            CadCommandSession session = Session(document);

            Run(session, "XLINE", "O");
            Run(session, "T");
            Assert.Equal("Select a line object:", session.PromptText);
            Run(session, P(5, 2.5));
            Assert.Equal("Specify through point:", session.PromptText);
            Run(session, P(3, 9));

            DxfXline xline = Assert.Single(All<DxfXline>(document.ModelSpace));
            Near(new GeoPoint3(3, 9, 0), xline.BasePoint, 1E-9);
            Way(0.89442719099991586, 0.44721359549995793, 0, xline.UnitDir);

            Run(session, P(5, 2.5), Enter);
            Assert.False(session.IsRunning);
            Assert.Single(All<DxfXline>(document.ModelSpace));
        }

        [Fact]
        public void TheDistanceIsKept_AndTheNextRunOffersIt_ThroughOffersItselfAfterThrough()
        {
            CadDocument document = Blank();
            Add(document, Slope());
            CadCommandSession session = Session(document);

            Run(session, "XLINE", "O", "5", Enter);
            Assert.False(session.IsRunning);

            Run(session, "XLINE", "O");
            Assert.Equal("Specify offset distance or [Through] <5.0000>:", session.PromptText);
            Run(session, "T", Esc);

            Run(session, "XLINE", "O");
            Assert.Equal("Specify offset distance or [Through] <Through>:", session.PromptText);
            Run(session, Esc);
        }

        [Fact]
        public void ADistanceBelowOrAtNought_IsRefused_AsAutoCadRefusesIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "XLINE", "O", "0");
            Assert.Contains("Value must be positive and nonzero.", session.History);
            Assert.Equal("Specify offset distance or [Through] <Through>:", session.PromptText);
        }

        [Fact]
        public void AngReference_CountsTheAngleFromTheLineObject_AsAutoCadDid()
        {
            CadDocument document = Blank();
            Add(document, Elbow());
            CadCommandSession session = Session(document);

            Run(session, "XLINE", "A");
            Assert.Equal("Enter angle of xline (0) or [Reference]:", session.PromptText);
            Run(session, "R");
            Assert.Equal("Select a line object:", session.PromptText);
            Run(session, P(20, 15));
            Assert.Equal("Enter angle of xline <0>:", session.PromptText);
            Run(session, "45");
            Assert.Equal("Specify through point:", session.PromptText);
            Run(session, P(0, 0));

            // The segment runs up (90 degrees): 45 more is 135.
            DxfXline xline = Assert.Single(All<DxfXline>(document.ModelSpace));
            Near(new GeoPoint3(0, 0, 0), xline.BasePoint, 1E-9);
            Way(-Math.Sqrt(0.5), Math.Sqrt(0.5), 0, xline.UnitDir);
        }

        [Fact]
        public void APickOnNothing_IsNoUsableObjectToo_AsAutoCadsConsoleSaidIt()
        {
            CadDocument document = Blank();
            Add(document, Slope());
            CadCommandSession session = Session(document);

            Run(session, "XLINE", "O", "5", P(50, 50));

            Assert.Contains("No usable object selected", session.History);
            Assert.DoesNotContain("Nothing selected.", session.History);
            Assert.Equal("Select a line object:", session.PromptText);
        }

        [Fact]
        public void AnArcSegmentOfAPolyline_IsNoUsableObject()
        {
            CadDocument document = Blank();
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(0, 0), 1.0, 0, 0);
            polyline.AddVertexAt(1, P(10, 0), 0, 0, 0);
            Add(document, polyline);
            CadCommandSession session = Session(document);

            Run(session, "XLINE", "O", "5", P(5, -5));

            Assert.Contains("No usable object selected", session.History);
            Assert.Empty(All<DxfXline>(document.ModelSpace));
        }
    }
}
