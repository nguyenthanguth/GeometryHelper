using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// Three gaps the matrix showed once it was drawn properly: a straight chain in space could be measured
    /// against nine shapes and asked whether it touched three; a face and a rectangle had
    /// <c>GetIntersections</c> and no <c>TryIntersectWith</c>; and the open shapes in space had
    /// <c>IsPointOn</c> without <c>Locate</c>, while the plane's edge had neither.
    /// </summary>
    /// <remarks>
    /// All three were wiring, not arithmetic, which is what the tests hold them to: every answer here is
    /// checked against the answer the same question already had by another route.
    /// </remarks>
    public class MatrixGapTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>A chain in z = 0: along x to (400, 0), then up to (400, 200).</summary>
        private static GeoPolyline3 Chain() => new GeoPolyline3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 200, 0));

        #region A straight chain in space

        [Fact]
        public void AStraightChainCrossesAPlaneWhereItsSegmentsDo()
        {
            GeoPolyline3 chain = Chain();
            var wall = new GeoPlane3(new GeoPoint3(200, 0, 0), GeoVector3.XAxis);

            GeoPoint3[] crossings = chain.GetIntersections(wall);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(200, 0, 0), Loose));
            Assert.True(chain.CollidesWith(wall));
            Assert.True(chain.TryIntersectWith(wall, out GeoPoint3[] found));
            Assert.Single(found);

            // The union over the segments is the whole claim, so it must agree with asking each one.
            GeoPoint3[] bySegment = Enumerable.Range(0, chain.EdgeCount)
                .SelectMany(i => chain.GetEdgeAt(i).GetIntersections(wall))
                .ToArray();

            Assert.Equal(bySegment.Length, crossings.Length);

            // A plane it never reaches is answered plainly.
            var clear = new GeoPlane3(new GeoPoint3(0, 0, 500), GeoVector3.ZAxis);

            Assert.Empty(chain.GetIntersections(clear));
            Assert.False(chain.CollidesWith(clear));
            Assert.False(chain.TryIntersectWith(clear, out GeoPoint3[] none));
            Assert.Empty(none);
        }

        [Fact]
        public void ACrossingAtAVertexIsNamedOnceAndNotTwice()
        {
            GeoPolyline3 chain = Chain();

            // A plane standing on the corner: both segments of the chain end there.
            var atCorner = new GeoPlane3(new GeoPoint3(400, 0, 0), new GeoVector3(1, 1, 0));

            GeoPoint3[] crossings = chain.GetIntersections(atCorner);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(400, 0, 0), Loose));
        }

        [Fact]
        public void AStraightChainAnswersAboutEveryShapeASegmentDoes()
        {
            GeoPolyline3 chain = Chain();

            var box = new GeoObb3(new GeoPoint3(200, 0, 0), 100, 100, 100);
            var square = new GeoAabb3(new GeoPoint3(150, -50, -50), new GeoPoint3(250, 50, 50));
            var triangle = new GeoTriangle3(new GeoPoint3(300, -50, -50), new GeoPoint3(300, 50, -50), new GeoPoint3(300, 0, 50));
            var polygon = new GeoPolygon3(
                new GeoPoint3(350, -50, -50), new GeoPoint3(350, 50, -50),
                new GeoPoint3(350, 50, 50), new GeoPoint3(350, -50, 50));
            var line = new GeoLine3(new GeoPoint3(100, -50, 0), new GeoPoint3(100, 50, 0));
            var ray = new GeoRay3(new GeoPoint3(50, -50, 0), new GeoVector3(0, 1, 0));
            GeoArc3 arc = GeoArc3.FromThreePoints(
                new GeoPoint3(250, -50, 0), new GeoPoint3(220, 0, 0), new GeoPoint3(250, 50, 0));
            var circle = new GeoCircle3(new GeoPoint3(280, 0, 0), GeoVector3.YAxis, 40.0);
            GeoSolid3 body = box.ToSolid();

            Assert.True(chain.CollidesWith(box));
            Assert.True(chain.CollidesWith(square));
            Assert.True(chain.CollidesWith(triangle));
            Assert.True(chain.CollidesWith(polygon));
            Assert.True(chain.CollidesWith(new GeoFace3(polygon)));
            Assert.True(chain.CollidesWith(line));
            Assert.True(chain.CollidesWith(ray));
            Assert.True(chain.CollidesWith(arc));
            Assert.True(chain.CollidesWith(circle));
            Assert.True(chain.CollidesWith(body));

            Assert.Equal(2, chain.GetIntersections(box).Length);
            Assert.Equal(2, chain.GetIntersections(square).Length);
            Assert.Single(chain.GetIntersections(polygon));
            Assert.Single(chain.GetIntersections(new GeoFace3(polygon)));
            Assert.Single(chain.GetIntersections(line));
            Assert.Single(chain.GetIntersections(ray));
            Assert.NotEmpty(chain.GetIntersections(arc));
            Assert.Equal(2, chain.GetIntersections(body).Length);

            // Nothing is asked of nothing.
            Assert.Throws<ArgumentNullException>(() => Intersection3.GetIntersections((GeoPolyline3)null, box));
            Assert.Throws<ArgumentNullException>(() => Collision3.CollidesWith((GeoPolyline3)null, box));
        }

        [Fact]
        public void TwoStraightChainsFindEachOther()
        {
            GeoPolyline3 chain = Chain();
            var across = new GeoPolyline3(
                new GeoPoint3(200, -50, 0), new GeoPoint3(200, 50, 0), new GeoPoint3(300, 50, 0));

            GeoPoint3[] crossings = chain.GetIntersections(across);

            Assert.Single(crossings);
            Assert.True(crossings[0].IsEqualTo(new GeoPoint3(200, 0, 0), Loose));
            Assert.True(chain.CollidesWith(across));
            Assert.True(chain.TryIntersectWith(across, out GeoPoint3[] found));
            Assert.Single(found);

            // Asked the other way round it is the same place.
            Assert.True(across.GetIntersections(chain)[0].IsEqualTo(crossings[0], Loose));

            // And reading either as a curved chain gives the same answer, which is the point of the reading.
            Assert.Single(chain.GetIntersections(new GeoPolylineArc3(across)));

            // Far apart is dropped by the boxes before any arithmetic, and answered plainly.
            GeoPolyline3 away = across.Translate(new GeoVector3(0, 0, 5000));

            Assert.Empty(chain.GetIntersections(away));
            Assert.False(chain.CollidesWith(away));

            // Two chains lying along each other meet along a length and name no place.
            var along = new GeoPolyline3(new GeoPoint3(100, 0, 0), new GeoPoint3(300, 0, 0));

            Assert.Empty(chain.GetIntersections(along));
            Assert.True(chain.CollidesWith(along));

            Assert.Throws<ArgumentNullException>(() => chain.GetIntersections((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => chain.CollidesWith((GeoPolyline3)null));
        }

        #endregion

        #region The Try twin a face and a rectangle were missing

        [Fact]
        public void AFaceAndARectangleReportWhetherTheyMeetAnythingAtAll()
        {
            var boundary = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(0, 200));
            var face = new GeoFace2(boundary);
            var rect = new GeoRectangle2(new GeoPoint2(100, 100), 200.0, 200.0);

            var crossing = new GeoLine2(new GeoPoint2(100, -50), new GeoPoint2(100, 50));
            var clear = new GeoLine2(new GeoPoint2(500, -50), new GeoPoint2(500, 50));

            // The Try twin is GetIntersections read a second way, so the two must agree everywhere.
            Assert.True(face.TryIntersectWith(crossing, out GeoPoint2[] onFace));
            Assert.Equal(face.GetIntersections(crossing).Length, onFace.Length);
            Assert.False(face.TryIntersectWith(clear, out GeoPoint2[] none));
            Assert.Empty(none);

            Assert.True(rect.TryIntersectWith(crossing, out GeoPoint2[] onRect));
            Assert.Equal(rect.GetIntersections(crossing).Length, onRect.Length);
            Assert.False(rect.TryIntersectWith(clear, out _));

            // Every shape either of them can be crossed with can now be asked the shorter question too.
            var arc = new GeoArc2(new GeoPoint2(100, 0), 50.0, 0.0, Math.PI);
            var circle = new GeoCircle2(new GeoPoint2(100, 0), 50.0);
            var chain = new GeoPolyline2(new GeoPoint2(-50, 100), new GeoPoint2(50, 100));
            var loop = new GeoPolygonArc2(new GeoPolygon2(
                new GeoPoint2(150, 150), new GeoPoint2(250, 150), new GeoPoint2(250, 250), new GeoPoint2(150, 250)));

            Assert.Equal(face.GetIntersections(arc).Length > 0, face.TryIntersectWith(arc, out _));
            Assert.Equal(face.GetIntersections(circle).Length > 0, face.TryIntersectWith(circle, out _));
            Assert.Equal(face.GetIntersections(chain).Length > 0, face.TryIntersectWith(chain, out _));
            Assert.Equal(face.GetIntersections(loop).Length > 0, face.TryIntersectWith(loop, out _));
            Assert.Equal(face.GetIntersections(boundary).Length > 0, face.TryIntersectWith(boundary, out _));
            Assert.Equal(face.GetIntersections(rect).Length > 0, face.TryIntersectWith(rect, out _));

            Assert.Equal(rect.GetIntersections(face).Length > 0, rect.TryIntersectWith(face, out _));
            Assert.Equal(rect.GetIntersections(arc).Length > 0, rect.TryIntersectWith(arc, out _));
            Assert.Equal(rect.GetIntersections(rect).Length > 0, rect.TryIntersectWith(rect, out _));

            // And the tolerance twin gives the tolerance twin's answer.
            Assert.Equal(
                face.TryIntersectWith(crossing, out GeoPoint2[] a),
                face.TryIntersectWith(crossing, out GeoPoint2[] b, Tolerance.Global));
            Assert.Equal(a.Length, b.Length);
        }

        #endregion

        #region Locate on the open shapes

        [Fact]
        public void AnOpenShapeInSpaceSaysOnSideOrOutSideAndNothingElse()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            GeoPolyline3 chain = Chain();
            var ray = new GeoRay3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis);

            Assert.Equal(PointLocation.OnSide, line.Locate(new GeoPoint3(50, 0, 0)));
            Assert.Equal(PointLocation.OutSide, line.Locate(new GeoPoint3(50, 10, 0)));
            Assert.Equal(PointLocation.OutSide, line.Locate(new GeoPoint3(500, 0, 0)));

            Assert.Equal(PointLocation.OnSide, chain.Locate(new GeoPoint3(400, 100, 0)));
            Assert.Equal(PointLocation.OutSide, chain.Locate(new GeoPoint3(200, 100, 0)));

            Assert.Equal(PointLocation.OnSide, ray.Locate(new GeoPoint3(5000, 0, 0)));
            Assert.Equal(PointLocation.OutSide, ray.Locate(new GeoPoint3(-5000, 0, 0)));

            // Locate and IsPointOn are the one answer read two ways, so they cannot disagree.
            foreach (GeoPoint3 point in new[]
                     {
                         new GeoPoint3(50, 0, 0), new GeoPoint3(50, 10, 0),
                         new GeoPoint3(400, 100, 0), new GeoPoint3(-1, 0, 0),
                     })
            {
                Assert.Equal(line.IsPointOn(point), line.Locate(point) == PointLocation.OnSide);
                Assert.Equal(chain.IsPointOn(point), chain.Locate(point) == PointLocation.OnSide);
                Assert.Equal(ray.IsPointOn(point), ray.Locate(point) == PointLocation.OnSide);
            }

            // Never Inside: there is nothing to be inside of, even where a chain's ends happen to meet.
            var closed = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0),
                new GeoPoint3(0, 100, 0), new GeoPoint3(0, 0, 0));

            Assert.Equal(PointLocation.OutSide, closed.Locate(new GeoPoint3(50, 50, 0)));
        }

        [Fact]
        public void ThePlanesEdgeAnswersWhatSpacesEdgeAlwaysHas()
        {
            var straight = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));

            Assert.True(straight.IsPointOn(new GeoPoint2(50, 0)));
            Assert.False(straight.IsPointOn(new GeoPoint2(50, 10)));
            Assert.Equal(PointLocation.OnSide, straight.Locate(new GeoPoint2(50, 0)));
            Assert.Equal(PointLocation.OutSide, straight.Locate(new GeoPoint2(50, 10)));

            // A curved edge reads itself as its arc, so a point on the arc is on the edge and the chord is not.
            var bend = new GeoEdge2(new GeoArc2(new GeoPoint2(0, 0), 100.0, 0.0, Math.PI / 2.0));

            Assert.True(bend.IsArc);
            Assert.True(bend.IsPointOn(bend.ToArc().GetPointAtParameter(0.5)));
            Assert.False(bend.IsPointOn(new GeoPoint2(50, 50)));
            Assert.Equal(PointLocation.OnSide, bend.Locate(bend.ToArc().GetPointAtParameter(0.5)));

            // And it agrees with the edge in space on the same shape laid flat.
            var inSpace = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

            Assert.Equal(straight.Locate(new GeoPoint2(50, 0)), inSpace.Locate(new GeoPoint3(50, 0, 0)));
            Assert.Equal(straight.Locate(new GeoPoint2(50, 10)), inSpace.Locate(new GeoPoint3(50, 10, 0)));

            Assert.Equal(straight.IsPointOn(new GeoPoint2(50, 0)), straight.IsPointOn(new GeoPoint2(50, 0), Tolerance.Global));
            Assert.Equal(straight.Locate(new GeoPoint2(50, 0)), straight.Locate(new GeoPoint2(50, 0), Tolerance.Global));
        }

        #endregion
    }
}
