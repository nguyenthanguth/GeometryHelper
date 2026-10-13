using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A corner rounded in space is rounded on the inside, as in the plane, and a joint that does not turn is left as it
    /// is: each corner is laid flat and handed to the plane's fillet, so the plane's fixes carry over.
    /// </summary>
    /// <remarks>
    /// A bar (0, 0, 0), (10, 0, 0), (3, 5, 0) bent at a radius of 0.0001 came back running on to (10.000032, 0, 0), past
    /// the corner, and then turning back along an arc of 35.5° for the corner's 144.5°. The same fault left a closed
    /// loop with a reversed arc, and the joints of a curved loop turning half a degree, inside the default angle
    /// tolerance of 1°, were rounded.
    /// </remarks>
    public class FilletFaultInSpaceTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A frame tilted out of every axis, its origin away from the world's.
        /// </summary>
        private static GeoCoordinateSystem3 Tilted() => new GeoCoordinateSystem3(
            new GeoPoint3(120, -40, 75), new GeoVector3(0.6, 0.8, 0.0), new GeoVector3(-0.48, 0.36, 0.8));

        private static GeoPoint3 In(GeoCoordinateSystem3 frame, double x, double y) => frame.ToGlobal(new GeoPoint3(x, y, 0));

        [Theory]
        [InlineData(0.002)]
        [InlineData(0.0001)]
        public void ABarBent144DegreesAtATinyRadius_IsRoundedInsideTheBend(double radius)
        {
            GeoCoordinateSystem3 frame = Tilted();

            var bar = new GeoPolyline3(In(frame, 0, 0), In(frame, 10, 0), In(frame, 3, 5));

            GeoPolylineArc3 bent = Corner3.Fillet(bar, new[] { 0.0, radius, 0.0 }, Thousandth);

            // The bend turns by the angle between (1, 0) and (-7, 5), and its touching points stand r tan(turn / 2)
            // back from the corner along each leg.
            double turn = Math.Atan2(5.0, -7.0);
            double reach = radius * Math.Tan(turn / 2.0);

            Assert.Equal(3, bent.EdgeCount);
            Assert.True(bent.GetEdgeAt(1).IsArc);
            Assert.True(bent.GetEdgeAt(0).EndPoint.IsEqualTo(In(frame, 10.0 - reach, 0), Tight));
            Assert.True(bent.GetEdgeAt(2).StartPoint.IsEqualTo(In(frame, 10.0 - 7.0 * reach / Math.Sqrt(74.0), 5.0 * reach / Math.Sqrt(74.0)), Tight));

            // Read through the bulge: an arc this small is below the global tolerance, which its own reading uses.
            Assert.Equal(turn, 4.0 * Math.Atan(Math.Abs(bent.GetEdgeAt(1).Bulge)), 9);
            Assert.Equal(bar.Length - 2.0 * reach, bent.GetEdgeAt(0).Length + bent.GetEdgeAt(2).Length, 9);
        }

        [Fact]
        public void ATriangleInATiltedPlaneFilletedByTwoThousandths_TurnsEveryArcTheWayItsCornerTurns()
        {
            GeoCoordinateSystem3 frame = Tilted();

            var triangle = new GeoPolygonArc3(new[] { In(frame, 0, 0), In(frame, 10, 0), In(frame, 3, 5) });

            GeoPolygonArc3 rounded = triangle.Fillet(0.002, Thousandth);

            // Three sides and three arcs; the arcs of a convex loop turn one way and, between them, a whole turn.
            Assert.Equal(6, rounded.EdgeCount);

            double swept = 0.0;
            int arcs = 0;

            for (int i = 0; i < rounded.EdgeCount; i++)
            {
                GeoEdge3 edge = rounded.GetEdgeAt(i);

                if (edge.IsArc)
                {
                    arcs++;
                    swept += 4.0 * Math.Atan(Math.Abs(edge.Bulge));
                }
            }

            Assert.Equal(3, arcs);
            Assert.Equal(2.0 * Math.PI, swept, 9);
        }

        [Fact]
        public void ASlotInATiltedPlaneWhoseEndsTurnHalfADegreeTooFar_KeepsItsFourPieces()
        {
            GeoCoordinateSystem3 frame = Tilted();

            // 100 by 40 with ends of 181° rather than 180°: each of the four joints turns 0.5°, inside the default
            // angle tolerance of 1°.
            GeoPoint3[] corners = { In(frame, 0, 0), In(frame, 100, 0), In(frame, 100, 40), In(frame, 0, 40) };

            double bulge = Math.Tan(181.0 * Math.PI / 180.0 / 4.0);

            var slot = new GeoPolygonArc3(frame, new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 40), new GeoPoint2(0, 40) },
                new[] { 0.0, bulge, 0.0, bulge }));

            GeoPolygonArc3 filleted = slot.Fillet(10.0, Thousandth);

            Assert.Equal(4, filleted.EdgeCount);

            for (int i = 0; i < corners.Length; i++)
            {
                Assert.True(filleted.GetEdgeAt(i).StartPoint.IsEqualTo(corners[i], Tight));
            }

            Assert.Equal(slot.Length, filleted.Length, 9);
        }
    }
}
