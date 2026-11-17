using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// XCLIP's Clipdepth on what is not a line, as AutoCAD 2021 draws it (PNGOUT of accoreconsole, front view and plan): a
    /// circle, an arc and a polyline that cross a plane are cut at it, what an INSERT inside the block holds is cut by the same
    /// planes carried through its placing, and an external reference is cut with its base point at the insertion point.
    /// </summary>
    public class XClipDepthCurvesTests : IDisposable
    {
        private static readonly GeoVector3 Facing = new GeoVector3(0, 1, 0);

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xclipcurves-" + Guid.NewGuid().ToString("N")
        );

        public XClipDepthCurvesTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file a watcher still holds is left for the system to clear.
            }
        }

        /// <summary>
        /// A block GHC holding the shapes the caller adds, inserted at (0, 0, 0) and clipped to the rectangle (-20, -20) to
        /// (300, 300) with the planes front 10 and back -10, which a viewer sees from above: a shape in the plane of the
        /// world's X and Z, standing on its edge, is a segment of the x axis.
        /// </summary>
        private static CadDocument Clipped(
            Action<DxfBlockTableRecord> fill,
            out string handle,
            double front = 10,
            double back = -10
        )
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHC" };
            database.BlockTable.Add(block);
            fill(block);
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipcurves.dxf");
            CadCommandSession session = Session(document);
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", P(-20, -20), P(300, 300));
            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "Clipdepth",
                "Distance",
                front.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Distance",
                back.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            return document;
        }

        /// <summary>The boxes of what is drawn of the reference, the frame of the clip left out, by their x then y.</summary>
        private static List<DisplayBox> Drawn(CadDocument document, string handle) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .Select(i => i.Bounds)
                .OrderBy(b => b.MinY)
                .ThenBy(b => b.MinX)
                .ToList();

        /// <summary>A polyline in the plane of the world's X and Z at y 100 (the normal (0, 1, 0), its x axis the world's -x).</summary>
        private static DxfPolyline Standing(bool closed, double bulge, params double[] xz)
        {
            var polyline = new DxfPolyline { Normal = Facing, Elevation = 100.0 };

            for (int i = 0; i < xz.Length / 2; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(-xz[2 * i], xz[2 * i + 1]), i == 0 ? bulge : 0.0, 0, 0);
            }

            polyline.Closed = closed;
            return polyline;
        }

        private static void AssertBox(DisplayBox box, double minX, double maxX)
        {
            Assert.Equal(minX, box.MinX, 3);
            Assert.Equal(maxX, box.MaxX, 3);
        }

        [Fact]
        public void ACircleOnItsEdge_IsCutAtThePlanes_ToTheTwoArcsBetweenThem()
        {
            // Probe: the circle about (50, 20, 0) of radius 30 in the plane of X and Z, planes 10 and -10, left of it two arcs
            // at the ends of its width; its width 20 to 80 runs 50 +- 30 cos(asin(1/3)) = 21.716 and 78.284 at the planes.
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfCircle(new GeoPoint3(50, 20, 0), Facing, 30)),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            // The arc at x 20 holds the start of the circle (its angle 0) and is two, one each side of it (XClipSeamDashTests).
            Assert.Equal(3, boxes.Count);
            AssertBox(boxes[0], 20.0, 50.0 - 30.0 * Math.Sqrt(8.0 / 9.0));
            AssertBox(boxes[1], 20.0, 50.0 - 30.0 * Math.Sqrt(8.0 / 9.0));
            AssertBox(boxes[2], 50.0 + 30.0 * Math.Sqrt(8.0 / 9.0), 80.0);
        }

        [Fact]
        public void AnArcOverTheTop_IsCutAtThePlanes_ToItsTwoEnds()
        {
            // The arc of radius 30 about (50, 60, 0) from 0 to 180 degrees of the system of its normal, over the top (z from 0
            // to 30): between the planes 10 and -10 it keeps the height up to 10 at each end.
            CadDocument document = Clipped(
                block => block.AppendEntity(new DxfArc(new GeoPoint3(50, 60, 0), Facing, 30, 0, Math.PI)),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(2, boxes.Count);
            AssertBox(boxes[0], 20.0, 50.0 - 30.0 * Math.Sqrt(8.0 / 9.0));
            AssertBox(boxes[1], 50.0 + 30.0 * Math.Sqrt(8.0 / 9.0), 80.0);
        }

        [Fact]
        public void AnArcBetweenThePlanes_IsDrawnWhole_AndOneAboveThemIsLeftOut()
        {
            CadDocument document = Clipped(
                block =>
                {
                    block.AppendEntity(new DxfArc(new GeoPoint3(50, 60, 0), Facing, 5, 0, Math.PI));
                    block.AppendEntity(new DxfArc(new GeoPoint3(50, 80, 40), Facing, 5, 0, Math.PI));
                },
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Single(boxes);
            AssertBox(boxes[0], 45.0, 55.0);
        }

        [Fact]
        public void APolyline_IsCutWhereEachSegmentCrossesAPlane()
        {
            // Probe (blue in the front view): the zigzag (10, 0), (30, 30), (50, -30), (90, 20) in X and Z, planes 10 and -10.
            // Segment 1 keeps x 10 to 16.667 (z 0 to 10), segment 2 x 36.667 to 43.333, segment 3 x 66 to 82.
            CadDocument document = Clipped(
                block => block.AppendEntity(Standing(false, 0.0, 10, 0, 30, 30, 50, -30, 90, 20)),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(3, boxes.Count);
            AssertBox(boxes[0], 10.0, 10.0 + 20.0 / 3.0);
            AssertBox(boxes[1], 30.0 + 20.0 / 3.0, 30.0 + 40.0 / 3.0);
            AssertBox(boxes[2], 66.0, 82.0);
        }

        [Fact]
        public void AnArcSegmentOfAPolyline_IsCutWhereItsWaveCrossesAPlane()
        {
            // The vertices (10, 0) and (50, 0) with a bulge of 1 stand on a half circle of radius 20 over the top (z to 20):
            // the planes 10 and -10 leave 30 -+ 20 cos(30 degrees) at the ends.
            CadDocument document = Clipped(
                block => block.AppendEntity(Standing(false, 1.0, 10, 0, 50, 0)),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(2, boxes.Count);
            AssertBox(boxes[0], 10.0, 30.0 - 20.0 * Math.Cos(Math.PI / 6.0));
            AssertBox(boxes[1], 30.0 + 20.0 * Math.Cos(Math.PI / 6.0), 50.0);
        }

        [Fact]
        public void AClosedPolyline_IsCutAtThePlanes_ToTheSidesBetweenThem()
        {
            // The parallelogram (10, -30), (60, -30), (90, 30), (40, 30) in X and Z: its sides at -30 and 30 are out, the other
            // two keep x 70 to 80 and x 20 to 30.
            CadDocument document = Clipped(
                block => block.AppendEntity(Standing(true, 0.0, 10, -30, 60, -30, 90, 30, 40, 30)),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(2, boxes.Count);
            AssertBox(boxes[0], 20.0, 30.0);
            AssertBox(boxes[1], 70.0, 80.0);
        }

        [Fact]
        public void APolylineThroughAPlaneAtAVertex_IsCutThere()
        {
            // The vertices (10, 0), (30, 10), (50, 20) in X and Z: the middle one stands on the front plane 10, so the part
            // above it, x 30 to 50, is left out and x 10 to 30 stays.
            CadDocument document = Clipped(
                block => block.AppendEntity(Standing(false, 0.0, 10, 0, 30, 10, 50, 20)),
                out string handle
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Single(boxes);
            AssertBox(boxes[0], 10.0, 30.0);
        }

        /// <summary>A block INNER of five lines 100 long along x: one rising from (0, 0, 0) to (100, 0, 100), four flat at y 20, 30, 40 and 50, at heights -30, 10, 30 and -5.</summary>
        private static DxfBlockTableRecord Inner(DxfDatabase database)
        {
            var inner = new DxfBlockTableRecord { Name = "INNER" };
            database.BlockTable.Add(inner);
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 100)));
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 20, -30), new GeoPoint3(100, 20, -30)));
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 30, 10), new GeoPoint3(100, 30, 10)));
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 40, 30), new GeoPoint3(100, 40, 30)));
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 50, -5), new GeoPoint3(100, 50, -5)));
            return inner;
        }

        private static CadDocument ClippedHost(
            DxfDatabase database,
            DxfBlockReference insert,
            out string handle,
            double front,
            double back
        )
        {
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipnested.dxf");
            CadCommandSession session = Session(document);
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", P(-20, -20), P(400, 400));
            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "Clipdepth",
                "Distance",
                front.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Distance",
                back.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            return document;
        }

        [Fact]
        public void AnInsertInsideTheBlock_IsCutByTheSamePlanes_ThroughItsPlacing()
        {
            // Probe (plan): OUTER holds INNER scaled by 2 and lifted 5; the planes 20 and -10 of the world leave, of its lines,
            // the rising one, at height 5 + 2 x, for its first 15 of x (z 5 to 35 is up to 20 at x 7.5 of the block, 15 of the
            // world), and the flat one at y 50 of the block, at height -5 (y 100 of the world), whole; the rest are out.
            var database = new DxfDatabase("AC1027");
            DxfBlockTableRecord inner = Inner(database);
            var outer = new DxfBlockTableRecord { Name = "OUTER" };
            database.BlockTable.Add(outer);
            outer.AppendEntity(
                new DxfBlockReference(new GeoPoint3(0, 0, 5), inner) { ScaleFactors = new GeoVector3(2, 2, 2) }
            );
            CadDocument document = ClippedHost(
                database,
                new DxfBlockReference(new GeoPoint3(0, 0, 0), outer),
                out string handle,
                20,
                -10
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(2, boxes.Count);
            Assert.Equal(0.0, boxes[0].MinY, 3);
            AssertBox(boxes[0], 0.0, 15.0);
            Assert.Equal(100.0, boxes[1].MinY, 3);
            AssertBox(boxes[1], 0.0, 200.0);
        }

        [Fact]
        public void AnInsertTiltedInsideTheBlock_IsCutAtTheHeightItsLinesStandAt()
        {
            // A line of a block from (0, 0, 0) to (0, 100, 0) put in OUTER with its plane tilted 45 degrees about X (its y axis
            // goes to (0, cos 45, sin 45)) rises as it goes: of the planes 20 and -10 of the world it keeps what is below
            // z 20, which is its first 20 of y in the plan.
            var database = new DxfDatabase("AC1027");
            var inner = new DxfBlockTableRecord { Name = "INNER" };
            database.BlockTable.Add(inner);
            inner.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0)));
            var outer = new DxfBlockTableRecord { Name = "OUTER" };
            database.BlockTable.Add(outer);
            double s = Math.Sqrt(0.5);
            outer.AppendEntity(
                new DxfBlockReference(new GeoPoint3(0, 0, 0), inner) { Normal = new GeoVector3(0, -s, s) }
            );
            CadDocument document = ClippedHost(
                database,
                new DxfBlockReference(new GeoPoint3(0, 0, 0), outer),
                out string handle,
                20,
                -10
            );

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Single(boxes);
            Assert.Equal(0.0, boxes[0].MinY, 3);
            Assert.Equal(20.0, boxes[0].MaxY, 3);
        }

        /// <summary>A drawing grid.dxf of the five lines of INNER, with the base point given, and the host that attaches it clipped.</summary>
        private CadDocument ClippedXref(GeoPoint3 basePoint, out string handle)
        {
            var source = new DxfDatabase("AC1027") { Insbase = basePoint };
            source.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 100)));
            source.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 20, -30), new GeoPoint3(100, 20, -30)));
            source.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 30, 10), new GeoPoint3(100, 30, 10)));
            source.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 40, 30), new GeoPoint3(100, 40, 30)));
            source.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 50, -5), new GeoPoint3(100, 50, -5)));
            source.Save(Path.Combine(_folder, "lines.dxf"));

            var host = new DxfDatabase("AC1027");
            DxfBlockTableRecord xref = host.AttachXref(".\\lines.dxf", "lines");
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), xref);
            host.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            string path = Path.Combine(_folder, "host.dxf");
            host.Save(path);
            CadDocument document = CadDocument.Open(path);
            CadCommandSession session = Session(document);
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", P(-20, -20), P(200, 200));
            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", "Distance", "20", "Distance", "-10");
            return document;
        }

        [Fact]
        public void AnExternalReference_IsCutByThePlanes_AsABlockIs()
        {
            // Probe (plan, a DWG of the five lines attached at (0, 0, 0)): front 20 and back -10 left the rising line for its
            // first 20 of x, the flat lines at 10 and -5 whole, those at -30 and 30 out.
            CadDocument document = ClippedXref(new GeoPoint3(0, 0, 0), out string handle);

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(3, boxes.Count);
            AssertBox(boxes[0], 0.0, 20.0);
            Assert.Equal(30.0, boxes[1].MinY, 3);
            Assert.Equal(50.0, boxes[2].MinY, 3);
        }

        [Fact]
        public void AnExternalReference_IsCutFromItsBasePoint_NotFromTheOriginOfItsDrawing()
        {
            // Probe: the same DWG with its base point at (0, 0, 10) stands 10 lower where it is attached at the origin: the
            // rising line is left for its first 30 of x (height x - 10), the flat lines of the drawing at 10 and 30 (0 and 20
            // there) whole, those at -30 and -5 out.
            CadDocument document = ClippedXref(new GeoPoint3(0, 0, 10), out string handle);

            List<DisplayBox> boxes = Drawn(document, handle);

            Assert.Equal(3, boxes.Count);
            AssertBox(boxes[0], 0.0, 30.0);
            Assert.Equal(30.0, boxes[1].MinY, 3);
            Assert.Equal(40.0, boxes[2].MinY, 3);
        }
    }
}
