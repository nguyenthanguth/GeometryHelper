using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Rounding corners one at a time, and rounding each by its own radius. The index names a vertex, the
    /// way a bulge does, and a radius of nought means the corner is left as it is.
    /// </summary>
    public class FilletPerCornerTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-8, 1E-8);

        /// <summary>
        /// A rectangle 300 by 200, counter-clockwise from the origin.
        /// </summary>
        private static GeoPolygonArc2 Plate() => new GeoPolygonArc2(new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 200), new GeoPoint2(0, 200)));

        private static GeoPolylineArc2 Chain() => new GeoPolylineArc2(new[]
        {
            new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 150), new GeoPoint2(400, 150)
        });

        /// <summary>
        /// Gets the radius of the arc nearest a point, so that a corner can be named by where it was.
        /// </summary>
        private static double RadiusNear(GeoPolygonArc2 loop, GeoPoint2 vertex)
        {
            GeoEdge2[] arcs = loop.GetEdges().Where(edge => edge.IsArc).ToArray();

            if (arcs.Length == 0)
            {
                return 0.0;
            }

            GeoEdge2 nearest = arcs[0];
            double best = vertex.DistanceTo(nearest.GetPointAtParameter(0.5));

            foreach (GeoEdge2 edge in arcs)
            {
                double reach = vertex.DistanceTo(edge.GetPointAtParameter(0.5));

                if (reach < best)
                {
                    best = reach;
                    nearest = edge;
                }
            }

            return nearest.ToArc().Radius;
        }

        [Fact]
        public void EachCornerTakesTheRadiusGivenForItsOwnVertex()
        {
            // Vertex 0 at the origin, 1 bottom right, 2 top right, 3 top left.
            GeoPolygonArc2 rounded = Plate().Fillet(new[] { 40.0, 10.0, 0.0, 25.0 });

            // Three corners rounded, one left square.
            Assert.Equal(3, rounded.GetEdges().Count(edge => edge.IsArc));

            Assert.Equal(40.0, RadiusNear(rounded, new GeoPoint2(0, 0)), 8);
            Assert.Equal(10.0, RadiusNear(rounded, new GeoPoint2(300, 0)), 8);
            Assert.Equal(25.0, RadiusNear(rounded, new GeoPoint2(0, 200)), 8);

            // The corner given nought is still a corner: the shape passes through it.
            Assert.Contains(rounded.Vertices, v => v.IsEqualTo(new GeoPoint2(300, 200), Tight));

            // And the three that were rounded no longer reach their old vertices.
            Assert.DoesNotContain(rounded.Vertices, v => v.IsEqualTo(new GeoPoint2(0, 0), Tight));
            Assert.DoesNotContain(rounded.Vertices, v => v.IsEqualTo(new GeoPoint2(300, 0), Tight));
            Assert.DoesNotContain(rounded.Vertices, v => v.IsEqualTo(new GeoPoint2(0, 200), Tight));
        }

        [Fact]
        public void OneRadiusForEveryCornerIsTheSameAsTheSingleRadiusForm()
        {
            GeoPolygonArc2 plate = Plate();

            Assert.True(plate.Fillet(new[] { 30.0, 30.0, 30.0, 30.0 }).IsEqualTo(plate.Fillet(30.0)));

            GeoPolylineArc2 chain = Chain();

            // A chain has no corner at either end, so what is given for those is ignored.
            Assert.True(chain.Fillet(new[] { 99.0, 20.0, 20.0, 99.0 }).IsEqualTo(chain.Fillet(20.0)));

            // A list shorter than the shape leaves the rest of the corners alone.
            GeoPolygonArc2 partly = plate.Fillet(new[] { 20.0 });
            Assert.Equal(1, partly.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(20.0, RadiusNear(partly, new GeoPoint2(0, 0)), 8);

            // An empty list changes nothing at all.
            Assert.True(plate.Fillet(new double[0]).IsEqualTo(plate));
        }

        [Fact]
        public void ACornerAskingForMoreThanTheEdgeHoldsGivesWayToItsNeighbour()
        {
            // The bottom edge is 300 long. One corner wants 250 of it and the other 20, which together
            // ask for more than there is, so the greedy one is the one that gives way.
            GeoPolygonArc2 rounded = Plate().Fillet(new[] { 250.0, 20.0, 0.0, 0.0 });

            Assert.Equal(1, rounded.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(20.0, RadiusNear(rounded, new GeoPoint2(300, 0)), 8);

            // The corner that asked for too much is still square.
            Assert.Contains(rounded.Vertices, v => v.IsEqualTo(new GeoPoint2(0, 0), Tight));
        }

        [Fact]
        public void OneCornerCanBeRoundedOnItsOwn()
        {
            GeoPolygonArc2 plate = Plate();

            Assert.True(plate.TryFilletAt(1, 30.0, out GeoPolygonArc2 rounded));

            Assert.Equal(1, rounded.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(30.0, RadiusNear(rounded, new GeoPoint2(300, 0)), 8);
            Assert.Equal(5, rounded.VertexCount);

            // Rounding one corner is rounding that corner and nothing else.
            Assert.True(rounded.IsEqualTo(plate.Fillet(new[] { 0.0, 30.0, 0.0, 0.0 })));

            // The arc really touches both edges it sits between.
            GeoEdge2 arcEdge = rounded.GetEdges().Single(edge => edge.IsArc);
            GeoArc2 arc = arcEdge.ToArc();

            Assert.True(arc.Center.IsEqualTo(new GeoPoint2(270, 30), Tight));
            Assert.True(arc.StartPoint.IsEqualTo(new GeoPoint2(270, 0), Tight));
            Assert.True(arc.EndPoint.IsEqualTo(new GeoPoint2(300, 30), Tight));
        }

        [Fact]
        public void ACornerWithNoRoomOrNoTurnIsRefusedRatherThanForced()
        {
            GeoPolygonArc2 plate = Plate();

            // A radius larger than the plate cannot be fitted anywhere.
            Assert.False(plate.TryFilletAt(0, 500.0, out GeoPolygonArc2 untouched));
            Assert.True(untouched.IsEqualTo(plate));

            // The ends of a chain are not corners.
            GeoPolylineArc2 chain = Chain();
            Assert.False(chain.TryFilletAt(0, 20.0, out _));
            Assert.False(chain.TryFilletAt(chain.VertexCount - 1, 20.0, out _));
            Assert.True(chain.TryFilletAt(1, 20.0, out GeoPolylineArc2 eased));
            Assert.Equal(1, eased.GetEdges().Count(edge => edge.IsArc));

            // Its two ends stay where they were.
            Assert.True(eased[0].IsEqualTo(chain[0], Tight));
            Assert.True(eased[eased.VertexCount - 1].IsEqualTo(chain[chain.VertexCount - 1], Tight));
        }

        [Fact]
        public void ACornerAgainstACurveIsRoundedHereToo()
        {
            // The right-hand side bulges, so the corner at vertex 2 is against an arc.
            var slot = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(300, 200), new GeoPoint2(0, 200) },
                new[] { 0.0, 0.4, 0.0, 0.0 });

            Assert.True(slot.TryFilletAt(2, 20.0, out GeoPolygonArc2 rounded));

            // The bulge that was there, plus the new fillet.
            Assert.Equal(2, rounded.GetEdges().Count(edge => edge.IsArc));

            GeoEdge2 fillet = rounded.GetEdges().Single(edge => edge.IsArc && Math.Abs(edge.ToArc().Radius - 20.0) < 1E-6);
            GeoArc2 arc = fillet.ToArc();

            // Its centre stands a radius away from the straight edge it touches.
            Assert.Equal(20.0, Math.Abs(200.0 - arc.Center.Y), 6);

            // Cutting the bulged side back kept the circle it was cut from.
            GeoArc2 was = slot.GetEdgeAt(1).ToArc();
            GeoArc2 now = rounded.GetEdges().Single(edge => edge.IsArc && edge.ToArc().Radius > 50.0).ToArc();

            Assert.Equal(was.Radius, now.Radius, 8);
            Assert.True(was.Center.IsEqualTo(now.Center, Tight));
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNonsense()
        {
            GeoPolygonArc2 plate = Plate();
            GeoPolylineArc2 chain = Chain();
            var radii = new[] { 10.0, 20.0, 0.0, 30.0 };

            Assert.True(Corner2.Fillet(plate, radii).IsEqualTo(plate.Fillet(radii)));
            Assert.True(Corner2.Fillet(plate, radii, Tolerance.Global).IsEqualTo(plate.Fillet(radii, Tolerance.Global)));
            Assert.True(Corner2.Fillet(chain, radii).IsEqualTo(chain.Fillet(radii)));
            Assert.True(Corner2.Fillet(chain, radii, Tolerance.Global).IsEqualTo(chain.Fillet(radii, Tolerance.Global)));

            Assert.Equal(
                Corner2.TryFilletAt(plate, 1, 30.0, out GeoPolygonArc2 fromStatic),
                plate.TryFilletAt(1, 30.0, out GeoPolygonArc2 fromInstance));
            Assert.True(fromStatic.IsEqualTo(fromInstance));

            Assert.Equal(
                Corner2.TryFilletAt(chain, 1, 30.0, out GeoPolylineArc2 staticChain),
                chain.TryFilletAt(1, 30.0, out GeoPolylineArc2 instanceChain, Tolerance.Global));
            Assert.True(staticChain.IsEqualTo(instanceChain));

            Assert.Throws<ArgumentNullException>(() => Corner2.Fillet(plate, (System.Collections.Generic.IReadOnlyList<double>)null));
            Assert.Throws<ArgumentNullException>(() => Corner2.Fillet((GeoPolygonArc2)null, radii));
            Assert.Throws<ArgumentOutOfRangeException>(() => plate.Fillet(new[] { 10.0, -5.0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => plate.Fillet(new[] { double.NaN }));
            Assert.Throws<ArgumentOutOfRangeException>(() => plate.TryFilletAt(99, 10.0, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => plate.TryFilletAt(1, 0.0, out _));
        }
    }
}
