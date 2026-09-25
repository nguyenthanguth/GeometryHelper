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
    /// Whether two shapes meet, and where. The arithmetic was all in Core already; what was missing was any
    /// way to ask a shape itself, and in the few places there was one it ran only one way round — a solid
    /// could be asked about a segment, a segment could not be asked about a solid. These hold both ways open
    /// and pin them to the answers Core gives, so a direction wired to the wrong operation is caught here.
    /// </summary>
    public class MeetingTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoAabb3 Box() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100));

        /// <summary>
        /// A segment running clean through the box along y = z = 50.
        /// </summary>
        private static GeoLine3 Through() => new GeoLine3(new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50));

        /// <summary>
        /// The same run, well clear of the box.
        /// </summary>
        private static GeoLine3 Past() => new GeoLine3(new GeoPoint3(-50, 500, 50), new GeoPoint3(150, 500, 50));

        /// <summary>
        /// A square in the plane z = 50, overhanging the box on three sides.
        /// </summary>
        private static GeoPolygon3 Sheet() => new GeoPolygon3(
            new GeoPoint3(-50, 50, 50), new GeoPoint3(150, 50, 50),
            new GeoPoint3(150, 150, 50), new GeoPoint3(-50, 150, 50));

        private static GeoPoint3[] Sorted(GeoPoint3[] points) =>
            points.OrderBy(point => point.X).ThenBy(point => point.Y).ThenBy(point => point.Z).ToArray();

        private static void Same(GeoPoint3[] expected, GeoPoint3[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);

            GeoPoint3[] left = Sorted(expected);
            GeoPoint3[] right = Sorted(actual);

            for (int i = 0; i < left.Length; i++)
            {
                Assert.True(left[i].IsEqualTo(right[i], Loose), $"{left[i]} != {right[i]}");
            }
        }

        [Fact]
        public void ASegmentCanBeAskedAboutTheBoxesAndTheSolidAndTheBoxesAboutIt()
        {
            GeoAabb3 aabb = Box();
            GeoObb3 obb = aabb.ToObb();
            GeoSolid3 solid = obb.ToSolid();
            GeoLine3 through = Through();
            GeoLine3 past = Past();

            Assert.True(through.CollidesWith(aabb));
            Assert.True(through.CollidesWith(obb));
            Assert.True(through.CollidesWith(solid));

            Assert.False(past.CollidesWith(aabb));
            Assert.False(past.CollidesWith(obb));
            Assert.False(past.CollidesWith(solid));

            // And the box answers the same question about the segment, which is the direction that was missing.
            Assert.True(aabb.CollidesWith(through));
            Assert.True(obb.CollidesWith(through));
            Assert.False(aabb.CollidesWith(past));
            Assert.False(obb.CollidesWith(past));

            // It enters at x = 0 and leaves at x = 100, whichever shape is asked and whichever way round.
            var expected = new[] { new GeoPoint3(0, 50, 50), new GeoPoint3(100, 50, 50) };

            Same(expected, through.GetIntersections(aabb));
            Same(expected, through.GetIntersections(obb));
            Same(expected, through.GetIntersections(solid));
            Same(expected, aabb.GetIntersections(through));
            Same(expected, obb.GetIntersections(through));
            Same(expected, solid.GetIntersections(through));

            Assert.Empty(past.GetIntersections(aabb));
            Assert.Empty(aabb.GetIntersections(past));
        }

        [Fact]
        public void ARayCanBeAskedTheSameAndTheBoxesAnswerBack()
        {
            GeoAabb3 aabb = Box();
            GeoObb3 obb = aabb.ToObb();
            GeoSolid3 solid = obb.ToSolid();

            var into = new GeoRay3(new GeoPoint3(-50, 50, 50), new GeoVector3(1, 0, 0));
            var away = new GeoRay3(new GeoPoint3(-50, 50, 50), new GeoVector3(-1, 0, 0));

            Assert.True(into.CollidesWith(solid));
            Assert.False(away.CollidesWith(solid));

            var expected = new[] { new GeoPoint3(0, 50, 50), new GeoPoint3(100, 50, 50) };

            Same(expected, into.GetIntersections(aabb));
            Same(expected, into.GetIntersections(obb));
            Same(expected, into.GetIntersections(solid));
            Same(expected, aabb.GetIntersections(into));
            Same(expected, obb.GetIntersections(into));

            // A ray only ever goes one way, so the one pointing off never reaches the box at all.
            Assert.Empty(away.GetIntersections(aabb));
            Assert.Empty(aabb.GetIntersections(away));
        }

        [Fact]
        public void APlaneAnswersAboutSegmentsRaysAndSolids()
        {
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 50), new GeoVector3(0, 0, 1));
            GeoSolid3 solid = Box().ToObb().ToSolid();

            var upright = new GeoLine3(new GeoPoint3(30, 40, 0), new GeoPoint3(30, 40, 100));
            var ray = new GeoRay3(new GeoPoint3(30, 40, 0), new GeoVector3(0, 0, 1));

            Assert.True(plane.TryIntersectWith(upright, out GeoPoint3 fromPlane));
            Assert.True(upright.TryIntersectWith(plane, out GeoPoint3 fromLine));
            Assert.True(fromPlane.IsEqualTo(new GeoPoint3(30, 40, 50), Loose));
            Assert.True(fromPlane.IsEqualTo(fromLine, Loose));

            Assert.True(plane.TryIntersectWith(ray, out GeoPoint3 fromRay));
            Assert.True(fromRay.IsEqualTo(new GeoPoint3(30, 40, 50), Loose));

            // A segment lying wholly to one side never reaches the plane.
            Assert.False(plane.TryIntersectWith(new GeoLine3(new GeoPoint3(0, 0, 60), new GeoPoint3(100, 0, 90)), out _));

            // The cube is cut at half height, so every point that comes back is on the plane, and the plane
            // is asked the same thing the solid has always been able to answer.
            GeoPoint3[] cut = plane.GetIntersections(solid);

            Assert.NotEmpty(cut);
            Same(solid.GetIntersections(plane), cut);
            Assert.All(cut, point => Assert.Equal(50.0, point.Z, 7));

            Assert.Empty(new GeoPlane3(new GeoPoint3(0, 0, 500), new GeoVector3(0, 0, 1)).GetIntersections(solid));
        }

        [Fact]
        public void FacesPolygonsAndTrianglesKnowWhatTheyTouch()
        {
            GeoObb3 obb = Box().ToObb();
            GeoSolid3 solid = obb.ToSolid();
            GeoPolygon3 sheet = Sheet();
            var face = new GeoFace3(sheet);

            GeoPolygon3 clear = sheet.Translate(new GeoVector3(0, 0, 1000));

            Assert.True(sheet.CollidesWith(obb));
            Assert.True(sheet.CollidesWith(solid));
            Assert.True(obb.CollidesWith(sheet));
            Assert.True(face.CollidesWith(solid));
            Assert.True(solid.CollidesWith(face));

            Assert.False(clear.CollidesWith(obb));
            Assert.False(clear.CollidesWith(solid));
            Assert.False(obb.CollidesWith(clear));
            Assert.False(new GeoFace3(clear).CollidesWith(solid));

            // Two triangles, one flat and one standing on edge through it.
            var flat = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
            var standing = new GeoTriangle3(new GeoPoint3(20, 20, -10), new GeoPoint3(20, 20, 10), new GeoPoint3(60, 20, 0));

            Assert.True(flat.CollidesWith(standing));
            Assert.True(standing.CollidesWith(flat));
            Assert.False(flat.CollidesWith(standing.Translate(new GeoVector3(0, 0, 1000))));
        }

        [Fact]
        public void ASegmentOrRayCanBeAskedWhereItPiercesAFaceOrAPolygon()
        {
            GeoPolygon3 sheet = Sheet();
            var face = new GeoFace3(sheet);

            var upright = new GeoLine3(new GeoPoint3(50, 75, -50), new GeoPoint3(50, 75, 150));
            var ray = new GeoRay3(new GeoPoint3(50, 75, -50), new GeoVector3(0, 0, 1));
            var expected = new GeoPoint3(50, 75, 50);

            Assert.True(upright.TryIntersectWith(face, out GeoPoint3 a));
            Assert.True(upright.TryIntersectWith(sheet, out GeoPoint3 b));
            Assert.True(ray.TryIntersectWith(face, out GeoPoint3 c));
            Assert.True(ray.TryIntersectWith(sheet, out GeoPoint3 d));

            foreach (GeoPoint3 found in new[] { a, b, c, d })
            {
                Assert.True(found.IsEqualTo(expected, Loose), found.ToString());
            }

            // The same asked the way round that already existed gives the same point.
            Assert.True(face.TryIntersectWith(upright, out GeoPoint3 e));
            Assert.True(sheet.TryIntersectWith(ray, out GeoPoint3 f));
            Assert.True(e.IsEqualTo(expected, Loose));
            Assert.True(f.IsEqualTo(expected, Loose));

            // A run that misses the sheet sideways pierces nothing.
            var beside = new GeoLine3(new GeoPoint3(50, 500, -50), new GeoPoint3(50, 500, 150));

            Assert.False(beside.TryIntersectWith(face, out _));
            Assert.False(beside.TryIntersectWith(sheet, out _));
        }

        [Fact]
        public void ASegmentGivesBackThePointItMeetsAnotherAtOrNothing()
        {
            var along = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var across = new GeoLine3(new GeoPoint3(50, -50, 0), new GeoPoint3(50, 50, 0));
            var skew = new GeoLine3(new GeoPoint3(50, -50, 10), new GeoPoint3(50, 50, 10));

            GeoPoint3? met = along.GetIntersection(across);

            Assert.True(met.HasValue);
            Assert.True(met.Value.IsEqualTo(new GeoPoint3(50, 0, 0), Loose));

            // Passing over at a height is not meeting.
            Assert.False(along.GetIntersection(skew).HasValue);

            // Off the end of this one, so nothing until this one is read as the line carrying it.
            var beyond = new GeoLine3(new GeoPoint3(200, -50, 0), new GeoPoint3(200, 50, 0));

            Assert.False(along.GetIntersection(beyond).HasValue);
            Assert.False(along.GetIntersection(beyond, LineExtension.None).HasValue);

            GeoPoint3? extended = along.GetIntersection(beyond, LineExtension.First);

            Assert.True(extended.HasValue);
            Assert.True(extended.Value.IsEqualTo(new GeoPoint3(200, 0, 0), Loose));

            // And it agrees with the TryIntersectWith beside it.
            Assert.True(along.TryIntersectWith(across, out GeoPoint3 tried));
            Assert.True(tried.IsEqualTo(met.Value, Loose));
        }

        [Fact]
        public void EveryNewMethodTakesAToleranceAndAnswersTheSameWithTheDefaultOne()
        {
            Tolerance global = Tolerance.Global;
            GeoAabb3 aabb = Box();
            GeoObb3 obb = aabb.ToObb();
            GeoSolid3 solid = obb.ToSolid();
            GeoLine3 through = Through();
            GeoPolygon3 sheet = Sheet();
            var face = new GeoFace3(sheet);
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 50), new GeoVector3(0, 0, 1));
            var ray = new GeoRay3(new GeoPoint3(-50, 50, 50), new GeoVector3(1, 0, 0));
            var flat = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
            var standing = new GeoTriangle3(new GeoPoint3(20, 20, -10), new GeoPoint3(20, 20, 10), new GeoPoint3(60, 20, 0));

            Assert.Equal(through.CollidesWith(aabb), through.CollidesWith(aabb, global));
            Assert.Equal(through.CollidesWith(obb), through.CollidesWith(obb, global));
            Assert.Equal(through.CollidesWith(solid), through.CollidesWith(solid, global));
            Assert.Equal(aabb.CollidesWith(obb), aabb.CollidesWith(obb, global));
            Assert.Equal(aabb.CollidesWith(through), aabb.CollidesWith(through, global));
            Assert.Equal(obb.CollidesWith(aabb), obb.CollidesWith(aabb, global));
            Assert.Equal(obb.CollidesWith(through), obb.CollidesWith(through, global));
            Assert.Equal(obb.CollidesWith(solid), obb.CollidesWith(solid, global));
            Assert.Equal(obb.CollidesWith(sheet), obb.CollidesWith(sheet, global));
            Assert.Equal(ray.CollidesWith(solid), ray.CollidesWith(solid, global));
            Assert.Equal(sheet.CollidesWith(obb), sheet.CollidesWith(obb, global));
            Assert.Equal(sheet.CollidesWith(solid), sheet.CollidesWith(solid, global));
            Assert.Equal(face.CollidesWith(solid), face.CollidesWith(solid, global));
            Assert.Equal(flat.CollidesWith(standing), flat.CollidesWith(standing, global));

            Same(through.GetIntersections(aabb), through.GetIntersections(aabb, global));
            Same(through.GetIntersections(obb), through.GetIntersections(obb, global));
            Same(through.GetIntersections(solid), through.GetIntersections(solid, global));
            Same(ray.GetIntersections(aabb), ray.GetIntersections(aabb, global));
            Same(ray.GetIntersections(obb), ray.GetIntersections(obb, global));
            Same(ray.GetIntersections(solid), ray.GetIntersections(solid, global));
            Same(aabb.GetIntersections(through), aabb.GetIntersections(through, global));
            Same(aabb.GetIntersections(ray), aabb.GetIntersections(ray, global));
            Same(obb.GetIntersections(through), obb.GetIntersections(through, global));
            Same(obb.GetIntersections(ray), obb.GetIntersections(ray, global));
            Same(plane.GetIntersections(solid), plane.GetIntersections(solid, global));

            var upright = new GeoLine3(new GeoPoint3(50, 75, -50), new GeoPoint3(50, 75, 150));
            var rising = new GeoRay3(new GeoPoint3(50, 75, -50), new GeoVector3(0, 0, 1));

            Assert.Equal(upright.TryIntersectWith(face, out GeoPoint3 p1), upright.TryIntersectWith(face, out GeoPoint3 q1, global));
            Assert.Equal(upright.TryIntersectWith(sheet, out GeoPoint3 p2), upright.TryIntersectWith(sheet, out GeoPoint3 q2, global));
            Assert.Equal(rising.TryIntersectWith(face, out GeoPoint3 p3), rising.TryIntersectWith(face, out GeoPoint3 q3, global));
            Assert.Equal(rising.TryIntersectWith(sheet, out GeoPoint3 p4), rising.TryIntersectWith(sheet, out GeoPoint3 q4, global));
            Assert.Equal(plane.TryIntersectWith(upright, out GeoPoint3 p5), plane.TryIntersectWith(upright, out GeoPoint3 q5, global));
            Assert.Equal(plane.TryIntersectWith(rising, out GeoPoint3 p6), plane.TryIntersectWith(rising, out GeoPoint3 q6, global));

            foreach (var pair in new[] { (p1, q1), (p2, q2), (p3, q3), (p4, q4), (p5, q5), (p6, q6) })
            {
                Assert.True(pair.Item1.IsEqualTo(pair.Item2, Loose));
            }

            var along = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var across = new GeoLine3(new GeoPoint3(50, -50, 0), new GeoPoint3(50, 50, 0));

            Assert.Equal(along.GetIntersection(across), along.GetIntersection(across, global));
            Assert.Equal(
                along.GetIntersection(across, LineExtension.Both),
                along.GetIntersection(across, LineExtension.Both, global));
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            GeoLine3 line = Through();
            GeoObb3 obb = Box().ToObb();

            Assert.Throws<ArgumentNullException>(() => line.CollidesWith((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => line.GetIntersections((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => line.TryIntersectWith((GeoFace3)null, out _));
            Assert.Throws<ArgumentNullException>(() => line.TryIntersectWith((GeoPolygon3)null, out _));
            Assert.Throws<ArgumentNullException>(() => obb.CollidesWith((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => obb.CollidesWith((GeoPolygon3)null));
        }
    }
}
