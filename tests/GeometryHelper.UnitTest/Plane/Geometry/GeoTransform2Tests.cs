using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The 2D transformation: that each factory does what it says, that combining and undoing them behave,
    /// and that every shape the plane half carries can be moved by one.
    /// </summary>
    public class GeoTransform2Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoPolygon2 Square() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(4, 2), new GeoPoint2(0, 2));

        [Fact]
        public void Identity_LeavesEverythingWhereItIs()
        {
            var point = new GeoPoint2(3, -7);

            Assert.True(GeoTransform2.Identity.Transform(point).IsEqualTo(point, Tight));
            Assert.Equal(1.0, GeoTransform2.Identity.GetDeterminant(), 12);
        }

        [Fact]
        public void Translation_MovesPointsAndLeavesVectorsAlone()
        {
            GeoTransform2 move = GeoTransform2.Translation(new GeoVector2(10, -3));

            Assert.True(move.Transform(new GeoPoint2(1, 1)).IsEqualTo(new GeoPoint2(11, -2), Tight));
            Assert.True(move.Transform(new GeoVector2(1, 1)).IsEqualTo(new GeoVector2(1, 1), Tight));
        }

        [Fact]
        public void Rotation_TurnsCounterClockwiseAboutTheOriginOrAPoint()
        {
            GeoTransform2 quarter = GeoTransform2.Rotation(Math.PI / 2.0);

            Assert.True(quarter.Transform(new GeoPoint2(1, 0)).IsEqualTo(new GeoPoint2(0, 1), Tight));

            // About a point: that point stays put.
            var center = new GeoPoint2(5, 5);
            GeoTransform2 about = GeoTransform2.Rotation(center, Math.PI / 2.0);

            Assert.True(about.Transform(center).IsEqualTo(center, Tight));
            Assert.True(about.Transform(new GeoPoint2(6, 5)).IsEqualTo(new GeoPoint2(5, 6), Tight));
        }

        [Fact]
        public void Scaling_StretchesUniformlyOrPerAxis()
        {
            Assert.True(GeoTransform2.Scaling(3.0).Transform(new GeoPoint2(2, 1)).IsEqualTo(new GeoPoint2(6, 3), Tight));
            Assert.True(GeoTransform2.Scaling(3.0, 0.5).Transform(new GeoPoint2(2, 4)).IsEqualTo(new GeoPoint2(6, 2), Tight));

            var center = new GeoPoint2(10, 10);
            Assert.True(GeoTransform2.Scaling(center, 2.0).Transform(center).IsEqualTo(center, Tight));

            // The determinant is the factor areas are multiplied by.
            Assert.Equal(9.0, GeoTransform2.Scaling(3.0).GetDeterminant(), 12);
            Assert.Equal(4.0 * 2.0 * 9.0, GeoTransform2.Scaling(3.0).Transform(Square()).Area, 9);
        }

        [Fact]
        public void Mirror_ReflectsAcrossALineAndTurnsTheWindingRound()
        {
            // Across the X axis.
            GeoTransform2 acrossX = GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 0)));
            Assert.True(acrossX.Transform(new GeoPoint2(3, 4)).IsEqualTo(new GeoPoint2(3, -4), Tight));

            // Across a line that is neither axis: the line itself stays put.
            var slanted = new GeoLine2(new GeoPoint2(1, 1), new GeoPoint2(5, 5));
            GeoTransform2 acrossSlant = GeoTransform2.Mirror(slanted);
            Assert.True(acrossSlant.Transform(new GeoPoint2(3, 3)).IsEqualTo(new GeoPoint2(3, 3), Tight));
            Assert.True(acrossSlant.Transform(new GeoPoint2(4, 2)).IsEqualTo(new GeoPoint2(2, 4), Tight));

            // "a polygon that ran counter-clockwise comes back clockwise"
            GeoPolygon2 square = Square();
            Assert.False(square.IsClockwise);
            Assert.True(acrossX.Transform(square).IsClockwise);
            Assert.Equal(square.Area, acrossX.Transform(square).Area, 9);
            Assert.Equal(-1.0, acrossX.GetDeterminant(), 12);
        }

        [Fact]
        public void Mirror_RefusesASegmentWithNoLength()
        {
            var point = new GeoPoint2(2, 2);

            Assert.Throws<InvalidOperationException>(() => GeoTransform2.Mirror(new GeoLine2(point, point)));
        }

        [Fact]
        public void FromFrame_PlacesGeometryBuiltAboutTheOrigin()
        {
            GeoTransform2 place = GeoTransform2.FromFrame(new GeoPoint2(100, 50), new GeoVector2(0, 2));

            // The origin lands on the frame origin, and the X axis runs along the named direction.
            Assert.True(place.Transform(GeoPoint2.Origin).IsEqualTo(new GeoPoint2(100, 50), Tight));
            Assert.True(place.Transform(new GeoPoint2(1, 0)).IsEqualTo(new GeoPoint2(100, 51), Tight));
            Assert.True(place.Transform(new GeoPoint2(0, 1)).IsEqualTo(new GeoPoint2(99, 50), Tight));

            // Its inverse reads a placed drawing back into local coordinates.
            Assert.True(place.Inverse().Transform(new GeoPoint2(100, 51)).IsEqualTo(new GeoPoint2(1, 0), Tight));
        }

        [Fact]
        public void Multiply_AppliesTheRightHandSideFirst()
        {
            GeoTransform2 move = GeoTransform2.Translation(new GeoVector2(10, 0));
            GeoTransform2 turn = GeoTransform2.Rotation(Math.PI / 2.0);

            // "apply b, then a": turn then move lands somewhere else than move then turn.
            Assert.True(move.Multiply(turn).Transform(new GeoPoint2(1, 0)).IsEqualTo(new GeoPoint2(10, 1), Tight));
            Assert.True(turn.Multiply(move).Transform(new GeoPoint2(1, 0)).IsEqualTo(new GeoPoint2(0, 11), Tight));

            // The operator is the same thing.
            Assert.True((move * turn).IsEqualTo(move.Multiply(turn)));
        }

        [Fact]
        public void Inverse_UndoesWhateverWasDone()
        {
            GeoTransform2 motion = GeoTransform2.Translation(new GeoVector2(12, -4))
                .Multiply(GeoTransform2.Rotation(0.7))
                .Multiply(GeoTransform2.Scaling(2.5));

            var point = new GeoPoint2(3, -2);

            Assert.True(motion.Inverse().Transform(motion.Transform(point)).IsEqualTo(point, Tight));
            Assert.True(motion.Multiply(motion.Inverse()).IsEqualTo(GeoTransform2.Identity, new Tolerance(1E-9, 1E-9)));
        }

        [Fact]
        public void Inverse_RefusesATransformationThatFlattensThePlane()
        {
            GeoTransform2 flat = GeoTransform2.Scaling(1.0, 0.0);

            Assert.False(flat.TryGetInverse(out GeoTransform2 inverse));
            Assert.True(inverse.IsEqualTo(GeoTransform2.Identity));
            Assert.Throws<InvalidOperationException>(() => flat.Inverse());
        }

        [Fact]
        public void Inverse_JudgesTheDeterminantAgainstTheSizeOfTheTransformation()
        {
            // Everything scaled by a thousandth: the determinant is 1e-6, and it is perfectly invertible.
            GeoTransform2 small = GeoTransform2.Scaling(0.001);

            Assert.True(small.TryGetInverse(out GeoTransform2 inverse));
            Assert.True(inverse.Transform(small.Transform(new GeoPoint2(7, 9))).IsEqualTo(new GeoPoint2(7, 9), Tight));
        }

        [Fact]
        public void EveryShapeOfThePlaneCanBeMoved()
        {
            GeoTransform2 motion = GeoTransform2.Translation(new GeoVector2(5, 5)).Multiply(GeoTransform2.Rotation(0.4));

            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(3, 4));
            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(3, 0), new GeoPoint2(3, 4));
            GeoPolygon2 square = Square();
            var face = new GeoFace2(square, new[] { new GeoPolygon2(new GeoPoint2(1, 0.5), new GeoPoint2(2, 0.5), new GeoPoint2(2, 1.5), new GeoPoint2(1, 1.5)) });
            var circle = new GeoCircle2(new GeoPoint2(2, 2), 3);
            var rectangle = new GeoRectangle2(new GeoPoint2(1, 1), 6, 4, 0.2);

            // A rigid motion leaves every measurement alone.
            Assert.Equal(line.Length, line.TransformBy(motion).Length, 9);
            Assert.Equal(chain.Length, chain.TransformBy(motion).Length, 9);
            Assert.Equal(square.Area, square.TransformBy(motion).Area, 9);
            Assert.Equal(face.Area, face.TransformBy(motion).Area, 9);
            Assert.Single(face.TransformBy(motion).Holes);
            Assert.Equal(circle.Radius, circle.TransformBy(motion).Radius, 9);
            Assert.Equal(rectangle.Area, rectangle.TransformBy(motion).Area, 9);
            Assert.True(new GeoPoint2(1, 2).TransformBy(motion).IsEqualTo(motion.Transform(new GeoPoint2(1, 2)), Tight));
            Assert.True(new GeoVector2(1, 2).TransformBy(motion).IsEqualTo(motion.Transform(new GeoVector2(1, 2)), Tight));

            // A sequence of points comes back in order.
            GeoPoint2[] moved = motion.Transform(square.Vertices).ToArray();
            Assert.Equal(4, moved.Length);
            Assert.True(moved[0].IsEqualTo(motion.Transform(square[0]), Tight));
        }

        [Fact]
        public void ACircleUnderAnUnequalScalingIsRefusedRatherThanAveraged()
        {
            var circle = new GeoCircle2(new GeoPoint2(1, 1), 2);

            Assert.Throws<InvalidOperationException>(() => GeoTransform2.Scaling(2.0, 3.0).Transform(circle));

            // Uniform scaling is fine, and the radius follows it.
            Assert.Equal(6.0, GeoTransform2.Scaling(3.0).Transform(circle).Radius, 9);
        }

        [Fact]
        public void ARectangleUnderAnUnequalScalingIsRefusedRatherThanSquashed()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(0, 0), 4, 2);

            Assert.Throws<InvalidOperationException>(() => GeoTransform2.Scaling(2.0, 3.0).Transform(rectangle));

            // The way out is to read it as a polygon, which any transformation can carry.
            GeoPolygon2 squashed = GeoTransform2.Scaling(2.0, 3.0).Transform(rectangle.ToPolygon());
            Assert.Equal(4 * 2 * 6.0, squashed.Area, 9);
        }

        [Fact]
        public void ARectangleKeepsItsShapeUnderAMoveATurnAndAMirror()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(3, 1), 6, 4, 0.3);

            GeoRectangle2 turned = rectangle.TransformBy(GeoTransform2.Rotation(0.5));
            Assert.Equal(rectangle.Width, turned.Width, 9);
            Assert.Equal(rectangle.Height, turned.Height, 9);
            Assert.Equal(0.8, turned.AngleRad, 9);

            GeoRectangle2 mirrored = rectangle.TransformBy(GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 0))));
            Assert.Equal(rectangle.Width, mirrored.Width, 9);
            Assert.Equal(-0.3, mirrored.AngleRad, 9);

            GeoRectangle2 scaled = rectangle.TransformBy(GeoTransform2.Scaling(2.0));
            Assert.Equal(12.0, scaled.Width, 9);
            Assert.Equal(8.0, scaled.Height, 9);
        }

        [Fact]
        public void TheMatrixIsReadableAndCopiedOnTheWayIn()
        {
            double[,] matrix = { { 2.0, 0.0, 5.0 }, { 0.0, 2.0, -1.0 }, { 0.0, 0.0, 1.0 } };
            var transform = new GeoTransform2(matrix);

            Assert.Equal(2.0, transform[0, 0], 12);
            Assert.Equal(5.0, transform[0, 2], 12);
            Assert.Throws<ArgumentOutOfRangeException>(() => transform[3, 0]);
            Assert.Throws<ArgumentException>(() => new GeoTransform2(new double[2, 2]));
            Assert.Throws<ArgumentNullException>(() => new GeoTransform2(null));

            // Changing the array afterwards does not change the transformation.
            matrix[0, 0] = 99.0;
            Assert.Equal(2.0, transform[0, 0], 12);

            Assert.True(transform.Clone().IsEqualTo(transform));
            Assert.Equal(transform, transform.Clone());
            Assert.Equal(transform.GetHashCode(), transform.Clone().GetHashCode());
            Assert.False(transform.Equals(null));
            Assert.Contains("GeoTransform2", transform.ToString());
        }

        [Fact]
        public void ATransformationAgreesWithTheMoveItStandsFor()
        {
            GeoPolygon2 square = Square();
            var by = new GeoVector2(7, -2);

            Assert.True(square.Translate(by).IsEqualTo(square.TransformBy(GeoTransform2.Translation(by))));

            GeoPolygon2 turnedTheOldWay = square.RotateBy(0.6, new GeoPoint2(1, 1));
            GeoPolygon2 turnedTheNewWay = square.TransformBy(GeoTransform2.Rotation(new GeoPoint2(1, 1), 0.6));

            for (int i = 0; i < square.VertexCount; i++)
            {
                Assert.True(turnedTheOldWay[i].IsEqualTo(turnedTheNewWay[i], Tight));
            }
        }
    }
}
