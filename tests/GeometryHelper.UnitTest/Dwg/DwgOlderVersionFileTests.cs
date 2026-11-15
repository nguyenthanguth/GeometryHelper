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
    /// The DWG files of R2004, R2010 and R2013 that <see cref="DwgWriter"/> writes. AutoCAD 2021 opened none of the R2010
    /// files that held a table, a multileader, a wipeout or an image: it took all the memory it could get, or refused them with
    /// <c>ErrorStatus</c> 53, because the file named no maintenance release (AutoCAD's own file of R2010 names 226, of R2004
    /// 104, of R2013 125) and its header and classes lacked the long of 0 after their size that R2010 and later carry. A field
    /// was written as R2007 lays it out into a file of R2004, which read it as the older form.
    /// </summary>
    public class DwgOlderVersionFileTests
    {
        private static byte[] Written(string version)
        {
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false)).ToText();
            return DwgFileWriter.Write(DwgDrawing.Parse(text, null), version, out _);
        }

        [Theory]
        [InlineData("AC1018", 104)]
        [InlineData("AC1024", 226)]
        [InlineData("AC1027", 125)]
        public void AFile_NamesTheMaintenanceReleaseAutoCadWritesForItsVersion(string version, int release)
        {
            Assert.Equal(release, Written(version)[0x0B]);
        }

        [Theory]
        [InlineData("AC1018", 0)]
        [InlineData("AC1024", 4)]
        [InlineData("AC1027", 4)]
        [InlineData("AC1032", 4)]
        public void TheHeaderSection_HasALongOfZeroAfterItsSize_FromR2010Only(string version, int longs)
        {
            byte[] bytes = DwgContainer.SectionsOf(Written(version), out _).Single(s => s.Key == "AcDb:Header").Value;
            int size = BitConverter.ToInt32(bytes, 16);

            // The sentinel (16 bytes), the size, the long where there is one, the data, the check sum and the sentinel.
            Assert.Equal(16 + 4 + longs + size + 2 + 16, bytes.Length);
        }

        [Theory]
        [InlineData("AC1018", false)]
        [InlineData("AC1024", true)]
        public void TheClassesSection_HasALongOfZeroAfterItsSize_FromR2010Only(string version, bool has)
        {
            byte[] bytes = DwgContainer.SectionsOf(Written(version), out _).Single(s => s.Key == "AcDb:Classes").Value;
            bool zero = bytes.Skip(20).Take(4).All(b => b == 0);

            Assert.Equal(has, zero);
        }

        private static string G(int code, string value) => code.ToString().PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, int value) => G(code, value.ToString());

        private static string Class(string name, string cpp, int flags) =>
            G(0, "CLASS") + G(1, name) + G(2, cpp) + G(3, "ObjectDBX Classes") + G(90, flags) + G(280, 0) + G(281, 0);

        /// <summary>The drawing of curves of AutoCAD's own DXF with a FIELD of the sheet's file name as its last object.</summary>
        private static string DrawingWithAField()
        {
            DxfDatabase database = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false));
            string text = database.ToText().Replace("\r\n", "\n");
            int classesAt = text.IndexOf("\nCLASSES\n", StringComparison.Ordinal);
            int classesEnd = text.IndexOf("  0\nENDSEC\n", classesAt, StringComparison.Ordinal);
            text = text.Insert(classesEnd, Class("FIELD", "AcDbField", 1152));
            int objectsAt = text.IndexOf("\nOBJECTS\n", StringComparison.Ordinal);
            int objectsEnd = text.IndexOf("  0\nENDSEC\n", objectsAt, StringComparison.Ordinal);
            string field =
                G(0, "FIELD")
                + G(5, "F001")
                + G(102, "{ACAD_REACTORS")
                + G(330, "C")
                + G(102, "}")
                + G(330, "C")
                + G(100, "AcDbField")
                + G(1, "AcVar")
                + G(2, "\\AcVar Filename \\f \"%tc4%fn2\"")
                + G(90, 0)
                + G(97, 0)
                + G(91, 63)
                + G(92, 0)
                + G(94, 59)
                + G(95, 2)
                + G(96, 0)
                + G(300, "")
                + G(93, 1)
                + G(6, "Variable")
                + G(93, 2)
                + G(90, 4)
                + G(1, "Filename")
                + G(94, 0)
                + G(300, "")
                + G(302, "")
                + G(304, "ACVALUE_END")
                + G(7, "ACFD_FIELD_VALUE")
                + G(93, 4)
                + G(90, 4)
                + G(1, "C:\\plans\\Ground floor.dwg")
                + G(94, 0)
                + G(300, "%tc4%fn2")
                + G(302, "Ground floor")
                + G(304, "ACVALUE_END")
                + G(301, "Ground floor")
                + G(98, 12);
            text = text.Insert(objectsEnd, field);
            return Regex.Replace(text, @"(\$HANDSEED\n\s*5\n)[0-9A-Fa-f]+", "${1}FFFF");
        }

        /// <summary>The curves drawing with the records given put at the end of its entities, the handle seed above them.</summary>
        private static string DrawingWithEntities(Func<string, string> entities)
        {
            DxfDatabase database = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("curves_AC1032"), false));
            string text = database.ToText().Replace("\r\n", "\n");
            int entitiesAt = text.IndexOf("\nENTITIES\n", StringComparison.Ordinal);
            int end = text.IndexOf("  0\nENDSEC\n", entitiesAt, StringComparison.Ordinal);
            text = text.Insert(end, entities(database.ModelSpace.Handle));
            return Regex.Replace(text, @"(\$HANDSEED\n\s*5\n)[0-9A-Fa-f]+", "${1}FFFF");
        }

        private static DwgDrawing ReadBack(string text, string version)
        {
            byte[] dwg = DwgWriter.ToDwg(text, version, out _);
            return DwgDrawing.Parse(Encoding.UTF8.GetString(DwgReader.ToDxf(dwg)));
        }

        [Theory]
        [InlineData("AC1018", 0)]
        [InlineData("AC1024", 3)]
        public void TheVertexIdsOfALwPolyline_AreWrittenFromR2010Only(string version, int ids)
        {
            string text = DrawingWithEntities(model =>
                G(0, "LWPOLYLINE")
                + G(5, "FFA0")
                + G(330, model)
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbPolyline")
                + G(90, 3)
                + G(70, 0)
                + G(43, "0.0")
                + G(10, "0.0")
                + G(20, "0.0")
                + G(91, 21)
                + G(10, "10.0")
                + G(20, "0.0")
                + G(91, 22)
                + G(10, "10.0")
                + G(20, "10.0")
                + G(91, 23)
            );

            DwgRecord polyline = ReadBack(text, version).Find(0xFFA0);

            Assert.Equal(ids, polyline.Of("AcDbPolyline").Each(91).Count);
            Assert.Equal(3, polyline.Of("AcDbPolyline").Each(10).Count);
        }

        [Theory]
        [InlineData("AC1018", true)]
        [InlineData("AC1024", false)]
        [InlineData("AC1027", false)]
        public void AMultileaderStyle_HasItsVersionAsExtendedData_BeforeR2010Only(string version, bool has)
        {
            // From R2010 a style keeps its version in its object; before it, AutoCAD refuses the file without the application.
            DwgDrawing drawing = ReadBack(DrawingWithEntities(model => string.Empty), version);
            DwgRecord style = drawing.All.Find(r => r.Type == "MLEADERSTYLE");

            Assert.Equal(has, style.XData.Exists(g => g.Key == 1001 && g.Value == "ACAD_MLEADERVER"));
            Assert.Equal(has, drawing.HandleOf("APPID", "ACAD_MLEADERVER") != 0);
        }

        /// <summary>A text of the proxy graphics of R2007 and later: the piece of type 38, its strings in 16 bits, as AutoCAD 2021 writes it.</summary>
        private static byte[] Text38(string text)
        {
            var piece = new List<byte>();
            piece.AddRange(BitConverter.GetBytes(0));
            piece.AddRange(BitConverter.GetBytes(38));

            for (int i = 0; i < 9; i++)
            {
                piece.AddRange(BitConverter.GetBytes(i == 0 ? 48.0 : 0.0));
            }

            Pad(piece, Encoding.Unicode.GetBytes(text + "\0"));
            piece.AddRange(BitConverter.GetBytes(text.Length));
            piece.AddRange(BitConverter.GetBytes(1));

            foreach (double number in new[] { 0.2, 1.0, 0.0, 1.0 })
            {
                piece.AddRange(BitConverter.GetBytes(number));
            }

            // Seven words of flags, the three of R2007, the font and the big font.
            piece.AddRange(new byte[28]);
            piece.AddRange(BitConverter.GetBytes(1));
            piece.AddRange(new byte[8]);
            Pad(piece, Encoding.Unicode.GetBytes("txt.shx\0"));
            Pad(piece, Encoding.Unicode.GetBytes("\0"));
            byte[] bytes = piece.ToArray();
            BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 0);
            return bytes;
        }

        private static void Pad(List<byte> into, byte[] text)
        {
            into.AddRange(text);

            while (into.Count % 4 != 0)
            {
                into.Add(0);
            }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty);

        /// <summary>The graphics of a LINE of the drawing made of the pieces: a size, a count and each piece.</summary>
        private static byte[] Graphics(params byte[][] pieces)
        {
            var all = new List<byte>();
            all.AddRange(BitConverter.GetBytes(0));
            all.AddRange(BitConverter.GetBytes(pieces.Length));

            foreach (byte[] piece in pieces)
            {
                all.AddRange(piece);
            }

            byte[] bytes = all.ToArray();
            BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 0);
            return bytes;
        }

        private static byte[] GraphicsOfALineWrittenAs(string version, byte[] graphics)
        {
            string text = DrawingWithEntities(model =>
                G(0, "LINE")
                + G(5, "FFA1")
                + G(330, model)
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(92, graphics.Length)
                + G(310, Hex(graphics))
                + G(100, "AcDbLine")
                + G(10, "0.0")
                + G(20, "0.0")
                + G(30, "0.0")
                + G(11, "1.0")
                + G(21, "1.0")
                + G(31, "0.0")
            );

            DwgGroups entity = ReadBack(text, version).Find(0xFFA1).Of("AcDbEntity");
            return DwgObjectWriter.Unhex(string.Concat(entity.Each(310)));
        }

        [Fact]
        public void ATextOfTheGraphicsOfR2007_WrittenAsR2004_IsAPieceOfType11_WithItsStringsIn8Bits()
        {
            byte[] other = new byte[12];
            BitConverter.GetBytes(12).CopyTo(other, 0);
            BitConverter.GetBytes(22).CopyTo(other, 4);
            byte[] graphics = Graphics(Text38("Multi"), other);

            byte[] written = GraphicsOfALineWrittenAs("AC1018", graphics);

            // The text: 8 + 72 + "Multi" with its zero padded to 8 + the 60 bytes of numbers + "txt.shx" and the big font, 8 and 4: 160.
            Assert.Equal(8 + 160 + 12, written.Length);
            Assert.Equal(written.Length, BitConverter.ToInt32(written, 0));
            Assert.Equal(2, BitConverter.ToInt32(written, 4));
            Assert.Equal(160, BitConverter.ToInt32(written, 8));
            Assert.Equal(11, BitConverter.ToInt32(written, 12));
            Assert.Equal("Multi", Encoding.ASCII.GetString(written, 8 + 8 + 72, 5));
            Assert.Equal(5, BitConverter.ToInt32(written, 8 + 8 + 72 + 8));
            Assert.Equal("txt.shx", Encoding.ASCII.GetString(written, 8 + 8 + 72 + 8 + 60, 7));
            Assert.Equal(22, BitConverter.ToInt32(written, 8 + 160 + 4));
        }

        [Fact]
        public void ATextOfTheGraphicsOfR2007_WrittenAsR2010_StaysAsItIs()
        {
            byte[] graphics = Graphics(Text38("Multi"));

            Assert.Equal(graphics, GraphicsOfALineWrittenAs("AC1024", graphics));
        }

        [Fact]
        public void ALayerNamedWithTheEscapeOfAnOlderDxf_WrittenAsR2010_IsKeptAsTheLetter()
        {
            // props.dwg is a file of AutoCAD 2000 with a layer named "Lớp ẩn", which the DXF of its version writes \U+1EDB.
            string text = Encoding.UTF8.GetString(DwgReader.ToDxf(DwgAnswerKey.Dwg("props")));
            Assert.Contains("\\U+1EDB", text);

            DwgDrawing drawing = ReadBack(text, "AC1024");

            Assert.Contains(drawing.Table("LAYER").Records, r => r.Of(null).Text(2).Contains("ớ"));
            Assert.DoesNotContain(drawing.Table("LAYER").Records, r => r.Of(null).Text(2).Contains("\\U+"));
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1024")]
        public void AnExtendedDataStringOf300Characters_IsReadBackWhole(string version)
        {
            string text = DrawingWithEntities(model =>
                G(0, "LINE")
                + G(5, "FFA2")
                + G(330, model)
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbLine")
                + G(10, "0.0")
                + G(20, "0.0")
                + G(30, "0.0")
                + G(11, "1.0")
                + G(21, "1.0")
                + G(31, "0.0")
                + G(1001, "ACAD")
                + G(1000, new string('x', 300))
            );

            DwgRecord line = ReadBack(text, version).Find(0xFFA2);

            Assert.Equal(new string('x', 300), line.XData.Single(g => g.Key == 1000).Value);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1024")]
        public void AField_WrittenAsAnOlderVersion_IsReadBackWithItsCodeValueAndText(string version)
        {
            byte[] dwg = DwgWriter.ToDwg(DrawingWithAField(), version, out IDictionary<string, int> left);

            Assert.DoesNotContain("FIELD", left.Keys);
            string dxf = Encoding.UTF8.GetString(DwgReader.ToDxf(dwg)).Replace("\r\n", "\n");
            string[] lines = dxf.Split('\n');
            int at = Enumerable
                .Range(0, lines.Length - 1)
                .First(i => i % 2 == 0 && lines[i].Trim() == "0" && lines[i + 1].Trim() == "FIELD");
            string record = string.Join("|", lines.Skip(at).Take(120).Select(l => l.Trim()));

            Assert.Contains("6|Variable", record);
            Assert.Contains("C:\\plans\\Ground floor.dwg", record);
            Assert.Contains("301|Ground floor|98|12", record);
            Assert.Contains("%tc4%fn2", record);
        }
    }
}
