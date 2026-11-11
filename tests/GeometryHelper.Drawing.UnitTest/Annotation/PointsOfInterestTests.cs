using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class PointsOfInterestTests
    {
        [Fact]
        public void ThePlateOfTC1SeenFromAboveHasFourCornersFourHoleCentresAndTwoWorkPoints()
        {
            DrawingObject[] objects = Objects(TestBodies.TC1());
            IReadOnlyList<DimensionPoint> points = Annotator.FindPoints(Project(objects, Top), objects, Top, Tol);

            DimensionPoint[] corners = points.Where(p => p.Kind == DimensionPointKind.Corner).ToArray();
            Assert.Equal(
                new[] { (0.0, 0.0), (0.0, 200.0), (300.0, 0.0), (300.0, 200.0) },
                corners.Select(p => (Round(p.ViewPoint.X), Round(p.ViewPoint.Y)))
            );

            // Each corner's model point is the corner of the top, the one nearest the viewer.
            Assert.All(corners, p => Near(20.0, p.ModelPoint.Z));

            DimensionPoint[] holes = points.Where(p => p.Kind == DimensionPointKind.HoleCentre).ToArray();
            Assert.Equal(
                new[] { (50.0, 50.0), (50.0, 150.0), (250.0, 50.0), (250.0, 150.0) },
                holes.Select(p => (Round(p.ViewPoint.X), Round(p.ViewPoint.Y)))
            );
            Assert.All(holes, p => Near(20.0, p.ModelPoint.Z));
            Assert.All(holes, p => Assert.Equal("TC1-plate", p.ObjectId));

            DimensionPoint[] work = points.Where(p => p.Kind == DimensionPointKind.WorkPoint).ToArray();
            Assert.Equal(
                new[] { (0.0, 0.0), (300.0, 0.0) },
                work.Select(p => (Round(p.ViewPoint.X), Round(p.ViewPoint.Y)))
            );
            Assert.DoesNotContain(points, p =>
                p.Kind == DimensionPointKind.BoltCentre || p.Kind == DimensionPointKind.PartEnd
            );
        }

        [Fact]
        public void ABoltSeenAcrossHasItsCentreHalfWayAlongItsAxisAndSeenAlongItAtItsCircle()
        {
            DrawingObject[] objects = Objects(TestBodies.TC5());

            // The bolts run from 30 below the base plate to 50 up, so their middles are 10 up.
            IReadOnlyList<DimensionPoint> front = Annotator.FindPoints(Project(objects, Front), objects, Front, Tol);
            DimensionPoint[] bolts = front.Where(p => p.Kind == DimensionPointKind.BoltCentre).ToArray();
            Assert.Equal(4, bolts.Length);
            Assert.All(bolts, p => Near(10.0, p.ViewPoint.Y));
            Assert.All(bolts, p => Near(150.0, System.Math.Abs(p.ViewPoint.X)));

            IReadOnlyList<DimensionPoint> top = Annotator.FindPoints(Project(objects, Top), objects, Top, Tol);
            DimensionPoint bolt = top.Single(p =>
                p.Kind == DimensionPointKind.BoltCentre && p.ObjectId == "TC5-bolt-4"
            );
            Near(150.0, bolt.ViewPoint.X);
            Near(150.0, bolt.ViewPoint.Y);

            // The base plate's hole round the bolt is a centre of its own, at the same place.
            Assert.Contains(top, p =>
                p.Kind == DimensionPointKind.HoleCentre
                && p.ObjectId == "TC5-base"
                && System.Math.Abs(p.ViewPoint.X - 150) <= Tol.EqualPoint
                && System.Math.Abs(p.ViewPoint.Y - 150) <= Tol.EqualPoint
            );
        }

        [Fact]
        public void ALineEndingWhereAPartInFrontHidesItEndsAtNoCorner()
        {
            // A box 300 long, and in front of it, nearer the viewer at -Y, a taller one from 100 to 200: the long box's top
            // edge is seen from 0 to 100 and from 200 to 300, its pieces ending at 100 and 200, where it has no corner.
            DrawingObject[] objects =
            {
                Part("long", Box(0, 0, 0, 300, 100, 100), true),
                Part("front", Box(100, -50, 0, 200, -10, 200)),
            };

            IReadOnlyList<DimensionPoint> points = Annotator.FindPoints(Project(objects, Front), objects, Front, Tol);
            DimensionPoint[] corners = points
                .Where(p => p.ObjectId == "long" && p.Kind == DimensionPointKind.Corner)
                .ToArray();

            Assert.Equal(
                new[] { (0.0, 0.0), (0.0, 100.0), (300.0, 0.0), (300.0, 100.0) },
                corners.Select(p => (Round(p.ViewPoint.X), Round(p.ViewPoint.Y)))
            );
        }

        [Fact]
        public void EveryPointOfTheTestBodiesLiesInTheViewWhereItsModelPointProjects()
        {
            foreach (
                TestBody body in new[]
                {
                    TestBodies.TC1(),
                    TestBodies.TC2(),
                    TestBodies.TC4(),
                    TestBodies.TC5(),
                    TestBodies.TC6(),
                    TestBodies.TC9(),
                }
            )
            {
                DrawingObject[] objects = Objects(body);

                foreach ((string _, GeoCoordinateSystem3 frame) in MainViews(body.ToSource().Frame))
                {
                    foreach (DimensionPoint p in Annotator.FindPoints(Project(objects, frame), objects, frame, Tol))
                    {
                        GeoPoint3 local = frame.ToLocal(p.ModelPoint);
                        Assert.True(
                            System.Math.Abs(local.X - p.ViewPoint.X) <= Tol.EqualPoint
                                && System.Math.Abs(local.Y - p.ViewPoint.Y) <= Tol.EqualPoint,
                            body.Name + " " + p
                        );
                    }
                }
            }
        }

        private static double Round(double value) => System.Math.Round(value, 6) + 0.0;
    }
}
