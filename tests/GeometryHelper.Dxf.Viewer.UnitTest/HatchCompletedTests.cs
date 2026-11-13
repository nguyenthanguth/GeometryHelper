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
    /// HATCH completed (round 2, R2.2): the rest of -HATCH's keywords (draW boundary, remove Boundaries, Origin, ANnotative, hatch
    /// COlor, LAyer, Transparency, and Advanced's Boundary set and Gap tolerance), the gradient of one colour, the plain HATCH
    /// drawn behind its boundary, and -BOUNDARY refusing ellipses and splines. The prompts, the answers they took and the values
    /// the hatches came out with were read from AutoCAD 2021 by accoreconsole probes.
    /// </summary>
    public class HatchCompletedTests
    {
        private const string MainPrompt =
            "Specify internal point or [Properties/Select objects/draW boundary/remove Boundaries/Advanced/DRaw order/Origin/ANnotative/hatch COlor/LAyer/Transparency]:";

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A drawing of four LINEs round each square given, by its least corner and side.</summary>
        private static CadDocument Squares(params double[] squares)
        {
            var database = new DxfDatabase("AC1027");

            for (int s = 0; s + 2 < squares.Length + 1; s += 3)
            {
                double x = squares[s];
                double y = squares[s + 1];
                double side = squares[s + 2];
                GeoPoint3[] corners =
                {
                    new GeoPoint3(x, y, 0),
                    new GeoPoint3(x + side, y, 0),
                    new GeoPoint3(x + side, y + side, 0),
                    new GeoPoint3(x, y + side, 0),
                };

                for (int i = 0; i < 4; i++)
                {
                    database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
                }
            }

            return CadDocument.From(database, "squares.dxf");
        }

        /// <summary>A square 100 by 100 of four lines whose right side has a gap of 2 in it, from y 0 to y 2.</summary>
        private static CadDocument SquareWithAGap()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 2, 0), new GeoPoint3(100, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(0, 0, 0)));
            return CadDocument.From(database, "gap.dxf");
        }

        private static DxfHatch OnlyHatch(CadDocument document) => Assert.Single(All<DxfHatch>(document.ModelSpace));

        private static DxfEllipse Ellipse(double x, double y, double major) =>
            new DxfEllipse(
                new GeoPoint3(x, y, 0),
                new GeoVector3(0, 0, 1),
                new GeoVector3(major, 0, 0),
                0.5,
                0,
                2 * Math.PI
            );

        #region the prompts

        [Fact]
        public void HatchTyped_AsksWithEveryKeywordOfAutoCad2021()
        {
            CadCommandSession session = Start(Squares(0, 0, 100), "-HATCH");

            Assert.Equal(MainPrompt, session.PromptText);
        }

        [Fact]
        public void HatchTyped_AdvancedAddsBoundarySetAndGapTolerance()
        {
            CadCommandSession session = Start(Squares(0, 0, 100), "-HATCH", "A");

            Assert.Equal(
                "Enter an option [Boundary set/Retain boundary/Island detection/Style/Associativity/Gap tolerance/separate Hatches]:",
                session.PromptText
            );
        }

        #endregion

        #region draW boundary

        [Fact]
        public void DrawBoundary_AsksThePolylineLikeAutoCad_AndHatchesTheShapeDrawnWithNoAssociation()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-HATCH", "W");
            Assert.Equal("Retain polyline boundary? [Yes/No] <N>:", session.PromptText);

            Run(session, "N");
            Assert.Equal("Specify start point:", session.PromptText);
            Run(session, P(10, 10));
            Assert.Equal("Specify next point or [Arc/Length/Undo]:", session.PromptText);
            Run(session, P(60, 10));
            Assert.Equal("Specify next point or [Arc/Close/Length/Undo]:", session.PromptText);
            Run(session, P(60, 60), "C");
            Assert.Equal("Specify start point for new boundary or <Accept>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal(MainPrompt, session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(1250.0, hatch.Area, 6);
            Assert.False(hatch.Associative);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));

            // AutoCAD leaves associativity off once a boundary was drawn.
            Assert.False(document.HatchSettings.Associative);
        }

        [Fact]
        public void DrawBoundary_RetainingTheBoundary_KeepsAClosedPolylineOfTheCornersDrawn()
        {
            CadDocument document = Blank();
            Start(document, "-HATCH", "W", "Y", P(10, 10), P(60, 10), P(60, 60), "C", Enter, Enter);

            DxfPolyline kept = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.True(kept.Closed);
            Assert.Equal(3, kept.NumberOfVertices);
            Assert.Equal(1, OnlyHatch(document).NumberOfLoops);
        }

        [Fact]
        public void DrawBoundary_UndoTakesTheLastPointBack_AndEnterEndsAnOpenShapeThatTheHatchClosesAcross()
        {
            CadDocument document = Blank();
            Start(document, "-HATCH", "W", "N", P(10, 10), P(60, 10), P(500, 500), "U", P(60, 60), Enter, Enter, Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(1250.0, hatch.Area, 6);
        }

        [Fact]
        public void DrawBoundary_ASecondBoundaryInsideTheFirstIsAnIslandOfTheSameHatch()
        {
            CadDocument document = Blank();
            Start(
                document,
                "-HATCH",
                "W",
                "N",
                P(0, 0),
                P(100, 0),
                P(100, 100),
                "C",
                P(20, 20),
                P(80, 20),
                P(80, 80),
                "C",
                Enter,
                Enter
            );

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(2, hatch.NumberOfLoops);
            Assert.Equal(DxfHatchLoopTypes.External, hatch.LoopTypeAt(0) & DxfHatchLoopTypes.External);
            Assert.Equal(DxfHatchLoopTypes.Default, hatch.LoopTypeAt(1) & DxfHatchLoopTypes.External);
        }

        #endregion

        #region remove Boundaries

        [Fact]
        public void RemoveBoundaries_TakesTheIslandOfTheObjectSelectedOutOfTheHatch_AndUndoBringsItBack()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            string inner = All<DxfLine>(document.ModelSpace)[4].Handle;
            CadCommandSession session = Start(document, "-HATCH", P(10, 10), "B");
            Assert.Equal("Select objects or [Add boundaries]:", session.PromptText);

            Run(session, new[] { inner }, Enter);
            Assert.Equal("Select objects or [Add boundaries/Undo]:", session.PromptText);
            Run(session, "U");
            Assert.Equal("Select objects or [Add boundaries]:", session.PromptText);
            Run(session, new[] { inner }, Enter, Enter);
            Assert.Equal(MainPrompt, session.PromptText);
            Run(session, Enter);

            Assert.Equal(1, OnlyHatch(document).NumberOfLoops);
        }

        [Fact]
        public void RemoveBoundaries_AddBoundariesGoesBackToThePointPrompt()
        {
            CadCommandSession session = Start(Squares(0, 0, 100), "-HATCH", "B", "A");

            Assert.Equal(MainPrompt, session.PromptText);
        }

        #endregion

        #region Origin

        [Fact]
        public void Origin_SetNewOrigin_AsksThePointAndWhetherToStoreIt()
        {
            CadDocument plain = Squares(0, 0, 100);
            Start(plain, "-HATCH", P(50, 50), Enter);
            DxfPatternDefinition before = OnlyHatch(plain).GetPatternDefinitionAt(0);

            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "O");
            Assert.Equal(
                "[Use current origin/Set new origin/Default to boundary extents] <Use current origin>:",
                session.PromptText
            );
            Run(session, "S");
            Assert.Equal("Select point:", session.PromptText);
            Run(session, P(10, 20));
            Assert.Equal("Store as default origin? [Yes/No] <N>:", session.PromptText);
            Run(session, "N", P(50, 50), Enter);

            DxfPatternDefinition line = OnlyHatch(document).GetPatternDefinitionAt(0);
            Assert.Equal(10.0, line.BaseX - before.BaseX, 6);
            Assert.Equal(20.0, line.BaseY - before.BaseY, 6);

            // Answered No, the origin is the hatch's alone.
            Assert.Equal(0.0, document.HatchSettings.OriginX);
            Assert.Equal(0.0, document.HatchSettings.OriginY);
        }

        [Fact]
        public void Origin_SetNewOrigin_StoredAsTheDefaultIsTheNextHatchesToo()
        {
            CadDocument document = Squares(0, 0, 100, 200, 0, 100);
            Start(document, "-HATCH", "O", "S", P(10, 20), "Y", P(50, 50), Enter);

            Assert.Equal(10.0, document.HatchSettings.OriginX);
            Assert.Equal(20.0, document.HatchSettings.OriginY);

            Start(document, "-HATCH", P(250, 50), Enter);
            Assert.Equal(2, All<DxfHatch>(document.ModelSpace).Count);
            DxfHatch[] both = All<DxfHatch>(document.ModelSpace).ToArray();
            Assert.Equal(both[0].GetPatternDefinitionAt(0).BaseX, both[1].GetPatternDefinitionAt(0).BaseX, 6);
        }

        [Fact]
        public void Origin_DefaultToBoundaryExtents_AsksTheCorner_AndLaysThePatternFromTheMiddleOfTheBoxRoundTheHatch()
        {
            CadDocument plain = Squares(0, 0, 100);
            Start(plain, "-HATCH", P(50, 50), Enter);
            DxfPatternDefinition before = OnlyHatch(plain).GetPatternDefinitionAt(0);

            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "O", "D");
            Assert.Equal("[bottom Left/bottom Right/top rIght/top lEft/Center] <bottom Left>:", session.PromptText);
            Run(session, "C");
            Assert.Equal("Store as default origin? [Yes/No] <N>:", session.PromptText);
            Run(session, "N", P(50, 50), Enter);

            DxfPatternDefinition line = OnlyHatch(document).GetPatternDefinitionAt(0);
            Assert.Equal(50.0, line.BaseX - before.BaseX, 6);
            Assert.Equal(50.0, line.BaseY - before.BaseY, 6);
            Assert.Equal(CadHatchOriginMode.Center, document.HatchSettings.OriginMode);
        }

        [Fact]
        public void Origin_TheCornersOfTheBoxAreTheOnesAutoCadNames()
        {
            double[,] expected =
            {
                { 0, 0 },
                { 100, 0 },
                { 100, 100 },
                { 0, 100 },
            };
            string[] keys = { "L", "R", "I", "E" };

            CadDocument plain = Squares(0, 0, 100);
            Start(plain, "-HATCH", P(50, 50), Enter);
            DxfPatternDefinition before = OnlyHatch(plain).GetPatternDefinitionAt(0);

            for (int i = 0; i < keys.Length; i++)
            {
                CadDocument document = Squares(0, 0, 100);
                Start(document, "-HATCH", "O", "D", keys[i], "N", P(50, 50), Enter);
                DxfPatternDefinition line = OnlyHatch(document).GetPatternDefinitionAt(0);
                Assert.Equal(expected[i, 0], line.BaseX - before.BaseX, 6);
                Assert.Equal(expected[i, 1], line.BaseY - before.BaseY, 6);
            }
        }

        #endregion

        #region ANnotative

        [Fact]
        public void Annotative_AsksYesOrNo_AndSaysAnAnnotativeHatchIsNotMadeYet()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "AN");
            Assert.Equal("Create annotative hatch [Yes/No] <N>:", session.PromptText);

            Run(session, "Y");
            Assert.Contains(session.History, l => l.Contains("annotative hatch is not made"));
            Assert.Equal(MainPrompt, session.PromptText);
            Run(session, P(50, 50), Enter);

            Assert.Equal(DxfAnnotativeStates.False, OnlyHatch(document).Annotative);
        }

        #endregion

        #region hatch COlor

        [Fact]
        public void HatchColor_AsksTheColourAndTheBackgroundColour_AndGivesThemToTheHatch()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "CO");
            Assert.Equal("New color [Truecolor/COlorbook/. (for use current)] <use current>:", session.PromptText);

            Run(session, "1");
            Assert.Equal("New background color [Truecolor/COlorbook/. (for none)] <None>:", session.PromptText);
            Run(session, "3");
            Assert.Equal(MainPrompt, session.PromptText);
            Run(session, P(50, 50), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(1, hatch.Color.ColorIndex);
            Assert.Equal(3, hatch.BackgroundColor.ColorIndex);
        }

        [Fact]
        public void HatchColor_TruecolorAsksRedGreenBlue_AndADotGoesBackToTheCurrentColour()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "CO", "T");
            Assert.Equal("Red,Green,Blue:", session.PromptText);
            Run(session, "10,20,30", ".", P(50, 50), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.True(hatch.Color.IsByColor);
            Assert.Equal(20, hatch.Color.Green);
            Assert.True(hatch.BackgroundColor.IsNone);

            // The next -HATCH offers what was set, and a dot takes it off.
            session = Start(document, "-HATCH", "CO");
            Assert.Equal("New color [Truecolor/COlorbook/. (for use current)] <10,20,30>:", session.PromptText);
            Run(session, ".", ".", P(50, 50), Enter);
            Assert.True(All<DxfHatch>(document.ModelSpace)[1].Color.IsByLayer);
        }

        [Fact]
        public void HatchColor_AColourThatIsNone_SaysSoAndAsksAgain()
        {
            CadCommandSession session = Start(Squares(0, 0, 100), "-HATCH", "CO", "foo");

            Assert.Contains("A color number or standard color name is required.", session.History);
            Assert.Equal("New color [Truecolor/COlorbook/. (for use current)] <use current>:", session.PromptText);
        }

        #endregion

        #region LAyer

        [Fact]
        public void Layer_AsksTheLayer_RefusesOneThatIsNotThere_AndPutsTheHatchOnTheOneGiven()
        {
            CadDocument document = Squares(0, 0, 100);
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "HL", ColorIndex = 7 });
            CadCommandSession session = Start(document, "-HATCH", "LA");
            Assert.Equal("Specify layer or [. (for use current)] <use current>:", session.PromptText);

            Run(session, "NOSUCH");
            Assert.Contains("Cannot find layer \"NOSUCH\".", session.History);
            Assert.Equal("Specify layer or [. (for use current)] <use current>:", session.PromptText);
            Run(session, "hl");
            Assert.Equal(MainPrompt, session.PromptText);
            Run(session, P(50, 50), Enter);

            Assert.Equal("HL", OnlyHatch(document).Layer);
        }

        [Fact]
        public void Layer_ADotTakesTheLayerOffAgain_AndTheNextPromptOffersTheOneSet()
        {
            CadDocument document = Squares(0, 0, 100);
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "HL", ColorIndex = 7 });
            Start(document, "-HATCH", "LA", "HL", Esc);
            CadCommandSession session = Start(document, "-HATCH", "LA");
            Assert.Equal("Specify layer or [. (for use current)] <HL>:", session.PromptText);

            Run(session, ".", P(50, 50), Enter);
            Assert.Equal("0", OnlyHatch(document).Layer);
        }

        #endregion

        #region Transparency

        [Fact]
        public void Transparency_AsksZeroToNinety_RefusesWhatIsOutside_AndGivesTheHatchTheAlphaAutoCadMakes()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "T");
            Assert.Equal(
                "Specify transparency value (0-90) or [. (for use current)] <use current>:",
                session.PromptText
            );

            Run(session, "95");
            Assert.Contains("Requires an integer between 0 and 90 or ByLayer/ByBlock.", session.History);
            Run(session, "50", P(50, 50), Enter);

            // 255 times half, rounded down, as the layer's transparency is worked out.
            Assert.Equal(127, OnlyHatch(document).Transparency.Alpha);
        }

        [Fact]
        public void Transparency_ByBlockAndByLayerAreTaken_AndTheNextPromptOffersThem()
        {
            CadDocument document = Squares(0, 0, 100);
            Start(document, "-HATCH", "T", "ByBlock", Esc);
            CadCommandSession session = Start(document, "-HATCH", "T");
            Assert.Equal("Specify transparency value (0-90) or [. (for use current)] <ByBlock>:", session.PromptText);

            Run(session, "60", P(50, 50), Enter);
            Assert.Equal(102, OnlyHatch(document).Transparency.Alpha);
        }

        #endregion

        #region Boundary set and Gap tolerance

        [Fact]
        public void BoundarySet_New_LooksOnlyAmongTheObjectsSelected()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            string[] inner = All<DxfLine>(document.ModelSpace).Skip(4).Select(l => l.Handle).ToArray();
            CadCommandSession session = Start(document, "-HATCH", "A", "B");
            Assert.Equal("Specify candidate set for boundary [New/Everything] <Everything>:", session.PromptText);

            Run(session, "N");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, inner, Enter);
            Assert.Contains("Analyzing the selected data...", session.History);
            Assert.Equal(
                "Enter an option [Boundary set/Retain boundary/Island detection/Style/Associativity/Gap tolerance/separate Hatches]:",
                session.PromptText
            );
            Run(session, Enter, P(10, 10));
            Assert.Contains("Valid hatch boundary not found.", session.History);
            Run(session, P(30, 30), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(3600.0, hatch.Area, 6);
        }

        [Fact]
        public void BoundarySet_Everything_GoesBackToAllTheObjects()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            string[] inner = All<DxfLine>(document.ModelSpace).Skip(4).Select(l => l.Handle).ToArray();
            Start(document, "-HATCH", "A", "B", "N", inner, Enter, "B", "E", Enter, P(10, 10), Enter);

            Assert.Equal(2, OnlyHatch(document).NumberOfLoops);
        }

        [Fact]
        public void GapTolerance_AsksTheValue_RefusesWhatIsOutsideZeroTo5000_AndSaysWhatItSet()
        {
            CadCommandSession session = Start(SquareWithAGap(), "-HATCH", "A", "G");
            Assert.Equal("Specify a boundary gap tolerance value <0>:", session.PromptText);

            Run(session, "-1");
            Assert.Contains("Value must be between 0 and 5000.", session.History);
            Assert.Equal("Specify a boundary gap tolerance value <0>:", session.PromptText);
            Run(session, "3");
            Assert.Contains("HPGAPTOL set to 3", session.History);
            Assert.Equal(
                "Enter an option [Boundary set/Retain boundary/Island detection/Style/Associativity/Gap tolerance/separate Hatches]:",
                session.PromptText
            );
        }

        [Fact]
        public void GapTolerance_AGapOfTwoIsClosedByAToleranceOfThree_AndNotByOneOrNought()
        {
            CadDocument none = SquareWithAGap();
            CadCommandSession session = Start(none, "-HATCH", P(50, 50), Enter);
            Assert.Contains("Valid hatch boundary not found.", session.History);
            Assert.Empty(All<DxfHatch>(none.ModelSpace));

            CadDocument narrow = SquareWithAGap();
            session = Start(narrow, "-HATCH", "A", "G", "1", Enter, P(50, 50), Enter);
            Assert.Contains("Valid hatch boundary not found.", session.History);
            Assert.Empty(All<DxfHatch>(narrow.ModelSpace));

            CadDocument wide = SquareWithAGap();
            Start(wide, "-HATCH", "A", "G", "3", Enter, P(50, 50), Enter);
            DxfHatch hatch = OnlyHatch(wide);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(10000.0, hatch.Area, 6);
            Assert.Equal(3.0, wide.HatchSettings.GapTolerance);
        }

        [Fact]
        public void Finder_Candidates_LeaveOutTheObjectsNotInTheSet()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            string[] inner = All<DxfLine>(document.ModelSpace).Skip(4).Select(l => l.Handle).ToArray();

            Assert.Null(
                CadBoundaryFinder.Find(document, document.ModelSpace, P(10, 10), Tolerance.Default, null, inner)
            );
            IReadOnlyList<CadBoundaryLoop> found = CadBoundaryFinder.Find(
                document,
                document.ModelSpace,
                P(30, 30),
                Tolerance.Default,
                null,
                inner
            );
            Assert.Single(found);
            Assert.Equal(3600.0, found[0].Area, 6);
        }

        [Fact]
        public void Finder_Gap_JoinsTheTwoLooseEndsWithAnEdgeOfNoObject()
        {
            CadDocument document = SquareWithAGap();

            Assert.Null(CadBoundaryFinder.Find(document, document.ModelSpace, P(50, 50), Tolerance.Default));
            Assert.Null(
                CadBoundaryFinder.Find(document, document.ModelSpace, P(50, 50), Tolerance.Default, null, null, 1.0)
            );
            IReadOnlyList<CadBoundaryLoop> found = CadBoundaryFinder.Find(
                document,
                document.ModelSpace,
                P(50, 50),
                Tolerance.Default,
                null,
                null,
                3.0
            );

            CadBoundaryLoop loop = Assert.Single(found);
            Assert.Equal(5, loop.Vertices.Count);
            Assert.Equal(10000.0, loop.Area, 6);
            Assert.Equal(4, loop.Sources.Count);
        }

        #endregion

        #region a gradient of one colour

        [Fact]
        public void Gradient_SingleColor_AsksTheColourAndTheTint_AndMakesTheGradientAutoCadWritesForOne()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "P", "G");
            Assert.Equal("Enter a gradient name or [?/Single color/Two color] <GR_LINEAR>:", session.PromptText);

            Run(session, "S");
            Assert.Equal("New gradient color [Truecolor/COlorbook] <0,0,255>:", session.PromptText);
            Run(session, "1");
            Assert.Equal("Specify a shade or tint value (0-1) <1.0000>:", session.PromptText);
            Run(session, "0.25");
            Assert.Equal(MainPrompt, session.PromptText);
            Run(session, P(50, 50), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.True(hatch.IsGradient);
            Assert.True(hatch.GradientOneColorMode);
            Assert.Equal(0.25f, hatch.ShadeTintValue);
            DxfGradientColor[] colours = hatch.GetGradientColors();
            Assert.Equal(2, colours.Length);
            Assert.Equal(1, colours[0].Color.ColorIndex);
            Assert.Equal(0.0f, colours[0].Value);
            Assert.Equal(1.0f, colours[1].Value);
            Assert.Equal((128, 0, 0), (colours[1].Color.Red, colours[1].Color.Green, colours[1].Color.Blue));
            Assert.True(document.HatchSettings.GradientOneColor);
        }

        [Theory]
        [InlineData("1", 1.0, 255, 255, 255)]
        [InlineData("1", 0.0, 0, 0, 0)]
        [InlineData("1", 0.25, 128, 0, 0)]
        [InlineData("0,128,255", 0.75, 128, 191, 255)]
        [InlineData("255,200,200", 0.5, 255, 0, 0)]
        [InlineData("100,150,50", 0.9, 230, 242, 217)]
        [InlineData("100,150,50", 0.1, 26, 38, 13)]
        [InlineData("100,150,50", 0.5, 128, 191, 64)]
        [InlineData("30,30,30", 0.4, 102, 102, 102)]
        [InlineData("200,100,50", 0.6, 214, 133, 92)]
        [InlineData("200,100,50", 0.4, 163, 82, 41)]
        public void Gradient_SingleColor_TheSecondEndIsTheColoursHueAtTheLightnessOfTheTint(
            string colour,
            double tint,
            int red,
            int green,
            int blue
        )
        {
            // The ends AutoCAD 2021 wrote for these pairs, read back from its hatches.
            CadDocument document = Squares(0, 0, 100);
            Start(
                document,
                "-HATCH",
                "P",
                "G",
                "S",
                colour,
                tint.ToString(System.Globalization.CultureInfo.InvariantCulture),
                P(50, 50),
                Enter
            );

            DxfGradientColor second = OnlyHatch(document).GetGradientColors()[1];
            Assert.Equal((red, green, blue), (second.Color.Red, second.Color.Green, second.Color.Blue));
        }

        [Fact]
        public void Gradient_SingleColor_TheSecondEndMatchesEveryOneOfTheFortyFivePairsAutoCadWrote()
        {
            // Colour, tint and the end AutoCAD 2021 wrote for it, five colours at nine tints.
            string[] pairs =
            {
                "255,200,200|0|0,0,0",
                "255,200,200|0.1|51,0,0",
                "255,200,200|0.25|128,0,0",
                "255,200,200|0.4|204,0,0",
                "255,200,200|0.5|255,0,0",
                "255,200,200|0.6|255,51,51",
                "255,200,200|0.75|255,128,128",
                "255,200,200|0.9|255,204,204",
                "255,200,200|1|255,255,255",
                "100,150,50|0|0,0,0",
                "100,150,50|0.1|26,38,13",
                "100,150,50|0.25|64,96,32",
                "100,150,50|0.4|102,153,51",
                "100,150,50|0.5|128,191,64",
                "100,150,50|0.6|153,204,102",
                "100,150,50|0.75|191,223,159",
                "100,150,50|0.9|230,242,217",
                "100,150,50|1|255,255,255",
                "200,100,50|0|0,0,0",
                "200,100,50|0.1|41,20,10",
                "200,100,50|0.25|102,51,25",
                "200,100,50|0.4|163,82,41",
                "200,100,50|0.5|204,102,51",
                "200,100,50|0.6|214,133,92",
                "200,100,50|0.75|230,179,153",
                "200,100,50|0.9|245,224,214",
                "200,100,50|1|255,255,255",
                "30,30,30|0|0,0,0",
                "30,30,30|0.1|26,26,26",
                "30,30,30|0.25|64,64,64",
                "30,30,30|0.4|102,102,102",
                "30,30,30|0.5|128,128,128",
                "30,30,30|0.6|153,153,153",
                "30,30,30|0.75|191,191,191",
                "30,30,30|0.9|230,230,230",
                "30,30,30|1|255,255,255",
                "128,128,128|0|0,0,0",
                "128,128,128|0.1|26,26,26",
                "128,128,128|0.25|64,64,64",
                "128,128,128|0.4|102,102,102",
                "128,128,128|0.5|128,128,128",
                "128,128,128|0.6|153,153,153",
                "128,128,128|0.75|191,191,191",
                "128,128,128|0.9|230,230,230",
                "128,128,128|1|255,255,255",
            };

            foreach (string pair in pairs)
            {
                string[] parts = pair.Split('|');
                CadDocument document = Squares(0, 0, 100);
                Start(document, "-HATCH", "P", "G", "S", "T", parts[0], parts[1], P(50, 50), Enter);

                DxfColor second = OnlyHatch(document).GetGradientColors()[1].Color;
                Assert.Equal(parts[2], second.Red + "," + second.Green + "," + second.Blue);
            }
        }

        [Fact]
        public void Gradient_SingleColor_ATintOutsideZeroToOneIsTakenAsTheNearerEnd()
        {
            CadDocument document = Squares(0, 0, 100);
            Start(document, "-HATCH", "P", "G", "S", "1", "2", P(50, 50), Enter);

            Assert.Equal(1.0f, OnlyHatch(document).ShadeTintValue);
        }

        [Fact]
        public void Gradient_TwoColor_AsksTheColoursWithTruecolorAndColorbookOffered()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "P", "G", "T");
            Assert.Equal("New gradient color [Truecolor/COlorbook] <0,0,255>:", session.PromptText);

            Run(session, "1");
            Assert.Equal("New gradient second color:[Truecolor/COlorbook] <255,255,153>:", session.PromptText);
            Run(session, "3", P(50, 50), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.False(hatch.GradientOneColorMode);
            Assert.False(document.HatchSettings.GradientOneColor);
        }

        [Fact]
        public void Gradient_TheSettingsOfAGradientOfOneColorAreReadBackFromTheHatch()
        {
            CadDocument document = Squares(0, 0, 100);
            Start(document, "-HATCH", "P", "G", "S", "0,128,255", "0.75", P(50, 50), Enter);

            CadHatchSettings read = CadHatchSettings.Of(OnlyHatch(document));

            Assert.True(read.GradientOneColor);
            Assert.Equal(0.75, read.GradientTint, 6);
            Assert.Equal(128, read.GradientColor1.Green);
        }

        #endregion

        #region plain HATCH

        [Fact]
        public void Hatch_IsDrawnBehindItsBoundaryByDefault_AsAutoCadsHpdraworderThreeDoes()
        {
            CadDocument document = Squares(0, 0, 100);
            Start(document, "HATCH", P(50, 50), Enter);

            List<DxfEntity> order = document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().ToList();
            Assert.IsType<DxfHatch>(order[0]);
            Assert.Equal(5, order.Count);
        }

        [Fact]
        public void Hatch_TakesTheDrawOrderChosenInDash_Hatch()
        {
            // The choice is the session's, as the rest of the command line's memory.
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "DR", "F", Esc);
            Run(session, "HATCH", P(50, 50), Enter);

            List<DxfEntity> order = document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().ToList();
            Assert.IsType<DxfHatch>(order[order.Count - 1]);
        }

        [Fact]
        public void Hatch_OfASelectedCircleWithNoBoundaryObjectInTheWay_IsLeftOnTop()
        {
            CadDocument document = Blank();
            var circle = new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 20);
            document.Database.ModelSpace.AppendEntity(circle);
            Start(document, "HATCH", "S", new[] { circle.Handle }, Enter);

            // The circle is its own boundary: the hatch goes behind it.
            List<DxfEntity> order = document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().ToList();
            Assert.IsType<DxfHatch>(order[0]);
        }

        [Fact]
        public void Hatch_TheLayerColourAndTransparencyOfTheSettingsAreGivenToAHatchOfTheDialog()
        {
            CadDocument document = Squares(0, 0, 100);
            document.HatchSettings = new CadHatchSettings
            {
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5),
                Transparency = new DxfTransparency(51),
            };
            Start(document, "HATCH", P(50, 50), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(5, hatch.Color.ColorIndex);
            Assert.Equal(51, hatch.Transparency.Alpha);
        }

        #endregion

        #region -BOUNDARY and ellipses and splines

        [Fact]
        public void BoundaryTyped_AnEllipseInsideTheArea_IsRefusedAsAutoCadRefusesIt_AndNothingIsMade()
        {
            CadDocument document = Squares(0, 0, 100);
            document.Database.ModelSpace.AppendEntity(Ellipse(50, 50, 20));
            CadCommandSession session = Start(document, "-BOUNDARY", P(10, 10));

            Assert.Contains("Spline or ellipse detected. Cannot create boundary.", session.History);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void BoundaryTyped_AnEllipseTheAreaIsMadeOf_IsRefused()
        {
            CadDocument document = Blank();
            document.Database.ModelSpace.AppendEntity(Ellipse(50, 50, 20));
            CadCommandSession session = Start(document, "-BOUNDARY", P(50, 50));

            Assert.Contains("Spline or ellipse detected. Cannot create boundary.", session.History);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void Boundary_AClosedSplineTheAreaIsMadeOf_IsRefusedToo()
        {
            CadDocument document = Blank();

            // A spline of degree 1 through the corners of a square, back to the first.
            var spline = new DxfSpline(
                1,
                false,
                true,
                false,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(100, 0, 0),
                    new GeoPoint3(100, 100, 0),
                    new GeoPoint3(0, 100, 0),
                    new GeoPoint3(0, 0, 0),
                },
                new[] { 0.0, 0, 1, 2, 3, 4, 4 },
                null,
                0,
                0
            );
            document.Database.ModelSpace.AppendEntity(spline);
            CadCommandSession session = Start(document, "BOUNDARY", P(50, 50));

            Assert.Contains("Spline or ellipse detected. Cannot create boundary.", session.History);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void BoundaryTyped_AnEllipseFarFromTheArea_DoesNotStopIt()
        {
            CadDocument document = Squares(0, 0, 100);
            document.Database.ModelSpace.AppendEntity(Ellipse(500, 500, 20));
            CadCommandSession session = Start(document, "-BOUNDARY", P(10, 10));

            Assert.Contains("BOUNDARY created 1 polyline", session.History);
            Assert.Single(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void HatchTyped_RetainBoundary_KeepsNoPolylineOfALoopThatIsAnEllipse()
        {
            CadDocument document = Blank();
            DxfEllipse ellipse = Ellipse(50, 50, 20);
            document.Database.ModelSpace.AppendEntity(ellipse);
            Start(document, "-HATCH", "A", "R", "Y", Enter, "S", new[] { ellipse.Handle }, Enter, Enter);

            Assert.Equal(1, OnlyHatch(document).NumberOfLoops);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
        }

        [Fact]
        public void HatchTyped_AnEllipseIsStillAHatchBoundary()
        {
            CadDocument document = Blank();
            document.Database.ModelSpace.AppendEntity(Ellipse(50, 50, 20));
            Start(document, "-HATCH", P(50, 50), Enter);

            Assert.Equal(1, OnlyHatch(document).NumberOfLoops);
        }

        #endregion
    }
}
