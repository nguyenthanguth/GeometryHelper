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
    /// Editing an external reference in place, as AutoCAD 2021's REFEDIT does: the objects of the drawing the reference names
    /// copied into the host where the reference draws them, REFCLOSE Save writing them back into that drawing's own file (DWG
    /// or DXF, in the version it had, its other objects and layers as they were), the names the session gave
    /// (<c>$0$WALL</c>, <c>SITE|WALL</c>) going back to the file's own, every reference of it drawn from the file saved;
    /// Discard leaving the file untouched; a file gone, read only, locked or changed on disk refused in the words of
    /// AcRefEdRes.dll. The xref files are built here, in a folder of their own.
    /// </summary>
    public class XrefEditTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xrefedit-" + Guid.NewGuid().ToString("N")
        );

        public XrefEditTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                foreach (string file in Directory.GetFiles(_folder))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file a watcher still holds is left for the system to clear.
            }
        }

        private string PathOf(string name) => Path.Combine(_folder, name);

        /// <summary>
        /// A drawing of a line from (0, 0) to (100, 0) and a circle of 10 about (50, 30) on the layer WALL (red), and a line from
        /// (0, 200) to (100, 200) on the layer ROOF (blue), saved under a name: a DWG where it ends in .dwg, in the version given.
        /// </summary>
        private string Site(string name, string version = null)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "WALL", ColorIndex = 1 });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "ROOF", ColorIndex = 5 });
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALL" }
            );
            database.ModelSpace.AppendEntity(
                new DxfCircle(new GeoPoint3(50, 30, 0), new GeoVector3(0, 0, 1), 10.0) { Layer = "WALL" }
            );
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 200, 0), new GeoPoint3(100, 200, 0)) { Layer = "ROOF" }
            );
            Save(database, name, version);
            return PathOf(name);
        }

        private void Save(DxfDatabase database, string name, string version = null) =>
            Assert.Equal(string.Empty, CadDrawingFile.Save(database, PathOf(name), version, null, null));

        /// <summary>host.dxf with the drawing of the file given attached as SITE at (1 000, 0), opened from its file.</summary>
        private CadDocument Host(string xref = "site.dxf", Action<DxfDatabase> more = null)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(1000, 0, 0), database.AttachXref(".\\" + xref, "SITE"))
            );
            more?.Invoke(database);
            string path = PathOf("host.dxf");
            database.Save(path);
            return CadDocument.Open(path);
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfBlockReference Insert(CadDocument document) => All<DxfBlockReference>(document.ModelSpace)[0];

        private static int Drawn(CadDocument document) =>
            document.GetDisplay(document.ModelSpace).Items.Count(i => i.Handle == Insert(document).Handle);

        /// <summary>REFEDIT on the reference with every object taken.</summary>
        private static CadCommandSession Opened(CadDocument document)
        {
            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0));
            return Run(session, Enter);
        }

        private DxfDatabase Read(string name) => CadDrawingFile.Open(PathOf(name));

        [Fact]
        public void Refedit_OfAnExternalReference_BringsTheObjectsOfItsDrawingToWhereTheReferenceDrawsThem()
        {
            Site("site.dxf");
            byte[] before = File.ReadAllBytes(PathOf("site.dxf"));
            CadDocument document = Host();
            Assert.Equal(3, Drawn(document));

            CadCommandSession session = Start(document, "REFEDIT");
            Assert.Equal("Select reference:", session.PromptText);
            Run(session, P(1050, 0));
            Assert.Equal("Enter object selection method [All/Nested] <All>:", session.PromptText);
            Run(session, Enter);

            Assert.Contains("3 Added to working set", session.History);
            Assert.NotNull(document.RefEdit);
            Assert.Equal("SITE", document.RefEdit.Name);
            Assert.Equal(PathOf("site.dxf"), document.RefEdit.XrefFile, ignoreCase: true);
            Assert.Equal(3, document.RefEdit.WorkingSet.Count);

            // The line of the drawing is at (1 000, 0)-(1 100, 0), its circle about (1 050, 30), its roof line at y 200.
            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(2, lines.Count);
            DxfLine wall = lines.Single(l => Math.Abs(l.StartPoint.Y) < 1E-9);
            Near(new GeoPoint3(1000, 0, 0), wall.StartPoint);
            Near(new GeoPoint3(1100, 0, 0), wall.EndPoint);
            Near(new GeoPoint3(1050, 30, 0), Assert.Single(All<DxfCircle>(document.ModelSpace)).Center);

            // The layers are copies of their own, WALL red as the file had it; the reference no longer draws what stands in the host.
            Assert.Equal("$0$WALL", wall.Layer);
            Assert.Equal(1, document.Database.LayerTable["$0$WALL"].ColorIndex);
            Assert.Equal("$0$ROOF", lines.Single(l => Math.Abs(l.StartPoint.Y - 200) < 1E-9).Layer);
            Assert.Equal(0, Drawn(document));

            // The file is not touched while the session lasts.
            Assert.Equal(before, File.ReadAllBytes(PathOf("site.dxf")));
        }

        [Fact]
        public void AnObjectMovedAndSaved_IsMovedInTheFileOfTheReference_WhichEveryReferenceDrawsAgain()
        {
            Site("site.dxf");
            CadDocument document = Host();
            DxfLine roof = null;
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;

            Run(session, "MOVE", new[] { circle }, Enter, P(0, 0), P(0, 50));
            Near(new GeoPoint3(1050, 80, 0), Entity<DxfCircle>(document, circle).Center);
            Run(session, "REFCLOSE");
            Assert.Equal("Enter option [Save/Discard reference changes] <Save>:", session.PromptText);
            Run(session, Enter);

            Assert.Null(document.RefEdit);
            Assert.Contains("1 xref instance updated", session.History);
            Assert.Contains("SITE redefined and reloaded.", session.History);

            // The file holds the circle moved 50 up, about (50, 80), its line and its roof line where they were, and no layer of the session.
            DxfDatabase file = Read("site.dxf");
            DxfCircle moved = Assert.Single(file.ModelSpace.OfType<DxfCircle>());
            Near(new GeoPoint3(50, 80, 0), moved.Center);
            Assert.Equal("WALL", moved.Layer);
            Assert.Equal(2, file.ModelSpace.OfType<DxfLine>().Count());
            roof = file.ModelSpace.OfType<DxfLine>().Single(l => l.Layer == "ROOF");
            Near(new GeoPoint3(0, 200, 0), roof.StartPoint);
            Assert.False(file.LayerTable.Has("$0$WALL"));
            Assert.Equal(1, file.LayerTable["WALL"].ColorIndex);
            Assert.Equal(5, file.LayerTable["ROOF"].ColorIndex);

            // The host holds no copy and none of the layers of the session, and draws the three objects of the file again.
            Assert.Single(All<DxfBlockReference>(document.ModelSpace));
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.Empty(All<DxfCircle>(document.ModelSpace));
            Assert.False(document.Database.LayerTable.Has("$0$WALL"));
            Assert.False(document.Database.LayerTable.Has("$0$ROOF"));
            Assert.Equal(3, Drawn(document));
            Assert.Equal(
                CadReferenceStatus.Loaded,
                document.Xrefs.GetReferences().Single(r => r.Name == "SITE").Status
            );
            AssertReopens(document);

            // The file is written, so undo does not go back to before it.
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ReficloseDiscard_LeavesTheFileOfTheReferenceAsItWas_ByteForByte()
        {
            Site("site.dxf");
            byte[] before = File.ReadAllBytes(PathOf("site.dxf"));
            CadDocument document = Host();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "ERASE", new[] { circle }, Enter, "REFCLOSE", "D");

            Assert.Null(document.RefEdit);
            Assert.DoesNotContain(session.History, line => line.Contains("updated"));
            Assert.Equal(before, File.ReadAllBytes(PathOf("site.dxf")));
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.False(document.Database.LayerTable.Has("$0$WALL"));
            Assert.Equal(3, Drawn(document));

            // Nothing was written, so one undo takes the whole of the session back.
            Assert.Equal("REFEDIT", document.NextUndo.Name);
        }

        [Fact]
        public void AnObjectErasedInTheSession_LeavesTheFileOfTheReference_WhenSaved()
        {
            Site("site.dxf");
            CadDocument document = Host();
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;

            Run(session, "ERASE", new[] { circle }, Enter, "REFCLOSE", Enter);

            DxfDatabase file = Read("site.dxf");
            Assert.Empty(file.ModelSpace.OfType<DxfCircle>());
            Assert.Equal(2, file.ModelSpace.OfType<DxfLine>().Count());
            Assert.Contains("1 object removed from SITE", session.History);
            Assert.Equal(2, Drawn(document));
        }

        [Fact]
        public void ALineDrawnOnALayerOfTheHostAndAddedByRefset_IsSavedIntoTheFile_AndItsLayerIsListed()
        {
            Site("site.dxf");
            CadDocument document = Host(more: d =>
                d.LayerTable.Add(new DxfLayerTableRecord { Name = "FOO", ColorIndex = 3 })
            );
            CadCommandSession session = Opened(document);
            Run(session, "LINE", P(1000, 300), P(1100, 300), Enter);
            DxfLine drawn = All<DxfLine>(document.ModelSpace).Single(l => Math.Abs(l.StartPoint.Y - 300) < 1E-9);

            Assert.True(CadPropertyEditor.Set(document, new[] { drawn.Handle }, "Layer", "FOO").Succeeded);

            Run(session, "REFSET", Enter, new[] { drawn.Handle }, Enter);
            Assert.Equal(4, document.RefEdit.WorkingSet.Count);
            Run(session, "REFCLOSE", Enter);

            DxfDatabase file = Read("site.dxf");
            DxfLine added = file.ModelSpace.OfType<DxfLine>().Single(l => Math.Abs(l.StartPoint.Y - 300) < 1E-9);
            Near(new GeoPoint3(0, 300, 0), added.StartPoint);
            Near(new GeoPoint3(100, 300, 0), added.EndPoint);
            Assert.Equal("FOO", added.Layer);
            Assert.Equal(3, file.LayerTable["FOO"].ColorIndex);
            Assert.Contains("The following symbols will be added to Xref file:", session.History);
            Assert.Contains("Layers: FOO", session.History);
            Assert.Contains("1 object added to SITE", session.History);
        }

        [Fact]
        public void WithUniqueNamesOff_TheLayersKeepTheirOwnNames_AndTheDrawingsOwnLayerIsShared()
        {
            Site("site.dxf");
            CadDocument document = Host(more: d =>
                d.LayerTable.Add(new DxfLayerTableRecord { Name = "WALL", ColorIndex = 4 })
            );
            document.RefEditUniqueNames = false;
            CadCommandSession session = Opened(document);

            Assert.All(All<DxfLine>(document.ModelSpace).Concat<DxfEntity>(All<DxfCircle>(document.ModelSpace)), e =>
                Assert.DoesNotContain("$", e.Layer)
            );
            DxfLine wall = All<DxfLine>(document.ModelSpace).Single(l => Math.Abs(l.StartPoint.Y) < 1E-9);
            Assert.Equal("WALL", wall.Layer);
            Assert.False(document.Database.LayerTable.Has("$0$WALL"));
            Assert.Equal(4, document.Database.LayerTable["WALL"].ColorIndex);

            Run(session, "REFCLOSE", Enter);
            DxfDatabase file = Read("site.dxf");
            Assert.Equal(1, file.LayerTable["WALL"].ColorIndex);
            Assert.Equal(3, file.ModelSpace.Count(e => !e.IsErased));
        }

        [Fact]
        public void AnObjectOnALayerTheReferenceBroughtIntoTheHost_GoesBackToTheLayersOwnName_InTheFile()
        {
            Site("site.dxf");

            // A line of the host on the layer SITE|WALL, as a drawing AutoCAD wrote may hold one: made on a layer of another name and the name written over.
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(1000, 0, 0), database.AttachXref(".\\site.dxf", "SITE"))
            );
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "TMPNAME", ColorIndex = 1 });
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(1000, 300, 0), new GeoPoint3(1100, 300, 0)) { Layer = "TMPNAME" }
            );
            File.WriteAllText(PathOf("host.dxf"), database.ToText().Replace("TMPNAME", "SITE|WALL"));
            CadDocument document = CadDocument.Open(PathOf("host.dxf"));
            Assert.True(document.Database.LayerTable["SITE|WALL"].IsDependent);
            DxfLine own = All<DxfLine>(document.ModelSpace).Single();
            Assert.Equal("SITE|WALL", own.Layer);

            // Added to the working set and saved: the file calls its layer WALL, and never holds the host's SITE|WALL.
            CadCommandSession session = Opened(document);
            Run(session, "REFSET", Enter, new[] { own.Handle }, Enter);
            Assert.Equal(4, document.RefEdit.WorkingSet.Count);
            Run(session, "REFCLOSE", Enter);

            DxfDatabase file = Read("site.dxf");
            DxfLine added = file.ModelSpace.OfType<DxfLine>().Single(l => Math.Abs(l.StartPoint.Y - 300) < 1E-9);
            Assert.Equal("WALL", added.Layer);
            Assert.False(file.LayerTable.Has("SITE|WALL"));
            Assert.Equal(1, file.LayerTable["WALL"].ColorIndex);
        }

        [Fact]
        public void AFileReadOnly_RefusesTheSave_TheSessionStaysOpenAndDiscardWorks()
        {
            string path = Site("site.dxf");
            CadDocument document = Host();
            CadCommandSession session = Opened(document);
            File.SetAttributes(path, FileAttributes.ReadOnly);
            byte[] before = File.ReadAllBytes(path);

            Run(session, "REFCLOSE", Enter);

            Assert.Contains("** Failed to Saveback Xref file **", session.History);
            Assert.NotNull(document.RefEdit);
            Assert.Equal(before, File.ReadAllBytes(path));

            Run(session, "REFCLOSE", "D");
            Assert.Null(document.RefEdit);
            Assert.Equal(3, Drawn(document));
        }

        [Fact]
        public void AFileGoneBeforeTheSave_RefusesIt_AndOneGoneBeforeTheEdit_IsNotEditable()
        {
            string path = Site("site.dxf");
            CadDocument document = Host();
            CadCommandSession session = Opened(document);
            File.Delete(path);

            CadEditResult refused = document.CloseRefEdit(true);

            Assert.True(refused.Refused);
            Assert.Equal("** Failed to Saveback Xref file **", refused.Message);
            Assert.NotNull(document.RefEdit);
            Assert.True(document.CloseRefEdit(false).Succeeded);

            CadDocument without = Host();
            string insert = Insert(without).Handle;
            CadEditResult result = without.OpenRefEdit(new[] { insert }, null);
            Assert.True(result.Refused);
            Assert.Equal("Selected Xref Block is NOT editable: .\\site.dxf", result.Message);
            Assert.Null(without.RefEdit);
            Assert.NotNull(session);
        }

        [Fact]
        public void AFileAnotherAutoCadHolds_IsNotCheckedOut_ForEditing()
        {
            string path = Site("site.dxf");
            File.WriteAllText(Path.ChangeExtension(path, ".dwl"), string.Empty);
            CadDocument document = Host();

            CadEditResult result = document.OpenRefEdit(new[] { Insert(document).Handle }, null);

            Assert.True(result.Refused);
            Assert.Equal(
                "Xref cannot be checked out for editing:\n" + path + "\nA file lock already exists on the drawing.",
                result.Message
            );
            Assert.Null(document.RefEdit);
        }

        [Fact]
        public void AFileChangedOnDisk_MustBeReloaded_ThenTheEditGoesOn()
        {
            string path = Site("site.dxf");
            CadDocument document = Host();
            Assert.Equal(3, Drawn(document));

            // The file is made again with one object more, as another program saved it.
            DxfDatabase again = Read("site.dxf");
            again.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 400, 0), new GeoPoint3(100, 400, 0)));
            again.Save(path);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));

            Assert.True(document.OpenRefEdit(new[] { Insert(document).Handle }, null).Refused);

            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0));
            Assert.Contains("Xref may have changed and must be reloaded: " + path, session.History);
            Assert.Equal("Reload the Xref [Ok/Cancel] <Ok>:", session.PromptText);
            Run(session, Enter, Enter);

            Assert.Contains("4 Added to working set", session.History);
            Assert.Equal(4, document.RefEdit.WorkingSet.Count);
        }

        [Fact]
        public void ADwgReference_IsWrittenBackAsADwg_OfTheVersionItHad()
        {
            string path = Site("site.dwg", "AC1027");
            Assert.Equal("AC1027", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 6));
            CadDocument document = Host("site.dwg");
            CadCommandSession session = Opened(document);
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;

            Run(session, "MOVE", new[] { circle }, Enter, P(0, 0), P(0, 50), "REFCLOSE", Enter);

            Assert.Null(document.RefEdit);
            Assert.Equal("AC1027", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 6));
            DxfDatabase file = Read("site.dwg");
            Near(new GeoPoint3(50, 80, 0), Assert.Single(file.ModelSpace.OfType<DxfCircle>()).Center);
            Assert.Equal(2, file.ModelSpace.OfType<DxfLine>().Count());
            Assert.Equal(3, Drawn(document));
        }

        [Fact]
        public void ANestedExternalReference_IsEditedThroughTheOneThatNamesIt_AndItsOwnFileIsWritten()
        {
            // inner.dxf holds a line from (0, 0) to (40, 0); site.dxf attaches it at (500, 0) and has a line of its own.
            DxfDatabase inner = CadTemplate.CreateMetric();
            inner.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)));
            Save(inner, "inner.dxf");
            DxfDatabase site = CadTemplate.CreateMetric();
            site.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)));
            site.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(500, 0, 0), site.AttachXref(".\\inner.dxf", "INNER"))
            );
            Save(site, "site.dxf");
            byte[] siteBefore = File.ReadAllBytes(PathOf("site.dxf"));
            CadDocument document = Host();

            // A point on the inner line reaches both references: SITE, then SITE > INNER.
            IReadOnlyList<CadRefEditLevel> levels = document.FindRefEditLevels(
                Insert(document).Handle,
                P(1520, 0),
                1.0
            );
            Assert.Equal(new[] { "SITE", "SITE > INNER" }, levels.Select(l => l.Path));
            Assert.Equal(PathOf("inner.dxf"), levels[1].FileName, ignoreCase: true);

            Assert.True(document.OpenRefEdit(levels[1].Chain, null).Succeeded);
            DxfLine copy = Assert.Single(All<DxfLine>(document.ModelSpace));
            Near(new GeoPoint3(1500, 0, 0), copy.StartPoint);
            Near(new GeoPoint3(1540, 0, 0), copy.EndPoint);
            Assert.Equal("INNER", document.RefEdit.Name);

            Run(Start(document), "MOVE", new[] { copy.Handle }, Enter, P(0, 0), P(0, 70));
            Assert.True(document.CloseRefEdit(true).Succeeded);

            DxfLine written = Assert.Single(Read("inner.dxf").ModelSpace.OfType<DxfLine>());
            Near(new GeoPoint3(0, 70, 0), written.StartPoint);
            Near(new GeoPoint3(40, 70, 0), written.EndPoint);
            Assert.Equal(siteBefore, File.ReadAllBytes(PathOf("site.dxf")));
        }

        [Fact]
        public void TheEditOfANestedReference_IsDiscarded_EvenWhereTheFileThatNamesItIsGone()
        {
            DxfDatabase inner = CadTemplate.CreateMetric();
            inner.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)));
            Save(inner, "inner.dxf");
            DxfDatabase site = CadTemplate.CreateMetric();
            site.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(500, 0, 0), site.AttachXref(".\\inner.dxf", "INNER"))
            );
            Save(site, "site.dxf");
            CadDocument document = Host();
            CadRefEditLevel nested = document.FindRefEditLevels(Insert(document).Handle, P(1520, 0), 1.0)[1];
            Assert.True(document.OpenRefEdit(nested.Chain, null).Succeeded);
            File.Delete(PathOf("site.dxf"));

            Assert.True(document.CloseRefEdit(false).Succeeded);

            Assert.Null(document.RefEdit);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.False(document.Database.LayerTable.Has("$0$0"));
        }

        [Fact]
        public void AnEditSavedInTheMiddle_OpensAgainWithTheEditOpen_OfTheSameFile()
        {
            Site("site.dxf");
            CadDocument document = Host();
            Opened(document);
            string copy = All<DxfLine>(document.ModelSpace)[0].Handle;
            Assert.NotNull(copy);
            string path = PathOf("host-open.dxf");
            document.Database.Save(path);

            CadDocument reopened = CadDocument.Open(path);

            Assert.NotNull(reopened.RefEdit);
            Assert.Equal("SITE", reopened.RefEdit.Name);
            Assert.Equal(PathOf("site.dxf"), reopened.RefEdit.XrefFile, ignoreCase: true);
            Assert.Equal(0, Drawn(reopened));
            Assert.True(reopened.CloseRefEdit(false).Succeeded);
            Assert.Equal(3, Drawn(reopened));
        }

        [Fact]
        public void TheFormatNotice_IsSaidWhenTheFileIsOfAnOlderRelease_AndTheFileKeepsItsRelease()
        {
            DxfDatabase old = new DxfDatabase("AC1015");
            old.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            string path = PathOf("site.dxf");
            old.Save(path);
            CadDocument document = Host();

            CadCommandSession session = Opened(document);

            Assert.Contains("The Xref selected references a previous drawing file format.", session.History);
            Run(session, "REFCLOSE", Enter);
            Assert.Equal("AC1015", Read("site.dxf").Version);
        }

        [Fact]
        public void TheReferenceEditDialog_OfTheWindow_ChoosesTheReferenceTheObjectsAndTheSettings()
        {
            DxfDatabase inner = CadTemplate.CreateMetric();
            inner.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0)));
            Save(inner, "inner.dxf");
            DxfDatabase site = CadTemplate.CreateMetric();
            site.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)));
            site.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(500, 0, 0), site.AttachXref(".\\inner.dxf", "INNER"))
            );
            Save(site, "site.dxf");
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            CadRefEditRequest seen = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "REFEDIT" && e.Payload is CadRefEditRequest request)
                {
                    seen = request;

                    // The window: the nested reference chosen, the objects asked for, unique names off, the lock off.
                    request.SelectedIndex = 1;
                    request.PromptForObjects = true;
                    request.UniqueNames = false;
                    request.LockObjects = false;
                    request.IsAccepted = true;
                    e.Handled = true;
                }
            };

            Run(session, "REFEDIT", P(1520, 0));

            // The references the point reaches are listed, with the file of each; the choices answer the questions on the command line.
            Assert.Equal(new[] { "SITE", "SITE > INNER" }, seen.Levels.Select(l => l.Path));
            Assert.All(seen.Levels, l => Assert.True(l.IsExternalReference));
            Assert.Equal("Select nested objects or [All]:", session.PromptText);
            Assert.False(document.RefEditUniqueNames);
            Assert.False(document.RefEditLocksObjects);

            Run(session, P(1520, 0), Enter);

            Assert.Equal("INNER", document.RefEdit.Name);
            Assert.Equal(PathOf("inner.dxf"), document.RefEdit.XrefFile, ignoreCase: true);
            Assert.False(document.RefEdit.LocksObjects);
            Assert.Single(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void TheReferenceEditDialog_Cancelled_OpensNoEdit()
        {
            Site("site.dxf");
            CadDocument document = Host();
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) => e.Handled = e.Name == "REFEDIT";

            Run(session, "REFEDIT", P(1050, 0));

            Assert.Null(document.RefEdit);
            Assert.False(session.IsRunning);
            Assert.True(document.RefEditUniqueNames);
        }

        [Fact]
        public void ABlockReference_IsEditedAsBefore_NoFileIsTouched()
        {
            Site("site.dxf");
            DxfDatabase database = CadTemplate.CreateMetric();
            var box = new DxfBlockTableRecord { Name = "BOX", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(box);
            box.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 0, 0), box));
            database.AttachXref(".\\site.dxf", "SITE");
            CadDocument document = CadDocument.From(database, PathOf("blocks.dxf"));

            CadCommandSession session = Start(document, "REFEDIT", P(1050, 0), Enter);

            Assert.Contains("1 Added to working set", session.History);
            Assert.Null(document.RefEdit.XrefFile);
            Assert.Equal("BOX", document.RefEdit.Name);
            Assert.Single(document.RefEdit.WorkingSet);
            Run(session, "REFCLOSE", Enter);
            Assert.Contains("1 block instance updated", session.History);
        }
    }
}
