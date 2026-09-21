using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Planar;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The plane library resolves its offsets with Clipper2; the solid library resolves the same raw loops
    /// with its own winding-number solver, compiled into these tests as well. Both must draw the same region,
    /// so each is a check on the other that does not share its arithmetic.
    /// </summary>
    public class OffsetCrossCheckTests
    {
        private const double Snap = 1e-6;

        private static GeoPolygon2 P(params double[] xy)
        {
            var points = new GeoPoint2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) { points[i] = new GeoPoint2(xy[2 * i], xy[2 * i + 1]); }
            return new GeoPolygon2(points);
        }

        /// <summary>
        /// The offset of a polygon worked out with the in-house solver alone: the region read under the even-odd
        /// rule, the raw loops the shared corner builder draws, and the positive rule over them.
        /// </summary>
        private static PathsD InHouseOffset(GeoPolygon2 polygon, double distance, OffsetOptions options)
        {
            GeoPoint2 origin = polygon[0];
            var input = new WindingRegion();
            input.AddLoop(polygon.Vertices.Select(v => new GeoPoint2(v.X - origin.X, v.Y - origin.Y)).ToList(), 0);

            var raw = new WindingRegion();
            var style = new CornerStyle(options.Join, options.MiterLimit, options.GetArcTolerance(distance));

            foreach (RegionLoop loop in input.Resolve(GeometryHelper.Internal.Planar.FillRule.EvenOdd, Snap))
            {
                List<GeoPoint2> cleaned = LoopTools.Clean(loop.Points, 1e-4, Snap);
                if (cleaned != null)
                {
                    raw.AddLoop(OffsetOutline.BuildClosedLoop(cleaned, distance, style), 0);
                }
            }

            // The same clean-up the library applies to what Clipper2 returns: points within the point tolerance
            // merged, and loops thinner than it dropped.
            var paths = new PathsD();
            foreach (RegionLoop loop in raw.Resolve(GeometryHelper.Internal.Planar.FillRule.Positive, Snap))
            {
                List<GeoPoint2> cleaned = LoopTools.Clean(loop.Points, 1e-4, Snap);
                if (cleaned == null || Math.Abs(LoopTools.SignedArea(cleaned)) <= 0.5e-4 * LoopTools.Perimeter(cleaned))
                {
                    continue;
                }

                paths.Add(new PathD(cleaned.Select(p => new PointD(p.X + origin.X, p.Y + origin.Y))));
            }

            return paths;
        }

        private static PathsD ToPaths(IEnumerable<GeoPolygon2> polygons)
        {
            var paths = new PathsD();
            foreach (GeoPolygon2 polygon in polygons)
            {
                // Holes come back wound the other way from their outer loop, so under the non-zero rule the
                // loops of an offset describe its region whichever way the polygon ran.
                paths.Add(new PathD(polygon.Vertices.Select(v => new PointD(v.X, v.Y))));
            }
            return paths;
        }

        private static double XorArea(PathsD a, PathsD b)
        {
            return Math.Abs(Clipper.Area(Clipper.Xor(a, b, Clipper2Lib.FillRule.NonZero, 8)));
        }

        private static IEnumerable<GeoPolygon2> Shapes(int seed, int count)
        {
            var rng = new Random(seed);

            for (int t = 0; t < count; t++)
            {
                var points = new List<GeoPoint2>();

                if (t % 3 == 0)
                {
                    int n = 6 + rng.Next(30);
                    for (int i = 0; i < n; i++)
                    {
                        double a = 2 * Math.PI * i / n, r = 2 + rng.NextDouble() * 9;
                        points.Add(new GeoPoint2(r * Math.Cos(a), r * Math.Sin(a)));
                    }
                }
                else if (t % 3 == 1)
                {
                    // A comb: teeth of random width standing on a base.
                    double x = 0;
                    var top = new List<GeoPoint2>();
                    for (int k = 0; k < 4 + rng.Next(5); k++)
                    {
                        double w = 0.2 + rng.NextDouble() * 1.5, g = 0.1 + rng.NextDouble() * 1.5, h = 2 + rng.NextDouble() * 5;
                        top.Add(new GeoPoint2(x, 1)); top.Add(new GeoPoint2(x, 1 + h)); top.Add(new GeoPoint2(x + w, 1 + h)); top.Add(new GeoPoint2(x + w, 1));
                        x += w + g;
                    }
                    points.Add(new GeoPoint2(0, 0)); points.Add(new GeoPoint2(x, 0)); points.Add(new GeoPoint2(x, 1));
                    top.Reverse();
                    points.AddRange(top);
                }
                else
                {
                    // Grid-aligned steps: collinear edges and equal lengths everywhere.
                    int n = 3 + rng.Next(5);
                    double y = 0;
                    points.Add(new GeoPoint2(0, 0));
                    for (int k = 0; k < n; k++) { points.Add(new GeoPoint2(k * 2 + 2, y)); y += 1 + rng.Next(3); points.Add(new GeoPoint2(k * 2 + 2, y)); }
                    points.Add(new GeoPoint2(0, y));
                }

                GeoPolygon2 polygon;
                try { polygon = new GeoPolygon2(points); } catch (ArgumentException) { continue; }
                yield return polygon;
            }
        }

        [Theory]
        [InlineData(OffsetJoin.Miter)]
        [InlineData(OffsetJoin.Chamfer)]
        [InlineData(OffsetJoin.Round)]
        public void ClipperAndTheInHouseSolver_DrawTheSameOffsets(OffsetJoin join)
        {
            var options = new OffsetOptions(join, 4.0, 0.002);
            int compared = 0;

            foreach (GeoPolygon2 shape in Shapes(17, 150))
            {
                foreach (double d in new[] { 0.3, -0.3, 1.2, -1.2, 3.0 })
                {
                    GeoPolygon2[] ours = shape.Offset(d, options);
                    double xor = XorArea(ToPaths(ours), InHouseOffset(shape, d, options));

                    Assert.True(xor < 1e-6, $"{join} offset {d} of shape {compared}: the two solvers differ by area {xor}");
                    compared++;
                }
            }

            Assert.True(compared >= 700);
        }
    }
}
