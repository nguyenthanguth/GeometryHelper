using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The four items PROGRESS.md parked after the L2 wave, each typed as AutoCAD 2021's console took it (probes p1 to p7 of the
    /// l2fix scratch): the major axis of ELLIPSE, the name lists of -LAYER with the back quote of a wild-card pattern, the
    /// variables ERRNO, PROGRAM, PLATFORM, VIEWCTR and UNDOCTL, and the prompts of -STYLE.
    /// </summary>
    public sealed class L2ParkedFixTests : IDisposable
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

        private static DxfEllipse Last(CadDocument document) => All<DxfEllipse>(document.ModelSpace).Last();

        private static void Same(GeoVector3 expected, GeoVector3 actual)
        {
            Assert.Equal(expected.X, actual.X, 9);
            Assert.Equal(expected.Y, actual.Y, 9);
            Assert.Equal(expected.Z, actual.Z, 9);
        }

        #region ELLIPSE (probes p1 and p7)

        [Fact]
        public void EllipseByTheEndsOfAnAxis_TakesItsMajorAxisFromTheCentreToTheFirstEnd()
        {
            // AutoCAD 2021 (p1): ELLIPSE 50,0 60,0 3 gives group 11 (-5 0 0); the ends the other way round, (5 0 0).
            CadDocument document = Blank();

            Typed(document, "ELLIPSE", P(50, 0), P(60, 0), "3");
            Same(new GeoVector3(-5, 0, 0), Last(document).MajorAxis);
            Assert.Equal(0.6, Last(document).RadiusRatio, 9);

            Typed(document, "ELLIPSE", P(60, 0), P(50, 0), "3");
            Same(new GeoVector3(5, 0, 0), Last(document).MajorAxis);

            Typed(document, "ELLIPSE", P(50, 0), P(50, 10), "3");
            Same(new GeoVector3(0, -5, 0), Last(document).MajorAxis);
        }

        [Fact]
        public void EllipseByTheEndsOfAnAxis_WhereTheOtherAxisIsLonger_TurnsTheMajorAxisAQuarterTurnFromTheFirstEnd()
        {
            // AutoCAD 2021 (p1): ELLIPSE 50,0 52,0 20 gives (11 0 -20 0) and ratio 0.05; the ends the other way round, (0 20 0).
            CadDocument document = Blank();

            Typed(document, "ELLIPSE", P(50, 0), P(52, 0), "20");
            Same(new GeoVector3(0, -20, 0), Last(document).MajorAxis);
            Assert.Equal(0.05, Last(document).RadiusRatio, 9);

            Typed(document, "ELLIPSE", P(52, 0), P(50, 0), "20");
            Same(new GeoVector3(0, 20, 0), Last(document).MajorAxis);
        }

        [Fact]
        public void EllipseArc_ByTheEndsOfAnAxis_RunsFromTheFirstEnd_WithAutoCadsParameters()
        {
            // AutoCAD 2021 (a4 of the L4 probes, p1): ELLIPSE A 50,0 60,0 3 0 90 has parameters 0 to 1.5708 and ends at (55 -3 0).
            CadDocument document = Blank();

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "0", "90");

            DxfEllipse arc = Last(document);
            Same(new GeoVector3(-5, 0, 0), arc.MajorAxis);
            Assert.Equal(0.0, arc.StartParam, 4);
            Assert.Equal(1.5708, arc.EndParam, 4);
            Near(new GeoPoint3(50, 0, 0), arc.StartPoint, 1E-6);
            Near(new GeoPoint3(55, -3, 0), arc.EndPoint, 1E-6);
        }

        [Fact]
        public void EllipseArc_AnglesOffTheAxes_AreTurnedIntoParametersOfTheMajorAxisEllipse()
        {
            // AutoCAD 2021 (p1, p7): angles 45 and 135 give the parameters 1.03038 and 2.11122; -45 and 45 give 5.25281 and 7.31356
            // (the end past the start); 200 and 300, 3.68686 and 5.04586; 0 and 0, a whole ellipse of 0 to 6.28319.
            CadDocument document = Blank();

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "45", "135");
            Assert.Equal(1.03038, Last(document).StartParam, 4);
            Assert.Equal(2.11122, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "-45", "45");
            Assert.Equal(5.25281, Last(document).StartParam, 4);
            Assert.Equal(7.31356, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "200", "300");
            Assert.Equal(3.68686, Last(document).StartParam, 4);
            Assert.Equal(5.04586, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "0", "0");
            Assert.Equal(0.0, Last(document).StartParam, 4);
            Assert.Equal(6.28319, Last(document).EndParam, 4);
            Assert.True(Last(document).Closed);
        }

        [Fact]
        public void EllipseArc_WithAnIncludedAngle_EndsThatFarCounterClockwiseFromTheStart()
        {
            // AutoCAD 2021 (p1, p7): start 30 and included 60 give 0.766163 to 1.5708; start 100 and included -30 give 1.6762 to
            // 7.63898, the end angle 70 reached counter-clockwise.
            CadDocument document = Blank();

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "30", "I", "60");
            Assert.Equal(0.766163, Last(document).StartParam, 4);
            Assert.Equal(1.5708, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "3", "100", "I", "-30");
            Assert.Equal(1.6762, Last(document).StartParam, 4);
            Assert.Equal(7.63898, Last(document).EndParam, 4);
        }

        [Fact]
        public void EllipseArc_WhereTheOtherAxisIsLonger_MeasuresItsAnglesFromTheTurnedMajorAxis()
        {
            // AutoCAD 2021 (p7): ELLIPSE A 50,0 52,0 20 0 90 has the major axis (0 -20 0) and the parameters 0 to 1.5708, its start
            // at (51 -20 0); by its centre, 10,10 and 12,10 with 5, the major axis (0 5 0) and the start at (10 15 0).
            CadDocument document = Blank();

            Typed(document, "ELLIPSE", "A", P(50, 0), P(52, 0), "20", "0", "90");
            Same(new GeoVector3(0, -20, 0), Last(document).MajorAxis);
            Assert.Equal(0.0, Last(document).StartParam, 4);
            Assert.Equal(1.5708, Last(document).EndParam, 4);
            Near(new GeoPoint3(51, -20, 0), Last(document).StartPoint, 1E-6);

            Typed(document, "ELLIPSE", "A", "C", P(10, 10), P(12, 10), "5", "0", "90");
            Same(new GeoVector3(0, 5, 0), Last(document).MajorAxis);
            Near(new GeoPoint3(10, 15, 0), Last(document).StartPoint, 1E-6);
            Near(new GeoPoint3(8, 10, 0), Last(document).EndPoint, 1E-6);
        }

        [Fact]
        public void EllipseByItsCentre_TakesTheEndGivenAsTheMajorAxis_AndTheRotationItsRatio()
        {
            // AutoCAD 2021 (p1): ELLIPSE C 0,0 0,10 4 gives (11 0 10 0), ratio 0.4; with an arc 45 to 135, the parameters 1.19029
            // to 1.9513; ELLIPSE A 50,0 60,0 R 60 gives (11 -5 0 0) and ratio 0.5.
            CadDocument document = Blank();

            Typed(document, "ELLIPSE", "C", P(0, 0), P(0, 10), "4");
            Same(new GeoVector3(0, 10, 0), Last(document).MajorAxis);

            Typed(document, "ELLIPSE", "A", "C", P(0, 0), P(0, 10), "4", "45", "135");
            Assert.Equal(1.19029, Last(document).StartParam, 4);
            Assert.Equal(1.9513, Last(document).EndParam, 4);

            Typed(document, "ELLIPSE", "A", P(50, 0), P(60, 0), "R", "60", "0", "90");
            Same(new GeoVector3(-5, 0, 0), Last(document).MajorAxis);
            Assert.Equal(0.5, Last(document).RadiusRatio, 9);
        }

        #endregion

        #region -LAYER name lists (probes p2 and p4)

        private static CadDocument TwoNamedLayers()
        {
            CadDocument document = Plan();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Pl#1.b@[2]~", ColorIndex = 7 });
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Plank", ColorIndex = 7 });
            return document;
        }

        [Fact]
        public void LayerNameLists_TakeAPatternWithTheBackQuoteOfAnEscape_ForEveryOptionThatAsksOne()
        {
            // AutoCAD 2021 (p2): "Pl`#1`.b`@`[2]`~" is the layer Pl#1.b@[2]~ in Color, Ltype, LWeight, Freeze, Thaw, OFF, ON, LOck and
            // Unlock; the L3 feed ft wrote Cannot find layer "Pl`#1`.b`@`[2]`~" and left it as it was.
            CadDocument document = TwoNamedLayers();
            DxfLayerTableRecord named = document.Database.LayerTable["Pl#1.b@[2]~"];
            const string escaped = "Pl`#1`.b`@`[2]`~";
            CadCommandSession session = Typed(document, "-LAYER", "COLOR", "3", escaped, Enter);
            Assert.Equal(3, named.ColorIndex);
            Assert.Equal(7, document.Database.LayerTable["Plank"].ColorIndex);

            Run(session, "-LAYER", "LTYPE", "HIDDEN", escaped, Enter);
            Assert.Equal("HIDDEN", named.LinetypeName);

            Run(session, "-LAYER", "LWEIGHT", "0.30", escaped, Enter);
            Assert.Equal(DxfLineWeight.LineWeight030, named.LineWeight);

            Run(session, "-LAYER", "FREEZE", escaped, Enter);
            Assert.True(named.IsFrozen);
            Run(session, "-LAYER", "THAW", escaped, Enter);
            Assert.False(named.IsFrozen);

            Run(session, "-LAYER", "OFF", escaped, Enter);
            Assert.True(named.IsOff);
            Run(session, "-LAYER", "ON", escaped, Enter);
            Assert.False(named.IsOff);

            Run(session, "-LAYER", "LOCK", escaped, Enter);
            Assert.True(named.IsLocked);
            Run(session, "-LAYER", "UNLOCK", escaped, Enter);
            Assert.False(named.IsLocked);
            Assert.DoesNotContain(session.History, l => l.StartsWith("Cannot find layer", StringComparison.Ordinal));
            Assert.DoesNotContain(session.History, l =>
                l.StartsWith("No matching layer names", StringComparison.Ordinal)
            );
            Assert.False(document.Database.LayerTable["Plank"].IsLocked);
        }

        [Fact]
        public void ALayerNameTypedAsItIs_WithTheCharactersOfAPattern_IsAPatternThatMatchesNothing()
        {
            // AutoCAD 2021 (p2): FREEZE Pl#1.b@[2]~ writes "No matching layer names found." and leaves the layer; so does COLOR 6 P?#1*.
            CadDocument document = TwoNamedLayers();
            DxfLayerTableRecord named = document.Database.LayerTable["Pl#1.b@[2]~"];

            CadCommandSession session = Typed(document, "-LAYER", "FREEZE", "Pl#1.b@[2]~", Enter);
            Assert.Equal("No matching layer names found.", session.History.Last(l => l.Length > 0 && l[0] == 'N'));
            Assert.False(named.IsFrozen);

            int before = session.History.Count;
            Run(session, "-LAYER", "COLOR", "6", "P?#1*", Enter);
            Assert.Contains("No matching layer names found.", session.History.Skip(before));
            Assert.Equal(7, named.ColorIndex);
        }

        [Fact]
        public void ANameListThatMatchesNoLayer_SaysSoOnce_AndOneThatMatchesSomeSaysNothingOfTheRest()
        {
            // AutoCAD 2021 (p4): FREEZE nosuch writes "No matching layer names found."; FREEZE Plank,nosuch freezes Plank and says
            // nothing; PLANK,plank is Plank once. The presenter wrote Cannot find layer "nosuch".
            CadDocument document = TwoNamedLayers();

            CadCommandSession session = Typed(document, "-LAYER", "FREEZE", "nosuch", Enter);
            Assert.Contains("No matching layer names found.", session.History);
            Assert.DoesNotContain(session.History, l => l.StartsWith("Cannot find layer", StringComparison.Ordinal));

            int before = session.History.Count;
            Run(session, "-LAYER", "FREEZE", "Plank,nosuch", Enter);
            Assert.True(document.Database.LayerTable["Plank"].IsFrozen);
            Assert.DoesNotContain(session.History.Skip(before), l =>
                l.StartsWith("Cannot find", StringComparison.Ordinal)
            );
            Assert.DoesNotContain("No matching layer names found.", session.History.Skip(before));

            Run(session, "-LAYER", "THAW", "nosuch,Plank", Enter);
            Assert.False(document.Database.LayerTable["Plank"].IsFrozen);
        }

        [Fact]
        public void ColorOfTheCurrentLayerByEnter_TakesTheNameAsItIs_EvenWhereItHoldsTheCharactersOfAPattern()
        {
            // The default of a name-list prompt is the current layer, a name and not a pattern.
            CadDocument document = TwoNamedLayers();
            document.Database.Clayer = "Pl#1.b@[2]~";

            CadCommandSession session = Typed(document, "-LAYER", "COLOR", "4", Enter, Enter);
            Assert.Equal(4, document.Database.LayerTable["Pl#1.b@[2]~"].ColorIndex);
            Assert.False(session.IsRunning);
            Assert.DoesNotContain("No matching layer names found.", session.History);
        }

        [Fact]
        public void LayerOnAndOff_AskForTheirNamesInLowerCase()
        {
            // AutoCAD 2021 (p2): "Enter name list of layer(s) to turn on:" and "... to turn off or <select objects>:".
            CadCommandSession session = Typed(TwoNamedLayers(), "-LAYER", "ON");
            Assert.Equal("Enter name list of layer(s) to turn on:", session.PromptText);

            Run(session, Enter, "OFF");
            Assert.Equal("Enter name list of layer(s) to turn off or <select objects>:", session.PromptText);
        }

        #endregion

        #region Variables (probes q6 of L2 and p5)

        [Fact]
        public void ErrnoIsAWholeNumberAroutineMaySet_AsAutoCadKeepsIt()
        {
            // AutoCAD 2021 (p5, q6): ERRNO 0 set gives 0, 7 set gives 7 (also quoted as 'errno), -5 is taken; 40000, 2.5 and "x" are
            // refused with: AutoCAD variable setting rejected. The L3 feed ft wrote that for (setvar 'errno 0).
            CadCommandSession session = Typed(Blank());

            Assert.Equal("0", Value(session, "(getvar \"ERRNO\")"));
            Assert.Equal("0", Value(session, "(setvar \"ERRNO\" 0)"));
            Assert.Equal("7", Value(session, "(setvar 'errno 7)"));
            Assert.Equal("7", Value(session, "(getvar \"ERRNO\")"));
            Assert.Equal("-5", Value(session, "(setvar \"ERRNO\" -5)"));
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"ERRNO\" 40000",
                Value(session, "(setvar \"ERRNO\" 40000)")
            );
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"ERRNO\" 2.5",
                Value(session, "(setvar \"ERRNO\" 2.5)")
            );
            Assert.Equal("-5", Value(session, "(getvar \"ERRNO\")"));
        }

        [Fact]
        public void ProgramAndPlatform_ReadAsAutoCadGivesThem_AndAreRefusedWhenSet()
        {
            // AutoCAD 2021 (q6, p5): "acad" and "Microsoft Windows NT Version 6.2 (x64)", both read-only; PRODUCT is the variable
            // that tells the presenter from AutoCAD, so PROGRAM stays what a routine that tests it expects.
            CadCommandSession session = Typed(Blank());

            Assert.Equal("\"acad\"", Value(session, "(getvar \"PROGRAM\")"));
            Assert.Equal("\"Microsoft Windows NT Version 6.2 (x64)\"", Value(session, "(getvar \"PLATFORM\")"));
            Assert.Equal("\"GeometryHelper.Cad\"", Value(session, "(getvar \"PRODUCT\")"));
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"PROGRAM\" \"x\"",
                Value(session, "(setvar \"PROGRAM\" \"x\")")
            );
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"PLATFORM\" \"x\"",
                Value(session, "(setvar \"PLATFORM\" \"x\")")
            );
        }

        [Fact]
        public void Viewctr_IsTheCentreOfTheViewInTheUserCoordinateSystem_AndRefusedWhenSet()
        {
            // AutoCAD 2021 (p5): ZOOM Window 0,0 20,10 gives VIEWCTR (10 5 0); a UCS with its origin at 10,10 moves it to (0 -5 0), as
            // p5's ZOOM Center 100,50 gave (100 50 0) and (90 40 0) after UCS Origin 10,10.
            CadDocument document = Blank();
            CadCommandSession session = Typed(document);
            session.View = new CadView(1000, 500);

            Run(session, "ZOOM", P(0, 0), P(20, 10));
            Assert.Equal("(10.0 5.0 0.0)", Value(session, "(getvar \"VIEWCTR\")"));

            Run(session, "UCS", P(10, 10), Enter);
            Assert.Equal("(0.0 -5.0 0.0)", Value(session, "(getvar \"VIEWCTR\")"));
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"VIEWCTR\" (1 2 0)",
                Value(session, "(setvar \"VIEWCTR\" '(1 2 0))")
            );
        }

        [Fact]
        public void Undoctl_AddsTheBitsOfUndosSettings_AsAutoCadDoes()
        {
            // AutoCAD 2021 (p5): All and Auto 5 at Combine and Layer No; 21 with Combine; 53 with Layer; Auto off 49; One 51; a group
            // open 61; None 0. The presenter starts with Combine and Layer Yes (the window's), so 53.
            CadCommandSession session = Typed(Blank());

            Assert.Equal("53", Value(session, "(getvar \"UNDOCTL\")"));

            Run(session, "UNDO", "BE");
            Assert.Equal("61", Value(session, "(getvar \"UNDOCTL\")"));
            Run(session, "UNDO", "E");
            Assert.Equal("53", Value(session, "(getvar \"UNDOCTL\")"));

            Run(session, "UNDO", "A", "OFF");
            Assert.Equal("49", Value(session, "(getvar \"UNDOCTL\")"));
            Run(session, "UNDO", "A", "ON");

            Run(session, "UNDO", "C", "C", "N");
            Run(session, "UNDO", "C", "L", "N");
            Assert.Equal("5", Value(session, "(getvar \"UNDOCTL\")"));

            Run(session, "UNDO", "C", "O");
            Assert.Equal("3", Value(session, "(getvar \"UNDOCTL\")"));
            Run(session, "UNDO", "C", "N");
            Assert.Equal("0", Value(session, "(getvar \"UNDOCTL\")"));
            Assert.Equal(
                "; error: AutoCAD variable setting rejected: \"UNDOCTL\" 1",
                Value(session, "(setvar \"UNDOCTL\" 1)")
            );
        }

        #endregion

        #region -STYLE (probes p3 and p6, and the L2 run dd)

        [Fact]
        public void StyleTyped_AsksAutoCadsWords_FontNameForShx_WidthFactorToFourPlaces_AndVertical()
        {
            // AutoCAD 2021 (dd, p3): "Specify font name or font filename (for SHX) <txt>:", "Specify width factor <1.0000>:",
            // "Specify obliquing angle <0>:" and "Vertical? [Yes/No] <No>:"; the presenter said "Specify full font name or font
            // filename (TTF or SHX)", "<1>" and "Vertically?".
            CadCommandSession session = Typed(Blank(), "-STYLE", "GHT2");
            Assert.Equal("New style.", session.History.Last());
            Assert.StartsWith(
                "Specify font name or font filename (for SHX) <",
                session.PromptText,
                StringComparison.Ordinal
            );

            Run(session, "romans.shx");
            Assert.Equal("Specify height of text or [Annotative] <0.0000>:", session.PromptText);

            Run(session, "2.5");
            Assert.Equal("Specify width factor <1.0000>:", session.PromptText);

            Run(session, "0.8");
            Assert.Equal("Specify obliquing angle <0>:", session.PromptText);

            Run(session, "15", "N", "Y");
            Assert.Equal("Vertical? [Yes/No] <No>:", session.PromptText);

            Run(session, "N");
            Assert.Equal("\"GHT2\" is now the current text style.", session.History.Last());

            Run(session, "-STYLE", "GHT2");
            Assert.Equal("Existing style.", session.History.Last());
            Run(session, Enter, Enter);
            Assert.Equal("Specify width factor <0.8000>:", session.PromptText);
        }

        [Fact]
        public void StyleTyped_OfAnExistingTrueTypeStyle_SaysItsFullFontName_AndAsksNoVertical()
        {
            // AutoCAD 2021 (p3): the second run on GHT, made with the font Arial, writes "Existing style. Full font name = Arial",
            // asks "Specify font name or font filename (for SHX) <Arial>:" and, after the upside-down question, ends.
            CadDocument document = Blank();
            Typed(document, "-STYLE", "GHT", "Arial", "0", "1", "0", "N", "N");
            CadCommandSession session = Typed(document, "-STYLE", "GHT");

            Assert.Equal("Existing style. Full font name = Arial", session.History.Last());
            Assert.Equal("Specify font name or font filename (for SHX) <Arial>:", session.PromptText);

            Run(session, Enter, Enter, Enter, Enter, Enter, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal("\"GHT\" is now the current text style.", session.History.Last());
        }

        #endregion
    }
}
