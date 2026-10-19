using System.Threading;
using GeometryHelper.Viewer.Scripting;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class ScriptToleranceTests
    {
        [Fact]
        public void TheToleranceAScriptStartsWith_IsTheDefaultOne_NotTheGlobalOne()
        {
            // A function of nothing, so that no test has to change the process-wide tolerance to see it: it reads Default and not Global.
            Tolerance start = ScriptGlobals.StartTolerance();

            Assert.Equal(Tolerance.Default, start);
            Assert.Equal(Tolerance.DefaultEqualPoint, start.EqualPoint);
            Assert.Equal(Tolerance.DefaultEqualVector, start.EqualVector);
            Assert.Equal(Tolerance.DefaultEqualAngleRad, start.EqualAngleRad);
            Assert.Equal(Tolerance.DefaultEqualPlanar, start.EqualPlanar);
        }

        [Fact]
        public void TolOfAScript_AndTheSceneItBuilds_StartAtThatTolerance()
        {
            var globals = new ScriptGlobals(new SceneWriter(), null, CancellationToken.None);

            try
            {
                Assert.Equal(ScriptGlobals.StartTolerance(), globals.Tol);
                Assert.Equal(ScriptGlobals.StartTolerance(), globals.Scene.Scene.Tolerance);
            }
            finally
            {
                globals.Close();
            }
        }
    }
}
