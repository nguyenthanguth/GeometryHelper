using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// What every mesh of a flat shape of space promises, held against the shape it was made of.
    /// </summary>
    internal static class Mesh3Assert
    {
        /// <summary>
        /// Holds a mesh of space to its promises: the mesh laid out in its frame keeps every promise of the plane against the
        /// shape laid out there; its vertices stand where the layout says, on the shape's plane; its faces run
        /// counter-clockwise about the normal and cover the area expected; and the shape's corners are vertices exactly.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="material">Where a point of space, on the shape's plane, stands against the shape.</param>
        /// <param name="corners">The shape's own corners, each of which a vertex has to be exactly, or none to check.</param>
        /// <param name="area">The area the faces should cover.</param>
        /// <param name="tolerance">The tolerance the mesh was made within.</param>
        /// <param name="covers">Whether every point of the material lies in a face: false for a grid with joints.</param>
        public static void IsSound(GeoMesh3 mesh, Func<GeoPoint3, PointLocation> material, IEnumerable<GeoPoint3> corners, double area, Tolerance tolerance, bool covers = true)
        {
            GeoCoordinateSystem3 frame = mesh.Frame;
            Assert.True(frame.IsValid, $"the frame is no frame: {frame}");

            GeoMesh2 flat = mesh.ToMesh2();
            Assert.Equal(mesh.VertexCount, flat.VertexCount);
            Assert.Equal(mesh.FaceCount, flat.FaceCount);
            Assert.Equal(mesh.Kind, flat.Kind);

            double size = mesh.Vertices.Max(v => frame.ToLocal(v).DistanceTo(new GeoPoint3(0, 0, 0)));

            for (int v = 0; v < mesh.VertexCount; v++)
            {
                GeoPoint3 local = frame.ToLocal(mesh.Vertices[v]);
                Assert.True(Math.Abs(local.X - flat.Vertices[v].X) <= 1E-9 * Math.Max(1.0, size) && Math.Abs(local.Y - flat.Vertices[v].Y) <= 1E-9 * Math.Max(1.0, size), $"vertex {v} stands at {local}, laid out at {flat.Vertices[v]}");
                Assert.True(Math.Abs(local.Z) <= 2.0 * tolerance.EqualPlanar + 1E-9 * Math.Max(1.0, size), $"vertex {v} stands {local.Z} off the plane");
            }

            foreach (GeoPoint3 corner in corners ?? Enumerable.Empty<GeoPoint3>())
            {
                Assert.True(mesh.Vertices.Contains(corner), $"the corner {corner} is not a vertex exactly");
            }

            double sum = 0.0;

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                GeoPolygon3 face = mesh.GetFace(f);
                Assert.Equal(mesh.GetFaceIndices(f).Length, face.VertexCount);
                Assert.True(face.Area > 0.0, $"face {f} has no area");
                Assert.True(face.Normal.DotProduct(mesh.Normal) > 0.999, $"face {f} runs {face.Normal}, not about {mesh.Normal}");
                sum += face.Area;
            }

            Assert.True(Math.Abs(sum - mesh.Area) <= 1E-9 * Math.Max(1.0, sum), $"Area {mesh.Area} is not the faces' {sum}");
            Assert.True(Math.Abs(area - mesh.Area) <= 1E-9 * Math.Max(1.0, area), $"the faces cover {mesh.Area:R}, not {area:R}");

            // The mesh laid out in its frame is a mesh of the plane, held to every promise of one against the shape laid out
            // there too.
            PointLocation Laid(GeoPoint2 p) => material(frame.ToGlobal(new GeoPoint3(p.X, p.Y, 0.0)));
            MeshAssert.IsSound(flat, Laid, flat.Area, tolerance, covers);

            // The triangles are the faces' and run about the normal too.
            GeoTriangle3[] triangles = mesh.ToTriangles();
            double triangled = triangles.Sum(t => t.Area);
            Assert.True(Math.Abs(triangled - mesh.Area) <= 1E-9 * Math.Max(1.0, mesh.Area), $"the triangles cover {triangled:R}, not {mesh.Area:R}");

            foreach (GeoTriangle3 triangle in triangles)
            {
                Assert.True(triangle.GetAreaVector().DotProduct(mesh.Normal) > 0.0, $"a triangle runs against the normal: {triangle}");
            }
        }

        /// <summary>
        /// Where a point of space stands against a face of space whose plane it lies on.
        /// </summary>
        public static Func<GeoPoint3, PointLocation> Locate(GeoFace3 face, Tolerance tolerance) => p => face.Locate(p, tolerance);

        /// <summary>
        /// Where a point of space stands against a polygon of space whose plane it lies on.
        /// </summary>
        public static Func<GeoPoint3, PointLocation> Locate(GeoPolygon3 polygon, Tolerance tolerance) => p => polygon.Locate(p, tolerance);
    }
}
