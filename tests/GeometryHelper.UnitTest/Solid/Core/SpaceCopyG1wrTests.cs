using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A curved chain in space is copied and moved edge for edge; a polygon and a face in space of any area are weighed for
    /// their centroids; and two axes span a coordinate system by the angle between them, whatever their lengths.
    /// </summary>
    /// <remarks>
    /// The copies were put back together within the global tolerance of a thousandth, which dropped the arcs and legs a
    /// chain keeps within a finer one. The centroids took an area of a millionth or less for none: a polygon gave its first
    /// corner and a face its boundary's centroid. Two axes were judged parallel by the length of their cross product against
    /// the global vector tolerance of 1E-5, so two short axes square to each other were refused and two long ones a few
    /// millionths of a radian apart were not. No test sets the global tolerance.
    /// </remarks>
    public class SpaceCopyG1wrTests
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

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        // The chain (0, 0, 0), (10, 0, 0), (10, 0, 10) rounded by 0.0005 at a millionth: an arc 0.0007 across between two legs.
        private static GeoPolylineArc3 Rounded() =>
            new GeoPolyline3(new[] { P(0, 0, 0), P(10, 0, 0), P(10, 0, 10) }).Fillet(0.0005, Millionth);

        private static int ArcsOf(GeoPolylineArc3 chain) => chain.GetEdges().Count(edge => edge.IsArc);

        [Fact]
        public void AChainInSpaceFilletedAtFiveTenThousandthsAtAMillionthCopied_KeepsItsArc()
        {
            GeoPolylineArc3 rounded = Rounded();

            GeoPolylineArc3 copy = rounded.Clone();

            Assert.Equal(4, copy.VertexCount);
            Assert.Equal(1, ArcsOf(copy));
            Assert.Equal(rounded.Length, copy.Length, 12);
        }

        [Fact]
        public void AChainInSpaceFilletedAtFiveTenThousandthsAtAMillionthMoved_KeepsItsArcWhereItWas()
        {
            GeoPolylineArc3 rounded = Rounded();
            var by = new GeoVector3(1, 2, 3);

            GeoPolylineArc3 moved = rounded.Translate(by);

            Assert.Equal(4, moved.VertexCount);
            Assert.Equal(1, ArcsOf(moved));

            for (int i = 0; i < rounded.VertexCount; i++)
            {
                Assert.True(moved[i].IsEqualTo(rounded[i].Add(by), Tight));
                Assert.Equal(rounded.Bulges[i], moved.Bulges[i]);
            }
        }

        [Fact]
        public void ATriangleStandingInSpaceNineTenThousandthsAcross_BalancesAtAThirdOfItsLegs()
        {
            // 4.05E-7 square units.
            var triangle = new GeoPolygon3(new[] { P(10, 20, 30), P(10.0009, 20, 30), P(10, 20, 30.0009) }, Millionth);

            GeoPoint3 centroid = triangle.Centroid;

            Assert.True(centroid.IsEqualTo(P(10.0003, 20, 30.0003), Tight));
        }

        [Fact]
        public void ASquareStandingInSpaceAThousandthAcrossWithAHoleFourTenThousandthsAcross_BalancesWhereItsMaterialDoes()
        {
            // 8.4E-7 square units: the hole's 1.6E-7 taken from the boundary's 1E-6 about (0.0003, 0, 0.0003) moves the
            // balance from (0.0005, 0, 0.0005) to 0.000452 / 0.84 = 0.000538095... along x and z.
            var boundary = new GeoPolygon3(
                new[] { P(0, 0, 0), P(0.001, 0, 0), P(0.001, 0, 0.001), P(0, 0, 0.001) },
                Millionth
            );
            var hole = new GeoPolygon3(
                new[] { P(0.0001, 0, 0.0001), P(0.0001, 0, 0.0005), P(0.0005, 0, 0.0005), P(0.0005, 0, 0.0001) },
                Millionth
            );
            var face = new GeoFace3(boundary, new[] { hole }, Millionth);

            GeoPoint3 centroid = face.Centroid;

            Assert.True(centroid.IsEqualTo(P(0.000452 / 0.84, 0, 0.000452 / 0.84), Tight));
        }

        [Fact]
        public void TwoAxesTwoThousandthsLongSquareToEachOther_SpanACoordinateSystem()
        {
            var system = new GeoCoordinateSystem3(P(1, 2, 3), new GeoVector3(0.002, 0, 0), new GeoVector3(0, 0.002, 0));

            Assert.Equal(1.0, system.XAxis.X, 15);
            Assert.Equal(1.0, system.YAxis.Y, 15);
            Assert.Equal(1.0, system.ZAxis.Z, 15);
        }

        [Fact]
        public void TwoAxesTwoThousandthsLongAtAnAngle_SpanACoordinateSystemSquaredUp()
        {
            var system = new GeoCoordinateSystem3(
                P(1, 2, 3),
                new GeoVector3(0.002, 0, 0),
                new GeoVector3(0.001, 0.002, 0)
            );

            Assert.Equal(1.0, system.XAxis.X, 15);
            Assert.Equal(1.0, system.YAxis.Y, 15);
            Assert.Equal(1.0, system.ZAxis.Z, 15);
        }

        [Fact]
        public void TwoUnitAxesAMillionthOfARadianApart_AreParallel()
        {
            Assert.Throws<ArgumentException>(() =>
                new GeoCoordinateSystem3(P(0, 0, 0), new GeoVector3(1, 0, 0), new GeoVector3(1, 1E-6, 0))
            );
        }

        [Fact]
        public void TwoAxesTenLongFiveMillionthsOfARadianApart_AreParallel()
        {
            // Their cross product is 5E-4 long, which the vector tolerance read as a length took for a plane they span.
            Assert.Throws<ArgumentException>(() =>
                new GeoCoordinateSystem3(P(0, 0, 0), new GeoVector3(10, 0, 0), new GeoVector3(10, 0.00005, 0))
            );
        }
    }
}
