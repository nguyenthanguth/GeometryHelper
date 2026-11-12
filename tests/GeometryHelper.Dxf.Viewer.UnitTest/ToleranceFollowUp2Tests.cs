using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The second follow-ups of TOLERANCE, as AutoCAD 2021 does them (accoreconsole probes GHTH2 to GHTH4, 2026-11-12, the
    /// style Standard 0.18 high in Arial): the hook's own vertex on a leader to a frame and in a UCS, as AutoCAD writes the
    /// leader; QLEADER's group 213; Nearest and Perpendicular on the rectangles of a frame's rows alone; a row written by hand
    /// read into the Geometric Tolerance dialog.
    /// </summary>
    public class ToleranceFollowUp2Tests
    {
        private static readonly CadUcs Turned = CadUcs.InPlane(P(0, 0), Math.PI / 6.0);

        /// <summary>A drawing of AutoCAD's Standard current (DIMTXT 0.18, DIMGAP 0.09, DIMASZ 0.18), its text style in Arial.</summary>
        private static CadDocument Arial()
        {
            var database = new DxfDatabase("AC1032");
            database.TextStyleTable["Standard"].FileName = "arial.ttf";
            StandardCurrent(database);
            return CadDocument.From(database, "arial.dxf");
        }

        /// <summary>A metric drawing (ISO-25: DIMTXT 2.5), its style's DIMTAD nought.</summary>
        private static CadDocument Metric()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.DimStyleTable[database.Dimstyle].Dimtad = 0;
            database.Dimtad = 0;
            return CadDocument.From(database, "metric.dxf");
        }

        /// <summary>Adds a frame of words at a point along the world's x, in the drawing's dimension style.</summary>
        private static DxfTolerance Frame(CadDocument document, string text, GeoPoint3 at)
        {
            DxfTolerance frame = null;
            document.ApplyEdit("frame", null, s =>
            {
                frame = new DxfTolerance { Text = text, Location = at };
                s.Append(document.ModelSpace, frame);
                frame.DimensionStyleName = s.Database.Dimstyle;
            });
            return frame;
        }

        /// <summary>
        /// Writes QLEADER's settings into a drawing as AutoCAD's dialog writes them: the defaults (MText, Middle of top line on
        /// the right, Middle of bottom line on the left), the groups given changed.
        /// </summary>
        private static void Settings(CadDocument document, params int[] codeValue)
        {
            var values = new SortedDictionary<int, int>
            {
                [60] = 0,
                [61] = 0,
                [62] = 1,
                [63] = 3,
                [64] = 0,
                [65] = 0,
                [66] = 0,
                [67] = 3,
                [68] = 1,
                [69] = 0,
                [70] = 0,
                [71] = 0,
                [72] = 0,
                [170] = 0,
            };

            for (int i = 0; i < codeValue.Length; i += 2)
            {
                values[codeValue[i]] = codeValue[i + 1];
            }

            var data = new List<DxfTypedValue>
            {
                new DxfTypedValue(90, "990106"),
                new DxfTypedValue(3, string.Empty),
                new DxfTypedValue(40, "0.0"),
            };
            data.AddRange(values.Select(p => new DxfTypedValue(p.Key, p.Value.ToString(CultureInfo.InvariantCulture))));
            document.Database.NamedObjectsDictionary.SetAt("AcadDim", new DxfXrecord { Data = data });
        }

        private static DxfLeader LastLeader(CadDocument document) => All<DxfLeader>(document.ModelSpace).Last();

        private static void Vertices(DxfLeader leader, params GeoPoint3[] expected)
        {
            Assert.Equal(expected.Length, leader.NumVertices);

            for (int i = 0; i < expected.Length; i++)
            {
                Near(expected[i], leader.VertexAt(i), 1E-6);
            }
        }

        private static void Offset(double x, double y, DxfLeader leader)
        {
            Assert.Equal(x, leader.AnnotationOffset.X, 9);
            Assert.Equal(y, leader.AnnotationOffset.Y, 9);
            Assert.Equal(0.0, leader.AnnotationOffset.Z);
        }

        private static CadSnapResult SnapAt(
            CadDocument document,
            CadSnapModes modes,
            GeoPoint2 cursor,
            GeoPoint2? basePoint = null
        ) =>
            new ObjectSnap(Tight) { Modes = modes }.Snap(
                document.GetPickIndex(document.ModelSpace),
                cursor,
                0.2,
                basePoint
            );

        // ---- The hook's own vertex

        [Fact]
        public void ALeaderToACopiedFrame_OnASteepLastSegment_GetsTheHooksOwnVertex_AsAutoCadWritesIt()
        {
            // AutoCAD 2021 (GHTH3): {\Fgdt;j}%%v0.1 copied to a leader from (200, 40) to (210, 45) is written with a vertex at
            // (209.82, 45), DIMASZ short of the end; to the left, from (200, 60) to (190, 65), at (190.18, 65).
            CadDocument document = Arial();
            CadCommandSession session = Session(document);
            DxfTolerance source = Frame(document, "{\\Fgdt;j}%%v0.1", new GeoPoint3(200, 200, 0));

            Run(session, "LEADER", P(200, 40), P(210, 45), Enter, Enter, "C", new[] { source.Handle });
            DxfLeader right = LastLeader(document);
            Vertices(right, new GeoPoint3(200, 40, 0), new GeoPoint3(209.82, 45, 0), new GeoPoint3(210, 45, 0));
            Assert.True(right.HasHookLine);
            Near(new GeoPoint3(210, 45, 0), ((DxfTolerance)right.Annotation).Location);

            Run(session, "LEADER", P(200, 60), P(190, 65), Enter, Enter, "C", new[] { source.Handle });
            DxfLeader left = LastLeader(document);
            Vertices(left, new GeoPoint3(200, 60, 0), new GeoPoint3(190.18, 65, 0), new GeoPoint3(190, 65, 0));
            Near(new GeoPoint3(189.077039563, 65, 0), ((DxfTolerance)left.Annotation).Location, 1E-6);
            AssertReopens(document);
        }

        [Fact]
        public void InAUcsTurned30Degrees_TheHooksOwnVertex_StandsDimaszBackAlongItsX_ToWordsAndToFrames()
        {
            // AutoCAD 2021 (GHTH3), the UCS turned 30 degrees about the origin, each leader written with three vertices.
            CadDocument document = Arial();
            document.Ucs.SetCurrent(Turned);
            CadCommandSession session = Session(document);
            DxfTolerance source = Frame(document, "{\\Fgdt;j}%%v0.1", new GeoPoint3(200, 200, 0));

            // Words to the right, from the UCS's (0, 0) to its (10, 10), and to the left, from (0, 50) to (-10, 60).
            Run(session, "LEADER", Turned.ToWorld(P(0, 0)), Turned.ToWorld(P(10, 10)), Enter, "ABC", Enter);
            Vertices(
                LastLeader(document),
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(3.504369465163192, 13.57025403784438, 0),
                new GeoPoint3(3.66025403784439, 13.66025403784438, 0)
            );
            Run(session, "LEADER", Turned.ToWorld(P(0, 50)), Turned.ToWorld(P(-10, 60)), Enter, "ABC", Enter);
            Vertices(
                LastLeader(document),
                new GeoPoint3(-25.0, 43.30127018922193, 0),
                new GeoPoint3(-38.5043694651632, 47.05152422706631, 0),
                new GeoPoint3(-38.6602540378444, 46.96152422706631, 0)
            );

            // A copied frame to the left, from the UCS's (0, 300) to its (-10, 310): set back its width along the UCS's X.
            Run(
                session,
                "LEADER",
                Turned.ToWorld(P(0, 300)),
                Turned.ToWorld(P(-10, 310)),
                Enter,
                Enter,
                "C",
                new[] { source.Handle }
            );
            DxfLeader framed = LastLeader(document);
            Vertices(
                framed,
                new GeoPoint3(-150.0, 259.8076211353316, 0),
                new GeoPoint3(-163.6280227169745, 264.0193553914571, 0),
                new GeoPoint3(-163.7839072896557, 263.9293553914571, 0)
            );
            Near(new GeoPoint3(-164.583214474, 263.467875173, 0), ((DxfTolerance)framed.Annotation).Location, 1E-6);

            // AutoCAD sets the frame back along the world's X, then moves the end to its right side: level in the UCS, from
            // its (0, 400) to its (-10, 400), the end picked at (-208.66025, 341.41016) is moved to (-208.78391, 341.87164).
            Run(
                session,
                "LEADER",
                Turned.ToWorld(P(0, 400)),
                Turned.ToWorld(P(-10, 400)),
                Enter,
                Enter,
                "C",
                new[] { source.Handle }
            );
            DxfLeader level = LastLeader(document);
            Assert.False(level.HasHookLine);
            Vertices(
                level,
                new GeoPoint3(-200.0, 346.4101615137755, 0),
                new GeoPoint3(-208.7839072896557, 341.8716417320565, 0)
            );
            Near(new GeoPoint3(-209.583214474, 341.410161514, 0), ((DxfTolerance)level.Annotation).Location, 1E-6);

            // QLEADER in the UCS too: from its (0, 150) to its (10, 160).
            Run(session, "QLEADER", Turned.ToWorld(P(0, 150)), Turned.ToWorld(P(10, 160)), Enter, "0", "ABC", Enter);
            Vertices(
                LastLeader(document),
                new GeoPoint3(-75.0, 129.9038105676658, 0),
                new GeoPoint3(-71.49563053483681, 143.4740646055102, 0),
                new GeoPoint3(-71.3397459621556, 143.5640646055102, 0)
            );
            AssertReopens(document);
        }

        // ---- QLEADER's group 213

        [Fact]
        public void QLeaderWords_WriteHowFarTheirPointIsRaisedFromTheEnd_AsGroup213_AsAutoCadDoes()
        {
            // AutoCAD 2021 (GHTH2, GHTH4): Middle of top line raises the words half DIMTXT up the world's Y, and the leader
            // writes (0, -0.09, 0) as 213; Middle of bottom line, (0, 0.09, 0); the other rows and Underline bottom line none.
            CadDocument document = Arial();
            CadCommandSession session = Session(document);

            Run(session, "QLEADER", P(200, 140), P(210, 145), Enter, "0", "ABC", Enter);
            Offset(0, -0.09, LastLeader(document));
            Assert.Equal(0.0, Number(Assert.Single(Written(document, LastLeader(document).Handle, 213))));
            Assert.Equal(-0.09, Number(Assert.Single(Written(document, LastLeader(document).Handle, 223))), 12);
            Run(session, "QLEADER", P(200, 160), P(190, 165), Enter, "0", "ABC", Enter);
            Offset(0, 0.09, LastLeader(document));

            // Level, no hook: the same.
            Run(session, "QLEADER", P(200, 180), P(210, 180), Enter, "0", "ABC", Enter);
            Offset(0, -0.09, LastLeader(document));

            // In a UCS turned 30 degrees, still along the world's Y.
            document.Ucs.SetCurrent(Turned);
            Run(session, "QLEADER", Turned.ToWorld(P(0, 150)), Turned.ToWorld(P(10, 160)), Enter, "0", "ABC", Enter);
            Offset(0, -0.09, LastLeader(document));
            document.Ucs.SetCurrent(CadUcs.World);

            // LEADER writes none, nor does QLEADER's copy of an MTEXT, which stands by its middle.
            Run(session, "LEADER", P(200, 120), P(210, 125), Enter, "ABC", Enter);
            Offset(0, 0, LastLeader(document));
            Assert.Empty(Written(document, LastLeader(document).Handle, 213));

            Settings(document, 62, 0, 63, 4);
            Run(session, "QLEADER", P(0, 0), P(10, 5), Enter, "0", "ABC", "DEF", Enter);
            Offset(0, 0, LastLeader(document));
            Run(session, "QLEADER", P(0, 20), P(-10, 25), Enter, "0", "ABC", "DEF", Enter);
            Offset(0, 0, LastLeader(document));
            Settings(document, 62, 2, 63, 2);
            Run(session, "QLEADER", P(0, 100), P(10, 105), Enter, "0", "ABC", "DEF", Enter);
            Offset(0, 0, LastLeader(document));
            Settings(document, 62, 3, 63, 1);
            Run(session, "QLEADER", P(0, 150), P(10, 155), Enter, "0", "ABC", "DEF", Enter);
            Offset(0, 0.09, LastLeader(document));
            Run(session, "QLEADER", P(0, 170), P(-10, 175), Enter, "0", "ABC", "DEF", Enter);
            Offset(0, -0.09, LastLeader(document));
            Settings(document, 64, 1);
            Run(session, "QLEADER", P(0, 250), P(10, 255), Enter, "0", "ABC", "DEF", Enter);
            Offset(0, 0, LastLeader(document));

            // Always left justify on the left: the words set back their width as well, which 213 holds as its X.
            Settings(document, 69, 1);
            Run(session, "QLEADER", P(0, 320), P(-10, 325), Enter, "ABC", "DEF", Enter);
            DxfLeader justified = LastLeader(document);
            Assert.True(justified.AnnoWidth > 0.0);
            Offset(justified.AnnoWidth, 0.09, justified);

            DxfMText words = AddWords(document);
            Settings(document, 60, 1);
            Run(session, "QLEADER", P(0, 350), P(10, 355), Enter, new[] { words.Handle });
            Assert.IsType<DxfMText>(LastLeader(document).Annotation);
            Offset(0, 0, LastLeader(document));
            AssertReopens(document);
        }

        private static DxfMText AddWords(CadDocument document)
        {
            DxfMText words = null;
            document.ApplyEdit("words", null, s =>
            {
                words = new DxfMText
                {
                    Contents = "XYZ",
                    TextHeight = 0.25,
                    Location = new GeoPoint3(900, 900, 0),
                };
                s.Append(document.ModelSpace, words);
            });
            return words;
        }

        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

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
                    if (lines[k].Trim() == code.ToString(CultureInfo.InvariantCulture))
                    {
                        values.Add(lines[k + 1].Trim());
                    }
                }

                break;
            }

            return values;
        }

        // ---- Nearest and Perpendicular on a frame

        [Fact]
        public void NearestAndPerpendicular_TakeTheRectanglesOfTheRows_NotTheWallsOrTheSymbols()
        {
            // AutoCAD 2021 (GHTH2): Nearest and Perpendicular give points of the sides of a row's rectangle alone, never of a
            // wall between cells or of a symbol. Rows 5 high: the first 7.678571 wide, the second 13.928571, its wall at 6.25.
            CadDocument document = Metric();
            Frame(document, "{\\Fgdt;c}\n{\\Fgdt;j}%%v{\\Fgdt;c}", new GeoPoint3(0, 0, 0));

            // On the wall, half way down the second row, and on the end of the position's upright stroke: none.
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Nearest, P(6.25, -5.0)).Kind);
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Nearest, P(3.125, -3.75)).Kind);
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Perpendicular, P(6.25, -5.0), P(20, -5)).Kind);

            // Near the bottom of the second row: its side.
            CadSnapResult nearest = SnapAt(document, CadSnapModes.Nearest, P(3.0, -7.45));
            Assert.Equal(CadSnapKind.Nearest, nearest.Kind);
            Near(P(3.0, -7.5), nearest.Point);
            CadSnapResult square = SnapAt(document, CadSnapModes.Perpendicular, P(3.0, -7.45), P(3.0, -20));
            Assert.Equal(CadSnapKind.Perpendicular, square.Kind);
            Near(P(3.0, -7.5), square.Point);
        }

        // ---- A row written by hand

        [Fact]
        public void ARowWrittenByHand_ReadsALetterAfterTheToleranceAsADatum_AndOkWritesTheDialogsLayout()
        {
            // {\Fgdt;j}%%v0.1%%vA%%vB has no cell for the second tolerance: A and B are its datums, not a second tolerance
            // and a first datum. AutoCAD 2021 draws it as the dialog's layout, a cell of nothing taking no room (GHTH2:
            // {\Fgdt;j}%%v0.1%%vA and {\Fgdt;j}%%v0.1%%v%%vA both 1.271051 wide in Arial).
            CadDocument document = Metric();
            DxfTolerance frame = Frame(
                document,
                "{\\Fgdt;j}%%v0.1%%vA%%vB\n0.1%%vA{\\Fgdt;m}",
                new GeoPoint3(10, 20, 0)
            );
            CadCommandSession session = Session(document);
            CadToleranceRequest asked = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadToleranceRequest request)
                {
                    asked = request;
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "TEXTEDIT", new[] { frame.Handle }, Enter);

            Assert.NotNull(asked);
            CadToleranceRow first = asked.Rows[0];
            Assert.Equal('j', first.Symbol);
            Assert.Equal("0.1", first.Tolerance1.Value);
            Assert.True(first.Tolerance2.IsEmpty);
            Assert.Equal(new[] { "A", "B", string.Empty }, first.Datums.Select(d => d.Letter));

            // A row of no symbol: its first cell the tolerance, A at MMC its first datum.
            CadToleranceRow second = asked.Rows[1];
            Assert.Null(second.Symbol);
            Assert.Equal("0.1", second.Tolerance1.Value);
            Assert.True(second.Tolerance2.IsEmpty);
            Assert.Equal("A", second.Datums[0].Letter);
            Assert.Equal(CadMaterialCondition.Maximum, second.Datums[0].Material);

            Assert.Equal(
                "{\\Fgdt;j}%%v0.1%%v%%vA%%vB\n%%v0.1%%v%%vA{\\Fgdt;m}",
                Entity<DxfTolerance>(document, frame.Handle).Text
            );
        }
    }
}
