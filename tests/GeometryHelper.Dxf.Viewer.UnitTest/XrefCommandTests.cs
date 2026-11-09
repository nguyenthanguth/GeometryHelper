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
    /// External references of C18b: a DXF attached or overlaid by XATTACH and -XREF, shown as its drawing's model space under
    /// the INSERT with its layers named after it, never written into the drawing, followed when nested, unloaded, reloaded,
    /// its path changed and detached in AutoCAD 2021's words, each change of the drawing one undo step.
    /// </summary>
    public class XrefCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gk", "xref-" + Guid.NewGuid().ToString("N"));

        public XrefCommandTests()
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
        /// A metric drawing of a grid line on layer GRID from (0, 0) to (1 000, 0) and a circle of radius 100 about the origin on
        /// layer 0, its base point at (0, 0), saved in the folder xref.
        /// </summary>
        private string Grid(string name = "grid", Action<DxfDatabase> more = null)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID" });
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" });
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0));
            more?.Invoke(database);
            string path = Path.Combine(_folder, "xref", name + ".dxf");
            database.Save(path);
            return path;
        }

        /// <summary>A metric drawing with a line from (0, 0) to (0, 500), saved in the folder plans as host.dxf and opened.</summary>
        private CadDocument Host()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 500, 0)));
            string path = Path.Combine(_folder, "plans", "host.dxf");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            database.Save(path);
            return CadDocument.Open(path);
        }

        private static void Answer<T>(CadCommandSession session, string name, Action<T> step)
        {
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == name)
                {
                    step((T)e.Payload);
                    e.Handled = true;
                }
            };
        }

        private static List<T> Items<T>(CadDocument document)
            where T : DisplayItem2 => document.GetDisplay(document.ModelSpace).Items.OfType<T>().ToList();

        [Fact]
        public void ADrawingAttachedByTheDialog_IsShownUnderItsInsert_ItsLayersNamedAfterIt_AsOneUndoStep()
        {
            string grid = Grid();
            CadDocument document = Host();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);
            Answer<CadAttachRequest>(session, "XATTACH", r =>
            {
                r.FileName = grid;
                Assert.Equal("grid", r.Name);
                Assert.Equal("..\\xref\\grid.dxf", r.SavedPath);
                Assert.Equal(DxfUnits.Millimeters, r.Units);
                Assert.Equal(1.0, r.UnitFactor);
                r.SpecifyPointOnScreen = false;
                r.Point = new GeoPoint3(100, 200, 0);
                r.Action = CadDialogAction.Ok;
            });

            Run(session, "XA");

            Assert.Contains("Attach Xref \"grid\": ..\\xref\\grid.dxf", session.History);
            Assert.Contains("\"grid\" loaded.", session.History);
            DxfBlockTableRecord block = document.Database.BlockTable["grid"];
            Assert.True(block.IsFromExternalReference);
            Assert.False(block.IsFromOverlayReference);
            Assert.Equal("..\\xref\\grid.dxf", block.PathName);
            Assert.Equal(0, block.Count);
            DxfBlockReference insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Near(new GeoPoint3(100, 200, 0), insert.Position);

            DisplayPolyline2 line = Assert.Single(Items<DisplayPolyline2>(document), p => p.Layer == "grid|GRID");
            Assert.Equal(new[] { 100.0, 200.0, 1100.0, 200.0 }, line.Xy);
            Assert.Equal(insert.Handle, line.Handle);
            DisplayArc2 circle = Assert.Single(Items<DisplayArc2>(document));
            Assert.Equal("0", circle.Layer);
            Assert.Equal(100.0, circle.Cx, 9);
            Assert.Equal(200.0, circle.Cy, 9);
            Assert.Contains(document.GetPickIndex(document.ModelSpace).Pick(600, 200, 1.0), h => h == insert.Handle);

            CadExternalReference row = Assert.Single(document.Xrefs.GetReferences());
            Assert.Equal(CadReferenceStatus.Loaded, row.Status);
            Assert.Equal("Attach", row.TypeName);
            Assert.Equal(Path.GetFullPath(grid), row.FoundAt);
            Assert.Equal(CadPathType.Relative, row.PathType);

            // Nothing of the drawing referenced is written into this one.
            string saved = document.Database.ToText();
            Assert.DoesNotContain("\n  2\nGRID\n", saved, StringComparison.Ordinal);
            Assert.DoesNotContain("\n  8\nGRID\n", saved, StringComparison.Ordinal);
            Assert.DoesNotContain("grid|", saved, StringComparison.OrdinalIgnoreCase);
            Assert.Single(All<DxfLine>(document.ModelSpace));

            Assert.Equal("XATTACH", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
            Assert.Empty(Items<DisplayArc2>(document));
        }

        [Fact]
        public void TheTypedXref_AttachesListsChangesThePathTypeUnloadsReloadsAndDetaches_InAutoCadsWords()
        {
            string grid = Grid();
            CadDocument document = Host();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "-XR", "A", grid, P(0, 0), Enter, Enter, Enter);
            Assert.Contains("Enter an option [?/Bind/Detach/Path/pathType/Unload/Reload/Overlay/Attach] <Attach>: A", session.History);
            Assert.Contains("Specify insertion point or [Scale/X/Y/Z/Rotate/PScale/PX/PY/PZ/PRotate]:", session.History);
            Assert.Contains("Enter X scale factor, specify opposite corner, or [Corner/XYZ] <1>:", session.History);
            Assert.Contains("Enter Y scale factor <use X scale factor>:", session.History);
            Assert.Contains("Specify rotation angle <0>:", session.History);
            Assert.Equal("..\\xref\\grid.dxf", document.Database.BlockTable["grid"].PathName);

            Run(session, "-XREF", "?", Enter);
            int at = session.History.ToList().LastIndexOf("Xref name                         Xref Type    Path");
            Assert.True(at > 0);
            Assert.Equal("----------------------            ---------    ----------", session.History[at + 1]);
            Assert.Equal("\"grid\"                            Attach       ..\\xref\\grid.dxf", session.History[at + 2]);
            Assert.Equal("Total Xref(s): 1", session.History[at + 3]);

            Run(session, "-XREF", "T", "grid", "F");
            Assert.Equal(Path.GetFullPath(grid), document.Database.BlockTable["grid"].PathName);
            Assert.Contains("1 path(s) changed.", session.History);
            Run(session, "-XREF", "T", "grid", "R");
            Assert.Equal("..\\xref\\grid.dxf", document.Database.BlockTable["grid"].PathName);

            Run(session, "-XREF", "U", "gr*");
            Assert.Contains("Xref \"grid\": ..\\xref\\grid.dxf", session.History);
            Assert.Contains("\"grid\" is unloaded.", session.History);
            Assert.Equal(CadReferenceStatus.Unloaded, Assert.Single(document.Xrefs.GetReferences()).Status);
            Assert.Empty(Items<DisplayArc2>(document));

            Run(session, "-XREF", "R", "grid");
            Assert.Contains("Reload Xref \"grid\": ..\\xref\\grid.dxf", session.History);
            Assert.Equal(CadReferenceStatus.Loaded, Assert.Single(document.Xrefs.GetReferences()).Status);
            Assert.Single(Items<DisplayArc2>(document));

            Run(session, "-XREF", "B", "grid");
            Assert.Contains("Bind is not offered: a DXF external reference is shown, never written into the drawing.", session.History);

            Run(session, "-XREF", "D", "grid");
            Assert.False(document.Database.BlockTable.Has("grid"));
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            Assert.Empty(document.Xrefs.GetReferences());

            // Attach, two path types, detach: four steps back to the drawing as it was.
            for (int i = 0; i < 4; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void AnOverlay_IsFlaggedTwelve_AndShown_ButADrawingReferencedShowsItsAttachmentsAndNotItsOverlays()
        {
            string inner = Grid("column");
            string hidden = Grid("survey");
            string outer = Grid("plan", d =>
            {
                DxfBlockTableRecord column = d.AttachXref("column.dxf", "column");
                d.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 5000, 0), column));
                DxfBlockTableRecord survey = d.OverlayXref("survey.dxf", "survey");
                d.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 9000, 0), survey));
            });
            Assert.True(File.Exists(inner) && File.Exists(hidden));
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Run(session, "-XREF", "O", outer, P(0, 0), Enter, Enter, Enter);

            DxfBlockTableRecord plan = document.Database.BlockTable["plan"];
            Assert.True(plan.IsFromOverlayReference);
            Assert.Contains("Overlay Xref \"plan\": ..\\xref\\plan.dxf", session.History);
            List<DisplayPolyline2> lines = Items<DisplayPolyline2>(document).Where(p => p.Layer.EndsWith("|GRID", StringComparison.Ordinal)).ToList();
            Assert.Contains(lines, p => p.Layer == "plan|GRID" && p.Xy[1] == 0.0);
            Assert.Contains(lines, p => p.Layer == "column|GRID" && p.Xy[1] == 5000.0);
            Assert.DoesNotContain(lines, p => p.Layer == "survey|GRID");

            // The drawing referenced, opened on its own, shows its overlay.
            CadDocument opened = CadDocument.Open(outer);
            Assert.Contains(Items<DisplayPolyline2>(opened), p => p.Layer == "survey|GRID" && p.Xy[1] == 9000.0);
        }

        [Fact]
        public void ADrawingThatReferencesItself_IsDrawnOnce()
        {
            string loop = Path.Combine(_folder, "xref", "loop.dxf");
            Grid("loop", d => d.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 300, 0), d.AttachXref("loop.dxf", "loop"))));
            CadDocument document = CadDocument.Open(loop);

            Assert.Single(Items<DisplayPolyline2>(document), p => p.Layer == "GRID");
            Assert.Single(Items<DisplayPolyline2>(document), p => p.Layer == "loop|GRID");
        }

        [Fact]
        public void AReferenceNotFound_ShowsItsPathAtItsInsertionPoint_AndIsFoundAgainInTheDrawingsFolderOrASupportFolder()
        {
            string grid = Grid();
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Run(session, "-XREF", "A", grid, P(50, 60), Enter, Enter, Enter);
            string insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).Handle;
            string moved = Path.Combine(_folder, "elsewhere", "grid.dxf");
            Directory.CreateDirectory(Path.GetDirectoryName(moved));
            File.Move(grid, moved);
            document.Regen();

            DisplayText2 path = Assert.Single(Items<DisplayText2>(document));
            Assert.Equal("..\\xref\\grid.dxf", path.Text);
            Assert.Equal(50.0, path.X, 9);
            Assert.Equal(60.0, path.Y, 9);
            Assert.Equal(insert, path.Handle);
            Assert.Equal(CadReferenceStatus.NotFound, Assert.Single(document.Xrefs.GetReferences()).Status);

            document.SupportFolders = new[] { Path.GetDirectoryName(moved) };
            document.Regen();
            Assert.Empty(Items<DisplayText2>(document));
            CadExternalReference row = Assert.Single(document.Xrefs.GetReferences());
            Assert.Equal(CadReferenceStatus.Loaded, row.Status);
            Assert.Equal(Path.GetFullPath(moved), row.FoundAt);
        }

        [Fact]
        public void AFileChangedOnDisk_StandsNeedsReloading_UntilReloadReadsItAgain()
        {
            string grid = Grid();
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Run(session, "-XREF", "A", grid, P(0, 0), Enter, Enter, Enter);
            Assert.Single(Items<DisplayArc2>(document));

            Grid(more: d => d.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 50.0)));
            File.SetLastWriteTimeUtc(grid, DateTime.UtcNow.AddMinutes(1));

            Assert.Equal(CadReferenceStatus.NeedsReloading, Assert.Single(document.Xrefs.GetReferences()).Status);
            Assert.Single(Items<DisplayArc2>(document));

            Run(session, "-XREF", "R", "grid");
            Assert.Contains("\"grid\" loaded.", session.History);
            Assert.Equal(CadReferenceStatus.Loaded, Assert.Single(document.Xrefs.GetReferences()).Status);
            Assert.Equal(2, Items<DisplayArc2>(document).Count);
        }

        [Fact]
        public void TheNewPathOfAReference_IsAskedUntilItNamesAFileFound_AndTheOldOneKeptOnEnter()
        {
            string grid = Grid();
            CadDocument document = Host();
            string copy = Path.Combine(_folder, "plans", "grid2.dxf");
            File.Copy(grid, copy);
            CadCommandSession session = Session(document);
            Run(session, "-XREF", "A", grid, P(0, 0), Enter, Enter, Enter);

            Run(session, "-XREF", "P", "grid", "nowhere.dxf", "grid2.dxf");

            Assert.Contains("xref name: \"grid\"", session.History);
            Assert.Contains("Old path: ..\\xref\\grid.dxf", session.History);
            Assert.Contains("\"nowhere.dxf\": Can't find file in search path.", session.History);
            Assert.Equal("grid2.dxf", document.Database.BlockTable["grid"].PathName);
            Assert.Equal(Path.GetFullPath(copy), Assert.Single(document.Xrefs.GetReferences()).FoundAt);

            Run(session, "-XREF", "P", "grid", Enter);
            Assert.Contains("Path unchanged.", session.History);
            Assert.Equal("grid2.dxf", document.Database.BlockTable["grid"].PathName);
        }

        [Fact]
        public void ARelativePath_IsTheFullPathUntilTheDrawingIsSaved_AndPathForWritesAutoCadsForms()
        {
            string grid = Grid();
            CadDocument unsaved = Blank();
            var request = new CadAttachRequest(unsaved, CadAttachKind.Xref) { FileName = grid };

            Assert.True(request.IsRelativeFallingBack);
            Assert.Equal(Path.GetFullPath(grid), request.SavedPath);
            request.PathType = CadPathType.None;
            Assert.Equal("grid.dxf", request.SavedPath);

            Assert.Equal(".\\grid.dxf", CadXrefManager.PathFor("D:\\Plans\\grid.dxf", CadPathType.Relative, "D:\\Plans"));
            Assert.Equal(".\\xref\\grid.dxf", CadXrefManager.PathFor("D:\\Plans\\xref\\grid.dxf", CadPathType.Relative, "D:\\Plans\\"));
            Assert.Equal("..\\..\\grid.dxf", CadXrefManager.PathFor("D:\\grid.dxf", CadPathType.Relative, "D:\\Plans\\A"));
            Assert.Equal("E:\\grid.dxf", CadXrefManager.PathFor("E:\\grid.dxf", CadPathType.Relative, "D:\\Plans"));
            Assert.Equal(CadPathType.Full, CadXrefManager.PathTypeOf("D:\\Plans\\grid.dxf"));
            Assert.Equal(CadPathType.Relative, CadXrefManager.PathTypeOf("..\\grid.dxf"));
            Assert.Equal(CadPathType.None, CadXrefManager.PathTypeOf("grid.dxf"));
        }

        /// <summary>
        /// A drawing of 100 000 entities shown as an external reference of a drawing of one line: read and drawn within twice
        /// the time of opening and drawing it on its own, each timed at its best of three. On the maintainer's machine both take
        /// about a second under .NET Framework 4.8.
        /// </summary>
        [Fact]
        public void AnXrefOfAHundredThousandEntities_IsReadAndShownWithinTwiceTheTimeOfOpeningItsDrawing()
        {
            string big = Path.Combine(_folder, "xref", "big.dxf");
            File.WriteAllText(big, BigDrawing.Text(100000));
            DxfDatabase host = CadTemplate.CreateMetric();
            host.ModelSpace.AppendEntity(new DxfBlockReference(GeoPoint3.Origin, host.AttachXref("..\\xref\\big.dxf", "big")));
            string hostPath = Path.Combine(_folder, "plans", "host.dxf");
            Directory.CreateDirectory(Path.GetDirectoryName(hostPath));
            host.Save(hostPath);

            double direct = double.MaxValue;
            double referenced = double.MaxValue;
            int directCount = 0;
            int referencedCount = 0;

            for (int i = 0; i < 3; i++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                CadDocument alone = CadDocument.Open(big);
                directCount = alone.GetDisplay(alone.ModelSpace).Count;
                direct = Math.Min(direct, watch.Elapsed.TotalMilliseconds);

                watch.Restart();
                CadDocument shown = CadDocument.Open(hostPath);
                referencedCount = shown.GetDisplay(shown.ModelSpace).Count;
                referenced = Math.Min(referenced, watch.Elapsed.TotalMilliseconds);
            }

            Assert.True(directCount == referencedCount, directCount + " items alone, " + referencedCount + " referenced.");
            Assert.True(referenced <= 2.0 * direct, "Shown as an external reference in " + referenced.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms, opened alone in " + direct.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " ms.");
        }

        [Fact]
        public void ANameOfABlock_OrOfAnotherFile_IsRefused_AndAFileNotADrawingIsUnresolved()
        {
            string grid = Grid();
            string other = Grid("other");
            string broken = Path.Combine(_folder, "xref", "broken.dxf");
            File.WriteAllText(broken, "not a drawing");
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Run(session, "-XREF", "A", grid, P(0, 0), Enter, Enter, Enter);

            Run(session, "-XREF", "A", "grid=" + other);
            Assert.Contains("Xref \"grid\" names another file: give this one a name of its own, as name=path.", session.History);
            Run(session, "-XREF", "A", broken);
            Assert.Contains("Error: Unable to load " + Path.GetFullPath(broken) + ".", session.History);
            Run(session, "-XREF", "A", grid, P(500, 0), Enter, Enter, Enter);
            Assert.Contains("Xref \"grid\" has already been defined.", session.History);

            Assert.Equal(2, All<DxfBlockReference>(document.ModelSpace).Count);
            Assert.Equal(2, Items<DisplayArc2>(document).Count);
        }
    }
}
