using System;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers the two remaining ways of presenting a cut: a plane sorted by side, and a bounded region
    /// that only cuts where the subject really goes through it.
    /// </summary>
    public class SplitBySideAndRegionTests
    {
        private static double TotalLength(GeoPolyline3[] pieces)
        {
            double total = 0.0;

            foreach (GeoPolyline3 piece in pieces)
            {
                total += piece.Length;
            }

            return total;
        }

        #region Sorted by side

        [Fact]
        public void AChainCrossingAPlaneIsSortedOntoTheTwoSides()
        {
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(0, 0, -4), new GeoPoint3(0, 0, 6));

            Assert.True(chain.TrySplitBy(GeoPlane3.XY, out GeoPolyline3[] above, out GeoPolyline3[] below));

            Assert.Single(above);
            Assert.Single(below);
            Assert.Equal(6.0, above[0].Length, 9);
            Assert.Equal(4.0, below[0].Length, 9);
        }

        [Fact]
        public void SortingBySideKeepsTheTotalLength()
        {
            GeoPolyline3 zigzag = new GeoPolyline3(
                new GeoPoint3(0, 0, -2),
                new GeoPoint3(1, 0, 2),
                new GeoPoint3(2, 0, -2),
                new GeoPoint3(3, 0, 2));

            Assert.True(zigzag.TrySplitBy(GeoPlane3.XY, out GeoPolyline3[] above, out GeoPolyline3[] below));

            Assert.Equal(2, above.Length);
            Assert.Equal(2, below.Length);
            Assert.Equal(zigzag.Length, TotalLength(above) + TotalLength(below), 9);
        }

        [Fact]
        public void TheSideSortedFormCutsAtTheSamePlacesAsTheOrderedOne()
        {
            GeoPolyline3 zigzag = new GeoPolyline3(
                new GeoPoint3(0, 0, -2),
                new GeoPoint3(1, 0, 2),
                new GeoPoint3(2, 0, -2),
                new GeoPoint3(3, 0, 2));

            Assert.True(zigzag.TrySplitBy(GeoPlane3.XY, out GeoPolyline3[] ordered));
            Assert.True(zigzag.TrySplitBy(GeoPlane3.XY, out GeoPolyline3[] above, out GeoPolyline3[] below));

            Assert.Equal(ordered.Length, above.Length + below.Length);
            Assert.Equal(TotalLength(ordered), TotalLength(above) + TotalLength(below), 9);
        }

        [Fact]
        public void AChainStayingOnOneSideIsNotCut()
        {
            GeoPolyline3 high = new GeoPolyline3(new GeoPoint3(0, 0, 5), new GeoPoint3(10, 0, 5));

            Assert.False(high.TrySplitBy(GeoPlane3.XY, out GeoPolyline3[] above, out GeoPolyline3[] below));
            Assert.Single(above);
            Assert.Empty(below);
            Assert.Equal(high, above[0]);

            GeoPolyline3 low = new GeoPolyline3(new GeoPoint3(0, 0, -5), new GeoPoint3(10, 0, -5));

            Assert.False(low.TrySplitBy(GeoPlane3.XY, out above, out below));
            Assert.Empty(above);
            Assert.Single(below);
        }

        [Fact]
        public void AStretchLyingInThePlaneGoesWithTheUpperSide()
        {
            // Down to the plane, along it, then back up. The middle stretch is on the plane itself.
            GeoPolyline3 chain = new GeoPolyline3(
                new GeoPoint3(0, 0, -5),
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 0, -5));

            Assert.True(chain.TrySplitBy(GeoPlane3.XY, out GeoPolyline3[] above, out GeoPolyline3[] below));

            // "Above" reads as not strictly below, which is the convention Contains keeps.
            Assert.Equal(10.0, TotalLength(above), 6);
            Assert.Equal(10.0, TotalLength(below), 6);
            Assert.Equal(chain.Length, TotalLength(above) + TotalLength(below), 9);
        }

        [Fact]
        public void ASegmentIsSortedTheSameWay()
        {
            GeoLine3 segment = new GeoLine3(new GeoPoint3(0, 0, -4), new GeoPoint3(0, 0, 6));

            Assert.True(segment.TrySplitBy(GeoPlane3.XY, out GeoLine3[] above, out GeoLine3[] below));

            Assert.Single(above);
            Assert.Single(below);
            Assert.Equal(6.0, above[0].Length, 9);
            Assert.Equal(4.0, below[0].Length, 9);
        }

        #endregion

        #region Bounded region

        /// <summary>
        /// A 10 by 10 plate in the XY plane, from the origin outwards.
        /// </summary>
        private static GeoPolygon3 MakePlate() => new GeoPolygon3(
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(10, 0, 0),
            new GeoPoint3(10, 10, 0),
            new GeoPoint3(0, 10, 0));

        [Fact]
        public void AChainThroughThePlateIsCutThere()
        {
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(5, 5, -5), new GeoPoint3(5, 5, 5));

            Assert.True(chain.TrySplitBy(MakePlate(), out GeoPolyline3[] pieces));

            Assert.Equal(2, pieces.Length);
            Assert.Equal(5.0, pieces[0].Length, 9);
            Assert.Equal(chain.Length, TotalLength(pieces), 9);
        }

        [Fact]
        public void AChainCrossingThePlaneBeyondThePlateIsLeftAlone()
        {
            // Crosses z = 0 well outside the 10 by 10 outline.
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(50, 50, -5), new GeoPoint3(50, 50, 5));

            Assert.False(chain.TrySplitBy(MakePlate(), out GeoPolyline3[] pieces));
            Assert.Single(pieces);
            Assert.Equal(chain, pieces[0]);

            // The infinite plane carrying the plate does cut it, which is the difference between the two.
            Assert.True(chain.TrySplitBy(MakePlate().GetPlane(), out GeoPolyline3[] byPlane));
            Assert.Equal(2, byPlane.Length);
        }

        [Fact]
        public void AChainThreadingAHoleInAFaceIsNotCut()
        {
            GeoPolygon3 hole = new GeoPolygon3(
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(6, 4, 0),
                new GeoPoint3(6, 6, 0),
                new GeoPoint3(4, 6, 0));

            GeoFace3 plate = new GeoFace3(MakePlate(), new[] { hole });

            GeoPolyline3 throughHole = new GeoPolyline3(new GeoPoint3(5, 5, -5), new GeoPoint3(5, 5, 5));
            GeoPolyline3 throughMaterial = new GeoPolyline3(new GeoPoint3(1, 1, -5), new GeoPoint3(1, 1, 5));

            Assert.False(throughHole.TrySplitBy(plate, out GeoPolyline3[] pieces));
            Assert.Single(pieces);

            Assert.True(throughMaterial.TrySplitBy(plate, out pieces));
            Assert.Equal(2, pieces.Length);
        }

        [Fact]
        public void AChainCrossingThePlateSeveralTimesIsCutAtEachCrossing()
        {
            GeoPolyline3 zigzag = new GeoPolyline3(
                new GeoPoint3(2, 5, -2),
                new GeoPoint3(4, 5, 2),
                new GeoPoint3(6, 5, -2),
                new GeoPoint3(8, 5, 2));

            Assert.True(zigzag.TrySplitBy(MakePlate(), out GeoPolyline3[] pieces));

            Assert.Equal(4, pieces.Length);
            Assert.Equal(zigzag.Length, TotalLength(pieces), 9);
        }

        [Fact]
        public void ASegmentThroughThePlateIsCutThere()
        {
            GeoLine3 segment = new GeoLine3(new GeoPoint3(5, 5, -5), new GeoPoint3(5, 5, 5));
            GeoLine3 beside = new GeoLine3(new GeoPoint3(50, 50, -5), new GeoPoint3(50, 50, 5));

            Assert.True(segment.TrySplitBy(MakePlate(), out GeoLine3[] pieces));
            Assert.Equal(2, pieces.Length);

            Assert.False(beside.TrySplitBy(MakePlate(), out pieces));
            Assert.Single(pieces);
        }

        [Fact]
        public void NullCuttersAreRejected()
        {
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));

            Assert.Throws<ArgumentNullException>(() => chain.TrySplitBy((GeoPolygon3)null, out _));
            Assert.Throws<ArgumentNullException>(() => chain.TrySplitBy((GeoFace3)null, out _));
        }

        #endregion
    }
}
