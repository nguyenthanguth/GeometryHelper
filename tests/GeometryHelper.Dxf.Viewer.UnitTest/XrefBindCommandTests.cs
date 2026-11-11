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
    /// -XREF Bind as AutoCAD 2021's (probe xref-bind): BINDTYPE 0 renames the records of the drawing after the reference
    /// (<c>grid$0$GRID</c>), 1 merges them by name and says each block the drawing has already; the references the drawing
    /// places are bound with it; an unloaded reference, a file not found and a nested reference refused in AutoCAD's words;
    /// one undo step, the drawing shown as it was.
    /// </summary>
    public class XrefBindCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xrefbind-" + Guid.NewGuid().ToString("N")
        );

        public XrefBindCommandTests()
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
        /// A metric drawing of a line on layer GRID from (0, 0) to (1 000, 0), a circle of radius 100 about the origin on layer
        /// 0 and an INSERT of the block TAG at (500, 0), its base point (50, 0, 0), saved in the folder xref.
        /// </summary>
        private string Grid(string name = "grid", Action<DxfDatabase> more = null)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.Insbase = new GeoPoint3(50, 0, 0);
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID", ColorIndex = 3 });
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" }
            );
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0));
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            database.BlockTable.Add(tag);
            tag.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 10, 0)));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(500, 0, 0), tag));
            more?.Invoke(database);
            string path = Path.Combine(_folder, "xref", name + ".dxf");
            database.Save(path);
            return path;
        }

        /// <summary>A metric drawing saved in the folder plans as host.dxf and opened, the drawing given attached at (0, 0).</summary>
        private CadDocument Host(string attached, out CadCommandSession session, Action<DxfDatabase> more = null)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            more?.Invoke(database);
            string path = Path.Combine(_folder, "plans", "host.dxf");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            database.Save(path);
            CadDocument document = CadDocument.Open(path);
            session = Session(document);
            Run(session, "-XREF", "A", attached, P(0, 0), Enter, Enter, Enter);
            return document;
        }

        private static List<DisplayPolyline2> Lines(CadDocument document) =>
            document.GetDisplay(document.ModelSpace).Items.OfType<DisplayPolyline2>().ToList();

        [Fact]
        public void BindTypeZero_RenamesTheRecordsAfterTheReference_AndShowsTheDrawingWhereItWas_AsOneUndoStep()
        {
            string grid = Grid();
            CadDocument document = Host(grid, out CadCommandSession session);
            string attached = document.Database.ToText();
            DisplayPolyline2 before = Assert.Single(Lines(document), p => p.Layer == "grid|GRID");

            Run(session, "-XREF", "B", "grid");

            Assert.Contains("Enter xref name(s) to bind: grid", session.History);
            DxfBlockTableRecord block = document.Database.BlockTable["grid"];
            Assert.False(block.IsFromExternalReference);
            Assert.Equal("..\\xref\\grid.dxf", block.PathName);
            Assert.Equal(new GeoPoint3(50, 0, 0), block.Origin);
            Assert.False(document.Database.LayerTable.Has("grid|GRID"));
            DxfLayerTableRecord layer = document.Database.LayerTable["grid$0$GRID"];
            Assert.False(layer.IsDependent);
            Assert.Equal(3, layer.ColorIndex);
            Assert.True(document.Database.BlockTable.Has("grid$0$TAG"));
            Assert.Empty(document.Xrefs.GetReferences());

            DisplayPolyline2 after = Assert.Single(Lines(document), p => p.Layer == "grid$0$GRID");
            Assert.Equal(before.Xy, after.Xy);
            Assert.Equal(new[] { -50.0, 0.0, 950.0, 0.0 }, after.Xy);
            AssertReopens(document);

            Assert.Equal("-XREF", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(attached, document.Database.ToText());
        }

        [Fact]
        public void BindTypeOne_MergesTheRecordsByName_AndSaysEachBlockTheDrawingHasAlready()
        {
            string grid = Grid();
            CadDocument document = Host(grid, out CadCommandSession session, d =>
            {
                d.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID", ColorIndex = 6 });
                d.BlockTable.Add(new DxfBlockTableRecord { Name = "TAG" });
            });

            Run(session, "BINDTYPE", "1");
            Run(session, "-XREF", "B", "grid");

            Assert.Contains("Duplicate Block \"TAG\" ignored.", session.History);
            Assert.Equal(6, document.Database.LayerTable["GRID"].ColorIndex);
            Assert.False(document.Database.LayerTable.Has("grid|GRID"));
            Assert.DoesNotContain("grid$0$", document.Database.ToText(), StringComparison.Ordinal);
            Assert.Equal(0, document.Database.BlockTable["TAG"].Count);
            Assert.Single(Lines(document), p => p.Layer == "GRID");
        }

        [Fact]
        public void TheReferencesADrawingPlaces_AreBoundWithIt_ItsOverlaysLeftOut_AndANestedOneAloneIsRefused()
        {
            Grid("column");
            Grid("survey");
            string plan = Grid("plan", d =>
            {
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref("column.dxf", "column"))
                );
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 9000, 0), d.OverlayXref("survey.dxf", "survey"))
                );
            });
            CadDocument document = Host(plan, out CadCommandSession session);
            string attached = document.Database.ToText();

            Run(session, "-XREF", "B", "column");
            Assert.Contains("Xref \"column\" is nested and cannot be bound.", session.History);
            Run(session, "-XREF", "B", "nothing");
            Assert.Contains("No matching xref names found.", session.History);
            Assert.Equal(attached, document.Database.ToText());

            Run(session, "-XREF", "B", "plan");

            Assert.False(document.Database.BlockTable["plan"].IsFromExternalReference);
            Assert.False(document.Database.BlockTable["column"].IsFromExternalReference);
            Assert.True(document.Database.LayerTable.Has("plan$0$GRID"));
            Assert.True(document.Database.LayerTable.Has("column$0$GRID"));
            Assert.False(document.Database.BlockTable.Has("survey"));
            Assert.Empty(document.Xrefs.GetReferences());
            Assert.Single(Lines(document), p => p.Layer == "column$0$GRID");
        }

        [Fact]
        public void AnUnloadedReference_OrAFileNotFound_IsNotBound_InAutoCadsWords()
        {
            string grid = Grid();
            CadDocument document = Host(grid, out CadCommandSession session);
            Run(session, "-XREF", "U", "grid");
            string unloaded = document.Database.ToText();

            Run(session, "-XREF", "B", "grid");
            Assert.Contains("Cannot bind unloaded xref(s).  Reload them first:", session.History);
            Assert.Contains("Xref \"grid\" is unloaded.", session.History);
            Assert.Equal(unloaded, document.Database.ToText());

            Run(session, "-XREF", "R", "grid");
            File.Delete(grid);
            string reloaded = document.Database.ToText();
            Run(session, "-XREF", "B", "grid");
            Assert.Contains("xref \"grid\" is currently not found. Reload or detach before binding.", session.History);
            Assert.Equal(reloaded, document.Database.ToText());
        }

        [Fact]
        public void ADrawingWhoseNestedReferenceIsNotFound_IsNotBound_TheNestedOneNamed()
        {
            string column = Grid("column");
            string plan = Grid("plan", d =>
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref("column.dxf", "column"))
                )
            );
            CadDocument document = Host(plan, out CadCommandSession session);
            File.Delete(column);
            string attached = document.Database.ToText();

            Run(session, "-XREF", "B", "plan");

            Assert.Contains(
                "xref \"column\" is currently not found. Reload or detach before binding.",
                session.History
            );
            Assert.Equal(attached, document.Database.ToText());
        }
    }
}
