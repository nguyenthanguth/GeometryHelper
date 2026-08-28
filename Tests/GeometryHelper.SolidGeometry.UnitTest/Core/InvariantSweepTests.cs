using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.SolidGeometry.Spatial;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    public partial class InvariantSweepTests
    {

        private static readonly Tolerance Tol = Tolerance.Global;

        private static GeoPoint3[] Star(Random r, int n, bool snap)
        {
            var pts = new GeoPoint3[n];
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n, rad = 2 + r.NextDouble() * 6;
                double x = rad * Math.Cos(a), y = rad * Math.Sin(a);
                if (snap) { x = Math.Round(x); y = Math.Round(y); }
                pts[i] = new GeoPoint3(x, y, 0);
            }
            return pts;
        }

        private static GeoSolid3 Prism(GeoPoint3[] p, double h)
        {
            var faces = new List<GeoFace3>();
            var top = new GeoPoint3[p.Length]; var bot = new GeoPoint3[p.Length];
            for (int i = 0; i < p.Length; i++) { top[i] = new GeoPoint3(p[i].X, p[i].Y, p[i].Z + h); bot[i] = p[p.Length - 1 - i]; }
            faces.Add(new GeoFace3(new GeoPolygon3(bot)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));
            for (int i = 0; i < p.Length; i++) { int j = (i + 1) % p.Length; faces.Add(new GeoFace3(new GeoPolygon3(p[i], p[j], top[j], top[i]))); }
            return new GeoSolid3(faces);
        }

        // ------------------------------------------------------------------ BVH

        [Fact]
        public void BvhAgreesWithTheNaiveScan()
        {
            var rng = new Random(4242);
            int cases = 0, distBad = 0, closestBad = 0, rayBad = 0, rayCases = 0, rayWithHits = 0, totalHits = 0;
            double worstDist = 0;

            for (int t = 0; t < 120; t++)
            {
                bool snap = t % 2 == 0;
                GeoSolid3 body = Prism(Star(rng, 4 + rng.Next(6), snap), snap ? 5 : 4.3);
                GeoBvh3 tree = GeoBvh3.FromSolid(body);
                GeoTriangle3[] mesh = body.Triangulate();

                for (int q = 0; q < 6; q++)
                {
                    var probe = new GeoPoint3(
                        rng.Next(-12, 13) + (snap ? 0 : rng.NextDouble()),
                        rng.Next(-12, 13) + (snap ? 0 : rng.NextDouble()),
                        rng.Next(-4, 10) + (snap ? 0 : rng.NextDouble()));

                    double brute = double.MaxValue;
                    foreach (var tri in mesh) { brute = Math.Min(brute, Distance3.DistanceTo(tri, probe)); }

                    double viaTree = tree.DistanceTo(probe);
                    cases++;

                    double d = Math.Abs(viaTree - brute);
                    if (d > 1e-9) { distBad++; worstDist = Math.Max(worstDist, d); }

                    // The closest point it names must really be that far away.
                    if (Math.Abs(tree.GetClosestPoint(probe).DistanceTo(probe) - viaTree) > 1e-9) { closestBad++; }
                }

                for (int q = 0; q < 8; q++)
                {
                    // Half the rays are aimed through the body so that hits are common, half are
                    // thrown at random so that misses and grazes are covered too.
                    var origin = new GeoPoint3(rng.Next(-14, 15), rng.Next(-14, 15), rng.Next(-6, 12));
                    GeoVector3 dir;

                    if (q % 2 == 0)
                    {
                        GeoPoint3 aim = body.Centroid;
                        dir = origin.GetVectorTo(new GeoPoint3(
                            aim.X + rng.NextDouble() * 4 - 2,
                            aim.Y + rng.NextDouble() * 4 - 2,
                            aim.Z + rng.NextDouble() * 4 - 2));
                    }
                    else
                    {
                        dir = new GeoVector3(rng.Next(-3, 4) + 0.37, rng.Next(-3, 4) + 0.11, rng.Next(-3, 4) - 0.23);
                    }

                    if (dir.IsZeroLength(Tol)) { continue; }
                    var ray = new GeoRay3(origin, dir);

                    var brute = new List<GeoPoint3>();
                    foreach (var tri in mesh)
                    {
                        if (Intersection3.TryIntersectWith(ray, tri, out GeoPoint3 hit, Tol)) { brute.Add(hit); }
                    }

                    GeoPoint3[] viaTree = tree.GetIntersections(ray, Tol);
                    rayCases++;
                    if (brute.Count > 0) { rayWithHits++; totalHits += brute.Count; }

                    // Every hit the scan finds must be among the tree's hits, and vice versa.
                    if (!SameSet(brute, viaTree)) { rayBad++; }
                }
            }

            Assert.True(cases > 400, $"only {cases} cases");
            Assert.Equal(0, distBad);
            Assert.Equal(0, closestBad);
            Assert.True(rayWithHits > 200, $"only {rayWithHits} rays hit anything");
            Assert.Equal(0, rayBad);
        }

        private static bool SameSet(List<GeoPoint3> a, GeoPoint3[] b)
        {
            foreach (var p in a) { if (!Has(b, p)) { return false; } }
            foreach (var p in b) { if (!Has(a, p)) { return false; } }
            return true;
        }

        private static bool Has(IEnumerable<GeoPoint3> set, GeoPoint3 p)
        {
            foreach (var q in set) { if (q.IsEqualTo(p, Tol)) { return true; } }
            return false;
        }

        [Fact]
        public void BvhPairwiseAgreesWithTheNaiveScan()
        {
            var rng = new Random(99);
            int cases = 0, collideBad = 0, distBad = 0, hits = 0, apart = 0;
            double worst = 0;

            for (int t = 0; t < 60; t++)
            {
                GeoSolid3 a = Prism(Star(rng, 4 + rng.Next(4), true), 4);
                double dx = rng.Next(-14, 15), dy = rng.Next(-14, 15);
                var moved = new List<GeoPoint3>();
                foreach (var p in Star(rng, 4 + rng.Next(4), true)) { moved.Add(new GeoPoint3(p.X + dx, p.Y + dy, 1)); }
                GeoSolid3 b;
                try { b = Prism(moved.ToArray(), 4); } catch (ArgumentException) { continue; }

                GeoTriangle3[] ma = a.Triangulate(), mb = b.Triangulate();
                GeoBvh3 ta = new GeoBvh3(ma), tb = new GeoBvh3(mb);

                bool bruteHit = false;
                double bruteDist = double.MaxValue;
                foreach (var x in ma)
                {
                    foreach (var y in mb)
                    {
                        if (Collision3.CollidesWith(x, y, Tol)) { bruteHit = true; }
                        bruteDist = Math.Min(bruteDist, Distance3.DistanceTo(x, y, Tol));
                    }
                }

                cases++;
                if (bruteHit) { hits++; } else { apart++; }
                if (ta.CollidesWith(tb, Tol) != bruteHit) { collideBad++; }

                double viaTree = ta.DistanceTo(tb, Tol);
                double d = Math.Abs(viaTree - bruteDist);
                if (d > 1e-9) { distBad++; worst = Math.Max(worst, d); }
            }

            Assert.True(hits > 5 && apart > 5, $"colliding={hits} apart={apart}");
            Assert.Equal(0, collideBad);
            Assert.Equal(0, distBad);
        }

        // -------------------------------------------------- Parametrization3

        [Fact]
        public void ParametrizationRoundTrips()
        {
            var rng = new Random(31415);
            int lineBad = 0, chainBad = 0, polyBad = 0, circleBad = 0;
            double wl = 0, wc = 0, wp = 0, wk = 0;

            for (int t = 0; t < 200; t++)
            {
                var line = new GeoLine3(
                    new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)),
                    new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)));
                if (line.Length < 1e-6) { continue; }

                for (int k = 1; k < 8; k++)
                {
                    double d = line.Length * k / 8.0;
                    double back = Parametrization3.GetDistanceAtPoint(line, Parametrization3.GetPointAtDistance(line, d));
                    if (Math.Abs(back - d) > 1e-9) { lineBad++; wl = Math.Max(wl, Math.Abs(back - d)); }
                }

                GeoPoint3[] prof = Star(rng, 5 + rng.Next(5), true);

                GeoPolyline3 chain;
                try { chain = new GeoPolyline3(prof); } catch (ArgumentException) { continue; }

                for (int k = 1; k < 8; k++)
                {
                    double d = chain.Length * k / 8.0;
                    double back = Parametrization3.GetDistanceAtPoint(chain, Parametrization3.GetPointAtDistance(chain, d), Tol);
                    if (Math.Abs(back - d) > 1e-6) { chainBad++; wc = Math.Max(wc, Math.Abs(back - d)); }
                }

                GeoPolygon3 poly;
                try { poly = new GeoPolygon3(prof); } catch (ArgumentException) { continue; }

                for (int k = 1; k < 8; k++)
                {
                    double d = poly.Length * k / 8.0;
                    double back = Parametrization3.GetDistanceAtPoint(poly, Parametrization3.GetPointAtDistance(poly, d), Tol);
                    if (Math.Abs(back - d) > 1e-6) { polyBad++; wp = Math.Max(wp, Math.Abs(back - d)); }
                }

                var circle = new GeoCircle3(new GeoPoint3(rng.Next(-5, 6), rng.Next(-5, 6), 0), GeoVector3.ZAxis, 1 + rng.Next(1, 6));
                for (int k = 1; k < 8; k++)
                {
                    double d = circle.Length * k / 8.0;
                    double back = Parametrization3.GetDistanceAtPoint(circle, Parametrization3.GetPointAtDistance(circle, d), Tol);
                    if (Math.Abs(back - d) > 1e-6) { circleBad++; wk = Math.Max(wk, Math.Abs(back - d)); }
                }
            }

            Assert.Equal(0, lineBad);
            Assert.Equal(0, chainBad);
            Assert.Equal(0, polyBad);
            Assert.Equal(0, circleBad);
        }

        [Fact]
        public void ParametrizationEndsAndMonotonicity()
        {
            var rng = new Random(2718);
            int endBad = 0, monoBad = 0;

            for (int t = 0; t < 150; t++)
            {
                GeoPoint3[] prof = Star(rng, 5 + rng.Next(5), true);
                GeoPolyline3 chain;
                try { chain = new GeoPolyline3(prof); } catch (ArgumentException) { continue; }

                if (!Parametrization3.GetPointAtParameter(chain, 0.0).IsEqualTo(chain.StartPoint, Tol)) { endBad++; }
                if (!Parametrization3.GetPointAtParameter(chain, 1.0).IsEqualTo(chain.EndPoint, Tol)) { endBad++; }

                double previous = -1.0;
                for (int k = 0; k <= 20; k++)
                {
                    double d = Parametrization3.GetDistanceAtParameter(chain, k / 20.0);
                    if (d < previous - 1e-9) { monoBad++; }
                    previous = d;
                }
            }

            Assert.Equal(0, endBad);
            Assert.Equal(0, monoBad);
        }

        // ------------------------------------------ Region by a closed volume

        [Fact]
        public void RegionCutByAVolumeConservesArea()
        {
            var rng = new Random(1234);
            int cases = 0, bad = 0, refused = 0, reallySplit = 0, bothSides = 0;
            double worst = 0;

            for (int t = 0; t < 250; t++)
            {
                bool snap = t % 2 == 0;
                GeoPolygon3 plate;
                try
                {
                    var flat = Star(rng, 4 + rng.Next(5), snap);
                    for (int i = 0; i < flat.Length; i++) { flat[i] = new GeoPoint3(flat[i].X, flat[i].Y, 2); }
                    plate = new GeoPolygon3(flat);
                }
                catch (ArgumentException) { refused++; continue; }

                double dx = rng.Next(-6, 7), dy = rng.Next(-6, 7);
                var prof = Star(rng, 4 + rng.Next(4), snap);
                for (int i = 0; i < prof.Length; i++) { prof[i] = new GeoPoint3(prof[i].X + dx, prof[i].Y + dy, 0); }

                GeoSolid3 body;
                try { body = Prism(prof, 4); } catch (ArgumentException) { refused++; continue; }

                bool ok = Splition3.TrySplitBy(plate, body, out GeoPolygon3[] inside, out GeoPolygon3[] outside, Tol);
                if (ok) { reallySplit++; }
                if (inside.Length > 0 && outside.Length > 0) { bothSides++; }

                double a = 0; foreach (var p in inside) { a += p.Area; }
                double b = 0; foreach (var p in outside) { b += p.Area; }

                cases++;
                double drift = Math.Abs(a + b - plate.Area);
                if (drift > 1e-6) { bad++; worst = Math.Max(worst, drift); }
            }

            Assert.True(reallySplit > 150, $"only {reallySplit} plates were really cut");
            Assert.Equal(0, bad);
        }
    }
}
