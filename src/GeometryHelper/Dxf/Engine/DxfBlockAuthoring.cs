using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>The kinds of parameter and action a dynamic block's evaluation graph holds, as AutoCAD's Block Editor shows them.</summary>
    internal enum DxfBlockElementKind
    {
        /// <summary>A point parameter, a position.</summary>
        Point,

        /// <summary>A linear parameter, a distance.</summary>
        Linear,

        /// <summary>A polar parameter, a distance and an angle.</summary>
        Polar,

        /// <summary>An XY parameter, a distance along x and one along y.</summary>
        XY,

        /// <summary>A rotation parameter, an angle.</summary>
        Rotation,

        /// <summary>A flip parameter, a mirror.</summary>
        Flip,

        /// <summary>An alignment parameter.</summary>
        Alignment,

        /// <summary>A visibility parameter.</summary>
        Visibility,

        /// <summary>A lookup parameter.</summary>
        Lookup,

        /// <summary>A base point parameter.</summary>
        BasePoint,

        /// <summary>A block properties table.</summary>
        BlockTable,

        /// <summary>A parameter of another kind, as a constraint parameter.</summary>
        OtherParameter,

        /// <summary>A move action.</summary>
        Move,

        /// <summary>A scale action.</summary>
        Scale,

        /// <summary>A stretch action.</summary>
        Stretch,

        /// <summary>A polar stretch action.</summary>
        PolarStretch,

        /// <summary>An array action.</summary>
        Array,

        /// <summary>A rotate action.</summary>
        Rotate,

        /// <summary>A flip action.</summary>
        FlipAction,

        /// <summary>A lookup action.</summary>
        LookupAction,

        /// <summary>An action of another kind.</summary>
        OtherAction,
    }

    /// <summary>
    /// One parameter or action of a dynamic block's evaluation graph, read as AutoCAD's Block Editor lists it: what kind it
    /// is, its name, the labels of the properties it gives, where it stands, the entities an action works on.
    /// </summary>
    internal sealed class DxfBlockElement
    {
        internal DxfBlockElement(DxfBlockTableRecord block, DxfItem item, DxfBlockElementKind kind, int node)
        {
            Block = block;
            Item = item;
            Kind = kind;
            Node = node;
        }

        /// <summary>The block the element is of.</summary>
        public DxfBlockTableRecord Block { get; }

        /// <summary>The element's object in the file.</summary>
        public DxfItem Item { get; }

        /// <summary>What the element is.</summary>
        public DxfBlockElementKind Kind { get; }

        /// <summary>The id of the element's node in the graph (90), which an action names its parameter by.</summary>
        public int Node { get; }

        /// <summary>The handle of the element's object.</summary>
        public string Handle => Item.Handle;

        /// <summary>Whether the element is a parameter (or a block properties table), else an action.</summary>
        public bool IsParameter => Kind <= DxfBlockElementKind.OtherParameter;

        /// <summary>The name of the element, as AutoCAD's Name row shows it: Linear, Linear1, Stretch.</summary>
        public string Name { get; internal set; } = string.Empty;

        /// <summary>The labels of the properties a parameter gives, in the order its Properties rows list them: one, but two for an XY parameter (x then y) and a polar one (distance then angle).</summary>
        public IReadOnlyList<string> Labels { get; internal set; } = new string[0];

        /// <summary>The descriptions of those properties, parallel to <see cref="Labels"/>.</summary>
        public IReadOnlyList<string> Descriptions { get; internal set; } = new string[0];

        /// <summary>The value sets of those properties, parallel to <see cref="Labels"/>; null for a property that takes any value.</summary>
        public IReadOnlyList<DxfBlockParameterValueSet> ValueSets { get; internal set; } =
            new DxfBlockParameterValueSet[0];

        /// <summary>The point a parameter of two points is measured from, or its one point; null for none.</summary>
        public GeoPoint3? Base { get; internal set; }

        /// <summary>The second point of a parameter of two points; null for none.</summary>
        public GeoPoint3? End { get; internal set; }

        /// <summary>Where the element's label or action bar is shown; null where the file gives none.</summary>
        public GeoPoint3? At { get; internal set; }

        /// <summary>The handles of the entities an action works on, in the order it holds them; a visibility parameter's governed entities.</summary>
        public IReadOnlyList<string> Entities { get; internal set; } = new string[0];

        /// <summary>The id of the parameter node an action follows (92); -1 for none.</summary>
        public int ParameterNode { get; internal set; } = -1;

        /// <summary>Whether a parameter's property is listed in the Properties palette of an INSERT (280).</summary>
        public bool ShowProperties { get; internal set; } = true;

        /// <summary>Whether a parameter's actions work when another action edits it (281).</summary>
        public bool ChainActions { get; internal set; }

        /// <summary>An action's distance multiplier (move, stretch, polar stretch); 1 for none.</summary>
        public double Multiplier { get; internal set; } = 1.0;

        /// <summary>An action's angle offset in radians (move, stretch, polar stretch); nought for none.</summary>
        public double AngleOffset { get; internal set; }

        /// <summary>An array action's distance between rows (140), nought for none.</summary>
        public double RowSpacing { get; internal set; }

        /// <summary>An array action's distance between columns (141), nought for none.</summary>
        public double ColumnSpacing { get; internal set; }

        /// <summary>The frame of a stretch or polar stretch action as two corners, low then high; null for none.</summary>
        public GeoPoint2[] Frame { get; internal set; }

        /// <summary>The parameter as the library's own type (<see cref="DxfBlockLinearParameter"/>, <see cref="DxfBlockXYParameter"/>, ...), which the Add...Action methods take; null for a kind that has none.</summary>
        public object Parameter { get; internal set; }
    }

    /// <summary>
    /// Reads the parameters and actions of a dynamic block as AutoCAD's Block Editor lists them, and edits them in place: the
    /// name, the labels, the value sets, the flags, an action's multiplier and offset, and takes one out of the graph.
    /// </summary>
    internal static class DxfBlockAuthoring
    {
        /// <summary>The parameters and actions of the block's evaluation graph, in the order of its nodes; none where it has no graph.</summary>
        internal static List<DxfBlockElement> Elements(DxfBlockTableRecord block)
        {
            var found = new List<DxfBlockElement>();
            DxfDBObject graph = DxfDynamicProperties.Entry(block.ExtensionDictionary, "ACAD_ENHANCEDBLOCK");

            if (graph?.Item == null || graph.Item.Type != "ACAD_EVALUATION_GRAPH")
            {
                return found;
            }

            DxfFile file = graph.Item.File;

            foreach (DxfTypedValue group in graph.Item.Groups)
            {
                if (group.TypeCode == 360 && file.ObjectItem(group.Value) is DxfItem node)
                {
                    DxfBlockElement element = Read(block, node);

                    if (element != null)
                    {
                        found.Add(element);
                    }
                }
            }

            return found;
        }

        /// <summary>The kind an object type is, null for a grip, a location component or anything that is no element.</summary>
        private static DxfBlockElementKind? KindOf(string type)
        {
            switch (type)
            {
                case "BLOCKPOINTPARAMETER":
                    return DxfBlockElementKind.Point;
                case "BLOCKLINEARPARAMETER":
                    return DxfBlockElementKind.Linear;
                case "BLOCKPOLARPARAMETER":
                    return DxfBlockElementKind.Polar;
                case "BLOCKXYPARAMETER":
                    return DxfBlockElementKind.XY;
                case "BLOCKROTATIONPARAMETER":
                    return DxfBlockElementKind.Rotation;
                case "BLOCKFLIPPARAMETER":
                    return DxfBlockElementKind.Flip;
                case "BLOCKALIGNMENTPARAMETER":
                    return DxfBlockElementKind.Alignment;
                case "BLOCKVISIBILITYPARAMETER":
                    return DxfBlockElementKind.Visibility;
                case "BLOCKLOOKUPPARAMETER":
                    return DxfBlockElementKind.Lookup;
                case "BLOCKBASEPOINTPARAMETER":
                    return DxfBlockElementKind.BasePoint;
                case "BLOCKPROPERTIESTABLE":
                    return DxfBlockElementKind.BlockTable;
                case "BLOCKMOVEACTION":
                    return DxfBlockElementKind.Move;
                case "BLOCKSCALEACTION":
                    return DxfBlockElementKind.Scale;
                case "BLOCKSTRETCHACTION":
                    return DxfBlockElementKind.Stretch;
                case "BLOCKPOLARSTRETCHACTION":
                    return DxfBlockElementKind.PolarStretch;
                case "BLOCKARRAYACTION":
                    return DxfBlockElementKind.Array;
                case "BLOCKROTATEACTION":
                    return DxfBlockElementKind.Rotate;
                case "BLOCKFLIPACTION":
                    return DxfBlockElementKind.FlipAction;
                case "BLOCKLOOKUPACTION":
                    return DxfBlockElementKind.LookupAction;
            }

            if (type == null || !type.StartsWith("BLOCK", StringComparison.Ordinal))
            {
                return null;
            }

            if (type.EndsWith("PARAMETER", StringComparison.Ordinal))
            {
                return DxfBlockElementKind.OtherParameter;
            }

            return type.EndsWith("ACTION", StringComparison.Ordinal)
                ? DxfBlockElementKind.OtherAction
                : (DxfBlockElementKind?)null;
        }

        private static DxfBlockElement Read(DxfBlockTableRecord block, DxfItem node)
        {
            DxfBlockElementKind? kind = KindOf(node.Type);

            if (kind == null)
            {
                return null;
            }

            IReadOnlyList<DxfTypedValue> groups = node.Groups;
            string id = DxfDynamicProperties.Text(DxfDynamicProperties.Section(groups, "AcDbEvalExpr"), 90)?.Trim();

            if (!int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return null;
            }

            var element = new DxfBlockElement(block, node, kind.Value, number);
            element.Name = DxfReader.NameOf(
                DxfDynamicProperties.Text(DxfDynamicProperties.Section(groups, "AcDbBlockElement"), 300) ?? string.Empty
            );

            if (element.IsParameter)
            {
                ReadParameter(element, groups);
            }
            else
            {
                ReadAction(element, groups);
            }

            IReadOnlyList<DxfTypedValue> environ = DxfDBObject.OfObject(node).GetXDataForApplication("ACAUTHENVIRON");

            if (environ != null && element.At == null)
            {
                element.At = XDataPoint(environ);
            }

            return element;
        }

        private static GeoPoint3? XDataPoint(IReadOnlyList<DxfTypedValue> data)
        {
            var found = new List<double>();

            foreach (DxfTypedValue group in data)
            {
                if (
                    (group.TypeCode == 1010 || group.TypeCode == 1020 || group.TypeCode == 1030)
                    && DxfDynamicProperties.Number(group.Value, out double value)
                )
                {
                    found.Add(value);
                }
            }

            return found.Count >= 2
                ? new GeoPoint3(found[0], found[1], found.Count > 2 ? found[2] : 0.0)
                : (GeoPoint3?)null;
        }

        private static GeoPoint3? PointOf(List<DxfTypedValue> section, int code)
        {
            double[] point = DxfDynamicProperties.PointOf(section, code);
            return point == null ? (GeoPoint3?)null : new GeoPoint3(point[0], point[1], point[2]);
        }

        private static string Words(List<DxfTypedValue> section, int code) =>
            DxfReader.Decode(DxfDynamicProperties.Text(section, code) ?? string.Empty);

        private static string Label(List<DxfTypedValue> section, int code) =>
            DxfReader.NameOf(DxfDynamicProperties.Text(section, code) ?? string.Empty);

        private static void ReadParameter(DxfBlockElement element, IReadOnlyList<DxfTypedValue> groups)
        {
            List<DxfTypedValue> head = DxfDynamicProperties.Section(groups, "AcDbBlockParameter");
            element.ShowProperties = DxfDynamicProperties.Text(head, 280)?.Trim() != "0";
            element.ChainActions = DxfDynamicProperties.Text(head, 281)?.Trim() == "1";
            List<DxfTypedValue> two = DxfDynamicProperties.Section(groups, "AcDbBlock2PtParameter");
            List<DxfTypedValue> one = DxfDynamicProperties.Section(groups, "AcDbBlock1PtParameter");

            if (two.Count > 0)
            {
                element.Base = PointOf(two, 1010);
                element.End = PointOf(two, 1011);
            }
            else if (one.Count > 0)
            {
                element.Base = PointOf(one, 1010);
            }

            DxfBlockTableRecord block = element.Block;
            string handle = element.Handle;
            GeoPoint3? baseAt = element.Base;
            GeoPoint3? endAt = element.End;

            switch (element.Kind)
            {
                case DxfBlockElementKind.Linear:
                {
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockLinearParameter");
                    element.Labels = new[] { Label(own, 305) };
                    element.Descriptions = new[] { Words(own, 306) };
                    element.ValueSets = new[] { ValueSetOf(own, 96, 141, 175, false) };
                    double offset = Number(DxfDynamicProperties.Text(own, 140));
                    element.Parameter =
                        !baseAt.HasValue || !endAt.HasValue
                            ? null
                            : new DxfBlockLinearParameter(
                                block,
                                element.Node,
                                handle,
                                element.Labels[0],
                                element.Descriptions[0],
                                baseAt.Value,
                                endAt.Value,
                                offset,
                                element.ValueSets[0]
                            );
                    break;
                }

                case DxfBlockElementKind.Polar:
                {
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockPolarParameter");
                    element.Labels = new[] { Label(own, 305), Label(own, 307) };
                    element.Descriptions = new[] { Words(own, 306), Words(own, 308) };
                    element.ValueSets = new[]
                    {
                        ValueSetOf(own, 96, 141, 175, false),
                        ValueSetOf(own, 97, 145, 176, true),
                    };
                    double offset = Number(DxfDynamicProperties.Text(own, 140));
                    element.Parameter =
                        !baseAt.HasValue || !endAt.HasValue
                            ? null
                            : new DxfBlockPolarParameter(
                                block,
                                element.Node,
                                handle,
                                element.Labels[0],
                                element.Descriptions[0],
                                element.Labels[1],
                                element.Descriptions[1],
                                baseAt.Value,
                                endAt.Value,
                                offset,
                                element.ValueSets[0],
                                element.ValueSets[1]
                            );
                    break;
                }

                case DxfBlockElementKind.XY:
                {
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockXYParameter");

                    // AutoCAD writes the label of y first (305) and of x second (306), the descriptions alike, and the sets of y
                    // (97) before those of x (96); the rows list x first.
                    element.Labels = new[] { Label(own, 306), Label(own, 305) };
                    element.Descriptions = new[] { Words(own, 308), Words(own, 307) };
                    element.ValueSets = new[]
                    {
                        ValueSetOf(own, 96, 142, 175, false),
                        ValueSetOf(own, 97, 146, 176, false),
                    };
                    element.Parameter =
                        !baseAt.HasValue || !endAt.HasValue
                            ? null
                            : new DxfBlockXYParameter(
                                block,
                                element.Node,
                                handle,
                                element.Labels[0],
                                element.Descriptions[0],
                                element.Labels[1],
                                element.Descriptions[1],
                                baseAt.Value,
                                endAt.Value,
                                element.ValueSets[0],
                                element.ValueSets[1]
                            );
                    break;
                }

                case DxfBlockElementKind.Rotation:
                {
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockRotationParameter");
                    element.Labels = new[] { Label(own, 305) };
                    element.Descriptions = new[] { Words(own, 306) };
                    element.ValueSets = new[] { ValueSetOf(own, 96, 141, 175, true) };

                    if (baseAt.HasValue && endAt.HasValue)
                    {
                        GeoVector3 along = baseAt.Value.GetVectorTo(endAt.Value);
                        element.Parameter = new DxfBlockRotationParameter(
                            block,
                            element.Node,
                            handle,
                            element.Labels[0],
                            element.Descriptions[0],
                            baseAt.Value,
                            along.Length,
                            Math.Atan2(along.Y, along.X),
                            element.ValueSets[0]
                        );
                    }

                    break;
                }

                case DxfBlockElementKind.Flip:
                {
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockFlipParameter");
                    element.Labels = new[] { Label(own, 305) };
                    element.Descriptions = new[] { Words(own, 306) };
                    element.ValueSets = new DxfBlockParameterValueSet[1];
                    element.Parameter =
                        !baseAt.HasValue || !endAt.HasValue
                            ? null
                            : new DxfBlockFlipParameter(
                                block,
                                element.Node,
                                handle,
                                element.Labels[0],
                                element.Descriptions[0],
                                baseAt.Value,
                                endAt.Value
                            );
                    break;
                }

                case DxfBlockElementKind.Point:
                {
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockPointParameter");
                    element.Labels = new[] { Label(own, 303) };
                    element.Descriptions = new[] { Words(own, 304) };
                    element.ValueSets = new DxfBlockParameterValueSet[1];
                    element.Parameter = !baseAt.HasValue
                        ? null
                        : new DxfBlockPointParameter(
                            block,
                            element.Node,
                            handle,
                            element.Labels[0],
                            element.Descriptions[0],
                            baseAt.Value
                        );
                    break;
                }

                case DxfBlockElementKind.Visibility:
                {
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockVisibilityParameter");
                    element.Labels = new[] { Label(own, 301) };
                    element.Descriptions = new[] { Words(own, 302) };
                    element.ValueSets = new DxfBlockParameterValueSet[1];
                    var governed = new List<string>();

                    foreach (DxfTypedValue group in own)
                    {
                        if (group.TypeCode == 331)
                        {
                            governed.Add(group.Value?.Trim());
                        }
                    }

                    element.Entities = governed;
                    break;
                }

                case DxfBlockElementKind.Lookup:
                {
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockLookUpParameter");
                    element.Labels = new[] { Label(own, 303) };
                    element.Descriptions = new[] { Words(own, 304) };
                    element.ValueSets = new DxfBlockParameterValueSet[1];
                    break;
                }
            }
        }

        private static void ReadAction(DxfBlockElement element, IReadOnlyList<DxfTypedValue> groups)
        {
            List<DxfTypedValue> head = DxfDynamicProperties.Section(groups, "AcDbBlockAction");
            element.At = PointOf(head, 1010);
            var acted = new List<string>();

            foreach (DxfTypedValue group in head)
            {
                if (group.TypeCode == 330)
                {
                    acted.Add(group.Value?.Trim());
                }
            }

            element.Entities = acted;

            // The parameter an action follows is the node of the first connection (92), in the section of its kind or, for a scale
            // and a rotate, of the base it has (AcDbBlockActionWithBasePt).
            string own = null;

            switch (element.Kind)
            {
                case DxfBlockElementKind.Move:
                    own = "AcDbBlockMoveAction";
                    break;
                case DxfBlockElementKind.Stretch:
                    own = "AcDbBlockStretchAction";
                    break;
                case DxfBlockElementKind.PolarStretch:
                    own = "AcDbBlockPolarStretchAction";
                    break;
                case DxfBlockElementKind.Array:
                    own = "AcDbBlockArrayAction";
                    break;
                case DxfBlockElementKind.FlipAction:
                    own = "AcDbBlockFlipAction";
                    break;
                case DxfBlockElementKind.Scale:
                case DxfBlockElementKind.Rotate:
                    own = "AcDbBlockActionWithBasePt";
                    break;
            }

            if (own != null)
            {
                string parameter = DxfDynamicProperties.Text(DxfDynamicProperties.Section(groups, own), 92)?.Trim();

                if (int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out int node))
                {
                    element.ParameterNode = node;
                }
            }

            switch (element.Kind)
            {
                case DxfBlockElementKind.Move:
                case DxfBlockElementKind.Stretch:
                {
                    List<DxfTypedValue> section = DxfDynamicProperties.Section(groups, own);
                    element.Multiplier = Number(DxfDynamicProperties.Text(section, 140), 1.0);
                    element.AngleOffset = Number(DxfDynamicProperties.Text(section, 141));

                    if (element.Kind == DxfBlockElementKind.Stretch)
                    {
                        element.Frame = Frame(section, 72, 1011);
                    }

                    break;
                }

                case DxfBlockElementKind.PolarStretch:
                {
                    List<DxfTypedValue> section = DxfDynamicProperties.Section(groups, own);
                    element.Multiplier = Number(DxfDynamicProperties.Text(section, 141), 1.0);
                    element.AngleOffset = Number(DxfDynamicProperties.Text(section, 140));
                    element.Frame = Frame(section, 73, 1011);
                    break;
                }

                case DxfBlockElementKind.Array:
                {
                    List<DxfTypedValue> section = DxfDynamicProperties.Section(groups, own);
                    element.RowSpacing = Number(DxfDynamicProperties.Text(section, 140));
                    element.ColumnSpacing = Number(DxfDynamicProperties.Text(section, 141));
                    break;
                }
            }
        }

        /// <summary>The two corners of a stretch frame, as many corners as the count group says: the least and the greatest point of them; null where there are none.</summary>
        private static GeoPoint2[] Frame(List<DxfTypedValue> section, int count, int code)
        {
            double minX = double.MaxValue,
                minY = double.MaxValue,
                maxX = double.MinValue,
                maxY = double.MinValue;
            bool any = false;

            for (int i = 0; i < section.Count; i++)
            {
                if (
                    section[i].TypeCode == code
                    && i + 1 < section.Count
                    && section[i + 1].TypeCode == code + 10
                    && DxfDynamicProperties.Number(section[i].Value, out double x)
                    && DxfDynamicProperties.Number(section[i + 1].Value, out double y)
                )
                {
                    any = true;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            return any ? new[] { new GeoPoint2(minX, minY), new GeoPoint2(maxX, maxY) } : null;
        }

        private static double Number(string text, double fallback = 0.0) =>
            DxfDynamicProperties.Number(text, out double value) ? value : fallback;

        /// <summary>
        /// The value set a parameter's groups hold: the flags (the code given), then the minimum, maximum and increment from the
        /// code after, the list counted by the count code and listed after the increment's code; null for flags that are none (1
        /// of a distance, 3 of an angle), or a set that cannot be read.
        /// </summary>
        private static DxfBlockParameterValueSet ValueSetOf(
            List<DxfTypedValue> section,
            int flagsCode,
            int first,
            int countCode,
            bool angle
        )
        {
            string flags = DxfDynamicProperties.Text(section, flagsCode)?.Trim();

            if (flags == "8")
            {
                var values = new List<double>();

                foreach (DxfTypedValue group in section)
                {
                    if (
                        group.TypeCode == first + 3
                        && DxfDynamicProperties.Number(group.Value, out double value)
                        && value >= 0.0
                    )
                    {
                        values.Add(value);
                    }
                }

                return values.Count == 0 ? null : DxfBlockParameterValueSet.List(values);
            }

            if (flags == "7" || flags == "6" || flags == "5" || flags == "4")
            {
                double minimum = Number(DxfDynamicProperties.Text(section, first));
                double maximum = Number(DxfDynamicProperties.Text(section, first + 1));
                double increment = Number(DxfDynamicProperties.Text(section, first + 2));

                if (minimum >= 0.0 && maximum > minimum && increment > 0.0)
                {
                    return DxfBlockParameterValueSet.Increments(minimum, maximum, increment);
                }
            }

            return null;
        }

        // ----- editing in place -----

        /// <summary>Where a group of a code stands among the groups of an object, counted as the file counts them, in the subclass of a marker; -1 where it has none.</summary>
        private static int IndexOf(DxfItem item, string marker, int code, int occurrence = 0)
        {
            IReadOnlyList<DxfTypedValue> groups = item.Groups;
            bool inside = false;
            int seen = 0;

            for (int i = 0; i < groups.Count; i++)
            {
                DxfTypedValue group = groups[i];

                if (group.TypeCode == 100 || group.TypeCode == 1001)
                {
                    if (inside)
                    {
                        break;
                    }

                    inside =
                        group.TypeCode == 100 && string.Equals(group.Value?.Trim(), marker, StringComparison.Ordinal);
                    continue;
                }

                if (inside && group.TypeCode == code && seen++ == occurrence)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string Real(double value) => Numbers.Exact(value + 0.0);

        private static void Set(DxfItem item, string marker, int code, string value, int occurrence = 0)
        {
            int at = IndexOf(item, marker, code, occurrence);

            if (at < 0)
            {
                throw new InvalidOperationException(
                    "The object " + item.Type + " has no group " + code.ToString(CultureInfo.InvariantCulture) + "."
                );
            }

            item.File.SetValueAt(item, at, value);
        }

        /// <summary>The marker, the label code and the description code of the property of a parameter, by its place in <see cref="DxfBlockElement.Labels"/>; null where the parameter gives none there.</summary>
        private static bool Property(
            DxfBlockElementKind kind,
            int which,
            out string marker,
            out int label,
            out int description
        )
        {
            marker = null;
            label = 0;
            description = 0;

            switch (kind)
            {
                case DxfBlockElementKind.Linear when which == 0:
                    marker = "AcDbBlockLinearParameter";
                    label = 305;
                    description = 306;
                    return true;
                case DxfBlockElementKind.Rotation when which == 0:
                    marker = "AcDbBlockRotationParameter";
                    label = 305;
                    description = 306;
                    return true;
                case DxfBlockElementKind.Flip when which == 0:
                    marker = "AcDbBlockFlipParameter";
                    label = 305;
                    description = 306;
                    return true;
                case DxfBlockElementKind.Polar when which == 0 || which == 1:
                    marker = "AcDbBlockPolarParameter";
                    label = which == 0 ? 305 : 307;
                    description = which == 0 ? 306 : 308;
                    return true;
                case DxfBlockElementKind.XY when which == 0 || which == 1:
                    marker = "AcDbBlockXYParameter";
                    label = which == 0 ? 306 : 305;
                    description = which == 0 ? 308 : 307;
                    return true;
                case DxfBlockElementKind.Point when which == 0:
                    marker = "AcDbBlockPointParameter";
                    label = 303;
                    description = 304;
                    return true;
                case DxfBlockElementKind.Visibility when which == 0:
                    marker = "AcDbBlockVisibilityParameter";
                    label = 301;
                    description = 302;
                    return true;
                case DxfBlockElementKind.Lookup when which == 0:
                    marker = "AcDbBlockLookUpParameter";
                    label = 303;
                    description = 304;
                    return true;
            }

            return false;
        }

        /// <summary>The names no other element of the block has taken: false for one that is empty, of more than a line, or taken by another element.</summary>
        internal static bool IsFreeName(DxfBlockTableRecord block, DxfBlockElement element, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                return false;
            }

            foreach (DxfBlockElement other in Elements(block))
            {
                if (
                    other.Node != element.Node
                    && string.Equals(other.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
                )
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether a property label is free in the block: no other property of any parameter has it, letters of either case the same.</summary>
        internal static bool IsFreeLabel(DxfBlockTableRecord block, DxfBlockElement element, int which, string label)
        {
            if (string.IsNullOrWhiteSpace(label) || label.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                return false;
            }

            foreach (DxfBlockElement other in Elements(block))
            {
                for (int i = 0; i < other.Labels.Count; i++)
                {
                    if (
                        (other.Node != element.Node || i != which)
                        && string.Equals(other.Labels[i], label.Trim(), StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Renames an element, as the Name row of its Properties does.</summary>
        /// <exception cref="ArgumentException">Thrown when the name is empty, of more than a line, or another element's.</exception>
        internal static void Rename(DxfBlockElement element, string name)
        {
            if (!IsFreeName(element.Block, element, name))
            {
                throw new ArgumentException("A parameter with that name already exists in the block.", nameof(name));
            }

            Set(element.Item, "AcDbBlockElement", 300, name.Trim());
        }

        /// <summary>Gives a parameter's property a label (the Distance name row of its Properties).</summary>
        /// <exception cref="ArgumentException">Thrown when the label is empty, of more than a line, another property's, or the parameter has no such property.</exception>
        internal static void SetLabel(DxfBlockElement element, int which, string label)
        {
            if (!Property(element.Kind, which, out string marker, out int code, out _))
            {
                throw new ArgumentException("The parameter has no such property.", nameof(which));
            }

            if (!IsFreeLabel(element.Block, element, which, label))
            {
                throw new ArgumentException("A property with that label already exists in the block.", nameof(label));
            }

            Set(element.Item, marker, code, label.Trim());
        }

        /// <summary>Gives a parameter's property a description.</summary>
        /// <exception cref="ArgumentException">Thrown when the description is of more than a line or the parameter has no such property.</exception>
        internal static void SetDescription(DxfBlockElement element, int which, string description)
        {
            if (!Property(element.Kind, which, out string marker, out _, out int code))
            {
                throw new ArgumentException("The parameter has no such property.", nameof(which));
            }

            description = description ?? string.Empty;

            if (description.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new ArgumentException("The description of a property is of one line.", nameof(description));
            }

            Set(element.Item, marker, code, description);
        }

        /// <summary>
        /// Puts an element where AutoCAD shows it: a parameter's label (the point of its ACAUTHENVIRON data) or an action's bar
        /// (1010 of its AcDbBlockAction). An alignment and a base point parameter have none, as AutoCAD writes them.
        /// </summary>
        internal static void SetLocation(DxfBlockElement element, GeoPoint3 at)
        {
            if (element.IsParameter)
            {
                if (element.Kind == DxfBlockElementKind.Alignment || element.Kind == DxfBlockElementKind.BasePoint)
                {
                    return;
                }

                DxfDBObject.OfObject(element.Item).XData = new[]
                {
                    new DxfTypedValue(1001, "ACAUTHENVIRON"),
                    new DxfTypedValue(1010, Real(at.X)),
                    new DxfTypedValue(1020, Real(at.Y)),
                    new DxfTypedValue(1030, Real(at.Z)),
                };
                return;
            }

            Set(element.Item, "AcDbBlockAction", 1010, Real(at.X));
            Set(element.Item, "AcDbBlockAction", 1020, Real(at.Y));
            Set(element.Item, "AcDbBlockAction", 1030, Real(at.Z));
        }

        /// <summary>Sets whether the parameter's property shows in the Properties palette of an INSERT (280).</summary>
        internal static void SetShowProperties(DxfBlockElement element, bool show) =>
            Set(element.Item, "AcDbBlockParameter", 280, show ? "1" : "0");

        /// <summary>Sets whether the parameter's actions work when another action edits it (281).</summary>
        internal static void SetChainActions(DxfBlockElement element, bool chain) =>
            Set(element.Item, "AcDbBlockParameter", 281, chain ? "1" : "0");

        /// <summary>Sets an action's distance multiplier.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number is not finite.</exception>
        internal static void SetMultiplier(DxfBlockElement element, double multiplier)
        {
            Guard.Finite(multiplier, nameof(multiplier), "The multiplier of an action is a finite number.");

            switch (element.Kind)
            {
                case DxfBlockElementKind.Move:
                    Set(element.Item, "AcDbBlockMoveAction", 140, Factor(multiplier));
                    break;
                case DxfBlockElementKind.Stretch:
                    Set(element.Item, "AcDbBlockStretchAction", 140, Factor(multiplier));
                    break;
                case DxfBlockElementKind.PolarStretch:
                    Set(element.Item, "AcDbBlockPolarStretchAction", 141, Factor(multiplier));
                    break;
                default:
                    throw new NotSupportedException("The action has no distance multiplier.");
            }
        }

        /// <summary>Sets an action's angle offset in radians.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number is not finite.</exception>
        internal static void SetAngleOffset(DxfBlockElement element, double offset)
        {
            Guard.Finite(offset, nameof(offset), "The angle offset of an action is a finite number.");

            switch (element.Kind)
            {
                case DxfBlockElementKind.Move:
                    Set(element.Item, "AcDbBlockMoveAction", 141, Factor(offset));
                    break;
                case DxfBlockElementKind.Stretch:
                    Set(element.Item, "AcDbBlockStretchAction", 141, Factor(offset));
                    break;
                case DxfBlockElementKind.PolarStretch:
                    Set(element.Item, "AcDbBlockPolarStretchAction", 140, Factor(offset));
                    break;
                default:
                    throw new NotSupportedException("The action has no angle offset.");
            }
        }

        /// <summary>Sets an array action's distance between columns (141), and between rows (140), each above the point tolerance or nought for none.</summary>
        internal static void SetSpacing(DxfBlockElement element, bool rows, double spacing)
        {
            if (element.Kind != DxfBlockElementKind.Array)
            {
                throw new NotSupportedException("The action has no spacing.");
            }

            if (!Guard.IsFinite(spacing) || spacing < 0.0 || (spacing > 0.0 && spacing <= Tolerance.Default.EqualPoint))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spacing),
                    spacing,
                    "The spacing of an array is nought or a finite number above the point tolerance."
                );
            }

            Set(element.Item, "AcDbBlockArrayAction", rows ? 140 : 141, rows ? Factor(spacing) : Real(spacing));
        }

        private static string Factor(double value) =>
            value == 1.0 ? "1.0"
            : value == 0.0 ? "0.0"
            : Real(value);

        /// <summary>
        /// Gives a parameter's property a value set, as the Value Set rows of its Properties do: the set written as AutoCAD writes
        /// one (flags 7 for increments, 8 for a list, the flags of none for null), in its place among the groups.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the parameter has no such property, or the set holds neither of the parameter's own values.</exception>
        internal static void SetValueSet(DxfBlockElement element, int which, DxfBlockParameterValueSet set)
        {
            int flags,
                first,
                count;
            string none,
                least,
                most;
            bool angle;

            switch (element.Kind)
            {
                case DxfBlockElementKind.Linear when which == 0:
                    (flags, first, count, none, least, most, angle) = (96, 141, 175, "1", "0", "0", false);
                    break;
                case DxfBlockElementKind.Polar when which == 0:
                    (flags, first, count, none, least, most, angle) = (96, 141, 175, "1", "0.0", "0.0", false);
                    break;
                case DxfBlockElementKind.Polar when which == 1:
                    (flags, first, count, none, least, most, angle) = (
                        97,
                        145,
                        176,
                        "3",
                        "0.0",
                        Real(2.0 * Math.PI),
                        true
                    );
                    break;
                case DxfBlockElementKind.XY when which == 0:
                    (flags, first, count, none, least, most, angle) = (96, 142, 175, "1", "0.0", "0.0", false);
                    break;
                case DxfBlockElementKind.XY when which == 1:
                    (flags, first, count, none, least, most, angle) = (97, 146, 176, "1", "0.0", "0.0", false);
                    break;
                case DxfBlockElementKind.Rotation when which == 0:
                    (flags, first, count, none, least, most, angle) = (
                        96,
                        141,
                        175,
                        "3",
                        "0.0",
                        Real(2.0 * Math.PI),
                        true
                    );
                    break;
                default:
                    throw new ArgumentException("The parameter has no value set there.", nameof(which));
            }

            DxfItem item = element.Item;
            List<DxfTypedValue> tail = item.File.TailOf(item, 0);
            int at = tail.FindIndex(g => g.TypeCode == flags);

            if (at < 0 || at + 4 >= tail.Count || tail[at + 4].TypeCode != count)
            {
                throw new InvalidOperationException("The parameter's value set is not in the form AutoCAD writes.");
            }

            int listed = int.TryParse(
                tail[at + 4].Value?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int n
            )
                ? n
                : 0;
            int end = Math.Min(tail.Count, at + 5 + listed);
            var written = new List<KeyValuePair<int, string>>();

            for (int i = 0; i < tail.Count; i++)
            {
                if (i == at)
                {
                    if (set == null)
                    {
                        written.Add(Pair(flags, none));
                        written.Add(Pair(first, least));
                        written.Add(Pair(first + 1, most));
                        written.Add(Pair(first + 2, least));
                        written.Add(Pair(count, "0"));
                    }
                    else
                    {
                        written.Add(Pair(flags, set.IsList ? "8" : "7"));
                        written.Add(Pair(first, Real(set.Minimum)));
                        written.Add(Pair(first + 1, Real(set.Maximum)));
                        written.Add(Pair(first + 2, Real(set.Increment)));
                        written.Add(Pair(count, set.Values.Count.ToString(CultureInfo.InvariantCulture)));

                        foreach (double value in set.Values)
                        {
                            written.Add(Pair(first + 3, Real(value)));
                        }
                    }

                    i = end - 1;
                    continue;
                }

                written.Add(Pair(tail[i].TypeCode, tail[i].Value));
            }

            item.File.ReplaceTail(item, 0, written);
        }

        private static KeyValuePair<int, string> Pair(int code, string value) =>
            new KeyValuePair<int, string>(code, value);
    }
}
