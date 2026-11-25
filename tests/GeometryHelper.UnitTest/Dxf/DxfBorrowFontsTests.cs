using System;
using System.Collections.Generic;
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
    /// <see cref="DxfDatabase.BorrowFonts(string)"/>: a path's fonts read once for as many scopes as a drawing opens, the path
    /// and fonts put back when a scope ends, and the texts measured in a scope where they are measured by setting
    /// <see cref="DxfDatabase.FontSearchPath"/>, to the bit. The fonts are written by the tests: an SHX of capitals 6 up and 4
    /// along, and <see cref="TrueTypeFile"/>'s .ttf.
    /// </summary>
    public class DxfBorrowFontsTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-9, 1E-9);

        /// <summary>The bytes of a unifont of capitals as txt.shx draws them (the pen down 6 and along 4, lifted, down 6 and along 2), a space and a question mark, 6 above and 2 below.</summary>
        private static byte[] Shx()
        {
            byte[] capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };
            var letters = new SortedDictionary<int, byte[]>
            {
                { ' ', new byte[] { 2, 0x60, 0 } },
                { '?', new byte[] { 1, 0x64, 0x30, 2, 0x6C, 0x20, 0 } },
            };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, capital);
            }

            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
            bytes.AddRange(BitConverter.GetBytes((uint)(letters.Count + 1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
            bytes.AddRange(header);

            foreach (KeyValuePair<int, byte[]> letter in letters)
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

        /// <summary>Two folders of their own, each holding txt.shx and gh.ttf, given to a test, and a third with neither; deleted after it.</summary>
        private static void InFolders(Action<string, string, string> test)
        {
            string root = Path.Combine(Path.GetTempPath(), "gh-borrow-" + Guid.NewGuid().ToString("N"));
            string first = Path.Combine(root, "a");
            string second = Path.Combine(root, "b");
            string empty = Path.Combine(root, "empty");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            Directory.CreateDirectory(empty);

            try
            {
                foreach (string folder in new[] { first, second })
                {
                    File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Shx());
                    File.WriteAllBytes(Path.Combine(folder, "gh.ttf"), TrueTypeFile.Font(TrueTypeFile.Letters()));
                }

                test(first, second, empty);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        /// <summary>A drawing with a style SHX of txt, a style TTF of gh.ttf and a style ROMANS of romans.shx, no fonts looked for.</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1032");
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "SHX", FileName = "txt" });
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "TTF", FileName = "gh.ttf" });
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "ROMANS", FileName = "romans.shx" });
            return db;
        }

        /// <summary>A TEXT in model space, centred on its second point and a hair slanted, so that its box leans on its font.</summary>
        private static DxfDBText Text(DxfDatabase db, string words, string style, int number)
        {
            var text = new DxfDBText
            {
                TextString = words,
                Height = 4 + number % 3,
                Oblique = 0.1 * (number % 4),
                TextStyleName = style,
                HorizontalMode = DxfTextHorizontalMode.TextCenter,
                AlignmentPoint = new GeoPoint3(10 * number, 5 * number, 0),
            };
            db.ModelSpace.AppendEntity(text);
            return text;
        }

        private static long[] Bits(GeoAabb3 box) =>
            new[] { box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z }
                .Select(BitConverter.DoubleToInt64Bits)
                .ToArray();

        [Fact]
        public void TwoBorrowsOfOnePath_ShareOneSetOfFonts_AndASetOfThePathIsAnother()
        {
            InFolders(
                (a, b, none) =>
                {
                    DxfDatabase db = Drawing();
                    DxfFonts first;

                    using (db.BorrowFonts(a))
                    {
                        first = db.Fonts;
                        Assert.NotNull(first);
                        Assert.Equal(a, db.FontSearchPath);
                    }

                    using (db.BorrowFonts(a))
                    {
                        Assert.Same(first, db.Fonts);
                    }

                    db.FontSearchPath = a;
                    Assert.NotSame(first, db.Fonts);
                    DxfFonts set = db.Fonts;
                    db.FontSearchPath = a;
                    Assert.NotSame(set, db.Fonts);
                }
            );
        }

        [Fact]
        public void ABorrowedSetReadsAFontOnce_ThoughItsFileIsGoneBeforeTheNextBorrow()
        {
            InFolders(
                (a, b, none) =>
                {
                    DxfDatabase db = Drawing();
                    long[] before;

                    using (db.BorrowFonts(a))
                    {
                        before = Bits(Text(db, "ABC", "SHX", 3).GeometricExtents);
                    }

                    File.Delete(Path.Combine(a, "txt.shx"));
                    long[] after;

                    using (db.BorrowFonts(a))
                    {
                        after = Bits(Text(db, "ABC", "SHX", 3).GeometricExtents);
                    }

                    Assert.Equal(before, after);

                    // A set of the path reads the folder anew, and the font is not there.
                    db.FontSearchPath = a;
                    Assert.NotEqual(before, Bits(Text(db, "ABC", "SHX", 3).GeometricExtents));
                }
            );
        }

        [Fact]
        public void AFontInNoFolder_IsSaidOnceAtDebugThroughManyBorrows_AndOnceASetOfThePath()
        {
            InFolders(
                (a, b, none) =>
                {
                    DxfDatabase db = Drawing();
                    var written = new List<string>();
                    Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
                    bool enabled = GeometryHelperLog.Enable;

                    try
                    {
                        GeometryHelperLog.Enable = true;
                        GeometryHelperLog.Writer = (level, message, exception) => written.Add(level + " " + message);

                        for (int i = 0; i < 5; i++)
                        {
                            using (db.BorrowFonts(a))
                            {
                                Text(db, "ABC", "ROMANS", 1).GeometricExtents.ToString();
                            }
                        }

                        Assert.Single(written, m => m.Contains("romans.shx"));
                        written.Clear();

                        for (int i = 0; i < 2; i++)
                        {
                            db.FontSearchPath = a;
                            Text(db, "ABC", "ROMANS", 1).GeometricExtents.ToString();
                        }

                        Assert.Equal(2, written.Count(m => m.Contains("romans.shx")));
                    }
                    finally
                    {
                        GeometryHelperLog.Writer = before;
                        GeometryHelperLog.Enable = enabled;
                    }
                }
            );
        }

        [Fact]
        public void DisposingAScope_PutsBackThePathAndTheFontsOfBefore()
        {
            InFolders(
                (a, b, none) =>
                {
                    DxfDatabase db = Drawing();

                    using (db.BorrowFonts(a))
                    {
                        Assert.NotNull(db.Fonts);
                    }

                    Assert.Equal(string.Empty, db.FontSearchPath);
                    Assert.Null(db.Fonts);

                    db.FontSearchPath = b;
                    DxfFonts set = db.Fonts;

                    using (db.BorrowFonts(a))
                    {
                        Assert.NotSame(set, db.Fonts);
                    }

                    Assert.Equal(b, db.FontSearchPath);
                    Assert.Same(set, db.Fonts);

                    using (db.BorrowFonts(string.Empty))
                    {
                        Assert.Equal(string.Empty, db.FontSearchPath);
                        Assert.Null(db.Fonts);
                    }

                    Assert.Same(set, db.Fonts);
                }
            );
        }

        [Fact]
        public void ADisposedScope_PutsNothingBackASecondTime()
        {
            InFolders(
                (a, b, none) =>
                {
                    DxfDatabase db = Drawing();
                    IDisposable scope = db.BorrowFonts(a);
                    scope.Dispose();
                    db.FontSearchPath = b;
                    DxfFonts set = db.Fonts;
                    scope.Dispose();

                    Assert.Equal(b, db.FontSearchPath);
                    Assert.Same(set, db.Fonts);
                }
            );
        }

        [Fact]
        public void NestedBorrows_EachPutBackWhatWasThereWhenItBegan()
        {
            InFolders(
                (a, b, none) =>
                {
                    DxfDatabase db = Drawing();

                    using (db.BorrowFonts(a))
                    {
                        DxfFonts outer = db.Fonts;

                        using (db.BorrowFonts(b))
                        {
                            DxfFonts inner = db.Fonts;
                            Assert.NotSame(outer, inner);
                            Assert.Equal(b, db.FontSearchPath);

                            using (db.BorrowFonts(a))
                            {
                                Assert.Same(outer, db.Fonts);
                            }

                            Assert.Same(inner, db.Fonts);
                            Assert.Equal(b, db.FontSearchPath);
                        }

                        Assert.Same(outer, db.Fonts);
                        Assert.Equal(a, db.FontSearchPath);
                    }

                    Assert.Null(db.Fonts);
                    Assert.Equal(string.Empty, db.FontSearchPath);
                }
            );
        }

        [Fact]
        public void ADifferentPath_IsADifferentSetOfFonts_AndAFolderWithoutTheFontLaysTheTextOutAsWithNone()
        {
            InFolders(
                (a, b, none) =>
                {
                    DxfDatabase db = Drawing();
                    DxfFonts inA;
                    DxfFonts inB;
                    GeoPoint3 measured;

                    using (db.BorrowFonts(a))
                    {
                        inA = db.Fonts;
                        measured = Text(db, "ABC", "SHX", 1).Position;
                    }

                    using (db.BorrowFonts(b))
                    {
                        inB = db.Fonts;
                    }

                    Assert.NotSame(inA, inB);

                    using (db.BorrowFonts(none))
                    {
                        Assert.False(measured.IsEqualTo(Text(db, "ABC", "SHX", 1).Position, Close));
                    }

                    using (db.BorrowFonts(a + ";" + b))
                    {
                        Assert.NotSame(inA, db.Fonts);
                        Assert.NotSame(inB, db.Fonts);
                    }
                }
            );
        }

        [Fact]
        public void TheExtentsOfTextsInSHXAndTrueType_AreTheSameToTheBitBorrowedAsSet()
        {
            InFolders(
                (a, b, none) =>
                {
                    string[] words = { "A", "AVH", "A VH", "VAH", "AVH %%c" };

                    foreach (string style in new[] { "SHX", "TTF" })
                    {
                        DxfDatabase set = Drawing();
                        DxfDatabase borrowing = Drawing();

                        for (int i = 0; i < words.Length * 3; i++)
                        {
                            set.FontSearchPath = a;
                            long[] expected = Bits(Text(set, words[i % words.Length], style, i + 1).GeometricExtents);
                            set.FontSearchPath = string.Empty;
                            long[] actual;

                            using (borrowing.BorrowFonts(a))
                            {
                                actual = Bits(Text(borrowing, words[i % words.Length], style, i + 1).GeometricExtents);
                            }

                            Assert.Equal(expected, actual);
                        }

                        // The fonts measured something: a text of no font is a point, and "AVH" is a box.
                        using (borrowing.BorrowFonts(a))
                        {
                            GeoAabb3 box = Text(borrowing, "AVH", style, 2).GeometricExtents;
                            Assert.True(box.Max.X - box.Min.X > 1.0, style + " " + box);
                        }
                    }
                }
            );
        }

        [Fact]
        public void ABorrowOfNull_Throws_AndOfNoFolder_GivesNoFonts()
        {
            DxfDatabase db = Drawing();

            Assert.Throws<ArgumentNullException>(() => db.BorrowFonts(null));

            using (db.BorrowFonts("  "))
            {
                Assert.Null(db.Fonts);
                Assert.Equal("  ", db.FontSearchPath);
            }

            Assert.Equal(string.Empty, db.FontSearchPath);
        }
    }
}
