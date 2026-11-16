using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// XCLIP's Clipdepth on a dashed line or polyline, as AutoCAD 2021 draws it (PNGOUT of accoreconsole, front view, of a
    /// DASHED line and of polylines with PLINEGEN on and off across the planes): the pieces that are left carry on the
    /// pattern of the whole, whose dashes fall where they did before it was cut, and none starts its pattern at the cut.
    /// </summary>
    public class XClipDashTests
    {
        private static readonly GeoVector3 Facing = new GeoVector3(0, 1, 0);

        /// <summary>A block GHD of the shapes the caller adds, all in the linetype DASHED (a dash of 12.5 and a gap of 6.25), clipped to a rectangle with the planes front 10 and back -10.</summary>
        private static CadDocument Clipped(Action<DxfBlockTableRecord> fill, out string handle)
        {
            var database = new DxfDatabase("AC1027");
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 12.5);
            dashed.SetDashLengthAt(1, -6.25);
            database.LinetypeTable.Add(dashed);
            var block = new DxfBlockTableRecord { Name = "GHD" };
            database.BlockTable.Add(block);
            fill(block);

            foreach (DxfEntity entity in block)
            {
                entity.Linetype = "DASHED";
            }

            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipdash.dxf");
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

        /// <summary>The phases of the dashes of what is drawn of the reference, by the x each starts at.</summary>
        private static List<double> Phases(CadDocument document, string handle) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .OrderBy(i => i.Bounds.MinX)
                .Select(i => i.Style.DashPhase)
                .ToList();

        [Fact]
        public void ADashedLineCutByThePlanes_GoesOnWithThePatternOfTheWhole()
        {
            // Probe (front view): the line from (0, -30) to (60, 30) in X and Z, planes 10 and -10, keeps x 20 to 40, and its
            // dashes fall where those of the whole line fall: the pattern at 20 along the line is 20 - 18.75 = 1.25 into a dash.
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfLine(new GeoPoint3(0, 100, -30), new GeoPoint3(60, 100, 30))),
                out string handle
            );

            List<double> phases = Phases(document, handle);

            Assert.Single(phases);
            Assert.Equal(20.0, phases[0], 6);
        }

        [Fact]
        public void ADashedPolylineCutByThePlanes_GoesOnWithThePatternAlongTheWhole()
        {
            // The zigzag (10, 0), (30, 30), (50, -30), (90, 20) in X and Z: it keeps x 10 to 16.67 from its start (phase 0), x
            // 36.67 to 43.33, which begins 20 + 6.67 along it in the plan, and x 66 to 82, which begins at 66 - 10 = 56.
            var polyline = new DxfPolyline { Normal = Facing, Elevation = 100.0 };
            double[] xz = { 10, 0, 30, 30, 50, -30, 90, 20 };

            for (int i = 0; i < xz.Length / 2; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(-xz[2 * i], xz[2 * i + 1]), 0.0, 0, 0);
            }

            CadDocument document = Clipped(block => block.AppendEntity(polyline), out string handle);

            List<double> phases = Phases(document, handle);

            Assert.Equal(3, phases.Count);
            Assert.Equal(0.0, phases[0], 6);
            Assert.Equal(20.0 + 20.0 / 3.0, phases[1], 6);
            Assert.Equal(56.0, phases[2], 6);
        }

        [Fact]
        public void ADashedCircleCutByThePlanes_GoesOnWithThePatternAlongTheWhole()
        {
            // A circle of radius 30 standing on its edge about (50, 100, 0), its point at the angle a at x = 50 - 30 cos a and
            // height 30 sin a, planes 10 and -10: seen from above it is the stretch x 20 to 80 walked there and back from its
            // start at x 20. The piece round x 80 starts at 160.53 degrees, 30 (1 + cos 19.47) = 58.28 along the plan; the one
            // round x 20 starts at 340.53 degrees, after the stretch there (60) and back to x 21.72, 118.28 along it.
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfCircle(new GeoPoint3(50, 100, 0), Facing, 30)),
                out string handle
            );

            List<double> phases = Phases(document, handle);

            double lean = 30.0 * Math.Sqrt(8.0 / 9.0);
            Assert.Equal(2, phases.Count);
            Assert.InRange(phases[0], 90.0 + lean - 0.1, 90.0 + lean + 0.1);
            Assert.InRange(phases[1], 30.0 + lean - 0.1, 30.0 + lean + 0.1);
        }

        [Fact]
        public void APieceThatStartsWhereTheWholeStarts_HasNoPhase_AndAContinuousOneNeverHasOne()
        {
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(60, 100, 30))),
                out string handle
            );

            // The line starts at height 0, inside the planes, and ends at 30, past the front plane 10: it keeps its first third.
            List<double> phases = Phases(document, handle);

            Assert.Single(phases);
            Assert.Equal(0.0, phases[0]);
        }
    }
}
