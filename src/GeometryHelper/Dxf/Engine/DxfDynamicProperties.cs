using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The dynamic properties of an INSERT, read as AutoCAD saved them: each parameter of the dynamic block's evaluation graph
    /// (the ACAD_EVALUATION_GRAPH under ACAD_ENHANCEDBLOCK of its BLOCK_RECORD's extension dictionary), in the order of the
    /// graph's nodes, its value the one the INSERT's representation data keeps of the parameter's node (the XRECORD named by
    /// the node's id under AcDbBlockRepresentation, AppDataCache and ACAD_ENHANCEDBLOCKDATA of the INSERT's extension
    /// dictionary), else the block's own. Nothing is worked out anew: a length is that of the points saved.
    /// </summary>
    internal static partial class DxfDynamicProperties
    {
        private const double Turn = 2.0 * Math.PI;

        private static readonly object[] None = new object[0];

        private static readonly object[] FlipStates = { (short)0, (short)1 };

        /// <summary>
        /// The properties of an INSERT of a dynamic block, as <see cref="DxfBlockReference.DynamicBlockReferencePropertyCollection"/>
        /// gives them; none for a block of no graph. A length is scaled by the INSERT's largest scale factor.
        /// </summary>
        internal static IReadOnlyList<DxfDynamicBlockReferenceProperty> Of(
            DxfBlockReference insert,
            DxfBlockTableRecord dynamic
        )
        {
            var properties = new List<DxfDynamicBlockReferenceProperty>();
            DxfDBObject graph = Entry(dynamic?.ExtensionDictionary, "ACAD_ENHANCEDBLOCK");

            if (graph?.Item == null || graph.Item.Type != "ACAD_EVALUATION_GRAPH")
            {
                return properties.AsReadOnly();
            }

            // A length is scaled by the largest factor, a mirror as its size: AutoCAD 2021 read 7.5 along x as 22.5 at the
            // scale (2, 3, 2) and as 15 at (2, 2, 1) and at (-2, 2, 2).
            GeoVector3 factors = insert.ScaleFactors;
            double scale = Math.Max(Math.Abs(factors.X), Math.Max(Math.Abs(factors.Y), Math.Abs(factors.Z)));

            DxfFile file = graph.Item.File;
            var nodes = new List<DxfItem>();

            foreach (DxfTypedValue group in graph.Item.Groups)
            {
                if (group.TypeCode == 360 && file.ObjectItem(group.Value) is DxfItem node)
                {
                    nodes.Add(node);
                }
            }

            Dictionary<string, DxfXrecord> saved = Saved(insert);
            HashSet<string> shown = ShownInState(nodes, saved);

            // The properties each parameter gave, by its node's id, for a block properties table to read; and each table, by
            // the place of its property.
            var given = new Dictionary<string, List<DxfDynamicBlockReferenceProperty>>(StringComparer.Ordinal);
            var tables = new List<KeyValuePair<int, DxfItem>>();

            foreach (DxfItem node in nodes)
            {
                // Actions and grips give no property: their groups are not read.
                if (
                    node.Type != "BLOCKPROPERTIESTABLE"
                    && (
                        !node.Type.StartsWith("BLOCK", StringComparison.Ordinal)
                        || !node.Type.EndsWith("PARAMETER", StringComparison.Ordinal)
                    )
                )
                {
                    continue;
                }

                IReadOnlyList<DxfTypedValue> groups = node.Groups;
                string id = Text(Section(groups, "AcDbEvalExpr"), 90)?.Trim();
                IReadOnlyList<DxfTypedValue> cache =
                    id != null && saved.TryGetValue(id, out DxfXrecord record) ? record.Data : null;
                int before = properties.Count;

                switch (node.Type)
                {
                    case "BLOCKPROPERTIESTABLE":
                        tables.Add(new KeyValuePair<int, DxfItem>(properties.Count, node));
                        properties.Add(null);
                        break;
                    case "BLOCKUSERPARAMETER":
                        if (User(groups, cache, file, properties))
                        {
                            Bind(properties, before, insert, id);
                        }

                        break;
                    case "BLOCKHORIZONTALCONSTRAINTPARAMETER":
                    case "BLOCKVERTICALCONSTRAINTPARAMETER":
                    case "BLOCKRADIALCONSTRAINTPARAMETER":
                        Constraint(node.Type, groups, cache, scale, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKALIGNEDCONSTRAINTPARAMETER":
                        Constraint(node.Type, groups, cache, scale, properties);
                        break;
                    case "BLOCKLINEARPARAMETER":
                        Linear(groups, cache, scale, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKPOLARPARAMETER":
                        Polar(groups, cache, scale, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKXYPARAMETER":
                        Xy(groups, cache, scale, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKROTATIONPARAMETER":
                        Rotation(groups, cache, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKPOINTPARAMETER":
                        Point(groups, cache, scale, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKFLIPPARAMETER":
                        Flip(groups, cache, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKVISIBILITYPARAMETER":
                        Visibility(groups, cache, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKLOOKUPPARAMETER":
                        Lookup(groups, id, cache, nodes, saved, properties);
                        Bind(properties, before, insert, id);
                        break;
                }

                if (id != null && properties.Count > before && properties[before] != null && !given.ContainsKey(id))
                {
                    given.Add(id, properties.GetRange(before, properties.Count - before));
                }

                Shown(properties, before, node, shown);

                if (properties.Count > before && properties[before] != null)
                {
                    int origin = properties.Count;
                    Origin(node.Type, groups, cache, properties, properties[before].VisibleInCurrentVisibilityState);

                    // The Origin of a linear, an XY or a polar parameter is set (it moves the base point and the end point
                    // together); a rotation's and a constraint parameter's is read only and stays unbound.
                    if (id != null && properties.Count > origin && !properties[origin].ReadOnly)
                    {
                        properties[origin].Bind(insert, id, origin - before);
                    }
                }
            }

            // Each table reads the values the other properties show, so it is read last, in its place; one that cannot be read
            // whole gives no property.
            for (int t = tables.Count - 1; t >= 0; t--)
            {
                int at = tables[t].Key;
                DxfPropertiesTable table = DxfPropertiesTable.Of(tables[t].Value.Groups);

                if (table == null)
                {
                    properties.RemoveAt(at);
                    continue;
                }

                string id = Text(Section(tables[t].Value.Groups, "AcDbEvalExpr"), 90)?.Trim();
                short row = (short)table.Row(column => Now(column, given, nodes, scale));
                properties[at] = new DxfDynamicBlockReferenceProperty(
                    table.Name,
                    table.Description,
                    row,
                    3,
                    DxfDynamicBlockReferencePropertyUnitsType.NoUnits,
                    false,
                    None
                );

                if (id != null)
                {
                    properties[at].Bind(insert, id, 0);
                }

                // A constraint parameter's distance a table to match holds reads as read only, as AutoCAD 2021 read Width and
                // Height of its Fluorescent sample (plugin D27b).
                foreach (
                    DxfPropertiesTable.Column column in table.MustMatch
                        ? table.Columns
                        : new List<DxfPropertiesTable.Column>()
                )
                {
                    string type =
                        column.Node == null
                            ? null
                            : nodes.Find(n => Text(Section(n.Groups, "AcDbEvalExpr"), 90)?.Trim() == column.Node)?.Type;

                    if (
                        type != null
                        && type.EndsWith("CONSTRAINTPARAMETER", StringComparison.Ordinal)
                        && column.Connection == "UpdatedDistance"
                        && given.TryGetValue(column.Node, out List<DxfDynamicBlockReferenceProperty> held)
                        && held.Count > 0
                    )
                    {
                        held[0].HeldByTable();
                    }
                }
            }

            return properties.AsReadOnly();
        }

        /// <summary>
        /// The block properties table of a block's evaluation graph, as <see cref="DxfBlockTableRecord.BlockPropertiesTable"/>
        /// gives it: the first node of the kind; each column named after the property it sets (the label of a linear, a
        /// rotation or a constraint parameter's distance, 305; of an XY parameter's x, 306, and y, 305; of a polar parameter's
        /// distance, 305, and angle, 307; of a point parameter's x and y, 303 with " X" and " Y"; the name of a user parameter's
        /// variable; the label of the visibility parameter, 301) or the tag of its attribute definition; null where the block
        /// has none or it cannot be read whole.
        /// </summary>
        internal static DxfBlockPropertiesTable Table(DxfBlockTableRecord block)
        {
            DxfDBObject graph = Entry(block.ExtensionDictionary, "ACAD_ENHANCEDBLOCK");

            if (graph?.Item == null || graph.Item.Type != "ACAD_EVALUATION_GRAPH")
            {
                return null;
            }

            DxfFile file = graph.Item.File;
            var nodes = new List<DxfItem>();

            foreach (DxfTypedValue group in graph.Item.Groups)
            {
                if (group.TypeCode == 360 && file.ObjectItem(group.Value) is DxfItem node)
                {
                    nodes.Add(node);
                }
            }

            DxfItem found = nodes.Find(n => n.Type == "BLOCKPROPERTIESTABLE");
            DxfPropertiesTable table = found == null ? null : DxfPropertiesTable.Of(found.Groups);

            if (table == null)
            {
                return null;
            }

            var names = new List<string>();

            foreach (DxfPropertiesTable.Column column in table.Columns)
            {
                if (column.IsAttribute)
                {
                    DxfEntity entity = null;

                    foreach (DxfEntity e in block)
                    {
                        if (string.Equals(e.Handle, column.Handle, StringComparison.OrdinalIgnoreCase))
                        {
                            entity = e;
                            break;
                        }
                    }

                    names.Add((entity as DxfAttributeDefinition)?.Tag ?? string.Empty);
                    continue;
                }

                DxfItem node = nodes.Find(n => Text(Section(n.Groups, "AcDbEvalExpr"), 90)?.Trim() == column.Node);
                names.Add(node == null ? string.Empty : ColumnName(node, column.Connection, file));
            }

            return new DxfBlockPropertiesTable(
                table.Name,
                table.MustMatch,
                names,
                table.Rows.ConvertAll(DxfBlockPropertiesTableRow.Read)
            );
        }

        /// <summary>The name of the property a column of a block properties table sets, of its parameter's node and connection.</summary>
        private static string ColumnName(DxfItem node, string connection, DxfFile file)
        {
            IReadOnlyList<DxfTypedValue> groups = node.Groups;

            switch (node.Type)
            {
                case "BLOCKXYPARAMETER":
                    return Name(Section(groups, "AcDbBlockXYParameter"), connection == "UpdatedDistanceY" ? 305 : 306);
                case "BLOCKPOLARPARAMETER":
                    return Name(Section(groups, "AcDbBlockPolarParameter"), connection == "UpdatedAngle" ? 307 : 305);
                case "BLOCKPOINTPARAMETER":
                    return Name(Section(groups, "AcDbBlockPointParameter"), 303)
                        + (connection == "UpdatedY" ? " Y" : " X");
                case "BLOCKLINEARPARAMETER":
                    return Name(Section(groups, "AcDbBlockLinearParameter"), 305);
                case "BLOCKROTATIONPARAMETER":
                    return Name(Section(groups, "AcDbBlockRotationParameter"), 305);
                case "BLOCKVISIBILITYPARAMETER":
                    return Name(Section(groups, "AcDbBlockVisibilityParameter"), 301);
                case "BLOCKRADIALCONSTRAINTPARAMETER":
                    return Name(Section(groups, "AcDbBlockRadialConstraintParameter"), 305);
                case "BLOCKUSERPARAMETER":
                    DxfItem variable = file.ObjectItem(Text(Section(groups, "AcDbBlockUserParameter"), 330));
                    List<DxfTypedValue> held =
                        variable == null ? new List<DxfTypedValue>() : Section(variable.Groups, "AcDbAssocVariable");
                    return held.Count > 1 && held[1].TypeCode == 1
                        ? DxfReader.NameOf(held[1].Value ?? string.Empty)
                        : string.Empty;
                default:
                    return Name(Section(groups, "AcDbBlockLinearConstraintParameter"), 305);
            }
        }

        /// <summary>
        /// The value a column of a block properties table reads now, in the block's units, of the properties the parameter of
        /// its node gave: the distance of a linear, a polar or a constraint parameter (UpdatedDistance), the distance along x or
        /// along y of an XY parameter (UpdatedDistanceX, UpdatedDistanceY), the angle of a polar or a rotation parameter
        /// (UpdatedAngle), the x or the y of a point parameter (UpdatedX, UpdatedY), the text of a user parameter
        /// (UserVariable), the state of the visibility parameter (VisibilityState); null where the parameter gave none.
        /// </summary>
        private static object Now(
            DxfPropertiesTable.Column column,
            Dictionary<string, List<DxfDynamicBlockReferenceProperty>> given,
            List<DxfItem> nodes,
            double scale
        )
        {
            if (!given.TryGetValue(column.Node, out List<DxfDynamicBlockReferenceProperty> read))
            {
                return null;
            }

            string type = nodes.Find(n => Text(Section(n.Groups, "AcDbEvalExpr"), 90)?.Trim() == column.Node)?.Type;
            int part;

            switch (column.Connection)
            {
                case "UpdatedDistanceY":
                case "UpdatedY":
                    part = 1;
                    break;
                case "UpdatedAngle":
                    part = type == "BLOCKPOLARPARAMETER" ? 1 : 0;
                    break;
                case "UpdatedDistance":
                case "UpdatedDistanceX":
                case "UpdatedX":
                case "UserVariable":
                case "VisibilityState":
                    part = 0;
                    break;
                default:
                    return null;
            }

            if (part >= read.Count)
            {
                return null;
            }

            DxfDynamicBlockReferenceProperty property = read[part];

            if (property.Value is double number)
            {
                return property.UnitsType == DxfDynamicBlockReferencePropertyUnitsType.Distance
                    ? number / scale
                    : number;
            }

            return property.Value as string;
        }

        /// <summary>
        /// A user parameter, as a block properties table makes one, of text (170 of 5) or of a real (170 of 3): its name and its
        /// description, the first and the fourth text of the ACDBASSOCVARIABLE it names (330) after its count, and its value,
        /// the one the INSERT saved (1 or 40 of its data) else the variable's (the group after those four texts), a
        /// <see cref="string"/> or a <see cref="double"/> of no units, nothing allowed, as AutoCAD 2021 read Spaces of its
        /// Parking sample and Size of its Concrete Pipe (plugin D26). A user parameter of another kind gives no property;
        /// false then.
        /// </summary>
        private static bool User(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            DxfFile file,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockUserParameter");
            string type = Text(own, 170)?.Trim();
            int code =
                type == "5" ? 1
                : type == "3" ? 40
                : 0;

            if (
                code == 0
                || !(file.ObjectItem(Text(own, 330)) is DxfItem variable)
                || variable.Type != "ACDBASSOCVARIABLE"
            )
            {
                return false;
            }

            List<DxfTypedValue> held = Section(variable.Groups, "AcDbAssocVariable");

            if (
                held.Count < 6
                || held[0].TypeCode != 90
                || held.GetRange(1, 4).Exists(g => g.TypeCode != 1)
                || held[5].TypeCode != code
            )
            {
                return false;
            }

            string name = DxfReader.NameOf(held[1].Value ?? string.Empty);
            string description = DxfReader.Decode(held[4].Value ?? string.Empty);

            if (code == 1)
            {
                string text = Saved(cache) ?? DxfReader.Decode(held[5].Value ?? string.Empty);
                properties.Add(
                    new DxfDynamicBlockReferenceProperty(
                        name,
                        description,
                        text,
                        5,
                        DxfDynamicBlockReferencePropertyUnitsType.NoUnits,
                        false,
                        None
                    )
                );
                return true;
            }

            string kept = held[5].Value;

            foreach (DxfTypedValue group in cache ?? (IReadOnlyList<DxfTypedValue>)new DxfTypedValue[0])
            {
                if (group.TypeCode == 40)
                {
                    kept = group.Value;
                    break;
                }
            }

            if (!Number(kept, out double real))
            {
                return false;
            }

            properties.Add(
                new DxfDynamicBlockReferenceProperty(
                    name,
                    description,
                    real,
                    1,
                    DxfDynamicBlockReferencePropertyUnitsType.NoUnits,
                    false,
                    None
                )
            );
            return true;
        }

        /// <summary>
        /// A constraint parameter a block properties table reads: its distance (305 its label, 306 its description), the length
        /// between its two points along x of a horizontal one, along y of a vertical one, and straight of an aligned or a radial
        /// one, as AutoCAD 2021 read Width and Height of its Fluorescent sample (plugin D26); of no allowed value, as AutoCAD
        /// read them. A horizontal, a vertical or a radial one is set by solving the block's constraints
        /// (<see cref="DxfBlockConstraints"/>, plugin D27); an aligned one is not set, as no sample showed its working.
        /// </summary>
        private static void Constraint(
            string type,
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            double scale,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(
                groups,
                type == "BLOCKRADIALCONSTRAINTPARAMETER"
                    ? "AcDbBlockRadialConstraintParameter"
                    : "AcDbBlockLinearConstraintParameter"
            );

            if (!TwoPoints(groups, cache, out double[] from, out double[] to))
            {
                return;
            }

            double distance =
                type == "BLOCKHORIZONTALCONSTRAINTPARAMETER" ? Math.Abs(to[0] - from[0])
                : type == "BLOCKVERTICALCONSTRAINTPARAMETER" ? Math.Abs(to[1] - from[1])
                : Distance(from, to);
            properties.Add(Length(own, 305, 306, distance * scale, List(own, 175, scale)));
        }

        /// <summary>
        /// The property Origin of a parameter that has a base point, after the properties the parameter gave: the base point the
        /// INSERT saved of it, else the block's own, in the block's coordinates (a <see cref="GeoPoint3"/>, not scaled), of the
        /// kind 12, no units, not shown and visible where the parameter is, as AutoCAD 2021 read it of a linear, an XY, a polar,
        /// a rotation and a constraint parameter, and of no other (plugin GhDy3a). Read only for a rotation and a constraint
        /// parameter, as AutoCAD read it; bound to the INSERT where it is set, and not otherwise.
        /// </summary>
        private static void Origin(
            string type,
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            List<DxfDynamicBlockReferenceProperty> properties,
            bool visible
        )
        {
            bool fixedBase;

            switch (type)
            {
                case "BLOCKLINEARPARAMETER":
                case "BLOCKPOLARPARAMETER":
                case "BLOCKXYPARAMETER":
                    fixedBase = false;
                    break;
                case "BLOCKROTATIONPARAMETER":
                case "BLOCKHORIZONTALCONSTRAINTPARAMETER":
                case "BLOCKVERTICALCONSTRAINTPARAMETER":
                case "BLOCKRADIALCONSTRAINTPARAMETER":
                case "BLOCKALIGNEDCONSTRAINTPARAMETER":
                    fixedBase = true;
                    break;
                default:
                    return;
            }

            if (!TwoPoints(groups, cache, out double[] from, out double[] _))
            {
                return;
            }

            properties.Add(
                new DxfDynamicBlockReferenceProperty(
                    "Origin",
                    string.Empty,
                    new GeoPoint3(from[0], from[1], from[2]),
                    12,
                    DxfDynamicBlockReferencePropertyUnitsType.NoUnits,
                    fixedBase,
                    None
                )
                {
                    Show = false,
                    VisibleInCurrentVisibilityState = visible,
                }
            );
        }

        /// <summary>Ties each property a parameter gave, where it gave one, to the INSERT, the parameter's node and its place among them, for its value to be set.</summary>
        private static void Bind(
            List<DxfDynamicBlockReferenceProperty> properties,
            int at,
            DxfBlockReference insert,
            string id
        )
        {
            for (int i = at; id != null && i < properties.Count; i++)
            {
                properties[i].Bind(insert, id, i - at);
            }
        }

        /// <summary>A linear parameter: its distance, the length from its base point to its end point (305 its label, 306 its description).</summary>
        private static void Linear(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            double scale,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockLinearParameter");

            if (!TwoPoints(groups, cache, out double[] from, out double[] to))
            {
                return;
            }

            properties.Add(
                Length(own, 305, 306, Distance(from, to) * scale, List(own, 175, scale, Own(groups, Distance), false))
            );
        }

        /// <summary>
        /// A polar parameter: its distance, from its base point to its end point (305 and 306), and the angle of that line
        /// from the block's x axis (307 and 308); the list of the distance's value set counted by 175, the angle's by 176.
        /// </summary>
        private static void Polar(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            double scale,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockPolarParameter");

            if (!TwoPoints(groups, cache, out double[] from, out double[] to))
            {
                return;
            }

            properties.Add(
                Length(own, 305, 306, Distance(from, to) * scale, List(own, 175, scale, Own(groups, Distance), false))
            );
            properties.Add(
                Angle(
                    own,
                    307,
                    308,
                    Normal(Math.Atan2(to[1] - from[1], to[0] - from[0])),
                    List(own, 176, 1.0, Own(groups, (f, t) => Normal(Math.Atan2(t[1] - f[1], t[0] - f[0]))), true)
                )
            );
        }

        /// <summary>
        /// An XY parameter: its distance along x (306 its label, 308 its description, its list counted by 175) and along y
        /// (305, 307, 176), each the length between its two points along that axis, as AutoCAD writes the y first.
        /// </summary>
        private static void Xy(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            double scale,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockXYParameter");

            if (!TwoPoints(groups, cache, out double[] from, out double[] to))
            {
                return;
            }

            properties.Add(
                Length(
                    own,
                    306,
                    308,
                    Math.Abs(to[0] - from[0]) * scale,
                    List(own, 175, scale, Own(groups, (f, t) => Math.Abs(t[0] - f[0])), false)
                )
            );
            properties.Add(
                Length(
                    own,
                    305,
                    307,
                    Math.Abs(to[1] - from[1]) * scale,
                    List(own, 176, scale, Own(groups, (f, t) => Math.Abs(t[1] - f[1])), false)
                )
            );
        }

        /// <summary>
        /// A rotation parameter: the angle of the line from its base point to its end point, from that to its base angle point
        /// (1011 of its own, the fourth point saved), counter-clockwise, from nought up to a whole turn.
        /// </summary>
        private static void Rotation(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockRotationParameter");

            if (!TwoPoints(groups, cache, out double[] from, out double[] to))
            {
                return;
            }

            List<double[]> points = Points(cache);
            double[] basis = points.Count >= 4 ? points[3] : PointOf(own, 1011);

            if (basis == null)
            {
                return;
            }

            double angle =
                Math.Atan2(to[1] - from[1], to[0] - from[0]) - Math.Atan2(basis[1] - from[1], basis[0] - from[0]);
            double[] ownBasis = PointOf(own, 1011) ?? basis;
            double ownAngle = Own(groups, (f, t) =>
                Normal(Math.Atan2(t[1] - f[1], t[0] - f[0]) - Math.Atan2(ownBasis[1] - f[1], ownBasis[0] - f[0]))
            );
            properties.Add(Angle(own, 305, 306, Normal(angle), List(own, 175, 1.0, ownAngle, true)));
        }

        /// <summary>A point parameter: its position's x and y, the label (303) with " X" and " Y" after it, both of its description (304).</summary>
        private static void Point(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            double scale,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockPointParameter");
            List<double[]> points = Points(cache);
            double[] at = points.Count >= 1 ? points[0] : PointOf(Section(groups, "AcDbBlock1PtParameter"), 1010);

            if (at == null)
            {
                return;
            }

            string name = Name(own, 303);
            string description = Words(own, 304);
            properties.Add(
                new DxfDynamicBlockReferenceProperty(
                    name + " X",
                    description,
                    at[0] * scale,
                    1,
                    DxfDynamicBlockReferencePropertyUnitsType.Distance,
                    false,
                    None
                )
            );
            properties.Add(
                new DxfDynamicBlockReferenceProperty(
                    name + " Y",
                    description,
                    at[1] * scale,
                    1,
                    DxfDynamicBlockReferencePropertyUnitsType.Distance,
                    false,
                    None
                )
            );
        }

        /// <summary>A flip parameter: 1 where the INSERT saved it flipped (the last 70 of its data after the two of its head), else 0.</summary>
        private static void Flip(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockFlipParameter");
            short state = 0;
            int seen = 0;

            foreach (DxfTypedValue group in cache ?? (IReadOnlyList<DxfTypedValue>)new DxfTypedValue[0])
            {
                if (
                    group.TypeCode == 70
                    && ++seen > 2
                    && short.TryParse(
                        group.Value?.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out short read
                    )
                )
                {
                    state = read;
                }
            }

            properties.Add(
                new DxfDynamicBlockReferenceProperty(
                    Name(own, 305),
                    Words(own, 306),
                    state,
                    3,
                    DxfDynamicBlockReferencePropertyUnitsType.NoUnits,
                    false,
                    FlipStates
                )
                {
                    StateNames = FlipNames(groups),
                }
            );
        }

        /// <summary>
        /// A visibility parameter: the state the INSERT saved (1 of its data), else the first of the parameter's states (each
        /// a 303 of its own), as AutoCAD inserts a block in it; every state allowed.
        /// </summary>
        private static void Visibility(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockVisibilityParameter");
            var states = new List<object>();

            foreach (DxfTypedValue group in own)
            {
                if (group.TypeCode == 303)
                {
                    states.Add(DxfReader.Decode(group.Value ?? string.Empty));
                }
            }

            string state = Saved(cache) ?? (states.Count > 0 ? (string)states[0] : string.Empty);
            properties.Add(
                new DxfDynamicBlockReferenceProperty(
                    Name(own, 301),
                    Words(own, 302),
                    state,
                    5,
                    DxfDynamicBlockReferencePropertyUnitsType.NoUnits,
                    false,
                    states.ToArray()
                )
            );
        }

        /// <summary>
        /// A lookup parameter: the value the INSERT saved (1 of its data); its column of the lookup table of the action its 94
        /// names, the column whose node (94) is the parameter's, gives the values allowed, each once, in the order of the rows.
        /// Where the INSERT saved none, a table with columns to look up from gives the cell of the first row whose inputs are
        /// the values the INSERT shows, as <see cref="Matched"/> says, else the column's label of no match (305), which is then
        /// allowed too; a table of none its first value. Read only where the column allows no reverse lookup (281 of 0), and
        /// then of its label of no match allowed last whatever it reads, as AutoCAD 2021 gave Custom last of the values of each
        /// read-only lookup of its samples, Bore Tolerance, Ball Count and Static Load of Roller Bearing among them (plugin
        /// D26b).
        /// </summary>
        private static void Lookup(
            IReadOnlyList<DxfTypedValue> groups,
            string id,
            IReadOnlyList<DxfTypedValue> cache,
            List<DxfItem> nodes,
            Dictionary<string, DxfXrecord> saved,
            List<DxfDynamicBlockReferenceProperty> properties
        )
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockLookUpParameter");
            string action = Text(own, 94)?.Trim();
            List<DxfTypedValue> table = null;

            foreach (DxfItem node in nodes)
            {
                if (
                    node.Type == "BLOCKLOOKUPACTION"
                    && Text(Section(node.Groups, "AcDbEvalExpr"), 90)?.Trim() == action
                )
                {
                    table = Section(node.Groups, "AcDbBlockLookupAction");
                    break;
                }
            }

            if (
                table == null
                || id == null
                || !int.TryParse(
                    Text(table, 93)?.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int width
                )
                || width <= 0
            )
            {
                return;
            }

            // The cells, row by row, then a description of each column: its node (94), whether it is looked up (282 of 1),
            // its label of no match (305) and whether it allows a reverse lookup (281).
            var cells = new List<string>();
            var columns = new List<Dictionary<int, string>>();

            foreach (DxfTypedValue group in table)
            {
                if (group.TypeCode == 302 && columns.Count == 0)
                {
                    cells.Add(DxfReader.Decode(group.Value ?? string.Empty));
                }
                else if (group.TypeCode == 303)
                {
                    columns.Add(new Dictionary<int, string>());
                }
                else if (columns.Count > 0 && !columns[columns.Count - 1].ContainsKey(group.TypeCode))
                {
                    columns[columns.Count - 1].Add(group.TypeCode, group.Value ?? string.Empty);
                }
            }

            int column = columns.FindIndex(c => c.TryGetValue(94, out string node) && node.Trim() == id);

            if (column < 0 || column >= width)
            {
                return;
            }

            var allowed = new List<object>();

            for (int at = column; at < cells.Count; at += width)
            {
                if (!allowed.Contains(cells[at]))
                {
                    allowed.Add(cells[at]);
                }
            }

            bool inputs = columns.Exists(c => !(c.TryGetValue(282, out string looked) && looked.Trim() == "1"));
            string unmatched = DxfReader.Decode(
                columns[column].TryGetValue(305, out string label) ? label : string.Empty
            );
            string value = Saved(cache)
                ?? (
                    inputs ? Matched(cells, columns, width, column, nodes, saved) ?? unmatched
                    : allowed.Count > 0 ? (string)allowed[0]
                    : string.Empty
                );

            bool readOnly = !(columns[column].TryGetValue(281, out string reverse) && reverse.Trim() == "1");

            if (inputs && (value == unmatched || readOnly) && !allowed.Contains(unmatched))
            {
                allowed.Add(unmatched);
            }

            properties.Add(
                new DxfDynamicBlockReferenceProperty(
                    Name(own, 303),
                    Words(own, 304),
                    value,
                    5,
                    DxfDynamicBlockReferencePropertyUnitsType.NoUnits,
                    readOnly,
                    allowed.ToArray()
                )
            );
        }

        /// <summary>
        /// The cell of a lookup's column, by its index, of the first row whose input cells are the values the INSERT shows its
        /// inputs at, within the point tolerance of <see cref="Tolerance.Default"/>, in the block's units, or an angle, taken
        /// into a turn, within its vector tolerance: the distance of a linear or a polar parameter (UpdatedDistance), the x or the
        /// y of a point parameter (UpdatedX, UpdatedY), the distance along x or along y of an XY parameter (UpdatedDistanceX,
        /// UpdatedDistanceY), the angle of a polar or a rotation parameter (UpdatedAngle), each as the INSERT saved it, else the
        /// block's own, as AutoCAD 2021 read Small on an INSERT of a lookup block itself whose Width 100 and Height 50 were the
        /// row Small's (plugin v21b); the state a visibility parameter shows (VisibilityState), the text alike, as AutoCAD read
        /// Part Number SSI-4 of its Shoulder Screw sample by its length and its state 4-40 (plugin D26b); null where no row
        /// holds them, or an input is of another kind.
        /// </summary>
        private static string Matched(
            List<string> cells,
            List<Dictionary<int, string>> columns,
            int width,
            int column,
            List<DxfItem> nodes,
            Dictionary<string, DxfXrecord> saved
        )
        {
            var inputs = new List<KeyValuePair<int, double>>();
            var angles = new HashSet<int>();
            var states = new List<KeyValuePair<int, string>>();

            for (int c = 0; c < width; c++)
            {
                if (c >= columns.Count)
                {
                    return null;
                }

                Dictionary<int, string> described = columns[c];

                if (described.TryGetValue(282, out string looked) && looked.Trim() == "1")
                {
                    continue;
                }

                string node = described.TryGetValue(94, out string read) ? read.Trim() : null;
                string connection = described.TryGetValue(304, out string named) ? named.Trim() : null;
                DxfItem parameter =
                    node == null ? null : nodes.Find(n => Text(Section(n.Groups, "AcDbEvalExpr"), 90)?.Trim() == node);
                IReadOnlyList<DxfTypedValue> cache =
                    node != null && saved.TryGetValue(node, out DxfXrecord record) ? record.Data : null;

                if (
                    (parameter?.Type == "BLOCKLINEARPARAMETER" || parameter?.Type == "BLOCKPOLARPARAMETER")
                    && connection == "UpdatedDistance"
                    && TwoPoints(parameter.Groups, cache, out double[] from, out double[] to)
                )
                {
                    inputs.Add(new KeyValuePair<int, double>(c, Distance(from, to)));
                }
                else if (
                    parameter?.Type == "BLOCKXYPARAMETER"
                    && (connection == "UpdatedDistanceX" || connection == "UpdatedDistanceY")
                    && TwoPoints(parameter.Groups, cache, out from, out to)
                )
                {
                    inputs.Add(
                        new KeyValuePair<int, double>(
                            c,
                            connection == "UpdatedDistanceX" ? Math.Abs(to[0] - from[0]) : Math.Abs(to[1] - from[1])
                        )
                    );
                }
                else if (
                    parameter?.Type == "BLOCKPOLARPARAMETER"
                    && connection == "UpdatedAngle"
                    && TwoPoints(parameter.Groups, cache, out from, out to)
                )
                {
                    inputs.Add(new KeyValuePair<int, double>(c, Normal(Math.Atan2(to[1] - from[1], to[0] - from[0]))));
                    angles.Add(c);
                }
                else if (parameter?.Type == "BLOCKROTATIONPARAMETER" && connection == "UpdatedAngle")
                {
                    var turned = new List<DxfDynamicBlockReferenceProperty>();
                    Rotation(parameter.Groups, cache, turned);

                    if (turned.Count == 0)
                    {
                        return null;
                    }

                    inputs.Add(new KeyValuePair<int, double>(c, (double)turned[0].Value));
                    angles.Add(c);
                }
                else if (parameter?.Type == "BLOCKVISIBILITYPARAMETER" && connection == "VisibilityState")
                {
                    var shown = new List<DxfDynamicBlockReferenceProperty>();
                    Visibility(parameter.Groups, cache, shown);
                    states.Add(new KeyValuePair<int, string>(c, (string)shown[0].Value));
                }
                else if (
                    parameter?.Type == "BLOCKPOINTPARAMETER"
                    && (connection == "UpdatedX" || connection == "UpdatedY")
                )
                {
                    List<double[]> points = Points(cache);
                    double[] at =
                        points.Count >= 1
                            ? points[0]
                            : PointOf(Section(parameter.Groups, "AcDbBlock1PtParameter"), 1010);

                    if (at == null)
                    {
                        return null;
                    }

                    inputs.Add(new KeyValuePair<int, double>(c, connection == "UpdatedX" ? at[0] : at[1]));
                }
                else
                {
                    return null;
                }
            }

            for (int row = 0; row + width <= cells.Count; row += width)
            {
                if (
                    inputs.TrueForAll(i =>
                        Number(cells[row + i.Key], out double cell)
                        && Math.Abs(cell - i.Value)
                            <= (angles.Contains(i.Key) ? Tolerance.Default.EqualVector : Tolerance.Default.EqualPoint)
                    ) && states.TrueForAll(s => string.Equals(cells[row + s.Key], s.Value, StringComparison.Ordinal))
                )
                {
                    return cells[row + column];
                }
            }

            return null;
        }

        /// <summary>A length property of a label and a description of the codes given.</summary>
        private static DxfDynamicBlockReferenceProperty Length(
            List<DxfTypedValue> own,
            int name,
            int description,
            double value,
            object[] allowed
        ) =>
            new DxfDynamicBlockReferenceProperty(
                Name(own, name),
                Words(own, description),
                value,
                1,
                DxfDynamicBlockReferencePropertyUnitsType.Distance,
                false,
                allowed
            );

        /// <summary>An angle property of a label and a description of the codes given.</summary>
        private static DxfDynamicBlockReferenceProperty Angle(
            List<DxfTypedValue> own,
            int name,
            int description,
            double value,
            object[] allowed
        ) =>
            new DxfDynamicBlockReferenceProperty(
                Name(own, name),
                Words(own, description),
                value,
                1,
                DxfDynamicBlockReferencePropertyUnitsType.Angular,
                false,
                allowed
            );

        /// <summary>
        /// The base point and end point of a parameter of two points: the first two points the INSERT saved, else the block's
        /// own (1010 and 1011 of AcDbBlock2PtParameter); false where neither can be read.
        /// </summary>
        private static bool TwoPoints(
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            out double[] from,
            out double[] to
        )
        {
            List<double[]> points = Points(cache);

            if (points.Count >= 2)
            {
                from = points[0];
                to = points[1];
                return true;
            }

            List<DxfTypedValue> two = Section(groups, "AcDbBlock2PtParameter");
            from = PointOf(two, 1010);
            to = PointOf(two, 1011);
            return from != null && to != null;
        }

        /// <summary>A measure of the block's own two points of a parameter (1010 and 1011 of AcDbBlock2PtParameter); not a number where they cannot be read.</summary>
        private static double Own(IReadOnlyList<DxfTypedValue> groups, Func<double[], double[], double> measure) =>
            TwoPoints(groups, null, out double[] from, out double[] to) ? measure(from, to) : double.NaN;

        /// <summary>The points of an INSERT's data of a node (10, 20, 30 each), in order; none where it has none.</summary>
        private static List<double[]> Points(IReadOnlyList<DxfTypedValue> cache)
        {
            var points = new List<double[]>();

            for (int i = 0; cache != null && i + 2 < cache.Count; i++)
            {
                if (
                    cache[i].TypeCode == 10
                    && cache[i + 1].TypeCode == 20
                    && cache[i + 2].TypeCode == 30
                    && Number(cache[i].Value, out double x)
                    && Number(cache[i + 1].Value, out double y)
                    && Number(cache[i + 2].Value, out double z)
                )
                {
                    points.Add(new[] { x, y, z });
                    i += 2;
                }
            }

            return points;
        }

        /// <summary>The text an INSERT saved of a node (its 1), letters read; null where it saved none.</summary>
        private static string Saved(IReadOnlyList<DxfTypedValue> cache)
        {
            foreach (DxfTypedValue group in cache ?? (IReadOnlyList<DxfTypedValue>)new DxfTypedValue[0])
            {
                if (group.TypeCode == 1)
                {
                    return DxfReader.Decode(group.Value ?? string.Empty);
                }
            }

            return null;
        }

        /// <summary>
        /// The data the INSERT's representation keeps of each node of its dynamic block, by the node's id: the XRECORDs of
        /// ACAD_ENHANCEDBLOCKDATA under AppDataCache under AcDbBlockRepresentation of its extension dictionary; none where it
        /// has none, as an INSERT of the dynamic block itself.
        /// </summary>
        private static Dictionary<string, DxfXrecord> Saved(DxfBlockReference insert)
        {
            var saved = new Dictionary<string, DxfXrecord>(StringComparer.Ordinal);
            DxfDBDictionary data =
                Entry(
                    Entry(
                        Entry(insert.ExtensionDictionary, "AcDbBlockRepresentation") as DxfDBDictionary,
                        "AppDataCache"
                    ) as DxfDBDictionary,
                    "ACAD_ENHANCEDBLOCKDATA"
                ) as DxfDBDictionary;

            if (data == null)
            {
                return saved;
            }

            foreach (DxfDBDictionaryEntry entry in data)
            {
                if (entry.Value is DxfXrecord record && !saved.ContainsKey(entry.Key.Trim()))
                {
                    saved.Add(entry.Key.Trim(), record);
                }
            }

            return saved;
        }

        /// <summary>
        /// The base point the INSERT saved of each node it saved two points or more of, by the node's id: the first point of the
        /// record of a linear, an XY, a polar, a rotation or a flip parameter; none where it saved none.
        /// </summary>
        internal static Dictionary<string, double[]> SavedBases(DxfBlockReference insert)
        {
            var bases = new Dictionary<string, double[]>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, DxfXrecord> saved in Saved(insert))
            {
                List<double[]> points = Points(saved.Value.Data);

                if (points.Count >= 2)
                {
                    bases.Add(saved.Key, points[0]);
                }
            }

            return bases;
        }

        /// <summary>The object of a dictionary's entry; null where the dictionary is none or has no entry of the name.</summary>
        internal static DxfDBObject Entry(DxfDBDictionary dictionary, string name) =>
            dictionary != null && dictionary.Contains(name) ? dictionary.GetAt(name) : null;

        /// <summary>
        /// The groups of a subclass of an object's record: those after its marker (100) up to the next marker or the
        /// extended data; none where the record has no such marker.
        /// </summary>
        internal static List<DxfTypedValue> Section(IReadOnlyList<DxfTypedValue> groups, string marker)
        {
            var section = new List<DxfTypedValue>();
            bool inside = false;

            foreach (DxfTypedValue group in groups)
            {
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

                if (inside)
                {
                    section.Add(group);
                }
            }

            return section;
        }

        /// <summary>The value of the first group of a code; null where there is none.</summary>
        internal static string Text(List<DxfTypedValue> groups, int code)
        {
            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>A label: the first group of a code, its escapes read and its ends trimmed, as a name is.</summary>
        private static string Name(List<DxfTypedValue> groups, int code) =>
            DxfReader.NameOf(Text(groups, code) ?? string.Empty);

        /// <summary>A description: the first group of a code, its escapes read, its spaces kept.</summary>
        private static string Words(List<DxfTypedValue> groups, int code) =>
            DxfReader.Decode(Text(groups, code) ?? string.Empty);

        /// <summary>The point of a code and the two after it by ten (1010, 1020, 1030); null where it cannot be read.</summary>
        internal static double[] PointOf(List<DxfTypedValue> groups, int code) =>
            Number(Text(groups, code), out double x)
            && Number(Text(groups, code + 10), out double y)
            && Number(Text(groups, code + 20) ?? "0", out double z)
                ? new[] { x, y, z }
                : null;

        /// <summary>
        /// The list of a value set: the reals after the group of a code that counts them (175 or 176), each scaled, where the
        /// flags of the set (96 before 175, 97 before 176) hold 8, a list; none where they do not, as AutoCAD 2021 gave none
        /// of a list of three under the flags 1, 2 and 4, and the three under 8; none where the count is nought, or the list
        /// cannot be read whole. The block's own value is one of the list where it was written without it, put after the last
        /// value below it (the first where none is), as AutoCAD 2021 gave Door Size of its Door sample, own 750, the values
        /// 600, 700, 750, 800, 900, 1000 of the list written without 750, and the own 100 of the list written 300, 200, 50 last
        /// (plugin D26b); a value the INSERT shows off the list is not added. A distance within the point tolerance of
        /// <see cref="Tolerance.Default"/> of the own one, an angle within its vector tolerance round the circle, is it.
        /// </summary>
        private static object[] List(
            List<DxfTypedValue> groups,
            int countCode,
            double scale,
            double own = double.NaN,
            bool angle = false
        )
        {
            int at = groups.FindIndex(g => g.TypeCode == countCode);
            int flags = groups.FindIndex(g => g.TypeCode == (countCode == 175 ? 96 : 97));

            if (
                flags < 0
                || !int.TryParse(
                    groups[flags].Value?.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int set
                )
                || (set & 8) == 0
            )
            {
                return None;
            }

            if (
                at < 0
                || !int.TryParse(
                    groups[at].Value?.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int count
                )
                || count <= 0
                || at + count >= groups.Count
            )
            {
                return None;
            }

            var values = new List<double>(count + 1);

            for (int i = 0; i < count; i++)
            {
                if (!Number(groups[at + 1 + i].Value, out double value))
                {
                    return None;
                }

                values.Add(value);
            }

            if (
                !double.IsNaN(own)
                && !values.Exists(v =>
                    angle
                        ? Round(v, own) <= Tolerance.Default.EqualVector
                        : Math.Abs(v - own) <= Tolerance.Default.EqualPoint
                )
            )
            {
                values.Insert(values.FindLastIndex(v => v < own) + 1, own);
            }

            return values.ConvertAll(v => (object)(v * scale)).ToArray();
        }

        /// <summary>How far apart two angles are round the circle, from nought to pi.</summary>
        private static double Round(double a, double b)
        {
            double apart = Math.Abs(a - b) % Turn;
            return apart > Math.PI ? Turn - apart : apart;
        }

        internal static bool Number(string text, out double value) =>
            Numbers.TryRead(text?.Trim(), out value) && Guard.IsFinite(value);

        private static double Distance(double[] from, double[] to)
        {
            double dx = to[0] - from[0];
            double dy = to[1] - from[1];
            double dz = to[2] - from[2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>An angle taken into a turn: from nought up to, not including, 2 pi.</summary>
        private static double Normal(double angle)
        {
            double turned = angle % Turn;

            if (turned < 0.0)
            {
                turned += Turn;
            }

            return turned >= Turn ? 0.0 : turned;
        }
    }
}
