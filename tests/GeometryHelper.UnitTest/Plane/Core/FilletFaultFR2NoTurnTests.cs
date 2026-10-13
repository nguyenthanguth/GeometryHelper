using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A corner that does not turn, as where a straight piece runs on into an arc it is tangent to, is left as it is.
    /// </summary>
    /// <remarks>
    /// The fillet had no test for a straight corner, so the joint of a bent bar was "rounded" by an arc of no sweep: the
    /// straight (-16.47, 9.135) to (-12.95, 4.540) running into a tangent arc of bulge 0.4557 came back with a third
    /// piece, an arc of bulge -5.6E-17 between two copies of the joint 1E-15 apart, at every radius and tolerance tried.
    /// </remarks>
    public class FilletFaultFR2NoTurnTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>
        /// A bent bar: 100 along, a quarter turn of radius 50 to the left, and 100 up, every joint tangent.
        /// </summary>
        private static GeoPolylineArc2 BentBar() => new GeoPolylineArc2(
            new[] { P(0, 0), P(100, 0), P(150, 50), P(150, 150) },
            new[] { 0.0, Math.Tan(Math.PI / 8.0), 0.0, 0.0 });

        private static void AssertUnchanged(GeoPolylineArc2 source, GeoPolylineArc2 result)
        {
            Assert.Equal(source.VertexCount, result.VertexCount);

            for (int i = 0; i < source.VertexCount; i++)
            {
                Assert.True(source[i].IsEqualTo(result[i], Tight));
                Assert.Equal(source.GetBulgeAt(i), result.GetBulgeAt(i), 12);
            }
        }

        [Theory]
        [InlineData(0.5, 1E-4)]
        [InlineData(2.0, 1E-3)]
        [InlineData(5.0, 0.1)]
        public void AStraightRunningOnIntoATangentArc_IsLeftAsItIs(double radius, double point)
        {
            var chain = new GeoPolylineArc2(
                new[] { P(-16.469603272373604, 9.1351020658086526), P(-12.948015227449066, 4.53966078708154), P(-11.931565295119594, 4.47675571380048) },
                new[] { 0.0, 0.45567259435258689, 0.0 });

            var tolerance = new Tolerance(point, 1E-5);

            AssertUnchanged(chain, Corner2.Fillet(chain, radius, tolerance));

            Assert.False(Corner2.TryFilletAt(chain, 1, radius, out GeoPolylineArc2 one, tolerance));
            Assert.Same(chain, one);
        }

        [Fact]
        public void ABentBarFilletedAtTwenty_KeepsItsTangentJointsAndGainsNoPiece()
        {
            GeoPolylineArc2 bar = BentBar();

            AssertUnchanged(bar, Corner2.Fillet(bar, 20.0, Tolerance.Default));
        }

        [Fact]
        public void ABarKinkedHalfADegreeWhereItsArcStarts_IsLeftAsItIsAtTheDefaultTolerance()
        {
            // The quarter turn starts 0.5° off the straight before it, inside the default angle tolerance of 1°.
            double chord = 50.0 * Math.Sqrt(2.0);
            double heading = (45.0 + 0.5) * Math.PI / 180.0;

            var bar = new GeoPolylineArc2(
                new[] { P(0, 0), P(100, 0), P(100 + chord * Math.Cos(heading), chord * Math.Sin(heading)) },
                new[] { 0.0, Math.Tan(Math.PI / 8.0), 0.0 });

            AssertUnchanged(bar, Corner2.Fillet(bar, 20.0, Tolerance.Default));

            Assert.False(Corner2.TryFilletAt(bar, 1, 20.0, out GeoPolylineArc2 one, Tolerance.Default));
            Assert.Same(bar, one);
        }

        [Fact]
        public void ASlotWhoseEndsTurnHalfADegreeTooFar_KeepsItsFourPieces()
        {
            // 100 by 40 with ends of 181° rather than 180°: each of the four joints turns 0.5° the other way, inside the
            // default angle tolerance of 1°.
            double bulge = Math.Tan(181.0 * Math.PI / 180.0 / 4.0);

            var slot = new GeoPolygonArc2(new[] { P(0, 0), P(100, 0), P(100, 40), P(0, 40) }, new[] { 0.0, bulge, 0.0, bulge });

            GeoPolygonArc2 filleted = Corner2.Fillet(slot, 10.0, Tolerance.Default);

            Assert.Equal(4, filleted.EdgeCount);

            for (int i = 0; i < slot.VertexCount; i++)
            {
                Assert.True(slot[i].IsEqualTo(filleted[i], Tight));
                Assert.Equal(slot.GetBulgeAt(i), filleted.GetBulgeAt(i), 12);
            }
        }

        [Fact]
        public void ABentBarWithOneRealCorner_IsRoundedThereAndNowhereElse()
        {
            // The bar turns a further quarter to the left at (150, 150), a real corner between two straights.
            var bar = new GeoPolylineArc2(
                new[] { P(0, 0), P(100, 0), P(150, 50), P(150, 150), P(50, 150) },
                new[] { 0.0, Math.Tan(Math.PI / 8.0), 0.0, 0.0, 0.0 });

            GeoPolylineArc2 filleted = Corner2.Fillet(bar, 20.0, Tolerance.Default);

            // The two tangent joints stay where they were, and the corner becomes a quarter turn of radius 20.
            Assert.Equal(6, filleted.VertexCount);
            Assert.True(filleted[1].IsEqualTo(P(100, 0), Tight));
            Assert.True(filleted[2].IsEqualTo(P(150, 50), Tight));

            GeoEdge2 arc = filleted.GetEdgeAt(3);

            Assert.True(arc.IsArc);
            Assert.Equal(Math.PI / 2.0, arc.ToArc().SweptAngle, 9);
            Assert.True(arc.StartPoint.IsEqualTo(P(150, 130), Tight));
            Assert.True(arc.EndPoint.IsEqualTo(P(130, 150), Tight));
        }
    }
}
