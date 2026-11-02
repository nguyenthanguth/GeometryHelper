using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The copy of a block's associative network that an anonymous block standing for it holds, as AutoCAD 2021 writes one
    /// into the anonymous block of an INSERT it sets (plugin D27b): AutoCAD solves the block's constraints again at a value
    /// set later only through that copy, and left an INSERT whose anonymous block held none, or one naming entities gone,
    /// as it was until RESETBLOCK.
    /// </summary>
    internal sealed partial class DxfBlockConstraints
    {
        /// <summary>
        /// The text AutoCAD 2021 writes a value set into the expression of its variable: the value to twelve significant
        /// digits, an exponent of three digits after an <c>e</c> where one is written, as it wrote Height 612.3456789012345
        /// of its Fluorescent sample as 612.345678901 and its d10 as 2.22044604925e-016 (plugin D27b).
        /// </summary>
        internal static string Written(double value)
        {
            string text = value.ToString("G12", CultureInfo.InvariantCulture);
            int e = text.IndexOf('E');

            if (e < 0)
            {
                return text;
            }

            char sign = text[e + 1] == '-' ? '-' : '+';
            string digits = text.Substring(e + 2).TrimStart('0');
            return text.Substring(0, e) + "e" + sign + digits.PadLeft(3, '0');
        }

        /// <summary>A value set as AutoCAD 2021 takes it: read back from the text <see cref="Written"/> gives, as it solved Height 612.3456789012345 of its Fluorescent sample at 612.345678901 (plugin D27b).</summary>
        internal static double Rounded(double value) => double.Parse(Written(value), NumberStyles.Float, CultureInfo.InvariantCulture);

        /// <summary>
        /// Takes out the copy of a network an anonymous block holds, where it holds one: each object of the network erased,
        /// each dependency of it taken out of the chain of dependencies on its object and out of that object's reactors, and
        /// the network out of the list of the network that owns it, so that nothing names an entity the anonymous block no
        /// longer has.
        /// </summary>
        internal static void Release(DxfBlockTableRecord anonymous)
        {
            DxfDBDictionary extension = anonymous?.ExtensionDictionary;
            DxfDBObject entry = DxfDynamicProperties.Entry(extension, "ACAD_ASSOCNETWORK");

            if (entry == null)
            {
                return;
            }

            DxfDBObject held = DxfDynamicProperties.Entry(entry as DxfDBDictionary, "ACAD_ASSOCNETWORK");
            DxfFile file = anonymous.Item.File;
            List<DxfItem> tree = held?.Item != null && held.Item.Type == "ACDBASSOCNETWORK" ? Tree(file, held.Item) : new List<DxfItem>();
            var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            tree.ForEach(i => mine.Add(i.Handle));

            foreach (DxfItem item in tree)
            {
                int at = Start(item.Groups, "AcDbAssocDependency");

                if (at < 0 || !Dependency(item.Groups, at))
                {
                    continue;
                }

                string on = item.Groups[at + 7].Value.Trim();
                string previous = item.Groups[at + 9].Value.Trim();
                string next = item.Groups[at + 10].Value.Trim();
                Relink(file, previous, mine, 10, next);
                Relink(file, next, mine, 9, previous);

                if (!mine.Contains(on) && file.ObjectItem(on) is DxfItem target && !target.IsRemoved)
                {
                    file.SetReactor(target, item.Handle, false);
                }
            }

            if (tree.Count > 0)
            {
                Unlist(file, tree[0]);
            }

            extension.Remove("ACAD_ASSOCNETWORK");

            foreach (DxfItem item in tree)
            {
                if (!item.IsRemoved)
                {
                    file.RemoveRecord(item);
                }
            }
        }

        /// <summary>Points the chain slot (9 the previous, 10 the next) of a dependency outside the network taken out at the handle given.</summary>
        private static void Relink(DxfFile file, string handle, HashSet<string> mine, int slot, string to)
        {
            if (handle == "0" || mine.Contains(handle) || !(file.ObjectItem(handle) is DxfItem other) || other.IsRemoved)
            {
                return;
            }

            int at = Start(other.Groups, "AcDbAssocDependency");

            if (at >= 0 && Dependency(other.Groups, at))
            {
                file.SetValueAt(other, at + slot, to);
            }
        }

        /// <summary>Takes a network out of the list of actions of the network that owns it.</summary>
        private static void Unlist(DxfFile file, DxfItem network)
        {
            DxfItem owner = OwningNetwork(file, network);

            if (owner == null)
            {
                return;
            }

            List<KeyValuePair<int, string>> groups = Raw(owner);
            int at = Start(owner.Groups, "AcDbAssocNetwork");

            if (at < 0 || at + 2 >= groups.Count || !int.TryParse(groups[at + 2].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            {
                return;
            }

            for (int i = at + 3; i < at + 3 + count && i < groups.Count; i++)
            {
                if (string.Equals(groups[i].Value.Trim(), network.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    groups.RemoveAt(i);
                    groups[at + 2] = new KeyValuePair<int, string>(90, (count - 1).ToString(CultureInfo.InvariantCulture));
                    file.ReplaceTail(owner, 0, groups);
                    return;
                }
            }
        }

        /// <summary>The network that owns a network as an action of it (the 330 of its AcDbAssocAction); null where none does.</summary>
        private static DxfItem OwningNetwork(DxfFile file, DxfItem network)
        {
            int at = Start(network.Groups, "AcDbAssocAction");
            DxfItem owner = at < 0 || at + 2 >= network.Groups.Count || network.Groups[at + 2].TypeCode != 330 ? null : file.ObjectItem(network.Groups[at + 2].Value.Trim());
            return owner != null && owner.Type == "ACDBASSOCNETWORK" && !owner.IsRemoved ? owner : null;
        }

        /// <summary>
        /// Writes a copy of the block's network into the anonymous block that stands for it, as AutoCAD 2021 copied it into
        /// the anonymous block of an INSERT it set (plugin D27b): under the entry ACAD_ASSOCNETWORK of a dictionary of the
        /// same name in the anonymous block's extension dictionary, each object of the network copied with a handle of its
        /// own, each handle naming the copy of what it named (an object of the network, or the copy of an entity of the
        /// block), the network listed by the network that owns the block's and given the next index of an action there, each
        /// dependency on a constraint parameter put last in the chain of the parameter's dependencies and among its reactors,
        /// and each copy of an entity given the reactors of its own. The copy holds the values set: each variable given its
        /// value at the values given (by name) and at the user parameters' given (by the handle of their variables), its
        /// expression the value's <see cref="Written"/> text where it was no expression of other variables and the value
        /// changed, whole or real as <see cref="DxfConstraintExpression"/> says, each dependency on a variable the value it
        /// read, each body of a dimension its value as the drawing writes lengths (LUPREC places, trailing zeros left out
        /// where DIMZIN says so) and each body of a parameter its name and that value, as AutoCAD wrote 612.3457 and
        /// Height=612.3457; and each geometry of the constraint groups the geometry of the copy of its entity, the actions
        /// having worked, as AutoCAD wrote the lines of its Window sample flipped. Nothing is written where the constraints
        /// hold a node the library does not read, the network is owned by no network, or a geometry's copy cannot be read.
        /// </summary>
        internal void Copy(DxfBlockTableRecord anonymous, IReadOnlyDictionary<string, DxfEntity> sources, IReadOnlyDictionary<string, DxfEntity> copies, IDictionary<string, double> given, IReadOnlyDictionary<string, object> held)
        {
            DxfFile file = _file;
            DxfItem top = OwningNetwork(file, _network);
            DxfItem inner = file.ObjectItem(file.OwnerOf(_network) ?? string.Empty);

            if (Refusal != null || top == null || inner == null || inner.Type != "DICTIONARY")
            {
                return;
            }

            // The groups of each constraint group, its geometry taken to the copies', and the unknowns of the copies' geometry.
            var patched = new Dictionary<string, List<KeyValuePair<int, string>>>(StringComparer.OrdinalIgnoreCase);
            double[] x = _start.ToArray();

            foreach (Node shape in _shapes)
            {
                if (!patched.TryGetValue(shape.Group.Handle, out List<KeyValuePair<int, string>> groups))
                {
                    patched[shape.Group.Handle] = groups = Raw(shape.Group);
                }

                if (shape.Kind != Kind.Construction && !Snapshot(shape, sources, copies, groups, x))
                {
                    return;
                }
            }

            // A construction line runs through the points put on it, its point the first's and its direction to the second, as
            // the block has it.
            foreach (Node shape in _shapes)
            {
                List<Node> on = shape.Kind != Kind.Construction ? null : shape.Links.FindAll(r => r != null && r.Class == "AcPointCurveConstraint" && r.Links.Count > 0 && Is(r.Links[0], Kind.Point));

                if (on == null)
                {
                    continue;
                }

                if (on.Count < 2)
                {
                    return;
                }

                double[] p = Point(on[0].Links[0], x);
                double[] q = Point(on[1].Links[0], x);
                x[shape.At] = p[0];
                x[shape.At + 1] = p[1];
                x[shape.At + 2] = q[0];
                x[shape.At + 3] = q[1];

                if (!Put(patched[shape.Group.Handle], shape.DataAt, 0, p) || !Put(patched[shape.Group.Handle], shape.DataAt, 1, Minus(q, p)))
                {
                    return;
                }
            }

            var wholes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, double> values = Values(given, wholes);
            List<DxfItem> tree = Tree(file, _network);

            // The copies made first, empty, so that each may name any other.
            anonymous.CreateExtensionDictionary();
            DxfDBDictionary extension = anonymous.ExtensionDictionary;
            DxfItem dictionary = extension.Add("ACAD_ASSOCNETWORK", "DICTIONARY", b =>
            {
                b.Group(100, "AcDbDictionary");
                b.Group(281, "1");
            });
            DxfItem network = ((DxfDBDictionary)DxfDBObject.OfObject(dictionary)).Add("ACAD_ASSOCNETWORK", "ACDBASSOCNETWORK", b => b.Group(100, "AcDbAssocAction"));
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [inner.Handle] = dictionary.Handle, [_network.Handle] = network.Handle };
            var made = new Dictionary<string, DxfItem>(StringComparer.OrdinalIgnoreCase) { [_network.Handle] = network };

            foreach (DxfItem item in tree)
            {
                if (!made.ContainsKey(item.Handle))
                {
                    DxfItem copy = file.AddObject(item.Type, null, b => { });
                    map[item.Handle] = copy.Handle;
                    made[item.Handle] = copy;
                }
            }

            foreach (KeyValuePair<string, DxfEntity> pair in copies)
            {
                map[pair.Key] = pair.Value.Handle;
            }

            // The last dependency of each chain on an object outside the network, and the copies to put after it.
            var last = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var chained = new List<KeyValuePair<string, string>>();

            foreach (DxfItem item in tree)
            {
                List<KeyValuePair<int, string>> groups = patched.TryGetValue(item.Handle, out List<KeyValuePair<int, string>> own) ? own : Raw(item);
                int dependency = Start(item.Groups, "AcDbAssocDependency");
                string outside = dependency >= 0 && Dependency(item.Groups, dependency) && !map.ContainsKey(groups[dependency + 7].Value.Trim()) ? groups[dependency + 7].Value.Trim() : null;
                DxfItem target = outside == null ? null : file.ObjectItem(outside);

                for (int i = 0; i < groups.Count; i++)
                {
                    int code = groups[i].Key;

                    if ((code == 330 || code == 340 || code == 350 || code == 360) && map.TryGetValue(groups[i].Value.Trim(), out string to))
                    {
                        groups[i] = new KeyValuePair<int, string>(code, to);
                    }
                }

                groups[0] = new KeyValuePair<int, string>(5, map[item.Handle]);

                if (dependency >= 0 && Dependency(item.Groups, dependency))
                {
                    if (target != null && !target.IsRemoved)
                    {
                        if (!last.TryGetValue(outside, out string end))
                        {
                            end = Last(file, item);
                        }

                        groups[dependency + 9] = new KeyValuePair<int, string>(330, end);
                        groups[dependency + 10] = new KeyValuePair<int, string>(330, "0");
                        chained.Add(new KeyValuePair<string, string>(end, map[item.Handle]));
                        last[outside] = map[item.Handle];
                        file.SetReactor(target, map[item.Handle], true);
                    }
                    else
                    {
                        for (int slot = 9; slot <= 10; slot++)
                        {
                            if (!map.ContainsValue(groups[dependency + slot].Value.Trim()))
                            {
                                groups[dependency + slot] = new KeyValuePair<int, string>(330, "0");
                            }
                        }
                    }
                }

                Values(item, groups, values, wholes, held);
                file.ReplaceTail(made[item.Handle], 0, groups);
            }

            // Each chain on an object outside the network runs on into its copy.
            foreach (KeyValuePair<string, string> link in chained)
            {
                DxfItem before = file.ObjectItem(link.Key);
                int at = before == null ? -1 : Start(before.Groups, "AcDbAssocDependency");

                if (at >= 0 && Dependency(before.Groups, at))
                {
                    file.SetValueAt(before, at + 10, link.Value);
                }
            }

            // Each copy of an entity names among its reactors each dependency of the copy on it, in the order of the network,
            // as AutoCAD's copies of its samples' entities do.
            foreach (DxfItem item in tree)
            {
                int at = Start(item.Groups, "AcDbAssocDependency");

                if (at >= 0 && Dependency(item.Groups, at) && copies.TryGetValue(item.Groups[at + 7].Value.Trim(), out DxfEntity entity))
                {
                    file.SetReactor(entity.Item, map[item.Handle], true);
                }
            }

            // Each dimension of a constraint shows its value as the body of its dependency does, as AutoCAD 2021 wrote 500 for
            // 500.0000 into the copies of its Fluorescent sample's dimensions once it set Height (plugin D27b).
            foreach (Rule rule in _rules)
            {
                List<DxfTypedValue> ties = rule.Variable == null ? null : rule.Rest.FindAll(g => g.TypeCode == 340);
                DxfItem tie = ties == null || ties.Count < 2 ? null : file.ObjectItem(ties[1].Value?.Trim() ?? string.Empty);
                int at = tie == null ? -1 : Start(tie.Groups, "AcDbAssocDependency");

                if (at >= 0 && Dependency(tie.Groups, at) && copies.TryGetValue(tie.Groups[at + 7].Value.Trim(), out DxfEntity entity) && entity is DxfDimension dimension
                    && values.TryGetValue(rule.Variable.Name, out double now))
                {
                    dimension.DimensionText = Shown(now, file.Database, rule.Class.EndsWith("AngleConstraint", StringComparison.Ordinal));
                    Measure(rule, dimension, x);
                }
            }

            Indexed(file, top, network, made);
        }

        /// <summary>
        /// Gives the copy of the network the next index of an action of the network that owns it, that network's count of
        /// its actions and their list taken on by it, and the copy's own next index the highest of its actions', as AutoCAD
        /// 2021 numbered its copies (plugin D27b).
        /// </summary>
        private static void Indexed(DxfFile file, DxfItem top, DxfItem network, Dictionary<string, DxfItem> made)
        {
            List<KeyValuePair<int, string>> groups = Raw(top);
            int at = Start(top.Groups, "AcDbAssocNetwork");

            if (at < 0 || at + 2 >= groups.Count || !int.TryParse(groups[at + 1].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int next)
                || !int.TryParse(groups[at + 2].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || at + 3 + count > groups.Count)
            {
                return;
            }

            next++;
            groups[at + 1] = new KeyValuePair<int, string>(90, next.ToString(CultureInfo.InvariantCulture));
            groups[at + 2] = new KeyValuePair<int, string>(90, (count + 1).ToString(CultureInfo.InvariantCulture));
            groups.Insert(at + 3 + count, new KeyValuePair<int, string>(330, network.Handle));
            file.ReplaceTail(top, 0, groups);

            List<KeyValuePair<int, string>> own = Raw(network);
            int action = Start(network.Groups, "AcDbAssocAction");
            int list = Start(network.Groups, "AcDbAssocNetwork");

            if (action >= 0 && action + 4 < own.Count && own[action + 4].Key == 90)
            {
                own[action + 4] = new KeyValuePair<int, string>(90, next.ToString(CultureInfo.InvariantCulture));
            }
            int highest = 0;

            foreach (KeyValuePair<int, string> group in own)
            {
                if (group.Key == 360 && file.ObjectItem(group.Value.Trim()) is DxfItem child && !ReferenceEquals(child, network))
                {
                    int head = Start(child.Groups, "AcDbAssocAction");

                    if (head >= 0 && head + 4 < child.Groups.Count && int.TryParse(child.Groups[head + 4].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    {
                        highest = Math.Max(highest, index);
                    }
                }
            }

            if (list >= 0 && highest > 0 && list + 1 < own.Count)
            {
                own[list + 1] = new KeyValuePair<int, string>(90, highest.ToString(CultureInfo.InvariantCulture));
            }

            file.ReplaceTail(network, 0, own);
        }

        /// <summary>The last dependency of the chain of dependencies on an object a dependency is in, followed by the next of each (330 of its slot 10).</summary>
        private static string Last(DxfFile file, DxfItem dependency)
        {
            DxfItem at = dependency;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (seen.Add(at.Handle))
            {
                int start = Start(at.Groups, "AcDbAssocDependency");
                string next = start >= 0 && Dependency(at.Groups, start) ? at.Groups[start + 10].Value.Trim() : "0";

                if (next == "0" || !(file.ObjectItem(next) is DxfItem following) || following.IsRemoved)
                {
                    return at.Handle;
                }

                at = following;
            }

            return at.Handle;
        }

        /// <summary>
        /// Writes the values set into a copy of an object of the network: a variable's value and expression, a dependency on a
        /// variable's value, a body of a dimension or a parameter its text, as <see cref="Copy"/> says.
        /// </summary>
        private void Values(DxfItem item, List<KeyValuePair<int, string>> groups, Dictionary<string, double> values, Dictionary<string, bool> wholes, IReadOnlyDictionary<string, object> held)
        {
            switch (item.Type)
            {
                case "ACDBASSOCVARIABLE":
                    {
                        int at = Start(item.Groups, "AcDbAssocVariable");
                        Variable variable = _variables.Find(v => string.Equals(v.Handle, item.Handle, StringComparison.OrdinalIgnoreCase));

                        if (at < 0 || variable == null || at + 5 >= groups.Count)
                        {
                            return;
                        }

                        if (held.TryGetValue(item.Handle, out object user))
                        {
                            if (user is string text && !(variable.Value is string own && own == text))
                            {
                                groups[at + 5] = new KeyValuePair<int, string>(1, text);
                            }
                            else if (user is double real && !(variable.Value is double was && was.Equals(real)))
                            {
                                groups[at + 5] = new KeyValuePair<int, string>(40, Numbers.Exact(real + 0.0));
                            }

                            return;
                        }

                        if (Changed(variable.Name, variable, values, wholes, out KeyValuePair<int, string> value))
                        {
                            groups[at + 5] = value;

                            if (!variable.Worked)
                            {
                                groups[at + 2] = new KeyValuePair<int, string>(1, Written(values[variable.Name]));
                            }
                        }

                        return;
                    }

                case "ACDBASSOCVALUEDEPENDENCY":
                    {
                        int at = Start(item.Groups, "AcDbAssocValueDependency");
                        int head = Start(item.Groups, "AcDbAssocDependency");
                        string on = head >= 0 && Dependency(item.Groups, head) ? item.Groups[head + 7].Value.Trim() : null;
                        Variable variable = _variables.Find(v => string.Equals(v.Handle, on, StringComparison.OrdinalIgnoreCase));

                        if (at < 0 || variable == null || at + 2 >= groups.Count)
                        {
                            return;
                        }

                        if (held.TryGetValue(variable.Handle, out object user))
                        {
                            groups[at + 2] = user is string text ? new KeyValuePair<int, string>(1, text) : new KeyValuePair<int, string>(40, Numbers.Exact((double)user + 0.0));
                        }
                        else if (Changed(variable.Name, variable, values, wholes, out KeyValuePair<int, string> value))
                        {
                            groups[at + 2] = value;
                        }

                        return;
                    }

                case "ASSOCDIMDEPENDENCYBODY":
                case "BLOCKPARAMDEPENDENCYBODY":
                    {
                        int at = Start(item.Groups, "AcDbImpAssocDimDependencyBodyBase");
                        string owner = _file.OwnerOf(item);
                        Rule rule = _rules.Find(r => r.Variable != null && r.Rest.FindAll(g => g.TypeCode == 340).Count > 1
                            && string.Equals(r.Rest.FindAll(g => g.TypeCode == 340)[1].Value?.Trim(), owner, StringComparison.OrdinalIgnoreCase));

                        if (at < 0 || rule == null || at + 1 >= groups.Count || groups[at + 1].Key != 1 || !values.TryGetValue(rule.Variable.Name, out double now))
                        {
                            return;
                        }

                        string shown = Shown(now, _file.Database, rule.Class.EndsWith("AngleConstraint", StringComparison.Ordinal));
                        groups[at + 1] = new KeyValuePair<int, string>(1, item.Type == "ASSOCDIMDEPENDENCYBODY" ? shown : rule.Variable.Name + "=" + shown);
                        return;
                    }
            }
        }

        /// <summary>Whether a numeric variable's value at the values set is not the one it keeps, and its group if so: a whole value (90) or a real (40).</summary>
        private static bool Changed(string name, Variable variable, Dictionary<string, double> values, Dictionary<string, bool> wholes, out KeyValuePair<int, string> group)
        {
            group = default(KeyValuePair<int, string>);

            if (!(variable.Value is double own) || !values.TryGetValue(name, out double now))
            {
                return false;
            }

            bool whole = wholes.TryGetValue(name, out bool w) && w;

            if (own.Equals(now))
            {
                return false;
            }

            group = whole && Math.Abs(now) < 2147483648.0
                ? new KeyValuePair<int, string>(90, ((long)Math.Round(now)).ToString(CultureInfo.InvariantCulture))
                : new KeyValuePair<int, string>(40, Numbers.Exact(now + 0.0));
            return true;
        }

        /// <summary>
        /// A length as AutoCAD 2021 wrote it into a body of a dimension or of a parameter of its samples' copies (plugin
        /// D27b): to the places the drawing shows lengths to (LUPREC), its trailing zeros and point left out where DIMZIN
        /// has 8 and a leading zero where it has 4, as 562.3457 and 25 at LUPREC 4 and DIMZIN 8, and 5.0000 at DIMZIN 0; an
        /// angle, in degrees, to the places the drawing shows angles to (AUPREC), as 151 at AUPREC 0.
        /// </summary>
        private static string Shown(double value, DxfDatabase database, bool angle)
        {
            int places = angle ? database?.Auprec ?? 0 : database?.Luprec ?? 4;
            int zeros = database?.Dimzin ?? 0;
            string text = value.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

            if ((zeros & 8) != 0 && text.IndexOf('.') >= 0)
            {
                text = text.TrimEnd('0').TrimEnd('.');
            }

            if ((zeros & 4) != 0 && (text.StartsWith("0.", StringComparison.Ordinal) || text.StartsWith("-0.", StringComparison.Ordinal)))
            {
                text = text.Replace("0.", ".");
            }

            return text == "-0" ? "0" : text;
        }

        /// <summary>
        /// Takes a geometry of a constraint group in its group's groups to the copy of the entity it is tied to: a line its
        /// base point, direction, start and end, an arc its centre, radius, start and end, a circle its centre and radius, of
        /// a LINE, an ARC, a CIRCLE or an edge of an LWPOLYLINE as the copy has it; false where the copy cannot be read so.
        /// </summary>
        private bool Snapshot(Node shape, IReadOnlyDictionary<string, DxfEntity> sources, IReadOnlyDictionary<string, DxfEntity> copies, List<KeyValuePair<int, string>> groups, double[] x)
        {
            DxfItem dependency = shape.Dependency == null ? null : _file.ObjectItem(shape.Dependency);
            List<DxfTypedValue> held = dependency == null ? null : DxfDynamicProperties.Section(dependency.Groups, "AcDbAssocDependency");
            string handle = held == null ? null : DxfDynamicProperties.Text(held, 330)?.Trim();

            if (handle == null || !sources.TryGetValue(handle, out DxfEntity source) || !copies.TryGetValue(handle, out DxfEntity copy))
            {
                return false;
            }

            List<DxfTypedValue> part = DxfDynamicProperties.Section(dependency.Groups, "AcDbAssocGeomDependency");
            int named = part.FindIndex(g => g.TypeCode == 1);
            string kind = named < 0 ? null : part[named].Value?.Trim();
            double[] start;
            double[] end;
            double[] centre = null;
            double radius = 0.0;

            if (kind == "AcDbAssocSingleEdgePersSubentId" && copy is DxfLine line && shape.Kind == Kind.Line)
            {
                start = new[] { line.StartPoint.X, line.StartPoint.Y };
                end = new[] { line.EndPoint.X, line.EndPoint.Y };
            }
            else if (kind == "AcDbAssocSingleEdgePersSubentId" && copy is DxfArc arc && shape.Kind == Kind.Arc)
            {
                centre = new[] { arc.Center.X, arc.Center.Y };
                radius = arc.Radius;
                start = new[] { centre[0] + radius * Math.Cos(arc.StartAngle), centre[1] + radius * Math.Sin(arc.StartAngle) };
                end = new[] { centre[0] + radius * Math.Cos(arc.EndAngle), centre[1] + radius * Math.Sin(arc.EndAngle) };
            }
            else if (kind == "AcDbAssocSingleEdgePersSubentId" && copy is DxfCircle circle && shape.Kind == Kind.Circle)
            {
                centre = new[] { circle.Center.X, circle.Center.Y };
                radius = circle.Radius;
                start = end = centre;
            }
            else if (kind == "AcDbAssocEdgePersSubentId" && named + 3 < part.Count && copy is DxfPolyline polyline && source is DxfPolyline own
                && Whole(part[named + 2], out int from) && Whole(part[named + 3], out int to))
            {
                List<int> ids = DxfDynamicProperties.Section(own.Item.Groups, "AcDbPolyline").FindAll(g => g.TypeCode == 91).ConvertAll(g => Whole(g, out int id) ? id : int.MinValue);
                int i = ids.IndexOf(from);
                int j = ids.IndexOf(to);

                if (i < 0 || j < 0 || ids.Count != polyline.NumberOfVertices || (shape.Kind != Kind.Line && shape.Kind != Kind.Arc))
                {
                    return false;
                }

                GeoPoint2 a = polyline.GetPoint2dAt(i);
                GeoPoint2 b = polyline.GetPoint2dAt(j);
                double bulge = polyline.GetBulgeAt(i);
                start = new[] { a.X, a.Y };
                end = new[] { b.X, b.Y };

                if (shape.Kind == Kind.Arc)
                {
                    if (bulge == 0.0)
                    {
                        return false;
                    }

                    // The centre lies off the chord's middle to its left by half the chord times (1 - b^2) / (2 b); an arc of a
                    // negative bulge runs counter-clockwise from the edge's end to its start.
                    double dx = b.X - a.X, dy = b.Y - a.Y;
                    double chord = Math.Sqrt(dx * dx + dy * dy);
                    double off = (1.0 - bulge * bulge) / (4.0 * bulge);
                    centre = new[] { 0.5 * (a.X + b.X) - dy * off, 0.5 * (a.Y + b.Y) + dx * off };
                    radius = chord * (1.0 + bulge * bulge) / (4.0 * Math.Abs(bulge));

                    if (bulge < 0.0)
                    {
                        double[] swap = start;
                        start = end;
                        end = swap;
                    }
                }
            }
            else
            {
                return false;
            }

            int at = shape.At;

            switch (shape.Kind)
            {
                case Kind.Line:
                    double length = Math.Sqrt((end[0] - start[0]) * (end[0] - start[0]) + (end[1] - start[1]) * (end[1] - start[1]));

                    if (!(length > 0.0))
                    {
                        return false;
                    }

                    x[at] = start[0];
                    x[at + 1] = start[1];
                    x[at + 2] = end[0];
                    x[at + 3] = end[1];
                    return Put(groups, shape.DataAt, 0, start) && Put(groups, shape.DataAt, 1, new[] { (end[0] - start[0]) / length, (end[1] - start[1]) / length })
                        && Put(groups, shape.DataAt, 2, start) && Put(groups, shape.DataAt, 3, end);
                case Kind.Arc:
                    x[at] = centre[0];
                    x[at + 1] = centre[1];
                    x[at + 2] = radius;
                    x[at + 3] = Math.Atan2(start[1] - centre[1], start[0] - centre[0]);
                    x[at + 4] = Math.Atan2(end[1] - centre[1], end[0] - centre[0]);
                    return Put(groups, shape.DataAt, 0, centre) && Put(groups, shape.DataAt, 3, start) && Put(groups, shape.DataAt, 4, end) && PutRadius(groups, shape.DataAt, radius);
                default:
                    x[at] = centre[0];
                    x[at + 1] = centre[1];
                    x[at + 2] = radius;
                    return Put(groups, shape.DataAt, 0, centre) && PutRadius(groups, shape.DataAt, radius);
            }
        }

        /// <summary>
        /// Moves the copy of a dimension of a constraint to the geometry of the copies (<paramref name="x"/>): an aligned or a
        /// rotated dimension of a distance its extension lines' points to the constraint's points, each to the one its own
        /// stands nearer, a rotated one's line point to where its line meets the second extension line and an aligned one's
        /// by as much as its second point moved, the text with it; a radial dimension of a
        /// radius its centre to the arc's and its chord point with it, at the radius now, as AutoCAD 2021 moved them once it
        /// solved its samples' constraints (plugin D27b). Any other is left where it is.
        /// </summary>
        private void Measure(Rule rule, DxfDimension dimension, double[] x)
        {
            double[] start = _start.ToArray();

            if (rule.Class == "AcDistanceConstraint")
            {
                double[] p = Point(rule.Links[0], x);
                double[] q = Point(rule.Links[1], x);
                double[] p0 = Point(rule.Links[0], start);
                double[] q0 = Point(rule.Links[1], start);
                GeoPoint3 first;
                GeoPoint3 second;
                GeoPoint3 line;

                if (dimension is DxfRotatedDimension rotated)
                {
                    first = rotated.XLine1Point;
                    second = rotated.XLine2Point;
                    line = rotated.DimLinePoint;
                }
                else if (dimension is DxfAlignedDimension aligned)
                {
                    first = aligned.XLine1Point;
                    second = aligned.XLine2Point;
                    line = aligned.DimLinePoint;
                }
                else
                {
                    return;
                }

                bool turned = Far(first, p0) + Far(second, q0) > Far(first, q0) + Far(second, p0);
                double[] one = turned ? q : p;
                double[] two = turned ? p : q;
                double dx = two[0] - second.X, dy = two[1] - second.Y;

                // A rotated dimension's line point is where its line meets the second extension line, as AutoCAD 2021 put
                // Fluorescent's Width at (550, -91.28) once its second point came to x = 550.
                if (dimension is DxfRotatedDimension along)
                {
                    double cos = Math.Cos(along.Rotation), sin = Math.Sin(along.Rotation);
                    double run = (two[0] - line.X) * cos + (two[1] - line.Y) * sin;
                    dx = run * cos;
                    dy = run * sin;
                }

                GeoPoint3 text = dimension.TextPosition;
                var newFirst = new GeoPoint3(one[0], one[1], first.Z);
                var newSecond = new GeoPoint3(two[0], two[1], second.Z);
                var newLine = new GeoPoint3(line.X + dx, line.Y + dy, line.Z);

                if (dimension is DxfRotatedDimension r)
                {
                    r.XLine1Point = newFirst;
                    r.XLine2Point = newSecond;
                    r.DimLinePoint = newLine;
                }
                else
                {
                    var a = (DxfAlignedDimension)dimension;
                    a.XLine1Point = newFirst;
                    a.XLine2Point = newSecond;
                    a.DimLinePoint = newLine;
                }

                dimension.TextPosition = new GeoPoint3(text.X + dx, text.Y + dy, text.Z);
            }
            else if (rule.Class == "AcAngleConstraint" && dimension is DxfLineAngularDimension2 angular)
            {
                // Each point of the dimension goes with the end of either line it stands on, as AutoCAD 2021 moved the angles
                // of its Concrete Pipe sample with the lines of the bell (plugin D27b); the arc's point and the text with the
                // first line's end.
                var ends = new List<Node>();

                foreach (Node line in new[] { rule.Links[0], rule.Links[1] })
                {
                    ends.Add(new Node { Owner = line, PointType = 0 });
                    ends.Add(new Node { Owner = line, PointType = 1 });
                }

                GeoPoint3[] points = { angular.XLine1Start, angular.XLine1End, angular.XLine2Start, angular.XLine2End };
                var moved = new GeoPoint3[4];
                double dx = 0.0, dy = 0.0;

                for (int i = 0; i < 4; i++)
                {
                    Node nearest = ends[0];

                    foreach (Node end in ends)
                    {
                        if (Far(points[i], Point(end, start)) < Far(points[i], Point(nearest, start)))
                        {
                            nearest = end;
                        }
                    }

                    double[] was = Point(nearest, start);
                    double[] now = Point(nearest, x);
                    moved[i] = new GeoPoint3(points[i].X + now[0] - was[0], points[i].Y + now[1] - was[1], points[i].Z);

                    if (i == 1)
                    {
                        dx = now[0] - was[0];
                        dy = now[1] - was[1];
                    }
                }

                GeoPoint3 arc = angular.ArcPoint;
                GeoPoint3 text = dimension.TextPosition;
                angular.XLine1Start = moved[0];
                angular.XLine1End = moved[1];
                angular.XLine2Start = moved[2];
                angular.XLine2End = moved[3];
                angular.ArcPoint = new GeoPoint3(arc.X + dx, arc.Y + dy, arc.Z);
                dimension.TextPosition = new GeoPoint3(text.X + dx, text.Y + dy, text.Z);
            }
            else if (rule.Class == "Ac3PointAngleConstraint" && dimension is DxfPoint3AngularDimension three)
            {
                // The centre goes with the angle's point, each extension line's point with the nearer of the other two, and
                // the arc's point and the text with the centre.
                double[] o0 = Point(rule.Links[0], start);
                double[] o = Point(rule.Links[0], x);
                GeoPoint3 Move(GeoPoint3 p)
                {
                    Node nearest = Far(p, Point(rule.Links[1], start)) <= Far(p, Point(rule.Links[2], start)) ? rule.Links[1] : rule.Links[2];
                    double[] was = Point(nearest, start);
                    double[] now = Point(nearest, x);
                    return new GeoPoint3(p.X + now[0] - was[0], p.Y + now[1] - was[1], p.Z);
                }

                GeoPoint3 centre = three.CenterPoint;
                GeoPoint3 arc = three.ArcPoint;
                GeoPoint3 text = dimension.TextPosition;
                double dx = o[0] - o0[0], dy = o[1] - o0[1];
                three.XLine1Point = Move(three.XLine1Point);
                three.XLine2Point = Move(three.XLine2Point);
                three.CenterPoint = new GeoPoint3(centre.X + dx, centre.Y + dy, centre.Z);
                three.ArcPoint = new GeoPoint3(arc.X + dx, arc.Y + dy, arc.Z);
                dimension.TextPosition = new GeoPoint3(text.X + dx, text.Y + dy, text.Z);
            }
            else if (rule.Class == "AcRadiusDiameterConstraint" && dimension is DxfRadialDimension radial)
            {
                Node round = rule.Links[0];
                GeoPoint3 centre = radial.Center;
                GeoPoint3 chord = radial.ChordPoint;
                double ux = chord.X - centre.X, uy = chord.Y - centre.Y;
                double length = Math.Sqrt(ux * ux + uy * uy);

                if (!(length > 0.0))
                {
                    return;
                }

                double r = x[round.At + 2];
                double cx = x[round.At], cy = x[round.At + 1];
                GeoPoint3 text = dimension.TextPosition;
                radial.Center = new GeoPoint3(cx, cy, centre.Z);
                radial.ChordPoint = new GeoPoint3(cx + ux / length * r, cy + uy / length * r, chord.Z);
                dimension.TextPosition = new GeoPoint3(text.X + cx - centre.X, text.Y + cy - centre.Y, text.Z);
            }
        }

        private static double Far(GeoPoint3 a, double[] b) => Math.Abs(a.X - b[0]) + Math.Abs(a.Y - b[1]);

        /// <summary>Writes x and y of the point of an order among a node's groups (a 10 or an 11 with its 20 or 21), each kept as written where it is the value; false where the node has no such point.</summary>
        private static bool Put(List<KeyValuePair<int, string>> groups, int from, int order, double[] point)
        {
            int seen = 0;

            for (int i = from; i + 1 < groups.Count; i++)
            {
                int code = groups[i].Key;

                if ((code == 10 || code == 11) && groups[i + 1].Key == code + 10)
                {
                    if (seen++ == order)
                    {
                        Set(groups, i, point[0]);
                        Set(groups, i + 1, point[1]);
                        return true;
                    }

                    i++;
                }
                else if (code == 1 || code == 70 && i > from)
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>Writes the first real (40) of a node's groups, its radius.</summary>
        private static bool PutRadius(List<KeyValuePair<int, string>> groups, int from, double radius)
        {
            for (int i = from; i < groups.Count; i++)
            {
                if (groups[i].Key == 40)
                {
                    Set(groups, i, radius);
                    return true;
                }
            }

            return false;
        }

        private static void Set(List<KeyValuePair<int, string>> groups, int at, double value)
        {
            if (!(DxfDynamicProperties.Number(groups[at].Value, out double was) && was.Equals(value)))
            {
                groups[at] = new KeyValuePair<int, string>(groups[at].Key, Numbers.Exact(value + 0.0));
            }
        }

        /// <summary>The objects of a network: itself first, then each object it owns (360) and each those own, in the order reached.</summary>
        private static List<DxfItem> Tree(DxfFile file, DxfItem network)
        {
            var tree = new List<DxfItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<DxfItem>();
            queue.Enqueue(network);
            seen.Add(network.Handle);

            while (queue.Count > 0)
            {
                DxfItem item = queue.Dequeue();
                tree.Add(item);

                foreach (DxfTypedValue group in item.Groups)
                {
                    if (group.TypeCode == 360 && file.ObjectItem(group.Value?.Trim() ?? string.Empty) is DxfItem owned && !owned.IsRemoved && seen.Add(owned.Handle))
                    {
                        queue.Enqueue(owned);
                    }
                }
            }

            return tree;
        }

        /// <summary>The groups of a record up to its extended data, as written: the handle (5) first.</summary>
        private static List<KeyValuePair<int, string>> Raw(DxfItem item)
        {
            var groups = new List<KeyValuePair<int, string>>();

            foreach (DxfTypedValue group in item.Groups)
            {
                if (group.TypeCode == 1001)
                {
                    break;
                }

                groups.Add(new KeyValuePair<int, string>(group.TypeCode, group.Value));
            }

            return groups;
        }

        /// <summary>Where the groups of a subclass begin among a record's groups (just after its marker, 100); -1 where it has none.</summary>
        private static int Start(IReadOnlyList<DxfTypedValue> groups, string marker)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode == 100 && groups[i].Value?.Trim() == marker)
                {
                    return i + 1;
                }
            }

            return -1;
        }

        /// <summary>Whether a dependency's groups from the place given are as every sample writes them: the object it depends on (330, its eighth), then a 290, the previous and the next dependency on that object (330, 330).</summary>
        private static bool Dependency(IReadOnlyList<DxfTypedValue> groups, int at)
            => at + 10 < groups.Count && groups[at + 7].TypeCode == 330 && groups[at + 8].TypeCode == 290 && groups[at + 9].TypeCode == 330 && groups[at + 10].TypeCode == 330;
    }
}
