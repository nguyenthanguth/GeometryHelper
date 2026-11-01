using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Sets the value of a dynamic property of an INSERT as AutoCAD's <c>DynamicBlockReferenceProperty.Value</c> sets it, for a
    /// dynamic block of linear, XY, polar, point, rotation, flip, visibility and lookup parameters with the stretch, polar
    /// stretch, move, scale, rotate, flip, array and lookup actions the library writes, and the stretches and moves of a base grip, the moves
    /// carrying linear parameters and the attribute definitions in a stretch, a move, a scale and a rotate that AutoCAD's Block
    /// Editor writes: the value taken to its value set, the block worked out at the values of all its properties into an
    /// anonymous block of AutoCAD's (*U) the INSERT places, the INSERT's ATTRIBs changed as their ATTDEFs' places change, and the INSERT's representation data and
    /// cache (ACAD_ENHANCEDBLOCKDATA) written as AutoCAD 2021 wrote them when it set a value (plugins v17, v18), a history of
    /// grip edits the INSERT keeps given the value set after it and none made where it keeps none (plugin D26). A block
    /// properties table sets its row, its user parameters and the INSERT's attributes of its columns.
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
        private static readonly string[] PolarKey = { "11323277", "15401870" };
        private static readonly string[] PolarStretchKey = { "82519608", "111249311" };
        private static readonly string[] UserKey = { "6894073", "9375021" };

        // A tolerance of nought: two transformations agree only to the bit.
        private static readonly Tolerance Exactly = new Tolerance(0.0, 0.0);

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
            var users = new Dictionary<string, object>(StringComparer.Ordinal);
            string state = null;

            foreach (Param parameter in block.Parameters)
            {
                values[parameter.Id] = (double[])parameter.Own.Clone();
            }

            foreach (User user in block.Users)
            {
                users[user.Id] = user.Own;
            }

            foreach (DxfDynamicBlockReferenceProperty property in DxfDynamicProperties.Of(insert, dynamic))
            {
                if (property.Node == null)
                {
                    continue;
                }

                if (users.ContainsKey(property.Node))
                {
                    users[property.Node] = property.Value;
                }
                else if (property.Value is string name)
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

            // A base point a grip moved: AutoCAD keeps it where the INSERT saved it and works the actions from there, drawing the
            // array of its XY sample's INSERT 2AE, whose base point a grip took from (0, 5) to (0, 104.30), at y 99.30 down to
            // 19.30; the library works a parameter out from the block's own base point, and would draw it at 0 down to -80. A
            // base point a move carried, with the parameter, stands where the move took it from the block's own.
            Dictionary<string, double[]> bases = DxfDynamicProperties.SavedBases(insert);
            Dictionary<string, double[]> carried = Carried(block, values);

            foreach (Param parameter in block.Parameters)
            {
                if (!(parameter is Spot) && bases.TryGetValue(parameter.Id, out double[] saved) && !new GeoPoint3(saved[0], saved[1], saved[2]).IsEqualTo(parameter.BaseNow(carried), Tolerance.Default))
                {
                    throw new NotSupportedException("The INSERT " + insert.Handle + " keeps a base point of a parameter of the block " + dynamic.Name + " away from the block's own, as a grip moved it: the library works a block out from its own base points, and does not set it.");
                }
            }

            var before = new Dictionary<string, double[]>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, double[]> now in values)
            {
                before.Add(now.Key, (double[])now.Value.Clone());
            }

            Param asked = block.Parameters.Find(p => p.Id == node);
            Looked lookup = block.Lookups.Find(l => l.Id == node);
            User given = block.Users.Find(u => u.Id == node);
            bool row = block.Sheet != null && block.Sheet.Id == node;
            var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // A table to match takes a parameter set to a row, as Sheet.Match says; a lookup or a user parameter set, or a
            // table of a column of a user parameter or of the visibility state, is not worked out so.
            bool matched = block.Sheet != null && block.Sheet.MustMatch && !row;

            if (matched && (lookup != null || given != null || block.Sheet.HasUsers))
            {
                throw new NotSupportedException("The block " + dynamic.Name + " has a block properties table its properties must match: the library sets its row and the parameters of its columns, not a lookup or a user parameter of it, nor a table of a column of a user parameter or of the visibility state, whose working the library was not shown.");
            }

            if (row)
            {
                long index = Whole(value);

                if (index < 0 || index >= block.Sheet.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A block properties table is set to the index of one of its rows, from 0 up to " + (block.Sheet.Count - 1).ToString(CultureInfo.InvariantCulture) + ", as AutoCAD 2021 refused any other (eInvalidInput).");
                }

                block.Sheet.Choose((int)index, values, users, texts, block.Visibility, ref state);
            }
            else if (given != null)
            {
                users[node] = given.IsText ? value as string ?? throw new ArgumentException("A user parameter of text is set by a string; a " + value.GetType().Name + " is not one.", nameof(value)) : (object)Number(value);
            }
            else if (lookup != null)
            {
                string label = value as string ?? throw new ArgumentException("A lookup property is set by the label of a row, a string; a " + value.GetType().Name + " is not one.", nameof(value));
                lookup.Choose(label, values, labels, dynamic.Name);
            }
            else if (asked is Linear linear)
            {
                double distance = Number(value);
                double taken = linear.Take(distance, scale);

                if (!(taken / scale > Tolerance.Default.EqualPoint))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A distance of a linear parameter is above the point tolerance once taken to its value set.");
                }

                values[node][0] = taken / scale;
            }
            else if (asked is Planar planar)
            {
                double distance = planar.Take(part, Number(value), scale);

                if (!(distance / scale > Tolerance.Default.EqualPoint))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A distance of an XY parameter is above the point tolerance once taken to its value set.");
                }

                values[node][part] = distance / scale;
            }
            else if (asked is Polar polar)
            {
                if (part == 0)
                {
                    double distance = polar.Take(0, Number(value), scale);

                    if (!(distance / scale > Tolerance.Default.EqualPoint))
                    {
                        throw new ArgumentOutOfRangeException(nameof(value), value, "A distance of a polar parameter is above the point tolerance once taken to its value set.");
                    }

                    values[node][0] = distance / scale;
                }
                else
                {
                    values[node][1] = polar.Take(1, Normal(Number(value)), scale);
                }
            }
            else if (asked is Spot)
            {
                values[node][part] = Number(value) / scale;
            }
            else if (asked is Turning turning)
            {
                values[node][0] = turning.Take(Normal(Number(value)));
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

            if (matched)
            {
                block.Sheet.Match(node, part, values);
            }

            // A property set leaving no row of the table matched gives each user parameter of a column its value of no match, as
            // AutoCAD 2021 read Spaces Custom once Width was set off the rows of its Parking sample (plugin D26); a user parameter
            // set keeps the value given.
            if (block.Sheet != null && !row && given == null)
            {
                block.Sheet.Unmatch(values, users, state);
            }

            // Each lookup shows the label of the first row of the values now, or its label of no match, the lookup set too, as
            // AutoCAD 2021 read Small after setting Twin, a row of Small's values further down, and South after setting Down, a
            // row of -pi / 2 (plugin D25b); a lookup set of a table of no input keeps its row.
            foreach (Looked other in block.Lookups)
            {
                if (lookup == null || other.Table != lookup.Table || other.Table.HasInputs)
                {
                    other.Table.Relabel(values, labels);
                }
            }

            List<KeyValuePair<DxfAttributeReference, GeoTransform3>> follow = Followed(insert, dynamic, block, before, values);
            Write(insert, dynamic, block, values, state, labels, users);

            foreach (KeyValuePair<DxfAttributeReference, GeoTransform3> attribute in follow)
            {
                attribute.Key.TransformBy(attribute.Value);
            }

            // A row sets the attribute of each attribute definition of a column to its text, as AutoCAD 2021 gave MARK P200.
            foreach (DxfAttributeReference attribute in insert.AttributeCollection)
            {
                if (texts.TryGetValue(attribute.Tag ?? string.Empty, out string text))
                {
                    attribute.TextString = text;
                }
            }

            foreach (DxfDynamicBlockReferenceProperty property in DxfDynamicProperties.Of(insert, dynamic))
            {
                if (property.Node == node && property.Part == part)
                {
                    Remember(insert, property);
                    return property.Value;
                }
            }

            return value;
        }

        /// <summary>
        /// Adds the property set, as read back, to the history of grip edits the INSERT keeps (ACAD_ENHANCEDBLOCKHISTORY), where
        /// it keeps one: the node's id (1071), the property's name (300) and its value, a real (40), a whole number (70) or a
        /// text (1), as AutoCAD 2021 added Block Table1 2, Spaces 5 Spaces and Width 5400 to its Parking sample's INSERT (plugin
        /// D26). An INSERT of no history is given none, as AutoCAD gave none.
        /// </summary>
        private static void Remember(DxfBlockReference insert, DxfDynamicBlockReferenceProperty property)
        {
            DxfDBDictionary cache = DxfDynamicProperties.Entry(DxfDynamicProperties.Entry(insert.ExtensionDictionary, "AcDbBlockRepresentation") as DxfDBDictionary, "AppDataCache") as DxfDBDictionary;

            if (!(DxfDynamicProperties.Entry(cache, "ACAD_ENHANCEDBLOCKHISTORY") is DxfXrecord history))
            {
                return;
            }

            DxfTypedValue kept;

            switch (property.Value)
            {
                case double number:
                    kept = new DxfTypedValue(40, Real(number));
                    break;
                case short flag:
                    kept = new DxfTypedValue(70, flag.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    kept = new DxfTypedValue(1, Convert.ToString(property.Value, CultureInfo.InvariantCulture));
                    break;
            }

            history.AppendKept(new[] { new DxfTypedValue(1071, property.Node), new DxfTypedValue(300, property.PropertyName), kept });
        }

        /// <summary>A whole number given as a row's index: a <see cref="short"/>, an <see cref="int"/>, a <see cref="long"/> or a <see cref="byte"/>.</summary>
        private static long Whole(object value)
        {
            switch (value)
            {
                case short s:
                    return s;
                case int i:
                    return i;
                case long l:
                    return l;
                case byte b:
                    return b;
                default:
                    throw new ArgumentException("A block properties table is set by the index of a row, a whole number; a " + value.GetType().Name + " is not one.", nameof(value));
            }
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
        /// The move, in x and y, an action of a stretch or a move gives at the values given: the change of the point it follows
        /// times its distance multiplier and turned by its angle offset; none for one following a base point, which a value set
        /// does not move.
        /// </summary>
        private static double[] Change(Block block, BlockAction action, Dictionary<string, double[]> values)
        {
            if (action.ByBase)
            {
                return new double[2];
            }

            Param parameter = block.Parameters.Find(p => p.Id == action.Parameter);
            return action.Factored(parameter.Moved(values[parameter.Id]));
        }

        /// <summary>
        /// The move, in x and y, each parameter a move carries is given at the values given, by the parameter's id: the sum of
        /// the moves of the actions carrying it, each as <see cref="Change"/> gives it. A parameter carried keeps its distance,
        /// and its own actions do not follow its being carried (it does not chain them).
        /// </summary>
        private static Dictionary<string, double[]> Carried(Block block, Dictionary<string, double[]> values)
        {
            var carried = new Dictionary<string, double[]>(StringComparer.Ordinal);

            foreach (BlockAction action in block.Actions)
            {
                if (action.Carried.Count == 0)
                {
                    continue;
                }

                double[] change = Change(block, action, values);

                foreach (string id in action.Carried)
                {
                    if (!carried.TryGetValue(id, out double[] sum))
                    {
                        sum = new double[2];
                        carried.Add(id, sum);
                    }

                    sum[0] += change[0];
                    sum[1] += change[1];
                }
            }

            return carried;
        }

        /// <summary>
        /// The ATTRIBs of the INSERT that follow their ATTDEF, told by its tag, as the actions take it from where the values
        /// before put it to where the values now put it, with what each is given in the world: the change of the ATTDEF's place
        /// in the block carried by the INSERT's placing. A move or a stretch moves it; a scale or a rotate scales or turns it
        /// about the parameter's base point, as AutoCAD 2021 scaled and turned the ATTRIBs of the library's (plugin D30, x2). An
        /// ATTRIB keeps a place the user gave it and changes by the change only. An ATTDEF not locked in the block takes no ATTRIB
        /// along, as AutoCAD 2021 left them (x1). Refused with <see cref="NotSupportedException"/> where ATTDEFs of one tag the
        /// INSERT has an ATTRIB of change apart.
        /// </summary>
        private static List<KeyValuePair<DxfAttributeReference, GeoTransform3>> Followed(DxfBlockReference insert, DxfBlockTableRecord dynamic, Block block, Dictionary<string, double[]> before, Dictionary<string, double[]> values)
        {
            var follow = new List<KeyValuePair<DxfAttributeReference, GeoTransform3>>();

            if (insert.AttributeCollection.Count == 0)
            {
                return follow;
            }

            // The change of each tag's ATTDEF, in the block: a move along x and y, or, where a scale or a rotate holds it, a
            // transformation (turned); a tag of ATTDEFs that change apart holds neither.
            var changes = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            var turns = new Dictionary<string, GeoTransform3>(StringComparer.OrdinalIgnoreCase);
            var apart = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            double slack = Tolerance.Default.EqualPoint;

            foreach (DxfEntity entity in dynamic)
            {
                if (!(entity is DxfAttributeDefinition definition))
                {
                    continue;
                }

                var change = new double[2];
                GeoTransform3 turned = null;
                bool locked = definition.LockPositionInBlock;

                if (locked && block.Actions.Exists(a => (a.Kind == ActionKind.Scale || a.Kind == ActionKind.Rotate) && Holds(a, entity.Handle)))
                {
                    GeoTransform3 now = Placed(block, entity.Handle, values);
                    GeoTransform3 then = Placed(block, entity.Handle, before);
                    turned = now.IsEqualTo(then, Exactly) ? GeoTransform3.Identity : now.Multiply(then.Inverse(Tolerance.Default));
                }
                else if (locked)
                {
                    foreach (BlockAction action in block.Actions)
                    {
                        if ((action.Kind == ActionKind.Move || action.Kind == ActionKind.Stretch) && Holds(action, entity.Handle))
                        {
                            double[] from = Change(block, action, before);
                            double[] to = Change(block, action, values);
                            change[0] += to[0] - from[0];
                            change[1] += to[1] - from[1];
                        }
                    }
                }

                string tag = definition.Tag ?? string.Empty;

                if (!changes.ContainsKey(tag) && !turns.ContainsKey(tag) && !apart.Contains(tag))
                {
                    if (turned != null)
                    {
                        turns.Add(tag, turned);
                    }
                    else
                    {
                        changes.Add(tag, change);
                    }
                }
                else if (!apart.Contains(tag))
                {
                    GeoTransform3 seen = turns.TryGetValue(tag, out GeoTransform3 kept) ? kept : null;
                    bool agree = turned == null && seen == null
                        ? Math.Abs(changes[tag][0] - change[0]) <= slack && Math.Abs(changes[tag][1] - change[1]) <= slack
                        : Agree(seen ?? GeoTransform3.Translation(new GeoVector3(changes[tag][0], changes[tag][1], 0.0)), turned ?? GeoTransform3.Translation(new GeoVector3(change[0], change[1], 0.0)));

                    if (!agree)
                    {
                        changes.Remove(tag);
                        turns.Remove(tag);
                        apart.Add(tag);
                    }
                }
            }

            GeoTransform3 placing = insert.BlockTransform;
            GeoPoint3 origin = placing.Transform(new GeoPoint3(0, 0, 0));

            foreach (DxfAttributeReference attribute in insert.AttributeCollection)
            {
                string tag = attribute.Tag ?? string.Empty;

                if (apart.Contains(tag))
                {
                    throw new NotSupportedException("The block " + dynamic.Name + " has attribute definitions of the tag " + attribute.Tag + " that its actions move apart: the INSERT's attribute of that tag has no one move to follow, and is not set.");
                }

                if (turns.TryGetValue(tag, out GeoTransform3 turned))
                {
                    if (!turned.IsIdentity(Exactly))
                    {
                        follow.Add(new KeyValuePair<DxfAttributeReference, GeoTransform3>(attribute, placing.Multiply(turned).Multiply(placing.Inverse(Tolerance.Default))));
                    }
                }
                else if (changes.TryGetValue(tag, out double[] change) && (change[0] != 0.0 || change[1] != 0.0))
                {
                    follow.Add(new KeyValuePair<DxfAttributeReference, GeoTransform3>(attribute, GeoTransform3.Translation(origin.GetVectorTo(placing.Transform(new GeoPoint3(change[0], change[1], 0.0))))));
                }
            }

            return follow;
        }

        /// <summary>Whether an action works on an entity of the block: among its entities, or, of a stretch, among those it holds.</summary>
        private static bool Holds(BlockAction action, string handle)
            => action.Entities.Exists(h => string.Equals(h, handle, StringComparison.OrdinalIgnoreCase))
            || (action.Kind == ActionKind.Stretch && action.Held.Exists(h => string.Equals(h.Key, handle, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// What the actions of the block give an entity of one point at the values given, in the order of the graph, as
        /// <see cref="Act"/> works them out: a stretch and a move move it, a scale scales it and a rotate turns it about the
        /// parameter's base point.
        /// </summary>
        private static GeoTransform3 Placed(Block block, string handle, Dictionary<string, double[]> values)
        {
            GeoTransform3 placed = GeoTransform3.Identity;

            foreach (BlockAction action in block.Actions)
            {
                if (!Holds(action, handle))
                {
                    continue;
                }

                GeoTransform3 step;

                if (action.Kind == ActionKind.Stretch)
                {
                    double[] change = Change(block, action, values);
                    step = GeoTransform3.Translation(new GeoVector3(change[0], change[1], 0.0));
                }
                else if (action.Kind == ActionKind.Move || action.Kind == ActionKind.Scale || action.Kind == ActionKind.Rotate)
                {
                    step = Transform(block.Parameters.Find(p => p.Id == action.Parameter), action, values);
                }
                else
                {
                    step = null;
                }

                if (step != null)
                {
                    placed = step.Multiply(placed);
                }
            }

            return placed;
        }

        /// <summary>Whether two transformations agree: the origin carried within the point tolerance, the axes within the vector tolerance of <see cref="Tolerance.Default"/>.</summary>
        private static bool Agree(GeoTransform3 a, GeoTransform3 b)
        {
            var origin = new GeoPoint3(0, 0, 0);

            if (!a.Transform(origin).IsEqualTo(b.Transform(origin), Tolerance.Default))
            {
                return false;
            }

            foreach (GeoVector3 axis in new[] { GeoVector3.XAxis, GeoVector3.YAxis, GeoVector3.ZAxis })
            {
                if (!a.Transform(axis).IsEqualTo(b.Transform(axis), Tolerance.Default))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Makes the INSERT place the block worked out at the values given: its anonymous block filled anew where no other INSERT
        /// places it, else a new one; then its representation data and cache written.
        /// </summary>
        private static void Write(DxfBlockReference insert, DxfBlockTableRecord dynamic, Block block, Dictionary<string, double[]> values, string state, Dictionary<string, string> labels, Dictionary<string, object> users)
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

            // A dimension that names no block is a dimension of the block's constraints, which AutoCAD keeps hidden in its own
            // anonymous copies (Parking's six read as hidden, plugin D26b): its copy, which keeps no tie to the constraint, is
            // hidden, as AutoCAD 2021 then read it, where it drew one left shown.
            foreach (DxfEntity copy in copies)
            {
                if (copy is DxfDimension dimension && dimension.PictureName.Length == 0)
                {
                    copy.Visible = false;
                }
            }

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

            Cache(insert, dynamic, block, values, state, labels, users);
        }

        /// <summary>
        /// Works the actions out on the copies, in the order of the graph: the stretches in a row first summed point by point
        /// and then applied, as <see cref="Stretch"/> says; a move, a scale, a rotation and a flip applied to each entity whole,
        /// but an attribute definition not locked in the block, which a stretch alone moves. An action whose parameter keeps
        /// the block's own value leaves its entities as they are. Arrays are worked out after them all (<see cref="Copy"/>).
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

                if (action.Kind == ActionKind.PolarStretch)
                {
                    PolarStretch((Polar)block.Parameters.Find(p => p.Id == action.Parameter), action, values, copyOf);
                    continue;
                }

                GeoTransform3 transform = Transform(block.Parameters.Find(p => p.Id == action.Parameter), action, values);

                if (transform == null)
                {
                    continue;
                }

                // An attribute definition not locked in the block stays, as AutoCAD 2021 left one of a move, a scale and a rotate
                // (plugin D30, x1).
                foreach (string handle in action.Entities)
                {
                    if (copyOf.TryGetValue(handle, out DxfEntity copy) && !(copy is DxfAttributeDefinition definition && !definition.LockPositionInBlock))
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
                    double[] move = action.ByBase ? new double[2] : action.Factored(parameter.Moved(now));
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
                double[] change = Change(block, stretch, values);
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
        /// Works a polar stretch out on the copies at the values given, as AutoCAD 2021 worked out the library's (plugin D25):
        /// each point the stretch holds (a line's start or end, a polyline's vertex) turns about the parameter's base point by
        /// the angle less the block's own and then moves along the parameter's line by the distance less the block's own, as
        /// the end point does; the other points of the entities it holds turn alone, so a line from the base point to the end
        /// point keeps its start and follows with its end. An entity of one point held, or an arc held by both ends, and an
        /// entity of its selection holding no point (a grip of AutoCAD's own sample) are turned and moved whole; an entity it
        /// turns only (331) and does not select is turned whole: AutoCAD 2021 turned a tick it named so about the base point,
        /// and turned and moved it too when the selection named it besides.
        /// </summary>
        private static void PolarStretch(Polar parameter, BlockAction action, Dictionary<string, double[]> values, Dictionary<string, DxfEntity> copyOf)
        {
            double[] now = values[parameter.Id];
            GeoTransform3 full = parameter.Turned(now, false);
            GeoTransform3 turn = parameter.Turned(now, true);

            if (full == null)
            {
                return;
            }

            var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, List<int>> entity in action.Held)
            {
                held.Add(entity.Key);

                if (!copyOf.TryGetValue(entity.Key, out DxfEntity copy))
                {
                    continue;
                }

                if (copy is DxfLine line)
                {
                    GeoTransform3 start = entity.Value.Contains(0) ? full : turn;
                    GeoTransform3 end = entity.Value.Contains(1) ? full : turn;
                    GeoPoint3 from = line.StartPoint, to = line.EndPoint;
                    line.StartPoint = start == null ? from : start.Transform(from);
                    line.EndPoint = end == null ? to : end.Transform(to);
                }
                else if (copy is DxfPolyline polyline)
                {
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        GeoTransform3 move = entity.Value.Contains(i) ? full : turn;

                        if (move != null)
                        {
                            GeoPoint3 inPlane = DxfEntity.WorldToOcs(polyline.Normal, move.Transform(polyline.GetPoint3dAt(i)));
                            polyline.SetPointAt(i, new GeoPoint2(inPlane.X, inPlane.Y));
                        }
                    }
                }
                else
                {
                    copy.TransformBy(full);
                }
            }

            foreach (string handle in action.Entities)
            {
                if (!held.Contains(handle) && copyOf.TryGetValue(handle, out DxfEntity copy))
                {
                    copy.TransformBy(full);
                    held.Add(handle);
                }
            }

            foreach (string handle in action.Turned)
            {
                if (turn != null && !held.Contains(handle) && copyOf.TryGetValue(handle, out DxfEntity copy))
                {
                    copy.TransformBy(turn);
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
        /// rounded down, as AutoCAD's save of ACadSharp's XY sample shows (four columns of 25 in 100.59, five rows of 20 in
        /// 104.30), and none copied where a count is nought, as AutoCAD 2021 drew the one circle of that sample set to 80 by 5
        /// (plugin v21, 24e-s2). The entity itself is the first copy; the others follow the block's entities cell by cell, row
        /// by row, each cell's in the order of the action's selection, and the arrays in the order of the graph, as AutoCAD 2021
        /// drew them (C0 L0 C60 L60 ..., not C0 L0 C60 C120 ... L60, plugin D30), each tagged as AutoCAD tags a copy of its
        /// arrays (AcDbBlockArrayActionTag, 1070 1, after the entity's own tag), without which AutoCAD 2021 refused to set the
        /// INSERT again (eInvalidInput, plugin v21, 24e-c and 24e-d).
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
                    int columns = (int)Math.Floor((now[0] + slack) / action.Columns);
                    int rows = (int)Math.Floor((now[1] + slack) / action.Rows);

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

                foreach (GeoVector3 offset in offsets)
                {
                    foreach (string handle in action.Entities)
                    {
                        if (!copyOf.TryGetValue(handle, out DxfEntity copy))
                        {
                            continue;
                        }

                        DxfEntity copied = database.WblockCloneObjects(new[] { copy }, anonymous)[0];
                        copied.TransformBy(GeoTransform3.Translation(offset));
                        copied.XData = new[] { new DxfTypedValue(1001, "AcDbBlockArrayActionTag"), new DxfTypedValue(1070, "1") };
                    }
                }
            }
        }

        /// <summary>
        /// The INSERT's extension dictionary as AutoCAD writes it for an INSERT whose value it set: AcDbBlockRepresentation with
        /// the representation data (AcDbRepData) naming the dynamic block, and AppDataCache with a record for each parameter and
        /// action of the graph (ACAD_ENHANCEDBLOCKDATA, by the node's id, in the order of the ids as text), and for a flip
        /// parameter's grip as AutoCAD keeps one, and for each lookup parameter its label; none for an array or a lookup action,
        /// as AutoCAD's samples keep none; a record for each user parameter, its location and its value (1 a text, 40 a real),
        /// as AutoCAD 2021 kept Spaces of its Parking sample (plugin D26), and none for a block properties table, whose row is
        /// read from the values; the history of grip edits and its data kept as they are, as AutoCAD 2021 kept them (checks
        /// v22, D26). The record of a stretch or a move holds its angle
        /// offset (40), as AutoCAD 2021 kept 1.5707963267948966 for a move of a quarter turn, and moved along x, unturned, the
        /// circle of an INSERT whose record held nought (plugin v21, 24e-b).
        /// </summary>
        private static void Cache(DxfBlockReference insert, DxfBlockTableRecord dynamic, Block block, Dictionary<string, double[]> values, string state, Dictionary<string, string> labels, Dictionary<string, object> users)
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
            DxfDBDictionary records = Child(cache, "ACAD_ENHANCEDBLOCKDATA");
            var written = new List<KeyValuePair<string, List<KeyValuePair<int, string>>>>();
            Dictionary<string, double[]> carried = Carried(block, values);

            foreach (Param parameter in block.Parameters)
            {
                written.AddRange(parameter.Records(values[parameter.Id], carried.TryGetValue(parameter.Id, out double[] shift) ? shift : null));
            }

            foreach (BlockAction action in block.Actions)
            {
                List<KeyValuePair<int, string>> groups;
                Param parameter = block.Parameters.Find(p => p.Id == action.Parameter);

                switch (action.Kind)
                {
                    case ActionKind.Stretch:
                        groups = Head(StretchKey);
                        groups.Add(new KeyValuePair<int, string>(40, Real(action.Offset)));
                        break;
                    case ActionKind.Move:
                        groups = Head(MoveKey);
                        groups.Add(new KeyValuePair<int, string>(40, Real(action.Offset)));
                        break;
                    case ActionKind.Scale:
                        groups = Head(ScaleKey);
                        Point(groups, parameter.Base);
                        break;
                    case ActionKind.Rotate:
                        groups = Head(RotateKey);
                        Point(groups, parameter.Base);
                        break;
                    case ActionKind.PolarStretch:
                        groups = Head(PolarStretchKey);
                        groups.Add(new KeyValuePair<int, string>(40, Real(action.Offset)));
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

            foreach (User user in block.Users)
            {
                List<KeyValuePair<int, string>> groups = Head(UserKey);
                Point(groups, user.Location);
                object now = users.TryGetValue(user.Id, out object given) ? given : user.Own;
                groups.Add(now is double real ? new KeyValuePair<int, string>(40, Real(real)) : new KeyValuePair<int, string>(1, Convert.ToString(now, CultureInfo.InvariantCulture)));
                written.Add(new KeyValuePair<string, List<KeyValuePair<int, string>>>(user.Id, groups));
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
            PolarStretch,
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

            /// <summary>The user parameters a block properties table made.</summary>
            internal List<User> Users { get; } = new List<User>();

            /// <summary>The block properties table; null where the block has none.</summary>
            internal Sheet Sheet { get; private set; }

            internal static Block Of(DxfBlockTableRecord dynamic)
            {
                var block = new Block();

                // The node id of each grip by its object's handle, the lookup tables by their action's node id, and the block
                // properties tables by their node id.
                var grips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var tables = new List<KeyValuePair<string, IReadOnlyList<DxfTypedValue>>>();
                var sheets = new List<KeyValuePair<string, IReadOnlyList<DxfTypedValue>>>();
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
                        case "BLOCKPOLARPARAMETER":
                            block.Parameters.Add(Polar.Of(groups, id, dynamic.Name));
                            break;
                        case "BLOCKSTRETCHACTION":
                            block.Actions.Add(BlockAction.Stretched(groups, id, dynamic.Name));
                            break;
                        case "BLOCKPOLARSTRETCHACTION":
                            block.Actions.Add(BlockAction.PolarStretched(groups, id, dynamic.Name));
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
                        case "BLOCKPROPERTIESTABLE":
                            sheets.Add(new KeyValuePair<string, IReadOnlyList<DxfTypedValue>>(id, groups));
                            break;
                        case "BLOCKUSERPARAMETER":
                            block.Users.Add(User.Of(groups, id, file, dynamic.Name));
                            break;
                        case "BLOCKLINEARGRIP":
                        case "BLOCKXYGRIP":
                        case "BLOCKROTATIONGRIP":
                        case "BLOCKFLIPGRIP":
                        case "BLOCKVISIBILITYGRIP":
                        case "BLOCKLOOKUPGRIP":
                        case "BLOCKALIGNMENTGRIP":
                        case "BLOCKPOLARGRIP":
                            if (id != null)
                            {
                                grips[node.Handle] = id;
                            }

                            break;
                        case "BLOCKGRIPLOCATIONCOMPONENT":
                        case "BLOCKALIGNMENTPARAMETER":
                        case "BLOCKBASEPOINTPARAMETER":
                        case "BLOCKPROPERTIESTABLEGRIP":
                        case "ACDB_DYNAMICBLOCKPROXYNODE":
                            break;
                        default:
                            throw new NotSupportedException("The block " + dynamic.Name + " holds a " + node.Type + ", which the library does not work out: it works out linear, XY, polar, point, rotation, flip, visibility and lookup parameters with stretch, polar stretch, move, scale, rotate, flip, array and lookup actions, alignment and base point parameters, and a block properties table with the user parameters it makes.");
                    }

                    if (block.Parameters.Count > before)
                    {
                        block.Parameters[before].Grips = Param.GripsOf(groups);
                        block.Parameters[before].Handle = node.Handle;
                        block.Parameters[before].Chains = Param.FlagsOf(DxfDynamicProperties.Section(groups, "AcDbBlockParameter"), 281) != 0;
                    }
                }

                foreach (Looked lookup in block.Lookups)
                {
                    KeyValuePair<string, IReadOnlyList<DxfTypedValue>> table = tables.Find(t => t.Key == lookup.Action);
                    lookup.Table = block.Lookups.Find(l => l.Action == lookup.Action && l.Table != null)?.Table
                        ?? Table.Of(table.Value ?? throw new NotSupportedException("A lookup parameter of the block " + dynamic.Name + " names no lookup action of it."), block, dynamic.Name);
                }

                // A move or a polar stretch may list its own parameter's grips among what it moves, as AutoCAD's point sample and
                // its Annotation sample's polar stretch do: the grips follow the parameter anyway, and are left out.
                foreach (BlockAction action in block.Actions)
                {
                    HashSet<string> own = block.Parameters.Find(p => p.Id == action.Parameter)?.Grips;

                    if ((action.Kind == ActionKind.Move || action.Kind == ActionKind.PolarStretch) && own != null)
                    {
                        action.Entities.RemoveAll(h => grips.TryGetValue(h, out string grip) && own.Contains(grip));
                    }
                }

                // A move may carry other parameters, as a move drawn in AutoCAD's Block Editor over a parameter does: each a linear
                // parameter other than its own, listed among what it moves, its grips following it and left out with it; a
                // parameter that chains its actions (its own following its being carried), or that a scale or an array follows, is
                // not worked out, as no sample of AutoCAD shows the order it takes.
                foreach (BlockAction action in block.Actions)
                {
                    foreach (string id in action.Carried)
                    {
                        Param carried = block.Parameters.Find(p => p.Id == id);

                        if (!(carried is Linear) || carried.Id == action.Parameter || carried.Chains || !action.Entities.Exists(h => string.Equals(h, carried.Handle, StringComparison.OrdinalIgnoreCase))
                            || block.Actions.Exists(a => a.Parameter == id && a.Kind != ActionKind.Stretch && a.Kind != ActionKind.Move))
                        {
                            throw new NotSupportedException("A move of the block " + dynamic.Name + " carries a parameter the library does not work out carried: it works out a linear parameter, other than the move's own, held by the move, of no chained actions, followed by stretches and moves only.");
                        }

                        action.Entities.RemoveAll(h => string.Equals(h, carried.Handle, StringComparison.OrdinalIgnoreCase) || (grips.TryGetValue(h, out string grip) && carried.Grips.Contains(grip)));
                    }
                }

                var entities = new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfEntity entity in dynamic)
                {
                    entities[entity.Handle] = entity;
                }

                if (sheets.Count > 1)
                {
                    throw new NotSupportedException("The block " + dynamic.Name + " has two block properties tables, where AutoCAD's Block Editor gives a block one.");
                }

                if (sheets.Count == 1)
                {
                    block.Sheet = Sheet.Of(sheets[0].Key, sheets[0].Value, block, entities, dynamic.Name);
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

                // An entity a rotate or a scale works on and a stretch or a move holds is not worked out: AutoCAD 2021 stretched
                // such an entity from where its last set left it (plugin v21, 24e-g).
                var turned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                block.Actions.FindAll(a => a.Kind == ActionKind.Rotate || a.Kind == ActionKind.Scale).ForEach(a => turned.UnionWith(a.Entities));

                foreach (BlockAction action in block.Actions)
                {
                    if ((action.Kind == ActionKind.Stretch || action.Kind == ActionKind.Move) && (action.Entities.Exists(turned.Contains) || action.Held.Exists(h => turned.Contains(h.Key))))
                    {
                        throw new NotSupportedException("The block " + dynamic.Name + " turns or scales an entity a stretch or a move holds, which AutoCAD 2021 worked out from the INSERT's last state rather than from the block: the library does not work it out.");
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

                // An entity a polar stretch turns and another action works on is not worked out: no sample shows the order they take.
                foreach (BlockAction polar in block.Actions.FindAll(a => a.Kind == ActionKind.PolarStretch))
                {
                    var own = new HashSet<string>(polar.Entities, StringComparer.OrdinalIgnoreCase);
                    own.UnionWith(polar.Turned);
                    polar.Held.ForEach(h => own.Add(h.Key));

                    foreach (BlockAction action in block.Actions)
                    {
                        if (action != polar && (action.Entities.Exists(own.Contains) || action.Turned.Exists(own.Contains) || action.Held.Exists(h => own.Contains(h.Key))))
                        {
                            throw new NotSupportedException("The block " + dynamic.Name + " turns by a polar stretch an entity another action works on, an order no sample of AutoCAD shows: the library does not work it out.");
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

            /// <summary>The handle of the parameter's node.</summary>
            internal string Handle { get; set; }

            /// <summary>Whether the parameter chains its actions (281 of AcDbBlockParameter): moved by another action, its own follow.</summary>
            internal bool Chains { get; set; }

            /// <summary>The base point now: the block's own, moved by what the moves carrying the parameter give it.</summary>
            internal GeoPoint3 BaseNow(Dictionary<string, double[]> carried)
                => carried.TryGetValue(Id, out double[] shift) ? new GeoPoint3(Base.X + shift[0], Base.Y + shift[1], Base.Z) : Base;

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

            /// <summary>The records of the cache the parameter gives, at the value given, carried by the move given (in x and y), or by none where it is null.</summary>
            internal abstract IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now, double[] carried);

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
            private Allowed _set;

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
                linear._set = Allowed.Of(DxfDynamicProperties.Section(groups, "AcDbBlockLinearParameter"), 96, 141, 175);
                return linear;
            }

            /// <summary>The move of the end point along the line from the base point: the distance now less the block's own.</summary>
            internal override double[] Moved(double[] now)
            {
                double change = now[0] - Distance;
                return new[] { UnitX * change, UnitY * change };
            }

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now, double[] carried)
            {
                double distance = now[0];
                GeoPoint3 from = carried == null ? Base : new GeoPoint3(Base.X + carried[0], Base.Y + carried[1], Base.Z);
                var end = new GeoPoint3(from.X + UnitX * distance, from.Y + UnitY * distance, from.Z + UnitZ * distance);
                yield return Record(Id, PointsRecord(LinearKey, from, end));
            }

            /// <summary>A value, in the INSERT's units (the block's scaled), taken to the value set, as <see cref="Allowed.Distance"/> says.</summary>
            internal double Take(double value, double scale) => _set.Distance(value, scale, Own[0] * scale);
        }

        /// <summary>
        /// A value set of a parameter, as AutoCAD keeps one: its flags (1 a minimum, 2 a maximum, 4 an increment, 8 a list), its
        /// minimum, maximum and increment (three codes in a row) and its list (counted by a code of its own, each of the code
        /// after the increment's), kept from the least up and as it is written.
        /// </summary>
        private sealed class Allowed
        {
            private int _flags;
            private double _minimum;
            private double _maximum;
            private double _increment;
            private readonly List<double> _list = new List<double>();
            private readonly List<double> _written = new List<double>();

            /// <summary>The set of a subclass of a parameter, of the codes of its flags, its minimum and its count.</summary>
            internal static Allowed Of(List<DxfTypedValue> own, int flags, int first, int count)
            {
                var set = new Allowed();
                int listed = -1;

                foreach (DxfTypedValue group in own)
                {
                    double number;

                    if (group.TypeCode == flags)
                    {
                        int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out set._flags);
                    }
                    else if (group.TypeCode == first)
                    {
                        set._minimum = DxfDynamicProperties.Number(group.Value, out number) ? number : 0.0;
                    }
                    else if (group.TypeCode == first + 1)
                    {
                        set._maximum = DxfDynamicProperties.Number(group.Value, out number) ? number : 0.0;
                    }
                    else if (group.TypeCode == first + 2)
                    {
                        set._increment = DxfDynamicProperties.Number(group.Value, out number) ? number : 0.0;
                    }
                    else if (group.TypeCode == count)
                    {
                        int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out listed);
                    }
                    else if (group.TypeCode == first + 3 && listed > set._list.Count && DxfDynamicProperties.Number(group.Value, out number))
                    {
                        set._list.Add(number);
                    }
                }

                set._written.AddRange(set._list);
                set._list.Sort();
                return set;
            }

            /// <summary>
            /// A distance of a linear, an XY or a polar parameter, in the INSERT's units (the block's scaled), taken to the value
            /// set as AutoCAD 2021 took it, from the block's own distance (in the INSERT's units) whatever the value before: a list
            /// (flags 8) to its nearest value, a tie to the block's own where it is one of the two, else to the one written first,
            /// the lesser of a list written from the least up, as AutoCAD 2021 took all 358 ties of lists of 3 to 6 values of
            /// plugin D25b (278 of linear parameters, 47 of an XY and 33 of polar ones), from every value before and in two
            /// sessions alike: 75 to 100, 125 to 100 and 175 to 150 of 50, 100 ... 300 for an own 100, 125 to 100 and 225 to 250
            /// for an own 250, and 125 to 150 of the same list written from 300 down for an own 200; the block's own value is
            /// one of the list where the list was written without it, as AutoCAD 2021 took 120, 125 and 75 to the own 100 of the
            /// list 50, 150, 200, and 250 to 300 of the list written 300, 200, 50 (plugin D26b); else to the nearest step of
            /// the increment from the minimum (flags 4), a tie to the step below, 95 to 90 for an own 100 as AutoCAD took all 100
            /// such ties, then into the minimum (flags 1) and the maximum (flags 2). Ties within the point tolerance of
            /// <see cref="Tolerance.Default"/>.
            /// </summary>
            internal double Distance(double value, double scale, double own)
            {
                double slack = Tolerance.Default.EqualPoint;

                if ((_flags & 8) != 0 && _written.Count > 0)
                {
                    // AutoCAD starts from the block's own value, listed or not, and takes a value of the list, in the order it is
                    // written, only where it is nearer the value asked.
                    int start = _written.FindIndex(v => Math.Abs(v * scale - own) <= slack);
                    double best = start < 0 ? own : _written[start] * scale;

                    foreach (double listed in _written)
                    {
                        double candidate = listed * scale;

                        if (Math.Abs(value - best) - Math.Abs(value - candidate) > slack)
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

            /// <summary>
            /// An angle, taken into a turn, taken to the value set round the circle, as AutoCAD 2021 took the angles of a rotation
            /// and a polar parameter (plugin D25): a list (flags 8) to the value nearest round the circle, 338 degrees to 0 of 0, 90,
            /// 180 and 270; else an angle outside the minimum (flags 1) and the maximum (flags 2) to the one of them nearer round
            /// the circle, 300 degrees to 0 and 200 to 90 of 0 to 90, and one within them to the nearest step of the increment from
            /// the minimum (flags 4), 40 to 45 of steps of 15, and into them; a tie, within the vector tolerance of
            /// <see cref="Tolerance.Default"/>, to the one behind the angle asked, clockwise, as <see cref="Tie"/> says. The block's
            /// own angle is one of a list written without it, as AutoCAD 2021 took 0.3 and 315 degrees to the own 0 of the list 90,
            /// 180 (plugin D26b). Read into a turn.
            /// </summary>
            internal double Angle(double value, double own)
            {
                double slack = Tolerance.Default.EqualVector;

                if ((_flags & 8) != 0 && _list.Count > 0)
                {
                    List<double> list = _list;

                    if (!_list.Exists(v => Round(v, own) <= slack))
                    {
                        list = new List<double>(_list) { own };
                        list.Sort();
                    }

                    double best = list[0];

                    for (int i = 1; i < list.Count; i++)
                    {
                        double nearer = Round(value, best) - Round(value, list[i]);

                        if (nearer > slack || (Math.Abs(nearer) <= slack && Tie(best, list[i], value) == list[i]))
                        {
                            best = list[i];
                        }
                    }

                    return Normal(best);
                }

                double low = (_flags & 1) != 0 ? _minimum : 0.0;
                double high = (_flags & 2) != 0 ? _maximum : Turn;

                if (value < low - slack || value > high + slack)
                {
                    double off = Round(value, low) - Round(value, high);
                    return Normal(off < -slack ? low : off > slack ? high : Tie(low, high, value));
                }

                double taken = value;

                if ((_flags & 4) != 0 && _increment > 0.0)
                {
                    double steps = (value - _minimum) / _increment;
                    double below = _minimum + Math.Floor(steps) * _increment;
                    double above = below + _increment;
                    double gap = (value - below) - (above - value);
                    taken = gap < -slack ? below : gap > slack ? above : Tie(below, above, value);
                }

                return Normal(Math.Min(Math.Max(taken, low), high));
            }

            /// <summary>
            /// Of two angles as near to the angle asked round the circle, the one behind it, clockwise, as the step below a
            /// distance is taken; of two half a turn away either way, the lesser. AutoCAD 2021's own choice followed its rounding
            /// rather than a rule (plugin D25): of 58 ties of steps of 15 degrees, from nought and from 180, it took the step behind
            /// in 26 and the one ahead in 32, 52.5 to 45 and 7.5 to 15; of 32 ties of the list 0, 90, 180 and 270 it took the one
            /// behind in 23, 45 to 0, 225 to 180 and 315 to 270, and 135 to 180 each time, the list's 180 read a bit below pi.
            /// </summary>
            private static double Tie(double first, double second, double value)
            {
                double behind = Normal(value - first), other = Normal(value - second);

                if (Math.Abs(behind - other) <= Tolerance.Default.EqualVector)
                {
                    return Math.Min(first, second);
                }

                return behind <= Math.PI ? first : second;
            }

            /// <summary>How far apart two angles are round the circle, from nought to pi.</summary>
            private static double Round(double a, double b)
            {
                double apart = Math.Abs(a - b) % Turn;
                return apart > Math.PI ? Turn - apart : apart;
            }
        }

        /// <summary>An XY parameter: its distances along x and along y, each measured from the base point the way the end point lies.</summary>
        private sealed class Planar : Param
        {
            private double _signX;
            private double _signY;
            private Allowed _x;
            private Allowed _y;

            internal static Planar Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var planar = new Planar();
                TwoPoints(planar, groups, id, "XY", block);
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockXYParameter");
                double dx = planar.End.X - planar.Base.X, dy = planar.End.Y - planar.Base.Y;
                planar._x = Allowed.Of(own, 96, 142, 175);
                planar._y = Allowed.Of(own, 97, 146, 176);

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

            /// <summary>A distance along x (part 0) or y (part 1), in the INSERT's units, taken to its value set, as <see cref="Allowed.Distance"/> says.</summary>
            internal double Take(int part, double value, double scale) => (part == 0 ? _x : _y).Distance(value, scale, Own[part] * scale);

            /// <summary>The move of the end point: each distance now less the block's own, the way the end point lies.</summary>
            internal override double[] Moved(double[] now) => new[] { _signX * (now[0] - Own[0]), _signY * (now[1] - Own[1]) };

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now, double[] carried)
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

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now, double[] carried)
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
            private Allowed _set;

            internal static Turning Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var turning = new Turning();
                TwoPoints(turning, groups, id, "rotation", block);
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockRotationParameter");
                double[] basis = DxfDynamicProperties.PointOf(own, 1011);
                turning._set = Allowed.Of(own, 96, 141, 175);

                if (basis == null)
                {
                    throw new NotSupportedException("A rotation parameter of the block " + block + " is not one the library works out: its base angle cannot be read.");
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

            /// <summary>An angle taken to the value set, as <see cref="Allowed.Angle"/> says.</summary>
            internal double Take(double value) => _set.Angle(value, Own[0]);

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now, double[] carried)
            {
                double angle = _from + now[0];
                var end = new GeoPoint3(Base.X + _radius * Math.Cos(angle), Base.Y + _radius * Math.Sin(angle), End.Z);
                List<KeyValuePair<int, string>> groups = PointsRecord(RotationKey, Base, end);
                Point(groups, _baseAngle);
                yield return Record(Id, groups);
            }
        }

        /// <summary>
        /// A polar parameter: the distance from its base point to its end point and the angle of that line from the block's x
        /// axis, each with its value set.
        /// </summary>
        private sealed class Polar : Param
        {
            private Allowed _distance;
            private Allowed _angle;

            internal static Polar Of(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var polar = new Polar();
                TwoPoints(polar, groups, id, "polar", block);
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockPolarParameter");
                double dx = polar.End.X - polar.Base.X, dy = polar.End.Y - polar.Base.Y;

                if (!(Math.Abs(dx) + Math.Abs(dy) > 0.0))
                {
                    throw new NotSupportedException("A polar parameter of the block " + block + " has its end point over its base point.");
                }

                polar._distance = Allowed.Of(own, 96, 141, 175);
                polar._angle = Allowed.Of(own, 97, 145, 176);
                polar.Own = new[] { polar.Base.GetVectorTo(polar.End).Length, Normal(Math.Atan2(dy, dx)) };
                return polar;
            }

            /// <summary>A distance (part 0), in the INSERT's units, or an angle (part 1), taken to its value set, as <see cref="Allowed"/> says.</summary>
            internal double Take(int part, double value, double scale) => part == 0 ? _distance.Distance(value, scale, Own[0] * scale) : _angle.Angle(value, Own[1]);

            /// <summary>The end point at the distance and the angle given, level with the block's own along z.</summary>
            internal GeoPoint3 EndAt(double[] now) => new GeoPoint3(Base.X + now[0] * Math.Cos(now[1]), Base.Y + now[0] * Math.Sin(now[1]), End.Z);

            /// <summary>The move of the end point, which a stretch or a move tied to it follows: where it is now less the block's own; none at the block's own values.</summary>
            internal override double[] Moved(double[] now)
            {
                if (now[0] == Own[0] && now[1] == Own[1])
                {
                    return new double[2];
                }

                GeoPoint3 end = EndAt(now);
                return new[] { end.X - End.X, end.Y - End.Y };
            }

            /// <summary>
            /// What a polar stretch does at the values given: to a point it holds, the turn about the base point by the angle less
            /// the block's own, then the move along the line by the distance less the block's own (<paramref name="turnOnly"/>
            /// false); to a point it does not hold and an entity it turns only, the turn alone. Null where it changes nothing.
            /// </summary>
            internal GeoTransform3 Turned(double[] now, bool turnOnly)
            {
                double angle = now[1] - Own[1];
                double change = turnOnly ? 0.0 : now[0] - Own[0];

                if (angle == 0.0 && change == 0.0)
                {
                    return null;
                }

                GeoTransform3 turn = GeoTransform3.RotationAxis(Base, new GeoVector3(0, 0, 1), angle);
                return change == 0.0 ? turn : GeoTransform3.Translation(new GeoVector3(change * Math.Cos(now[1]), change * Math.Sin(now[1]), 0.0)).Multiply(turn);
            }

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now, double[] carried)
            {
                List<KeyValuePair<int, string>> groups = PointsRecord(PolarKey, Base, EndAt(now));
                Point(groups, new GeoPoint3(Base.X + 10.0, Base.Y, Base.Z));
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

            internal override IEnumerable<KeyValuePair<string, List<KeyValuePair<int, string>>>> Records(double[] now, double[] carried)
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

            /// <summary>Whether a stretch or a move follows its parameter's base point (BaseXDelta, BaseYDelta) rather than its end point.</summary>
            internal bool ByBase { get; private set; }

            /// <summary>The node ids of the parameters a move carries (the 91s its 70 counts), in their order; none for the others.</summary>
            internal List<string> Carried { get; } = new List<string>();

            /// <summary>The handles of the entities a polar stretch turns only (its 331s); none for the others.</summary>
            internal List<string> Turned { get; } = new List<string>();

            /// <summary>
            /// A polar stretch action: the polar parameter it follows by its end point (the six connections EndXDelta,
            /// EndYDelta, Base, End, UpdatedBase and UpdatedEnd of one node), its selection (330), the entities it turns only
            /// (331 counted by 72), each entity it stretches with the indices of its points in the frame (332, the count 75 and
            /// the indices 76, counted by 74), as AutoCAD saved those of ACadSharp's sample and of its Annotation sample;
            /// refused where it follows the base point, has codes (78) or a distance multiplier or an angle offset other than 1
            /// and nought, whose working AutoCAD's samples do not show.
            /// </summary>
            internal static BlockAction PolarStretched(IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var stretch = new BlockAction { Id = id, Kind = ActionKind.PolarStretch };
                DxfDynamicProperties.Section(groups, "AcDbBlockAction").FindAll(g => g.TypeCode == 330).ForEach(g => stretch.Entities.Add(g.Value?.Trim()));
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockPolarStretchAction");
                var nodes = new List<string>();
                var connections = new List<string>();
                int codes = 0;
                double multiplier = 1.0, angle = 0.0;
                bool held = false;

                foreach (DxfTypedValue group in own)
                {
                    string text = group.Value?.Trim();

                    switch (group.TypeCode)
                    {
                        case 92:
                        case 93:
                        case 94:
                        case 95:
                        case 96:
                        case 97:
                            nodes.Add(text);
                            break;
                        case 301:
                        case 302:
                        case 303:
                        case 304:
                        case 305:
                        case 306:
                            connections.Add(text);
                            break;
                        case 331:
                            stretch.Turned.Add(text);
                            break;
                        case 74:
                            held = true;
                            break;
                        case 332:
                            stretch.Held.Add(new KeyValuePair<string, List<int>>(text, new List<int>()));
                            break;
                        case 76:
                            if (held && stretch.Held.Count > 0 && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                            {
                                stretch.Held[stretch.Held.Count - 1].Value.Add(index);
                            }

                            break;
                        case 78:
                            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out codes);
                            held = false;
                            break;
                        case 141:
                            DxfDynamicProperties.Number(text, out multiplier);
                            break;
                        case 140:
                            DxfDynamicProperties.Number(text, out angle);
                            break;
                    }
                }

                double slack = Tolerance.Default.EqualVector;

                if (id == null || nodes.Count != 6 || nodes.Exists(n => n == null || n != nodes[0]) || connections.Count != 6 || connections[0] != "EndXDelta" || connections[1] != "EndYDelta"
                    || codes != 0 || Math.Abs(multiplier - 1.0) > slack || Math.Abs(angle) > slack)
                {
                    throw new NotSupportedException("A polar stretch of the block " + block + " is not one the library works out: it works out a polar stretch by the end point of a polar parameter, of the distance multiplier 1 and the angle offset nought.");
                }

                stretch.Parameter = nodes[0];
                stretch._x = nodes[0];
                stretch._y = nodes[0];
                return stretch;
            }

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
                stretch.ByBase = stretch._xBy == "BaseXDelta" && stretch._yBy == "BaseYDelta";

                if (id == null || stretch._x == null || stretch._x != stretch._y || !(stretch.ByBase || (stretch._xBy == "EndXDelta" && stretch._yBy == "EndYDelta")) || parameters != 0)
                {
                    throw new NotSupportedException("A stretch of the block " + block + " is not one the library works out: it works out a stretch by the end point or the base point of a linear, an XY or a polar parameter, moving no other parameter.");
                }

                stretch.Parameter = stretch._x;
                stretch.Multiplier = Math.Abs(multiplier - 1.0) <= slack ? 1.0 : multiplier;
                stretch.Offset = Math.Abs(angle) <= slack ? 0.0 : angle;
                return stretch;
            }

            /// <summary>
            /// A move, a scale, a rotate or a flip action: the parameter it follows, its connections, the entities it works on,
            /// and the parameters a move carries: the node ids (91) the count of its head (70) gives, as LibreDWG reads them,
            /// each a parameter its entities (330) hold too, as AutoCAD's Block Editor writes a move whose selection holds one.
            /// </summary>
            internal static BlockAction Of(string type, IReadOnlyList<DxfTypedValue> groups, string id, string block)
            {
                var action = new BlockAction { Id = id };
                List<DxfTypedValue> head = DxfDynamicProperties.Section(groups, "AcDbBlockAction");
                int carried = Param.FlagsOf(head, 70);

                foreach (DxfTypedValue group in head)
                {
                    if (group.TypeCode == 330)
                    {
                        action.Entities.Add(group.Value?.Trim());
                    }
                    else if (group.TypeCode == 91)
                    {
                        action.Carried.Add(group.Value?.Trim());
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
                    action.ByBase = action._xBy == "BaseXDelta" && action._yBy == "BaseYDelta";
                    double multiplier = Param.RealOf(own, 140, 1.0), angle = Param.RealOf(own, 141, 0.0);
                    action.Multiplier = Math.Abs(multiplier - 1.0) <= slack ? 1.0 : multiplier;
                    action.Offset = Math.Abs(angle) <= slack ? 0.0 : angle;
                    known = carried == action.Carried.Count && !action.Carried.Contains(null);
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

                if (id == null || action._x == null || action._x != action._y || (action.Kind != ActionKind.Move && (carried != 0 || action.Carried.Count != 0)) || !known)
                {
                    throw new NotSupportedException("A " + type + " of the block " + block + " is not one the library works out: it works out a move, a scale and a rotate about the parameter's base point, a flip and an array of a column offset, each of entities only, and a move carrying linear parameters.");
                }

                action.Parameter = action._x;
                return action;
            }

            /// <summary>
            /// Refuses an action whose parameter is not of the kind it follows, or that works on what is not an entity of the
            /// block the library works out: a stretch of a linear, an XY or a polar parameter, of lines, polylines and entities of
            /// one point by their points, and of arcs by both ends or, lying flat, by one; a move of a linear, an XY or a polar
            /// parameter by its end point or its base point, of a point parameter by its position, the attribute definitions a
            /// stretch, a move, a scale and a rotate alone may hold; a scale of a linear parameter, a rotate of a rotation parameter, a flip of a flip parameter, an array
            /// of a linear parameter or of an XY parameter of a row offset; a polar stretch of a polar parameter, of the points
            /// a stretch holds but an arc by one end, and of entities to turn only.
            /// </summary>
            internal void Check(Block block, Dictionary<string, DxfEntity> entities, string name)
            {
                Param parameter = block.Parameters.Find(p => p.Id == Parameter);
                bool end = parameter is Linear || parameter is Planar || parameter is Polar;
                bool fits;

                switch (Kind)
                {
                    case ActionKind.Stretch:
                        fits = end && (_xyType == 0 || !(parameter is Planar));
                        break;
                    case ActionKind.Move:
                        fits = _xyType == 0 && ((end && ((_xBy == "EndXDelta" && _yBy == "EndYDelta") || ByBase)) || (parameter is Spot && _xBy == "XDelta" && _yBy == "YDelta"));
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
                    case ActionKind.PolarStretch:
                        fits = parameter is Polar;
                        break;
                    default:
                        fits = parameter is Mirror;
                        break;
                }

                if (!fits)
                {
                    throw new NotSupportedException("A " + Kind.ToString().ToLowerInvariant() + " action of the block " + name + " follows a parameter or a connection the library does not work out for it.");
                }

                var acted = new List<string>(Entities);
                acted.AddRange(Turned);

                foreach (string handle in acted)
                {
                    if (Kind != ActionKind.Stretch && (!entities.TryGetValue(handle, out DxfEntity entity) || entity is DxfUnknownEntity || (entity is DxfAttributeDefinition && Kind != ActionKind.Move && Kind != ActionKind.Scale && Kind != ActionKind.Rotate)))
                    {
                        throw new NotSupportedException("A " + Kind.ToString().ToLowerInvariant() + " action of the block " + name + " works on " + handle + ", which is not an entity of the block the library works out: it may be another parameter or a grip, moved with it.");
                    }
                }

                foreach (KeyValuePair<string, List<int>> held in Held)
                {
                    entities.TryGetValue(held.Key, out DxfEntity entity);
                    List<GeoPoint3> points = entity == null ? null : DxfBlockTableRecord.StretchPoints(entity, Kind == ActionKind.Stretch);
                    int count = points?.Count ?? 0;
                    bool whole = entity is DxfLine || entity is DxfPolyline || held.Value.Count == count || (Kind == ActionKind.Stretch && entity is DxfArc arc && DxfBlockTableRecord.Flat(arc.Normal));

                    if (count == 0 || held.Value.Exists(i => i < 0 || i >= count) || !whole)
                    {
                        throw new NotSupportedException("A " + (Kind == ActionKind.Stretch ? "stretch" : "polar stretch") + " of the block " + name + " holds the entity " + held.Key + ", which is not one the library works out with the points it names: lines and polylines by their points, arcs by both ends"
                            + (Kind == ActionKind.Stretch ? " or, lying flat, by one" : string.Empty) + ", circles, texts, multiline texts and INSERTs by their one point.");
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
        /// The table of a lookup action, as AutoCAD writes it: its cells row by row, kept exactly as the file holds them
        /// (AutoCAD's lookup sample labels a row "Size 6 ", which AutoCAD 2021 read back so, plugin v21), and its columns, each
        /// of a parameter's node, looked up (282 of 1) or an input, its label of no match (305) and its connection (304). The
        /// library works out input columns of a linear or a polar parameter's distance (UpdatedDistance), a point parameter's x
        /// and y (UpdatedX, UpdatedY) and an XY parameter's distances (UpdatedDistanceX, UpdatedDistanceY), of reals in the
        /// block's units, and of a polar or a rotation parameter's angle (UpdatedAngle), of radians, as AutoCAD 2021 set them
        /// (plugin D25b); and looked-up columns of a lookup parameter (lookupString).
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
                        cells.Add(DxfReader.Decode(group.Value ?? string.Empty));
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
                    string connection = column.Connection;
                    bool known = column.LookedUp
                        ? connection == "lookupString" && block.Lookups.Exists(l => l.Id == column.Node)
                        : ((parameter is Linear || parameter is Polar) && connection == "UpdatedDistance") || (parameter is Spot && (connection == "UpdatedX" || connection == "UpdatedY"))
                            || (parameter is Planar && (connection == "UpdatedDistanceX" || connection == "UpdatedDistanceY")) || ((parameter is Polar || parameter is Turning) && connection == "UpdatedAngle");

                    if (!known || (!column.LookedUp && table._rows.Exists(r => !DxfDynamicProperties.Number(r[c], out _))))
                    {
                        throw new NotSupportedException("A lookup table of the block " + name + " has a column the library does not work out: it works out inputs of a linear or a polar parameter's distance, a point parameter's x and y, an XY parameter's distances and a polar or a rotation parameter's angle, of numbers, and lookup parameters looked up.");
                    }

                    column.IsAngle = connection == "UpdatedAngle";
                    column.Part = connection == "UpdatedY" || connection == "UpdatedDistanceY" || (parameter is Polar && column.IsAngle) ? 1 : 0;
                }

                return table;
            }

            /// <summary>
            /// The label a lookup, by its node's id, shows at the values given: its cell of the first row whose inputs are the values, within the
            /// point tolerance of <see cref="Tolerance.Default"/>, an angle (taken into a turn) within its vector tolerance, not
            /// round the circle, as AutoCAD 2021 read Custom at nought for a row of 2 pi (plugin D25b); else its column's label of
            /// no match.
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
                            double now = values[_columns[c].Node][_columns[c].Part];
                            all = Math.Abs(cell - now) <= (_columns[c].IsAngle ? Tolerance.Default.EqualVector : Tolerance.Default.EqualPoint);
                        }
                    }

                    if (all)
                    {
                        return row[column];
                    }
                }

                return _columns[column].Unmatched;
            }

            /// <summary>Whether the table has an input column, which a row sets.</summary>
            internal bool HasInputs => _columns.Exists(c => !c.LookedUp);

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
            /// Sets each input of the table to its cell of the first row whose cell of the lookup's column is the label given, else
            /// of the first whose cell is the label, both trimmed and letters of either case the same, and each lookup of the
            /// table to its cell of that row, as the table holds it; refuses a label of no row.
            /// </summary>
            internal void Choose(Looked lookup, string label, Dictionary<string, double[]> values, Dictionary<string, string> labels, string block)
            {
                int column = _columns.FindIndex(c => c.LookedUp && c.Node == lookup.Id);
                string[] chosen = column < 0 ? null : _rows.Find(r => string.Equals(r[column], label, StringComparison.Ordinal))
                    ?? _rows.Find(r => string.Equals(r[column].Trim(), label.Trim(), StringComparison.OrdinalIgnoreCase));

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
                        values[_columns[c].Node][_columns[c].Part] = _columns[c].IsAngle ? Normal(cell) : cell;
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

                /// <summary>Whether the column gives an angle (UpdatedAngle), in radians, matched within the vector tolerance.</summary>
                internal bool IsAngle { get; set; }
            }
        }

        /// <summary>
        /// A user parameter a block properties table made: its node's id, its location, whether it holds a text (170 of 5) or a
        /// real (170 of 3), and the block's own value, the one its ACDBASSOCVARIABLE keeps; refused where it is of another kind,
        /// or its variable is worked out from an expression, which the library does not evaluate.
        /// </summary>
        private sealed class User
        {
            internal string Id { get; private set; }

            internal GeoPoint3 Location { get; private set; }

            internal bool IsText { get; private set; }

            internal object Own { get; private set; }

            internal static User Of(IReadOnlyList<DxfTypedValue> groups, string id, DxfFile file, string block)
            {
                List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockUserParameter");
                string type = DxfDynamicProperties.Text(own, 170)?.Trim();
                double[] at = DxfDynamicProperties.PointOf(DxfDynamicProperties.Section(groups, "AcDbBlock1PtParameter"), 1010) ?? new double[3];
                DxfItem variable = file.ObjectItem(DxfDynamicProperties.Text(own, 330));
                List<DxfTypedValue> held = variable?.Type == "ACDBASSOCVARIABLE" ? DxfDynamicProperties.Section(variable.Groups, "AcDbAssocVariable") : new List<DxfTypedValue>();
                int code = type == "5" ? 1 : type == "3" ? 40 : 0;

                if (id == null || code == 0 || held.Count < 6 || held[5].TypeCode != code || (held[2].Value ?? string.Empty).Trim().Length > 0 && code == 1)
                {
                    throw new NotSupportedException("A user parameter of the block " + block + " is not one the library works out: it works out a user parameter of a text or a real a block properties table made, of no expression.");
                }

                object value;

                if (code == 1)
                {
                    value = DxfReader.Decode(held[5].Value ?? string.Empty);
                }
                else if (DxfDynamicProperties.Number(held[5].Value, out double real))
                {
                    value = real;
                }
                else
                {
                    throw new NotSupportedException("A user parameter of the block " + block + " keeps a value the library cannot read.");
                }

                return new User { Id = id, Location = new GeoPoint3(at[0], at[1], at[2]), IsText = code == 1, Own = value };
            }
        }

        /// <summary>
        /// A block properties table: its node's id and its table (<see cref="DxfPropertiesTable"/>), each column of a parameter
        /// the library works out (a linear or a polar parameter's distance, an XY parameter's distances, a polar or a rotation
        /// parameter's angle, a point parameter's x and y), of a user parameter (UserVariable), of the visibility state
        /// (VisibilityState, plugin D26b) or of an attribute definition of the block, as AutoCAD 2021 set them (plugin D26).
        /// </summary>
        private sealed class Sheet
        {
            private DxfPropertiesTable _table;

            // For each column: the node of its parameter or user parameter, which value of it, whether an angle; the tag of an
            // attribute definition's.
            private readonly List<string> _nodes = new List<string>();
            private readonly List<int> _parts = new List<int>();
            private readonly List<bool> _angles = new List<bool>();
            private readonly List<string> _tags = new List<string>();
            private readonly List<bool> _users = new List<bool>();
            private readonly List<bool> _states = new List<bool>();

            internal string Id { get; private set; }

            /// <summary>The number of rows.</summary>
            internal int Count => _table.Rows.Count;

            internal bool MustMatch => _table.MustMatch;

            internal static Sheet Of(string id, IReadOnlyList<DxfTypedValue> groups, Block block, Dictionary<string, DxfEntity> entities, string name)
            {
                var sheet = new Sheet { Id = id, _table = DxfPropertiesTable.Of(groups) };

                if (id == null || sheet._table == null)
                {
                    throw new NotSupportedException("The block properties table of the block " + name + " cannot be read whole.");
                }

                foreach (DxfPropertiesTable.Column column in sheet._table.Columns)
                {
                    Param parameter = block.Parameters.Find(p => p.Id == column.Node);
                    string connection = column.Connection;
                    string tag = null;
                    bool user = false;
                    bool shown = !column.IsAttribute && connection == "VisibilityState" && block.Visibility != null && block.Visibility.Id == column.Node;

                    if (column.IsAttribute)
                    {
                        tag = entities.TryGetValue(column.Handle, out DxfEntity entity) && entity is DxfAttributeDefinition definition ? definition.Tag ?? string.Empty : null;
                    }
                    else if (connection == "UserVariable")
                    {
                        user = block.Users.Exists(u => u.Id == column.Node);
                    }

                    bool known = column.IsAttribute ? tag != null : user || shown
                        || ((parameter is Linear || parameter is Polar) && connection == "UpdatedDistance") || (parameter is Spot && (connection == "UpdatedX" || connection == "UpdatedY"))
                        || (parameter is Planar && (connection == "UpdatedDistanceX" || connection == "UpdatedDistanceY")) || ((parameter is Polar || parameter is Turning) && connection == "UpdatedAngle");

                    if (!known)
                    {
                        throw new NotSupportedException("The block properties table of the block " + name + " has a column the library does not work out: it works out a linear or a polar parameter's distance, an XY parameter's distances, a polar or a rotation parameter's angle, a point parameter's x and y, user parameters, the visibility state and attribute definitions.");
                    }

                    bool angle = connection == "UpdatedAngle";
                    sheet._nodes.Add(column.Node);
                    sheet._parts.Add(connection == "UpdatedY" || connection == "UpdatedDistanceY" || (parameter is Polar && angle) ? 1 : 0);
                    sheet._angles.Add(angle);
                    sheet._tags.Add(tag);
                    sheet._users.Add(user);
                    sheet._states.Add(shown);
                }

                return sheet;
            }

            /// <summary>The index of the first row of the values and the visibility state given, as <see cref="DxfPropertiesTable.Row"/> says; -1 where none holds them.</summary>
            internal int Row(Dictionary<string, double[]> values, Dictionary<string, object> users, string state) => _table.Row(column =>
            {
                int c = _table.Columns.IndexOf(column);
                return _states[c] ? state : _users[c] ? (users.TryGetValue(_nodes[c], out object user) ? user : null) : (object)values[_nodes[c]][_parts[c]];
            });

            /// <summary>
            /// Sets each column to its cell of a row, as AutoCAD 2021 set the library's table and its Parking sample's (plugin
            /// D26): a parameter's value (an angle taken into a turn), a user parameter's value, the visibility state the cell
            /// names (letters of either case the same), and the text of the attribute of an attribute definition's tag; an empty
            /// cell of an attribute definition's column gives its attribute an empty text, as AutoCAD 2021 gave MARK (plugin
            /// D26b); another empty cell leaves its column as it is, and so does a cell not of its column's kind.
            /// </summary>
            internal void Choose(int row, Dictionary<string, double[]> values, Dictionary<string, object> users, Dictionary<string, string> texts, Visible visibility, ref string state)
            {
                object[] cells = _table.Rows[row];

                for (int c = 0; c < cells.Length; c++)
                {
                    object cell = cells[c];

                    if (cell == null)
                    {
                        if (_tags[c] != null)
                        {
                            texts[_tags[c]] = string.Empty;
                        }

                        continue;
                    }

                    if (_states[c])
                    {
                        State named = cell is string text ? visibility.States.Find(s => string.Equals(s.Name, text.Trim(), StringComparison.OrdinalIgnoreCase)) : null;
                        state = named?.Name ?? state;
                    }
                    else if (_tags[c] != null)
                    {
                        texts[_tags[c]] = cell as string ?? Convert.ToString(cell, CultureInfo.InvariantCulture);
                    }
                    else if (_users[c])
                    {
                        if (users.TryGetValue(_nodes[c], out object now) && now?.GetType() == cell.GetType())
                        {
                            users[_nodes[c]] = cell;
                        }
                    }
                    else if (cell is double number)
                    {
                        values[_nodes[c]][_parts[c]] = _angles[c] ? Normal(number) : number;
                    }
                }
            }

            /// <summary>
            /// Takes the values of a table whose properties must match a row to a row after the property of a column, by its
            /// node and part, was set, as AutoCAD 2021 took them (plugin D26b): to the first row that holds the value set and
            /// the first row's values in the other columns; else, where the column holds no value twice, to the row that holds
            /// the value set; else the value set stays, beside the first row's values in the other columns, and no row matches.
            /// Of the rows 100 x 50, 200 x 80 and 150 x 60, Width 200 took the row 1 and Width 120 stood beside Height 50; of the
            /// rows 100 x 50, 200 x 80, 200 x 50 and 150 x 60, Width 200 took 200 x 50, and Height 60 stood beside Width 100.
            /// Values within <see cref="DxfPropertiesTable.Held"/>; a column of an attribute definition is left as it is, as
            /// AutoCAD left the attribute; a property of no column changes nothing here.
            /// </summary>
            internal void Match(string node, int part, Dictionary<string, double[]> values)
            {
                int set = -1;

                for (int c = 0; c < _nodes.Count; c++)
                {
                    if (_tags[c] == null && !_users[c] && !_states[c] && _nodes[c] == node && _parts[c] == part)
                    {
                        set = c;
                        break;
                    }
                }

                if (set < 0 || _table.Rows.Count == 0)
                {
                    return;
                }

                double asked = values[node][part];
                object[] first = _table.Rows[0];
                int chosen = -1;

                for (int r = 0; r < _table.Rows.Count && chosen < 0; r++)
                {
                    bool all = true;

                    for (int c = 0; c < _nodes.Count && all; c++)
                    {
                        if (_tags[c] == null && first[c] != null)
                        {
                            all = DxfPropertiesTable.Same(_table.Rows[r][c], c == set ? (object)asked : first[c], _angles[c]);
                        }
                    }

                    chosen = all ? r : -1;
                }

                if (chosen < 0 && Unique(set))
                {
                    chosen = _table.Rows.FindIndex(cells => DxfPropertiesTable.Same(cells[set], asked, _angles[set]));
                }

                object[] taken = chosen < 0 ? first : _table.Rows[chosen];

                for (int c = 0; c < _nodes.Count; c++)
                {
                    if (_tags[c] == null && c != set && taken[c] is double number)
                    {
                        values[_nodes[c]][_parts[c]] = _angles[c] ? Normal(number) : number;
                    }
                }

                if (chosen >= 0 && _table.Rows[chosen][set] is double cell)
                {
                    values[node][part] = _angles[set] ? Normal(cell) : cell;
                }
            }

            /// <summary>Whether no two rows hold the same value in a column.</summary>
            private bool Unique(int column)
            {
                for (int a = 0; a < _table.Rows.Count; a++)
                {
                    for (int b = a + 1; b < _table.Rows.Count; b++)
                    {
                        if (_table.Rows[a][column] != null && DxfPropertiesTable.Same(_table.Rows[a][column], _table.Rows[b][column], _angles[column]))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            /// <summary>Whether the table has a column of a user parameter or of the visibility state, which a table to match does not work out.</summary>
            internal bool HasUsers => _users.Contains(true) || _states.Contains(true);

            /// <summary>Gives each user parameter of a column its value of no match where no row holds the values given.</summary>
            internal void Unmatch(Dictionary<string, double[]> values, Dictionary<string, object> users, string state)
            {
                if (Row(values, users, state) >= 0)
                {
                    return;
                }

                for (int c = 0; c < _table.Columns.Count; c++)
                {
                    object unmatched = _table.Columns[c].Unmatched;

                    if (_users[c] && unmatched != null && users.TryGetValue(_nodes[c], out object now) && now?.GetType() == unmatched.GetType())
                    {
                        users[_nodes[c]] = unmatched;
                    }
                }
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
