using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// CAL as AutoCAD 2021's accoreconsole runs it: the value written as the command line writes it, the snap functions that ask
    /// "Select entity for END snap:", rad, the vector functions, cvunit, and 'CAL typed at another command's prompt. The plan:
    /// A1 line (0,0)-(100,0) on Steel, A2 line (0,0)-(0,50), A3 circle of radius 10 at (200,0), A4 arc of radius 20 at (300,0)
    /// from 0 to 90 degrees, A5 polyline, A6 text "NOTE" at (0,200), A7 block BOX at (500,0), A8 line (0,-50)-(100,-50).
    /// </summary>
    public class CalculatorSnapTests
    {
        private static CadCommandSession Start(params object[] inputs) => Run(Session(Plan()), inputs);

        [Theory]
        [InlineData("1/3", "0.333333333")]
        [InlineData("[1/3,2/3,1]", "0.333333333,0.666666667,1")]
        [InlineData("1e10", "1e+10")]
        [InlineData("123456789.5", "123456790")]
        [InlineData("123456.789", "123456.789")]
        [InlineData("0.0001234", "0.0001234")]
        [InlineData("[1,2,3]-[1,2,3]", "0,0,0")]
        [InlineData("[1,2,3]*[4,5,6]", "32")]
        [InlineData("[1.5,2.25,3]*2", "3,4.5,6")]
        [InlineData("vec([0,0],[3,4])", "3,4,0")]
        [InlineData("vec1([0,0],[3,4])", "0.6,0.8,0")]
        [InlineData("nor([0,0],[3,4])", "-0.8,0.6,0")]
        [InlineData("nor([3,4])", "-0.8,0.6,0")]
        [InlineData("nor([0,0],[3,4],[0,0,1])", "0.8,-0.6,0")]
        [InlineData("nor([0,0,0],[1,0,0],[0,1,0])", "0,0,1")]
        [InlineData("ang([0,0],[1,0],[0,1])", "90")]
        [InlineData("ang([0,0,0],[1,0,0],[0,1,0],[0,0,1])", "90")]
        [InlineData("dpl([5,5],[0,0],[10,0])", "5")]
        [InlineData("plt([0,0],[10,0],0.5)", "5,0,0")]
        [InlineData("pld([0,0],[10,0],3)", "3,0,0")]
        [InlineData("abs([1,2])", "2.23606798")]
        [InlineData("abs(-3)+0", "3")]
        [InlineData("2^3^2", "64")]
        [InlineData("-4^2", "16")]
        [InlineData("cvunit(1,mm,m)", "0.001")]
        [InlineData("cvunit(1,inch,mm)", "25.4")]
        [InlineData("cvunit(2.5,m,cm)", "250")]
        [InlineData("cvunit(180,deg,rad)", "3.14159265")]
        [InlineData("log(100)", "2")]
        [InlineData("pi", "3.14159265")]
        public void Calculator_Evaluates_AsAutoCADsCommandLineWritesIt(string expression, string expected)
        {
            Assert.Equal(expected, CadCalculator.Evaluate(expression).ToString());
        }

        [Theory]
        [InlineData("tan(45)", "Function _TAN takes no arguments")]
        [InlineData("10 mod 3", "Wrong syntax of expression")]
        [InlineData("vee(1)", "Function VEE takes no arguments")]
        [InlineData("vec(1,2)", "Use VEC(FromPt,ToPt)")]
        [InlineData("dpp([5,5],[0,0,0],[0,0,1])", "Use DPP(p,p1,p2,p3)")]
        [InlineData("pld([0,0],[10,0],3,2)", "Use PLD(p1,p2,dist)")]
        [InlineData("cvunit(1,foo,m)", "CVUNIT failed to convert units")]
        [InlineData("cvunit(1,mm,deg)", "CVUNIT failed to convert units")]
        [InlineData("end", "No object picked.")]
        [InlineData(
            "ilp([0,0],[1,1],[0,0,0],[0,0,1],[0,0,5])",
            "The three points defining the plane are coincident or collinear"
        )]
        public void Calculator_AWrongExpression_IsRefusedInAutoCADsWords(string expression, string message)
        {
            string said = Assert.Throws<CadCalcException>(() => CadCalculator.Evaluate(expression)).Message;
            Assert.Equal(
                message == "Function ROTX doesn't exist" ? "AutoLISP variable rotx has no value" : message,
                said
            );
        }

        [Fact]
        public void Calculator_LinePlane_GivesWhereTheLinePierces()
        {
            Assert.Equal("0,0,5", CadCalculator.Evaluate("ilp([0,0,0],[0,0,1],[1,0,5],[0,1,5],[1,1,5])").ToString());
        }

        [Fact]
        public void Inputs_AreListedInTheOrderTheExpressionAsksThem()
        {
            Assert.Equal(
                new[] { "Snap END", "Snap MID", "Point ", "Radius " },
                CadCalculator.Inputs("dist(end,mid)+cur*rad").Select(i => i.Kind + " " + i.Mode).ToArray()
            );
            Assert.Equal(new[] { "END", "END" }, CadCalculator.Inputs("vee").Select(i => i.Mode).ToArray());
            Assert.Equal(4, CadCalculator.Inputs("ille").Count);
            Assert.Equal(">> Select entity for QUA snap", CadCalculator.Inputs("qua")[0].Prompt);
            Assert.Equal(
                ">> Select circle, arc or polyline segment for RAD function",
                CadCalculator.Inputs("rad")[0].Prompt
            );
            Assert.Empty(CadCalculator.Inputs("cvunit(1,rad,deg)"));
            Assert.Equal(2, CadCalculator.PointsPicked("dist(cur, CUR) + curve"));
        }

        [Fact]
        public void Cal_End_AsksForAnObject_AndGivesTheEndNearestThePick()
        {
            CadCommandSession session = Start("CAL", "end");
            Assert.Equal(">> Select entity for END snap:", session.PromptText);
            Run(session, P(80, 0));

            Assert.False(session.IsRunning);
            Assert.Equal("100,0,0", session.History.Last());
            Near(new GeoPoint2(100, 0), session.LastPoint);
        }

        [Theory]
        [InlineData("mid", 30, 0, "50,0,0")]
        [InlineData("cen", 210, 0, "200,0,0")]
        [InlineData("qua", 208.66025403784438, 5, "210,0,0")]
        [InlineData("nea", 40, 0, "40,0,0")]
        public void Cal_Snaps_GiveWhatTheObjectGives(string function, double x, double y, string expected)
        {
            CadCommandSession session = Start("CAL", function, P(x, y));
            Assert.Equal(expected, session.History.Last());
        }

        [Fact]
        public void Cal_AnObjectThatHasNoSuchPoint_IsRefused_AndTheSamePromptComesAgain()
        {
            CadCommandSession session = Start("CAL", "cen", P(30, 0));
            Assert.Equal("Improper type of object picked.", session.History.Last());
            Assert.Equal(">> Select entity for CEN snap:", session.PromptText);

            Run(session, P(210, 0));
            Assert.Equal("200,0,0", session.History.Last());
        }

        [Fact]
        public void Cal_APickOnNothing_SaysNoObjectPicked_AndAsksAgain()
        {
            CadCommandSession session = Start("CAL", "mid", P(1000, 1000));
            Assert.Equal("No object picked.", session.History.Last());
            Assert.Equal(">> Select entity for MID snap:", session.PromptText);
            Assert.True(session.IsRunning);
        }

        [Fact]
        public void Cal_Int_NeedsTheTwoObjectsMeetingAtThePick()
        {
            CadCommandSession session = Start("CAL", "int", P(30, 0));
            Assert.Equal("Improper type of object picked.", session.History.Last());

            Run(session, P(0, 0));
            Assert.Equal("0,0,0", session.History.Last());
        }

        [Fact]
        public void Cal_Per_ReachesTheFootFromTheLastPoint()
        {
            CadCommandSession session = Session(Plan());
            session.LastPoint = P(50, 30);
            Run(session, "CAL", "per", P(40, 0));

            Assert.Equal("50,0,0", session.History.Last());
        }

        [Fact]
        public void Cal_Tan_TakesTheTangentPointNearestThePick_FromTheLastPoint()
        {
            CadCommandSession session = Session(Plan());
            session.LastPoint = P(230, 0);
            Run(session, "CAL", "tan", P(203.33, 9.4));

            string[] parts = session.History.Last().Split(',');
            Assert.Equal(203.333333, double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), 5);
            Assert.Equal(9.42809042, double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), 5);

            session.LastPoint = P(230, 0);
            Run(session, "CAL", "tan", P(203.33, -9.4));
            Assert.Equal(
                -9.42809042,
                double.Parse(session.History.Last().Split(',')[1], System.Globalization.CultureInfo.InvariantCulture),
                5
            );
        }

        [Fact]
        public void Cal_Rad_GivesTheRadiusOfACircleOrAnArc_AndRefusesAnythingElse()
        {
            CadCommandSession session = Start("CAL", "rad");
            Assert.Equal(">> Select circle, arc or polyline segment for RAD function:", session.PromptText);
            Run(session, P(30, 0));
            Assert.Equal("Improper type of object picked.", session.History.Last());

            Run(session, P(210, 0));
            Assert.Equal("10", session.History.Last());

            Run(session, "CAL", "rad+rad", P(210, 0), P(300, 20));
            Assert.Equal("30", session.History.Last());
        }

        [Fact]
        public void Cal_DistOfTwoEnds_AsksTwiceInTheOrderOfTheExpression()
        {
            CadCommandSession session = Start("CAL", "dist(end,end)", P(80, 0));
            Assert.Equal(">> Select entity for END snap:", session.PromptText);
            Run(session, P(10, -50));

            Assert.Equal("111.803399", session.History.Last());
        }

        [Fact]
        public void Cal_MidOfTwoEnds_Mee_PicksTwoEnds()
        {
            CadCommandSession session = Start("CAL", "mee", P(90, 0), P(90, -50));
            Assert.Equal("100,-25,0", session.History.Last());
        }

        [Fact]
        public void Cal_AnErrorAfterThePicks_WritesItAndAsksTheExpressionAgain()
        {
            CadCommandSession session = Start("CAL", "end/0", P(80, 0));
            Assert.Equal("Division by 0", session.History.Last());
            Assert.Equal(">> Expression:", session.PromptText);
        }

        [Fact]
        public void TransparentCal_AtAPointPrompt_TypesItsValueThere()
        {
            CadCommandSession session = Start("LINE", "'CAL");
            Assert.Equal(">>>> Expression:", session.PromptText);

            Run(session, "[10,20]+[1,1]");
            Assert.Contains("Resuming LINE command.", session.History);
            Assert.DoesNotContain("11,21,0", session.History.Where(h => !h.Contains("Specify")));
            Assert.StartsWith("Specify next point", session.PromptText);

            Run(session, P(50, 50), Enter);
            Assert.False(session.IsRunning);
            DxfLine line = All<DxfLine>(session.Document.ModelSpace).Last();
            Near(new GeoPoint3(11, 21, 0), line.StartPoint);
            Near(new GeoPoint3(50, 50, 0), line.EndPoint);
        }

        [Fact]
        public void TransparentCal_AtADistancePrompt_AnswersWithItsNumber()
        {
            CadCommandSession session = Start("CIRCLE", P(0, 0), "'CAL");
            Run(session, "3+4");

            Assert.False(session.IsRunning);
            Assert.Equal(7.0, All<DxfCircle>(session.Document.ModelSpace).Last().Radius, 9);
        }

        [Fact]
        public void TransparentCal_WithASnapFunction_AsksTheObjectAfterTheExpression()
        {
            CadCommandSession session = Start("LINE", "'CAL");
            Run(session, "mid");
            Assert.Equal(">>>> Select entity for MID snap:", session.PromptText);

            Run(session, P(30, 0));
            Assert.StartsWith("Specify next point", session.PromptText);
            Run(session, P(60, 60), Enter);

            DxfLine line = All<DxfLine>(session.Document.ModelSpace).Last();
            Near(new GeoPoint3(50, 0, 0), line.StartPoint);
        }

        [Fact]
        public void TransparentCal_EscapeEndsBothCommands()
        {
            CadCommandSession session = Start("LINE", "'CAL");
            Run(session, Esc);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void TransparentCal_AtTheCommandPrompt_IsCalAsItIs()
        {
            CadCommandSession session = Start("'CAL");
            Assert.Equal(">> Expression:", session.PromptText);
            Run(session, "1+2");
            Assert.Equal("3", session.History.Last());
        }
    }
}
