using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The hull of the points of a plate a few tolerances thick closes and holds every one of them, and so does the box fitted
    /// round them.
    /// </summary>
    /// <remarks>
    /// The hull took a point only where it stood further than the tolerance off the plane of a face it saw, and the faces of a
    /// plate a thousandth thick meet at its rim so nearly flat that a point 10 off the rim stands within a thousandth of every
    /// plane; and reading which faces a point sees by a test that rounds, it came out open, or folded over itself. Of 300 sets
    /// of 40 points on plates 0.002 thick, every hull was open or wound against itself and 185 left a point out, by up to 24;
    /// and the boxes fitted round them left points out of 82, by up to 13. A hull that does not close, folds back over
    /// itself, or leaves a point further than the tolerance from its triangles is built again now with an exact test of sides, from the
    /// widest tetrahedron the points give, each point left out only where it is within the tolerance of the triangles.
    /// </remarks>
    public class ConvexHull3FaultLN5PlateTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        /// <summary>
        /// Points on the top and the bottom of a plate 200 by 100 and <paramref name="thickness"/> thick, and some within it,
        /// from a sequence of whole numbers so that every runtime has the same ones.
        /// </summary>
        private static List<GeoPoint3> Plate(double thickness, int count, int seed)
        {
            var points = new List<GeoPoint3>();
            uint state = (uint)(seed * 7919 + 17);

            for (int i = 0; i < count; i++)
            {
                state = state * 1664525u + 1013904223u;
                double x = (state >> 8) % 20001 / 100.0;
                state = state * 1664525u + 1013904223u;
                double y = (state >> 8) % 10001 / 100.0;
                state = state * 1664525u + 1013904223u;
                uint side = (state >> 8) % 4;
                double z = side == 0 ? 0.0 : side == 3 ? thickness / 2 : thickness;
                points.Add(new GeoPoint3(500 + x, 300 + y, 40 + z));
            }

            return points;
        }

        private static double Outside(GeoSolid3 hull, GeoPoint3 point)
            => hull.Contains(point, Default) ? 0.0 : hull.DistanceTo(point, Default);

        private static double Outside(GeoObb3 box, GeoPoint3 point)
        {
            GeoVector3 d = box.Center.GetVectorTo(point);
            double x = Math.Max(0.0, Math.Abs(d.DotProduct(box.AxisX)) - box.ExtentX);
            double y = Math.Max(0.0, Math.Abs(d.DotProduct(box.AxisY)) - box.ExtentY);
            double z = Math.Max(0.0, Math.Abs(d.DotProduct(box.AxisZ)) - box.ExtentZ);
            return Math.Sqrt(x * x + y * y + z * z);
        }

        [Theory]
        [InlineData(0.0025)]
        [InlineData(0.005)]
        [InlineData(0.01)]
        public void TheHullOfAPlateAFewTolerancesThick_ClosesAndHoldsEveryPointWithinTheTolerance(double thickness)
        {
            // Its triangles merged only where they lie in one plane to a trillionth: merged within the tolerance, a face can
            // stand as far again from a point it held; see the test below.
            var flat = new Tolerance(Default.EqualPoint, Default.EqualVector, Default.EqualAngleRad, 1E-12);

            for (int seed = 0; seed < 20; seed++)
            {
                List<GeoPoint3> points = Plate(thickness, 40, seed);

                Assert.True(ConvexHull3.TryOf(points, out GeoSolid3 hull, flat));
                SolidValidation3 validation = hull.Validate(Default);
                Assert.True(validation.IsClosed, $"seed {seed}: {validation}");
                Assert.True(validation.IsWoundAlike, $"seed {seed}: {validation}");

                foreach (GeoPoint3 point in points)
                {
                    Assert.True(Outside(hull, point) <= Default.EqualPoint, $"seed {seed}: {point} {Outside(hull, point):R} outside");
                }
            }
        }

        [Theory]
        [InlineData(0.0025)]
        [InlineData(0.005)]
        [InlineData(0.01)]
        public void TheHullOfAPlate_MergedWithinTheTolerance_HoldsEveryPointWithinTwiceIt(double thickness)
        {
            for (int seed = 0; seed < 20; seed++)
            {
                List<GeoPoint3> points = Plate(thickness, 40, seed);

                Assert.True(ConvexHull3.TryOf(points, out GeoSolid3 hull, Default));
                Assert.True(hull.Validate(Default).IsClosed);

                foreach (GeoPoint3 point in points)
                {
                    Assert.True(Outside(hull, point) <= 2.0 * Default.EqualPoint, $"seed {seed}: {point} {Outside(hull, point):R} outside");
                }
            }
        }

        [Theory]
        [InlineData(0.001)]
        [InlineData(0.002)]
        [InlineData(0.005)]
        [InlineData(0.01)]
        public void TheBoxFittedRoundAPlate_HoldsEveryPoint(double thickness)
        {
            for (int seed = 0; seed < 20; seed++)
            {
                List<GeoPoint3> points = Plate(thickness, 40, seed);

                GeoObb3 box = GeoObb3.Fit(points, Default);

                foreach (GeoPoint3 point in points)
                {
                    Assert.True(Outside(box, point) <= Default.EqualPoint, $"seed {seed}: {point} {Outside(box, point):R} outside");
                }
            }
        }

        [Fact]
        public void APlateNoThickerThanTheTolerance_SpansNoVolume()
        {
            Assert.False(ConvexHull3.TryOf(Plate(0.0005, 40, 3), out _, Default));
        }

        [Fact]
        public void APlateNoThickerThanTwiceTheTolerance_SpansNoVolume_OrHoldsEveryPoint()
        {
            // Every point of such a plate lies within the tolerance of its middle plane: whether the hull finds that depends
            // on the plane it starts from, and either answer is within the tolerance.
            var flat = new Tolerance(Default.EqualPoint, Default.EqualVector, Default.EqualAngleRad, 1E-12);

            for (int seed = 0; seed < 20; seed++)
            {
                List<GeoPoint3> points = Plate(0.0015, 40, seed);

                if (!ConvexHull3.TryOf(points, out GeoSolid3 hull, flat))
                {
                    continue;
                }

                Assert.True(hull.Validate(Default).IsClosed);

                foreach (GeoPoint3 point in points)
                {
                    Assert.True(Outside(hull, point) <= Default.EqualPoint, $"seed {seed}: {point} {Outside(hull, point):R} outside");
                }
            }
        }
    }
}
