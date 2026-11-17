using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Reads the constraints of a space from the objects AutoCAD 2021 keeps them in (or this presenter wrote, which are the
    /// same): see <see cref="Write"/>. A space whose group holds a kind of node the presenter does not work out (a spline, an
    /// ellipse, a smooth or three-point-angle constraint, a layout older than AutoCAD 2018's) is read as far as it goes and
    /// marked <see cref="CadConstraintSet.Refusal"/>, so that nothing writes it again and loses what it did not read.
    /// </summary>
    internal static partial class CadConstraintStore
    {
        private sealed class Raw
        {
            internal int Id;
            internal string Class;
            internal List<int> Links = new List<int>();
            internal int Flags;
            internal List<DxfTypedValue> Body = new List<DxfTypedValue>();

            // What the node means once read.
            internal CadRef Ref;
            internal double[] Cached;
            internal Raw Owner;
            internal int PointType;
        }

        /// <summary>Reads the constraints of a space.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="space">The block of the space.</param>
        /// <param name="entityOf">The object of a handle; null where there is none.</param>
        /// <returns>The set; empty, with no network, where the space holds none.</returns>
        internal static CadConstraintSet Read(
            DxfDatabase database,
            DxfBlockTableRecord space,
            Func<string, DxfEntity> entityOf
        )
        {
            var set = new CadConstraintSet();
            DxfItem network = NetworkOf(space);

            if (network == null)
            {
                return set;
            }

            set.Network = network.Handle;
            DxfFile file = database.File;
            var groups = new List<DxfItem>();
            var variables = new List<DxfItem>();

            foreach (string handle in CadCenterMarks.ActionsOf(network))
            {
                DxfItem action = file.ObjectItem(handle);

                if (action == null || action.IsRemoved)
                {
                    continue;
                }

                if (action.Type == "ACDBASSOC2DCONSTRAINTGROUP")
                {
                    groups.Add(action);
                }
                else if (action.Type == "ACDBASSOCVARIABLE")
                {
                    variables.Add(action);
                }
            }

            var byHandle = new Dictionary<string, CadParameter>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfItem variable in variables)
            {
                CadParameter parameter = ReadVariable(variable);

                if (parameter == null)
                {
                    Refuse(set, "a parameter it cannot read");
                    continue;
                }

                set.Parameters.Add(parameter);
                byHandle[variable.Handle] = parameter;
            }

            foreach (DxfItem group in groups)
            {
                ReadGroup(file, group, set, byHandle, entityOf);
            }

            var measured = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (CadDimConstraint dim in set.Dimensional)
            {
                measured.Add(dim.Name);
            }

            for (int i = 0; i < set.Parameters.Count; i++)
            {
                CadParameter p = set.Parameters[i];

                if (!measured.Contains(p.Name))
                {
                    set.Parameters[i] = new CadParameter(p.Name, p.Expression, p.Value, true) { Source = p.Source };
                }
            }

            return set;
        }

        private static void Refuse(CadConstraintSet set, string why)
        {
            if (set.Refusal == null)
            {
                set.Refusal = "The constraints of this space hold " + why + ", which the presenter does not work out.";
            }
        }

        private static CadParameter ReadVariable(DxfItem item)
        {
            IReadOnlyList<DxfTypedValue> groups = item.Groups;
            int at = CadCenterMarks.Start(groups, "AcDbAssocVariable");

            if (at < 0)
            {
                return null;
            }

            var texts = new List<string>();
            int i = at + 1;

            while (i < groups.Count && texts.Count < 4)
            {
                if (groups[i].TypeCode == 1)
                {
                    texts.Add(groups[i].Value ?? string.Empty);
                }

                i++;
            }

            if (texts.Count < 4 || i >= groups.Count)
            {
                return null;
            }

            double value;

            if (groups[i].TypeCode == 90 || groups[i].TypeCode == 40)
            {
                if (
                    !double.TryParse(
                        groups[i].Value.Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value
                    )
                )
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            return new CadParameter(texts[0], texts[1], value, false) { Source = item.Handle };
        }

        private static void ReadGroup(
            DxfFile file,
            DxfItem group,
            CadConstraintSet set,
            Dictionary<string, CadParameter> variables,
            Func<string, DxfEntity> entityOf
        )
        {
            List<DxfTypedValue> head = DxfDynamicProperties.Section(group.Groups, "AcDbAssocAction");
            int first = head.FindIndex(g => g.TypeCode == 1);

            if (first < 0 || first + 1 >= head.Count || head[first + 1].TypeCode != 90)
            {
                Refuse(set, "a group of an older layout");
                return;
            }

            var listed = new List<KeyValuePair<int, string>>();
            int at = first;

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

            var nodes = new List<Raw>();
            var byId = new Dictionary<int, Raw>();

            for (int k = 0; k < listed.Count; k++)
            {
                if (!Opens(head, at, listed[k].Key, out int count))
                {
                    Refuse(set, "a node it cannot read");
                    return;
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
                        Refuse(set, "a node it cannot read");
                        return;
                    }
                }

                var node = new Raw
                {
                    Id = listed[k].Key,
                    Class = listed[k].Value,
                    Flags = Whole(head[at + 2 + count], out int flags) ? flags : 0,
                    Body = head.GetRange(data, end - data),
                };

                for (int i = 0; i < count; i++)
                {
                    node.Links.Add(Whole(head[at + 2 + i], out int link) ? link : int.MinValue);
                }

                nodes.Add(node);
                byId[node.Id] = node;
                at = end;
            }

            // Geometry first: the objects, then their implicit points.
            foreach (Raw node in nodes)
            {
                if (!ReadGeometry(file, node, set, entityOf))
                {
                    return;
                }
            }

            foreach (Raw node in nodes)
            {
                if (node.Class == "AcConstrainedImplicitPoint" && !ReadPoint(node, byId, set))
                {
                    return;
                }
            }

            foreach (Raw node in nodes)
            {
                if (!node.Class.StartsWith("AcConstrained", StringComparison.Ordinal))
                {
                    ReadConstraint(file, node, byId, set, variables, entityOf);
                }
            }
        }

        private static bool Whole(DxfTypedValue group, out int value) =>
            int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private static bool Real(DxfTypedValue group, out double value) =>
            double.TryParse(group.Value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

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

        private static List<double[]> Points(List<DxfTypedValue> body)
        {
            var points = new List<double[]>();

            for (int i = 0; i + 2 < body.Count; i++)
            {
                if (
                    (body[i].TypeCode == 10 || body[i].TypeCode == 11)
                    && body[i + 1].TypeCode == body[i].TypeCode + 10
                    && Real(body[i], out double x)
                    && Real(body[i + 1], out double y)
                )
                {
                    points.Add(new[] { x, y });
                    i += 2;
                }
            }

            return points;
        }

        /// <summary>Reads a node of an object: which object (and segment) it is on and the numbers it holds, where it is one the presenter works out.</summary>
        private static bool ReadGeometry(DxfFile file, Raw node, CadConstraintSet set, Func<string, DxfEntity> entityOf)
        {
            if (!node.Class.StartsWith("AcConstrained", StringComparison.Ordinal))
            {
                return true;
            }

            switch (node.Class)
            {
                case "AcConstrainedBoundedLine":
                case "AcConstrainedCircle":
                case "AcConstrainedArc":
                    break;
                case "AcConstrainedImplicitPoint":
                case "AcConstrainedDatumLine":
                case "AcConstrained2PointsConstructionLine":
                    return true;
                default:
                    Refuse(set, "an " + node.Class);
                    return false;
            }

            string dependency = node.Body.Count > 0 && node.Body[0].TypeCode == 330 ? node.Body[0].Value.Trim() : "0";
            DxfItem item = file.ObjectItem(dependency);

            if (item == null || item.Type != "ACDBASSOCGEOMDEPENDENCY")
            {
                Refuse(set, "an object with no dependency");
                return false;
            }

            IReadOnlyList<DxfTypedValue> g = item.Groups;
            int at = CadCenterMarks.Start(g, "AcDbAssocDependency");

            if (at < 0 || at + 7 >= g.Count || g[at + 7].TypeCode != 330)
            {
                Refuse(set, "a dependency it cannot read");
                return false;
            }

            string handle = g[at + 7].Value.Trim().ToUpperInvariant();
            DxfEntity entity = entityOf(handle);
            CadShape shape = CadConstraintShapes.Of(entity);

            if (shape == null)
            {
                Refuse(set, "an object it does not work out");
                return false;
            }

            int segment = -1;

            if (shape.Kind == CadShapeKind.Polyline)
            {
                int geom = CadCenterMarks.Start(g, "AcDbAssocGeomDependency");
                int ids = -1;

                for (int i = geom; i >= 0 && i < g.Count; i++)
                {
                    if (g[i].TypeCode == 1 && g[i].Value.Trim() == "AcDbAssocEdgePersSubentId")
                    {
                        ids = i + 1;
                        break;
                    }
                }

                if (ids < 0 || ids + 2 >= g.Count || !Whole(g[ids + 1], out int from) || !Whole(g[ids + 2], out int to))
                {
                    Refuse(set, "a polyline edge it cannot read");
                    return false;
                }

                int[] vertexIds = VertexIdsOf(entity, shape.Vertices);

                for (int i = 0; i < shape.Segments; i++)
                {
                    if (vertexIds[i] == from && vertexIds[(i + 1) % vertexIds.Length] == to)
                    {
                        segment = i;
                    }
                }

                if (segment < 0)
                {
                    Refuse(set, "a polyline edge it cannot find");
                    return false;
                }
            }

            node.Ref = new CadRef(handle, segment, CadPart.Whole);
            List<double[]> p = Points(node.Body);

            if (node.Class == "AcConstrainedBoundedLine" && p.Count >= 4)
            {
                node.Cached = new[] { p[2][0], p[2][1], p[3][0], p[3][1] };
            }
            else if (node.Class == "AcConstrainedCircle" && p.Count >= 1)
            {
                node.Cached = new[] { p[0][0], p[0][1], Radius(node.Body) };
            }
            else if (node.Class == "AcConstrainedArc" && p.Count >= 5)
            {
                node.Cached = new[] { p[0][0], p[0][1], Radius(node.Body), p[3][0], p[3][1], p[4][0], p[4][1] };
            }
            else
            {
                Refuse(set, "an object it cannot read");
                return false;
            }

            return true;
        }

        private static double Radius(List<DxfTypedValue> body)
        {
            foreach (DxfTypedValue group in body)
            {
                if (group.TypeCode == 40 && Real(group, out double r))
                {
                    return r;
                }
            }

            return 0.0;
        }

        /// <summary>The ids AutoCAD writes for the vertices of a light polyline (group 91), or 1, 2, 3 ... where it has none.</summary>
        internal static int[] VertexIdsOf(DxfEntity entity, int vertices)
        {
            var ids = new List<int>();

            foreach (DxfTypedValue group in entity.Item.Groups)
            {
                if (group.TypeCode == 91 && Whole(group, out int id))
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

        private static bool ReadPoint(Raw node, Dictionary<int, Raw> byId, CadConstraintSet set)
        {
            // The groups: 330, 90, 280 (the kind of point), 90, 90 (the object's node).
            List<DxfTypedValue> b = node.Body;

            if (
                b.Count < 5
                || !Whole(b[2], out int type)
                || !Whole(b[4], out int owner)
                || !byId.TryGetValue(owner, out Raw of)
                || of.Ref == null
            )
            {
                Refuse(set, "a point it cannot read");
                return false;
            }

            CadPart part;

            switch (type)
            {
                case 0:
                    part = CadPart.Start;
                    break;
                case 1:
                    part = CadPart.End;
                    break;
                case 2:
                    part = CadPart.Mid;
                    break;
                case 3:
                    part = CadPart.Center;
                    break;
                default:
                    Refuse(set, "a point of a kind it does not work out");
                    return false;
            }

            node.Owner = of;
            node.PointType = type;
            node.Ref = of.Ref.Of(part);
            return true;
        }

        private static bool Flag(Raw node, int index, out int value)
        {
            // The flags of a constraint: after its 90, the two 290s.
            int seen = 0;

            foreach (DxfTypedValue group in node.Body)
            {
                if (group.TypeCode == 290)
                {
                    if (seen == index)
                    {
                        return Whole(group, out value);
                    }

                    seen++;
                }
            }

            value = 0;
            return false;
        }

        private static CadRef RefOf(Raw node, bool whole) => node?.Ref;

        private static void ReadConstraint(
            DxfFile file,
            Raw node,
            Dictionary<int, Raw> byId,
            CadConstraintSet set,
            Dictionary<string, CadParameter> variables,
            Func<string, DxfEntity> entityOf
        )
        {
            var linked = new List<Raw>();

            foreach (int id in node.Links)
            {
                if (!byId.TryGetValue(id, out Raw other))
                {
                    Refuse(set, "a constraint tied to a node it cannot find");
                    return;
                }

                linked.Add(other);
            }

            // What AutoCAD makes for its own sake is not the user's: a point held to its object, a polyline's joints.
            bool own = Flag(node, 0, out int mine) && mine == 1;

            switch (node.Class)
            {
                case "AcCenterPointConstraint":
                case "AcMidPointConstraint":
                    return;
                case "AcPointCurveConstraint":
                    if (own || linked.Count != 2)
                    {
                        return;
                    }

                    if (
                        linked[0].Ref == null
                        || linked[1].Ref == null
                        || linked[0].Ref.IsPoint == false
                        || linked[1].Ref.IsPoint
                    )
                    {
                        Refuse(set, "a point-on-curve constraint it cannot read");
                        return;
                    }

                    set.Geometric.Add(
                        new CadGeoConstraint(CadGeoKind.PointOnCurve, new[] { linked[0].Ref, linked[1].Ref })
                    );
                    return;
                case "AcPointCoincidenceConstraint":
                    if (own)
                    {
                        return;
                    }

                    Add(set, CadGeoKind.Coincident, linked, 2);
                    return;
                case "AcColinearConstraint":
                    Add(set, CadGeoKind.Collinear, linked, 2);
                    return;
                case "AcConcentricConstraint":
                    Add(set, CadGeoKind.Concentric, linked, 2);
                    return;
                case "AcParallelConstraint":
                    Add(set, CadGeoKind.Parallel, linked, 2);
                    return;
                case "AcPerpendicularConstraint":
                    Add(set, CadGeoKind.Perpendicular, linked, 2);
                    return;
                case "AcEqualLengthConstraint":
                    Add(set, CadGeoKind.EqualLength, linked, 2);
                    return;
                case "AcEqualRadiusConstraint":
                    Add(set, CadGeoKind.EqualRadius, linked, 2);
                    return;
                case "AcHorizontalConstraint":
                    Add(set, CadGeoKind.Horizontal, linked, linked.Count);
                    return;
                case "AcVerticalConstraint":
                    Add(set, CadGeoKind.Vertical, linked, linked.Count);
                    return;
                case "AcSymmetricConstraint":
                    Add(set, CadGeoKind.Symmetric, linked, 3);
                    return;
                case "AcFixedConstraint":
                {
                    if (linked.Count != 1 || linked[0].Ref == null || !linked[0].Ref.IsPoint)
                    {
                        Refuse(set, "a fix constraint it cannot read");
                        return;
                    }

                    var fix = new CadGeoConstraint(CadGeoKind.Fix, new[] { linked[0].Ref });
                    fix.Place = CachedPoint(linked[0]);
                    set.Geometric.Add(fix);
                    return;
                }

                case "AcTangentConstraint":
                {
                    if (linked.Count != 2 || linked[0].Ref == null || linked[1].Ref == null)
                    {
                        Refuse(set, "a tangent constraint it cannot read");
                        return;
                    }

                    var tangent = new CadGeoConstraint(CadGeoKind.Tangent, new[] { linked[0].Ref, linked[1].Ref });
                    tangent.Side = SideOf(linked[0], linked[1]);
                    set.Geometric.Add(tangent);
                    return;
                }

                case "AcDistanceConstraint":
                case "AcAngleConstraint":
                case "AcRadiusDiameterConstraint":
                    ReadDimension(file, node, linked, set, variables, entityOf);
                    return;
                default:
                    Refuse(set, "an " + node.Class);
                    return;
            }
        }

        private static void Add(CadConstraintSet set, CadGeoKind kind, List<Raw> linked, int count)
        {
            if (linked.Count != count)
            {
                Refuse(set, "a " + kind + " constraint it cannot read");
                return;
            }

            var refs = new List<CadRef>();

            foreach (Raw node in linked)
            {
                if (node.Ref == null)
                {
                    Refuse(set, "a " + kind + " constraint it cannot read");
                    return;
                }

                refs.Add(node.Ref);
            }

            // Horizontal or vertical: a line, or two points.
            bool points = linked.Count > 0 && linked[0].Ref.IsPoint;

            if ((kind == CadGeoKind.Horizontal || kind == CadGeoKind.Vertical) && linked.Count == 1 && points)
            {
                Refuse(set, "a " + kind + " constraint it cannot read");
                return;
            }

            set.Geometric.Add(new CadGeoConstraint(kind, refs));
        }

        private static double[] CachedPoint(Raw point)
        {
            if (point.Owner?.Cached == null)
            {
                return null;
            }

            var shape = new CadShape("0", ShapeKind(point.Owner), point.Owner.Cached);
            GeoPoint2? p = CadConstraintShapes.PointOf(shape, new CadRef("0", -1, PartOf(point.PointType)));
            return p.HasValue ? new[] { p.Value.X, p.Value.Y } : null;
        }

        private static CadShapeKind ShapeKind(Raw geometry) =>
            geometry.Class == "AcConstrainedCircle" ? CadShapeKind.Circle
            : geometry.Class == "AcConstrainedArc" ? CadShapeKind.Arc
            : CadShapeKind.Line;

        private static CadPart PartOf(int type) =>
            type == 0 ? CadPart.Start
            : type == 1 ? CadPart.End
            : type == 2 ? CadPart.Mid
            : CadPart.Center;

        /// <summary>
        /// Which side a tangent was made on, from the numbers its nodes hold: for a line and a circle +1 where the circle's centre
        /// lies to the left of the line, -1 to the right; for two circles 1 where they touch from outside, -1 where the first
        /// holds the second, -2 where the second holds the first.
        /// </summary>
        private static int SideOf(Raw a, Raw b)
        {
            if (a.Cached == null || b.Cached == null)
            {
                return 1;
            }

            Raw line = a.Class == "AcConstrainedBoundedLine" ? a : (b.Class == "AcConstrainedBoundedLine" ? b : null);

            if (line != null)
            {
                Raw round = ReferenceEquals(line, a) ? b : a;
                double[] l = line.Cached;
                double dx = l[2] - l[0];
                double dy = l[3] - l[1];
                double across = dx * (round.Cached[1] - l[1]) - dy * (round.Cached[0] - l[0]);
                return across >= 0.0 ? 1 : -1;
            }

            double dist = Math.Sqrt(
                (a.Cached[0] - b.Cached[0]) * (a.Cached[0] - b.Cached[0])
                    + (a.Cached[1] - b.Cached[1]) * (a.Cached[1] - b.Cached[1])
            );
            double external = a.Cached[2] + b.Cached[2];
            double inside = Math.Abs(a.Cached[2] - b.Cached[2]);

            if (Math.Abs(dist - external) <= Math.Abs(dist - inside))
            {
                return 1;
            }

            return a.Cached[2] >= b.Cached[2] ? -1 : -2;
        }

        private static void ReadDimension(
            DxfFile file,
            Raw node,
            List<Raw> linked,
            CadConstraintSet set,
            Dictionary<string, CadParameter> variables,
            Func<string, DxfEntity> entityOf
        )
        {
            // The groups: 90, two 290s, then the handles of the value dependency and the dimension's (340, 340), the kind (280).
            var handles = new List<string>();
            int kind = -1;
            double[] vector = null;
            bool seenKind = false;

            foreach (DxfTypedValue group in node.Body)
            {
                if (group.TypeCode == 340)
                {
                    handles.Add(group.Value.Trim());
                }
                else if (group.TypeCode == 280 && !seenKind && Whole(group, out kind))
                {
                    seenKind = true;
                }
            }

            List<double[]> points = Points(node.Body);

            if (points.Count > 0)
            {
                vector = points[0];
            }

            if (handles.Count < 2 || !seenKind)
            {
                Refuse(set, "a dimensional constraint it cannot read");
                return;
            }

            string variable = TargetOf(file, handles[0]);
            string dimension = handles[1] == "0" ? null : TargetOf(file, handles[1]);

            if (variable == null || !variables.TryGetValue(variable, out CadParameter parameter))
            {
                Refuse(set, "a dimensional constraint with no parameter");
                return;
            }

            var refs = new List<CadRef>();

            foreach (Raw l in linked)
            {
                if (l.Ref == null)
                {
                    Refuse(set, "a dimensional constraint it cannot read");
                    return;
                }

                refs.Add(l.Ref);
            }

            CadDimKind dimKind;
            int direction = 1;

            switch (node.Class)
            {
                case "AcDistanceConstraint":
                    if (refs.Count != 2 || !refs[0].IsPoint || !refs[1].IsPoint)
                    {
                        Refuse(set, "a distance it cannot read");
                        return;
                    }

                    if (kind == 0)
                    {
                        dimKind = CadDimKind.Aligned;
                    }
                    else if (vector != null && Math.Abs(vector[1]) < 1E-9 && Math.Abs(Math.Abs(vector[0]) - 1.0) < 1E-9)
                    {
                        dimKind = CadDimKind.Horizontal;
                        direction = vector[0] < 0.0 ? 1 : -1;
                    }
                    else if (vector != null && Math.Abs(vector[0]) < 1E-9 && Math.Abs(Math.Abs(vector[1]) - 1.0) < 1E-9)
                    {
                        dimKind = CadDimKind.Vertical;
                        direction = vector[1] < 0.0 ? 1 : -1;
                    }
                    else
                    {
                        Refuse(set, "a distance along a slanting direction");
                        return;
                    }

                    break;
                case "AcAngleConstraint":
                    if (refs.Count != 2 || refs[0].IsPoint || refs[1].IsPoint)
                    {
                        Refuse(set, "an angle it cannot read");
                        return;
                    }

                    dimKind = CadDimKind.Angular;
                    break;
                default:
                    if (refs.Count != 1 || refs[0].IsPoint || kind < 0 || kind > 1)
                    {
                        Refuse(set, "a radius or diameter it cannot read");
                        return;
                    }

                    dimKind = kind == 1 ? CadDimKind.Diameter : CadDimKind.Radius;
                    break;
            }

            DxfEntity shown = dimension == null ? null : entityOf(dimension);
            set.Dimensional.Add(
                new CadDimConstraint(dimKind, refs, parameter.Name)
                {
                    Direction = direction,
                    DimensionHandle = shown?.Handle,
                    Dynamic =
                        shown == null || string.Equals(shown.Layer, DynamicLayer, StringComparison.OrdinalIgnoreCase),
                    Source = node.Id.ToString(CultureInfo.InvariantCulture),
                }
            );
        }

        /// <summary>The layer AutoCAD 2021 draws dynamic dimensional constraints on.</summary>
        internal const string DynamicLayer = "*ADSK_CONSTRAINTS";

        /// <summary>The object a dependency is on: its first 330 after the 90 of -10000, 0 or the like.</summary>
        private static string TargetOf(DxfFile file, string dependency)
        {
            DxfItem item = file.ObjectItem(dependency);

            if (item == null)
            {
                return null;
            }

            int at = CadCenterMarks.Start(item.Groups, "AcDbAssocDependency");
            return at >= 0 && at + 7 < item.Groups.Count && item.Groups[at + 7].TypeCode == 330
                ? item.Groups[at + 7].Value.Trim()
                : null;
        }
    }
}
