using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// XCLIP's Clipdepth on a dashed circle, arc or closed polyline, as AutoCAD 2021 draws it (PNGOUT in plan of a DASHED circle
    /// and a DASHED closed polyline of a plane tilted 45 degrees about X, and of an arc, each with the planes 10 and -10 and
    /// again with none): the dashes of what is left are those of the whole, pixel for pixel (156 of 156, 512 of 512 and 65 of
    /// 65), and the pattern of a closed curve begins at its start, so that the piece that holds the start is two, the pattern
    /// beginning again where it is split.
    /// </summary>
    public class XClipSeamDashTests
    {
        private static readonly GeoVector3 Facing = new GeoVector3(0, 1, 0);

        private static readonly double Half = Math.Sqrt(0.5);

        /// <summary>A block GHS of the shapes the caller adds, all DASHED (a dash of 12.5 and a gap of 6.25), clipped with the planes front 10 and back -10.</summary>
        private static CadDocument Clipped(Action<DxfBlockTableRecord> fill, out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 12.5);
            dashed.SetDashLengthAt(1, -6.25);
            database.LinetypeTable.Add(dashed);
            var block = new DxfBlockTableRecord { Name = "GHS" };
            database.BlockTable.Add(block);
            fill(block);

            foreach (DxfEntity entity in block)
            {
                entity.Linetype = "DASHED";
            }

            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipseam.dxf");
            CadCommandSession session = Session(document);
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", P(-20, -200), P(300, 300));
            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "Clipdepth",
                "Distance",
                10.0.ToString(CultureInfo.InvariantCulture),
                "Distance",
                (-10.0).ToString(CultureInfo.InvariantCulture)
            );
            return document;
        }

        /// <summary>The phases of the dashes of what is drawn of the reference, smallest first.</summary>
        private static List<double> Phases(CadDocument document, string handle) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .Select(i => i.Style.DashPhase)
                .OrderBy(p => p)
                .ToList();

        [Fact]
        public void ADashedCircleCutAcrossItsStart_IsSplitThere_AndThePatternBeginsAgain()
        {
            // The circle of radius 30 standing on its edge about (50, 100, 0), planes 10 and -10: the pieces are the arcs round
            // x 20 and x 80, and the one round x 20 holds the start of the circle (angle 0, x 20). Seen from above, the stretch
            // x 20 to 80 is walked there and back from the start: the piece from the start has phase 0, the one round x 80
            // starts 58.28 along, and the one that ends at the start 118.28 along.
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfCircle(new GeoPoint3(50, 100, 0), Facing, 30)),
                out string handle
            );

            List<double> phases = Phases(document, handle);

            double lean = 30.0 * Math.Sqrt(8.0 / 9.0);
            Assert.Equal(3, phases.Count);
            Assert.Equal(0.0, phases[0], 6);
            Assert.InRange(phases[1], 30.0 + lean - 0.1, 30.0 + lean + 0.1);
            Assert.InRange(phases[2], 90.0 + lean - 0.1, 90.0 + lean + 0.1);
        }

        [Fact]
        public void ADashedCircleOfATiltedPlane_CutAcrossItsStart_BeginsThePatternAgainAtTheStart()
        {
            // Probe (plan): the circle of radius 30 about (100, 0, 0) in the plane tilted 45 degrees about X is the ellipse of
            // semi-axes 30 and 21.21 in the plan; the planes 10 and -10 leave y -10 to 10, the arcs from -28.13 to 28.13 and
            // 151.87 to 208.13 degrees. The pattern is the whole's, which begins again at angle 0: the arc that ends there is
            // (360 - 28.13) degrees along the ellipse, the arc round the left end 151.87 degrees along it.
            CadDocument document = Clipped(
                block =>
                    block.AppendEntity(new DxfCircle(new GeoPoint3(100, 0, 0), new GeoVector3(0, -Half, Half), 30)),
                out string handle
            );

            List<double> phases = Phases(document, handle);

            double cut = Math.Asin(10.0 / (30.0 * Half));
            Assert.Equal(3, phases.Count);
            Assert.Equal(0.0, phases[0], 6);
            Assert.Equal(EllipseLength(30.0, 30.0 * Half, 0.0, Math.PI - cut), phases[1], 1);
            Assert.Equal(EllipseLength(30.0, 30.0 * Half, 0.0, 2.0 * Math.PI - cut), phases[2], 1);
        }

        /// <summary>The length of the ellipse x = a cos t, y = b sin t from one angle to another, walked finely.</summary>
        private static double EllipseLength(double a, double b, double from, double to)
        {
            const int steps = 200000;
            double sum = 0.0;
            double x = a * Math.Cos(from);
            double y = b * Math.Sin(from);

            for (int i = 1; i <= steps; i++)
            {
                double t = from + (to - from) * i / steps;
                double nx = a * Math.Cos(t);
                double ny = b * Math.Sin(t);
                sum += Math.Sqrt((nx - x) * (nx - x) + (ny - y) * (ny - y));
                x = nx;
                y = ny;
            }

            return sum;
        }

        [Fact]
        public void ADashedClosedPolylineCutAcrossItsFirstVertex_IsSplitThere_AndThePatternBeginsAgain()
        {
            // The rectangle (10, -5), (90, -5), (90, 40), (10, 40) in X and Z, planes 10 and -10: what is left is the bottom
            // side and the parts of the sides under z 10, one piece through the first vertex. Seen from above the sides that
            // stand are points: from the start the piece holds x 10 to 90 (phase 0), and the piece before it, down the left
            // side, begins 80 + 80 along the plan.
            var polyline = new DxfPolyline
            {
                Normal = Facing,
                Elevation = 100.0,
                Closed = true,
            };
            double[] xz = { 10, -5, 90, -5, 90, 40, 10, 40 };

            for (int i = 0; i < xz.Length / 2; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(-xz[2 * i], xz[2 * i + 1]), 0.0, 0, 0);
            }

            CadDocument document = Clipped(block => block.AppendEntity(polyline), out string handle);

            List<double> phases = Phases(document, handle);

            Assert.Equal(2, phases.Count);
            Assert.Equal(0.0, phases[0], 6);
            Assert.Equal(160.0, phases[1], 6);
        }

        [Fact]
        public void ADashedArcCutAtThePlanes_CarriesTheWholesPatternFromItsStart()
        {
            // The arc of the circle of the first test from 17.19 to 229.18 degrees: no start of a closed curve to split at. The
            // piece round x 80 begins where the arc has walked from x 50 - 30 cos(17.19) to 78.28.
            var arc = new DxfArc(new GeoPoint3(50, 100, 0), Facing, 30, 0.3, 4.0);
            CadDocument document = Clipped(block => block.AppendEntity(arc), out string handle);

            List<double> phases = Phases(document, handle);

            Assert.Equal(2, phases.Count);
            Assert.Equal(0.0, phases[0], 6);
            Assert.Equal(50.0 + 30.0 * Math.Sqrt(8.0 / 9.0) - (50.0 - 30.0 * Math.Cos(0.3)), phases[1], 1);
        }

        [Fact]
        public void ACircleCutByTheBoundarysPrism_ThatHoldsItsStart_IsSplitThere()
        {
            // A clip made in a system turned 30 degrees about X, a rectangle -50 to 250 by -40 to 40 of it (XClipPrismTests): the
            // circle of radius 30 about (50, 0, -40) in the plane of Y and Z stands at -20 + 30 cos(a - 30 degrees) across the
            // boundary, under -40 for a from 161.8 to 258.2 degrees; what is left runs through the start of the circle and is two
            // pieces, as one cut across its start is.
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHP" };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(50, 0, -40), GeoVector3.XAxis, 30));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            double cos30 = Math.Cos(Math.PI / 6.0);
            insert.ClipTo(
                new[] { new GeoPoint2(-50, -40), new GeoPoint2(250, 40) },
                CadXClips.WorldToClip(
                    new CadUcs(new GeoPoint3(0, 0, 0), GeoVector3.XAxis, new GeoVector3(0.0, cos30, 0.5))
                )
            );
            CadDocument document = CadDocument.From(database, "xclipseam.dxf");

            int pieces = document
                .GetDisplay(document.ModelSpace)
                .Items.Count(i => i.Handle == insert.Handle && !i.IsClipFrame);

            Assert.Equal(2, pieces);
        }
    }
}
