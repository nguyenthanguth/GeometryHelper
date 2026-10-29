using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Unions of a column and a beam whose end runs exactly the tolerance into its side, turned and moved, which came out
    /// wound against itself however they were worked out: a union is worked out once more with a corner read as on a plane
    /// within what rounding adds to the tolerance, and taken where that closes wound alike and holds no less than the column
    /// and the beam beyond its face.
    /// </summary>
    public class Boolean3FaultG8eTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Theory]
        [InlineData(0.2395568307207696, -0.339326023515, -0.2179910112502943, 0.9992652893994307, 711.3330898393565, 802.1510018977108, 33.0569239021544, 386.69003354697026, 364.8172751836559, false)]
        [InlineData(0.2395568307207696, -0.339326023515, -0.2179910112502943, 0.9992652893994307, 711.3330898393565, 802.1510018977108, 33.0569239021544, 386.69003354697026, 364.8172751836559, true)]
        [InlineData(-0.33995636172590604, 0.09602619688772884, -0.05376437162364106, 4.846547078735449, 298.7642564339397, 470.00785566401106, 43.887355897523165, 356.0942423791132, 267.2554473705848, false)]
        [InlineData(0.13021495921081627, 0.1476146409509772, 0.4546852920212249, 2.417831423886973, 619.2661102950881, 22.541310648685933, 24.869296292247856, 320.33824232422666, 171.6280659994241, false)]
        [InlineData(0.16170491402116838, 0.06390027355584327, -0.40268392453700486, 2.046917811057958, 0, 0, 0, 322.0229114042702, 131.28827215698, false)]
        public void ABeamRunningExactlyAThousandthIntoAColumnWoundAgainstItself_IsJoined(double ax, double ay, double az, double angle, double sx, double sy, double sz, double w, double h, bool beamFirst)
        {
            // A column 300 by 300 by 1 000 and a beam wider than it from 0.001 inside its side to 2 000 along x, turned about
            // the origin and moved. The first four came out wound against itself either way, holding the two and 63.5 or 73.3,
            // or the two less 117.2, and were not made; the fifth came out so holding the two and was made, though Validate
            // read it as not valid. They hold the two less 125.26, 87.67, 53.23 and 40.83 now, the slab where they overlap
            // being 109.45, 80.18, 51.49 and 39.39, and the beam's whole end 141.07, 95.17, 54.98 and 42.28.
            var tolerance = new Tolerance(0.001, 1E-5, 1E-5, 0.001);
            const double d = 0.001;
            GeoTransform3 place = GeoTransform3.Translation(new GeoVector3(sx, sy, sz)) * GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(ax, ay, az), angle);
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 1000).TransformBy(place);
            GeoSolid3 beam = Box(300 - d, 150 - w / 2, 500 - h / 2, 2000, 150 + w / 2, 500 + h / 2).TransformBy(place);
            double two = 300.0 * 300 * 1000 + (1700 + d) * w * h;
            double end = d * w * h;

            GeoSolid3 union;
            Assert.True(beamFirst ? beam.TryUnion(column, out union, tolerance) : column.TryUnion(beam, out union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            // No less than the column and the beam beyond its face, its end moved onto it; no more than the two.
            Assert.InRange(union.GetVolume(tolerance), two - end - 1E-3, two + 1E-3);
        }
    }
}
