using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// POLARMODE's bits 1 and 4 as AutoCAD 2021 reads them: the polar angles counted from the last segment drawn, and the
    /// additional angles of POLARADDANG, held as they are but not their multiples.
    /// </summary>
    public class PolarModeTests
    {
        private static PickIndex Index()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Line(1000, 1000, 1100, 1000, handle: "A1")),
                "polar.dxf"
            );
            return document.GetPickIndex(document.ModelSpace);
        }

        private static ObjectSnap Polar(double degrees) =>
            new ObjectSnap(Tight)
            {
                IsOn = false,
                Polar = true,
                PolarAngle = degrees * Math.PI / 180.0,
            };

        private static GeoVector2 Way(double degrees) =>
            new GeoVector2(Math.Cos(degrees * Math.PI / 180.0), Math.Sin(degrees * Math.PI / 180.0));

        private static void Snapped(CadSnapResult result, CadSnapKind kind, double x, double y)
        {
            Assert.Equal(kind, result.Kind);
            Near(P(x, y), result.Point, 1E-9);
        }

        [Fact]
        public void PolarFromTheLastSegment_HoldsTheCursorOnAnglesCountedFromTheLineDrawnLast_AsPolarmode1()
        {
            PickIndex index = Index();
            ObjectSnap snap = Polar(90);
            snap.LastSegment = Way(45);

            // Counted from the X axis the quarter turns run level and upright, and the cursor at 46 degrees is on neither.
            Snapped(snap.Snap(index, P(10, 10.5), 1.0, P(0, 0)), CadSnapKind.None, 10, 10.5);

            // Counted from a line drawn at 45 degrees they run at 45, 135, 225 and 315.
            snap.PolarFromLastSegment = true;
            Snapped(snap.Snap(index, P(10, 10.5), 1.0, P(0, 0)), CadSnapKind.Polar, 10.25, 10.25);
            Snapped(snap.Snap(index, P(-10, 10.5), 1.0, P(0, 0)), CadSnapKind.Polar, -10.25, 10.25);
            Snapped(snap.Snap(index, P(10, 0.5), 1.0, P(0, 0)), CadSnapKind.None, 10, 0.5);

            // With no segment drawn yet they are counted from the X axis.
            snap.LastSegment = null;
            Snapped(snap.Snap(index, P(10, 0.5), 1.0, P(0, 0)), CadSnapKind.Polar, 10, 0);
        }

        [Fact]
        public void AnAdditionalAngle_IsHeldButNotItsMultiples_AndTurnsWithTheLastSegment_AsPolaraddang()
        {
            PickIndex index = Index();
            ObjectSnap snap = Polar(90);
            snap.AdditionalPolarAngles = new[] { 33.0 * Math.PI / 180.0 };
            GeoVector2 at33 = Way(33);
            GeoVector2 at66 = Way(66);

            Snapped(
                snap.Snap(index, P(at33.X * 50 - 0.3, at33.Y * 50 + 0.3), 1.0, P(0, 0)),
                CadSnapKind.Polar,
                at33.X * (50 * 1.0 + 0.3 * (at33.Y - at33.X)),
                at33.Y * (50 * 1.0 + 0.3 * (at33.Y - at33.X))
            );
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(at66.X * 50, at66.Y * 50 + 0.3), 1.0, P(0, 0)).Kind);

            // Not behind the base point either: 213 degrees is no polar angle.
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(-at33.X * 50, -at33.Y * 50), 1.0, P(0, 0)).Kind);

            // Counted from a line drawn upright, 33 is held at 123 degrees.
            snap.PolarFromLastSegment = true;
            snap.LastSegment = Way(90);
            GeoVector2 at123 = Way(123);
            Snapped(
                snap.Snap(index, P(at123.X * 50, at123.Y * 50), 1.0, P(0, 0)),
                CadSnapKind.Polar,
                at123.X * 50,
                at123.Y * 50
            );
        }

        [Fact]
        public void TrackingLinesWithEveryPolarAngle_RunOnTheAdditionalAnglesToo()
        {
            PickIndex index = Index();
            var snap = new ObjectSnap(Tight)
            {
                Modes = CadSnapModes.Endpoint,
                Tracking = true,
                Polar = true,
                PolarAngle = Math.PI / 2.0,
                TracksPolarAngles = true,
            };
            Assert.True(snap.Acquire(index, P(1100.3, 1000.2), 1.0));
            GeoVector2 at33 = Way(33);
            GeoPoint2 cursor = P(1100 + at33.X * 40, 1000 + at33.Y * 40 + 0.4);

            Assert.Equal(CadSnapKind.None, snap.Snap(index, cursor, 1.0).Kind);
            snap.AdditionalPolarAngles = new[] { 33.0 * Math.PI / 180.0 };
            CadSnapResult tracked = snap.Snap(index, cursor, 1.0);
            Assert.Equal(CadSnapKind.Tracking, tracked.Kind);
            Assert.Equal(0.0, (tracked.Point.Y - 1000) * at33.X - (tracked.Point.X - 1100) * at33.Y, 9);
        }

        [Fact]
        public void TheAdditionalAngles_ReadAsPolaraddangTakesThem()
        {
            Assert.True(CadUnits.TryAngles("10;20;33.5", 0, out double[] three));
            Assert.Equal(new[] { 10.0, 20.0, 33.5 }, three.Select(Degrees));
            Assert.True(CadUnits.TryAngles("-10;400;370", 0, out double[] turned));
            Assert.Equal(new[] { 350.0, 40.0, 10.0 }, turned.Select(Degrees));
            Assert.True(CadUnits.TryAngles("45d;1e1;20;10;20", 0, out double[] marked));
            Assert.Equal(new[] { 45.0, 10.0, 20.0, 10.0, 20.0 }, marked.Select(Degrees));
            Assert.True(CadUnits.TryAngles("50", 2, out double[] grads));
            Assert.Equal(new[] { 45.0 }, grads.Select(Degrees));
            Assert.True(CadUnits.TryAngles(string.Empty, 0, out double[] none));
            Assert.Empty(none);

            foreach (
                string refused in new[] { "1;2;3;4;5;6;7;8;9;10;11", "abc", "10;;20;", "10,20", "15 ; 25", "  ", "x;6" }
            )
            {
                Assert.False(CadUnits.TryAngles(refused, 0, out _), refused);
            }
        }

        [Fact]
        public void TheLastSegment_IsTheWayTheLastLineDrawnRan()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Assert.Null(session.LastSegment);

            Run(session, "LINE", "0,0", "30,40", Enter);

            GeoVector2 way = session.LastSegment.Value;
            Assert.Equal(0.6, way.X, 12);
            Assert.Equal(0.8, way.Y, 12);
        }

        private static double Degrees(double radians) => Math.Round(radians * 180.0 / Math.PI, 9);
    }
}
