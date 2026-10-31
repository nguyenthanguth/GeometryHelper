using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Sets the value of a dynamic property of an INSERT as AutoCAD's <c>DynamicBlockReferenceProperty.Value</c> sets it, for a
    /// dynamic block of linear parameters with stretches of lines and polylines and of a visibility parameter: the value taken to
    /// its value set, the block worked out at the values of all its properties into an anonymous block of AutoCAD's (*U) the
    /// INSERT places, and the INSERT's representation data and cache (ACAD_ENHANCEDBLOCKDATA) written as AutoCAD 2021 wrote
    /// them when it set a value (plugins v17, v18): no history.
    /// </summary>
    internal static class DxfDynamicBlockRepresentation
    {
        // The two numbers each record of AutoCAD's cache opens with, by the kind of its node (the same in every file seen).
        private static readonly string[] LinearKey = { "18597260", "25303744" };
        private static readonly string[] StretchKey = { "6895636", "9291323" };
        private static readonly string[] VisibilityKey = { "135625452", "184556386" };

        /// <summary>
        /// Sets the value of the property of a node of the INSERT's dynamic block, as
        /// <see cref="DxfDynamicBlockReferenceProperty.Value"/> says, and gives back the value read back.
        /// </summary>
        internal static object Set(DxfBlockReference insert, string node, object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!insert.IsResident || insert.IsErased || insert.Item.File.Database == null)
            {
                throw new InvalidOperationException("The INSERT of " + insert.Name + " is in no drawing, or is erased, and its dynamic properties are not set.");
            }

            DxfBlockTableRecord dynamic = insert.DynamicBlockTableRecord;

            if (!insert.IsDynamicBlock)
            {
                throw new InvalidOperationException("The INSERT " + insert.Handle + " places no dynamic block, or is scaled unlike along its axes, which AutoCAD does not take as a dynamic block's.");
            }

            Block block = Block.Of(dynamic);
            GeoVector3 factors = insert.ScaleFactors;
            double scale = Math.Max(Math.Abs(factors.X), Math.Max(Math.Abs(factors.Y), Math.Abs(factors.Z)));

            // The values the INSERT has now: each distance in the block's own units, and the state.
            var distances = new Dictionary<string, double>(StringComparer.Ordinal);
            string state = null;

            foreach (DxfDynamicBlockReferenceProperty property in DxfDynamicProperties.Of(insert, dynamic))
            {
                if (property.Node != null && property.Value is double distance)
                {
                    distances[property.Node] = distance / scale;
                }
                else if (property.Node != null && property.Value is string name)
                {
                    state = name;
                }
            }

            Linear linear = block.Linears.Find(l => l.Id == node);

            if (linear != null)
            {
                double asked = Number(value);
                double now = distances.TryGetValue(node, out double had) ? had * scale : linear.Distance * scale;
                double taken = linear.Take(asked, now, scale);

                if (!(taken / scale > Tolerance.Default.EqualPoint))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A distance of a linear parameter is above the point tolerance once taken to its value set.");
                }

                distances[node] = taken / scale;
            }
            else if (block.Visibility != null && block.Visibility.Id == node)
            {
                string asked = value as string ?? throw new ArgumentException("A visibility state is set by its name, a string; a " + value.GetType().Name + " is not one.", nameof(value));
                State found = block.Visibility.States.Find(s => string.Equals(s.Name, asked.Trim(), StringComparison.OrdinalIgnoreCase));
                state = found?.Name ?? throw new ArgumentException("The visibility parameter " + block.Visibility.Name + " has no state named " + asked + ".", nameof(value));
            }
            else
            {
                throw new NotSupportedException("The property's parameter is not one the library works out.");
            }

            Write(insert, dynamic, block, distances, state);

            foreach (DxfDynamicBlockReferenceProperty property in DxfDynamicProperties.Of(insert, dynamic))
            {
                if (property.Node == node)
                {
                    return property.Value;
                }
            }

            return value;
        }

        /// <summary>A number given as a distance: a <see cref="double"/>, or a whole or other number taken as one; finite.</summary>
        private static double Number(object value)
        {
            double number;

            switch (value)
            {
                case double d:
                    number = d;
                    break;
                case float f:
                    number = f;
                    break;
                case int i:
                    number = i;
                    break;
                case long l:
                    number = l;
                    break;
                case short s:
                    number = s;
                    break;
                case decimal m:
                    number = (double)m;
                    break;
                default:
                    throw new ArgumentException("A distance is set as a number; a " + value.GetType().Name + " is not one.", nameof(value));
            }

            if (!Guard.IsFinite(number))
            {
                throw new ArgumentException("A distance is set as a finite number; " + number.ToString("R", CultureInfo.InvariantCulture) + " is not.", nameof(value));
            }

            return number;
        }

        /// <summary>
        /// Makes the INSERT place the block worked out at the values given: its anonymous block filled anew where no other INSERT
        /// places it, else a new one; then its representation data and cache written.
        /// </summary>
        private static void Write(DxfBlockReference insert, DxfBlockTableRecord dynamic, Block block, Dictionary<string, double> distances, string state)
        {
            DxfFile file = insert.Item.File;
            DxfDatabase database = file.Database;
            DxfBlockTableRecord anonymous = insert.AnonymousBlockTableRecord;

            // The anonymous block is the INSERT's own where no other INSERT places it.
            bool own = anonymous != null;

            if (own)
            {
                foreach (DxfBlockReference other in anonymous.GetBlockReferences(true))
                {
                    if (!string.Equals(other.Handle, insert.Handle, StringComparison.OrdinalIgnoreCase))
                    {
                        own = false;
                        break;
                    }
                }
            }

            if (own)
            {
                foreach (DxfEntity entity in new List<DxfEntity>(anonymous))
                {
                    entity.Erase();
                }
            }
            else
            {
                anonymous = new DxfBlockTableRecord { Name = database.BlockTable.FreeAnonymousName("*U"), MadeFlags = 1, Origin = dynamic.Origin };

                if (file.Supports(1021))
                {
                    anonymous.Units = dynamic.Units;
                }

                database.BlockTable.AddAnonymous(anonymous);
                anonymous.XData = new[] { new DxfTypedValue(1001, "AcDbBlockRepBTag"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1005, dynamic.Handle) };
            }

            var sources = new List<DxfEntity>(dynamic);
            IReadOnlyList<DxfEntity> copies = database.WblockCloneObjects(sources, anonymous);
            var copyOf = new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < sources.Count && i < copies.Count; i++)
            {
                copyOf[sources[i].Handle] = copies[i];
            }

            Stretch(block, distances, copyOf);

            if (block.Visibility != null)
            {
                State shown = block.Visibility.States.Find(s => s.Name == state) ?? block.Visibility.States[0];

                foreach (string handle in block.Visibility.Governed)
                {
                    if (copyOf.TryGetValue(handle, out DxfEntity copy))
                    {
                        copy.Visible = shown.Shown.Contains(handle);
                    }
                }
            }

            if (!ReferenceEquals(insert.BlockTableRecord, anonymous))
            {
                insert.BlockTableRecord = anonymous;
            }

            Cache(insert, dynamic, block, distances, state);
        }

        /// <summary>
        /// Moves the points of the copies each stretch holds by its parameter's change: the distance now less the block's own,
        /// along the line from the parameter's base point to its end point, in x and y; a point held by several stretches moves
        /// by them all.
        /// </summary>
        private static void Stretch(Block block, Dictionary<string, double> distances, Dictionary<string, DxfEntity> copyOf)
        {
            // The moves of each entity's points, by the entity's handle and the point's index; summed in the order of the graph.
            var moves = new Dictionary<string, SortedDictionary<int, double[]>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (Stretched stretch in block.Stretches)
            {
                Linear linear = block.Linears.Find(l => l.Id == stretch.Parameter);
                double change = (distances.TryGetValue(linear.Id, out double now) ? now : linear.Distance) - linear.Distance;
                double dx = linear.UnitX * change;
                double dy = linear.UnitY * change;

                foreach (KeyValuePair<string, List<int>> held in stretch.Held)
                {
                    if (!moves.TryGetValue(held.Key, out SortedDictionary<int, double[]> points))
                    {
                        points = new SortedDictionary<int, double[]>();
                        moves.Add(held.Key, points);
                        order.Add(held.Key);
                    }

                    foreach (int index in held.Value)
                    {
                        if (!points.TryGetValue(index, out double[] move))
                        {
                            move = new double[2];
                            points.Add(index, move);
                        }

                        move[0] += dx;
                        move[1] += dy;
                    }
                }
            }

            foreach (string handle in order)
            {
                if (!copyOf.TryGetValue(handle, out DxfEntity copy))
                {
                    continue;
                }

                foreach (KeyValuePair<int, double[]> move in moves[handle])
                {
                    if (copy is DxfLine line)
                    {
                        GeoPoint3 at = move.Key == 0 ? line.StartPoint : line.EndPoint;
                        var moved = new GeoPoint3(at.X + move.Value[0], at.Y + move.Value[1], at.Z);

                        if (move.Key == 0)
                        {
                            line.StartPoint = moved;
                        }
                        else
                        {
                            line.EndPoint = moved;
                        }
                    }
                    else if (copy is DxfPolyline polyline)
                    {
                        GeoPoint3 at = polyline.GetPoint3dAt(move.Key);
                        GeoPoint3 inPlane = DxfEntity.WorldToOcs(polyline.Normal, new GeoPoint3(at.X + move.Value[0], at.Y + move.Value[1], at.Z));
                        polyline.SetPointAt(move.Key, new GeoPoint2(inPlane.X, inPlane.Y));
                    }
                }
            }
        }

        /// <summary>
        /// The INSERT's extension dictionary as AutoCAD writes it for an INSERT whose value it set: AcDbBlockRepresentation with
        /// the representation data (AcDbRepData) naming the dynamic block, and AppDataCache with a record for each parameter and
        /// action of the graph (ACAD_ENHANCEDBLOCKDATA, by the node's id, in the order of the ids as text); the history and its
        /// data taken out.
        /// </summary>
        private static void Cache(DxfBlockReference insert, DxfBlockTableRecord dynamic, Block block, Dictionary<string, double> distances, string state)
        {
            DxfFile file = insert.Item.File;
            insert.CreateExtensionDictionary();
            DxfDBDictionary representation = Child(insert.ExtensionDictionary, "AcDbBlockRepresentation");
            DxfDBObject data = DxfDynamicProperties.Entry(representation, "AcDbRepData");

            if (data?.Item == null || data.Item.Type != "ACDB_BLOCKREPRESENTATION_DATA" || !string.Equals(file.ValueOf(data.Item, 340)?.Trim(), dynamic.Handle, StringComparison.OrdinalIgnoreCase))
            {
                representation.Add("AcDbRepData", "ACDB_BLOCKREPRESENTATION_DATA", b =>
                {
                    b.Group(100, "AcDbBlockRepresentationData");
                    b.Group(70, "1");
                    b.Group(340, dynamic.Handle);
                });
            }

            file.AddClass("ACDB_BLOCKREPRESENTATION_DATA", "AcDbBlockRepresentationData", 1153);
            DxfDBDictionary cache = Child(representation, "AppDataCache");

            foreach (string kept in new[] { "ACAD_ENHANCEDBLOCKHISTORY", "ACAD_ENHANCEDBLOCKHDATA" })
            {
                if (cache.Contains(kept))
                {
                    cache.Remove(kept);
                }
            }

            DxfDBDictionary records = Child(cache, "ACAD_ENHANCEDBLOCKDATA");
            var written = new List<KeyValuePair<string, List<KeyValuePair<int, string>>>>();

            foreach (Linear linear in block.Linears)
            {
                double distance = distances.TryGetValue(linear.Id, out double now) ? now : linear.Distance;
                var end = new GeoPoint3(linear.Base.X + linear.UnitX * distance, linear.Base.Y + linear.UnitY * distance, linear.Base.Z + linear.UnitZ * distance);
                List<KeyValuePair<int, string>> groups = Head(LinearKey);
                Point(groups, linear.Base);
                Point(groups, end);
                Point(groups, new GeoPoint3(0, 0, -1));
                written.Add(new KeyValuePair<string, List<KeyValuePair<int, string>>>(linear.Id, groups));
            }

            foreach (Stretched stretch in block.Stretches)
            {
                List<KeyValuePair<int, string>> groups = Head(StretchKey);
                groups.Add(new KeyValuePair<int, string>(40, Real(0.0)));
                written.Add(new KeyValuePair<string, List<KeyValuePair<int, string>>>(stretch.Id, groups));
            }

            if (block.Visibility != null)
            {
                List<KeyValuePair<int, string>> groups = Head(VisibilityKey);
                Point(groups, block.Visibility.Location);
                groups.Add(new KeyValuePair<int, string>(1, state ?? block.Visibility.States[0].Name));
                written.Add(new KeyValuePair<string, List<KeyValuePair<int, string>>>(block.Visibility.Id, groups));
            }

            written.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            foreach (KeyValuePair<string, List<KeyValuePair<int, string>>> record in written)
            {
                records.Add(record.Key, "XRECORD", b =>
                {
                    b.Group(100, "AcDbXrecord");
                    b.Group(280, "1");
                    record.Value.ForEach(g => b.Group(g.Key, g.Value));
                });
            }
        }

        /// <summary>The dictionary of an entry of a dictionary, made where it has none: owning its entries hard, as AutoCAD writes those of an INSERT's representation.</summary>
        private static DxfDBDictionary Child(DxfDBDictionary owner, string name)
        {
            if (DxfDynamicProperties.Entry(owner, name) is DxfDBDictionary found)
            {
                return found;
            }

            DxfItem made = owner.Add(name, "DICTIONARY", b =>
            {
                b.Group(100, "AcDbDictionary");
                b.Group(280, "1");
                b.Group(281, "1");
            });

            return (DxfDBDictionary)DxfDBObject.OfObject(made);
        }

        /// <summary>The head of a record of the cache: the two numbers of its kind, then 25 and 104, as AutoCAD writes them.</summary>
        private static List<KeyValuePair<int, string>> Head(string[] key) => new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(1071, key[0]),
            new KeyValuePair<int, string>(1071, key[1]),
            new KeyValuePair<int, string>(70, "25"),
            new KeyValuePair<int, string>(70, "104"),
        };

        private static void Point(List<KeyValuePair<int, string>> groups, GeoPoint3 point)
        {
            groups.Add(new KeyValuePair<int, string>(10, Real(point.X)));
            groups.Add(new KeyValuePair<int, string>(20, Real(point.Y)));
            groups.Add(new KeyValuePair<int, string>(30, Real(point.Z)));
        }

        private static string Real(double value) => Numbers.Exact(value + 0.0);

        /// <summary>
        /// The parameters and actions of a dynamic block's evaluation graph that the library works out, read from its nodes in
        /// their order; any other node refused with <see cref="NotSupportedException"/>, as is a stretch it does not work out.
        /// </summary>
        private sealed class Block
        {
            internal List<Linear> Linears { get; } = new List<Linear>();

            internal List<Stretched> Stretches { get; } = new List<Stretched>();

            internal Visible Visibility { get; private set; }

            internal static Block Of(DxfBlockTableRecord dynamic)
            {
                var block = new Block();
                DxfDBObject graph = DxfDynamicProperties.Entry(dynamic.ExtensionDictionary, "ACAD_ENHANCEDBLOCK");

                if (graph?.Item == null || graph.Item.Type != "ACAD_EVALUATION_GRAPH")
                {
                    throw new NotSupportedException("The block " + dynamic.Name + " has no evaluation graph to work out.");
                }

                DxfFile file = graph.Item.File;

                foreach (DxfTypedValue group in graph.Item.Groups)
                {
                    if (group.TypeCode != 360 || !(file.ObjectItem(group.Value) is DxfItem node))
                    {
                        continue;
                    }

                    IReadOnlyList<DxfTypedValue> groups = node.Groups;
                    string id = DxfDynamicProperties.Text(DxfDynamicProperties.Section(groups, "AcDbEvalExpr"), 90)?.Trim();

                    switch (node.Type)
                    {
                        case "BLOCKLINEARPARAMETER":
                            block.Linears.Add(Linear.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKSTRETCHACTION":
                            block.Stretches.Add(Stretched.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKVISIBILITYPARAMETER":
                            block.Visibility = Visible.Of(groups, id);
                            break;
                        case "BLOCKLINEARGRIP":
                        case "BLOCKVISIBILITYGRIP":
                        case "BLOCKGRIPLOCATIONCOMPONENT":
                            break;
                        default:
                            throw new NotSupportedException("The block " + dynamic.Name + " holds a " + node.Type + ", which the library does not work out: it works out linear parameters with stretches of lines and polylines, and a visibility parameter.");
                    }
                }

                // Each stretch follows a linear parameter of the block, and holds lines and polylines of it by points they have.
                var entities = new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfEntity entity in dynamic)
                {
                    entities[entity.Handle] = entity;
                }

                foreach (Stretched stretch in block.Stretches)
                {
                    if (!block.Linears.Exists(l => l.Id == stretch.Parameter))
                    {
                        throw new NotSupportedException("A stretch of the block " + dynamic.Name + " follows a node other than a linear parameter, which the library does not work out.");
                    }

                    foreach (KeyValuePair<string, List<int>> held in stretch.Held)
                    {
                        entities.TryGetValue(held.Key, out DxfEntity entity);
                        int count = entity is DxfLine ? 2 : entity is DxfPolyline polyline ? polyline.NumberOfVertices : 0;

                        if (count == 0 || held.Value.Exists(i => i < 0 || i >= count))
                        {
                            throw new NotSupportedException("A stretch of the block " + dynamic.Name + " holds the entity " + held.Key + ", which is not a line or a polyline of the block with the points it names: the library does not work it out.");
                        }
                    }
                }

                return block;
            }
        }

        /// <summary>A linear parameter: its node's id, its base point, the unit direction to its end point, its distance and its value set.</summary>
        private sealed class Linear
        {
            private int _flags;
            private double _minimum;
            private double _maximum;
            private double _increment;
            private List<double> _list = new List<double>();

            internal string Id { get; private set; }

            internal GeoPoint3 Base { get; private set; }

            internal double Distance { get; private set; }

            internal double UnitX { get; private set; }

            internal double UnitY { get; private set; }

            internal double UnitZ { get; private set; }

            internal static Linear Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                List<DxfTypedValue> two = DxfDynamicProperties.Section(groups, "AcDbBlock2PtParameter");
                double[] from = DxfDynamicProperties.PointOf(two, 1010);
                double[] to = DxfDynamicProperties.PointOf(two, 1011);
                string location = DxfDynamicProperties.Text(two, 177)?.Trim();

                if (id == null || from == null || to == null || (location != null && location != "0"))
                {
                    throw new NotSupportedException("A linear parameter of the block " + block + " is not one the library works out: its points cannot be read, or its distance is measured from its middle.");
                }

                double dx = to[0] - from[0], dy = to[1] - from[1], dz = to[2] - from[2];
                double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                if (!(length > 0.0))
                {
                    throw new NotSupportedException("A linear parameter of the block " + block + " has its end point on its base point.");
                }

                var linear = new Linear { Id = id, Base = new GeoPoint3(from[0], from[1], from[2]), Distance = length, UnitX = dx / length, UnitY = dy / length, UnitZ = dz / length };
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockLinearParameter");
                int count = -1;

                foreach (DxfTypedValue group in own)
                {
                    double number;

                    switch (group.TypeCode)
                    {
                        case 96:
                            int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out linear._flags);
                            break;
                        case 141:
                            linear._minimum = DxfDynamicProperties.Number(group.Value, out number) ? number : 0.0;
                            break;
                        case 142:
                            linear._maximum = DxfDynamicProperties.Number(group.Value, out number) ? number : 0.0;
                            break;
                        case 143:
                            linear._increment = DxfDynamicProperties.Number(group.Value, out number) ? number : 0.0;
                            break;
                        case 175:
                            int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
                            break;
                        case 144:
                            if (count > linear._list.Count && DxfDynamicProperties.Number(group.Value, out number))
                            {
                                linear._list.Add(number);
                            }

                            break;
                    }
                }

                linear._list.Sort();
                return linear;
            }

            /// <summary>
            /// A value, in the INSERT's units (the block's scaled), taken to the value set as AutoCAD 2021 took it: a list (flags
            /// 8) to its nearest value, a tie to the one nearer the value the property had, then to the least; else to the nearest
            /// step of the increment from the minimum (flags 4), a tie to the even step, then into the minimum (flags 1) and the
            /// maximum (flags 2). Ties within the point tolerance of <see cref="Tolerance.Default"/>.
            /// </summary>
            internal double Take(double value, double now, double scale)
            {
                double slack = Tolerance.Default.EqualPoint;

                if ((_flags & 8) != 0 && _list.Count > 0)
                {
                    double best = _list[0] * scale;

                    for (int i = 1; i < _list.Count; i++)
                    {
                        double candidate = _list[i] * scale;
                        double nearer = Math.Abs(value - best) - Math.Abs(value - candidate);

                        if (nearer > slack || (Math.Abs(nearer) <= slack && Math.Abs(now - candidate) < Math.Abs(now - best) - slack))
                        {
                            best = candidate;
                        }
                    }

                    return best;
                }

                double taken = value;
                double step = _increment * scale;
                double minimum = _minimum * scale;

                if ((_flags & 4) != 0 && step > 0.0)
                {
                    double steps = (value - minimum) / step;
                    double below = Math.Floor(steps);
                    double gap = (steps - below) * step;

                    // Nearer the step below, nearer the one above, or between them within the tolerance: then the even one.
                    double chosen = Math.Abs(gap - (step - gap)) <= slack ? (below % 2.0 == 0.0 ? below : below + 1.0) : gap < step - gap ? below : below + 1.0;
                    taken = minimum + chosen * step;
                }

                if ((_flags & 1) != 0 && taken < minimum)
                {
                    taken = minimum;
                }

                if ((_flags & 2) != 0 && taken > _maximum * scale)
                {
                    taken = _maximum * scale;
                }

                return taken;
            }
        }

        /// <summary>A stretch action: its node's id, the linear parameter it follows, and the points it holds, by the entity's handle.</summary>
        private sealed class Stretched
        {
            internal string Id { get; private set; }

            internal string Parameter { get; private set; }

            internal List<KeyValuePair<string, List<int>>> Held { get; } = new List<KeyValuePair<string, List<int>>>();

            internal static Stretched Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var stretch = new Stretched { Id = id };
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockStretchAction");
                string x = null, y = null, xBy = null, yBy = null;
                bool entities = false;
                double multiplier = 1.0, angle = 0.0;
                int parameters = 0;

                foreach (DxfTypedValue group in own)
                {
                    string text = group.Value?.Trim();

                    switch (group.TypeCode)
                    {
                        case 92:
                            x = text;
                            break;
                        case 93:
                            y = text;
                            break;
                        case 301:
                            xBy = text;
                            break;
                        case 302:
                            yBy = text;
                            break;
                        case 73:
                            entities = true;
                            break;
                        case 331:
                            stretch.Held.Add(new KeyValuePair<string, List<int>>(text, new List<int>()));
                            break;
                        case 94:
                            if (entities && stretch.Held.Count > 0 && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                            {
                                stretch.Held[stretch.Held.Count - 1].Value.Add(index);
                            }

                            break;
                        case 75:
                            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parameters);
                            entities = false;
                            break;
                        case 140:
                            DxfDynamicProperties.Number(text, out multiplier);
                            break;
                        case 141:
                            DxfDynamicProperties.Number(text, out angle);
                            break;
                    }
                }

                double slack = Tolerance.Default.EqualVector;

                if (id == null || x == null || x != y || xBy != "EndXDelta" || yBy != "EndYDelta" || parameters != 0 || Math.Abs(multiplier - 1.0) > slack || Math.Abs(angle) > slack)
                {
                    throw new NotSupportedException("A stretch of the block " + block + " is not one the library works out: it works out a stretch by the end point of a linear parameter, of lines and polylines only, unscaled and unturned.");
                }

                stretch.Parameter = x;
                return stretch;
            }
        }

        /// <summary>A visibility parameter: its node's id, its label, its location, the entities it governs and its states.</summary>
        private sealed class Visible
        {
            internal string Id { get; private set; }

            internal string Name { get; private set; }

            internal GeoPoint3 Location { get; private set; }

            internal List<string> Governed { get; } = new List<string>();

            internal List<State> States { get; } = new List<State>();

            internal static Visible Of(IReadOnlyList<DxfTypedValue> groups, string id)
            {
                double[] at = DxfDynamicProperties.PointOf(DxfDynamicProperties.Section(groups, "AcDbBlock1PtParameter"), 1010) ?? new double[3];
                var visible = new Visible { Id = id, Location = new GeoPoint3(at[0], at[1], at[2]) };

                foreach (DxfTypedValue group in DxfDynamicProperties.Section(groups, "AcDbBlockVisibilityParameter"))
                {
                    switch (group.TypeCode)
                    {
                        case 301:
                            visible.Name = DxfReader.NameOf(group.Value ?? string.Empty);
                            break;
                        case 331:
                            visible.Governed.Add(group.Value?.Trim());
                            break;
                        case 303:
                            visible.States.Add(new State(DxfReader.Decode(group.Value ?? string.Empty)));
                            break;
                        case 332:
                            if (visible.States.Count > 0)
                            {
                                visible.States[visible.States.Count - 1].Shown.Add(group.Value?.Trim());
                            }

                            break;
                    }
                }

                if (id == null || visible.States.Count == 0)
                {
                    throw new NotSupportedException("A visibility parameter of no state, or of no id, is not worked out.");
                }

                return visible;
            }
        }

        /// <summary>A visibility state: its name and the handles of the entities it shows.</summary>
        private sealed class State
        {
            internal State(string name)
            {
                Name = name;
            }

            internal string Name { get; }

            internal HashSet<string> Shown { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
