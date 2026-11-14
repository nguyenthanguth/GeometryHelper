using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The last two flags of an ACIS transform, <c>reflect</c> and <c>shear</c>, in every version. AutoCAD 2021 never writes
    /// them: it bakes a mirror into the geometry of a solid (a box 30 by 20 by 10 mirrored about x = 0 and moved has the
    /// transform of a move alone) and refuses to scale one unevenly (<c>eCannotScaleNonUniformly</c>). It reads them from a
    /// file, as the probe of a box with the transform edited showed: <c>shear</c> shears it (its extents 30 by 35 by 10, from
    /// 30 by 20 by 10) and no other word of the flag is read (a body with another is one it reports a modeling error of).
    /// </summary>
    public class DxfAcisTransformWordsTests
    {
        private static List<string> Records400(string transform) =>
            new List<string>
            {
                "400 3 1 0           ",
                "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
                "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
                "body $-1 $1 $-1 $2 #",
                "lump $-1 $-1 $-1 $0 #",
                transform,
            };

        private static string Joined(IEnumerable<string> lines) => string.Join("\n", lines) + "\n";

        [Theory]
        [InlineData("-1 0 0 0 1 0 0 0 1 100 0 0 1 no_rotate reflect no_shear")]
        [InlineData("-1 0 0 0 1 0 0 0 1 100 0 0 1 rotate reflect no_shear")]
        [InlineData("1 0.5 0 0 1 0 0 0 1 100 0 0 1 no_rotate no_reflect shear")]
        [InlineData("0.5 0.25 0 -0.25 0.5 0 0 0 1 0 0 0 2 rotate reflect shear")]
        public void ATransformThatReflectsOrShears_KeepsItsWords_InEveryVersion(string flags)
        {
            List<string> body = Records400("transform $-1 " + flags + " #");

            foreach (string version in new[] { "AC1018", "AC1021", "AC1024" })
            {
                string up = DwgSat.ToSat(body, AcisFormat.For(version));

                Assert.NotNull(up);
                Assert.Contains(" " + flags + " #", up);
                Assert.Equal(
                    Joined(body),
                    DwgSat.ToSat(up.TrimEnd('\n').Split('\n').ToList(), AcisFormat.For("AC1015"))
                );
            }

            byte[] sab = DwgSat.ToSab(body, AcisFormat.For("AC1032"), string.Empty);

            Assert.NotNull(sab);
            Assert.Contains(" " + flags + " #", DwgSat.ToSat(sab, AcisFormat.For("AC1032")));
        }

        [Theory]
        [InlineData("no_rotate reflektt no_shear")]
        [InlineData("no_rotate no_reflect Shear")]
        public void ATransformWithAnotherWord_IsRefused_ForAutoCadReadsNoOtherWord(string flags)
        {
            List<string> body = Records400("transform $-1 1 0 0 0 1 0 0 0 1 0 0 0 1 " + flags + " #");

            Assert.Null(DwgSat.ToSab(body, AcisFormat.For("AC1032"), string.Empty));
            Assert.Null(DwgSat.ToSat(body, AcisFormat.For("AC1018")));
        }
    }
}
