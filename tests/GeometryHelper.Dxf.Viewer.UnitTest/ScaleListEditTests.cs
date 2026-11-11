using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// -SCALELISTEDIT on the drawing's list of scales, its prompts and answers word for word as AutoCAD 2021's console gave
    /// them: "Enter option [?/Add/Delete/Reset/Exit] &lt;Add&gt;:", "GH already exists.  Redefine scale? [Yes/No] &lt;No&gt;:",
    /// "Scale 1:1 is referenced so it cannot be deleted.", "GH scale deleted.", "Scale list reset to default entries."
    /// </summary>
    public class ScaleListEditTests
    {
        private static string[] Names(CadDocument document) =>
            document
                .Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")
                .Select(s => s.Name)
                .ToArray();

        private static DxfAnnotationScale Scale(CadDocument document, string name) =>
            (DxfAnnotationScale)
                document.Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext(name);

        [Fact]
        public void AddRedefineAndList_AsAutoCADsConsoleDoes()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "-SCALELISTEDIT", "R", "Y", "A", "GH", "1=3");
            Assert.Equal(
                "Invalid entry. Expects a ratio entered in the following format: \"1:1\"",
                session.History.Last()
            );
            Run(session, "1:3", "A", "gh", "Y", "2:3", "?");

            DxfAnnotationScale gh = Scale(document, "GH");
            Assert.Equal(2.0, gh.PaperUnits);
            Assert.Equal(3.0, gh.DrawingUnits);
            Assert.Contains("gh already exists.  Redefine scale? [Yes/No] <No>: Y", session.History);
            Assert.Contains("    Scale Name             Paper Units  Drawing Units  Effective Scale", session.History);
            Assert.Contains("    ----------             -----------  -------------  ---------------", session.History);
            Assert.Contains(" 1: 1:1                         1.0000        1.0000        1.0000", session.History);
            int at = Names(document).ToList().IndexOf("GH") + 1;
            Assert.Contains(
                at.ToString().PadLeft(2) + ": GH                          2.0000        3.0000        0.6667",
                session.History
            );

            // A no to the redefinition keeps the scale; Exit ends.
            Run(session, "A", "GH", "N", "E");
            Assert.False(session.IsRunning);
            Assert.Equal(2.0, Scale(document, "GH").PaperUnits);
        }

        [Fact]
        public void Delete_TakesOneOrEveryUnused_AndKeepsTheReferenced_EachChangeOneUndoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "-SCALELISTEDIT", "R", "Y", "A", "GH", "1:3", "A", "N1", "2.5:1");
            Assert.Contains("GH", Names(document));
            Assert.Contains("N1", Names(document));

            Run(session, "D", "NOSUCH");
            Assert.Equal("Scale NOSUCH not found.", session.History.Last());
            Run(session, "D", "1:1");
            Assert.Equal("Scale 1:1 is referenced so it cannot be deleted.", session.History.Last());
            Run(session, "D", "gh");
            Assert.Equal("GH scale deleted.", session.History.Last());
            Assert.DoesNotContain("GH", Names(document));

            Run(session, "D", "*", "E");
            Assert.Equal(new[] { "1:1" }, Names(document).Where(n => n == "1:1" || n == "N1"));
            Assert.DoesNotContain("1:50", Names(document));

            // The whole command is one undo step, as a command is: the list as it stood before it, none.
            Assert.Equal("-SCALELISTEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Empty(Names(document));
        }

        [Fact]
        public void Reset_AsksFirst_ThenPutsBackTheDefaultScales_KeepingThoseInUse()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "-SCALELISTEDIT", "D", "*", "A", "GH", "1:3", "R", Enter);
            Assert.Equal("Scale list not reset.", session.History.Last());
            Assert.Contains("GH", Names(document));

            Run(session, "R", "Y", "E");
            Assert.Contains("Scale list reset to default entries.", session.History);
            Assert.DoesNotContain("GH", Names(document));
            Assert.Equal(CadViewports.StandardScales.OrderBy(n => n), Names(document).OrderBy(n => n));
        }
    }
}
