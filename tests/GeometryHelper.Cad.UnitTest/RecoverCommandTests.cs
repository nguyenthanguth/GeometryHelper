using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>RECOVER opens a damaged file as a new document of what could be read; AUDIT checks the drawing shown and fixes it as one undo step.</summary>
    public class RecoverCommandTests
    {
        /// <summary>The text of a drawing of two lines 100 long, the second on the layer WALLS.</summary>
        private static string TwoLines()
        {
            var db = new DxfDatabase("AC1027");
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)) { Layer = "WALLS" }
            );
            return db.ToText();
        }

        [Fact]
        public void RecoverOpensWhatCouldBeReadOfAFileCutShort_AsANewDocumentOfNoFile_AndTellsTheReport()
        {
            string text = TwoLines();
            string path = Path.Combine(Path.GetTempPath(), "recover-" + Guid.NewGuid().ToString("N") + ".dxf");
            File.WriteAllText(
                path,
                text.Substring(0, text.IndexOf("  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS", StringComparison.Ordinal)),
                new UTF8Encoding(false)
            );

            try
            {
                CadDocument shown = null;
                CadCommandSession session = Session(Blank());
                session.UiRequested += (sender, e) =>
                {
                    shown = e.Name == "RECOVER" ? e.Document : shown;
                    e.Handled = e.Name == "RECOVER";
                };

                Run(session, "RECOVER");
                Assert.Equal("Enter file name to recover:", session.PromptText);
                Run(session, path);

                Assert.False(session.IsRunning);
                Assert.NotNull(shown);
                Assert.Equal(Path.GetFileName(path) + " (recovered)", shown.Title);
                Assert.False(shown.HasFile);
                Assert.Equal(2, shown.EntityCount);
                Assert.Contains("2 errors found, 1 fixed.", session.History);
                Assert.Contains(session.History, l => l.StartsWith("The file ends on line ", StringComparison.Ordinal));
                Assert.Contains(
                    "The file has no OBJECTS section, though its records are of R13 or later: what it held is lost.",
                    session.History
                );
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void RecoverOfAFileThatIsNotThere_SaysSo()
        {
            CadCommandSession session = Session(Blank());

            Run(session, "RECOVER", @"Z:\no\such\plan.dxf");

            Assert.StartsWith("Cannot recover \"Z:\\no\\such\\plan.dxf\": ", session.History.Last());
        }

        [Fact]
        public void AuditNoTellsWhatIsWrong_AndAuditYesFixesItAsOneUndoStep()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(TwoLines().Replace("  2\nWALLS\n", "  2\nWALLZ\n"));
            CadDocument document = CadDocument.Open(new MemoryStream(bytes), "walls.dxf");
            CadCommandSession session = Session(document);

            Run(session, "AUDIT");
            Assert.Equal("Fix any errors detected? [Yes/No] <N>:", session.PromptText);
            Run(session, Enter);

            Assert.Contains("Total errors found 1 fixed 0", session.History);
            Assert.False(document.Database.LayerTable.Has("WALLS"));
            Assert.False(document.CanUndo);

            Run(session, "AUDIT", "Yes");

            Assert.Contains("Total errors found 1 fixed 1", session.History);
            Assert.Contains(session.History, l =>
                l.StartsWith("The layer WALLS, named by 1 entity (the first: the LINE ", StringComparison.Ordinal)
                && l.EndsWith("; it was added.", StringComparison.Ordinal)
            );
            // The error lines come before the total, as AutoCAD 2021 writes its report.
            Assert.True(
                session.History.ToList().FindLastIndex(l => l.EndsWith("; it was added.", StringComparison.Ordinal))
                    < session.History.ToList().FindLastIndex(l => l == "Total errors found 1 fixed 1")
            );
            Assert.True(document.Database.LayerTable.Has("WALLS"));
            Assert.Equal("AUDIT", document.NextUndo.Name);

            Run(session, "AUDIT", "Yes");
            Assert.Equal("Erased 0 objects", session.History.Last());
            Assert.Equal("Total errors found 0 fixed 0", session.History[session.History.Count - 2]);
            Assert.DoesNotContain("No errors found.", session.History);
            Assert.Equal("AUDIT", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.False(document.Database.LayerTable.Has("WALLS"));
            Assert.False(document.CanUndo);
        }
    }
}
