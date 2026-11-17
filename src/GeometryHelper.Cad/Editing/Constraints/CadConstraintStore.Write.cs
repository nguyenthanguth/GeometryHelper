using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// Writes the constraints of a space as AutoCAD 2021 keeps them (read off DXF files it wrote): in the associative network
    /// of the space's block record an <c>ACDBASSOC2DCONSTRAINTGROUP</c> holding the nodes (the geometry, its implicit points
    /// and the constraints between them, each a class, an id, the ids it is tied to and its groups), an
    /// <c>ACDBASSOCGEOMDEPENDENCY</c> for each object or polyline segment tied (a persistent reactor of the object), and for
    /// each dimensional constraint an <c>ACDBASSOCVARIABLE</c> with its value, a value dependency on it and a dependency on
    /// the DIMENSION that shows it. The objects of an earlier write are taken out first, so a write puts the set whole.
    /// </summary>
    internal static partial class CadConstraintStore
    {
        private const string Evaluator = "AcDbCalc:1.0";

        private static readonly string[] Classes =
        {
            "ACDBASSOCNETWORK|AcDbAssocNetwork",
            "ACDBASSOCACTION|AcDbAssocAction",
            "ACDBASSOCDEPENDENCY|AcDbAssocDependency",
            "ACDBASSOCGEOMDEPENDENCY|AcDbAssocGeomDependency",
            "ACDBASSOC2DCONSTRAINTGROUP|AcDbAssoc2dConstraintGroup",
            "ACDBASSOCVARIABLE|AcDbAssocVariable",
            "ACDBASSOCVALUEDEPENDENCY|AcDbAssocValueDependency",
            "ASSOCDIMDEPENDENCYBODY|AcDbAssocDimDependencyBody",
        };

        /// <summary>
        /// Writes a set into the network of a space, the objects an earlier write made taken out. A set with nothing in it
        /// leaves the space with none.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="space">The block of the space.</param>
        /// <param name="set">The constraints.</param>
        /// <param name="shapes">Each object the set ties, by handle, where it stands now.</param>
        /// <param name="entityOf">The object of a handle, for its reactors; the dimensions too.</param>
        internal static void Write(
            DxfDatabase database,
            DxfBlockTableRecord space,
            CadConstraintSet set,
            IReadOnlyDictionary<string, CadShape> shapes,
            Func<string, DxfEntity> entityOf
        )
        {
            Clear(database, space, entityOf);

            if (set.IsEmpty)
            {
                return;
            }

            DxfFile file = database.File;

            foreach (string name in Classes)
            {
                string[] parts = name.Split('|');
                file.AddClass(parts[0], parts[1], 1025);
                file.CountClass(parts[0]);
            }

            DxfItem network = CadCenterMarks.EnsureNetwork(database, space, out _);
            set.Network = network.Handle;
            new Writer(file, network, set, shapes, entityOf).Run();
        }

        /// <summary>Takes every constraint group and variable out of the network of a space, with what they own and the reactors that point at them.</summary>
        internal static void Clear(DxfDatabase database, DxfBlockTableRecord space, Func<string, DxfEntity> entityOf)
        {
            DxfItem network = NetworkOf(space);

            if (network == null)
            {
                return;
            }

            DxfFile file = database.File;

            foreach (string handle in CadCenterMarks.ActionsOf(network))
            {
                DxfItem action = file.ObjectItem(handle);

                if (action == null || action.IsRemoved)
                {
                    continue;
                }

                if (action.Type == "ACDBASSOC2DCONSTRAINTGROUP" || action.Type == "ACDBASSOCVARIABLE")
                {
                    foreach (string dependency in DependenciesOf(action))
                    {
                        RemoveDependency(file, dependency, entityOf);
                    }

                    CadCenterMarks.Unlist(file, network, handle);
                    file.RemoveRecord(action);
                }
            }
        }

        /// <summary>
        /// Gives each vertex of a light polyline the id AutoCAD names its edges by (group 91 of the vertex, 1 to the count where
        /// it has none): an edge of a constraint is written as the ids of its two vertices, and AutoCAD drops a constraint on an
        /// edge it cannot find by them.
        /// </summary>
        internal static void EnsureVertexIds(DxfFile file, DxfEntity polyline)
        {
            IReadOnlyList<DxfTypedValue> groups = polyline.Item.Groups;

            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == 91)
                {
                    return;
                }
            }

            List<KeyValuePair<int, string>> raw = CadCenterMarks.Raw(polyline.Item);
            var written = new List<KeyValuePair<int, string>>(raw.Count + raw.Count / 2);
            int id = 0;
            bool inside = false;

            for (int i = 0; i < raw.Count; i++)
            {
                written.Add(raw[i]);

                if (raw[i].Key == 100 && raw[i].Value.Trim() == "AcDbPolyline")
                {
                    inside = true;
                }
                else if (inside && raw[i].Key == 20 && i > 0 && raw[i - 1].Key == 10)
                {
                    id++;
                    written.Add(new KeyValuePair<int, string>(91, id.ToString(CultureInfo.InvariantCulture)));
                }
            }

            file.ReplaceTail(polyline.Item, 0, written);
        }

        /// <summary>The network of a space, where it holds one; null otherwise.</summary>
        internal static DxfItem NetworkOf(DxfBlockTableRecord space)
        {
            DxfDBObject entry = DxfDynamicProperties.Entry(space?.ExtensionDictionary, "ACAD_ASSOCNETWORK");
            DxfDBObject network = DxfDynamicProperties.Entry(entry as DxfDBDictionary, "ACAD_ASSOCNETWORK");
            return network?.Item != null && network.Item.Type == "ACDBASSOCNETWORK" ? network.Item : null;
        }

        /// <summary>The dependencies a group or a variable owns, by handle.</summary>
        private static List<string> DependenciesOf(DxfItem action)
        {
            var handles = new List<string>();
            IReadOnlyList<DxfTypedValue> groups = action.Groups;

            if (action.Type == "ACDBASSOC2DCONSTRAINTGROUP")
            {
                int at = CadCenterMarks.Start(groups, "AcDbAssocAction");

                // The plane's three points, then 360 0, then the count and the handles.
                for (int i = at; i + 1 < groups.Count; i++)
                {
                    if (
                        groups[i].TypeCode == 360
                        && groups[i].Value.Trim() == "0"
                        && i + 1 < groups.Count
                        && groups[i + 1].TypeCode == 90
                        && i > at + 12
                        && groups[i - 1].TypeCode == 30
                    )
                    {
                        if (
                            int.TryParse(
                                groups[i + 1].Value.Trim(),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out int count
                            )
                        )
                        {
                            for (int k = 0; k < count && i + 2 + k < groups.Count; k++)
                            {
                                handles.Add(groups[i + 2 + k].Value.Trim());
                            }
                        }

                        break;
                    }
                }
            }
            else
            {
                int at = CadCenterMarks.Start(groups, "AcDbAssocVariable");

                // Two 90s and four 1s ... the value, then the count of dependencies with each handle and its flag.
                if (at >= 0)
                {
                    int i = at + 1;
                    int texts = 0;

                    while (i < groups.Count && texts < 4)
                    {
                        if (groups[i].TypeCode == 1)
                        {
                            texts++;
                        }

                        i++;
                    }

                    // The value (90 or 40), two flags, then the count.
                    i += 3;

                    if (
                        i < groups.Count
                        && groups[i].TypeCode == 90
                        && int.TryParse(
                            groups[i].Value.Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int count
                        )
                    )
                    {
                        for (int k = 0; k < count && i + 1 + 2 * k < groups.Count; k++)
                        {
                            handles.Add(groups[i + 1 + 2 * k].Value.Trim());
                        }
                    }
                }
            }

            return handles;
        }

        private static void RemoveDependency(DxfFile file, string handle, Func<string, DxfEntity> entityOf)
        {
            DxfItem dependency = file.ObjectItem(handle);

            if (dependency == null || dependency.IsRemoved)
            {
                return;
            }

            IReadOnlyList<DxfTypedValue> groups = dependency.Groups;
            int at = CadCenterMarks.Start(groups, "AcDbAssocDependency");

            if (at >= 0 && at + 12 < groups.Count)
            {
                string target = groups[at + 7].Value.Trim();
                string previous = groups[at + 9].Value.Trim();
                string next = groups[at + 10].Value.Trim();
                string body = groups[at + 11].Value.Trim();

                if (previous != "0" && file.ObjectItem(previous) is DxfItem before)
                {
                    int b = CadCenterMarks.Start(before.Groups, "AcDbAssocDependency");

                    if (b >= 0)
                    {
                        file.SetValueAt(before, b + 10, next);
                    }
                }

                if (next != "0" && file.ObjectItem(next) is DxfItem after)
                {
                    int a = CadCenterMarks.Start(after.Groups, "AcDbAssocDependency");

                    if (a >= 0)
                    {
                        file.SetValueAt(after, a + 9, previous);
                    }
                }

                DxfEntity entity = entityOf(target);

                if (entity != null)
                {
                    file.SetReactor(entity.Item, handle, false);
                }
                else if (file.ObjectItem(target) is DxfItem other)
                {
                    file.SetReactor(other, handle, false);
                }

                if (body != "0" && file.ObjectItem(body) is DxfItem bodyItem)
                {
                    file.RemoveRecord(bodyItem);
                }
            }

            file.RemoveRecord(dependency);
        }

        /// <summary>One node of a constraint group: its class, its id, the ids it is tied to, its flags and its own groups.</summary>
        private sealed class Node
        {
            internal int Id;
            internal string Class;
            internal List<int> Links = new List<int>();
            internal int Flags;
            internal CadCenterMarks.Gs Body = new CadCenterMarks.Gs();
        }

        /// <summary>The nodes of one object or polyline segment.</summary>
        private sealed class Geometry
        {
            internal Node Own;
            internal Node Start;
            internal Node End;
            internal Node Mid;
            internal Node Center;
            internal CadShape Shape;
            internal string Handle;
            internal int Segment;
        }

        private sealed class Writer
        {
            private readonly DxfFile _file;
            private readonly DxfItem _network;
            private readonly CadConstraintSet _set;
            private readonly IReadOnlyDictionary<string, CadShape> _shapes;
            private readonly Func<string, DxfEntity> _entityOf;
            private readonly List<Node> _nodes = new List<Node>();
            private readonly Dictionary<string, Geometry> _geometry = new Dictionary<string, Geometry>(
                StringComparer.Ordinal
            );
            private readonly List<string> _dependencies = new List<string>();
            private readonly Dictionary<string, string> _geomDependency = new Dictionary<string, string>(
                StringComparer.Ordinal
            );
            private readonly Dictionary<string, string> _variableOf = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );
            private readonly Dictionary<string, DxfItem> _variableItems = new Dictionary<string, DxfItem>(
                StringComparer.OrdinalIgnoreCase
            );
            private readonly Dictionary<string, List<string>> _owned = new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase
            );
            private DxfItem _group;
            private int _next = 1;
            private int _order;
            private Node _datumX;
            private Node _datumY;

            internal Writer(
                DxfFile file,
                DxfItem network,
                CadConstraintSet set,
                IReadOnlyDictionary<string, CadShape> shapes,
                Func<string, DxfEntity> entityOf
            )
            {
                _file = file;
                _network = network;
                _set = set;
                _shapes = shapes;
                _entityOf = entityOf;
            }

            internal void Run()
            {
                // The variables first, empty, so that what depends on them can name them; the group the same.
                foreach (CadParameter parameter in _set.Parameters)
                {
                    DxfItem item = _file.AddObject("ACDBASSOCVARIABLE", null, b =>
                    {
                        b.Group(330, _network.Handle);
                        b.Group(100, "AcDbAssocAction");
                    });
                    parameter.Source = item.Handle;
                    _variableItems[parameter.Name] = item;
                    _owned[parameter.Name] = new List<string>();
                }

                _group = _file.AddObject("ACDBASSOC2DCONSTRAINTGROUP", null, b =>
                {
                    b.Group(330, _network.Handle);
                    b.Group(100, "AcDbAssocAction");
                });

                foreach (CadGeoConstraint constraint in _set.Geometric)
                {
                    AddGeometric(constraint);
                }

                var dimensional = new List<KeyValuePair<CadDimConstraint, Node>>();

                foreach (CadDimConstraint constraint in _set.Dimensional)
                {
                    dimensional.Add(new KeyValuePair<CadDimConstraint, Node>(constraint, AddDimensional(constraint)));
                }

                WriteGroup();
                WriteVariables();

                foreach (CadParameter parameter in _set.Parameters)
                {
                    CadCenterMarks.List(_file, _network, 360, parameter.Source);
                }

                CadCenterMarks.List(_file, _network, 360, _group.Handle);
            }

            #region Nodes

            private Node NewNode(string cls, int flags)
            {
                var node = new Node
                {
                    Id = _next++,
                    Class = cls,
                    Flags = flags,
                };
                _nodes.Add(node);
                return node;
            }

            private static void Tie(Node constraint, params Node[] tied)
            {
                foreach (Node node in tied)
                {
                    constraint.Links.Add(node.Id);
                    node.Links.Add(constraint.Id);
                }
            }

            private static string KeyOf(CadRef r) =>
                r.Handle.ToUpperInvariant() + "#" + r.Segment.ToString(CultureInfo.InvariantCulture);

            private Geometry GeometryOf(CadRef r)
            {
                string key = KeyOf(r);

                if (_geometry.TryGetValue(key, out Geometry found))
                {
                    return found;
                }

                CadShape shape = _shapes[r.Handle];
                var geometry = new Geometry
                {
                    Shape = shape,
                    Handle = r.Handle,
                    Segment = r.Segment,
                };
                string dependency = AddGeomDependency(r);
                bool line = shape.Kind == CadShapeKind.Line || shape.Kind == CadShapeKind.Polyline;
                geometry.Own = NewNode(
                    line ? "AcConstrainedBoundedLine"
                        : shape.Kind == CadShapeKind.Circle ? "AcConstrainedCircle"
                        : "AcConstrainedArc",
                    1
                );
                _geometry[key] = geometry;
                CadCenterMarks.Gs g = geometry.Own.Body;
                double[] v = shape.Values;
                g.G(330, dependency);
                g.G(90, 0);

                if (line)
                {
                    GeoPoint2 p = CadConstraintShapes.PointOf(shape, r.Of(CadPart.Start)).Value;
                    GeoPoint2 q = CadConstraintShapes.PointOf(shape, r.Of(CadPart.End)).Value;
                    double length = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
                    g.P(10, p.X, p.Y, 0.0);
                    g.P(10, (q.X - p.X) / length, (q.Y - p.Y) / length, 0.0);
                    g.G(290, 0);
                    g.P(10, p.X, p.Y, 0.0);
                    g.P(11, q.X, q.Y, 0.0);
                }
                else
                {
                    g.P(10, v[0], v[1], 0.0);
                    g.P(10, 0.0, 0.0, 1.0);
                    g.P(10, 1.0, 0.0, 0.0);
                    g.G(40, v[2]);
                    g.G(40, 0.0);
                    g.G(40, 2.0 * Math.PI);
                    g.G(40, 0.0);

                    if (shape.Kind == CadShapeKind.Arc)
                    {
                        g.P(10, v[3], v[4], 0.0);
                        g.P(11, v[5], v[6], 0.0);
                    }
                }

                if (line)
                {
                    geometry.Start = Implicit(geometry, 0, "AcPointCurveConstraint");
                    geometry.End = Implicit(geometry, 1, "AcPointCurveConstraint");
                    JoinPolyline(r, shape);
                }
                else
                {
                    if (shape.Kind == CadShapeKind.Arc)
                    {
                        geometry.Start = Implicit(geometry, 0, "AcPointCurveConstraint");
                        geometry.End = Implicit(geometry, 1, "AcPointCurveConstraint");
                    }

                    geometry.Center = Implicit(geometry, 3, "AcCenterPointConstraint");
                }

                return geometry;
            }

            /// <summary>An implicit point of a geometry and the constraint that holds it to its object.</summary>
            private Node Implicit(Geometry owner, int type, string holder)
            {
                Node point = NewNode("AcConstrainedImplicitPoint", 1);
                point.Body.G(330, "0");
                point.Body.G(90, 0);
                point.Body.G(280, type);
                point.Body.G(90, -1);
                point.Body.G(90, owner.Own.Id);
                Node held = NewNode(holder, 0);
                held.Body.G(90, 0);
                held.Body.G(290, 1);
                held.Body.G(290, 1);
                Tie(held, point, owner.Own);
                return point;
            }

            /// <summary>The point of a part of a geometry: its start, end, middle or centre, the middle made where first asked for.</summary>
            private Node PointOf(CadRef r)
            {
                Geometry geometry = GeometryOf(r);

                switch (r.Part)
                {
                    case CadPart.Start:
                        return geometry.Start;
                    case CadPart.End:
                        return geometry.End;
                    case CadPart.Center:
                        return geometry.Center;
                    default:
                        return geometry.Mid ?? (geometry.Mid = Implicit(geometry, 2, "AcMidPointConstraint"));
                }
            }

            /// <summary>
            /// A polyline's segments are joined where they meet: a segment made after its neighbour is held to it by a coincident
            /// constraint AutoCAD marks as its own (the first flag 1), which a constraint of the user's is not.
            /// </summary>
            private void JoinPolyline(CadRef r, CadShape shape)
            {
                if (shape.Kind != CadShapeKind.Polyline)
                {
                    return;
                }

                int count = shape.Segments;

                foreach (
                    int neighbour in new[]
                    {
                        r.Segment - 1 < 0 && shape.Closed ? count - 1 : r.Segment - 1,
                        r.Segment + 1 < count ? r.Segment + 1 : (shape.Closed ? 0 : -1),
                    }
                )
                {
                    if (neighbour < 0 || neighbour == r.Segment)
                    {
                        continue;
                    }

                    string other = r.Handle.ToUpperInvariant() + "#" + neighbour.ToString(CultureInfo.InvariantCulture);

                    if (!_geometry.TryGetValue(other, out Geometry near))
                    {
                        continue;
                    }

                    Geometry mine = _geometry[KeyOf(r)];
                    bool before = neighbour == (r.Segment - 1 + count) % count;
                    Node joined = NewNode("AcPointCoincidenceConstraint", 0);
                    joined.Body.G(90, 0);
                    joined.Body.G(290, 1);
                    joined.Body.G(290, 1);

                    if (before)
                    {
                        Tie(joined, near.End, mine.Start);
                    }
                    else
                    {
                        Tie(joined, mine.End, near.Start);
                    }
                }
            }

            private Node Datum(bool horizontal)
            {
                if (horizontal && _datumX != null)
                {
                    return _datumX;
                }

                if (!horizontal && _datumY != null)
                {
                    return _datumY;
                }

                // Both datum lines are made together, the x axis first, as AutoCAD 2021 makes them.
                _datumX = NewDatum(true);
                _datumY = NewDatum(false);
                return horizontal ? _datumX : _datumY;
            }

            private Node NewDatum(bool horizontal)
            {
                Node datum = NewNode("AcConstrainedDatumLine", 0);
                datum.Body.G(330, "0");
                datum.Body.G(90, 0);
                datum.Body.P(10, 0.0, 0.0, 0.0);
                datum.Body.P(10, horizontal ? 1.0 : 0.0, horizontal ? 0.0 : 1.0, 0.0);
                return datum;
            }

            /// <summary>Makes the nodes of what a constraint ties before the constraint's own, as AutoCAD numbers them.</summary>
            private void Prepare(IEnumerable<CadRef> refs)
            {
                foreach (CadRef r in refs)
                {
                    if (r.IsPoint)
                    {
                        PointOf(r);
                    }
                    else
                    {
                        GeometryOf(r);
                    }
                }
            }

            private void AddGeometric(CadGeoConstraint c)
            {
                Prepare(c.Refs);

                switch (c.Kind)
                {
                    case CadGeoKind.Coincident:
                    {
                        Node n = NewNode("AcPointCoincidenceConstraint", 0);
                        Plain(n, 0);
                        Tie(n, PointOf(c.Refs[0]), PointOf(c.Refs[1]));
                        break;
                    }

                    case CadGeoKind.PointOnCurve:
                    {
                        Node n = NewNode("AcPointCurveConstraint", 0);
                        Plain(n, 0);
                        Tie(n, PointOf(c.Refs[0]), GeometryOf(c.Refs[1]).Own);
                        break;
                    }

                    case CadGeoKind.Fix:
                    {
                        Node n = NewNode("AcFixedConstraint", 0);
                        Plain(n, 0);
                        Tie(n, PointOf(c.Refs[0]));
                        break;
                    }

                    case CadGeoKind.Horizontal:
                    case CadGeoKind.Vertical:
                    {
                        bool horizontal = c.Kind == CadGeoKind.Horizontal;

                        if (c.Refs.Count == 1)
                        {
                            Node datum = Datum(horizontal);
                            Node n = NewNode(horizontal ? "AcHorizontalConstraint" : "AcVerticalConstraint", 0);
                            Plain(n, 0);
                            n.Body.G(90, datum.Id);
                            Tie(n, GeometryOf(c.Refs[0]).Own);
                            break;
                        }

                        Node first = PointOf(c.Refs[0]);
                        Node second = PointOf(c.Refs[1]);
                        Node dat = Datum(horizontal);
                        Node through = NewNode("AcConstrained2PointsConstructionLine", 0);
                        CadShape s = _shapes[c.Refs[0].Handle];
                        GeoPoint2 at = CadConstraintShapes.PointOf(s, c.Refs[0]) ?? default;
                        through.Body.G(330, "0");
                        through.Body.G(90, 0);
                        through.Body.P(10, at.X, at.Y, 0.0);
                        through.Body.P(10, horizontal ? 1.0 : 0.0, horizontal ? 0.0 : 1.0, 0.0);

                        foreach (Node end in new[] { first, second })
                        {
                            Node held = NewNode("AcPointCurveConstraint", 0);
                            Plain(held, 1);
                            Tie(held, end, through);
                        }

                        Node tied = NewNode(horizontal ? "AcHorizontalConstraint" : "AcVerticalConstraint", 0);
                        Plain(tied, 0);
                        tied.Body.G(90, dat.Id);
                        Tie(tied, first, second);
                        break;
                    }

                    case CadGeoKind.Symmetric:
                    {
                        Node n = NewNode("AcSymmetricConstraint", 0);
                        Plain(n, 0);
                        bool points = c.Refs[0].IsPoint;
                        Tie(
                            n,
                            points ? PointOf(c.Refs[0]) : GeometryOf(c.Refs[0]).Own,
                            points ? PointOf(c.Refs[1]) : GeometryOf(c.Refs[1]).Own,
                            GeometryOf(c.Refs[2]).Own
                        );
                        break;
                    }

                    default:
                    {
                        string cls;

                        switch (c.Kind)
                        {
                            case CadGeoKind.Collinear:
                                cls = "AcColinearConstraint";
                                break;
                            case CadGeoKind.Concentric:
                                cls = "AcConcentricConstraint";
                                break;
                            case CadGeoKind.Parallel:
                                cls = "AcParallelConstraint";
                                break;
                            case CadGeoKind.Perpendicular:
                                cls = "AcPerpendicularConstraint";
                                break;
                            case CadGeoKind.Tangent:
                                cls = "AcTangentConstraint";
                                break;
                            case CadGeoKind.EqualLength:
                                cls = "AcEqualLengthConstraint";
                                break;
                            default:
                                cls = "AcEqualRadiusConstraint";
                                break;
                        }

                        Node n = NewNode(cls, 0);
                        Plain(n, 0);
                        Tie(n, GeometryOf(c.Refs[0]).Own, GeometryOf(c.Refs[1]).Own);
                        break;
                    }
                }
            }

            /// <summary>The groups every constraint node has: a 90, and two flags, the first 1 where AutoCAD made the constraint itself.</summary>
            private static void Plain(Node n, int own)
            {
                n.Body.G(90, 0);
                n.Body.G(290, own);
                n.Body.G(290, 1);
            }

            private Node AddDimensional(CadDimConstraint c)
            {
                Prepare(c.Refs);
                CadParameter parameter = _set.Parameter(c.Name);
                DxfItem variable = _variableItems[c.Name];
                string valueDependency = AddValueDependency(
                    variable.Handle,
                    _group.Handle,
                    parameter.Value,
                    parameter.Expression,
                    0
                );
                string dimensionDependency = c.DimensionHandle == null ? "0" : AddDimensionDependency(c);
                Node n;

                switch (c.Kind)
                {
                    case CadDimKind.Horizontal:
                    case CadDimKind.Vertical:
                    case CadDimKind.Aligned:
                        n = NewNode("AcDistanceConstraint", 0);
                        Plain(n, 0);
                        n.Body.G(340, valueDependency);
                        n.Body.G(340, dimensionDependency);

                        if (c.Kind == CadDimKind.Aligned)
                        {
                            n.Body.G(280, 0);
                        }
                        else
                        {
                            n.Body.G(280, 1);
                            double s = c.Direction >= 0 ? -1.0 : 1.0;
                            n.Body.P(
                                10,
                                c.Kind == CadDimKind.Horizontal ? s : 0.0,
                                c.Kind == CadDimKind.Vertical ? s : 0.0,
                                0.0
                            );
                        }

                        Tie(n, PointOf(c.Refs[0]), PointOf(c.Refs[1]));
                        break;
                    case CadDimKind.Angular:
                        n = NewNode("AcAngleConstraint", 0);
                        Plain(n, 0);
                        n.Body.G(340, valueDependency);
                        n.Body.G(340, dimensionDependency);
                        n.Body.G(280, 0);
                        Tie(n, GeometryOf(c.Refs[0]).Own, GeometryOf(c.Refs[1]).Own);
                        break;
                    default:
                        n = NewNode("AcRadiusDiameterConstraint", 0);
                        Plain(n, 0);
                        n.Body.G(340, valueDependency);
                        n.Body.G(340, dimensionDependency);
                        n.Body.G(280, c.Kind == CadDimKind.Diameter ? 1 : 0);
                        Tie(n, GeometryOf(c.Refs[0]).Own);
                        break;
                }

                return n;
            }

            #endregion

            #region Dependencies

            private static void Dependency(
                CadCenterMarks.Gs g,
                string owner,
                bool[] flags,
                int status,
                string target,
                string previous,
                string next,
                string body,
                int order,
                int priority
            )
            {
                g.G(330, owner);
                g.G(100, "AcDbAssocDependency");
                g.G(90, 2);
                g.G(90, status);

                foreach (bool flag in flags)
                {
                    g.G(290, flag ? 1 : 0);
                }

                g.G(90, priority);
                g.G(330, target);
                g.G(290, 0);
                g.G(330, previous);
                g.G(330, next);
                g.G(360, body);
                g.G(90, order);
            }

            private string AddGeomDependency(CadRef r)
            {
                DxfEntity entity = _entityOf(r.Handle);

                if (r.Segment >= 0)
                {
                    EnsureVertexIds(_file, entity);
                }

                string previous = LastDependency(entity);
                var g = new CadCenterMarks.Gs();
                Dependency(
                    g,
                    _group.Handle,
                    new[] { true, true, true, true },
                    0,
                    r.Handle,
                    previous,
                    "0",
                    "0",
                    ++_order,
                    -10000
                );
                g.G(100, "AcDbAssocGeomDependency");
                g.G(90, 0);
                g.G(290, 1);

                if (r.Segment >= 0)
                {
                    int[] ids = VertexIds(entity);
                    int count = ids.Length;
                    g.G(1, "AcDbAssocEdgePersSubentId");
                    g.G(90, 0);
                    g.G(90, ids[r.Segment]);
                    g.G(90, ids[(r.Segment + 1) % count]);
                }
                else
                {
                    g.G(1, "AcDbAssocSingleEdgePersSubentId");
                }

                g.G(290, 0);
                DxfItem made = _file.AddObject("ACDBASSOCGEOMDEPENDENCY", null, g.Emit);
                Chain(previous, made.Handle);
                _file.SetReactor(entity.Item, made.Handle, true);
                _dependencies.Add(made.Handle);
                return made.Handle;
            }

            private string AddValueDependency(string target, string owner, double value, string expression, int order)
            {
                string previous = "0";
                bool byGroup = order <= 0;

                if (_owned.TryGetValue(NameOf(target), out List<string> before) && before.Count > 0)
                {
                    previous = before[before.Count - 1];
                }

                var g = new CadCenterMarks.Gs();
                Dependency(
                    g,
                    owner,
                    new[] { true, false, true, true },
                    0,
                    target,
                    previous,
                    "0",
                    "0",
                    byGroup ? ++_order : order,
                    0
                );
                g.G(100, "AcDbAssocValueDependency");
                g.G(90, 0);
                g.G(1, string.Empty);
                Number(g, value, expression);
                DxfItem made = _file.AddObject("ACDBASSOCVALUEDEPENDENCY", null, g.Emit);
                Chain(previous, made.Handle);

                if (_owned.TryGetValue(NameOf(target), out List<string> list))
                {
                    list.Add(made.Handle);
                }

                _file.SetReactor(_file.ObjectItem(target), made.Handle, true);

                if (byGroup)
                {
                    _dependencies.Add(made.Handle);
                }

                return made.Handle;
            }

            private string NameOf(string variableHandle)
            {
                foreach (KeyValuePair<string, DxfItem> pair in _variableItems)
                {
                    if (string.Equals(pair.Value.Handle, variableHandle, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Key;
                    }
                }

                return string.Empty;
            }

            private string AddDimensionDependency(CadDimConstraint c)
            {
                DxfEntity dimension = _entityOf(c.DimensionHandle);
                DxfItem dependency = _file.AddObject("ACDBASSOCDEPENDENCY", null, b =>
                {
                    b.Group(330, _group.Handle);
                    b.Group(100, "AcDbAssocDependency");
                });
                string text = (dimension as DxfDimension)?.DimensionText ?? string.Empty;
                DxfItem body = _file.AddObject("ASSOCDIMDEPENDENCYBODY", null, b =>
                {
                    b.Group(330, dependency.Handle);
                    b.Group(100, "AcDbAssocDependencyBody");
                    b.Group(90, "1");
                    b.Group(100, "AcDbImpAssocDimDependencyBodyBase");
                    b.Group(90, "1");
                    b.Group(1, text);
                    b.Group(100, "AcDbAssocDimDependencyBody");
                    b.Group(90, "1");
                });
                var g = new CadCenterMarks.Gs();
                g.G(5, dependency.Handle);
                Dependency(
                    g,
                    _group.Handle,
                    new[] { false, true, true, true },
                    0,
                    c.DimensionHandle,
                    "0",
                    "0",
                    body.Handle,
                    ++_order,
                    0
                );
                _file.ReplaceTail(dependency, 0, g);
                _file.SetReactor(dimension.Item, dependency.Handle, true);
                _dependencies.Add(dependency.Handle);
                return dependency.Handle;
            }

            /// <summary>The last dependency on an object, which a new one follows in the chain; "0" where there is none.</summary>
            private string LastDependency(DxfEntity entity)
            {
                string last = "0";

                foreach (string reactor in entity.GetPersistentReactorIds())
                {
                    if (
                        _file.ObjectItem(reactor) is DxfItem item
                        && !item.IsRemoved
                        && item.Type == "ACDBASSOCGEOMDEPENDENCY"
                    )
                    {
                        last = item.Handle;
                    }
                }

                return last;
            }

            /// <summary>Makes a dependency the next of the one before it in a chain.</summary>
            private void Chain(string previous, string made)
            {
                if (previous != "0" && _file.ObjectItem(previous) is DxfItem before)
                {
                    int at = CadCenterMarks.Start(before.Groups, "AcDbAssocDependency");

                    if (at >= 0)
                    {
                        _file.SetValueAt(before, at + 10, made);
                    }
                }
            }

            private static void Number(CadCenterMarks.Gs g, double value, string expression)
            {
                if (IsWhole(value, expression))
                {
                    g.G(90, (int)Math.Round(value));
                }
                else
                {
                    g.G(40, value);
                }
            }

            /// <summary>Whether a value is kept as a whole number: it is one, and the expression that came to it holds nothing but whole numbers and + - *.</summary>
            internal static bool IsWhole(double value, string expression)
            {
                if (Math.Abs(value - Math.Round(value)) > 1E-12 || Math.Abs(value) > 1E9)
                {
                    return false;
                }

                foreach (char c in expression ?? string.Empty)
                {
                    if (c == '.' || c == '/' || c == '^' || c == '%' || c == '(')
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>The ids AutoCAD writes for the vertices of a light polyline (group 91), or 1, 2, 3 ... where it has none.</summary>
            private static int[] VertexIds(DxfEntity entity)
            {
                var ids = new List<int>();
                int vertices = (entity as DxfPolyline)?.NumberOfVertices ?? 0;

                foreach (DxfTypedValue group in entity.Item.Groups)
                {
                    if (
                        group.TypeCode == 91
                        && int.TryParse(
                            group.Value.Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int id
                        )
                    )
                    {
                        ids.Add(id);
                    }
                }

                if (ids.Count != vertices)
                {
                    ids.Clear();

                    for (int i = 0; i < vertices; i++)
                    {
                        ids.Add(i + 1);
                    }
                }

                return ids.ToArray();
            }

            #endregion

            #region Records

            private void WriteGroup()
            {
                var g = new CadCenterMarks.Gs();
                g.G(5, _group.Handle);
                g.G(330, _network.Handle);
                g.G(100, "AcDbAssocAction");
                g.G(90, 2);
                g.G(90, 0);
                g.G(330, _network.Handle);
                g.G(360, "0");
                g.G(90, CadCenterMarks.ActionsOf(_network).Count + _set.Parameters.Count + 1);
                g.G(90, _dependencies.Count);

                for (int i = 0; i < 5; i++)
                {
                    g.G(90, 0);
                }

                g.G(90, 2);
                g.G(70, 0);
                g.P(10, 0.0, 0.0, 0.0);
                g.P(10, 1.0, 0.0, 0.0);
                g.P(10, 0.0, 1.0, 0.0);
                g.G(360, "0");
                g.G(90, _dependencies.Count);

                foreach (string handle in _dependencies)
                {
                    g.G(360, handle);
                }

                int last = _nodes.Count == 0 ? 0 : _nodes[_nodes.Count - 1].Id;
                g.G(90, last);
                g.G(90, 0);
                g.G(90, 0);
                g.G(290, 0);
                g.G(90, _nodes.Count);

                foreach (Node node in _nodes)
                {
                    g.G(1, node.Class);
                    g.G(90, node.Id);
                }

                foreach (Node node in _nodes)
                {
                    g.G(90, node.Id);
                    g.G(90, node.Links.Count);

                    foreach (int link in node.Links)
                    {
                        g.G(90, link);
                    }

                    g.G(70, node.Flags);
                    g.AddRange(node.Body);
                }

                _file.ReplaceTail(_group, 0, g);
            }

            private void WriteVariables()
            {
                int index = CadCenterMarks.ActionsOf(_network).Count;
                var made = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                // A parameter made of others: a value dependency of its own on each name its expression reads, made for all
                // of them first, so that the tails written below name them and the reactors are put in after.
                foreach (CadParameter parameter in _set.Parameters)
                {
                    DxfItem item = _variableItems[parameter.Name];
                    var depends = new List<string>();

                    foreach (string name in CadConstraintExpression.NamesIn(parameter.Expression))
                    {
                        if (_variableItems.TryGetValue(name, out DxfItem read) && !ReferenceEquals(read, item))
                        {
                            CadParameter source = _set.Parameter(name);
                            depends.Add(
                                AddValueDependency(
                                    read.Handle,
                                    item.Handle,
                                    source.Value,
                                    source.Expression,
                                    depends.Count + 1
                                )
                            );
                        }
                    }

                    made[parameter.Name] = depends;
                }

                foreach (CadParameter parameter in _set.Parameters)
                {
                    index++;
                    DxfItem item = _variableItems[parameter.Name];
                    List<string> depends = made[parameter.Name];
                    var g = new CadCenterMarks.Gs();
                    g.G(5, item.Handle);
                    g.G(330, _network.Handle);
                    g.G(100, "AcDbAssocAction");
                    g.G(90, 2);
                    g.G(90, 0);
                    g.G(330, _network.Handle);
                    g.G(360, "0");
                    g.G(90, index);
                    g.G(90, depends.Count);

                    for (int i = 0; i < 5; i++)
                    {
                        g.G(90, 0);
                    }

                    g.G(100, "AcDbAssocVariable");
                    g.G(90, 2);
                    g.G(1, parameter.Name);
                    g.G(
                        1,
                        IsPlainNumber(parameter) && !IsWhole(parameter.Value, parameter.Expression) && !parameter.IsUser
                            ? Plain(parameter.Value)
                            : parameter.Expression
                    );
                    g.G(1, Evaluator);
                    g.G(1, string.Empty);
                    Number(g, parameter.Value, parameter.Expression);
                    g.G(290, 0);
                    g.G(290, 0);
                    g.G(90, depends.Count);

                    for (int i = 0; i < depends.Count; i++)
                    {
                        g.G(360, depends[i]);
                        g.G(90, i);
                    }

                    _file.ReplaceTail(item, 0, g);
                }

                // The reactors of each variable, the dependencies on it: written after the tails, which replace them.
                foreach (CadParameter parameter in _set.Parameters)
                {
                    foreach (string dependency in _owned[parameter.Name])
                    {
                        _file.SetReactor(_variableItems[parameter.Name], dependency, true);
                    }
                }
            }

            private static bool IsPlainNumber(CadParameter parameter) =>
                string.IsNullOrEmpty(parameter.Expression)
                || double.TryParse(parameter.Expression, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

            private static string Plain(double value) =>
                Math.Round(value, 10).ToString("0.##########", CultureInfo.InvariantCulture);

            #endregion
        }
    }
}
