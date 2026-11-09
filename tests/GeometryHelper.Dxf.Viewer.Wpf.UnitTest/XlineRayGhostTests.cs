using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>The ghost a transforming command drags draws an XLINE and a RAY as the canvas does: the part the view shows.</summary>
    public class XlineRayGhostTests
    {
        private static void Segment(double[] actual, double x0, double y0, double x1, double y1)
        {
            Assert.Equal(4, actual.Length);
            Assert.Equal(x0, actual[0], 9);
            Assert.Equal(y0, actual[1], 9);
            Assert.Equal(x1, actual[2], 9);
            Assert.Equal(y1, actual[3], 9);
        }

        [Fact]
        public void TheGhostOfAnXlineAndARay_MovedAndTurned_IsTheirPartInTheView()
        {
            // An xline level through the origin and a ray up from (50, 0), both selected, in a view from (-100, -100) to (100, 100).
            var db = new DxfDatabase("AC1027");
            var xline = new DxfXline { BasePoint = new GeoPoint3(0, 0, 0), UnitDir = new GeoVector3(1, 0, 0) };
            var ray = new DxfRay { BasePoint = new GeoPoint3(50, 0, 0), UnitDir = new GeoVector3(0, 1, 0) };
            db.ModelSpace.AppendEntity(xline);
            db.ModelSpace.AppendEntity(ray);
            CadDocument document = CadDocument.From(db, "rays.dxf");
            document.Selection.Set(new[] { xline.Handle, ray.Handle });
            var session = new CadCommandSession(document) { PickAperture = 1.0 };
            var ghost = new CadGhost();
            session.PromptChanged += (s, e) => ghost.Follow(session);
            var view = new DisplayBox(-100, -100, 100, 100);

            session.Feed("MOVE");
            session.Feed(new GeoPoint2(0, 0));
            Affine2 move = CadGhost.Transform(session, new GeoPoint2(10, 20)).Value;
            ghost.Paths(document, document.ModelSpace, 1.0);
            IReadOnlyList<double[]> moved = ghost.Rays(move, view);

            // Moved by (10, 20): the xline along y = 20 across the view, the ray up from (60, 20) to the top of it.
            Assert.Equal(2, moved.Count);
            Segment(moved[0], -100, 20, 100, 20);
            Segment(moved[1], 60, 20, 60, 100);

            session.Escape();
            session.Feed("ROTATE");
            session.Feed(new GeoPoint2(0, 0));
            Affine2 turn = CadGhost.Transform(session, new GeoPoint2(0, 30)).Value;
            ghost.Paths(document, document.ModelSpace, 1.0);
            IReadOnlyList<double[]> turned = ghost.Rays(turn, view);

            // A quarter turn about the origin: the xline upright along x = 0, the ray from (0, 50) to the left edge.
            Segment(turned[0], 0, -100, 0, 100);
            Segment(turned[1], 0, 50, -100, 50);

            // Moved 500 to the right, the ray leaves the view and draws nothing; the xline still crosses it.
            Segment(Assert.Single(ghost.Rays(Affine2.Translation(500, 0), view)), -100, 0, 100, 0);
        }
    }
}
