using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// How deep inside a closed shape a point sits. <c>DistanceTo</c> reads a closed shape as a filled
    /// region and answers nothing at all for a point inside one, so the depth cannot be recovered from it.
    /// <c>SignedDistanceTo</c> keeps it, and its whole definition is tied to <c>Locate</c>: negative inside,
    /// nought on the boundary, positive outside.
    /// </summary>
    public class SignedDistanceTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static GeoCircle2 Circle() => new GeoCircle2(new GeoPoint2(0, 0), 30.0);

        private static GeoRectangle2 Plate() => new GeoRectangle2(0, 0, 100, 100);

        private static GeoPolygon2 Square() => new GeoPolygon2(
            new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

        /// <summary>
        /// The square with its right-hand side swelled into a half circle of radius fifty about (100, 50),
        /// reaching out to x = 150.
        /// </summary>
        private static GeoPolygonArc2 Slot() => new GeoPolygonArc2(
            new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
            new[] { 0.0, 1.0, 0.0, 0.0 });

        /// <summary>
        /// The square with a twenty-wide hole in the middle of it.
        /// </summary>
        private static GeoFace2 Pierced() => new GeoFace2(
            Square(),
            new[]
            {
                new GeoPolygon2(
                    new GeoPoint2(40, 40), new GeoPoint2(60, 40), new GeoPoint2(60, 60), new GeoPoint2(40, 60))
            });

        /// <summary>
        /// Gets the distance from a point to the nearest edge of a polygon, without going through any of the
        /// projection code the answer under test is built on.
        /// </summary>
        private static double ToEdges(GeoPolygon2 poly, GeoPoint2 point)
            => poly.GetEdges().Min(edge => Distance2.DistanceTo(edge, point));

        [Fact]
        public void ThePlainNumbersAreWhatTheDrawingSays()
        {
            // A plate a hundred square: its middle stands fifty from the nearest side.
            Assert.Equal(-50.0, Plate().SignedDistanceTo(new GeoPoint2(50, 50)), 9);
            Assert.Equal(-10.0, Plate().SignedDistanceTo(new GeoPoint2(10, 50)), 9);
            Assert.Equal(20.0, Plate().SignedDistanceTo(new GeoPoint2(-20, 50)), 9);
            Assert.Equal(0.0, Plate().SignedDistanceTo(new GeoPoint2(0, 50)), 9);

            // A circle of radius thirty about the origin.
            Assert.Equal(-30.0, Circle().SignedDistanceTo(new GeoPoint2(0, 0)), 9);
            Assert.Equal(-20.0, Circle().SignedDistanceTo(new GeoPoint2(10, 0)), 9);
            Assert.Equal(20.0, Circle().SignedDistanceTo(new GeoPoint2(50, 0)), 9);

            Assert.Equal(-40.0, Square().SignedDistanceTo(new GeoPoint2(50, 40)), 9);
            Assert.Equal(10.0, Square().SignedDistanceTo(new GeoPoint2(-10, 50)), 9);

            // DistanceTo is untouched: a point inside is still nought away.
            Assert.Equal(0.0, Plate().DistanceTo(new GeoPoint2(50, 50)), 12);
            Assert.Equal(0.0, Circle().DistanceTo(new GeoPoint2(10, 0)), 12);
            Assert.Equal(0.0, Square().DistanceTo(new GeoPoint2(50, 40)), 12);
        }

        [Fact]
        public void ACurvedLoopIsMeasuredOnItsArcsAndOnlyWhereTheyReach()
        {
            GeoPolygonArc2 slot = Slot();

            // (130, 50) stands thirty from the centre of a bulge of radius fifty, so twenty inside the arc.
            // Against the straight square the same point is thirty outside, which is what measuring the
            // chord instead of the arc would have said.
            Assert.Equal(-20.0, slot.SignedDistanceTo(new GeoPoint2(130, 50)), 9);
            Assert.Equal(30.0, Square().SignedDistanceTo(new GeoPoint2(130, 50)), 9);

            // A point on the bulge itself is on the boundary, and one beyond it is outside by the gap.
            Assert.Equal(0.0, slot.SignedDistanceTo(new GeoPoint2(150, 50)), 9);
            Assert.Equal(50.0, slot.SignedDistanceTo(new GeoPoint2(200, 50)), 9);

            // The arc counts only where it reaches. (30, 50) is twenty from the circle carrying the bulge,
            // but that direction is outside the sweep, so the nearest boundary is the left-hand side at
            // thirty. Judging reach by an angle rather than by a length used to hand back the twenty.
            Assert.Equal(-30.0, slot.SignedDistanceTo(new GeoPoint2(30, 50)), 9);

            GeoEdge2 bulge = slot.GetEdges().Single(edge => edge.IsArc);

            Assert.Equal(20.0, Math.Abs(new GeoPoint2(30, 50).DistanceTo(bulge.ToArc().Center) - bulge.ToArc().Radius), 9);
            Assert.Equal(Math.Sqrt(70.0 * 70.0 + 50.0 * 50.0), bulge.DistanceTo(new GeoPoint2(30, 50)), 9);
        }

        [Fact]
        public void AHoleIsPartOfTheBoundary()
        {
            GeoFace2 face = Pierced();

            // The middle of the hole is off the material, ten from the rim it sits in.
            Assert.Equal(10.0, face.SignedDistanceTo(new GeoPoint2(50, 50)), 9);
            Assert.Equal(PointLocation.OutSide, face.Locate(new GeoPoint2(50, 50)));

            // On the material between the outline and the hole, measured to whichever is nearer.
            Assert.Equal(-15.0, face.SignedDistanceTo(new GeoPoint2(25, 50)), 9);
            Assert.Equal(-5.0, face.SignedDistanceTo(new GeoPoint2(35, 50)), 9);
            Assert.Equal(-5.0, face.SignedDistanceTo(new GeoPoint2(5, 50)), 9);

            // On the rim itself is nought, the same as on the outline.
            Assert.Equal(0.0, face.SignedDistanceTo(new GeoPoint2(40, 50)), 9);
            Assert.Equal(0.0, face.SignedDistanceTo(new GeoPoint2(0, 50)), 9);

            // A face now answers the plain question too, which it could not before.
            Assert.Equal(0.0, face.DistanceTo(new GeoPoint2(25, 50)), 12);
            Assert.Equal(10.0, face.DistanceTo(new GeoPoint2(50, 50)), 9);
        }

        /// <summary>
        /// The defining property, over a grid rather than at a handful of chosen points: the sign says what
        /// <c>Locate</c> says, and the magnitude is the distance to the boundary worked out another way.
        /// </summary>
        [Fact]
        public void TheSignFollowsLocateAndTheMagnitudeIsTheDistanceToTheBoundary()
        {
            GeoCircle2 circle = Circle();
            GeoRectangle2 plate = Plate();
            GeoPolygon2 square = Square();
            GeoPolygonArc2 slot = Slot();
            GeoFace2 face = Pierced();

            GeoPolygon2 hole = face.Holes[0];
            int inside = 0;
            int outside = 0;
            int onBoundary = 0;

            for (int i = -6; i <= 26; i++)
            {
                for (int j = -6; j <= 26; j++)
                {
                    var point = new GeoPoint2(i * 7.0, j * 7.0);

                    Check(circle.SignedDistanceTo(point), circle.Locate(point),
                        Math.Abs(point.DistanceTo(circle.Center) - circle.Radius),
                        ref inside, ref outside, ref onBoundary);

                    Check(plate.SignedDistanceTo(point), plate.Locate(point),
                        ToEdges(plate.ToPolygon(), point), ref inside, ref outside, ref onBoundary);

                    Check(square.SignedDistanceTo(point), square.Locate(point),
                        ToEdges(square, point), ref inside, ref outside, ref onBoundary);

                    Check(slot.SignedDistanceTo(point), slot.Locate(point),
                        slot.GetEdges().Min(edge => edge.DistanceTo(point)),
                        ref inside, ref outside, ref onBoundary);

                    Check(face.SignedDistanceTo(point), face.Locate(point),
                        Math.Min(ToEdges(face.Boundary, point), ToEdges(hole, point)),
                        ref inside, ref outside, ref onBoundary);
                }
            }

            // The grid is worth nothing unless it landed in all three places.
            Assert.True(inside > 500, "only " + inside + " inside");
            Assert.True(outside > 500, "only " + outside + " outside");
            Assert.True(onBoundary > 20, "only " + onBoundary + " on the boundary");
        }

        private static void Check(
            double signed,
            PointLocation where,
            double toBoundary,
            ref int inside,
            ref int outside,
            ref int onBoundary)
        {
            Assert.Equal(toBoundary, Math.Abs(signed), 7);

            switch (where)
            {
                case PointLocation.Inside:
                    Assert.True(signed < 0.0, "inside but " + signed);
                    inside++;
                    break;

                case PointLocation.OnSide:
                    Assert.True(Math.Abs(signed) <= Tolerance.Global.EqualPoint, "on the boundary but " + signed);
                    onBoundary++;
                    break;

                default:
                    Assert.True(signed > 0.0, "outside but " + signed);
                    outside++;
                    break;
            }
        }

        [Fact]
        public void TheMagnitudeAgreesWithTheNearestPointOnTheBoundary()
        {
            var probes = new List<GeoPoint2>
            {
                new GeoPoint2(50, 50), new GeoPoint2(10, 20), new GeoPoint2(-30, 140),
                new GeoPoint2(0, 0), new GeoPoint2(99, 1), new GeoPoint2(130, 50)
            };

            foreach (GeoPoint2 point in probes)
            {
                Assert.Equal(
                    point.DistanceTo(Plate().GetClosestPointOnBoundary(point)),
                    Math.Abs(Plate().SignedDistanceTo(point)), 7);

                Assert.Equal(
                    point.DistanceTo(Square().GetClosestPointOnBoundary(point)),
                    Math.Abs(Square().SignedDistanceTo(point)), 7);

                Assert.Equal(
                    point.DistanceTo(Circle().GetClosestPointOnBoundary(point)),
                    Math.Abs(Circle().SignedDistanceTo(point)), 7);

                Assert.Equal(
                    point.DistanceTo(Slot().GetClosestPointOnBoundary(point)),
                    Math.Abs(Slot().SignedDistanceTo(point)), 7);
            }
        }

        [Fact]
        public void TheSameWorkReadsBothWaysAndRefusesNothing()
        {
            var point = new GeoPoint2(30, 40);

            Assert.Equal(Distance2.SignedDistanceTo(Circle(), point), Circle().SignedDistanceTo(point), 12);
            Assert.Equal(Distance2.SignedDistanceTo(Plate(), point), Plate().SignedDistanceTo(point), 12);
            Assert.Equal(Distance2.SignedDistanceTo(Square(), point), Square().SignedDistanceTo(point), 12);
            Assert.Equal(Distance2.SignedDistanceTo(Slot(), point), Slot().SignedDistanceTo(point), 12);
            Assert.Equal(Distance2.SignedDistanceTo(Pierced(), point), Pierced().SignedDistanceTo(point), 12);

            Assert.Equal(Circle().SignedDistanceTo(point), Circle().SignedDistanceTo(point, Tolerance.Global), 12);
            Assert.Equal(Plate().SignedDistanceTo(point), Plate().SignedDistanceTo(point, Tolerance.Global), 12);
            Assert.Equal(Square().SignedDistanceTo(point), Square().SignedDistanceTo(point, Tolerance.Global), 12);
            Assert.Equal(Slot().SignedDistanceTo(point), Slot().SignedDistanceTo(point, Tolerance.Global), 12);
            Assert.Equal(Pierced().SignedDistanceTo(point), Pierced().SignedDistanceTo(point, Tolerance.Global), 12);

            Assert.Throws<ArgumentNullException>(() => Distance2.SignedDistanceTo((GeoPolygon2)null, point));
            Assert.Throws<ArgumentNullException>(() => Distance2.SignedDistanceTo((GeoPolygonArc2)null, point));
            Assert.Throws<ArgumentNullException>(() => Distance2.SignedDistanceTo((GeoFace2)null, point));
            Assert.Throws<ArgumentNullException>(() => Distance2.DistanceTo((GeoFace2)null, point));
        }

        [Fact]
        public void ANonSimplePolygonTakesItsSignFromTheEvenOddRule()
        {
            // A bowtie: the two lobes are traced the opposite way round, so the even-odd rule reads the
            // crossing point as boundary and both lobes as inside.
            var bowtie = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(100, 100), new GeoPoint2(100, 0), new GeoPoint2(0, 100));

            Assert.False(bowtie.IsSimple());

            var inLeft = new GeoPoint2(20, 50);
            var inRight = new GeoPoint2(80, 50);

            // Whatever Locate says of each lobe, the sign says the same, which is the whole promise.
            Assert.Equal(bowtie.Locate(inLeft) == PointLocation.Inside, bowtie.SignedDistanceTo(inLeft) < 0.0);
            Assert.Equal(bowtie.Locate(inRight) == PointLocation.Inside, bowtie.SignedDistanceTo(inRight) < 0.0);
            Assert.Equal(ToEdges(bowtie, inLeft), Math.Abs(bowtie.SignedDistanceTo(inLeft)), 7);
        }
    }
}
