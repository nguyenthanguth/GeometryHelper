using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The clip boundary of an external reference and the base point ($INSBASE) of its drawing, as AutoCAD 2021 draws it
    /// (PNGOUT and entget of a drawing attached at (100, 0, 0) and clipped to the rectangle (130, 10) to (190, 40) of the
    /// world, its base point (70, 0, 0): the filter holds the drawing's own 100 to 160, and the drawing reloaded with a
    /// base point of 70 where it had 0 showed a boundary of 20 to 80 from -50 to 10): the boundary goes with the content when
    /// the base point changes.
    /// </summary>
    public class XClipXrefBaseTests
    {
        /// <summary>A folder under the temporary files that is made anew and cleared after.</summary>
        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gk", "xclipbase-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void Clear(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException)
            {
                // A file a watcher still holds is left for the system to clear.
            }
        }

        /// <summary>grid.dxf: a line from (0, 0) to (1 000, 0), its base point as given.</summary>
        private static void WriteGrid(string folder, GeoPoint3 basePoint)
        {
            DxfDatabase grid = CadTemplate.CreateMetric();
            grid.Insbase = basePoint;
            grid.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
            grid.Save(Path.Combine(folder, "grid.dxf"));
        }

        /// <summary>host.dxf: the drawing grid.dxf attached at a point, its record's origin that of the drawing's base point.</summary>
        private static string WriteHost(string folder, GeoPoint3 at, GeoPoint3 recorded)
        {
            DxfDatabase host = CadTemplate.CreateMetric();
            DxfBlockTableRecord block = host.AttachXref(".\\grid.dxf", "grid");
            block.Origin = recorded;
            host.ModelSpace.AppendEntity(new DxfBlockReference(at, block));
            string path = Path.Combine(folder, "host.dxf");
            host.Save(path);
            return path;
        }

        private static List<DisplayItem2> Drawn(CadDocument document, string handle) =>
            document.GetDisplay(document.ModelSpace).Items.Where(i => i.Handle == handle && !i.IsClipFrame).ToList();

        [Fact]
        public void AnExternalReferenceClipped_FollowsItsContent_WhenTheDrawingsBasePointChanges()
        {
            // Probe (PNGOUT): the boundary x 20 to 80 of a drawing whose base point was 0 and is 70 now stands at x -50 to 10.
            string folder = Folder();

            try
            {
                WriteGrid(folder, new GeoPoint3(0, 0, 0));
                string path = WriteHost(folder, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 0));
                CadDocument document = CadDocument.Open(path);
                string handle = All<DxfBlockReference>(document.ModelSpace).Single().Handle;
                Run(
                    Session(document),
                    "XCLIP",
                    new[] { handle },
                    Enter,
                    "New boundary",
                    "Rectangular",
                    P(20, -50),
                    P(80, 50)
                );
                document.Database.Save(path);
                WriteGrid(folder, new GeoPoint3(70, 0, 0));

                CadDocument reopened = CadDocument.Open(path);
                string again = All<DxfBlockReference>(reopened.ModelSpace).Single().Handle;
                DisplayItem2 line = Assert.Single(Drawn(reopened, again));

                Assert.Equal(-70.0, line.Bounds.MinX, 9);
                Assert.Equal(-50.0, line.BlockClip.Bounds.MinX, 9);
                Assert.Equal(10.0, line.BlockClip.Bounds.MaxX, 9);
                Assert.Contains(reopened.GetDisplay(reopened.ModelSpace).Items, i =>
                    i.IsClipFrame && Math.Abs(i.Bounds.MinX + 50.0) < 1E-6
                );
            }
            finally
            {
                Clear(folder);
            }
        }

        [Fact]
        public void XClipOnAnExternalReference_PutsTheBoundaryWhereItIsPicked_WhateverTheDrawingsBasePoint()
        {
            // The drawing's base point is (70, 0, 0) and the host records an origin of 0, as the library attaches one: the
            // boundary picked at x 130 to 190 of the world stands there, and holds the drawing's 100 to 160.
            string folder = Folder();

            try
            {
                WriteGrid(folder, new GeoPoint3(70, 0, 0));
                string path = WriteHost(folder, new GeoPoint3(100, 0, 0), new GeoPoint3(0, 0, 0));
                CadDocument document = CadDocument.Open(path);
                string handle = All<DxfBlockReference>(document.ModelSpace).Single().Handle;

                Run(
                    Session(document),
                    "XCLIP",
                    new[] { handle },
                    Enter,
                    "New boundary",
                    "Rectangular",
                    P(130, -50),
                    P(190, 50)
                );

                DisplayItem2 line = Assert.Single(Drawn(document, handle));
                Assert.Equal(30.0, line.Bounds.MinX, 9);
                Assert.Equal(130.0, line.BlockClip.Bounds.MinX, 9);
                Assert.Equal(190.0, line.BlockClip.Bounds.MaxX, 9);
                var reference = (DxfBlockReference)document.FindEntity(handle);
                Assert.Equal(100.0, reference.ClipOutline[0].X, 9);
            }
            finally
            {
                Clear(folder);
            }
        }
    }
}
