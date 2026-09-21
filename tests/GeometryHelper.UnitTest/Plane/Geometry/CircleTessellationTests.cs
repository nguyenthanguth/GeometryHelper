using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Cutting a circle into straight pieces, the three ways a caller can ask for: by how far the pieces
    /// may stray from the circle, by how far apart the points may be, and by how many pieces there are.
    /// Both dimensions answer alike.
    /// </summary>
    public class CircleTessellationTests
    {
        private const double Radius = 1000.0;

        private static GeoCircle2 Flat() => new GeoCircle2(new GeoPoint2(120, -40), Radius);

        private static GeoCircle3 Tilted() => new GeoCircle3(
            new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1), Radius);

        private static double LargestSagitta(GeoCircle2 circle, GeoPolygon2 polygon)
        {
            double worst = 0.0;

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                GeoLine2 edge = polygon.GetEdgeAt(i);
                GeoPoint2 middle = new GeoPoint2((edge.StartPoint.X + edge.EndPoint.X) * 0.5,
                                                 (edge.StartPoint.Y + edge.EndPoint.Y) * 0.5);
                worst = Math.Max(worst, circle.Radius - circle.Center.DistanceTo(middle));
            }

            return worst;
        }

        [Fact]
        public void TheAutomaticCutIsAboutFiftyPiecesAndStaysWithinTwoTenthsOfAPercent()
        {
            GeoCircle2 circle = Flat();
            GeoPolygon2 polygon = circle.ToPolygon();

            Assert.InRange(polygon.VertexCount, 45, 55);
            Assert.True(LargestSagitta(circle, polygon) <= Radius * 0.002 + 1E-9);

            // Every vertex lies on the circle: the polygon is inscribed.
            Assert.All(polygon.Vertices, v => Assert.Equal(Radius, circle.Center.DistanceTo(v), 9));

            // Inscribed, so it encloses a little less than the circle does.
            Assert.True(polygon.Area < circle.Area);
            Assert.True(polygon.Area > circle.Area * 0.995);
        }

        [Theory]
        [InlineData(50.0)]
        [InlineData(5.0)]
        [InlineData(0.5)]
        public void ACutByChordToleranceKeepsEveryEdgeWithinIt(double chordTolerance)
        {
            GeoCircle2 circle = Flat();
            GeoPolygon2 polygon = circle.ToPolygonByChordTolerance(chordTolerance);

            Assert.True(LargestSagitta(circle, polygon) <= chordTolerance + 1E-9);

            // And no finer than it has to be: one piece fewer would break the promise.
            GeoPolygon2 coarser = circle.ToPolygon(polygon.VertexCount - 1);
            Assert.True(LargestSagitta(circle, coarser) > chordTolerance);
        }

        [Theory]
        [InlineData(100.0)]
        [InlineData(31.0)]
        public void ACutBySpacingPutsNoTwoPointsFurtherApartThanAsked(double spacing)
        {
            GeoCircle2 circle = Flat();
            GeoPolygon2 polygon = circle.ToPolygonBySpacing(spacing);

            double along = circle.Length / polygon.VertexCount;

            // "no two points further apart than asked, measured along the circumference"
            Assert.True(along <= spacing + 1E-9);

            // "the vertices are spread evenly and no short edge is left at the end"
            double[] edges = Enumerable.Range(0, polygon.EdgeCount).Select(i => polygon.GetEdgeAt(i).Length).ToArray();
            Assert.All(edges, e => Assert.Equal(edges[0], e, 9));

            // Asking for one piece fewer would put the points further apart than asked.
            Assert.True(circle.Length / (polygon.VertexCount - 1) > spacing);
        }

        [Fact]
        public void ACutByCountGivesExactlyThatManyEdges()
        {
            Assert.Equal(12, Flat().ToPolygon(12).VertexCount);
            Assert.Equal(3, Flat().ToPolygon(3).VertexCount);

            Assert.Throws<ArgumentOutOfRangeException>(() => Flat().ToPolygon(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Flat().ToPolygonBySpacing(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Flat().ToPolygonBySpacing(-5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Flat().ToPolygonByChordTolerance(-1.0));
        }

        [Fact]
        public void AChainRepeatsItsFirstPointToCloseTheLoop()
        {
            GeoPolyline2 chain = Flat().ToPolyline(12);

            Assert.Equal(13, chain.VertexCount);
            Assert.True(chain[0].IsEqualTo(chain[12]));

            // The chain and the polygon place the same points.
            GeoPolygon2 polygon = Flat().ToPolygon(12);
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                Assert.True(chain[i].IsEqualTo(polygon[i]));
            }

            Assert.Equal(Flat().ToPolygon().VertexCount + 1, Flat().ToPolyline().VertexCount);
            Assert.Equal(13, Flat().ToPolylineBySpacing(Flat().Length / 12.0).VertexCount);
            Assert.True(Flat().ToPolylineByChordTolerance(5.0).VertexCount > 3);
        }

        [Fact]
        public void AVeryFineToleranceAsksForALongLoopRatherThanAnEndlessOne()
        {
            // A tolerance far below what double precision can hold: capped rather than run away.
            GeoPolygon2 polygon = Flat().ToPolygonByChordTolerance(1E-15);

            Assert.InRange(polygon.VertexCount, 3, 4096);
        }

        [Fact]
        public void ACircleInSpaceIsCutTheSameWay()
        {
            GeoCircle3 circle = Tilted();

            GeoPolygon3 automatic = circle.ToPolygon();
            Assert.InRange(automatic.VertexCount, 45, 55);
            Assert.All(automatic.Vertices, v => Assert.Equal(Radius, circle.Center.DistanceTo(v), 6));

            // It stays in the plane of the circle.
            GeoPlane3 plane = circle.GetPlane();
            Assert.All(automatic.Vertices, v => Assert.Equal(0.0, plane.SignedDistanceTo(v), 6));

            Assert.Equal(12, circle.ToPolygon(12).VertexCount);
            Assert.Equal(13, circle.ToPolyline(12).VertexCount);
            Assert.True(circle.ToPolyline(12)[0].IsEqualTo(circle.ToPolyline(12)[12]));

            GeoPolygon3 spaced = circle.ToPolygonBySpacing(100.0);
            Assert.True(circle.Length / spaced.VertexCount <= 100.0 + 1E-9);

            // The automatic tolerance here is 0.2 % of 1000, so 2.0: a coarser one gives fewer pieces
            // and a finer one more.
            Assert.True(circle.ToPolygonByChordTolerance(5.0).VertexCount < automatic.VertexCount);
            Assert.True(circle.ToPolygonByChordTolerance(0.5).VertexCount > automatic.VertexCount);
        }

        [Fact]
        public void TheTwoDimensionsAgreeOnHowManyPiecesAGivenCircleNeeds()
        {
            var flat = new GeoCircle2(GeoPoint2.Origin, 250.0);
            var inSpace = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 250.0);

            Assert.Equal(flat.ToPolygon().VertexCount, inSpace.ToPolygon().VertexCount);
            Assert.Equal(flat.ToPolygonByChordTolerance(1.0).VertexCount, inSpace.ToPolygonByChordTolerance(1.0).VertexCount);
            Assert.Equal(flat.ToPolygonBySpacing(40.0).VertexCount, inSpace.ToPolygonBySpacing(40.0).VertexCount);
        }
    }
}
