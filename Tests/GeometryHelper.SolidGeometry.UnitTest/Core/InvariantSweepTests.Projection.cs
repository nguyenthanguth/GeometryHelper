using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Oracles for Projection3 and Merge3.
    /// The projection of a point must BE the nearest point, so its distance has to agree with Distance3
    /// exactly; and projecting twice must change nothing.
    /// </summary>
    public partial class InvariantSweepTests
    {

        private sealed class Tally
        {
            public string Name;
            public int Cases, Nearest, Idempotent, OnShape;
            public double Worst;

            public Tally(string name) { Name = name; }

            public void Nearness(double projected, double reported)
            {
                Cases++;
                double d = Math.Abs(projected - reported);
                if (d > 1E-9) { Nearest++; Worst = Math.Max(Worst, d); }
            }

            public override string ToString() =>
                $"{Name,-26} cases={Cases,5} notNearest={Nearest,4} notIdempotent={Idempotent,4} offShape={OnShape,4} worst={Worst:0.###########}";
        }

        [Fact]
        public void ProjectionLandsOnTheNearestPoint()
        {
            var rng = new Random(6060);

            var line = new Tally("ProjectToLine");
            var plane = new Tally("ProjectToPlane");
            var tri = new Tally("ProjectToTriangle");
            var poly = new Tally("ProjectToPolygon");
            var bound = new Tally("ProjectToPolygonBoundary");
            var chain = new Tally("ProjectToPolyline");
            var circle = new Tally("ProjectToCircle");
            var disc = new Tally("ProjectToDisc");
            var obb = new Tally("ProjectToObb");
            var solid = new Tally("ProjectToSolid");
            var seg = new Tally("GetClosestSegment");
            int solidOutside = 0;

            for (int t = 0; t < 150; t++)
            {
                bool snap = t % 2 == 0;
                GeoPoint3 P() => new GeoPoint3(
                    rng.Next(-12, 13) + (snap ? 0 : rng.NextDouble()),
                    rng.Next(-12, 13) + (snap ? 0 : rng.NextDouble()),
                    rng.Next(-8, 13) + (snap ? 0 : rng.NextDouble()));

                var l = new GeoLine3(P(), P());
                if (l.Length < 1E-6) { continue; }

                var pl = new GeoPlane3(P(), new GeoVector3(rng.Next(-3, 4) + 0.3, rng.Next(-3, 4) + 0.7, rng.Next(-3, 4) + 0.1));

                GeoPoint3[] prof = Star(rng, 4 + rng.Next(5), snap);
                GeoPolygon3 pg;
                GeoPolyline3 pc;
                GeoSolid3 body;
                try
                {
                    pg = new GeoPolygon3(prof);
                    pc = new GeoPolyline3(prof);
                    body = Prism(prof, snap ? 5 : 4.3);
                }
                catch (ArgumentException) { continue; }

                var tr = new GeoTriangle3(prof[0], prof[1], prof[2]);
                var ci = new GeoCircle3(P(), new GeoVector3(0, 0, 1), 1 + rng.Next(1, 6));
                var bx = new GeoObb3(P(), 2 + rng.Next(1, 6), 2 + rng.Next(1, 6), 2 + rng.Next(1, 6));

                for (int q = 0; q < 5; q++)
                {
                    GeoPoint3 x = P();

                    Check(line, Projection3.ProjectToLine(l, x, Tol), x, Distance3.DistanceTo(l, x, Tol),
                          p => Projection3.ProjectToLine(l, p, Tol), p => Containment3.IsPointOn(l, p, Tol));

                    Check(plane, Projection3.ProjectToPlane(pl, x), x, Distance3.DistanceTo(pl, x),
                          p => Projection3.ProjectToPlane(pl, p), p => Math.Abs(pl.SignedDistanceTo(p)) <= Tol.EqualPlanar);

                    Check(tri, Projection3.ProjectToTriangle(tr, x), x, Distance3.DistanceTo(tr, x),
                          p => Projection3.ProjectToTriangle(tr, p), null);

                    Check(poly, Projection3.ProjectToPolygon(pg, x, Tol), x, Distance3.DistanceTo(pg, x, Tol),
                          p => Projection3.ProjectToPolygon(pg, p, Tol), null);

                    Check(chain, Projection3.ProjectToPolyline(pc, x, Tol), x, Distance3.DistanceTo(pc, x, Tol),
                          p => Projection3.ProjectToPolyline(pc, p, Tol), p => Containment3.IsPointOn(pc, p, Tol));

                    // Distance to the rim, worked out independently: how far the point is off the plane,
                    // and how far its shadow is from the rim within the plane.
                    GeoPoint3 flat = Projection3.ProjectToPlane(ci.GetPlane(), x);
                    double outOfPlane = flat.DistanceTo(x);
                    double inPlane = Math.Abs(flat.DistanceTo(ci.Center) - ci.Radius);
                    double toRim = Math.Sqrt(outOfPlane * outOfPlane + inPlane * inPlane);

                    Check(circle, Projection3.ProjectToCircle(ci, x, Tol), x, toRim,
                          p => Projection3.ProjectToCircle(ci, p, Tol), null);

                    Check(disc, Projection3.ProjectToDisc(ci, x, Tol), x, Distance3.DistanceTo(ci, x, Tol),
                          p => Projection3.ProjectToDisc(ci, p, Tol), null);

                    Check(obb, Projection3.ProjectToObb(bx, x), x, Distance3.DistanceTo(bx, x),
                          p => Projection3.ProjectToObb(bx, p), null);

                    bool outsideBody = !Containment3.Contains(body, x, Tol);
                    Check(solid, Projection3.ProjectToSolid(body, x, Tol), x,
                          outsideBody ? Distance3.DistanceTo(body, x, Tol) : double.NaN,
                          p => Projection3.ProjectToSolid(body, p, Tol), null);
                    if (outsideBody) { solidOutside++; }

                    // The boundary projection is nearest among the edges, which is what its own distance
                    // has no direct counterpart for; check it lands on an edge instead.
                    GeoPoint3 onEdge = Projection3.ProjectToPolygonBoundary(pg, x, Tol);
                    bound.Cases++;
                    bool sits = false;
                    for (int e = 0; e < pg.EdgeCount; e++)
                    {
                        if (Containment3.IsPointOn(pg.GetEdgeAt(e), onEdge, Tol)) { sits = true; break; }
                    }
                    if (!sits) { bound.OnShape++; }
                    if (!Projection3.ProjectToPolygonBoundary(pg, onEdge, Tol).IsEqualTo(onEdge, Tol)) { bound.Idempotent++; }
                }

                // The bridge between two segments must be exactly as long as the distance between them.
                var l2 = new GeoLine3(P(), P());
                if (l2.Length >= 1E-6)
                {
                    GeoLine3 bridge = Projection3.GetClosestSegment(l, l2, Tol);
                    seg.Nearness(bridge.Length, Distance3.DistanceTo(l, l2, Tol));
                    if (!Containment3.IsPointOn(l, bridge.StartPoint, Tol)) { seg.OnShape++; }
                    if (!Containment3.IsPointOn(l2, bridge.EndPoint, Tol)) { seg.OnShape++; }
                }
            }
            Assert.True(solidOutside > 400, $"only {solidOutside} points fell outside the body");

            foreach (var tally in new[] { line, plane, tri, poly, bound, chain, circle, disc, obb, solid, seg })
            {
                Assert.True(tally.Cases > 100, $"{tally.Name}: only {tally.Cases} cases");
                Assert.Equal(0, tally.Nearest);
                Assert.Equal(0, tally.Idempotent);
                Assert.Equal(0, tally.OnShape);
            }
        }

        private static void Check(Tally tally, GeoPoint3 projected, GeoPoint3 source, double reported,
                                  Func<GeoPoint3, GeoPoint3> again, Func<GeoPoint3, bool> onShape)
        {
            if (double.IsNaN(reported))
            {
                tally.Cases++;
            }
            else
            {
                tally.Nearness(projected.DistanceTo(source), reported);
            }

            // Projecting what is already projected must change nothing.
            if (!again(projected).IsEqualTo(projected, Tol)) { tally.Idempotent++; }

            if (onShape != null && !onShape(projected)) { tally.OnShape++; }
        }

        [Fact]
        public void MergeKeepsEveryMillimetreOfChain()
        {
            var rng = new Random(8080);
            int cases = 0, lengthBad = 0, idemBad = 0;
            double worst = 0;

            for (int t = 0; t < 200; t++)
            {
                // A chain cut into pieces at random, shuffled, and some reversed. Joining it back must
                // return the same total length however the pieces arrive.
                var vertices = new List<GeoPoint3>();
                int n = 3 + rng.Next(6);
                for (int i = 0; i < n; i++)
                {
                    vertices.Add(new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)));
                }

                GeoPolyline3 whole;
                try { whole = new GeoPolyline3(vertices); } catch (ArgumentException) { continue; }

                var pieces = new List<GeoLine3>();
                for (int i = 0; i < whole.EdgeCount; i++)
                {
                    GeoLine3 e = whole.GetEdgeAt(i);
                    pieces.Add(rng.Next(2) == 0 ? e : new GeoLine3(e.EndPoint, e.StartPoint));
                }

                for (int i = pieces.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    GeoLine3 tmp = pieces[i]; pieces[i] = pieces[j]; pieces[j] = tmp;
                }

                GeoPolyline3[] joined = Merge3.Join(pieces, Tol);

                double total = 0.0;
                foreach (var c in joined) { total += c.Length; }

                cases++;
                double d = Math.Abs(total - whole.Length);
                if (d > 1E-6) { lengthBad++; worst = Math.Max(worst, d); }

                // Joining an already joined set must not change it.
                double again = 0.0;
                foreach (var c in Merge3.Join(joined, Tol)) { again += c.Length; }
                if (Math.Abs(again - total) > 1E-6) { idemBad++; }
            }

            Assert.True(cases > 100, $"only {cases} cases");
            Assert.Equal(0, lengthBad);
            Assert.Equal(0, idemBad);
        }

        [Fact]
        public void ConsecutiveMergesKeepLength()
        {
            var rng = new Random(9090);
            int cases = 0, lineBad = 0, chainBad = 0;
            double worstL = 0, worstC = 0;

            for (int t = 0; t < 200; t++)
            {
                // A straight run chopped into collinear pieces, which ConsecutiveLines should rejoin.
                var a = new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));
                var b = new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));
                var run = new GeoLine3(a, b);
                if (run.Length < 1E-3) { continue; }

                var cuts = new List<double>();
                for (int k = 1; k < 5; k++) { cuts.Add(run.Length * k / 5.0); }

                GeoLine3[] parts = Splition3.SplitAtDistances(run, cuts, Tol);

                double before = 0.0;
                foreach (var p in parts) { before += p.Length; }

                double after = 0.0;
                foreach (var p in Merge3.ConsecutiveLines(parts, Tol)) { after += p.Length; }

                cases++;
                if (Math.Abs(after - before) > 1E-6) { lineBad++; worstL = Math.Max(worstL, Math.Abs(after - before)); }

                var chains = new List<GeoPolyline3>();
                foreach (var p in parts) { chains.Add(new GeoPolyline3(p.StartPoint, p.EndPoint)); }

                double afterChains = 0.0;
                foreach (var c in Merge3.ConsecutivePolylines(chains, Tol)) { afterChains += c.Length; }
                if (Math.Abs(afterChains - before) > 1E-6) { chainBad++; worstC = Math.Max(worstC, Math.Abs(afterChains - before)); }
            }

            Assert.Equal(0, lineBad);
            Assert.Equal(0, chainBad);
        }
    }
}
