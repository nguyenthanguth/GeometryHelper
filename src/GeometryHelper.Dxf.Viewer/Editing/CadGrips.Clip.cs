using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The grips on the clip boundary of a block reference (XCLIP), as AutoCAD 2021's <c>GetGripPoints</c> gives them.</summary>
    public static partial class CadGrips
    {
        /// <summary>The boundary of a clip as its grips see it: the corners of a rectangle or the vertices of a polygon, in the clip coordinate system.</summary>
        private sealed class ClipShape
        {
            public DxfSpatialFilter Filter;

            /// <summary>The corners of a rectangle from the least x and y, counter-clockwise, or the vertices of a polygon in order.</summary>
            public List<GeoPoint2> Vertices;

            public bool IsRectangle;

            /// <summary>Whether the clip is turned inside out: kept as a keyhole in the filter.</summary>
            public bool IsInverted;

            /// <summary>The boundary as the filter keeps it before it is inverted: two corners for a rectangle, else the polygon.</summary>
            public List<GeoPoint2> Boundary
            {
                get
                {
                    if (IsRectangle)
                    {
                        return new List<GeoPoint2> { Vertices[0], Vertices[2] };
                    }

                    return Vertices;
                }
            }
        }

        /// <summary>Reads the clip boundary of a reference; null where it has none or the boundary has too few points.</summary>
        private static ClipShape ReadClip(DxfBlockReference insert)
        {
            DxfSpatialFilter filter = insert.DrawnFilter;

            if (filter == null)
            {
                return null;
            }

            IReadOnlyList<GeoPoint2> points = filter.Points;
            var shape = new ClipShape { Filter = filter };
            double tolerance = Tolerance.Default.EqualPoint;

            if (points.Count == 2)
            {
                shape.IsRectangle = true;
                shape.Vertices = Corners(points[0], points[1]);
            }
            else if (CadXClips.TryUninvert(points, out IReadOnlyList<GeoPoint2> polygon))
            {
                shape.IsInverted = true;
                IReadOnlyList<GeoPoint2> corners = CadXClips.AsRectangle(polygon, tolerance);
                shape.IsRectangle = corners.Count == 2;
                shape.Vertices = shape.IsRectangle ? Corners(corners[0], corners[1]) : new List<GeoPoint2>(polygon);
            }
            else
            {
                shape.Vertices = new List<GeoPoint2>(points);
            }

            return shape.Vertices.Count >= 3 ? shape : null;
        }

        /// <summary>The four corners of the rectangle between two, from the least x and y round counter-clockwise.</summary>
        private static List<GeoPoint2> Corners(GeoPoint2 a, GeoPoint2 b)
        {
            double x0 = Math.Min(a.X, b.X);
            double x1 = Math.Max(a.X, b.X);
            double y0 = Math.Min(a.Y, b.Y);
            double y1 = Math.Max(a.Y, b.Y);
            return new List<GeoPoint2>
            {
                new GeoPoint2(x0, y0),
                new GeoPoint2(x1, y0),
                new GeoPoint2(x1, y1),
                new GeoPoint2(x0, y1),
            };
        }

        /// <summary>
        /// The grips of a clipped reference: the arrow at the middle of the first side of the boundary, pointing across it to
        /// what the clip hides (outwards for the part outside the boundary, inwards for a clip turned inside out), then the
        /// corners or vertices in order. Nothing for a reference that is not clipped.
        /// </summary>
        private static void ClipGrips(DxfBlockReference insert, Action<CadGripKind, GeoPoint2, GeoVector2> add)
        {
            ClipShape shape = ReadClip(insert);

            if (shape == null)
            {
                return;
            }

            var world = new List<GeoPoint2>(shape.Vertices.Count);
            double area = 0.0;

            for (int i = 0; i < shape.Vertices.Count; i++)
            {
                GeoPoint3 point = insert.ClipPointInWorld(shape.Filter, shape.Vertices[i]);
                world.Add(new GeoPoint2(point.X, point.Y));
            }

            for (int i = 0, j = world.Count - 1; i < world.Count; j = i++)
            {
                area += world[j].X * world[i].Y - world[i].X * world[j].Y;
            }

            GeoVector2 side = world[1] - world[0];
            double length = side.Length;
            var middle = new GeoPoint2((world[0].X + world[1].X) / 2.0, (world[0].Y + world[1].Y) / 2.0);
            GeoVector2 across = default;

            if (length > 0.0)
            {
                // The right of the way along the first side is outside a polygon that runs counter-clockwise, the left inside it.
                double turn = (area > 0.0) != shape.IsInverted ? 1.0 : -1.0;
                across = new GeoVector2(turn * side.Y / length, -turn * side.X / length);
            }

            add(CadGripKind.ClipFlip, middle, across);

            foreach (GeoPoint2 point in world)
            {
                add(CadGripKind.ClipVertex, point, default);
            }
        }

        /// <summary>
        /// Moves a corner or vertex of a clip boundary to a point of the drawing, as AutoCAD 2021's <c>MoveGripPointsAt</c> does
        /// (probes GHXE3): a rectangle's corner takes its two sides, the opposite corner staying, and the rectangle is kept in
        /// order where the corner crosses it; a polygon's vertex alone moves. A clip turned inside out stays so, its keyhole
        /// made anew about the box that holds the reference. A boundary of no area is refused.
        /// </summary>
        private static CadEditResult DragClipVertex(
            CadDocument document,
            DxfBlockReference insert,
            string handle,
            string property,
            string was,
            string went,
            int vertex,
            GeoPoint2 to
        )
        {
            ClipShape shape = ReadClip(insert);

            if (shape == null || vertex < 0 || vertex >= shape.Vertices.Count)
            {
                return CadEditResult.Refuse("The reference has no clip boundary.");
            }

            // The point dragged to lies in the plan at the height the grip stands at.
            GeoPoint3 now = insert.ClipPointInWorld(shape.Filter, shape.Vertices[vertex]);

            if (!insert.TryClipPointOfWorld(shape.Filter, new GeoPoint3(to.X, to.Y, now.Z), out GeoPoint2 moved))
            {
                return CadEditResult.Refuse("The clip boundary cannot be moved in this plane.");
            }

            double least = Tolerance.Default.EqualPoint;
            List<GeoPoint2> boundary;

            if (shape.IsRectangle)
            {
                GeoPoint2 low = shape.Vertices[0];
                GeoPoint2 high = shape.Vertices[2];
                double x0 = vertex == 0 || vertex == 3 ? moved.X : low.X;
                double x1 = vertex == 1 || vertex == 2 ? moved.X : high.X;
                double y0 = vertex == 0 || vertex == 1 ? moved.Y : low.Y;
                double y1 = vertex == 2 || vertex == 3 ? moved.Y : high.Y;

                if (Math.Abs(x1 - x0) <= least || Math.Abs(y1 - y0) <= least)
                {
                    return CadEditResult.Refuse("Boundaries with zero area are not acceptable.");
                }

                boundary = new List<GeoPoint2>
                {
                    new GeoPoint2(Math.Min(x0, x1), Math.Min(y0, y1)),
                    new GeoPoint2(Math.Max(x0, x1), Math.Max(y0, y1)),
                };
            }
            else
            {
                boundary = new List<GeoPoint2>(shape.Vertices) { [vertex] = moved };
                double area = 0.0;

                for (int i = 0, j = boundary.Count - 1; i < boundary.Count; j = i++)
                {
                    area += boundary[j].X * boundary[i].Y - boundary[i].X * boundary[j].Y;
                }

                if (Math.Abs(area / 2.0) <= least)
                {
                    return CadEditResult.Refuse("Boundaries with zero area are not acceptable.");
                }
            }

            return Reshape(document, handle, property, was, went, () =>
            {
                var written = (DxfBlockReference)document.FindEntity(handle);
                DxfSpatialFilter filter = written.SpatialFilter;
                filter.SetPoints(shape.IsInverted ? Keyhole(written, filter, boundary) : boundary);
                return true;
            });
        }

        /// <summary>
        /// A boundary turned inside out: its polygon with the slit and the box that holds the reference, as
        /// <see cref="CadXClips.Inverted"/> makes it, the box taken from the reference's extents as it stands, carried back to
        /// where the clip system found it.
        /// </summary>
        private static IReadOnlyList<GeoPoint2> Keyhole(
            DxfBlockReference insert,
            DxfSpatialFilter filter,
            IReadOnlyList<GeoPoint2> boundary
        )
        {
            double minX = double.MaxValue,
                minY = double.MaxValue,
                maxX = double.MinValue,
                maxY = double.MinValue;
            GeoAabb3 box = insert.GeometricExtents;

            if (!box.IsEmpty)
            {
                GeoPoint3[] corners =
                {
                    new GeoPoint3(box.Min.X, box.Min.Y, 0.0),
                    new GeoPoint3(box.Max.X, box.Min.Y, 0.0),
                    new GeoPoint3(box.Max.X, box.Max.Y, 0.0),
                    new GeoPoint3(box.Min.X, box.Max.Y, 0.0),
                };

                foreach (GeoPoint3 corner in corners)
                {
                    if (insert.TryClipPointOfWorld(filter, corner, out GeoPoint2 inClip))
                    {
                        minX = Math.Min(minX, inClip.X);
                        maxX = Math.Max(maxX, inClip.X);
                        minY = Math.Min(minY, inClip.Y);
                        maxY = Math.Max(maxY, inClip.Y);
                    }
                }
            }

            // No extents, or none that could be carried: the boundary's own box.
            if (minX > maxX)
            {
                foreach (GeoPoint2 point in boundary)
                {
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }
            }

            return CadXClips.Inverted(boundary, new GeoPoint2(minX, minY), new GeoPoint2(maxX, maxY));
        }

        /// <summary>
        /// Clicks the arrow of a clipped reference, as AutoCAD 2021's clip frame does: the clip is turned inside out, so that
        /// what the boundary held is hidden and what lay outside it shown, or back; one undo step through
        /// <see cref="CadDocument.ApplyEdit"/>. A clip turned inside out is kept as a keyhole in the boundary, a rectangle kept
        /// as two corners again when it is turned back.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the block reference.</param>
        /// <param name="gripIndex">The index of the arrow, as <see cref="Of"/> gives it.</param>
        /// <returns>What came of it: refused for an entity on a locked layer, or a grip that is not the arrow of a clip.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult ClickClipFlip(CadDocument document, string handle, int gripIndex)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!(document.FindEntity(handle) is DxfBlockReference insert))
            {
                return CadEditResult.Refuse("The drawing has no block reference " + handle + ".");
            }

            if (CadLayerEditor.IsLocked(document.Database, insert.Layer))
            {
                return CadEditResult.Refuse("The object is on a locked layer.");
            }

            IReadOnlyList<CadGrip> grips = Of(insert);

            if (gripIndex < 0 || gripIndex >= grips.Count || grips[gripIndex].Kind != CadGripKind.ClipFlip)
            {
                return CadEditResult.Refuse("The grip is not the arrow of a clip boundary.");
            }

            ClipShape shape = ReadClip(insert);
            bool inverted = shape.IsInverted;
            List<GeoPoint2> inner = shape.Boundary;
            return Reshape(
                document,
                handle,
                "Grip " + gripIndex,
                inverted ? "Inside mode" : "Outside mode",
                inverted ? "Outside mode" : "Inside mode",
                () =>
                {
                    var written = (DxfBlockReference)document.FindEntity(handle);
                    DxfSpatialFilter filter = written.SpatialFilter;
                    filter.SetPoints(inverted ? inner : Keyhole(written, filter, inner));
                    return true;
                }
            );
        }
    }
}
