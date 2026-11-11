using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The follow-ups of TOLERANCE, as AutoCAD 2021 does them (accoreconsole probes GHTQ1 and GHTQ2, 2026-11-12): the snaps End
    /// and Mid on the rectangles of a frame's rows alone and Insertion at its point; the room of each gdt symbol, which set
    /// cells of words and symbols 0.0043 too wide; the current dimension variables written as overrides; a frame edited in
    /// the Geometric Tolerance dialog and in Properties; EXPLODE refusing one by name; a frame on a leader to the left set
    /// back its widest row; QLEADER's angle constraints and LEADER's group 211 in a UCS.
    /// </summary>
    public class ToleranceFollowUpTests
    {
        private const string Full = "{\\Fgdt;j}%%v{\\Fgdt;n}0.1{\\Fgdt;m}%%vA%%vB%%vC";

        // A row as the Geometric Tolerance dialog writes it: the empty second tolerance kept between its cells.
        private const string Filled = "{\\Fgdt;j}%%v{\\Fgdt;n}0.1{\\Fgdt;m}%%v%%vA%%vB%%vC";

        /// <summary>A metric drawing (ISO-25: DIMTXT 2.5), its style's DIMTAD nought.</summary>
        private static CadDocument Metric()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.DimStyleTable[database.Dimstyle].Dimtad = 0;
            database.Dimtad = 0;
            return CadDocument.From(database, "metric.dxf");
        }

        /// <summary>A drawing of AutoCAD's Standard current (DIMTXT 0.18), its text style in Arial.</summary>
        private static CadDocument Arial()
        {
            var database = new DxfDatabase("AC1032");
            database.TextStyleTable["Standard"].FileName = "arial.ttf";
            StandardCurrent(database);
            return CadDocument.From(database, "arial.dxf");
        }

        /// <summary>Adds a frame of words at a point along the world's x, in the drawing's dimension style.</summary>
        private static DxfTolerance Frame(CadDocument document, string text, GeoPoint3 at)
        {
            DxfTolerance frame = null;
            document.ApplyEdit("frame", null, s =>
            {
                frame = new DxfTolerance { Text = text, Location = at };
                s.Append(document.ModelSpace, frame);

                if (s.Database.DimStyleTable.Has(s.Database.Dimstyle))
                {
                    frame.DimensionStyleName = s.Database.Dimstyle;
                }
            });
            return frame;
        }

        /// <summary>The width of what the canvas draws of an entity.</summary>
        private static double DrawnWidth(CadDocument document, string handle)
        {
            List<DisplayItem2> items = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle)
                .ToList();
            Assert.NotEmpty(items);
            return items.Max(i => i.Bounds.MaxX) - items.Min(i => i.Bounds.MinX);
        }

        /// <summary>The groups the overrides of an entity name, in the order they are written.</summary>
        private static string Overrides(DxfEntity entity)
        {
            IReadOnlyList<DxfTypedValue> data = entity.GetXDataForApplication("ACAD");

            if (data == null)
            {
                return string.Empty;
            }

            int open = data.ToList().FindIndex(v => v.TypeCode == 1002 && v.Value == "{");
            var groups = new List<string>();

            for (int i = open + 1; i < data.Count && data[i].TypeCode == 1070; i += 2)
            {
                groups.Add(data[i].Value.Trim());
            }

            return string.Join(" ", groups);
        }

        /// <summary>The values of a group of the record of an entity, as the drawing writes it.</summary>
        private static List<string> Written(CadDocument document, string handle, int code)
        {
            string[] lines = document.Database.ToText().Replace("\r", string.Empty).Split('\n');
            var values = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "5" || lines[i + 1].Trim() != handle)
                {
                    continue;
                }

                for (int k = i + 2; k + 1 < lines.Length && lines[k].Trim() != "0"; k += 2)
                {
                    if (lines[k].Trim() == code.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    {
                        values.Add(lines[k + 1].Trim());
                    }
                }

                break;
            }

            return values;
        }

        private static CadSnapResult SnapAt(CadDocument document, CadSnapModes modes, GeoPoint2 cursor) =>
            new ObjectSnap(Tight) { Modes = modes }.Snap(document.GetPickIndex(document.ModelSpace), cursor, 0.2);

        // ---- The width of cells of words and symbols

        [Fact]
        public void InArial_CellsOfWordsAndSymbols_AreAsWideAsAutoCads_EachSymbolWithItsOwnRoom()
        {
            // AutoCAD 2021's console (GHTQ1, GHTQ2), DIMTXT 0.18 in Arial: the widths of the frames' extents. The diameter has
            // 1.5/21 of the height either side, the runouts 2.25/21 on their right, the rest 2/21 either side.
            CadDocument document = Arial();
            var widths = new[]
            {
                new KeyValuePair<string, double>(Full, 2.462701227830832),
                new KeyValuePair<string, double>("{\\Fgdt;n}0.1", 0.67625414149289043),
                new KeyValuePair<string, double>("0.1{\\Fgdt;m}", 0.80626973299551707),
                new KeyValuePair<string, double>("{\\Fgdt;n}0.1{\\Fgdt;m}", 1.0095634379263316),
                new KeyValuePair<string, double>("{\\Fgdt;n}{\\Fgdt;m}", 0.66),
                new KeyValuePair<string, double>("{\\Fgdt;n}1", 0.4665406353537378),
                new KeyValuePair<string, double>("A{\\Fgdt;n}", 0.54094718378483719),
                new KeyValuePair<string, double>("{\\Fgdt;h}A", 0.635232898070555),
                new KeyValuePair<string, double>("{\\Fgdt;t}{\\Fgdt;t}", 1.1464285714285722),
                new KeyValuePair<string, double>("{\\Fgdt;c}", 0.5528571428571496),
                // Words inked past their pen (A, f) set what follows on from their ink; ink left of a cell's start is not
                // counted, ink left of words after a symbol is (GHTQ3).
                new KeyValuePair<string, double>("A", 0.34809004092769441),
                new KeyValuePair<string, double>("j{\\Fgdt;n}", 0.42872344572208143),
                new KeyValuePair<string, double>("{\\Fgdt;n}j", 0.41141103098811149),
                new KeyValuePair<string, double>("f{\\Fgdt;n}", 0.44910543753654258),
                new KeyValuePair<string, double>("{\\Fgdt;n}f", 0.4514383161177159),
            };
            double y = 0.0;

            foreach (KeyValuePair<string, double> width in widths)
            {
                DxfTolerance frame = Frame(document, width.Key, new GeoPoint3(0, y, 0));
                Assert.True(
                    Math.Abs(DrawnWidth(document, frame.Handle) - width.Value) < 1E-6,
                    width.Key + ": " + DrawnWidth(document, frame.Handle)
                );
                y += 10.0;
            }
        }

        // ---- Snaps

        [Fact]
        public void EndAndMid_SnapToTheRectanglesOfTheRows_NotToTheWallsOrTheSymbols()
        {
            // AutoCAD 2021 (GHTQ2): End gives the ends and Mid the middle of a side of a row's rectangle, none of a wall between
            // cells or of a symbol. Rows 5 high: the first 7.678571 wide (the flatness), the second 13.928571.
            CadDocument document = Metric();
            Frame(document, "{\\Fgdt;c}\n{\\Fgdt;j}%%v{\\Fgdt;c}", new GeoPoint3(0, 0, 0));

            CadSnapResult corner = SnapAt(document, CadSnapModes.Endpoint, P(0.05, 2.45));
            Assert.Equal(CadSnapKind.Endpoint, corner.Kind);
            Near(P(0, 2.5), corner.Point);

            // The bottom of the first row, 7.678571 long, has its own middle; the left sides theirs, a row each.
            CadSnapResult bottom = SnapAt(document, CadSnapModes.Midpoint, P(3.84, -2.45));
            Assert.Equal(CadSnapKind.Midpoint, bottom.Kind);
            Near(P(7.678571428571429 / 2.0, -2.5), bottom.Point, 1E-6);
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Midpoint, P(0.05, -2.45)).Kind);
            Assert.Equal(CadSnapKind.Midpoint, SnapAt(document, CadSnapModes.Midpoint, P(0.05, -5.0)).Kind);

            // The wall after the position, at x 6.25 in the second row, and the end of the position's upright stroke.
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Endpoint, P(6.25, -2.55)).Kind);
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Midpoint, P(6.25, -5.0)).Kind);
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Endpoint, P(3.125, -3.75)).Kind);
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Midpoint, P(3.125, -5.0)).Kind);
        }

        [Fact]
        public void Insertion_SnapsToTheFramesPoint()
        {
            // AutoCAD 2021 (GHTQ1): Insertion gives the frame's insertion point, the middle of the left side of its first row.
            CadDocument document = Metric();
            Frame(document, "{\\Fgdt;j}%%v{\\Fgdt;c}", new GeoPoint3(10, 20, 0));

            CadSnapResult insertion = SnapAt(document, CadSnapModes.Insertion, P(10.05, 20.1));
            Assert.Equal(CadSnapKind.Insertion, insertion.Kind);
            Near(P(10, 20), insertion.Point);
        }

        // ---- TOLERANCE's overrides

        [Fact]
        public void Tolerance_WritesTheCurrentVariablesOtherThanItsStyles_AsOverrides()
        {
            // As dimensions and leaders: DIMTXT 5 and DIMCLRD red typed over ISO-25 are written 140 then 176, and the frame is
            // drawn by them, its rows 10 high.
            CadDocument document = Metric();
            document.ApplyEdit("vars", null, s =>
            {
                s.Database.Dimtxt = 5.0;
                s.Database.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            });
            CadCommandSession session = Session(document);
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadToleranceRequest request)
                {
                    request.Rows[0].Symbol = 'j';
                    request.Rows[0].Tolerance1.Value = "0.1";
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "TOLERANCE", P(10, 20));

            DxfTolerance frame = Assert.Single(All<DxfTolerance>(document.ModelSpace));
            Assert.Equal("ISO-25", frame.DimensionStyleName);
            Assert.Equal("140 176", Overrides(frame));
            DxfDimStyleTableRecord data = frame.GetDimstyleData();
            Assert.Equal(5.0, data.Dimtxt);
            Assert.Equal(1, data.Dimclrd.ColorIndex);
            List<DisplayItem2> items = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == frame.Handle)
                .ToList();
            Assert.Equal(10.0, items.Max(i => i.Bounds.MaxY) - items.Min(i => i.Bounds.MinY), 6);
            AssertReopens(document);
        }

        // ---- Editing a frame

        [Fact]
        public void TextEdit_OpensTheDialogFilledFromTheFrame_AndOkWritesItsWords()
        {
            CadDocument document = Metric();
            DxfTolerance frame = Frame(
                document,
                Filled + "\n{\\Fgdt;c}%%v0.05\n5{\\Fgdt;p}\nD",
                new GeoPoint3(10, 20, 0)
            );
            CadCommandSession session = Session(document);
            CadToleranceRequest asked = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadToleranceRequest request)
                {
                    asked = request;
                    request.Rows[1].Tolerance1.Value = "0.02";
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "TEXTEDIT", new[] { frame.Handle }, Enter);

            Assert.NotNull(asked);
            CadToleranceRow first = asked.Rows[0];
            Assert.Equal('j', first.Symbol);
            Assert.True(first.Tolerance1.Diameter);
            Assert.Equal("0.1", first.Tolerance1.Value);
            Assert.Equal(CadMaterialCondition.Maximum, first.Tolerance1.Material);
            Assert.True(first.Tolerance2.IsEmpty);
            Assert.Equal(new[] { "A", "B", "C" }, first.Datums.Select(d => d.Letter));
            Assert.Equal('c', asked.Rows[1].Symbol);
            Assert.Equal("5", asked.Height);
            Assert.True(asked.ProjectedZone);
            Assert.Equal("D", asked.DatumIdentifier);

            // OK wrote the words back, the second row's tolerance changed; one undo step takes it back.
            Assert.Equal(
                Filled + "\n{\\Fgdt;c}%%v0.02\n5{\\Fgdt;p}\nD",
                Entity<DxfTolerance>(document, frame.Handle).Text
            );
            Assert.True(document.Undo());
            Assert.Equal(
                Filled + "\n{\\Fgdt;c}%%v0.05\n5{\\Fgdt;p}\nD",
                Entity<DxfTolerance>(document, frame.Handle).Text
            );
        }

        [Fact]
        public void Properties_ShowAFramesStyleColoursAndTextHeight_AndWriteThemAsOverrides()
        {
            CadDocument document = Metric();
            DxfTolerance frame = Frame(document, "{\\Fgdt;j}%%v0.1", new GeoPoint3(0, 0, 0));

            string Row(string label) =>
                CadProperties
                    .Describe(document, null, new[] { frame.Handle })
                    .Groups.SelectMany(g => g.Rows)
                    .Single(r => r.Label == label)
                    .Value;
            Assert.Equal("ISO-25", Row("Dim style"));
            Assert.Equal("ByBlock", Row("Dim line color"));
            Assert.Equal("ByBlock", Row("Text color"));
            Assert.Equal("2.5000", Row("Text height"));

            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Text height", "5").Succeeded);
            Assert.True(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Dim line color", "1").Succeeded);
            Assert.Equal("140 176", Overrides(Entity<DxfTolerance>(document, frame.Handle)));
            Assert.Equal("5.0000", Row("Text height"));
            Assert.Equal(1, Entity<DxfTolerance>(document, frame.Handle).GetDimstyleData().Dimclrd.ColorIndex);

            Assert.False(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Dim style", "NoSuchStyle").Succeeded);
            Assert.False(CadPropertyEditor.Set(document, new[] { frame.Handle }, "Text height", "0").Succeeded);
        }

        // ---- EXPLODE

        [Fact]
        public void Explode_RefusesAFrameByName_AsAutoCadDoes()
        {
            // AutoCAD 2021 (GHTQ1): "Cannot explode TOLERANCE", the frame kept.
            CadDocument document = Metric();
            DxfTolerance frame = Frame(document, "{\\Fgdt;j}%%v0.1", new GeoPoint3(0, 0, 0));
            CadCommandSession session = Session(document);

            Run(session, "EXPLODE", new[] { frame.Handle }, Enter);

            Assert.Contains("Cannot explode TOLERANCE", session.History);
            Assert.DoesNotContain(session.History, l => l.Contains("not able to be exploded"));
            Assert.Single(All<DxfTolerance>(document.ModelSpace));
        }

        // ---- Leaders

        [Fact]
        public void AFrameOnALeaderToTheLeft_IsSetBackItsWidestRow_AndTheLeaderHoldsItsWidthAndHeight()
        {
            // AutoCAD 2021 (GHTQ2): a frame of rows 0.55286 and 1.27105 wide copied to a leader ending at (190, 105) stands at
            // (188.72895, 105); the leader's 40 is 1.27105 and its 41 0.72, the two rows. Here rows 7.678571 and 13.928571.
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            DxfTolerance source = Frame(document, "{\\Fgdt;c}\n{\\Fgdt;j}%%v{\\Fgdt;c}", new GeoPoint3(200, 200, 0));

            Run(session, "LEADER", P(100, 100), P(90, 105), Enter, Enter, "C", new[] { source.Handle });

            DxfTolerance copy = All<DxfTolerance>(document.ModelSpace).Last();
            Near(new GeoPoint3(90.0 - 13.928571428571429, 105, 0), copy.Location, 1E-6);
            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            Assert.Equal(13.928571428571429, leader.AnnoHeight, 6);
            Assert.Equal(10.0, leader.AnnoWidth, 9);
        }

        [Fact]
        public void QLeaderInATurnedUcs_HoldsItsAngleConstraintsAlongTheUcs_AndLaysThePointOffAlongTheWorld()
        {
            // AutoCAD 2021 (GHTQ1), UCS turned 30 degrees: the UCS's (310, 503) from (300, 500) held level is 10 along the
            // world's X; (305, 510) from (300, 500) held to 45 degrees is (7.5, 7.5) on in the world.
            foreach (CadQLeaderAngle angle in new[] { CadQLeaderAngle.Horizontal, CadQLeaderAngle.FortyFive })
            {
                CadDocument document = Metric();
                CadUcs ucs = CadUcs.InPlane(P(0, 0), Math.PI / 6.0);
                document.Ucs.SetCurrent(ucs);
                CadCommandSession session = Session(document);
                session.UiRequested += (s, e) =>
                {
                    if (e.Payload is CadQLeaderRequest request)
                    {
                        request.Settings.Annotation = CadQLeaderAnnotation.None;
                        request.Settings.FirstSegment = angle;
                        request.Action = CadDialogAction.Ok;
                        e.Handled = true;
                    }
                };

                GeoPoint2 first = ucs.ToWorld(P(300, 500));
                GeoPoint2 picked = ucs.ToWorld(angle == CadQLeaderAngle.Horizontal ? P(310, 503) : P(305, 510));
                Run(session, "QLEADER", "S", first, picked, ucs.ToWorld(P(320, 530)));

                DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
                GeoPoint3 second = leader.VertexAt(1);
                GeoVector2 way = angle == CadQLeaderAngle.Horizontal ? new GeoVector2(10, 0) : new GeoVector2(7.5, 7.5);
                Near(new GeoPoint3(first.X + way.X, first.Y + way.Y, 0), second, 1E-9);
            }
        }

        [Fact]
        public void ALeaderToWordsInATurnedUcs_WritesItsXAsGroup211_OneOfNoAnnotationTheWorlds()
        {
            // AutoCAD 2021 (GHTQ1, GHTQ2): 211 (0.866, 0.5, 0) on a leader to words or a frame made in a UCS turned 30 degrees,
            // none (the world's X) on one of no annotation.
            CadDocument document = Metric();
            CadUcs ucs = CadUcs.InPlane(P(0, 0), Math.PI / 6.0);
            document.Ucs.SetCurrent(ucs);
            CadCommandSession session = Session(document);

            Run(session, "LEADER", ucs.ToWorld(P(0, 0)), ucs.ToWorld(P(10, 5)), Enter, "ABC", Enter);
            DxfLeader words = All<DxfLeader>(document.ModelSpace).Last();
            Run(session, "LEADER", ucs.ToWorld(P(0, 50)), ucs.ToWorld(P(10, 55)), Enter, Enter, "N");
            DxfLeader none = All<DxfLeader>(document.ModelSpace).Last();

            List<string> x = Written(document, words.Handle, 211);
            Assert.Equal(
                0.8660254037844387,
                double.Parse(Assert.Single(x), System.Globalization.CultureInfo.InvariantCulture),
                12
            );
            Assert.Equal(
                0.5,
                double.Parse(
                    Assert.Single(Written(document, words.Handle, 221)),
                    System.Globalization.CultureInfo.InvariantCulture
                ),
                12
            );
            List<string> world = Written(document, none.Handle, 211);
            Assert.True(world.Count == 0 || world[0] == "1.0" || world[0] == "1", string.Join(",", world));
        }
    }
}
