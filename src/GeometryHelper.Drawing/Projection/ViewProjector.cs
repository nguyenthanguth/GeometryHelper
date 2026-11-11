using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// Projects bodies into the lines of one view: which edges are seen, which are hidden, and the faces a section cuts,
    /// exactly on the planar faces of the bodies, to the point tolerance, with no depth buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view is orthographic, along the Z axis of its frame: the viewer stands on the side Z points to and looks back
    /// towards minus Z. A point of the model is drawn at its X and Y in the frame, in millimetres at 1:1. A box 300 by 200
    /// by 20 lying in the frame's XY plane is seen from the front as a rectangle of four seen lines, 300 by 200: its four
    /// back edges fall on the four front ones and are not drawn again, and the four edges running along Z are points.
    /// Turned so that three of its faces are seen, it shows nine seen lines and three hidden ones.
    /// </para>
    /// <para>
    /// Which edges are drawn. An edge where two faces of a body meet at more than the smooth angle is sharp and drawn. An edge
    /// between two faces meeting within it, two facets of a tube, is drawn only as a silhouette, where one of its faces is
    /// turned to the viewer and the other away or seen edge-on. An edge seen end-on, shorter in the view than the point
    /// tolerance, is a point and not drawn. A face whose side runs past a corner of the face beside it, as the faces a boolean
    /// leaves can, is read as meeting it in pieces.
    /// </para>
    /// <para>
    /// What hides what. An edge is hidden where it lies within a face turned to the viewer, the face's outline counting as
    /// within it to the point tolerance, and stands behind the face's plane by more than the point tolerance; a face never
    /// hides the edges it is bounded by. So an edge piercing a face is seen on one side and hidden on the other; an edge lying
    /// in a face's plane, as where two plates touch face to face or lie edge to edge with their faces flush, is not hidden by
    /// it; and a face seen edge-on, its normal square to the view within the vector tolerance, hides nothing. A face a hair
    /// off flat is taken in the plane through the middle of its corners.
    /// </para>
    /// <para>
    /// Each line once. Where edges fall on one another in the view, of one object or of two touching, the line is drawn once:
    /// seen where any of them is seen, hidden where all are, and listing the objects whose edges give it, lowest id first.
    /// Lines on one line that touch, alike in visibility, kind and objects, are one line. Chains of short lines a faceted
    /// circle leaves are fitted back into arcs and circles (see <see cref="ProjectionOptions.SmoothAngleRad"/>): a hole of 22
    /// through a plate seen along its axis is one circle of 22.
    /// </para>
    /// <para>
    /// A body's openings are cut in before it is drawn. The bodies are taken in the order of their ids, so the same bodies in
    /// any order give the same view, to the bit. Every body should be closed and wound outwards, as the library makes them;
    /// one wound inwards is turned first. Faces turned away from the viewer hide nothing, since a closed body always has a face
    /// turned to the viewer in front of them, so an open body can show edges its missing faces would have hidden.
    /// </para>
    /// </remarks>
    public static class ViewProjector
    {
        /// <summary>
        /// Projects bodies into a plain view, with the default smooth angle and no limit on depth.
        /// </summary>
        /// <param name="inputs">The objects, each with an id unique among them.</param>
        /// <param name="frame">The view frame: X to the right of the sheet, Y up it, Z towards the viewer.</param>
        /// <param name="tolerance">The tolerance: points within its point tolerance are one.</param>
        /// <returns>The lines of the view.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the objects are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when an object has no id or no body, two objects have the same id, or the frame's axes are not of unit
        /// length and square to each other.
        /// </exception>
        public static ProjectedView2 Project(
            IEnumerable<ProjectionInput> inputs,
            GeoCoordinateSystem3 frame,
            Tolerance tolerance
        ) => Project(inputs, frame, ProjectionOptions.Default, tolerance);

        /// <summary>
        /// Projects bodies into a view: the lines seen and hidden, and, for a section, the faces the plane cuts.
        /// </summary>
        /// <param name="inputs">The objects, each with an id unique among them.</param>
        /// <param name="frame">The view frame: X to the right of the sheet, Y up it, Z towards the viewer.</param>
        /// <param name="options">The smooth angle, the depth of the view and the section plane.</param>
        /// <param name="tolerance">The tolerance: points within its point tolerance are one.</param>
        /// <returns>The lines of the view.</returns>
        /// <remarks>
        /// <para>
        /// For a section, what lies between the plane and the viewer is taken away: each body is split by the plane
        /// (<see cref="GeoSolid3.TrySplitBy(GeoPlane3, out GeoSolid3, out GeoSolid3, Tolerance)"/>) and the part beyond it
        /// drawn, its cut face hiding what lies behind it. The faces the plane cuts out of each body are those of
        /// <see cref="GeoSolid3.Section(GeoPlane3, Tolerance)"/>, seen in the view; a plane square to the view's Z axis gives
        /// them at their own size, so a plate 300 by 200 by 20 cut across its length gives a face of 4 000. A body the plane
        /// misses is drawn whole where it lies beyond the plane and left out where it lies in front. A body the split refuses
        /// though the plane crosses it is drawn whole, and logged as a warning.
        /// </para>
        /// <para>
        /// A body that reaches into neither side of the depth range is left out, and counted in one debug line of the log.
        /// A body whose openings cannot be cut in is drawn without them, and logged as a warning.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the objects or the options are null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when an object has no id or no body, two objects have the same id, the frame's axes are not of unit length
        /// and square to each other, or the section plane is seen edge-on.
        /// </exception>
        public static ProjectedView2 Project(
            IEnumerable<ProjectionInput> inputs,
            GeoCoordinateSystem3 frame,
            ProjectionOptions options,
            Tolerance tolerance
        )
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!frame.IsValid)
            {
                throw new ArgumentException(
                    "A view frame needs axes of unit length, square to each other.",
                    nameof(frame)
                );
            }

            var objects = new List<ProjectionInput>();

            foreach (ProjectionInput input in inputs)
            {
                if (string.IsNullOrEmpty(input.Id) || input.Solid == null)
                {
                    throw new ArgumentException("Every object needs an id and a body.", nameof(inputs));
                }

                objects.Add(input);
            }

            objects.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var ids = new string[objects.Count];

            for (int i = 0; i < objects.Count; i++)
            {
                ids[i] = objects[i].Id;

                if (i > 0 && string.CompareOrdinal(ids[i - 1], ids[i]) == 0)
                {
                    throw new ArgumentException("Two objects have the id " + ids[i] + ".", nameof(inputs));
                }
            }

            GeoPlane3? cut = null;
            double[] cutInView = null;

            if (options.SectionPlane.HasValue)
            {
                GeoPlane3 plane = options.SectionPlane.Value;
                double facing = plane.Normal.DotProduct(frame.ZAxis);

                if (!(Math.Abs(facing) > tolerance.EqualVector))
                {
                    throw new ArgumentException(
                        "A section plane seen edge-on has no side away from the viewer.",
                        nameof(options)
                    );
                }

                // The normal is turned to the viewer, so that the part below the plane is the part kept.
                cut = facing > 0.0 ? plane : plane.Flip();
                GeoVector3 n = frame.ToLocal(cut.Value.Normal);
                GeoPoint3 o = frame.ToLocal(cut.Value.Origin);
                cutInView = new[] { n.X, n.Y, n.Z, n.X * o.X + n.Y * o.Y + n.Z * o.Z };
            }

            double tol = tolerance.EqualPoint;
            var edges = new List<CandidateEdge>();
            var occluders = new List<Occluder>();
            var sections = new List<ProjectedFace2>();
            int faceBase = 0;
            int outOfDepth = 0;

            for (int k = 0; k < objects.Count; k++)
            {
                GeoSolid3 body = Material(objects[k], tolerance);

                if (cut.HasValue)
                {
                    GeoFace3[] faces = body.Section(cut.Value, tolerance);
                    body = Beyond(body, objects[k].Id, cut.Value, tolerance);

                    if (body == null)
                    {
                        continue;
                    }

                    if (!InDepth(body, frame, options, tol))
                    {
                        outOfDepth++;
                        continue;
                    }

                    foreach (GeoFace3 face in faces)
                    {
                        GeoFace2 seen = Seen(face, frame, objects[k].Id);

                        if (seen != null)
                        {
                            sections.Add(new ProjectedFace2(ids[k], seen));
                        }
                    }
                }
                else if (!InDepth(body, frame, options, tol))
                {
                    outOfDepth++;
                    continue;
                }

                ViewBody.Add(
                    body,
                    k,
                    frame,
                    cutInView,
                    options.SmoothAngleRad,
                    tolerance,
                    ref faceBase,
                    edges,
                    occluders
                );
            }

            List<Piece2> pieces = Occlusion.Run(edges, occluders, tol);
            List<Piece2> lines = LineMerge.Run(pieces, tol);
            List<ProjectedEdge2> all = ArcFit.Run(lines, options.SmoothAngleRad, tol, ids);
            var visible = new List<ProjectedEdge2>();
            var hidden = new List<ProjectedEdge2>();

            foreach (ProjectedEdge2 edge in all)
            {
                (edge.Visibility == EdgeVisibility.Visible ? visible : hidden).Add(edge);
            }

            visible.Sort(CompareEdges);
            hidden.Sort(CompareEdges);
            sections.Sort(CompareFaces);

            if (outOfDepth > 0)
            {
                GeometryHelperLog.Debug(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ViewProjector: {0} of {1} objects lie outside the depth of the view and were left out.",
                        outOfDepth,
                        objects.Count
                    )
                );
            }

            GeometryHelperLog.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ViewProjector: {0} objects gave {1} seen lines, {2} hidden lines and {3} section faces.",
                    objects.Count,
                    visible.Count,
                    hidden.Count,
                    sections.Count
                )
            );
            bool empty = visible.Count == 0 && hidden.Count == 0 && sections.Count == 0;
            return new ProjectedView2(
                visible.ToArray(),
                hidden.ToArray(),
                sections.ToArray(),
                Bounds(all, sections),
                empty
            );
        }

        /// <summary>
        /// The body wound outwards with its openings cut in; the body as given, without them, where they cannot be.
        /// </summary>
        private static GeoSolid3 Material(ProjectionInput input, Tolerance tolerance)
        {
            GeoSolid3 solid = input.Solid.TurnOutwards();

            if (solid.Openings.Count == 0)
            {
                return solid;
            }

            if (solid.TryCutOpenings(out GeoSolid3 material, tolerance) && material != null)
            {
                return material;
            }

            GeometryHelperLog.Warn(
                "ViewProjector: the openings of object " + input.Id + " could not be cut in; it is drawn without them."
            );
            return solid.WithOpenings(new GeoSolid3[0]);
        }

        /// <summary>
        /// The part of a body beyond a section plane, the plane's normal turned to the viewer; null where none of it is.
        /// </summary>
        private static GeoSolid3 Beyond(GeoSolid3 body, string id, GeoPlane3 cut, Tolerance tolerance)
        {
            if (body.TrySplitBy(cut, out _, out GeoSolid3 below, tolerance))
            {
                return below;
            }

            bool before = false;
            bool after = false;

            foreach (GeoFace3 face in body.Faces)
            {
                foreach (GeoPoint3 p in face.Boundary.Vertices)
                {
                    double s = cut.Normal.DotProduct(cut.Origin.GetVectorTo(p));
                    before |= s > tolerance.EqualPoint;
                    after |= s < -tolerance.EqualPoint;
                }
            }

            if (!before)
            {
                return body;
            }

            if (!after)
            {
                return null;
            }

            GeometryHelperLog.Warn(
                "ViewProjector: the section plane crosses object "
                    + id
                    + " but it could not be split; it is drawn whole."
            );
            return body;
        }

        /// <summary>
        /// Whether a body reaches between the far and the near depth of the view.
        /// </summary>
        private static bool InDepth(GeoSolid3 body, GeoCoordinateSystem3 frame, ProjectionOptions options, double tol)
        {
            if (double.IsPositiveInfinity(options.NearDepth) && double.IsNegativeInfinity(options.FarDepth))
            {
                return true;
            }

            double low = double.PositiveInfinity;
            double high = double.NegativeInfinity;

            foreach (GeoFace3 face in body.Faces)
            {
                foreach (GeoPoint3 p in face.Boundary.Vertices)
                {
                    double z = frame.ToLocal(p).Z;
                    low = Math.Min(low, z);
                    high = Math.Max(high, z);
                }
            }

            return high >= options.FarDepth - tol && low <= options.NearDepth + tol;
        }

        /// <summary>
        /// A face of a section as seen in the view; null, logged, where its outline is no polygon in the view.
        /// </summary>
        private static GeoFace2 Seen(GeoFace3 face, GeoCoordinateSystem3 frame, string id)
        {
            try
            {
                var holes = new List<GeoPolygon2>();

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    holes.Add(Seen(hole, frame));
                }

                return new GeoFace2(Seen(face.Boundary, frame), holes);
            }
            catch (ArgumentException exception)
            {
                GeometryHelperLog.Warn(
                    "ViewProjector: a section face of object " + id + " is no polygon in the view and was left out.",
                    exception
                );
                return null;
            }
        }

        private static GeoPolygon2 Seen(GeoPolygon3 polygon, GeoCoordinateSystem3 frame)
        {
            var points = new List<GeoPoint2>(polygon.VertexCount);

            foreach (GeoPoint3 vertex in polygon.Vertices)
            {
                GeoPoint3 p = frame.ToLocal(vertex);
                points.Add(new GeoPoint2(p.X, p.Y));
            }

            return new GeoPolygon2(points);
        }

        private static GeoRectangle2 Bounds(List<ProjectedEdge2> edges, List<ProjectedFace2> sections)
        {
            double x0 = double.PositiveInfinity,
                y0 = double.PositiveInfinity;
            double x1 = double.NegativeInfinity,
                y1 = double.NegativeInfinity;

            void Grow(double x, double y)
            {
                x0 = Math.Min(x0, x);
                y0 = Math.Min(y0, y);
                x1 = Math.Max(x1, x);
                y1 = Math.Max(y1, y);
            }

            foreach (ProjectedEdge2 edge in edges)
            {
                if (!edge.IsArc)
                {
                    Grow(edge.Line.StartPoint.X, edge.Line.StartPoint.Y);
                    Grow(edge.Line.EndPoint.X, edge.Line.EndPoint.Y);
                    continue;
                }

                GeoArc2 arc = edge.Arc;
                Grow(arc.StartPoint.X, arc.StartPoint.Y);
                Grow(arc.EndPoint.X, arc.EndPoint.Y);

                // The arc reaches each quarter point of its circle that it sweeps past.
                for (int quarter = 0; quarter < 4; quarter++)
                {
                    double angle = quarter * Math.PI / 2.0;
                    double past = angle - arc.StartAngle;
                    past -= 2.0 * Math.PI * Math.Floor(past / (2.0 * Math.PI));

                    if (past <= arc.SweptAngle)
                    {
                        Grow(arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle));
                    }
                }
            }

            foreach (ProjectedFace2 section in sections)
            {
                foreach (GeoPoint2 p in section.Face.Boundary.Vertices)
                {
                    Grow(p.X, p.Y);
                }
            }

            return x0 > x1 ? new GeoRectangle2(0.0, 0.0, 0.0, 0.0) : new GeoRectangle2(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>
        /// Orders lines by their objects, kind and visibility, straight before round, then by their coordinates.
        /// </summary>
        private static int CompareEdges(ProjectedEdge2 a, ProjectedEdge2 b)
        {
            int c = CompareIds(a.ObjectIds, b.ObjectIds);
            if (c != 0)
                return c;
            c = a.Kind.CompareTo(b.Kind);
            if (c != 0)
                return c;
            c = a.Visibility.CompareTo(b.Visibility);
            if (c != 0)
                return c;
            c = a.IsArc.CompareTo(b.IsArc);
            if (c != 0)
                return c;

            if (!a.IsArc)
            {
                c = a.Line.StartPoint.X.CompareTo(b.Line.StartPoint.X);
                if (c != 0)
                    return c;
                c = a.Line.StartPoint.Y.CompareTo(b.Line.StartPoint.Y);
                if (c != 0)
                    return c;
                c = a.Line.EndPoint.X.CompareTo(b.Line.EndPoint.X);
                return c != 0 ? c : a.Line.EndPoint.Y.CompareTo(b.Line.EndPoint.Y);
            }

            c = a.Arc.Center.X.CompareTo(b.Arc.Center.X);
            if (c != 0)
                return c;
            c = a.Arc.Center.Y.CompareTo(b.Arc.Center.Y);
            if (c != 0)
                return c;
            c = a.Arc.Radius.CompareTo(b.Arc.Radius);
            if (c != 0)
                return c;
            c = a.Arc.StartAngle.CompareTo(b.Arc.StartAngle);
            return c != 0 ? c : a.Arc.SweptAngle.CompareTo(b.Arc.SweptAngle);
        }

        private static int CompareIds(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                int c = string.CompareOrdinal(a[i], b[i]);

                if (c != 0)
                {
                    return c;
                }
            }

            return a.Count.CompareTo(b.Count);
        }

        /// <summary>
        /// Orders section faces by object, then by the first corner of the outline, its corner count and its area.
        /// </summary>
        private static int CompareFaces(ProjectedFace2 a, ProjectedFace2 b)
        {
            int c = string.CompareOrdinal(a.ObjectId, b.ObjectId);
            if (c != 0)
                return c;
            GeoPoint2 pa = a.Face.Boundary.Vertices[0];
            GeoPoint2 pb = b.Face.Boundary.Vertices[0];
            c = pa.X.CompareTo(pb.X);
            if (c != 0)
                return c;
            c = pa.Y.CompareTo(pb.Y);
            if (c != 0)
                return c;
            c = a.Face.Boundary.VertexCount.CompareTo(b.Face.Boundary.VertexCount);
            return c != 0 ? c : a.Face.Area.CompareTo(b.Face.Area);
        }
    }
}
