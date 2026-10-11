using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// A handful of checks on the collisions and crossings in space that neither side of a pair answered: flat regions
    /// against each other, a plane, a ray and a body, rays against rays, boxes through their bodies, and an edge against
    /// the chains. The full suites live elsewhere; these only show the wiring holds.
    /// </summary>
    public class GroupCSmokeTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPoint3 Pt(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPolygon3 Square(double x0, double y0, double x1, double y1, double z)
            => new GeoPolygon3(Pt(x0, y0, z), Pt(x1, y0, z), Pt(x1, y1, z), Pt(x0, y1, z));

        // A square standing upright along y = 50.
        private static GeoPolygon3 Upright(double x0, double x1, double z0, double z1)
            => new GeoPolygon3(Pt(x0, 50, z0), Pt(x1, 50, z0), Pt(x1, 50, z1), Pt(x0, 50, z1));

        // The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle.
        private static GeoFace3 Plate() => new GeoFace3(Square(0, 0, 100, 100, 0), new[] { Square(30, 30, 70, 70, 0) });

        private static void AssertPoints(GeoPoint3[] actual, params GeoPoint3[] expected)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(actual[i].IsEqualTo(expected[i], Tol), actual[i] + " is not " + expected[i]);
            }
        }

        [Fact]
        public void ASquareStandingAcrossAnotherCrossesItWhereItsSidesMeetTheFloor()
        {
            GeoPolygon3 floor = Square(0, 0, 100, 100, 0);
            GeoPolygon3 upright = Upright(0, 100, -50, 50);

            AssertPoints(floor.GetIntersections(upright, Tol), Pt(100, 50, 0), Pt(0, 50, 0));
            Assert.True(floor.CollidesWith(upright, Tol));
            Assert.True(floor.TryIntersectWith(upright, out GeoPoint3[] crossings, Tol));
            Assert.Equal(2, crossings.Length);
            Assert.False(floor.CollidesWith(Square(0, 0, 100, 100, 10), Tol));
        }

        [Fact]
        public void TwoSquaresInOnePlaneCrossWhereTheirOutlinesDo()
        {
            GeoPolygon3 first = Square(0, 0, 100, 100, 0);
            GeoPolygon3 second = Square(50, 50, 150, 150, 0);

            AssertPoints(first.GetIntersections(second, Tol), Pt(100, 50, 0), Pt(50, 100, 0));
            Assert.True(first.CollidesWith(Square(20, 20, 40, 40, 0), Tol));
            Assert.Empty(first.GetIntersections(Square(20, 20, 40, 40, 0), Tol));
        }

        [Fact]
        public void ATriangleStandingInAHoleTouchesNothingAndOnTheMaterialItDoes()
        {
            var inHole = new GeoTriangle3(Pt(40, 50, -10), Pt(60, 50, -10), Pt(50, 50, 10));
            var onMaterial = new GeoTriangle3(Pt(70, 50, -10), Pt(90, 50, -10), Pt(80, 50, 10));

            Assert.False(inHole.CollidesWith(Plate(), Tol));
            Assert.Empty(inHole.GetIntersections(Plate(), Tol));
            Assert.True(onMaterial.CollidesWith(Plate(), Tol));
            Assert.Equal(inHole.CollidesWith(Plate(), Tol), Plate().CollidesWith(inHole, Tol));
        }

        [Fact]
        public void APolygonLyingInAPlaneTouchesItAndCrossesItNowhere()
        {
            GeoPolygon3 lying = Square(0, 0, 100, 100, 0);

            Assert.True(GeoPlane3.XY.CollidesWith(lying, Tol));
            Assert.Empty(GeoPlane3.XY.GetIntersections(lying, Tol));
            AssertPoints(GeoPlane3.XY.GetIntersections(Upright(0, 100, -50, 50), Tol), Pt(100, 50, 0), Pt(0, 50, 0));
            Assert.False(GeoPlane3.XY.CollidesWith(Upright(0, 100, 10, 110), Tol));
        }

        [Fact]
        public void APlaneBetweenTwoPiecesOfABodyTouchesNeither()
        {
            GeoSolid3 lower = new GeoAabb3(Pt(0, 0, 0), Pt(100, 100, 100)).ToObb().ToSolid();
            GeoSolid3 upper = new GeoAabb3(Pt(0, 0, 200), Pt(100, 100, 300)).ToObb().ToSolid();
            var twoPieces = new GeoSolid3(lower.Faces.Concat(upper.Faces));

            Assert.False(new GeoPlane3(Pt(0, 0, 150), GeoVector3.ZAxis).CollidesWith(twoPieces, Tol));
            Assert.True(new GeoPlane3(Pt(0, 0, 100), GeoVector3.ZAxis).CollidesWith(twoPieces, Tol));
            Assert.True(twoPieces.CollidesWith(new GeoPlane3(Pt(0, 0, 250), GeoVector3.ZAxis), Tol));
        }

        [Fact]
        public void TwoRaysCrossAtAPointAndAlongOneLineTouchWithoutNamingOne()
        {
            var alongX = new GeoRay3(GeoPoint3.Origin, GeoVector3.XAxis);
            var alongY = new GeoRay3(Pt(50, -50, 0), GeoVector3.YAxis);
            var facing = new GeoRay3(Pt(100, 0, 0), GeoVector3.XAxis.Negate());

            AssertPoints(alongX.GetIntersections(alongY, Tol), Pt(50, 0, 0));
            Assert.True(alongX.CollidesWith(facing, Tol));
            Assert.Empty(alongX.GetIntersections(facing, Tol));
            Assert.False(alongX.CollidesWith(new GeoRay3(Pt(-100, 0, 0), GeoVector3.XAxis.Negate()), Tol));
            AssertPoints(new GeoRay3(Pt(0, 0, 10), GeoVector3.ZAxis.Negate()).GetIntersections(GeoPlane3.XY, Tol), GeoPoint3.Origin);
        }

        [Fact]
        public void ATriangleInsideABodyTouchesItAndOneAcrossItCrossesItsFourUprightEdges()
        {
            GeoSolid3 box = new GeoAabb3(Pt(0, 0, 0), Pt(100, 100, 100)).ToObb().ToSolid();
            var inside = new GeoTriangle3(Pt(10, 10, 10), Pt(50, 10, 10), Pt(10, 50, 10));
            var across = new GeoTriangle3(Pt(-50, -50, 50), Pt(300, -50, 50), Pt(-50, 300, 50));

            Assert.True(box.CollidesWith(inside, Tol));
            Assert.Empty(box.GetIntersections(inside, Tol));

            GeoPoint3[] crossings = across.GetIntersections(box, Tol);

            Assert.Equal(4, crossings.Length);
            Assert.All(crossings, p => Assert.Equal(50.0, p.Z, 9));
        }

        [Fact]
        public void ABoxIsAnsweredAsItsBody()
        {
            var aabb = new GeoAabb3(Pt(0, 0, 0), Pt(100, 100, 100));
            GeoObb3 obb = aabb.ToObb();
            GeoPolygon3 across = Square(-50, -50, 150, 150, 50);

            Assert.Equal(across.GetIntersections(obb.ToSolid(), Tol), obb.GetIntersections(across, Tol));
            Assert.Equal(across.GetIntersections(obb.ToSolid(), Tol), across.GetIntersections(aabb, Tol));
            Assert.True(aabb.CollidesWith(GeoPlane3.XY, Tol));
            Assert.Throws<ArgumentNullException>("box", () => across.GetIntersections((GeoObb3)null, Tol));
        }

        [Fact]
        public void AnEdgeIsAskedAsWhicheverPieceItIs()
        {
            var straight = new GeoEdge3(Pt(0, -50, 0), Pt(0, 50, 0));
            var chain = new GeoPolyline3(Pt(-50, 0, 0), Pt(50, 0, 0), Pt(50, 100, 0));
            var bend = new GeoEdge3(GeoArc3.FromThreePoints(Pt(50, 0, 0), Pt(50 * Math.Sqrt(0.5), 50 * Math.Sqrt(0.5), 0), Pt(0, 50, 0)));
            var loop = new GeoPolygonArc3(Square(0, 0, 100, 100, 0));

            AssertPoints(straight.GetIntersections(chain, Tol), GeoPoint3.Origin);
            Assert.Equal(straight.ToLine().GetIntersections(chain, Tol), straight.GetIntersections(chain, Tol));
            Assert.Equal(bend.ToArc().GetIntersections(loop, Tol), loop.GetIntersections(bend, Tol));
            Assert.True(loop.CollidesWith(bend, Tol));
        }
    }
}
