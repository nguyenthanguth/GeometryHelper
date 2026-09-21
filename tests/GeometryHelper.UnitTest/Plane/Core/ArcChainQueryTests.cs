using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Asking a chain that may curve the questions a straight one answers: how far along, how far away,
    /// what is inside. Every answer here is measured on the arcs themselves, so the tests pick points that
    /// the chords would get wrong.
    /// </summary>
    public class ArcChainQueryTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-8, 1E-8);

        /// <summary>
        /// A slot 200 by 50 with a half circle of radius 25 on each end, running counter-clockwise.
        /// </summary>
        private static GeoPolygonArc2 Slot()
        {
            return new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, 1.0, 0.0, 1.0 });
        }

        /// <summary>
        /// The same outline with both ends bitten inward instead.
        /// </summary>
        private static GeoPolygonArc2 Bitten()
        {
            return new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, -1.0, 0.0, -1.0 });
        }

        [Fact]
        public void TheSlotIsTheShapeTheseTestsThinkItIs()
        {
            GeoPolygonArc2 slot = Slot();

            // Two straight sides of 200 and two half circles of radius 25.
            Assert.Equal(400.0 + Math.PI * 50.0, slot.Length, 8);
            Assert.Equal(10000.0 + Math.PI * 625.0, slot.Area, 8);

            // The round end reaches 25 beyond the chord it is drawn on.
            Assert.True(slot.GetEdgeAt(1).GetPointAtParameter(0.5).IsEqualTo(new GeoPoint2(225, 25), Tight));
            Assert.True(slot.GetEdgeAt(3).GetPointAtParameter(0.5).IsEqualTo(new GeoPoint2(-25, 25), Tight));

            // Bitten the other way, the same half circles are taken out instead.
            Assert.Equal(slot.Length, Bitten().Length, 8);
            Assert.Equal(10000.0 - Math.PI * 625.0, Bitten().Area, 8);
        }

        [Fact]
        public void WalkingRoundALoopFollowsTheArcsRatherThanTheChords()
        {
            GeoPolygonArc2 slot = Slot();

            // The first side is straight and 200 long.
            Assert.True(Parametrization2.GetPointAtDistance(slot, 0.0).IsEqualTo(new GeoPoint2(0, 0), Tight));
            Assert.True(Parametrization2.GetPointAtDistance(slot, 200.0).IsEqualTo(new GeoPoint2(200, 0), Tight));

            // Halfway round the first arc is the far point of the round end, 25 beyond the chord.
            Assert.True(Parametrization2.GetPointAtDistance(slot, 200.0 + Math.PI * 12.5)
                .IsEqualTo(new GeoPoint2(225, 25), Tight));

            // A parameter is the same walk measured against the whole length.
            Assert.Equal(slot.Length, Parametrization2.GetDistanceAtParameter(slot, 1.0), 8);
            Assert.Equal(0.5, Parametrization2.GetParameterAtDistance(slot, slot.Length * 0.5), 12);

            Assert.True(Parametrization2.GetPointAtParameter(slot, 200.0 / slot.Length)
                .IsEqualTo(new GeoPoint2(200, 0), Tight));

            // Past either end the walk stops there rather than running on.
            Assert.True(Parametrization2.GetPointAtDistance(slot, -50.0).IsEqualTo(slot[0], Tight));
            Assert.True(Parametrization2.GetPointAtDistance(slot, slot.Length + 50.0).IsEqualTo(slot[0], Tight));
        }

        [Fact]
        public void AWalkAndAPointFindEachOtherAgain()
        {
            GeoPolygonArc2 slot = Slot();

            foreach (double distance in new[] { 0.0, 37.5, 200.0, 220.0, 260.0, 400.0, 500.0 })
            {
                GeoPoint2 at = Parametrization2.GetPointAtDistance(slot, distance);

                Assert.Equal(distance, Parametrization2.GetDistanceAtPoint(slot, at), 6);
                Assert.Equal(0.0, Containment2.IsPointOn(slot, at) ? 0.0 : 1.0, 9);
            }

            GeoPolylineArc2 chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) },
                new[] { 0.5, 0.0 });

            Assert.True(Parametrization2.GetPointAtDistance(chain, 0.0).IsEqualTo(chain[0], Tight));
            Assert.True(Parametrization2.GetPointAtDistance(chain, chain.Length).IsEqualTo(chain[2], Tight));
            Assert.Equal(1.0, Parametrization2.GetParameterAtPoint(chain, chain[2]), 6);
        }

        [Fact]
        public void DistanceIsMeasuredToTheArcNotToItsChord()
        {
            GeoPolygonArc2 slot = Slot();

            // Thirty beyond the centre of the right hand end: five from the arc, thirty from the chord.
            var outside = new GeoPoint2(230, 25);

            Assert.Equal(5.0, Distance2.DistanceTo(slot, outside), 8);
            Assert.True(Projection2.ProjectToPolygonArc(slot, outside).IsEqualTo(new GeoPoint2(225, 25), Tight));

            // The straight loop through the vertices would say thirty, which is the point of all this.
            var chords = new GeoPolygon2(slot.Vertices.ToArray());
            Assert.Equal(30.0, Distance2.DistanceTo(chords, outside), 8);

            // A point on the arc is on the loop, and one a hair off it is not.
            Assert.True(Containment2.IsPointOn(slot, new GeoPoint2(225, 25)));
            Assert.False(Containment2.IsPointOn(slot, new GeoPoint2(225.001, 25)));

            // And the distance to something else is measured the same way.
            var bar = new GeoLine2(new GeoPoint2(240, -100), new GeoPoint2(240, 100));
            Assert.Equal(15.0, Distance2.DistanceTo(slot, bar), 8);
        }

        [Fact]
        public void WhatIsInsideFollowsTheArcsBothWaysTheyLean()
        {
            GeoPolygonArc2 slot = Slot();

            // In the round end: outside the straight loop through the vertices, inside the slot.
            Assert.Equal(PointLocation.Inside, Containment2.Locate(slot, new GeoPoint2(215, 25)));
            Assert.Equal(PointLocation.Inside, Containment2.Locate(slot, new GeoPoint2(-15, 25)));

            // Plainly inside, plainly outside, and exactly on the arc.
            Assert.Equal(PointLocation.Inside, Containment2.Locate(slot, new GeoPoint2(100, 25)));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(slot, new GeoPoint2(230, 25)));
            Assert.Equal(PointLocation.OnSide, Containment2.Locate(slot, new GeoPoint2(225, 25)));

            Assert.True(Containment2.Contains(slot, new GeoPoint2(215, 25)));
            Assert.False(Containment2.Contains(slot, new GeoPoint2(230, 25)));

            // Bitten the other way, the same place is outside and the bite is not enclosed.
            GeoPolygonArc2 bitten = Bitten();

            Assert.Equal(PointLocation.OutSide, Containment2.Locate(bitten, new GeoPoint2(215, 25)));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(bitten, new GeoPoint2(190, 25)));
            Assert.Equal(PointLocation.Inside, Containment2.Locate(bitten, new GeoPoint2(100, 25)));
            Assert.Equal(PointLocation.OnSide, Containment2.Locate(bitten, new GeoPoint2(175, 25)));
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(0.4)]
        [InlineData(-0.4)]
        public void WhatIsInsideAgreesWithAFinelyFlattenedLoop(double bulge)
        {
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 50), new GeoPoint2(0, 50) },
                new[] { 0.0, bulge, 0.0, bulge });

            GeoPolygon2 flat = loop.Flatten(0.0005);
            int checkedPoints = 0;

            for (double x = -40; x <= 240; x += 3.5)
            {
                for (double y = -10; y <= 60; y += 3.5)
                {
                    var point = new GeoPoint2(x, y);

                    // Near the boundary the two need not agree, because the chords are not the arcs there.
                    if (Distance2.DistanceTo(loop, point) < 0.5)
                    {
                        continue;
                    }

                    Assert.Equal(Containment2.Contains(flat, point), Containment2.Contains(loop, point));
                    checkedPoints++;
                }
            }

            Assert.True(checkedPoints > 500);
        }

        [Fact]
        public void AChainEnclosesNothingHoweverItCurves()
        {
            var arch = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0) },
                new[] { -1.0 });

            // A half circle above the line: the middle of it is on the chain, the middle of the disc is not.
            Assert.Equal(PointLocation.OnSide, Containment2.Locate(arch, new GeoPoint2(50, 50)));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(arch, new GeoPoint2(50, 25)));
            Assert.Equal(PointLocation.OutSide, Containment2.Locate(arch, new GeoPoint2(50, -25)));

            Assert.Equal(25.0, Distance2.DistanceTo(arch, new GeoPoint2(50, 25)), 8);
            Assert.Equal(Math.PI * 50.0, arch.Length, 8);
        }

        [Fact]
        public void NothingToWorkWithIsRefusedRatherThanGuessedAt()
        {
            Assert.Throws<ArgumentNullException>(() => Parametrization2.GetPointAtDistance((GeoPolygonArc2)null, 1.0));
            Assert.Throws<ArgumentNullException>(() => Parametrization2.GetPointAtParameter((GeoPolylineArc2)null, 0.5));
            Assert.Throws<ArgumentNullException>(() => Projection2.ProjectToPolygonArc(null, GeoPoint2.Origin));
            Assert.Throws<ArgumentNullException>(() => Containment2.Locate((GeoPolygonArc2)null, GeoPoint2.Origin));
            Assert.Throws<ArgumentNullException>(() => Containment2.Contains((GeoPolygonArc2)null, GeoPoint2.Origin));
            Assert.Throws<ArgumentNullException>(() => Distance2.DistanceTo((GeoPolygonArc2)null, GeoPoint2.Origin));
        }
    }
}
