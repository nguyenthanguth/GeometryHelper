using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// WMFOUT, PSOUT, STLOUT and SVGOUT, and the types of EXPORT that go on as they and WBLOCK and ATTEXT do: the prompts as
    /// AutoCAD 2021's resource strings give them, and each file read back by a parser of its own format that does not share
    /// code with the writer (the records of a metafile, the operators of PostScript, the XML of an SVG, the groups of a
    /// DXX file), and for the metafile also by the reader of WMFIN.
    /// </summary>
    public class ExportWritersTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "exportwriters-" + Guid.NewGuid().ToString("N")
        );

        public ExportWritersTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        /// <summary>
        /// A red line A1 along y = 20 and a green line A2 along y = 80, both from x = 10 to x = 190, saved as two-lines.dxf in
        /// the test's folder; the box of both is 180 by 60.
        /// </summary>
        private CadDocument TwoLines()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Line(10, 20, 190, 20, color: 1, handle: "A1"),
                    TestDrawings.Line(10, 80, 190, 80, color: 3, handle: "A2")
                )
            );
            document.SaveAs(Path.Combine(_folder, "two-lines.dxf"));
            return document;
        }

        private string At(string name) => Path.Combine(_folder, name);

        // The records of a Windows Metafile, read here as [MS-WMF] lays them out.

        private sealed class Record
        {
            public int Function;
            public byte[] Data;

            public int Word(int index) => (short)(Data[2 * index] | (Data[2 * index + 1] << 8));

            public int Count => Data.Length / 2;
        }

        private static int Int16At(byte[] bytes, int at) => (short)(bytes[at] | (bytes[at + 1] << 8));

        private static int UInt16At(byte[] bytes, int at) => bytes[at] | (bytes[at + 1] << 8);

        /// <summary>The records of a placeable metafile, after checking its two headers: the key, the checksum, the sizes.</summary>
        private static List<Record> Records(byte[] file, out int width, out int height, out int inch)
        {
            Assert.Equal(0x9AC6CDD7u, BitConverter.ToUInt32(file, 0));
            int check = 0;

            for (int i = 0; i < 20; i += 2)
            {
                check ^= UInt16At(file, i);
            }

            Assert.Equal(check, UInt16At(file, 20));
            Assert.Equal(0, Int16At(file, 6));
            Assert.Equal(0, Int16At(file, 8));
            width = Int16At(file, 10);
            height = Int16At(file, 12);
            inch = UInt16At(file, 14);
            Assert.Equal(1, UInt16At(file, 22));
            Assert.Equal(9, UInt16At(file, 24));
            Assert.Equal(0x0300, UInt16At(file, 26));
            long totalWords = BitConverter.ToUInt32(file, 28);
            Assert.Equal((file.Length - 22) / 2, totalWords);
            var records = new List<Record>();
            int at = 22 + 18;
            int largest = 0;

            while (at < file.Length)
            {
                int words = (int)BitConverter.ToUInt32(file, at);
                largest = Math.Max(largest, words);
                var record = new Record
                {
                    Function = UInt16At(file, at + 4),
                    Data = file.Skip(at + 6).Take(words * 2 - 6).ToArray(),
                };
                records.Add(record);
                at += words * 2;
            }

            Assert.Equal(file.Length, at);
            Assert.Equal(0x0000, records.Last().Function);
            Assert.Equal(largest, (int)BitConverter.ToUInt32(file, 34));
            return records;
        }

        private static List<Record> Records(byte[] file) => Records(file, out _, out _, out _);

        private static int Rgb(Record pen, int at)
        {
            return (pen.Data[at] << 16) | (pen.Data[at + 1] << 8) | pen.Data[at + 2];
        }

        /// <summary>The points of a Polyline record, as (x, y) pairs.</summary>
        private static List<int[]> PolylinePoints(Record polyline)
        {
            int count = polyline.Word(0);
            return Enumerable
                .Range(0, count)
                .Select(i => new[] { polyline.Word(1 + 2 * i), polyline.Word(2 + 2 * i) })
                .ToList();
        }

        // WMFOUT

        [Fact]
        public void WmfOut_AsksAutoCadsPrompts_AndWritesTheSelectedLinesAsAPlaceableMetafileInTheirColours()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);
            string file = At("plan");

            Run(session, "WMFOUT");
            Assert.Equal("Enter file name <" + At("two-lines.wmf") + ">:", session.PromptText);
            Run(session, file);
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, (object)new[] { "A1", "A2" });
            int lines = session.History.Count;
            Run(session, Enter);

            // Nothing is printed, and the drawing is not changed.
            Assert.False(session.IsRunning);
            Assert.Equal(CadCommandOutcome.Completed, session.LastOutcome);
            Assert.Equal(lines + 1, session.History.Count);
            Assert.False(document.IsModified);
            Assert.Null(document.NextUndo);

            byte[] bytes = File.ReadAllBytes(file + ".wmf");
            List<Record> records = Records(bytes, out int width, out int height, out int inch);

            // The longer side, 180 units, is 20 000 logical units; the 60 of the shorter, 6 667.
            Assert.Equal(20000, width);
            Assert.Equal(6667, height);
            Assert.Equal(1440, inch);
            Record extent = Assert.Single(records, r => r.Function == 0x020C);
            Assert.Equal(6667, extent.Word(0));
            Assert.Equal(20000, extent.Word(1));

            // Two pens, red then green, each followed by its line: A1 along the bottom (y down), A2 along the top.
            Record[] pens = records.Where(r => r.Function == 0x02FA).ToArray();
            Assert.Equal(2, pens.Length);
            Assert.Equal(0xFF0000, Rgb(pens[0], 6));
            Assert.Equal(0x00FF00, Rgb(pens[1], 6));
            Record[] polylines = records.Where(r => r.Function == 0x0325).ToArray();
            Assert.Equal(2, polylines.Length);
            Assert.Equal(new[] { new[] { 0, 6667 }, new[] { 20000, 6667 } }, PolylinePoints(polylines[0]));
            Assert.Equal(new[] { new[] { 0, 0 }, new[] { 20000, 0 } }, PolylinePoints(polylines[1]));

            // WMFIN's reader takes the same two lines back.
            CadWmfPicture picture = CadWmf.Read(bytes);
            Assert.Equal(20000.0, picture.ExtentX);
            Assert.Equal(6667.0, picture.ExtentY);
            Assert.Equal(2, picture.Shapes.Count);
            Assert.Equal(0xFF0000, picture.Shapes[0].Rgb);
            Assert.Equal(0x00FF00, picture.Shapes[1].Rgb);
            Assert.Equal(new[] { 0.0, 6667.0 }, picture.Shapes[0].Points[0]);
        }

        [Fact]
        public void WmfOut_WritesOnlyTheObjectsSelected_AndNothingWhereNoneIs()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "WMFOUT", At("red"), new[] { "A1" }, Enter);
            byte[] bytes = File.ReadAllBytes(At("red.wmf"));
            List<Record> records = Records(bytes, out int width, out int height, out _);

            // The box of one horizontal line is 180 by nought: the width is the longer side, the height 0.
            Assert.Equal(20000, width);
            Assert.Equal(0, height);
            Assert.Single(records, r => r.Function == 0x0325);

            Run(session, "WMFOUT", At("none"), Enter);
            Assert.False(session.IsRunning);
            Assert.False(File.Exists(At("none.wmf")));
        }

        [Fact]
        public void WmfOutWithAWindow_AsksItsFileInACreateWmfFileDialog_OfTheMetafileType()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);
            var asked = new List<CadExportJob>();
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "WMFOUT")
                {
                    asked.Add(
                        new CadExportJob(e.Export.Title, e.Export.FileTypes, e.Export.FileType, e.Export.FileName)
                    );
                    e.Export.FileName = At("chosen.wmf");
                    e.Handled = true;
                }
            };

            Run(session, "WMFOUT");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { "A2" }, Enter);

            CadExportJob job = Assert.Single(asked);
            Assert.Equal("Create WMF File", job.Title);
            Assert.Equal(new[] { "Metafile (*.wmf)" }, job.FileTypes);
            Assert.Equal(At("two-lines.wmf"), job.FileName);
            Assert.True(File.Exists(At("chosen.wmf")));

            // A dialog cancelled writes nothing and does not ask for objects.
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "WMFOUT")
                {
                    e.Export.FileName = null;
                    e.Handled = true;
                }
            };
            File.Delete(At("chosen.wmf"));
            Run(session, "WMFOUT");
            Assert.False(session.IsRunning);
            Assert.False(File.Exists(At("chosen.wmf")));
        }

        [Fact]
        public void WmfOut_GivesACircleItsPointsAFillItsBrushAndAWordsItsFont_AllReadBackByWmfIn()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 40.0));
            var solid = new DxfSolid(
                new GeoPoint3(100, 10, 0),
                new GeoPoint3(140, 10, 0),
                new GeoPoint3(100, 30, 0),
                new GeoPoint3(140, 30, 0)
            )
            {
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5),
            };
            database.ModelSpace.AppendEntity(solid);
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = "Hello (wmf)",
                    Height = 8.0,
                    Position = new GeoPoint3(10, 100, 0),
                    TextStyleName = "Standard",
                }
            );
            string path = At("shapes.dxf");
            database.Save(path);
            CadDocument document = CadDocument.Open(path);
            CadCommandSession session = Session(document);
            string[] all = document.Database.ModelSpace.Select(e => e.Handle).ToArray();

            Run(session, "WMFOUT", At("shapes"), all, Enter);

            byte[] bytes = File.ReadAllBytes(At("shapes.wmf"));
            List<Record> records = Records(bytes);

            // The circle is a closed chain of at least 60 short pieces; the solid is a PolyPolygon of one ring of four corners
            // under a solid brush of blue and a pen of none; the words are a TextOut of them, in a font of Arial.
            Record circle = records.First(r => r.Function == 0x0325 && r.Word(0) > 60);
            List<int[]> ring = PolylinePoints(circle);
            Assert.Equal(ring.First(), ring.Last());
            Record polygon = Assert.Single(records, r => r.Function == 0x0538);
            Assert.Equal(1, polygon.Word(0));
            Assert.Equal(4, polygon.Word(1));
            Record brush = Assert.Single(records, r => r.Function == 0x02FC);
            Assert.Equal(0x0000FF, Rgb(brush, 2));
            Assert.Contains(records, r => r.Function == 0x02FA && r.Word(0) == 5);
            Record words = Assert.Single(records, r => r.Function == 0x0521);
            Assert.Equal(11, words.Word(0));
            Assert.Equal("Hello (wmf)", Encoding.ASCII.GetString(words.Data, 2, 11));
            Record font = Assert.Single(records, r => r.Function == 0x02FB);
            Assert.Equal("Arial", Encoding.ASCII.GetString(font.Data, 18, 32).TrimEnd('\0'));

            // The em of text 8 high is 8 / 0.72 units, at 20 000 over the longer side of the drawing's box.
            CadWmfPicture picture = CadWmf.Read(bytes);
            CadWmfShape text = Assert.Single(picture.Shapes, s => s.Text != null);
            Assert.Equal("Hello (wmf)", text.Text);
            Assert.Equal("Arial", text.Face);
            double f =
                20000.0 / Math.Max(picture.ExtentX, picture.ExtentY) * Math.Max(picture.ExtentX, picture.ExtentY);
            Assert.True(f > 0.0);
            Assert.InRange(text.Em, 1.0, 20000.0);
            Assert.Contains(picture.Shapes, s => s.Text == null && s.Closed == false && s.Points.Count > 60);
        }

        [Fact]
        public void WmfOut_GivesADashedLineApenOfNoWidthAndTheDashStyle()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.DrawingWithLinetypes(
                    TestDrawings.Linetype("DASHED", 12.0, -6.0),
                    null,
                    null,
                    TestDrawings.Entity(
                        "LINE",
                        "0",
                        1,
                        "D1",
                        "6",
                        "DASHED",
                        "10",
                        0.0,
                        "20",
                        0.0,
                        "11",
                        100.0,
                        "21",
                        0.0
                    )
                )
            );
            document.SaveAs(At("dashed.dxf"));
            CadCommandSession session = Session(document);

            Run(session, "WMFOUT", At("dashed"), new[] { "D1" }, Enter);

            // GDI draws no dash with a wide pen: the pen is of the dash style, 1, and of no width.
            Record pen = Assert.Single(Records(File.ReadAllBytes(At("dashed.wmf"))), r => r.Function == 0x02FA);
            Assert.Equal(1, pen.Word(0));
            Assert.Equal(0, pen.Word(1));
            Assert.Equal(0xFF0000, Rgb(pen, 6));
        }

        // PSOUT

        private static List<string> PostScript(string path) =>
            File.ReadAllText(path, Encoding.ASCII).Split('\n').ToList();

        [Fact]
        public void PsOut_AsksAutoCadsQuestionsInOrder_AndWritesTheAreaToScaleAsEncapsulatedPostScript()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);
            string file = At("plan");

            Run(session, "PSOUT");
            Assert.Equal("Enter PostScript file name <" + At("two-lines.eps") + ">:", session.PromptText);
            Run(session, file);
            Assert.Equal("What to plot -- Display, Extents, Limits, View, or Window <D>:", session.PromptText);
            Run(session, "E");
            Assert.Equal("Enter screen preview image type [None/EPSI/TIFF] <None>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter size units [Inches/Millimeters] <Inches>:", session.PromptText);
            Run(session, "M");
            Assert.Equal(
                "Enter plotted Millimeters to drawing units (as P=D) or [Fit/? (help)] <F>:",
                session.PromptText
            );
            Run(session, "1=10");
            Assert.Equal("Enter the Size or Width,Height (in Millimeters) <USER>:", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("Effective plotting area:  18.00 wide by 6.00 high", session.History.Last());

            // 180 by 60 drawing units at 1 millimetre to 10 is 18 by 6 millimetres: 51.02 by 17.01 points.
            List<string> lines = PostScript(file + ".eps");
            Assert.Equal("%!PS-Adobe-3.0 EPSF-3.0", lines[0]);
            Assert.Contains("%%BoundingBox: 0 0 52 18", lines);
            Assert.Contains("%%HiResBoundingBox: 0 0 51.024 17.008", lines);
            Assert.Equal("%%EOF", lines[lines.Count - 2]);

            // A1 in red: its start at the lower left corner of the output, its end 51.024 points along; A2 in green above.
            string red = lines.Single(l => l.StartsWith("1 0 0 C", StringComparison.Ordinal));
            Assert.Contains("0 0 m 51.024 0 l s", red);
            string green = lines.Single(l => l.StartsWith("0 1 0 C", StringComparison.Ordinal));
            Assert.Contains("0 17.008 m 51.024 17.008 l s", green);

            // The answers are the next defaults: Extents and millimetres.
            Run(session, "PSOUT", At("again"));
            Assert.Equal("What to plot -- Display, Extents, Limits, View, or Window <E>:", session.PromptText);
            Run(session, Enter, Enter);
            Assert.Equal("Enter size units [Inches/Millimeters] <Millimeters>:", session.PromptText);
        }

        [Fact]
        public void PsOutWithFitAndASize_ScalesTheAreaToFitItAndCentresIt()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            // Inches, Fit, 10 by 4: the area 180 by 60 is limited by its width, 10 / 180 inch a unit, so 3.33 high, centred.
            Run(session, "PSOUT", At("fit"), "E", Enter, Enter, Enter, "10,4");

            Assert.False(session.IsRunning);
            Assert.Equal("Effective plotting area:  10.00 wide by 3.33 high", session.History.Last());
            List<string> lines = PostScript(At("fit.eps"));
            Assert.Contains("%%BoundingBox: 0 0 720 288", lines);
            string red = lines.Single(l => l.StartsWith("1 0 0 C", StringComparison.Ordinal));

            // The 240 points of the area's height are centred in the 288 of the box: 24 points from the bottom.
            Assert.Contains("0 24 m 720 24 l s", red);
        }

        [Fact]
        public void PsOut_WithNoSizeAndFit_FitsTheAreaToAnAnsiASheetTurnedToItsShape()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            // 180 by 60 in AutoCAD's USER size, 7.5 by 10.5 inches: its width limits it, 7.5 inches by 2.5, which is the
            // output. Fit lists the standard sizes before the size is asked, as AutoCAD's console does.
            Run(session, "PSOUT", At("sheet"), "E", Enter, Enter, Enter, Enter);

            Assert.Equal("Effective plotting area:  7.50 wide by 2.50 high", session.History.Last());
            Assert.Contains("Standard values for output size", session.History);
            Assert.Contains("A           8.00      10.50", session.History);
            Assert.Contains("A4          7.80      11.20", session.History);
            Assert.Contains("USER        7.50      10.50", session.History);
            Assert.Contains("%%BoundingBox: 0 0 540 180", PostScript(At("sheet.eps")));
        }

        [Fact]
        public void PsOut_AnswersAQuestionMarkWithTheHelpAndTheTable_AndRefusesWhatIsNoAnswer()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "PSOUT", At("help"), "X");
            Assert.Equal("What to plot -- Display, Extents, Limits, View, or Window <D>:", session.PromptText);
            Assert.Equal("Requires Display, Extents, Limits, View or Window.", session.History.Last());
            Run(session, "E", Enter, Enter, "?");
            Assert.Contains(session.History, l =>
                l.StartsWith("To specify an explicit scale", StringComparison.Ordinal)
            );
            Run(session, "two to one");
            Assert.Equal("Requires two positive numbers as P=D, or Fit.", session.History.Last());
            Run(session, "2=1", "?");
            Assert.Contains("Standard values for output size", session.History);
            Assert.Contains("B          10.00      16.00", session.History);
            Run(session, "wide");
            Assert.Equal("Invalid size label", session.History.Last());
            Run(session, "b");

            // 2 inches to a drawing unit makes 360 by 120 inches, in a sheet B of 10 by 16, centred and cut.
            Assert.False(session.IsRunning);
            Assert.Contains("%%BoundingBox: 0 0 720 1152", PostScript(At("help.eps")));
        }

        [Fact]
        public void PsOut_Window_TakesTheTwoCornersAndWritesOnlyWhatTheyHold()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "PSOUT", At("window"), "W");
            Assert.Equal("Specify first corner:", session.PromptText);
            Run(session, new GeoPoint2(0, 10), new GeoPoint2(100, 50), Enter, "M", "1=1", Enter);

            Assert.False(session.IsRunning);

            // The window is 100 by 40, and holds A1 alone (A2 lies along y = 80).
            List<string> lines = PostScript(At("window.eps"));
            Assert.Contains("%%BoundingBox: 0 0 284 114", lines);
            Assert.Single(lines, l => l.EndsWith(" s", StringComparison.Ordinal));
            Assert.Equal("Effective plotting area:  100.00 wide by 40.00 high", session.History.Last());
        }

        [Fact]
        public void PsOut_ASquareWindowOfNoSize_IsInvalid_AndWritesNothing()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "PSOUT", At("flat"), "W", new GeoPoint2(0, 10), new GeoPoint2(100, 10));

            Assert.False(session.IsRunning);
            Assert.Equal("Invalid window - output canceled.", session.History.Last());
            Assert.False(File.Exists(At("flat.eps")));
        }

        [Fact]
        public void PsOut_WritesTextInABaseFontAtItsPlaceAndAngle_EscapingItsParentheses_AndAFillAsEvenOdd()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = "a(b)\\c",
                    Height = 7.2,
                    Position = new GeoPoint3(20, 30, 0),
                    TextStyleName = "Standard",
                    Rotation = Math.PI / 2.0,
                }
            );
            database.ModelSpace.AppendEntity(
                new DxfSolid(
                    new GeoPoint3(100, 0, 0),
                    new GeoPoint3(120, 0, 0),
                    new GeoPoint3(100, 20, 0),
                    new GeoPoint3(120, 20, 0)
                )
                {
                    Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3),
                }
            );
            string path = At("words.dxf");
            database.Save(path);
            CadDocument document = CadDocument.Open(path);
            CadCommandSession session = Session(document);

            Run(session, "PSOUT", At("words"), "E", Enter, "M", "1=1", Enter);

            List<string> lines = PostScript(At("words.eps"));

            // A capital of 7.2 is an em of 10 units: 28.346 points at 1 millimetre to a unit. The text stands 20 and 30 units
            // in, less the area's lower left corner, in a box that the area centres exactly.
            string words = lines.Single(l => l.EndsWith("show grestore", StringComparison.Ordinal));
            Assert.Contains(
                "/Helvetica findfont [28.346 0 0 28.346 0 0] makefont setfont 0 0 m (a\\(b\\)\\\\c) show grestore",
                words
            );
            Assert.Contains(" 90 rotate ", words);
            Assert.StartsWith("0 0 0 C gsave ", words, StringComparison.Ordinal);

            // The solid is its four corners as a ring (the corners of a SOLID in the order of its drawing), filled even-odd, green.
            int fill = lines.IndexOf("eofill");
            Assert.True(fill > 2);
            Assert.StartsWith("0 1 0 C newpath", lines[fill - 2], StringComparison.Ordinal);
            Assert.Contains(" m ", lines[fill - 1]);
            Assert.EndsWith("c", lines[fill - 1], StringComparison.Ordinal);
        }

        [Fact]
        public void PsOut_Limits_AndAViewNotInTheDrawing_AreTakenFromTheDrawing()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            // A named view the drawing does not have: said, and nothing written.
            Run(session, "PSOUT", At("noview"), "V", "Nowhere");
            Assert.False(session.IsRunning);
            Assert.Equal("Invalid view - output canceled.", session.History.Last());
            Assert.False(File.Exists(At("noview.eps")));

            // The limits of the drawing, 0,0 to 420,297 (a metric drawing's), hold both lines.
            Run(session, "PSOUT", At("limits"), "L", Enter, "M", "1=1", Enter);
            List<string> lines = PostScript(At("limits.eps"));
            Assert.Contains("%%BoundingBox: 0 0 1191 842", lines);
            Assert.Equal(2, lines.Count(l => l.EndsWith(" s", StringComparison.Ordinal)));
        }

        [Fact]
        public void PsOut_AskstheSizeOfAScreenPreview_ForEpsiAndTiff_AndWritesNone()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "PSOUT", At("preview"), "E", "EPSI");
            Assert.Equal("Enter size of preview image (128x128 is standard) [128/256/512] <128>:", session.PromptText);
            Run(session, "256");
            Assert.Equal("The file holds no screen preview: the picture alone is written.", session.History.Last());
            Assert.Equal("Enter size units [Inches/Millimeters] <Inches>:", session.PromptText);
            Run(session, Enter, "1=1", Enter);
            Assert.DoesNotContain(File.ReadAllLines(At("preview.eps")), l =>
                l.StartsWith("%%BeginPreview", StringComparison.Ordinal)
            );
        }

        // SVGOUT

        [Fact]
        public void SvgOut_WritesTheObjectsAsAnSvgThatAnXmlParserReads_ColoursLinesAndWords()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var line = new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(190, 20, 0))
            {
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1),
            };
            database.ModelSpace.AppendEntity(line);
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = "R&D <1>",
                    Height = 7.2,
                    Position = new GeoPoint3(10, 50, 0),
                    TextStyleName = "Standard",
                }
            );
            string path = At("svg.dxf");
            database.Save(path);
            CadDocument document = CadDocument.Open(path);
            CadCommandSession session = Session(document);

            Run(session, "SVGOUT");
            Assert.Equal("Enter file name <" + At("svg.svg") + ">:", session.PromptText);
            Run(session, At("out"));
            Assert.Equal("Select objects or <all objects and viewports>:", session.PromptText);
            Run(session, Enter);

            XDocument xml = XDocument.Load(At("out.svg"));
            XNamespace ns = "http://www.w3.org/2000/svg";
            Assert.Equal(ns + "svg", xml.Root.Name);
            XElement polyline = Assert.Single(xml.Root.Elements(ns + "polyline"));
            Assert.Equal("#ff0000", polyline.Attribute("stroke").Value);
            Assert.Equal("none", polyline.Attribute("fill").Value);
            XElement text = Assert.Single(xml.Root.Elements(ns + "text"));
            Assert.Equal("R&D <1>", text.Value);
            Assert.Equal("Arial", text.Attribute("font-family").Value);
            Assert.Equal("10", text.Attribute("font-size").Value);

            // The box is 180 wide and as tall as the text reaches: the viewBox is in drawing units, the width in pixels.
            string[] box = xml.Root.Attribute("viewBox").Value.Split(' ');
            Assert.Equal("180", box[2]);
            double pixels = double.Parse(xml.Root.Attribute("width").Value, CultureInfo.InvariantCulture);
            Assert.Equal(1333.3333, pixels, 2);
        }

        // STLOUT

        [Fact]
        public void StlOut_AsksForSolids_AndRemovesWhatIsSelected_WritingNothing()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Session(document);

            Run(session, "STLOUT");
            Assert.Equal("Select solids or watertight meshes:", session.PromptText);
            Run(session, new[] { "A1", "A2" }, Enter);
            Assert.Equal("2 were filtered out", session.History.Last());

            // AutoCAD (probed) asks again, a lone object is "1 was filtered out", and Enter on no objects asks once more:
            // only Esc ends it.
            Assert.True(session.IsRunning);
            Assert.Equal("Select solids or watertight meshes:", session.PromptText);
            Run(session, new[] { "A1" }, Enter);
            Assert.Equal("1 was filtered out", session.History.Last());
            Run(session, Enter);
            Assert.True(session.IsRunning);
            Assert.Equal("Select solids or watertight meshes:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.Empty(Directory.GetFiles(_folder, "*.stl"));
        }

        // EXPORT

        private static CadCommandSession Stubbed(
            CadDocument document,
            string file,
            string type,
            List<CadExportJob> asked
        )
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "EXPORT")
                {
                    asked.Add(
                        new CadExportJob(e.Export.Title, e.Export.FileTypes, e.Export.FileType, e.Export.FileName)
                    );
                    e.Export.FileName = file;
                    e.Export.FileType = type;
                    e.Handled = true;
                }
            };
            return session;
        }

        [Fact]
        public void ExportMetafile_GoesOnAsWmfOut_WithTheFileChosen()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Stubbed(document, At("via.wmf"), "Metafile (*.wmf)", new List<CadExportJob>());

            Run(session, "EXPORT");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { "A1", "A2" }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(2, Records(File.ReadAllBytes(At("via.wmf"))).Count(r => r.Function == 0x0325));
        }

        [Fact]
        public void ExportEncapsulatedPs_GoesOnAsPsOutFromItsFirstQuestion()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Stubbed(
                document,
                At("via.eps"),
                "Encapsulated PS (*.eps)",
                new List<CadExportJob>()
            );

            Run(session, "EXPORT");
            Assert.Equal("What to plot -- Display, Extents, Limits, View, or Window <D>:", session.PromptText);
            Run(session, "E", Enter, "M", "1=10", Enter);

            Assert.Contains("%%BoundingBox: 0 0 52 18", PostScript(At("via.eps")));
        }

        [Fact]
        public void ExportLithography_GoesOnAsStlOut_AndRefusesWhatIsSelected()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Stubbed(
                document,
                At("via.stl"),
                "Lithography (*.stl)",
                new List<CadExportJob>()
            );

            Run(session, "EXPORT");
            Assert.Equal("Select solids or watertight meshes:", session.PromptText);
            Run(session, new[] { "A1" }, Enter);
            Assert.Equal("1 was filtered out", session.History.Last());
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.False(File.Exists(At("via.stl")));
        }

        [Fact]
        public void ExportBlock_GoesOnAsDashWblockAfterItsFile_WritingTheWholeDrawing()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Stubbed(document, At("via.dwg"), "Block (*.dwg)", new List<CadExportJob>());

            Run(session, "EXPORT");
            Assert.Equal(
                "Enter name of existing block or [= (block=output file)/* (whole drawing)] <define new drawing>:",
                session.PromptText
            );
            Run(session, "*");

            Assert.False(session.IsRunning);
            byte[] dwg = File.ReadAllBytes(At("via.dwg"));
            Assert.Equal("AC1032", Encoding.ASCII.GetString(dwg, 0, 6));
        }

        // DXX

        /// <summary>The groups of a DXF file with no sections, as (code, value) pairs: what a DXX extract is.</summary>
        private static List<KeyValuePair<int, string>> Groups(string path)
        {
            string[] lines = File.ReadAllText(path).Split(new[] { "\r\n" }, StringSplitOptions.None);
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                groups.Add(
                    new KeyValuePair<int, string>(
                        int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture),
                        lines[i + 1]
                    )
                );
            }

            return groups;
        }

        private CadDocument Parts(out List<string> handles)
        {
            DxfDatabase db = CadTemplate.CreateMetric();
            var block = new DxfBlockTableRecord { Name = "ITEM", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5.0));
            block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, 0, 0), string.Empty, "NAME", "NAME", null));
            var made = new List<DxfBlockReference>();

            foreach (string name in new[] { "Bolt M8", "Nut" })
            {
                var insert = new DxfBlockReference(new GeoPoint3(made.Count * 20.0, 20, 0), block);
                db.ModelSpace.AppendEntity(insert);
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(block.OfType<DxfAttributeDefinition>().First(), insert.BlockTransform);
                attribute.TextString = name;
                insert.AttributeCollection.AppendAttribute(attribute);
                made.Add(insert);
            }

            handles = made.Select(m => m.Handle).ToList();
            string path = At("parts.dxf");
            db.Save(path);
            return CadDocument.Open(path);
        }

        [Fact]
        public void ExportDxxExtract_WritesTheBlocksSelectedWithTheirAttributesAsDxfGroups()
        {
            CadDocument document = Parts(out List<string> handles);
            CadCommandSession session = Stubbed(
                document,
                At("via.dxx"),
                "DXX Extract (*.dxx)",
                new List<CadExportJob>()
            );

            Run(session, "EXPORT");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { handles[1] }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal("3 objects in extract file.", session.History.Last());

            // The extract is DXF groups with no sections: INSERT ITEM at (20, 20), its ATTRIB NAME "Nut", its SEQEND, EOF.
            List<KeyValuePair<int, string>> groups = Groups(At("via.dxx"));
            Assert.Equal(
                new[] { "INSERT", "ATTRIB", "SEQEND", "EOF" },
                groups.Where(g => g.Key == 0).Select(g => g.Value).ToArray()
            );
            Assert.Equal("ITEM", groups.Single(g => g.Key == 2 && g.Value == "ITEM").Value);
            Assert.Equal(20.0, double.Parse(groups.First(g => g.Key == 10).Value, CultureInfo.InvariantCulture), 6);
            Assert.Equal("Nut", groups.Single(g => g.Key == 1).Value);
            Assert.Equal("NAME", groups.Single(g => g.Key == 2 && g.Value == "NAME").Value);
        }

        [Fact]
        public void ExportATypeWithNoWriter_StillSaysSoAndWritesNothing_ForIges()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Stubbed(
                document,
                At("plan.igs"),
                "IGES (*.iges;*.igs)",
                new List<CadExportJob>()
            );

            Run(session, "EXPORT");

            Assert.False(session.IsRunning);
            Assert.Contains(session.History, l =>
                l.StartsWith("IGES (*.iges;*.igs) has no writer in the presenter", StringComparison.Ordinal)
            );
            Assert.Empty(Directory.GetFiles(_folder, "*.igs"));
        }

        // The picture the writers share.

        [Fact]
        public void TheVectorPicture_TurnsWhiteBlack_KeepsAMaskWhite_AndPutsTheLinesOfATextBelowOneAnother()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0))
                {
                    Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7),
                }
            );
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = "one",
                    Height = 8.0,
                    Position = new GeoPoint3(0, 20, 0),
                    TextStyleName = "Standard",
                }
            );
            string path = At("picture.dxf");
            database.Save(path);
            CadDocument document = CadDocument.Open(path);

            CadVectorPicture picture = CadVectorPicture.Of(document, document.ModelSpace, null, DisplayBox.Empty);

            CadVectorStroke stroke = Assert.IsType<CadVectorStroke>(picture.Items[0]);
            Assert.Equal(0x000000, stroke.Rgb);
            CadVectorText text = Assert.IsType<CadVectorText>(picture.Items[1]);
            Assert.Equal("one", text.Text);
            Assert.Equal(8.0 / 0.72, text.Em, 6);
            Assert.False(picture.IsEmpty);
            Assert.Equal(0.0, picture.Box.MinX, 6);
            Assert.Equal(0.0, picture.Box.MinY, 6);
            Assert.InRange(picture.Box.MaxX, 10.0, 20.0);
            Assert.Equal(0, picture.Skipped);
        }

        [Fact]
        public void TheVectorPicture_OfAnEmptyWindow_IsEmpty_AndTheWritersRefuseIt()
        {
            CadDocument document = TwoLines();
            CadVectorPicture picture = CadVectorPicture.Of(
                document,
                document.ModelSpace,
                null,
                new DisplayBox(500, 500, 600, 600)
            );

            Assert.True(picture.IsEmpty);
            Assert.Throws<ArgumentException>(() => CadWmfWriter.Write(picture));
            Assert.Throws<ArgumentException>(() => CadSvgWriter.Write(picture));
        }
    }
}
