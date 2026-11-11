using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The geometric and dimensional constraints of a block, as AutoCAD keeps them in the associative network of its
    /// BLOCK_RECORD (ACAD_ASSOCNETWORK of its extension dictionary): the constraint groups (ACDBASSOC2DCONSTRAINTGROUP), each a
    /// list of nodes, the geometry (bounded lines, arcs, circles, datum lines and the implicit points of their ends, middles
    /// and centres, each line, arc or circle tied by an ACDBASSOCGEOMDEPENDENCY to a LINE, an ARC, a CIRCLE or an edge of an
    /// LWPOLYLINE of the block) and the constraints between them; and the variables (ACDBASSOCVARIABLE) the dimensional
    /// constraints read their values from, some worked out from expressions of others. Solved at new values of variables by
    /// Newton's method from the block's own geometry, as AutoCAD 2021 solved its Dynamic Blocks samples once
    /// <c>AssocManager.EvaluateTopLevelNetwork</c> ran (plugin D27): its Fluorescent, Window and Channel to 1E-12 at 18 values
    /// set. Geometry the constraints leave free keeps the base point of each constraint parameter set where it is along its
    /// distance, as AutoCAD 2021 kept the left side of its Door Elevation at a new Width where the least change would move both
    /// sides; what is still free is held where the block has it, the radii and the arcs' ends first where all at once cannot
    /// be met, as it kept the fillets of its Hex Head Bolt whole (plugin D27b). A group of R2018 (AC1032) is read as one of
    /// R2010, and a copy of the network is written into an anonymous block standing for the block (DxfBlockConstraints.Network).
    /// </summary>
    internal sealed partial class DxfBlockConstraints
    {
        private const double Turn = 2.0 * Math.PI;

        // The nodes of every group by their group's handle and id, the geometry of unknowns in the order read, the constraints
        // in the order read, and the variables in the order of the network.
        private readonly Dictionary<string, Node> _nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
        private readonly List<Node> _shapes = new List<Node>();
        private readonly List<Rule> _rules = new List<Rule>();
        private readonly List<Variable> _variables = new List<Variable>();
        private readonly Dictionary<string, Rule> _parameters = new Dictionary<string, Rule>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly List<double> _start = new List<double>();
        private DxfFile _file;
        private string _block;
        private DxfItem _network;

        /// <summary>Why the constraints cannot be solved, where they cannot: a kind of node or a tie the library does not work out; null where they can.</summary>
        internal string Refusal { get; private set; }

        /// <summary>
        /// The constraints of a block; null where it has no associative network. Read whole, a node of a kind the library does
        /// not work out recorded in <see cref="Refusal"/> rather than thrown.
        /// </summary>
        internal static DxfBlockConstraints Of(DxfBlockTableRecord block)
        {
            DxfDBObject entry = DxfDynamicProperties.Entry(block?.ExtensionDictionary, "ACAD_ASSOCNETWORK");
            DxfDBObject network = DxfDynamicProperties.Entry(entry as DxfDBDictionary, "ACAD_ASSOCNETWORK");

            if (network?.Item == null || network.Item.Type != "ACDBASSOCNETWORK")
            {
                return null;
            }

            var constraints = new DxfBlockConstraints
            {
                _file = network.Item.File,
                _block = block.Name,
                _network = network.Item,
            };
            constraints.Read(network.Item);
            return constraints;
        }

        /// <summary>The dimensional constraint a constraint parameter of the block's graph drives, by the parameter's handle; null where none does.</summary>
        internal Dimension DimensionOf(string parameter) =>
            _parameters.TryGetValue(parameter ?? string.Empty, out Rule rule) ? rule.Dimension : null;

        private void Read(DxfItem network)
        {
            List<DxfTypedValue> own = DxfDynamicProperties.Section(network.Groups, "AcDbAssocNetwork");
            var groups = new List<DxfItem>();
            var dependencies = new Dictionary<string, Variable>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfTypedValue group in own)
            {
                if (group.TypeCode != 360 || !(_file.ObjectItem(group.Value) is DxfItem action))
                {
                    continue;
                }

                if (action.Type == "ACDBASSOC2DCONSTRAINTGROUP")
                {
                    groups.Add(action);
                }
                else if (action.Type == "ACDBASSOCVARIABLE")
                {
                    Variable variable = Variable.Of(action);

                    if (variable == null)
                    {
                        Refuse("a variable it cannot read");
                    }
                    else
                    {
                        _variables.Add(variable);
                    }
                }
            }

            foreach (DxfItem group in groups)
            {
                ReadGroup(group);
            }

            foreach (Rule rule in _rules)
            {
                Tie(rule);
            }
        }

        private void Refuse(string why)
        {
            if (Refusal == null)
            {
                Refusal =
                    "The constraints of the block "
                    + _block
                    + " hold "
                    + why
                    + ", which the library does not work out.";
            }
        }

        /// <summary>
        /// Reads the nodes of a constraint group. A group of R2010 (AC1024) writes each node as its class (1), its group (330),
        /// its id (90), its flags (70) and the ids of the nodes it is tied to (a count, 90, and as many 90s), then the groups
        /// of its class, up to the next class; one of R2018 (AC1032), as AutoCAD 2021 saved its samples (plugin D27b), lists
        /// the classes and ids of its nodes first (a 1 and a 90 each) and writes each node as its id, the count and the ids it
        /// is tied to, its flags (70) and the same groups of its class, up to the next node's id so followed.
        /// </summary>
        private void ReadGroup(DxfItem group)
        {
            IReadOnlyList<DxfTypedValue> groups = group.Groups;
            int marker = -1;

            for (int i = 0; i < groups.Count && marker < 0; i++)
            {
                if (groups[i].TypeCode == 100 && groups[i].Value?.Trim() == "AcDbAssocAction")
                {
                    marker = i;
                }
            }

            List<DxfTypedValue> head = DxfDynamicProperties.Section(groups, "AcDbAssocAction");
            int first = head.FindIndex(g => g.TypeCode == 1);
            List<double[]> plane = Points(head.GetRange(0, first < 0 ? head.Count : first));

            // The group's work plane: its origin and its axes, the block's own as every sample has it.
            if (
                plane.Count < 3
                || !Same(plane[0], 0, 0)
                || !Same(plane[1], 1, 0)
                || !Same(plane[2], 0, 1)
                || plane[0][2] != 0.0
                || plane[1][2] != 0.0
                || plane[2][2] != 0.0
            )
            {
                Refuse("a group off the block's own plane");
                return;
            }

            var read = new List<KeyValuePair<Node, List<DxfTypedValue>>>();

            if (first > 0 && first + 1 < head.Count && head[first + 1].TypeCode != 330)
            {
                if (!ReadListed(group, head, first, marker + 1, read))
                {
                    Refuse("a node it cannot read");
                    return;
                }
            }

            for (int i = read.Count > 0 ? -1 : first; i >= 0 && i < head.Count; )
            {
                int next = head.FindIndex(i + 1, g => g.TypeCode == 1);
                List<DxfTypedValue> tail = head.GetRange(i + 1, (next < 0 ? head.Count : next) - i - 1);

                if (
                    tail.Count < 4
                    || tail[0].TypeCode != 330
                    || !Whole(tail[1], out int id)
                    || !Whole(tail[3], out int count)
                    || count < 0
                    || tail.Count < 4 + count
                )
                {
                    Refuse("a node it cannot read");
                    return;
                }

                var node = new Node
                {
                    Class = head[i].Value?.Trim() ?? string.Empty,
                    Key = group.Handle + ":" + id.ToString(CultureInfo.InvariantCulture),
                    Group = group,
                    DataAt = marker + 1 + i + 5 + count,
                };

                for (int k = 0; k < count; k++)
                {
                    node.LinkIds.Add(Whole(tail[4 + k], out int link) ? link : int.MinValue);
                }

                _nodes[node.Key] = node;
                read.Add(
                    new KeyValuePair<Node, List<DxfTypedValue>>(node, tail.GetRange(4 + count, tail.Count - 4 - count))
                );
                i = next;
            }

            foreach (KeyValuePair<Node, List<DxfTypedValue>> pair in read)
            {
                Node node = pair.Key;
                List<DxfTypedValue> rest = pair.Value;

                foreach (int link in node.LinkIds)
                {
                    node.Links.Add(
                        _nodes.TryGetValue(
                            group.Handle + ":" + link.ToString(CultureInfo.InvariantCulture),
                            out Node linked
                        )
                            ? linked
                            : null
                    );
                }

                if (node.Class.StartsWith("AcConstrained", StringComparison.Ordinal))
                {
                    Shape(node, rest, group.Handle);
                }
                else
                {
                    _rules.Add(
                        new Rule
                        {
                            Class = node.Class,
                            Links = node.Links,
                            Rest = rest,
                            Group = group.Handle,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Reads the nodes of a group of R2018: the list of their classes and ids from the place given, then each node in the
        /// list's order, as <see cref="ReadGroup"/> says; false where they cannot be read so. The place of each node's groups
        /// of its class is kept counted in the group's record, from the offset given.
        /// </summary>
        private bool ReadListed(
            DxfItem group,
            List<DxfTypedValue> head,
            int from,
            int offset,
            List<KeyValuePair<Node, List<DxfTypedValue>>> read
        )
        {
            var listed = new List<KeyValuePair<int, string>>();
            int at = from;

            while (
                at + 1 < head.Count
                && head[at].TypeCode == 1
                && head[at + 1].TypeCode == 90
                && Whole(head[at + 1], out int id)
            )
            {
                listed.Add(new KeyValuePair<int, string>(id, head[at].Value?.Trim() ?? string.Empty));
                at += 2;
            }

            for (int k = 0; k < listed.Count; k++)
            {
                if (!Opens(head, at, listed[k].Key, out int count))
                {
                    return false;
                }

                int data = at + 3 + count;
                int end = head.Count;

                if (k + 1 < listed.Count)
                {
                    end = -1;

                    for (int p = data; p < head.Count && end < 0; p++)
                    {
                        if (Opens(head, p, listed[k + 1].Key, out _))
                        {
                            end = p;
                        }
                    }

                    if (end < 0)
                    {
                        return false;
                    }
                }

                var node = new Node
                {
                    Class = listed[k].Value,
                    Key = group.Handle + ":" + listed[k].Key.ToString(CultureInfo.InvariantCulture),
                    Group = group,
                    DataAt = offset + data,
                };

                for (int i = 0; i < count; i++)
                {
                    node.LinkIds.Add(Whole(head[at + 2 + i], out int link) ? link : int.MinValue);
                }

                _nodes[node.Key] = node;
                read.Add(new KeyValuePair<Node, List<DxfTypedValue>>(node, head.GetRange(data, end - data)));
                at = end;
            }

            return listed.Count > 0;
        }

        /// <summary>Whether a node of a group of R2018 opens at a place: its id given (90), a count (90), as many ids (90) and its flags (70).</summary>
        private static bool Opens(List<DxfTypedValue> head, int at, int id, out int count)
        {
            count = 0;

            if (
                at + 1 >= head.Count
                || head[at].TypeCode != 90
                || head[at + 1].TypeCode != 90
                || !Whole(head[at], out int said)
                || said != id
                || !Whole(head[at + 1], out count)
                || count < 0
                || at + 2 + count >= head.Count
            )
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (head[at + 2 + i].TypeCode != 90)
                {
                    return false;
                }
            }

            return head[at + 2 + count].TypeCode == 70;
        }

        /// <summary>Reads a node of geometry: its unknowns from where the block has it, or its datum, or its owner and kind of point.</summary>
        private void Shape(Node node, List<DxfTypedValue> rest, string group)
        {
            List<double[]> points = Points(rest);
            List<double> reals = rest.FindAll(g => g.TypeCode == 40)
                .ConvertAll(g => DxfDynamicProperties.Number(g.Value, out double v) ? v : double.NaN);
            node.Dependency = rest.Count > 0 && rest[0].TypeCode == 330 ? rest[0].Value?.Trim() : null;

            switch (node.Class)
            {
                case "AcConstrainedBoundedLine":
                    if (points.Count < 4)
                    {
                        break;
                    }

                    node.Kind = Kind.Line;
                    Unknowns(node, points[2][0], points[2][1], points[3][0], points[3][1]);
                    return;
                case "AcConstrainedArc":
                    if (points.Count < 5 || reals.Count < 1 || !Same(points[1], 0, 0) || points[1][2] <= 0.0)
                    {
                        break;
                    }

                    node.Kind = Kind.Arc;
                    double[] c = points[0];
                    Unknowns(
                        node,
                        c[0],
                        c[1],
                        reals[0],
                        Math.Atan2(points[3][1] - c[1], points[3][0] - c[0]),
                        Math.Atan2(points[4][1] - c[1], points[4][0] - c[0])
                    );
                    return;
                case "AcConstrainedCircle":
                    if (points.Count < 3 || reals.Count < 1 || !Same(points[1], 0, 0) || points[1][2] <= 0.0)
                    {
                        break;
                    }

                    node.Kind = Kind.Circle;
                    Unknowns(node, points[0][0], points[0][1], reals[0]);
                    return;
                case "AcConstrainedDatumLine":
                    if (points.Count < 2)
                    {
                        break;
                    }

                    node.Kind = Kind.Datum;
                    node.Datum = new[] { points[1][0], points[1][1] };
                    return;
                case "AcConstrained2PointsConstructionLine":
                    // A line of no entity, through its point along its direction, as the Hex Head Bolt's lines through the
                    // centres and the ends of its fillets: two points of it are its unknowns.
                    if (
                        points.Count < 2
                        || node.Dependency != "0"
                        || !(Math.Abs(points[1][0]) + Math.Abs(points[1][1]) > 0.0)
                    )
                    {
                        break;
                    }

                    node.Kind = Kind.Construction;
                    Unknowns(
                        node,
                        points[0][0],
                        points[0][1],
                        points[0][0] + points[1][0],
                        points[0][1] + points[1][1]
                    );
                    return;
                case "AcConstrainedImplicitPoint":
                    if (
                        rest.Count < 5
                        || !Whole(rest[2], out int type)
                        || !Whole(rest[4], out int owner)
                        || !_nodes.TryGetValue(group + ":" + owner.ToString(CultureInfo.InvariantCulture), out Node of)
                    )
                    {
                        break;
                    }

                    node.Kind = Kind.Point;
                    node.PointType = type;
                    node.Owner = of;
                    return;
            }

            Refuse("an " + node.Class);
        }

        private void Unknowns(Node node, params double[] values)
        {
            node.At = _start.Count;
            _start.AddRange(values);
            _shapes.Add(node);
        }

        /// <summary>
        /// Checks a constraint's ties: the kinds of geometry each class is tied to, as the samples tie them; and, of a
        /// dimensional constraint, its variable (the object of the value dependency, its first 340) and the constraint
        /// parameter of the block's graph that drives it (the object of its second 340), where one does.
        /// </summary>
        private void Tie(Rule rule)
        {
            Node a = rule.Links.Count > 0 ? rule.Links[0] : null;
            Node b = rule.Links.Count > 1 ? rule.Links[1] : null;

            foreach (Node point in rule.Links)
            {
                if (point != null && point.Kind == Kind.Point && !Placed(point))
                {
                    Refuse("a point of an " + point.Owner.Class);
                    return;
                }
            }

            bool ok;

            switch (rule.Class)
            {
                case "AcPointCurveConstraint":
                    ok =
                        Is(a, Kind.Point)
                        && (Is(b, Kind.Line) || Is(b, Kind.Construction) || Is(b, Kind.Arc) || Is(b, Kind.Circle));
                    break;
                case "AcPointCoincidenceConstraint":
                    ok = Is(a, Kind.Point) && Is(b, Kind.Point);
                    break;
                case "AcParallelConstraint":
                case "AcPerpendicularConstraint":
                case "AcColinearConstraint":
                case "AcEqualLengthConstraint":
                    ok = Is(a, Kind.Line) && Is(b, Kind.Line);
                    break;
                case "AcHorizontalConstraint":
                    rule.Datum =
                        rule.Rest.Count > 3
                        && Whole(rule.Rest[3], out int datum)
                        && _nodes.TryGetValue(
                            rule.Group + ":" + datum.ToString(CultureInfo.InvariantCulture),
                            out Node line
                        )
                            ? line
                            : null;
                    ok = rule.Links.Count == 1 && Is(a, Kind.Line) && Is(rule.Datum, Kind.Datum);
                    break;
                case "AcEqualRadiusConstraint":
                    ok = (Is(a, Kind.Arc) || Is(a, Kind.Circle)) && (Is(b, Kind.Arc) || Is(b, Kind.Circle));
                    break;
                case "AcMidPointConstraint":
                    ok = Is(a, Kind.Point) && Is(b, Kind.Line);
                    break;
                case "AcCenterPointConstraint":
                    ok = Is(a, Kind.Point) && (Is(b, Kind.Arc) || Is(b, Kind.Circle));
                    break;
                case "AcFixedConstraint":
                    ok = rule.Links.Count > 0 && rule.Links.TrueForAll(n => Is(n, Kind.Point));
                    break;
                case "AcTangentConstraint":
                    ok =
                        (Is(a, Kind.Line) || Is(a, Kind.Arc) || Is(a, Kind.Circle))
                        && (Is(b, Kind.Line) || Is(b, Kind.Arc) || Is(b, Kind.Circle))
                        && !(Is(a, Kind.Line) && Is(b, Kind.Line));
                    break;
                case "AcDistanceConstraint":
                    ok = Is(a, Kind.Point) && Is(b, Kind.Point) && Dimensional(rule, true);
                    break;
                case "AcAngleConstraint":
                    ok = Is(a, Kind.Line) && Is(b, Kind.Line) && Dimensional(rule, false);
                    break;
                case "Ac3PointAngleConstraint":
                    ok =
                        rule.Links.Count == 3
                        && rule.Links.TrueForAll(n => Is(n, Kind.Point))
                        && Dimensional(rule, false);
                    break;
                case "AcRadiusDiameterConstraint":
                    ok =
                        rule.Links.Count == 1
                        && (Is(a, Kind.Arc) || Is(a, Kind.Circle))
                        && Dimensional(rule, false)
                        && Flag(rule, 280) == 0;
                    break;
                default:
                    Refuse("an " + rule.Class);
                    return;
            }

            if (!ok)
            {
                Refuse("an " + rule.Class + " tied to geometry the library does not tie it to");
            }
        }

        /// <summary>
        /// Reads a dimensional constraint's variable and its direction, where it measures along one (a distance of 280 1 along
        /// its point 10, as every sample's measures along x or y); false where the variable cannot be found or the distance is
        /// measured another way.
        /// </summary>
        private bool Dimensional(Rule rule, bool distance)
        {
            List<DxfTypedValue> handles = rule.Rest.FindAll(g => g.TypeCode == 340);

            if (handles.Count < 2)
            {
                return false;
            }

            DxfItem value = _file.ObjectItem(handles[0].Value);
            string held =
                value == null
                    ? null
                    : DxfDynamicProperties
                        .Text(DxfDynamicProperties.Section(value.Groups, "AcDbAssocDependency"), 330)
                        ?.Trim();
            rule.Variable = _variables.Find(v => string.Equals(v.Handle, held, StringComparison.OrdinalIgnoreCase));

            if (rule.Variable == null || !(rule.Variable.Value is double))
            {
                return false;
            }

            if (distance)
            {
                List<double[]> along = Points(rule.Rest);

                if (
                    Flag(rule, 280) != 1
                    || along.Count != 1
                    || Math.Abs(Math.Sqrt(along[0][0] * along[0][0] + along[0][1] * along[0][1]) - 1.0) > 1E-9
                    || along[0][2] != 0.0
                )
                {
                    return false;
                }

                rule.Along = new[] { along[0][0], along[0][1] };
            }

            DxfItem tie = _file.ObjectItem(handles[1].Value);
            string driver =
                tie == null
                    ? null
                    : DxfDynamicProperties
                        .Text(DxfDynamicProperties.Section(tie.Groups, "AcDbAssocDependency"), 330)
                        ?.Trim();

            if (
                driver != null
                && _file.ObjectItem(driver) is DxfItem parameter
                && parameter.Type.StartsWith("BLOCK", StringComparison.Ordinal)
                && parameter.Type.EndsWith("CONSTRAINTPARAMETER", StringComparison.Ordinal)
            )
            {
                rule.Dimension = new Dimension(_rules.IndexOf(rule), rule.Variable.Name);
                _parameters[parameter.Handle] = rule;
            }

            return true;
        }

        private static int Flag(Rule rule, int code) =>
            rule.Rest.Find(g => g.TypeCode == code) is DxfTypedValue g && Whole(g, out int flag) ? flag : -1;

        private static bool Is(Node node, Kind kind) => node != null && node.Kind == kind;

        /// <summary>Whether a point is one the library places: the start (0), the end (1) or the middle (2) of a line, the start, the end or the centre (3) of an arc, the centre of a circle.</summary>
        private static bool Placed(Node point)
        {
            switch (point.Owner.Kind)
            {
                case Kind.Line:
                    return point.PointType >= 0 && point.PointType <= 2;
                case Kind.Arc:
                    return point.PointType == 0 || point.PointType == 1 || point.PointType == 3;
                case Kind.Circle:
                    return point.PointType == 3;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Solves the constraints at the values of the variables given, by name (letters of either case the same), the others
        /// at the values the network keeps, those of an expression worked out from it: null where no dimensional constraint
        /// then measures other than at the block's own values, else the geometry solved; the base point of each parameter whose
        /// distance changes, by its variable's name (<paramref name="bases"/>), kept as <see cref="DxfBlockConstraints"/> says.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when the constraints hold a node the library does not work out, an
        /// expression it does not evaluate, or cannot be met at the values.</exception>
        internal Solution Solve(IDictionary<string, double> given, IDictionary<string, GeoPoint3> bases)
        {
            if (Refusal != null)
            {
                throw new NotSupportedException(Refusal);
            }

            Dictionary<string, double> before = Values(
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            );
            Dictionary<string, double> after = Values(given);
            bool changed = false;

            foreach (Rule rule in _rules)
            {
                if (rule.Variable != null && !Near(before[rule.Variable.Name], after[rule.Variable.Name]))
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                return null;
            }

            double[] start = _start.ToArray();
            var kept = new List<Func<double[], double>>();

            // The base point of each constraint parameter whose distance changes is kept where it is along the distance, as a
            // linear parameter keeps its own: AutoCAD 2021 kept its Door Elevation's left side at a new Width (plugin D27b).
            foreach (Rule rule in _rules)
            {
                if (
                    rule.Dimension != null
                    && rule.Class == "AcDistanceConstraint"
                    && bases != null
                    && bases.TryGetValue(rule.Variable.Name, out GeoPoint3 basePoint)
                    && !Near(before[rule.Variable.Name], after[rule.Variable.Name])
                )
                {
                    double[] p = Point(rule.Links[0], start);
                    double[] q = Point(rule.Links[1], start);
                    Node at =
                        Math.Abs(p[0] - basePoint.X) + Math.Abs(p[1] - basePoint.Y)
                        <= Math.Abs(q[0] - basePoint.X) + Math.Abs(q[1] - basePoint.Y)
                            ? rule.Links[0]
                            : rule.Links[1];
                    double[] own = Point(at, start);
                    double[] along = rule.Along;
                    kept.Add(x => Dot(Minus(Point(at, x), own), along));
                }
            }

            // The decomposition of the equations at the block's own geometry, by the list of those added to them: the base-point
            // check and the first stage of each attempt with the same equations read the one made first.
            var none = new List<Func<double[], double>>();
            var decomposed = new Dictionary<List<Func<double[], double>>, Decomposition>();

            // A base point the constraints fix already is not kept again: keeping it adds nothing where it need not move, and
            // makes the equations unmeetable where it must.
            if (kept.Count > 0)
            {
                List<Func<double[], double>> residuals = Residuals(start, before, after);
                Decomposition plain = decomposed[none] = Decompose(Jacobian(residuals, start), residuals.Count);
                double[][] gradients = Jacobian(kept, start);
                var free = new List<Func<double[], double>>();

                for (int i = 0; i < kept.Count; i++)
                {
                    double size = 0.0;
                    double weight = 0.0;

                    for (int j = 0; j < start.Length; j++)
                    {
                        size += gradients[j][i] * gradients[j][i];
                    }

                    for (int k = 0; k < plain.Singular.Length; k++)
                    {
                        if (!(plain.Singular[k] > 1E-8 * plain.Largest))
                        {
                            double along = 0.0;
                            double[] vector = plain.Right[k];

                            for (int j = 0; j < start.Length; j++)
                            {
                                along += gradients[j][i] * vector[j];
                            }

                            weight += along * along;
                        }
                    }

                    if (size > 0.0 && Math.Sqrt(weight / size) > 1E-6)
                    {
                        free.Add(kept[i]);
                    }
                }

                kept = free;
            }

            // Tried in turn: the base points kept, then not, the free unknowns held all at once, then staged.
            bool freed = false;
            double[] solved = kept.Count > 0 ? Attempt(start, before, after, kept, false, decomposed, out _) : null;
            solved = solved ?? Attempt(start, before, after, none, false, decomposed, out freed);
            solved = solved ?? (kept.Count > 0 ? Attempt(start, before, after, kept, true, decomposed, out _) : null);
            solved = solved ?? Attempt(start, before, after, none, true, decomposed, out _);

            if (solved == null)
            {
                throw new NotSupportedException(
                    freed
                        ? "The constraints of the block "
                            + _block
                            + " cannot be met at these values without moving geometry they leave free other than as AutoCAD 2021 moved it (each parameter's base point kept, the rest held, radii and the ends of arcs first): the library does not set them."
                        : "The constraints of the block " + _block + " cannot be met at these values."
                );
            }

            foreach (Node shape in _shapes)
            {
                if (
                    (
                        (shape.Kind == Kind.Line || shape.Kind == Kind.Construction)
                        && !(
                            Math.Abs(solved[shape.At + 2] - solved[shape.At])
                                + Math.Abs(solved[shape.At + 3] - solved[shape.At + 1])
                            > 0.0
                        )
                    ) || ((shape.Kind == Kind.Arc || shape.Kind == Kind.Circle) && !(solved[shape.At + 2] > 0.0))
                )
                {
                    throw new NotSupportedException(
                        "The constraints of the block "
                            + _block
                            + " at these values take a line to a point or an arc to no radius."
                    );
                }
            }

            return new Solution(this, solved);
        }

        /// <summary>
        /// Solves the equations of the constraints and those given, from the block's own geometry, the unknowns they leave
        /// free held, whether any is so; null where Newton's method does not meet them. The decomposition of the equations and
        /// those given at the block's own geometry is read from those given by the list of the equations given, where made
        /// before, and put there where not; a stage that holds nothing more reads the one before's.
        /// </summary>
        private double[] Attempt(
            double[] start,
            Dictionary<string, double> before,
            Dictionary<string, double> after,
            List<Func<double[], double>> extra,
            bool staged,
            Dictionary<List<Func<double[], double>>, Decomposition> decomposed,
            out bool freed
        )
        {
            List<Func<double[], double>> residuals = Residuals(start, before, after);
            residuals.AddRange(extra);
            int held = residuals.Count;

            // The unknowns the constraints leave free about the block's own geometry (the null space of their Jacobian there)
            // are held where they are: a solution that keeps them is the one AutoCAD's least change gave in every sample. A
            // construction line's two points, free to slide along it, are not: it is drawn nowhere. Staged, the radii are held
            // first, the angles of the arcs' ends next and the rest last, each stage of the null space left by those before,
            // as AutoCAD 2021 kept the fillets of its Hex Head Bolt whole and moved their centres (plugin D27b).
            var stage = new int[start.Length];

            foreach (Node shape in _shapes)
            {
                int count =
                    shape.Kind == Kind.Arc ? 5
                    : shape.Kind == Kind.Circle ? 3
                    : 4;

                for (int k = 0; k < count; k++)
                {
                    stage[shape.At + k] =
                        shape.Kind == Kind.Construction ? -1
                        : !staged ? 0
                        : k == 2 && shape.Kind != Kind.Line ? 0
                        : k >= 3 && shape.Kind == Kind.Arc ? 1
                        : 2;
                }
            }

            Decomposition made = null;

            for (int now = 0; now <= (staged ? 2 : 0); now++)
            {
                if (now == 0 && decomposed.TryGetValue(extra, out Decomposition cached))
                {
                    made = cached;
                }
                else if (made == null || made.Rows != residuals.Count)
                {
                    made = Decompose(Jacobian(residuals, start), residuals.Count);

                    if (now == 0)
                    {
                        decomposed[extra] = made;
                    }
                }

                for (int j = 0; j < start.Length; j++)
                {
                    if (stage[j] != now)
                    {
                        continue;
                    }

                    double weight = 0.0;

                    for (int k = 0; k < made.Singular.Length; k++)
                    {
                        if (!(made.Singular[k] > 1E-8 * made.Largest))
                        {
                            weight += made.Right[k][j] * made.Right[k][j];
                        }
                    }

                    if (Math.Sqrt(weight) > 1E-6)
                    {
                        int unknown = j;
                        double own = start[j];
                        residuals.Add(x => x[unknown] - own);
                    }
                }
            }

            freed = residuals.Count > held;
            return Newton(residuals, start);
        }

        private static bool Near(double a, double b) => Math.Abs(a - b) <= 1E-12 * Math.Max(1.0, Math.Abs(a));

        /// <summary>
        /// The values of the numeric variables: those given, the others the network's, an expression worked out where a
        /// variable has one; whether each is whole put in the dictionary given, where one is.
        /// </summary>
        private Dictionary<string, double> Values(
            IDictionary<string, double> given,
            Dictionary<string, bool> wholes = null
        )
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            wholes = wholes ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (Variable variable in _variables)
            {
                if (variable.Value is double value)
                {
                    values[variable.Name] = value;
                    wholes[variable.Name] = variable.IsWhole;
                }
            }

            var worked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Variable variable in _variables)
            {
                if (variable.Value is double)
                {
                    Value(variable, given, values, wholes, worked, 0);
                }
            }

            return values;
        }

        private double Value(
            Variable variable,
            IDictionary<string, double> given,
            Dictionary<string, double> values,
            Dictionary<string, bool> wholes,
            HashSet<string> worked,
            int depth
        )
        {
            if (given.TryGetValue(variable.Name, out double set))
            {
                wholes[variable.Name] = Written(set).IndexOfAny(new[] { '.', 'e' }) < 0;
                return values[variable.Name] = set;
            }

            if (!variable.Worked || worked.Contains(variable.Name))
            {
                return values[variable.Name];
            }

            if (depth > _variables.Count)
            {
                throw new NotSupportedException(
                    "The variable " + variable.Name + " of the block " + _block + " is worked out from itself."
                );
            }

            double result = DxfConstraintExpression.Evaluate(
                variable.Expression,
                name =>
                {
                    Variable used = _variables.Find(v =>
                        string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase)
                    );

                    if (used == null || !(used.Value is double))
                    {
                        throw new NotSupportedException(
                            "The expression "
                                + variable.Expression
                                + " of the block "
                                + _block
                                + " names "
                                + name
                                + ", no variable of a number of it."
                        );
                    }

                    return Value(used, given, values, wholes, worked, depth + 1);
                },
                name => wholes.TryGetValue(name, out bool whole) && whole,
                out bool isWhole
            );

            worked.Add(variable.Name);
            wholes[variable.Name] = isWhole;
            return values[variable.Name] = result;
        }

        /// <summary>
        /// The equations of the constraints, each nought where its constraint is met, at the values after; a sign or a side
        /// each takes (a distance along its direction, the side of a line an arc touches, how an angle is read) from the
        /// block's own geometry at the values before.
        /// </summary>
        private List<Func<double[], double>> Residuals(
            double[] start,
            Dictionary<string, double> before,
            Dictionary<string, double> after
        )
        {
            var residuals = new List<Func<double[], double>>();

            foreach (Rule rule in _rules)
            {
                Node a = rule.Links.Count > 0 ? rule.Links[0] : null;
                Node b = rule.Links.Count > 1 ? rule.Links[1] : null;

                switch (rule.Class)
                {
                    case "AcPointCurveConstraint":
                        if (a.Owner == b)
                        {
                            break;
                        }

                        if (b.Kind == Kind.Line || b.Kind == Kind.Construction)
                        {
                            residuals.Add(x => Across(b, Point(a, x), x));
                        }
                        else
                        {
                            residuals.Add(x => Length(Point(a, x), Centre(b, x)) - x[b.At + 2]);
                        }

                        break;
                    case "AcPointCoincidenceConstraint":
                        residuals.Add(x => Point(a, x)[0] - Point(b, x)[0]);
                        residuals.Add(x => Point(a, x)[1] - Point(b, x)[1]);
                        break;
                    case "AcParallelConstraint":
                        residuals.Add(x => Sine(Direction(a, x), Direction(b, x)));
                        break;
                    case "AcHorizontalConstraint":
                        Node datum = rule.Datum;
                        residuals.Add(x => Sine(Direction(a, x), Direction(datum, x)));
                        break;
                    case "AcPerpendicularConstraint":
                        residuals.Add(x => Cosine(Direction(a, x), Direction(b, x)));
                        break;
                    case "AcColinearConstraint":
                        residuals.Add(x => Sine(Direction(a, x), Direction(b, x)));
                        residuals.Add(x => Across(a, new[] { x[b.At], x[b.At + 1] }, x));
                        break;
                    case "AcEqualLengthConstraint":
                        residuals.Add(x => Norm(Direction(a, x)) - Norm(Direction(b, x)));
                        break;
                    case "AcEqualRadiusConstraint":
                        residuals.Add(x => x[a.At + 2] - x[b.At + 2]);
                        break;
                    case "AcMidPointConstraint":
                        if (a.Owner == b && a.PointType == 2)
                        {
                            break;
                        }

                        residuals.Add(x => Point(a, x)[0] - 0.5 * (x[b.At] + x[b.At + 2]));
                        residuals.Add(x => Point(a, x)[1] - 0.5 * (x[b.At + 1] + x[b.At + 3]));
                        break;
                    case "AcCenterPointConstraint":
                        if (a.Owner == b && a.PointType == 3)
                        {
                            break;
                        }

                        residuals.Add(x => Point(a, x)[0] - x[b.At]);
                        residuals.Add(x => Point(a, x)[1] - x[b.At + 1]);
                        break;
                    case "AcFixedConstraint":
                        foreach (Node point in rule.Links)
                        {
                            double[] own = Point(point, start);
                            residuals.Add(x => Point(point, x)[0] - own[0]);
                            residuals.Add(x => Point(point, x)[1] - own[1]);
                        }

                        break;
                    case "AcTangentConstraint":
                        Tangent(a, b, start, residuals);
                        break;
                    case "AcDistanceConstraint":
                        double[] along = rule.Along;
                        double distance = after[rule.Variable.Name];
                        double side = Dot(Minus(Point(b, start), Point(a, start)), along) < 0.0 ? -1.0 : 1.0;
                        residuals.Add(x => Dot(Minus(Point(b, x), Point(a, x)), along) - side * distance);
                        break;
                    case "AcRadiusDiameterConstraint":
                        double radius = after[rule.Variable.Name];
                        residuals.Add(x => x[a.At + 2] - radius);
                        break;
                    case "AcAngleConstraint":
                        Func<double, double> read = Reading(
                            Angle(a, b, start),
                            before[rule.Variable.Name] * Math.PI / 180.0
                        )
                            ?? throw new NotSupportedException(
                                "An angle constraint of the block "
                                    + _block
                                    + " measures its lines in a way the library does not read."
                            );
                        double angle = after[rule.Variable.Name] * Math.PI / 180.0;
                        residuals.Add(x => read(Angle(a, b, x)) - angle);
                        break;
                    case "Ac3PointAngleConstraint":
                        // The angle at the first point from the second to the third, as the Hex Head Bolt holds each fillet's
                        // sweep at its centre from its start to its end.
                        Node c = rule.Links[2];
                        Func<double, double> reads = Reading(
                            Angle(a, b, c, start),
                            before[rule.Variable.Name] * Math.PI / 180.0
                        )
                            ?? throw new NotSupportedException(
                                "An angle constraint of the block "
                                    + _block
                                    + " measures its points in a way the library does not read."
                            );
                        double turned = after[rule.Variable.Name] * Math.PI / 180.0;
                        residuals.Add(x => reads(Angle(a, b, c, x)) - turned);
                        break;
                }
            }

            return residuals;
        }

        /// <summary>The angle at a point from a second point to a third.</summary>
        private static double Angle(Node at, Node from, Node to, double[] x)
        {
            double[] o = Point(at, x);
            double[] u = Minus(Point(from, x), o);
            double[] v = Minus(Point(to, x), o);
            return Math.Atan2(u[0] * v[1] - u[1] * v[0], Dot(u, v));
        }

        /// <summary>A tangency: the centre of an arc or a circle as far from a line as its radius, on the side it is; two arcs or circles apart by the sum of their radii, or by the difference, as they touch.</summary>
        private static void Tangent(Node a, Node b, double[] start, List<Func<double[], double>> residuals)
        {
            if (a.Kind == Kind.Line || b.Kind == Kind.Line)
            {
                Node line = a.Kind == Kind.Line ? a : b;
                Node round = a.Kind == Kind.Line ? b : a;
                double side = Across(line, Centre(round, start), start) < 0.0 ? -1.0 : 1.0;
                residuals.Add(x => Across(line, Centre(round, x), x) - side * x[round.At + 2]);
                return;
            }

            double apart = Length(Centre(a, start), Centre(b, start));
            bool outside =
                Math.Abs(apart - (start[a.At + 2] + start[b.At + 2]))
                <= Math.Abs(apart - Math.Abs(start[a.At + 2] - start[b.At + 2]));

            if (outside)
            {
                residuals.Add(x => Length(Centre(a, x), Centre(b, x)) - (x[a.At + 2] + x[b.At + 2]));
            }
            else
            {
                double sign = start[a.At + 2] >= start[b.At + 2] ? 1.0 : -1.0;
                residuals.Add(x => Length(Centre(a, x), Centre(b, x)) - sign * (x[a.At + 2] - x[b.At + 2]));
            }
        }

        /// <summary>How an angle constraint reads the angle from its first line to its second: the one of the readings that gives its value at the block's own geometry.</summary>
        private static Func<double, double> Reading(double own, double value)
        {
            var readings = new Func<double, double>[]
            {
                t => t,
                t => -t,
                t => Math.PI - t,
                t => t - Math.PI,
                t => Math.PI + t,
                t => -Math.PI - t,
                t => t + Turn,
                t => Turn - t,
            };

            foreach (Func<double, double> reading in readings)
            {
                if (Math.Abs(reading(own) - value) <= 1E-6)
                {
                    return reading;
                }
            }

            return null;
        }

        private static double Angle(Node a, Node b, double[] x)
        {
            double[] u = Direction(a, x);
            double[] v = Direction(b, x);
            return Math.Atan2(u[0] * v[1] - u[1] * v[0], Dot(u, v));
        }

        /// <summary>
        /// Newton's method from the start given, each step the least that meets the equations as they run there (by the
        /// singular values of their Jacobian, those below 1E-9 of the largest taken as nought): the solution once every
        /// equation is within 1E-12 of nought, or within 1E-10 and a step no nearer; within 1E-9 once four steps in a row
        /// do not halve the worst, or after 60 steps; else null.
        /// </summary>
        private static double[] Newton(List<Func<double[], double>> residuals, double[] start)
        {
            double[] x = (double[])start.Clone();
            double previous = double.PositiveInfinity;
            int stalled = 0;

            for (int step = 0; step < 60; step++)
            {
                double[] f = Evaluate(residuals, x);
                double worst = 0.0;
                Array.ForEach(f, v => worst = Math.Max(worst, Math.Abs(v)));

                // Met to rounding, or as near as rounding lets the steps come; given up where four steps in a row do not halve
                // what is left, as on equations that cannot all be met.
                if (worst < 1E-12 || (worst <= 1E-10 && worst >= previous))
                {
                    return x;
                }

                stalled = worst > 0.5 * previous ? stalled + 1 : 0;

                if (stalled >= 4)
                {
                    return worst <= 1E-9 ? x : null;
                }

                previous = worst;

                int m = residuals.Count;
                int n = x.Length;
                Decomposition made = Decompose(Jacobian(residuals, x), m);

                for (int k = 0; k < n; k++)
                {
                    if (!(made.Singular[k] > 1E-9 * made.Largest))
                    {
                        continue;
                    }

                    // The column k of the decomposed matrix is the k-th left singular vector times its singular value.
                    double along = 0.0;
                    double[] column = made.Columns[k];
                    double[] vector = made.Right[k];

                    for (int i = 0; i < m; i++)
                    {
                        along -= column[i] * f[i];
                    }

                    along /= made.Singular[k] * made.Singular[k];

                    for (int j = 0; j < n; j++)
                    {
                        x[j] += vector[j] * along;
                    }
                }
            }

            double[] last = Evaluate(residuals, x);
            double end = 0.0;
            Array.ForEach(last, v => end = Math.Max(end, Math.Abs(v)));
            return end <= 1E-9 ? x : null;
        }

        private static double[] Evaluate(List<Func<double[], double>> residuals, double[] x)
        {
            var f = new double[residuals.Count];

            for (int i = 0; i < f.Length; i++)
            {
                f[i] = residuals[i](x);
            }

            return f;
        }

        /// <summary>
        /// The Jacobian of the equations by central differences, each unknown stepped by 1E-5 of its size, or of 1 where
        /// smaller: a column of the derivatives of every equation by each unknown.
        /// </summary>
        private static double[][] Jacobian(List<Func<double[], double>> residuals, double[] x)
        {
            int m = residuals.Count;
            var jacobian = new double[x.Length][];
            double[] probe = (double[])x.Clone();

            for (int j = 0; j < x.Length; j++)
            {
                double h = 1E-5 * Math.Max(1.0, Math.Abs(x[j]));
                probe[j] = x[j] + h;
                double[] up = Evaluate(residuals, probe);
                probe[j] = x[j] - h;
                double[] down = Evaluate(residuals, probe);
                probe[j] = x[j];
                var column = jacobian[j] = new double[m];

                for (int i = 0; i < m; i++)
                {
                    column[i] = (up[i] - down[i]) / (2.0 * h);
                }
            }

            return jacobian;
        }

        /// <summary>
        /// The singular value decomposition of a matrix of the rows given, its columns given, by one-sided Jacobi rotations of
        /// its columns: the columns left as the left singular vectors times their values, the values and the right singular
        /// vectors.
        /// </summary>
        private static Decomposition Decompose(double[][] a, int m)
        {
            int n = a.Length;
            var right = new double[n][];

            // A column of a square size within 1E-200 of the largest's is nought: a rotation of it moves nothing else, and in
            // numbers that small, its squares below the least normal double, gives no end of rotations, each slow.
            double nought = 0.0;

            for (int j = 0; j < n; j++)
            {
                right[j] = new double[n];
                right[j][j] = 1.0;
                double size = 0.0;

                for (int i = 0; i < m; i++)
                {
                    size += a[j][i] * a[j][i];
                }

                nought = Math.Max(nought, 1E-200 * size);
            }

            for (int sweep = 0; sweep < 80; sweep++)
            {
                bool rotated = false;

                for (int p = 0; p < n - 1; p++)
                {
                    double[] ap = a[p];
                    double[] vp = right[p];

                    for (int q = p + 1; q < n; q++)
                    {
                        double[] aq = a[q];
                        double alpha = 0.0,
                            beta = 0.0,
                            gamma = 0.0;

                        for (int i = 0; i < m; i++)
                        {
                            alpha += ap[i] * ap[i];
                            beta += aq[i] * aq[i];
                            gamma += ap[i] * aq[i];
                        }

                        if (alpha <= nought || beta <= nought || Math.Abs(gamma) <= 1E-15 * Math.Sqrt(alpha * beta))
                        {
                            continue;
                        }

                        rotated = true;
                        double zeta = (beta - alpha) / (2.0 * gamma);
                        double t = (zeta >= 0.0 ? 1.0 : -1.0) / (Math.Abs(zeta) + Math.Sqrt(1.0 + zeta * zeta));
                        double c = 1.0 / Math.Sqrt(1.0 + t * t);
                        double s = c * t;

                        for (int i = 0; i < m; i++)
                        {
                            double p0 = ap[i];
                            double q0 = aq[i];
                            ap[i] = c * p0 - s * q0;
                            aq[i] = s * p0 + c * q0;
                        }

                        double[] vq = right[q];

                        for (int i = 0; i < n; i++)
                        {
                            double p0 = vp[i];
                            double q0 = vq[i];
                            vp[i] = c * p0 - s * q0;
                            vq[i] = s * p0 + c * q0;
                        }
                    }
                }

                if (!rotated)
                {
                    break;
                }
            }

            var singular = new double[n];
            double largest = 0.0;

            for (int j = 0; j < n; j++)
            {
                double sum = 0.0;

                for (int i = 0; i < m; i++)
                {
                    sum += a[j][i] * a[j][i];
                }

                singular[j] = Math.Sqrt(sum);
                largest = Math.Max(largest, singular[j]);
            }

            return new Decomposition(m, a, singular, right, largest);
        }

        /// <summary>
        /// A singular value decomposition: the count of the rows of the matrix, its columns made the left singular vectors
        /// times their values, the values, the largest of them, and the right singular vectors, each an array.
        /// </summary>
        private sealed class Decomposition
        {
            internal Decomposition(int rows, double[][] columns, double[] singular, double[][] right, double largest)
            {
                Rows = rows;
                Columns = columns;
                Singular = singular;
                Right = right;
                Largest = largest;
            }

            internal int Rows { get; }

            internal double[][] Columns { get; }

            internal double[] Singular { get; }

            internal double[][] Right { get; }

            internal double Largest { get; }
        }

        /// <summary>A point: the start (0), the end (1) or the middle (2) of its line, the start, the end or the centre (3) of its arc, the centre of its circle.</summary>
        private static double[] Point(Node point, double[] x)
        {
            Node owner = point.Owner;
            int at = owner.At;

            if (owner.Kind == Kind.Line)
            {
                switch (point.PointType)
                {
                    case 0:
                        return new[] { x[at], x[at + 1] };
                    case 1:
                        return new[] { x[at + 2], x[at + 3] };
                    default:
                        return new[] { 0.5 * (x[at] + x[at + 2]), 0.5 * (x[at + 1] + x[at + 3]) };
                }
            }

            if (point.PointType == 3)
            {
                return new[] { x[at], x[at + 1] };
            }

            double angle = point.PointType == 0 ? x[at + 3] : x[at + 4];
            return new[] { x[at] + x[at + 2] * Math.Cos(angle), x[at + 1] + x[at + 2] * Math.Sin(angle) };
        }

        private static double[] Centre(Node round, double[] x) => new[] { x[round.At], x[round.At + 1] };

        private static double[] Direction(Node line, double[] x) =>
            line.Kind == Kind.Datum
                ? line.Datum
                : new[] { x[line.At + 2] - x[line.At], x[line.At + 3] - x[line.At + 1] };

        /// <summary>How far a point stands off a line, to its left positive.</summary>
        private static double Across(Node line, double[] point, double[] x)
        {
            double[] d = Direction(line, x);
            return (d[0] * (point[1] - x[line.At + 1]) - d[1] * (point[0] - x[line.At])) / Norm(d);
        }

        private static double Sine(double[] u, double[] v) => (u[0] * v[1] - u[1] * v[0]) / (Norm(u) * Norm(v));

        private static double Cosine(double[] u, double[] v) => Dot(u, v) / (Norm(u) * Norm(v));

        private static double Dot(double[] u, double[] v) => u[0] * v[0] + u[1] * v[1];

        private static double Norm(double[] u) => Math.Sqrt(u[0] * u[0] + u[1] * u[1]);

        private static double[] Minus(double[] u, double[] v) => new[] { u[0] - v[0], u[1] - v[1] };

        private static double Length(double[] u, double[] v) => Norm(Minus(u, v));

        private static bool Same(double[] point, double x, double y) => point[0] == x && point[1] == y;

        private static bool Whole(DxfTypedValue group, out int value) =>
            int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        /// <summary>The points of groups in order: each a 10 or an 11 with the 20 or 21 and the 30 or 31 after it.</summary>
        private static List<double[]> Points(List<DxfTypedValue> groups)
        {
            var points = new List<double[]>();

            for (int i = 0; i + 2 < groups.Count; i++)
            {
                int code = groups[i].TypeCode;

                if (
                    (code == 10 || code == 11)
                    && groups[i + 1].TypeCode == code + 10
                    && groups[i + 2].TypeCode == code + 20
                    && DxfDynamicProperties.Number(groups[i].Value, out double x)
                    && DxfDynamicProperties.Number(groups[i + 1].Value, out double y)
                    && DxfDynamicProperties.Number(groups[i + 2].Value, out double z)
                )
                {
                    points.Add(new[] { x, y, z });
                    i += 2;
                }
            }

            return points;
        }

        private enum Kind
        {
            None,
            Line,
            Arc,
            Circle,
            Datum,
            Point,
            Construction,
        }

        /// <summary>A node of a group: its class, key and ties; of geometry, its kind, where its unknowns start, its dependency, its datum direction, or its owner and kind of point.</summary>
        private sealed class Node
        {
            internal string Class { get; set; }

            internal string Key { get; set; }

            internal List<int> LinkIds { get; } = new List<int>();

            internal List<Node> Links { get; } = new List<Node>();

            internal Kind Kind { get; set; }

            internal int At { get; set; } = -1;

            internal string Dependency { get; set; }

            internal double[] Datum { get; set; }

            internal int PointType { get; set; }

            internal Node Owner { get; set; }

            /// <summary>The constraint group the node is written in.</summary>
            internal DxfItem Group { get; set; }

            /// <summary>Where the groups of the node's class begin among the groups of its group's record.</summary>
            internal int DataAt { get; set; }
        }

        /// <summary>A constraint: its class, the geometry it ties, the groups of its class, its group; a horizontal one's datum line, a dimensional one's variable and direction.</summary>
        private sealed class Rule
        {
            internal string Class { get; set; }

            internal List<Node> Links { get; set; }

            internal List<DxfTypedValue> Rest { get; set; }

            internal string Group { get; set; }

            internal Node Datum { get; set; }

            internal Variable Variable { get; set; }

            internal double[] Along { get; set; }

            internal Dimension Dimension { get; set; }
        }

        /// <summary>
        /// A variable of the network (AcDbAssocVariable): its handle, its name, its expression and the value it keeps, a real
        /// (40), a whole number (90) or a text (1); whether its expression names other variables, and so is worked out.
        /// </summary>
        private sealed class Variable
        {
            internal string Handle { get; private set; }

            internal string Name { get; private set; }

            internal string Expression { get; private set; }

            internal object Value { get; private set; }

            internal bool Worked { get; private set; }

            /// <summary>Whether the value kept is a whole number (90) rather than a real (40).</summary>
            internal bool IsWhole { get; private set; }

            internal static Variable Of(DxfItem item)
            {
                List<DxfTypedValue> held = DxfDynamicProperties.Section(item.Groups, "AcDbAssocVariable");

                if (held.Count < 6 || held[1].TypeCode != 1 || held[2].TypeCode != 1)
                {
                    return null;
                }

                object value = null;

                if (
                    (held[5].TypeCode == 40 || held[5].TypeCode == 90)
                    && DxfDynamicProperties.Number(held[5].Value, out double number)
                )
                {
                    value = number;
                }
                else if (held[5].TypeCode == 1)
                {
                    value = DxfReader.Decode(held[5].Value ?? string.Empty);
                }

                string expression = (held[2].Value ?? string.Empty).Trim();
                return new Variable
                {
                    Handle = item.Handle,
                    Name = DxfReader.NameOf(held[1].Value ?? string.Empty),
                    Expression = expression,
                    Value = value,
                    Worked = expression.Length > 0 && !Numbers.TryRead(expression, out _),
                    IsWhole = held[5].TypeCode == 90,
                };
            }
        }

        /// <summary>A dimensional constraint a constraint parameter of the block's graph drives: its place among the constraints, and the name of its variable.</summary>
        internal sealed class Dimension
        {
            internal Dimension(int index, string variable)
            {
                Index = index;
                Variable = variable;
            }

            internal int Index { get; }

            /// <summary>The name of the variable the constraint reads its value from.</summary>
            internal string Variable { get; }
        }

        /// <summary>
        /// The base point and the end point a constraint parameter shows at a solution, from its own two points in the block:
        /// of a distance the constraint's two points, in the order the parameter's own are; of a radius the centre and the point
        /// of the arc the parameter's own end shows the way to.
        /// </summary>
        internal void Places(
            Dimension dimension,
            Solution solution,
            GeoPoint3 ownBase,
            GeoPoint3 ownEnd,
            out GeoPoint3 basePoint,
            out GeoPoint3 endPoint
        )
        {
            Rule rule = _rules[dimension.Index];
            double[] start = _start.ToArray();
            double[] x = solution.Unknowns;
            Node a = rule.Links[0];

            if (rule.Class == "AcRadiusDiameterConstraint")
            {
                double dx = ownEnd.X - ownBase.X,
                    dy = ownEnd.Y - ownBase.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                double r = x[a.At + 2];
                basePoint = new GeoPoint3(x[a.At], x[a.At + 1], ownBase.Z);
                endPoint =
                    length > 0.0
                        ? new GeoPoint3(x[a.At] + dx / length * r, x[a.At + 1] + dy / length * r, ownEnd.Z)
                        : new GeoPoint3(x[a.At] + r, x[a.At + 1], ownEnd.Z);
                return;
            }

            Node b = rule.Links[1];
            double[] first = Point(a, start);
            double[] second = Point(b, start);
            bool turned =
                Math.Abs(first[0] - ownBase.X) + Math.Abs(first[1] - ownBase.Y)
                > Math.Abs(second[0] - ownBase.X) + Math.Abs(second[1] - ownBase.Y);
            double[] p = Point(turned ? b : a, x);
            double[] q = Point(turned ? a : b, x);
            basePoint = new GeoPoint3(p[0], p[1], ownBase.Z);
            endPoint = new GeoPoint3(q[0], q[1], ownEnd.Z);
        }

        /// <summary>The geometry solved: the unknowns, and the writing of them into the copies of the block's entities.</summary>
        internal sealed class Solution
        {
            private readonly DxfBlockConstraints _owner;

            internal Solution(DxfBlockConstraints owner, double[] unknowns)
            {
                _owner = owner;
                Unknowns = unknowns;
            }

            internal double[] Unknowns { get; }

            /// <summary>
            /// Writes the geometry into the copies of the entities each node is tied to (by the handle of the block's own):
            /// a LINE its ends, an ARC its centre, radius and angles, a CIRCLE its centre and radius, an LWPOLYLINE the vertices
            /// of each edge (found by the vertex identifiers, 91, of the block's own) and the bulge of an arc's edge. With no
            /// copies, the ties are checked and nothing is written.
            /// </summary>
            /// <exception cref="NotSupportedException">Thrown when a node is tied to an entity or a part of one the library does not write.</exception>
            internal void Write(
                IReadOnlyDictionary<string, DxfEntity> sources,
                IReadOnlyDictionary<string, DxfEntity> copies
            )
            {
                double[] x = Unknowns;
                double[] start = _owner._start.ToArray();
                bool writing = copies != null;

                foreach (Node shape in _owner._shapes)
                {
                    if (shape.Kind == Kind.Construction)
                    {
                        continue;
                    }

                    DxfItem dependency = shape.Dependency == null ? null : _owner._file.ObjectItem(shape.Dependency);
                    List<DxfTypedValue> held =
                        dependency == null
                            ? null
                            : DxfDynamicProperties.Section(dependency.Groups, "AcDbAssocDependency");
                    string handle = held == null ? null : DxfDynamicProperties.Text(held, 330)?.Trim();
                    DxfEntity source = null;
                    DxfEntity copy = null;

                    if (
                        dependency == null
                        || dependency.Type != "ACDBASSOCGEOMDEPENDENCY"
                        || handle == null
                        || !sources.TryGetValue(handle, out source)
                        || (writing ? !copies.TryGetValue(handle, out copy) : (copy = source) == null)
                    )
                    {
                        throw new NotSupportedException(
                            "A geometry of the constraints of the block "
                                + _owner._block
                                + " is tied to no entity of it."
                        );
                    }

                    List<DxfTypedValue> part = DxfDynamicProperties.Section(
                        dependency.Groups,
                        "AcDbAssocGeomDependency"
                    );
                    int named = part.FindIndex(g => g.TypeCode == 1);
                    string kind = named < 0 ? null : part[named].Value?.Trim();
                    int at = shape.At;

                    if (kind == "AcDbAssocSingleEdgePersSubentId")
                    {
                        Place(shape, copy, x, writing);
                    }
                    else if (
                        kind == "AcDbAssocEdgePersSubentId"
                        && named + 3 < part.Count
                        && copy is DxfPolyline polyline
                        && source is DxfPolyline own
                        && Whole(part[named + 2], out int from)
                        && Whole(part[named + 3], out int to)
                    )
                    {
                        List<int> ids = DxfDynamicProperties
                            .Section(own.Item.Groups, "AcDbPolyline")
                            .FindAll(g => g.TypeCode == 91)
                            .ConvertAll(g => Whole(g, out int id) ? id : int.MinValue);
                        int i = ids.IndexOf(from);
                        int j = ids.IndexOf(to);

                        if (
                            i < 0
                            || j < 0
                            || ids.Count != polyline.NumberOfVertices
                            || !(own.Normal.Z > 0.0)
                            || own.Normal.X != 0.0
                            || own.Normal.Y != 0.0
                        )
                        {
                            throw new NotSupportedException(
                                "An edge of a polyline of the constraints of the block "
                                    + _owner._block
                                    + " cannot be found, or the polyline is off the block's plane."
                            );
                        }

                        if (shape.Kind == Kind.Circle)
                        {
                            throw new NotSupportedException(
                                "A circle of the constraints of the block "
                                    + _owner._block
                                    + " is tied to an edge of a polyline."
                            );
                        }

                        if (!writing)
                        {
                            continue;
                        }

                        if (shape.Kind == Kind.Line)
                        {
                            polyline.SetPointAt(i, new GeoPoint2(x[at], x[at + 1]));
                            polyline.SetPointAt(j, new GeoPoint2(x[at + 2], x[at + 3]));
                            polyline.SetBulgeAt(i, 0.0);
                        }
                        else
                        {
                            // The arc runs counter-clockwise from its start: from the edge's first vertex where it starts there,
                            // else backwards, its bulge then negative.
                            GeoPoint2 vertex = own.GetPoint2dAt(i);
                            double[] begins = Point(new Node { Owner = shape, PointType = 0 }, start);
                            double[] ends = Point(new Node { Owner = shape, PointType = 1 }, start);
                            bool forward =
                                Math.Abs(begins[0] - vertex.X) + Math.Abs(begins[1] - vertex.Y)
                                <= Math.Abs(ends[0] - vertex.X) + Math.Abs(ends[1] - vertex.Y);
                            double[] s = Point(new Node { Owner = shape, PointType = 0 }, x);
                            double[] e = Point(new Node { Owner = shape, PointType = 1 }, x);
                            double sweep = Normal(x[at + 4] - x[at + 3]);
                            polyline.SetPointAt(i, forward ? new GeoPoint2(s[0], s[1]) : new GeoPoint2(e[0], e[1]));
                            polyline.SetPointAt(j, forward ? new GeoPoint2(e[0], e[1]) : new GeoPoint2(s[0], s[1]));
                            polyline.SetBulgeAt(i, (forward ? 1.0 : -1.0) * Math.Tan(sweep / 4.0));
                        }
                    }
                    else
                    {
                        throw new NotSupportedException(
                            "A geometry of the constraints of the block "
                                + _owner._block
                                + " is tied to a part of an entity the library does not write."
                        );
                    }
                }
            }

            private void Place(Node shape, DxfEntity copy, double[] x, bool writing)
            {
                int at = shape.At;

                if (shape.Kind == Kind.Line && copy is DxfLine line)
                {
                    if (writing)
                    {
                        line.StartPoint = new GeoPoint3(x[at], x[at + 1], line.StartPoint.Z);
                        line.EndPoint = new GeoPoint3(x[at + 2], x[at + 3], line.EndPoint.Z);
                    }
                }
                else if (
                    shape.Kind == Kind.Arc
                    && copy is DxfArc arc
                    && arc.Normal.Z > 0.0
                    && arc.Normal.X == 0.0
                    && arc.Normal.Y == 0.0
                )
                {
                    if (writing)
                    {
                        arc.Center = new GeoPoint3(x[at], x[at + 1], arc.Center.Z);
                        arc.Radius = x[at + 2];
                        arc.StartAngle = Normal(x[at + 3]);
                        arc.EndAngle = Normal(x[at + 4]);
                    }
                }
                else if (
                    shape.Kind == Kind.Circle
                    && copy is DxfCircle circle
                    && circle.Normal.Z > 0.0
                    && circle.Normal.X == 0.0
                    && circle.Normal.Y == 0.0
                )
                {
                    if (writing)
                    {
                        circle.Center = new GeoPoint3(x[at], x[at + 1], circle.Center.Z);
                        circle.Radius = x[at + 2];
                    }
                }
                else
                {
                    throw new NotSupportedException(
                        "A geometry of the constraints of the block "
                            + _owner._block
                            + " is tied to a "
                            + copy.GetType().Name
                            + " the library does not write so, or one off the block's plane."
                    );
                }
            }

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
}
