using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>One row of the Properties panel: a label and a value, which may be read-only.</summary>
    public sealed class CadPropertyRow
    {
        /// <summary>Makes a row.</summary>
        /// <param name="label">The label.</param>
        /// <param name="value">The value as text; "*Varies*" where the selected entities differ.</param>
        /// <param name="isReadOnly">Whether the value cannot be edited.</param>
        public CadPropertyRow(string label, string value, bool isReadOnly)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Value = value ?? string.Empty;
            IsReadOnly = isReadOnly;
        }

        /// <summary>Gets the label.</summary>
        public string Label { get; }

        /// <summary>Gets the value as text.</summary>
        public string Value { get; }

        /// <summary>Gets whether the value cannot be edited.</summary>
        public bool IsReadOnly { get; }

        /// <summary>Gets whether the selected entities differ in this property.</summary>
        public bool Varies => Value == VariesText;

        /// <summary>The text of a value that differs among the selected entities.</summary>
        public const string VariesText = "*Varies*";
    }

    /// <summary>A titled group of rows of the Properties panel, such as General or Geometry.</summary>
    public sealed class CadPropertyGroup
    {
        /// <summary>Makes a group.</summary>
        /// <param name="title">The title.</param>
        /// <param name="rows">The rows.</param>
        public CadPropertyGroup(string title, IReadOnlyList<CadPropertyRow> rows)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        /// <summary>Gets the title.</summary>
        public string Title { get; }

        /// <summary>Gets the rows.</summary>
        public IReadOnlyList<CadPropertyRow> Rows { get; }
    }

    /// <summary>What the Properties panel shows for a selection: the text of its selection box and its groups.</summary>
    public sealed class CadSelectionInfo
    {
        /// <summary>Makes the description of a selection.</summary>
        /// <param name="label">The text of the selection box: "No selection", "Line (1)", "Dimension (3)".</param>
        /// <param name="groups">The groups.</param>
        public CadSelectionInfo(string label, IReadOnlyList<CadPropertyGroup> groups)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Groups = groups ?? throw new ArgumentNullException(nameof(groups));
        }

        /// <summary>Gets the text of the selection box.</summary>
        public string Label { get; }

        /// <summary>Gets the groups of rows.</summary>
        public IReadOnlyList<CadPropertyGroup> Groups { get; }
    }

    /// <summary>Describes a selection for the Properties panel, as AutoCAD's Properties palette does.</summary>
    public static class CadProperties
    {
        private static readonly string[] ColorNames = { null, "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        /// <summary>
        /// Describes the selected entities as AutoCAD's Properties palette does. With none selected, the current settings
        /// the drawing's header holds (General: colour, layer, linetype, its scale, lineweight, transparency), the plot style,
        /// the view of the space (its centre, height and width, those of what the space draws, read-only) and Misc (the
        /// annotation scale, the UCS icon, the visual style "2D Wireframe"). With one, the box reads "Line (1)" and the rows
        /// are its General ones (its colour as ByLayer, ByBlock, Red to White, an index such as 8, or red, green and blue such
        /// as 255,128,0) and those of its kind: a line from (0, 0) to (300, 400) has Length 500.0000 and, at an <c>$AUPREC</c> of 0, Angle 53. With
        /// several, the box reads "Line (3)" where they are all of one kind and "All (3)" where not, and the rows are those they
        /// share, a value that differs shown as <see cref="CadPropertyRow.VariesText"/>. Numbers are written in the invariant
        /// culture, lengths and coordinates to 4 places, angles in degrees to the places the header's <c>$AUPREC</c> gives.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space shown; null for the document's current space.</param>
        /// <param name="handles">The handles of the selected entities, as <see cref="CadSelection.Handles"/> gives them; null
        /// or empty for none. A handle the document has no entity of is passed over.</param>
        /// <returns>The description.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadSelectionInfo Describe(CadDocument document, CadSpace space, IReadOnlyCollection<string> handles) => Describe(document, space, handles, null);

        /// <summary>
        /// Describes the selected entities as <see cref="Describe(CadDocument, CadSpace, IReadOnlyCollection{string})"/> does, the
        /// View group of no selection showing what the canvas shows: the centre, height and width of the view's visible box, in
        /// model space the model's view and in a layout the paper's, as AutoCAD's palette shows them. A canvas 1 000 by 500
        /// pixels at 2 pixels to the unit about (100, 50) shows Center X 100.0000, Height 250.0000 and Width 500.0000.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space shown; null for the document's current space.</param>
        /// <param name="handles">The handles of the selected entities; null or empty for none.</param>
        /// <param name="view">The view of the canvas showing the space; null for the box of what the space draws, as the overload without it.</param>
        /// <returns>The description.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadSelectionInfo Describe(CadDocument document, CadSpace space, IReadOnlyCollection<string> handles, CadView view)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            space = space ?? document.CurrentSpace;
            var entities = new List<DxfEntity>();

            if (handles != null)
            {
                var sorted = new List<string>(handles);
                sorted.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (string handle in sorted)
                {
                    DxfEntity entity = document.FindEntity(handle);

                    if (entity != null)
                    {
                        entities.Add(entity);
                    }
                }
            }

            if (entities.Count == 0)
            {
                return new CadSelectionInfo("No selection", Nothing(document, space, view));
            }

            var format = new Formats(document.Database);
            string kind = KindOf(entities[0]);
            bool oneKind = true;

            for (int i = 1; i < entities.Count && oneKind; i++)
            {
                oneKind = KindOf(entities[i]) == kind;
            }

            string label = (oneKind ? kind : "All") + " (" + entities.Count.ToString(CultureInfo.InvariantCulture) + ")";
            List<CadPropertyGroup> merged = null;

            foreach (DxfEntity entity in entities)
            {
                var groups = new List<CadPropertyGroup> { General(entity) };

                if (oneKind)
                {
                    groups.AddRange(Own(entity, format));
                }

                merged = merged == null ? groups : Merge(merged, groups);
            }

            return new CadSelectionInfo(label, merged);
        }

        /// <summary>The groups of no selection: the current settings, the plot style, the view and Misc.</summary>
        private static IReadOnlyList<CadPropertyGroup> Nothing(CadDocument document, CadSpace space, CadView shown)
        {
            DxfDatabase db = document.Database;
            var general = new List<CadPropertyRow>
            {
                new CadPropertyRow("Color", Read(() => ColorText(db.Cecolor), "ByLayer"), false),
                new CadPropertyRow("Layer", Read(() => db.Clayer, "0"), false),
                new CadPropertyRow("Linetype", Read(() => LinetypeText(db.Celtype), "ByLayer"), false),
                new CadPropertyRow("Linetype scale", Read(() => Length(db.Celtscale), Length(1.0)), false),
                new CadPropertyRow("Lineweight", Read(() => LineWeightText(db.Celweight), "ByLayer"), false),
                new CadPropertyRow("Transparency", Read(() => TransparencyText(db.Cetransparency), "ByLayer"), false),
            };

            bool colorDependent = Read(() => db.PlotStyleMode, true);
            string table = space?.Layout == null ? null : Read(() => space.Layout.CurrentStyleSheet, null);
            var plot = new List<CadPropertyRow>
            {
                new CadPropertyRow("Plot style", colorDependent ? "ByColor" : "ByLayer", true),
                new CadPropertyRow("Plot style table", string.IsNullOrEmpty(table) ? "None" : table, false),
                new CadPropertyRow("Plot table attached to", space == null || space.IsModel ? "Model" : "Layout", true),
                new CadPropertyRow("Plot table type", string.IsNullOrEmpty(table) ? "Not available" : colorDependent ? "Color dependent" : "Named", true),
            };

            DisplayBox box = shown != null ? shown.VisibleBox : space == null ? DisplayBox.Empty : document.GetDisplay(space).Bounds;
            double cx = box.IsEmpty ? 0.0 : (box.MinX + box.MaxX) / 2.0;
            double cy = box.IsEmpty ? 0.0 : (box.MinY + box.MaxY) / 2.0;
            var view = new List<CadPropertyRow>
            {
                new CadPropertyRow("Center X", Length(cx), true),
                new CadPropertyRow("Center Y", Length(cy), true),
                new CadPropertyRow("Center Z", Length(0.0), true),
                new CadPropertyRow("Height", Length(box.Height), true),
                new CadPropertyRow("Width", Length(box.Width), true),
            };

            bool iconOn = true;
            bool iconAtOrigin = true;

            if (space != null && !space.IsModel)
            {
                foreach (DxfEntity entity in space.Block)
                {
                    if (entity is DxfViewport sheet && sheet.Number == 1)
                    {
                        iconOn = Read(() => sheet.UcsIconVisible, true);
                        iconAtOrigin = Read(() => sheet.UcsIconAtOrigin, true);
                        break;
                    }
                }
            }

            var misc = new List<CadPropertyRow>
            {
                new CadPropertyRow("Annotation scale", Read(() => db.Cannoscale, "1:1") ?? "1:1", false),
                new CadPropertyRow("UCS icon On", YesNo(iconOn), false),
                new CadPropertyRow("UCS icon at origin", YesNo(iconAtOrigin), false),
                new CadPropertyRow("Visual style", "2D Wireframe", true),
            };

            return new[]
            {
                new CadPropertyGroup("General", general),
                new CadPropertyGroup("Plot style", plot),
                new CadPropertyGroup("View", view),
                new CadPropertyGroup("Misc", misc),
            };
        }

        /// <summary>
        /// The reader of the rows of one entity as the palette shows them, for Quick Select: its General rows alone, or those and
        /// the rows of its kind; the drawing's formats read once.
        /// </summary>
        internal static Func<DxfEntity, bool, List<CadPropertyRow>> RowReader(DxfDatabase database)
        {
            var format = new Formats(database);
            return (entity, own) =>
            {
                var rows = new List<CadPropertyRow>(General(entity).Rows);

                if (own)
                {
                    foreach (CadPropertyGroup group in Own(entity, format))
                    {
                        rows.AddRange(group.Rows);
                    }
                }

                return rows;
            };
        }

        /// <summary>The General group of one entity.</summary>
        private static CadPropertyGroup General(DxfEntity entity)
        {
            var rows = new List<CadPropertyRow>
            {
                new CadPropertyRow("Color", Read(() => ColorText(entity.Color), "ByLayer"), false),
                new CadPropertyRow("Layer", entity.Layer ?? "0", false),
                new CadPropertyRow("Linetype", Read(() => LinetypeText(entity.Linetype), "ByLayer"), false),
                new CadPropertyRow("Linetype scale", Read(() => Length(entity.LinetypeScale), Length(1.0)), false),
                new CadPropertyRow("Lineweight", Read(() => LineWeightText(entity.LineWeight), "ByLayer"), false),
            };
            return new CadPropertyGroup("General", rows);
        }

        /// <summary>The groups of one entity's kind, as AutoCAD's palette heads them.</summary>
        private static IEnumerable<CadPropertyGroup> Own(DxfEntity entity, Formats f)
        {
            var groups = new List<CadPropertyGroup>();

            void Group(string title, params CadPropertyRow[] rows) => groups.Add(new CadPropertyGroup(title, rows));

            CadPropertyRow Row(string label, Func<string> value, bool isReadOnly = false) => new CadPropertyRow(label, Read(value, string.Empty), isReadOnly);

            switch (entity)
            {
                case DxfLine line:
                    Group(
                        "Geometry",
                        Row("Start X", () => Length(line.StartPoint.X)),
                        Row("Start Y", () => Length(line.StartPoint.Y)),
                        Row("Start Z", () => Length(line.StartPoint.Z)),
                        Row("End X", () => Length(line.EndPoint.X)),
                        Row("End Y", () => Length(line.EndPoint.Y)),
                        Row("End Z", () => Length(line.EndPoint.Z)),
                        Row("Length", () => Length(line.Length), true),
                        Row("Angle", () => f.Angle(Math.Atan2(line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.X - line.StartPoint.X)), true));
                    break;
                case DxfCircle circle:
                    Group(
                        "Geometry",
                        Row("Center X", () => Length(circle.Center.X)),
                        Row("Center Y", () => Length(circle.Center.Y)),
                        Row("Center Z", () => Length(circle.Center.Z)),
                        Row("Radius", () => Length(circle.Radius)),
                        Row("Diameter", () => Length(circle.Diameter)),
                        Row("Circumference", () => Length(circle.Circumference)));
                    break;
                case DxfArc arc:
                    Group(
                        "Geometry",
                        Row("Center X", () => Length(arc.Center.X)),
                        Row("Center Y", () => Length(arc.Center.Y)),
                        Row("Center Z", () => Length(arc.Center.Z)),
                        Row("Radius", () => Length(arc.Radius)),
                        Row("Start angle", () => f.Angle(arc.StartAngle)),
                        Row("End angle", () => f.Angle(arc.EndAngle)),
                        Row("Arc length", () => Length(arc.Length), true));
                    break;
                case DxfPolyline polyline:
                    Group(
                        "Geometry",
                        Row("Vertices", () => polyline.NumberOfVertices.ToString(CultureInfo.InvariantCulture), true),
                        Row("Length", () => Length(polyline.Length), true),
                        Row("Area", () => Length(polyline.Area), true));
                    Group("Misc", Row("Closed", () => YesNo(polyline.Closed)));
                    break;
                case DxfDBText text:
                    Group(
                        "Text",
                        Row("Contents", () => text.TextString),
                        Row("Height", () => Length(text.Height)),
                        Row("Rotation", () => f.Angle(text.Rotation)));
                    Group(
                        "Geometry",
                        Row("Position X", () => Length(text.Position.X)),
                        Row("Position Y", () => Length(text.Position.Y)),
                        Row("Position Z", () => Length(text.Position.Z)));
                    break;
                case DxfMText mtext:
                    Group(
                        "Text",
                        Row("Contents", () => mtext.Text),
                        Row("Text height", () => Length(mtext.TextHeight)),
                        Row("Rotation", () => f.Angle(mtext.Rotation)));
                    Group(
                        "Geometry",
                        Row("Position X", () => Length(mtext.Location.X)),
                        Row("Position Y", () => Length(mtext.Location.Y)),
                        Row("Position Z", () => Length(mtext.Location.Z)));
                    break;
                case DxfBlockReference insert:
                    Group(
                        "Geometry",
                        Row("Position X", () => Length(insert.Position.X)),
                        Row("Position Y", () => Length(insert.Position.Y)),
                        Row("Position Z", () => Length(insert.Position.Z)),
                        Row("Scale X", () => Length(insert.ScaleFactors.X)),
                        Row("Scale Y", () => Length(insert.ScaleFactors.Y)),
                        Row("Scale Z", () => Length(insert.ScaleFactors.Z)));
                    Group(
                        "Misc",
                        Row("Name", () => insert.Name),
                        Row("Rotation", () => f.Angle(insert.Rotation)));
                    break;
                case DxfMLeader mleader:
                    Group("Leaders", Row("Arrowhead", () => CadArrowheads.NameOf(mleader.ArrowSymbol)));
                    break;
                case DxfDimension dimension:
                    Group("Misc", Row("Dim style", () => dimension.DimensionStyleName));
                    Group(
                        "Text",
                        Row("Measurement", () => IsAngular(dimension) ? f.Angle(dimension.Measurement) : Length(dimension.Measurement), true),
                        Row("Text override", () => dimension.DimensionText));
                    break;
                case DxfHatch hatch:
                    Group(
                        "Pattern",
                        Row("Pattern name", () => hatch.IsGradient ? hatch.GradientName : hatch.PatternName),
                        Row("Angle", () => f.Angle(hatch.PatternAngle)),
                        Row("Scale", () => Length(hatch.PatternScale)));
                    Group("Geometry", Row("Area", () => Length(hatch.Area), true));
                    break;
                case DxfViewport viewport:
                    Group(
                        "Geometry",
                        Row("Center X", () => Length(viewport.CenterPoint.X)),
                        Row("Center Y", () => Length(viewport.CenterPoint.Y)),
                        Row("Center Z", () => Length(viewport.CenterPoint.Z)),
                        Row("Height", () => Length(viewport.Height)),
                        Row("Width", () => Length(viewport.Width)));
                    Group(
                        "Misc",
                        Row("On", () => YesNo(viewport.On)),
                        Row("Standard scale", () => ScaleText(viewport.StandardScale)),
                        Row("Custom scale", () => Length(viewport.CustomScale)));
                    break;
            }

            return groups;
        }

        /// <summary>The groups two sets share, by title and label, in the order of the first; a value that differs varies.</summary>
        private static List<CadPropertyGroup> Merge(List<CadPropertyGroup> first, List<CadPropertyGroup> second)
        {
            var result = new List<CadPropertyGroup>();

            foreach (CadPropertyGroup group in first)
            {
                CadPropertyGroup other = second.Find(g => g.Title == group.Title);

                if (other == null)
                {
                    continue;
                }

                var rows = new List<CadPropertyRow>();

                foreach (CadPropertyRow row in group.Rows)
                {
                    CadPropertyRow match = null;

                    foreach (CadPropertyRow candidate in other.Rows)
                    {
                        if (candidate.Label == row.Label)
                        {
                            match = candidate;
                            break;
                        }
                    }

                    if (match != null)
                    {
                        rows.Add(match.Value == row.Value ? row : new CadPropertyRow(row.Label, CadPropertyRow.VariesText, row.IsReadOnly || match.IsReadOnly));
                    }
                }

                if (rows.Count > 0)
                {
                    result.Add(new CadPropertyGroup(group.Title, rows));
                }
            }

            return result;
        }

        /// <summary>The name AutoCAD's palette gives an entity's kind: Line, Circle, Polyline, Block Reference, Rotated Dimension.</summary>
        internal static string KindOf(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfLine _: return "Line";
                case DxfCircle _: return "Circle";
                case DxfArc _: return "Arc";
                case DxfEllipse _: return "Ellipse";
                case DxfPolyline _: return "Polyline";
                case DxfPolyline2d _: return "2D Polyline";
                case DxfPolyline3d _: return "3D Polyline";
                case DxfSpline _: return "Spline";
                case DxfLeader _: return "Leader";
                case DxfXline _: return "Xline";
                case DxfRay _: return "Ray";
                case DxfAttributeDefinition _: return "Attribute Definition";
                case DxfDBText _: return "Text";
                case DxfMText _: return "MText";
                case DxfMInsertBlock _: return "MInsert Block";
                case DxfBlockReference _: return "Block Reference";
                case DxfRotatedDimension _: return "Rotated Dimension";
                case DxfAlignedDimension _: return "Aligned Dimension";
                case DxfRadialDimension _: return "Radial Dimension";
                case DxfRadialDimensionLarge _: return "Radial Dimension Large";
                case DxfDiametricDimension _: return "Diametric Dimension";
                case DxfArcDimension _: return "Arc Dimension";
                case DxfOrdinateDimension _: return "Ordinate Dimension";
                case DxfLineAngularDimension2 _:
                case DxfPoint3AngularDimension _:
                    return "Angular Dimension";
                case DxfDimension _: return "Dimension";
                case DxfHatch _: return "Hatch";
                case DxfViewport _: return "Viewport";
                case DxfMLeader _: return "Multileader";
                case DxfTable _: return "Table";
                case DxfWipeout _: return "Wipeout";
                case DxfRasterImage _: return "Raster Image";
                case DxfSolid _: return "Solid";
                case DxfFace _: return "3D Face";
                case DxfDBPoint _: return "Point";
                case DxfOle2Frame _: return "OLE Object";
                default:
                    string name = entity.GetType().Name;
                    return name.StartsWith("Dxf", StringComparison.Ordinal) ? name.Substring(3) : name;
            }
        }

        private static bool IsAngular(DxfDimension dimension) => dimension is DxfLineAngularDimension2 || dimension is DxfPoint3AngularDimension;

        /// <summary>A colour as the palette writes it: ByLayer, ByBlock, Red to White, an index, or red, green and blue.</summary>
        private static string ColorText(DxfColor color)
        {
            if (color == null || color.IsByLayer)
            {
                return "ByLayer";
            }

            if (color.IsByBlock)
            {
                return "ByBlock";
            }

            if (color.IsByColor)
            {
                return color.ColorName ?? color.Red.ToString(CultureInfo.InvariantCulture) + "," + color.Green.ToString(CultureInfo.InvariantCulture) + "," + color.Blue.ToString(CultureInfo.InvariantCulture);
            }

            if (color.IsNone)
            {
                return "None";
            }

            int index = color.ColorIndex;
            return index >= 1 && index <= 7 ? ColorNames[index] : index.ToString(CultureInfo.InvariantCulture);
        }

        private static string LinetypeText(string name)
        {
            if (string.IsNullOrEmpty(name) || string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                return "ByLayer";
            }

            if (string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                return "ByBlock";
            }

            return string.Equals(name, "Continuous", StringComparison.OrdinalIgnoreCase) ? "Continuous" : name;
        }

        private static string LineWeightText(DxfLineWeight weight)
        {
            switch (weight)
            {
                case DxfLineWeight.ByLayer: return "ByLayer";
                case DxfLineWeight.ByBlock: return "ByBlock";
                case DxfLineWeight.ByLineWeightDefault: return "Default";
                default:
                    int raw = (int)weight;
                    return raw >= 0 ? (raw / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm" : "Default";
            }
        }

        /// <summary>A transparency as the palette writes it: ByLayer, ByBlock, or a per cent from 0, solid, to 90.</summary>
        private static string TransparencyText(DxfTransparency transparency)
        {
            if (transparency.IsByBlock)
            {
                return "ByBlock";
            }

            if (!transparency.IsByAlpha)
            {
                return "ByLayer";
            }

            int percent = (int)Math.Round((255 - transparency.Alpha) * 100.0 / 255.0, MidpointRounding.AwayFromZero);
            return percent.ToString(CultureInfo.InvariantCulture);
        }

        private static string ScaleText(DxfStandardScaleType scale)
        {
            string name = scale.ToString();

            if (scale == DxfStandardScaleType.ScaleToFit)
            {
                return "Scale to fit";
            }

            if (scale == DxfStandardScaleType.CustomScale)
            {
                return "Custom";
            }

            int to = name.IndexOf("To", StringComparison.Ordinal);

            if (name.StartsWith("Scale", StringComparison.Ordinal) && to > 5 && int.TryParse(name.Substring(5, to - 5), NumberStyles.None, CultureInfo.InvariantCulture, out int a)
                && int.TryParse(name.Substring(to + 2), NumberStyles.None, CultureInfo.InvariantCulture, out int b))
            {
                return a.ToString(CultureInfo.InvariantCulture) + ":" + b.ToString(CultureInfo.InvariantCulture);
            }

            return name;
        }

        private static string YesNo(bool value) => value ? "Yes" : "No";

        /// <summary>A length or coordinate to 4 places, as AutoCAD's palette writes one at <c>$LUPREC</c> 4.</summary>
        private static string Length(double value) => (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.0000", CultureInfo.InvariantCulture);

        private static T Read<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>How the drawing writes angles: in degrees to the places of its <c>$AUPREC</c>.</summary>
        private sealed class Formats
        {
            private readonly string _angle;

            public Formats(DxfDatabase database)
            {
                int places = Read(() => database.Auprec, 0);
                places = Math.Max(0, Math.Min(8, places));
                _angle = places == 0 ? "0" : "0." + new string('0', places);
            }

            /// <summary>An angle in radians written in degrees from 0 up to 360.</summary>
            public string Angle(double radians)
            {
                double degrees = radians * 180.0 / Math.PI;
                degrees -= Math.Floor(degrees / 360.0) * 360.0;
                string text = degrees.ToString(_angle, CultureInfo.InvariantCulture);
                return text == 360.0.ToString(_angle, CultureInfo.InvariantCulture) ? 0.0.ToString(_angle, CultureInfo.InvariantCulture) : text;
            }
        }
    }
}
