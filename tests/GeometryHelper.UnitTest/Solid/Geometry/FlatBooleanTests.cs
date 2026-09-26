using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Combining two flat shapes in space, and two boxes. <see cref="Boolean3"/> combined bodies and nothing
    /// else, so a <see cref="GeoPolygon3"/>, a <see cref="GeoFace3"/> and a <see cref="GeoObb3"/> could not be
    /// combined at all.
    /// </summary>
    /// <remarks>
    /// The flat pairs are the coplanar lift, so the test that earns its keep is the one comparing the answer with
    /// the plane's on the same shapes laid flat. The boxes go through the body they bound, which is exact: six
    /// flat faces, no fitting.
    /// </remarks>
    public class FlatBooleanTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        /// <summary>A square in z = 0.</summary>
        private static GeoPolygon3 Plate(double x, double y, double size) => new GeoPolygon3(
            new GeoPoint3(x, y, 0), new GeoPoint3(x + size, y, 0),
            new GeoPoint3(x + size, y + size, 0), new GeoPoint3(x, y + size, 0));

        /// <summary>The same square standing in a tilted plane, so nothing rests on z = 0 being special.</summary>
        private static GeoPolygon3 Tilted(double along, double size) => new GeoPolygon3(
            new GeoPoint3(along, along, 0), new GeoPoint3(along + size, along + size, 0),
            new GeoPoint3(along + size, along + size, size), new GeoPoint3(along, along, size));

        [Fact]
        public void TwoPlatesInOnePlaneCanBeJoinedAndCutFromEachOther()
        {
            GeoPolygon3 first = Plate(0, 0, 100);
            GeoPolygon3 overlapping = Plate(50, 50, 100);

            GeoFace3[] joined = first.Union(overlapping);

            Assert.Single(joined);
            Assert.Equal(100 * 100 + 100 * 100 - 50 * 50, joined[0].Area, 6);

            Assert.Equal(50 * 50, first.Intersect(overlapping)[0].Area, 6);
            Assert.Equal(100 * 100 - 50 * 50, first.Subtract(overlapping)[0].Area, 6);
            Assert.Equal(2 * (100 * 100 - 50 * 50), first.Xor(overlapping).Sum(face => face.Area), 6);

            // Everything comes back in the plane the two share.
            foreach (GeoFace3 face in joined.Concat(first.Xor(overlapping)))
            {
                Assert.All(face.Boundary.Vertices, vertex => Assert.Equal(0.0, vertex.Z, 6));
            }
        }

        [Fact]
        public void JoiningTwoPlatesCanLeaveAHoleWhichIsWhyTheAnswerIsAFace()
        {
            // A ring built out of four bars: the hole in the middle belongs to no bar.
            GeoPolygon3 bottom = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(300, 50, 0), new GeoPoint3(0, 50, 0));
            GeoPolygon3 top = bottom.Translate(new GeoVector3(0, 250, 0));
            GeoPolygon3 left = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0), new GeoPoint3(50, 300, 0), new GeoPoint3(0, 300, 0));
            GeoPolygon3 right = left.Translate(new GeoVector3(250, 0, 0));

            // Each bar has to touch what is there already, or the running union is two pieces and carrying one
            // of them forward quietly drops the other. Bottom, left, top, right touches all the way round.
            GeoFace3[] ring = bottom.Union(left);

            Assert.Single(ring);

            ring = ring[0].Union(top);

            Assert.Single(ring);

            ring = ring[0].Union(right);

            Assert.Single(ring);
            Assert.Single(ring[0].Holes);
            Assert.Equal(200 * 200, ring[0].Holes[0].Area, 6);
            Assert.Equal(300 * 300 - 200 * 200, ring[0].Area, 6);

            // Two bars that touch nowhere stay two faces, which is why the order above matters.
            Assert.Equal(2, bottom.Union(top).Length);
        }

        [Fact]
        public void APlateInAnotherPlaneIsRefusedAndNotQuietlyProjected()
        {
            GeoPolygon3 flat = Plate(0, 0, 100);
            GeoPolygon3 above = flat.Translate(new GeoVector3(0, 0, 100));

            Assert.True(flat.SharesPlaneWith(flat.GetPlane()));
            Assert.False(flat.SharesPlaneWith(above.GetPlane(), Loose));

            Assert.Throws<ArgumentException>(() => flat.Union(above));
            Assert.Throws<ArgumentException>(() => flat.Intersect(above));
            Assert.Throws<ArgumentException>(() => flat.Subtract(above));
            Assert.Throws<ArgumentException>(() => flat.Xor(above));

            // A face and a curved loop out of the plane are refused the same way.
            Assert.Throws<ArgumentException>(() => flat.Union(new GeoFace3(above)));
            Assert.Throws<ArgumentException>(() => new GeoFace3(flat).Union(new GeoFace3(above)));
            Assert.Throws<ArgumentException>(() => new GeoFace3(flat).Union(above));
            Assert.Throws<ArgumentException>(() => flat.Union(new GeoPolygonArc3(above)));

            // Standing upright shares no plane with lying flat either.
            var upright = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 0, 100), new GeoPoint3(0, 0, 100));

            Assert.False(flat.SharesPlaneWith(upright.GetPlane(), Loose));
            Assert.Throws<ArgumentException>(() => flat.Union(upright));

            Assert.Throws<ArgumentNullException>(() => flat.Union((GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => flat.Union((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => new GeoFace3(flat).Subtract((GeoPolygon3)null));
        }

        [Fact]
        public void AShapeWoundTheOtherWayIsStillTheSameArea()
        {
            GeoPolygon3 first = Plate(0, 0, 100);

            // The same overlapping square with its vertices the other way round, so its normal points the
            // other way. It has to read as an area, not as a hole.
            var reversed = new GeoPolygon3(
                new GeoPoint3(50, 150, 0), new GeoPoint3(150, 150, 0), new GeoPoint3(150, 50, 0), new GeoPoint3(50, 50, 0));

            Assert.True(reversed.Normal.IsEqualTo(first.Normal.Negate(), Loose));
            Assert.True(first.SharesPlaneWith(reversed.GetPlane(), Loose));

            Assert.Equal(50 * 50, first.Intersect(reversed)[0].Area, 6);
            Assert.Equal(100 * 100 - 50 * 50, first.Subtract(reversed)[0].Area, 6);
        }

        [Fact]
        public void AFaceKeepsItsHolesThroughACombination()
        {
            var plate = new GeoFace3(
                Plate(0, 0, 300),
                new[] { Plate(200, 200, 50) });

            Assert.Equal(300 * 300 - 50 * 50, plate.Area, 6);

            // Joining a patch that misses the hole leaves the hole where it was.
            GeoFace3[] joined = plate.Union(Plate(300, 0, 100));

            Assert.Single(joined);
            Assert.Single(joined[0].Holes);
            Assert.Equal(plate.Area + 100 * 100, joined[0].Area, 6);

            // Joining a patch that fills the hole closes it.
            GeoFace3[] filled = plate.Union(Plate(200, 200, 50));

            Assert.Single(filled);
            Assert.Empty(filled[0].Holes);
            Assert.Equal(300 * 300, filled[0].Area, 6);
        }

        [Fact]
        public void ATiltedPlaneWorksTheSameAndTheAnswerStaysInIt()
        {
            GeoPolygon3 first = Tilted(0, 100);
            GeoPolygon3 overlapping = first.Translate(new GeoVector3(0, 0, 50));

            Assert.True(first.SharesPlaneWith(overlapping.GetPlane(), Loose));

            GeoFace3[] joined = first.Union(overlapping);
            GeoPlane3 plane = first.GetPlane();

            // The rectangle is a hundred tall and a hundred root two along the tilt, so that is the width every
            // area below is measured in.
            double width = 100.0 * Math.Sqrt(2.0);

            Assert.Equal(width * 100.0, first.Area, 6);

            Assert.Single(joined);
            Assert.All(joined[0].Boundary.Vertices, vertex => Assert.True(plane.IsPointOn(vertex, Loose)));
            Assert.Equal(width * 150.0, joined[0].Area, 6);

            Assert.Equal(width * 50.0, first.Intersect(overlapping)[0].Area, 6);
        }

        [Fact]
        public void TheLiftGivesTheSameAnswerAsThePlaneOnTheSameShapes()
        {
            GeoPolygon3 first = Plate(0, 0, 100);
            GeoPolygon3 second = Plate(50, 50, 100);

            var flatFirst = new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));
            var flatSecond = new GeoPolygon2(
                new GeoPoint2(50, 50), new GeoPoint2(150, 50), new GeoPoint2(150, 150), new GeoPoint2(50, 150));

            Assert.Equal(Boolean2.Union(flatFirst, flatSecond).Length, first.Union(second).Length);
            Assert.Equal(Boolean2.Union(flatFirst, flatSecond).Sum(f => f.Area), first.Union(second).Sum(f => f.Area), 6);
            Assert.Equal(Boolean2.Xor(flatFirst, flatSecond).Length, first.Xor(second).Length);
            Assert.Equal(Boolean2.Xor(flatFirst, flatSecond).Sum(f => f.Area), first.Xor(second).Sum(f => f.Area), 6);
            Assert.Equal(Boolean2.Subtract(flatFirst, flatSecond).Sum(f => f.Area), first.Subtract(second).Sum(f => f.Area), 6);
        }

        [Fact]
        public void APolygonAndACurvedLoopInOnePlaneMeetFromEitherSide()
        {
            GeoPolygon3 plate = Plate(0, 0, 100);
            var loop = new GeoPolygonArc3(Plate(50, 50, 100));

            // The loop already answered about a polygon; now the polygon answers about the loop, and the two
            // readings agree.
            Assert.Equal(loop.Intersect(plate)[0].Area, plate.Intersect(loop)[0].Area, 6);
            Assert.Equal(loop.Union(plate).Sum(f => f.Area), plate.Union(loop).Sum(f => f.Area), 6);

            // Subtract is the one that is not symmetric, so the two directions differ and both are right.
            Assert.Equal(100 * 100 - 50 * 50, plate.Subtract(loop)[0].Area, 6);
            Assert.Equal(100 * 100 - 50 * 50, loop.Subtract(plate)[0].Area, 6);
            Assert.Equal(2 * (100 * 100 - 50 * 50), plate.Xor(loop).Sum(f => f.Area), 6);
        }

        [Fact]
        public void TwoBoxesCanBeJoinedThroughTheBodiesTheyBound()
        {
            var first = new GeoObb3(new GeoPoint3(0, 0, 0), 100, 100, 100);
            var second = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 100, 100);

            Assert.True(first.TryUnion(second, out GeoSolid3 joined));
            Assert.Equal(150.0 * 100.0 * 100.0, joined.Volume, 3);

            Assert.True(first.TryIntersect(second, out GeoSolid3 shared));
            Assert.Equal(50.0 * 100.0 * 100.0, shared.Volume, 3);

            Assert.True(first.TrySubtract(second, out GeoSolid3 left));
            Assert.Equal(50.0 * 100.0 * 100.0, left.Volume, 3);

            // A turned box is exact too: six flat faces, nothing fitted.
            var turned = new GeoObb3(new GeoPoint3(0, 0, 0), 100, 100, 100,
                new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0));

            Assert.True(turned.TryIntersect(turned, out GeoSolid3 itself));
            Assert.Equal(turned.Volume, itself.Volume, 3);
        }

        [Fact]
        public void ABoxAndABodyMeetFromEitherSide()
        {
            var box = new GeoObb3(new GeoPoint3(0, 0, 0), 100, 100, 100);
            GeoSolid3 body = new GeoObb3(new GeoPoint3(50, 0, 0), 100, 100, 100).ToSolid();

            Assert.True(box.TryUnion(body, out GeoSolid3 fromTheBox));
            Assert.True(body.TryUnion(box, out GeoSolid3 fromTheBody));
            Assert.Equal(fromTheBox.Volume, fromTheBody.Volume, 3);

            // Subtract is not symmetric, and each way round takes the other one away.
            Assert.True(box.TrySubtract(body, out GeoSolid3 boxLeft));
            Assert.True(body.TrySubtract(box, out GeoSolid3 bodyLeft));
            Assert.Equal(50.0 * 100.0 * 100.0, boxLeft.Volume, 3);
            Assert.Equal(50.0 * 100.0 * 100.0, bodyLeft.Volume, 3);

            Assert.Throws<ArgumentNullException>(() => box.TryUnion((GeoObb3)null, out _));
            Assert.Throws<ArgumentNullException>(() => box.TrySubtract((GeoObb3)null, out _));
            Assert.Throws<ArgumentNullException>(() => body.TryUnion((GeoObb3)null, out _));
        }

        [Fact]
        public void TwoBoxesThatShareNothingHaveNothingToKeep()
        {
            var first = new GeoObb3(new GeoPoint3(0, 0, 0), 100, 100, 100);
            var apart = new GeoObb3(new GeoPoint3(5000, 0, 0), 100, 100, 100);

            Assert.False(first.TryIntersect(apart, out _));

            // Taking away a body that touches nothing leaves the box whole.
            Assert.True(first.TrySubtract(apart, out GeoSolid3 whole));
            Assert.Equal(first.Volume, whole.Volume, 3);
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndNothingIsAskedOfNothing()
        {
            Tolerance global = Tolerance.Global;
            GeoPolygon3 first = Plate(0, 0, 100);
            GeoPolygon3 second = Plate(50, 50, 100);
            var face = new GeoFace3(first);
            var loop = new GeoPolygonArc3(second);
            var box = new GeoObb3(new GeoPoint3(0, 0, 0), 100, 100, 100);

            Assert.Equal(first.Union(second).Length, first.Union(second, global).Length);
            Assert.Equal(first.Intersect(second)[0].Area, first.Intersect(second, global)[0].Area, 9);
            Assert.Equal(face.Union(second).Length, face.Union(second, global).Length);
            Assert.Equal(face.Xor(new GeoFace3(second)).Length, face.Xor(new GeoFace3(second), global).Length);
            Assert.Equal(first.Subtract(loop)[0].Area, first.Subtract(loop, global)[0].Area, 9);

            Assert.Equal(first.SharesPlaneWith(second.GetPlane()), first.SharesPlaneWith(second.GetPlane(), global));
            Assert.Equal(face.SharesPlaneWith(second.GetPlane()), face.SharesPlaneWith(second.GetPlane(), global));
            Assert.Equal(loop.SharesPlaneWith(second.GetPlane()), loop.SharesPlaneWith(second.GetPlane(), global));

            Assert.Equal(
                box.TryUnion(box, out GeoSolid3 a),
                box.TryUnion(box, out GeoSolid3 b, global));
            Assert.Equal(a.Volume, b.Volume, 3);

            Assert.Throws<ArgumentNullException>(() => Boolean3.Union((GeoPolygon3)null, first));
            Assert.Throws<ArgumentNullException>(() => Boolean3.Union(first, (GeoPolygon3)null));
            Assert.Throws<ArgumentNullException>(() => Boolean3.Subtract((GeoFace3)null, face));
            Assert.Throws<ArgumentNullException>(() => Boolean3.Xor(face, (GeoFace3)null));
        }
    }
}
