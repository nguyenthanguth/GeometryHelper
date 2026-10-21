using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The hull of points its faster build reads wrong is built in bounded time and memory, and a hull that build gets right
    /// is the one it always gave, to the bit.
    /// </summary>
    /// <remarks>
    /// Read in floating point, the faces a point sees could make a rim that is not one loop, and the faces put on it then no
    /// longer closed the hull; built on to the last point, each further point multiplied its faces, and 200 points in a plate
    /// 100 by 100 and 0.002 thick ran out of memory, as did 500, 120 points within a thousandth of a cube's faces, 10 000 in a
    /// cube and 3 000 on a sphere. The faster build stops as soon as a rim is not one loop or the faces outnumber what a closed
    /// surface on the points taken can have, and the hull is built exactly. A hull that closes and holds every point within
    /// the tolerance is kept as it is: rebuilt wherever it folded by more than rounding, one in 30 hulls of 200 points in a
    /// cube came out with other bits, and each of 30 of points on a lattice with copies a hair off its corners.
    /// </remarks>
    public class ConvexHull3FaultLN5BoundedTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static List<GeoPoint3> InAPlate(Random random, int count)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i < count; i++)
            {
                points.Add(new GeoPoint3(random.NextDouble() * 100, random.NextDouble() * 100, random.NextDouble() * 0.002));
            }

            return points;
        }

        private static List<GeoPoint3> NearACubesFaces(Random random, int count)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i < count; i++)
            {
                int face = i % 6;
                double a = random.NextDouble() * 100, b = random.NextDouble() * 100, off = (random.NextDouble() - 0.5) * 0.002;
                points.Add(face == 0 ? new GeoPoint3(off, a, b) : face == 1 ? new GeoPoint3(100 + off, a, b)
                    : face == 2 ? new GeoPoint3(a, off, b) : face == 3 ? new GeoPoint3(a, 100 + off, b)
                    : face == 4 ? new GeoPoint3(a, b, off) : new GeoPoint3(a, b, 100 + off));
            }

            return points;
        }

        private static List<GeoPoint3> InACube(Random random, int count)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i < count; i++)
            {
                points.Add(new GeoPoint3(random.NextDouble() * 100, random.NextDouble() * 100, random.NextDouble() * 100));
            }

            return points;
        }

        private static List<GeoPoint3> OnASphere(Random random, int count)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i < count; i++)
            {
                double u = 2 * random.NextDouble() - 1, turn = 2 * Math.PI * random.NextDouble(), across = Math.Sqrt(1 - u * u);
                points.Add(new GeoPoint3(50 * across * Math.Cos(turn), 50 * across * Math.Sin(turn), 50 * u));
            }

            return points;
        }

        /// <summary>The corners of a lattice 30 across at 10, each twice and once more 1E-7 along X.</summary>
        private static List<GeoPoint3> OnALatticeWithCopies(Random random, int count)
        {
            var points = new List<GeoPoint3>();

            for (int i = 0; i < count; i++)
            {
                var point = new GeoPoint3(Math.Round(random.NextDouble() * 3) * 10, Math.Round(random.NextDouble() * 3) * 10, Math.Round(random.NextDouble() * 3) * 10);
                points.Add(point);
                points.Add(point);
                points.Add(new GeoPoint3(point.X + 1E-7, point.Y, point.Z));
            }

            return points;
        }

        /// <summary>The bits of every corner of every face, in order.</summary>
        private static string Bits(GeoSolid3 hull)
        {
            unchecked
            {
                long hash = 17;

                foreach (GeoFace3 face in hull.Faces)
                {
                    foreach (GeoPoint3 corner in face.Boundary.Vertices)
                    {
                        hash = hash * 31 + BitConverter.DoubleToInt64Bits(corner.X);
                        hash = hash * 31 + BitConverter.DoubleToInt64Bits(corner.Y);
                        hash = hash * 31 + BitConverter.DoubleToInt64Bits(corner.Z);
                    }

                    hash = hash * 31 + 7;
                }

                return hash.ToString("X16") + " f" + hull.Faces.Count;
            }
        }

        private static void AssertClosedAndHolding(List<GeoPoint3> points, int every, string name)
        {
            Assert.True(ConvexHull3.TryOf(points, out GeoSolid3 hull, Default), name);
            Assert.True(hull.Validate(Default).IsClosed, name);

            for (int i = 0; i < points.Count; i += every)
            {
                double outside = hull.SignedDistanceTo(points[i], Default);
                Assert.True(outside <= Default.EqualPoint, $"{name}: {points[i]} {outside:R} outside");
            }
        }

        [Fact]
        public void PointsInAPlateTwoThousandthsThick_GiveAClosedHullHoldingThem_AndAFittedBox()
        {
            var random = new Random(12345);

            for (int set = 0; set < 3; set++)
            {
                List<GeoPoint3> points = InAPlate(random, 200);
                AssertClosedAndHolding(points, 1, $"200, set {set}");
                Assert.True(GeoObb3.Fit(points, Default).Volume > 0.0);
            }

            List<GeoPoint3> more = InAPlate(new Random(12345), 500);
            AssertClosedAndHolding(more, 1, "500");
            Assert.True(GeoObb3.Fit(more, Default).Volume > 0.0);
        }

        [Fact]
        public void PointsWithinAThousandthOfACubesFaces_GiveAClosedHullHoldingThem_AndAFittedBox()
        {
            var random = new Random(12345);

            for (int set = 0; set < 5; set++)
            {
                List<GeoPoint3> points = NearACubesFaces(random, 120);
                AssertClosedAndHolding(points, 1, $"set {set}");
                Assert.True(GeoObb3.Fit(points, Default).Volume > 0.0);
            }
        }

        [Fact]
        public void TenThousandPointsInACube_AndThreeThousandOnASphere_GiveClosedHullsHoldingThem()
        {
            var random = new Random(12345);

            for (int set = 0; set < 2; set++)
            {
                AssertClosedAndHolding(InACube(random, 10000), 7, $"cube, set {set}");
            }

            AssertClosedAndHolding(OnASphere(new Random(12345), 3000), 11, "sphere");
        }

        [Fact]
        public void AHullTheFasterBuildGetsRight_IsTheOneItAlwaysGave()
        {
            // Measured on 12.2.0 and its round before: the fifth of six sets of 200 in a cube folds by more than rounding and
            // holds every point, and so do the lattices, whose copies stand 1E-7 off the corners.
            var random = new Random(12345);
            var cubes = new List<string>();

            for (int set = 0; set < 6; set++)
            {
                Assert.True(ConvexHull3.TryOf(InACube(random, 200), out GeoSolid3 hull, Default));
                cubes.Add(Bits(hull));
            }

            Assert.Equal("BC0DB0636F76CEFB f82", cubes[4]);
            Assert.Equal("746ED5379CB8CE45 f80", cubes[5]);

            random = new Random(12345);
            var lattices = new List<string>();

            for (int set = 0; set < 3; set++)
            {
                Assert.True(ConvexHull3.TryOf(OnALatticeWithCopies(random, 200), out GeoSolid3 hull, Default));
                lattices.Add(Bits(hull));
            }

            Assert.Equal(new[] { "69E58E1A196A977B f10", "79E2844269A83046 f9", "A3F78E3296FCF1A7 f10" }, lattices);
        }
    }
}
