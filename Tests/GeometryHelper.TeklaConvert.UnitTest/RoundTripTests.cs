using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Geometry;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.TeklaConvert;
using TSG = Tekla.Structures.Geometry3d;
using Xunit;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// A converter has one invariant worth more than any hand-written expected value: carrying a shape
    /// across and back must return what went in. Anything the round trip loses — a coordinate, an axis,
    /// the direction of a segment — shows up here without having to guess where to look.
    /// </summary>
    public class RoundTripTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        private static double Coord(Random rng) => rng.Next(-10000, 10001) / 8.0;

        [Fact]
        public void PointsSurviveTheRoundTrip()
        {
            Random rng = new Random(1234);

            for (int t = 0; t < 500; t++)
            {
                var start = new GeoPoint3(Coord(rng), Coord(rng), Coord(rng));

                GeoPoint3 back = start.ToTeklaPoint().ToGeoPoint3();
                Assert.True(back.IsEqualTo(start, Tol), $"{start} -> {back}");

                // Dropping to the plane keeps X and Y, which is the whole of what a 2D point carries.
                GeoPoint2 flat = start.ToTeklaPoint().ToGeoPoint2();
                Assert.Equal(start.X, flat.X, 9);
                Assert.Equal(start.Y, flat.Y, 9);
            }
        }

        [Fact]
        public void VectorsSurviveTheRoundTrip()
        {
            Random rng = new Random(2345);

            for (int t = 0; t < 500; t++)
            {
                var start = new GeoVector3(Coord(rng), Coord(rng), Coord(rng));

                GeoVector3 back = start.ToTeklaVector().ToGeoVector3();
                Assert.True(back.IsEqualTo(start, Tol), $"{start} -> {back}");
            }
        }

        [Fact]
        public void SegmentsSurviveTheRoundTripWithTheirDirection()
        {
            Random rng = new Random(3456);
            int cases = 0;

            for (int t = 0; t < 400; t++)
            {
                var a = new GeoPoint3(Coord(rng), Coord(rng), Coord(rng));
                var b = new GeoPoint3(Coord(rng), Coord(rng), Coord(rng));

                if (a.DistanceTo(b) < 1E-6) { continue; }

                var start = new GeoLine3(a, b);
                GeoLine3 back = start.ToTeklaLineSegment().ToGeoLine3();

                cases++;

                // A segment that comes back turned round would still measure the same, so both ends are
                // checked rather than the length.
                Assert.True(back.StartPoint.IsEqualTo(start.StartPoint, Tol), $"start {start.StartPoint} -> {back.StartPoint}");
                Assert.True(back.EndPoint.IsEqualTo(start.EndPoint, Tol), $"end {start.EndPoint} -> {back.EndPoint}");
            }

            Assert.True(cases > 300, $"only {cases} cases");
        }

        [Fact]
        public void BoxesSurviveTheRoundTrip()
        {
            Random rng = new Random(4567);

            for (int t = 0; t < 300; t++)
            {
                var lo = new GeoPoint3(Coord(rng), Coord(rng), Coord(rng));
                var hi = new GeoPoint3(lo.X + 1 + rng.Next(50), lo.Y + 1 + rng.Next(50), lo.Z + 1 + rng.Next(50));

                var start = new GeoAabb3(lo, hi);
                GeoAabb3 back = start.ToTeklaAabb().ToGeoAabb3();

                Assert.True(back.Min.IsEqualTo(start.Min, Tol), $"min {start.Min} -> {back.Min}");
                Assert.True(back.Max.IsEqualTo(start.Max, Tol), $"max {start.Max} -> {back.Max}");
            }
        }

        [Fact]
        public void CoordinateSystemsSurviveTheRoundTrip()
        {
            Random rng = new Random(5678);
            int cases = 0;

            for (int t = 0; t < 300; t++)
            {
                var origin = new GeoPoint3(Coord(rng), Coord(rng), Coord(rng));
                var x = new GeoVector3(rng.Next(-9, 10) + 0.3, rng.Next(-9, 10) + 0.7, rng.Next(-9, 10) + 0.1);
                var y = new GeoVector3(rng.Next(-9, 10) + 0.9, rng.Next(-9, 10) + 0.2, rng.Next(-9, 10) + 0.5);

                GeoCoordinateSystem3 start;
                try { start = new GeoCoordinateSystem3(origin, x, y); }
                catch (ArgumentException) { continue; }

                GeoCoordinateSystem3 back = start.ToTeklaCoordinateSystem().ToGeoCoordinateSystem3();

                cases++;

                Assert.True(back.Origin.IsEqualTo(start.Origin, Tol), $"origin {start.Origin} -> {back.Origin}");
                Assert.True(back.XAxis.IsEqualTo(start.XAxis, Tol), $"x {start.XAxis} -> {back.XAxis}");
                Assert.True(back.YAxis.IsEqualTo(start.YAxis, Tol), $"y {start.YAxis} -> {back.YAxis}");
                Assert.True(back.ZAxis.IsEqualTo(start.ZAxis, Tol), $"z {start.ZAxis} -> {back.ZAxis}");
            }

            Assert.True(cases > 200, $"only {cases} cases");
        }

        [Fact]
        public void PlanesSurviveTheRoundTrip()
        {
            Random rng = new Random(6789);
            int cases = 0;

            for (int t = 0; t < 300; t++)
            {
                var origin = new GeoPoint3(Coord(rng), Coord(rng), Coord(rng));
                var normal = new GeoVector3(rng.Next(-9, 10) + 0.3, rng.Next(-9, 10) + 0.7, rng.Next(-9, 10) + 0.1);

                GeoPlane3 start;
                try { start = new GeoPlane3(origin, normal); }
                catch (ArgumentException) { continue; }
                catch (InvalidOperationException) { continue; }

                GeoPlane3 back = start.ToTeklaPlane().ToGeoPlane3();

                cases++;

                // The origin may slide anywhere on the surface, so what must survive is the surface
                // itself: same direction, and the old origin still on the new plane.
                Assert.True(back.Normal.IsEqualTo(start.Normal, Tol), $"normal {start.Normal} -> {back.Normal}");
                Assert.True(Math.Abs(back.SignedDistanceTo(start.Origin)) <= Tol.EqualPlanar,
                            $"origin drifted off the plane by {back.SignedDistanceTo(start.Origin)}");
            }

            Assert.True(cases > 200, $"only {cases} cases");
        }

        [Fact]
        public void TransformationsSurviveTheRoundTrip()
        {
            Random rng = new Random(7890);
            int cases = 0;

            for (int t = 0; t < 200; t++)
            {
                GeoTransform3 start =
                    GeoTransform3.Translation(new GeoVector3(Coord(rng), Coord(rng), Coord(rng)))
                    * GeoTransform3.RotationAxis(
                        new GeoVector3(rng.Next(-3, 4) + 0.3, rng.Next(-3, 4) + 0.5, rng.Next(-3, 4) + 0.9),
                        rng.NextDouble() * Math.PI * 2.0);

                GeoTransform3 back = start.ToTeklaMatrix().ToGeoTransform3();

                cases++;

                // Two transformations are the same when they move every point the same way, so the check
                // is on what they do rather than on the numbers they are made of.
                for (int q = 0; q < 4; q++)
                {
                    var probe = new GeoPoint3(Coord(rng), Coord(rng), Coord(rng));
                    Assert.True(back.Transform(probe).IsEqualTo(start.Transform(probe), Tol),
                                $"{start.Transform(probe)} -> {back.Transform(probe)}");
                }
            }

            Assert.True(cases > 150, $"only {cases} cases");
        }

        [Fact]
        public void ChainsAndOutlinesSurviveTheRoundTrip()
        {
            Random rng = new Random(8901);
            int chains = 0, outlines = 0;

            for (int t = 0; t < 200; t++)
            {
                var points = new List<GeoPoint3>();
                int n = 3 + rng.Next(5);
                for (int i = 0; i < n; i++)
                {
                    double a = 2 * Math.PI * i / n;
                    double r = 500 + rng.Next(2000);
                    points.Add(new GeoPoint3(Math.Round(r * Math.Cos(a)), Math.Round(r * Math.Sin(a)), rng.Next(-500, 501)));
                }

                List<TSG.Point> carried = points.ToTeklaPoint();

                GeoPolyline3 chain;
                try { chain = new GeoPolyline3(points); }
                catch (ArgumentException) { continue; }

                GeoPolyline3 backChain = carried.ToGeoPolyline3();
                chains++;

                Assert.Equal(chain.VertexCount, backChain.VertexCount);
                for (int i = 0; i < chain.VertexCount; i++)
                {
                    Assert.True(backChain[i].IsEqualTo(chain[i], Tol), $"vertex {i}: {chain[i]} -> {backChain[i]}");
                }

                // The outline needs the points to be flat, which the ring above is only when Z is level.
                var level = new List<GeoPoint3>();
                foreach (GeoPoint3 p in points) { level.Add(new GeoPoint3(p.X, p.Y, 0)); }

                GeoPolygon3 ring;
                try { ring = new GeoPolygon3(level); }
                catch (ArgumentException) { continue; }

                GeoPolygon3 backRing = level.ToTeklaPoint().ToGeoPolygon3();
                outlines++;

                Assert.Equal(ring.VertexCount, backRing.VertexCount);
                Assert.Equal(ring.Area, backRing.Area, 6);
            }

            Assert.True(chains > 150, $"only {chains} chains");
            Assert.True(outlines > 100, $"only {outlines} outlines");
        }
    }
}
