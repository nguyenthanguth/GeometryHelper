using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment, a ray or a line without ends lying in the plane of a spline in space is crossed where the plane has the two cross, as
    /// an arc or a chain lying there is; and the seam of a closed spline in space is no end of it.
    /// </summary>
    /// <remarks>
    /// The straight shapes were not laid in the plane with the spline: a segment along the first side of a polyline spline, 1.15E-6
    /// off it and turned by 7E-8 of a radian, was given the spline's start besides the place the plane gives.
    /// </remarks>
    public class Spline3FaultLN8Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static readonly GeoCoordinateSystem3[] Frames =
        {
            GeoCoordinateSystem3.Global,
            new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 2, 3), new GeoVector3(-2, 1, 0.5)),
        };

        private static GeoPoint3 Up(GeoCoordinateSystem3 frame, GeoPoint2 point)
            => frame.Origin.Add(frame.XAxis.Multiply(point.X)).Add(frame.YAxis.Multiply(point.Y));

        private static GeoVector3 Up(GeoCoordinateSystem3 frame, GeoVector2 direction)
            => frame.XAxis.Multiply(direction.X).Add(frame.YAxis.Multiply(direction.Y));

        /// <summary>
        /// The places in space are those of the plane: to the bit in the floor, and to a millionth in a tilted plane, whose own numbers
        /// are rounded, along a segment that runs within a millionth of a side for 20.
        /// </summary>
        private static void AssertAsThePlane(GeoCoordinateSystem3 frame, GeoPoint2[] flat, GeoPoint3[] space)
        {
            Assert.Equal(flat.Length, space.Length);
            double within = frame.Equals(GeoCoordinateSystem3.Global) ? 0.0 : 1E-6;

            foreach (GeoPoint2 place in flat)
            {
                Assert.InRange(space.Min(p => p.DistanceTo(Up(frame, place))), 0.0, within);
            }
        }

        [Fact]
        public void AStraightShapeAlongASideOfASplineInItsPlane_IsCrossedWhereThePlaneHasItCrossed()
        {
            GeoSpline2 spline = GeoSpline2.FromControlPoints(1, new[]
            {
                P(22.312184624519286, 3.1257048589762815), P(6.094810648865447, 23.89663652698353), P(-16.863499910972777, 1.6061820702656142),
                P(12.08833105493725, -15.523839479090565), P(5.284467234874365, -6.126567626430914), P(4.095892004713369, -16.702657182096807),
                P(-18.069468386503612, 12.069301634127886), P(-3.418837349591225, 12.322598766685749),
            });
            var line = new GeoLine2(P(28.08763755577209, -4.271396759370319), P(-8.836979365638442, 43.02101981249756));
            var direction = new GeoVector2(-0.6154102820235089, 0.7882069428644647);

            Assert.Single(spline.GetIntersections(line, Thousandth));

            foreach (GeoCoordinateSystem3 frame in Frames)
            {
                GeoSpline3 inSpace = spline.ToSpline3(frame);
                AssertAsThePlane(frame, spline.GetIntersections(line, Thousandth), inSpace.GetIntersections(new GeoLine3(Up(frame, line.StartPoint), Up(frame, line.EndPoint)), Thousandth));
                AssertAsThePlane(frame, spline.GetIntersections(new GeoRay2(line.StartPoint, direction), Thousandth), inSpace.GetIntersections(new GeoRay3(Up(frame, line.StartPoint), Up(frame, direction)), Thousandth));
                AssertAsThePlane(frame, spline.GetIntersections(new GeoXLine2(line.StartPoint, direction), Thousandth), inSpace.GetIntersections(new GeoXLine3(Up(frame, line.StartPoint), Up(frame, direction)), Thousandth));
            }
        }

        [Fact]
        public void ALineTangentToACircleSplineInSpaceBesideItsSeam_TouchesItOnce()
        {
            // The circle of radius 100 standing in a tilted plane, and a line of space 0.0002 off its plane, tangent to it 0.003 of a
            // radian past its seam: the seam, 4.5E-4 off, is no place.
            var frame = new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 2, 3), new GeoVector3(-2, 1, 0.5));
            GeoVector3 normal = frame.XAxis.CrossProduct(frame.YAxis).Normalize();
            GeoSpline3 ring = GeoSpline3.FromCircle(new GeoCircle3(frame.Origin, normal, 100));
            double angle = 0.003;
            GeoPoint3 touch = ring.GetPointAtParameter(angle / (2.0 * Math.PI));
            GeoVector3 along = ring.GetTangentAtParameter(angle / (2.0 * Math.PI));
            GeoPoint3 lifted = touch.Add(normal.Multiply(0.0002));
            var line = new GeoLine3(lifted.Add(along.Multiply(-50)), lifted.Add(along.Multiply(50)));

            Assert.True(ring.IsClosed(Thousandth));
            Assert.InRange(Assert.Single(ring.GetIntersections(line, Thousandth)).DistanceTo(touch), 0.0, 1E-3);
        }
    }
}
