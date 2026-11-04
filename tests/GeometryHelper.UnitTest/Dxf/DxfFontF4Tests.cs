using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// MTEXTs of several lines laid out in the drawing's fonts (<see cref="DxfDatabase.FontSearchPath"/>, check F4): a unifont
    /// written by the tests, no Autodesk font in the repository, its capitals drawn as txt.shx draws them (inked 4 by 6 and
    /// moving the pen 6), a letter n with a bearing on either side, and 中 and the full-width comma, so that the numbers
    /// AutoCAD 2021 gave for txt.shx (rounds GHF4A to GHF4E) are asserted.
    /// </summary>
    public class DxfFontF4Tests
    {
        private const double Near = 1E-9;

        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        // ---- the font ----------------------------------------------------------------------------------------------------------

        /// <summary>A capital as txt.shx draws one: the pen down where it starts, up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        /// <summary>The letters of the unifont txt.shx, 6 above and 2 below, by their Unicode numbers.</summary>
        private static Dictionary<int, byte[]> Letters()
        {
            var letters = new Dictionary<int, byte[]>
            {
                { ' ', new byte[] { 2, 0x60, 0 } },
                { '?', Capital },
                { 'n', new byte[] { 2, 0x10, 1, 0x64, 0x20, 0x6C, 2, 0x10, 0 } },
                { 0x4E2D, Capital },
                { 0xFF0C, new byte[] { 1, 0x10, 2, 0x50, 0 } },
            };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            return letters;
        }

        /// <summary>The bytes of a unifont of AutoCAD's form: its header (name, above, below, modes, encoding, embedding), then each letter, its number, length, name and codes.</summary>
        private static byte[] Unifont(IDictionary<int, byte[]> letters)
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
            bytes.AddRange(BitConverter.GetBytes((uint)(letters.Count + 1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
            bytes.AddRange(header);

            foreach (KeyValuePair<int, byte[]> letter in letters.OrderBy(l => l.Key))
            {
                byte[] record = new byte[] { 0 }.Concat(letter.Value).ToArray();
                bytes.AddRange(BitConverter.GetBytes((ushort)letter.Key));
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Length));
                bytes.AddRange(record);
            }

            return bytes.ToArray();
        }

        /// <summary>A folder of its own holding txt.shx, the unifont of <see cref="Letters"/>; deleted after the test.</summary>
        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-fonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Unifont(Letters()));
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>A new drawing of R2018, its Standard of txt, its fonts looked for in the folder given (none for null).</summary>
        private static DxfDatabase Drawing(string folder)
        {
            var db = new DxfDatabase("AC1032");

            if (folder != null)
            {
                db.FontSearchPath = folder;
            }

            return db;
        }

        /// <summary>An MTEXT in model space 4 high at (10, 20), its words, width, attachment and line spacing given.</summary>
        private static DxfMText Words(DxfDatabase db, string contents, double width = 0, DxfAttachmentPoint attachment = DxfAttachmentPoint.TopLeft, double factor = 1, DxfLineSpacingStyle spacing = DxfLineSpacingStyle.AtLeast)
        {
            var mtext = new DxfMText { Contents = contents, TextHeight = 4, Width = width, Attachment = attachment, Location = P(10, 20), LineSpacingFactor = factor, LineSpacingStyle = spacing };
            db.ModelSpace.AppendEntity(mtext);
            return mtext;
        }

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Is(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= Near, "expected " + expected.ToString("R", CultureInfo.InvariantCulture) + ", got " + actual.ToString("R", CultureInfo.InvariantCulture));

        private static void Is(GeoPoint3 expected, GeoPoint3 actual) => Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        /// <summary>The TEXTs an MTEXT explodes to, each its words, the start of its baseline and its height.</summary>
        private static List<DxfDBText> Exploded(DxfMText mtext) => mtext.Explode().Cast<DxfDBText>().ToList();

        private static void Text(DxfDBText text, string words, double x, double y, double height = 4)
        {
            Assert.Equal(words, text.TextString);
            Is(P(x, y), text.Position);
            Is(height, text.Height);
        }

        // ---- paragraphs and spacing ----------------------------------------------------------------------------------------------

        [Fact]
        public void AnMTextOfParagraphs_IsLaidOutLineByLine_AsAutoCadLaysItOut()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // AutoCAD 2021, txt.shx 4 high (round GHF4A): each line 5/3 of the height below the one before.
                DxfMText two = Words(db, "ABC\\PDE", 41);
                Is(10.666666666666666, two.ActualWidth);
                Is(10.666666666666666, two.ActualHeight);
                GeoAabb3 box = two.GeometricExtents;
                Is(P(10, 9.3333333333333339), box.Min);
                Is(P(51, 20), box.Max);
                List<DxfDBText> texts = Exploded(two);
                Assert.Equal(2, texts.Count);
                Text(texts[0], "ABC", 10, 16);
                Text(texts[1], "DE", 10, 9.3333333333333339);

                // An empty paragraph is a line; one at the end is none, but one more is (round GHF4B).
                Is(17.333333333333332, Words(db, "ABC\\P\\PDE").ActualHeight);
                Is(10.666666666666666, Words(db, "\\PABC").ActualHeight);
                Is(4, Words(db, "ABC\\P").ActualHeight);
                Is(10.666666666666666, Words(db, "ABC\\P\\P").ActualHeight);
                Is(4, Words(db, "\\P").ActualHeight);

                // The spacing at least: a factor of 1.5 puts the second line 10 below the first; exactly, the MTEXT is the
                // height, the spacing and a third of the height high, whatever its letters.
                Is(14, Words(db, "ABC\\PDE", 0, DxfAttachmentPoint.TopLeft, 1.5).ActualHeight);
                Is(12, Words(db, "ABC\\PDE", 0, DxfAttachmentPoint.TopLeft, 1, DxfLineSpacingStyle.Exactly).ActualHeight);
                Is(5.333333333333333, Words(db, "ABC", 0, DxfAttachmentPoint.TopLeft, 1, DxfLineSpacingStyle.Exactly).ActualHeight);
            });
        }

        [Fact]
        public void ALineOfTallerLetters_StandsTheLowerBelowTheLineBeforeIt_AsAutoCadSpacesIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // Round GHF4D: from line to line the top of the next line, a third of the height of this one and a third of
                // the MTEXT's height: 12 + 4/3 + 4/3 under the line 4 high, then 4 + 4 + 4/3.
                DxfMText tall = Words(db, "ABC\\P{\\H3x;ABC}\\PDEF");
                Is(28, tall.ActualHeight);
                List<DxfDBText> texts = Exploded(tall);
                Text(texts[0], "ABC", 10, 16);
                Text(texts[1], "ABC", 10, 1.3333333333333339, 12);
                Text(texts[2], "DEF", 10, -8);

                // Exactly: the MTEXT's own height spaces the lines.
                DxfMText exactly = Words(db, "ABC\\P{\\H3x;ABC}\\PDEF", 0, DxfAttachmentPoint.TopLeft, 1, DxfLineSpacingStyle.Exactly);
                Is(18.666666666666664, exactly.ActualHeight);
                texts = Exploded(exactly);
                Text(texts[1], "ABC", 10, 9.3333333333333339, 12);
                Text(texts[2], "DEF", 10, 2.6666666666666679);

                // A line under the letters reaches a fifth of their height below the baseline (round GHF4D, "\LABC\l DEF").
                DxfMText under = Words(db, "\\LABC\\l DEF");
                Is(26.666666666666664, under.ActualWidth);
                Is(4.8, under.ActualHeight);
                texts = Exploded(under);
                Text(texts[0], "%%UABC", 10, 16);
                Text(texts[1], " DEF", 22, 16);
            });
        }

        // ---- wrapping ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void WordsWrapAtTheWidth_AWordMovingDownWhereTheLineWouldReachIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // "AAA BBB" is inked 26.667 wide: at that width BBB moves down, a hair wider it stays (round GHF4B's sweep).
                double inked = Words(db, "AAA BBB").ActualWidth;
                Is(26.666666666666668, inked);
                Assert.Equal(new[] { "AAA", "BBB", "CCC" }, Exploded(Words(db, "AAA BBB CCC", inked)).Select(t => t.TextString));
                DxfMText kept = Words(db, "AAA BBB CCC", 26.67);
                Assert.Equal(new[] { "AAA BBB", "CCC" }, Exploded(kept).Select(t => t.TextString));
                Is(26.666666666666668, kept.ActualWidth);
                Is(10.666666666666666, kept.ActualHeight);

                // A word longer than the width is left whole on a line of its own (round GHF4A, the alphabet 15 wide).
                DxfMText along = Words(db, "AB ABCDEFGHIJ CD", 15);
                Assert.Equal(new[] { "AB", "ABCDEFGHIJ", "CD" }, Exploded(along).Select(t => t.TextString));
                Is(38.666666666666664, along.ActualWidth);
                Is(P(10, 2.6666666666666679), along.GeometricExtents.Min);
                Is(P(25, 20), along.GeometricExtents.Max);

                // Spaces before the first word are a word of their own: "  ABC DEF  GHI" 15 wide begins with a line of them.
                DxfMText spaced = Words(db, "  ABC DEF  GHI", 15);
                Is(24, spaced.ActualHeight);
                List<DxfDBText> texts = Exploded(spaced);
                Text(texts[0], "ABC", 10, 9.3333333333333339);
                Text(texts[1], "DEF", 10, 2.6666666666666679);
                Text(texts[2], "GHI", 10, -4);
            });
        }

        [Fact]
        public void ALineFits_ByThePenAtItsLastWordAndThatWordsInk_AsAutoCadTellsIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // n is inked from 2/3 to 2 and moves the pen 8/3. "AA nn" is inked 16.667 wide, but AutoCAD tells the fit by
                // the pen at nn, 12, and nn's own ink, 4: 16, so that it holds the line at 16.5 (romans "H H t" in round GHF4C).
                DxfMText wide = Words(db, "AA nn AA", 16.5);
                Assert.Equal(new[] { "AA nn", "AA" }, Exploded(wide).Select(t => t.TextString));
                Is(16.666666666666664, wide.ActualWidth);

                // "nn AA" is inked from 2/3 to 16, 15.333 wide, but fits by 16: at 15.5 AA moves down. A line's ink stands at
                // the left of its place: nn's TEXT starts 2/3 left of the MTEXT.
                DxfMText narrow = Words(db, "nn AA", 15.5);
                List<DxfDBText> texts = Exploded(narrow);
                Assert.Equal(2, texts.Count);
                Text(texts[0], "nn", 9.3333333333333339, 16);
                Text(texts[1], "AA", 10, 9.3333333333333339);
                Is(6.666666666666667, narrow.ActualWidth);
            });
        }

        [Fact]
        public void ChineseWordsBreakAtEachLetter_ButNotBeforeAComma_AsAutoCadBreaksThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // Round GHF4A and GHF4D: a line breaks before any of 中中中中中, but not before the full-width comma, which takes
                // the letter before it down with it.
                Assert.Equal(new[] { "中中中中", "中" }, Exploded(Words(db, "中中中中中", 15)).Select(t => t.TextString));
                Assert.Equal(new[] { "中中中", "中，中" }, Exploded(Words(db, "中中中中，中", 15)).Select(t => t.TextString));
            });
        }

        // ---- looks within a line, attachments ---------------------------------------------------------------------------------

        [Fact]
        public void ARunOfAnotherHeight_ExplodesToATextOfItsOwn_WhereThePenStands()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // Round GHF4D, txt.shx "A\H2x;B\H0.5x;C\PDEF": the first line 8 high, its baseline 8 below the top.
                DxfMText runs = Words(db, "A\\H2x;B\\H0.5x;C\\PDEF");
                Is(14.666666666666664, runs.ActualWidth);
                Is(16, runs.ActualHeight);
                List<DxfDBText> texts = Exploded(runs);
                Assert.Equal(4, texts.Count);
                Text(texts[0], "A", 10, 12);
                Text(texts[1], "B", 14, 12, 8);
                Text(texts[2], "C", 22, 12);
                Text(texts[3], "DEF", 10, 4);

                // Held at the middle of a line of taller letters (\A1;): ABC 2 up from the baseline of DEF 8 high.
                texts = Exploded(Words(db, "\\A1;ABC\\H2x;DEF"));
                Text(texts[0], "ABC", 10, 14);
                Text(texts[1], "DEF", 22, 12, 8);

                // Spread (\T): every move of the pen but the last twice as long, "A\T2;BC DEF" 46.667 wide.
                Is(46.666666666666664, Words(db, "A\\T2;BC DEF").ActualWidth);
            });
        }

        [Fact]
        public void EachLine_IsHeldByTheAttachment_InTheBoxOfTheWidth()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Drawing(folder);

                // Round GHF4A, "ABC\PDE" 41 wide held at its middle centre at (10, 20): the box 41 wide about it, each line
                // centred on 10 on its own.
                DxfMText middle = Words(db, "ABC\\PDE", 41, DxfAttachmentPoint.MiddleCenter);
                GeoAabb3 box = middle.GeometricExtents;
                Is(P(-10.5, 14.666666666666666), box.Min);
                Is(P(30.5, 25.333333333333332), box.Max);
                List<DxfDBText> texts = Exploded(middle);
                Text(texts[0], "ABC", 4.666666666666667, 21.333333333333332);
                Text(texts[1], "DE", 6.666666666666667, 14.666666666666664);

                // Held at its bottom right, with no width the box is as wide as its widest line.
                DxfMText corner = Words(db, "ABC\\PDE", 0, DxfAttachmentPoint.BottomRight);
                box = corner.GeometricExtents;
                Is(P(-0.666666666666666, 20), box.Min);
                Is(P(10, 30.666666666666664), box.Max);
                texts = Exploded(corner);
                Text(texts[0], "ABC", -0.666666666666666, 26.666666666666664);
                Text(texts[1], "DE", 3.333333333333333, 20);
            });
        }

        // ---- no fonts ------------------------------------------------------------------------------------------------------------

        [Fact]
        public void NoFontSearchPath_LeavesAnMTextOfSeveralLinesAsBefore()
        {
            InFolder(folder =>
            {
                var none = Drawing(null);
                var cleared = Drawing(folder);
                cleared.FontSearchPath = string.Empty;

                foreach (DxfDatabase db in new[] { none, cleared })
                {
                    DxfMText mtext = Words(db, "AAA BBB CCC\\PDE", 15);
                    Assert.Equal(0.0, mtext.ActualWidth);
                    Assert.Equal(0.0, mtext.ActualHeight);
                    Is(P(10, 20), mtext.GeometricExtents.Min);
                    Is(P(10, 20), mtext.GeometricExtents.Max);
                    Assert.Equal(new[] { "AAA BBB CCC", "DE" }, Exploded(mtext).Select(t => t.TextString));
                }

                Assert.Equal(none.ToText(), cleared.ToText());
            });
        }
    }
}
