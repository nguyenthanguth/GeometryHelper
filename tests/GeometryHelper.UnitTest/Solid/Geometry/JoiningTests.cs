using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The shortest segment joining two shapes in space. Core worked out nine pairs of these and only
    /// GeoLine3 and GeoSolid3 could be asked for one, so eleven directions were unreachable. A segment
    /// always leaves the shape it was asked of and lands on the other, which is the part worth pinning:
    /// a direction Core reads the other way round has to be turned over, and one that is handed straight
    /// back points the wrong way while still having the right length.
    /// </summary>
    public class JoiningTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoSolid3 Cube() =>
            new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100)).ToObb().ToSolid();

        /// <summary>
        /// A triangle standing in the plane x = 200, clear of the cube in all three directions so that
        /// exactly one of its corners is the nearest point of it to anything below and behind.
        /// </summary>
        private static GeoTriangle3 Standing() => new GeoTriangle3(
            new GeoPoint3(200, 150, 150), new GeoPoint3(200, 250, 150), new GeoPoint3(200, 150, 250));

        /// <summary>
        /// The corner of that triangle nearest the cube, the segment and the ray used below.
        /// </summary>
        private static GeoPoint3 Corner() => new GeoPoint3(200, 150, 150);

        private static void Joins(GeoLine3 join, GeoPoint3 from, GeoPoint3 to)
        {
            Assert.True(join.StartPoint.IsEqualTo(from, Loose), $"leaves {join.StartPoint}, wanted {from}");
            Assert.True(join.EndPoint.IsEqualTo(to, Loose), $"lands on {join.EndPoint}, wanted {to}");
        }

        [Fact]
        public void APointCanBeAskedForItsReachToASolid()
        {
            GeoSolid3 cube = Cube();
            var point = new GeoPoint3(200, 50, 50);

            Joins(point.GetShortestLineTo(cube), point, new GeoPoint3(100, 50, 50));
            Assert.Equal(100.0, point.GetShortestLineTo(cube).Length, 6);

            // The solid has always been able to answer, and it answers the same the other way about.
            Joins(cube.GetShortestLineTo(point), new GeoPoint3(100, 50, 50), point);
        }

        [Fact]
        public void ASegmentReachesRaysTrianglesAndSolids()
        {
            GeoSolid3 cube = Cube();
            GeoTriangle3 standing = Standing();

            var inside = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));
            var outside = new GeoLine3(new GeoPoint3(200, 50, 50), new GeoPoint3(300, 50, 50));
            var ray = new GeoRay3(new GeoPoint3(200, 50, 50), new GeoVector3(1, 0, 0));

            Joins(outside.GetShortestLineTo(cube), new GeoPoint3(200, 50, 50), new GeoPoint3(100, 50, 50));
            Assert.Equal(100.0, outside.GetShortestLineTo(cube).Length, 6);

            Joins(inside.GetShortestLineTo(standing), new GeoPoint3(50, 50, 50), Corner());
            Assert.Equal(Math.Sqrt(150 * 150 + 100 * 100 + 100 * 100), inside.GetShortestLineTo(standing).Length, 6);

            Joins(inside.GetShortestLineTo(ray), new GeoPoint3(50, 50, 50), new GeoPoint3(200, 50, 50));
            Assert.Equal(150.0, inside.GetShortestLineTo(ray).Length, 6);
        }

        [Fact]
        public void ARayReachesSegmentsTrianglesAndSolids()
        {
            GeoSolid3 cube = Cube();
            GeoTriangle3 standing = Standing();

            var away = new GeoRay3(new GeoPoint3(200, 50, 50), new GeoVector3(1, 0, 0));
            var inside = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));

            // A ray only goes one way, so nothing behind its origin is ever nearer than the origin itself.
            Joins(away.GetShortestLineTo(inside), new GeoPoint3(200, 50, 50), new GeoPoint3(50, 50, 50));
            Joins(away.GetShortestLineTo(cube), new GeoPoint3(200, 50, 50), new GeoPoint3(100, 50, 50));

            var below = new GeoRay3(new GeoPoint3(200, 50, -50), new GeoVector3(0, 0, -1));

            Joins(below.GetShortestLineTo(standing), new GeoPoint3(200, 50, -50), Corner());
            Assert.Equal(Math.Sqrt(100 * 100 + 200 * 200), below.GetShortestLineTo(standing).Length, 6);
        }

        [Fact]
        public void ATriangleReachesSegmentsRaysSolidsAndOtherTriangles()
        {
            GeoSolid3 cube = Cube();
            GeoTriangle3 standing = Standing();

            var inside = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));
            var below = new GeoRay3(new GeoPoint3(200, 50, -50), new GeoVector3(0, 0, -1));

            // The cube's far top corner is the nearest of it to the triangle, and the triangle's low corner
            // the nearest of it to everything else here.
            Joins(standing.GetShortestLineTo(cube), Corner(), new GeoPoint3(100, 100, 100));
            Assert.Equal(Math.Sqrt(100 * 100 + 50 * 50 + 50 * 50), standing.GetShortestLineTo(cube).Length, 6);

            Joins(standing.GetShortestLineTo(inside), Corner(), new GeoPoint3(50, 50, 50));
            Joins(standing.GetShortestLineTo(below), Corner(), new GeoPoint3(200, 50, -50));

            var flat = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
            GeoTriangle3 over = new GeoTriangle3(
                new GeoPoint3(0, 0, 200), new GeoPoint3(100, 0, 200), new GeoPoint3(0, 100, 200));

            GeoLine3 between = flat.GetShortestLineTo(over);

            Assert.Equal(200.0, between.Length, 6);
            Assert.Equal(0.0, between.StartPoint.Z, 6);
            Assert.Equal(200.0, between.EndPoint.Z, 6);
        }

        [Fact]
        public void EveryDirectionRunsTheOppositeWayToItsOppositeNumber()
        {
            GeoSolid3 cube = Cube();
            GeoTriangle3 standing = Standing();

            var point = new GeoPoint3(200, 50, 50);
            var line = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));
            var ray = new GeoRay3(new GeoPoint3(200, 50, -50), new GeoVector3(0, 0, -1));

            var pairs = new[]
            {
                (point.GetShortestLineTo(cube), cube.GetShortestLineTo(point)),
                (line.GetShortestLineTo(cube), cube.GetShortestLineTo(line)),
                (standing.GetShortestLineTo(cube), cube.GetShortestLineTo(standing)),
                (ray.GetShortestLineTo(cube), cube.GetShortestLineTo(ray)),
                (line.GetShortestLineTo(standing), standing.GetShortestLineTo(line)),
                (ray.GetShortestLineTo(standing), standing.GetShortestLineTo(ray)),
            };

            foreach (var pair in pairs)
            {
                Assert.True(pair.Item1.StartPoint.IsEqualTo(pair.Item2.EndPoint, Loose));
                Assert.True(pair.Item1.EndPoint.IsEqualTo(pair.Item2.StartPoint, Loose));
                Assert.Equal(pair.Item1.Length, pair.Item2.Length, 6);
            }
        }

        [Fact]
        public void ShapesThatTouchAreJoinedByNothing()
        {
            GeoSolid3 cube = Cube();

            // A ray fired into the cube reaches it, so there is no gap left to measure.
            var into = new GeoRay3(new GeoPoint3(200, 50, 50), new GeoVector3(-1, 0, 0));

            Assert.Equal(0.0, into.GetShortestLineTo(cube).Length, 6);

            var piercing = new GeoTriangle3(
                new GeoPoint3(50, 50, 50), new GeoPoint3(300, 50, 50), new GeoPoint3(300, 150, 50));

            Assert.Equal(0.0, piercing.GetShortestLineTo(cube).Length, 6);
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndAnswersTheSameWithTheDefaultOne()
        {
            Tolerance global = Tolerance.Global;
            GeoSolid3 cube = Cube();
            GeoTriangle3 standing = Standing();

            var point = new GeoPoint3(200, 50, 50);
            var line = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));
            var ray = new GeoRay3(new GeoPoint3(200, 50, -50), new GeoVector3(0, 0, -1));

            var pairs = new[]
            {
                (point.GetShortestLineTo(cube), point.GetShortestLineTo(cube, global)),
                (line.GetShortestLineTo(cube), line.GetShortestLineTo(cube, global)),
                (line.GetShortestLineTo(standing), line.GetShortestLineTo(standing, global)),
                (line.GetShortestLineTo(ray), line.GetShortestLineTo(ray, global)),
                (ray.GetShortestLineTo(line), ray.GetShortestLineTo(line, global)),
                (ray.GetShortestLineTo(standing), ray.GetShortestLineTo(standing, global)),
                (ray.GetShortestLineTo(cube), ray.GetShortestLineTo(cube, global)),
                (standing.GetShortestLineTo(line), standing.GetShortestLineTo(line, global)),
                (standing.GetShortestLineTo(ray), standing.GetShortestLineTo(ray, global)),
                (standing.GetShortestLineTo(cube), standing.GetShortestLineTo(cube, global)),
                (standing.GetShortestLineTo(standing), standing.GetShortestLineTo(standing, global)),
            };

            foreach (var pair in pairs)
            {
                Assert.True(pair.Item1.IsEqualTo(pair.Item2, Loose));
            }
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            var point = new GeoPoint3(200, 50, 50);
            var line = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));

            Assert.Throws<ArgumentNullException>(() => point.GetShortestLineTo(null));
            Assert.Throws<ArgumentNullException>(() => line.GetShortestLineTo((GeoSolid3)null));
        }
    }
}
