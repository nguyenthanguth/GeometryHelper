using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The functions of AutoLISP that need no drawing, <c>angle</c>, <c>distance</c>, <c>polar</c>, <c>inters</c> and
    /// <c>cvunit</c>, against AutoCAD 2021 (accoreconsole, en-US, blank drawing, measurement q6): each row is an expression
    /// and the text AutoCAD printed for it. They are defined in a bare interpreter, as they need nothing of a drawing.
    /// </summary>
    [Collection("LispSerial")]
    public sealed class LispGeometryTests
    {
        private static string Out(string expression) => new LispInterpreter().Run(expression).Output;

        [Theory]
        [InlineData("(angle '(0 0) '(1 1))", "0.785398")] // AutoCAD 2021 (q6): 0.785398
        [InlineData("(angle '(0 0) '(-1 0))", "3.14159")] // AutoCAD 2021 (q6): 3.14159
        [InlineData("(angle '(0 0 5) '(0 -1 0))", "4.71239")] // AutoCAD 2021 (q6): 4.71239, the Z is not looked at
        [InlineData("(angle '(1 1) '(1 1))", "0.0")] // AutoCAD 2021 (q6): 0.0, no direction is none
        [InlineData("(angle '(10 10) '(10 20))", "1.5708")] // a quarter turn, from the plan's own numbers
        public void Angle_IsTheDirectionInThePlaneFromNoughtUpToATurn(string expression, string expected)
        {
            Assert.Equal(expected, Out(expression));
        }

        [Theory]
        [InlineData("(distance '(0 0) '(3 4))", "5.0")] // AutoCAD 2021 (q6): 5.0
        [InlineData("(distance '(0 0 0) '(1 2 2))", "3.0")] // AutoCAD 2021 (q6): 3.0, in space where both have a Z
        [InlineData("(distance '(0 0) '(1 2 2))", "2.23607")] // AutoCAD 2021 (q6): 2.23607, in the plane where one has none
        [InlineData("(distance '(1 1 9) '(4 5))", "5.0")] // ours: either having none flattens both, as q6's (0 0) (1 2 2)
        public void Distance_IsInSpaceWhenBothPointsHaveAZ(string expression, string expected)
        {
            Assert.Equal(expected, Out(expression));
        }

        [Theory]
        [InlineData("(polar '(0 0) 0 5)", "(5.0 0.0)")] // AutoCAD 2021 (q6): (5.0 0.0)
        [InlineData("(polar '(1 1 3) (/ pi 2) 2)", "(1.0 3.0 3.0)")] // AutoCAD 2021 (q6): (1.0 3.0 3.0), Z kept
        [InlineData("(polar '(0 0) pi 1)", "(-1.0 1.22465e-16)")] // AutoCAD 2021 (q6): (-1.0 1.22465e-16)
        [InlineData("(polar '(0 0) (/ pi 2) 1)", "(6.12323e-17 1.0)")] // ours: the cosine of a quarter turn, as the C library gives it
        [InlineData("(polar '(10 20) 0 -5)", "(5.0 20.0)")] // a negative distance goes the other way
        public void Polar_KeepsTheDimensionOfThePoint(string expression, string expected)
        {
            Assert.Equal(expected, Out(expression));
        }

        [Theory]
        [InlineData("(inters '(0 0) '(10 0) '(5 -5) '(5 5))", "(5.0 0.0)")] // AutoCAD 2021 (q6): (5.0 0.0)
        [InlineData("(inters '(0 0) '(1 0) '(5 -5) '(5 5))", "nil")] // AutoCAD 2021 (q6): nil, outside the first segment
        [InlineData("(inters '(0 0) '(1 0) '(5 -5) '(5 5) nil)", "(5.0 0.0)")] // AutoCAD 2021 (q6): (5.0 0.0), on the lines
        [InlineData("(inters '(0 0) '(10 0) '(0 1) '(10 1))", "nil")] // AutoCAD 2021 (q6): nil, parallel
        [InlineData("(inters '(0 0 0) '(10 0 0) '(5 -5 1) '(5 5 1))", "nil")] // AutoCAD 2021 (q6): nil, skew in space
        [InlineData("(inters '(0 0 0) '(10 0 0) '(5 -5 0) '(5 5 0))", "(5.0 0.0 0.0)")] // ours: the same lines meeting in space
        [InlineData("(inters '(0 0 0) '(10 0 0) '(5 -5 1) '(5 5 1) nil)", "nil")] // ours: skew lines never meet, segments or not
        [InlineData("(inters '(0 0) '(10 0) '(2 0) '(8 0))", "nil")] // ours: one line twice over has no single point
        [InlineData("(inters '(0 0) '(10 10) '(0 10) '(10 0))", "(5.0 5.0)")] // ours: the diagonals of a square
        [InlineData("(inters '(0 0) '(0 0) '(0 1) '(10 1))", "nil")] // ours: a segment with no length
        [InlineData("(inters '(0 0) '(10 0) '(5 -5 1) '(5 5 1))", "(5.0 0.0)")] // ours: a point with no Z makes all four flat
        public void Inters_ReturnsTheCrossingOfTwoLines_OnTheSegmentsUnlessToldOtherwise(
            string expression,
            string expected
        )
        {
            Assert.Equal(expected, Out(expression));
        }

        [Theory]
        [InlineData("(cvunit 1 \"inch\" \"mm\")", "25.4")] // AutoCAD 2021 (q6): 25.4
        [InlineData("(cvunit 1 \"m\" \"mm\")", "1000.0")] // AutoCAD 2021 (q6): 1000.0
        [InlineData("(cvunit 90 \"degree\" \"radian\")", "1.5708")] // AutoCAD 2021 (q6): 1.5708
        [InlineData("(cvunit '(1 2) \"inch\" \"mm\")", "(25.4 50.8)")] // AutoCAD 2021 (q6): (25.4 50.8)
        [InlineData("(cvunit 1 \"inch\" \"gallon\")", "nil")] // AutoCAD 2021 (q6): nil, a unit of another table
        [InlineData("(cvunit 1 \"foo\" \"mm\")", "nil")] // AutoCAD 2021 (q6): nil, no such unit
        [InlineData("(cvunit 1 \"INCH\" \"Mm\")", "25.4")] // ours: D57, letters of either case
        [InlineData("(cvunit 1 \"foot\" \"inch\")", "12.0")] // ours: the table's own factors
        [InlineData("(cvunit 1 \"mile\" \"km\")", "1.60934")] // ours: 1 609 344 mm
        [InlineData("(cvunit 1 \"square foot\" \"square inch\")", "144.0")] // ours: areas as the square of the length
        [InlineData("(cvunit 1 \"cubic yard\" \"cubic foot\")", "27.0")] // ours: volumes as the cube of the length
        [InlineData("(cvunit 200 \"grad\" \"degree\")", "180.0")] // ours: a grad is 1/400 of a turn
        [InlineData("(cvunit 1 \"lb\" \"g\")", "453.592")] // ours: mass
        [InlineData("(cvunit 2 \"hour\" \"minute\")", "120.0")] // ours: time
        [InlineData("(cvunit 1 \"inch\" \"degree\")", "nil")] // ours: a length is not an angle
        [InlineData("(cvunit 1 \"square foot\" \"foot\")", "nil")] // ours: nor an area a length
        [InlineData("(cvunit '(1 2) \"inch\" \"gallon\")", "nil")] // ours: a list with no conversion is nil whole
        public void Cvunit_ConvertsByTheTableOfOurOwn(string expression, string expected)
        {
            Assert.Equal(expected, Out(expression));
        }

        [Theory]
        [InlineData("(angle 5 6)", "; error: bad argument type: 2D/3D point: 5")] // a guess: AutoCAD's wording not measured
        [InlineData("(angle '(1) '(1 2))", "; error: bad argument type: 2D/3D point: (1)")] // a guess
        [InlineData("(distance '(1 2 3 4) '(1 2))", "; error: bad argument type: 2D/3D point: (1 2 3 4)")] // a guess
        [InlineData("(polar '(0 0) \"a\" 1)", "; error: bad argument type: numberp: \"a\"")] // a guess
        [InlineData("(inters '(0 0) '(1 1) \"x\" '(1 1))", "; error: bad argument type: 2D/3D point: \"x\"")] // a guess
        [InlineData("(cvunit \"a\" \"in\" \"mm\")", "; error: bad argument type: numberp: \"a\"")] // a guess
        [InlineData("(cvunit 1 5 \"mm\")", "; error: bad argument type: stringp 5")] // a guess
        [InlineData("(angle '(0 0))", "; error: too few arguments")]
        [InlineData("(inters '(0 0) '(1 1) '(0 1))", "; error: too few arguments")]
        [InlineData("(polar '(0 0) 0 1 2)", "; error: too many arguments")]
        public void AnArgumentOfTheWrongKind_IsAnErrorLine(string expression, string expected)
        {
            Assert.Equal(expected, Out(expression));
        }

        [Fact]
        public void TheFunctionsOfAnotherStage_StillAreNotDefinedInABareInterpreter()
        {
            Assert.Equal("; error: no function definition: ALERT", Out("(alert \"x\")"));
            Assert.Equal("; error: no function definition: GETVAR", Out("(getvar \"LUNITS\")"));
        }
    }
}
