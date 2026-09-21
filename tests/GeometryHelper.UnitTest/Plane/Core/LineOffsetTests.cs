using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Offsetting a segment sideways (Offset2 for lines), intersecting segments read as infinite lines
    /// (Intersection2 with a LineExtension), and the small GeoLine2 members the new operations rely on.
    /// </summary>
    public class LineOffsetTests
    {
        private static void AssertPoint(GeoPoint2 expected, GeoPoint2 actual, double tolerance = 1e-9)
        {
            Assert.True(expected.DistanceTo(actual) <= tolerance, $"expected {expected}, got {actual}");
        }

        private static void AssertLine(GeoLine2 expected, GeoLine2 actual, double tolerance = 1e-9)
        {
            AssertPoint(expected.StartPoint, actual.StartPoint, tolerance);
            AssertPoint(expected.EndPoint, actual.EndPoint, tolerance);
        }

        #region Offset

        [Fact]
        public void Offset_PositiveGoesToTheLeftOfTheDirection()
        {
            var line = new GeoLine2(0, 0, 10, 0);

            AssertLine(new GeoLine2(0, 2, 10, 2), line.Offset(2.0));
            AssertLine(new GeoLine2(0, -2, 10, -2), line.Offset(-2.0));

            // The left of a segment drawn the other way is the other side.
            AssertLine(new GeoLine2(10, -2, 0, -2), line.Reverse().Offset(2.0));
        }

        [Fact]
        public void Offset_OfAnObliqueSegment_MovesSquareToIt()
        {
            var line = new GeoLine2(0, 0, 3, 4);

            // Left of (0.6, 0.8) is (-0.8, 0.6).
            AssertLine(new GeoLine2(-4, 3, -1, 7), line.Offset(5.0));
            Assert.Equal(5.0, line.Offset(5.0).Length, 12);
        }

        [Fact]
        public void Offset_KeepsLengthAndDirectionAndLandsAtTheDistance()
        {
            var rng = new Random(11);

            for (int i = 0; i < 100; i++)
            {
                var line = new GeoLine2(rng.NextDouble() * 100, rng.NextDouble() * 100, rng.NextDouble() * 100, rng.NextDouble() * 100);
                if (line.Length < 1.0) { continue; }
                double distance = rng.NextDouble() * 40 - 20;

                GeoLine2 moved = line.Offset(distance);

                Assert.Equal(line.Length, moved.Length, 9);
                Assert.True(line.Direction.IsParallelTo(moved.Direction));
                Assert.True(line.Direction.DotProduct(moved.Direction) > 0);

                // Signed distance from the original carrier, positive on the left.
                double signed = line.Direction.CrossProduct(line.StartPoint.GetVectorTo(moved.StartPoint)) / line.Length;
                Assert.Equal(distance, signed, 9);

                // Offsetting back returns the original.
                AssertLine(line, moved.Offset(-distance), 1e-9);
            }
        }

        [Fact]
        public void OffsetThrough_PassesThroughThePointOnEitherSide()
        {
            var line = new GeoLine2(0, 0, 10, 0);

            AssertLine(new GeoLine2(0, 7, 10, 7), line.OffsetThrough(new GeoPoint2(3, 7)));

            // The point need not lie between the ends: only the square distance counts.
            AssertLine(new GeoLine2(0, -3, 10, -3), line.OffsetThrough(new GeoPoint2(20, -3)));

            // A point on the line gives the line back.
            AssertLine(line, line.OffsetThrough(new GeoPoint2(4, 0)));
        }

        [Fact]
        public void Offset_ThroughCoreAndInstance_Agree()
        {
            var line = new GeoLine2(1, 2, 7, -3);

            Assert.Equal(Offset2.Offset(line, 2.5), line.Offset(2.5));
            Assert.Equal(Offset2.Offset(line, 2.5, Tolerance.Global), line.Offset(2.5, Tolerance.Global));
            Assert.Equal(Offset2.OffsetThrough(line, new GeoPoint2(4, 4)), line.OffsetThrough(new GeoPoint2(4, 4)));
        }

        [Fact]
        public void Offset_OfADegenerateSegment_OrByANonFiniteDistance_IsRefused()
        {
            var point = new GeoLine2(2, 2, 2, 2);

            Assert.Throws<InvalidOperationException>(() => point.Offset(1.0));
            Assert.Throws<InvalidOperationException>(() => point.OffsetThrough(new GeoPoint2(5, 5)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLine2(0, 0, 1, 0).Offset(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLine2(0, 0, 1, 0).Offset(double.PositiveInfinity));
        }

        #endregion

        #region Intersection with extension

        [Fact]
        public void IntersectWith_Extension_ReachesPastTheNamedSegmentsOnly()
        {
            var shortLine = new GeoLine2(0, 0, 1, 0);
            var farUpright = new GeoLine2(5, -1, 5, 1);

            Assert.False(shortLine.TryIntersectWith(farUpright, LineExtension.None, out _));
            Assert.True(shortLine.TryIntersectWith(farUpright, LineExtension.First, out GeoPoint2 reached));
            AssertPoint(new GeoPoint2(5, 0), reached);
            Assert.False(shortLine.TryIntersectWith(farUpright, LineExtension.Second, out _));
            Assert.True(shortLine.TryIntersectWith(farUpright, LineExtension.Both, out GeoPoint2 both));
            AssertPoint(new GeoPoint2(5, 0), both);

            var highUpright = new GeoLine2(0.5, 2, 0.5, 3);
            Assert.True(shortLine.TryIntersectWith(highUpright, LineExtension.Second, out GeoPoint2 onShort));
            AssertPoint(new GeoPoint2(0.5, 0), onShort);
            Assert.False(shortLine.TryIntersectWith(highUpright, LineExtension.First, out _));

            // Two segments far from each other and from the crossing of their lines.
            var a = new GeoLine2(0, 0, 1, 1);
            var b = new GeoLine2(10, 0, 11, -1);
            Assert.True(a.TryIntersectWith(b, LineExtension.Both, out GeoPoint2 far));
            AssertPoint(new GeoPoint2(5, 5), far);
        }

        [Fact]
        public void IntersectWith_NoExtension_MatchesThePlainOverload()
        {
            var rng = new Random(3);

            for (int i = 0; i < 300; i++)
            {
                var a = new GeoLine2(rng.Next(-10, 11), rng.Next(-10, 11), rng.Next(-10, 11), rng.Next(-10, 11));
                var b = new GeoLine2(rng.Next(-10, 11), rng.Next(-10, 11), rng.Next(-10, 11), rng.Next(-10, 11));

                bool plain = a.TryIntersectWith(b, out GeoPoint2 p1);
                bool none = a.TryIntersectWith(b, LineExtension.None, out GeoPoint2 p2);

                Assert.Equal(plain, none);
                if (plain) { Assert.Equal(p1, p2); }

                // Extending can only find more, never fewer.
                if (plain) { Assert.True(a.TryIntersectWith(b, LineExtension.Both, out _)); }
            }
        }

        [Fact]
        public void IntersectWith_Extension_RefusesParallelLines()
        {
            var a = new GeoLine2(0, 0, 10, 0);

            foreach (LineExtension extension in new[] { LineExtension.None, LineExtension.First, LineExtension.Second, LineExtension.Both })
            {
                Assert.False(a.TryIntersectWith(new GeoLine2(0, 5, 10, 5), extension, out _));
                Assert.False(a.TryIntersectWith(new GeoLine2(20, 0, 30, 0), extension, out _));
                Assert.Null(a.GetIntersection(new GeoLine2(0, 5, 10, 5), extension));
            }
        }

        [Fact]
        public void IntersectWith_Extension_ThroughCoreAndInstance_Agree()
        {
            var a = new GeoLine2(0, 0, 1, 1);
            var b = new GeoLine2(10, 0, 11, -1);

            Assert.Equal(Intersection2.GetIntersection(a, b, LineExtension.Both), a.GetIntersection(b, LineExtension.Both));
            Assert.Equal(Intersection2.GetIntersection(a, b, LineExtension.Both, Tolerance.Global), a.GetIntersection(b, LineExtension.Both, Tolerance.Global));
            Assert.Null(a.GetIntersection(b, LineExtension.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.TryIntersectWith(b, (LineExtension)9, out _));
        }

        #endregion

        #region New GeoLine2 members

        [Fact]
        public void Reverse_SwapsTheEnds()
        {
            var line = new GeoLine2(1, 2, 3, 4);

            Assert.Equal(new GeoLine2(3, 4, 1, 2), line.Reverse());
            Assert.Equal(line, line.Reverse().Reverse());
        }

        [Fact]
        public void IsDegenerate_FollowsTheTolerance()
        {
            Assert.True(new GeoLine2(1, 1, 1, 1).IsDegenerate());
            Assert.True(new GeoLine2(1, 1, 1.00005, 1).IsDegenerate());
            Assert.False(new GeoLine2(1, 1, 1.001, 1).IsDegenerate());
            Assert.True(new GeoLine2(1, 1, 1.001, 1).IsDegenerate(new Tolerance(0.01, 0.01)));
        }

        [Fact]
        public void TranslateAndRotate_MoveBothEnds()
        {
            var line = new GeoLine2(1, 0, 3, 0);

            Assert.Equal(new GeoLine2(2, 5, 4, 5), line.Translate(new GeoVector2(1, 5)));

            GeoLine2 turned = line.RotateBy(Math.PI / 2.0, new GeoPoint2(0, 0));
            AssertPoint(new GeoPoint2(0, 1), turned.StartPoint);
            AssertPoint(new GeoPoint2(0, 3), turned.EndPoint);
        }

        #endregion
    }
}
