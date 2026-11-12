using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The edits of a polyline's hover menu: add a vertex, remove one, make a segment an arc or a line.</summary>
    public static partial class CadGrips
    {
        /// <summary>
        /// Adds a vertex to a polyline at a point, as the Add Vertex item of a polyline's grip menu does, one undo step: the new
        /// vertex goes after the vertex of the grip, or after the vertex the segment of a segment middle grip leaves from, so
        /// that it lies between that vertex and the next (after the last vertex of an open polyline it extends the end). The new
        /// segment is straight; the segment split keeps its widths, the width at the point being the one where the point falls
        /// along it. A straight segment splits in two straight ones; an arc segment in two arcs of the one arc through its start,
        /// the point and its end: a half circle from (0, 0) to (20, 0) given the point (10, -10) is two quarter circles, a bulge of
        /// 0.4142 each.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the polyline.</param>
        /// <param name="gripIndex">The index of a vertex grip or a segment middle grip, as <see cref="Of"/> gives it.</param>
        /// <param name="point">Where the vertex goes, in drawing units.</param>
        /// <returns>What came of it: refused for an object on a locked layer, no polyline or no such grip.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult AddVertex(CadDocument document, string handle, int gripIndex, GeoPoint2 point)
        {
            CadEditResult refused = PolylineGrip(
                document,
                handle,
                gripIndex,
                false,
                out DxfPolyline polyline,
                out CadGrip grip
            );

            if (refused != null)
            {
                return refused;
            }

            bool below = polyline.Normal.Z < 0.0;
            GeoPoint2 own = Own(point, below);
            int after = VertexAfter(polyline, grip, gripIndex);

            return Reshape(
                document,
                handle,
                "Add vertex",
                CadCommands.Values(grip.Point.X, grip.Point.Y),
                CadCommands.Values(point.X, point.Y),
                () => InsertVertexAfter((DxfPolyline)document.FindEntity(handle), after, own)
            );
        }

        /// <summary>
        /// Takes a vertex out of a polyline, as the Remove Vertex item does, one undo step: the segment that now joins its
        /// two neighbours is straight, even where the segments either side of the vertex were arcs. A polyline of two vertices
        /// keeps them, as AutoCAD keeps them; a closed one of three leaves two.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the polyline.</param>
        /// <param name="gripIndex">The index of the vertex grip, as <see cref="Of"/> gives it.</param>
        /// <returns>What came of it: refused for an object on a locked layer, a grip that is not a vertex, or a polyline of two vertices.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult RemoveVertex(CadDocument document, string handle, int gripIndex)
        {
            CadEditResult refused = PolylineGrip(
                document,
                handle,
                gripIndex,
                true,
                out DxfPolyline polyline,
                out CadGrip grip
            );

            if (refused != null)
            {
                return refused;
            }

            if (polyline.NumberOfVertices <= 2)
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
                    var p = (DxfPolyline)document.FindEntity(handle);
                    int before =
                        gripIndex > 0 ? gripIndex - 1
                        : p.Closed ? p.NumberOfVertices - 1
                        : -1;

                    if (before >= 0 && Math.Abs(p.GetBulgeAt(before)) >= 1E-12)
                    {
                        p.SetBulgeAt(before, 0.0);
                    }

                    p.RemoveVertexAt(gripIndex);
                    return true;
                }
            );
        }

        /// <summary>
        /// Makes a straight segment of a polyline an arc through a point, as the Convert to Arc item does, one undo step: the
        /// arc from the segment's start through the point to its end. A chord of 100 with the point 20 off its middle is a bulge
        /// of 0.4 (the sagitta over half the chord), above nought where the point is to the right of the way the segment runs.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the polyline.</param>
        /// <param name="gripIndex">The index of the segment middle grip, as <see cref="Of"/> gives it.</param>
        /// <param name="point">The point the arc passes through, in drawing units.</param>
        /// <returns>What came of it: refused for an object on a locked layer, a grip that is not the middle of a straight segment, or a point in line with the segment.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult ConvertToArc(CadDocument document, string handle, int gripIndex, GeoPoint2 point)
        {
            CadEditResult refused = PolylineGrip(
                document,
                handle,
                gripIndex,
                false,
                out DxfPolyline polyline,
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

            int segment = gripIndex - polyline.NumberOfVertices;

            if (Math.Abs(polyline.GetBulgeAt(segment)) >= 1E-12)
            {
                return CadEditResult.Refuse("The segment is an arc already.");
            }

            GeoPoint2 own = Own(point, polyline.Normal.Z < 0.0);

            if (!BendSegment(polyline, segment, own, out double bulge))
            {
                return CadEditResult.Refuse("The three points of the arc lie in a line.");
            }

            return Reshape(
                document,
                handle,
                "Convert to arc",
                CadCommands.Values(grip.Point.X, grip.Point.Y),
                CadCommands.Values(point.X, point.Y),
                () =>
                {
                    ((DxfPolyline)document.FindEntity(handle)).SetBulgeAt(segment, bulge);
                    return true;
                }
            );
        }

        /// <summary>Makes an arc segment of a polyline straight, as the Convert to Line item does, one undo step.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the polyline.</param>
        /// <param name="gripIndex">The index of the segment middle grip, as <see cref="Of"/> gives it.</param>
        /// <returns>What came of it: refused for an object on a locked layer, or a grip that is not the middle of an arc segment.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult ConvertToLine(CadDocument document, string handle, int gripIndex)
        {
            CadEditResult refused = PolylineGrip(
                document,
                handle,
                gripIndex,
                false,
                out DxfPolyline polyline,
                out CadGrip grip
            );

            if (refused != null)
            {
                return refused;
            }

            int segment = gripIndex - polyline.NumberOfVertices;

            if (grip.Kind != CadGripKind.SegmentMiddle || Math.Abs(polyline.GetBulgeAt(segment)) < 1E-12)
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
                    ((DxfPolyline)document.FindEntity(handle)).SetBulgeAt(segment, 0.0);
                    return true;
                }
            );
        }

        /// <summary>Finds the polyline and the grip of an edit, refusing what the edits cannot be made on.</summary>
        /// <returns>Null where the edit may go on, else the refusal.</returns>
        private static CadEditResult PolylineGrip(
            CadDocument document,
            string handle,
            int gripIndex,
            bool vertexOnly,
            out DxfPolyline polyline,
            out CadGrip grip
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            polyline = document.FindEntity(handle) as DxfPolyline;
            grip = null;

            if (polyline == null)
            {
                return CadEditResult.Refuse("The drawing has no polyline " + handle + ".");
            }

            if (CadLayerEditor.IsLocked(document.Database, polyline.Layer))
            {
                return CadEditResult.Refuse("The object is on a locked layer.");
            }

            IReadOnlyList<CadGrip> grips = Of(polyline);

            if (gripIndex < 0 || gripIndex >= grips.Count)
            {
                return CadEditResult.Refuse("The polyline has no grip " + gripIndex + ".");
            }

            grip = grips[gripIndex];

            if (vertexOnly && grip.Kind != CadGripKind.Vertex)
            {
                return CadEditResult.Refuse("Only a vertex can be taken out.");
            }

            return null;
        }

        /// <summary>The vertex a new vertex goes after: the grip's own, or the one the segment of a segment middle leaves from.</summary>
        internal static int VertexAfter(DxfPolyline polyline, CadGrip grip, int gripIndex) =>
            grip.Kind == CadGripKind.Vertex ? gripIndex : gripIndex - polyline.NumberOfVertices;

        /// <summary>How many segments a polyline draws: one fewer than its vertices, and the closing one where it is closed on more than two.</summary>
        private static int SegmentsOf(DxfPolyline polyline) =>
            polyline.Closed && polyline.NumberOfVertices > 2
                ? polyline.NumberOfVertices
                : polyline.NumberOfVertices - 1;

        /// <summary>
        /// Puts a vertex after a vertex, in the polyline's own plane, as <see cref="AddVertex"/> says; also what its preview
        /// draws on a copy.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="after">The vertex it goes after.</param>
        /// <param name="own">The new vertex, in the polyline's own plane.</param>
        /// <returns>Whether it was put in.</returns>
        internal static bool InsertVertexAfter(DxfPolyline polyline, int after, GeoPoint2 own)
        {
            int n = polyline.NumberOfVertices;

            if (after < 0 || after >= n || !own.IsValid)
            {
                return false;
            }

            if (after >= SegmentsOf(polyline))
            {
                // After the last vertex of an open polyline, or of a closed one of two: the end is extended.
                polyline.AddVertexAt(n, own, 0.0, 0.0, 0.0);
                return true;
            }

            GeoPoint2 a = polyline.GetPoint2dAt(after);
            GeoPoint2 b = polyline.GetPoint2dAt((after + 1) % n);
            double bulge = polyline.GetBulgeAt(after);
            double startWidth = polyline.GetStartWidthAt(after);
            double endWidth = polyline.GetEndWidthAt(after);

            // The width at the point is where its foot falls along the segment.
            GeoVector2 chord = b - a;
            double square = chord.X * chord.X + chord.Y * chord.Y;
            double along = square > 0.0 ? ((own.X - a.X) * chord.X + (own.Y - a.Y) * chord.Y) / square : 0.0;
            double width = startWidth + (endWidth - startWidth) * Math.Min(1.0, Math.Max(0.0, along));

            double first = 0.0;
            double second = 0.0;

            if (Math.Abs(bulge) >= 1E-12)
            {
                SplitBulge(a, own, b, out first, out second);
            }

            polyline.SetBulgeAt(after, first);

            if (Math.Abs(width - endWidth) > 1E-12)
            {
                polyline.SetEndWidthAt(after, width);
            }

            polyline.AddVertexAt(after + 1, own, second, width, endWidth);
            return true;
        }

        /// <summary>
        /// Bends a segment through a point, in the polyline's own plane, as <see cref="ConvertToArc"/> says; also what its
        /// preview draws on a copy.
        /// </summary>
        /// <returns>Whether the segment is straight and the point is off the line of it.</returns>
        internal static bool BendInto(DxfPolyline polyline, int segment, GeoPoint2 own)
        {
            if (
                segment < 0
                || segment >= SegmentsOf(polyline)
                || Math.Abs(polyline.GetBulgeAt(segment)) >= 1E-12
                || !own.IsValid
                || !BendSegment(polyline, segment, own, out double bulge)
            )
            {
                return false;
            }

            polyline.SetBulgeAt(segment, bulge);
            return true;
        }

        /// <summary>A polyline with a vertex put after a vertex, for a point of the world: what the prompts of the menu items preview.</summary>
        internal static bool InsertVertexAfterAt(DxfEntity entity, int after, GeoPoint2 point) =>
            entity is DxfPolyline polyline && InsertVertexAfter(polyline, after, Own(point, polyline.Normal.Z < 0.0));

        /// <summary>A polyline with a segment bent through a point of the world: what the prompt of Convert to Arc previews.</summary>
        internal static bool BendIntoAt(DxfEntity entity, int segment, GeoPoint2 point) =>
            entity is DxfPolyline polyline && BendInto(polyline, segment, Own(point, polyline.Normal.Z < 0.0));

        private static bool BendSegment(DxfPolyline polyline, int segment, GeoPoint2 own, out double bulge)
        {
            int n = polyline.NumberOfVertices;
            return BulgeThrough(
                polyline.GetPoint2dAt(segment),
                own,
                polyline.GetPoint2dAt((segment + 1) % n),
                out bulge
            );
        }

        /// <summary>
        /// The bulges of the two parts of the arc from a through q to b, split at q: each part's quarter of its sweep as a
        /// tangent, above nought for the arc that runs counter-clockwise. Nought both where the three points lie in a line.
        /// </summary>
        private static void SplitBulge(GeoPoint2 a, GeoPoint2 q, GeoPoint2 b, out double first, out double second)
        {
            first = 0.0;
            second = 0.0;

            if (!CircleThrough(a, q, b, out GeoPoint2 center, out _))
            {
                return;
            }

            // Counter-clockwise where a, q, b turn left.
            double turn = (q.X - a.X) * (b.Y - q.Y) - (q.Y - a.Y) * (b.X - q.X);
            double side = turn > 0.0 ? 1.0 : -1.0;

            double Sweep(GeoPoint2 from, GeoPoint2 to)
            {
                double angle =
                    side
                    * (Math.Atan2(to.Y - center.Y, to.X - center.X) - Math.Atan2(from.Y - center.Y, from.X - center.X));
                angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
                return angle;
            }

            first = side * Math.Tan(Sweep(a, q) / 4.0);
            second = side * Math.Tan(Sweep(q, b) / 4.0);
        }
    }
}
