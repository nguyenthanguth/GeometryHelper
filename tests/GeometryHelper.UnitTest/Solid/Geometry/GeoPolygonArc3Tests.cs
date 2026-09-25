using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A closed loop in space whose pieces may be arcs. Coplanarity is enforced, and that one rule is what
    /// makes everything else exact: area, what is inside, offsetting and rounding are all worked out by
    /// laying the loop out in its own plane and lifting the answer back. The tests hold each of those
    /// against the plane doing the same work.
    /// </summary>
    public class GeoPolygonArc3Tests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>
        /// A frame standing away from the origin and tilted about every axis, so nothing passes by accident
        /// of the identity.
        /// </summary>
        private static GeoCoordinateSystem3 Tilted()
            => new GeoCoordinateSystem3(new GeoPlane3(new GeoPoint3(37, -14, 52), new GeoVector3(2, -3, 6)));

        /// <summary>
        /// A square of side one hundred whose right-hand side swells into a half circle reaching x = 150.
        /// </summary>
        private static GeoPolygonArc2 Slot() => new GeoPolygonArc2(
            new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
            new[] { 0.0, 1.0, 0.0, 0.0 });

        private static GeoPolygonArc3 SlotInSpace() => new GeoPolygonArc3(Tilted(), Slot());

        /// <summary>
        /// A stirrup set out by its corners, three hundred by two hundred, lying in the XY plane.
        /// </summary>
        private static GeoPolygon3 Stirrup() => new GeoPolygon3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(300, 200, 0), new GeoPoint3(0, 200, 0));

        [Fact]
        public void ALoopLiftedIntoSpaceKeepsEveryMeasurementThePlaneGaveIt()
        {
            GeoPolygonArc2 flat = Slot();
            GeoPolygonArc3 lifted = SlotInSpace();

            Assert.Equal(flat.VertexCount, lifted.VertexCount);
            Assert.Equal(flat.EdgeCount, lifted.EdgeCount);
            Assert.Equal(flat.Length, lifted.Length, 6);
            Assert.Equal(flat.Area, lifted.Area, 5);
            Assert.Equal(flat.IsSimple(), lifted.IsSimple());
            Assert.Equal(1, lifted.GetEdges().Count(edge => edge.IsArc));

            // It lies in the plane it was lifted into.
            Assert.True(lifted.Normal.IsParallelTo(Tilted().ZAxis, Loose));
            Assert.True(lifted.GetPlane().Normal.IsParallelTo(Tilted().ZAxis, Loose));

            // And it comes back down to the loop that went up.
            Assert.True(lifted.ProjectToPolygonArc2(Tilted()).IsEqualTo(flat, Loose));

            // Its own frame gives the same area, which is what a frame turning with the shape is for.
            Assert.Equal(flat.Area, lifted.ToPolygonArc2().Area, 5);
        }

        [Fact]
        public void ALoopThatDoesNotLieFlatIsRefusedWhereItIsBuilt()
        {
            // Four corners of a box lid that do not share a plane.
            var notFlat = new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 100, 50), new GeoPoint3(0, 100, 0)
            };

            Assert.Throws<ArgumentException>(() => new GeoPolygonArc3(notFlat));

            // Flat corners, but an arc bulging out of their plane.
            var corners = new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0)
            };

            Assert.Throws<ArgumentException>(() => new GeoPolygonArc3(
                corners,
                new[] { 0.5, 0.0, 0.0, 0.0 },
                new[] { new GeoVector3(0, 1, 0), default(GeoVector3), default(GeoVector3), default(GeoVector3) }));

            // The same bulge in the plane of the loop is fine.
            var fine = new GeoPolygonArc3(
                corners,
                new[] { 0.5, 0.0, 0.0, 0.0 },
                new[] { new GeoVector3(0, 0, 1), default(GeoVector3), default(GeoVector3), default(GeoVector3) });

            Assert.Equal(1, fine.GetEdges().Count(edge => edge.IsArc));

            // Fewer than three distinct corners is no loop.
            Assert.Throws<ArgumentException>(() => new GeoPolygonArc3(new[] { corners[0], corners[1] }));
            Assert.Throws<ArgumentNullException>(() => new GeoPolygonArc3((System.Collections.Generic.IEnumerable<GeoPoint3>)null));
            Assert.Throws<ArgumentNullException>(() => new GeoPolygonArc3((GeoPolygon3)null));
        }

        [Fact]
        public void WhatIsInsideIsAnsweredInThePlaneOfTheLoopAndNowhereElse()
        {
            GeoCoordinateSystem3 frame = Tilted();
            GeoPolygonArc3 loop = SlotInSpace();

            // A point in the middle of the slot, brought up into the plane.
            GeoPoint3 inside = PlanarMap.ToPoint3(frame, new GeoPoint2(50, 50));
            GeoPoint3 outside = PlanarMap.ToPoint3(frame, new GeoPoint2(400, 50));

            Assert.Equal(PointLocation.Inside, loop.Locate(inside));
            Assert.True(loop.Contains(inside));
            Assert.Equal(PointLocation.OutSide, loop.Locate(outside));
            Assert.False(loop.Contains(outside));

            // The same point lifted off the plane is outside, whatever it is over: a flat loop encloses a
            // region of its own plane and nothing above or below it.
            GeoPoint3 above = inside.Add(frame.ZAxis.Multiply(25.0));

            Assert.Equal(PointLocation.OutSide, loop.Locate(above));
            Assert.False(loop.Contains(above));

            // A point on the outline is on it, and the distance to the outline is not nought inside.
            Assert.Equal(PointLocation.OnSide, loop.Locate(PlanarMap.ToPoint3(frame, new GeoPoint2(0, 50))));
            Assert.Equal(50.0, loop.DistanceTo(inside), 6);
        }

        [Fact]
        public void TheAreaCountsEachArcAgainstItsOwnChord()
        {
            GeoPolygonArc3 loop = SlotInSpace();

            // A square of ten thousand plus a half circle of radius fifty bulging out of one side.
            Assert.Equal(10000.0 + Math.PI * 50.0 * 50.0 / 2.0, loop.Area, 5);

            // Flattening throws the bulge away, so the area falls to the square.
            Assert.Equal(10000.0, loop.Flatten().Area, 5);

            // And the centroid is pulled towards the bulge, out past the middle of the square.
            GeoPoint2 centre = PlanarMap.ProjectToPoint2(Tilted(), loop.Centroid);

            Assert.True(centre.X > 50.0);
            Assert.Equal(Slot().Centroid.X, centre.X, 5);
            Assert.Equal(50.0, centre.Y, 5);
        }

        [Fact]
        public void AStirrupIsTheLoopWithItsCornersRounded()
        {
            // This is the shape a closed tie is: set out by its corners, bent to a radius.
            GeoPolygonArc3 tie = new GeoPolygonArc3(Stirrup()).Fillet(40.0);

            // Four corners rounded, so four arcs and four straight runs.
            Assert.Equal(4, tie.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(8, tie.EdgeCount);
            Assert.All(tie.GetEdges().Where(edge => edge.IsArc), edge => Assert.Equal(40.0, edge.ToArc().Radius, 6));

            // Every bend turns in the plane of the tie, because the tie is flat.
            Assert.All(tie.GetEdges().Where(edge => edge.IsArc),
                edge => Assert.True(edge.Normal.IsParallelTo(new GeoVector3(0, 0, 1), Loose)));

            // The bar is shorter than the set-out, by 2r - pi r / 2 at each of the four bends.
            double savedPerBend = 2.0 * 40.0 - Math.PI * 40.0 / 2.0;

            Assert.Equal(Stirrup().Length - 4.0 * savedPerBend, tie.Length, 6);

            // Rounding takes a little area off the corners.
            Assert.True(tie.Area < Stirrup().Area);

            // One radius per corner works too, and nought leaves a corner square.
            GeoPolygonArc3 mixed = new GeoPolygonArc3(Stirrup()).Fillet(new[] { 40.0, 0.0, 20.0, 0.0 });

            Assert.Equal(2, mixed.GetEdges().Count(edge => edge.IsArc));
        }

        [Fact]
        public void OffsettingMovesTheOutlineInThePlaneOfTheLoop()
        {
            GeoPolygonArc3 loop = SlotInSpace();
            GeoPolygonArc3[] grown = loop.Offset(10.0);

            Assert.Single(grown);
            Assert.True(grown[0].Area > loop.Area);
            Assert.True(grown[0].Normal.IsParallelTo(loop.Normal, Loose));

            // It still curves: offsetting a curved loop keeps the curve rather than straightening it.
            Assert.Equal(1, grown[0].GetEdges().Count(edge => edge.IsArc));

            // And the plane says the same, which is the whole promise of enforcing flatness.
            GeoPolygonArc2[] inPlane = Slot().Offset(10.0);

            Assert.Equal(inPlane.Length, grown.Length);
            Assert.Equal(inPlane[0].Area, grown[0].Area, 4);

            // Shrinking goes the other way, and eating a loop away leaves nothing.
            Assert.True(loop.Offset(-10.0)[0].Area < loop.Area);
            Assert.Empty(loop.Offset(-500.0));
        }

        [Fact]
        public void MovingTheLoopCarriesItsPlaneAndItsArcs()
        {
            GeoPolygonArc3 loop = SlotInSpace();

            foreach (GeoTransform3 move in new[]
            {
                GeoTransform3.RotationX(0.5),
                GeoTransform3.RotationAxis(new GeoVector3(1, 2, -1), 1.2),
                GeoTransform3.Translation(new GeoVector3(9, -8, 7)),
                GeoTransform3.Mirror(GeoPlane3.XY)
            })
            {
                GeoPolygonArc3 moved = loop.TransformBy(move);

                Assert.Equal(loop.Length, moved.Length, 5);
                Assert.Equal(loop.Area, moved.Area, 4);
                Assert.Equal(loop.EdgeCount, moved.EdgeCount);
                Assert.Equal(1, moved.GetEdges().Count(edge => edge.IsArc));
            }

            var by = new GeoVector3(9, -8, 7);

            Assert.True(loop.Translate(by).IsEqualTo(loop.TransformBy(GeoTransform3.Translation(by)), Loose));

            // Running the loop the other way draws the same outline. Its winding cannot be read from
            // IsClockwise, because a loop is laid out in a frame that turns with it, so what turns over is
            // the plane the loop names.
            GeoPolygonArc3 back = loop.Reverse();

            Assert.Equal(loop.Area, back.Area, 4);
            Assert.Equal(loop.Length, back.Length, 5);

            for (int i = 0; i <= 12; i++)
            {
                Assert.True(back.IsPointOn(loop.GetPointAtParameter(i / 12.0), new Tolerance(1E-6, 1E-6)), "at " + i);
            }

            Assert.True(back.Normal.IsParallelTo(loop.Normal, Loose));
            Assert.True(back.Normal.DotProduct(loop.Normal) < 0.0, "the plane did not turn over");
        }

        [Fact]
        public void TheLoopWalksAndBoxesAlongItsArcs()
        {
            GeoPolygonArc3 loop = SlotInSpace();
            GeoCoordinateSystem3 frame = Tilted();

            // Walking round it lands where the plane says it should.
            for (int i = 0; i <= 20; i++)
            {
                Assert.True(
                    loop.GetPointAtParameter(i / 20.0).IsEqualTo(
                        PlanarMap.ToPoint3(frame, Slot().GetPointAtParameter(i / 20.0)), Loose),
                    "at " + i);
            }

            // The box holds the bulge, which reaches past every vertex.
            GeoPoint3 onBulge = PlanarMap.ToPoint3(frame, new GeoPoint2(150, 50));

            Assert.True(loop.GetAabb().Contains(onBulge));
            Assert.True(loop.IsPointOn(onBulge, new Tolerance(1E-6, 1E-6)));

            // Opening it out gives a chain running once round, of the same length.
            Assert.Equal(loop.Length, loop.ToPolylineArc3().Length, 6);

            // And sampling follows the arc as closely as asked.
            Assert.True(loop.ToPolygon3(0.01).Length > loop.ToPolygon3(5.0).Length);
        }

        [Fact]
        public void TwoLoopsAreTheSameWhenEveryEdgeAgrees()
        {
            GeoPolygonArc3 loop = SlotInSpace();

            Assert.True(loop.IsEqualTo(SlotInSpace(), Loose));
            Assert.True(loop.Equals(SlotInSpace()));
            Assert.True(loop == SlotInSpace());
            Assert.Equal(loop.GetHashCode(), SlotInSpace().GetHashCode());
            Assert.True(loop.Clone().IsEqualTo(loop, Loose));

            Assert.False(loop.IsEqualTo(new GeoPolygonArc3(Stirrup()), Loose));
            Assert.True(loop != new GeoPolygonArc3(Stirrup()));
            Assert.False(loop.IsEqualTo(null, Loose));

            Assert.Contains("curved", loop.ToString());
            Assert.Throws<ArgumentNullException>(() => loop.TransformBy(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => loop.GetEdgeAt(loop.EdgeCount));
        }
    }
}
