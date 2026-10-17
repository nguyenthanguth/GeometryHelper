using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An edge with a bulge that is not nought is an arc, however little it bows. These arcs were built through the
    /// three points of their start, middle and end, a solve that refuses three points within the global point tolerance
    /// of one line: an edge 0.01 long with a bulge of a thousandth bows 0.000005 off its chord, and every question asked
    /// of it threw. They are built from their circle now. A positive bulge turns counter-clockwise round the normal,
    /// so these edges, running along +X with the normal +Z, bow towards -Y.
    /// </summary>
    public class FlatArc3Tests
    {
        private static readonly Tolerance Fine = new Tolerance(1E-4, 1E-6);

        /// <summary>The edge from the origin to (0.01, 0, 0), bowing by a thousandth towards -Y in the plane z = 0.</summary>
        private static GeoEdge3 Short() => new GeoEdge3(GeoPoint3.Origin, new GeoPoint3(0.01, 0, 0), 1E-3, GeoVector3.ZAxis);

        /// <summary>The edge from the origin to (100, 0, 0), bowing by a billionth: 0.00000005 off its chord.</summary>
        private static GeoEdge3 Long() => new GeoEdge3(GeoPoint3.Origin, new GeoPoint3(100, 0, 0), 1E-9, GeoVector3.ZAxis);

        private static double Sweep(double bulge) => 4 * Math.Atan(bulge);

        private static double Radius(double chord, double bulge) => chord / (2 * Math.Sin(Sweep(bulge) / 2));

        [Fact]
        public void AnEdgeOneHundredthLongBowingByAThousandth_IsTheArcOfItsBulge()
        {
            GeoArc3 arc = Short().ToArc();

            Assert.Equal(Radius(0.01, 1E-3), arc.Radius, 12);
            Assert.Equal(Sweep(1E-3), arc.SweptAngle, 15);
            Assert.True(arc.StartPoint.DistanceTo(GeoPoint3.Origin) < 1E-15);
            Assert.True(arc.EndPoint.DistanceTo(new GeoPoint3(0.01, 0, 0)) < 1E-15);

            // The middle bows half the chord times the bulge off it: 0.000005. An arc of radius 2.5 places its points
            // within a few parts in 1E16 of that.
            Assert.True(arc.MidPoint.DistanceTo(new GeoPoint3(0.005, -5E-6, 0)) < 1E-14);
        }

        [Fact]
        public void AnEdgeOneHundredLongBowingByABillionth_IsTheArcOfItsBulge()
        {
            GeoArc3 arc = Long().ToArc();

            // An arc kept as its centre and radius places its points within about its radius times 1E-16 of where
            // they are, as in the plane: here 1E-5 for a radius of 2.5E10, inside the point tolerance.
            Assert.Equal(1.0, arc.Radius / Radius(100, 1E-9), 9);
            Assert.True(arc.MidPoint.DistanceTo(new GeoPoint3(50, -5E-8, 0)) < arc.Radius * 1E-15);
            Assert.True(arc.EndPoint.DistanceTo(new GeoPoint3(100, 0, 0)) < arc.Radius * 1E-15);
        }

        [Fact]
        public void AFlatEdge_IsMeasuredAlongItsArc()
        {
            Assert.Equal(Sweep(1E-3) * Radius(0.01, 1E-3), Short().Length, 15);
            Assert.Equal(Sweep(1E-9) * Radius(100, 1E-9), Long().Length, 10);
            Assert.True(Short().MidPoint.DistanceTo(new GeoPoint3(0.005, -5E-6, 0)) < 1E-14);
        }

        [Fact]
        public void ALoopWithAFlatSide_IsMeasuredRoundItsArc()
        {
            var loop = new GeoPolygonArc3(
                new[] { GeoPoint3.Origin, new GeoPoint3(0.01, 0, 0), new GeoPoint3(0.01, 0.01, 0), new GeoPoint3(0, 0.01, 0) },
                new[] { -1E-3, 0, 0, 0 },
                new[] { GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis },
                Fine);

            Assert.Equal(0.03 + Sweep(1E-3) * Radius(0.01, 1E-3), loop.Length, 15);
        }

        [Fact]
        public void ABarWithAFlatBend_CollidesWithABoxItRunsInto()
        {
            // A bend 1 long bowing a millionth: 0.0000005 off its chord.
            var bar = new GeoPolylineArc3(
                new[] { GeoPoint3.Origin, new GeoPoint3(1, 0, 0), new GeoPoint3(1, 1, 0) },
                new[] { 1E-6, 0 },
                new[] { GeoVector3.ZAxis, GeoVector3.ZAxis });
            var box = new GeoAabb3(new GeoPoint3(0.4, -0.1, -0.1), new GeoPoint3(0.6, 0.1, 0.1));

            Assert.True(bar.CollidesWith(box, Fine));
            Assert.Single(bar.GetIntersections(new GeoPlane3(new GeoPoint3(0.5, 0, 0), GeoVector3.XAxis), Fine));
        }

        [Fact]
        public void AFlatEdgeTurnedAboutAnAxis_KeepsItsBulgeAndLandsWhereItsPointsLand()
        {
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1), 0.7);
            GeoEdge3 edge = Short();
            GeoEdge3 turned = edge.TransformBy(turn);

            Assert.Equal(1.0, turned.Bulge / edge.Bulge, 10);
            Assert.True(turned.StartPoint.DistanceTo(turn.Transform(edge.StartPoint)) < 1E-12);
            Assert.True(turned.EndPoint.DistanceTo(turn.Transform(edge.EndPoint)) < 1E-12);
            Assert.True(turned.MidPoint.DistanceTo(turn.Transform(edge.MidPoint)) < 1E-12);
        }

        [Fact]
        public void AFlatEdgeMirrored_BowsTheWayItsMirrorImageDoes()
        {
            // Mirrored in the plane x = 0: the edge runs from the origin to (-0.01, 0, 0) and still bows towards -Y.
            GeoTransform3 mirror = GeoTransform3.Scaling(-1, 1, 1);
            GeoEdge3 edge = Short();
            GeoEdge3 mirrored = edge.TransformBy(mirror);

            Assert.True(mirrored.MidPoint.DistanceTo(new GeoPoint3(-0.005, -5E-6, 0)) < 1E-14);
            Assert.True(mirrored.EndPoint.DistanceTo(new GeoPoint3(-0.01, 0, 0)) < 1E-15);
        }

        [Fact]
        public void AFlatArcTurnedAboutAnAxis_LandsWhereItsPointsLand()
        {
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1), 0.7);
            GeoArc3 arc = Short().ToArc();
            GeoArc3 turned = arc.TransformBy(turn);

            Assert.Equal(arc.Radius, turned.Radius, 12);
            Assert.Equal(1.0, Math.Abs(turned.SweptAngle / arc.SweptAngle), 10);
            Assert.True(turned.StartPoint.DistanceTo(turn.Transform(arc.StartPoint)) < 1E-12);
            Assert.True(turned.MidPoint.DistanceTo(turn.Transform(arc.MidPoint)) < 1E-12);
            Assert.True(turned.EndPoint.DistanceTo(turn.Transform(arc.EndPoint)) < 1E-12);
        }

        [Fact]
        public void AFlatArcLaidOutInItsOwnPlane_IsTheSameArc()
        {
            var frame = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis, GeoVector3.YAxis);
            GeoArc2 flat = Short().ToArc().ProjectToArc2(frame);

            // Its three points lie within the point tolerance of one line, so it is laid out from its ends and sweep.
            Assert.Equal(1.0, flat.Radius / Radius(0.01, 1E-3), 9);
            Assert.True(flat.MidPoint.DistanceTo(new GeoPoint2(0.005, -5E-6)) < 1E-14);
        }

        [Fact]
        public void AnArcBowingABillionthLaidOutInItsOwnPlane_RunsBetweenItsEnds()
        {
            // Its three points lie on one line to the last digit, so it is laid out from its ends and its sweep.
            var frame = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 0), GeoVector3.XAxis, GeoVector3.YAxis);
            GeoArc2 flat = Long().ToArc().ProjectToArc2(frame);

            Assert.True(flat.StartPoint.DistanceTo(new GeoPoint2(0, 0)) < flat.Radius * 1E-15);
            Assert.True(flat.EndPoint.DistanceTo(new GeoPoint2(100, 0)) < flat.Radius * 1E-15);
            Assert.Equal(Sweep(1E-9), flat.SweptAngle, 15);
        }

        [Fact]
        public void ABarWithABendBowingATrillionthTurned_StaysInOnePiece()
        {
            // The moved arc of radius 2.5E13 places its own ends only within 0.005 of where they are; the bar keeps the
            // moved ends, so its legs still meet.
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1), 0.7);
            var bar = new GeoPolylineArc3(
                new[] { GeoPoint3.Origin, new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0) },
                new[] { 1E-12, 0 },
                new[] { GeoVector3.ZAxis, GeoVector3.ZAxis });
            GeoPolylineArc3 turned = bar.TransformBy(turn);

            Assert.Equal(3, turned.VertexCount);
            Assert.True(turned[1].DistanceTo(turn.Transform(new GeoPoint3(100, 0, 0))) < 1E-12);
            // Read again from the moved points, a sag of 0.00000000005 comes out within 1E-15 of it.
            Assert.True(Math.Abs(turned.GetEdgeAt(0).Bulge - 1E-12) * 50 < 1E-15);
        }

        [Fact]
        public void AFlatArcLiftedIntoSpace_IsTheSameArcStandingInTheFrame()
        {
            var frame = new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), GeoVector3.XAxis, GeoVector3.ZAxis);
            GeoArc2 flat = GeoArc2.FromBulge(new GeoPoint2(0, 0), new GeoPoint2(0.01, 0), 1E-3);
            GeoArc3 lifted = PlanarMap.ToArc3(frame, flat);

            Assert.Equal(flat.Radius, lifted.Radius, 12);
            Assert.True(lifted.MidPoint.DistanceTo(PlanarMap.ToPoint3(frame, flat.MidPoint)) < 1E-15);
        }

        [Fact]
        public void AWholeCircleLiftedIntoSpace_IsAWholeTurn()
        {
            var frame = new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), GeoVector3.XAxis, GeoVector3.ZAxis);
            var whole = new GeoArc2(new GeoPoint2(5, 0), 5, 0, 0);
            GeoArc3 lifted = PlanarMap.ToArc3(frame, whole);

            Assert.Equal(2 * Math.PI, lifted.SweptAngle, 15);
            Assert.Equal(5, lifted.Radius, 15);
            Assert.True(lifted.Center.DistanceTo(new GeoPoint3(15, 20, 30)) < 1E-15);
        }

        [Fact]
        public void AWholeTurnMoved_IsAWholeTurnWhereItsCircleLands()
        {
            // Its start and its end are one point, so three of its points never settled where it went.
            var whole = new GeoArc3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 1, 1), 5, 0.3, 0.3);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1), 0.7);
            GeoArc3 moved = whole.TransformBy(turn);

            Assert.Equal(2 * Math.PI, moved.SweptAngle, 15);
            Assert.Equal(5, moved.Radius, 12);
            Assert.True(moved.StartPoint.DistanceTo(turn.Transform(whole.StartPoint)) < 1E-12);
        }

        /// <summary>A stretch by a factor along a unit direction, which leaves square to it as it was.</summary>
        private static GeoTransform3 Stretch(GeoVector3 direction, double factor)
        {
            GeoVector3 d = direction.Normalize();
            double k = factor - 1;
            return new GeoTransform3(new double[,]
            {
                { 1 + k * d.X * d.X, k * d.X * d.Y, k * d.X * d.Z, 0 },
                { k * d.Y * d.X, 1 + k * d.Y * d.Y, k * d.Y * d.Z, 0 },
                { k * d.Z * d.X, k * d.Z * d.Y, 1 + k * d.Z * d.Z, 0 },
                { 0, 0, 0, 1 },
            });
        }

        [Fact]
        public void ACurvedLoopPressedFlat_IsMovedAsItWas()
        {
            // Pressing space onto z = 0 has no inverse, but it keeps this loop's own plane, so the loop comes down whole.
            var loop = new GeoPolygonArc3(
                new[] { new GeoPoint3(0, 0, 500), new GeoPoint3(1000, 0, 500), new GeoPoint3(1000, 1000, 500), new GeoPoint3(0, 1000, 500) },
                new[] { 0.3, 0, 0, 0 },
                new[] { GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis });

            GeoPolygonArc3 pressed = loop.TransformBy(GeoTransform3.Scaling(1, 1, 0));

            Assert.Equal(4058.9596866029174, pressed.Length, 9);
            Assert.True(pressed[1].DistanceTo(new GeoPoint3(1000, 0, 0)) < 1E-12);
        }

        [Fact]
        public void AnEdgeStretchedALittleUnevenly_RunsThroughWhereItsEndsAndMiddleLand()
        {
            // A half circle 1 000 across, its normal 0.002 off +Z, stretched to twice its height: its plane's two axes
            // stretch alike to within 0.000004, but its points land up to 0.003 off any circle about its own centre.
            var edge = new GeoEdge3(GeoPoint3.Origin, new GeoPoint3(0, 1000, 0), 1, new GeoVector3(Math.Sin(0.002), 0, Math.Cos(0.002)));
            GeoTransform3 stretch = GeoTransform3.Scaling(1, 1, 2);

            // The moved middle lies on the moved arc, though not halfway round it. A bulge this close to a half turn
            // places the centre only to about 1E-8, from the radius squared less the half chord squared, and the arc's
            // points with it.
            GeoPoint3 middle = stretch.Transform(edge.MidPoint);

            GeoEdge3 moved = edge.TransformBy(stretch);
            Assert.True(moved.DistanceTo(middle, Tolerance.Default) < 2E-8);
            Assert.True(moved.EndPoint.DistanceTo(stretch.Transform(edge.EndPoint)) < 1E-12);

            GeoArc3 arc = edge.ToArc().TransformBy(stretch);
            Assert.True(arc.StartPoint.DistanceTo(stretch.Transform(edge.StartPoint)) < 2E-8);
            Assert.True(arc.DistanceTo(middle, Tolerance.Default) < 2E-8);
            Assert.True(arc.EndPoint.DistanceTo(stretch.Transform(edge.EndPoint)) < 2E-8);
        }

        [Fact]
        public void AnEdgeStretchedAskew_RunsThroughWhereItsEndsAndMiddleLand()
        {
            // Stretched to twice along a diagonal of its plane, both axes of the plane grow alike to 1.58, but a circle
            // of radius 10 becomes an ellipse 10 by 20.
            GeoTransform3 skew = Stretch(new GeoVector3(-1, 1, 0), 2);
            var edge = new GeoEdge3(GeoPoint3.Origin, new GeoPoint3(10, 0, 0), 0.5, GeoVector3.ZAxis);

            // The moved middle lies on the moved arc, 2.39 short of halfway round it.
            GeoPoint3 middle = skew.Transform(edge.MidPoint);

            GeoEdge3 moved = edge.TransformBy(skew);
            Assert.True(moved.DistanceTo(middle, Tolerance.Default) < 1E-12);

            GeoArc3 arc = edge.ToArc().TransformBy(skew);
            Assert.True(arc.StartPoint.DistanceTo(skew.Transform(edge.StartPoint)) < 1E-12);
            Assert.True(arc.DistanceTo(middle, Tolerance.Default) < 1E-12);
            Assert.True(arc.EndPoint.DistanceTo(skew.Transform(edge.EndPoint)) < 1E-12);
        }

        [Fact]
        public void AFlatEdgeStretchedUnevenly_RunsThroughWhereItsMiddleLands()
        {
            // Stretched along its chord the edge bows the same 0.000005 over twice the length, its middle where it was.
            GeoTransform3 stretch = GeoTransform3.Scaling(2, 1, 1);
            GeoEdge3 moved = Short().TransformBy(stretch);

            Assert.True(moved.MidPoint.DistanceTo(new GeoPoint3(0.01, -5E-6, 0)) < 1E-14);
            Assert.True(moved.EndPoint.DistanceTo(new GeoPoint3(0.02, 0, 0)) < 1E-15);
        }

        [Fact]
        public void AFlatEdgeBowingTheOtherWay_BowsTowardsPlusY()
        {
            var edge = new GeoEdge3(GeoPoint3.Origin, new GeoPoint3(0.01, 0, 0), -1E-3, GeoVector3.ZAxis);

            Assert.True(edge.MidPoint.DistanceTo(new GeoPoint3(0.005, 5E-6, 0)) < 1E-14);
            Assert.True(edge.TransformBy(GeoTransform3.Scaling(-1, 1, 1)).MidPoint.DistanceTo(new GeoPoint3(-0.005, 5E-6, 0)) < 1E-14);
        }

        [Fact]
        public void AFlatArcLaidOutInAFrameFacingAway_TurnsTheOtherWayRoundIt()
        {
            // The frame's Z runs along -Z, so the arc, counter-clockwise round +Z, runs clockwise in it, and -Y is +Y.
            var frame = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, new GeoVector3(0, -1, 0));
            GeoArc2 flat = Short().ToArc().ProjectToArc2(frame);

            Assert.True(flat.SweptAngle < 0);
            Assert.True(flat.MidPoint.DistanceTo(new GeoPoint2(0.005, 5E-6)) < 1E-14);
        }

        [Fact]
        public void AClockwiseArcTurned_LandsWhereItsPointsLand()
        {
            var arc = new GeoArc3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 2, 2), 40, 0.3, 2.2, true);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(5, 0, 0), new GeoVector3(0, 1, 1), 1.1);
            GeoArc3 turned = arc.TransformBy(turn);

            Assert.True(turned.StartPoint.DistanceTo(turn.Transform(arc.StartPoint)) < 1E-12);
            Assert.True(turned.MidPoint.DistanceTo(turn.Transform(arc.MidPoint)) < 1E-12);
            Assert.True(turned.EndPoint.DistanceTo(turn.Transform(arc.EndPoint)) < 1E-12);
            Assert.Equal(arc.Length, turned.Length, 9);
        }

        [Theory]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        [InlineData(2.0)]
        [InlineData(-2.0)]
        public void AnOrdinaryEdge_IsTheArcOfItsEndsAndBulge(double bulge)
        {
            // The middle stands half the chord times the bulge off the chord's middle, on the right of the run about the
            // normal for a positive bulge and on the left for a negative one.
            GeoVector3 normal = new GeoVector3(1, 4, 0).Normalize();
            var start = new GeoPoint3(1, 2, 3);
            var end = new GeoPoint3(41, -8, 13);
            GeoArc3 arc = new GeoEdge3(start, end, bulge, normal).ToArc();

            GeoVector3 chord = start.GetVectorTo(end);
            GeoVector3 along = chord.Normalize();
            GeoVector3 bow = along.CrossProduct(normal).Normalize().Multiply(Math.Sign(bulge));
            GeoPoint3 middle = start.Add(chord.Multiply(0.5)).Add(bow.Multiply(Math.Abs(bulge) * chord.Length / 2));

            Assert.True(arc.StartPoint.DistanceTo(start) < 1E-12);
            Assert.True(arc.EndPoint.DistanceTo(end) < 1E-12);
            Assert.True(arc.MidPoint.DistanceTo(middle) < 1E-12);
        }

        [Theory]
        [InlineData(0.5)]
        [InlineData(-0.5)]
        [InlineData(2.0)]
        [InlineData(-2.0)]
        public void AnOrdinaryArc_IsTheArcThroughItsThreePoints(double bulge)
        {
            // The arc an edge stands for runs counter-clockwise round its own normal, which points against the edge's
            // where the bulge is negative, as the three-point build always gave it.
            var edge = new GeoEdge3(new GeoPoint3(1, 2, 3), new GeoPoint3(41, -8, 13), bulge, new GeoVector3(1, 4, 0));
            GeoArc3 arc = edge.ToArc();
            GeoArc3 through = GeoArc3.FromThreePoints(arc.StartPoint, arc.MidPoint, arc.EndPoint, Tolerance.Default);

            Assert.True(arc.SweptAngle > 0);
            Assert.True(arc.Normal.DotProduct(edge.Normal) * bulge > 0);
            Assert.Equal(through.Radius, arc.Radius, 9);
            Assert.Equal(through.SweptAngle, arc.SweptAngle, 12);
            Assert.True(arc.Normal.Subtract(through.Normal).Length < 1E-12);
            Assert.True(arc.Center.DistanceTo(through.Center) < 1E-9);
        }
    }
}
