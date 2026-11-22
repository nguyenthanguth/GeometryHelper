using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The options a split button's choice keeps for the prompts of its command (<c>StartCommandWithOptionCommand</c>), as AutoCAD's
    /// ribbon macros give them: "ARC \ C A" leaves the start point to the user, gives Center at the next prompt and Angle at the
    /// end prompt; "ARC ;" answers the first prompt with Enter; a wrong answer at a prompt left to the user is asked again and
    /// the options do not move on; "CIRCLE 3P TAN \ TAN \ TAN \" types the tangent override before each of three picks and draws
    /// a circle touching the three objects.
    /// </summary>
    public sealed class RibbonOptionQueueTests
    {
        private const string StartPoint = "Specify start point of arc or [Center]:";

        [Fact]
        public void ArcBackslashCenterAngle_WaitsAtTheStartPoint_GivesCenterAtTheNextPrompt_WaitsAtTheCenter_AndGivesAngleAtTheEndPrompt()
        {
            OnRoom(model =>
            {
                model.StartCommandWithOptionCommand.Execute("ARC \\ C A");

                // The start point is the user's: Center is not given at the first prompt, which offers it too.
                Assert.Equal(StartPoint, model.Prompt);

                model.Execute("100,0");
                Assert.Equal("Specify center point of arc:", model.Prompt);

                // The center point is the user's too; Angle waits for the prompt that offers it.
                model.Execute("0,0");
                Assert.StartsWith("Specify included angle", model.Prompt, StringComparison.Ordinal);
                model.Escape();
            });
        }

        [Fact]
        public void ArcSemicolon_AnswersTheFirstPromptWithEnter_AndContinuesFromTheEndOfTheLastLine()
        {
            OnRoom(model =>
            {
                model.Execute("LINE 0,0 100,0");
                model.Enter();

                model.StartCommandWithOptionCommand.Execute("ARC ;");

                Assert.StartsWith("Specify end point of arc", model.Prompt, StringComparison.Ordinal);
                model.Escape();
            });
        }

        [Fact]
        public void AWrongAnswerAtThePromptLeftToTheUser_IsAskedAgain_AndTheOptionsDoNotMoveOn()
        {
            OnRoom(model =>
            {
                model.StartCommandWithOptionCommand.Execute("ARC \\ C A");
                Assert.Equal(StartPoint, model.Prompt);

                // Not a point: the start point is asked again, and Center is still not given there (it would make the arc from
                // its center, not from its start).
                model.Execute("not a point");
                Assert.Equal(StartPoint, model.Prompt);
                Assert.True(model.IsCommandRunning);

                model.Execute("100,0");
                Assert.Equal("Specify center point of arc:", model.Prompt);
                model.Execute("0,0");
                Assert.StartsWith("Specify included angle", model.Prompt, StringComparison.Ordinal);
                model.Escape();
            });
        }

        [Fact]
        public void TheTanTanTanOfTheCircleSplit_DrawsACircleTouchingThreeLines()
        {
            OnRoom(model =>
            {
                // A triangle 100 wide whose sides are equal: its inscribed circle has its center at (50, 28.8675) and radius 28.8675.
                double top = 100.0 * Math.Sqrt(3.0) / 2.0;
                model.Execute("LINE 0,0 100,0");
                model.Enter();
                model.Execute(
                    "LINE 100,0 50," + top.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture)
                );
                model.Enter();
                model.Execute(
                    "LINE 50," + top.ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture) + " 0,0"
                );
                model.Enter();
                int before = model.Document.ModelSpace.Block.Count();

                model.StartCommandWithOptionCommand.Execute("CIRCLE 3P TAN \\ TAN \\ TAN \\");

                // The first point is the user's, with the tangent override already typed: the prompt asks for a point "to".
                Assert.StartsWith("to", model.Prompt, StringComparison.Ordinal);
                model.Execute("50,0");
                model.Execute(
                    "75," + (top / 2.0).ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture)
                );
                model.Execute(
                    "25," + (top / 2.0).ToString("0.#########", System.Globalization.CultureInfo.InvariantCulture)
                );

                Assert.False(model.IsCommandRunning);
                DxfCircle circle = Assert.Single(model.Document.ModelSpace.Block.OfType<DxfCircle>());
                Assert.Equal(before + 1, model.Document.ModelSpace.Block.Count());

                // Tangent to a line: the distance from the center to the line is the radius.
                double radius = circle.Radius;
                Assert.Equal(top / 3.0, radius, 3);
                Assert.Equal(50.0, circle.Center.X, 3);
                Assert.Equal(top / 3.0, circle.Center.Y, 3);

                foreach (DxfLine line in model.Document.ModelSpace.Block.OfType<DxfLine>())
                {
                    Assert.Equal(radius, DistanceToLine(circle.Center.X, circle.Center.Y, line), 3);
                }
            });
        }

        private static double DistanceToLine(double x, double y, DxfLine line)
        {
            double dx = line.EndPoint.X - line.StartPoint.X;
            double dy = line.EndPoint.Y - line.StartPoint.Y;
            return Math.Abs((dx * (line.StartPoint.Y - y)) - (dy * (line.StartPoint.X - x)))
                / Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static void OnRoom(Action<MainViewModel> check)
        {
            var model = new MainViewModel();
            model.NewCommand.Execute(null);
            check(model);
        }
    }
}
