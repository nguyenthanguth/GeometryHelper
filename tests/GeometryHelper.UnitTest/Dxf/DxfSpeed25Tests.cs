using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of tables11: a data link put in or taken away from a table read from a file with its TABLECONTENT, read from
    /// the cells the table keeps of it and written by copying the bytes of the cells it does not change; the first edit of
    /// such a table making no string of the groups of the cells it does not touch; the groups of a table's own record not copied
    /// twice for an edit; a table's handle read from its first groups, not by walking its record. The saved bytes are held to
    /// what the library gave before, the speed to what it allocates, which does not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed25Tests
    {
        // The SHA-256 (24 hex) of what each run of 120 edits saves and says, as the library gave it before the work of tables11.
        private static readonly string[] RunsBefore =
        {
            "AC1032 seed 1 D2AA9A3E2616EF20CEE730B7",
            "AC1032 seed 2 B7ED31BC53CC74709A1959C9",
            "AC1032 seed 3 0602B1F8DCE968B54E99F265",
            "AC1032 seed 4 DDC5B25CD1E884775CD1A768",
            "AC1032 seed 5 DCD10A8799EE15795535BE87",
            "AC1032 seed 6 1784FA562BFD41E1CA9A90F0",
            "AC1032 seed 7 40BD29C0CE1286766038AAA2",
            "AC1032 seed 8 1E556AC9D1D60B3B75BA91E9",
            "AC1027 seed 1 E147B6FCDEC0433B0C168C14",
            "AC1027 seed 2 88A56C51FCD2386E33EE7C0F",
            "AC1027 seed 3 2D30D72870C2D6A66BCA7E37",
            "AC1027 seed 4 DA749075E61F511CCED3B29F",
            "AC1027 seed 5 C1C4DE4453FC4B6306006594",
            "AC1027 seed 6 3117CCF9ACB734CAAE6B2784",
            "AC1027 seed 7 683A1D3D91553DC93D774820",
            "AC1027 seed 8 CCA4A13DA6360F38B3153AA1",
            "AC1021 seed 1 B88222E192D4218E1A8E0BBA",
            "AC1021 seed 2 EAD59D66E8C57E8FF2806BC3",
            "AC1021 seed 3 CE54D4BBA00C044E7CCA063B",
            "AC1021 seed 4 80D6B8145517D743667858CE",
            "AC1021 seed 5 E848D2086CEBC1F8BFFE5AFD",
            "AC1021 seed 6 B34F89CD6AC52D233F81AD1A",
            "AC1021 seed 7 F777B52514FF85B714427DB9",
            "AC1021 seed 8 F63C5F0037C92DF597126CCB",
            "AC1032 comment seed 1 DFE43EF252806BB9333B20F3",
            "AC1032 comment seed 2 5D2878E2720BADAFD37901D8",
            "AC1032 comment seed 3 8ACBAB60AB88ADED49ACEB67",
            "AC1021 comment seed 1 090FCABA997613FA73FC8AA0",
            "AC1021 comment seed 2 31B47CACCA75789A9B1B95E4",
            "AC1021 comment seed 3 1C492EAAB8D882EE7FB8C713",
            "AC1032 extended seed 1 0B6E93CD9B96EA777DD35349",
            "AC1032 extended seed 2 F3F41C50E5D7CF7B78B134C2",
            "AC1032 extended seed 3 3AF985069BF94FC05E8FB320",
            "AC1021 extended seed 1 A11828F00245C02238DD8FFD",
            "AC1021 extended seed 2 9D657F91F116D9CE91A9C32C",
            "AC1021 extended seed 3 6084439DE0AE808FF5575BCE",
            "AC1032 crlf seed 1 15C97B2F6955E9B3B80CB983",
            "AC1032 crlf seed 2 A851ABB103DB3881E491CE33",
            "AC1032 crlf seed 3 29F94C1496D7DEC9D76822AC",
            "AC1021 crlf seed 1 56E9EB4E84957F24A0170CA8",
            "AC1021 crlf seed 2 946247EA6ACC3A4A370377FF",
            "AC1021 crlf seed 3 589641B47969C831254FF894",
            "AC1032 unpadded seed 1 7172A17F36350509437A40F7",
            "AC1032 unpadded seed 2 3C22A38D1FF36C339A65696E",
            "AC1032 unpadded seed 3 C8D746F0D361617A1FE960FF",
            "AC1021 unpadded seed 1 DC556DA4A1260BD84B7B148D",
            "AC1021 unpadded seed 2 6F597C04120A283343B3353C",
            "AC1021 unpadded seed 3 04ED3CD620EC450D8645932D",
            "AC1032 escape seed 1 A439D98E044ACAFEC4CF871E",
            "AC1032 escape seed 2 D19E6766877BAC5C4371CE0C",
            "AC1032 escape seed 3 0602B1F8DCE968B54E99F265",
            "AC1021 escape seed 1 1EDD46550442250F34E10F7C",
            "AC1021 escape seed 2 6B76CF4515706D6DE86B1962",
            "AC1021 escape seed 3 CE54D4BBA00C044E7CCA063B",
            "AC1032 letter seed 1 4202458AF8DF73D02C5C3C34",
            "AC1032 letter seed 2 FDAB3322EB237267123617E1",
            "AC1032 letter seed 3 0602B1F8DCE968B54E99F265",
            "AC1021 letter seed 1 DBD7F74AAD818C801289C527",
            "AC1021 letter seed 2 72215EDA6C132CDA267C57F2",
            "AC1021 letter seed 3 CE54D4BBA00C044E7CCA063B",
        };

        [Fact]
        public void RunsOfEdits_AmongDataLinksOfATableOfAFile_SaveAndSayWhatTheyDidBefore()
        {
            Assert.Equal(RunsBefore, DxfSpeed25Scenarios.Runs());
        }

        [Theory]
        [InlineData("AC1032", 3, 0)]
        [InlineData("AC1032", 7, 0)]
        [InlineData("AC1021", 5, 0)]
        [InlineData("AC1027", 11, 0)]
        [InlineData("AC1032", 4, 1)]
        [InlineData("AC1032", 6, 2)]
        [InlineData("AC1032", 8, 3)]
        [InlineData("AC1021", 9, 4)]
        [InlineData("AC1032", 12, 5)]
        [InlineData("AC1021", 13, 6)]
        public void RunsOfEdits_AmongDataLinks_MakeTheTextOfTheSameEditsOnATableReadAnewEachTime(
            string version,
            int seed,
            int form
        )
        {
            // The form is how the TABLECONTENT stands in the file: as the library writes it, with a comment among its groups,
            // with extended data after, with carriage returns, with its codes not padded, with an escape in a text, and with a
            // letter beyond ASCII in one.
            Assert.Equal(
                DxfSpeed25Scenarios.Run(version, seed, 60, true, 8, 6, form),
                DxfSpeed25Scenarios.Run(version, seed, 60, false, 8, 6, form)
            );
        }

        [Fact]
        public void TenLinksPutInAndTakenAway_OfATableOf1800CellsReadFromAFile_AllocateLessThan200Megabytes()
        {
            // Each link put in or taken away read the groups of the TABLECONTENT of 1.6 MB three or four times and wrote them all
            // again: 1 551 MB for these 20 edits (147 MB now).
            long allocated = DxfSpeed25Scenarios.Allocated(DxfSpeed25Scenarios.LinksPutInAndTakenAway);

            Assert.True(allocated < 200_000_000L, "links allocated " + allocated);
        }

        [Fact]
        public void TheFirstMerge_OfATableOf1800CellsReadFromAFile_AllocatesLessThan32Megabytes()
        {
            // The first merge made a string of every group of the TABLECONTENT, 90 000 of them, to know where its rows and cells
            // stand: 46.3 MB (24.1 MB now, the groups of the cells it leaves alone made only when they are asked for).
            long allocated = DxfSpeed25Scenarios.Allocated(DxfSpeed25Scenarios.FirstMerge, false);

            Assert.True(allocated < 32_000_000L, "first merge allocated " + allocated);
        }

        [Fact]
        public void TwentyHeightsSet_OfATableOf1800Cells_AllocateLessThan48Megabytes()
        {
            // An edit copied the groups of the table's own record twice, the second to compare with them once it was written:
            // 60.4 MB for these 20 edits (41.6 MB now).
            long allocated = DxfSpeed25Scenarios.Allocated(DxfSpeed25Scenarios.HeightsSet);

            Assert.True(allocated < 48_000_000L, "heights allocated " + allocated);
        }

        // The map of the groups of the TABLECONTENT of a table of the form given (see the theory above).
        private static DxfFile.TailMap MapOf(int form, out DxfFile file)
        {
            DxfDatabase db = DxfSpeed25Scenarios.Table("AC1032", 8, 6, form);
            file = db.File;
            string text = db.ToText();
            Match content = Regex.Match(text, "\n *0\r?\nTABLECONTENT\r?\n *5\r?\n([0-9A-Fa-f]+)\r?\n");
            Assert.True(content.Success, "form " + form);
            return db.File.MapTail(db.File.ObjectItem(content.Groups[1].Value));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void AGroupOfATableContent_IsTheCodeAndTextAskedFor_ExactlyWhereItsValueTrimmedIs(int form)
        {
            DxfFile.TailMap map = MapOf(form, out _);
            int escaped = 0;

            for (int i = 0; i < map.Count; i++)
            {
                string value = map.Value(i);
                escaped += value.IndexOf('\\') >= 0 || value.Any(c => c > 0x7E) ? 1 : 0;

                foreach (
                    string asked in new[] { value.Trim(), value, "ROW", "CELL", "TABLECELL_END", string.Empty, "x" }
                )
                {
                    Assert.Equal(value.Trim() == asked, map.Is(i, map.Code(i), asked));
                    Assert.False(map.Is(i, map.Code(i) + 1, asked));
                }
            }

            Assert.True(form < 5 || escaped > 0, "a group with an escape or a letter beyond ASCII, form " + form);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        [InlineData(4, false)]
        [InlineData(5, false)]
        [InlineData(6, true)]
        public void TheGroupsOfATableContent_StandAsTheFileWritesThem_ExactlyWhereWritingThemGivesTheBytes(
            int form,
            bool stands
        )
        {
            // A comment among the groups, a code not padded and an escape (written as the letter it stands for) are not as the
            // file writes a group; extended data after the last group, carriage returns throughout and a letter beyond ASCII in
            // a file of UTF-8 are.
            DxfFile.TailMap map = MapOf(form, out DxfFile file);

            Assert.Equal(stands, file.TailStandsAsWritten(map, 0));
        }

        // The check as it was made before the work of tables11: every group from the one given on written through a builder and
        // the bytes compared with those of the record, which makes a string of every group.
        private static bool StandsWrittenWhole(DxfFile file, DxfFile.TailMap map, int from)
        {
            if (from >= map.Count)
            {
                return true;
            }

            var builder = new DxfFile.Builder(file);

            for (int k = from; k < map.Count; k++)
            {
                builder.Group(map.Code(k), map.Value(k));
            }

            byte[] written = builder.ToArray();
            int start = map.Record.Start + map.Start(from);
            int end = map.Record.Start + map.End(map.Count - 1);

            if (end - start != written.Length || map.End(map.Count - 1) != map.DataEnd)
            {
                return false;
            }

            return !written.Where((b, i) => map.Record.Bytes[start + i] != b).Any();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        public void TheGroupsOfATableContent_StandAsTheFileWritesThem_WhereWritingEveryGroupOfThemGivesTheBytesToo(
            int form
        )
        {
            DxfFile.TailMap map = MapOf(form, out DxfFile file);

            foreach (
                int from in new[] { 0, 1, 5, map.Count / 3, map.Count / 2, map.Count - 2, map.Count - 1, map.Count }
            )
            {
                Assert.Equal(StandsWrittenWhole(file, map, from), file.TailStandsAsWritten(map, from));
            }
        }

        [Fact]
        public void ALinkTakenAwayFromATableOfAFile_LeavesTheTextOfItsCells_AndNoLinkInTheTable()
        {
            DxfDatabase db = DxfSpeed25Scenarios.Table("AC1032", 20, 5);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            var link = (DxfDataLink)db.GetObject("FFF01");
            table.SetTextString(3, 2, "kept");
            table.SetDataLink(new DxfCellRange(3, 1, 4, 2), link);

            Assert.Same(link, table.GetDataLink(4, 2));
            Assert.Equal(new DxfCellRange(3, 1, 4, 2), table.GetDataLinkRange(4, 1));

            table.RemoveDataLink(4, 2);

            Assert.Null(table.GetDataLink(4, 2));
            Assert.Empty(table.GetDataLink());
            Assert.Equal("kept", table.TextString(3, 2));
            Assert.Equal(20, table.NumRows);
        }

        [Fact]
        public void TheHandleOfATable_AfterAnEdit_IsReadFromItsFirstGroups()
        {
            // The handle was found by the walk that finds a look, over every group of the record, after each edit that wrote
            // the record anew: a quarter of the time of a height set (0.26 ms against 1.1) for the 1 MB of the record of 1 800
            // cells. Read from its first groups it takes a hundredth. The time of the reads is held to a ratio of that of the edits.
            double best = double.MaxValue;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                DxfDatabase db = DxfSpeed25Scenarios.Table("AC1032", 300, 6);
                DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
                table.SetTextHeight(0, 1, 2.0);
                string handle = table.Handle;
                var edits = new Stopwatch();
                var reads = new Stopwatch();

                for (int k = 0; k < 60; k++)
                {
                    edits.Start();
                    table.SetRowHeight(40 + k % 20, 9.0 + k % 7);
                    edits.Stop();
                    reads.Start();
                    string read = table.Handle;
                    reads.Stop();
                    Assert.Equal(handle, read);
                }

                best = Math.Min(best, reads.Elapsed.TotalMilliseconds / edits.Elapsed.TotalMilliseconds);
            }

            Assert.True(best < 0.05, "the reads took " + best.ToString("F3") + " of the time of the edits");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TheHandleOfEveryRecordOfADrawing_ReadFromItsFirstGroups_IsTheOneItsLookIsReadWith(bool r2007)
        {
            string text = r2007
                ? DxfDimensionDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\nAC1021\n")
                : DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var items = db.ModelSpace.Select(e => e.Item).ToList();

            foreach (string table in new[] { "LAYER", "LTYPE", "STYLE", "DIMSTYLE", "BLOCK_RECORD", "VPORT", "APPID" })
            {
                items.AddRange(db.File.TableItems(table));
            }

            foreach (Match handle in Regex.Matches(text, "\n  5\n([0-9A-Fa-f]+)\n"))
            {
                DxfItem item = db.File.ObjectItem(handle.Groups[1].Value);

                if (item != null)
                {
                    items.Add(item);
                }
            }

            Assert.True(items.Count > 40, "records found " + items.Count);

            foreach (DxfItem item in items)
            {
                Assert.Equal(item.Handle, db.File.HandleOf(item));
            }
        }
    }
}
