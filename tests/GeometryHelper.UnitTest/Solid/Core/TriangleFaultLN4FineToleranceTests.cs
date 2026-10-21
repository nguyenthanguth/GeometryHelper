using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A triangle with an area at the tolerance a query is asked within is read as one, though it has none at the global
    /// tolerance.
    /// </summary>
    /// <remarks>
    /// <see cref="Containment3.Locate(GeoTriangle3, GeoPoint3, Tolerance)"/> judged the area at the tolerance it was given and
    /// then asked <see cref="GeoTriangle3.TryGetBarycentric(GeoPoint3, out double, out double, out double)"/>, which judges it
    /// again at the global one: at 1E-5 the triangle 100 long and 0.0005 high has an area, and a point in its middle read
    /// outside it, a segment through it collided and crossed nothing, and 1 954 of 2 000 points just inside a chamfer 0.0004
    /// across of a body prepared at 1E-5 read outside it. The containment reads the coordinates as they stand now; the
    /// public method keeps its global reading.
    /// </remarks>
    public class TriangleFaultLN4FineToleranceTests
    {
        private static readonly Tolerance Fine = new Tolerance(1E-5, 1E-7, 1E-7, 1E-5);

        private static readonly GeoTriangle3 Thin = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 0.0005, 0));

        [Fact]
        public void APointInTheMiddleOfAThinTriangle_IsInsideItAtAFineTolerance()
        {
            Assert.False(Thin.IsDegenerate(Fine));
            Assert.Equal(PointLocation.Inside, Thin.Locate(new GeoPoint3(50, 0.0002, 0), Fine));
        }

        [Fact]
        public void TheGlobalReadingOfTheBarycentricCoordinates_IsKept()
        {
            // At the global tolerance the triangle has no area, and the public method says so.
            Assert.True(Thin.IsDegenerate());
            Assert.False(Thin.TryGetBarycentric(new GeoPoint3(50, 0.0002, 0), out _, out _, out _));
        }

        [Fact]
        public void ASegmentAndARayThroughAThinTriangle_CrossItAtAFineTolerance()
        {
            var segment = new GeoLine3(new GeoPoint3(50, 0.0002, -1), new GeoPoint3(50, 0.0002, 1));
            var ray = new GeoRay3(new GeoPoint3(50, 0.0002, -1), new GeoVector3(0, 0, 1));

            Assert.True(segment.CollidesWith(Thin, Fine));
            Assert.True(Assert.Single(Thin.GetIntersections(segment, Fine)).DistanceTo(new GeoPoint3(50, 0.0002, 0)) < 1E-12);
            Assert.True(Assert.Single(Thin.GetIntersections(ray, Fine)).DistanceTo(new GeoPoint3(50, 0.0002, 0)) < 1E-12);
        }

        [Fact]
        public void PointsJustInsideAChamferOfAPreparedBody_AreInIt()
        {
            // The box 100 by 100 by 100 with its edge x = y = 100 chamfered 0.0004 across: its surface has triangles 0.0004
            // wide, which have an area at 1E-5 and none at the global tolerance.
            const double c = 0.0004;
            var ring = new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100 - c), new GeoPoint2(100 - c, 100), new GeoPoint2(0, 100) };
            GeoSolid3 solid = Prism(ring, 0, 100);
            var prepared = new GeoPreparedSolid3(solid, Fine);
            var random = new Random(3);

            for (int i = 0; i < 200; i++)
            {
                double d = 0.00021 + random.NextDouble() * 0.0003;
                var point = new GeoPoint3(100 - d, 100 - d, 1 + random.NextDouble() * 98);

                Assert.Equal(Containment3.Locate(solid, point, Fine), prepared.Locate(point, Fine));
                Assert.Equal(PointLocation.Inside, prepared.Locate(point, Fine));
            }
        }

        private static GeoSolid3 Prism(GeoPoint2[] ring, double bottom, double top)
        {
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(ring.Reverse().Select(p => new GeoPoint3(p.X, p.Y, bottom)), Fine)),
                new GeoFace3(new GeoPolygon3(ring.Select(p => new GeoPoint3(p.X, p.Y, top)), Fine)),
            };

            for (int i = 0; i < ring.Length; i++)
            {
                GeoPoint2 a = ring[i];
                GeoPoint2 b = ring[(i + 1) % ring.Length];

                faces.Add(new GeoFace3(new GeoPolygon3(
                    new[] { new GeoPoint3(a.X, a.Y, bottom), new GeoPoint3(b.X, b.Y, bottom), new GeoPoint3(b.X, b.Y, top), new GeoPoint3(a.X, a.Y, top) },
                    Fine)));
            }

            return new GeoSolid3(faces);
        }
    }
}
