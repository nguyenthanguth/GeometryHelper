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
    /// SPLINE in the words of AutoCAD 2021: Method Fit and CV, Knots, Degree, Object, start and end Tangency, toLerance, Undo and
    /// Close. The numbers are the records AutoCAD 2021 wrote (accoreconsole, standard input, R2018 DXF) for splines drawn
    /// through (10, 10), (30, 40), (60, 10) and (80, 50), and are met to 1E-7.
    /// </summary>
    public class SplineCommandTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfSpline Made(CadDocument document) => Assert.Single(All<DxfSpline>(document.ModelSpace));

        private static void Matches(DxfSpline spline, double[] xy, double[] knots)
        {
            IReadOnlyList<GeoPoint3> points = spline.ControlPoints;
            Assert.Equal(xy.Length / 2, points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                Assert.Equal(xy[2 * i], points[i].X, 7);
                Assert.Equal(xy[2 * i + 1], points[i].Y, 7);
            }

            IReadOnlyList<double> have = spline.Knots;
            Assert.Equal(knots.Length, have.Count);

            for (int i = 0; i < knots.Length; i++)
            {
                Assert.Equal(knots[i], have[i], 7);
            }
        }

        private static readonly object[] Four = { P(10, 10), P(30, 40), P(60, 10), P(80, 50) };

        private static object[] Join(params object[][] parts) => parts.SelectMany(p => p).ToArray();

        #region The prompts

        [Fact]
        public void TheFirstPointIsAskedWithMethodKnotsAndObject_AndTheCurrentSettingsAreWritten()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE");
            Assert.Equal("Specify first point or [Method/Knots/Object]:", session.PromptText);
            Assert.Contains("Current settings: Method=Fit   Knots=Chord", session.History);
        }

        [Fact]
        public void TheFitMethodAsksNextPointsWithTangencyToleranceUndoAndClose_ClosePastTwoPoints()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", P(10, 10));
            Assert.Equal("Enter next point or [start Tangency/toLerance]:", session.PromptText);
            Run(session, P(30, 40));
            Assert.Equal("Enter next point or [end Tangency/toLerance/Undo]:", session.PromptText);
            Run(session, P(60, 10));
            Assert.Equal("Enter next point or [end Tangency/toLerance/Undo/Close]:", session.PromptText);
        }

        [Fact]
        public void TheControlVertexMethodAsksNextPointsWithUndoAndClose_ClosePastTwoPoints()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", "M", "CV");
            Assert.Equal("Specify first point or [Method/Degree/Object]:", session.PromptText);
            Assert.Contains("Current settings: Method=CV   Degree=3", session.History);
            Run(session, P(10, 10));
            Assert.Equal("Enter next point:", session.PromptText);
            Run(session, P(30, 40));
            Assert.Equal("Enter next point or [Undo]:", session.PromptText);
            Run(session, P(60, 10));
            Assert.Equal("Enter next point or [Close/Undo]:", session.PromptText);
        }

        [Fact]
        public void MethodKnotsAndDegreeAskTheirOwnPrompts_InAutoCadsWords()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", "M");
            Assert.Equal("Enter spline creation method [Fit/CV] <Fit>:", session.PromptText);
            Run(session, "CV");
            Assert.Equal("Specify first point or [Method/Degree/Object]:", session.PromptText);
            Run(session, "D");
            Assert.Equal("Enter degree of spline <3>:", session.PromptText);
            Run(session, Enter, "M", "F", "K");
            Assert.Equal("Enter knot parameterization [Chord/Square root/Uniform] <Chord>:", session.PromptText);
        }

        [Fact]
        public void TheSettingsAreKeptForTheNextSpline_AsSplmethodSplknotsAndSpldegree()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", "K", "S", "M", "CV", "D", "5", Esc, "SPLMETHOD");
            Assert.Equal("Enter new value for SPLMETHOD <1>:", session.PromptText);
            Run(session, Esc, "SPLKNOTS");
            Assert.Equal("Enter new value for SPLKNOTS <1>:", session.PromptText);
            Run(session, Esc, "SPLDEGREE");
            Assert.Equal("Enter new value for SPLDEGREE <5>:", session.PromptText);
            Run(session, Esc, "SPLINE");
            Assert.Contains("Current settings: Method=CV   Degree=5", session.History);
        }

        [Fact]
        public void ADegreeOutsideOneToTenIsAskedAgain_AndWordsAreRefused()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", "M", "CV", "D", "0");
            Assert.Equal("Enter degree of spline <3>:", session.PromptText);
            Run(session, "11");
            Assert.Equal("Enter degree of spline <3>:", session.PromptText);
            Run(session, "abc");
            Assert.Contains("Requires an integer between -32768 and 32767, or option keyword.", session.History);
            Run(session, "10");
            Assert.Equal("Specify first point or [Method/Degree/Object]:", session.PromptText);
            Assert.Contains("Current settings: Method=CV   Degree=10", session.History);
        }

        [Fact]
        public void SplIsTheAliasOfSpline()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPL");
            Assert.Equal("Specify first point or [Method/Knots/Object]:", session.PromptText);
        }

        #endregion

        #region The fit method

        [Fact]
        public void FourPointsEndedWithEnter_MakeTheCubicAutoCadFitted()
        {
            CadDocument document = Blank();
            Start(document, Join(new object[] { "SPLINE" }, Four, new object[] { Enter }));
            DxfSpline spline = Made(document);
            Assert.True(spline.HasFitData);
            Matches(
                spline,
                new[]
                {
                    10,
                    10,
                    16.0293323198,
                    25.7004793432,
                    29.1533607516,
                    59.8756626580,
                    63.9770838946,
                    -13.6733639451,
                    74.5660776401,
                    28.4061706501,
                    80,
                    50,
                },
                new[]
                {
                    0,
                    0,
                    0,
                    0,
                    36.0555127546,
                    78.4819196258,
                    123.2032791758,
                    123.2032791758,
                    123.2032791758,
                    123.2032791758,
                }
            );
            Assert.Equal(4, spline.NumFitPoints);
            Assert.False(spline.Closed);
        }

        [Fact]
        public void KnotsBySquareRootAndUniform_GiveTheSplinesAutoCadFitted()
        {
            CadDocument document = Blank();
            Start(document, Join(new object[] { "SPLINE", "K", "S" }, Four, new object[] { Enter }));
            Matches(
                Made(document),
                new[]
                {
                    10,
                    10,
                    15.8082346674,
                    26.2653034953,
                    27.9169900362,
                    60.1745017801,
                    63.6665572887,
                    -13.1330274948,
                    74.5079279974,
                    28.7717054591,
                    80,
                    50,
                },
                new[]
                {
                    0,
                    0,
                    0,
                    0,
                    6.0046242809,
                    12.5181799052,
                    19.2055829550,
                    19.2055829550,
                    19.2055829550,
                    19.2055829550,
                }
            );

            document = Blank();
            Start(document, Join(new object[] { "SPLINE", "K", "U" }, Four, new object[] { Enter }));
            Matches(
                Made(document),
                new[]
                {
                    10,
                    10,
                    15.5555555556,
                    26.8888888889,
                    26.6666666667,
                    60.6666666667,
                    63.3333333333,
                    -12.6666666667,
                    74.4444444444,
                    29.1111111111,
                    80,
                    50,
                },
                new double[] { 0, 0, 0, 0, 1, 2, 3, 3, 3, 3 }
            );
        }

        [Fact]
        public void Close_MakesTheClosedCubicThroughTheFirstPointAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, Join(new object[] { "SPLINE" }, Four, new object[] { "C" }));
            Assert.Null(session.Prompt);
            DxfSpline spline = Made(document);
            Assert.True(spline.Closed);
            Matches(
                spline,
                new[]
                {
                    10,
                    10,
                    9.2941473741,
                    16.4499285275,
                    33.8358316395,
                    66.4351325225,
                    58.6494903151,
                    -21.5375391733,
                    107.1632972441,
                    101.5253934175,
                    11.5783344535,
                    -4.4224786374,
                    10,
                    10,
                },
                new[]
                {
                    0,
                    0,
                    0,
                    0,
                    36.0555127546,
                    78.4819196258,
                    123.2032791758,
                    203.8258566588,
                    203.8258566588,
                    203.8258566588,
                    203.8258566588,
                }
            );
        }

        [Fact]
        public void StartAndEndTangents_AreDirectionsFromTheEndsToThePointsPicked()
        {
            // The tangent points of AutoCAD's run: (1, 0) for the start of the spline through (10, 10), (30, 40) and (60, 10), and
            // (0, -1) for its end.
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", P(10, 10), "T", P(1, 0), P(30, 40), P(60, 10), "T");
            Assert.Equal("Specify end tangent:", session.PromptText);
            Run(session, P(0, -1));
            Assert.Null(session.Prompt);
            DxfSpline spline = Made(document);
            Matches(
                spline,
                new[]
                {
                    10,
                    10,
                    -98.1665382639,
                    -110.1850425155,
                    86.7482898353,
                    140.0250434358,
                    73.9102973348,
                    12.5502211780,
                    60,
                    10,
                },
                new[] { 0, 0, 0, 0, 36.0555127546, 78.4819196258, 78.4819196258, 78.4819196258, 78.4819196258 }
            );
        }

        [Fact]
        public void AStartTangentAloneAndAnEndTangentAlone_GiveTheSplinesAutoCadFitted()
        {
            CadDocument document = Blank();
            Start(document, "SPLINE", P(10, 10), "T", P(20, 10), P(30, 40), P(60, 10), Enter);
            Matches(
                Made(document),
                new[]
                {
                    10,
                    10,
                    130.1850425155,
                    10,
                    -28.4962326980,
                    62.5610127126,
                    28.9469097473,
                    28.4435181225,
                    60,
                    10,
                },
                new[] { 0, 0, 0, 0, 36.0555127546, 78.4819196258, 78.4819196258, 78.4819196258, 78.4819196258 }
            );

            document = Blank();
            Start(document, "SPLINE", P(10, 10), P(30, 40), P(60, 10), "T", P(0, 0));
            Matches(
                Made(document),
                new[]
                {
                    10,
                    10,
                    13.4772303621,
                    25.7793818056,
                    21.0461066019,
                    60.1263118587,
                    73.9497166493,
                    12.3249527749,
                    60,
                    10,
                },
                new[] { 0, 0, 0, 0, 36.0555127546, 78.4819196258, 78.4819196258, 78.4819196258, 78.4819196258 }
            );
        }

        [Fact]
        public void TwoPointsMakeTheStraightCubic_ThatIsOneThirdApart()
        {
            CadDocument document = Blank();
            Start(document, "SPLINE", P(10, 10), P(30, 40), Enter);
            DxfSpline spline = Made(document);
            Matches(
                spline,
                new[] { 10, 10, 16.6666666667, 20, 23.3333333333, 30, 30, 40 },
                new[] { 0, 0, 0, 0, 36.0555127546, 36.0555127546, 36.0555127546, 36.0555127546 }
            );
        }

        [Fact]
        public void UndoTakesTheLastPointBack_AndOnlyFromTheSecondPoint()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", P(10, 10), P(30, 40), P(60, 10));
            Run(session, "U");
            Assert.Equal("Enter next point or [end Tangency/toLerance/Undo]:", session.PromptText);
            Run(session, "U");
            Assert.Equal("Enter next point or [start Tangency/toLerance]:", session.PromptText);
            Run(session, P(90, 10), Enter);
            Matches(
                Made(document),
                new[] { 10, 10, 36.6666666667, 10, 63.3333333333, 10, 90, 10 },
                new double[] { 0, 0, 0, 0, 80, 80, 80, 80 }
            );
        }

        [Fact]
        public void ToLeranceAsksForADistance_RefusesANegativeOne_AndIsWrittenToTheSpline()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", P(10, 10), "L");
            Assert.Equal("Specify fit tolerance <0.0000>:", session.PromptText);
            Run(session, "-1");
            Assert.Contains("Value must be positive.", session.History);
            Assert.Equal("Specify fit tolerance <0.0000>:", session.PromptText);
            Run(session, "0.5", P(30, 40), Enter);
            Assert.Equal(0.5, Made(document).FitTolerance, 9);
        }

        [Fact]
        public void EnterAfterOnePoint_MakesNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", P(10, 10), Enter);
            Assert.Null(session.Prompt);
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
        }

        [Fact]
        public void TheSplineThroughThePointsAndTheCursor_IsDrawnWhileTheNextPointIsAsked()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", P(10, 10));
            Assert.Empty(session.Prompt.Preview.Items(P(10, 10)));
            Assert.NotEmpty(session.Prompt.Preview.Items(P(30, 40)));
            Run(session, P(30, 40), P(60, 10));
            Assert.NotEmpty(session.Prompt.Preview.Items(P(80, 50)));
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
        }

        [Fact]
        public void TheSpline_TakesTheCurrentLayerAndIsOneStepOfUndo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, Join(new object[] { "SPLINE" }, Four, new object[] { Enter }));
            Assert.Equal(document.Database.Clayer, Made(document).Layer);
            document.Undo();
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
        }

        #endregion

        #region The control vertex method

        [Fact]
        public void FourVerticesOfDegree2_MakeAClampedQuadraticOnThem()
        {
            CadDocument document = Blank();
            Start(document, Join(new object[] { "SPLINE", "M", "CV", "D", "2" }, Four, new object[] { Enter }));
            DxfSpline spline = Made(document);
            Assert.False(spline.HasFitData);
            Assert.Equal(2, spline.Degree);
            Matches(spline, new double[] { 10, 10, 30, 40, 60, 10, 80, 50 }, new double[] { 0, 0, 0, 1, 2, 2, 2 });
        }

        [Fact]
        public void UndoTakesAVertexBack_AndFewerVerticesThanTheDegreeAndOneLowerTheDegree()
        {
            CadDocument document = Blank();
            Start(document, "SPLINE", "M", "CV", P(10, 10), P(30, 40), P(60, 10), P(80, 50), "U", P(90, 20), Enter);
            Matches(
                Made(document),
                new double[] { 10, 10, 30, 40, 60, 10, 90, 20 },
                new double[] { 0, 0, 0, 0, 1, 1, 1, 1 }
            );

            document = Blank();
            Start(document, "SPLINE", "M", "CV", P(10, 10), P(30, 40), Enter);
            DxfSpline line = Made(document);
            Assert.Equal(1, line.Degree);
            Matches(line, new double[] { 10, 10, 30, 40 }, new double[] { 0, 0, 1, 1 });
        }

        [Fact]
        public void CloseOfFourVerticesOfDegree3_MakesTheSevenPointPeriodicSplineAutoCadWrote()
        {
            CadDocument document = Blank();
            Start(document, Join(new object[] { "SPLINE", "M", "CV" }, Four, new object[] { "C" }));
            DxfSpline spline = Made(document);
            Assert.True(spline.Closed);
            Assert.True(spline.IsPeriodic);
            Matches(
                spline,
                new[]
                {
                    58.3333333333,
                    21.6666666667,
                    66.6666666667,
                    23.3333333333,
                    80,
                    50,
                    10,
                    10,
                    30,
                    40,
                    50,
                    20,
                    58.3333333333,
                    21.6666666667,
                },
                new double[] { 0, 0, 0, 0, 1, 2, 3, 4, 4, 4, 4 }
            );
        }

        [Fact]
        public void CloseOfThreeVerticesOfDegree3_AndOfFiveOfDegree5_KeepTheDegreeAsAutoCadDoes()
        {
            CadDocument document = Blank();
            Start(document, "SPLINE", "M", "CV", P(10, 10), P(30, 40), P(60, 20), "C");
            Matches(
                Made(document),
                new[]
                {
                    31.6666666667,
                    31.6666666667,
                    40,
                    33.3333333333,
                    60,
                    20,
                    10,
                    10,
                    23.3333333333,
                    30,
                    31.6666666667,
                    31.6666666667,
                },
                new double[] { 0, 0, 0, 0, 1, 2, 3, 3, 3, 3 }
            );

            document = Blank();
            Start(document, "SPLINE", "M", "CV", "D", "5", P(10, 10), P(30, 40), P(60, 10), P(80, 50), P(90, 20), "C");
            DxfSpline five = Made(document);
            Assert.Equal(5, five.Degree);
            Matches(
                five,
                new[]
                {
                    57.6666666667,
                    25.25,
                    62.5,
                    26.1666666667,
                    71.5,
                    30.5,
                    82,
                    44,
                    90,
                    20,
                    10,
                    10,
                    26,
                    34,
                    42.5,
                    25,
                    52.8333333333,
                    24.3333333333,
                    57.6666666667,
                    25.25,
                },
                new double[] { 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 5, 5, 5, 5, 5 }
            );
        }

        #endregion

        #region Object

        private static DxfPolyline2d SplineFitted(CadDocument document, bool closed, params double[] xy)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i + 1 < xy.Length; i += 2)
            {
                points.Add(new GeoPoint3(xy[i], xy[i + 1], 0));
            }

            var heavy = new DxfPolyline2d(
                DxfPoly2dType.SimplePoly,
                points,
                0.0,
                closed,
                0,
                0,
                new double[points.Count]
            );
            document.Database.ModelSpace.AppendEntity(heavy);
            heavy.ConvertToPolyType(DxfPoly2dType.CubicSplinePoly);
            return heavy;
        }

        [Fact]
        public void Object_AsksForASplineFitPolyline_AndMakesTheSplineOnItsFrame_AsAutoCadDoes()
        {
            CadDocument document = Blank();
            DxfPolyline2d polyline = SplineFitted(document, false, 10, 60, 30, 90, 60, 60, 90, 90, 95, 50);
            polyline.Layer = "0";
            CadCommandSession session = Start(document, "SPLINE", "O");
            Assert.Equal("Select spline-fit polyline:", session.PromptText);
            Run(session, new[] { polyline.Handle }, Enter);
            Assert.Null(session.Prompt);
            DxfSpline spline = Made(document);
            Assert.Empty(All<DxfPolyline2d>(document.ModelSpace));
            Matches(
                spline,
                new double[] { 10, 60, 30, 90, 60, 60, 90, 90, 95, 50 },
                new double[] { 0, 0, 0, 0, 1, 2, 2, 2, 2 }
            );
            Assert.Equal(3, spline.Degree);
            document.Undo();
            Assert.Single(All<DxfPolyline2d>(document.ModelSpace));
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
        }

        [Fact]
        public void ASplineFitPolylineOnALayerOfItsOwn_KeepsTheLayerAndColour()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "L1" });
            DxfPolyline2d polyline = SplineFitted(document, false, 10, 60, 30, 90, 60, 60, 90, 90);
            polyline.Layer = "L1";
            polyline.ColorIndex = 1;
            Start(document, "SPLINE", "O", new[] { polyline.Handle }, Enter);
            DxfSpline spline = Made(document);
            Assert.Equal("L1", spline.Layer);
            Assert.Equal(1, spline.ColorIndex);
        }

        [Fact]
        public void AClosedSplineFitPolyline_MakesAClosedSplineOfTheSameLength()
        {
            CadDocument document = Blank();
            DxfPolyline2d polyline = SplineFitted(document, true, 10, 10, 30, 40, 60, 10, 80, 50);
            Start(document, "SPLINE", "O", new[] { polyline.Handle }, Enter);
            DxfSpline spline = Made(document);
            Assert.True(spline.Closed);

            // AutoCAD wrote the same curve with the knots 0, 0, 0, 0, 1, 2, 3, 4, 4, 4, 4 on 7 points, a clamped form of it.
            var autocad = new DxfSpline(
                3,
                false,
                true,
                true,
                new[]
                {
                    new GeoPoint3(31.6666666667, 30, 0),
                    new GeoPoint3(40, 30, 0),
                    new GeoPoint3(60, 10, 0),
                    new GeoPoint3(80, 50, 0),
                    new GeoPoint3(10, 10, 0),
                    new GeoPoint3(23.3333333333, 30, 0),
                    new GeoPoint3(31.6666666667, 30, 0),
                },
                new double[] { 0, 0, 0, 0, 1, 2, 3, 4, 4, 4, 4 },
                null,
                1E-10,
                1E-10
            );
            Assert.Equal(
                autocad.GetDistanceAtParameter(autocad.EndParam),
                spline.GetDistanceAtParameter(spline.EndParam),
                6
            );
        }

        [Fact]
        public void APolylineThatIsNotSplineFitted_IsRefusedInAutoCadsWords_AndLeftAlone()
        {
            CadDocument document = Blank();
            var points = new List<GeoPoint3>
            {
                new GeoPoint3(10, 20, 0),
                new GeoPoint3(30, 25, 0),
                new GeoPoint3(60, 20, 0),
            };
            var heavy = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0.0, false, 0, 0, new double[3]);
            document.Database.ModelSpace.AppendEntity(heavy);
            CadCommandSession session = Start(document, "SPLINE", "O", new[] { heavy.Handle }, Enter);
            Assert.Contains("Only spline fitted polylines can be converted to splines.", session.History);
            Assert.Contains("Unable to convert the selected object.", session.History);
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
            Assert.Single(All<DxfPolyline2d>(document.ModelSpace));
        }

        [Fact]
        public void ObjectWithNothingPicked_SaysNoneFound_AndEndsTheCommand()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "SPLINE", "O", Enter);
            Assert.Contains("None found.", session.History);
            Assert.Null(session.Prompt);
        }

        #endregion
    }
}
