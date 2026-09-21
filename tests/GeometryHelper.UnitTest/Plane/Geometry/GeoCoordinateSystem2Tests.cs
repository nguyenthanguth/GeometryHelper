using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The local coordinate system in the plane. It has to agree with the transformation that says the
    /// same thing, and it has to read backwards without inverting anything — that is the whole reason for
    /// it to exist beside <see cref="GeoTransform2"/>.
    /// </summary>
    public class GeoCoordinateSystem2Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoCoordinateSystem2 Placed() =>
            new GeoCoordinateSystem2(new GeoPoint2(1000, -400), new GeoVector2(3, 4));

        [Fact]
        public void TheAxesAreSquareToEachOtherAndOfUnitLength()
        {
            GeoCoordinateSystem2 frame = Placed();

            // The X axis is normalized whatever length it was given.
            Assert.Equal(1.0, frame.XAxis.Length, 12);
            Assert.Equal(1.0, frame.YAxis.Length, 12);
            Assert.True(frame.XAxis.IsEqualTo(new GeoVector2(0.6, 0.8), Tight));

            // The Y axis is a quarter turn counter-clockwise from the X axis.
            Assert.True(frame.YAxis.IsEqualTo(new GeoVector2(-0.8, 0.6), Tight));
            Assert.Equal(0.0, frame.XAxis.DotProduct(frame.YAxis), 12);

            Assert.Equal(Math.Atan2(0.8, 0.6), frame.AngleRad, 12);

            // Building it from the angle gives the same thing.
            Assert.True(new GeoCoordinateSystem2(frame.Origin, frame.AngleRad).IsEqualTo(frame));

            // The global one is the drawing's own.
            Assert.True(GeoCoordinateSystem2.Global.Origin.IsEqualTo(GeoPoint2.Origin, Tight));
            Assert.True(GeoCoordinateSystem2.Global.XAxis.IsEqualTo(GeoVector2.XAxis, Tight));
        }

        [Fact]
        public void ReadingAPointThereAndBackAgainGivesItUnchanged()
        {
            GeoCoordinateSystem2 frame = Placed();

            foreach (GeoPoint2 point in new[]
            {
                new GeoPoint2(0, 0), new GeoPoint2(1000, -400), new GeoPoint2(-37, 512), new GeoPoint2(1e5, 1e5)
            })
            {
                Assert.True(frame.ToGlobal(frame.ToLocal(point)).IsEqualTo(point, Tight));
                Assert.True(frame.ToLocal(frame.ToGlobal(point)).IsEqualTo(point, Tight));
            }

            // The origin of the frame is nought in its own coordinates.
            Assert.True(frame.ToLocal(frame.Origin).IsEqualTo(GeoPoint2.Origin, Tight));

            // A vector has no place, so only the axes turn it.
            GeoVector2 vector = new GeoVector2(10, 0);

            Assert.True(frame.ToGlobal(vector).IsEqualTo(frame.XAxis.Multiply(10.0), Tight));
            Assert.True(frame.ToLocal(frame.XAxis).IsEqualTo(GeoVector2.XAxis, Tight));
            Assert.True(frame.ToGlobal(frame.ToLocal(vector)).IsEqualTo(vector, Tight));

            // Lengths and angles survive, because a frame only turns and moves.
            Assert.Equal(vector.Length, frame.ToGlobal(vector).Length, 12);
        }

        [Fact]
        public void ItSaysTheSameThingAsTheTransformationThatSaysIt()
        {
            GeoCoordinateSystem2 frame = Placed();
            GeoTransform2 place = frame.ToTransform();

            Assert.True(place.IsEqualTo(GeoTransform2.FromCoordinateSystem(frame)));
            Assert.True(place.IsEqualTo(GeoTransform2.FromFrame(frame.Origin, frame.XAxis)));

            foreach (GeoPoint2 point in new[] { new GeoPoint2(0, 0), new GeoPoint2(50, -20), new GeoPoint2(-3, 7) })
            {
                // Placing geometry built about the origin.
                Assert.True(place.Transform(point).IsEqualTo(frame.ToGlobal(point), Tight));

                // And reading placed geometry back, which the frame does without inverting a matrix.
                Assert.True(place.Inverse().Transform(point).IsEqualTo(frame.ToLocal(point), Tight));
            }
        }

        [Fact]
        public void MovingAFrameMovesWhatItReads()
        {
            GeoCoordinateSystem2 frame = Placed();
            GeoTransform2 turn = GeoTransform2.Rotation(new GeoPoint2(12, 34), 0.7);

            GeoCoordinateSystem2 moved = frame.TransformBy(turn);

            Assert.True(moved.Origin.IsEqualTo(turn.Transform(frame.Origin), Tight));
            Assert.Equal(1.0, moved.XAxis.Length, 12);

            // A point keeps the same local coordinates when the frame and the point move together.
            var point = new GeoPoint2(77, -13);

            Assert.True(moved.ToLocal(turn.Transform(point)).IsEqualTo(frame.ToLocal(point), new Tolerance(1E-8, 1E-8)));

            Assert.Throws<ArgumentNullException>(() => frame.TransformBy(null));
            Assert.Throws<InvalidOperationException>(() => frame.TransformBy(GeoTransform2.Scaling(0.0)));
        }

        [Fact]
        public void ARectangleCarriesItsOwnFrameTheWayABoxInSpaceDoes()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(100, 50), 80, 40, Math.PI / 6.0);

            GeoCoordinateSystem2 frame = rectangle.CoordinateSystem;

            Assert.True(frame.Origin.IsEqualTo(rectangle.Center, Tight));
            Assert.Equal(rectangle.AngleRad, frame.AngleRad, 12);

            // The centre is nought, and a corner is half the size along each axis.
            Assert.True(frame.ToLocal(rectangle.Center).IsEqualTo(GeoPoint2.Origin, Tight));

            GeoPoint2 corner = frame.ToGlobal(new GeoPoint2(40, 20));
            GeoPoint2 back = frame.ToLocal(corner);

            Assert.True(back.IsEqualTo(new GeoPoint2(40, 20), Tight));
            Assert.Equal(Math.Sqrt(40.0 * 40.0 + 20.0 * 20.0), rectangle.Center.DistanceTo(corner), 9);

            // And a rectangle can be built from a frame, as a box in space is built from its own.
            var built = new GeoRectangle2(frame, 80, 40);

            Assert.True(built.Center.IsEqualTo(rectangle.Center, Tight));
            Assert.Equal(rectangle.AngleRad, built.AngleRad, 12);
            Assert.Equal(rectangle.Area, built.Area, 9);
        }

        [Fact]
        public void EqualityAndTheUsualShorthandsAreThere()
        {
            GeoCoordinateSystem2 frame = Placed();
            GeoCoordinateSystem2 same = Placed();

            Assert.True(frame == same);
            Assert.False(frame != same);
            Assert.Equal(frame, frame.Clone());
            Assert.Equal(frame.GetHashCode(), same.GetHashCode());
            Assert.True(frame.IsEqualTo(same, Tolerance.Global));
            Assert.False(frame.IsEqualTo(GeoCoordinateSystem2.Global));
            Assert.Contains("LCS", frame.ToString());

            Assert.Throws<ArgumentException>(() => new GeoCoordinateSystem2(GeoPoint2.Origin, new GeoVector2(0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoCoordinateSystem2(GeoPoint2.Origin, double.NaN));
        }

        [Fact]
        public void ALoopCanBeTurnedRoundTheOtherWay()
        {
            var square = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10));

            GeoPolygon2 other = square.Reverse();

            Assert.Equal(square.Area, other.Area, 12);
            Assert.Equal(-square.SignedArea, other.SignedArea, 12);
            Assert.NotEqual(square.IsClockwise, other.IsClockwise);
            Assert.True(other.Reverse().IsEqualTo(square));

            var inSpace = new GeoPolygon3(
                new GeoPoint3(0, 0, 5), new GeoPoint3(10, 0, 5), new GeoPoint3(10, 10, 5));

            GeoPolygon3 turned = inSpace.Reverse();

            Assert.Equal(inSpace.Area, turned.Area, 12);
            Assert.True(turned.Reverse().IsEqualTo(inSpace));

            // Turning a loop round swaps which way a pair of chamfer distances goes.
            Assert.True(square.Chamfer(3.0, 1.0).IsEqualTo(other.Chamfer(1.0, 3.0).Reverse()));
        }
    }
}
