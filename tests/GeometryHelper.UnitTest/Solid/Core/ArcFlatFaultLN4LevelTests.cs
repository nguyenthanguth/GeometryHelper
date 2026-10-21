using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc or a segment lies level over a shape, and is given no touch, only where it rises off its plane by no more than
    /// the planar tolerance: a length, not an angle.
    /// </summary>
    /// <remarks>
    /// An arc was read as lying level where its normal stood within the vector tolerance of the plane's, and a segment beside
    /// a box where it ran with the face to that angle, so a long one tilted less than that, rising far more than the tolerance
    /// along it, was given nothing where it dipped within the tolerance of the shape and collided: the arc of radius 300 tilted
    /// 9E-6, which rises 0.0027 round its circle. Both read the rise now, an arc as <c>IsCoplanar</c> does.
    /// </remarks>
    public class ArcFlatFaultLN4LevelTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoPolygon3 Square = new GeoPolygon3(
            new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0), new GeoPoint3(1000, 1000, 0), new GeoPoint3(0, 1000, 0) }, Default);

        private static readonly GeoObb3 Slab = new GeoObb3(new GeoPoint3(500, 500, -5), 1000, 1000, 10);

        [Fact]
        public void AWideArcTiltedLessThanTheVectorTolerance_TouchesWhereItDipsNearest()
        {
            double tilt = 9E-6;
            var arc = new GeoArc3(new GeoPoint3(400, 400, 0.0006 + 300 * Math.Sin(tilt)), new GeoVector3(0, Math.Sin(tilt), Math.Cos(tilt)), 300, 0, 6);

            Assert.True(Arc3.CollidesWith(arc, Square, Default));
            Assert.True(Assert.Single(Arc3.GetIntersections(arc, Square, Default)).DistanceTo(new GeoPoint3(400, 700, 0.0006)) < 1E-6);
            Assert.True(Assert.Single(Arc3.GetIntersections(arc, new GeoFace3(Square), Default)).DistanceTo(new GeoPoint3(400, 700, 0.0006)) < 1E-6);
            Assert.True(Assert.Single(Arc3.GetIntersections(arc, Slab, Default)).DistanceTo(new GeoPoint3(400, 700, 0.0006)) < 1E-6);
        }

        [Fact]
        public void AnArcLevelOverTheMaterial_IsStillGivenNone()
        {
            var arc = new GeoArc3(new GeoPoint3(400, 400, 0.0006), GeoVector3.ZAxis, 100, 0, 2);

            Assert.True(Arc3.CollidesWith(arc, Square, Default));
            Assert.Empty(Arc3.GetIntersections(arc, Square, Default));
            Assert.Empty(Arc3.GetIntersections(arc, Slab, Default));
        }

        [Fact]
        public void ALongSegmentTiltedLessThanTheVectorTolerance_TouchesTheBoxAtItsLowEnd()
        {
            // 800 long, rising 0.0072 from 0.0006 over the top of the slab.
            var segment = new GeoLine3(new GeoPoint3(100, 500, 0.0006), new GeoPoint3(900, 500, 0.0006 + 800 * 9E-6));

            Assert.True(Slab.CollidesWith(segment, Default));
            Assert.Equal(segment.StartPoint, Assert.Single(Slab.GetIntersections(segment, Default)));
        }

        [Fact]
        public void ASegmentRisingLessThanTheTolerance_StillLiesAlongTheBox()
        {
            var segment = new GeoLine3(new GeoPoint3(100, 500, 0.0003), new GeoPoint3(900, 500, 0.0009));

            Assert.True(Slab.CollidesWith(segment, Default));
            Assert.Empty(Slab.GetIntersections(segment, Default));
        }
    }
}
