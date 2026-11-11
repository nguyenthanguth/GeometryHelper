using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The lines a HATCH takes from the caller's own pattern files (<see cref="DxfDatabase.PatternSearchPath"/>, check P4c):
    /// pattern files written by the tests, of patterns of their own, so that the rules AutoCAD 2021 reads and places them by
    /// (probes GHP4CR, GHP4CS and GHP4CT, on these same patterns) are asserted: acadiso.pat for a drawing of millimetres and
    /// acad.pat for one of inches, a file named after the pattern, which file first, how a line is read and when a pattern
    /// is refused, and each line turned and scaled with the HATCH.
    /// </summary>
    public class DxfHatchPatternFileTests
    {
        private const double Near = 1E-9;

        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        private static readonly GeoPoint2[] Square =
        {
            new GeoPoint2(0, 0),
            new GeoPoint2(30, 0),
            new GeoPoint2(30, 20),
            new GeoPoint2(0, 20),
        };

        /// <summary>A folder of its own holding the pattern files given, each a name and its text; deleted after the test.</summary>
        private static void InFolder(Action<string> test, params string[] files)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-patterns-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                for (int i = 0; i + 1 < files.Length; i += 2)
                {
                    File.WriteAllBytes(Path.Combine(folder, files[i]), Encoding.ASCII.GetBytes(files[i + 1]));
                }

                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>A new drawing of R2018 of the measurement given, written in its header, its patterns looked for in the folder given.</summary>
        private static DxfDatabase Drawing(string folder, DxfMeasurementValue measurement = DxfMeasurementValue.Metric)
        {
            var db = new DxfDatabase("AC1032");
            db.Measurement =
                measurement == DxfMeasurementValue.Metric ? DxfMeasurementValue.English : DxfMeasurementValue.Metric;
            db.Measurement = measurement;
            db.PatternSearchPath = folder;
            return db;
        }

        /// <summary>A HATCH on the square 30 by 20 added to model space, then given the pattern.</summary>
        private static DxfHatch Hatch(
            DxfDatabase db,
            string name,
            DxfHatchPatternType type = DxfHatchPatternType.PreDefined
        )
        {
            var hatch = new DxfHatch();
            hatch.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            db.ModelSpace.AppendEntity(hatch);
            hatch.SetHatchPattern(type, name);
            return hatch;
        }

        private static void Is(double expected, double actual) =>
            Assert.True(
                Math.Abs(expected - actual) <= Near,
                "expected "
                    + expected.ToString("R", CultureInfo.InvariantCulture)
                    + ", got "
                    + actual.ToString("R", CultureInfo.InvariantCulture)
            );

        /// <summary>A line of the pattern is the one given: its angle in degrees, base point, offset and dashes.</summary>
        private static void Line(
            DxfHatch hatch,
            int index,
            double degrees,
            double baseX,
            double baseY,
            double offsetX,
            double offsetY,
            params double[] dashes
        )
        {
            DxfPatternDefinition line = hatch.GetPatternDefinitionAt(index);
            Is(degrees, line.Angle * 180.0 / Math.PI);
            Assert.True(
                new GeoPoint2(baseX, baseY).IsEqualTo(new GeoPoint2(line.BaseX, line.BaseY), Close),
                "base (" + line.BaseX + ", " + line.BaseY + ")"
            );
            Assert.True(
                new GeoPoint2(offsetX, offsetY).IsEqualTo(new GeoPoint2(line.OffsetX, line.OffsetY), Close),
                "offset (" + line.OffsetX + ", " + line.OffsetY + ")"
            );
            Assert.Equal(dashes.Length, line.GetDashes().Count);

            for (int i = 0; i < dashes.Length; i++)
            {
                Is(dashes[i], line.GetDashes()[i]);
            }
        }

        /// <summary>The messages logged while an action runs, every level, each after its level.</summary>
        private static List<string> Logged(Action action)
        {
            var written = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            bool enabled = GeometryHelperLog.Enable;

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, exception) => written.Add(level + " " + message);
                action();
            }
            finally
            {
                GeometryHelperLog.Writer = before;
                GeometryHelperLog.Enable = enabled;
            }

            return written;
        }

        // ---- the lines written ------------------------------------------------------------------------------------------------

        [Fact]
        public void APatternOfTheDrawingsFile_IsGivenItsLines_AsAutoCadGivesThem()
        {
            // AutoCAD 2021 (GHP4CR, GHE): "45, 1, 2, 0, 3, 1.5, -.5, 0, -.5" is the line at 45 degrees from (1, 2), offset
            // (-2.121, 2.121), its dashes as written; (GHP4CS, XK) a line of -30 degrees is one of 330.
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    DxfHatch hatch = Hatch(db, "GHE");

                    Assert.Equal(3, hatch.NumberOfPatternDefinitions);
                    Line(hatch, 0, 0, 0, 0, 0, 2);
                    Line(hatch, 1, 45, 1, 2, -2.1213203435596424, 2.1213203435596424, 1.5, -0.5, 0, -0.5);
                    Line(hatch, 2, 90, 0, 0, -4, 0);
                    Assert.Equal("GHE", hatch.PatternName);
                    Assert.Equal(DxfHatchPatternType.PreDefined, hatch.PatternType);

                    DxfHatch turned = Hatch(db, "XK");
                    Line(turned, 0, 0, 0, 0, 0, 2);
                    Line(turned, 1, 330, 1, 1, 1, 1.7320508075688772);
                },
                "acadiso.pat",
                ";; the tests' own patterns\r\n;;\r\n\r\n*GHE\r\n; a comment line inside\r\n0, 0,0, 0,2 ; an inline comment\r\n\r\n   45 ,  1 , 2 ,  0 , 3 , 1.5, -.5, 0, -.5\r\n\t90,\t0,0,0,4\r\n*XK, k\r\n720, 0,0, 0,2\r\n-30, 1,1, 0,2\r\n"
            );
        }

        [Fact]
        public void ThePatternsScaleAndAngle_TurnAndScaleItsLinesAboutTheOrigin_WrittenAnewFromTheFile()
        {
            // AutoCAD 2021 (GHP4CS, XI "0, 0,0, 0,2, -1, 1"): scale 3 and angle 90 give 90 (0, 0) (-6, 0) [-3, 3]; scale 1 and
            // 400 degrees give 40 (0, 0) (-1.286, 1.532) [-1, 1]; -30 degrees give 330 (0, 0) (1, 1.732) [-1, 1]. Scale 2.5 and
            // -45 degrees turn a line at 45 degrees from (2, 0) to one at 0 from (3.536, -3.536), as AutoCAD turns ANSI33's (GHP4CR).
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    DxfHatch hatch = Hatch(db, "XI");

                    hatch.PatternScale = 3;
                    Line(hatch, 0, 0, 0, 0, 0, 6, -3, 3);
                    hatch.PatternAngle = Math.PI / 2;
                    Line(hatch, 0, 90, 0, 0, -6, 0, -3, 3);
                    hatch.PatternScale = 1;
                    hatch.PatternAngle = 400 * Math.PI / 180;
                    Line(hatch, 0, 40, 0, 0, -1.2855752193730785, 1.5320888862379562, -1, 1);
                    hatch.PatternAngle = -30 * Math.PI / 180;
                    Line(hatch, 0, 330, 0, 0, 1, 1.7320508075688772, -1, 1);

                    // A pattern given twice: AutoCAD (GHP4CR) gives a pattern of a file no more lines.
                    hatch.PatternDouble = true;
                    Assert.Equal(1, hatch.NumberOfPatternDefinitions);

                    DxfHatch second = Hatch(db, "GHT");
                    second.PatternScale = 2.5;
                    second.PatternAngle = -Math.PI / 4;
                    Line(second, 0, 0, 0, 0, 0, 7.5);
                    Line(second, 1, 0, 3.5355339059327378, -3.5355339059327378, 0, 7.5, 2.5);
                },
                "acadiso.pat",
                "*XI, i\r\n0, 0,0, 0,2, -1, 1\r\n*GHT, t\r\n45, 0,0, 0,3\r\n45, 2,0, 0,3, 1\r\n"
            );
        }

        [Fact]
        public void APatternOfTheFile_DrawsAsTheSamePatternMadeByTheUser()
        {
            // "45, 0,0, 0,3" is the one line AutoCAD makes of a pattern of the user's at 45 degrees spaced 3 (point 35).
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    DxfHatch filed = Hatch(db, "GH45");
                    DxfHatch user = Hatch(db, "_USER", DxfHatchPatternType.UserDefined);
                    user.PatternAngle = Math.PI / 4;
                    user.PatternSpace = 3;

                    IReadOnlyList<GeoLine2> lines = filed.GetHatchLinesData();
                    IReadOnlyList<GeoLine2> made = user.GetHatchLinesData();
                    Assert.NotEmpty(lines);
                    Assert.Equal(made.Count, lines.Count);

                    for (int i = 0; i < lines.Count; i++)
                    {
                        Assert.True(made[i].IsEqualTo(lines[i], Close), made[i] + " / " + lines[i]);
                    }

                    Assert.Equal(lines.Count, filed.Explode().Count);
                },
                "acadiso.pat",
                "*GH45, test\r\n45, 0,0, 0,3\r\n"
            );
        }

        // ---- which file -------------------------------------------------------------------------------------------------------

        [Fact]
        public void ADrawingOfInches_TakesAcadPat_AndOneOfMillimetresAcadisoPat()
        {
            // AutoCAD 2021 (GHP4CS, GHZ): acadiso.pat for $MEASUREMENT 1, acad.pat for 0, the first pattern of a name taken.
            InFolder(
                folder =>
                {
                    Line(Hatch(Drawing(folder, DxfMeasurementValue.Metric), "GHZ"), 0, 0, 0, 0, 0, 2);
                    Line(Hatch(Drawing(folder, DxfMeasurementValue.English), "GHZ"), 0, 0, 0, 0, 0, 2.5);

                    // A header of no $MEASUREMENT is of millimetres to the library (Measurement).
                    var none = new DxfDatabase("AC1032") { PatternSearchPath = folder };
                    Line(Hatch(none, "GHZ"), 0, 0, 0, 0, 0, 2);
                },
                "acadiso.pat",
                "*GHZ, first\r\n0, 0,0, 0,2\r\n*GHZ, second\r\n90, 0,0, 0,5\r\n",
                "acad.pat",
                "*GHZ, first\r\n0, 0,0, 0,2.5\r\n"
            );
        }

        [Fact]
        public void APatternOfTheDrawingsFileIsLookedForThereFirst_ACustomOneInItsOwnFileFirst_EachTypedByTheFileItIsIn()
        {
            // AutoCAD 2021 (GHP4CT, GHY and GOOD; GHP4CR, GHA and ANSI31): a pattern of AutoCAD's file is looked for in it, then
            // in a file of its name; a custom one the other way round; each written of the type of the file it is found in and
            // its name in capitals; a pattern refused in the first file that has it is not looked for in the other (BAD).
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    DxfHatch predefined = Hatch(db, "ghy");
                    DxfHatch custom = Hatch(db, "GHY", DxfHatchPatternType.CustomDefined);
                    DxfHatch own = Hatch(db, "GHA");
                    DxfHatch filed = Hatch(db, "GH1", DxfHatchPatternType.CustomDefined);

                    Line(predefined, 0, 0, 0, 0, 0, 2);
                    Assert.Equal("GHY", predefined.PatternName);
                    Assert.Equal(DxfHatchPatternType.PreDefined, predefined.PatternType);
                    Line(custom, 0, 0, 0, 0, 0, 7);
                    Assert.Equal(DxfHatchPatternType.CustomDefined, custom.PatternType);
                    Line(own, 0, 0, 0, 0, 0, 4);
                    Assert.Equal(DxfHatchPatternType.CustomDefined, own.PatternType);
                    Line(filed, 0, 0, 0, 0, 0, 3);
                    Assert.Equal(DxfHatchPatternType.PreDefined, filed.PatternType);
                    Assert.Equal(0, Hatch(db, "BAD").NumberOfPatternDefinitions);
                    Line(Hatch(db, "BAD", DxfHatchPatternType.CustomDefined), 0, 0, 0, 0, 0, 9);
                },
                "acadiso.pat",
                "*BAD, refused\r\nbroken\r\n*GHY, in acadiso\r\n0, 0,0, 0,2\r\n*GH1\r\n0,0,0,0,3\r\n",
                "GHY.pat",
                "*GHY, beside\r\n0, 0,0, 0,7\r\n",
                "GHA.pat",
                "*GHA, custom\r\n0, 0,0, 0,4\r\n",
                "BAD.pat",
                "*BAD\r\n0,0,0,0,9\r\n"
            );
        }

        [Fact]
        public void EachFile_IsTakenFromTheFirstFolderThatHasIt()
        {
            // AutoCAD 2021 (GHP4CS): an acadiso.pat of the current folder, lacking ANSI33, is taken before the support path's,
            // which has it, and ANSI33 is not found.
            InFolder(
                first =>
                    InFolder(
                        second =>
                        {
                            DxfDatabase db = Drawing(first + ";" + second);
                            Line(Hatch(db, "GHF"), 0, 0, 0, 0, 0, 2);
                            Assert.Equal(0, Hatch(db, "GHS").NumberOfPatternDefinitions);
                            Line(Hatch(db, "GHO"), 0, 0, 0, 0, 0, 5);
                        },
                        "acadiso.pat",
                        "*GHF\r\n0,0,0,0,3\r\n*GHS\r\n0,0,0,0,3\r\n",
                        "GHO.pat",
                        "*GHO\r\n0,0,0,0,6\r\n"
                    ),
                "acadiso.pat",
                "*GHF\n0,0,0,0,2\n",
                "GHO.pat",
                "*GHO\r\n0,0,0,0,5\r\n"
            );
        }

        // ---- how a file is read -----------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("*P\r\n0, 0,0, 0,2,\r\n", "0 0 0 0 2 0")] // a comma at the end, a dash of nought (GHJ)
        [InlineData("*P\r\n0, 0,0, 0,2,  \r\n", "0 0 0 0 2 0")] // and blanks after it (TC)
        [InlineData("*P\r\n0, 0,0, 0,2,,\r\n", "0 0 0 0 2 0")] // two, one dash (TD)
        [InlineData("*P\r\n0,0,0,0,2,,1\r\n", "0 0 0 0 2 1")] // commas running together, one (TV)
        [InlineData("*P\r\n0,0,0,0,2 1\r\n", "0 0 0 0 2")] // a number ended by a blank, the rest passed over (TG)
        [InlineData("*P\r\n0, 0,0, 0,2;5\r\n", "0 0 0 0 2")] // a comment in a number (GHT)
        [InlineData("*P\r\n+30, .5,-.5, 1e0,2.5E0, 1E-1,-0.1\r\n", "30 0.5 -0.5 1 2.5 0.1 -0.1")] // (GHH)
        [InlineData("*P\n0, 0,0, 0,2, 1,-1\n90, 0,0, 0,2\n", "0 0 0 0 2 1 -1|90 0 0 0 2")] // lines ended by LF alone (GHF)
        [InlineData("*P\r\n0,0,0,0,2\r\n   \r\n\t\r\n90,0,0,0,3\r\n", "0 0 0 0 2|90 0 0 0 3")] // lines of blanks (TE)
        [InlineData("*P\r\n0,0,0,0,10,1,-1,1,-1,1,-1\r\n", "0 0 0 0 10 1 -1 1 -1 1 -1")] // six dashes (D6)
        [InlineData("*P  \r\n0,0,0,0,2\r\n", "0 0 0 0 2")] // blanks after the name (TS)
        [InlineData("*P;comment\r\n0, 0,0, 0,2\r\n", "0 0 0 0 2")] // a comment after the name (XC)
        [InlineData("*p, lower case\r\n0, 0,0, 0,2\r\n", "0 0 0 0 2")] // the name in another case (GHK)
        [InlineData("*P,x\r\n0,0,0,0,2\r\n*P,second\r\nbroken\r\n", "0 0 0 0 2")] // the first of a name (TH)
        [InlineData("*P, d\r\n0, 0,0, 0,2\r\n;\r\n*\r\n90, 0,0, 0,3\r\n", "0 0 0 0 2")] // a name of none ends it (XD)
        [InlineData("*P\r\n0,0,0,0,10,1,-1,1,-1,1,-1,1\r\n", null)] // seven dashes (D7)
        [InlineData("*P\r\n0,0,0,0,2,1,-1,1,-1,1,-1,\r\n", null)] // six and a comma at the end (TL)
        [InlineData("*P\r\n0, 0,0, 0\r\n90, 0,0, 0,2\r\n", null)] // four numbers (GHI)
        [InlineData("*P\r\n0, 0,0, 0,2x\r\n", null)] // a number not ended by a blank (X2)
        [InlineData("*P\r\nabc, 0,0, 0,2\r\n", null)] // a word (XA)
        [InlineData("*P\r\n0, ,0, 0,2\r\n", null)] // a blank between commas (XE)
        [InlineData("*P\r\n0 0 0 0 2\r\n", null)] // no commas (XS)
        [InlineData("*P\r\n1e400, 0,0, 0,2\r\n", null)] // a number too great (XF)
        [InlineData("*P, none\r\n*Q, next\r\n0,0,0,0,2\r\n", null)] // no line (GHL)
        [InlineData("*P,x\r\nbroken\r\n*P,second\r\n0,0,0,0,2\r\n", null)] // the first of a name refused (TI)
        public void APatternFile_IsReadAsAutoCadReadsIt(string text, string expected)
        {
            Dictionary<string, List<double[]>> patterns = DxfPatterns.Read(Encoding.ASCII.GetBytes(text));
            Assert.True(patterns.TryGetValue("P", out List<double[]> lines));

            if (expected == null)
            {
                Assert.Null(lines);
                return;
            }

            string[] want = expected.Split('|');
            Assert.Equal(want.Length, lines.Count);

            for (int i = 0; i < want.Length; i++)
            {
                double[] numbers = want[i]
                    .Split(' ')
                    .Select(s => double.Parse(s, CultureInfo.InvariantCulture))
                    .ToArray();
                Assert.Equal(numbers.Length, lines[i].Length);

                for (int k = 0; k < numbers.Length; k++)
                {
                    Is(numbers[k], lines[i][k]);
                }
            }
        }

        [Theory]
        [InlineData("*P , blanks before the comma\r\n0, 0,0, 0,2\r\n")] // named "P " (GHR)
        [InlineData("  *P, indented\r\n0, 0,0, 0,2\r\n")] // no name (GHS)
        [InlineData("*P\tx\r\n0,0,0,0,2\r\n")] // named "P\tx" (TT)
        [InlineData("﻿*P, after a mark of UTF-8\r\n0, 0,0, 0,2\r\n")] // no name (XL)
        public void ANameNotWrittenAsAutoCadReadsIt_IsNotFound(string text)
        {
            Assert.False(DxfPatterns.Read(Encoding.UTF8.GetBytes(text)).ContainsKey("P"));
        }

        // ---- what is not given lines --------------------------------------------------------------------------------------------

        [Fact]
        public void APatternInNoFile_OrRefused_IsLeftWithNoLines_AndSaidOnce()
        {
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    List<string> logged = Logged(() =>
                    {
                        foreach (string name in new[] { "NOSUCH", "NOSUCH", "GHI", "GHI" })
                        {
                            DxfHatch hatch = Hatch(db, name);
                            Assert.Equal(0, hatch.NumberOfPatternDefinitions);
                            Assert.Equal(name, hatch.PatternName);
                            Assert.Throws<NotSupportedException>(() => hatch.Explode());
                        }
                    });

                    Assert.Single(logged, m => m.StartsWith("Debug", StringComparison.Ordinal) && m.Contains("NOSUCH"));
                    Assert.Single(logged, m => m.StartsWith("Warn", StringComparison.Ordinal) && m.Contains("GHI"));
                    Assert.Equal(2, logged.Count);
                },
                "acadiso.pat",
                "*GHI, a short line\r\n0, 0,0, 0\r\n"
            );
        }

        [Fact]
        public void WithNoFolders_NothingIsGivenLines_AndTheDrawingIsWrittenAsBefore()
        {
            // A drawing of folders that hold no pattern file is written as one of none.
            InFolder(folder =>
            {
                var texts = new List<string>();

                foreach (string path in new[] { string.Empty, folder, " ; " })
                {
                    var db = new DxfDatabase("AC1032") { PatternSearchPath = path };
                    List<string> logged = Logged(() =>
                    {
                        DxfHatch hatch = Hatch(db, "ANSI31");
                        hatch.PatternScale = 2;
                        hatch.PatternAngle = 0.5;
                        Assert.Equal(0, hatch.NumberOfPatternDefinitions);
                    });

                    Assert.Equal(path.Length == 0 ? 0 : 1, logged.Count);
                    texts.Add(db.ToText());
                }

                Assert.Equal(texts[0], texts[1]);
                Assert.Equal(texts[0], texts[2]);
            });

            Assert.Throws<ArgumentNullException>(() => new DxfDatabase("AC1032").PatternSearchPath = null);
        }

        // ---- when the lines are given ---------------------------------------------------------------------------------------------

        [Fact]
        public void AHatchGivenItsPatternBeforeItIsAdded_IsGivenItsLinesWhenAdded_ButACopyOfOneOfNoLinesIsNot()
        {
            // AutoCAD 2021 (GHP4CR) gives a HATCH in no drawing its lines at once, from the working drawing's file.
            InFolder(
                folder =>
                {
                    DxfDatabase db = Drawing(folder);
                    var hatch = new DxfHatch();
                    hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "xi");
                    hatch.AppendLoop(DxfHatchLoopTypes.External, Square, null);
                    hatch.PatternScale = 3;
                    hatch.PatternAngle = Math.PI / 2;
                    Assert.Equal(0, hatch.NumberOfPatternDefinitions);
                    var copy = (DxfHatch)hatch.Clone();

                    db.ModelSpace.AppendEntity(hatch);
                    Line(hatch, 0, 90, 0, 0, -6, 0, -3, 3);
                    Assert.Equal("XI", hatch.PatternName);

                    db.ModelSpace.AppendEntity(copy);
                    Assert.Equal(0, copy.NumberOfPatternDefinitions);
                },
                "acadiso.pat",
                "*XI, i\r\n0, 0,0, 0,2, -1, 1\r\n"
            );
        }

        [Fact]
        public void AHatchReadFromAFile_KeepsTheLinesItHolds_UntilItsPatternIsGivenAgain()
        {
            InFolder(
                other =>
                    InFolder(
                        folder =>
                        {
                            // Written with no folders, GHZ of no lines; with another file, XI of its lines.
                            var made = new DxfDatabase("AC1032");
                            string none = Hatch(made, "GHZ").Handle;
                            made.PatternSearchPath = other;
                            string held = Hatch(made, "XI").Handle;

                            DxfDatabase db = DxfDatabase.Parse(made.ToText());
                            db.PatternSearchPath = folder;
                            DxfHatch unfilled = DxfModel.ByHandle<DxfHatch>(db, none);
                            DxfHatch kept = DxfModel.ByHandle<DxfHatch>(db, held);
                            Assert.Equal(0, unfilled.NumberOfPatternDefinitions);
                            Line(kept, 0, 0, 0, 0, 0, 1);

                            // Given again, the one of no lines is given them; the one of lines keeps them, as a property set to the value it has.
                            unfilled.SetHatchPattern(DxfHatchPatternType.PreDefined, "GHZ");
                            kept.SetHatchPattern(DxfHatchPatternType.PreDefined, "XI");
                            Line(unfilled, 0, 0, 0, 0, 0, 2);
                            Line(kept, 0, 0, 0, 0, 0, 1);

                            // Its scale set, the lines are written anew from the file.
                            kept.PatternScale = 2;
                            Line(kept, 0, 0, 0, 0, 0, 4, -2, 2);
                        },
                        "acadiso.pat",
                        "*GHZ\r\n0, 0,0, 0,2\r\n*XI, i\r\n0, 0,0, 0,2, -1, 1\r\n"
                    ),
                "acadiso.pat",
                "*XI, another\r\n0, 0,0, 0,1\r\n"
            );
        }

        // ---- files that are no pattern files ------------------------------------------------------------------------------------

        [Fact]
        public void EveryPatternFileCutShort_OrOfABytePutWrong_IsReadOrRefused_AndNeverThrows()
        {
            byte[] whole = Encoding.ASCII.GetBytes(
                ";; header\r\n*GHE, e\r\n0, 0,0, 0,2 ; c\r\n\r\n45, 1, 2, 0, 3, 1.5, -.5, 0, -.5\r\n*XK\r\n720, 0,0, 0,2\r\n-30, 1,1, 0,2\r\n*GHF,lf\n0, 0,0, 0,2, 1,-1\n90, 0,0, 0,2\n"
            );
            var random = new Random(20261104);
            Assert.Equal(3, DxfPatterns.Read(whole).Count);

            for (int length = 0; length <= whole.Length; length++)
            {
                Read(whole.Take(length).ToArray());
            }

            for (int round = 0; round < 3000; round++)
            {
                byte[] wrong = (byte[])whole.Clone();

                for (int k = random.Next(1, 4); k > 0; k--)
                {
                    wrong[random.Next(wrong.Length)] = (byte)random.Next(256);
                }

                Read(wrong);
            }

            // Through a drawing too: a file of every byte, and one that is a folder.
            var bytes = new byte[4096];
            random.NextBytes(bytes);
            InFolder(folder =>
            {
                Directory.CreateDirectory(Path.Combine(folder, "GHD.pat"));
                File.WriteAllBytes(Path.Combine(folder, "acadiso.pat"), bytes);
                DxfDatabase db = Drawing(folder);

                foreach (string name in new[] { "GHE", "GHD", "A:B", "..", new string('x', 300) })
                {
                    Assert.Equal(0, Hatch(db, name).NumberOfPatternDefinitions);
                }
            });

            void Read(byte[] file)
            {
                foreach (List<double[]> lines in DxfPatterns.Read(file).Values)
                {
                    Assert.True(
                        lines == null
                            || (
                                lines.Count > 0
                                && lines.All(l => l.Length >= 5 && l.Length <= 11 && l.All(Guard.IsFinite))
                            )
                    );
                }
            }
        }
    }
}
