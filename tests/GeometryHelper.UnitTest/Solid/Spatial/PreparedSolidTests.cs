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

        /// <summary>
        /// A point well inside a turned block is inside the prepared block too. A ray from it leaves the block near an
        /// edge at a shallow angle to the face beyond, and that face, which holds a point up to the tolerance past its
        /// rim, took the ray a little further on as well: two crossings apart along the ray, read as two faces of the
        /// body, and the point came out outside. They are told apart by where they land on their triangles.
        /// </summary>
        [Fact]
        public void APointInsideATurnedBlockIsInsideThePreparedBlockToo()
        {
            var block = new GeoSolid3(new[]
            {
                Face(-18.613655394218284, -45.939572033089149, 4.9079073220571363, -26.579187396095953, -26.741965246386197, -4.9410618417792254, 34.040098640504972, -3.6195412815219496, -8.8977865481281544, 42.005630642382641, -22.8171480682249, 0.95118261570820728),
                Face(-24.298775227431165, -22.395040804968172, 55.398841773006296, 36.320510809169761, 0.72738315989607472, 51.442117066657374, 28.354978807292092, 19.924989946599027, 41.593147902821009, -32.264307229308834, -3.1974340182652234, 45.549872609169938),
                Face(-18.613655394218284, -45.939572033089149, 4.9079073220571363, 42.005630642382641, -22.8171480682249, 0.95118261570820728, 36.320510809169761, 0.72738315989607472, 51.442117066657374, -24.298775227431165, -22.395040804968172, 55.398841773006296),
                Face(34.040098640504972, -3.6195412815219496, -8.8977865481281544, -26.579187396095953, -26.741965246386197, -4.9410618417792254, -32.264307229308834, -3.1974340182652234, 45.549872609169938, 28.354978807292092, 19.924989946599027, 41.593147902821009),
                Face(42.005630642382641, -22.8171480682249, 0.95118261570820728, 34.040098640504972, -3.6195412815219496, -8.8977865481281544, 28.354978807292092, 19.924989946599027, 41.593147902821009, 36.320510809169761, 0.72738315989607472, 51.442117066657374),
                Face(-26.579187396095953, -26.741965246386197, -4.9410618417792254, -18.613655394218284, -45.939572033089149, 4.9079073220571363, -24.298775227431165, -22.395040804968172, 55.398841773006296, -32.264307229308834, -3.1974340182652234, 45.549872609169938)
            });
            // A millimetre and a third inside.
            var point = new GeoPoint3(6.1005820907312369, -5.773486415592167, 12.704989168937304);

            Assert.Equal(PointLocation.Inside, block.Locate(point));
            Assert.Equal(PointLocation.Inside, block.Prepare().Locate(point));
        }

        private static GeoFace3 Face(params double[] xyz)
        {
            var corners = new List<GeoPoint3>();

            for (int i = 0; i < xyz.Length; i += 3)
            {
                corners.Add(new GeoPoint3(xyz[i], xyz[i + 1], xyz[i + 2]));
            }

            return new GeoFace3(new GeoPolygon3(corners));
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

                    // Off the surface the magnitude is the reach to the nearest point of it. On it, within the
                    // tolerance, the sign is nought and that point is no further than the tolerance.
                    double reach = prepared.GetClosestPointOnBoundary(point).DistanceTo(point);
                    if (body.Locate(point) == PointLocation.OnSide)
                    {
                        Assert.True(reach <= Tolerance.Global.EqualPoint, $"{point} is on the surface and {reach} from it");
                    }
                    else
                    {
                        Assert.Equal(Math.Abs(body.SignedDistanceTo(point)), reach, 9);
                    }
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
