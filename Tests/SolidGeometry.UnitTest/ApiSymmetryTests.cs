using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommonGeometry.Enums;
using SolidGeometry;
using SolidGeometry.Core;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest
{
    /// <summary>
    /// Guards the promises the library makes about the shape of its own API: that every geometry type
    /// offers the same handful of members, that the instance form of an operation agrees with the static
    /// one, and that nothing in the geometry namespace implements an operation the Core classes own.
    /// </summary>
    public class ApiSymmetryTests
    {
        private static IEnumerable<Type> GeometryTypes()
        {
            return typeof(GeoPoint3).Assembly
                .GetTypes()
                .Where(t => t.IsPublic && t.Namespace == "SolidGeometry.Geometry" && t.Name.StartsWith("Geo"))
                .OrderBy(t => t.Name);
        }

        [Fact]
        public void EveryGeometryTypeOffersTheSameWayToAskForACopy()
        {
            foreach (Type type in GeometryTypes())
            {
                Assert.True(
                    type.GetMethod("Clone", Type.EmptyTypes) != null,
                    type.Name + " has no Clone()");
            }
        }

        [Fact]
        public void EveryGeometryTypeCanBeTransformed()
        {
            foreach (Type type in GeometryTypes())
            {
                if (type == typeof(GeoTransform3))
                {
                    // A transformation is applied, not transformed.
                    continue;
                }

                Assert.True(
                    type.GetMethod("TransformBy", new[] { typeof(GeoTransform3) }) != null,
                    type.Name + " has no TransformBy(GeoTransform3)");
            }
        }

        [Fact]
        public void EveryBoundedTypeReportsItsBoundingBox()
        {
            Type[] unbounded = { typeof(GeoPlane3), typeof(GeoRay3), typeof(GeoTransform3), typeof(GeoCoordinateSystem3), typeof(GeoAabb3), typeof(GeoVector3), typeof(GeoPoint3) };

            foreach (Type type in GeometryTypes())
            {
                if (unbounded.Contains(type))
                {
                    continue;
                }

                Assert.True(
                    type.GetMethod("GetAabb", Type.EmptyTypes) != null,
                    type.Name + " has no GetAabb()");
            }
        }

        [Fact]
        public void TransformingThroughEitherFormGivesTheSameResult()
        {
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(3, -4, 5))
                .Multiply(GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7));

            var point = new GeoPoint3(1, 2, 3);
            var vector = new GeoVector3(4, 5, 6);
            var line = new GeoLine3(point, point.Add(vector));
            var ray = new GeoRay3(point, vector);
            var plane = new GeoPlane3(point, vector);
            var triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0));
            var circle = new GeoCircle3(point, vector, 2.0);
            var bounds = new GeoAabb3(GeoPoint3.Origin, point);
            var frame = new GeoCoordinateSystem3(point, GeoVector3.XAxis, GeoVector3.YAxis);

            Assert.Equal(motion.Transform(point), point.TransformBy(motion));
            Assert.Equal(motion.Transform(vector), vector.TransformBy(motion));
            Assert.Equal(motion.Transform(line), line.TransformBy(motion));
            Assert.Equal(motion.Transform(ray), ray.TransformBy(motion));
            Assert.Equal(motion.Transform(plane), plane.TransformBy(motion));
            Assert.Equal(motion.Transform(triangle), triangle.TransformBy(motion));
            Assert.Equal(motion.Transform(circle), circle.TransformBy(motion));
            Assert.Equal(motion.Transform(bounds), bounds.TransformBy(motion));
            Assert.Equal(motion.Transform(frame), frame.TransformBy(motion));
        }

        [Fact]
        public void ARigidMotionLeavesEveryMeasurementAlone()
        {
            GeoTransform3 motion = GeoTransform3.RotationAxis(new GeoVector3(1, 1, 1), 0.9);

            var circle = new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 1), 5.0);
            var triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(3, 0, 0), new GeoPoint3(0, 4, 0));
            var line = new GeoLine3(GeoPoint3.Origin, new GeoPoint3(3, 4, 12));

            Assert.Equal(circle.Radius, circle.TransformBy(motion).Radius, 9);
            Assert.Equal(triangle.Area, triangle.TransformBy(motion).Area, 9);
            Assert.Equal(line.Length, line.TransformBy(motion).Length, 9);
        }

        [Fact]
        public void ACircleStretchedUnevenlyIsRefusedRatherThanFlattenedIntoOne()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 5.0);

            // Uniform scaling keeps it a circle.
            Assert.Equal(10.0, circle.TransformBy(GeoTransform3.Scaling(2.0)).Radius, 9);

            // Stretching one axis of its plane would make it an ellipse, which has no type here.
            Assert.Throws<InvalidOperationException>(() => circle.TransformBy(GeoTransform3.Scaling(3.0, 1.0, 1.0)));
        }

        [Fact]
        public void AnAxisAlignedBoxGrowsWhenItIsTurned()
        {
            var bounds = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 1, 1));
            GeoAabb3 turned = bounds.TransformBy(GeoTransform3.RotationZ(Math.PI / 4.0));

            // Alignment is insisted on, so a turned box has to be reported by a larger aligned one.
            Assert.True(turned.Volume > bounds.Volume);
            Assert.True(GeoAabb3.Empty.TransformBy(GeoTransform3.Identity).IsEmpty);
        }

        [Fact]
        public void TheCircleAgreesWithTheCoreClassesItNowDelegatesTo()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 5.0);
            var probe = new GeoPoint3(9, 2, 4);

            Assert.Equal(Containment3.Locate(circle, probe), circle.Locate(probe));
            Assert.Equal(Containment3.Contains(circle, probe), circle.Contains(probe));
            Assert.Equal(Containment3.IsPointOn(circle, probe), circle.IsPointOn(probe));
            Assert.Equal(Projection3.ProjectToCircle(circle, probe), circle.GetClosestPointOnBoundary(probe));
            Assert.Equal(Distance3.DistanceTo(circle, probe), circle.DistanceTo(probe), 12);
            Assert.Equal(Distance3.DistanceTo(circle, probe), probe.DistanceTo(circle), 12);
        }

        [Fact]
        public void TheDiscIsMeasuredAsAFilledSurfaceAndTheRimAsARim()
        {
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 5.0);
            var above = new GeoPoint3(0, 0, 4);

            // Straight down onto the surface, not out to the rim.
            Assert.Equal(4.0, circle.DistanceTo(above), 9);
            Assert.True(Projection3.ProjectToDisc(circle, above).IsEqualTo(GeoPoint3.Origin));

            // The rim projection always lands at the radius.
            Assert.Equal(5.0, circle.Center.DistanceTo(Projection3.ProjectToCircle(circle, above)), 9);
        }

        [Fact]
        public void TheFaceAgreesWithTheCoreClassesItNowDelegatesTo()
        {
            GeoPolygon3 boundary = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            GeoPolygon3 hole = new GeoPolygon3(
                new GeoPoint3(4, 4, 0), new GeoPoint3(6, 4, 0),
                new GeoPoint3(6, 6, 0), new GeoPoint3(4, 6, 0));

            GeoFace3 face = new GeoFace3(boundary, new[] { hole });

            GeoPoint3[] probes =
            {
                new GeoPoint3(1, 1, 0),
                new GeoPoint3(5, 5, 0),
                new GeoPoint3(4, 5, 0),
                new GeoPoint3(50, 50, 0)
            };

            foreach (GeoPoint3 probe in probes)
            {
                Assert.Equal(Containment3.Locate(face, probe), face.Locate(probe));
                Assert.Equal(Containment3.Locate(face, probe), probe.LocateIn(face));
                Assert.Equal(Containment3.Contains(face, probe), face.Contains(probe));
            }
        }

        [Fact]
        public void APointCanBeMeasuredAgainstEveryShape()
        {
            var probe = new GeoPoint3(20, 20, 20);

            var polyline = new GeoPolyline3(GeoPoint3.Origin, new GeoPoint3(10, 0, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));
            var face = new GeoFace3(polygon);
            var circle = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 5.0);
            var obb = new GeoObb3(GeoPoint3.Origin, 4, 4, 4);
            var aabb = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(4, 4, 4));
            GeoSolid3 solid = aabb.ToObb().ToSolid();

            // Each of these had no instance form on the point before; every one must now agree with Core.
            Assert.Equal(Distance3.DistanceTo(polyline, probe), probe.DistanceTo(polyline), 9);
            Assert.Equal(Distance3.DistanceTo(polygon, probe), probe.DistanceTo(polygon), 9);
            Assert.Equal(Distance3.DistanceTo(face, probe), probe.DistanceTo(face), 9);
            Assert.Equal(Distance3.DistanceTo(circle, probe), probe.DistanceTo(circle), 9);
            Assert.Equal(Distance3.DistanceTo(obb, probe), probe.DistanceTo(obb), 9);
            Assert.Equal(Distance3.DistanceTo(aabb, probe), probe.DistanceTo(aabb), 9);
            Assert.Equal(Distance3.DistanceTo(solid, probe), probe.DistanceTo(solid), 6);

            Assert.Equal(PointLocation.OutSide, probe.LocateIn(polygon));
            Assert.Equal(PointLocation.OutSide, probe.LocateIn(circle));
            Assert.Equal(PointLocation.OutSide, probe.LocateIn(obb));
            Assert.Equal(PointLocation.OutSide, probe.LocateIn(aabb));
            Assert.Equal(PointLocation.OutSide, probe.LocateIn(solid));

            Assert.False(probe.IsPointOn(polyline));
            Assert.False(probe.IsPointOn(polygon));
            Assert.False(probe.IsPointOn(circle));
        }

        [Fact]
        public void ASegmentCanBeMeasuredAgainstTheFilledShapes()
        {
            var line = new GeoLine3(new GeoPoint3(1, 1, 4), new GeoPoint3(2, 2, 9));

            var triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));
            GeoSolid3 solid = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 1)).ToObb().ToSolid();

            Assert.Equal(Distance3.DistanceTo(line, triangle), line.DistanceTo(triangle), 9);
            Assert.Equal(Distance3.DistanceTo(line, polygon), line.DistanceTo(polygon), 9);
            Assert.Equal(Distance3.DistanceTo(line, solid), line.DistanceTo(solid), 6);
        }

        [Fact]
        public void PlanarRegionsCanBeComparedForOrientation()
        {
            var flat = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            var higher = new GeoPolygon3(
                new GeoPoint3(0, 0, 5), new GeoPoint3(10, 0, 5),
                new GeoPoint3(10, 10, 5), new GeoPoint3(0, 10, 5));

            var upright = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 0, 10), new GeoPoint3(0, 0, 10));

            Assert.True(Parallel3.IsParallel(flat, higher));
            Assert.True(Parallel3.IsParallel(flat, flat.Flip()));
            Assert.True(Parallel3.IsPerpendicular(flat, upright));

            // Parallel is not the same as coplanar: these two are five apart.
            Assert.False(Parallel3.IsCoplanar(flat, higher));
            Assert.True(Parallel3.IsCoplanar(flat, flat.Flip()));

            Assert.True(Parallel3.IsParallel(flat, GeoPlane3.XY));
            Assert.True(Parallel3.IsPerpendicular(upright, GeoPlane3.XY));

            Assert.True(Parallel3.IsCoplanar(new GeoFace3(flat), new GeoFace3(flat.Flip())));
            Assert.False(Parallel3.IsCoplanar(new GeoFace3(flat), new GeoFace3(higher)));
        }
    }
}
