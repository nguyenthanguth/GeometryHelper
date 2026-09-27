using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Clash;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The region two convex bodies share is now clipped out directly. Wherever the clipping answers, it must be
    /// the region: measured against the general boolean worked at a tolerance too tight to round anything away,
    /// which is the nearest thing to the exact answer there is, and against counting random points.
    /// </summary>
    /// <remarks>
    /// Not against the general boolean at the ordinary tolerance: on some pairs that one is wrong outright — it
    /// found nothing shared by an octagonal prism and a block sharing eight thousand cubic units, and nearly twice
    /// the volume a twelve-sided bar and a block really share — while the clipping and the tight boolean agree
    /// with each other and with the count.
    /// </remarks>
    public class ConvexIntersectTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        private static readonly Tolerance Tight = new Tolerance(1E-10, 1E-10, Tolerance.DefaultEqualAngleRad, 1E-10);

        private static GeoSolid3 Block(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>
        /// A convex part of some kind near the middle of a small space, turned any way: a block, a round bar, or a
        /// prism on a convex outline.
        /// </summary>
        private static GeoSolid3 ConvexPart(Random rng)
        {
            var at = new GeoPoint3(rng.Next(-40, 41), rng.Next(-40, 41), rng.Next(-40, 41));
            GeoSolid3 part;

            switch (rng.Next(3))
            {
                case 0:
                    part = Block(at.X, at.Y, at.Z, at.X + rng.Next(5, 80), at.Y + rng.Next(5, 80), at.Z + rng.Next(5, 80));
                    break;
                case 1:
                    part = GeoSolid3.Cylinder(at, at.Add(new GeoVector3(rng.Next(-80, 81), rng.Next(-80, 81), rng.Next(20, 90))), rng.Next(3, 25), 6 + 2 * rng.Next(0, 8));
                    break;
                default:
                    // A convex outline: points on an ellipse, taken in order round it.
                    int corners = rng.Next(3, 9);
                    double a = rng.Next(10, 50), b = rng.Next(10, 50), start = rng.NextDouble();
                    var outline = new GeoPolygon2(Enumerable.Range(0, corners)
                        .Select(k => start + 2 * Math.PI * (k + 0.3 * rng.NextDouble()) / corners)
                        .Select(t => new GeoPoint2(a * Math.Cos(t), b * Math.Sin(t))));
                    part = GeoSolid3.Extrude(outline, new GeoCoordinateSystem3(at, GeoVector3.XAxis, GeoVector3.YAxis), rng.Next(5, 70));
                    break;
            }

            return part.TransformBy(GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5), rng.NextDouble() * 6));
        }

        [Fact]
        public void ConvexPartsAreKnownAndOthersAreNot()
        {
            var rng = new Random(20);
            for (int k = 0; k < 60; k++)
            {
                Assert.True(Boolean3.IsConvex(ConvexPart(rng), Tol));
            }

            var l = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(80, 0), new GeoPoint2(80, 20), new GeoPoint2(20, 20), new GeoPoint2(20, 80), new GeoPoint2(0, 80));
            GeoSolid3 block = Block(0, 0, 0, 100, 100, 20);

            Assert.False(Boolean3.IsConvex(GeoSolid3.Extrude(l, new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis), 30), Tol));
            Assert.False(Boolean3.IsConvex(block.WithOpenings(new[] { GeoSolid3.Cylinder(new GeoPoint3(50, 50, -5), new GeoPoint3(50, 50, 25), 5, 8) }), Tol));
            Assert.True(block.WithOpenings(new[] { GeoSolid3.Cylinder(new GeoPoint3(50, 50, -5), new GeoPoint3(50, 50, 25), 5, 8) }).TryCutOpenings(out GeoSolid3 holed));
            Assert.False(Boolean3.IsConvex(holed, Tol));

            // Faces turned inwards: not taken for convex, so left to the general boolean.
            Assert.False(Boolean3.IsConvex(new GeoSolid3(block.Faces.Select(f => f.Flip())), Tol));
        }

        [Fact]
        public void WhereTheClippingAnswersItIsTheRegion()
        {
            var rng = new Random(21);
            int answered = 0, overlapping = 0;

            for (int k = 0; k < 400; k++)
            {
                GeoSolid3 a = ConvexPart(rng), b = ConvexPart(rng);

                GeoSolid3[] exact = Boolean3.Intersect(a, b, Tight);
                double exactVolume = exact.Sum(p => p.Volume);
                overlapping += exactVolume > 1.0 ? 1 : 0;

                if (!Boolean3.TryIntersectConvex(a, b, Tol, out GeoSolid3[] clipped))
                {
                    continue;
                }

                answered++;
                GeoSolid3 piece = Assert.Single(clipped);
                Assert.True(piece.IsClosed());
                Assert.True(exact.Length > 0, "the clipping found a region the tight boolean does not");

                // The clipping builds its faces at the ordinary tolerance, so its corners may move by that much:
                // as much volume, at most, as every face moved out or in by the point tolerance.
                double surface = piece.Faces.Sum(f => f.Area);
                double allowed = Tol.EqualPoint * surface;
                Assert.True(Math.Abs(exactVolume - piece.Volume) <= allowed, $"volume {piece.Volume}, exactly {exactVolume}, allowed {allowed}");

                GeoPoint3 middle = exact.Length == 1 ? exact[0].Centroid : piece.Centroid;
                Assert.True(middle.DistanceTo(piece.Centroid) <= allowed / piece.Volume * 10.0 + 1E-9, $"middles {middle} and {piece.Centroid}");
            }

            Assert.True(answered > 80 && answered >= overlapping * 8 / 10, $"clipping answered {answered} of {overlapping} overlapping pairs");
        }

        /// <summary>
        /// Random points counted inside both parts give the shared volume within a few standard errors, and
        /// points the clipped region holds are inside both parts.
        /// </summary>
        [Fact]
        public void TheClippedRegionHoldsWhatBothPartsHold()
        {
            var rng = new Random(23);
            int checkedPairs = 0;

            for (int k = 0; k < 200 && checkedPairs < 8; k++)
            {
                GeoSolid3 a = ConvexPart(rng), b = ConvexPart(rng);

                if (!Boolean3.TryIntersectConvex(a, b, Tol, out GeoSolid3[] clipped) || clipped[0].Volume < 500)
                {
                    continue;
                }

                checkedPairs++;
                GeoSolid3 piece = clipped[0];
                GeoPreparedSolid3 pa = a.Prepare(), pb = b.Prepare(), pr = piece.Prepare();
                GeoAabb3 box = piece.GetAabb();
                double boxVolume = (box.Max.X - box.Min.X) * (box.Max.Y - box.Min.Y) * (box.Max.Z - box.Min.Z);
                int both = 0;
                const int samples = 6000;

                for (int s = 0; s < samples; s++)
                {
                    var probe = new GeoPoint3(
                        box.Min.X + (box.Max.X - box.Min.X) * rng.NextDouble(),
                        box.Min.Y + (box.Max.Y - box.Min.Y) * rng.NextDouble(),
                        box.Min.Z + (box.Max.Z - box.Min.Z) * rng.NextDouble());

                    bool inBoth = pa.Contains(probe) && pb.Contains(probe);
                    both += inBoth ? 1 : 0;

                    if (pr.Locate(probe) == PointLocation.Inside)
                    {
                        Assert.True(inBoth, $"{probe} is in the clipped region and not in both parts");
                    }
                }

                double counted = boxVolume * both / samples;
                double error = boxVolume * Math.Sqrt(Math.Max(both, 1)) / samples;
                Assert.True(Math.Abs(counted - piece.Volume) <= 5.0 * error, $"counted {counted} +/- {error}, clipped {piece.Volume}");
            }

            Assert.True(checkedPairs >= 8, $"only {checkedPairs} pairs checked");
        }

        [Fact]
        public void SlicesAndTouchesAreLeftToTheBoolean()
        {
            // Blocks set against each other's face, or into it by less than ten times the tolerance: the clipping
            // does not answer, so it cannot say otherwise than the boolean.
            foreach (double depth in new[] { 0.0, 1E-9, 1E-6, 5E-5, 1E-4, 5E-4 })
            {
                GeoSolid3 a = Block(0, 0, 0, 50, 50, 50);
                GeoSolid3 b = Block(50 - depth, 10, 10, 90, 40, 40);

                Assert.False(Boolean3.TryIntersectConvex(a, b, Tol, out _), $"answered at a depth of {depth}");
            }

            // In by a millimetre: plainly a region, and the clipping answers.
            Assert.True(Boolean3.TryIntersectConvex(Block(0, 0, 0, 50, 50, 50), Block(49, 10, 10, 90, 40, 40), Tol, out GeoSolid3[] slab));
            Assert.Equal(1.0 * 30 * 30, Assert.Single(slab).Volume, 6);
        }

        /// <summary>
        /// A mesh of round bars and a scatter of turned blocks: the clash check gives what checking every pair by
        /// hand with the general boolean gives.
        /// </summary>
        [Fact]
        public void AClashCheckOfConvexPartsGivesWhatTheBooleanGivesByHand()
        {
            var rng = new Random(22);
            var parts = new List<GeoSolid3>();

            for (int i = 0; i < 8; i++)
            {
                parts.Add(GeoSolid3.Cylinder(new GeoPoint3(-20, 20 + i * 40, 0), new GeoPoint3(340, 20 + i * 40, rng.Next(-3, 4)), 8, 12));
                parts.Add(GeoSolid3.Cylinder(new GeoPoint3(20 + i * 40, -20, 14), new GeoPoint3(20 + i * 40, 340, 14 + rng.Next(-3, 4)), 8, 12));
            }

            for (int i = 0; i < 20; i++)
            {
                double x = rng.Next(0, 300), y = rng.Next(0, 300);
                parts.Add(Block(x, y, -10, x + rng.Next(10, 50), y + rng.Next(10, 50), rng.Next(0, 30))
                    .TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(x, y, 0), GeoVector3.ZAxis, rng.NextDouble())));
            }

            GeoPreparedSolid3[] prepared = parts.Select(p => p.Prepare()).ToArray();
            ClashResult[] found = Clash3.Find(prepared);
            var byHand = new List<(int, int, ClashKind, double)>();

            for (int i = 0; i < prepared.Length; i++)
            {
                for (int j = i + 1; j < prepared.Length; j++)
                {
                    if (!prepared[i].CollidesWith(prepared[j]))
                    {
                        continue;
                    }

                    double shared = Boolean3.Intersect(prepared[i].Material, prepared[j].Material, Tol).Sum(p => p.Volume);
                    byHand.Add((i, j, shared > 0 ? ClashKind.Hard : ClashKind.Touch, shared));
                }
            }

            Assert.True(byHand.Count(p => p.Item3 == ClashKind.Hard) > 40, $"only {byHand.Count} colliding pairs");
            Assert.Equal(byHand.Select(p => (p.Item1, p.Item2, p.Item3)), found.Select(r => (r.First, r.Second, r.Kind)));

            for (int k = 0; k < found.Length; k++)
            {
                Assert.Equal(byHand[k].Item4, found[k].Volume, 6);
            }
        }
    }
}
