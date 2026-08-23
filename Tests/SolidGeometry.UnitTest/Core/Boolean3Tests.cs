using System;
using System.Collections.Generic;
using CommonGeometry.Enums;
using SolidGeometry;
using SolidGeometry.Core;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers combining solids. The invariant that matters most here is volume: whatever the boundary
    /// comes out looking like, the measured volume has to match what set arithmetic says it should be.
    /// </summary>
    public class Boolean3Tests
    {
        private static GeoSolid3 Box(GeoPoint3 min, GeoPoint3 max) =>
            new GeoAabb3(min, max).ToObb().ToSolid();

        /// <summary>
        /// A 10 cube at the origin, volume 1000.
        /// </summary>
        private static GeoSolid3 Cube() => Box(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));

        /// <summary>
        /// A second 10 cube offset by 5 on every axis, so the two share a 5 cube of volume 125.
        /// </summary>
        private static GeoSolid3 Offset() => Box(new GeoPoint3(5, 5, 5), new GeoPoint3(15, 15, 15));

        #region Intersect

        [Fact]
        public void TheSharedPartOfTwoOverlappingCubesIsTheCornerTheyBothCover()
        {
            Assert.True(Cube().TryIntersect(Offset(), out GeoSolid3 shared));

            Assert.Equal(125.0, shared.Volume, 4);
            Assert.True(shared.IsClosed());
            Assert.Equal(6, shared.Faces.Count);
        }

        [Fact]
        public void BodiesThatDoNotTouchShareNothing()
        {
            Assert.False(Cube().TryIntersect(Box(new GeoPoint3(50, 50, 50), new GeoPoint3(60, 60, 60)), out _));
        }

        [Fact]
        public void TheSharedPartOfABodyWithItselfIsTheWholeBody()
        {
            GeoSolid3 cube = Cube();

            Assert.True(cube.TryIntersect(Box(new GeoPoint3(-5, -5, -5), new GeoPoint3(15, 15, 15)), out GeoSolid3 shared));

            Assert.Equal(cube.Volume, shared.Volume, 4);
        }

        [Fact]
        public void IntersectionIsSymmetric()
        {
            Assert.True(Cube().TryIntersect(Offset(), out GeoSolid3 first));
            Assert.True(Offset().TryIntersect(Cube(), out GeoSolid3 second));

            Assert.Equal(first.Volume, second.Volume, 4);
        }

        #endregion

        #region Subtract

        [Fact]
        public void TakingOneCubeOutOfAnotherLeavesTheRest()
        {
            Assert.True(Cube().TrySubtract(Offset(), out GeoSolid3 left));

            Assert.Equal(1000.0 - 125.0, left.Volume, 4);
            Assert.True(left.IsClosed());
        }

        [Fact]
        public void TakingAToolOutLeavesAPointInTheRemovedPartOutside()
        {
            Assert.True(Cube().TrySubtract(Offset(), out GeoSolid3 left));

            // In the corner that was removed.
            Assert.Equal(PointLocation.OutSide, left.Locate(new GeoPoint3(8, 8, 8)));

            // In the part that survived.
            Assert.Equal(PointLocation.Inside, left.Locate(new GeoPoint3(2, 2, 2)));
        }

        [Fact]
        public void ATunnelRightThroughABodyLeavesTheExpectedVolume()
        {
            // A 4 by 4 bar driven all the way through the cube along Z.
            GeoSolid3 bar = Box(new GeoPoint3(3, 3, -5), new GeoPoint3(7, 7, 15));

            Assert.True(Cube().TrySubtract(bar, out GeoSolid3 pierced));

            Assert.Equal(1000.0 - 4.0 * 4.0 * 10.0, pierced.Volume, 4);
            Assert.True(pierced.IsClosed());

            Assert.Equal(PointLocation.OutSide, pierced.Locate(new GeoPoint3(5, 5, 5)));
            Assert.Equal(PointLocation.Inside, pierced.Locate(new GeoPoint3(1, 1, 5)));
        }

        [Fact]
        public void APocketThatDoesNotBreakThroughLeavesACavity()
        {
            // Open at the top, stopping short of the bottom.
            GeoSolid3 pocket = Box(new GeoPoint3(3, 3, 4), new GeoPoint3(7, 7, 15));

            Assert.True(Cube().TrySubtract(pocket, out GeoSolid3 hollowed));

            Assert.Equal(1000.0 - 4.0 * 4.0 * 6.0, hollowed.Volume, 4);
            Assert.True(hollowed.IsClosed());

            Assert.Equal(PointLocation.OutSide, hollowed.Locate(new GeoPoint3(5, 5, 8)));
            Assert.Equal(PointLocation.Inside, hollowed.Locate(new GeoPoint3(5, 5, 2)));
        }

        [Fact]
        public void ATooolThatMissesChangesNothing()
        {
            GeoSolid3 cube = Cube();

            Assert.True(cube.TrySubtract(Box(new GeoPoint3(50, 50, 50), new GeoPoint3(60, 60, 60)), out GeoSolid3 left));

            Assert.Equal(cube.Volume, left.Volume, 6);
        }

        [Fact]
        public void ATooolThatSwallowsTheSubjectLeavesNothing()
        {
            Assert.False(Cube().TrySubtract(Box(new GeoPoint3(-5, -5, -5), new GeoPoint3(15, 15, 15)), out _));
        }

        #endregion

        #region Union

        [Fact]
        public void JoiningTwoOverlappingCubesCountsTheSharedPartOnce()
        {
            Assert.True(Cube().TryUnion(Offset(), out GeoSolid3 joined));

            Assert.Equal(1000.0 + 1000.0 - 125.0, joined.Volume, 4);
            Assert.True(joined.IsClosed());
        }

        [Fact]
        public void JoiningIsSymmetric()
        {
            Assert.True(Cube().TryUnion(Offset(), out GeoSolid3 first));
            Assert.True(Offset().TryUnion(Cube(), out GeoSolid3 second));

            Assert.Equal(first.Volume, second.Volume, 4);
        }

        [Fact]
        public void JoiningBodiesThatDoNotTouchGivesOneSolidCarryingBothShells()
        {
            GeoSolid3 apart = Box(new GeoPoint3(50, 50, 50), new GeoPoint3(60, 60, 60));

            Assert.True(Cube().TryUnion(apart, out GeoSolid3 joined));

            Assert.Equal(2000.0, joined.Volume, 4);
            Assert.True(joined.IsClosed());
            Assert.Equal(PointLocation.Inside, joined.Locate(new GeoPoint3(5, 5, 5)));
            Assert.Equal(PointLocation.Inside, joined.Locate(new GeoPoint3(55, 55, 55)));
            Assert.Equal(PointLocation.OutSide, joined.Locate(new GeoPoint3(30, 30, 30)));
        }

        [Fact]
        public void JoiningTwoHalvesBackTogetherRebuildsTheWhole()
        {
            GeoSolid3 lower = Box(GeoPoint3.Origin, new GeoPoint3(10, 10, 5));
            GeoSolid3 upper = Box(new GeoPoint3(0, 0, 5), new GeoPoint3(10, 10, 10));

            Assert.True(lower.TryUnion(upper, out GeoSolid3 whole));

            Assert.Equal(1000.0, whole.Volume, 4);
            Assert.Equal(6, whole.Faces.Count);
            Assert.True(whole.IsClosed());
        }

        #endregion

        #region The three together

        [Fact]
        public void UnionAndIntersectionAccountForEverything()
        {
            // |A| + |B| = |A union B| + |A intersect B|, whatever shape the two are.
            GeoSolid3 first = Cube();
            GeoSolid3 second = Offset();

            Assert.True(first.TryUnion(second, out GeoSolid3 joined));
            Assert.True(first.TryIntersect(second, out GeoSolid3 shared));

            Assert.Equal(first.Volume + second.Volume, joined.Volume + shared.Volume, 4);
        }

        [Fact]
        public void SubtractingBothWaysAndTheSharedPartRebuildTheUnion()
        {
            GeoSolid3 first = Cube();
            GeoSolid3 second = Offset();

            Assert.True(first.TrySubtract(second, out GeoSolid3 onlyFirst));
            Assert.True(second.TrySubtract(first, out GeoSolid3 onlySecond));
            Assert.True(first.TryIntersect(second, out GeoSolid3 shared));
            Assert.True(first.TryUnion(second, out GeoSolid3 joined));

            Assert.Equal(joined.Volume, onlyFirst.Volume + onlySecond.Volume + shared.Volume, 4);
        }

        [Fact]
        public void ARotatedToolCutsJustAsWell()
        {
            GeoObb3 turned = new GeoObb3(
                new GeoPoint3(5, 5, 5), 4, 4, 40,
                new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0));

            Assert.True(Cube().TrySubtract(turned.ToSolid(), out GeoSolid3 pierced));

            Assert.True(pierced.IsClosed());
            Assert.True(pierced.Volume < 1000.0);
            Assert.True(pierced.Volume > 0.0);
            Assert.Equal(PointLocation.OutSide, pierced.Locate(new GeoPoint3(5, 5, 5)));
        }

        [Fact]
        public void AConcaveSubjectSurvivesTheOperation()
        {
            GeoSolid3 lShape = MakePrism(new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 4, 0),
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(4, 10, 0),
                new GeoPoint3(0, 10, 0)
            }, 6.0);

            // A bar through the foot of the L.
            GeoSolid3 bar = Box(new GeoPoint3(6, 1, -5), new GeoPoint3(8, 3, 11));

            Assert.Equal(384.0, lShape.Volume, 4);
            Assert.True(lShape.TrySubtract(bar, out GeoSolid3 pierced));

            Assert.Equal(384.0 - 2.0 * 2.0 * 6.0, pierced.Volume, 4);
            Assert.True(pierced.IsClosed());
        }

        [Fact]
        public void NullArgumentsAreRejected()
        {
            GeoSolid3 cube = Cube();

            Assert.Throws<ArgumentNullException>(() => Boolean3.TryUnion(null, cube, out _));
            Assert.Throws<ArgumentNullException>(() => Boolean3.TryIntersect(cube, null, out _));
            Assert.Throws<ArgumentNullException>(() => Boolean3.TrySubtract(null, cube, out _));
        }

        #endregion

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
    }
}
