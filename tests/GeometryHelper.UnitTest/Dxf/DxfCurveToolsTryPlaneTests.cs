using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfCurveTools.TryPlane"/> answers false, never a throw, for points that give no plane to tell, and still
    /// answers the plane of points on one line, or nearly, and of points given a normal of any length.
    /// </summary>
    public class DxfCurveToolsTryPlaneTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        private static void NoPlane(IReadOnlyList<GeoPoint3> points, GeoVector3 preferred)
        {
            Assert.False(DxfCurveTools.TryPlane(points, preferred, out GeoVector3 normal));
            Assert.True(normal.IsEqualTo(Up, Tight));
        }

        [Fact]
        public void NoPointsOneOrPointsAllAtOne_GiveNoPlane_WithOrWithoutANormalPreferred()
        {
            foreach (GeoVector3 preferred in new[] { default(GeoVector3), new GeoVector3(1, 0, 0) })
            {
                NoPlane(new GeoPoint3[0], preferred);
                NoPlane(new[] { new GeoPoint3(5, 6, 7) }, preferred);
                NoPlane(new[] { new GeoPoint3(5, 6, 7), new GeoPoint3(5, 6, 7), new GeoPoint3(5, 6, 7) }, preferred);

                // A triangle a ten-thousandth across, inside the point tolerance of 1E-3: once a throw, its Newell sum of
                // 1E-8 too short to normalise.
                NoPlane(new[] { new GeoPoint3(5, 6, 7), new GeoPoint3(5.0001, 6, 7), new GeoPoint3(5, 6.0001, 7.0001) }, preferred);
            }
        }

        [Fact]
        public void PointsNotFinite_GiveNoPlane()
        {
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            {
                NoPlane(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, bad, 0) }, default(GeoVector3));
                NoPlane(new[] { new GeoPoint3(bad, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0) }, Up);
            }
        }

        [Fact]
        public void PointsSoFarApartTheirSumsRunPastTheLargestDouble_GiveNoPlane()
        {
            // As the DWG fuzz damaged a SPLINE: one control point moved to y = -4.8E+233, which once threw from the
            // normalising of a Newell sum past the largest double.
            NoPlane(new[] { new GeoPoint3(20, 313.3, 0), new GeoPoint3(40, 300, 0), new GeoPoint3(20, -4.8438564479782119E+233, 0), new GeoPoint3(0, 300, 0) }, default(GeoVector3));
        }

        [Fact]
        public void PointsOnOneLine_LieInThePlaneThroughItNearestTheFloor()
        {
            Assert.True(DxfCurveTools.TryPlane(new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(100, 0, 5), new GeoPoint3(300, 0, 5) }, default(GeoVector3), out GeoVector3 flat));
            Assert.True(flat.IsEqualTo(Up, Tight));

            Assert.True(DxfCurveTools.TryPlane(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 100) }, default(GeoVector3), out GeoVector3 standing));
            Assert.True(standing.IsEqualTo(new GeoVector3(1, 0, 0), Tight));
        }

        [Fact]
        public void PointsANanometreOffOneLine_LieInTheirPlane_NotAThrow()
        {
            // The Newell sum of (0, 0, 0), (1000, 0, 0) and (2000, 1E-9, 0) is (0, 0, 1E-6), too short for Normalize: once a
            // throw.
            Assert.True(DxfCurveTools.TryPlane(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0), new GeoPoint3(2000, 1E-9, 0) }, default(GeoVector3), out GeoVector3 normal));
            Assert.True(normal.IsEqualTo(Up, Tight));
        }

        [Fact]
        public void ANormalPreferredOfAnyLength_IsTakenWhereThePointsLieSquareToIt()
        {
            // A normal of length 1E-7, too short for Normalize: once a throw.
            GeoPoint3[] wall = { new GeoPoint3(5, 0, 0), new GeoPoint3(5, 100, 0), new GeoPoint3(5, 100, 50), new GeoPoint3(5, 0, 50) };
            Assert.True(DxfCurveTools.TryPlane(wall, new GeoVector3(-1E-7, 0, 0), out GeoVector3 normal));
            Assert.True(normal.IsEqualTo(new GeoVector3(-1, 0, 0), Tight));
        }

        [Fact]
        public void PointsInNoPlane_GiveNone()
        {
            NoPlane(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 30) }, default(GeoVector3));
        }

        [Fact]
        public void A3dPolylineANanometreOffOneLine_IsOffsetInItsPlane_NotAThrow()
        {
            var polyline = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0), new GeoPoint3(2000, 1E-9, 0) }, false);

            Assert.NotEmpty(polyline.GetOffsetCurves(10.0));
        }
    }
}
