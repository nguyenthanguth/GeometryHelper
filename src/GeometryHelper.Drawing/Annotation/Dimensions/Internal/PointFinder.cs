using System;
using System.Collections.Generic;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// Finds the points of interest of a view, object by object: the corners of each body the view's straight lines end at,
    /// the centres of its whole circles, the axes of bolts and holes, and the ends of parts.
    /// </summary>
    /// <remarks>
    /// A point of the view is tied to the model through the body's own corners: of the corners that fall on it in the view,
    /// the one nearest the viewer is its model point. A line of the view that ends where something in front hides it ends
    /// at no corner, and gives no point.
    /// </remarks>
    internal static class PointFinder
    {
        private const double FullTurn = 2.0 * Math.PI;

        /// <summary>The points of one object of the view.</summary>
        internal sealed class ObjectPoints
        {
            public ObjectPoints(DrawingObject drawingObject)
            {
                Object = drawingObject;
            }

            public DrawingObject Object { get; }

            public List<DimensionPoint> Corners { get; } = new List<DimensionPoint>();

            /// <summary>The centres of the holes and bolts of the object.</summary>
            public List<DimensionPoint> Centres { get; } = new List<DimensionPoint>();

            /// <summary>The ends of a part's reference line, its start first.</summary>
            public List<DimensionPoint> Ends { get; } = new List<DimensionPoint>();

            /// <summary>The box of the object in the view, from its lines; empty where it has none.</summary>
            public double MinX { get; set; } = double.PositiveInfinity;

            public double MinY { get; set; } = double.PositiveInfinity;

            public double MaxX { get; set; } = double.NegativeInfinity;

            public double MaxY { get; set; } = double.NegativeInfinity;

            public bool Contains(GeoPoint2 p, double tol) => p.X >= MinX - tol && p.X <= MaxX + tol && p.Y >= MinY - tol && p.Y <= MaxY + tol;

            public double Area => (MaxX - MinX) * (MaxY - MinY);
        }

        /// <summary>A corner of a body taken into the view.</summary>
        private struct Seen
        {
            public double X;
            public double Y;
            public double Depth;
            public GeoPoint3 Model;
        }

        /// <summary>
        /// The points of every object that has a line in the view, in the order of the objects' ids; each object's points
        /// in the order of their kinds, then of X, then of Y.
        /// </summary>
        internal static List<ObjectPoints> Find(ProjectedView2 view, IEnumerable<DrawingObject> objects, GeoCoordinateSystem3 frame, Tolerance tolerance)
        {
            double tol = tolerance.EqualPoint;
            var sorted = new List<DrawingObject>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (DrawingObject item in objects)
            {
                if (item == null)
                {
                    throw new ArgumentException("An object of a view cannot be null.", nameof(objects));
                }

                if (!seenIds.Add(item.Id))
                {
                    throw new ArgumentException("The id \"" + item.Id + "\" is given to two objects.", nameof(objects));
                }

                sorted.Add(item);
            }

            sorted.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            // The lines of each object, in the view's own order: the seen ones, then the hidden ones.
            var linesOf = new Dictionary<string, List<ProjectedEdge2>>(StringComparer.Ordinal);

            foreach (IReadOnlyList<ProjectedEdge2> list in new[] { view.VisibleEdges, view.HiddenEdges })
            {
                foreach (ProjectedEdge2 edge in list)
                {
                    foreach (string id in edge.ObjectIds)
                    {
                        if (!linesOf.TryGetValue(id, out List<ProjectedEdge2> lines))
                        {
                            linesOf.Add(id, lines = new List<ProjectedEdge2>());
                        }

                        lines.Add(edge);
                    }
                }
            }

            var found = new List<ObjectPoints>();

            foreach (DrawingObject item in sorted)
            {
                if (!linesOf.TryGetValue(item.Id, out List<ProjectedEdge2> lines))
                {
                    continue;
                }

                Seen[] corners = Corners(item.Solid, frame);
                var points = new ObjectPoints(item);
                AddCorners(points, lines, corners, tol);
                AddBox(points, lines);

                switch (item.Kind)
                {
                    case DrawingObjectKind.Part:
                        AddCircleCentres(points, lines, corners, frame, tol);
                        AddEnds(points, frame, tol);
                        break;
                    case DrawingObjectKind.Bolt:
                    case DrawingObjectKind.Hole:
                        AddAxisCentre(points, frame);
                        break;
                }

                points.Corners.Sort(CompareXY);
                points.Centres.Sort(CompareXY);
                found.Add(points);
            }

            return found;
        }

        /// <summary>Orders points by X, then Y, then kind.</summary>
        internal static int CompareXY(DimensionPoint a, DimensionPoint b)
        {
            int c = a.ViewPoint.X.CompareTo(b.ViewPoint.X);

            if (c != 0)
            {
                return c;
            }

            c = a.ViewPoint.Y.CompareTo(b.ViewPoint.Y);
            return c != 0 ? c : a.Kind.CompareTo(b.Kind);
        }

        /// <summary>Every corner of a body and of its openings, taken into the view, in the order of X, Y and depth.</summary>
        private static Seen[] Corners(GeoSolid3 solid, GeoCoordinateSystem3 frame)
        {
            var seen = new List<Seen>();
            AddCorners(solid, frame, seen);

            foreach (GeoSolid3 opening in solid.Openings)
            {
                AddCorners(opening, frame, seen);
            }

            Seen[] all = seen.ToArray();
            Array.Sort(all, (a, b) =>
            {
                int c = a.X.CompareTo(b.X);
                c = c != 0 ? c : a.Y.CompareTo(b.Y);
                c = c != 0 ? c : a.Depth.CompareTo(b.Depth);
                c = c != 0 ? c : a.Model.X.CompareTo(b.Model.X);
                c = c != 0 ? c : a.Model.Y.CompareTo(b.Model.Y);
                return c != 0 ? c : a.Model.Z.CompareTo(b.Model.Z);
            });

            return all;
        }

        private static void AddCorners(GeoSolid3 solid, GeoCoordinateSystem3 frame, List<Seen> seen)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                Add(face.Boundary, frame, seen);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    Add(hole, frame, seen);
                }
            }
        }

        private static void Add(GeoPolygon3 polygon, GeoCoordinateSystem3 frame, List<Seen> seen)
        {
            foreach (GeoPoint3 vertex in polygon.Vertices)
            {
                GeoPoint3 local = frame.ToLocal(vertex);
                seen.Add(new Seen { X = local.X, Y = local.Y, Depth = local.Z, Model = vertex });
            }
        }

        /// <summary>
        /// The corner of the body nearest the viewer of those within the point tolerance of a point of the view; false where
        /// none is.
        /// </summary>
        private static bool TryCorner(Seen[] corners, double x, double y, double tol, out Seen best)
        {
            best = default;
            bool any = false;
            int i = LowerBound(corners, x - tol);

            for (; i < corners.Length && corners[i].X <= x + tol; i++)
            {
                if (Math.Abs(corners[i].Y - y) > tol)
                {
                    continue;
                }

                // The corners are in the order of X, Y and depth, so the first deepest is kept and the result is the same
                // whatever order the faces came in.
                if (!any || corners[i].Depth > best.Depth)
                {
                    best = corners[i];
                    any = true;
                }
            }

            return any;
        }

        private static int LowerBound(Seen[] corners, double x)
        {
            int lo = 0, hi = corners.Length;

            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;

                if (corners[mid].X < x)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }

        private static void AddCorners(ObjectPoints points, List<ProjectedEdge2> lines, Seen[] corners, double tol)
        {
            var found = new List<DimensionPoint>();

            foreach (ProjectedEdge2 edge in lines)
            {
                if (edge.IsArc)
                {
                    continue;
                }

                foreach (GeoPoint2 end in new[] { edge.StartPoint, edge.EndPoint })
                {
                    if (TryCorner(corners, end.X, end.Y, tol, out Seen corner))
                    {
                        found.Add(new DimensionPoint(points.Object.Id, DimensionPointKind.Corner, end, corner.Model));
                    }
                }
            }

            found.Sort(CompareXY);
            Distinct(found, points.Corners, tol);
        }

        /// <summary>Keeps the first of the points that lie within the tolerance of one another, the points in the order of X.</summary>
        private static void Distinct(List<DimensionPoint> found, List<DimensionPoint> kept, double tol)
        {
            int first = kept.Count;

            foreach (DimensionPoint p in found)
            {
                bool twice = false;

                for (int k = kept.Count - 1; k >= first && kept[k].ViewPoint.X >= p.ViewPoint.X - tol; k--)
                {
                    if (Math.Abs(kept[k].ViewPoint.Y - p.ViewPoint.Y) <= tol)
                    {
                        twice = true;
                        break;
                    }
                }

                if (!twice)
                {
                    kept.Add(p);
                }
            }
        }

        private static void AddBox(ObjectPoints points, List<ProjectedEdge2> lines)
        {
            foreach (ProjectedEdge2 edge in lines)
            {
                if (edge.IsArc)
                {
                    GeoArc2 arc = edge.Arc;
                    Grow(points, arc.Center.X - arc.Radius, arc.Center.Y - arc.Radius);
                    Grow(points, arc.Center.X + arc.Radius, arc.Center.Y + arc.Radius);
                }
                else
                {
                    Grow(points, edge.StartPoint.X, edge.StartPoint.Y);
                    Grow(points, edge.EndPoint.X, edge.EndPoint.Y);
                }
            }
        }

        private static void Grow(ObjectPoints points, double x, double y)
        {
            points.MinX = Math.Min(points.MinX, x);
            points.MinY = Math.Min(points.MinY, y);
            points.MaxX = Math.Max(points.MaxX, x);
            points.MaxY = Math.Max(points.MaxY, y);
        }

        /// <summary>
        /// The centre of each whole circle of a part, its arcs about one centre and of one radius adding up to a full turn
        /// counted as one; circles about one centre, as the two rims of a hole or the two sides of a tube's wall, give one.
        /// </summary>
        private static void AddCircleCentres(ObjectPoints points, List<ProjectedEdge2> lines, Seen[] corners, GeoCoordinateSystem3 frame, double tol)
        {
            var arcs = new List<GeoArc2>();

            foreach (ProjectedEdge2 edge in lines)
            {
                if (edge.IsArc)
                {
                    arcs.Add(edge.Arc);
                }
            }

            if (arcs.Count == 0)
            {
                return;
            }

            arcs.Sort((a, b) =>
            {
                int c = a.Center.X.CompareTo(b.Center.X);
                c = c != 0 ? c : a.Center.Y.CompareTo(b.Center.Y);
                c = c != 0 ? c : a.Radius.CompareTo(b.Radius);
                return c != 0 ? c : a.StartAngle.CompareTo(b.StartAngle);
            });

            var used = new bool[arcs.Count];
            var circles = new List<GeoArc2>();

            for (int i = 0; i < arcs.Count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                double turn = 0.0;

                for (int j = i; j < arcs.Count && arcs[j].Center.X <= arcs[i].Center.X + tol; j++)
                {
                    if (!used[j] && Math.Abs(arcs[j].Center.Y - arcs[i].Center.Y) <= tol && Math.Abs(arcs[j].Radius - arcs[i].Radius) <= tol)
                    {
                        used[j] = true;
                        turn += Math.Abs(arcs[j].SweptAngle);
                    }
                }

                if (turn >= FullTurn * (1.0 - 1E-9))
                {
                    circles.Add(arcs[i]);
                }
            }

            var found = new List<DimensionPoint>();

            foreach (GeoArc2 circle in circles)
            {
                double depth = RimDepth(corners, circle, tol);
                GeoPoint3 model = frame.ToGlobal(new GeoPoint3(circle.Center.X, circle.Center.Y, depth));
                found.Add(new DimensionPoint(points.Object.Id, DimensionPointKind.HoleCentre, circle.Center, model));
            }

            found.Sort(CompareXY);
            Distinct(found, points.Centres, tol);
        }

        /// <summary>
        /// The depth of the rim of a circle nearest the viewer: the deepest of the body's corners on the circle; the deepest
        /// of all its corners where none lies on it.
        /// </summary>
        private static double RimDepth(Seen[] corners, GeoArc2 circle, double tol)
        {
            double rim = double.NegativeInfinity;
            double any = double.NegativeInfinity;
            int i = LowerBound(corners, circle.Center.X - circle.Radius - 2.0 * tol);

            for (; i < corners.Length && corners[i].X <= circle.Center.X + circle.Radius + 2.0 * tol; i++)
            {
                double d = Math.Sqrt((corners[i].X - circle.Center.X) * (corners[i].X - circle.Center.X) + (corners[i].Y - circle.Center.Y) * (corners[i].Y - circle.Center.Y));

                if (Math.Abs(d - circle.Radius) <= 2.0 * tol)
                {
                    rim = Math.Max(rim, corners[i].Depth);
                }
            }

            if (!double.IsNegativeInfinity(rim))
            {
                return rim;
            }

            foreach (Seen corner in corners)
            {
                any = Math.Max(any, corner.Depth);
            }

            return double.IsNegativeInfinity(any) ? 0.0 : any;
        }

        /// <summary>The middle of a bolt's or a hole's axis, its frame's X, half way between the ends of its body.</summary>
        private static void AddAxisCentre(ObjectPoints points, GeoCoordinateSystem3 frame)
        {
            GeoCoordinateSystem3 own = points.Object.Frame;

            if (!Extent(points.Object.Solid, own, out double min, out double max))
            {
                return;
            }

            GeoPoint3 model = own.Origin.Add(own.XAxis.Multiply(0.5 * (min + max)));
            GeoPoint3 local = frame.ToLocal(model);
            DimensionPointKind kind = points.Object.Kind == DrawingObjectKind.Bolt ? DimensionPointKind.BoltCentre : DimensionPointKind.HoleCentre;
            points.Centres.Add(new DimensionPoint(points.Object.Id, kind, new GeoPoint2(local.X, local.Y), model));
        }

        /// <summary>The ends of a part's reference line: its frame's origin, and as far along its X as its body reaches.</summary>
        private static void AddEnds(ObjectPoints points, GeoCoordinateSystem3 frame, double tol)
        {
            GeoCoordinateSystem3 own = points.Object.Frame;
            DimensionPointKind kind = points.Object.IsMain ? DimensionPointKind.WorkPoint : DimensionPointKind.PartEnd;
            AddEnd(points, own.Origin, kind, frame);

            if (Extent(points.Object.Solid, own, out _, out double max) && max > tol)
            {
                AddEnd(points, own.Origin.Add(own.XAxis.Multiply(max)), kind, frame);
            }
        }

        private static void AddEnd(ObjectPoints points, GeoPoint3 model, DimensionPointKind kind, GeoCoordinateSystem3 frame)
        {
            GeoPoint3 local = frame.ToLocal(model);
            points.Ends.Add(new DimensionPoint(points.Object.Id, kind, new GeoPoint2(local.X, local.Y), model));
        }

        /// <summary>How far along a frame's X the corners of a body reach, from its origin.</summary>
        private static bool Extent(GeoSolid3 solid, GeoCoordinateSystem3 own, out double min, out double max)
        {
            min = double.PositiveInfinity;
            max = double.NegativeInfinity;

            foreach (GeoFace3 face in solid.Faces)
            {
                foreach (GeoPoint3 vertex in face.Boundary.Vertices)
                {
                    double along = own.XAxis.DotProduct(own.Origin.GetVectorTo(vertex));
                    min = Math.Min(min, along);
                    max = Math.Max(max, along);
                }
            }

            return min <= max;
        }
    }
}
