using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Shapes;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// What the infinite line does where a shape is awkward: a body with an opening in it, an arc of a radius of a million, a
    /// line that is no number or has no direction, an argument that is missing, and a line that runs along what it is asked
    /// of. Every expectation is worked out by hand.
    /// </summary>
    public class XLine3EdgeCasesTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(P(x0, y0, z0), P(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>The plate 100 by 100 by 20 with the hole 20 by 20 at 40 to 60 through it, overshooting both faces by one.</summary>
        private static GeoSolid3 Plate() => Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });

        #region A body with an opening

        [Fact]
        public void ALineUpTheMiddleOfTheHole_TouchesNothingOfThePlate_AndIsTenFromItsWalls()
        {
            GeoSolid3 plate = Plate();
            var line = X(50, 50, -300, 0, 0, 1);

            Assert.False(line.CollidesWith(plate, Strict));
            Assert.False(plate.CollidesWith(line, Strict));
            Assert.Equal(10.0, line.DistanceTo(plate, Strict), 6);
            Assert.Equal(10.0, plate.DistanceTo(line, Strict), 6);
            Assert.Empty(line.GetIntersections(plate, Strict));
            Assert.Equal(10.0, line.GetShortestLineTo(plate, Strict).Length, 6);
        }

        [Fact]
        public void ALineUpTheMaterialBesideTheHole_CrossesThePlateTwice_AndOneAcrossTheHoleCrossesItFourTimes()
        {
            GeoSolid3 plate = Plate();

            GeoPoint3[] up = X(20, 20, -300, 0, 0, 1).GetIntersections(plate, Strict);

            Assert.Equal(2, up.Length);
            Assert.True(up[0].DistanceTo(P(20, 20, 0)) <= 1E-6 && up[1].DistanceTo(P(20, 20, 20)) <= 1E-6);

            // Along X at mid height through the hole: in at 0, out into the hole at 40, in again at 60, out at 100.
            GeoPoint3[] across = X(-100, 50, 10, 1, 0, 0).GetIntersections(plate, Strict);

            Assert.Equal(4, across.Length);
            Assert.Equal(new[] { 0.0, 40.0, 60.0, 100.0 }, across.Select(p => Math.Round(p.X, 6)).ToArray());
        }

        #endregion

        #region A very large arc

        [Fact]
        public void AnArcOfARadiusOfAMillion_IsCrossedWhereItPassesAndNotStretched()
        {
            // The arc over the top of the circle about (0, -1E6, 0), 100 long, crossing the plane x = 0 at the origin.
            double radius = 1E6;
            double half = 5E-5;
            GeoArc3 arc = ArcFrom(P(0, -radius, 0), GeoVector3.ZAxis, radius, P(radius * Math.Sin(half), radius * Math.Cos(half) - radius, 0), 2 * half);

            // A line up through the middle of the arc, along Z.
            GeoPoint3[] crossings = X(0, 0, -300, 0, 0, 1).GetIntersections(arc, Strict);

            Assert.Single(crossings);
            Assert.True(crossings[0].DistanceTo(P(0, 0, 0)) <= 1E-6, crossings[0].ToString());
            Assert.True(X(0, 0, -300, 0, 0, 1).CollidesWith(arc, Strict));
            Assert.True(arc.CollidesWith(X(0, 0, -300, 0, 0, 1), Strict));

            // And one 0.5 beside it along X, in the plane of the arc, does not touch it: it crosses y = 0 at x = 0.5 where the arc is 1E-7 off it
            // and so does touch it; at 60 along it is past its end.
            Assert.True(X(0.5, -300, 0, 0, 1, 0).CollidesWith(arc, Strict));
            Assert.False(X(60, -300, 0, 0, 1, 0).CollidesWith(arc, Strict));
        }

        #endregion

        #region No number, no direction, no argument

        [Fact]
        public void ALineWithNoDirection_IsOnlyItsOrigin_AndAskedWithoutThrowing()
        {
            GeoXLine3 none = default(GeoXLine3);

            Assert.Equal(0.0, none.DistanceTo(P(0, 0, 0)));
            Assert.Equal(5.0, none.DistanceTo(P(3, 4, 0)), 12);
            Assert.True(none.CollidesWith(Aabb(), Strict));
            Assert.Equal(none.DistanceTo(Aabb(), Strict), Aabb().DistanceTo(none, Strict));
        }

        [Fact]
        public void ALineThatIsNoNumber_IsNoNumberAwayFromAnything_AndCollidesWithNothing()
        {
            var nan = new GeoXLine3(P(double.NaN, 0, 0), GeoVector3.XAxis);

            Assert.True(double.IsNaN(nan.DistanceTo(P(1, 2, 3))));
            Assert.True(double.IsNaN(nan.DistanceTo(Floor())));
            Assert.False(nan.CollidesWith(Aabb(), Strict));
            Assert.False(nan.CollidesWith(SolidBox(), Strict));
            Assert.False(nan.CollidesWith(Square(), Strict));
            Assert.Empty(nan.GetIntersections(Aabb(), Strict));
            Assert.Empty(nan.GetIntersections(Square(), Strict));
            Assert.False(nan.TryIntersectWith(Square(), out _, Strict));
            Assert.False(nan.IsValid);
        }

        [Fact]
        public void AMissingArgument_IsRefusedByName_FromEitherSide()
        {
            var line = X(0, 0, 0, 1, 0, 0);

            Assert.Throws<ArgumentNullException>(() => line.DistanceTo((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => line.DistanceTo((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => line.DistanceTo((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => line.DistanceTo((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => line.DistanceTo((GeoObb3)null));
            Assert.Throws<ArgumentNullException>(() => line.CollidesWith((GeoPolylineArc3)null));
            Assert.Throws<ArgumentNullException>(() => line.CollidesWith((GeoPolygonArc3)null));
            Assert.Throws<ArgumentNullException>(() => line.GetIntersections((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => line.GetShortestLineTo((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => line.TryIntersectWith((GeoFace3)null, out GeoPoint3 _));
        }

        #endregion

        #region The order of the crossings

        [Fact]
        public void TheCrossingsOfAChainAreNamedInTheOrderOfItsEdges_AsASegmentsAre_AndThoseOfABodyInTheOrderTheLineRuns()
        {
            var u = new GeoPolyline3(P(0, 0, 0), P(0, 100, 0), P(100, 100, 0), P(100, 0, 0));
            var back = X(200, 50, 0, -1, 0, 0);
            var segment = new GeoLine3(P(200, 50, 0), P(-200, 50, 0));

            // The chain's first edge is the one at x = 0, whichever way the line runs, and the segment says the same.
            GeoPoint3[] byEdges = back.GetIntersections(u, Strict);

            Assert.Equal(new[] { 0.0, 100.0 }, byEdges.Select(p => Math.Round(p.X, 6)).ToArray());
            Assert.Equal(segment.GetIntersections(u, Strict), byEdges);

            // A body names them as the line meets them: from 200 along -X, the side at x = 100 and then the one at x = 0.
            GeoPoint3[] throughBody = X(200, 50, 10, -1, 0, 0).GetIntersections(SolidBox(), Strict);

            Assert.Equal(new[] { 100.0, 0.0 }, throughBody.Select(p => Math.Round(p.X, 6)).ToArray());
        }

        #endregion

        #region A line along what it is asked of

        [Fact]
        public void ALineAlongARayOfTheSameLine_TouchesItAndCrossesItNowhereInParticular()
        {
            var line = X(0, 0, 0, 1, 0, 0);
            var ray = new GeoRay3(P(100, 0, 0), V(1, 0, 0));

            Assert.True(line.CollidesWith(ray, Strict));
            Assert.Equal(0.0, line.DistanceTo(ray, Strict));
            Assert.Empty(line.GetIntersections(ray, Strict));
            Assert.False(ray.TryIntersectWith(line, out GeoPoint3[] none, Strict));
            Assert.Empty(none);
        }

        [Fact]
        public void ALineAlongASegmentOfItself_TouchesItAndCrossesItNowhereInParticular()
        {
            var line = X(0, 0, 0, 1, 0, 0);
            var segment = new GeoLine3(P(10, 0, 0), P(30, 0, 0));

            Assert.True(line.CollidesWith(segment, Strict));
            Assert.Equal(0.0, line.DistanceTo(segment, Strict), 12);
            Assert.Empty(line.GetIntersections(segment, Strict));
            Assert.Equal(0.0, line.GetShortestLineTo(segment, Strict).Length, 12);
        }

        [Fact]
        public void AFailedTry_LeavesAnEmptyArrayAndNotNull()
        {
            var line = X(0, 0, 500, 1, 0, 0);

            Assert.False(line.TryIntersectWith(Hook(), out GeoPoint3[] hook, Strict));
            Assert.NotNull(hook);
            Assert.Empty(hook);
            Assert.False(line.TryIntersectWith(Arc(), out GeoPoint3[] arc, Strict));
            Assert.NotNull(arc);
            Assert.Empty(arc);
            Assert.False(line.TryIntersectWith(Ell(), out GeoPoint3[] ell, Strict));
            Assert.NotNull(ell);
            Assert.Empty(ell);
        }

        #endregion
    }
}
