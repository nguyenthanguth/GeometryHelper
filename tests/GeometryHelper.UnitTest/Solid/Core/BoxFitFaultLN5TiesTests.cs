using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Of the boxes as small as each other round a set of points, the fit takes the thinnest, and a clash reads that thickness.
    /// </summary>
    /// <remarks>
    /// A prism on a right triangle with legs 18 and 24 fits a box along its legs and one along its long side, 30 by 14.4, as
    /// large as each other, and the fit took the one it found first, 18 thick where the prism is 14.4 across, whatever order
    /// the corners came in. Boxes as large to rounding are told apart by their least side now, then the middle one, then the
    /// longest.
    /// </remarks>
    public class BoxFitFaultLN5TiesTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static List<GeoPoint3> Prism(double length)
        {
            var corners = new List<GeoPoint3>();

            foreach (double z in new[] { 0.0, length })
            {
                corners.Add(new GeoPoint3(0, 0, z));
                corners.Add(new GeoPoint3(18, 0, z));
                corners.Add(new GeoPoint3(0, 24, z));
            }

            return corners;
        }

        private static double[] Sides(GeoObb3 box) => new[] { box.SizeX, box.SizeY, box.SizeZ }.OrderBy(side => side).ToArray();

        [Theory]
        [InlineData(10.0, 10.0, 14.4, 30.0)]
        [InlineData(16.0, 14.4, 16.0, 30.0)]
        [InlineData(40.0, 14.4, 30.0, 40.0)]
        public void APrismOnATriangle_IsFittedTheThinnestOfTheBoxesAsSmall(double length, double least, double middle, double longest)
        {
            List<GeoPoint3> corners = Prism(length);

            // Every order of the corners comes to the same box.
            for (int turn = 0; turn < corners.Count; turn++)
            {
                List<GeoPoint3> order = corners.Skip(turn).Concat(corners.Take(turn)).ToList();
                double[] sides = Sides(GeoObb3.Fit(order, Default));

                Assert.Equal(least, sides[0], 9);
                Assert.Equal(middle, sides[1], 9);
                Assert.Equal(longest, sides[2], 9);
            }
        }

        [Fact]
        public void AClashOverAPrism_IsAsDeepAsThePrismIsThin()
        {
            // A block 18 by 24 by 40 and a prism on its corner's triangle running past both its ends share the prism 40 long.
            GeoSolid3 block = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(18, 24, 40)).ToObb().ToSolid();
            var bottom = new GeoPolygon3(new[] { new GeoPoint3(0, 0, -10), new GeoPoint3(0, 24, -10), new GeoPoint3(18, 0, -10) }, Default);
            GeoSolid3 prism = GeoSolid3.Extrude(bottom, new GeoVector3(0, 0, 60), Default);

            ClashResult clash = Assert.Single(Clash3.Find(new[] { block }, new[] { prism }, ClashOptions.Default, Default));

            Assert.Equal(18.0 * 24.0 / 2.0 * 40.0, clash.Volume, 6);
            Assert.Equal(14.4, clash.Depth, 6);
        }
    }
}
