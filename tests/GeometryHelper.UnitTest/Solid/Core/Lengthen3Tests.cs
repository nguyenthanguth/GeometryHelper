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
    /// Lengthening, extending, trimming and cornering segments in space (Lengthen3 and the GeoLine3 members
    /// delegating to it), with the Tekla cases that motivate them: a beam axis fitted to a column face, to a
    /// column axis it passes at a distance, and to the body of a column.
    /// </summary>
    public class Lengthen3Tests
    {
        private static readonly GeoLine3 Beam = new GeoLine3(0, 0, 3000, 4000, 0, 3000);

        private static void AssertPoint(GeoPoint3 expected, GeoPoint3 actual, double tolerance = 1e-7)
        {
            Assert.True(expected.DistanceTo(actual) <= tolerance, $"expected {expected}, got {actual}");
        }

        private static void AssertLine(GeoLine3 expected, GeoLine3 actual, double tolerance = 1e-7)
        {
            AssertPoint(expected.StartPoint, actual.StartPoint, tolerance);
            AssertPoint(expected.EndPoint, actual.EndPoint, tolerance);
        }

        #region By distance

        [Fact]
        public void Extend_FollowsTheSegmentsOwnDirection()
        {
            var line = new GeoLine3(0, 0, 0, 2, 3, 6);

            AssertLine(new GeoLine3(0, 0, 0, 4, 6, 12), line.Extend(7.0, LineEnd.End));
            AssertLine(new GeoLine3(-2, -3, -6, 2, 3, 6), line.Extend(7.0, LineEnd.Start));
            AssertLine(new GeoLine3(-2, -3, -6, 4, 6, 12), line.Extend(7.0, 7.0));
            AssertLine(new GeoLine3(0, 0, 0, 1, 1.5, 3), line.ExtendToLength(3.5, LineEnd.End));
            Assert.Equal(21.0, line.Extend(7.0, 7.0).Length, 9);
        }

        [Fact]
        public void Extend_RefusesWhatHasNoAnswer()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Beam.Extend(-4000.0, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beam.Extend(-2500.0, -1500.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beam.Extend(double.NaN, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beam.ExtendToLength(0.0, LineEnd.End));
            Assert.Throws<ArgumentOutOfRangeException>(() => Beam.Extend(1.0, (LineEnd)3));
            Assert.Throws<InvalidOperationException>(() => new GeoLine3(1, 1, 1, 1, 1, 1).Extend(5.0, LineEnd.End));
        }

        [Fact]
        public void Extend_ThroughCoreAndInstance_Agree()
        {
            Assert.Equal(Lengthen3.Extend(Beam, 250.0, LineEnd.Start), Beam.Extend(250.0, LineEnd.Start));
            Assert.Equal(Lengthen3.Extend(Beam, 250.0, -100.0, Tolerance.Global), Beam.Extend(250.0, -100.0, Tolerance.Global));
            Assert.Equal(Lengthen3.ExtendToLength(Beam, 6000.0, LineEnd.End), Beam.ExtendToLength(6000.0, LineEnd.End));
        }

        #endregion

        #region To a plane

        [Fact]
        public void ExtendToPlane_FitsABeamEndToAColumnFace()
        {
            var columnFace = new GeoPlane3(new GeoPoint3(5000, 0, 0), GeoVector3.XAxis);

            Assert.True(Beam.TryExtendTo(columnFace, LineEnd.End, out GeoLine3 fitted));
            AssertLine(new GeoLine3(0, 0, 3000, 5000, 0, 3000), fitted);

            // The same face is behind the start, so only the end reaches it.
            Assert.False(Beam.TryExtendTo(columnFace, LineEnd.Start, out GeoLine3 unchanged));
            Assert.Equal(Beam, unchanged);

            // A face the beam runs along is never pierced.
            Assert.False(Beam.TryExtendTo(new GeoPlane3(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis), LineEnd.End, out _));
        }

        [Fact]
        public void TrimToPlane_CutsBackToAFaceTheSegmentCrosses()
        {
            // A mitre cut: the plane x + z = 5000 crosses the beam, at height 3000, where x = 2000.
            var mitre = new GeoPlane3(new GeoPoint3(2000, 0, 3000), new GeoVector3(1, 0, 1));
            Assert.True(Beam.TryTrimTo(mitre, LineEnd.End, out GeoLine3 trimmed));
            AssertLine(new GeoLine3(0, 0, 3000, 2000, 0, 3000), trimmed);

            // The plane x + z = 2000 crosses the line only behind the start, so nothing is left to trim to.
            var behind = new GeoPlane3(new GeoPoint3(2000, 0, 0), new GeoVector3(1, 0, 1));
            Assert.False(Beam.TryTrimTo(behind, LineEnd.End, out _));
            Assert.True(Beam.TryExtendTo(behind, LineEnd.Start, out GeoLine3 extendedBack));
            AssertLine(new GeoLine3(-1000, 0, 3000, 4000, 0, 3000), extendedBack);

            var upright = new GeoPlane3(new GeoPoint3(2500, 0, 0), GeoVector3.XAxis);
            Assert.True(Beam.TryTrimTo(upright, LineEnd.End, out GeoLine3 atUpright));
            AssertLine(new GeoLine3(0, 0, 3000, 2500, 0, 3000), atUpright);
        }

        [Fact]
        public void ExtendOrTrimToPlane_FitsFromEitherSide()
        {
            var face = new GeoPlane3(new GeoPoint3(3000, 0, 0), new GeoVector3(1, 0.2, 0));

            foreach (GeoLine3 beam in new[] { Beam, Beam.ExtendToLength(1000, LineEnd.End) })
            {
                Assert.True(beam.TryExtendTo(face, LineEnd.End, out GeoLine3 fitted) || beam.TryTrimTo(face, LineEnd.End, out fitted));
                Assert.True(face.IsPointOn(fitted.EndPoint));
                Assert.Equal(beam.StartPoint, fitted.StartPoint);
            }
        }

        #endregion

        #region To a point and to another segment

        [Fact]
        public void ExtendToPoint_UsesTheFootOfThePerpendicular()
        {
            Assert.True(Beam.TryExtendTo(new GeoPoint3(6000, 500, -200), LineEnd.End, out GeoLine3 longer));
            AssertLine(new GeoLine3(0, 0, 3000, 6000, 0, 3000), longer);

            Assert.True(Beam.TryTrimTo(new GeoPoint3(1000, 500, -200), LineEnd.Start, out GeoLine3 shorter));
            AssertLine(new GeoLine3(1000, 0, 3000, 4000, 0, 3000), shorter);

            Assert.False(Beam.TryExtendTo(new GeoPoint3(1000, 0, 0), LineEnd.End, out _));
        }

        [Fact]
        public void ExtendToSegment_OnlyWhereTheLinesMeet()
        {
            var column = new GeoLine3(6000, 0, 0, 6000, 0, 4000);

            Assert.True(Beam.TryExtendTo(column, LineEnd.End, out GeoLine3 reached));
            AssertLine(new GeoLine3(0, 0, 3000, 6000, 0, 3000), reached);

            // A column axis the beam passes 150 away from is not met.
            var offsetColumn = new GeoLine3(6000, 150, 0, 6000, 150, 4000);
            Assert.False(Beam.TryExtendTo(offsetColumn, LineEnd.End, out _));

            // Tekla's LineToLine: the common perpendicular, and the end brought level with the column.
            GeoLine3 bridge = Beam.GetClosestOnBoundary(offsetColumn, LineExtension.Both);
            Assert.Equal(150.0, bridge.Length, 9);
            Assert.True(Beam.TryExtendTo(bridge.StartPoint, LineEnd.End, out GeoLine3 level));
            AssertLine(new GeoLine3(0, 0, 3000, 6000, 0, 3000), level);

            // A column too short to reach the level of the beam.
            Assert.False(Beam.TryExtendTo(new GeoLine3(6000, 0, 0, 6000, 0, 2000), LineEnd.End, out _));

            // A parallel axis is never met.
            Assert.False(Beam.TryExtendTo(new GeoLine3(5000, 0, 3000, 7000, 0, 3000), LineEnd.End, out _));
        }

        [Fact]
        public void TrimToSegment_CutsWhereTheOtherCrosses()
        {
            var brace = new GeoLine3(1000, -500, 2500, 1000, 500, 3500);

            Assert.True(Beam.TryTrimTo(brace, LineEnd.End, out GeoLine3 trimmed));
            AssertLine(new GeoLine3(0, 0, 3000, 1000, 0, 3000), trimmed);
        }

        #endregion

        #region To polygons, faces and solids

        private static GeoPolygon3 Slab() => new GeoPolygon3(
            new GeoPoint3(5000, -1000, 0), new GeoPoint3(5000, 1000, 0),
            new GeoPoint3(5000, 1000, 4000), new GeoPoint3(5000, -1000, 4000));

        [Fact]
        public void ExtendToPolygon_StopsWhereItPiercesTheSurface()
        {
            Assert.True(Beam.TryExtendTo(Slab(), LineEnd.End, out GeoLine3 reached));
            AssertLine(new GeoLine3(0, 0, 3000, 5000, 0, 3000), reached);

            // Above the wall: the plane is pierced outside the polygon.
            var high = new GeoLine3(0, 0, 5000, 4000, 0, 5000);
            Assert.False(high.TryExtendTo(Slab(), LineEnd.End, out _));
        }

        [Fact]
        public void ExtendToPolygon_InItsPlane_StopsAtAnEdge()
        {
            var floor = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));
            var along = new GeoLine3(-20, 5, 0, -10, 5, 0);

            Assert.True(along.TryExtendTo(floor, LineEnd.End, out GeoLine3 toNearEdge));
            AssertLine(new GeoLine3(-20, 5, 0, 0, 5, 0), toNearEdge);

            var inside = new GeoLine3(2, 5, 0, 4, 5, 0);
            Assert.True(inside.TryExtendTo(floor, LineEnd.End, out GeoLine3 toFarEdge));
            AssertLine(new GeoLine3(2, 5, 0, 10, 5, 0), toFarEdge);

            // Parallel to the plane but above it: never met.
            Assert.False(new GeoLine3(-20, 5, 1, -10, 5, 1).TryExtendTo(floor, LineEnd.End, out _));
        }

        [Fact]
        public void ExtendToFace_PassesThroughAHole()
        {
            GeoPolygon3 outer = Slab();
            var hole = new GeoPolygon3(
                new GeoPoint3(5000, -200, 2800), new GeoPoint3(5000, 200, 2800),
                new GeoPoint3(5000, 200, 3200), new GeoPoint3(5000, -200, 3200));
            var wall = new GeoFace3(outer, new[] { hole });

            // The beam axis runs through the opening, so the face is never met.
            Assert.False(Beam.TryExtendTo(wall, LineEnd.End, out _));

            // Lower down it hits material.
            var lower = new GeoLine3(0, 0, 1000, 4000, 0, 1000);
            Assert.True(lower.TryExtendTo(wall, LineEnd.End, out GeoLine3 hit));
            AssertLine(new GeoLine3(0, 0, 1000, 5000, 0, 1000), hit);
        }

        [Fact]
        public void ExtendAndTrimToSolid_UseTheNearestFace()
        {
            GeoSolid3 box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10)).ToObb().ToSolid();

            var approaching = new GeoLine3(-20, 5, 5, -10, 5, 5);
            Assert.True(approaching.TryExtendTo(box, LineEnd.End, out GeoLine3 toNearFace));
            AssertLine(new GeoLine3(-20, 5, 5, 0, 5, 5), toNearFace);

            var inside = new GeoLine3(5, 5, 5, 6, 5, 5);
            Assert.True(inside.TryExtendTo(box, LineEnd.End, out GeoLine3 toExit));
            AssertLine(new GeoLine3(5, 5, 5, 10, 5, 5), toExit);

            var through = new GeoLine3(-20, 5, 5, 20, 5, 5);
            Assert.True(through.TryTrimTo(box, LineEnd.End, out GeoLine3 trimmed));
            AssertLine(new GeoLine3(-20, 5, 5, 10, 5, 5), trimmed);

            // Passing beside the box.
            Assert.False(new GeoLine3(-20, 15, 5, -10, 15, 5).TryExtendTo(box, LineEnd.End, out _));
        }

        [Fact]
        public void EveryTargetKind_AgreesBetweenCoreAndInstance()
        {
            var plane = new GeoPlane3(new GeoPoint3(5000, 0, 0), GeoVector3.XAxis);
            var column = new GeoLine3(6000, 0, 0, 6000, 0, 4000);
            var point = new GeoPoint3(4500, 200, 0);
            GeoPolygon3 slab = Slab();
            var face = new GeoFace3(slab);
            GeoSolid3 box = new GeoAabb3(new GeoPoint3(5000, -100, 2000), new GeoPoint3(5500, 100, 4000)).ToObb().ToSolid();

            Assert.Equal(Lengthen3.TryExtendTo(Beam, point, LineEnd.End, out GeoLine3 a1), Beam.TryExtendTo(point, LineEnd.End, out GeoLine3 b1));
            Assert.Equal(a1, b1);
            Assert.Equal(Lengthen3.TryExtendTo(Beam, column, LineEnd.End, out GeoLine3 a2), Beam.TryExtendTo(column, LineEnd.End, out GeoLine3 b2));
            Assert.Equal(a2, b2);
            Assert.Equal(Lengthen3.TryExtendTo(Beam, plane, LineEnd.End, out GeoLine3 a3), Beam.TryExtendTo(plane, LineEnd.End, out GeoLine3 b3));
            Assert.Equal(a3, b3);
            Assert.Equal(Lengthen3.TryExtendTo(Beam, slab, LineEnd.End, out GeoLine3 a4), Beam.TryExtendTo(slab, LineEnd.End, out GeoLine3 b4));
            Assert.Equal(a4, b4);
            Assert.Equal(Lengthen3.TryExtendTo(Beam, face, LineEnd.End, out GeoLine3 a5), Beam.TryExtendTo(face, LineEnd.End, out GeoLine3 b5));
            Assert.Equal(a5, b5);
            Assert.Equal(Lengthen3.TryExtendTo(Beam, box, LineEnd.End, out GeoLine3 a6), Beam.TryExtendTo(box, LineEnd.End, out GeoLine3 b6));
            Assert.Equal(a6, b6);

            GeoLine3 longBeam = Beam.ExtendToLength(8000, LineEnd.End);
            Assert.Equal(Lengthen3.TryTrimTo(longBeam, point, LineEnd.End, out GeoLine3 c1), longBeam.TryTrimTo(point, LineEnd.End, out GeoLine3 d1));
            Assert.Equal(c1, d1);
            Assert.Equal(Lengthen3.TryTrimTo(longBeam, column, LineEnd.End, out GeoLine3 c2), longBeam.TryTrimTo(column, LineEnd.End, out GeoLine3 d2));
            Assert.Equal(c2, d2);
            Assert.Equal(Lengthen3.TryTrimTo(longBeam, plane, LineEnd.End, out GeoLine3 c3), longBeam.TryTrimTo(plane, LineEnd.End, out GeoLine3 d3));
            Assert.Equal(c3, d3);
            Assert.Equal(Lengthen3.TryTrimTo(longBeam, slab, LineEnd.End, out GeoLine3 c4), longBeam.TryTrimTo(slab, LineEnd.End, out GeoLine3 d4));
            Assert.Equal(c4, d4);
            Assert.Equal(Lengthen3.TryTrimTo(longBeam, face, LineEnd.End, out GeoLine3 c5), longBeam.TryTrimTo(face, LineEnd.End, out GeoLine3 d5));
            Assert.Equal(c5, d5);
            Assert.Equal(Lengthen3.TryTrimTo(longBeam, box, LineEnd.End, out GeoLine3 c6), longBeam.TryTrimTo(box, LineEnd.End, out GeoLine3 d6));
            Assert.Equal(c6, d6);
        }

        [Fact]
        public void NullBoundaries_AreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Beam.TryExtendTo((GeoPolygon3)null, LineEnd.End, out _));
            Assert.Throws<ArgumentNullException>(() => Beam.TryExtendTo((GeoFace3)null, LineEnd.End, out _));
            Assert.Throws<ArgumentNullException>(() => Beam.TryExtendTo((GeoSolid3)null, LineEnd.End, out _));
            Assert.Throws<ArgumentNullException>(() => Beam.TryTrimTo((GeoPolygon3)null, LineEnd.End, out _));
            Assert.Throws<ArgumentNullException>(() => Beam.TryTrimTo((GeoFace3)null, LineEnd.End, out _));
            Assert.Throws<ArgumentNullException>(() => Beam.TryTrimTo((GeoSolid3)null, LineEnd.End, out _));
        }

        #endregion

        #region Corner

        [Fact]
        public void Corner_JoinsTwoSegmentsThatMeet()
        {
            var column = new GeoLine3(6000, 0, 0, 6000, 0, 2000);

            Assert.True(Beam.TryTrimExtendToCorner(column, out GeoLine3 beam, out GeoLine3 post));
            AssertLine(new GeoLine3(0, 0, 3000, 6000, 0, 3000), beam);
            AssertLine(new GeoLine3(6000, 0, 0, 6000, 0, 3000), post);
            Assert.Equal(beam.EndPoint, post.EndPoint);
        }

        [Fact]
        public void Corner_OfSegmentsThatPassEachOther_IsRefused()
        {
            var offsetColumn = new GeoLine3(6000, 150, 0, 6000, 150, 2000);

            Assert.False(Beam.TryTrimExtendToCorner(offsetColumn, out GeoLine3 a, out GeoLine3 b));
            Assert.Equal(Beam, a);
            Assert.Equal(offsetColumn, b);
            Assert.False(Beam.TryTrimExtendToCorner(new GeoLine3(0, 100, 3000, 10, 100, 3000), out _, out _));
        }

        #endregion

        #region Invariance

        private static IEnumerable<GeoTransform3> Motions()
        {
            yield return GeoTransform3.Translation(new GeoVector3(1000.0, -2000.0, 3000.0));
            yield return GeoTransform3.RotationAxis(new GeoVector3(1.0, 2.0, 3.0), 1.3);
            yield return GeoTransform3.Translation(new GeoVector3(5.0, 5.0, 5.0))
                .Multiply(GeoTransform3.RotationAxis(new GeoVector3(-3.0, 1.0, 2.0), 2.1));
            yield return GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(1.0, 0.0, 0.0), new GeoVector3(1.0, 1.0, 1.0)));
        }

        [Fact]
        public void ARigidMotionOfTheWholeScene_MovesEveryAnswerWithIt()
        {
            GeoSolid3 box = new GeoAabb3(new GeoPoint3(5000, -100, 2000), new GeoPoint3(5500, 100, 4000)).ToObb().ToSolid();
            var plane = new GeoPlane3(new GeoPoint3(4500, 0, 0), new GeoVector3(1, 0.3, -0.2));
            var column = new GeoLine3(6000, 0, 0, 6000, 0, 4000);

            foreach (GeoTransform3 motion in Motions())
            {
                GeoLine3 movedBeam = Beam.TransformBy(motion);

                Assert.True(Beam.TryExtendTo(box, LineEnd.End, out GeoLine3 toBox));
                Assert.True(movedBeam.TryExtendTo(box.TransformBy(motion), LineEnd.End, out GeoLine3 movedToBox));
                AssertLine(toBox.TransformBy(motion), movedToBox, 1e-6);

                Assert.True(Beam.TryExtendTo(plane, LineEnd.End, out GeoLine3 toPlane));
                Assert.True(movedBeam.TryExtendTo(plane.TransformBy(motion), LineEnd.End, out GeoLine3 movedToPlane));
                AssertLine(toPlane.TransformBy(motion), movedToPlane, 1e-6);

                Assert.True(Beam.TryTrimExtendToCorner(column, out GeoLine3 c1, out GeoLine3 c2));
                Assert.True(movedBeam.TryTrimExtendToCorner(column.TransformBy(motion), out GeoLine3 m1, out GeoLine3 m2));
                AssertLine(c1.TransformBy(motion), m1, 1e-6);
                AssertLine(c2.TransformBy(motion), m2, 1e-6);
            }
        }

        #endregion
    }
}
