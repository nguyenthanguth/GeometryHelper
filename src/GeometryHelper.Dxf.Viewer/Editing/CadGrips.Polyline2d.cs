using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The edits of the grips of a 2D POLYLINE of straight and arc segments (<see cref="DxfPolyline2d"/> of type
    /// <see cref="DxfPoly2dType.SimplePoly"/>), the same as a light polyline's: stretch a vertex or a segment, add a vertex,
    /// remove one, make a segment an arc or a line. A curve-fit or spline-fit one has a grip on each vertex of its frame, which
    /// <see cref="DragPolyline2d"/> moves and fits again, and no menu.
    /// </summary>
    public static partial class CadGrips
    {
        /// <summary>The vertices of a POLYLINE in order, for the edits of its grips.</summary>
        private static List<DxfVertex2d> VerticesOf(DxfPolyline2d heavy) => heavy.ToList();

        /// <summary>How many segments a POLYLINE draws: one fewer than its vertices, and the closing one where it is closed on more than two.</summary>
        private static int SegmentsOf(DxfPolyline2d heavy, int n) => heavy.Closed && n > 2 ? n : n - 1;

        /// <summary>How many vertices a polyline of either kind has; nought for an entity that is neither.</summary>
        internal static int VertexCount(DxfEntity entity) =>
            entity is DxfPolyline2d heavy ? VerticesOf(heavy).Count
            : entity is DxfPolyline light ? light.NumberOfVertices
            : 0;

        /// <summary>The bulge of a segment of a 2D POLYLINE.</summary>
        internal static double BulgeOf(DxfEntity entity, int segment) =>
            VerticesOf((DxfPolyline2d)entity)[segment].Bulge;

        /// <summary>The vertices of the frame of a POLYLINE fitted with a curve or a spline: those the fitting did not make, the vertices it was given.</summary>
        private static List<DxfVertex2d> FrameOf(DxfPolyline2d heavy) =>
            heavy
                .Where(v =>
                    v.VertexType == DxfVertex2dType.SimpleVertex || v.VertexType == DxfVertex2dType.SplineControlVertex
                )
                .ToList();

        private static GeoPoint2 At(DxfVertex2d vertex) => new GeoPoint2(vertex.Position.X, vertex.Position.Y);

        private static void MoveTo(DxfVertex2d vertex, GeoPoint2 own) =>
            vertex.Position = new GeoPoint3(own.X, own.Y, vertex.Position.Z);

        /// <summary>
        /// Moves a grip of a POLYLINE to a point, as a light polyline's is moved: a vertex alone, a straight segment whole, an
        /// arc segment bent through the point.
        /// </summary>
        private static CadEditResult DragPolyline2d(
            CadDocument document,
            DxfPolyline2d heavy,
            string handle,
            CadGrip grip,
            int gripIndex,
            GeoPoint2 to,
            string property,
            string was,
            string went
        )
        {
            List<DxfVertex2d> vertices = VerticesOf(heavy);
            int n = vertices.Count;
            bool below = heavy.Normal.Z < 0.0;
            GeoPoint2 own = Own(to, below);
            GeoVector2 shifted = own - Own(grip.Point, below);

            if (heavy.PolyType != DxfPoly2dType.SimplePoly)
            {
                // A frame vertex of a fitted one: straightened, the vertex moved, and fitted again as AutoCAD 2021 refits it (a
                // curve as before, a spline by SPLINETYPE and SPLINESEGS), probed: the frame (0, 0), (100, 0), (100, 60), (0, 60)
                // with its second vertex dragged by (7, 5) is fitted through (107, 5).
                return Reshape(document, handle, property, was, went, () =>
                {
                    var fitted = (DxfPolyline2d)document.FindEntity(handle);
                    DxfPoly2dType type = fitted.PolyType;
                    fitted.Straighten();
                    MoveTo(VerticesOf(fitted)[gripIndex], own);

                    if (type == DxfPoly2dType.FitCurvePoly)
                    {
                        fitted.CurveFit();
                    }
                    else
                    {
                        fitted.SplineFit();
                    }

                    return true;
                });
            }

            if (grip.Kind == CadGripKind.Vertex)
            {
                return Reshape(document, handle, property, was, went, () =>
                {
                    MoveTo(VerticesOf((DxfPolyline2d)document.FindEntity(handle))[gripIndex], own);
                    return true;
                });
            }

            int segment = gripIndex - n;
            int next = (segment + 1) % n;
            GeoPoint2 a = At(vertices[segment]);
            GeoPoint2 b = At(vertices[next]);

            if (Math.Abs(vertices[segment].Bulge) < 1E-12)
            {
                return Reshape(document, handle, property, was, went, () =>
                {
                    List<DxfVertex2d> now = VerticesOf((DxfPolyline2d)document.FindEntity(handle));
                    MoveTo(now[segment], a + shifted);
                    MoveTo(now[next], b + shifted);
                    return true;
                });
            }

            if (!BulgeThrough(a, own, b, out double bent))
            {
                return CadEditResult.Refuse("The three points of the arc lie in a line.");
            }

            return Reshape(document, handle, property, was, went, () =>
            {
                VerticesOf((DxfPolyline2d)document.FindEntity(handle))[segment].Bulge = bent;
                return true;
            });
        }

        /// <summary>Finds the POLYLINE and the grip of an edit, refusing what the edits cannot be made on.</summary>
        /// <returns>Null where the edit may go on, else the refusal.</returns>
        private static CadEditResult Polyline2dGrip(
            CadDocument document,
            string handle,
            int gripIndex,
            bool vertexOnly,
            out DxfPolyline2d heavy,
            out CadGrip grip
        )
        {
            heavy = (DxfPolyline2d)document.FindEntity(handle);
            grip = null;

            if (CadLayerEditor.IsLocked(document.Database, heavy.Layer))
            {
                return CadEditResult.Refuse("The object is on a locked layer.");
            }

            if (heavy.PolyType != DxfPoly2dType.SimplePoly)
            {
                return CadEditResult.Refuse(
                    "A polyline fitted with a curve or a spline has its frame to stretch, no vertices to add, take out or bend."
                );
            }

            IReadOnlyList<CadGrip> grips = Of(heavy);

            if (gripIndex < 0 || gripIndex >= grips.Count)
            {
                return CadEditResult.Refuse("The polyline has no grip " + gripIndex + ".");
            }

            grip = grips[gripIndex];
            return vertexOnly && grip.Kind != CadGripKind.Vertex
                ? CadEditResult.Refuse("Only a vertex can be taken out.")
                : null;
        }

        private static CadEditResult AddVertex2d(CadDocument document, string handle, int gripIndex, GeoPoint2 point)
        {
            CadEditResult refused = Polyline2dGrip(
                document,
                handle,
                gripIndex,
                false,
                out DxfPolyline2d heavy,
                out CadGrip grip
            );

            if (refused != null)
            {
                return refused;
            }

            GeoPoint2 own = Own(point, heavy.Normal.Z < 0.0);
            int after = grip.Kind == CadGripKind.Vertex ? gripIndex : gripIndex - VerticesOf(heavy).Count;
            return Reshape(
                document,
                handle,
                "Add vertex",
                CadCommands.Values(grip.Point.X, grip.Point.Y),
                CadCommands.Values(point.X, point.Y),
                () => InsertVertexAfter((DxfPolyline2d)document.FindEntity(handle), after, own)
            );
        }

        private static CadEditResult RemoveVertex2d(CadDocument document, string handle, int gripIndex)
        {
            CadEditResult refused = Polyline2dGrip(
                document,
                handle,
                gripIndex,
                true,
                out DxfPolyline2d heavy,
                out CadGrip grip
            );

            if (refused != null)
            {
                return refused;
            }

            if (VerticesOf(heavy).Count <= 2)
            {
                return CadEditResult.Refuse("A polyline keeps two vertices at least.");
            }

            return Reshape(
                document,
                handle,
                "Remove vertex",
                CadCommands.Values(grip.Point.X, grip.Point.Y),
                null,
                () =>
                {
                    var p = (DxfPolyline2d)document.FindEntity(handle);
                    List<DxfVertex2d> vertices = VerticesOf(p);
                    int before =
                        gripIndex > 0 ? gripIndex - 1
                        : p.Closed ? vertices.Count - 1
                        : -1;

                    if (before >= 0 && Math.Abs(vertices[before].Bulge) >= 1E-12)
                    {
                        vertices[before].Bulge = 0.0;
                    }

                    vertices[gripIndex].Erase();
                    return true;
                }
            );
        }

        private static CadEditResult ConvertToArc2d(CadDocument document, string handle, int gripIndex, GeoPoint2 point)
        {
            CadEditResult refused = Polyline2dGrip(
                document,
                handle,
                gripIndex,
                false,
                out DxfPolyline2d heavy,
                out CadGrip grip
            );

            if (refused != null)
            {
                return refused;
            }

            if (grip.Kind != CadGripKind.SegmentMiddle)
            {
                return CadEditResult.Refuse("Only the middle of a segment converts to an arc.");
            }

            List<DxfVertex2d> vertices = VerticesOf(heavy);
            int segment = gripIndex - vertices.Count;

            if (Math.Abs(vertices[segment].Bulge) >= 1E-12)
            {
                return CadEditResult.Refuse("The segment is an arc already.");
            }

            if (!BendInto(heavy.Clone() as DxfPolyline2d, segment, Own(point, heavy.Normal.Z < 0.0)))
            {
                return CadEditResult.Refuse("The three points of the arc lie in a line.");
            }

            return Reshape(
                document,
                handle,
                "Convert to arc",
                CadCommands.Values(grip.Point.X, grip.Point.Y),
                CadCommands.Values(point.X, point.Y),
                () => BendInto((DxfPolyline2d)document.FindEntity(handle), segment, Own(point, heavy.Normal.Z < 0.0))
            );
        }

        private static CadEditResult ConvertToLine2d(CadDocument document, string handle, int gripIndex)
        {
            CadEditResult refused = Polyline2dGrip(
                document,
                handle,
                gripIndex,
                false,
                out DxfPolyline2d heavy,
                out CadGrip grip
            );

            if (refused != null)
            {
                return refused;
            }

            List<DxfVertex2d> vertices = VerticesOf(heavy);
            int segment = gripIndex - vertices.Count;

            if (grip.Kind != CadGripKind.SegmentMiddle || Math.Abs(vertices[segment].Bulge) < 1E-12)
            {
                return CadEditResult.Refuse("Only the middle of an arc segment converts to a line.");
            }

            return Reshape(
                document,
                handle,
                "Convert to line",
                CadCommands.Values(grip.Point.X, grip.Point.Y),
                null,
                () =>
                {
                    VerticesOf((DxfPolyline2d)document.FindEntity(handle))[segment].Bulge = 0.0;
                    return true;
                }
            );
        }

        /// <summary>
        /// Puts a vertex after a vertex of a POLYLINE, in its own plane, as <see cref="AddVertex"/> says of a light polyline's;
        /// also what its prompt previews on a copy.
        /// </summary>
        internal static bool InsertVertexAfter(DxfPolyline2d heavy, int after, GeoPoint2 own)
        {
            List<DxfVertex2d> vertices = VerticesOf(heavy);
            int n = vertices.Count;

            if (after < 0 || after >= n || !own.IsValid)
            {
                return false;
            }

            double z = vertices[after].Position.Z;

            if (after >= SegmentsOf(heavy, n))
            {
                heavy.AppendVertex(new DxfVertex2d(new GeoPoint3(own.X, own.Y, z), 0.0, 0.0, 0.0, 0.0));
                return true;
            }

            DxfVertex2d from = vertices[after];
            GeoPoint2 a = At(from);
            GeoPoint2 b = At(vertices[(after + 1) % n]);
            double endWidth = from.EndWidth;

            // The width at the point is where its foot falls along the segment.
            GeoVector2 chord = b - a;
            double square = chord.X * chord.X + chord.Y * chord.Y;
            double along = square > 0.0 ? ((own.X - a.X) * chord.X + (own.Y - a.Y) * chord.Y) / square : 0.0;
            double width = from.StartWidth + (endWidth - from.StartWidth) * Math.Min(1.0, Math.Max(0.0, along));

            double first = 0.0;
            double second = 0.0;

            if (Math.Abs(from.Bulge) >= 1E-12)
            {
                SplitBulge(a, own, b, out first, out second);
            }

            from.Bulge = first;

            if (Math.Abs(width - endWidth) > 1E-12)
            {
                from.EndWidth = width;
            }

            heavy.InsertVertexAt(from, new DxfVertex2d(new GeoPoint3(own.X, own.Y, z), second, width, endWidth, 0.0));
            return true;
        }

        /// <summary>
        /// Bends a segment of a POLYLINE through a point, in its own plane, as <see cref="ConvertToArc"/> says of a light
        /// polyline's; also what its prompt previews on a copy.
        /// </summary>
        internal static bool BendInto(DxfPolyline2d heavy, int segment, GeoPoint2 own)
        {
            List<DxfVertex2d> vertices = VerticesOf(heavy);
            int n = vertices.Count;

            if (
                segment < 0
                || segment >= SegmentsOf(heavy, n)
                || Math.Abs(vertices[segment].Bulge) >= 1E-12
                || !own.IsValid
                || !BulgeThrough(At(vertices[segment]), own, At(vertices[(segment + 1) % n]), out double bulge)
            )
            {
                return false;
            }

            vertices[segment].Bulge = bulge;
            return true;
        }
    }
}
