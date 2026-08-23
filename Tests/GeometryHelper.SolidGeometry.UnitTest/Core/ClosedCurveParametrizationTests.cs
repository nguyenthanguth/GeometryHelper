using System;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers the promise the class summary of <see cref="Parametrization3"/> makes about closed curves:
    /// that a polygon and a circle wrap, so a parameter of 1.25 is the same position as 0.25.
    /// </summary>
    public class ClosedCurveParametrizationTests
    {
        /// <summary>
        /// A 10 by 10 square in the XY plane, perimeter 40, starting at the origin.
        /// </summary>
        private static GeoPolygon3 MakeSquare() => new GeoPolygon3(
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(10, 0, 0),
            new GeoPoint3(10, 10, 0),
            new GeoPoint3(0, 10, 0));

        private static readonly GeoCircle3 UnitDisc = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 5.0);

        #region Polygon

        [Fact]
        public void ParameterZeroIsTheFirstVertexAndOneReturnsToIt()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.True(square.GetPointAtParameter(0.0).IsEqualTo(square[0]));
            Assert.True(square.GetPointAtParameter(1.0).IsEqualTo(square[0]));
        }

        [Fact]
        public void TheParameterWalksTheBoundaryInVertexOrder()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.True(square.GetPointAtParameter(0.25).IsEqualTo(new GeoPoint3(10, 0, 0)));
            Assert.True(square.GetPointAtParameter(0.5).IsEqualTo(new GeoPoint3(10, 10, 0)));
            Assert.True(square.GetPointAtParameter(0.75).IsEqualTo(new GeoPoint3(0, 10, 0)));
            Assert.True(square.GetPointAtParameter(0.125).IsEqualTo(new GeoPoint3(5, 0, 0)));
        }

        [Fact]
        public void ThePolygonParameterWraps()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.True(square.GetPointAtParameter(1.25).IsEqualTo(square.GetPointAtParameter(0.25)));
            Assert.True(square.GetPointAtParameter(-0.25).IsEqualTo(square.GetPointAtParameter(0.75)));
            Assert.True(square.GetPointAtParameter(7.5).IsEqualTo(square.GetPointAtParameter(0.5)));
        }

        [Fact]
        public void DistanceAndParameterAgreeAroundThePolygon()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.Equal(40.0, square.Length, 9);
            Assert.Equal(10.0, square.GetDistanceAtParameter(0.25), 9);
            Assert.Equal(0.25, square.GetParameterAtDistance(10.0), 9);
            Assert.True(square.GetPointAtDistance(15.0).IsEqualTo(new GeoPoint3(10, 5, 0)));
        }

        [Fact]
        public void ThePolygonDistanceWrapsToo()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.True(square.GetPointAtDistance(45.0).IsEqualTo(square.GetPointAtDistance(5.0)));
            Assert.True(square.GetPointAtDistance(-5.0).IsEqualTo(square.GetPointAtDistance(35.0)));
        }

        [Fact]
        public void PointAndParameterRoundTripAroundThePolygon()
        {
            GeoPolygon3 square = MakeSquare();

            for (double t = 0.0; t < 1.0; t += 0.05)
            {
                GeoPoint3 onBoundary = square.GetPointAtParameter(t);

                Assert.Equal(t, square.GetParameterAtPoint(onBoundary), 9);
            }
        }

        [Fact]
        public void APointOffTheBoundaryReportsTheNearestPositionOnIt()
        {
            GeoPolygon3 square = MakeSquare();

            // Beside the middle of the first edge, and above the middle of the polygon.
            Assert.Equal(5.0, square.GetDistanceAtPoint(new GeoPoint3(5, -3, 0)), 9);
            Assert.Equal(0.125, square.GetParameterAtPoint(new GeoPoint3(5, -3, 0)), 9);
        }

        [Fact]
        public void TheStaticAndInstanceFormsAgreeForAPolygon()
        {
            GeoPolygon3 square = MakeSquare();
            GeoPoint3 probe = new GeoPoint3(3, -2, 1);

            Assert.Equal(Parametrization3.GetPointAtParameter(square, 0.3), square.GetPointAtParameter(0.3));
            Assert.Equal(Parametrization3.GetDistanceAtPoint(square, probe), square.GetDistanceAtPoint(probe), 12);
            Assert.Equal(Parametrization3.GetParameterAtPoint(square, probe), square.GetParameterAtPoint(probe), 12);
        }

        #endregion

        #region Circle

        [Fact]
        public void TheCircleParameterWraps()
        {
            Assert.True(UnitDisc.GetPointAtParameter(1.25).IsEqualTo(UnitDisc.GetPointAtParameter(0.25)));
            Assert.True(UnitDisc.GetPointAtParameter(-0.25).IsEqualTo(UnitDisc.GetPointAtParameter(0.75)));
            Assert.True(UnitDisc.GetPointAtParameter(0.0).IsEqualTo(UnitDisc.GetPointAtParameter(1.0)));
        }

        [Fact]
        public void DistanceAndParameterAgreeAroundTheCircle()
        {
            double circumference = 2.0 * Math.PI * 5.0;

            Assert.Equal(circumference, UnitDisc.Length, 9);
            Assert.Equal(circumference * 0.25, UnitDisc.GetDistanceAtParameter(0.25), 9);
            Assert.Equal(0.25, UnitDisc.GetParameterAtDistance(circumference * 0.25), 9);
            Assert.True(UnitDisc.GetPointAtDistance(circumference * 0.5)
                .IsEqualTo(UnitDisc.GetPointAtParameter(0.5)));
        }

        [Fact]
        public void PointAndParameterRoundTripAroundTheCircle()
        {
            for (double t = 0.0; t < 1.0; t += 0.05)
            {
                GeoPoint3 onCircumference = UnitDisc.GetPointAtParameter(t);

                Assert.Equal(t, UnitDisc.GetParameterAtPoint(onCircumference), 9);
            }
        }

        [Fact]
        public void APointOffTheCircleIsReadThroughItsProjection()
        {
            // Twice the radius out along the zero direction, and lifted off the plane: both project onto
            // the same place on the circumference.
            GeoPoint3 atZero = UnitDisc.GetPointAtParameter(0.0);
            GeoVector3 outward = UnitDisc.Center.GetVectorTo(atZero);

            GeoPoint3 farOut = UnitDisc.Center.Add(outward.Multiply(3.0)).Add(new GeoVector3(0, 0, 7));

            Assert.Equal(0.0, UnitDisc.GetParameterAtPoint(farOut), 9);
        }

        [Fact]
        public void APointOnTheAxisHasNoNearestPositionSoZeroComesBack()
        {
            Assert.Equal(0.0, UnitDisc.GetParameterAtPoint(UnitDisc.Center), 12);
            Assert.Equal(0.0, UnitDisc.GetParameterAtPoint(UnitDisc.Center.Add(new GeoVector3(0, 0, 9))), 12);
        }

        [Fact]
        public void TheStaticAndInstanceFormsAgreeForACircle()
        {
            GeoPoint3 probe = new GeoPoint3(9, 2, 4);

            Assert.Equal(Parametrization3.GetPointAtParameter(UnitDisc, 0.3), UnitDisc.GetPointAtParameter(0.3));
            Assert.Equal(Parametrization3.GetParameterAtPoint(UnitDisc, probe), UnitDisc.GetParameterAtPoint(probe), 12);
            Assert.Equal(Parametrization3.GetDistanceAtPoint(UnitDisc, probe), UnitDisc.GetDistanceAtPoint(probe), 12);
        }

        #endregion

        [Fact]
        public void EachFamilyOfCurveTreatsAnOutOfRangeParameterItsOwnWay()
        {
            // This is the whole promise of the Parametrization3 summary, in one place.
            GeoLine3 segment = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            GeoPolygon3 square = MakeSquare();

            // A segment extrapolates along its carrier.
            Assert.True(segment.GetPointAtParameter(2.0).IsEqualTo(new GeoPoint3(20, 0, 0)));

            // An open chain clamps, having nowhere further to go.
            Assert.True(chain.GetPointAtParameter(2.0).IsEqualTo(new GeoPoint3(10, 0, 0)));

            // A closed curve wraps.
            Assert.True(square.GetPointAtParameter(2.25).IsEqualTo(square.GetPointAtParameter(0.25)));
            Assert.True(UnitDisc.GetPointAtParameter(2.25).IsEqualTo(UnitDisc.GetPointAtParameter(0.25)));
        }
    }
}
