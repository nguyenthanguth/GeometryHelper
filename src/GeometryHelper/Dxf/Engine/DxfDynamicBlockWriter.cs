using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Writes the parameters and actions of a dynamic block made in code, in the form AutoCAD writes them and AutoCAD 2021 read
    /// back as dynamic (plugin v17): the BLOCK_RECORD's extended data (AcDbDynamicBlockTrueName, AcDbDynamicBlockGUID,
    /// AcDbBlockRepETag) and extension dictionary (ACAD_ENHANCEDBLOCK naming the ACAD_EVALUATION_GRAPH,
    /// AcDbDynamicBlockRoundTripPurgePreventer), a tag of AcDbBlockRepETag on each entity of the block, the nodes of the graph
    /// with their edges, and the classes in the CLASSES section.
    /// </summary>
    internal static class DxfDynamicBlockWriter
    {
        // The version of each node's object (98 and 99 of AcDbEvalExpr and AcDbBlockElement), as AutoCAD's files of R2010 hold it.
        private const string Major = "33";
        private const string Minor = "197";

        // The value of a grip location component of an end grip before AutoCAD evaluates it, as AutoCAD writes it.
        private const string Unset = "1.797693134862314E+99";

        /// <summary>
        /// Writes a linear parameter with its two grips, each with its two location components: seven nodes, and the six edges
        /// AutoCAD writes for them (the parameter to each component, each grip to the parameter).
        /// </summary>
        internal static DxfBlockLinearParameter AddLinear(DxfBlockTableRecord block, string name, string description, GeoPoint3 basePoint, GeoPoint3 endPoint, double labelOffset, DxfBlockParameterValueSet valueSet)
        {
            Graph graph = Graph.Of(block);
            DxfFile file = graph.File;
            int p = graph.NextId;
            string element = graph.FreeName("Linear");
            GeoVector3 along = basePoint.GetVectorTo(endPoint);

            DxfItem parameter = file.AddObject("BLOCKLINEARPARAMETER", null, b =>
            {
                Head(b, graph.Handle, p, element);
                b.Group(100, "AcDbBlockParameter");
                b.Group(280, "1");
                b.Group(281, "0");
                b.Group(100, "AcDbBlock2PtParameter");
                Point(b, 1010, basePoint);
                Point(b, 1011, endPoint);
                b.Group(170, "4");
                b.Group(91, Int(p + 4));
                b.Group(91, Int(p + 1));
                b.Group(91, "0");
                b.Group(91, "0");
                Connection(b, 171, 92, p + 4, "DisplacementX");
                Connection(b, 172, 93, p + 4, "DisplacementY");
                Connection(b, 173, 94, p + 1, "DisplacementX");
                Connection(b, 174, 95, p + 1, "DisplacementY");
                b.Group(177, "0");
                b.Group(100, "AcDbBlockLinearParameter");
                b.Group(305, name);
                b.Group(306, description);
                b.Group(140, Real(labelOffset));
                b.Group(307, string.Empty);
                Values(b, valueSet);
            });

            // The label stands off the middle of the line to its left, looking from the base to the end, by the offset.
            double flat = Math.Sqrt(along.X * along.X + along.Y * along.Y);
            GeoPoint3 middle = basePoint.GetMiddlePoint(endPoint);
            GeoPoint3 label = flat > 0.0 ? new GeoPoint3(middle.X - along.Y / flat * labelOffset, middle.Y + along.X / flat * labelOffset, middle.Z) : middle;
            DxfDBObject.OfObject(parameter).XData = new[]
            {
                new DxfTypedValue(1001, "ACAUTHENVIRON"), new DxfTypedValue(1010, Real(label.X)), new DxfTypedValue(1020, Real(label.Y)), new DxfTypedValue(1030, Real(label.Z)),
            };

            graph.Add(p, parameter.Handle);
            Grip(graph, p, p + 1, "End Grip", endPoint, along, Unset, "UpdatedEnd");
            Grip(graph, p, p + 4, "Base Grip", basePoint, new GeoVector3(-along.X, -along.Y, -along.Z), "0.0", "UpdatedBase");
            graph.Link(p, p + 2, 1);
            graph.Link(p, p + 3, 1);
            graph.Link(p + 1, p, 2);
            graph.Link(p, p + 5, 1);
            graph.Link(p, p + 6, 1);
            graph.Link(p + 4, p, 2);
            graph.Write();

            file.AddClass("BLOCKLINEARPARAMETER", "AcDbBlockLinearParameter", 1153);
            file.AddClass("BLOCKLINEARGRIP", "AcDbBlockLinearGrip", 1153);
            file.AddClass("BLOCKGRIPLOCATIONCOMPONENT", "AcDbBlockGripExpr", 1153);
            return new DxfBlockLinearParameter(block, p, parameter.Handle, name, description, basePoint, endPoint, labelOffset, valueSet);
        }

        /// <summary>
        /// Writes a stretch action driven by the end point of a linear parameter (EndXDelta, EndYDelta): its frame of two
        /// corners, and each entity with the indices of its points inside the frame; one node, and the edge from the parameter.
        /// </summary>
        internal static void AddStretch(DxfBlockLinearParameter parameter, GeoPoint2 low, GeoPoint2 high, List<KeyValuePair<DxfEntity, List<int>>> held)
        {
            Graph graph = Graph.Of(parameter.Block);

            if (!graph.HasNode(parameter.Node))
            {
                throw new ArgumentException("The parameter " + parameter.Name + " is no longer in the evaluation graph of the block " + parameter.Block.Name + ".", nameof(parameter));
            }

            DxfFile file = graph.File;
            int s = graph.NextId;
            string element = graph.FreeName("Stretch");
            DxfItem action = file.AddObject("BLOCKSTRETCHACTION", null, b =>
            {
                Head(b, graph.Handle, s, element);
                b.Group(100, "AcDbBlockAction");
                b.Group(70, "0");
                b.Group(71, Int(held.Count));

                foreach (KeyValuePair<DxfEntity, List<int>> entity in held)
                {
                    b.Group(330, entity.Key.Handle);
                }

                Point(b, 1010, new GeoPoint3((low.X + high.X) / 2.0, (low.Y + high.Y) / 2.0, 0.0));
                b.Group(100, "AcDbBlockStretchAction");
                b.Group(92, Int(parameter.Node));
                b.Group(301, "EndXDelta");
                b.Group(93, Int(parameter.Node));
                b.Group(302, "EndYDelta");
                b.Group(72, "2");
                b.Group(1011, Real(low.X));
                b.Group(1021, Real(low.Y));
                b.Group(1011, Real(high.X));
                b.Group(1021, Real(high.Y));
                b.Group(73, Int(held.Count));

                foreach (KeyValuePair<DxfEntity, List<int>> entity in held)
                {
                    b.Group(331, entity.Key.Handle);
                    b.Group(74, Int(entity.Value.Count));
                    entity.Value.ForEach(i => b.Group(94, Int(i)));
                }

                b.Group(75, "0");
                b.Group(140, "1.0");
                b.Group(141, "0.0");
                b.Group(280, "0");
            });

            graph.Add(s, action.Handle);
            graph.Link(parameter.Node, s, 2);
            graph.Write();
            file.AddClass("BLOCKSTRETCHACTION", "AcDbBlockStretchAction", 1153);
        }

        /// <summary>A grip of a linear parameter and its two location components, the nodes after it.</summary>
        private static void Grip(Graph graph, int parameter, int id, string name, GeoPoint3 at, GeoVector3 direction, string value, string connection)
        {
            DxfItem grip = graph.File.AddObject("BLOCKLINEARGRIP", null, b =>
            {
                Head(b, graph.Handle, id, name);
                b.Group(100, "AcDbBlockGrip");
                b.Group(91, Int(id + 1));
                b.Group(92, Int(id + 2));
                Point(b, 1010, at);
                b.Group(280, "1");
                b.Group(93, "-1");
                b.Group(100, "AcDbBlockLinearGrip");
                b.Group(140, Real(direction.X));
                b.Group(141, Real(direction.Y));
                b.Group(142, Real(direction.Z));
            });

            graph.Add(id, grip.Handle);

            for (int k = 1; k <= 2; k++)
            {
                string axis = k == 1 ? "X" : "Y";
                DxfItem component = graph.File.AddObject("BLOCKGRIPLOCATIONCOMPONENT", null, b =>
                {
                    Head(b, graph.Handle, id + k, null);
                    b.Group(1, string.Empty);
                    b.Group(70, "40");
                    b.Group(140, value);
                    b.Group(100, "AcDbBlockGripExpr");
                    b.Group(91, Int(parameter));
                    b.Group(300, connection + axis);
                });

                graph.Add(id + k, component.Handle);
            }
        }

        /// <summary>The head of a node's object: its owner, the graph, its id, and its name where it is a named element.</summary>
        private static void Head(DxfFile.Builder b, string graph, int id, string element)
        {
            b.Group(330, graph);
            b.Group(100, "AcDbEvalExpr");
            b.Group(90, Int(id));
            b.Group(98, Major);
            b.Group(99, Minor);

            if (element != null)
            {
                b.Group(100, "AcDbBlockElement");
                b.Group(300, element);
                b.Group(98, Major);
                b.Group(99, Minor);
                b.Group(1071, "0");
            }
        }

        /// <summary>A connection of a parameter's point to a grip: its count (1), the grip's id and what it gives.</summary>
        private static void Connection(DxfFile.Builder b, int count, int node, int grip, string connection)
        {
            b.Group(count, "1");
            b.Group(node, Int(grip));
            b.Group(count + 130, connection);
        }

        /// <summary>
        /// The value set of a distance: its flags (96), minimum, maximum and increment (141 to 143), and the list counted by 175;
        /// the flags 1 and noughts where there is none, as AutoCAD writes a set of None.
        /// </summary>
        private static void Values(DxfFile.Builder b, DxfBlockParameterValueSet set)
        {
            b.Group(96, set == null ? "1" : set.IsList ? "8" : "7");
            b.Group(141, Real(set?.Minimum ?? 0.0));
            b.Group(142, Real(set?.Maximum ?? 0.0));
            b.Group(143, Real(set?.Increment ?? 0.0));
            IReadOnlyList<double> list = set?.Values ?? new double[0];
            b.Group(175, Int(list.Count));

            foreach (double value in list)
            {
                b.Group(144, Real(value));
            }
        }

        private static void Point(DxfFile.Builder b, int code, GeoPoint3 point)
        {
            b.Group(code, Real(point.X));
            b.Group(code + 10, Real(point.Y));
            b.Group(code + 20, Real(point.Z));
        }

        private static string Real(double value) => Numbers.Exact(value + 0.0);

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The evaluation graph of a dynamic block, its nodes and edges as its record holds them, to which nodes and edges are
        /// added and the record written anew: the four links of each node (its first and last edge in, its first and last edge
        /// out) and the five of each edge (the edges before and after it into the same node and out of the same node, then -1)
        /// worked out from the order of the edges, as AutoCAD writes them.
        /// </summary>
        private sealed class Graph
        {
            private readonly DxfItem _item;
            private readonly List<int[]> _nodes = new List<int[]>();          // flags, id
            private readonly List<string> _handles = new List<string>();
            private readonly List<int[]> _edges = new List<int[]>();          // flags, kind, from index, to index
            private readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private int _last;

            private Graph(DxfFile file, DxfItem item)
            {
                File = file;
                _item = item;
            }

            internal DxfFile File { get; }

            internal string Handle => _item.Handle;

            /// <summary>The id the next node is given: one above the last the graph gave (96) and every id of its nodes.</summary>
            internal int NextId
            {
                get
                {
                    int last = _last;
                    _nodes.ForEach(n => last = Math.Max(last, n[1]));
                    return last + 1;
                }
            }

            /// <summary>
            /// The graph of a block, made where it has none: the BLOCK_RECORD given its extension dictionary with the graph and
            /// the purge preventer, and its extended data of the dynamic block (its true name and a new GUID) where it has none;
            /// each entity of the block without a tag given the next, and the record the tag after the last.
            /// </summary>
            internal static Graph Of(DxfBlockTableRecord block)
            {
                DxfFile file = block.File;
                block.CreateExtensionDictionary();
                DxfDBDictionary dictionary = block.ExtensionDictionary;
                DxfItem item = dictionary.Contains("ACAD_ENHANCEDBLOCK") ? dictionary.GetAt("ACAD_ENHANCEDBLOCK")?.Item : null;

                if (item == null || item.Type != "ACAD_EVALUATION_GRAPH")
                {
                    item = dictionary.Add("ACAD_ENHANCEDBLOCK", "ACAD_EVALUATION_GRAPH", b =>
                    {
                        b.Group(100, "AcDbEvalGraph");
                        b.Group(96, "0");
                        b.Group(97, "0");
                    });

                    if (!dictionary.Contains("AcDbDynamicBlockRoundTripPurgePreventer"))
                    {
                        dictionary.Add("AcDbDynamicBlockRoundTripPurgePreventer", "ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION", b =>
                        {
                            b.Group(100, "AcDbDynamicBlockPurgePreventer");
                            b.Group(70, "1");
                        });
                    }

                    file.AddClass("ACAD_EVALUATION_GRAPH", "AcDbEvalGraph", 1153);
                    file.AddClass("ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION", "AcDbDynamicBlockPurgePreventer", 1153);
                }

                Tag(block);
                var graph = new Graph(file, item);
                graph.Read();
                return graph;
            }

            /// <summary>
            /// The extended data of the dynamic block on its BLOCK_RECORD, and a tag on each of its entities that has none: the
            /// application AcDbBlockRepETag with 1070 1, the tag (1071) and the entity's own handle (1005), as AutoCAD tags them;
            /// the record's 1071 the tag after the last.
            /// </summary>
            private static void Tag(DxfBlockTableRecord block)
            {
                int next = Tagged(block.GetXDataForApplication("AcDbBlockRepETag"));
                var untagged = new List<DxfEntity>();

                foreach (DxfEntity entity in block)
                {
                    IReadOnlyList<DxfTypedValue> tag = entity.GetXDataForApplication("AcDbBlockRepETag");

                    if (tag == null)
                    {
                        untagged.Add(entity);
                    }
                    else
                    {
                        next = Math.Max(next, Tagged(tag) + 1);
                    }
                }

                foreach (DxfEntity entity in untagged)
                {
                    entity.XData = new[] { new DxfTypedValue(1001, "AcDbBlockRepETag"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1071, Int(next++)), new DxfTypedValue(1005, entity.Handle) };
                }

                var data = new List<DxfTypedValue>();

                if (block.GetXDataForApplication("AcDbDynamicBlockTrueName") == null)
                {
                    data.Add(new DxfTypedValue(1001, "AcDbDynamicBlockTrueName"));
                    data.Add(new DxfTypedValue(1000, block.Name));
                }

                if (block.GetXDataForApplication("AcDbDynamicBlockGUID") == null)
                {
                    data.Add(new DxfTypedValue(1001, "AcDbDynamicBlockGUID"));
                    data.Add(new DxfTypedValue(1000, Guid.NewGuid().ToString("B", CultureInfo.InvariantCulture).ToUpperInvariant()));
                }

                data.Add(new DxfTypedValue(1001, "AcDbBlockRepETag"));
                data.Add(new DxfTypedValue(1070, "1"));
                data.Add(new DxfTypedValue(1071, Int(next)));
                block.XData = data;
            }

            /// <summary>The tag of an application's data of AcDbBlockRepETag (its 1071); nought where it has none.</summary>
            private static int Tagged(IReadOnlyList<DxfTypedValue> data)
            {
                foreach (DxfTypedValue group in data ?? (IReadOnlyList<DxfTypedValue>)new DxfTypedValue[0])
                {
                    if (group.TypeCode == 1071 && int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int tag))
                    {
                        return tag;
                    }
                }

                return 0;
            }

            /// <summary>Whether a node of the id is in the graph.</summary>
            internal bool HasNode(int id) => _nodes.Exists(n => n[1] == id);

            /// <summary>The first of a name, then of the name with 1, 2 and on after it, that no element of the graph has, as AutoCAD names its parameters and actions.</summary>
            internal string FreeName(string name)
            {
                string free = name;

                for (int i = 1; _names.Contains(free); i++)
                {
                    free = name + i.ToString(CultureInfo.InvariantCulture);
                }

                _names.Add(free);
                return free;
            }

            internal void Add(int id, string handle)
            {
                _nodes.Add(new[] { 32, id });
                _handles.Add(handle);
            }

            /// <summary>An edge from one node to another, by their ids, of a kind (94): 1 to a grip's component, 2 to a parameter or an action.</summary>
            internal void Link(int from, int to, int kind) => _edges.Add(new[] { 0, kind, _nodes.FindIndex(n => n[1] == from), _nodes.FindIndex(n => n[1] == to) });

            /// <summary>Reads the nodes and edges the record holds, and the names of the elements of its nodes.</summary>
            private void Read()
            {
                List<DxfTypedValue> groups = File.TailOf(_item, 0);
                int at = groups.FindIndex(g => g.TypeCode == 100 && g.Value?.Trim() == "AcDbEvalGraph") + 1;

                if (at < groups.Count && groups[at].TypeCode == 96)
                {
                    _last = Number(groups[at].Value);
                }

                while (at < groups.Count && (groups[at].TypeCode == 96 || groups[at].TypeCode == 97))
                {
                    at++;
                }

                while (at + 7 < groups.Count && groups[at].TypeCode == 91)
                {
                    _nodes.Add(new[] { Number(groups[at + 1].Value), Number(groups[at + 2].Value) });
                    _handles.Add(groups[at + 3].Value?.Trim());
                    at += 8;
                }

                while (at + 9 < groups.Count && groups[at].TypeCode == 92)
                {
                    _edges.Add(new[] { Number(groups[at + 1].Value), Number(groups[at + 2].Value), Number(groups[at + 3].Value), Number(groups[at + 4].Value) });
                    at += 10;
                }

                foreach (string handle in _handles)
                {
                    DxfItem node = File.ObjectItem(handle);
                    IReadOnlyList<DxfTypedValue> own = node?.Groups ?? (IReadOnlyList<DxfTypedValue>)new DxfTypedValue[0];
                    int marker = -1;

                    for (int i = 0; i < own.Count; i++)
                    {
                        if (own[i].TypeCode == 100)
                        {
                            marker = own[i].Value?.Trim() == "AcDbBlockElement" ? i : -1;
                        }
                        else if (marker >= 0 && own[i].TypeCode == 300)
                        {
                            _names.Add(DxfReader.NameOf(own[i].Value ?? string.Empty));
                            break;
                        }
                    }
                }
            }

            /// <summary>Writes the record anew after its subclass marker: the last id given, twice, then the nodes and the edges.</summary>
            internal void Write()
            {
                List<DxfTypedValue> groups = File.TailOf(_item, 0);
                int from = groups.FindIndex(g => g.TypeCode == 100 && g.Value?.Trim() == "AcDbEvalGraph") + 1;
                var written = new List<KeyValuePair<int, string>>();
                string last = Int(NextId - 1);
                written.Add(Pair(96, last));
                written.Add(Pair(97, last));

                for (int i = 0; i < _nodes.Count; i++)
                {
                    List<int> into = Edges(3, i);
                    List<int> outOf = Edges(2, i);
                    written.Add(Pair(91, Int(i)));
                    written.Add(Pair(93, Int(_nodes[i][0])));
                    written.Add(Pair(95, Int(_nodes[i][1])));
                    written.Add(Pair(360, _handles[i]));
                    written.Add(Pair(92, Int(into.Count > 0 ? into[0] : -1)));
                    written.Add(Pair(92, Int(into.Count > 0 ? into[into.Count - 1] : -1)));
                    written.Add(Pair(92, Int(outOf.Count > 0 ? outOf[0] : -1)));
                    written.Add(Pair(92, Int(outOf.Count > 0 ? outOf[outOf.Count - 1] : -1)));
                }

                for (int k = 0; k < _edges.Count; k++)
                {
                    int[] edge = _edges[k];
                    List<int> into = Edges(3, edge[3]);
                    List<int> outOf = Edges(2, edge[2]);
                    int i = into.IndexOf(k);
                    int o = outOf.IndexOf(k);
                    written.Add(Pair(92, Int(k)));
                    written.Add(Pair(93, Int(edge[0])));
                    written.Add(Pair(94, Int(edge[1])));
                    written.Add(Pair(91, Int(edge[2])));
                    written.Add(Pair(91, Int(edge[3])));
                    written.Add(Pair(92, Int(i > 0 ? into[i - 1] : -1)));
                    written.Add(Pair(92, Int(i + 1 < into.Count ? into[i + 1] : -1)));
                    written.Add(Pair(92, Int(o > 0 ? outOf[o - 1] : -1)));
                    written.Add(Pair(92, Int(o + 1 < outOf.Count ? outOf[o + 1] : -1)));
                    written.Add(Pair(92, "-1"));
                }

                File.ReplaceTail(_item, from, written);
            }

            /// <summary>The indices of the edges whose end of a place (2 from, 3 to) is a node, in the order of the edges.</summary>
            private List<int> Edges(int end, int node)
            {
                var found = new List<int>();

                for (int k = 0; k < _edges.Count; k++)
                {
                    if (_edges[k][end] == node)
                    {
                        found.Add(k);
                    }
                }

                return found;
            }

            private static KeyValuePair<int, string> Pair(int code, string value) => new KeyValuePair<int, string>(code, value);

            private static int Number(string text) => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }
    }
}
