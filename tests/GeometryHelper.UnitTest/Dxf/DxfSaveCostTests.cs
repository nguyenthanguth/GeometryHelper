using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What was done to make saving a drawing with tables made in code cheap, held to give what it gave before: the completed copy
    /// of a drawing kept between two saves is dropped by a change; the bytes of a group written without an array for it are those
    /// written with one; the converter reads a group's code as an integer is read from the line with its white space cut off; and a
    /// cell's text longer than 256 letters or beyond ASCII is saved to the byte as it was (the SHA-256 of what the library wrote
    /// before the change, as R2018, R2013 and R2000).
    /// </summary>
    public class DxfSaveCostTests
    {
        #region Helpers

        private static DxfDatabase Metric()
        {
            var db = new DxfDatabase("AC1032");
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));

            if (db.TableStyleDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_TXT", FileName = "arial.ttf" });
            return db;
        }

        /// <summary>A drawing with a table of 3 rows and 2 columns (9 high, 30 wide) with two cells given a text.</summary>
        private static DxfDatabase Drawing(string first, string second, out DxfTable table)
        {
            DxfDatabase db = Metric();
            table = new DxfTable(new GeoPoint3(0.0, 0.0, 0.0), 3, 2, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            table.SetTextString(1, 0, first);
            table.SetTextString(2, 1, second);
            return db;
        }

        private static byte[] Saved(DxfDatabase db, string version = null)
        {
            using (var stream = new MemoryStream())
            {
                if (version == null)
                {
                    db.Save(stream);
                }
                else
                {
                    db.Save(stream, version);
                }

                return stream.ToArray();
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }

        #endregion

        #region The completed copy kept between saves

        [Fact]
        public void ADrawingSavedTwiceUnchanged_GivesTheSameBytes_NativeAndInAnOlderVersion()
        {
            DxfDatabase db = Drawing("STEEL", "42", out _);

            byte[] first = Saved(db);
            byte[] second = Saved(db);
            byte[] olderFirst = Saved(db, "AC1027");
            byte[] olderSecond = Saved(db, "AC1027");

            Assert.Equal(first, second);
            Assert.Equal(olderFirst, olderSecond);
            Assert.Contains("TABLECONTENT", Encoding.UTF8.GetString(first));
        }

        [Fact]
        public void ACellChangedBetweenTwoSaves_IsInTheSecondSave_AsInADrawingSavedOnceWithIt()
        {
            DxfDatabase db = Drawing("STEEL", "42", out DxfTable table);
            byte[] before = Saved(db);

            table.SetTextString(1, 0, "CHANGED");
            byte[] after = Saved(db);

            DxfDatabase fresh = Drawing("CHANGED", "42", out _);
            byte[] once = Saved(fresh);

            Assert.NotEqual(before, after);
            Assert.Equal(once, after);
        }

        [Fact]
        public void ALineAddedBetweenTwoSaves_IsInTheSecondSave_WhereTheTablesAreTheSame()
        {
            DxfDatabase db = Drawing("STEEL", "42", out DxfTable table);
            byte[] before = Saved(db);

            table.SetGridColor(
                1,
                0,
                DxfGridLineType.HorizontalBottom,
                DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1)
            );
            byte[] after = Saved(db);

            Assert.NotEqual(before, after);
            Assert.NotEqual(before, Saved(db, "AC1027"));
        }

        #endregion

        #region A group written without an array of its own

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(90)]
        [InlineData(300)]
        [InlineData(999)]
        [InlineData(1000)]
        [InlineData(1071)]
        [InlineData(-1)]
        public void AGroupOfAWholeNumber_IsWrittenAsTheGroupOfItsText(int code)
        {
            foreach (bool padded in new[] { true, false })
            {
                DxfFile file = DxfDatabase.Parse(Lines(padded)).File;

                foreach (int value in new[] { int.MinValue, -5, -1, 0, 1, 7, 10, 999, 1000, 123456789, int.MaxValue })
                {
                    var whole = new DxfFile.Builder(file);
                    var text = new DxfFile.Builder(file);
                    whole.Group(code, value);
                    text.Group(code, value.ToString(CultureInfo.InvariantCulture));

                    Assert.Equal(text.ToArray(), whole.ToArray());
                }
            }
        }

        [Fact]
        public void AGroupWhoseValueIsLongOrBeyondAscii_IsWrittenAsTheFileEncodesIt()
        {
            DxfFile file = DxfDatabase.Parse(Lines(true)).File;
            string text256 = new string('x', 256);
            string text257 = new string('x', 257);
            var b = new DxfFile.Builder(file);
            b.Group(1, text256);
            b.Group(1, text257);
            string written = Encoding.ASCII.GetString(b.ToArray());

            Assert.Equal(2 * (3 + 1 + 1) + 256 + 257, written.Length);
            Assert.Contains("\n" + text256 + "\n", written);
            Assert.Contains("\n" + text257 + "\n", written);

            var accented = new DxfFile.Builder(file);
            accented.Group(1, "café");

            Assert.Equal(Encoding.UTF8.GetBytes("  1\ncafé\n"), accented.ToArray());
        }

        /// <summary>The text of a small drawing as the library writes it, its codes padded or not, every line end a line feed.</summary>
        private static string Lines(bool padded)
        {
            string[] lines = Metric().ToText().Replace("\r\n", "\n").Split('\n');

            for (int i = 0; !padded && i < lines.Length; i += 2)
            {
                lines[i] = lines[i].Trim();
            }

            return string.Join("\n", lines);
        }

        #endregion

        #region The code of a group the converter reads

        [Fact]
        public void ACodeWithASignAndWhiteSpaceAround_IsReadAsTheIntegerOfItsTrimmedLine()
        {
            string[] lines = Lines(true).Split('\n');
            string plain = DxfVersionConverter.Convert(string.Join("\n", lines), 1027, new List<string>());

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                string code = lines[i].Trim();
                lines[i] = (code.StartsWith("-", StringComparison.Ordinal) ? "\t" : "\t+") + code + "  ";
            }

            string signed = DxfVersionConverter.Convert(string.Join("\n", lines), 1027, new List<string>());

            Assert.Equal(plain, signed);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("- 1")]
        [InlineData("1.5")]
        [InlineData("1 2")]
        [InlineData("99999999999")]
        [InlineData("-2147483649")]
        public void ALineThatIsNoCode_IsRefusedByItsNumber(string line)
        {
            string[] lines = Lines(true).Split('\n');
            lines[4] = line;

            var thrown = Assert.Throws<FormatException>(() =>
                DxfVersionConverter.Convert(string.Join("\n", lines), 1027, new List<string>())
            );

            Assert.Equal("Line 5 is not a group code.", thrown.Message);
        }

        [Fact]
        public void ATextThatEndsAfterACodeLine_IsRefusedAtThatLine()
        {
            const string text = "  0\nSECTION\n  2\nHEADER\n  9";

            var thrown = Assert.Throws<FormatException>(() =>
                DxfVersionConverter.Convert(text, 1027, new List<string>())
            );

            Assert.Equal("Line 5 is not a group code.", thrown.Message);
        }

        #endregion

        #region The converter's bytes

        [Theory]
        [InlineData(1015)]
        [InlineData(1018)]
        [InlineData(1021)]
        [InlineData(1024)]
        [InlineData(1027)]
        [InlineData(1032)]
        [InlineData(1009)]
        public void ADrawingAllAscii_IsConvertedToTheUtf8BytesOfItsConvertedText(int target)
        {
            string text = Lines(true);
            string converted = DxfVersionConverter.Convert(text, target, new List<string>());

            byte[] bytes = DxfVersionConverter.ConvertToBytes(
                text,
                target,
                new List<string>(),
                null,
                out int length,
                out string kept,
                out bool whole
            );

            Assert.Null(kept);
            Assert.Equal(new UTF8Encoding(false).GetBytes(converted), bytes.Take(length).ToArray());
            Assert.True(length <= bytes.Length);
        }

        [Theory]
        [InlineData(1021, true)]
        [InlineData(1024, true)]
        [InlineData(1027, true)]
        [InlineData(1032, true)]
        [InlineData(1015, false)]
        [InlineData(1018, false)]
        public void ALayerNamedWithAnAccent_IsKeptAsBytesFromR2007_AndAsTextBefore(int target, bool asBytes)
        {
            DxfDatabase db = Metric();
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "café Đ" });
            string text = db.ToText();
            string converted = DxfVersionConverter.Convert(text, target, new List<string>());

            byte[] bytes = DxfVersionConverter.ConvertToBytes(
                text,
                target,
                new List<string>(),
                null,
                out int length,
                out string kept,
                out bool whole
            );

            if (asBytes)
            {
                Assert.Null(kept);
                Assert.Equal(new UTF8Encoding(false).GetBytes(converted), bytes.Take(length).ToArray());
            }
            else
            {
                Assert.Null(bytes);
                Assert.Equal(converted, kept);
            }
        }

        [Theory]
        [InlineData(1015, true)]
        [InlineData(1018, true)]
        [InlineData(1021, true)]
        [InlineData(1032, true)]
        [InlineData(1009, false)]
        public void TheBytesOfAConversion_AreTheWholeFile_ExceptForR12(int target, bool whole)
        {
            DxfVersionConverter.ConvertToBytes(
                Lines(true),
                target,
                new List<string>(),
                null,
                out _,
                out _,
                out bool isWhole
            );

            Assert.Equal(whole, isWhole);
        }

        [Theory]
        [InlineData("AcDbVertex\n100\nAcDbFaceRecord", false)]
        [InlineData("AcDbFaceRecord", false)]
        [InlineData("AcDbVertex", true)]
        public void AFaceOfAPolyfaceMesh_IsAFileOpeningItAgainMends_AndTheBytesAreNotTheWholeFile(
            string markers,
            bool whole
        )
        {
            string text = Lines(true)
                .Replace("  2\nENTITIES\n", "  2\nENTITIES\n  0\nVERTEX\n100\nAcDbEntity\n100\n" + markers + "\n");

            DxfVersionConverter.ConvertToBytes(text, 1027, new List<string>(), null, out _, out _, out bool isWhole);

            Assert.Equal(whole, isWhole);
        }

        [Fact]
        public void ADrawingSavedInAnOlderVersion_IsTheBytesItsConvertedTextIsKeptAs()
        {
            var db = new DxfDatabase("AC1032");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "café Đ" });
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(5.0, 5.0, 0.0)));

            foreach (string version in new[] { "AC1027", "AC1024", "AC1021", "AC1018", "AC1015" })
            {
                string text = DxfVersionConverter.Convert(db.ToText(), DxfVersions.Number(version), new List<string>());

                var kept = new MemoryStream();
                DxfFile.Parse(text).Save(kept);

                Assert.Equal(kept.ToArray(), Saved(db, version));
            }
        }

        [Theory]
        [InlineData("AC1027")]
        [InlineData("AC1015")]
        [InlineData("AC1009")]
        public void ADrawingSavedToAPathInAnOlderVersion_IsTheBytesSavedToAStream(string version)
        {
            DxfDatabase db = Drawing("STEEL", "café", out _);
            string path = Path.Combine(Path.GetTempPath(), "dxfver10-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                db.Save(path, version);

                Assert.Equal(Saved(db, version), File.ReadAllBytes(path));
                Assert.Equal(Path.GetFullPath(path), db.Filename);
            }
            finally
            {
                File.Delete(path);
            }
        }
        #endregion

        #region A record's extension dictionary spliced in and out

        private static string Said(DxfFile file, DxfItem item)
        {
            var said = new List<string>();

            foreach (DxfTypedValue group in file.GroupsOf(item))
            {
                said.Add(group.TypeCode + "=" + group.Value);
            }

            return string.Join("|", said);
        }

        private static DxfItem Made(DxfFile file, params object[] groups)
        {
            return file.AddObject("XRECORD", null, b =>
            {
                for (int i = 0; i < groups.Length; i += 2)
                {
                    b.Group((int)groups[i], (string)groups[i + 1]);
                }
            });
        }

        private static readonly object[] Reactors = { 102, "{ACAD_REACTORS", 330, "A1", 102, "}" };

        private static object[] Join(params object[][] parts) => parts.SelectMany(p => p).ToArray();

        [Theory]
        [InlineData(true, "\n")]
        [InlineData(false, "\n")]
        [InlineData(true, "\r\n")]
        [InlineData(false, "\r\n")]
        public void AnExtensionDictionary_IsPutAfterTheReactorsAndTakenOut_AsTheRecordsOwnerIsKept(
            bool padded,
            string lineEnd
        )
        {
            DxfFile file = DxfFile.Parse(Lines(padded).Replace("\n", lineEnd));
            DxfItem item = Made(file, Join(Reactors, new object[] { 330, "OWN", 100, "AcDbXrecord", 1, "text" }));
            string h = item.Handle;
            string before = Said(file, item);

            file.SetExtensionDictionary(item, "D1");

            Assert.Equal(
                "5="
                    + h
                    + "|102={ACAD_REACTORS|330=A1|102=}|102={ACAD_XDICTIONARY|360=D1|102=}|330=OWN|100=AcDbXrecord|1=text",
                Said(file, item)
            );

            file.SetExtensionDictionary(item, "D1");
            Assert.Contains("D1", Said(file, item));

            file.SetExtensionDictionary(item, null);
            Assert.Equal(before, Said(file, item));

            file.SetExtensionDictionary(item, "D2", true);
            Assert.Equal(
                "5="
                    + h
                    + "|102={ACAD_XDICTIONARY|360=D2|102=}|102={ACAD_REACTORS|330=A1|102=}|330=OWN|100=AcDbXrecord|1=text",
                Said(file, item)
            );
        }

        [Fact]
        public void ARecordWithNoReactors_IsGivenTheDictionaryRightAfterItsHandle()
        {
            DxfFile file = DxfFile.Parse(Lines(true));
            DxfItem item = Made(file, 330, "OWN", 100, "AcDbXrecord");

            file.SetExtensionDictionary(item, "D1");

            Assert.Equal(
                "5=" + item.Handle + "|102={ACAD_XDICTIONARY|360=D1|102=}|330=OWN|100=AcDbXrecord",
                Said(file, item)
            );
        }

        [Fact]
        public void ARecordOfNothingBeyondItsHandle_IsGivenTheDictionaryAtItsEnd()
        {
            DxfFile file = DxfFile.Parse(Lines(true));
            DxfItem item = Made(file);

            file.SetExtensionDictionary(item, "D1");

            Assert.Equal("5=" + item.Handle + "|102={ACAD_XDICTIONARY|360=D1|102=}", Said(file, item));
        }

        [Fact]
        public void AGroupOfExtendedDataThatSaysXdictionary_IsNotTheRecordsOwn()
        {
            DxfFile file = DxfFile.Parse(Lines(true));
            DxfItem item = Made(file, 330, "OWN", 1001, "APP", 102, "{ACAD_XDICTIONARY", 360, "DX", 102, "}");

            file.SetExtensionDictionary(item, null);
            Assert.Equal("5=" + item.Handle + "|330=OWN|1001=APP|102={ACAD_XDICTIONARY|360=DX|102=}", Said(file, item));

            file.SetExtensionDictionary(item, "D1");
            Assert.Equal(
                "5="
                    + item.Handle
                    + "|102={ACAD_XDICTIONARY|360=D1|102=}|330=OWN|1001=APP|102={ACAD_XDICTIONARY|360=DX|102=}",
                Said(file, item)
            );
        }

        [Fact]
        public void AListOfReactorsThatNeverCloses_LeavesTheRecordAsItWas_AndOneOfTheDictionaryToo()
        {
            DxfFile file = DxfFile.Parse(Lines(true));
            DxfItem open = Made(file, 102, "{ACAD_REACTORS", 330, "A1", 1, "text");
            DxfItem never = Made(file, 330, "OWN", 102, "{ACAD_XDICTIONARY", 360, "DX", 1, "text");
            string openSaid = Said(file, open);
            string neverSaid = Said(file, never);
            string text = file.ToText();

            file.SetExtensionDictionary(open, "D1");
            file.SetExtensionDictionary(never, "D1");
            file.SetExtensionDictionary(never, null);

            Assert.Equal(openSaid, Said(file, open));
            Assert.Equal(neverSaid, Said(file, never));
            Assert.Equal(text, file.ToText());
        }

        [Fact]
        public void AnExtensionDictionaryAmongComments_IsPutStraightAfterTheHandle_TheCommentsKept()
        {
            DxfFile file = DxfFile.Parse(Lines(true).Replace("\n", "\r\n"));
            DxfItem item = Made(file, 999, "note", 330, "OWN", 999, "again", 1, "text");

            file.SetExtensionDictionary(item, "D1");

            string text = file.ToText();
            string at =
                "\r\n  5\r\n"
                + item.Handle
                + "\r\n102\r\n{ACAD_XDICTIONARY\r\n360\r\nD1\r\n102\r\n}\r\n999\r\nnote\r\n";

            Assert.Contains(at, text);
            Assert.Contains("999\r\nagain\r\n", text);

            file.SetExtensionDictionary(item, null);

            Assert.DoesNotContain("D1", file.ToText());
            Assert.Contains(
                "  5\r\n" + item.Handle + "\r\n999\r\nnote\r\n330\r\nOWN\r\n999\r\nagain\r\n  1\r\ntext\r\n",
                file.ToText()
            );
        }

        #endregion

        #region A cell's text longer than 256 letters or beyond ASCII

        // The SHA-256 and the length of what the library saved before the change, for the table of Drawing in R2018, R2013 and R2000.
        [Theory]
        [InlineData(true, null, "10AD4BA0AA463C58D150FBE92E838E13C3EA98CC35298B0B480C82795FF89BAA", 21166)]
        [InlineData(true, "AC1027", "B01E59497E6097748A9B8DCB60CFD9465BA9C882B12D972A8873871F3623AACA", 21028)]
        // C2: a long text in R2000 is now written as AutoCAD's table, not as its block (11254 bytes before).
        [InlineData(true, "AC1015", "ED240BE61E3776742D0A714B76653B675C3F765AF9A80D87FD156276B2BDD287", 21787)]
        [InlineData(false, null, "E8AAD6BB75C75149FFB201C791760FC27FFCE8AEA08D38ED79271FEF76CA924E", 19983)]
        [InlineData(false, "AC1027", "7B308B1C559507A8B0909DD125A1B31C8CEB31610CC74DA02B65F58D772E2F3D", 19845)]
        [InlineData(false, "AC1015", "6AF3A79697AE30BFACB7EB13AED34CBAE04A755351CC517B77D76DC9CD463C8D", 20627)]
        public void ALongTextAndAnAccentedOne_AreSavedAsTheyWereBeforeTheChange(
            bool longText,
            string version,
            string hash,
            int length
        )
        {
            string first = longText ? new string('x', 150) + new string('y', 150) : "café Đ x";
            DxfDatabase db = Drawing(first, longText ? "B" : "C", out _);

            byte[] saved = Saved(db, version);

            Assert.Equal(length, saved.Length);
            Assert.Equal(hash, Hash(saved));
        }

        #endregion
    }
}
