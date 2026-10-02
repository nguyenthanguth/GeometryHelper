using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// Breaking the flat shapes of space into the faces of a <see cref="GeoMesh3"/>: triangles, the cells of a grid, strips or
    /// convex pieces, as the shapes of the plane break into a <see cref="GeoMesh2"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A flat shape is laid out in a frame of its plane and meshed there exactly as <see cref="Mesh2"/> meshes the shapes of
    /// the plane, with the same <see cref="MeshOptions"/>; the vertices then go back into space, the shape's own corners
    /// exactly where it has them. The frame has its Z axis along the shape's normal, so the faces run counter-clockwise seen
    /// from that side, and its X axis along the grid's first axis, which the <see cref="MeshPlacement3"/> lays in the plane:
    /// level, with the second axis up the slope, unless it says otherwise. <see cref="MeshOptions.AngleRad"/> turns it from
    /// there, counter-clockwise about the normal.
    /// </para>
    /// <para>
    /// In space a grid's origin is a point of space, given by <see cref="MeshPlacement3.At"/> and put onto the shape's plane;
    /// <see cref="MeshOptions.Origin"/>, a point of the plane, is refused here. The shapes' own <c>ToMesh</c> members come
    /// here.
    /// </para>
    /// </remarks>
    public static class Mesh3
    {
        #region Polygons

        /// <summary>
        /// Breaks a polygon into faces as the options say, its grid level, using the default tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoPolygon3 polygon, MeshOptions options) => ToMesh(polygon, options, MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks a polygon into faces as the options say, its grid level, within a tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoPolygon3 polygon, MeshOptions options, Tolerance tolerance) => ToMesh(polygon, options, MeshPlacement3.World, tolerance);

        /// <summary>
        /// Breaks a polygon into faces as the options say, its grid standing as the placement says, using the default
        /// tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoPolygon3 polygon, MeshOptions options, MeshPlacement3 placement) => ToMesh(polygon, options, placement, Tolerance.Global);

        /// <summary>
        /// Breaks a polygon into faces as the options say, its grid standing as the placement says, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon; one crossing itself is read as <see cref="GeoPolygon2.MakeValid()"/> reads it.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the polygon's plane, and where a cell starts.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when the polygon encloses no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape, the options or the placement are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the options give an origin of the plane, a grid's cell is no larger than the point tolerance, the grid
        /// would lay more cells over the shape than a mesh may have, or the placement's direction stands square to the plane.
        /// </exception>
        public static GeoMesh3 ToMesh(GeoPolygon3 polygon, MeshOptions options, MeshPlacement3 placement, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            Check(options, placement);
            return MeshRings(polygon.Vertices, new IReadOnlyList<GeoPoint3>[0], polygon.Normal, options, placement, tolerance);
        }

        #endregion

        #region Faces

        /// <summary>
        /// Breaks a face into faces with no hole as the options say, its grid level, using the default tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoFace3 face, MeshOptions options) => ToMesh(face, options, MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks a face into faces with no hole as the options say, its grid level, within a tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoFace3 face, MeshOptions options, Tolerance tolerance) => ToMesh(face, options, MeshPlacement3.World, tolerance);

        /// <summary>
        /// Breaks a face into faces with no hole as the options say, its grid standing as the placement says, using the
        /// default tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoFace3 face, MeshOptions options, MeshPlacement3 placement) => ToMesh(face, options, placement, Tolerance.Global);

        /// <summary>
        /// Breaks a face into faces with no hole as the options say, its grid standing as the placement says, within a
        /// tolerance.
        /// </summary>
        /// <param name="face">The face; its holes are left open.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the face's plane, and where a cell starts.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when the face encloses no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape, the options or the placement are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the options give an origin of the plane, a grid's cell is no larger than the point tolerance, the grid
        /// would lay more cells over the shape than a mesh may have, or the placement's direction stands square to the plane.
        /// </exception>
        /// <remarks>
        /// A face a hair out of flat, as <see cref="GeoFace3.FromLoops(IEnumerable{GeoPoint3}, IEnumerable{IEnumerable{GeoPoint3}})"/>
        /// keeps them, keeps its corners where they are, and the points the meshing puts on its sides stay on them; only the
        /// points inside lie on the plane.
        /// </remarks>
        public static GeoMesh3 ToMesh(GeoFace3 face, MeshOptions options, MeshPlacement3 placement, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return MeshFace(face, options, placement, tolerance);
        }

        #endregion

        #region Loops with arcs

        /// <summary>
        /// Breaks a loop with arcs into faces as the options say, its grid level, using the default tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoPolygonArc3 loop, MeshOptions options) => ToMesh(loop, options, MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks a loop with arcs into faces as the options say, its grid level, within a tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoPolygonArc3 loop, MeshOptions options, Tolerance tolerance) => ToMesh(loop, options, MeshPlacement3.World, tolerance);

        /// <summary>
        /// Breaks a loop with arcs into faces as the options say, its grid standing as the placement says, using the default
        /// tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoPolygonArc3 loop, MeshOptions options, MeshPlacement3 placement) => ToMesh(loop, options, placement, Tolerance.Global);

        /// <summary>
        /// Breaks a loop with arcs into faces as the options say, its grid standing as the placement says, within a tolerance,
        /// each arc flattened by the options' chord tolerance first.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the loop's plane, and where a cell starts.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>
        /// The mesh of the flattened loop, its normal the one the loop runs counter-clockwise about; one with no faces when it
        /// encloses no area.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the shape, the options or the placement are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the options give an origin of the plane, a grid's cell is no larger than the point tolerance, the grid
        /// would lay more cells over the shape than a mesh may have, or the placement's direction stands square to the plane.
        /// </exception>
        public static GeoMesh3 ToMesh(GeoPolygonArc3 loop, MeshOptions options, MeshPlacement3 placement, Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            Check(options, placement);

            // The loop's plane does not say which way round it runs; the mesh's normal is the one it runs counter-clockwise
            // about, as a polygon's is.
            GeoVector3 normal = loop.Normal;
            GeoCoordinateSystem3 trial = new GeoCoordinateSystem3(new GeoPlane3(loop[0], normal));

            if (loop.ProjectToPolygonArc2(trial).IsClockwise)
            {
                normal = normal.Negate();
            }

            GeoCoordinateSystem3 frame = placement.FrameOnPlane(loop[0], normal, loop.Vertices, options.AngleRad ?? 0.0, tolerance);
            GeoPolygonArc2 laid = loop.ProjectToPolygonArc2(frame);
            GeoMesh2 flat = Mesh2.ToMesh(laid, options.InFrame(Anchor(options, placement, frame, tolerance)), tolerance);

            var corners = new GeoPoint2[laid.VertexCount];

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = laid[i];
            }

            return Lifted(flat, frame, new[] { loop.Vertices }, new[] { corners }, false);
        }

        #endregion

        #region Discs

        /// <summary>
        /// Breaks a disc into faces as the options say, its grid level, using the default tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoCircle3 circle, MeshOptions options) => ToMesh(circle, options, MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks a disc into faces as the options say, its grid level, within a tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoCircle3 circle, MeshOptions options, Tolerance tolerance) => ToMesh(circle, options, MeshPlacement3.World, tolerance);

        /// <summary>
        /// Breaks a disc into faces as the options say, its grid standing as the placement says, using the default tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoCircle3 circle, MeshOptions options, MeshPlacement3 placement) => ToMesh(circle, options, placement, Tolerance.Global);

        /// <summary>
        /// Breaks a disc into faces as the options say, its grid standing as the placement says, within a tolerance, its rim
        /// flattened by the options' chord tolerance first.
        /// </summary>
        /// <param name="circle">The circle; its normal is the mesh's.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the disc's plane, and where a cell starts.</param>
        /// <param name="tolerance">The tolerance the shape is read within: which of its rings touch, what has no area, and for a grid which cells are whole.</param>
        /// <returns>
        /// The mesh of the disc's rim flattened; its triangles fanned from the center, as in the plane. None when the radius
        /// is nought.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the options or the placement are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the options give an origin of the plane, a grid's cell is no larger than the point tolerance, the grid
        /// would lay more cells over the shape than a mesh may have, or the placement's direction stands square to the plane.
        /// </exception>
        public static GeoMesh3 ToMesh(GeoCircle3 circle, MeshOptions options, MeshPlacement3 placement, Tolerance tolerance)
        {
            Check(options, placement);

            GeoCoordinateSystem3 frame = placement.FrameOnPlane(circle.Center, circle.Normal, new[] { circle.Center }, options.AngleRad ?? 0.0, tolerance);

            if (!(circle.Radius > tolerance.EqualPoint))
            {
                return new GeoMesh3(Mesh2.Empty(options.Kind, tolerance), new GeoPoint3[0], frame);
            }

            // The rim has no corners of its own, so its points go onto the plane, where the circle is.
            GeoMesh2 flat = Mesh2.ToMesh(new GeoCircle2(new GeoPoint2(0.0, 0.0), circle.Radius), options.InFrame(Anchor(options, placement, frame, tolerance)), tolerance);

            return Lifted(flat, frame, new[] { (IReadOnlyList<GeoPoint3>)new[] { circle.Center } }, new[] { new[] { new GeoPoint2(0.0, 0.0) } }, false);
        }

        #endregion

        #region Triangles

        /// <summary>
        /// Breaks a triangle into faces as the options say, its grid level, using the default tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoTriangle3 triangle, MeshOptions options) => ToMesh(triangle, options, MeshPlacement3.World, Tolerance.Global);

        /// <summary>
        /// Breaks a triangle into faces as the options say, its grid level, within a tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoTriangle3 triangle, MeshOptions options, Tolerance tolerance) => ToMesh(triangle, options, MeshPlacement3.World, tolerance);

        /// <summary>
        /// Breaks a triangle into faces as the options say, its grid standing as the placement says, using the default
        /// tolerance.
        /// </summary>
        public static GeoMesh3 ToMesh(GeoTriangle3 triangle, MeshOptions options, MeshPlacement3 placement) => ToMesh(triangle, options, placement, Tolerance.Global);

        /// <summary>
        /// Breaks a triangle into faces as the options say, its grid standing as the placement says, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle; its normal, from A by B to C, is the mesh's.</param>
        /// <param name="options">How to break it up.</param>
        /// <param name="placement">Which way the grid or the strips run in the triangle's plane, and where a cell starts.</param>
        /// <param name="tolerance">The tolerance the shape is read within: what has no area, and for a grid which cells are whole.</param>
        /// <returns>The mesh; one with no faces when the triangle encloses no area within the tolerance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the options or the placement are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the options give an origin of the plane, a grid's cell is no larger than the point tolerance, the grid
        /// would lay more cells over the shape than a mesh may have, or the placement's direction stands square to the plane.
        /// </exception>
        public static GeoMesh3 ToMesh(GeoTriangle3 triangle, MeshOptions options, MeshPlacement3 placement, Tolerance tolerance)
        {
            Check(options, placement);

            if (triangle.IsDegenerate(tolerance) || !triangle.GetAreaVector().TryGetNormal(out GeoVector3 normal, tolerance))
            {
                GeoCoordinateSystem3 anywhere = GeoCoordinateSystem3.Global.WithOrigin(triangle.A);
                return new GeoMesh3(Mesh2.Empty(options.Kind, tolerance), new GeoPoint3[0], anywhere);
            }

            var corners = new[] { triangle.A, triangle.B, triangle.C };
            return MeshRings(corners, new IReadOnlyList<GeoPoint3>[0], normal, options, placement, tolerance);
        }

        #endregion

        /// <summary>
        /// Meshes a face of straight edges: laid out in the frame its placement gives, meshed in the plane, lifted back.
        /// </summary>
        private static GeoMesh3 MeshFace(GeoFace3 face, MeshOptions options, MeshPlacement3 placement, Tolerance tolerance)
        {
            Check(options, placement);

            var holes = new IReadOnlyList<GeoPoint3>[face.Holes.Count];

            for (int i = 0; i < holes.Length; i++)
            {
                holes[i] = face.Holes[i].Vertices;
            }

            return MeshRings(face.Boundary.Vertices, holes, face.Normal, options, placement, tolerance);
        }

        private static GeoMesh3 MeshRings(IReadOnlyList<GeoPoint3> boundary, IReadOnlyList<GeoPoint3>[] holes, GeoVector3 normal, MeshOptions options, MeshPlacement3 placement, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = placement.FrameOnPlane(boundary[0], normal, boundary, options.AngleRad ?? 0.0, tolerance);
            var rings = new List<IReadOnlyList<GeoPoint3>>(holes.Length + 1) { boundary };
            rings.AddRange(holes);

            var laid = new List<GeoPoint2[]>(rings.Count);
            GeoPolygon2 outline = null;
            var openings = new List<GeoPolygon2>(holes.Length);

            for (int r = 0; r < rings.Count; r++)
            {
                GeoPoint2[] flat = MeshLift3.LayOut(frame, rings[r]);
                laid.Add(flat);
                GeoPolygon2 polygon = MeshLift3.LayOutPolygon(flat);

                if (r == 0)
                {
                    outline = polygon;
                }
                else if (polygon != null)
                {
                    openings.Add(polygon);
                }
            }

            if (outline == null)
            {
                return new GeoMesh3(Mesh2.Empty(options.Kind, tolerance), new GeoPoint3[0], frame);
            }

            GeoMesh2 mesh = Mesh2.Mesh(new GeoFace2(outline, openings), options.InFrame(Anchor(options, placement, frame, tolerance)), 0.0, tolerance);
            return Lifted(mesh, frame, rings, laid, true);
        }

        private static GeoMesh3 Lifted(GeoMesh2 flat, GeoCoordinateSystem3 frame, IReadOnlyList<IReadOnlyList<GeoPoint3>> rings, IReadOnlyList<GeoPoint2[]> laid, bool straight)
            => new GeoMesh3(flat, MeshLift3.Lift(flat, frame, rings, laid, straight), frame);

        private static void Check(MeshOptions options, MeshPlacement3 placement)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (placement == null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            if (options.Origin.HasValue)
            {
                throw new ArgumentException(
                    "In space a grid's origin is a point of space: give it with MeshPlacement3.At(origin) rather than as MeshOptions.Origin.",
                    nameof(options));
            }
        }

        /// <summary>
        /// Where a cell of an anchored grid starts in the frame: the placement's origin put onto the plane, moved by whole
        /// cells and joints to the cell nearest the frame's origin, so that the shape is laid out from near it and Clipper2
        /// rounds it as finely as the shape allows. Null when the alignments place the grid.
        /// </summary>
        private static GeoPoint2? Anchor(MeshOptions options, MeshPlacement3 placement, GeoCoordinateSystem3 frame, Tolerance tolerance)
        {
            if (options.Kind != MeshKind.Grid || !placement.Origin.HasValue)
            {
                return null;
            }

            GeoPoint3 local = frame.ToLocal(placement.Origin.Value);

            // The joint the grid lays: one no wider than the tolerance is none.
            double joint = options.Joint > tolerance.EqualPoint ? options.Joint : 0.0;
            double pitchU = options.CellWidth + joint;
            double pitchV = options.CellHeight + joint;

            return new GeoPoint2(local.X - Math.Round(local.X / pitchU) * pitchU, local.Y - Math.Round(local.Y / pitchV) * pitchV);
        }
    }
}
