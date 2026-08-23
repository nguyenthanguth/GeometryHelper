using System;
using System.Collections.Generic;
using CommonGeometry;
using SolidGeometry;
using SolidGeometry.Core;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers cutting a curve by a closed body, where the pieces are sorted into inside and outside rather
    /// than above and below.
    /// </summary>
    public class SplitByVolumeTests
    {
        private static GeoSolid3 MakeBoxSolid(GeoPoint3 min, GeoPoint3 max) =>
            new GeoAabb3(min, max).ToObb().ToSolid();

        /// <summary>
        /// A cube from (0,0,0) to (10,10,10).
        /// </summary>
        private static GeoSolid3 MakeCube() => MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));

        /// <summary>
        /// Builds a closed prism from a profile walked counter-clockwise seen from +Z.
        /// </summary>
        private static GeoSolid3 MakePrism(GeoPoint3[] profile, double height)
        {
            GeoPoint3[] top = new GeoPoint3[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                top[i] = profile[i].Add(new GeoVector3(0, 0, height));
            }

            List<GeoFace3> faces = new List<GeoFace3>();

            GeoPoint3[] bottomReversed = new GeoPoint3[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                bottomReversed[i] = profile[profile.Length - 1 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < profile.Length; i++)
            {
                int next = (i + 1) % profile.Length;
                faces.Add(new GeoFace3(new GeoPolygon3(profile[i], profile[next], top[next], top[i])));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// Sums the length of a set of chains.
        /// </summary>
        private static double TotalLength(GeoPolyline3[] pieces)
        {
            double total = 0.0;

            foreach (GeoPolyline3 piece in pieces)
            {
                total += piece.Length;
            }

            return total;
        }

        [Fact]
        public void AChainThroughASolidComesBackInThreePieces()
        {
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            Assert.True(chain.TrySplitBy(MakeCube(), out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(2, outside.Length);
            Assert.Equal(10.0, inside[0].Length, 9);
            Assert.Equal(15.0, TotalLength(outside), 9);
        }

        [Fact]
        public void TheTotalLengthOfEveryPieceMatchesTheSubject()
        {
            GeoPolyline3 chain = new GeoPolyline3(
                new GeoPoint3(-5, 5, 5),
                new GeoPoint3(5, 5, 5),
                new GeoPoint3(5, 5, 20),
                new GeoPoint3(30, 5, 20));

            Assert.True(chain.TrySplitBy(MakeCube(), out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Equal(chain.Length, TotalLength(inside) + TotalLength(outside), 9);
        }

        [Fact]
        public void EveryPieceLandsInTheBucketItsMidpointBelongsTo()
        {
            GeoSolid3 cube = MakeCube();
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            Assert.True(chain.TrySplitBy(cube, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            foreach (GeoPolyline3 piece in inside)
            {
                Assert.True(cube.Contains(piece.GetPointAtParameter(0.5)));
            }

            foreach (GeoPolyline3 piece in outside)
            {
                Assert.False(cube.Contains(piece.GetPointAtParameter(0.5)));
            }
        }

        [Fact]
        public void AChainWhollyInsideOrWhollyOutsideIsNotCut()
        {
            GeoSolid3 cube = MakeCube();

            GeoPolyline3 within = new GeoPolyline3(new GeoPoint3(2, 2, 2), new GeoPoint3(8, 8, 8));
            GeoPolyline3 beyond = new GeoPolyline3(new GeoPoint3(50, 50, 50), new GeoPoint3(60, 60, 60));

            Assert.False(within.TrySplitBy(cube, out GeoPolyline3[] inside, out GeoPolyline3[] outside));
            Assert.Single(inside);
            Assert.Empty(outside);
            Assert.Equal(within, inside[0]);

            Assert.False(beyond.TrySplitBy(cube, out inside, out outside));
            Assert.Empty(inside);
            Assert.Single(outside);
            Assert.Equal(beyond, outside[0]);
        }

        [Fact]
        public void AChainCrossingSeveralTimesAlternatesBetweenTheBuckets()
        {
            // Two separate cubes standing side by side, and a chain skewering both.
            GeoSolid3 first = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(35, 5, 5));

            Assert.True(chain.TrySplitBy(first, out GeoPolyline3[] inside, out GeoPolyline3[] outside));
            Assert.Single(inside);
            Assert.Equal(2, outside.Length);

            GeoSolid3 second = MakeBoxSolid(new GeoPoint3(20, 0, 0), new GeoPoint3(30, 10, 10));
            Assert.True(chain.TrySplitBy(second, out GeoPolyline3[] inside2, out GeoPolyline3[] outside2));
            Assert.Single(inside2);
            Assert.Equal(2, outside2.Length);
            Assert.Equal(10.0, inside2[0].Length, 9);
        }

        [Fact]
        public void AConcaveSolidCanTakeTheChainInAndOutMoreThanOnce()
        {
            // A U-shaped prism. A straight chain across both arms is inside, outside, inside, outside...
            GeoSolid3 uShape = MakePrism(new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(9, 0, 0),
                new GeoPoint3(9, 10, 0),
                new GeoPoint3(6, 10, 0),
                new GeoPoint3(6, 4, 0),
                new GeoPoint3(3, 4, 0),
                new GeoPoint3(3, 10, 0),
                new GeoPoint3(0, 10, 0)
            }, 5.0);

            // At y = 7 the profile is material over x in [0,3] and [6,9], and void between.
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 7, 2), new GeoPoint3(15, 7, 2));

            Assert.True(chain.TrySplitBy(uShape, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Equal(2, inside.Length);
            Assert.Equal(3, outside.Length);
            Assert.Equal(chain.Length, TotalLength(inside) + TotalLength(outside), 9);

            foreach (GeoPolyline3 arm in inside)
            {
                Assert.Equal(3.0, arm.Length, 9);
            }
        }

        [Fact]
        public void AChainThroughAnOpeningIsOutsideWhileItIsInTheVoid()
        {
            GeoSolid3 slab = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoSolid3 duct = MakeBoxSolid(new GeoPoint3(4, 4, 0), new GeoPoint3(6, 6, 10));
            GeoSolid3 pierced = slab.WithOpenings(new[] { duct });

            // Straight down the middle of the duct, entering and leaving through the material above and
            // below it. The duct runs the full height, so the chain is inside only where it is outside the
            // duct footprint — which along this line is nowhere.
            GeoPolyline3 throughDuct = new GeoPolyline3(new GeoPoint3(5, 5, -5), new GeoPoint3(5, 5, 15));

            Assert.False(throughDuct.TrySplitBy(pierced, out GeoPolyline3[] inside, out GeoPolyline3[] outside));
            Assert.Empty(inside);
            Assert.Single(outside);

            // Beside the duct the material is solid, so the chain is cut and its middle piece is inside.
            GeoPolyline3 besideDuct = new GeoPolyline3(new GeoPoint3(2, 2, -5), new GeoPoint3(2, 2, 15));

            Assert.True(besideDuct.TrySplitBy(pierced, out inside, out outside));
            Assert.Single(inside);
            Assert.Equal(10.0, inside[0].Length, 9);
        }

        [Fact]
        public void ACrossingChainIsCutWhereItEntersTheOpening()
        {
            GeoSolid3 slab = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoSolid3 duct = MakeBoxSolid(new GeoPoint3(4, 4, 0), new GeoPoint3(6, 6, 10));
            GeoSolid3 pierced = slab.WithOpenings(new[] { duct });

            // Across the slab at mid height, straight through the duct: material, void, material.
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(15, 5, 5));

            Assert.True(chain.TrySplitBy(pierced, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Equal(2, inside.Length);
            Assert.Equal(3, outside.Length);
            Assert.Equal(chain.Length, TotalLength(inside) + TotalLength(outside), 9);

            foreach (GeoPolyline3 piece in inside)
            {
                Assert.Equal(4.0, piece.Length, 9);
            }
        }

        [Fact]
        public void ASegmentIsCutTheSameWayAChainIs()
        {
            GeoSolid3 cube = MakeCube();
            GeoLine3 segment = new GeoLine3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            Assert.True(segment.TrySplitBy(cube, out GeoLine3[] inside, out GeoLine3[] outside));

            Assert.Single(inside);
            Assert.Equal(2, outside.Length);
            Assert.Equal(10.0, inside[0].Length, 9);
        }

        [Fact]
        public void AnOrientedBoxCutsTheSameWayItsSolidDoes()
        {
            GeoObb3 box = new GeoObb3(new GeoPoint3(5, 5, 5), 10, 10, 10);
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            Assert.True(chain.TrySplitBy(box, out GeoPolyline3[] boxInside, out GeoPolyline3[] boxOutside));
            Assert.True(chain.TrySplitBy(box.ToSolid(), out GeoPolyline3[] solidInside, out GeoPolyline3[] solidOutside));

            Assert.Equal(solidInside.Length, boxInside.Length);
            Assert.Equal(solidOutside.Length, boxOutside.Length);
            Assert.Equal(TotalLength(solidInside), TotalLength(boxInside), 9);
        }

        [Fact]
        public void ARotatedBoxCutsAlongItsOwnAxes()
        {
            GeoObb3 box = new GeoObb3(
                GeoPoint3.Origin, 20, 4, 4,
                new GeoVector3(1, 1, 0),
                new GeoVector3(-1, 1, 0));

            // Along the long axis of the box, entering and leaving through its end faces.
            GeoPolyline3 chain = new GeoPolyline3(
                GeoPoint3.Origin.Add(box.AxisX.Multiply(-30)),
                GeoPoint3.Origin.Add(box.AxisX.Multiply(30)));

            Assert.True(chain.TrySplitBy(box, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(20.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        [Fact]
        public void AnAxisAlignedBoxCutsTheSameWayTheOrientedOneDoes()
        {
            GeoAabb3 bounds = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            Assert.True(chain.TrySplitBy(bounds, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(10.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        [Fact]
        public void AnEmptyBoxHoldsNothingSoNothingIsCut()
        {
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            Assert.False(chain.TrySplitBy(GeoAabb3.Empty, out GeoPolyline3[] inside, out GeoPolyline3[] outside));
            Assert.Empty(inside);
            Assert.Single(outside);
        }

        [Fact]
        public void ATangentialTouchDoesNotProduceAZeroLengthPiece()
        {
            GeoSolid3 cube = MakeCube();

            // Grazing exactly along the top face rather than passing through the body.
            GeoPolyline3 grazing = new GeoPolyline3(new GeoPoint3(-5, 5, 10), new GeoPoint3(20, 5, 10));

            grazing.TrySplitBy(cube, out GeoPolyline3[] inside, out GeoPolyline3[] outside);

            foreach (GeoPolyline3 piece in inside)
            {
                Assert.True(piece.Length > Tolerance.Global.EqualPoint);
            }

            foreach (GeoPolyline3 piece in outside)
            {
                Assert.True(piece.Length > Tolerance.Global.EqualPoint);
            }

            Assert.Equal(grazing.Length, TotalLength(inside) + TotalLength(outside), 9);
        }

        [Fact]
        public void APieceLyingOnTheSurfaceCountsAsInside()
        {
            GeoSolid3 cube = MakeCube();

            // Runs along the top face from beyond one edge to beyond the other.
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 10), new GeoPoint3(15, 5, 10));

            Assert.True(chain.TrySplitBy(cube, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            // The stretch lying on the face is reported as inside, following Contains.
            Assert.Equal(10.0, TotalLength(inside), 6);
            Assert.Equal(10.0, TotalLength(outside), 6);
        }

        [Fact]
        public void TheStaticAndInstanceFormsAgree()
        {
            GeoSolid3 cube = MakeCube();
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            bool fromStatic = Splition3.TrySplitBy(chain, cube, out GeoPolyline3[] staticInside, out _);
            bool fromInstance = chain.TrySplitBy(cube, out GeoPolyline3[] instanceInside, out _);

            Assert.Equal(fromStatic, fromInstance);
            Assert.Equal(staticInside.Length, instanceInside.Length);
            Assert.Equal(TotalLength(staticInside), TotalLength(instanceInside), 9);
        }

        [Fact]
        public void NullArgumentsAreRejected()
        {
            GeoPolyline3 chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));

            Assert.Throws<ArgumentNullException>(() => Splition3.TrySplitBy(chain, (GeoSolid3)null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => Splition3.TrySplitBy(chain, (GeoObb3)null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => Splition3.TrySplitBy((GeoPolyline3)null, MakeCube(), out _, out _));
        }
    }
}
