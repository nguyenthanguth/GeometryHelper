using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The arc in space: that it lies in its plane, measures what its flat counterpart measures, and comes
    /// back the same after being built from three points, turned, moved or flattened.
    /// </summary>
    public class GeoArc3Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-8, 1E-8);

        private static GeoArc3 Quarter() => new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 10.0, 0.0, Math.PI / 2.0);

        private static GeoArc3 Tilted() => new GeoArc3(
            new GeoPoint3(12, -8, 30), new GeoVector3(1, 1, 1), 250.0, 0.3, 2.4);

        [Fact]
        public void AnArcLiesInItsOwnPlane()
        {
            GeoArc3 arc = Tilted();
            GeoPlane3 plane = arc.GetPlane();

            foreach (double t in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
            {
                GeoPoint3 point = arc.GetPointAtParameter(t);
                Assert.Equal(0.0, plane.SignedDistanceTo(point), 8);
                Assert.Equal(arc.Radius, arc.Center.DistanceTo(point), 8);
            }

            Assert.True(arc.StartPoint.IsEqualTo(arc.GetPointAtParameter(0.0), Tight));
            Assert.True(arc.EndPoint.IsEqualTo(arc.GetPointAtParameter(1.0), Tight));
            Assert.True(arc.MidPoint.IsEqualTo(arc.GetPointAtParameter(0.5), Tight));
        }

        [Fact]
        public void ItMeasuresWhatTheFlatOneMeasures()
        {
            GeoArc3 arc = Quarter();

            Assert.Equal(Math.PI / 2.0 * 10.0, arc.Length, 9);
            Assert.Equal(Math.PI / 2.0, arc.SweptAngle, 12);
            Assert.False(arc.IsClockwise);
            Assert.Equal(10.0, arc.GetCircle().Radius, 12);
            Assert.Equal(Math.Sqrt(200.0), arc.GetChord().Length, 9);

            Assert.Equal(arc.Length * 0.5, arc.GetDistanceAtParameter(0.5), 9);
            Assert.Equal(0.5, arc.GetParameterAtDistance(arc.Length * 0.5), 9);
            Assert.True(arc.GetPointAtDistance(arc.Length * 0.5).IsEqualTo(arc.MidPoint, Tight));
        }

        [Fact]
        public void FromThreePoints_PassesThroughAllThreeWhereverTheyLie()
        {
            var start = new GeoPoint3(10, 0, 5);
            var middle = new GeoPoint3(0, 10, 5);
            var end = new GeoPoint3(-10, 0, 5);

            GeoArc3 arc = GeoArc3.FromThreePoints(start, middle, end);

            Assert.True(arc.StartPoint.IsEqualTo(start, Tight));
            Assert.True(arc.MidPoint.IsEqualTo(middle, Tight));
            Assert.True(arc.EndPoint.IsEqualTo(end, Tight));
            Assert.Equal(10.0, arc.Radius, 8);
            Assert.True(arc.Center.IsEqualTo(new GeoPoint3(0, 0, 5), Tight));

            // And on a plane that is no axis plane at all.
            var a = new GeoPoint3(3, 1, 2);
            var b = new GeoPoint3(6, 5, 9);
            var c = new GeoPoint3(1, 8, 4);
            GeoArc3 tilted = GeoArc3.FromThreePoints(a, b, c);

            Assert.True(tilted.StartPoint.IsEqualTo(a, Tight));
            Assert.True(tilted.EndPoint.IsEqualTo(c, Tight));

            // The arc passes through the middle point, which is not the same as it being halfway along:
            // three points fix the curve, not how the length is shared between them.
            Assert.True(tilted.IsPointOn(b));
            Assert.Equal(0.0, tilted.DistanceTo(b), 8);
            Assert.InRange(tilted.GetParameterAtPoint(b), 0.0, 1.0);
        }

        [Fact]
        public void FromThreePoints_RefusesPointsOnOneLine()
        {
            Assert.Throws<InvalidOperationException>(() => GeoArc3.FromThreePoints(
                new GeoPoint3(0, 0, 0), new GeoPoint3(2, 2, 2), new GeoPoint3(5, 5, 5)));
        }

        [Fact]
        public void TheNearestPointIsOnTheArcOrAtOneOfItsEnds()
        {
            GeoArc3 arc = Quarter();

            // A plane in space picks its own reference direction, so where the zero angle sits is not the
            // world X axis and must not be assumed: everything here is measured from the arc's own points.
            GeoVector3 outward = arc.Center.GetVectorTo(arc.MidPoint);

            // Straight out through the middle of the arc.
            Assert.True(arc.GetClosestPointOnBoundary(arc.Center.Add(outward.Multiply(10.0))).IsEqualTo(arc.MidPoint, Tight));

            // Round past the end: the nearer end answers, not the far side of the circle.
            GeoPoint3 pastTheEnd = arc.GetPointAtParameter(1.4);
            Assert.True(arc.GetClosestPointOnBoundary(pastTheEnd).IsEqualTo(arc.EndPoint, Tight));
            Assert.True(arc.GetClosestPointOnBoundary(arc.GetPointAtParameter(-0.4)).IsEqualTo(arc.StartPoint, Tight));

            // Off the plane: only the direction within the plane decides where along the arc it falls, and
            // the distance counts the height as well.
            GeoPoint3 above = arc.MidPoint.Add(arc.Normal.Multiply(30.0));
            Assert.True(arc.GetClosestPointOnBoundary(above).IsEqualTo(arc.MidPoint, Tight));
            Assert.Equal(30.0, arc.DistanceTo(above), 8);

            Assert.True(arc.IsPointOn(arc.MidPoint));
            Assert.False(arc.IsPointOn(arc.GetPointAtParameter(1.5)));
            Assert.Equal(PointLocation.OnSide, arc.Locate(arc.StartPoint));
            Assert.Equal(PointLocation.OutSide, arc.Locate(arc.Center));
        }

        [Fact]
        public void ReversingSwapsTheEndsAndOffsettingKeepsTheCentre()
        {
            GeoArc3 arc = Tilted();
            GeoArc3 back = arc.Reverse();

            Assert.True(back.StartPoint.IsEqualTo(arc.EndPoint, Tight));
            Assert.True(back.EndPoint.IsEqualTo(arc.StartPoint, Tight));
            Assert.Equal(arc.Length, back.Length, 8);
            Assert.NotEqual(arc.IsClockwise, back.IsClockwise);

            Assert.True(arc.TryOffset(50.0, out GeoArc3 wider));
            Assert.Equal(300.0, wider.Radius, 9);
            Assert.True(wider.Center.IsEqualTo(arc.Center, Tight));
            Assert.Equal(arc.SweptAngle, wider.SweptAngle, 12);

            Assert.False(arc.TryOffset(-250.0, out GeoArc3 refused));
            Assert.True(refused.IsEqualTo(arc));
        }

        [Fact]
        public void ARigidMotionLeavesEveryMeasurementAlone()
        {
            GeoArc3 arc = Tilted();
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(100, -40, 15))
                .Multiply(GeoTransform3.RotationAxis(new GeoPoint3(5, 5, 5), new GeoVector3(1, 2, 3), 0.9));

            GeoArc3 moved = arc.TransformBy(motion);

            Assert.Equal(arc.Radius, moved.Radius, 8);
            Assert.Equal(arc.Length, moved.Length, 8);
            Assert.True(moved.StartPoint.IsEqualTo(motion.Transform(arc.StartPoint), Tight));
            Assert.True(moved.MidPoint.IsEqualTo(motion.Transform(arc.MidPoint), Tight));
            Assert.True(moved.EndPoint.IsEqualTo(motion.Transform(arc.EndPoint), Tight));

            // A uniform scaling multiplies the radius with it.
            GeoArc3 scaled = arc.TransformBy(GeoTransform3.Scaling(2.0));
            Assert.Equal(arc.Radius * 2.0, scaled.Radius, 7);
            Assert.Equal(arc.Length * 2.0, scaled.Length, 7);

            Assert.Throws<ArgumentNullException>(() => arc.TransformBy(null));
        }

        [Fact]
        public void FlatteningIntoAFrameGivesTheArcItsPlanViewWouldDraw()
        {
            GeoArc3 arc = Tilted();
            var frame = new GeoCoordinateSystem3(arc.GetPlane());

            GeoArc2 flat = arc.ProjectToArc2(frame);

            Assert.Equal(arc.Radius, flat.Radius, 6);
            Assert.Equal(arc.Length, flat.Length, 6);
            Assert.Equal(arc.SweptAngle, flat.SweptAngle, 6);
        }

        [Fact]
        public void ItCutsItselfIntoStraightPiecesTheSameThreeWays()
        {
            var arc = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 1000.0, 0.0, Math.PI / 2.0);

            GeoPolyline3 automatic = arc.ToPolyline();
            Assert.True(automatic[0].IsEqualTo(arc.StartPoint, Tight));
            Assert.True(automatic[automatic.VertexCount - 1].IsEqualTo(arc.EndPoint, Tight));
            Assert.InRange(automatic.VertexCount, 10, 20);

            GeoPolyline3 spaced = arc.ToPolylineBySpacing(100.0);
            Assert.True(arc.Length / (spaced.VertexCount - 1) <= 100.0 + 1E-9);

            Assert.Equal(9, arc.ToPolyline(8).VertexCount);
            Assert.True(arc.ToPolylineByChordTolerance(0.5).VertexCount > automatic.VertexCount);

            // Every piece stays in the plane of the arc.
            GeoPlane3 plane = arc.GetPlane();
            foreach (GeoPoint3 point in automatic.Vertices)
            {
                Assert.Equal(0.0, plane.SignedDistanceTo(point), 8);
            }
        }

        [Fact]
        public void TheTwoDimensionsDescribeTheSameCurve()
        {
            // The same quarter turn, once in the XY plane and once as a flat arc.
            GeoArc3 inSpace = Quarter();
            var flat = new GeoArc2(GeoPoint2.Origin, 10.0, 0.0, Math.PI / 2.0);

            Assert.Equal(flat.Length, inSpace.Length, 9);
            Assert.Equal(flat.SweptAngle, inSpace.SweptAngle, 12);
            Assert.Equal(flat.ToPolyline().VertexCount, inSpace.ToPolyline().VertexCount);
            Assert.Equal(flat.ToPolylineBySpacing(2.0).VertexCount, inSpace.ToPolylineBySpacing(2.0).VertexCount);
        }

        [Fact]
        public void TheArgumentsAreCheckedAndEqualityComparesTheCurve()
        {
            Assert.Throws<ArgumentException>(() => new GeoArc3(GeoPoint3.Origin, GeoVector3.Zero, 5.0, 0.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 0.0, 0.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 5.0, double.NaN, 1.0));

            GeoArc3 arc = Tilted();

            Assert.Equal(arc, arc.Clone());
            Assert.Equal(arc.GetHashCode(), arc.Clone().GetHashCode());
            Assert.True(arc == arc.Clone());
            Assert.False(arc != arc.Clone());
            Assert.True(arc.IsEqualTo(arc.Clone()));
            Assert.False(arc.IsEqualTo(arc.Reverse()));
            Assert.False(arc.Equals("not an arc"));
            Assert.Contains("GeoArc3", arc.ToString());
        }

        [Fact]
        public void TheBoundingBoxHoldsTheArcRatherThanTheWholeCircle()
        {
            // A quarter turn in the XY plane, built from points so that nothing is assumed about where a
            // plane starts measuring.
            GeoArc3 quarter = GeoArc3.FromThreePoints(
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10 / Math.Sqrt(2.0), 10 / Math.Sqrt(2.0), 0),
                new GeoPoint3(0, 10, 0));

            GeoAabb3 box = quarter.GetAabb();

            // The circle would span -10 to 10 both ways; the quarter only spans 0 to 10.
            Assert.Equal(0.0, box.Min.X, 8);
            Assert.Equal(0.0, box.Min.Y, 8);
            Assert.Equal(10.0, box.Max.X, 8);
            Assert.Equal(10.0, box.Max.Y, 8);
            Assert.Equal(0.0, box.Min.Z, 8);
            Assert.Equal(0.0, box.Max.Z, 8);

            // Every point of the arc is inside it.
            for (int i = 0; i <= 50; i++)
            {
                GeoPoint3 point = quarter.GetPointAtParameter(i / 50.0);
                Assert.True(point.X >= box.Min.X - 1E-9 && point.X <= box.Max.X + 1E-9);
                Assert.True(point.Y >= box.Min.Y - 1E-9 && point.Y <= box.Max.Y + 1E-9);
            }

            // An arc sweeping the whole way round is bounded like its circle.
            var full = new GeoArc3(GeoPoint3.Origin, GeoVector3.ZAxis, 10.0, 1.0, 1.0);
            GeoAabb3 whole = full.GetAabb();
            Assert.Equal(-10.0, whole.Min.X, 6);
            Assert.Equal(10.0, whole.Max.X, 6);

            // And a tilted arc stays inside the box its own points give.
            GeoArc3 tilted = Tilted();
            GeoAabb3 tiltedBox = tilted.GetAabb();
            for (int i = 0; i <= 100; i++)
            {
                GeoPoint3 point = tilted.GetPointAtParameter(i / 100.0);
                Assert.True(point.X >= tiltedBox.Min.X - 1E-8 && point.X <= tiltedBox.Max.X + 1E-8);
                Assert.True(point.Y >= tiltedBox.Min.Y - 1E-8 && point.Y <= tiltedBox.Max.Y + 1E-8);
                Assert.True(point.Z >= tiltedBox.Min.Z - 1E-8 && point.Z <= tiltedBox.Max.Z + 1E-8);
            }
        }
    }
}
