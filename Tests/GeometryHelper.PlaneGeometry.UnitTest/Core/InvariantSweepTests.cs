using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Core
{
    /// <summary>
    /// Oracles for the plane library, mirroring the ones that found the bugs in the solid library:
    /// conservation across a split, the projection being the nearest point, and merging keeping length.
    /// </summary>
    public partial class InvariantSweepTests
    {

        private static readonly Tolerance Tol = Tolerance.Global;

        /// <summary>Star-shaped about its centre, so it is always simple. Snapped so vertices collide.</summary>
        private static GeoPoint2[] Star(Random r, int n, bool snap, double dx = 0, double dy = 0)
        {
            var pts = new GeoPoint2[n];
            for (int i = 0; i < n; i++)
            {
                double a = 2 * Math.PI * i / n, rad = 2 + r.NextDouble() * 6;
                double x = rad * Math.Cos(a) + dx, y = rad * Math.Sin(a) + dy;
                if (snap) { x = Math.Round(x); y = Math.Round(y); }
                pts[i] = new GeoPoint2(x, y);
            }
            return pts;
        }

        private static double Total(GeoPolyline2[] p) { double s = 0; foreach (var c in p) { s += c.Length; } return s; }
        private static double Total(GeoLine2[] p) { double s = 0; foreach (var c in p) { s += c.Length; } return s; }

        [Fact]
        public void SplittingACurveByPolygonsConservesLength()
        {
            var rng = new Random(2026);
            int chainCases = 0, chainBad = 0, lineCases = 0, lineBad = 0, split = 0;
            double wc = 0, wl = 0;

            for (int t = 0; t < 400; t++)
            {
                bool snap = t % 2 == 0;

                var cutters = new List<GeoPolygon2>();
                for (int k = 0; k < 1 + rng.Next(3); k++)
                {
                    try { cutters.Add(new GeoPolygon2(Star(rng, 4 + rng.Next(4), snap, rng.Next(-6, 7), rng.Next(-6, 7)))); }
                    catch (ArgumentException) { }
                }
                if (cutters.Count == 0) { continue; }

                var chain = new GeoPolyline2(
                    new GeoPoint2(-20, rng.Next(-8, 9)),
                    new GeoPoint2(0, rng.Next(-8, 9)),
                    new GeoPoint2(20, rng.Next(-8, 9)));

                if (chain.TrySplitBy(cutters.ToArray(), out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tol)) { split++; }

                chainCases++;
                double d = Math.Abs(Total(inside) + Total(outside) - chain.Length);
                if (d > 1e-6) { chainBad++; wc = Math.Max(wc, d); }

                var seg = new GeoLine2(new GeoPoint2(-20, rng.Next(-8, 9)), new GeoPoint2(20, rng.Next(-8, 9)));
                seg.TrySplitBy(cutters.ToArray(), out GeoLine2[] segIn, out GeoLine2[] segOut, Tol);

                lineCases++;
                double e = Math.Abs(Total(segIn) + Total(segOut) - seg.Length);
                if (e > 1e-6) { lineBad++; wl = Math.Max(wl, e); }
            }

            Assert.True(split > 150, $"only {split} chains were really cut");
            Assert.Equal(0, chainBad);
            Assert.Equal(0, lineBad);
        }

        [Fact]
        public void SplittingByCuttersAndByPointsConservesLength()
        {
            var rng = new Random(5150);
            int cases = 0, byLines = 0, byPoints = 0, byChains = 0;
            double w1 = 0, w2 = 0, w3 = 0;

            for (int t = 0; t < 400; t++)
            {
                var chain = new GeoPolyline2(
                    new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)),
                    new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)),
                    new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)));

                if (chain.Length < 1e-3) { continue; }
                cases++;

                var cutters = new List<GeoLine2>();
                for (int k = 0; k < 3; k++)
                {
                    cutters.Add(new GeoLine2(new GeoPoint2(rng.Next(-9, 10), -20), new GeoPoint2(rng.Next(-9, 10), 20)));
                }

                chain.TrySplitBy(cutters.ToArray(), out GeoPolyline2[] a);
                double d1 = Math.Abs(Total(a) - chain.Length);
                if (d1 > 1e-6) { byLines++; w1 = Math.Max(w1, d1); }

                var points = new List<GeoPoint2>();
                for (int k = 1; k < 4; k++) { points.Add(Parametrization2.GetPointAtDistance(chain, chain.Length * k / 4.0)); }

                chain.TrySplitBy(points.ToArray(), out GeoPolyline2[] b);
                double d2 = Math.Abs(Total(b) - chain.Length);
                if (d2 > 1e-6) { byPoints++; w2 = Math.Max(w2, d2); }

                var knives = new List<GeoPolyline2>
                {
                    new GeoPolyline2(new GeoPoint2(rng.Next(-9, 10), -20), new GeoPoint2(rng.Next(-9, 10), 20))
                };

                chain.TrySplitBy(knives.ToArray(), out GeoPolyline2[] c);
                double d3 = Math.Abs(Total(c) - chain.Length);
                if (d3 > 1e-6) { byChains++; w3 = Math.Max(w3, d3); }
            }

            Assert.True(cases > 200, $"only {cases} cases");
            Assert.Equal(0, byLines);
            Assert.Equal(0, byPoints);
            Assert.Equal(0, byChains);
        }

        [Fact]
        public void ProjectionLandsOnTheNearestPoint()
        {
            var rng = new Random(4040);
            int cases = 0, lineBad = 0, polyBad = 0, chainBad = 0, circleBad = 0, rectBad = 0, segBad = 0;
            int insidePolygon = 0, insideCircle = 0, insideRect = 0;
            double w = 0;

            for (int t = 0; t < 300; t++)
            {
                bool snap = t % 2 == 0;
                GeoPoint2 P() => new GeoPoint2(
                    rng.Next(-12, 13) + (snap ? 0 : rng.NextDouble()),
                    rng.Next(-12, 13) + (snap ? 0 : rng.NextDouble()));

                var l = new GeoLine2(P(), P());
                if (l.Length < 1e-6) { continue; }

                GeoPolygon2 pg;
                GeoPolyline2 pc;
                try
                {
                    GeoPoint2[] prof = Star(rng, 4 + rng.Next(5), snap);
                    pg = new GeoPolygon2(prof);
                    pc = new GeoPolyline2(prof);
                }
                catch (ArgumentException) { continue; }

                var ci = new GeoCircle2(P(), 1 + rng.Next(1, 6));
                var rc = new GeoRectangle2(P(), 2 + rng.Next(1, 6), 2 + rng.Next(1, 6));

                for (int q = 0; q < 4; q++)
                {
                    GeoPoint2 x = P();
                    cases++;

                    if (Off(Projection2.ProjectToLine(l, x, Tol), x, Distance2.DistanceTo(l, x), ref w)) { lineBad++; }
                    if (Off(Projection2.ProjectToPolyline(pc, x, Tol), x, Distance2.DistanceTo(pc, x), ref w)) { chainBad++; }

                    // Distance2 reads a polygon, a circle and a rectangle as FILLED regions, so a point
                    // inside one is at distance zero, while Projection2 reports the nearest point on the
                    // boundary. The two deliberately disagree there, so the nearness check only applies
                    // outside; inside, what must hold is that the projection sits on the boundary.
                    GeoPoint2 onPolygon = Projection2.ProjectToPolygon(pg, x, Tol);
                    if (Containment2.Contains(pg, x, Tol))
                    {
                        insidePolygon++;
                        if (!OnBoundary(pg, onPolygon)) { polyBad++; }
                    }
                    else if (Off(onPolygon, x, Distance2.DistanceTo(pg, x), ref w)) { polyBad++; }

                    GeoPoint2 onCircle = Projection2.ProjectToCircle(ci, x, Tol);
                    if (Math.Abs(onCircle.DistanceTo(ci.Center) - ci.Radius) > 1E-9) { circleBad++; }
                    else if (ci.Center.DistanceTo(x) > ci.Radius
                             && Off(onCircle, x, Distance2.DistanceTo(ci, x), ref w)) { circleBad++; }
                    else if (ci.Center.DistanceTo(x) <= ci.Radius) { insideCircle++; }

                    GeoPoint2 onRect = Projection2.ProjectToRectangle(rc, x);
                    if (Containment2.Contains(rc, x, Tol))
                    {
                        insideRect++;
                    }
                    else if (Off(onRect, x, Distance2.DistanceTo(rc, x), ref w)) { rectBad++; }
                }

                var l2 = new GeoLine2(P(), P());
                if (l2.Length >= 1e-6)
                {
                    GeoLine2 bridge = Projection2.GetClosestSegment(l, l2, Tol);
                    if (Math.Abs(bridge.Length - Distance2.DistanceTo(l, l2, Tol)) > 1e-9) { segBad++; }
                }
            }

            Assert.True(cases > 500, $"only {cases} cases");
            Assert.Equal(0, lineBad);
            Assert.Equal(0, polyBad);
            Assert.Equal(0, chainBad);
            Assert.Equal(0, circleBad);
            Assert.Equal(0, rectBad);
            Assert.Equal(0, segBad);
            Assert.True(insidePolygon > 20 && insideCircle > 20 && insideRect > 5,
                $"interior coverage: polygon={insidePolygon} circle={insideCircle} rect={insideRect}");
        }

        private static bool OnBoundary(GeoPolygon2 poly, GeoPoint2 p)
        {
            for (int i = 0; i < poly.EdgeCount; i++)
            {
                if (Containment2.IsPointOn(poly.GetEdgeAt(i), p, Tol)) { return true; }
            }
            return false;
        }

        private static bool Off(GeoPoint2 projected, GeoPoint2 source, double reported, ref double worst)
        {
            double d = Math.Abs(projected.DistanceTo(source) - reported);
            if (d > 1e-9) { worst = Math.Max(worst, d); return true; }
            return false;
        }

        [Fact]
        public void MergeKeepsLengthAndAgreesWithItsReferenceImplementation()
        {
            var rng = new Random(7070);
            int cases = 0, lengthBad = 0, disagree = 0, idemBad = 0;
            double worst = 0;

            for (int t = 0; t < 300; t++)
            {
                var vertices = new List<GeoPoint2>();
                int n = 3 + rng.Next(6);
                for (int i = 0; i < n; i++) { vertices.Add(new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10))); }

                GeoPolyline2 whole;
                try { whole = new GeoPolyline2(vertices); } catch (ArgumentException) { continue; }

                var pieces = new List<GeoLine2>();
                for (int i = 0; i < whole.VertexCount - 1; i++)
                {
                    var e = new GeoLine2(whole[i], whole[i + 1]);
                    if (e.Length < 1e-9) { continue; }
                    pieces.Add(rng.Next(2) == 0 ? e : new GeoLine2(e.EndPoint, e.StartPoint));
                }
                if (pieces.Count == 0) { continue; }

                for (int i = pieces.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    GeoLine2 tmp = pieces[i]; pieces[i] = pieces[j]; pieces[j] = tmp;
                }

                GeoPolyline2[] joined = Merge2.Join(pieces, Tol);
                GeoPolyline2[] backup = Merge2.JoinBackup(pieces, Tol);

                cases++;

                double expected = 0.0;
                foreach (var p in pieces) { expected += p.Length; }

                double d = Math.Abs(Total(joined) - expected);
                if (d > 1e-6) { lengthBad++; worst = Math.Max(worst, d); }

                // The two implementations are kept side by side precisely so they can be compared.
                if (Math.Abs(Total(joined) - Total(backup)) > 1e-6) { disagree++; }

                var again = new List<GeoLine2>();
                foreach (var c in joined)
                {
                    for (int i = 0; i < c.VertexCount - 1; i++)
                    {
                        var e = new GeoLine2(c[i], c[i + 1]);
                        if (e.Length >= 1e-9) { again.Add(e); }
                    }
                }
                if (Math.Abs(Total(Merge2.Join(again, Tol)) - Total(joined)) > 1e-6) { idemBad++; }
            }

            Assert.True(cases > 100, $"only {cases} cases");
            Assert.Equal(0, lengthBad);
            Assert.Equal(0, disagree);
            Assert.Equal(0, idemBad);
        }

        [Fact]
        public void ParametrizationRoundTrips()
        {
            var rng = new Random(3030);
            int lineBad = 0, chainBad = 0, polyBad = 0, circleBad = 0, rectBad = 0;
            double w = 0;

            for (int t = 0; t < 300; t++)
            {
                var line = new GeoLine2(new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)), new GeoPoint2(rng.Next(-9, 10), rng.Next(-9, 10)));
                if (line.Length < 1e-6) { continue; }

                GeoPoint2[] prof = Star(rng, 5 + rng.Next(4), true);
                GeoPolyline2 chain;
                GeoPolygon2 poly;
                try { chain = new GeoPolyline2(prof); poly = new GeoPolygon2(prof); }
                catch (ArgumentException) { continue; }

                var circle = new GeoCircle2(new GeoPoint2(rng.Next(-5, 6), rng.Next(-5, 6)), 1 + rng.Next(1, 6));
                var rect = new GeoRectangle2(new GeoPoint2(rng.Next(-5, 6), rng.Next(-5, 6)), 2 + rng.Next(1, 6), 2 + rng.Next(1, 6));

                for (int k = 1; k < 8; k++)
                {
                    double f = k / 8.0;

                    if (Trip(Parametrization2.GetDistanceAtPoint(line, Parametrization2.GetPointAtDistance(line, line.Length * f)), line.Length * f, ref w)) { lineBad++; }
                    if (Trip(Parametrization2.GetDistanceAtPoint(chain, Parametrization2.GetPointAtDistance(chain, chain.Length * f)), chain.Length * f, ref w)) { chainBad++; }
                    if (Trip(Parametrization2.GetDistanceAtPoint(poly, Parametrization2.GetPointAtDistance(poly, poly.Length * f)), poly.Length * f, ref w)) { polyBad++; }
                    if (Trip(Parametrization2.GetDistanceAtPoint(circle, Parametrization2.GetPointAtDistance(circle, circle.Circumference * f)), circle.Circumference * f, ref w)) { circleBad++; }
                    if (Trip(Parametrization2.GetDistanceAtPoint(rect, Parametrization2.GetPointAtDistance(rect, rect.Length * f)), rect.Length * f, ref w)) { rectBad++; }
                }
            }

            Assert.Equal(0, lineBad);
            Assert.Equal(0, chainBad);
            Assert.Equal(0, polyBad);
            Assert.Equal(0, circleBad);
            Assert.Equal(0, rectBad);
        }

        private static bool Trip(double back, double expected, ref double worst)
        {
            double d = Math.Abs(back - expected);
            if (d > 1e-6) { worst = Math.Max(worst, d); return true; }
            return false;
        }
    }
}
