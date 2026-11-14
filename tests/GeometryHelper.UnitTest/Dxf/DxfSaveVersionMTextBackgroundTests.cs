using System;
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
    /// The background of an MTEXT going to R2000, which has no groups for it: AutoCAD 2021 keeps it in the extended data
    /// <c>ACAD_MTEXT_BBRT</c> to <c>ACAD_MTEXT_BERT</c> of the application ACAD, after the line spacing that every MTEXT of R2000
    /// carries, and going up again it makes the groups of R2004 and later of it. Five MTEXTs AutoCAD wrote (a colour of the index 1,
    /// the window's background, a true colour, a scale of 2 and a transparency of half) saved as R2000 and read back.
    /// </summary>
    public class DxfSaveVersionMTextBackgroundTests
    {
        private static string G(int code, string value) =>
            code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        /// <summary>A drawing of the version with one MTEXT of 40 wide: the groups of its background after those of its text.</summary>
        private static string Drawing(string version, string background, string data = "")
        {
            var text = new StringBuilder();
            text.Append(G(0, "SECTION")).Append(G(2, "HEADER")).Append(G(9, "$ACADVER")).Append(G(1, version));
            text.Append(G(0, "ENDSEC")).Append(G(0, "SECTION")).Append(G(2, "ENTITIES"));
            text.Append(G(0, "MTEXT")).Append(G(5, "A1")).Append(G(330, "1F")).Append(G(100, "AcDbEntity"));
            text.Append(G(8, "0"))
                .Append(G(100, "AcDbMText"))
                .Append(G(10, "0.0"))
                .Append(G(20, "0.0"))
                .Append(G(30, "0.0"));
            text.Append(G(40, "2.5")).Append(G(41, "40.0"));

            if (version != "AC1015" && version != "AC1018")
            {
                text.Append(G(46, "0.0"));
            }

            text.Append(G(71, "1")).Append(G(72, "5")).Append(G(1, "Note"));
            text.Append(G(73, "1")).Append(G(44, "1.0")).Append(background).Append(data);
            return text.Append(G(0, "ENDSEC")).Append(G(0, "EOF")).ToString();
        }

        private static DwgAnswerKey.Record Saved(string text, string version)
        {
            DxfDatabase db = DxfDatabase.Parse(text);

            using (var stream = new MemoryStream())
            {
                db.Save(stream, version);
                return DwgAnswerKey.Parse(stream.ToArray()).Single(r => r.Type == "MTEXT");
            }
        }

        private static string Shown(DwgAnswerKey.Record record) =>
            string.Join(
                " ",
                record
                    .Groups.Where(g => g.Key != 5 && g.Key != 330 && g.Key != 102 && g.Key != 360)
                    .Select(g => g.Key + "=" + g.Value.Trim())
            );

        private static string Groups(string pairs) =>
            string.Concat(
                pairs
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p =>
                        G(int.Parse(p.Split('=')[0], CultureInfo.InvariantCulture), p.Substring(p.IndexOf('=') + 1))
                    )
            );

        private const string Line =
            "1001=ACAD 1000=MTEXTBEGIN 1070=73 1070=1 1070=44 1040=1.0 1070=74 1070=0 1000=MTEXTEND ";

        // AutoCAD's: 91 the flags, 46 the scale, 64 the colour in one number (0xC3 and the index, 0xC2 and the red, green and blue),
        // 65 and 66 the names of the colour and its book, empty, and 442 the transparency.
        [Theory]
        [InlineData(
            "90=3 63=1 45=1.5 441=0",
            "1070=91 1071=3 1070=46 1040=1.5 1070=64 1071=-1023410175 1070=442 1071=0"
        )]
        [InlineData(
            "90=3 63=9 421=13158600 45=1.5 441=0",
            "1070=91 1071=3 1070=46 1040=1.5 1070=64 1071=-1027028792 1070=442 1071=0"
        )]
        [InlineData(
            "90=3 63=102 421=706590 45=1.5 441=0",
            "1070=91 1071=3 1070=46 1040=1.5 1070=64 1071=-1039480802 1070=442 1071=0"
        )]
        [InlineData(
            "90=3 63=3 45=2.0 441=0",
            "1070=91 1071=3 1070=46 1040=2.0 1070=64 1071=-1023410173 1070=442 1071=0"
        )]
        [InlineData(
            "90=3 63=5 45=1.5 441=33554560",
            "1070=91 1071=3 1070=46 1040=1.5 1070=64 1071=-1023410171 1070=442 1071=33554560"
        )]
        public void TheBackgroundOfAnMText_GoingToR2000_IsInItsExtendedData_AsAutoCadWritesIt(
            string background,
            string numbers
        )
        {
            DwgAnswerKey.Record saved = Saved(Drawing("AC1032", Groups(background)), "AC1015");
            string[] n = numbers.Split(' ');
            string data =
                Line
                + "1000=ACAD_MTEXT_BBRT "
                + string.Join(" ", n.Take(6))
                + " 1070=65 1000= 1070=66 1000= "
                + string.Join(" ", n.Skip(6))
                + " 1000=ACAD_MTEXT_BERT";

            Assert.EndsWith(data, Shown(saved));
            Assert.DoesNotContain(saved.Groups, g => g.Key == 90 || g.Key == 63 || g.Key == 45 || g.Key == 441);
        }

        [Fact]
        public void AnMTextWithNoBackground_GoingToR2000_HasNoDataOfOne()
        {
            DwgAnswerKey.Record saved = Saved(Drawing("AC1032", string.Empty), "AC1015");

            Assert.DoesNotContain(saved.Groups, g =>
                g.Key == 1000 && g.Value.StartsWith("ACAD_MTEXT_B", StringComparison.Ordinal)
            );
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1032")]
        public void TheBackgroundOfAnMTextOfR2000_GoingUp_IsTheGroupsAutoCadMakesOfIt(string version)
        {
            string old =
                "1001=ACAD 1000=MTEXTBEGIN 1070=73 1070=1 1070=44 1040=1.0 1070=74 1070=0 1000=MTEXTEND "
                + "1000=ACAD_MTEXT_BBRT 1070=91 1071=3 1070=46 1040=2.0 1070=64 1071=-1039480802 1070=65 1000= 1070=66 1000= 1070=442 1071=0 1000=ACAD_MTEXT_BERT";
            DwgAnswerKey.Record saved = Saved(Drawing("AC1015", string.Empty, Groups(old)), version);

            Assert.EndsWith(" 73=1 44=1.0 90=3 63=102 421=706590 45=2.0 441=0", Shown(saved));
            Assert.DoesNotContain(saved.Groups, g => g.Key == 1001 || g.Key == 1000);
        }

        [Theory]
        [InlineData("AC1021")]
        [InlineData("AC1032")]
        public void AnMTextOfR2000_GoingToR2007OrLater_HasTheHeightOfItsColumn_AsAutoCadWritesIt(string version)
        {
            DwgAnswerKey.Record saved = Saved(Drawing("AC1015", string.Empty), version);

            Assert.Contains(" 41=40.0 46=0.0 71=1 ", Shown(saved) + " ");
        }

        [Fact]
        public void TheBackgroundOfAnMTextGoingDownAndUp_IsTheOneItHad()
        {
            string background = Groups("90=3 63=5 45=1.5 441=33554560");

            using (var down = new MemoryStream())
            using (var up = new MemoryStream())
            {
                DxfDatabase.Parse(Drawing("AC1032", background)).Save(down, "AC1015");
                DxfDatabase.Parse(Encoding.GetEncoding(28591).GetString(down.ToArray())).Save(up, "AC1032");
                DwgAnswerKey.Record text = DwgAnswerKey.Parse(up.ToArray()).Single(r => r.Type == "MTEXT");

                Assert.EndsWith(" 90=3 63=5 45=1.5 441=33554560", Shown(text));
            }
        }
    }
}
