using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// What -RENAME refuses, as AutoCAD 2021's console answered the same names fed through standard input: which refusals end
    /// the command with <c>*Invalid*</c> and which ask again, layer 0, Continuous and the Standard styles, and the words of each.
    /// </summary>
    public class RenameRefusalTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static CadDocument PlanWithMore()
        {
            CadDocument document = Blank();
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 12.5);
            dashed.SetDashLengthAt(1, -6.25);
            document.Database.LinetypeTable.Add(dashed);
            document.Database.BlockTable.Add(new DxfBlockTableRecord { Name = "CRATE" });
            document.Database.TextStyleTable.Add(
                new DxfTextStyleTableRecord { Name = "NOTES", FileName = "romans.shx" }
            );
            document.Database.TextStyleTable.Add(
                new DxfTextStyleTableRecord { Name = "LABELS", FileName = "romans.shx" }
            );
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Defpoints" });
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel" });
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Locked" });
            document.Database.BlockTable.Add(new DxfBlockTableRecord { Name = "BOX" });
            return document;
        }

        [Fact]
        public void LayerZero_IsRefusedAtItsOldName_AndTheCommandEnds()
        {
            CadDocument document = PlanWithMore();
            CadCommandSession session = Start(document, "-RENAME", "LA", "0");

            Assert.Contains("Cannot rename layer \"0\".", session.History);
            Assert.Contains("*Invalid*", session.History);
            Assert.False(session.IsRunning);
            Assert.True(document.Database.LayerTable.Has("0"));
        }

        [Fact]
        public void ContinuousIsRefusedAtItsOldName_AndByLayerIsNotALinetypeName()
        {
            CadDocument document = PlanWithMore();
            CadCommandSession session = Start(document, "-RENAME", "LT", "Continuous");

            Assert.Contains("Cannot rename linetype \"Continuous\".", session.History);
            Assert.Contains("*Invalid*", session.History);
            Assert.False(session.IsRunning);

            session = Start(document, "-RENAME", "LT", "ByLayer");
            Assert.Contains("Invalid linetype name.", session.History);
            Assert.Equal("Enter old linetype name:", session.PromptText);

            session = Start(document, "-RENAME", "LT", "DASHED", "ByBlock");
            Assert.Contains("Invalid linetype name.", session.History);
            Assert.Equal("Enter new linetype name:", session.PromptText);
        }

        [Fact]
        public void ANewNameThatIsTaken_IsRefusedAsInvalid_AndTheCommandEnds()
        {
            CadDocument document = PlanWithMore();

            CadCommandSession session = Start(document, "-RENAME", "LA", "Steel", "LOCKED");
            Assert.Contains("A layer named \"LOCKED\" already exists.", session.History);
            Assert.Contains("*Invalid*", session.History);
            Assert.False(session.IsRunning);
            Assert.True(document.Database.LayerTable.Has("Steel"));

            session = Start(document, "-RENAME", "LT", "DASHED", "Continuous");
            Assert.Contains("A linetype named \"Continuous\" already exists.", session.History);
            Assert.False(session.IsRunning);

            session = Start(document, "-RENAME", "B", "BOX", "crate");
            Assert.Contains("A block named \"crate\" already exists.", session.History);
            Assert.False(session.IsRunning);

            session = Start(document, "-RENAME", "S", "NOTES", "LABELS");
            Assert.Contains("A text style named \"LABELS\" already exists.", session.History);
            Assert.False(session.IsRunning);
            Assert.True(document.Database.TextStyleTable.Has("NOTES"));
        }

        [Fact]
        public void AnInvalidOrEmptyName_IsRefused_AndAskedAgain()
        {
            CadDocument document = PlanWithMore();
            CadCommandSession session = Start(document, "-RENAME", "LA", "Steel", "A<B");

            Assert.Contains("Invalid layer name.", session.History);
            Assert.Equal("Enter new layer name:", session.PromptText);

            int before = session.History.Count(l => l == "Invalid layer name.");
            Run(session, Enter);
            Assert.Equal(before + 1, session.History.Count(l => l == "Invalid layer name."));
            Assert.Equal("Enter new layer name:", session.PromptText);
            Run(session, "  ");
            Assert.Equal(before + 2, session.History.Count(l => l == "Invalid layer name."));
            Run(session, "Beams");
            Assert.True(document.Database.LayerTable.Has("Beams"));
            Assert.False(session.IsRunning);

            session = Start(document, "-RENAME", "LA", Enter);
            Assert.Contains("Invalid layer name.", session.History);
            Assert.Equal("Enter old layer name:", session.PromptText);
        }

        [Fact]
        public void AnEscapeEndsTheQuestionsThatAskAgain()
        {
            CadDocument document = PlanWithMore();
            CadCommandSession session = Start(document, "-RENAME", "LA", "Steel", "A<B");

            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.True(document.Database.LayerTable.Has("Steel"));
        }

        [Fact]
        public void TheStandardTextStyleAndDimensionStyle_AreRenamedAsAnyOther()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-RENAME", "S", "Standard", "XX");

            Assert.False(session.IsRunning);
            Assert.True(document.Database.TextStyleTable.Has("XX"), string.Join(" / ", session.History));
            Assert.False(document.Database.TextStyleTable.Has("Standard"));

            session = Start(document, "-RENAME", "D", "Standard", "YY");
            Assert.False(session.IsRunning);
            Assert.True(document.Database.DimStyleTable.Has("YY"), string.Join(" / ", session.History));
            Assert.False(document.Database.DimStyleTable.Has("Standard"));
        }

        [Fact]
        public void TheDefpointsLayer_IsRenamedAsAnyOther()
        {
            CadDocument document = PlanWithMore();
            CadCommandSession session = Start(document, "-RENAME", "LA", "Defpoints", "DP2");

            Assert.False(session.IsRunning);
            Assert.True(document.Database.LayerTable.Has("DP2"));
            Assert.False(document.Database.LayerTable.Has("Defpoints"));
        }

        [Fact]
        public void ARenameToTheSameNameOrToLettersOfAnotherCase_IsDoneWithNoWord()
        {
            CadDocument document = PlanWithMore();
            CadCommandSession session = Start(document, "-RENAME", "LA", "Steel", "Steel");

            Assert.False(session.IsRunning);
            Assert.DoesNotContain("*Invalid*", session.History);
            Assert.True(document.Database.LayerTable.Has("Steel"));

            session = Start(document, "-RENAME", "LA", "Steel", "STEEL");
            Assert.False(session.IsRunning);
            Assert.DoesNotContain("*Invalid*", session.History);
            Assert.Contains(document.Database.LayerTable, l => l.Name == "STEEL");
        }

        [Fact]
        public void TheStandardMultileaderAndTableStyles_AreRefused_AndTheOldNameIsAskedAgain()
        {
            CadDocument document = BlankWithMLeaderStyle();
            CadCommandSession session = Start(document, "-RENAME", "R", "Standard");

            Assert.Contains("Cannot rename multileader style \"Standard\".", session.History);
            Assert.DoesNotContain("*Invalid*", session.History);
            Assert.Equal("Enter old multileader style name:", session.PromptText);
            Run(session, "Nope");
            Assert.Contains("Cannot find multileader style \"Nope\"", session.History);
            Assert.DoesNotContain("Cannot find multileader style \"Nope\".", session.History);

            Assert.True(CadTableStyleEditor.Add(document, "Standard", null).Succeeded);
            session = Start(document, "-RENAME", "T", "Standard");
            Assert.Contains("Cannot rename table style \"Standard\".", session.History);
            Assert.Equal("Enter old table style name:", session.PromptText);
            Run(session, "Nope");
            Assert.Contains("Cannot find table style \"Nope\"", session.History);
            Assert.DoesNotContain("Cannot find table style \"Nope\".", session.History);
        }

        [Fact]
        public void TheWordsOfAUcs_SayUcsInCapitals_ExceptWhereTheNameIsTaken()
        {
            CadDocument document = Blank();
            document.Ucs.Save("U1");
            document.Ucs.Save("U2");

            CadCommandSession session = Start(document, "-RENAME", "U", "XX");
            Assert.Contains("Cannot find UCS \"XX\".", session.History);
            Run(session, "*ACTIVE");
            Assert.Contains("Invalid UCS name.", session.History);
            Run(session, Esc);

            session = Start(document, "-RENAME", "U", "U1", "*WORLD");
            Assert.Contains("Invalid UCS name.", session.History);
            Assert.Equal("Enter new ucs name:", session.PromptText);
            Run(session, "u2");
            Assert.Contains("A ucs named \"u2\" already exists.", session.History);
            Assert.Contains("*Invalid*", session.History);
            Assert.False(session.IsRunning);
        }
    }
}
