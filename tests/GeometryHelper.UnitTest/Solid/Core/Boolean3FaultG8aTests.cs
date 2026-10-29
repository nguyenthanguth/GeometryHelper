using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Unions of a column and a beam whose end stands exactly the tolerance into or clear of its side, which share nothing
    /// at the tolerance: a union that came out open on the way is checked to hold anything from the two less the slab to the
    /// two and the gap, the faces where they meet within the tolerance, not the two exactly.
    /// </summary>
    public class Boolean3FaultG8aTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Theory]
        [InlineData(0.045782150489176665, -0.19460321343252585, -0.037744915993299766, 4.0049806060292665, 239.2036772515642, 344.8615182865698, false)]
        [InlineData(0.045782150489176665, -0.19460321343252585, -0.037744915993299766, 4.0049806060292665, 239.2036772515642, 344.8615182865698, true)]
        [InlineData(-0.426282864029651, 0.06595052618810471, -0.36435328294399816, 2.0168732954267754, 115.52392743319456, 258.02756890562716, false)]
        [InlineData(-0.426282864029651, 0.06595052618810471, -0.36435328294399816, 2.0168732954267754, 115.52392743319456, 258.02756890562716, true)]
        public void ABeamRunningExactlyAThousandthIntoAColumn_IsJoined(double ax, double ay, double az, double angle, double w, double h, bool beamFirst)
        {
            // A column 300 by 300 by 1 000 and a beam from 299.999 to 2 000 along x, turned about the origin. The union came
            // out open and, closed, held the two less 13.75 and 4.97, and was not made: sharing nothing at the tolerance, it
            // was held to the two exactly, where the slab is 82.5 and 29.8.
            var tolerance = new Tolerance(0.001, 1E-5, 1E-5, 0.001);
            const double d = 0.001;
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(ax, ay, az), angle);
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 1000).TransformBy(turn);
            GeoSolid3 beam = Box(300 - d, 150 - w / 2, 500 - h / 2, 2000, 150 + w / 2, 500 + h / 2).TransformBy(turn);
            double two = 300.0 * 300 * 1000 + (1700 + d) * w * h;
            double slab = d * w * h;

            GeoSolid3 union;
            Assert.True(beamFirst ? beam.TryUnion(column, out union, tolerance) : column.TryUnion(beam, out union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());
            Assert.InRange(union.GetVolume(tolerance), two - slab - 1E-3, two + 1E-3);
        }

        [Theory]
        [InlineData(-0.21499471260001635, -0.21264074403449928, -0.2137702689817037, 42.2151892875392, 0.19047016100514225, 0.001, -20, 30, 20, 60, false)]
        [InlineData(-0.21499471260001635, -0.21264074403449928, -0.2137702689817037, 42.2151892875392, 0.19047016100514225, 0.001, -20, 30, 20, 60, true)]
        [InlineData(-0.013161964022164241, -0.4819161952389014, -0.10200243767118194, 7.0752715026378965, 3.503710397288068, 0.001, -20, -20, 20, 20, false)]
        public void ABlockRunningExactlyTheToleranceIntoACubesTop_IsJoined(double ax, double ay, double az, double ox, double angle, double tol, double x0, double y0, double x1, double y1, bool blockFirst)
        {
            // A cube of 100 and a block 50 high whose foot runs exactly the tolerance into the cube's top over its edge, or its
            // corner, turned. The union came out open and, closed, held the two less 0.2, and less 0.67, and was not made; the
            // slab is 0.6, and 0.4.
            var tolerance = new Tolerance(tol, 1E-5, 1E-5, tol);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(ox, 0, 0), new GeoVector3(ax, ay, az), angle);
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 block = Box(x0, y0, 100 - tol, x1, y1, 150).TransformBy(turn);
            double two = 1E6 + (x1 - x0) * (y1 - y0) * (50 + tol);
            double slab = tol * x1 * (y1 - System.Math.Max(0, y0));

            GeoSolid3 union;
            Assert.True(blockFirst ? block.TryUnion(cube, out union, tolerance) : cube.TryUnion(block, out union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            // Over the edge the union holds between the two less the slab and the two. At the corner it holds the two less
            // 1.67 times the slab, the faces where they meet a further 0.27 in, which is within the tolerance of a union too.
            bool corner = y0 < 0;
            Assert.InRange(union.GetVolume(tolerance), two - (corner ? 2 : 1) * slab - 1E-6, two + 1E-6);
        }

        [Theory]
        [InlineData(0.1350835569366271, 0.4313071984477841, -0.4327822054204448, 32.51210343209659, 4.569126457240957, 0.001, 0.001)]
        [InlineData(-0.41481209635492977, 0.22046442456565074, 0.4379260156233451, 18.52324999334442, 0.5034039982144739, 0.001, 0.001)]
        [InlineData(0.4187595988245493, 0.2226362706733105, -0.28656169219853433, 45.17612014672538, 2.6073217348229707, 0.01, -0.01)]
        public void TwoCubesExactlyTheToleranceApartOrIntoEachOther_AreJoined(double ax, double ay, double az, double ox, double angle, double tol, double gap)
        {
            // Two cubes of 100 side by side, turned, the second the tolerance clear of the first, or into it. The union came out
            // open and, closed, held the two and 5 and 6.67 of the gap's 10, and the two less the slab of 100, and was not
            // made: it was held to the two, or to the two and the gap.
            var tolerance = new Tolerance(tol, 1E-5, 1E-5, tol);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(ox, 0, 0), new GeoVector3(ax, ay, az), angle);
            GeoSolid3 first = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 second = Box(100 + gap, 0, 0, 200, 100, 100).TransformBy(turn);
            double two = 2E6 - gap * 100 * 100;
            double slab = System.Math.Abs(gap) * 100 * 100;

            Assert.True(first.TryUnion(second, out GeoSolid3 union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            if (gap > 0)
            {
                Assert.InRange(union.GetVolume(tolerance), two - 1E-6, two + slab + 1E-6);
            }
            else
            {
                Assert.InRange(union.GetVolume(tolerance), two - slab - 1E-3, two + 1E-6);
            }
        }
    }
}
