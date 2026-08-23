using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest
{
    /// <summary>
    /// Checks the properties that must hold across the whole library rather than for one method: that a
    /// rigid motion changes nothing that is measured in the shape itself, that distance does not depend
    /// on which shape is asked first, and that the degenerate cases answer rather than crash.
    /// </summary>
    public class InvariantTests
    {
        /// <summary>
        /// A handful of rigid motions: translation, rotation about each kind of axis, and combinations.
        /// A rigid motion is one that changes no length, so every measurement below must survive it.
        /// </summary>
        private static IEnumerable<GeoTransform3> RigidMotions()
        {
            yield return GeoTransform3.Identity;
            yield return GeoTransform3.Translation(new GeoVector3(1000.0, -2000.0, 3000.0));
            yield return GeoTransform3.RotationZ(0.7);
            yield return GeoTransform3.RotationAxis(new GeoVector3(1.0, 2.0, 3.0), 1.3);
            yield return GeoTransform3.Translation(new GeoVector3(5.0, 5.0, 5.0))
                .Multiply(GeoTransform3.RotationAxis(new GeoVector3(-3.0, 1.0, 2.0), 2.1));
            yield return GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(1.0, 0.0, 0.0), new GeoVector3(1.0, 1.0, 1.0)));
        }

        private static GeoPolygon3 MakeLShape() => new GeoPolygon3(
            new GeoPoint3(0.0, 0.0, 0.0),
            new GeoPoint3(10.0, 0.0, 0.0),
            new GeoPoint3(10.0, 4.0, 0.0),
            new GeoPoint3(4.0, 4.0, 0.0),
            new GeoPoint3(4.0, 10.0, 0.0),
            new GeoPoint3(0.0, 10.0, 0.0));

        private static GeoSolid3 MakeCube() =>
            new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0)).ToObb().ToSolid();

        [Fact]
        public void PolygonAreaSurvivesEveryRigidMotion()
        {
            GeoPolygon3 polygon = MakeLShape();

            foreach (GeoTransform3 motion in RigidMotions())
            {
                Assert.Equal(polygon.Area, polygon.TransformBy(motion).Area, 6);
                Assert.Equal(polygon.Length, polygon.TransformBy(motion).Length, 6);
            }
        }

        [Fact]
        public void SolidVolumeAndSurfaceAreaSurviveEveryRigidMotion()
        {
            GeoSolid3 cube = MakeCube();

            foreach (GeoTransform3 motion in RigidMotions())
            {
                GeoSolid3 moved = cube.TransformBy(motion);

                Assert.Equal(cube.Volume, moved.Volume, 4);
                Assert.Equal(cube.SurfaceArea, moved.SurfaceArea, 4);
                Assert.True(moved.IsClosed());
            }
        }

        [Fact]
        public void PolylineLengthSurvivesEveryRigidMotion()
        {
            GeoPolyline3 polyline = new GeoPolyline3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(3.0, 0.0, 0.0),
                new GeoPoint3(3.0, 4.0, 0.0),
                new GeoPoint3(3.0, 4.0, 12.0));

            foreach (GeoTransform3 motion in RigidMotions())
            {
                Assert.Equal(polyline.Length, polyline.TransformBy(motion).Length, 6);
            }
        }

        [Fact]
        public void ContainmentSurvivesEveryRigidMotion()
        {
            GeoSolid3 cube = MakeCube();
            GeoPoint3 inside = new GeoPoint3(5.0, 5.0, 5.0);
            GeoPoint3 outside = new GeoPoint3(50.0, 5.0, 5.0);

            foreach (GeoTransform3 motion in RigidMotions())
            {
                GeoSolid3 moved = cube.TransformBy(motion);

                Assert.Equal(PointLocation.Inside, moved.Locate(motion.Transform(inside)));
                Assert.Equal(PointLocation.OutSide, moved.Locate(motion.Transform(outside)));
            }
        }

        [Fact]
        public void DistanceSurvivesEveryRigidMotion()
        {
            GeoPolygon3 polygon = MakeLShape();
            GeoPoint3 probe = new GeoPoint3(20.0, 20.0, 7.0);

            double reference = polygon.DistanceTo(probe);

            foreach (GeoTransform3 motion in RigidMotions())
            {
                Assert.Equal(reference, polygon.TransformBy(motion).DistanceTo(motion.Transform(probe)), 6);
            }
        }

        [Fact]
        public void DistanceIsSymmetricForEveryPairItIsDefinedOn()
        {
            GeoPoint3 point = new GeoPoint3(7.0, -3.0, 11.0);
            GeoLine3 line = new GeoLine3(new GeoPoint3(-5.0, 0.0, 0.0), new GeoPoint3(5.0, 2.0, 1.0));
            GeoLine3 other = new GeoLine3(new GeoPoint3(0.0, -8.0, 4.0), new GeoPoint3(3.0, 8.0, -2.0));
            GeoPlane3 plane = new GeoPlane3(new GeoPoint3(1.0, 1.0, 1.0), new GeoVector3(2.0, -1.0, 3.0));
            GeoTriangle3 triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(6.0, 0.0, 0.0), new GeoPoint3(0.0, 8.0, 0.0));

            Assert.Equal(Distance3.DistanceTo(line, point), point.DistanceTo(line), 12);
            Assert.Equal(Distance3.DistanceTo(plane, point), point.DistanceTo(plane), 12);
            Assert.Equal(Distance3.DistanceTo(triangle, point), point.DistanceTo(triangle), 12);
            Assert.Equal(line.DistanceTo(other), other.DistanceTo(line), 9);
        }

        [Fact]
        public void ProjectionAndDistanceAlwaysAgree()
        {
            GeoPolygon3 polygon = MakeLShape();
            GeoObb3 box = new GeoObb3(new GeoPoint3(1.0, 2.0, 3.0), 4.0, 5.0, 6.0);
            GeoTriangle3 triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(6.0, 0.0, 0.0), new GeoPoint3(0.0, 8.0, 0.0));

            GeoPoint3[] probes =
            {
                new GeoPoint3(5.0, 5.0, 5.0),
                new GeoPoint3(-10.0, -10.0, 0.0),
                new GeoPoint3(100.0, 3.0, -7.0),
                new GeoPoint3(2.0, 2.0, 0.0)
            };

            foreach (GeoPoint3 probe in probes)
            {
                Assert.Equal(polygon.DistanceTo(probe), polygon.GetClosestPointOnBoundary(probe).DistanceTo(probe), 9);
                Assert.Equal(box.DistanceTo(probe), box.GetClosestPointOnBoundary(probe).DistanceTo(probe), 9);
                Assert.Equal(triangle.DistanceTo(probe), triangle.GetClosestPointOnBoundary(probe).DistanceTo(probe), 9);
            }
        }

        [Fact]
        public void ClosestPointIsNeverFartherThanAnyOtherPointOnTheShape()
        {
            GeoPolygon3 polygon = MakeLShape();
            GeoPoint3 probe = new GeoPoint3(7.0, 7.0, 3.0);

            double best = polygon.DistanceTo(probe);

            // Sample the polygon densely and confirm nothing on it beats the reported answer.
            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                GeoLine3 edge = polygon.GetEdgeAt(i);

                for (double t = 0.0; t <= 1.0; t += 0.05)
                {
                    Assert.True(edge.GetPointAtParameter(t).DistanceTo(probe) >= best - 1E-9);
                }
            }
        }

        [Fact]
        public void ReversingOrientationChangesTheNormalButNothingMeasured()
        {
            GeoPolygon3 polygon = MakeLShape();
            GeoPolygon3 flipped = polygon.Flip();

            Assert.True(flipped.Normal.IsEqualTo(polygon.Normal.Negate()));
            Assert.Equal(polygon.Area, flipped.Area, 9);
            Assert.Equal(polygon.Length, flipped.Length, 9);
            Assert.True(polygon.Centroid.IsEqualTo(flipped.Centroid));
            Assert.Equal(polygon.Locate(new GeoPoint3(2.0, 2.0, 0.0)), flipped.Locate(new GeoPoint3(2.0, 2.0, 0.0)));
        }

        [Fact]
        public void CollisionIsSymmetricForEveryPairItIsDefinedOn()
        {
            GeoObb3 box1 = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);
            GeoObb3 box2 = new GeoObb3(new GeoPoint3(6.0, 2.0, 1.0), 8.0, 4.0, 4.0,
                new GeoVector3(1.0, 1.0, 0.0), new GeoVector3(-1.0, 1.0, 0.0));

            GeoSolid3 solid1 = box1.ToSolid();
            GeoSolid3 solid2 = box2.ToSolid();

            Assert.Equal(Collision3.CollidesWith(box1, box2), Collision3.CollidesWith(box2, box1));
            Assert.Equal(Collision3.CollidesWith(solid1, solid2), Collision3.CollidesWith(solid2, solid1));

            GeoTriangle3 t1 = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(10.0, 0.0, 0.0), new GeoPoint3(0.0, 10.0, 0.0));
            GeoTriangle3 t2 = new GeoTriangle3(new GeoPoint3(2.0, 2.0, -5.0), new GeoPoint3(2.0, 2.0, 5.0), new GeoPoint3(8.0, 2.0, 5.0));

            Assert.Equal(Collision3.CollidesWith(t1, t2), Collision3.CollidesWith(t2, t1));
        }

        [Fact]
        public void DegenerateInputsAnswerRatherThanCrash()
        {
            GeoLine3 dot = new GeoLine3(new GeoPoint3(1.0, 1.0, 1.0), new GeoPoint3(1.0, 1.0, 1.0));
            GeoTriangle3 sliver = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(1.0, 0.0, 0.0), new GeoPoint3(2.0, 0.0, 0.0));
            GeoObb3 flatBox = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 0.0);
            GeoPoint3 probe = new GeoPoint3(5.0, 5.0, 5.0);

            // None of these is a shape the library can say much about, but every one must return.
            Assert.True(dot.DistanceTo(probe) > 0.0);
            Assert.Equal(0.0, sliver.Area, 12);
            Assert.Equal(PointLocation.OutSide, sliver.Locate(new GeoPoint3(1.0, 0.0, 0.0)));
            Assert.False(dot.TryIntersectWith(GeoPlane3.XY, out _));
            Assert.True(flatBox.IsDegenerate());
            Assert.Equal(0.0, flatBox.Volume, 12);
            Assert.True(flatBox.DistanceTo(probe) > 0.0);
        }

        [Fact]
        public void EveryShapeStaysInsideItsOwnBoundingBox()
        {
            Tolerance loose = new Tolerance(1E-6, 1E-6);

            GeoPolygon3 polygon = MakeLShape();
            GeoAabb3 polygonBounds = polygon.GetAabb();

            foreach (GeoPoint3 vertex in polygon.Vertices)
            {
                Assert.True(polygonBounds.Contains(vertex, loose));
            }

            GeoObb3 box = new GeoObb3(new GeoPoint3(1.0, 2.0, 3.0), 4.0, 5.0, 6.0,
                new GeoVector3(1.0, 2.0, 3.0), new GeoVector3(0.0, 1.0, 0.0));
            GeoAabb3 boxBounds = box.GetAabb();

            foreach (GeoPoint3 corner in box.GetCorners())
            {
                Assert.True(boxBounds.Contains(corner, loose));
            }

            GeoSolid3 solid = MakeCube().TransformBy(GeoTransform3.RotationAxis(new GeoVector3(1.0, 1.0, 0.0), 0.6));
            GeoAabb3 solidBounds = solid.GetAabb();

            foreach (GeoTriangle3 triangle in solid.Triangulate())
            {
                Assert.True(solidBounds.Contains(triangle.A, loose));
                Assert.True(solidBounds.Contains(triangle.Centroid, loose));
            }
        }

        [Fact]
        public void ParametrizationRoundTripsOnEveryCurve()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(1.0, 2.0, 3.0), new GeoPoint3(11.0, -8.0, 15.0));
            GeoPolyline3 polyline = new GeoPolyline3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(3.0, 0.0, 0.0),
                new GeoPoint3(3.0, 4.0, 0.0));

            for (double t = 0.0; t <= 1.0; t += 0.1)
            {
                GeoPoint3 onLine = line.GetPointAtParameter(t);
                Assert.Equal(t, line.GetParameterAtPoint(onLine), 9);

                GeoPoint3 onPolyline = polyline.GetPointAtParameter(t);
                Assert.Equal(t, polyline.GetParameterAtPoint(onPolyline), 9);
            }
        }
    }
}
