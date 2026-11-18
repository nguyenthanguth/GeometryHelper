using System;
using System.IO;
using System.Text;
using Xunit;

namespace GeometryHelper.Cad.Remote.UnitTest
{
    public class RemoteJsonTests
    {
        [Fact]
        public void AMessage_IsWrittenInTheOrderItsMembersWereSet_AndReadBackAsItWas()
        {
            var message = new JsonObject()
                .Set("id", 7L)
                .Set("op", "commit")
                .Set("ok", true)
                .Set("none", null)
                .Set("x", 0.1 + 0.2)
                .Set("list", new JsonArray(new object[] { "a", 2L, false, new JsonObject().Set("k", "v") }));

            string text = RemoteJson.Write(message);

            Assert.StartsWith(
                "{\"id\":7,\"op\":\"commit\",\"ok\":true,\"none\":null,\"x\":0.30000000000000004,",
                text,
                StringComparison.Ordinal
            );
            var back = (JsonObject)RemoteJson.Parse(text);
            Assert.Equal(7L, back.GetLong("id"));
            Assert.Equal("commit", back.GetString("op"));
            Assert.True(back.GetBool("ok"));
            Assert.Equal(0.1 + 0.2, (double)back.Get("x"));
            Assert.Equal("v", ((JsonObject)back.GetArray("list")[3]).GetString("k"));
            Assert.Equal(text, RemoteJson.Write(back));
        }

        [Fact]
        public void ADxfText_WithLineBreaksQuotesBackslashesAndNonAsciiLetters_SurvivesTheFrame()
        {
            string dxf = "  0\r\nSECTION\n  2\nHEADER\n  1\n\"Đường\\n\" \u0001 ∅12 \U0001F600\n";
            var message = new JsonObject().Set("fragment", dxf);
            using (var stream = new MemoryStream())
            {
                RemoteFrames.Write(stream, message);
                stream.Position = 0;

                JsonObject back = RemoteFrames.Read(stream);

                Assert.Equal(dxf, back.GetString("fragment"));
                Assert.Null(RemoteFrames.Read(stream));
            }
        }

        [Fact]
        public void AFrame_IsItsLengthLeastSignificantFirst_ThenItsJsonInUtf8()
        {
            using (var stream = new MemoryStream())
            {
                RemoteFrames.Write(stream, new JsonObject().Set("a", "é"));

                byte[] bytes = stream.ToArray();
                string body = "{\"a\":\"é\"}";

                Assert.Equal(Encoding.UTF8.GetByteCount(body), bytes[0] | (bytes[1] << 8));
                Assert.Equal(0, bytes[2]);
                Assert.Equal(body, Encoding.UTF8.GetString(bytes, 4, bytes.Length - 4));
            }
        }

        [Fact]
        public void AFrameCutShort_OrOfAnImpossibleLength_IsAnErrorNotAHang()
        {
            Assert.Throws<IOException>(() => RemoteFrames.Read(new MemoryStream(new byte[] { 10, 0, 0, 0, 1, 2 })));
            Assert.Throws<IOException>(() =>
                RemoteFrames.Read(new MemoryStream(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }))
            );
            Assert.Throws<IOException>(() =>
                RemoteFrames.Read(new MemoryStream(new byte[] { 3, 0, 0, 0, (byte)'{', (byte)'}', (byte)'x' }))
            );
        }

        [Theory]
        [InlineData("{")]
        [InlineData("{\"a\":}")]
        [InlineData("[1,2")]
        [InlineData("{\"a\":1} x")]
        [InlineData("\"abc")]
        public void TextThatIsNotJson_IsAFormatError(string text)
        {
            Assert.Throws<FormatException>(() => RemoteJson.Parse(text));
        }
    }
}
