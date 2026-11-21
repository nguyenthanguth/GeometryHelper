using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The examples of <c>src/python/examples/in_app</c> run as PYRUN runs a file and as PYLOAD loads one (P5 D1002, D1007),
    /// with the answers a user gives at their prompts. The files are copied beside the test assembly by one item of the test
    /// project, so that the text a reader sees on the guide page is the text run here. Each case checks what the example
    /// wrote (the command line and the palette), what it drew (kinds, coordinates, texts) and that one <c>Undo()</c> takes the
    /// whole run back.
    /// </summary>
    public sealed class ExampleTests : PythonHostTestBase
    {
        public ExampleTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>The path of an example, copied beside the test assembly into <c>examples</c>.</summary>
        private static string Example(string name)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "examples", name);
            Assert.True(File.Exists(path), path + " does not exist: the example is not written");
            return path;
        }

        /// <summary>Types a command, plays the answers at its prompts, and checks that each was asked for and none was left.</summary>
        private void Type(CadCommandSession session, string command, params Action<CadCommandSession>[] answers)
        {
            Owner.Session = session;
            Owner.Answer(answers);
            Assert.True(session.Feed(command));
            Owner.Drain();
            Assert.Empty(Owner.Unanswered);
            Assert.Equal(0, Owner.PendingAnswers);
        }

        private static void Near(double expected, double actual) => Assert.Equal(expected, actual, 6);

        #region Drawing H, and a drawing of layers

        /// <summary>
        /// Drawing H of D1006 in the document: a line (0, 0)-(300, 400) of 500, a circle at (1000, 0) of radius 120 (753.982 round,
        /// 45 238.934 across), a closed polyline rectangle 100 by 50 at (2000, 0) (300 round, 5 000 across) and a single-line text.
        /// </summary>
        /// <returns>The handles of the four, in that order.</returns>
        private static string[] DrawH(CadDocument document)
        {
            var rectangle = new DxfPolyline();
            rectangle.AddVertexAt(0, new GeoPoint2(2000, 0), 0, 0, 0);
            rectangle.AddVertexAt(1, new GeoPoint2(2100, 0), 0, 0, 0);
            rectangle.AddVertexAt(2, new GeoPoint2(2100, 50), 0, 0, 0);
            rectangle.AddVertexAt(3, new GeoPoint2(2000, 50), 0, 0, 0);
            rectangle.Closed = true;
            var entities = new DxfEntity[]
            {
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(300, 400, 0)),
                new DxfCircle(new GeoPoint3(1000, 0, 0), new GeoVector3(0, 0, 1), 120),
                rectangle,
                new DxfDBText
                {
                    TextString = "Plate",
                    Position = new GeoPoint3(0, 100, 0),
                    Height = 5,
                },
            };
            return Append(document, entities);
        }

        private static string[] Append(CadDocument document, params DxfEntity[] entities)
        {
            document.ApplyEdit("draw", null, scope =>
            {
                foreach (DxfEntity entity in entities)
                {
                    scope.Append(document.ModelSpace, entity);
                }
            });
            return entities.Select(entity => entity.Handle).ToArray();
        }

        private static DxfLine OnLayer(string layer, double y) =>
            new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(100, y, 0)) { Layer = layer };

        #endregion

        #region number_points.py

        [Fact]
        public void ThreePointsPickedFromANumberTyped_AreNumberedFromIt_AndTheRunIsOneUndoStep()
        {
            // Start 5 and three points: the texts 5, 6 and 7 of height 2.5, each at its point, in the order picked.
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = Run(
                session,
                Example("number_points.py"),
                Typed("5"),
                Picked(100, 200),
                Picked(300, 200),
                Picked(500, 200),
                EnterKey
            );

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(new[] { "3 points numbered, 5 to 7." }, Printed);
            Assert.Empty(Errors);
            Assert.Contains("3 points numbered, 5 to 7.", session.History);
            Assert.StartsWith("First number", Owner.Asked[0], StringComparison.Ordinal);
            Assert.StartsWith("Specify point", Owner.Asked[1], StringComparison.Ordinal);
            CadDocument document = session.Document;
            var texts = All<DxfDBText>(document);
            Assert.Equal(new[] { "5", "6", "7" }, texts.Select(text => text.TextString));
            Assert.Equal(new[] { 100.0, 300.0, 500.0 }, texts.Select(text => text.Position.X));
            Assert.Equal(new[] { 200.0, 200.0, 200.0 }, texts.Select(text => text.Position.Y));
            Assert.Equal(new[] { 2.5, 2.5, 2.5 }, texts.Select(text => text.Height));
            Assert.Equal(3, document.EntityCount);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void EnterAtTheFirstQuestion_NumbersFromOne()
        {
            CadCommandSession session = NewSession();

            Run(
                session,
                Example("number_points.py"),
                EnterKey,
                Picked(10, 20),
                Picked(30, 40),
                Picked(50, 60),
                EnterKey
            );

            Assert.Equal(new[] { "3 points numbered, 1 to 3." }, Printed);
            Assert.Equal(new[] { "1", "2", "3" }, All<DxfDBText>(session.Document).Select(text => text.TextString));
        }

        [Fact]
        public void EnterAtTheFirstPoint_SaysNoPointWasPicked_AndDrawsNothing()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = Run(session, Example("number_points.py"), EnterKey, EnterKey);

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(new[] { "No point picked." }, Printed);
            Assert.Contains("No point picked.", session.History);
            Assert.Equal(0, session.Document.EntityCount);
            Assert.False(session.Document.CanUndo);
            Assert.False(session.Document.IsModified);
        }

        [Fact]
        public void EscAtASecondPoint_CancelsTheRun_AndTheTextAlreadyDrawnGoesWithIt()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = Run(session, Example("number_points.py"), EnterKey, Picked(10, 20), EscKey);

            Assert.Equal(CadPythonOutcome.Cancelled, run.Outcome);
            Assert.Empty(Printed);
            Assert.Equal(0, session.Document.EntityCount);
            Assert.False(session.Document.CanUndo);
        }

        [Fact]
        public void TheTextsAreAddedToWhatTheDrawingHolds_AndUndoLeavesTheRest()
        {
            CadCommandSession session = NewSession();
            CadDocument document = session.Document;
            AddLine(document, 0);

            Run(session, Example("number_points.py"), EnterKey, Picked(10, 20), Picked(30, 40), EnterKey);

            Assert.Equal(3, document.EntityCount);
            Assert.True(document.Undo());
            DxfLine line = Assert.Single(All<DxfLine>(document));
            Assert.Equal(0.0, line.StartPoint.Y);
            Assert.Equal(1, document.EntityCount);
        }

        #endregion

        #region drawing_tools.py

        [Fact]
        public void PyLoadOfTheFile_DefinesRectcAndLayercount_AndSaysSo()
        {
            CadCommandSession session = NewSession();

            CadPythonRunEventArgs run = Load(session, Example("drawing_tools.py"));

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal("PYLOAD", run.Name);
            Assert.Contains("drawing_tools.py loaded: LAYERCOUNT, RECTC.", session.History);
            Assert.Contains("RECTC", Host.CommandNames);
            Assert.Contains("LAYERCOUNT", Host.CommandNames);
            Assert.NotNull(session.Registry.Find("RECTC"));
            Assert.Equal(0, session.Document.EntityCount);
        }

        [Fact]
        public void Rectc_DrawsAClosedRectangleAboutThePickedCentre_AndWritesNothing()
        {
            // The centre (500, 300), a width of 200 and a height of 100: corners (400, 250), (600, 250), (600, 350), (400, 350),
            // an area of 20 000, on layer 0, one undo step named after the command.
            CadCommandSession session = NewSession();
            Load(session, Example("drawing_tools.py"));
            int printedBefore = Printed.Count;

            Type(session, "RECTC", Picked(500, 300), Typed("200"), Typed("100"));

            Assert.StartsWith("Specify centre", Owner.Asked[0], StringComparison.Ordinal);
            Assert.StartsWith("Width", Owner.Asked[1], StringComparison.Ordinal);
            Assert.StartsWith("Height", Owner.Asked[2], StringComparison.Ordinal);
            Assert.Equal(printedBefore, Printed.Count);
            Assert.Empty(Errors);
            CadDocument document = session.Document;
            DxfPolyline rectangle = Assert.Single(All<DxfPolyline>(document));
            Assert.True(rectangle.Closed);
            Assert.Equal(4, rectangle.NumberOfVertices);
            Assert.Equal("0", rectangle.Layer);
            Near(20000, rectangle.Area);
            var corners = Enumerable
                .Range(0, 4)
                .Select(i => rectangle.GetPoint2dAt(i))
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                .ToList();
            Assert.Equal(new[] { 400.0, 600.0, 400.0, 600.0 }, corners.Select(p => p.X));
            Assert.Equal(new[] { 250.0, 250.0, 350.0, 350.0 }, corners.Select(p => p.Y));
            Assert.Equal("RECTC", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0, document.EntityCount);
        }

        [Fact]
        public void Rectc_WithEnterForWidthAndHeight_TakesTheirDefaultsOf1000And500()
        {
            // 1000 by 500 about (0, 0): corners (-500, -250) to (500, 250), an area of 500 000.
            CadCommandSession session = NewSession();
            Load(session, Example("drawing_tools.py"));

            Type(session, "RECTC", Picked(0, 0), EnterKey, EnterKey);

            DxfPolyline rectangle = Assert.Single(All<DxfPolyline>(session.Document));
            Near(500000, rectangle.Area);
            var corners = Enumerable.Range(0, 4).Select(i => rectangle.GetPoint2dAt(i)).ToList();
            Near(-500, corners.Min(p => p.X));
            Near(500, corners.Max(p => p.X));
            Near(-250, corners.Min(p => p.Y));
            Near(250, corners.Max(p => p.Y));
        }

        [Fact]
        public void Rectc_WithAWidthOfZero_IsRefusedWithAMessage_AndDrawsNothing()
        {
            // Not in D1002: what a user typing 0 at "Width" meets. RECTANG takes a rectangle of no width for no corner and
            // asks again, so feeding it would end in "RuntimeError: RECTANG still asked ..." and a traceback on the palette;
            // the example refuses with a message instead. The answers after the 0 are there for an example that asks again;
            // one that stops leaves them unused.
            CadCommandSession session = NewSession();
            Load(session, Example("drawing_tools.py"));
            int printedBefore = Printed.Count;
            Owner.Session = session;
            Owner.Answer(Picked(500, 300), Typed("0"), Typed("100"), Typed("100"));

            Assert.True(session.Feed("RECTC"));
            Owner.Drain();

            Assert.Empty(Errors);
            Assert.Equal(new[] { "The width and the height must be more than 0." }, Printed.Skip(printedBefore));
            Assert.Equal(0, session.Document.EntityCount);
        }

        [Fact]
        public void TheAliasRc_RunsRectc()
        {
            CadCommandSession session = NewSession();
            Load(session, Example("drawing_tools.py"));

            Type(session, "RC", Picked(0, 0), Typed("40"), Typed("20"));

            DxfPolyline rectangle = Assert.Single(All<DxfPolyline>(session.Document));
            Near(800, rectangle.Area);
        }

        [Fact]
        public void EnterAtTheCentre_DrawsNothing()
        {
            CadCommandSession session = NewSession();
            Load(session, Example("drawing_tools.py"));

            Type(session, "RECTC", EnterKey);

            Assert.Equal(0, session.Document.EntityCount);
            Assert.False(session.Document.CanUndo);
        }

        [Fact]
        public void Layercount_AfterARectangleOnLayerZero_WritesZeroColonOne()
        {
            CadCommandSession session = NewSession();
            Load(session, Example("drawing_tools.py"));
            Type(session, "RECTC", Picked(0, 0), EnterKey, EnterKey);
            int printedBefore = Printed.Count;

            Type(session, "LAYERCOUNT");

            Assert.Equal(new[] { "0: 1" }, Printed.Skip(printedBefore));
            Assert.Equal("RECTC", session.Document.NextUndo.Name);
        }

        [Fact]
        public void Layercount_WritesAnObjectCountForEachLayerThatHasObjects_SortedWithoutRegardToCase()
        {
            // Walls 2, slabs 1, 0 1 and Empty none: Empty is left out, and "slabs" sorts before "Walls" by
            // (name.casefold(), name), where a plain sort would put "Walls" first.
            CadCommandSession session = NewSession();
            CadDocument document = session.Document;
            foreach (string name in new[] { "Walls", "slabs", "Empty" })
            {
                document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = name });
            }

            Append(document, OnLayer("Walls", 0), OnLayer("slabs", 10), OnLayer("0", 20), OnLayer("Walls", 30));
            Load(session, Example("drawing_tools.py"));
            int printedBefore = Printed.Count;

            Type(session, "LAYERCOUNT");

            Assert.Equal(new[] { "0: 1", "slabs: 1", "Walls: 2" }, Printed.Skip(printedBefore));
            Assert.Equal(4, document.EntityCount);
        }

        [Fact]
        public void Layercount_CountsAnObjectOnALayerSpeltInAnotherCase_WithTheLayerItIsOn()
        {
            // Layer names are one name in whatever case, yet an entity keeps the spelling it was written with: a line on
            // "Walls" and one on "walls" are two objects on the one layer Walls, written with the table's spelling.
            CadCommandSession session = NewSession();
            CadDocument document = session.Document;
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Walls" });
            Append(document, OnLayer("Walls", 0), OnLayer("walls", 10));
            Load(session, Example("drawing_tools.py"));
            int printedBefore = Printed.Count;

            Type(session, "LAYERCOUNT");

            Assert.Equal(new[] { "Walls: 2" }, Printed.Skip(printedBefore));
        }

        #endregion

        #region selection_lengths.py

        [Fact]
        public void TheFourObjectsOfDrawingH_GiveALengthAnAreaAndOneSkipped()
        {
            // 500 + 753.982 + 300 = 1 553.982 of line, circle and rectangle; 45 238.934 + 5 000 = 50 238.934 closed; the text is
            // skipped. Nothing is drawn, so the run is no undo step: the step on top is still the one that drew H.
            CadCommandSession session = NewSession();
            string[] handles = DrawH(session.Document);

            CadPythonRunEventArgs run = Run(session, Example("selection_lengths.py"), Objects(handles), EnterKey);

            Assert.Equal(CadPythonOutcome.Completed, run.Outcome);
            Assert.Equal(new[] { "4 objects: length 1553.982, closed area 50238.934, 1 skipped" }, Printed);
            Assert.Contains("4 objects: length 1553.982, closed area 50238.934, 1 skipped", session.History);
            Assert.StartsWith("Select objects", Owner.Asked[0], StringComparison.Ordinal);
            CadDocument document = session.Document;
            Assert.Equal(4, document.EntityCount);
            Assert.Single(All<DxfLine>(document));
            Assert.Single(All<DxfCircle>(document));
            Assert.Single(All<DxfPolyline>(document));
            Assert.Equal("Plate", Assert.Single(All<DxfDBText>(document)).TextString);
            Assert.Equal("draw", document.NextUndo.Name);
        }

        [Fact]
        public void EnterAsTheFirstAnswer_SaysNothingSelected()
        {
            CadCommandSession session = NewSession();
            DrawH(session.Document);

            Run(session, Example("selection_lengths.py"), EnterKey);

            Assert.Equal(new[] { "Nothing selected." }, Printed);
            Assert.Contains("Nothing selected.", session.History);
        }

        [Fact]
        public void AnArcAndAnOpenPolyline_AddTheirLengths_AndAnOpenPolylineHasNoArea()
        {
            // A half circle of radius 120 is 376.991 long and the polyline (0, 0) (30, 0) (30, 40) is 70: 446.991. An open
            // polyline is no closed area, though its Area reads as one closed by a line (600 here).
            CadCommandSession session = NewSession();
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(30, 0), 0, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(30, 40), 0, 0, 0);
            string[] handles = Append(
                session.Document,
                new DxfArc(new GeoPoint3(0, 500, 0), 120, 0, Math.PI),
                polyline
            );

            Run(session, Example("selection_lengths.py"), Objects(handles), EnterKey);

            Assert.Equal(new[] { "2 objects: length 446.991, closed area 0.000, 0 skipped" }, Printed);
        }

        #endregion
    }
}
