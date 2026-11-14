using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The objects a dynamic block keeps: the evaluation graph and the expressions it owns (the parameters, grips and
    /// actions of a linear, visibility, flip, rotation, XY, polar, point, base point, lookup and alignment parameter and of
    /// a stretch, polar stretch, move, scale, rotate, flip, array and lookup action, and the components of the locations
    /// of the grips), and the representation data of its anonymous blocks. The layouts follow ACadSharp (MIT) and are
    /// checked against AutoCAD 2021's own DXF of the same drawings.
    /// </content>
    internal sealed partial class DwgObjectReader
    {
        /// <summary>
        /// The start of every expression of a graph (AcDbEvalExpr): a whole number DXF does not give, the two version numbers
        /// (98 and 99), the kind of value the expression holds (none, a real of kind 40 or a short of kind 70) and its id (90),
        /// which DXF gives before the versions.
        /// </summary>
        private void ReadEvalExpression()
        {
            // The first number is -1 in every expression of the samples.
            _data.BitLong();
            int v98 = _data.BitLong();
            int v99 = _data.BitLong();
            int kind = _data.BitShort();

            if (kind > 0 && kind != 40 && kind != 70)
            {
                throw new FormatException(
                    "An expression of a dynamic block holds a value of a kind the reader does not know."
                );
            }

            double real = kind == 40 ? _data.BitDouble() : 0.0;
            int whole = kind == 70 ? _data.BitShort() : 0;
            _object.Add(100, "AcDbEvalExpr");
            _object.Add(90, _data.BitLong());
            AddVersions(v98, v99);

            if (kind > 0)
            {
                // DXF gives a value the expression holds as an empty text, its kind (70) and the value: a real, kind 40, under
                // 140; a short, kind 70, under 70.
                _object.Add(1, new byte[0]);
                _object.Add(70, kind);

                if (kind == 40)
                {
                    _object.Add(140, real);
                }
                else
                {
                    _object.Add(70, whole);
                }
            }
        }

        /// <summary>
        /// The two version numbers (98 and 99) of an expression or an element. AutoCAD 2021 gives an older drawing's as its
        /// own, 33 and 174, in DXF, and so does this reader.
        /// </summary>
        private void AddVersions(int major, int minor)
        {
            if (major < 33)
            {
                major = 33;
                minor = 174;
            }

            _object.Add(98, major);
            _object.Add(99, minor);
        }

        /// <summary>A block element (AcDbBlockElement): the expression, its name, two version numbers and a number.</summary>
        private void ReadBlockElement()
        {
            ReadEvalExpression();
            byte[] name = T();
            int v98 = _data.BitLong();
            int v99 = _data.BitLong();
            _object.Add(100, "AcDbBlockElement");
            _object.Add(300, name);
            AddVersions(v98, v99);
            _object.Add(1071, _data.BitLong());
        }

        /// <summary>A connection to a node of the graph: its id and the name of the property it exposes.</summary>
        private void ReadEvalConnection(int idCode, int nameCode)
        {
            _object.Add(idCode, _data.BitLong());
            _object.Add(nameCode, T());
        }

        /// <summary>A grip of a block (AcDbBlockGrip): the two expressions it takes, its place, whether it cycles and a number.</summary>
        private void ReadBlockGrip()
        {
            ReadBlockElement();
            _object.Add(100, "AcDbBlockGrip");
            _object.Add(91, _data.BitLong());
            _object.Add(92, _data.BitLong());
            _object.AddPoint(1010, _data.Point3());
            _object.Add(280, _data.Bit() ? 1 : 0);
            _object.Add(93, _data.BitLong());
        }

        private void ReadBlockLinearGrip()
        {
            ReadBlockGrip();
            _object.Add(100, "AcDbBlockLinearGrip");
            _object.Add(140, _data.BitDouble());
            _object.Add(141, _data.BitDouble());
            _object.Add(142, _data.BitDouble());
        }

        /// <summary>A grip that holds nothing past a grip's own data: a rotation, XY, polar, visibility or lookup grip.</summary>
        private void ReadPlainGrip(string marker)
        {
            ReadBlockGrip();
            _object.Add(100, marker);
        }

        /// <summary>
        /// An alignment grip: a grip and the direction of the alignment. DXF gives the three numbers as 140 to 142.
        /// </summary>
        private void ReadBlockAlignmentGrip()
        {
            ReadBlockGrip();
            _object.Add(100, "AcDbBlockAlignmentGrip");
            _object.Add(140, _data.BitDouble());
            _object.Add(141, _data.BitDouble());
            _object.Add(142, _data.BitDouble());
        }

        /// <summary>A flip grip: a grip, the id of its flip expression and a direction. DXF gives the direction, then the id (93).</summary>
        private void ReadBlockFlipGrip()
        {
            ReadBlockGrip();
            _object.Add(100, "AcDbBlockFlipGrip");
            int expression = _data.BitLong();
            _object.Add(140, _data.BitDouble());
            _object.Add(141, _data.BitDouble());
            _object.Add(142, _data.BitDouble());
            _object.Add(93, expression);
        }

        private void ReadBlockGripLocationComponent()
        {
            ReadEvalExpression();
            _object.Add(100, "AcDbBlockGripExpr");
            ReadEvalConnection(91, 300);
        }

        /// <summary>The two flags every parameter carries (AcDbBlockParameter): whether it shows its properties, and whether it chains actions.</summary>
        private void ReadBlockParameter()
        {
            ReadBlockElement();
            int properties = _data.Bit() ? 1 : 0;
            int chain = _data.Bit() ? 1 : 0;
            _object.Add(100, "AcDbBlockParameter");
            _object.Add(280, properties);
            _object.Add(281, chain);
        }

        /// <summary>
        /// A parameter of one point (AcDbBlock1PtParameter): its point, the connections of its two displacements (a count
        /// each, under 170 and 171) and the id of its grip, which DXF gives before them.
        /// </summary>
        private void ReadBlock1PtParameter()
        {
            ReadBlockParameter();
            _object.Add(100, "AcDbBlock1PtParameter");
            _object.AddPoint(1010, _data.Point3());
            var connections = new List<DwgGroup>();

            for (int k = 0; k < 2; k++)
            {
                int count = Count(_data.BitShort());
                connections.Add(new DwgGroup(170 + k, count));

                for (int i = 0; i < count; i++)
                {
                    connections.Add(new DwgGroup(91 + k, _data.BitLong()));
                    connections.Add(new DwgGroup(301 + k, T()));
                }
            }

            _object.Add(93, _data.BitLong());
            _object.Body.AddRange(connections);
        }

        /// <summary>
        /// A set of values a parameter allows (a type, a minimum, a maximum, an increment and a list), which DXF gives after an
        /// empty text under <paramref name="start"/>; the type is under <paramref name="type"/>, the three numbers from
        /// <paramref name="value"/>, the count under <paramref name="count"/> and each listed value under <c>value + 3</c>.
        /// </summary>
        private void ReadParameterValueSet(int start, int type, int value, int count)
        {
            _object.Add(start, new byte[0]);
            _object.Add(type, _data.BitLong());
            _object.Add(value, _data.BitDouble());
            _object.Add(value + 1, _data.BitDouble());
            _object.Add(value + 2, _data.BitDouble());
            int values = Count(_data.BitShort());
            _object.Add(count, values);

            for (int i = 0; i < values; i++)
            {
                _object.Add(value + 3, _data.BitDouble());
            }
        }

        /// <summary>
        /// A parameter of two points (AcDbBlock2PtParameter): its flags, both points, the four grips it names, the connections
        /// of its four displacements (each under its own codes) and where the base is.
        /// </summary>
        private void ReadBlock2PtParameter()
        {
            ReadBlockParameter();
            _object.Add(100, "AcDbBlock2PtParameter");
            _object.AddPoint(1010, _data.Point3());
            _object.AddPoint(1011, _data.Point3());
            var connections = new List<DwgGroup>[4];

            for (int k = 0; k < 4; k++)
            {
                int count = Count(_data.BitShort());
                connections[k] = new List<DwgGroup>();
                connections[k].Add(new DwgGroup(171 + k, count));

                for (int i = 0; i < count; i++)
                {
                    connections[k].Add(new DwgGroup(92 + k, _data.BitLong()));
                    connections[k].Add(new DwgGroup(301 + k, T()));
                }
            }

            _object.Add(170, 4);

            for (int k = 0; k < 4; k++)
            {
                _object.Add(91, _data.BitLong());
            }

            for (int k = 0; k < 4; k++)
            {
                _object.Body.AddRange(connections[k]);
            }

            _object.Add(177, _data.BitShort());
        }

        private void ReadBlockLinearParameter()
        {
            ReadBlock2PtParameter();
            _object.Add(100, "AcDbBlockLinearParameter");
            _object.Add(305, T());
            _object.Add(306, T());
            _object.Add(140, _data.BitDouble());
            ReadParameterValueSet(307, 96, 141, 175);
        }

        /// <summary>A flip parameter: a label, a description, the names of its two states, the place of its label and the connection it updates.</summary>
        private void ReadBlockFlipParameter()
        {
            ReadBlock2PtParameter();
            _object.Add(100, "AcDbBlockFlipParameter");
            _object.Add(305, T());
            _object.Add(306, T());
            _object.Add(307, T());
            _object.Add(308, T());
            _object.AddPoint(1012, _data.Point3());
            int id = _data.BitLong();
            _object.Add(309, T());
            _object.Add(96, id);
        }

        /// <summary>A rotation parameter: its second point, a label, a description, the offset of its label and the angles it allows.</summary>
        private void ReadBlockRotationParameter()
        {
            ReadBlock2PtParameter();
            _object.Add(100, "AcDbBlockRotationParameter");
            double[] point = _data.Point3();
            _object.Add(305, T());
            _object.Add(306, T());
            _object.AddPoint(1011, point);
            _object.Add(140, _data.BitDouble());
            ReadParameterValueSet(307, 96, 141, 175);
        }

        /// <summary>An XY parameter: the labels and descriptions of its two directions (Y first), the offsets of their labels (X first in the file, written 141 then 140) and the values each allows.</summary>
        private void ReadBlockXYParameter()
        {
            ReadBlock2PtParameter();
            _object.Add(100, "AcDbBlockXYParameter");

            for (int code = 305; code <= 308; code++)
            {
                _object.Add(code, T());
            }

            double x = _data.BitDouble();
            double y = _data.BitDouble();
            _object.Add(141, x);
            _object.Add(140, y);
            ReadParameterValueSet(309, 97, 146, 176);
            ReadParameterValueSet(410, 96, 142, 175);
        }

        /// <summary>A polar parameter: the label and description of its distance and of its angle, the offset of its label and the values each allows.</summary>
        private void ReadBlockPolarParameter()
        {
            ReadBlock2PtParameter();
            _object.Add(100, "AcDbBlockPolarParameter");

            for (int code = 305; code <= 308; code++)
            {
                _object.Add(code, T());
            }

            _object.Add(140, _data.BitDouble());
            ReadParameterValueSet(309, 96, 141, 175);
            ReadParameterValueSet(410, 97, 145, 176);
        }

        /// <summary>An alignment parameter: a parameter of two points and one flag.</summary>
        private void ReadBlockAlignmentParameter()
        {
            ReadBlock2PtParameter();
            _object.Add(100, "AcDbBlockAlignmentParameter");
            _object.Add(280, _data.Bit() ? 1 : 0);
        }

        /// <summary>A point parameter: a parameter of one point, a label, a description and the place of the point.</summary>
        private void ReadBlockPointParameter()
        {
            ReadBlock1PtParameter();
            _object.Add(100, "AcDbBlockPointParameter");
            _object.Add(303, T());
            _object.Add(304, T());
            _object.AddPoint(1011, _data.Point3());
        }

        /// <summary>A base point parameter: a parameter of one point and two more points.</summary>
        private void ReadBlockBasePointParameter()
        {
            ReadBlock1PtParameter();
            _object.Add(100, "AcDbBlockBasepointParameter");
            _object.AddPoint(1011, _data.Point3());
            _object.AddPoint(1012, _data.Point3());
        }

        /// <summary>A lookup parameter: a parameter of one point, the id of its action, a label and a description (DXF gives the id last).</summary>
        private void ReadBlockLookupParameter()
        {
            ReadBlock1PtParameter();
            _object.Add(100, "AcDbBlockLookUpParameter");
            int action = _data.BitLong();
            _object.Add(303, T());
            _object.Add(304, T());
            _object.Add(94, action);
        }

        /// <summary>
        /// A visibility parameter: a parameter of one point, a flag, a label, a description, a number, the entities it governs
        /// and the states, each a name with the entities it shows and the expressions it owns.
        /// </summary>
        private void ReadBlockVisibilityParameter()
        {
            ReadBlock1PtParameter();
            _object.Add(100, "AcDbBlockVisibilityParameter");
            _object.Add(281, _data.Bit() ? 1 : 0);
            _object.Add(301, T());
            _object.Add(302, T());
            _object.Add(91, _data.Bit() ? 1 : 0);
            int entities = Count(_data.BitLong());
            _object.Add(93, entities);

            for (int i = 0; i < entities; i++)
            {
                _object.Add(331, H());
            }

            int states = Count(_data.BitLong());
            _object.Add(92, states);

            for (int i = 0; i < states; i++)
            {
                _object.Add(303, T());
                int shown = Count(_data.BitLong());
                _object.Add(94, shown);

                for (int j = 0; j < shown; j++)
                {
                    _object.Add(332, H());
                }

                int owned = Count(_data.BitLong());
                _object.Add(95, owned);

                for (int j = 0; j < owned; j++)
                {
                    _object.Add(333, H());
                }
            }
        }

        /// <summary>An action of a block (AcDbBlockAction): its label place, the entities it acts on and the parameters it takes.</summary>
        private void ReadBlockAction()
        {
            ReadBlockElement();
            double[] label = _data.Point3();
            int entities = Count(_data.BitLong());
            int parameters = Count(_data.BitLong());
            _object.Add(100, "AcDbBlockAction");
            _object.Add(70, parameters);

            for (int i = 0; i < parameters; i++)
            {
                _object.Add(91, _data.BitLong());
            }

            _object.Add(71, entities);

            for (int i = 0; i < entities; i++)
            {
                _object.Add(330, H());
            }

            _object.AddPoint(1010, label);
        }

        private void ReadBlockStretchAction()
        {
            ReadBlockAction();
            _object.Add(100, "AcDbBlockStretchAction");
            ReadEvalConnection(92, 301);
            ReadEvalConnection(93, 302);
            int corners = Count(_data.BitLong());
            _object.Add(72, corners);

            for (int i = 0; i < corners; i++)
            {
                _object.AddPoint(1011, _data.RawPoint2());
            }

            int entities = Count(_data.BitLong());
            _object.Add(73, entities);
            var bound = new List<KeyValuePair<ulong, int[]>>(entities);

            for (int i = 0; i < entities; i++)
            {
                ulong entity = H();
                var points = new int[Count(_data.BitLong())];

                for (int j = 0; j < points.Length; j++)
                {
                    points[j] = _data.BitLong();
                }

                bound.Add(new KeyValuePair<ulong, int[]>(entity, points));
            }

            // AutoCAD's DXF lists the entities by handle, whichever order the file keeps them in.
            bound.Sort((a, b) => a.Key.CompareTo(b.Key));

            foreach (KeyValuePair<ulong, int[]> one in bound)
            {
                _object.Add(331, one.Key);
                _object.Add(74, one.Value.Length);

                foreach (int point in one.Value)
                {
                    _object.Add(94, point);
                }
            }

            int nodes = Count(_data.BitLong());
            _object.Add(75, nodes);

            for (int i = 0; i < nodes; i++)
            {
                _object.Add(95, _data.BitLong());
                int points = Count(_data.BitLong());
                _object.Add(76, points);

                for (int j = 0; j < points; j++)
                {
                    _object.Add(94, _data.BitLong());
                }
            }

            _object.Add(140, _data.BitDouble());
            _object.Add(141, _data.BitDouble());
            _object.Add(280, _data.RawChar());
        }

        /// <summary>
        /// The connections of an action, each an id and a name. DXF gives the ids from <paramref name="idFrom"/> and the names
        /// from <paramref name="nameFrom"/>, as pairs where <paramref name="paired"/> is true and as all the ids then all the
        /// names where it is not.
        /// </summary>
        private void ReadActionConnections(int count, int idFrom, int nameFrom, bool paired)
        {
            var ids = new int[count];
            var names = new byte[count][];

            for (int i = 0; i < count; i++)
            {
                ids[i] = _data.BitLong();
                names[i] = T();
            }

            for (int i = 0; i < count; i++)
            {
                _object.Add(idFrom + i, ids[i]);

                if (paired)
                {
                    _object.Add(nameFrom + i, names[i]);
                }
            }

            for (int i = 0; i < count && !paired; i++)
            {
                _object.Add(nameFrom + i, names[i]);
            }
        }

        /// <summary>An action with a base point: an action, the point, the connections of its base, a flag and a second point.</summary>
        private void ReadBlockActionBasePoint()
        {
            ReadBlockAction();
            _object.Add(100, "AcDbBlockActionWithBasePt");
            double[] point = _data.Point3();
            ReadActionConnections(2, 92, 301, false);
            _object.AddPoint(1011, point);
            _object.Add(280, _data.Bit() ? 1 : 0);
            _object.AddPoint(1012, _data.Point3());
        }

        private void ReadBlockRotateAction()
        {
            ReadBlockActionBasePoint();
            _object.Add(100, "AcDbBlockRotationAction");
            ReadEvalConnection(94, 303);
        }

        private void ReadBlockScaleAction()
        {
            ReadBlockActionBasePoint();
            _object.Add(100, "AcDbBlockScaleAction");
            ReadActionConnections(3, 94, 303, false);
            _data.RawChar();
        }

        private void ReadBlockMoveAction()
        {
            ReadBlockAction();
            _object.Add(100, "AcDbBlockMoveAction");
            ReadEvalConnection(92, 301);
            ReadEvalConnection(93, 302);
            _object.Add(140, _data.BitDouble());
            _object.Add(141, _data.BitDouble());
            _object.Add(280, _data.RawChar());
        }

        private void ReadBlockArrayAction()
        {
            ReadBlockAction();
            _object.Add(100, "AcDbBlockArrayAction");
            ReadActionConnections(4, 92, 301, false);
            _object.Add(140, _data.BitDouble());
            _object.Add(141, _data.BitDouble());
        }

        private void ReadBlockFlipAction()
        {
            ReadBlockAction();
            _object.Add(100, "AcDbBlockFlipAction");
            ReadActionConnections(4, 92, 301, false);
        }

        /// <summary>
        /// A polar stretch action: six connections, the corners of the area it stretches, the entities it rotates, the entities
        /// it stretches with the points of each, the nodes it moves, two numbers and a count of numbers that no sample fills.
        /// </summary>
        private void ReadBlockPolarStretchAction()
        {
            ReadBlockAction();
            _object.Add(100, "AcDbBlockPolarStretchAction");
            ReadActionConnections(6, 92, 301, true);
            int corners = Count(_data.BitLong());
            _object.Add(73, corners);

            for (int i = 0; i < corners; i++)
            {
                _object.AddPoint(1011, _data.RawPoint2());
            }

            int rotated = Count(_data.BitLong());
            _object.Add(72, rotated);

            for (int i = 0; i < rotated; i++)
            {
                _object.Add(331, H());
            }

            int stretched = Count(_data.BitLong());
            _object.Add(74, stretched);

            for (int i = 0; i < stretched; i++)
            {
                _object.Add(332, H());
                int points = Count(_data.BitLong());
                _object.Add(75, points);

                for (int j = 0; j < points; j++)
                {
                    _object.Add(76, _data.BitLong());
                }
            }

            int nodes = Count(_data.BitLong());
            _object.Add(78, nodes);

            for (int i = 0; i < nodes; i++)
            {
                _object.Add(98, _data.BitLong());
                int points = Count(_data.BitLong());
                _object.Add(79, points);

                for (int j = 0; j < points; j++)
                {
                    _object.Add(76, _data.BitLong());
                }
            }

            _object.Add(141, _data.BitDouble());
            _object.Add(140, _data.BitDouble());
            int unknown = _data.BitLong();
            _object.Add(77, unknown);

            if (unknown != 0)
            {
                throw new FormatException("A polar stretch action holds numbers the reader does not know.");
            }
        }

        /// <summary>
        /// A lookup action: the rows and columns of its table (the values row by row), and for each column its node, the kind
        /// of its value, whether it looks up, the name it takes where nothing matches, whether it is read only and its connection.
        /// </summary>
        private void ReadBlockLookupAction()
        {
            ReadBlockAction();
            _object.Add(100, "AcDbBlockLookupAction");
            int rows = Count(_data.BitLong());
            int columns = Count(_data.BitLong());
            _object.Add(92, rows);
            _object.Add(93, columns);
            _object.Add(301, new byte[0]);

            // The values are texts of the string stream, each of at least one bit.
            long cells = (long)rows * columns;

            if (cells > (_strings == null ? 0 : _strings.Remaining))
            {
                throw new FormatException("The object names more values than it has room for.");
            }

            for (long i = 0; i < cells; i++)
            {
                _object.Add(302, T());
            }

            for (int i = 0; i < columns; i++)
            {
                _object.Add(303, new byte[0]);
                _object.Add(94, _data.BitLong());
                _object.Add(95, _data.BitLong());
                _object.Add(96, _data.BitLong());
                _object.Add(282, _data.Bit() ? 1 : 0);
                _object.Add(305, T());
                _object.Add(281, _data.Bit() ? 1 : 0);
                _object.Add(304, T());
            }

            _object.Add(280, _data.Bit() ? 1 : 0);
        }

        /// <summary>
        /// The node of a graph that stands for the block's own properties (ACDB_DYNAMICBLOCKPROXYNODE): nothing past the start
        /// of an expression.
        /// </summary>
        private void ReadDynamicBlockProxyNode() => ReadEvalExpression();

        /// <summary>
        /// A value a block properties table or a user parameter holds (an AcDbEvalVariant): the kind (-9999 none, 1 a text of
        /// 300, 40 a real of 140), which DXF gives as 170 with the value after it.
        /// </summary>
        private void ReadVariant()
        {
            int kind = _data.BitShort();
            _object.Add(170, kind);

            switch (kind)
            {
                case -9999:
                    break;
                case 1:
                    _object.Add(300, T());
                    break;
                case 40:
                    _object.Add(140, _data.BitDouble());
                    break;
                default:
                    throw new FormatException(
                        "A block properties table holds a value of a kind the reader does not know."
                    );
            }
        }

        /// <summary>
        /// A user parameter: a parameter of one point, a number, the variable of the constraint network that holds its value
        /// (330), a label and its kind of value (170); the value it holds (309) is none in every sample, which DXF writes as
        /// the text <c>AcDbEvalVariant NULL = -9999</c>.
        /// </summary>
        private void ReadBlockUserParameter()
        {
            ReadBlock1PtParameter();
            _object.Add(100, "AcDbBlockUserParameter");
            _object.Add(90, _data.BitLong());
            _object.Add(330, H());
            _object.Add(301, T());

            if (_data.BitShort() != -9999)
            {
                throw new FormatException("A user parameter holds a value the reader does not know.");
            }

            _object.Add(309, "AcDbEvalVariant NULL = -9999");
            _object.Add(170, _data.BitShort());
        }

        /// <summary>
        /// A block properties table: a parameter of one point, its name and a number, the columns (each the parameter it reads
        /// or sets, the names of its property and connection, the node it stands for, two values, five flags and a text; DXF
        /// gives the parameter before them and a second pointer after) and the rows (a number and a value for each column),
        /// and the row count again with three flags.
        /// </summary>
        private void ReadBlockPropertiesTable()
        {
            ReadBlock1PtParameter();
            _object.Add(100, "AcDbBlockPropertiesTable");
            _object.Add(90, _data.BitLong());
            _object.Add(300, T());
            _object.Add(301, T());
            int columns = Count(_data.BitLong());
            _object.Add(91, columns);

            for (int i = 0; i < columns; i++)
            {
                _object.Add(340, H());
                int first = _data.BitShort();
                int second = _data.BitShort();
                _object.Add(170, first);
                _object.Add(171, second);
                _object.Add(300, T());
                _object.Add(301, T());
                _object.Add(90, _data.BitLong());
                ReadVariant();
                ReadVariant();

                for (int flag = 290; flag <= 294; flag++)
                {
                    _object.Add(flag, _data.Bit() ? 1 : 0);
                }

                _object.Add(302, T());
                _object.Add(340, H());
            }

            int rows = Count(_data.BitLong());
            _object.Add(92, rows);

            for (int i = 0; i < rows; i++)
            {
                _object.Add(90, _data.BitLong());

                for (int j = 0; j < columns; j++)
                {
                    ReadVariant();
                }
            }

            _object.Add(93, _data.BitLong());

            for (int flag = 290; flag <= 292; flag++)
            {
                _object.Add(flag, _data.Bit() ? 1 : 0);
            }
        }

        /// <summary>The representation data of a dynamic block, kept by each anonymous block made from it: a version and the block.</summary>
        private void ReadBlockRepresentationData()
        {
            _object.Add(100, "AcDbBlockRepresentationData");
            _object.Add(70, _data.BitShort());
            _object.Add(340, H());
        }

        /// <summary>
        /// The evaluation graph of a dynamic block: two numbers DXF repeats, the nodes (index, flags, id, the expression
        /// they hold and four numbers) and the edges between them (index, flags, count, the two nodes and five numbers).
        /// </summary>
        private void ReadEvaluationGraph()
        {
            _object.Add(100, "AcDbEvalGraph");
            _object.Add(96, _data.BitLong());
            _object.Add(97, _data.BitLong());
            int nodes = Count(_data.BitLong());

            for (int i = 0; i < nodes; i++)
            {
                _object.Add(91, _data.BitLong());
                _object.Add(93, _data.BitLong());
                _object.Add(95, _data.BitLong());
                _object.Add(360, H());

                for (int j = 0; j < 4; j++)
                {
                    _object.Add(92, _data.BitLong());
                }
            }

            int edges = Count(_data.BitLong());

            for (int i = 0; i < edges; i++)
            {
                _object.Add(92, _data.BitLong());
                _object.Add(93, _data.BitLong());
                _object.Add(94, _data.BitLong());
                _object.Add(91, _data.BitLong());
                _object.Add(91, _data.BitLong());

                for (int j = 0; j < 5; j++)
                {
                    _object.Add(92, _data.BitLong());
                }
            }
        }
    }
}
