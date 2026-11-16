using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region The associative network of a dynamic block going down

        /// <summary>
        /// Gives an action of the associative network of a dynamic block (ACDBASSOCNETWORK, ACDBASSOCVARIABLE,
        /// ACDBASSOC2DCONSTRAINTGROUP) the layout of AutoCAD 2010 and 2007 when it goes to a version before R2013, as AutoCAD
        /// 2021 writes it there: the action at version 1, without the four numbers the version 2 of R2013 and R2018 adds after
        /// its three, and a constraint group in its first layout (see <see cref="GroupDown"/>). An action that is not at the
        /// version 2 is left as it is.
        /// </summary>
        private void ActionDown(Record record)
        {
            List<Group> groups = record.Groups;
            int at = groups.FindIndex(g => g.Code == 100 && g.Value == "AcDbAssocAction") + 1;

            // The version, the status, the network and the body, three numbers, then the four of the version 2.
            if (at <= 0 || at + 11 > groups.Count || !Number(groups[at].Value, out int version) || version < 2)
            {
                return;
            }

            for (int i = at + 7; i < at + 11; i++)
            {
                if (groups[i].Code != 90)
                {
                    return;
                }
            }

            List<Group> own = null;

            if (record.Type == "ACDBASSOC2DCONSTRAINTGROUP")
            {
                own = GroupDown(record, at + 11);

                if (own == null)
                {
                    return;
                }
            }

            groups[at] = Whole(90, 1);

            if (own == null)
            {
                groups.RemoveRange(at + 7, 4);
            }
            else
            {
                groups.RemoveRange(at + 7, groups.Count - at - 7);
                groups.AddRange(own);
            }
        }

        /// <summary>A node of a constraint group of the version 2: its class, identifier, byte, the identifiers it names and what its class holds.</summary>
        private sealed class ConstraintNode
        {
            internal string Kind;
            internal Group Id;
            internal Group Byte;
            internal readonly List<Group> Numbers = new List<Group>();
            internal readonly List<Group> Fields = new List<Group>();
        }

        /// <summary>
        /// Gives the groups of a constraint group of the version 2 (AutoCAD 2018: past the plane, the actions it owns and the next identifier,
        /// two numbers and a flag, a directory of every node as class and identifier, then the nodes by identifier without
        /// their class) the layout of the version 1 that AutoCAD 2010 and 2007 write: the count of the nodes, then each node by
        /// identifier with its class (1), its group (330, this record's handle), its identifier, byte and the identifiers it
        /// names, and what its class holds, a circle and an arc with three reals where the version 2 has four (the fourth is
        /// not kept). A group that does not read as the version 2, or holds a node of a class the library does not know, is left
        /// as it is (null). <paramref name="start"/> is where the group's own groups begin, past the action; the groups from there on come back.
        /// </summary>
        private List<Group> GroupDown(Record record, int start)
        {
            List<Group> groups = record.Groups;
            int at = start;

            // The number, the flag, the plane (three points), the owner of the actions, their count and each, the next identifier.
            if (
                !Skip(groups, ref at, 90, 70, 10, 20, 30, 10, 20, 30, 10, 20, 30, 360)
                || !Count(groups, ref at, out int actions)
            )
            {
                return null;
            }

            for (int i = 0; i < actions; i++)
            {
                if (!Skip(groups, ref at, 360))
                {
                    return null;
                }
            }

            if (!Skip(groups, ref at, 90))
            {
                return null;
            }

            int head = at;

            // Two numbers and a flag the first layout has no place for, then the count of the nodes and the directory.
            if (!Skip(groups, ref at, 90, 90, 290) || !Count(groups, ref at, out int count))
            {
                return null;
            }

            var nodes = new List<ConstraintNode>(count);

            for (int i = 0; i < count; i++)
            {
                if (at + 1 >= groups.Count || groups[at].Code != 1 || groups[at + 1].Code != 90)
                {
                    return null;
                }

                string kind = groups[at].Value.Trim();

                if (!DxfConstraintNodes.IsKnown(kind))
                {
                    return null;
                }

                nodes.Add(new ConstraintNode { Kind = kind, Id = groups[at + 1] });
                at += 2;
            }

            foreach (ConstraintNode node in nodes)
            {
                // Its identifier again, the count of the identifiers it names and each, its byte, and what its class holds.
                if (!Skip(groups, ref at, 90) || !Count(groups, ref at, out int named))
                {
                    return null;
                }

                for (int i = 0; i < named; i++)
                {
                    if (at >= groups.Count || groups[at].Code != 90)
                    {
                        return null;
                    }

                    node.Numbers.Add(groups[at++]);
                }

                if (at >= groups.Count || groups[at].Code != 70)
                {
                    return null;
                }

                node.Byte = groups[at++];

                foreach (int code in DxfConstraintNodes.FieldCodes(node.Kind, true))
                {
                    if (at >= groups.Count || groups[at].Code != code)
                    {
                        return null;
                    }

                    node.Fields.Add(groups[at++]);
                }
            }

            // The nodes by identifier, each in the order the directory gave them where identifiers are alike.
            var ordered = new List<KeyValuePair<int, ConstraintNode>>(count);

            for (int i = 0; i < count; i++)
            {
                ordered.Add(new KeyValuePair<int, ConstraintNode>(i, nodes[i]));
            }

            ordered.Sort(
                (a, b) =>
                {
                    int byId = WholeValue(a.Value.Id.Value).CompareTo(WholeValue(b.Value.Id.Value));
                    return byId != 0 ? byId : a.Key.CompareTo(b.Key);
                }
            );

            string handle = HandleOf(record);
            var made = new List<Group>(groups.Count);
            made.AddRange(groups.GetRange(start, head - start));
            made.Add(Whole(90, count));

            foreach (KeyValuePair<int, ConstraintNode> pair in ordered)
            {
                ConstraintNode node = pair.Value;
                made.Add(new Group(1, node.Kind));
                made.Add(new Group(330, handle ?? "0"));
                made.Add(node.Id);
                made.Add(node.Byte);
                made.Add(Whole(90, node.Numbers.Count));
                made.AddRange(node.Numbers);

                for (int i = 0; i < node.Fields.Count; i++)
                {
                    // The pointer, a number and three points of three groups come first, then the four reals; the fourth goes.
                    bool fourth = i == 14 && (node.Kind == "AcConstrainedArc" || node.Kind == "AcConstrainedCircle");

                    if (!fourth)
                    {
                        made.Add(node.Fields[i]);
                    }
                }
            }

            made.AddRange(groups.GetRange(at, groups.Count - at));
            return made;
        }

        /// <summary>Whether the groups from <paramref name="at"/> have the codes given, in order; <paramref name="at"/> moves past them.</summary>
        private static bool Skip(List<Group> groups, ref int at, params int[] codes)
        {
            if (at + codes.Length > groups.Count)
            {
                return false;
            }

            for (int i = 0; i < codes.Length; i++)
            {
                if (groups[at + i].Code != codes[i])
                {
                    return false;
                }
            }

            at += codes.Length;
            return true;
        }

        /// <summary>Reads the whole number of the group (90) at <paramref name="at"/>, which moves past it; false where it is not one or is negative.</summary>
        private static bool Count(List<Group> groups, ref int at, out int count)
        {
            count = 0;

            if (at >= groups.Count || groups[at].Code != 90 || !Number(groups[at].Value, out count) || count < 0)
            {
                return false;
            }

            at++;
            return true;
        }

        #endregion
    }
}
