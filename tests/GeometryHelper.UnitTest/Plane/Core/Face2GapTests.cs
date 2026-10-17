using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The distance from the material of a face is nought only where a shape truly meets it, and otherwise the gap from
    /// the nearest rim. It used to read nought from the crossing tests, which keep a band of the point tolerance, and to
    /// measure otherwise the shortest line, which lets a segment run on up to the tolerance past its ends: a shape 0.0012
    /// off a corner at a tolerance of 0.001 was nought away, and one 0.0005 off was too.
    /// </summary>
    public class Face2GapTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPolygon2 Box(double left, double bottom, double right, double top) => new GeoPolygon2(
            new GeoPoint2(left, bottom), new GeoPoint2(right, bottom), new GeoPoint2(right, top), new GeoPoint2(left, top));

        /// <summary>The square 100 by 100 with a corner at the origin and a hole 20 by 20 in its middle.</summary>
        private static GeoFace2 Plate() => new GeoFace2(Box(0, 0, 100, 100), new[] { Box(40, 40, 60, 60) });

        /// <summary>The point a gap off the corner (100, 100), out along the diagonal.</summary>
        private static GeoPoint2 OffTheCorner(double gap) => new GeoPoint2(100 + gap / Math.Sqrt(2), 100 + gap / Math.Sqrt(2));

        public static IEnumerable<object[]> CornerCases()
        {
            foreach (string kind in new[] { "segment", "chain", "polygon", "triangle", "face", "rectangle", "ray", "straight curved chain", "curved loop" })
            {
                foreach (double gap in new[] { 0.0005, 0.0012, 0.0014, 0.0019, 0.01 })
                {
                    yield return new object[] { kind, gap };
                }
            }
        }

        /// <summary>The distance and the collision of the plate with a shape whose tip stands a gap off its corner.</summary>
        private static (double Distance, bool Collides) Measure(string kind, double gap)
        {
            GeoFace2 plate = Plate();
            GeoPoint2 tip = OffTheCorner(gap);
            var left = new GeoPoint2(tip.X + 3, tip.Y + 9);
            var right = new GeoPoint2(tip.X + 9, tip.Y + 3);

            switch (kind)
            {
                case "segment": var line = new GeoLine2(tip, left); return (plate.DistanceTo(line, Tol), plate.CollidesWith(line, Tol));
                case "chain": var chain = new GeoPolyline2(left, tip, right); return (plate.DistanceTo(chain, Tol), plate.CollidesWith(chain, Tol));
                case "polygon": var polygon = new GeoPolygon2(left, tip, right); return (plate.DistanceTo(polygon, Tol), plate.CollidesWith(polygon, Tol));
                case "triangle": var triangle = new GeoTriangle2(left, tip, right); return (plate.DistanceTo(triangle, Tol), plate.CollidesWith(triangle, Tol));
                case "face": var face = new GeoFace2(new GeoPolygon2(left, tip, right)); return (plate.DistanceTo(face, Tol), plate.CollidesWith(face, Tol));
                case "rectangle": var rect = new GeoRectangle2(tip.X, tip.Y, 5, 5); return (plate.DistanceTo(rect, Tol), plate.CollidesWith(rect, Tol));
                case "ray": var ray = new GeoRay2(tip, new GeoVector2(1, 2)); return (plate.DistanceTo(ray, Tol), plate.CollidesWith(ray, Tol));
                case "straight curved chain": var bent = new GeoPolylineArc2(new[] { left, tip, right }, new[] { 0, 0.0 }); return (plate.DistanceTo(bent, Tol), plate.CollidesWith(bent, Tol));
                default: var loop = new GeoPolygonArc2(new[] { left, tip, right }, new[] { 0, 0, 0.4 }); return (plate.DistanceTo(loop, Tol), plate.CollidesWith(loop, Tol));
            }
        }

        [Theory]
        [MemberData(nameof(CornerCases))]
        public void AShapeOffACorner_IsAsFarAsItsTipAndCollidesOnlyWithinTheTolerance(string kind, double gap)
        {
            (double distance, bool collides) = Measure(kind, gap);

            Assert.Equal(gap, distance, 12);
            Assert.Equal(gap <= Tol.EqualPoint, collides);
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.0012)]
        [InlineData(0.0015)]
        public void ACurveOffACorner_IsAsFarAsItsNearestPoint(double gap)
        {
            // A circle of radius 5 coming at the corner along the diagonal, an arc of it, and a loop and a chain through
            // that arc: the corner is the nearest point of the plate to each.
            GeoFace2 plate = Plate();
            double k = (gap + 5) / Math.Sqrt(2);
            var centre = new GeoPoint2(100 + k, 100 + k);
            double toward = Math.PI * 1.25;
            var arc = new GeoArc2(centre, 5, toward - 0.6, toward + 0.6);

            Assert.Equal(gap, plate.DistanceTo(new GeoCircle2(centre, 5), Tol), 12);
            Assert.Equal(gap, plate.DistanceTo(arc, Tol), 12);
            Assert.Equal(gap, plate.DistanceTo(new GeoPolygonArc2(new[] { arc.StartPoint, arc.EndPoint, centre }, new[] { Math.Tan(0.3), 0, 0 }), Tol), 12);
            Assert.Equal(gap, plate.DistanceTo(new GeoPolylineArc2(new[] { arc.StartPoint, arc.EndPoint }, new[] { Math.Tan(0.3) }), Tol), 12);
        }

        [Fact]
        public void ACircleHalfTheToleranceOffACorner_IsThatFar()
        {
            var centre = new GeoPoint2(100 + 5.0005 / Math.Sqrt(2), 100 + 5.0005 / Math.Sqrt(2));

            Assert.Equal(0.0005, Plate().DistanceTo(new GeoCircle2(centre, 5), Tol), 12);
        }

        [Fact]
        public void AShapeInTheHoleNearItsCorner_IsAsFarAsTheNearerSide()
        {
            // 0.0012 off the hole's corner (40, 40) inside the hole: 0.00085 from each of its two sides.
            GeoFace2 plate = Plate();
            var tip = new GeoPoint2(40 + 0.0012 / Math.Sqrt(2), 40 + 0.0012 / Math.Sqrt(2));
            var a = new GeoPoint2(45, 50);
            var b = new GeoPoint2(50, 45);
            double expected = 0.0012 / Math.Sqrt(2);

            Assert.Equal(expected, plate.DistanceTo(new GeoLine2(tip, a), Tol), 12);
            Assert.Equal(expected, plate.DistanceTo(new GeoPolygon2(a, tip, b), Tol), 12);
            Assert.Equal(expected, plate.DistanceTo(new GeoFace2(new GeoPolygon2(a, tip, b)), Tol), 12);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.0005)]
        public void AFlatRectangle_IsMeasuredAsTheSegmentItIs(double height)
        {
            // A rectangle 5 wide and as good as no height, 10 to the right of the plate, and one lying on its material.
            GeoFace2 plate = Plate();
            var fine = new Tolerance(1E-6, 1E-6);

            Assert.Equal(10.0, plate.DistanceTo(new GeoRectangle2(110, 50, 5, height), fine), 9);
            Assert.Equal(10.0, new GeoRectangle2(110, 50, 5, height).DistanceTo(plate, fine), 9);
            Assert.Equal(0.0, plate.DistanceTo(new GeoRectangle2(20, 20, 5, height), fine));
        }

        [Fact]
        public void ASegmentShorterThanTheToleranceAcrossARim_IsNoughtAwayEitherWayRound()
        {
            // 0.0008 long, straddling the outline at x = 100 and the hole's rim at x = 40.
            GeoFace2 plate = Plate();

            foreach (double x in new[] { 100.0, 40.0 })
            {
                var across = new GeoLine2(new GeoPoint2(x - 0.0004, 50), new GeoPoint2(x + 0.0004, 50));

                Assert.Equal(0.0, plate.DistanceTo(across, Tol));
                Assert.Equal(0.0, plate.DistanceTo(new GeoLine2(across.EndPoint, across.StartPoint), Tol));
            }
        }

        [Fact]
        public void ASegmentRunningInThroughAChamferShorterThanTheTolerance_IsNoughtAway()
        {
            // The corner (100, 100) cut off by 0.004 each way, at a tolerance of 0.01: the chamfer is 0.0057 long.
            var coarse = new Tolerance(0.01, 1E-3);
            var plate = new GeoFace2(new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 99.996), new GeoPoint2(99.996, 100), new GeoPoint2(0, 100)));
            var inward = new GeoLine2(new GeoPoint2(101, 101), new GeoPoint2(90, 90));

            Assert.Equal(0.0, plate.DistanceTo(inward, coarse));
            Assert.Equal(0.0, plate.DistanceTo(new GeoLine2(inward.EndPoint, inward.StartPoint), coarse));

            // A chain and a triangle that start outside and poke into the material through the chamfer alone.
            var poke = new GeoPolyline2(new GeoPoint2(101, 101), new GeoPoint2(99.997, 99.997));
            var wedge = new GeoPolygon2(new GeoPoint2(101, 101), new GeoPoint2(99.997, 99.997), new GeoPoint2(101, 100.5));

            Assert.Equal(0.0, plate.DistanceTo(poke, coarse));
            Assert.Equal(0.0, plate.DistanceTo(wedge, coarse));
        }

        [Fact]
        public void ACurveThatCrossesTheRim_IsNoughtAway()
        {
            // An arc from (-10, 50) to (110, 50) bowing 3 off its chord crosses the outline twice and the hole's rim, and
            // starts and ends off the plate.
            GeoFace2 plate = Plate();
            var arc = GeoArc2.FromBulge(new GeoPoint2(-10, 50), new GeoPoint2(110, 50), 0.05);

            Assert.Equal(0.0, plate.DistanceTo(arc, Tol));
            Assert.Equal(0.0, plate.DistanceTo(new GeoEdge2(arc.StartPoint, arc.EndPoint, 0.05), Tol));
            Assert.Equal(0.0, plate.DistanceTo(new GeoPolylineArc2(new[] { arc.StartPoint, arc.EndPoint, new GeoPoint2(110, -20) }, new[] { 0.05, 0 }), Tol));
            Assert.Equal(0.0, plate.DistanceTo(new GeoPolygonArc2(new[] { arc.StartPoint, arc.EndPoint, new GeoPoint2(110, -20), new GeoPoint2(-10, -20) }, new[] { 0.05, 0, 0, 0 }), Tol));
        }

        [Theory]
        [InlineData(1E-12)]
        [InlineData(1E-13)]
        public void AnEdgeThatBarelyBows_IsAsFarAsItsChord(double bulge)
        {
            // An edge 1 000 long along the X axis bowing a trillionth or less, of radius 2.5E14 or more, and the square 100
            // to 900 in X standing 0.002 or 0.05 above it: as far as its chord is, and colliding exactly when that is within
            // the tolerance.
            foreach (double gap in new[] { 0.002, 0.05 })
            {
                var plate = new GeoFace2(Box(100, gap, 900, 800 + gap));
                var edge = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(1000, 0), bulge);

                Assert.Equal(gap, plate.DistanceTo(edge, Tol), 9);
                Assert.Equal(gap, plate.DistanceTo(new GeoPolylineArc2(new[] { edge.StartPoint, edge.EndPoint }, new[] { bulge }), Tol), 9);
                Assert.False(plate.CollidesWith(edge, Tol));
            }

            // Turned 45 degrees, where a centre 2.5E14 away puts the arc's own ends back only to about 0.03, the edge is
            // read between its own ends.
            var tilted = new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(707.10678118654755, 707.10678118654755), bulge);
            var beside = new GeoFace2(new GeoPolygon2(
                new GeoPoint2(400.0014142135624, 399.9985857864376), new GeoPoint2(600.0014142135624, 599.9985857864376),
                new GeoPoint2(700, 500), new GeoPoint2(500, 300)));

            Assert.Equal(0.002, beside.DistanceTo(tilted, Tol), 9);

            // An arc built as such, its ends put back from a centre 2.5E14 off to within about 0.03: still apart, and as
            // apart as it collides, where its circle once read a meeting.
            var arc = GeoArc2.FromBulge(new GeoPoint2(0, 0), new GeoPoint2(1000, 0), bulge);
            var above = new GeoFace2(Box(100, 0.05, 900, 800.05));

            Assert.True(above.DistanceTo(arc, Tol) > Tol.EqualPoint);
            Assert.False(above.CollidesWith(arc, Tol));
        }

        [Fact]
        public void AShapeThatMeetsTheMaterial_IsNoughtAway()
        {
            GeoFace2 plate = Plate();

            Assert.Equal(0.0, plate.DistanceTo(new GeoLine2(new GeoPoint2(90, 50), new GeoPoint2(120, 50)), Tol));        // crosses the outline
            Assert.Equal(0.0, plate.DistanceTo(new GeoLine2(new GeoPoint2(10, 10), new GeoPoint2(20, 20)), Tol));          // in the material
            Assert.Equal(0.0, plate.DistanceTo(new GeoLine2(new GeoPoint2(100, 50), new GeoPoint2(120, 50)), Tol));        // starts on the outline
            Assert.Equal(0.0, plate.DistanceTo(Box(-10, -10, 110, 110), Tol));                                             // holds the whole plate
            Assert.Equal(0.0, plate.DistanceTo(new GeoCircle2(new GeoPoint2(50, 50), 15), Tol));                           // the hole and more
            Assert.Equal(0.0, plate.DistanceTo(new GeoFace2(Box(30, 30, 70, 70), new[] { Box(45, 45, 55, 55) }), Tol));    // rings the hole
        }

        [Fact]
        public void AShapeInTheHole_IsMeasuredToTheHolesRim()
        {
            GeoFace2 plate = Plate();

            Assert.Equal(5.0, plate.DistanceTo(Box(45, 45, 55, 55), Tol), 12);
            Assert.Equal(5.0, plate.DistanceTo(new GeoCircle2(new GeoPoint2(50, 50), 5), Tol), 12);
            Assert.Equal(5.0, plate.DistanceTo(new GeoFace2(Box(45, 45, 55, 55)), Tol), 12);
        }
    }
}
