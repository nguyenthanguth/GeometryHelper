using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// SPELL (SP): the words of the TEXT, MTEXT and attributes checked against a small built-in English list and a custom
    /// dictionary, each word not in them offered with the words nearest it, Ignore, ignore all, Add, Change (to the first
    /// suggestion), a replacement typed, or Stop; the command line form of AutoCAD's Check Spelling dialog, which the
    /// presenter has no design for yet. Words in capitals, with digits or of one letter are passed over.
    /// </summary>
    public class SpellCommandTests
    {
        private static readonly string Folder = TestRun.Folder("spell");

        /// <summary>A text T1 "Helo wrold", a text T2 "The beam is fixed to the plate" and an MTEXT C1 "Pleese check \fArial;thiss text".</summary>
        private static CadDocument Texts() =>
            CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(0, 0, 5, "Helo wrold", handle: "T1"),
                    TestDrawings.Text(0, 20, 5, "The beam is fixed to the plate", handle: "T2"),
                    TestDrawings.Entity(
                        "MTEXT",
                        "0",
                        256,
                        "C1",
                        "10",
                        0.0,
                        "20",
                        60.0,
                        "30",
                        0.0,
                        "40",
                        5.0,
                        "41",
                        100.0,
                        "71",
                        1,
                        "1",
                        "Pleese check \\fArial;thiss text"
                    )
                ),
                "texts.dxf"
            );

        [Fact]
        public void TheCommandAsksWhatToCheck_AndEnterChecksEveryObject()
        {
            CadDocument document = Texts();
            CadCommandSession session = Run(Session(document), "SPELL");
            Assert.Equal("Select objects or [All/Dictionary]:", session.PromptText);

            Run(session, Enter);

            Assert.Contains(
                "Not in dictionary: \"Helo\". Suggestions: Hello",
                session.History.First(h => h.StartsWith("Not in dictionary"))
            );
            Assert.Equal("Enter replacement or [Ignore/ignore aLl/Add/Change/Stop] <Ignore>:", session.PromptText);
        }

        [Fact]
        public void TheAliasSp_StartsIt()
        {
            Assert.Equal("Select objects or [All/Dictionary]:", Run(Session(Texts()), "SP").PromptText);
        }

        [Fact]
        public void AReplacementTypedAndChange_WriteTheWordsIn_InOneUndoStep()
        {
            CadDocument document = Texts();
            CadCommandSession session = Run(Session(document), "SP", "A");

            Run(session, "Change");

            Assert.Contains(
                "Not in dictionary: \"wrold\". Suggestions: world",
                session.History.Last(h => h.StartsWith("Not in dictionary"))
            );
            Run(session, "world");
            Assert.Equal("Hello world", Entity<DxfDBText>(document, "T1").TextString);

            // The MTEXT's words follow, its codes left out of them and kept in it.
            Assert.Contains(
                "Not in dictionary: \"Pleese\"",
                session.History.Last(h => h.StartsWith("Not in dictionary"))
            );
            Run(session, "Please", "this");
            Assert.Equal("Please check \\fArial;this text", Entity<DxfMText>(document, "C1").Contents);
            Assert.False(session.IsRunning);
            Assert.Contains("Spelling check complete.", session.History);
            Assert.Equal("SPELL", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal("Helo wrold", Entity<DxfDBText>(document, "T1").TextString);
            Assert.Equal("Pleese check \\fArial;thiss text", Entity<DxfMText>(document, "C1").Contents);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void Ignore_LeavesTheWord_IgnoreAllLeavesEveryLikeIt_AndEnterIsIgnore()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(0, 0, 5, "wrold wrold", handle: "T1"),
                    TestDrawings.Text(0, 20, 5, "wrold again", handle: "T2")
                ),
                "ignore.dxf"
            );
            CadCommandSession session = Run(Session(document), "SPELL", "All");

            Run(session, "Ignore");
            Assert.Equal("Enter replacement or [Ignore/ignore aLl/Add/Change/Stop] <Ignore>:", session.PromptText);
            Run(session, "L");

            Assert.False(session.IsRunning);
            Assert.Equal(2, session.History.Count(h => h.StartsWith("Not in dictionary")));
            Assert.Equal("wrold wrold", Entity<DxfDBText>(document, "T1").TextString);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void Add_TakesTheWordIntoTheDictionaryForTheRest_AndIntoItsFileWhereThereIsOne()
        {
            string path = Path.Combine(Folder, "added.txt");
            File.WriteAllText(path, "# words of the office\ncolumnar\n");
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(0, 0, 5, "Helo Helo", handle: "T1"),
                    TestDrawings.Text(0, 20, 5, "Helo wrold", handle: "T2")
                ),
                "add.dxf"
            );
            CadCommandSession session = Run(Session(document), "SP", "D");
            Assert.Equal("Enter custom dictionary file <none>:", session.PromptText);
            Run(session, path);
            Assert.Equal("Select objects or [All/Dictionary]:", session.PromptText);

            Run(session, Enter, "Add");

            // The other Helos are known now: the next word asked is wrold of the second text.
            Assert.Contains(
                "Not in dictionary: \"wrold\"",
                session.History.Last(h => h.StartsWith("Not in dictionary"))
            );
            Assert.Equal(1, session.History.Count(h => h.StartsWith("Not in dictionary: \"Helo\"")));
            Run(session, "Stop");
            Assert.False(session.IsRunning);
            string[] kept = File.ReadAllLines(path);
            Assert.Equal("columnar", kept[1]);
            Assert.Contains("Helo", kept);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ACustomDictionary_MakesItsWordsKnown_AndAnUnreadableOneIsRefused()
        {
            string path = Path.Combine(Folder, "office.txt");
            File.WriteAllText(path, "helo\nwrold\npleese\nthiss\n");
            CadDocument document = Texts();

            CadCommandSession session = Run(Session(document), "SPELL", "D", path, Enter);

            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History, h => h.StartsWith("Not in dictionary"));
            Assert.Contains("Spelling check complete.", session.History);

            CadCommandSession refused = Run(Session(document), "SPELL", "D", Path.Combine(Folder, "none.txt"));
            Assert.Contains("Cannot read custom dictionary.", refused.History);
            Assert.Equal("Select objects or [All/Dictionary]:", refused.PromptText);
        }

        [Fact]
        public void OnlyTheObjectsSelected_AreChecked()
        {
            CadDocument document = Texts();

            CadCommandSession session = Run(Session(document), "SPELL", new[] { "T2" }, Enter);

            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History, h => h.StartsWith("Not in dictionary"));
            Assert.Contains("Spelling check complete.", session.History);
        }

        [Fact]
        public void WordsInCapitalsWithDigitsOrOfOneLetter_AreNotChecked_AndEndingsAreAllowed()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Text(0, 0, 5, "NOTE: B-12 a x2 beams bolted plates fitted", handle: "T1")
                ),
                "caps.dxf"
            );

            CadCommandSession session = Run(Session(document), "SPELL", Enter);

            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History, h => h.StartsWith("Not in dictionary"));
        }

        [Fact]
        public void TheValueOfAnAttribute_IsCheckedAndChangedToo()
        {
            string block = TestDrawings.Block(
                "TB",
                TestDrawings.Line(0, 0, 100, 0),
                TestDrawings.Entity(
                    "ATTDEF",
                    "0",
                    256,
                    null,
                    "10",
                    5.0,
                    "20",
                    18.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "TITLE?",
                    "3",
                    "Title",
                    "2",
                    "TITLE",
                    "70",
                    0
                )
            );
            string insert =
                "0\nINSERT\n5\nB1\n8\n0\n66\n1\n2\nTB\n10\n20\n20\n20\n30\n0\n41\n1\n42\n1\n43\n1\n"
                + TestDrawings.Entity(
                    "ATTRIB",
                    "0",
                    1,
                    "B2",
                    "10",
                    25.0,
                    "20",
                    38.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "Plate thikness",
                    "2",
                    "TITLE",
                    "70",
                    0
                )
                + "0\nSEQEND\n5\nB5\n8\n0\n";
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, block, insert), "attrib.dxf");

            CadCommandSession session = Run(Session(document), "SPELL", Enter);
            Assert.Contains(
                "Not in dictionary: \"thikness\"",
                session.History.Last(h => h.StartsWith("Not in dictionary"))
            );
            Run(session, "thickness");

            Assert.Equal(
                "Plate thickness",
                Entity<DxfBlockReference>(document, "B1").AttributeCollection.Single().TextString
            );
            Assert.Equal("SPELL", document.NextUndo.Name);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void StopEndsTheCheck_KeepingWhatWasChanged()
        {
            CadDocument document = Texts();
            CadCommandSession session = Run(Session(document), "SPELL", Enter, "Hello", "Stop");

            Assert.False(session.IsRunning);
            Assert.Equal("Hello wrold", Entity<DxfDBText>(document, "T1").TextString);
            Assert.Equal("Pleese check \\fArial;thiss text", Entity<DxfMText>(document, "C1").Contents);
        }
    }
}
