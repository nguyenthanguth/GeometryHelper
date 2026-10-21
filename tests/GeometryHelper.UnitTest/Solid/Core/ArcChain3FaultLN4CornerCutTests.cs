using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A curved chain cut within the point tolerance of one of its corners breaks at the corner, between the two edges, and
    /// leaves no piece shorter than the tolerance.
    /// </summary>
    /// <remarks>
    /// The walk broke the chain at a corner the cut stood within the tolerance of, as its remarks say, and cut the edge there as
    /// well: a piece shorter than the tolerance was left between the two cuts, and where the cut stood on the corner to rounding
    /// the piece was too short to have a plane, and splitting the chain threw (28 of 12 000 chains cut by boxes, faces and bodies).
    /// Such a cut breaks the chain at the corner alone now.
    /// </remarks>
    public class ArcChain3FaultLN4CornerCutTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoAabb3 Box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10));

        [Fact]
        public void ACutWithinTheToleranceOfACorner_BreaksTheChainAtTheCorner()
        {
            // The corner stands 0.0004 over the top of the box, where the first edge leaves it.
            var corner = new GeoPoint3(5, 5, 10.0004);
            var chain = new GeoPolylineArc3(new[]
            {
                new GeoEdge3(new GeoPoint3(5, 5, 5), corner, 0.3, GeoVector3.XAxis),
                new GeoEdge3(corner, new GeoPoint3(5, 9, 14), -0.2, GeoVector3.XAxis),
            });

            Assert.True(chain.TrySplitBy(Box, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside, Default));

            GeoPolylineArc3[] pieces = inside.Concat(outside).ToArray();

            Assert.Equal(2, pieces.Length);
            Assert.All(pieces, piece => Assert.All(piece.GetEdges(), edge => Assert.True(edge.Length > Default.EqualPoint)));
            Assert.Equal(corner, Assert.Single(inside).EndPoint);
            Assert.Equal(corner, Assert.Single(outside).StartPoint);
        }

        [Fact]
        public void ACutOnACornerToRounding_NoLongerThrows()
        {
            // A chain the face z = 10 meets at its second corner to the last few digits: the arc after it was cut a hair from
            // its start, and the piece left had no plane.
            var first = new GeoPoint3(9.999818083745808, 0.002020889530433756, 7.758680716044587);
            var second = new GeoPoint3(0.001318125284425041, 2.1902432768560214, 9.999938192374044);
            var third = new GeoPoint3(10.001042654410956, 10.001131652528482, 0.0010239292224980558);
            var face = new GeoFace3(
                new GeoPolygon3(new[] { new GeoPoint3(0, 0, 10), new GeoPoint3(10, 0, 10), new GeoPoint3(10, 10, 10), new GeoPoint3(0, 10, 10) }, Default),
                new[] { new GeoPolygon3(new[] { new GeoPoint3(3, 3, 10), new GeoPoint3(3, 7, 10), new GeoPoint3(7, 7, 10), new GeoPoint3(7, 3, 10) }, Default) },
                Default);
            var chain = new GeoPolylineArc3(new[]
            {
                new GeoEdge3(first, second),
                new GeoEdge3(second, third, 0.09623384815465369, second.GetVectorTo(third).CrossProduct(new GeoVector3(0.3, -0.5, 0.7))),
            });

            Assert.True(chain.TrySplitBy(face, out GeoPolylineArc3[] pieces, Default));
            Assert.Equal(2, pieces.Length);
            Assert.Equal(second, pieces[0].EndPoint);
        }
    }
}
