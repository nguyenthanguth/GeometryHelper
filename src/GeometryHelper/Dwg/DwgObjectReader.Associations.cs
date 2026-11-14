using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The objects of the associative network a dynamic block keeps for its constraint parameters and user parameters: the
    /// networks, the variables, the dependencies and their bodies, and the constraint parameters that lean on them. No
    /// source gives their layouts: each is read from AutoCAD 2021's own DWG bytes and checked, group for group, against its
    /// DXF of the same drawing.
    /// </content>
    internal sealed partial class DwgObjectReader
    {
        /// <summary>
        /// The start of every action (AcDbAssocAction): a version, a status, three numbers DXF gives after its two pointers (the
        /// network it belongs to, as a soft pointer, and an action body, hard owned).
        /// </summary>
        private int ReadAssocAction()
        {
            int version = _data.BitLong();
            int status = _data.BitLong();
            int index = _data.BitLong();
            int first = _data.BitLong();
            int second = _data.BitLong();
            _object.Add(100, "AcDbAssocAction");
            _object.Add(90, version);
            _object.Add(90, status);
            _object.Add(330, H());
            _object.Add(360, H());
            _object.Add(90, index);
            _object.Add(90, first);
            _object.Add(90, second);

            // AutoCAD 2018 writes the version 2 of an action, which holds four numbers more.
            if (version >= 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    _object.Add(90, _data.BitLong());
                }
            }

            return version;
        }

        /// <summary>
        /// A value an association keeps (an AcDbEvalVariant): a kind (none, a text, a real of 40 or a whole number of 90) and
        /// the value under the code that names its kind, left out for none.
        /// </summary>
        private void ReadAssocValue(int textCode)
        {
            int kind = _data.BitShort();

            switch (kind)
            {
                case -9999:
                    break;
                case 1:
                    _object.Add(textCode, T());
                    break;
                case 40:
                    _object.Add(40, _data.BitDouble());
                    break;
                case 90:
                    _object.Add(90, _data.BitLong());
                    break;
                default:
                    throw new FormatException("An association holds a value of a kind the reader does not know.");
            }
        }

        /// <summary>
        /// A variable (AcDbAssocVariable): its version, name, expression, evaluator and a text, its value, two flags and the
        /// objects it feeds, each a hard owned pointer and a number.
        /// </summary>
        private void ReadAssocVariable()
        {
            ReadAssocAction();
            _object.Add(100, "AcDbAssocVariable");
            _object.Add(90, _data.BitLong());
            _object.Add(1, T());
            _object.Add(1, T());
            _object.Add(1, T());
            _object.Add(1, T());
            ReadAssocValue(1);
            _object.Add(290, _data.Bit() ? 1 : 0);
            _object.Add(290, _data.Bit() ? 1 : 0);
            int count = Count(_data.BitLong());
            _object.Add(90, count);

            for (int i = 0; i < count; i++)
            {
                _object.Add(360, H());
                _object.Add(90, _data.BitLong());
            }
        }

        /// <summary>
        /// A network (AcDbAssocNetwork): its version, the highest index of its actions and their count; each action is a pointer,
        /// hard owned (360) where a bit before the list is set and soft (330) where it is not.
        /// </summary>
        private void ReadAssocNetwork()
        {
            ReadAssocAction();
            _object.Add(100, "AcDbAssocNetwork");
            _object.Add(90, _data.BitLong());
            _object.Add(90, _data.BitLong());
            int count = Count(_data.BitLong());
            _object.Add(90, count);
            var owned = new bool[count];

            for (int i = 0; i < count; i++)
            {
                owned[i] = _data.Bit();
            }

            int last = _data.BitLong();

            for (int i = 0; i < count; i++)
            {
                _object.Add(owned[i] ? 360 : 330, H());
            }

            _object.Add(90, last);
        }

        /// <summary>A dependency (AcDbAssocDependency): the numbers, flags and pointers every dependency carries.</summary>
        private void ReadAssocDependency()
        {
            _object.Add(100, "AcDbAssocDependency");
            _object.Add(90, _data.BitLong());
            _object.Add(90, _data.BitLong());

            for (int i = 0; i < 4; i++)
            {
                _object.Add(290, _data.Bit() ? 1 : 0);
            }

            _object.Add(90, _data.BitLong());
            _object.Add(330, H());
            _object.Add(290, _data.Bit() ? 1 : 0);
            _object.Add(330, H());
            _object.Add(330, H());
            _object.Add(360, H());
            _object.Add(90, _data.BitLong());
        }

        /// <summary>
        /// A dependency on a piece of geometry (AcDbAssocGeomDependency): a number, a flag, the class of the way it names the
        /// edge (without a number, or with two numbers after a third) and a flag.
        /// </summary>
        private void ReadAssocGeomDependency()
        {
            ReadAssocDependency();
            _object.Add(100, "AcDbAssocGeomDependency");
            _object.Add(90, _data.BitLong());
            _object.Add(290, _data.Bit() ? 1 : 0);
            byte[] name = EdgeName();
            _object.Add(1, name);

            if (Is(name, "AcDbAssocEdgePersSubentId"))
            {
                _object.Add(90, _data.BitLong());
                _object.Add(90, _data.BitLong());
                _object.Add(90, _data.BitLong());
            }
            else if (!Is(name, "AcDbAssocSingleEdgePersSubentId"))
            {
                throw new FormatException("A dependency on geometry names an edge in a way the reader does not know.");
            }

            _object.Add(290, _data.Bit() ? 1 : 0);
        }

        /// <summary>
        /// The class of the way a dependency names an edge: its name as a text, and from R2013 a flag and a number of a short, 1
        /// for <c>AcDbAssocSingleEdgePersSubentId</c> and 3 for <c>AcDbAssocEdgePersSubentId</c>.
        /// </summary>
        private byte[] EdgeName()
        {
            if (!R2013Plus)
            {
                return T();
            }

            if (_data.Bit())
            {
                throw new FormatException("A dependency on geometry names an edge in a way the reader does not know.");
            }

            switch (_data.BitShort())
            {
                case 1:
                    return Encoding.ASCII.GetBytes("AcDbAssocSingleEdgePersSubentId");
                case 3:
                    return Encoding.ASCII.GetBytes("AcDbAssocEdgePersSubentId");
                default:
                    throw new FormatException(
                        "A dependency on geometry names an edge in a way the reader does not know."
                    );
            }
        }

        private static bool Is(byte[] text, string name) => Encoding.ASCII.GetString(text) == name;

        /// <summary>A dependency on a value (AcDbAssocValueDependency): a number, a name and the value.</summary>
        private void ReadAssocValueDependency()
        {
            ReadAssocDependency();
            _object.Add(100, "AcDbAssocValueDependency");
            _object.Add(90, _data.BitLong());
            _object.Add(1, T());
            ReadAssocValue(1);
        }

        /// <summary>
        /// The body of a dependency that holds a dimension or a block parameter (AcDbAssocDependencyBody and the base of
        /// AcDbImpAssocDimDependencyBody): two versions and a text; <paramref name="last"/> is the marker of the last class,
        /// which holds a number.
        /// </summary>
        private void ReadAssocDependencyBody(string last)
        {
            int first = _data.BitLong();
            int second = _data.BitLong();
            byte[] text = T();
            _object.Add(100, "AcDbAssocDependencyBody");
            _object.Add(90, first);
            _object.Add(100, "AcDbImpAssocDimDependencyBodyBase");
            _object.Add(90, second);
            _object.Add(1, text);
            _object.Add(100, last);
            _object.Add(90, _data.BitLong());
        }

        /// <summary>
        /// A group of 2D constraints (AcDbAssoc2dConstraintGroup): a number and a flag, the plane it lies in (an origin and two
        /// axes), the actions it owns, two numbers (the next identifier and the count of its nodes) and the nodes, the
        /// geometry and constraints the solver keeps. A DWG of R2010 keeps the nodes in an order of its own, which DXF gives by
        /// identifier; a group of the version 2 (AutoCAD 2018) is read by <see cref="ReadAssocConstraintGroup2"/>.
        /// </summary>
        private void ReadAssocConstraintGroup()
        {
            int version = ReadAssocAction();
            _object.Add(90, _data.BitLong());
            _object.Add(70, _data.Bit() ? 1 : 0);

            for (int i = 0; i < 3; i++)
            {
                _object.AddPoint(10, _data.Point3());
            }

            _object.Add(360, H());
            int actions = Count(_data.BitLong());
            int next = _data.BitLong();

            if (version >= 2)
            {
                ReadAssocConstraintGroup2(actions, next);
                return;
            }

            int nodes = Count(_data.BitLong());
            _object.Add(90, actions);

            for (int i = 0; i < actions; i++)
            {
                _object.Add(360, H());
            }

            _object.Add(90, next);
            _object.Add(90, nodes);
            var kept = new List<KeyValuePair<int, List<DwgGroup>>>(nodes);

            for (int i = 0; i < nodes; i++)
            {
                int start = _object.Body.Count;
                int id = ReadConstraintNode();
                kept.Add(
                    new KeyValuePair<int, List<DwgGroup>>(id, _object.Body.GetRange(start, _object.Body.Count - start))
                );
                _object.Body.RemoveRange(start, _object.Body.Count - start);
            }

            kept.Sort((a, b) => a.Key.CompareTo(b.Key));

            foreach (KeyValuePair<int, List<DwgGroup>> node in kept)
            {
                _object.Body.AddRange(node.Value);
            }
        }

        /// <summary>The identifiers of the constraints a piece of geometry takes part in: a count and each number.</summary>
        private void ReadConstraintNumbers()
        {
            int count = Count(_data.BitLong());
            _object.Add(90, count);

            for (int i = 0; i < count; i++)
            {
                _object.Add(90, _data.BitLong());
            }
        }

        /// <summary>
        /// One node of a constraint group: its class (a text), the group (330, which the file does not repeat), its identifier and a byte, and what its class
        /// adds. Returns the identifier.
        /// </summary>
        private int ReadConstraintNode()
        {
            byte[] name = T();
            string kind = Encoding.ASCII.GetString(name);
            _object.Add(1, name);
            _object.Add(330, _object.Handle);
            int id = _data.BitLong();
            _object.Add(90, id);
            _object.Add(70, _data.RawChar());
            ReadConstraintNumbers();
            ReadConstraintFields(kind, false);
            return id;
        }

        /// <summary>
        /// What a node of a constraint group holds past its identifiers (and past its byte, in the layout of R2018), by its
        /// class: the pointer, numbers and points of a piece of geometry, or the flags and pointers of a constraint.
        /// </summary>
        private void ReadConstraintFields(string kind, bool version2)
        {
            switch (kind)
            {
                case "AcConstrainedImplicitPoint":
                    _object.Add(330, H());
                    _object.Add(90, _data.BitLong());
                    _object.Add(280, _data.RawChar());
                    _object.Add(90, _data.BitLong());
                    _object.Add(90, _data.BitLong());
                    break;
                case "AcConstrainedBoundedLine":
                    _object.Add(330, H());
                    _object.Add(90, _data.BitLong());
                    _object.AddPoint(10, _data.Point3());
                    _object.AddPoint(10, _data.Point3());
                    _object.Add(290, _data.Bit() ? 1 : 0);
                    _object.AddPoint(10, _data.Point3());
                    _object.AddPoint(11, _data.Point3());
                    break;
                case "AcConstrainedArc":
                    _object.Add(330, H());
                    _object.Add(90, _data.BitLong());

                    for (int i = 0; i < 3; i++)
                    {
                        _object.AddPoint(10, _data.Point3());
                    }

                    for (int i = 0; i < (version2 ? 4 : 3); i++)
                    {
                        _object.Add(40, _data.BitDouble());
                    }

                    _object.AddPoint(10, _data.Point3());
                    _object.AddPoint(11, _data.Point3());
                    break;
                case "AcConstrainedCircle":
                    _object.Add(330, H());
                    _object.Add(90, _data.BitLong());

                    for (int i = 0; i < 3; i++)
                    {
                        _object.AddPoint(10, _data.Point3());
                    }

                    for (int i = 0; i < (version2 ? 4 : 3); i++)
                    {
                        _object.Add(40, _data.BitDouble());
                    }

                    break;
                case "AcConstrainedDatumLine":
                case "AcConstrained2PointsConstructionLine":
                    _object.Add(330, H());
                    _object.Add(90, _data.BitLong());
                    _object.AddPoint(10, _data.Point3());
                    _object.AddPoint(10, _data.Point3());
                    break;
                case "AcPointCurveConstraint":
                case "AcPointCoincidenceConstraint":
                case "AcMidPointConstraint":
                case "AcCenterPointConstraint":
                case "AcColinearConstraint":
                case "AcParallelConstraint":
                case "AcPerpendicularConstraint":
                case "AcTangentConstraint":
                case "AcEqualLengthConstraint":
                case "AcEqualRadiusConstraint":
                case "AcFixedConstraint":
                case "AcHorizontalConstraint":
                case "AcDistanceConstraint":
                case "AcAngleConstraint":
                case "Ac3PointAngleConstraint":
                case "AcRadiusDiameterConstraint":
                    _object.Add(90, _data.BitLong());
                    _object.Add(290, _data.Bit() ? 1 : 0);
                    _object.Add(290, _data.Bit() ? 1 : 0);

                    if (kind == "AcHorizontalConstraint")
                    {
                        _object.Add(90, _data.BitLong());
                    }
                    else if (
                        kind == "AcDistanceConstraint"
                        || kind == "AcAngleConstraint"
                        || kind == "Ac3PointAngleConstraint"
                        || kind == "AcRadiusDiameterConstraint"
                    )
                    {
                        _object.Add(340, H());
                        _object.Add(340, H());
                        _object.Add(280, _data.RawChar());

                        if (kind == "AcDistanceConstraint")
                        {
                            _object.AddPoint(10, _data.Point3());
                        }
                    }

                    break;
                default:
                    throw new FormatException("A constraint group holds a node of a class the reader does not know.");
            }
        }

        /// <summary>
        /// The rest of a group of the version 2 of AutoCAD 2018, past the next identifier: two numbers and a flag, then the
        /// table of the classes of its nodes (a count and the names, in the order the nodes first name them), the count of the
        /// nodes, a directory of every node (its class by the number of the table, from 1, and its identifier) and then the
        /// nodes themselves, each with its identifier first, then its identifiers, a byte, and what its class adds. DXF gives
        /// the same in that order: the numbers and the flag, the count, the directory as class name and identifier, and the
        /// nodes without their class.
        /// </summary>
        private void ReadAssocConstraintGroup2(int actions, int next)
        {
            int first = _data.BitLong();
            int second = _data.BitLong();

            if (_data.Bit())
            {
                throw new FormatException(
                    "A constraint group names the classes of its nodes in a way the reader does not know."
                );
            }

            int classes = Count(_data.BitShort());
            var names = new byte[classes][];

            for (int i = 0; i < classes; i++)
            {
                names[i] = T();
            }

            int nodes = Count(_data.BitLong());
            _object.Add(90, actions);

            for (int i = 0; i < actions; i++)
            {
                _object.Add(360, H());
            }

            _object.Add(90, next);
            _object.Add(90, first);
            _object.Add(90, second);
            _object.Add(290, 0);
            _object.Add(90, nodes);
            var ids = new int[nodes];
            var kinds = new string[nodes];

            for (int i = 0; i < nodes; i++)
            {
                if (_data.Bit())
                {
                    throw new FormatException(
                        "A constraint group names the class of a node in a way the reader does not know."
                    );
                }

                int number = _data.BitShort();

                if (number < 1 || number > classes)
                {
                    throw new FormatException("A constraint group names a class its table does not hold.");
                }

                ids[i] = _data.BitLong();
                kinds[i] = Encoding.ASCII.GetString(names[number - 1]);
                _object.Add(1, names[number - 1]);
                _object.Add(90, ids[i]);
            }

            for (int i = 0; i < nodes; i++)
            {
                int id = _data.BitLong();

                if (id != ids[i])
                {
                    throw new FormatException("A constraint group holds a node that its directory does not name.");
                }

                _object.Add(90, id);
                ReadConstraintNumbers();
                _object.Add(70, _data.RawChar());
                ReadConstraintFields(kinds[i], true);
            }
        }

        /// <summary>The first of a constraint parameter's classes past its two points: the dependency it is tied to and its value set.</summary>
        private void ReadBlockConstraintParameter(string marker)
        {
            ReadBlock2PtParameter();
            _object.Add(100, "AcDbBlockConstraintParameter");
            _object.Add(330, H());

            if (marker != "AcDbBlockRadialConstraintParameter")
            {
                _object.Add(100, "AcDbBlockLinearConstraintParameter");
            }
            else
            {
                _object.Add(100, marker);
            }

            _object.Add(305, T());
            _object.Add(306, T());
            _object.Add(140, _data.BitDouble());
            ReadParameterValueSet(307, 96, 141, 175);

            if (marker != "AcDbBlockRadialConstraintParameter")
            {
                _object.Add(100, marker);
            }
        }
    }
}
