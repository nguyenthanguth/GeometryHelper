using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face with thousands of corners is cut into triangles in time that grows with its corners, and the triangles are
    /// the ones it was cut into before, to the last bit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clipping took the first ear of the loop each time and looked at every corner of the loop to see that none stood
    /// within it, so a ring of 4 000 corners took 0.1 s, one of 16 000 1.4 s and one of 64 000 15 s, though a convex ring
    /// has no corner that can stand within an ear. Only a reflex corner can, so only those are looked at now: the same
    /// ears are found in the same order, the ring of 16 000 takes 20 ms, and one of 64 000 60 ms. The limits here are far
    /// above that, to stand a slow machine, and far below what the clipping took.
    /// </para>
    /// <para>
    /// The shapes are built with no sine or cosine, which differ in the last bit between runtimes, and the hashes are of
    /// the triangles as the clipping gave them at 89dde8f, before the change: the corners of every triangle, in order.
    /// </para>
    /// </remarks>
    public class EarClippingBigRingTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>
        /// Points round a circle by the rational parametrization, so that no two runtimes differ in a bit: a quarter at a
        /// time, each counter-clockwise from the axis, <paramref name="perQuarter"/> to a quarter.
        /// </summary>
        private static List<(double X, double Y)> Circle(int perQuarter, double radius, double centerX, double centerY)
        {
            var corners = new List<(double, double)>();

            for (int quarter = 0; quarter < 4; quarter++)
            {
                for (int k = 0; k < perQuarter; k++)
                {
                    double t = (double)k / perQuarter;
                    double x = (1.0 - t * t) / (1.0 + t * t), y = 2.0 * t / (1.0 + t * t);
                    (double X, double Y) turned = quarter == 0 ? (x, y) : quarter == 1 ? (-y, x) : quarter == 2 ? (-x, -y) : (y, -x);
                    corners.Add((centerX + radius * turned.X, centerY + radius * turned.Y));
                }
            }

            return corners;
        }

        /// <summary>
        /// A corner on the plane z = 0, or on a plane standing up 0.6 in 1 over x, shifted 5 up.
        /// </summary>
        private static GeoPoint3 At((double X, double Y) corner, bool tilted)
            => tilted ? new GeoPoint3(corner.X * 0.8, corner.Y, corner.X * 0.6 + 5.0) : new GeoPoint3(corner.X, corner.Y, 0.0);

        private static GeoPolygon3 Polygon(IEnumerable<(double X, double Y)> corners, bool tilted)
            => new GeoPolygon3(corners.Select(c => At(c, tilted)), Within);

        private static GeoFace3 Round(int perQuarter, double radius, bool tilted)
            => new GeoFace3(Polygon(Circle(perQuarter, radius, 0, 0), tilted), null, Within);

        private static GeoFace3 Star(bool tilted)
        {
            // 400 corners, every other one pulled in from 500 to 300 from the center.
            List<(double X, double Y)> corners = Circle(100, 1, 0, 0)
                .Select((p, i) => (p.X * (i % 2 == 0 ? 500.0 : 300.0), p.Y * (i % 2 == 0 ? 500.0 : 300.0))).ToList();

            return new GeoFace3(Polygon(corners, tilted), null, Within);
        }

        private static GeoFace3 Comb(bool tilted)
        {
            // 80 teeth 5 wide and 5 apart, 100 tall, on a spine 30 tall.
            const int teeth = 80;
            const double width = 5.0;
            var corners = new List<(double, double)> { (0.0, 0.0), (teeth * 2 * width - width, 0.0) };

            for (int i = teeth - 1; i >= 0; i--)
            {
                double left = i * 2 * width;
                corners.Add((left + width, 100.0));
                corners.Add((left, 100.0));

                if (i > 0)
                {
                    corners.Add((left, 30.0));
                    corners.Add((left - width, 30.0));
                }
            }

            return new GeoFace3(Polygon(corners, tilted), null, Within);
        }

        private static GeoFace3 Plate(bool tilted)
        {
            // 600 by 400 with five round holes of 24 corners, alternately above and below the middle.
            GeoPolygon3 boundary = Polygon(new[] { (-300.0, -200.0), (300.0, -200.0), (300.0, 200.0), (-300.0, 200.0) }, tilted);
            var holes = new List<GeoPolygon3>();

            for (int h = 0; h < 5; h++)
            {
                holes.Add(Polygon(Circle(6, 40, -200 + 100 * h, h % 2 == 0 ? 30 : -30), tilted));
            }

            return new GeoFace3(boundary, holes, Within);
        }

        private static GeoFace3 NearlyConvex(bool tilted)
        {
            // 500 corners round a circle of 500, every seventh a hundredth of a millimetre pulled in, so that each is reflex.
            List<(double X, double Y)> corners = Circle(125, 500, 0, 0);

            for (int i = 0; i < corners.Count; i += 7)
            {
                corners[i] = (corners[i].X * (1 - 0.02 / 500), corners[i].Y * (1 - 0.02 / 500));
            }

            return new GeoFace3(Polygon(corners, tilted), null, Within);
        }

        private static GeoFace3 RoundedRectangle(bool tilted)
        {
            // 800 by 500 with corners of radius 100 and 40 corners to each.
            var corners = new List<(double, double)>();

            for (int quarter = 0; quarter < 4; quarter++)
            {
                double centerX = quarter == 0 || quarter == 3 ? 300.0 : -300.0;
                double centerY = quarter < 2 ? 150.0 : -150.0;
                corners.AddRange(Circle(40, 100, centerX, centerY).Skip(40 * quarter).Take(40));
            }

            return new GeoFace3(Polygon(corners, tilted), null, Within);
        }

        private static GeoFace3 Shape(string name, bool tilted)
        {
            switch (name)
            {
                case "convex 700": return Round(175, 500, tilted);
                case "convex 4000": return Round(1000, 500, tilted);
                case "star 400": return Star(tilted);
                case "comb 80": return Comb(tilted);
                case "plate": return Plate(tilted);
                case "nearly convex 500": return NearlyConvex(tilted);
                default: return RoundedRectangle(tilted);
            }
        }

        private static ulong Fold(ulong hash, double value)
        {
            hash ^= unchecked((ulong)BitConverter.DoubleToInt64Bits(value));
            hash *= 1099511628211UL;
            return hash ^ (hash >> 29);
        }

        private static string Hash(GeoTriangle3[] triangles)
        {
            ulong hash = Fold(14695981039346656037UL, triangles.Length);

            foreach (GeoTriangle3 triangle in triangles)
            {
                foreach (GeoPoint3 corner in new[] { triangle.A, triangle.B, triangle.C })
                {
                    hash = Fold(Fold(Fold(hash, corner.X), corner.Y), corner.Z);
                }
            }

            return hash.ToString("x16");
        }

        [Theory]
        [InlineData("convex 700", false, 698, "a23fb820dad72df8")]
        [InlineData("convex 700", true, 698, "d5319a7260e58dee")]
        [InlineData("convex 4000", false, 3998, "79d3085a78f6f039")]
        [InlineData("convex 4000", true, 3998, "1f1a6cb38d3fbef3")]
        [InlineData("star 400", false, 398, "8722edbd4d19b1d6")]
        [InlineData("star 400", true, 398, "76fa590af5e16e9e")]
        [InlineData("comb 80", false, 318, "95f7a30de38e444d")]
        [InlineData("comb 80", true, 318, "3760eab7c38f0062")]
        [InlineData("plate", false, 132, "d1b67587d1161c65")]
        [InlineData("plate", true, 132, "107a134d4e85d6ba")]
        [InlineData("nearly convex 500", false, 498, "04f83a316f317b72")]
        [InlineData("nearly convex 500", true, 498, "6978025cd50b8411")]
        [InlineData("rounded rectangle", false, 158, "25c65d4b71fb376f")]
        [InlineData("rounded rectangle", true, 158, "9221812ecde3ee8e")]
        public void AFaceIsCutIntoTheTrianglesItWasCutIntoBefore(string shape, bool tilted, int count, string hash)
        {
            GeoTriangle3[] triangles = Shape(shape, tilted).TriangulateSurface(Within);

            Assert.Equal(count, triangles.Length);
            Assert.Equal(hash, Hash(triangles));
        }

        [Fact]
        public void AConvexRingOf16000Corners_IsFannedRoundItsLastCornerInUnderASecond()
        {
            GeoFace3 face = Round(4000, 1000, false);
            IReadOnlyList<GeoPoint3> corners = face.Boundary.Vertices;
            int n = corners.Count;

            var clock = Stopwatch.StartNew();
            GeoTriangle3[] triangles = face.TriangulateSurface(Within);
            clock.Stop();

            Assert.Equal(16000, n);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(0.25), $"{clock.Elapsed.TotalSeconds:F2} s");
            Assert.Equal(n - 2, triangles.Length);

            // The first corner is an ear every time, so the cut is the fan round the last corner.
            for (int k = 0; k <= n - 4; k++)
            {
                Assert.True(triangles[k].A.IsEqualTo(corners[n - 1], Within) && triangles[k].B.IsEqualTo(corners[k], Within) && triangles[k].C.IsEqualTo(corners[k + 1], Within), $"triangle {k}");
            }

            Assert.True(triangles[n - 3].A.IsEqualTo(corners[n - 3], Within) && triangles[n - 3].B.IsEqualTo(corners[n - 2], Within) && triangles[n - 3].C.IsEqualTo(corners[n - 1], Within));
            Assert.InRange(triangles.Sum(t => t.Area), face.Area - 1E-3, face.Area + 1E-3);
        }

        [Fact]
        public void AConvexRingOf16000Corners_IsCutInTheSamePlaneInUnderASecond()
        {
            IEnumerable<GeoPoint2> corners = Circle(4000, 1000, 0, 0).Select(c => new GeoPoint2(c.X, c.Y));
            var polygon = new GeoPolygon2(corners);
            var face = new GeoFace2(polygon);

            var clock = Stopwatch.StartNew();
            GeoTriangle2[] fromFace = face.TriangulateSurface(Within);
            TimeSpan faceTime = clock.Elapsed;
            clock.Restart();
            GeoTriangle2[] fromPolygon = polygon.TriangulateSurface(Within);
            TimeSpan polygonTime = clock.Elapsed;

            Assert.True(faceTime < TimeSpan.FromSeconds(1), $"face {faceTime.TotalSeconds:F2} s");
            Assert.True(polygonTime < TimeSpan.FromSeconds(1), $"polygon {polygonTime.TotalSeconds:F2} s");
            Assert.Equal(15998, fromFace.Length);
            Assert.Equal(15998, fromPolygon.Length);
            Assert.InRange(fromFace.Sum(t => t.Area), polygon.Area - 1E-3, polygon.Area + 1E-3);
            Assert.All(fromFace, t => Assert.False(t.IsClockwise));
        }

        [Fact]
        public void AStarOf2000Corners_IsCutIntoTrianglesOfItsOwnArea()
        {
            // Every other corner pulled in, so half the corners are reflex and each ear is looked at against them.
            List<(double X, double Y)> corners = Circle(500, 1, 0, 0)
                .Select((p, i) => (p.X * (i % 2 == 0 ? 500.0 : 300.0), p.Y * (i % 2 == 0 ? 500.0 : 300.0))).ToList();
            var face = new GeoFace3(Polygon(corners, false), null, Within);

            GeoTriangle3[] triangles = face.TriangulateSurface(Within);

            Assert.Equal(1998, triangles.Length);
            Assert.InRange(triangles.Sum(t => t.Area), face.Area - 1E-3, face.Area + 1E-3);
            Assert.All(triangles, t => Assert.True(t.GetAreaVector().DotProduct(face.Normal) > 0.0));
        }
    }
}
