using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// ATTDISP and -ATTEDIT's global editing, as AutoCAD 2021's (probed in its console: "Enter attribute visibility setting
    /// [Normal/ON/OFF] &lt;Normal&gt;:", "Regenerating model.", "Performing global editing of attribute values.", "Edit only
    /// attributes visible on screen? [Yes/No] &lt;Y&gt;:", "Drawing must be regenerated afterwards.", "Enter block name
    /// specification &lt;*&gt;:", "n attributes selected.", "Enter string to change:", "Enter new string:"). Three INSERTs of
    /// the block TAGS, each with the attributes NOTE and CODE, stand at (0, 0), (50, 0) and (100, 0); CODE is invisible.
    /// </summary>
    public class AttributeDisplayTests
    {
        /// <summary>The three INSERTs; their NOTE reads "HELLO ABC abc ABC", "ABC one" and "none here", their CODE "second ABC", "abc two" and "nothing".</summary>
        private static CadDocument Tagged()
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "TAGS" };
            database.BlockTable.Add(block);
            block.AppendEntity(
                new DxfAttributeDefinition
                {
                    Tag = "NOTE",
                    Prompt = "Note",
                    TextString = "x",
                    Position = new GeoPoint3(0, 5, 0),
                    Height = 2.5,
                }
            );
            block.AppendEntity(
                new DxfAttributeDefinition
                {
                    Tag = "CODE",
                    Prompt = "Code",
                    TextString = "y",
                    Position = new GeoPoint3(0, 0, 0),
                    Height = 2.5,
                    Invisible = true,
                }
            );

            string[][] values =
            {
                new[] { "HELLO ABC abc ABC", "second ABC" },
                new[] { "ABC one", "abc two" },
                new[] { "none here", "nothing" },
            };

            for (int i = 0; i < 3; i++)
            {
                var insert = new DxfBlockReference(new GeoPoint3(50 * i, 0, 0), block);
                database.ModelSpace.AppendEntity(insert);

                foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>())
                {
                    var attribute = new DxfAttributeReference
                    {
                        Tag = definition.Tag,
                        TextString = values[i][definition.Tag == "NOTE" ? 0 : 1],
                        Position = new GeoPoint3(50 * i, definition.Tag == "NOTE" ? 5 : 0, 0),
                        Height = 2.5,
                        Invisible = definition.Invisible,
                    };
                    insert.AttributeCollection.AppendAttribute(attribute);
                }
            }

            return CadDocument.From(database, "tags.dxf");
        }

        private static string[] Values(CadDocument document, string tag) =>
            All<DxfBlockReference>(document.ModelSpace)
                .SelectMany(i => i.AttributeCollection.OfType<DxfAttributeReference>())
                .Where(a => a.Tag == tag)
                .Select(a => a.TextString)
                .ToArray();

        private static int Shown(CadDocument document) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.OfType<DisplayText2>()
                .Count(t => !string.IsNullOrEmpty(t.Handle));

        [Fact]
        public void AttDisp_AsksNormalOnOff_ShowsOrHidesAttributesAtOnce_AndIsOneUndoStep()
        {
            CadDocument document = Tagged();
            Assert.Equal(3, Shown(document));

            CadCommandSession session = Run(Session(document), "ATTDISP");
            Assert.Equal("Enter attribute visibility setting [Normal/ON/OFF] <Normal>:", session.PromptText);
            Run(session, "ON");
            Assert.Contains("Regenerating model.", session.History);
            Assert.Equal(2, document.Database.Attmode);
            Assert.Equal(6, Shown(document));
            Assert.Equal("ATTDISP", document.NextUndo.Name);

            Run(session, "ATTDISP");
            Assert.Equal("Enter attribute visibility setting [Normal/ON/OFF] <ON>:", session.PromptText);
            Run(session, "OFF");
            Assert.Equal(0, document.Database.Attmode);
            Assert.Equal(0, Shown(document));

            Assert.True(document.Undo());
            Assert.Equal(2, document.Database.Attmode);
            Assert.Equal(6, Shown(document));
            Assert.True(document.Undo());
            Assert.Equal(1, document.Database.Attmode);
            Assert.Equal(3, Shown(document));
        }

        [Fact]
        public void AttDisp_TheSameSetting_ChangesNothingAndSaysNothing()
        {
            CadDocument document = Tagged();
            CadCommandSession session = Run(Session(document), "ATTDISP", Enter);

            Assert.False(session.IsRunning);
            Assert.DoesNotContain("Regenerating model.", session.History);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void GlobalAttEdit_AsksInAutoCadsWords_AndReplacesEveryOccurrenceOfTheString_CaseKeptApart()
        {
            CadDocument document = Tagged();
            CadCommandSession session = Run(Session(document), "-ATTEDIT");
            Assert.Equal("Edit attributes one at a time? [Yes/No] <Yes>:", session.PromptText);
            Run(session, "N");
            Assert.Contains("Performing global editing of attribute values.", session.History);
            Assert.Equal("Edit only attributes visible on screen? [Yes/No] <Y>:", session.PromptText);
            Run(session, "N");
            Assert.Contains("Drawing must be regenerated afterwards.", session.History);
            Assert.Equal("Enter block name specification <*>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter attribute tag specification <*>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter attribute value specification <*>:", session.PromptText);
            Run(session, Enter);
            Assert.Contains("6 attributes selected.", session.History);
            Assert.Equal("Enter string to change:", session.PromptText);
            Run(session, "ABC");
            Assert.Equal("Enter new string:", session.PromptText);
            Run(session, "xyz");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "HELLO xyz abc xyz", "xyz one", "none here" }, Values(document, "NOTE"));
            Assert.Equal(new[] { "second xyz", "abc two", "nothing" }, Values(document, "CODE"));
            Assert.Equal("-ATTEDIT", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "HELLO ABC abc ABC", "ABC one", "none here" }, Values(document, "NOTE"));
            Assert.Equal(new[] { "second ABC", "abc two", "nothing" }, Values(document, "CODE"));
        }

        [Fact]
        public void GlobalAttEdit_TheSpecificationsPickTheAttributes_AnEmptyStringToChangePutsTheNewOneAtTheFront()
        {
            CadDocument document = Tagged();
            CadCommandSession session = Run(Session(document), "-ATTEDIT", "N", "N", "TAGS", "NOTE", "n*");
            Assert.Contains("1 attributes selected.", session.History);
            Run(session, Enter, ">> ");

            Assert.Equal(new[] { "HELLO ABC abc ABC", "ABC one", ">> none here" }, Values(document, "NOTE"));
            Assert.Equal(new[] { "second ABC", "abc two", "nothing" }, Values(document, "CODE"));
        }

        [Fact]
        public void GlobalAttEdit_AnEmptyNewString_TakesTheOldOneOut_AndNoMatchEndsTheCommand()
        {
            CadDocument document = Tagged();
            Run(Session(document), "-ATTEDIT", "N", "N", Enter, Enter, Enter, "ABC", Enter);
            Assert.Equal(new[] { "HELLO  abc ", " one", "none here" }, Values(document, "NOTE"));

            CadCommandSession session = Run(Session(document), "-ATTEDIT", "N", "N", Enter, Enter, "nomatch*");
            Assert.Contains("0 attributes selected.", session.History);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void GlobalAttEdit_OfTheVisibleAttributes_TakesTheOnesTheDisplayShows()
        {
            CadDocument document = Tagged();
            string[] inserts = All<DxfBlockReference>(document.ModelSpace).Select(i => i.Handle).ToArray();
            CadCommandSession session = Run(Session(document), "-ATTEDIT", "N", "Y", Enter, Enter, Enter);
            Assert.Equal("Select Attributes:", session.PromptText);
            Run(session, inserts, Enter);

            // CODE is invisible, so only the three NOTEs are on screen.
            Assert.Contains("3 attributes selected.", session.History);
            Run(session, "ABC", "Q");
            Assert.Equal(new[] { "HELLO Q abc Q", "Q one", "none here" }, Values(document, "NOTE"));
            Assert.Equal(new[] { "second ABC", "abc two", "nothing" }, Values(document, "CODE"));
        }
    }
}
