using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The grips of the boundary of a hatch that follows no objects (not associative), as AutoCAD 2021's GetGripPoints gives
    /// them after the grip of the middle: for each loop a grip on each vertex followed by one in the middle of the segment
    /// that leaves it, or the four quadrants of a loop that is one circle.
    /// </summary>
    public static partial class CadGrips
    {
        /// <summary>One loop of a hatch as its grips see it, in the plan: the points and bulges of a chain of lines and arcs, or a circle.</summary>
        internal sealed class HatchBoundary
        {
            /// <summary>The index of the loop in the hatch.</summary>
            internal int Loop { get; set; }

            /// <summary>What the loop is, as the hatch has it.</summary>
            internal DxfHatchLoopTypes Type { get; set; }

            /// <summary>Whether the loop is a polyline; else it is edges.</summary>
            internal bool IsPolyline { get; set; }

            /// <summary>Whether the loop is one whole circle.</summary>
            internal bool IsCircle { get; set; }

            internal GeoPoint2 Center { get; set; }

            internal double Radius { get; set; }

            internal double Elevation { get; set; }

            /// <summary>The vertices of a chain, closed back to the first.</summary>
            internal List<GeoPoint2> Points { get; } = new List<GeoPoint2>();

            /// <summary>The bulge of the segment that leaves each vertex, above nought where it runs counter-clockwise.</summary>
            internal List<double> Bulges { get; } = new List<double>();

            /// <summary>How many grips the loop has: a vertex and a middle for each segment, or the four quadrants.</summary>
            internal int GripCount => IsCircle ? 4 : 2 * Points.Count;
        }

        /// <summary>
        /// The loops of a hatch that have grips: none for a hatch that follows objects, is not drawn from above or has an edge
        /// that is not a line or an arc; the loops of such edges, an open polyline or an ellipse or a spline alone are left out.
        /// </summary>
        private static List<HatchBoundary> HatchBoundaries(DxfHatch hatch)
        {
            var boundaries = new List<HatchBoundary>();

            if (hatch.Associative || hatch.Normal.Z <= 0.0)
            {
                return boundaries;
            }

            for (int i = 0; i < hatch.NumberOfLoops; i++)
            {
                try
                {
                    HatchBoundary boundary = BoundaryOf(hatch.GetLoopAt(i), i);

                    if (boundary != null)
                    {
                        boundaries.Add(boundary);
                    }
                }
                catch (InvalidOperationException)
                {
                    // An edge that is no shape: the loop has no grips.
                }
            }

            return boundaries;
        }

        internal static HatchBoundary BoundaryOf(DxfHatchLoop loop, int index)
        {
            var boundary = new HatchBoundary
            {
                Loop = index,
                Type = loop.LoopType,
                IsPolyline = loop.IsPolyline,
            };
            var edges = new List<GeoEdge3>();

            if (loop.IsPolyline)
            {
                if (!(loop.Polyline is GeoPolygonArc3 polygon))
                {
                    return null;
                }

                for (int i = 0; i < polygon.EdgeCount; i++)
                {
                    edges.Add(polygon.GetEdgeAt(i));
                }
            }
            else if (loop.Curves.Count == 1 && loop.Curves[0] is GeoCircle3 circle)
            {
                if (circle.Normal.Z <= 0.0 || !IsPlan(circle.Normal))
                {
                    return null;
                }

                boundary.IsCircle = true;
                boundary.Center = new GeoPoint2(circle.Center.X, circle.Center.Y);
                boundary.Radius = circle.Radius;
                boundary.Elevation = circle.Center.Z;
                return boundary;
            }
            else
            {
                foreach (object curve in loop.Curves)
                {
                    switch (curve)
                    {
                        case GeoLine3 line:
                            edges.Add(new GeoEdge3(line.StartPoint, line.EndPoint));
                            break;
                        case GeoArc3 arc:
                            edges.Add(new GeoEdge3(arc));
                            break;
                        case GeoEdge3 edge:
                            edges.Add(edge);
                            break;
                        default:
                            return null;
                    }
                }
            }

            if (edges.Count < 2)
            {
                return null;
            }

            // The edges must run end to end and close, each arc counter-clockwise or clockwise seen from above.
            Tolerance tolerance = Tolerance.Default;

            for (int i = 0; i < edges.Count; i++)
            {
                GeoEdge3 edge = edges[i];

                if (!edge.EndPoint.IsEqualTo(edges[(i + 1) % edges.Count].StartPoint, tolerance))
                {
                    return null;
                }

                double bulge = 0.0;

                if (edge.Bulge != 0.0)
                {
                    if (!IsPlan(edge.Normal))
                    {
                        return null;
                    }

                    bulge = edge.Bulge * (edge.Normal.Z > 0.0 ? 1.0 : -1.0);
                }

                boundary.Points.Add(new GeoPoint2(edge.StartPoint.X, edge.StartPoint.Y));
                boundary.Bulges.Add(bulge);
            }

            boundary.Elevation = edges[0].StartPoint.Z;
            return boundary;
        }

        /// <summary>Adds the grips of a hatch's boundary in the order AutoCAD 2021 gives them; <paramref name="add"/> takes a kind and a point.</summary>
        private static void HatchBoundaryGrips(DxfHatch hatch, Action<CadGripKind, GeoPoint2> add)
        {
            foreach (HatchBoundary boundary in HatchBoundaries(hatch))
            {
                if (boundary.IsCircle)
                {
                    // East, west, north and south, as AutoCAD 2021 gives them.
                    GeoPoint2 c = boundary.Center;
                    double r = boundary.Radius;
                    add(CadGripKind.Quadrant, new GeoPoint2(c.X + r, c.Y));
                    add(CadGripKind.Quadrant, new GeoPoint2(c.X - r, c.Y));
                    add(CadGripKind.Quadrant, new GeoPoint2(c.X, c.Y + r));
                    add(CadGripKind.Quadrant, new GeoPoint2(c.X, c.Y - r));
                    continue;
                }

                int n = boundary.Points.Count;

                for (int i = 0; i < n; i++)
                {
                    GeoPoint2 from = boundary.Points[i];
                    add(CadGripKind.Vertex, from);
                    add(CadGripKind.Middle, SegmentMiddle(from, boundary.Points[(i + 1) % n], boundary.Bulges[i]));
                }
            }
        }

        /// <summary>
        /// Drags a grip of a hatch's boundary, as MoveGripPointsAt of AutoCAD 2021 does (probed in its console): a vertex moves
        /// alone, its bulges kept; the middle of a straight segment takes both its ends along; that of an arc segment bends the
        /// arc through the point; a quadrant of a circle makes its radius the distance to the point. The pattern is laid again
        /// in the new boundary, its origin kept. One undo step.
        /// </summary>
        private static CadEditResult DragHatchBoundary(
            CadDocument document,
            DxfHatch hatch,
            string handle,
            int gripIndex,
            CadGrip grip,
            GeoPoint2 to,
            string property,
            string was,
            string went
        )
        {
            int first = 1;
            DxfHatchLoop made = null;
            int loop = -1;

            foreach (HatchBoundary boundary in HatchBoundaries(hatch))
            {
                int local = gripIndex - first;
                first += boundary.GripCount;

                if (local < 0 || local >= boundary.GripCount)
                {
                    continue;
                }

                loop = boundary.Loop;
                int n = boundary.Points.Count;

                if (boundary.IsCircle)
                {
                    double radius = (to - boundary.Center).Length;

                    if (!(radius > 0.0))
                    {
                        return CadEditResult.Refuse("A circle needs a radius above nought.");
                    }

                    made = new DxfHatchLoop(
                        boundary.Type,
                        new object[]
                        {
                            new GeoCircle3(
                                new GeoPoint3(boundary.Center.X, boundary.Center.Y, boundary.Elevation),
                                new GeoVector3(0.0, 0.0, 1.0),
                                radius
                            ),
                        }
                    );
                    break;
                }

                int segment = local / 2;
                int next = (segment + 1) % n;
                var points = new List<GeoPoint2>(boundary.Points);
                var bulges = new List<double>(boundary.Bulges);

                if (local % 2 == 0)
                {
                    points[segment] = to;
                }
                else if (Math.Abs(bulges[segment]) < 1E-12)
                {
                    // A straight segment is moved whole, its two vertices with it, as AutoCAD's Stretch moves it.
                    GeoVector2 shifted = to - grip.Point;
                    points[segment] = points[segment] + shifted;
                    points[next] = points[next] + shifted;
                }
                else if (!BulgeThrough(points[segment], to, points[next], out double bent))
                {
                    return CadEditResult.Refuse("The three points of the arc lie in a line.");
                }
                else
                {
                    bulges[segment] = bent;
                }

                try
                {
                    made = HatchLoopOf(boundary, points, bulges);
                }
                catch (ArgumentException)
                {
                    return CadEditResult.Refuse("The boundary cannot be that shape: its vertices have to stay apart.");
                }

                break;
            }

            if (made == null)
            {
                return CadEditResult.Refuse("The grip cannot be dragged.");
            }

            return Reshape(document, handle, property, was, went, () =>
            {
                var edited = (DxfHatch)document.FindEntity(handle);
                edited.RemoveLoopAt(loop);
                edited.InsertLoopAt(loop, made);
                return true;
            });
        }

        /// <summary>The loop of a boundary with its points and bulges changed: a polyline as one, edges as lines and arcs.</summary>
        private static DxfHatchLoop HatchLoopOf(HatchBoundary boundary, List<GeoPoint2> points, List<double> bulges)
        {
            var up = new GeoVector3(0.0, 0.0, 1.0);
            var vertices = new List<GeoPoint3>(points.Count);

            foreach (GeoPoint2 point in points)
            {
                vertices.Add(new GeoPoint3(point.X, point.Y, boundary.Elevation));
            }

            if (boundary.IsPolyline)
            {
                var normals = new List<GeoVector3>(points.Count);

                foreach (double bulge in bulges)
                {
                    normals.Add(up);
                }

                return new DxfHatchLoop(boundary.Type, new GeoPolygonArc3(vertices, bulges, normals));
            }

            var edges = new List<object>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint3 end = vertices[(i + 1) % points.Count];
                edges.Add(
                    Math.Abs(bulges[i]) < 1E-12
                        ? new GeoEdge3(vertices[i], end)
                        : new GeoEdge3(vertices[i], end, bulges[i], up)
                );
            }

            return new DxfHatchLoop(boundary.Type, edges);
        }
    }
}
