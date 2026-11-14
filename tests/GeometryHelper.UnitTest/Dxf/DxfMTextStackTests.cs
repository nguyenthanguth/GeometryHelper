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
    /// The stacks (<c>\S1/2;</c>) of an MTEXT exploded, which is how a drawing saved as R12 holds them, as AutoCAD 2021
    /// explodes them (its console, <c>Database.DxfOut</c> at R12 of 70 MTEXTs and dimensions made in side databases): the TEXT
    /// of the numerator, a LINE for a bar or a slash, the TEXT of the denominator. The font is a unifont written here whose
    /// letters are inked and moved as txt.shx's are (a capital inked 4 of 6 and moving the pen 6, a 1 inked 2 and moving
    /// the pen 4), so that the numbers asserted are AutoCAD's own.
    /// </summary>
    public class DxfMTextStackTests
    {
        private const double Near = 1E-6;

        // A capital as txt.shx draws one: up 6, along 4, lifted, down 6 and along 2.
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        // A 1 as txt.shx draws it: up 6, along 2, lifted, down 6 and along 2.
        private static readonly byte[] One = { 1, 0x64, 0x20, 2, 0x6C, 0x20, 0 };

        private static Dictionary<int, byte[]> Letters()
        {
            var letters = new Dictionary<int, byte[]>
            {
                { ' ', new byte[] { 2, 0x60, 0 } },
                { '?', Capital },
                { '1', One },
            };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            for (char c = '2'; c <= '9'; c++)
            {
                letters.Add(c, Capital);
            }

            return letters;
        }

        private static byte[] Unifont(IDictionary<int, byte[]> letters)
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
            bytes.AddRange(BitConverter.GetBytes((uint)(letters.Count + 1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
            bytes.AddRange(header);

            foreach (KeyValuePair<int, byte[]> letter in letters.OrderBy(l => l.Key))
            {
                byte[] record = new byte[] { 0 }
                    .Concat(letter.Value)
                    .ToArray();
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

        private static DxfDatabase Drawing(string folder)
        {
            var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
            return db;
        }

        /// <summary>An MTEXT in model space at (10, 10), its words, height and attachment given.</summary>
        private static DxfMText Words(
            DxfDatabase db,
            string contents,
            double height,
            DxfAttachmentPoint attachment = DxfAttachmentPoint.TopLeft
        )
        {
            var mtext = new DxfMText
            {
                Contents = contents,
                TextHeight = height,
                Attachment = attachment,
                Location = new GeoPoint3(10, 10, 0),
            };
            db.ModelSpace.AppendEntity(mtext);
            return mtext;
        }

        private static void Is(double expected, double actual) =>
            Assert.True(
                Math.Abs(expected - actual) <= Near,
                "expected "
                    + expected.ToString("R", CultureInfo.InvariantCulture)
                    + ", got "
                    + actual.ToString("R", CultureInfo.InvariantCulture)
            );

        private static void Text(DxfEntity piece, string words, double x, double y, double height)
        {
            var text = Assert.IsType<DxfDBText>(piece);
            Assert.Equal(words, text.TextString);
            Is(x, text.Position.X);
            Is(y, text.Position.Y);
            Is(height, text.Height);
        }

        private static void Bar(DxfEntity piece, double x0, double y0, double x1, double y1)
        {
            var line = Assert.IsType<DxfLine>(piece);
            Is(x0, line.StartPoint.X);
            Is(y0, line.StartPoint.Y);
            Is(x1, line.EndPoint.X);
            Is(y1, line.EndPoint.Y);
        }

        [Fact]
        public void AStackWithABar_IsTheNumeratorABarAndTheDenominator_EachTheHeightOfTheLetters()
        {
            InFolder(folder =>
            {
                // AutoCAD 2021, txt.shx 2.5 high held at the top left at (10, 10): the ink of AB 4.167 wide sets the bar's width,
                // CD under it on the baseline of the line, AB a bar and a third of the height above, EF a third of the height
                // after the stack.
                IReadOnlyList<DxfEntity> pieces = Words(Drawing(folder), "\\SAB/CD;EF", 2.5).Explode();
                Assert.Equal(4, pieces.Count);
                Text(pieces[0], "AB", 10, 7.5, 2.5);
                Bar(pieces[1], 10, 7.0833333333333339, 14.166666666666668, 7.0833333333333339);
                Text(pieces[2], "CD", 10, 4.1666666666666661, 2.5);
                Text(pieces[3], "EF", 15, 4.1666666666666661, 2.5);
            });
        }

        [Fact]
        public void ALetterAndAStackAndALetter_StandLeftToRight_TheHalvesCentredOnTheBar()
        {
            InFolder(folder =>
            {
                // 0.18 high at the top left: A, then the stack 0.18 on (a 1 inked 0.06 wide, centred over the 2's 0.12), then B 0.06
                // past the stack; the line is 0.42 high, so A and the denominator stand 0.42 below the top.
                IReadOnlyList<DxfEntity> pieces = Words(Drawing(folder), "A\\S1/2;B", 0.18).Explode();
                Assert.Equal(5, pieces.Count);
                Text(pieces[0], "A", 10, 9.58, 0.18);
                Text(pieces[1], "1", 10.21, 9.82, 0.18);
                Bar(pieces[2], 10.18, 9.79, 10.3, 9.79);
                Text(pieces[3], "2", 10.18, 9.58, 0.18);
                Text(pieces[4], "B", 10.36, 9.58, 0.18);
            });
        }

        [Fact]
        public void ALineWithAStack_IsHeldByTheHeightOfTheStack_AtTheMiddleAndAtTheBottom()
        {
            InFolder(folder =>
            {
                // The line with the stack is 0.42 high: held at the middle its denominator stands 0.21 below the place, held at the
                // bottom on it (AutoCAD 2021).
                IReadOnlyList<DxfEntity> middle = Words(
                        Drawing(folder),
                        "A\\S1/2;B",
                        0.18,
                        DxfAttachmentPoint.MiddleLeft
                    )
                    .Explode();
                Text(middle[0], "A", 10, 9.79, 0.18);
                Text(middle[1], "1", 10.21, 10.03, 0.18);
                Bar(middle[2], 10.18, 10, 10.3, 10);

                IReadOnlyList<DxfEntity> bottom = Words(
                        Drawing(folder),
                        "A\\S1/2;B",
                        0.18,
                        DxfAttachmentPoint.BottomLeft
                    )
                    .Explode();
                Text(bottom[0], "A", 10, 10, 0.18);
                Text(bottom[1], "1", 10.21, 10.24, 0.18);
                Bar(bottom[2], 10.18, 10.21, 10.3, 10.21);
            });
        }

        [Fact]
        public void TheLinesAroundAStack_StandApartAsAutoCadSpacesThem()
        {
            InFolder(folder =>
            {
                // Held at the top left, X then A, a 1/2 and B, then Y (AutoCAD 2021): the second line stands 0.54 below the first
                // for the height of the stack, the third 0.24 below the second.
                IReadOnlyList<DxfEntity> pieces = Words(Drawing(folder), "X\\PA\\S1/2;B\\PY", 0.18).Explode();
                Assert.Equal(7, pieces.Count);
                Text(pieces[0], "X", 10, 9.82, 0.18);
                Text(pieces[1], "A", 10, 9.28, 0.18);
                Text(pieces[5], "B", 10.36, 9.28, 0.18);
                Text(pieces[6], "Y", 10, 9.04, 0.18);
            });
        }

        [Fact]
        public void AStackWithASlash_HasTheDenominatorAfterTheNumeratorsPen_AndALineAt55Degrees()
        {
            InFolder(folder =>
            {
                // AutoCAD 2021, 2.5 high: A, a slash 5 long through the numerator's height, BC after A's pen of 2.5, D after BC
                // and a third of the height.
                IReadOnlyList<DxfEntity> pieces = Words(Drawing(folder), "\\SA#BC;D", 2.5).Explode();
                Assert.Equal(4, pieces.Count);
                Text(pieces[0], "A", 10, 7.5, 2.5);
                Bar(pieces[1], 10.64939224245571, 5.45211988927752, 13.51727442421094, 9.54788011072248);
                Text(pieces[2], "BC", 12.5, 5, 2.5);
                Text(pieces[3], "D", 17.5, 5, 2.5);
            });
        }

        [Fact]
        public void AStackWithACaret_HasBothHalvesAtTheLeftAndNoLine()
        {
            InFolder(folder =>
            {
                // A tolerance as AutoCAD 2021 explodes it: AB over C, both at the left, D after the wider one.
                IReadOnlyList<DxfEntity> pieces = Words(Drawing(folder), "\\SAB^C;D", 2.5).Explode();
                Assert.Equal(3, pieces.Count);
                Text(pieces[0], "AB", 10, 7.5, 2.5);
                Text(pieces[1], "C", 10, 4.1666666666666661, 2.5);
                Text(pieces[2], "D", 15, 4.1666666666666661, 2.5);
            });
        }

        [Fact]
        public void AStackWithNoSign_IsTheNumeratorOverAnEmptyDenominator_AndNoLine()
        {
            InFolder(folder =>
            {
                // AutoCAD 2021: the words 12 over a TEXT of none, on the baseline, B after the ink of 12 and a third of the height.
                IReadOnlyList<DxfEntity> pieces = Words(Drawing(folder), "A\\S12;B", 2.5).Explode();
                Assert.Equal(4, pieces.Count);
                Text(pieces[0], "A", 10, 6.6666666666666661, 2.5);
                Text(pieces[1], "12", 12.5, 7.5, 2.5);
                Text(pieces[2], string.Empty, 12.5, 6.6666666666666661, 2.5);
                Text(pieces[3], "B", 16.666666666666668, 6.6666666666666661, 2.5);
            });
        }

        [Fact]
        public void AStackWithAnEmptyHalf_StandsNoHeightAbove_OrBelow_ForIt()
        {
            InFolder(folder =>
            {
                // AutoCAD 2021: "A over nothing" is as high as the numerator and a third of the height more, its empty denominator
                // centred under the bar; nothing over B is a numerator at the top of the line.
                IReadOnlyList<DxfEntity> noDenominator = Words(Drawing(folder), "\\SA/;D", 2.5).Explode();
                Text(noDenominator[0], "A", 10, 7.5, 2.5);
                Bar(noDenominator[1], 10, 7.0833333333333339, 11.666666666666668, 7.0833333333333339);
                Text(noDenominator[2], string.Empty, 10.833333333333334, 6.6666666666666661, 2.5);
                Text(noDenominator[3], "D", 12.5, 6.6666666666666661, 2.5);

                IReadOnlyList<DxfEntity> noNumerator = Words(Drawing(folder), "\\S/B;D", 2.5).Explode();
                Text(noNumerator[0], string.Empty, 10.833333333333334, 10, 2.5);
                Text(noNumerator[2], "B", 10, 6.6666666666666661, 2.5);
            });
        }

        [Fact]
        public void TheWordsOfADimension_WithAStackAndTheMiddleHold_StandAsAutoCadWritesThemIntoAnR12()
        {
            InFolder(folder =>
            {
                // The block of a dimension (\A1; holds the plain words at the middle of the line 0.42 high): A, 1 over 2 and B,
                // as AutoCAD 2021's own R12 has them in the block (a 1 over 2 in the words of a dimension of AutoCAD's sample).
                DxfDatabase database = Drawing(folder);
                var picture = new DxfBlockTableRecord { Name = "*D" };
                database.BlockTable.Add(picture);
                picture.AppendEntity(
                    new DxfMText
                    {
                        Location = new GeoPoint3(50, 20, 0),
                        TextHeight = 0.18,
                        Attachment = DxfAttachmentPoint.MiddleCenter,
                        Contents = "\\A1;A\\S1/2;B",
                    }
                );
                database.ModelSpace.AppendEntity(
                    new DxfRotatedDimension(
                        0,
                        new GeoPoint3(0, 0, 0),
                        new GeoPoint3(100, 0, 0),
                        new GeoPoint3(50, 20, 0),
                        "A\\S1/2;B",
                        null
                    )
                    {
                        DimBlock = picture,
                    }
                );
                string name = picture.Name;

                using (var stream = new MemoryStream())
                {
                    database.Save(stream, "AC1009");
                    List<Record> blocks = Blocks(Encoding.GetEncoding(1252).GetString(stream.ToArray()));

                    // The block of the dimension has the INSERT of the block of the words' pieces: A, 1 over 2 with its bar, and B.
                    int start = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == name);
                    Assert.True(start >= 0, "the block of the dimension is written");
                    Record insert = blocks
                        .Skip(start + 1)
                        .TakeWhile(r => r.Type != "ENDBLK")
                        .Single(r => r.Type == "INSERT");
                    int words = blocks.FindIndex(r => r.Type == "BLOCK" && r.First(2) == insert.First(2));
                    Record[] pieces = blocks.Skip(words + 1).TakeWhile(r => r.Type != "ENDBLK").ToArray();
                    Assert.Equal(new[] { "TEXT", "TEXT", "LINE", "TEXT", "TEXT" }, pieces.Select(r => r.Type));

                    // AutoCAD 2021's own R12: A on the middle of the line 0.42 high, the 1 and the 2 over and under it.
                    Assert.Equal(new[] { "A", "1", null, "2", "B" }, pieces.Select(r => r.First(1)));
                    Assert.Equal(
                        new[] { 49.76, 49.97, 49.94, 49.94, 50.12 },
                        pieces.Select(r => r.Number(10)).Select(n => Math.Round(n, 4))
                    );
                    Assert.Equal(
                        new[] { 19.91, 20.03, 20.0, 19.79, 19.91 },
                        pieces.Select(r => r.Number(20)).Select(n => Math.Round(n, 4))
                    );
                    Assert.Equal(50.06, Math.Round(pieces[2].Number(11), 4));
                }
            });
        }

        private sealed class Record
        {
            internal Record(string type)
            {
                Type = type;
                Groups = new List<KeyValuePair<int, string>>();
            }

            internal string Type { get; }

            internal List<KeyValuePair<int, string>> Groups { get; }

            internal string First(int code) => Groups.Where(g => g.Key == code).Select(g => g.Value).FirstOrDefault();

            internal double Number(int code) => double.Parse(First(code), CultureInfo.InvariantCulture);
        }

        /// <summary>The records of the BLOCKS section: a record runs from its group 0 to the next.</summary>
        private static List<Record> Blocks(string text)
        {
            string[] lines = text.Replace("\r", string.Empty).Split('\n');
            var records = new List<Record>();
            bool inside = false;
            Record record = null;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "SECTION")
                {
                    inside = lines[i + 3] == "BLOCKS";
                    record = null;
                    i += 2;
                }
                else if (code == 0 && (value == "ENDSEC" || value == "EOF"))
                {
                    inside = false;
                    record = null;
                }
                else if (inside && code == 0)
                {
                    record = new Record(value);
                    records.Add(record);
                }
                else if (inside)
                {
                    record?.Groups.Add(new KeyValuePair<int, string>(code, value));
                }
            }

            return records;
        }

        [Fact]
        public void AStackWithALineUnderIt_IsNotLaidOut_AsBefore()
        {
            InFolder(folder =>
            {
                // A stack under a line (\L) is not measured: the MTEXT breaks into the TEXT of its words as it did.
                IReadOnlyList<DxfEntity> pieces = Words(Drawing(folder), "\\LA\\S1/2;B", 2.5).Explode();
                Assert.Single(pieces);
                Assert.IsType<DxfDBText>(pieces[0]);
            });
        }
    }
}
