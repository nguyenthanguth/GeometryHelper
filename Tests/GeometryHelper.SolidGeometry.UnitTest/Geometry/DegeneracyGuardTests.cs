using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the guards that decide when a quantity has collapsed. The failure they exist to prevent is
    /// not an exception but a number: dividing by something merely tiny yields infinities and NaN that
    /// then travel silently through everything downstream.
    /// </summary>
    public class DegeneracyGuardTests
    {
        #region Inverting a transformation

        [Fact]
        public void TryGetInverse_RefusesAFlatteningTransformation()
        {
            GeoTransform3 flatten = GeoTransform3.Scaling(1.0, 1.0, 0.0);

            Assert.False(flatten.TryGetInverse(out GeoTransform3 inverse));
            Assert.True(inverse.IsEqualTo(GeoTransform3.Identity));
        }

        [Fact]
        public void TryGetInverse_RefusesAxesThatAreNearlyCoplanar()
        {
            // Three unit-length axes tilted almost into one plane. The determinant is about 1e-12 while
            // every axis is still full length, so a bare comparison against zero calls this invertible and
            // hands back a matrix of numbers around 1e12.
            GeoTransform3 nearlyFlat = FromAxes(
                new GeoVector3(1, 0, 0),
                new GeoVector3(0, 1, 0),
                new GeoVector3(0.6, 0.8, 1E-12));

            Assert.False(nearlyFlat.TryGetInverse(out _));
        }

        [Fact]
        public void Inverse_ThrowsForAxesThatAreNearlyCoplanar()
        {
            GeoTransform3 nearlyFlat = FromAxes(
                new GeoVector3(1, 0, 0),
                new GeoVector3(0, 1, 0),
                new GeoVector3(0.6, 0.8, 1E-12));

            Assert.Throws<InvalidOperationException>(() => nearlyFlat.Inverse());
        }

        [Fact]
        public void TryGetInverse_AcceptsASmallButHonestTransformation()
        {
            // Uniformly scaled down by a millionth. The determinant is 1e-18, far below anything an
            // absolute threshold would accept, yet the transformation is perfectly invertible: judging
            // the determinant against the size of the axes is what tells the two cases apart.
            GeoTransform3 tiny = GeoTransform3.Scaling(1E-6);

            Assert.True(tiny.TryGetInverse(out GeoTransform3 inverse));

            GeoPoint3 there = tiny.Transform(new GeoPoint3(3, 4, 5));
            GeoPoint3 back = inverse.Transform(there);

            Assert.True(back.IsEqualTo(new GeoPoint3(3, 4, 5)));
        }

        [Fact]
        public void TryGetInverse_AcceptsOrdinaryTransformations()
        {
            GeoTransform3 moved = GeoTransform3.Translation(new GeoVector3(3, -4, 5))
                * GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.9);

            Assert.True(moved.TryGetInverse(out GeoTransform3 inverse));

            GeoPoint3 point = new GeoPoint3(7, 8, 9);
            Assert.True(inverse.Transform(moved.Transform(point)).IsEqualTo(point));
        }

        [Fact]
        public void TryGetInverse_WithALooserTolerance_RefusesMore()
        {
            // Axes tilted enough to give a determinant of about 1e-3: sound under the default tolerance,
            // collapsed under a coarse one.
            GeoTransform3 tilted = FromAxes(
                new GeoVector3(1, 0, 0),
                new GeoVector3(0, 1, 0),
                new GeoVector3(0.6, 0.8, 1E-3));

            Assert.True(tilted.TryGetInverse(out _, Tolerance.Global));
            Assert.False(tilted.TryGetInverse(out _, new Tolerance(1E-1, 1E-1)));
        }

        /// <summary>
        /// Builds a transformation whose three transformed axes are the vectors given.
        /// </summary>
        private static GeoTransform3 FromAxes(GeoVector3 x, GeoVector3 y, GeoVector3 z)
        {
            double[,] values =
            {
                { x.X, y.X, z.X, 0.0 },
                { x.Y, y.Y, z.Y, 0.0 },
                { x.Z, y.Z, z.Z, 0.0 },
                { 0.0, 0.0, 0.0, 1.0 }
            };

            return new GeoTransform3(values);
        }

        #endregion

        #region Centroids of collapsed shapes

        [Fact]
        public void Polygon_ThatCancelsItselfOut_IsRefusedAtConstruction()
        {
            // A bow tie: the two lobes are wound against each other, so the net area cancels. This is the
            // shape whose centroid would divide by nothing, and the constructor is where it is stopped —
            // which is why the guard inside GeoPolygon3.Centroid is a backstop rather than a path a caller
            // can reach. The equivalent guard on GeoSolid3.Centroid is reachable, and is covered below.
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 10, 0),
                new GeoPoint3(0, 10, 0),
                new GeoPoint3(10, 1E-17, 0)));
        }

        [Fact]
        public void PolygonCentroid_OfAnOrdinaryShape_IsUnaffected()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(4, 0, 0),
                new GeoPoint3(4, 4, 0), new GeoPoint3(0, 4, 0));

            Assert.True(square.Centroid.IsEqualTo(new GeoPoint3(2, 2, 0)));
        }

        [Fact]
        public void SolidCentroid_OfAShellWithNoVolume_FallsBackToTheVertexAverage()
        {
            // Two coincident triangular sheets facing opposite ways: a closed shell enclosing nothing, so
            // the signed volume cancels and there is no centroid to weight by it.
            GeoPoint3 a = new GeoPoint3(0, 0, 0);
            GeoPoint3 b = new GeoPoint3(6, 0, 0);
            GeoPoint3 c = new GeoPoint3(0, 6, 0);

            GeoSolid3 sheet = new GeoSolid3(
                new GeoFace3(new GeoPolygon3(a, b, c)),
                new GeoFace3(new GeoPolygon3(c, b, a)),
                new GeoFace3(new GeoPolygon3(a, b, c)),
                new GeoFace3(new GeoPolygon3(c, b, a)));

            Assert.Equal(0.0, sheet.Volume, 9);

            GeoPoint3 centroid = sheet.Centroid;

            AssertFinite(centroid);
            Assert.True(centroid.IsEqualTo(new GeoPoint3(2, 2, 0)));
        }

        [Fact]
        public void SolidCentroid_OfAnOrdinaryBody_IsUnaffected()
        {
            GeoSolid3 box = Boxes.Unit(0, 0, 0, 4, 6, 8);

            Assert.True(box.Centroid.IsEqualTo(new GeoPoint3(2, 3, 4)));
        }

        private static void AssertFinite(GeoPoint3 point)
        {
            Assert.False(double.IsNaN(point.X) || double.IsInfinity(point.X));
            Assert.False(double.IsNaN(point.Y) || double.IsInfinity(point.Y));
            Assert.False(double.IsNaN(point.Z) || double.IsInfinity(point.Z));
        }

        #endregion

        /// <summary>
        /// Builds axis-aligned boxes for the tests that need an ordinary body.
        /// </summary>
        private static class Boxes
        {
            public static GeoSolid3 Unit(double x0, double y0, double z0, double x1, double y1, double z1)
            {
                GeoPoint3[] baseCcw =
                {
                    new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y0, z0),
                    new GeoPoint3(x1, y1, z0), new GeoPoint3(x0, y1, z0)
                };

                GeoPoint3[] top = new GeoPoint3[4];
                GeoPoint3[] bottomReversed = new GeoPoint3[4];

                for (int i = 0; i < 4; i++)
                {
                    top[i] = new GeoPoint3(baseCcw[i].X, baseCcw[i].Y, z1);
                    bottomReversed[i] = baseCcw[3 - i];
                }

                GeoFace3[] faces = new GeoFace3[6];
                faces[0] = new GeoFace3(new GeoPolygon3(bottomReversed));
                faces[1] = new GeoFace3(new GeoPolygon3(top));

                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    faces[2 + i] = new GeoFace3(new GeoPolygon3(baseCcw[i], baseCcw[j], top[j], top[i]));
                }

                return new GeoSolid3(faces);
            }
        }
    }
}
