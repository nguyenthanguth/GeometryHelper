using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// GROUPEDIT as AutoCAD 2021 asks it: "Select group or [Name]:", then "Enter an option [Add objects/Remove objects/REName]:"
    /// until Enter. Add and Remove pick the objects one by one as -GROUP's options do; REName asks the new name.
    /// </summary>
    public class GroupEditTests
    {
        private static CadDocument WithGroup()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "-GROUP", "Create", "WALLS", "Two walls", new[] { "A1", "A2" }, Enter);
            Assert.NotNull(document.Database.GroupDictionary);
            return document;
        }

        private static string[] Members(CadDocument document, string group) =>
            ((DxfGroup)document.Database.GroupDictionary.GetAt(group))
                .GetAllEntityIds()
                .Select(e => e.Handle)
                .OrderBy(h => h, System.StringComparer.Ordinal)
                .ToArray();

        [Fact]
        public void PickingAnObjectOfAGroup_AsksTheOptions_AndAddObjectsAddsThemAsOneUndoStep()
        {
            CadDocument document = WithGroup();
            CadCommandSession session = Session(document);

            Run(session, "GROUPEDIT");
            Assert.Equal("Select group or [Name]:", session.PromptText);
            session.FeedSelection(new[] { "A1" });
            Assert.Equal("Enter an option [Add objects/Remove objects/REName]:", session.PromptText);
            Run(session, "Add objects");
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Contains("Select objects to add to group...", session.History);
            Run(session, new[] { "A3", "A4" }, Enter);

            // The options are asked again until Enter ends the command.
            Assert.Equal("Enter an option [Add objects/Remove objects/REName]:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "A1", "A2", "A3", "A4" }, Members(document, "WALLS"));
            Assert.Equal("GROUPEDIT", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(new[] { "A1", "A2" }, Members(document, "WALLS"));
        }

        [Fact]
        public void TheGroupCanBeNamed_AndRemoveObjectsTakesThemOut()
        {
            CadDocument document = WithGroup();
            CadCommandSession session = Session(document);

            Run(session, "GROUPEDIT", "Name");
            Assert.Equal("Enter a group name or [?]:", session.PromptText);
            Run(session, "WALLS", "Remove objects");
            Assert.Equal("Remove objects:", session.PromptText);
            Run(session, new[] { "A2" }, Enter, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "A1" }, Members(document, "WALLS"));
        }

        [Fact]
        public void ANameThatIsNoGroup_IsToldNotFound_AndAskedAgain()
        {
            CadDocument document = WithGroup();
            CadCommandSession session = Session(document);

            Run(session, "GROUPEDIT", "Name", "NOPE");

            Assert.Contains("Group not found.", session.History);
            Assert.Equal("Enter a group name or [?]:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AnObjectInNoGroup_IsToldSo_AndAnotherAsked()
        {
            CadDocument document = WithGroup();
            CadCommandSession session = Session(document);

            Run(session, "GROUPEDIT", new[] { "A5" });

            Assert.Contains("Object is not a member of any group.", session.History);
            Assert.Equal("Select group or [Name]:", session.PromptText);
        }

        [Fact]
        public void REName_AsksTheNewName_RenamesTheGroup_AndTheOptionsGoOnWithTheNewName()
        {
            CadDocument document = WithGroup();
            CadCommandSession session = Session(document);

            Run(session, "GROUPEDIT", new[] { "A1" }, "REName");
            Assert.Equal("Enter a new name for group or [?]:", session.PromptText);
            Run(session, "BEAMS");

            Assert.Equal("Enter an option [Add objects/Remove objects/REName]:", session.PromptText);
            Assert.False(document.Database.GroupDictionary.Contains("WALLS"));
            Assert.Equal(new[] { "A1", "A2" }, Members(document, "BEAMS"));

            // Adding now goes to the group under its new name.
            Run(session, "Add objects", new[] { "A5" }, Enter, Enter);
            Assert.Equal(new[] { "A1", "A2", "A5" }, Members(document, "BEAMS"));
            Assert.Equal("GROUPEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.True(document.Database.GroupDictionary.Contains("WALLS"));
        }

        [Fact]
        public void ANameAGroupAlreadyHas_IsRefusedAtREName()
        {
            CadDocument document = WithGroup();
            CadCommandSession session = Session(document);
            Run(session, "-GROUP", "Create", "BEAMS", "Beams", new[] { "A5" }, Enter);

            Run(session, "GROUPEDIT", new[] { "A1" }, "REName", "BEAMS");

            Assert.Contains("Group \"BEAMS\" already exists.", session.History);
            Assert.True(document.Database.GroupDictionary.Contains("WALLS"));
        }

        [Fact]
        public void AnAddedObjectAlreadyInTheGroup_IsPassedOver()
        {
            CadDocument document = WithGroup();
            CadCommandSession session = Session(document);

            Run(session, "GROUPEDIT", new[] { "A1" }, "Add objects", new[] { "A1", "A3" }, Enter, Enter);

            Assert.Equal(new[] { "A1", "A2", "A3" }, Members(document, "WALLS"));
        }
    }
}
