using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// CHANGE and -RENAME of AutoCAD 2021's acad.pgp, each checked against what AutoCAD 2021's console asked and did to the same
    /// objects: the prompts word for word, the change point and the Properties option of CHANGE, and every kind -RENAME holds.
    /// </summary>
    public class PgpBatch1bTests
    {
        private const string Properties =
            "Enter property to change [Color/Elev/LAyer/LType/ltScale/LWeight/Thickness/TRansparency/Material/Annotative]:";

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A drawing of R2013, which holds true colours and transparency, with a line from (0, 0) to (100, 0) and a circle of radius 10 about (200, 0).</summary>
        private static CadDocument LineAndCircle(out string line, out string circle)
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(100, 0), Enter, "CIRCLE", P(200, 0), "10");
            line = All<DxfLine>(document.ModelSpace).Single().Handle;
            circle = All<DxfCircle>(document.ModelSpace).Single().Handle;
            return document;
        }

        private static CadDocument PlanWithDashed()
        {
            CadDocument document = Plan();
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 12.5);
            dashed.SetDashLengthAt(1, -6.25);
            document.Database.LinetypeTable.Add(dashed);
            return document;
        }

        [Fact]
        public void TheAliasesOfChangeAndRename_ReachTheirCommands()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            foreach (
                string[] pair in new[]
                {
                    new[] { "-CH", "CHANGE" },
                    new[] { "REN", "RENAME" },
                    new[] { "-REN", "-RENAME" },
                }
            )
            {
                Assert.Equal(pair[1], CadShortcuts.Default.Aliases[pair[0]]);
                Assert.Equal(pair[1], registry.Find(pair[0])?.Name);
            }
        }

        [Fact]
        public void AChangePointGivenOnALine_MovesTheEndNearestIt_AsOneUndoStep()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A1" }, Enter);

            Assert.Equal("Specify change point or [Properties]:", session.PromptText);
            Run(session, P(110, 5));

            var line = Entity<DxfLine>(document, "A1");
            Near(new GeoPoint3(0, 0, 0), line.StartPoint);
            Near(new GeoPoint3(110, 5, 0), line.EndPoint);
            AssertReopens(document);
            Assert.True(document.Undo());
            Near(new GeoPoint3(100, 0, 0), Entity<DxfLine>(document, "A1").EndPoint);
        }

        [Fact]
        public void WithOrthomodeOn_AChangePointKeepsTheLineHorizontalOrVertical_AsAutoCadDoes()
        {
            CadDocument document = Blank();
            document.Database.Orthomode = true;
            Start(document, "LINE", P(0, 20), P(100, 30), Enter, "LINE", P(0, 0), P(100, 0), Enter);
            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            string[] both = lines.Select(l => l.Handle).ToArray();
            lines = lines.ToList();

            // AutoCAD 2021: two lines and a point (60, 60) with ORTHOMODE 1; the first, whose end nearest the point is
            // (100, 30), lies along x to (60, 20); the other, whose nearest end is (100, 0), lies along y to (0, 60)
            // (the tie, 60 along each, goes vertical).
            DxfLine slanted = lines.Single(l =>
                Math.Abs(l.EndPoint.Y - 30) < 1E-9 || Math.Abs(l.StartPoint.Y - 30) < 1E-9
            );
            DxfLine flat = lines.Single(l => l != slanted);
            Start(document, "CHANGE", both, Enter, P(60, 60));

            Assert.Contains(new[] { slanted.StartPoint, slanted.EndPoint }, p =>
                p.DistanceTo(new GeoPoint3(60, 20, 0)) < 1E-9
            );
            Assert.Contains(new[] { flat.StartPoint, flat.EndPoint }, p =>
                p.DistanceTo(new GeoPoint3(0, 60, 0)) < 1E-9
            );
        }

        [Fact]
        public void AChangePointOnALoneCircle_SetsItsRadiusToTheDistance_ButIsIgnoredAmongSeveralObjects()
        {
            CadDocument document = Plan();
            Start(document, "CHANGE", new[] { "A3" }, Enter, P(215, 0));
            Assert.Equal(15.0, Entity<DxfCircle>(document, "A3").Radius, 9);

            document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A1", "A3" }, Enter, P(110, 5));
            Assert.Contains("Change point ignored", session.History);
            Assert.Equal("Specify new circle radius <no change>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal(10.0, Entity<DxfCircle>(document, "A3").Radius, 9);
            Near(new GeoPoint3(110, 5, 0), Entity<DxfLine>(document, "A1").EndPoint);
        }

        [Fact]
        public void EnterAtTheChangePointOnACircle_AsksItsRadius_AndRefusesNoughtAsAutoCadDoes()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A3" }, Enter, Enter);

            Assert.Equal("Specify new circle radius <no change>:", session.PromptText);
            Run(session, "0");
            Assert.Contains("Value must be positive and nonzero.", session.History);
            Assert.Equal("Specify new circle radius <no change>:", session.PromptText);
            Run(session, "30");
            Assert.Equal(30.0, Entity<DxfCircle>(document, "A3").Radius, 9);
        }

        [Fact]
        public void EnterAtTheChangePointOnAText_AsksItsInsertionPointStyleHeightRotationAndWords()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A6" }, Enter, Enter);

            Assert.Equal("Specify new text insertion point <no change>:", session.PromptText);
            Run(session, P(10, 210));
            Assert.Equal("Enter new text style <Standard>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify new height <5.0000>:", session.PromptText);
            Run(session, "0");
            Assert.Contains("Value must be positive and nonzero.", session.History);
            Run(session, "7");
            Assert.Equal("Specify new rotation angle <0>:", session.PromptText);
            Run(session, "30");
            Assert.Equal("Enter new text <NOTE>:", session.PromptText);
            Run(session, "NEW TEXT");

            var text = Entity<DxfDBText>(document, "A6");
            Near(new GeoPoint3(10, 210, 0), text.Position);
            Assert.Equal(7.0, text.Height, 9);
            Assert.Equal(30.0 * Math.PI / 180.0, text.Rotation, 9);
            Assert.Equal("NEW TEXT", text.TextString);
            Assert.True(document.Undo());
            Assert.Equal("NOTE", Entity<DxfDBText>(document, "A6").TextString);
            Near(new GeoPoint3(0, 200, 0), Entity<DxfDBText>(document, "A6").Position);
        }

        [Fact]
        public void AChangePointOnALoneText_IsItsNewInsertionPoint_AndAStyleTheDrawingLacksIsSaidSo()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A6" }, Enter, P(40, 60));

            Assert.Equal("Enter new text style <Standard>:", session.PromptText);
            Run(session, "NoSuch");
            Assert.Contains("Style \"NoSuch\" not defined - use STYLE command.", session.History);
            Assert.Equal("Specify new height <5.0000>:", session.PromptText);
            Run(session, Enter, Enter, Enter);

            var text = Entity<DxfDBText>(document, "A6");
            Near(new GeoPoint3(40, 60, 0), text.Position);
            Assert.Equal("NOTE", text.TextString);
            Assert.Equal(5.0, text.Height, 9);
        }

        [Fact]
        public void AChangePointAmongSeveralTexts_IsIgnored_AndEachIsAskedInTurn()
        {
            CadDocument document = Blank();
            Start(document, "TEXT", P(0, 0), "5", "0", "ONE", Enter, "TEXT", P(0, 20), "5", "0", "TWO", Enter);
            string[] both = All<DxfDBText>(document.ModelSpace).Select(t => t.Handle).ToArray();

            CadCommandSession session = Start(document, "CHANGE", both, Enter, P(50, 50));

            Assert.Contains("Change point ignored", session.History);
            Assert.Equal("Specify new text insertion point <no change>:", session.PromptText);
            Run(session, Enter, Enter, Enter, Enter, Enter);
            Assert.Equal("Specify new text insertion point <no change>:", session.PromptText);
        }

        [Fact]
        public void ABlock_IsAskedItsInsertionPointAndRotation_OrOnlyTheRotationWhereAPointWasGiven()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A7" }, Enter, Enter);

            Assert.Equal("Specify new block insertion point <no change>:", session.PromptText);
            Run(session, P(510, 10));
            Assert.Equal("Specify new block rotation angle <0>:", session.PromptText);
            Run(session, "abc");
            Assert.Contains("Requires valid numeric angle or second point.", session.History);
            Run(session, "45");

            var block = Entity<DxfBlockReference>(document, "A7");
            Near(new GeoPoint3(510, 10, 0), block.Position);
            Assert.Equal(45.0 * Math.PI / 180.0, block.Rotation, 9);

            document = Plan();
            session = Start(document, "CHANGE", new[] { "A7" }, Enter, P(520, 20));
            Assert.Equal("Specify new block rotation angle <0>:", session.PromptText);
            Run(session, Enter);
            Near(new GeoPoint3(520, 20, 0), Entity<DxfBlockReference>(document, "A7").Position);
        }

        [Fact]
        public void ObjectsThatChangePointCannotChange_SaySoAndEndTheCommand()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A4", "A5" }, Enter, P(1, 1));

            Assert.Contains("No changeable object selected", session.History);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ThePropertiesOption_OffersAutoCad2021sProperties_AndTheColourOfTheRunBeforeItAsTheDefault()
        {
            CadDocument document = LineAndCircle(out string line, out string circle);
            CadCommandSession session = Start(document, "CHANGE", new[] { line, circle }, Enter, "P");

            Assert.Equal(Properties, session.PromptText);
            Run(session, "C");
            Assert.Equal("New color [Truecolor/COlorbook] <BYLAYER>:", session.PromptText);
            Run(session, "300");
            Assert.Contains("Invalid color number.", session.History);
            Assert.Equal("New color [Truecolor/COlorbook] <BYLAYER>:", session.PromptText);
            Run(session, "green");
            Assert.Equal(Properties, session.PromptText);
            Assert.Equal(3, Entity<DxfLine>(document, line).Color.ColorIndex);
            Assert.Equal(3, Entity<DxfCircle>(document, circle).Color.ColorIndex);

            Run(session, "C");
            Assert.Equal("New color [Truecolor/COlorbook] <3 (green)>:", session.PromptText);
            Run(session, "T");
            Assert.Equal("Red,Green,Blue:", session.PromptText);
            Run(session, "10,20,30");
            Assert.True(Entity<DxfLine>(document, line).Color.IsByColor);
            Run(session, Enter);
            Assert.False(session.IsRunning);

            // One undo step for the whole run.
            Assert.True(document.Undo());
            Assert.True(Entity<DxfLine>(document, line).Color.IsByLayer);
            Assert.True(Entity<DxfCircle>(document, circle).Color.IsByLayer);
        }

        [Fact]
        public void ElevGivesEveryPointTheLevel_AndThicknessIsSetOnWhatHoldsIt()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A1", "A3", "A5" }, Enter, "P", "E");

            Assert.Equal("Specify new elevation <0.0000>:", session.PromptText);
            Run(session, "abc");
            Assert.Contains("Requires numeric distance or two points.", session.History);
            Run(session, "5", "T");
            Assert.Equal("Specify new thickness <0.0000>:", session.PromptText);
            Run(session, "10", Enter);

            var line = Entity<DxfLine>(document, "A1");
            Assert.Equal(5.0, line.StartPoint.Z, 9);
            Assert.Equal(5.0, line.EndPoint.Z, 9);
            Assert.Equal(5.0, Entity<DxfCircle>(document, "A3").Center.Z, 9);
            Assert.Equal(5.0, Entity<DxfPolyline>(document, "A5").Elevation, 9);
            Assert.Equal(10.0, line.Thickness, 9);
            Assert.Equal(10.0, Entity<DxfCircle>(document, "A3").Thickness, 9);
        }

        [Fact]
        public void LayerAndLinetype_AreAskedByNameAndTheNameOfAnotherThingIsRefusedAsAutoCadDoes()
        {
            CadDocument document = PlanWithDashed();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A2" }, Enter, "P", "LA");

            Assert.Equal("Enter new layer name <0>:", session.PromptText);
            Run(session, "NoSuch");
            Assert.Contains("Cannot find layer \"NoSuch\".", session.History);
            Assert.Equal("Enter new layer name <0>:", session.PromptText);
            Run(session, "steel");
            Assert.Equal("Steel", Entity<DxfLine>(document, "A2").Layer);

            Run(session, "LT");
            Assert.Equal("Enter new linetype name <ByLayer>:", session.PromptText);
            Run(session, "Nope");
            Assert.Contains("Linetype \"Nope\" not found.  Use LINETYPE command to load it.", session.History);
            Assert.Equal(Properties, session.PromptText);
            Run(session, "LT", "dashed", Enter);
            Assert.Equal("DASHED", Entity<DxfLine>(document, "A2").Linetype);
        }

        [Fact]
        public void LtscaleLweightAndTransparency_TakeAutoCadsValuesAndWords()
        {
            CadDocument document = LineAndCircle(out string line, out _);
            CadCommandSession session = Start(document, "CHANGE", new[] { line }, Enter, "P", "LTS");

            Assert.Equal("Specify new linetype scale <1.0000>:", session.PromptText);
            Run(session, "0");
            Assert.Contains("Value must be nonzero.", session.History);
            Run(session, "-2");
            Assert.Equal(2.0, Entity<DxfLine>(document, line).LinetypeScale, 9);

            Run(session, "LW");
            Assert.Equal("Enter new lineweight <ByLayer>:", session.PromptText);
            Run(session, "abc");
            Assert.Contains("A weight in lineweight units is required.", session.History);
            Run(session, "-1");
            Assert.Contains("Invalid lweight number.", session.History);
            Run(session, "0.33");
            Assert.Contains("Lineweight rounded to nearest valid value of 0.35 mm", session.History);
            Assert.Equal(DxfLineWeight.LineWeight035, Entity<DxfLine>(document, line).LineWeight);

            Run(session, "TR");
            Assert.Equal("Specify transparency value <ByLayer>:", session.PromptText);
            Run(session, "100");
            Assert.Contains("Requires an integer between 0 and 90.", session.History);
            Run(session, "50");
            Assert.Equal(127, Entity<DxfLine>(document, line).Transparency.Alpha);
            Run(session, "TR", "ByBlock");
            Assert.True(Entity<DxfLine>(document, line).Transparency.IsByBlock);
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void MaterialAndAnnotative_AreAskedAsAutoCadAsksThemAndChangeNothing()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "CHANGE", new[] { "A2" }, Enter, "P", "M");

            Assert.Equal("Enter new material name <ByLayer>:", session.PromptText);
            Run(session, Enter, "A");
            Assert.Equal("Make Annotative? [Yes/No] <No>:", session.PromptText);
            Run(session, Enter, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Rename_WithNoWindowToShowItsDialog_AsksTheKindOfAutoCad2021()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "REN");

            Assert.Equal(
                "Enter object type to rename [Block/dEtailviewstyle/Dimstyle/LAyer/LType/Material/multileadeRstyle/seCtionviewstyle/Style/Tablestyle/Ucs/VIew/VPort]:",
                session.PromptText
            );
        }

        [Fact]
        public void RenameBlock_TakesEveryInsertWithIt_AsksAgainForAWildcardOrAMissingBlock_AsOneUndoStep()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-REN", "B");

            Assert.Equal("Enter old block name:", session.PromptText);
            Run(session, "B*");
            Assert.Contains("Invalid block name.", session.History);
            Run(session, "Nope");
            Assert.Contains("Cannot find block \"Nope\".", session.History);
            Assert.Equal("Enter old block name:", session.PromptText);
            Run(session, "box");
            Assert.Equal("Enter new block name:", session.PromptText);
            Run(session, "A<B");
            Assert.Contains("Invalid block name.", session.History);
            Run(session, "CRATE");

            Assert.Equal("CRATE", Entity<DxfBlockReference>(document, "A7").Name);
            Assert.True(document.Database.BlockTable.Has("CRATE"));
            Assert.False(document.Database.BlockTable.Has("BOX"));
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Equal("BOX", Entity<DxfBlockReference>(document, "A7").Name);
        }

        [Fact]
        public void RenameLayer_AndLinetype_FollowTheirEntitiesAndTheCurrentSetting()
        {
            CadDocument document = PlanWithDashed();
            Entity<DxfLine>(document, "A2").Linetype = "DASHED";
            document.Database.Celtype = "DASHED";

            CadCommandSession session = Start(document, "-RENAME", "LT");
            Assert.Equal("Enter old linetype name:", session.PromptText);
            Run(session, "dashed");
            Assert.Equal("Enter new linetype name:", session.PromptText);
            Run(session, "HIDDEN");

            Assert.Equal("HIDDEN", Entity<DxfLine>(document, "A2").Linetype);
            Assert.Equal("HIDDEN", document.Database.Celtype);

            session = Start(document, "-RENAME", "LA", "Steel");
            Assert.Equal("Enter new layer name:", session.PromptText);
            Run(session, "0");

            // AutoCAD 2021 ends the command on a name another layer has, as *Invalid*; the rename is asked again from the start.
            Assert.Contains("A layer named \"0\" already exists.", session.History);
            Assert.False(session.IsRunning);
            session = Start(document, "-RENAME", "LA", "Steel", "Beams");
            Assert.Equal("Beams", Entity<DxfLine>(document, "A1").Layer);
            Assert.True(document.Undo());
            Assert.Equal("Steel", Entity<DxfLine>(document, "A1").Layer);
            Assert.True(document.Undo());
            Assert.Equal("DASHED", Entity<DxfLine>(document, "A2").Linetype);
        }

        [Fact]
        public void RenameTextStyle_AndDimensionStyle_UseAutoCadsWordsForThem()
        {
            CadDocument document = Blank();
            document.Database.TextStyleTable.Add(
                new DxfTextStyleTableRecord { Name = "NOTES", FileName = "romans.shx" }
            );
            CadCommandSession session = Start(document, "-RENAME", "S");

            Assert.Equal("Enter old text style name:", session.PromptText);
            Run(session, "NOTES");
            Assert.Equal("Enter new text style name:", session.PromptText);
            Run(session, "LABELS");
            Assert.True(document.Database.TextStyleTable.Has("LABELS"));
            Assert.False(document.Database.TextStyleTable.Has("NOTES"));

            session = Start(document, "-RENAME", "D");
            Assert.Equal("Enter old dimension style name:", session.PromptText);
            Run(session, "Standard");
            Assert.Equal("Enter new dimension style name:", session.PromptText);
        }

        [Fact]
        public void RenameMultileaderStyle_AndTableStyle_KeepTheCurrentOneCurrent()
        {
            CadDocument document = BlankWithMLeaderStyle();
            Assert.True(CadMLeaderStyleEditor.Add(document, "NOTES", null).Succeeded);
            Assert.True(CadMLeaderStyleEditor.SetCurrent(document, "NOTES").Succeeded);
            Assert.True(CadTableStyleEditor.Add(document, "SCHEDULE", null).Succeeded);
            Assert.True(CadTableStyleEditor.SetCurrent(document, "SCHEDULE").Succeeded);

            CadCommandSession session = Start(document, "-RENAME", "R");
            Assert.Equal("Enter old multileader style name:", session.PromptText);
            Run(session, "notes");
            Assert.Equal("Enter new multileader style name:", session.PromptText);
            Run(session, "LABELS");
            Assert.NotNull(CadMLeaderStyleEditor.Find(document.Database, "LABELS"));
            Assert.Null(CadMLeaderStyleEditor.Find(document.Database, "NOTES"));
            Assert.Equal("LABELS", CadMLeaderStyleEditor.Current(document.Database));

            session = Start(document, "-RENAME", "T");
            Assert.Equal("Enter old table style name:", session.PromptText);
            Run(session, "SCHEDULE", "LIST");
            Assert.NotNull(CadTableStyleEditor.Find(document.Database, "LIST"));
            Assert.Equal("LIST", CadTableStyleEditor.Current(document.Database));

            Assert.False(CadMLeaderStyleEditor.Rename(document, "Standard", "OTHER").Succeeded);
            Assert.False(CadTableStyleEditor.Rename(document, "Standard", "OTHER").Succeeded);
        }

        [Fact]
        public void RenameViewAndViewport_AskAutoCadsWords_AndSayWhereNothingHasTheName()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-RENAME", "VI");
            Assert.Equal("Enter old view name:", session.PromptText);
            Run(session, "NONE");
            Assert.Contains("Cannot find view \"NONE\".", session.History);

            session = Start(document, "-RENAME", "VP");
            Assert.Equal("Enter old viewport configuration name:", session.PromptText);
            Run(session, "Tiles");
            Assert.Contains("Cannot find viewport configuration \"Tiles\".", session.History);

            session = Start(document, "-RENAME", "U");
            Assert.Equal("Enter old ucs name:", session.PromptText);
        }

        [Fact]
        public void RenameMaterial_AndTheViewStylesOfSheets_SayTheDrawingHoldsNone()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-RENAME", "M");

            Assert.Contains("This drawing holds no materials.", session.History);
            Assert.False(session.IsRunning);
        }
    }
}
