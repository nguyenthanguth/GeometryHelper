using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest
{
    /// <summary>
    /// Checks that every geometry type offers the same way to ask for a copy, and that a copy of a
    /// reference type shares no state with its original.
    /// </summary>
    public class CloneTests
    {
        [Fact]
        public void ValueTypesCloneToAnEqualValue()
        {
            GeoPoint3 point = new GeoPoint3(1.0, 2.0, 3.0);
            GeoVector3 vector = new GeoVector3(4.0, 5.0, 6.0);
            GeoLine3 line = new GeoLine3(point, point.Add(vector));
            GeoRay3 ray = new GeoRay3(point, vector);
            GeoPlane3 plane = new GeoPlane3(point, vector);
            GeoTriangle3 triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(1.0, 0.0, 0.0), new GeoPoint3(0.0, 1.0, 0.0));
            GeoCircle3 circle = new GeoCircle3(point, vector, 2.0);
            GeoAabb3 bounds = new GeoAabb3(GeoPoint3.Origin, point);
            GeoCoordinateSystem3 system = new GeoCoordinateSystem3(point, GeoVector3.XAxis, GeoVector3.YAxis);

            Assert.Equal(point, point.Clone());
            Assert.Equal(vector, vector.Clone());
            Assert.Equal(line, line.Clone());
            Assert.Equal(ray, ray.Clone());
            Assert.Equal(plane, plane.Clone());
            Assert.Equal(triangle, triangle.Clone());
            Assert.Equal(circle, circle.Clone());
            Assert.Equal(bounds, bounds.Clone());
            Assert.Equal(system, system.Clone());
        }

        [Fact]
        public void APolylineCopyIsASeparateObjectWithTheSameShape()
        {
            GeoPolyline3 original = new GeoPolyline3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(3.0, 0.0, 0.0),
                new GeoPoint3(3.0, 4.0, 5.0));

            GeoPolyline3 copy = original.Clone();

            Assert.NotSame(original, copy);
            Assert.Equal(original, copy);
            Assert.Equal(original.Length, copy.Length, 12);
            Assert.Equal(original.VertexCount, copy.VertexCount);
        }

        [Fact]
        public void APolygonCopyKeepsItsMeasuredNormalAndArea()
        {
            GeoPolygon3 original = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            GeoPolygon3 copy = original.Clone();

            Assert.NotSame(original, copy);
            Assert.Equal(original.Area, copy.Area, 12);
            Assert.True(original.Normal.IsEqualTo(copy.Normal));
        }

        [Fact]
        public void ACopyTakenAfterTheGlobalToleranceWasWidenedKeepsItsVertices()
        {
            // The public constructor filters against the global tolerance, so a copy that went through it
            // could silently lose vertices here. Clone goes through the private one for exactly this
            // reason, and the count must not change.
            GeoPolygon3 fine = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(1.0005, 0.0005, 0.0),
                new GeoPoint3(1.0, 1.0, 0.0),
                new GeoPoint3(0.0, 1.0, 0.0));

            Tolerance original = Tolerance.Global;

            try
            {
                Tolerance.Global = new Tolerance(0.01, 0.01);

                GeoPolygon3 copy = fine.Clone();

                Assert.Equal(fine.VertexCount, copy.VertexCount);
                Assert.Equal(fine.Area, copy.Area, 12);
            }
            finally
            {
                Tolerance.Global = original;
            }
        }

        [Fact]
        public void AFaceCopyCarriesItsHoles()
        {
            GeoPolygon3 boundary = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            GeoPolygon3 hole = new GeoPolygon3(
                new GeoPoint3(4.0, 4.0, 0.0),
                new GeoPoint3(6.0, 4.0, 0.0),
                new GeoPoint3(6.0, 6.0, 0.0),
                new GeoPoint3(4.0, 6.0, 0.0));

            GeoFace3 original = new GeoFace3(boundary, new[] { hole });
            GeoFace3 copy = original.Clone();

            Assert.NotSame(original, copy);
            Assert.NotSame(original.Holes[0], copy.Holes[0]);
            Assert.Equal(original.Area, copy.Area, 12);
            Assert.Equal(original, copy);
        }

        [Fact]
        public void ASolidCopyCarriesItsFacesAndOpenings()
        {
            GeoSolid3 slab = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0)).ToObb().ToSolid();
            GeoSolid3 duct = new GeoAabb3(new GeoPoint3(4.0, 4.0, 4.0), new GeoPoint3(6.0, 6.0, 6.0)).ToObb().ToSolid();

            GeoSolid3 original = slab.WithOpenings(new[] { duct });
            GeoSolid3 copy = original.Clone();

            Assert.NotSame(original, copy);
            Assert.NotSame(original.Faces[0], copy.Faces[0]);
            Assert.Equal(original.Faces.Count, copy.Faces.Count);
            Assert.Equal(original.Openings.Count, copy.Openings.Count);
            Assert.Equal(original.NetVolume, copy.NetVolume, 6);
        }

        [Fact]
        public void ABoxCopyIsASeparateObjectWithTheSamePlacement()
        {
            GeoObb3 original = new GeoObb3(
                new GeoPoint3(1.0, 2.0, 3.0), 4.0, 5.0, 6.0,
                new GeoVector3(1.0, 1.0, 0.0),
                new GeoVector3(-1.0, 1.0, 0.0));

            GeoObb3 copy = original.Clone();

            Assert.NotSame(original, copy);
            Assert.Equal(original, copy);
            Assert.Equal(original.Volume, copy.Volume, 12);
        }

        [Fact]
        public void ATransformCopyIsIndependentOfTheOriginal()
        {
            GeoTransform3 original = GeoTransform3.RotationZ(0.5);
            GeoTransform3 copy = original.Clone();

            Assert.NotSame(original, copy);
            Assert.Equal(original, copy);
        }
    }
}
