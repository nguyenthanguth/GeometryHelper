using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A DXF read from a stream: from where the stream stands, whether it can seek or not, and the text of a file of R2007
    /// or later whose bytes are not all UTF-8.
    /// </summary>
    public class DxfReaderStreamTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static string Drawing(string version, string layer)
            => G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, version, 0, "ENDSEC", 0, "SECTION", 2, "ENTITIES")
            + G(0, "LINE", 8, layer, 10, "0", 20, "0", 11, "1", 21, "0", 0, "ENDSEC", 0, "EOF");

        /// <summary>A stream that reads and cannot seek, as a network stream reads.</summary>
        private sealed class OneWay : Stream
        {
            private readonly Stream _inner;

            internal OneWay(Stream inner)
            {
                _inner = inner;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        [Fact]
        public void AStream_IsReadFromWhereItStands_WhetherItCanSeekOrNot()
        {
            byte[] junk = Encoding.ASCII.GetBytes("not the drawing\n");
            byte[] drawing = Encoding.UTF8.GetBytes(Drawing("AC1032", "Grün"));
            byte[] both = junk.Concat(drawing).ToArray();

            using (var seeking = new MemoryStream(both))
            using (var oneWay = new OneWay(new MemoryStream(drawing)))
            {
                seeking.Position = junk.Length;

                Assert.Equal("Grün", Assert.Single(DxfReader.Read(seeking, new DxfReadOptions { Tolerance = Tol }).Shapes).Layer);
                Assert.Equal("Grün", Assert.Single(DxfReader.Read(oneWay, new DxfReadOptions { Tolerance = Tol }).Shapes).Layer);
            }
        }

        [Fact]
        public void AFileOfR2007OrLaterWhoseBytesAreNotAllUtf8_IsReadWithTheReplacementCharacter_AndSaid()
        {
            // 0xFC is ü in a code page, and no UTF-8 byte stands alone like that.
            byte[] text = Encoding.ASCII.GetBytes(Drawing("AC1032", "gr#n"));
            text[Array.IndexOf(text, (byte)'#')] = 0xFC;

            using (var stream = new MemoryStream(text))
            {
                DxfDocument read = DxfReader.Read(stream, new DxfReadOptions { Tolerance = Tol });

                Assert.Equal("gr�n", Assert.Single(read.Shapes).Layer);
                Assert.Contains(read.Warnings, w => w.StartsWith("The file is of R2007 or later so its text is UTF-8, but some of its bytes are not", StringComparison.Ordinal));
            }
        }
    }
}
