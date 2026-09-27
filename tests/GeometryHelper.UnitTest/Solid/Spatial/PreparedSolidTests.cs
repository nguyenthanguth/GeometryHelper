using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A prepared body answers as the body does: the openings cut once, the surface indexed once, the answers
    /// the same.
    /// </summary>
    public class PreparedSolidTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>A plate with a square hole, a round-ish hole and a notch cut from its edge.</summary>
        private static GeoSolid3 Plate()
            => Box(0, 0, 0, 200, 100, 20).WithOpenings(new[]
            {
                Box(40, 40, -1, 60, 60, 21),
                Box(120, 30, -1, 150, 70, 21).TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(135, 50, 10), GeoVector3.ZAxis, 0.4)),
                Box(180, -5, -1, 205, 30, 21),
            });

        private static IEnumerable<GeoSolid3> Bodies()
        {
            yield return Plate();
            yield return Box(0, 0, 0, 60, 40, 30).TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(30, 20, 15), new GeoVector3(1, 2, 3), 0.7));

            Assert.True(Box(0, 0, 0, 100, 30, 30).TryUnion(Box(0, 0, 0, 30, 100, 30), out GeoSolid3 ell));
            yield return ell;
        }

        [Fact]
        public void APinThroughABoltHoleIsClearOfThePreparedPlate()
        {
            GeoPreparedSolid3 plate = Box(0, 0, 0, 100, 100, 20)
                .WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) })
                .Prepare();
            GeoPreparedSolid3 pin = Box(45, 45, -50, 55, 55, 50).Prepare();

            Assert.False(plate.CollidesWith(pin));
            Assert.Equal(5.0, plate.DistanceTo(pin), 9);
            Assert.Equal(PointLocation.OutSide, plate.Locate(new GeoPoint3(50, 50, 20)));
            Assert.Empty(plate.GetIntersections(new GeoRay3(new GeoPoint3(50, 50, 100), new GeoVector3(0, 0, -1))));
            Assert.Equal(10, plate.Material.Faces.Count);
        }

        [Fact]
        public void PointsAreAnsweredAsTheBodyAnswersThem()
        {
            var rng = new Random(31);

            foreach (GeoSolid3 body in Bodies())
            {
                GeoPreparedSolid3 prepared = body.Prepare();
                GeoAabb3 box = body.GetAabb();

                for (int k = 0; k < 300; k++)
                {
                    var point = new GeoPoint3(
                        box.Min.X - 10 + rng.NextDouble() * (box.Max.X - box.Min.X + 20),
                        box.Min.Y - 10 + rng.NextDouble() * (box.Max.Y - box.Min.Y + 20),
                        box.Min.Z - 10 + rng.NextDouble() * (box.Max.Z - box.Min.Z + 20));

                    Assert.Equal(body.Locate(point), prepared.Locate(point));
                    Assert.Equal(body.DistanceTo(point), prepared.DistanceTo(point), 9);
                    Assert.Equal(body.SignedDistanceTo(point), prepared.SignedDistanceTo(point), 9);
                    Assert.Equal(Math.Abs(body.SignedDistanceTo(point)), prepared.GetClosestPointOnBoundary(point).DistanceTo(point), 9);
                }
            }
        }

        [Fact]
        public void RaysAreAnsweredAsTheBodyAnswersThem()
        {
            var rng = new Random(37);

            foreach (GeoSolid3 body in Bodies())
            {
                GeoPreparedSolid3 prepared = body.Prepare();
                GeoAabb3 box = body.GetAabb();

                for (int k = 0; k < 200; k++)
                {
                    var origin = new GeoPoint3(
                        box.Min.X - 50 + rng.NextDouble() * (box.Max.X - box.Min.X + 100),
                        box.Min.Y - 50 + rng.NextDouble() * (box.Max.Y - box.Min.Y + 100),
                        box.Min.Z - 50 + rng.NextDouble() * (box.Max.Z - box.Min.Z + 100));
                    var ray = new GeoRay3(origin, new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5));

                    GeoPoint3[] expected = body.GetIntersections(ray);
                    GeoPoint3[] actual = prepared.GetIntersections(ray);

                    Assert.Equal(expected.Length, actual.Length);

                    for (int i = 0; i < expected.Length; i++)
                    {
                        Assert.True(expected[i].DistanceTo(actual[i]) < 1E-6);
                    }

                    Assert.Equal(body.CollidesWith(ray), prepared.CollidesWith(ray));
                }
            }
        }

        [Fact]
        public void PairsOfBodiesAreAnsweredAsTheBodiesAnswerThem()
        {
            var rng = new Random(41);
            GeoSolid3 plate = Plate();
            GeoPreparedSolid3 prepared = plate.Prepare();

            for (int k = 0; k < 60; k++)
            {
                double x = rng.NextDouble() * 220 - 10, y = rng.NextDouble() * 120 - 10, z = rng.NextDouble() * 60 - 20;
                GeoSolid3 probe = Box(x, y, z, x + 5 + rng.NextDouble() * 30, y + 5 + rng.NextDouble() * 30, z + 5 + rng.NextDouble() * 30);
                GeoPreparedSolid3 preparedProbe = probe.Prepare();

                bool collides = plate.CollidesWith(probe);

                Assert.Equal(collides, prepared.CollidesWith(preparedProbe));
                Assert.Equal(collides, prepared.CollidesWith(probe));
                Assert.Equal(plate.DistanceTo(probe), prepared.DistanceTo(preparedProbe), 6);
                Assert.Equal(plate.DistanceTo(probe), prepared.DistanceTo(probe), 6);
                Assert.Equal(plate.Intersect(probe).Sum(piece => piece.Volume), prepared.Intersect(preparedProbe).Sum(piece => piece.Volume), 6);
            }
        }

        [Fact]
        public void OneBodyCanBeAskedFromManyThreadsAtOnce()
        {
            GeoSolid3 plate = Plate();
            GeoPreparedSolid3 prepared = plate.Prepare();
            var points = Enumerable.Range(0, 400).Select(i => new GeoPoint3(i % 20 * 11.0 - 5, i / 20 * 6.0 - 5, (i * 7 % 30) - 5)).ToArray();

            PointLocation[] alone = points.Select(p => prepared.Locate(p)).ToArray();
            var together = new PointLocation[points.Length];

            Parallel.For(0, points.Length, i => together[i] = prepared.Locate(points[i]));

            Assert.Equal(alone, together);
        }

        [Fact]
        public void OpeningsThatTakeAllTheMaterialLeaveTheGrossBody()
        {
            // As every query of the body does, rather than answering for nothing at all.
            GeoSolid3 swallowed = Box(0, 0, 0, 10, 10, 10).WithOpenings(new[] { Box(-1, -1, -1, 11, 11, 11) });
            GeoPreparedSolid3 prepared = swallowed.Prepare();

            Assert.Equal(1000.0, prepared.Material.Volume, 9);
            Assert.Equal(swallowed.DistanceTo(new GeoPoint3(20, 5, 5)), prepared.DistanceTo(new GeoPoint3(20, 5, 5)), 9);
        }
    }
}
