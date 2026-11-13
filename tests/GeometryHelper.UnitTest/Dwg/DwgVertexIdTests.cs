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
    /// A vertex of a 2D polyline in a DWG of R2010 and later: the vertex id (group 91) comes between the bulge and the
    /// tangent direction (group 50). A vertex written with the tangent first, its id after, was refused by AutoCAD 2021
    /// (ErrorStatus 53, eFilerError) as soon as the tangent was other than 0, and a vertex AutoCAD wrote with a tangent
    /// was read with none.
    /// </summary>
    public class DwgVertexIdTests
    {
        /// <summary>
        /// The two vertices of a polyline AutoCAD 2021 saved as R2018 (a Polyline2d of four points made in code in a side
        /// database): handle 7A has a bulge of 0.25 and a tangent of 1 radian, handle 7B a tangent of a quarter turn. Each
        /// is the object from its size to its check sum.
        /// </summary>
        private const string VertexWithBulgeAndTangent =
            "20002502805EA15A0000874080000000000002440AA000000000000340FE58204A2A9FBF20";

        private const string VertexWithTangent =
            "28002502805EE15A000087408000000000000244000000000000005102AA060B51153EC87E4FD8206A2A9FBCE5";

        private static DwgObject Read(string hex, ulong handle)
        {
            byte[] bytes = Enumerable
                .Range(0, hex.Length / 2)
                .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
                .ToArray();
            return new DwgObjectReader(bytes, new List<DwgClass>(), "AC1032").Read(handle, 0);
        }

        private static double Group(DwgObject vertex, int code) =>
            Convert.ToDouble(vertex.Body.Single(g => g.Code == code).Value, CultureInfo.InvariantCulture);

        [Fact]
        public void AVertexAutoCadWroteWithATangent_IsReadWithTheTangent()
        {
            DwgObject vertex = Read(VertexWithTangent, 0x7B);

            Assert.Equal("VERTEX", vertex.Name);
            Assert.Equal(90.0, Group(vertex, 50), 9);
            Assert.DoesNotContain(vertex.Body, g => g.Code == 91);
        }

        [Fact]
        public void AVertexAutoCadWroteWithABulgeAndATangent_IsReadWithBoth()
        {
            DwgObject vertex = Read(VertexWithBulgeAndTangent, 0x7A);

            Assert.Equal(0.25, Group(vertex, 42), 9);
            Assert.Equal(180.0 / Math.PI, Group(vertex, 50), 9);
        }

        /// <summary>The DXF of the drawing of polylines with the second vertex of the first given an id and a tangent.</summary>
        private static string WithATangentAndAnId()
        {
            List<string> lines = DxfDatabase
                .Open(new MemoryStream(DwgAnswerKey.Key("polys"), false))
                .ToText()
                .Replace("\r\n", "\n")
                .Split('\n')
                .ToList();
            int bulge = lines.FindIndex(l => l.Trim() == "VERTEX");
            bulge = lines.FindIndex(bulge + 1, l => l.Trim() == "VERTEX");
            bulge = lines.FindIndex(bulge, l => l.Trim() == "42");
            Assert.Equal("0.5", lines[bulge + 1].Trim());
            lines.InsertRange(bulge + 2, new[] { "91", "7", "50", "90.0" });
            return string.Join("\n", lines);
        }

        [Fact]
        public void AVertexWithAnIdAndATangent_WrittenAsDwg_IsReadBackWithBoth()
        {
            byte[] back = DwgReader.ToDxf(DwgWriter.ToDwg(WithATangentAndAnId(), "AC1032"));

            DwgAnswerKey.Record vertex = Assert.Single(DwgAnswerKey.Parse(back), r =>
                r.Type == "VERTEX"
                && r.Groups.Any(g => g.Key == 42 && g.Value.Trim() == "0.5")
                && r.Groups.Any(g => g.Key == 91)
            );
            Assert.Equal("7", vertex.Groups.Single(g => g.Key == 91).Value.Trim());
            Assert.Equal(
                90.0,
                double.Parse(vertex.Groups.Single(g => g.Key == 50).Value, CultureInfo.InvariantCulture),
                9
            );
        }
    }
}
