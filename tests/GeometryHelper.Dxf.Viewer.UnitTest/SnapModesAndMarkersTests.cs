using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The snaps Node and Geometric Center, the override of one point, and AutoCAD's markers as data.</summary>
    public class SnapModesAndMarkersTests
    {
        private static PickIndex Index(params string[] entities)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "snap.dxf");
            return document.GetPickIndex(document.ModelSpace);
        }

        private static string Point(double x, double y, string handle) => TestDrawings.Entity("POINT", "0", 256, handle, "10", x, "20", y, "30", 0.0);

        [Fact]
        public void APointIsANode_NotAnEndpoint_AsInAutoCAD()
        {
            PickIndex index = Index(Point(30, 40, "B1"));

            CadSnapResult node = new ObjectSnap(Tight) { Modes = CadSnapModes.Node }.Snap(index, P(30.4, 39.7), 1.0);
            CadSnapResult endpoint = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint }.Snap(index, P(30.4, 39.7), 1.0);

            Assert.Equal(CadSnapKind.Node, node.Kind);
            Near(P(30, 40), node.Point);
            Assert.Equal("B1", node.Handle);
            Assert.Equal(CadSnapKind.None, endpoint.Kind);
        }

        [Fact]
        public void AnLShapedPolylineGivesTheCentroidOfItsArea_FromItsOutlineOrNearTheCentroid()
        {
            // An L of 30 by 10 and 10 by 20: areas 300 at (15, 5) and 200 at (5, 20), the centroid (11, 11).
            PickIndex index = Index(TestDrawings.Polyline("0", "C1", true, 0, 0, 0, 30, 0, 0, 30, 10, 0, 10, 10, 0, 10, 30, 0, 0, 30, 0));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.GeometricCenter };

            CadSnapResult fromOutline = snap.Snap(index, P(20, 10.3), 1.0);
            CadSnapResult nearIt = snap.Snap(index, P(11.4, 10.8), 1.0);

            Assert.Equal(CadSnapKind.GeometricCenter, fromOutline.Kind);
            Near(P(11, 11), fromOutline.Point);
            Assert.Equal(CadSnapKind.GeometricCenter, nearIt.Kind);
            Near(P(11, 11), nearIt.Point);
        }

        [Fact]
        public void AnOpenPolylineAndASliverGiveNoGeometricCenter()
        {
            PickIndex open = Index(TestDrawings.Polyline("0", "C2", false, 0, 0, 0, 30, 0, 0, 30, 10, 0));
            PickIndex sliver = Index(TestDrawings.Polyline("0", "C3", true, 0, 0, 0, 100, 0, 0, 100, 0.0001, 0, 0, 0.0001, 0));
            var snap = new ObjectSnap(new Tolerance(1E-3, 1E-5)) { Modes = CadSnapModes.GeometricCenter };

            Assert.Equal(CadSnapKind.None, snap.Snap(open, P(20, 0.3), 1.0).Kind);
            Assert.Equal(CadSnapKind.None, snap.Snap(sliver, P(50, 0.3), 1.0).Kind);
        }

        [Fact]
        public void AnOverrideTakesItsSnapsAlone_WithOsnapOffToo_AndNoneTakesNoSnap()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "A1"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint | CadSnapModes.Midpoint, IsOn = false, Override = CadSnapModes.Midpoint };

            CadSnapResult overridden = snap.Snap(index, P(50.3, 0.2), 2.0);
            CadSnapResult endIgnored = snap.Snap(index, P(99.5, 0.4), 2.0);
            snap.IsOn = true;
            snap.Override = CadSnapModes.None;
            CadSnapResult none = snap.Snap(index, P(99.5, 0.4), 2.0);
            snap.Override = null;
            CadSnapResult usual = snap.Snap(index, P(99.5, 0.4), 2.0);

            Assert.Equal(CadSnapKind.Midpoint, overridden.Kind);
            Near(P(50, 0), overridden.Point);
            Assert.Equal(CadSnapKind.None, endIgnored.Kind);
            Assert.Equal(CadSnapKind.None, none.Kind);
            Near(P(99.5, 0.4), none.Point);
            Assert.Equal(CadSnapKind.Endpoint, usual.Kind);
        }

        [Fact]
        public void EachMarkerIsAutoCADsShape_NineAcross()
        {
            const double h = CadSnapMarker.Size / 2.0;

            IReadOnlyList<CadMarkerStroke> end = CadSnapMarker.Strokes(CadSnapKind.Endpoint);
            Assert.Single(end);
            Assert.True(end[0].IsClosed);
            Assert.Equal(new[] { P(-h, -h), P(h, -h), P(h, h), P(-h, h) }, end[0].Points);

            CadMarkerStroke triangle = Assert.Single(CadSnapMarker.Strokes(CadSnapKind.Midpoint));
            Assert.Equal(3, triangle.Points.Count);
            Assert.True(triangle.IsClosed);

            CadMarkerStroke circle = Assert.Single(CadSnapMarker.Strokes(CadSnapKind.Center));
            Assert.All(circle.Points, p => Assert.Equal(h, Math.Sqrt(p.X * p.X + p.Y * p.Y), 9));

            IReadOnlyList<CadMarkerStroke> gcen = CadSnapMarker.Strokes(CadSnapKind.GeometricCenter);
            Assert.Equal(2, gcen.Count);
            Assert.True(gcen[1].IsFilled);
            Assert.All(gcen[1].Points, p => Assert.True(Math.Sqrt(p.X * p.X + p.Y * p.Y) < 2.0));

            IReadOnlyList<CadMarkerStroke> node = CadSnapMarker.Strokes(CadSnapKind.Node);
            Assert.Equal(3, node.Count);
            Assert.Equal(2, node.Count(s => s.Points.Count == 2));

            CadMarkerStroke diamond = Assert.Single(CadSnapMarker.Strokes(CadSnapKind.Quadrant));
            Assert.Equal(new[] { P(0, -h), P(h, 0), P(0, h), P(-h, 0) }, diamond.Points);

            Assert.Equal(new[] { P(-h, -h), P(h, h) }, CadSnapMarker.Strokes(CadSnapKind.Intersection)[0].Points);
            Assert.Contains(CadSnapMarker.Strokes(CadSnapKind.Extension), s => s.IsDotted);
            Assert.Equal(2, CadSnapMarker.Strokes(CadSnapKind.Insertion).Count(s => s.IsClosed && s.Points.Count == 4));
            Assert.Equal(new[] { P(-h, -h), P(-h, h), P(h, h) }, CadSnapMarker.Strokes(CadSnapKind.Perpendicular)[0].Points);

            IReadOnlyList<CadMarkerStroke> tangent = CadSnapMarker.Strokes(CadSnapKind.Tangent);
            Assert.Equal(new[] { P(-h, -h), P(h, -h) }, tangent[1].Points);

            // The hourglass: its top and bottom edges level, its other two crossing at the snap point.
            CadMarkerStroke hourglass = Assert.Single(CadSnapMarker.Strokes(CadSnapKind.Nearest));
            Assert.Equal(new[] { P(-h, -h), P(h, -h), P(-h, h), P(h, h) }, hourglass.Points);

            Assert.Equal(3, CadSnapMarker.Strokes(CadSnapKind.ApparentIntersection).Count);

            IReadOnlyList<CadMarkerStroke> parallel = CadSnapMarker.Strokes(CadSnapKind.Parallel);
            GeoVector2 first = parallel[0].Points[1] - parallel[0].Points[0];
            GeoVector2 second = parallel[1].Points[1] - parallel[1].Points[0];
            Assert.Equal(0.0, first.X * second.Y - first.Y * second.X, 9);
        }

        [Fact]
        public void EveryMarkerStaysWithinTwiceItsSize_AndTheCursorKindsHaveNone()
        {
            foreach (CadSnapKind kind in Enum.GetValues(typeof(CadSnapKind)).Cast<CadSnapKind>())
            {
                IReadOnlyList<CadMarkerStroke> strokes = CadSnapMarker.Strokes(kind);

                if (kind == CadSnapKind.None || kind == CadSnapKind.Grid || kind == CadSnapKind.Ortho || kind == CadSnapKind.Polar)
                {
                    Assert.Empty(strokes);
                    continue;
                }

                Assert.NotEmpty(strokes);
                Assert.All(strokes.SelectMany(s => s.Points), p => Assert.True(Math.Abs(p.X) <= CadSnapMarker.Size && Math.Abs(p.Y) <= CadSnapMarker.Size, kind + " " + p));
            }
        }

        [Fact]
        public void MarkersAreYellowOnDarkAndDarkOnLight_AndNamedAsAutoCADsTips()
        {
            Assert.Equal(0xFFFF00, CadSnapMarker.ColorOn(0x000000));
            Assert.Equal(0xFFFF00, CadSnapMarker.ColorOn(0x212830));
            Assert.Equal(0x1A1A1A, CadSnapMarker.ColorOn(0xFFFFFF));
            Assert.Equal("Endpoint", CadSnapMarker.NameOf(CadSnapKind.Endpoint));
            Assert.Equal("Geometric Center", CadSnapMarker.NameOf(CadSnapKind.GeometricCenter));
            Assert.Equal("Apparent Intersection", CadSnapMarker.NameOf(CadSnapKind.ApparentIntersection));
            Assert.Equal(string.Empty, CadSnapMarker.NameOf(CadSnapKind.None));
        }
    }
}
