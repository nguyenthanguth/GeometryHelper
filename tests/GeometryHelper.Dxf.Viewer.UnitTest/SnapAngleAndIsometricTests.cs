using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The snap grid laid from SNAPBASE and turned by SNAPANG, ortho and polar following the turn, and the isometric snap of
    /// SNAPSTYL 1: its points where the 30, 90 and 150 degree lines cross, and ortho on the two axes of the plane SNAPISOPAIR
    /// names, as AutoCAD 2021 has them.
    /// </summary>
    public class SnapAngleAndIsometricTests
    {
        private static readonly double Cos30 = Math.Sqrt(3.0) / 2.0;

        private static PickIndex Empty()
        {
            CadDocument blank = Blank();
            return blank.GetPickIndex(blank.ModelSpace);
        }

        private static GeoPoint2 Turned(GeoPoint2 origin, double angle, double x, double y) =>
            new GeoPoint2(
                origin.X + x * Math.Cos(angle) - y * Math.Sin(angle),
                origin.Y + x * Math.Sin(angle) + y * Math.Cos(angle)
            );

        private static void Snapped(CadSnapResult result, CadSnapKind kind, GeoPoint2 expected)
        {
            Assert.Equal(kind, result.Kind);
            Near(expected, result.Point, 1E-9);
        }

        [Fact]
        public void ASnapOf10TurnedBy30Degrees_PutsTheCursorOnTheTurnedGrid()
        {
            var snap = new ObjectSnap(Tight)
            {
                IsOn = false,
                GridSnap = true,
                SnapAngle = Math.PI / 6.0,
            };

            // The point of the documentation: (9, 4) lies 9.79 along the turned X axis and 1.04 below it.
            Snapped(snap.Snap(Empty(), P(9, 4), 1.0), CadSnapKind.Grid, P(10 * Cos30, 5));
        }

        [Fact]
        public void TheSnapGrid_IsLaidFromSnapBase_AndTurnedAboutIt()
        {
            var origin = P(3, 4);
            var snap = new ObjectSnap(Tight)
            {
                IsOn = false,
                GridSnap = true,
                GridX = 10,
                GridY = 5,
                GridOrigin = origin,
            };

            Snapped(snap.Snap(Empty(), P(14, 10), 1.0), CadSnapKind.Grid, P(13, 9));

            snap.SnapAngle = Math.PI / 4.0;
            GeoPoint2 cursor = Turned(origin, Math.PI / 4.0, 21.0, -7.0);
            Snapped(snap.Snap(Empty(), cursor, 1.0), CadSnapKind.Grid, Turned(origin, Math.PI / 4.0, 20.0, -5.0));
        }

        [Fact]
        public void OrthoAndPolar_FollowTheSnapAngle()
        {
            double turn = Math.PI / 6.0;
            var snap = new ObjectSnap(Tight)
            {
                IsOn = false,
                Ortho = true,
                SnapAngle = turn,
            };
            GeoPoint2 from = P(100, 50);

            Snapped(
                snap.Snap(Empty(), Turned(from, turn, 40, 6), 1.0, from),
                CadSnapKind.Ortho,
                Turned(from, turn, 40, 0)
            );
            Snapped(
                snap.Snap(Empty(), Turned(from, turn, -3, -25), 1.0, from),
                CadSnapKind.Ortho,
                Turned(from, turn, 0, -25)
            );

            // A polar increment of 90 counted from 30 degrees: the cursor at 32 degrees is held at 30, at 2 degrees it is not.
            snap.Ortho = false;
            snap.Polar = true;
            snap.PolarAngle = Math.PI / 2.0;
            double near = 32.0 * Math.PI / 180.0;
            CadSnapResult held = snap.Snap(Empty(), Turned(from, near, 100, 0), 5.0, from);
            Snapped(held, CadSnapKind.Polar, Turned(from, turn, 100 * Math.Cos(near - turn), 0));
            Assert.Equal(
                CadSnapKind.None,
                snap.Snap(Empty(), Turned(from, 2.0 * Math.PI / 180.0, 100, 0), 5.0, from).Kind
            );
        }

        [Fact]
        public void AnIsometricSnap_TakesThePointsWhereThe30_90And150DegreeLinesCross()
        {
            // SNAP Style Isometric 5 keeps 8.6603 by 2.5: the points lie 5 apart along each isometric line.
            var snap = new ObjectSnap(Tight)
            {
                IsOn = false,
                GridSnap = true,
                GridX = 5 * Math.Sqrt(3.0),
                GridY = 2.5,
                Isoplane = CadIsoplane.Top,
            };

            Snapped(snap.Snap(Empty(), P(4.6, 2.3), 1.0), CadSnapKind.Grid, P(5 * Cos30, 2.5));
            Snapped(snap.Snap(Empty(), P(0.2, 4.8), 1.0), CadSnapKind.Grid, P(0, 5));
            Snapped(snap.Snap(Empty(), P(8.4, 0.4), 1.0), CadSnapKind.Grid, P(10 * Cos30, 0));
            Snapped(snap.Snap(Empty(), P(-4.0, -2.0), 1.0), CadSnapKind.Grid, P(-5 * Cos30, -2.5));

            // Laid from SNAPBASE and turned by SNAPANG as the rectangular grid is.
            snap.GridOrigin = P(1, 1);
            snap.SnapAngle = Math.PI / 2.0;
            Snapped(
                snap.Snap(Empty(), P(-1.2, 5.2), 1.0),
                CadSnapKind.Grid,
                Turned(P(1, 1), Math.PI / 2.0, 5 * Cos30, 2.5)
            );
        }

        [Theory]
        [InlineData(CadIsoplane.Left, 140.0, 150.0)]
        [InlineData(CadIsoplane.Left, 80.0, 90.0)]
        [InlineData(CadIsoplane.Left, 10.0, 150.0)]
        [InlineData(CadIsoplane.Top, 20.0, 30.0)]
        [InlineData(CadIsoplane.Top, 100.0, 150.0)]
        [InlineData(CadIsoplane.Right, 100.0, 90.0)]
        [InlineData(CadIsoplane.Right, 200.0, 30.0)]
        public void OrthoWithAnIsometricSnap_HoldsTheCursorOnTheAxesOfThePlane(
            CadIsoplane plane,
            double cursorDegrees,
            double axisDegrees
        )
        {
            var snap = new ObjectSnap(Tight)
            {
                IsOn = false,
                Ortho = true,
                Isoplane = plane,
            };
            double way = cursorDegrees * Math.PI / 180.0;
            double axis = axisDegrees * Math.PI / 180.0;
            GeoPoint2 from = P(10, 20);

            CadSnapResult held = snap.Snap(Empty(), Turned(from, way, 50, 0), 1.0, from);

            Snapped(held, CadSnapKind.Ortho, Turned(from, axis, 50 * Math.Cos(way - axis), 0));
        }

        [Fact]
        public void AWholeNumberOfNoughtOrOne_IsRefusedAsAutoCadRefusesOrthomode2()
        {
            CadCommandSession session = Session(Blank());
            session.Tolerance = Tolerance.Default;
            Run(session, "PICKFIRST", "2");

            Assert.Equal("Requires 0 or 1 only.", session.History.Last());
            Assert.Equal("Enter new value for PICKFIRST <1>:", session.PromptText);
        }
    }
}
