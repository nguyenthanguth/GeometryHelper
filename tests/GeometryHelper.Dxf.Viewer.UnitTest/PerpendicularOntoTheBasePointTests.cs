using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A perpendicular from a base point that lies on the object itself is the base point, and AutoCAD 2021 offers it: LINE
    /// from (30, 0) with PER picked on the line from (0, 0) to (100, 0) drew a line of no length to (30, 0), and so it did
    /// from (150, 0) on the line run on, from (225, 0) on a circle and an arc of radius 25 about (200, 0), and from (30, 0)
    /// on a polyline's segment. The snaps of every prompt offer it too; only a table's grip drag leaves it out.
    /// </summary>
    public class PerpendicularOntoTheBasePointTests
    {
        private static PickIndex Index(params string[] entities)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "per.dxf");
            return document.GetPickIndex(document.ModelSpace);
        }

        private static void At(CadSnapResult result, double x, double y)
        {
            Assert.Equal(CadSnapKind.Perpendicular, result.Kind);
            Near(P(x, y), result.Point, 1E-9);
        }

        [Fact]
        public void ABasePointOnALine_OnItsRunOn_OrOnACircle_IsItsOwnPerpendicular()
        {
            PickIndex index = Index(
                TestDrawings.Line(0, 0, 100, 0, handle: "A1"),
                TestDrawings.Circle(200, 0, 25, handle: "A3")
            );
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Perpendicular };

            At(snap.Snap(index, P(60, 0.5), 1.0, P(30, 0)), 30, 0);
            At(snap.Snap(index, P(60, 0.5), 1.0, P(150, 0)), 150, 0);

            // On the circle the foot nearer the cursor: the base point itself, or the point across, as AutoCAD 2021 took them.
            double c = 25.0 / System.Math.Sqrt(2.0);
            At(snap.Snap(index, P(200 + c, c + 0.3), 1.0, P(225, 0)), 225, 0);
            At(snap.Snap(index, P(200 - c, -c - 0.3), 1.0, P(225, 0)), 175, 0);
        }
    }
}
