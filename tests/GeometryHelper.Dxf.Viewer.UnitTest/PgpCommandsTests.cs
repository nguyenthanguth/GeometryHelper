using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The commands and aliases of AutoCAD 2021's acad.pgp the presenter lacked: the aliases of commands it had, and DIVIDE,
    /// MEASURE, DIMCENTER, COLOR, LWEIGHT, REDRAW, REDRAWALL, REGENALL, ISOLATEOBJECTS, HIDEOBJECTS and UNISOLATEOBJECTS,
    /// each checked against what AutoCAD 2021's console made of the same objects.
    /// </summary>
    public class PgpCommandsTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static string Last(CadDocument document) => document.ModelSpace.Block.Last().Handle;

        private static List<GeoPoint2> Points(CadDocument document) =>
            All<DxfDBPoint>(document.ModelSpace).Select(p => new GeoPoint2(p.Position.X, p.Position.Y)).ToList();

        private static void Near(IReadOnlyList<GeoPoint2> expected, IReadOnlyList<GeoPoint2> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
            {
                EditingDrawings.Near(expected[i], actual[i], 1E-6);
            }
        }

        private static string Said(CadCommandSession session) => string.Join("\n", session.History);

        [Fact]
        public void TheStockAliasesOfAcadPgp_ReachTheirCommandsAndVariables()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            foreach (
                string[] pair in new[]
                {
                    new[] { "DELETE", "ERASE" },
                    new[] { "MO", "PROPERTIES" },
                    new[] { "PROPS", "PROPERTIES" },
                    new[] { "DDMODIFY", "PROPERTIES" },
                    new[] { "PRINT", "PLOT" },
                    new[] { "PRE", "PREVIEW" },
                    new[] { "PA", "PASTESPEC" },
                    new[] { "EXP", "EXPORT" },
                    new[] { "LMAN", "LAYERSTATE" },
                    new[] { "-LTYPE", "-LINETYPE" },
                    new[] { "ATTE", "-ATTEDIT" },
                    new[] { "DDA", "DIMDISASSOCIATE" },
                    new[] { "DRE", "DIMREASSOCIATE" },
                    new[] { "SET", "SETVAR" },
                    new[] { "TEDIT", "TEXTEDIT" },
                    new[] { "DIMLIN", "DIMLINEAR" },
                    new[] { "DIMALI", "DIMALIGNED" },
                    new[] { "DIMSTY", "DIMSTYLE" },
                    new[] { "DTEXT", "TEXT" },
                    new[] { "DOUGHNUT", "DONUT" },
                    new[] { "RECTANGLE", "RECTANG" },
                    new[] { "PAINTER", "MATCHPROP" },
                    new[] { "PREFERENCES", "OPTIONS" },
                    new[] { "DXFOUT", "SAVEAS" },
                    new[] { "DDUCS", "UCSMAN" },
                    new[] { "COL", "COLOR" },
                    new[] { "COLOUR", "COLOR" },
                    new[] { "DDCOLOR", "COLOR" },
                    new[] { "LW", "LWEIGHT" },
                    new[] { "LINEWEIGHT", "LWEIGHT" },
                    new[] { "DIV", "DIVIDE" },
                    new[] { "ME", "MEASURE" },
                    new[] { "DCE", "DIMCENTER" },
                    new[] { "R", "REDRAW" },
                    new[] { "RA", "REDRAWALL" },
                    new[] { "REA", "REGENALL" },
                    new[] { "ISOLATE", "ISOLATEOBJECTS" },
                    new[] { "UNISOLATE", "UNISOLATEOBJECTS" },
                    new[] { "UNHIDE", "UNISOLATEOBJECTS" },
                }
            )
            {
                Assert.Equal(pair[1], CadShortcuts.Default.Aliases[pair[0]]);

                if (pair[1] != "PREVIEW" && pair[1] != "EXPORT" && pair[1] != "SAVEAS")
                {
                    Assert.Equal(pair[1], registry.Find(pair[0])?.Name);
                }
            }

            Assert.Equal("TILEMODE", registry.FindVariable("TI")?.Name);
            Assert.Equal("TILEMODE", registry.FindVariable("TM")?.Name);
        }

        [Fact]
        public void DivideALineIntoFive_PutsFourPointsAsAutoCadDoes_AsOneUndoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LINE", P(0, 0), P(100, 0), Enter, "DIV");

            Assert.Equal("Select object to divide:", session.PromptText);
            Run(session, P(50, 0));
            Assert.Equal("Enter the number of segments or [Block]:", session.PromptText);
            Run(session, "1");
            Assert.Contains("Requires an integer between 2 and 32767, or option keyword.", session.History);
            Assert.Equal("Enter the number of segments or [Block]:", session.PromptText);
            Run(session, "5");

            Near(new[] { P(20, 0), P(40, 0), P(60, 0), P(80, 0) }, Points(document));
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Empty(Points(document));
            Assert.Single(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void DivideClosedShapes_StartsAtTheirStartAndPutsAPointThere()
        {
            CadDocument document = Blank();

            // AutoCAD 2021: a circle from angle nought, an ellipse from its major axis, a polyline from its first vertex.
            Start(document, "CIRCLE", P(400, 0), "50", "DIVIDE", P(450, 0), "4");
            Near(new[] { P(450, 0), P(400, 50), P(350, 0), P(400, -50) }, Points(document));

            document = Blank();
            Start(document, "ELLIPSE", "C", P(600, 0), P(680, 0), "40", "DIVIDE", P(680, 0), "4");
            Near(new[] { P(680, 0), P(600, 40), P(520, 0), P(600, -40) }, Points(document));

            document = Blank();
            Start(document, "PLINE", P(0, 200), P(100, 200), "A", P(100, 300), "L", P(0, 300), "C");
            Start(document, "DIVIDE", P(50, 200), "4");
            Near(
                new[] { P(0, 200), P(114.076977, 202.022519), P(127.015115, 292.073549), P(14.269908, 300) },
                Points(document)
            );

            document = Blank();
            Start(document, "ARC", "C", P(200, 0), P(250, 0), P(200, 50), "DIVIDE", P(250, 0), "3");
            Near(new[] { P(243.30127, 25), P(225, 43.30127) }, Points(document));
        }

        [Fact]
        public void DivideWithABlock_InsertsItAlongTheCurveOrLevel_AndRefusesABlockNotThere()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(0, 5), Enter);
            Start(document, "-BLOCK", "B1", P(0, 0), new[] { Last(document) }, Enter);
            Start(document, "ARC", "C", P(200, 0), P(250, 0), P(200, 50));

            CadCommandSession session = Start(document, "DIVIDE", P(250, 0), "B");
            Assert.Equal("Enter name of block to insert:", session.PromptText);
            Run(session, "NOSUCH");
            Assert.Contains("Cannot find block \"NOSUCH\".", session.History);
            Assert.Equal("Enter name of block to insert:", session.PromptText);
            Run(session, "B1");
            Assert.Equal("Align block with object? [Yes/No] <Y>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter the number of segments:", session.PromptText);
            Run(session, "3");

            List<DxfBlockReference> aligned = All<DxfBlockReference>(document.ModelSpace);
            Assert.Equal(2, aligned.Count);
            EditingDrawings.Near(new GeoPoint3(243.30127, 25, 0), aligned[0].Position, 1E-5);
            Assert.Equal(120.0, aligned[0].Rotation * 180.0 / Math.PI, 6);
            Assert.Equal(150.0, aligned[1].Rotation * 180.0 / Math.PI, 6);
            Assert.All(aligned, i => Assert.Equal("B1", i.Name));

            Start(document, "DIVIDE", P(250, 0), "B", "B1", "N", "3");
            List<DxfBlockReference> level = All<DxfBlockReference>(document.ModelSpace).Skip(2).ToList();
            Assert.Equal(2, level.Count);
            Assert.All(level, i => Assert.Equal(0.0, i.Rotation, 9));
        }

        [Fact]
        public void MeasureALine_StepsFromTheEndNearestThePick_AndSaysWhenItIsTooShort()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LINE", P(0, 0), P(100, 0), Enter, "ME");

            Assert.Equal("Select object to measure:", session.PromptText);
            Run(session, P(1, 0));
            Assert.Equal("Specify length of segment or [Block]:", session.PromptText);
            Run(session, "30");
            Near(new[] { P(30, 0), P(60, 0), P(90, 0) }, Points(document));

            document = Blank();
            Start(document, "LINE", P(0, 0), P(100, 0), Enter, "MEASURE", P(99, 0), "30");
            Near(new[] { P(70, 0), P(40, 0), P(10, 0) }, Points(document));

            session = Start(document, "MEASURE", P(50, 0), "200");
            Assert.Contains("Object isn't that long.", session.History);
            Assert.Equal(3, Points(document).Count);
        }

        [Fact]
        public void MeasureArcsCirclesAndClosedPolylines_AsAutoCad2021()
        {
            // The arc picked near its end at 90 degrees is measured from there.
            CadDocument document = Blank();
            Start(document, "ARC", "C", P(200, 0), P(250, 0), P(200, 50));
            double near = 89.0 * Math.PI / 180.0;
            Start(document, "MEASURE", P(200 + 50 * Math.Cos(near), 50 * Math.Sin(near)), "20");
            Near(
                new[] { P(219.470917, 46.05305), P(235.867805, 34.835335), P(246.601954, 18.117888) },
                Points(document)
            );

            document = Blank();
            Start(document, "CIRCLE", P(400, 0), "50", "MEASURE", P(350, 0), "100");
            Near(
                new[] { P(379.192658, 45.464871), P(367.317819, -37.840125), P(448.008514, -13.970775) },
                Points(document)
            );

            document = Blank();
            Start(document, "PLINE", P(0, 200), P(100, 200), "A", P(100, 300), "L", P(0, 300), "C");
            Start(document, "MEASURE", P(0, 250), "70");
            Near(
                new[]
                {
                    P(70, 200),
                    P(135.867805, 215.164665),
                    P(140.42482, 279.425056),
                    P(77.079633, 300),
                    P(7.079633, 300),
                    P(0, 237.079633),
                },
                Points(document)
            );
        }

        [Fact]
        public void MeasureACircleWithAnAlignedBlock_TurnsEachAlongTheCircle()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(0, 5), Enter);
            Start(document, "-BLOCK", "B1", P(0, 0), new[] { Last(document) }, Enter);
            Start(document, "CIRCLE", P(400, 0), "50");

            CadCommandSession session = Start(document, "MEASURE", P(450, 0), "B", "B1", "Y");
            Assert.Equal("Specify length of segment:", session.PromptText);
            Run(session, "100");

            List<DxfBlockReference> inserts = All<DxfBlockReference>(document.ModelSpace);
            Assert.Equal(
                new[] { 204.591559, 319.183118, 73.774677 },
                inserts.Select(i => Math.Round(i.Rotation * 180.0 / Math.PI, 6))
            );
        }

        [Fact]
        public void DimCenter_DrawsTheMarkOfDimcen_AndCentreLinesBelowNoughtWhereTheRadiusTakesThem()
        {
            CadDocument document = Blank();
            document.Database.Dimcen = 2.5;
            CadCommandSession session = Start(document, "CIRCLE", P(0, 0), "50", "DCE");

            Assert.Equal("Select arc or circle:", session.PromptText);
            Run(session, P(50, 0));
            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(2, lines.Count);
            HasLine(lines, P(-2.5, 0), P(2.5, 0));
            HasLine(lines, P(0, -2.5), P(0, 2.5));
            Assert.True(document.Undo());
            Assert.Empty(All<DxfLine>(document.ModelSpace));

            // AutoCAD 2021: centre lines from twice the mark out to the mark past the rim, only where the radius is over twice it.
            document = Blank();
            document.Database.Dimcen = -2.5;
            Start(document, "CIRCLE", P(0, 0), "50", "DIMCENTER", P(50, 0));
            lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(6, lines.Count);
            HasLine(lines, P(5, 0), P(52.5, 0));
            HasLine(lines, P(-5, 0), P(-52.5, 0));
            HasLine(lines, P(0, 5), P(0, 52.5));
            HasLine(lines, P(0, -5), P(0, -52.5));

            document = Blank();
            document.Database.Dimcen = -2.5;
            Start(document, "CIRCLE", P(0, 0), "5", "DIMCENTER", P(5, 0));
            Assert.Equal(2, All<DxfLine>(document.ModelSpace).Count);

            document = Blank();
            document.Database.Dimcen = 0.0;
            session = Start(document, "CIRCLE", P(0, 0), "50", "DIMCENTER");
            Assert.Contains("DIMCEN = 0.0, not drawing center cross. *Invalid*", session.History);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ARadiusWithCentreLines_DrawsNoneWhereTheRadiusIsNotOverTwiceTheMark()
        {
            // AutoCAD 2021's radius under DIMCEN -2.5 has centre lines on a circle of 5.01, the cross alone on one of 5 or 4.
            Assert.Contains(RadiusLines(5.01), l => Close(l.StartPoint, P(5, 0)) || Close(l.EndPoint, P(5, 0)));
            List<DxfLine> lines = RadiusLines(4.0);
            Assert.DoesNotContain(lines, l => Close(l.StartPoint, P(5, 0)) || Close(l.EndPoint, P(5, 0)));
            Assert.DoesNotContain(lines, l => Close(l.StartPoint, P(0, 5)) || Close(l.EndPoint, P(0, 5)));
            HasLine(lines, P(-2.5, 0), P(2.5, 0));
        }

        private static List<DxfLine> RadiusLines(double radius)
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimscale = 1.0;
            style.Dimcen = -2.5;
            style.Dimtofl = false;
            StandardCurrent(document.Database);
            Start(document, "CIRCLE", P(0, 0), radius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Start(document, "DIMRADIUS", new[] { Last(document) }, P(20, 0));

            DxfDimension dimension = All<DxfDimension>(document.ModelSpace).Single();
            return dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased).ToList();
        }

        private static void HasLine(IEnumerable<DxfLine> lines, GeoPoint2 a, GeoPoint2 b) =>
            Assert.Contains(lines, l =>
                (Close(l.StartPoint, a) && Close(l.EndPoint, b)) || (Close(l.StartPoint, b) && Close(l.EndPoint, a))
            );

        private static bool Close(GeoPoint3 p, GeoPoint2 q) => Math.Abs(p.X - q.X) < 1E-9 && Math.Abs(p.Y - q.Y) < 1E-9;

        [Fact]
        public void DashColor_AsksAsAutoCad_AndSetsTheColourOfNewObjects_AsColorDoesWithNoWindow()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-COLOR");

            Assert.Equal("Enter default object color [Truecolor/COlorbook] <BYLAYER>:", session.PromptText);
            Run(session, "1");
            Assert.Equal(1, document.Database.Cecolor.ColorIndex);

            session = Start(document, "COL");
            Assert.Equal("Enter default object color [Truecolor/COlorbook] <1 (red)>:", session.PromptText);
            Run(session, "30");
            Assert.Equal(30, document.Database.Cecolor.ColorIndex);

            session = Start(document, "-COLOR");
            Assert.Equal("Enter default object color [Truecolor/COlorbook] <30>:", session.PromptText);
            Run(session, "bylayer");
            Assert.True(document.Database.Cecolor.IsByLayer);
        }

        [Fact]
        public void DashLweight_RoundsToTheNearestLineweight_AndTakesTheWordsAsAutoCad()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-LWEIGHT");

            Assert.Contains("Current lineweight: BYLAYER", session.History);
            Assert.Equal("Enter default lineweight for new objects or [?]:", session.PromptText);
            Run(session, "0.33");
            Assert.Contains("Lineweight rounded to nearest valid value of 0.35 mm", session.History);
            Assert.Equal(DxfLineWeight.LineWeight035, document.Database.Celweight);

            session = Start(document, "LW");
            Assert.Contains("Current lineweight: 0.35 mm", session.History);
            Run(session, "-1");
            Assert.Contains("Invalid lweight number.", session.History);
            Assert.Equal("Enter default lineweight for new objects or [?]:", session.PromptText);
            Run(session, "L");
            Assert.Contains("A weight in lineweight units is required.", session.History);
            Run(session, "?");
            Assert.Contains("1.06 mm 1.20 mm  1.40 mm    1.58 mm 2.00 mm  2.11 mm", session.History);
            Run(session, "3");
            Assert.Contains("Lineweight rounded to nearest valid value of 2.11 mm", session.History);
            Assert.Equal(DxfLineWeight.LineWeight211, document.Database.Celweight);

            Start(document, "-LWEIGHT", "byblock");
            Assert.Equal(DxfLineWeight.ByBlock, document.Database.Celweight);
            Start(document, "-LWEIGHT", "Default");
            Assert.Equal(DxfLineWeight.ByLineWeightDefault, document.Database.Celweight);
            session = Start(document, "-LWEIGHT", "0", Enter);
            Assert.Equal(DxfLineWeight.LineWeight000, document.Database.Celweight);
            session = Start(document, "-LWEIGHT");
            Assert.Contains("Current lineweight: None", session.History);
            Run(session, Enter);

            Assert.True(document.Undo());
            Assert.Equal(DxfLineWeight.ByLineWeightDefault, document.Database.Celweight);
        }

        [Fact]
        public void RedrawRedrawallAndRegenall_RunAsAutoCad()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "R", "RA");

            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History, l => l.StartsWith("Unknown command", StringComparison.Ordinal));
            Start(document, "REA");
            Assert.Contains("Regenerating model.", Start(document, "REGENALL").History);
        }

        [Fact]
        public void IsolateHideAndUnisolate_HideOthersOrThoseSelected_AndShowThemAgainAsAutoCad()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 100), P(100, 100), Enter);
            string first = Last(document);
            Start(document, "LINE", P(0, 120), P(100, 120), Enter);
            string second = Last(document);

            Start(document, "ISOLATEOBJECTS", new[] { first }, Enter);
            Assert.True(Entity<DxfLine>(document, first).Visible);
            Assert.False(Entity<DxfLine>(document, second).Visible);

            CadCommandSession session = Start(document, "UNISOLATE");
            Assert.Contains("1 object(s) unisolated.", session.History);
            Assert.True(Entity<DxfLine>(document, second).Visible);
            session = Start(document, "UNISOLATEOBJECTS");
            Assert.Contains("No objects to unisolate.", session.History);

            Start(document, "HIDEOBJECTS", new[] { first }, Enter);
            Assert.False(Entity<DxfLine>(document, first).Visible);

            // Hidden for the session only, as AutoCAD's OBJECTISOLATIONMODE 0: the file keeps it shown.
            string path = Path.Combine(TestRun.Folder("pgp"), "hidden.dxf");
            document.SaveAs(path);
            DxfDatabase saved = DxfDatabase.Open(path);
            Assert.All(saved.ModelSpace.OfType<DxfLine>(), l => Assert.True(l.Visible));
            Assert.False(Entity<DxfLine>(document, first).Visible);

            Assert.True(document.Undo());
            Assert.True(Entity<DxfLine>(document, first).Visible);
            session = Start(document, "UNISOLATEOBJECTS");
            Assert.Contains("No objects to unisolate.", session.History);
        }
    }
}
