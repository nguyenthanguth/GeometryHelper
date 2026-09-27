using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two parts parted by a face of one of them share no volume, and the clash check now takes that from their
    /// corners rather than from the boolean. Where the corners say so, the boolean must find nothing either.
    /// </summary>
    public class PartingFaceTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        private static GeoSolid3 Block(double x, double y, double z) => new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(x, y, z)).ToObb().ToSolid();

        /// <summary>
        /// A block, and a second one set against its +X face — or off it, or into it, by <paramref name="gap"/> —
        /// slid along the face and, now and then, tipped so that only an edge meets the face; the pair turned any
        /// way and put anywhere.
        /// </summary>
        private static (GeoSolid3, GeoSolid3, bool) Against(Random rng, double gap)
        {
            double ax = rng.Next(20, 300), ay = rng.Next(20, 300), az = rng.Next(20, 300);
            double bx = rng.Next(20, 300), by = rng.Next(20, 300), bz = rng.Next(20, 300);

            GeoSolid3 first = Block(ax, ay, az);
            GeoSolid3 second = Block(bx, by, bz).TransformBy(GeoTransform3.Translation(new GeoVector3(ax + gap, rng.Next(-(int)by + 5, (int)ay - 5), rng.Next(-(int)bz + 5, (int)az - 5))));

            bool tipped = rng.NextDouble() < 0.3;

            if (tipped)
            {
                // Tipped about an edge lying in the face: only that edge still touches it, and where the edge lies
                // beyond the first block the rest swings clear of it, set in or not.
                GeoAabb3 box = second.GetAabb();
                second = second.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(box.Min.X, box.Min.Y, box.Min.Z), GeoVector3.ZAxis, -0.2 * rng.NextDouble()));
            }

            GeoTransform3 anywhere = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5), rng.NextDouble() * 6)
                .Multiply(GeoTransform3.Translation(new GeoVector3(rng.Next(-5000, 5000), rng.Next(-5000, 5000), rng.Next(-5000, 5000))));

            return (first.TransformBy(anywhere), second.TransformBy(anywhere), tipped);
        }

        [Fact]
        public void WherePartedTheBooleanFindsNoSharedVolumeEither()
        {
            var rng = new Random(8);
            double[] gaps = { 0.0, 0.0, 0.0, 1E-9, -1E-9, 1E-7, -1E-7, -1E-6, -1E-5, -1E-4, -1E-3, -0.5, 1E-3 };
            int parted = 0, notParted = 0;

            for (int k = 0; k < 260; k++)
            {
                double gap = gaps[k % gaps.Length];
                (GeoSolid3 a, GeoSolid3 b, bool tipped) = Against(rng, gap);
                GeoPreparedSolid3 pa = a.Prepare(), pb = b.Prepare();

                bool isParted = pa.IsPartedFrom(pb, Tol);
                Assert.Equal(isParted, pb.IsPartedFrom(pa, Tol));

                if (isParted)
                {
                    parted++;
                    Assert.True(pa.Intersect(pb).Length == 0, $"parted at {gap}, yet the boolean finds shared volume");
                }
                else
                {
                    notParted++;
                }

                // Lying against each other, or apart, as the corners say to the last bit: always parted.
                if (Math.Abs(gap) <= 1E-9 || gap > 0)
                {
                    Assert.True(isParted, $"not parted at {gap}");
                }

                // Square on and in by more than rounding: the boolean decides.
                if (gap <= -1E-6 && !tipped)
                {
                    Assert.False(isParted, $"parted at {gap}");
                }
            }

            Assert.True(parted > 100 && notParted > 80, $"{parted} parted, {notParted} not; the test needs both");
        }

        /// <summary>
        /// Parts of other shapes stood on the top face of a block or of an L — a round bar at any angle, another
        /// L, a block — set on it, just off it or into it. The L has faces whose planes cut through the L itself,
        /// which must never be taken to part it from anything.
        /// </summary>
        [Fact]
        public void AnythingPartedHasNoSharedVolumeWhateverTheShapes()
        {
            var rng = new Random(9);
            double[] gaps = { 0.0, 0.0, 1E-9, -1E-9, -1E-7, -1E-5, -1E-3, -1.0, 2.0 };
            int parted = 0, colliding = 0;
            var l = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(80, 0), new GeoPoint2(80, 20), new GeoPoint2(20, 20), new GeoPoint2(20, 80), new GeoPoint2(0, 80));

            for (int k = 0; k < 240; k++)
            {
                double gap = gaps[k % gaps.Length];
                double height = rng.Next(10, 60);

                GeoSolid3 below = rng.NextDouble() < 0.5
                    ? Block(80, 80, height)
                    : GeoSolid3.Extrude(l, new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), height);

                GeoSolid3 above;
                switch (rng.Next(3))
                {
                    case 0:
                        above = GeoSolid3.Cylinder(GeoPoint3.Origin, new GeoPoint3(rng.Next(-60, 61), rng.Next(-60, 61), rng.Next(10, 80)), rng.Next(3, 15), 12);
                        break;
                    case 1:
                        above = GeoSolid3.Extrude(l, new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), rng.Next(10, 60))
                            .TransformBy(GeoTransform3.RotationAxis(GeoPoint3.Origin, GeoVector3.ZAxis, rng.NextDouble() * 6));
                        break;
                    default:
                        above = Block(rng.Next(10, 60), rng.Next(10, 60), rng.Next(10, 60))
                            .TransformBy(GeoTransform3.RotationAxis(GeoPoint3.Origin, GeoVector3.ZAxis, rng.NextDouble() * 6));
                        break;
                }

                // Its lowest point set on the top face, somewhere over the part below, then off it or into it.
                GeoAabb3 box = above.GetAabb();
                var lift = new GeoVector3(rng.Next(5, 60) - (box.Min.X + box.Max.X) / 2, rng.Next(5, 60) - (box.Min.Y + box.Max.Y) / 2, height - box.Min.Z + gap);
                above = above.TransformBy(GeoTransform3.Translation(lift));

                GeoTransform3 anywhere = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5), rng.NextDouble() * 6);
                GeoPreparedSolid3 pa = below.TransformBy(anywhere).Prepare(), pb = above.TransformBy(anywhere).Prepare();

                if (!pa.CollidesWith(pb))
                {
                    continue;
                }

                colliding++;

                if (pa.IsPartedFrom(pb, Tol))
                {
                    parted++;
                    Assert.True(pa.Intersect(pb).Length == 0, $"parted at {gap}, yet the boolean finds shared volume");
                }
                else if (gap >= -1E-9)
                {
                    // Resting on the top face or clear of it: that face parts them.
                    Assert.Fail($"not parted at {gap}");
                }
            }

            Assert.True(parted > 60 && colliding - parted > 30, $"{parted} parted of {colliding} colliding; the test needs both");
        }

        [Fact]
        public void ABoltInAHoleItFillsIsNotPartedAndABeamOnAFlangeIs()
        {
            GeoSolid3 plate = Block(100, 100, 20).WithOpenings(new[] { GeoSolid3.Cylinder(new GeoPoint3(50, 50, -10), new GeoPoint3(50, 50, 30), 10, 16) });
            GeoSolid3 bolt = GeoSolid3.Cylinder(new GeoPoint3(50, 50, -30), new GeoPoint3(50, 50, 50), 10, 16);
            Assert.True(plate.Prepare().CollidesWith(bolt.Prepare()));
            Assert.False(plate.Prepare().IsPartedFrom(bolt.Prepare(), Tol));

            GeoSolid3 flange = Block(300, 20, 300);
            GeoSolid3 beam = Block(200, 400, 100).TransformBy(GeoTransform3.Translation(new GeoVector3(50, 20, 100)));
            Assert.True(flange.Prepare().IsPartedFrom(beam.Prepare(), Tol));
        }

        /// <summary>
        /// Blocks on a grid touch face to face and edge to edge; checking them by hand with the boolean must give
        /// what the check gives taking the corners' word for the pairs that are parted.
        /// </summary>
        [Fact]
        public void TheClashCheckStillSaysTouchWhereTheBooleanWould()
        {
            var rng = new Random(10);
            var parts = new List<GeoSolid3>();

            for (int k = 0; k < 90; k++)
            {
                double x = rng.Next(0, 12) * 10, y = rng.Next(0, 12) * 10, z = rng.Next(0, 4) * 10;
                GeoSolid3 block = new GeoAabb3(new GeoPoint3(x, y, z), new GeoPoint3(x + rng.Next(1, 4) * 10, y + rng.Next(1, 4) * 10, z + rng.Next(1, 4) * 10)).ToObb().ToSolid();
                parts.Add(block.TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(60, 60, 20), new GeoVector3(0.2, -0.4, 1), 0.7)));
            }

            GeoPreparedSolid3[] prepared = parts.Select(p => p.Prepare()).ToArray();
            ClashResult[] found = Clash3.Find(prepared);
            var byHand = new List<(int, int, ClashKind)>();
            int partedPairs = 0;

            for (int i = 0; i < prepared.Length; i++)
            {
                for (int j = i + 1; j < prepared.Length; j++)
                {
                    if (!prepared[i].CollidesWith(prepared[j]))
                    {
                        continue;
                    }

                    partedPairs += prepared[i].IsPartedFrom(prepared[j], Tol) ? 1 : 0;
                    byHand.Add((i, j, prepared[i].Intersect(prepared[j]).Length > 0 ? ClashKind.Hard : ClashKind.Touch));
                }
            }

            Assert.True(partedPairs > 30 && byHand.Count(p => p.Item3 == ClashKind.Hard) > 30, $"{partedPairs} parted, {byHand.Count} colliding");
            Assert.Equal(byHand, found.Select(r => (r.First, r.Second, r.Kind)));
        }
    }
}
