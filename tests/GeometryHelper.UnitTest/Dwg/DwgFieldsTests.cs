using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// SORTENTSTABLE (the draw order of a block), FIELDLIST, FIELD and DATALINK, which the reader and the writer left out
    /// until the fields and links of AutoCAD 2021's own sample drawings were checked: the four types are written into a
    /// drawing in the groups of AutoCAD's DXF, written as a DWG of R2018 and read back to the same records.
    /// </summary>
    public class DwgFieldsTests
    {
        private static readonly string[] Types = { "SORTENTSTABLE", "FIELDLIST", "FIELD", "DATALINK" };

        private static string G(int code, string value) => code.ToString().PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, int value) => G(code, value.ToString());

        /// <summary>A value of a field or a data map in the form of R2007: flags, type, the value, unit, format, text and the end.</summary>
        private static string Value(int flags, int type, string value, string shown = "", string format = "")
        {
            var text = new StringBuilder().Append(G(93, flags)).Append(G(90, type));

            if ((flags & 1) == 0)
            {
                switch (type)
                {
                    case 0:
                    case 1:
                        text.Append(G(91, value));
                        break;
                    case 2:
                        text.Append(G(140, value));
                        break;
                    case 8:
                        text.Append(G(92, value.Length / 2)).Append(G(310, value));
                        break;
                    default:
                        text.Append(G(1, value));
                        break;
                }
            }

            return text.Append(G(94, 0))
                .Append(G(300, format))
                .Append(G(302, shown))
                .Append(G(304, "ACVALUE_END"))
                .ToString();
        }

        private static string Class(string name, string cpp, int flags) =>
            G(0, "CLASS") + G(1, name) + G(2, cpp) + G(3, "ObjectDBX Classes") + G(90, flags) + G(280, 0) + G(281, 0);

        private static string Header(string type, string handle, string owner) =>
            G(0, type) + G(5, handle) + G(102, "{ACAD_REACTORS") + G(330, owner) + G(102, "}") + G(330, owner);

        /// <summary>
        /// The drawing of curves of AutoCAD's own DXF with the four classes listed and, at the end of its objects, the records
        /// the test gives (built from the handles of the first LINE and CIRCLE and of model space), the handle seed above them.
        /// </summary>
        private static string Drawing(Func<string, string, string, string> objects, out string line, out string circle)
        {
            DxfDatabase database = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false));
            string model = database.ModelSpace.Handle;
            line = database.ModelSpace.OfType<DxfLine>().First().Handle;
            circle = database.ModelSpace.OfType<DxfCircle>().First().Handle;
            string text = database.ToText().Replace("\r\n", "\n");
            string classes =
                Class("SORTENTSTABLE", "AcDbSortentsTable", 0)
                + Class("FIELD", "AcDbField", 1152)
                + Class("FIELDLIST", "AcDbFieldList", 1152)
                + Class("DATALINK", "AcDbDataLink", 1152);
            int classesAt = text.IndexOf("\nCLASSES\n", StringComparison.Ordinal);
            int classesEnd = text.IndexOf("  0\nENDSEC\n", classesAt, StringComparison.Ordinal);
            text = text.Insert(classesEnd, classes);
            int objectsAt = text.IndexOf("\nOBJECTS\n", StringComparison.Ordinal);
            int objectsEnd = text.IndexOf("  0\nENDSEC\n", objectsAt, StringComparison.Ordinal);
            string records = objects(model, line, circle);
            text = text.Insert(objectsEnd, records);
            return Regex.Replace(text, @"(\$HANDSEED\n\s*5\n)[0-9A-Fa-f]+", "${1}FFFF");
        }

        private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

        /// <summary>A drawing written as a DWG of R2018 and read back as DXF.</summary>
        private static string RoundTrip(string text, out DwgAnswerKey.Result result)
        {
            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out IDictionary<string, int> leftOut);

            foreach (string type in Types)
            {
                Assert.DoesNotContain(type, leftOut.Keys);
            }

            byte[] back = DwgReader.ToDxf(dwg);
            result = DwgAnswerKey.Compare(Utf8(text), back);
            return Encoding.UTF8.GetString(back).Replace("\r\n", "\n");
        }

        /// <summary>The records of the DXF of one type, each as its groups on one line of code=value.</summary>
        private static List<string> RecordsOf(string dxf, string type)
        {
            string[] lines = dxf.Split('\n');
            var found = new List<string>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() != "0" || lines[i + 1].Trim() != type)
                {
                    continue;
                }

                var groups = new StringBuilder();
                int j = i + 2;

                for (; j + 1 < lines.Length && lines[j].Trim() != "0"; j += 2)
                {
                    groups.Append(lines[j].Trim() + "=" + lines[j + 1].Trim() + " ");
                }

                if (groups.ToString().Contains("100=AcDb") || type == "CLASS")
                {
                    found.Add(groups.ToString().TrimEnd());
                }
            }

            return found;
        }

        private static string Fields(string model, string line, string circle) =>
            // A field of two children... the parent holds the sheet-set values by name, in the order AutoCAD writes them.
            Header("FIELD", "F001", "C")
            + G(100, "AcDbField")
            + G(1, "AcVar")
            + G(2, "\\AcVar Filename \\f \"%tc4%fn2\"")
            + G(90, 1)
            + G(360, "F002")
            + G(97, 1)
            + G(331, line)
            + G(91, 63)
            + G(92, 0)
            + G(94, 59)
            + G(95, 2)
            + G(96, 0)
            + G(300, "")
            + G(93, 3)
            + G(6, "ACFD_FIELDTEXT_CHECKSUM")
            + Value(2, 2, "35788.0")
            + G(6, "Date")
            + Value(2, 8, "D6070A00010002001200250035000000")
            + G(6, "Variable")
            + Value(2, 4, "Filename")
            + G(7, "ACFD_FIELD_VALUE")
            + Value(4, 4, "C:\\plans\\Ground floor.dwg", "Ground floor", "%tc4%fn2")
            + G(301, "Ground floor")
            + G(98, 12)
            + Header("FIELD", "F002", "F001")
            + G(100, "AcDbField")
            + G(1, "_text")
            + G(2, "%<\\_FldIdx 0>%")
            + G(90, 0)
            + G(97, 0)
            + G(91, 63)
            + G(92, 0)
            + G(94, 9)
            + G(95, 2)
            + G(96, 0)
            + G(300, "")
            + G(93, 0)
            + G(7, "ACFD_FIELD_VALUE")
            + Value(3, 0, "0")
            + G(301, "")
            + G(98, 0)
            + Header("FIELDLIST", "F003", "C")
            + G(100, "AcDbIdSet")
            + G(90, 2)
            + G(330, "F001")
            + G(330, "F002")
            + G(100, "AcDbFieldList");

        [Fact]
        public void FieldsAFieldListAndADrawOrder_WrittenAsDwg_ReadBackToTheSameRecords()
        {
            string text = Drawing(
                (model, line, circle) =>
                    Fields(model, line, circle)
                    + Header("SORTENTSTABLE", "F004", "C")
                    + G(100, "AcDbSortentsTable")
                    + G(330, model)
                    + G(331, circle)
                    + G(5, "1B")
                    + G(331, line)
                    + G(5, "1A"),
                out _,
                out _
            );

            string dxf = RoundTrip(text, out DwgAnswerKey.Result result);

            Assert.DoesNotContain(result.Differences, d => Types.Any(t => d.Contains(" " + t + " ")));
            Assert.DoesNotContain(result.Missing.Keys, key => Types.Contains(key));
            Assert.Equal(3, RecordsOf(dxf, "FIELD").Count + RecordsOf(dxf, "FIELDLIST").Count);
            Assert.Single(RecordsOf(dxf, "SORTENTSTABLE"));
        }

        [Fact]
        public void TheDrawOrder_IsReadBack_WithTheSortHandleOfEachEntity()
        {
            string text = Drawing(
                (model, line, circle) =>
                    Header("SORTENTSTABLE", "F004", "C")
                    + G(100, "AcDbSortentsTable")
                    + G(330, model)
                    + G(331, circle)
                    + G(5, "1B")
                    + G(331, line)
                    + G(5, "1A"),
                out string firstLine,
                out string firstCircle
            );

            string dxf = RoundTrip(text, out _);

            string table = Assert.Single(RecordsOf(dxf, "SORTENTSTABLE"));
            Assert.EndsWith(
                "100=AcDbSortentsTable 330="
                    + Handle(text, "SORTENTSTABLE")
                    + " 331="
                    + firstCircle
                    + " 5=1B 331="
                    + firstLine
                    + " 5=1A",
                table
            );
        }

        /// <summary>The block (330 after the marker) of the SORTENTSTABLE of the DXF.</summary>
        private static string Handle(string dxf, string type) =>
            RecordsOf(dxf, type)[0]
                .Split(' ')
                .SkipWhile(g => g != "100=AcDbSortentsTable")
                .Skip(1)
                .First()
                .Substring(4);

        [Fact]
        public void ADrawOrderEntryOfAnEntityNotWritten_IsLeftOutWithItsSortHandle()
        {
            string text = Drawing(
                (model, line, circle) =>
                    Header("SORTENTSTABLE", "F004", "C")
                    + G(100, "AcDbSortentsTable")
                    + G(330, model)
                    + G(331, "FF00")
                    + G(5, "1B")
                    + G(331, line)
                    + G(5, "1A"),
                out string firstLine,
                out _
            );

            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out _);
            string dxf = Encoding.UTF8.GetString(DwgReader.ToDxf(dwg)).Replace("\r\n", "\n");

            string table = Assert.Single(RecordsOf(dxf, "SORTENTSTABLE"));
            Assert.EndsWith("331=" + firstLine + " 5=1A", table);
            Assert.DoesNotContain("FF00", table);
        }

        [Fact]
        public void TheValuesOfAFieldKeptByName_AreReadBack_InTheOrderOfTheirNames()
        {
            // Written in the order Variable, Date, ACFD_FIELDTEXT_CHECKSUM, as a file of AutoCAD 2005 keeps them; DXF, and
            // AutoCAD's reading of the file, give them by name.
            string text = Drawing(
                (model, line, circle) =>
                    Header("FIELD", "F001", "C")
                    + G(100, "AcDbField")
                    + G(1, "AcVar")
                    + G(2, "\\AcVar Filename")
                    + G(90, 0)
                    + G(97, 0)
                    + G(91, 63)
                    + G(92, 0)
                    + G(94, 59)
                    + G(95, 2)
                    + G(96, 0)
                    + G(300, "")
                    + G(93, 3)
                    + G(6, "Variable")
                    + Value(2, 4, "Filename")
                    + G(6, "Date")
                    + Value(2, 1, "5")
                    + G(6, "ACFD_FIELDTEXT_CHECKSUM")
                    + Value(2, 2, "1.5")
                    + G(7, "ACFD_FIELD_VALUE")
                    + Value(3, 0, "0")
                    + G(301, "")
                    + G(98, 0),
                out _,
                out _
            );

            string dxf = RoundTrip(text, out _);

            string field = Assert.Single(RecordsOf(dxf, "FIELD"));
            int checksum = field.IndexOf("6=ACFD_FIELDTEXT_CHECKSUM", StringComparison.Ordinal);
            int date = field.IndexOf("6=Date", StringComparison.Ordinal);
            int variable = field.IndexOf("6=Variable", StringComparison.Ordinal);
            Assert.True(checksum > 0 && checksum < date && date < variable, field);
        }

        [Fact]
        public void ACodeOfMoreThan250Characters_IsGivenInPiecesOf250UnderGroup3_AndTheRestUnder2()
        {
            // A sheet-set field: 600 characters, so two pieces of 250 under 3 and 100 under 2.
            string code = new string('a', 250) + new string('b', 250) + new string('c', 100);
            string text = Drawing(
                (model, line, circle) =>
                    Header("FIELD", "F001", "C")
                    + G(100, "AcDbField")
                    + G(1, "AcSm")
                    + G(3, code.Substring(0, 250))
                    + G(3, code.Substring(250, 250))
                    + G(2, code.Substring(500))
                    + G(90, 0)
                    + G(97, 0)
                    + G(91, 63)
                    + G(92, 0)
                    + G(94, 59)
                    + G(95, 2)
                    + G(96, 0)
                    + G(300, "")
                    + G(93, 0)
                    + G(7, "ACFD_FIELD_VALUE")
                    + Value(3, 0, "0")
                    + G(301, "")
                    + G(98, 0),
                out _,
                out _
            );

            string dxf = RoundTrip(text, out DwgAnswerKey.Result result);

            string field = Assert.Single(RecordsOf(dxf, "FIELD"));
            Assert.Contains(
                "3=" + code.Substring(0, 250) + " 3=" + code.Substring(250, 250) + " 2=" + code.Substring(500),
                field
            );
            Assert.DoesNotContain(result.Differences, d => d.Contains(" FIELD "));
        }

        [Fact]
        public void AFieldOfAnOlderDxf_WithItsFormatApart_IsWrittenAsR2007KeepsIt()
        {
            // The DXF of AutoCAD 2004 gives the format (4) before the options, and the values with no flags, unit or text.
            string text = Drawing(
                (model, line, circle) =>
                    Header("FIELD", "F001", "C")
                    + G(100, "AcDbField")
                    + G(1, "AcVar")
                    + G(2, "\\AcVar Filename \\f \"%tc4%fn2\"")
                    + G(90, 0)
                    + G(97, 0)
                    + G(4, "%tc4%fn2")
                    + G(91, 63)
                    + G(92, 0)
                    + G(94, 59)
                    + G(95, 2)
                    + G(96, 0)
                    + G(300, "")
                    + G(93, 1)
                    + G(6, "Variable")
                    + G(90, 4)
                    + G(1, "Filename")
                    + G(7, "ACFD_FIELD_VALUE")
                    + G(90, 4)
                    + G(1, "C:\\plans\\Ground floor.dwg")
                    + G(301, "Ground floor")
                    + G(98, 12),
                out _,
                out _
            );

            string dxf = RoundTrip(text, out _);

            string field = Assert.Single(RecordsOf(dxf, "FIELD"));
            Assert.DoesNotContain(" 4=", field);
            Assert.Contains(
                "6=Variable 93=2 90=4 1=Filename 94=0 300= 302= 304=ACVALUE_END 7=ACFD_FIELD_VALUE 93=4 90=4 1=C:\\plans\\Ground floor.dwg 94=0 300=%tc4%fn2 302=Ground floor 304=ACVALUE_END 301=Ground floor 98=12",
                field
            );
        }

        private static string Link(string first, string second, int options, bool withData) =>
            Header("DATALINK", "F005", "C")
            + G(100, "AcDbDataLink")
            + G(1, "AcExcel")
            + G(300, "")
            + G(301, "Data Link^JPiping Details^JC:\\data\\mass.xls")
            + G(302, "C:\\data\\mass.xls!Sheet1")
            + G(90, options)
            + G(91, 1835009)
            + G(92, 1)
            + G(170, 2006)
            + G(171, 12)
            + G(172, 18)
            + G(173, 8)
            + G(174, 11)
            + G(175, 6)
            + G(176, 0)
            + G(177, 1)
            + G(93, 0)
            + G(304, "")
            + G(94, 2)
            + G(330, first)
            + G(330, second)
            + G(360, "0")
            + G(305, "CUSTOMDATA")
            + G(1, "DATAMAP_BEGIN")
            + G(90, withData ? 2 : 0)
            + (
                withData
                    ? G(300, "ACEXCEL_CONNECTION_STRING")
                        + G(301, "DATAMAP_VALUE")
                        + Value(2, 4, "C:\\data\\mass.xls!Sheet1")
                        + G(300, "ACEXCEL_SOURCEDATE")
                        + G(301, "DATAMAP_VALUE")
                        + Value(2, 8, "D6070A00010002001200250035000000")
                    : string.Empty
            )
            + G(309, "DATAMAP_END");

        /// <summary>The two handles in the order of their numbers.</summary>
        private static string[] Ascending(string a, string b) =>
            Convert.ToUInt64(a, 16) < Convert.ToUInt64(b, 16) ? new[] { a, b } : new[] { b, a };

        [Theory]
        [InlineData(0, true)]
        [InlineData(5, true)]
        [InlineData(5, false)]
        [InlineData(0, false)]
        public void ADataLink_WithOrWithoutCustomData_IsReadBackToTheSameRecord(int options, bool withData)
        {
            // Of the options, AutoCAD's file holds a bit that says the data map follows and DXF leaves it out of group 90.
            string text = Drawing(
                (model, line, circle) =>
                {
                    string[] targets = Ascending(line, circle);
                    return Link(targets[0], targets[1], options, withData);
                },
                out _,
                out _
            );

            string dxf = RoundTrip(text, out DwgAnswerKey.Result result);

            string link = Assert.Single(RecordsOf(dxf, "DATALINK"));
            Assert.DoesNotContain(result.Differences, d => d.Contains(" DATALINK "));
            Assert.Contains(" 90=" + options + " 91=1835009 92=1 170=2006 ", link);
            Assert.Contains("301=Data Link^JPiping Details^JC:\\data\\mass.xls", link);
            Assert.EndsWith(
                withData
                    ? "305=CUSTOMDATA 1=DATAMAP_BEGIN 90=2 300=ACEXCEL_CONNECTION_STRING 301=DATAMAP_VALUE 93=2 90=4 1=C:\\data\\mass.xls!Sheet1 94=0 300= 302= 304=ACVALUE_END 300=ACEXCEL_SOURCEDATE 301=DATAMAP_VALUE 93=2 90=8 92=16 310=D6070A00010002001200250035000000 94=0 300= 302= 304=ACVALUE_END 309=DATAMAP_END"
                    : "305=CUSTOMDATA 1=DATAMAP_BEGIN 90=0 309=DATAMAP_END",
                link
            );
        }

        [Fact]
        public void TheObjectsADataLinkFills_AreWrittenInTheOrderOfTheirHandles_AsAutoCadsFileKeepsThem()
        {
            string text = Drawing(
                (model, line, circle) =>
                {
                    string[] targets = Ascending(line, circle);
                    return Link(targets[1], targets[0], 0, false);
                },
                out string firstLine,
                out string firstCircle
            );

            byte[] dwg = DwgWriter.ToDwg(text, "AC1032", out _);

            string link = RecordsOf(Encoding.UTF8.GetString(DwgReader.ToDxf(dwg)), "DATALINK").Single();
            string[] targets = Ascending(firstLine, firstCircle);
            Assert.Contains(" 94=2 330=" + targets[0] + " 330=" + targets[1] + " 360=0 ", link);
        }

        [Fact]
        public void ADrawOrderMadeInCode_WhichListsNoClassOfItsOwn_IsWrittenWithTheClassListed()
        {
            // The drawing of curves lists no class SORTENTSTABLE; the table made by CreateDrawOrderTable does not add one.
            DxfDatabase database = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false));
            DxfEntity second = database.ModelSpace.Skip(1).First();
            database.ModelSpace.CreateDrawOrderTable().MoveToBottom(new[] { second });
            byte[] dwg;

            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream);
                dwg = stream.ToArray();
            }

            string dxf = Encoding.UTF8.GetString(DwgReader.ToDxf(dwg)).Replace("\r\n", "\n");

            Assert.Single(RecordsOf(dxf, "CLASS"), c => c.StartsWith("1=SORTENTSTABLE 2=AcDbSortentsTable"));
            string table = Assert.Single(RecordsOf(dxf, "SORTENTSTABLE"));
            Assert.Contains("331=" + second.Handle + " 5=", table);
        }

        [Fact]
        public void TheFieldsOfAWrittenDwg_AreNoLongerCountedAmongTheTypesNotReadYet()
        {
            var logged = new List<string>();
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) => logged.Add(message);
            string dxf;

            try
            {
                string text = Drawing((model, line, circle) => Fields(model, line, circle), out _, out _);
                dxf = Encoding.UTF8.GetString(DwgReader.ToDxf(DwgWriter.ToDwg(text, "AC1032", out _)));
            }
            finally
            {
                GeometryHelperLog.Writer = null;
            }

            Assert.Equal(2, RecordsOf(dxf, "FIELD").Count);
            Assert.DoesNotContain(logged, m => m.Contains("types that are not read yet"));
        }
    }
}
