using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>MSPACE and PSPACE: model space worked in through a viewport of the layout shown, and the paper again; VPSCALE, a viewport's scale.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// MSPACE (MS): works in model space through the viewport last worked through, else the layout's first, as AutoCAD's;
        /// refused on the Model tab; in a layout with no viewport to work through it says "There are no active Model space
        /// viewports.", and worked through one already "Already in model space." Each is one undo step, an empty one for the
        /// two that say so, as AutoCAD 2021's console records them.
        /// </summary>
        private static IEnumerable<CadPrompt> ModelSpaceThrough(CadCommandContext c)
        {
            if (c.Document.CurrentSpace.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            DxfViewport active = c.Document.ActiveViewport;

            if (active != null)
            {
                c.Write("Already in model space.");
                c.Document.WorkThrough(active.Handle, "MSPACE");
                yield break;
            }

            DxfViewport viewport = c.Document.ViewportToEnter();

            if (viewport == null)
            {
                c.Write("There are no active Model space viewports.");
            }

            c.Document.WorkThrough(viewport?.Handle, "MSPACE");
        }

        /// <summary>
        /// VPSCALE: the scale of the viewport worked through or selected alone, else of one picked, written as AutoCAD writes it
        /// ("PS:MS == 1:20", "Viewport Scale Factor = 0.0500"); then a new scale asked, as 1:50 or 2:1, Enter keeping it, one
        /// undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> ViewportScale(CadCommandContext c)
        {
            if (c.Document.CurrentSpace.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            DxfViewport viewport = CadViewportLayerEditor.TargetViewport(c.Document);

            if (viewport == null)
            {
                yield return CadPrompt.ForEntity("Select a viewport");
                viewport =
                    c.Input.Kind == CadInputKind.Selection
                        ? c.Document.FindEntity(c.Input.Handles[0]) as DxfViewport
                        : null;
            }

            if (viewport == null || viewport.Number == 1 || !(viewport.Height > 0.0) || !(viewport.ViewHeight > 0.0))
            {
                c.Write("The object selected is not a viewport.");
                yield break;
            }

            string name = CadViewports.ScaleName(viewport.CustomScale);
            c.Write("PS:MS == " + name);
            c.Write("Viewport Scale Factor = " + CadCommandContext.Length(viewport.CustomScale));
            yield return CadPrompt.ForString("Enter a new viewport scale (1:50, 2:1)", name);
            string typed = (c.Input.Text ?? string.Empty).Trim();

            if (c.Input.IsNone || typed.Length == 0 || string.Equals(typed, name, StringComparison.Ordinal))
            {
                yield break;
            }

            if (!CadViewports.TryReadScale(typed, out double scale))
            {
                c.Write("A scale is written as 1:50 or 2:1.");
                yield break;
            }

            c.OpenUndoStep();
            CadEditResult result = CadPropertyEditor.Set(
                c.Document,
                new[] { viewport.Handle },
                "Custom scale",
                scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            );

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// VPCLIP: clips a viewport to an object or a polygon, as AutoCAD 2021's. "Select viewport to clip:", then "Select
        /// clipping object or [Polygonal] &lt;Polygonal&gt;:": a closed polyline, circle, ellipse or closed spline picked becomes the
        /// viewport's border and its frame the box of it; Polygonal asks the points of a polygon as PLINE does, with its Arc,
        /// Length and Undo, Enter or Close ending it, and makes a closed polyline on the current layer; Delete, taken for a
        /// viewport that is clipped, erases the boundary and leaves the frame. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> ViewportClip(CadCommandContext c)
        {
            // Worked through a viewport (MSPACE), AutoCAD says "Switching to Paper space.", asks on the paper, and goes back into
            // the viewport where it ends, however it ends.
            string back = c.Document.ActiveViewport?.Handle;

            if (back != null)
            {
                c.Write("Switching to Paper space.");
                c.Document.LeaveViewport();
            }

            try
            {
                foreach (CadPrompt prompt in ClipViewport(c))
                {
                    yield return prompt;
                }
            }
            finally
            {
                if (back != null)
                {
                    c.Write("Switching to Model space.");
                    c.Document.EnterViewport(back);
                }
            }
        }

        private static IEnumerable<CadPrompt> ClipViewport(CadCommandContext c)
        {
            if (c.Document.CurrentSpace.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            DxfViewport viewport = null;

            while (viewport == null)
            {
                yield return CadPrompt.ForEntity("Select viewport to clip");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                foreach (string handle in c.Input.Handles)
                {
                    viewport = viewport ?? c.Document.FindEntity(handle) as DxfViewport;
                }

                if (viewport == null)
                {
                    c.Write("Object selected was not a viewport.");
                }
            }

            string name = viewport.Handle;
            bool first = true;

            while (true)
            {
                yield return first
                    ? CadPrompt.ForEntity("Select clipping object", new[] { "Polygonal" }, "Polygonal").Hiding("Delete")
                    : CadPrompt.ForEntity("Select object to clip viewport");

                if (first && c.Input.Is("Delete"))
                {
                    DxfEntity boundary = viewport.NonRectClipEntity;

                    if (boundary == null)
                    {
                        c.Write("Viewport does not have clip boundary associated.");
                        yield break;
                    }

                    string boundaryName = boundary.Handle;
                    c.Apply(
                        new[]
                        {
                            new CadEditRecord(CadEditRecordKind.Reshaped, name),
                            new CadEditRecord(CadEditRecordKind.Erased, boundaryName),
                        },
                        s => ((DxfViewport)s.Document.FindEntity(name)).RemoveClip()
                    );
                    yield break;
                }

                if (first && (c.Input.IsNone || c.Input.Is("Polygonal")))
                {
                    var polygon = new List<GeoPoint2>();
                    var bulges = new List<double>();

                    foreach (CadPrompt prompt in ClipPolygon(c, polygon, bulges))
                    {
                        yield return prompt;
                    }

                    if (polygon.Count >= 3)
                    {
                        c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, name) }, s =>
                        {
                            var made = new DxfPolyline();

                            for (int i = 0; i < polygon.Count; i++)
                            {
                                made.AddVertexAt(i, polygon[i], bulges[i], 0.0, 0.0);
                            }

                            made.Closed = true;
                            DxfPolyline added = s.Append(c.Space, Current(c, made));
                            ((DxfViewport)s.Document.FindEntity(name)).ClipTo(added);
                        });
                    }

                    yield break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);
                string why =
                    picked == null ? "Clip object must be closed." : CadViewportClips.WhyNotClip(viewport, picked);

                if (why != null)
                {
                    c.Write(why);
                    first = false;
                    continue;
                }

                string clip = picked.Handle;
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, name) }, s =>
                    ((DxfViewport)s.Document.FindEntity(name)).ClipTo(s.Document.FindEntity(clip))
                );
                yield break;
            }
        }

        /// <summary>
        /// The points of the polygon VPCLIP's Polygonal asks, as PLINE asks them: "Specify next point or [Arc/Length/Undo]:", with
        /// Close from the second segment on, the arc segments by their end point or an Angle, CEnter, Direction, Radius or Second
        /// pt; Enter closes it with a straight edge. The vertices and the bulges of the edges leaving them are left in the
        /// lists, none where the polygon was given up for want of three vertices.
        /// </summary>
        private static IEnumerable<CadPrompt> ClipPolygon(
            CadCommandContext c,
            List<GeoPoint2> points,
            List<double> bulges
        )
        {
            yield return CadPrompt.ForPoint("Specify start point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var tangents = new List<GeoVector2> { new GeoVector2(1.0, 0.0) };
            points.Add(c.Input.Point);
            bulges.Add(0.0);
            bool arcs = false;
            var plain = new[] { 0.0, 0.0 };

            while (true)
            {
                GeoPoint2 last = points[points.Count - 1];
                GeoVector2 along = tangents[tangents.Count - 1];
                int segments = points.Count - 1;

                // The polygon so far with the segment the cursor would add, drawn as the rubber band.
                DxfPolyline Band(GeoPoint2 to, double sweep)
                {
                    if ((to - last).Length <= c.Session.Tolerance.EqualPoint || double.IsNaN(sweep))
                    {
                        return null;
                    }

                    var band = new DxfPolyline();

                    for (int i = 0; i < points.Count; i++)
                    {
                        band.AddVertexAt(
                            i,
                            points[i],
                            i == points.Count - 1 ? Math.Tan(sweep / 4.0) : bulges[i],
                            0.0,
                            0.0
                        );
                    }

                    band.AddVertexAt(points.Count, to, 0.0, 0.0, 0.0);
                    return Current(c, band);
                }

                if (arcs)
                {
                    yield return CadPrompt
                        .ForPoint(
                            "Enter an arc boundary option",
                            last,
                            segments >= 1
                                ? new[]
                                {
                                    "Angle",
                                    "CEnter",
                                    "CLose",
                                    "Direction",
                                    "Line",
                                    "Radius",
                                    "Second pt",
                                    "Undo",
                                }
                                : new[] { "Angle", "CEnter", "Direction", "Line", "Radius", "Second pt", "Undo" }
                        )
                        .Previewing(
                            CadPreview.MakingOne(c, input => Band(input.Point, SweepLeaving(along, input.Point - last)))
                        );
                }
                else
                {
                    yield return CadPrompt
                        .ForPoint(
                            "Specify next point",
                            last,
                            segments >= 1
                                ? new[] { "Arc", "Close", "Length", "Undo" }
                                : new[] { "Arc", "Length", "Undo" }
                        )
                        .Previewing(CadPreview.MakingOne(c, input => Band(input.Point, 0.0)));
                }

                if (c.Input.Is("Arc"))
                {
                    arcs = true;
                    continue;
                }

                if (c.Input.Is("Line"))
                {
                    arcs = false;
                    bulges[bulges.Count - 1] = 0.0;
                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    if (segments == 0)
                    {
                        c.Write("All segments already undone.");
                        continue;
                    }

                    points.RemoveAt(points.Count - 1);
                    bulges.RemoveAt(bulges.Count - 1);
                    tangents.RemoveAt(tangents.Count - 1);
                    bulges[bulges.Count - 1] = 0.0;
                    continue;
                }

                bool close = c.Input.Is("Close") || c.Input.Is("CLose");
                GeoPoint2 next;
                double sweep = 0.0;

                if (close || c.Input.IsPoint)
                {
                    next = close ? points[0] : c.Input.Point;

                    if (arcs)
                    {
                        sweep = SweepLeaving(c.Input.Control ? along * -1.0 : along, next - last);
                    }
                }
                else if (!arcs && c.Input.Is("Length"))
                {
                    yield return CadPrompt
                        .ForDistance("Specify length of line", last)
                        .Previewing(CadPreview.MakingOne(c, input => Band(last + along * input.Number, 0.0), true));

                    if (c.Input.Kind != CadInputKind.Number)
                    {
                        continue;
                    }

                    next = last + along * c.Input.Number;
                }
                else if (arcs && !c.Input.IsNone)
                {
                    var end = new PlineArcEnd();

                    foreach (CadPrompt prompt in PlineArc(c, c.Input.Text, last, along, end, plain))
                    {
                        yield return prompt;
                    }

                    if (!end.Done)
                    {
                        continue;
                    }

                    next = end.End;
                    sweep = end.Sweep;
                }
                else
                {
                    // Enter: the polygon closes with a straight edge, if it has the three vertices of one.
                    if (points.Count < 3)
                    {
                        c.Write("Clip polyline must have at least 3 vertices.");
                        points.Clear();
                        bulges.Clear();
                    }

                    yield break;
                }

                GeoVector2 span = next - last;

                if (span.Length <= c.Session.Tolerance.EqualPoint)
                {
                    c.Write("Invalid point: a segment needs two points apart.");
                    continue;
                }

                bulges[bulges.Count - 1] = Math.Tan(sweep / 4.0);

                if (close)
                {
                    yield break;
                }

                GeoVector2 leaving = span / span.Length;

                if (arcs)
                {
                    double outAngle = Math.Atan2(span.Y, span.X) + sweep / 2.0;
                    leaving = new GeoVector2(Math.Cos(outAngle), Math.Sin(outAngle));
                }

                points.Add(next);
                bulges.Add(0.0);
                tangents.Add(leaving);
            }
        }

        /// <summary>
        /// PSPACE (PS): works on the paper of the layout shown again; refused on the Model tab; on the paper already it says
        /// "Already in paper space." One undo step, an empty one where it says so.
        /// </summary>
        private static IEnumerable<CadPrompt> PaperSpace(CadCommandContext c)
        {
            if (c.Document.CurrentSpace.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            if (c.Document.ActiveViewport == null)
            {
                c.Write("Already in paper space.");
            }

            c.Document.WorkThrough(null, "PSPACE");
        }
    }
}
