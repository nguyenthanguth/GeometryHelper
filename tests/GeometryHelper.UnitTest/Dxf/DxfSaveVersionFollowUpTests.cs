using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A drawing saved as another version of DXF, the follow-ups to the first rules: the ACIS data of a solid turned from the
    /// SAB of R2013 into the SAT of R2010 and before and back, a table across R2007, the visual styles across R2007, R2010 and
    /// R2013, and the columns of an MTEXT going up to R2018. The answers are AutoCAD 2021's own DXF of the same drawing in each
    /// version (the keys of the DWG tests: acis_ is a box, a sphere, a cylinder and a region in R2004, R2010, R2013 and R2018,
    /// table_ a table of 4 rows and 3 columns with merged cells in R2000, R2004, R2007 and R2018).
    /// </summary>
    public class DxfSaveVersionFollowUpTests
    {
        #region Helpers

        private static byte[] Saved(byte[] file, string version, out IReadOnlyList<string> said)
        {
            DxfDatabase db = Opened(file);

            using (var stream = new MemoryStream())
            {
                said = db.Save(stream, version);
                return stream.ToArray();
            }
        }

        private static byte[] Saved(byte[] file, string version) => Saved(file, version, out _);

        private static List<DwgAnswerKey.Record> Of(byte[] dxf, string section, string type) =>
            DwgAnswerKey.Parse(dxf).Where(r => r.Section == section && r.Type == type).ToList();

        /// <summary>The lines of the SAT of an entity, as the groups 3 and 1 after its AcDbModelerGeometry hold them, deciphered.</summary>
        private static List<string> Sat(DwgAnswerKey.Record entity)
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            bool inside = false;

            foreach (KeyValuePair<int, string> group in entity.Groups)
            {
                if (group.Key == 100)
                {
                    inside = group.Value == "AcDbModelerGeometry";
                }
                else if (inside && (group.Key == 3 || group.Key == 1))
                {
                    line.Append(group.Value);

                    if (group.Key == 1)
                    {
                        lines.Add(DxfUnknownEntity.Plain(line.ToString()));
                        line.Clear();
                    }
                }
            }

            return lines;
        }

        /// <summary>The SAB of an entity, from the record of the data store of its file that names its handle.</summary>
        private static byte[] Sab(byte[] dxf, DwgAnswerKey.Record entity)
        {
            string handle = entity.Groups.First(g => g.Key == 5).Value.Trim();
            DwgAnswerKey.Record data = DwgAnswerKey
                .Parse(dxf)
                .Single(r =>
                    r.Section == "ACDSDATA"
                    && r.Type == "ACDSRECORD"
                    && r.Groups.Any(g => g.Key == 320 && g.Value.Trim() == handle)
                );
            string hex = string.Concat(data.Groups.Where(g => g.Key == 310).Select(g => g.Value.Trim()));
            int size = int.Parse(data.Groups.First(g => g.Key == 94).Value.Trim(), CultureInfo.InvariantCulture);
            var bytes = new byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        private static List<DwgAnswerKey.Record> Solids(byte[] dxf) => Of(dxf, "ENTITIES", "3DSOLID");

        /// <summary>The groups of a record but those a save writes for itself: handles, owners, extension dictionaries, graphics.</summary>
        private static List<KeyValuePair<int, string>> Body(DwgAnswerKey.Record record, params int[] left) =>
            record
                .Groups.Where(g => g.Key != 5 && g.Key != 330 && g.Key != 360 && g.Key != 102 && !left.Contains(g.Key))
                .Select(g => new KeyValuePair<int, string>(g.Key, g.Value.Trim()))
                .ToList();

        private static string Show(List<KeyValuePair<int, string>> groups) =>
            string.Join(" ", groups.Select(g => g.Key + "=" + g.Value));

        #endregion

        #region Solids

        [Fact]
        public void TheSolidsOfR2010_SavedAsR2013_AreTheSabAutoCadWritesOfThem()
        {
            byte[] saved = Saved(DwgAnswerKey.Key("acis_AC1024"), "AC1027", out IReadOnlyList<string> said);
            byte[] key = DwgAnswerKey.Key("acis_AC1027");
            List<DwgAnswerKey.Record> solids = Solids(saved);

            Assert.Equal(3, solids.Count);
            Assert.Empty(said);

            for (int i = 0; i < solids.Count; i++)
            {
                // The SAB is read into SAT again to be compared, which gives the same lines for the same records.
                Assert.Equal(DwgSat.ToSat(Sab(key, Solids(key)[i])), DwgSat.ToSat(Sab(saved, solids[i])));
                Assert.Contains(solids[i].Groups, g => g.Key == 290);
                Assert.Contains(solids[i].Groups, g => g.Key == 2 && g.Value.StartsWith("{", StringComparison.Ordinal));
                Assert.DoesNotContain(solids[i].Groups, g => g.Key == 1);
            }
        }

        [Theory]
        [InlineData("acis_AC1027", "acis_AC1024", "AC1024")]
        [InlineData("acis_AC1032", "acis_AC1024", "AC1024")]
        [InlineData("acis_AC1027", "acis_AC1018", "AC1018")]
        [InlineData("acis_AC1018", "acis_AC1024", "AC1024")]
        [InlineData("acis_AC1024", "acis_AC1018", "AC1018")]
        public void TheSolidsOfOneVersion_SavedAsAnother_AreTheSatAutoCadWritesOfThem(
            string from,
            string answer,
            string version
        )
        {
            byte[] saved = Saved(DwgAnswerKey.Key(from), version, out IReadOnlyList<string> said);
            List<DwgAnswerKey.Record> key = Solids(DwgAnswerKey.Key(answer));
            List<DwgAnswerKey.Record> solids = Solids(saved);

            Assert.Equal(key.Count, solids.Count);
            Assert.Empty(said);

            for (int i = 0; i < key.Count; i++)
            {
                Assert.Equal(Sat(key[i]), Sat(solids[i]));
                Assert.DoesNotContain(solids[i].Groups, g => g.Key == 2 || g.Key == 290);
            }

            // The data store is for R2013 and later alone.
            Assert.DoesNotContain(DwgAnswerKey.Parse(saved), r => r.Section == "ACDSDATA");
        }

        [Fact]
        public void ASolidOfR2013_SavedAsR2018_KeepsItsSabAsItIs()
        {
            byte[] key = DwgAnswerKey.Key("acis_AC1027");
            byte[] saved = Saved(key, "AC1032");

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(Sab(key, Solids(key)[i]), Sab(saved, Solids(saved)[i]));
            }
        }

        [Fact]
        public void ASolidOfR2007AndLater_HasItsHistory_AndOneOfR2004AndBeforeHasNone()
        {
            byte[] sat = DwgAnswerKey.Key("acis_AC1018");

            foreach (string version in new[] { "AC1021", "AC1024", "AC1027" })
            {
                DwgAnswerKey.Record solid = Solids(Saved(sat, version))[0];
                int at = solid.Groups.FindIndex(g => g.Key == 100 && g.Value == "AcDb3dSolid");

                Assert.True(at > 0, version);
                Assert.Equal("350", solid.Groups[at + 1].Key.ToString(CultureInfo.InvariantCulture));
            }

            Assert.DoesNotContain(Solids(Saved(DwgAnswerKey.Key("acis_AC1024"), "AC1018"))[0].Groups, g =>
                g.Key == 100 && g.Value == "AcDb3dSolid"
            );
        }

        [Fact]
        public void ASolidOfR2000_ItsTextBeingACIS4_IsLeftOutOfR2013AndKeptInTheOthers()
        {
            byte[] r2000 = DwgAnswerKey.Key("solids_AC1015");
            int solids = Solids(r2000).Count;

            Assert.True(solids > 0);

            // Going to R2013 the entity cannot keep text: AutoCAD refuses a file whose solid does, as no drawing.
            byte[] as2013 = Saved(r2000, "AC1027", out IReadOnlyList<string> said);
            Assert.Empty(Solids(as2013));
            Assert.Contains(said, s => s.Contains("3DSOLID " + solids.ToString(CultureInfo.InvariantCulture)));

            // To R2010 the text stays, and the solid gets the history that version writes.
            List<DwgAnswerKey.Record> as2010 = Solids(Saved(r2000, "AC1024"));
            Assert.Equal(solids, as2010.Count);
            Assert.All(as2010, solid => Assert.Contains(solid.Groups, g => g.Key == 100 && g.Value == "AcDb3dSolid"));
        }

        [Fact]
        public void ABodyThatIsNotASolid_IsTurnedAsASolidIs()
        {
            // A BODY holds the same data and none of the solid's history.
            byte[] key = DwgAnswerKey.Key("acis_AC1024");
            string text = Encoding.GetEncoding(28591).GetString(key);
            string body = text.Replace("3DSOLID", "BODY");
            DxfDatabase database = DxfDatabase.Parse(body);

            using (var stream = new MemoryStream())
            {
                database.Save(stream, "AC1027");
                List<DwgAnswerKey.Record> bodies = Of(stream.ToArray(), "ENTITIES", "BODY");

                Assert.Equal(3, bodies.Count);
                Assert.All(bodies, b => Assert.Contains(b.Groups, g => g.Key == 290));
            }
        }

        #endregion

        #region The reading of SAT into SAB

        [Theory]
        [InlineData("acis_AC1018", "AC1018")]
        [InlineData("acis_AC1024", "AC1024")]
        [InlineData("splines_AC1018", "AC1018")]
        [InlineData("splines_AC1021", "AC1021")]
        [InlineData("splines_AC1024", "AC1024")]
        public void TheSatOfAutoCad_ReadAsSab_IsWrittenAsSatLineForLine(string key, string version)
        {
            AcisFormat form = AcisFormat.For(version);
            int bodies = 0;

            foreach (DwgAnswerKey.Record entity in Of(DwgAnswerKey.Key(key), "ENTITIES", "3DSOLID"))
            {
                List<string> lines = Sat(entity);
                byte[] sab = DwgSat.ToSab(lines, form, string.Empty);

                Assert.NotNull(sab);
                Assert.Equal(string.Join("\n", lines) + "\n", DwgSat.ToSat(sab));
                bodies++;
            }

            Assert.True(bodies > 0);
        }

        [Fact]
        public void TheSatOfAnOlderVersion_IsGivenTheNumbersOfTheNewer_SoThatItIsTheSatAutoCadWritesThere()
        {
            // R2004 has no number on a vertex, R2007 and R2010 have; the form is that of AutoCAD's own, solid by solid.
            List<DwgAnswerKey.Record> old = Of(DwgAnswerKey.Key("acis_AC1018"), "ENTITIES", "3DSOLID");
            List<DwgAnswerKey.Record> newer = Of(DwgAnswerKey.Key("acis_AC1024"), "ENTITIES", "3DSOLID");

            Assert.Equal(3, old.Count);

            for (int i = 0; i < old.Count; i++)
            {
                Assert.Equal(
                    string.Join("\n", Sat(newer[i])) + "\n",
                    DwgSat.ToSat(Sat(old[i]), AcisFormat.For("AC1024"))
                );
            }
        }

        [Fact]
        public void ASatOfAnUnknownRecordOrOfACIS4_IsRefused()
        {
            List<string> box = Sat(Solids(DwgAnswerKey.Key("acis_AC1024"))[0]);
            AcisFormat r2013 = AcisFormat.For("AC1027");

            var unknown = new List<string>(box);
            unknown.Insert(5, "blend-surface $-1 -1 $-1 0 0 0 #");
            Assert.Null(DwgSat.ToSab(unknown, r2013, string.Empty));

            // ACIS 4.00 has records of its own forms: version 400 is not read.
            List<string> old = Sat(Solids(DwgAnswerKey.Key("solids_AC1015"))[0]);
            Assert.StartsWith("400 ", old[0], StringComparison.Ordinal);
            Assert.Null(DwgSat.ToSab(old, r2013, string.Empty));
            Assert.Null(DwgSat.ToSat(old, AcisFormat.For("AC1024")));
        }

        /// <summary>The SAB of R2010 of the head, an asmheader and a plane whose normal is the vector given.</summary>
        private static byte[] PlaneSab(double nx, double ny, double nz)
        {
            var sab = new List<byte>(Encoding.ASCII.GetBytes("ACIS BinaryFile"));

            void Int(int value) => sab.AddRange(BitConverter.GetBytes(value));
            void Text(string value)
            {
                sab.Add(0x07);
                sab.Add((byte)value.Length);
                sab.AddRange(Encoding.ASCII.GetBytes(value));
            }
            void Part(byte tag, string value)
            {
                sab.Add(tag);
                sab.Add((byte)value.Length);
                sab.AddRange(Encoding.ASCII.GetBytes(value));
            }
            void Pointer(int index)
            {
                sab.Add(0x0C);
                Int(index);
            }
            void Whole(int value)
            {
                sab.Add(0x04);
                Int(value);
            }
            void Triple(byte tag, double x, double y, double z)
            {
                sab.Add(tag);
                sab.AddRange(BitConverter.GetBytes(x));
                sab.AddRange(BitConverter.GetBytes(y));
                sab.AddRange(BitConverter.GetBytes(z));
            }

            Int(21500);
            Int(0);
            Int(2);
            Int(24);
            Text("Autodesk AutoCAD");
            Text("ASM 226.0.0.65535 NT");
            Text(string.Empty);

            foreach (double real in new[] { 25.4, 1E-06, 1E-10 })
            {
                sab.Add(0x06);
                sab.AddRange(BitConverter.GetBytes(real));
            }

            Part(0x0D, "asmheader");
            Pointer(-1);
            Whole(-1);
            Text("226.0.0.65535");
            sab.Add(0x11);
            Part(0x0E, "plane");
            Part(0x0D, "surface");
            Pointer(-1);
            Whole(-1);
            Pointer(-1);
            Triple(0x13, -3.545084971874738, -4.755282581475766, 0.0);
            Triple(0x14, nx, ny, nz);
            Triple(0x14, 0.0, 0.0, 1.0);

            for (int i = 0; i < 5; i++)
            {
                sab.Add(0x0B);
            }

            sab.Add(0x11);
            Part(0x0E, "End");
            Part(0x0E, "of");
            Part(0x0E, "ASM");
            Part(0x0D, "data");
            return sab.ToArray();
        }

        [Fact]
        public void ANormalOfOneButForTheLastBit_IsWrittenAsItIs_AndOneThatIsLongerIsDivided()
        {
            // The plane of a pyramid's face: its normal is 0.9999999999999999 long, and AutoCAD's SAT holds its three numbers.
            string unit = DwgSat.ToSat(PlaneSab(0.7861926661477994, 0.5712024071622966, -0.23585779984433983));

            Assert.NotNull(unit);
            Assert.Contains("0.7861926661477993683 0.5712024071622966437 -0.2358577998443398271", unit);

            // A normal of length 2 is divided by it, as the writer always did.
            string two = DwgSat.ToSat(PlaneSab(0.0, 0.0, 2.0));

            Assert.NotNull(two);
            Assert.Contains(" 0 0 1 ", two);
        }

        #endregion

        #region Tables

        /// <summary>
        /// The tables of a file (each key holds two) as their groups but their graphics, handles, extension dictionaries and data
        /// version: the form of the cells is the answer. The value flags of an empty cell (93) are 3 or 7: AutoCAD writes either for
        /// the same cell, as the DWG tests found, and here both are 3.
        /// </summary>
        private static List<string> Tables(byte[] dxf) =>
            Of(dxf, "ENTITIES", "ACAD_TABLE")
                .Select(table => Show(Body(table, 310, 92, 160, 280)).Replace(" 93=7 ", " 93=3 "))
                .ToList();

        [Theory]
        [InlineData("table_AC1018", "AC1021", "table_AC1021")]
        [InlineData("table_AC1018", "AC1032", "table_AC1032")]
        [InlineData("table_AC1015", "AC1021", "table_AC1021")]
        [InlineData("table_AC1021", "AC1018", "table_AC1018")]
        [InlineData("table_AC1032", "AC1018", "table_AC1018")]
        [InlineData("table_AC1021", "AC1015", "table_AC1015")]
        [InlineData("table_AC1015", "AC1018", "table_AC1018")]
        [InlineData("table_AC1018", "AC1015", "table_AC1015")]
        public void ATableOfOneForm_SavedAsTheOther_IsTheTableAutoCadWritesThere(
            string from,
            string version,
            string answer
        )
        {
            byte[] saved = Saved(DwgAnswerKey.Key(from), version, out IReadOnlyList<string> said);

            Assert.Empty(said);
            Assert.Equal(2, Tables(saved).Count);
            Assert.Equal(Tables(DwgAnswerKey.Key(answer)), Tables(saved));
        }

        [Fact]
        public void ATableGoingToR2010_HasItsDataVersion_AndOneGoingBeforeItHasNone()
        {
            Assert.All(Of(Saved(DwgAnswerKey.Key("table_AC1018"), "AC1024"), "ENTITIES", "ACAD_TABLE"), table =>
                Assert.Contains(table.Groups, g => g.Key == 280)
            );
            Assert.All(Of(Saved(DwgAnswerKey.Key("table_AC1032"), "AC1021"), "ENTITIES", "ACAD_TABLE"), table =>
                Assert.DoesNotContain(table.Groups, g => g.Key == 280)
            );
        }

        [Fact]
        public void ATableGoingToR2007OrLater_LosesTheExtensionDictionaryOfItsRoundTrip()
        {
            byte[] saved = Saved(DwgAnswerKey.Key("table_AC1018"), "AC1021");

            Assert.All(Of(saved, "ENTITIES", "ACAD_TABLE"), table =>
                Assert.DoesNotContain(table.Groups, g => g.Key == 102 && g.Value == "{ACAD_XDICTIONARY")
            );
            Assert.DoesNotContain(DwgAnswerKey.Parse(saved), r => r.Type == "DATATABLE" || r.Type == "ACDBDATATABLE");
        }

        [Fact]
        public void TheRoundTripOfATableOfR2000_IsNamedAsR2004NamesIt_AndBack()
        {
            byte[] as2004 = Saved(DwgAnswerKey.Key("table_AC1015"), "AC1018");
            List<DwgAnswerKey.Record> data = DwgAnswerKey.Parse(as2004).Where(r => r.Type == "DATATABLE").ToList();

            Assert.Equal(2, data.Count);
            Assert.All(data, d => Assert.Contains(d.Groups, g => g.Key == 100 && g.Value == "AcDbDataTable"));
            Assert.Contains(DwgAnswerKey.Parse(as2004), r =>
                r.Type == "CLASS" && r.Groups.Any(g => g.Key == 1 && g.Value == "DATATABLE")
            );

            byte[] as2000 = Saved(DwgAnswerKey.Key("table_AC1018"), "AC1015");
            data = DwgAnswerKey.Parse(as2000).Where(r => r.Type == "ACDBDATATABLE" && r.Section == "OBJECTS").ToList();

            Assert.Equal(2, data.Count);
            Assert.All(data, d => Assert.Contains(d.Groups, g => g.Key == 100 && g.Value == "ACDBDATATABLE"));
        }

        [Fact]
        public void ATableWithACellAboutText_StaysTheBlockItIsDrawnWithWhereItsCellsHaveNoForm()
        {
            // A cell with a height of its own (140) is none of the plain cells: that table is the INSERT it was made, the other stays.
            string text = Encoding.GetEncoding(28591).GetString(DwgAnswerKey.Key("table_AC1021")).Replace("\r\n", "\n");
            int at = text.IndexOf("\n145\n0.0\n", StringComparison.Ordinal);
            string changed = text.Insert(at + "\n145\n0.0\n".Length, "140\n2.5\n");
            DxfDatabase database = DxfDatabase.Parse(changed);

            using (var stream = new MemoryStream())
            {
                IReadOnlyList<string> said = database.Save(stream, "AC1018");
                byte[] saved = stream.ToArray();

                Assert.Single(Of(saved, "ENTITIES", "ACAD_TABLE"));
                Assert.Single(Of(saved, "ENTITIES", "INSERT"));
                Assert.Contains(said, s => s.Contains("ACAD_TABLE 1"));
            }
        }

        #endregion

        #region Visual styles

        private static Dictionary<string, List<KeyValuePair<int, string>>> Styles(byte[] dxf)
        {
            var styles = new Dictionary<string, List<KeyValuePair<int, string>>>(StringComparer.Ordinal);

            foreach (DwgAnswerKey.Record record in Of(dxf, "OBJECTS", "VISUALSTYLE"))
            {
                int at = record.Groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbVisualStyle");
                int stop = record.Groups.FindIndex(at, g => g.Key == 1001);
                List<KeyValuePair<int, string>> body = record
                    .Groups.Skip(at + 1)
                    .Take((stop < 0 ? record.Groups.Count : stop) - at - 1)
                    .Select(g => new KeyValuePair<int, string>(g.Key, g.Value.Trim()))
                    .ToList();
                styles[body.First(g => g.Key == 2).Value] = body;
            }

            return styles;
        }

        [Theory]
        [InlineData("acis_AC1024", "AC1027", "acis_AC1027")]
        [InlineData("acis_AC1024", "AC1032", "acis_AC1032")]
        [InlineData("acis_AC1027", "AC1024", "acis_AC1024")]
        [InlineData("acis_AC1032", "AC1024", "acis_AC1024")]
        [InlineData("table_AC1018", "AC1021", "table_AC1021")]
        [InlineData("table_AC1021", "AC1018", "table_AC1018")]
        public void TheVisualStylesOfOneForm_SavedAsAnother_AreTheOnesAutoCadWritesThere(
            string from,
            string version,
            string answer
        )
        {
            Dictionary<string, List<KeyValuePair<int, string>>> key = Styles(DwgAnswerKey.Key(answer));
            Dictionary<string, List<KeyValuePair<int, string>>> saved = Styles(
                Saved(DwgAnswerKey.Key(from), version, out IReadOnlyList<string> said)
            );

            Assert.Equal(24, key.Count);
            Assert.Equal(
                key.Keys.OrderBy(k => k, StringComparer.Ordinal),
                saved.Keys.OrderBy(k => k, StringComparer.Ordinal)
            );
            Assert.DoesNotContain(said, s => s.Contains("VISUALSTYLE"));

            foreach (string name in key.Keys)
            {
                Assert.Equal(Show(key[name]), Show(saved[name]));
            }
        }

        [Fact]
        public void TheVisualStylesOfR2004_GoingToR2010_AreSetInEveryProperty_WhereAutoCadLeavesThreeToTheirParent()
        {
            // R2004 holds no word on how a property is set; AutoCAD writes it set (1) but for the 3 styles that give their edges
            // no colour, no jitter and no overhang, whose properties come from the style they derive from (0).
            Dictionary<string, List<KeyValuePair<int, string>>> key = Styles(DwgAnswerKey.Key("acis_AC1024"));
            Dictionary<string, List<KeyValuePair<int, string>>> saved = Styles(
                Saved(DwgAnswerKey.Key("acis_AC1018"), "AC1024")
            );
            var inherited = new[] { "EdgeColorOff", "JitterOff", "OverhangOff" };

            // The lighting of Realistic differs between AutoCAD's own R2004 and R2010 files of this drawing (72 is 2 and 3).
            foreach (string name in key.Keys.Where(k => !inherited.Contains(k) && k != "Realistic"))
            {
                Assert.Equal(Show(key[name]), Show(saved[name]));
            }

            foreach (string name in inherited)
            {
                Assert.Equal(
                    Show(key[name].Where(g => g.Key != 176).ToList()),
                    Show(saved[name].Where(g => g.Key != 176).ToList())
                );
            }
        }

        [Fact]
        public void TheVisualStylesOfR2000_StayInTheFormOfR2004_WhichHasTheTrueColoursAutoCadAdds()
        {
            // R2000 has no true colour (421): going up they are not made, as the styles are the ones the version wrote.
            Dictionary<string, List<KeyValuePair<int, string>>> saved = Styles(
                Saved(DwgAnswerKey.Key("table_AC1015"), "AC1021")
            );

            Assert.Equal(24, saved.Count);
            Assert.All(saved.Values, body => Assert.DoesNotContain(body, g => g.Key == 421));
        }

        #endregion

        #region Columns of an MTEXT

        private static string Mtext(string handle, string text, string xdata) =>
            "  0\nMTEXT\n  5\n"
            + handle
            + "\n330\n1F\n100\nAcDbEntity\n  8\n0\n100\nAcDbMText\n 10\n10.0\n 20\n100.0\n 30\n0.0\n 40\n2.5\n 41\n28.0\n 46\n0.0\n 71\n     1\n 72\n     5\n  1\n"
            + text
            + "\n 73\n     1\n 44\n1.0\n"
            + xdata;

        private const string ColumnInfo =
            "1001\nACAD\n1000\nACAD_MTEXT_COLUMN_INFO_BEGIN\n1070\n    75\n1070\n     1\n1070\n    79\n1070\n     0\n1070\n    76\n1070\n     2\n1070\n    78\n1070\n     0\n1070\n    48\n1040\n28.0\n1070\n    49\n1040\n4.0\n1000\nACAD_MTEXT_COLUMN_INFO_END\n";

        private const string ColumnList =
            "1000\nACAD_MTEXT_COLUMNS_BEGIN\n1070\n    47\n1070\n     2\n1005\nC2\n1000\nACAD_MTEXT_COLUMNS_END\n";

        private static string TwoColumns(string version) =>
            "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\n"
            + version
            + "\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n"
            + Mtext("C1", "One two ", ColumnInfo + ColumnList)
            + Mtext("C2", "three four five", ColumnInfo)
            + "  0\nENDSEC\n  0\nEOF\n";

        [Fact]
        public void TheColumnsOfAnMTextOfR2013_AreOneMTextWithAnEmbeddedObjectInR2018()
        {
            byte[] saved = Saved(
                Encoding.GetEncoding(28591).GetBytes(TwoColumns("AC1027")),
                "AC1032",
                out IReadOnlyList<string> said
            );
            DwgAnswerKey.Record text = Of(saved, "ENTITIES", "MTEXT").Single();
            List<KeyValuePair<int, string>> groups = text
                .Groups.Select(g => new KeyValuePair<int, string>(g.Key, g.Value.Trim()))
                .ToList();
            int at = groups.FindIndex(g => g.Key == 101);

            Assert.Empty(said);
            Assert.Contains(groups, g => g.Key == 1 && g.Value == "One two three four five");
            Assert.Equal("60.0", groups.First(g => g.Key == 41).Value);
            Assert.True(at > 0);
            Assert.Equal(
                "101=Embedded Object 70=1 10=1.0 20=0.0 30=0.0 11=10.0 21=100.0 31=0.0 40=60.0 41=0.0 42=0.0 43=0.0 71=1 72=2 44=28.0 45=4.0 73=0 74=0",
                Show(groups.Skip(at).ToList())
            );
            Assert.DoesNotContain(groups, g => g.Key == 1001 || g.Key == 1000);
        }

        [Fact]
        public void TheColumnsOfAnMTextGoingDownAndUpAgain_AreTheOnesItHad()
        {
            byte[] r2018 = Saved(Encoding.GetEncoding(28591).GetBytes(TwoColumns("AC1027")), "AC1032");
            byte[] down = Saved(r2018, "AC1024");
            byte[] up = Saved(down, "AC1032");

            Assert.Equal(
                Show(Body(Of(r2018, "ENTITIES", "MTEXT").Single())),
                Show(Body(Of(up, "ENTITIES", "MTEXT").Single()))
            );
        }

        #endregion
    }
}
