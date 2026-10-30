using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two openings of a body whose walls stand apart, within the tolerance of one plane, are each cut in their own plane.
    /// Cut within a thousandth, the plane of the first opening cut the whole body, the plane of the second found the cell's
    /// corners on it and left it whole, and the second opening's wall was cut 0.0009 off, in the first one's plane, 27 away.
    /// </summary>
    public class OpeningWallsApartG1u2Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        /// <summary>
        /// An L 100 by 80, its legs 20 wide, 50 high, its corner at (far, far, far), with a blind opening 13 by 8.8 up from its
        /// floor 22 deep, and one 15.5 by 6.5 down from its top 37 deep; the far walls of the two stand 0.0009 apart, the
        /// openings 27 apart along them.
        /// </summary>
        private static GeoSolid3 L(double far, bool turnedRound)
        {
            GeoPoint3 At(double x, double y, double z) => new GeoPoint3(far + x, far + y, far + z);

            GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
                => GeoSolid3.Extrude(new GeoPolygon3(At(x0, y0, z0), At(x1, y0, z0), At(x1, y1, z0), At(x0, y1, z0)), new GeoVector3(0, 0, z1 - z0), Thousandth);

            GeoSolid3 body = GeoSolid3.Extrude(
                new GeoPolygon3(At(0, 0, 0), At(100, 0, 0), At(100, 20, 0), At(20, 20, 0), At(20, 80, 0), At(0, 80, 0)), new GeoVector3(0, 0, 50), Thousandth);
            GeoSolid3 up = Box(35, 4.6, -7, 48, 13.4271, 22);
            GeoSolid3 down = Box(75, 6.9, 13, 90.5, 13.428, 57);

            return body.WithOpenings(turnedRound ? new[] { down, up } : new[] { up, down });
        }

        /// <summary>The L less what the openings take of it.</summary>
        private const double Held = 3_200 * 50 - 13 * (13.4271 - 4.6) * 22 - 15.5 * (13.428 - 6.9) * 37;

        [Theory]
        [InlineData(0.0, false)]
        [InlineData(0.0, true)]
        [InlineData(1E6, false)]
        [InlineData(1E6, true)]
        public void TwoOpeningsWithWallsANineTenThousandthApart_EachKeepItsOwnWall(double far, bool turnedRound)
        {
            // Cut within a thousandth, the L held 0.51 too much one way round and 0.26 too little the other.
            GeoSolid3 solid = L(far, turnedRound);

            Assert.Equal(Held, solid.GetVolume(Thousandth), 1E-5);
            Assert.True(solid.TryCutOpenings(out GeoSolid3 material, Thousandth));
            Assert.Equal(Held, material.GrossVolume, 1E-5);
            Assert.True(material.IsClosed(Thousandth));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E6)]
        public void TwoOpeningsWithWallsANineTenThousandthApart_CutTheirOwnSection(double far)
        {
            // Halfway up, across both openings: the L less the floor of each.
            GeoFace3[] section = L(far, false).Section(new GeoPlane3(new GeoPoint3(far + 50, far + 10, far + 17.5), new GeoVector3(0, 0, 1)), Thousandth);

            Assert.Equal(3_200 - 13 * (13.4271 - 4.6) - 15.5 * (13.428 - 6.9), section.Sum(f => f.Area), 1E-6);
        }
    }
}
