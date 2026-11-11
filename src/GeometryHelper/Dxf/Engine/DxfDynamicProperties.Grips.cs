using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>The custom grips of an INSERT of a dynamic block, read from its block's graph and the INSERT's saved values.</summary>
    internal static partial class DxfDynamicProperties
    {
        /// <summary>
        /// The custom grips of an INSERT of a dynamic block, as <see cref="DxfBlockReference.GetDynamicGrips"/> gives them, in
        /// the order of the grips' nodes in the graph, as AutoCAD 2021 gave them: of each linear, polar, XY, rotation, horizontal,
        /// vertical or radial constraint parameter the grip at its end, of a flip parameter its grip, of a point, a visibility or
        /// a lookup parameter its grip, each where the parameter lists one, gives a property that is not read only, and the
        /// visibility state the INSERT shows lists the parameter and the grip where the block has a visibility parameter. A grip
        /// stands where the block puts it (1010 of its AcDbBlockGrip), moved as the point it follows moved from the block's own to
        /// where the INSERT saved it: the end point of a parameter of two points, the base point of a flip parameter (mirrored
        /// about the line while flipped), the position of a parameter of one point. None for a block of no graph.
        /// </summary>
        internal static IReadOnlyList<DxfDynamicBlockGrip> Grips(DxfBlockReference insert, DxfBlockTableRecord dynamic)
        {
            var found = new List<KeyValuePair<int, DxfDynamicBlockGrip>>();
            DxfDBObject graph = Entry(dynamic?.ExtensionDictionary, "ACAD_ENHANCEDBLOCK");

            if (graph?.Item == null || graph.Item.Type != "ACAD_EVALUATION_GRAPH")
            {
                return new List<DxfDynamicBlockGrip>().AsReadOnly();
            }

            DxfFile file = graph.Item.File;
            var nodes = new List<DxfItem>();
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (DxfTypedValue group in graph.Item.Groups)
            {
                if (group.TypeCode == 360 && file.ObjectItem(group.Value) is DxfItem node)
                {
                    string id = Text(Section(node.Groups, "AcDbEvalExpr"), 90)?.Trim();

                    if (id != null && !byId.ContainsKey(id))
                    {
                        byId.Add(id, nodes.Count);
                    }

                    nodes.Add(node);
                }
            }

            Dictionary<string, DxfXrecord> saved = Saved(insert);
            HashSet<string> shown = ShownInState(nodes, saved);
            IReadOnlyList<DxfDynamicBlockReferenceProperty> properties = Of(insert, dynamic);
            GeoTransform3 placed = insert.BlockTransform;

            foreach (DxfItem node in nodes)
            {
                IReadOnlyList<DxfTypedValue> groups = node.Groups;
                string id = Text(Section(groups, "AcDbEvalExpr"), 90)?.Trim();

                if (id == null)
                {
                    continue;
                }

                var own = new List<DxfDynamicBlockReferenceProperty>();

                foreach (DxfDynamicBlockReferenceProperty property in properties)
                {
                    if (property.Node == id)
                    {
                        own.Add(property);
                    }
                }

                // A grip of no property set, or of one AutoCAD keeps read only, drives nothing.
                if (own.Count == 0 || own.TrueForAll(p => p.ReadOnly))
                {
                    continue;
                }

                IReadOnlyList<DxfTypedValue> cache = saved.TryGetValue(id, out DxfXrecord record) ? record.Data : null;
                DxfDynamicBlockGripType type;
                int slot;

                switch (node.Type)
                {
                    case "BLOCKLINEARPARAMETER":
                    case "BLOCKHORIZONTALCONSTRAINTPARAMETER":
                    case "BLOCKVERTICALCONSTRAINTPARAMETER":
                    case "BLOCKRADIALCONSTRAINTPARAMETER":
                        type = DxfDynamicBlockGripType.Linear;
                        slot = 1;
                        break;
                    case "BLOCKPOLARPARAMETER":
                        type = DxfDynamicBlockGripType.Polar;
                        slot = 1;
                        break;
                    case "BLOCKXYPARAMETER":
                        type = DxfDynamicBlockGripType.XY;
                        slot = 1;
                        break;
                    case "BLOCKROTATIONPARAMETER":
                        type = DxfDynamicBlockGripType.Rotation;
                        slot = 1;
                        break;
                    case "BLOCKFLIPPARAMETER":
                        type = DxfDynamicBlockGripType.Flip;
                        slot = 0;
                        break;
                    case "BLOCKPOINTPARAMETER":
                        type = DxfDynamicBlockGripType.Point;
                        slot = -1;
                        break;
                    case "BLOCKVISIBILITYPARAMETER":
                        type = DxfDynamicBlockGripType.Visibility;
                        slot = -1;
                        break;
                    case "BLOCKLOOKUPPARAMETER":
                        type = DxfDynamicBlockGripType.Lookup;
                        slot = -1;
                        break;
                    default:
                        continue;
                }

                // The grip of a parameter of one point is its 93; of two points the 91 of the slot: the base grip, the end grip,
                // and two corners of an XY parameter.
                List<DxfTypedValue> slots = Section(groups, "AcDbBlock2PtParameter").FindAll(g => g.TypeCode == 91);
                string gripId = (
                    slot < 0 ? Text(Section(groups, "AcDbBlock1PtParameter"), 93)
                    : slot < slots.Count ? slots[slot].Value
                    : null
                )?.Trim();

                if (
                    string.IsNullOrEmpty(gripId)
                    || gripId == "0"
                    || !byId.TryGetValue(gripId, out int at)
                    || !nodes[at].Type.EndsWith("GRIP", StringComparison.Ordinal)
                )
                {
                    continue;
                }

                DxfItem gripNode = nodes[at];

                // A parameter, or a grip, the visibility state shown does not list is hidden with it, as AutoCAD 2021 showed no
                // grip of the Angle of its Toilet sample in the state Elongated (Front) (plugin GhDyn1).
                if (
                    shown != null
                    && node.Type != "BLOCKVISIBILITYPARAMETER"
                    && (!shown.Contains(node.Handle) || !shown.Contains(gripNode.Handle))
                )
                {
                    continue;
                }

                DxfDynamicBlockGrip grip =
                    slot < 0
                        ? OnePointGrip(type, groups, cache, gripNode, own, placed)
                        : TwoPointGrip(type, groups, cache, gripNode, own, placed);

                if (grip != null)
                {
                    found.Add(new KeyValuePair<int, DxfDynamicBlockGrip>(at, grip));
                }
            }

            // In the order of the grips' nodes, a tie (none, as a grip has one parameter) kept in the order of the parameters.
            var grips = new List<DxfDynamicBlockGrip>(found.Count);
            int[] order = new int[found.Count];

            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
                found[a].Key != found[b].Key ? found[a].Key.CompareTo(found[b].Key) : a.CompareTo(b)
            );

            foreach (int i in order)
            {
                grips.Add(found[i].Value);
            }

            return grips.AsReadOnly();
        }

        /// <summary>The vector of a code and the two after it by one (140, 141, 142); null where it cannot be read.</summary>
        private static GeoVector3? VectorOf(List<DxfTypedValue> groups, int code) =>
            Number(Text(groups, code), out double x)
            && Number(Text(groups, code + 1), out double y)
            && Number(Text(groups, code + 2) ?? "0", out double z)
                ? new GeoVector3(x, y, z)
                : (GeoVector3?)null;

        /// <summary>
        /// The grip of a parameter of two points: moved by the end point's change, its direction the orientation its linear
        /// grip keeps (140 to 142 of AcDbBlockLinearGrip, along x of a horizontal constraint parameter, along y of a vertical
        /// one), else from the base point to the end; a flip's moved by the base point's change, mirrored about its line while
        /// flipped. Null where the grip's position or the parameter's points cannot be read.
        /// </summary>
        private static DxfDynamicBlockGrip TwoPointGrip(
            DxfDynamicBlockGripType type,
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            DxfItem gripNode,
            List<DxfDynamicBlockReferenceProperty> own,
            GeoTransform3 placed
        )
        {
            double[] at = PointOf(Section(gripNode.Groups, "AcDbBlockGrip"), 1010);

            if (
                at == null
                || !TwoPoints(groups, null, out double[] fromOwn, out double[] toOwn)
                || !TwoPoints(groups, cache, out double[] from, out double[] to)
            )
            {
                return null;
            }

            GeoPoint3 basePoint = P(from);
            GeoPoint3 end = P(to);
            GeoVector3 along = basePoint.GetVectorTo(end);
            GeoPoint3 position;
            GeoVector3 direction;

            if (type == DxfDynamicBlockGripType.Flip)
            {
                position = new GeoPoint3(
                    at[0] + from[0] - fromOwn[0],
                    at[1] + from[1] - fromOwn[1],
                    at[2] + from[2] - fromOwn[2]
                );
                double flat = Math.Sqrt(along.X * along.X + along.Y * along.Y);
                bool flipped = own[0].Value is short state && state == 1;

                if (!(flat > 0.0))
                {
                    return null;
                }

                // Across the line, to its left looking from its base to its end; to its right, and the grip mirrored, while flipped.
                direction = new GeoVector3(-along.Y / flat, along.X / flat, 0.0);

                if (flipped)
                {
                    GeoVector3 off = basePoint.GetVectorTo(position);
                    double side = off.X * direction.X + off.Y * direction.Y;
                    position = new GeoPoint3(
                        position.X - 2.0 * side * direction.X,
                        position.Y - 2.0 * side * direction.Y,
                        position.Z
                    );
                    direction = new GeoVector3(-direction.X, -direction.Y, 0.0);
                }
            }
            else
            {
                position = new GeoPoint3(at[0] + to[0] - toOwn[0], at[1] + to[1] - toOwn[1], at[2] + to[2] - toOwn[2]);
                GeoVector3 way =
                    VectorOf(Section(gripNode.Groups, "AcDbBlockLinearGrip"), 140) is GeoVector3 kept
                    && kept.Length > 0.0
                        ? kept
                        : along;
                direction =
                    type == DxfDynamicBlockGripType.Linear && way.Length > 0.0
                        ? way.Normalize()
                        : new GeoVector3(0.0, 0.0, 0.0);
            }

            return Placed(type, position, basePoint, direction, own, placed);
        }

        /// <summary>
        /// The grip of a parameter of one point: moved by the change of the position, the first point the INSERT saved of the
        /// parameter against its own (1010 of AcDbBlock1PtParameter); null where the grip's position cannot be read.
        /// </summary>
        private static DxfDynamicBlockGrip OnePointGrip(
            DxfDynamicBlockGripType type,
            IReadOnlyList<DxfTypedValue> groups,
            IReadOnlyList<DxfTypedValue> cache,
            DxfItem gripNode,
            List<DxfDynamicBlockReferenceProperty> own,
            GeoTransform3 placed
        )
        {
            double[] at = PointOf(Section(gripNode.Groups, "AcDbBlockGrip"), 1010);
            double[] ownAt = PointOf(Section(groups, "AcDbBlock1PtParameter"), 1010);

            if (at == null || ownAt == null)
            {
                return null;
            }

            List<double[]> points = Points(cache);
            double[] now = points.Count >= 1 ? points[0] : ownAt;
            var position = new GeoPoint3(
                at[0] + now[0] - ownAt[0],
                at[1] + now[1] - ownAt[1],
                at[2] + now[2] - ownAt[2]
            );
            return Placed(type, position, position, new GeoVector3(0.0, 0.0, 0.0), own, placed);
        }

        /// <summary>The grip of block coordinates put where the INSERT places them; its direction turned with the block, a unit vector still.</summary>
        private static DxfDynamicBlockGrip Placed(
            DxfDynamicBlockGripType type,
            GeoPoint3 position,
            GeoPoint3 basePoint,
            GeoVector3 direction,
            List<DxfDynamicBlockReferenceProperty> own,
            GeoTransform3 placed
        )
        {
            GeoVector3 turned = placed.Transform(direction);
            turned = turned.Length > 0.0 ? turned.Normalize() : turned;
            return new DxfDynamicBlockGrip(
                type,
                placed.Transform(position),
                placed.Transform(basePoint),
                turned,
                own.ConvertAll(p => p.PropertyName)
            );
        }

        private static GeoPoint3 P(double[] point) => new GeoPoint3(point[0], point[1], point[2]);

        /// <summary>
        /// The handles of what the visibility state the INSERT shows lists as shown (each 333 after the state's 303 of
        /// AcDbBlockVisibilityParameter: parameters, their grips and actions), with the visibility parameter and its grip; null
        /// where the block has no visibility parameter, every parameter shown then. The state is the one the INSERT saved, else
        /// the parameter's first, as <see cref="Visibility"/> reads it. The library's own blocks list none, and AutoCAD 2021 read
        /// every other property of them as not visible in the state, and showed none of their grips (plugin GhDyn1).
        /// </summary>
        private static HashSet<string> ShownInState(List<DxfItem> nodes, Dictionary<string, DxfXrecord> saved)
        {
            DxfItem visibility = nodes.Find(n => n.Type == "BLOCKVISIBILITYPARAMETER");

            if (visibility == null)
            {
                return null;
            }

            string id = Text(Section(visibility.Groups, "AcDbEvalExpr"), 90)?.Trim();
            string state = id != null && saved.TryGetValue(id, out DxfXrecord record) ? Saved(record.Data) : null;
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { visibility.Handle };
            string grip = Text(Section(visibility.Groups, "AcDbBlock1PtParameter"), 93)?.Trim();
            DxfItem gripNode = nodes.Find(n => Text(Section(n.Groups, "AcDbEvalExpr"), 90)?.Trim() == grip);

            if (gripNode != null)
            {
                shown.Add(gripNode.Handle);
            }

            bool inside = false;
            bool first = true;

            foreach (DxfTypedValue group in Section(visibility.Groups, "AcDbBlockVisibilityParameter"))
            {
                if (group.TypeCode == 303)
                {
                    inside =
                        state == null
                            ? first
                            : string.Equals(
                                DxfReader.Decode(group.Value ?? string.Empty),
                                state,
                                StringComparison.Ordinal
                            );
                    first = false;
                }
                else if (group.TypeCode == 333 && inside && group.Value != null)
                {
                    shown.Add(group.Value.Trim());
                }
            }

            return shown;
        }

        /// <summary>
        /// Marks the properties a node gave, from the index given: shown in the Properties palette where its parameter's Show
        /// properties (280 of AcDbBlockParameter) is not nought, as AutoCAD 2021 read Stud Height of its Stud sample as not
        /// shown; visible in the state the INSERT shows where the block has no visibility parameter, the node is that parameter,
        /// or the state lists the node.
        /// </summary>
        private static void Shown(
            List<DxfDynamicBlockReferenceProperty> properties,
            int from,
            DxfItem node,
            HashSet<string> shown
        )
        {
            string show = Text(Section(node.Groups, "AcDbBlockParameter"), 280)?.Trim();
            bool visible = shown == null || node.Type == "BLOCKVISIBILITYPARAMETER" || shown.Contains(node.Handle);

            for (int i = from; i < properties.Count; i++)
            {
                if (properties[i] != null)
                {
                    properties[i].Show = show != "0";
                    properties[i].VisibleInCurrentVisibilityState = visible;
                }
            }
        }

        /// <summary>
        /// The visibility states of a block, as <see cref="DxfBlockTableRecord.GetVisibilityStates"/> gives them: of the first
        /// visibility parameter of its graph, each state (303 of AcDbBlockVisibilityParameter) with the entities of the block
        /// of the handles after it (332); none where the block has no such parameter.
        /// </summary>
        internal static IReadOnlyList<DxfBlockVisibilityState> VisibilityStates(DxfBlockTableRecord block)
        {
            var states = new List<DxfBlockVisibilityState>();
            DxfDBObject graph = Entry(block.ExtensionDictionary, "ACAD_ENHANCEDBLOCK");

            if (graph?.Item == null || graph.Item.Type != "ACAD_EVALUATION_GRAPH")
            {
                return states.AsReadOnly();
            }

            DxfFile file = graph.Item.File;
            DxfItem found = null;

            foreach (DxfTypedValue group in graph.Item.Groups)
            {
                if (
                    group.TypeCode == 360
                    && file.ObjectItem(group.Value) is DxfItem node
                    && node.Type == "BLOCKVISIBILITYPARAMETER"
                )
                {
                    found = node;
                    break;
                }
            }

            if (found == null)
            {
                return states.AsReadOnly();
            }

            var entities = new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in block)
            {
                if (entity.Handle != null && !entities.ContainsKey(entity.Handle))
                {
                    entities.Add(entity.Handle, entity);
                }
            }

            string name = null;
            var shown = new List<DxfEntity>();

            foreach (DxfTypedValue group in Section(found.Groups, "AcDbBlockVisibilityParameter"))
            {
                if (group.TypeCode == 303)
                {
                    Add(states, name, shown);
                    name = DxfReader.Decode(group.Value ?? string.Empty);
                    shown = new List<DxfEntity>();
                }
                else if (
                    group.TypeCode == 332
                    && name != null
                    && entities.TryGetValue(group.Value?.Trim() ?? string.Empty, out DxfEntity entity)
                    && !shown.Contains(entity)
                )
                {
                    shown.Add(entity);
                }
            }

            Add(states, name, shown);
            return states.AsReadOnly();
        }

        /// <summary>Adds a state read, where its name is one a state may have.</summary>
        private static void Add(List<DxfBlockVisibilityState> states, string name, List<DxfEntity> shown)
        {
            if (name != null && name.Trim().Length > 0 && name.IndexOf('\r') < 0 && name.IndexOf('\n') < 0)
            {
                states.Add(new DxfBlockVisibilityState(name, shown));
            }
        }

        /// <summary>
        /// The names a flip parameter gives its two states (307 and 308 of AcDbBlockFlipParameter), Not flipped and Flipped as
        /// AutoCAD's Block Editor names them, or Left and Right as its Door sample does; none for a parameter of another kind.
        /// </summary>
        internal static string[] FlipNames(IReadOnlyList<DxfTypedValue> groups)
        {
            List<DxfTypedValue> own = Section(groups, "AcDbBlockFlipParameter");
            string off = Text(own, 307),
                on = Text(own, 308);
            return off == null || on == null ? new string[0] : new[] { DxfReader.Decode(off), DxfReader.Decode(on) };
        }
    }
}
