using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The distance between arcs and curved shapes, past what <see cref="Arc2ExactTests"/> pins: arcs lying on one
    /// circle or inside another, a stub shorter than the tolerance crossing an arc, a ray beside a bowed side, and a
    /// collision that answers exactly where the distance is within the tolerance.
    /// </summary>
    public class Arc2ExactMoreTests
    {
        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        [Fact]
        public void AChainAndAnArcWhoseCirclesCrossNearBothEndsButWhoseEndsAre1Point2TolerancesApart_DoNotCollide()
        {
            // The chain's arc and the arc cross as circles at a point within a tolerance of both their ends; the ends stand
            // 0.00012 apart at a tolerance of 0.0001, and the crossing used to let them collide.
            Tolerance t = At(1E-4);
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(40, 0), new GeoPoint2(70, 25), new GeoPoint2(100, 25) },
                new[] { 0.0, -0.35, 0.0, 0.0 });
            var arc = new GeoArc2(new GeoPoint2(25.858140813193945, -47.95846739069786), 50.0, 5.3172292646646655, 7.5672292646646655);

            Assert.InRange(chain.DistanceTo(arc, t), 0.00012 - 1E-9, 0.00012 + 1E-9);
            Assert.InRange(arc.DistanceTo(chain, t), 0.00012 - 1E-9, 0.00012 + 1E-9);
            Assert.False(chain.CollidesWith(arc, t));
            Assert.False(arc.CollidesWith(chain, t));
        }

        [Fact]
        public void TwoArcsOfOneCircleThatOverlap_AreNoughtApart_AndTwoThatDoNotAreAnEndApart()
        {
            var circle = new GeoPoint2(10, 20);
            var first = new GeoArc2(circle, 30, 0, Math.PI / 2);
            var overlapping = new GeoArc2(circle, 30, Math.PI / 4, Math.PI);
            var apart = new GeoArc2(circle, 30, Math.PI, 3 * Math.PI / 2);

            Assert.Equal(0.0, first.DistanceTo(overlapping, Tolerance.Default), 12);
            Assert.Equal(0.0, overlapping.DistanceTo(first, Tolerance.Default), 12);

            // The ends nearest each other are the end of the first at a quarter turn and the start of the other at a half:
            // a quarter turn of a circle of radius 30 apart, chord 30 * sqrt(2).
            Assert.Equal(30 * Math.Sqrt(2), first.DistanceTo(apart, Tolerance.Default), 9);
        }

        [Fact]
        public void AnArcInsideACircleOfAnotherArc_IsTheRadialGapAway()
        {
            // The arcs are concentric, of radius 50 and 50.0005, and overlap in angle: 0.0005 apart, not nought.
            var centre = new GeoPoint2(0, 0);
            var inner = new GeoArc2(centre, 50, 0, Math.PI);
            var outer = new GeoArc2(centre, 50.0005, 0.5, 2.5);
            Tolerance t = At(1E-3);

            Assert.InRange(inner.DistanceTo(outer, t), 0.0005 - 1E-11, 0.0005 + 1E-11);
            Assert.InRange(outer.DistanceTo(inner, t), 0.0005 - 1E-11, 0.0005 + 1E-11);
            Assert.True(inner.CollidesWith(outer, t));
        }

        [Fact]
        public void AStubShorterThanTheToleranceCrossingAnArc_IsNoughtFromIt()
        {
            // 0.0006 long, crossing the top of the arc of radius 50 about the origin at a tolerance of 0.001.
            Tolerance t = At(1E-3);
            var dome = new GeoArc2(new GeoPoint2(0, 0), 50, 0, Math.PI);
            var stub = new GeoLine2(new GeoPoint2(0, 49.9997), new GeoPoint2(0, 50.0003));

            Assert.Equal(0.0, dome.DistanceTo(stub, t));
            Assert.Equal(0.0, stub.DistanceTo(dome, t));
        }

        [Fact]
        public void ARayStartingHalfAToleranceOffTheBowedSideOfALoop_IsThatFarAway()
        {
            // The right side of the square 100 by 100 is bowed out to x = 150 by a bulge of 1.
            Tolerance t = At(1E-3);
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new[] { 0.0, 1.0, 0.0, 0.0 });
            var ray = new GeoRay2(new GeoPoint2(150.0005, 50), new GeoVector2(1, 0));

            Assert.InRange(loop.DistanceTo(ray, t), 0.0005 - 1E-11, 0.0005 + 1E-11);
            Assert.InRange(ray.DistanceTo(loop, t), 0.0005 - 1E-11, 0.0005 + 1E-11);
        }

        [Fact]
        public void ACircleHoldingACurvedLoopWhole_IsNoughtFromIt_AndOneBesideItIsTheGap()
        {
            Tolerance t = At(1E-3);
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new[] { 0.0, 1.0, 0.0, 0.0 });

            Assert.Equal(0.0, new GeoCircle2(new GeoPoint2(50, 50), 200).DistanceTo(loop, t));
            Assert.InRange(new GeoCircle2(new GeoPoint2(200.0005, 50), 50).DistanceTo(loop, t), 0.0005 - 1E-11, 0.0005 + 1E-11);
        }
    }
}
