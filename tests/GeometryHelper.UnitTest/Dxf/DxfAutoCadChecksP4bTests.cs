using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the checks still open on 4 November 2026 (P4b, its own plugin run headless
    /// in accoreconsole): a DWGPROPS of R2000 holds more than ten custom properties, each after the tenth in a group 300 after
    /// their count, as AutoCAD saved twelve and read them back (point 26); and a block given an attribute definition says so
    /// by the flag 2 of its BLOCK, without which AutoCAD took no value a multileader gave the block's attribute (point 61).
    /// Each drawing is one of ezdxf's, or made by the library, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksP4bTests
    {
        /// <summary>The group 70 of the BLOCK of a block of a drawing's text.</summary>
        private static string BlockFlags(string text, string name)
        {
            string marker = "AcDbBlockBegin\n  2\n" + name + "\n 70\n";
            int at = text.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(at >= 0, "no BLOCK " + name);
            int start = at + marker.Length;
            return text.Substring(start, text.IndexOf('\n', start) - start).Trim();
        }

        [Fact]
        public void ABlockGivenAnAttributeDefinition_IsMarkedAsOneOfAttributeDefinitions_AsAutoCadMarksIt()
        {
            // P4b, GHP4BF: AutoCAD 2021's blocks given an attribute definition, plain, constant or preset, and one whose
            // definition was then erased, read HasAttributeDefinitions true and were saved with 70 of 2; one of none, 0; model
            // space given one, 2. The library's GH_TAG of point 61, saved with 70 of 0, AutoCAD read with no attribute: the value
            // B7 its multileader gave MARK was not found (GetBlockAttribute KeyNotFound, SetBlockAttribute eInvalidInput), and
            // EXPLODE gave its INSERT no ATTRIB; the same file with 70 of 2 gave MARK=B7 and an INSERT of the ATTRIB MARK=B7.
            var db = new DxfDatabase("AC1032");
            var plain = new DxfBlockTableRecord { Name = "GH_PLAIN" };
            var constant = new DxfBlockTableRecord { Name = "GH_CONSTANT" };
            var erased = new DxfBlockTableRecord { Name = "GH_ERASED" };
            var none = new DxfBlockTableRecord { Name = "GH_NONE" };

            foreach (DxfBlockTableRecord block in new[] { plain, constant, erased, none })
            {
                db.BlockTable.Add(block);
                block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5));
                Assert.False(block.HasAttributeDefinitions);
            }

            plain.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(-2, -1, 0), "?", "MARK", "Mark", null) { Height = 2.5 });
            constant.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(-2, -1, 0), "C", "MARK", "Mark", null) { Height = 2.5, Constant = true });
            var gone = new DxfAttributeDefinition(new GeoPoint3(-2, -1, 0), "?", "MARK", "Mark", null) { Height = 2.5 };
            erased.AppendEntity(gone);
            gone.Erase();
            db.ModelSpace.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(50, 0, 0), "?", "LOOSE", "Loose", null) { Height = 2.5 });

            Assert.True(plain.HasAttributeDefinitions);
            Assert.True(constant.HasAttributeDefinitions);
            Assert.True(erased.HasAttributeDefinitions);
            Assert.False(none.HasAttributeDefinitions);
            string text = db.ToText();
            Assert.Equal("2", BlockFlags(text, "GH_PLAIN"));
            Assert.Equal("2", BlockFlags(text, "GH_CONSTANT"));
            Assert.Equal("2", BlockFlags(text, "GH_ERASED"));
            Assert.Equal("0", BlockFlags(text, "GH_NONE"));
            Assert.Equal("2", BlockFlags(text, "*Model_Space"));
            Assert.Equal("0", BlockFlags(text, "*Paper_Space"));
            Assert.True(DxfDatabase.Parse(text).BlockTable["GH_PLAIN"].HasAttributeDefinitions);
        }

        // The DWGPROPS of ezdxf's drawing of R2000: Project and Phase in 300 and 301, the eight groups after them empty, a count
        // of nought after the base of the hyperlinks.
        private const string EmptyEight = "302\n=\n303\n=\n304\n=\n305\n=\n306\n=\n307\n=\n308\n=\n309\n=\n";

        private const string CountOfNone = "  1\nhttps://example.com/\n 90\n0\n";

        private static string Name(int i) => "P" + i.ToString(CultureInfo.InvariantCulture);

        private static string Value(int i) => "v" + i.ToString(CultureInfo.InvariantCulture);

        /// <summary>The XRECORD DWGPROPS of a drawing's text, from its marker to the group 0 after it.</summary>
        private static string Record(string text)
        {
            int start = text.IndexOf("DWGPROPS COOKIE", StringComparison.Ordinal);
            int end = text.IndexOf("\n  0\n", start, StringComparison.Ordinal);
            return text.Substring(start, end - start + 1);
        }

        [Fact]
        public void ADwgpropsOfR2000OfTwelveCustomProperties_AsAutoCadSavedOne_ReadsAllTwelve()
        {
            // AutoCAD 2021 saved 26-e as R2000 (v8-26-e-autocad-r2000.dxf) with ten custom properties in 300 to 309, the count
            // 90 12 after the base of the hyperlinks, then P8=v8 and P9=v9 each in a group 300; read again by DxfIn (P4b, GHP4BA)
            // its SummaryInfo gave all twelve.
            string ten = string.Concat(Enumerable.Range(0, 8).Select(i => (302 + i).ToString(CultureInfo.InvariantCulture) + "\n" + Name(i) + "=" + Value(i) + "\n"));
            string text = DxfObjectsEzdxfDrawings.R2000
                .Replace(EmptyEight, ten)
                .Replace(CountOfNone, "  1\nhttps://example.com/\n 90\n12\n300\nP8=v8\n300\nP9=v9\n");
            Assert.Contains("300\nP9=v9\n  0\n", text, StringComparison.Ordinal);

            DxfDatabaseSummaryInfo read = DxfDatabase.Parse(text).SummaryInfo;

            var want = new List<string> { "Project=Grid A", "Phase=2" };
            want.AddRange(Enumerable.Range(0, 10).Select(i => Name(i) + "=" + Value(i)));
            Assert.Equal(want, read.CustomProperties.Select(p => p.Key + "=" + p.Value));
        }

        [Fact]
        public void TwelveCustomPropertiesSetInADrawingOfR2000_AreWrittenAsAutoCadWritesThem_AndReadBack()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2000);
            var builder = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo);

            for (int i = 0; i < 10; i++)
            {
                builder.CustomPropertyTable.Add(new KeyValuePair<string, string>(Name(i), Value(i)));
            }

            db.SummaryInfo = builder.ToDatabaseSummaryInfo();

            // As AutoCAD 2021 wrote twelve in R2000: ten in the groups 300 to 309, their count, then the two more in groups 300.
            string record = Record(db.ToText());
            Assert.Contains("300\nProject=Grid A\n301\nPhase=2\n302\nP0=v0\n", record, StringComparison.Ordinal);
            Assert.Contains("309\nP7=v7\n 40\n0.125\n", record, StringComparison.Ordinal);
            Assert.EndsWith("  1\nhttps://example.com/\n 90\n12\n300\nP8=v8\n300\nP9=v9\n", record, StringComparison.Ordinal);
            DxfDatabaseSummaryInfo read = DxfDatabase.Parse(db.ToText()).SummaryInfo;
            Assert.Equal(12, read.CustomProperties.Count);
            Assert.Equal("P9=v9", read.CustomProperties[11].Key + "=" + read.CustomProperties[11].Value);

            // Set again with one fewer, the record holds the one set: the eleventh alone after the count, the twelfth gone.
            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            var fewer = new DxfDatabaseSummaryInfoBuilder(again.SummaryInfo);
            fewer.CustomPropertyTable.RemoveAt(11);
            again.SummaryInfo = fewer.ToDatabaseSummaryInfo();
            string kept = Record(again.ToText());
            Assert.EndsWith(" 90\n11\n300\nP8=v8\n", kept, StringComparison.Ordinal);
            Assert.DoesNotContain("P9=v9", kept, StringComparison.Ordinal);
            Assert.Equal(11, DxfDatabase.Parse(again.ToText()).SummaryInfo.CustomProperties.Count);
        }
    }
}
