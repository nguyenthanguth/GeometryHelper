using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// REVERSE against AutoCAD 2021's console: "Select line, polyline, spline or helix to reverse direction:", then
    /// "Select objects:", and "Object direction has been reversed." once. AutoCAD turned the closed square (0, 0), (10, 0),
    /// (10, 10) into (10, 10), (10, 0), (0, 0), with its Closed flag kept, as it does for PEDIT Reverse.
    /// </summary>
    public class ReverseCommandTests
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

        private static DxfPolyline Square()
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, P(10, 0), 0, 0, 0);
            polyline.AddVertexAt(2, P(10, 10), 0, 0, 0);
            polyline.Closed = true;
            return polyline;
        }

        [Fact]
        public void ALine_IsTurnedFromItsEndToItsStart_AndTheRunSaysSoOnce()
        {
            CadDocument document = Blank();
            string line = Add(
                document,
                new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(10, 5, 0) }
            );
            CadCommandSession session = Session(document);

            Run(session, "REVERSE");
            Assert.Contains("Select line, polyline, spline or helix to reverse direction:", session.History);
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { line }, Enter);

            DxfLine turned = Entity<DxfLine>(document, line);
            Near(new GeoPoint3(10, 5, 0), turned.StartPoint);
            Near(new GeoPoint3(0, 0, 0), turned.EndPoint);
            Assert.Equal(1, session.History.Count(h => h == "Object direction has been reversed."));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AClosedPolyline_HasItsVerticesInTheOtherOrderThroughout_AsAutoCadTurnedTheSquare()
        {
            CadDocument document = Blank();
            string handle = Add(document, Square());
            CadCommandSession session = Session(document);

            Run(session, "REVERSE", new[] { handle }, Enter);

            DxfPolyline square = Entity<DxfPolyline>(document, handle);
            Assert.True(square.Closed);
            Near(P(10, 10), square.GetPoint2dAt(0));
            Near(P(10, 0), square.GetPoint2dAt(1));
            Near(P(0, 0), square.GetPoint2dAt(2));
        }

        [Fact]
        public void AnOpenPolylineWithAnArc_HasItsBulgeTurnedOver_AndItsWidthsSwapped()
        {
            CadDocument document = Blank();
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(0, 0), 1.0, 2.0, 4.0);
            polyline.AddVertexAt(1, P(10, 0), 0, 4.0, 4.0);
            polyline.AddVertexAt(2, P(10, 10), 0, 0, 0);
            string handle = Add(document, polyline);
            CadCommandSession session = Session(document);

            Run(session, "REVERSE", new[] { handle }, Enter);

            DxfPolyline turned = Entity<DxfPolyline>(document, handle);
            Near(P(10, 10), turned.GetPoint2dAt(0));
            Near(P(10, 0), turned.GetPoint2dAt(1));
            Near(P(0, 0), turned.GetPoint2dAt(2));
            Assert.Equal(-1.0, turned.GetBulgeAt(1), 9);
            Assert.Equal(4.0, turned.GetStartWidthAt(1), 9);
            Assert.Equal(2.0, turned.GetEndWidthAt(1), 9);
            Assert.Equal(0.0, turned.GetBulgeAt(0), 9);
        }

        [Fact]
        public void ASplineAndA3dPolyline_AreReversed_AndACircleIsPassedOverWithoutAWord()
        {
            CadDocument document = Blank();
            string spline = Add(
                document,
                new DxfSpline(
                    2,
                    false,
                    false,
                    false,
                    new[] { new GeoPoint3(0, 20, 0), new GeoPoint3(5, 25, 0), new GeoPoint3(10, 20, 0) },
                    new[] { 0.0, 0.0, 0.0, 1.0, 1.0, 1.0 },
                    new double[0],
                    1E-10,
                    1E-10
                )
            );
            string three = Add(
                document,
                new DxfPolyline3d(
                    DxfPoly3dType.SimplePoly,
                    new[] { new GeoPoint3(0, 30, 0), new GeoPoint3(10, 30, 5), new GeoPoint3(10, 40, 5) },
                    true
                )
            );
            string circle = Add(document, new DxfCircle { Center = new GeoPoint3(30, 30, 0), Radius = 3 });
            CadCommandSession session = Session(document);

            Run(session, "REVERSE", new[] { spline, three, circle }, Enter);

            Near(new GeoPoint3(10, 20, 0), Entity<DxfSpline>(document, spline).StartPoint, 1E-6);

            // The closed frame (0, 30, 0), (10, 30, 5), (10, 40, 5) in the other order, the first vertex last, as AutoCAD turned it.
            DxfPolyline3d turned = Entity<DxfPolyline3d>(document, three);
            var vertices = turned.ToList();
            Near(new GeoPoint3(10, 40, 5), vertices[0].Position);
            Near(new GeoPoint3(10, 30, 5), vertices[1].Position);
            Near(new GeoPoint3(0, 30, 0), vertices[2].Position);
            Assert.Equal(1, session.History.Count(h => h == "Object direction has been reversed."));
            Assert.Equal(1.0, Entity<DxfCircle>(document, circle).Radius - 2.0);
        }

        [Fact]
        public void NothingOfTheFourKinds_IsZeroFound_AndNoReversal()
        {
            CadDocument document = Blank();
            string circle = Add(document, new DxfCircle { Center = new GeoPoint3(30, 30, 0), Radius = 3 });
            CadCommandSession session = Session(document);

            Run(session, "REVERSE", new[] { circle }, Enter);

            Assert.DoesNotContain("Object direction has been reversed.", session.History);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void PeditReverseOfAClosedPolyline_PutsTheFirstVertexLast_AsAutoCadsPeditDid()
        {
            CadDocument document = Blank();
            string handle = Add(document, Square());
            CadCommandSession session = Session(document);

            Run(session, "PEDIT", new[] { handle }, "R", Enter);

            DxfPolyline square = Entity<DxfPolyline>(document, handle);
            Near(P(10, 10), square.GetPoint2dAt(0));
            Near(P(10, 0), square.GetPoint2dAt(1));
            Near(P(0, 0), square.GetPoint2dAt(2));
        }

        [Fact]
        public void TheRunIsOneUndoStep()
        {
            CadDocument document = Blank();
            string handle = Add(document, Square());
            CadCommandSession session = Session(document);
            Run(session, "REVERSE", new[] { handle }, Enter);

            Assert.Equal("REVERSE", document.NextUndo.Name);
            Assert.True(document.Undo());
            Near(P(0, 0), Entity<DxfPolyline>(document, handle).GetPoint2dAt(0));
        }
    }
}
