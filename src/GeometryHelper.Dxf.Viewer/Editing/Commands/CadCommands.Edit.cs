using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The edit commands: MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP, LAYER, UNDO, U, REDO.</summary>
    internal static partial class CadCommands
    {
        private static readonly GeoVector3 Up = new GeoVector3(0.0, 0.0, 1.0);

        private static IEnumerable<CadPrompt> Move(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            var move = new Ref<GeoVector2?>();

            foreach (CadPrompt prompt in Displacement(c, move))
            {
                yield return prompt;
            }

            if (move.Value.HasValue)
            {
                Transform(c, c.Selected, GeoTransform3.Translation(new GeoVector3(move.Value.Value.X, move.Value.Value.Y, 0.0)), false, "Move", Values(move.Value.Value.X, move.Value.Value.Y));
            }
        }

        private static IEnumerable<CadPrompt> Copy(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            c.Write("Current settings:  Copy mode = Multiple");
            yield return CadPrompt.ForPoint("Specify base point", null, new[] { "Displacement" }, "Displacement");

            if (!c.Input.IsPoint)
            {
                var once = new Ref<GeoVector2?>();
                GeoVector2 last = new GeoVector2(c.Remembered("displacement.x", 0.0), c.Remembered("displacement.y", 0.0));
                yield return CadPrompt.ForPoint("Specify displacement", null, null, Length(last.X) + ", " + Length(last.Y) + ", " + Length(0.0));
                once.Value = c.Input.IsPoint ? AsDisplacement(c, c.Input.Point) : last;
                Remember(c, once.Value.Value);
                Transform(c, c.Selected, GeoTransform3.Translation(new GeoVector3(once.Value.Value.X, once.Value.Value.Y, 0.0)), true);
                yield break;
            }

            GeoPoint2 from = c.Input.Point;
            var placed = new List<List<string>>();
            yield return CadPrompt.ForPoint("Specify second point or", from, null, "use first point as displacement");

            if (c.Input.IsNone)
            {
                placed.Add(Transform(c, c.Selected, GeoTransform3.Translation(new GeoVector3(AsDisplacement(c, from).X, AsDisplacement(c, from).Y, 0.0)), true));
                yield break;
            }

            while (true)
            {
                if (c.Input.IsPoint)
                {
                    GeoVector2 move = c.Input.Point - from;
                    placed.Add(Transform(c, c.Selected, GeoTransform3.Translation(new GeoVector3(move.X, move.Y, 0.0)), true));
                }
                else if (c.Input.Is("Undo"))
                {
                    if (placed.Count > 0)
                    {
                        EraseAll(c, placed[placed.Count - 1]);
                        placed.RemoveAt(placed.Count - 1);
                    }
                }
                else
                {
                    yield break;
                }

                yield return CadPrompt.ForPoint("Specify second point", from, new[] { "Exit", "Undo" }, "Exit");

                if (c.Input.Is("Exit"))
                {
                    yield break;
                }
            }
        }

        private static IEnumerable<CadPrompt> Erase(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count > 0)
            {
                EraseAll(c, c.Selected);
            }
        }

        private static IEnumerable<CadPrompt> Rotate(CadCommandContext c)
        {
            c.Write("Current positive angle in UCS:  ANGDIR=counterclockwise  ANGBASE=0");

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify base point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 center = c.Input.Point;
            bool copy = false;
            double last = c.Remembered("rotate", 0.0);

            double angle;

            while (true)
            {
                yield return CadPrompt.ForAngle("Specify rotation angle", center, new[] { "Copy", "Reference" }, Number(last * 180.0 / Math.PI));

                if (c.Input.Is("Copy"))
                {
                    copy = true;
                    c.Write("Rotating a copy of the selected objects.");
                    continue;
                }

                if (c.Input.Is("Reference"))
                {
                    // The objects turned from a reference angle to a new one, each typed or given by two points.
                    var reference = new Ref<double?>();

                    foreach (CadPrompt prompt in Measured(c, "Specify the reference angle", Number(0.0), true, reference))
                    {
                        yield return prompt;
                    }

                    if (!reference.Value.HasValue)
                    {
                        yield break;
                    }

                    yield return CadPrompt.ForAngle("Specify the new angle", center, new[] { "Points" }, Number(0.0));
                    var target = new Ref<double?>();

                    if (c.Input.Is("Points"))
                    {
                        foreach (CadPrompt prompt in TwoPoints(c, true, target))
                        {
                            yield return prompt;
                        }
                    }
                    else
                    {
                        target.Value = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 0.0;
                    }

                    if (!target.Value.HasValue)
                    {
                        yield break;
                    }

                    angle = target.Value.Value - reference.Value.Value;
                    break;
                }

                angle = c.Input.Kind == CadInputKind.Number ? c.Input.Number : last;
                break;
            }

            c.Remember("rotate", angle);
            Transform(c, c.Selected, GeoTransform3.RotationAxis(P3(center), Up, angle), copy, "Rotate", Values(center.X, center.Y, angle));
        }

        private static IEnumerable<CadPrompt> Scale(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify base point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 center = c.Input.Point;
            bool copy = false;
            double last = c.Remembered("scale", 1.0);
            double factor;

            while (true)
            {
                yield return CadPrompt.ForDistance("Specify scale factor", center, new[] { "Copy", "Reference" }, Length(last));

                if (c.Input.Is("Copy"))
                {
                    copy = true;
                    c.Write("Scaling a copy of the selected objects.");
                    continue;
                }

                if (c.Input.Is("Reference"))
                {
                    // The objects scaled so that a reference length becomes a new one, each typed or given by two points.
                    var reference = new Ref<double?>();

                    foreach (CadPrompt prompt in Measured(c, "Specify reference length", Length(1.0), false, reference))
                    {
                        yield return prompt;
                    }

                    if (!reference.Value.HasValue)
                    {
                        yield break;
                    }

                    yield return CadPrompt.ForDistance("Specify new length", center, new[] { "Points" }, Length(1.0));
                    var target = new Ref<double?>();

                    if (c.Input.Is("Points"))
                    {
                        foreach (CadPrompt prompt in TwoPoints(c, false, target))
                        {
                            yield return prompt;
                        }
                    }
                    else
                    {
                        target.Value = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 1.0;
                    }

                    if (!target.Value.HasValue)
                    {
                        yield break;
                    }

                    if (!(reference.Value.Value > c.Session.Tolerance.EqualPoint))
                    {
                        c.Write("The reference length must be above nought.");
                        continue;
                    }

                    factor = target.Value.Value / reference.Value.Value;
                }
                else
                {
                    factor = c.Input.Kind == CadInputKind.Number ? c.Input.Number : last;
                }

                if (factor > 0.0)
                {
                    break;
                }

                c.Write("Value must be positive and nonzero.");
            }

            c.Remember("scale", factor);
            Transform(c, c.Selected, GeoTransform3.Scaling(P3(center), factor), copy, "Scale", Values(center.X, center.Y, factor));
        }

        private static IEnumerable<CadPrompt> Mirror(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify first point of mirror line");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;
            GeoPoint2 second;

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify second point of mirror line", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                second = c.Input.Point;

                if ((second - first).Length > c.Session.Tolerance.EqualPoint)
                {
                    break;
                }

                c.Write("Invalid point: the mirror line needs two points apart.");
            }

            yield return CadPrompt.ForKeyword("Erase source objects?", new[] { "Yes", "No" }, "No");
            GeoVector2 along = second - first;
            var normal = new GeoVector3(-along.Y, along.X, 0.0).Normalize();
            Transform(c, c.Selected, GeoTransform3.Mirror(new GeoPlane3(P3(first), normal)), !c.Input.Is("Yes"), "Mirror", Values(first.X, first.Y, second.X, second.Y));
        }

        private static IEnumerable<CadPrompt> Explode(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var parts = new List<KeyValuePair<string, IReadOnlyList<DxfEntity>>>();
            int refused = 0;

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                IReadOnlyList<DxfEntity> pieces = null;

                // An entity of an owner is not exploded: its owner would take the erase as its own and the pieces be lost.
                if (entity != null && c.Document.Owners.GetEditRule(entity) != CadEditRule.Free)
                {
                    c.Write("This " + (entity is DxfDimension ? "dimension" : CadOwnerEditGate.Describe(entity)) + " belongs to the model and cannot be exploded here.");
                    continue;
                }

                try
                {
                    pieces = entity.Explode();
                }
                catch (NotSupportedException)
                {
                    pieces = null;
                }
                catch (InvalidOperationException)
                {
                    pieces = null;
                }

                if (pieces == null || pieces.Count == 0)
                {
                    refused++;
                    continue;
                }

                parts.Add(new KeyValuePair<string, IReadOnlyList<DxfEntity>>(handle, pieces));
            }

            if (refused > 0)
            {
                c.Write(refused.ToString(CultureInfo.InvariantCulture) + (refused == 1 ? " was" : " were") + " not able to be exploded.");
            }

            if (parts.Count == 0)
            {
                yield break;
            }

            var planned = new List<CadEditRecord>();

            foreach (KeyValuePair<string, IReadOnlyList<DxfEntity>> pair in parts)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, pair.Key));
            }

            c.Apply(planned, s =>
            {
                foreach (KeyValuePair<string, IReadOnlyList<DxfEntity>> pair in parts)
                {
                    if (s.IsHandled(pair.Key))
                    {
                        continue;
                    }

                    CadSpace space = s.Document.FindSpace(pair.Key);
                    DxfEntity entity = s.Document.FindEntity(pair.Key);

                    foreach (DxfEntity piece in pair.Value)
                    {
                        s.Append(space, piece);
                    }

                    entity.Erase();
                }
            });
        }

        private static IEnumerable<CadPrompt> MatchProperties(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select source object");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string source = c.Input.Handles[0];
            c.Write("Current active settings: Color Layer Ltype Ltscale Lineweight Transparency");
            yield return CadPrompt.ForSelection("Select destination object(s)");
            var targets = new List<string>();

            foreach (string handle in c.Input.Handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null || string.Equals(handle, source, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    c.Write("1 was on a locked layer.");
                    continue;
                }

                targets.Add(handle);
            }

            if (targets.Count == 0)
            {
                yield break;
            }

            var planned = new List<CadEditRecord>();

            foreach (string handle in targets)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Match properties", null, source));
            }

            c.Apply(planned, s =>
            {
                DxfEntity from = s.Document.FindEntity(source);

                foreach (string handle in targets)
                {
                    if (s.IsHandled(handle))
                    {
                        continue;
                    }

                    DxfEntity to = s.Document.FindEntity(handle);
                    to.Layer = from.Layer;
                    to.Color = from.Color;
                    to.Linetype = from.Linetype;
                    to.LinetypeScale = from.LinetypeScale;
                    to.LineWeight = from.LineWeight;
                    if (!Equals(to.Transparency, from.Transparency))
                    {
                        to.Transparency = from.Transparency;
                    }
                }
            });
        }

        private static IEnumerable<CadPrompt> Layer(CadCommandContext c)
        {
            if (c.Session.Request("LAYER"))
            {
                yield break;
            }

            string[] options = { "Set", "New", "ON", "OFF", "Freeze", "Thaw", "LOck", "Unlock" };
            c.Write("Current layer:  \"" + c.Database.Clayer + "\"");

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an option", options);

                if (c.Input.IsNone)
                {
                    yield break;
                }

                string option = c.Input.Text;
                string ask;

                switch (option)
                {
                    case "Set": ask = "Enter layer name to make current"; break;
                    case "New": ask = "Enter name list for new layer(s)"; break;
                    case "ON": ask = "Enter name list of layer(s) to turn On"; break;
                    case "OFF": ask = "Enter name list of layer(s) to turn Off"; break;
                    case "Freeze": ask = "Enter name list of layer(s) to freeze"; break;
                    case "Thaw": ask = "Enter name list of layer(s) to thaw"; break;
                    case "LOck": ask = "Enter name list of layer(s) to lock"; break;
                    default: ask = "Enter name list of layer(s) to unlock"; break;
                }

                yield return CadPrompt.ForString(ask);

                if (c.Input.Kind != CadInputKind.Text)
                {
                    continue;
                }

                c.OpenUndoStep();

                foreach (string piece in c.Input.Text.Split(','))
                {
                    string name = piece.Trim();

                    if (name.Length == 0)
                    {
                        continue;
                    }

                    CadEditResult result;

                    switch (option)
                    {
                        case "Set": result = CadLayerEditor.SetCurrent(c.Document, name); break;
                        case "New": result = CadLayerEditor.Add(c.Document, name); break;
                        case "ON": result = CadLayerEditor.SetOn(c.Document, name, true); break;
                        case "OFF": result = CadLayerEditor.SetOn(c.Document, name, false); break;
                        case "Freeze": result = CadLayerEditor.SetFrozen(c.Document, name, true); break;
                        case "Thaw": result = CadLayerEditor.SetFrozen(c.Document, name, false); break;
                        case "LOck": result = CadLayerEditor.SetLocked(c.Document, name, true); break;
                        default: result = CadLayerEditor.SetLocked(c.Document, name, false); break;
                    }

                    if (!result.Succeeded)
                    {
                        c.Write(result.Message);
                    }
                }
            }
        }

        private static IEnumerable<CadPrompt> Undo(CadCommandContext c)
        {
            yield return CadPrompt.ForInteger("Enter the number of operations to undo", null, "1");
            int count = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 1;

            if (count <= 0)
            {
                c.Write("Requires a positive integer.");
                yield break;
            }

            UndoSteps(c, count);
        }

        private static IEnumerable<CadPrompt> UndoOne(CadCommandContext c)
        {
            UndoSteps(c, 1);
            yield break;
        }

        private static void UndoSteps(CadCommandContext c, int count)
        {
            for (int i = 0; i < count; i++)
            {
                CadUndoStep step = c.Document.NextUndo;

                if (step == null || !c.Document.Undo())
                {
                    c.Write("Everything has been undone");
                    return;
                }

                c.Write(step.Name);
            }
        }

        private static IEnumerable<CadPrompt> Redo(CadCommandContext c)
        {
            CadUndoStep step = c.Document.NextRedo;

            if (step == null || !c.Document.Redo())
            {
                c.Write("Nothing to redo.");
                yield break;
            }

            c.Write(step.Name);
        }

        /// <summary>
        /// Asks a reference angle or length as AutoCAD's Reference option does: typed as a number (an angle in degrees), Enter
        /// for the default (nought degrees, a length of 1), or by two points, the second asked after the first.
        /// </summary>
        private static IEnumerable<CadPrompt> Measured(CadCommandContext c, string message, string offered, bool angle, Ref<double?> result)
        {
            yield return CadPrompt.ForPoint(message, null, null, offered, true);

            if (c.Input.IsNone)
            {
                result.Value = angle ? 0.0 : 1.0;
            }
            else if (c.Input.Kind == CadInputKind.Text)
            {
                if (double.TryParse(c.Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed) && !double.IsNaN(typed) && !double.IsInfinity(typed))
                {
                    result.Value = angle ? typed * Math.PI / 180.0 : typed;
                }
                else
                {
                    c.Write("Requires a number or two points.");
                }
            }
            else if (c.Input.IsPoint)
            {
                GeoPoint2 first = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second point", first);

                if (c.Input.IsPoint)
                {
                    GeoVector2 between = c.Input.Point - first;
                    result.Value = angle ? c.Session.Ucs.AngleToUcs(Math.Atan2(between.Y, between.X)) : between.Length;
                }
            }
        }

        /// <summary>Asks two points and gives the angle of the line from the first to the second, or its length.</summary>
        private static IEnumerable<CadPrompt> TwoPoints(CadCommandContext c, bool angle, Ref<double?> result)
        {
            yield return CadPrompt.ForPoint("Specify first point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second point", first);

            if (c.Input.IsPoint)
            {
                GeoVector2 between = c.Input.Point - first;
                result.Value = angle ? c.Session.Ucs.AngleToUcs(Math.Atan2(between.Y, between.X)) : between.Length;
            }
        }

        /// <summary>
        /// A point given as a displacement, as "use first point as displacement" takes one: its coordinates in the UCS, turned
        /// into the world's. In a UCS turned a quarter at (100, 0), 10,0 typed moves objects 10 up the world's Y.
        /// </summary>
        private static GeoVector2 AsDisplacement(CadCommandContext c, GeoPoint2 point)
        {
            CadUcs ucs = c.Session.Ucs;
            return ucs.IsWorld ? new GeoVector2(point.X, point.Y) : point - new GeoPoint2(ucs.Origin.X, ucs.Origin.Y);
        }

        /// <summary>Asks the base point and the second point, or a displacement, as MOVE asks them.</summary>
        private static IEnumerable<CadPrompt> Displacement(CadCommandContext c, Ref<GeoVector2?> result)
        {
            yield return CadPrompt.ForPoint("Specify base point", null, new[] { "Displacement" }, "Displacement");

            if (c.Input.IsPoint)
            {
                GeoPoint2 from = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second point or", from, null, "use first point as displacement");
                result.Value = c.Input.IsPoint ? c.Input.Point - from : AsDisplacement(c, from);
                yield break;
            }

            if (!c.Input.Is("Displacement"))
            {
                yield break;
            }

            var last = new GeoVector2(c.Remembered("displacement.x", 0.0), c.Remembered("displacement.y", 0.0));
            yield return CadPrompt.ForPoint("Specify displacement", null, null, Length(last.X) + ", " + Length(last.Y) + ", " + Length(0.0));
            result.Value = c.Input.IsPoint ? AsDisplacement(c, c.Input.Point) : last;
            Remember(c, result.Value.Value);
        }

        private static void Remember(CadCommandContext c, GeoVector2 displacement)
        {
            c.Remember("displacement.x", displacement.X);
            c.Remember("displacement.y", displacement.Y);
        }

        /// <summary>Moves, turns, scales or mirrors entities, or copies of them; gives the handles of the copies.</summary>
        private static List<string> Transform(CadCommandContext c, IReadOnlyList<string> handles, GeoTransform3 transform, bool copy, string how = "Move", string value = null)
        {
            var planned = new List<CadEditRecord>();

            if (!copy)
            {
                foreach (string handle in handles)
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.Transformed, handle, how, null, value));
                }
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    if (!copy && s.IsHandled(handle))
                    {
                        continue;
                    }

                    DxfEntity entity = s.Document.FindEntity(handle);

                    if (copy)
                    {
                        DxfEntity copied = entity.GetTransformedCopy(transform);
                        CadOwnerTag.Remove(copied);
                        s.Append(s.Document.FindSpace(handle), copied);
                    }
                    else
                    {
                        entity.TransformBy(transform);
                    }
                }
            });

            var made = new List<string>();

            foreach (CadEditRecord record in result.Records)
            {
                if (record.Kind == CadEditRecordKind.Added)
                {
                    made.Add(record.Handle);
                }
            }

            return made;
        }

        private static void EraseAll(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var planned = new List<CadEditRecord>();

            foreach (string handle in handles)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, handle));
            }

            c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    if (!s.IsHandled(handle))
                    {
                        s.Document.FindEntity(handle).Erase();
                    }
                }
            });
        }

        /// <summary>A value an iterator gives back to the body that ran it.</summary>
        private sealed class Ref<T>
        {
            public T Value { get; set; }
        }
    }
}
