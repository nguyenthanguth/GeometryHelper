using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Work that grew with the square of its size where every point is on the hull, and no longer does, each held to a time
    /// several times what it takes now and well under what it took: the hull of points on a sphere, the box fitted round them,
    /// and triangles each in a plane of its own grouped for merging.
    /// </summary>
    public class SpeedF2Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        /// <summary>
        /// Points on a sphere of 100 round (3, -7, 11), drawn in a cube and pushed out onto it, with no sine or cosine, so that
        /// no two runtimes differ in a bit.
        /// </summary>
        private static List<GeoPoint3> OnASphere(int count)
        {
            var random = new Random(1);
            var points = new List<GeoPoint3>();

            while (points.Count < count)
            {
                double x = random.NextDouble() * 2 - 1, y = random.NextDouble() * 2 - 1, z = random.NextDouble() * 2 - 1;
                double length = Math.Sqrt(x * x + y * y + z * z);

                if (length >= 0.1 && length <= 1)
                {
                    points.Add(new GeoPoint3(100 * x / length + 3, 100 * y / length - 7, 100 * z / length + 11));
                }
            }

            return points;
        }

        [Fact]
        public void TheHullOf24000PointsOnASphere_IsBuiltInUnderSixSeconds()
        {
            // Each point was read against every face built before it, alive or gone, and the triangles were then
            // grouped by asking every group found so far: 15 seconds under .NET Framework. The faces are filed by the
            // way they face, and the groups by their normals.
            List<GeoPoint3> points = OnASphere(24000);

            var watch = Stopwatch.StartNew();
            GeoSolid3 hull = ConvexHull3.Of(points, Thousandth);
            watch.Stop();

            Assert.True(hull.IsClosed(Thousandth));
            Assert.InRange(hull.GetVolume(Thousandth), 0.99 * 4.0 / 3.0 * Math.PI * 1E6, 4.0 / 3.0 * Math.PI * 1E6);
            Assert.True(watch.Elapsed.TotalSeconds < 6, watch.Elapsed.ToString());
        }

        [Fact]
        public void ABoxFittedRound12000PointsOnASphere_HoldsThemAll()
        {
            // Each of the hull's 24 000 faces read every corner and every face of the hull for what the box on it could
            // be at least: 9.3 seconds under .NET Framework. The corners and the edges between faces are laid out in
            // trees of boxes, and each face reads only the few that can reach furthest along its normal or lie on its
            // rim. The time is not asserted: a bound under 9.3 seconds leaves too little room either side of it.
            List<GeoPoint3> points = OnASphere(12000);

            GeoObb3 box = GeoObb3.Fit(points, Thousandth);

            Assert.All(points, point => Assert.True(box.Contains(point, Thousandth)));
            Assert.InRange(box.Volume, 4.0 / 3.0 * Math.PI * 1E6, 8E6);
        }

        [Fact]
        public void FiftyThousandTrianglesEachInAPlaneOfItsOwn_AreGroupedInUnderTwoSeconds()
        {
            // Each triangle was tried against the plane of every group found before it, 1.25 billion pairs: 4.2 seconds
            // under .NET Framework.
            var random = new Random(2);
            var triangles = new List<GeoFace3>();

            while (triangles.Count < 50000)
            {
                var a = new GeoPoint3(random.NextDouble() * 1000, random.NextDouble() * 1000, random.NextDouble() * 1000);
                var b = new GeoPoint3(a.X + random.NextDouble() * 10, a.Y + random.NextDouble() * 10, a.Z - random.NextDouble() * 10);
                var c = new GeoPoint3(a.X - random.NextDouble() * 10, a.Y + random.NextDouble() * 10, a.Z + random.NextDouble() * 10);
                triangles.Add(new GeoFace3(new GeoPolygon3(new[] { a, b, c })));
            }

            var watch = Stopwatch.StartNew();
            GeoFace3[] merged = Merge3.CoplanarFaces(triangles, Thousandth);
            watch.Stop();

            Assert.Equal(triangles.Count, merged.Length);
            Assert.True(watch.Elapsed.TotalSeconds < 2, watch.Elapsed.ToString());
        }
    }
}
