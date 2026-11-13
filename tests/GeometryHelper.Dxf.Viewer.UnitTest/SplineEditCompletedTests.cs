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
    /// SPLINEDIT completed (round 2, item R2.3): Fit data (Add, Close/Open, Delete, Kink, Move, Purge, Tangents, toLerance),
    /// Join, Refine (Add, Delete, Elevate order, Move, Weight) and the Close and Open of a spline of control points, in the
    /// words of AutoCAD 2021. The numbers are the records AutoCAD 2021 wrote (accoreconsole, R2018 DXF, the same spline
    /// through (0, 0), (10, 10), (20, 0), (30, 10) and (40, 0) each time) and are met to 1E-5.
    /// </summary>
    public class SplineEditCompletedTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static GeoPoint3 P3(double x, double y) => new GeoPoint3(x, y, 0);

        private static DxfSpline Fit(CadDocument document, params double[] xy)
        {
            var spline = new DxfSpline();
            document.Database.ModelSpace.AppendEntity(spline);
            var points = new List<GeoPoint3>();

            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                points.Add(P3(xy[i], xy[i + 1]));
            }

            spline.FitData = new DxfFitData(points, 3, 0.0);
            return spline;
        }

        /// <summary>The spline AutoCAD 2021 was probed with: the cubic through (0, 0), (10, 10), (20, 0), (30, 10) and (40, 0).</summary>
        private static DxfSpline Probe(CadDocument document) => Fit(document, 0, 0, 10, 10, 20, 0, 30, 10, 40, 0);

        private static void Matches(DxfSpline spline, double[] x, double[] y, double[] knots)
        {
            DxfNurbsData data = spline.NurbsData;
            IReadOnlyList<GeoPoint3> points = data.GetControlPoints();
            Assert.Equal(x.Length, points.Count);

            for (int i = 0; i < x.Length; i++)
            {
                Assert.Equal(x[i], points[i].X, 5);
                Assert.Equal(y[i], points[i].Y, 5);
            }

            IReadOnlyList<double> have = data.GetKnots();
            Assert.Equal(knots.Length, have.Count);

            for (int i = 0; i < knots.Length; i++)
            {
                Assert.Equal(knots[i], have[i], 5);
            }
        }

        private static void FitPointsAre(DxfSpline spline, params double[] xy)
        {
            Assert.Equal(xy.Length / 2, spline.NumFitPoints);

            for (int i = 0; i < xy.Length / 2; i++)
            {
                Near(P3(xy[2 * i], xy[2 * i + 1]), spline.GetFitPointAt(i), 1E-9);
            }
        }

        #region The menus

        [Fact]
        public void SplineEdit_OffersFitDataCloseJoinAndRefine_ForAnOpenSplineOfFitPoints()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle });
            Assert.Equal(
                "Enter an option [Fit data/Close/Join/Move vertex/Refine/rEverse/convert to Polyline/Undo/eXit] <eXit>:",
                session.PromptText
            );
        }

        [Fact]
        public void SplineEdit_OffersOpenAndNoJoin_ForAClosedSplineOfFitPoints()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "C");
            Assert.Equal(
                "Enter an option [Fit data/Open/Move vertex/Refine/rEverse/convert to Polyline/Undo/eXit] <eXit>:",
                session.PromptText
            );
        }

        [Fact]
        public void SplineEdit_OffersNoFitData_ForASplineOfControlPoints()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            spline.PurgeFitData();
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle });
            Assert.Equal(
                "Enter an option [Close/Join/Move vertex/Refine/rEverse/convert to Polyline/Undo/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "C");
            Assert.Equal(
                "Enter an option [Open/Move vertex/Refine/rEverse/convert to Polyline/Undo/eXit] <eXit>:",
                session.PromptText
            );
        }

        [Fact]
        public void SplineEdit_FitData_AsksTheFitDataOptions_AndOffersOpenOnceClosed()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F");
            Assert.Equal(
                "Enter a fit data option [Add/Close/Delete/Kink/Move/Purge/Tangents/toLerance/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "C");
            Assert.True(spline.Closed);
            Assert.Equal(
                "Enter a fit data option [Add/Open/Delete/Kink/Move/Purge/Tangents/toLerance/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "X");
            Assert.StartsWith("Enter an option [Fit data/Open/", session.PromptText);
        }

        #endregion

        #region Fit data

        [Fact]
        public void FitData_Add_PutsEachPointAfterTheOnePicked_AndFitsTheCubicAutoCadFitted()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "A");
            Assert.Equal("Specify existing fit point on spline <exit>:", session.PromptText);
            Run(session, P(10, 10));
            Assert.Equal("Specify new fit point to add <exit>:", session.PromptText);
            Run(session, P(15, 8));
            Assert.Equal("Specify new fit point to add <exit>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify existing fit point on spline <exit>:", session.PromptText);

            FitPointsAre(spline, 0, 0, 10, 10, 15, 8, 20, 0, 30, 10, 40, 0);
            Matches(
                spline,
                new[]
                {
                    0.0,
                    2.795492477961791,
                    6.655476777488896,
                    16.94280978710257,
                    19.76193739676469,
                    30.39121364552439,
                    36.79707121517479,
                    40.0,
                },
                new[]
                {
                    0.0,
                    4.920791376291104,
                    11.71536428373845,
                    8.326052818841377,
                    -6.901000442297234,
                    17.48628146549088,
                    5.82876048849696,
                    0.0,
                },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    14.14213562373095,
                    19.52730043086545,
                    28.96128156292206,
                    43.10341718665301,
                    57.24555281038396,
                    57.24555281038396,
                    57.24555281038396,
                    57.24555281038396,
                }
            );
        }

        [Fact]
        public void FitData_Add_ChainsTheNewPointsAfterOneAnother()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            Start(
                document,
                "SPLINEDIT",
                new[] { spline.Handle },
                "F",
                "A",
                P(10, 10),
                P(12, 5),
                P(14, 6),
                Enter,
                Enter
            );

            FitPointsAre(spline, 0, 0, 10, 10, 12, 5, 14, 6, 20, 0, 30, 10, 40, 0);
        }

        [Fact]
        public void FitData_Add_AtTheFirstPoint_AsksAfterOrBefore_AndAPointTypedGoesBefore()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(
                document,
                "SPLINEDIT",
                new[] { spline.Handle },
                "F",
                "A",
                P(0, 0)
            );
            Assert.Equal("Specify new point or [After/Before] <exit>:", session.PromptText);
            Run(session, P(3, 3));
            Assert.Equal("Specify new fit point to add <exit>:", session.PromptText);
            Run(session, Enter, Enter);

            // AutoCAD 2021: (3, 3) first, the cubic of its record 8 control points from (3, 3) to (40, 0).
            FitPointsAre(spline, 3, 3, 0, 0, 10, 10, 20, 0, 30, 10, 40, 0);
            Matches(
                spline,
                new[]
                {
                    3.0,
                    1.742725880551301,
                    -3.705461970393057,
                    12.29709035222052,
                    19.38744257274119,
                    30.15313935681471,
                    36.71771311893823,
                    40.0,
                },
                new[]
                {
                    3.0,
                    1.65084226646248,
                    -4.195507912200101,
                    20.26033690658499,
                    -9.402756508422662,
                    17.35068912710566,
                    5.783563042368556,
                    0.0,
                },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    4.242640687119284,
                    18.38477631085023,
                    32.52691193458119,
                    46.66904755831214,
                    60.81118318204309,
                    60.81118318204309,
                    60.81118318204309,
                    60.81118318204309,
                }
            );
        }

        [Fact]
        public void FitData_Add_AtTheFirstPoint_BeforeChainsEachPointBeforeTheOneBefore_AndAfterPutsThemAfter()
        {
            CadDocument document = Blank();
            DxfSpline before = Probe(document);
            Start(document, "SPLINEDIT", new[] { before.Handle }, "F", "A", P(0, 0), "B", P(-3, 3), P(-5, 5), Enter, Enter);
            FitPointsAre(before, -5, 5, -3, 3, 0, 0, 10, 10, 20, 0, 30, 10, 40, 0);

            CadDocument other = Blank();
            DxfSpline after = Probe(other);
            Start(other, "SPLINEDIT", new[] { after.Handle }, "F", "A", P(0, 0), "A", P(-3, -3), Enter, Enter);
            FitPointsAre(after, 0, 0, -3, -3, 10, 10, 20, 0, 30, 10, 40, 0);
        }

        [Fact]
        public void FitData_Add_ASpotThatIsNoFitPoint_Says_NotAFitPoint()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "A", P(5, 50));
            Assert.Contains("Not a fit point.", session.History);
            Assert.Equal("Specify existing fit point on spline <exit>:", session.PromptText);
        }

        [Fact]
        public void FitData_Delete_TakesTheFitPointPickedOut_AndFitsTheCubicAutoCadFitted()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "D", P(20, 0));
            Assert.Equal("Specify existing fit point on spline <exit>:", session.PromptText);

            FitPointsAre(spline, 0, 0, 10, 10, 30, 10, 40, 0);
            Matches(
                spline,
                new[] { 0.0, 2.928932188134525, 10.0, 30.0, 37.07106781186548, 40.0 },
                new[] { 0.0, 3.867295401695068, 13.20377241017041, 13.2037724101704, 3.867295401695067, 0.0 },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    14.14213562373095,
                    34.14213562373095,
                    48.2842712474619,
                    48.2842712474619,
                    48.2842712474619,
                    48.2842712474619,
                }
            );
        }

        [Fact]
        public void FitData_Kink_AtAFitPoint_PutsItsKnotInThreeTimes_AndTheFourthAMillionthPast_AsAutoCadWroteIt()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "K");
            Assert.Equal("Specify a point on the spline <exit>:", session.PromptText);
            Run(session, P(20, 0));

            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 10.0, 16.66666666666666, 20.0, 20.0, 23.33333333333333, 30.0, 36.66666666666666, 40.0 },
                new[] { 0.0, 5.714285714285712, 17.14285714285714, 0.0, 0.0, 0.0, 0.0, 17.14285714285714, 5.714285714285713, 0.0 },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    14.14213562373095,
                    28.2842712474619,
                    28.2842712474619,
                    28.2842712474619,
                    28.2842712574619,
                    42.42640687119285,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                }
            );
            Assert.True(spline.HasFitData);
        }

        [Fact]
        public void FitData_Kink_AtAPointBetweenTheFitPoints_PutsAKnotThereThreeTimes()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "K", P(23.333333333333336, 2.2751322751322749));

            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 10.0, 17.77777777777777, 22.22222222222222, 23.33333333333333, 25.55555555555556, 31.11111111111112, 36.66666666666666, 40.0 },
                new[] { 0.0, 5.714285714285712, 17.14285714285714, -2.857142857142856, 0.9523809523809527, 2.275132275132275, 4.92063492063492, 15.23809523809524, 5.714285714285713, 0.0 },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    14.14213562373095,
                    28.2842712474619,
                    32.99831645537222,
                    32.99831645537222,
                    32.99831645537222,
                    42.42640687119285,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                }
            );
        }

        [Fact]
        public void FitData_Move_MovesTheMarkedFitPoint_KeepsItMarked_AndNextGoesOn()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "M");
            Assert.Equal("Specify new location or [Next/Previous/Select point/eXit] <N>:", session.PromptText);
            Run(session, P(5, 5), "N", P(12, 3), "N", P(25, 3));
            Assert.True(spline.HasFitData);
            FitPointsAre(spline, 5, 5, 12, 3, 25, 3, 30, 10, 40, 0);
            Matches(
                spline,
                new[] { 5.0, 7.247723773239432, 13.50917941268843, 25.26497897490332, 30.42476580566914, 36.32890335783717, 40.0 },
                new[] { 5.0, 4.367039480409767, 2.603809379753331, -1.024868187991226, 15.39842936937144, 5.903680391018529, 0.0 },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    7.280109889280518,
                    20.28010988928052,
                    28.88243515632315,
                    43.02457078005409,
                    43.02457078005409,
                    43.02457078005409,
                    43.02457078005409,
                }
            );
            Run(session, "X", "X", "X");
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void FitData_Move_SelectPoint_TakesTheFitPointNearestAPick_AndSaysNotAFitPointForNone()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "M", "S", P(5, 50));
            Assert.Contains("Not a fit point.", session.History);
            Run(session, P(30, 10));
            Assert.Equal("Specify new location or [Next/Previous/Select point/eXit] <N>:", session.PromptText);
            Run(session, P(30, 12));
            FitPointsAre(spline, 0, 0, 10, 10, 20, 0, 30, 12, 40, 0);
        }

        [Fact]
        public void FitData_Purge_TakesTheFitDataOut_AndGoesBackToTheTopMenuWithoutFitData()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "P");

            Assert.False(spline.HasFitData);
            Assert.Equal(
                "Enter an option [Close/Join/Move vertex/Refine/rEverse/convert to Polyline/Undo/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "U");
            Assert.True(((DxfSpline)document.FindEntity(spline.Handle)).HasFitData);
        }

        [Fact]
        public void FitData_Tangents_GivenAtBothEnds_AreThePointToTheStartAndTheEndToThePoint()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "T");
            Assert.Equal("Specify start tangent or [System default]:", session.PromptText);
            Run(session, P(1, 1));
            Assert.Equal("Specify end tangent or [System default]:", session.PromptText);
            Run(session, P(45, 0));

            double r = Math.Sqrt(0.5);
            Near(P3(-r, -r), new GeoPoint3(spline.StartFitTangent.X, spline.StartFitTangent.Y, 0), 1E-9);
            Near(P3(1, 0), new GeoPoint3(spline.EndFitTangent.X, spline.EndFitTangent.Y, 0), 1E-9);
            Matches(
                spline,
                new[] { 0.0, -3.333333333333333, 13.14454923361585, 18.99407768234454, 30.87914003700598, 35.28595479208969, 40.0 },
                new[] { 0.0, -3.333333333333333, 21.54761904761905, -10.41666666666666, 20.11904761904762, 0.0, 0.0 },
                new[] { 0.0, 0.0, 0.0, 0.0, 14.14213562373095, 28.2842712474619, 42.42640687119285, 56.5685424949238, 56.5685424949238, 56.5685424949238, 56.5685424949238 }
            );
        }

        [Fact]
        public void FitData_Tangents_SystemDefaultAtTheStart_LeavesItFree_AndAtTheEndTakesBothAway()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "T", "S", P(45, 0));
            Matches(
                spline,
                new[] { 0.0, 3.347567476370208, 10.04270242911062, 19.82919028355748, 30.64053643665943, 35.28595479208969, 40.0 },
                new[] { 0.0, 5.773195876288658, 17.31958762886598, -9.278350515463916, 19.79381443298969, 0.0, 0.0 },
                new[] { 0.0, 0.0, 0.0, 0.0, 14.14213562373095, 28.2842712474619, 42.42640687119285, 56.5685424949238, 56.5685424949238, 56.5685424949238, 56.5685424949238 }
            );

            CadDocument other = Blank();
            DxfSpline plain = Probe(other);
            double[] natural = plain.NurbsData.GetControlPoints().SelectMany(p => new[] { p.X, p.Y }).ToArray();
            Start(other, "SPLINEDIT", new[] { plain.Handle }, "F", "T", P(-5, -5), "S");
            double[] same = plain.NurbsData.GetControlPoints().SelectMany(p => new[] { p.X, p.Y }).ToArray();

            for (int i = 0; i < natural.Length; i++)
            {
                Assert.Equal(natural[i], same[i], 9);
            }
        }

        [Fact]
        public void FitData_ToLerance_AsksTheFitTolerance_AndSetsIt()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            spline.FitTolerance = 1E-10;
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "F", "L");
            Assert.Equal("Enter fit tolerance <1.0000E-10>:", session.PromptText);
            Run(session, "0.5");
            Assert.Equal(0.5, spline.FitTolerance, 9);
            Assert.StartsWith("Enter a fit data option", session.PromptText);
        }

        [Fact]
        public void FitData_Steps_AreTakenBackOneByOneByUndo_AndTheWholeRunIsOneStep()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(
                document,
                "SPLINEDIT",
                new[] { spline.Handle },
                "F",
                "D",
                P(20, 0),
                Enter,
                "X"
            );
            Assert.Equal(4, spline.NumFitPoints);
            Run(session, "U");
            Assert.Equal(5, ((DxfSpline)document.FindEntity(spline.Handle)).NumFitPoints);
            Run(session, "F", "A", P(10, 10), P(15, 8), Enter, Enter, "X", Enter);
            Assert.False(session.IsRunning);
            Assert.Equal("SPLINEDIT", document.NextUndo.Name);
            document.Undo();
            Assert.Equal(5, ((DxfSpline)document.FindEntity(spline.Handle)).NumFitPoints);
        }

        #endregion

        #region Join

        [Fact]
        public void Join_TakesALineThatMeetsTheEnd_AsAutoCadJoinedIt_AndSaysOneObjectJoinedToSource()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            var line = new DxfLine(P3(40, 0), P3(60, 10));
            document.Database.ModelSpace.AppendEntity(line);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "J");
            Assert.Equal("Select any open curves to join to source:", session.PromptText);
            Run(session, new[] { line.Handle }, Enter);

            Assert.Contains("1 object joined to source", session.History);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.False(spline.HasFitData);
            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 10.0, 20.0, 30.0, 36.66666666666666, 40.0, 46.66666666666666, 53.33333333333332, 59.99999999999999 },
                new[] { 0.0, 5.714285714285712, 17.14285714285714, -8.571428571428569, 17.14285714285714, 5.714285714285713, 0.0, 3.333333333333333, 6.666666666666666, 10.0 },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    14.14213562373095,
                    28.2842712474619,
                    42.42640687119285,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                    57.56854249492381,
                    57.56854249492381,
                    57.56854249492381,
                    57.56854249492381,
                }
            );
            Run(session, "U");
            Assert.Single(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void Join_ALineApartFromTheEnd_IsDiscarded_AsAutoCadSaid()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            var line = new DxfLine(P3(100, 100), P3(120, 120));
            document.Database.ModelSpace.AppendEntity(line);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "J", new[] { line.Handle }, Enter);

            Assert.Contains("0 objects joined to source, 1 object discarded from the operation", session.History);
            Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.True(spline.HasFitData);
        }

        [Fact]
        public void Join_IsNotOffered_ToAClosedSpline_AndIsForAnOpenOneOfControlPoints()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            spline.PurgeFitData();
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "J");
            Assert.Equal("Select any open curves to join to source:", session.PromptText);
        }

        #endregion

        #region Refine

        [Fact]
        public void Refine_AsksTheVertexEditingOptions_AndKeepsTheFitDataWhileNothingChanges()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "R");
            Assert.Equal(
                "Enter a vertex editing option [Add/Delete/Elevate order/Move/Weight/eXit] <eXit>:",
                session.PromptText
            );
            Run(session, "X");
            Assert.True(spline.HasFitData);
        }

        [Fact]
        public void Refine_Add_PutsAKnotInAtTheNearestPointOfTheCurve_AsAutoCadDid_AndTheFitDataGo()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "R", "A");
            Assert.Equal("Specify a point on the spline <exit>:", session.PromptText);
            Run(session, P(13.33333333333333, 6.7724867724867712));

            Assert.False(spline.HasFitData);
            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 7.777777777777776, 14.44444444444444, 21.11111111111111, 30.0, 36.66666666666666, 40.0 },
                new[] { 0.0, 5.714285714285712, 13.33333333333333, 5.714285714285713, -5.714285714285711, 17.14285714285714, 5.714285714285713, 0.0 },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    14.14213562373095,
                    18.85618083164127,
                    28.2842712474619,
                    42.42640687119285,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                }
            );
        }

        [Fact]
        public void Refine_ElevateOrder_RaisesTheDegree_AsAutoCadDid_AndRefusesAnOrderBelowTheOwn()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "R", "E");
            Assert.Equal("Enter new order <4>:", session.PromptText);
            Run(session, "3");
            Assert.Contains("Requires an integer between 4 and 11.", session.History);
            Assert.Equal("Enter new order <4>:", session.PromptText);
            Run(session, "5");

            Assert.Equal(4, spline.Degree);
            Assert.False(spline.HasFitData);
            Matches(
                spline,
                new[] { 0.0, 2.5, 5.0, 10.0, 15.0, 20.0, 25.0, 30.0, 35.0, 37.5, 40.0 },
                new[] { 0.0, 4.285714285714284, 8.571428571428569, 13.57142857142857, 4.285714285714281, -4.285714285714284, 4.285714285714284, 13.57142857142857, 8.571428571428572, 4.28571428571429, 0.0 },
                new[]
                {
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    14.14213562373095,
                    14.14213562373095,
                    28.2842712474619,
                    28.2842712474619,
                    42.42640687119285,
                    42.42640687119285,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                    56.5685424949238,
                }
            );
            Assert.StartsWith("Enter a vertex editing option", session.PromptText);
        }

        [Fact]
        public void Refine_Weight_MakesTheSplineRational_SetsTheWeightOfTheMarkedVertex_AndNextGoesOn()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "R", "W");
            Assert.Contains("Spline is not rational. Will make it so.", session.History);
            Assert.Equal(
                "Enter new weight (current = 1.0000) or [Next/Previous/Select point/eXit] <N>:",
                session.PromptText
            );
            Run(session, "2");
            Assert.Equal(
                "Enter new weight (current = 2.0000) or [Next/Previous/Select point/eXit] <N>:",
                session.PromptText
            );
            Run(session, "N", "3", "X", "X", "X");

            Assert.True(spline.IsRational);
            Assert.False(spline.HasFitData);
            double[] weights = { 2, 3, 1, 1, 1, 1, 1 };

            for (int i = 0; i < weights.Length; i++)
            {
                Assert.Equal(weights[i], spline.WeightAt(i), 9);
            }

            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 10.0, 20.0, 30.0, 36.66666666666666, 40.0 },
                new[] { 0.0, 5.714285714285712, 17.14285714285714, -8.571428571428569, 17.14285714285714, 5.714285714285713, 0.0 },
                new[] { 0.0, 0.0, 0.0, 0.0, 14.14213562373095, 28.2842712474619, 42.42640687119285, 56.5685424949238, 56.5685424949238, 56.5685424949238, 56.5685424949238 }
            );
        }

        [Fact]
        public void Refine_Delete_TakesTheVertexPickedOut_AndSaysNotAControlVertexForNone()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "R", "D");
            Assert.Equal("Specify control vertex to delete:", session.PromptText);
            Run(session, P(5, 50));
            Assert.Contains("Not a control vertex.", session.History);
            Assert.Equal(7, spline.NumControlPoints);

            Run(session, P(20, -8.57142857142857));
            Assert.Equal(6, spline.NumControlPoints);
            Assert.False(spline.HasFitData);
            Assert.Equal("Specify control vertex to delete:", session.PromptText);
        }

        [Fact]
        public void Refine_Move_IsMoveVertex()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "R", "M");
            Assert.Equal("Specify new location or [Next/Previous/Select point/eXit] <N>:", session.PromptText);
            Run(session, P(3, -3));
            Near(P3(3, -3), spline.GetControlPointAt(0));
            Assert.False(spline.HasFitData);
        }

        #endregion

        #region Close and Open of control points

        [Fact]
        public void Close_OfAControlPointSpline_MakesAutoCadsPeriodicFormOfItsPoints_AndOpenGivesThemBackWithUniformKnots()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            spline.PurgeFitData();
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "C");

            Assert.True(spline.Closed);
            Assert.True(spline.IsPeriodic);
            Matches(
                spline,
                new[] { 36.11111111111112, 37.77777777777778, 40.0, 0.0, 3.333333333333333, 10.0, 20.0, 30.0, 34.44444444444445, 36.11111111111112 },
                new[] { 6.666666666666666, 3.809523809523809, 0.0, 0.0, 5.714285714285712, 17.14285714285714, -8.571428571428569, 17.14285714285714, 9.523809523809523, 6.666666666666666 },
                new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 7.0, 7.0, 7.0 }
            );

            Run(session, "O");
            Assert.False(spline.Closed);
            Assert.False(spline.IsPeriodic);
            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 10.0, 20.0, 30.0, 36.66666666666666, 40.0 },
                new[] { 0.0, 5.714285714285712, 17.14285714285714, -8.571428571428569, 17.14285714285714, 5.714285714285713, 0.0 },
                new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0, 4.0, 4.0, 4.0 }
            );
        }

        [Fact]
        public void Close_OfAControlPointSpline_OfUnequalKnots_TakesUniformOnesAsAutoCadDid()
        {
            CadDocument document = Blank();
            DxfSpline spline = Fit(document, 0, 0, 5, 10, 20, 0, 30, 15, 50, 0);
            spline.PurgeFitData();
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "C");

            Matches(
                spline,
                new[] { 41.51834051842167, 44.95222714125155, 50.0, 0.0, 1.300501786969027, 4.697999695671485, 22.31696863777824, 29.39668026302076, 38.08445389559181, 41.51834051842167 },
                new[] { 11.14650434972958, 6.63372801616133, 0.0, 0.0, 4.804101776696287, 17.35458490794947, -11.04872935401213, 27.07665800140949, 15.65928068329783, 11.14650434972958 },
                new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 7.0, 7.0, 7.0 }
            );

            Run(session, "O");
            Matches(
                spline,
                new[] { 0.0, 1.300501786969027, 4.697999695671485, 22.31696863777824, 29.39668026302076, 42.42834071187733, 50.0 },
                new[] { 0.0, 4.804101776696287, 17.35458490794947, -11.04872935401213, 27.07665800140949, 9.950592024241995, 0.0 },
                new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0, 4.0, 4.0, 4.0 }
            );
            Run(session, "U");
            Assert.True(((DxfSpline)document.FindEntity(spline.Handle)).Closed);
        }

        [Fact]
        public void Close_OfARationalControlPointSpline_KeepsTheWeightsWithTheirPoints()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            spline.PurgeFitData();
            spline.SetWeightAt(1, 2.0);
            CadCommandSession session = Start(document, "SPLINEDIT", new[] { spline.Handle }, "C");
            Assert.True(spline.IsRational);
            Assert.True(spline.IsPeriodic);
            Run(session, "O");
            Assert.False(spline.IsPeriodic);
            Assert.Equal(2.0, spline.WeightAt(1), 9);
            Assert.Equal(1.0, spline.WeightAt(0), 9);
            Assert.Equal(7, spline.NumControlPoints);
        }

        #endregion

    }
}
