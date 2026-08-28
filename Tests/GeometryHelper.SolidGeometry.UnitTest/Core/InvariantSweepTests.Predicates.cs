using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Invariants tying the predicates to one another and to the measurements.
    /// <para>
    /// A collision test and a distance are two ways of asking the same question, so they have to agree:
    /// two shapes touch exactly when nothing separates them. A containment test and a projection agree
    /// the same way. Where two answers can be derived independently, checking them against each other is
    /// worth more than checking either against a number worked out by hand.
    /// </para>
    /// </summary>
    public partial class InvariantSweepTests
    {
        [Fact]
        public void CollisionAgreesWithDistanceAndIsSymmetric()
        {
            Random rng = new Random(5511);
            int cases = 0, touching = 0, apart = 0, disagree = 0, asymmetric = 0;

            for (int t = 0; t < 200; t++)
            {
                bool snap = t % 2 == 0;

                GeoSolid3 a = Prism(Star(rng, 4 + rng.Next(4), snap), 4.0);

                double dx = rng.Next(-14, 15);
                double dy = rng.Next(-14, 15);
                List<GeoPoint3> moved = new List<GeoPoint3>();
                foreach (GeoPoint3 p in Star(rng, 4 + rng.Next(4), snap))
                {
                    moved.Add(new GeoPoint3(p.X + dx, p.Y + dy, 1));
                }

                GeoSolid3 b;
                try { b = Prism(moved.ToArray(), 4.0); }
                catch (ArgumentException) { continue; }

                cases++;

                bool hit = Collision3.CollidesWith(a, b, Tol);
                if (hit) { touching++; } else { apart++; }

                // Asking the other way round must not change the answer.
                if (Collision3.CollidesWith(b, a, Tol) != hit) { asymmetric++; }

                // Two bodies touch exactly when the gap between them has closed.
                bool near = Distance3.DistanceTo(a, b, Tol) <= Tol.EqualPoint;
                if (near != hit) { disagree++; }
            }

            Assert.True(cases > 120, $"only {cases} cases");
            Assert.True(touching > 10 && apart > 10, $"touching={touching} apart={apart}");
            Assert.Equal(0, asymmetric);
            Assert.Equal(0, disagree);
        }

        [Fact]
        public void ContainmentAgreesAcrossTheWaysOfDescribingABox()
        {
            Random rng = new Random(6622);
            int cases = 0, inside = 0, outside = 0, disagree = 0;

            for (int t = 0; t < 200; t++)
            {
                var min = new GeoPoint3(rng.Next(-8, 3), rng.Next(-8, 3), rng.Next(-8, 3));
                var max = new GeoPoint3(min.X + 1 + rng.Next(6), min.Y + 1 + rng.Next(6), min.Z + 1 + rng.Next(6));

                var aabb = new GeoAabb3(min, max);
                GeoObb3 obb = aabb.ToObb();
                GeoSolid3 solid = obb.ToSolid();

                for (int q = 0; q < 6; q++)
                {
                    // Probes are drawn from a range a little wider than the box itself, so that inside,
                    // outside and exactly-on-a-face all come up often.
                    var probe = new GeoPoint3(
                        min.X - 1 + rng.Next((int)(max.X - min.X) + 3),
                        min.Y - 1 + rng.Next((int)(max.Y - min.Y) + 3),
                        min.Z - 1 + rng.Next((int)(max.Z - min.Z) + 3));

                    cases++;

                    bool inBox = aabb.Contains(probe, Tol);
                    bool inObb = Containment3.Contains(obb, probe, Tol);
                    bool inSolid = Containment3.Contains(solid, probe, Tol);

                    if (inBox) { inside++; } else { outside++; }

                    // The same region described three ways must hold the same points.
                    if (inBox != inObb || inObb != inSolid) { disagree++; }
                }
            }

            Assert.True(cases > 600, $"only {cases} cases");
            Assert.True(inside > 30 && outside > 30, $"inside={inside} outside={outside}");
            Assert.Equal(0, disagree);
        }

        [Fact]
        public void LocateAndContainsTellTheSameStory()
        {
            Random rng = new Random(7733);
            int cases = 0, onSide = 0, disagree = 0, distanceDisagree = 0;

            for (int t = 0; t < 150; t++)
            {
                GeoPoint3[] prof = Star(rng, 4 + rng.Next(5), true);
                GeoSolid3 body;
                GeoPolygon3 plate;
                try { body = Prism(prof, 4.0); plate = new GeoPolygon3(prof); }
                catch (ArgumentException) { continue; }

                for (int q = 0; q < 6; q++)
                {
                    // Half the probes are pulled onto the surface, so that the boundary case is covered.
                    GeoPoint3 probe = q % 2 == 0
                        ? new GeoPoint3(rng.Next(-10, 11), rng.Next(-10, 11), rng.Next(-3, 8))
                        : Projection3.ProjectToSolid(body, new GeoPoint3(rng.Next(-10, 11), rng.Next(-10, 11), rng.Next(-3, 8)), Tol);

                    cases++;

                    PointLocation where = Containment3.Locate(body, probe, Tol);
                    if (where == PointLocation.OnSide) { onSide++; }

                    // Contains is Locate with the two inner answers merged.
                    if (Containment3.Contains(body, probe, Tol) != (where != PointLocation.OutSide)) { disagree++; }

                    // A point the body holds is at no distance from it.
                    bool held = where != PointLocation.OutSide;
                    bool noGap = Distance3.DistanceTo(body, probe, Tol) <= Tol.EqualPoint;
                    if (held != noGap) { distanceDisagree++; }

                    // The same for the flat region, whose own boundary projection must sit on it.
                    GeoPoint3 onPlate = Projection3.ProjectToPolygonBoundary(plate, probe, Tol);
                    if (Containment3.Locate(plate, onPlate, Tol) == PointLocation.OutSide) { disagree++; }
                }
            }

            Assert.True(cases > 500, $"only {cases} cases");
            Assert.True(onSide > 50, $"only {onSide} probes landed on the surface");
            Assert.Equal(0, disagree);
            Assert.Equal(0, distanceDisagree);
        }

        [Fact]
        public void AnIntersectionPointLiesOnBothShapes()
        {
            Random rng = new Random(8844);
            int planeCases = 0, triangleCases = 0, offPlane = 0, offTriangle = 0, missedDistance = 0;

            for (int t = 0; t < 400; t++)
            {
                var line = new GeoLine3(
                    new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)),
                    new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)));

                if (line.Length < 1E-6) { continue; }

                var plane = new GeoPlane3(
                    new GeoPoint3(rng.Next(-5, 6), rng.Next(-5, 6), rng.Next(-5, 6)),
                    new GeoVector3(rng.Next(-3, 4) + 0.3, rng.Next(-3, 4) + 0.7, rng.Next(-3, 4) + 0.1));

                if (Intersection3.TryIntersectWith(line, plane, out GeoPoint3 hit, Tol))
                {
                    planeCases++;

                    // The point has to be on the segment and on the plane, or it is not their crossing.
                    if (!Containment3.IsPointOn(line, hit, Tol)) { offPlane++; }
                    if (Math.Abs(plane.SignedDistanceTo(hit)) > Tol.EqualPlanar) { offPlane++; }

                    // Something crossing is something at no distance.
                    if (Distance3.DistanceTo(plane, line) > Tol.EqualPoint) { missedDistance++; }
                }

                var triangle = new GeoTriangle3(
                    new GeoPoint3(rng.Next(-6, 7), rng.Next(-6, 7), 0),
                    new GeoPoint3(rng.Next(-6, 7), rng.Next(-6, 7), 0),
                    new GeoPoint3(rng.Next(-6, 7), rng.Next(-6, 7), 2));

                if (triangle.IsDegenerate(Tol)) { continue; }

                if (Intersection3.TryIntersectWith(line, triangle, out GeoPoint3 spot, Tol))
                {
                    triangleCases++;

                    if (!Containment3.IsPointOn(line, spot, Tol)) { offTriangle++; }
                    if (Distance3.DistanceTo(triangle, spot) > Tol.EqualPoint) { offTriangle++; }
                }
            }

            Assert.True(planeCases > 50, $"only {planeCases} line-plane crossings");
            Assert.True(triangleCases > 5, $"only {triangleCases} line-triangle crossings");
            Assert.Equal(0, offPlane);
            Assert.Equal(0, offTriangle);
            Assert.Equal(0, missedDistance);
        }

        [Fact]
        public void TransformationsCompoundAndUndoThemselves()
        {
            Random rng = new Random(9955);
            int cases = 0, roundTripBad = 0, composeBad = 0, rigidBad = 0, volumeBad = 0;

            for (int t = 0; t < 200; t++)
            {
                GeoTransform3 move = GeoTransform3.Translation(
                    new GeoVector3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)));

                GeoTransform3 turn = GeoTransform3.RotationAxis(
                    new GeoVector3(rng.Next(-3, 4) + 0.3, rng.Next(-3, 4) + 0.5, rng.Next(-3, 4) + 0.9),
                    rng.NextDouble() * Math.PI * 2.0);

                GeoTransform3 rigid = move * turn;
                GeoTransform3 scale = GeoTransform3.Scaling(1 + rng.Next(1, 4), 1 + rng.Next(1, 4), 1 + rng.Next(1, 4));
                GeoTransform3 mixed = rigid * scale;

                var p = new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));
                var q = new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));

                cases++;

                // Undoing a transformation must put the point back.
                if (mixed.TryGetInverse(out GeoTransform3 undo))
                {
                    if (!undo.Transform(mixed.Transform(p)).IsEqualTo(p, Tol)) { roundTripBad++; }
                }
                else
                {
                    roundTripBad++;
                }

                // Applying two in a row is the same as applying the one they compose into.
                if (!rigid.Transform(scale.Transform(p)).IsEqualTo(mixed.Transform(p), Tol)) { composeBad++; }

                // A rigid motion moves everything without changing any distance.
                double before = p.DistanceTo(q);
                double after = rigid.Transform(p).DistanceTo(rigid.Transform(q));
                if (Math.Abs(before - after) > 1E-6) { rigidBad++; }

                // The determinant is exactly the factor volume is multiplied by.
                GeoPoint3[] prof = Star(rng, 4 + rng.Next(4), true);
                GeoSolid3 body;
                try { body = Prism(prof, 4.0); }
                catch (ArgumentException) { continue; }

                double expected = body.Volume * Math.Abs(mixed.GetDeterminant());
                double actual = body.TransformBy(mixed).Volume;
                if (Math.Abs(expected - actual) > 1E-6 * Math.Max(1.0, expected)) { volumeBad++; }
            }

            Assert.True(cases > 150, $"only {cases} cases");
            Assert.Equal(0, roundTripBad);
            Assert.Equal(0, composeBad);
            Assert.Equal(0, rigidBad);
            Assert.Equal(0, volumeBad);
        }
    }
}
