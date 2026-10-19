using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    public class SnapTests
    {
        private const double Radius = SnapEngine.DefaultRadius;

        private static SnapHit SnapAt(ViewFixture view, Vec3 near, SnapKinds kinds, double offsetX = 1.5, double offsetY = -1.0, Func<int, bool> visible = null)
        {
            (double x, double y) = view.Pixel(near);
            return SnapEngine.Find(view.Geometry, view.Map, x + offsetX, y + offsetY, kinds, Radius, view.Scene.EqualPoint, visible);
        }

        [Fact]
        public void TheVertexOfAChain_SnapsAsAnEnd_ExactlyWhereItIs()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 60))));

            SnapHit hit = SnapAt(view, new Vec3(100, 0, 0), SnapKinds.All);

            Assert.Equal("end", hit.Name);
            Assert.Equal(SnapKinds.End, hit.Kind);
            Assert.Equal(new Vec3(100, 0, 0), hit.Point);
        }

        [Fact]
        public void ThePointItem_SnapsByTheNamePoint()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoPoint3(12.5, -3.25, 4)).Add(new GeoPoint3(50, 50, 50)));

            SnapHit hit = SnapAt(view, new Vec3(12.5, -3.25, 4), SnapKinds.End);

            Assert.Equal("point", hit.Name);
            Assert.Equal(new Vec3(12.5, -3.25, 4), hit.Point);
        }

        [Fact]
        public void TheMiddleOfASegment_IsTheHalfwayPointOfItsEnds()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoLine2(new GeoPoint2(10.1, 20.3), new GeoPoint2(110.7, 80.9))));

            SnapHit hit = SnapAt(view, new Vec3(60.4, 50.6, 0), SnapKinds.Mid);

            Assert.Equal("mid", hit.Name);
            ViewFixture.AssertNear(new Vec3((10.1 + 110.7) * 0.5, (20.3 + 80.9) * 0.5, 0), hit.Point);
        }

        [Fact]
        public void TheEndsAndTheMiddleOfAnArc_AreOnTheExactArc()
        {
            var arc = new GeoArc2(new GeoPoint2(10, 20), 50.0, 0.3, 1.5);
            var view = ViewFixture.Top(new SceneWriter().Add(arc));

            Vec3 end = new Vec3(10 + 50 * Math.Cos(1.5), 20 + 50 * Math.Sin(1.5), 0);
            Vec3 start = new Vec3(10 + 50 * Math.Cos(0.3), 20 + 50 * Math.Sin(0.3), 0);
            Vec3 mid = new Vec3(10 + 50 * Math.Cos(0.9), 20 + 50 * Math.Sin(0.9), 0);

            ViewFixture.AssertNear(end, SnapAt(view, end, SnapKinds.End).Point);
            ViewFixture.AssertNear(start, SnapAt(view, start, SnapKinds.End).Point);
            ViewFixture.AssertNear(mid, SnapAt(view, mid, SnapKinds.Mid).Point);
        }

        [Fact]
        public void TheBulgeOfAChain_GivesAnArcWithItsEndsExactlyTheVerticesAndTheMiddleAHalfBulgeOffTheChord()
        {
            // A bulge of 0.5 on a chord of 100 turns through 4 atan 0.5 = 106 degrees: the sagitta is 25, the radius 62.5 and the centre 37.5 up the chord.
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 80) }, new[] { 0.5, 0.0, 0.0 });
            var view = ViewFixture.Top(new SceneWriter().Add(chain));

            ViewFixture.AssertNear(new Vec3(50, -25, 0), SnapAt(view, new Vec3(50, -25, 0), SnapKinds.Mid).Point);
            ViewFixture.AssertNear(new Vec3(50, 37.5, 0), SnapAt(view, new Vec3(50, 37.5, 0), SnapKinds.Centre).Point);
            Assert.Equal(new Vec3(0, 0, 0), SnapAt(view, new Vec3(0, 0, 0), SnapKinds.End).Point);

            SceneCurve arc = view.Geometry.Curves.Single(c => c.Shape == CurveShape.Conic);
            Assert.Equal(62.5, arc.RadiusU, 12);
            Assert.True(arc.IsCircular);
        }

        [Fact]
        public void ABulgeTheOtherWay_PutsTheMiddleOnTheOtherSideOfTheChord()
        {
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 80) }, new[] { -0.5, 0.0, 0.0 });
            var view = ViewFixture.Top(new SceneWriter().Add(chain));

            ViewFixture.AssertNear(new Vec3(50, 25, 0), SnapAt(view, new Vec3(50, 25, 0), SnapKinds.Mid).Point);
            ViewFixture.AssertNear(new Vec3(50, -37.5, 0), SnapAt(view, new Vec3(50, -37.5, 0), SnapKinds.Centre).Point);
        }

        [Fact]
        public void TheCentreOfACircleAnArcAndAnEllipse_IsTheCentreTheyWereGiven()
        {
            var circle = new GeoCircle2(new GeoPoint2(10.25, -7.5), 30.0);
            var arc = new GeoArc2(new GeoPoint2(200.5, 40.125), 20.0, 0.0, 1.0);
            var ellipse = new GeoEllipse2(new GeoPoint2(-120.75, 33.5), new GeoVector2(1, 0), 25.0, 10.0);
            var view = ViewFixture.Top(new SceneWriter().Add(circle).Add(arc).Add(ellipse));

            Assert.Equal(new Vec3(10.25, -7.5, 0), SnapAt(view, new Vec3(10.25, -7.5, 0), SnapKinds.Centre).Point);
            Assert.Equal(new Vec3(200.5, 40.125, 0), SnapAt(view, new Vec3(200.5, 40.125, 0), SnapKinds.Centre).Point);
            ViewFixture.AssertNear(new Vec3(-120.75, 33.5, 0), SnapAt(view, new Vec3(-120.75, 33.5, 0), SnapKinds.Centre).Point);
            Assert.Equal("centre", SnapAt(view, new Vec3(10.25, -7.5, 0), SnapKinds.Centre).Name);
        }

        [Fact]
        public void ACircleHasNoEndsAndNoMiddle_ButAnArcHasBoth()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoCircle2(new GeoPoint2(0, 0), 40.0)));

            // At the point where the circle's parameter starts there is no end to snap to.
            Assert.Null(SnapAt(view, new Vec3(40, 0, 0), SnapKinds.End | SnapKinds.Mid, 0, 0));
        }

        [Fact]
        public void TwoSegmentsThatCross_SnapAtTheirCrossing()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 100)))
                .Add(new GeoLine2(new GeoPoint2(0, 100), new GeoPoint2(100, 0))));

            SnapHit hit = SnapAt(view, new Vec3(50, 50, 0), SnapKinds.Intersection);

            Assert.Equal("intersection", hit.Name);
            ViewFixture.AssertNear(new Vec3(50, 50, 0), hit.Point);
            Assert.Equal(0, hit.ItemIndex);
            Assert.Equal(1, hit.OtherItemIndex);
        }

        [Fact]
        public void TwoSegmentsThatMeetOnlyAtAnEnd_GiveTheEnd_AndTheCrossingWhereEndsAreOff()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0)))
                .Add(new GeoLine2(new GeoPoint2(100, 0), new GeoPoint2(100, 100))));

            // Ends are snapped to: the meeting is the end. Where they are not, it is still a crossing, the one place the two come together.
            Assert.Equal("end", SnapAt(view, new Vec3(100, 0, 0), SnapKinds.All).Name);
            Assert.Equal("intersection", SnapAt(view, new Vec3(100, 0, 0), SnapKinds.Intersection).Name);
        }

        [Fact]
        public void ASegmentThroughACircle_CrossesItWhereTheNumbersSay()
        {
            var circle = new GeoCircle2(new GeoPoint2(50, 50), 30.0);
            var line = new GeoLine2(new GeoPoint2(0, 50), new GeoPoint2(100, 50));
            var view = ViewFixture.Top(new SceneWriter().Add(circle).Add(line));

            ViewFixture.AssertNear(new Vec3(20, 50, 0), SnapAt(view, new Vec3(20, 50, 0), SnapKinds.Intersection).Point);
            ViewFixture.AssertNear(new Vec3(80, 50, 0), SnapAt(view, new Vec3(80, 50, 0), SnapKinds.Intersection).Point);
        }

        [Fact]
        public void ASegmentAcrossAnEllipse_CrossesItAtTheEndOfTheMinorAxis()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 0), 10.0, 5.0))
                .Add(new GeoLine2(new GeoPoint2(0, -20), new GeoPoint2(0, 20))));

            ViewFixture.AssertNear(new Vec3(0, 5, 0), SnapAt(view, new Vec3(0, 5, 0), SnapKinds.Intersection).Point);
            ViewFixture.AssertNear(new Vec3(0, -5, 0), SnapAt(view, new Vec3(0, -5, 0), SnapKinds.Intersection).Point);
        }

        [Fact]
        public void TwoCirclesThatOverlap_CrossInTwoPoints()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 5.0))
                .Add(new GeoCircle2(new GeoPoint2(6, 0), 5.0)));

            ViewFixture.AssertNear(new Vec3(3, 4, 0), SnapAt(view, new Vec3(3, 4, 0), SnapKinds.Intersection).Point);
            ViewFixture.AssertNear(new Vec3(3, -4, 0), SnapAt(view, new Vec3(3, -4, 0), SnapKinds.Intersection).Point);
        }


        [Fact]
        public void AnArcAndASegment_CrossOnlyWhereTheArcIs()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 10.0, 0.0, Math.PI / 2.0);
            var line = new GeoLine2(new GeoPoint2(-20, 6), new GeoPoint2(20, 6));
            var view = ViewFixture.Top(new SceneWriter().Add(arc).Add(line));
            SceneCurve arcCurve = view.Geometry.Curves.Single(c => c.Shape == CurveShape.Conic);
            SceneCurve lineCurve = view.Geometry.Curves.Single(c => c.Shape == CurveShape.Segment);

            // The line meets the whole circle at x = 8 and x = -8; the arc is the quarter in the first quadrant.
            Vec3 only = Assert.Single(CurveIntersections.Find(arcCurve, lineCurve, 1E-9));
            ViewFixture.AssertNear(new Vec3(8, 6, 0), only);
            Assert.Equal(only, Assert.Single(CurveIntersections.Find(lineCurve, arcCurve, 1E-9)));
        }

        [Fact]
        public void TwoSegmentsInSpace_CrossWhereTheyCome_AndNotWhereTheyPassOver()
        {
            var meeting = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10)))
                .Add(new GeoLine3(new GeoPoint3(10, 0, 5), new GeoPoint3(0, 10, 5))));

            SceneCurve a = meeting.Geometry.Curves[0];
            SceneCurve b = meeting.Geometry.Curves[1];
            ViewFixture.AssertNear(new Vec3(5, 5, 5), Assert.Single(CurveIntersections.Find(a, b, 1E-9)));

            // Raised a millimetre they pass over one another: seen from above they cross, in space they do not.
            var apart = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10)))
                .Add(new GeoLine3(new GeoPoint3(10, 0, 6), new GeoPoint3(0, 10, 6))));

            Assert.Empty(CurveIntersections.Find(apart.Geometry.Curves[0], apart.Geometry.Curves[1], 1E-3));
        }

        [Fact]
        public void ASegmentThroughTheCircleOfAnotherPlane_CrossesItWhereItPiercesThePlane()
        {
            var circle = new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), 10.0);
            var line = new GeoLine3(new GeoPoint3(-5, 10, 0), new GeoPoint3(5, 10, 0));
            var view = ViewFixture.Top(new SceneWriter().Add(circle).Add(line));

            Vec3 only = Assert.Single(CurveIntersections.Find(view.Geometry.Curves[0], view.Geometry.Curves[1], 1E-9));
            ViewFixture.AssertNear(new Vec3(0, 10, 0), only);
        }

        [Fact]
        public void TwoCirclesInPlanesAtRightAngles_CrossOnTheLineWhereThePlanesMeet()
        {
            var flat = new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10.0);
            var upright = new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 0), 10.0);
            var view = ViewFixture.Iso(new SceneWriter().Add(flat).Add(upright));

            Vec3[] found = CurveIntersections.Find(view.Geometry.Curves[0], view.Geometry.Curves[1], 1E-9).OrderBy(p => p.X).ToArray();

            Assert.Equal(2, found.Length);
            ViewFixture.AssertNear(new Vec3(-10, 0, 0), found[0]);
            ViewFixture.AssertNear(new Vec3(10, 0, 0), found[1]);
        }

        [Fact]
        public void AnEllipseAgainstACircle_IsNotLookedFor()
        {
            var view = ViewFixture.Top(new SceneWriter()
                .Add(new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 0), 10.0, 5.0))
                .Add(new GeoCircle2(new GeoPoint2(0, 0), 7.0)));

            Assert.False(CurveIntersections.IsSupported(view.Geometry.Curves[0], view.Geometry.Curves[1]));
            Assert.Empty(CurveIntersections.Find(view.Geometry.Curves[0], view.Geometry.Curves[1], 1E-9));
        }

        [Fact]
        public void TheNearestPoint_IsOnTheCurve_AndOnlyTakenWhereNothingElseIsInReach()
        {
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            var view = ViewFixture.Top(new SceneWriter().Add(line));

            // About a third of the way along, two pixels off the line, far from the ends and the middle.
            (double x, double y) = view.Pixel(new Vec3(30, 0, 0));
            SnapHit hit = SnapEngine.Find(view.Geometry, view.Map, x, y + 2.0, SnapKinds.All, Radius, 1E-3, null);

            Assert.Equal("nearest", hit.Name);
            Assert.Equal(0.0, hit.Point.Y);
            Assert.InRange(hit.Point.X, 29.0, 31.0);

            // Beside the end the end wins, though the nearest point is nearer the cursor.
            (double ex, double ey) = view.Pixel(new Vec3(100, 0, 0));
            Assert.Equal("end", SnapEngine.Find(view.Geometry, view.Map, ex - 8.0, ey + 1.0, SnapKinds.All, Radius, 1E-3, null).Name);

            // With nearest off there is nothing to snap to at the third.
            Assert.Null(SnapEngine.Find(view.Geometry, view.Map, x, y + 2.0, SnapKinds.End | SnapKinds.Mid | SnapKinds.Centre | SnapKinds.Intersection, Radius, 1E-3, null));
        }

        [Fact]
        public void TheNearestPointOfACircle_IsOnTheCircleToTheLastDigit()
        {
            var circle = new GeoCircle2(new GeoPoint2(100, 200), 37.5);
            var view = ViewFixture.Top(new SceneWriter().Add(circle));

            (double x, double y) = view.Pixel(new Vec3(100 + 37.5 * Math.Cos(0.7), 200 + 37.5 * Math.Sin(0.7), 0));
            SnapHit hit = SnapEngine.Find(view.Geometry, view.Map, x + 3.0, y - 2.0, SnapKinds.Nearest, Radius, 1E-3, null);

            Assert.Equal("nearest", hit.Name);
            Assert.Equal(37.5, (hit.Point - new Vec3(100, 200, 0)).Length, 12);
        }

        [Fact]
        public void ACursorFarFromEverything_SnapsToNothing()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0))).Add(new GeoPoint2(0, 100)));

            Assert.Null(SnapEngine.Find(view.Geometry, view.Map, 900.0, 40.0, SnapKinds.All, Radius, 1E-3, null));
            Assert.Null(SnapEngine.Find(view.Geometry, view.Map, 500.0, 400.0, SnapKinds.None, Radius, 1E-3, null));
        }

        [Fact]
        public void AnItemThatIsHidden_IsNotSnappedTo()
        {
            var view = ViewFixture.Top(new SceneWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0))).Add(new GeoLine2(new GeoPoint2(0, 50), new GeoPoint2(100, 50))));

            Assert.Equal(0, SnapAt(view, new Vec3(0, 0, 0), SnapKinds.End, 0, 0).ItemIndex);
            Assert.Null(SnapAt(view, new Vec3(0, 0, 0), SnapKinds.End, 0, 0, item => item != 0));
        }

        [Fact]
        public void InPerspective_TheSnapsAreFoundAtTheirExactPlacesToo()
        {
            var view = ViewFixture.Iso(new SceneWriter().Add(new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 20, 30), new GeoPoint3(100, 120, 60))));

            Assert.False(view.Map.Orthographic);
            ViewFixture.AssertNear(new Vec3(100, 20, 30), SnapAt(view, new Vec3(100, 20, 30), SnapKinds.End).Point);
            ViewFixture.AssertNear(new Vec3(50, 10, 15), SnapAt(view, new Vec3(50, 10, 15), SnapKinds.Mid).Point);
            ViewFixture.AssertNear(new Vec3(100, 70, 45), SnapAt(view, new Vec3(100, 70, 45), SnapKinds.Mid).Point);
        }

        [Fact]
        public void TwoSnapsAsNearAsEachOther_AreChosenByKindAndNotByTheOrderTheCurvesAreHeldIn()
        {
            // The middle of one segment lies on the end of two others: three snaps in one place.
            var forward = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0)))
                .Add(new GeoLine2(new GeoPoint2(50, 0), new GeoPoint2(50, 60)))
                .Add(new GeoLine2(new GeoPoint2(50, 0), new GeoPoint2(50, -60))));
            var backward = ViewFixture.Top(new SceneWriter()
                .Add(new GeoLine2(new GeoPoint2(50, 0), new GeoPoint2(50, -60)))
                .Add(new GeoLine2(new GeoPoint2(50, 0), new GeoPoint2(50, 60)))
                .Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0))));

            SnapHit a = SnapAt(forward, new Vec3(50, 0, 0), SnapKinds.All, 0, 0);
            SnapHit b = SnapAt(backward, new Vec3(50, 0, 0), SnapKinds.All, 0, 0);

            Assert.Equal("end", a.Name);
            Assert.Equal("end", b.Name);
            Assert.Equal(1, a.ItemIndex);
            Assert.Equal(0, b.ItemIndex);
        }
    }
}