using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// What happens a long way from the origin.
    /// <para>
    /// A real model is not centred on zero: a building is surveyed in metres from a national grid point
    /// and comes into the modeller at coordinates in the millions of millimetres. A double carries about
    /// sixteen significant digits, so at 10^6 the gap between one representable number and the next is
    /// around 10^-10, and at 10^9 it is around 10^-7 — approaching the default point tolerance of 10^-4.
    /// Every measurement here is taken twice, once at the origin and once carried far away, and the two
    /// must agree.
    /// </para>
    /// </summary>
    public partial class InvariantSweepTests
    {
        /// <summary>The distances a model realistically sits at, in millimetres.</summary>
        public static IEnumerable<object[]> Offsets()
        {
            yield return new object[] { 0.0 };          // at the origin
            yield return new object[] { 1.0E4 };        // ten metres
            yield return new object[] { 1.0E6 };        // a kilometre
            yield return new object[] { 1.0E7 };        // ten kilometres
        }

        [Theory]
        [MemberData(nameof(Offsets))]
        public void MeasurementsHoldFarFromTheOrigin(double offset)
        {
            Random rng = new Random(6060);
            var shift = new GeoVector3(offset, offset * 0.5, offset * 0.25);

            for (int t = 0; t < 60; t++)
            {
                GeoPoint3[] near = Star(rng, 4 + rng.Next(5), true);

                GeoPoint3[] far = new GeoPoint3[near.Length];
                for (int i = 0; i < near.Length; i++) { far[i] = near[i].Add(shift); }

                GeoSolid3 here;
                GeoSolid3 there;
                try { here = Prism(near, 4.0); there = Prism(far, 4.0); }
                catch (ArgumentException) { continue; }

                // Moving a body does not change how big it is.
                Assert.Equal(here.Volume, there.Volume, 6);
                Assert.Equal(here.SurfaceArea, there.SurfaceArea, 6);
                Assert.True(there.IsClosed(), $"the body stopped closing at {offset:0.###e0}");

                // Nor where its middle is, relative to itself.
                GeoPoint3 expected = here.Centroid.Add(shift);
                Assert.True(there.Centroid.IsEqualTo(expected, Tol),
                            $"centroid drifted by {there.Centroid.DistanceTo(expected):0.#########} at {offset:0.###e0}");

                // Cutting it in half is the same operation wherever it sits.
                var cutHere = new GeoPlane3(GeoPoint3.Origin, GeoVector3.YAxis);
                var cutThere = new GeoPlane3(GeoPoint3.Origin.Add(shift), GeoVector3.YAxis);

                bool splitHere = here.TrySplitBy(cutHere, out GeoSolid3 upHere, out GeoSolid3 loHere);
                bool splitThere = there.TrySplitBy(cutThere, out GeoSolid3 upThere, out GeoSolid3 loThere);

                Assert.Equal(splitHere, splitThere);

                if (splitHere)
                {
                    Assert.Equal(upHere.Volume, upThere.Volume, 6);
                    Assert.Equal(loHere.Volume, loThere.Volume, 6);
                    Assert.Equal(there.Volume, upThere.Volume + loThere.Volume, 6);
                }
            }
        }

        [Theory]
        [MemberData(nameof(Offsets))]
        public void SmallFeaturesSurviveFarFromTheOrigin(double offset)
        {
            var shift = new GeoVector3(offset, offset * 0.5, offset * 0.25);

            // A millimetre-scale feature carried out to where a real model sits. This is the awkward
            // combination the plan calls out: a large coordinate holding a small distance.
            var a = new GeoPoint3(0, 0, 0).Add(shift);
            var b = new GeoPoint3(1, 0, 0).Add(shift);
            var c = new GeoPoint3(1, 1, 0).Add(shift);
            var d = new GeoPoint3(0, 1, 0).Add(shift);

            var square = new GeoPolygon3(a, b, c, d);

            Assert.Equal(1.0, square.Area, 6);
            Assert.Equal(4.0, square.Length, 6);

            // The two points are a millimetre apart wherever they are, and must not merge.
            Assert.False(a.IsEqualTo(b, Tol), $"a millimetre collapsed to nothing at {offset:0.###e0}");
            Assert.Equal(1.0, a.DistanceTo(b), 6);

            // Cutting the square down the middle gives two halves of the same size.
            var cutter = new GeoPlane3(new GeoPoint3(0.5, 0, 0).Add(shift), GeoVector3.XAxis);

            Assert.True(square.TrySplitBy(cutter, out GeoPolygon3[] above, out GeoPolygon3[] below),
                        $"the cut stopped working at {offset:0.###e0}");

            double up = 0.0; foreach (GeoPolygon3 p in above) { up += p.Area; }
            double down = 0.0; foreach (GeoPolygon3 p in below) { down += p.Area; }

            Assert.Equal(0.5, up, 6);
            Assert.Equal(0.5, down, 6);
        }

        [Fact]
        public void TheLimitOfPrecisionIsWhereItIsExpectedToBe()
        {
            // Not a check on the library so much as on the arithmetic beneath it: this records where a
            // millimetre stops being representable against the default tolerance, so that a later reader
            // knows the ceiling rather than discovering it in a model.
            foreach (double offset in new[] { 1.0E6, 1.0E7, 1.0E8 })
            {
                var origin = new GeoPoint3(offset, offset, offset);
                var oneAway = new GeoPoint3(offset + 1.0, offset, offset);

                Assert.False(origin.IsEqualTo(oneAway, Tol),
                             $"a millimetre apart already reads as the same point at {offset:0.###e0}");

                Assert.Equal(1.0, origin.DistanceTo(oneAway), 6);
            }

            // A tenth of the point tolerance apart must read as the same point, at any of these ranges.
            foreach (double offset in new[] { 0.0, 1.0E6, 1.0E7 })
            {
                var origin = new GeoPoint3(offset, offset, offset);
                var barelyAway = new GeoPoint3(offset + Tolerance.DefaultEqualPoint / 10.0, offset, offset);

                Assert.True(origin.IsEqualTo(barelyAway, Tol),
                            $"two points well within tolerance read as different at {offset:0.###e0}");
            }
        }
    }
}
