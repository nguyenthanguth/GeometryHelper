using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.SolidGeometry.Spatial;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the surface triangulation: every triangle must lie within the material of the face it came
    /// from, so that a mesh read as a surface describes the body and nothing more.
    /// </summary>
    public class SurfaceTriangulationTests
    {
        /// <summary>
        /// A U-shaped outline. The notch spans x in (2, 8) and y in (0, 8) and holds no material, which is
        /// what a fan triangulation from the first vertex spans over.
        /// </summary>
        private static GeoPoint3[] UShape() => new[]
        {
            new GeoPoint3(0, 0, 0), new GeoPoint3(2, 0, 0), new GeoPoint3(2, 8, 0), new GeoPoint3(8, 8, 0),
            new GeoPoint3(8, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0)
        };

        private static GeoSolid3 Extrude(GeoPoint3[] baseCcw, double height)
        {
            List<GeoFace3> faces = new List<GeoFace3>();
            GeoPoint3[] top = new GeoPoint3[baseCcw.Length];
            GeoPoint3[] bottomReversed = new GeoPoint3[baseCcw.Length];

            for (int i = 0; i < baseCcw.Length; i++)
            {
                top[i] = new GeoPoint3(baseCcw[i].X, baseCcw[i].Y, baseCcw[i].Z + height);
                bottomReversed[i] = baseCcw[baseCcw.Length - 1 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < baseCcw.Length; i++)
            {
                int j = (i + 1) % baseCcw.Length;
                faces.Add(new GeoFace3(new GeoPolygon3(baseCcw[i], baseCcw[j], top[j], top[i])));
            }

            return new GeoSolid3(faces);
        }

        [Fact]
        public void TriangulateSurface_OnConcaveFace_KeepsEveryTriangleInsideIt()
        {
            GeoFace3 face = new GeoFace3(new GeoPolygon3(UShape()));

            GeoTriangle3[] triangles = face.TriangulateSurface();

            Assert.NotEmpty(triangles);

            foreach (GeoTriangle3 triangle in triangles)
            {
                Assert.NotEqual(PointLocation.OutSide, face.Locate(triangle.Centroid));
            }
        }

        [Fact]
        public void TriangulateSurface_OnConcaveFace_CoversItExactly()
        {
            GeoFace3 face = new GeoFace3(new GeoPolygon3(UShape()));

            double meshed = 0.0;
            foreach (GeoTriangle3 triangle in face.TriangulateSurface())
            {
                meshed += triangle.Area;
            }

            Assert.Equal(52.0, face.Area, 9);
            Assert.Equal(face.Area, meshed, 9);
        }

        [Fact]
        public void TriangulateSurface_OnConcaveFace_SharesTheFaceNormal()
        {
            GeoFace3 face = new GeoFace3(new GeoPolygon3(UShape()));

            foreach (GeoTriangle3 triangle in face.TriangulateSurface())
            {
                Assert.True(triangle.Normal.IsCodirectionalTo(face.Normal));
            }
        }

        [Fact]
        public void TriangulateSurface_OnFaceWithHole_LeavesTheHoleOpen()
        {
            GeoPolygon3 boundary = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            // Wound against the boundary, as a hole is.
            GeoPolygon3 hole = new GeoPolygon3(
                new GeoPoint3(4, 4, 0), new GeoPoint3(4, 6, 0), new GeoPoint3(6, 6, 0), new GeoPoint3(6, 4, 0));

            GeoFace3 face = new GeoFace3(boundary, new[] { hole });

            GeoTriangle3[] triangles = face.TriangulateSurface();

            double meshed = 0.0;
            foreach (GeoTriangle3 triangle in triangles)
            {
                meshed += triangle.Area;

                // Nothing may be laid over the hole.
                Assert.False(IsInsideHole(triangle.Centroid));
            }

            Assert.Equal(96.0, face.Area, 9);
            Assert.Equal(face.Area, meshed, 9);
        }

        private static bool IsInsideHole(GeoPoint3 point)
        {
            return point.X > 4.0 && point.X < 6.0 && point.Y > 4.0 && point.Y < 6.0;
        }

        [Fact]
        public void TriangulateSurface_OnConvexFace_StillCoversIt()
        {
            GeoFace3 face = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(4, 0, 0), new GeoPoint3(4, 3, 0), new GeoPoint3(0, 3, 0)));

            GeoTriangle3[] triangles = face.TriangulateSurface();

            double meshed = 0.0;
            foreach (GeoTriangle3 triangle in triangles)
            {
                meshed += triangle.Area;
            }

            Assert.Equal(2, triangles.Length);
            Assert.Equal(12.0, meshed, 9);
        }

        [Fact]
        public void TriangulateSurface_OnATiltedFace_CoversItExactly()
        {
            // The same U-shape carried onto a plane that is square to none of the world axes, so that the
            // projection into the plane of the face is what is being exercised.
            GeoTransform3 tilt = GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7);

            GeoPoint3[] shape = UShape();
            for (int i = 0; i < shape.Length; i++)
            {
                shape[i] = shape[i].TransformBy(tilt);
            }

            GeoFace3 face = new GeoFace3(new GeoPolygon3(shape));

            double meshed = 0.0;
            foreach (GeoTriangle3 triangle in face.TriangulateSurface())
            {
                meshed += triangle.Area;
            }

            Assert.Equal(52.0, face.Area, 6);
            Assert.Equal(face.Area, meshed, 6);
        }

        [Fact]
        public void Bvh_OnAConcaveSolid_MeasuresToTheRealSurface()
        {
            GeoSolid3 body = Extrude(UShape(), 1.0);
            GeoBvh3 bvh = GeoBvh3.FromSolid(body);

            GeoPoint3 inNotch = new GeoPoint3(5, 2, 0.5);

            // The nearest material is the wall at x = 2 or x = 8, three units away. A fan triangulation
            // spans the notch at z = 0 and z = 1 and answers 0.5.
            Assert.Equal(3.0, bvh.DistanceTo(inNotch), 9);
            Assert.Equal(3.0, Distance3.DistanceTo(body, inNotch), 9);
        }

        [Fact]
        public void Bvh_OnAConcaveSolid_LetsARayThroughTheNotch()
        {
            GeoSolid3 body = Extrude(UShape(), 1.0);
            GeoBvh3 bvh = GeoBvh3.FromSolid(body);

            GeoRay3 upThroughTheNotch = new GeoRay3(new GeoPoint3(5, 2, -5), GeoVector3.ZAxis);

            Assert.Empty(bvh.GetIntersections(upThroughTheNotch));
        }

        [Fact]
        public void Bvh_OnAConcaveSolid_StillCatchesARayThroughMaterial()
        {
            GeoSolid3 body = Extrude(UShape(), 1.0);
            GeoBvh3 bvh = GeoBvh3.FromSolid(body);

            GeoRay3 upThroughTheWall = new GeoRay3(new GeoPoint3(1, 2, -5), GeoVector3.ZAxis);

            Assert.Equal(2, bvh.GetIntersections(upThroughTheWall).Length);
        }

        [Fact]
        public void Collision_OnAConcaveSolid_IgnoresABodyInTheNotch()
        {
            GeoSolid3 body = Extrude(UShape(), 1.0);

            // Sits wholly in the empty notch and crosses the z = 0 plane, which is where a fan triangle
            // spans the opening.
            GeoSolid3 probe = Extrude(new[]
            {
                new GeoPoint3(4, 1, -0.5), new GeoPoint3(6, 1, -0.5),
                new GeoPoint3(6, 3, -0.5), new GeoPoint3(4, 3, -0.5)
            }, 1.0);

            Assert.False(Collision3.CollidesWith(body, probe));
        }

        [Fact]
        public void Collision_OnAConcaveSolid_StillCatchesABodyInTheMaterial()
        {
            GeoSolid3 body = Extrude(UShape(), 1.0);

            GeoSolid3 probe = Extrude(new[]
            {
                new GeoPoint3(0.5, 1, -0.5), new GeoPoint3(1.5, 1, -0.5),
                new GeoPoint3(1.5, 3, -0.5), new GeoPoint3(0.5, 3, -0.5)
            }, 1.0);

            Assert.True(Collision3.CollidesWith(body, probe));
        }

        [Fact]
        public void Triangulate_OnAConcaveSolid_MeshesTheWholeSurface()
        {
            GeoSolid3 body = Extrude(UShape(), 1.0);

            double meshed = 0.0;
            foreach (GeoTriangle3 triangle in body.Triangulate())
            {
                meshed += triangle.Area;
            }

            Assert.Equal(body.SurfaceArea, meshed, 9);
        }

        [Fact]
        public void SignedSums_AreUnaffected_BecauseTheyStillFan()
        {
            GeoSolid3 body = Extrude(UShape(), 1.0);

            Assert.Equal(52.0, body.Volume, 9);
            Assert.True(body.IsClosed());

            // The fan is still what the signed sums are built on, and it is still reachable.
            GeoFace3 face = new GeoFace3(new GeoPolygon3(UShape()));
            Assert.Equal(UShape().Length - 2, face.Triangulate().Length);
        }
    }
}
