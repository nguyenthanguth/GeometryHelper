using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.XLine3Probe;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The corners of the matrix a spline in space is asked about: a shape of no length or no area, an end of the spline on the shape,
    /// a spline through a corner or an edge of a box, shapes a million from the origin, and the arguments a member refuses.
    /// </summary>
    public class Spline3EdgeCasesTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoSpline3 Line(GeoPoint3 a, GeoPoint3 b) => GeoSpline3.FromPolyline(new GeoPolyline3(a, b));

        /// <summary>The circle of radius 50 about the origin in the floor, from (50, 0, 0) counter-clockwise.</summary>
        private static GeoSpline3 Ring() => GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 50)).ToSpline3(GeoCoordinateSystem3.Global);

        [Fact]
        public void ASpline_AgainstASegmentOfNoLength_IsAgainstItsPoint()
        {
            GeoSpline3 ring = Ring();
            var on = new GeoLine3(P(50, 0, 0), P(50, 0, 0));
            var off = new GeoLine3(P(60, 0, 0), P(60, 0, 0));

            Assert.Equal(0.0, ring.DistanceTo(on, Strict), 9);
            Assert.Equal(10.0, ring.DistanceTo(off, Strict), 9);
            Assert.Equal(10.0, off.DistanceTo(ring, Strict), 9);
            Assert.True(ring.CollidesWith(on, Strict));
            Assert.False(ring.CollidesWith(off, Strict));
            Assert.Single(ring.GetIntersections(on, Strict));
            Assert.Empty(ring.GetIntersections(off, Strict));
        }

        [Fact]
        public void ASpline_AgainstATriangleWithNoArea_IsAgainstTheSegmentThatSpansIt()
        {
            GeoSpline3 ring = Ring();
            var thin = new GeoTriangle3(P(0, 0, 0), P(50, 0, 0), P(100, 0, 0));
            var beside = new GeoTriangle3(P(60, 0, 0), P(80, 0, 0), P(100, 0, 0));

            GeoPoint3[] through = ring.GetIntersections(thin, Strict);

            Assert.Equal(0.0, ring.DistanceTo(thin, Strict), 9);
            Assert.Single(through);
            Assert.True(through[0].DistanceTo(P(50, 0, 0)) <= 1E-6);
            Assert.Equal(10.0, ring.DistanceTo(beside, Strict), 9);
            Assert.Equal(ring.DistanceTo(thin, Strict), thin.DistanceTo(ring, Strict));
        }

        [Fact]
        public void APolylineWithRepeatedVertices_IsAsLongAsItsDistinctOnes()
        {
            GeoSpline3 ring = Ring();
            var plain = new GeoPolyline3(P(60, -100, 0), P(60, 100, 0));
            var repeated = new GeoPolyline3(P(60, -100, 0), P(60, -100, 0), P(60, 0, 0), P(60, 0, 0), P(60, 100, 0));

            Assert.Equal(ring.DistanceTo(plain, Strict), ring.DistanceTo(repeated, Strict), 9);
            Assert.Equal(10.0, ring.DistanceTo(repeated, Strict), 9);
        }

        [Fact]
        public void AnEndOfASplineOnAShape_IsAPlaceOfItsOwn()
        {
            GeoSpline3 arch = new GeoSpline3(3, new[] { P(0, 0, 0), P(100, 0, 150), P(200, 0, 150), P(300, 0, 0) }, new double[] { 0, 0, 0, 0, 1, 1, 1, 1 });

            // The floor meets the arch at its two ends and nowhere between, the plane z = -5 at neither, and a ball about the start at the start.
            GeoPoint3[] floor = arch.GetIntersections(GeoPlane3.XY, Strict);

            Assert.Equal(2, floor.Length);
            Assert.True(floor[0].DistanceTo(P(0, 0, 0)) <= 1E-9 && floor[1].DistanceTo(P(300, 0, 0)) <= 1E-9);
            Assert.Empty(arch.GetIntersections(new GeoPlane3(P(0, 0, -5), GeoVector3.ZAxis), Strict));
            Assert.Equal(5.0, arch.DistanceTo(new GeoPlane3(P(0, 0, -5), GeoVector3.ZAxis), Strict), 9);
            Assert.Single(arch.GetIntersections(new GeoSphere3(P(0, 0, 0), 20), Strict));
            Assert.Single(arch.GetIntersections(new GeoSphere3(P(-30, 0, 0), 30), Strict));
            Assert.Equal(0.0, arch.DistanceTo(new GeoSphere3(P(-30, 0, 0), 30), Strict), 9);
        }

        [Fact]
        public void ALineThroughACornerOfABox_MeetsItOnceThere_AndOneAlongAnEdgeNeverCrossesIt()
        {
            var box = new GeoAabb3(P(0, 0, 0), P(100, 100, 100));
            GeoSpline3 diagonal = Line(P(-10, -10, -10), P(110, 110, 110));
            GeoSpline3 along = Line(P(-10, 0, 0), P(110, 0, 0));
            GeoSpline3 grazing = Line(P(-10, -10, 50), P(110, 110, 50));

            GeoPoint3[] places = diagonal.GetIntersections(box, Strict);

            Assert.Equal(2, places.Length);
            Assert.True(places[0].DistanceTo(P(0, 0, 0)) <= 1E-6 && places[1].DistanceTo(P(100, 100, 100)) <= 1E-6);
            Assert.Equal(0.0, diagonal.DistanceTo(box, Strict), 9);

            // Along an edge: on the box's skin all the way and nought from it; where it lies along the skin it crosses nothing.
            Assert.Equal(0.0, along.DistanceTo(box, Strict), 9);
            Assert.All(along.GetIntersections(box, Strict), p => Assert.True(p.Y == 0.0 && p.Z == 0.0 || (Math.Abs(p.Y) <= 1E-9 && Math.Abs(p.Z) <= 1E-9)));

            // Across the middle of two faces it meets the edge between them.
            GeoPoint3[] edge = grazing.GetIntersections(box, Strict);

            Assert.Equal(2, edge.Length);
            Assert.True(edge[0].DistanceTo(P(0, 0, 50)) <= 1E-6 && edge[1].DistanceTo(P(100, 100, 50)) <= 1E-6);
        }

        [Fact]
        public void ARingAMillionFromTheOrigin_MeetsAPlaneAndABallAsAtTheOrigin()
        {
            var far = new GeoVector3(1E6, 1E6, 1E6);
            GeoSpline3 ring = Ring() + far;
            var plane = new GeoPlane3(P(1E6 + 30, 1E6, 1E6), GeoVector3.XAxis);
            var ball = new GeoSphere3(P(1E6 + 50, 1E6, 1E6), 10);

            GeoPoint3[] cut = ring.GetIntersections(plane, Strict);
            GeoPoint3[] pierced = ring.GetIntersections(ball, Strict);

            Assert.Equal(2, cut.Length);
            Assert.True(cut[0].DistanceTo(P(1E6 + 30, 1E6 + 40, 1E6)) <= 1E-6 && cut[1].DistanceTo(P(1E6 + 30, 1E6 - 40, 1E6)) <= 1E-6);
            Assert.Equal(2, pierced.Length);
            Assert.True(pierced[0].DistanceTo(P(1E6 + 49, 1E6 + Math.Sqrt(99), 1E6)) <= 1E-6);
            Assert.Equal(0.0, ring.DistanceTo(plane, Strict), 9);
            Assert.Equal(20.0, ring.DistanceTo(new GeoPlane3(P(1E6, 1E6, 1E6 + 20), GeoVector3.ZAxis), Strict), 6);
        }

        [Fact]
        public void ASplineThroughAVertexOfAPolygon_MeetsItThere_FromEitherSide()
        {
            var square = new GeoPolygon3(P(0, 0, 0), P(100, 0, 0), P(100, 100, 0), P(0, 100, 0));
            GeoSpline3 through = Line(P(100, 100, -10), P(100, 100, 10));
            GeoSpline3 pastEdge = Line(P(50, 0, -10), P(50, 0, 10));

            GeoPoint3[] atCorner = through.GetIntersections(square, Strict);
            GeoPoint3[] atEdge = pastEdge.GetIntersections(square, Strict);

            Assert.Single(atCorner);
            Assert.True(atCorner[0].DistanceTo(P(100, 100, 0)) <= 1E-6);
            Assert.Single(atEdge);
            Assert.True(atEdge[0].DistanceTo(P(50, 0, 0)) <= 1E-6);
            Assert.Equal(through.GetIntersections(square, Strict)[0], square.GetIntersections(through, Strict)[0]);
            Assert.Equal(0.0, through.DistanceTo(square, Strict), 9);
        }

        [Fact]
        public void ASplineInTheMiddleOfAnOpeningOfASolid_IsNotInItsMaterial()
        {
            GeoSolid3 plate = new GeoObb3(P(0, 0, 0), 100, 100, 10).ToSolid().WithOpenings(new[] { new GeoObb3(P(0, 0, 0), 20, 20, 12).ToSolid() });
            GeoSpline3 inside = Line(P(0, 0, -2), P(0, 0, 2));
            GeoSpline3 across = Line(P(30, 0, -2), P(30, 0, 2));

            Assert.Equal(10.0, inside.DistanceTo(plate, Strict), 9);
            Assert.False(inside.CollidesWith(plate, Strict));
            Assert.Empty(inside.GetIntersections(plate, Strict));
            Assert.Equal(0.0, across.DistanceTo(plate, Strict), 9);
            Assert.Equal(10.0, inside.GetShortestLineTo(plate, Strict).Length, 9);
        }

        [Fact]
        public void ACircleSpline_LiesAlongTheCircleAndTheArcOfItself_AndMeetsThemNowhere_AsInThePlane()
        {
            var circle = new GeoCircle3(P(10, 20, 30), new GeoVector3(1, 2, 2).Normalize(), 70);
            GeoSpline3 spline = GeoSpline3.FromCircle(circle);
            var arc = new GeoArc3(circle.Center, circle.Normal, circle.Radius, 0.5, 2.5);

            Assert.True(spline.DistanceTo(circle, Strict) <= 1E-9);
            Assert.Empty(spline.GetIntersections(circle, Strict));
            Assert.True(spline.DistanceTo(arc, Strict) <= 1E-9);

            // The spline and the arc lie in one plane, and are crossed as the plane crosses them: the spline runs along the circle the arc
            // is of, past the arc's ends as well, and meets it nowhere, as the circle spline of the plane meets an arc of its own circle.
            Assert.Empty(spline.GetIntersections(arc, Strict));
            Assert.Empty(GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(10, 20), 70)).GetIntersections(new GeoArc2(new GeoPoint2(10, 20), 70, 0.5, 2.5), Strict));
            Assert.True(spline.CollidesWith(arc, Strict));
            Assert.Equal(spline.DistanceTo(arc, Strict), arc.DistanceTo(spline, Strict));
        }

        [Fact]
        public void ASplineAgainstAShapeBothWaysRound_IsRefusedForNothingButNull()
        {
            GeoSpline3 ring = Ring();
            Type[] classes = { typeof(GeoPolyline3), typeof(GeoPolygon3), typeof(GeoFace3), typeof(GeoObb3), typeof(GeoSolid3), typeof(GeoPolylineArc3), typeof(GeoPolygonArc3) };

            Assert.Throws<ArgumentNullException>(() => ring.DistanceTo((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => ring.GetIntersections((GeoSolid3)null, Strict));
            Assert.Throws<ArgumentNullException>(() => ring.GetShortestLineTo((GeoObb3)null));
            Assert.Throws<ArgumentNullException>(() => ring.CollidesWith((GeoFace3)null));
            Assert.Equal(7, classes.Length);
        }
    }
}
