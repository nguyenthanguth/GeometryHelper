using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The previews prompts draw under the cursor, as AutoCAD 2021 drags the object about to be made: at each, the preview
    /// with the cursor at a point is, in geometry, what the command makes when that point is picked.
    /// </summary>
    public class PreviewTests
    {
        /// <summary>The items of a list as text, one line each: its kind and its numbers to a millionth.</summary>
        internal static List<string> Shapes(IEnumerable<DisplayItem2> items)
        {
            var lines = new List<string>();

            foreach (DisplayItem2 item in items)
            {
                var text = new StringBuilder(item.GetType().Name);

                switch (item)
                {
                    case DisplayPolyline2 chain:
                        text.Append(chain.Closed ? " closed" : " open");
                        Numbers(text, chain.Xy);
                        break;
                    case DisplaySegments2 segments:
                        Numbers(text, segments.Xy);
                        break;
                    case DisplayArc2 arc:
                        Numbers(text, new[] { arc.Cx, arc.Cy, arc.Radius, arc.Start, arc.Sweep });
                        break;
                    case DisplayRay2 ray:
                        text.Append(ray.IsBothWays ? " both" : " one");
                        Numbers(text, new[] { ray.X, ray.Y, ray.Dx, ray.Dy });
                        break;
                    case DisplayFill2 fill:
                        foreach (double[] ring in fill.Rings)
                        {
                            text.Append(" |");
                            Numbers(text, ring);
                        }

                        break;
                    case DisplayText2 words:
                        text.Append(' ').Append(words.Text);
                        Numbers(text, new[] { words.X, words.Y, words.Height, words.Rotation });
                        break;
                    default:
                        DisplayBox box = item.Bounds;
                        Numbers(text, new[] { box.MinX, box.MinY, box.MaxX, box.MaxY });
                        break;
                }

                lines.Add(text.ToString());
            }

            return lines;
        }

        private static void Numbers(StringBuilder text, IEnumerable<double> numbers)
        {
            foreach (double n in numbers)
            {
                double rounded = Math.Round(n, 6);
                text.Append(' ').Append((rounded == 0.0 ? 0.0 : rounded).ToString("0.000000", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>The handles of the entities of model space.</summary>
        private static HashSet<string> Handles(CadDocument document)
            => new HashSet<string>(All<DxfEntity>(document.ModelSpace).Select(e => e.Handle), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Checks that the prompt waiting previews, with the cursor at a point, what picking the point there (and the inputs
        /// after it) makes: the items of the preview, in geometry, the items model space draws for the entities added.
        /// </summary>
        internal static List<string> PreviewMakes(CadCommandSession session, GeoPoint2 cursor, params object[] after)
        {
            CadDocument document = session.Document;
            CadPreview preview = session.Prompt?.Preview;
            Assert.NotNull(preview);
            List<string> previewed = Shapes(preview.Items(cursor));
            Assert.NotEmpty(previewed);
            HashSet<string> before = Handles(document);
            session.Cursor = cursor;
            Run(session, cursor);
            Run(session, after);
            List<string> made = Shapes(document.GetDisplay(document.ModelSpace).Items.Where(i => i.Handle != null && !before.Contains(i.Handle)));
            Assert.Equal(made, previewed);
            return previewed;
        }

        private static CadCommandSession Started(params object[] inputs)
        {
            CadDocument document = Blank();
            CadLayerEditor.Add(document, "Steel");
            CadLayerEditor.SetCurrent(document, "Steel");
            return Run(Session(document), inputs);
        }

        [Fact]
        public void RectangAskingItsOtherCorner_PreviewsTheRectangle_NotALine()
        {
            CadCommandSession session = Started("RECTANG", P(0, 0));
            Assert.Equal("Specify other corner point:", session.PromptText);
            Assert.False(session.Prompt.Preview.ShowsBand);

            List<string> shapes = PreviewMakes(session, P(30, 20));

            Assert.Equal("DisplayPolyline2 closed 0.000000 0.000000 30.000000 0.000000 30.000000 20.000000 0.000000 20.000000", Assert.Single(shapes));
        }

        [Fact]
        public void RectangPreview_IsDrawnInTheCurrentLayersColour()
        {
            CadCommandSession session = Started("RECTANG", P(0, 0));
            DxfLayerTableRecord steel = session.Document.Database.LayerTable["Steel"];
            int made = session.Document.GetDisplay(session.Document.ModelSpace).Items.Count;

            DisplayItem2 item = Assert.Single(session.Prompt.Preview.Items(P(30, 20)));
            Run(session, P(30, 20));

            Assert.Equal(made + 1, session.Document.GetDisplay(session.Document.ModelSpace).Items.Count);
            Assert.Equal(session.Document.GetDisplay(session.Document.ModelSpace).Items.Last().Style.Rgb, item.Style.Rgb);
            Assert.Equal("Steel", item.Layer);
            Assert.NotNull(steel);
        }

        [Fact]
        public void RectangWithTheCornersInALine_PreviewsNothing()
        {
            CadCommandSession session = Started("RECTANG", P(0, 0));

            Assert.Empty(session.Prompt.Preview.Items(P(30, 0)));
        }

        [Fact]
        public void CircleByCentre_PreviewsTheCircleOfTheRadiusAndTheDiameter_WithTheRubberBand()
        {
            CadCommandSession session = Started("CIRCLE", P(10, 10));
            Assert.True(session.Prompt.Preview.ShowsBand);
            PreviewMakes(session, P(13, 14));

            Run(session, "CIRCLE", P(10, 10), "D");
            Assert.Equal("Specify diameter of circle <10.0000>:", session.PromptText);
            Assert.Contains("5.000000", PreviewMakes(session, P(20, 10)).Single());
        }

        [Fact]
        public void CircleByTwoAndThreePoints_PreviewsTheCircleThroughTheCursor()
        {
            CadCommandSession session = Started("CIRCLE", "2P", P(0, 0));
            PreviewMakes(session, P(10, 0));

            Run(session, "CIRCLE", "3P", P(0, 0), P(10, 0));
            Assert.Equal("Specify third point on circle:", session.PromptText);
            PreviewMakes(session, P(5, 5));
        }

        [Fact]
        public void CircleTtrAndTanTanTan_PreviewTheCircleTouchingTheObjects()
        {
            CadCommandSession session = Started("LINE", P(0, 0), P(100, 0), Enter, "LINE", P(0, 0), P(0, 100), Enter, "LINE", P(100, 0), P(0, 100), Enter);
            Run(session, "CIRCLE", "T", P(50, 0), P(0, 50));
            Assert.StartsWith("Specify radius of circle", session.PromptText);
            PreviewMakes(session, P(0, 60));

            Run(session, "CIRCLE", "3P", "_tan", P(50, 0), "_tan", P(0, 50), "_tan");
            Assert.Equal("to:", session.PromptText);
            PreviewMakes(session, P(50, 50));
        }

        [Fact]
        public void ArcThroughThreePoints_PreviewsTheArcThroughTheCursor()
        {
            CadCommandSession session = Started("ARC", P(0, 0), P(10, 10));
            Assert.Equal("Specify end point of arc:", session.PromptText);
            Assert.StartsWith("DisplayArc2", PreviewMakes(session, P(20, 0)).Single());
        }

        [Fact]
        public void ArcByStartCentre_PreviewsTheArcToTheEnd_TheAngle_AndTheChord()
        {
            CadCommandSession session = Started("ARC", P(10, 0), "C", P(0, 0));
            PreviewMakes(session, P(0, 10));

            Run(session, "ARC", P(10, 0), "C", P(0, 0), "A");
            PreviewMakes(session, P(-5, 5));

            Run(session, "ARC", P(10, 0), "C", P(0, 0), "L");
            PreviewMakes(session, P(10, 12));

            Run(session, "ARC", "C", P(0, 0), P(10, 0));
            PreviewMakes(session, P(-3, -3));
        }

        [Fact]
        public void ArcByStartEnd_PreviewsTheArcOfTheCentre_TheAngle_TheDirection_AndTheRadius()
        {
            CadCommandSession session = Started("ARC", P(0, 0), "E", P(10, 0));
            Assert.StartsWith("Specify center point of arc", session.PromptText);
            PreviewMakes(session, P(5, -5));

            foreach (string option in new[] { "A", "D", "R" })
            {
                Run(session, "ARC", P(0, 0), "E", P(10, 0), option);
                PreviewMakes(session, P(3, 8));
            }
        }

        [Fact]
        public void ArcContinuedWithEnter_PreviewsTheArcLeavingTheLastLine()
        {
            CadCommandSession session = Started("LINE", P(0, 0), P(10, 0), Enter, "ARC", Enter);
            Assert.StartsWith("Specify end point of arc", session.PromptText);
            PreviewMakes(session, P(20, 10));
        }

        [Fact]
        public void Ellipse_PreviewsTheEllipseOfTheOtherAxis_AndItsRotation()
        {
            CadCommandSession session = Started("ELLIPSE", P(0, 0), P(20, 0));
            Assert.StartsWith("Specify distance to other axis", session.PromptText);
            PreviewMakes(session, P(10, 4));

            Run(session, "ELLIPSE", "C", P(0, 0), P(20, 0), "R");
            PreviewMakes(session, P(10, 10));
        }

        [Fact]
        public void EllipticalArc_PreviewsTheWholeEllipseAtTheStart_AndTheArcAtTheEndAndTheIncludedAngle()
        {
            CadCommandSession session = Started("ELLIPSE", "A", P(0, 0), P(20, 0), "4");
            Assert.StartsWith("Specify start angle", session.PromptText);
            Assert.NotEmpty(session.Prompt.Preview.Items(P(5, 5)));
            Run(session, "30");
            PreviewMakes(session, P(-10, 2));

            Run(session, "ELLIPSE", "A", P(0, 0), P(20, 0), "4", "30", "I");
            PreviewMakes(session, P(-10, -2));
        }

        [Fact]
        public void Polygon_PreviewsThePolygonOfTheRadius_InscribedCircumscribedAndOnAnEdge()
        {
            CadCommandSession session = Started("POLYGON", "6", P(0, 0), "I");
            PreviewMakes(session, P(10, 3));

            Run(session, "POLYGON", "5", P(0, 0), "C");
            PreviewMakes(session, P(10, 3));

            Run(session, "POLYGON", "3", "E", P(0, 0));
            PreviewMakes(session, P(10, 0));
        }

        [Fact]
        public void Donut_PreviewsTheDonutAtTheCursor()
        {
            CadCommandSession session = Started("DONUT", "10", "20");
            Assert.StartsWith("Specify center of donut", session.PromptText);
            PreviewMakes(session, P(50, 50), Enter);
        }

        [Fact]
        public void XlineAndRay_PreviewTheEndlessLineThroughTheCursor()
        {
            CadCommandSession session = Started("XLINE", P(0, 0));
            Assert.StartsWith("DisplayRay2 both", PreviewMakes(session, P(10, 5), Enter).Single());

            Run(session, "XLINE", "H");
            PreviewMakes(session, P(3, 7), Enter);

            Run(session, "XLINE", "B", P(0, 0), P(10, 0));
            PreviewMakes(session, P(0, 10), Enter);

            Run(session, "RAY", P(0, 0));
            Assert.StartsWith("DisplayRay2 one", PreviewMakes(session, P(10, 5), Enter).Single());
        }

        [Fact]
        public void Pline_PreviewsTheNextLine_TheArcLeavingTheLast_AndTheirWidths()
        {
            CadCommandSession session = Started("PLINE", P(0, 0), "W", "2", "4");
            PreviewMakes(session, P(100, 0), Esc);

            Run(session, "PLINE", P(0, 0), "W", "0", "0", "A");
            Assert.StartsWith("DisplayArc2", PreviewMakes(session, P(100, 50), Esc).Single());
        }

        [Fact]
        public void PlineArcOptions_PreviewTheArcSegment()
        {
            CadCommandSession session = Started();

            foreach (object[] steps in new[]
            {
                new object[] { "A", "A", "90" },
                new object[] { "A", "A", "90", "CE" },
                new object[] { "A", "CE", P(0, 50) },
                new object[] { "A", "CE", P(0, 50), "A" },
                new object[] { "A", "CE", P(0, 50), "L" },
                new object[] { "A", "D", "90" },
                new object[] { "A", "R", "60" },
                new object[] { "A", "R", "60", "A", "90" },
                new object[] { "A", "S", P(30, 30) },
            })
            {
                Run(session, "PLINE", P(0, 0));
                Run(session, steps);
                PreviewMakes(session, P(40, 20), Esc);
            }
        }

        /// <summary>The ends of the lines of model space, each as text to a millionth, in the order of the handles given.</summary>
        private static List<string> Ends(CadDocument document, IEnumerable<string> handles, Affine2 m)
        {
            var ends = new List<string>();

            foreach (string handle in handles)
            {
                var line = (DxfLine)document.FindEntity(handle);
                var text = new StringBuilder();
                Numbers(text, new[] { m.X(line.StartPoint.X, line.StartPoint.Y), m.Y(line.StartPoint.X, line.StartPoint.Y), m.X(line.EndPoint.X, line.EndPoint.Y), m.Y(line.EndPoint.X, line.EndPoint.Y) });
                ends.Add(text.ToString());
            }

            return ends;
        }

        /// <summary>
        /// Checks that the prompt waiting moves the copies of the one line selected, with the cursor at a point, where picking
        /// the point there (and the inputs after it) leaves the line.
        /// </summary>
        private static void MovesAsMade(CadCommandSession session, string line, GeoPoint2 cursor, params object[] after)
        {
            CadPreview preview = session.Prompt?.Preview;
            Assert.NotNull(preview);
            Assert.Equal(new[] { line }, preview.Handles);
            List<string> previewed = Ends(session.Document, new[] { line }, Assert.Single(preview.Moves(cursor)));
            Run(session, cursor);
            Run(session, after);
            Assert.Equal(previewed, Ends(session.Document, new[] { line }, Affine2.Identity));
        }

        private static CadCommandSession WithALine(out string line)
        {
            CadCommandSession session = Started("LINE", P(10, 0), P(30, 10), Enter);
            line = All<DxfLine>(session.Document.ModelSpace).Single().Handle;
            return session;
        }

        [Fact]
        public void RotateScaleAndMirror_MoveTheGhostAsTheyMoveTheObjects_ByAReferenceToo()
        {
            CadCommandSession session = WithALine(out string line);
            Run(session, "ROTATE", new[] { line }, Enter, P(0, 0));
            MovesAsMade(session, line, P(-3, 5));

            Run(session, "ROTATE", new[] { line }, Enter, P(0, 0), "R", "30");
            Assert.StartsWith("Specify the new angle", session.PromptText);
            MovesAsMade(session, line, P(5, 5));

            Run(session, "SCALE", new[] { line }, Enter, P(0, 0));
            MovesAsMade(session, line, P(2, 0));

            Run(session, "SCALE", new[] { line }, Enter, P(0, 0), "R", "4");
            Assert.StartsWith("Specify new length", session.PromptText);
            MovesAsMade(session, line, P(3, 0));

            Run(session, "MIRROR", new[] { line }, Enter, P(0, 0));
            MovesAsMade(session, line, P(10, 10), "Y");
        }

        [Fact]
        public void RotateInATurnedUcs_TurnsTheGhostByTheAngleItTurnsTheObjects()
        {
            CadCommandSession session = WithALine(out string line);
            Run(session, "UCS", "Z", "30");
            Run(session, "ROTATE", new[] { line }, Enter, P(0, 0));
            MovesAsMade(session, line, P(0, 10));
        }

        [Fact]
        public void RectangularArray_PreviewsEveryCopyWhileItsOptionsAreOffered()
        {
            CadCommandSession session = WithALine(out string line);
            Run(session, "ARRAYRECT", new[] { line }, Enter);
            Assert.StartsWith("Select grip to edit array", session.PromptText);
            IReadOnlyList<Affine2> moves = session.Prompt.Preview.Moves(P(0, 0));
            Assert.Equal(11, moves.Count);
            HashSet<string> before = Handles(session.Document);
            List<string> previewed = moves.SelectMany(m => Ends(session.Document, new[] { line }, m)).OrderBy(s => s, StringComparer.Ordinal).ToList();

            Run(session, Enter);

            List<string> made = Ends(session.Document, Handles(session.Document).Where(h => !before.Contains(h)), Affine2.Identity).OrderBy(s => s, StringComparer.Ordinal).ToList();
            Assert.Equal(made, previewed);
        }

        [Fact]
        public void DimensionsUnderADimscaleOfTen_PreviewThePictureTheyMake()
        {
            CadCommandSession session = Started("DIMSCALE", "10", "CIRCLE", P(0, 0), "200");
            string circle = All<DxfCircle>(session.Document.ModelSpace).Single().Handle;

            Run(session, "DIMLINEAR", P(0, 0), P(300, 0));
            PreviewMakes(session, P(150, -60));

            Run(session, "DIMRADIUS", new[] { circle });
            PreviewMakes(session, P(300, 300));

            Run(session, "DIMANGULAR", Enter, P(0, 0), P(500, 0), P(0, 500));
            PreviewMakes(session, P(200, 200));

            Run(session, "DIMORDINATE", P(400, 200));
            PreviewMakes(session, P(400, 600));

            Run(session, "DIMLINEAR", P(0, 0), P(100, 0), P(50, 200), "DIMBASELINE");
            PreviewMakes(session, P(400, 0), Esc);
        }

        [Fact]
        public void PolarArray_PreviewsEveryCopy()
        {
            CadCommandSession session = WithALine(out string line);
            Run(session, "ARRAYPOLAR", new[] { line }, Enter, P(0, 0));
            IReadOnlyList<Affine2> moves = session.Prompt.Preview.Moves(P(0, 0));
            Assert.Equal(5, moves.Count);
            HashSet<string> before = Handles(session.Document);
            List<string> previewed = moves.SelectMany(m => Ends(session.Document, new[] { line }, m)).OrderBy(s => s, StringComparer.Ordinal).ToList();

            Run(session, Enter);

            List<string> made = Ends(session.Document, Handles(session.Document).Where(h => !before.Contains(h)), Affine2.Identity).OrderBy(s => s, StringComparer.Ordinal).ToList();
            Assert.Equal(made, previewed);
        }

        [Fact]
        public void Offset_PreviewsTheCopyOnTheCursorsSide_AtTheDistanceAndThrough()
        {
            CadCommandSession session = WithALine(out string line);
            Run(session, "OFFSET", "5", new[] { line });
            Assert.StartsWith("Specify point on side to offset", session.PromptText);
            PreviewMakes(session, P(10, 20));

            Run(session, Esc, "OFFSET", "T", new[] { line });
            PreviewMakes(session, P(30, -20), Esc);
        }

        [Fact]
        public void Stretch_PreviewsTheObjectsStretched()
        {
            CadCommandSession session = Started("PLINE", P(0, 0), P(100, 0), P(100, 50), Enter, "LINE", P(200, 0), P(220, 0), Enter);
            Run(session, "STRETCH", "C", P(90, -10), P(230, 60), Enter, P(100, 0));
            CadPreview preview = session.Prompt.Preview;
            List<string> previewed = Shapes(preview.Items(P(130, 10)));
            Assert.Equal(2, previewed.Count);

            Run(session, P(130, 10));

            Assert.Equal(Shapes(session.Document.GetDisplay(session.Document.ModelSpace).Items), previewed);
        }

        [Fact]
        public void LinearAndAlignedDimensions_PreviewTheWholeDimensionAtTheCursor()
        {
            CadCommandSession session = Started("DIMLINEAR", P(0, 0), P(100, 30));
            Assert.StartsWith("Specify dimension line location", session.PromptText);
            Assert.Contains(PreviewMakes(session, P(50, 60)), s => s.StartsWith("DisplayText2 100", StringComparison.Ordinal));

            Run(session, "DIMLINEAR", P(0, 0), P(100, 30), "V");
            PreviewMakes(session, P(150, 10));

            Run(session, "DIMALIGNED", P(0, 0), P(100, 30));
            PreviewMakes(session, P(40, 60));
        }

        [Fact]
        public void RadiusDiameterAndAngularDimensions_PreviewTheWholeDimension()
        {
            CadCommandSession session = Started("CIRCLE", P(0, 0), "20");
            string circle = All<DxfCircle>(session.Document.ModelSpace).Single().Handle;

            Run(session, "DIMRADIUS", new[] { circle });
            PreviewMakes(session, P(30, 30));

            Run(session, "DIMDIAMETER", new[] { circle });
            PreviewMakes(session, P(-30, 10));

            Run(session, "DIMANGULAR", Enter, P(0, 0), P(50, 0), P(0, 50));
            Assert.StartsWith("Specify dimension arc line location", session.PromptText);
            PreviewMakes(session, P(20, 20));
        }

        [Fact]
        public void OrdinateContinuedAndBaselineDimensions_PreviewTheNextDimension()
        {
            CadCommandSession session = Started("DIMORDINATE", P(40, 20));
            PreviewMakes(session, P(40, 60));

            Run(session, "DIMLINEAR", P(0, 0), P(100, 0), P(50, 20), "DIMCONTINUE");
            Assert.StartsWith("Specify a second extension line origin", session.PromptText);
            PreviewMakes(session, P(160, 0), Esc);

            Run(session, "DIMBASELINE");
            PreviewMakes(session, P(220, 0), Esc);
        }

        [Fact]
        public void MText_PreviewsTheFrameOfTheText()
        {
            CadCommandSession session = Started("MTEXT", P(10, 10));
            Assert.StartsWith("Specify opposite corner", session.PromptText);

            Assert.Equal("DisplayPolyline2 closed 10.000000 10.000000 60.000000 10.000000 60.000000 40.000000 10.000000 40.000000", Assert.Single(Shapes(session.Prompt.Preview.Items(P(60, 40)))));
        }

        [Fact]
        public void Revcloud_PreviewsTheCloudOfTheRectangleAndOfThePolygonSoFar()
        {
            CadCommandSession session = Started("REVCLOUD", "R", P(0, 0));
            Assert.StartsWith("Specify opposite corner", session.PromptText);
            PreviewMakes(session, P(100, 60));

            Run(session, "REVCLOUD", "P", P(0, 0), P(100, 0));
            PreviewMakes(session, P(50, 80), Enter);
        }
    }
}
