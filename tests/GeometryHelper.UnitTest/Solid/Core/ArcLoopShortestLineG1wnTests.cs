using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The shortest line from a curved loop in space to a chain, a polygon, a face or another curved loop in its plane is
    /// measured to every side the other shape has, however short.
    /// </summary>
    /// <remarks>
    /// The other shape was laid out in the loop's plane within the global tolerance of a thousandth, so a side shorter than
    /// that, which the shape keeps when built within a finer one, was dropped and the line ran to the corner kept before
    /// it. The square 10 by 10 at the origin is 0.5 from each shape below, whose nearest corner (10.5, 5) sits 0.0004 from
    /// the corner (10.5004, 5); it read 0.5004. No test sets the global tolerance.
    /// </remarks>
    public class ArcLoopShortestLineG1wnTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);
        private static readonly Tolerance Fine = new Tolerance(1E-11, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-11);

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static GeoPolygonArc3 Square()
            => new GeoPolygonArc3(new[] { P(0, 0), P(10, 0), P(10, 10), P(0, 10) }, new[] { 0.0, 0.0, 0.0, 0.0 }, new[] { GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis }, Fine);

        private static GeoPoint3[] Corners() => new[] { P(10.5004, 5), P(16, 2), P(16, 8), P(10.5, 5) };

        [Fact]
        public void APolygonWithItsNearestCornerOnASideShorterThanTheGlobalTolerance_IsMeasuredToThatCorner()
        {
            GeoLine3 line = Square().GetShortestLineTo(new GeoPolygon3(Corners(), Fine), Millionth);

            Assert.Equal(0.5, line.Length, 12);
            Assert.True(line.EndPoint.IsEqualTo(P(10.5, 5), Millionth));
        }

        [Fact]
        public void AChainWithItsNearestCornerOnALegShorterThanTheGlobalTolerance_IsMeasuredToThatCorner()
        {
            var corners = new[] { P(16, 2), P(10.5004, 5), P(10.5, 5) };

            GeoLine3 line = Square().GetShortestLineTo(new GeoPolyline3(corners, true), Millionth);

            Assert.Equal(0.5, line.Length, 12);
        }

        [Fact]
        public void AFaceWithItsNearestCornerOnASideShorterThanTheGlobalTolerance_IsMeasuredToThatCorner()
        {
            var hole = new GeoPolygon3(new[] { P(13, 4.5), P(13, 5.5), P(14, 5.5), P(14, 4.5) }, Fine);
            var face = new GeoFace3(new GeoPolygon3(Corners(), Fine), new[] { hole }, Fine);

            GeoLine3 line = Square().GetShortestLineTo(face, Millionth);

            Assert.Equal(0.5, line.Length, 12);
        }

        [Fact]
        public void ACurvedLoopWithItsNearestCornerOnASideShorterThanTheGlobalTolerance_IsMeasuredToThatCorner()
        {
            var other = new GeoPolygonArc3(Corners(), new[] { 0.0, 0.0, 0.0, 0.0 }, new[] { GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis }, Fine);

            GeoLine3 line = Square().GetShortestLineTo(other, Millionth);

            Assert.Equal(4, other.VertexCount);
            Assert.Equal(0.5, line.Length, 12);
        }
    }
}
