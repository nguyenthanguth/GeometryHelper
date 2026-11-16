using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The items of a multi-functional grip's hover menu (<see cref="CadCommandSession.StartGripAction"/>): the grip edit as a click on
    /// the grip makes it, Add Vertex, Convert to Arc, Convert to Line, Remove Vertex, Lengthen, Radius, the hatch's three and the dimensions' items
    /// (<see cref="GripDimension"/>) with their prompts.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>Runs one item of a grip's menu; all the edits it makes are one undo step.</summary>
        internal static IEnumerable<CadPrompt> GripAction(
            CadCommandContext c,
            string handle,
            int gripIndex,
            CadGripAction action
        )
        {
            switch (action)
            {
                case CadGripAction.Stretch:
                case CadGripAction.StretchVertex:
                    foreach (
                        CadPrompt prompt in GripEdit(c, new[] { new KeyValuePair<string, int>(handle, gripIndex) })
                    )
                    {
                        yield return prompt;
                    }

                    break;
                case CadGripAction.AddVertex:
                    foreach (
                        CadPrompt prompt in c.Document.FindEntity(handle) is DxfSpline
                            ? GripAddSplineVertex(c, handle, gripIndex)
                            : GripAddVertex(c, handle, gripIndex)
                    )
                    {
                        yield return prompt;
                    }

                    break;
                case CadGripAction.ConvertToArc:
                    foreach (CadPrompt prompt in GripConvertToArc(c, handle, gripIndex))
                    {
                        yield return prompt;
                    }

                    break;
                case CadGripAction.ConvertToLine:
                    foreach (CadPrompt prompt in GripConvertToLine(c, handle, gripIndex))
                    {
                        yield return prompt;
                    }

                    break;
                case CadGripAction.HatchScale:
                case CadGripAction.HatchAngle:
                case CadGripAction.HatchOrigin:
                    foreach (CadPrompt prompt in GripHatch(c, handle, gripIndex, action))
                    {
                        yield return prompt;
                    }

                    break;
                case CadGripAction.Lengthen:
                case CadGripAction.Radius:
                    foreach (CadPrompt prompt in GripLengthen(c, handle, gripIndex, action == CadGripAction.Radius))
                    {
                        yield return prompt;
                    }

                    break;
                case CadGripAction.RemoveVertex:
                    foreach (CadPrompt prompt in GripRemoveVertex(c, handle, gripIndex))
                    {
                        yield return prompt;
                    }

                    break;
                default:
                    foreach (CadPrompt prompt in GripDimension(c, handle, gripIndex, action))
                    {
                        yield return prompt;
                    }

                    break;
            }
        }

        /// <summary>
        /// "** REMOVE VERTEX **" and "Pick to remove vertex:" of a polyline's vertex, "** REMOVE CONTROL VERTEX **" and "Pick to
        /// remove vertex:" of a spline's control vertex, "** REMOVE FIT POINT **" and "Pick to remove fit point:" of a fit point,
        /// as AutoCAD 2021's string table words them: any point picked takes the vertex out, Enter or Esc leaves it.
        /// </summary>
        private static IEnumerable<CadPrompt> GripRemoveVertex(CadCommandContext c, string handle, int gripIndex)
        {
            DxfSpline spline = c.Document.FindEntity(handle) as DxfSpline;
            bool fit = spline != null && spline.HasFitData;
            c.Write(
                fit ? "** REMOVE FIT POINT **"
                : spline != null ? "** REMOVE CONTROL VERTEX **"
                : "** REMOVE VERTEX **"
            );
            yield return CadPrompt.ForPoint(fit ? "Pick to remove fit point" : "Pick to remove vertex");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            c.OpenUndoStep();
            CadEditResult result = CadGrips.RemoveVertex(c.Document, handle, gripIndex);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// "** ADD VERTEX **" and "Specify new vertex point or [Base point/Copy/Undo/eXit]:", the polyline previewed with the
        /// vertex at the cursor. A point puts the vertex after the grip's vertex and ends; with Copy on, each next vertex goes
        /// after the one just made, until eXit, Enter or Esc, and Undo takes the last vertex made back, the segment it split
        /// given back as it was. With a Base point given the vertex goes where the point lies from the base as the grip lies
        /// from where it was.
        /// </summary>
        private static IEnumerable<CadPrompt> GripAddVertex(CadCommandContext c, string handle, int gripIndex)
        {
            DxfEntity polyline = c.Document.FindEntity(handle);
            CadGrip grip = CadGrips.Of(polyline)[gripIndex];
            int after = CadGrips.VertexAfter(polyline, grip, gripIndex);
            GeoPoint2 origin = grip.Point;
            GeoPoint2 basePoint = origin;
            int at = gripIndex;
            bool multiple = false;
            var made = new List<AddedVertex>();
            CadPreview preview = CadPreview.Reshaping(c, handle, (entity, point) =>
                CadGrips.InsertVertexAfterAt(entity, after, origin + (point - basePoint))
            );

            while (true)
            {
                c.Write("** ADD VERTEX" + (multiple ? " (multiple)" : string.Empty) + " **");
                yield return CadPrompt
                    .ForPoint("Specify new vertex point", basePoint, new[] { "Base point", "Copy", "Undo", "eXit" })
                    .Previewing(preview);
                CadInput input = c.Input;

                if (input.IsNone)
                {
                    yield break;
                }

                if (input.Kind == CadInputKind.Keyword)
                {
                    if (input.Is("eXit"))
                    {
                        yield break;
                    }

                    if (input.Is("Copy"))
                    {
                        multiple = true;
                    }
                    else if (input.Is("Undo"))
                    {
                        // The vertex made last goes, the drawing read back to before it, and the next one is asked as it was then.
                        if (made.Count > 0)
                        {
                            AddedVertex last = made[made.Count - 1];
                            made.RemoveAt(made.Count - 1);
                            c.Document.BackTo(last.Mark);
                            after = last.After;
                            at = last.At;
                            origin = last.Origin;
                            basePoint = last.BasePoint;
                        }
                    }
                    else if (input.Is("Base point"))
                    {
                        yield return CadPrompt.ForPoint("Specify base point");

                        if (c.Input.IsPoint)
                        {
                            basePoint = c.Input.Point;
                        }
                    }

                    continue;
                }

                if (input.Kind != CadInputKind.Point && input.Kind != CadInputKind.Number)
                {
                    continue;
                }

                GeoPoint2 target = origin + (input.Point - basePoint);
                c.OpenUndoStep();
                CadGroupMark mark = c.Document.MarkGroup();
                CadEditResult result = CadGrips.AddVertex(c.Document, handle, at, target);

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                    continue;
                }

                made.Add(new AddedVertex(mark, after, at, origin, basePoint));

                if (!multiple)
                {
                    yield break;
                }

                // The next vertex goes after the one just made, rubber-banded from it.
                after++;
                at = after;
                origin = target;
                basePoint = target;
            }
        }

        /// <summary>
        /// Add Vertex of a spline point: "** ADD FIT POINT **" and "Specify new fit point:", or "** ADD CONTROL VERTEX **" and
        /// "Specify new vertex point:", the spline previewed with the point at the cursor; a point puts it in and ends.
        /// </summary>
        private static IEnumerable<CadPrompt> GripAddSplineVertex(CadCommandContext c, string handle, int gripIndex)
        {
            var spline = (DxfSpline)c.Document.FindEntity(handle);
            bool fit = spline.HasFitData;
            return GripAskPoint(
                c,
                handle,
                fit ? "** ADD FIT POINT **" : "** ADD CONTROL VERTEX **",
                fit ? "Specify new fit point" : "Specify new vertex point",
                CadGrips.Of(spline)[gripIndex].Point,
                (e, target) => CadGrips.SplineAddAt(e, gripIndex, target),
                (edited, target) => CadGrips.AddVertex(c.Document, edited, gripIndex, target),
                false
            );
        }

        /// <summary>A vertex Add Vertex has made: the savepoint before it and where the next vertex was to go then, for Undo.</summary>
        private sealed class AddedVertex
        {
            internal AddedVertex(CadGroupMark mark, int after, int at, GeoPoint2 origin, GeoPoint2 basePoint)
            {
                Mark = mark;
                After = after;
                At = at;
                Origin = origin;
                BasePoint = basePoint;
            }

            internal CadGroupMark Mark { get; }

            internal int After { get; }

            internal int At { get; }

            internal GeoPoint2 Origin { get; }

            internal GeoPoint2 BasePoint { get; }
        }

        /// <summary>
        /// The three items of a hatch's menu that ask for a value, with the command line text AutoCAD 2021 gives two of them:
        /// "** HATCH SCALE **" and "Specify hatch scale:" (a number, or a point whose distance from the hatch's grip is the
        /// scale), "** HATCH ANGLE **" and "Specify hatch angle:" (an angle, or a point whose direction from the grip is the
        /// angle), "** ORIGIN POINT **" and "Specify origin point:". The hatch is previewed with the cursor; Enter or Esc
        /// leaves it as it was.
        /// </summary>
        private static IEnumerable<CadPrompt> GripHatch(
            CadCommandContext c,
            string handle,
            int gripIndex,
            CadGripAction action
        )
        {
            GeoPoint2 centre = CadGrips.Of(c.Document.FindEntity(handle))[gripIndex].Point;
            bool scale = action == CadGripAction.HatchScale;
            bool angle = action == CadGripAction.HatchAngle;
            CadPreview preview = CadPreview.Reshaping(c, handle, (e, point) =>
            {
                GeoVector2 way = point - centre;
                return scale ? CadGrips.HatchScaleAt(e, way.Length)
                    : angle ? CadGrips.HatchAngleAt(e, Math.Atan2(way.Y, way.X))
                    : CadGrips.HatchOriginAt(e, point);
            });
            c.Write(
                scale ? "** HATCH SCALE **"
                : angle ? "** HATCH ANGLE **"
                : "** ORIGIN POINT **"
            );

            while (true)
            {
                CadPrompt asked =
                    scale ? CadPrompt.ForDistance("Specify hatch scale", centre)
                    : angle ? CadPrompt.ForAngle("Specify hatch angle", centre)
                    : CadPrompt.ForPoint("Specify origin point", centre);
                yield return asked.Previewing(preview);
                CadInput input = c.Input;

                if (input.Kind != CadInputKind.Point && input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                c.OpenUndoStep();
                CadEditResult result =
                    scale ? CadGrips.ChangeHatchScale(c.Document, handle, input.Number)
                    : angle ? CadGrips.ChangeHatchAngle(c.Document, handle, input.Number)
                    : CadGrips.SetHatchOrigin(c.Document, handle, input.Point);

                if (result.Succeeded)
                {
                    yield break;
                }

                c.Write(result.Message);
            }
        }

        /// <summary>
        /// Lengthen of an end of a line or an arc, or Radius of the middle of an arc, as AutoCAD 2021's string table words
        /// them: "** LENGTHEN **" and "Specify end point:" ("Specify end point of arc:" for an arc), "** RADIUS **" and "Specify
        /// radius:", the entity previewed with the cursor, with Base point (the point is then taken as far from the base as the
        /// grip is from where it stands), Copy, Undo and eXit, as the stretch of a grip has them. A point makes the edit and
        /// ends; with Copy on each point makes a copy of the entity with the edit and asks again (the original stays, Undo takes
        /// the last copy back). Enter or Esc leaves the entity as it was.
        /// </summary>
        private static IEnumerable<CadPrompt> GripLengthen(
            CadCommandContext c,
            string handle,
            int gripIndex,
            bool radius
        )
        {
            DxfEntity entity = c.Document.FindEntity(handle);
            string message =
                radius ? "Specify radius"
                : entity is DxfArc ? "Specify end point of arc"
                : "Specify end point";

            return GripAskPoint(
                c,
                handle,
                radius ? "** RADIUS **" : "** LENGTHEN **",
                message,
                CadGrips.Of(entity)[gripIndex].Point,
                (e, target) => radius ? CadGrips.RadiusAt(e, target) : CadGrips.LengthenAt(e, gripIndex, target),
                (edited, target) =>
                    radius
                        ? CadGrips.ChangeRadius(c.Document, edited, gripIndex, target)
                        : CadGrips.Lengthen(c.Document, edited, gripIndex, target),
                true
            );
        }

        /// <summary>
        /// A menu item that asks for one point, with Base point and eXit: the command line says the header, the entity is
        /// previewed with the point at the cursor (<paramref name="preview"/> makes the edit on a copy), and a point makes
        /// the edit (<paramref name="apply"/>, given the handle to edit) and ends. With a Base point given the point is taken
        /// as far from the base as the grip is from where it stands (<paramref name="origin"/>). Where
        /// <paramref name="copying"/> says so it also has Copy and Undo, as a grip's stretch has: with Copy on a point edits a
        /// copy of the entity, leaves the original and asks again, with the header saying "(multiple)", and Undo takes the last
        /// copy back. An edit that is refused says why and asks again; Enter, Esc and eXit leave the entity as it was.
        /// </summary>
        private static IEnumerable<CadPrompt> GripAskPoint(
            CadCommandContext c,
            string handle,
            string header,
            string message,
            GeoPoint2 origin,
            Func<DxfEntity, GeoPoint2, bool> preview,
            Func<string, GeoPoint2, CadEditResult> apply,
            bool copying
        )
        {
            GeoPoint2 basePoint = origin;
            CadPreview shown = CadPreview.Reshaping(c, handle, (e, point) => preview(e, origin + (point - basePoint)));
            string[] keywords = copying
                ? new[] { "Base point", "Copy", "Undo", "eXit" }
                : new[] { "Base point", "eXit" };
            var placed = new List<string>();
            bool copy = false;
            c.Write(header);

            while (true)
            {
                yield return CadPrompt.ForPoint(message, basePoint, keywords).Previewing(shown);
                CadInput input = c.Input;

                if (input.IsNone || (input.Kind == CadInputKind.Keyword && input.Is("eXit")))
                {
                    yield break;
                }

                if (input.Kind == CadInputKind.Keyword)
                {
                    if (input.Is("Base point"))
                    {
                        yield return CadPrompt.ForPoint("Specify base point");

                        if (c.Input.IsPoint)
                        {
                            basePoint = c.Input.Point;
                        }
                    }
                    else if (copying && input.Is("Copy"))
                    {
                        copy = true;
                        c.Write(header.Substring(0, header.Length - 3) + " (multiple) **");
                    }
                    else if (copying && input.Is("Undo") && placed.Count > 0)
                    {
                        EraseAll(c, new[] { placed[placed.Count - 1] });
                        placed.RemoveAt(placed.Count - 1);
                    }

                    continue;
                }

                if (input.Kind != CadInputKind.Point && input.Kind != CadInputKind.Number)
                {
                    continue;
                }

                c.OpenUndoStep();
                string edited = handle;

                if (copy)
                {
                    List<string> copied = Transform(
                        c,
                        new[] { handle },
                        GeoTransform3.Translation(new GeoVector3(0.0, 0.0, 0.0)),
                        true
                    );

                    if (copied.Count == 0)
                    {
                        yield break;
                    }

                    edited = copied[0];
                }

                CadEditResult result = apply(edited, origin + (input.Point - basePoint));

                if (result.Succeeded)
                {
                    if (!copy)
                    {
                        yield break;
                    }

                    placed.Add(edited);
                    continue;
                }

                c.Write(result.Message);

                // A copy whose edit was refused is taken back at once.
                if (copy)
                {
                    EraseAll(c, new[] { edited });
                }
            }
        }

        /// <summary>
        /// "** CONVERT TO LINE **" and "Pick to convert to line segment:", as AutoCAD 2021's string table words it: any point
        /// picked makes the arc segment straight, and Enter or Esc leaves it as it was.
        /// </summary>
        private static IEnumerable<CadPrompt> GripConvertToLine(CadCommandContext c, string handle, int gripIndex)
        {
            c.Write("** CONVERT TO LINE **");
            yield return CadPrompt.ForPoint("Pick to convert to line segment");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            c.OpenUndoStep();
            CadEditResult result = CadGrips.ConvertToLine(c.Document, handle, gripIndex);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>"** CONVERT TO ARC **" and "Specify midpoint of arc segment:", the polyline previewed bent through the cursor; a point makes the arc.</summary>
        private static IEnumerable<CadPrompt> GripConvertToArc(CadCommandContext c, string handle, int gripIndex)
        {
            DxfEntity polyline = c.Document.FindEntity(handle);
            int segment = CadGrips.VertexAfter(polyline, CadGrips.Of(polyline)[gripIndex], gripIndex);
            CadPreview preview = CadPreview.Reshaping(c, handle, (entity, point) =>
                CadGrips.BendIntoAt(entity, segment, point)
            );
            c.Write("** CONVERT TO ARC **");

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify midpoint of arc segment").Previewing(preview);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                c.OpenUndoStep();
                CadEditResult result = CadGrips.ConvertToArc(c.Document, handle, gripIndex, c.Input.Point);

                if (result.Succeeded)
                {
                    yield break;
                }

                c.Write(result.Message);
            }
        }
    }
}
