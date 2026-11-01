using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The escapes <c>\U+XXXX</c> as AutoCAD 2021 reads them (D23f, checked headless on files the library wrote, R2004 and
    /// R2018 alike): every escape of four hexadecimal digits, either case of U, is the letter it stands for in every text,
    /// before anything else reads the words, so that a backslash before it stays a backslash and <c>\U+005CP</c> is a new
    /// paragraph; only <c>\U+0000</c> and an escape of fewer digits are kept as written. A letter beyond U+FFFF is written as
    /// the escapes of its halves in a file of R2018 too, as AutoCAD 2021 refuses the file that holds its four bytes of UTF-8.
    /// </summary>
    public class DxfTextEscapesD23fTests
    {
        private const string Placeholder = "GH_WORDS";

        // An escaped backslash before an escape, the escapes of A, of a backslash, of D with stroke with u in lower case and
        // with a fifth digit, of half a letter alone, and two escapes of too few digits.
        private const string Written = "a\\\\U+0110b \\U+0041 \\u+0110 \\U+01100 \\U+005CP \\U+D83D \\U+00G1 \\U+01";
        private const string Read = "a\\Đb A Đ Đ0 \\P \uD83D \\U+00G1 \\U+01";

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0.0);

        /// <summary>
        /// The drawing of the dimension tests of a version, its words written as given in an MTEXT, a TEXT, the ATTRIB of an
        /// INSERT, a rotated dimension, a MULTILEADER and the cell (2, 0) of its table, as the file holds them.
        /// </summary>
        private static DxfDatabase Holding(string version, string written)
        {
            DxfDatabase db = DxfDatabase.Parse(version == "R2000" ? DxfDimensionDrawings.R2000 : DxfDimensionDrawings.R2018);
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
            db.ModelSpace.OfType<DxfTable>().First().SetTextString(2, 0, Placeholder);
            string text = db.ToText();
            Assert.True(text.Split('\n').Count(line => line == Placeholder) >= 6);
            return DxfDatabase.Parse(text.Replace("\n" + Placeholder + "\n", "\n" + written + "\n"));
        }

        /// <summary>The words of each text of the drawing <see cref="Holding"/> makes: MTEXT, TEXT, ATTRIB, dimension, MULTILEADER, table cell and its value.</summary>
        private static string[] Words(DxfDatabase db)
        {
            DxfTable table = db.ModelSpace.OfType<DxfTable>().First();

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

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void EveryEscapeOfFourDigits_IsReadAsTheLetterItStandsFor_InEveryText_AsAutoCadReadsIt(string version)
        {
            Assert.All(Words(Holding(version, Written)), words => Assert.Equal(Read, words));
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheEscapeOfNought_IsKeptAsWritten_AsAutoCadKeepsIt(string version)
        {
            Assert.All(Words(Holding(version, "x\\U+0000y")), words => Assert.Equal("x\\U+0000y", words));
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheWordsOfAnMText_AreReadWithItsEscapesAsLetters_BeforeItsFormatting(string version)
        {
            // AutoCAD's MText.Text: the backslash before an escape stays, the escape of a backslash makes a new paragraph.
            DxfMText mtext = Holding(version, Written).ModelSpace.OfType<DxfMText>().Single();

            Assert.Equal("a\\Đb A Đ Đ0 \n \uD83D \\U+00G1 \\U+01", mtext.Text);
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheEscapeOfALineFeed_IsALineFeedInTheWords_AndTheWordsGivenBackChangeNothing(string version)
        {
            DxfDatabase db = Holding(version, "a\\U+000Ab");
            DxfMText mtext = db.ModelSpace.OfType<DxfMText>().Single();
            DxfRotatedDimension dimension = db.ModelSpace.OfType<DxfRotatedDimension>().Single(d => d.DimBlock?.Name == "GH_DRAWN");
            string before = db.ToText();

            Assert.Equal("a\nb", mtext.Contents);
            Assert.Equal("a\nb", dimension.DimensionText);
            mtext.Contents = mtext.Contents;
            dimension.DimensionText = dimension.DimensionText;
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AnMTextOfR2004AsAutoCadWritesIt_AnEscapeCutBetweenTwoPieces_ReadsWhole()
        {
            // AutoCAD 2021 writes the pieces of 250 characters of the escaped words, cutting an escape: \U+01 | 10.
            string escapes = string.Concat(Enumerable.Repeat("\\U+0110", 40));
            DxfDatabase db = DxfDatabase.Parse(new DxfDatabase("AC1018").ToText());
            db.ModelSpace.AppendEntity(new DxfMText { Contents = Placeholder, Location = P(0, 0), TextHeight = 2.5 });
            string text = db.ToText().Replace("  1\n" + Placeholder + "\n", "  3\n" + escapes.Substring(0, 250) + "\n  1\n" + escapes.Substring(250) + "\n");
            Assert.EndsWith("\\U+01", escapes.Substring(0, 250));
            DxfMText mtext = DxfDatabase.Parse(text).ModelSpace.OfType<DxfMText>().Single();

            Assert.Equal(new string('Đ', 40), mtext.Contents);
            Assert.Equal(new string('Đ', 40), mtext.Text);
        }

        [Fact]
        public void ALetterBeyondUFFFF_IsWrittenAsTheEscapesOfItsHalves_InAFileOfR2018_AndReadBackAsTheLetter()
        {
            // AutoCAD 2021 refuses a DXF of R2018 holding the four bytes of UTF-8 of U+1F600 (eFilerError) and reads its escapes.
            string face = char.ConvertFromUtf32(0x1F600);
            var db = new DxfDatabase("AC1032");
            db.ModelSpace.AppendEntity(new DxfMText { Contents = "x" + face + "yĐ", Location = P(0, 0), TextHeight = 2.5 });
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = "x" + face + "yĐ", Position = P(0, 10), Height = 2.5 });

            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                byte[] bytes = stream.ToArray();

                Assert.DoesNotContain(bytes, b => b >= 0xF0);
                Assert.Equal(2, db.ToText().Split('\n').Count(line => line == "x\\U+D83D\\U+DE00yĐ"));
                stream.Position = 0;
                DxfDatabase read = DxfDatabase.Open(stream);
                Assert.Equal("x" + face + "yĐ", read.ModelSpace.OfType<DxfMText>().Single().Contents);
                Assert.Equal("x" + face + "yĐ", read.ModelSpace.OfType<DxfDBText>().Single().TextString);
            }
        }

        [Fact]
        public void TheObjectsOfADrawingCountedWhileOtherThreadsLookEntitiesUpByHandle_AreCountedAsAlone()
        {
            // ApproxNumObjects makes the entities by handle under the lock ItemByHandle takes, as EntityByHandle does.
            var made = new DxfDatabase("AC1032");

            for (int i = 0; i < 300; i++)
            {
                made.ModelSpace.AppendEntity(new DxfLine(P(i, 0), P(i, 10)));
            }

            string text = made.ToText();
            DxfDatabase first = DxfDatabase.Parse(text);
            int alone = first.ApproxNumObjects;
            string[] handles = first.ModelSpace.Select(e => e.Handle).ToArray();
            int wrong = 0;
            int thrown = 0;
            string seen = null;

            for (int round = 0; round < 60; round++)
            {
                DxfDatabase shared = DxfDatabase.Parse(text);

                using (var go = new System.Threading.ManualResetEventSlim(false))
                {
                    var threads = Enumerable.Range(0, 8).Select(t => new System.Threading.Thread(() =>
                    {
                        go.Wait();

                        try
                        {
                            if (t % 2 == 0)
                            {
                                if (shared.ApproxNumObjects != alone)
                                {
                                    System.Threading.Interlocked.Increment(ref wrong);
                                }
                            }
                            else
                            {
                                foreach (string handle in handles)
                                {
                                    if (shared.EntityByHandle(handle) == null)
                                    {
                                        System.Threading.Interlocked.Increment(ref wrong);
                                    }
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
