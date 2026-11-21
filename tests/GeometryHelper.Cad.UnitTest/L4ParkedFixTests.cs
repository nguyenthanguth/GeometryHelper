using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The items PROGRESS.md parked after the L4 acceptance, each typed as the full AutoCAD 2021 took it (feeds te and ft of the
    /// L4 acceptance, probes p1 to p4 of the l2fix2 scratch): the variable NOMUTT, the <c>_</c> and <c>.</c> in front of a typed
    /// command name, the order of the objects <c>ssget</c> gives for a window, a crossing and a polygon (and the view they are
    /// found in), the order -BLOCK keeps its objects in, the options of ZOOM, and the linetype of the objects the drawing
    /// commands make.
    /// </summary>
    public sealed class L4ParkedFixTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Typed(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        // What a form gives, as the lines after it.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        #region NOMUTT

        [Fact]
        public void Nomutt_IsNoughtAtFirst_TakesNoughtAndOne_AndKeepsWhatItWasGiven()
        {
            CadDocument document = Blank();
            CadCommandSession session = Typed(document);

            // AutoCAD 2021 (p3): (getvar "NOMUTT") 0; (setvar "NOMUTT" 1) 1, and 1 after it; 2 and "1" are rejected, 5 typed
            // says "Requires 0 or 1 only."
            Assert.Equal("0", Value(session, "(getvar \"NOMUTT\")"));
            Assert.Equal("1", Value(session, "(setvar \"NOMUTT\" 1)"));
            Assert.Equal("1", Value(session, "(getvar \"NOMUTT\")"));
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"NOMUTT\" 2",
                Value(session, "(setvar \"NOMUTT\" 2)")
            );
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"NOMUTT\" \"1\"",
                Value(session, "(setvar \"NOMUTT\" \"1\")")
            );
            Assert.Equal("1", Value(session, "(getvar \"NOMUTT\")"));

            // The session keeps it: no undo step, and the drawing is as it was.
            Assert.False(document.CanUndo);

            Run(session, "NOMUTT");
            Assert.Equal("Enter new value for NOMUTT <1>:", session.PromptText);
            Run(session, "5");
            Assert.Equal("Requires 0 or 1 only.", session.History[session.History.Count - 1].TrimEnd());
            Run(session, "0");
            Assert.Equal("0", Value(session, "(getvar \"NOMUTT\")"));
        }

        #endregion

        #region The prefixes of a typed command name

        [Theory]
        [InlineData("_REGEN")]
        [InlineData(".REGEN")]
        [InlineData("_.REGEN")]
        [InlineData("._regen")]
        public void ACommandNameTypedWithAnUnderscoreOrAPointInFront_RunsTheCommand(string typed)
        {
            // AutoCAD 2021 (p2): REGEN, _REGEN, .REGEN and _.REGEN each write "Regenerating model."
            CadCommandSession session = Typed(Blank());

            Run(session, typed);

            Assert.Equal("Command: " + typed, session.History[0]);
            Assert.Equal("Regenerating model.", session.History[1]);
            Assert.DoesNotContain(session.History, l => l.StartsWith("Unknown command", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("_L")]
        [InlineData(".L")]
        public void AnAliasTypedWithAPrefix_StartsItsCommand(string typed)
        {
            CadCommandSession session = Typed(Blank());

            Run(session, typed);

            Assert.True(session.IsRunning);
            Assert.Equal("LINE", session.Current.Name);
            Assert.Equal("Specify first point:", session.PromptText);
        }

        [Theory]
        [InlineData("_U")]
        [InlineData(".U")]
        public void UTypedWithAPrefix_UndoesAsUDoes(string typed)
        {
            // AutoCAD 2021 (p2): _U and .U undo the last command, as U does
            CadDocument document = Blank();
            CadCommandSession session = Typed(document, "LINE", "0,0", "10,0", Enter);
            Assert.Single(All<DxfLine>(document.ModelSpace));

            Run(session, typed);

            Assert.Empty(All<DxfLine>(document.ModelSpace));
        }

        [Theory]
        [InlineData("_FOO", "FOO")]
        [InlineData(".FOO", "FOO")]
        [InlineData("_.FOO", "FOO")]
        [InlineData("_.-FOO", "-FOO")]
        [InlineData("-FOO", "-FOO")]
        public void AnUnknownNameTypedWithAPrefix_IsRefusedWithoutIt(string typed, string named)
        {
            // AutoCAD 2021 (p2): _FOO, .FOO and _.FOO say Unknown command "FOO"; _.-FOO and -FOO say "-FOO"
            CadCommandSession session = Typed(Blank());

            Assert.False(session.Start(typed));

            Assert.Equal("Unknown command \"" + named + "\".  Press F1 for help.", session.History.Last().TrimEnd());
        }

        #endregion

        #region ssget: window, crossing, polygon and Previous (probes p1 and p4)

        // Five lines from (0, y) to (20, y) at y 0, 10, 20, 30 and 40, made in that order, and a routine that gives the y of the
        // objects of a set in the order it holds them.
        private static CadCommandSession Lines()
        {
            CadCommandSession session = Typed(Blank());
            session.Feed(
                "(foreach y '(0 10 20 30 40) (entmake (list '(0 . \"LINE\") (list 10 0.0 (float y) 0.0) (list 11 20.0 (float y) 0.0))))"
            );
            session.Feed(
                "(defun gy (ss / i r) (if ss (progn (setq i 0 r nil) (repeat (sslength ss) (setq r (cons (nth 2 (assoc 10 (entget (ssname ss i)))) r) i (1+ i))) (reverse r)) 'NOSET))"
            );
            return session;
        }

        private const string NewestFirst = "(40.0 30.0 20.0 10.0 0.0)";

        [Theory]
        [InlineData("(gy (ssget \"_X\"))")]
        [InlineData("(gy (ssget \"_W\" '(-5.0 -5.0) '(25.0 45.0)))")]
        [InlineData("(gy (ssget \"_C\" '(-5.0 -5.0) '(25.0 45.0)))")]
        [InlineData("(gy (ssget \"_WP\" '((-5.0 -5.0) (25.0 -5.0) (25.0 45.0) (-5.0 45.0))))")]
        [InlineData("(gy (ssget \"_CP\" '((-5.0 -5.0) (25.0 -5.0) (25.0 45.0) (-5.0 45.0))))")]
        public void AWindowACrossingAndAPolygon_GiveTheObjectMadeLastFirst_AsXDoes(string form)
        {
            // AutoCAD 2021 (p1, a view that shows them all): each gives y 40 30 20 10 0
            Assert.Equal(NewestFirst, Value(Lines(), form));
        }

        [Fact]
        public void PreviousAfterAWindow_GivesTheSetInTheOrderItHad()
        {
            CadCommandSession session = Lines();

            // AutoCAD 2021 (p1): (ssget "_W" ...) then (ssget "_P") gives y 40 30 20 10 0; so does P after X
            Value(session, "(ssget \"_W\" '(-5.0 -5.0) '(25.0 45.0))");
            Assert.Equal(NewestFirst, Value(session, "(gy (ssget \"_P\"))"));
            Value(session, "(ssget \"_X\")");
            Assert.Equal(NewestFirst, Value(session, "(gy (ssget \"_P\"))"));
        }

        [Theory]
        [InlineData("W")]
        [InlineData("C")]
        public void AWindowTypedAtTheSelectPrompt_GivesTheObjectMadeLastFirst(string option)
        {
            CadCommandSession session = Lines();

            // AutoCAD 2021 (p1): (setq ss (ssget)) then W (or C), -5,-5, 25,45 and Enter gives y 40 30 20 10 0
            Value(session, "(setq ss (ssget))");
            Run(session, option, "-5,-5", "25,45", Enter);

            Assert.Equal(NewestFirst, Value(session, "(gy ss)"));
        }

        [Fact]
        public void AWindowOfTwoPointsTypedAtTheSelectPrompt_GivesTheObjectMadeLastFirst()
        {
            CadCommandSession session = Lines();

            Value(session, "(setq ss (ssget))");
            Run(session, "-5,-5", "25,45", Enter);

            Assert.Equal(NewestFirst, Value(session, "(gy ss)"));
        }

        [Fact]
        public void PreviousAfterACommandsTypedWindow_GivesTheObjectMadeLastFirst()
        {
            CadCommandSession session = Lines();

            // AutoCAD 2021 (p1): a window of a command makes the same set as ssget's, newest first, and P takes it as it is
            Run(session, "MOVE", "W", "-5,-5", "25,45", Enter, Esc);
            Assert.Equal(NewestFirst, Value(session, "(gy (ssget \"_P\"))"));
        }

        // The session shows a window 942 by 534 pixels, zoomed to (-10, 15) to (30, 25): in AutoCAD VIEWCTR (10 20 0) and VIEWSIZE
        // 22.6567, so that the view runs from y 8.67 to 31.33 and shows the lines at 10, 20 and 30 and not those at 0 and 40.
        private static CadCommandSession LinesSeenThroughAView()
        {
            CadCommandSession session = Lines();
            session.View = new CadView(942, 534);
            Run(session, "ZOOM", P(-10, 15), P(30, 25));
            Assert.Equal(22.68, session.View.Height / session.View.PixelsPerUnit, 2);
            return session;
        }

        [Theory]
        [InlineData("(gy (ssget \"_W\" '(-5.0 -5.0) '(25.0 45.0)))", "(30.0 20.0 10.0)")]
        [InlineData("(gy (ssget \"_C\" '(-5.0 -5.0) '(25.0 45.0)))", "(30.0 20.0 10.0)")]
        [InlineData("(gy (ssget \"_WP\" '((-5.0 -5.0) (25.0 -5.0) (25.0 45.0) (-5.0 45.0))))", "(30.0 20.0 10.0)")]
        [InlineData("(gy (ssget \"_CP\" '((-5.0 -5.0) (25.0 45.0) (25.0 -5.0))))", "(30.0 20.0 10.0 0.0)")]
        [InlineData("(gy (ssget \"_F\" '((5.0 -5.0) (5.0 45.0))))", "(0.0 10.0 20.0 30.0 40.0)")]
        [InlineData("(gy (ssget \"_X\"))", "(40.0 30.0 20.0 10.0 0.0)")]
        public void AWindowACrossingAndAWindowPolygon_FindOnlyWhatTheViewShows_WhereTheSessionHasAView(
            string form,
            string expected
        )
        {
            // AutoCAD 2021 (p1, accoreconsole): with that view W, C and WP gave y 30 20 10; CP (a triangle), F and X were not held
            // to the view: 30 20 10 0 for the triangle, 0 10 20 30 40 for a fence going up, and X all five
            Assert.Equal(expected, Value(LinesSeenThroughAView(), form));
        }

        [Fact]
        public void AWindowTypedAtTheSelectPrompt_FindsOnlyWhatTheViewShows()
        {
            CadCommandSession session = LinesSeenThroughAView();

            // AutoCAD 2021 (p1): the typed window of the same view found 3 objects, y 30 20 10
            Value(session, "(setq ss (ssget))");
            Run(session, "W", "-5,-5", "25,45", Enter);

            Assert.Equal("(30.0 20.0 10.0)", Value(session, "(gy ss)"));
        }

        [Fact]
        public void AnObjectOnlyPartlyInTheView_IsFoundByAWindowThatHoldsIt()
        {
            CadCommandSession session = LinesSeenThroughAView();
            session.Feed("(entmake '((0 . \"LINE\") (10 5.0 0.0 0.0) (11 5.0 20.0 0.0)))");
            session.Feed("(entmake '((0 . \"LINE\") (10 40.0 20.0 0.0) (11 50.0 20.0 0.0)))");

            // AutoCAD 2021 (p4): a line from (5, 0) to (5, 20) across the edge of the view is found; one from (40, 20) to
            // (50, 20), wholly outside it, is not, though the window holds it
            Assert.Equal(
                "1",
                Value(session, "(sslength (ssget \"_W\" '(-100.0 -100.0) '(100.0 100.0) '((10 5.0 0.0 0.0))))")
            );
            Assert.Equal("nil", Value(session, "(ssget \"_W\" '(-100.0 -100.0) '(100.0 100.0) '((10 40.0 20.0 0.0)))"));
            Assert.Equal("4", Value(session, "(sslength (ssget \"_W\" '(-100.0 -100.0) '(100.0 100.0)))"));
        }

        #endregion

        #region -BLOCK keeps the order of the selection (probe p2)

        private static CadCommandSession ThreeAttributeDefinitions()
        {
            CadCommandSession session = Typed(Blank());

            for (int i = 1; i <= 3; i++)
            {
                string n = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                session.Feed(
                    "(command \"_.-ATTDEF\" \"\" \"A"
                        + n
                        + "\" \"P"
                        + n
                        + "\" \"d"
                        + n
                        + "\" \"0,"
                        + (3 * i)
                        + "\" \"2\" \"0\")"
                );
            }

            return session;
        }

        private static string[] TagsOf(DxfBlockTableRecord block) =>
            block.OfType<DxfAttributeDefinition>().Select(a => a.Tag).ToArray();

        [Fact]
        public void BlockTyped_PutsItsObjectsInTheOrderOfTheSelection_NotTheOrderOfTheDrawing()
        {
            CadCommandSession session = ThreeAttributeDefinitions();

            // AutoCAD 2021 (p2): three ATTDEFs A1, A2 and A3, then -BLOCK TB 0,0 (ssget "_X") "": the block holds them as A3, A2, A1,
            // the order of the set, and -INSERT asks P3, P2 and P1 in that order
            session.Feed("(command \"_.-BLOCK\" \"TB\" \"0,0\" (ssget \"_X\") \"\")");

            Assert.Equal(new[] { "A3", "A2", "A1" }, TagsOf(session.Document.Database.BlockTable["TB"]));

            session.Feed("(command \"_.-INSERT\" \"TB\" \"20,20\" \"1\" \"1\" \"0\" \"v1\" \"v2\" \"v3\")");

            var insert = Assert.Single(All<DxfBlockReference>(session.Document.ModelSpace));
            Assert.Equal(new[] { "A3", "A2", "A1" }, insert.AttributeCollection.Select(a => a.Tag).ToArray());
            Assert.Equal(new[] { "v1", "v2", "v3" }, insert.AttributeCollection.Select(a => a.TextString).ToArray());
            Assert.Contains("P3 <d3>: v1", string.Join("\n", session.History));
        }

        [Fact]
        public void BlockTyped_WithAWindowTyped_PutsItsObjectsNewestFirst()
        {
            CadCommandSession session = ThreeAttributeDefinitions();

            // AutoCAD 2021 (p2): -BLOCK TB2 0,0 _W -5,-5 15,45 "" took the three in the order A3, A2, A1 too
            Run(session, "_.-BLOCK", "TB2", "0,0", "_W", "-5,-5", "15,45", Enter);

            Assert.Equal(new[] { "A3", "A2", "A1" }, TagsOf(session.Document.Database.BlockTable["TB2"]));
        }

        [Fact]
        public void BlockTyped_WithObjectsPickedOneByOne_KeepsTheOrderTheyWerePickedIn()
        {
            CadCommandSession session = ThreeAttributeDefinitions();
            string[] handles = All<DxfAttributeDefinition>(session.Document.ModelSpace).Select(a => a.Handle).ToArray();

            Run(
                session,
                "-BLOCK",
                "TB3",
                "0,0",
                new[] { handles[1] },
                new[] { handles[2] },
                new[] { handles[0] },
                Enter
            );

            Assert.Equal(new[] { "A2", "A3", "A1" }, TagsOf(session.Document.Database.BlockTable["TB3"]));
        }

        #endregion

        #region ZOOM Object, Center, Scale and Dynamic (probe p2)

        private const string ZoomPrompt =
            "Specify corner of window, enter a scale factor (nX or nXP) or [All/Center/Dynamic/Extents/Previous/Scale/Window/Object] <real time>:";

        private static double ViewHeight(CadCommandSession session) => session.View.Height / session.View.PixelsPerUnit;

        private static CadCommandSession ViewedLines()
        {
            CadCommandSession session = Lines();
            session.View = new CadView(942, 534);
            return session;
        }

        [Fact]
        public void ZoomAsksItsOptionsAsAutoCadDoes()
        {
            CadCommandSession session = ViewedLines();

            // AutoCAD 2021 (p2): Specify corner of window, enter a scale factor (nX or nXP), or
            // [All/Center/Dynamic/Extents/Previous/Scale/Window/Object] <real time>:
            Run(session, "ZOOM");

            Assert.Equal(ZoomPrompt, session.PromptText);
        }

        [Fact]
        public void ZoomObject_ShowsTheObjectsSelected_AsAWhole()
        {
            CadCommandSession session = ViewedLines();

            // AutoCAD 2021 (p2): ZOOM Object of the line from (0, 40) to (20, 40) gave VIEWCTR (10 40 0); of all five lines VIEWCTR
            // (10 20 0), their box 20 by 40 shown, as ZOOM Extents shows it
            Run(session, "ZOOM", "O");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { LastLine(session) }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(10.0, session.View.CenterX, 6);
            Assert.Equal(40.0, session.View.CenterY, 6);
            Assert.True(session.View.VisibleBox.Contains(new DisplayBox(0, 40, 20, 40)));
            Assert.True(session.View.VisibleBox.Width < 40.0);

            Run(session, "ZOOM", "O", "ALL", Enter);

            Assert.Equal(10.0, session.View.CenterX, 6);
            Assert.Equal(20.0, session.View.CenterY, 6);
            Assert.True(session.View.VisibleBox.Contains(new DisplayBox(0, 0, 20, 40)));
        }

        private static string LastLine(CadCommandSession session) =>
            All<DxfLine>(session.Document.ModelSpace).Last().Handle;

        [Fact]
        public void ZoomObjectWithNothingSelected_LeavesTheViewAsItWas()
        {
            CadCommandSession session = ViewedLines();
            Run(session, "ZOOM", P(-10, -10), P(30, 50));
            double height = ViewHeight(session);

            // AutoCAD 2021 (p2): ZOOM Object, then Enter, ends with VIEWCTR and VIEWSIZE as they were
            Run(session, "ZOOM", "O", Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(10.0, session.View.CenterX, 6);
            Assert.Equal(20.0, session.View.CenterY, 6);
            Assert.Equal(height, ViewHeight(session), 6);
        }

        [Fact]
        public void ZoomCenter_AsksTheCentreAndThenAHeightOrAMagnification()
        {
            CadCommandSession session = ViewedLines();
            Run(session, "ZOOM", P(-10, -10), P(30, 50));

            // AutoCAD 2021 (p2): ZOOM Center 50,60 asks "Specify center point:" and "Enter magnification or height <40.4519>:" (the
            // height of the view now); 30 gave VIEWCTR (50 60 0) and VIEWSIZE 30, 3X then VIEWSIZE 10, Enter left it
            Run(session, "ZOOM", "C");
            Assert.Equal("Specify center point:", session.PromptText);
            Run(session, "50,60");
            Assert.Equal(
                "Enter magnification or height <" + ViewHeight(session).ToString("0.0000") + ">:",
                session.PromptText
            );
            Run(session, "30");

            Assert.Equal(50.0, session.View.CenterX, 6);
            Assert.Equal(60.0, session.View.CenterY, 6);
            Assert.Equal(30.0, ViewHeight(session), 6);

            Run(session, "ZOOM", "C", "50,60");
            Assert.Equal("Enter magnification or height <30.0000>:", session.PromptText);
            Run(session, "3X");
            Assert.Equal(10.0, ViewHeight(session), 6);

            Run(session, "ZOOM", "C", "50,60", Enter);
            Assert.Equal(10.0, ViewHeight(session), 6);
            Assert.Equal(60.0, session.View.CenterY, 6);
        }

        [Fact]
        public void ZoomScale_AsksTheFactorAndTakesItAsTypedAtTheFirstPromptIs()
        {
            CadCommandSession session = ViewedLines();
            Run(session, "ZOOM", P(0, 0), P(20, 10));
            double height = ViewHeight(session);

            // AutoCAD 2021 (p2): ZOOM Scale asks "Enter a scale factor (nX or nXP):"; 0.5X doubled VIEWSIZE (4.5 to 9)
            Run(session, "ZOOM", "S");
            Assert.Equal("Enter a scale factor (nX or nXP):", session.PromptText);
            Run(session, "0.5X");

            Assert.False(session.IsRunning);
            Assert.Equal(2.0 * height, ViewHeight(session), 6);
        }

        #endregion

        #region The linetype of the objects the commands make (probe p3)

        [Theory]
        [InlineData("(command \"_.LINE\" \"0,0\" \"10,0\" \"\")")]
        [InlineData("(command \"_.CIRCLE\" \"0,0\" \"5\")")]
        [InlineData("(command \"_.ARC\" \"0,0\" \"5,5\" \"10,0\")")]
        [InlineData("(command \"_.PLINE\" \"0,0\" \"10,0\" \"\")")]
        [InlineData("(command \"_.RECTANG\" \"0,0\" \"10,10\")")]
        [InlineData("(command \"_.POINT\" \"3,3\")")]
        [InlineData("(command \"_.TEXT\" \"0,0\" \"2\" \"0\" \"x\")")]
        [InlineData("(command \"_.ELLIPSE\" \"0,0\" \"10,0\" \"3\")")]
        [InlineData("(command \"_.DONUT\" \"1\" \"2\" \"5,5\" \"\")")]
        [InlineData("(command \"_.XLINE\" \"0,0\" \"10,10\" \"\")")]
        [InlineData("(command \"_.SPLINE\" \"0,0\" \"5,5\" \"10,0\" \"\" \"\" \"\")")]
        [InlineData("(command \"_.-ATTDEF\" \"\" \"A1\" \"P1\" \"d1\" \"0,3\" \"2\" \"0\")")]
        public void ADrawingCommand_GivesItsObjectTheCeltypeOfTheDrawing_AndEntgetShowsIt(string form)
        {
            // AutoCAD 2021 (p3): with CELTYPE ByBlock every one of these made its object with (6 . "ByBlock"), with Continuous
            // (6 . "Continuous"), and with ByLayer none; the colour 62 followed CECOLOR the same way
            foreach (string celtype in new[] { "ByBlock", "Continuous" })
            {
                CadCommandSession session = Typed(Blank());
                Value(session, "(setvar \"CELTYPE\" \"" + celtype + "\")");
                session.Feed(form);

                Assert.Equal("(6 . \"" + celtype + "\")", Value(session, "(assoc 6 (entget (entlast)))"));
            }

            CadCommandSession byLayer = Typed(Blank());
            Value(byLayer, "(setvar \"CELTYPE\" \"ByLayer\")");
            byLayer.Feed(form);

            Assert.Equal("nil", Value(byLayer, "(assoc 6 (entget (entlast)))"));
        }

        [Fact]
        public void ABlockInsertedAndACopy_TakeTheCeltypeToo()
        {
            CadCommandSession session = Typed(Blank());
            session.Feed("(command \"_.-ATTDEF\" \"\" \"A1\" \"P1\" \"d1\" \"0,3\" \"2\" \"0\")");
            session.Feed("(command \"_.-BLOCK\" \"TB\" \"0,0\" (entlast) \"\")");
            Value(session, "(setvar \"CELTYPE\" \"ByBlock\")");

            // AutoCAD 2021 (p3): -INSERT of a block gave the INSERT (6 . "DASHED") with CELTYPE DASHED, and COPY of it too
            session.Feed("(command \"_.-INSERT\" \"TB\" \"20,20\" \"1\" \"1\" \"0\" \"v1\")");
            Assert.Equal("(6 . \"ByBlock\")", Value(session, "(assoc 6 (entget (entlast)))"));

            session.Feed("(command \"_.COPY\" (entlast) \"\" \"0,0\" \"5,5\")");
            Assert.Equal("(6 . \"ByBlock\")", Value(session, "(assoc 6 (entget (entlast)))"));
        }

        #endregion

        #region nentselp on a dimension (feed e1 of the L5 acceptance)

        [Theory]
        [InlineData("(115 95 0)", "(4 \"LINE\" (\"DIMENSION\"))")]
        [InlineData("(115 95.3 0)", "(4 \"LINE\" (\"DIMENSION\"))")]
        [InlineData("(115 96 0)", "(4 \"LINE\" (\"DIMENSION\"))")]
        [InlineData("(100 87 0)", "(4 \"LINE\" (\"DIMENSION\"))")]
        public void NentselpOnADimension_GivesAPrimitiveOfItsBlockAndTheDimensionAsItsContainer(
            string point,
            string expected
        )
        {
            CadCommandSession session = Typed(Blank());
            // The full AutoCAD ran with PICKBOX 25 pixels at 4.1 pixels to the unit (ZOOM Window 90,-20 150,110): about 6 units.
            session.PickAperture = 6.0;
            session.Feed("(command \"_.DIMLINEAR\" \"100,80\" \"130,80\" \"115,95\")");

            // AutoCAD 2021 (feed e1, full AutoCAD): a DIMLINEAR from (100, 80) to (130, 80) with its line through (115, 95), picked by
            // nentselp at (115 95 0), (115 95.3 0), (115 96 0) and, on an extension line, (100 87 0) gave a list of four,
            // (ename-of-a-LINE pt matrix (dimension-ename))
            string form =
                "(progn (setq r (nentselp '"
                + point
                + ")) (list (length r) (cdr (assoc 0 (entget (car r)))) (mapcar '(lambda (e) (cdr (assoc 0 (entget e)))) (cadddr r))))";
            Assert.Equal(expected, Value(session, form));
            Assert.Equal("((1.0 0.0 0.0) (0.0 1.0 0.0) (0.0 0.0 1.0) (0.0 0.0 0.0))", Value(session, "(caddr r)"));
            Assert.Equal("T", Value(session, "(equal (cadr r) (mapcar (quote float) (quote " + point + ")))"));
        }

        #endregion

        #region ssgetfirst keeps the order of the set given to sssetfirst (feed fdval-full)

        [Fact]
        public void Ssgetfirst_GivesTheSetInTheOrderSssetfirstWasGiven_NotTheOrderOfTheDrawing()
        {
            CadCommandSession session = Typed(Blank());
            session.Feed("(setq e0 (progn (entmake '((0 . \"LINE\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0))) (entlast)))");
            session.Feed(
                "(setq e150 (progn (entmake '((0 . \"LINE\") (10 150.0 0.0 0.0) (11 160.0 0.0 0.0))) (entlast)))"
            );
            session.Feed("(setq s2 (ssadd e150 (ssadd)))");
            session.Feed("(ssadd e0 s2)");

            // AutoCAD 2021 (feed fdval-full): (sssetfirst nil s2) with s2 built from the entity at 150 and then the one at 0, then
            // (cadr (ssgetfirst)) holds them as (150 0), the order of the set
            session.Feed("(sssetfirst nil s2)");
            const string xs = "(mapcar '(lambda (i) (cadr (assoc 10 (entget (ssname (cadr (ssgetfirst)) i))))) '(0 1))";
            Assert.Equal("(150.0 0.0)", Value(session, xs));

            // A selection changed since is given in the order of the drawing again.
            session.Document.Selection.Set(new[] { All<DxfLine>(session.Document.ModelSpace).First().Handle });
            Assert.Equal("(0.0)", Value(session, xs.Replace("'(0 1)", "'(0)")));
        }

        #endregion

        #region entmake of an INSERT takes the linetype and colour groups (feed ft)

        [Fact]
        public void AnEntmadeInsert_TakesTheCeltypeAndTheColourItIsGiven()
        {
            CadCommandSession session = Typed(Blank());
            session.Feed("(entmake '((0 . \"LINE\") (8 . \"0\") (10 0.0 0.0 0.0) (11 10.0 0.0 0.0)))");
            session.Feed("(command \"_.-BLOCK\" \"BLKA\" \"0,0\" (entlast) \"\")");
            Value(session, "(setvar \"CELTYPE\" \"Continuous\")");

            // AutoCAD 2021 (feed ft): an INSERT entmade with no group 6 has (6 . "Continuous") where CELTYPE is Continuous, and one
            // given (62 . 5) keeps it
            session.Feed("(entmake '((0 . \"INSERT\") (8 . \"0\") (2 . \"BLKA\") (10 50.0 0.0 0.0)))");
            Assert.Equal("(6 . \"Continuous\")", Value(session, "(assoc 6 (entget (entlast)))"));
            Assert.Equal("nil", Value(session, "(assoc 62 (entget (entlast)))"));

            session.Feed(
                "(entmake '((0 . \"INSERT\") (8 . \"0\") (62 . 5) (6 . \"ByBlock\") (2 . \"BLKA\") (10 70.0 0.0 0.0)))"
            );
            Assert.Equal("(6 . \"ByBlock\")", Value(session, "(assoc 6 (entget (entlast)))"));
            Assert.Equal("(62 . 5)", Value(session, "(assoc 62 (entget (entlast)))"));
        }

        #endregion
    }
}
