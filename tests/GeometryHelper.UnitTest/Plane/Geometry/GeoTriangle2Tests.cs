using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The triangle of the plane: its measures, its circles, its barycentric coordinates, where a point is, how it
    /// moves, and what it turns into.
    /// </summary>
    public class GeoTriangle2Tests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        /// <summary>A right triangle with legs 4 and 3 along the axes, counter-clockwise.</summary>
        private static GeoTriangle2 Right() => new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(0, 3));

        [Fact]
        public void ItKeepsItsVerticesInTheOrderGiven()
        {
            GeoTriangle2 t = Right();

            Assert.Equal(new GeoPoint2(0, 0), t[0]);
            Assert.Equal(new GeoPoint2(4, 0), t[1]);
            Assert.Equal(new GeoPoint2(0, 3), t[2]);
            Assert.Equal(new[] { t.A, t.B, t.C }, t.GetVertices());
            Assert.Throws<ArgumentOutOfRangeException>(() => t[3]);
            Assert.Throws<ArgumentOutOfRangeException>(() => t.GetEdgeAt(-1));
        }

        [Fact]
        public void ItsEdgesRunFromEachVertexToTheNext()
        {
            GeoTriangle2 t = Right();
            GeoLine2[] edges = t.GetEdges();

            Assert.Equal(3, edges.Length);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(t[i], edges[i].StartPoint);
                Assert.Equal(t[(i + 1) % 3], edges[i].EndPoint);
                Assert.Equal(edges[i], t.GetEdgeAt(i));
            }
        }

        [Fact]
        public void ItMeasuresAreaPerimeterAndCentroid()
        {
            GeoTriangle2 t = Right();

            Assert.Equal(6.0, t.SignedArea, 12);
            Assert.Equal(6.0, t.Area, 12);
            Assert.False(t.IsClockwise);
            Assert.Equal(12.0, t.Length, 12);
            Assert.True(t.Centroid.IsEqualTo(new GeoPoint2(4.0 / 3.0, 1.0), Tolerance));

            GeoTriangle2 turned = t.Reverse();
            Assert.Equal(-6.0, turned.SignedArea, 12);
            Assert.Equal(6.0, turned.Area, 12);
            Assert.True(turned.IsClockwise);
        }

        [Fact]
        public void ItsAnglesAddUpToHalfATurn()
        {
            GeoTriangle2 t = Right();
            double[] angles = t.GetAngles();

            Assert.Equal(Math.PI / 2, angles[0], 12);
            Assert.Equal(Math.Atan2(3, 4), angles[1], 12);
            Assert.Equal(Math.Atan2(4, 3), angles[2], 12);
            Assert.Equal(Math.PI, angles.Sum(), 12);
            Assert.Equal(Math.Atan2(3, 4), t.MinAngle, 12);
        }

        [Fact]
        public void AVertexOnAnotherHasAnAngleOfNoughtRatherThanNoNumber()
        {
            var t = new GeoTriangle2(new GeoPoint2(1, 1), new GeoPoint2(1, 1), new GeoPoint2(5, 2));

            Assert.All(t.GetAngles(), angle => Assert.False(double.IsNaN(angle)));
            Assert.Equal(0.0, t.MinAngle, 12);
        }

        [Fact]
        public void TheCircumcircleOfARightTriangleStandsOnItsHypotenuse()
        {
            // Far from the origin, so that the arithmetic must work from a corner and not from zero.
            var offset = new GeoVector2(612345.25, -98765.5);
            GeoTriangle2 t = Right().Translate(offset);

            Assert.True(t.TryGetCircumcircle(out GeoCircle2 circle));
            Assert.True(circle.Center.IsEqualTo(new GeoPoint2(2, 1.5).Add(offset), new Tolerance(1E-9, 1E-9)));
            Assert.Equal(2.5, circle.Radius, 9);

            foreach (GeoPoint2 corner in t.GetVertices())
            {
                Assert.Equal(2.5, corner.DistanceTo(circle.Center), 9);
            }
        }

        [Fact]
        public void TheIncircleOfAThreeFourFiveTriangleHasARadiusOfOne()
        {
            Assert.True(Right().TryGetIncircle(out GeoCircle2 circle));
            Assert.True(circle.Center.IsEqualTo(new GeoPoint2(1, 1), new Tolerance(1E-12, 1E-12)));
            Assert.Equal(1.0, circle.Radius, 12);

            // The same circle whichever way the triangle runs.
            Assert.True(Right().Reverse().TryGetIncircle(out GeoCircle2 turned));
            Assert.Equal(circle.Radius, turned.Radius, 12);
        }

        [Fact]
        public void ATriangleOfNoAreaHasNeitherCircle()
        {
            var flat = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), new GeoPoint2(3, 0));

            Assert.True(flat.IsDegenerate());
            Assert.False(flat.TryGetCircumcircle(out _));
            Assert.False(flat.TryGetIncircle(out _));
        }

        [Fact]
        public void DegeneracyIsTwiceTheAreaAgainstTheVectorTolerance()
        {
            var sliver = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(1000, 0), new GeoPoint2(500, 1E-6));
            var thin = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(1000, 0), new GeoPoint2(500, 1E-4));

            // Twice the area: 1E-3 against 0.01, and 0.1 against 0.01.
            Assert.True(sliver.IsDegenerate(Tolerance));
            Assert.False(thin.IsDegenerate(Tolerance));
            Assert.False(thin.IsDegenerate(new Tolerance()));
            Assert.True(new GeoTriangle2(new GeoPoint2(2, 2), new GeoPoint2(2, 2), new GeoPoint2(2, 2)).IsDegenerate(new Tolerance()));
        }

        [Fact]
        public void BarycentricCoordinatesWeighTheVertices()
        {
            GeoTriangle2 t = Right();

            Assert.True(t.TryGetBarycentric(t.A, out double u, out double v, out double w));
            Assert.Equal((1.0, 0.0, 0.0), (Math.Round(u, 12), Math.Round(v, 12), Math.Round(w, 12)));

            Assert.True(t.TryGetBarycentric(t.Centroid, out u, out v, out w));
            Assert.Equal(1.0 / 3.0, u, 12);
            Assert.Equal(1.0 / 3.0, v, 12);
            Assert.Equal(1.0 / 3.0, w, 12);

            // A point outside has a negative weight, and the point comes back from its weights.
            var outside = new GeoPoint2(5, 5);
            Assert.True(t.TryGetBarycentric(outside, out u, out v, out w));
            Assert.True(u < 0.0);
            Assert.Equal(1.0, u + v + w, 12);
            Assert.True(t.GetPointAtBarycentric(u, v, w).IsEqualTo(outside, new Tolerance(1E-12, 1E-12)));

            // Clockwise, the weights belong to the same vertices.
            Assert.True(t.Reverse().TryGetBarycentric(t.B, out u, out v, out w));
            Assert.Equal(1.0, w, 12);
        }

        [Fact]
        public void ATriangleOfNoAreaHasNoBarycentricCoordinates()
        {
            var flat = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(1, 0), new GeoPoint2(2, 0));

            Assert.False(flat.TryGetBarycentric(new GeoPoint2(1, 1), out double u, out double v, out double w));
            Assert.Equal((0.0, 0.0, 0.0), (u, v, w));
        }

        [Fact]
        public void APointIsInsideOnOrOutside()
        {
            GeoTriangle2 t = Right();

            Assert.Equal(PointLocation.Inside, t.Locate(new GeoPoint2(1, 1), Tolerance));
            Assert.Equal(PointLocation.OnSide, t.Locate(new GeoPoint2(2, 0.005), Tolerance));
            Assert.Equal(PointLocation.OnSide, t.Locate(new GeoPoint2(2, 1.5), Tolerance));
            Assert.Equal(PointLocation.OnSide, t.Locate(t.C, Tolerance));
            Assert.Equal(PointLocation.OutSide, t.Locate(new GeoPoint2(3, 3), Tolerance));
            Assert.Equal(PointLocation.OutSide, t.Locate(new GeoPoint2(-0.5, 1), Tolerance));

            Assert.True(t.Contains(new GeoPoint2(1, 1), Tolerance));
            Assert.True(t.Contains(new GeoPoint2(2, 1.5), Tolerance));
            Assert.False(t.Contains(new GeoPoint2(3, 3), Tolerance));
        }

        [Fact]
        public void AClockwiseTriangleHasTheSameInside()
        {
            GeoTriangle2 t = Right().Reverse();

            Assert.Equal(PointLocation.Inside, t.Locate(new GeoPoint2(1, 1), Tolerance));
            Assert.Equal(PointLocation.OutSide, t.Locate(new GeoPoint2(3, 3), Tolerance));
        }

        [Fact]
        public void ATriangleOfNoAreaHasNoInside()
        {
            var flat = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(8, 0));
            var point = new GeoTriangle2(new GeoPoint2(1, 1), new GeoPoint2(1, 1), new GeoPoint2(1, 1));

            Assert.Equal(PointLocation.OnSide, flat.Locate(new GeoPoint2(6, 0), Tolerance));
            Assert.Equal(PointLocation.OutSide, flat.Locate(new GeoPoint2(6, 1), Tolerance));
            Assert.Equal(PointLocation.OnSide, point.Locate(new GeoPoint2(1, 1.005), Tolerance));
            Assert.Equal(PointLocation.OutSide, point.Locate(new GeoPoint2(1, 2), Tolerance));
        }

        [Fact]
        public void ItMovesAndTurnsCornerByCorner()
        {
            GeoTriangle2 t = Right();
            var move = new GeoVector2(10, -2);

            Assert.Equal(new GeoTriangle2(t.A.Add(move), t.B.Add(move), t.C.Add(move)), t.Translate(move));
            Assert.Equal(t.Translate(move), t + move);
            Assert.Equal(t, t + move - move);

            GeoTriangle2 turned = t.RotateBy(Math.PI / 2, GeoPoint2.Origin);
            Assert.True(turned.B.IsEqualTo(new GeoPoint2(0, 4), new Tolerance(1E-12, 1E-12)));
            Assert.Equal(t.SignedArea, turned.SignedArea, 9);
        }

        [Fact]
        public void TransformingThroughEitherFormGivesTheSameTriangle()
        {
            GeoTriangle2 t = Right();
            GeoTransform2 motion = GeoTransform2.Translation(new GeoVector2(3, -4)).Multiply(GeoTransform2.Rotation(0.7));

            Assert.Equal(motion.Transform(t), t.TransformBy(motion));
            Assert.Equal(new GeoTriangle2(motion.Transform(t.A), motion.Transform(t.B), motion.Transform(t.C)), t.TransformBy(motion));
            Assert.Equal(t.Area, t.TransformBy(motion).Area, 9);
            Assert.Throws<ArgumentNullException>(() => t.TransformBy(null));
        }

        [Fact]
        public void AMirrorTurnsTheTriangleRound()
        {
            GeoTriangle2 mirrored = Right().TransformBy(GeoTransform2.Scaling(-1.0, 1.0));

            Assert.True(mirrored.IsClockwise);
            Assert.Equal(6.0, mirrored.Area, 12);
        }

        [Fact]
        public void ItReadsAsAPolygonAndAsAClosedChain()
        {
            GeoTriangle2 t = Right();

            GeoPolygon2 polygon = t.ToPolygon();
            Assert.Equal(3, polygon.VertexCount);
            Assert.Equal(t.SignedArea, polygon.SignedArea, 12);

            GeoPolyline2 chain = t.ToPolyline();
            Assert.Equal(4, chain.VertexCount);
            Assert.Equal(t.Length, chain.Length, 12);

            var pinched = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(0, 0), new GeoPoint2(1, 0));
            Assert.Throws<ArgumentException>(() => pinched.ToPolygon());
        }

        [Fact]
        public void ItGoesIntoSpaceAndComesBack()
        {
            var frame = new GeoCoordinateSystem3(new GeoPlane3(new GeoPoint3(100, -50, 30), new GeoVector3(0.3, -0.4, 0.86).Normalize()));
            GeoTriangle2 t = Right();

            GeoTriangle3 lifted = t.ToTriangle3(frame);
            Assert.Equal(t.Area, lifted.Area, 9);

            GeoTriangle2 back = lifted.ProjectToTriangle2(frame);
            Assert.True(back.IsEqualTo(t, new Tolerance(1E-9, 1E-9)));
            Assert.False(back.IsClockwise);
        }

        [Fact]
        public void EqualityIsExactAndIsEqualToForgivesWhereTheLoopStarts()
        {
            GeoTriangle2 t = Right();
            var rolled = new GeoTriangle2(t.B, t.C, t.A);

            Assert.Equal(t, t.Clone());
            Assert.True(t == t.Clone());
            Assert.True(t != rolled);
            Assert.Equal(t.GetHashCode(), t.Clone().GetHashCode());

            Assert.True(t.IsEqualTo(rolled, Tolerance));
            Assert.False(t.IsEqualTo(t.Reverse(), Tolerance));
            Assert.True(t.IsEqualTo(t.Translate(new GeoVector2(0.004, 0)), Tolerance));
            Assert.False(t.IsEqualTo(t.Translate(new GeoVector2(0.02, 0)), Tolerance));
        }

        [Fact]
        public void ItIsValidWhenEveryCornerIs()
        {
            Assert.True(Right().IsValid);
            Assert.False(new GeoTriangle2(new GeoPoint2(double.NaN, 0), new GeoPoint2(1, 0), new GeoPoint2(0, 1)).IsValid);
        }

        [Fact]
        public void ItSaysWhatItIs()
        {
            Assert.Equal("GeoTriangle2[(0, 0), (4, 0), (0, 3)]", Right().ToString());
        }
    }
}
