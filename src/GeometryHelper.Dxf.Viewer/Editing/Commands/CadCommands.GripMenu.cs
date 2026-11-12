using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The items of a multi-functional grip's hover menu (<see cref="CadCommandSession.StartGripAction"/>): the grip edit as a click on
    /// the grip makes it, Add Vertex and Convert to Arc with their prompts, Remove Vertex and Convert to Line done at once.
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
                    foreach (CadPrompt prompt in GripAddVertex(c, handle, gripIndex))
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
                default:
                    c.OpenUndoStep();
                    CadEditResult result =
                        action == CadGripAction.RemoveVertex
                            ? CadGrips.RemoveVertex(c.Document, handle, gripIndex)
                            : CadGrips.ConvertToLine(c.Document, handle, gripIndex);

                    if (!result.Succeeded)
                    {
                        c.Write(result.Message);
                    }

                    break;
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
            var polyline = (DxfPolyline)c.Document.FindEntity(handle);
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

        /// <summary>"** CONVERT TO ARC **" and "Specify midpoint of arc segment:", the polyline previewed bent through the cursor; a point makes the arc.</summary>
        private static IEnumerable<CadPrompt> GripConvertToArc(CadCommandContext c, string handle, int gripIndex)
        {
            var polyline = (DxfPolyline)c.Document.FindEntity(handle);
            int segment = gripIndex - polyline.NumberOfVertices;
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
