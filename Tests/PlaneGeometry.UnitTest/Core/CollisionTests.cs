using System;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest.Core
{
    public class CollisionTests
    {
        #region Line - Line

        [Fact]
        public void LineLine_CollidesWith_ComprehensiveCases()
        {
            var l1 = new GeoLine2(new GeoPoint2(0, 5), new GeoPoint2(10, 5));

            // Crossing lines
            var lCross = new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 10));
            Assert.True(Collision2.CollidesWith(l1, lCross));

            // Touching at endpoint (T-junction)
            var lTouch = new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(5, 15));
            Assert.True(Collision2.CollidesWith(l1, lTouch));

            // Collinear overlapping
            var lOverlap = new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(15, 5));
            Assert.True(Collision2.CollidesWith(l1, lOverlap));

            // Parallel2 disjoint
            var lParallel = new GeoLine2(new GeoPoint2(0, 10), new GeoPoint2(10, 10));
            Assert.False(Collision2.CollidesWith(l1, lParallel));

            // Skew disjoint
            var lSkew = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(3, 3));
            Assert.False(Collision2.CollidesWith(l1, lSkew));
        }

        #endregion

        #region Circle - Shapes

        [Fact]
        public void CircleCircle_CollidesWith_ComprehensiveCases()
        {
            var c1 = new GeoCircle2(new GeoPoint2(0, 0), 10);

            // Overlapping
            var cOverlap = new GeoCircle2(new GeoPoint2(10, 0), 5);
            Assert.True(Collision2.CollidesWith(c1, cOverlap));

            // Tangent externally
            var cTangentExt = new GeoCircle2(new GeoPoint2(15, 0), 5);
            Assert.True(Collision2.CollidesWith(c1, cTangentExt));

            // Tangent internally
            var cTangentInt = new GeoCircle2(new GeoPoint2(5, 0), 5);
            Assert.True(Collision2.CollidesWith(c1, cTangentInt));

            // Concentric inside
            var cConcentric = new GeoCircle2(new GeoPoint2(0, 0), 3);
            Assert.True(Collision2.CollidesWith(c1, cConcentric));

            // Disjoint
            var cDisjoint = new GeoCircle2(new GeoPoint2(20, 0), 4);
            Assert.False(Collision2.CollidesWith(c1, cDisjoint));
        }

        [Fact]
        public void CircleLine_CollidesWith_ComprehensiveCases()
        {
            var circle = new GeoCircle2(new GeoPoint2(0, 0), 10);

            // Secant (crosses through)
            var lSecant = new GeoLine2(new GeoPoint2(-15, 0), new GeoPoint2(15, 0));
            Assert.True(Collision2.CollidesWith(circle, lSecant));

            // Tangent
            var lTangent = new GeoLine2(new GeoPoint2(-10, 10), new GeoPoint2(10, 10));
            Assert.True(Collision2.CollidesWith(circle, lTangent));

            // Line strictly inside circle
            var lInside = new GeoLine2(new GeoPoint2(-3, 0), new GeoPoint2(3, 0));
            Assert.True(Collision2.CollidesWith(circle, lInside));

            // Disjoint line
            var lDisjoint = new GeoLine2(new GeoPoint2(-10, 15), new GeoPoint2(10, 15));
            Assert.False(Collision2.CollidesWith(circle, lDisjoint));
        }

        [Fact]
        public void CircleRectangle_CollidesWith_ComprehensiveCases()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 20, 20, 0);

            // Center inside rectangle
            var cInside = new GeoCircle2(new GeoPoint2(0, 0), 5);
            Assert.True(Collision2.CollidesWith(cInside, rect));

            // Circle overlapping edge
            var cOverlap = new GeoCircle2(new GeoPoint2(12, 0), 5);
            Assert.True(Collision2.CollidesWith(cOverlap, rect));

            // Circle touching corner (10, 10): center at (13, 14), dist to (10, 10) = 5
            var cCorner = new GeoCircle2(new GeoPoint2(13, 14), 5);
            Assert.True(Collision2.CollidesWith(cCorner, rect));

            // Disjoint circle
            var cDisjoint = new GeoCircle2(new GeoPoint2(25, 25), 5);
            Assert.False(Collision2.CollidesWith(cDisjoint, rect));
        }

        [Fact]
        public void CirclePolygon_CollidesWith_ComprehensiveCases()
        {
            var poly = new GeoPolygon2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(20, 0),
                new GeoPoint2(20, 20),
                new GeoPoint2(0, 20)
            });

            // Center inside polygon
            var cInside = new GeoCircle2(new GeoPoint2(10, 10), 3);
            Assert.True(Collision2.CollidesWith(cInside, poly));

            // Intersecting edge
            var cIntersect = new GeoCircle2(new GeoPoint2(20, 10), 3);
            Assert.True(Collision2.CollidesWith(cIntersect, poly));

            // Disjoint
            var cDisjoint = new GeoCircle2(new GeoPoint2(30, 30), 3);
            Assert.False(Collision2.CollidesWith(cDisjoint, poly));
        }

        [Fact]
        public void CirclePolyline_CollidesWith_ComprehensiveCases()
        {
            var pl = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10)
            });

            // Circle crossing first segment
            var cCross = new GeoCircle2(new GeoPoint2(5, 0), 3);
            Assert.True(Collision2.CollidesWith(cCross, pl));

            // Circle touching bend vertex (10, 0)
            var cTouchVertex = new GeoCircle2(new GeoPoint2(13, 4), 5);
            Assert.True(Collision2.CollidesWith(cTouchVertex, pl));

            // Disjoint circle
            var cDisjoint = new GeoCircle2(new GeoPoint2(0, 20), 3);
            Assert.False(Collision2.CollidesWith(cDisjoint, pl));
        }

        #endregion

        #region Rectangle - Shapes (SAT)

        [Fact]
        public void RectangleRectangle_CollidesWith_SATRotatedCases()
        {
            var r1 = new GeoRectangle2(new GeoPoint2(0, 0), 10, 10, 0);

            // Aligned overlap
            var rAligned = new GeoRectangle2(new GeoPoint2(5, 5), 10, 10, 0);
            Assert.True(Collision2.CollidesWith(r1, rAligned));

            // Rotated 45 degrees intersecting
            var rRotated = new GeoRectangle2(new GeoPoint2(7, 0), 5, 5, Math.PI / 4.0);
            Assert.True(Collision2.CollidesWith(r1, rRotated));

            // Edge-touching
            var rEdgeTouch = new GeoRectangle2(new GeoPoint2(10, 0), 10, 10, 0);
            Assert.True(Collision2.CollidesWith(r1, rEdgeTouch));

            // Disjoint
            var rDisjoint = new GeoRectangle2(new GeoPoint2(20, 20), 10, 10, 0);
            Assert.False(Collision2.CollidesWith(r1, rDisjoint));
        }

        [Fact]
        public void RectangleLine_CollidesWith_ComprehensiveCases()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 20, 10, 0);

            // Line crossing through
            var lCross = new GeoLine2(new GeoPoint2(-15, 0), new GeoPoint2(15, 0));
            Assert.True(Collision2.CollidesWith(rect, lCross));

            // Line contained completely inside
            var lInside = new GeoLine2(new GeoPoint2(-5, 0), new GeoPoint2(5, 0));
            Assert.True(Collision2.CollidesWith(rect, lInside));

            // Disjoint parallel line
            var lDisjoint = new GeoLine2(new GeoPoint2(-10, 10), new GeoPoint2(10, 10));
            Assert.False(Collision2.CollidesWith(rect, lDisjoint));
        }

        [Fact]
        public void RectanglePolygon_CollidesWith_ComprehensiveCases()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 20, 10, 0);

            // Polygon overlapping rectangle
            var pOverlap = new GeoPolygon2(new[]
            {
                new GeoPoint2(5, 0),
                new GeoPoint2(15, 0),
                new GeoPoint2(15, 10),
                new GeoPoint2(5, 10)
            });
            Assert.True(Collision2.CollidesWith(rect, pOverlap));

            // Disjoint polygon
            var pDisjoint = new GeoPolygon2(new[]
            {
                new GeoPoint2(30, 30),
                new GeoPoint2(40, 30),
                new GeoPoint2(40, 40),
                new GeoPoint2(30, 40)
            });
            Assert.False(Collision2.CollidesWith(rect, pDisjoint));
        }

        [Fact]
        public void RectanglePolyline_CollidesWith_ComprehensiveCases()
        {
            var rect = new GeoRectangle2(new GeoPoint2(0, 0), 20, 10, 0);

            // Polyline cutting through rectangle
            var plCross = new GeoPolyline2(new[]
            {
                new GeoPoint2(-15, 0),
                new GeoPoint2(0, 0),
                new GeoPoint2(0, 15)
            });
            Assert.True(Collision2.CollidesWith(plCross, rect));
            Assert.True(rect.CollidesWith(plCross));
            Assert.True(plCross.CollidesWith(rect));

            // Disjoint polyline
            var plDisjoint = new GeoPolyline2(new[]
            {
                new GeoPoint2(30, 30),
                new GeoPoint2(40, 30)
            });
            Assert.False(Collision2.CollidesWith(plDisjoint, rect));
            Assert.False(rect.CollidesWith(plDisjoint));
            Assert.False(plDisjoint.CollidesWith(rect));
        }

        #endregion

        #region Polygon & Polyline

        [Fact]
        public void PolygonPolygon_CollidesWith_ComprehensiveCases()
        {
            var p1 = new GeoPolygon2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10),
                new GeoPoint2(0, 10)
            });

            // Overlapping
            var pOverlap = new GeoPolygon2(new[]
            {
                new GeoPoint2(5, 5),
                new GeoPoint2(15, 5),
                new GeoPoint2(15, 15),
                new GeoPoint2(5, 15)
            });
            Assert.True(Collision2.CollidesWith(p1, pOverlap));

            // Touching along edge
            var pTouch = new GeoPolygon2(new[]
            {
                new GeoPoint2(10, 0),
                new GeoPoint2(20, 0),
                new GeoPoint2(20, 10),
                new GeoPoint2(10, 10)
            });
            Assert.True(Collision2.CollidesWith(p1, pTouch));

            // Disjoint
            var pDisjoint = new GeoPolygon2(new[]
            {
                new GeoPoint2(20, 20),
                new GeoPoint2(30, 20),
                new GeoPoint2(30, 30),
                new GeoPoint2(20, 30)
            });
            Assert.False(Collision2.CollidesWith(p1, pDisjoint));
        }

        [Fact]
        public void PolylinePolyline_CollidesWith_ComprehensiveCases()
        {
            var pl1 = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(10, 0),
                new GeoPoint2(10, 10)
            });

            // Crossing segment 1
            var plCross = new GeoPolyline2(new[]
            {
                new GeoPoint2(5, -5),
                new GeoPoint2(5, 5)
            });
            Assert.True(Collision2.CollidesWith(pl1, plCross));

            // Touching endpoint
            var plTouch = new GeoPolyline2(new[]
            {
                new GeoPoint2(10, 10),
                new GeoPoint2(20, 10)
            });
            Assert.True(Collision2.CollidesWith(pl1, plTouch));

            // Disjoint
            var plDisjoint = new GeoPolyline2(new[]
            {
                new GeoPoint2(0, 20),
                new GeoPoint2(10, 20)
            });
            Assert.False(Collision2.CollidesWith(pl1, plDisjoint));
        }

        #endregion
        #region Degenerate Rectangle Regression

        [Fact]
        public void RectangleRectangle_ZeroExtent_DoesNotThrowAndStillSeparates()
        {
            var flat = new GeoRectangle2(new GeoPoint2(0, 0), 10, 0);      // zero height
            var thin = new GeoRectangle2(new GeoPoint2(0, 0), 0, 10);      // zero width
            var degenerate = new GeoRectangle2(new GeoPoint2(0, 0), 0, 0); // a single point
            var overlapping = new GeoRectangle2(new GeoPoint2(1, 0), 10, 10);
            var faraway = new GeoRectangle2(new GeoPoint2(500, 500), 10, 10);

            // Normalizing a zero-length edge axis used to throw before the boxes were ever tested.
            Assert.True(Collision2.CollidesWith(flat, overlapping));
            Assert.True(Collision2.CollidesWith(thin, overlapping));
            Assert.True(Collision2.CollidesWith(degenerate, overlapping));

            Assert.False(Collision2.CollidesWith(flat, faraway));
            Assert.False(Collision2.CollidesWith(thin, faraway));
            Assert.False(Collision2.CollidesWith(degenerate, faraway));
        }

        #endregion

        #region Polyline Is A Curve, Not A Region

        [Fact]
        public void PolylinePolyline_NestedLoops_DoNotCollideButTheirPolygonsDo()
        {
            var outer = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100), new GeoPoint2(0, 0));
            var inner = new GeoPolyline2(new GeoPoint2(40, 40), new GeoPoint2(60, 40), new GeoPoint2(60, 60), new GeoPoint2(40, 60), new GeoPoint2(40, 40));

            // No edges cross, and a polyline encloses nothing, so the two curves never meet. Asking about
            // the enclosed area is what GeoPolygon2 is for, and there the containment does count.
            Assert.False(Collision2.CollidesWith(outer, inner));
            Assert.True(Collision2.CollidesWith(outer.ToPolygon(), inner.ToPolygon()));
            Assert.True(Distance2.DistanceTo(outer, inner) > 0.0);
        }

        [Fact]
        public void Polyline_CollinearOverlap_StillCollides()
        {
            // Two segments lying on top of each other are reported as parallel by the intersection test,
            // never as a crossing, so an edge loop alone would miss this.
            var path = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

            var alongAnEdge = new GeoLine2(new GeoPoint2(2, 0), new GeoPoint2(8, 0));
            Assert.False(Intersection2.TryIntersectWith(path.GetEdgeAt(0), alongAnEdge, out _));
            Assert.True(Collision2.CollidesWith(path, alongAnEdge));

            var other = new GeoPolyline2(new GeoPoint2(2, 0), new GeoPoint2(8, 0));
            Assert.True(Collision2.CollidesWith(path, other));
        }

        [Fact]
        public void PolylinePolyline_DisjointLoops_DoNotCollide()
        {
            var left = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10), new GeoPoint2(0, 0));
            var right = new GeoPolyline2(new GeoPoint2(50, 0), new GeoPoint2(60, 0), new GeoPoint2(60, 10), new GeoPoint2(50, 10), new GeoPoint2(50, 0));

            Assert.False(Collision2.CollidesWith(left, right));
        }

        [Fact]
        public void OpenPolyline_EnclosesNothing_SoContainedShapesDoNotCollide()
        {
            // An open polyline is a curve: a shape sitting on its concave side is not inside it.
            var open = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100));
            var box = new GeoRectangle2(new GeoPoint2(50, 90), 4, 4);

            Assert.False(Collision2.CollidesWith(open, box));
        }

        [Fact]
        public void TracedLoop_EnclosesNothing_UntilItBecomesAPolygon()
        {
            var loop = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100), new GeoPoint2(0, 0));
            var polygon = loop.ToPolygon();

            var box = new GeoRectangle2(new GeoPoint2(50, 50), 4, 4);
            var circle = new GeoCircle2(new GeoPoint2(50, 50), 2);
            var line = new GeoLine2(new GeoPoint2(40, 50), new GeoPoint2(60, 50));
            var inner = new GeoPolygon2(new GeoPoint2(40, 40), new GeoPoint2(60, 40), new GeoPoint2(60, 60));

            // Nothing here touches the path itself, so nothing collides with the curve.
            Assert.False(Collision2.CollidesWith(loop, box));
            Assert.False(Collision2.CollidesWith(circle, loop));
            Assert.False(Collision2.CollidesWith(loop, line));
            Assert.False(Collision2.CollidesWith(loop, inner));

            // The same geometry as a region swallows all four.
            Assert.True(Collision2.CollidesWith(box, polygon));
            Assert.True(Collision2.CollidesWith(circle, polygon));
            Assert.True(Collision2.CollidesWith(polygon, line));
            Assert.True(Collision2.CollidesWith(polygon, inner));
        }

        #endregion
    }
}
