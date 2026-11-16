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
    /// The visual styles, the detail and section view styles and the table styles a DWG of R2000 to R2010 holds, as AutoCAD
    /// 2021 writes them: the writer left the visual styles out before R2013 and the view styles before R2018 (and every
    /// dictionary that named one came out empty), a table style that came from R2010 or later got no map of cell styles, and
    /// a file of R2000 had no table of viewport entity headers where the drawing has no numbered viewport.
    /// </summary>
    public class DwgStylesOfOlderVersionsTests
    {
        /// <summary>The DWG of the DXF the reader makes of one of AutoCAD's own files, written as its own version.</summary>
        private static byte[] OursOf(string answer, string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(DwgAnswerKey.Dwg(answer)), false)).ToText();
            return DwgWriter.ToDwg(text, version, out _);
        }

        /// <summary>The DWG of a drawing of AutoCAD's own DXF of R2018, written as <paramref name="version"/>.</summary>
        private static byte[] OursFromR2018(string answer, string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(answer), false)).ToText();
            return DwgWriter.ToDwg(text, version, out _);
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

        /// <summary>The handles of the records of a type in AutoCAD's own DXF of a drawing.</summary>
        private static List<ulong> HandlesOf(string answer, string type) =>
            DwgAnswerKey
                .Parse(DwgAnswerKey.Key(answer))
                .Where(r => r.Section == "OBJECTS" && r.Type == type)
                .Select(r => ulong.Parse(r.Groups.First(g => g.Key == 5).Value, NumberStyles.HexNumber))
                .ToList();

        [Theory]
        [InlineData("text", "AC1015")]
        [InlineData("matvis_AC1018", "AC1018")]
        [InlineData("matvis_AC1021", "AC1021")]
        [InlineData("matvis_AC1024", "AC1024")]
        [InlineData("matvis_AC1027", "AC1027")]
        public void TheVisualStylesOfAnOlderFile_WrittenAgain_AreAutoCADsBytes(string answer, string version)
        {
            List<ulong> styles = HandlesOf(answer, "VISUALSTYLE");
            byte[] ours = OursOf(answer, version);
            byte[] acad = DwgAnswerKey.Dwg(answer);

            Assert.InRange(styles.Count, 24, 25);

            foreach (ulong style in styles)
            {
                Assert.Equal(ObjectOf(acad, style), ObjectOf(ours, style));
            }
        }

        [Theory]
        [InlineData("text", "AC1015", 0x69UL)]
        [InlineData("text", "AC1015", 0x6BUL)]
        [InlineData("matvis_AC1018", "AC1018", 0x69UL)]
        [InlineData("matvis_AC1018", "AC1018", 0x6BUL)]
        [InlineData("matvis_AC1021", "AC1021", 0x69UL)]
        [InlineData("matvis_AC1021", "AC1021", 0x6BUL)]
        [InlineData("matvis_AC1024", "AC1024", 0x69UL)]
        [InlineData("matvis_AC1024", "AC1024", 0x6BUL)]
        [InlineData("matvis_AC1027", "AC1027", 0x69UL)]
        [InlineData("matvis_AC1027", "AC1027", 0x6BUL)]
        public void TheSectionAndDetailViewStylesOfAnOlderFile_WrittenAgain_AreAutoCADsBytes(
            string answer,
            string version,
            ulong handle
        )
        {
            Assert.Equal(ObjectOf(DwgAnswerKey.Dwg(answer), handle), ObjectOf(OursOf(answer, version), handle));
        }

        [Fact]
        public void ADetailAndASectionViewStyleOfItsOwn_WrittenAsR2004_AreAutoCADsBytes()
        {
            // views_AC1018 holds a detail style (0x7E) and a section style (0x7F) with every property off AutoCAD's default.
            byte[] acad = DwgAnswerKey.Dwg("views_AC1018");
            byte[] ours = OursOf("views_AC1018", "AC1018");

            Assert.Equal(ObjectOf(acad, 0x7E), ObjectOf(ours, 0x7E));
            Assert.Equal(ObjectOf(acad, 0x7F), ObjectOf(ours, 0x7F));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        public void ADrawingOfR2018_WrittenToAnOlderVersion_KeepsItsVisualAndViewStyles(string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("matvis_AC1032"), false)).ToText();
            DwgWriter.ToDwg(text, version, out IDictionary<string, int> left);

            Assert.DoesNotContain("VISUALSTYLE", left.Keys);
            Assert.DoesNotContain("ACDBDETAILVIEWSTYLE", left.Keys);
            Assert.DoesNotContain("ACDBSECTIONVIEWSTYLE", left.Keys);
        }

        [Theory]
        [InlineData("matvis_AC1018", "AC1018")]
        [InlineData("matvis_AC1021", "AC1021")]
        [InlineData("matvis_AC1024", "AC1024")]
        [InlineData("matvis_AC1027", "AC1027")]
        public void TheStylesOfADrawingOfR2018_WrittenToAnOlderVersion_AreThoseAutoCADWritesThere(
            string answer,
            string version
        )
        {
            byte[] back = DwgReader.ToDxf(OursFromR2018("matvis_AC1032", version));

            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(answer), back);

            Assert.Equal(25, DwgAnswerKey.Parse(back).Count(r => r.Type == "VISUALSTYLE"));
            Assert.DoesNotContain("VISUALSTYLE", result.Missing.Keys);
            Assert.DoesNotContain("ACDBDETAILVIEWSTYLE", result.Missing.Keys);
            Assert.DoesNotContain("ACDBSECTIONVIEWSTYLE", result.Missing.Keys);

            // The style Realistic (X:33) keeps its lighting model 3 there only from R2010, as 2 in R2004 and R2007, which the
            // DXF of R2018 cannot give back; the rest of every style, and its stamp, are AutoCAD's.
            Assert.All(
                result.Differences.Where(d =>
                    d.Contains(" VISUALSTYLE ")
                    || d.Contains(" ACDBDETAILVIEWSTYLE ")
                    || d.Contains(" ACDBSECTIONVIEWSTYLE ")
                ),
                d =>
                {
                    Assert.DoesNotContain(answer, new[] { "matvis_AC1024", "matvis_AC1027" });
                    Assert.Contains("VISUALSTYLE X:33:", d);
                }
            );
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        public void TheDisplayNameAndTheFlagsOfAViewStyleOfR2018_GoDownToTheRoundTripRecordOfItsExtensionDictionary(
            string version
        )
        {
            // AutoCAD keeps them there before R2018: DISPLAYNAME and FLAGS under ACAD_XREC_ROUNDTRIP.
            DxfDatabase source = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("matvis_AC1032"), false));

            using (var stream = new MemoryStream())
            {
                source.Save(stream, version);
                List<DwgAnswerKey.Record> all = DwgAnswerKey.Parse(stream.ToArray());
                DwgAnswerKey.Record detail = Assert.Single(all, r => r.Type == "ACDBDETAILVIEWSTYLE");

                Assert.DoesNotContain(detail.Groups, g => g.Key == 300 && g.Value == "Imperial24" && IsOwn(detail, g));
                KeyValuePair<int, string> held = detail.Groups.First(g => g.Key == 360);
                DwgAnswerKey.Record dictionary = all.Single(r => r.Key == "X:" + held.Value.ToUpperInvariant());
                KeyValuePair<int, string> entry = dictionary.Groups.First(g => g.Key == 360);
                DwgAnswerKey.Record xrecord = all.Single(r => r.Key == "X:" + entry.Value.ToUpperInvariant());

                Assert.Equal(
                    new[] { "102=DISPLAYNAME", "1=Imperial24", "102=FLAGS", "90=0" },
                    xrecord
                        .Groups.SkipWhile(g => g.Key != 280)
                        .Skip(1)
                        .Select(g => g.Key + "=" + g.Value.Trim())
                        .ToArray()
                );
            }
        }

        private static bool IsOwn(DwgAnswerKey.Record style, KeyValuePair<int, string> group)
        {
            int model = style.Groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbModelDocViewStyle");
            int detail = style.Groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbDetailViewStyle");
            int at = style.Groups.IndexOf(group);
            return at > model && at < detail;
        }

        [Theory]
        [InlineData("matvis_AC1018", 0x69UL)]
        [InlineData("matvis_AC1018", 0x6BUL)]
        [InlineData("matvis_AC1021", 0x69UL)]
        [InlineData("matvis_AC1021", 0x6BUL)]
        [InlineData("matvis_AC1024", 0x69UL)]
        [InlineData("matvis_AC1024", 0x6BUL)]
        public void TheViewStylesOfAnOlderDwg_WrittenAsR2018_AreAutoCADsBytes_WithTheirNameTakenIn(
            string answer,
            ulong handle
        )
        {
            // AutoCAD's own R2018 file of the same drawing, matvis_AC1032.
            Assert.Equal(
                ObjectOf(DwgAnswerKey.Dwg("matvis_AC1032"), handle),
                ObjectOf(OursOf(answer, "AC1032"), handle)
            );
        }

        [Fact]
        public void TheDisplayNameOfAViewStyleOfR2010_ComesUpIntoTheStyle_WhenItIsSavedAsR2018()
        {
            DxfDatabase source = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("matvis_AC1024"), false));

            using (var stream = new MemoryStream())
            {
                source.Save(stream, "AC1032");
                List<DwgAnswerKey.Record> all = DwgAnswerKey.Parse(stream.ToArray());
                DwgAnswerKey.Record detail = Assert.Single(all, r => r.Type == "ACDBDETAILVIEWSTYLE");
                DwgAnswerKey.Record acad = Assert.Single(DwgAnswerKey.Parse(DwgAnswerKey.Key("matvis_AC1032")), r =>
                    r.Type == "ACDBDETAILVIEWSTYLE"
                );

                Assert.Equal(
                    string.Join(" ", acad.Groups.Select(g => g.Key + "=" + g.Value)),
                    string.Join(" ", detail.Groups.Select(g => g.Key + "=" + g.Value))
                );
                Assert.DoesNotContain(all, r => r.Type == "XRECORD" && r.Groups.Any(g => g.Value == "DISPLAYNAME"));
            }
        }

        [Theory]
        [InlineData("text")]
        [InlineData("tables")]
        [InlineData("curves")]
        [InlineData("polys")]
        public void ADrawingOfR2000WithNoNumberedViewport_HasAnEmptyTableOfViewportEntityHeaders(string answer)
        {
            byte[] acad = DwgAnswerKey.Dwg(answer);
            byte[] ours = OursOf(answer, "AC1015");

            // The table, handle B, lists no header, and the header variable of the viewport entities names it.
            Assert.Equal(ObjectOf(acad, 0xB), ObjectOf(ours, 0xB));
            Assert.Equal(HeaderVariable(acad, "_VPORTENTITIES"), HeaderVariable(ours, "_VPORTENTITIES"));
            Assert.Equal("HB", HeaderVariable(ours, "_VPORTENTITIES"));
        }

        private static string HeaderVariable(byte[] file, string name)
        {
            DwgHeaderVariables header = DwgHeaderVariables.Read(DwgContainer.Read(file));
            var values =
                (Dictionary<string, object>)
                    typeof(DwgHeaderVariables)
                        .GetField(
                            "_values",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                        )
                        .GetValue(header);
            return values[name] is ulong handle ? "H" + handle.ToString("X", CultureInfo.InvariantCulture) : null;
        }

        /// <summary>The groups of each CELLSTYLEMAP of a DXF after its owner, without the handles.</summary>
        private static List<string> MapsOf(byte[] dxf) =>
            DwgAnswerKey
                .Parse(dxf)
                .Where(r => r.Type == "CELLSTYLEMAP")
                .Select(r =>
                    string.Join(" ", r.Groups.SkipWhile(g => g.Key != 100).Select(g => g.Key + "=" + g.Value.Trim()))
                )
                .ToList();

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        public void ATableStyleOfR2018WithNoMap_IsGivenTheMapAutoCADKeepsOfADefaultStyle(string version)
        {
            // props_AC1018 is AutoCAD's own R2004 file of a drawing whose only table style is the default Standard.
            List<string> acad = MapsOf(DwgAnswerKey.Key("props_AC1018"));
            byte[] back = DwgReader.ToDxf(OursOf("ttable_AC1032", version));
            List<DwgAnswerKey.Record> all = DwgAnswerKey.Parse(back);
            DwgAnswerKey.Record style = Assert.Single(all, r => r.Type == "TABLESTYLE");

            Assert.Equal(acad, MapsOf(back));

            // The style holds an extension dictionary that names the map.
            KeyValuePair<int, string> held = style.Groups.First(g => g.Key == 360);
            DwgAnswerKey.Record dictionary = all.Single(r => r.Key == "X:" + held.Value.ToUpperInvariant());
            Assert.Contains(dictionary.Groups, g =>
                g.Key == 3 && g.Value == "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP"
            );
        }

        [Fact]
        public void ATableStyleWhoseExtensionDictionaryIsEmpty_IsGivenItsMapInThatDictionary()
        {
            // The style Standard of tcustom (R2018) has an empty dictionary; AutoCAD's R2007 file keeps the map in it.
            byte[] back = DwgReader.ToDxf(OursOf("tcustom_AC1032", "AC1021"));
            List<DwgAnswerKey.Record> all = DwgAnswerKey.Parse(back);
            List<DwgAnswerKey.Record> styles = all.Where(r => r.Type == "TABLESTYLE").ToList();

            Assert.Equal(2, styles.Count);
            Assert.Equal(2, all.Count(r => r.Type == "CELLSTYLEMAP"));
            Assert.All(styles, style =>
            {
                string dictionary = style.Groups.First(g => g.Key == 360).Value;
                Assert.Single(all, r =>
                    r.Type == "DICTIONARY"
                    && r.Key == "X:" + dictionary.ToUpperInvariant()
                    && r.Groups.Any(g => g.Key == 3 && g.Value == "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP")
                );
            });
        }

        [Theory]
        [InlineData("text", "AC1015")]
        [InlineData("tables", "AC1015")]
        [InlineData("matvis_AC1018", "AC1018")]
        [InlineData("props_AC1018", "AC1018")]
        public void TheFileHeaderOfR2000AndR2004_NamesTheApplicationThatSavedIt_AsAutoCAD2021Does(
            string answer,
            string version
        )
        {
            // AutoCAD recomposes the cell styles of a table style as it reads a file that names another application, and keeps
            // the map the file holds where it names AutoCAD 2021 (33, maintenance release 174), as its DXFOUT of the file shows.
            byte[] acad = DwgAnswerKey.Dwg(answer);
            byte[] ours = OursOf(answer, version);

            Assert.Equal(new byte[] { 33, 174 }, new[] { acad[0x11], acad[0x12] });
            Assert.Equal(new[] { acad[0x11], acad[0x12] }, new[] { ours[0x11], ours[0x12] });
            Assert.Equal(acad[0x16], ours[0x16]);
            Assert.Equal(acad[0x17], ours[0x17]);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        public void ATableStyleThatHasItsMap_IsNotGivenASecond(string version)
        {
            // The two styles of tstyles (Standard and one with cell styles of its own) come with their maps in AutoCAD's DXF.
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("tstyles_AC1032"), false)).ToText();
            List<DwgAnswerKey.Record> back = DwgAnswerKey.Parse(DwgReader.ToDxf(DwgWriter.ToDwg(text, version)));

            Assert.Equal(2, back.Count(r => r.Type == "CELLSTYLEMAP"));
        }
    }
}
