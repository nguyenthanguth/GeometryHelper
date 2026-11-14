using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// XCLIP's Clipdepth as AutoCAD 2021 does it (probes of accoreconsole through standard input, drawings read back with
    /// PNGOUT): a point answers with its distance from the boundary's plane, a front plane that is not above the back one by a
    /// millionth is refused, and the planes cut what the block holds in height, the block shown between them, in the units of
    /// the clip system, a line cut where it crosses one.
    /// </summary>
    public class XClipDepthTests
    {
        /// <summary>
        /// A block GHD of five lines 100 long along x: one rising from (0, 0, 0) to (100, 0, 100), and four flat at y 20, 30, 40
        /// and 50, at heights -30, 10, 30 and -5. Inserted at (0, 0, 0), or where it is given, scaled as it is given.
        /// </summary>
        private static CadDocument WithLines(
            out CadCommandSession session,
            out string handle,
            double lift = 0.0,
            double scale = 1.0
        )
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHD" };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 100)));
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 20, -30), new GeoPoint3(100, 20, -30)));
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 30, 10), new GeoPoint3(100, 30, 10)));
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 40, 30), new GeoPoint3(100, 40, 30)));
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 50, -5), new GeoPoint3(100, 50, -5)));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, lift), block)
            {
                ScaleFactors = new GeoVector3(scale, scale, scale),
            };
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipdepth.dxf");
            session = Session(document);
            return document;
        }

        private static DxfBlockReference Reference(CadDocument document, string handle) =>
            Entity<DxfBlockReference>(document, handle);

        private static void ClipToRectangle(CadCommandSession session, string handle, GeoPoint2 a, GeoPoint2 b) =>
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", a, b);

        private static void Depth(CadCommandSession session, string handle, string front, string back) =>
            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", "Distance", front, "Distance", back);

        /// <summary>The items of the reference that are lines: the boxes of what is drawn of each, ordered by their y.</summary>
        private static List<DisplayBox> Lines(CadDocument document, string handle) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle && !i.IsClipFrame)
                .Select(i => i.Bounds)
                .OrderBy(b => b.MinY)
                .ToList();

        [Fact]
        public void AFrontPlaneNotAboveTheBackOneByAMillionth_IsRefused_AsAutoCadRefusesIt()
        {
            CadDocument document = WithLines(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(-20, -20), P(200, 200));

            // AutoCAD 2021: front 10 and back 10 is refused, as is back 9.999999; back 9.999998 is taken.
            Depth(session, handle, "10", "10");
            Assert.Equal(
                "Front clipping plane behind back clipping plane.  Clipdepth not applied.",
                session.History.Last()
            );
            Assert.Null(Reference(document, handle).SpatialFilter.FrontClip);

            Depth(session, handle, "10", "9.999999");
            Assert.Null(Reference(document, handle).SpatialFilter.FrontClip);

            Depth(session, handle, "10", "9.999998");
            Assert.Equal(10.0, Reference(document, handle).SpatialFilter.FrontClip);
            Assert.Equal(9.999998, Reference(document, handle).SpatialFilter.BackClip);
        }

        [Fact]
        public void APointAnswered_IsItsDistanceFromTheBoundaryPlane_TheUcsPlaneItIsPickedOn()
        {
            CadDocument document = WithLines(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(-20, -20), P(200, 200));

            // The clip is made in the world's plane; the system then lifted 15: a point picked is 15 above the plane (AutoCAD
            // 2021 wrote front 15 for the point 0,0, and 20 for the point 0,0,5).
            document.Ucs.SetCurrent(new CadUcs(new GeoPoint3(0, 0, 15), GeoVector3.XAxis, GeoVector3.YAxis));
            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", P(1, 1), "Remove");

            Assert.Equal(15.0, Reference(document, handle).SpatialFilter.FrontClip);
            Assert.Null(Reference(document, handle).SpatialFilter.BackClip);
        }

        [Fact]
        public void APointAnswered_OnTheSystemTheClipWasMadeIn_IsNought()
        {
            CadDocument document = WithLines(out CadCommandSession session, out string handle);
            document.Ucs.SetCurrent(new CadUcs(new GeoPoint3(0, 0, 15), GeoVector3.XAxis, GeoVector3.YAxis));
            ClipToRectangle(session, handle, P(-20, -20), P(200, 200));
            Assert.Equal(15.0, Reference(document, handle).SpatialFilter.Origin.Z, 9);

            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", P(1, 1), "Remove");

            Assert.Equal(0.0, Reference(document, handle).SpatialFilter.FrontClip.Value, 9);
        }

        [Fact]
        public void TheBlockBetweenThePlanes_IsShown_TheRestIsNot_AndALineIsCutWhereItCrossesOne()
        {
            CadDocument document = WithLines(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(-20, -20), P(200, 200));
            Assert.Equal(5, Lines(document, handle).Count);

            // AutoCAD 2021, PNGOUT of this drawing: with front 20 and back -10 the rising line is left from x 0 to 20, the flat
            // ones at heights -30 and 30 are gone, those at 10 and -5 stay whole.
            Depth(session, handle, "20", "-10");

            List<DisplayBox> lines = Lines(document, handle);
            Assert.Equal(3, lines.Count);
            Assert.Equal(0.0, lines[0].MinY, 9);
            Assert.Equal(0.0, lines[0].MinX, 9);
            Assert.Equal(20.0, lines[0].MaxX, 9);
            Assert.Equal(30.0, lines[1].MinY, 9);
            Assert.Equal(100.0, lines[1].MaxX, 9);
            Assert.Equal(50.0, lines[2].MinY, 9);
            Assert.Equal(100.0, lines[2].MaxX, 9);
        }

        [Fact]
        public void APlaneThroughALine_KeepsIt_AsAutoCadKeepsALineOnTheBoundaryPlane()
        {
            CadDocument document = WithLines(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(-20, -20), P(200, 200));

            // The flat line at height 10 lies on the front plane, the one at -5 on the back plane: both are shown.
            Depth(session, handle, "10", "-5");

            List<DisplayBox> lines = Lines(document, handle);
            Assert.Equal(3, lines.Count);
            Assert.Equal(new[] { 0.0, 30.0, 50.0 }, lines.Select(b => b.MinY));
            Assert.Equal(10.0, lines[0].MaxX, 9);
        }

        [Fact]
        public void TheDistances_AreInTheUnitsOfTheClipSystem_ThatOfAScaledReferenceToo()
        {
            CadDocument document = WithLines(out CadCommandSession session, out string handle, scale: 2.0);
            ClipToRectangle(session, handle, P(-20, -20), P(300, 300));

            // The block scaled by 2: the rising line stands from (0, 0, 0) to (200, 0, 200); the planes at 20 and -10 of the
            // world (AutoCAD 2021 left its first 20 of x, as it did for the block not scaled).
            Depth(session, handle, "20", "-10");

            List<DisplayBox> lines = Lines(document, handle);
            Assert.Equal(20.0, lines[0].MaxX, 9);
            Assert.Equal(0.0, lines[0].MinY, 9);
        }

        [Fact]
        public void TheClipPlane_IsTheOneItWasMadeAt_WhereTheReferenceStandsLifted()
        {
            CadDocument document = WithLines(out CadCommandSession session, out string handle, lift: 15.0);
            ClipToRectangle(session, handle, P(-20, -20), P(200, 200));

            // The reference lifted 15 and clipped in the world's plane: the rising line is at 15 to 115, and of the planes 20
            // and -10 it keeps its first 5 of x (AutoCAD 2021 drew the same 5).
            Depth(session, handle, "20", "-10");

            List<DisplayBox> lines = Lines(document, handle);
            Assert.Equal(0.0, lines[0].MinX, 9);
            Assert.Equal(5.0, lines[0].MaxX, 9);
        }

        [Fact]
        public void APlaneRemoved_ShowsTheBlockAgain_AndAClipSwitchedOffShowsItAll()
        {
            CadDocument document = WithLines(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(-20, -20), P(200, 200));
            Depth(session, handle, "20", "-10");
            Assert.Equal(3, Lines(document, handle).Count);

            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", "Remove", "Remove");
            Assert.Equal(5, Lines(document, handle).Count);

            Depth(session, handle, "20", "-10");
            Run(session, "XCLIP", new[] { handle }, Enter, "OFF");
            List<DisplayBox> all = Lines(document, handle);
            Assert.Equal(5, all.Count);
            Assert.Equal(100.0, all[0].MaxX, 9);
        }
    }
}
