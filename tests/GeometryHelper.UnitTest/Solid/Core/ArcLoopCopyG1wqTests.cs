using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A curved loop in space is copied, moved and transformed edge for edge, every arc and side however small.
    /// </summary>
    /// <remarks>
    /// The copies were put back together within the global tolerance of a thousandth, which dropped the arcs and sides a
    /// loop keeps within a finer one. No test sets the global tolerance.
    /// </remarks>
    public class ArcLoopCopyG1wqTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);
        private static readonly Tolerance Tight = new Tolerance(1E-12, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-12);

        // The rectangle 100 by 50 standing in the plane x = 20, every corner rounded by 0.0005 at a millionth: eight edges,
        // four of them arcs 0.0007 across.
        private static GeoPolygonArc3 Rounded()
            => new GeoPolygonArc3(
                new[] { new GeoPoint3(20, 0, 0), new GeoPoint3(20, 100, 0), new GeoPoint3(20, 100, 50), new GeoPoint3(20, 0, 50) },
                null,
                null,
                Tight).Fillet(0.0005, Millionth);

        private static readonly double RoundedArea = 5000.0 - 0.0005 * 0.0005 * (4.0 - Math.PI);

        private static int ArcsOf(GeoPolygonArc3 loop) => loop.GetEdges().Count(edge => edge.IsArc);

        [Fact]
        public void ARectangleInSpaceFilletedAtFiveTenThousandthsAtAMillionthCopied_KeepsItsFourArcs()
        {
            GeoPolygonArc3 copy = Rounded().Clone();

            Assert.Equal(8, copy.VertexCount);
            Assert.Equal(4, ArcsOf(copy));
            Assert.Equal(RoundedArea, copy.Area, 9);
        }

        [Fact]
        public void ARectangleInSpaceFilletedAtFiveTenThousandthsAtAMillionthMoved_KeepsItsFourArcsWhereTheyWere()
        {
            GeoPolygonArc3 rounded = Rounded();
            var by = new GeoVector3(1, 2, 3);

            GeoPolygonArc3 moved = rounded.Translate(by);

            Assert.Equal(8, moved.VertexCount);
            Assert.Equal(4, ArcsOf(moved));

            for (int i = 0; i < rounded.VertexCount; i++)
            {
                Assert.True(moved[i].IsEqualTo(rounded[i].Add(by), Tight));
            }
        }

        [Fact]
        public void ARectangleInSpaceFilletedAtFiveTenThousandthsAtAMillionthTurned_KeepsItsFourArcs()
        {
            GeoPolygonArc3 turned = Rounded().TransformBy(GeoTransform3.RotationAxis(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1), 0.7));

            Assert.Equal(8, turned.VertexCount);
            Assert.Equal(4, ArcsOf(turned));
            Assert.Equal(RoundedArea, turned.Area, 9);
        }

        [Fact]
        public void ARectangleInSpaceFilletedAtFiveTenThousandthsAtAMillionthMirrored_KeepsItsFourArcs()
        {
            GeoPolygonArc3 mirrored = Rounded().TransformBy(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0))));

            Assert.Equal(8, mirrored.VertexCount);
            Assert.Equal(4, ArcsOf(mirrored));
            Assert.Equal(RoundedArea, mirrored.Area, 9);
            Assert.All(mirrored.Vertices, corner => Assert.Equal(-20.0, corner.X, 12));
        }
    }
}
