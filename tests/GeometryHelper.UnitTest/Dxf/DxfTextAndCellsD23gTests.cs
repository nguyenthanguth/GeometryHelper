using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What AutoCAD 2021 showed of long table cells, texts given as strings, multibyte codes and the escape of nought (D23g,
    /// checked headless on files the library wrote and on tables AutoCAD wrote itself): a cell longer than 250 characters is
    /// written in pieces of 3 before R2007 and its text in pieces of 303 from R2007 on; a drawing before R2007 opened from a
    /// string is kept in the bytes of its code page; <c>\M+</c>, a code page and two bytes, is the letter of those bytes in
    /// every text; and the plain words of an MTEXT leave out <c>\U+0000</c>.
    /// </summary>
    public class DxfTextAndCellsD23gTests
    {
        private const string Placeholder = "GH_WORDS";

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0.0);

        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().First();

        private static DxfDatabase Drawing(string version) => DxfDatabase.Parse(version == "R2000" ? DxfDimensionDrawings.R2000 : DxfDimensionDrawings.R2018);

        /// <summary>Letters and digits in tens, A123456789B123456789 and on, so that a piece out of place shows.</summary>
        private static string Plain(int length)
        {
            var text = new StringBuilder(length);

            for (int k = 0; text.Length < length; k++)
            {
                text.Append((char)('A' + (k % 26))).Append("123456789");
            }

            return text.ToString(0, length);
        }

        private static string Escapes(char letter, int count) => string.Concat(Enumerable.Repeat("\\U+" + ((int)letter).ToString("X4"), count));

        /// <summary>
        /// The drawing of the dimension tests of a version, its words written as given in an MTEXT, a TEXT, the ATTRIB of an
        /// INSERT, a rotated dimension, a MULTILEADER and the cell (2, 0) of its table, as the file holds them.
        /// </summary>
        private static DxfDatabase Holding(string version, string written)
        {
            DxfDatabase db = Drawing(version);
            db.ModelSpace.AppendEntity(new DxfMText { Contents = Placeholder, Location = P(0, 0), TextHeight = 2.5 });
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = Placeholder, Position = P(0, 10), Height = 2.5 });
            var block = new DxfBlockTableRecord { Name = "GH_TAGGED" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfAttributeDefinition(P(0, 0), "v", "TAG", "p", null));
            var insert = new DxfBlockReference(P(0, 20), block);
            db.ModelSpace.AppendEntity(insert);
            insert.AttributeCollection.AppendAttribute(new DxfAttributeReference(P(0, 20), Placeholder, "TAG", null));
            var drawn = new DxfBlockTableRecord { Name = "GH_DRAWN" };
            db.BlockTable.Add(drawn);
            drawn.AppendEntity(new DxfLine(P(0, 70), P(100, 70)));
            db.ModelSpace.AppendEntity(new DxfRotatedDimension(0.0, P(0, 60), P(100, 60), P(50, 70), Placeholder, null) { DimBlock = drawn });
            var mleader = new DxfMLeader { MText = new DxfMText { Contents = Placeholder, Location = P(30, 42), TextHeight = 2.5 } };
            mleader.AddLeaderLine(P(0, 30));
            db.ModelSpace.AppendEntity(mleader);
            Table(db).SetTextString(2, 0, Placeholder);
            string text = db.ToText();
            Assert.True(text.Split('\n').Count(line => line == Placeholder) >= 6);
            return DxfDatabase.Parse(text.Replace("\n" + Placeholder + "\n", "\n" + written + "\n"));
        }

        /// <summary>The words of each text of the drawing <see cref="Holding"/> makes: MTEXT, TEXT, ATTRIB, dimension, MULTILEADER, table cell and its value.</summary>
        private static string[] Words(DxfDatabase db)
        {
            DxfTable table = Table(db);

            return new[]
            {
                db.ModelSpace.OfType<DxfMText>().Single().Contents,
                db.ModelSpace.OfType<DxfDBText>().Single().TextString,
                db.ModelSpace.OfType<DxfBlockReference>().Single(b => b.Name == "GH_TAGGED").AttributeCollection.Cast<DxfAttributeReference>().Single().TextString,
                db.ModelSpace.OfType<DxfRotatedDimension>().Single(d => d.DimBlock?.Name == "GH_DRAWN").DimensionText,
                db.ModelSpace.OfType<DxfMLeader>().Single().MText.Contents,
                table.TextString(2, 0),
                (string)table.Value(2, 0),
            };
        }

        // ---- long table cells ------------------------------------------------------------------------------------------------

        [Theory]
        [InlineData(251)]
        [InlineData(600)]
        [InlineData(750)]
        public void ACellOfR2000LongerThan250Letters_IsWrittenInPiecesOfThreeAndALastOne_AsAutoCadWritesIt(int length)
        {
            // AutoCAD 2021 reads such a cell written in pieces of 2 as empty, and writes one in pieces of 3 and the rest in 1.
            string words = Plain(length);
            DxfDatabase db = Drawing("R2000");
            Table(db).SetTextString(2, 0, words);
            string text = db.ToText();
            var expected = new StringBuilder();
            int at = 0;

            for (; words.Length - at > 250; at += 250)
            {
                expected.Append("\n  3\n").Append(words, at, 250);
            }

            expected.Append("\n  1\n").Append(words, at, words.Length - at).Append('\n');

            Assert.Contains(expected.ToString(), text, StringComparison.Ordinal);
            Assert.DoesNotContain("\n  2\n" + words.Substring(0, 250) + "\n", text, StringComparison.Ordinal);
            Assert.Equal(words, Table(DxfDatabase.Parse(text)).TextString(2, 0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ACellOfR2000OfLettersOutsideItsCodePage_IsCutByLetters_EachPieceWrittenAsItsEscapes(bool givenAsEscapes)
        {
            // AutoCAD 2021 writes 300 D with stroke in R2000 as 250 escapes in a 3 (1 750 characters) and 50 in the 1, and reads
            // the escapes of each piece alone: 250 escapes cut at 250 characters read as 286 letters.
            DxfDatabase db = Drawing("R2000");
            Table(db).SetTextString(2, 0, givenAsEscapes ? Escapes('Đ', 300) : new string('Đ', 300));
            string text = db.ToText();

            Assert.Contains("\n  3\n" + Escapes('Đ', 250) + "\n  1\n" + Escapes('Đ', 50) + "\n", text, StringComparison.Ordinal);
            Assert.Equal(new string('Đ', 300), Table(DxfDatabase.Parse(text)).TextString(2, 0));
            Table(db).SetTextString(2, 0, Escapes('Đ', 250));
            Assert.Contains("\n  1\n" + Escapes('Đ', 250) + "\n", db.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void ACellOfR2018LongerThan250Letters_HasItsValueInTwosAndItsTextIn303s_AsAutoCadWritesIt()
        {
            string words = Plain(600);
            DxfDatabase db = Drawing("R2018");
            Table(db).SetTextString(2, 0, words);
            string text = db.ToText();
            string a = words.Substring(0, 250), b = words.Substring(250, 250), c = words.Substring(500);

            Assert.Contains("\n 90\n4\n  2\n" + a + "\n  2\n" + b + "\n  1\n" + c + "\n 94\n0\n300\n\n303\n" + a + "\n303\n" + b + "\n302\n" + c + "\n304\nACVALUE_END\n", text, StringComparison.Ordinal);
            Assert.Equal(words, Table(DxfDatabase.Parse(text)).TextString(2, 0));
            Assert.Equal(words, Table(DxfDatabase.Parse(text)).Value(2, 0));
        }

        [Fact]
        public void ALongCellOfR2018AsAutoCadWritesIt_IsReadWhole()
        {
            // AutoCAD 2021's own DxfOut of a cell of 600 letters: 2, 2 and 1 in its value, 303, 303 and 302 for its text.
            string words = Plain(600);
            string a = words.Substring(0, 250), b = words.Substring(250, 250), c = words.Substring(500);
            DxfDatabase db = Drawing("R2018");
            Table(db).SetTextString(2, 0, Placeholder);
            string text = db.ToText()
                .Replace("\n  1\n" + Placeholder + "\n", "\n  2\n" + a + "\n  2\n" + b + "\n  1\n" + c + "\n")
                .Replace("\n302\n" + Placeholder + "\n", "\n303\n" + a + "\n303\n" + b + "\n302\n" + c + "\n");
            DxfTable table = Table(DxfDatabase.Parse(text));

            Assert.Equal(words, table.TextString(2, 0));
            Assert.Equal(words, table.Value(2, 0));
        }

        [Theory]
        [InlineData("R2000", "x\\U+0041y", "xAy")]
        [InlineData("R2018", "x\\U+0110y", "xĐy")]
        public void TheWordsACellHas_AsTheyRead_ChangeNothingWhenSetAgain(string version, string written, string read)
        {
            DxfDatabase db = Holding(version, written);
            DxfTable table = Table(db);
            string before = db.ToText();

            Assert.Equal(read, table.TextString(2, 0));
            table.SetTextString(2, 0, table.TextString(2, 0));
            Assert.Equal(before, db.ToText());
            table.SetValue(2, 0, table.Value(2, 0));
            Assert.Equal(before, db.ToText());
            table.SetTextString(2, 0, written);
            Assert.Equal(before, db.ToText());
        }

        // ---- a drawing before R2007 opened from a string ---------------------------------------------------------------------

        /// <summary>A drawing of a version with an MTEXT and a TEXT of words and a layer of a name, as a string holds it: every letter as itself, no escape.</summary>
        private static string AsAString(string version, string words, string layer)
        {
            var made = new DxfDatabase(version);
            made.LayerTable.Add(new DxfLayerTableRecord { Name = Placeholder + "L" });
            made.ModelSpace.AppendEntity(new DxfMText { Contents = Placeholder, Location = P(0, 0), TextHeight = 2.5, Layer = Placeholder + "L" });
            made.ModelSpace.AppendEntity(new DxfDBText { TextString = Placeholder, Position = P(0, 10), Height = 2.5 });
            return made.ToText().Replace(Placeholder + "L", layer).Replace(Placeholder, words);
        }

        private static byte[] Saved(DxfDatabase db)
        {
            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                return stream.ToArray();
            }
        }

        private static bool Holds(byte[] bytes, byte[] part)
        {
            for (int i = 0; i + part.Length <= bytes.Length; i++)
            {
                int k = 0;

                while (k < part.Length && bytes[i + k] == part[k])
                {
                    k++;
                }

                if (k == part.Length)
                {
                    return true;
                }
            }

            return false;
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void ADrawingBeforeR2007GivenAsAString_IsSavedInItsCodePage_ALetterItLacksAsItsEscape(string version)
        {
            // AutoCAD 2021 refused the drawing saved as the UTF-8 of the string (eInvalidSymbolTableName) and reads this one.
            DxfDatabase db = DxfDatabase.Parse(AsAString(version, "café Đ", "Thép é Đ"));
            byte[] bytes = Saved(db);

            Assert.Equal("ANSI_1252", db.CodePage);
            Assert.Empty(db.Warnings);
            Assert.True(Holds(bytes, Encoding.GetEncoding(1252).GetBytes("café").Concat(Encoding.ASCII.GetBytes(" \\U+0110")).ToArray()));
            Assert.False(Holds(bytes, new byte[] { 0xC3, 0xA9 }));
            DxfDatabase read = DxfDatabase.Open(new MemoryStream(bytes));
            Assert.Equal("café Đ", read.ModelSpace.OfType<DxfMText>().Single().Contents);
            Assert.Equal("café Đ", read.ModelSpace.OfType<DxfDBText>().Single().TextString);
            Assert.Equal("Thép é Đ", read.ModelSpace.OfType<DxfMText>().Single().Layer);
            Assert.Contains(read.LayerTable.Cast<DxfLayerTableRecord>(), l => l.Name == "Thép é Đ");
        }

        [Fact]
        public void ADrawingBeforeR2007GivenAsAString_AndEdited_IsSavedInItsCodePageAsAWhole()
        {
            DxfDatabase db = DxfDatabase.Parse(AsAString("AC1018", "café Đ", "Thép"));
            db.ModelSpace.AppendEntity(new DxfMText { Contents = "Đường é", Location = P(0, 20), TextHeight = 2.5 });
            byte[] bytes = Saved(db);

            Assert.DoesNotContain(bytes, b => b >= 0xC0 && b != 0xE9);
            Assert.True(Holds(bytes, Encoding.ASCII.GetBytes("\\U+0110\\U+01B0\\U+1EDDng")));
            DxfDatabase read = DxfDatabase.Open(new MemoryStream(bytes));
            Assert.Equal(new[] { "café Đ", "Đường é" }, read.ModelSpace.OfType<DxfMText>().Select(m => m.Contents));
            Assert.Equal(db.ToText(), read.ToText());
        }

        [Fact]
        public void ADrawingBeforeR2007OfAnotherCodePageGivenAsAString_IsSavedInThatPage()
        {
            string text = AsAString("AC1015", "Сталь Đ", "Сталь").Replace("ANSI_1252", "ANSI_1251");
            DxfDatabase db = DxfDatabase.Parse(text);
            byte[] bytes = Saved(db);
            byte[] steel = Encoding.GetEncoding(1251).GetBytes("Сталь");

            Assert.Equal("ANSI_1251", db.CodePage);
            Assert.True(Holds(bytes, steel.Concat(Encoding.ASCII.GetBytes(" \\U+0110")).ToArray()));
            Assert.Equal("Сталь Đ", DxfDatabase.Open(new MemoryStream(bytes)).ModelSpace.OfType<DxfMText>().Single().Contents);
        }

        [Fact]
        public void ADrawingOfR2018GivenAsAString_IsKeptInUtf8()
        {
            DxfDatabase db = DxfDatabase.Parse(AsAString("AC1032", "café Đ", "Thép"));

            Assert.Equal("UTF-8", db.CodePage);
            Assert.True(Holds(Saved(db), Encoding.UTF8.GetBytes("café Đ")));
        }

        // ---- multibyte codes and the escape of nought ------------------------------------------------------------------------

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void AMultibyteCodeOfADoubleByteLetter_IsReadAsTheLetter_InEveryText_AsAutoCadReadsIt(string version)
        {
            // \M+, the code page (1 Shift-JIS, 2 Big5, 3 Wansung, 4 Johab, 5 GB 2312) and the two bytes; a byte alone after 00;
            // either case of M and of the digits; a backslash before it kept.
            const string Written = "x\\M+182A0y \\m+182a0 \\M+2A440 \\M+3B0A1 \\M+48861 \\M+5B0A1 \\M+10041 \\M+100A4 \\M+1007Bz\\M+1007D \\\\M+18140";
            const string Read = "xあy あ 一 가 가 啊 A \uFF64 {z} \\\u3000";

            Assert.All(Words(Holding(version, Written)), words => Assert.Equal(Read, words));
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void AMultibyteCodeOfASingleByteFirst_OfAnUnknownPage_OrTooShort_IsKeptAsWritten(string version)
        {
            const string Written = "x\\M+1A4A1y \\M+14142 \\M+6B0A1 \\M+0B0A1 \\M+182A";

            Assert.All(Words(Holding(version, Written)), words => Assert.Equal(Written, words));
        }

        [Fact]
        public void ALayerNamedWithAMultibyteCode_IsNamedWithTheLetter()
        {
            var made = new DxfDatabase("AC1018");
            made.LayerTable.Add(new DxfLayerTableRecord { Name = Placeholder });
            made.ModelSpace.AppendEntity(new DxfLine(P(0, 0), P(1, 0)) { Layer = Placeholder });
            DxfDatabase db = DxfDatabase.Parse(made.ToText().Replace(Placeholder, "Lx\\M+182A0y"));

            Assert.Contains(db.LayerTable.Cast<DxfLayerTableRecord>(), l => l.Name == "Lxあy");
            Assert.Equal("Lxあy", db.ModelSpace.OfType<DxfLine>().Single().Layer);
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ThePlainWordsOfAnMText_LeaveOutTheEscapeOfNought_AndReadTheFirstByteOfACodeKeptAsWritten(string version)
        {
            // AutoCAD 2021's MText.Text: \U+0000 is nothing, a backslash before it gone with it, and of \M+1A4A1 the first byte,
            // U+FF64; Contents keeps both as written.
            const string Written = "a\\U+0000b c\\\\U+0000d {\\U+0000}e \\u+0000 x\\M+1A4A1y \\M+14142 \\M+1A1A1";
            DxfDatabase db = Holding(version, Written);
            DxfMText mtext = db.ModelSpace.OfType<DxfMText>().Single();

            Assert.Equal(Written, mtext.Contents);
            Assert.Equal("ab cd e  x\uFF64y A \uFF61", mtext.Text);
            Assert.Equal("ab cd e  x\uFF64y A \uFF61", db.ModelSpace.OfType<DxfMLeader>().Single().MText.Text);
            Assert.Equal(Written, db.ModelSpace.OfType<DxfDBText>().Single().TextString);
        }

        // ---- the records by handle -------------------------------------------------------------------------------------------

        [Fact]
        public void TheRecordsOfTheTablesLookedUpByHandleFromManyThreads_AreFoundAsAlone()
        {
            // ApproxNumObjects and GetObject make the records by handle under the lock the entities by handle take.
            var made = new DxfDatabase("AC1032");

            for (int i = 0; i < 200; i++)
            {
                made.LayerTable.Add(new DxfLayerTableRecord { Name = "L" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                made.ModelSpace.AppendEntity(new DxfLine(P(i, 0), P(i, 10)) { Layer = "L" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) });
            }

            string text = made.ToText();
            DxfDatabase first = DxfDatabase.Parse(text);
            int alone = first.ApproxNumObjects;
            string[] handles = first.LayerTable.Cast<DxfLayerTableRecord>().Select(l => l.Handle).ToArray();
            int wrong = 0;
            int thrown = 0;
            string seen = null;

            for (int round = 0; round < 40; round++)
            {
                DxfDatabase shared = DxfDatabase.Parse(text);

                using (var go = new System.Threading.ManualResetEventSlim(false))
                {
                    var threads = Enumerable.Range(0, 8).Select(t => new System.Threading.Thread(() =>
                    {
                        go.Wait();

                        try
                        {
                            if (t % 2 == 0 && shared.ApproxNumObjects != alone)
                            {
                                System.Threading.Interlocked.Increment(ref wrong);
                            }

                            foreach (string handle in handles)
                            {
                                if (!(shared.GetObject(handle) is DxfLayerTableRecord))
                                {
                                    System.Threading.Interlocked.Increment(ref wrong);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            System.Threading.Interlocked.Increment(ref thrown);
                            System.Threading.Interlocked.CompareExchange(ref seen, e.GetType().Name + ": " + e.Message, null);
                        }
                    })).ToList();

                    threads.ForEach(thread => thread.Start());
                    go.Set();
                    threads.ForEach(thread => thread.Join());
                }
            }

            Assert.True(thrown == 0, seen);
            Assert.Equal(0, wrong);
        }
    }
}
