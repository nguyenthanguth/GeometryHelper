using System;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfLines;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// TEXT and MTEXT read as <see cref="DxfText"/>: where each stands by its alignment, its words with their codes made
    /// plain, its plane, through blocks, and written back by <see cref="DxfWriter"/>.
    /// </summary>
    public class DxfReaderTextTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static DxfDocument Read(string text, bool planAs2D = false) => DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = planAs2D, Tolerance = Tol });

        private static DxfText Only(DxfDocument read) => Assert.IsType<DxfText>(Assert.Single(read.Shapes).Shape);

        private static void Same(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.Equal(expected.X, actual.X);
            Assert.Equal(expected.Y, actual.Y);
            Assert.Equal(expected.Z, actual.Z);
        }

        private static void Same(GeoVector3 expected, GeoVector3 actual)
        {
            Assert.Equal(expected.X, actual.X);
            Assert.Equal(expected.Y, actual.Y);
            Assert.Equal(expected.Z, actual.Z);
        }

        #region Reading

        [Theory]
        [InlineData(0, 0, 1.0)]
        [InlineData(1, 0, 5.0)]
        [InlineData(2, 0, 5.0)]
        [InlineData(3, 0, 1.0)]
        [InlineData(4, 0, 5.0)]
        [InlineData(5, 0, 1.0)]
        [InlineData(0, 1, 5.0)]
        [InlineData(0, 2, 5.0)]
        [InlineData(0, 3, 5.0)]
        [InlineData(2, 3, 5.0)]
        public void ATextAlignedLeftOrAlignedOrFittedOnItsBaseline_StandsAtItsFirstPoint_AndOneAlignedAnyOtherWayAtItsSecond(int horizontal, int vertical, double x)
        {
            DxfText text = Only(Read(R2000(G(0, "TEXT", 8, "0", 10, "1", 20, "2", 30, "0", 40, "2.5", 1, "A", 72, horizontal, 11, "5", 21, "6", 31, "0", 73, vertical))));

            Same(new GeoPoint3(x, x == 1.0 ? 2.0 : 6.0, 0.0), text.Position);
            Assert.False(text.IsMultiline);
            Assert.Equal(2.5, text.Height);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        public void AnAlignedOrFittedText_RunsFromItsFirstPointToItsSecond_AndStandsAtTheFirst(int horizontal)
        {
            // ezdxf places an ALIGNED or FIT text from its insert (10) to its align point (11): the text from (0, 0) to (100, 0).
            DxfText text = Only(Read(R2000(G(0, "TEXT", 8, "0", 10, "0", 20, "0", 30, "0", 40, "2.5", 1, "ALIGNED TEXT", 72, horizontal, 11, "100", 21, "0", 31, "0", 100, "AcDbText", 73, 0))));

            Same(new GeoPoint3(0.0, 0.0, 0.0), text.Position);
        }

        [Fact]
        public void ATextAlignedOtherwiseWithoutItsSecondPoint_StandsAtItsFirst()
        {
            DxfText text = Only(Read(R2000(G(0, "TEXT", 8, "0", 10, "1", 20, "2", 30, "0", 40, "2.5", 1, "A", 72, 1))));

            Same(new GeoPoint3(1.0, 2.0, 0.0), text.Position);
        }

        [Fact]
        public void TheSignsOfAText_AreTheirCharacters_InEitherCase_AndItsEscapesItsLetters()
        {
            DxfText text = Only(Read(R2000(G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, "%%c25 %%C %%d%%D %%p%%P Caf\\U+00E9 50%"))));

            Assert.Equal("\u230025 \u2300 \u00B0\u00B0 \u00B1\u00B1 Caf\u00E9 50%", text.Value);
            Assert.Equal(1.0, text.WidthFactor);
            Assert.Equal(0.0, text.Width);
            Assert.Equal(0.0, text.Rotation);
            Same(new GeoVector3(0.0, 0.0, 1.0), text.Normal);
        }

        [Theory]
        [InlineData("a\\Pb", "a\nb")]
        [InlineData("\\Lunder\\lline", "underline")]
        [InlineData("\\Ostrike\\o and \\Kk\\k", "strike and k")]
        [InlineData("{\\fArial|b1;first}\\Psecond", "first\nsecond")]
        [InlineData("\\H2.5x;tall \\C1;red \\W0.8;wide \\T2;spaced \\Q15;slant", "tall red wide spaced slant")]
        [InlineData("\\pxi-3,l3;item", "item")]
        [InlineData("1\\~2", "1 2")]
        [InlineData("\\\\ \\{ \\}", "\\ { }")]
        [InlineData("\\S1/2; and \\S3^4;", "1/2 and 3/4")]
        [InlineData("x\\U+00E9", "x\u00E9")]
        [InlineData("a\\", "a\\")]
        [InlineData("45%%d", "45\u00B0")]
        [InlineData("Col one\\NCol two", "Col one Col two")]
        [InlineData("A\\NB\\H2;C", "A BC")]
        [InlineData("big\\M+1A4A1end", "big\\M+1A4A1end")]
        [InlineData("\\H2.5x tall", "\\H2.5x tall")]
        [InlineData("\\S1/2 open", "\\S1/2 open")]
        [InlineData("line^Jcaret", "line\ncaret")]
        [InlineData("a^Mb^ c", "ab^c")]
        public void TheFormattingOfAnMText_GoesWithoutTakingAnyWordWithIt(string markup, string expected)
        {
            DxfText text = Only(Read(R2000(G(0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, markup))));

            Assert.True(text.IsMultiline);
            Assert.Equal(expected, text.Value);
        }

        [Theory]
        [InlineData("50%%% off", "50% off")]
        [InlineData("50%%%d", "50%d")]
        [InlineData("%%065%%066%%067", "ABC")]
        [InlineData("%%u under %%o over", "%%u under %%o over")]
        [InlineData("%%12 two digits", "%%12 two digits")]
        [InlineData("ends %%", "ends %%")]
        public void APerCentSignAndACharacterByItsCode_AreReadAsAutoCadReadsThem(string written, string expected)
        {
            Assert.Equal(expected, Only(Read(R2000(G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, written)))).Value);
            Assert.Equal(expected, Only(Read(R2000(G(0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, written)))).Value);
        }

        [Fact]
        public void ControlCharactersInCaretForm_AreTheCharacters_ATextKeepingToOneLine()
        {
            DxfDocument read = Read(R2000(
                G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, "tab^Ihere, a caret ^ , a line^Jfeed^M and a\\U+000Aline\\U+000D")
                + G(0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, "tab^Ihere^Jnext\\U+000Dline\\U+000Alast")));
            DxfText[] texts = read.Shapes.Select(e => (DxfText)e.Shape).ToArray();

            // A TEXT is one line: a line feed is a space and a carriage return is left out; an MTEXT keeps its line feeds.
            Assert.Equal("tab\there, a caret ^, a line feed and a line", texts[0].Value);
            Assert.Equal("tab\there\nnextline\nlast", texts[1].Value);
        }

        [Fact]
        public void ATextWithControlCharacters_IsWrittenAndReadBackTheSame_ThroughItsEntity()
        {
            DxfDocument read = Read(R2000(
                G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, "a\\U+000Ab\\U+000Dc^Id^ e")
                + G(0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, "a\\U+000Ab\\U+000Dc^Id^ e\\U+0001f")));
            var writer = new DxfWriter();

            foreach (DxfShape entity in read.Shapes)
            {
                writer.Add(entity);
            }

            DxfDocument again = Read(writer.ToString());

            Assert.Equal(read.Shapes.Select(e => ((DxfText)e.Shape).Value).ToArray(), again.Shapes.Select(e => ((DxfText)e.Shape).Value).ToArray());
            Assert.Equal("a bc\td^e", ((DxfText)again.Shapes[0].Shape).Value);
            Assert.Equal("a\nbc\td^e\u0001f", ((DxfText)again.Shapes[1].Shape).Value);
            Assert.Contains("a bc^Id^ e", writer.ToString());
        }

        [Theory]
        [InlineData("-90", 270.0)]
        [InlineData("450", 90.0)]
        [InlineData("360", 0.0)]
        [InlineData("30", 30.0)]
        public void ARotation_IsBroughtIntoAWholeTurn(string degrees, double expected)
        {
            DxfText text = Only(Read(R2000(G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "1", 1, "a", 50, degrees))));

            Assert.True(text.Rotation >= 0.0 && text.Rotation < Math.PI * 2.0);
            Assert.Equal(expected * Math.PI / 180.0, text.Rotation, 12);
            Assert.Equal(Math.PI * 1.5, new DxfText(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0, -Math.PI / 2.0, "a").Rotation, 15);
        }

        [Fact]
        public void AnMTextIsWritten_TurnedByTheDirectionItReadsAlong_NotByAnAngle()
        {
            var text = new DxfText(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 1), 2.0, Math.PI / 2.0, "up", true);
            string written = new DxfWriter().Add(text).ToString();
            string[] lines = written.Split('\n');
            int at = Array.IndexOf(lines, "MTEXT");
            int end = Array.IndexOf(lines, "  0", at + 1);
            string[] codes = lines.Skip(at + 1).Take(end - at - 1).Where((l, i) => i % 2 == 0).Select(l => l.Trim()).ToArray();

            Assert.Contains("11", codes);
            Assert.DoesNotContain("50", codes);
            Assert.Equal(Math.PI / 2.0, ((DxfText)Read(written).Shapes[0].Shape).Rotation, 15);
        }

        [Fact]
        public void AnMText_IsItsPiecesInTheirOrder_ThenItsLast()
        {
            DxfText text = Only(Read(R2000(G(0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "1", 3, "first piece {\\fArial;", 3, "bold} second ", 1, "last\\Pline"))));

            Assert.Equal("first piece bold second last\nline", text.Value);
        }

        [Fact]
        public void ATextOnTheExtrusionDown_StandsWhereItsPlaneSaysInTheWorld_TurnedInThatPlane()
        {
            DxfText text = Only(Read(R2000(G(0, "TEXT", 8, "0", 10, "3", 20, "4", 30, "5", 40, "2", 1, "under", 50, "45", 41, "0.75", 210, "0", 220, "0", 230, "-1"))));

            // The system of (0, 0, -1) has its x along the world's -x: the point (3, 4, 5) of it is (-3, 4, -5).
            Same(new GeoPoint3(-3.0, 4.0, -5.0), text.Position);
            Same(new GeoVector3(0.0, 0.0, -1.0), text.Normal);
            Assert.Equal(45.0 * Math.PI / 180.0, text.Rotation);
            Assert.Equal(0.75, text.WidthFactor);
        }

        [Fact]
        public void AnMText_StandsInWorldCoordinates_TurnedByItsDirection_OrByItsAngleInDegreesWhereItHasNone()
        {
            DxfDocument read = Read(R2000(
                G(0, "MTEXT", 8, "0", 10, "1", 20, "2", 30, "3", 40, "2", 41, "20", 1, "tilted", 210, "0.6", 220, "0", 230, "0.8", 11, "-0.8", 21, "0", 31, "0.6")
                + G(0, "MTEXT", 8, "0", 10, "0", 20, "5", 30, "0", 40, "1.5", 1, "up", 50, "90")
                + G(0, "MTEXT", 8, "0", 10, "0", 20, "5", 30, "0", 40, "1.5", 1, "both", 50, "90", 11, "1", 21, "1", 31, "0")));
            DxfText[] texts = read.Shapes.Select(e => (DxfText)e.Shape).ToArray();

            // On (0.6, 0, 0.8) the system's x is (0, 1, 0) and its y (-0.8, 0, 0.6): the direction is a quarter turn on.
            Same(new GeoPoint3(1.0, 2.0, 3.0), texts[0].Position);
            Same(new GeoVector3(0.6, 0.0, 0.8), texts[0].Normal);
            Assert.Equal(Math.PI / 2.0, texts[0].Rotation, 15);
            Assert.Equal(20.0, texts[0].Width);
            Assert.Equal(1.0, texts[0].WidthFactor);
            Assert.Equal(90.0 * Math.PI / 180.0, texts[1].Rotation);
            Assert.Equal(Math.PI / 4.0, texts[2].Rotation);
        }

        [Fact]
        public void ATextOfNoHeight_IsCountedAndLeftOut_AndTheRestRead()
        {
            DxfDocument read = Read(R2000(
                G(0, "TEXT", 8, "0", 10, "0", 20, "0", 1, "no height")
                + G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "0", 1, "nought")
                + G(0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "-1", 1, "below nought")
                + G(0, "TEXT", 8, "0", 40, "1", 1, "nowhere")
                + G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0")));

            Assert.Equal("LINE", Assert.Single(read.Shapes).Type);
            Assert.Equal(3, read.Skipped["TEXT"]);
            Assert.Equal(1, read.Skipped["MTEXT"]);
        }

        [Fact]
        public void AText_IsATextWithPlanAs2D_AndItsLengthsAreScaledToMillimeters_NotItsAngleOrWidthFactor()
        {
            string text = File("AC1015", 6, string.Empty, G(0, "TEXT", 8, "0", 10, "1.5", 20, "2", 30, "0", 40, "0.25", 1, "a", 50, "30", 41, "0.8")
                + G(0, "MTEXT", 8, "0", 10, "1", 20, "1", 30, "0", 40, "0.5", 41, "2", 1, "b"));
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { PlanAs2D = true, ToMillimeters = true, Tolerance = Tol });
            DxfText[] texts = read.Shapes.Select(e => (DxfText)e.Shape).ToArray();

            Same(new GeoPoint3(1500.0, 2000.0, 0.0), texts[0].Position);
            Assert.Equal(250.0, texts[0].Height);
            Assert.Equal(30.0 * Math.PI / 180.0, texts[0].Rotation);
            Assert.Equal(0.8, texts[0].WidthFactor);
            Assert.Equal(500.0, texts[1].Height);
            Assert.Equal(2000.0, texts[1].Width);
        }

        [Fact]
        public void AText_SaysWhatItIs()
        {
            DxfText text = Only(Read(R2000(G(0, "TEXT", 8, "0", 10, "1000", 20, "500", 40, "2.5", 1, "Grid A"))));

            Assert.StartsWith("TEXT \"Grid A\" at (", text.ToString(), StringComparison.Ordinal);
        }

        #endregion

        #region Through blocks

        private static string WithBlock(string blocks, string entities) => File("AC1015", 4, G(0, "SECTION", 2, "BLOCKS") + blocks + G(0, "ENDSEC"), entities);

        [Fact]
        public void ATextOfABlock_TakesTheLayerAndColourOfItsInsert_AndTheNamesOfItsBlocks()
        {
            string text = WithBlock(
                G(0, "BLOCK", 8, "0", 2, "TAG", 70, 0, 10, "0", 20, "0", 30, "0") + G(0, "TEXT", 5, "A1", 8, "0", 62, 0, 10, "1", 20, "2", 40, "2.5", 1, "D1") + G(0, "ENDBLK", 8, "0"),
                G(0, "INSERT", 5, "B1", 8, "doors", 62, 3, 2, "TAG", 10, "10", 20, "20") + G(0, "INSERT", 5, "B2", 8, "walls", 2, "TAG", 10, "30", 20, "20"));

            DxfDocument read = Read(text);

            Assert.Equal(new[] { "doors", "walls" }, read.Shapes.Select(e => e.Layer).ToArray());
            Assert.Equal(new int?[] { 3, null }, read.Shapes.Select(e => e.Color).ToArray());
            Assert.All(read.Shapes, e => Assert.Equal(new[] { "TAG" }, e.BlockPath.ToArray()));
            Assert.Equal(new[] { "B1", "B2" }, read.Shapes.Select(e => e.InsertHandles[0]).ToArray());
            Same(new GeoPoint3(11.0, 22.0, 0.0), ((DxfText)read.Shapes[0].Shape).Position);
            Assert.Empty(read.Skipped);
        }

        [Fact]
        public void TheTextOfAMirroredInsert_FacesTheOtherWay_ReadingAlongTheMirroredAxis_AsAutoCadWritesIt()
        {
            string text = WithBlock(
                G(0, "BLOCK", 8, "0", 2, "TAG", 70, 0, 10, "0", 20, "0", 30, "0") + G(0, "TEXT", 8, "0", 10, "1", 20, "0", 40, "2", 1, "D1") + G(0, "ENDBLK", 8, "0"),
                G(0, "INSERT", 8, "0", 2, "TAG", 10, "0", 20, "0", 41, "-1") + G(0, "INSERT", 8, "0", 2, "TAG", 10, "0", 20, "0", 41, "2", 42, "2", 43, "2", 50, "90"));

            DxfText[] texts = Read(text).Shapes.Select(e => (DxfText)e.Shape).ToArray();

            // Mirrored in x, the text faces down, whose system has its x along the world's -x: it reads from (-1, 0) on along
            // -x, its rotation nought, as it would seen in a mirror.
            Same(new GeoPoint3(-1.0, 0.0, 0.0), texts[0].Position);
            Same(new GeoVector3(0.0, 0.0, -1.0), texts[0].Normal);
            Assert.Equal(0.0, texts[0].Rotation);
            Assert.Equal(2.0, texts[0].Height);
            Assert.Equal(1.0, texts[0].WidthFactor);

            // Turned a quarter and scaled by 2, it reads up the page from (0, 2), twice as tall.
            Same(new GeoPoint3(0.0, 2.0, 0.0), texts[1].Position);
            Same(new GeoVector3(0.0, 0.0, 1.0), texts[1].Normal);
            Assert.Equal(Math.PI / 2.0, texts[1].Rotation);
            Assert.Equal(4.0, texts[1].Height);
        }

        [Fact]
        public void AnUnevenInsert_StretchesTheHeightAsItsYAxis_AndATextsWidthFactorAsItsXAxisAgainstIt()
        {
            string text = WithBlock(
                G(0, "BLOCK", 8, "0", 2, "TAG", 70, 0, 10, "0", 20, "0", 30, "0")
                + G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "2", 41, "0.5", 1, "D1")
                + G(0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "2", 41, "10", 1, "D2")
                + G(0, "ENDBLK", 8, "0"),
                G(0, "INSERT", 8, "0", 2, "TAG", 10, "0", 20, "0", 41, "3", 42, "2"));

            DxfText[] texts = Read(text).Shapes.Select(e => (DxfText)e.Shape).ToArray();

            Assert.Equal(4.0, texts[0].Height);
            Assert.Equal(0.75, texts[0].WidthFactor);
            Assert.Equal(0.0, texts[0].Rotation);
            Assert.Equal(4.0, texts[1].Height);
            Assert.Equal(1.0, texts[1].WidthFactor);
            Assert.Equal(30.0, texts[1].Width);
        }

        [Fact]
        public void TheTextsOfABlock_CountTowardsTheBoundOnWhatInsertsPlace()
        {
            string text = WithBlock(
                G(0, "BLOCK", 8, "0", 2, "TAG", 70, 0, 10, "0", 20, "0", 30, "0")
                + G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "2", 1, "a") + G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "2", 1, "b") + G(0, "MTEXT", 8, "0", 10, "0", 20, "0", 40, "2", 1, "c")
                + G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0")
                + G(0, "ENDBLK", 8, "0"),
                G(0, "INSERT", 8, "0", 2, "TAG", 10, "0", 20, "0"));

            DxfDocument under = DxfReader.Parse(text, new DxfReadOptions { MaxPlacedEntities = 3, Tolerance = Tol });
            DxfDocument enough = DxfReader.Parse(text, new DxfReadOptions { MaxPlacedEntities = 4, Tolerance = Tol });

            Assert.Empty(under.Shapes);
            Assert.Equal(1, under.Skipped["INSERT"]);
            Assert.Equal(new[] { "TEXT", "TEXT", "MTEXT", "LINE" }, enough.Shapes.Select(e => e.Type).ToArray());
        }

        [Fact]
        public void ATextOfABlockFlattenedByItsInsert_IsCountedAndLeftOut()
        {
            string text = WithBlock(
                G(0, "BLOCK", 8, "0", 2, "TAG", 70, 0, 10, "0", 20, "0", 30, "0") + G(0, "TEXT", 8, "0", 10, "0", 20, "0", 40, "2", 1, "a") + G(0, "ENDBLK", 8, "0"),
                G(0, "INSERT", 8, "0", 2, "TAG", 10, "0", 20, "0", 42, "0"));

            DxfDocument read = Read(text);

            Assert.Empty(read.Shapes);
            Assert.Equal(1, read.Skipped["TEXT"]);
        }

        #endregion

        #region Writing

        [Fact]
        public void TextsWrittenAndReadBack_AreTheSameTexts_TheirRotationToTheLastPlace()
        {
            string longWords = string.Concat(Enumerable.Repeat("words {in braces} and a \\ back slash, ", 20)) + "\u00E9\nnext paragraph";
            var normal = new GeoVector3(1.0, 2.0, 3.0);
            DxfText[] texts =
            {
                new DxfText(new GeoPoint3(1.1, 2.2, 3.3), new GeoVector3(0.0, 0.0, 1.0), 2.5, 0.7, "Grid A \u00E9 \u2300", widthFactor: 0.8),
                new DxfText(new GeoPoint3(-4.25, 5.5, -1.0), new GeoVector3(0.0, 0.0, -1.0), 3.5, 4.0, "under"),
                new DxfText(new GeoPoint3(10.0, 20.0, 30.0), normal, 1.25, 2.0, "tilted"),
                new DxfText(new GeoPoint3(0.1, 0.2, 0.3), new GeoVector3(0.0, 0.0, 1.0), 2.0, 0.0, "line one\nline {two} \\ \u00B1", true, 1.0, 50.0),
                new DxfText(new GeoPoint3(7.7, -8.8, 9.9), normal, 1.5, 1.2, longWords, true, 1.0, 0.0),
            };
            var writer = new DxfWriter();

            foreach (DxfText text in texts)
            {
                writer.Add(text, "notes");
            }

            DxfDocument read = Read(writer.ToString());

            Assert.Equal(new[] { "TEXT", "TEXT", "TEXT", "MTEXT", "MTEXT" }, read.Shapes.Select(e => e.Type).ToArray());

            for (int i = 0; i < texts.Length; i++)
            {
                var back = (DxfText)read.Shapes[i].Shape;
                DxfText text = texts[i];

                Assert.Equal(text.Value, back.Value);
                Assert.Equal(text.IsMultiline, back.IsMultiline);
                Assert.Equal(text.Height, back.Height);
                Assert.Equal(text.WidthFactor, back.WidthFactor);
                Assert.Equal(text.Width, back.Width);
                Same(text.Normal, back.Normal);
                Assert.True(Math.Abs(text.Rotation - back.Rotation) <= 1E-14, "rotation " + text.Rotation.ToString("R") + " came back " + back.Rotation.ToString("R"));
                Assert.Equal("notes", read.Shapes[i].Layer);

                // The TEXT on a tilted normal goes through its plane's system and comes back to its rounding.
                if (i == 2)
                {
                    Assert.True(text.Position.DistanceTo(back.Position) <= 1E-13, back.Position.ToString());
                }
                else
                {
                    Same(text.Position, back.Position);
                }
            }
        }

        [Fact]
        public void ATextReadWithItsLook_IsWrittenWithIt_AndReadBackTheSame()
        {
            string text = R2000(
                G(0, "TEXT", 8, "steel", 6, "DASHED", 62, 1, 370, 35, 420, 16744448, 39, "5", 10, "1", 20, "2", 30, "0", 40, "2.5", 1, "note", 50, "30", 41, "0.9")
                + G(0, "MTEXT", 8, "cut", 62, 5, 10, "3", 20, "4", 30, "0", 40, "1.5", 41, "12", 1, "a\\Pb"));
            DxfDocument read = Read(text);
            var writer = new DxfWriter();

            foreach (DxfShape entity in read.Shapes)
            {
                writer.Add(entity);
            }

            DxfDocument again = Read(writer.ToString());

            for (int i = 0; i < 2; i++)
            {
                DxfShape first = read.Shapes[i];
                DxfShape second = again.Shapes[i];
                var a = (DxfText)first.Shape;
                var b = (DxfText)second.Shape;

                Assert.Equal(first.Type, second.Type);
                Assert.Equal(first.Layer, second.Layer);
                Assert.Equal(first.Color, second.Color);
                Assert.Equal(first.TrueColor, second.TrueColor);
                Assert.Equal(first.Linetype, second.Linetype);
                Assert.Equal(first.LineWeight, second.LineWeight);
                Assert.Equal(first.Thickness, second.Thickness);
                Assert.Equal(a.Value, b.Value);
                Same(a.Position, b.Position);
                Assert.Equal(a.Height, b.Height);
                Assert.Equal(a.Width, b.Width);
                Assert.Equal(a.WidthFactor, b.WidthFactor);
                Assert.True(Math.Abs(a.Rotation - b.Rotation) <= 1E-14, "rotation " + a.Rotation.ToString("R") + " came back " + b.Rotation.ToString("R"));
            }

            Assert.Equal(5.0, again.Shapes[0].Thickness);
            Assert.Equal("a\nb", ((DxfText)again.Shapes[1].Shape).Value);
        }

        [Fact]
        public void ATextMadeInCode_IsCheckedWhenMade()
        {
            var at = new GeoPoint3(0.0, 0.0, 0.0);
            var up = new GeoVector3(0.0, 0.0, 1.0);

            Assert.Throws<ArgumentNullException>(() => new DxfText(at, up, 1.0, 0.0, null));
            Assert.Throws<ArgumentException>(() => new DxfText(new GeoPoint3(double.NaN, 0.0, 0.0), up, 1.0, 0.0, "a"));
            Assert.Throws<ArgumentException>(() => new DxfText(at, new GeoVector3(0.0, 0.0, 0.0), 1.0, 0.0, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfText(at, up, 0.0, 0.0, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfText(at, up, 1.0, double.PositiveInfinity, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfText(at, up, 1.0, 0.0, "a", widthFactor: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfText(at, up, 1.0, 0.0, "a", true, 1.0, -1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfText(at, up, 1.0, 0.0, "a", true, 2.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfText(at, up, 1.0, 0.0, "a", false, 1.0, 5.0));
            Assert.Throws<ArgumentException>(() => new DxfText(at, up, 1.0, 0.0, "a\nb"));
            Assert.Throws<ArgumentException>(() => new DxfText(at, up, 1.0, 0.0, "a\rb", true));
            Assert.Throws<ArgumentNullException>(() => new DxfWriter().Add((DxfText)null));

            // A normal not of unit length is made unit.
            Same(new GeoVector3(0.0, 0.0, -1.0), new DxfText(at, new GeoVector3(0.0, 0.0, -4.0), 1.0, 0.0, "a").Normal);
        }

        #endregion
    }
}
