using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Planar;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Breaks the shapes of the plane into triangles that each lie within their material, holes left open.
    /// <para>
    /// A face of the plane is laid on the plane z = 0 of space and meshed as <see cref="GeoFace3.TriangulateSurface(Tolerance)"/>
    /// meshes a face, then brought back down. Its corners go up and come back unchanged, since nothing but a zero is
    /// added to them, so the plane and space cannot come to mesh one shape two ways, as the plane's cuts by a line go
    /// through <see cref="Splition3"/> for the same reason. The triangles come back running counter-clockwise, whichever
    /// way the face did.
    /// </para>
    /// </summary>
    internal static class Triangulation2
    {
        /// <summary>
        /// Triangulates the material of a face, holes left open.
        /// </summary>
        /// <param name="face">The face to break up.</param>
        /// <param name="tolerance">The tolerance deciding what counts as no area at all.</param>
        /// <returns>The triangles, counter-clockwise; none when the face encloses no area within the tolerance.</returns>
        public static GeoTriangle2[] Triangulate(GeoFace2 face, Tolerance tolerance)
        {
            var triangles = new List<GeoTriangle2>();

            foreach (GeoFace3 lifted in Lift(face, tolerance))
            {
                foreach (GeoTriangle3 meshed in lifted.TriangulateSurface(tolerance))
                {
                    var triangle = new GeoTriangle2(Drop(meshed.A), Drop(meshed.B), Drop(meshed.C));
                    triangles.Add(triangle.IsClockwise ? triangle.Reverse() : triangle);
                }
            }

            return triangles.ToArray();
        }

        /// <summary>
        /// Lays the material of a face on the plane z = 0 of space, as the faces of space its rings make there: the face
        /// itself, or, when a ring crosses itself, the faces its region resolves into, or none when it encloses no area.
        /// </summary>
        /// <param name="face">The face to lay out.</param>
        /// <param name="tolerance">The tolerance deciding what counts as no area at all.</param>
        public static List<GeoFace3> Lift(GeoFace2 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (tolerance == null)
            {
                throw new ArgumentNullException(nameof(tolerance));
            }

            if (!TryLift(face.Boundary, tolerance, out GeoPolygon3 boundary))
            {
                return CrossesItself(face.Boundary, tolerance) ? LiftResolved(face, tolerance) : new List<GeoFace3>();
            }

            var holes = new List<GeoPolygon3>(face.Holes.Count);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                if (TryLift(hole, tolerance, out GeoPolygon3 lifted))
                {
                    holes.Add(lifted);
                }
                else if (CrossesItself(hole, tolerance))
                {
                    return LiftResolved(face, tolerance);
                }

                // Otherwise the hole encloses nothing, and takes nothing away.
            }

            return new List<GeoFace3> { new GeoFace3(boundary, holes, tolerance) };
        }

        /// <summary>
        /// Whether a loop the polygons of space refused crosses itself, rather than enclosing nothing.
        /// </summary>
        /// <remarks>
        /// A loop crossing itself into two lobes of equal area, wound against each other, sums to no area at all, and
        /// space refuses it as it refuses a loop of three corners in a row; but the region it covers, read under the
        /// even-odd rule as <see cref="GeoPolygon2.MakeValid()"/> reads it, is both lobes.
        /// </remarks>
        private static bool CrossesItself(GeoPolygon2 loop, Tolerance tolerance) => loop.VertexCount > 3 && !loop.IsSimple(tolerance);

        /// <summary>
        /// Lays out a face whose rings cross themselves by resolving its region first, as the booleans read it, into
        /// faces whose rings neither cross nor overlap.
        /// </summary>
        private static List<GeoFace3> LiftResolved(GeoFace2 face, Tolerance tolerance)
        {
            GeoPoint2 origin = face.Boundary[0];
            var points = new List<GeoPoint2>(face.Boundary.Vertices);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                points.AddRange(hole.Vertices);
            }

            int precision = ClipperRegion.GetPrecision(ClipperRegion.Extent(points, origin));
            List<LoopGroup> groups = ClipperRegion.Resolve(ClipperRegion.RegionOf(face, origin, precision, tolerance), Clipper2Lib.FillRule.Positive, precision, tolerance);
            var faces = new List<GeoFace3>();

            foreach (GeoFace2 piece in ClipperRegion.ToFaces(groups, origin, false))
            {
                if (!TryLift(piece.Boundary, tolerance, out GeoPolygon3 boundary))
                {
                    continue;
                }

                var holes = new List<GeoPolygon3>(piece.Holes.Count);

                foreach (GeoPolygon2 hole in piece.Holes)
                {
                    if (TryLift(hole, tolerance, out GeoPolygon3 lifted))
                    {
                        holes.Add(lifted);
                    }
                }

                faces.Add(new GeoFace3(boundary, holes, tolerance));
            }

            return faces;
        }

        /// <summary>
        /// Lays a loop of the plane on the plane z = 0, when it encloses an area the polygons of space accept.
        /// </summary>
        /// <remarks>
        /// <see cref="GeoPolygon3"/> refuses a loop with fewer than three corners apart, or enclosing no more than the
        /// vector tolerance, and the same is asked here first, so that a loop of no area is passed over rather than
        /// thrown at.
        /// </remarks>
        private static bool TryLift(GeoPolygon2 polygon, Tolerance tolerance, out GeoPolygon3 lifted)
        {
            lifted = null;

            var kept = new List<GeoPoint2>(polygon.VertexCount);

            foreach (GeoPoint2 vertex in polygon.Vertices)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].IsEqualTo(vertex, tolerance))
                {
                    kept.Add(vertex);
                }
            }

            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], tolerance))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            if (kept.Count < 3)
            {
                return false;
            }

            double twice = 0.0;

            for (int i = 0; i < kept.Count; i++)
            {
                GeoPoint2 a = kept[i];
                GeoPoint2 b = kept[(i + 1) % kept.Count];
                twice += (a.X - kept[0].X) * (b.Y - kept[0].Y) - (b.X - kept[0].X) * (a.Y - kept[0].Y);
            }

            if (!(Math.Abs(twice) * 0.5 > tolerance.EqualVector))
            {
                return false;
            }

            var corners = new GeoPoint3[kept.Count];

            for (int i = 0; i < kept.Count; i++)
            {
                corners[i] = new GeoPoint3(kept[i].X, kept[i].Y, 0.0);
            }

            lifted = new GeoPolygon3(corners, tolerance);
            return true;
        }

        public static GeoPoint2 Drop(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);

        /// <summary>
        /// Fans a loop that is known to be convex from a point inside it, keeping the triangles of any area.
        /// </summary>
        /// <param name="center">The point every triangle shares.</param>
        /// <param name="rim">The corners of the loop, in order.</param>
        /// <param name="tolerance">The tolerance deciding what counts as no area at all.</param>
        /// <returns>The triangles, counter-clockwise.</returns>
        public static GeoTriangle2[] Fan(GeoPoint2 center, IReadOnlyList<GeoPoint2> rim, Tolerance tolerance)
        {
            var triangles = new List<GeoTriangle2>(rim.Count);

            for (int i = 0; i < rim.Count; i++)
            {
                var triangle = new GeoTriangle2(center, rim[i], rim[(i + 1) % rim.Count]);

                if (!triangle.IsDegenerate(tolerance))
                {
                    triangles.Add(triangle.IsClockwise ? triangle.Reverse() : triangle);
                }
            }

            return triangles.ToArray();
        }
    }
}
