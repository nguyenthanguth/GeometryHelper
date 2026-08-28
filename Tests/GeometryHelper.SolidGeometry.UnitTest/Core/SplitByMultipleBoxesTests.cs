using System;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers cutting a curve by several boxes at once, oriented and axis-aligned alike. The rule is the
    /// one the solid overloads follow — the cutters act as their union — but a box is reached through the
    /// slab test rather than by walking a surface, and an axis-aligned box is a value with no null to
    /// skip, so the degenerate entry to leave out is the empty box instead.
    /// </summary>
    public class SplitByMultipleBoxesTests
    {
        private static GeoAabb3 Aabb(double x0, double x1) =>
            new GeoAabb3(new GeoPoint3(x0, 0, 0), new GeoPoint3(x1, 10, 10));

        private static GeoObb3 Obb(double x0, double x1) => Aabb(x0, x1).ToObb();

        /// <summary>
        /// A straight run along X at y = z = 5, from x = -10 to x = 60, seventy units long.
        /// </summary>
        private static GeoPolyline3 Chain() =>
            new GeoPolyline3(new GeoPoint3(-10, 5, 5), new GeoPoint3(60, 5, 5));

        private static GeoLine3 Segment() =>
            new GeoLine3(new GeoPoint3(-10, 5, 5), new GeoPoint3(60, 5, 5));

        private static double TotalLength(GeoPolyline3[] pieces)
        {
            double total = 0.0;
            foreach (GeoPolyline3 piece in pieces) { total += piece.Length; }
            return total;
        }

        private static double TotalLength(GeoLine3[] pieces)
        {
            double total = 0.0;
            foreach (GeoLine3 piece in pieces) { total += piece.Length; }
            return total;
        }

        #region Agreement with the single-cutter overload

        [Fact]
        public void Polyline_AnArrayOfOneObb_AsksExactlyWhatTheSingleOverloadAnswers()
        {
            GeoObb3 box = Obb(0, 10);

            bool one = Chain().TrySplitBy(box, out GeoPolyline3[] oneIn, out GeoPolyline3[] oneOut);
            bool many = Chain().TrySplitBy(new[] { box }, out GeoPolyline3[] manyIn, out GeoPolyline3[] manyOut);

            Assert.Equal(one, many);
            Assert.Equal(oneIn.Length, manyIn.Length);
            Assert.Equal(oneOut.Length, manyOut.Length);
            Assert.Equal(TotalLength(oneIn), TotalLength(manyIn), 9);
        }

        [Fact]
        public void Segment_AnArrayOfOneObb_AsksExactlyWhatTheSingleOverloadAnswers()
        {
            GeoObb3 box = Obb(0, 10);

            bool one = Segment().TrySplitBy(box, out GeoLine3[] oneIn, out GeoLine3[] oneOut);
            bool many = Segment().TrySplitBy(new[] { box }, out GeoLine3[] manyIn, out GeoLine3[] manyOut);

            Assert.Equal(one, many);
            Assert.Equal(oneIn.Length, manyIn.Length);
            Assert.Equal(oneOut.Length, manyOut.Length);
            Assert.Equal(TotalLength(oneIn), TotalLength(manyIn), 9);
        }

        [Fact]
        public void Polyline_AnArrayOfOneAabb_AsksExactlyWhatTheSingleOverloadAnswers()
        {
            GeoAabb3 box = Aabb(0, 10);

            bool one = Chain().TrySplitBy(box, out GeoPolyline3[] oneIn, out GeoPolyline3[] oneOut);
            bool many = Chain().TrySplitBy(new[] { box }, out GeoPolyline3[] manyIn, out GeoPolyline3[] manyOut);

            Assert.Equal(one, many);
            Assert.Equal(oneIn.Length, manyIn.Length);
            Assert.Equal(oneOut.Length, manyOut.Length);
            Assert.Equal(TotalLength(oneIn), TotalLength(manyIn), 9);
        }

        [Fact]
        public void Segment_AnArrayOfOneAabb_AsksExactlyWhatTheSingleOverloadAnswers()
        {
            GeoAabb3 box = Aabb(0, 10);

            bool one = Segment().TrySplitBy(box, out GeoLine3[] oneIn, out GeoLine3[] oneOut);
            bool many = Segment().TrySplitBy(new[] { box }, out GeoLine3[] manyIn, out GeoLine3[] manyOut);

            Assert.Equal(one, many);
            Assert.Equal(oneIn.Length, manyIn.Length);
            Assert.Equal(oneOut.Length, manyOut.Length);
            Assert.Equal(TotalLength(oneIn), TotalLength(manyIn), 9);
        }

        [Fact]
        public void AnArrayOfBoxesAgreesWithTheSameBoxesAsSolids()
        {
            GeoAabb3[] boxes = { Aabb(0, 10), Aabb(20, 30) };
            GeoSolid3[] solids = { boxes[0].ToObb().ToSolid(), boxes[1].ToObb().ToSolid() };

            bool asBoxes = Chain().TrySplitBy(boxes, out GeoPolyline3[] boxIn, out GeoPolyline3[] boxOut);
            bool asSolids = Chain().TrySplitBy(solids, out GeoPolyline3[] solidIn, out GeoPolyline3[] solidOut);

            Assert.Equal(asSolids, asBoxes);
            Assert.Equal(solidIn.Length, boxIn.Length);
            Assert.Equal(solidOut.Length, boxOut.Length);
            Assert.Equal(TotalLength(solidIn), TotalLength(boxIn), 9);
        }

        #endregion

        #region The union rule

        [Fact]
        public void TwoBoxesApart_GiveOnePieceInsideEach()
        {
            Assert.True(Chain().TrySplitBy(new[] { Obb(0, 10), Obb(20, 30) }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Equal(2, inside.Length);
            Assert.Equal(3, outside.Length);
            Assert.Equal(20.0, TotalLength(inside), 9);
            Assert.Equal(50.0, TotalLength(outside), 9);
        }

        [Fact]
        public void TwoBoxesOverlapping_GiveOneUnbrokenPiece()
        {
            // 0..10 and 5..20 share 5..10; their union is 0..20 and nothing separates it.
            Assert.True(Chain().TrySplitBy(new[] { Obb(0, 10), Obb(5, 20) }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(20.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        [Fact]
        public void TwoBoxesMeetingFaceToFace_LeaveNoCutBetweenThem()
        {
            Assert.True(Chain().TrySplitBy(new[] { Obb(0, 10), Obb(10, 25) }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(25.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        [Fact]
        public void ARotatedBoxIsHandledLikeAnyOther()
        {
            // Turned forty-five degrees about Z, centred on the run at x = 5. Its half-diagonal across the
            // XY plane is what the chain meets, so the stretch inside is the full width of the diagonal.
            GeoObb3 turned = new GeoObb3(
                new GeoPoint3(5, 5, 5), 10, 10, 10,
                new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0));

            Assert.True(Chain().TrySplitBy(new[] { turned }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(10.0 * Math.Sqrt(2.0), inside[0].Length, 6);
            Assert.Equal(2, outside.Length);
        }

        #endregion

        #region Degenerate entries

        [Fact]
        public void AnEmptyObbArray_LeavesTheWholeChainOutside()
        {
            Assert.False(Chain().TrySplitBy(new GeoObb3[0], out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Empty(inside);
            Assert.Single(outside);
            Assert.Equal(70.0, outside[0].Length, 9);
        }

        [Fact]
        public void AnEmptyAabbArray_LeavesTheWholeChainOutside()
        {
            Assert.False(Chain().TrySplitBy(new GeoAabb3[0], out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Empty(inside);
            Assert.Single(outside);
            Assert.Equal(70.0, outside[0].Length, 9);
        }

        [Fact]
        public void NullObbEntries_AreSkippedRatherThanRefused()
        {
            GeoObb3 box = Obb(0, 10);

            bool dense = Chain().TrySplitBy(new[] { box }, out GeoPolyline3[] denseIn, out _);
            bool sparse = Chain().TrySplitBy(new[] { null, box, null }, out GeoPolyline3[] sparseIn, out _);

            Assert.Equal(dense, sparse);
            Assert.Equal(denseIn.Length, sparseIn.Length);
            Assert.Equal(TotalLength(denseIn), TotalLength(sparseIn), 9);
        }

        [Fact]
        public void EmptyAabbEntries_AreSkippedRatherThanRefused()
        {
            // An axis-aligned box is a value, so there is no null to pass. The empty box takes its place.
            GeoAabb3 box = Aabb(0, 10);

            bool dense = Chain().TrySplitBy(new[] { box }, out GeoPolyline3[] denseIn, out _);
            bool sparse = Chain().TrySplitBy(new[] { GeoAabb3.Empty, box, GeoAabb3.Empty }, out GeoPolyline3[] sparseIn, out _);

            Assert.Equal(dense, sparse);
            Assert.Equal(denseIn.Length, sparseIn.Length);
            Assert.Equal(TotalLength(denseIn), TotalLength(sparseIn), 9);
        }

        [Fact]
        public void AnArrayOfNothingButEmptyBoxes_CutsNothing()
        {
            Assert.False(Chain().TrySplitBy(new[] { GeoAabb3.Empty, GeoAabb3.Empty }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Empty(inside);
            Assert.Single(outside);
        }

        [Fact]
        public void ANullArray_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Chain().TrySplitBy((GeoObb3[])null, out GeoPolyline3[] _, out GeoPolyline3[] _));

            Assert.Throws<ArgumentNullException>(() =>
                Chain().TrySplitBy((GeoAabb3[])null, out GeoPolyline3[] _, out GeoPolyline3[] _));

            Assert.Throws<ArgumentNullException>(() =>
                Segment().TrySplitBy((GeoObb3[])null, out GeoLine3[] _, out GeoLine3[] _));

            Assert.Throws<ArgumentNullException>(() =>
                Segment().TrySplitBy((GeoAabb3[])null, out GeoLine3[] _, out GeoLine3[] _));
        }

        [Fact]
        public void ANullSubject_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Splition3.TrySplitBy((GeoPolyline3)null, new[] { Obb(0, 10) }, out GeoPolyline3[] _, out GeoPolyline3[] _));

            Assert.Throws<ArgumentNullException>(() =>
                Splition3.TrySplitBy((GeoPolyline3)null, new[] { Aabb(0, 10) }, out GeoPolyline3[] _, out GeoPolyline3[] _));
        }

        [Fact]
        public void BoxesTheChainMisses_LeaveTheWholeChainOutside()
        {
            GeoAabb3[] elsewhere =
            {
                new GeoAabb3(new GeoPoint3(0, 100, 0), new GeoPoint3(10, 110, 10)),
                new GeoAabb3(new GeoPoint3(20, 100, 0), new GeoPoint3(30, 110, 10))
            };

            Assert.False(Chain().TrySplitBy(elsewhere, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Empty(inside);
            Assert.Single(outside);
        }

        #endregion

        #region Invariants

        [Fact]
        public void NothingIsLostOrGained_WhateverTheArrangement()
        {
            GeoAabb3[][] arrangements =
            {
                new[] { Aabb(0, 10) },
                new[] { Aabb(0, 10), Aabb(20, 30) },
                new[] { Aabb(0, 10), Aabb(5, 20) },
                new[] { Aabb(0, 10), Aabb(10, 25) },
                new[] { GeoAabb3.Empty },
                new GeoAabb3[0]
            };

            foreach (GeoAabb3[] cutters in arrangements)
            {
                Chain().TrySplitBy(cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside);
                Assert.Equal(70.0, TotalLength(inside) + TotalLength(outside), 9);

                Segment().TrySplitBy(cutters, out GeoLine3[] segIn, out GeoLine3[] segOut);
                Assert.Equal(70.0, TotalLength(segIn) + TotalLength(segOut), 9);
            }
        }

        [Fact]
        public void TheOrderOfTheBoxesDoesNotMatter()
        {
            GeoObb3 first = Obb(0, 10);
            GeoObb3 second = Obb(20, 30);

            Chain().TrySplitBy(new[] { first, second }, out GeoPolyline3[] inA, out GeoPolyline3[] outA);
            Chain().TrySplitBy(new[] { second, first }, out GeoPolyline3[] inB, out GeoPolyline3[] outB);

            Assert.Equal(inA.Length, inB.Length);
            Assert.Equal(outA.Length, outB.Length);
            Assert.Equal(TotalLength(inA), TotalLength(inB), 9);
        }

        [Fact]
        public void ABentChain_IsCutAcrossItsCorners()
        {
            // Turns the corner inside the box, so the piece inside spans two edges of the chain: five
            // units along X to the corner, then five more up Z to the top of the box.
            GeoPolyline3 bent = new GeoPolyline3(
                new GeoPoint3(-10, 5, 5), new GeoPoint3(5, 5, 5), new GeoPoint3(5, 5, 40));

            Assert.True(bent.TrySplitBy(new[] { Obb(0, 10) }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(10.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        [Fact]
        public void SegmentAndChainAgreeOnTheSameRun()
        {
            GeoObb3[] cutters = { Obb(0, 10), Obb(20, 30) };

            Chain().TrySplitBy(cutters, out GeoPolyline3[] chainIn, out GeoPolyline3[] chainOut);
            Segment().TrySplitBy(cutters, out GeoLine3[] segIn, out GeoLine3[] segOut);

            Assert.Equal(chainIn.Length, segIn.Length);
            Assert.Equal(chainOut.Length, segOut.Length);
            Assert.Equal(TotalLength(chainIn), TotalLength(segIn), 9);
            Assert.Equal(TotalLength(chainOut), TotalLength(segOut), 9);
        }

        #endregion
    }
}
