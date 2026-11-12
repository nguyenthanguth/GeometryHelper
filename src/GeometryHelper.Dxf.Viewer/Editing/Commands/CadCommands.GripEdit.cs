using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The grip modes of AutoCAD 2021, which a click on a grip starts (<see cref="CadCommandSession.StartGripEdit"/>): STRETCH,
    /// MOVE, ROTATE, SCALE and MIRROR in turn as Enter or Space is pressed, each with Base point, Copy, Undo and eXit, ROTATE and
    /// SCALE with Reference as well.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] GripModes = { "STRETCH", "MOVE", "ROTATE", "SCALE", "MIRROR" };

        // The modes typed at a grip prompt, STRETCH or ST and the others, taken but not shown, in the order of GripModes.
        private static readonly string[] GripModeWords = { "STretch", "MOve", "ROtate", "SCale", "MIrror" };

        /// <summary>
        /// A grip edit: "** STRETCH **" and "Specify stretch point or [Base point/Copy/Undo/eXit]:" first, the base point the
        /// first hot grip's. A point given makes the mode's edit and ends the grip edit, or with Copy on leaves the originals,
        /// asks again and ends at eXit, Enter or Esc. Every edit of it is one undo step.
        /// </summary>
        internal static IEnumerable<CadPrompt> GripEdit(
            CadCommandContext c,
            IReadOnlyList<KeyValuePair<string, int>> hot
        )
        {
            List<string> objects = GripObjects(c, hot);
            var origins = new List<GeoPoint2>();

            foreach (KeyValuePair<string, int> grip in hot)
            {
                origins.Add(CadGrips.Of(c.Document.FindEntity(grip.Key))[grip.Value].Point);
            }

            GeoPoint2 basePoint = origins[0];
            var placed = new List<List<string>>();
            int mode = 0;
            bool copy = false;
            bool first = true;

            // One custom grip dragged previews its dynamic block worked out at the cursor, as AutoCAD 2021 redraws it.
            CadGrip custom = hot.Count == 1 ? CadGrips.Of(c.Document.FindEntity(hot[0].Key))[hot[0].Value] : null;
            CadPreview reshaped =
                custom?.Custom == null || custom.Kind == CadGripKind.CustomFlip || custom.Kind == CadGripKind.CustomList
                    ? null
                    : CadPreview.Reshaping(c, hot[0].Key, (entity, point) =>
                    {
                        Action<DxfBlockReference> write = CadDynamicBlocks.Plan(
                            (DxfBlockReference)entity,
                            custom,
                            origins[0] + (point - basePoint),
                            out _
                        );
                        write?.Invoke((DxfBlockReference)entity);
                        return write != null;
                    });

            while (true)
            {
                c.Write("** " + GripModes[mode] + (copy ? " (multiple)" : string.Empty) + " **");
                CadPrompt asked = GripPrompt(mode, basePoint);
                yield return mode == 0 && reshaped != null ? asked.Previewing(reshaped) : asked;
                CadInput input = c.Input;

                if (input.IsNone)
                {
                    // Enter or Space goes on to the next mode, after MIRROR back to STRETCH; once copies are made it ends.
                    if (placed.Count > 0)
                    {
                        yield break;
                    }

                    mode = (mode + 1) % GripModes.Length;
                    continue;
                }

                if (input.Kind == CadInputKind.Keyword)
                {
                    int jumped = Array.FindIndex(GripModeWords, w => input.Is(w));

                    if (jumped >= 0)
                    {
                        mode = jumped;
                    }
                    else if (input.Is("eXit"))
                    {
                        yield break;
                    }
                    else if (input.Is("Copy"))
                    {
                        copy = true;
                    }
                    else if (input.Is("Undo"))
                    {
                        if (placed.Count > 0)
                        {
                            EraseAll(c, placed[placed.Count - 1]);
                            placed.RemoveAt(placed.Count - 1);
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
                    else if (input.Is("Reference"))
                    {
                        var amount = new Ref<double?>();

                        foreach (CadPrompt prompt in GripReference(c, mode == 2, basePoint, amount))
                        {
                            yield return prompt;
                        }

                        if (
                            amount.Value.HasValue
                            && GripApply(
                                c,
                                mode,
                                objects,
                                hot,
                                origins,
                                basePoint,
                                amount.Value.Value,
                                default,
                                copy,
                                placed
                            )
                            && !copy
                        )
                        {
                            yield break;
                        }
                    }

                    continue;
                }

                if (input.Kind != CadInputKind.Point && input.Kind != CadInputKind.Number)
                {
                    continue;
                }

                // Ctrl held as the first point is picked turns Copy on, as AutoCAD's.
                copy |= first && input.Control;
                first = false;

                if (
                    GripApply(c, mode, objects, hot, origins, basePoint, input.Number, input.Point, copy, placed)
                    && !copy
                )
                {
                    yield break;
                }
            }
        }

        /// <summary>The prompt of a mode: a point for STRETCH, MOVE and MIRROR, an angle for ROTATE, a factor for SCALE, each from the base point.</summary>
        private static CadPrompt GripPrompt(int mode, GeoPoint2 basePoint)
        {
            switch (mode)
            {
                case 2:
                    return CadPrompt
                        .ForAngle(
                            "Specify rotation angle",
                            basePoint,
                            new[] { "Base point", "Copy", "Undo", "Reference", "eXit" }
                        )
                        .Turning()
                        .Hiding(GripModeWords);
                case 3:
                    return CadPrompt
                        .ForDistance(
                            "Specify scale factor",
                            basePoint,
                            new[] { "Base point", "Copy", "Undo", "Reference", "eXit" }
                        )
                        .Hiding(GripModeWords);
                default:
                    string message =
                        mode == 0 ? "Specify stretch point"
                        : mode == 1 ? "Specify move point"
                        : "Specify second point";
                    return CadPrompt
                        .ForPoint(message, basePoint, new[] { "Base point", "Copy", "Undo", "eXit" })
                        .Hiding(GripModeWords);
            }
        }

        /// <summary>
        /// ROTATE's or SCALE's Reference: the reference angle or length, typed or given by two points, then the new one; the
        /// angle turned through or the factor scaled by.
        /// </summary>
        private static IEnumerable<CadPrompt> GripReference(
            CadCommandContext c,
            bool angle,
            GeoPoint2 basePoint,
            Ref<double?> result
        )
        {
            var reference = new Ref<double?>();

            foreach (
                CadPrompt prompt in Measured(
                    c,
                    angle ? "Specify reference angle" : "Specify reference length",
                    angle ? Counted(c, 0.0) : Length(1.0),
                    angle,
                    reference
                )
            )
            {
                yield return prompt;
            }

            if (!reference.Value.HasValue)
            {
                yield break;
            }

            yield return angle
                ? CadPrompt.ForAngle("Specify new angle", basePoint, new[] { "Points" }, Counted(c, 0.0))
                : CadPrompt.ForDistance("Specify new length", basePoint, new[] { "Points" }, Length(1.0));
            var target = new Ref<double?>();

            if (c.Input.Is("Points"))
            {
                foreach (CadPrompt prompt in TwoPoints(c, angle, target))
                {
                    yield return prompt;
                }
            }
            else
            {
                target.Value =
                    c.Input.Kind == CadInputKind.Number ? c.Input.Number
                    : angle ? 0.0
                    : 1.0;
            }

            if (!target.Value.HasValue)
            {
                yield break;
            }

            if (angle)
            {
                result.Value = target.Value.Value - reference.Value.Value;
            }
            else if (reference.Value.Value > c.Session.Tolerance.EqualPoint)
            {
                result.Value = target.Value.Value / reference.Value.Value;
            }
            else
            {
                c.Write("The reference length must be above nought.");
            }
        }

        /// <summary>
        /// Makes a mode's edit: STRETCH moves each hot grip by the way from the base point to the point, MOVE moves the objects
        /// so, ROTATE turns them by the angle about the base point, SCALE scales them by the factor from it, MIRROR mirrors them
        /// across the line from it to the point; with Copy on, copies are made and the originals stay.
        /// </summary>
        /// <returns>Whether an edit was made.</returns>
        private static bool GripApply(
            CadCommandContext c,
            int mode,
            IReadOnlyList<string> objects,
            IReadOnlyList<KeyValuePair<string, int>> hot,
            IReadOnlyList<GeoPoint2> origins,
            GeoPoint2 basePoint,
            double number,
            GeoPoint2 point,
            bool copy,
            List<List<string>> placed
        )
        {
            GeoVector2 way = point - basePoint;

            switch (mode)
            {
                case 0:
                    return GripStretch(c, hot, origins, way, copy, placed);
                case 1:
                    return GripTransform(
                        c,
                        objects,
                        GeoTransform3.Translation(new GeoVector3(way.X, way.Y, 0.0)),
                        copy,
                        placed,
                        "Move",
                        Values(way.X, way.Y)
                    );
                case 2:
                    return GripTransform(
                        c,
                        objects,
                        GeoTransform3.RotationAxis(P3(basePoint), Up, number),
                        copy,
                        placed,
                        "Rotate",
                        Values(basePoint.X, basePoint.Y, number)
                    );
                case 3:
                    if (!(number > 0.0))
                    {
                        c.Write("Value must be positive and nonzero.");
                        return false;
                    }

                    return GripTransform(
                        c,
                        objects,
                        GeoTransform3.Scaling(P3(basePoint), number),
                        copy,
                        placed,
                        "Scale",
                        Values(basePoint.X, basePoint.Y, number)
                    );
                default:
                    if (!(way.Length > c.Session.Tolerance.EqualPoint))
                    {
                        c.Write("Invalid point: the mirror line needs two points apart.");
                        return false;
                    }

                    var normal = new GeoVector3(-way.Y, way.X, 0.0).Normalize();
                    return GripTransform(
                        c,
                        objects,
                        GeoTransform3.Mirror(new GeoPlane3(P3(basePoint), normal)),
                        copy,
                        placed,
                        "Mirror",
                        Values(basePoint.X, basePoint.Y, point.X, point.Y)
                    );
            }
        }

        /// <summary>Moves, turns, scales or mirrors the objects, or copies of them, keeping the copies for Undo.</summary>
        private static bool GripTransform(
            CadCommandContext c,
            IReadOnlyList<string> objects,
            GeoTransform3 transform,
            bool copy,
            List<List<string>> placed,
            string how,
            string value
        )
        {
            if (objects.Count == 0)
            {
                return false;
            }

            List<string> made = Transform(c, objects, transform, copy, how, value);

            if (copy)
            {
                placed.Add(made);
            }

            return true;
        }

        /// <summary>
        /// STRETCH: each hot grip dragged by the way, from where it was when the edit started, as the grips of one object move
        /// together; with Copy on, each object of a hot grip copied first and the copy's grip dragged.
        /// </summary>
        private static bool GripStretch(
            CadCommandContext c,
            IReadOnlyList<KeyValuePair<string, int>> hot,
            IReadOnlyList<GeoPoint2> origins,
            GeoVector2 way,
            bool copy,
            List<List<string>> placed
        )
        {
            c.OpenUndoStep();
            var copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var made = new List<string>();
            bool any = false;

            for (int i = 0; i < hot.Count; i++)
            {
                KeyValuePair<string, int> grip = hot[i];
                string handle = grip.Key;

                if (copy)
                {
                    if (!copies.TryGetValue(grip.Key, out handle))
                    {
                        List<string> copied = Transform(
                            c,
                            new[] { grip.Key },
                            GeoTransform3.Translation(new GeoVector3(0.0, 0.0, 0.0)),
                            true
                        );

                        if (copied.Count == 0)
                        {
                            continue;
                        }

                        handle = copied[0];
                        copies[grip.Key] = handle;
                        made.Add(handle);
                    }
                }

                if (c.Document.FindEntity(handle) == null)
                {
                    continue;
                }

                // Each grip goes from where it was as the edit started: a circle's centre and quadrant both hot move the circle
                // and keep its radius, as AutoCAD's.
                CadEditResult result = c.Document.GripDrag(handle, grip.Value, origins[i] + way);

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                }
                else
                {
                    any = true;
                }
            }

            if (copy)
            {
                placed.Add(made);
            }

            return any;
        }

        /// <summary>The objects MOVE, ROTATE, SCALE and MIRROR take: those selected and those of the hot grips, those on a locked layer left out, in ordinal order.</summary>
        private static List<string> GripObjects(CadCommandContext c, IReadOnlyList<KeyValuePair<string, int>> hot)
        {
            var handles = new SortedSet<string>(c.Document.Selection.Handles, StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, int> grip in hot)
            {
                handles.Add(grip.Key);
            }

            var kept = new List<string>();

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && !CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    kept.Add(handle);
                }
            }

            return kept;
        }
    }
}
