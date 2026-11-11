using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A curved chain, a straight chain and a polygon in the plane are copied, moved and turned edge for edge; a face of any
    /// area is weighed with its holes for its centroid; a coordinate system follows a scaling of any size; and a loop of any
    /// size can be flattened.
    /// </summary>
    /// <remarks>
    /// The copies were put back together within the global tolerance of a thousandth, which dropped the arcs and sides a
    /// chain or a polygon keeps within a finer one. The centroid of a face took an area of a millionth or less for none and
    /// gave the boundary's, its holes left out. A coordinate system read the global vector tolerance of 1E-5 as a length,
    /// so a scaling by 1E-5 or less left it with no axes. A loop flattened was put back together within the global
    /// tolerance too, and one smaller than a thousandth across had too few corners left to be a polygon. No test sets the
    /// global tolerance.
    /// </remarks>
    public class CopyG1wrTests
    {
        private static readonly Tolerance Millionth = new Tolerance(
            1E-6,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-6
        );
        private static readonly Tolerance Tight = new Tolerance(
            1E-12,
            Tolerance.DefaultEqualVector,
            Tolerance.DefaultEqualAngleRad,
            1E-12
        );

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        // The chain (0, 0), (10, 0), (10, 10) rounded by 0.0005 at a millionth: an arc 0.0007 across between two legs.
        private static GeoPolylineArc2 Rounded() =>
            new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10)).Fillet(0.0005, Millionth);

        private static int ArcsOf(GeoPolylineArc2 chain) => chain.GetEdges().Count(edge => edge.IsArc);

        [Fact]
        public void AChainFilletedAtFiveTenThousandthsAtAMillionthCopied_KeepsItsArc()
        {
            GeoPolylineArc2 rounded = Rounded();

            GeoPolylineArc2 copy = rounded.Clone();

            Assert.Equal(4, copy.VertexCount);
            Assert.Equal(1, ArcsOf(copy));
            Assert.Equal(rounded.Length, copy.Length, 12);
        }

        [Fact]
        public void AChainFilletedAtFiveTenThousandthsAtAMillionthMoved_KeepsItsArcWhereItWas()
        {
            GeoPolylineArc2 rounded = Rounded();
            var by = new GeoVector2(5, 7);

            GeoPolylineArc2 moved = rounded.Translate(by);

            Assert.Equal(4, moved.VertexCount);
            Assert.Equal(1, ArcsOf(moved));

            for (int i = 0; i < rounded.VertexCount; i++)
            {
                Assert.True(moved[i].IsEqualTo(rounded[i].Add(by), Tight));
                Assert.Equal(rounded.Bulges[i], moved.Bulges[i]);
            }
        }

        [Fact]
        public void AChainFilletedAtFiveTenThousandthsAtAMillionthTurnedAboutAPoint_KeepsItsArc()
        {
            GeoPolylineArc2 rounded = Rounded();

            GeoPolylineArc2 turned = rounded.RotateBy(0.7, P(3, 4));

            Assert.Equal(4, turned.VertexCount);
            Assert.Equal(1, ArcsOf(turned));
            Assert.Equal(rounded.Length, turned.Length, 12);
        }

        [Fact]
        public void ATriangleCutByThreeTenThousandthsAtAMillionthMoved_KeepsItsSixCorners()
        {
            GeoPolygon2 cut = Corner2.Chamfer(new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10)), 0.0003, 0.0003, Millionth);
            var by = new GeoVector2(5, 7);

            GeoPolygon2 moved = cut.Translate(by);

            Assert.Equal(6, moved.VertexCount);

            for (int i = 0; i < cut.VertexCount; i++)
            {
                Assert.True(moved[i].IsEqualTo(cut[i].Add(by), Tight));
            }
        }

        [Fact]
        public void ATriangleCutByThreeTenThousandthsAtAMillionthTurnedAboutAPoint_KeepsItsSixCorners()
        {
            GeoPolygon2 cut = Corner2.Chamfer(new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10)), 0.0003, 0.0003, Millionth);

            GeoPolygon2 turned = cut.RotateBy(0.7, P(3, 4));

            Assert.Equal(6, turned.VertexCount);
            Assert.Equal(cut.Area, turned.Area, 12);
        }

        [Fact]
        public void AChainCutByThreeTenThousandthsAtAMillionthMoved_TurnsAtBothEndsOfTheCut()
        {
            GeoPolyline2 cut = Corner2.Chamfer(
                new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10)),
                0.0003,
                0.0003,
                Millionth
            );

            GeoPolyline2 moved = cut.Translate(new GeoVector2(5, 7));

            Assert.Equal(4, moved.VertexCount);
            Assert.True(moved[1].IsEqualTo(P(14.9997, 7), Tight));
            Assert.True(moved[2].IsEqualTo(P(15, 7.0003), Tight));
        }

        [Fact]
        public void AChainCutByThreeTenThousandthsAtAMillionthTurnedAboutAPoint_TurnsAtBothEndsOfTheCut()
        {
            GeoPolyline2 cut = Corner2.Chamfer(
                new GeoPolyline2(P(0, 0), P(10, 0), P(10, 10)),
                0.0003,
                0.0003,
                Millionth
            );

            GeoPolyline2 turned = cut.RotateBy(Math.PI / 2.0, P(0, 0));

            Assert.Equal(4, turned.VertexCount);
            Assert.True(turned[1].IsEqualTo(P(0, 9.9997), Tight));
            Assert.True(turned[2].IsEqualTo(P(-0.0003, 10), Tight));
        }

        [Fact]
        public void ASquareAThousandthAcrossWithAHoleFourTenThousandthsAcross_BalancesWhereItsMaterialDoes()
        {
            // 8.4E-7 square units: the hole's 1.6E-7 taken from the boundary's 1E-6 about (0.0003, 0.0003) moves the
            // balance from (0.0005, 0.0005) to 0.000452 / 0.84 = 0.000538095... each way.
            var face = new GeoFace2(
                ConvexHull2.Of(new[] { P(0, 0), P(0.001, 0), P(0.001, 0.001), P(0, 0.001) }, Tight),
                new[]
                {
                    ConvexHull2.Of(
                        new[] { P(0.0001, 0.0001), P(0.0001, 0.0005), P(0.0005, 0.0005), P(0.0005, 0.0001) },
                        Tight
                    ),
                }
            );

            GeoPoint2 centroid = face.Centroid;

            Assert.Equal(0.000452 / 0.84, centroid.X, 15);
            Assert.Equal(0.000452 / 0.84, centroid.Y, 15);
        }

        [Fact]
        public void AFaceWhoseHoleIsItsBoundary_BalancesWhereItsBoundaryDoes()
        {
            GeoPolygon2 boundary = ConvexHull2.Of(new[] { P(0, 0), P(0.001, 0), P(0.001, 0.001), P(0, 0.001) }, Tight);
            var face = new GeoFace2(boundary, new[] { boundary });

            GeoPoint2 centroid = face.Centroid;

            Assert.Equal(0.0005, centroid.X, 15);
            Assert.Equal(0.0005, centroid.Y, 15);
        }

        [Fact]
        public void ACoordinateSystemScaledByAMillionth_KeepsItsAxes()
        {
            var system = new GeoCoordinateSystem2(P(1, 2), 0.5);

            GeoCoordinateSystem2 scaled = system.TransformBy(GeoTransform2.Scaling(1E-6));

            Assert.True(scaled.Origin.IsEqualTo(P(1E-6, 2E-6), Tight));
            Assert.Equal(system.XAxis.X, scaled.XAxis.X, 15);
            Assert.Equal(system.XAxis.Y, scaled.XAxis.Y, 15);
            Assert.Equal(system.YAxis.X, scaled.YAxis.X, 15);
            Assert.Equal(system.YAxis.Y, scaled.YAxis.Y, 15);
        }

        [Fact]
        public void ASquareFiveTenThousandthsAcrossWithBowedSides_FlattensToEveryCornerOfItsChords()
        {
            // The square 50 by 50 with sides bowed by a bulge of 0.2, scaled down edge for edge.
            GeoPolygonArc2 loop = new GeoPolygonArc2(
                new[] { P(0, 0), P(50, 0), P(50, 50), P(0, 50) },
                new[] { 0.2, 0.2, 0.2, 0.2 }
            ).TransformBy(GeoTransform2.Scaling(1E-5));

            GeoPolygon2 flat = loop.Flatten();

            // The chords lie inside the arcs, so the polygon encloses a little less than the loop.
            Assert.True(flat.VertexCount > 4);
            Assert.InRange(flat.Area, 0.99 * loop.Area, loop.Area);
        }

        [Fact]
        public void ASquareFiveTenThousandthsAcross_FlattensToItsFourCorners()
        {
            GeoPolygonArc2 loop = new GeoPolygonArc2(
                new[] { P(0, 0), P(50, 0), P(50, 50), P(0, 50) },
                null
            ).TransformBy(GeoTransform2.Scaling(1E-5));

            GeoPolygon2 flat = loop.Flatten();

            Assert.Equal(4, flat.VertexCount);
            Assert.Equal(0.00000025, flat.Area, 15);
        }

        [Fact]
        public void AChainFiveTenThousandthsLongWithABowedLeg_FlattensToEveryCornerOfItsChords()
        {
            var chain = new GeoPolylineArc2(
                new[] { new GeoEdge2(P(0, 0), P(0.0005, 0), 0.0), new GeoEdge2(P(0.0005, 0), P(0.0005, 0.0005), 0.4) }
            );

            GeoPolyline2 flat = chain.Flatten();

            Assert.True(flat.VertexCount > 3);
            Assert.True(flat.StartPoint.IsEqualTo(P(0, 0), Tight));
            Assert.True(flat.EndPoint.IsEqualTo(P(0.0005, 0.0005), Tight));
        }
    }
}
