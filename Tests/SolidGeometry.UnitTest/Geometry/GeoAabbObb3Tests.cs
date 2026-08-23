using System;
using CommonGeometry.Enums;
using SolidGeometry;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the two box types: the axis-aligned bound and the oriented shape.
    /// </summary>
    public class GeoAabbObb3Tests
    {
        #region Bounding box

        [Fact]
        public void TheDefaultBoundingBoxIsEmptyRatherThanAPointAtTheOrigin()
        {
            GeoAabb3 box = default;

            Assert.True(box.IsEmpty);
            Assert.Equal(GeoAabb3.Empty, box);
            Assert.Equal(0.0, box.Volume, 9);
            Assert.Empty(box.GetCorners());
        }

        [Fact]
        public void CornersAreSortedWhicheverWayRoundTheyAreGiven()
        {
            GeoAabb3 box = new GeoAabb3(new GeoPoint3(5.0, 5.0, 5.0), new GeoPoint3(1.0, 2.0, 3.0));

            Assert.True(box.Min.IsEqualTo(new GeoPoint3(1.0, 2.0, 3.0)));
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(5.0, 5.0, 5.0)));
        }

        [Fact]
        public void SizesVolumeAndSurfaceAreaFollowTheCorners()
        {
            GeoAabb3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(2.0, 3.0, 4.0));

            Assert.Equal(2.0, box.SizeX, 9);
            Assert.Equal(3.0, box.SizeY, 9);
            Assert.Equal(4.0, box.SizeZ, 9);
            Assert.Equal(24.0, box.Volume, 9);
            Assert.Equal(52.0, box.SurfaceArea, 9);
            Assert.True(box.Center.IsEqualTo(new GeoPoint3(1.0, 1.5, 2.0)));
        }

        [Fact]
        public void FromPointsEnclosesEveryPointAndAnEmptySequenceGivesTheEmptyBox()
        {
            GeoPoint3[] points =
            {
                new GeoPoint3(1.0, 5.0, -2.0),
                new GeoPoint3(-3.0, 0.0, 4.0),
                new GeoPoint3(0.0, 2.0, 0.0)
            };

            GeoAabb3 box = GeoAabb3.FromPoints(points);

            foreach (GeoPoint3 point in points)
            {
                Assert.True(box.Contains(point));
            }

            Assert.True(box.Min.IsEqualTo(new GeoPoint3(-3.0, 0.0, -2.0)));
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(1.0, 5.0, 4.0)));
            Assert.True(GeoAabb3.FromPoints(new GeoPoint3[0]).IsEmpty);
        }

        [Fact]
        public void UnionGrowsAndIntersectShrinks()
        {
            GeoAabb3 first = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0));
            GeoAabb3 second = new GeoAabb3(new GeoPoint3(5.0, 5.0, 5.0), new GeoPoint3(20.0, 20.0, 20.0));

            Assert.True(first.Union(second).Max.IsEqualTo(new GeoPoint3(20.0, 20.0, 20.0)));
            Assert.True(first.Intersect(second).Min.IsEqualTo(new GeoPoint3(5.0, 5.0, 5.0)));
            Assert.True(first.Intersect(second).Max.IsEqualTo(new GeoPoint3(10.0, 10.0, 10.0)));
        }

        [Fact]
        public void IntersectingDisjointBoxesGivesTheEmptyBox()
        {
            GeoAabb3 first = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(1.0, 1.0, 1.0));
            GeoAabb3 second = new GeoAabb3(new GeoPoint3(5.0, 5.0, 5.0), new GeoPoint3(6.0, 6.0, 6.0));

            Assert.True(first.Intersect(second).IsEmpty);
            Assert.False(first.CollidesWith(second));
        }

        [Fact]
        public void TheEmptyBoxIsTheIdentityForUnion()
        {
            GeoAabb3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(1.0, 2.0, 3.0));

            Assert.Equal(box, box.Union(GeoAabb3.Empty));
            Assert.Equal(box, GeoAabb3.Empty.Union(box));
        }

        [Fact]
        public void ShrinkingPastTheWidthGivesTheEmptyBoxRatherThanAnInvertedOne()
        {
            GeoAabb3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(2.0, 2.0, 2.0));

            Assert.False(box.Expand(-0.5).IsEmpty);
            Assert.True(box.Expand(-5.0).IsEmpty);
        }

        [Fact]
        public void LocateSeparatesInteriorSurfaceAndOutside()
        {
            GeoAabb3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0));

            Assert.Equal(PointLocation.Inside, box.Locate(new GeoPoint3(5.0, 5.0, 5.0)));
            Assert.Equal(PointLocation.OnSide, box.Locate(new GeoPoint3(0.0, 5.0, 5.0)));
            Assert.Equal(PointLocation.OutSide, box.Locate(new GeoPoint3(-1.0, 5.0, 5.0)));
        }

        [Fact]
        public void TouchingBoxesCountAsColliding()
        {
            GeoAabb3 first = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0));
            GeoAabb3 touching = new GeoAabb3(new GeoPoint3(10.0, 0.0, 0.0), new GeoPoint3(20.0, 10.0, 10.0));

            Assert.True(first.CollidesWith(touching));
        }

        [Fact]
        public void TheEmptyBoxIsContainedByEverythingAndContainsNothing()
        {
            GeoAabb3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(1.0, 1.0, 1.0));

            Assert.True(box.Contains(GeoAabb3.Empty));
            Assert.False(GeoAabb3.Empty.Contains(box));
            Assert.False(GeoAabb3.Empty.CollidesWith(box));
        }

        #endregion

        #region Oriented box

        [Fact]
        public void VolumeAndSurfaceAreaFollowTheSizes()
        {
            GeoObb3 box = new GeoObb3(GeoPoint3.Origin, 2.0, 3.0, 4.0);

            Assert.Equal(24.0, box.Volume, 9);
            Assert.Equal(52.0, box.SurfaceArea, 9);
            Assert.Equal(1.0, box.ExtentX, 9);
        }

        [Fact]
        public void NegativeSizesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoObb3(GeoPoint3.Origin, -1.0, 1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoObb3(GeoPoint3.Origin, 1.0, -1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoObb3(GeoPoint3.Origin, 1.0, 1.0, -1.0));
        }

        [Fact]
        public void AxesAreMadeOrthonormalEvenWhenTheInputIsSkewed()
        {
            // The Y direction given here is not square to X. A box built from it must still come out with
            // three mutually perpendicular unit axes.
            GeoObb3 box = new GeoObb3(
                GeoPoint3.Origin, 2.0, 2.0, 2.0,
                GeoVector3.XAxis,
                new GeoVector3(0.5, 1.0, 0.0));

            Assert.True(box.AxisX.IsUnitLength());
            Assert.True(box.AxisY.IsUnitLength());
            Assert.True(box.AxisZ.IsUnitLength());
            Assert.True(box.AxisX.IsPerpendicularTo(box.AxisY));
            Assert.True(box.AxisY.IsPerpendicularTo(box.AxisZ));
            Assert.True(box.AxisZ.IsPerpendicularTo(box.AxisX));
            Assert.True(box.AxisX.CrossProduct(box.AxisY).IsEqualTo(box.AxisZ));
        }

        [Fact]
        public void ParallelAxesAreRejectedBecauseTheyDoNotSpanAFrame()
        {
            Assert.Throws<ArgumentException>(() => new GeoObb3(
                GeoPoint3.Origin, 1.0, 1.0, 1.0, GeoVector3.XAxis, GeoVector3.XAxis));
        }

        [Fact]
        public void CornersAreAllOnTheBoxAndSpanIt()
        {
            GeoObb3 box = new GeoObb3(new GeoPoint3(1.0, 2.0, 3.0), 2.0, 4.0, 6.0);
            GeoPoint3[] corners = box.GetCorners();

            Assert.Equal(8, corners.Length);

            foreach (GeoPoint3 corner in corners)
            {
                Assert.Equal(PointLocation.OnSide, box.Locate(corner));
            }

            GeoAabb3 bounds = GeoAabb3.FromPoints(corners);
            Assert.True(bounds.Min.IsEqualTo(new GeoPoint3(0.0, 0.0, 0.0)));
            Assert.True(bounds.Max.IsEqualTo(new GeoPoint3(2.0, 4.0, 6.0)));
        }

        [Fact]
        public void EveryFaceNormalPointsAwayFromTheCentre()
        {
            GeoObb3 box = new GeoObb3(new GeoPoint3(5.0, 5.0, 5.0), 2.0, 4.0, 6.0);

            foreach (GeoPolygon3 face in box.GetFaces())
            {
                GeoVector3 outward = box.Center.GetVectorTo(face.Centroid);

                Assert.True(face.Normal.IsCodirectionalTo(outward));
            }
        }

        [Fact]
        public void LocateSeparatesInteriorSurfaceAndOutsideInTheLocalFrame()
        {
            GeoObb3 box = new GeoObb3(
                GeoPoint3.Origin, 10.0, 10.0, 10.0,
                new GeoVector3(1.0, 1.0, 0.0),
                new GeoVector3(-1.0, 1.0, 0.0));

            Assert.Equal(PointLocation.Inside, box.Locate(GeoPoint3.Origin));
            Assert.Equal(PointLocation.OnSide, box.Locate(box.Center.Add(box.AxisX.Multiply(5.0))));
            Assert.Equal(PointLocation.OutSide, box.Locate(box.Center.Add(box.AxisX.Multiply(5.1))));
        }

        [Fact]
        public void DistanceIsZeroInsideAndPositiveOutside()
        {
            GeoObb3 box = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);

            Assert.Equal(0.0, box.DistanceTo(GeoPoint3.Origin), 9);
            Assert.Equal(5.0, box.DistanceTo(new GeoPoint3(10.0, 0.0, 0.0)), 9);
        }

        [Fact]
        public void BoundingBoxOfAnAxisAlignedBoxMatchesItExactly()
        {
            GeoObb3 box = new GeoObb3(new GeoPoint3(1.0, 2.0, 3.0), 2.0, 4.0, 6.0);
            GeoAabb3 bounds = box.GetAabb();

            Assert.Equal(box.Volume, bounds.Volume, 9);
            Assert.True(bounds.Center.IsEqualTo(box.Center));
        }

        [Fact]
        public void BoundingBoxOfARotatedBoxIsLargerThanTheBoxItself()
        {
            GeoObb3 rotated = new GeoObb3(
                GeoPoint3.Origin, 10.0, 2.0, 2.0,
                new GeoVector3(1.0, 1.0, 0.0),
                new GeoVector3(-1.0, 1.0, 0.0));

            Assert.True(rotated.GetAabb().Volume > rotated.Volume);
        }

        [Fact]
        public void ABoxTurnsIntoASolidOfTheSameVolume()
        {
            GeoObb3 box = new GeoObb3(new GeoPoint3(1.0, 2.0, 3.0), 2.0, 4.0, 6.0);
            GeoSolid3 solid = box.ToSolid();

            Assert.Equal(6, solid.Faces.Count);
            Assert.Equal(box.Volume, solid.Volume, 6);
            Assert.Equal(box.SurfaceArea, solid.SurfaceArea, 6);
            Assert.True(solid.IsClosed());
        }

        [Fact]
        public void ARotatedBoxAlsoTurnsIntoASolidOfTheSameVolume()
        {
            GeoObb3 box = new GeoObb3(
                new GeoPoint3(7.0, -3.0, 2.0), 10.0, 4.0, 2.0,
                new GeoVector3(1.0, 1.0, 1.0),
                new GeoVector3(0.0, 1.0, 0.0));

            Assert.Equal(box.Volume, box.ToSolid().Volume, 6);
            Assert.True(box.ToSolid().IsClosed());
        }

        [Fact]
        public void ADegenerateBoxHasNoFacesToBuild()
        {
            GeoObb3 flat = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 0.0);

            Assert.True(flat.IsDegenerate());
            Assert.Throws<InvalidOperationException>(() => flat.GetFaces());
        }

        [Fact]
        public void OverlappingBoxesCollideAndSeparatedOnesDoNot()
        {
            GeoObb3 first = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);

            Assert.True(first.CollidesWith(new GeoObb3(new GeoPoint3(5.0, 0.0, 0.0), 10.0, 10.0, 10.0)));
            Assert.True(first.CollidesWith(new GeoObb3(new GeoPoint3(10.0, 0.0, 0.0), 10.0, 10.0, 10.0)));
            Assert.False(first.CollidesWith(new GeoObb3(new GeoPoint3(11.0, 0.0, 0.0), 10.0, 10.0, 10.0)));
        }

        [Fact]
        public void TheEdgeCaseTheSeparatingAxisTheoremNeedsItsCrossAxesFor()
        {
            // Two long thin boxes crossing at an angle, offset so that no face of either faces the gap.
            // Only the nine cross-product axes separate them, which is what this case exists to check.
            GeoObb3 alongX = new GeoObb3(GeoPoint3.Origin, 20.0, 1.0, 1.0);

            GeoObb3 diagonalClose = new GeoObb3(
                new GeoPoint3(0.0, 0.0, 1.2), 20.0, 1.0, 1.0,
                new GeoVector3(1.0, 1.0, 0.0),
                new GeoVector3(-1.0, 1.0, 0.0));

            GeoObb3 diagonalFar = new GeoObb3(
                new GeoPoint3(0.0, 0.0, 5.0), 20.0, 1.0, 1.0,
                new GeoVector3(1.0, 1.0, 0.0),
                new GeoVector3(-1.0, 1.0, 0.0));

            Assert.False(alongX.CollidesWith(diagonalClose));
            Assert.False(alongX.CollidesWith(diagonalFar));
            Assert.True(alongX.CollidesWith(new GeoObb3(
                GeoPoint3.Origin, 20.0, 1.0, 1.0,
                new GeoVector3(1.0, 1.0, 0.0),
                new GeoVector3(-1.0, 1.0, 0.0))));
        }

        [Fact]
        public void CollisionIsSymmetric()
        {
            GeoObb3 first = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);
            GeoObb3 second = new GeoObb3(new GeoPoint3(7.0, 3.0, 1.0), 6.0, 6.0, 6.0,
                new GeoVector3(1.0, 2.0, 0.0),
                new GeoVector3(-2.0, 1.0, 0.0));

            Assert.Equal(first.CollidesWith(second), second.CollidesWith(first));
        }

        [Fact]
        public void SegmentReachingIntoABoxCollidesWithIt()
        {
            GeoObb3 box = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);

            Assert.True(SolidGeometry.Core.Collision3.CollidesWith(
                new GeoLine3(new GeoPoint3(-20.0, 0.0, 0.0), new GeoPoint3(20.0, 0.0, 0.0)), box));
            Assert.True(SolidGeometry.Core.Collision3.CollidesWith(
                new GeoLine3(GeoPoint3.Origin, new GeoPoint3(1.0, 1.0, 1.0)), box));
            Assert.False(SolidGeometry.Core.Collision3.CollidesWith(
                new GeoLine3(new GeoPoint3(-20.0, 20.0, 0.0), new GeoPoint3(20.0, 20.0, 0.0)), box));
        }

        [Fact]
        public void SegmentThroughABoxEntersAndLeavesIt()
        {
            GeoObb3 box = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);
            GeoLine3 line = new GeoLine3(new GeoPoint3(-20.0, 0.0, 0.0), new GeoPoint3(20.0, 0.0, 0.0));

            GeoPoint3[] hits = SolidGeometry.Core.Intersection3.GetIntersections(line, box);

            Assert.Equal(2, hits.Length);
            Assert.True(hits[0].IsEqualTo(new GeoPoint3(-5.0, 0.0, 0.0)));
            Assert.True(hits[1].IsEqualTo(new GeoPoint3(5.0, 0.0, 0.0)));
        }

        [Fact]
        public void SegmentStartingInsideOnlyLeavesOnce()
        {
            GeoObb3 box = new GeoObb3(GeoPoint3.Origin, 10.0, 10.0, 10.0);
            GeoLine3 line = new GeoLine3(GeoPoint3.Origin, new GeoPoint3(20.0, 0.0, 0.0));

            GeoPoint3[] hits = SolidGeometry.Core.Intersection3.GetIntersections(line, box);

            Assert.Single(hits);
            Assert.True(hits[0].IsEqualTo(new GeoPoint3(5.0, 0.0, 0.0)));
        }

        [Fact]
        public void TransformingABoxMovesItAndKeepsItsVolumeUnderARigidMotion()
        {
            GeoObb3 box = new GeoObb3(GeoPoint3.Origin, 2.0, 4.0, 6.0);
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(10.0, 0.0, 0.0))
                .Multiply(GeoTransform3.RotationZ(Math.PI / 3.0));

            GeoObb3 moved = box.TransformBy(motion);

            Assert.Equal(box.Volume, moved.Volume, 9);
            Assert.True(moved.Center.IsEqualTo(new GeoPoint3(10.0, 0.0, 0.0)));
        }

        #endregion
    }
}
