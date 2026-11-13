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
    /// Batch 3 of acad.pgp's 2D commands: SOLID, -BOUNDARY, -HATCH, GRADIENT, HATCHTOBACK, REGION and CLASSICINSERT, in the
    /// words of AutoCAD 2021 (the prompts and the order of the points were read from it by accoreconsole probes).
    /// </summary>
    public class ShapesBatchThreeTests
    {
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

        private static GeoPoint3 P3(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Corners(DxfSolid solid, params GeoPoint3[] expected)
        {
            for (short i = 0; i < expected.Length; i++)
            {
                Near(expected[i], solid.GetPointAt(i));
            }
        }

        #region SOLID

        [Fact]
        public void Solid_AsksFirstSecondThirdAndFourthPoint_AndKeepsTheCornersInTheOrderPicked()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SOLID");
            Assert.Equal("Specify first point:", session.PromptText);
            Run(session, P(0, 0));
            Assert.Equal("Specify second point:", session.PromptText);
            Run(session, P(10, 0));
            Assert.Equal("Specify third point:", session.PromptText);
            Run(session, P(0, 10));
            Assert.Equal("Specify fourth point or <exit>:", session.PromptText);
            Run(session, P(10, 10));

            Assert.Equal("Specify third point:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
            DxfSolid solid = Assert.Single(All<DxfSolid>(document.ModelSpace));
            Corners(solid, P3(0, 0), P3(10, 0), P3(0, 10), P3(10, 10));
        }

        [Fact]
        public void Solid_EnterAtTheFourthPoint_MakesATriangleOfTheThirdCornerTwice()
        {
            CadDocument document = Blank();
            Start(document, "SO", P(0, 0), P(10, 0), P(5, 10), Enter, Enter);

            DxfSolid solid = Assert.Single(All<DxfSolid>(document.ModelSpace));
            Corners(solid, P3(0, 0), P3(10, 0), P3(5, 10), P3(5, 10));
        }

        [Fact]
        public void Solid_GoesOnWithTheThirdAndFourthCornersOfTheLastAsTheFirstAndSecondOfTheNext()
        {
            CadDocument document = Blank();
            Start(document, "SOLID", P(0, 0), P(10, 0), P(0, 10), P(10, 10), P(0, 20), P(10, 20), Enter);

            List<DxfSolid> solids = All<DxfSolid>(document.ModelSpace);
            Assert.Equal(2, solids.Count);
            Corners(solids[0], P3(0, 0), P3(10, 0), P3(0, 10), P3(10, 10));
            Corners(solids[1], P3(0, 10), P3(10, 10), P3(0, 20), P3(10, 20));
        }

        [Fact]
        public void Solid_AfterATriangleTheNextSolidStartsFromTheThirdCornerTwice()
        {
            CadDocument document = Blank();
            Start(document, "SOLID", P(0, 0), P(10, 0), P(5, 10), Enter, P(0, 20), P(10, 20), Enter);

            List<DxfSolid> solids = All<DxfSolid>(document.ModelSpace);
            Assert.Equal(2, solids.Count);
            Corners(solids[1], P3(5, 10), P3(5, 10), P3(0, 20), P3(10, 20));
        }

        [Fact]
        public void Solid_CancelledBeforeTheThirdPoint_MakesNothing_AndEachSolidIsItsOwnUndoStep()
        {
            CadDocument document = Blank();
            Start(document, "SOLID", P(0, 0), P(10, 0), Enter);
            Assert.Empty(All<DxfSolid>(document.ModelSpace));

            Start(document, "SOLID", P(0, 0), P(10, 0), P(0, 10), P(10, 10), P(0, 20), P(10, 20), Enter);
            Assert.Equal(2, All<DxfSolid>(document.ModelSpace).Count);
            document.Undo();
            Assert.Empty(All<DxfSolid>(document.ModelSpace));
        }

        [Fact]
        public void Solid_IsOnTheCurrentLayer()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Fill" });
            document.Database.Clayer = "Fill";
            Start(document, "SOLID", P(0, 0), P(10, 0), P(5, 10), Enter, Enter);

            Assert.Equal("Fill", Assert.Single(All<DxfSolid>(document.ModelSpace)).Layer);
        }

        #endregion

        #region -BOUNDARY

        [Fact]
        public void BoundaryTyped_AsksForAnInternalPointOrAdvancedOptions_AndMakesTheAreaAndItsIslandAsPolylines()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            CadCommandSession session = Start(document, "-BO");
            Assert.Equal("Specify internal point or [Advanced options]:", session.PromptText);

            Run(session, P(10, 10));
            Assert.Contains("BOUNDARY created 2 polylines", session.History);
            Assert.Equal("Specify internal point or [Advanced options]:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);
            Assert.Equal(2, All<DxfPolyline>(document.ModelSpace).Count);
        }

        [Fact]
        public void BoundaryTyped_AdvancedIslandDetectionOff_MakesTheOuterLoopAlone_AndBoundaryKeepsThatSetting()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            CadCommandSession session = Start(document, "-BOUNDARY", "A");
            Assert.Equal("Enter an option [Island detection/Object type]:", session.PromptText);
            Run(session, "I");
            Assert.Equal("Do you want island detection? [Yes/No] <Y>:", session.PromptText);
            Run(session, "N");
            Assert.Equal("Enter an option [Island detection/Object type]:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify internal point or [Advanced options]:", session.PromptText);
            Run(session, P(10, 10), Enter);

            Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Contains("BOUNDARY created 1 polyline", session.History);

            // The same session's BOUNDARY keeps the setting: one polyline more, the outer loop alone.
            Run(session, "BO", P(10, 10), Enter);
            Assert.Equal(2, All<DxfPolyline>(document.ModelSpace).Count);
        }

        [Fact]
        public void BoundaryTyped_ObjectTypeRegion_SaysItCannotWriteTheRegion_AndMakesNothing()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-BO", "A", "O");
            Assert.Equal("Enter type of boundary object [Region/Polyline] <Polyline>:", session.PromptText);
            Run(session, "R", Enter, P(10, 10), Enter);

            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
            Assert.Contains(session.History, l => l.Contains("ACIS"));
        }

        [Fact]
        public void BoundaryTyped_APointOutsideAnyArea_SaysNoValidBoundary()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-BO", P(500, 500), Enter);

            Assert.Contains("Valid hatch boundary not found.", session.History);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
        }

        #endregion

        #region -HATCH

        private static DxfHatch OnlyHatch(CadDocument document) => Assert.Single(All<DxfHatch>(document.ModelSpace));

        [Fact]
        public void HatchTyped_SaysTheCurrentPattern_AsksForAPointOrAKeyword_AndMakesAHatchOfTheAreaAndItsIsland()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            CadCommandSession session = Start(document, "-H");
            Assert.Contains("Current hatch pattern:  ANSI31", session.History);
            Assert.Equal(
                "Specify internal point or [Properties/Select objects/draW boundary/remove Boundaries/Advanced/DRaw order/Origin/ANnotative/hatch COlor/LAyer/Transparency]:",
                session.PromptText
            );

            Run(session, P(10, 10));
            Assert.Contains("Analyzing the selected data...", session.History);
            Assert.True(session.IsRunning);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(2, hatch.NumberOfLoops);
            Assert.Equal("ANSI31", hatch.PatternName);
            Assert.Equal("-HATCH", document.NextUndo.Name);
        }

        [Fact]
        public void HatchTyped_Properties_AsksThePatternAndGoesBackToThePrompt()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "P");
            Assert.Equal("Enter a pattern name or [?/Solid/User defined/Gradient] <ANSI31>:", session.PromptText);
            Run(session, "ANSI37", "2", Enter);

            Assert.Equal(
                "Specify internal point or [Properties/Select objects/draW boundary/remove Boundaries/Advanced/DRaw order/Origin/ANnotative/hatch COlor/LAyer/Transparency]:",
                session.PromptText
            );
            Run(session, P(10, 10), Enter);
            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal("ANSI37", hatch.PatternName);
            Assert.Equal(2.0, hatch.PatternScale);
        }

        [Fact]
        public void HatchTyped_SelectObjects_HatchesTheClosedObjectsSelected()
        {
            CadDocument document = Blank();
            var circle = new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 20);
            document.Database.ModelSpace.AppendEntity(circle);
            CadCommandSession session = Start(document, "-HATCH", "S", new[] { circle.Handle }, Enter, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(1, OnlyHatch(document).NumberOfLoops);
        }

        [Fact]
        public void HatchTyped_AdvancedAsksAsAutoCadDoes_AndIslandDetectionOffHatchesTheOuterLoopAlone()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            CadCommandSession session = Start(document, "-HATCH", "A");
            Assert.Equal(
                "Enter an option [Boundary set/Retain boundary/Island detection/Style/Associativity/Gap tolerance/separate Hatches]:",
                session.PromptText
            );
            Run(session, "I");
            Assert.Equal("Do you want island detection? [Yes/No] <Y>:", session.PromptText);
            Run(session, "N");
            Run(session, "S");
            Assert.Equal("Enter hatching style [Ignore/Outer/Normal] <Normal>:", session.PromptText);
            Run(session, Enter, "A");
            Assert.Equal("Do you want associativity? [Yes/No] <Y>:", session.PromptText);
            Run(session, "N", Enter, P(10, 10), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.False(hatch.Associative);
        }

        [Fact]
        public void HatchTyped_StyleIgnore_IsWrittenToTheHatch()
        {
            CadDocument document = Squares(0, 0, 100, 20, 20, 60);
            Start(document, "-HATCH", "A", "S", "I", Enter, P(10, 10), Enter);

            Assert.Equal(DxfHatchStyle.Ignore, OnlyHatch(document).HatchStyle);
        }

        [Fact]
        public void HatchTyped_RetainBoundary_KeepsPolylinesOfTheLoops()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "A", "R");
            Assert.Equal("Retain derived boundaries? [Yes/No] <N>:", session.PromptText);
            Run(session, "Y", Enter, P(10, 10), Enter);

            Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(1, OnlyHatch(document).NumberOfLoops);
        }

        [Fact]
        public void HatchTyped_SeparateHatches_MakesOneHatchOfEachPointPicked()
        {
            CadDocument document = Squares(0, 0, 100, 200, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "A", "H");
            Assert.Equal("Create separate hatches? [Yes/No] <N>:", session.PromptText);
            Run(session, "Y", Enter, P(10, 10), P(210, 10), Enter);

            Assert.Equal(2, All<DxfHatch>(document.ModelSpace).Count);
        }

        [Fact]
        public void HatchTyped_ByDefaultOneHatchHoldsEveryPointPicked()
        {
            CadDocument document = Squares(0, 0, 100, 200, 0, 100);
            Start(document, "-HATCH", P(10, 10), P(210, 10), Enter);

            Assert.Equal(2, OnlyHatch(document).NumberOfLoops);
        }

        [Fact]
        public void HatchTyped_DrawOrder_DefaultsToBehindTheBoundary_AndTakesTheFiveChoices()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "DR");
            Assert.Equal(
                "Enter draw order [do Not assign/send to Back/bring to Front/send beHind boundary/bring in front of bounDary] <send beHind boundary>:",
                session.PromptText
            );
            Run(session, Enter, P(10, 10), Enter);

            // The hatch lies under the lowest of its four boundary lines.
            List<DxfEntity> order = document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().ToList();
            Assert.IsType<DxfHatch>(order[0]);
            Assert.Equal(5, order.Count);

            document = Squares(0, 0, 100);
            Start(document, "-HATCH", "DR", "F", P(10, 10), Enter);
            order = document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().ToList();
            Assert.IsType<DxfHatch>(order[order.Count - 1]);

            document = Squares(0, 0, 100);
            Start(document, "-HATCH", "DR", "B", P(10, 10), Enter);
            order = document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().ToList();
            Assert.IsType<DxfHatch>(order[0]);

            document = Squares(0, 0, 100);
            Start(document, "-HATCH", "DR", "D", P(10, 10), Enter);
            order = document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().ToList();
            Assert.IsType<DxfHatch>(order[order.Count - 1]);
        }

        [Fact]
        public void HatchTyped_AGradientIsAskedItsNameAngleAndCentring_AsAutoCadAsksThem()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "P", "G");
            Assert.Equal("Enter a gradient name or [?/Single color/Two color] <GR_LINEAR>:", session.PromptText);
            Run(session, "?");
            Assert.Contains(session.History, l => l.Contains("GR_CYLIN"));
            Run(session, "GR_CYLIN");
            Assert.Equal("Specify an angle for the gradient <0>:", session.PromptText);
            Run(session, "45");
            Assert.Equal("Center the gradient [Yes/No] <Yes>:", session.PromptText);
            Run(session, "N", P(10, 10), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.True(hatch.IsGradient);
            Assert.Equal("CYLINDER", hatch.GradientName);
            Assert.Equal(Math.PI / 4.0, hatch.GradientAngle, 12);
            Assert.Equal(0.25f, hatch.GradientShift);
        }

        [Fact]
        public void HatchTyped_TwoColorAsksBothColours()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "-HATCH", "P", "G", "T");
            Assert.Equal("New gradient color [Truecolor/COlorbook] <0,0,255>:", session.PromptText);
            Run(session, "1");
            Assert.Equal("New gradient second color:[Truecolor/COlorbook] <255,255,153>:", session.PromptText);
            Run(session, "3", P(10, 10), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(255, hatch.GetGradientColors()[0].Color.Red);
            Assert.Equal(255, hatch.GetGradientColors()[1].Color.Green);
        }

        [Fact]
        public void HatchTyped_UndoTakesTheLastPointBack()
        {
            CadDocument document = Squares(0, 0, 100, 200, 0, 100);
            Start(document, "-HATCH", P(10, 10), P(210, 10), "U", Enter);

            Assert.Equal(1, OnlyHatch(document).NumberOfLoops);
        }

        #endregion

        #region GRADIENT

        [Fact]
        public void Gradient_HatchesWithAGradientAndAsksTheSamePrompts()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "GD");
            Assert.Equal("Pick internal point or [Select objects/Undo/seTtings]:", session.PromptText);
            Run(session, P(10, 10), Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.True(hatch.IsGradient);
            Assert.Equal("LINEAR", hatch.GradientName);
            Assert.Equal(CadHatchType.Gradient, document.HatchSettings.Type);
        }

        #endregion

        #region HATCHTOBACK

        [Fact]
        public void HatchToBack_SendsEveryHatchOfTheSpaceBehindTheOtherObjects()
        {
            CadDocument document = Squares(0, 0, 100);
            Start(document, "HATCH", P(10, 10), Enter);
            document.Database.ModelSpace.AppendEntity(
                new DxfCircle(new GeoPoint3(500, 500, 0), new GeoVector3(0, 0, 1), 5)
            );
            Assert.IsType<DxfLine>(document.ModelSpace.Block.Cast<DxfEntity>().First());

            CadCommandSession session = Start(document, "HATCHTOBACK");

            Assert.False(session.IsRunning);
            List<DxfEntity> order = document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().ToList();
            Assert.IsType<DxfHatch>(order[0]);
            Assert.Contains("1 hatch object(s) sent to back.", session.History);
            Assert.Equal("HATCHTOBACK", document.NextUndo.Name);
        }

        [Fact]
        public void HatchToBack_OfADrawingWithNoHatch_SaysSoAndChangesNothing()
        {
            CadDocument document = Squares(0, 0, 100);
            CadCommandSession session = Start(document, "HATCHTOBACK");

            Assert.Contains("No hatch objects found.", session.History);
        }

        #endregion

        #region REGION

        [Fact]
        public void Region_AsksForObjects_CountsTheLoopsAndSaysItCannotWriteTheRegions()
        {
            CadDocument document = Squares(0, 0, 100);
            var circle = new DxfCircle(new GeoPoint3(500, 500, 0), new GeoVector3(0, 0, 1), 5);
            document.Database.ModelSpace.AppendEntity(circle);
            CadCommandSession session = Start(document, "REG");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { circle.Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Contains("1 loop extracted.", session.History);
            Assert.Contains(session.History, l => l.Contains("ACIS"));
            Assert.Equal(4, All<DxfLine>(document.ModelSpace).Count);
        }

        [Fact]
        public void Region_OfLinesThatDoNotClose_SaysNoClosedLoop()
        {
            CadDocument document = Squares(0, 0, 100);
            DxfLine line = All<DxfLine>(document.ModelSpace)[0];
            CadCommandSession session = Start(document, "REGION", new[] { line.Handle }, Enter);

            Assert.Contains("0 loops extracted.", session.History);
            Assert.Contains("0 Regions created.", session.History);
        }

        #endregion

        #region SPLINEDIT

        /// <summary>The spline AutoCAD 2021 was probed with: the cubic through (0, 0), (10, 10), (20, 0), (30, 10) and (40, 0).</summary>
        private static DxfSpline FitSpline(CadDocument document)
        {
            var spline = new DxfSpline();
            document.Database.ModelSpace.AppendEntity(spline);
            spline.FitData = new DxfFitData(new[] { P3(0, 0), P3(10, 10), P3(20, 0), P3(30, 10), P3(40, 0) }, 3, 0.0);
            return spline;
        }

        [Fact]
        public void SplineEdit_AsksForASpline_AgainWhereThePickIsNone_AndOffersCloseForASplineOfFitPoints()
        {
            CadDocument document = Squares(0, 0, 100);
            DxfLine line = All<DxfLine>(document.ModelSpace)[0];
            DxfSpline spline = FitSpline(document);
            CadCommandSession session = Start(document, "SPE");
            Assert.Equal("Select spline:", session.PromptText);

            Run(session, (object)new[] { line.Handle });
            Assert.Equal("Select spline:", session.PromptText);
            Run(session, (object)new[] { spline.Handle });
            Assert.Equal(
                "Enter an option [Fit data/Close/Join/Move vertex/Refine/rEverse/convert to Polyline/Undo/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void SplineEdit_Close_MakesTheClosedPeriodicCubicAutoCadMakes_AndOpenMakesTheOpenOneAgain()
        {
            CadDocument document = Blank();
            DxfSpline spline = FitSpline(document);
            double[] open = spline.NurbsData.GetControlPoints().SelectMany(p => new[] { p.X, p.Y }).ToArray();
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "C");

            Assert.Equal(
                "Enter an option [Fit data/Open/Move vertex/Refine/rEverse/convert to Polyline/Undo/eXit] <eXit>:",
                session.PromptText
            );
            Assert.True(spline.Closed);
            Assert.True(spline.IsPeriodic);
            Assert.Equal(6, spline.NumFitPoints);

            // The record AutoCAD 2021 wrote (R2000 DXF) for the same spline closed: 8 control points, 12 knots, 70 of 3115.
            double[] expected =
            {
                0,
                0,
                0.2574052769362165,
                4.753860797980345,
                11.31825488131305,
                17.62306960100983,
                20,
                -8.811534800504912,
                28.68174511868695,
                17.62306960100983,
                55.3849982449426,
                -13.44594882827518,
                -0.7280520673391955,
                -13.44594882827517,
                0,
                0,
            };
            double[] closed = spline.NurbsData.GetControlPoints().SelectMany(p => new[] { p.X, p.Y }).ToArray();

            Assert.Equal(expected.Length, closed.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], closed[i], 5);
            }

            Run(session, "O");
            Assert.False(spline.Closed);
            Assert.Equal(7, spline.NurbsData.GetControlPoints().Count);
            double[] again = spline.NurbsData.GetControlPoints().SelectMany(p => new[] { p.X, p.Y }).ToArray();

            for (int i = 0; i < open.Length; i++)
            {
                Assert.Equal(open[i], again[i], 9);
            }

            Run(session, Enter);
            Assert.Equal("SPLINEDIT", document.NextUndo.Name);
            document.Undo();
            Assert.False(((DxfSpline)document.FindEntity(spline.Handle)).Closed);
        }

        [Fact]
        public void SplineEdit_Reverse_TurnsTheSplineRound_AndUndoTakesItBack()
        {
            CadDocument document = Blank();
            DxfSpline spline = FitSpline(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "E");

            Assert.Contains("Spline has been reversed.", session.History);
            Near(P3(40, 0), spline.GetControlPointAt(0));
            Near(P3(40, 0), spline.GetFitPointAt(0));

            Run(session, "U");
            Near(P3(0, 0), ((DxfSpline)document.FindEntity(spline.Handle)).GetControlPointAt(0));
            Run(session, Enter);
        }

        [Fact]
        public void SplineEdit_MoveVertex_MovesTheMarkedControlVertex_AndTheSplineLosesItsFitData()
        {
            CadDocument document = Blank();
            DxfSpline spline = FitSpline(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "M");
            Assert.Equal("Specify new location or [Next/Previous/Select point/eXit] <N>:", session.PromptText);

            Run(session, P(3, -3));
            Near(P3(3, -3), spline.GetControlPointAt(0));
            Assert.False(spline.HasFitData);

            // The vertex stays marked: a second point moves it again, as AutoCAD 2021 did.
            Run(session, P(6, -6));
            Near(P3(6, -6), spline.GetControlPointAt(0));

            Run(session, "N", P(5, 5));
            Near(P3(5, 5), spline.GetControlPointAt(1));
            Run(session, "X", Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void SplineEdit_ConvertToPolyline_AsksThePrecisionAndReplacesTheSplineByLines()
        {
            CadDocument document = Blank();
            DxfSpline spline = FitSpline(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "P");
            Assert.Equal("Specify a precision <10>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
            DxfPolyline polyline = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.False(polyline.Closed);

            // AutoCAD 2021 made 53 vertices of lines of this spline at precision 10.
            Assert.Equal(53, polyline.NumberOfVertices);
            Near(P3(0, 0), P3(polyline.GetPoint2dAt(0).X, polyline.GetPoint2dAt(0).Y));
        }

        #endregion

        #region CLASSICINSERT

        [Fact]
        public void ClassicInsert_AsksAsInsertDoes_AndDDInsertIsItsAlias()
        {
            CadDocument document = Blank();
            CadCommandSession insert = Start(document, "INSERT");
            CadCommandSession classic = Start(document, "DDINSERT");

            Assert.NotNull(CadCommandRegistry.CreateDefault().Find("CLASSICINSERT"));
            Assert.Equal(insert.PromptText, classic.PromptText);
        }

        #endregion

        #region Aliases

        [Theory]
        [InlineData("SO", "SOLID")]
        [InlineData("-BO", "-BOUNDARY")]
        [InlineData("-H", "-HATCH")]
        [InlineData("GD", "GRADIENT")]
        [InlineData("HB", "HATCHTOBACK")]
        [InlineData("REG", "REGION")]
        [InlineData("SPE", "SPLINEDIT")]
        [InlineData("DDINSERT", "CLASSICINSERT")]
        public void TheAliasesOfAcadPgp_ReachTheCommand(string alias, string name)
        {
            Assert.Equal(name, CadCommandRegistry.CreateDefault().Find(alias)?.Name);
        }

        #endregion
    }
}
