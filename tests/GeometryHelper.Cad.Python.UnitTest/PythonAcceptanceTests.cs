using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The acceptance of Python commands like AutoLISP's (D1677). C1: a command loaded from a Pythons folder at start, run in
    /// the app, moves a selection to the layer of a picked object, and one U takes it back; the outside half is
    /// <c>tests/python/remote/test_c12_acceptance.py</c>, with the same function body
    /// (<c>tests/python/c1_acceptance.py</c>). DIMLAY: a routine written in AutoLISP and its port in Python run on two
    /// copies of one drawing give the same drawing, compared object by object after both are saved and read again.
    /// </summary>
    public sealed class PythonAcceptanceTests : PythonHostTestBase
    {
        public PythonAcceptanceTests(PythonHostFixture fixture)
            : base(fixture) { }

        /// <summary>The folder of <c>c1_acceptance.py</c>, found above the test assembly.</summary>
        private static string PythonTestsFolder()
        {
            string folder = AppContext.BaseDirectory;

            while (folder != null && !File.Exists(Path.Combine(folder, "tests", "python", "c1_acceptance.py")))
            {
                folder = Path.GetDirectoryName(folder);
            }

            Assert.NotNull(folder);
            return Path.Combine(folder, "tests", "python");
        }

        /// <summary>Loads the files of a Pythons folder as the app does at its start.</summary>
        private void LoadFromPythons(CadCommandSession session, string pythons)
        {
            Owner.Session = session;
            IReadOnlyList<string> files = CadPythonStartup.FilesToLoad(new[] { pythons }, Array.Empty<string>());
            Assert.NotEmpty(files);
            bool started = new CadPythonStartup(Host).Run(session, files);
            Owner.Drain();
            Assert.True(started);
            Assert.Empty(Errors);
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

        private static DxfLine LineAt(double y, string layer) =>
            new DxfLine(new GeoPoint3(0, y, 0), new GeoPoint3(100, y, 0)) { Layer = layer };

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

        private static List<string> Layers(CadDocument document) =>
            document
                .Database.ModelSpace.Where(e => !e.IsErased)
                .OrderBy(e => ((DxfLine)e).StartPoint.Y)
                .Select(e => e.Layer)
                .ToList();

        #region C1

        [Fact]
        public void C1LoadedFromThePythonsFolderAtStart_MovesTheSelectionToThePickedLayer_AndOneUndoTakesItBack()
        {
            // A on y = 0 (the picked), B at 50, 0 at 100 (both selected), B at 150 (not selected): after C1 the lines at 50 and
            // 100 are on A and the one at 150 is still on B; the command is one undo step named C1, and one U puts the layers back.
            string pythons = Path.Combine(Folder, "Pythons");
            Directory.CreateDirectory(pythons);
            WriteFile(
                pythons,
                "c1.py",
                Lines(
                    "import sys",
                    "sys.path.insert(0, " + Raw(PythonTestsFolder()) + ")",
                    "from c1_acceptance import move_the_selection_to_the_layer_of_the_picked_object",
                    "from geometryhelper.cad import active_drawing, command",
                    "",
                    "@command('C1')",
                    "def c1() -> None:",
                    "    move_the_selection_to_the_layer_of_the_picked_object(active_drawing())"
                )
            );
            CadCommandSession session = NewSession();
            CadDocument document = session.Document;
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "A" });
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "B" });
            string[] handles = Append(document, LineAt(0, "A"), LineAt(50, "B"), LineAt(100, "0"), LineAt(150, "B"));
            LoadFromPythons(session, pythons);
            Assert.NotNull(session.Registry.Find("C1"));
            Assert.Equal(new[] { "A", "B", "0", "B" }, Layers(document));

            Type(session, "C1", Objects(handles[0]), Objects(handles[1], handles[2]), EnterKey);

            Assert.Equal(new[] { "A", "A", "A", "B" }, Layers(document));
            Assert.Equal("C1", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(new[] { "A", "B", "0", "B" }, Layers(document));
            Assert.NotEqual("C1", document.NextUndo?.Name);
        }

        #endregion

        #region DIMLAY

        private const string DimlayLisp =
            "(defun c:DIMLAY ( / ss i ed) "
            + "(setq ss (ssget '((0 . \"DIMENSION\")))) "
            + "(if ss (progn "
            + "(if (not (tblsearch \"LAYER\" \"DIM\")) (entmake '((0 . \"LAYER\") (100 . \"AcDbSymbolTableRecord\") "
            + "(100 . \"AcDbLayerTableRecord\") (2 . \"DIM\") (70 . 0) (62 . 7) (6 . \"Continuous\")))) "
            + "(setq i 0) "
            + "(while (< i (sslength ss)) "
            + "(setq ed (entget (ssname ss i) '(\"ACAD\"))) "
            + "(setq ed (subst (cons 8 \"DIM\") (assoc 8 ed) ed)) "
            + "(setq ed (append (vl-remove-if '(lambda (g) (= (car g) -3)) ed) "
            + "(list (list -3 (list \"ACAD\" '(1000 . \"DSTYLE\") '(1002 . \"{\") '(1070 . 140) '(1040 . 2.5) '(1002 . \"}\")))))) "
            + "(entmod ed) "
            + "(setq i (1+ i))))) "
            + "(princ))";

        private static readonly string[] DimlayPython =
        {
            "from geometryhelper.cad import active_drawing, command",
            "",
            "@command('DIMLAY')",
            "def dimlay() -> None:",
            "    drawing = active_drawing()",
            "    dimensions = [e for e in drawing.pick_objects('Select dimensions') if e.dxf_type == 'DIMENSION']",
            "    if not dimensions:",
            "        return",
            "    if 'DIM' not in drawing.layers:",
            "        drawing.layers.add('DIM', color=7, linetype='Continuous')",
            "    for e in dimensions:",
            "        e.layer = 'DIM'",
            "        e.net.Dimtxt = 2.5",
            "        drawing.modify(e)",
            "    drawing.commit()",
        };

        /// <summary>Three linear dimensions of 100, 200 and 300 on layer 0, made by DIMLINEAR as a user makes them, saved as a file.</summary>
        private string DimensionsFile()
        {
            CadDocument document = Blank("dims.dxf");
            // The style Standard writes the words 3.5 high, so that the override of 2.5 is a change DIMLAY makes.
            DxfDimStyleTableRecord standard = document.Database.DimStyleTable["Standard"];
            standard.Dimtxt = 3.5;
            document.Database.Dimstyle = standard.Name;
            document.Database.SetDimstyleData(standard);

            CadCommandSession session = new CadCommandSession(document);
            session.Remembered["filedia"] = 0.0;

            foreach ((double width, double y) in new[] { (100.0, 0.0), (200.0, 100.0), (300.0, 200.0) })
            {
                Assert.True(session.Feed("DIMLINEAR"));
                Assert.True(session.Feed("0," + y.ToString(CultureInfo.InvariantCulture)));
                Assert.True(
                    session.Feed(
                        width.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture)
                    )
                );
                Assert.True(session.Feed("50," + (y + 20).ToString(CultureInfo.InvariantCulture)));
            }

            Append(document, new DxfLine(new GeoPoint3(0, 300, 0), new GeoPoint3(10, 300, 0)));
            string path = Path.Combine(Folder, "source.dxf");
            document.SaveAs(path);
            return path;
        }

        /// <summary>What a drawing holds, one line for each object and layer: layer, measurement, text height and the picture.</summary>
        private static List<string> Described(CadDocument document)
        {
            var lines = new List<string>();

            foreach (
                DxfLayerTableRecord layer in document.Database.LayerTable.OrderBy(
                    l => l.Name,
                    StringComparer.OrdinalIgnoreCase
                )
            )
            {
                lines.Add("layer " + layer.Name + " " + layer.ColorIndex + " " + layer.LinetypeName);
            }

            foreach (
                DxfEntity entity in document
                    .Database.ModelSpace.Where(e => !e.IsErased)
                    .OrderBy(e => Convert.ToInt64(e.Handle, 16))
            )
            {
                if (entity is DxfDimension dimension)
                {
                    var picture = new List<string>();

                    foreach (DxfEntity part in dimension.DimBlock.Where(e => !e.IsErased))
                    {
                        picture.Add(
                            part is DxfMText words
                                ? "mtext "
                                    + words.Contents
                                    + " "
                                    + words.TextHeight.ToString("F6", CultureInfo.InvariantCulture)
                            : part is DxfLine stroke
                                ? "line "
                                    + stroke.StartPoint.X.ToString("F6", CultureInfo.InvariantCulture)
                                    + ","
                                    + stroke.StartPoint.Y.ToString("F6", CultureInfo.InvariantCulture)
                                    + " "
                                    + stroke.EndPoint.X.ToString("F6", CultureInfo.InvariantCulture)
                                    + ","
                                    + stroke.EndPoint.Y.ToString("F6", CultureInfo.InvariantCulture)
                            : part.GetType().Name
                        );
                    }

                    lines.Add(
                        entity.Handle
                            + " "
                            + entity.Layer
                            + " measurement "
                            + dimension.Measurement.ToString("F6", CultureInfo.InvariantCulture)
                            + " text "
                            + dimension.Dimtxt.ToString("F6", CultureInfo.InvariantCulture)
                            + " picture "
                            + string.Join(";", picture)
                    );
                }
                else
                {
                    lines.Add(entity.Handle + " " + entity.Layer + " " + entity.GetType().Name);
                }
            }

            return lines;
        }

        [Fact]
        public void DimlayInLispAndItsPortInPython_GiveTheSameDrawing_ObjectByObject()
        {
            // Three dimensions and a line. DIMLAY selects the three dimensions (the line is left out by the filter and by the
            // selection), moves them to layer DIM, made where it is missing, and sets the text height of each to 2.5.
            string source = DimensionsFile();
            string pythons = Path.Combine(Folder, "Pythons");
            Directory.CreateDirectory(pythons);
            WriteFile(pythons, "dimlay.py", Lines(DimlayPython));

            CadDocument lispDocument = CadDocument.Open(source);
            CadDocument pythonDocument = CadDocument.Open(source);
            string[] dimensions = lispDocument
                .Database.ModelSpace.OfType<DxfDimension>()
                .Select(d => d.Handle)
                .ToArray();
            Assert.Equal(3, dimensions.Length);
            Assert.All(dimensions, h => Assert.NotEqual(2.5, ((DxfDimension)pythonDocument.FindEntity(h)).Dimtxt, 6));
            List<string> before = Described(pythonDocument);
            Assert.Equal(Described(lispDocument), before);

            var lisp = new CadCommandSession(lispDocument);
            Assert.True(lisp.Feed(DimlayLisp));
            Assert.True(lisp.Feed("DIMLAY"));
            Assert.True(lisp.FeedSelection(dimensions));
            lisp.Enter();
            Assert.False(lisp.IsRunning);

            CadCommandSession python = new CadCommandSession(pythonDocument);
            python.Remembered["filedia"] = 0.0;
            Host.AddCommandsTo(python.Registry);
            LoadFromPythons(python, pythons);
            Type(python, "DIMLAY", Objects(dimensions), EnterKey);

            string lispFile = Path.Combine(Folder, "lisp.dxf");
            string pythonFile = Path.Combine(Folder, "python.dxf");
            lispDocument.SaveAs(lispFile);
            pythonDocument.SaveAs(pythonFile);
            List<string> fromLisp = Described(CadDocument.Open(lispFile));
            List<string> fromPython = Described(CadDocument.Open(pythonFile));

            Assert.Contains(fromLisp, line => line.StartsWith("layer DIM 7 Continuous", StringComparison.Ordinal));
            Assert.Equal(
                3,
                fromLisp.Count(line =>
                    line.IndexOf(" DIM measurement ", StringComparison.Ordinal) >= 0
                    && line.IndexOf(" text 2.500000 ", StringComparison.Ordinal) >= 0
                )
            );
            Assert.NotEqual(before, fromLisp);
            // The picture was drawn again with the words 2.5 high, in the drawing LISP changed and in the one Python changed.
            Assert.All(fromLisp.Where(line => line.IndexOf(" DIM measurement ", StringComparison.Ordinal) >= 0), line =>
                Assert.Matches(@"mtext \S+ 2\.500000", line)
            );
            Assert.Equal(fromLisp, fromPython);
        }

        #endregion
    }
}
