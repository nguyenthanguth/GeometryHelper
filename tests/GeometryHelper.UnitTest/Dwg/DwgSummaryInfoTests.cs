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
    /// The section <c>AcDb:SummaryInfo</c> of a DWG of R2004 or later, which holds the properties DWGPROPS shows: written as
    /// AutoCAD 2021 writes it and read back, in every version, and the properties of a drawing kept through DWG.
    /// </summary>
    /// <remarks>
    /// The three sections of hex below were read from files AutoCAD 2021 saved (probe GhM2 of the work on DWGPROPS, whose
    /// drawings were made in code): a drawing titled "Plate B-12" about a stiffener by "J. Doe" with the keywords "plate,
    /// steel", the comments "Rev A", the revision 7, the hyperlink base "C:\plans" and the custom properties "Part number" =
    /// "B-12" and "Weight" = "42.5", saved as R2018 and as R2004, and one titled "T" by "Au" that "A. Smith" saved last, revision
    /// 9, hyperlink base "HTTP://X", 1.5 hours of editing, made on the Julian day 2451546.25, saved as R2018. AutoCAD gave the
    /// first two drawings its own clock for both dates, the day 2461361 and 76 875 000 milliseconds (21:21:15).
    /// </remarks>
    public class DwgSummaryInfoTests
    {
        private const string Plate2018 =
            "0B0050006C00610074006500200042002D003100320000000A005300740069006600660065006E0065007200000007004A002E00200044006F00650000000D0070006C006100740065002C00200073007400650065006C000000060052006500760020004100000001000000020037000000090043003A005C0070006C0061006E00730000000000000000000000B18E2500F8049504B18E2500F804950402000C00500061007200740020006E0075006D006200650072000000050042002D00310032000000070057006500690067006800740000000500340032002E00350000000000000000000000";

        private const string Plate2004 =
            "0B00506C61746520422D3132000A005374696666656E65720007004A2E20446F65000D00706C6174652C20737465656C000600526576204100010000020037000900433A5C706C616E73000000000000000000B18E2500F8049504B18E2500F804950402000C0050617274206E756D626572000500422D313200070057656967687400050034322E35000000000000000000";

        private const string Small2018 =
            "0200540000000100000003004100750000000100000001000000090041002E00200053006D006900740068000000020039000000090048005400540050003A002F002F005800000000000000C06552005A68250000974901B18E250058E9960400000000000000000000";

        private static byte[] Bytes(string hex)
        {
            var bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static string Julian(int day, int milliseconds) =>
            (day + (milliseconds / 86400000.0)).ToString("R", CultureInfo.InvariantCulture);

        private static DxfDatabaseSummaryInfo PlateInfo() =>
            new DxfDatabaseSummaryInfoBuilder
            {
                Title = "Plate B-12",
                Subject = "Stiffener",
                Author = "J. Doe",
                Keywords = "plate, steel",
                Comments = "Rev A",
                RevisionNumber = "7",
                HyperlinkBase = "C:\\plans",
                CustomPropertyTable =
                {
                    new KeyValuePair<string, string>("Part number", "B-12"),
                    new KeyValuePair<string, string>("Weight", "42.5"),
                },
            }.ToDatabaseSummaryInfo();

        /// <summary>The drawing the first two sections were written for, as the DXF taken apart the DWG writer starts from.</summary>
        private static DwgDrawing PlateDrawing(string version)
        {
            var database = new DxfDatabase(version);
            database.SetHeader("$TDUCREATE", new DxfTypedValue(40, Julian(2461361, 76875000)));
            database.SetHeader("$TDUUPDATE", new DxfTypedValue(40, Julian(2461361, 76875000)));
            database.SummaryInfo = PlateInfo();
            return DwgDrawing.Parse(database.ToText());
        }

        private static byte[] Written(DxfDatabase database, string version)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream, version);
                return stream.ToArray();
            }
        }

        [Fact]
        public void AutoCadsSectionOfR2018_ReadsToItsTextsAndCustomProperties()
        {
            DwgSummaryInfo.Fields fields = DwgSummaryInfo.Read(Bytes(Plate2018), true);

            Assert.Equal(
                new[] { "Plate B-12", "Stiffener", "J. Doe", "plate, steel", "Rev A", string.Empty, "7", "C:\\plans" },
                fields.Texts.Select(t => System.Text.Encoding.UTF8.GetString(t))
            );
            Assert.Equal(
                new[] { "Part number=B-12", "Weight=42.5" },
                fields.Custom.Select(c =>
                    System.Text.Encoding.UTF8.GetString(c.Key) + "=" + System.Text.Encoding.UTF8.GetString(c.Value)
                )
            );
        }

        [Fact]
        public void AutoCadsSectionOfR2004_HoldsTheTextsInTheCodePageAndReadsTheSame()
        {
            DwgSummaryInfo.Fields fields = DwgSummaryInfo.Read(Bytes(Plate2004), false);

            Assert.Equal("Plate B-12", System.Text.Encoding.ASCII.GetString(fields.Texts[0]));
            Assert.Equal("C:\\plans", System.Text.Encoding.ASCII.GetString(fields.Texts[7]));
            Assert.Equal(2, fields.Custom.Count);
            Assert.Equal("Weight", System.Text.Encoding.ASCII.GetString(fields.Custom[1].Key));
        }

        [Fact]
        public void ASectionCutShort_IsReadAsNone_NotAsAnError()
        {
            byte[] whole = Bytes(Plate2018);

            Assert.Null(DwgSummaryInfo.Read(null, true));
            Assert.Null(DwgSummaryInfo.Read(new byte[0], true));
            Assert.Null(DwgSummaryInfo.Read(whole.Take(20).ToArray(), true));
            Assert.Null(DwgSummaryInfo.Read(whole.Take(whole.Length - 30).ToArray(), true));
        }

        [Fact]
        public void TheSectionOfR2018_IsWrittenByteForByteAsAutoCadWroteIt()
        {
            byte[] written = DwgSummaryInfo.Write(PlateDrawing("AC1032"), true);

            Assert.Equal(Bytes(Plate2018), written);
        }

        [Fact]
        public void TheSectionOfR2004_IsWrittenByteForByteAsAutoCadWroteIt_InTheCodePage()
        {
            byte[] written = DwgSummaryInfo.Write(PlateDrawing("AC1018"), false);

            Assert.Equal(Bytes(Plate2004), written);
        }

        [Fact]
        public void WhoSavedItLastTheTimeSpentAndBothDates_AreWrittenAsAutoCadWroteThem()
        {
            var database = new DxfDatabase("AC1032");
            database.SetHeader("$TDUCREATE", new DxfTypedValue(40, "2451546.25"));
            database.SetHeader("$TDUUPDATE", new DxfTypedValue(40, Julian(2461361, 76999000)));
            database.SetHeader("$TDINDWG", new DxfTypedValue(40, "0.0625"));
            database.SetHeader("$LASTSAVEDBY", new DxfTypedValue(1, "A. Smith"));
            database.SummaryInfo = new DxfDatabaseSummaryInfoBuilder
            {
                Title = "T",
                Author = "Au",
                RevisionNumber = "9",
                HyperlinkBase = "HTTP://X",
            }.ToDatabaseSummaryInfo();

            byte[] written = DwgSummaryInfo.Write(DwgDrawing.Parse(database.ToText()), true);

            Assert.Equal(Bytes(Small2018), written);
        }

        [Fact]
        public void TheSectionsPageIsItsLengthUpToTheNext128Bytes_AsAutoCadsAre()
        {
            Assert.Equal(256, DwgSummaryInfo.PageSize(Bytes(Plate2018)));
            Assert.Equal(256, DwgSummaryInfo.PageSize(Bytes(Plate2004)));
            Assert.Equal(128, DwgSummaryInfo.PageSize(Bytes(Small2018)));
            Assert.Equal(128, DwgSummaryInfo.PageSize(new byte[0]));
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void ThePropertiesOfADrawing_SurviveASaveAsDwgAndBack_InEveryVersionThatHasTheSection(string version)
        {
            var database = new DxfDatabase("AC1032");
            database.SummaryInfo = PlateInfo();

            DxfDatabase back = DwgReader.Open(new MemoryStream(Written(database, version), false));

            Assert.Equal(PlateInfo(), back.SummaryInfo);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1032")]
        public void ALetterBeyondAscii_IsKeptInTheCodePageOfR2004AndInUtf16Later(string version)
        {
            var database = new DxfDatabase("AC1032");
            database.SummaryInfo = new DxfDatabaseSummaryInfoBuilder
            {
                Title = "Plaque d\u00E9coup\u00E9e",
                Author = "Ren\u00E9",
                CustomPropertyTable = { new KeyValuePair<string, string>("Mati\u00E8re", "acier") },
            }.ToDatabaseSummaryInfo();

            DxfDatabaseSummaryInfo back = DwgReader
                .Open(new MemoryStream(Written(database, version), false))
                .SummaryInfo;

            Assert.Equal("Plaque d\u00E9coup\u00E9e", back.Title);
            Assert.Equal("Ren\u00E9", back.Author);
            Assert.Equal("Mati\u00E8re", Assert.Single(back.CustomProperties).Key);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1032")]
        public void TheFileHoldsTheSectionAsWritten_AndItsDatesComeFromTheHeader(string version)
        {
            var database = new DxfDatabase("AC1032");
            database.SetHeader("$TDUCREATE", new DxfTypedValue(40, "2451546.25"));
            database.SetHeader("$TDUUPDATE", new DxfTypedValue(40, "2451548.5"));
            database.SummaryInfo = PlateInfo();
            byte[] file = Written(database, version);

            byte[] section = DwgContainer.Read(file).SummaryInfo;

            Assert.NotNull(section);
            DwgSummaryInfo.Fields fields = DwgSummaryInfo.Read(section, version != "AC1018");
            Assert.Equal(8, fields.Texts.Count);
            Assert.Equal(2, fields.Custom.Count);

            // The days and milliseconds after the eight texts: nought of editing, then the two dates.
            int at = TextsEnd(section, version != "AC1018");
            Assert.Equal(new[] { 0, 0, 2451546, 21600000, 2451548, 43200000 }, Wholes(section, at, 6));
        }

        [Fact]
        public void ADrawingOfR2000HasNoSection_AndKeepsItsPropertiesAmongItsObjects()
        {
            var database = new DxfDatabase("AC1015");
            database.SummaryInfo = PlateInfo();

            byte[] file = Written(database, "AC1015");
            DxfDatabase back = DwgReader.Open(new MemoryStream(file, false));

            Assert.Null(DwgContainer.Read(file).SummaryInfo);
            Assert.Equal(PlateInfo().Title, back.SummaryInfo.Title);
            Assert.Equal(PlateInfo().CustomProperties, back.SummaryInfo.CustomProperties);
        }

        [Fact]
        public void ADrawingWithNoProperties_WritesASectionOfEmptyTexts_AndReadsBackEmpty()
        {
            var database = new DxfDatabase("AC1032");

            byte[] file = Written(database, "AC1032");

            Assert.NotNull(DwgContainer.Read(file).SummaryInfo);
            Assert.Equal(DxfDatabaseSummaryInfo.Empty, DwgReader.Open(new MemoryStream(file, false)).SummaryInfo);
        }

        [Fact]
        public void ACustomPropertyIsKeptInTheOrderOfTheHeader_EvenWhenAnotherCarriesTheSameValue()
        {
            var database = new DxfDatabase("AC1032");
            database.SummaryInfo = new DxfDatabaseSummaryInfoBuilder
            {
                CustomPropertyTable =
                {
                    new KeyValuePair<string, string>("B", "same"),
                    new KeyValuePair<string, string>("A", "same"),
                    new KeyValuePair<string, string>("C", string.Empty),
                },
            }.ToDatabaseSummaryInfo();

            DxfDatabaseSummaryInfo back = DwgReader
                .Open(new MemoryStream(Written(database, "AC1032"), false))
                .SummaryInfo;

            Assert.Equal(new[] { "B=same", "A=same", "C=" }, back.CustomProperties.Select(p => p.Key + "=" + p.Value));
        }

        /// <summary>Where the eight texts of a section end.</summary>
        private static int TextsEnd(byte[] section, bool unicode)
        {
            int at = 0;

            for (int i = 0; i < 8; i++)
            {
                int length = section[at] | (section[at + 1] << 8);
                at += 2 + (length * (unicode ? 2 : 1));
            }

            return at;
        }

        private static int[] Wholes(byte[] data, int at, int count)
        {
            var numbers = new int[count];

            for (int i = 0; i < count; i++)
            {
                numbers[i] = BitConverter.ToInt32(data, at + (4 * i));
            }

            return numbers;
        }
    }
}
