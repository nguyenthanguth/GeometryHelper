using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The command gaps the acceptance routines of the L2 wave showed, each typed as AutoCAD 2021's console took it: -LAYER's
    /// Make, Color and Ltype and the Set prompt that offers an object, UCS's hidden E option and the word it asks again after,
    /// -PURGE's prompts and its lines for each kind, REVCLOUD as a routine's <c>command</c> runs it, and the message DIMLINEAR
    /// writes for a dimension of points that hold on to no object (runs ll, pur, ee, hh, cld, rev and cd of the L2 spec, and
    /// the probes of the F2 part).
    /// </summary>
    public sealed class RoutineCommandGapsTests
    {
        private const string LayerOptions =
            "Enter an option [?/Make/Set/New/Rename/ON/OFF/Color/Ltype/LWeight/TRansparency/MATerial/Plot/Freeze/Thaw/LOck/Unlock/stAte/Description/rEconcile/Xref]:";

        private const string PurgeKinds =
            "Enter type of unused objects to purge [Blocks/DEtailviewstyles/Dimstyles/Groups/LAyers/LTypes/MAterials/MUltileaderstyles/Plotstyles/SHapes/textSTyles/Mlinestyles/SEctionviewstyles/Tablestyles/Visualstyles/Regapps/Zero-length geometry/Empty text objects/Orphaned data/All]:";

        private static CadCommandSession Typed(CadDocument document, params object[] inputs) =>
            Run(Session(document), inputs);

        // The history from the first line that starts with the text, without the trailing spaces.
        private static string[] From(CadCommandSession session, string start) =>
            session
                .History.SkipWhile(l => !l.StartsWith(start, StringComparison.Ordinal))
                .Select(l => l.TrimEnd())
                .ToArray();

        #region -LAYER (runs ll, pur, hh and the F2 probes)

        [Fact]
        public void LayerMake_OffersTheCurrentLayer_MakesTheNameAndMakesItCurrent()
        {
            // AutoCAD 2021 (ll): "Enter name for new layer (becomes the current layer) <0>:", then the options again.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-LAYER");
            Assert.Equal(LayerOptions, session.PromptText);

            Run(session, "M");
            Assert.Equal("Enter name for new layer (becomes the current layer) <0>:", session.PromptText);

            Run(session, "T1-Thay");
            Assert.True(document.Database.LayerTable.Has("T1-Thay"));
            Assert.Equal("T1-Thay", document.Database.Clayer);
            Assert.Equal(7, document.Database.LayerTable["T1-Thay"].ColorIndex);
            Assert.Equal(LayerOptions, session.PromptText);

            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void LayerMake_OfALayerTheDrawingHasSetsItCurrent_AndEnterAloneKeepsTheCurrentOne()
        {
            // AutoCAD 2021 (F2 probe): Make of an existing name sets it current and adds nothing; Enter keeps the current layer.
            CadDocument document = Plan();
            int layers = document.Database.LayerTable.Count();
            CadCommandSession session = Typed(document, "-LAYER", "M", "Steel");

            Assert.Equal("Steel", document.Database.Clayer);
            Assert.Equal(layers, document.Database.LayerTable.Count());

            Run(session, "M", Enter);
            Assert.Equal("Steel", document.Database.Clayer);
            Assert.Equal("Enter an option", session.PromptText.Substring(0, 15));
        }

        [Fact]
        public void LayerColor_AsksTheColourThenTheLayers_NamingTheColoursOneToSeven()
        {
            // AutoCAD 2021 (ll): "Enter name list of layer(s) for color 7 (white) <Defpoints>:", colour 9 with no name.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-LAYER", "M", "T1-Thay", "C", "3");
            Assert.StartsWith("New color [Truecolor/COlorbook]", session.History.Last(l => l.StartsWith("New color")));
            Assert.Equal("Enter name list of layer(s) for color 3 (green) <T1-Thay>:", session.PromptText);

            Run(session, Enter);
            Assert.Equal(3, document.Database.LayerTable["T1-Thay"].ColorIndex);

            Run(session, "C", "9");
            Assert.Equal("Enter name list of layer(s) for color 9 <T1-Thay>:", session.PromptText);
            Run(session, "Steel, Locked");
            Assert.Equal(9, document.Database.LayerTable["Steel"].ColorIndex);
            Assert.Equal(9, document.Database.LayerTable["Locked"].ColorIndex);
        }

        [Fact]
        public void LayerColor_WithAWordItDoesNotKnowOrANumberPastTheTable_SaysSoAndAsksTheColourAgain()
        {
            // AutoCAD 2021 (F2 probe): a word that is no colour, a number outside 1 to 255, and red,green,blue without Truecolor.
            CadCommandSession session = Typed(Plan(), "-LAYER", "C", "foo");
            Assert.Contains("A color number or standard color name is required.", session.History);
            Assert.StartsWith("New color [Truecolor/COlorbook]", session.PromptText);

            Run(session, "999");
            Assert.Contains("Invalid color number.", session.History);
            Assert.StartsWith("New color [Truecolor/COlorbook]", session.PromptText);

            Run(session, "red");
            Assert.Equal("Enter name list of layer(s) for color 1 (red) <0>:", session.PromptText);
        }

        [Fact]
        public void LayerLtype_LoadsTheLinetypeItNamesAndGivesItToTheLayers()
        {
            // AutoCAD 2021 (ll): "Enter loaded linetype name or [?] <Continuous>:", then the name in capitals in the next prompt.
            CadDocument document = Plan();
            Assert.False(document.Database.LinetypeTable.Has("HIDDEN"));
            CadCommandSession session = Typed(document, "-LAYER", "M", "T2-Khuat", "L");
            Assert.Equal("Enter loaded linetype name or [?] <Continuous>:", session.PromptText);

            Run(session, "hidden");
            Assert.Equal("Enter name list of layer(s) for linetype \"HIDDEN\" <T2-Khuat>:", session.PromptText);

            Run(session, Enter);
            Assert.True(document.Database.LinetypeTable.Has("HIDDEN"));
            Assert.Equal("HIDDEN", document.Database.LayerTable["T2-Khuat"].LinetypeName);
            Assert.Equal("Continuous", document.Database.LayerTable["Steel"].LinetypeName, true);
        }

        [Fact]
        public void LayerLtype_OfALinetypeNobodyHas_SaysSoAndGoesBackToTheOptions()
        {
            // AutoCAD 2021 (F2 probe): the linetype is looked for in the linetype file, and a miss ends the question.
            CadCommandSession session = Typed(Plan(), "-LAYER", "L", "zzz");

            Assert.Contains("Linetype \"zzz\" not found.  Use LINETYPE command to load it.", session.History);
            Assert.Equal(LayerOptions, session.PromptText);
        }

        [Fact]
        public void LayerSet_OffersASelectedObject_AsksAgainForAMissingLayer_AndTakesTheLayerOfAnObject()
        {
            // AutoCAD 2021 (F2 probe): "Enter layer name to make current or <select object>:"; a name the drawing lacks is
            // "Cannot find layer "x"." and the question again; Enter asks "Select object:".
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-LAYER", "S");
            Assert.Equal("Enter layer name to make current or <select object>:", session.PromptText);

            Run(session, "nosuch");
            Assert.Contains("Cannot find layer \"nosuch\".", session.History);
            Assert.Equal("Enter layer name to make current or <select object>:", session.PromptText);

            Run(session, Enter);
            Assert.Equal("Select object:", session.PromptText);

            Run(session, P(50, 0));
            Assert.Equal("Steel", document.Database.Clayer);
            Assert.Equal(LayerOptions, session.PromptText);
        }

        [Fact]
        public void LayerLweight_AsksTheWidthThenTheLayers_AsAutoCad2021SpellsIt()
        {
            // AutoCAD 2021 (F2 probe): "Enter lineweight (0.0mm - 2.11mm):", then "...layers(s) for lineweight 0.30mm <GHB>:".
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-LAYER", "M", "GHB", "LW");
            Assert.Equal("Enter lineweight (0.0mm - 2.11mm):", session.PromptText);

            Run(session, "0.30");
            Assert.Equal("Enter name list of layers(s) for lineweight 0.30mm <GHB>:", session.PromptText);

            Run(session, "GHB, Steel");
            Assert.Equal(DxfLineWeight.LineWeight030, document.Database.LayerTable["GHB"].LineWeight);
            Assert.Equal(DxfLineWeight.LineWeight030, document.Database.LayerTable["Steel"].LineWeight);
        }

        [Fact]
        public void LayerRename_AsksTheOldNameThenTheNewOne_AndSaysOfALayerTheDrawingLacks()
        {
            // AutoCAD 2021 (F2 probe): "Enter old layer name:".
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-LAYER", "R");
            Assert.Equal("Enter old layer name:", session.PromptText);

            Run(session, "nosuch");
            Assert.Contains("Cannot find layer \"nosuch\".", session.History);

            Run(session, "R", "Steel");
            Assert.Equal("Enter new layer name:", session.PromptText);

            Run(session, "Beams");
            Assert.True(document.Database.LayerTable.Has("Beams"));
            Assert.False(document.Database.LayerTable.Has("Steel"));
        }

        [Fact]
        public void ARoutineThatMakesALayerAndGivesItAColourAndALinetype_LeavesBothAndEndsTheCommand()
        {
            // The ll routine: (command "layer" "m" "T2-Khuat" "c" "6" "" "l" "hidden" "" "") and a second (command "layer" "n" ...).
            CadDocument document = Blank();
            CadCommandSession session = Typed(
                document,
                "(command \"layer\" \"m\" \"T2-Khuat\" \"c\" \"6\" \"\" \"l\" \"hidden\" \"\" \"\")",
                "(command \"layer\" \"n\" \"I-Wall-hatch\" \"C\" \"8\" \"I-Wall-hatch\" \"\")"
            );

            DxfLayerTableRecord khuat = document.Database.LayerTable["T2-Khuat"];
            Assert.Equal(6, khuat.ColorIndex);
            Assert.Equal("HIDDEN", khuat.LinetypeName);
            Assert.Equal("T2-Khuat", document.Database.Clayer);
            Assert.Equal(8, document.Database.LayerTable["I-Wall-hatch"].ColorIndex);
            Assert.False(session.IsRunning);
            Assert.Equal("Command: nil", session.History.Last());
        }

        [Fact]
        public void LayerThawAndUnlockOfAStar_TakeEveryLayer()
        {
            // The routine LL's LUF and LU (runs/ll): (command "_.LAYER" "_THAW" "*" "") and (command "-Layer" "Unlock" "*" "")
            // thaw and unlock every layer; the presenter wrote Cannot find layer "*" and left them as they were (L2 spec D80).
            CadDocument document = Plan();
            CadCommandSession session = Typed(
                document,
                "(command \"-LAYER\" \"M\" \"A\" \"M\" \"B\" \"S\" \"0\" \"F\" \"A,B\" \"LO\" \"A,B\" \"\")",
                "(command \"_.LAYER\" \"_THAW\" \"*\" \"\")",
                "(command \"-Layer\" \"Unlock\" \"*\" \"\")"
            );

            Assert.DoesNotContain(session.History, l => l.StartsWith("Cannot find layer", StringComparison.Ordinal));
            Assert.False(document.Database.LayerTable["A"].IsFrozen);
            Assert.False(document.Database.LayerTable["B"].IsFrozen);
            Assert.False(document.Database.LayerTable["A"].IsLocked);
            Assert.False(document.Database.LayerTable["B"].IsLocked);
        }

        #endregion

        #region UCS (run ee)

        [Fact]
        public void UcsE_IsTheObjectOption_AsAutoCadKeepsItHidden()
        {
            // AutoCAD 2021 (ee): "e" at the origin prompt asks "Select object to align UCS:".
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "UCS", "e");
            Assert.Equal("Select object to align UCS:", session.PromptText);

            Run(session, P(0, 45));
            Near(new GeoPoint3(0, 50, 0), document.Ucs.Current.Origin);
            Assert.Equal(-Math.PI / 2.0, document.Ucs.Current.Angle, 12);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void UcsAfterAWordItDoesNotKnow_SaysSoAndAsksTheOriginAgain()
        {
            // AutoCAD 2021 (q2): "Point or option keyword required." and the same prompt, as LINE asks again.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "UCS", "100,50", Enter);
            Run(session, "UCS", "q");

            Assert.Contains("Point or option keyword required.", session.History);
            Assert.True(session.IsRunning);
            Assert.Equal(
                "Specify origin of UCS or [Face/NAmed/OBject/Previous/View/World/X/Y/Z/ZAxis] <World>:",
                session.PromptText
            );

            Run(session, "W");
            Assert.False(session.IsRunning);
            Assert.True(document.Ucs.Current.IsWorld);
        }

        [Fact]
        public void ARoutineThatTypesUcsE_LeavesTheCommandAskingForTheObject()
        {
            // The ee routine: (command "ucs" "e") ends with UCS waiting at "Select object to align UCS:".
            CadCommandSession session = Typed(Plan(), "(command \"ucs\" \"e\")");

            Assert.True(session.IsRunning);
            Assert.Equal("Select object to align UCS:", session.PromptText);
        }

        #endregion

        #region -PURGE (run pur and the F2 probes)

        private static CadDocument WithUnusedLayer(string name)
        {
            CadDocument document = Blank();
            Run(Session(document), "-LAYER", "M", name, Enter);
            Run(Session(document), "-LAYER", "S", "0", Enter);
            return document;
        }

        [Fact]
        public void Purge_SaysItsSettingsFirst_AndAsksForTheKindsAutoCad2021Offers()
        {
            CadCommandSession session = Typed(Blank(), "-PURGE");

            Assert.Equal(
                "Nested items = Off    Orphaned data = Off",
                session.History.Last(l => l.StartsWith("Nested"))
            );
            Assert.Equal(PurgeKinds, session.PromptText);

            Run(session, "LA");
            Assert.Equal("Enter name(s) to purge <*>:", session.PromptText);

            Run(session, Enter);
            Assert.Equal("Verify each name to be purged? [Yes/No] <Y>:", session.PromptText);
        }

        [Fact]
        public void PurgeAll_NamesTheLayerItDeletesAndSaysWhatItFoundNoneOf_InTheOrderOfAutoCad2021()
        {
            // The pur routine: -PURGE All, Enter for every name, N for verifying (AutoCAD 2021's lines, one per kind).
            CadDocument document = WithUnusedLayer("UNUSED");
            CadCommandSession session = Typed(document, "-PURGE", "A", Enter, "N");

            Assert.False(document.Database.LayerTable.Has("UNUSED"));
            string[] lines = From(session, "Verify each name").Skip(1).ToArray();
            Assert.Equal(
                new[]
                {
                    "No unreferenced blocks found.",
                    "Deleting layer \"UNUSED\".",
                    "1 layer deleted.",
                    "No unreferenced linetypes found.",
                    "No unreferenced text styles found.",
                    "No unreferenced shape files found.",
                    "No unreferenced dimension styles found.",
                    "No unreferenced mlinestyles found.",
                    "No unreferenced plotstyles found.",
                    "No unreferenced table styles found.",
                    "No unreferenced materials found.",
                    "No unreferenced visual styles found.",
                    "No unreferenced multileader styles found.",
                    "No unreferenced groups found.",
                    "No unreferenced Detail view styles found.",
                    "No unreferenced Section view styles found.",
                },
                lines
            );
        }

        [Fact]
        public void PurgeAll_OfADrawingWithNothingUnused_SaysNoneOfEachKindFound()
        {
            CadDocument document = WithUnusedLayer("UNUSED");
            Run(Session(document), "-PURGE", "A", Enter, "N");
            CadCommandSession session = Typed(document, "-PURGE", "A", Enter, "N");

            string[] lines = From(session, "Verify each name").Skip(1).ToArray();
            Assert.Equal(15, lines.Length);
            Assert.Equal("No unreferenced blocks found.", lines[0]);
            Assert.Equal("No unreferenced layers found.", lines[1]);
            Assert.Equal("No unreferenced Section view styles found.", lines[14]);
            Assert.DoesNotContain("No unreferenced objects found.", session.History);
        }

        [Fact]
        public void PurgeLayersWithVerify_AsksEachNameWithoutBracketsAndTakesYAndN()
        {
            // AutoCAD 2021 (F2 probe): Purge layer "GHU1"? <N> Y, then "GHU2"? <N> N; only the first goes.
            CadDocument document = Blank();
            Run(Session(document), "-LAYER", "M", "GHU1", "M", "GHU2", "S", "0", Enter);
            CadCommandSession session = Typed(document, "-PURGE", "LA", Enter, "Y");
            Assert.Equal("Purge layer \"GHU1\"? <N>", session.PromptText);

            Run(session, "Y");
            Assert.Equal("Purge layer \"GHU2\"? <N>", session.PromptText);

            Run(session, "N");
            Assert.False(document.Database.LayerTable.Has("GHU1"));
            Assert.True(document.Database.LayerTable.Has("GHU2"));
            Assert.Equal("Deleting layer \"GHU1\".", session.History[session.History.Count - 2].TrimEnd());
        }

        [Fact]
        public void PurgeOneKind_OfADrawingThatHasNone_SaysNoUnreferencedOfThatKind()
        {
            CadCommandSession session = Typed(Blank(), "-PURGE", "B", Enter, "N");

            Assert.Equal("No unreferenced blocks found.", session.History.Last().TrimEnd());
        }

        [Fact]
        public void PurgeOrphanedData_SaysThereIsNoneAndEnds()
        {
            // AutoCAD 2021 (F2 probe): "No orphaned data found." with no name or verify question.
            CadCommandSession session = Typed(Blank(), "-PURGE", "O");

            Assert.Equal("No orphaned data found.", session.History.Last().TrimEnd());
            Assert.False(session.IsRunning);
        }

        #endregion

        #region REVCLOUD under a routine (runs cld and rev, the F2 probe)

        [Fact]
        public void RevcloudRunByACommand_AsksTheMinimumAndTheMaximumArcLength_AndMakesTheCloudOfAnObject()
        {
            // AutoCAD 2021 (cld): (command "_.revcloud" "_A" 10 "" "_O" (entlast) "") writes the old prompts: the lengths,
            // "Specify start point or [Arc length/Object/Style] <Object>:", the minimum, the maximum that offers the minimum.
            CadDocument document = Blank();
            CadCommandSession session = Typed(
                document,
                "(command \"_.rectangle\" '(0 0) '(100 50))",
                "(command \"_.revcloud\" \"_A\")"
            );
            Assert.Equal("Specify minimum length of arc <15.0000>:", session.PromptText);
            Assert.Equal(
                "Minimum arc length: 15.0000   Maximum arc length: 15.0000   Style: Normal   Type: Freehand",
                session.History.Last(l => l.StartsWith("Minimum arc length")).TrimEnd()
            );

            // The maximum offers the longer of the new minimum and the old maximum (15 in a metric drawing).
            Run(session, "(command 10)");
            Assert.Equal("Specify maximum length of arc <15.0000>:", session.PromptText);

            Run(session, "(command 10)");
            Assert.Equal("Specify start point or [Arc length/Object/Style] <Object>:", session.PromptText);

            // Asked again (the rev routine's second run): the 10 it kept is offered for the maximum, and Enter takes it.
            Run(session, "(command \"_A\" 10)");
            Assert.Equal("Specify maximum length of arc <10.0000>:", session.PromptText);

            Run(session, "(command \"\")");
            Assert.Equal("Specify start point or [Arc length/Object/Style] <Object>:", session.PromptText);

            Run(session, "(command \"_O\" (entlast) \"\")");
            Assert.False(session.IsRunning);
            Assert.Equal("Revision cloud finished.", session.History[session.History.Count - 2].TrimEnd());

            Assert.Single(document.ModelSpace.Block);
            DxfPolyline cloud = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(30, cloud.NumberOfVertices);
        }

        [Fact]
        public void RevcloudTypedByHand_KeepsItsOwnPrompts()
        {
            // AutoCAD 2021 (F2 probe): typed, REVCLOUD asks for an approximate length, not a minimum and a maximum.
            CadCommandSession session = Typed(Blank(), "REVCLOUD", "A");

            Assert.StartsWith("Specify approximate length of arc", session.PromptText);
        }

        #endregion

        #region DIMLINEAR (run cd)

        [Fact]
        public void DimlinearOfTwoTypedPoints_SaysItMakesANonAssociativeDimension_BeforeAskingWhereTheLineGoes()
        {
            // AutoCAD 2021 (cd): the points hold on to no object, so the dimension is not associative.
            CadCommandSession session = Typed(Blank(), "DIMLINEAR", P(0, 0), P(100, 0));

            string[] lines = session.History.Select(l => l.TrimEnd()).ToArray();
            int made = Array.IndexOf(lines, "Non-associative dimension created.");
            Assert.True(made > 0);
            Assert.StartsWith("Specify second extension line origin", lines[made - 1]);
            Assert.StartsWith("Specify dimension line location or", session.PromptText);
        }

        [Fact]
        public void DimlinearOfALineSelected_IsAssociative_AndSaysNothingOfIt()
        {
            CadCommandSession session = Typed(Blank(), "LINE", P(0, 0), P(100, 0), Enter, "DIMLINEAR", Enter, P(50, 0));

            Assert.StartsWith("Specify dimension line location or", session.PromptText);
            Assert.DoesNotContain("Non-associative dimension created.", session.History);
        }

        #endregion
    }
}
