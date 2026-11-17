using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A multileader mirrored across a line at 45 degrees keeps its plane's x axis square to itself, not turned back, as
    /// AutoCAD 2021 reads it (mirror-insert GHMI1). The line picked at whole numbers has its matrix exact since
    /// <see cref="GeoTransform3.Mirror(GeoPlane3)"/> was made to, but one picked at decimal points ((1.1, 2.2) to (3.3, 4.4):
    /// 2.1999999999999997 across, 2.2 up) is 45 degrees within a last digit and its mirrored x axis meets the old one at
    /// -2.2E-16, which only the tolerance of <c>DxfMLeader.FinishMirror</c> reads as square.
    /// </summary>
    public class MLeaderMirror45Tests
    {
        private static IReadOnlyList<double> Axes(CadDocument document, string handle)
        {
            string[] lines = document.Database.ToText().Replace("\r\n", "\n").Split('\n');
            var found = new Dictionary<int, double>();
            bool inside = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1].Trim();

                if (code == 0 && inside)
                {
                    break;
                }

                if (code == 5 && value == handle)
                {
                    inside = true;
                }
                else if (
                    inside && (code == 111 || code == 121 || code == 131 || code == 112 || code == 122 || code == 132)
                )
                {
                    found[code] = double.Parse(value, CultureInfo.InvariantCulture);
                }
            }

            return new[] { 111, 121, 131, 112, 122, 132 }.Select(c => found[c]).ToList();
        }

        private static IReadOnlyList<double> MirroredAxes(GeoPoint2 first, GeoPoint2 second)
        {
            CadDocument document = BlankWithMLeaderStyle();
            Run(Session(document), "MLD", P(10, 0), P(30, 20), "II");
            DxfMLeader source = Assert.Single(All<DxfMLeader>(document.ModelSpace));
            Run(Session(document), "MIRROR", new[] { source.Handle }, Enter, first, second, Enter);
            DxfMLeader copy = All<DxfMLeader>(document.ModelSpace).Single(e => e.Handle != source.Handle);
            return Axes(document, copy.Handle);
        }

        [Fact]
        public void AMultileaderMirroredAcrossALineAt45DegreesPickedAtDecimalPoints_HasItsPlaneAsAcrossOneAtWholeNumbers()
        {
            IReadOnlyList<double> whole = MirroredAxes(P(0, 0), P(10, 10));
            IReadOnlyList<double> decimals = MirroredAxes(P(1.1, 2.2), P(3.3, 4.4));

            for (int i = 0; i < whole.Count; i++)
            {
                Assert.Equal(whole[i], decimals[i], 9);
            }
        }
    }
}
