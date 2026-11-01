using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face or a body transformed reads the transformation once and hands it to every polygon, which comes out as the
    /// polygon transformed alone; a polygon or a face of the plane laid into a frame keeps its corners side for side.
    /// </summary>
    /// <remarks>
    /// A polygon or a face of the plane was laid into space through the polygon's constructor at the global tolerance, so
    /// one shrunk in the plane to sides of a few ten-thousandths came back with its cut corners merged away, where the same
    /// polygon laid in at full size and shrunk in space kept them. No test sets the global tolerance.
    /// </remarks>
    public class SpaceCarryingG1wxTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static readonly GeoTransform3 Shear = new GeoTransform3(new double[,] { { 1, 0.3, 0, 5 }, { 0, 1, 0.2, -3 }, { 0, 0, 1.5, 2 }, { 0, 0, 0, 1 } });

        // A frame leaning every way, far from the origin.
        private static readonly GeoCoordinateSystem3 Leaning = new GeoCoordinateSystem3(new GeoPlane3(P(312.5, -87.25, 40.125), new GeoVector3(0.3, -0.5, 0.8)));

        // The triangle (0, 0), (10, 0), (10, 10) with every corner cut by 0.3.
        private static GeoPolygon2 CutTriangle()
        {
            double c = 0.3 * Math.Sqrt(0.5);

            return new GeoPolygon2(P(0.3, 0), P(9.7, 0), P(10, 0.3), P(10, 9.7), P(10 - c, 10 - c), P(c, c));
        }

        private static void SameBits(GeoPolygon3 expected, GeoPolygon3 actual)
        {
            Assert.Equal(expected.VertexCount, actual.VertexCount);

            for (int i = 0; i < expected.VertexCount; i++)
            {
                Assert.Equal(expected.Vertices[i].X, actual.Vertices[i].X);
                Assert.Equal(expected.Vertices[i].Y, actual.Vertices[i].Y);
                Assert.Equal(expected.Vertices[i].Z, actual.Vertices[i].Z);
            }

            Assert.Equal(expected.Normal.X, actual.Normal.X);
            Assert.Equal(expected.Normal.Y, actual.Normal.Y);
            Assert.Equal(expected.Normal.Z, actual.Normal.Z);
            Assert.Equal(expected.Area, actual.Area);
        }

        [Fact]
        public void AFaceWithAHoleShearedRotatedOrScaled_IsItsBoundaryAndHoleTransformedOneByOneToTheBit()
        {
            var boundary = new GeoPolygon3(new[] { P(0, 0, 0), P(100, 0, 0), P(100, 70, 0.0004), P(0, 70, 0) }, Tolerance.Default);
            var hole = new GeoPolygon3(new[] { P(20, 20, 0), P(40, 20, 0), P(40, 30, 0), P(20, 30, 0) }, Tolerance.Default);
            var face = new GeoFace3(boundary, new[] { hole }, Tolerance.Default);

            var transforms = new[]
            {
                Shear,
                GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7),
                GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7) * GeoTransform3.Scaling(10),
            };

            foreach (GeoTransform3 transform in transforms)
            {
                GeoFace3 moved = face.TransformBy(transform);

                SameBits(boundary.TransformBy(transform), moved.Boundary);
                SameBits(hole.TransformBy(transform), moved.Holes[0]);
            }
        }

        [Fact]
        public void ABoxShearedAndStretched_HasEveryFaceAsThatFaceTransformedAlone()
        {
            GeoSolid3 box = new GeoObb3(P(50, 35, 20), 100, 70, 40).ToSolid();

            GeoSolid3 sheared = box.TransformBy(Shear);

            Assert.Equal(box.Faces.Count, sheared.Faces.Count);

            for (int i = 0; i < box.Faces.Count; i++)
            {
                SameBits(box.Faces[i].TransformBy(Shear).Boundary, sheared.Faces[i].Boundary);
            }

            Assert.Equal(100 * 70 * 40 * 1.5, sheared.GetVolume(Tolerance.Default), 6);
        }

        [Fact]
        public void ATriangleWithItsCornersCutByThreeTenThousandthsInThePlane_IsLaidIntoAFrameWithItsSixCorners()
        {
            // Cut at 0.3 and shrunk by a thousandth in the plane, side for side: cuts 0.0003 long, the triangle 0.01 across.
            GeoPolygon2 shrunk = CutTriangle().TransformBy(GeoTransform2.Scaling(0.001));
            Assert.Equal(6, shrunk.VertexCount);

            GeoPolygon3 laid = shrunk.ToPolygon3(Leaning);

            Assert.Equal(6, laid.VertexCount);
            Assert.Equal(Math.Abs(shrunk.SignedArea), laid.Area, 15);

            // As the triangle laid in at full size and shrunk in space.
            GeoPolygon3 reference = CutTriangle().ToPolygon3(Leaning).TransformBy(GeoTransform3.Scaling(Leaning.Origin, 0.001));
            Assert.Equal(reference.VertexCount, laid.VertexCount);
        }

        [Fact]
        public void AFaceWithACutCornerHoleShrunkInThePlane_IsLaidIntoAFrameWithEveryCornerOfItsHole()
        {
            var boundary = new GeoPolygon2(P(0, 0), P(10, 0), P(10, 10), P(0, 10));
            var hole = new GeoPolygon2(P(2.3, 2), P(4, 2), P(4, 4), P(2, 4), P(2, 2.3));
            GeoFace2 shrunk = new GeoFace2(boundary, new[] { hole }).TransformBy(GeoTransform2.Scaling(0.001));
            Assert.Equal(5, shrunk.Holes[0].VertexCount);

            GeoFace3 laid = shrunk.ToFace3(Leaning);

            Assert.Equal(4, laid.Boundary.VertexCount);
            Assert.Single(laid.Holes);
            Assert.Equal(5, laid.Holes[0].VertexCount);
            Assert.Equal(shrunk.Area, laid.Area, 15);
        }

        [Fact]
        public void APolygonOfThePlaneThatEnclosesNoArea_IsStillRefusedWhenLaidIntoAFrame()
        {
            var flat = new GeoPolygon2(new List<GeoPoint2> { P(0, 0), P(10, 0), P(20, 0) });

            Assert.Throws<ArgumentException>(() => flat.ToPolygon3(Leaning));
        }
    }
}
