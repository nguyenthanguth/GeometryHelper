using System;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Geometry
{
    public class CloneTests
    {
        #region Value types

        [Fact]
        public void Clone_Point_CopiesCoordinates()
        {
            var original = new GeoPoint2(3.5, -7.25);
            var clone = original.Clone();

            Assert.Equal(original, clone);
            Assert.Equal(original.X, clone.X);
            Assert.Equal(original.Y, clone.Y);
        }

        [Fact]
        public void Clone_Vector_CopiesComponents()
        {
            var original = new GeoVector2(3.0, 4.0);
            var clone = original.Clone();

            Assert.Equal(original, clone);
            Assert.Equal(5.0, clone.Length, 9);
        }

        [Fact]
        public void Clone_Line_CopiesEndpoints()
        {
            var original = new GeoLine2(new GeoPoint2(1, 2), new GeoPoint2(9, 14));
            var clone = original.Clone();

            Assert.Equal(original, clone);
            Assert.Equal(original.StartPoint, clone.StartPoint);
            Assert.Equal(original.EndPoint, clone.EndPoint);
            Assert.Equal(original.Length, clone.Length, 9);
        }

        [Fact]
        public void Clone_Circle_CopiesCenterAndRadius()
        {
            var original = new GeoCircle2(new GeoPoint2(2, -3), 7.5);
            var clone = original.Clone();

            Assert.Equal(original, clone);
            Assert.Equal(original.Center, clone.Center);
            Assert.Equal(original.Radius, clone.Radius);
        }

        [Fact]
        public void Clone_Rectangle_CopiesEveryField()
        {
            var original = new GeoRectangle2(new GeoPoint2(4, 5), 12.0, 3.5, Math.PI / 5.0);
            var clone = original.Clone();

            Assert.Equal(original, clone);
            Assert.Equal(original.Center, clone.Center);
            Assert.Equal(original.Width, clone.Width);
            Assert.Equal(original.Height, clone.Height);
            Assert.Equal(original.AngleRad, clone.AngleRad);
        }

        #endregion

        #region Reference types

        [Fact]
        public void Clone_Polygon_ProducesAnEqualButSeparateInstance()
        {
            var original = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0),
                new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var clone = original.Clone();

            // Equal by value, but a different object.
            Assert.Equal(original, clone);
            Assert.True(original == clone);
            Assert.False(ReferenceEquals(original, clone));

            Assert.Equal(original.VertexCount, clone.VertexCount);
            Assert.Equal(original.GetArea(), clone.GetArea(), 9);
            Assert.True(original.Vertices.SequenceEqual(clone.Vertices));
        }

        [Fact]
        public void Clone_Polygon_DoesNotShareTheVertexArray()
        {
            var original = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var clone = original.Clone();

            // The point of cloning a reference type is that the two hold separate storage.
            Assert.False(ReferenceEquals(original.Vertices, clone.Vertices));
        }

        [Fact]
        public void Clone_Polyline_ProducesAnEqualButSeparateInstance()
        {
            var original = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var clone = original.Clone();

            Assert.Equal(original, clone);
            Assert.True(original == clone);
            Assert.False(ReferenceEquals(original, clone));

            Assert.Equal(original.VertexCount, clone.VertexCount);
            Assert.Equal(original.Length, clone.Length, 9);
            Assert.True(original.Vertices.SequenceEqual(clone.Vertices));
        }

        [Fact]
        public void Clone_Polyline_KeepsItsEdges()
        {
            var path = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

            Assert.Equal(path.EdgeCount, path.Clone().EdgeCount);
            Assert.Equal(path.Length, path.Clone().Length, 9);
        }

        [Fact]
        public void Clone_Polyline_DoesNotShareTheVertexArray()
        {
            var original = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var clone = original.Clone();

            Assert.False(ReferenceEquals(original.Vertices, clone.Vertices));
        }

        #endregion

        #region Independence from Tolerance.Global

        [Fact]
        public void Clone_IsNotAffectedByAWiderGlobalTolerance()
        {
            // The public constructors drop consecutive vertices that are within Tolerance.Global of each
            // other. Clone must not re-run that filter, or widening the global would silently shrink a
            // polygon that was perfectly valid when it was built.
            var original = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(2, 0), new GeoPoint2(2, 2), new GeoPoint2(0, 2));
            var originalLine = new GeoPolyline2(
                new GeoPoint2(0, 0), new GeoPoint2(2, 0), new GeoPoint2(2, 2));

            Tolerance saved = Tolerance.Global;
            try
            {
                // Wide enough that every vertex would be swallowed by its neighbour.
                Tolerance.Global = new Tolerance(5.0, 5.0);

                var clonedPoly = original.Clone();
                Assert.Equal(4, clonedPoly.VertexCount);
                Assert.True(original.Vertices.SequenceEqual(clonedPoly.Vertices));

                var clonedLine = originalLine.Clone();
                Assert.Equal(3, clonedLine.VertexCount);
                Assert.True(originalLine.Vertices.SequenceEqual(clonedLine.Vertices));
            }
            finally
            {
                Tolerance.Global = saved;
            }
        }

        #endregion

        #region Round trip through operations

        [Fact]
        public void Clone_BehavesIdenticallyInOperations()
        {
            var probe = new GeoPoint2(4, 9);

            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var circle = new GeoCircle2(new GeoPoint2(5, 5), 4);
            var rect = new GeoRectangle2(new GeoPoint2(5, 5), 8, 6, 0.4);
            var poly = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));
            var polyline = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

            Assert.Equal(probe.DistanceTo(line), probe.DistanceTo(line.Clone()), 9);
            Assert.Equal(probe.DistanceTo(circle), probe.DistanceTo(circle.Clone()), 9);
            Assert.Equal(probe.DistanceTo(rect), probe.DistanceTo(rect.Clone()), 9);
            Assert.Equal(probe.DistanceTo(poly), probe.DistanceTo(poly.Clone()), 9);
            Assert.Equal(probe.DistanceTo(polyline), probe.DistanceTo(polyline.Clone()), 9);

            Assert.Equal(probe.GetClosestPointOnBoundary(poly), probe.GetClosestPointOnBoundary(poly.Clone()));
            Assert.Equal(poly.Contains(probe), poly.Clone().Contains(probe));
        }

        [Fact]
        public void Clone_OfATranslatedShape_MatchesTheTranslation()
        {
            var poly = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var shift = new GeoVector2(3, -4);

            // Cloning then translating and translating then cloning describe the same polygon.
            Assert.Equal(poly.Clone().Translate(shift), poly.Translate(shift).Clone());
        }

        #endregion
    }
}
