using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// A true colour and a colour of a book do not exist in a DWG of AutoCAD 2000. AutoCAD keeps them as round-trip data (the
    /// extended data <c>RTTcAl</c> of ACAD on an entity, an extension dictionary holding <c>ACAD_XREC_ROUNDTRIP</c> on a
    /// layer, the extended data <c>DBColXD</c> on a DBCOLOR) and takes them back into the colour when it saves the drawing as
    /// R2004 or later. The drawing here is built in code with that data in the groups AutoCAD 2021 was measured to write
    /// (4 layers, 9 entities and a colour of the book DIC, from a DXF saved as AC1015 and read back by our reader); AutoCAD
    /// saving such a file as R2018 gives every colour back, and so does the writer.
    /// </summary>
    public class DwgRoundTripColourTests
    {
        private const string Book = "DIC COLOR GUIDE(R)$DIC 266";

        private static string Lines(params object[] groups)
        {
            var text = new StringBuilder();

            for (int i = 0; i < groups.Length; i += 2)
            {
                text.Append(groups[i]).Append('\n').Append(groups[i + 1]).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>A layer of R2000 whose colour is in the round-trip record <paramref name="record"/>, held by the dictionary before it.</summary>
        private static string Layer(string handle, string name, int index, bool roundTrip) =>
            Lines("0", "LAYER", "5", handle, "330", "1")
            + (
                roundTrip
                    ? Lines(
                        "102",
                        "{ACAD_XDICTIONARY",
                        "360",
                        (Convert.ToInt32(handle, 16) + 0x10).ToString("X"),
                        "102",
                        "}"
                    )
                    : string.Empty
            )
            + Lines(
                "100",
                "AcDbSymbolTableRecord",
                "100",
                "AcDbLayerTableRecord",
                "2",
                name,
                "70",
                "0",
                "62",
                index,
                "6",
                "Continuous",
                "370",
                "-3",
                "390",
                "13"
            );

        /// <summary>The dictionary and the XRECORD AutoCAD writes under a layer: COLORACI, COLORTRUE and, for a book colour, COLORNAME.</summary>
        private static string RoundTrip(string layer, int index, int colour, string name)
        {
            int dictionary = Convert.ToInt32(layer, 16) + 0x10;
            string record = (dictionary + 0x10).ToString("X");
            return Lines(
                    "0",
                    "DICTIONARY",
                    "5",
                    dictionary.ToString("X"),
                    "330",
                    layer,
                    "100",
                    "AcDbDictionary",
                    "280",
                    "1",
                    "281",
                    "1",
                    "3",
                    "ACAD_XREC_ROUNDTRIP",
                    "360",
                    record,
                    "0",
                    "XRECORD",
                    "5",
                    record,
                    "102",
                    "{ACAD_REACTORS",
                    "330",
                    dictionary.ToString("X"),
                    "102",
                    "}",
                    "330",
                    dictionary.ToString("X"),
                    "100",
                    "AcDbXrecord",
                    "280",
                    "1",
                    "102",
                    "COLORACI",
                    "70",
                    index,
                    "102",
                    "COLORTRUE",
                    "90",
                    colour
                ) + (name == null ? string.Empty : Lines("102", "COLORNAME", "1", name));
        }

        /// <summary>An entity of R2000 with the round-trip colour: the flags and index, the colour and the name of its book.</summary>
        private static string Entity(
            string type,
            string handle,
            string owner,
            int index,
            int flags,
            int colour,
            string name,
            string layer,
            string tail
        ) =>
            Lines("0", type, "5", handle, "330", owner, "100", "AcDbEntity", "8", layer, "62", index)
            + tail
            + Lines("1001", "ACAD", "1000", "RTTcAl", "1070", flags, "1071", colour, "1000", name ?? "$$NoName$$");

        private static string Line(
            string handle,
            string owner,
            int index,
            int flags,
            int colour,
            string name,
            double y
        ) =>
            Entity(
                "LINE",
                handle,
                owner,
                index,
                flags,
                colour,
                name,
                "0",
                Lines("100", "AcDbLine", "10", 0.0, "20", y, "30", 0.0, "11", 10.0, "21", y, "31", 0.0)
            );

        private static string Source()
        {
            string text = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018).ToText().Replace("\r\n", "\n");
            string owner = Regex
                .Match(
                    text,
                    @"  5\n(\w+)\n330\n\w+\n100\nAcDbSymbolTableRecord\n100\nAcDbBlockTableRecord\n  2\n\*Model_Space\n"
                )
                .Groups[1]
                .Value;
            Assert.NotEqual(string.Empty, owner);

            string layers =
                Layer("F0001", "LT", 102, true)
                + Layer("F0002", "LB", 125, true)
                + Layer("F0003", "LI", 5, false)
                + Layer("F0004", "LR", 1, true);
            string entities =
                Line("F0005", owner, 102, -32666, -1039480802, null, 1.0)
                + Line("F0006", owner, 125, -16259, -1034706304, Book, 2.0)
                + Line("F0007", owner, 1, -32767, -1023475712, null, 3.0)
                + Line("F0008", owner, 250, -32518, -1036831949, null, 7.0)
                + Entity(
                    "CIRCLE",
                    "F000C",
                    owner,
                    102,
                    -32666,
                    -1039480802,
                    null,
                    "0",
                    Lines("100", "AcDbCircle", "10", 0.0, "20", 10.0, "30", 0.0, "40", 2.0)
                )
                + Entity(
                    "TEXT",
                    "F000D",
                    owner,
                    102,
                    -32666,
                    -1039480802,
                    null,
                    "0",
                    Lines(
                        "100",
                        "AcDbText",
                        "10",
                        0.0,
                        "20",
                        12.0,
                        "30",
                        0.0,
                        "40",
                        2.0,
                        "1",
                        "hello",
                        "100",
                        "AcDbText"
                    )
                );
            string objects =
                RoundTrip("F0001", 102, -1039480802, null)
                + RoundTrip("F0002", 125, -1034706304, Book)
                + RoundTrip("F0004", 1, -1023475712, null)
                + Lines(
                    "0",
                    "DBCOLOR",
                    "5",
                    "F000E",
                    "330",
                    "C",
                    "100",
                    "AcDbColor",
                    "1001",
                    "ACAD",
                    "1000",
                    "DBColXD",
                    "1070",
                    125,
                    "1071",
                    -1034706304,
                    "1000",
                    Book
                );

            if (!text.Contains("\nDBCOLOR\n"))
            {
                text = Insert(
                    text,
                    "CLASSES",
                    Lines(
                        "0",
                        "CLASS",
                        "1",
                        "DBCOLOR",
                        "2",
                        "AcDbColor",
                        "3",
                        "ObjectDBX Classes",
                        "90",
                        1153,
                        "280",
                        0,
                        "281",
                        0
                    )
                );
            }

            text = Insert(text, "LAYER", layers, true);
            text = Insert(text, "ENTITIES", entities);
            return Insert(text, "OBJECTS", objects);
        }

        /// <summary>Puts <paramref name="records"/> before the end of the section (or of the table of layers where <paramref name="table"/>).</summary>
        private static string Insert(string text, string name, string records, bool table = false)
        {
            int start = table
                ? text.LastIndexOf("AcDbLayerTableRecord", StringComparison.Ordinal)
                : text.IndexOf("  2\n" + name + "\n", StringComparison.Ordinal);
            Assert.True(start >= 0, name);
            int at = text.IndexOf(table ? "  0\nENDTAB" : "  0\nENDSEC", start, StringComparison.Ordinal);
            return text.Insert(at, records);
        }

        private static List<DwgAnswerKey.Record> Written()
        {
            return DwgAnswerKey.Parse(DwgReader.ToDxf(DwgWriter.ToDwg(Source(), "AC1032")));
        }

        private static DwgAnswerKey.Record Of(List<DwgAnswerKey.Record> records, string handle) =>
            records.Single(r => r.Key.EndsWith(":" + handle, StringComparison.OrdinalIgnoreCase));

        private static List<string> Colour(DwgAnswerKey.Record record) =>
            record
                .Groups.Where(g => g.Key == 62 || g.Key == 420 || g.Key == 430)
                .Select(g => g.Key + "=" + g.Value.Trim())
                .ToList();

        [Theory]
        [InlineData("F0005", "62=102|420=706590")]
        [InlineData("F0007", "62=1|420=16711680")]
        [InlineData("F0008", "62=250|420=3355443")]
        [InlineData("F000C", "62=102|420=706590")]
        [InlineData("F000D", "62=102|420=706590")]
        [InlineData("F0006", "62=125|420=5481088|430=" + Book)]
        public void AnEntityOfAnR2000FileWithRoundTripColour_IsWrittenToR2018WithTheTrueColour(
            string handle,
            string colour
        )
        {
            DwgAnswerKey.Record entity = Of(Written(), handle);

            Assert.Equal(colour.Split('|'), Colour(entity));
            Assert.DoesNotContain(entity.Groups, g => g.Key == 1000 && g.Value.StartsWith("RTT"));
        }

        [Theory]
        [InlineData("F0001", "62=102|420=706590")]
        [InlineData("F0002", "62=125|420=5481088|430=" + Book)]
        [InlineData("F0003", "62=5")]
        [InlineData("F0004", "62=1|420=16711680")]
        public void ALayerOfAnR2000FileWithRoundTripColour_IsWrittenToR2018WithTheTrueColourAndNoExtensionDictionary(
            string handle,
            string colour
        )
        {
            List<DwgAnswerKey.Record> records = Written();
            DwgAnswerKey.Record layer = Of(records, handle);

            Assert.Equal(colour.Split('|'), Colour(layer));
            Assert.DoesNotContain(layer.Groups, g => g.Key == 102 && g.Value.Contains("XDICTIONARY"));
            Assert.DoesNotContain(records, r =>
                r.Type == "XRECORD" && r.Groups.Any(g => g.Key == 102 && g.Value.Trim() == "COLORTRUE")
            );
        }

        [Fact]
        public void ABookColourOfAnR2000File_IsWrittenToR2018AsAnIndexATrueColourAndTheNames()
        {
            DwgAnswerKey.Record colour = Of(Written(), "F000E");

            Assert.Equal(new[] { "62=125", "420=5481088", "430=" + Book }, Colour(colour));
            Assert.DoesNotContain(colour.Groups, g => g.Key == 1000 && g.Value == "DBColXD");
        }
    }
}
