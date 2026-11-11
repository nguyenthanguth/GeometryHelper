using System;
using System.Linq;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Projection
{
    public class ViewProjectorTests
    {
        [Fact]
        public void ABoxSeenFromAboveIsFourSeenLinesItsBackEdgesUnderThemAndItsUprightEdgesPoints()
        {
            ProjectedView2 view = Project(Top, Box(0, 0, 0, 300, 200, 20));

            Assert.Equal(4, view.VisibleEdges.Count);
            Assert.Empty(view.HiddenEdges);
            HasLine(view.VisibleEdges, 0, 0, 300, 0);
            HasLine(view.VisibleEdges, 300, 0, 300, 200);
            HasLine(view.VisibleEdges, 0, 200, 300, 200);
            HasLine(view.VisibleEdges, 0, 0, 0, 200);
            Assert.All(view.VisibleEdges, e => Assert.Equal(EdgeKind.Sharp, e.Kind));
            Assert.All(view.VisibleEdges, e => Assert.Equal("A", e.ObjectId));
            Assert.Equal(150.0, view.Bounds.Center.X, 9);
            Assert.Equal(300.0, view.Bounds.Width, 9);
            Assert.Equal(200.0, view.Bounds.Height, 9);
        }

        [Fact]
        public void ABoxSeenFromTheFrontAndTheSideIsARectangleOfItsHeight()
        {
            GeoSolid3 box = Box(0, 0, 0, 300, 200, 20);

            ProjectedView2 front = Project(Front, box);
            ProjectedView2 side = Project(Right, box);

            Assert.Equal(4, front.VisibleEdges.Count);
            Assert.Empty(front.HiddenEdges);
            HasLine(front.VisibleEdges, 0, 0, 300, 0);
            HasLine(front.VisibleEdges, 0, 20, 300, 20);
            HasLine(front.VisibleEdges, 0, 0, 0, 20);
            HasLine(front.VisibleEdges, 300, 0, 300, 20);
            Assert.Equal(4, side.VisibleEdges.Count);
            Assert.Empty(side.HiddenEdges);
            HasLine(side.VisibleEdges, 0, 20, 200, 20);
        }

        [Fact]
        public void ABoxSeenFromACornerShowsNineSeenEdgesAndThreeHidden()
        {
            ProjectedView2 view = Project(Iso, Box(0, 0, 0, 300, 200, 100));

            Assert.Equal(9, view.VisibleEdges.Count);
            Assert.Equal(3, view.HiddenEdges.Count);

            // The three hidden edges meet at the corner furthest from the viewer, the origin.
            Assert.All(view.HiddenEdges, e => Assert.True(Near(e.StartPoint, 0, 0) || Near(e.EndPoint, 0, 0)));
            Assert.Empty(Overlaps(view.VisibleEdges, view.HiddenEdges, false));
        }

        [Fact]
        public void APlateWithTwoHolesSeenAlongThemShowsTwoCirclesOfTheirDiameterAndNoHiddenLine()
        {
            GeoSolid3 plate = PlateWithHoles(300, 200, 20, 22, 24, P(60, 100), P(240, 100));

            ProjectedView2 top = Project(Top, plate);

            Assert.Equal(6, top.VisibleEdges.Count);
            Assert.Equal(2, top.VisibleEdges.Count(e => e.IsCircle));
            HasCircle(top.VisibleEdges, 60, 100, 11);
            HasCircle(top.VisibleEdges, 240, 100, 11);
            Assert.Empty(top.HiddenEdges);
        }

        [Fact]
        public void APlateWithTwoHolesSeenFromTheFrontShowsEachHoleAsTwoHiddenLinesAcrossItsThickness()
        {
            GeoSolid3 plate = PlateWithHoles(300, 200, 20, 22, 24, P(60, 100), P(240, 100));

            ProjectedView2 front = Project(Front, plate);
            ProjectedView2 side = Project(Right, plate);

            Assert.Equal(4, front.VisibleEdges.Count);
            Assert.Equal(4, front.HiddenEdges.Count);
            HasLine(front.HiddenEdges, 49, 0, 49, 20);
            HasLine(front.HiddenEdges, 71, 0, 71, 20);
            HasLine(front.HiddenEdges, 229, 0, 229, 20);
            HasLine(front.HiddenEdges, 251, 0, 251, 20);
            Assert.All(front.HiddenEdges, e => Assert.Equal(EdgeKind.Silhouette, e.Kind));

            // From the side the two holes stand one behind the other, and their walls give one pair of lines.
            Assert.Equal(4, side.VisibleEdges.Count);
            Assert.Equal(2, side.HiddenEdges.Count);
            HasLine(side.HiddenEdges, 89, 0, 89, 20);
            HasLine(side.HiddenEdges, 111, 0, 111, 20);
        }

        [Fact]
        public void APlateSeenFromBelowShowsItsHolesAsCirclesToo()
        {
            GeoSolid3 plate = PlateWithHoles(300, 200, 20, 22, 24, P(60, 100), P(240, 100));
            var below = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis.Negate(), GeoVector3.YAxis);

            ProjectedView2 view = Project(below, plate);

            Assert.Equal(2, view.VisibleEdges.Count(e => e.IsCircle));
            HasCircle(view.VisibleEdges, -60, 100, 11);
            Assert.Empty(view.HiddenEdges);
        }

        [Fact]
        public void AnAngleSeenAlongItsLengthIsItsSixSidedSection()
        {
            ProjectedView2 end = Project(Right, Angle(100, 10, 1000));

            Assert.Equal(6, end.VisibleEdges.Count);
            Assert.Empty(end.HiddenEdges);
            HasLine(end.VisibleEdges, 10, 10, 100, 10);
            HasLine(end.VisibleEdges, 10, 10, 10, 100);
        }

        [Fact]
        public void AnAngleSeenFromAboveShowsTheInsideOfItsUprightLegAndFromTheFrontItsLegHidden()
        {
            GeoSolid3 angle = Angle(100, 10, 1000);

            ProjectedView2 top = Project(Top, angle);
            ProjectedView2 front = Project(Front, angle);

            Assert.Equal(5, top.VisibleEdges.Count);
            Assert.Empty(top.HiddenEdges);
            HasLine(top.VisibleEdges, 0, 10, 1000, 10);

            // From the front, the upright leg hides the top of the leg lying behind it.
            Assert.Equal(4, front.VisibleEdges.Count);
            Assert.Single(front.HiddenEdges);
            HasLine(front.HiddenEdges, 0, 10, 1000, 10);
        }

        [Fact]
        public void ATubeSeenAcrossIsTwoOuterSilhouettesTwoEndsAndTwoHiddenInnerSilhouettes()
        {
            ProjectedView2 view = Project(Front, Tube(168.3, 6, 1000, 32));

            Assert.Equal(4, view.VisibleEdges.Count);
            Assert.Equal(2, view.HiddenEdges.Count);
            HasLine(view.VisibleEdges, 0, 84.15, 1000, 84.15);
            HasLine(view.VisibleEdges, 0, -84.15, 1000, -84.15);
            HasLine(view.VisibleEdges, 0, -84.15, 0, 84.15);
            HasLine(view.HiddenEdges, 0, 78.15, 1000, 78.15);
            HasLine(view.HiddenEdges, 0, -78.15, 1000, -78.15);
            Assert.Equal(2, view.VisibleEdges.Count(e => e.Kind == EdgeKind.Silhouette));
        }

        [Fact]
        public void ATubeSeenAlongItsAxisIsTwoCircles()
        {
            ProjectedView2 view = Project(Right, Tube(168.3, 6, 1000, 32));

            Assert.Equal(2, view.VisibleEdges.Count);
            HasCircle(view.VisibleEdges, 0, 0, 84.15);
            HasCircle(view.VisibleEdges, 0, 0, 78.15);
            Assert.Empty(view.HiddenEdges);
        }

        [Fact]
        public void ATubeSeenAtThirtyDegreesGivesNoLineTwiceAndNoneBothSeenAndHidden()
        {
            var slant = new GeoCoordinateSystem3(
                GeoPoint3.Origin,
                new GeoVector3(Math.Cos(Math.PI / 6), -Math.Sin(Math.PI / 6), 0),
                GeoVector3.ZAxis
            );

            ProjectedView2 view = Project(slant, Tube(168.3, 6, 1000, 32));

            Assert.NotEmpty(view.VisibleEdges);
            Assert.Empty(Overlaps(view.VisibleEdges, view.VisibleEdges, true));
            Assert.Empty(Overlaps(view.HiddenEdges, view.HiddenEdges, true));
            Assert.Empty(Overlaps(view.VisibleEdges, view.HiddenEdges, false));

            // The two outer silhouettes run the length of the tube, 84.15 either side of its axis.
            Assert.Equal(
                2,
                view.VisibleEdges.Count(e =>
                    e.Kind == EdgeKind.Silhouette && !e.IsArc && Math.Abs(Math.Abs(e.StartPoint.Y) - 84.15) <= 1E-3
                )
            );
        }

        [Fact]
        public void AHexagonalBarSeenAlongItsAxisStaysSixLines()
        {
            GeoSolid3 bar = GeoSolid3.Extrude(
                Circle(0, 0, 20, 6),
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.YAxis, GeoVector3.ZAxis),
                100,
                Tol
            );

            ProjectedView2 view = Project(Right, bar);

            Assert.Equal(6, view.VisibleEdges.Count);
            Assert.DoesNotContain(view.VisibleEdges, e => e.IsArc);
        }

        [Fact]
        public void ARoundedCornerFacetedInFourIsOneArcBetweenItsStraightSides()
        {
            // A plate 100 by 60 whose top right corner is rounded to a radius of 20 in four facets.
            var outline = new System.Collections.Generic.List<GeoPoint2> { P(0, 0), P(100, 0), P(100, 40) };

            for (int i = 1; i < 4; i++)
            {
                double a = Math.PI / 2 * i / 4;
                outline.Add(P(80 + 20 * Math.Cos(a), 40 + 20 * Math.Sin(a)));
            }

            outline.Add(P(80, 60));
            outline.Add(P(0, 60));
            GeoSolid3 plate = GeoSolid3.Extrude(new GeoPolygon2(outline), GeoCoordinateSystem3.Global, 10, Tol);

            ProjectedView2 view = Project(Top, plate);

            ProjectedEdge2 arc = Assert.Single(view.VisibleEdges, e => e.IsArc);
            Assert.False(arc.IsCircle);
            Assert.Equal(20.0, arc.Arc.Radius, 6);
            Assert.Equal(80.0, arc.Arc.Center.X, 6);
            Assert.Equal(40.0, arc.Arc.Center.Y, 6);
            Assert.Equal(Math.PI / 2, arc.Arc.SweptAngle, 6);
            Assert.Equal(5, view.VisibleEdges.Count);
        }

        [Fact]
        public void AHoleHalfBehindAPlateInFrontIsAnArcSeenAndAnArcHiddenOnOneCircle()
        {
            // A plate with a hole of 40 at (100, 100), and in front of it a plate covering everything left of x = 100.
            GeoSolid3 back = PlateWithHoles(200, 200, 10, 40, 24, P(100, 100));
            GeoSolid3 front = Box(-10, -10, 20, 100, 210, 30);

            ProjectedView2 view = Project(Top, new ProjectionInput("back", back), new ProjectionInput("front", front));

            ProjectedEdge2 seen = Assert.Single(view.VisibleEdges, e => e.IsArc);
            Assert.Equal(20.0, seen.Arc.Radius, 3);
            Assert.Equal(Math.PI, seen.Arc.SweptAngle, 3);
            Assert.True(seen.Arc.StartPoint.X > 99.99 && seen.Arc.EndPoint.X > 99.99);
            Assert.Contains(view.HiddenEdges, e =>
                e.IsArc && Math.Abs(e.Arc.Radius - 20) <= 1E-3 && Math.Abs(e.Arc.SweptAngle - Math.PI) <= 1E-3
            );
        }

        [Fact]
        public void ABarThroughABoxIsHiddenInsideItAndSeenEitherSide()
        {
            GeoSolid3 box = Box(0, 0, 0, 100, 100, 100);
            GeoSolid3 bar = Box(-100, 45, 45, 200, 55, 55);

            ProjectedView2 view = Project(Top, new ProjectionInput("box", box), new ProjectionInput("bar", bar));

            Assert.Equal(2, view.HiddenEdges.Count);
            HasLine(view.HiddenEdges, 0, 45, 100, 45);
            HasLine(view.HiddenEdges, 0, 55, 100, 55);
            HasLine(view.VisibleEdges, -100, 45, 0, 45);
            HasLine(view.VisibleEdges, 100, 55, 200, 55);
            Assert.Equal(10, view.VisibleEdges.Count);
        }

        [Fact]
        public void AnEdgePiercingAFaceIsSeenAboveItAndHiddenBelowFromWhereItCrosses()
        {
            // A square rod sloping down through a plate 10 thick: its edges cross the plate's top where z is 10.
            GeoSolid3 plate = Box(0, 0, 0, 300, 200, 10);
            var placement = new GeoCoordinateSystem3(
                new GeoPoint3(100, 95, -50),
                GeoVector3.YAxis,
                new GeoVector3(-100, 0, 50)
            );
            GeoSolid3 rod = GeoSolid3.Extrude(Rectangle(0, 0, 10, 10), placement, Math.Sqrt(50 * 50 + 100 * 100), Tol);

            ProjectedView2 view = Project(Top, new ProjectionInput("plate", plate), new ProjectionInput("rod", rod));

            // Seen from above, the rod's upper long edges run from (91.05573, -45.52786) up to (141.05573, 54.47214) at
            // y = 95 and 105, rising through the plate's top, z = 10, at x = 118.81966; its lower edges lie under them.
            foreach (double y in new[] { 95.0, 105.0 })
            {
                HasLine(view.HiddenEdges, 91.05573, y, 118.81966, y);
                HasLine(view.VisibleEdges, 118.81966, y, 150, y);
            }

            Assert.Empty(Overlaps(view.VisibleEdges, view.HiddenEdges, false));
        }

        [Fact]
        public void TwoBoxesSideBySideWithFlushFacesShareTheLineBetweenThem()
        {
            ProjectedView2 view = Project(
                Front,
                new ProjectionInput("A", Box(0, 0, 0, 100, 50, 30)),
                new ProjectionInput("B", Box(100, 0, 0, 200, 50, 30))
            );

            Assert.Equal(7, view.VisibleEdges.Count);
            Assert.Empty(view.HiddenEdges);
            ProjectedEdge2 shared = Assert.Single(view.VisibleEdges, e => e.ObjectIds.Count == 2);
            Assert.Equal(new[] { "A", "B" }, shared.ObjectIds);
            HasLine(new[] { shared }, 100, 0, 100, 30);
            Assert.Empty(Overlaps(view.VisibleEdges, view.VisibleEdges, true));
        }

        [Fact]
        public void TheThreePlatesOfTC8GiveEveryLineOnceSeenOverHidden()
        {
            ProjectionInput[] plates = TestBodies
                .TC8()
                .Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid))
                .ToArray();

            ProjectedView2 front = Project(Front, plates);
            ProjectedView2 top = Project(Top, plates);

            Assert.Equal(11, front.VisibleEdges.Count);
            Assert.Empty(front.HiddenEdges);
            Assert.Equal(10, top.VisibleEdges.Count);
            Assert.Empty(top.HiddenEdges);

            foreach (ProjectedView2 view in new[] { front, top })
            {
                Assert.Empty(Overlaps(view.VisibleEdges, view.VisibleEdges, true));
            }

            // A's top edge under B's bottom one, face to face, is one line of both; A's and C's ends edge to edge are one.
            Assert.Contains(front.VisibleEdges, e =>
                e.ObjectIds.SequenceEqual(new[] { "TC8-A", "TC8-B" })
                && Near(e.StartPoint, 0, 10)
                && Near(e.EndPoint, 150, 10)
            );
            Assert.Contains(front.VisibleEdges, e =>
                e.ObjectIds.SequenceEqual(new[] { "TC8-A", "TC8-C" })
                && Near(e.StartPoint, 200, 0)
                && Near(e.EndPoint, 200, 10)
            );
        }

        [Fact]
        public void ABeamWithEndPlatesSeenFromTheFrontAndFromAboveHasTheLinesADraughtsmanDraws()
        {
            ProjectionInput[] assembly = BeamWithEndPlates();

            ProjectedView2 front = Project(Front, assembly);
            ProjectedView2 top = Project(Top, assembly);

            // From the front: each plate four lines; the beam its flanges' outer and inner edges, which meet the plates.
            Assert.Equal(12, front.VisibleEdges.Count);
            Assert.Empty(front.HiddenEdges);
            HasLine(front.VisibleEdges, 0, 90, 1000, 90);
            HasLine(front.VisibleEdges, 0, -100, 1000, -100);
            HasLine(front.VisibleEdges, 0, -110, 0, 110);

            // From above: the top flange's sides and the web hidden under it.
            Assert.Equal(10, top.VisibleEdges.Count);
            Assert.Equal(2, top.HiddenEdges.Count);
            HasLine(top.HiddenEdges, 0, 3, 1000, 3);
            HasLine(top.HiddenEdges, 0, -3, 1000, -3);
            Assert.All(top.HiddenEdges, e => Assert.Equal("beam", e.ObjectId));
        }

        [Fact]
        public void TheHolesOfTC1AndTC2ComeOutAsOneCircleOfTwentyTwoEach()
        {
            TestBody tc1 = TestBodies.TC1();
            TestBody tc2 = TestBodies.TC2();

            ProjectedView2 plate = ViewProjector.Project(
                tc1.Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid)),
                Top,
                Tol
            );
            ProjectedView2 beam = ViewProjector.Project(
                tc2.Parts.Select(p => new ProjectionInput(p.Object.Id, p.Object.Solid)),
                Right,
                Tol
            );

            Assert.Equal(4, plate.VisibleEdges.Count(e => e.IsCircle));
            Assert.All(tc1.Parts[0].HoleCentres, c => HasCircle(plate.VisibleEdges, c.X, c.Y, 11));
            Assert.Equal(6, beam.VisibleEdges.Count(e => e.IsCircle));
            Assert.All(tc2["TC2-endplate"].HoleCentres, c => HasCircle(beam.VisibleEdges, c.Y, c.Z, 11));
        }

        [Fact]
        public void TheExactAndTheHairOffPlatesOfTC9GiveTheSameLinesWhileTheHairStaysWithinTheTolerance()
        {
            // Turned by 2E-6 about X, no corner of the copy moves by more than 0.00065, within the point tolerance.
            TestBody tc9 = TestBodies.TC9(2E-6);
            GeoSolid3 exact = tc9["TC9-exact"].Object.Solid;
            GeoSolid3 hair = tc9["TC9-hair"].Object.Solid.Translate(new GeoVector3(-500, 0, 0));
            var side = new GeoCoordinateSystem3(
                GeoPoint3.Origin,
                new GeoVector3(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6), 0),
                GeoVector3.ZAxis
            );

            foreach (GeoCoordinateSystem3 frame in new[] { Top, side })
            {
                ProjectedView2 a = Project(frame, exact);
                ProjectedView2 b = Project(frame, hair);

                Assert.Equal(a.VisibleEdges.Count, b.VisibleEdges.Count);
                Assert.Equal(a.HiddenEdges.Count, b.HiddenEdges.Count);
                Assert.Equal(a.VisibleEdges.Count(e => e.IsCircle), b.VisibleEdges.Count(e => e.IsCircle));
            }
        }

        [Fact]
        public void APlateTurnedFurtherThanTheToleranceOffEdgeOnStillDrawsNoLineTwice()
        {
            // Turned by 1E-5, the far top edge of the copy stands 0.0017 over the near one seen from the side: two lines
            // within a tolerance of 0.001, but neither drawn twice, and its holes still circles from above.
            TestBody tc9 = TestBodies.TC9();
            GeoSolid3 hair = tc9["TC9-hair"].Object.Solid;
            var side = new GeoCoordinateSystem3(
                GeoPoint3.Origin,
                new GeoVector3(Math.Cos(Math.PI / 6), Math.Sin(Math.PI / 6), 0),
                GeoVector3.ZAxis
            );

            ProjectedView2 top = Project(Top, hair);
            ProjectedView2 across = Project(side, hair);

            Assert.Equal(4, top.VisibleEdges.Count(e => e.IsCircle));

            foreach (ProjectedView2 view in new[] { top, across })
            {
                Assert.Empty(Overlaps(view.VisibleEdges, view.VisibleEdges, true));
                Assert.Empty(Overlaps(view.HiddenEdges, view.HiddenEdges, true));
                Assert.Empty(Overlaps(view.VisibleEdges, view.HiddenEdges, false));
            }
        }

        [Fact]
        public void ADepthRangeLeavesOutWhatLiesWhollyBeforeOrBehindIt()
        {
            ProjectionInput near = new ProjectionInput("near", Box(0, 0, 100, 10, 10, 110));
            ProjectionInput far = new ProjectionInput("far", Box(20, 0, 0, 30, 10, 10));
            var options = new ProjectionOptions(nearDepth: 50, farDepth: -50);

            ProjectedView2 view = ViewProjector.Project(new[] { near, far }, Top, options, Tol);

            Assert.All(view.VisibleEdges, e => Assert.Equal("far", e.ObjectId));
            Assert.Equal(4, view.VisibleEdges.Count);
        }

        [Fact]
        public void TheSameObjectsInAnyOrderGiveTheSameViewToTheBit()
        {
            ProjectionInput[] assembly = BeamWithEndPlates();

            string a = Signature(Project(Iso, assembly));
            string b = Signature(Project(Iso, assembly.Reverse().ToArray()));

            Assert.Equal(a, b);
        }

        [Fact]
        public void BadInputIsRefused()
        {
            GeoSolid3 box = Box(0, 0, 0, 1, 1, 1);

            Assert.Throws<ArgumentException>(() => new ProjectionInput(null, box));
            Assert.Throws<ArgumentNullException>(() => new ProjectionInput("A", null));
            Assert.Throws<ArgumentNullException>(() => ViewProjector.Project(null, Top, Tol));
            Assert.Throws<ArgumentException>(() =>
                ViewProjector.Project(new[] { new ProjectionInput("A", box), new ProjectionInput("A", box) }, Top, Tol)
            );
            Assert.Throws<ArgumentException>(() => ViewProjector.Project(new[] { default(ProjectionInput) }, Top, Tol));
            Assert.Throws<ArgumentException>(() =>
                ViewProjector.Project(new[] { new ProjectionInput("A", box) }, default(GeoCoordinateSystem3), Tol)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectionOptions(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectionOptions(nearDepth: -1, farDepth: 1));
            var edgeOn = new ProjectionOptions(sectionPlane: new GeoPlane3(GeoPoint3.Origin, GeoVector3.XAxis));
            Assert.Throws<ArgumentException>(() =>
                ViewProjector.Project(new[] { new ProjectionInput("A", box) }, Top, edgeOn, Tol)
            );
        }

        [Fact]
        public void NothingToDrawIsAnEmptyView()
        {
            ProjectedView2 view = ViewProjector.Project(new ProjectionInput[0], Top, Tol);

            Assert.True(view.IsEmpty);
            Assert.Empty(view.VisibleEdges);
            Assert.Equal(0.0, view.Bounds.Width);
        }
    }
}
