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
    /// the INSERT with its layers named after it, their records written as AutoCAD 2021 writes them and its entities not,
    /// followed when nested, unloaded, reloaded, its path changed and detached in AutoCAD 2021's words, each change of the
    /// drawing one undo step.
    /// </summary>
    public class XrefCommandTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xref-" + Guid.NewGuid().ToString("N")
        );

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
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" }
            );
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

            // Of the drawing referenced, the records AutoCAD 2021 writes: its layer GRID as grid|GRID, flagged 48 (dependent,
            // resolved), its own properties and nothing changed of them (1071 0); its block flagged 36, loaded. Layer 0 and the
            // entities are not written.
            string saved = document.Database.ToText();
            Assert.DoesNotContain("\n  2\nGRID\n", saved, StringComparison.Ordinal);
            Assert.DoesNotContain("\n  8\nGRID\n", saved, StringComparison.Ordinal);
            Assert.DoesNotContain("grid|0", saved, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("  2\ngrid|GRID\n 70\n48\n 62\n7\n  6\nContinuous\n", saved, StringComparison.Ordinal);
            DxfLayerTableRecord layer = document.Database.LayerTable["grid|GRID"];
            Assert.True(layer.IsResolved);
            Assert.Equal("0", layer.GetXDataForApplication("AcXRefLayerPropertyOverride")[1].Value.Trim());
            Assert.True(block.IsResolved);
            Assert.Contains("  2\ngrid\n 70\n36\n", saved, StringComparison.Ordinal);
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
            Assert.Contains(
                "Enter an option [?/Bind/Detach/Path/pathType/Unload/Reload/Overlay/Attach] <Attach>: A",
                session.History
            );
            Assert.Contains(
                "Specify insertion point or [Scale/X/Y/Z/Rotate/PScale/PX/PY/PZ/PRotate]:",
                session.History
            );
            Assert.Contains("Enter X scale factor, specify opposite corner, or [Corner/XYZ] <1>:", session.History);
            Assert.Contains("Enter Y scale factor <use X scale factor>:", session.History);
            Assert.Contains("Specify rotation angle <0>:", session.History);
            Assert.Equal("..\\xref\\grid.dxf", document.Database.BlockTable["grid"].PathName);

            Run(session, "-XREF", "?", Enter);
            int at = session.History.ToList().LastIndexOf("Xref name                         Xref Type    Path");
            Assert.True(at > 0);
            Assert.Equal("----------------------            ---------    ----------", session.History[at + 1]);
            Assert.Equal(
                "\"grid\"                            Attach       ..\\xref\\grid.dxf",
                session.History[at + 2]
            );
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
            Assert.False(document.Database.LayerTable["grid|GRID"].IsResolved);
            Assert.True(document.Database.LayerTable["grid|GRID"].IsDependent);
            Assert.False(document.Database.BlockTable["grid"].IsResolved);

            Run(session, "-XREF", "R", "grid");
            Assert.Contains("Reload Xref \"grid\": ..\\xref\\grid.dxf", session.History);
            Assert.Equal(CadReferenceStatus.Loaded, Assert.Single(document.Xrefs.GetReferences()).Status);
            Assert.Single(Items<DisplayArc2>(document));
            Assert.True(document.Database.LayerTable["grid|GRID"].IsResolved);

            Run(session, "-XREF", "B", "nothing");
            Assert.Contains("No matching xref names found.", session.History);

            Run(session, "-XREF", "D", "grid");
            Assert.False(document.Database.BlockTable.Has("grid"));
            Assert.False(document.Database.LayerTable.Has("grid|GRID"));
            Assert.Empty(All<DxfBlockReference>(document.ModelSpace));
            Assert.Empty(document.Xrefs.GetReferences());

            // Attach, two path types, unload and reload (the flags of grid|GRID and of the block, 16 and 48 again), detach: six
            // steps back to the drawing as it was.
            for (int i = 0; i < 6; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.Equal(start, document.Database.ToText());
        }

        /// <summary>
        /// As AutoCAD 2021's -XREF Attach of x.dwg wrote it, its layer WALL green on DASHED: the linetype x|DASHED with its
        /// dashes and the layer x|WALL on it, each flagged 48, the layer with the properties and description of the file's and
        /// 1071 0 of AcXRefLayerPropertyOverride; a layer the drawing has already keeps what it has.
        /// </summary>
        [Fact]
        public void TheTypedAttach_WritesTheLinetypesAndLayersOfTheDrawing_AsAutoCadWritesThem()
        {
            string grid = Grid("x", d =>
            {
                var dashed = new DxfLinetypeTableRecord
                {
                    Name = "DASHED",
                    AsciiDescription = "Dashed __ __",
                    NumDashes = 2,
                };
                dashed.SetDashLengthAt(0, 0.5);
                dashed.SetDashLengthAt(1, -0.25);
                d.LinetypeTable.Add(dashed);
                d.LayerTable.Add(
                    new DxfLayerTableRecord
                    {
                        Name = "WALL",
                        ColorIndex = 3,
                        LinetypeName = "DASHED",
                        LineWeight = DxfLineWeight.LineWeight050,
                        IsPlottable = false,
                    }
                );
                d.LayerTable["WALL"].Description = "walls";
            });
            CadDocument document = Host();
            CadCommandSession session = Session(document);

            Run(session, "-XREF", "A", grid, P(0, 0), Enter, Enter, Enter);

            string saved = document.Database.ToText();
            Assert.Contains("  2\nx|DASHED\n 70\n48\n  3\nDashed __ __\n", saved, StringComparison.Ordinal);
            DxfLinetypeTableRecord linetype = document.Database.LinetypeTable["x|DASHED"];
            Assert.Equal(2, linetype.NumDashes);
            Assert.Equal(-0.25, linetype.DashLengthAt(1));
            Assert.Equal(0.75, linetype.PatternLength);
            DxfLayerTableRecord wall = document.Database.LayerTable["x|WALL"];
            Assert.True(wall.IsDependent);
            Assert.True(wall.IsResolved);
            Assert.Equal(3, wall.ColorIndex);
            Assert.Equal("x|DASHED", wall.LinetypeName);
            Assert.Equal(DxfLineWeight.LineWeight050, wall.LineWeight);
            Assert.False(wall.IsPlottable);
            Assert.Equal("walls", wall.Description);
            Assert.Equal("0", wall.GetXDataForApplication("AcXRefLayerPropertyOverride")[1].Value.Trim());
            Assert.Equal(7, document.Database.LayerTable["x|GRID"].ColorIndex);

            // Reloaded, the records the drawing has keep what it gave them.
            Assert.True(CadLayerEditor.SetColor(document, "x|WALL", 1).Succeeded);
            Run(session, "-XREF", "R", "x");
            Assert.Equal(1, document.Database.LayerTable["x|WALL"].ColorIndex);
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
            List<DisplayPolyline2> lines = Items<DisplayPolyline2>(document)
                .Where(p => p.Layer.EndsWith("|GRID", StringComparison.Ordinal))
                .ToList();
            Assert.Contains(lines, p => p.Layer == "plan|GRID" && p.Xy[1] == 0.0);
            Assert.Contains(lines, p => p.Layer == "column|GRID" && p.Xy[1] == 5000.0);
            Assert.DoesNotContain(lines, p => p.Layer == "survey|GRID");

            // The drawing referenced, opened on its own, shows its overlay.
            CadDocument opened = CadDocument.Open(outer);
            Assert.Contains(Items<DisplayPolyline2>(opened), p => p.Layer == "survey|GRID" && p.Xy[1] == 9000.0);
        }

        [Fact]
        public void ANestedReference_IsListedUnderItsParent_OrphanedWhileItIsUnloaded_AndAutoCadsRecordOfItIsNotUnreferenced()
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
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Run(session, "-XREF", "A", plan, P(0, 0), Enter, Enter, Enter);

            // AutoCAD keeps a record of the nested reference in the drawing, which no INSERT of the drawing places.
            document.Database.AttachXref("..\\xref\\column.dxf", "column");

            IReadOnlyList<CadExternalReference> rows = document.Xrefs.GetReferences();
            Assert.Equal(new[] { "plan Loaded", "column Loaded" }, rows.Select(r => r.ToString()).ToArray());
            Assert.Equal(new[] { 0, 1 }, rows.Select(r => r.Depth).ToArray());
            Assert.Equal("plan", rows[1].ParentName);
            Assert.True(rows[1].IsNested);
            Assert.Equal(Path.Combine(_folder, "xref", "column.dxf"), rows[1].FoundAt);

            Run(session, "-XREF", "U", "plan");
            Assert.Equal(
                new[] { "plan Unloaded", "column Orphaned" },
                document.Xrefs.GetReferences().Select(r => r.ToString()).ToArray()
            );
            Run(session, "-XREF", "R", "plan");
            File.Delete(Path.Combine(_folder, "xref", "column.dxf"));
            Assert.Equal(
                new[] { "plan Loaded", "column Not Found" },
                document.Xrefs.GetReferences().Select(r => r.ToString()).ToArray()
            );
        }

        [Fact]
        public void ADrawingThatReferencesItself_IsDrawnOnce()
        {
            string loop = Path.Combine(_folder, "xref", "loop.dxf");
            Grid("loop", d =>
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 300, 0), d.AttachXref("loop.dxf", "loop"))
                )
            );
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

            Grid(more: d =>
                d.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 50.0))
            );
            File.SetLastWriteTimeUtc(grid, DateTime.UtcNow.AddMinutes(1));

            CadExternalReference changed = Assert.Single(document.Xrefs.GetReferences());
            Assert.Equal(CadReferenceStatus.NeedsReloading, changed.Status);

            // The words of AutoCAD 2021's help of the palette's List View: "Needs Reloading", "Not Found".
            Assert.Equal("Needs Reloading", changed.StatusText);
            Assert.Single(Items<DisplayArc2>(document));

            Run(session, "-XREF", "R", "grid");
            Assert.Contains("\"grid\" loaded.", session.History);
            Assert.Equal(CadReferenceStatus.Loaded, Assert.Single(document.Xrefs.GetReferences()).Status);
            Assert.Equal(2, Items<DisplayArc2>(document).Count);
        }

        [Fact]
        public void Xopen_RefusesALineAsAutoCadDoes_ThenHandsTheWindowTheFileOfTheXrefPicked()
        {
            string grid = Grid();
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            Run(session, "-XREF", "A", grid, P(0, 0), Enter, Enter, Enter);
            string line = Assert.Single(All<DxfLine>(document.ModelSpace)).Handle;
            string insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).Handle;
            var opened = new List<string>();
            Answer<string>(session, "XOPEN", opened.Add);

            Run(session, "XOPEN", new[] { line }, new[] { insert });

            Assert.Contains("Select Xref:", session.History);
            Assert.Contains("Object is not an Xref", session.History);
            Assert.Equal(new[] { Path.GetFullPath(grid) }, opened);
            Assert.False(session.IsRunning);

            // Enter ends it saying nothing; a file gone is refused as -XREF Reload refuses it.
            int said = session.History.Count;
            Run(session, "XOPEN", Enter);
            Assert.Equal(new[] { "Command: XOPEN", "Select Xref:" }, session.History.Skip(said).ToArray());
            File.Delete(grid);
            Run(session, "XOPEN", new[] { insert });
            Assert.Contains("\"..\\xref\\grid.dxf\" cannot be found.", session.History);
            Assert.Single(opened);
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

            Assert.Equal(
                ".\\grid.dxf",
                CadXrefManager.PathFor("D:\\Plans\\grid.dxf", CadPathType.Relative, "D:\\Plans")
            );
            Assert.Equal(
                ".\\xref\\grid.dxf",
                CadXrefManager.PathFor("D:\\Plans\\xref\\grid.dxf", CadPathType.Relative, "D:\\Plans\\")
            );
            Assert.Equal(
                "..\\..\\grid.dxf",
                CadXrefManager.PathFor("D:\\grid.dxf", CadPathType.Relative, "D:\\Plans\\A")
            );
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
            host.ModelSpace.AppendEntity(
                new DxfBlockReference(GeoPoint3.Origin, host.AttachXref("..\\xref\\big.dxf", "big"))
            );
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

            Assert.True(
                directCount == referencedCount,
                directCount + " items alone, " + referencedCount + " referenced."
            );
            Assert.True(
                referenced <= 2.0 * direct,
                "Shown as an external reference in "
                    + referenced.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                    + " ms, opened alone in "
                    + direct.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
                    + " ms."
            );
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
            Assert.Contains(
                "Xref \"grid\" names another file: give this one a name of its own, as name=path.",
                session.History
            );
            Run(session, "-XREF", "A", broken);
            Assert.Contains("Error: Unable to load " + Path.GetFullPath(broken) + ".", session.History);
            Run(session, "-XREF", "A", grid, P(500, 0), Enter, Enter, Enter);
            Assert.Contains("Xref \"grid\" has already been defined.", session.History);

            Assert.Equal(2, All<DxfBlockReference>(document.ModelSpace).Count);
            Assert.Equal(2, Items<DisplayArc2>(document).Count);
        }
    }
}
