using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The faults the acceptance of L4 (part H4) and its documentation pass found against the full AutoCAD 2021, each typed
    /// as the feeds <c>dcd</c>, <c>ft</c>, <c>te</c>, <c>fdval</c> and <c>undo</c> typed them and the probe <c>f2consts</c>
    /// (runs-gui of the L4 scratch): the value of <c>vlax-for</c>, the <c>ac...</c> constants, the case ActiveX symbols print
    /// in, the realignment of a justified text, the undo steps of typed forms, and the colour and linetype groups of
    /// <c>entmake</c> and <c>entmod</c>. A row is marked <c>AutoCAD 2021 (feed)</c> where the full AutoCAD gave it.
    /// </summary>
    public sealed class LispAcceptanceFixTests : IDisposable
    {
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Loaded(CadDocument document = null)
        {
            CadCommandSession session = Session(document ?? Blank());
            Feed(session, "(vl-load-com)");
            Feed(session, "(setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))");
            return session;
        }

        // Types a form and gives what it wrote after the echo.
        private static string Feed(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form.StartsWith("(", StringComparison.Ordinal) ? "!" + form : form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        private const string Line =
            "(entmake (list '(0 . \"LINE\") '(8 . \"0\") '(10 0.0 {0} 0.0) '(11 10.0 {0} 0.0)))";

        private static void MakeLine(CadCommandSession session, double y) =>
            Feed(session, string.Format(CultureInfo.InvariantCulture, Line, y));

        #region vlax-for

        [Fact]
        public void VlaxFor_GivesTheValueOfItsLastBodyForm()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (feed fdval): (vlax-for b (vla-get-Blocks doc) 5) gives 5, and with (vla-get-Name b) as the body the
            // name of the last block; an empty collection gives nil.
            Assert.Equal("5", Feed(session, "(vlax-for b (vla-get-Blocks doc) 5)"));
            Assert.Equal("7", Feed(session, "(vlax-for b (vla-get-Blocks doc) 6 7)"));
            Assert.Equal(
                Feed(
                    session,
                    "(vla-get-Name (vla-Item (vla-get-Blocks doc) (1- (vla-get-Count (vla-get-Blocks doc)))))"
                ),
                Feed(session, "(vlax-for b (vla-get-Blocks doc) (vla-get-Name b))")
            );
            Assert.Equal("nil", Feed(session, "(vlax-for b (vla-get-SelectionSets doc) 7)"));
        }

        [Fact]
        public void VlaxFor_GivesNilWhereTheBodyIsEmpty()
        {
            CadCommandSession session = Loaded();

            // ours: no body form, no value; the loop still runs
            Assert.Equal("nil", Feed(session, "(vlax-for b (vla-get-Blocks doc))"));
        }

        #endregion

        #region The ac constants

        [Theory]
        [InlineData("acSelectionSetWindow", "0")]
        [InlineData("acSelectionSetAll", "5")]
        [InlineData("acSelectionSetCrossingPolygon", "7")]
        [InlineData("acActiveViewport", "0")]
        [InlineData("acAllViewports", "1")]
        [InlineData("acPaperSpace", "0")]
        [InlineData("acModelSpace", "1")]
        [InlineData("acAttachmentPointTopLeft", "1")]
        [InlineData("acAttachmentPointBottomRight", "9")]
        [InlineData("acAlignmentLeft", "0")]
        [InlineData("acAlignmentCenter", "1")]
        [InlineData("acAlignmentMiddle", "4")]
        [InlineData("acTrue", "1")]
        [InlineData("acFalse", "0")]
        [InlineData("acRed", "1")]
        [InlineData("acByLayer", "256")]
        [InlineData("acLnWt025", "25")]
        [InlineData("acInsertUnitsMillimeters", "4")]
        [InlineData("acDegrees", "0")]
        [InlineData("acUnknown", "-1")]
        public void AfterVlLoadCom_TheEnumerationConstantsAreBoundToTheirValues(string name, string value)
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (probe f2consts): every constant of the type library's enumerations is bound, to the value of the
            // library (all 898 names); the values here are those of research/interop-members.txt.
            Assert.Equal(value, Feed(session, "!" + name));
        }

        [Fact]
        public void TheConstantsOfTheEnumerationsAreUnboundBeforeVlLoadCom()
        {
            CadCommandSession session = Session(Blank());

            // ours: they come with the other ActiveX names; the console (a3) had only vlax-for before the load
            Assert.Equal("nil", Feed(session, "!acSelectionSetAll"));
        }

        [Fact]
        public void ASelectionSetSelectedWithAcSelectionSetAll_HoldsEveryEntity()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 0.0);
            MakeLine(session, 5.0);
            MakeLine(session, 10.0);
            Feed(session, "(setq ss (vla-Add (vla-get-SelectionSets doc) \"GHF2\"))");

            // AutoCAD 2021 (probe f2consts): (vla-Select ss acSelectionSetAll) selects the entities of the drawing
            Assert.Equal("nil", Feed(session, "(vla-Select ss acSelectionSetAll)"));
            Assert.Equal("3", Feed(session, "(vla-get-Count ss)"));
        }

        [Fact]
        public void ARoutinesOwnValueOfAConstant_IsKeptAtTheLoad()
        {
            CadCommandSession session = Session(Blank());
            Feed(session, "(setq acRed 99)");
            Feed(session, "(vl-load-com)");

            // ours: the load binds only what is unbound, as AutoCAD (where they are bound at the start) lets a setq change one
            Assert.Equal("99", Feed(session, "!acRed"));
            Assert.Equal("2", Feed(session, "!acYellow"));
        }

        #endregion

        #region Print names

        [Fact]
        public void TheActiveXFunctionsPrintInTheCaseAutoCadRegistersThem()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (probe f2consts): vlax-for stays upper case, it exists before vl-load-com; a name that is none prints
            // as read
            Assert.Equal(
                "\"(vla-get-Name vla-put-Color vlax-get-acad-object VLAX-FOR vlax-invoke FOO)\"",
                Feed(
                    session,
                    "(vl-prin1-to-string '(vla-get-Name vla-put-Color vlax-get-acad-object vlax-for vlax-invoke foo))"
                )
            );
            Assert.Equal(
                "\"(vlax-curve-getEndParam vlax-curve-isClosed vlax-map-Collection vlax-vbDouble vlax-3d-point)\"",
                Feed(
                    session,
                    "(vl-prin1-to-string '(vlax-curve-getendparam vlax-curve-isclosed vlax-map-collection vlax-vbdouble vlax-3d-point))"
                )
            );
            Assert.Equal(
                "\"(variant-value safearray-value TEXTBOX acSelectionSetAll)\"",
                Feed(session, "(vl-prin1-to-string '(variant-value safearray-value textbox acselectionsetall))")
            );
        }

        [Fact]
        public void ASymbolReadBeforeTheLoad_PrintsInTheRegisteredCaseAfterIt()
        {
            CadCommandSession session = Session(Blank());
            Feed(session, "(setq early '(vla-get-name vlax-for))");
            Assert.Equal("(VLA-GET-NAME VLAX-FOR)", Feed(session, "!early"));
            Feed(session, "(vl-load-com)");

            // AutoCAD 2021 (probe f2consts): the symbol is the one the library registered, whenever it was read
            Assert.Equal("(vla-get-Name VLAX-FOR)", Feed(session, "!early"));
        }

        [Fact]
        public void AnActiveXFunctionTypedInAnyCase_StillCalls()
        {
            CadCommandSession session = Loaded();

            Assert.Equal("\"0\"", Feed(session, "(vla-get-name (vla-get-ActiveLayer doc))"));
            Assert.Equal("\"0\"", Feed(session, "(VLA-GET-NAME (vla-get-activelayer doc))"));
        }

        #endregion

        #region Realigning a justified text

        private static CadCommandSession WithFont(FontFolder fonts)
        {
            fonts.Write("test.shx", ShxFiles.Unifont());
            CadDocument document = Blank();
            document.Database.TextStyleTable["Standard"].FileName = "test.shx";
            document.Fonts = new CadFonts(new[] { fonts.FullPath }, null, null);
            return Loaded(document);
        }

        private static double X(CadCommandSession session, string entity) =>
            double.Parse(Feed(session, "(car (cdr (assoc 10 (entget " + entity + "))))"), CultureInfo.InvariantCulture);

        private static void MakeText(CadCommandSession session, string name, double x, int mode)
        {
            Feed(
                session,
                "(entmake (list '(0 . \"TEXT\") '(8 . \"0\") (list 10 "
                    + x.ToString("R", CultureInfo.InvariantCulture)
                    + " 0.0 0.0) '(40 . 2.5) '(1 . \"IOI\") '(50 . 0.0) '(7 . \"Standard\") '(72 . "
                    + mode
                    + ") (list 11 "
                    + x.ToString("R", CultureInfo.InvariantCulture)
                    + " 0.0 0.0)))"
            );
            Feed(session, "(setq " + name + " (entlast))");
        }

        [Fact]
        public void ANewTextOfAJustifiedText_MovesItsFirstPointByTheSameShiftThroughTheObjectAndThroughEntmod()
        {
            using (var fonts = new FontFolder())
            {
                CadCommandSession session = WithFont(fonts);
                MakeText(session, "t1", 0.0, 1);
                MakeText(session, "t2", 100.0, 1);

                // AutoCAD 2021 (feed undo, D253): two centred TEXTs "ab" at x 0 and 100 changed to "a much longer text" give the
                // first point at -21.4583 and 78.5417, the same shift of 21.4583 both ways; the font here is the test's own.
                Feed(session, "(vla-put-TextString (vlax-ename->vla-object t1) \"IOIOIOIOI\")");
                Feed(session, "(entmod (subst (cons 1 \"IOIOIOIOI\") (assoc 1 (entget t2)) (entget t2)))");

                double vla = -X(session, "t1");
                double entmod = 100.0 - X(session, "t2");
                Assert.True(vla > 1.0, "the first point moved back by half the words: " + vla);
                Assert.Equal(vla, entmod, 9);
            }
        }

        [Fact]
        public void AJustificationGivenToATextByTheObjectAndByEntmod_PlacesItsFirstPointAlike()
        {
            using (var fonts = new FontFolder())
            {
                CadCommandSession session = WithFont(fonts);
                Feed(session, "(entmake '((0 . \"TEXT\") (8 . \"0\") (10 0.0 20.0 0.0) (40 . 2.5) (1 . \"IOI\")))");
                Feed(session, "(setq t1 (entlast))");
                Feed(session, "(entmake '((0 . \"TEXT\") (8 . \"0\") (10 100.0 20.0 0.0) (40 . 2.5) (1 . \"IOI\")))");
                Feed(session, "(setq t2 (entlast))");

                // AutoCAD 2021 (feed undo, D253; probe g3): the shift of vla-put-Alignment 1 and of entmod (72 . 1) is the same
                Feed(session, "(vla-put-Alignment (vlax-ename->vla-object t1) 1)");
                Feed(session, "(entmod (list (cons -1 t2) '(72 . 1) (list 11 100.0 20.0 0.0)))");

                Assert.Equal(-X(session, "t1"), 100.0 - X(session, "t2"), 9);
                Assert.True(X(session, "t1") < -1.0);
            }
        }

        #endregion

        #region Undo of typed forms

        [Fact]
        public void TwoTypedEntmakes_OneUndoRemovesBoth()
        {
            CadCommandSession session = Loaded();
            Run(session, "REGEN");

            // AutoCAD 2021 (feed undo A): REGEN, two typed entmakes, one U removes both
            MakeLine(session, 100.0);
            MakeLine(session, 110.0);
            Assert.Equal(2, session.Document.EntityCount);
            Run(session, "U");
            Assert.Equal(0, session.Document.EntityCount);
        }

        [Fact]
        public void TwoTypedEntdels_OneUndoRestoresBoth()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 200.0);
            Feed(session, "(setq la (entlast))");
            MakeLine(session, 210.0);
            Feed(session, "(setq lb (entlast))");
            Run(session, "REGEN");

            // AutoCAD 2021 (feed undo B): REGEN, two typed entdels, one U brings back both
            Feed(session, "(entdel la)");
            Feed(session, "(entdel lb)");
            Assert.Equal(0, session.Document.EntityCount);
            Run(session, "U");
            Assert.Equal(2, session.Document.EntityCount);
        }

        [Fact]
        public void TwoTypedSetvars_OneUndoGivesTheValueBeforeBoth()
        {
            CadCommandSession session = Loaded();
            Run(session, "REGEN");

            // AutoCAD 2021 (feed undo C): DIMSCALE 2 then 3 typed, U gives 1.0
            Feed(session, "(setvar \"DIMSCALE\" 2.0)");
            Feed(session, "(setvar \"DIMSCALE\" 3.0)");
            Assert.Equal("3.0", Feed(session, "(getvar \"DIMSCALE\")"));
            Run(session, "U");
            Assert.Equal("1.0", Feed(session, "(getvar \"DIMSCALE\")"));
        }

        [Fact]
        public void ATypedEntmakeThenAnEntdel_AreOneUndo()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 300.0);
            Run(session, "REGEN");

            // AutoCAD 2021 (feed undo D): REGEN, entmake, entdel of it, U puts back both steps: the drawing is as before the REGEN
            MakeLine(session, 320.0);
            Feed(session, "(entdel (entlast))");
            Assert.Equal(1, session.Document.EntityCount);
            Run(session, "U");
            Assert.Equal(1, session.Document.EntityCount);
            Assert.Equal("300.0", Feed(session, "(cadr (cdr (assoc 10 (entget (entlast)))))"));
        }

        [Fact]
        public void ATypedEntmakeThenATypedPut_AreOneUndo()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 400.0);
            Feed(session, "(setq o (vlax-ename->vla-object (entlast)))");
            Run(session, "REGEN");

            // AutoCAD 2021 (feed undo E): REGEN, entmake, (vla-put-Color o 3), one U undoes both
            MakeLine(session, 430.0);
            Feed(session, "(vla-put-Color o 3)");
            Assert.Equal(2, session.Document.EntityCount);
            Assert.Equal("3", Feed(session, "(vla-get-Color o)"));
            Run(session, "U");
            Assert.Equal(1, session.Document.EntityCount);
            Assert.Equal("256", Feed(session, "(vla-get-Color o)"));
        }

        [Fact]
        public void ATypedPutThenATypedEntmod_TheLaterColorWinsAndOneUndoTakesBoth()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 500.0);
            Feed(session, "(setq e (entlast))");
            Feed(session, "(setq o (vlax-ename->vla-object e))");
            Run(session, "REGEN");

            // AutoCAD 2021 (feed undo F): typed put 5 then entmod colour 6 gives 6 (the colour of the later form); U 256
            Feed(session, "(vla-put-Color o 5)");
            Feed(session, "(entmod (append (entget e) '((62 . 6))))");
            Assert.Equal("6", Feed(session, "(vla-get-Color o)"));
            Run(session, "U");
            Assert.Equal("256", Feed(session, "(vla-get-Color o)"));
        }

        [Fact]
        public void ATypedSetvarThenATypedEntmod_AreOneUndo()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 700.0);
            Feed(session, "(setq e (entlast))");
            Feed(session, "(setq o (vlax-ename->vla-object e))");
            Run(session, "REGEN");

            // AutoCAD 2021 (feed undo H): REGEN, setvar DIMSCALE 4, entmod colour 2, U gives (1.0 256)
            Feed(session, "(setvar \"DIMSCALE\" 4.0)");
            Feed(session, "(entmod (append (entget e) '((62 . 2))))");
            Assert.Equal("(4.0 2)", Feed(session, "(list (getvar \"DIMSCALE\") (vla-get-Color o))"));
            Run(session, "U");
            Assert.Equal("(1.0 256)", Feed(session, "(list (getvar \"DIMSCALE\") (vla-get-Color o))"));
        }

        [Fact]
        public void TypedFormsAfterACommand_JoinThatCommandsStep()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 0.0);
            MakeLine(session, 5.0);
            Run(session, "REGEN");

            // AutoCAD 2021 (feed undo G): REGEN, entmake 600, LINE, entmake 620; U 1 takes the LINE and the entmake after it,
            // U 2 the entmake before it, U 3 everything earlier. The REGEN makes no step here, so the entmake after it is a step
            // of its own, as the REGEN's step is in AutoCAD.
            MakeLine(session, 600.0);
            Run(session, "LINE", "0,610", "10,610", Enter);
            MakeLine(session, 620.0);
            Assert.Equal(5, session.Document.EntityCount);
            Run(session, "U");
            Assert.Equal(3, session.Document.EntityCount);
            Run(session, "U");
            Assert.Equal(2, session.Document.EntityCount);
            Run(session, "U");
            Assert.Equal(0, session.Document.EntityCount);
        }

        [Fact]
        public void ATypedPutAfterACommand_JoinsTheStepOfTheCommand()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 0.0);
            Feed(session, "(setq o (vlax-ename->vla-object (entlast)))");
            Run(session, "CIRCLE", "50,50", "5");

            // AutoCAD 2021 (feed undo G, rule 2): a typed change after a command is part of that command's step
            Feed(session, "(vla-put-Color o 3)");
            Run(session, "U");
            Assert.Equal("256", Feed(session, "(vla-get-Color o)"));
            Assert.Empty(session.Document.Database.ModelSpace.OfType<DxfCircle>());
        }

        [Fact]
        public void TypedFormsAfterAnUndo_JoinTheStepThatIsLastAgain()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 0.0);
            Run(session, "REGEN");
            MakeLine(session, 10.0);
            Run(session, "U");
            Assert.Equal(1, session.Document.EntityCount);

            // AutoCAD 2021 (feed undo, sections B to G): the forms typed after a U join the step that is last again, so the
            // next U takes them with it (G's third U took five entities at once)
            MakeLine(session, 20.0);
            Run(session, "U");
            Assert.Equal(0, session.Document.EntityCount);
        }

        #endregion

        #region entmake and entmod, colour and linetype

        [Fact]
        public void AnEntmakeWithNoLinetype_TakesTheDrawingsCeltype()
        {
            CadCommandSession session = Loaded();
            Assert.Equal("\"Continuous\"", Feed(session, "(setvar \"CELTYPE\" \"Continuous\")"));
            MakeLine(session, 0.0);

            // AutoCAD 2021 (feed ft, dcd, undo): a LINE entmade with no group 6 has (6 . "Continuous") where CELTYPE is Continuous
            Assert.Equal("(6 . \"Continuous\")", Feed(session, "(assoc 6 (entget (entlast)))"));
        }

        [Fact]
        public void TheLinetypeAndColourOfAnEntmadeEntity_StandAfterItsLayerInEntget()
        {
            CadCommandSession session = Loaded();
            Feed(session, "(setvar \"CELTYPE\" \"Continuous\")");
            Feed(session, "(entmake '((0 . \"LINE\") (62 . 3) (10 0.0 0.0 0.0) (11 5.0 0.0 0.0)))");

            // AutoCAD 2021 (feed undo, group B): (... (410 . "Model") (8 . "0") (6 . "Continuous") (100 . "AcDbLine") (10 ...)
            // (11 ...) (210 ...)); the colour follows the linetype
            Assert.Equal(
                "(-1 0 330 5 100 67 410 8 6 62 100 10 11 210)",
                Feed(session, "(mapcar 'car (entget (entlast)))")
            );
        }

        [Fact]
        public void AnEntmakeWithNoLinetype_TakesAnotherCeltypeToo_AndNoneWhereItIsByLayer()
        {
            CadCommandSession session = Loaded();
            Feed(session, "(setvar \"CELTYPE\" \"ByLayer\")");
            MakeLine(session, 0.0);
            Assert.Equal("nil", Feed(session, "(assoc 6 (entget (entlast)))"));
            Feed(session, "(setvar \"CELTYPE\" \"ByBlock\")");
            MakeLine(session, 5.0);

            // reference: ByLayer is what an entity has where it has no group 6; ByBlock is written
            Assert.Equal("(6 . \"ByBlock\")", Feed(session, "(assoc 6 (entget (entlast)))"));
        }

        [Fact]
        public void AnExplicitByLayerLinetypeAndColourGivenToEntmake_DoNotAppearInEntget()
        {
            CadCommandSession session = Loaded();

            // AutoCAD 2021 (feed dcd): (6 . "ByLayer") (62 . 256) of an entmade TEXT are dropped, and CELTYPE is not put in
            // their place
            Feed(
                session,
                "(entmake '((0 . \"TEXT\") (8 . \"0\") (6 . \"ByLayer\") (62 . 256) (10 0.0 0.0 0.0) (40 . 2.5) (1 . \"+10\")))"
            );
            Assert.Equal("nil", Feed(session, "(assoc 6 (entget (entlast)))"));
            Assert.Equal("nil", Feed(session, "(assoc 62 (entget (entlast)))"));
            Assert.Equal("\"+10\"", Feed(session, "(cdr (assoc 1 (entget (entlast))))"));
        }

        [Fact]
        public void AnExplicitByLayerGivenToEntmod_TakesTheGroupOutOfTheEntity()
        {
            CadCommandSession session = Loaded();
            Feed(session, "(entmake '((0 . \"LINE\") (8 . \"0\") (62 . 3) (10 0.0 0.0 0.0) (11 5.0 0.0 0.0)))");
            Feed(session, "(setq e (entlast))");
            Assert.Equal("3", Feed(session, "(cdr (assoc 62 (entget e)))"));

            // AutoCAD 2021 (feed dcd): an explicit ByLayer colour or linetype is not kept as a group
            Feed(session, "(entmod (append (entget e) '((62 . 256) (6 . \"ByLayer\"))))");
            Assert.Equal("nil", Feed(session, "(assoc 62 (entget e))"));
            Assert.Equal("nil", Feed(session, "(assoc 6 (entget e))"));
            Assert.Equal("256", Feed(session, "(vla-get-Color (vlax-ename->vla-object e))"));
        }

        [Fact]
        public void AGroupCodeGivenTwiceToEntmod_TheLastReplacesTheFirst()
        {
            CadCommandSession session = Loaded();
            MakeLine(session, 0.0);
            Feed(session, "(setq e (entlast))");

            // AutoCAD 2021 (feed undo F, H; the report): (append (entget e) '((62 . 5) (62 . 6))) gives one group (62 . 6)
            Feed(session, "(entmod (append (entget e) '((62 . 5) (62 . 6))))");
            Assert.Equal("(62 . 6)", Feed(session, "(assoc 62 (entget e))"));
            Assert.Equal("1", Feed(session, "(length (vl-remove-if-not '(lambda (g) (= (car g) 62)) (entget e)))"));
        }

        [Fact]
        public void AnEntgetListWithItsColourAppendedANewOne_GivesTheNewColourOnly()
        {
            CadCommandSession session = Loaded();
            Feed(session, "(entmake '((0 . \"LINE\") (8 . \"0\") (62 . 3) (10 0.0 0.0 0.0) (11 5.0 0.0 0.0)))");
            Feed(session, "(setq e (entlast))");

            // AutoCAD 2021 (feed undo F): entget has (62 . 3); appending (62 . 6) changes it
            Feed(session, "(entmod (append (entget e) '((62 . 6))))");
            Assert.Equal("6", Feed(session, "(cdr (assoc 62 (entget e)))"));
            Assert.Equal("1", Feed(session, "(length (vl-remove-if-not '(lambda (g) (= (car g) 62)) (entget e)))"));
        }

        [Fact]
        public void TheGroupsThatAreSeveralInARecord_StayAsGiven()
        {
            CadCommandSession session = Loaded();
            Feed(
                session,
                "(entmake '((0 . \"LWPOLYLINE\") (8 . \"0\") (90 . 3) (70 . 0) (10 0.0 0.0) (10 5.0 0.0) (10 5.0 5.0)))"
            );
            Feed(session, "(setq e (entlast))");

            // ours: several groups of one code are not collapsed where the entity has several (the vertices)
            Feed(session, "(entmod (append (entget e) '((62 . 2))))");
            Assert.Equal("3", Feed(session, "(length (vl-remove-if-not '(lambda (g) (= (car g) 10)) (entget e)))"));
        }

        #endregion
    }
}
