using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Batch 4 of acad.pgp's commands with a window of their own: VPORTS and -VPORTS, FILTER, QUICKCALC and CAL,
    /// QUICKPROPERTIES, in the words of AutoCAD 2021 (the prompts, the order of the tiles and the messages read from it by
    /// accoreconsole probes). No dialog is shown: the window's part is played by a handler of the session's requests.
    /// </summary>
    public class ViewUtilityBatchFourTests
    {
        private const string VportsPrompt =
            "Enter an option [Save/Restore/Delete/Join/SIngle/?/2/3/4/Toggle/MOde] <3>:";

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static void Rect(CadTiledViewport tile, int id, double l, double b, double r, double t)
        {
            Assert.Equal(id, tile.Id);
            Assert.Equal(l, tile.Left, 9);
            Assert.Equal(b, tile.Bottom, 9);
            Assert.Equal(r, tile.Right, 9);
            Assert.Equal(t, tile.Top, 9);
        }

        #region -VPORTS

        [Fact]
        public void VportsTyped_AsksAutoCADsOptions_AndTwoVerticalKeepsTheRightHalfCurrent()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-VPORTS");
            Assert.Equal(VportsPrompt, session.PromptText);
            Run(session, "2");
            Assert.Equal("Enter a configuration option [Horizontal/Vertical] <Vertical>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            IReadOnlyList<CadTiledViewport> tiles = document.TiledViewports.Tiles;
            Assert.Equal(2, tiles.Count);
            Rect(tiles[0], 2, 0.5, 0.0, 1.0, 1.0);
            Rect(tiles[1], 3, 0.0, 0.0, 0.5, 1.0);
            Assert.Equal("Regenerating model.", session.History.Last());
        }

        [Fact]
        public void VportsTyped_EnterAtTheFirstPrompt_SplitsInThreeWithTheRightOneCurrent()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-VPORTS", Enter);
            Assert.Equal(
                "Enter a configuration option [Horizontal/Vertical/Above/Below/Left/Right] <Right>:",
                session.PromptText
            );
            Run(session, Enter);

            IReadOnlyList<CadTiledViewport> tiles = document.TiledViewports.Tiles;
            Assert.Equal(3, tiles.Count);
            Rect(tiles[0], 2, 0.5, 0.0, 1.0, 1.0);
            Rect(tiles[1], 3, 0.0, 0.5, 0.5, 1.0);
            Rect(tiles[2], 4, 0.0, 0.0, 0.5, 0.5);
        }

        [Theory]
        [InlineData("H", 0.0, 2.0 / 3.0, 1.0, 1.0)]
        [InlineData("V", 2.0 / 3.0, 0.0, 1.0, 1.0)]
        [InlineData("A", 0.0, 0.5, 1.0, 1.0)]
        [InlineData("B", 0.0, 0.0, 1.0, 0.5)]
        [InlineData("L", 0.0, 0.0, 0.5, 1.0)]
        public void VportsTyped_ThreeOfEachArrangement_KeepsThePieceAutoCADKeepsCurrent(
            string arrangement,
            double l,
            double b,
            double r,
            double t
        )
        {
            CadDocument document = Plan();
            Start(document, "-VPORTS", "3", arrangement);

            Assert.Equal(3, document.TiledViewports.Tiles.Count);
            Rect(document.TiledViewports.Current, 2, l, b, r, t);
        }

        [Fact]
        public void VportsTyped_Four_KeepsTheLowerRightCurrent_AndNumbersTheOthersRoundIt()
        {
            CadDocument document = Plan();
            Start(document, "-VPORTS", "4");

            IReadOnlyList<CadTiledViewport> tiles = document.TiledViewports.Tiles;
            Assert.Equal(4, tiles.Count);
            Rect(tiles[0], 2, 0.5, 0.0, 1.0, 0.5);
            Rect(tiles[1], 3, 0.5, 0.5, 1.0, 1.0);
            Rect(tiles[2], 4, 0.0, 0.5, 0.5, 1.0);
            Rect(tiles[3], 5, 0.0, 0.0, 0.5, 0.5);
        }

        [Fact]
        public void VportsTyped_SplittingASecondTime_SplitsTheCurrentTileOnly()
        {
            CadDocument document = Plan();
            Start(document, "-VPORTS", "2", "V", "-VPORTS", "2", "H");

            IReadOnlyList<CadTiledViewport> tiles = document.TiledViewports.Tiles;
            Assert.Equal(3, tiles.Count);
            Rect(tiles[0], 2, 0.5, 0.5, 1.0, 1.0);
            Rect(tiles[1], 3, 0.0, 0.0, 0.5, 1.0);
            Rect(tiles[2], 4, 0.5, 0.0, 1.0, 0.5);
        }

        [Fact]
        public void VportsTyped_ANewTile_ShowsTheWholeOfTheViewItCameFrom()
        {
            CadDocument document = Plan();
            CadTiledViewports tiles = document.TiledViewports;
            tiles.AreaAspect = 1.0;
            tiles.Current.Center = new GeoPoint2(50, 25);
            tiles.Current.Width = 100;
            tiles.Current.Height = 100;
            Start(document, "-VPORTS", "2", "V");

            foreach (CadTiledViewport tile in tiles.Tiles)
            {
                Near(new GeoPoint2(50, 25), tile.Center);
                Assert.Equal(100.0, tile.Width, 9);
                Assert.Equal(200.0, tile.Height, 9);
            }
        }

        [Fact]
        public void VportsTyped_Single_KeepsTheCurrentTileAlone_AndSaysNothingWhereItIsAlone()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-VPORTS", "4", "-VPORTS", "SI");
            Assert.Single(document.TiledViewports.Tiles);
            Rect(document.TiledViewports.Current, 2, 0, 0, 1, 1);

            int lines = session.History.Count;
            Run(session, "-VPORTS", "SI");
            Assert.DoesNotContain("Regenerating model.", session.History.Skip(lines));
        }

        [Fact]
        public void VportsTyped_SaveAndList_WriteAsAutoCADDoes()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-VPORTS", "2", "V", "-VPORTS", "S");
            Assert.Equal("Enter name for new viewport configuration or [?]:", session.PromptText);
            Run(session, "A", "-VPORTS", "?");
            Assert.Equal("Enter name(s) of viewport configuration(s) to list <*>:", session.PromptText);
            int lines = session.History.Count;
            Run(session, Enter);

            Assert.Equal(
                new[]
                {
                    "Current configuration:",
                    "id# 2",
                    "   corners: 0.5000,0.0000 1.0000,1.0000",
                    "id# 3",
                    "   corners: 0.0000,0.0000 0.5000,1.0000",
                    "Configuration \"A\":",
                    "    0.5000,0.0000 1.0000,1.0000",
                    "    0.0000,0.0000 0.5000,1.0000",
                },
                session.History.Skip(lines + 1)
            );
        }

        [Fact]
        public void VportsTyped_SaveUnderANameInUse_AsksWhetherToReplaceIt()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-VPORTS", "S", "A", "-VPORTS", "2", "V", "-VPORTS", "S", "A");
            Assert.Equal("Viewport configuration \"A\" already exists.  Replace it? <N>", session.PromptText);
            Run(session, "N");
            Assert.Equal("Enter name for new viewport configuration or [?]:", session.PromptText);
            Run(session, "A", "Y");

            Assert.False(session.IsRunning);
            Assert.Equal(2, document.TiledViewports.Saved("A").Count);
        }

        [Fact]
        public void VportsTyped_Restore_BringsBackTheTilesAndTheirViews_AndRefusesANameNotSaved()
        {
            CadDocument document = Plan();
            CadTiledViewports tiles = document.TiledViewports;
            CadCommandSession session = Start(document, "-VPORTS", "4");
            tiles.Current.Center = new GeoPoint2(7, 8);
            Run(session, "-VPORTS", "S", "A", "-VPORTS", "SI", "-VPORTS", "R", "NOPE");
            Assert.Equal("Cannot find viewport configuration \"NOPE\".", session.History.Last());
            Assert.Equal("Enter name of viewport configuration to restore or [?]:", session.PromptText);
            Run(session, "a");

            Assert.Equal(4, tiles.Tiles.Count);
            Rect(tiles.Current, 2, 0.5, 0.0, 1.0, 0.5);
            Near(new GeoPoint2(7, 8), tiles.Current.Center);
        }

        [Fact]
        public void VportsTyped_Delete_CountsWhatItDeleted()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-VPORTS", "S", "A", "-VPORTS", "S", "B", "-VPORTS", "S", "C");
            Run(session, "-VPORTS", "D");
            Assert.Equal("Enter name(s) of viewport configurations to delete <none>:", session.PromptText);
            Run(session, "A,B");
            Assert.Equal("Deleted 2 viewport configurations.", session.History.Last());
            Run(session, "-VPORTS", "D", "C");
            Assert.Equal("Deleted one viewport configuration.", session.History.Last());
            Run(session, "-VPORTS", "D", "Q");
            Assert.Equal("No matching viewport configurations found.", session.History.Last());
            Assert.Empty(document.TiledViewports.Names);
        }

        [Fact]
        public void VportsTyped_Toggle_ShowsTheCurrentTileAlone_ThenBringsTheOthersBack()
        {
            CadDocument document = Plan();
            Start(document, "-VPORTS", "4", "-VPORTS", "T");
            Assert.Single(document.TiledViewports.Tiles);
            Assert.Empty(document.TiledViewports.Names);

            Start(document, "-VPORTS", "T");
            Assert.Equal(4, document.TiledViewports.Tiles.Count);
            Rect(document.TiledViewports.Current, 2, 0.5, 0.0, 1.0, 0.5);
        }

        [Fact]
        public void VportsTyped_Join_MakesTheDominantTileTakeThePlaceOfBoth()
        {
            CadDocument document = Plan();
            CadTiledViewports tiles = document.TiledViewports;
            CadCommandSession session = Start(document, "-VPORTS", "2", "V", "-VPORTS", "J");
            Assert.Equal("Select dominant viewport <current viewport>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Select viewport to join:", session.PromptText);

            // A click in the left tile makes it current, then answers.
            Assert.True(tiles.MakeCurrent(3));
            Run(session, P(10, 10));

            Assert.False(session.IsRunning);
            Assert.Single(tiles.Tiles);
            Rect(tiles.Current, 2, 0, 0, 1, 1);
        }

        [Fact]
        public void VportsTyped_JoinOfTilesThatMakeNoRectangle_IsRefused()
        {
            CadDocument document = Plan();
            CadTiledViewports tiles = document.TiledViewports;
            CadCommandSession session = Start(document, "-VPORTS", "4", "-VPORTS", "J", Enter);
            tiles.MakeCurrent(4);
            Run(session, P(0, 0));

            Assert.Equal("The selected viewports do not form a rectangle.", session.History.Last());
            Assert.Equal(4, tiles.Tiles.Count);
        }

        [Fact]
        public void VportsTyped_IsOneUndoStep_TakenBackAndPutAgain()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-VPORTS", "2", "V");
            Assert.Equal(2, document.TiledViewports.Tiles.Count);

            Run(session, "U");
            Assert.Single(document.TiledViewports.Tiles);
            Run(session, "REDO");
            Assert.Equal(2, document.TiledViewports.Tiles.Count);
            Rect(document.TiledViewports.Current, 2, 0.5, 0.0, 1.0, 1.0);
        }

        [Fact]
        public void VportsTyped_InALayout_AsksMviewsPrompt()
        {
            CadDocument document = Blank();
            document.CurrentSpace = document.Spaces.First(s => !s.IsModel);
            CadCommandSession session = Start(document, "-VPORTS");

            Assert.StartsWith("Specify corner of viewport or [", session.PromptText);
        }

        [Fact]
        public void Vports_TheTilesSaved_ReadBackAsAutoCADWritesThem()
        {
            CadDocument document = Blank();
            CadTiledViewports tiles = document.TiledViewports;
            Start(document, "-VPORTS", "2", "V", "-VPORTS", "S", "Pair");
            tiles.Current.Center = new GeoPoint2(12.5, -3);
            tiles.Current.Height = 40;
            tiles.Current.Width = 30;
            string path = Path.Combine(Path.GetTempPath(), "gh-vports-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                document.SaveAs(path);
                DxfDatabase database = DxfDatabase.Open(path);
                Assert.Equal(4, database.ViewportTable.Count());
                Assert.Equal("*Active", database.ViewportTable.First().Name);

                CadTiledViewports again = CadDocument.Open(path).TiledViewports;
                Assert.Equal(2, again.Tiles.Count);
                Rect(again.Tiles[0], 2, 0.5, 0.0, 1.0, 1.0);
                Rect(again.Tiles[1], 3, 0.0, 0.0, 0.5, 1.0);
                Near(new GeoPoint2(12.5, -3), again.Current.Center);
                Assert.Equal(40.0, again.Current.Height, 9);
                Assert.Equal(30.0, again.Current.Width, 9);
                Assert.Equal(new[] { "Pair" }, again.Names);
                Assert.Equal(2, again.Saved("Pair").Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Vports_ADrawingNeverSplit_IsSavedWithItsVportTableAsItWas()
        {
            CadDocument document = Plan();
            string before = document.Database.ToText();
            Assert.Single(document.TiledViewports.Tiles);
            string path = Path.Combine(Path.GetTempPath(), "gh-vports-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                document.SaveAs(path);
                Assert.Equal(before, document.Database.ToText());
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Vports_WithAWindow_AsksItForTheDialog_AndWithNoneSaysSo()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "VPORTS");
            Assert.Equal("Viewports is shown by the window.", session.History.Last());

            session.UiRequested += (s, e) =>
            {
                if (e.Name == "VPORTS")
                {
                    ((CadTiledViewports)e.Payload).Split(4, null, true);
                    e.Handled = true;
                }
            };
            Run(session, "VIEWPORTS");
            Assert.Equal(4, document.TiledViewports.Tiles.Count);
            Assert.Equal("Regenerating model.", session.History.Last());

            Run(session, "U");
            Assert.Single(document.TiledViewports.Tiles);
        }

        [Fact]
        public void Vports_FourRightOfTheDialog_KeepsTheRightHalfCurrent_AndThirdsTheLeftFromTheTop()
        {
            CadDocument document = Plan();
            CadTiledViewports tiles = document.TiledViewports;
            Start(document, "-VPORTS", "2", "H");
            Assert.Null(tiles.SplitAs("Four: Right", true));

            Assert.Equal(4, tiles.Tiles.Count);
            Rect(tiles.Tiles[0], 2, 0.5, 0.0, 1.0, 1.0);
            Rect(tiles.Tiles[1], 3, 0.0, 2.0 / 3.0, 0.5, 1.0);
            Rect(tiles.Tiles[2], 4, 0.0, 1.0 / 3.0, 0.5, 2.0 / 3.0);
            Rect(tiles.Tiles[3], 5, 0.0, 0.0, 0.5, 1.0 / 3.0);
            Assert.Equal(12, CadTiledViewports.StandardNames.Count);
            Assert.Equal(4, CadTiledViewports.PiecesOf("Four: Left").Count);
            Assert.Throws<ArgumentException>(() => tiles.SplitAs("Five: Equal", true));
        }

        [Fact]
        public void Vports_TheViewTheWindowShows_IsReadIntoTheCurrentTileBeforeASplit()
        {
            CadDocument document = Plan();
            CadTiledViewports tiles = document.TiledViewports;
            tiles.AreaAspect = 2.0;
            tiles.ReadView = tile =>
            {
                tile.Center = new GeoPoint2(300, 200);
                tile.Width = 80;
                tile.Height = 40;
            };
            Start(document, "-VPORTS", "2", "H");

            // Each half is 4 to 1 wide: the 80 by 40 the window showed, fitted, is 160 by 40.
            foreach (CadTiledViewport tile in tiles.Tiles)
            {
                Near(new GeoPoint2(300, 200), tile.Center);
                Assert.Equal(160.0, tile.Width, 9);
                Assert.Equal(40.0, tile.Height, 9);
            }
        }

        #endregion

        #region FILTER

        [Fact]
        public void Filter_Apply_KeepsWhatPassesAsThePreviousSet()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            session.UiRequested += (s, e) =>
            {
                if (e.Name == "FILTER")
                {
                    var filter = (CadSelectionFilter)e.Payload;
                    filter.Lines.Clear();
                    filter.Lines.Add(new CadFilterLine(CadFilterProperty.Layer, "Steel"));
                    e.Answer = "Apply";
                    e.Handled = true;
                }
            };
            Run(session, "FI");
            Assert.Equal("Applying filter to selection.", session.History.Last());
            Assert.Equal("Select objects:", session.PromptText);
            session.FeedSelection(new[] { "A1", "A2", "A3" });
            session.Enter();

            Assert.False(session.IsRunning);
            Assert.Contains("2 were filtered out", session.History);
            Assert.Equal("Exiting filtered selection.", session.History.Last());

            Run(session, "ERASE", "P", Enter);
            Assert.Null(document.FindEntity("A1"));
            Assert.NotNull(document.FindEntity("A2"));
            Assert.NotNull(document.FindEntity("A3"));
        }

        [Fact]
        public void Filter_AddSelectedObject_AddsItsKindLayerColorLinetypeAndItsEnds()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            int asked = 0;
            session.UiRequested += (s, e) =>
            {
                if (e.Name == "FILTER")
                {
                    e.Answer = asked++ == 0 ? "Add Selected Object" : null;
                    e.Handled = true;
                }
            };
            Run(session, "FILTER");
            Assert.Equal("Select object:", session.PromptText);
            session.FeedSelection(new[] { "A1" });

            Assert.False(session.IsRunning);
            Assert.Equal(
                new[]
                {
                    "Object               = Line",
                    "Layer                = Steel",
                    "Color                = 256 - By Layer",
                    "Linetype             = ByLayer",
                    "Line Start           X = 0.0000  Y = 0.0000  Z = 0.0000",
                    "Line End             X = 100.0000  Y = 0.0000  Z = 0.0000",
                },
                session.SelectionFilter.Lines.Select(l => l.Text)
            );
        }

        [Fact]
        public void Filter_WithNoWindow_SaysTheWindowShowsIt()
        {
            CadCommandSession session = Start(Plan(), "FILTER");
            Assert.Equal("Object Selection Filters is shown by the window.", session.History.Last());
        }

        [Fact]
        public void FilterLine_Color_TakesTheFormsAutoCADsListTakes()
        {
            Assert.Equal("1 - Red", new CadFilterLine(CadFilterProperty.Color, "1").Value);
            Assert.Equal("1 - Red", new CadFilterLine(CadFilterProperty.Color, "red").Value);
            Assert.Equal("256 - By Layer", new CadFilterLine(CadFilterProperty.Color, "ByLayer").Value);
            Assert.Equal("0 - By Block", new CadFilterLine(CadFilterProperty.Color, "BYBLOCK").Value);
            Assert.Equal("8", new CadFilterLine(CadFilterProperty.Color, "8").Value);
            ArgumentException refused = Assert.Throws<ArgumentException>(() =>
                new CadFilterLine(CadFilterProperty.Color, "300")
            );
            Assert.StartsWith("Invalid Color number.", refused.Message);
        }

        [Fact]
        public void FilterLine_LayerAndObject_MatchAsAutoCADsFilterDoes()
        {
            CadDocument document = Plan();
            var filter = new CadSelectionFilter();
            filter.Lines.Add(new CadFilterLine(CadFilterProperty.Object, "line"));
            filter.Lines.Add(new CadFilterLine(CadFilterProperty.Layer, "St*"));

            Assert.Equal(new[] { "A1" }, filter.Apply(document, new[] { "A1", "A2", "A3", "A8" }));
        }

        [Fact]
        public void SelectionFilter_NamedLists_AreSavedRestoredAndDeleted()
        {
            var filter = new CadSelectionFilter();
            filter.Lines.Add(new CadFilterLine(CadFilterProperty.Layer, "WALLS"));
            Assert.Equal("Null Filter name not allowed.", filter.SaveAs(" "));
            Assert.Equal("Invalid characters in Filter name.", filter.SaveAs("a*b"));
            Assert.Null(filter.SaveAs("Walls"));
            filter.Lines.Clear();

            Assert.True(filter.Restore("WALLS"));
            Assert.Single(filter.Lines);
            Assert.Equal(new[] { CadSelectionFilter.Unnamed, "Walls" }, filter.Names);
            Assert.Null(filter.DeleteCurrent());
            Assert.Equal("Cannot delete *unnamed Filter list.", filter.DeleteCurrent());
        }

        #endregion

        #region CAL and QUICKCALC

        [Fact]
        public void Cal_AsksForAnExpression_AndWritesItsValue()
        {
            CadCommandSession session = Start(Blank(), "CAL");
            Assert.Equal(">> Expression:", session.PromptText);
            Run(session, "1+2*3");

            Assert.False(session.IsRunning);
            Assert.Equal("7", session.History.Last());
        }

        [Fact]
        public void Cal_AWrongExpression_WritesErrorAndAsksAgain()
        {
            CadCommandSession session = Start(Blank(), "CAL", "sqrt(16)+[1,2]");

            Assert.Equal("Error:", session.History[session.History.Count - 2]);
            Assert.Equal("Operators '+', '-' require operands of the same type", session.History.Last());
            Assert.Equal(">> Expression:", session.PromptText);
        }

        [Fact]
        public void Cal_Cur_AsksForAPointInTheDrawing_AndTheWindowIsGivenTheValue()
        {
            CadCommandSession session = Session(Blank());
            object given = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Name == "CAL")
                {
                    given = e.Payload;
                    e.Handled = true;
                }
            };
            Run(session, "CAL", "dist(cur,cur)");
            Assert.Equal(">> Enter a point:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal(">> Enter a point:", session.PromptText);
            Run(session, P(300, 400));

            Assert.Equal("500", session.History.Last());
            Assert.Equal(500.0, ((CadCalcValue)given).Number, 9);
        }

        [Theory]
        [InlineData("1+2*3", "7")]
        [InlineData("7/2", "3.5")]
        [InlineData("sqrt(16)+2", "6")]
        [InlineData("2^10", "1024")]
        [InlineData("-(3-5)", "2")]
        [InlineData("sin(30)", "0.5")]
        [InlineData("r2d(pi)", "180")]
        [InlineData("round(2.5)+trunc(-2.7)", "1")]
        [InlineData("ang([0,0],[300,400])", "53.1301024")]
        [InlineData("dist([0,0],[300,400])/2", "250")]
        [InlineData("[100,50]+[20,30]", "120,80,0")]
        [InlineData("[1,2,3]*2", "2,4,6")]
        [InlineData("ill([0,0],[10,10],[0,10],[10,0])", "5,5,0")]
        [InlineData("1.5e3", "1500")]
        public void Calculator_Evaluates_AsCalWritesTheValue(string expression, string expected)
        {
            Assert.Equal(expected, CadCalculator.Evaluate(expression).ToString());
        }

        [Theory]
        [InlineData("1/0", "Division by 0")]
        [InlineData("(1+2", "Right parenthesis ')' missing")]
        [InlineData("foo(1)", "Function FOO doesn't exist")]
        [InlineData("sqrt(-1)", "Argument to SQRT cannot be negative")]
        [InlineData("asin(2)", "Argument to ASIN, ACOS must be between -1 and 1")]
        [InlineData("1+", "Unexpected end of expression")]
        [InlineData("2$", "Illegal character '$'")]
        [InlineData("x+1", "AutoLISP variable x has no value")]
        [InlineData("[1,2", "Wrong syntax of vector, maybe ']' missing")]
        [InlineData("dist(1,2)", "Function DIST requires vector argument")]
        [InlineData("ill([0,0],[1,0],[0,1],[1,1])", "The two lines in function ILL do not intersect or are collinear")]
        public void Calculator_AWrongExpression_IsRefusedInAutoCADsWords(string expression, string message)
        {
            CadCalcException refused = Assert.Throws<CadCalcException>(() => CadCalculator.Evaluate(expression));
            Assert.Equal(message, refused.Message);
        }

        [Fact]
        public void CalcValue_ToInput_GivesTextCalReadsBack()
        {
            CadCalcValue point = CadCalculator.Evaluate("[100,50]+[20.5,30]");
            Assert.Equal("[120.5,80,0]", point.ToInput());
            Assert.Equal(point.Point, CadCalculator.Evaluate(point.ToInput()).Point);
            Assert.Equal(2, CadCalculator.PointsPicked("dist(cur, CUR) + curve"));
        }

        [Fact]
        public void QuickCalc_AsksTheWindowForThePalette_AndWithNoneSaysSo()
        {
            CadCommandSession session = Start(Blank(), "QC");
            Assert.Equal("QuickCalc is shown by the window.", session.History.Last());

            bool shown = false;
            session.UiRequested += (s, e) =>
            {
                shown |= e.Name == "QUICKCALC";
                e.Handled = true;
            };
            Run(session, "QUICKCALC");
            Assert.True(shown);
            Assert.False(session.IsRunning);
        }

        #endregion

        #region QUICKPROPERTIES

        [Fact]
        public void QuickProperties_AsksForObjectsWhereNoneAreSelected_ThenGivesThemToThePanel()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            IReadOnlyList<string> given = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Name == "QUICKPROPERTIES")
                {
                    given = (IReadOnlyList<string>)e.Payload;
                    e.Handled = true;
                }
            };
            Run(session, "QP");
            Assert.Equal("Select objects:", session.PromptText);
            session.FeedSelection(new[] { "A1", "A3" });
            session.Enter();

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "A1", "A3" }, given);
            Assert.Equal(2, document.Selection.Handles.Count);
        }

        [Fact]
        public void QuickProperties_TheObjectsSelectedBefore_AreGivenToThePanelWithNoPrompt_AndStaySelected()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A3", "A1" });
            CadCommandSession session = Session(document);
            IReadOnlyList<string> given = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Name == "QUICKPROPERTIES")
                {
                    given = (IReadOnlyList<string>)e.Payload;
                    e.Handled = true;
                }
            };
            Run(session, "QUICKPROPERTIES");

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { "A1", "A3" }, given);
            Assert.Equal(2, document.Selection.Handles.Count);
        }

        [Fact]
        public void QuickProperties_WithNoWindow_SaysTheWindowShowsIt()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "QUICKPROPERTIES");
            session.FeedSelection(new[] { "A1" });
            session.Enter();
            Assert.Equal("Quick Properties is shown by the window.", session.History.Last());
        }

        #endregion
    }
}
