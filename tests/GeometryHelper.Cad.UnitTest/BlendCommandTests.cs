using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// BLEND in the words of AutoCAD 2021: a spline between the ends of two open curves, Tangent or Smooth. The Tangent numbers are
    /// the records AutoCAD 2021 wrote (accoreconsole, standard input) for lines, an arc, a polyline and a spline, and are met to
    /// 1E-7; the Smooth spline is checked against its own rules (the curve's end, its direction and its curvature there),
    /// since AutoCAD's Smooth control points are another set.
    /// </summary>
    public class BlendCommandTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfLine Line(CadDocument document, double x1, double y1, double x2, double y2)
        {
            var line = new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));
            document.Database.ModelSpace.AppendEntity(line);
            return line;
        }

        private static DxfSpline Blended(CadDocument document) => Assert.Single(All<DxfSpline>(document.ModelSpace));

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

        private static double[] Cubic(double length) => new[] { 0, 0, 0, 0, length, length, length, length };

        /// <summary>The curvature of a spline at a parameter: the cross product of its first and second derivatives over the speed cubed.</summary>
        private static double Curvature(DxfSpline spline, double parameter)
        {
            GeoVector3 d1 = spline.GetFirstDerivative(parameter);
            GeoVector3 d2 = spline.GetSecondDerivative(parameter);
            return d1.CrossProduct(d2).Length / Math.Pow(d1.Length, 3);
        }

        #region The prompts

        [Fact]
        public void BlendSaysItsContinuity_AndAsksForTheFirstThenTheSecondObject()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            Line(document, 50, 20, 80, 20);
            CadCommandSession session = Start(document, "BLEND");
            Assert.Contains("Continuity = Tangent", session.History);
            Assert.Equal("Select first object or [CONtinuity]:", session.PromptText);
            Run(session, P(29, 0));
            Assert.Equal("Select second object:", session.PromptText);
        }

        [Fact]
        public void Continuity_AsksTangentOrSmooth_AndIsKeptForTheNextBlend()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "BLEND", "CON");
            Assert.Equal("Enter continuity [Tangent/Smooth] <Tangent>:", session.PromptText);
            Run(session, "S");
            Assert.Equal("Select first object or [CONtinuity]:", session.PromptText);
            Run(session, Esc, "BLEND");
            Assert.Contains("Continuity = Smooth", session.History);
            Run(session, "CON");
            Assert.Equal("Enter continuity [Tangent/Smooth] <Smooth>:", session.PromptText);
        }

        [Fact]
        public void ACircleIsRefused_AndAskedAgain()
        {
            CadDocument document = Blank();
            document.Database.ModelSpace.AppendEntity(
                new DxfCircle(new GeoPoint3(0, 100, 0), new GeoVector3(0, 0, 1), 10)
            );
            Line(document, 0, 0, 30, 0);
            CadCommandSession session = Start(document, "BLEND", P(10, 100));
            Assert.Contains("Cannot BLEND this object.", session.History);
            Assert.Equal("Select first object or [CONtinuity]:", session.PromptText);
            Run(session, P(29, 0), P(10, 100));
            Assert.Equal("Select second object:", session.PromptText);
        }

        [Fact]
        public void TheSameLineTwice_IsRefusedInAutoCadsWords()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            CadCommandSession session = Start(document, "BLEND", P(29, 0), P(1, 0));
            Assert.Contains("Cannot blend an entity with itself at the specified location.", session.History);
            Assert.Equal("Select second object:", session.PromptText);
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
        }

        [Fact]
        public void AClosedPolylineIsRefused()
        {
            CadDocument document = Blank();
            var points = new List<GeoPoint3>
            {
                new GeoPoint3(200, 100, 0),
                new GeoPoint3(230, 100, 0),
                new GeoPoint3(230, 130, 0),
                new GeoPoint3(200, 130, 0),
            };
            document.Database.ModelSpace.AppendEntity(
                new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0.0, true, 0, 0, new double[4])
            );
            CadCommandSession session = Start(document, "BLEND", P(200, 110));
            Assert.Contains("Cannot BLEND this object.", session.History);
        }

        #endregion

        #region Tangent

        [Fact]
        public void TwoLinesPickedAtTheirNearEnds_GiveTheCubicAutoCadWrote()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            Line(document, 50, 20, 80, 20);
            CadCommandSession session = Start(document, "BLEND", P(29, 0), P(51, 20));
            Assert.Null(session.Prompt);
            Matches(
                Blended(document),
                new[] { 30, 0, 39.4280904158, 0, 40.5719095842, 20, 50, 20 },
                Cubic(28.2842712475)
            );
        }

        [Fact]
        public void TwoLinesPickedAtTheirFarEnds_RunFromEndToEnd_WhicheverEndIsNearestThePick()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            Line(document, 50, 20, 80, 20);
            Start(document, "BLEND", P(1, 0), P(79, 20));
            Matches(
                Blended(document),
                new[] { 0, 0, -27.4873708375, 0, 107.4873708375, 20, 80, 20 },
                Cubic(82.4621125124)
            );
        }

        [Fact]
        public void ALineStandingSquareToTheFirst_GivesTheCubicAutoCadWrote()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            Line(document, 50, 20, 50, 50);
            Start(document, "BLEND", P(29, 0), P(50, 21));
            Matches(
                Blended(document),
                new[] { 30, 0, 39.4280904158, 0, 50, 10.5719095842, 50, 20 },
                Cubic(28.2842712475)
            );
        }

        [Fact]
        public void LinesInOneRow_GiveTheStraightCubicAThirdApart()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            Line(document, 50, 0, 80, 0);
            Start(document, "BLEND", P(29, 0), P(51, 0));
            Matches(Blended(document), new[] { 30, 0, 36.6666666667, 0, 43.3333333333, 0, 50, 0 }, Cubic(20));
        }

        [Fact]
        public void AnArcAndALine_LeaveTheArcAlongItsTangent_AsAutoCadDoes()
        {
            CadDocument document = Blank();
            document.Database.ModelSpace.AppendEntity(
                new DxfArc(new GeoPoint3(120, 200, 0), new GeoVector3(0, 0, 1), 20, 0, Math.PI)
            );
            Line(document, 180, 210, 220, 210);
            Start(document, "BLEND", P(139, 200), P(181, 210));
            Matches(
                Blended(document),
                new[] { 140, 200, 140, 186.25631458, 166.25631458, 210, 180, 210 },
                Cubic(41.2310562562)
            );
        }

        [Fact]
        public void APolylineAndALine_GiveTheCubicAutoCadWrote()
        {
            CadDocument document = Blank();
            var polyline = new DxfPolyline();
            document.Database.ModelSpace.AppendEntity(polyline);
            polyline.AddVertexAt(0, new GeoPoint2(0, 150), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(20, 150), 0, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(30, 170), 0, 0, 0);
            Line(document, 60, 180, 90, 180);
            Start(document, "BLEND", P(29, 169), P(61, 180));
            Matches(
                Blended(document),
                new[] { 30, 170, 34.71404521, 179.42809042, 49.45907447, 180, 60, 180 },
                Cubic(31.6227766017)
            );
        }

        [Fact]
        public void ASplineAndALine_GiveTheCubicAutoCadWrote()
        {
            CadDocument document = Blank();
            var spline = new DxfSpline();
            document.Database.ModelSpace.AppendEntity(spline);
            spline.FitData = new DxfFitData(
                new[] { new GeoPoint3(0, 220, 0), new GeoPoint3(20, 240, 0), new GeoPoint3(40, 220, 0) },
                3,
                0.0
            );
            Line(document, 70, 230, 90, 230);
            Start(document, "BLEND", P(39, 221), P(71, 230));
            DxfSpline blend = All<DxfSpline>(document.ModelSpace).Single(s => !ReferenceEquals(s, spline));
            Matches(
                blend,
                new[] { 40, 220, 45.84705346, 211.22941981, 59.45907447, 230, 70, 230 },
                Cubic(31.6227766017)
            );
        }

        [Fact]
        public void TheBlend_IsOnTheCurrentLayer_AndOneStepOfUndo()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            Line(document, 50, 20, 80, 20);
            Start(document, "BLEND", P(29, 0), P(51, 20));
            Assert.Equal(document.Database.Clayer, Blended(document).Layer);
            document.Undo();
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
        }

        [Fact]
        public void TheSplineBetweenTheCursorsObject_IsDrawnWhileTheSecondObjectIsAsked()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            Line(document, 50, 20, 80, 20);
            CadCommandSession session = Start(document, "BLEND", P(29, 0));
            Assert.NotEmpty(session.Prompt.Preview.Items(P(51, 20)));
            Assert.Empty(session.Prompt.Preview.Items(P(1, 0)));
            Assert.Empty(All<DxfSpline>(document.ModelSpace));
        }

        #endregion

        #region Smooth

        [Fact]
        public void SmoothMakesADegree5SplineThatStartsAndEndsOnTheEndsAlongTheLines_WithNoCurvatureThere()
        {
            CadDocument document = Blank();
            Line(document, 0, 0, 30, 0);
            Line(document, 50, 20, 80, 20);
            Start(document, "BLEND", "CON", "S", P(29, 0), P(51, 20));
            DxfSpline spline = Blended(document);
            Assert.Equal(5, spline.Degree);
            IReadOnlyList<GeoPoint3> points = spline.ControlPoints;
            Assert.Equal(6, points.Count);
            Near(new GeoPoint3(30, 0, 0), points[0]);
            Near(new GeoPoint3(50, 20, 0), points[5]);

            // It leaves along the first line, going on past its end, and arrives along the second, going into its start.
            Assert.True(spline.GetFirstDerivative(spline.StartParam).X > 0.0);
            Assert.Equal(0.0, spline.GetFirstDerivative(spline.StartParam).Y, 9);
            Assert.True(spline.GetFirstDerivative(spline.EndParam).X > 0.0);
            Assert.Equal(0.0, spline.GetFirstDerivative(spline.EndParam).Y, 9);
            Assert.Equal(0.0, Curvature(spline, spline.StartParam), 9);
            Assert.Equal(0.0, Curvature(spline, spline.EndParam), 9);
        }

        [Fact]
        public void SmoothTakesTheCurvatureOfAnArcAtItsEnd_OneOverItsRadius()
        {
            CadDocument document = Blank();
            document.Database.ModelSpace.AppendEntity(
                new DxfArc(new GeoPoint3(120, 200, 0), new GeoVector3(0, 0, 1), 20, 0, Math.PI)
            );
            Line(document, 180, 210, 220, 210);
            Start(document, "BLEND", "CON", "S", P(139, 200), P(181, 210));
            DxfSpline spline = Blended(document);
            Assert.Equal(0.05, Curvature(spline, spline.StartParam), 9);
            Assert.Equal(0.0, Curvature(spline, spline.EndParam), 9);
            Near(new GeoPoint3(140, 200, 0), spline.GetPointAtParameter(spline.StartParam));
            Near(new GeoPoint3(180, 210, 0), spline.GetPointAtParameter(spline.EndParam));
        }

        #endregion
    }
}
