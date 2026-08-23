using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers vector arithmetic, normalization, angles and the degenerate cases around them.
    /// </summary>
    public class GeoVector3Tests
    {
        private const double Precision = 1E-9;

        [Fact]
        public void LengthAndLengthSquaredAgree()
        {
            GeoVector3 v = new GeoVector3(3.0, 4.0, 12.0);

            Assert.Equal(13.0, v.Length, 9);
            Assert.Equal(169.0, v.LengthSquared, 9);
        }

        [Fact]
        public void CrossProductOfWorldAxesFollowsTheRightHandRule()
        {
            Assert.Equal(GeoVector3.ZAxis, GeoVector3.XAxis.CrossProduct(GeoVector3.YAxis));
            Assert.Equal(GeoVector3.XAxis, GeoVector3.YAxis.CrossProduct(GeoVector3.ZAxis));
            Assert.Equal(GeoVector3.YAxis, GeoVector3.ZAxis.CrossProduct(GeoVector3.XAxis));
        }

        [Fact]
        public void CrossProductOfParallelVectorsIsZero()
        {
            GeoVector3 v = new GeoVector3(2.0, -3.0, 5.0);

            Assert.True(v.CrossProduct(v.Multiply(4.0)).IsZeroLength());
        }

        [Fact]
        public void TripleProductIsZeroForCoplanarVectorsAndIsTheVolumeOtherwise()
        {
            Assert.Equal(0.0, GeoVector3.XAxis.TripleProduct(GeoVector3.YAxis, new GeoVector3(1.0, 1.0, 0.0)), 9);
            Assert.Equal(1.0, GeoVector3.XAxis.TripleProduct(GeoVector3.YAxis, GeoVector3.ZAxis), 9);
        }

        [Fact]
        public void NormalizeGivesUnitLength()
        {
            GeoVector3 unit = new GeoVector3(3.0, 4.0, 12.0).Normalize();

            Assert.True(unit.IsUnitLength());
            Assert.Equal(1.0, unit.Length, 9);
        }

        [Fact]
        public void NormalizeRefusesAZeroLengthVector()
        {
            Assert.Throws<InvalidOperationException>(() => GeoVector3.Zero.Normalize());
        }

        [Fact]
        public void TryGetNormalReportsFailureInsteadOfThrowing()
        {
            Assert.False(GeoVector3.Zero.TryGetNormal(out GeoVector3 normal));
            Assert.Equal(GeoVector3.Zero, normal);

            Assert.True(GeoVector3.XAxis.Multiply(7.0).TryGetNormal(out GeoVector3 unit));
            Assert.True(unit.IsEqualTo(GeoVector3.XAxis));
        }

        [Fact]
        public void DivideByZeroIsRejectedRatherThanProducingInfinities()
        {
            Assert.Throws<DivideByZeroException>(() => GeoVector3.XAxis.Divide(0.0));
        }

        [Fact]
        public void PerpendicularVectorIsUnitLengthAndSquareToTheSource()
        {
            GeoVector3[] samples =
            {
                GeoVector3.XAxis,
                GeoVector3.YAxis,
                GeoVector3.ZAxis,
                new GeoVector3(1.0, 2.0, 3.0),
                new GeoVector3(-5.0, 0.0, 0.001)
            };

            foreach (GeoVector3 v in samples)
            {
                GeoVector3 perpendicular = v.GetPerpendicularVector();

                Assert.True(perpendicular.IsUnitLength());
                Assert.True(perpendicular.IsPerpendicularTo(v));
            }
        }

        [Fact]
        public void AngleBetweenAxesIsARightAngleAndBetweenOppositesIsStraight()
        {
            Assert.Equal(Math.PI / 2.0, GeoVector3.XAxis.GetAngleTo(GeoVector3.YAxis), 9);
            Assert.Equal(Math.PI, GeoVector3.XAxis.GetAngleTo(GeoVector3.XAxis.Negate()), 9);
            Assert.Equal(0.0, GeoVector3.XAxis.GetAngleTo(GeoVector3.XAxis.Multiply(3.0)), 9);
        }

        [Fact]
        public void AngleStaysAccurateForNearlyCollinearVectors()
        {
            // Acos would lose most of its significant digits here; Atan2 keeps them.
            GeoVector3 a = GeoVector3.XAxis;
            GeoVector3 b = new GeoVector3(1.0, 1E-8, 0.0);

            Assert.Equal(1E-8, a.GetAngleTo(b), 15);
        }

        [Fact]
        public void SignedAngleFlipsWithTheReferenceAxis()
        {
            double positive = GeoVector3.XAxis.GetSignedAngleTo(GeoVector3.YAxis, GeoVector3.ZAxis);
            double negative = GeoVector3.XAxis.GetSignedAngleTo(GeoVector3.YAxis, GeoVector3.ZAxis.Negate());

            Assert.Equal(Math.PI / 2.0, positive, 9);
            Assert.Equal(-Math.PI / 2.0, negative, 9);
        }

        [Fact]
        public void AngleToAZeroLengthVectorIsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => GeoVector3.XAxis.GetAngleTo(GeoVector3.Zero));
            Assert.Throws<InvalidOperationException>(() => GeoVector3.Zero.GetAngleTo(GeoVector3.XAxis));
        }

        [Fact]
        public void RotationAroundAnAxisPreservesLengthAndReturnsAfterAFullTurn()
        {
            GeoVector3 v = new GeoVector3(1.0, 2.0, 3.0);
            GeoVector3 axis = new GeoVector3(1.0, 1.0, 0.0);

            GeoVector3 quarter = v.RotateBy(Math.PI / 2.0, axis);
            GeoVector3 full = v.RotateBy(2.0 * Math.PI, axis);

            Assert.Equal(v.Length, quarter.Length, 9);
            Assert.True(full.IsEqualTo(v));
        }

        [Fact]
        public void RotatingXAroundZByAQuarterTurnGivesY()
        {
            GeoVector3 rotated = GeoVector3.XAxis.RotateBy(Math.PI / 2.0, GeoVector3.ZAxis);

            Assert.True(rotated.IsEqualTo(GeoVector3.YAxis));
        }

        [Fact]
        public void ProjectionOntoAnAxisKeepsOnlyTheComponentAlongIt()
        {
            GeoVector3 v = new GeoVector3(3.0, 4.0, 5.0);

            Assert.True(v.ProjectOnto(GeoVector3.XAxis).IsEqualTo(new GeoVector3(3.0, 0.0, 0.0)));
            Assert.True(v.ProjectOntoPlane(GeoVector3.ZAxis).IsEqualTo(new GeoVector3(3.0, 4.0, 0.0)));
        }

        [Fact]
        public void ProjectionOntoADegenerateAxisIsZeroRatherThanNaN()
        {
            Assert.Equal(GeoVector3.Zero, new GeoVector3(1.0, 2.0, 3.0).ProjectOnto(GeoVector3.Zero));
        }

        [Fact]
        public void ParallelIsNotTheSameAsCodirectional()
        {
            GeoVector3 forward = GeoVector3.XAxis;
            GeoVector3 backward = GeoVector3.XAxis.Negate();

            Assert.True(forward.IsParallelTo(backward));
            Assert.False(forward.IsCodirectionalTo(backward));
            Assert.True(forward.IsCodirectionalTo(forward.Multiply(2.0)));
        }

        [Fact]
        public void DegenerateVectorsAreNeitherParallelNorPerpendicular()
        {
            Assert.False(GeoVector3.Zero.IsParallelTo(GeoVector3.XAxis));
            Assert.False(GeoVector3.Zero.IsPerpendicularTo(GeoVector3.XAxis));
            Assert.False(GeoVector3.Zero.IsCodirectionalTo(GeoVector3.Zero));
        }

        [Fact]
        public void CoplanarityOfVectorsIsScaleInvariant()
        {
            GeoVector3 a = new GeoVector3(1.0, 0.0, 0.0);
            GeoVector3 b = new GeoVector3(0.0, 1.0, 0.0);
            GeoVector3 inPlane = new GeoVector3(1000.0, 2000.0, 0.0);
            GeoVector3 outOfPlane = new GeoVector3(1.0, 1.0, 1.0);

            Assert.True(a.IsCoplanarWith(b, inPlane));
            Assert.False(a.IsCoplanarWith(b, outOfPlane));
        }

        [Fact]
        public void OperatorsMatchTheNamedMethods()
        {
            GeoVector3 a = new GeoVector3(1.0, 2.0, 3.0);
            GeoVector3 b = new GeoVector3(4.0, 5.0, 6.0);

            Assert.Equal(a.Add(b), a + b);
            Assert.Equal(a.Subtract(b), a - b);
            Assert.Equal(a.Multiply(2.0), a * 2.0);
            Assert.Equal(a.Multiply(2.0), 2.0 * a);
            Assert.Equal(a.Divide(2.0), a / 2.0);
            Assert.Equal(a.Negate(), -a);
        }

        [Fact]
        public void EqualHashCodesFollowEqualValues()
        {
            GeoVector3 a = new GeoVector3(1.5, -2.5, 3.5);
            GeoVector3 b = new GeoVector3(1.5, -2.5, 3.5);

            Assert.Equal(a, b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void DefaultValueIsTheZeroVector()
        {
            Assert.Equal(GeoVector3.Zero, default(GeoVector3));
            Assert.True(default(GeoVector3).IsZeroLength());
        }

        [Fact]
        public void DotProductIsCommutativeAndCrossProductAntiCommutative()
        {
            GeoVector3 a = new GeoVector3(1.0, -2.0, 3.0);
            GeoVector3 b = new GeoVector3(-4.0, 5.0, 6.0);

            Assert.Equal(a.DotProduct(b), b.DotProduct(a), 9);
            Assert.True(a.CrossProduct(b).IsEqualTo(b.CrossProduct(a).Negate(), new Tolerance(Precision, Precision)));
        }
    }
}
