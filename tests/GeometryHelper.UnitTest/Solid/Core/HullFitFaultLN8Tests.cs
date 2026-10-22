using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The hull of a thin plate holds every point it is built from within the point tolerance, its faces merged; and a body fits the
    /// same box however it is turned, two rectangles on one face as large weighed as two on two faces are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The triangles of a hull merged within the tolerance could stand a face as far again from a point: 5 of 1 000 plates 100 by 100
    /// and from 0.002 to 1 thick left a point up to 1.08E-3 outside the hull they were built from.
    /// </para>
    /// <para>
    /// The tetrahedron on (0, 0, 0), (24, 0, 0), (0, 18, 0) and (0, 0, 40) fitted 13.548779 by 27.340984 by 46.647615 as it is and
    /// 13.548779 by 30 by 42.513057 turned: two rectangles of one area on its long face, the one found first taken.
    /// </para>
    /// </remarks>
    public class HullFitFaultLN8Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        /// <summary>
        /// The plates: for each thickness, two hundred sets of 8 to 207 points in a plate 100 by 100, every other set turned about an
        /// axis through a point of the X axis.
        /// </summary>
        private static IEnumerable<List<GeoPoint3>> Plates()
        {
            var random = new Random(12345);

            foreach (double thick in new[] { 0.002, 0.005, 0.01, 0.05, 1.0 })
            {
                for (int set = 0; set < 200; set++)
                {
                    int count = 8 + random.Next(200);
                    var points = new List<GeoPoint3>();

                    for (int i = 0; i < count; i++)
                    {
                        points.Add(new GeoPoint3(random.NextDouble() * 100, random.NextDouble() * 100, random.NextDouble() * thick));
                    }

                    if (random.Next(2) == 0)
                    {
                        GeoTransform3 turn = GeoTransform3.RotationAxis(
                            new GeoPoint3(random.NextDouble() * 1000, 0, 0),
                            new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5 + 1E-3),
                            random.NextDouble() * 6);
                        points = points.Select(p => p.TransformBy(turn)).ToList();
                    }

                    yield return points;
                }
            }
        }

        [Fact]
        public void TheHullOfAThinPlate_HoldsEveryPointWithinTheTolerance()
        {
            int built = 0;

            foreach (List<GeoPoint3> points in Plates())
            {
                if (!ConvexHull3.TryOf(points, out GeoSolid3 hull, Thousandth))
                {
                    continue;
                }

                built++;
                Assert.True(hull.IsClosed(Thousandth));

                foreach (GeoPoint3 point in points)
                {
                    Assert.InRange(hull.SignedDistanceTo(point, Thousandth), double.NegativeInfinity, Thousandth.EqualPoint);
                }
            }

            Assert.InRange(built, 900, 1000);
        }

        private static readonly GeoPoint3[] Tetrahedron = { new GeoPoint3(0, 0, 0), new GeoPoint3(24, 0, 0), new GeoPoint3(0, 18, 0), new GeoPoint3(0, 0, 40) };

        private static double[] Sides(GeoObb3 box)
        {
            double[] sides = { box.SizeX, box.SizeY, box.SizeZ };
            Array.Sort(sides);
            return sides;
        }

        [Fact]
        public void ATetrahedronTurned_FitsTheBoxItFitsAsItIs()
        {
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(10, 30, 20), new GeoVector3(0.3, -0.2, 1), 0.7);
            double[] asItIs = Sides(GeoObb3.Fit(Tetrahedron, Thousandth));
            double[] turned = Sides(GeoObb3.Fit(Tetrahedron.Select(p => p.TransformBy(turn)), Thousandth));

            Assert.Equal(13.548779, asItIs[0], 6);
            Assert.Equal(27.340984, asItIs[1], 6);
            Assert.Equal(46.647615, asItIs[2], 6);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(asItIs[i], turned[i], 9);
            }
        }

        [Fact]
        public void ATetrahedronTurnedAnyWay_FitsOneBox()
        {
            var random = new Random(77);
            double[] asItIs = Sides(GeoObb3.Fit(Tetrahedron, Thousandth));

            for (int k = 0; k < 40; k++)
            {
                GeoTransform3 turn = GeoTransform3.RotationAxis(
                    new GeoPoint3(0, 0, 0), new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5), random.NextDouble() * 6);
                double[] turned = Sides(GeoObb3.Fit(Tetrahedron.Select(p => p.TransformBy(turn)), Thousandth));

                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(asItIs[i], turned[i], 9);
                }
            }
        }
    }
}
