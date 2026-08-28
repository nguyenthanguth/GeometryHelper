using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Invariants on the shapes that were only reached indirectly by the other sweeps: the oriented box,
    /// the circle and the ray. Each is checked against something derived a different way — the box against
    /// the solid it turns into, the circle against its own plane and radius, the ray against the segment
    /// it can be cut down to.
    /// </summary>
    public partial class InvariantSweepTests
    {
        [Fact]
        public void AnOrientedBoxAgreesWithTheSolidItBecomes()
        {
            Random rng = new Random(3311);
            int cases = 0, inside = 0, outside = 0;

            for (int t = 0; t < 200; t++)
            {
                var centre = new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));
                double sx = 1 + rng.Next(8);
                double sy = 1 + rng.Next(8);
                double sz = 1 + rng.Next(8);

                GeoObb3 box;
                try
                {
                    box = new GeoObb3(centre, sx, sy, sz,
                        new GeoVector3(rng.Next(-3, 4) + 0.3, rng.Next(-3, 4) + 0.7, rng.Next(-3, 4) + 0.1),
                        new GeoVector3(rng.Next(-3, 4) + 0.9, rng.Next(-3, 4) + 0.2, rng.Next(-3, 4) + 0.5));
                }
                catch (ArgumentException) { continue; }

                cases++;

                GeoSolid3 solid = box.ToSolid();

                // The box knows its own volume from its sizes; the solid works it out from its faces.
                Assert.Equal(box.Volume, solid.Volume, 6);
                Assert.Equal(sx * sy * sz, box.Volume, 6);
                Assert.Equal(box.SurfaceArea, solid.SurfaceArea, 6);
                Assert.True(solid.IsClosed());

                // Eight corners, each at the box centre plus or minus half of each axis.
                GeoPoint3[] corners = box.GetCorners();
                Assert.Equal(8, corners.Length);

                foreach (GeoPoint3 corner in corners)
                {
                    // A corner is on the boundary, so the box holds it but it is not strictly within.
                    Assert.NotEqual(PointLocation.OutSide, Containment3.Locate(box, corner, Tol));
                    Assert.True(Distance3.DistanceTo(box, corner) <= Tol.EqualPoint);
                }

                // The centre of the corners is the centre of the box.
                double cx = 0, cy = 0, cz = 0;
                foreach (GeoPoint3 corner in corners) { cx += corner.X; cy += corner.Y; cz += corner.Z; }
                Assert.True(new GeoPoint3(cx / 8.0, cy / 8.0, cz / 8.0).IsEqualTo(box.Center, Tol));

                // Six faces, and their areas add up to the surface the box reports.
                GeoPolygon3[] faces = box.GetFaces();
                Assert.Equal(6, faces.Length);

                double covered = 0.0;
                foreach (GeoPolygon3 face in faces) { covered += face.Area; }
                Assert.Equal(box.SurfaceArea, covered, 6);

                for (int q = 0; q < 4; q++)
                {
                    // Drawn from a spread a little wider than the box, so that inside and outside both
                    // come up often. A probe thrown across the whole scene would almost always miss.
                    double reach = Math.Max(sx, Math.Max(sy, sz));
                    var probe = new GeoPoint3(
                        centre.X + (rng.NextDouble() * 2 - 1) * reach,
                        centre.Y + (rng.NextDouble() * 2 - 1) * reach,
                        centre.Z + (rng.NextDouble() * 2 - 1) * reach);

                    bool inBox = Containment3.Contains(box, probe, Tol);
                    if (inBox) { inside++; } else { outside++; }

                    // The box and the solid it becomes must hold the same points.
                    Assert.Equal(inBox, Containment3.Contains(solid, probe, Tol));
                }
            }

            Assert.True(cases > 150, $"only {cases} boxes");
            Assert.True(inside > 20 && outside > 20, $"inside={inside} outside={outside}");
        }

        [Fact]
        public void ACircleStaysOnItsOwnPlaneAtItsOwnRadius()
        {
            Random rng = new Random(4422);
            int cases = 0;

            for (int t = 0; t < 200; t++)
            {
                var centre = new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));
                double radius = 1 + rng.Next(8);

                GeoCircle3 circle;
                try
                {
                    circle = new GeoCircle3(centre,
                        new GeoVector3(rng.Next(-3, 4) + 0.3, rng.Next(-3, 4) + 0.7, rng.Next(-3, 4) + 0.1),
                        radius);
                }
                catch (ArgumentException) { continue; }
                catch (InvalidOperationException) { continue; }

                cases++;

                Assert.Equal(2.0 * Math.PI * radius, circle.Length, 9);
                Assert.Equal(Math.PI * radius * radius, circle.Area, 9);

                GeoPlane3 plane = circle.GetPlane();

                for (int k = 0; k < 12; k++)
                {
                    // Every point of the rim is exactly a radius from the centre and exactly on the plane,
                    // whichever way it is asked for.
                    GeoPoint3 byAngle = circle.GetPointAtAngle(2.0 * Math.PI * k / 12.0);
                    GeoPoint3 byParameter = circle.GetPointAtParameter(k / 12.0);
                    GeoPoint3 byDistance = circle.GetPointAtDistance(circle.Length * k / 12.0);

                    foreach (GeoPoint3 rim in new[] { byAngle, byParameter, byDistance })
                    {
                        Assert.Equal(radius, rim.DistanceTo(centre), 6);
                        Assert.True(Math.Abs(plane.SignedDistanceTo(rim)) <= Tol.EqualPlanar);
                    }

                    // The three ways of naming the same position must name the same point.
                    Assert.True(byParameter.IsEqualTo(byDistance, Tol), $"{byParameter} vs {byDistance}");
                    Assert.True(byAngle.IsEqualTo(byParameter, Tol), $"{byAngle} vs {byParameter}");
                }

                // Moving the circle moves its rim with it and leaves the radius alone.
                GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10)));
                GeoCircle3 moved = circle.TransformBy(move);

                Assert.Equal(circle.Radius, moved.Radius, 9);
                Assert.True(moved.Center.IsEqualTo(move.Transform(circle.Center), Tol));
            }

            Assert.True(cases > 150, $"only {cases} circles");
        }

        [Fact]
        public void ARayRunsForwardsOnlyAndAgreesWithTheSegmentItCutsDownTo()
        {
            Random rng = new Random(5533);
            int cases = 0, behind = 0;

            for (int t = 0; t < 300; t++)
            {
                var origin = new GeoPoint3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));
                var direction = new GeoVector3(rng.Next(-5, 6) + 0.3, rng.Next(-5, 6) + 0.7, rng.Next(-5, 6) + 0.1);

                GeoRay3 ray;
                try { ray = new GeoRay3(origin, direction); }
                catch (ArgumentException) { continue; }
                catch (InvalidOperationException) { continue; }

                cases++;

                // The direction is stored as a unit vector, which is what makes the distance along the ray
                // a real distance rather than a multiple of whatever length was passed in.
                Assert.True(ray.Direction.IsUnitLength(Tol));

                for (int k = 1; k <= 6; k++)
                {
                    double d = k * 3.0;
                    GeoPoint3 along = ray.GetPointAtDistance(d);

                    Assert.Equal(d, ray.GetDistanceAtPoint(along), 6);
                    Assert.Equal(d, origin.DistanceTo(along), 6);
                    Assert.True(ray.IsPointOn(along));

                    // Cutting the ray down to a segment must give the same stretch.
                    GeoLine3 cut = ray.ToLine(d);
                    Assert.True(cut.StartPoint.IsEqualTo(origin, Tol));
                    Assert.True(cut.EndPoint.IsEqualTo(along, Tol));
                    Assert.Equal(d, cut.Length, 6);
                }

                // A point behind the origin is not on the ray, however close it is to the line it runs on.
                GeoPoint3 back = ray.GetPointAtDistance(-5.0);
                if (!ray.IsPointOn(back))
                {
                    behind++;

                    // It is off the ray, so the nearest point on the ray is the origin itself.
                    Assert.True(Projection3.ProjectToRay(ray, back).IsEqualTo(origin, Tol));
                    Assert.Equal(origin.DistanceTo(back), Distance3.DistanceTo(ray, back), 6);
                }

                // Turning the ray round makes what was behind it lie ahead.
                Assert.True(ray.Reverse().IsPointOn(back));
            }

            Assert.True(cases > 200, $"only {cases} rays");
            Assert.True(behind > 200, $"only {behind} points behind the origin were rejected");
        }
    }
}
