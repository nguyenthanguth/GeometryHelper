using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The constraint solver on its own: the numbers of lines, circles and arcs moved to meet geometric and dimensional
    /// constraints, the least way from where they stand, the first object picked held; and the expressions of parameters.
    /// </summary>
    public class ConstraintSolverTests
    {
        private static CadShape Line(string handle, double x1, double y1, double x2, double y2) =>
            new CadShape(handle, CadShapeKind.Line, new[] { x1, y1, x2, y2 });

        private static CadShape Circle(string handle, double x, double y, double r) =>
            new CadShape(handle, CadShapeKind.Circle, new[] { x, y, r });

        private static CadRef Whole(string handle) => new CadRef(handle, -1, CadPart.Whole);

        private static CadRef Point(string handle, CadPart part) => new CadRef(handle, -1, part);

        private static CadSolveResult Solve(
            CadConstraintSet set,
            Dictionary<string, CadShape> shapes,
            string anchor = null,
            Func<string, double?> parameter = null
        )
        {
            CadConstraintSolver.Weighter weight = (handle, index) =>
            {
                double w = shapes[handle].Kind == CadShapeKind.Line && index < 2 ? 1E12 : 1.0;
                return string.Equals(handle, anchor, StringComparison.OrdinalIgnoreCase) ? w * 1E24 : w;
            };
            return CadConstraintSolver.Solve(set, shapes, parameter, weight);
        }

        private static Dictionary<string, CadShape> Shapes(params CadShape[] shapes)
        {
            var map = new Dictionary<string, CadShape>(StringComparer.OrdinalIgnoreCase);

            foreach (CadShape shape in shapes)
            {
                map[shape.Handle] = shape;
            }

            return map;
        }

        [Fact]
        public void AHorizontalConstraint_LevelsTheEndOfALineAndKeepsItsStart()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Horizontal, new[] { Whole("A") }));
            CadSolveResult result = Solve(set, Shapes(Line("A", 0, 0, 10, 1)));

            Assert.True(result.Succeeded, result.Message);
            double[] v = result.Values["A"];
            Assert.Equal(0.0, v[0], 6);
            Assert.Equal(0.0, v[1], 6);
            Assert.True(Math.Abs(v[3] - v[1]) < 1E-13, "dy " + (v[3] - v[1]));
            Assert.True(Math.Abs(v[2] - 10.0) < 0.1);
        }

        [Fact]
        public void AParallelConstraint_TurnsTheSecondLineToTheFirstWhichStaysWhereItIs()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Parallel, new[] { Whole("A"), Whole("B") }));
            CadSolveResult result = Solve(set, Shapes(Line("A", 0, 0, 10, 1), Line("B", 0, 5, 8, 7)), "A");

            Assert.True(result.Succeeded, result.Message);
            double[] a = result.Values["A"];
            double[] b = result.Values["B"];
            Assert.True(Math.Abs(a[2] - 10.0) < 0.01 && Math.Abs(a[3] - 1.0) < 0.01);
            double cross = (a[2] - a[0]) * (b[3] - b[1]) - (a[3] - a[1]) * (b[2] - b[0]);
            Assert.Equal(0.0, cross, 6);
        }

        [Fact]
        public void APerpendicularConstraint_MakesARightAngle()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Perpendicular, new[] { Whole("A"), Whole("B") }));
            CadSolveResult result = Solve(set, Shapes(Line("A", 0, 0, 10, 1), Line("B", 0, 5, 8, 7)), "A");
            Assert.True(result.Succeeded, result.Message);
            double[] a = result.Values["A"];
            double[] b = result.Values["B"];
            double dot = (a[2] - a[0]) * (b[2] - b[0]) + (a[3] - a[1]) * (b[3] - b[1]);
            Assert.True(Math.Abs(dot) < 1E-5, "dot " + dot);
        }

        [Fact]
        public void ACoincidentConstraint_MovesTheSecondPointToTheFirst()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(
                new CadGeoConstraint(
                    CadGeoKind.Coincident,
                    new[] { Point("A", CadPart.End), Point("B", CadPart.Start) }
                )
            );
            CadSolveResult result = Solve(set, Shapes(Line("A", 0, 0, 10, 0), Line("B", 12, 3, 20, 3)), "A");

            Assert.True(result.Succeeded, result.Message);
            double[] a = result.Values["A"];
            double[] b = result.Values["B"];
            Assert.Equal(a[2], b[0], 6);
            Assert.Equal(a[3], b[1], 6);
            Assert.True(Math.Abs(a[2] - 10.0) < 0.5);
        }

        [Fact]
        public void ATangentConstraint_PutsALineOnACircleWhichIsNotMoved()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Tangent, new[] { Whole("C"), Whole("L") }) { Side = -1 });
            CadSolveResult result = Solve(set, Shapes(Circle("C", 0, 0, 5), Line("L", -10, 5.5, 10, 5.5)), "C");

            Assert.True(result.Succeeded, result.Message);
            double[] l = result.Values["L"];
            double length = Math.Sqrt((l[2] - l[0]) * (l[2] - l[0]) + (l[3] - l[1]) * (l[3] - l[1]));
            double across = Math.Abs((l[2] - l[0]) * (0.0 - l[1]) - (l[3] - l[1]) * (0.0 - l[0])) / length;
            Assert.Equal(5.0, across, 6);
        }

        [Fact]
        public void TwoCircles_MadeConcentricAndEqual_ShareACentreAndARadius()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Concentric, new[] { Whole("C"), Whole("D") }));
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.EqualRadius, new[] { Whole("C"), Whole("D") }));
            CadSolveResult result = Solve(set, Shapes(Circle("C", 0, 0, 5), Circle("D", 2, 3, 8)), "C");

            Assert.True(result.Succeeded, result.Message);
            double[] c = result.Values["C"];
            double[] d = result.Values["D"];
            Assert.Equal(c[0], d[0], 6);
            Assert.Equal(c[1], d[1], 6);
            Assert.Equal(c[2], d[2], 6);
        }

        [Fact]
        public void AFixedPoint_HoldsWhereAnotherConstraintWouldMoveIt()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(
                new CadGeoConstraint(CadGeoKind.Fix, new[] { Point("A", CadPart.Start) }) { Place = new[] { 0.0, 0.0 } }
            );
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Horizontal, new[] { Whole("A") }));
            CadSolveResult result = Solve(set, Shapes(Line("A", 0, 0, 10, 3)));

            Assert.True(result.Succeeded, result.Message);
            double[] a = result.Values["A"];
            Assert.Equal(0.0, a[0], 8);
            Assert.Equal(0.0, a[1], 8);
            Assert.Equal(a[1], a[3], 8);
        }

        [Fact]
        public void ASymmetricConstraint_MakesTwoPointsMirrorImages()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(
                new CadGeoConstraint(
                    CadGeoKind.Symmetric,
                    new[] { Point("A", CadPart.Start), Point("B", CadPart.Start), Whole("M") }
                )
            );
            CadSolveResult result = Solve(
                set,
                Shapes(Line("A", 1, 1, 5, 1), Line("B", 9, 2, 12, 2), Line("M", 5, -10, 5, 10)),
                "A"
            );

            Assert.True(result.Succeeded, result.Message);
            double[] a = result.Values["A"];
            double[] b = result.Values["B"];
            double[] m = result.Values["M"];
            double dx = m[2] - m[0];
            double dy = m[3] - m[1];
            double midX = (a[0] + b[0]) / 2.0 - m[0];
            double midY = (a[1] + b[1]) / 2.0 - m[1];
            Assert.Equal(0.0, dx * midY - dy * midX, 6);
            Assert.Equal(0.0, dx * (b[0] - a[0]) + dy * (b[1] - a[1]), 6);
        }

        [Fact]
        public void HorizontalAndVerticalOnOneLine_CannotBeSatisfied()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Horizontal, new[] { Whole("A") }));
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Vertical, new[] { Whole("A") }));
            CadSolveResult result = Solve(set, Shapes(Line("A", 0, 0, 10, 1)));

            Assert.False(result.Succeeded);
            Assert.Equal("Constraints cannot be satisfied.", result.Message);
        }

        [Fact]
        public void ARepeatedConstraint_ShowsAsRedundant()
        {
            var one = new CadConstraintSet();
            one.Geometric.Add(new CadGeoConstraint(CadGeoKind.Horizontal, new[] { Whole("A") }));
            var two = new CadConstraintSet();
            two.Geometric.Add(new CadGeoConstraint(CadGeoKind.Horizontal, new[] { Whole("A") }));
            two.Geometric.Add(new CadGeoConstraint(CadGeoKind.Horizontal, new[] { Whole("A") }));
            CadSolveResult single = Solve(one, Shapes(Line("A", 0, 0, 10, 0)));
            CadSolveResult doubled = Solve(two, Shapes(Line("A", 0, 0, 10, 0)));

            Assert.False(single.Redundant);
            Assert.True(doubled.Redundant);
            Assert.Equal(single.Freedom, doubled.Freedom);
        }

        [Fact]
        public void AHorizontalDistance_StretchesALineAlongXWithItsStartKept()
        {
            var set = new CadConstraintSet();
            set.Dimensional.Add(
                new CadDimConstraint(
                    CadDimKind.Horizontal,
                    new[] { Point("A", CadPart.Start), Point("A", CadPart.End) },
                    "d1"
                )
            );
            CadSolveResult result = Solve(set, Shapes(Line("A", 0, 0, 10, 1)), "A", name =>
                name == "d1" ? 20.0 : (double?)null
            );

            Assert.True(result.Succeeded, result.Message);
            double[] v = result.Values["A"];
            Assert.Equal(0.0, v[0], 3);
            Assert.Equal(20.0, v[2] - v[0], 8);
            Assert.Equal(1.0, v[3], 2);
        }

        [Fact]
        public void AnAngleOfFifty_TurnsTheSecondLine()
        {
            var set = new CadConstraintSet();
            set.Dimensional.Add(new CadDimConstraint(CadDimKind.Angular, new[] { Whole("A"), Whole("B") }, "ang1"));
            CadSolveResult result = Solve(set, Shapes(Line("A", 60, 0, 70, 2), Line("B", 60, 0, 66, 8)), "A", name =>
                name == "ang1" ? 50.0 : (double?)null
            );

            Assert.True(result.Succeeded, result.Message);
            double[] a = result.Values["A"];
            double[] b = result.Values["B"];
            double turn = Math.Atan2(b[3] - b[1], b[2] - b[0]) - Math.Atan2(a[3] - a[1], a[2] - a[0]);
            Assert.Equal(50.0, turn * 180.0 / Math.PI, 6);
        }

        [Fact]
        public void ARadiusAndADiameter_ResizeCircles()
        {
            var set = new CadConstraintSet();
            set.Dimensional.Add(new CadDimConstraint(CadDimKind.Radius, new[] { Whole("C") }, "rad1"));
            set.Dimensional.Add(new CadDimConstraint(CadDimKind.Diameter, new[] { Whole("D") }, "dia1"));
            CadSolveResult result = Solve(set, Shapes(Circle("C", 0, 0, 4), Circle("D", 20, 0, 4)), null, name => name
                == "rad1"
                    ? 6.0
                : name == "dia1" ? 14.0
                : (double?)null);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(6.0, result.Values["C"][2], 8);
            Assert.Equal(7.0, result.Values["D"][2], 8);
        }

        [Fact]
        public void AnArcsEnds_StayOnItsCircle()
        {
            var set = new CadConstraintSet();
            set.Dimensional.Add(new CadDimConstraint(CadDimKind.Radius, new[] { Whole("R") }, "rad1"));
            var arc = new CadShape("R", CadShapeKind.Arc, new[] { 0.0, 0.0, 5.0, 5.0, 0.0, 0.0, 5.0 });
            CadSolveResult result = Solve(set, Shapes(arc), null, name => 8.0);

            Assert.True(result.Succeeded, result.Message);
            double[] v = result.Values["R"];
            Assert.Equal(8.0, v[2], 8);
            Assert.Equal(8.0, Math.Sqrt((v[3] - v[0]) * (v[3] - v[0]) + (v[4] - v[1]) * (v[4] - v[1])), 7);
            Assert.Equal(8.0, Math.Sqrt((v[5] - v[0]) * (v[5] - v[0]) + (v[6] - v[1]) * (v[6] - v[1])), 7);
        }

        [Fact]
        public void ConstraintsOnSeparateObjects_AreSolvedSeparately_AndTheSameEachTime()
        {
            var set = new CadConstraintSet();
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Horizontal, new[] { Whole("A") }));
            set.Geometric.Add(new CadGeoConstraint(CadGeoKind.Vertical, new[] { Whole("B") }));
            CadShape a = Line("A", 0, 0, 10, 1);
            CadShape b = Line("B", 20, 0, 21, 10);
            CadSolveResult one = Solve(set, Shapes(a, b));
            CadSolveResult two = Solve(set, Shapes(b, a));

            Assert.Equal(one.Values["A"], two.Values["A"]);
            Assert.Equal(one.Values["B"], two.Values["B"]);
            Assert.Equal(2, one.Rank);
        }

        [Theory]
        [InlineData("1+2*3", 7.0)]
        [InlineData("(1+2)*3", 9.0)]
        [InlineData("2^3^2", 512.0)]
        [InlineData("-4+10", 6.0)]
        [InlineData("sin(30)", 0.5)]
        [InlineData("sqrt(16)+pi*0", 4.0)]
        [InlineData("max(3,7)-min(3,7)", 4.0)]
        [InlineData("d1*2+3", 23.0)]
        [InlineData("10%4", 2.0)]
        public void AnExpression_ComesToItsValue(string text, double expected)
        {
            bool ok = CadConstraintExpression.TryEvaluate(
                text,
                name => name == "d1" ? 10.0 : (double?)null,
                out double value,
                out string error
            );

            Assert.True(ok, error);
            Assert.Equal(expected, value, 9);
        }

        [Theory]
        [InlineData("1/0")]
        [InlineData("2+")]
        [InlineData("(1+2")]
        [InlineData("nothing*2")]
        [InlineData("")]
        public void AnExpressionThatCannotBeWorkedOut_SaysWhy(string text)
        {
            bool ok = CadConstraintExpression.TryEvaluate(text, name => null, out _, out string error);

            Assert.False(ok);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void TheNamesAnExpressionReads_AreListedInOrder()
        {
            Assert.Equal(new[] { "d1", "width" }, CadConstraintExpression.NamesIn("d1*2+width-d1").ToArray());
            Assert.True(CadConstraintExpression.IsName("up_1"));
            Assert.False(CadConstraintExpression.IsName("1up"));
            Assert.False(CadConstraintExpression.IsName("sin"));
        }

        [Fact]
        public void ParametersThatNameOneAnotherInARing_AreRefused()
        {
            var set = new CadConstraintSet();
            set.Parameters.Add(new CadParameter("a", "b+1", 0.0, true));
            set.Parameters.Add(new CadParameter("b", "a+1", 0.0, true));

            Assert.False(CadConstraints.Evaluate(set, out string error));
            Assert.Contains("ring", error, StringComparison.Ordinal);
        }

        [Fact]
        public void ParametersMadeOfOthers_AreWorkedOutInTheirOrder()
        {
            var set = new CadConstraintSet();
            set.Parameters.Add(new CadParameter("up2", "up1*2", 0.0, true));
            set.Parameters.Add(new CadParameter("up1", "d1*2+3", 0.0, true));
            set.Parameters.Add(new CadParameter("d1", "10", 0.0, false));

            Assert.True(CadConstraints.Evaluate(set, out _));
            Assert.Equal(10.0, set.Parameter("d1").Value);
            Assert.Equal(23.0, set.Parameter("up1").Value);
            Assert.Equal(46.0, set.Parameter("up2").Value);
        }
    }
}
