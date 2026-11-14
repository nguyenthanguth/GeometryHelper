using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The extended data every MTEXT of R2000 carries (application ACAD, <c>MTEXTBEGIN</c> to <c>MTEXTEND</c>: the line spacing
    /// style and factor again, and a 74 of nought), the leader's note of AutoCAD's own drawing included: it has it in R2000 and in no
    /// later version (leaders_AC1015 against leaders_AC1032).
    /// </summary>
    public class DxfSaveVersionMTextDataTests
    {
        private static byte[] Saved(byte[] file, string version)
        {
            DxfDatabase db = DxfDatabase.Parse(System.Text.Encoding.GetEncoding(28591).GetString(file));

            using (var stream = new MemoryStream())
            {
                db.Save(stream, version);
                return stream.ToArray();
            }
        }

        private static List<DwgAnswerKey.Record> MTexts(byte[] dxf) =>
            DwgAnswerKey.Parse(dxf).Where(r => r.Type == "MTEXT").ToList();

        /// <summary>The groups of an MTEXT but its handle, owner and reactors, as <c>code=value</c>.</summary>
        private static string Shown(DwgAnswerKey.Record record) =>
            string.Join(
                " ",
                record
                    .Groups.Where(g => g.Key != 5 && g.Key != 330 && g.Key != 102 && g.Key != 360)
                    .Select(g => g.Key + "=" + g.Value.Trim())
            );

        private const string Data =
            " 1001=ACAD 1000=MTEXTBEGIN 1070=73 1070=1 1070=44 1040=1.0 1070=74 1070=0 1000=MTEXTEND";

        [Fact]
        public void TheNoteOfALeader_GoingToR2000_HasTheExtendedDataAutoCadWritesIt()
        {
            byte[] saved = Saved(DwgAnswerKey.Key("leaders_AC1032"), "AC1015");
            DwgAnswerKey.Record note = Assert.Single(MTexts(saved));

            Assert.Equal(Shown(Assert.Single(MTexts(DwgAnswerKey.Key("leaders_AC1015")))), Shown(note));
            Assert.EndsWith(Data, Shown(note));
        }

        [Fact]
        public void ALineSpacingOfItsOwn_IsRepeatedInTheExtendedData()
        {
            string text = System
                .Text.Encoding.GetEncoding(28591)
                .GetString(DwgAnswerKey.Key("leaders_AC1032"))
                .Replace("\r\n", "\n")
                .Replace("73\n     1\n 44\n1.0\n", "73\n     2\n 44\n1.5\n");
            byte[] saved = Saved(System.Text.Encoding.GetEncoding(28591).GetBytes(text), "AC1015");

            Assert.EndsWith(
                " 73=2 44=1.5 1001=ACAD 1000=MTEXTBEGIN 1070=73 1070=2 1070=44 1040=1.5 1070=74 1070=0 1000=MTEXTEND",
                Shown(Assert.Single(MTexts(saved)))
            );
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1032")]
        public void TheNoteOfALeaderOfR2000_GoingUp_LosesTheExtendedData(string version)
        {
            byte[] saved = Saved(DwgAnswerKey.Key("leaders_AC1015"), version);
            DwgAnswerKey.Record note = Assert.Single(MTexts(saved));

            Assert.DoesNotContain(note.Groups, g => g.Key == 1001);
            Assert.DoesNotContain(note.Groups, g => g.Key == 1000 && g.Value == "MTEXTBEGIN");
        }

        [Fact]
        public void TheMTextsOfR2000_GoingToR2004AndBack_AreTheSame()
        {
            byte[] key = DwgAnswerKey.Key("later");
            byte[] back = Saved(Saved(key, "AC1018"), "AC1015");

            Assert.Equal(MTexts(key).Select(Shown), MTexts(back).Select(Shown));
        }
    }
}
