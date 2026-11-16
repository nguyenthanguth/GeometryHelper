using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A table with a number in a cell (a whole number or a real), a general value with nothing in it, or a field in a cell,
    /// written as a DWG of R2000 or R2004. The form of those versions knows only the words a cell shows, so the number is
    /// written as the text it shows (<c>12</c>, <c>2.500000</c>), the empty value as no words and the field as its handle; the
    /// table stays a table, with its round-trip objects, as AutoCAD 2021 writes it. It was written as the block it is drawn with.
    /// </summary>
    public class DwgTableValuesOfOlderVersionsTests
    {
        private static byte[] OursFromR2018(string answer, string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(answer), false)).ToText();
            return DwgWriter.ToDwg(text, version, out _);
        }

        private static List<DwgAnswerKey.Record> RecordsOf(byte[] dwg, params string[] types) =>
            DwgAnswerKey.Parse(DwgReader.ToDxf(dwg)).Where(r => types.Contains(r.Type)).ToList();

        /// <summary>The groups of each cell of a table in the old form: those from each <c>171</c> to the next.</summary>
        private static List<List<KeyValuePair<int, string>>> CellsOf(DwgAnswerKey.Record table)
        {
            var cells = new List<List<KeyValuePair<int, string>>>();

            foreach (KeyValuePair<int, string> group in table.Groups.SkipWhile(g => g.Key != 171))
            {
                if (group.Key == 171)
                {
                    cells.Add(new List<KeyValuePair<int, string>>());
                }

                cells[cells.Count - 1].Add(group);
            }

            return cells;
        }

        /// <summary>The words each cell of a table shows (its group 1; empty where it has none).</summary>
        private static List<string> WordsOf(DwgAnswerKey.Record table) =>
            CellsOf(table)
                .Select(cell => cell.Where(g => g.Key == 1).Select(g => g.Value.Trim()).FirstOrDefault() ?? "")
                .ToList();

        /// <summary>The XRECORDs the data table of the tables holds for their cells, in the order of the cells, as their groups.</summary>
        private static List<List<KeyValuePair<int, string>>> CellRecordsOf(byte[] dwg)
        {
            List<DwgAnswerKey.Record> all = DwgAnswerKey.Parse(DwgReader.ToDxf(dwg));
            DwgAnswerKey.Record data = all.Single(r => r.Type == "ACDBDATATABLE" || r.Type == "DATATABLE");
            List<string> handles = data.Groups.Where(g => g.Key == 360).Select(g => g.Value.Trim()).ToList();

            return handles
                .Select(h =>
                    all.Single(r => r.Type == "XRECORD" && r.Groups.First(g => g.Key == 5).Value.Trim() == h).Groups
                )
                .Select(groups => groups.ToList())
                .ToList();
        }

        private static string Of(List<KeyValuePair<int, string>> groups, int code) =>
            groups.Where(g => g.Key == code).Select(g => g.Value.Trim()).FirstOrDefault();

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void ATableWithANumberInACell_IsATableOfTheVersion_WithTheNumberAsTheTextItShows(string version)
        {
            // tclean: four rows of three cells, a whole number 12 and a real 2.5 in the second column of the last two rows.
            byte[] ours = OursFromR2018("tclean_AC1032", version);
            DwgAnswerKey.Record table = Assert.Single(RecordsOf(ours, "ACAD_TABLE"));
            List<string> words = WordsOf(table);

            Assert.Equal(12, words.Count);
            Assert.Equal(
                new[] { "Clean", "Item", "Count", "bolts", "12", "plates", "2.500000" },
                words.Where(w => w.Length > 0)
            );
            Assert.Equal("12", words[7]);
            Assert.Equal("2.500000", words[10]);

            // The data table holds the checksum of the words shown (the sum of each character times its place from 1) and the
            // value of the cell: its type 1 or 2 and the number, as the cell of the table drawn from it holds it.
            DwgAnswerKey.Record data = Assert.Single(RecordsOf(ours, "ACDBDATATABLE", "DATATABLE"));
            List<double> sums = data
                .Groups.Where(g => g.Key == 40)
                .Select(g => double.Parse(g.Value.Trim(), CultureInfo.InvariantCulture))
                .ToList();

            Assert.Equal(149.0, sums[7]);
            Assert.Equal(1741.0, sums[10]);

            List<List<KeyValuePair<int, string>>> records = CellRecordsOf(ours);

            Assert.Equal(
                ("6", "1", "12", "12"),
                (Of(records[7], 93), Of(records[7], 90), Of(records[7], 91), Of(records[7], 302))
            );
            Assert.Equal(
                ("6", "2", "2.5", "2.500000"),
                (Of(records[10], 93), Of(records[10], 90), Of(records[10], 140), Of(records[10], 302))
            );
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void ATableWithAGeneralValueWithNothingInIt_IsATableOfTheVersion_AndItsCellsShowNoWords(string version)
        {
            // tcustom: five rows of three cells, four of them holding a general value (type 512) with no text.
            byte[] ours = OursFromR2018("tcustom_AC1032", version);
            DwgAnswerKey.Record table = Assert.Single(RecordsOf(ours, "ACAD_TABLE"));
            List<string> words = WordsOf(table);

            Assert.Equal(15, words.Count);
            Assert.Equal(
                new[] { "Styles", "A", "B", "note row", "wide cell", "plain" },
                words.Where(w => w.Length > 0)
            );

            List<List<KeyValuePair<int, string>>> records = CellRecordsOf(ours);

            Assert.Equal(4, records.Count(r => Of(r, 90) == "512"));
            Assert.All(records.Where(r => Of(r, 90) == "512"), r => Assert.Equal("5", Of(r, 93)));
        }

        [Fact]
        public void TheContentOfATableWithANumberAndAGeneralValue_HoldsThemInTheFormOfR2000()
        {
            // In the TABLECONTENT of R2000 a value is its type and the data after it (a number, or the words of a general value
            // as an empty 1), with no flags before it and no unit, format and shown text after.
            List<string> Values(byte[] dwg)
            {
                DwgAnswerKey.Record content = Assert.Single(RecordsOf(dwg, "TABLECONTENT"));
                var values = new List<string>();

                for (int i = 0; i < content.Groups.Count; i++)
                {
                    if (content.Groups[i].Key == 300 && content.Groups[i].Value.Trim() == "VALUE")
                    {
                        values.Add(
                            string.Join(
                                " ",
                                content.Groups.Skip(i + 1).Take(3).Select(g => g.Key + "=" + g.Value.Trim())
                            )
                        );
                    }
                }

                return values;
            }

            // The groups after the data are the 91 that closes the content of the cell.
            List<string> clean = Values(OursFromR2018("tclean_AC1032", "AC1015"));
            List<string> custom = Values(OursFromR2018("tcustom_AC1032", "AC1015"));

            Assert.Contains("90=1 91=12 91=0", clean);
            Assert.Contains("90=2 140=2.5 91=0", clean);
            Assert.Contains("90=512 1= 91=0", custom);
        }

        private static string G(int code, string value) => code.ToString().PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, int value) => G(code, value.ToString());

        private static string FieldHead(string handle, string owner) =>
            G(0, "FIELD")
            + G(5, handle)
            + G(102, "{ACAD_REACTORS")
            + G(330, owner)
            + G(102, "}")
            + G(330, owner)
            + G(100, "AcDbField");

        /// <summary>
        /// The drawing of a table (ttable: four rows of three cells) with a field in the cell of its second row and first
        /// column, as AutoCAD 2021 holds a field of a table: the field of the cell (<c>_text</c>) that names the one that works
        /// out the date (<c>AcVar Date</c>, the year 2026), and the cell with the field's handle (344) and no words.
        /// </summary>
        private static string DrawingWithAField(bool withFields = true)
        {
            string text = DxfDatabase
                .Open(new MemoryStream(DwgAnswerKey.Key("ttable_AC1032"), false))
                .ToText()
                .Replace("\r\n", "\n");
            int table = text.IndexOf("ACAD_TABLE\n", StringComparison.Ordinal);
            string tableHandle = text.Substring(text.IndexOf("  5\n", table, StringComparison.Ordinal) + 4, 4).Trim();

            // The cell at index 3: its 344 after its rotation, its value flags 1 and type 4 with no words.
            int cell = table;

            for (int i = 0; i < 4; i++)
            {
                cell = text.IndexOf("171\n", cell + 1, StringComparison.Ordinal);
            }

            int rotation = text.IndexOf("145\n", cell, StringComparison.Ordinal);
            rotation = text.IndexOf('\n', text.IndexOf('\n', rotation) + 1) + 1;
            text = text.Insert(rotation, G(344, "F5001"));
            int value = text.IndexOf("301\nCELL_VALUE\n", rotation, StringComparison.Ordinal);
            int words = text.IndexOf("  1\n", value, StringComparison.Ordinal);
            int wordsEnd = text.IndexOf("304\nACVALUE_END\n", words, StringComparison.Ordinal);
            text = text.Remove(words, wordsEnd - words).Insert(words, G(94, 0) + G(300, "") + G(302, ""));
            value = text.IndexOf("301\nCELL_VALUE\n", rotation, StringComparison.Ordinal);
            int flags = text.IndexOf("93\n", value, StringComparison.Ordinal);
            text = text.Remove(flags, text.IndexOf('\n', text.IndexOf('\n', flags) + 1) + 1 - flags)
                .Insert(flags, G(93, 1));
            int kind = text.IndexOf(" 90\n", flags, StringComparison.Ordinal);
            text = text.Remove(kind, text.IndexOf('\n', text.IndexOf('\n', kind + 1) + 1) + 1 - kind)
                .Insert(kind, G(90, 4));

            string wrapper =
                FieldHead("F5001", tableHandle)
                + G(1, "_text")
                + G(2, "%<\\_FldIdx 0>%")
                + G(90, 1)
                + G(360, "F5002")
                + G(97, 0)
                + G(91, 63)
                + G(92, 0)
                + G(94, 13)
                + G(95, 2)
                + G(96, 0)
                + G(300, "")
                + G(93, 1)
                + G(6, "ACFD_FIELDTEXT_CHECKSUM")
                + G(93, 2)
                + G(90, 2)
                + G(140, "512.0")
                + G(94, 0)
                + G(300, "")
                + G(302, "")
                + G(304, "ACVALUE_END")
                + G(7, "ACFD_FIELD_VALUE")
                + G(93, 3)
                + G(90, 0)
                + G(94, 0)
                + G(300, "")
                + G(302, "")
                + G(304, "ACVALUE_END")
                + G(301, "")
                + G(98, 0);
            string child =
                FieldHead("F5002", "F5001")
                + G(1, "AcVar")
                + G(2, "\\AcVar Date \\f \"yyyy\"")
                + G(90, 0)
                + G(97, 0)
                + G(91, 32)
                + G(92, 0)
                + G(94, 59)
                + G(95, 2)
                + G(96, 0)
                + G(300, "")
                + G(93, 1)
                + G(6, "Variable")
                + G(93, 2)
                + G(90, 4)
                + G(1, "Date")
                + G(94, 0)
                + G(300, "")
                + G(302, "")
                + G(304, "ACVALUE_END")
                + G(7, "ACFD_FIELD_VALUE")
                + G(93, 4)
                + G(90, 8)
                + G(92, 16)
                + G(94, 0)
                + G(300, "yyyy")
                + G(302, "2026")
                + G(304, "ACVALUE_END")
                + G(301, "2026")
                + G(98, 4);
            int classesEnd = text.IndexOf(
                "  0\nENDSEC\n",
                text.IndexOf("\nCLASSES\n", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            text = text.Insert(
                classesEnd,
                G(0, "CLASS")
                    + G(1, "FIELD")
                    + G(2, "AcDbField")
                    + G(3, "ObjectDBX Classes")
                    + G(90, 1152)
                    + G(280, 0)
                    + G(281, 0)
            );
            int objectsEnd = text.IndexOf(
                "  0\nENDSEC\n",
                text.IndexOf("\nOBJECTS\n", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            return withFields ? text.Insert(objectsEnd, wrapper + child) : text;
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void ATableWithAFieldInACell_IsATableOfTheVersion_WithTheFieldsHandleAndItsValueInTheDataTable(
            string version
        )
        {
            byte[] ours = DwgWriter.ToDwg(DrawingWithAField(), version, out _);
            DwgAnswerKey.Record table = Assert.Single(RecordsOf(ours, "ACAD_TABLE"));
            List<List<KeyValuePair<int, string>>> cells = CellsOf(table);

            // The field's handle stands where the words would, and the cell has none.
            Assert.Equal(12, cells.Count);
            Assert.Equal("F5001", Of(cells[3], 344));
            Assert.DoesNotContain(cells[3], g => g.Key == 1);
            Assert.Single(cells, c => c.Any(g => g.Key == 344));

            // The data table keeps what the field works out (its year, as a date with its format) for the cell, and no checksum.
            List<List<KeyValuePair<int, string>>> records = CellRecordsOf(ours);
            List<string> value = new[] { 93, 90, 94, 300, 302 }.Select(code => Of(records[3], code)).ToList();

            Assert.Equal(new[] { "4", "8", "0", "yyyy", "2026" }, value);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        public void ATableWithAFieldTheDrawingDoesNotHold_IsATableOfTheVersion_WithTheCellAsOneWithNoWords(
            string version
        )
        {
            // A cell can name a field that is not in the file (AutoCAD keeps nothing of it: its own R2000 file of such a table has
            // the cell with no words). The handle written would be a null one and the cell read as one with words, past the end of
            // the data: AutoCAD refused the file (ErrorStatus 53) and the reader gave no table.
            byte[] ours = DwgWriter.ToDwg(DrawingWithAField(false), version, out _);
            DwgAnswerKey.Record table = Assert.Single(RecordsOf(ours, "ACAD_TABLE"));
            List<List<KeyValuePair<int, string>>> cells = CellsOf(table);

            Assert.Equal(12, cells.Count);
            Assert.DoesNotContain(table.Groups, g => g.Key == 344);
            Assert.Equal(string.Empty, Of(cells[3], 1) ?? string.Empty);
        }

        /// <summary>The mask the data table keeps for each cell: the second of its columns of whole numbers.</summary>
        private static List<int> MasksOf(byte[] dwg)
        {
            DwgAnswerKey.Record data = Assert.Single(RecordsOf(dwg, "ACDBDATATABLE", "DATATABLE"));
            var masks = new List<int>();
            int column = 0;

            foreach (KeyValuePair<int, string> group in data.Groups)
            {
                if (group.Key == 92)
                {
                    column++;
                }
                else if (group.Key == 93 && column == 2)
                {
                    masks.Add(int.Parse(group.Value.Trim(), CultureInfo.InvariantCulture));
                }
            }

            return masks;
        }

        [Fact]
        public void TheColoursOfAViewStyleOfAutoCADsOwnR2000File_SurviveItsDxfAndAreWrittenAgainAsTheirIndexes()
        {
            // The section view style of its own of views (handle 0x7F): AutoCAD's R2000 file holds each colour of a view style as
            // its index, which its DXF of R2000 leaves out. The DXF the reader makes of the file holds them (62), and the DWG
            // written from that DXF is AutoCAD's bytes in the object's data (its size and handles are not compared).
            byte[] acad = DwgAnswerKey.Dwg("views_AC1015");
            string text = DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(acad), false)).ToText();
            byte[] ours = DwgWriter.ToDwg(text, "AC1015", out _);

            Assert.Equal(ObjectOf(acad, 0x7F).Skip(2).Take(170), ObjectOf(ours, 0x7F).Skip(2).Take(170));
        }

        /// <summary>The bytes of an object of a file by its handle: its size, its data and its check sum.</summary>
        private static byte[] ObjectOf(byte[] file, ulong handle)
        {
            DwgContainer container = DwgContainer.Read(file);
            long at = container.ObjectMap.Single(pair => pair.Key == handle).Value;
            var reader = new DwgBitReader(container.Objects, at * 8, container.Objects.LongLength * 8);
            long size = reader.ModularShort();

            if (DwgContainer.NumberOf(container.Version) >= 1024)
            {
                reader.ModularChar(false);
            }

            int length = (int)(((reader.Position - (at * 8)) / 8) + size + 2);
            return container.Objects.Skip((int)at).Take(length).ToArray();
        }

        [Theory]
        [InlineData("ttable_AC1032", 12)]
        [InlineData("tstyles_AC1032", 15)]
        [InlineData("tclean_AC1032", 12)]
        public void TheMasksOfTheDataTable_AreNoneForWhatTheStyleAndTheTablesOwnOverridesGive(string answer, int cells)
        {
            // ttable: the header and data rows hold the overrides the table itself gives them (a text style, colours, lineweights
            // of 50, an alignment of 9 and a height of 0.3), and tstyles and tclean a lineweight of 35 on the line under the title
            // and over the header that their table style gives; the cells hold those values in their flags as the cell flags do
            // for any other, and AutoCAD's own R2000 file of each drawing holds a mask of 0 for every cell.
            List<int> masks = MasksOf(OursFromR2018(answer, "AC1015"));

            Assert.Equal(cells, masks.Count);
            Assert.All(masks, mask => Assert.Equal(0, mask));
        }

        [Theory]
        [InlineData("tclean_AC1032", "AC1015")]
        [InlineData("tclean_AC1032", "AC1018")]
        [InlineData("tcustom_AC1032", "AC1015")]
        public void ATableWithANumberOrAGeneralValue_WrittenAsR2000OrR2004AndOpenedAgain_IsATableOfR2018(
            string answer,
            string version
        )
        {
            // The DWG read back gives the table in the form of its version (the overrides of its cells before their rotation, as
            // the reader of a DWG writes them), and saving it as R2018 gives the table, not the block it was drawn with, and the
            // values of its content their flags (6 for a number, 5 for a general value).
            byte[] old = OursFromR2018(answer, version);
            var saved = new MemoryStream();
            DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(old), false)).Save(saved, "AC1032");
            List<DwgAnswerKey.Record> records = DwgAnswerKey.Parse(saved.ToArray());

            Assert.Single(records, r => r.Section == "ENTITIES" && r.Type == "ACAD_TABLE");
            DwgAnswerKey.Record content = Assert.Single(records, r => r.Type == "TABLECONTENT");
            string values = string.Join(
                " ",
                content
                    .Groups.Select(g => g.Key + "=" + g.Value.Trim())
                    .Where(s =>
                        s.StartsWith("93=", StringComparison.Ordinal) || s.StartsWith("90=", StringComparison.Ordinal)
                    )
            );

            if (answer.StartsWith("tclean", StringComparison.Ordinal))
            {
                Assert.Contains("93=6 90=1", values, StringComparison.Ordinal);
                Assert.Contains("93=6 90=2", values, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("93=5 90=512", values, StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData("tclean_AC1032")]
        [InlineData("tcustom_AC1032")]
        public void ATableWithANumberOrAGeneralValue_IsStillATableInR2007(string answer)
        {
            byte[] ours = OursFromR2018(answer, "AC1021");

            Assert.Single(RecordsOf(ours, "ACAD_TABLE"));
        }
    }
}
