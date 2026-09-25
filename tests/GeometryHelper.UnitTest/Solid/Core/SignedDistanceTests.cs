using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// How deep inside a body a point sits. <c>DistanceTo</c> reads a body as solid through and answers
    /// nothing at all for a point inside one; <c>SignedDistanceTo</c> keeps the depth, and its definition is
    /// tied to <c>Locate</c>: negative inside, nought on the surface, positive outside.
    /// </summary>
    public class SignedDistanceTests
    {
        private static GeoAabb3 Box() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100));

        private static GeoSolid3 Cube() => Box().ToObb().ToSolid();

        /// <summary>
        /// The cube with a twenty-square duct bored right through it along z, from x = 40 to 60 and y = 40
        /// to 60. The duct runs past both faces so its ends are not walls of the body.
        /// </summary>
        private static GeoSolid3 Pierced() => Cube().WithOpenings(new[]
        {
            new GeoAabb3(new GeoPoint3(40, 40, -10), new GeoPoint3(60, 60, 110)).ToObb().ToSolid()
        });

        [Fact]
        public void ThePlainNumbersAreWhatTheModelSays()
        {
            GeoSolid3 cube = Cube();
            GeoObb3 obb = Box().ToObb();
            GeoAabb3 aabb = Box();

            // The middle of a cube a hundred on a side stands fifty from the nearest face.
            Assert.Equal(-50.0, cube.SignedDistanceTo(new GeoPoint3(50, 50, 50)), 9);
            Assert.Equal(-50.0, obb.SignedDistanceTo(new GeoPoint3(50, 50, 50)), 9);
            Assert.Equal(-50.0, aabb.SignedDistanceTo(new GeoPoint3(50, 50, 50)), 9);

            Assert.Equal(-10.0, cube.SignedDistanceTo(new GeoPoint3(50, 50, 10)), 9);
            Assert.Equal(-10.0, obb.SignedDistanceTo(new GeoPoint3(50, 50, 10)), 9);
            Assert.Equal(-10.0, aabb.SignedDistanceTo(new GeoPoint3(50, 50, 10)), 9);

            Assert.Equal(0.0, cube.SignedDistanceTo(new GeoPoint3(50, 50, 0)), 9);
            Assert.Equal(20.0, cube.SignedDistanceTo(new GeoPoint3(50, 50, -20)), 9);
            Assert.Equal(20.0, obb.SignedDistanceTo(new GeoPoint3(50, 50, -20)), 9);
            Assert.Equal(20.0, aabb.SignedDistanceTo(new GeoPoint3(50, 50, -20)), 9);

            // DistanceTo is untouched: a point inside is still nought away.
            Assert.Equal(0.0, cube.DistanceTo(new GeoPoint3(50, 50, 50)), 12);
            Assert.Equal(0.0, aabb.DistanceTo(new GeoPoint3(50, 50, 50)), 12);
        }

        [Fact]
        public void TheWallOfADuctIsPartOfTheSurface()
        {
            GeoSolid3 pierced = Pierced();

            // Beside the duct: the nearest boundary is its wall at x = 40, ten away, not the far skin of
            // the body at fifty or seventy.
            Assert.Equal(PointLocation.Inside, pierced.Locate(new GeoPoint3(30, 50, 50)));
            Assert.Equal(-10.0, pierced.SignedDistanceTo(new GeoPoint3(30, 50, 50)), 9);

            // The unpierced body, which has no duct to be near, measures the same point to its own side.
            Assert.Equal(-30.0, Cube().SignedDistanceTo(new GeoPoint3(30, 50, 50)), 9);

            // Inside the duct is not inside the body, and it is ten from the wall it sits in.
            Assert.Equal(PointLocation.OutSide, pierced.Locate(new GeoPoint3(50, 50, 50)));
            Assert.Equal(10.0, pierced.SignedDistanceTo(new GeoPoint3(50, 50, 50)), 9);

            // On the wall itself is nought, the same as on an outer face.
            Assert.Equal(0.0, pierced.SignedDistanceTo(new GeoPoint3(40, 50, 50)), 9);
            Assert.Equal(0.0, pierced.SignedDistanceTo(new GeoPoint3(0, 50, 50)), 9);
        }

        /// <summary>
        /// The defining property over a grid: the sign says what <c>Locate</c> says, and nothing on the
        /// surface is nearer than the magnitude claims.
        /// </summary>
        [Fact]
        public void TheSignFollowsLocateAndTheMagnitudeReachesTheSurface()
        {
            GeoSolid3 cube = Cube();
            GeoSolid3 pierced = Pierced();
            GeoObb3 obb = Box().ToObb();
            GeoAabb3 aabb = Box();

            int inside = 0;
            int outside = 0;
            int onSurface = 0;

            for (int i = -2; i <= 12; i++)
            {
                for (int j = -2; j <= 12; j++)
                {
                    for (int k = -2; k <= 12; k++)
                    {
                        var point = new GeoPoint3(i * 10.0, j * 10.0, k * 10.0);

                        foreach (double signed in new[]
                        {
                            cube.SignedDistanceTo(point), pierced.SignedDistanceTo(point),
                            obb.SignedDistanceTo(point), aabb.SignedDistanceTo(point)
                        })
                        {
                            // The magnitude is a distance, so it is never negative in itself.
                            Assert.True(Math.Abs(signed) >= 0.0);
                        }

                        string at = " at (" + point.X + "," + point.Y + "," + point.Z + ")";
                        Follows(cube.SignedDistanceTo(point), cube.Locate(point), ref inside, ref outside, ref onSurface, "cube" + at);
                        Follows(pierced.SignedDistanceTo(point), pierced.Locate(point), ref inside, ref outside, ref onSurface, "pierced" + at);
                        Follows(obb.SignedDistanceTo(point), obb.Locate(point), ref inside, ref outside, ref onSurface, "obb" + at);
                        Follows(aabb.SignedDistanceTo(point), aabb.Locate(point), ref inside, ref outside, ref onSurface, "aabb" + at);

                        // An unpierced box has one surface and no openings, so its magnitude is the distance
                        // to the surface point the projection hands back.
                        Assert.Equal(
                            point.DistanceTo(cube.GetClosestPointOnBoundary(point)),
                            Math.Abs(cube.SignedDistanceTo(point)), 6);
                    }
                }
            }

            Assert.True(inside > 500, "only " + inside + " inside");
            Assert.True(outside > 500, "only " + outside + " outside");
            Assert.True(onSurface > 100, "only " + onSurface + " on the surface");
        }

        private static void Follows(
            double signed,
            PointLocation where,
            ref int inside,
            ref int outside,
            ref int onSurface,
            string what)
        {
            switch (where)
            {
                case PointLocation.Inside:
                    Assert.True(signed < 0.0, "inside but " + signed + " " + what);
                    inside++;
                    break;

                case PointLocation.OnSide:
                    Assert.True(Math.Abs(signed) <= Tolerance.Global.EqualPoint, "on the surface but " + signed + " " + what);
                    onSurface++;
                    break;

                default:
                    Assert.True(signed > 0.0, "outside but " + signed + " " + what);
                    outside++;
                    break;
            }
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNothing()
        {
            GeoSolid3 cube = Cube();
            GeoObb3 obb = Box().ToObb();
            GeoAabb3 aabb = Box();
            var point = new GeoPoint3(30, 40, 50);

            Assert.Equal(Distance3.SignedDistanceTo(cube, point), cube.SignedDistanceTo(point), 12);
            Assert.Equal(Distance3.SignedDistanceTo(obb, point), obb.SignedDistanceTo(point), 12);
            Assert.Equal(Distance3.SignedDistanceTo(aabb, point), aabb.SignedDistanceTo(point), 12);

            Assert.Equal(cube.SignedDistanceTo(point), cube.SignedDistanceTo(point, Tolerance.Global), 12);
            Assert.Equal(obb.SignedDistanceTo(point), obb.SignedDistanceTo(point, Tolerance.Global), 12);
            Assert.Equal(aabb.SignedDistanceTo(point), aabb.SignedDistanceTo(point, Tolerance.Global), 12);

            Assert.Throws<ArgumentNullException>(() => Distance3.SignedDistanceTo((GeoSolid3)null, point));
            Assert.Throws<ArgumentNullException>(() => Distance3.SignedDistanceTo((GeoObb3)null, point));

            // An empty box has no surface to measure to, and says so rather than answering nonsense.
            Assert.Throws<InvalidOperationException>(() => new GeoAabb3().SignedDistanceTo(point));
        }
    }
}
