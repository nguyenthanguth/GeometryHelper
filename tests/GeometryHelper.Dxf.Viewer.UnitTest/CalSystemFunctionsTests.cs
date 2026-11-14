using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// CAL's getvar, the conversions between the world and the user coordinate system (w2u, u2w), rot, and the functions that
    /// take a vector apart (xof, yof, zof, xyof, rxof, ryof, rzof), with the values and the messages of AutoCAD 2021's
    /// accoreconsole: <c>getvar(ltscale)</c> 1, <c>getvar(nosuchvar)</c> "Unknown AutoCAD system variable NOSUCHVAR",
    /// <c>rot([1,0,0],[0,0,0],[0,1,0],90)</c> 0,0,-1.
    /// </summary>
    public class CalSystemFunctionsTests
    {
        private static string Value(CadCommandSession session, string expression) =>
            CadCalculator.Evaluate(expression, Array.Empty<CadCalcValue>(), session).ToString();

        private static string Refused(CadCommandSession session, string expression) =>
            Assert
                .Throws<CadCalcException>(() =>
                    CadCalculator.Evaluate(expression, Array.Empty<CadCalcValue>(), session)
                )
                .Message;

        private static CadCommandSession Blank0() => Session(Blank());

        private static CadCommandSession WithUcs(GeoPoint2 origin, double angle)
        {
            CadDocument document = Blank();
            document.Ucs.SetCurrent(CadUcs.InPlane(origin, angle));
            return Session(document);
        }

        #region getvar

        // A drawing with TEXTSIZE 3.5 (a real) and the variables of the kinds the registry takes from the window: an angle of 30
        // degrees, a switch that is on, a point of two coordinates.
        private static CadCommandSession WithVariables()
        {
            CadCommandSession session = Run(Blank0(), "TEXTSIZE", "3.5");
            session.Registry.AddVariable(CadSystemVariable.Angle("MYANGLE", () => Math.PI / 6.0, v => { }));
            session.Registry.AddVariable(CadSystemVariable.Switch("MYSWITCH", () => true, v => { }));
            session.Registry.AddVariable(
                CadSystemVariable.Point("MYPOINT", () => new GeoPoint2(21.5, 11.25), v => { })
            );
            return session;
        }

        [Theory]
        [InlineData("getvar(textsize)", "3.5")]
        [InlineData("GETVAR(TEXTSIZE)*2", "7")]
        [InlineData("getvar( textsize )", "3.5")]
        [InlineData("getvar(textsize)+1", "4.5")]
        [InlineData("getvar(pdmode)", "0")]
        [InlineData("getvar(tilemode)", "1")]
        [InlineData("getvar(ucsorg)", "0,0,0")]
        [InlineData("getvar(insbase)", "0,0,0")]
        // AutoCAD 2021: SNAPANG 30, then getvar(snapang) is 0.523598776; ORTHOMODE on is 1; VIEWCTR is 21.320188,11.2226824,0.
        [InlineData("getvar(myangle)", "0.523598776")]
        [InlineData("getvar(myswitch)+1", "2")]
        [InlineData("getvar(mypoint)", "21.5,11.25,0")]
        public void Getvar_GivesTheNumberOrThePointOfAVariable_AsAutoCADWritesIt(string expression, string expected)
        {
            Assert.Equal(expected, Value(WithVariables(), expression));
        }

        [Theory]
        [InlineData("getvar(nosuchvar)", "Unknown AutoCAD system variable NOSUCHVAR")]
        [InlineData("getvar(\"textsize\")", "Unknown AutoCAD system variable \"TEXTSIZE\"")]
        [InlineData("getvar()", "Use GETVAR(AutoCAD_system_variable)")]
        [InlineData("getvar", "Use GETVAR(AutoCAD_system_variable)")]
        [InlineData("getvar textsize", "Use GETVAR(AutoCAD_system_variable)")]
        [InlineData("getvar(1)", "Use GETVAR(AutoCAD_system_variable)")]
        [InlineData("getvar([1,2])", "Use GETVAR(AutoCAD_system_variable)")]
        [InlineData("getvar(getvar(textsize))", "Use GETVAR(AutoCAD_system_variable)")]
        [InlineData("getvar(textsize,1)", "Right parenthesis ')' missing")]
        [InlineData("getvar(textsize", "Right parenthesis ')' missing")]
        [InlineData("getvar(clayer)", "Only real, integer or point AutoCAD system variables accepted")]
        [InlineData("getvar(clayer)+1", "Only real, integer or point AutoCAD system variables accepted")]
        [InlineData("getvar(dwgname)", "Only real, integer or point AutoCAD system variables accepted")]
        [InlineData("getvar(textsize)+[1,2]", "Operators '+', '-' require operands of the same type")]
        public void Getvar_AWrongCall_IsRefusedInAutoCADsWords(string expression, string message)
        {
            Assert.Equal(message, Refused(WithVariables(), expression));
        }

        [Fact]
        public void Getvar_WithNoDrawing_KnowsNoVariable()
        {
            Assert.Equal(
                "Unknown AutoCAD system variable TEXTSIZE",
                Assert.Throws<CadCalcException>(() => CadCalculator.Evaluate("getvar(textsize)")).Message
            );
        }

        [Fact]
        public void Cal_Getvar_WritesTheValueAtTheCommandLine()
        {
            CadCommandSession session = Run(WithVariables(), "CAL", "getvar(textsize)+1");

            Assert.Equal("4.5", session.History.Last());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Cal_Getvar_OfAVariableNotThere_WritesTheErrorAndAsksAgain()
        {
            CadCommandSession session = Run(Blank0(), "CAL", "getvar(nosuchvar)");

            Assert.Contains("Unknown AutoCAD system variable NOSUCHVAR", session.History);
            Assert.Equal(">> Expression:", session.PromptText);
        }

        [Fact]
        public void TheInputsOfAnExpressionWithGetvar_AreNone()
        {
            Assert.Empty(CadCalculator.Inputs("getvar(textsize)+w2u([1,2,3])"));
        }

        #endregion

        #region w2u and u2w

        [Fact]
        public void W2uAndU2w_FollowTheOriginOfTheUcs()
        {
            // AutoCAD 2021, UCS Origin 10,20,0: w2u([1,2,3]) -9,-18,3, u2w([1,2,3]) 11,22,3, w2u([10,20,0]) 0,0,0.
            CadCommandSession session = WithUcs(new GeoPoint2(10, 20), 0.0);

            Assert.Equal("-9,-18,3", Value(session, "w2u([1,2,3])"));
            Assert.Equal("11,22,3", Value(session, "u2w([1,2,3])"));
            Assert.Equal("0,0,0", Value(session, "w2u([10,20,0])"));
            Assert.Equal("10,20,0", Value(session, "u2w([0,0,0])"));
            Assert.Equal("-9,-18,0", Value(session, "w2u([1,2])"));
            Assert.Equal("-8,-17,4", Value(session, "w2u([1,2,3])+[1,1,1]"));
            Assert.Equal("-10,-20,0", Value(session, "w2u(w2u([10,20,0]))"));
        }

        [Fact]
        public void W2uAndU2w_FollowTheRotationOfTheUcs()
        {
            // AutoCAD 2021, UCS Z 90: w2u([1,0,0]) 0,-1,0, u2w([1,0,0]) 0,1,0, w2u([5,5,5]) 5,-5,5.
            CadCommandSession session = WithUcs(new GeoPoint2(0, 0), Math.PI / 2.0);

            Assert.Equal("0,-1,0", Value(session, "w2u([1,0,0])"));
            Assert.Equal("0,1,0", Value(session, "u2w([1,0,0])"));
            Assert.Equal("5,-5,5", Value(session, "w2u([5,5,5])"));
        }

        [Fact]
        public void W2uAndU2w_InTheWorldSystem_ChangeNothing()
        {
            Assert.Equal("1,2,3", Value(Blank0(), "w2u([1,2,3])"));
            Assert.Equal("1,2,3", CadCalculator.Evaluate("u2w([1,2,3])").ToString());
        }

        [Theory]
        [InlineData("w2u(1)", "Function W2U requires vector argument")]
        [InlineData("u2w(1)", "Function U2W requires vector argument")]
        [InlineData("u2w()", "Function U2W requires one argument")]
        [InlineData("w2u", "Function W2U requires one argument")]
        [InlineData("u2w", "Function U2W requires one argument")]
        [InlineData("w2u([1,2,3],[4,5,6])", "Function W2U requires one argument")]
        public void W2uAndU2w_AWrongCall_IsRefusedInAutoCADsWords(string expression, string message)
        {
            Assert.Equal(message, Refused(Blank0(), expression));
        }

        #endregion

        #region rot

        [Theory]
        [InlineData("rot([1,0,0],[0,0,0],90)", "0,1,0")]
        [InlineData("rot([1,0,0],[0,0,0],[0,0,1],90)", "0,1,0")]
        [InlineData("rot([1,0,0],[0,0,0],[0,1,0],90)", "0,0,-1")]
        [InlineData("rot([1,0,0],[0,0,0],1)", "0.999847695,0.0174524064,0")]
        [InlineData("rot([1,0,0],[0,0,0],45)", "0.707106781,0.707106781,0")]
        [InlineData("rot([1,2],[1,1],180)", "1,0,0")]
        [InlineData("rot([1,2,3],[1,1,0],180)", "1,0,3")]
        [InlineData("rot([1,2,3],[1,1,5],180)", "1,0,3")]
        public void Rot_TurnsAPointAboutAnAxis_InDegrees(string expression, string expected)
        {
            Assert.Equal(expected, Value(Blank0(), expression));
        }

        [Theory]
        [InlineData("rot(1,[0,0,0],90)")]
        [InlineData("rot([1,0,0],1,90)")]
        [InlineData("rot([1,0,0],[0,0,0],[1,0,0])")]
        [InlineData("rot([1,0,0],[0,0,0],[0,0,1],[1,2,3])")]
        [InlineData("rot([1,0,0],[0,0,0],[0,0,1],90,1)")]
        [InlineData("rot()")]
        [InlineData("rot")]
        [InlineData("rot([1,0,0],90)")]
        public void Rot_AWrongCall_IsToldItsTwoForms(string expression)
        {
            Assert.Equal("Use ROT(p,origin,angle) or ROT(p,p1ax,p2ax,angle)", Refused(Blank0(), expression));
        }

        [Fact]
        public void Rot_AnAxisOfOnePoint_Or_APointOnTheAxis_IsRefused()
        {
            Assert.Equal(
                "The points 'p1ax' and 'p2ax' in ROT(p,p1ax,p2ax,angle) coincide",
                Refused(Blank0(), "rot([1,0,0],[0,0,0],[0,0,0],90)")
            );
            Assert.Equal(
                "The point to be rotated lies on the axis of rotation",
                Refused(Blank0(), "rot([1,0,0],[0,0,0],[1,0,0],90)")
            );
            Assert.Equal(
                "The point to be rotated lies on the axis of rotation",
                Refused(Blank0(), "rot([1,1,1],[0,0,0],[1,1,1],45)")
            );

            // The axis of the three-argument form is the vertical one through the origin.
            Assert.Equal(
                "The point to be rotated lies on the axis of rotation",
                Refused(Blank0(), "rot([1,1,7],[1,1,0],90)")
            );
            Assert.Equal(
                "The point to be rotated lies on the axis of rotation",
                Refused(Blank0(), "rot([1,1,0],[1,1,0],90)")
            );
        }

        #endregion

        #region xof and the others

        [Theory]
        [InlineData("xyof([1,2,3])", "1,2,0")]
        [InlineData("xof([1,2,3])", "1,0,0")]
        [InlineData("yof([1,2,3])", "0,2,0")]
        [InlineData("zof([1,2,3])", "0,0,3")]
        [InlineData("xof(1)", "1,0,0")]
        [InlineData("yof(1)", "0,1,0")]
        [InlineData("zof(1)", "0,0,1")]
        [InlineData("rxof([1,2,3])", "1")]
        [InlineData("ryof([1,2,3])", "2")]
        [InlineData("rzof([1,2,3])", "3")]
        public void TheFunctionsThatTakeAVectorApart_GiveWhatAutoCADGives(string expression, string expected)
        {
            Assert.Equal(expected, Value(Blank0(), expression));
        }

        [Theory]
        [InlineData("xyof(1)", "Function XYof requires vector argument")]
        [InlineData("rxof(1)", "Function rXof requires vector argument")]
        [InlineData("ryof(1)", "Function rYof requires vector argument")]
        [InlineData("rzof(1)", "Function rZof requires vector argument")]
        [InlineData("xof()", "Function Xof requires one argument")]
        [InlineData("yof()", "Function Yof requires one argument")]
        [InlineData("zof()", "Function Zof requires one argument")]
        [InlineData("xyof()", "Function XYof requires one argument")]
        [InlineData("rxof()", "Function rXof requires one argument")]
        [InlineData("rxof([1,2,3],[1,2,3])", "Function rXof requires one argument")]
        [InlineData("xyof([1,2,3],1)", "Function XYof requires one argument")]
        [InlineData("xof", "Function Xof requires one argument")]
        [InlineData("rxof", "Function rXof requires one argument")]
        public void TheFunctionsThatTakeAVectorApart_AWrongCall_IsRefusedInAutoCADsWords(
            string expression,
            string message
        )
        {
            Assert.Equal(message, Refused(Blank0(), expression));
        }

        #endregion

        #region rad of a straight polyline segment

        [Fact]
        public void Cal_Rad_OnAStraightSegmentOfAPolyline_SaysAnArcSegmentMustBePicked_AndAsksAgain()
        {
            // AutoCAD 2021: a pick on the straight segment of a polyline says "Polyline arc segment must be picked." and asks again;
            // a line is "Improper type of object picked.".
            var database = TestDrawings.Drawing(
                string.Empty,
                string.Empty,
                TestDrawings.Polyline("0", "P1", false, 0, 0, 0, 100, 0, 1, 100, 100, 0, 0, 100, 0),
                TestDrawings.Line(300, 0, 400, 0, handle: "L1")
            );
            CadCommandSession session = Run(Session(CadDocument.From(database, "arc.dxf")), "CAL", "rad");

            Run(session, P(50, 0));
            Assert.Equal("Polyline arc segment must be picked.", session.History.Last());
            Assert.Equal(">> Select circle, arc or polyline segment for RAD function:", session.PromptText);

            Run(session, P(350, 0));
            Assert.Equal("Improper type of object picked.", session.History.Last());

            Run(session, P(150, 50));
            Assert.Equal("50", session.History.Last());
        }

        #endregion
    }
}
