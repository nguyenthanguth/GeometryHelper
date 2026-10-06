using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The ways of asking an ellipse about another shape, held against each other, as <see cref="PlaneMirrorTests"/> and
    /// <see cref="Ellipse2MirrorTests"/> hold theirs. Every query lives in <see cref="Ellipse2"/>, the ellipse first, and
    /// GeoEllipse2's members hand it on in one line; each comes with a form taking a tolerance and one reading
    /// Tolerance.Global. A member wired to the wrong static, or an overload that drops the tolerance it was handed, gives an
    /// answer that is plausible and wrong. The four forms are one computation, so they are compared exactly. The shapes are
    /// those of Ellipse2Oracle's "Shapes a gap above the top": 0.0004 above, which touches at the global tolerance of a
    /// thousandth, where a form reading some other tolerance would part company, and 0.5 above, which touches at a
    /// tolerance of 1 and not at a thousandth.
    /// </summary>
    public class Ellipse2QueryMirrorTests
    {
        private static readonly Tolerance Global = Tolerance.Global;

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoEllipse2 Tilted() => Ellipse2Oracle.Tilted();

        [Fact]
        public void AgainstALine_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoLine2 x = Ellipse2Oracle.LineAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s1, Global), e.TryIntersectWith(x, out GeoPoint2[] m1, Global));
                Assert.Equal(s1, m1);
                Assert.Equal(e.TryIntersectWith(x, out GeoPoint2[] m2), e.TryIntersectWith(x, out GeoPoint2[] m3, Global));
                Assert.Equal(m3, m2);
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s2), e.TryIntersectWith(x, out GeoPoint2[] m4));
                Assert.Equal(s2, m4);
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
            }
        }

        [Fact]
        public void AgainstAPolyline_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoPolyline2 x = Ellipse2Oracle.PolylineAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x, Global), e.GetClosestEdge(x, Global));
                Assert.Equal(e.GetClosestEdge(x, Global), e.GetClosestEdge(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x), e.GetClosestEdge(x));
            }
        }

        [Fact]
        public void AgainstAPolygon_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoPolygon2 x = Ellipse2Oracle.PolygonAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x, Global), e.GetClosestEdge(x, Global));
                Assert.Equal(e.GetClosestEdge(x, Global), e.GetClosestEdge(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x), e.GetClosestEdge(x));
            }
        }

        [Fact]
        public void AgainstARectangle_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoRectangle2 x = Ellipse2Oracle.RectangleAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x, Global), e.GetClosestEdge(x, Global));
                Assert.Equal(e.GetClosestEdge(x, Global), e.GetClosestEdge(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x), e.GetClosestEdge(x));
            }
        }

        [Fact]
        public void AgainstATriangle_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoTriangle2 x = Ellipse2Oracle.TriangleAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x, Global), e.GetClosestEdge(x, Global));
                Assert.Equal(e.GetClosestEdge(x, Global), e.GetClosestEdge(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x), e.GetClosestEdge(x));
            }
        }

        [Fact]
        public void AgainstAFace_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoFace2 x = new GeoFace2(Ellipse2Oracle.PolygonAbove(e, gap));

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
            }
        }

        [Fact]
        public void AgainstAnEdge_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoEdge2 x = new GeoEdge2(Ellipse2Oracle.ArcAbove(e, gap));

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s1, Global), e.TryIntersectWith(x, out GeoPoint2[] m1, Global));
                Assert.Equal(s1, m1);
                Assert.Equal(e.TryIntersectWith(x, out GeoPoint2[] m2), e.TryIntersectWith(x, out GeoPoint2[] m3, Global));
                Assert.Equal(m3, m2);
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s2), e.TryIntersectWith(x, out GeoPoint2[] m4));
                Assert.Equal(s2, m4);
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
            }
        }

        [Fact]
        public void AgainstACircle_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoCircle2 x = Ellipse2Oracle.CircleAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s1, Global), e.TryIntersectWith(x, out GeoPoint2[] m1, Global));
                Assert.Equal(s1, m1);
                Assert.Equal(e.TryIntersectWith(x, out GeoPoint2[] m2), e.TryIntersectWith(x, out GeoPoint2[] m3, Global));
                Assert.Equal(m3, m2);
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s2), e.TryIntersectWith(x, out GeoPoint2[] m4));
                Assert.Equal(s2, m4);
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
            }
        }

        [Fact]
        public void AgainstAnArc_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoArc2 x = Ellipse2Oracle.ArcAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s1, Global), e.TryIntersectWith(x, out GeoPoint2[] m1, Global));
                Assert.Equal(s1, m1);
                Assert.Equal(e.TryIntersectWith(x, out GeoPoint2[] m2), e.TryIntersectWith(x, out GeoPoint2[] m3, Global));
                Assert.Equal(m3, m2);
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s2), e.TryIntersectWith(x, out GeoPoint2[] m4));
                Assert.Equal(s2, m4);
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
            }
        }

        [Fact]
        public void AgainstAPolylineArc_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoPolylineArc2 x = Ellipse2Oracle.ChainAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s1, Global), e.TryIntersectWith(x, out GeoPoint2[] m1, Global));
                Assert.Equal(s1, m1);
                Assert.Equal(e.TryIntersectWith(x, out GeoPoint2[] m2), e.TryIntersectWith(x, out GeoPoint2[] m3, Global));
                Assert.Equal(m3, m2);
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s2), e.TryIntersectWith(x, out GeoPoint2[] m4));
                Assert.Equal(s2, m4);
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x, Global), e.GetClosestEdge(x, Global));
                Assert.Equal(e.GetClosestEdge(x, Global), e.GetClosestEdge(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x), e.GetClosestEdge(x));
            }
        }

        [Fact]
        public void AgainstAPolygonArc_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoPolygonArc2 x = Ellipse2Oracle.LoopAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s1, Global), e.TryIntersectWith(x, out GeoPoint2[] m1, Global));
                Assert.Equal(s1, m1);
                Assert.Equal(e.TryIntersectWith(x, out GeoPoint2[] m2), e.TryIntersectWith(x, out GeoPoint2[] m3, Global));
                Assert.Equal(m3, m2);
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s2), e.TryIntersectWith(x, out GeoPoint2[] m4));
                Assert.Equal(s2, m4);
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x, Global), e.GetClosestEdge(x, Global));
                Assert.Equal(e.GetClosestEdge(x, Global), e.GetClosestEdge(x));
                Assert.Equal(Ellipse2.GetClosestEdge(e, x), e.GetClosestEdge(x));
            }
        }

        [Fact]
        public void AgainstAnEllipse_EveryQueryAnswersTheSameWhicheverWayItIsAsked()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoEllipse2 x = Ellipse2Oracle.EllipseAbove(e, gap);

                Assert.Equal(Ellipse2.CollidesWith(e, x, Global), e.CollidesWith(x, Global));
                Assert.Equal(e.CollidesWith(x, Global), e.CollidesWith(x));
                Assert.Equal(Ellipse2.CollidesWith(e, x), e.CollidesWith(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x, Global), e.DistanceTo(x, Global));
                Assert.Equal(e.DistanceTo(x, Global), e.DistanceTo(x));
                Assert.Equal(Ellipse2.DistanceTo(e, x), e.DistanceTo(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x, Global), e.GetIntersections(x, Global));
                Assert.Equal(e.GetIntersections(x, Global), e.GetIntersections(x));
                Assert.Equal(Ellipse2.GetIntersections(e, x), e.GetIntersections(x));
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s1, Global), e.TryIntersectWith(x, out GeoPoint2[] m1, Global));
                Assert.Equal(s1, m1);
                Assert.Equal(e.TryIntersectWith(x, out GeoPoint2[] m2), e.TryIntersectWith(x, out GeoPoint2[] m3, Global));
                Assert.Equal(m3, m2);
                Assert.Equal(Ellipse2.TryIntersectWith(e, x, out GeoPoint2[] s2), e.TryIntersectWith(x, out GeoPoint2[] m4));
                Assert.Equal(s2, m4);
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x, Global), e.GetShortestLineTo(x, Global));
                Assert.Equal(e.GetShortestLineTo(x, Global), e.GetShortestLineTo(x));
                Assert.Equal(Ellipse2.GetShortestLineTo(e, x), e.GetShortestLineTo(x));
            }
        }

        [Fact]
        public void APointAndTheRegionShapesAreAskedTheSameWhicheverWay()
        {
            GeoEllipse2 e = Tilted();

            foreach (double gap in new[] { 0.0004, -20.0, 30.0 })
            {
                GeoPoint2 point = Ellipse2Oracle.OffRim(e, 1.1, gap);
                GeoLine2 line = new GeoLine2(e.Center, point);
                GeoCircle2 circle = new GeoCircle2(Ellipse2Oracle.OffRim(e, 1.1, gap - 20.0), 20.0);
                GeoEllipse2 other = Ellipse2Oracle.FrameEllipse(e, 0, 50 - gap, 0, 60, 50);

                Assert.Equal(Ellipse2.DistanceTo(e, point, Global), e.DistanceTo(point, Global));
                Assert.Equal(e.DistanceTo(point, Global), e.DistanceTo(point));
                Assert.Equal(Ellipse2.DistanceTo(e, point), e.DistanceTo(point));
                Assert.Equal(Ellipse2.SignedDistanceTo(e, point, Global), e.SignedDistanceTo(point, Global));
                Assert.Equal(e.SignedDistanceTo(point, Global), e.SignedDistanceTo(point));
                Assert.Equal(Ellipse2.SignedDistanceTo(e, point), e.SignedDistanceTo(point));

                Assert.Equal(Ellipse2.Contains(e, line, Global), e.Contains(line, Global));
                Assert.Equal(e.Contains(line, Global), e.Contains(line));
                Assert.Equal(Ellipse2.Contains(e, line), e.Contains(line));
                Assert.Equal(Ellipse2.Contains(e, circle, Global), e.Contains(circle, Global));
                Assert.Equal(e.Contains(circle, Global), e.Contains(circle));
                Assert.Equal(Ellipse2.Contains(e, circle), e.Contains(circle));
                Assert.Equal(Ellipse2.Contains(e, other, Global), e.Contains(other, Global));
                Assert.Equal(e.Contains(other, Global), e.Contains(other));
                Assert.Equal(Ellipse2.Contains(e, other), e.Contains(other));
            }
        }

        [Fact]
        public void TheToleranceHandedInIsTheOneRead_ForTouching()
        {
            // Half a unit above the top: touching within a whole unit, apart within a thousandth, through the static and the
            // member alike.
            GeoEllipse2 e = Tilted();
            const double gap = 0.5;

            GeoLine2 line = Ellipse2Oracle.LineAbove(e, gap);
            Assert.True(e.CollidesWith(line, Loose));
            Assert.True(Ellipse2.CollidesWith(e, line, Loose));
            Assert.False(e.CollidesWith(line, Strict));
            Assert.False(Ellipse2.CollidesWith(e, line, Strict));

            GeoPolyline2 polyline = Ellipse2Oracle.PolylineAbove(e, gap);
            Assert.True(e.CollidesWith(polyline, Loose));
            Assert.True(Ellipse2.CollidesWith(e, polyline, Loose));
            Assert.False(e.CollidesWith(polyline, Strict));
            Assert.False(Ellipse2.CollidesWith(e, polyline, Strict));

            GeoPolygon2 polygon = Ellipse2Oracle.PolygonAbove(e, gap);
            Assert.True(e.CollidesWith(polygon, Loose));
            Assert.True(Ellipse2.CollidesWith(e, polygon, Loose));
            Assert.False(e.CollidesWith(polygon, Strict));
            Assert.False(Ellipse2.CollidesWith(e, polygon, Strict));

            GeoRectangle2 rectangle = Ellipse2Oracle.RectangleAbove(e, gap);
            Assert.True(e.CollidesWith(rectangle, Loose));
            Assert.True(Ellipse2.CollidesWith(e, rectangle, Loose));
            Assert.False(e.CollidesWith(rectangle, Strict));
            Assert.False(Ellipse2.CollidesWith(e, rectangle, Strict));

            GeoTriangle2 triangle = Ellipse2Oracle.TriangleAbove(e, gap);
            Assert.True(e.CollidesWith(triangle, Loose));
            Assert.True(Ellipse2.CollidesWith(e, triangle, Loose));
            Assert.False(e.CollidesWith(triangle, Strict));
            Assert.False(Ellipse2.CollidesWith(e, triangle, Strict));

            GeoFace2 face = new GeoFace2(Ellipse2Oracle.PolygonAbove(e, gap));
            Assert.True(e.CollidesWith(face, Loose));
            Assert.True(Ellipse2.CollidesWith(e, face, Loose));
            Assert.False(e.CollidesWith(face, Strict));
            Assert.False(Ellipse2.CollidesWith(e, face, Strict));

            GeoEdge2 edge = new GeoEdge2(Ellipse2Oracle.ArcAbove(e, gap));
            Assert.True(e.CollidesWith(edge, Loose));
            Assert.True(Ellipse2.CollidesWith(e, edge, Loose));
            Assert.False(e.CollidesWith(edge, Strict));
            Assert.False(Ellipse2.CollidesWith(e, edge, Strict));

            GeoCircle2 circle = Ellipse2Oracle.CircleAbove(e, gap);
            Assert.True(e.CollidesWith(circle, Loose));
            Assert.True(Ellipse2.CollidesWith(e, circle, Loose));
            Assert.False(e.CollidesWith(circle, Strict));
            Assert.False(Ellipse2.CollidesWith(e, circle, Strict));

            GeoArc2 arc = Ellipse2Oracle.ArcAbove(e, gap);
            Assert.True(e.CollidesWith(arc, Loose));
            Assert.True(Ellipse2.CollidesWith(e, arc, Loose));
            Assert.False(e.CollidesWith(arc, Strict));
            Assert.False(Ellipse2.CollidesWith(e, arc, Strict));

            GeoPolylineArc2 polylineArc = Ellipse2Oracle.ChainAbove(e, gap);
            Assert.True(e.CollidesWith(polylineArc, Loose));
            Assert.True(Ellipse2.CollidesWith(e, polylineArc, Loose));
            Assert.False(e.CollidesWith(polylineArc, Strict));
            Assert.False(Ellipse2.CollidesWith(e, polylineArc, Strict));

            GeoPolygonArc2 polygonArc = Ellipse2Oracle.LoopAbove(e, gap);
            Assert.True(e.CollidesWith(polygonArc, Loose));
            Assert.True(Ellipse2.CollidesWith(e, polygonArc, Loose));
            Assert.False(e.CollidesWith(polygonArc, Strict));
            Assert.False(Ellipse2.CollidesWith(e, polygonArc, Strict));

            GeoEllipse2 ellipse = Ellipse2Oracle.EllipseAbove(e, gap);
            Assert.True(e.CollidesWith(ellipse, Loose));
            Assert.True(Ellipse2.CollidesWith(e, ellipse, Loose));
            Assert.False(e.CollidesWith(ellipse, Strict));
            Assert.False(Ellipse2.CollidesWith(e, ellipse, Strict));

        }

        [Fact]
        public void TheToleranceHandedInIsTheOneRead_ForCrossings()
        {
            // Within a whole unit a shape half a unit above touches the rim at a point; within a thousandth it meets it
            // nowhere.
            GeoEllipse2 e = Tilted();
            const double gap = 0.5;

            GeoLine2 line = Ellipse2Oracle.LineAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(line, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, line, Loose));
            Assert.Empty(e.GetIntersections(line, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, line, Strict));

            Assert.True(e.TryIntersectWith(line, out _, Loose));
            Assert.True(Ellipse2.TryIntersectWith(e, line, out _, Loose));
            Assert.False(e.TryIntersectWith(line, out _, Strict));
            Assert.False(Ellipse2.TryIntersectWith(e, line, out _, Strict));

            GeoPolyline2 polyline = Ellipse2Oracle.PolylineAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(polyline, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, polyline, Loose));
            Assert.Empty(e.GetIntersections(polyline, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, polyline, Strict));

            GeoPolygon2 polygon = Ellipse2Oracle.PolygonAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(polygon, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, polygon, Loose));
            Assert.Empty(e.GetIntersections(polygon, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, polygon, Strict));

            GeoRectangle2 rectangle = Ellipse2Oracle.RectangleAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(rectangle, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, rectangle, Loose));
            Assert.Empty(e.GetIntersections(rectangle, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, rectangle, Strict));

            GeoTriangle2 triangle = Ellipse2Oracle.TriangleAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(triangle, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, triangle, Loose));
            Assert.Empty(e.GetIntersections(triangle, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, triangle, Strict));

            GeoFace2 face = new GeoFace2(Ellipse2Oracle.PolygonAbove(e, gap));
            Assert.NotEmpty(e.GetIntersections(face, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, face, Loose));
            Assert.Empty(e.GetIntersections(face, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, face, Strict));

            GeoEdge2 edge = new GeoEdge2(Ellipse2Oracle.ArcAbove(e, gap));
            Assert.NotEmpty(e.GetIntersections(edge, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, edge, Loose));
            Assert.Empty(e.GetIntersections(edge, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, edge, Strict));

            Assert.True(e.TryIntersectWith(edge, out _, Loose));
            Assert.True(Ellipse2.TryIntersectWith(e, edge, out _, Loose));
            Assert.False(e.TryIntersectWith(edge, out _, Strict));
            Assert.False(Ellipse2.TryIntersectWith(e, edge, out _, Strict));

            GeoCircle2 circle = Ellipse2Oracle.CircleAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(circle, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, circle, Loose));
            Assert.Empty(e.GetIntersections(circle, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, circle, Strict));

            Assert.True(e.TryIntersectWith(circle, out _, Loose));
            Assert.True(Ellipse2.TryIntersectWith(e, circle, out _, Loose));
            Assert.False(e.TryIntersectWith(circle, out _, Strict));
            Assert.False(Ellipse2.TryIntersectWith(e, circle, out _, Strict));

            GeoArc2 arc = Ellipse2Oracle.ArcAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(arc, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, arc, Loose));
            Assert.Empty(e.GetIntersections(arc, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, arc, Strict));

            Assert.True(e.TryIntersectWith(arc, out _, Loose));
            Assert.True(Ellipse2.TryIntersectWith(e, arc, out _, Loose));
            Assert.False(e.TryIntersectWith(arc, out _, Strict));
            Assert.False(Ellipse2.TryIntersectWith(e, arc, out _, Strict));

            GeoPolylineArc2 polylineArc = Ellipse2Oracle.ChainAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(polylineArc, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, polylineArc, Loose));
            Assert.Empty(e.GetIntersections(polylineArc, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, polylineArc, Strict));

            Assert.True(e.TryIntersectWith(polylineArc, out _, Loose));
            Assert.True(Ellipse2.TryIntersectWith(e, polylineArc, out _, Loose));
            Assert.False(e.TryIntersectWith(polylineArc, out _, Strict));
            Assert.False(Ellipse2.TryIntersectWith(e, polylineArc, out _, Strict));

            GeoPolygonArc2 polygonArc = Ellipse2Oracle.LoopAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(polygonArc, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, polygonArc, Loose));
            Assert.Empty(e.GetIntersections(polygonArc, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, polygonArc, Strict));

            Assert.True(e.TryIntersectWith(polygonArc, out _, Loose));
            Assert.True(Ellipse2.TryIntersectWith(e, polygonArc, out _, Loose));
            Assert.False(e.TryIntersectWith(polygonArc, out _, Strict));
            Assert.False(Ellipse2.TryIntersectWith(e, polygonArc, out _, Strict));

            GeoEllipse2 ellipse = Ellipse2Oracle.EllipseAbove(e, gap);
            Assert.NotEmpty(e.GetIntersections(ellipse, Loose));
            Assert.NotEmpty(Ellipse2.GetIntersections(e, ellipse, Loose));
            Assert.Empty(e.GetIntersections(ellipse, Strict));
            Assert.Empty(Ellipse2.GetIntersections(e, ellipse, Strict));

            Assert.True(e.TryIntersectWith(ellipse, out _, Loose));
            Assert.True(Ellipse2.TryIntersectWith(e, ellipse, out _, Loose));
            Assert.False(e.TryIntersectWith(ellipse, out _, Strict));
            Assert.False(Ellipse2.TryIntersectWith(e, ellipse, out _, Strict));

        }

        [Fact]
        public void TheToleranceHandedInIsTheOneRead_ForShortestLines()
        {
            // Within a whole unit the touch is a segment of no length; within a thousandth the segment spans the half unit.
            GeoEllipse2 e = Tilted();
            const double gap = 0.5;

            GeoLine2 line = Ellipse2Oracle.LineAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(line, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, line, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(line, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, line, Strict).Length, 9);

            GeoPolyline2 polyline = Ellipse2Oracle.PolylineAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(polyline, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, polyline, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(polyline, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, polyline, Strict).Length, 9);

            GeoPolygon2 polygon = Ellipse2Oracle.PolygonAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(polygon, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, polygon, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(polygon, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, polygon, Strict).Length, 9);

            GeoRectangle2 rectangle = Ellipse2Oracle.RectangleAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(rectangle, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, rectangle, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(rectangle, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, rectangle, Strict).Length, 9);

            GeoTriangle2 triangle = Ellipse2Oracle.TriangleAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(triangle, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, triangle, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(triangle, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, triangle, Strict).Length, 9);

            GeoFace2 face = new GeoFace2(Ellipse2Oracle.PolygonAbove(e, gap));
            Assert.Equal(0.0, e.GetShortestLineTo(face, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, face, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(face, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, face, Strict).Length, 9);

            GeoEdge2 edge = new GeoEdge2(Ellipse2Oracle.ArcAbove(e, gap));
            Assert.Equal(0.0, e.GetShortestLineTo(edge, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, edge, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(edge, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, edge, Strict).Length, 9);

            GeoCircle2 circle = Ellipse2Oracle.CircleAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(circle, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, circle, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(circle, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, circle, Strict).Length, 9);

            GeoArc2 arc = Ellipse2Oracle.ArcAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(arc, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, arc, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(arc, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, arc, Strict).Length, 9);

            GeoPolylineArc2 polylineArc = Ellipse2Oracle.ChainAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(polylineArc, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, polylineArc, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(polylineArc, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, polylineArc, Strict).Length, 9);

            GeoPolygonArc2 polygonArc = Ellipse2Oracle.LoopAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(polygonArc, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, polygonArc, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(polygonArc, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, polygonArc, Strict).Length, 9);

            GeoEllipse2 ellipse = Ellipse2Oracle.EllipseAbove(e, gap);
            Assert.Equal(0.0, e.GetShortestLineTo(ellipse, Loose).Length);
            Assert.Equal(0.0, Ellipse2.GetShortestLineTo(e, ellipse, Loose).Length);
            Assert.Equal(0.5, e.GetShortestLineTo(ellipse, Strict).Length, 9);
            Assert.Equal(0.5, Ellipse2.GetShortestLineTo(e, ellipse, Strict).Length, 9);

        }

        [Fact]
        public void TheDistancesToAPointASegmentACircleAndAnEllipse_ReadNoTolerance()
        {
            // Half a unit above the top, each is half a unit away within a whole unit as within a thousandth: these
            // distances are exact, as the circle's are (Distance2.cs:94-121; the coder's rule, accepted by the lead).
            GeoEllipse2 e = Tilted();
            const double gap = 0.5;
            GeoPoint2 point = Ellipse2Oracle.FromFrame(e, 0, e.MinorRadius + gap);

            foreach (Tolerance tolerance in new[] { Loose, Strict })
            {
                Assert.Equal(0.5, e.DistanceTo(point, tolerance), 9);
                Assert.Equal(0.5, e.DistanceTo(Ellipse2Oracle.LineAbove(e, gap), tolerance), 9);
                Assert.Equal(0.5, e.DistanceTo(Ellipse2Oracle.CircleAbove(e, gap), tolerance), 9);
                Assert.Equal(0.5, e.DistanceTo(Ellipse2Oracle.EllipseAbove(e, gap), tolerance), 9);
                Assert.Equal(0.5, Ellipse2.DistanceTo(e, Ellipse2Oracle.EllipseAbove(e, gap), tolerance), 9);
            }
        }

        [Fact]
        public void TheToleranceHandedInIsTheOneRead_ForAPointAndForHolding()
        {
            // Half a unit out along the normal at t = 1.1: on the rim within a unit, signed nought; outside within a
            // thousandth. A segment ending there, and a circle of radius 20 overhanging the rim by it, are held within a unit
            // and not within a thousandth.
            GeoEllipse2 e = Tilted();
            GeoPoint2 point = Ellipse2Oracle.OffRim(e, 1.1, 0.5);
            var line = new GeoLine2(e.Center, point);
            var circle = new GeoCircle2(Ellipse2Oracle.OffRim(e, 1.1, -19.5), 20.0);

            Assert.Equal(0.0, e.SignedDistanceTo(point, Loose));
            Assert.Equal(0.0, Ellipse2.SignedDistanceTo(e, point, Loose));
            Assert.Equal(0.5, e.SignedDistanceTo(point, Strict), 9);
            Assert.Equal(0.5, Ellipse2.SignedDistanceTo(e, point, Strict), 9);

            Assert.True(e.Contains(line, Loose));
            Assert.True(Ellipse2.Contains(e, line, Loose));
            Assert.False(e.Contains(line, Strict));
            Assert.False(Ellipse2.Contains(e, line, Strict));

            Assert.True(e.Contains(circle, Loose));
            Assert.True(Ellipse2.Contains(e, circle, Loose));
            Assert.False(e.Contains(circle, Strict));
            Assert.False(Ellipse2.Contains(e, circle, Strict));
        }
    }
}
