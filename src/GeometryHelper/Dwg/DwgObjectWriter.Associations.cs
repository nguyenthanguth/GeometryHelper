using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The objects of the associative network of a dynamic block (<c>DwgObjectReader.Associations</c>): each is written from
    /// the groups of its DXF in their order, its pointers in the handle stream in the order DXF gives them, a soft pointer
    /// (330) as code 4 and a hard owned one (360) as code 3.
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        /// <summary>
        /// The start of an action: its numbers in the data and its two pointers (the network and the body) after them.
        /// <paramref name="written"/> is the version to write where it is not the one the groups give (the four numbers of the
        /// version 2 are taken from the groups where they have them and are 0 where they do not); 0 writes the groups'.
        /// </summary>
        private void WriteAssocAction(
            List<KeyValuePair<int, string>> groups,
            ref int at,
            out ulong network,
            out ulong body,
            int written = 0
        )
        {
            int version = Whole(groups, ref at, 90);
            int status = Whole(groups, ref at, 90);
            network = DwgRecord.Hex(Take(groups, ref at, 330, "0"));
            body = DwgRecord.Hex(Take(groups, ref at, 360, "0"));
            _data.BitLong(written == 0 ? version : written);
            _data.BitLong(status);
            _data.BitLong(Whole(groups, ref at, 90));
            _data.BitLong(Whole(groups, ref at, 90));
            _data.BitLong(Whole(groups, ref at, 90));

            // The version 2 of an action, which AutoCAD 2018 writes, holds four numbers more.
            for (int i = 0; (written == 0 ? version : written) >= 2 && i < 4; i++)
            {
                _data.BitLong(version >= 2 ? Whole(groups, ref at, 90) : 0);
            }
        }

        private void WriteAssocAction(List<KeyValuePair<int, string>> groups, ref int at, int written = 0)
        {
            WriteAssocAction(groups, ref at, out ulong network, out ulong body, written);
            H(4, network);
            H(3, body);
        }

        /// <summary>A value of an association: the kind from the group that follows (a text of <paramref name="textCode"/>, a real or a whole number), none where it is another.</summary>
        private void WriteAssocValue(List<KeyValuePair<int, string>> groups, ref int at, int textCode)
        {
            int code = at < groups.Count ? groups[at].Key : 0;

            if (code == textCode)
            {
                _data.BitShort(1);
                T(Take(groups, ref at, textCode));
            }
            else if (code == 40)
            {
                _data.BitShort(40);
                _data.BitDouble(Real(groups, ref at, 40));
            }
            else if (code == 90)
            {
                _data.BitShort(90);
                _data.BitLong(Whole(groups, ref at, 90));
            }
            else
            {
                _data.BitShort(-9999);
            }
        }

        private void WriteAssocVariable()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteAssocAction(groups, ref at);
            _data.BitLong(Whole(groups, ref at, 90));

            for (int i = 0; i < 4; i++)
            {
                T(Take(groups, ref at, 1));
            }

            WriteAssocValue(groups, ref at, 1);
            _data.Bit(Whole(groups, ref at, 290) != 0);
            _data.Bit(Whole(groups, ref at, 290) != 0);
            int count = Whole(groups, ref at, 90);
            _data.BitLong(count);

            for (int i = 0; i < count; i++)
            {
                H(3, DwgRecord.Hex(Take(groups, ref at, 360, "0")));
                _data.BitLong(Whole(groups, ref at, 90));
            }
        }

        /// <summary>A network: its two numbers, the actions that are written with a bit each (set for a hard owned one), then a last number.</summary>
        private void WriteAssocNetwork()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteAssocAction(groups, ref at);
            _data.BitLong(Whole(groups, ref at, 90));
            _data.BitLong(Whole(groups, ref at, 90));
            int count = Whole(groups, ref at, 90);
            var actions = new List<KeyValuePair<bool, ulong>>();

            for (int i = 0; i < count && at < groups.Count; i++)
            {
                bool owned = groups[at].Key == 360;
                ulong action = DwgRecord.Hex(groups[at++].Value);

                if (_written.Contains(action))
                {
                    actions.Add(new KeyValuePair<bool, ulong>(owned, action));
                }
            }

            _data.BitLong(actions.Count);

            foreach (KeyValuePair<bool, ulong> action in actions)
            {
                _data.Bit(action.Key);
            }

            _data.BitLong(Whole(groups, ref at, 90));

            foreach (KeyValuePair<bool, ulong> action in actions)
            {
                H(action.Key ? 3 : 4, action.Value);
            }
        }

        /// <summary>The numbers, flags and pointers every dependency carries.</summary>
        private void WriteAssocDependency(List<KeyValuePair<int, string>> groups, ref int at)
        {
            _data.BitLong(Whole(groups, ref at, 90));
            _data.BitLong(Whole(groups, ref at, 90));

            for (int i = 0; i < 4; i++)
            {
                _data.Bit(Whole(groups, ref at, 290) != 0);
            }

            _data.BitLong(Whole(groups, ref at, 90));
            ulong first = DwgRecord.Hex(Take(groups, ref at, 330, "0"));
            _data.Bit(Whole(groups, ref at, 290) != 0);
            ulong second = DwgRecord.Hex(Take(groups, ref at, 330, "0"));
            ulong third = DwgRecord.Hex(Take(groups, ref at, 330, "0"));
            ulong body = DwgRecord.Hex(Take(groups, ref at, 360, "0"));
            _data.BitLong(Whole(groups, ref at, 90));
            H(4, first);
            H(4, second);
            H(4, third);
            H(3, body);
        }

        private void WriteAssocDependency()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteAssocDependency(groups, ref at);
        }

        private void WriteAssocGeomDependency()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteAssocDependency(groups, ref at);
            _data.BitLong(Whole(groups, ref at, 90));
            _data.Bit(Whole(groups, ref at, 290) != 0);
            string name = Take(groups, ref at, 1);

            if (R2013Plus)
            {
                // From R2013 the class of the way an edge is named is a flag and a number of a short.
                _data.Bit(false);
                _data.BitShort(name == "AcDbAssocEdgePersSubentId" ? 3 : 1);
            }
            else
            {
                T(name);
            }

            if (name == "AcDbAssocEdgePersSubentId")
            {
                for (int i = 0; i < 3; i++)
                {
                    _data.BitLong(Whole(groups, ref at, 90));
                }
            }

            _data.Bit(Whole(groups, ref at, 290) != 0);
        }

        private void WriteAssocValueDependency()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteAssocDependency(groups, ref at);
            _data.BitLong(Whole(groups, ref at, 90));
            T(Take(groups, ref at, 1));
            WriteAssocValue(groups, ref at, 1);
        }

        private void WriteAssocDependencyBody()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            _data.BitLong(Whole(groups, ref at, 90));
            _data.BitLong(Whole(groups, ref at, 90));
            T(Take(groups, ref at, 1));
            _data.BitLong(Whole(groups, ref at, 90));
        }

        /// <summary>The classes of the nodes of a constraint group that the writer knows (<c>DwgObjectReader.ReadConstraintNode</c>).</summary>
        private static readonly HashSet<string> ConstraintNodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "AcConstrainedImplicitPoint",
            "AcConstrainedBoundedLine",
            "AcConstrainedArc",
            "AcConstrainedCircle",
            "AcConstrainedDatumLine",
            "AcConstrained2PointsConstructionLine",
            "AcPointCurveConstraint",
            "AcPointCoincidenceConstraint",
            "AcMidPointConstraint",
            "AcCenterPointConstraint",
            "AcColinearConstraint",
            "AcParallelConstraint",
            "AcPerpendicularConstraint",
            "AcTangentConstraint",
            "AcEqualLengthConstraint",
            "AcEqualRadiusConstraint",
            "AcFixedConstraint",
            "AcHorizontalConstraint",
            "AcDistanceConstraint",
            "AcAngleConstraint",
            "Ac3PointAngleConstraint",
            "AcRadiusDiameterConstraint",
        };

        /// <summary>Whether every node of the constraint group <paramref name="record"/> is of a class the writer knows.</summary>
        private static bool NodesAreKnown(DwgRecord record) =>
            !record.Body.Exists(group => group.Key == 1 && !ConstraintNodes.Contains(group.Value.Trim()));

        /// <summary>Whether the constraint group <paramref name="record"/> is of the version 2, the layout of AutoCAD 2013 and later, which an older DWG is not written in.</summary>
        private static bool IsConstraintGroup2(DwgRecord record) =>
            record.Body.Exists(group => group.Key == 90)
            && DwgRecord.Int(record.Body.Find(group => group.Key == 90).Value) >= 2;

        /// <summary>
        /// A group of constraints: its number and flag, its plane, the actions it owns that are written, in ascending order of
        /// handle as a DWG keeps them (DXF lists them in the order of AutoCAD's own table), and its nodes.
        /// </summary>
        private void WriteAssocConstraintGroup()
        {
            if (R2013Plus)
            {
                WriteAssocConstraintGroup2();
                return;
            }

            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteAssocAction(groups, ref at);
            _data.BitLong(Whole(groups, ref at, 90));
            _data.Bit(Whole(groups, ref at, 70) != 0);

            for (int i = 0; i < 3; i++)
            {
                _data.Point3(Point(groups, ref at, 10));
            }

            ulong own = DwgRecord.Hex(Take(groups, ref at, 360, "0"));
            int count = Whole(groups, ref at, 90);
            var actions = new List<ulong>();

            for (int i = 0; i < count; i++)
            {
                actions.Add(DwgRecord.Hex(Take(groups, ref at, 360, "0")));
            }

            actions = Written(actions);
            actions.Sort();
            int next = Whole(groups, ref at, 90);
            int nodes = Whole(groups, ref at, 90);
            _data.BitLong(actions.Count);
            _data.BitLong(next);
            _data.BitLong(nodes);
            H(3, own);

            foreach (ulong action in actions)
            {
                H(3, action);
            }

            for (int i = 0; i < nodes; i++)
            {
                WriteConstraintNode(groups, ref at);
            }
        }

        /// <summary>The identifiers of the constraints a piece of geometry takes part in: a count and each number.</summary>
        private void WriteConstraintNumbers(List<KeyValuePair<int, string>> groups, ref int at)
        {
            int count = Whole(groups, ref at, 90);
            _data.BitLong(count);

            for (int i = 0; i < count; i++)
            {
                _data.BitLong(Whole(groups, ref at, 90));
            }
        }

        private void WriteConstraintNode(List<KeyValuePair<int, string>> groups, ref int at)
        {
            string kind = Take(groups, ref at, 1);
            T(kind);
            Take(groups, ref at, 330);
            _data.BitLong(Whole(groups, ref at, 90));
            _data.RawChar(Whole(groups, ref at, 70));
            WriteConstraintNumbers(groups, ref at);
            WriteConstraintFields(kind, groups, ref at, false);
        }

        /// <summary>
        /// What a node holds past its identifiers (and past its byte, in the layout of R2018), by its class; the arcs and
        /// circles of the version 2 have a fourth real, which is 0 where the groups do not have it.
        /// </summary>
        private void WriteConstraintFields(
            string kind,
            List<KeyValuePair<int, string>> groups,
            ref int at,
            bool version2
        )
        {
            switch (kind)
            {
                case "AcConstrainedImplicitPoint":
                    H(4, DwgRecord.Hex(Take(groups, ref at, 330, "0")));
                    _data.BitLong(Whole(groups, ref at, 90));
                    _data.RawChar(Whole(groups, ref at, 280));
                    _data.BitLong(Whole(groups, ref at, 90));
                    _data.BitLong(Whole(groups, ref at, 90));
                    break;
                case "AcConstrainedBoundedLine":
                    H(4, DwgRecord.Hex(Take(groups, ref at, 330, "0")));
                    _data.BitLong(Whole(groups, ref at, 90));
                    _data.Point3(Point(groups, ref at, 10));
                    _data.Point3(Point(groups, ref at, 10));
                    _data.Bit(Whole(groups, ref at, 290) != 0);
                    _data.Point3(Point(groups, ref at, 10));
                    _data.Point3(Point(groups, ref at, 11));
                    break;
                case "AcConstrainedArc":
                case "AcConstrainedCircle":
                    H(4, DwgRecord.Hex(Take(groups, ref at, 330, "0")));
                    _data.BitLong(Whole(groups, ref at, 90));

                    for (int i = 0; i < 3; i++)
                    {
                        _data.Point3(Point(groups, ref at, 10));
                    }

                    for (int i = 0; i < (version2 ? 4 : 3); i++)
                    {
                        _data.BitDouble(Real(groups, ref at, 40));
                    }

                    if (kind == "AcConstrainedArc")
                    {
                        _data.Point3(Point(groups, ref at, 10));
                        _data.Point3(Point(groups, ref at, 11));
                    }

                    break;
                case "AcConstrainedDatumLine":
                case "AcConstrained2PointsConstructionLine":
                    H(4, DwgRecord.Hex(Take(groups, ref at, 330, "0")));
                    _data.BitLong(Whole(groups, ref at, 90));
                    _data.Point3(Point(groups, ref at, 10));
                    _data.Point3(Point(groups, ref at, 10));
                    break;
                default:
                    _data.BitLong(Whole(groups, ref at, 90));
                    _data.Bit(Whole(groups, ref at, 290) != 0);
                    _data.Bit(Whole(groups, ref at, 290) != 0);

                    if (kind == "AcHorizontalConstraint")
                    {
                        _data.BitLong(Whole(groups, ref at, 90));
                    }
                    else if (
                        kind == "AcDistanceConstraint"
                        || kind == "AcAngleConstraint"
                        || kind == "Ac3PointAngleConstraint"
                        || kind == "AcRadiusDiameterConstraint"
                    )
                    {
                        H(5, DwgRecord.Hex(Take(groups, ref at, 340, "0")));
                        H(5, DwgRecord.Hex(Take(groups, ref at, 340, "0")));
                        _data.RawChar(Whole(groups, ref at, 280));

                        if (kind == "AcDistanceConstraint")
                        {
                            _data.Point3(Point(groups, ref at, 10));
                        }
                    }

                    break;
            }
        }

        /// <summary>A node of a constraint group, taken from the groups of either layout of its DXF.</summary>
        private sealed class ConstraintNodeData
        {
            public string Kind;
            public int Id;
            public int Order;
            public int Byte;
            public readonly List<int> Numbers = new List<int>();
            public readonly List<KeyValuePair<int, string>> Fields = new List<KeyValuePair<int, string>>();
        }

        /// <summary>The codes of the groups a node of <paramref name="kind"/> holds past its byte, in the order of DXF.</summary>
        private static List<int> ConstraintFieldCodes(string kind, bool version2)
        {
            var codes = new List<int>();

            void AddPoint(int code)
            {
                codes.AddRange(new[] { code, code + 10, code + 20 });
            }

            switch (kind)
            {
                case "AcConstrainedImplicitPoint":
                    codes.AddRange(new[] { 330, 90, 280, 90, 90 });
                    break;
                case "AcConstrainedBoundedLine":
                    codes.AddRange(new[] { 330, 90 });
                    AddPoint(10);
                    AddPoint(10);
                    codes.Add(290);
                    AddPoint(10);
                    AddPoint(11);
                    break;
                case "AcConstrainedArc":
                case "AcConstrainedCircle":
                    codes.AddRange(new[] { 330, 90 });

                    for (int i = 0; i < 3; i++)
                    {
                        AddPoint(10);
                    }

                    for (int i = 0; i < (version2 ? 4 : 3); i++)
                    {
                        codes.Add(40);
                    }

                    if (kind == "AcConstrainedArc")
                    {
                        AddPoint(10);
                        AddPoint(11);
                    }

                    break;
                case "AcConstrainedDatumLine":
                case "AcConstrained2PointsConstructionLine":
                    codes.AddRange(new[] { 330, 90 });
                    AddPoint(10);
                    AddPoint(10);
                    break;
                default:
                    codes.AddRange(new[] { 90, 290, 290 });

                    if (kind == "AcHorizontalConstraint")
                    {
                        codes.Add(90);
                    }
                    else if (
                        kind == "AcDistanceConstraint"
                        || kind == "AcAngleConstraint"
                        || kind == "Ac3PointAngleConstraint"
                        || kind == "AcRadiusDiameterConstraint"
                    )
                    {
                        codes.AddRange(new[] { 340, 340, 280 });

                        if (kind == "AcDistanceConstraint")
                        {
                            AddPoint(10);
                        }
                    }

                    break;
            }

            return codes;
        }

        /// <summary>
        /// The nodes of a constraint group from its groups: where the groups are of the version 2 (the layout of AutoCAD 2018,
        /// <c>DwgObjectReader.ReadAssocConstraintGroup2</c>) a directory of class and identifier and then each node by its
        /// identifier; where they are not, each node from its class on.
        /// </summary>
        private static List<ConstraintNodeData> TakeConstraintNodes(
            List<KeyValuePair<int, string>> groups,
            ref int at,
            int count,
            bool version2
        )
        {
            var nodes = new List<ConstraintNodeData>();

            for (int i = 0; i < count && at < groups.Count; i++)
            {
                var node = new ConstraintNodeData { Order = i, Kind = Take(groups, ref at, 1) };
                nodes.Add(node);

                if (version2)
                {
                    node.Id = Whole(groups, ref at, 90);
                    continue;
                }

                Take(groups, ref at, 330);
                node.Id = Whole(groups, ref at, 90);
                node.Byte = Whole(groups, ref at, 70);
                TakeConstraintNumbers(node, groups, ref at);

                while (at < groups.Count && groups[at].Key != 1)
                {
                    node.Fields.Add(groups[at++]);
                }
            }

            if (version2)
            {
                foreach (ConstraintNodeData node in nodes)
                {
                    Take(groups, ref at, 90);
                    TakeConstraintNumbers(node, groups, ref at);
                    node.Byte = Whole(groups, ref at, 70);

                    foreach (int code in ConstraintFieldCodes(node.Kind, true))
                    {
                        node.Fields.Add(new KeyValuePair<int, string>(code, Take(groups, ref at, code, "0")));
                    }
                }
            }

            return nodes;
        }

        private static void TakeConstraintNumbers(
            ConstraintNodeData node,
            List<KeyValuePair<int, string>> groups,
            ref int at
        )
        {
            int numbers = Whole(groups, ref at, 90);

            for (int i = 0; i < numbers; i++)
            {
                node.Numbers.Add(Whole(groups, ref at, 90));
            }
        }

        /// <summary>
        /// A group of constraints in the layout of R2018, from the groups of either layout of its DXF (a group of the version
        /// 1 is brought up to the version 2, as AutoCAD does when it saves one): the action, the number and flag, the plane,
        /// the actions it owns, two numbers and a flag that are 0, the table of the classes of its nodes in the order they are
        /// first named by identifier, the directory of the nodes (the table's number of the class, from 1, and the
        /// identifier), and the nodes in the order of the directory.
        /// </summary>
        private void WriteAssocConstraintGroup2()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            bool version2 = groups.Count > 0 && DwgRecord.Int(groups[0].Value) >= 2;
            WriteAssocAction(groups, ref at, 2);
            _data.BitLong(Whole(groups, ref at, 90));
            _data.Bit(Whole(groups, ref at, 70) != 0);

            for (int i = 0; i < 3; i++)
            {
                _data.Point3(Point(groups, ref at, 10));
            }

            ulong own = DwgRecord.Hex(Take(groups, ref at, 360, "0"));
            int count = Whole(groups, ref at, 90);
            var actions = new List<ulong>();

            for (int i = 0; i < count; i++)
            {
                actions.Add(DwgRecord.Hex(Take(groups, ref at, 360, "0")));
            }

            actions = Written(actions);
            actions.Sort();
            int next = Whole(groups, ref at, 90);
            int first = 0;
            int second = 0;

            if (version2)
            {
                first = Whole(groups, ref at, 90);
                second = Whole(groups, ref at, 90);
                Take(groups, ref at, 290);
            }

            int nodeCount = Whole(groups, ref at, 90);
            List<ConstraintNodeData> nodes = TakeConstraintNodes(groups, ref at, nodeCount, version2);
            nodes.Sort((a, b) => a.Id != b.Id ? a.Id.CompareTo(b.Id) : a.Order.CompareTo(b.Order));
            var classes = new List<string>();

            foreach (ConstraintNodeData node in nodes)
            {
                if (!classes.Contains(node.Kind))
                {
                    classes.Add(node.Kind);
                }
            }

            _data.BitLong(actions.Count);
            _data.BitLong(next);
            _data.BitLong(first);
            _data.BitLong(second);
            _data.Bit(false);
            _data.BitShort(classes.Count);

            foreach (string kind in classes)
            {
                T(kind);
            }

            _data.BitLong(nodes.Count);
            H(3, own);

            foreach (ulong action in actions)
            {
                H(3, action);
            }

            foreach (ConstraintNodeData node in nodes)
            {
                _data.Bit(false);
                _data.BitShort(classes.IndexOf(node.Kind) + 1);
                _data.BitLong(node.Id);
            }

            foreach (ConstraintNodeData node in nodes)
            {
                _data.BitLong(node.Id);
                _data.BitLong(node.Numbers.Count);

                foreach (int number in node.Numbers)
                {
                    _data.BitLong(number);
                }

                _data.RawChar(node.Byte);
                int field = 0;
                WriteConstraintFields(node.Kind, node.Fields, ref field, true);
            }
        }

        /// <summary>A constraint parameter: a parameter of two points, the dependency it is tied to, its labels and its values.</summary>
        private void WriteBlockConstraintParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock2PtParameter(groups, ref at);
            ulong dependency = DwgRecord.Hex(Take(groups, ref at, 330, "0"));
            T(Take(groups, ref at, 305));
            T(Take(groups, ref at, 306));
            _data.BitDouble(Real(groups, ref at, 140));
            WriteParameterValueSet(groups, ref at, 307, 96, 141, 175);
            H(4, dependency);
        }
    }
}
