using System;
using System.Collections.Generic;
using System.Diagnostics;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Work that grew with the square of its size on a long shape, and no longer does, held to a time several times what it
    /// takes now and well under what it took: a long comb offset, and long strips side by side united in time and memory.
    /// </summary>
    public class SpeedF2Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        /// <summary>
        /// A comb along X: teeth 2 wide and 100 long, 2 apart, standing on a back 100 deep.
        /// </summary>
        private static GeoPolygon2 Comb(int teeth)
        {
            var points = new List<GeoPoint2> { new GeoPoint2(0, 0) };
            double x = 0;

            for (int i = 0; i < teeth; i++)
            {
                points.Add(new GeoPoint2(x + 2, 0));
                points.Add(new GeoPoint2(x + 2, 100));
                points.Add(new GeoPoint2(x + 4, 100));
                points.Add(new GeoPoint2(x + 4, 0));
                x += 4;
            }

            points.Add(new GeoPoint2(x + 2, 0));
            points.Add(new GeoPoint2(x + 2, 200));
            points.Add(new GeoPoint2(0, 200));
            return new GeoPolygon2(points);
        }

        [Fact]
        public void ACombOf40000TeethOffsetInwards_ComesBackInUnderSixSeconds()
        {
            // The points Clipper2 rounds are given back their digits from the input edges near them, found in cells of a grid
            // 64 across the drawing: on a comb 160 000 long each cell held thousands of edges, and every point read them all,
            // 11 seconds under .NET Framework. The cells are sized by the number of edges now.
            GeoPolygon2 comb = Comb(40000);

            var watch = Stopwatch.StartNew();
            GeoPolygon2[] offset = Offset2.Offset(comb, -0.5, OffsetOptions.Default, Thousandth);
            watch.Stop();

            // Every side moves in by a half, and the corners give back a quarter for each of the four by which those standing
            // out outnumber those standing in.
            Assert.Single(offset);
            Assert.InRange(offset[0].Area, comb.Area - 0.5 * comb.Length + 0.5, comb.Area - 0.5 * comb.Length + 1.5);
            Assert.Equal(comb.Vertices.Count, offset[0].Vertices.Count);
            Assert.True(watch.Elapsed.TotalSeconds < 6, watch.Elapsed.ToString());
        }

        [Fact]
        public void EightThousandStrips100000LongSideBySide_AreUnitedInUnderTenSecondsAndAGigabyte()
        {
            // Each strip is 100 000 by 1, and the next a tenth along and a thousandth up. Filed under every square cell along it,
            // as many cells as edges, each long edge took tens of thousands of entries: 2.2 GB and 9 seconds. The cells are as
            // much wider than tall as the edges run, and no finer than lets the edges fill sixteen cells an edge on the whole.
            var strips = new List<GeoPolygon2>();

            for (int q = 0; q <= 8000; q++)
            {
                double x = q * 0.1, y = q * 1E-3;
                strips.Add(new GeoPolygon2(new List<GeoPoint2> { new GeoPoint2(x, y), new GeoPoint2(x + 1E5, y), new GeoPoint2(x + 1E5, y + 1), new GeoPoint2(x, y + 1) }));
            }

            AppDomain.MonitoringIsEnabled = true;
            long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
            var watch = Stopwatch.StartNew();
            GeoFace2[] union = Boolean2.Union(strips, Thousandth);
            watch.Stop();
            long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;

            // One region 9 high along the middle, 99 200 long, and a stair of about 4 000 at either end, its steps a thousandth
            // high merged within the point tolerance.
            Assert.Single(union);
            Assert.InRange(union[0].Area, 9.0E5, 9.01E5);
            Assert.True(allocated < 1L << 30, allocated.ToString());
            Assert.True(watch.Elapsed.TotalSeconds < 10, watch.Elapsed.ToString());
        }
    }
}
