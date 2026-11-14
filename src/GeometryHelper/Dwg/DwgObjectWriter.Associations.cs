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
        /// <summary>The start of an action: its numbers in the data and its two pointers (the network and the body) after them.</summary>
        private void WriteAssocAction(
            List<KeyValuePair<int, string>> groups,
            ref int at,
            out ulong network,
            out ulong body
        )
        {
            int version = Whole(groups, ref at, 90);
            int status = Whole(groups, ref at, 90);
            network = DwgRecord.Hex(Take(groups, ref at, 330, "0"));
            body = DwgRecord.Hex(Take(groups, ref at, 360, "0"));
            _data.BitLong(version);
            _data.BitLong(status);
            _data.BitLong(Whole(groups, ref at, 90));
            _data.BitLong(Whole(groups, ref at, 90));
            _data.BitLong(Whole(groups, ref at, 90));

            // The version 2 of an action, which AutoCAD 2018 writes, holds four numbers more.
            for (int i = 0; version >= 2 && i < 4; i++)
            {
                _data.BitLong(Whole(groups, ref at, 90));
            }
        }

        private void WriteAssocAction(List<KeyValuePair<int, string>> groups, ref int at)
        {
            WriteAssocAction(groups, ref at, out ulong network, out ulong body);
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

            if (R2018Plus)
            {
                // From R2018 the class of the way an edge is named is a flag and a number of a short.
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

        /// <summary>
        /// A group of constraints: its number and flag, its plane, the actions it owns that are written, in ascending order of
        /// handle as a DWG keeps them (DXF lists them in the order of AutoCAD's own table), and its nodes.
        /// </summary>
        private void WriteAssocConstraintGroup()
        {
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

            switch (kind)
            {
                case "AcConstrainedImplicitPoint":
                    WriteConstraintNumbers(groups, ref at);
                    H(4, DwgRecord.Hex(Take(groups, ref at, 330, "0")));
                    _data.BitLong(Whole(groups, ref at, 90));
                    _data.RawChar(Whole(groups, ref at, 280));
                    _data.BitLong(Whole(groups, ref at, 90));
                    _data.BitLong(Whole(groups, ref at, 90));
                    break;
                case "AcConstrainedBoundedLine":
                    WriteConstraintNumbers(groups, ref at);
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
                    WriteConstraintNumbers(groups, ref at);
                    H(4, DwgRecord.Hex(Take(groups, ref at, 330, "0")));
                    _data.BitLong(Whole(groups, ref at, 90));

                    for (int i = 0; i < 3; i++)
                    {
                        _data.Point3(Point(groups, ref at, 10));
                    }

                    for (int i = 0; i < 3; i++)
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
                    WriteConstraintNumbers(groups, ref at);
                    H(4, DwgRecord.Hex(Take(groups, ref at, 330, "0")));
                    _data.BitLong(Whole(groups, ref at, 90));
                    _data.Point3(Point(groups, ref at, 10));
                    _data.Point3(Point(groups, ref at, 10));
                    break;
                default:
                    WriteConstraintNumbers(groups, ref at);
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
