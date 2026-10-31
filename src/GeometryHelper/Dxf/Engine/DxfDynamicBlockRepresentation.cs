using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Sets the value of a dynamic property of an INSERT as AutoCAD's <c>DynamicBlockReferenceProperty.Value</c> sets it, for a
    /// dynamic block of linear, XY, point, rotation, flip, visibility and lookup parameters with the stretch, move, scale,
    /// rotate, flip, array and lookup actions the library writes: the value taken to its value set, the block worked out at
    /// the values of all its properties into an anonymous block of AutoCAD's (*U) the INSERT places, and the INSERT's
    /// representation data and cache (ACAD_ENHANCEDBLOCKDATA) written as AutoCAD 2021 wrote them when it set a value (plugins
    /// v17, v18): no history.
    /// </summary>
    internal static class DxfDynamicBlockRepresentation
    {
        private const double Turn = 2.0 * Math.PI;

        // The two numbers each record of AutoCAD's cache opens with, by the kind of its node (the same in every file seen).
        private static readonly string[] LinearKey = { "18597260", "25303744" };
        private static readonly string[] StretchKey = { "6895636", "9291323" };
        private static readonly string[] VisibilityKey = { "135625452", "184556386" };
        private static readonly string[] XYKey = { "2550941", "3467982" };
        private static readonly string[] PointKey = { "11324608", "15407465" };
        private static readonly string[] RotationKey = { "50242585", "68333042" };
        private static readonly string[] FlipKey = { "6887019", "9372877" };
        private static readonly string[] FlipGripKey = { "575085", "763560" };
        private static readonly string[] MoveKey = { "1553528", "2094574" };
        private static readonly string[] ScaleKey = { "2551693", "3435815" };
        private static readonly string[] RotateKey = { "4195796", "5656503" };
        private static readonly string[] LookupKey = { "18605567", "25317787" };

        /// <summary>
        /// Sets the value of a property of a node of the INSERT's dynamic block (its part: 0, else 1 for the y of a point or of
        /// an XY parameter), as <see cref="DxfDynamicBlockReferenceProperty.Value"/> says, and gives back the value read back.
        /// </summary>
        internal static object Set(DxfBlockReference insert, string node, int part, object value)
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

            // The values the INSERT has now, by the parameter's node: lengths and positions in the block's own units, angles,
            // flip states; and the visibility state.
            var values = new Dictionary<string, double[]>(StringComparer.Ordinal);
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            string state = null;

            foreach (Param parameter in block.Parameters)
            {
                values[parameter.Id] = (double[])parameter.Own.Clone();
            }

            foreach (DxfDynamicBlockReferenceProperty property in DxfDynamicProperties.Of(insert, dynamic))
            {
                if (property.Node == null)
                {
                    continue;
                }

                if (property.Value is string name)
                {
                    if (block.Lookups.Exists(l => l.Id == property.Node))
                    {
                        labels[property.Node] = name;
                    }
                    else
                    {
                        state = name;
                    }
                }
                else if (values.TryGetValue(property.Node, out double[] now) && property.Part < now.Length)
                {
                    double read = property.Value is short flag ? flag : property.Value is double number ? number : double.NaN;

                    if (!double.IsNaN(read))
                    {
                        now[property.Part] = property.UnitsType == DxfDynamicBlockReferencePropertyUnitsType.Distance ? read / scale : read;
                    }
                }
            }

            Param asked = block.Parameters.Find(p => p.Id == node);
            Looked lookup = block.Lookups.Find(l => l.Id == node);

            if (lookup != null)
            {
                string label = value as string ?? throw new ArgumentException("A lookup property is set by the label of a row, a string; a " + value.GetType().Name + " is not one.", nameof(value));
                lookup.Choose(label.Trim(), values, labels, dynamic.Name);
            }
            else if (asked is Linear linear)
            {
                double distance = Number(value);
                double taken = linear.Take(distance, values[node][0] * scale, scale);

                if (!(taken / scale > Tolerance.Default.EqualPoint))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A distance of a linear parameter is above the point tolerance once taken to its value set.");
                }

                values[node][0] = taken / scale;
            }
            else if (asked is Planar)
            {
                double distance = Number(value);

                if (!(distance / scale > Tolerance.Default.EqualPoint))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A distance of an XY parameter is above the point tolerance.");
                }

                values[node][part] = distance / scale;
            }
            else if (asked is Spot)
            {
                values[node][part] = Number(value) / scale;
            }
            else if (asked is Turning)
            {
                values[node][0] = Normal(Number(value));
            }
            else if (asked is Mirror)
            {
                values[node][0] = Flag(value);
            }
            else if (block.Visibility != null && block.Visibility.Id == node)
            {
                string named = value as string ?? throw new ArgumentException("A visibility state is set by its name, a string; a " + value.GetType().Name + " is not one.", nameof(value));
                State found = block.Visibility.States.Find(s => string.Equals(s.Name, named.Trim(), StringComparison.OrdinalIgnoreCase));
                state = found?.Name ?? throw new ArgumentException("The visibility parameter " + block.Visibility.Name + " has no state named " + named + ".", nameof(value));
            }
            else
            {
                throw new NotSupportedException("The property's parameter is not one the library works out.");
            }

            // Each lookup shows the label of the row of the values now, or its label of no match; a lookup set keeps its row.
            foreach (Looked other in block.Lookups)
            {
                if (lookup == null || other.Table != lookup.Table)
                {
                    other.Table.Relabel(values, labels);
                }
            }

            Write(insert, dynamic, block, values, state, labels);

            foreach (DxfDynamicBlockReferenceProperty property in DxfDynamicProperties.Of(insert, dynamic))
            {
                if (property.Node == node && property.Part == part)
                {
                    return property.Value;
                }
            }

            return value;
        }

        /// <summary>A number given as a distance or an angle: a <see cref="double"/>, or a whole or other number taken as one; finite.</summary>
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
                    throw new ArgumentException("A distance or an angle is set as a number; a " + value.GetType().Name + " is not one.", nameof(value));
            }

            if (!Guard.IsFinite(number))
            {
                throw new ArgumentException("A distance or an angle is set as a finite number; " + number.ToString("R", CultureInfo.InvariantCulture) + " is not.", nameof(value));
            }

            return number;
        }

        /// <summary>The state of a flip given: a whole number of 0 (not flipped) or 1 (flipped).</summary>
        private static double Flag(object value)
        {
            long flag;

            switch (value)
            {
                case short s:
                    flag = s;
                    break;
                case int i:
                    flag = i;
                    break;
                case long l:
                    flag = l;
                    break;
                case byte b:
                    flag = b;
                    break;
                default:
                    throw new ArgumentException("A flip state is set as a whole number, 0 or 1; a " + value.GetType().Name + " is not one.", nameof(value));
            }

            if (flag != 0 && flag != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "A flip state is 0, not flipped, or 1, flipped.");
            }

            return flag;
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

        /// <summary>
        /// Makes the INSERT place the block worked out at the values given: its anonymous block filled anew where no other INSERT
        /// places it, else a new one; then its representation data and cache written.
        /// </summary>
        private static void Write(DxfBlockReference insert, DxfBlockTableRecord dynamic, Block block, Dictionary<string, double[]> values, string state, Dictionary<string, string> labels)
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

            Act(block, values, copyOf);

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

            Copy(block, values, copyOf, database, anonymous);

            if (!ReferenceEquals(insert.BlockTableRecord, anonymous))
            {
                insert.BlockTableRecord = anonymous;
            }

            Cache(insert, dynamic, block, values, state, labels);
        }

        /// <summary>
        /// Works the actions out on the copies, in the order of the graph: the stretches in a row first summed point by point
        /// and then applied, as <see cref="Stretch"/> says; a move, a scale, a rotation and a flip applied to each entity whole.
        /// An action whose parameter keeps the block's own value leaves its entities as they are. Arrays are worked out after
        /// them all (<see cref="Copy"/>).
        /// </summary>
        private static void Act(Block block, Dictionary<string, double[]> values, Dictionary<string, DxfEntity> copyOf)
        {
            var stretches = new List<BlockAction>();

            foreach (BlockAction action in block.Actions)
            {
                if (action.Kind == ActionKind.Array)
                {
                    continue;
                }

                if (action.Kind == ActionKind.Stretch)
                {
                    stretches.Add(action);
                    continue;
                }

                Stretch(block, stretches, values, copyOf);
                stretches.Clear();
                GeoTransform3 transform = Transform(block.Parameters.Find(p => p.Id == action.Parameter), action, values);

                if (transform == null)
                {
                    continue;
                }

                foreach (string handle in action.Entities)
                {
                    if (copyOf.TryGetValue(handle, out DxfEntity copy))
                    {
                        copy.TransformBy(transform);
                    }
                }
            }

            Stretch(block, stretches, values, copyOf);
        }

        /// <summary>
        /// What an action of a kind does to its entities at the parameter's value: a move by the change of the point it follows,
        /// times its distance multiplier and turned by its angle offset; a scale about the base point by the distance over the
        /// block's own; a turn about the base point by the angle less the block's own; a mirror about the reflection line where
        /// flipped; null where it changes nothing.
        /// </summary>
        private static GeoTransform3 Transform(Param parameter, BlockAction action, Dictionary<string, double[]> values)
        {
            double[] now = values[parameter.Id];

            switch (action.Kind)
            {
                case ActionKind.Move:
                    double[] move = action.Factored(parameter.Moved(now));
                    return move[0] == 0.0 && move[1] == 0.0 ? null : GeoTransform3.Translation(new GeoVector3(move[0], move[1], 0.0));
                case ActionKind.Scale:
                    double factor = now[0] / parameter.Own[0];
                    return factor == 1.0 ? null : GeoTransform3.Scaling(parameter.Base, factor);
                case ActionKind.Rotate:
                    double angle = now[0] - parameter.Own[0];
                    return angle == 0.0 ? null : GeoTransform3.RotationAxis(parameter.Base, new GeoVector3(0, 0, 1), angle);
                default:
                    if (now[0] != 1.0)
                    {
                        return null;
                    }

                    GeoVector3 along = parameter.Base.GetVectorTo(parameter.End);
                    return GeoTransform3.Mirror(new GeoPlane3(parameter.Base, new GeoVector3(-along.Y, along.X, 0.0)));
            }
        }

        /// <summary>
        /// Moves the points of the copies each stretch holds by its parameter's change: the end point now less the block's own,
        /// in x and y, times the stretch's distance multiplier and turned by its angle offset; a point held by several stretches
        /// moves by them all. A line moves its start or end, a polyline its vertex; an entity of one point moves whole; an arc
        /// held by both ends moves whole, by one end has that end moved, as <see cref="Bend"/> says.
        /// </summary>
        private static void Stretch(Block block, List<BlockAction> stretches, Dictionary<string, double[]> values, Dictionary<string, DxfEntity> copyOf)
        {
            // The moves of each entity's points, by the entity's handle and the point's index; summed in the order of the graph.
            var moves = new Dictionary<string, SortedDictionary<int, double[]>>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (BlockAction stretch in stretches)
            {
                Param parameter = block.Parameters.Find(p => p.Id == stretch.Parameter);
                double[] change = stretch.Factored(parameter.Moved(values[parameter.Id]));
                double dx = change[0];
                double dy = change[1];

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

                if (copy is DxfArc arc)
                {
                    moves[handle].TryGetValue(0, out double[] start);
                    moves[handle].TryGetValue(1, out double[] end);
                    Bend(arc, start ?? new double[2], end ?? new double[2]);
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
                    else
                    {
                        // An entity moved whole: every point it has is held, and moves alike, so the first tells the move.
                        if (move.Value[0] != 0.0 || move.Value[1] != 0.0)
                        {
                            copy.TransformBy(GeoTransform3.Translation(new GeoVector3(move.Value[0], move.Value[1], 0.0)));
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Moves the ends of an arc by the moves given, in x and y: both alike, the arc moves whole; else the arc is drawn anew
        /// through its ends moved, in its plane and its sense, keeping the height of its middle off its chord, as AutoCAD 2021
        /// kept 2.929 for the arc of the radius 10 from (100, 25) to (90, 35) whose start went to (150, 25) and then to (200, 25),
        /// its radius becoming 159.37 and then 522.13 (plugin v20, 24d-k). An arc whose ends would meet within the point
        /// tolerance of <see cref="Tolerance.Default"/> moves whole by the move of its start.
        /// </summary>
        private static void Bend(DxfArc arc, double[] start, double[] end)
        {
            if (start[0] == end[0] && start[1] == end[1])
            {
                if (start[0] != 0.0 || start[1] != 0.0)
                {
                    arc.TransformBy(GeoTransform3.Translation(new GeoVector3(start[0], start[1], 0.0)));
                }

                return;
            }

            GeoPoint3 from = arc.StartPoint;
            GeoPoint3 to = arc.EndPoint;
            GeoPoint3 middle = arc.GetPointAtParameter((arc.StartParam + arc.EndParam) / 2.0);
            double cx = to.X - from.X, cy = to.Y - from.Y;
            double chord = Math.Sqrt(cx * cx + cy * cy);
            var a = new GeoPoint3(from.X + start[0], from.Y + start[1], from.Z);
            var b = new GeoPoint3(to.X + end[0], to.Y + end[1], to.Z);
            double nx = b.X - a.X, ny = b.Y - a.Y;
            double reach = Math.Sqrt(nx * nx + ny * ny);

            if (!(chord > 0.0) || !(reach > Tolerance.Default.EqualPoint))
            {
                arc.TransformBy(GeoTransform3.Translation(new GeoVector3(start[0], start[1], 0.0)));
                return;
            }

            // The height of the middle off the chord, signed to the left of the chord from the start to the end, kept.
            double height = ((middle.X - (from.X + to.X) / 2.0) * -cy + (middle.Y - (from.Y + to.Y) / 2.0) * cx) / chord;
            double half = reach / 2.0;
            double radius = (half * half + height * height) / (2.0 * Math.Abs(height));
            double lx = -ny / reach, ly = nx / reach;
            double off = height - Math.Sign(height) * radius;
            var centre = new GeoPoint3((a.X + b.X) / 2.0 + lx * off, (a.Y + b.Y) / 2.0 + ly * off, from.Z);
            GeoVector3 normal = arc.Normal;
            GeoPoint3 c = DxfEntity.WorldToOcs(normal, centre);
            GeoPoint3 s = DxfEntity.WorldToOcs(normal, a);
            GeoPoint3 e = DxfEntity.WorldToOcs(normal, b);
            arc.Center = centre;
            arc.Radius = radius;
            arc.StartAngle = Math.Atan2(s.Y - c.Y, s.X - c.X);
            arc.EndAngle = Math.Atan2(e.Y - c.Y, e.X - c.X);
        }

        /// <summary>
        /// Copies the entities of each array of the block, after the other actions and the visibility are worked out, into the
        /// anonymous block: along a linear parameter, a copy at each column spacing from the entity along the line from the
        /// base point to the end point, as many in all as the spacing goes whole into the distance, one at least; of an XY
        /// parameter, in rows and columns the way the end point lies, each count the distance along its axis over its spacing,
        /// rounded down, one at least, as AutoCAD's save of ACadSharp's XY sample shows (four columns of 25 in 100.59, five rows
        /// of 20 in 104.30). The entity itself is the first copy; the others follow the block's entities, row by row.
        /// </summary>
        private static void Copy(Block block, Dictionary<string, double[]> values, Dictionary<string, DxfEntity> copyOf, DxfDatabase database, DxfBlockTableRecord anonymous)
        {
            double slack = Tolerance.Default.EqualPoint;

            foreach (BlockAction action in block.Actions)
            {
                if (action.Kind != ActionKind.Array)
                {
                    continue;
                }

                Param parameter = block.Parameters.Find(p => p.Id == action.Parameter);
                double[] now = values[parameter.Id];
                var offsets = new List<GeoVector3>();

                if (parameter is Linear linear)
                {
                    int count = Math.Max(1, (int)Math.Floor((now[0] + slack) / action.Columns));

                    for (int i = 1; i < count; i++)
                    {
                        offsets.Add(new GeoVector3(linear.UnitX * i * action.Columns, linear.UnitY * i * action.Columns, linear.UnitZ * i * action.Columns));
                    }
                }
                else
                {
                    double[] signs = ((Planar)parameter).Signs;
                    int columns = Math.Max(1, (int)Math.Floor((now[0] + slack) / action.Columns));
                    int rows = Math.Max(1, (int)Math.Floor((now[1] + slack) / action.Rows));

                    for (int j = 0; j < rows; j++)
                    {
                        for (int i = 0; i < columns; i++)
                        {
                            if (i > 0 || j > 0)
                            {
                                offsets.Add(new GeoVector3(signs[0] * i * action.Columns, signs[1] * j * action.Rows, 0.0));
                            }
                        }
                    }
                }

                foreach (string handle in action.Entities)
                {
                    if (!copyOf.TryGetValue(handle, out DxfEntity copy))
                    {
                        continue;
                    }

                    foreach (GeoVector3 offset in offsets)
                    {
                        database.WblockCloneObjects(new[] { copy }, anonymous)[0].TransformBy(GeoTransform3.Translation(offset));
                    }
                }
            }
        }

        /// <summary>
        /// The INSERT's extension dictionary as AutoCAD writes it for an INSERT whose value it set: AcDbBlockRepresentation with
        /// the representation data (AcDbRepData) naming the dynamic block, and AppDataCache with a record for each parameter and
        /// action of the graph (ACAD_ENHANCEDBLOCKDATA, by the node's id, in the order of the ids as text), and for a flip
        /// parameter's grip as AutoCAD keeps one, and for each lookup parameter its label; none for an array or a lookup action,
        /// as AutoCAD's samples keep none; the history and its data taken out.
        /// </summary>
        private static void Cache(DxfBlockReference insert, DxfBlockTableRecord dynamic, Block block, Dictionary<string, double[]> values, string state, Dictionary<string, string> labels)
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

            foreach (Param parameter in block.Parameters)
            {
                written.AddRange(parameter.Records(values[parameter.Id]));
            }

            foreach (BlockAction action in block.Actions)
            {
                List<KeyValuePair<int, string>> groups;
                Param parameter = block.Parameters.Find(p => p.Id == action.Parameter);

                switch (action.Kind)
                {
                    case ActionKind.Stretch:
                        groups = Head(StretchKey);
                        groups.Add(new KeyValuePair<int, string>(40, Real(0.0)));
                        break;
                    case ActionKind.Move:
                        groups = Head(MoveKey);
                        groups.Add(new KeyValuePair<int, string>(40, Real(0.0)));
                        break;
                    case ActionKind.Scale:
                        groups = Head(ScaleKey);
                        Point(groups, parameter.Base);
                        break;
                    case ActionKind.Rotate:
                        groups = Head(RotateKey);
                        Point(groups, parameter.Base);
                        break;
                    default:
                        continue;
                }

                written.Add(new KeyValuePair<string, List<KeyValuePair<int, string>>>(action.Id, groups));
            }

            foreach (Looked lookup in block.Lookups)
            {
                List<KeyValuePair<int, string>> groups = Head(LookupKey);
                Point(groups, lookup.Location);
                groups.Add(new KeyValuePair<int, string>(1, labels.TryGetValue(lookup.Id, out string label) ? label : lookup.Table.Label(lookup.Id, values)));
                written.Add(new KeyValuePair<string, List<KeyValuePair<int, string>>>(lookup.Id, groups));
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

        /// <summary>A record of the cache, by the node's id.</summary>
        private static KeyValuePair<string, List<KeyValuePair<int, string>>> Record(string id, List<KeyValuePair<int, string>> groups)
            => new KeyValuePair<string, List<KeyValuePair<int, string>>>(id, groups);

        /// <summary>A record of a parameter of two points: its key, the base point, the end point and the normal down (0, 0, -1), as AutoCAD writes them.</summary>
        private static List<KeyValuePair<int, string>> PointsRecord(string[] key, GeoPoint3 basePoint, GeoPoint3 endPoint)
        {
            List<KeyValuePair<int, string>> groups = Head(key);
            Point(groups, basePoint);
            Point(groups, endPoint);
            Point(groups, new GeoPoint3(0, 0, -1));
            return groups;
        }

        /// <summary>The kinds of action the library works out.</summary>
        private enum ActionKind
        {
            Stretch,
            Move,
            Scale,
            Rotate,
            Flip,
            Array,
        }

        /// <summary>
        /// The parameters and actions of a dynamic block's evaluation graph that the library works out, read from its nodes in
        /// their order; any other node refused with <see cref="NotSupportedException"/>, as is an action it does not work out.
        /// </summary>
        private sealed class Block
        {
            internal List<Param> Parameters { get; } = new List<Param>();

            internal List<BlockAction> Actions { get; } = new List<BlockAction>();

            internal Visible Visibility { get; private set; }

            internal List<Looked> Lookups { get; } = new List<Looked>();

            internal static Block Of(DxfBlockTableRecord dynamic)
            {
                var block = new Block();

                // The node id of each grip by its object's handle, and the lookup tables by their action's node id.
                var grips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var tables = new List<KeyValuePair<string, IReadOnlyList<DxfTypedValue>>>();
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
                    int before = block.Parameters.Count;

                    switch (node.Type)
                    {
                        case "BLOCKLINEARPARAMETER":
                            block.Parameters.Add(Linear.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKXYPARAMETER":
                            block.Parameters.Add(Planar.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKPOINTPARAMETER":
                            block.Parameters.Add(Spot.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKROTATIONPARAMETER":
                            block.Parameters.Add(Turning.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKFLIPPARAMETER":
                            block.Parameters.Add(Mirror.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKSTRETCHACTION":
                            block.Actions.Add(BlockAction.Stretched(groups, id, dynamic.Name));
                            break;
                        case "BLOCKMOVEACTION":
                        case "BLOCKSCALEACTION":
                        case "BLOCKROTATEACTION":
                        case "BLOCKFLIPACTION":
                        case "BLOCKARRAYACTION":
                            block.Actions.Add(BlockAction.Of(node.Type, groups, id, dynamic.Name));
                            break;
                        case "BLOCKVISIBILITYPARAMETER":
                            block.Visibility = Visible.Of(groups, id);
                            break;
                        case "BLOCKLOOKUPPARAMETER":
                            block.Lookups.Add(Looked.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKLOOKUPACTION":
                            tables.Add(new KeyValuePair<string, IReadOnlyList<DxfTypedValue>>(id, groups));
                            break;
                        case "BLOCKLINEARGRIP":
                        case "BLOCKXYGRIP":
                        case "BLOCKROTATIONGRIP":
                        case "BLOCKFLIPGRIP":
                        case "BLOCKVISIBILITYGRIP":
                        case "BLOCKLOOKUPGRIP":
                        case "BLOCKALIGNMENTGRIP":
                            if (id != null)
                            {
                                grips[node.Handle] = id;
                            }

                            break;
                        case "BLOCKGRIPLOCATIONCOMPONENT":
                        case "BLOCKALIGNMENTPARAMETER":
                        case "BLOCKBASEPOINTPARAMETER":
                            break;
                        default:
                            throw new NotSupportedException("The block " + dynamic.Name + " holds a " + node.Type + ", which the library does not work out: it works out linear, XY, point, rotation, flip, visibility and lookup parameters with stretch, move, scale, rotate, flip, array and lookup actions, and alignment and base point parameters.");
                    }

                    if (block.Parameters.Count > before)
                    {
                        block.Parameters[before].Grips = Param.GripsOf(groups);
                    }
                }

                foreach (Looked lookup in block.Lookups)
                {
                    KeyValuePair<string, IReadOnlyList<DxfTypedValue>> table = tables.Find(t => t.Key == lookup.Action);
                    lookup.Table = block.Lookups.Find(l => l.Action == lookup.Action && l.Table != null)?.Table
                        ?? Table.Of(table.Value ?? throw new NotSupportedException("A lookup parameter of the block " + dynamic.Name + " names no lookup action of it."), block, dynamic.Name);
                }

                // A move may list its own parameter's grips among what it moves, as AutoCAD's point sample does: the grips follow
                // the parameter anyway, and are left out.
                foreach (BlockAction action in block.Actions)
                {
                    HashSet<string> own = block.Parameters.Find(p => p.Id == action.Parameter)?.Grips;

                    if (action.Kind == ActionKind.Move && own != null)
                    {
                        action.Entities.RemoveAll(h => grips.TryGetValue(h, out string grip) && own.Contains(grip));
                    }
                }

                var entities = new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfEntity entity in dynamic)
                {
                    entities[entity.Handle] = entity;
                }

                foreach (BlockAction action in block.Actions)
                {
                    action.Check(block, entities, dynamic.Name);
                }

                // An entity a flip mirrors and another action moves is not worked out: AutoCAD 2021 stretched such an entity
                // from where its last flip left it, and refused a flip of it (plugin v20, 24d-i).
                var flipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                block.Actions.FindAll(a => a.Kind == ActionKind.Flip).ForEach(a => flipped.UnionWith(a.Entities));

                foreach (BlockAction action in block.Actions)
                {
                    if (action.Kind != ActionKind.Flip && (action.Entities.Exists(flipped.Contains) || action.Held.Exists(h => flipped.Contains(h.Key))))
                    {
                        throw new NotSupportedException("The block " + dynamic.Name + " flips an entity another action moves, which AutoCAD 2021 worked out from the INSERT's last state rather than from the block: the library does not work it out.");
                    }
                }

                // An entity an array copies and another action works on is not worked out: no sample shows the order they take.
                foreach (BlockAction array in block.Actions.FindAll(a => a.Kind == ActionKind.Array))
                {
                    foreach (BlockAction action in block.Actions)
                    {
                        if (action != array && (action.Entities.Exists(array.Entities.Contains) || action.Held.Exists(h => array.Entities.Contains(h.Key))))
                        {
                            throw new NotSupportedException("The block " + dynamic.Name + " copies by an array an entity another action works on, an order no sample of AutoCAD shows: the library does not work it out.");
                        }
                    }
                }

                return block;
            }
        }

        /// <summary>
        /// A parameter the library works out: its node's id, its points in the block's coordinates, and its own value (the
        /// block's), part by part.
        /// </summary>
        private abstract class Param
        {
            internal string Id { get; set; }

            internal GeoPoint3 Base { get; set; }

            internal GeoPoint3 End { get; set; }

            internal double[] Own { get; set; }

            /// <summary>The node ids of the parameter's grips.</summary>
            internal HashSet<string> Grips { get; set; }

            /// <summary>The node ids of the grips a parameter lists: the 91s of a parameter of two points, the 93 of one of one point; nought left out.</summary>
            internal static HashSet<string> GripsOf(IReadOnlyList<DxfTypedValue> groups)
            {
                var grips = new HashSet<string>(StringComparer.Ordinal);
                DxfDynamicProperties.Section(groups, "AcDbBlock2PtParameter").FindAll(g => g.TypeCode == 91).ForEach(g => grips.Add(g.Value?.Trim()));
                DxfDynamicProperties.Section(groups, "AcDbBlock1PtParameter").FindAll(g => g.TypeCode == 93).ForEach(g => grips.Add(g.Value?.Trim()));
                grips.Remove("0");
                grips.Remove(null);
                return grips;
            }

            /// <summary>The move, in x and y, of the point a stretch or a move follows, at the value given; none for a parameter no stretch or move follows.</summary>
            internal virtual double[] Moved(double[] now) => new double[2];

            /// <summary>The records of the cache the parameter gives, at the value given.</summary>
            internal abstract IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now);

            /// <summary>The base point and the end point of a parameter of two points, read; refused where they cannot be read or its distance is measured from its middle.</summary>
            protected static void TwoPoints(Param parameter, IReadOnlyList<DxfTypedValue> groups, string id, string kind, string block)
            {
                List<DxfTypedValue> two = DxfDynamicProperties.Section(groups, "AcDbBlock2PtParameter");
                double[] from = DxfDynamicProperties.PointOf(two, 1010);
                double[] to = DxfDynamicProperties.PointOf(two, 1011);
                string location = DxfDynamicProperties.Text(two, 177)?.Trim();

                if (id == null || from == null || to == null || (location != null && location != "0"))
                {
                    throw new NotSupportedException("A " + kind + " parameter of the block " + block + " is not one the library works out: its points cannot be read, or its distance is measured from its middle.");
                }

                parameter.Id = id;
                parameter.Base = new GeoPoint3(from[0], from[1], from[2]);
                parameter.End = new GeoPoint3(to[0], to[1], to[2]);
            }

            /// <summary>Whether a value set of flags, a minimum and a maximum is of none: no increment, no list, a minimum of nought or less and a maximum of the bound or more where they are given.</summary>
            protected static bool None(int flags, double minimum, double maximum, double bound)
            {
                double slack = Tolerance.Default.EqualVector;
                return (flags & 12) == 0 && ((flags & 1) == 0 || minimum <= slack) && ((flags & 2) == 0 || maximum >= bound - slack);
            }

            /// <summary>A whole number of a code of a subclass; nought where there is none.</summary>
            internal static int FlagsOf(List<DxfTypedValue> own, int code)
                => int.TryParse(DxfDynamicProperties.Text(own, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int flags) ? flags : 0;

            /// <summary>A real of a code of a subclass; the value given where there is none.</summary>
            internal static double RealOf(List<DxfTypedValue> own, int code, double none)
                => DxfDynamicProperties.Number(DxfDynamicProperties.Text(own, code), out double number) ? number : none;
        }

        /// <summary>A linear parameter: its node's id, its base point, the unit direction to its end point, its distance and its value set.</summary>
        private sealed class Linear : Param
        {
            private int _flags;
            private double _minimum;
            private double _maximum;
            private double _increment;
            private List<double> _list = new List<double>();

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

                var linear = new Linear
                {
                    Id = id, Base = new GeoPoint3(from[0], from[1], from[2]), End = new GeoPoint3(to[0], to[1], to[2]), Distance = length, UnitX = dx / length, UnitY = dy / length, UnitZ = dz / length,
                    Own = new[] { length },
                };
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

            /// <summary>The move of the end point along the line from the base point: the distance now less the block's own.</summary>
            internal override double[] Moved(double[] now)
            {
                double change = now[0] - Distance;
                return new[] { UnitX * change, UnitY * change };
            }

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now)
            {
                double distance = now[0];
                var end = new GeoPoint3(Base.X + UnitX * distance, Base.Y + UnitY * distance, Base.Z + UnitZ * distance);
                yield return Record(Id, PointsRecord(LinearKey, Base, end));
            }

            /// <summary>
            /// A value, in the INSERT's units (the block's scaled), taken to the value set as AutoCAD 2021 took it: a list (flags
            /// 8) to its nearest value, a tie to the one farther from the value the property had, beyond the value asked in the
            /// way it changes, then to the least (AutoCAD 2021 took 8 of the 11 ties of plugin v20 so, 125 to 100 from 300 and 225
            /// to 250 from 50, and was not consistent over the rounds before); else to the nearest step of the increment from
            /// the minimum (flags 4), a tie to the step below, then into the minimum (flags 1) and the maximum (flags 2). Ties
            /// within the point tolerance of <see cref="Tolerance.Default"/>.
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

                        if (nearer > slack || (Math.Abs(nearer) <= slack && Math.Abs(now - candidate) > Math.Abs(now - best) + slack))
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

                    // Nearer the step below, nearer the one above, or between them within the tolerance: then the one below, as
                    // AutoCAD 2021 took 85 to 80, 65 to 60 and 75 to 70 (plugin v19).
                    double chosen = gap <= step - gap + slack ? below : below + 1.0;
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

        /// <summary>An XY parameter: its distances along x and along y, each measured from the base point the way the end point lies.</summary>
        private sealed class Planar : Param
        {
            private double _signX;
            private double _signY;

            internal static Planar Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var planar = new Planar();
                TwoPoints(planar, groups, id, "XY", block);
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockXYParameter");
                double dx = planar.End.X - planar.Base.X, dy = planar.End.Y - planar.Base.Y;

                if (!None(FlagsOf(own, 96), RealOf(own, 142, 0.0), 0.0, double.PositiveInfinity) || !None(FlagsOf(own, 97), RealOf(own, 146, 0.0), 0.0, double.PositiveInfinity))
                {
                    throw new NotSupportedException("An XY parameter of the block " + block + " has a value set, which the library does not work out.");
                }

                if (!(Math.Abs(dx) > 0.0) || !(Math.Abs(dy) > 0.0))
                {
                    throw new NotSupportedException("An XY parameter of the block " + block + " has its end point level with its base point along x or along y.");
                }

                planar._signX = dx > 0.0 ? 1.0 : -1.0;
                planar._signY = dy > 0.0 ? 1.0 : -1.0;
                planar.Own = new[] { Math.Abs(dx), Math.Abs(dy) };
                return planar;
            }

            /// <summary>The way the end point lies from the base point along x and along y, 1 or -1 each.</summary>
            internal double[] Signs => new[] { _signX, _signY };

            /// <summary>The move of the end point: each distance now less the block's own, the way the end point lies.</summary>
            internal override double[] Moved(double[] now) => new[] { _signX * (now[0] - Own[0]), _signY * (now[1] - Own[1]) };

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now)
            {
                var end = new GeoPoint3(Base.X + _signX * now[0], Base.Y + _signY * now[1], End.Z);
                yield return Record(Id, PointsRecord(XYKey, Base, end));
            }
        }

        /// <summary>A point parameter: its position's x and y.</summary>
        private sealed class Spot : Param
        {
            internal static Spot Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                double[] at = DxfDynamicProperties.PointOf(DxfDynamicProperties.Section(groups, "AcDbBlock1PtParameter"), 1010);

                if (id == null || at == null)
                {
                    throw new NotSupportedException("A point parameter of the block " + block + " is not one the library works out: its position cannot be read.");
                }

                var point = new GeoPoint3(at[0], at[1], at[2]);
                return new Spot { Id = id, Base = point, End = point, Own = new[] { at[0], at[1] } };
            }

            /// <summary>The move of the position: where it is now less the block's own.</summary>
            internal override double[] Moved(double[] now) => new[] { now[0] - Own[0], now[1] - Own[1] };

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now)
            {
                List<KeyValuePair<int, string>> groups = Head(PointKey);
                Point(groups, new GeoPoint3(now[0], now[1], Base.Z));
                yield return Record(Id, groups);
            }
        }

        /// <summary>A rotation parameter: the angle of its end point about its base point, from its base angle point.</summary>
        private sealed class Turning : Param
        {
            private double _radius;
            private double _from;
            private GeoPoint3 _baseAngle;

            internal static Turning Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var turning = new Turning();
                TwoPoints(turning, groups, id, "rotation", block);
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockRotationParameter");
                double[] basis = DxfDynamicProperties.PointOf(own, 1011);

                if (basis == null || !None(FlagsOf(own, 96), RealOf(own, 141, 0.0), RealOf(own, 142, 0.0), Turn))
                {
                    throw new NotSupportedException("A rotation parameter of the block " + block + " is not one the library works out: its base angle cannot be read, or it has a value set.");
                }

                double dx = turning.End.X - turning.Base.X, dy = turning.End.Y - turning.Base.Y;
                turning._radius = Math.Sqrt(dx * dx + dy * dy);

                if (!(turning._radius > 0.0))
                {
                    throw new NotSupportedException("A rotation parameter of the block " + block + " has its end point on its base point.");
                }

                turning._baseAngle = new GeoPoint3(basis[0], basis[1], basis[2]);
                turning._from = Math.Atan2(basis[1] - turning.Base.Y, basis[0] - turning.Base.X);
                turning.Own = new[] { Normal(Math.Atan2(dy, dx) - turning._from) };
                return turning;
            }

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now)
            {
                double angle = _from + now[0];
                var end = new GeoPoint3(Base.X + _radius * Math.Cos(angle), Base.Y + _radius * Math.Sin(angle), End.Z);
                List<KeyValuePair<int, string>> groups = PointsRecord(RotationKey, Base, end);
                Point(groups, _baseAngle);
                yield return Record(Id, groups);
            }
        }

        /// <summary>A flip parameter: its reflection line, and the id of its grip, whose state AutoCAD keeps in the cache too.</summary>
        private sealed class Mirror : Param
        {
            private string _grip;

            internal static Mirror Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var mirror = new Mirror();
                TwoPoints(mirror, groups, id, "flip", block);
                mirror._grip = DxfDynamicProperties.Text(DxfDynamicProperties.Section(groups, "AcDbBlock2PtParameter"), 91)?.Trim();

                if (!(Math.Abs(mirror.End.X - mirror.Base.X) + Math.Abs(mirror.End.Y - mirror.Base.Y) > 0.0))
                {
                    throw new NotSupportedException("A flip parameter of the block " + block + " has its reflection line of no length along x and y.");
                }

                mirror.Own = new[] { 0.0 };
                return mirror;
            }

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now)
            {
                string state = now[0] == 1.0 ? "1" : "0";
                List<KeyValuePair<int, string>> groups = PointsRecord(FlipKey, Base, End);
                groups.Add(new KeyValuePair<int, string>(70, state));
                yield return Record(Id, groups);

                if (_grip != null && _grip != "0")
                {
                    List<KeyValuePair<int, string>> grip = Head(FlipGripKey);
                    grip.Add(new KeyValuePair<int, string>(70, "0"));
                    grip.Add(new KeyValuePair<int, string>(70, state));
                    yield return Record(_grip, grip);
                }
            }
        }

        /// <summary>
        /// An action: its node's id, its kind, the parameter it follows, and what it works on: the points a stretch holds, by
        /// the entity's handle, or the entities another action moves whole.
        /// </summary>
        private sealed class BlockAction
        {
            internal string Id { get; private set; }

            internal ActionKind Kind { get; private set; }

            internal string Parameter { get; private set; }

            internal List<KeyValuePair<string, List<int>>> Held { get; } = new List<KeyValuePair<string, List<int>>>();

            internal List<string> Entities { get; } = new List<string>();

            /// <summary>The distance multiplier of a stretch or a move (140); 1 for the others.</summary>
            internal double Multiplier { get; private set; } = 1.0;

            /// <summary>The angle offset of a stretch or a move (141), in radians; nought for the others.</summary>
            internal double Offset { get; private set; }

            /// <summary>The column offset of an array (141).</summary>
            internal double Columns { get; private set; }

            /// <summary>The row offset of an array (140).</summary>
            internal double Rows { get; private set; }

            /// <summary>A move in x and y times the multiplier and turned by the offset, counter-clockwise.</summary>
            internal double[] Factored(double[] move)
            {
                if (Multiplier == 1.0 && Offset == 0.0)
                {
                    return move;
                }

                double cos = Math.Cos(Offset), sin = Math.Sin(Offset);
                return new[] { Multiplier * (cos * move[0] - sin * move[1]), Multiplier * (sin * move[0] + cos * move[1]) };
            }

            // The connections and the numbers read, checked against the parameter once the graph is read.
            private string _x;
            private string _y;
            private string _xBy;
            private string _yBy;
            private int _xyType;

            internal static BlockAction Stretched(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var stretch = new BlockAction { Id = id, Kind = ActionKind.Stretch };
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockStretchAction");
                bool entities = false;
                double multiplier = 1.0, angle = 0.0;
                int parameters = 0;

                foreach (DxfTypedValue group in own)
                {
                    string text = group.Value?.Trim();

                    switch (group.TypeCode)
                    {
                        case 92:
                            stretch._x = text;
                            break;
                        case 93:
                            stretch._y = text;
                            break;
                        case 301:
                            stretch._xBy = text;
                            break;
                        case 302:
                            stretch._yBy = text;
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
                        case 280:
                            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out stretch._xyType);
                            break;
                    }
                }

                double slack = Tolerance.Default.EqualVector;

                if (id == null || stretch._x == null || stretch._x != stretch._y || stretch._xBy != "EndXDelta" || stretch._yBy != "EndYDelta" || parameters != 0)
                {
                    throw new NotSupportedException("A stretch of the block " + block + " is not one the library works out: it works out a stretch by the end point of a linear or an XY parameter, moving no other parameter.");
                }

                stretch.Parameter = stretch._x;
                stretch.Multiplier = Math.Abs(multiplier - 1.0) <= slack ? 1.0 : multiplier;
                stretch.Offset = Math.Abs(angle) <= slack ? 0.0 : angle;
                return stretch;
            }

            /// <summary>A move, a scale, a rotate or a flip action: the parameter it follows, its connections, and the entities it works on.</summary>
            internal static BlockAction Of(string type, IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var action = new BlockAction { Id = id };
                List<DxfTypedValue> head = DxfDynamicProperties.Section(groups, "AcDbBlockAction");
                bool chained = (DxfDynamicProperties.Text(head, 70)?.Trim() ?? "0") != "0";

                foreach (DxfTypedValue group in head)
                {
                    if (group.TypeCode == 330)
                    {
                        action.Entities.Add(group.Value?.Trim());
                    }
                }

                double slack = Tolerance.Default.EqualVector;
                bool known;

                if (type == "BLOCKMOVEACTION")
                {
                    action.Kind = ActionKind.Move;
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockMoveAction");
                    action._x = DxfDynamicProperties.Text(own, 92)?.Trim();
                    action._y = DxfDynamicProperties.Text(own, 93)?.Trim();
                    action._xBy = DxfDynamicProperties.Text(own, 301)?.Trim();
                    action._yBy = DxfDynamicProperties.Text(own, 302)?.Trim();
                    action._xyType = Param.FlagsOf(own, 280);
                    double multiplier = Param.RealOf(own, 140, 1.0), angle = Param.RealOf(own, 141, 0.0);
                    action.Multiplier = Math.Abs(multiplier - 1.0) <= slack ? 1.0 : multiplier;
                    action.Offset = Math.Abs(angle) <= slack ? 0.0 : angle;
                    known = true;
                }
                else if (type == "BLOCKARRAYACTION")
                {
                    action.Kind = ActionKind.Array;
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockArrayAction");
                    action._x = DxfDynamicProperties.Text(own, 92)?.Trim();
                    action._y = DxfDynamicProperties.Text(own, 93)?.Trim();
                    action._xBy = DxfDynamicProperties.Text(own, 301)?.Trim();
                    action._yBy = DxfDynamicProperties.Text(own, 302)?.Trim();
                    action.Rows = Param.RealOf(own, 140, 0.0);
                    action.Columns = Param.RealOf(own, 141, 0.0);
                    known = action._xBy == "Base" && action._yBy == "End" && action.Columns > Tolerance.Default.EqualPoint
                        && DxfDynamicProperties.Text(own, 94)?.Trim() == action._x && DxfDynamicProperties.Text(own, 95)?.Trim() == action._x;
                }
                else if (type == "BLOCKFLIPACTION")
                {
                    action.Kind = ActionKind.Flip;
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockFlipAction");
                    action._x = DxfDynamicProperties.Text(own, 92)?.Trim();
                    action._y = DxfDynamicProperties.Text(own, 93)?.Trim();
                    action._xBy = DxfDynamicProperties.Text(own, 301)?.Trim();
                    known = action._xBy == "Flip";
                }
                else
                {
                    // A scale or a rotate: its base the parameter's base point, dependent on it, of no offset and 1012 nought, as
                    // AutoCAD 2021 adds 1012 to the parameter's base point (plugin v20).
                    bool scale = type == "BLOCKSCALEACTION";
                    action.Kind = scale ? ActionKind.Scale : ActionKind.Rotate;
                    List<DxfTypedValue> basis = DxfDynamicProperties.Section(groups, "AcDbBlockActionWithBasePt");
                    List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, scale ? "AcDbBlockScaleAction" : "AcDbBlockRotationAction");
                    double[] offset = DxfDynamicProperties.PointOf(basis, 1011);
                    double[] added = DxfDynamicProperties.PointOf(basis, 1012) ?? new double[3];
                    offset = offset == null ? null : new[] { Math.Abs(offset[0]) + Math.Abs(added[0]), Math.Abs(offset[1]) + Math.Abs(added[1]), Math.Abs(offset[2]) + Math.Abs(added[2]) };
                    action._x = DxfDynamicProperties.Text(basis, 92)?.Trim();
                    action._y = DxfDynamicProperties.Text(own, 94)?.Trim();
                    action._xBy = DxfDynamicProperties.Text(basis, 301)?.Trim();
                    action._yBy = DxfDynamicProperties.Text(own, 303)?.Trim();
                    known = DxfDynamicProperties.Text(basis, 93)?.Trim() == action._x && action._xBy == "UpdatedBaseX" && action._yBy == (scale ? "Scale" : "AngleDelta")
                        && Param.FlagsOf(basis, 280) == 1 && offset != null && Math.Abs(offset[0]) + Math.Abs(offset[1]) + Math.Abs(offset[2]) <= slack;
                }

                if (id == null || action._x == null || action._x != action._y || chained || !known)
                {
                    throw new NotSupportedException("A " + type + " of the block " + block + " is not one the library works out: it works out a move, a scale and a rotate about the parameter's base point, a flip and an array of a column offset, each of entities only.");
                }

                action.Parameter = action._x;
                return action;
            }

            /// <summary>
            /// Refuses an action whose parameter is not of the kind it follows, or that works on what is not an entity of the
            /// block the library works out: a stretch of a linear or an XY parameter, of lines, polylines and entities of one
            /// point by their points, and of arcs by both ends or, lying flat, by one; a move of a linear or an XY parameter by
            /// its end point, of a point parameter by its position; a scale of a linear parameter, a rotate of a rotation
            /// parameter, a flip of a flip parameter, an array of a linear parameter or of an XY parameter of a row offset.
            /// </summary>
            internal void Check(Block block, Dictionary<string, DxfEntity> entities, string name)
            {
                Param parameter = block.Parameters.Find(p => p.Id == Parameter);
                bool end = parameter is Linear || parameter is Planar;
                bool fits;

                switch (Kind)
                {
                    case ActionKind.Stretch:
                        fits = end && (_xyType == 0 || !(parameter is Planar));
                        break;
                    case ActionKind.Move:
                        fits = _xyType == 0 && ((end && _xBy == "EndXDelta" && _yBy == "EndYDelta") || (parameter is Spot && _xBy == "XDelta" && _yBy == "YDelta"));
                        break;
                    case ActionKind.Scale:
                        fits = parameter is Linear;
                        break;
                    case ActionKind.Rotate:
                        fits = parameter is Turning;
                        break;
                    case ActionKind.Array:
                        fits = parameter is Linear || (parameter is Planar && Rows > Tolerance.Default.EqualPoint);
                        break;
                    default:
                        fits = parameter is Mirror;
                        break;
                }

                if (!fits)
                {
                    throw new NotSupportedException("A " + Kind.ToString().ToLowerInvariant() + " action of the block " + name + " follows a parameter or a connection the library does not work out for it.");
                }

                foreach (string handle in Entities)
                {
                    if (Kind != ActionKind.Stretch && (!entities.TryGetValue(handle, out DxfEntity entity) || entity is DxfUnknownEntity || entity is DxfAttributeDefinition))
                    {
                        throw new NotSupportedException("A " + Kind.ToString().ToLowerInvariant() + " action of the block " + name + " works on " + handle + ", which is not an entity of the block the library works out: it may be another parameter or a grip, moved with it.");
                    }
                }

                foreach (KeyValuePair<string, List<int>> held in Held)
                {
                    entities.TryGetValue(held.Key, out DxfEntity entity);
                    List<GeoPoint3> points = entity == null ? null : DxfBlockTableRecord.StretchPoints(entity);
                    int count = points?.Count ?? 0;
                    bool whole = entity is DxfLine || entity is DxfPolyline || held.Value.Count == count || (entity is DxfArc arc && DxfBlockTableRecord.Flat(arc.Normal));

                    if (count == 0 || held.Value.Exists(i => i < 0 || i >= count) || !whole)
                    {
                        throw new NotSupportedException("A stretch of the block " + name + " holds the entity " + held.Key + ", which is not one the library works out with the points it names: lines and polylines by their points, arcs by both ends or, lying flat, by one, circles, texts, multiline texts and INSERTs by their one point.");
                    }
                }
            }
        }

        /// <summary>A lookup parameter: its node's id, its location, the id of its lookup action, and the table of that action.</summary>
        private sealed class Looked
        {
            internal string Id { get; private set; }

            internal GeoPoint3 Location { get; private set; }

            internal string Action { get; private set; }

            internal Table Table { get; set; }

            internal static Looked Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                double[] at = DxfDynamicProperties.PointOf(DxfDynamicProperties.Section(groups, "AcDbBlock1PtParameter"), 1010);
                string action = DxfDynamicProperties.Text(DxfDynamicProperties.Section(groups, "AcDbBlockLookUpParameter"), 94)?.Trim();

                if (id == null || at == null || action == null)
                {
                    throw new NotSupportedException("A lookup parameter of the block " + block + " is not one the library works out: its location or its action cannot be read.");
                }

                return new Looked { Id = id, Location = new GeoPoint3(at[0], at[1], at[2]), Action = action };
            }

            /// <summary>Sets the inputs and the lookups of the table to the row of a label of this lookup's column, as <see cref="Table.Choose"/> says.</summary>
            internal void Choose(string label, Dictionary<string, double[]> values, Dictionary<string, string> labels, string block) => Table.Choose(this, label, values, labels, block);
        }

        /// <summary>
        /// The table of a lookup action, as AutoCAD writes it: its cells row by row, and its columns, each of a parameter's node,
        /// looked up (282 of 1) or an input, its label of no match (305) and its connection (304). The library works out input
        /// columns of a linear parameter's distance (UpdatedDistance) and of a point parameter's x and y (UpdatedX, UpdatedY),
        /// of reals in the block's units, and looked-up columns of a lookup parameter (lookupString).
        /// </summary>
        private sealed class Table
        {
            private readonly List<string[]> _rows = new List<string[]>();
            private readonly List<Column> _columns = new List<Column>();

            internal static Table Of(IReadOnlyList<DxfTypedValue> groups, Block block, string name)
            {
                var table = new Table();
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockLookupAction");
                int rows = Param.FlagsOf(own, 92), width = Param.FlagsOf(own, 93);
                var cells = new List<string>();

                foreach (DxfTypedValue group in own)
                {
                    if (group.TypeCode == 302 && table._columns.Count == 0)
                    {
                        cells.Add(DxfReader.Decode(group.Value ?? string.Empty).Trim());
                    }
                    else if (group.TypeCode == 303)
                    {
                        table._columns.Add(new Column());
                    }
                    else if (table._columns.Count > 0)
                    {
                        Column column = table._columns[table._columns.Count - 1];

                        switch (group.TypeCode)
                        {
                            case 94:
                                column.Node = group.Value?.Trim();
                                break;
                            case 282:
                                column.LookedUp = group.Value?.Trim() == "1";
                                break;
                            case 305:
                                column.Unmatched = DxfReader.Decode(group.Value ?? string.Empty);
                                break;
                            case 304:
                                column.Connection = group.Value?.Trim();
                                break;
                        }
                    }
                }

                if (rows <= 0 || width <= 0 || width != table._columns.Count || cells.Count != rows * width)
                {
                    throw new NotSupportedException("A lookup table of the block " + name + " cannot be read whole.");
                }

                for (int r = 0; r < rows; r++)
                {
                    table._rows.Add(cells.GetRange(r * width, width).ToArray());
                }

                for (int c = 0; c < width; c++)
                {
                    Column column = table._columns[c];
                    Param parameter = block.Parameters.Find(p => p.Id == column.Node);
                    bool known = column.LookedUp
                        ? column.Connection == "lookupString" && block.Lookups.Exists(l => l.Id == column.Node)
                        : (parameter is Linear && column.Connection == "UpdatedDistance") || (parameter is Spot && (column.Connection == "UpdatedX" || column.Connection == "UpdatedY"));

                    if (!known || (!column.LookedUp && table._rows.Exists(r => !DxfDynamicProperties.Number(r[c], out _))))
                    {
                        throw new NotSupportedException("A lookup table of the block " + name + " has a column the library does not work out: it works out inputs of a linear parameter's distance and of a point parameter's x and y, of numbers, and lookup parameters looked up.");
                    }

                    column.Part = column.Connection == "UpdatedY" ? 1 : 0;
                }

                return table;
            }

            /// <summary>
            /// The label a lookup, by its node's id, shows at the values given: its cell of the first row whose inputs are the values, within the
            /// point tolerance of <see cref="Tolerance.Default"/>; else its column's label of no match.
            /// </summary>
            internal string Label(string lookup, Dictionary<string, double[]> values)
            {
                int column = _columns.FindIndex(c => c.LookedUp && c.Node == lookup);

                if (column < 0)
                {
                    return string.Empty;
                }

                foreach (string[] row in _rows)
                {
                    bool all = true;

                    for (int c = 0; c < _columns.Count && all; c++)
                    {
                        if (!_columns[c].LookedUp)
                        {
                            DxfDynamicProperties.Number(row[c], out double cell);
                            all = Math.Abs(cell - values[_columns[c].Node][_columns[c].Part]) <= Tolerance.Default.EqualPoint;
                        }
                    }

                    if (all)
                    {
                        return row[column];
                    }
                }

                return _columns[column].Unmatched;
            }

            /// <summary>Gives each lookup of the table the label of the values given.</summary>
            internal void Relabel(Dictionary<string, double[]> values, Dictionary<string, string> labels)
            {
                foreach (Column column in _columns)
                {
                    if (column.LookedUp)
                    {
                        labels[column.Node] = Label(column.Node, values);
                    }
                }
            }

            /// <summary>
            /// Sets each input of the table to its cell of the first row whose cell of the lookup's column is the label given,
            /// letters of either case the same, and each lookup of the table to its cell of that row; refuses a label of no row.
            /// </summary>
            internal void Choose(Looked lookup, string label, Dictionary<string, double[]> values, Dictionary<string, string> labels, string block)
            {
                int column = _columns.FindIndex(c => c.LookedUp && c.Node == lookup.Id);
                string[] chosen = column < 0 ? null : _rows.Find(r => string.Equals(r[column], label, StringComparison.OrdinalIgnoreCase));

                if (chosen == null)
                {
                    throw new ArgumentException("The lookup table of the block " + block + " has no row labelled " + label + ".", "value");
                }

                for (int c = 0; c < _columns.Count; c++)
                {
                    if (_columns[c].LookedUp)
                    {
                        labels[_columns[c].Node] = chosen[c];
                    }
                    else
                    {
                        DxfDynamicProperties.Number(chosen[c], out double cell);
                        values[_columns[c].Node][_columns[c].Part] = cell;
                    }
                }
            }

            /// <summary>A column of the table.</summary>
            private sealed class Column
            {
                internal string Node { get; set; }

                internal bool LookedUp { get; set; }

                internal string Unmatched { get; set; } = string.Empty;

                internal string Connection { get; set; }

                internal int Part { get; set; }
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
