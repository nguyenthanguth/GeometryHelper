using System;
using System.IO;
using System.Linq;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The sample routine that ships in the Lisps folder, GH-DrawingInfo.lsp, as the build leaves it beside GeometryHelper.Cad.dll:
    /// it defines GHINFO when a drawing opens and writes nothing, and GHINFO writes the drawing's name, units, current layer and
    /// text style, the objects of model space by type, the layers and blocks, and the extents. It changes nothing.
    /// </summary>
    public sealed class LispDrawingInfoSampleTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Opened(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.Document.SupportRoots = new CadSupportFolders(AppDomain.CurrentDomain.BaseDirectory);
            session.Document.SupportFolders = new string[0];
            new LispStartup().OnDocumentOpened(session);
            return session;
        }

        /// <summary>A drawing in millimetres with two circles and a line in model space, the layer Steel, and a block of one line.</summary>
        private static CadDocument TwoCirclesAndALine()
        {
            CadDocument document = Blank();
            DxfDatabase database = document.Database;
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel", ColorIndex = 1 });
            var block = new DxfBlockTableRecord { Name = "BOLT" };
            database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 10));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(50, 0, 0), GeoVector3.ZAxis, 10));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(120, 80, 0)));
            return document;
        }

        [Fact]
        public void TheSampleFile_IsBesideTheProgramInTheLispsFolder()
        {
            string program = AppDomain.CurrentDomain.BaseDirectory;

            Assert.True(File.Exists(Path.Combine(program, "Lisps", "GH-DrawingInfo.lsp")));
        }

        [Fact]
        public void ANewDrawing_HasGhinfoDefined_AndNothingWasWrittenWhenTheFileLoaded()
        {
            CadCommandSession session = Opened(Blank());

            Assert.Empty(session.History);
            session.Feed("(boundp (quote c:ghinfo))");
            Assert.Equal("T", session.History.Last().Trim());
        }

        [Fact]
        public void Ghinfo_OnTwoCirclesAndALine_WritesTheNameUnitsLayerStyleObjectsLayersBlocksAndExtents()
        {
            CadDocument document = TwoCirclesAndALine();
            CadCommandSession session = Opened(document);

            session.Feed("GHINFO");

            string[] lines = session.History.Skip(1).Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToArray();
            Assert.StartsWith("Drawing: blank.dxf in ", lines[0]);
            Assert.Equal(
                new[]
                {
                    "Units: INSUNITS 4, LUNITS 2",
                    "Current layer: 0, text style: Standard",
                    "Model space: 3 objects",
                    "  CIRCLE   2",
                    "  LINE     1",
                    "Layers: 2, blocks: 1",
                },
                lines.Skip(1).Take(6)
            );
            Assert.StartsWith("Extents: (", lines[7]);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void Ghinfo_OnADrawingWithNothingInModelSpace_SaysZeroObjectsAndNoTypes()
        {
            CadCommandSession session = Opened(Blank());

            session.Feed("GHINFO");

            string[] lines = session.History.Skip(1).Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToArray();
            Assert.Equal("Model space: 0 objects", lines[3]);
            Assert.Equal("Layers: 1, blocks: 0", lines[4]);
        }
    }
}
