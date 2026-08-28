using System;
using System.Collections.Generic;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers cutting a curve by several solids at once. The bodies act as their union, so a stretch is
    /// inside when any one of them holds it, and a cut that separates nothing is not a cut.
    /// </summary>
    public class SplitByMultipleSolidsTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            GeoPoint3[] baseCcw =
            {
                new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y0, z0),
                new GeoPoint3(x1, y1, z0), new GeoPoint3(x0, y1, z0)
            };

            List<GeoFace3> faces = new List<GeoFace3>();
            GeoPoint3[] top = new GeoPoint3[4];
            GeoPoint3[] bottomReversed = new GeoPoint3[4];

            for (int i = 0; i < 4; i++)
            {
                top[i] = new GeoPoint3(baseCcw[i].X, baseCcw[i].Y, z1);
                bottomReversed[i] = baseCcw[3 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                faces.Add(new GeoFace3(new GeoPolygon3(baseCcw[i], baseCcw[j], top[j], top[i])));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A straight run along X at y = z = 5, from x = -10 to x = 60.
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
        public void Polyline_AnArrayOfOne_AsksExactlyWhatTheSingleOverloadAnswers()
        {
            GeoSolid3 box = Box(0, 0, 0, 10, 10, 10);

            bool one = Chain().TrySplitBy(box, out GeoPolyline3[] oneIn, out GeoPolyline3[] oneOut);
            bool many = Chain().TrySplitBy(new[] { box }, out GeoPolyline3[] manyIn, out GeoPolyline3[] manyOut);

            Assert.Equal(one, many);
            Assert.Equal(oneIn.Length, manyIn.Length);
            Assert.Equal(oneOut.Length, manyOut.Length);
            Assert.Equal(TotalLength(oneIn), TotalLength(manyIn), 9);
            Assert.Equal(TotalLength(oneOut), TotalLength(manyOut), 9);
        }

        [Fact]
        public void Segment_AnArrayOfOne_AsksExactlyWhatTheSingleOverloadAnswers()
        {
            GeoSolid3 box = Box(0, 0, 0, 10, 10, 10);

            bool one = Segment().TrySplitBy(box, out GeoLine3[] oneIn, out GeoLine3[] oneOut);
            bool many = Segment().TrySplitBy(new[] { box }, out GeoLine3[] manyIn, out GeoLine3[] manyOut);

            Assert.Equal(one, many);
            Assert.Equal(oneIn.Length, manyIn.Length);
            Assert.Equal(oneOut.Length, manyOut.Length);
            Assert.Equal(TotalLength(oneIn), TotalLength(manyIn), 9);
            Assert.Equal(TotalLength(oneOut), TotalLength(manyOut), 9);
        }

        #endregion

        #region The union rule

        [Fact]
        public void TwoBodiesApart_GiveOnePieceInsideEach()
        {
            GeoSolid3[] cutters = { Box(0, 0, 0, 10, 10, 10), Box(20, 0, 0, 30, 10, 10) };

            Assert.True(Chain().TrySplitBy(cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Equal(2, inside.Length);
            Assert.Equal(3, outside.Length);

            // 0..10 and 20..30 are material; the run is 70 long in total.
            Assert.Equal(20.0, TotalLength(inside), 9);
            Assert.Equal(50.0, TotalLength(outside), 9);
        }

        [Fact]
        public void TwoBodiesOverlapping_GiveOneUnbrokenPiece()
        {
            // 0..10 and 5..20 share the stretch 5..10. Their union is 0..20 and nothing separates it.
            GeoSolid3[] cutters = { Box(0, 0, 0, 10, 10, 10), Box(5, 0, 0, 20, 10, 10) };

            Assert.True(Chain().TrySplitBy(cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(20.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        [Fact]
        public void TwoBodiesMeetingFaceToFace_LeaveNoCutBetweenThem()
        {
            // They share the plane x = 10. The chain crosses that plane, but it separates material from
            // material, so it is not a cut.
            GeoSolid3[] cutters = { Box(0, 0, 0, 10, 10, 10), Box(10, 0, 0, 25, 10, 10) };

            Assert.True(Chain().TrySplitBy(cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(25.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        [Fact]
        public void ABodyIsSwallowedByAnother_ChangesNothing()
        {
            GeoSolid3[] cutters = { Box(0, 0, 0, 20, 20, 20), Box(5, 5, 5, 10, 10, 10) };

            Assert.True(Chain().TrySplitBy(cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(20.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        #endregion

        #region Nothing to cut

        [Fact]
        public void AnEmptyArray_LeavesTheWholeChainOutside()
        {
            Assert.False(Chain().TrySplitBy(new GeoSolid3[0], out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Empty(inside);
            Assert.Single(outside);
            Assert.Equal(70.0, outside[0].Length, 9);
        }

        [Fact]
        public void BodiesTheChainMisses_LeaveTheWholeChainOutside()
        {
            GeoSolid3[] cutters = { Box(0, 100, 0, 10, 110, 10), Box(20, 100, 0, 30, 110, 10) };

            Assert.False(Chain().TrySplitBy(cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Empty(inside);
            Assert.Single(outside);
            Assert.Equal(70.0, outside[0].Length, 9);
        }

        [Fact]
        public void NullEntries_AreSkippedRatherThanRefused()
        {
            GeoSolid3 box = Box(0, 0, 0, 10, 10, 10);

            bool dense = Chain().TrySplitBy(new[] { box }, out GeoPolyline3[] denseIn, out GeoPolyline3[] denseOut);
            bool sparse = Chain().TrySplitBy(new[] { null, box, null }, out GeoPolyline3[] sparseIn, out GeoPolyline3[] sparseOut);

            Assert.Equal(dense, sparse);
            Assert.Equal(denseIn.Length, sparseIn.Length);
            Assert.Equal(denseOut.Length, sparseOut.Length);
            Assert.Equal(TotalLength(denseIn), TotalLength(sparseIn), 9);
        }

        [Fact]
        public void ANullArray_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Chain().TrySplitBy((GeoSolid3[])null, out GeoPolyline3[] _, out GeoPolyline3[] _));

            Assert.Throws<ArgumentNullException>(() =>
                Segment().TrySplitBy((GeoSolid3[])null, out GeoLine3[] _, out GeoLine3[] _));
        }

        [Fact]
        public void ANullSubject_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Splition3.TrySplitBy((GeoPolyline3)null, new[] { Box(0, 0, 0, 1, 1, 1) }, out GeoPolyline3[] _, out GeoPolyline3[] _));
        }

        #endregion

        #region Openings

        [Fact]
        public void AnOpeningInOneOfTheBodies_CountsAsOutside()
        {
            // A block from 0 to 30 with a duct carved through it from 10 to 20, along the run of the
            // chain. The chain therefore meets material twice, not once.
            GeoSolid3 block = Box(0, 0, 0, 30, 10, 10).WithOpenings(new[] { Box(10, 3, 3, 20, 7, 7) });

            Assert.True(Chain().TrySplitBy(new[] { block }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Equal(2, inside.Length);
            Assert.Equal(20.0, TotalLength(inside), 9);

            // Before the block, through the duct, and beyond the block.
            Assert.Equal(3, outside.Length);
            Assert.Equal(50.0, TotalLength(outside), 9);
        }

        [Fact]
        public void AnotherBodyFillingTheOpening_ClosesItAgain()
        {
            GeoSolid3 block = Box(0, 0, 0, 30, 10, 10).WithOpenings(new[] { Box(10, 3, 3, 20, 7, 7) });
            GeoSolid3 plug = Box(10, 3, 3, 20, 7, 7);

            Assert.True(Chain().TrySplitBy(new[] { block, plug }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            // The union of the drilled block and the plug is the solid block again.
            Assert.Single(inside);
            Assert.Equal(30.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        #endregion

        #region Invariants

        [Fact]
        public void NothingIsLostOrGained_WhateverTheArrangement()
        {
            GeoSolid3[][] arrangements =
            {
                new[] { Box(0, 0, 0, 10, 10, 10) },
                new[] { Box(0, 0, 0, 10, 10, 10), Box(20, 0, 0, 30, 10, 10) },
                new[] { Box(0, 0, 0, 10, 10, 10), Box(5, 0, 0, 20, 10, 10) },
                new[] { Box(0, 0, 0, 10, 10, 10), Box(10, 0, 0, 25, 10, 10) },
                new[] { Box(0, 100, 0, 10, 110, 10) },
                new GeoSolid3[0]
            };

            foreach (GeoSolid3[] cutters in arrangements)
            {
                Chain().TrySplitBy(cutters, out GeoPolyline3[] inside, out GeoPolyline3[] outside);
                Assert.Equal(70.0, TotalLength(inside) + TotalLength(outside), 9);

                Segment().TrySplitBy(cutters, out GeoLine3[] segIn, out GeoLine3[] segOut);
                Assert.Equal(70.0, TotalLength(segIn) + TotalLength(segOut), 9);
            }
        }

        [Fact]
        public void TheOrderOfTheBodiesDoesNotMatter()
        {
            GeoSolid3 first = Box(0, 0, 0, 10, 10, 10);
            GeoSolid3 second = Box(20, 0, 0, 30, 10, 10);

            Chain().TrySplitBy(new[] { first, second }, out GeoPolyline3[] inA, out GeoPolyline3[] outA);
            Chain().TrySplitBy(new[] { second, first }, out GeoPolyline3[] inB, out GeoPolyline3[] outB);

            Assert.Equal(inA.Length, inB.Length);
            Assert.Equal(outA.Length, outB.Length);
            Assert.Equal(TotalLength(inA), TotalLength(inB), 9);
            Assert.Equal(TotalLength(outA), TotalLength(outB), 9);
        }

        [Fact]
        public void ABentChain_IsCutAcrossItsCorners()
        {
            // Turns the corner inside the body, so the piece inside spans two edges of the chain.
            GeoPolyline3 bent = new GeoPolyline3(
                new GeoPoint3(-10, 5, 5), new GeoPoint3(5, 5, 5), new GeoPoint3(5, 5, 40));

            Assert.True(bent.TrySplitBy(new[] { Box(0, 0, 0, 10, 10, 10) }, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);

            // From x = 0 along to the corner at x = 5, then up from z = 5 to z = 10.
            Assert.Equal(10.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        #endregion
    }
}
