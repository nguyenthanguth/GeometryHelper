using System;
using GeometryHelper.Cad.Lisp;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>rtos</c>, <c>angtos</c>, <c>distof</c> and <c>angtof</c> as functions of the interpreter: the arguments, the defaults
    /// taken from the drawing's units, and the errors. The text each gives for each mode is in <see cref="LispUnitsTests"/>.
    /// </summary>
    public sealed class LispUnitsWiringTests
    {
        private static string Output(string expression, Func<string, object> variable = null) =>
            new LispInterpreter { Variable = variable }
                .Run(expression)
                .Output;

        [Theory]
        [InlineData("(rtos 12.5 3 0)", "\"1'-1\\\"\"")] // AutoCAD 2021: "1'-1\""
        [InlineData("(rtos 0.5 4 2)", "\"1/2\\\"\"")] // AutoCAD 2021: "1/2\""
        [InlineData("(rtos 12.75 5 4)", "\"12 3/4\"")] // AutoCAD 2021: "12 3/4"
        [InlineData("(rtos 12.5 2 9)", "\"12.500000000\"")] // AutoCAD 2021: "12.500000000"
        [InlineData("(rtos 123.456 2)", "\"123.4560\"")] // blank drawing: LUPREC 4
        [InlineData("(rtos 5)", "\"5.0000\"")]
        [InlineData("(angtos 0.5 0 4)", "\"28.6479\"")] // AutoCAD 2021: "28.6479"
        [InlineData("(angtos 0.5 1 2)", "\"28d39'\"")] // AutoCAD 2021: "28d39'"
        [InlineData("(angtos 0.5 4 2)", "\"N 61d21' E\"")] // AutoCAD 2021: "N 61d21' E"
        [InlineData("(angtos 0.5)", "\"29\"")] // blank drawing: AUNITS 0, AUPREC 0
        [InlineData("(distof \"1.5E+01\" 1)", "15.0")] // AutoCAD 2021: 15.0
        [InlineData("(distof \"1'-6 1/2\\\"\" 4)", "18.5")] // AutoCAD 2021: 18.5
        [InlineData("(distof \"18\\\"\" 2)", "nil")] // AutoCAD 2021: nil
        [InlineData("(distof \"1.5\")", "1.5")]
        [InlineData("(angtof \"90\" 0)", "1.5708")] // AutoCAD 2021: 1.5708
        [InlineData("(angtof \"N45d0'E\" 4)", "0.785398")] // AutoCAD 2021: 0.785398
        [InlineData("(angtof \"abc\" 0)", "nil")] // AutoCAD 2021: nil
        [InlineData("(angtof \"90\")", "1.5708")]
        public void TheFunctionsAreWrittenAndReadAsAutoCadDoes(string expression, string expected)
        {
            Assert.Equal(expected, Output(expression));
        }

        [Fact]
        public void RtosWithNoPrecision_TakesTheDrawingsLuprec()
        {
            // The drawing's LUPREC 2: (rtos 1.5) is "1.50".
            Assert.Equal("\"1.50\"", Output("(rtos 1.5)", name => name == "LUPREC" ? 2 : (object)null));
        }

        [Fact]
        public void RtosWithNoMode_TakesTheDrawingsLunitsAndDimzin()
        {
            // LUNITS 4 at DIMZIN 0 drops the zero feet and the zero inches, AutoCAD 2021: (rtos 12.0 4 2) "1'", (rtos 1.0 4 2) "1\"".
            object Architectural(string name) => name == "LUNITS" ? 4 : (object)null;

            Assert.Equal("\"1\\\"\"", Output("(rtos 1.0)", Architectural));
            Assert.Equal("\"1'\"", Output("(rtos 12.0)", Architectural));

            // DIMZIN 8 drops the trailing zeros of a decimal: (rtos 1.0 2 2) is "1" in AutoCAD 2021.
            Assert.Equal("\"1\"", Output("(rtos 1.0)", name => name == "DIMZIN" ? 8 : (object)null));
        }

        [Fact]
        public void AngtosWithNoMode_TakesTheDrawingsAunitsAndAuprec()
        {
            object Units(string name) =>
                name == "AUNITS" ? 1
                : name == "AUPREC" ? 2
                : (object)null;

            Assert.Equal("\"28d39'\"", Output("(angtos 0.5)", Units));
        }

        [Theory]
        [InlineData("(rtos 12.5 6 2)", "; error: bad argument value: rtos mode: 6")] // AutoCAD 2021
        [InlineData("(rtos 12.5 2 -1)", "; error: bad argument value: rtos prec: -1")] // AutoCAD 2021
        [InlineData("(rtos \"a\")", "; error: bad argument type: numberp: \"a\"")] // AutoCAD 2021
        [InlineData("(angtos 0.5 5 2)", "; error: bad argument value: angtos mode: 5")] // AutoCAD 2021
        [InlineData("(distof \"1.5\" 6)", "; error: bad argument value: distof mode: 6")] // AutoCAD 2021
        [InlineData("(angtof \"90\" 5)", "; error: bad argument value: angtof mode: 5")] // AutoCAD 2021
        [InlineData("(rtos 1 2.0)", "; error: bad argument type: fixnump: 2.0")] // a guess: not measured
        [InlineData("(rtos)", "; error: too few arguments")]
        [InlineData("(rtos 1 2 3 4)", "; error: too many arguments")]
        [InlineData("(distof 5)", "; error: bad argument type: stringp 5")]
        public void AnArgumentRefused_GivesTheErrorLineOfAutoCad(string expression, string expected)
        {
            Assert.Equal(expected, Output(expression));
        }
    }
}
