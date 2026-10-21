using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An edge in space with a chord a few thousandths long reads as its arc, and a short arc moves under any turn.
    /// </summary>
    /// <remarks>
    /// The frame an edge is read in was built from the chord and the normal across it, each as long as the chord, and the
    /// plane they span was asked of the global vector tolerance: their cross product, the chord squared, was nothing under a
    /// chord of 0.00316, so an edge 0.003 long threw "The X and Y directions must not be parallel" from <c>ToArc</c> and
    /// <c>Length</c>, and so did a short arc moved under a turn rounded to a float, which is carried through its ends and
    /// its middle. The frame is read from rounding alone now, to the bit as it was for every longer chord.
    /// </remarks>
    public class EdgeFaultLN2ShortChordTests
    {
        [Theory]
        [InlineData(0.002)]
        [InlineData(0.003)]
        [InlineData(0.0032)]
        public void AShortEdge_ReadsAsItsArc(double chord)
        {
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(chord, 0, 0), 0.5, GeoVector3.ZAxis);

            GeoArc3 arc = edge.ToArc();

            // A bulge of 0.5 sweeps 4 atan 0.5 on a radius of 0.625 times the chord.
            Assert.Equal(0.625 * chord, arc.Radius, 12);
            Assert.Equal(0.625 * chord * 4.0 * Math.Atan(0.5), edge.Length, 12);
            Assert.Equal(0.0, arc.EndPoint.DistanceTo(edge.EndPoint), 12);
        }

        [Theory]
        [InlineData(1.0, 0.002)]
        [InlineData(0.01, 0.3)]
        [InlineData(10.0, 0.0003)]
        public void AShortArc_MovesUnderATurnRoundedToAFloat(double radius, double sweep)
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

            var rounded = new GeoTransform3(matrix);
            var arc = new GeoArc3(new GeoPoint3(10, 20, 30), new GeoVector3(0.2, -0.1, 1), radius, 0.3, 0.3 + sweep);

            GeoArc3 moved = arc.TransformBy(rounded);

            Assert.Equal(0.0, moved.StartPoint.DistanceTo(rounded.Transform(arc.StartPoint)), 9);
            Assert.Equal(0.0, moved.EndPoint.DistanceTo(rounded.Transform(arc.EndPoint)), 9);
        }
    }
}
