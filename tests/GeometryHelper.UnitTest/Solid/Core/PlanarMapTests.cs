using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Carrying flat geometry between the plane and space: that a shape laid out in a frame and put back
    /// comes home unchanged, that its measurements survive the trip, and that a point off the plane is
    /// refused rather than quietly flattened.
    /// </summary>
    public class PlanarMapTests
    {
        private static GeoCoordinateSystem3 TiltedFrame(int seed)
        {
            var rng = new Random(seed);

            var plane = new GeoPlane3(
                new GeoPoint3(rng.NextDouble() * 200 - 100, rng.NextDouble() * 200 - 100, rng.NextDouble() * 200 - 100),
                new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5 + 0.01).Normalize());

            return PlanarMap.FrameOf(plane);
        }

        private static GeoPolygon3 TiltedPlate(GeoCoordinateSystem3 frame, params double[] xy)
        {
            return new GeoPolygon3(Enumerable.Range(0, xy.Length / 2)
                .Select(i => PlanarMap.ToPoint3(frame, new GeoPoint2(xy[2 * i], xy[2 * i + 1]))));
        }

        [Fact]
        public void APointGoesOutAndComesBackWhereItStarted()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                GeoCoordinateSystem3 frame = TiltedFrame(seed);
                var rng = new Random(seed + 1000);

                var flat = new GeoPoint2(rng.NextDouble() * 100 - 50, rng.NextDouble() * 100 - 50);
                GeoPoint3 inSpace = flat.ToPoint3(frame);

                Assert.True(inSpace.ProjectToPoint2(frame).IsEqualTo(flat, new Tolerance(1E-9, 1E-9)));
            }
        }

        [Fact]
        public void APlateFlattensToItsPlanViewAndLiftsBackUnchanged()
        {
            GeoCoordinateSystem3 frame = TiltedFrame(7);
            GeoPolygon3 plate = TiltedPlate(frame, 0, 0, 40, 0, 40, 25, 0, 25);

            GeoPolygon2 flat = plate.ProjectToPolygon2(frame);

            // The plan view is the rectangle it was built from, and the area survives the tilt.
            Assert.Equal(4, flat.VertexCount);
            Assert.Equal(40.0 * 25.0, flat.Area, 9);
            Assert.Equal(plate.Area, flat.Area, 9);

            GeoPolygon3 back = flat.ToPolygon3(frame);

            for (int i = 0; i < plate.VertexCount; i++)
            {
                Assert.True(back.Vertices[i].IsEqualTo(plate.Vertices[i], new Tolerance(1E-9, 1E-9)));
            }
        }

        [Fact]
        public void AFrameBuiltFromAShapeTurnsWithIt()
        {
            GeoCoordinateSystem3 first = TiltedFrame(11);
            GeoPolygon3 plate = TiltedPlate(first, 0, 0, 30, 0, 30, 12, 0, 12);

            // Built from the plate itself, the frame does not depend on the plate's place in the model: the
            // plan view of a plate and of the same plate moved and turned are the same drawing.
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(120, -40, 15))
                .Multiply(GeoTransform3.RotationAxis(new GeoPoint3(5, -3, 2), GeoVector3.XAxis, 0.7));

            GeoPolygon2 here = plate.ProjectToPolygon2(plate.GetFrame());
            GeoPolygon2 there = plate.TransformBy(motion).ProjectToPolygon2(plate.TransformBy(motion).GetFrame());

            for (int i = 0; i < here.VertexCount; i++)
            {
                Assert.True(here[i].IsEqualTo(there[i], new Tolerance(1E-8, 1E-8)));
            }
        }

        [Fact]
        public void AFaceKeepsItsHolesBothWays()
        {
            GeoCoordinateSystem3 frame = TiltedFrame(3);
            var face = new GeoFace3(
                TiltedPlate(frame, 0, 0, 20, 0, 20, 20, 0, 20),
                new[] { TiltedPlate(frame, 5, 5, 9, 5, 9, 9, 5, 9) });

            GeoFace2 flat = face.ProjectToFace2(frame);

            Assert.Single(flat.Holes);
            Assert.Equal(20.0 * 20.0 - 4.0 * 4.0, flat.Area, 9);

            GeoFace3 back = flat.ToFace3(frame);

            Assert.Single(back.Holes);
            Assert.Equal(face.Area, back.Area, 9);
        }

        [Fact]
        public void APointOffThePlaneIsRefusedRatherThanFlattened()
        {
            // Named axes, so the local coordinates of a point are its own X and Y: a frame built from a
            // plane alone takes whichever first axis the plane hands out.
            var frame = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis);
            var tolerance = new Tolerance(1E-4, 1E-4);

            // On the plane within EqualPlanar: taken.
            Assert.True(new GeoPoint3(3, 4, 0.00005).TryToPoint2(frame, out GeoPoint2 on, tolerance));
            Assert.True(on.IsEqualTo(new GeoPoint2(3, 4), new Tolerance(1E-9, 1E-9)));

            // Clear of it: refused, and the projection is what to call instead when that is what you meant.
            Assert.False(new GeoPoint3(3, 4, 5).TryToPoint2(frame, out _, tolerance));
            Assert.True(new GeoPoint3(3, 4, 5).ProjectToPoint2(frame).IsEqualTo(new GeoPoint2(3, 4), new Tolerance(1E-9, 1E-9)));
        }

        [Fact]
        public void AFrameOfAPlaneOpposingTheShapeTurnsTheWindingRound()
        {
            var plate = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 6, 0), new GeoPoint3(0, 6, 0));

            // Its own frame agrees with its normal, so the plan view runs the way the polygon does.
            GeoPolygon2 agreeing = plate.ProjectToPolygon2(plate.GetFrame());

            // A frame whose Z axis opposes it mirrors the plan view, so the winding flips.
            GeoCoordinateSystem3 opposed = PlanarMap.FrameOf(new GeoPlane3(GeoPoint3.Origin, plate.Normal.Multiply(-1.0)));
            GeoPolygon2 opposing = plate.ProjectToPolygon2(opposed);

            Assert.Equal(Math.Abs(agreeing.SignedArea), Math.Abs(opposing.SignedArea), 9);
            Assert.NotEqual(agreeing.IsClockwise, opposing.IsClockwise);
        }

        [Fact]
        public void SegmentsChainsAndVectorsMakeTheTripToo()
        {
            GeoCoordinateSystem3 frame = TiltedFrame(21);

            var line = new GeoLine2(new GeoPoint2(1, 2), new GeoPoint2(9, 5));
            Assert.Equal(line.Length, line.ToLine3(frame).Length, 9);
            Assert.True(line.ToLine3(frame).ProjectToLine2(frame).StartPoint.IsEqualTo(line.StartPoint, new Tolerance(1E-9, 1E-9)));

            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(4, 0), new GeoPoint2(4, 3));
            GeoPolyline3 lifted = chain.ToPolyline3(frame);
            Assert.Equal(3, lifted.VertexCount);
            Assert.Equal(chain.Length, lifted.Length, 9);

            var vector = new GeoVector2(3, 4);
            GeoVector3 inSpace = vector.ToVector3(frame);
            Assert.Equal(5.0, inSpace.Length, 9);
            Assert.True(inSpace.ProjectToVector2(frame).IsEqualTo(vector, new Tolerance(1E-9, 1E-9)));
        }

        [Fact]
        public void TheWholeReasonForIt_APlanarBooleanInSpace()
        {
            // Two coplanar plates in a tilted plane, unioned through the plane library and put back.
            GeoCoordinateSystem3 frame = TiltedFrame(5);
            GeoPolygon3 first = TiltedPlate(frame, 0, 0, 10, 0, 10, 10, 0, 10);
            GeoPolygon3 second = TiltedPlate(frame, 5, 5, 15, 5, 15, 15, 5, 15);

            GeoFace2[] joined = Boolean2.Union(
                first.ProjectToPolygon2(frame),
                second.ProjectToPolygon2(frame));

            GeoFace3 back = joined.Single().ToFace3(frame);

            Assert.Equal(175.0, back.Area, 9);
            Assert.True(back.Normal.IsParallelTo(first.Normal));
        }
    }
}
