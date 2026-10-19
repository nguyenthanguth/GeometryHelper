using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A pair of parts is checked the same whichever is listed first: the same volume, depth and place to the last
    /// bit, the same patches of contact and the same gap, each written from the part that was listed first.
    /// </summary>
    public class ClashOrderTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoSolid3 Turned(GeoSolid3 solid, Random rng)
        {
            GeoPoint3 centre = solid.GetAabb().Center;
            var axis = new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() + 0.2);

            return solid.TransformBy(GeoTransform3.RotationAxis(centre, axis, rng.NextDouble() * 3.0));
        }

        /// <summary>A slab at the given corner, plain, with a hole through it, or an L; most of them turned about a slant axis.</summary>
        private static GeoSolid3 Part(Random rng, double x, double y, double z, int shape)
        {
            double width = rng.Next(2, 12) * 10, depth = rng.Next(2, 12) * 10, height = rng.Next(1, 6) * 10;
            GeoSolid3 part = Box(x, y, z, x + width, y + depth, z + height);

            if (shape == 1)
            {
                part = part.WithOpenings(new[] { Box(x + width * 0.3, y + depth * 0.3, z - 1, x + width * 0.6, y + depth * 0.6, z + height + 1) });
            }
            else if (shape == 2 && Box(x, y, z, x + width, y + depth * 0.4, z + height).TryUnion(Box(x, y, z, x + width * 0.4, y + depth, z + height), out GeoSolid3 l))
            {
                part = l;
            }

            return rng.NextDouble() < 0.7 ? Turned(part, rng) : part;
        }

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static long[] Bits(GeoPoint3 p) => new[] { Bits(p.X), Bits(p.Y), Bits(p.Z) };

        private static void SameHard(ClashResult x, ClashResult y)
        {
            Assert.Equal(x.Kind, y.Kind);
            Assert.Equal(Bits(x.Volume), Bits(y.Volume));
            Assert.Equal(Bits(x.Depth), Bits(y.Depth));
            Assert.Equal(Bits(x.Location), Bits(y.Location));
            Assert.Equal(x.Overlaps.Count, y.Overlaps.Count);
            Assert.Equal(Corners(x.Overlaps.SelectMany(o => o.Faces)), Corners(y.Overlaps.SelectMany(o => o.Faces)));
            Assert.Equal(x.Overlaps.Select(o => Corners(o.Faces)).OrderBy(c => string.Join(";", c), StringComparer.Ordinal).Select(c => string.Join(";", c)),
                y.Overlaps.Select(o => Corners(o.Faces)).OrderBy(c => string.Join(";", c), StringComparer.Ordinal).Select(c => string.Join(";", c)));
        }

        /// <summary>The corners of the faces, as the bits of their coordinates, in order of those bits: what a loop is made of, whatever corner it starts at and whichever way it winds.</summary>
        private static List<string> Corners(IEnumerable<GeoFace3> faces)
        {
            var corners = new List<string>();

            foreach (GeoFace3 face in faces)
            {
                foreach (GeoPoint3 p in face.Boundary.Vertices.Concat(face.Holes.SelectMany(h => h.Vertices)))
                {
                    corners.Add(string.Join(",", Bits(p)));
                }
            }

            corners.Sort(StringComparer.Ordinal);

            return corners;
        }

        [Fact]
        public void ThreeHundredAndSixtyRandomPairs_GiveTheSameVolumeDepthAndPlaceEitherWayRound()
        {
            var rng = new Random(359);
            int pairs = 0, hard = 0;

            while (pairs < 360)
            {
                GeoSolid3 a = Part(rng, 0, 0, 0, rng.Next(3));
                GeoSolid3 b = Part(rng, rng.Next(-60, 100), rng.Next(-60, 100), rng.Next(-30, 40), rng.Next(3));

                ClashResult[] ab = Clash3.Find(new[] { a }, new[] { b }, ClashOptions.Default, Tolerance);
                ClashResult[] ba = Clash3.Find(new[] { b }, new[] { a }, ClashOptions.Default, Tolerance);

                Assert.Equal(ab.Length, ba.Length);

                if (ab.Length == 0)
                {
                    continue;
                }

                pairs++;
                SameHard(ab[0], ba[0]);
                hard += ab[0].Kind == ClashKind.Hard ? 1 : 0;
            }

            Assert.True(hard > 300, $"only {hard} of 360 pairs share volume; the test needs more");
        }

        [Fact]
        public void OneListOfTwoParts_GivesTheSameResultInEitherOrder()
        {
            var rng = new Random(77);
            int hard = 0;

            for (int k = 0; k < 60; k++)
            {
                GeoSolid3 a = Part(rng, 0, 0, 0, rng.Next(3));
                GeoSolid3 b = Part(rng, rng.Next(-40, 60), rng.Next(-40, 60), rng.Next(-20, 20), rng.Next(3));

                ClashResult[] ab = Clash3.Find(new[] { a, b }, ClashOptions.Default, Tolerance);
                ClashResult[] ba = Clash3.Find(new[] { b, a }, ClashOptions.Default, Tolerance);

                Assert.Equal(ab.Length, ba.Length);

                if (ab.Length == 1)
                {
                    SameHard(ab[0], ba[0]);
                    hard += ab[0].Kind == ClashKind.Hard ? 1 : 0;
                }
            }

            Assert.True(hard > 20, $"only {hard} pairs share volume; the test needs more");
        }

        [Fact]
        public void TwoPartsAgainstEachOther_GiveTheSameAreaAndPlaceOfContact_EachPatchFacingOutOfTheOneListedFirst()
        {
            var rng = new Random(5);

            for (int k = 0; k < 40; k++)
            {
                // Two blocks 40 and 30 along X sharing a face, both turned the same way about the same axis.
                double height = rng.Next(1, 6) * 10;
                GeoSolid3 a = Box(0, 0, 0, 40, 30, height);
                GeoSolid3 b = Box(40, rng.Next(0, 20), 0, 70, 30 + rng.Next(0, 20), height + rng.Next(0, 3) * 5);
                GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(10, 5, 5), new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() + 0.2), rng.NextDouble() * 3.0);
                a = a.TransformBy(turn);
                b = b.TransformBy(turn);

                ClashResult ab = Assert.Single(Clash3.Find(new[] { a }, new[] { b }, ClashOptions.Default, Tolerance));
                ClashResult ba = Assert.Single(Clash3.Find(new[] { b }, new[] { a }, ClashOptions.Default, Tolerance));

                Assert.Equal(ClashKind.Touch, ab.Kind);
                Assert.Equal(ClashKind.Touch, ba.Kind);
                Assert.Equal(Bits(ab.ContactArea), Bits(ba.ContactArea));
                Assert.Equal(Bits(ab.Location), Bits(ba.Location));
                Assert.Equal(ab.Contact.Count, ba.Contact.Count);
                Assert.Equal(Corners(ab.Contact), Corners(ba.Contact));

                // A patch faces out of the part listed first: from a's side towards b in the one call, from b's towards a in the other.
                GeoVector3 towardsB = a.GetAabb().Center.GetVectorTo(b.GetAabb().Center);

                Assert.All(ab.Contact, patch => Assert.True(patch.Normal.DotProduct(towardsB) > 0.0));
                Assert.All(ba.Contact, patch => Assert.True(patch.Normal.DotProduct(towardsB) < 0.0));
            }
        }

        [Fact]
        public void ABodyAndItsCopyPreparedWithinAnotherTolerance_GiveTheSameResultEitherWayRound()
        {
            // A guard, not a fix: it passes without any tie-break on what the preparation made of the bodies, and says so for whoever adds one.
            // A box 40 by 30 by 20 with a notch 0.005 deep across its top: convex within 1E-2, not within 1E-3. The two copies share every face
            // and every box, so only what the preparation made of them tells them apart.
            Assert.True(Box(0, 0, 0, 40, 30, 20).TrySubtract(Box(10, 10, 19.995, 20, 20, 21), out GeoSolid3 notched));

            var fine = new GeoPreparedSolid3(notched, new Tolerance(1E-3, 1E-5, 1E-3, 1E-3));
            var coarse = new GeoPreparedSolid3(notched, new Tolerance(1E-2, 1E-5, 1E-2, 1E-2));

            ClashResult fineFirst = Assert.Single(Clash3.Find(new[] { fine }, new[] { coarse }, ClashOptions.Default, Tolerance));
            ClashResult coarseFirst = Assert.Single(Clash3.Find(new[] { coarse }, new[] { fine }, ClashOptions.Default, Tolerance));

            SameHard(fineFirst, coarseFirst);
        }

        [Fact]
        public void TwoPartsApart_GiveTheSameGapEitherWayRound_EachWrittenFromTheOneListedFirst()
        {
            var rng = new Random(11);
            int gaps = 0;

            for (int k = 0; k < 40; k++)
            {
                GeoSolid3 a = Turned(Box(0, 0, 0, 30, 20, 10), rng);
                GeoSolid3 b = Turned(Box(rng.Next(35, 45), rng.Next(-5, 5), rng.Next(-3, 3), rng.Next(60, 70), rng.Next(20, 30), rng.Next(10, 20)), rng);
                var options = new ClashOptions(clearance: 25.0);

                ClashResult[] ab = Clash3.Find(new[] { a }, new[] { b }, options, Tolerance);
                ClashResult[] ba = Clash3.Find(new[] { b }, new[] { a }, options, Tolerance);

                Assert.Equal(ab.Length, ba.Length);

                if (ab.Length == 0)
                {
                    continue;
                }

                Assert.Equal(ab[0].Kind, ba[0].Kind);

                if (ab[0].Kind != ClashKind.Clearance)
                {
                    continue;
                }

                gaps++;
                Assert.Equal(Bits(ab[0].Distance), Bits(ba[0].Distance));
                Assert.Equal(Bits(ab[0].Location), Bits(ba[0].Location));

                // The gap is written from the part listed first, so the other call writes it backwards.
                GeoLine3 forward = ab[0].Gap.Value;
                GeoLine3 backward = ba[0].Gap.Value;

                Assert.Equal(Bits(forward.StartPoint), Bits(backward.EndPoint));
                Assert.Equal(Bits(forward.EndPoint), Bits(backward.StartPoint));
            }

            Assert.True(gaps > 20, $"only {gaps} pairs are apart; the test needs more");
        }
    }
}
