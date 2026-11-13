using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// CHANGE (-CH), as AutoCAD 2021 asks it: the objects, then a change point or Properties. A change point moves the end of
    /// each line nearest it, sets the radius of a lone circle, and (or Enter) asks each text for its insertion point, style,
    /// height, rotation and words, and each block for its insertion point and rotation; Properties asks Color, Elev, LAyer,
    /// LType, ltScale, LWeight, Thickness, TRansparency, Material and Annotative until Enter. One undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] ChangeProperties =
        {
            "Color",
            "Elev",
            "LAyer",
            "LType",
            "ltScale",
            "LWeight",
            "Thickness",
            "TRansparency",
            "Material",
            "Annotative",
        };

        private static IEnumerable<CadPrompt> Change(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify change point", null, new[] { "Properties" });

            if (c.Input.Is("Properties"))
            {
                foreach (CadPrompt prompt in ChangeProperty(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (c.Input.Kind != CadInputKind.Point && c.Input.Kind != CadInputKind.None)
            {
                yield break;
            }

            GeoPoint2? at = c.Input.Kind == CadInputKind.Point ? c.Input.Point : (GeoPoint2?)null;
            var objects = new List<DxfEntity>();

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity is DxfLine || entity is DxfCircle || entity is DxfDBText || entity is DxfBlockReference)
                {
                    objects.Add(entity);
                }
            }

            if (objects.Count == 0)
            {
                c.Write("No changeable object selected");
                yield break;
            }

            // A change point serves a lone circle, text or block; with several objects it is for the lines only.
            bool lone = c.Selected.Count == 1;

            foreach (DxfEntity entity in objects)
            {
                switch (entity)
                {
                    case DxfLine line:
                        if (at.HasValue)
                        {
                            ChangeLine(c, line, at.Value);
                        }

                        break;
                    case DxfCircle circle:
                        if (at.HasValue && lone)
                        {
                            double radius = Flat(circle.Center).DistanceTo(at.Value);

                            if (radius > 0.0)
                            {
                                ChangeApply(c, circle, "Radius", Length(circle.Radius), Length(radius), e =>
                                    ((DxfCircle)e).Radius = radius
                                );
                            }

                            break;
                        }

                        if (at.HasValue)
                        {
                            c.Write("Change point ignored");
                        }

                        foreach (CadPrompt prompt in ChangeCircleRadius(c, circle))
                        {
                            yield return prompt;
                        }

                        break;
                    case DxfDBText text:
                        if (at.HasValue && !lone)
                        {
                            c.Write("Change point ignored");
                        }

                        foreach (CadPrompt prompt in ChangeText(c, text, lone ? at : null))
                        {
                            yield return prompt;
                        }

                        break;
                    case DxfBlockReference block:
                        if (at.HasValue && !lone)
                        {
                            c.Write("Change point ignored");
                        }

                        foreach (CadPrompt prompt in ChangeBlock(c, block, lone ? at : null))
                        {
                            yield return prompt;
                        }

                        break;
                }
            }
        }

        /// <summary>Applies one change to one entity, a record of it kept for the canvas, joining the command's undo step.</summary>
        private static void ChangeApply(
            CadCommandContext c,
            DxfEntity entity,
            string label,
            string old,
            string now,
            Action<DxfEntity> write
        )
        {
            string handle = entity.Handle;
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, label, old, now) }, s =>
                write(s.Document.FindEntity(handle))
            );
        }

        /// <summary>
        /// A line given a change point: its end nearest the point moves there, the point's own place, or with ORTHOMODE on the
        /// place that keeps the line horizontal, where the point is further along x than y of the other end, else vertical.
        /// </summary>
        private static void ChangeLine(CadCommandContext c, DxfLine line, GeoPoint2 at)
        {
            GeoPoint3 start = line.StartPoint;
            GeoPoint3 end = line.EndPoint;
            bool moveEnd = Flat(end).DistanceTo(at) < Flat(start).DistanceTo(at);
            GeoPoint3 other = moveEnd ? start : end;
            GeoPoint3 moved = moveEnd ? end : start;
            GeoPoint2 place = at;

            if (c.Database.Orthomode)
            {
                place =
                    Math.Abs(at.X - other.X) > Math.Abs(at.Y - other.Y)
                        ? new GeoPoint2(at.X, other.Y)
                        : new GeoPoint2(other.X, at.Y);
            }

            var now = new GeoPoint3(place.X, place.Y, moved.Z);
            ChangeApply(
                c,
                line,
                moveEnd ? "End" : "Start",
                Values(moved.X, moved.Y, moved.Z),
                Values(now.X, now.Y, now.Z),
                e =>
                {
                    if (moveEnd)
                    {
                        ((DxfLine)e).EndPoint = now;
                    }
                    else
                    {
                        ((DxfLine)e).StartPoint = now;
                    }
                }
            );
        }

        private static IEnumerable<CadPrompt> ChangeCircleRadius(CadCommandContext c, DxfCircle circle)
        {
            while (true)
            {
                yield return CadPrompt.ForDistance("Specify new circle radius", Flat(circle.Center), null, "no change");

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                double radius = c.Input.Number;

                if (!(radius > 0.0))
                {
                    c.Write("Value must be positive and nonzero.");
                    continue;
                }

                ChangeApply(c, circle, "Radius", Length(circle.Radius), Length(radius), e =>
                    ((DxfCircle)e).Radius = radius
                );
                yield break;
            }
        }

        /// <summary>A text: its insertion point (given by a change point, else asked), style, height, rotation and words; attribute definitions go on to the tag, the prompt and the default value.</summary>
        private static IEnumerable<CadPrompt> ChangeText(CadCommandContext c, DxfDBText text, GeoPoint2? given)
        {
            GeoPoint3 held = text.IsDefaultAlignment ? text.Position : text.AlignmentPoint;
            GeoPoint2? place = given;
            var changes = new List<Action<DxfEntity>>();

            if (!place.HasValue)
            {
                yield return CadPrompt.ForPoint("Specify new text insertion point", null, null, "no change");

                if (c.Input.Kind == CadInputKind.Point)
                {
                    place = c.Input.Point;
                }
            }

            if (place.HasValue)
            {
                GeoPoint2 target = place.Value;
                changes.Add(e =>
                    e.TransformBy(GeoTransform3.Translation(new GeoVector3(target.X - held.X, target.Y - held.Y, 0.0)))
                );
            }

            string style = text.TextStyleName;
            yield return CadPrompt.ForString("Enter new text style", style);

            if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
            {
                string typed = c.Input.Text.Trim();

                if (c.Database.TextStyleTable.Has(typed))
                {
                    string chosen = c.Database.TextStyleTable[typed].Name;
                    changes.Add(e => ((DxfDBText)e).TextStyleName = chosen);
                }
                else
                {
                    c.Write("Style \"" + typed + "\" not defined - use STYLE command.");
                }
            }

            GeoPoint2 anchor = Flat(held);

            while (true)
            {
                yield return CadPrompt.ForDistance("Specify new height", anchor, null, Length(text.Height));

                if (c.Input.Kind != CadInputKind.Number)
                {
                    break;
                }

                double height = c.Input.Number;

                if (!(height > 0.0))
                {
                    c.Write("Value must be positive and nonzero.");
                    continue;
                }

                changes.Add(e => ((DxfDBText)e).Height = height);
                break;
            }

            yield return CadPrompt.ForAngle("Specify new rotation angle", anchor, null, Counted(c, text.Rotation));

            if (c.Input.Kind == CadInputKind.Number)
            {
                double angle = c.Session.Ucs.AngleToWorld(c.Input.Number);
                angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
                double turned = angle >= 2.0 * Math.PI ? 0.0 : angle;
                changes.Add(e => ((DxfDBText)e).Rotation = turned);
            }

            var definition = text as DxfAttributeDefinition;

            if (definition != null)
            {
                // An attribute definition goes on to its tag, its prompt and its default value, as AutoCAD asks them.
                yield return CadPrompt.ForString("Enter new tag", definition.Tag);

                if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
                {
                    string tag = c.Input.Text.Trim();

                    if (CadBlocks.IsTag(tag))
                    {
                        changes.Add(e => ((DxfAttributeDefinition)e).Tag = tag);
                    }
                    else
                    {
                        c.Write("Invalid tag.");
                    }
                }

                yield return CadPrompt.ForString("Enter new prompt", definition.Prompt);

                if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Length > 0)
                {
                    string asked = c.Input.Text;
                    changes.Add(e => ((DxfAttributeDefinition)e).Prompt = asked);
                }
            }

            yield return CadPrompt.ForString(
                definition != null ? "Enter new default value" : "Enter new text",
                text.TextString
            );

            if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Length > 0)
            {
                string words = c.Input.Text;
                changes.Add(e => ((DxfDBText)e).TextString = words);
            }

            if (changes.Count > 0)
            {
                string handle = text.Handle;
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle) }, s =>
                {
                    DxfEntity entity = s.Document.FindEntity(handle);

                    foreach (Action<DxfEntity> change in changes)
                    {
                        change(entity);
                    }
                });
            }
        }

        /// <summary>A block: its insertion point (given by a change point, else asked) and rotation.</summary>
        private static IEnumerable<CadPrompt> ChangeBlock(
            CadCommandContext c,
            DxfBlockReference block,
            GeoPoint2? given
        )
        {
            GeoPoint2? place = given;

            if (!place.HasValue)
            {
                yield return CadPrompt.ForPoint("Specify new block insertion point", null, null, "no change");

                if (c.Input.Kind == CadInputKind.Point)
                {
                    place = c.Input.Point;
                }
            }

            yield return CadPrompt.ForAngle(
                "Specify new block rotation angle",
                Flat(block.Position),
                null,
                Counted(c, block.Rotation)
            );
            double? rotation = null;

            if (c.Input.Kind == CadInputKind.Number)
            {
                double angle = c.Session.Ucs.AngleToWorld(c.Input.Number);
                angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
                rotation = angle >= 2.0 * Math.PI ? 0.0 : angle;
            }

            if (!place.HasValue && !rotation.HasValue)
            {
                yield break;
            }

            GeoPoint2? target = place;
            double? turn = rotation;
            string handle = block.Handle;
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle) }, s =>
            {
                var entity = (DxfBlockReference)s.Document.FindEntity(handle);

                if (target.HasValue)
                {
                    entity.Position = new GeoPoint3(target.Value.X, target.Value.Y, entity.Position.Z);
                }

                if (turn.HasValue)
                {
                    entity.Rotation = turn.Value;
                }
            });
        }

        /// <summary>
        /// The Properties option: asks a property, then its value, until Enter. Each prompt offers the value of the run before it,
        /// else the drawing's own (CECOLOR, CLAYER and the like), and Enter on it applies that. A name the drawing lacks is said so.
        /// </summary>
        private static IEnumerable<CadPrompt> ChangeProperty(CadCommandContext c)
        {
            DxfDatabase db = c.Database;
            DxfColor colour = db.Cecolor;
            string layer = db.Clayer;
            string linetype = db.Celtype;
            double elevation = db.Elevation;
            double ltscale = db.Celtscale;
            DxfLineWeight weight = db.Celweight;
            double thickness = db.Thickness;
            DxfTransparency transparency = db.Cetransparency;

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter property to change", ChangeProperties).Hiding("LTScale");

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                if (c.Input.Is("Color"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForKeywordOrText(
                            "New color",
                            new[] { "Truecolor", "COlorbook" },
                            ColorOffered(colour)
                        );

                        if (c.Input.Is("COlorbook"))
                        {
                            c.Write("Color books are not available here.");
                            break;
                        }

                        string typed;

                        if (c.Input.Is("Truecolor"))
                        {
                            yield return CadPrompt.ForString("Red,Green,Blue");
                            typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : null;
                        }
                        else
                        {
                            typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : ChangeColourText(colour);
                        }

                        if (typed == null || typed.Length == 0)
                        {
                            break;
                        }

                        DxfColor parsed;

                        if (
                            (
                                int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                                && (index < 1 || index > 255)
                            )
                            || CadPropertyEditor.ParseColor(typed, out parsed) != null
                        )
                        {
                            c.Write("Invalid color number.");
                            continue;
                        }

                        colour = parsed;
                        DxfColor chosen = parsed;
                        ChangeEach(
                            c,
                            "Color",
                            e => CadPropertyEditor.ColorName(e.Color),
                            CadPropertyEditor.ColorName(chosen),
                            e => e.Color = chosen
                        );
                        break;
                    }
                }
                else if (c.Input.Is("Elev"))
                {
                    yield return CadPrompt.ForDistance("Specify new elevation", null, null, Length(elevation));

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        elevation = c.Input.Number;
                    }
                    else if (c.Input.Kind != CadInputKind.None)
                    {
                        continue;
                    }

                    double level = elevation;
                    ChangeEach(c, "Elevation", e => null, Length(level), e => SetElevation(e, level));
                }
                else if (c.Input.Is("LAyer"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForString("Enter new layer name", layer);
                        string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : layer;

                        if (!db.LayerTable.Has(typed))
                        {
                            c.Write("Cannot find layer \"" + typed + "\".");
                            continue;
                        }

                        layer = db.LayerTable[typed].Name;
                        string name = layer;
                        ChangeEach(c, "Layer", e => e.Layer, name, e => e.Layer = name);
                        break;
                    }
                }
                else if (c.Input.Is("LType"))
                {
                    yield return CadPrompt.ForString("Enter new linetype name", linetype);
                    string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : linetype;
                    string name;

                    if (string.Equals(typed, "ByLayer", StringComparison.OrdinalIgnoreCase))
                    {
                        name = "ByLayer";
                    }
                    else if (string.Equals(typed, "ByBlock", StringComparison.OrdinalIgnoreCase))
                    {
                        name = "ByBlock";
                    }
                    else if (typed.Length > 0 && db.LinetypeTable.Has(typed))
                    {
                        name = db.LinetypeTable[typed].Name;
                    }
                    else
                    {
                        c.Write("Linetype \"" + typed + "\" not found.  Use LINETYPE command to load it.");
                        continue;
                    }

                    linetype = name;
                    ChangeEach(c, "Linetype", e => e.Linetype, name, e => e.Linetype = name);
                }
                else if (c.Input.Is("ltScale"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForDistance("Specify new linetype scale", null, null, Length(ltscale));

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            if (c.Input.Number == 0.0)
                            {
                                c.Write("Value must be nonzero.");
                                continue;
                            }

                            ltscale = Math.Abs(c.Input.Number);
                        }
                        else if (c.Input.Kind != CadInputKind.None)
                        {
                            break;
                        }

                        double scale = ltscale;
                        ChangeEach(
                            c,
                            "Linetype scale",
                            e => Number(e.LinetypeScale),
                            Number(scale),
                            e => e.LinetypeScale = scale
                        );
                        break;
                    }
                }
                else if (c.Input.Is("LWeight"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForString("Enter new lineweight", ChangeWeightText(weight));
                        string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().TrimStart('_') : null;

                        if (typed != null && typed.Length > 0)
                        {
                            if (!ChangeWeight(c, typed, out DxfLineWeight parsed))
                            {
                                continue;
                            }

                            weight = parsed;
                        }

                        DxfLineWeight chosen = weight;
                        ChangeEach(
                            c,
                            "Lineweight",
                            e => LineweightShown(e.LineWeight),
                            LineweightShown(chosen),
                            e => e.LineWeight = chosen
                        );
                        break;
                    }
                }
                else if (c.Input.Is("Thickness"))
                {
                    yield return CadPrompt.ForDistance("Specify new thickness", null, null, Length(thickness));

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        thickness = c.Input.Number;
                    }
                    else if (c.Input.Kind != CadInputKind.None)
                    {
                        continue;
                    }

                    double depth = thickness;
                    ChangeEach(c, "Thickness", e => null, Length(depth), e => SetThickness(e, depth));
                }
                else if (c.Input.Is("TRansparency"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForString(
                            "Specify transparency value",
                            ChangeTransparencyText(transparency)
                        );
                        string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : null;

                        if (typed != null && typed.Length > 0)
                        {
                            if (string.Equals(typed, "ByLayer", StringComparison.OrdinalIgnoreCase))
                            {
                                transparency = DxfTransparency.ByLayer;
                            }
                            else if (string.Equals(typed, "ByBlock", StringComparison.OrdinalIgnoreCase))
                            {
                                transparency = DxfTransparency.ByBlock;
                            }
                            else if (
                                int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent)
                                && percent >= 0
                                && percent <= 90
                            )
                            {
                                transparency = new DxfTransparency((byte)Math.Floor(255.0 * (100 - percent) / 100.0));
                            }
                            else
                            {
                                c.Write("Requires an integer between 0 and 90.");
                                continue;
                            }
                        }

                        DxfTransparency chosen = transparency;
                        ChangeEach(
                            c,
                            "Transparency",
                            e => ChangeTransparencyText(e.Transparency),
                            ChangeTransparencyText(chosen),
                            e => e.Transparency = chosen
                        );
                        break;
                    }
                }
                else if (c.Input.Is("Material"))
                {
                    yield return CadPrompt.ForString("Enter new material name", "ByLayer");
                    c.Write("Materials are not held by the presenter: the material is not changed.");
                }
                else
                {
                    yield return CadPrompt.ForKeyword("Make Annotative?", new[] { "Yes", "No" }, "No");
                    c.Write("Annotative objects are not changed by CHANGE here.");
                }
            }
        }

        /// <summary>The colour a Color prompt offers back as typed: its number or name.</summary>
        private static string ChangeColourText(DxfColor colour) => CadPropertyEditor.ColorName(colour);

        /// <summary>A lineweight as the prompt offers it: ByLayer, ByBlock, Default or the width in millimetres.</summary>
        private static string ChangeWeightText(DxfLineWeight weight)
        {
            switch (weight)
            {
                case DxfLineWeight.ByLayer:
                    return "ByLayer";
                case DxfLineWeight.ByBlock:
                    return "ByBlock";
                case DxfLineWeight.ByLineWeightDefault:
                    return "Default";
                default:
                    return ((int)weight / 100.0).ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>A transparency as the prompt offers it: ByLayer, ByBlock, or a per cent from 0 to 90.</summary>
        private static string ChangeTransparencyText(DxfTransparency transparency)
        {
            if (transparency.IsByBlock)
            {
                return "ByBlock";
            }

            if (!transparency.IsByAlpha)
            {
                return "ByLayer";
            }

            return (
                (int)Math.Round((255 - transparency.Alpha) * 100.0 / 255.0, MidpointRounding.AwayFromZero)
            ).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A lineweight typed at CHANGE: ByLayer, ByBlock, Default or a width in millimetres, rounded to AutoCAD's nearest with
        /// a word where it is not one; false, with the line AutoCAD writes, where it is not a lineweight.
        /// </summary>
        private static bool ChangeWeight(CadCommandContext c, string typed, out DxfLineWeight weight)
        {
            weight = DxfLineWeight.ByLayer;

            if (string.Equals(typed, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(typed, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                weight = DxfLineWeight.ByBlock;
                return true;
            }

            if (string.Equals(typed, "Default", StringComparison.OrdinalIgnoreCase))
            {
                weight = DxfLineWeight.ByLineWeightDefault;
                return true;
            }

            if (
                !double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double millimetres)
                || double.IsNaN(millimetres)
                || double.IsInfinity(millimetres)
            )
            {
                c.Write("A weight in lineweight units is required.");
                return false;
            }

            if (millimetres < 0.0)
            {
                c.Write("Invalid lweight number.");
                return false;
            }

            int nearest = NearestLineweight(millimetres * 100.0);

            if (Math.Abs(nearest - millimetres * 100.0) > 1E-9)
            {
                c.Write(
                    "Lineweight rounded to nearest valid value of "
                        + LineweightShown((DxfLineWeight)nearest).Replace("None", "0.00 mm")
                );
            }

            weight = (DxfLineWeight)nearest;
            return true;
        }

        /// <summary>Sets a property of each selected entity, as one edit of the command: the records of those that have it changed.</summary>
        private static void ChangeEach(
            CadCommandContext c,
            string label,
            Func<DxfEntity, string> was,
            string now,
            Action<DxfEntity> write
        )
        {
            var handles = new List<string>();
            var records = new List<CadEditRecord>();

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null)
                {
                    continue;
                }

                handles.Add(handle);
                records.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, label, was(entity), now));
            }

            if (handles.Count == 0)
            {
                return;
            }

            c.Apply(records, s =>
            {
                foreach (string handle in handles)
                {
                    DxfEntity entity = s.Document.FindEntity(handle);

                    if (entity != null)
                    {
                        write(entity);
                    }
                }
            });
        }

        /// <summary>Gives an entity the elevation, as AutoCAD's Elev does: the z of every point of a line, circle, arc, text, block or point, the elevation of a polyline; any other kind is left.</summary>
        private static void SetElevation(DxfEntity entity, double level)
        {
            switch (entity)
            {
                case DxfLine line:
                    line.StartPoint = new GeoPoint3(line.StartPoint.X, line.StartPoint.Y, level);
                    line.EndPoint = new GeoPoint3(line.EndPoint.X, line.EndPoint.Y, level);
                    break;
                case DxfCircle circle:
                    circle.Center = new GeoPoint3(circle.Center.X, circle.Center.Y, level);
                    break;
                case DxfArc arc:
                    arc.Center = new GeoPoint3(arc.Center.X, arc.Center.Y, level);
                    break;
                case DxfDBText text:
                    text.TransformBy(GeoTransform3.Translation(new GeoVector3(0.0, 0.0, level - text.Position.Z)));
                    break;
                case DxfBlockReference block:
                    block.Position = new GeoPoint3(block.Position.X, block.Position.Y, level);
                    break;
                case DxfDBPoint point:
                    point.Position = new GeoPoint3(point.Position.X, point.Position.Y, level);
                    break;
                case DxfPolyline polyline:
                    polyline.Elevation = level;
                    break;
                case DxfPolyline2d polyline2d:
                    polyline2d.Elevation = level;
                    break;
            }
        }

        /// <summary>Gives an entity the thickness: a line, circle, arc, text, point, solid or polyline; any other kind is left.</summary>
        private static void SetThickness(DxfEntity entity, double depth)
        {
            switch (entity)
            {
                case DxfLine line:
                    line.Thickness = depth;
                    break;
                case DxfCircle circle:
                    circle.Thickness = depth;
                    break;
                case DxfArc arc:
                    arc.Thickness = depth;
                    break;
                case DxfDBText text:
                    text.Thickness = depth;
                    break;
                case DxfDBPoint point:
                    point.Thickness = depth;
                    break;
                case DxfSolid solid:
                    solid.Thickness = depth;
                    break;
                case DxfPolyline polyline:
                    polyline.Thickness = depth;
                    break;
                case DxfPolyline2d polyline2d:
                    polyline2d.Thickness = depth;
                    break;
            }
        }
    }
}
