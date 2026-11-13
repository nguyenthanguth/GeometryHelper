using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// -VPORTS MOde as AutoCAD 2021's accoreconsole runs it: "Apply viewport configuration to [Display/Current viewport]
    /// &lt;Display&gt;:", then the options again; 2, 3 and 4 split the current viewport in either mode (probed from one to seven
    /// viewports: the same corners, the same viewport current).
    /// </summary>
    public class VportsModeTests
    {
        private const string OptionPrompt =
            "Enter an option [Save/Restore/Delete/Join/SIngle/?/2/3/4/Toggle/MOde] <3>:";

        private const string ModePrompt = "Apply viewport configuration to [Display/Current viewport] <Display>:";

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static string Corners(CadDocument document) =>
            string.Join(" ", document.TiledViewports.Tiles.Select(t => t.Id + ":" + t.Corners));

        [Fact]
        public void Mode_AsksWhereConfigurationsApply_ThenTheOptionsAgain()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "-VPORTS", "MO");
            Assert.Equal(ModePrompt, session.PromptText);

            Run(session, "C");
            Assert.True(session.IsRunning);
            Assert.Equal(OptionPrompt, session.PromptText);
            Assert.True(document.TiledViewports.AppliesToCurrentViewport);

            Run(session, "MO");
            Assert.Equal(ModePrompt, session.PromptText);
            Run(session, Enter);
            Assert.False(document.TiledViewports.AppliesToCurrentViewport);
            Assert.Equal(OptionPrompt, session.PromptText);

            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.False(document.TiledViewports.IsTiled);
        }

        [Fact]
        public void Mode_AnswerDisplay_IsTheDefaultAndLongFormsAreTaken()
        {
            CadDocument document = Plan();
            Start(document, "-VPORTS", "MO", "Current viewport", "MO", "Display");
            Assert.False(document.TiledViewports.AppliesToCurrentViewport);

            Start(document, "-VPORTS", "MO", "Current viewport", "SI");
            Assert.True(document.TiledViewports.AppliesToCurrentViewport);
        }

        [Fact]
        public void Mode_ThenTwoVertical_SplitsAsItDoesWithNoMode()
        {
            var results = new List<string>();

            foreach (
                string[] inputs in new[]
                {
                    new[] { "-VPORTS", "2", "V" },
                    new[] { "-VPORTS", "MO", "D", "2", "V" },
                    new[] { "-VPORTS", "MO", "C", "2", "V" },
                }
            )
            {
                CadDocument document = Plan();
                CadCommandSession session = Start(document, inputs);
                Assert.False(session.IsRunning);
                results.Add(Corners(document));
            }

            Assert.Equal("2:0.5000,0.0000 1.0000,1.0000 3:0.0000,0.0000 0.5000,1.0000", results[0]);
            Assert.Equal(results[0], results[1]);
            Assert.Equal(results[0], results[2]);
        }

        [Fact]
        public void Mode_InEither_ThreeRightAndFour_SplitTheCurrentViewportOnly()
        {
            foreach (string mode in new[] { "D", "C" })
            {
                CadDocument document = Plan();
                Start(document, "-VPORTS", "MO", mode, "2", "V", "-VPORTS", "3", "R");
                Assert.Equal(
                    "2:0.7500,0.0000 1.0000,1.0000 3:0.0000,0.0000 0.5000,1.0000 "
                        + "4:0.5000,0.5000 0.7500,1.0000 5:0.5000,0.0000 0.7500,0.5000",
                    Corners(document)
                );

                Start(document, "-VPORTS", "4");
                Assert.Equal(
                    "2:0.8750,0.0000 1.0000,0.5000 3:0.0000,0.0000 0.5000,1.0000 4:0.5000,0.5000 0.7500,1.0000 "
                        + "5:0.5000,0.0000 0.7500,0.5000 6:0.8750,0.5000 1.0000,1.0000 "
                        + "7:0.7500,0.5000 0.8750,1.0000 8:0.7500,0.0000 0.8750,0.5000",
                    Corners(document)
                );
            }
        }
    }
}
