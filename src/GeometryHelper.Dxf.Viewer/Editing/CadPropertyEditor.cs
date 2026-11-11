using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Sets a property of the selected entities from the Properties panel, as AutoCAD's palette does: the row's label and the
    /// text typed into it, the text written as <see cref="CadProperties.Describe(CadDocument, CadSpace, IReadOnlyCollection{string})"/> shows it, so that what a row shows can be
    /// typed back. One call is one undo step.
    /// </summary>
    /// <remarks>
    /// The rows that can be set: Color (ByLayer, ByBlock, Red to White, an index 1 to 255, or red, green and blue as
    /// 255,128,0), Layer (a layer of the drawing), Linetype (ByLayer, ByBlock or a linetype of the drawing), Linetype scale (above
    /// nought) and Lineweight (ByLayer, ByBlock, Default, or one of AutoCAD's widths in millimetres as 0.25 mm) of every entity;
    /// the ends of a line; the centre, radius, diameter and circumference of a circle; the centre, radius and the start and end
    /// angles (in degrees) of an arc; the contents, height, rotation (in degrees) and position of a text and of an MTEXT; the
    /// text override of a dimension, "&lt;&gt; TYP" showing 3000 TYP on a dimension of 3 000, an empty one its measurement and
    /// one of a single space, which is not trimmed as every other value is, no text at all, as AutoCAD reads it; the arrowhead
    /// of a multileader, one of <see cref="CadArrowheads.Names"/> (its block made as AutoCAD makes it on first use) or a block
    /// of the drawing as a user arrow; and the rows AutoCAD 2021 sets of a polyline, an ellipse, a hatch, a dimension (as
    /// overrides of its variables, the dimension drawn again), an MTEXT, a block reference and its attributes, a viewport and a
    /// multileader. Current vertex of a polyline is no edit: it sets <see cref="CadSelection.CurrentVertex"/>. With
    /// no entity selected, the first five set the drawing's current settings, which new entities take.
    /// </remarks>
    public static partial class CadPropertyEditor
    {
        private static readonly string[] ColorNames =
        {
            null,
            "Red",
            "Yellow",
            "Green",
            "Cyan",
            "Blue",
            "Magenta",
            "White",
        };

        /// <summary>Sets one property of entities.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles of the entities; null or empty for the drawing's current settings.</param>
        /// <param name="label">The row's label, as <see cref="CadProperties.Describe(CadDocument, CadSpace, IReadOnlyCollection{string})"/> gives it: "Layer", "Start X", "Radius".</param>
        /// <param name="value">The text typed.</param>
        /// <returns>What came of it; refused with the reason, nothing written, for a value that cannot be taken, a row that cannot
        /// be set, or an entity on a locked layer.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the label or the value is null.</exception>
        public static CadEditResult Set(
            CadDocument document,
            IReadOnlyCollection<string> handles,
            string label,
            string value
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (label == null)
            {
                throw new ArgumentNullException(nameof(label));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var sorted = new List<string>(handles ?? Array.Empty<string>());
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            var entities = new List<DxfEntity>();

            foreach (string handle in sorted)
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity != null)
                {
                    entities.Add(entity);
                }
            }

            if (entities.Count == 0)
            {
                return SetCurrent(document, label, value.Trim());
            }

            if (label == "Current vertex" && entities.TrueForAll(e => e is DxfPolyline))
            {
                return Browse(document, entities, value.Trim());
            }

            int vertex = document.Selection.CurrentVertex;

            foreach (DxfEntity entity in entities)
            {
                if (CadLayerEditor.IsLocked(document.Database, entity.Layer))
                {
                    return CadEditResult.Refuse(
                        "The " + CadDocument.KindName(entity) + " " + entity.Handle + " is on a locked layer."
                    );
                }
            }

            string error = Writer(
                document.Database,
                label,
                value,
                vertex,
                entities[0],
                out Action<DxfEntity> write,
                out string normal
            );

            if (error != null)
            {
                return CadEditResult.Refuse(error);
            }

            var planned = new List<CadEditRecord>();

            for (int i = 0; i < entities.Count; i++)
            {
                if (i > 0 && Writer(document.Database, label, value, vertex, entities[i], out _, out _) != null)
                {
                    return CadEditResult.Refuse("The selected objects do not all have the property " + label + ".");
                }

                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        entities[i].Handle,
                        label,
                        Read(document, entities[i], label),
                        normal
                    )
                );
            }

            return document.ApplyEdit("Properties", planned, s =>
            {
                foreach (CadEditRecord record in planned)
                {
                    if (s.IsHandled(record.Handle))
                    {
                        continue;
                    }

                    DxfEntity entity = s.Document.FindEntity(record.Handle);
                    Writer(s.Database, label, value, vertex, entity, out Action<DxfEntity> each, out _);
                    each(entity);
                }
            });
        }

        /// <summary>The value a row shows for one entity, as the record's old value.</summary>
        private static string Read(CadDocument document, DxfEntity entity, string label)
        {
            CadSelectionInfo info = CadProperties.Describe(document, null, new[] { entity.Handle });

            foreach (CadPropertyGroup group in info.Groups)
            {
                foreach (CadPropertyRow row in group.Rows)
                {
                    if (row.Label == label)
                    {
                        return row.Value;
                    }
                }
            }

            return null;
        }

        /// <summary>Works out how a value is written to an entity: null and the writer, or the reason it cannot be.</summary>
        private static string Writer(
            DxfDatabase db,
            string label,
            string value,
            int vertex,
            DxfEntity entity,
            out Action<DxfEntity> write,
            out string normal
        )
        {
            write = null;

            // AutoCAD reads a text override of one space as no text at all, so that one is kept as it is typed; so is a
            // dimension's prefix or suffix, whose spaces stand between it and the measurement.
            normal =
                (label == "Text override" && value == " ")
                || (entity is DxfDimension && (label == "Dim prefix" || label == "Dim suffix"))
                    ? value
                    : value.Trim();
            string text = normal;

            switch (label)
            {
                case "Color":
                {
                    string error = ParseColor(text, out DxfColor color);
                    write = e => e.Color = color;
                    return error;
                }

                case "Layer":
                {
                    DxfLayerTableRecord layer =
                        text.Length == 0 || !db.LayerTable.Has(text) ? null : db.LayerTable[text];

                    if (layer == null)
                    {
                        return "The drawing has no layer \"" + text + "\".";
                    }

                    string name = layer.Name;
                    normal = name;
                    write = e => e.Layer = name;
                    return null;
                }

                case "Linetype":
                {
                    string name;

                    if (
                        string.Equals(text, "ByLayer", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(text, "ByBlock", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        name = string.Equals(text, "ByLayer", StringComparison.OrdinalIgnoreCase)
                            ? "ByLayer"
                            : "ByBlock";
                    }
                    else if (text.Length > 0 && db.LinetypeTable.Has(text))
                    {
                        name = text;
                    }
                    else
                    {
                        return "The drawing has no linetype \"" + text + "\".";
                    }

                    write = e => e.Linetype = name;
                    return null;
                }

                case "Linetype scale":
                {
                    if (!Number(text, out double scale) || scale <= 0.0)
                    {
                        return "Linetype scale is a number above nought.";
                    }

                    write = e => e.LinetypeScale = scale;
                    return null;
                }

                case "Lineweight":
                {
                    string error = ParseLineWeight(text, out DxfLineWeight weight);
                    write = e => e.LineWeight = weight;
                    return error;
                }

                case "Text override" when entity is DxfDimension:
                    write = e => Override(db, (DxfDimension)e, text);
                    return null;
                case "Arrowhead" when entity is DxfMLeader:
                {
                    // One of AutoCAD's arrowheads, its block made on first use, or a block of the drawing as a user arrow.
                    string name = null;

                    foreach (string arrow in CadArrowheads.Names)
                    {
                        if (string.Equals(arrow, text, StringComparison.OrdinalIgnoreCase))
                        {
                            name = arrow;
                            break;
                        }
                    }

                    if (
                        name == null
                        && text.Length > 0
                        && db.BlockTable.Has(text)
                        && !text.StartsWith("*", StringComparison.Ordinal)
                    )
                    {
                        name = db.BlockTable[text].Name;
                    }

                    if (name == null)
                    {
                        return "The drawing has no arrowhead \"" + text + "\".";
                    }

                    normal = name;
                    write = e => ((DxfMLeader)e).ArrowSymbol = CadArrowheads.Ensure(db, name);
                    return null;
                }
            }

            return Own(db, label, text, vertex, entity, ref write);
        }

        /// <summary>The rows of an entity's own kind.</summary>
        private static string Own(
            DxfDatabase db,
            string label,
            string text,
            int vertex,
            DxfEntity entity,
            ref Action<DxfEntity> write
        )
        {
            bool number = Number(text, out double v);
            string notNumber = label + " is a number.";
            string notPositive = label + " is a number above nought.";

            switch (entity)
            {
                case DxfLine _:
                    switch (label)
                    {
                        case "Start X":
                            write = e =>
                            {
                                var l = (DxfLine)e;
                                l.StartPoint = new GeoPoint3(v, l.StartPoint.Y, l.StartPoint.Z);
                            };
                            return number ? null : notNumber;
                        case "Start Y":
                            write = e =>
                            {
                                var l = (DxfLine)e;
                                l.StartPoint = new GeoPoint3(l.StartPoint.X, v, l.StartPoint.Z);
                            };
                            return number ? null : notNumber;
                        case "Start Z":
                            write = e =>
                            {
                                var l = (DxfLine)e;
                                l.StartPoint = new GeoPoint3(l.StartPoint.X, l.StartPoint.Y, v);
                            };
                            return number ? null : notNumber;
                        case "End X":
                            write = e =>
                            {
                                var l = (DxfLine)e;
                                l.EndPoint = new GeoPoint3(v, l.EndPoint.Y, l.EndPoint.Z);
                            };
                            return number ? null : notNumber;
                        case "End Y":
                            write = e =>
                            {
                                var l = (DxfLine)e;
                                l.EndPoint = new GeoPoint3(l.EndPoint.X, v, l.EndPoint.Z);
                            };
                            return number ? null : notNumber;
                        case "End Z":
                            write = e =>
                            {
                                var l = (DxfLine)e;
                                l.EndPoint = new GeoPoint3(l.EndPoint.X, l.EndPoint.Y, v);
                            };
                            return number ? null : notNumber;
                    }

                    break;
                case DxfCircle _:
                    switch (label)
                    {
                        case "Center X":
                            write = e =>
                            {
                                var c = (DxfCircle)e;
                                c.Center = new GeoPoint3(v, c.Center.Y, c.Center.Z);
                            };
                            return number ? null : notNumber;
                        case "Center Y":
                            write = e =>
                            {
                                var c = (DxfCircle)e;
                                c.Center = new GeoPoint3(c.Center.X, v, c.Center.Z);
                            };
                            return number ? null : notNumber;
                        case "Center Z":
                            write = e =>
                            {
                                var c = (DxfCircle)e;
                                c.Center = new GeoPoint3(c.Center.X, c.Center.Y, v);
                            };
                            return number ? null : notNumber;
                        case "Radius":
                            write = e => ((DxfCircle)e).Radius = v;
                            return number && v > 0.0 ? null : notPositive;
                        case "Diameter":
                            write = e => ((DxfCircle)e).Radius = v / 2.0;
                            return number && v > 0.0 ? null : notPositive;
                        case "Circumference":
                            write = e => ((DxfCircle)e).Radius = v / (2.0 * Math.PI);
                            return number && v > 0.0 ? null : notPositive;
                    }

                    break;
                case DxfArc _:
                    switch (label)
                    {
                        case "Center X":
                            write = e =>
                            {
                                var a = (DxfArc)e;
                                a.Center = new GeoPoint3(v, a.Center.Y, a.Center.Z);
                            };
                            return number ? null : notNumber;
                        case "Center Y":
                            write = e =>
                            {
                                var a = (DxfArc)e;
                                a.Center = new GeoPoint3(a.Center.X, v, a.Center.Z);
                            };
                            return number ? null : notNumber;
                        case "Center Z":
                            write = e =>
                            {
                                var a = (DxfArc)e;
                                a.Center = new GeoPoint3(a.Center.X, a.Center.Y, v);
                            };
                            return number ? null : notNumber;
                        case "Radius":
                            write = e => ((DxfArc)e).Radius = v;
                            return number && v > 0.0 ? null : notPositive;
                        case "Start angle":
                            write = e => ((DxfArc)e).StartAngle = Direction(db, v);
                            return number ? null : notNumber;
                        case "End angle":
                            write = e => ((DxfArc)e).EndAngle = Direction(db, v);
                            return number ? null : notNumber;
                    }

                    break;
                case DxfDBText _:
                    switch (label)
                    {
                        case "Contents":
                            write = e => ((DxfDBText)e).TextString = text;
                            return text.Length > 0 ? null : "A text has contents.";
                        case "Height":
                            write = e => ((DxfDBText)e).Height = v;
                            return number && v > 0.0 ? null : notPositive;
                        case "Rotation":
                            write = e => ((DxfDBText)e).Rotation = Direction(db, v);
                            return number ? null : notNumber;
                        case "Position X":
                            write = e => Shift(e, v - ((DxfDBText)e).Position.X, 0.0, 0.0);
                            return number ? null : notNumber;
                        case "Position Y":
                            write = e => Shift(e, 0.0, v - ((DxfDBText)e).Position.Y, 0.0);
                            return number ? null : notNumber;
                        case "Position Z":
                            write = e => Shift(e, 0.0, 0.0, v - ((DxfDBText)e).Position.Z);
                            return number ? null : notNumber;
                    }

                    break;
                case DxfMText _:
                    switch (label)
                    {
                        case "Contents":
                            write = e => ((DxfMText)e).Contents = text;
                            return text.Length > 0 ? null : "An MTEXT has contents.";
                        case "Text height":
                            write = e => ((DxfMText)e).TextHeight = v;
                            return number && v > 0.0 ? null : notPositive;
                        case "Rotation":
                            write = e => ((DxfMText)e).Rotation = Direction(db, v);
                            return number ? null : notNumber;
                        case "Position X":
                            write = e =>
                            {
                                var m = (DxfMText)e;
                                m.Location = new GeoPoint3(v, m.Location.Y, m.Location.Z);
                            };
                            return number ? null : notNumber;
                        case "Position Y":
                            write = e =>
                            {
                                var m = (DxfMText)e;
                                m.Location = new GeoPoint3(m.Location.X, v, m.Location.Z);
                            };
                            return number ? null : notNumber;
                        case "Position Z":
                            write = e =>
                            {
                                var m = (DxfMText)e;
                                m.Location = new GeoPoint3(m.Location.X, m.Location.Y, v);
                            };
                            return number ? null : notNumber;
                    }

                    break;
                case DxfViewport viewport
                    when label == "Display locked" || label == "Standard scale" || label == "Custom scale":
                    return ViewportRow(label, text, viewport, ref write);
            }

            if (More(db, label, text, vertex, entity, out write, out string error))
            {
                return error;
            }

            write = null;
            return "The property " + label + " of a " + CadDocument.KindName(entity) + " cannot be set here.";
        }

        /// <summary>
        /// A viewport's rows: Display locked (Yes or No); Standard scale, one of <see cref="CadViewports.StandardScales"/>, and
        /// Custom scale, the paper units to the model unit above nought, each making the view's height the frame's over the
        /// scale about the same centre, as AutoCAD's palette does; a viewport whose display is locked keeps its scale.
        /// </summary>
        private static string ViewportRow(string label, string text, DxfViewport viewport, ref Action<DxfEntity> write)
        {
            switch (label)
            {
                case "Display locked":
                {
                    bool yes = string.Equals(text, "Yes", StringComparison.OrdinalIgnoreCase);

                    if (!yes && !string.Equals(text, "No", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Display locked is Yes or No.";
                    }

                    write = e => ((DxfViewport)e).Locked = yes;
                    return null;
                }

                case "Standard scale":
                case "Custom scale":
                {
                    double scale;

                    if (label == "Standard scale")
                    {
                        if (!CadViewports.StandardScales.Contains(text) || !CadViewports.TryReadScale(text, out scale))
                        {
                            return "Standard scale is one of " + string.Join(", ", CadViewports.StandardScales) + ".";
                        }
                    }
                    else if (!Number(text, out scale) || !(scale > 0.0))
                    {
                        return "Custom scale is a number above nought.";
                    }

                    if (viewport.Locked)
                    {
                        return "The viewport's display is locked.";
                    }

                    if (viewport.Number == 1 || !(viewport.Height > 0.0))
                    {
                        return "The viewport of the sheet has no scale to set.";
                    }

                    write = e => ((DxfViewport)e).CustomScale = scale;
                    return null;
                }
            }

            write = null;
            return "The property " + label + " of a viewport cannot be set here.";
        }

        /// <summary>The current settings of the drawing, set with no entity selected.</summary>
        private static CadEditResult SetCurrent(CadDocument document, string label, string text)
        {
            DxfDatabase db = document.Database;
            Action<DxfDatabase> write;
            string error;

            if (
                document.BlockEditor is CadBlockEditor editor
                && !editor.IsCurrentDrawing
                && (
                    label == "Description"
                    || label == "Units"
                    || label == "Allow exploding"
                    || label == "Scale uniformly"
                )
            )
            {
                return SetBlock(document, editor, label, text);
            }

            switch (label)
            {
                case "Color":
                    error = ParseColor(text, out DxfColor color);
                    write = d => d.Cecolor = color;
                    break;
                case "Layer":
                    return CadLayerEditor.SetCurrent(document, text);
                case "Linetype":
                {
                    string name =
                        string.Equals(text, "ByLayer", StringComparison.OrdinalIgnoreCase) ? "ByLayer"
                        : string.Equals(text, "ByBlock", StringComparison.OrdinalIgnoreCase) ? "ByBlock"
                        : text;
                    error =
                        name == "ByLayer" || name == "ByBlock" || (name.Length > 0 && db.LinetypeTable.Has(name))
                            ? null
                            : "The drawing has no linetype \"" + text + "\".";
                    write = d => d.Celtype = name;
                    break;
                }

                case "Linetype scale":
                    error =
                        Number(text, out double scale) && scale > 0.0
                            ? null
                            : "Linetype scale is a number above nought.";
                    write = d => d.Celtscale = scale;
                    break;
                case "Lineweight":
                    error = ParseLineWeight(text, out DxfLineWeight weight);
                    write = d => d.Celweight = weight;
                    break;
                default:
                    return CadEditResult.Refuse("The property " + label + " of the drawing cannot be set here.");
            }

            if (error != null)
            {
                return CadEditResult.Refuse(error);
            }

            return document.ApplyEdit(
                "Properties",
                new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, label, null, text) },
                s => write(s.Database)
            );
        }

        /// <summary>
        /// Sets a row of the Block group the Block Editor shows with nothing selected: the description, unit or flags of the
        /// block opened, one undo step of the editor's session. A drawing older than R2007, which holds no unit or flag of a
        /// block, refuses those.
        /// </summary>
        private static CadEditResult SetBlock(CadDocument document, CadBlockEditor editor, string label, string text)
        {
            string name = editor.Name;
            Action<DxfBlockTableRecord> write;

            switch (label)
            {
                case "Description":
                    write = b => b.Comments = text;
                    break;
                case "Units":
                    int at = CadBlockUnits.IndexOf(text);

                    if (at < 0)
                    {
                        return CadEditResult.Refuse("No unit is named " + text + ".");
                    }

                    DxfUnits units = CadBlockUnits.Values[at];
                    write = b => b.Units = units;
                    break;
                default:
                    bool yes = string.Equals(text, "Yes", StringComparison.OrdinalIgnoreCase);

                    if (!yes && !string.Equals(text, "No", StringComparison.OrdinalIgnoreCase))
                    {
                        return CadEditResult.Refuse(label + " is Yes or No.");
                    }

                    write =
                        label == "Allow exploding"
                            ? (Action<DxfBlockTableRecord>)(b => b.Explodable = yes)
                            : b => b.BlockScaling = yes ? DxfBlockScaling.Uniform : DxfBlockScaling.Any;
                    break;
            }

            // A value the drawing's version cannot hold throws, and the edit fails with its message, nothing written.
            return document.ApplyEdit(
                "Properties",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Block " + label, null, text) },
                s => write(s.Database.BlockTable[name])
            );
        }

        /// <summary>
        /// Sets a dimension's text override, as AutoCAD's palette sets it, and the words of its picture with it, so that it shows at
        /// once: the measurement where the text is empty, in the place of a &lt;&gt; of it. A picture another dimension draws with
        /// too, as a copy's, is left for AutoCAD to draw anew.
        /// </summary>
        private static void Override(DxfDatabase db, DxfDimension dimension, string text)
        {
            dimension.DimensionText = text;
            DxfBlockTableRecord picture = dimension.DimBlock;

            if (
                picture == null
                || double.IsNaN(dimension.Measurement)
                || double.IsInfinity(dimension.Measurement)
                || SharesPicture(db, dimension, picture)
            )
            {
                return;
            }

            string words = dimension.FormatMeasurement(dimension.Measurement, text);

            foreach (DxfEntity entity in picture)
            {
                if (entity.IsErased)
                {
                    continue;
                }

                if (entity is DxfMText mtext)
                {
                    mtext.Contents = words;
                }
                else if (entity is DxfDBText single)
                {
                    single.TextString = words;
                }
            }
        }

        /// <summary>Whether another dimension of model space or a layout draws with the same picture.</summary>
        internal static bool SharesPicture(DxfDatabase db, DxfDimension dimension, DxfBlockTableRecord picture)
        {
            foreach (DxfLayout layout in db.Layouts)
            {
                if (layout.BlockTableRecord == null)
                {
                    continue;
                }

                foreach (DxfEntity entity in layout.BlockTableRecord)
                {
                    if (
                        !entity.IsErased
                        && entity is DxfDimension other
                        && !ReferenceEquals(other, dimension)
                        && other.DimBlock != null
                        && string.Equals(other.DimBlock.Name, picture.Name, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void Shift(DxfEntity entity, double dx, double dy, double dz) =>
            entity.TransformBy(GeoTransform3.Translation(new GeoVector3(dx, dy, dz)));

        /// <summary>
        /// The direction a row was given in degrees counted as the drawing counts angles, from ANGBASE and clockwise with
        /// ANGDIR 1, in radians from the X axis as an entity keeps it.
        /// </summary>
        private static double Direction(DxfDatabase db, double degrees) =>
            Turn(CadUnits.ToDirection(degrees * Math.PI / 180.0, db.Angbase, db.Angdir) * 180.0 / Math.PI);

        private static double Turn(double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            radians -= Math.Floor(radians / (2.0 * Math.PI)) * 2.0 * Math.PI;
            return radians >= 2.0 * Math.PI ? 0.0 : radians;
        }

        private static bool Number(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);

        /// <summary>A colour as the palette writes it: ByLayer, ByBlock, Red to White, an index 1 to 255, or red, green and blue.</summary>
        internal static string ParseColor(string text, out DxfColor color)
        {
            color = null;

            if (string.Equals(text, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                color = DxfColor.ByLayer;
                return null;
            }

            if (string.Equals(text, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                color = DxfColor.ByBlock;
                return null;
            }

            for (int i = 1; i < ColorNames.Length; i++)
            {
                if (string.Equals(text, ColorNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)i);
                    return null;
                }
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                if (index < 1 || index > 255)
                {
                    return "A colour's index is 1 to 255.";
                }

                color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);
                return null;
            }

            string[] parts = text.Split(',');

            if (
                parts.Length == 3
                && byte.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                && byte.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                && byte.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b)
            )
            {
                color = DxfColor.FromRgb(r, g, b);
                return null;
            }

            return "A colour is ByLayer, ByBlock, a name from Red to White, an index 1 to 255, or red, green and blue as 255,128,0.";
        }

        /// <summary>A colour as the palette writes it, so that it can be typed back: ByLayer, ByBlock, Red to White, 30, or 10,20,30.</summary>
        internal static string ColorName(DxfColor color)
        {
            if (color.IsByLayer)
            {
                return "ByLayer";
            }

            if (color.IsByBlock)
            {
                return "ByBlock";
            }

            if (color.IsByColor)
            {
                return color.Red.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + color.Green.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + color.Blue.ToString(CultureInfo.InvariantCulture);
            }

            return color.ColorIndex >= 1 && color.ColorIndex <= 7
                ? ColorNames[color.ColorIndex]
                : color.ColorIndex.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A lineweight as the palette writes it: ByLayer, ByBlock, Default, or one of AutoCAD's widths as 0.25 mm.</summary>
        internal static string ParseLineWeight(string text, out DxfLineWeight weight)
        {
            weight = DxfLineWeight.ByLayer;

            if (string.Equals(text, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(text, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                weight = DxfLineWeight.ByBlock;
                return null;
            }

            if (string.Equals(text, "Default", StringComparison.OrdinalIgnoreCase))
            {
                weight = DxfLineWeight.ByLineWeightDefault;
                return null;
            }

            string number = text.EndsWith("mm", StringComparison.OrdinalIgnoreCase)
                ? text.Substring(0, text.Length - 2).Trim()
                : text;

            if (Number(number, out double millimetres))
            {
                int hundredths = (int)Math.Round(millimetres * 100.0, MidpointRounding.AwayFromZero);

                if (
                    Math.Abs(hundredths - millimetres * 100.0) < 1E-6
                    && hundredths >= 0
                    && Enum.IsDefined(typeof(DxfLineWeight), hundredths)
                )
                {
                    weight = (DxfLineWeight)hundredths;
                    return null;
                }
            }

            return "A lineweight is ByLayer, ByBlock, Default, or one of AutoCAD's widths in millimetres, as 0.25 mm.";
        }
    }
}
