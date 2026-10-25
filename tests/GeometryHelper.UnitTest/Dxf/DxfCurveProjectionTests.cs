using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A curve as the spline that is exactly it, and projected onto a plane along a direction or square to it, as AutoCAD's
    /// <c>Spline</c>, <c>GetProjectedCurve</c> and <c>GetOrthoProjectedCurve</c> give them.
    /// </summary>
    public class DxfCurveProjectionTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        /// <summary>The plane y + z = 0 through the origin.</summary>
        private static readonly GeoPlane3 Slope = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 1, 1));

        /// <summary>The point a point is projected to along (0, 0, 1) onto <see cref="Slope"/>.</summary>
        private static GeoPoint3 Down(GeoPoint3 p) => new GeoPoint3(p.X, p.Y, -p.Y);

        private static void SameCurve(DxfCurve expected, DxfCurve actual, Func<GeoPoint3, GeoPoint3> map, int steps)
        {
            for (int i = 0; i <= steps; i++)
            {
                double t = expected.StartParam + (expected.EndParam - expected.StartParam) * i / steps;
                GeoPoint3 want = map(expected.GetPointAtParameter(t));
                GeoPoint3 on = actual.GetClosestPointTo(want, false);
                Assert.True(on.IsEqualTo(want, Tight), "At " + t + " the curve is " + on.DistanceTo(want) + " from " + want);
            }
        }

        [Fact]
        public void TheSplineOfACurve_IsTheCurveExactly_OverItsParameters()
        {
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 4, 0));
            DxfSpline straight = line.Spline;
            Assert.Equal(1, straight.Degree);
            Assert.Equal(5.0, straight.EndParam);
            Assert.True(straight.GetPointAtParameter(2.5).IsEqualTo(line.GetPointAtParameter(2.5), Tight));

            // A circle of radius 10 is 9 control points over 0 to 2 pi, its point at each angle the circle's.
            var circle = new DxfCircle(new GeoPoint3(1, 2, 3), new GeoVector3(0, 1, 1), 10) { Layer = "steel" };
            DxfSpline round = circle.Spline;
            Assert.Equal(9, round.NumControlPoints);
            Assert.True(round.IsRational);
            Assert.True(round.Closed);
            Assert.Equal("0", round.Layer);
            Assert.Equal(2 * Math.PI, round.EndParam, 15);

            foreach (double angle in new[] { 0.3, 1.0, 2.5, 4.0, 6.0 })
            {
                Assert.Equal(10.0, round.GetPointAtParameter(angle).DistanceTo(circle.Center), 9);
            }

            Assert.True(round.GetPointAtParameter(Math.PI / 2).IsEqualTo(circle.GetPointAtParameter(Math.PI / 2), Tight));

            var arc = new DxfArc(new GeoPoint3(0, 0, 0), 5, 0.5, 2.5);
            SameCurve(arc, arc.Spline, p => p, 20);
            var ellipse = new DxfEllipse(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(8, 0, 0), 0.5, 0, 2 * Math.PI) { StartParam = 1.0, EndParam = 4.0 };
            DxfSpline oval = ellipse.Spline;
            Assert.Equal(1.0, oval.StartParam);
            Assert.True(oval.GetPointAtParameter(2.5).IsEqualTo(ellipse.GetPointAtParameter(2.5), Tight));
            SameCurve(ellipse, oval, p => p, 20);

            // A polyline with an arc: rational quadratic, its vertices at its whole parameters.
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(10, 0), 1, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(10, 10), 0, 0, 0);
            polyline.Normal = new GeoVector3(0, 0, -1);
            DxfSpline chain = polyline.Spline;
            Assert.Equal(2, chain.Degree);
            Assert.Equal(2.0, chain.EndParam);
            Assert.True(chain.GetPointAtParameter(1.0).IsEqualTo(polyline.GetPoint3dAt(1), Tight));
            SameCurve(polyline, chain, p => p, 20);

            var lines = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(3, 4, 0), new GeoPoint3(3, 4, 12) }, true);
            DxfSpline frame = lines.Spline;
            Assert.Equal(1, frame.Degree);
            Assert.Equal(3.0, frame.EndParam);
            Assert.True(frame.GetPointAtParameter(1.5).IsEqualTo(new GeoPoint3(3, 4, 6), Tight));
            Assert.Throws<NotSupportedException>(() => new DxfRay { BasePoint = new GeoPoint3(0, 0, 0), UnitDir = new GeoVector3(1, 0, 0) }.Spline);
        }

        [Fact]
        public void ALineRayAndXline_ProjectedAlongADirection_AreTheirPointsProjected()
        {
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 5, 7)) { ColorIndex = 3 };
            var down = (DxfLine)line.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1));

            Assert.True(down.StartPoint.IsEqualTo(new GeoPoint3(0, 0, 0), Tight));
            Assert.True(down.EndPoint.IsEqualTo(new GeoPoint3(10, 5, -5), Tight));
            Assert.Equal(3, down.ColorIndex);

            var flat = (DxfLine)line.GetOrthoProjectedCurve(new GeoPlane3(new GeoPoint3(0, 0, 2), new GeoVector3(0, 0, 1)));
            Assert.True(flat.EndPoint.IsEqualTo(new GeoPoint3(10, 5, 2), Tight));

            var ray = (DxfRay)new DxfRay { BasePoint = new GeoPoint3(1, 1, 1), UnitDir = new GeoVector3(0, 1, 0) }.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1));
            Assert.True(ray.BasePoint.IsEqualTo(new GeoPoint3(1, 1, -1), Tight));
            Assert.True(ray.UnitDir.IsEqualTo(new GeoVector3(0, Math.Sqrt(0.5), -Math.Sqrt(0.5)), Tight));

            Assert.Throws<InvalidOperationException>(() => new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 5)).GetProjectedCurve(Slope, new GeoVector3(0, 0, 1)));
            Assert.Throws<ArgumentException>(() => line.GetProjectedCurve(Slope, new GeoVector3(1, 0, 0)));
        }

        [Fact]
        public void ACircleProjected_IsAnEllipse_OrItselfMovedOntoAParallelPlane()
        {
            // A circle of radius 10 on the floor projected along (0, 0, 1) onto y + z = 0: an ellipse of radii 14.14 and 10.
            var circle = new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10) { Layer = "steel" };
            var ellipse = (DxfEllipse)circle.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1));

            Assert.Equal(10 * Math.Sqrt(2), ellipse.MajorAxis.Length, 9);
            Assert.Equal(Math.Sqrt(0.5), ellipse.RadiusRatio, 9);
            Assert.True(ellipse.Closed);
            Assert.Equal("steel", ellipse.Layer);
            SameCurve(circle, ellipse, Down, 24);

            var arc = new DxfArc(new GeoPoint3(1, 2, 0), 5, 0.3, 2.0);
            var part = (DxfEllipse)arc.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1));
            Assert.True(part.StartPoint.IsEqualTo(Down(arc.StartPoint), Tight));
            Assert.True(part.EndPoint.IsEqualTo(Down(arc.EndPoint), Tight));
            SameCurve(arc, part, Down, 12);

            var raised = (DxfCircle)circle.GetOrthoProjectedCurve(new GeoPlane3(new GeoPoint3(0, 0, 7), new GeoVector3(0, 0, -1)));
            Assert.True(raised.Center.IsEqualTo(new GeoPoint3(0, 0, 7), Tight));
            Assert.Equal(10.0, raised.Radius);

            var oval = new DxfEllipse(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(0, 8, 0), 0.5, 0, 2 * Math.PI) { StartParam = 0.5, EndParam = 2.0 };
            SameCurve(oval, oval.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1)), Down, 12);
            Assert.Throws<InvalidOperationException>(() => circle.GetProjectedCurve(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0)), new GeoVector3(1, 0, 0)));
        }

        [Fact]
        public void ACircleSeenEdgeOnOrNearly_IsRefused_ItsEllipseThinnerThanAutoCADTakes()
        {
            // A circle of radius 1000 standing on its edge projects square onto the floor to a line: its minor axis nought, or
            // 1E-4 of its major one for the normal (1, 0, 1E-7), below the 1E-6 AutoCAD takes. Tilted by 1E-4 it is an ellipse.
            var floor = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));

            foreach (GeoVector3 normal in new[] { new GeoVector3(0.6, 0.8, 0), new GeoVector3(1, 0, 0), new GeoVector3(1, 1, 0), new GeoVector3(1, 0, 1E-7) })
            {
                var circle = new DxfCircle(new GeoPoint3(3, 4, 5), normal, 1000);
                Assert.Throws<InvalidOperationException>(() => circle.GetOrthoProjectedCurve(floor));
                Assert.Throws<InvalidOperationException>(() => new DxfArc(new GeoPoint3(3, 4, 5), normal, 1000, 0.5, 2.0).GetOrthoProjectedCurve(floor));
            }

            var thin = (DxfEllipse)new DxfCircle(new GeoPoint3(3, 4, 5), new GeoVector3(1, 0, 1E-4), 1000).GetOrthoProjectedCurve(floor);
            Assert.Equal(1E-4, thin.RadiusRatio, 9);
            Assert.True(thin.Normal.IsParallelTo(new GeoVector3(0, 0, 1), Tight));
        }

        [Fact]
        public void ACurveProjectedToAPoint_IsRefused()
        {
            var floor = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));
            var standing = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(1, 2, 0), new GeoPoint3(1, 2, 5), new GeoPoint3(1, 2, 9) }, false);
            var upright = new DxfSpline(2, false, false, false, new[] { new GeoPoint3(1, 2, 0), new GeoPoint3(1, 2, 3), new GeoPoint3(1, 2, 9) }, new[] { 0.0, 0, 0, 1, 1, 1 }, null, 0, 0);
            var wall = new DxfPolyline { Normal = new GeoVector3(1, 0, 0) };
            wall.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            wall.AddVertexAt(1, new GeoPoint2(0, 10), 0, 0, 0);

            Assert.Throws<InvalidOperationException>(() => standing.GetOrthoProjectedCurve(floor));
            Assert.Throws<InvalidOperationException>(() => upright.GetOrthoProjectedCurve(floor));
            Assert.Throws<InvalidOperationException>(() => wall.GetOrthoProjectedCurve(floor));
            Assert.Throws<InvalidOperationException>(() => new DxfLine(new GeoPoint3(1, 2, 0), new GeoPoint3(1, 2, 9)).GetOrthoProjectedCurve(floor));
        }

        [Fact]
        public void AZigzagWithinATinyBox_ProjectedLongerThanThePointTolerance_IsKept_AsItsPolylineIs()
        {
            // Twelve corners within a box 0.00063 by 0.00054 on the floor, its diagonal under 1E-3, the zigzag some 0.008 long.
            var floor = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));
            GeoPoint3[] corners = Enumerable.Range(0, 12).Select(i => new GeoPoint3((i % 2) * 0.7 * 0.0009, (i % 3) * 0.3 * 0.0009, i)).ToArray();
            var knots = new List<double> { 0, 0 };
            knots.AddRange(Enumerable.Range(1, 10).Select(k => (double)k));
            knots.AddRange(new double[] { 11, 11 });
            var zigzag = new DxfSpline(1, false, false, false, corners, knots, null, 0, 0);
            var polyline = new DxfPolyline3d(DxfPoly3dType.SimplePoly, corners, false);

            var flat = Assert.IsType<DxfSpline>(zigzag.GetOrthoProjectedCurve(floor));

            Assert.True(flat.GetDistanceAtParameter(flat.EndParam) > 0.007);
            Assert.IsType<DxfPolyline3d>(polyline.GetOrthoProjectedCurve(floor));
        }

        [Fact]
        public void APolylineProjected_StaysOneWhereItCan_AndIsASplineOfItsArcsWhereItCannot()
        {
            var straight = new DxfPolyline();
            straight.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            straight.AddVertexAt(1, new GeoPoint2(10, 0), 0, 0, 0);
            straight.AddVertexAt(2, new GeoPoint2(10, 10), 0, 0, 0);
            var down = (DxfPolyline)straight.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1));
            Assert.True(down.GetPoint3dAt(2).IsEqualTo(new GeoPoint3(10, 10, -10), Tight));
            Assert.True(down.Normal.IsParallelTo(new GeoVector3(0, 1, 1), Tight));
            SameCurve(straight, down, Down, 8);

            var bent = new DxfPolyline();
            bent.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            bent.AddVertexAt(1, new GeoPoint2(10, 0), 1, 0, 0);
            bent.AddVertexAt(2, new GeoPoint2(10, 10), 0, 0, 0);
            var spline = (DxfSpline)bent.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1));
            SameCurve(bent, spline, Down, 24);
            var lifted = (DxfPolyline)bent.GetOrthoProjectedCurve(new GeoPlane3(new GeoPoint3(0, 0, 3), new GeoVector3(0, 0, 1)));
            Assert.Equal(3.0, lifted.Elevation);
            Assert.Equal(1.0, lifted.GetBulgeAt(1));

            var plane2d = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0) }, 0, false, 0, 0, null);
            SameCurve(plane2d, plane2d.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1)), Down, 8);

            var space = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(3, 4, 5), new GeoPoint3(3, 8, 12) }, false);
            var projected = (DxfPolyline3d)space.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1));
            Assert.True(projected.EndPoint.IsEqualTo(new GeoPoint3(3, 8, -8), Tight));
        }

        [Fact]
        public void ASplineProjected_IsItsControlPointsProjected_ItsFitDataTakenOut()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfSpline spline = ByHandle<DxfSpline>(db, "6F");

            var down = (DxfSpline)spline.GetProjectedCurve(Slope, new GeoVector3(0, 0, 1));

            Assert.False(down.HasFitData);
            Assert.Null(down.Handle);
            SameCurve(spline, down, Down, 20);
            Assert.True(spline.HasFitData);
        }
    }
}
