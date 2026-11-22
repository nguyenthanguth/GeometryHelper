using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The block and layer commands, AUDIT and the locked-layer line brought to AutoCAD 2021's words as the console of the
    /// "L2 parked" probes (pb, pb2, pd, pd2) wrote them: -ATTDEF, -BLOCK and -INSERT, -LAYER, AUDIT, and where "1 was on a
    /// locked layer." stands among the lines of "Select objects:".
    /// </summary>
    public sealed class L2ParkedBlockLayerTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Typed(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A metric drawing with a line L1 from (0, 0) to (10, 0) and a line L2 from (0, 5) to (10, 5).</summary>
        private static CadDocument TwoLines()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 5, 0), new GeoPoint3(10, 5, 0)));
            return CadDocument.From(database, "two.dxf");
        }

        private static string[] Handles(CadDocument document) =>
            document.ModelSpace.Block.Select(e => e.Handle).ToArray();

        private static int IndexOf(CadCommandSession session, string line, int from = 0)
        {
            for (int i = from; i < session.History.Count; i++)
            {
                if (session.History[i] == line)
                {
                    return i;
                }
            }

            return -1;
        }

        #region The locked-layer line among the lines of "Select objects:" (probe pb2)

        [Fact]
        public void ErasedByAll_TheLockedLine_StandsRightAfterTheFoundLine()
        {
            // AutoCAD 2021 (console, l2p pb2): ERASE, ALL gives "2 found", "1 was on a locked layer.", then "Select objects:";
            // the presenter wrote the line after the last prompt.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "E", "ALL");

            int found = session.History.ToList().FindLastIndex(l => l.EndsWith(" found", StringComparison.Ordinal));
            Assert.True(found > 0);
            Assert.Equal("1 was on a locked layer.", session.History[found + 1]);
            Assert.Equal("Select objects:", session.PromptText);

            Run(session, Enter);
            Assert.Equal(1, session.History.Count(l => l.EndsWith(" on a locked layer.", StringComparison.Ordinal)));
            Assert.NotNull(document.FindEntity("A8"));
            Assert.Null(document.FindEntity("A2"));
        }

        [Fact]
        public void APickOnALockedObject_SaysTheLockedLineRightAfterItsFoundLine_AndOnlyOnce()
        {
            // AutoCAD 2021 (console, l2p pb): a pick on a locked object gives "1 found" and "1 was on a locked layer.".
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "E", new[] { "A8" });

            int found = session.History.ToList().FindLastIndex(l => l == "1 found");
            Assert.True(found > 0);
            Assert.Equal("1 was on a locked layer.", session.History[found + 1]);

            Run(session, Enter);
            Assert.Equal(1, session.History.Count(l => l.EndsWith(" on a locked layer.", StringComparison.Ordinal)));
            Assert.NotNull(document.FindEntity("A8"));
        }

        [Fact]
        public void SeveralLockedObjects_AreSaidInTheWordsOfThePlural()
        {
            // AutoCAD 2021 (console, l2p pb): "3 were on a locked layer."; the plural of one is "was".
            CadDocument document = Plan();
            Assert.True(CadLayerEditor.SetLocked(document, "Steel", true).Succeeded);

            CadCommandSession session = Typed(document, "E", new[] { "A1", "A8", "A2" });

            Assert.Contains("2 were on a locked layer.", session.History);
        }

        #endregion

        #region -ATTDEF (probe pd)

        [Fact]
        public void AttdefShowsItsModesOnTwoLines_AndEachOptionTogglesOneAndShowsThemAgain()
        {
            // AutoCAD 2021 (console, l2p pd): "Current attribute modes:" on a line of its own, then the seven modes with two
            // spaces between them and "Multiple line=N"; the option prompt names Annotative and "Multiple lines".
            CadCommandSession session = Typed(Metric(), "-ATTDEF");

            Assert.Equal("Current attribute modes:", session.History[session.History.Count - 2]);
            Assert.Equal(
                "Invisible=N  Constant=N  Verify=N  Preset=N  Lock position=Y  Annotative=N  Multiple line=N",
                session.History.Last()
            );
            Assert.Equal(
                "Enter an option to change [Invisible/Constant/Verify/Preset/Lock position/Annotative/Multiple lines] <done>:",
                session.PromptText
            );

            Run(session, "I");
            Assert.Equal(
                "Invisible=Y  Constant=N  Verify=N  Preset=N  Lock position=Y  Annotative=N  Multiple line=N",
                session.History.Last()
            );

            Run(session, "A", "L");
            Assert.Equal(
                "Invisible=Y  Constant=N  Verify=N  Preset=N  Lock position=N  Annotative=Y  Multiple line=N",
                session.History.Last()
            );
        }

        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "attdef.dxf");

        [Fact]
        public void AttdefAsksItsWordsAsAutoCadDoes_FromTheJustificationToTheRotation()
        {
            // AutoCAD 2021 (console, l2p pd): "Current text style:  "Standard"  Text height:  2.5000  Justify:  Left", "Specify
            // start point of text or [Justify/Style]:", J asks "Enter an option [Left/Center/Right/Align/Middle/Fit/TL/TC/TR/ML/MC/
            // MR/BL/BC/BR]:" and MC "Specify middle point of text:" (no second line of the style, no Justify or Style any more),
            // then "Specify height <2.5000>:" and "Specify rotation angle of text <0>:".
            CadDocument document = Metric();
            CadCommandSession session = Typed(document, "-ATTDEF", Enter, "TAG2", "Pr", "dv");

            Assert.Equal(
                "Current text style:  \"Standard\"  Text height:  2.5000  Justify:  Left",
                session.History.Last()
            );
            Assert.Equal("Specify start point of text or [Justify/Style]:", session.PromptText);

            Run(session, "J");
            Assert.Equal(
                "Enter an option [Left/Center/Right/Align/Middle/Fit/TL/TC/TR/ML/MC/MR/BL/BC/BR]:",
                session.PromptText
            );

            Run(session, "MC");
            Assert.Equal("Specify middle point of text:", session.PromptText);

            Run(session, P(0, 10));
            Assert.Equal("Specify height <2.5000>:", session.PromptText);

            Run(session, "2.5");
            Assert.Equal("Specify rotation angle of text <0>:", session.PromptText);

            Run(session, "0");
            DxfAttributeDefinition made = Assert.Single(All<DxfAttributeDefinition>(document.ModelSpace));
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, made.Justify);
            Assert.Equal("TAG2", made.Tag);
        }

        [Fact]
        public void AttdefStyle_AsksForAStyleNameOrAQuestionMark_AndShowsTheStyleLineAndThePromptAgain()
        {
            // AutoCAD 2021 (console, l2p pd): "Enter style name or [?] <Standard>:", then "Current text style: ..." and the start
            // point prompt again.
            CadCommandSession session = Typed(Metric(), "-ATTDEF", Enter, "TAG3", "Pr", "dv", "S");

            Assert.Equal("Enter style name or [?] <Standard>:", session.PromptText);

            Run(session, "Standard");
            Assert.Equal(
                "Current text style:  \"Standard\"  Text height:  2.5000  Justify:  Left",
                session.History.Last()
            );
            Assert.Equal("Specify start point of text or [Justify/Style]:", session.PromptText);
        }

        [Fact]
        public void AttdefModesAndJustification_CarryToTheNextAttdef()
        {
            // AutoCAD 2021 (console, l2p pd): after Invisible and Lock position were toggled and MC chosen, the next
            // -ATTDEF shows Invisible=Y, Lock position=N and "Justify:  MC", and asks "Specify middle point of text or [Justify/Style]:".
            CadDocument document = Metric();
            CadCommandSession session = Typed(
                document,
                "-ATTDEF",
                "I",
                "L",
                Enter,
                "TAG2",
                "Pr",
                "dv",
                "J",
                "MC",
                P(0, 10),
                "2.5",
                "0"
            );

            Run(session, "-ATTDEF");

            Assert.Equal(
                "Invisible=Y  Constant=N  Verify=N  Preset=N  Lock position=N  Annotative=N  Multiple line=N",
                session.History.Last()
            );

            Run(session, Enter, "TAG3", "Pr", "dv");
            Assert.Equal(
                "Current text style:  \"Standard\"  Text height:  2.5000  Justify:  MC",
                session.History.Last()
            );
            Assert.Equal("Specify middle point of text or [Justify/Style]:", session.PromptText);

            Run(session, P(0, 20), "2.5", "0");
            DxfAttributeDefinition made = All<DxfAttributeDefinition>(document.ModelSpace).Last();
            Assert.True(made.Invisible);
            Assert.False(made.LockPositionInBlock);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, made.Justify);
        }

        [Fact]
        public void AttdefConstant_AsksTheValueInsteadOfThePromptAndTheDefault()
        {
            // AutoCAD 2021 (console, l2p pd): with Constant on, "Enter attribute tag name:" is followed by "Enter attribute value:".
            CadCommandSession session = Typed(Metric(), "-ATTDEF", "C", Enter, "TAG4");

            Assert.Equal("Enter attribute value:", session.PromptText);
        }

        [Fact]
        public void AttdefAnnotative_IsShownToggledAndStoredOnTheAttributeDefinition()
        {
            // AutoCAD 2021 (console, l2p pd): Annotative=Y after A, and the ATTDEF made is annotative; ours: it is stored as the
            // presenter stores annotative text.
            CadDocument document = Metric();
            Typed(document, "-ATTDEF", "A", Enter, "TAG5", "Pr", "dv", P(0, 0), "2.5", "0");

            DxfAttributeDefinition made = Assert.Single(All<DxfAttributeDefinition>(document.ModelSpace));
            Assert.Equal(DxfAnnotativeStates.True, made.Annotative);
        }

        #endregion

        #region -BLOCK (probes pd and pd2)

        [Fact]
        public void BlockAsksItsBasePointWithAnnotativeAndMode_AndOnlyOAndAAreKeywords()
        {
            // AutoCAD 2021 (console, l2p pd): "Specify insertion base point or [Annotative/mOde]:"; M alone is "Point or option
            // keyword required." and the prompt again.
            CadCommandSession session = Typed(TwoLines(), "-BLOCK", "GHB");

            Assert.Equal("Specify insertion base point or [Annotative/mOde]:", session.PromptText);

            Run(session, "M");
            Assert.Equal("Point or option keyword required.", session.History.Last());
            Assert.Equal("Specify insertion base point or [Annotative/mOde]:", session.PromptText);
        }

        [Fact]
        public void BlockAnnotative_AsksYesNoThenTheOrientation_AndThenTheBasePointAgain()
        {
            // AutoCAD 2021 (console, l2p pd, pd2): A asks "Create annotative block [Yes/No] <Y>:", then always "Orient relative
            // to sheet in paper space viewports [Yes/No] <N>:" (also after No), then the base point prompt again; a point there
            // is "Yes or No, please.".
            CadDocument document = TwoLines();
            CadCommandSession session = Typed(document, "-BLOCK", "GHB", "A");

            Assert.Equal("Create annotative block [Yes/No] <Y>:", session.PromptText);

            Run(session, "Y");
            Assert.Equal("Orient relative to sheet in paper space viewports [Yes/No] <N>:", session.PromptText);

            Run(session, "0,0");
            Assert.Equal("Yes or No, please.", session.History.Last());

            Run(session, "Y");
            Assert.Equal("Specify insertion base point or [Annotative/mOde]:", session.PromptText);

            Run(session, P(0, 0), Handles(document), Enter);
            DxfBlockTableRecord block = document.Database.BlockTable["GHB"];
            Assert.Equal(DxfAnnotativeStates.True, block.Annotative);
            Assert.Equal(DxfPaperOrientationStates.True, block.PaperOrientation);
        }

        [Fact]
        public void BlockAnnotativeNo_StillAsksTheOrientation_AndMakesAnOrdinaryBlock()
        {
            // AutoCAD 2021 (console, l2p pd2): A, N then "Orient relative to sheet in paper space viewports [Yes/No] <N>:".
            CadDocument document = TwoLines();
            CadCommandSession session = Typed(document, "-BLOCK", "GHB3", "A", "N");

            Assert.Equal("Orient relative to sheet in paper space viewports [Yes/No] <N>:", session.PromptText);

            Run(session, Enter, P(0, 0), Handles(document), Enter);
            Assert.NotEqual(DxfAnnotativeStates.True, document.Database.BlockTable["GHB3"].Annotative);
        }

        [Fact]
        public void BlockMode_AsksHowTheObjectsAreConverted_AndRetainKeepsThem()
        {
            // AutoCAD 2021 (console, l2p pd2): O asks "Choose mode for selected object conversion [Convert to block/Retain/Delete]
            // <Delete>:" and then the base point prompt again; a point or Y there is "Invalid option keyword.".
            CadDocument document = TwoLines();
            CadCommandSession session = Typed(document, "-BLOCK", "GHB2", "O");

            Assert.Equal(
                "Choose mode for selected object conversion [Convert to block/Retain/Delete] <Delete>:",
                session.PromptText
            );

            Run(session, "Y");
            Assert.Equal("Invalid option keyword.", session.History.Last());

            Run(session, "R");
            Assert.Equal("Specify insertion base point or [Annotative/mOde]:", session.PromptText);

            Run(session, P(0, 0), Handles(document), Enter);
            Assert.True(document.Database.BlockTable.Has("GHB2"));
            Assert.Equal(2, document.ModelSpace.Block.Count());
        }

        [Fact]
        public void BlockModeConvertToBlock_ReplacesTheObjectsByAReference_AndEnterIsDelete()
        {
            // AutoCAD 2021 (console, l2p pd2): Delete is the default of the mode prompt (Enter keeps the way -BLOCK has always
            // erased the objects); Convert to block puts a reference where they were.
            CadDocument document = TwoLines();
            Typed(document, "-BLOCK", "GHB4", "O", "C", P(0, 0), Handles(document), Enter);

            Assert.Single(document.ModelSpace.Block);
            Assert.Single(All<DxfBlockReference>(document.ModelSpace));

            document = TwoLines();
            Typed(document, "-BLOCK", "GHB5", "O", Enter, P(0, 0), Handles(document), Enter);

            Assert.Empty(document.ModelSpace.Block);
        }

        [Fact]
        public void BlockOfAnExistingName_AsksToRedefineItWithTheDefaultN()
        {
            // AutoCAD 2021 (console, l2p pd): "Block "GHB3" already exists.  Redefine it? [Yes/No] <N>:" (one space after the
            // full stop of the message; the presenter wrote <No>).
            CadDocument document = TwoLines();
            Typed(document, "-BLOCK", "GHB3", P(0, 0), Handles(document), Enter);

            CadCommandSession session = Typed(document, "-BLOCK", "GHB3");

            Assert.Equal("Block \"GHB3\" already exists. Redefine it? [Yes/No] <N>:", session.PromptText);

            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void BlockQuestionMark_AsksWhichBlocksToList_QuotesTheNamesAndEndsWithTheFourLineCount()
        {
            // AutoCAD 2021 (console, l2p pd): "Enter block(s) to list <*>:", "Defined blocks.", each name quoted and indented two
            // spaces, then the table of counts; the command then ends.
            CadDocument document = TwoLines();
            Typed(document, "-BLOCK", "GHB", P(0, 0), new[] { Handles(document)[0] }, Enter);
            Typed(document, "-BLOCK", "GHB3", P(0, 0), Handles(document), Enter);

            CadCommandSession session = Typed(document, "-BLOCK", "?");
            Assert.Equal("Enter block(s) to list <*>:", session.PromptText);

            Run(session, Enter);
            string[] lines = session.History.Skip(IndexOf(session, "Defined blocks.")).ToArray();
            Assert.Equal("Defined blocks.", lines[0]);
            Assert.Equal("  \"GHB\"", lines[1]);
            Assert.Equal("  \"GHB3\"", lines[2]);
            Assert.Equal("User     External     Dependent   Unnamed", lines[3]);
            Assert.Equal("Blocks   References   Blocks      Blocks", lines[4]);
            Assert.Equal("    2         0           0           0", lines[5]);
            Assert.False(session.IsRunning);
        }

        #endregion

        #region -INSERT (probes pd and pd2)

        /// <summary>A metric drawing with the block GB made of a line (0, 0) to (10, 0), and a line from (0, 5) to (10, 5) left.</summary>
        private static CadDocument WithBlock()
        {
            CadDocument document = TwoLines();
            Typed(document, "-BLOCK", "GB", "0,0", new[] { Handles(document)[0] }, Enter);
            return document;
        }

        [Fact]
        public void InsertSaysItsUnitsWithFourSpacesBeforeTheConversion_AndOffersExplodeAndRepeat()
        {
            // AutoCAD 2021 (console, l2p pd): "Units: Millimeters   Conversion:    1.0000" and "Specify insertion point or
            // [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:".
            CadCommandSession session = Typed(WithBlock(), "-INSERT", "GB");

            Assert.Contains("Units: Millimeters   Conversion:    1.0000", session.History);
            Assert.Equal(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:",
                session.PromptText
            );
        }

        [Fact]
        public void InsertExplode_AsksYesNoWithYesAsDefault_AsksTheInsertionPointAgain_AndExplodesTheReference()
        {
            // AutoCAD 2021 (console, l2p pd2): E asks "Explode block [Yes/No] <Yes>:", then the insertion point prompt again; the
            // scale is then asked as for any block ("Enter X scale factor, ..."), and entlast is a LINE, not an INSERT.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-INSERT", "BOX", "E");

            Assert.Equal("Explode block [Yes/No] <Yes>:", session.PromptText.TrimEnd());

            Run(session, Enter);
            Assert.Equal(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:",
                session.PromptText
            );

            Run(session, P(30, 0));
            Assert.Equal("Enter X scale factor, specify opposite corner, or [Corner/XYZ] <1>:", session.PromptText);

            int inserts = All<DxfBlockReference>(document.ModelSpace).Count;
            int lines = All<DxfLine>(document.ModelSpace).Count;
            Run(session, "1", Enter, Enter);

            Assert.Equal(inserts, All<DxfBlockReference>(document.ModelSpace).Count);
            Assert.Equal(lines + 4, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void InsertScaleAndX_OfferOneAsTheirDefault_NotOnePointNoughtNoughtNoughtNought()
        {
            // AutoCAD 2021 (console, l2p pd): "Specify scale factor for XYZ axes <1>:" and "Specify X scale factor <1>:".
            CadCommandSession session = Typed(Plan(), "-INSERT", "BOX", "S");
            Assert.Equal("Specify scale factor for XYZ axes <1>:", session.PromptText);

            Run(session, "2");
            Assert.Equal(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:",
                session.PromptText
            );

            Run(session, "X");
            Assert.Equal("Specify X scale factor <1>:", session.PromptText);
        }

        [Fact]
        public void InsertR_IsRotate_AndRepeatNeedsRE_ThenTheInsertionIsAskedAgainUntilEnter()
        {
            // AutoCAD 2021 (console, l2p pd2): R is Rotate ("Specify rotation angle <0>:" then the insertion point prompt again).
            // Ours (unmeasured): RE places the block again and again until Enter.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-INSERT", "BOX", "R");
            Assert.Equal("Specify rotation angle <0>:", session.PromptText);

            Run(session, "0");
            Assert.Equal(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:",
                session.PromptText
            );
            Run(session, Esc);

            document = Plan();
            int before = All<DxfBlockReference>(document.ModelSpace).Count;
            session = Typed(document, "-INSERT", "BOX", "RE", P(200, 0), "1", Enter, "0");
            Assert.Equal(before + 1, All<DxfBlockReference>(document.ModelSpace).Count);
            Assert.Equal(
                "Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate/Explode/REpeat]:",
                session.PromptText
            );

            Run(session, P(300, 0), "1", Enter, "0");
            Assert.Equal(before + 2, All<DxfBlockReference>(document.ModelSpace).Count);

            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        #endregion

        #region -LAYER (probes pb, pb2 and p1)

        [Fact]
        public void LayerColor_AsksNewColorWithASpaceBeforeTheColon()
        {
            // AutoCAD 2021 (console, l2p pb2, runs/ll): "New color [Truecolor/COlorbook] :".
            CadCommandSession session = Typed(Plan(), "-LAYER", "C");

            Assert.Equal("New color [Truecolor/COlorbook] :", session.PromptText);
        }

        [Fact]
        public void LayerFreezeOffLockAndUnlock_OfferSelectObjects_AndThawAndOnDoNot()
        {
            // AutoCAD 2021 (console, l2p pb2): "Enter name list of layer(s) to freeze or <select objects>:" for Freeze, OFF, LOck
            // and Unlock; Thaw and ON have no <select objects>.
            CadDocument document = Plan();
            Assert.Equal(
                "Enter name list of layer(s) to freeze or <select objects>:",
                Typed(document, "-LAYER", "F").PromptText
            );
            Assert.Equal(
                "Enter name list of layer(s) to turn off or <select objects>:",
                Typed(document, "-LAYER", "OFF").PromptText
            );
            Assert.Equal(
                "Enter name list of layer(s) to lock or <select objects>:",
                Typed(document, "-LAYER", "LO").PromptText
            );
            Assert.Equal(
                "Enter name list of layer(s) to unlock or <select objects>:",
                Typed(document, "-LAYER", "U").PromptText
            );
            Assert.Equal("Enter name list of layer(s) to thaw:", Typed(document, "-LAYER", "T").PromptText);
            Assert.Equal("Enter name list of layer(s) to turn on:", Typed(document, "-LAYER", "ON").PromptText);
        }

        [Fact]
        public void LayerFreezeWithEnter_AsksForObjects_AndFreezesTheLayersOfThoseSelected()
        {
            // AutoCAD 2021 (console, l2p pb2): Enter at <select objects> asks "Select objects:" and takes the layers of the
            // objects selected.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-LAYER", "F", Enter);

            Assert.Equal("Select objects:", session.PromptText);

            Run(session, new[] { "A1" }, Enter);
            Assert.True(document.Database.LayerTable["Steel"].IsFrozen);
            Assert.False(document.Database.LayerTable["0"].IsFrozen);
            Assert.Equal(
                "Enter an option [?/Make/Set/New/Rename/ON/OFF/Color/Ltype/LWeight/TRansparency/MATerial/Plot/Freeze/Thaw/LOck/Unlock/stAte/Description/rEconcile/Xref]:",
                session.PromptText
            );
        }

        [Fact]
        public void LayerMakeAndSet_WithANameNotValid_SayInvalidLayerNameAndAskTheSamePromptAgain()
        {
            // AutoCAD 2021 (console, l2p pb, pb2): M a*b writes "Invalid layer name." and asks "Enter name for new layer (becomes
            // the current layer) <0>:" again; S a*b the same for "Enter layer name to make current or <select object>:"; a name
            // with a back quote is not valid at Set (it takes one name, not a list); a valid name that is not there is
            // Cannot find layer "nosuch". and the prompt again.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-LAYER", "M", "a*b");

            Assert.Equal("Invalid layer name.", session.History.Last());
            Assert.Equal("Enter name for new layer (becomes the current layer) <0>:", session.PromptText);

            Run(session, Enter);
            Run(session, "S", "a*b");
            Assert.Equal("Invalid layer name.", session.History.Last());
            Assert.Equal("Enter layer name to make current or <select object>:", session.PromptText);

            Run(session, "Pl`#1");
            Assert.Equal("Invalid layer name.", session.History.Last());

            Run(session, "nosuch");
            Assert.Equal("Cannot find layer \"nosuch\".", session.History.Last());
            Assert.Equal("Enter layer name to make current or <select object>:", session.PromptText);

            Run(session, "Steel");
            Assert.Equal("Steel", document.Database.Clayer);
        }

        [Fact]
        public void LayerRename_AsksAgainForANameNotValidOrNotThere_AtBothPrompts()
        {
            // AutoCAD 2021 (console, l2p pb, pb2): R a*b is "Invalid layer name." and "Enter old layer name:" again; R nosuch is
            // Cannot find layer "nosuch". and the same again; with the old name found, a new name a*b is "Invalid layer name."
            // and "Enter new layer name:" again.
            CadDocument document = Plan();
            CadCommandSession session = Typed(document, "-LAYER", "R", "a*b");

            Assert.Equal("Invalid layer name.", session.History.Last());
            Assert.Equal("Enter old layer name:", session.PromptText);

            Run(session, "nosuch");
            Assert.Equal("Cannot find layer \"nosuch\".", session.History.Last());
            Assert.Equal("Enter old layer name:", session.PromptText);

            Run(session, "Steel");
            Assert.Equal("Enter new layer name:", session.PromptText);

            Run(session, "a*b");
            Assert.Equal("Invalid layer name.", session.History.Last());
            Assert.Equal("Enter new layer name:", session.PromptText);

            Run(session, "Iron");
            Assert.True(document.Database.LayerTable.Has("Iron"));
            Assert.False(document.Database.LayerTable.Has("Steel"));
        }

        #endregion

        #region AUDIT (probes pb2 and runs/pur)

        [Fact]
        public void AuditAsksWithTheDefaultN_AndReportsInAutoCadsLines()
        {
            // AutoCAD 2021 (console, l2p pb2, runs/pur): "Fix any errors detected? [Yes/No] <N>:", then "Auditing Header", "Auditing
            // Tables", "Auditing Entities Pass 1", "Pass 1 100     objects audited", "Auditing Entities Pass 2", "Pass 2 100
            // objects audited", "Auditing Blocks", " 0       Blocks audited", "Auditing AcDsRecords", "Total errors found 0 fixed 0",
            // "Erased 0 objects" on the blank drawing; "No errors found." is gone.
            CadCommandSession session = Typed(Blank(), "AUDIT");
            Assert.Equal("Fix any errors detected? [Yes/No] <N>:", session.PromptText);

            Run(session, "N");
            string[] lines = session.History.Skip(IndexOf(session, "Auditing Header")).ToArray();
            Assert.Equal(
                new[]
                {
                    "Auditing Header",
                    "Auditing Tables",
                    "Auditing Entities Pass 1",
                    "Pass 1 100     objects audited",
                    "Auditing Entities Pass 2",
                    "Pass 2 100     objects audited",
                    "Auditing Blocks",
                    " 0       Blocks audited",
                    "Auditing AcDsRecords",
                    "Total errors found 0 fixed 0",
                },
                lines.Take(10).ToArray()
            );
            Assert.Equal("Erased 0 objects", session.History.Last());
            Assert.DoesNotContain("No errors found.", session.History);
        }

        [Fact]
        public void AuditCountsObjectsInHundreds_FromTheDrawingsObjectCount()
        {
            // AutoCAD 2021 (console, l2p pb2): 150 lines more gave "Pass 1 200", 450 more "Pass 1 500": the count rounded up to a
            // hundred (ours: from the presenter's own object count, which is not AutoCAD's).
            DxfDatabase database = CadTemplate.CreateMetric();

            for (int i = 0; i < 450; i++)
            {
                database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)));
            }

            CadCommandSession session = Typed(CadDocument.From(database, "many.dxf"), "AUDIT", "N");

            Assert.Contains("Pass 1 500     objects audited", session.History);
            Assert.Contains("Pass 2 500     objects audited", session.History);
        }

        #endregion

        #region Prompts of two lines (probes pc, runs/cd and runs/dd)

        [Fact]
        public void DimlinearsLocationPrompt_BreaksBeforeItsOptions_InTheHistory()
        {
            // AutoCAD 2021 (console, l2p runs/cd): "Specify dimension line location or" on a line and "[Mtext/Text/Angle/Horizontal/
            // Vertical/Rotated]: 50,20" on the next; the presenter wrote one line. DIMALIGNED's short list stays on one line.
            CadCommandSession session = Typed(Metric(), "DIMLINEAR", P(0, 0), P(100, 0));

            Assert.Equal(
                "Specify dimension line location or\n[Mtext/Text/Angle/Horizontal/Vertical/Rotated]:",
                session.PromptText
            );

            Run(session, "50,20");
            int at = IndexOf(
                session,
                "Specify dimension line location or\n[Mtext/Text/Angle/Horizontal/Vertical/Rotated]: 50,20"
            );
            Assert.True(at > 0);
        }

        [Fact]
        public void PlinesArcSegmentPrompt_BreaksBeforeItsOptions()
        {
            // AutoCAD 2021 (console, l2p pc): "Specify endpoint of arc (hold Ctrl to switch direction) or" and, on the next line,
            // "[Angle/CEnter/CLose/Direction/Halfwidth/Line/Radius/Second pt/Undo/Width]:".
            CadCommandSession session = Typed(Metric(), "PLINE", P(0, 0), P(10, 0), "A");

            Assert.Equal(
                "Specify endpoint of arc (hold Ctrl to switch direction) or\n[Angle/CEnter/CLose/Direction/Halfwidth/Line/Radius/Second pt/Undo/Width]:",
                session.PromptText
            );
        }

        [Fact]
        public void DimstylesOptionPrompt_BreaksBeforeItsOptions()
        {
            // AutoCAD 2021 (console, l2p runs/dd): "Enter a dimension style option" and, on the next line, "[ANnotative/Save/Restore/
            // STatus/Variables/Apply/?] <Restore>:".
            CadCommandSession session = Typed(Metric(), "-DIMSTYLE");

            Assert.Equal(
                "Enter a dimension style option\n[ANnotative/Save/Restore/STatus/Variables/Apply/?] <Restore>:",
                session.PromptText
            );
        }

        #endregion
    }
}
