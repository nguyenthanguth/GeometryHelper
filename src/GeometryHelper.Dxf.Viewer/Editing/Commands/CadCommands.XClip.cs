using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>XCLIP: the clip boundary of block references and external references, as AutoCAD 2021's.</summary>
    internal static partial class CadCommands
    {
        private static readonly string[] XClipOptions =
        {
            "ON",
            "OFF",
            "Clipdepth",
            "Delete",
            "generate Polyline",
            "New boundary",
        };

        /// <summary>
        /// XCLIP: "Select objects:", the block references and external references among them kept (the others "1 was ineligible
        /// for clipping.", "None found." where there are none), then "Enter clipping option [ON/OFF/Clipdepth/Delete/generate
        /// Polyline/New boundary] &lt;New&gt;:". ON and OFF switch the clip of each reference that has one, Delete takes it off,
        /// generate Polyline draws a closed polyline on the current layer along it; these say "No clip boundary found." and ask
        /// again where none of the references has one. Clipdepth asks the front and the back plane, by a distance from the boundary
        /// or Remove. New boundary asks "Delete old boundary(s)?" where one has a boundary, then the boundary: Rectangular by two
        /// corners, Polygonal by points as far as Enter, Select polyline, or Invert clip, which hides what the boundary holds
        /// instead of what it leaves out. The boundary is in the user coordinate system that is current. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> XClip(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var references = new List<string>();
            int ineligible = 0;

            foreach (string handle in c.Selected)
            {
                if (c.Document.FindEntity(handle) is DxfBlockReference)
                {
                    references.Add(handle);
                }
                else
                {
                    ineligible++;
                }
            }

            if (ineligible > 0)
            {
                c.Write(
                    ineligible.ToString(CultureInfo.InvariantCulture)
                        + (ineligible == 1 ? " was" : " were")
                        + " ineligible for clipping."
                );
            }

            if (references.Count == 0)
            {
                c.Write("None found.");
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter clipping option", XClipOptions, "New boundary");

                if (c.Input.Is("New boundary") || c.Input.IsNone)
                {
                    foreach (CadPrompt prompt in XClipNew(c, references))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                bool on = c.Input.Is("ON");
                bool off = c.Input.Is("OFF");
                bool delete = c.Input.Is("Delete");
                bool polyline = c.Input.Is("generate Polyline");
                bool depth = c.Input.Is("Clipdepth");
                List<string> clipped = references.FindAll(h =>
                    ((DxfBlockReference)c.Document.FindEntity(h)).SpatialFilter != null
                );

                if (clipped.Count == 0)
                {
                    c.Write(
                        depth
                            ? "Xref must contain a clip boundary before specifying a clipdepth."
                            : "No clip boundary found."
                    );
                    continue;
                }

                if (on || off)
                {
                    c.Apply(Reshaped(clipped), s =>
                    {
                        foreach (string handle in clipped)
                        {
                            ((DxfBlockReference)s.Document.FindEntity(handle)).SpatialFilter.IsEnabled = on;
                        }
                    });
                }
                else if (delete)
                {
                    c.Apply(Reshaped(clipped), s =>
                    {
                        foreach (string handle in clipped)
                        {
                            ((DxfBlockReference)s.Document.FindEntity(handle)).RemoveClip();
                        }
                    });
                }
                else if (polyline)
                {
                    c.Apply(Reshaped(clipped), s =>
                    {
                        foreach (string handle in clipped)
                        {
                            var reference = (DxfBlockReference)s.Document.FindEntity(handle);
                            IReadOnlyList<GeoPoint2> ring = reference.ClipOutline;

                            if (ring == null)
                            {
                                continue;
                            }

                            var made = new DxfPolyline();
                            GeoTransform3 placed = reference.BlockTransform;

                            for (int i = 0; i < ring.Count; i++)
                            {
                                GeoPoint3 world = placed.Transform(new GeoPoint3(ring[i].X, ring[i].Y, 0.0));
                                made.AddVertexAt(i, new GeoPoint2(world.X, world.Y), 0.0, 0.0, 0.0);
                            }

                            made.Closed = true;
                            s.Append(c.Space, Current(c, made));
                        }
                    });
                }
                else if (depth)
                {
                    foreach (CadPrompt prompt in XClipDepth(c, clipped))
                    {
                        yield return prompt;
                    }
                }

                yield break;
            }
        }

        private static CadEditRecord[] Reshaped(IReadOnlyList<string> handles)
        {
            var records = new CadEditRecord[handles.Count];

            for (int i = 0; i < records.Length; i++)
            {
                records[i] = new CadEditRecord(CadEditRecordKind.Reshaped, handles[i]);
            }

            return records;
        }

        /// <summary>
        /// Clipdepth: "Specify front clip point or [Distance/Remove]:" then "Specify back clip point or [Distance/Remove]:", each a
        /// distance from the boundary (a point on the screen is on the boundary's plane: 0), or Remove; Enter keeps the plane. The
        /// front plane may not lie behind the back one: "Front clipping plane behind back clipping plane.  Clipdepth not
        /// applied."
        /// </summary>
        private static IEnumerable<CadPrompt> XClipDepth(CadCommandContext c, IReadOnlyList<string> clipped)
        {
            double? front = null;
            double? back = null;
            bool setFront = false;
            bool setBack = false;

            for (int plane = 0; plane < 2; plane++)
            {
                string which = plane == 0 ? "front" : "back";
                yield return CadPrompt.ForPoint(
                    "Specify " + which + " clip point",
                    null,
                    new[] { "Distance", "Remove" }
                );
                double? value = null;
                bool set = true;

                if (c.Input.IsPoint)
                {
                    value = 0.0;
                }
                else if (c.Input.Is("Distance"))
                {
                    yield return CadPrompt.ForDistance("Specify distance from boundary");

                    if (c.Input.Kind != CadInputKind.Number)
                    {
                        yield break;
                    }

                    value = c.Input.Number;
                }
                else if (!c.Input.Is("Remove"))
                {
                    set = false;
                }

                if (plane == 0)
                {
                    front = value;
                    setFront = set;
                }
                else
                {
                    back = value;
                    setBack = set;
                }
            }

            if (!setFront && !setBack)
            {
                yield break;
            }

            // Each reference's planes as they would stand: the front may not lie behind the back.
            foreach (string handle in clipped)
            {
                DxfSpatialFilter filter = ((DxfBlockReference)c.Document.FindEntity(handle)).SpatialFilter;
                double? f = setFront ? front : filter.FrontClip;
                double? b = setBack ? back : filter.BackClip;

                if (f.HasValue && b.HasValue && f.Value < b.Value)
                {
                    c.Write("Front clipping plane behind back clipping plane.  Clipdepth not applied.");
                    yield break;
                }
            }

            c.Apply(Reshaped(clipped), s =>
            {
                foreach (string handle in clipped)
                {
                    DxfSpatialFilter filter = ((DxfBlockReference)s.Document.FindEntity(handle)).SpatialFilter;

                    if (setFront)
                    {
                        filter.FrontClip = front;
                    }

                    if (setBack)
                    {
                        filter.BackClip = back;
                    }
                }
            });
        }

        /// <summary>
        /// New boundary: "Delete old boundary(s)? [Yes/No] &lt;Yes&gt;:" where a reference has one, then "Specify clipping boundary or
        /// select invert option: [Select polyline/Polygonal/Rectangular/Invert clip] &lt;Rectangular&gt;:", Invert clip turning "Outside
        /// mode - Objects outside boundary will be hidden." to "Inside mode - Objects inside boundary will be hidden." and back.
        /// </summary>
        private static IEnumerable<CadPrompt> XClipNew(CadCommandContext c, IReadOnlyList<string> references)
        {
            bool old = false;

            foreach (string handle in references)
            {
                old |= ((DxfBlockReference)c.Document.FindEntity(handle)).SpatialFilter != null;
            }

            if (old)
            {
                yield return CadPrompt.ForKeyword("Delete old boundary(s)?", new[] { "Yes", "No" }, "Yes");

                if (c.Input.Is("No"))
                {
                    yield break;
                }
            }

            bool inverted = false;
            c.Write("Outside mode - Objects outside boundary will be hidden.");
            CadUcs ucs = c.Session.Ucs;
            var boundary = new List<GeoPoint2>();

            while (boundary.Count == 0)
            {
                yield return CadPrompt.ForKeyword(
                    "Specify clipping boundary or select invert option",
                    new[] { "Select polyline", "Polygonal", "Rectangular", "Invert clip" },
                    "Rectangular"
                );

                if (c.Input.Is("Invert clip"))
                {
                    inverted = !inverted;
                    c.Write(
                        inverted
                            ? "Inside mode - Objects inside boundary will be hidden."
                            : "Outside mode - Objects outside boundary will be hidden."
                    );
                    continue;
                }

                if (c.Input.Is("Select polyline"))
                {
                    var picked = new List<GeoPoint2>();

                    foreach (CadPrompt prompt in XClipPicked(c, ucs, picked))
                    {
                        yield return prompt;
                    }

                    if (picked.Count == 0)
                    {
                        yield break;
                    }

                    boundary.AddRange(
                        inverted ? picked : CadXClips.AsRectangle(picked, c.Session.Tolerance.EqualPoint)
                    );
                }
                else if (c.Input.Is("Polygonal"))
                {
                    var points = new List<GeoPoint2>();

                    foreach (CadPrompt prompt in XClipPolygon(c, points))
                    {
                        yield return prompt;
                    }

                    if (points.Count == 0)
                    {
                        yield break;
                    }

                    if (points.Count < 3 || Math.Abs(Area(points)) <= c.Session.Tolerance.EqualPoint)
                    {
                        c.Write("Boundaries with zero area are not acceptable.");
                        yield break;
                    }

                    foreach (GeoPoint2 point in points)
                    {
                        boundary.Add(ucs.ToUcs(point));
                    }
                }
                else if (c.Input.IsNone || c.Input.Is("Rectangular"))
                {
                    var corners = new List<GeoPoint2>();

                    foreach (CadPrompt prompt in XClipRectangle(c, ucs, corners))
                    {
                        yield return prompt;
                    }

                    if (corners.Count == 0)
                    {
                        yield break;
                    }

                    if (
                        Math.Abs(corners[1].X - corners[0].X) <= c.Session.Tolerance.EqualPoint
                        || Math.Abs(corners[1].Y - corners[0].Y) <= c.Session.Tolerance.EqualPoint
                    )
                    {
                        c.Write("Boundaries with zero area are not acceptable.");
                        yield break;
                    }

                    boundary.AddRange(corners);
                }
                else
                {
                    yield break;
                }
            }

            GeoTransform3 worldToClip = CadXClips.WorldToClip(ucs);
            c.Apply(Reshaped(references), s =>
            {
                foreach (string handle in references)
                {
                    var reference = (DxfBlockReference)s.Document.FindEntity(handle);
                    reference.ClipTo(
                        inverted ? InvertedAbout(reference, boundary, worldToClip) : boundary,
                        worldToClip
                    );
                }
            });
        }

        /// <summary>The boundary of an inverted clip of a reference: the polygon with a slit to the box that holds the reference, in the clip system.</summary>
        private static IReadOnlyList<GeoPoint2> InvertedAbout(
            DxfBlockReference reference,
            IReadOnlyList<GeoPoint2> boundary,
            GeoTransform3 worldToClip
        )
        {
            GeoAabb3 box = reference.GeometricExtents;
            double minX = double.MaxValue,
                minY = double.MaxValue,
                maxX = double.MinValue,
                maxY = double.MinValue;

            if (box.IsEmpty)
            {
                foreach (GeoPoint2 point in boundary)
                {
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }
            }
            else
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
                    GeoPoint3 inClip = worldToClip.Transform(corner);
                    minX = Math.Min(minX, inClip.X);
                    maxX = Math.Max(maxX, inClip.X);
                    minY = Math.Min(minY, inClip.Y);
                    maxY = Math.Max(maxY, inClip.Y);
                }
            }

            return CadXClips.Inverted(boundary, new GeoPoint2(minX, minY), new GeoPoint2(maxX, maxY));
        }

        /// <summary>The area of a polygon, positive where it runs counter-clockwise.</summary>
        private static double Area(IReadOnlyList<GeoPoint2> points)
        {
            double sum = 0.0;

            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                sum += points[j].X * points[i].Y - points[i].X * points[j].Y;
            }

            return sum / 2.0;
        }

        /// <summary>Rectangular: "Specify first corner:", "Specify opposite corner:", the sides along the axes of the user coordinate system; the two corners in it, none where the command was given up.</summary>
        private static IEnumerable<CadPrompt> XClipRectangle(CadCommandContext c, CadUcs ucs, List<GeoPoint2> corners)
        {
            yield return CadPrompt.ForPoint("Specify first corner");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;
            GeoPoint2 a = ucs.ToUcs(first);

            DxfPolyline Make(GeoPoint2 other)
            {
                GeoPoint2 b = ucs.ToUcs(other);
                var rectangle = new DxfPolyline();
                rectangle.AddVertexAt(0, ucs.ToWorld(a), 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(1, ucs.ToWorld(new GeoPoint2(b.X, a.Y)), 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(2, ucs.ToWorld(b), 0.0, 0.0, 0.0);
                rectangle.AddVertexAt(3, ucs.ToWorld(new GeoPoint2(a.X, b.Y)), 0.0, 0.0, 0.0);
                rectangle.Closed = true;
                return Current(c, rectangle);
            }

            yield return CadPrompt
                .ForPoint("Specify opposite corner", first)
                .Previewing(CadPreview.MakingOne(c, input => Make(input.Point)));

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            corners.Add(a);
            corners.Add(ucs.ToUcs(c.Input.Point));
        }

        /// <summary>
        /// Polygonal: "Specify first point:", then "Specify next point or [Undo]:" until Enter, the points in the world. A point that
        /// would make a side of no length or cross another is refused: "Invalid point, polygon segment is zero length." and
        /// "Invalid point, polygon segments cannot intersect."
        /// </summary>
        private static IEnumerable<CadPrompt> XClipPolygon(CadCommandContext c, List<GeoPoint2> points)
        {
            yield return CadPrompt.ForPoint("Specify first point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            points.Add(c.Input.Point);

            while (true)
            {
                GeoPoint2 last = points[points.Count - 1];

                DxfPolyline Band(GeoPoint2 to)
                {
                    var band = new DxfPolyline();

                    for (int i = 0; i < points.Count; i++)
                    {
                        band.AddVertexAt(i, points[i], 0.0, 0.0, 0.0);
                    }

                    band.AddVertexAt(points.Count, to, 0.0, 0.0, 0.0);
                    band.Closed = points.Count >= 2;
                    return Current(c, band);
                }

                yield return CadPrompt
                    .ForPoint("Specify next point", last, new[] { "Undo" })
                    .Previewing(CadPreview.MakingOne(c, input => Band(input.Point)));

                if (c.Input.Is("Undo"))
                {
                    if (points.Count > 1)
                    {
                        points.RemoveAt(points.Count - 1);
                    }

                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 next = c.Input.Point;
                double tolerance = c.Session.Tolerance.EqualPoint;

                if ((next - last).Length <= tolerance)
                {
                    c.Write("Invalid point, polygon segment is zero length.");
                    continue;
                }

                if (Crosses(points, next, tolerance))
                {
                    c.Write("Invalid point, polygon segments cannot intersect.");
                    continue;
                }

                points.Add(next);
            }
        }

        /// <summary>Whether a point added to a chain of points would make its last side or the closing side cross a side that does not touch it.</summary>
        private static bool Crosses(IReadOnlyList<GeoPoint2> points, GeoPoint2 next, double tolerance)
        {
            int n = points.Count;

            // The side from the last point to the new one against the sides before the last.
            for (int j = 0; j + 2 < n; j++)
            {
                if (SidesCross(points[n - 1], next, points[j], points[j + 1], tolerance))
                {
                    return true;
                }
            }

            // The closing side from the new point to the first, against the sides between, once there are three corners.
            if (n >= 2)
            {
                for (int j = 1; j + 1 < n; j++)
                {
                    if (SidesCross(next, points[0], points[j], points[j + 1], tolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Whether two sides cross one another, or one ends on the other.</summary>
        private static bool SidesCross(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c, GeoPoint2 d, double tolerance)
        {
            double d1 = Side(a, b, c);
            double d2 = Side(a, b, d);
            double d3 = Side(c, d, a);
            double d4 = Side(c, d, b);
            return ((d1 > tolerance && d2 < -tolerance) || (d1 < -tolerance && d2 > tolerance))
                && ((d3 > tolerance && d4 < -tolerance) || (d3 < -tolerance && d4 > tolerance));
        }

        private static double Side(GeoPoint2 a, GeoPoint2 b, GeoPoint2 p) =>
            ((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X)) / Math.Max((b - a).Length, 1E-12);

        /// <summary>
        /// Select polyline: "Select polyline:", a polyline or a 2D polyline of vertices, which is the boundary by its vertices in
        /// the user coordinate system (its arcs left out, as AutoCAD takes it); anything else "Selected object is not a polyline."
        /// and asks again. A polyline of fewer than three vertices "Boundaries with zero area are not acceptable."
        /// </summary>
        private static IEnumerable<CadPrompt> XClipPicked(CadCommandContext c, CadUcs ucs, List<GeoPoint2> picked)
        {
            while (true)
            {
                yield return CadPrompt.ForEntity("Select polyline");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                DxfEntity entity = c.Document.FindEntity(c.Input.Handles[0]);
                int count;
                Func<int, GeoPoint3> vertex;

                if (entity is DxfPolyline lightweight)
                {
                    count = lightweight.NumberOfVertices;
                    vertex = i => lightweight.GetPointAtParameter(i);
                }
                else if (entity is DxfPolyline2d heavy)
                {
                    var all = new List<GeoPoint3>();

                    foreach (DxfVertex2d corner in heavy)
                    {
                        all.Add(heavy.VertexPosition(corner));
                    }

                    count = all.Count;
                    vertex = i => all[i];
                }
                else
                {
                    c.Write("Selected object is not a polyline.");
                    continue;
                }

                var points = new List<GeoPoint2>();

                for (int i = 0; i < count; i++)
                {
                    GeoPoint3 world = vertex(i);
                    points.Add(ucs.ToUcs(new GeoPoint2(world.X, world.Y)));
                }

                if (points.Count < 3 || Math.Abs(Area(points)) <= c.Session.Tolerance.EqualPoint)
                {
                    c.Write("Boundaries with zero area are not acceptable.");
                    yield break;
                }

                picked.AddRange(points);
                yield break;
            }
        }
    }
}
