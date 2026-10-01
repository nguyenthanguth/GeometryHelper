using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// What every mesh promises, held against the material it was made of.
    /// </summary>
    internal static class MeshAssert
    {
        /// <summary>A tolerance that reads rounding only, as the faces of a mesh are joined.</summary>
        private static readonly Tolerance Strict = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// Holds a mesh to its promises: faces simple, counter-clockwise and of three corners or more; the area expected;
        /// faces meeting edge to edge, an edge two faces share run the two ways round and no vertex standing on a side it is
        /// not an end of; and, at points spread over the shape, every point of the material in one face and every point
        /// outside it in none.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="material">Where a point stands against the material.</param>
        /// <param name="area">The area the faces should cover.</param>
        /// <param name="tolerance">The tolerance the mesh was made within.</param>
        /// <param name="covers">
        /// Whether every point of the material lies in a face: false for a grid with joints, whose joints no face covers.
        /// </param>
        public static void IsSound(GeoMesh2 mesh, Func<GeoPoint2, PointLocation> material, double area, Tolerance tolerance, bool covers = true)
        {
            Assert.True(mesh.FaceCount > 0, "the mesh has no faces");
            GeoPolygon2[] faces = mesh.GetFaces();
            double sum = 0.0;

            for (int i = 0; i < faces.Length; i++)
            {
                int[] indices = mesh.GetFaceIndices(i);
                Assert.True(indices.Length >= 3, $"face {i} has {indices.Length} corners");
                Assert.True(indices.Distinct().Count() == indices.Length, $"face {i} comes back to a corner: {string.Join(" ", indices)}");
                Assert.True(faces[i].SignedArea > 0.0, $"face {i} runs clockwise or has no area: {faces[i].SignedArea}");
                Assert.True(faces[i].IsSimple(Strict), $"face {i} crosses or touches itself: {Describe(faces[i])}");
                sum += faces[i].SignedArea;
            }

            Assert.True(Math.Abs(sum - mesh.Area) <= 1E-9 * Math.Max(1.0, Math.Abs(sum)), $"Area {mesh.Area} is not the faces' {sum}");
            Assert.True(Math.Abs(area - mesh.Area) <= 1E-9 * Math.Max(1.0, area), $"the faces cover {mesh.Area:R}, not {area:R}");

            EdgeToEdge(mesh, tolerance, covers ? material : null);
            Covers(mesh, faces, material, tolerance, covers);
        }

        /// <summary>
        /// Every edge one face has once and in one direction only, the vertices apart, and nothing across an edge no other
        /// face runs back along: a face there would meet this one along part of the edge only, at a corner standing on it.
        /// </summary>
        public static void EdgeToEdge(GeoMesh2 mesh, Tolerance tolerance) => EdgeToEdge(mesh, tolerance, null);

        /// <summary>
        /// <see cref="EdgeToEdge(GeoMesh2, Tolerance)"/>, and across an edge no other face runs back along, no material
        /// either, further than the tolerance, when the mesh is to cover all of it.
        /// </summary>
        public static void EdgeToEdge(GeoMesh2 mesh, Tolerance tolerance, Func<GeoPoint2, PointLocation> uncovered)
        {
            var directed = new HashSet<(int, int)>();
            IReadOnlyList<GeoPoint2> vertices = mesh.Vertices;

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                int[] face = mesh.GetFaceIndices(f);

                for (int i = 0; i < face.Length; i++)
                {
                    (int, int) edge = (face[i], face[(i + 1) % face.Length]);
                    Assert.True(directed.Add(edge), $"the edge {edge} is run the same way by two faces");
                }
            }

            // Points the same but for rounding are one vertex; points nearer than the tolerance may be two, the corners of
            // features that close.
            for (int a = 0; a < vertices.Count; a++)
            {
                for (int b = a + 1; b < vertices.Count; b++)
                {
                    Assert.False(vertices[a].IsEqualTo(vertices[b], Strict), $"vertices {a} and {b} stand on each other: {vertices[a]}");
                }
            }

            GeoPolygon2[] faces = mesh.GetFaces();

            foreach ((int a, int b) in directed)
            {
                if (directed.Contains((b, a)))
                {
                    continue;
                }

                // A hair to the right of the middle of the edge, outside the face that runs it.
                GeoPoint2 start = vertices[a];
                GeoPoint2 end = vertices[b];
                GeoVector2 along = start.GetVectorTo(end).Multiply(1.0 / start.DistanceTo(end));
                var across = new GeoPoint2(0.5 * (start.X + end.X) + 1E-5 * along.Y, 0.5 * (start.Y + end.Y) - 1E-5 * along.X);

                for (int f = 0; f < faces.Length; f++)
                {
                    Assert.False(Containment2.Locate(faces[f], across, Strict) == PointLocation.Inside, $"face {f} lies across the edge {a}-{b} ({start} to {end}) without running back along it");
                }

                if (uncovered != null)
                {
                    Assert.False(uncovered(across) == PointLocation.Inside, $"material lies across the edge {a}-{b} ({start} to {end}) and no face covers it");
                }
            }
        }

        /// <summary>
        /// At points spread over the shape: a point of the material in one face, or in at most one where joints leave some
        /// uncovered, and a point outside it in none.
        /// </summary>
        private static void Covers(GeoMesh2 mesh, GeoPolygon2[] faces, Func<GeoPoint2, PointLocation> material, Tolerance tolerance, bool covers)
        {
            double minX = mesh.Vertices.Min(p => p.X), maxX = mesh.Vertices.Max(p => p.X);
            double minY = mesh.Vertices.Min(p => p.Y), maxY = mesh.Vertices.Max(p => p.Y);
            double marginX = 0.1 * (maxX - minX), marginY = 0.1 * (maxY - minY);
            var random = new Random(7);
            int inside = 0;

            for (int k = 0; k < 1500; k++)
            {
                var point = new GeoPoint2(minX - marginX + random.NextDouble() * (maxX - minX + 2 * marginX), minY - marginY + random.NextDouble() * (maxY - minY + 2 * marginY));
                PointLocation where = material(point);

                if (where == PointLocation.OnSide)
                {
                    continue;
                }

                int count = 0;
                bool onSide = false;

                foreach (GeoPolygon2 face in faces)
                {
                    PointLocation inFace = Containment2.Locate(face, point, tolerance);
                    onSide |= inFace == PointLocation.OnSide;
                    count += inFace == PointLocation.Inside ? 1 : 0;
                }

                if (onSide)
                {
                    continue;
                }

                if (where == PointLocation.Inside)
                {
                    inside++;
                    Assert.True(covers ? count == 1 : count <= 1, $"{point} of the material lies in {count} faces");
                }
                else
                {
                    Assert.True(count == 0, $"{point} outside the material lies in {count} faces");
                }
            }

            Assert.True(inside > 50, $"only {inside} points fell in the material");
        }

        public static string Describe(GeoPolygon2 polygon) => string.Join(" ", polygon.Vertices.Select(p => $"({p.X:R}, {p.Y:R})"));

        /// <summary>
        /// The corners of a face that turn: those with three in a row dropped.
        /// </summary>
        public static List<GeoPoint2> Turns(GeoPolygon2 face, double within)
        {
            List<GeoPoint2> corners = face.Vertices.ToList();
            bool dropped = true;

            while (dropped && corners.Count > 3)
            {
                dropped = false;

                for (int i = 0; i < corners.Count; i++)
                {
                    GeoPoint2 previous = corners[(i + corners.Count - 1) % corners.Count];
                    GeoPoint2 next = corners[(i + 1) % corners.Count];

                    if (new GeoLine2(previous, next).DistanceTo(corners[i]) <= within)
                    {
                        corners.RemoveAt(i);
                        dropped = true;
                        break;
                    }
                }
            }

            return corners;
        }
    }
}
