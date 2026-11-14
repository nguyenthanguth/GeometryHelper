using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The associative objects AutoCAD 2021 writes with a center mark or a center line, as its DXF showed them: an
    /// <c>ACDBASSOCACTION</c> in the <c>ACDBASSOCNETWORK</c> of the space (the network in the space's extension dictionary and
    /// in a dictionary of the named objects), its body (<c>AcDbCenterMarkActionBody</c> or <c>AcDbCenterLineActionBody</c>), a
    /// dependency on the INSERT and one on each circle, arc or line (each an <c>ACDBASSOCGEOMDEPENDENCY</c> the object has among
    /// its persistent reactors) with the edge parameter that names it, and the values the picture is drawn from.
    /// </summary>
    internal static partial class CadCenterMarks
    {
        private const string NetworkKey = "ACAD_ASSOCNETWORK";
        private const string MarkBody = "AcDbCenterMarkActionBody";
        private const string LineBody = "AcDbCenterLineActionBody";
        private const double Unlimited = 1.0E+60;

        private static readonly string[] AssocClasses =
        {
            "ACDBASSOCNETWORK|AcDbAssocNetwork",
            "ACDBASSOCACTION|AcDbAssocAction",
            "ACDBASSOCDEPENDENCY|AcDbAssocDependency",
            "ACDBASSOCGEOMDEPENDENCY|AcDbAssocGeomDependency",
            "ACDBASSOCEDGEACTIONPARAM|AcDbAssocEdgeActionParam",
        };

        /// <summary>The groups of a record written one after another, as the DXF holds them.</summary>
        internal sealed class Gs : List<KeyValuePair<int, string>>
        {
            internal void G(int code, string value) => Add(new KeyValuePair<int, string>(code, value));

            internal void G(int code, int value) => G(code, value.ToString(CultureInfo.InvariantCulture));

            internal void G(int code, double value) => G(code, Numbers.Exact(value + 0.0));

            /// <summary>A point as its three groups: 10, 20 and 30 for a first code of 10.</summary>
            internal void P(int code, double x, double y, double z)
            {
                G(code, x);
                G(code + 10, y);
                G(code + 20, z);
            }

            internal void Emit(DxfFile.Builder b)
            {
                foreach (KeyValuePair<int, string> group in this)
                {
                    b.Group(group.Key, group.Value);
                }
            }
        }

        /// <summary>One value of an action, by name: a real, a text or the handle of the dependency on the INSERT.</summary>
        internal sealed class Value
        {
            internal string Name;
            internal char Kind;
            internal double Real;
            internal string Text;
        }

        /// <summary>One circle, arc or line an action is built on: the entity, how its dependency names the part of it, and the edge it hands the action.</summary>
        internal sealed class Input
        {
            internal DxfEntity Entity;
            internal Gs Subentity = new Gs();
            internal int EdgeType;
            internal Gs Edge = new Gs();
        }

        /// <summary>Whether the drawing can hold the associative objects: an OBJECTS section, handles, and the file version that brought them (R2010, AC1024).</summary>
        internal static bool CanTie(DxfDatabase database) =>
            database.File.HoldsObjects
            && string.CompareOrdinal(database.Version ?? string.Empty, "AC1024") >= 0
            && database.File.WritesHandles;

        /// <summary>
        /// Writes an action tying an INSERT to the circles, arcs or lines it is drawn from, as AutoCAD 2021 writes one: the
        /// networks made where the drawing has none, the action listed in the space's network, its body, the dependencies and
        /// edge parameters, the persistent reactors of the entities and of the INSERT.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="space">The block of the space the INSERT is in.</param>
        /// <param name="body">The type of the action's body.</param>
        /// <param name="inputs">What the picture is drawn from.</param>
        /// <param name="insert">The INSERT.</param>
        /// <param name="values">The action's values, in AutoCAD's order.</param>
        internal static void Tie(
            DxfDatabase database,
            DxfBlockTableRecord space,
            string body,
            IReadOnlyList<Input> inputs,
            DxfBlockReference insert,
            IReadOnlyList<Value> values
        )
        {
            DxfFile file = database.File;

            foreach (string name in AssocClasses)
            {
                string[] parts = name.Split('|');
                file.AddClass(parts[0], parts[1], 1025);
                file.CountClass(parts[0]);
            }

            file.AddClass(body, body, 1025, "AcDbSmartCenter");
            file.CountClass(body);

            DxfItem network = EnsureNetwork(database, space, out DxfItem global);
            int index = ActionsOf(network).Count + 1;

            // The action first and empty, so that what it owns can name it; written whole at the end.
            DxfItem action = file.AddObject("ACDBASSOCACTION", null, b =>
            {
                b.Group(330, network.Handle);
                b.Group(100, "AcDbAssocAction");
            });
            var bodyGroups = new Gs();
            bodyGroups.G(330, action.Handle);
            bodyGroups.G(100, "AcDbAssocActionBody");
            bodyGroups.G(90, 2);
            bodyGroups.G(100, "AcDbSmartCenterActionBody");
            bodyGroups.G(90, 0);
            DxfItem bodyItem = file.AddObject(body, null, bodyGroups.Emit);

            var dependencies = new List<string>();
            var parameters = new List<string>();
            int order = 0;

            foreach (Input input in inputs)
            {
                order++;
                string previous = LastGeomDependency(file, input.Entity);
                var dependency = new Gs();
                Dependency(dependency, action.Handle, true, input.Entity.Handle, previous, "0", order);
                dependency.G(100, "AcDbAssocGeomDependency");
                dependency.G(90, 0);
                dependency.G(290, 1);
                dependency.AddRange(input.Subentity);
                DxfItem made = file.AddObject("ACDBASSOCGEOMDEPENDENCY", null, dependency.Emit);
                dependencies.Add(made.Handle);

                if (previous != "0" && file.ObjectItem(previous) is DxfItem before)
                {
                    int at = Start(before.Groups, "AcDbAssocDependency");

                    if (at >= 0)
                    {
                        file.SetValueAt(before, at + 10, made.Handle);
                    }
                }

                file.SetReactor(input.Entity.Item, made.Handle, true);

                var edge = new Gs();
                edge.G(330, action.Handle);
                edge.G(100, "AcDbAssocActionParam");
                edge.G(90, 1);
                edge.G(90, 0);
                edge.G(1, "InputEdge");
                edge.G(100, "AcDbAssocSingleDependencyActionParam");
                edge.G(90, 0);
                edge.G(330, made.Handle);
                edge.G(100, "AcDbAssocEdgeActionParam");
                edge.G(90, 0);
                edge.G(330, "0");
                edge.G(290, 1);
                edge.G(90, input.EdgeType);
                edge.AddRange(input.Edge);
                parameters.Add(file.AddObject("ACDBASSOCEDGEACTIONPARAM", null, edge.Emit).Handle);
            }

            var onInsert = new Gs();
            Dependency(onInsert, action.Handle, false, insert.Handle, "0", "0", order + 1);
            DxfItem insertDependency = file.AddObject("ACDBASSOCDEPENDENCY", null, onInsert.Emit);
            dependencies.Add(insertDependency.Handle);
            file.SetReactor(insert.Item, insertDependency.Handle, true);

            var whole = new Gs();
            whole.G(5, action.Handle);
            whole.G(330, network.Handle);
            whole.G(100, "AcDbAssocAction");
            whole.G(90, 2);
            whole.G(90, 0);
            whole.G(330, network.Handle);
            whole.G(360, bodyItem.Handle);
            whole.G(90, index);
            whole.G(90, dependencies.Count);
            whole.G(90, dependencies.Count);

            foreach (string handle in dependencies)
            {
                whole.G(360, handle);
            }

            whole.G(90, 0);
            whole.G(90, parameters.Count);

            foreach (string handle in parameters)
            {
                whole.G(360, handle);
            }

            whole.G(90, 0);
            whole.G(90, values.Count);

            foreach (Value value in values)
            {
                WriteValue(whole, value, insertDependency.Handle);
            }

            file.ReplaceTail(action, 0, whole);
            List(file, network, 360, action.Handle);
        }

        private static void Dependency(
            Gs g,
            string action,
            bool geometry,
            string target,
            string previous,
            string next,
            int order
        )
        {
            g.G(330, action);
            g.G(100, "AcDbAssocDependency");
            g.G(90, 2);
            g.G(90, 0);
            g.G(290, 1);
            g.G(290, geometry ? 0 : 1);
            g.G(290, 1);
            g.G(290, 1);
            g.G(90, 0);
            g.G(330, target);
            g.G(290, 0);
            g.G(330, previous);
            g.G(330, next);
            g.G(360, "0");
            g.G(90, order);
        }

        private static void WriteValue(Gs g, Value value, string insertDependency)
        {
            g.G(90, 0);
            g.G(1, value.Name);
            g.G(90, 0);

            if (value.Kind == 'e')
            {
                // The ends of the two lines' directions: what AutoCAD 2021 wrote for every pair, two flags and no object.
                g.G(90, 2);
                g.G(90, 1);
                g.G(330, "0");
                g.G(90, 1);
                g.G(330, "0");
                g.G(330, "0");
                return;
            }

            g.G(90, 1);

            switch (value.Kind)
            {
                case 'd':
                    g.G(40, value.Real);
                    break;
                case 's':
                    g.G(1, value.Text);
                    break;
                default:
                    g.G(330, insertDependency);
                    break;
            }

            g.G(330, "0");
            g.G(330, "0");
        }

        private static Value Real(string name, double value) =>
            new Value
            {
                Name = name,
                Kind = 'd',
                Real = value,
            };

        private static Value Text(string name, string value) =>
            new Value
            {
                Name = name,
                Kind = 's',
                Text = value,
            };

        /// <summary>The handle of the last dependency of the drawing on an entity: the last of its persistent reactors that is a geometry dependency, or 0.</summary>
        private static string LastGeomDependency(DxfFile file, DxfEntity entity)
        {
            string last = "0";

            foreach (string reactor in entity.GetPersistentReactorIds())
            {
                if (
                    file.ObjectItem(reactor) is DxfItem item
                    && !item.IsRemoved
                    && item.Type == "ACDBASSOCGEOMDEPENDENCY"
                )
                {
                    last = item.Handle;
                }
            }

            return last;
        }

        /// <summary>The network of a space, made with the one of the drawing it is listed in where there is none; the network of the drawing too.</summary>
        private static DxfItem EnsureNetwork(DxfDatabase database, DxfBlockTableRecord space, out DxfItem global)
        {
            DxfFile file = database.File;
            DxfDBDictionary root = database.NamedObjectsDictionary;

            if (!root.Contains(NetworkKey))
            {
                root.Add(NetworkKey, "DICTIONARY", b =>
                {
                    b.Group(100, "AcDbDictionary");
                    b.Group(280, "0");
                    b.Group(281, "1");
                });
            }

            var wrapper = (DxfDBDictionary)root[NetworkKey];
            DxfItem top = wrapper.Contains(NetworkKey) ? wrapper[NetworkKey].Item : null;

            if (top == null || top.IsRemoved)
            {
                var made = new Gs();
                Network(made, "0", 0, 330, Array.Empty<string>());
                top = wrapper.Add(NetworkKey, "ACDBASSOCNETWORK", made.Emit);
            }

            global = top;
            space.CreateExtensionDictionary();
            DxfDBDictionary extension = space.ExtensionDictionary;

            if (!extension.Contains(NetworkKey))
            {
                extension.Add(NetworkKey, "DICTIONARY", b =>
                {
                    b.Group(100, "AcDbDictionary");
                    b.Group(280, "0");
                    b.Group(281, "1");
                });
            }

            var held = (DxfDBDictionary)extension[NetworkKey];

            if (held.Contains(NetworkKey) && !held[NetworkKey].Item.IsRemoved)
            {
                return held[NetworkKey].Item;
            }

            var own = new Gs();
            Network(own, top.Handle, ActionsOf(top).Count + 1, 360, Array.Empty<string>());
            DxfItem network = held.Add(NetworkKey, "ACDBASSOCNETWORK", own.Emit);
            List(file, top, 330, network.Handle);
            return network;
        }

        /// <summary>The groups of a network that holds the objects given (360 for actions, 330 for the networks of the spaces), as AutoCAD 2021 writes one.</summary>
        private static void Network(Gs g, string owner, int index, int code, IReadOnlyList<string> held)
        {
            g.G(100, "AcDbAssocAction");
            g.G(90, 2);
            g.G(90, 0);
            g.G(330, owner);
            g.G(360, "0");
            g.G(90, index);

            for (int i = 0; i < 6; i++)
            {
                g.G(90, 0);
            }

            g.G(100, "AcDbAssocNetwork");
            g.G(90, 0);
            g.G(90, held.Count);
            g.G(90, held.Count);

            foreach (string handle in held)
            {
                g.G(code, handle);
            }

            g.G(90, 0);
        }

        /// <summary>The handles a network lists, in order.</summary>
        private static List<string> ActionsOf(DxfItem network)
        {
            var handles = new List<string>();
            IReadOnlyList<DxfTypedValue> groups = network.Groups;
            int at = Start(groups, "AcDbAssocNetwork");

            if (at < 0 || at + 2 >= groups.Count)
            {
                return handles;
            }

            if (
                !int.TryParse(
                    groups[at + 1].Value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int count
                )
            )
            {
                return handles;
            }

            for (int i = at + 3; i < at + 3 + count && i < groups.Count; i++)
            {
                handles.Add(groups[i].Value.Trim());
            }

            return handles;
        }

        /// <summary>Lists a handle last in a network's list of objects, both counts one more.</summary>
        private static void List(DxfFile file, DxfItem network, int code, string handle)
        {
            List<KeyValuePair<int, string>> groups = Raw(network);
            int at = Start(network.Groups, "AcDbAssocNetwork");

            if (
                at < 0
                || !int.TryParse(
                    groups[at + 1].Value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int count
                )
            )
            {
                return;
            }

            groups.Insert(at + 3 + count, new KeyValuePair<int, string>(code, handle));
            string next = (count + 1).ToString(CultureInfo.InvariantCulture);
            groups[at + 1] = new KeyValuePair<int, string>(90, next);
            groups[at + 2] = new KeyValuePair<int, string>(90, next);
            file.ReplaceTail(network, 0, groups);
        }

        /// <summary>Takes a handle out of a network's list, both counts one less.</summary>
        private static void Unlist(DxfFile file, DxfItem network, string handle)
        {
            List<KeyValuePair<int, string>> groups = Raw(network);
            int at = Start(network.Groups, "AcDbAssocNetwork");

            if (
                at < 0
                || !int.TryParse(
                    groups[at + 1].Value.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int count
                )
            )
            {
                return;
            }

            for (int i = at + 3; i < at + 3 + count && i < groups.Count; i++)
            {
                if (string.Equals(groups[i].Value.Trim(), handle, StringComparison.OrdinalIgnoreCase))
                {
                    groups.RemoveAt(i);
                    string next = (count - 1).ToString(CultureInfo.InvariantCulture);
                    groups[at + 1] = new KeyValuePair<int, string>(90, next);
                    groups[at + 2] = new KeyValuePair<int, string>(90, next);
                    file.ReplaceTail(network, 0, groups);
                    return;
                }
            }
        }

        /// <summary>The groups of a record up to its extended data, the handle (5) first.</summary>
        internal static List<KeyValuePair<int, string>> Raw(DxfItem item)
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

        /// <summary>Where the groups of a subclass begin among a record's groups, just after its marker (100); -1 where it has none.</summary>
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
    }
}
