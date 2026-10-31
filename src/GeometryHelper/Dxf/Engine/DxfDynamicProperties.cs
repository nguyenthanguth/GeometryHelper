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
    internal static class DxfDynamicProperties
    {
        private const double Turn = 2.0 * Math.PI;

        private static readonly object[] None = new object[0];

        private static readonly object[] FlipStates = { (short)0, (short)1 };

        /// <summary>
        /// The properties of an INSERT of a dynamic block, as <see cref="DxfBlockReference.DynamicBlockReferencePropertyCollection"/>
        /// gives them; none for a block of no graph. A length is scaled by the INSERT's largest scale factor.
        /// </summary>
        internal static IReadOnlyList<DxfDynamicBlockReferenceProperty> Of(DxfBlockReference insert, DxfBlockTableRecord dynamic)
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

            foreach (DxfItem node in nodes)
            {
                // Actions and grips give no property: their groups are not read.
                if (!node.Type.StartsWith("BLOCK", StringComparison.Ordinal) || !node.Type.EndsWith("PARAMETER", StringComparison.Ordinal))
                {
                    continue;
                }

                IReadOnlyList<DxfTypedValue> groups = node.Groups;
                string id = Text(Section(groups, "AcDbEvalExpr"), 90)?.Trim();
                IReadOnlyList<DxfTypedValue> cache = id != null && saved.TryGetValue(id, out DxfXrecord record) ? record.Data : null;
                int before = properties.Count;

                switch (node.Type)
                {
                    case "BLOCKLINEARPARAMETER":
                        Linear(groups, cache, scale, properties);
                        Bind(properties, before, insert, id);
                        break;
                    case "BLOCKPOLARPARAMETER":
                        Polar(groups, cache, scale, properties);
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
                        Lookup(groups, id, cache, nodes, properties);
                        break;
                }
            }

            return properties.AsReadOnly();
        }

        /// <summary>Ties each property a parameter gave, where it gave one, to the INSERT, the parameter's node and its place among them, for its value to be set.</summary>
        private static void Bind(List<DxfDynamicBlockReferenceProperty> properties, int at, DxfBlockReference insert, string id)
        {
            for (int i = at; id != null && i < properties.Count; i++)
            {
                properties[i].Bind(insert, id, i - at);
            }
        }

        /// <summary>A linear parameter: its distance, the length from its base point to its end point (305 its label, 306 its description).</summary>
        private static void Linear(IReadOnlyList<DxfTypedValue> groups, IReadOnlyList<DxfTypedValue> cache, double scale, List<DxfDynamicBlockReferenceProperty> properties)
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockLinearParameter");

            if (!TwoPoints(groups, cache, out double[] from, out double[] to))
            {
                return;
            }

            properties.Add(Length(own, 305, 306, Distance(from, to) * scale, List(own, 175, scale)));
        }

        /// <summary>
        /// A polar parameter: its distance, from its base point to its end point (305 and 306), and the angle of that line
        /// from the block's x axis (307 and 308); the list of the distance's value set counted by 175, the angle's by 176.
        /// </summary>
        private static void Polar(IReadOnlyList<DxfTypedValue> groups, IReadOnlyList<DxfTypedValue> cache, double scale, List<DxfDynamicBlockReferenceProperty> properties)
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockPolarParameter");

            if (!TwoPoints(groups, cache, out double[] from, out double[] to))
            {
                return;
            }

            properties.Add(Length(own, 305, 306, Distance(from, to) * scale, List(own, 175, scale)));
            properties.Add(Angle(own, 307, 308, Normal(Math.Atan2(to[1] - from[1], to[0] - from[0])), List(own, 176, 1.0)));
        }

        /// <summary>
        /// An XY parameter: its distance along x (306 its label, 308 its description, its list counted by 175) and along y
        /// (305, 307, 176), each the length between its two points along that axis, as AutoCAD writes the y first.
        /// </summary>
        private static void Xy(IReadOnlyList<DxfTypedValue> groups, IReadOnlyList<DxfTypedValue> cache, double scale, List<DxfDynamicBlockReferenceProperty> properties)
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockXYParameter");

            if (!TwoPoints(groups, cache, out double[] from, out double[] to))
            {
                return;
            }

            properties.Add(Length(own, 306, 308, Math.Abs(to[0] - from[0]) * scale, List(own, 175, scale)));
            properties.Add(Length(own, 305, 307, Math.Abs(to[1] - from[1]) * scale, List(own, 176, scale)));
        }

        /// <summary>
        /// A rotation parameter: the angle of the line from its base point to its end point, from that to its base angle point
        /// (1011 of its own, the fourth point saved), counter-clockwise, from nought up to a whole turn.
        /// </summary>
        private static void Rotation(IReadOnlyList<DxfTypedValue> groups, IReadOnlyList<DxfTypedValue> cache, List<DxfDynamicBlockReferenceProperty> properties)
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

            double angle = Math.Atan2(to[1] - from[1], to[0] - from[0]) - Math.Atan2(basis[1] - from[1], basis[0] - from[0]);
            properties.Add(Angle(own, 305, 306, Normal(angle), List(own, 175, 1.0)));
        }

        /// <summary>A point parameter: its position's x and y, the label (303) with " X" and " Y" after it, both of its description (304).</summary>
        private static void Point(IReadOnlyList<DxfTypedValue> groups, IReadOnlyList<DxfTypedValue> cache, double scale, List<DxfDynamicBlockReferenceProperty> properties)
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
            properties.Add(new DxfDynamicBlockReferenceProperty(name + " X", description, at[0] * scale, 1, DxfDynamicBlockReferencePropertyUnitsType.Distance, false, None));
            properties.Add(new DxfDynamicBlockReferenceProperty(name + " Y", description, at[1] * scale, 1, DxfDynamicBlockReferencePropertyUnitsType.Distance, false, None));
        }

        /// <summary>A flip parameter: 1 where the INSERT saved it flipped (the last 70 of its data after the two of its head), else 0.</summary>
        private static void Flip(IReadOnlyList<DxfTypedValue> groups, IReadOnlyList<DxfTypedValue> cache, List<DxfDynamicBlockReferenceProperty> properties)
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockFlipParameter");
            short state = 0;
            int seen = 0;

            foreach (DxfTypedValue group in cache ?? (IReadOnlyList<DxfTypedValue>)new DxfTypedValue[0])
            {
                if (group.TypeCode == 70 && ++seen > 2 && short.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out short read))
                {
                    state = read;
                }
            }

            properties.Add(new DxfDynamicBlockReferenceProperty(Name(own, 305), Words(own, 306), state, 3, DxfDynamicBlockReferencePropertyUnitsType.NoUnits, false, FlipStates));
        }

        /// <summary>
        /// A visibility parameter: the state the INSERT saved (1 of its data), else the first of the parameter's states (each
        /// a 303 of its own), as AutoCAD inserts a block in it; every state allowed.
        /// </summary>
        private static void Visibility(IReadOnlyList<DxfTypedValue> groups, IReadOnlyList<DxfTypedValue> cache, List<DxfDynamicBlockReferenceProperty> properties)
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
            properties.Add(new DxfDynamicBlockReferenceProperty(Name(own, 301), Words(own, 302), state, 5, DxfDynamicBlockReferencePropertyUnitsType.NoUnits, false, states.ToArray()));
        }

        /// <summary>
        /// A lookup parameter: the value the INSERT saved (1 of its data); its column of the lookup table of the action its 94
        /// names, the column whose node (94) is the parameter's, gives the values allowed, each once, in the order of the rows.
        /// Where the INSERT saved none, a table with columns to look up from gives the column's label of no match (305), which
        /// is then allowed too, a table of none its first value. Read only where the column allows no reverse lookup (281 of 0).
        /// </summary>
        private static void Lookup(IReadOnlyList<DxfTypedValue> groups, string id, IReadOnlyList<DxfTypedValue> cache, List<DxfItem> nodes, List<DxfDynamicBlockReferenceProperty> properties)
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockLookUpParameter");
            string action = Text(own, 94)?.Trim();
            List<DxfTypedValue> table = null;

            foreach (DxfItem node in nodes)
            {
                if (node.Type == "BLOCKLOOKUPACTION" && Text(Section(node.Groups, "AcDbEvalExpr"), 90)?.Trim() == action)
                {
                    table = Section(node.Groups, "AcDbBlockLookupAction");
                    break;
                }
            }

            if (table == null || id == null || !int.TryParse(Text(table, 93)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) || width <= 0)
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
            string unmatched = DxfReader.Decode(columns[column].TryGetValue(305, out string label) ? label : string.Empty);
            string value = Saved(cache) ?? (inputs ? unmatched : allowed.Count > 0 ? (string)allowed[0] : string.Empty);

            if (inputs && value == unmatched && !allowed.Contains(unmatched))
            {
                allowed.Add(unmatched);
            }

            bool readOnly = !(columns[column].TryGetValue(281, out string reverse) && reverse.Trim() == "1");
            properties.Add(new DxfDynamicBlockReferenceProperty(Name(own, 303), Words(own, 304), value, 5, DxfDynamicBlockReferencePropertyUnitsType.NoUnits, readOnly, allowed.ToArray()));
        }

        /// <summary>A length property of a label and a description of the codes given.</summary>
        private static DxfDynamicBlockReferenceProperty Length(List<DxfTypedValue> own, int name, int description, double value, object[] allowed)
            => new DxfDynamicBlockReferenceProperty(Name(own, name), Words(own, description), value, 1, DxfDynamicBlockReferencePropertyUnitsType.Distance, false, allowed);

        /// <summary>An angle property of a label and a description of the codes given.</summary>
        private static DxfDynamicBlockReferenceProperty Angle(List<DxfTypedValue> own, int name, int description, double value, object[] allowed)
            => new DxfDynamicBlockReferenceProperty(Name(own, name), Words(own, description), value, 1, DxfDynamicBlockReferencePropertyUnitsType.Angular, false, allowed);

        /// <summary>
        /// The base point and end point of a parameter of two points: the first two points the INSERT saved, else the block's
        /// own (1010 and 1011 of AcDbBlock2PtParameter); false where neither can be read.
        /// </summary>
        private static bool TwoPoints(IReadOnlyList<DxfTypedValue> groups, IReadOnlyList<DxfTypedValue> cache, out double[] from, out double[] to)
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

        /// <summary>The points of an INSERT's data of a node (10, 20, 30 each), in order; none where it has none.</summary>
        private static List<double[]> Points(IReadOnlyList<DxfTypedValue> cache)
        {
            var points = new List<double[]>();

            for (int i = 0; cache != null && i + 2 < cache.Count; i++)
            {
                if (cache[i].TypeCode == 10 && cache[i + 1].TypeCode == 20 && cache[i + 2].TypeCode == 30
                    && Number(cache[i].Value, out double x) && Number(cache[i + 1].Value, out double y) && Number(cache[i + 2].Value, out double z))
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
            DxfDBDictionary data = Entry(Entry(Entry(insert.ExtensionDictionary, "AcDbBlockRepresentation") as DxfDBDictionary, "AppDataCache") as DxfDBDictionary, "ACAD_ENHANCEDBLOCKDATA") as DxfDBDictionary;

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

        /// <summary>The object of a dictionary's entry; null where the dictionary is none or has no entry of the name.</summary>
        internal static DxfDBObject Entry(DxfDBDictionary dictionary, string name)
            => dictionary != null && dictionary.Contains(name) ? dictionary.GetAt(name) : null;

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

                    inside = group.TypeCode == 100 && string.Equals(group.Value?.Trim(), marker, StringComparison.Ordinal);
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
        private static string Name(List<DxfTypedValue> groups, int code) => DxfReader.NameOf(Text(groups, code) ?? string.Empty);

        /// <summary>A description: the first group of a code, its escapes read, its spaces kept.</summary>
        private static string Words(List<DxfTypedValue> groups, int code) => DxfReader.Decode(Text(groups, code) ?? string.Empty);

        /// <summary>The point of a code and the two after it by ten (1010, 1020, 1030); null where it cannot be read.</summary>
        internal static double[] PointOf(List<DxfTypedValue> groups, int code)
            => Number(Text(groups, code), out double x) && Number(Text(groups, code + 10), out double y) && Number(Text(groups, code + 20) ?? "0", out double z) ? new[] { x, y, z } : null;

        /// <summary>
        /// The list of a value set: the reals after the group of a code that counts them (175 or 176), each scaled, where the
        /// flags of the set (96 before 175, 97 before 176) hold 8, a list; none where they do not, as AutoCAD 2021 gave none
        /// of a list of three under the flags 1, 2 and 4, and the three under 8; none where the count is nought, or the list
        /// cannot be read whole.
        /// </summary>
        private static object[] List(List<DxfTypedValue> groups, int countCode, double scale)
        {
            int at = groups.FindIndex(g => g.TypeCode == countCode);
            int flags = groups.FindIndex(g => g.TypeCode == (countCode == 175 ? 96 : 97));

            if (flags < 0 || !int.TryParse(groups[flags].Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int set) || (set & 8) == 0)
            {
                return None;
            }

            if (at < 0 || !int.TryParse(groups[at].Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count <= 0 || at + count >= groups.Count)
            {
                return None;
            }

            var values = new object[count];

            for (int i = 0; i < count; i++)
            {
                if (!Number(groups[at + 1 + i].Value, out double value))
                {
                    return None;
                }

                values[i] = value * scale;
            }

            return values;
        }

        internal static bool Number(string text, out double value) => Numbers.TryRead(text?.Trim(), out value) && Guard.IsFinite(value);

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
