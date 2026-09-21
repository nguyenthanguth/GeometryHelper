using System;
using System.Collections.Generic;
using Clipper2Lib;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Planar;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static methods that combine regions in the plane: union, intersection, difference and exclusive
    /// or, as AutoCAD's UNION, INTERSECT and SUBTRACT do for regions.
    /// <para>
    /// A polygon is read as the region it encloses under the even-odd rule, as <see cref="Containment2"/> reads
    /// it, and a face as its boundary less its holes. The result is always a set of faces: combining regions can
    /// leave several pieces, and pieces with holes, which a polygon alone cannot carry. The faces come back
    /// largest first, every boundary counter-clockwise and every hole clockwise, so that signed areas add up.
    /// An empty result is an empty array.
    /// </para>
    /// <para>
    /// The work is done by Clipper2, on integer coordinates in a frame placed at the first shape, so the answer
    /// does not depend on rounding luck and loses no precision far from the origin. Points closer than the
    /// point tolerance are merged, and pieces thinner than it are dropped as the seams two outlines leave where
    /// they almost meet.
    /// </para>
    /// </summary>
    public static partial class Boolean2
    {
        #region Two polygons

        /// <summary>
        /// Gets the region covered by either of two polygons, using default tolerance.
        /// </summary>
        public static GeoFace2[] Union(GeoPolygon2 polygon1, GeoPolygon2 polygon2) => Union(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by either of two polygons, within tolerance.
        /// </summary>
        /// <param name="polygon1">The first polygon.</param>
        /// <param name="polygon2">The second polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces of the union: one where the polygons overlap or touch along an edge, two where they are apart.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a polygon is null.</exception>
        public static GeoFace2[] Union(GeoPolygon2 polygon1, GeoPolygon2 polygon2, Tolerance tolerance)
        {
            return Combine(ClipType.Union, new[] { Of(polygon1, nameof(polygon1)), Of(polygon2, nameof(polygon2)) }, null, tolerance);
        }

        /// <summary>
        /// Gets the region covered by both of two polygons, using default tolerance.
        /// </summary>
        public static GeoFace2[] Intersect(GeoPolygon2 polygon1, GeoPolygon2 polygon2) => Intersect(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by both of two polygons, within tolerance.
        /// </summary>
        /// <param name="polygon1">The first polygon.</param>
        /// <param name="polygon2">The second polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces of the overlap; none when the polygons only touch or lie apart.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a polygon is null.</exception>
        public static GeoFace2[] Intersect(GeoPolygon2 polygon1, GeoPolygon2 polygon2, Tolerance tolerance)
        {
            return Combine(ClipType.Intersection, new[] { Of(polygon1, nameof(polygon1)) }, new[] { Of(polygon2, nameof(polygon2)) }, tolerance);
        }

        /// <summary>
        /// Gets the region of one polygon with another taken out of it, using default tolerance.
        /// </summary>
        public static GeoFace2[] Subtract(GeoPolygon2 subject, GeoPolygon2 tool) => Subtract(subject, tool, Tolerance.Global);

        /// <summary>
        /// Gets the region of one polygon with another taken out of it, within tolerance.
        /// </summary>
        /// <param name="subject">The polygon cut from.</param>
        /// <param name="tool">The polygon taken out.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces left: a tool lying wholly inside the subject leaves a hole.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a polygon is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygon2 subject, GeoPolygon2 tool, Tolerance tolerance)
        {
            return Combine(ClipType.Difference, new[] { Of(subject, nameof(subject)) }, new[] { Of(tool, nameof(tool)) }, tolerance);
        }

        /// <summary>
        /// Gets the region covered by exactly one of two polygons, using default tolerance.
        /// </summary>
        public static GeoFace2[] Xor(GeoPolygon2 polygon1, GeoPolygon2 polygon2) => Xor(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by exactly one of two polygons, within tolerance: their union less their
        /// overlap.
        /// </summary>
        /// <param name="polygon1">The first polygon.</param>
        /// <param name="polygon2">The second polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces covered by one polygon but not the other.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a polygon is null.</exception>
        public static GeoFace2[] Xor(GeoPolygon2 polygon1, GeoPolygon2 polygon2, Tolerance tolerance)
        {
            return Combine(ClipType.Xor, new[] { Of(polygon1, nameof(polygon1)) }, new[] { Of(polygon2, nameof(polygon2)) }, tolerance);
        }

        #endregion

        #region Two faces

        /// <summary>
        /// Gets the region covered by either of two faces, using default tolerance.
        /// </summary>
        public static GeoFace2[] Union(GeoFace2 face1, GeoFace2 face2) => Union(face1, face2, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by either of two faces, within tolerance. A hole in one face that the other
        /// covers is filled.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when a face is null.</exception>
        public static GeoFace2[] Union(GeoFace2 face1, GeoFace2 face2, Tolerance tolerance)
        {
            return Combine(ClipType.Union, new[] { Of(face1, nameof(face1)), Of(face2, nameof(face2)) }, null, tolerance);
        }

        /// <summary>
        /// Gets the region covered by both of two faces, using default tolerance.
        /// </summary>
        public static GeoFace2[] Intersect(GeoFace2 face1, GeoFace2 face2) => Intersect(face1, face2, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by both of two faces, within tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when a face is null.</exception>
        public static GeoFace2[] Intersect(GeoFace2 face1, GeoFace2 face2, Tolerance tolerance)
        {
            return Combine(ClipType.Intersection, new[] { Of(face1, nameof(face1)) }, new[] { Of(face2, nameof(face2)) }, tolerance);
        }

        /// <summary>
        /// Gets the region of one face with another taken out of it, using default tolerance.
        /// </summary>
        public static GeoFace2[] Subtract(GeoFace2 subject, GeoFace2 tool) => Subtract(subject, tool, Tolerance.Global);

        /// <summary>
        /// Gets the region of one face with another taken out of it, within tolerance. The tool's own holes are
        /// not taken out, since they are not part of it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when a face is null.</exception>
        public static GeoFace2[] Subtract(GeoFace2 subject, GeoFace2 tool, Tolerance tolerance)
        {
            return Combine(ClipType.Difference, new[] { Of(subject, nameof(subject)) }, new[] { Of(tool, nameof(tool)) }, tolerance);
        }

        /// <summary>
        /// Gets the region covered by exactly one of two faces, using default tolerance.
        /// </summary>
        public static GeoFace2[] Xor(GeoFace2 face1, GeoFace2 face2) => Xor(face1, face2, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by exactly one of two faces, within tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when a face is null.</exception>
        public static GeoFace2[] Xor(GeoFace2 face1, GeoFace2 face2, Tolerance tolerance)
        {
            return Combine(ClipType.Xor, new[] { Of(face1, nameof(face1)) }, new[] { Of(face2, nameof(face2)) }, tolerance);
        }

        #endregion

        #region Many shapes

        /// <summary>
        /// Gets the region covered by any of a set of polygons, using default tolerance.
        /// </summary>
        public static GeoFace2[] Union(IEnumerable<GeoPolygon2> polygons) => Union(polygons, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by any of a set of polygons, within tolerance. Each polygon is read on its own
        /// under the even-odd rule before they are joined, so two that overlap stay filled where they overlap.
        /// </summary>
        /// <param name="polygons">The polygons; none gives no faces.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces of the union.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sequence or one of its polygons is null.</exception>
        public static GeoFace2[] Union(IEnumerable<GeoPolygon2> polygons, Tolerance tolerance)
        {
            if (polygons == null) throw new ArgumentNullException(nameof(polygons));

            List<Operand> operands = new List<Operand>();

            foreach (GeoPolygon2 polygon in polygons)
            {
                operands.Add(Of(polygon, nameof(polygons)));
            }

            return Combine(ClipType.Union, operands, null, tolerance);
        }

        /// <summary>
        /// Gets the region covered by any of a set of faces, using default tolerance.
        /// </summary>
        public static GeoFace2[] Union(IEnumerable<GeoFace2> faces) => Union(faces, Tolerance.Global);

        /// <summary>
        /// Gets the region covered by any of a set of faces, within tolerance.
        /// </summary>
        /// <param name="faces">The faces; none gives no faces.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces of the union.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sequence or one of its faces is null.</exception>
        public static GeoFace2[] Union(IEnumerable<GeoFace2> faces, Tolerance tolerance)
        {
            if (faces == null) throw new ArgumentNullException(nameof(faces));

            List<Operand> operands = new List<Operand>();

            foreach (GeoFace2 face in faces)
            {
                operands.Add(Of(face, nameof(faces)));
            }

            return Combine(ClipType.Union, operands, null, tolerance);
        }

        /// <summary>
        /// Gets the region of a polygon with every one of a set of polygons taken out of it, using default
        /// tolerance.
        /// </summary>
        public static GeoFace2[] Subtract(GeoPolygon2 subject, IEnumerable<GeoPolygon2> tools) => Subtract(subject, tools, Tolerance.Global);

        /// <summary>
        /// Gets the region of a polygon with every one of a set of polygons taken out of it, within tolerance: a
        /// slab with its openings cut, in one call.
        /// </summary>
        /// <param name="subject">The polygon cut from.</param>
        /// <param name="tools">The polygons taken out; none leaves the subject whole.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces left.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the subject, the sequence or one of its polygons is null.</exception>
        public static GeoFace2[] Subtract(GeoPolygon2 subject, IEnumerable<GeoPolygon2> tools, Tolerance tolerance)
        {
            if (tools == null) throw new ArgumentNullException(nameof(tools));

            Operand subjectOperand = Of(subject, nameof(subject));
            List<Operand> operands = new List<Operand>();

            foreach (GeoPolygon2 tool in tools)
            {
                operands.Add(Of(tool, nameof(tools)));
            }

            return Combine(ClipType.Difference, new[] { subjectOperand }, operands, tolerance);
        }

        /// <summary>
        /// Gets the region of a face with every one of a set of faces taken out of it, using default tolerance.
        /// </summary>
        public static GeoFace2[] Subtract(GeoFace2 subject, IEnumerable<GeoFace2> tools) => Subtract(subject, tools, Tolerance.Global);

        /// <summary>
        /// Gets the region of a face with every one of a set of faces taken out of it, within tolerance.
        /// </summary>
        /// <param name="subject">The face cut from.</param>
        /// <param name="tools">The faces taken out; none leaves the subject whole.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The faces left.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the subject, the sequence or one of its faces is null.</exception>
        public static GeoFace2[] Subtract(GeoFace2 subject, IEnumerable<GeoFace2> tools, Tolerance tolerance)
        {
            if (tools == null) throw new ArgumentNullException(nameof(tools));

            Operand subjectOperand = Of(subject, nameof(subject));
            List<Operand> operands = new List<Operand>();

            foreach (GeoFace2 tool in tools)
            {
                operands.Add(Of(tool, nameof(tools)));
            }

            return Combine(ClipType.Difference, new[] { subjectOperand }, operands, tolerance);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// One shape taking part: its vertices, to place the frame and size the rounding grid, and how to lay
        /// its region out as loops in that frame.
        /// </summary>
        private sealed class Operand
        {
            public Operand(IEnumerable<GeoPoint2> vertices, GeoPoint2 first, Func<GeoPoint2, int, Tolerance, List<List<GeoPoint2>>> region)
            {
                Vertices = vertices;
                First = first;
                Region = region;
            }

            public IEnumerable<GeoPoint2> Vertices { get; }

            public GeoPoint2 First { get; }

            public Func<GeoPoint2, int, Tolerance, List<List<GeoPoint2>>> Region { get; }
        }

        private static Operand Of(GeoPolygon2 polygon, string name)
        {
            if (polygon == null) throw new ArgumentNullException(name);

            return new Operand(polygon.Vertices, polygon[0], (origin, precision, tolerance) => ClipperRegion.RegionOf(polygon, origin, precision, tolerance));
        }

        private static Operand Of(GeoFace2 face, string name)
        {
            if (face == null) throw new ArgumentNullException(name);

            List<GeoPoint2> vertices = new List<GeoPoint2>(face.Boundary.Vertices);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                vertices.AddRange(hole.Vertices);
            }

            return new Operand(vertices, face.Boundary[0], (origin, precision, tolerance) => ClipperRegion.RegionOf(face, origin, precision, tolerance));
        }

        /// <summary>
        /// Lays every operand out in one frame, has Clipper2 combine the subjects with the clips under the
        /// positive rule, and builds the faces of the result.
        /// </summary>
        private static GeoFace2[] Combine(ClipType clipType, IReadOnlyList<Operand> subjects, IReadOnlyList<Operand> clips, Tolerance tolerance)
        {
            Operand anchor = subjects.Count > 0 ? subjects[0] : clips != null && clips.Count > 0 ? clips[0] : null;

            if (anchor == null)
            {
                return Array.Empty<GeoFace2>();
            }

            GeoPoint2 origin = anchor.First;
            double extent = 0.0;

            foreach (Operand operand in Concat(subjects, clips))
            {
                extent = Math.Max(extent, ClipperRegion.Extent(operand.Vertices, origin));
            }

            int precision = ClipperRegion.GetPrecision(extent);
            List<List<GeoPoint2>> subjectLoops = new List<List<GeoPoint2>>();
            List<List<GeoPoint2>> clipLoops = new List<List<GeoPoint2>>();

            foreach (Operand operand in subjects)
            {
                subjectLoops.AddRange(operand.Region(origin, precision, tolerance));
            }

            if (clips != null)
            {
                foreach (Operand operand in clips)
                {
                    clipLoops.AddRange(operand.Region(origin, precision, tolerance));
                }
            }

            // Every operand's loops wind once round its own region, so the positive rule reads the subjects, and
            // separately the clips, as the union of their regions however they overlap.
            List<LoopGroup> groups = ClipperRegion.Execute(clipType, subjectLoops, clipLoops, Clipper2Lib.FillRule.Positive, precision, tolerance);
            return ClipperRegion.ToFaces(groups, origin, false);
        }

        private static IEnumerable<Operand> Concat(IReadOnlyList<Operand> first, IReadOnlyList<Operand> second)
        {
            foreach (Operand operand in first)
            {
                yield return operand;
            }

            if (second != null)
            {
                foreach (Operand operand in second)
                {
                    yield return operand;
                }
            }
        }

        #endregion
    }
}
