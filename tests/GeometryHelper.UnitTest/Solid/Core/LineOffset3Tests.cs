using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Offsetting a segment in space (Offset3 for lines), and the closest-approach and intersection queries
    /// that read segments as the infinite lines carrying them.
    /// </summary>
    public class LineOffset3Tests
    {
        private static void AssertPoint(GeoPoint3 expected, GeoPoint3 actual, double tolerance = 1e-9)
        {
            Assert.True(expected.DistanceTo(actual) <= tolerance, $"expected {expected}, got {actual}");
        }

        private static void AssertLine(GeoLine3 expected, GeoLine3 actual, double tolerance = 1e-9)
        {
            AssertPoint(expected.StartPoint, actual.StartPoint, tolerance);
            AssertPoint(expected.EndPoint, actual.EndPoint, tolerance);
        }

        #region Offset in a plane

        [Fact]
        public void OffsetInPlane_WithTheWorldZ_MatchesTheFlatConvention()
        {
            var line = new GeoLine3(0, 0, 5, 10, 0, 5);

            AssertLine(new GeoLine3(0, 2, 5, 10, 2, 5), line.OffsetInPlane(2.0, GeoVector3.ZAxis));
            AssertLine(new GeoLine3(0, -2, 5, 10, -2, 5), line.OffsetInPlane(-2.0, GeoVector3.ZAxis));

            // Seen from below, left is the other side.
            AssertLine(new GeoLine3(0, -2, 5, 10, -2, 5), line.OffsetInPlane(2.0, GeoVector3.ZAxis.Negate()));
        }

        [Fact]
        public void OffsetInPlane_StraightensANormalThatLeansAlongTheSegment()
        {
            var line = new GeoLine3(0, 0, 0, 10, 0, 0);

            AssertLine(line.OffsetInPlane(3.0, GeoVector3.ZAxis), line.OffsetInPlane(3.0, new GeoVector3(1, 0, 1)));
        }

        [Fact]
        public void OffsetInPlane_RefusesANormalThatGivesNoPlane()
        {
            var line = new GeoLine3(0, 0, 0, 10, 0, 0);

            Assert.Throws<ArgumentException>(() => line.OffsetInPlane(1.0, GeoVector3.XAxis));
            Assert.Throws<ArgumentException>(() => line.OffsetInPlane(1.0, new GeoVector3(-5, 0, 0)));
            Assert.Throws<ArgumentException>(() => line.OffsetInPlane(1.0, GeoVector3.Zero));
            Assert.Throws<InvalidOperationException>(() => new GeoLine3(1, 1, 1, 1, 1, 1).OffsetInPlane(1.0, GeoVector3.ZAxis));
            Assert.Throws<ArgumentOutOfRangeException>(() => line.OffsetInPlane(double.NaN, GeoVector3.ZAxis));
        }

        #endregion

        #region Offset towards a direction

        [Fact]
        public void OffsetTowards_UsesOnlyThePartSquareToTheSegment()
        {
            var line = new GeoLine3(0, 0, 0, 10, 0, 0);

            GeoLine3 moved = line.Offset(5.0, new GeoVector3(1, 1, 0));
            AssertLine(new GeoLine3(0, 5, 0, 10, 5, 0), moved);
            Assert.Equal(5.0, line.DistanceTo(moved.MidPoint), 9);

            AssertLine(new GeoLine3(0, 0, -5, 10, 0, -5), line.Offset(-5.0, GeoVector3.ZAxis));
            Assert.Throws<ArgumentException>(() => line.Offset(5.0, GeoVector3.XAxis));
        }

        #endregion

        #region Offset in a beam frame

        [Fact]
        public void OffsetInFrame_MovesAHorizontalBeamLeftAndUp()
        {
            var alongX = new GeoLine3(0, 0, 3000, 6000, 0, 3000);
            AssertLine(new GeoLine3(0, 100, 3050, 6000, 100, 3050), alongX.Offset(100.0, 50.0, GeoVector3.ZAxis));

            // Left of a beam running along +Y, seen from above, is -X.
            var alongY = new GeoLine3(0, 0, 3000, 0, 6000, 3000);
            AssertLine(new GeoLine3(-100, 0, 3000, -100, 6000, 3000), alongY.Offset(100.0, 0.0, GeoVector3.ZAxis));
        }

        [Fact]
        public void OffsetInFrame_TiltsUpWithASlopingBeam()
        {
            var rafter = new GeoLine3(0, 0, 0, 10, 0, 10);
            GeoLine3 raised = rafter.Offset(0.0, Math.Sqrt(2.0), GeoVector3.ZAxis);

            // Up for a 45 degree rafter is square to it, leaning back.
            AssertLine(new GeoLine3(-1, 0, 1, 9, 0, 11), raised);

            // Left of it stays level.
            AssertLine(new GeoLine3(0, 2, 0, 10, 2, 10), rafter.Offset(2.0, 0.0, GeoVector3.ZAxis));
        }

        [Fact]
        public void OffsetInFrame_OfAColumn_NeedsAnUpAcrossIt()
        {
            var column = new GeoLine3(0, 0, 0, 0, 0, 4000);

            Assert.Throws<ArgumentException>(() => column.Offset(100.0, 0.0, GeoVector3.ZAxis));

            // With X as its "up", left is X x Z = -Y.
            AssertLine(new GeoLine3(0, -100, 0, 0, -100, 4000), column.Offset(100.0, 0.0, GeoVector3.XAxis));
            AssertLine(new GeoLine3(30, 0, 0, 30, 0, 4000), column.Offset(0.0, 30.0, GeoVector3.XAxis));
        }

        [Fact]
        public void OffsetInFrame_AgreesWithTheSingleDirectionForms()
        {
            var line = new GeoLine3(1, 2, 3, 7, -1, 4);
            var up = new GeoVector3(0.2, 0.1, 1.0);

            AssertLine(line.OffsetInPlane(2.5, up), line.Offset(2.5, 0.0, up));
            AssertLine(line.Offset(1.5, up), line.Offset(0.0, 1.5, up));
        }

        #endregion

        #region Through a point, and properties

        [Fact]
        public void OffsetThrough_PassesThroughThePoint()
        {
            var line = new GeoLine3(0, 0, 0, 10, 0, 0);

            AssertLine(new GeoLine3(0, 3, 4, 10, 3, 4), line.OffsetThrough(new GeoPoint3(25, 3, 4)));
            AssertLine(line, line.OffsetThrough(new GeoPoint3(5, 0, 0)));
            Assert.Throws<InvalidOperationException>(() => new GeoLine3(1, 1, 1, 1, 1, 1).OffsetThrough(new GeoPoint3(0, 0, 0)));
        }

        [Fact]
        public void EveryOffset_KeepsLengthAndDirectionAndLandsAtTheDistance()
        {
            var rng = new Random(5);

            for (int i = 0; i < 200; i++)
            {
                var line = new GeoLine3(rng.NextDouble() * 100, rng.NextDouble() * 100, rng.NextDouble() * 100,
                                        rng.NextDouble() * 100, rng.NextDouble() * 100, rng.NextDouble() * 100);
                var normal = new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5);
                if (line.Length < 1.0 || normal.CrossProduct(line.Direction).Length < 0.1 * normal.Length * line.Length) { continue; }
                double distance = rng.NextDouble() * 40 - 20;

                foreach (GeoLine3 moved in new[] { line.OffsetInPlane(distance, normal), line.Offset(distance, normal) })
                {
                    Assert.Equal(line.Length, moved.Length, 9);
                    Assert.True(line.Direction.IsCodirectionalTo(moved.Direction));
                    Assert.Equal(Math.Abs(distance), line.StartPoint.DistanceTo(moved.StartPoint), 9);
                    Assert.Equal(0.0, line.Direction.DotProduct(line.StartPoint.GetVectorTo(moved.StartPoint)), 6);
                }
            }
        }

        [Fact]
        public void EveryOffset_TurnsWithARotationOfTheWholeScene()
        {
            var line = new GeoLine3(1, 2, 3, 7, -1, 4);
            var normal = new GeoVector3(0.2, 0.1, 1.0);
            var point = new GeoPoint3(3, 3, 3);

            foreach (GeoTransform3 motion in new[]
            {
                GeoTransform3.RotationAxis(new GeoVector3(1.0, 2.0, 3.0), 1.3),
                GeoTransform3.Translation(new GeoVector3(1000, -500, 250)).Multiply(GeoTransform3.RotationZ(2.2))
            })
            {
                GeoLine3 moved = line.TransformBy(motion);
                GeoVector3 movedNormal = normal.TransformBy(motion);

                AssertLine(line.OffsetInPlane(2.0, normal).TransformBy(motion), moved.OffsetInPlane(2.0, movedNormal), 1e-8);
                AssertLine(line.Offset(2.0, normal).TransformBy(motion), moved.Offset(2.0, movedNormal), 1e-8);
                AssertLine(line.Offset(2.0, -1.0, normal).TransformBy(motion), moved.Offset(2.0, -1.0, movedNormal), 1e-8);
                AssertLine(line.OffsetThrough(point).TransformBy(motion), moved.OffsetThrough(point.TransformBy(motion)), 1e-8);
            }
        }

        [Fact]
        public void Offset_ThroughCoreAndInstance_Agree()
        {
            var line = new GeoLine3(1, 2, 3, 7, -1, 4);
            var up = GeoVector3.ZAxis;

            Assert.Equal(Offset3.OffsetInPlane(line, 2.0, up), line.OffsetInPlane(2.0, up));
            Assert.Equal(Offset3.OffsetInPlane(line, 2.0, up, Tolerance.Global), line.OffsetInPlane(2.0, up, Tolerance.Global));
            Assert.Equal(Offset3.Offset(line, 2.0, up), line.Offset(2.0, up));
            Assert.Equal(Offset3.Offset(line, 2.0, 1.0, up), line.Offset(2.0, 1.0, up));
            Assert.Equal(Offset3.OffsetThrough(line, new GeoPoint3(0, 0, 0)), line.OffsetThrough(new GeoPoint3(0, 0, 0)));
        }

        #endregion

        #region Closest approach and intersection with extension

        [Fact]
        public void ClosestSegment_ReachesPastTheNamedSegmentsOnly()
        {
            var shortLine = new GeoLine3(0, 0, 0, 1, 0, 0);
            var farCross = new GeoLine3(5, -1, 3, 5, 1, 3);

            AssertLine(new GeoLine3(5, 0, 0, 5, 0, 3), shortLine.GetClosestOnBoundary(farCross, LineExtension.Both));
            AssertLine(new GeoLine3(1, 0, 0, 5, 0, 3), shortLine.GetClosestOnBoundary(farCross, LineExtension.None));
            AssertLine(new GeoLine3(1, 0, 0, 5, 0, 3), shortLine.GetClosestOnBoundary(farCross, LineExtension.Second));

            // The second segment does not reach y = 0, so its start is the nearest it can offer.
            var raised = new GeoLine3(5, 2, 3, 5, 4, 3);
            AssertLine(new GeoLine3(5, 0, 0, 5, 2, 3), shortLine.GetClosestOnBoundary(raised, LineExtension.First));
        }

        [Fact]
        public void ClosestSegment_BetweenParallelLines_IsTheirGap()
        {
            var a = new GeoLine3(0, 0, 0, 1, 0, 0);
            var b = new GeoLine3(7, 2, 0, 9, 2, 0);

            foreach (LineExtension extension in new[] { LineExtension.First, LineExtension.Second, LineExtension.Both })
            {
                Assert.Equal(2.0, a.GetClosestOnBoundary(b, extension).Length, 9);
            }
        }

        [Fact]
        public void ClosestSegment_NoExtension_MatchesThePlainOverload()
        {
            var rng = new Random(8);

            for (int i = 0; i < 200; i++)
            {
                var a = new GeoLine3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));
                var b = new GeoLine3(rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10), rng.Next(-9, 10));

                Assert.Equal(Projection3.GetClosestSegment(a, b), Projection3.GetClosestSegment(a, b, LineExtension.None));

                // Allowing either line to reach further can only bring them closer.
                double none = a.GetClosestOnBoundary(b, LineExtension.None).Length;
                Assert.True(a.GetClosestOnBoundary(b, LineExtension.First).Length <= none + 1e-9);
                Assert.True(a.GetClosestOnBoundary(b, LineExtension.Second).Length <= none + 1e-9);
                Assert.True(a.GetClosestOnBoundary(b, LineExtension.Both).Length <= none + 1e-9);
            }
        }

        [Fact]
        public void ClosestSegment_BothExtended_IsSquareToBothLines()
        {
            var rng = new Random(9);

            for (int i = 0; i < 200; i++)
            {
                var a = new GeoLine3(rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10);
                var b = new GeoLine3(rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10, rng.NextDouble() * 10);
                if (a.Length < 0.5 || b.Length < 0.5 || a.IsParallelTo(b)) { continue; }

                GeoLine3 bridge = a.GetClosestOnBoundary(b, LineExtension.Both);
                if (bridge.Length < 1e-6) { continue; }

                Assert.Equal(0.0, bridge.Direction.DotProduct(a.Direction) / (bridge.Length * a.Length), 9);
                Assert.Equal(0.0, bridge.Direction.DotProduct(b.Direction) / (bridge.Length * b.Length), 9);
            }
        }

        [Fact]
        public void IntersectWith_Extension_FindsWhereTheLinesMeet()
        {
            var a = new GeoLine3(0, 0, 0, 1, 1, 0);
            var b = new GeoLine3(10, 0, 0, 11, -1, 0);

            Assert.False(a.TryIntersectWith(b, LineExtension.None, out _));
            Assert.True(a.TryIntersectWith(b, LineExtension.Both, out GeoPoint3 crossing));
            AssertPoint(new GeoPoint3(5, 5, 0), crossing);
            Assert.Equal(Intersection3.GetIntersection(a, b, LineExtension.Both), crossing);
            Assert.Null(Intersection3.GetIntersection(a, b, LineExtension.First));

            // Lifted off the plane: the axes pass each other and never meet.
            var lifted = new GeoLine3(10, 0, 1, 11, -1, 1);
            Assert.False(a.TryIntersectWith(lifted, LineExtension.Both, out _));

            Assert.Throws<ArgumentOutOfRangeException>(() => a.TryIntersectWith(b, (LineExtension)9, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => a.GetClosestOnBoundary(b, (LineExtension)9));
        }

        #endregion
    }
}
