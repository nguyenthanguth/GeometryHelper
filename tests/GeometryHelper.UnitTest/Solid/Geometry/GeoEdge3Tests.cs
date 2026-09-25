using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// One piece of a chain in space. A bulge alone does not say what arc it means once there is a third
    /// dimension, because any of the planes through the chord would satisfy it, so the plane is carried too.
    /// The promise that binds the new type to the old is that an arc laid flat in the XY plane is the same
    /// arc <see cref="GeoEdge2"/> makes of the same numbers.
    /// </summary>
    public class GeoEdge3Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoVector3 Up() => new GeoVector3(0, 0, 1);

        /// <summary>
        /// A quarter turn of radius fifty from (0, 0, 0) to (50, 50, 0), bulging in the XY plane.
        /// </summary>
        private static GeoEdge3 Quarter() => new GeoEdge3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(50, 50, 0), Math.Tan(Math.PI / 8.0), Up());

        private static GeoEdge2 QuarterFlat() => new GeoEdge2(
            new GeoPoint2(0, 0), new GeoPoint2(50, 50), Math.Tan(Math.PI / 8.0));

        [Fact]
        public void AnArcLaidFlatIsTheArcThePlaneWouldHaveMade()
        {
            GeoEdge3 space = Quarter();
            GeoEdge2 flat = QuarterFlat();

            Assert.True(space.IsArc);
            Assert.Equal(flat.Length, space.Length, 9);
            Assert.Equal(flat.ToArc().Radius, space.ToArc().Radius, 9);
            Assert.Equal(flat.ToArc().SweptAngle, space.ToArc().SweptAngle, 9);
            Assert.Equal(50.0, space.ToArc().Radius, 9);

            // Its centre is where the plane puts it, lifted onto z = 0.
            GeoPoint2 centre = flat.ToArc().Center;

            Assert.True(space.ToArc().Center.IsEqualTo(new GeoPoint3(centre.X, centre.Y, 0.0), Tight));

            // And every point along it lands in the same place.
            for (int i = 0; i <= 10; i++)
            {
                GeoPoint2 there = flat.GetPointAtParameter(i / 10.0);
                GeoPoint3 here = space.GetPointAtParameter(i / 10.0);

                Assert.True(here.IsEqualTo(new GeoPoint3(there.X, there.Y, 0.0), Tight), "at " + i);
            }
        }

        [Fact]
        public void APositiveBulgeSweepsCounterClockwiseAboutTheNormal()
        {
            // A positive bulge sweeps counter-clockwise about the normal, which bows the curve towards the
            // chord crossed with it: a chord along X about a normal along Z bows towards minus Y.
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1.0, Up());

            Assert.True(edge.GetPointAtParameter(0.5).Y < 0.0);

            // The same bulge about the other normal bows the other way.
            var other = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1.0, new GeoVector3(0, 0, -1));

            Assert.True(other.GetPointAtParameter(0.5).Y > 0.0);

            // And so does the other sign of bulge about the same normal.
            var back = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -1.0, Up());

            Assert.True(back.GetPointAtParameter(0.5).Y > 0.0);

            // A bulge of one is a half turn, so the middle stands a radius off the chord.
            Assert.Equal(-50.0, edge.GetPointAtParameter(0.5).Y, 9);
            Assert.Equal(50.0, edge.ToArc().Radius, 9);

            // The plane says the same of the same numbers.
            Assert.Equal(
                new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), 1.0).GetPointAtParameter(0.5).Y,
                edge.GetPointAtParameter(0.5).Y,
                9);
        }

        [Fact]
        public void AnArcInAnyPlaneKeepsItsShape()
        {
            // The same numbers about a normal pointing anywhere give the same radius and the same length.
            var normals = new[]
            {
                new GeoVector3(0, 0, 1), new GeoVector3(0, 1, 0), new GeoVector3(1, 0, 0),
                new GeoVector3(0, 3, 4), new GeoVector3(0, -1, 2)
            };

            foreach (GeoVector3 normal in normals)
            {
                // The chord has to lie square to the normal, so it is built from the normal itself.
                GeoVector3 along = normal.CrossProduct(new GeoVector3(1, 2, 3));

                if (along.Length < 1E-6)
                {
                    continue;
                }

                GeoPoint3 end = GeoPoint3.Origin.Add(along.Normalize().Multiply(100.0));
                var edge = new GeoEdge3(GeoPoint3.Origin, end, 1.0, normal);

                Assert.Equal(50.0, edge.ToArc().Radius, 8);
                Assert.Equal(Math.PI * 50.0, edge.Length, 8);

                // The middle of a half turn stands a radius from the middle of the chord.
                Assert.Equal(50.0, edge.GetPointAtParameter(0.5).DistanceTo(new GeoPoint3(end.X * 0.5, end.Y * 0.5, end.Z * 0.5)), 8);
            }
        }

        [Fact]
        public void WalkingBackwardsDrawsTheSameCurve()
        {
            GeoEdge3 edge = Quarter();
            GeoEdge3 back = edge.Reverse();

            Assert.Equal(edge.Length, back.Length, 9);
            Assert.Equal(edge.ToArc().Radius, back.ToArc().Radius, 9);
            Assert.True(back.StartPoint.IsEqualTo(edge.EndPoint, Tight));
            Assert.True(back.EndPoint.IsEqualTo(edge.StartPoint, Tight));

            for (int i = 0; i <= 10; i++)
            {
                Assert.True(
                    back.GetPointAtParameter(i / 10.0).IsEqualTo(edge.GetPointAtParameter(1.0 - i / 10.0), Tight),
                    "at " + i);
            }

            // The bulge turns over and the plane stays, as it does in the plane. Turning the normal over as
            // well would put the arc back on the side it started from and draw the mirror of it.
            Assert.Equal(-edge.Bulge, back.Bulge, 12);
            Assert.True(back.Normal.IsEqualTo(edge.Normal, Tight));

            var mirrored = new GeoEdge3(edge.EndPoint, edge.StartPoint, -edge.Bulge, edge.Normal.Multiply(-1.0));

            Assert.False(mirrored.GetPointAtParameter(0.5).IsEqualTo(edge.GetPointAtParameter(0.5), Tight));
        }

        [Fact]
        public void MovingTheEdgeCarriesThePlaneWithIt()
        {
            GeoEdge3 edge = Quarter();

            foreach (GeoTransform3 move in new[]
            {
                GeoTransform3.RotationX(0.7),
                GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 1.1),
                GeoTransform3.Translation(new GeoVector3(10, -20, 30)),
                GeoTransform3.Mirror(GeoPlane3.XY)
            })
            {
                GeoEdge3 moved = edge.TransformBy(move);

                Assert.True(moved.IsArc);
                Assert.Equal(edge.Length, moved.Length, 7);
                Assert.Equal(edge.ToArc().Radius, moved.ToArc().Radius, 7);

                // The middle of the arc goes where the transformation sends it, mirror or no mirror.
                Assert.True(
                    moved.GetPointAtParameter(0.5).IsEqualTo(edge.GetPointAtParameter(0.5).TransformBy(move), new Tolerance(1E-7, 1E-7)),
                    move.ToString());
            }

            // Translating is the cheap path and lands in the same place.
            var by = new GeoVector3(10, -20, 30);

            Assert.True(edge.Translate(by).GetPointAtParameter(0.5)
                .IsEqualTo(edge.TransformBy(GeoTransform3.Translation(by)).GetPointAtParameter(0.5), Tight));
        }

        [Fact]
        public void CuttingAnArcLeavesTwoArcsOfTheSameRadius()
        {
            GeoEdge3 edge = Quarter();

            Assert.True(edge.TrySplitAtParameter(0.25, out GeoEdge3 first, out GeoEdge3 second));

            Assert.Equal(edge.ToArc().Radius, first.ToArc().Radius, 8);
            Assert.Equal(edge.ToArc().Radius, second.ToArc().Radius, 8);
            Assert.Equal(edge.Length, first.Length + second.Length, 8);
            Assert.True(first.EndPoint.IsEqualTo(second.StartPoint, Tight));
            Assert.True(first.StartPoint.IsEqualTo(edge.StartPoint, Tight));
            Assert.True(second.EndPoint.IsEqualTo(edge.EndPoint, Tight));

            // A cut at either end, or outside, is no cut at all.
            Assert.False(edge.TrySplitAtParameter(0.0, out _, out _));
            Assert.False(edge.TrySplitAtParameter(1.0, out _, out _));
            Assert.False(edge.TrySplitAtParameter(1.5, out _, out _));
            Assert.False(edge.TrySplitAtParameter(double.NaN, out _, out _));

            // A straight edge cuts into two straight edges.
            var straight = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

            Assert.True(straight.TrySplitAtParameter(0.4, out GeoEdge3 near, out GeoEdge3 far));
            Assert.False(near.IsArc);
            Assert.False(far.IsArc);
            Assert.Equal(100.0, near.Length + far.Length, 9);
        }

        [Fact]
        public void TheNearestPointIsOnTheArcAndNotOnItsChord()
        {
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1.0, Up());

            // A half turn of radius fifty about (50, 0, 0), bowing to y = -50.
            var beyond = new GeoPoint3(50, -80, 0);

            Assert.True(edge.GetClosestPointOnBoundary(beyond).IsEqualTo(new GeoPoint3(50, -50, 0), Tight));
            Assert.Equal(30.0, edge.DistanceTo(beyond), 9);

            // A point on the chord is not on the arc, which is what tells the two apart.
            Assert.False(edge.IsPointOn(new GeoPoint3(50, 0, 0)));
            Assert.True(edge.IsPointOn(new GeoPoint3(50, -50, 0)));
            Assert.Equal(PointLocation.OnSide, edge.Locate(new GeoPoint3(50, -50, 0)));
            Assert.Equal(PointLocation.OutSide, edge.Locate(new GeoPoint3(50, 0, 0)));

            // Off the plane of the arc, the nearest point is still on the arc.
            Assert.Equal(Math.Sqrt(30.0 * 30.0 + 40.0 * 40.0), edge.DistanceTo(new GeoPoint3(50, -80, 40)), 9);

            // And a chord is a chord, whether the edge curves or not.
            Assert.Equal(100.0, edge.GetChord().Length, 9);
            Assert.Equal(Math.PI * 50.0, edge.Length, 9);
        }

        [Fact]
        public void TheBoxHoldsTheArcAndNotJustItsEnds()
        {
            var edge = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1.0, Up());
            GeoAabb3 box = edge.GetAabb();

            // The bulge reaches y = -50, well past both ends.
            Assert.True(box.Contains(new GeoPoint3(50, -50, 0)));
            Assert.Equal(-50.0, box.Min.Y, 6);

            var straight = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));

            Assert.Equal(0.0, straight.GetAabb().Min.Y, 9);
        }

        [Fact]
        public void AnEdgeThatCannotBeAnArcSaysSo()
        {
            var start = new GeoPoint3(0, 0, 0);
            var end = new GeoPoint3(100, 0, 0);

            // A bulge needs a plane, and a plane needs a normal with length.
            Assert.Throws<ArgumentException>(() => new GeoEdge3(start, end, 1.0, new GeoVector3(0, 0, 0)));

            // The chord lies in the plane of its own arc, so the normal has to be square to it.
            Assert.Throws<ArgumentException>(() => new GeoEdge3(start, end, 1.0, new GeoVector3(1, 0, 0)));
            Assert.Throws<ArgumentException>(() => new GeoEdge3(start, end, 1.0, new GeoVector3(1, 0, 1)));

            // Two points in the same place have no chord to bulge.
            Assert.Throws<ArgumentException>(() => new GeoEdge3(start, start, 1.0, Up()));

            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEdge3(start, end, double.NaN, Up()));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEdge3(start, end, double.PositiveInfinity, Up()));

            // A straight edge has no arc and no plane; an arc is not a segment.
            var straight = new GeoEdge3(start, end);

            Assert.Throws<InvalidOperationException>(() => straight.ToArc());
            Assert.Throws<InvalidOperationException>(() => straight.GetPlane());
            Assert.Throws<InvalidOperationException>(() => Quarter().ToLine());

            // A straight edge ignores whatever normal it is handed, because it bulges in no plane.
            Assert.False(new GeoEdge3(start, end, 0.0, new GeoVector3(1, 0, 0)).IsArc);
        }

        [Fact]
        public void AnArcInSpaceCanBeTakenApartAndPutBackTogether()
        {
            GeoEdge3 edge = Quarter();
            GeoArc3 arc = edge.ToArc();

            // Round tripping through GeoArc3 keeps the edge.
            var again = new GeoEdge3(arc);

            Assert.True(again.IsEqualTo(edge, new Tolerance(1E-7, 1E-7)));
            Assert.True(again.Normal.IsEqualTo(edge.Normal, new Tolerance(1E-7, 1E-7)));

            // The plane the arc bulges in is the plane the edge names.
            Assert.True(edge.GetPlane().Normal.IsParallelTo(Up(), Tight));
        }

        [Fact]
        public void TwoEdgesAreTheSameWhenTheirEndsAndTheirBulgeAndItsPlaneAgree()
        {
            GeoEdge3 edge = Quarter();

            Assert.True(edge.IsEqualTo(Quarter(), Tight));
            Assert.True(edge.Equals(Quarter()));
            Assert.True(edge == Quarter());
            Assert.Equal(edge.GetHashCode(), Quarter().GetHashCode());
            Assert.True(edge.Clone().IsEqualTo(edge, Tight));

            // The same ends and the same bulge, in the other plane, is not the same edge.
            var other = new GeoEdge3(edge.StartPoint, edge.EndPoint, edge.Bulge, new GeoVector3(1, -1, 0));

            Assert.False(edge.IsEqualTo(other, Tight));
            Assert.True(edge != other);

            // A straight edge and a curved one between the same points are not the same either.
            Assert.False(edge.IsEqualTo(new GeoEdge3(edge.StartPoint, edge.EndPoint), Tight));

            Assert.Contains("bulge", edge.ToString());
            Assert.DoesNotContain("bulge", new GeoEdge3(edge.StartPoint, edge.EndPoint).ToString());
        }
    }
}
