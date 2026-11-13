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
    /// -XREF Unload and Reload of nested references in the words AutoCAD 2021's console used (probed on a host attaching b that
    /// places c): unloading b says c orphaned, c alone can be unloaded and reloaded, a reload under an unloaded parent says
    /// "orphaned", and a reload says "has not changed" where the file did not and "has been restored" where it was unloaded.
    /// </summary>
    public class XrefNestedUnloadTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xrefnest-" + Guid.NewGuid().ToString("N")
        );

        public XrefNestedUnloadTests()
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

        private string Grid(string name, Action<DxfDatabase> more = null)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID" });
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" }
            );
            more?.Invoke(database);
            string path = Path.Combine(_folder, "xref", name + ".dxf");
            database.Save(path);
            return path;
        }

        /// <summary>A host that attaches plan, which places column, with the record AutoCAD keeps of column in the host.</summary>
        private CadDocument Nested(out CadCommandSession session, out string plan, out string column)
        {
            column = Grid("column");
            plan = Grid("plan", d =>
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref("column.dxf", "column"))
                )
            );
            DxfDatabase database = CadTemplate.CreateMetric();
            string path = Path.Combine(_folder, "plans", "host.dxf");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            database.Save(path);
            CadDocument document = CadDocument.Open(path);
            session = Session(document);
            Run(session, "-XREF", "A", plan, P(0, 0), Enter, Enter, Enter);
            document.Database.AttachXref("..\\xref\\column.dxf", "column");
            document.Regen();
            return document;
        }

        private static string[] Said(CadCommandSession session, int count) =>
            session.History.Skip(session.History.Count - count).ToArray();

        private static string[] States(CadDocument document) =>
            document.Xrefs.GetReferences().Select(r => r.ToString()).ToArray();

        private static int GridLines(CadDocument document) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.OfType<DisplayPolyline2>()
                .Count(p => p.Layer.EndsWith("GRID", StringComparison.Ordinal));

        [Fact]
        public void UnloadOfAParent_SaysItsNestedReferenceOrphaned_ThatOneIsStillLoadedForUnload()
        {
            CadDocument document = Nested(out CadCommandSession session, out _, out _);

            Run(session, "-XREF", "U", "plan");

            Assert.Equal(
                new[]
                {
                    "Xref \"plan\": ..\\xref\\plan.dxf",
                    "\"plan\" is unloaded.",
                    "Xref \"column\": ..\\xref\\column.dxf",
                    "\"column\" is orphaned.",
                },
                Said(session, 4)
            );
            Assert.Equal(new[] { "plan Unloaded", "column Orphaned" }, States(document));

            // Orphaned counts as loaded: unloading it says so.
            Run(session, "-XREF", "U", "column");
            Assert.Equal(
                new[] { "Xref \"column\": ..\\xref\\column.dxf", "\"column\" is unloaded." },
                Said(session, 2)
            );
        }

        [Fact]
        public void UnloadOfANestedName_UnloadsItAlone_AndAgainFindsNothingLoaded_ReloadRestoresIt()
        {
            CadDocument document = Nested(out CadCommandSession session, out _, out string column);
            Assert.Equal(2, GridLines(document));

            Run(session, "-XREF", "U", "column");
            Assert.Equal(
                new[] { "Xref \"column\": ..\\xref\\column.dxf", "\"column\" is unloaded." },
                Said(session, 2)
            );
            Assert.Equal(new[] { "plan Loaded", "column Unloaded" }, States(document));
            Assert.Equal(1, GridLines(document));

            Run(session, "-XREF", "U", "column");
            Assert.Equal("No matching and loaded xref names found.", session.History.Last());

            Run(session, "-XREF", "R", "column");
            Assert.Equal(
                new[]
                {
                    "Reload Xref \"column\": ..\\xref\\column.dxf",
                    Path.GetFullPath(column) + " has not changed.",
                    "\"column\" has been restored.",
                },
                Said(session, 3)
            );
            Assert.Equal(new[] { "plan Loaded", "column Loaded" }, States(document));
            Assert.Equal(2, GridLines(document));
        }

        [Fact]
        public void ReloadOfAReferenceUnderAnUnloadedParent_SaysOrphaned_AndChangesNothing_ReloadOfTheParentSaysTheOneUnloaded()
        {
            CadDocument document = Nested(out CadCommandSession session, out string plan, out _);
            Run(session, "-XREF", "U", "plan", "-XREF", "U", "column");

            Run(session, "-XREF", "R", "column");
            Assert.Equal(
                new[] { "Xref \"column\": ..\\xref\\column.dxf", "\"column\" is orphaned." },
                Said(session, 2)
            );
            Assert.DoesNotContain(session.History, l =>
                l.StartsWith("Reload Xref \"column\"", StringComparison.Ordinal)
            );

            Run(session, "-XREF", "R", "plan");
            Assert.Equal(
                new[]
                {
                    "Reload Xref \"plan\": ..\\xref\\plan.dxf",
                    Path.GetFullPath(plan) + " has not changed.",
                    "\"plan\" has been restored.",
                    "Xref \"column\": ..\\xref\\column.dxf",
                    "\"column\" is unloaded.",
                },
                Said(session, 5)
            );
            Assert.Equal(new[] { "plan Loaded", "column Unloaded" }, States(document));
        }

        [Fact]
        public void PathAndPathTypeOfNoReferenceNamed_SayNoMatchingXrefNames_AndAskNothingFurther()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            foreach (string option in new[] { "P", "T" })
            {
                Run(session, "-XREF", option, "nope");

                Assert.Equal("No matching xref names found.", session.History.Last());
                Assert.False(session.IsRunning);
            }

            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AReferenceNoInsertPlaces_CanBeUnloaded_ReloadSaysItUnreferenced_AndBindRefusesItAsNested()
        {
            CadDocument document = Nested(out CadCommandSession session, out _, out _);
            string insert = Assert.Single(All<DxfBlockReference>(document.ModelSpace)).Handle;
            Run(session, "ERASE", new[] { insert }, Enter);
            Assert.Equal(CadReferenceStatus.Unreferenced, document.Xrefs.GetReferences().First().Status);

            Run(session, "-XREF", "U", "plan");
            Assert.Equal(new[] { "Xref \"plan\": ..\\xref\\plan.dxf", "\"plan\" is unloaded." }, Said(session, 2));
            Assert.True(document.Xrefs.IsUnloaded("plan"));

            // AutoCAD 2021's console: no "Reload Xref" line, the reference said unreferenced, and no longer unloaded.
            Run(session, "-XREF", "R", "plan");
            Assert.Equal(new[] { "Xref \"plan\": ..\\xref\\plan.dxf", "\"plan\" is unreferenced." }, Said(session, 2));
            Assert.False(document.Xrefs.IsUnloaded("plan"));

            string text = document.Database.ToText();
            Run(session, "-XREF", "B", "plan");
            Assert.Equal("Xref \"plan\" is nested and cannot be bound.", session.History.Last());
            Assert.Equal(text, document.Database.ToText());
        }

        [Fact]
        public void UnloadOfEveryName_UnloadsTheParentThenTheNestedOneEachOnItsOwn_NoOrphanedLine()
        {
            CadDocument document = Nested(out CadCommandSession session, out _, out _);

            Run(session, "-XREF", "U", "*");

            Assert.Equal(
                new[]
                {
                    "Xref \"plan\": ..\\xref\\plan.dxf",
                    "\"plan\" is unloaded.",
                    "Xref \"column\": ..\\xref\\column.dxf",
                    "\"column\" is unloaded.",
                },
                Said(session, 4)
            );
            Assert.Equal(new[] { "plan Unloaded", "column Orphaned" }, States(document));
        }

        [Fact]
        public void ReloadOfEveryName_RestoresTheParent_AndTheNestedOneThatWasOrphanedIsSaidRestoredToo()
        {
            Nested(out CadCommandSession session, out _, out string column);
            Run(session, "-XREF", "U", "plan");

            Run(session, "-XREF", "R", "*");

            Assert.Equal(
                new[]
                {
                    "Reload Xref \"column\": ..\\xref\\column.dxf",
                    Path.GetFullPath(column) + " has not changed.",
                    "\"column\" has been restored.",
                },
                Said(session, 6).Skip(3).ToArray()
            );
        }

        [Fact]
        public void ReloadOfAReferenceLoaded_SaysHasNotChanged_AndOfOneWhoseFileChanged_SaysLoadedWithItsPath()
        {
            CadDocument document = Nested(out CadCommandSession session, out string plan, out _);

            Run(session, "-XREF", "R", "plan");
            Assert.Equal(
                new[] { "Reload Xref \"plan\": ..\\xref\\plan.dxf", Path.GetFullPath(plan) + " has not changed." },
                Said(session, 2)
            );

            Grid("plan", d =>
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref("column.dxf", "column"))
                )
            );
            File.SetLastWriteTimeUtc(plan, DateTime.UtcNow.AddMinutes(1));
            Assert.Equal(CadReferenceStatus.NeedsReloading, document.Xrefs.GetReferences().First().Status);

            Run(session, "-XREF", "R", "plan");
            Assert.Equal(
                new[] { "Reload Xref \"plan\": ..\\xref\\plan.dxf", "\"plan\" loaded: " + Path.GetFullPath(plan) },
                Said(session, 2)
            );
        }
    }
}
