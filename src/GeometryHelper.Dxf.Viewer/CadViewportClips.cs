using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Gets the outline a clipped viewport shows model space through: its clip boundary (<see cref="DxfViewport.NonRectClipEntity"/>)
        /// as a ring of paper units, a circle or the arcs of a polyline kept as arcs. Null for a viewport that is not clipped, and for
        /// one whose boundary has no outline the library reads, which is shown as the rectangle of its frame.
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
        /// that is not a closed circle, ellipse, polyline or spline, "Invalid: Clip entity not currently regen'ed." for a viewport or
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

            bool shape =
                candidate is DxfCircle
                || candidate is DxfEllipse
                || candidate is DxfPolyline
                || candidate is DxfPolyline2d
                || candidate is DxfSpline;

            if (!shape || !((DxfCurve)candidate).Closed)
            {
                return "Clip object must be closed.";
            }

            GeoAabb3 box = candidate.GeometricExtents;
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
                    Refit(made, transform, how);
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
        /// AutoCAD 2021 leaves it (probe GhVq): a MOVE or a MIRROR keeps the view's centre (the frame goes with its boundary);
        /// a SCALE scales the frame and the height of the view alike and moves the view's centre by the step of the frame's
        /// centre over the scale of the view; a ROTATE turns the view by the angle (its twist, group 51) and writes the view's
        /// centre as the twist turns <c>2 V - C' - T</c>, V the view's centre before, C' the frame's centre carried by the
        /// transformation and T the view's target, as it does for any angle and any scale of the view.
        /// </summary>
        /// <param name="viewport">The viewport, still as it was before the command.</param>
        /// <param name="transform">What the boundary was moved by.</param>
        /// <param name="how">The command, as its record names it: Rotate and Scale are told apart, every other moves like a Move.</param>
        internal static void Refit(DxfViewport viewport, GeoTransform3 transform, string how)
        {
            GeoPoint3 moved = transform.Transform(viewport.CenterPoint);

            if (how == "Scale")
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
    }
}
