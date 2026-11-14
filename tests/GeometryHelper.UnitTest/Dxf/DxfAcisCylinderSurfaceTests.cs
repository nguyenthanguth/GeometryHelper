using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The surface of a closed spline extruded square to its plane (<c>cylsur</c> of R2004, <c>cyl_spl_sur</c> of R2007 and later:
    /// the number 22600 after its name, its curve of the exact kind in the form of its version, and from R2010 a flag F before its
    /// closing brace) and the number of the vertex where the closed edge of its profile meets the seam, which is 2 (a closed edge
    /// alone has 0). A box 30 by 20 by 10 and a closed spline of 8 control points were extruded 8 high in AutoCAD 2021, which
    /// wrote the same body in R2004, R2007 and R2010 as these records, the profile here a simpler curve of the same form: the
    /// conversions of the three versions between each other were record for record AutoCAD's, as were those of a sweep of a circle
    /// along an arc.
    /// </summary>
    public class DxfAcisCylinderSurfaceTests
    {
        private static readonly string[] Head =
        {
            "20800 8 2 0          ",
            "16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0  ",
            "25.39999999999999858 9.999999999999999547e-07 1.000000000000000036e-10 ",
        };

        private static readonly string[] Profile =
        {
            " 0 3 10 1 20 1 30 1 40 1 ",
            " 50 3 ",
            " 0 0 0 ",
            " 3 1 0 ",
            " 10 3 0 ",
            " 18 16 0 ",
            " 3 23 0 ",
            " -8 10 0 ",
            " -2 3 0 ",
            " 0 0 0 ",
            " 0 ",
            " ",
            " null_surface ",
            " null_surface ",
            " nullbs ",
            " nullbs ",
            " I I ",
            " 0 ",
            " 0 ",
            " 4 10 20 30 40 ",
        };

        private static readonly string[] Tail =
        {
            " 0 0 1 ",
            " 3 7 0 ",
            " none F 0 F 50 F 0 F 8 CLOSED OPEN NON_SINGULAR NON_SINGULAR ",
            " 0 ",
            " 0 ",
            " 4 10 20 30 40 ",
            " 0 ",
            " 0 ",
            " 0 ",
        };

        /// <summary>The surface in the form of one version of the modeler (<paramref name="form"/> 0 R2004, 1 R2007, 2 R2010).</summary>
        private static List<string> Surface(int form)
        {
            var lines = new List<string>(Head);
            lines[0] =
                (
                    form == 0 ? "20800"
                    : form == 1 ? "21200"
                    : "21500"
                ) + " 6 2 0          ";
            lines.Add("asmheader $-1 -1 @13 226.0.0.65535 #");
            string name = form == 0 ? "cylsur" : "cyl_spl_sur 22600";
            string curve =
                form == 0 ? "exactcur full"
                : form == 1 ? "exact_int_cur full"
                : "exact_int_cur 22600 full";
            lines.Add("spline-surface $-1 -1 $-1 forward { " + name + " ");
            lines.Add(" intcurve forward { " + curve + " nubs 3 closed 6 ");
            lines.AddRange(Profile);

            if (form > 0)
            {
                lines.Add(" ");
            }

            lines.Add(
                form == 0 ? " F 1 F 0 } F 0 F 50 "
                : form == 1 ? " 0 F 1 F 0 } F 0 F 50 "
                : " 0 F 1 F 0 UNEXTENDED UNEXTENDED } F 0 F 50 "
            );
            lines.AddRange(Tail);
            lines.Add(form == 2 ? " F } I I I I #" : " } I I I I #");
            return lines;
        }

        private static List<string> Records(string text) => text.TrimEnd('\n').Split('\n').ToList();

        [Theory]
        [InlineData(0, 1)]
        [InlineData(0, 2)]
        [InlineData(1, 0)]
        [InlineData(1, 2)]
        [InlineData(2, 0)]
        [InlineData(2, 1)]
        public void ACylinderSweptAlongAClosedSpline_IsTheSameRecordsInTheFormOfEachVersion(int from, int to)
        {
            string[] versions = { "AC1018", "AC1021", "AC1024" };
            string sat = DwgSat.ToSat(Surface(from), AcisFormat.For(versions[to]));

            Assert.NotNull(sat);
            Assert.Equal(Surface(to).Skip(1), Records(sat).Skip(1));
        }

        [Fact]
        public void ACylinderSweptAlongAClosedSpline_GoesIntoTheSabOfR2013AndBack()
        {
            byte[] sab = DwgSat.ToSab(Surface(1), AcisFormat.For("AC1032"), string.Empty);

            Assert.NotNull(sab);
            Assert.Equal(Surface(2).Skip(1), Records(DwgSat.ToSat(sab, AcisFormat.For("AC1024"))).Skip(1));
        }

        private static List<string> Edges(bool seam)
        {
            var lines = new List<string>(Head);
            lines[0] = "20800 7 2 0          ";
            lines.Add("asmheader $-1 -1 @13 226.0.0.65535 #");
            lines.Add("edge $-1 -1 $-1 $3 0 $3 50 $-1 $-1 forward @7 unknown #");
            lines.Add(
                seam
                    ? "edge $-1 -1 $-1 $3 0 $4 8 $-1 $-1 forward @7 unknown #"
                    : "edge $-1 -1 $-1 $4 0 $4 8 $-1 $-1 forward @7 unknown #"
            );
            lines.Add("vertex $-1 -1 $-1 $1 $5 #");
            lines.Add("vertex $-1 -1 $-1 $2 $6 #");
            lines.Add("point $-1 -1 $-1 0 0 0 #");
            lines.Add("point $-1 -1 $-1 0 0 8 #");
            return lines;
        }

        [Fact]
        public void TheVertexOfAClosedEdgeThatMeetsASeam_IsNumberedTwo()
        {
            string with = DwgSat.ToSat(Edges(seam: true), AcisFormat.For("AC1021"));

            Assert.Contains("\nvertex $-1 -1 $-1 $1 2 $5 #\n", with);
            Assert.Contains("\nvertex $-1 -1 $-1 $2 1 $6 #\n", with);
        }

        [Fact]
        public void TheVertexOfAClosedEdgeThatMeetsNoSeam_IsNumberedNought()
        {
            string alone = DwgSat.ToSat(Edges(seam: false), AcisFormat.For("AC1021"));

            Assert.Contains("\nvertex $-1 -1 $-1 $1 0 $5 #\n", alone);
            Assert.Contains("\nvertex $-1 -1 $-1 $2 0 $6 #\n", alone);
        }
    }
}
