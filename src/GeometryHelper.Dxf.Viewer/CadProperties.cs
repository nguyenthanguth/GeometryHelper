using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            : this(label, value, isReadOnly, null) { }

        /// <summary>Makes a row whose value is one of a list, as the Justify row of an MTEXT offers Top left to Bottom right.</summary>
        /// <param name="label">The label.</param>
        /// <param name="value">The value as text; "*Varies*" where the selected entities differ.</param>
        /// <param name="isReadOnly">Whether the value cannot be edited.</param>
        /// <param name="choices">The values the row offers, in the order of AutoCAD's drop-down; null for a row typed into.</param>
        public CadPropertyRow(string label, string value, bool isReadOnly, IReadOnlyList<string> choices)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Value = value ?? string.Empty;
            IsReadOnly = isReadOnly;
            Choices = choices;
        }

        /// <summary>Gets the label.</summary>
        public string Label { get; }

        /// <summary>Gets the value as text.</summary>
        public string Value { get; }

        /// <summary>Gets whether the value cannot be edited.</summary>
        public bool IsReadOnly { get; }

        /// <summary>Gets the values the row offers, as the Text pos vert row of a dimension offers Centered, Above, Outside, JIS and Below; null for a row typed into.</summary>
        public IReadOnlyList<string> Choices { get; }

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
    public static partial class CadProperties
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

        /// <summary>
        /// The title of the group of a dynamic block's properties, as AutoCAD's palette names it: its rows are set through
        /// <see cref="CadDynamicBlocks.SetProperty"/>, by the name of the property, not through <see cref="CadPropertyEditor"/>.
        /// </summary>
        public const string CustomGroup = "Custom";

        internal static readonly string[] YesNoChoices = { "Yes", "No" };
        internal static readonly string[] EnabledChoices = { "Enabled", "Disabled" };
        internal static readonly string[] JustifyChoices =
        {
            "Top left",
            "Top center",
            "Top right",
            "Middle left",
            "Middle center",
            "Middle right",
            "Bottom left",
            "Bottom center",
            "Bottom right",
        };
        internal static readonly string[] DirectionChoices = { "Left to right", "Top to bottom", "By style" };
        internal static readonly string[] LineSpaceChoices = { "At least", "Exactly" };
        internal static readonly string[] ColumnChoices = { "None", "Static", "Dynamic" };
        internal static readonly string[] HatchTypeChoices = { "User defined", "Predefined", "Custom" };
        internal static readonly string[] IslandChoices = { "Normal", "Outer", "Ignore" };
        internal static readonly string[] LeaderTypeChoices = { "None", "Straight", "Spline" };

        /// <summary>The standard scales of a viewport in the order of AutoCAD's list, those but Scale to fit and Custom.</summary>
        internal static readonly string[] ScaleChoices = StandardScales();

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
        public static CadSelectionInfo Describe(
            CadDocument document,
            CadSpace space,
            IReadOnlyCollection<string> handles
        ) => Describe(document, space, handles, null);

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
        public static CadSelectionInfo Describe(
            CadDocument document,
            CadSpace space,
            IReadOnlyCollection<string> handles,
            CadView view
        )
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

            var format = new Formats(document.Database) { Vertex = document.Selection.CurrentVertex };
            string kind = KindOf(entities[0]);
            bool oneKind = true;

            for (int i = 1; i < entities.Count && oneKind; i++)
            {
                oneKind = KindOf(entities[i]) == kind;
            }

            string label =
                (oneKind ? kind : "All") + " (" + entities.Count.ToString(CultureInfo.InvariantCulture) + ")";
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
                new CadPropertyRow(
                    "Plot table type",
                    string.IsNullOrEmpty(table) ? "Not available"
                        : colorDependent ? "Color dependent"
                        : "Named",
                    true
                ),
            };

            DisplayBox box =
                shown != null ? shown.VisibleBox
                : space == null ? DisplayBox.Empty
                : document.GetDisplay(space).Bounds;
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

            var groups = new List<CadPropertyGroup>
            {
                new CadPropertyGroup("General", general),
                new CadPropertyGroup("Plot style", plot),
                new CadPropertyGroup("View", view),
                new CadPropertyGroup("Misc", misc),
            };

            // In the Block Editor the definition opened comes first, as AutoCAD's Block group: its name to be read, the rest set here.
            CadBlockEditor editor = document.BlockEditor;

            if (editor != null && !editor.IsCurrentDrawing && ReferenceEquals(space, editor.Space))
            {
                DxfBlockTableRecord block = editor.Space.Block;
                groups.Insert(
                    0,
                    new CadPropertyGroup(
                        "Block",
                        new List<CadPropertyRow>
                        {
                            new CadPropertyRow("Name", block.Name, true),
                            new CadPropertyRow(
                                "Description",
                                Read(() => block.Comments, string.Empty) ?? string.Empty,
                                false
                            ),
                            new CadPropertyRow(
                                "Units",
                                CadCommands.UnitName(Read(() => block.Units, DxfUnits.Undefined)),
                                false,
                                CadBlockUnits.Names
                            ),
                            new CadPropertyRow(
                                "Allow exploding",
                                YesNo(Read(() => block.Explodable, true)),
                                false,
                                YesNoChoices
                            ),
                            new CadPropertyRow(
                                "Scale uniformly",
                                YesNo(Read(() => block.BlockScaling == DxfBlockScaling.Uniform, false)),
                                false,
                                YesNoChoices
                            ),
                            new CadPropertyRow("Annotative", "No", true),
                        }
                    )
                );
            }

            return groups;
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

            CadPropertyRow Row(string label, Func<string> value, bool isReadOnly = false) =>
                new CadPropertyRow(label, Read(value, string.Empty), isReadOnly);

            CadPropertyRow Pick(
                string label,
                Func<string> value,
                IReadOnlyList<string> choices,
                bool isReadOnly = false
            ) => new CadPropertyRow(label, Read(value, string.Empty), isReadOnly, choices);

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
                        Row(
                            "Angle",
                            () =>
                                f.Angle(
                                    Math.Atan2(line.EndPoint.Y - line.StartPoint.Y, line.EndPoint.X - line.StartPoint.X)
                                ),
                            true
                        )
                    );
                    break;
                case DxfCircle circle:
                    Group(
                        "Geometry",
                        Row("Center X", () => Length(circle.Center.X)),
                        Row("Center Y", () => Length(circle.Center.Y)),
                        Row("Center Z", () => Length(circle.Center.Z)),
                        Row("Radius", () => Length(circle.Radius)),
                        Row("Diameter", () => Length(circle.Diameter)),
                        Row("Circumference", () => Length(circle.Circumference))
                    );
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
                        Row("Arc length", () => Length(arc.Length), true)
                    );
                    break;
                case DxfEllipse ellipse:
                    Group(
                        "Geometry",
                        Row("Center X", () => Length(ellipse.Center.X)),
                        Row("Center Y", () => Length(ellipse.Center.Y)),
                        Row("Center Z", () => Length(ellipse.Center.Z)),
                        Row("Major radius", () => Length(ellipse.MajorRadius)),
                        Row("Minor radius", () => Length(ellipse.MinorRadius)),
                        Row("Radius ratio", () => Length(ellipse.RadiusRatio)),
                        // From the major axis, written as AutoCAD 2021's LIST writes them: as directions counted by
                        // ANGBASE and ANGDIR, 30 and 120 as 60 and 330 with angles counted from north, clockwise.
                        Row("Start angle", () => f.Angle(ellipse.StartAngle)),
                        Row("End angle", () => f.Angle(ellipse.EndAngle)),
                        Row("Area", () => Length(ellipse.Area), true)
                    );
                    break;
                case DxfPolyline polyline:
                {
                    int at = Math.Max(0, Math.Min(f.Vertex, Read(() => polyline.NumberOfVertices, 0)) - 1);
                    Group(
                        "Geometry",
                        Row("Current vertex", () => (at + 1).ToString(CultureInfo.InvariantCulture)),
                        Row("Vertex X", () => Length(polyline.GetPoint2dAt(at).X)),
                        Row("Vertex Y", () => Length(polyline.GetPoint2dAt(at).Y)),
                        Row("Start segment width", () => Length(polyline.GetStartWidthAt(at))),
                        Row("End segment width", () => Length(polyline.GetEndWidthAt(at))),
                        Row("Global width", () => Length(polyline.ConstantWidth)),
                        Row("Elevation", () => Length(polyline.Elevation)),
                        Row("Vertices", () => polyline.NumberOfVertices.ToString(CultureInfo.InvariantCulture), true),
                        Row("Length", () => Length(polyline.Length), true),
                        Row("Area", () => Length(polyline.Area), true)
                    );
                    Group(
                        "Misc",
                        Pick("Closed", () => YesNo(polyline.Closed), YesNoChoices),
                        Pick("Linetype generation", () => polyline.Plinegen ? "Enabled" : "Disabled", EnabledChoices)
                    );
                    break;
                }
                case DxfDBText text:
                    Group(
                        "Text",
                        Row("Contents", () => text.TextString),
                        Row("Height", () => Length(text.Height)),
                        Row("Rotation", () => f.Angle(text.Rotation))
                    );
                    Group(
                        "Geometry",
                        Row("Position X", () => Length(text.Position.X)),
                        Row("Position Y", () => Length(text.Position.Y)),
                        Row("Position Z", () => Length(text.Position.Z))
                    );
                    break;
                case DxfMText mtext:
                    Group(
                        "Text",
                        Row("Contents", () => mtext.Text),
                        Pick("Style", () => mtext.TextStyleName, TextStyles(f.Database)),
                        Pick("Justify", () => JustifyText(mtext.Attachment), JustifyChoices),
                        Pick("Direction", () => DirectionText(mtext.FlowDirection), DirectionChoices),
                        Row("Text height", () => Length(mtext.TextHeight)),
                        Row("Rotation", () => f.Angle(mtext.Rotation)),
                        Row("Line space factor", () => Length(mtext.LineSpacingFactor)),
                        Row("Line space distance", () => Length(mtext.LineSpaceDistance)),
                        Pick(
                            "Line space style",
                            () => mtext.LineSpacingStyle == DxfLineSpacingStyle.Exactly ? "Exactly" : "At least",
                            LineSpaceChoices
                        ),
                        Row("Defined width", () => Length(mtext.Width)),
                        Pick("Columns", () => ColumnsText(mtext.ColumnType), ColumnChoices)
                    );
                    Group(
                        "Geometry",
                        Row("Position X", () => Length(mtext.Location.X)),
                        Row("Position Y", () => Length(mtext.Location.Y)),
                        Row("Position Z", () => Length(mtext.Location.Z))
                    );
                    break;
                case DxfBlockReference insert:
                    Group(
                        "Geometry",
                        Row("Position X", () => Length(insert.Position.X)),
                        Row("Position Y", () => Length(insert.Position.Y)),
                        Row("Position Z", () => Length(insert.Position.Z)),
                        Row("Scale X", () => Length(insert.ScaleFactors.X)),
                        Row("Scale Y", () => Length(insert.ScaleFactors.Y)),
                        Row("Scale Z", () => Length(insert.ScaleFactors.Z))
                    );

                    // A dynamic block's properties, as AutoCAD's Custom group lists them; its anonymous copy named by the block.
                    List<CadPropertyRow> custom = Read(
                        () => CadDynamicBlocks.CustomRows(insert, f.Angle),
                        new List<CadPropertyRow>()
                    );

                    if (custom.Count > 0)
                    {
                        groups.Add(new CadPropertyGroup(CustomGroup, custom));
                    }

                    Group(
                        "Misc",
                        Row("Name", () => CadDynamicBlocks.NameOf(insert), true),
                        Row("Rotation", () => f.Angle(insert.Rotation))
                    );
                    AttributesOf(insert, groups);
                    break;
                case DxfMLeader mleader:
                    Group("Misc", Pick("Multileader style", () => mleader.MLeaderStyle, MLeaderStyles(f.Database)));
                    Group(
                        "Leaders",
                        Pick("Leader type", () => LeaderTypeText(mleader.LeaderLineType), LeaderTypeChoices),
                        Pick("Arrowhead", () => CadArrowheads.NameOf(mleader.ArrowSymbol), CadArrowheads.Names),
                        Pick("Horizontal landing", () => YesNo(mleader.EnableLanding), YesNoChoices),
                        Row("Landing distance", () => Length(mleader.DoglegLength))
                    );
                    if (Read(() => mleader.ContentType == DxfContentType.MTextContent, false))
                    {
                        Group(
                            "Text",
                            Row("Contents", () => mleader.MText.Text),
                            Pick("Text style", () => mleader.MText.TextStyleName, TextStyles(f.Database)),
                            Row("Text height", () => Length(mleader.TextHeight))
                        );
                    }

                    break;
                case DxfDimension dimension:
                    groups.AddRange(DimensionGroups(dimension, f));
                    break;
                case DxfTolerance tolerance:
                {
                    // As AutoCAD's palette heads a TOLERANCE's rows, each variable read from its override, else its style's.
                    DxfDimStyleTableRecord data = Read(() => tolerance.GetDimstyleData(), null);
                    Group(
                        "Misc",
                        Pick(
                            "Dim style",
                            () => tolerance.DimensionStyleName,
                            CadDimStyleEditor.Styles(f.Database).Select(s => s.Name).ToList()
                        )
                    );
                    Group("Lines & Arrows", Row("Dim line color", () => ColorText(data.Dimclrd)));
                    Group(
                        "Text",
                        Row("Text color", () => ColorText(data.Dimclrt)),
                        Row("Text height", () => Length(data.Dimtxt))
                    );
                    break;
                }
                case DxfHatch hatch:
                {
                    bool gradient = Read(() => hatch.IsGradient, false);
                    bool solid = gradient || Read(() => hatch.IsSolidFill, false);
                    bool user = !solid && Read(() => hatch.PatternType == DxfHatchPatternType.UserDefined, false);
                    Group(
                        "Pattern",
                        Pick("Type", () => HatchTypeText(hatch.PatternType), HatchTypeChoices, gradient),
                        Row("Pattern name", () => gradient ? hatch.GradientName : hatch.PatternName, gradient),
                        Row("Angle", () => f.Angle(hatch.PatternAngle), solid),
                        Row("Scale", () => Length(hatch.PatternScale), solid || user),
                        Row("Spacing", () => Length(hatch.PatternSpace), !user),
                        Pick("Double", () => YesNo(hatch.PatternDouble), YesNoChoices, !user),
                        Row(
                            "Origin X",
                            () =>
                                solid || hatch.NumberOfPatternDefinitions == 0
                                    ? Length(0.0)
                                    : Length(hatch.GetPatternDefinitionAt(0).BaseX),
                            solid || user
                        ),
                        Row(
                            "Origin Y",
                            () =>
                                solid || hatch.NumberOfPatternDefinitions == 0
                                    ? Length(0.0)
                                    : Length(hatch.GetPatternDefinitionAt(0).BaseY),
                            solid || user
                        )
                    );
                    Group(
                        "Geometry",
                        Row("Elevation", () => Length(hatch.Elevation)),
                        Row("Area", () => Length(hatch.Area), true)
                    );
                    Group(
                        "Misc",
                        Row("Associative", () => YesNo(hatch.Associative), true),
                        Pick("Island detection", () => IslandText(hatch.HatchStyle), IslandChoices),
                        Row("Background color", () => BackgroundText(hatch.BackgroundColor))
                    );
                    break;
                }

                case DxfOle2Frame ole:
                    // As AutoCAD 2021's palette gives an OLE object: where and how large it stands, its scale in percent of the
                    // size it has at 100 percent, whether the two scale together, what kind of object it is and what application
                    // draws it, and the quality it plots in.
                    Group(
                        "OLE",
                        Row("Position X", () => Length(ole.Location.X)),
                        Row("Position Y", () => Length(ole.Location.Y)),
                        Row("Width", () => Length(ole.WcsWidth)),
                        Row("Height", () => Length(ole.WcsHeight)),
                        Row("Scale width", () => Percent(ole.ScaleWidth)),
                        Row("Scale height", () => Percent(ole.ScaleHeight)),
                        Pick("Lock aspect", () => YesNo(ole.LockAspect), YesNoChoices),
                        Row("Type", () => OleTypeText(ole.Type), true),
                        Pick("Plot quality", () => OleQualityText(ole.OutputQuality), OleQualityChoices),
                        Row("Source application", () => ole.UserType, true)
                    );
                    break;
                case DxfViewport viewport:
                    Group(
                        "Geometry",
                        Row("Center X", () => Length(viewport.CenterPoint.X)),
                        Row("Center Y", () => Length(viewport.CenterPoint.Y)),
                        Row("Center Z", () => Length(viewport.CenterPoint.Z)),
                        Row("Height", () => Length(viewport.Height)),
                        Row("Width", () => Length(viewport.Width))
                    );
                    Group(
                        "Misc",
                        Pick("On", () => YesNo(viewport.On), YesNoChoices),
                        Row("Clipped", () => YesNo(viewport.NonRectClipOn), true),
                        Pick("Display locked", () => YesNo(viewport.Locked), YesNoChoices),
                        Pick("Standard scale", () => ScaleText(viewport.StandardScale), ScaleChoices),
                        Row("Custom scale", () => Scale(viewport.CustomScale)),
                        Row("Visual style", () => "2D Wireframe", true)
                    );
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
                        rows.Add(
                            match.Value == row.Value && match.IsReadOnly == row.IsReadOnly
                                ? row
                                : new CadPropertyRow(
                                    row.Label,
                                    match.Value == row.Value ? row.Value : CadPropertyRow.VariesText,
                                    row.IsReadOnly || match.IsReadOnly,
                                    row.Choices
                                )
                        );
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
                case DxfLine _:
                    return "Line";
                case DxfCircle _:
                    return "Circle";
                case DxfArc _:
                    return "Arc";
                case DxfEllipse _:
                    return "Ellipse";
                case DxfPolyline _:
                    return "Polyline";
                case DxfPolyline2d _:
                    return "2D Polyline";
                case DxfPolyline3d _:
                    return "3D Polyline";
                case DxfSpline _:
                    return "Spline";
                case DxfLeader _:
                    return "Leader";
                case DxfXline _:
                    return "Xline";
                case DxfRay _:
                    return "Ray";
                case DxfAttributeDefinition _:
                    return "Attribute Definition";
                case DxfDBText _:
                    return "Text";
                case DxfMText _:
                    return "MText";
                case DxfMInsertBlock _:
                    return "MInsert Block";
                case DxfBlockReference _:
                    return "Block Reference";
                case DxfRotatedDimension _:
                    return "Rotated Dimension";
                case DxfAlignedDimension _:
                    return "Aligned Dimension";
                case DxfRadialDimension _:
                    return "Radial Dimension";
                case DxfRadialDimensionLarge _:
                    return "Radial Dimension Large";
                case DxfDiametricDimension _:
                    return "Diametric Dimension";
                case DxfArcDimension _:
                    return "Arc Dimension";
                case DxfOrdinateDimension _:
                    return "Ordinate Dimension";
                case DxfLineAngularDimension2 _:
                case DxfPoint3AngularDimension _:
                    return "Angular Dimension";
                case DxfDimension _:
                    return "Dimension";
                case DxfHatch _:
                    return "Hatch";
                case DxfViewport _:
                    return "Viewport";
                case DxfMLeader _:
                    return "Multileader";
                case DxfTable _:
                    return "Table";
                case DxfWipeout _:
                    return "Wipeout";
                case DxfRasterImage _:
                    return "Raster Image";
                case DxfSolid _:
                    return "Solid";
                case DxfFace _:
                    return "3D Face";
                case DxfDBPoint _:
                    return "Point";
                case DxfOle2Frame _:
                    return "OLE Object";
                default:
                    string name = entity.GetType().Name;
                    return name.StartsWith("Dxf", StringComparison.Ordinal) ? name.Substring(3) : name;
            }
        }

        private static bool IsAngular(DxfDimension dimension) =>
            dimension is DxfLineAngularDimension2 || dimension is DxfPoint3AngularDimension;

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
                return color.ColorName
                    ?? color.Red.ToString(CultureInfo.InvariantCulture)
                        + ","
                        + color.Green.ToString(CultureInfo.InvariantCulture)
                        + ","
                        + color.Blue.ToString(CultureInfo.InvariantCulture);
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
                case DxfLineWeight.ByLayer:
                    return "ByLayer";
                case DxfLineWeight.ByBlock:
                    return "ByBlock";
                case DxfLineWeight.ByLineWeightDefault:
                    return "Default";
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

        internal static string ScaleText(DxfStandardScaleType scale)
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

            if (
                name.StartsWith("Scale", StringComparison.Ordinal)
                && to > 5
                && int.TryParse(name.Substring(5, to - 5), NumberStyles.None, CultureInfo.InvariantCulture, out int a)
                && int.TryParse(name.Substring(to + 2), NumberStyles.None, CultureInfo.InvariantCulture, out int b)
            )
            {
                return a.ToString(CultureInfo.InvariantCulture) + ":" + b.ToString(CultureInfo.InvariantCulture);
            }

            return name;
        }

        private static string[] StandardScales()
        {
            var names = new List<string>();

            foreach (DxfStandardScaleType scale in (DxfStandardScaleType[])Enum.GetValues(typeof(DxfStandardScaleType)))
            {
                if (scale != DxfStandardScaleType.ScaleToFit && scale != DxfStandardScaleType.CustomScale)
                {
                    names.Add(ScaleText(scale));
                }
            }

            return names.ToArray();
        }

        /// <summary>The standard scale a name of <see cref="ScaleChoices"/> stands for, as 1:50; false for none.</summary>
        internal static bool TryParseScale(string text, out DxfStandardScaleType scale)
        {
            foreach (DxfStandardScaleType each in (DxfStandardScaleType[])Enum.GetValues(typeof(DxfStandardScaleType)))
            {
                if (
                    each != DxfStandardScaleType.ScaleToFit
                    && each != DxfStandardScaleType.CustomScale
                    && string.Equals(ScaleText(each), text, StringComparison.OrdinalIgnoreCase)
                )
                {
                    scale = each;
                    return true;
                }
            }

            scale = DxfStandardScaleType.CustomScale;
            return false;
        }

        /// <summary>The Attributes group of a block reference, a row of each attribute by its tag.</summary>
        private static void AttributesOf(DxfBlockReference insert, List<CadPropertyGroup> groups)
        {
            var attributes = new List<CadPropertyRow>();

            foreach (
                DxfAttributeReference attribute in Read(
                    () => (IEnumerable<DxfAttributeReference>)insert.AttributeCollection,
                    Array.Empty<DxfAttributeReference>()
                )
            )
            {
                if (
                    !attribute.IsErased
                    && !string.IsNullOrEmpty(attribute.Tag)
                    && attributes.TrueForAll(r => r.Label != attribute.Tag)
                )
                {
                    attributes.Add(
                        new CadPropertyRow(
                            attribute.Tag,
                            Read(() => attribute.TextString, string.Empty),
                            Read(() => attribute.IsConstant, false)
                        )
                    );
                }
            }

            if (attributes.Count > 0)
            {
                groups.Add(new CadPropertyGroup("Attributes", attributes));
            }
        }

        /// <summary>The names of the drawing's text styles, in the order of the table.</summary>
        private static IReadOnlyList<string> TextStyles(DxfDatabase database)
        {
            var names = new List<string>();

            foreach (
                DxfTextStyleTableRecord style in Read(
                    () => (IEnumerable<DxfTextStyleTableRecord>)database.TextStyleTable,
                    Array.Empty<DxfTextStyleTableRecord>()
                )
            )
            {
                if (!string.IsNullOrEmpty(style.Name) && !style.IsShapeFile)
                {
                    names.Add(style.Name);
                }
            }

            return names;
        }

        /// <summary>The names of the drawing's multileader styles, in the order of its dictionary.</summary>
        private static IReadOnlyList<string> MLeaderStyles(DxfDatabase database)
        {
            var names = new List<string>();

            foreach (
                DxfDBDictionaryEntry entry in Read(
                    () => (IEnumerable<DxfDBDictionaryEntry>)database.MLeaderStyleDictionary,
                    null
                ) ?? Array.Empty<DxfDBDictionaryEntry>()
            )
            {
                names.Add(entry.Key);
            }

            return names;
        }

        internal static string JustifyText(DxfAttachmentPoint attachment)
        {
            int index = (int)attachment - 1;
            return index >= 0 && index < JustifyChoices.Length ? JustifyChoices[index] : attachment.ToString();
        }

        internal static string DirectionText(DxfFlowDirection direction)
        {
            switch (direction)
            {
                case DxfFlowDirection.TopToBottom:
                    return "Top to bottom";
                case DxfFlowDirection.ByStyle:
                    return "By style";
                case DxfFlowDirection.RightToLeft:
                    return "Right to left";
                case DxfFlowDirection.BottomToTop:
                    return "Bottom to top";
                default:
                    return "Left to right";
            }
        }

        internal static string ColumnsText(DxfColumnType type) =>
            type == DxfColumnType.StaticColumns ? "Static"
            : type == DxfColumnType.DynamicColumns ? "Dynamic"
            : "None";

        internal static string HatchTypeText(DxfHatchPatternType type) =>
            type == DxfHatchPatternType.UserDefined ? "User defined"
            : type == DxfHatchPatternType.CustomDefined ? "Custom"
            : "Predefined";

        internal static string IslandText(DxfHatchStyle style) =>
            style == DxfHatchStyle.Outer ? "Outer"
            : style == DxfHatchStyle.Ignore ? "Ignore"
            : "Normal";

        internal static string LeaderTypeText(DxfLeaderType type) =>
            type == DxfLeaderType.InVisibleLeader ? "None"
            : type == DxfLeaderType.SplineLeader ? "Spline"
            : "Straight";

        /// <summary>The background colour of a hatch as the palette writes it: None, or a colour as <see cref="ColorText"/> writes one.</summary>
        private static string BackgroundText(DxfColor color) =>
            color == null || color.IsNone ? "None" : ColorText(color);

        /// <summary>A scale to as many places as tell it from its neighbours, 0.0200 for 1:50 and 0.003333333333333 for 1:300.</summary>
        private static string Scale(double value) => value.ToString("0.0000###########", CultureInfo.InvariantCulture);

        private static string YesNo(bool value) => value ? "Yes" : "No";

        /// <summary>A length or coordinate to 4 places, as AutoCAD's palette writes one at <c>$LUPREC</c> 4.</summary>
        /// <summary>The words of the Plot quality row of an OLE object, by group 73: 0, 1 and 2 as AutoCAD 2021 names them, 3 (automatic) as high graphics, which is what a picture is plotted at.</summary>
        internal static readonly string[] OleQualityChoices =
        {
            "Monochrome (e.g. spreadsheet)",
            "Low graphics (e.g. color text)",
            "High graphics (e.g. photograph)",
        };

        private static string OleQualityText(byte quality) => OleQualityChoices[Math.Min((int)quality, 2)];

        private static string OleTypeText(DxfOle2FrameItemType type) =>
            type == DxfOle2FrameItemType.Link ? "Linked"
            : type == DxfOle2FrameItemType.Embedded ? "Embedded"
            : "Static";

        /// <summary>A scale in percent as the palette writes it: 100%, 88.0349%.</summary>
        private static string Percent(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.####", CultureInfo.InvariantCulture) + "%";

        private static string Length(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.0000", CultureInfo.InvariantCulture);

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
                Database = database;
            }

            /// <summary>The drawing, whose styles the rows of a dimension, a hatch or an MTEXT offer.</summary>
            public DxfDatabase Database { get; }

            /// <summary>The vertex of a polyline shown, from 1, as <see cref="CadSelection.CurrentVertex"/>.</summary>
            public int Vertex { get; set; } = 1;

            /// <summary>
            /// A direction in radians from the X axis written in degrees from 0 up to 360, counted as the drawing counts
            /// angles: from ANGBASE, and clockwise with ANGDIR 1.
            /// </summary>
            public string Angle(double radians) =>
                Plain(
                    CadUnits.FromDirection(
                        radians,
                        Read(() => Database.Angbase, 0.0),
                        Read(() => Database.Angdir, false)
                    )
                );

            /// <summary>An angle in radians written in degrees from 0 up to 360.</summary>
            public string Plain(double radians)
            {
                double degrees = radians * 180.0 / Math.PI;
                degrees -= Math.Floor(degrees / 360.0) * 360.0;
                string text = degrees.ToString(_angle, CultureInfo.InvariantCulture);
                return text == 360.0.ToString(_angle, CultureInfo.InvariantCulture)
                    ? 0.0.ToString(_angle, CultureInfo.InvariantCulture)
                    : text;
            }
        }
    }
}
