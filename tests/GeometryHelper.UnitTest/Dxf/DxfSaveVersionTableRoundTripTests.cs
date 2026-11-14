using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A table saved as another version of DXF across R2007 with what its cells hold beyond text: the overrides of a cell and a
    /// block cell in each form, and the TABLECONTENT and TABLEGEOMETRY AutoCAD keeps under the table's round trip
    /// (ACAD_ROUNDTRIP_2008_TABLE_ENTITY) in the form of the version. The drawing is the one the tests of the data links use
    /// (table F6 of 3 rows by 2 columns: Parts over two columns, Mark with a text style, height and alignment of its own, Qty,
    /// B-12 and a block cell), and the forms are those AutoCAD 2021 wrote of it in R2000, R2004 and R2018.
    /// </summary>
    public class DxfSaveVersionTableRoundTripTests
    {
        #region Helpers

        private static string G(int code, string value) =>
            code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static byte[] Saved(string text, string version, out IReadOnlyList<string> said)
        {
            DxfDatabase db = DxfDatabase.Parse(text);

            using (var stream = new MemoryStream())
            {
                said = db.Save(stream, version);
                return stream.ToArray();
            }
        }

        private static byte[] Saved(string text, string version) => Saved(text, version, out _);

        private static string Text(byte[] dxf) => Encoding.GetEncoding(28591).GetString(dxf);

        private static DwgAnswerKey.Record Record(byte[] dxf, string handle) =>
            DwgAnswerKey.Parse(dxf).Single(r => r.Key == "X:" + handle);

        private static List<DwgAnswerKey.Record> Tables(byte[] dxf) =>
            DwgAnswerKey.Parse(dxf).Where(r => r.Section == "ENTITIES" && r.Type == "ACAD_TABLE").ToList();

        private static string Show(IEnumerable<KeyValuePair<int, string>> groups) =>
            string.Join(" ", groups.Select(g => g.Key + "=" + g.Value.Trim()));

        /// <summary>The cells of a table, each as its groups from its 171 to the next 171 or the end of its own part.</summary>
        private static List<string> Cells(DwgAnswerKey.Record table)
        {
            var cells = new List<List<KeyValuePair<int, string>>>();

            foreach (KeyValuePair<int, string> group in table.Groups)
            {
                if (group.Key == 171)
                {
                    cells.Add(new List<KeyValuePair<int, string>>());
                }

                if (cells.Count > 0 && group.Key < 1000)
                {
                    cells[cells.Count - 1].Add(group);
                }
            }

            return cells.Select(Show).ToList();
        }

        /// <summary>
        /// The drawing of the data links with its first cell of the TABLECONTENT given the text Parts as AutoCAD 2021 wrote the value of
        /// a cell (its 95 of 1, its CONTENT holding a VALUE of the type 4), the others as they are.
        /// </summary>
        private static string Linked
        {
            get
            {
                string text = DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018);
                int at = text.IndexOf(G(0, "TABLECONTENT"), StringComparison.Ordinal);
                at = text.IndexOf(G(95, "0"), at, StringComparison.Ordinal);
                return text.Substring(0, at)
                    + G(95, "1")
                    + G(302, "CONTENT")
                    + G(1, "CELLCONTENT_BEGIN")
                    + G(90, "1")
                    + G(300, "VALUE")
                    + G(93, "6")
                    + G(90, "4")
                    + G(1, "Parts")
                    + G(94, "0")
                    + G(300, string.Empty)
                    + G(302, "Parts")
                    + G(304, "ACVALUE_END")
                    + G(91, "0")
                    + G(309, "CELLCONTENT_END")
                    + G(1, "FORMATTEDCELLCONTENT_BEGIN")
                    + G(170, "0")
                    + G(309, "FORMATTEDCELLCONTENT_END")
                    + text.Substring(at + G(95, "0").Length);
            }
        }

        // The cells of the table F6 in R2018 and in R2004 and R2000, as AutoCAD 2021 wrote them of the same table (the overrides of
        // the cell Mark after its text in the old form, before its 92 in the new one, its 91 and 177 differing by the flag of text).
        private const string Head = "171=1 172=0 173=1 174=0 175=2 176=1 ";
        private const string OldMark =
            "171=1 172=0 173=0 174=0 175=0 176=0 177=49 178=0 145=0.0 1=Mark 7=GH_TXT 140=3.5 170=5";
        private const string OldBlock =
            "171=2 172=0 173=0 174=0 175=0 176=0 177=0 178=0 145=0.0 340=48 144=1.0 179=1 331=4C 300=M12";

        #endregion

        #region The cells

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1015")]
        public void ATableWithOverridesAndABlockCell_GoingToR2004OrR2000_StaysATableInTheOldForm(string version)
        {
            byte[] saved = Saved(DxfDimensionDrawings.R2018, version, out IReadOnlyList<string> said);

            Assert.Empty(said);
            List<string> cells = Cells(Assert.Single(Tables(saved)));

            Assert.Equal(6, cells.Count);
            Assert.Equal(Head + "177=0 178=0 145=0.0 1=Parts", cells[0]);
            Assert.Equal(OldMark, cells[2]);
            Assert.Equal(OldBlock, cells[5]);
            Assert.DoesNotContain(DwgAnswerKey.Parse(saved), r =>
                r.Section == "ENTITIES" && r.Type == "INSERT" && r.Key == "X:F6"
            );
        }

        [Theory]
        [InlineData("AC1021")]
        [InlineData("AC1032")]
        public void ATableOfTheOldFormWithOverridesAndABlockCell_GoingUp_StaysATableInTheNewForm(string version)
        {
            byte[] old = Saved(DxfDimensionDrawings.R2018, "AC1018");
            byte[] saved = Saved(Text(old), version, out IReadOnlyList<string> said);

            Assert.Empty(said);
            List<string> cells = Cells(Assert.Single(Tables(saved)));

            Assert.Equal(6, cells.Count);
            Assert.Equal(
                Head
                    + "91=262144 178=0 145=0.0 92=0 301=CELL_VALUE 93=6 90=4 1=Parts 94=0 300= 302=Parts 304=ACVALUE_END",
                cells[0]
            );
            Assert.Equal(
                "171=1 172=0 173=0 174=0 175=0 176=0 91=262193 178=0 145=0.0 7=GH_TXT 140=3.5 170=5 92=0 301=CELL_VALUE 93=6 90=4 1=Mark 94=0 300= 302=Mark 304=ACVALUE_END",
                cells[2]
            );
            Assert.Equal(
                "171=2 172=0 173=0 174=0 175=0 176=0 91=0 178=0 145=0.0 340=48 144=1.0 179=1 331=4C 300=M12 92=0 301=CELL_VALUE 93=1 90=4 94=0 300= 302= 304=ACVALUE_END",
                cells[5]
            );
        }

        // The cell Qty of the drawing of the data links, with the flags of a border and its groups as AutoCAD 2021 wrote them of a cell
        // with a border of its own: a bottom border of 100 (0x8880, 297088 in the new form and -30592 in the old, which writes the low 16
        // bits as a signed number), a left border (0x2200, which has 0x20000 more in the new form), a top one (0x4440) and a right one with the top
        // (0x5540, which has 0x10000 more).
        private static string QtyWith(string flags, string overrides) =>
            DxfDimensionDrawings.R2018.Replace(
                G(91, "0")
                    + G(178, "0")
                    + G(145, "0.0")
                    + G(92, "0")
                    + G(301, "CELL_VALUE")
                    + G(93, "6")
                    + G(90, "4")
                    + G(1, "Qty"),
                G(91, flags)
                    + G(178, "0")
                    + G(145, "0.0")
                    + overrides
                    + G(92, "0")
                    + G(301, "CELL_VALUE")
                    + G(93, "6")
                    + G(90, "4")
                    + G(1, "Qty")
            );

        [Theory]
        [InlineData("297088", "-30592", "275=100 65=0")]
        [InlineData("401920", "8704", "278=-2 288=1 68=0")]
        [InlineData("279616", "17472", "279=50 69=5")]
        [InlineData("349504", "21824", "276=30 286=1 66=1 279=-2 289=1 69=0")]
        public void ACellWithABorderOfItsOwn_IsTheFlagsAutoCadWritesInEachForm_GoingDownAndUp(
            string flags,
            string old,
            string groups
        )
        {
            string overrides = string.Concat(
                groups
                    .Split(' ')
                    .Select(g => G(int.Parse(g.Split('=')[0], CultureInfo.InvariantCulture), g.Split('=')[1]))
            );
            byte[] down = Saved(QtyWith(flags, overrides), "AC1018", out IReadOnlyList<string> said);

            Assert.Empty(said);
            Assert.Equal(
                "171=1 172=0 173=0 174=0 175=0 176=0 177=" + old + " 178=0 145=0.0 1=Qty " + groups,
                Cells(Assert.Single(Tables(down)))[3]
            );

            byte[] up = Saved(Text(down), "AC1032", out said);

            Assert.Empty(said);
            Assert.Equal(
                "171=1 172=0 173=0 174=0 175=0 176=0 91="
                    + (int.Parse(flags, CultureInfo.InvariantCulture) | 0x40000).ToString(CultureInfo.InvariantCulture)
                    + " 178=0 145=0.0 "
                    + groups
                    + " 92=0 301=CELL_VALUE 93=6 90=4 1=Qty 94=0 300= 302=Qty 304=ACVALUE_END",
                Cells(Assert.Single(Tables(up)))[3]
            );
        }

        [Fact]
        public void ACellOfTheOldFormWithAGroupItHasNoRuleFor_GoingUp_LeavesTheTableTheBlockItIsDrawnWith()
        {
            string text = Text(Saved(DxfDimensionDrawings.R2018, "AC1018"))
                .Replace(G(1, "Qty"), G(1, "Qty") + G(271, "5"));
            byte[] saved = Saved(text, "AC1032", out IReadOnlyList<string> said);

            Assert.Empty(Tables(saved));
            Assert.Contains(said, s => s.Contains("ACAD_TABLE 1"));
        }

        #endregion

        #region The round trip of the table content

        [Fact]
        public void ATableWithATableContent_GoingToR2004_KeepsItsRoundTrip_WithTheValuesInTheOldForm()
        {
            byte[] saved = Saved(Linked, "AC1018", out IReadOnlyList<string> said);

            Assert.Empty(said);
            DwgAnswerKey.Record table = Assert.Single(Tables(saved));
            Assert.Contains(table.Groups, g => g.Key == 360 && g.Value == "B01");
            Assert.Contains(Record(saved, "B02").Groups, g =>
                g.Key == 102 && g.Value == "ACAD_ROUNDTRIP_2008_TABLE_ENTITY"
            );
            Assert.Contains(Record(saved, "B02").Groups, g => g.Key == 360 && g.Value == "B03");

            DwgAnswerKey.Record content = Record(saved, "B03");
            Assert.Equal("TABLECONTENT", content.Type);
            Assert.DoesNotContain(content.Groups, g => g.Key == 304);
            Assert.DoesNotContain(content.Groups, g => g.Key == 93 && g.Value == "6");

            // A value in the old form: its type (90) and its data, no flags (93) before and no 94, 300, 302 and 304 after.
            string shown = Show(content.Groups);
            Assert.Contains("300=VALUE 90=4 1=Parts 91=0 309=CELLCONTENT_END", shown, StringComparison.Ordinal);
            Assert.Contains("301=DATAMAP_VALUE 90=2 140=0.0 309=DATAMAP_END", shown, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("AC1021")]
        [InlineData("AC1032")]
        public void ATableContentOfTheOldForm_GoingUp_IsTheOneItWasMadeFrom(string version)
        {
            byte[] original = Encoding.GetEncoding(28591).GetBytes(Linked);
            byte[] old = Saved(Linked, "AC1018");
            byte[] saved = Saved(Text(old), version, out IReadOnlyList<string> said);

            Assert.Empty(said);
            Assert.Equal(Show(Record(original, "B03").Groups), Show(Record(saved, "B03").Groups));
            Assert.Equal(Show(Record(original, "B04").Groups), Show(Record(saved, "B04").Groups));
            Assert.Equal(Show(Record(original, "B02").Groups), Show(Record(saved, "B02").Groups));
            Assert.Contains(Tables(saved).Single().Groups, g => g.Key == 360 && g.Value == "B01");
        }

        [Fact]
        public void TheDataTableOfThePre2007RoundTrip_GoesWhenTheTableGoesUp_AndTheRoundTripOfItsContentStays()
        {
            // AutoCAD writes after the table content's pointers the part of the form before R2007: the data table (data table,
            // one XRECORD for each cell) that holds the cells of the form it replaces. A later form has none of it.
            byte[] old = Saved(Linked, "AC1018");
            string text = Text(old)
                .Replace(
                    G(361, "B04"),
                    G(361, "B04")
                        + G(102, "ACAD_ROUNDTRIP_PRE2007_TABLE")
                        + G(90, "3")
                        + G(91, "2")
                        + G(102, "ACAD_ROUNDTRIP_PRE2007_TABLECELL")
                        + G(360, "B7F")
                )
                .Replace(
                    G(0, "ENDSEC") + G(0, "EOF"),
                    G(0, "DATATABLE")
                        + G(5, "B7F")
                        + G(330, "B02")
                        + G(100, "AcDbDataTable")
                        + G(70, "2")
                        + G(90, "4")
                        + G(91, "0")
                        + G(1, string.Empty)
                        + G(0, "ENDSEC")
                        + G(0, "EOF")
                );
            Assert.Contains("DATATABLE", Text(Saved(text, "AC1018")));

            byte[] saved = Saved(text, "AC1032", out IReadOnlyList<string> said);

            Assert.Empty(said);
            Assert.DoesNotContain(DwgAnswerKey.Parse(saved), r => r.Type == "DATATABLE" || r.Type == "ACDBDATATABLE");
            Assert.DoesNotContain(Record(saved, "B02").Groups, g =>
                g.Key == 102 && g.Value.StartsWith("ACAD_ROUNDTRIP_PRE2007", StringComparison.Ordinal)
            );
            Assert.Contains(Record(saved, "B02").Groups, g => g.Key == 361 && g.Value == "B04");
            Assert.Single(Tables(saved));
        }

        [Fact]
        public void ATableWhoseCellsHaveNoForm_TakesItsRoundTripWithItWhenItBecomesABlock()
        {
            // A cell with a field (344) is none of the cells the old form is made of: the table is the INSERT, and its table
            // content, geometry and data link owner go with the extension dictionary.
            string text = Linked.Replace(
                G(145, "0.0") + G(92, "0") + G(301, "CELL_VALUE"),
                G(145, "0.0") + G(344, "AB") + G(92, "0") + G(301, "CELL_VALUE")
            );
            byte[] saved = Saved(text, "AC1018", out IReadOnlyList<string> said);

            Assert.Empty(Tables(saved));
            Assert.Contains(said, s => s.Contains("ACAD_TABLE 1"));
            Assert.DoesNotContain(DwgAnswerKey.Parse(saved), r =>
                r.Type == "TABLECONTENT" || r.Type == "TABLEGEOMETRY"
            );
        }

        #endregion
    }
}
