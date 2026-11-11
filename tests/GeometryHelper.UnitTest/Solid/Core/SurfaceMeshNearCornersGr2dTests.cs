using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face whose corners stand within the tolerance of edges they are not on is meshed to its own area. Such a face is
    /// cut into strips, and each of its rings was read within the tolerance first: corners closer than it were merged,
    /// corners within a hundredth of it of a straight run dropped, and a ring thinner than it left out, so the triangles
    /// lost or gained what lay within the tolerance. The rings are read within rounding now.
    /// </summary>
    public class SurfaceMeshNearCornersGr2dTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, 0.017453292519943295, 0.01);

        private static readonly Tolerance Millionth = new Tolerance(1E-6, 1E-5, 0.017453292519943295, 1E-6);

        private static readonly Tolerance Fine = new Tolerance(1E-9, 1E-9, 1E-9, 1E-9);

        private static Tolerance Within(double tolerance) => tolerance == 1E-2 ? Hundredth : Thousandth;

        private static GeoPoint3 At(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPolygon3 Flat(params double[] xy)
        {
            var corners = new GeoPoint3[xy.Length / 2];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = At(xy[2 * i], xy[2 * i + 1], 0);
            }

            return new GeoPolygon3(corners, Fine);
        }

        private static void AssertMeshedToItsArea(GeoFace3 face, Tolerance tolerance, double rounding)
        {
            GeoTriangle3[] surface = face.TriangulateSurface(tolerance);

            Assert.Equal(face.Area, surface.Sum(t => t.Area), rounding);
            Assert.All(surface, t => Assert.True(t.GetAreaVector().DotProduct(face.Normal) > 0.0));
            Assert.All(surface, t =>
                Assert.NotEqual(PointLocation.OutSide, Containment3.Locate(face.Boundary, t.Centroid, Fine))
            );
        }

        [Theory]
        [InlineData(1E-2)]
        [InlineData(1E-3)]
        public void AWallWithCornersCloserThanTheToleranceUpItsSides_IsMeshedToItsArea(double tolerance)
        {
            // The wall of the GR2c plate's deeper opening, 50 by 1.50015, with the corners the shallower one's ceiling left at
            // 1.5 up both its sides. Read within the tolerance, the corners 0.00015 apart were merged, the upper one kept on
            // one side and the lower one on the other, and the triangles came to 0.00375 less than the wall.
            var wall = new GeoFace3(
                new GeoPolygon3(
                    new[]
                    {
                        At(75, 15, 1.50015),
                        At(125, 15, 1.50015),
                        At(125, 15, 1.5),
                        At(125, 15, 0),
                        At(75, 15, 0),
                        At(75, 15, 1.5),
                    },
                    Millionth
                ),
                new GeoPolygon3[0],
                Millionth
            );

            AssertMeshedToItsArea(wall, Within(tolerance), 1E-12);
        }

        [Theory]
        [InlineData(1E-2)]
        [InlineData(1E-3)]
        public void AFaceWithAStepAndANotchNarrowerThanTheTolerance_IsMeshedToItsArea(double tolerance)
        {
            // A plate 100 by 30 on a tilted plane 41 250 out, its top stepping up 0.0005 at 60 along and a notch 0.0002 wide
            // cut 10 into it from the bottom at 20 along: 3 000.028. The step was merged away and the notch closed, 0.013 short.
            var frame = new GeoCoordinateSystem3(
                new GeoPlane3(At(41250.5, -18300.25, 7350.0), new GeoVector3(0.2, -0.3, 0.93).Normalize())
            );
            double[] uv =
            {
                0,
                0,
                20,
                0,
                20,
                10,
                20.0002,
                10,
                20.0002,
                0,
                100,
                0,
                100,
                30,
                60,
                30,
                60,
                30.0005,
                0,
                30.0005,
            };
            var corners = new GeoPoint3[uv.Length / 2];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = frame.ToGlobal(At(uv[2 * i], uv[2 * i + 1], 0));
            }

            var face = new GeoFace3(new GeoPolygon3(corners, Fine), new GeoPolygon3[0], Fine);

            Assert.Equal(100 * 30 + 60 * 0.0005 - 0.0002 * 10, face.Area, 1E-9);
            AssertMeshedToItsArea(face, Within(tolerance), 1E-9);
        }

        [Fact]
        public void AFaceThinnerThanTheTolerance_IsStillASeamWithNoTriangle()
        {
            // A face 40 by 0.0004 is a seam at a thousandth, as the side of a thin slab is, and has no triangle; at a
            // ten-thousandth it is its two.
            var sliver = new GeoFace3(Flat(0, 0, 40, 0, 40, 0.0004, 0, 0.0004), new GeoPolygon3[0], Fine);

            Assert.Empty(sliver.TriangulateSurface(Thousandth));
            AssertMeshedToItsArea(sliver, new Tolerance(1E-4, 1E-5, 0.017453292519943295, 1E-4), 1E-15);
        }

        [Fact]
        public void ANeedleBetweenTheEdgeAndAHoleJustOverTheToleranceAbove_IsMeshedToItsArea()
        {
            // A plate 81.43 by 17.44 with a hole 5.09 by 1.94 standing 0.00000162 above its bottom, which has a corner
            // 0.0000010096 past another along it, at a millionth: the clipping was left with the needle between the bottom
            // and the hole's corner, every ear of it blocked by corners within rounding of its sides, and dropped it as
            // nothing, 0.000066 short.
            var face = new GeoFace3(
                Flat(0, 0, 49.178, 0, 49.1780010096, 0, 81.43, 0, 81.43, 17.44, 0, 17.44),
                new[] { Flat(29.75, 1.62E-6, 29.75, 1.94, 34.84, 1.94, 34.84, 1.62E-6) },
                Fine
            );

            Assert.Equal(81.43 * 17.44 - 5.09 * (1.94 - 1.62E-6), face.Area, 1E-10);
            AssertMeshedToItsArea(face, Millionth, 1E-10);
        }

        [Theory]
        [InlineData(1E-2)]
        [InlineData(1E-3)]
        public void ThePlateWithCeilingsApart_ReadAsTheTrianglesInItsFaces_HoldsWhatItHolds(double tolerance)
        {
            // The GR2c plate, 200 by 100 by 3 with openings 50 by 70 up from its floor 1.5 and 1.50015 deep: read through the
            // triangles lying in its faces it held 0.175 too much, read through fans what it holds.
            GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
                GeoSolid3.Extrude(
                    new GeoPolygon3(At(x0, y0, z0), At(x1, y0, z0), At(x1, y1, z0), At(x0, y1, z0)),
                    new GeoVector3(0, 0, z1 - z0),
                    Thousandth
                );

            GeoSolid3 plate = Box(0, 0, 0, 200, 100, 3)
                .WithOpenings(new[] { Box(10, 15, -7, 60, 85, 1.5), Box(75, 15, -7, 125, 85, 1.50015) });
            const double held = 60_000 - 50 * 70 * 1.5 - 50 * 70 * 1.50015;

            Assert.Equal(held, Measure3.Volume(plate, VolumeMethod.Surface, Within(tolerance)), 1E-9);
            Assert.Equal(held, plate.GetMassProperties(7.85E-6, Within(tolerance)).Volume, 1E-9);
        }
    }
}
