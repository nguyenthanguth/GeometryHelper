using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An open chain in space whose pieces may be arcs: straight runs with a bend in them, which is what a
    /// reinforcing bar is. Nothing here requires the chain to be flat, and the binding promise is the one
    /// <see cref="GeoEdge3"/> carries: a chain laid flat in the XY plane answers what
    /// <see cref="GeoPolylineArc2"/> answers of the same numbers.
    /// </summary>
    public class GeoPolylineArc3Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoVector3 Up() => new GeoVector3(0, 0, 1);

        /// <summary>
        /// A hairpin lying flat: out along X, a half turn of radius fifty, and back.
        /// </summary>
        private static GeoPolylineArc3 Hairpin() => new GeoPolylineArc3(
            new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(200, 0, 0), new GeoPoint3(200, -100, 0), new GeoPoint3(0, -100, 0) },
            new[] { 0.0, -1.0, 0.0, 0.0 },
            new[] { default(GeoVector3), Up(), default(GeoVector3), default(GeoVector3) });

        private static GeoPolylineArc2 HairpinFlat() => new GeoPolylineArc2(
            new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, -100), new GeoPoint2(0, -100) },
            new[] { 0.0, -1.0, 0.0, 0.0 });

        /// <summary>
        /// A chain that leaves the plane: the second bend turns about a different axis from the first.
        /// </summary>
        private static GeoPolylineArc3 Twisted() => new GeoPolylineArc3(
            new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(100, 100, 100) },
            new[] { 0.0, 0.3, 0.4, 0.0 },
            new[] { default(GeoVector3), new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), default(GeoVector3) });

        [Fact]
        public void AChainLaidFlatIsTheChainThePlaneWouldHaveMade()
        {
            GeoPolylineArc3 space = Hairpin();
            GeoPolylineArc2 flat = HairpinFlat();

            Assert.Equal(flat.VertexCount, space.VertexCount);
            Assert.Equal(flat.EdgeCount, space.EdgeCount);
            Assert.Equal(flat.Length, space.Length, 8);

            for (int i = 0; i <= 20; i++)
            {
                GeoPoint2 there = flat.GetPointAtParameter(i / 20.0);
                GeoPoint3 here = space.GetPointAtParameter(i / 20.0);

                Assert.True(here.IsEqualTo(new GeoPoint3(there.X, there.Y, 0.0), new Tolerance(1E-7, 1E-7)), "at " + i);
            }

            // One edge of the three curves, and it is the one that was given a bulge.
            Assert.Equal(1, space.GetEdges().Count(edge => edge.IsArc));
            Assert.True(space.GetEdgeAt(1).IsArc);
            Assert.Equal(50.0, space.GetEdgeAt(1).ToArc().Radius, 8);
        }

        [Fact]
        public void TheLengthIsWalkedAlongTheArcsAndNotAcrossTheirChords()
        {
            GeoPolylineArc3 hairpin = Hairpin();

            // Two straight runs of two hundred, and a half turn of radius fifty between them.
            Assert.Equal(200.0 + Math.PI * 50.0 + 200.0, hairpin.Length, 8);

            // Flattening throws the curve away, and the chord is shorter than the arc it stood for.
            Assert.Equal(200.0 + 100.0 + 200.0, hairpin.Flatten().Length, 8);
            Assert.True(hairpin.Length > hairpin.Flatten().Length);

            // Walking lands on the ends, and never outside them.
            Assert.True(hairpin.GetPointAtDistance(-10.0).IsEqualTo(hairpin.StartPoint, Tight));
            Assert.True(hairpin.GetPointAtDistance(1E6).IsEqualTo(hairpin.EndPoint, Tight));
            Assert.Equal(0.0, hairpin.GetParameterAtDistance(-1.0), 12);
            Assert.Equal(1.0, hairpin.GetParameterAtDistance(1E6), 12);
        }

        [Fact]
        public void TheNearestPointIsOnTheBendAndNotOnItsChord()
        {
            GeoPolylineArc3 hairpin = Hairpin();

            // The half turn bulges out to x = 250, so a point beyond it is answered on the bend.
            var beyond = new GeoPoint3(300, -50, 0);

            Assert.True(hairpin.GetClosestPointOnBoundary(beyond).IsEqualTo(new GeoPoint3(250, -50, 0), new Tolerance(1E-7, 1E-7)));
            Assert.Equal(50.0, hairpin.DistanceTo(beyond), 7);

            Assert.True(hairpin.IsPointOn(new GeoPoint3(250, -50, 0), new Tolerance(1E-6, 1E-6)));
            Assert.False(hairpin.IsPointOn(new GeoPoint3(200, -50, 0)));
            Assert.Equal(PointLocation.OutSide, hairpin.Locate(new GeoPoint3(200, -50, 0)));

            // Off the plane the nearest point is still on the chain.
            Assert.Equal(Math.Sqrt(50.0 * 50.0 + 120.0 * 120.0), hairpin.DistanceTo(new GeoPoint3(300, -50, 120)), 7);

            // And walking to it gives back the distance along, which puts it back where it was.
            double along = hairpin.GetDistanceAtPoint(beyond);

            Assert.True(hairpin.GetPointAtDistance(along).IsEqualTo(new GeoPoint3(250, -50, 0), new Tolerance(1E-6, 1E-6)));
        }

        [Fact]
        public void AChainNeedNotBeFlatAndSaysWhetherItIs()
        {
            Assert.True(Hairpin().IsPlanar());
            Assert.True(Hairpin().TryGetPlane(out GeoPlane3 plane));
            Assert.True(plane.Normal.IsParallelTo(Up(), Tight));

            // The twisted one bends about two different axes, so no one plane holds it.
            Assert.False(Twisted().IsPlanar());
            Assert.False(Twisted().TryGetPlane(out _));

            // Vertices alone do not settle it. These three are flat whichever way the bend goes, because
            // three points always are; it is the plane the arc bulges in that decides.
            var corner = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0) };

            var inPlane = new GeoPolylineArc3(corner, new[] { 0.5, 0.0, 0.0 }, new[] { Up(), default(GeoVector3), default(GeoVector3) });
            var outOfPlane = new GeoPolylineArc3(corner, new[] { 0.5, 0.0, 0.0 }, new[] { new GeoVector3(0, 1, 0), default(GeoVector3), default(GeoVector3) });

            Assert.True(new GeoPolyline3(corner).IsPlanar());
            Assert.True(inPlane.IsPlanar());
            Assert.False(outOfPlane.IsPlanar());
        }

        [Fact]
        public void WalkingBackwardsDrawsTheSameChain()
        {
            GeoPolylineArc3 chain = Twisted();
            GeoPolylineArc3 back = chain.Reverse();

            Assert.Equal(chain.Length, back.Length, 7);
            Assert.True(back.StartPoint.IsEqualTo(chain.EndPoint, Tight));
            Assert.True(back.EndPoint.IsEqualTo(chain.StartPoint, Tight));

            for (int i = 0; i <= 20; i++)
            {
                Assert.True(
                    back.GetPointAtParameter(i / 20.0).IsEqualTo(chain.GetPointAtParameter(1.0 - i / 20.0), new Tolerance(1E-6, 1E-6)),
                    "at " + i);
            }

            Assert.True(back.Reverse().IsEqualTo(chain, new Tolerance(1E-7, 1E-7)));
        }

        [Fact]
        public void MovingTheChainCarriesEveryBendWithIt()
        {
            GeoPolylineArc3 chain = Twisted();

            foreach (GeoTransform3 move in new[]
            {
                GeoTransform3.RotationX(0.6),
                GeoTransform3.RotationAxis(new GeoVector3(2, -1, 3), 0.9),
                GeoTransform3.Translation(new GeoVector3(5, 6, 7)),
                GeoTransform3.Mirror(GeoPlane3.XY)
            })
            {
                GeoPolylineArc3 moved = chain.TransformBy(move);

                Assert.Equal(chain.Length, moved.Length, 6);
                Assert.Equal(chain.EdgeCount, moved.EdgeCount);

                for (int i = 0; i <= 10; i++)
                {
                    Assert.True(
                        moved.GetPointAtParameter(i / 10.0).IsEqualTo(
                            chain.GetPointAtParameter(i / 10.0).TransformBy(move), new Tolerance(1E-6, 1E-6)),
                        "at " + i);
                }
            }

            var by = new GeoVector3(5, 6, 7);

            Assert.True(chain.Translate(by).IsEqualTo(chain.TransformBy(GeoTransform3.Translation(by)), new Tolerance(1E-7, 1E-7)));
        }

        [Fact]
        public void TheBoxHoldsTheBendsAndNotJustTheVertices()
        {
            GeoPolylineArc3 hairpin = Hairpin();
            GeoAabb3 box = hairpin.GetAabb();

            // The half turn reaches x = 250, past every vertex.
            Assert.Equal(250.0, box.Max.X, 6);
            Assert.True(box.Contains(new GeoPoint3(250, -50, 0)));

            // The chords alone would have stopped at 200.
            Assert.Equal(200.0, GeoAabb3.FromPoints(hairpin.Vertices).Max.X, 9);
        }

        [Fact]
        public void SamplingFollowsTheArcsAsCloselyAsAsked()
        {
            GeoPolylineArc3 hairpin = Hairpin();

            foreach (double asked in new[] { 5.0, 1.0, 0.1, 0.01 })
            {
                GeoPolyline3 sampled = hairpin.ToPolyline3(asked);

                // Every point of the sample sits on the chain, to within what was asked for.
                foreach (GeoPoint3 vertex in sampled.Vertices)
                {
                    Assert.True(hairpin.DistanceTo(vertex) <= 1E-6, "a sampled vertex left the chain");
                }

                // And the sample is never longer than the chain, but closes on it as the ask tightens.
                Assert.True(sampled.Length <= hairpin.Length + 1E-9);
                Assert.True(sampled.Length >= hairpin.Length - asked * 10.0, "sampled " + sampled.Length + " against " + hairpin.Length);
            }

            Assert.True(hairpin.ToPolyline3(0.001).Length > hairpin.ToPolyline3(5.0).Length);

            Assert.Throws<ArgumentOutOfRangeException>(() => hairpin.ToPolyline3(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => hairpin.ToPolyline3(-1.0));
        }

        [Fact]
        public void AChainCanBeBuiltFromEdgesOrFromAStraightOne()
        {
            GeoPolylineArc3 hairpin = Hairpin();

            // From its own edges, back to itself.
            Assert.True(new GeoPolylineArc3(hairpin.GetEdges()).IsEqualTo(hairpin, Tight));

            // From a straight chain, losing nothing but gaining no curves.
            var straight = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0));
            var widened = new GeoPolylineArc3(straight);

            Assert.Equal(straight.Length, widened.Length, 9);
            Assert.All(widened.GetEdges(), edge => Assert.False(edge.IsArc));
            Assert.True(widened.Flatten().IsEqualTo(straight));
        }

        [Fact]
        public void AChainThatCannotBeDrawnSaysSo()
        {
            var points = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0) };

            Assert.Throws<ArgumentNullException>(() => new GeoPolylineArc3((System.Collections.Generic.IEnumerable<GeoPoint3>)null));
            Assert.Throws<ArgumentNullException>(() => new GeoPolylineArc3((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => new GeoPolylineArc3((System.Collections.Generic.IEnumerable<GeoEdge3>)null));
            Assert.Throws<ArgumentNullException>(() => Hairpin().TransformBy(null));

            // One vertex, or two in the same place, is no chain at all.
            Assert.Throws<ArgumentException>(() => new GeoPolylineArc3(new[] { points[0] }));
            Assert.Throws<ArgumentException>(() => new GeoPolylineArc3(new[] { points[0], points[0] }));

            // A bulge with no plane to bulge in is refused where it is given, not later on.
            Assert.Throws<ArgumentException>(() => new GeoPolylineArc3(points, new[] { 1.0, 0.0 }, null));
            Assert.Throws<ArgumentException>(() =>
                new GeoPolylineArc3(points, new[] { 1.0, 0.0 }, new[] { new GeoVector3(1, 0, 0), default(GeoVector3) }));

            // Edges that do not run end to end are not a chain.
            Assert.Throws<ArgumentException>(() => new GeoPolylineArc3(new[]
            {
                new GeoEdge3(points[0], points[1]),
                new GeoEdge3(new GeoPoint3(500, 0, 0), new GeoPoint3(600, 0, 0))
            }));

            Assert.Throws<ArgumentOutOfRangeException>(() => Hairpin().GetEdgeAt(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Hairpin().GetEdgeAt(Hairpin().EdgeCount));
        }

        [Fact]
        public void TwoChainsAreTheSameWhenEveryEdgeAgrees()
        {
            GeoPolylineArc3 hairpin = Hairpin();

            Assert.True(hairpin.IsEqualTo(Hairpin(), Tight));
            Assert.True(hairpin.Equals(Hairpin()));
            Assert.True(hairpin == Hairpin());
            Assert.Equal(hairpin.GetHashCode(), Hairpin().GetHashCode());
            Assert.True(hairpin.Clone().IsEqualTo(hairpin, Tight));

            Assert.False(hairpin.IsEqualTo(Twisted(), Tight));
            Assert.True(hairpin != Twisted());
            Assert.False(hairpin.IsEqualTo(null, Tight));

            // The same vertices with the bend in the other plane is a different chain.
            var other = new GeoPolylineArc3(
                hairpin.Vertices,
                new[] { 0.0, -1.0, 0.0, 0.0 },
                new[] { default(GeoVector3), new GeoVector3(1, 0, 0), default(GeoVector3), default(GeoVector3) });

            Assert.False(hairpin.IsEqualTo(other, Tight));

            Assert.Contains("curved", hairpin.ToString());
        }
    }
}
