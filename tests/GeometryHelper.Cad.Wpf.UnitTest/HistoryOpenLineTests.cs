using System.Linq;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The history of the window follows a line a routine left open: the text of <c>princ</c> with no new line after it is shown
    /// as it was written, then made longer where the prompt of a get function and its answer go on it, as AutoCAD 2021 writes
    /// <c>Aq: x</c>; the model does not keep the short line as a line of its own.
    /// </summary>
    public class HistoryOpenLineTests
    {
        [Fact]
        public void ALineMadeLongerAfterItWasShown_ReplacesTheShortOneInTheModelsHistory()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));

                model.Execute("(defun c:r3 () (princ \"A\") (getstring \"q: \") (princ))");
                model.Execute("R3");

                // The routine waits at its prompt, the open line shown as written.
                Assert.Equal("A", model.History.Last());

                model.Execute("x");

                Assert.Equal("Aq: x", model.History.Last());
                Assert.DoesNotContain("A", model.History);
                Assert.Equal(1, model.History.Count(l => l.StartsWith("Command: R3", System.StringComparison.Ordinal)));
            }
        }
    }
}
