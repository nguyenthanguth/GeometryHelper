using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The <c>Try</c> methods of a segment answer false where the segment, or the shape given with it, has a coordinate
    /// that is not a finite number. They answered true and passed NaN or infinite points out, or threw from inside, as a
    /// reflection fuzz of every <c>Try</c> method of <see cref="GeoLine2"/> found.
    /// </summary>
    public class TryOnNonFiniteInputTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-3, 1E-5);

        /// <summary>The segment from (NaN, 0) to (0, 100): its end is a point like any other.</summary>
        private static readonly GeoLine2 NaNLine = new GeoLine2(new GeoPoint2(double.NaN, 0), new GeoPoint2(0, 100));

        /// <summary>The segment from (-1, 0.0001) to (1 000 000, infinity).</summary>
        private static readonly GeoLine2 InfiniteLine = new GeoLine2(-1, 0.0001, 1000000, double.PositiveInfinity);

        private static readonly GeoLine2 Upright = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0, 100));

        private static readonly GeoLine2 Across = new GeoLine2(new GeoPoint2(-50, 50), new GeoPoint2(50, 50));

        [Fact]
        public void ASegmentMeetingASegmentARayOrALine_WithACoordinateNotANumber_IsFalse()
        {
            foreach (GeoLine2 bad in new[] { NaNLine, InfiniteLine })
            {
                Assert.False(Upright.TryIntersectWith(bad, out _, Tol));
                Assert.False(bad.TryIntersectWith(Upright, out _, Tol));
                Assert.False(Upright.TryIntersectWith(bad, LineExtension.Both, out _, Tol));
                Assert.False(bad.TryIntersectWith(new GeoRay2(new GeoPoint2(0, 0), new GeoVector2(0, 1)), out _, Tol));
                Assert.False(
                    Intersection2.TryIntersectWith(
                        new GeoRay2(new GeoPoint2(0, 0), new GeoVector2(0, 1)),
                        bad,
                        out _,
                        Tol
                    )
                );
                Assert.False(
                    bad.TryIntersectWith(new GeoXLine2(new GeoPoint2(0, 100), new GeoVector2(-1, 0.0001)), out _, Tol)
                );
            }

            // A ray from (0, infinity) is no more a place than a segment there.
            Assert.False(
                Across.TryIntersectWith(
                    new GeoRay2(new GeoPoint2(0, double.PositiveInfinity), new GeoVector2(1, 0)),
                    out _,
                    Tol
                )
            );

            // Finite segments still meet: the upright and the segment across it at (0, 50).
            Assert.True(Upright.TryIntersectWith(Across, out GeoPoint2 meeting, Tol));
            Assert.True(meeting.IsEqualTo(new GeoPoint2(0, 50), Tol));
        }

        [Fact]
        public void ASegmentMeetingACircleOrAnArc_WithACoordinateNotANumber_IsFalse_NotAThrow()
        {
            var circle = new GeoCircle2(new GeoPoint2(0.0001, -1), 100);
            GeoArc2 arc = GeoArc2.FromThreePoints(new GeoPoint2(-100, 0), new GeoPoint2(0, 100), new GeoPoint2(100, 0));

            foreach (GeoLine2 bad in new[] { NaNLine, InfiniteLine })
            {
                Assert.False(bad.TryIntersectWith(circle, out GeoPoint2[] a, Tol));
                Assert.Empty(a);
                Assert.False(bad.TryIntersectWith(arc, out GeoPoint2[] b, Tol));
                Assert.Empty(b);
            }

            // An arc with an end at (NaN, 0) has a centre and a radius of NaN; it threw "A radius has to be a number".
            GeoArc2 nanArc = GeoArc2.FromBulge(new GeoPoint2(200, 0), new GeoPoint2(double.NaN, 0), 1E-12);
            Assert.False(Upright.TryIntersectWith(nanArc, out GeoPoint2[] c, Tol));
            Assert.Empty(c);

            Assert.True(Upright.TryIntersectWith(arc, out GeoPoint2[] top, Tol));
            Assert.Single(top);
        }

        [Fact]
        public void ASegmentMeetingAChainOrALoop_WithACoordinateNotANumber_IsFalse_NotAThrow()
        {
            var polyline = new GeoPolyline2(new GeoPoint2(-100, 100), new GeoPoint2(0, 100), new GeoPoint2(100, 0));
            var chain = new GeoPolylineArc2(polyline);
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(-100, 0), new GeoPoint2(100, 0), new GeoPoint2(0, 100) },
                new[] { 0.0, 0.0, 0.2 }
            );

            foreach (GeoLine2 bad in new[] { NaNLine, InfiniteLine })
            {
                Assert.False(bad.TryIntersectWith(polyline, out GeoPoint2[] a, Tol));
                Assert.Empty(a);
                Assert.False(bad.TryIntersectWith(chain, out GeoPoint2[] b, Tol));
                Assert.Empty(b);
                Assert.False(bad.TryIntersectWith(loop, out GeoPoint2[] c, Tol));
                Assert.Empty(c);
            }

            // A loop with a vertex at (NaN, 0) threw "A radius has to be a number" from the arc after it, and one with a
            // bulge of NaN threw "A bulge must be a number".
            var nanVertex = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(double.NaN, 0), new GeoPoint2(0, 100) },
                new[] { 1.0, 2.0, 3.0 }
            );
            var nanBulge = new GeoPolygonArc2(
                new[] { new GeoPoint2(-100, 0), new GeoPoint2(100, 0), new GeoPoint2(0, 100) },
                new[] { double.NaN, 1.0 }
            );
            Assert.False(Across.TryIntersectWith(nanVertex, out _, Tol));
            Assert.False(Across.TryIntersectWith(nanBulge, out _, Tol));

            Assert.True(Across.TryIntersectWith(loop, out GeoPoint2[] crossings, Tol));
            Assert.All(crossings, p => Assert.True(p.IsValid));
        }

        [Fact]
        public void ASegmentMeetingAFaceARectangleOrATriangle_WithACoordinateNotANumber_IsFalse()
        {
            var square = new GeoPolygon2(
                new GeoPoint2(-100, 0),
                new GeoPoint2(100, 0),
                new GeoPoint2(100, 100),
                new GeoPoint2(-100, 100)
            );
            var face = new GeoFace2(square);
            var rectangle = new GeoRectangle2(new GeoPoint2(0, 50), 200, 100);
            var triangle = new GeoTriangle2(new GeoPoint2(-100, 0), new GeoPoint2(100, 0), new GeoPoint2(0, 100));

            foreach (GeoLine2 bad in new[] { NaNLine, InfiniteLine })
            {
                Assert.False(bad.TryIntersectWith(face, out GeoPoint2[] a, Tol));
                Assert.Empty(a);
                Assert.False(face.TryIntersectWith(bad, out _, Tol));
                Assert.False(bad.TryIntersectWith(rectangle, out GeoPoint2[] b, Tol));
                Assert.Empty(b);
                Assert.False(bad.TryIntersectWith(triangle, out GeoPoint2[] c, Tol));
                Assert.Empty(c);
            }

            // The segment from (-150, 50) to (150, 50) crosses the sides of the square at (-100, 50) and (100, 50).
            var through = new GeoLine2(new GeoPoint2(-150, 50), new GeoPoint2(150, 50));
            Assert.True(through.TryIntersectWith(face, out GeoPoint2[] crossings, Tol));
            Assert.Equal(2, crossings.Length);
        }

        [Fact]
        public void ASegmentSplitExtendedOrCornered_WithACoordinateNotANumber_IsFalse()
        {
            foreach (GeoLine2 bad in new[] { NaNLine, InfiniteLine })
            {
                Assert.False(bad.TrySplitAtDistance(1, out _, out _, Tol));
                Assert.False(bad.TryTrimExtendToCorner(Across, out _, out GeoLine2 b, Tol));
                Assert.Equal(Across, b);
            }

            // The end brought level with (infinity, -1): no place to reach, and the segment stays.
            var segment = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            Assert.False(
                segment.TryExtendTo(new GeoPoint2(double.PositiveInfinity, -1), LineEnd.End, out GeoLine2 kept, Tol)
            );
            Assert.Equal(segment, kept);
            Assert.False(segment.TryExtendTo(new GeoPoint2(double.NaN, 0), LineEnd.End, out _, Tol));
            Assert.False(Upright.TrySplitAtDistance(double.NaN, out _, out _, Tol));

            Assert.True(segment.TryExtendTo(new GeoPoint2(150, -1), LineEnd.End, out GeoLine2 longer, Tol));
            Assert.Equal(150, longer.Length, 9);
        }
    }
}
