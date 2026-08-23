using System;
using System.Collections.Generic;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers putting geometry together and taking it apart again.
    /// </summary>
    public class MergeAndSplitionTests
    {
        #region Merge3

        [Fact]
        public void ConsecutiveCollinearSegmentsBecomeOne()
        {
            GeoLine3[] merged = Merge3.ConsecutiveLines(new[]
            {
                new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0)),
                new GeoLine3(new GeoPoint3(3, 0, 0), new GeoPoint3(7, 0, 0)),
                new GeoLine3(new GeoPoint3(7, 0, 0), new GeoPoint3(10, 0, 0))
            });

            Assert.Single(merged);
            Assert.Equal(10.0, merged[0].Length, 9);
        }

        [Fact]
        public void ATurnBreaksTheRun()
        {
            GeoLine3[] merged = Merge3.ConsecutiveLines(new[]
            {
                new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0)),
                new GeoLine3(new GeoPoint3(3, 0, 0), new GeoPoint3(3, 4, 0))
            });

            Assert.Equal(2, merged.Length);
        }

        [Fact]
        public void AGapBreaksTheRunEvenWhenTheDirectionsAgree()
        {
            GeoLine3[] merged = Merge3.ConsecutiveLines(new[]
            {
                new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0)),
                new GeoLine3(new GeoPoint3(5, 0, 0), new GeoPoint3(8, 0, 0))
            });

            Assert.Equal(2, merged.Length);
        }

        [Fact]
        public void ASegmentDoublingBackIsNotMerged()
        {
            GeoLine3[] merged = Merge3.ConsecutiveLines(new[]
            {
                new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)),
                new GeoLine3(new GeoPoint3(10, 0, 0), new GeoPoint3(4, 0, 0))
            });

            Assert.Equal(2, merged.Length);
        }

        [Fact]
        public void PolylinesMeetingEndToStartConcatenate()
        {
            GeoPolyline3 merged = Merge3.Polylines(new[]
            {
                new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0)),
                new GeoPolyline3(new GeoPoint3(3, 0, 0), new GeoPoint3(3, 4, 0))
            });

            Assert.Equal(3, merged.VertexCount);
            Assert.Equal(7.0, merged.Length, 9);
        }

        [Fact]
        public void TheStrictFormRefusesAGap()
        {
            Assert.Throws<ArgumentException>(() => Merge3.Polylines(new[]
            {
                new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0)),
                new GeoPolyline3(new GeoPoint3(9, 0, 0), new GeoPoint3(9, 4, 0))
            }));
        }

        [Fact]
        public void TheForgivingFormStartsANewChainAtAGap()
        {
            GeoPolyline3[] merged = Merge3.ConsecutivePolylines(new[]
            {
                new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0)),
                new GeoPolyline3(new GeoPoint3(9, 0, 0), new GeoPoint3(9, 4, 0))
            });

            Assert.Equal(2, merged.Length);
        }

        [Fact]
        public void JoinReassemblesAShuffledAndReversedSetIntoOneChain()
        {
            // The three pieces of a single path, given out of order and some running backwards.
            GeoPolyline3[] joined = Merge3.Join(new[]
            {
                new GeoPolyline3(new GeoPoint3(3, 4, 0), new GeoPoint3(3, 0, 0)),   // reversed middle
                new GeoPolyline3(new GeoPoint3(3, 4, 0), new GeoPoint3(8, 4, 0)),   // last
                new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0))    // reversed first
            });

            Assert.Single(joined);
            Assert.Equal(4, joined[0].VertexCount);
            Assert.Equal(3.0 + 4.0 + 5.0, joined[0].Length, 9);
        }

        [Fact]
        public void JoinLeavesDisconnectedSetsAsSeparateChains()
        {
            GeoPolyline3[] joined = Merge3.Join(new[]
            {
                new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)),
                new GeoLine3(new GeoPoint3(1, 0, 0), new GeoPoint3(2, 0, 0)),
                new GeoLine3(new GeoPoint3(50, 0, 0), new GeoPoint3(51, 0, 0))
            });

            Assert.Equal(2, joined.Length);
        }

        [Fact]
        public void JoinUsesEveryPieceExactlyOnce()
        {
            GeoLine3[] edges =
            {
                new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)),
                new GeoLine3(new GeoPoint3(1, 0, 0), new GeoPoint3(2, 0, 0)),
                new GeoLine3(new GeoPoint3(2, 0, 0), new GeoPoint3(3, 0, 0)),
                new GeoLine3(new GeoPoint3(10, 0, 0), new GeoPoint3(11, 0, 0))
            };

            double totalIn = 0.0;
            foreach (GeoLine3 edge in edges)
            {
                totalIn += edge.Length;
            }

            double totalOut = 0.0;
            foreach (GeoPolyline3 chain in Merge3.Join(edges))
            {
                totalOut += chain.Length;
            }

            Assert.Equal(totalIn, totalOut, 9);
        }

        #endregion

        #region Splitting curves

        [Fact]
        public void ASegmentSplitsInTwoAtADistance()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            Assert.True(line.TrySplitAtDistance(4.0, out GeoLine3[] pieces));
            Assert.Equal(2, pieces.Length);
            Assert.Equal(4.0, pieces[0].Length, 9);
            Assert.Equal(6.0, pieces[1].Length, 9);

            // The pieces come back in order along the subject.
            Assert.True(pieces[0].StartPoint.IsEqualTo(line.StartPoint));
            Assert.True(pieces[1].EndPoint.IsEqualTo(line.EndPoint));
            Assert.True(pieces[0].EndPoint.IsEqualTo(pieces[1].StartPoint));
        }

        [Fact]
        public void ACutAtAnEndpointIsRefusedAndHandsBackTheSubjectWhole()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            Assert.False(line.TrySplitAtDistance(0.0, out GeoLine3[] atStart));
            Assert.Single(atStart);
            Assert.Equal(line, atStart[0]);

            Assert.False(line.TrySplitAtDistance(10.0, out GeoLine3[] atEnd));
            Assert.Single(atEnd);

            Assert.False(line.TrySplitAtDistance(20.0, out GeoLine3[] beyond));
            Assert.Single(beyond);
        }

        [Fact]
        public void APointOffTheSubjectIsRefusedRatherThanProjectedOntoIt()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            Assert.False(line.TrySplitBy(new GeoPoint3(5, 3, 0), out GeoLine3[] pieces));
            Assert.Single(pieces);
            Assert.True(line.TrySplitBy(new GeoPoint3(5, 0, 0), out _));
        }

        [Fact]
        public void SplittingAtSeveralDistancesKeepsTheTotalLength()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            GeoLine3[] pieces = line.SplitAtDistances(new[] { 7.0, 2.0, 5.0 });

            Assert.Equal(4, pieces.Length);

            double total = 0.0;
            foreach (GeoLine3 piece in pieces)
            {
                total += piece.Length;
            }

            Assert.Equal(line.Length, total, 9);
        }

        [Fact]
        public void PositionsOutsideOrTooCloseTogetherAreDroppedFromAMultipleSplit()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            // -1 and 40 are off the segment; 5.0 and 5.00001 are the same cut.
            GeoLine3[] pieces = line.SplitAtDistances(new[] { -1.0, 5.0, 5.00001, 40.0 });

            Assert.Equal(2, pieces.Length);

            foreach (GeoLine3 piece in pieces)
            {
                Assert.False(piece.IsDegenerate());
            }
        }

        [Fact]
        public void APlaneCutsASegmentWhereItCrossesIt()
        {
            GeoLine3 line = new GeoLine3(new GeoPoint3(0, 0, -4), new GeoPoint3(0, 0, 6));

            Assert.True(line.TrySplitBy(GeoPlane3.XY, out GeoLine3[] pieces));
            Assert.Equal(2, pieces.Length);
            Assert.Equal(4.0, pieces[0].Length, 9);
            Assert.Equal(6.0, pieces[1].Length, 9);
        }

        [Fact]
        public void APolylineSplitsAtACornerAndAlongASegment()
        {
            GeoPolyline3 chain = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0), new GeoPoint3(3, 4, 0));

            Assert.True(chain.TrySplitAtDistance(3.0, out GeoPolyline3[] atCorner));
            Assert.Equal(2, atCorner.Length);
            Assert.Equal(3.0, atCorner[0].Length, 9);
            Assert.Equal(4.0, atCorner[1].Length, 9);

            Assert.True(chain.TrySplitAtDistance(5.0, out GeoPolyline3[] midway));
            Assert.Equal(2, midway.Length);
            Assert.Equal(5.0, midway[0].Length, 9);
            Assert.Equal(3, midway[0].VertexCount);
        }

        [Fact]
        public void SplittingAPolylineKeepsItsTotalLength()
        {
            GeoPolyline3 chain = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0), new GeoPoint3(3, 4, 0), new GeoPoint3(3, 4, 12));

            GeoPolyline3[] pieces = chain.SplitAtDistances(new[] { 1.0, 3.0, 6.0, 15.0 });

            double total = 0.0;
            foreach (GeoPolyline3 piece in pieces)
            {
                total += piece.Length;
            }

            Assert.Equal(chain.Length, total, 9);
            Assert.Equal(5, pieces.Length);
        }

        [Fact]
        public void APlaneCrossingAChainSeveralTimesCutsItAtEveryCrossing()
        {
            // A zig-zag that crosses the XY plane twice.
            GeoPolyline3 zigzag = new GeoPolyline3(
                new GeoPoint3(0, 0, -2),
                new GeoPoint3(1, 0, 2),
                new GeoPoint3(2, 0, -2),
                new GeoPoint3(3, 0, 2));

            Assert.True(zigzag.TrySplitBy(GeoPlane3.XY, out GeoPolyline3[] pieces));
            Assert.Equal(4, pieces.Length);

            double total = 0.0;
            foreach (GeoPolyline3 piece in pieces)
            {
                total += piece.Length;
            }

            Assert.Equal(zigzag.Length, total, 9);
        }

        [Fact]
        public void APlaneMissingAChainDoesNotCutIt()
        {
            GeoPolyline3 chain = new GeoPolyline3(
                new GeoPoint3(0, 0, 5), new GeoPoint3(3, 0, 5), new GeoPoint3(3, 4, 5));

            Assert.False(chain.TrySplitBy(GeoPlane3.XY, out GeoPolyline3[] pieces));
            Assert.Single(pieces);
            Assert.Equal(chain, pieces[0]);
        }

        #endregion

        #region Splitting a polygon

        [Fact]
        public void AConvexPolygonSplitsIntoOnePiecePerSide()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            // A vertical plane through the middle, its normal pointing along +X.
            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(5, 0, 0), GeoVector3.XAxis);

            Assert.True(square.TrySplitBy(cutter, out GeoPolygon3[] above, out GeoPolygon3[] below));
            Assert.Single(above);
            Assert.Single(below);
            Assert.Equal(50.0, above[0].Area, 6);
            Assert.Equal(50.0, below[0].Area, 6);
        }

        [Fact]
        public void ThePiecesOfASplitPolygonAddUpToTheOriginal()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(3, 0, 0), new GeoVector3(1, 1, 0));

            Assert.True(square.TrySplitBy(cutter, out GeoPolygon3[] above, out GeoPolygon3[] below));

            double total = 0.0;
            foreach (GeoPolygon3 piece in above)
            {
                total += piece.Area;
            }

            foreach (GeoPolygon3 piece in below)
            {
                total += piece.Area;
            }

            Assert.Equal(square.Area, total, 6);
        }

        [Fact]
        public void EveryPieceKeepsTheOrientationOfTheSubject()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(5, 0, 0), GeoVector3.XAxis);

            Assert.True(square.TrySplitBy(cutter, out GeoPolygon3[] above, out GeoPolygon3[] below));

            foreach (GeoPolygon3 piece in above)
            {
                Assert.True(piece.Normal.IsCodirectionalTo(square.Normal));
            }

            foreach (GeoPolygon3 piece in below)
            {
                Assert.True(piece.Normal.IsCodirectionalTo(square.Normal));
            }
        }

        [Fact]
        public void EachPieceLandsOnTheSideItIsReportedOn()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(5, 0, 0), GeoVector3.XAxis);

            Assert.True(square.TrySplitBy(cutter, out GeoPolygon3[] above, out GeoPolygon3[] below));

            Assert.True(cutter.SignedDistanceTo(above[0].Centroid) > 0.0);
            Assert.True(cutter.SignedDistanceTo(below[0].Centroid) < 0.0);
        }

        [Fact]
        public void AConcavePolygonCanFallIntoMorePiecesOnOneSide()
        {
            // A U shape opening upwards. A horizontal cut through the arms leaves one piece below and
            // two above, which is why each side comes back as an array.
            GeoPolygon3 uShape = new GeoPolygon3(
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(9, 0, 0),
                new GeoPoint3(9, 10, 0),
                new GeoPoint3(6, 10, 0),
                new GeoPoint3(6, 4, 0),
                new GeoPoint3(3, 4, 0),
                new GeoPoint3(3, 10, 0),
                new GeoPoint3(0, 10, 0));

            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(0, 7, 0), GeoVector3.YAxis);

            Assert.True(uShape.TrySplitBy(cutter, out GeoPolygon3[] above, out GeoPolygon3[] below));

            Assert.Equal(2, above.Length);
            Assert.Single(below);

            double total = 0.0;
            foreach (GeoPolygon3 piece in above)
            {
                total += piece.Area;
            }

            foreach (GeoPolygon3 piece in below)
            {
                total += piece.Area;
            }

            Assert.Equal(uShape.Area, total, 6);

            // Each arm above the cut is 3 wide and 3 tall.
            foreach (GeoPolygon3 arm in above)
            {
                Assert.Equal(9.0, arm.Area, 6);
            }
        }

        [Fact]
        public void APlaneMissingAPolygonHandsItBackWholeOnTheSideItLiesOn()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            GeoPlane3 farAway = new GeoPlane3(new GeoPoint3(-50, 0, 0), GeoVector3.XAxis);

            Assert.False(square.TrySplitBy(farAway, out GeoPolygon3[] above, out GeoPolygon3[] below));
            Assert.Single(above);
            Assert.Empty(below);
            Assert.Equal(square, above[0]);
        }

        #endregion

        #region Splitting a solid

        private static GeoSolid3 MakeBoxSolid(GeoPoint3 min, GeoPoint3 max) =>
            new GeoAabb3(min, max).ToObb().ToSolid();

        [Fact]
        public void AConvexSolidSplitsIntoTwoClosedHalves()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPlane3 cutter = GeoPlane3.XY.Offset(4.0);

            Assert.True(cube.TrySplitBy(cutter, out GeoSolid3 above, out GeoSolid3 below));

            Assert.True(above.IsClosed());
            Assert.True(below.IsClosed());
            Assert.Equal(600.0, above.Volume, 4);
            Assert.Equal(400.0, below.Volume, 4);
            Assert.Equal(cube.Volume, above.Volume + below.Volume, 4);
        }

        [Fact]
        public void TheHalvesLandOnTheSidesTheyAreReportedOn()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPlane3 cutter = GeoPlane3.XY.Offset(4.0);

            Assert.True(cube.TrySplitBy(cutter, out GeoSolid3 above, out GeoSolid3 below));

            Assert.True(cutter.SignedDistanceTo(above.Centroid) > 0.0);
            Assert.True(cutter.SignedDistanceTo(below.Centroid) < 0.0);
        }

        [Fact]
        public void ASlantedCutAlsoDividesTheVolumeExactly()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));

            // A plane through the middle at 45 degrees: it cuts the cube into two equal wedges.
            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(5, 5, 5), new GeoVector3(1, 0, 1));

            Assert.True(cube.TrySplitBy(cutter, out GeoSolid3 above, out GeoSolid3 below));

            Assert.Equal(cube.Volume, above.Volume + below.Volume, 4);
            Assert.Equal(above.Volume, below.Volume, 4);
        }

        [Fact]
        public void APlaneMissingASolidDoesNotSplitIt()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));

            Assert.False(cube.TrySplitBy(GeoPlane3.XY.Offset(50.0), out GeoSolid3 above, out GeoSolid3 below));
            Assert.Equal(cube, above);
            Assert.Equal(cube, below);
        }

        [Fact]
        public void APlaneGrazingAFaceDoesNotSplitTheSolid()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));

            Assert.False(cube.TrySplitBy(GeoPlane3.XY, out _, out _));
        }

        [Fact]
        public void AConcaveSolidSplitsIntoTwoClosedHalves()
        {
            // An L-shaped prism: closed, but nowhere near convex.
            GeoSolid3 lShape = MakeLShapedSolid();

            Assert.True(lShape.IsClosed());
            Assert.Equal(384.0, lShape.Volume, 4);

            Assert.True(lShape.TrySplitBy(GeoPlane3.XY.Offset(3.0), out GeoSolid3 above, out GeoSolid3 below));

            Assert.True(above.IsClosed());
            Assert.True(below.IsClosed());
            Assert.Equal(192.0, above.Volume, 4);
            Assert.Equal(192.0, below.Volume, 4);
            Assert.Equal(lShape.Volume, above.Volume + below.Volume, 4);
        }

        [Fact]
        public void AConcaveSolidCutThroughItsNotchStillCloses()
        {
            // A vertical plane that only reaches the leg of the L, leaving an L-shaped section.
            GeoSolid3 lShape = MakeLShapedSolid();
            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(0, 7, 0), GeoVector3.YAxis);

            Assert.True(lShape.TrySplitBy(cutter, out GeoSolid3 above, out GeoSolid3 below));

            Assert.True(above.IsClosed());
            Assert.True(below.IsClosed());

            // Above the cut only the leg survives: 4 wide, 3 deep, 6 tall.
            Assert.Equal(72.0, above.Volume, 4);
            Assert.Equal(lShape.Volume, above.Volume + below.Volume, 4);
        }

        [Fact]
        public void CuttingAlongAPlaneTheBodyAlreadyHasAFaceInKeepsBothHalvesWhole()
        {
            // The L-shaped prism has a face lying in the plane y = 4, the wall of its notch. Cutting along
            // that plane leaves the two halves meeting the plane over different areas: the foot spans x 0
            // to 10 there, the leg only x 0 to 4. Capping one half with the other turned over loses the
            // difference, and the volumes stop adding up.
            GeoSolid3 lShape = MakeLShapedSolid();
            GeoPlane3 alongTheNotch = new GeoPlane3(new GeoPoint3(0, 4, 0), GeoVector3.YAxis);

            Assert.True(lShape.TrySplitBy(alongTheNotch, out GeoSolid3 leg, out GeoSolid3 foot));

            Assert.True(leg.IsClosed());
            Assert.True(foot.IsClosed());

            // The foot is 10 by 4 by 6, the leg 4 by 6 by 6.
            Assert.Equal(240.0, foot.Volume, 4);
            Assert.Equal(144.0, leg.Volume, 4);
            Assert.Equal(lShape.Volume, foot.Volume + leg.Volume, 4);
        }

        [Fact]
        public void RepeatedCuttingNeverLosesVolume()
        {
            // Cutting by every face plane of a body, its own included, is what the boolean operations do.
            // Any cut that mishandles a plane holding an existing face shows up here as volume going
            // missing, which is how the case above was found.
            GeoSolid3 lShape = MakeLShapedSolid();

            List<GeoPlane3> planes = new List<GeoPlane3>();
            foreach (GeoFace3 face in lShape.Faces)
            {
                planes.Add(face.GetPlane());
            }

            planes.Add(new GeoPlane3(new GeoPoint3(6, 0, 0), GeoVector3.XAxis));
            planes.Add(new GeoPlane3(new GeoPoint3(0, 1, 0), GeoVector3.YAxis));
            planes.Add(GeoPlane3.XY.Offset(2.0));

            List<GeoSolid3> cells = new List<GeoSolid3> { lShape };

            foreach (GeoPlane3 plane in planes)
            {
                List<GeoSolid3> divided = new List<GeoSolid3>();

                foreach (GeoSolid3 cell in cells)
                {
                    if (cell.TrySplitBy(plane, out GeoSolid3 above, out GeoSolid3 below))
                    {
                        divided.Add(above);
                        divided.Add(below);
                    }
                    else
                    {
                        divided.Add(cell);
                    }
                }

                cells = divided;
            }

            double total = 0.0;
            foreach (GeoSolid3 cell in cells)
            {
                Assert.True(cell.IsClosed());
                total += cell.Volume;
            }

            Assert.Equal(lShape.Volume, total, 4);
        }

        [Fact]
        public void ACutCanLeaveOneHalfAsSeveralDisconnectedShells()
        {
            // A U-shaped prism cut across both arms. The upper half is two separate bodies, and the
            // section is therefore two loops rather than one.
            GeoSolid3 uShape = MakeUShapedSolid();

            Assert.Equal(360.0, uShape.Volume, 4);

            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(0, 7, 0), GeoVector3.YAxis);

            Assert.True(uShape.TrySplitBy(cutter, out GeoSolid3 above, out GeoSolid3 below));

            Assert.True(above.IsClosed());
            Assert.True(below.IsClosed());
            Assert.Equal(90.0, above.Volume, 4);
            Assert.Equal(270.0, below.Volume, 4);

            // Two arms, so two caps: the upper half has both of them.
            Assert.Equal(2, CountFacesOnPlane(above, cutter));
        }

        [Fact]
        public void CuttingAHollowBodyLeavesACapWithAHoleInIt()
        {
            // A square tube: the section across it is a ring, not a disc, so each cap needs a hole.
            GeoSolid3 tube = MakeTubeSolid();

            Assert.True(tube.IsClosed());
            Assert.Equal(840.0, tube.Volume, 4);

            Assert.True(tube.TrySplitBy(GeoPlane3.XY.Offset(5.0), out GeoSolid3 above, out GeoSolid3 below));

            Assert.Equal(420.0, above.Volume, 4);
            Assert.Equal(420.0, below.Volume, 4);
            Assert.Equal(tube.Volume, above.Volume + below.Volume, 4);

            GeoFace3 cap = FindFaceOnPlane(above, GeoPlane3.XY.Offset(5.0));
            Assert.Single(cap.Holes);
            Assert.Equal(84.0, cap.Area, 4);
        }

        [Fact]
        public void AFaceKeepsAHoleThePlaneMisses()
        {
            GeoFace3 plate = MakePlateWithHole();
            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(9, 0, 0), GeoVector3.XAxis);

            Assert.True(plate.TrySplitBy(cutter, out GeoFace3[] above, out GeoFace3[] below));

            Assert.Single(above);
            Assert.Single(below);
            Assert.Empty(above[0].Holes);
            Assert.Single(below[0].Holes);
            Assert.Equal(plate.Area, above[0].Area + below[0].Area, 6);
        }

        [Fact]
        public void APlaneThroughAHoleTurnsItIntoPartOfTheBoundary()
        {
            // Cutting straight through the hole leaves each piece with a boundary made partly of the old
            // outer edge and partly of the old rim, so neither piece has a hole any more.
            GeoFace3 plate = MakePlateWithHole();
            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(5, 0, 0), GeoVector3.XAxis);

            Assert.True(plate.TrySplitBy(cutter, out GeoFace3[] above, out GeoFace3[] below));

            Assert.Single(above);
            Assert.Single(below);
            Assert.Empty(above[0].Holes);
            Assert.Empty(below[0].Holes);
            Assert.Equal(plate.Area, above[0].Area + below[0].Area, 6);
            Assert.Equal(48.0, above[0].Area, 6);
        }

        /// <summary>
        /// Builds the plate with a square hole used by the face-splitting tests.
        /// </summary>
        private static GeoFace3 MakePlateWithHole()
        {
            GeoPolygon3 boundary = new GeoPolygon3(
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0),
                new GeoPoint3(0, 10, 0));

            GeoPolygon3 hole = new GeoPolygon3(
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(6, 4, 0),
                new GeoPoint3(6, 6, 0),
                new GeoPoint3(4, 6, 0));

            return new GeoFace3(boundary, new[] { hole });
        }

        /// <summary>
        /// Counts the faces of a solid that lie entirely on a plane.
        /// </summary>
        private static int CountFacesOnPlane(GeoSolid3 solid, GeoPlane3 plane)
        {
            int count = 0;

            foreach (GeoFace3 face in solid.Faces)
            {
                if (plane.ContainsAll(face.Boundary.Vertices))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Finds the single face of a solid that lies on a plane.
        /// </summary>
        private static GeoFace3 FindFaceOnPlane(GeoSolid3 solid, GeoPlane3 plane)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                if (plane.ContainsAll(face.Boundary.Vertices))
                {
                    return face;
                }
            }

            throw new InvalidOperationException("No face of the solid lies on that plane.");
        }

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

                faces.Add(new GeoFace3(new GeoPolygon3(
                    profile[i], profile[next], top[next], top[i])));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// Builds a closed but concave solid: an L-shaped prism 6 units tall, of volume 384.
        /// </summary>
        private static GeoSolid3 MakeLShapedSolid()
        {
            return MakePrism(new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 4, 0),
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(4, 10, 0),
                new GeoPoint3(0, 10, 0)
            }, 6.0);
        }

        /// <summary>
        /// Builds a U-shaped prism 5 units tall, of volume 360.
        /// </summary>
        private static GeoSolid3 MakeUShapedSolid()
        {
            return MakePrism(new[]
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
        }

        /// <summary>
        /// Builds a square tube: a 10 x 10 x 10 block with a 4 x 4 shaft running right through it.
        /// </summary>
        /// <remarks>
        /// The shaft is part of the boundary rather than an opening, so the top and bottom faces carry a
        /// hole and there are four inner walls wound to face into the void. That is what makes the section
        /// across it a ring.
        /// </remarks>
        private static GeoSolid3 MakeTubeSolid()
        {
            GeoPoint3[] outer =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0),
                new GeoPoint3(0, 10, 0)
            };

            GeoPoint3[] inner =
            {
                new GeoPoint3(3, 3, 0),
                new GeoPoint3(7, 3, 0),
                new GeoPoint3(7, 7, 0),
                new GeoPoint3(3, 7, 0)
            };

            const double height = 10.0;
            GeoVector3 up = new GeoVector3(0, 0, height);

            List<GeoFace3> faces = new List<GeoFace3>();

            // Bottom: wound the other way so its normal points down and out, hole wound to match.
            GeoPoint3[] bottomOuter = { outer[3], outer[2], outer[1], outer[0] };
            GeoPoint3[] bottomInner = { inner[3], inner[2], inner[1], inner[0] };

            faces.Add(new GeoFace3(
                new GeoPolygon3(bottomOuter),
                new[] { new GeoPolygon3(bottomInner) }));

            GeoPoint3[] topOuter = new GeoPoint3[4];
            GeoPoint3[] topInner = new GeoPoint3[4];
            for (int i = 0; i < 4; i++)
            {
                topOuter[i] = outer[i].Add(up);
                topInner[i] = inner[i].Add(up);
            }

            faces.Add(new GeoFace3(
                new GeoPolygon3(topOuter),
                new[] { new GeoPolygon3(topInner) }));

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;

                faces.Add(new GeoFace3(new GeoPolygon3(
                    outer[i], outer[next], outer[next].Add(up), outer[i].Add(up))));

                // The inner walls run the other way round, so their normals point into the shaft, which is
                // out of the material.
                faces.Add(new GeoFace3(new GeoPolygon3(
                    inner[next], inner[i], inner[i].Add(up), inner[next].Add(up))));
            }

            return new GeoSolid3(faces);
        }

        #endregion
    }
}
