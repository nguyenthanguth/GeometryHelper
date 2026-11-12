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
    /// What -XREF Unload, Reload and Detach say where they find nothing to act on, as AutoCAD 2021 says it (probes xrefs3-1
    /// and xrefs3-2): "No matching xref names found.", "No matching and loaded xref names found." and, for a nested
    /// reference, "Xref column has multiple references. Not detached."; and a name with a wildcard character taken whole
    /// under the back quote.
    /// </summary>
    public class XrefRefusalTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "xrefrefusal-" + Guid.NewGuid().ToString("N")
        );

        public XrefRefusalTests()
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

        /// <summary>A metric drawing of a line on layer GRID from (0, 0) to (1 000, 0), saved in the folder xref.</summary>
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

        /// <summary>A metric drawing saved in the folder plans as host.dxf and opened, each file given attached at (0, 0) under its name.</summary>
        private CadDocument Host(out CadCommandSession session, params string[] attached)
        {
            string path = Path.Combine(_folder, "plans", "host.dxf");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            CadTemplate.CreateMetric().Save(path);
            CadDocument document = CadDocument.Open(path);
            session = Session(document);

            foreach (string file in attached)
            {
                Run(session, "-XREF", "A", file, P(0, 0), Enter, Enter, Enter);
            }

            return document;
        }

        [Theory]
        [InlineData("U")]
        [InlineData("R")]
        [InlineData("D")]
        public void AName_MatchingNothing_IsAnsweredNoMatchingXrefNamesFound(string option)
        {
            CadDocument document = Host(out CadCommandSession session, Grid("grid"));
            string attached = document.Database.ToText();
            int said = session.History.Count;

            Run(session, "-XREF", option, "nothing");

            Assert.Equal("No matching xref names found.", session.History.Skip(said).Last());
            Assert.Equal(attached, document.Database.ToText());
        }

        [Fact]
        public void UnloadOfReferencesAllUnloaded_IsAnsweredNoMatchingAndLoadedXrefNamesFound()
        {
            CadDocument document = Host(out CadCommandSession session, Grid("grid"), Grid("axes"));
            Run(session, "-XREF", "U", "grid,axes");
            string unloaded = document.Database.ToText();
            int said = session.History.Count;

            Run(session, "-XREF", "U", "grid,axes");

            Assert.Equal("No matching and loaded xref names found.", session.History.Skip(said).Last());
            Assert.DoesNotContain(session.History.Skip(said), l =>
                l.EndsWith(" is unloaded.", StringComparison.Ordinal)
            );
            Assert.Equal(unloaded, document.Database.ToText());
        }

        [Fact]
        public void UnloadOfALoadedReferenceAndAnUnloadedOne_UnloadsTheLoadedOneAlone()
        {
            CadDocument document = Host(out CadCommandSession session, Grid("grid"), Grid("axes"));
            Run(session, "-XREF", "U", "axes");
            int said = session.History.Count;

            Run(session, "-XREF", "U", "grid,axes");

            string[] said2 = session
                .History.Skip(said)
                .Where(l =>
                    l.StartsWith("Xref \"", StringComparison.Ordinal)
                    || l.EndsWith(" is unloaded.", StringComparison.Ordinal)
                )
                .ToArray();
            Assert.Equal(new[] { "Xref \"grid\": ..\\xref\\grid.dxf", "\"grid\" is unloaded." }, said2);
            Assert.True(document.Xrefs.IsUnloaded("grid"));
        }

        [Fact]
        public void DetachOfANestedReference_IsRefusedAsAutoCadRefusesIt_NamingItsParent()
        {
            Grid("column");
            string plan = Grid("plan", d =>
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref("column.dxf", "column"))
                )
            );
            CadDocument document = Host(out CadCommandSession session, plan, Grid("grid"));
            string attached = document.Database.ToText();
            int said = session.History.Count;

            Run(session, "-XREF", "D", "column");

            string[] lines = session.History.Skip(said).ToArray();
            int at = Array.IndexOf(lines, "Xref column has multiple references. Not detached.");
            Assert.True(at >= 0);
            Assert.Equal("\"plan\"", lines[at + 1]);
            Assert.Equal(attached, document.Database.ToText());

            Run(session, "-XREF", "D", "grid,column");

            Assert.False(document.Database.BlockTable.Has("grid"));
            Assert.True(document.Database.BlockTable.Has("plan"));
            Assert.Equal(2, session.History.Count(l => l == "Xref column has multiple references. Not detached."));
        }

        [Fact]
        public void ANameWithWildcardCharacters_IsTakenWholeUnderTheBackQuote_AndAsAPatternWithout()
        {
            string grid = Grid("grid");
            CadDocument document = Host(out CadCommandSession session);

            foreach (string name in new[] { "a#1", "a@b", "a.b", "~t", "[x]", "a-b", "ab" })
            {
                Run(session, "-XREF", "A", name + "=" + grid, P(0, 0), Enter, Enter, Enter);
            }

            Run(session, "-XREF", "U", "a#1");
            Assert.DoesNotContain(document.Xrefs.GetReferences(), r => r.Status == CadReferenceStatus.Unloaded);

            Run(session, "-XREF", "U", "a`#1,`~t,`[x`]");
            Assert.Equal(
                new[] { "[x]", "a#1", "~t" },
                document
                    .Xrefs.GetReferences()
                    .Where(r => r.Status == CadReferenceStatus.Unloaded)
                    .Select(r => r.Name)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToArray()
            );

            Run(session, "-XREF", "U", "a.b");
            Assert.True(document.Xrefs.IsUnloaded("a.b"));
            Assert.True(document.Xrefs.IsUnloaded("a-b"));
            Assert.False(document.Xrefs.IsUnloaded("ab"));
        }
    }
}
