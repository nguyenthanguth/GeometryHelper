using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Viewports clipped by an entity (VPCLIP): the outline a clipped viewport shows through, the entity and the viewport taken
    /// together when either is picked, and the viewport's frame fitted again when its entity is edited, as AutoCAD 2021 keeps
    /// them.
    /// </summary>
    public static class CadViewportClips
    {
        private const double SplineSegments = 128.0;
        private const double EllipseSegments = 180.0;

        /// <summary>
        /// Gets the outline a clipped viewport shows model space through: its clip boundary (<see cref="DxfViewport.NonRectClipEntity"/>)
        /// as a ring of paper units, a circle or the arcs of a polyline kept as arcs. Null for a viewport that is not clipped, and for
        /// one whose boundary has no outline the library reads, which is shown as the rectangle of its frame. A region gives a ring for
        /// each loop of each face, in the outline's <see cref="DisplayClip2.Others"/>.
        /// </summary>
        /// <param name="viewport">The viewport.</param>
        /// <returns>The outline, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public static DisplayClip2 OutlineOf(DxfViewport viewport)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            DxfEntity boundary = viewport.NonRectClipEntity;
            return boundary == null ? null : OutlineOfBoundary(boundary);
        }

        private static DisplayClip2 OutlineOfBoundary(DxfEntity boundary)
        {
            if (boundary is DxfUnknownEntity region)
            {
                return CachedOutlineOfRegion(region);
            }

            foreach (object shape in boundary.GetGeometry())
            {
                switch (shape)
                {
                    case GeoPolygonArc3 ring when ring.Vertices.Count >= 3:
                    {
                        int n = ring.Vertices.Count;
                        var xy = new double[2 * n];
                        var bulges = new double[n];

                        for (int i = 0; i < n; i++)
                        {
                            xy[2 * i] = ring.Vertices[i].X;
                            xy[2 * i + 1] = ring.Vertices[i].Y;
                            bulges[i] = ring.Normals[i].Z < 0.0 ? -ring.Bulges[i] : ring.Bulges[i];
                        }

                        return new DisplayClip2(xy, bulges);
                    }

                    case GeoPolygon3 polygon when polygon.Vertices.Count >= 3:
                        return Sampled(polygon.Vertices.Count, i => polygon.Vertices[i]);
                    case GeoCircle3 circle:
                        return new DisplayClip2(
                            new[]
                            {
                                circle.Center.X + circle.Radius,
                                circle.Center.Y,
                                circle.Center.X - circle.Radius,
                                circle.Center.Y,
                            },
                            new[] { 1.0, 1.0 }
                        );
                    case GeoEllipse3 ellipse:
                        return Sampled(180, i => ellipse.GetPointAtAngle(2.0 * Math.PI * i / 180.0));
                    case GeoSpline3 spline:
                        return Sampled((int)SplineSegments, i => spline.GetPointAtParameter(i / SplineSegments));
                }
            }

            return null;
        }

        // The outline of each region of a file, with the count of edits of the file it was read at: the canvas asks for it on every
        // paint of a clipped viewport, and reading the region's ACIS data again took 80 microseconds a call.
        private static readonly ConditionalWeakTable<DxfUnknownEntity, RegionOutline> RegionOutlines =
            new ConditionalWeakTable<DxfUnknownEntity, RegionOutline>();

        /// <summary>The outline of a region of a file, read again once the file has been edited; null where the region is not read.</summary>
        private static DisplayClip2 CachedOutlineOfRegion(DxfUnknownEntity region)
        {
            if (!region.IsResident)
            {
                return OutlineOfRegion(region);
            }

            int edits = region.Item.File.EditCount;
            RegionOutline kept = RegionOutlines.GetOrCreateValue(region);

            lock (kept)
            {
                if (!kept.Read || kept.Edits != edits)
                {
                    kept.Outline = OutlineOfRegion(region);
                    kept.Edits = edits;
                    kept.Read = true;
                }

                return kept.Outline;
            }
        }

        /// <summary>The outline of a region and the count of edits of its file it was read at.</summary>
        private sealed class RegionOutline
        {
            internal bool Read;
            internal int Edits;
            internal DisplayClip2 Outline;
        }

        /// <summary>
        /// The outline of a region: a ring for each loop of each face, a hole as a ring of its own, as the boundary a viewport
        /// shows model space through (AutoCAD 2021 plotted a ring of radius 40 and 15 with nothing in the hole, and two squares
        /// apart as two windows). Null where the region's shape is not read.
        /// </summary>
        private static DisplayClip2 OutlineOfRegion(DxfUnknownEntity region)
        {
            DxfRegionShape shape = region.GetRegionShape();
            var rings = new List<DisplayClip2>();

            if (shape != null)
            {
                foreach (DxfRegionLoop loop in shape.Loops)
                {
                    DisplayClip2 ring = RingOfLoop(loop);

                    if (ring != null)
                    {
                        rings.Add(ring);
                    }
                }
            }

            if (rings.Count == 0)
            {
                return null;
            }

            DisplayClip2 first = rings[0];
            rings.RemoveAt(0);

            if (rings.Count == 0)
            {
                return first;
            }

            var xy = new double[2 * first.Count];
            var bulges = new double[first.Count];

            for (int i = 0; i < first.Count; i++)
            {
                xy[2 * i] = first.X(i);
                xy[2 * i + 1] = first.Y(i);
                bulges[i] = first.Bulge(i);
            }

            return new DisplayClip2(xy, bulges, rings);
        }

        /// <summary>
        /// The ring of a loop of a region: its curves joined end to end the way the loop runs, a line a straight edge, an arc an edge
        /// with its bulge, a circle two half circles, a piece of an ellipse its points. The curves come as AutoCAD's Explode gives
        /// them, arcs counter-clockwise whichever way the loop goes, so each is turned to start where the one before it ends.
        /// </summary>
        private static DisplayClip2 RingOfLoop(DxfRegionLoop loop)
        {
            var edges = new List<RegionEdge>();

            foreach (DxfCurve curve in loop.Curves)
            {
                switch (curve)
                {
                    case DxfCircle circle:
                    {
                        GeoPoint3 c = circle.Center;
                        return new DisplayClip2(
                            new[] { c.X + circle.Radius, c.Y, c.X - circle.Radius, c.Y },
                            new[] { 1.0, 1.0 }
                        );
                    }

                    case DxfArc arc:
                        edges.Add(
                            new RegionEdge(
                                new[] { arc.StartPoint, arc.EndPoint },
                                (arc.Normal.Z < 0.0 ? -1.0 : 1.0) * Math.Tan(arc.TotalAngle / 4.0)
                            )
                        );
                        break;
                    case DxfEllipse ellipse:
                    {
                        double span = ellipse.EndParam - ellipse.StartParam;
                        int steps = Math.Max(8, (int)Math.Ceiling(Math.Abs(span) / (2.0 * Math.PI) * EllipseSegments));
                        var points = new GeoPoint3[steps + 1];

                        for (int i = 0; i <= steps; i++)
                        {
                            points[i] = ellipse.GetPointAtParameter(ellipse.StartParam + span * i / steps);
                        }

                        edges.Add(new RegionEdge(points, 0.0));
                        break;
                    }

                    default:
                        edges.Add(new RegionEdge(new[] { curve.StartPoint, curve.EndPoint }, 0.0));
                        break;
                }
            }

            if (edges.Count < 2)
            {
                return null;
            }

            // The first edge runs towards the second; each after it from where the one before ends.
            RegionEdge next = edges[1];
            double toNext = Math.Min(Gap(edges[0].Last, next.First), Gap(edges[0].Last, next.Last));
            double fromNext = Math.Min(Gap(edges[0].First, next.First), Gap(edges[0].First, next.Last));

            if (fromNext < toNext)
            {
                edges[0] = edges[0].Reversed();
            }

            for (int i = 1; i < edges.Count; i++)
            {
                if (Gap(edges[i - 1].Last, edges[i].First) > Gap(edges[i - 1].Last, edges[i].Last))
                {
                    edges[i] = edges[i].Reversed();
                }
            }

            var xy = new List<double>();
            var bulges = new List<double>();

            foreach (RegionEdge edge in edges)
            {
                for (int i = 0; i < edge.Points.Length - 1; i++)
                {
                    xy.Add(edge.Points[i].X);
                    xy.Add(edge.Points[i].Y);
                    bulges.Add(i == 0 ? edge.Bulge : 0.0);
                }
            }

            return bulges.Count < 2 ? null : new DisplayClip2(xy.ToArray(), bulges.ToArray());
        }

        private static double Gap(GeoPoint3 a, GeoPoint3 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        /// <summary>An edge of a region's loop: the points it runs through and the bulge of the first, as an arc has.</summary>
        private readonly struct RegionEdge
        {
            internal RegionEdge(GeoPoint3[] points, double bulge)
            {
                Points = points;
                Bulge = bulge;
            }

            internal GeoPoint3[] Points { get; }

            internal double Bulge { get; }

            internal GeoPoint3 First => Points[0];

            internal GeoPoint3 Last => Points[Points.Length - 1];

            internal RegionEdge Reversed() => new RegionEdge(Points.Reverse().ToArray(), -Bulge);
        }

        private static DisplayClip2 Sampled(int count, Func<int, GeoPoint3> point)
        {
            var xy = new double[2 * count];

            for (int i = 0; i < count; i++)
            {
                GeoPoint3 p = point(i);
                xy[2 * i] = p.X;
                xy[2 * i + 1] = p.Y;
            }

            return new DisplayClip2(xy, new double[count]);
        }

        /// <summary>
        /// Says why an entity cannot clip a viewport, in the words of AutoCAD 2021's VPCLIP: "Clip object must be closed." for one
        /// that is not a closed circle, ellipse, polyline or spline or a region, "Invalid: Clip entity not currently regen'ed." for a viewport or
        /// an entity of another layout; null where <see cref="DxfViewport.ClipTo"/> takes it.
        /// </summary>
        /// <param name="viewport">The viewport to clip.</param>
        /// <param name="candidate">The entity picked to clip it.</param>
        /// <returns>The reason, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport or the candidate is null.</exception>
        public static string WhyNotClip(DxfViewport viewport, DxfEntity candidate)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (candidate is DxfViewport || !ReferenceEquals(candidate.Owner, viewport.Owner))
            {
                return "Invalid: Clip entity not currently regen'ed.";
            }

            if (!DxfViewport.CanClip(candidate, out GeoAabb3 box))
            {
                return "Clip object must be closed.";
            }

            double least = Tolerance.Default.EqualPoint;
            return box.Max.X - box.Min.X > least && box.Max.Y - box.Min.Y > least
                ? null
                : "Clip object must be closed.";
        }

        /// <summary>
        /// Says why an entity cannot become a viewport by MVIEW's Object option, in the words of AutoCAD 2021: "Clip object must
        /// be closed." for a polyline, spline, ellipse or region that is not closed or has no area, "Invalid viewport clip object."
        /// for any other entity; null where it can, with the box of it, the frame the new viewport takes.
        /// </summary>
        /// <param name="candidate">The entity picked.</param>
        /// <param name="space">The layout the viewport is made in.</param>
        /// <param name="box">The box of the entity, where it can.</param>
        /// <returns>The reason, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the candidate or the space is null.</exception>
        public static string WhyNotMakeViewport(DxfEntity candidate, CadSpace space, out GeoAabb3 box)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            box = default;
            bool shape =
                candidate is DxfCircle
                || candidate is DxfEllipse
                || candidate is DxfPolyline
                || candidate is DxfPolyline2d
                || candidate is DxfSpline
                || (candidate is DxfUnknownEntity region && region.DxfName == "REGION");

            if (!shape || !ReferenceEquals(candidate.Owner, space.Block))
            {
                return "Invalid viewport clip object.";
            }

            if (!DxfViewport.CanClip(candidate, out box))
            {
                return "Clip object must be closed.";
            }

            double least = Tolerance.Default.EqualPoint;
            return box.Max.X - box.Min.X > least && box.Max.Y - box.Min.Y > least
                ? null
                : "Clip object must be closed.";
        }

        /// <summary>
        /// Gets the handles given with, for each viewport clipped by an entity among them, that entity, and for each entity that
        /// clips a viewport among them, that viewport, as AutoCAD takes the two for one object when either is picked ("2 found"):
        /// those given, then the partners in ordinal order of their handles, each once.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles picked.</param>
        /// <returns>The handles with their partners.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the handles are null.</exception>
        public static IReadOnlyList<string> WithPartners(CadDocument document, IEnumerable<string> handles)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            var given = new List<string>(handles);
            Dictionary<string, string> pairs = Pairs(document);

            if (pairs.Count == 0 || given.Count == 0)
            {
                return given;
            }

            var taken = new HashSet<string>(given, StringComparer.OrdinalIgnoreCase);
            var added = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in given)
            {
                if (pairs.TryGetValue(handle.Trim(), out string partner) && !taken.Contains(partner))
                {
                    added.Add(partner);
                }
            }

            given.AddRange(added);
            return given;
        }

        /// <summary>
        /// Gets the handles given with a clipped viewport's partner left out where the viewport or its boundary comes before it, as
        /// AutoCAD's pick takes the two for one object: the Selection list of a click on the boundary shows one row, not the
        /// boundary and the viewport under it. The order of the rest is kept.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles of the objects under a pick, the nearest first.</param>
        /// <returns>The handles, one for each pair.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the handles are null.</exception>
        public static IReadOnlyList<string> WithoutPartners(CadDocument document, IReadOnlyList<string> handles)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            Dictionary<string, string> pairs = Pairs(document);

            if (pairs.Count == 0 || handles.Count < 2)
            {
                return handles;
            }

            var kept = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in handles)
            {
                if (pairs.TryGetValue(handle.Trim(), out string partner) && seen.Contains(partner))
                {
                    continue;
                }

                kept.Add(handle);
                seen.Add(handle.Trim());
            }

            return kept;
        }

        /// <summary>The clipped viewports of the drawing and their boundaries: each handle of the one to the handle of the other.</summary>
        private static Dictionary<string, string> Pairs(CadDocument document)
        {
            var pairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfLayout layout in document.Database.Layouts)
            {
                if (layout.BlockTableRecord == null || layout.BlockTableRecord.IsErased)
                {
                    continue;
                }

                foreach (DxfViewport viewport in layout.GetViewports())
                {
                    DxfEntity boundary = viewport.IsErased ? null : viewport.NonRectClipEntity;

                    if (boundary != null && viewport.Handle != null && boundary.Handle != null)
                    {
                        pairs[viewport.Handle] = boundary.Handle;
                        pairs[boundary.Handle] = viewport.Handle;
                    }
                }
            }

            return pairs;
        }

        /// <summary>
        /// Fits the frame of every viewport whose clip boundary an edit has changed to the box of the boundary again, in the edit's
        /// own undo step, as AutoCAD moves a viewport with the boundary dragged: the records of the viewports it fitted.
        /// </summary>
        internal static List<CadEditRecord> Follow(
            CadDocument document,
            IReadOnlyList<CadEditRecord> planned,
            IReadOnlyList<CadEditRecord> made
        )
        {
            var followed = new List<CadEditRecord>();
            Dictionary<string, string> pairs = null;
            var viewports = new SortedSet<string>(StringComparer.Ordinal);

            foreach (IReadOnlyList<CadEditRecord> records in new[] { planned, made })
            {
                foreach (CadEditRecord record in records)
                {
                    if (record.Handle == null)
                    {
                        continue;
                    }

                    switch (record.Kind)
                    {
                        case CadEditRecordKind.Transformed:
                        case CadEditRecordKind.Reshaped:
                        case CadEditRecordKind.PropertyChanged:
                            break;
                        default:
                            continue;
                    }

                    pairs = pairs ?? Pairs(document);

                    if (pairs.Count == 0)
                    {
                        return followed;
                    }

                    if (pairs.TryGetValue(record.Handle, out string partner))
                    {
                        viewports.Add(document.FindEntity(record.Handle) is DxfViewport ? record.Handle : partner);
                    }
                }
            }

            foreach (string handle in viewports)
            {
                if (document.FindEntity(handle) is DxfViewport viewport && viewport.FitToClip())
                {
                    followed.Add(
                        new CadEditRecord(CadEditRecordKind.Reshaped, viewport.Handle)
                        {
                            Space = document.FindSpace(handle)?.Name,
                        }
                    );
                }
            }

            return followed;
        }

        /// <summary>
        /// How near a multiple of a quarter turn a rotation may be, in radians, for AutoCAD 2021 to take it as one: 90.00001 degrees
        /// (1.7E-7) turned a rectangular viewport into a rectangle 80 by 100, 90.0001 (1.7E-6) into a clipped one.
        /// </summary>
        private const double QuarterTurn = 1E-6;

        /// <summary>
        /// How much a frame's width or height may differ from the box of its mirrored boundary, in paper units, to be taken as the
        /// same size: AutoCAD 2021 left a view alone where a triangle mirrored about a line 3E-7 degrees from horizontal changed
        /// its height by 8E-7, and moved it where 1E-6 degrees changed it by 2.8E-6.
        /// </summary>
        private const double SameSize = 1E-6;

        /// <summary>
        /// Turns or mirrors a viewport that is not clipped, as AutoCAD 2021's ROTATE and MIRROR do (probes GhVr6, GhVr7, GhVr12): a
        /// mirror puts the frame's centre at its mirror image, its size and its view as they were; a rotation by a multiple of a
        /// quarter turn turns the frame about the base point, swapping its width and height for an odd one, and the view by the
        /// angle; any other rotation makes the viewport clipped, to a new closed 2D polyline of the frame's four corners turned
        /// (lower left, lower right, upper right, upper left) on the viewport's layer, the frame the box of it. The view's twist
        /// grows by the angle, its height by the frame's, and its centre becomes the turn by the new twist of the old centre less the
        /// target (the model point at the middle of the frame is then the old view centre read as a point of model space, which is
        /// where it was while the target is the origin and the twist nought): a viewport 100 by 80 showing 400 high about (50, -20)
        /// with its target at (30, 10), twisted 17.2 degrees, turned 60 degrees about (20, 30), is 119.28 by 126.6 about (-0.62, 134.28),
        /// twisted 77.2 degrees, its view centre (33.69, 12.85), 633 high.
        /// </summary>
        /// <param name="s">The edit.</param>
        /// <param name="viewport">The viewport, which has no clip boundary.</param>
        /// <param name="transform">The rotation or the mirror.</param>
        /// <param name="how">The command, as its record names it: Rotate or Mirror.</param>
        internal static void TurnPlain(CadEditScope s, DxfViewport viewport, GeoTransform3 transform, string how)
        {
            GeoPoint3 now = viewport.CenterPoint;
            GeoPoint3 centre = transform.Transform(now);

            if (how != "Rotate")
            {
                viewport.TransformBy(GeoTransform3.Translation(centre - now));
                return;
            }

            double angle = Math.Atan2(transform[1, 0], transform[0, 0]);
            double quarters = Math.Round(angle / (Math.PI / 2.0));
            bool square = Math.Abs(angle - quarters * Math.PI / 2.0) <= QuarterTurn;

            if (square && quarters == 0.0)
            {
                return;
            }

            if (square)
            {
                angle = quarters * Math.PI / 2.0;
            }

            double twist = viewport.TwistAngle + angle;
            twist -= 2.0 * Math.PI * Math.Floor(twist / (2.0 * Math.PI));

            if (Math.Abs(twist - 2.0 * Math.PI) < 1E-12)
            {
                twist = 0.0;
            }

            GeoPoint2 view = viewport.ViewCenter;
            GeoPoint3 target = viewport.ViewTarget;
            double x = view.X - target.X;
            double y = view.Y - target.Y;
            double cos = Math.Cos(twist);
            double sin = Math.Sin(twist);

            if (square)
            {
                double width = viewport.Width;
                double height = viewport.Height;
                bool odd = Math.Abs(quarters % 2.0) == 1.0;
                double newHeight = odd ? width : height;
                viewport.CenterPoint = centre;
                viewport.Width = odd ? height : width;
                viewport.Height = newHeight;
                viewport.ViewHeight *= newHeight / height;
            }
            else
            {
                double w = viewport.Width / 2.0;
                double h = viewport.Height / 2.0;
                var corners = new List<GeoPoint3>();

                foreach (
                    GeoPoint3 corner in new[]
                    {
                        new GeoPoint3(now.X - w, now.Y - h, 0.0),
                        new GeoPoint3(now.X + w, now.Y - h, 0.0),
                        new GeoPoint3(now.X + w, now.Y + h, 0.0),
                        new GeoPoint3(now.X - w, now.Y + h, 0.0),
                    }
                )
                {
                    GeoPoint3 turned = transform.Transform(corner);
                    corners.Add(new GeoPoint3(turned.X, turned.Y, 0.0));
                }

                var boundary = new DxfPolyline2d(DxfPoly2dType.SimplePoly, corners, 0.0, true, 0.0, 0.0, null)
                {
                    Layer = viewport.Layer,
                };
                s.Append(s.Document.FindSpace(viewport.Handle), boundary);
                viewport.ClipTo(boundary);
            }

            viewport.TwistAngle = twist;
            viewport.ViewCenter = new GeoPoint2(x * cos - y * sin, x * sin + y * cos);
        }

        /// <summary>
        /// Ties the copy of a clipped viewport to the copy of its boundary made by the same command, as AutoCAD keeps the two
        /// together, and fits its frame to that copy as the transformation leaves it (<see cref="Refit"/>); a copy whose boundary
        /// was not copied stays tied to the original's.
        /// </summary>
        /// <param name="s">The edit.</param>
        /// <param name="copies">Each copy by the handle of its original.</param>
        /// <param name="transform">What the copies were moved by.</param>
        /// <param name="how">The command that made them, as its record names it.</param>
        internal static void TieCopies(
            CadEditScope s,
            IReadOnlyDictionary<string, DxfEntity> copies,
            GeoTransform3 transform,
            string how
        )
        {
            foreach (KeyValuePair<string, DxfEntity> copy in copies)
            {
                if (
                    copy.Value is DxfViewport made
                    && s.Document.FindEntity(copy.Key) is DxfViewport original
                    && original.NonRectClipEntity is DxfEntity boundary
                    && copies.TryGetValue(boundary.Handle, out DxfEntity copiedBoundary)
                )
                {
                    made.TieClip(copiedBoundary);
                    Refit(made, transform, how, true);
                }
            }
        }

        /// <summary>
        /// Whether a clipped viewport and its boundary are in the list of handles together, which a copy or a transformation of
        /// the two is made of as one: the viewport takes its place and view from the boundary (<see cref="Refit"/>).
        /// </summary>
        internal static bool IsPaired(DxfViewport viewport, ISet<string> handles) =>
            viewport.NonRectClipEntity is DxfEntity boundary && handles.Contains(boundary.Handle);

        /// <summary>
        /// Fits a clipped viewport to its boundary after a command moved the boundary by a transformation, with the view left as
        /// AutoCAD 2021 leaves it (probes GhVq and GhVr12): a MOVE or a MIRROR keeps the view's centre (the frame goes with its
        /// boundary), except that a copy a mirror made whose frame changes size (a triangle mirrored about a diagonal, its box 80
        /// by 60 turned to 60 by 80) has the view's centre moved by the step of the frame's centre from where the copy stands over
        /// the scale of the view, as a stretch moves it; a SCALE scales the frame and the height of the view alike and moves the
        /// view's centre by the step of the frame's centre over the scale of the view; a ROTATE turns the view by the angle (its
        /// twist, group 51) and writes the view's centre as the twist turns <c>2 V - C' - T</c>, V the view's centre before, C'
        /// the frame's centre carried by the transformation and T the view's target, as it does for any angle and any scale of the
        /// view.
        /// </summary>
        /// <param name="viewport">The viewport, still as it was before the command.</param>
        /// <param name="transform">What the boundary was moved by.</param>
        /// <param name="how">The command, as its record names it: Rotate and Scale are told apart, every other moves like a Move.</param>
        /// <param name="copy">Whether the viewport is the copy a command made, standing where its original does.</param>
        internal static void Refit(DxfViewport viewport, GeoTransform3 transform, string how, bool copy = false)
        {
            GeoPoint3 moved = transform.Transform(viewport.CenterPoint);

            if (how == "Scale" || (copy && how == "Mirror" && SizeChanged(viewport)))
            {
                viewport.FitToClip();
                return;
            }

            if (how != "Rotate")
            {
                viewport.FitToClip(moved);
                return;
            }

            double twist = viewport.TwistAngle + Math.Atan2(transform[1, 0], transform[0, 0]);
            twist -= 2.0 * Math.PI * Math.Floor(twist / (2.0 * Math.PI));

            if (Math.Abs(twist - 2.0 * Math.PI) < 1E-12)
            {
                twist = 0.0;
            }

            GeoPoint2 view = viewport.ViewCenter;
            GeoPoint3 target = viewport.ViewTarget;
            double x = 2.0 * view.X - moved.X - target.X;
            double y = 2.0 * view.Y - moved.Y - target.Y;
            double cos = Math.Cos(twist);
            double sin = Math.Sin(twist);
            viewport.FitToClip(moved);
            viewport.TwistAngle = twist;
            viewport.ViewCenter = new GeoPoint2(x * cos - y * sin, x * sin + y * cos);
        }

        /// <summary>Whether the box of a clipped viewport's boundary is another size than its frame (<see cref="SameSize"/>).</summary>
        private static bool SizeChanged(DxfViewport viewport)
        {
            DxfEntity boundary = viewport.NonRectClipEntity;

            return boundary != null
                && DxfViewport.CanClip(boundary, out GeoAabb3 box)
                && (
                    Math.Abs(box.Max.X - box.Min.X - viewport.Width) > SameSize
                    || Math.Abs(box.Max.Y - box.Min.Y - viewport.Height) > SameSize
                );
        }
    }
}
