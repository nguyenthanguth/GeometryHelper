using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Where two bodies lie against each other, face to face, when they share no volume.
    /// </summary>
    /// <remarks>
    /// Every area here is worked out by hand from the drawing: the footprint the two faces share, less any hole
    /// in the material under it.
    /// </remarks>
    public class SolidContactTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        [Fact]
        public void AColumnOnAFootingBearsOverItsWholeFootprint()
        {
            GeoSolid3 footing = Box(0, 0, 0, 200, 200, 50);
            GeoSolid3 column = Box(50, 50, 50, 150, 150, 500);

            Assert.True(footing.TryGetContact(column, out GeoFace3[] contact));

            Assert.Single(contact);
            Assert.Equal(100.0 * 100, contact[0].Area, 6);

            // It lies in the plane they share, on the footing's top face and facing the way that face does.
            Assert.All(contact[0].Boundary.Vertices, vertex => Assert.Equal(50.0, vertex.Z, 6));
            Assert.True(contact[0].Boundary.Normal.IsEqualTo(GeoVector3.ZAxis, Loose));

            // Asked from the column's side it is the same patch, facing the column's way.
            Assert.True(column.TryGetContact(footing, out GeoFace3[] fromColumn));
            Assert.Equal(100.0 * 100, fromColumn.Sum(patch => patch.Area), 6);
            Assert.True(fromColumn[0].Boundary.Normal.IsEqualTo(GeoVector3.ZAxis.Negate(), Loose));

            // Touching shares no volume: the one-body and the many-body answers both say so.
            Assert.False(footing.TryIntersect(column, out _));
            Assert.Empty(footing.Intersect(column));
            Assert.True(footing.CollidesWith(column));
        }

        [Fact]
        public void APartlyOverhangingBlockBearsOnlyWhereItSitsOnTheOther()
        {
            GeoSolid3 below = Box(0, 0, 0, 100, 100, 50);
            GeoSolid3 above = Box(60, 70, 50, 160, 170, 100);

            Assert.True(below.TryGetContact(above, out GeoFace3[] contact));
            Assert.Equal(40.0 * 30, contact.Sum(patch => patch.Area), 6);
        }

        [Fact]
        public void TouchingOnlyAlongAnEdgeOrAtACornerIsNoPatch()
        {
            GeoSolid3 block = Box(0, 0, 0, 100, 100, 100);

            Assert.False(block.TryGetContact(Box(100, 100, 0, 200, 200, 100), out GeoFace3[] edge));
            Assert.Empty(edge);
            Assert.False(block.TryGetContact(Box(100, 100, 100, 200, 200, 200), out _));

            // Both of those do touch, which CollidesWith reports.
            Assert.True(block.CollidesWith(Box(100, 100, 0, 200, 200, 100)));
        }

        [Fact]
        public void FacesFlushAndSideBySideAreNotInContact()
        {
            // Two blocks standing next to each other with a gap: their tops are in one plane and face the same
            // way, which is side by side and not back to back.
            GeoSolid3 left = Box(0, 0, 0, 100, 100, 50);
            GeoSolid3 right = Box(150, 0, 0, 250, 100, 50);

            Assert.False(left.TryGetContact(right, out _));

            // And bodies far apart are not asked anything at all.
            Assert.False(left.TryGetContact(Box(1000, 0, 0, 1100, 100, 50), out _));
        }

        [Fact]
        public void AHoleUnderTheColumnDoesNotBear()
        {
            // A base plate with a bolt hole right under the middle of the column.
            GeoSolid3 plate = Box(0, 0, 0, 200, 200, 20).WithOpenings(new[] { Box(90, 90, -1, 110, 110, 21) });
            GeoSolid3 column = Box(50, 50, 20, 150, 150, 500);

            Assert.True(plate.TryGetContact(column, out GeoFace3[] contact));

            // The footprint less the hole: the patch is a square frame.
            Assert.Equal(100.0 * 100 - 20.0 * 20, contact.Sum(patch => patch.Area), 6);
            Assert.Single(contact);
            Assert.Single(contact[0].Holes);
        }

        [Fact]
        public void AContactAcrossSeveralFacesComesBackAsOneRegion()
        {
            // The footing's top is made of two faces meeting under the column; the patches from each join.
            Assert.True(Box(0, 0, 0, 100, 200, 50).TryUnion(Box(100, 0, 0, 200, 200, 50), out GeoSolid3 footing));

            GeoSolid3 column = Box(50, 50, 50, 150, 150, 500);

            Assert.True(footing.TryGetContact(column, out GeoFace3[] contact));
            Assert.Single(contact);
            Assert.Equal(100.0 * 100, contact[0].Area, 6);
        }

        [Fact]
        public void EveryWayInTakesAToleranceAndNothingIsAskedOfNothing()
        {
            GeoSolid3 footing = Box(0, 0, 0, 200, 200, 50);
            GeoSolid3 column = Box(50, 50, 50, 150, 150, 500);

            Assert.Equal(
                footing.TryGetContact(column, out GeoFace3[] a),
                footing.TryGetContact(column, out GeoFace3[] b, Tolerance.Global));
            Assert.Equal(a.Length, b.Length);

            Assert.Throws<ArgumentNullException>(() => Boolean3.TryGetContact(null, column, out _));
            Assert.Throws<ArgumentNullException>(() => Boolean3.TryGetContact(footing, null, out _));
        }
    }
}
