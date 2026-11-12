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
    /// A drawing with an external reference bound, saved as a DXF and as a DWG and opened again (probe xrefs3-audit: AutoCAD
    /// 2021's AUDIT of the four files, BINDTYPE 0 and 1, a DXF and a DWG each, found 0 errors and listed the records as the
    /// presenter lists them): the reference is an ordinary block, no record of it is dependent any more, nothing of the
    /// drawing is lost, and a reference left unbound is still one.
    /// </summary>
    public class XrefBoundSaveTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xrefbound-" + Guid.NewGuid().ToString("N")
        );

        public XrefBoundSaveTests()
        {
            Directory.CreateDirectory(Path.Combine(_folder, "xref"));
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
        /// A drawing of the layers GRID (green, DASHED) and TXT, a text style, a dimension style, a block TAG, a line, a circle
        /// and a text, saved in the folder xref; the second also places the first nested.
        /// </summary>
        private string Rich(string name, string nested = null)
        {
            DxfDatabase d = CadTemplate.CreateMetric();
            d.LinetypeTable.Add(
                new DxfLinetypeTableRecord
                {
                    Name = "DASHED",
                    AsciiDescription = "__ __ __",
                    NumDashes = 2,
                }
            );
            d.LayerTable.Add(
                new DxfLayerTableRecord
                {
                    Name = "GRID",
                    ColorIndex = 3,
                    LinetypeName = "DASHED",
                }
            );
            d.LayerTable.Add(new DxfLayerTableRecord { Name = "TXT", ColorIndex = 1 });
            d.TextStyleTable.Add(
                new DxfTextStyleTableRecord
                {
                    Name = "ROMANS",
                    FileName = "romans.shx",
                    TextSize = 2.5,
                }
            );
            d.DimStyleTable.Add(
                new DxfDimStyleTableRecord
                {
                    Name = "GH_25",
                    Dimasz = 2.5,
                    Dimtxt = 2.5,
                }
            );
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            d.BlockTable.Add(tag);
            tag.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 10, 0)));
            d.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID", Linetype = "DASHED" }
            );
            d.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0));
            d.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(500, 0, 0), tag) { Layer = "GRID" });
            d.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = "B-12",
                    Position = new GeoPoint3(10, 20, 0),
                    Height = 3.5,
                    TextStyleName = "ROMANS",
                    Layer = "TXT",
                }
            );

            if (nested != null)
            {
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref(nested + ".dxf", nested))
                );
            }

            string path = Path.Combine(_folder, "xref", name + ".dxf");
            d.Save(path);
            return path;
        }

        [Theory]
        [InlineData("0", ".dxf")]
        [InlineData("0", ".dwg")]
        [InlineData("1", ".dxf")]
        [InlineData("1", ".dwg")]
        public void ABoundReference_SavedAndOpenedAgain_HasNoDependentRecord_AndLosesNothing(
            string bindType,
            string extension
        )
        {
            string grid = Rich("grid");
            string plan = Rich("plan", "column");
            Rich("column");
            string annex = Rich("annex");
            string hostPath = Path.Combine(_folder, "host.dxf");
            CadTemplate.CreateMetric().Save(hostPath);
            CadDocument document = CadDocument.Open(hostPath);
            CadCommandSession session = Session(document);

            foreach (string file in new[] { grid, plan, annex })
            {
                Run(session, "-XREF", "A", file, P(0, 0), Enter, Enter, Enter);
            }

            Run(session, "BINDTYPE", bindType);
            Run(session, "-XREF", "B", "grid,plan");
            Assert.Single(document.Xrefs.GetReferences());
            string saved = Path.Combine(_folder, "bound" + extension);
            document.SaveAs(saved);

            CadDocument again = CadDocument.Open(saved);
            DxfDatabase database = again.Database;

            Assert.Equal("annex", Assert.Single(again.Xrefs.GetReferences()).Name);
            Assert.False(database.BlockTable["grid"].IsFromExternalReference);
            Assert.False(database.BlockTable["plan"].IsFromExternalReference);
            Assert.False(database.BlockTable["column"].IsFromExternalReference);
            Assert.True(database.BlockTable["annex"].IsFromExternalReference);
            // Only the records of the reference left unbound, annex, are still dependent.
            Assert.DoesNotContain(database.LayerTable, l =>
                l.IsDependent && !l.Name.StartsWith("annex|", StringComparison.Ordinal)
            );
            Assert.DoesNotContain(database.LinetypeTable, l =>
                l.IsDependent && !l.Name.StartsWith("annex|", StringComparison.Ordinal)
            );
            Assert.DoesNotContain(database.TextStyleTable, s =>
                s.IsDependent && !s.Name.StartsWith("annex|", StringComparison.Ordinal)
            );
            Assert.DoesNotContain(database.DimStyleTable, s =>
                s.IsDependent && !s.Name.StartsWith("annex|", StringComparison.Ordinal)
            );
            Assert.DoesNotContain(database.LayerTable, l => l.Name.IndexOf('|') >= 0 && !l.IsDependent);
            Assert.Equal(
                document.Database.LayerTable.Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal),
                database.LayerTable.Select(l => l.Name).OrderBy(n => n, StringComparer.Ordinal)
            );
            Assert.Equal(
                document
                    .Database.BlockTable.Where(b => !b.IsLayout)
                    .Select(b => b.Name)
                    .OrderBy(n => n, StringComparer.Ordinal),
                database.BlockTable.Where(b => !b.IsLayout).Select(b => b.Name).OrderBy(n => n, StringComparer.Ordinal)
            );
        }
    }
}
