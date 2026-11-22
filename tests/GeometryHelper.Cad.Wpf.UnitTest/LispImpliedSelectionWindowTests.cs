using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A routine run on objects selected first, through the window's own path: a click or a window with no command running
    /// selects them (the grips), then the routine's name is typed at the command line. A drafter's <c>(defun c:C1 () (ssget)
    /// (command "change" "p" "" "p" "la" "ColLayout" ""))</c> changes their layer with no question, as AutoCAD 2021 does: its
    /// <c>ssget</c> takes the selection, and the CHANGE it starts is answered by the P that follows, not by the selection.
    /// </summary>
    public sealed class LispImpliedSelectionWindowTests
    {
        private const string C1 =
            "(defun c:C1 () (ssget) (command \"change\" \"p\" \"\" \"p\" \"la\" \"ColLayout\" \"\"))";

        /// <summary>Three lines at y 0, 5 and 9 from x 0 to 10, on layer 0, and a layer ColLayout, with the routine defined.</summary>
        private static MainViewModel Open()
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "ColLayout", ColorIndex = 2 });
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 5, 0), new GeoPoint3(10, 5, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 9, 0), new GeoPoint3(10, 9, 0)));
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "lines.dxf"));
            model.Toggles.Single(t => t.Name == "SNAP").IsOn = false;
            model.Execute(C1);
            return model;
        }

        private static string[] Layers(MainViewModel model) =>
            model
                .Document.Database.ModelSpace.OfType<DxfLine>()
                .OrderBy(l => l.StartPoint.Y)
                .Select(l => l.Layer)
                .ToArray();

        [Fact]
        public void AWindowRoundOneLine_ThenC1Typed_ChangesItsLayerWithNoQuestion()
        {
            MainViewModel model = Open();

            // a window round the line at y 5 alone, dragged left to right, as the canvas makes it
            Assert.Single(model.SelectBox(new DisplayBox(-1, 4, 11, 6), false, false));
            int before = model.History.Count;

            model.Execute("C1");

            Assert.False(model.Session.IsRunning);
            Assert.False(model.Session.IsLispWaiting);
            Assert.Equal(new[] { "0", "ColLayout", "0" }, Layers(model));
            Assert.Equal(0, model.Document.Selection.Count);
            Assert.Equal(
                new[] { "Command: C1", "1 found", "change", "Select objects: p", "1 found" },
                model.History.Skip(before).Where(l => l.Length > 0).Take(5).ToArray()
            );
        }

        [Fact]
        public void TwoLinesClicked_ThenC1Typed_ChangesBoth()
        {
            MainViewModel model = Open();

            Assert.True(model.ClickAt(new GeoPoint2(5, 0), 0.5));
            Assert.True(model.ClickAt(new GeoPoint2(5, 9), 0.5, shift: true));
            Assert.Equal(2, model.Document.Selection.Count);

            model.Execute("C1");

            Assert.False(model.Session.IsRunning);
            Assert.Equal(new[] { "ColLayout", "0", "ColLayout" }, Layers(model));
        }

        [Fact]
        public void C1TypedWithNothingSelected_AsksSelectObjects_AndAWindowAnswersIt()
        {
            MainViewModel model = Open();

            model.Execute("C1");

            Assert.True(model.Session.IsRunning);
            Assert.Equal("Select objects:", model.Prompt.TrimEnd());

            model.SelectBox(new DisplayBox(-1, -1, 11, 6), false, false);
            model.Execute(string.Empty);

            Assert.False(model.Session.IsRunning);
            Assert.Equal(new[] { "ColLayout", "ColLayout", "0" }, Layers(model));
        }

        [Fact]
        public void AWindowThenARoutineThatErasesThePreviousSelection_ErasesWhatItSelectedBeforeAndNothingElse()
        {
            MainViewModel model = Open();
            model.Execute("(defun c:ER () (ssget) (command \"_.ERASE\" \"p\" \"\"))");
            model.SelectBox(new DisplayBox(-1, 4, 11, 10), false, false);

            model.Execute("ER");

            Assert.False(model.Session.IsRunning);
            Assert.Equal(
                new[] { 0.0 },
                model
                    .Document.Database.ModelSpace.OfType<DxfLine>()
                    .Where(l => !l.IsErased)
                    .Select(l => l.StartPoint.Y)
                    .ToArray()
            );
        }
    }
}
