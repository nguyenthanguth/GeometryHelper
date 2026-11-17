using System;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A long text of a DWG of R2007 or later (an MTEXT of 300 Korean letters) is written to the DXF in pieces of 250 bytes, none
    /// of which ends inside a letter of UTF-8, whatever the code page of the drawing's header is: a drawing made on a Korean
    /// machine has ANSI_949, a double-byte page, and its pieces were cut after every second byte of a letter of three.
    /// </summary>
    public sealed class DwgLongTextUtf8Tests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        [Fact]
        public void KoreanLetters_InADoubleByteCodePage_AreNotCutInsideALetterOfUtf8()
        {
            string korean = string.Concat(Enumerable.Repeat("돋움체", 100));
            var writer = new DwgDxfWriter(4096);

            writer.WriteChunks(3, 1, Encoding.UTF8.GetBytes(korean), 250, doubleByte: true, utf8: true);

            string[] lines = Latin1
                .GetString(writer.ToArray())
                .Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            var strict = new UTF8Encoding(false, true);
            string[] pieces = lines
                .Where((l, i) => i % 2 == 1)
                .Select(l => strict.GetString(Latin1.GetBytes(l)))
                .ToArray();

            Assert.Equal(new[] { "3", "3", "3", "1" }, lines.Where((l, i) => i % 2 == 0));
            Assert.Equal(korean, string.Concat(pieces));
            Assert.All(pieces, p => Assert.InRange(Encoding.UTF8.GetByteCount(p), 1, 250));
        }
    }
}
