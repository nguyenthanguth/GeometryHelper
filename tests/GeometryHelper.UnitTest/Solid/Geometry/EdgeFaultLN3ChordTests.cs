using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An edge in space is refused a chord only where its two ends are one, as an edge in the plane is.
    /// </summary>
    /// <remarks>
    /// The constructor read the length of the chord, a length, against the vector tolerance, a number with no size, and so
    /// refused any chord of 1E-5 or less: an edge 1E-6 long could not be built, a piece split off an arc a hair from its end
    /// threw "Two points in the same place have no chord to bulge", and so did a short arc moved under a turn rounded to a
    /// float, which is carried through its ends and its middle. Moving such an edge read the side it bows to against the
    /// same tolerance. A chord is refused now only where it has no length at all, and every edge built before is the same to
    /// the bit.
    /// </remarks>
    public class EdgeFaultLN3ChordTests
    {
        private static readonly GeoVector3 Along = new GeoVector3(0.6, 0.8, 0);

        private static GeoTransform3 TurnRoundedToAFloat()
        {
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoVector3(0.3, 0.7, -0.2), 0.7);
            var matrix = new double[4, 4];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    matrix[i, j] = (float)turn[i, j];
                }
            }

            return new GeoTransform3(matrix);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(1000.0)]
        public void AnEdge1EMinus6Long_IsAnArc(double offset)
        {
            var start = new GeoPoint3(offset, 2 * offset, 3 * offset);
            var edge = new GeoEdge3(start, start.Add(Along.Multiply(1E-6)), 0.5, GeoVector3.ZAxis);

            GeoArc3 arc = edge.ToArc();

            // A bulge of 0.5 sweeps 4 atan 0.5 on a radius of 0.625 times the chord.
            Assert.Equal(0.625E-6, arc.Radius, 12);
            Assert.Equal(0.625E-6 * 4.0 * Math.Atan(0.5), edge.Length, 12);
            Assert.Equal(0.0, arc.EndPoint.DistanceTo(edge.EndPoint), 12);
        }

        [Fact]
        public void AnEdgeWhoseEndsAreOne_IsStillRefused()
        {
            var at = new GeoPoint3(1, 2, 3);

            Assert.Throws<ArgumentException>(() => new GeoEdge3(at, at, 0.5, GeoVector3.ZAxis));
        }

        [Fact]
        public void AnEdge1EMinus6Long_MovesUnderATurnRoundedToAFloat()
        {
            GeoTransform3 turn = TurnRoundedToAFloat();
            var start = new GeoPoint3(10, 20, 30);
            var edge = new GeoEdge3(start, start.Add(Along.Multiply(1E-6)), 0.5, GeoVector3.ZAxis);

            GeoEdge3 moved = edge.TransformBy(turn);

            Assert.Equal(turn.Transform(edge.EndPoint), moved.EndPoint);
            Assert.Equal(0.5, moved.Bulge, 6);
        }

        [Fact]
        public void AnArcWithAChordOf3EMinus6_MovesUnderATurnRoundedToAFloat()
        {
            GeoTransform3 turn = TurnRoundedToAFloat();
            var arc = new GeoArc3(new GeoPoint3(10, 20, 30), new GeoVector3(0.2, -0.1, 1), 1E-5, 0.3, 0.6);

            GeoArc3 moved = arc.TransformBy(turn);

            Assert.Equal(0.0, moved.StartPoint.DistanceTo(turn.Transform(arc.StartPoint)), 12);
            Assert.Equal(0.0, moved.EndPoint.DistanceTo(turn.Transform(arc.EndPoint)), 12);
        }

        [Fact]
        public void AnArcWithAChordOf1EMinus6_IsAnEdge()
        {
            var tiny = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(1E-6, 0, 0), 0.5, GeoVector3.ZAxis);

            var again = new GeoEdge3(tiny.ToArc());

            Assert.Equal(tiny.Length, again.Length, 15);
            Assert.Equal(0.0, again.EndPoint.DistanceTo(tiny.EndPoint), 15);
        }

        [Fact]
        public void AnEdge1EMinus6Long_IsExtended()
        {
            var tiny = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(1E-6, 0, 0), 0.5, GeoVector3.ZAxis);

            GeoEdge3 longer = tiny.Extend(1E-6, GeometryHelper.Enums.LineEnd.End, Tolerance.Default);

            Assert.Equal(tiny.Length + 1E-6, longer.Length, 12);
        }

        [Fact]
        public void AnArcOfAWholeTurn_IsStillNoEdge()
        {
            var whole = new GeoArc3(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 1E-6, 0.0, 2 * Math.PI);

            Assert.Throws<ArgumentException>(() => new GeoEdge3(whole));
        }

        [Fact]
        public void APieceSplitOffAHairFromTheEnd_IsAnArc()
        {
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 0.5, GeoVector3.ZAxis);

            Assert.True(edge.TrySplitAtParameter(1.0 - 1E-8, out GeoEdge3 first, out GeoEdge3 second));
            Assert.Equal(edge.EndPoint, second.EndPoint);
            Assert.Equal(first.EndPoint, second.StartPoint);
            Assert.True(second.IsArc);
        }
    }
}
