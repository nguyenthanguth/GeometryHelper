using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The layers of an external reference (<c>grid|GRID</c>) a drawing keeps, as AutoCAD 2021 keeps them under VISRETAIN 1:
    /// on, frozen, locked, colour, linetype and lineweight changed in the drawing and kept there, each change marked in the
    /// bits of <c>AcXRefLayerPropertyOverride</c>, the rest read from the drawing referenced; the name and the current layer
    /// refused in AutoCAD 2021's words.
    /// </summary>
    public class XrefLayerTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gk", "xreflayer-" + Guid.NewGuid().ToString("N"));

        public XrefLayerTests()
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

        /// <summary>A drawing of a line from (0, 0) to (1 000, 0) on layer GRID of a colour of the index, saved as grid.dxf.</summary>
        private string Grid(int color)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID", ColorIndex = color });
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" });
            string path = Path.Combine(_folder, "grid.dxf");
            database.Save(path);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(color));
            return path;
        }

        /// <summary>
        /// host.dxf with grid.dxf attached at the origin and the record AutoCAD 2021 writes for its layer: grid|GRID, flagged 48
        /// (dependent, resolved), green as the file was when attached, and 1071 0 of AcXRefLayerPropertyOverride, nothing
        /// changed. Opened.
        /// </summary>
        private CadDocument Host()
        {
            string grid = Grid(3);
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), database.AttachXref(".\\grid.dxf", "grid")));
            string text = database.ToText();
            int table = text.IndexOf("  0\nTABLE\n  2\nLAYER\n", StringComparison.Ordinal);
            int end = text.IndexOf("  0\nENDTAB\n", table, StringComparison.Ordinal);
            const string Record = "  0\nLAYER\n  5\nFFF1\n330\n2\n100\nAcDbSymbolTableRecord\n100\nAcDbLayerTableRecord\n  2\ngrid|GRID\n 70\n48\n 62\n3\n  6\nContinuous\n370\n-3\n";
            string path = Path.Combine(_folder, "host.dxf");
            File.WriteAllText(path, text.Insert(end, Record));
            Assert.True(File.Exists(grid));
            CadDocument document = CadDocument.Open(path);
            DxfLayerTableRecord layer = document.Database.LayerTable["grid|GRID"];
            Assert.True(layer.IsDependent);
            layer.XData = new[] { new DxfTypedValue(1001, "AcXRefLayerPropertyOverride"), new DxfTypedValue(1071, "0") };
            document.Regen();
            return document;
        }

        private static DisplayPolyline2 Line(CadDocument document)
            => document.GetDisplay(document.ModelSpace).Items.OfType<DisplayPolyline2>().SingleOrDefault(p => p.Layer == "grid|GRID");

        private static string Bits(CadDocument document)
            => document.Database.LayerTable["grid|GRID"].GetXDataForApplication("AcXRefLayerPropertyOverride")[1].Value.Trim();

        [Fact]
        public void ALayerNotChanged_IsDrawnAsTheFileReferencedHasItNow_AndOneChangedAsTheDrawingKeepsIt()
        {
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Assert.Equal(0x00FF00, Line(document).Style.Rgb);

            // The file made blue: the drawing changed nothing of the layer, which is drawn blue once reloaded.
            Grid(5);
            Run(session, "-XREF", "R", "grid");
            Assert.Equal(0x0000FF, Line(document).Style.Rgb);

            // Made red in the drawing, one undo step marked 16 and 256 as AutoCAD 2021 marks it, and kept red when the file is made cyan.
            Assert.True(CadLayerEditor.SetColor(document, "grid|GRID", 1).Succeeded);
            Assert.Equal("272", Bits(document));
            Assert.Equal(0xFF0000, Line(document).Style.Rgb);
            Grid(4);
            Run(session, "-XREF", "R", "grid");
            Assert.Equal(0xFF0000, Line(document).Style.Rgb);

            // -LAYER switches it off and on, freezes, thaws, locks and unlocks it, each marked.
            Run(session, "-LAYER", "OFF", "grid|GRID", Enter);
            Assert.Null(Line(document));
            Run(session, "-LAYER", "ON", "grid|GRID", "F", "grid|GRID", "T", "grid|GRID", "LO", "grid|GRID", Enter);
            Assert.NotNull(Line(document));
            Assert.Equal("279", Bits(document));
            Assert.True(CadLayerEditor.SetLineWeight(document, "grid|GRID", DxfLineWeight.LineWeight050).Succeeded);
            Assert.Equal("343", Bits(document));

            // The changes are the drawing's: each undone, the colour last, back to the file's cyan.
            while (document.NextUndo != null && document.NextUndo.Name != "-XREF")
            {
                Assert.True(document.Undo());
            }

            Assert.Equal("0", Bits(document));
            Assert.Equal(0x00FFFF, Line(document).Style.Rgb);
        }

        /// <summary>
        /// -LAYER TRansparency, Plot and Description, asked as AutoCAD 2021 asks them, each marked on a layer of a reference as
        /// AutoCAD 2021 marked b|WALL: 128 TRansparency, 8 Plot, 1024 Description; the name list offers the current layer.
        /// </summary>
        [Fact]
        public void TransparencyPlotAndDescription_AreSetByLayer_AndMarkedOnALayerOfAReference()
        {
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Run(session, "-LAYER", "TR", "95", "50", "grid|GRID", Enter);
            Assert.Contains("Enter transparency value (0-90): 95", session.History);
            Assert.Contains("Requires an integer between 0 and 90.", session.History);
            Assert.Contains("Enter name list of layer(s) for transparency 50% <0>: grid|GRID", session.History);
            DxfLayerTableRecord layer = document.Database.LayerTable["grid|GRID"];
            Assert.Equal(127, layer.Transparency.Alpha);
            Assert.Equal("128", Bits(document));

            Run(session, "-LAYER", "P", "N", "grid|GRID", Enter);
            Assert.Contains("Enter a plotting preference [Plot/No plot] <Plot>: N", session.History);
            Assert.Contains("Enter layer name(s) for this plot preference <0>: grid|GRID", session.History);
            Assert.False(document.Database.LayerTable["grid|GRID"].IsPlottable);
            Assert.Equal("136", Bits(document));

            Run(session, "-LAYER", "D", "Grid lines", "grid|GRID", Enter);
            Assert.Contains("Enter layer description: Grid lines", session.History);
            Assert.Equal("Grid lines", document.Database.LayerTable["grid|GRID"].Description);
            Assert.Equal("1160", Bits(document));

            // Enter takes the current layer, 0, which is the drawing's own and marked nothing.
            Run(session, "-LAYER", "TR", "20", Enter, Enter);
            Assert.Equal(204, document.Database.LayerTable["0"].Transparency.Alpha);
            Assert.Null(document.Database.LayerTable["0"].GetXDataForApplication("AcXRefLayerPropertyOverride"));
        }

        [Fact]
        public void TheNameAndTheCurrentLayer_AreRefusedInAutoCadsWords()
        {
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Run(session, "-LAYER", "S", "grid|GRID", Enter);
            Assert.Contains("Invalid layer name.", session.History);
            Run(session, "-LAYER", "N", "grid|X", Enter);
            Assert.Contains("\"grid|X\" is an invalid layer name.", session.History);
            Assert.False(document.Database.LayerTable.Has("grid|X"));

            Assert.Equal("Cannot set CLAYER to that value.", CadLayerEditor.SetCurrent(document, "grid|GRID").Message);
            Assert.False(CadLayerEditor.Delete(document, "grid|GRID").Succeeded);
            Assert.Equal("0", document.Database.Clayer);
            Assert.Equal("0", Bits(document));
        }
    }
}
