using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Core
{
    /// <summary>
    /// Invariants tying the plane predicates to one another and to the measurements: two shapes touch
    /// exactly when nothing separates them, a point on a boundary is at no distance from it, and a
    /// reported crossing really lies on both shapes.
    /// </summary>
    public partial class InvariantSweepTests
    {
        [Fact]
        public void CollisionAgreesWithDistanceAndIsSymmetric()
        {
            Random rng = new Random(1717);
            int cases = 0, touching = 0, apart = 0, disagree = 0, asymmetric = 0;

            for (int t = 0; t < 400; t++)
            {
                bool snap = t % 2 == 0;

                GeoPolygon2 first;
                GeoPolygon2 second;
                try
                {
                    first = new GeoPolygon2(Star(rng, 4 + rng.Next(4), snap));
                    second = new GeoPolygon2(Star(rng, 4 + rng.Next(4), snap, rng.Next(-12, 13), rng.Next(-12, 13)));
                }
                catch (ArgumentException) { continue; }

                var circle = new GeoCircle2(new GeoPoint2(rng.Next(-10, 11), rng.Next(-10, 11)), 1 + rng.Next(1, 5));
                var rect = new GeoRectangle2(new GeoPoint2(rng.Next(-10, 11), rng.Next(-10, 11)), 2 + rng.Next(1, 5), 2 + rng.Next(1, 5));

                cases++;

                Pair(Collision2.CollidesWith(first, second, Tol), Collision2.CollidesWith(second, first, Tol),
                     Distance2.DistanceTo(first, second), ref touching, ref apart, ref disagree, ref asymmetric);

                Pair(Collision2.CollidesWith(circle, rect, Tol), Collision2.CollidesWith(circle, rect, Tol),
                     Distance2.DistanceTo(circle, rect), ref touching, ref apart, ref disagree, ref asymmetric);

                Pair(Collision2.CollidesWith(circle, first, Tol), Collision2.CollidesWith(circle, first, Tol),
                     Distance2.DistanceTo(circle, first), ref touching, ref apart, ref disagree, ref asymmetric);

                Pair(Collision2.CollidesWith(rect, first, Tol), Collision2.CollidesWith(rect, first, Tol),
                     Distance2.DistanceTo(rect, first), ref touching, ref apart, ref disagree, ref asymmetric);
            }

            Assert.True(cases > 200, $"only {cases} cases");
            Assert.True(touching > 50 && apart > 50, $"touching={touching} apart={apart}");
            Assert.Equal(0, asymmetric);
            Assert.Equal(0, disagree);
        }

        private static void Pair(bool hit, bool mirrored, double gap,
                                 ref int touching, ref int apart, ref int disagree, ref int asymmetric)
        {
            if (hit) { touching++; } else { apart++; }
            if (mirrored != hit) { asymmetric++; }

            // Shapes touch exactly when the gap between them has closed.
            if ((gap <= Tol.EqualPoint) != hit) { disagree++; }
        }

        [Fact]
        public void ABoundaryPointIsAtNoDistanceAndIsHeld()
        {
            Random rng = new Random(1818);
            int cases = 0, offBoundary = 0, notHeld = 0, insideCases = 0;

            for (int t = 0; t < 300; t++)
            {
                GeoPolygon2 poly;
                try { poly = new GeoPolygon2(Star(rng, 4 + rng.Next(5), true)); }
                catch (ArgumentException) { continue; }

                var circle = new GeoCircle2(new GeoPoint2(rng.Next(-6, 7), rng.Next(-6, 7)), 1 + rng.Next(1, 5));

                for (int q = 0; q < 4; q++)
                {
                    var probe = new GeoPoint2(rng.Next(-10, 11), rng.Next(-10, 11));
                    cases++;

                    // A point pulled onto the boundary must be recognised as being on it, must be held by
                    // the filled region, and must be at no distance from it.
                    GeoPoint2 onEdge = Projection2.ProjectToPolygon(poly, probe, Tol);

                    if (!Containment2.IsPointOn(poly, onEdge, Tol)) { offBoundary++; }
                    if (!Containment2.Contains(poly, onEdge, Tol)) { notHeld++; }
                    if (Distance2.DistanceTo(poly, onEdge) > Tol.EqualPoint) { notHeld++; }

                    GeoPoint2 onRim = Projection2.ProjectToCircle(circle, probe, Tol);

                    if (!Containment2.IsPointOn(circle, onRim, Tol)) { offBoundary++; }
                    if (!Containment2.Contains(circle, onRim, Tol)) { notHeld++; }

                    if (Containment2.Contains(poly, probe, Tol)) { insideCases++; }
                }
            }

            Assert.True(cases > 500, $"only {cases} cases");
            Assert.True(insideCases > 20, $"only {insideCases} probes fell inside a polygon");
            Assert.Equal(0, offBoundary);
            Assert.Equal(0, notHeld);
        }

        [Fact]
        public void ACrossingLiesOnBothShapes()
        {
            Random rng = new Random(1919);
            int circleLine = 0, circleCircle = 0, rectLine = 0, off = 0;

            for (int t = 0; t < 500; t++)
            {
                var circle = new GeoCircle2(new GeoPoint2(rng.Next(-6, 7), rng.Next(-6, 7)), 1 + rng.Next(1, 5));
                var other = new GeoCircle2(new GeoPoint2(rng.Next(-6, 7), rng.Next(-6, 7)), 1 + rng.Next(1, 5));
                var rect = new GeoRectangle2(new GeoPoint2(rng.Next(-6, 7), rng.Next(-6, 7)), 2 + rng.Next(1, 5), 2 + rng.Next(1, 5));

                var line = new GeoLine2(
                    new GeoPoint2(rng.Next(-12, 13), rng.Next(-12, 13)),
                    new GeoPoint2(rng.Next(-12, 13), rng.Next(-12, 13)));

                if (line.Length < 1E-6) { continue; }

                foreach (GeoPoint2 hit in Intersection2.GetIntersections(circle, line, Tol))
                {
                    circleLine++;
                    if (!Containment2.IsPointOn(circle, hit, Tol)) { off++; }
                    if (!Containment2.IsPointOn(line, hit, Tol)) { off++; }
                }

                foreach (GeoPoint2 hit in Intersection2.GetIntersections(circle, other, Tol))
                {
                    circleCircle++;
                    if (!Containment2.IsPointOn(circle, hit, Tol)) { off++; }
                    if (!Containment2.IsPointOn(other, hit, Tol)) { off++; }
                }

                foreach (GeoPoint2 hit in Intersection2.GetIntersections(rect, line, Tol))
                {
                    rectLine++;
                    if (!Containment2.IsPointOn(line, hit, Tol)) { off++; }
                    if (Distance2.DistanceTo(rect, hit) > Tol.EqualPoint) { off++; }
                }
            }

            Assert.True(circleLine > 50, $"only {circleLine} circle-line crossings");
            Assert.True(circleCircle > 20, $"only {circleCircle} circle-circle crossings");
            Assert.True(rectLine > 50, $"only {rectLine} rectangle-line crossings");
            Assert.Equal(0, off);
        }
    }
}
