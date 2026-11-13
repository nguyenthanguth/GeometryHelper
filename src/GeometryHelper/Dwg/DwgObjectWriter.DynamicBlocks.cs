using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The objects a dynamic block keeps: the evaluation graph and the expressions it owns (the parameters, grips and
    /// actions the reader names), and the representation data of its anonymous blocks. Each is written from the groups of
    /// its DXF in the order AutoCAD gives them, the layouts of <c>DwgObjectReader.DynamicBlocks</c>.
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        /// <summary>The groups of the record's data in order, the subclass markers left out.</summary>
        private List<KeyValuePair<int, string>> Sequence() => _record.Body.FindAll(group => group.Key != 100);

        private static int Whole(List<KeyValuePair<int, string>> groups, ref int at, int code, int otherwise = 0) =>
            DwgRecord.Int(Take(groups, ref at, code, otherwise.ToString()));

        /// <summary>
        /// A real of the groups. AutoCAD's DXF gives double.MaxValue, which it keeps for a value not set yet, as
        /// 1.797693134862314E+99, a number of its own: the DWG holds the largest real.
        /// </summary>
        private static double Real(List<KeyValuePair<int, string>> groups, ref int at, int code)
        {
            double value = DwgRecord.Double(Take(groups, ref at, code, "0"));
            return Math.Abs(value) == Unset ? Math.Sign(value) * double.MaxValue : value;
        }

        private const double Unset = 1.797693134862314E+99;

        private static double[] Point(List<KeyValuePair<int, string>> groups, ref int at, int code)
        {
            double x = Real(groups, ref at, code);
            double y = Real(groups, ref at, code + 10);
            return new[] { x, y, Real(groups, ref at, code + 20) };
        }

        /// <summary>
        /// The start of an expression: a -1, the two versions, the kind of the value it holds (-9999 for none; a real is kind
        /// 40 and a short kind 70, which DXF gives after an empty text as 70 and the value under 140 or 70) and the id, which
        /// DXF gives first.
        /// </summary>
        private void WriteEvalExpression(List<KeyValuePair<int, string>> groups, ref int at)
        {
            int id = Whole(groups, ref at, 90);
            int major = Whole(groups, ref at, 98, 33);
            int minor = Whole(groups, ref at, 99, 174);
            bool holds = at < groups.Count && groups[at].Key == 1 && at + 1 < groups.Count && groups[at + 1].Key == 70;
            _data.BitLong(-1);
            _data.BitLong(major);
            _data.BitLong(minor);

            if (holds)
            {
                Take(groups, ref at, 1);
                int kind = Whole(groups, ref at, 70);

                if (kind == 40)
                {
                    _data.BitShort(kind);
                    _data.BitDouble(Real(groups, ref at, 140));
                }
                else if (kind == 70)
                {
                    _data.BitShort(kind);
                    _data.BitShort(Whole(groups, ref at, 70));
                }
                else
                {
                    // A value of another kind is left out: AutoCAD evaluates the expression again.
                    at++;
                    _data.BitShort(-9999);
                }
            }
            else
            {
                _data.BitShort(-9999);
            }

            _data.BitLong(id);
        }

        private void WriteBlockElement(List<KeyValuePair<int, string>> groups, ref int at)
        {
            WriteEvalExpression(groups, ref at);
            T(Take(groups, ref at, 300));
            _data.BitLong(Whole(groups, ref at, 98, 33));
            _data.BitLong(Whole(groups, ref at, 99, 174));
            _data.BitLong(Whole(groups, ref at, 1071));
        }

        private void WriteEvalConnection(List<KeyValuePair<int, string>> groups, ref int at, int idCode, int nameCode)
        {
            _data.BitLong(Whole(groups, ref at, idCode));
            T(Take(groups, ref at, nameCode));
        }

        private void WriteBlockGrip(List<KeyValuePair<int, string>> groups, ref int at)
        {
            WriteBlockElement(groups, ref at);
            _data.BitLong(Whole(groups, ref at, 91));
            _data.BitLong(Whole(groups, ref at, 92));
            _data.Point3(Point(groups, ref at, 1010));
            _data.Bit(Whole(groups, ref at, 280) != 0);
            _data.BitLong(Whole(groups, ref at, 93));
        }

        /// <summary>A grip and three numbers (140 to 142): a linear grip's distances, an alignment grip's direction.</summary>
        private void WriteBlockLinearGrip()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockGrip(groups, ref at);
            _data.BitDouble(Real(groups, ref at, 140));
            _data.BitDouble(Real(groups, ref at, 141));
            _data.BitDouble(Real(groups, ref at, 142));
        }

        private void WriteBlockGripLocationComponent()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteEvalExpression(groups, ref at);
            WriteEvalConnection(groups, ref at, 91, 300);
        }

        private void WriteBlockParameter(List<KeyValuePair<int, string>> groups, ref int at)
        {
            WriteBlockElement(groups, ref at);
            _data.Bit(Whole(groups, ref at, 280) != 0);
            _data.Bit(Whole(groups, ref at, 281) != 0);
        }

        /// <summary>A parameter of one point: its point, the connections of its two displacements and its grip, which DXF gives before them.</summary>
        private void WriteBlock1PtParameter(List<KeyValuePair<int, string>> groups, ref int at)
        {
            WriteBlockParameter(groups, ref at);
            _data.Point3(Point(groups, ref at, 1010));
            int grip = Whole(groups, ref at, 93);

            for (int k = 0; k < 2; k++)
            {
                int count = Whole(groups, ref at, 170 + k);
                _data.BitShort(count);

                for (int i = 0; i < count; i++)
                {
                    WriteEvalConnection(groups, ref at, 91 + k, 301 + k);
                }
            }

            _data.BitLong(grip);
        }

        private void WriteBlock2PtParameter(List<KeyValuePair<int, string>> groups, ref int at)
        {
            WriteBlockParameter(groups, ref at);
            _data.Point3(Point(groups, ref at, 1010));
            _data.Point3(Point(groups, ref at, 1011));
            Whole(groups, ref at, 170);
            var grips = new int[4];

            for (int k = 0; k < 4; k++)
            {
                grips[k] = Whole(groups, ref at, 91);
            }

            for (int k = 0; k < 4; k++)
            {
                int count = Whole(groups, ref at, 171 + k);
                _data.BitShort(count);

                for (int i = 0; i < count; i++)
                {
                    WriteEvalConnection(groups, ref at, 92 + k, 301 + k);
                }
            }

            foreach (int grip in grips)
            {
                _data.BitLong(grip);
            }

            _data.BitShort(Whole(groups, ref at, 177));
        }

        /// <summary>The set of values of a parameter: the empty text DXF gives first (left out), a type, three numbers and a list.</summary>
        private void WriteParameterValueSet(
            List<KeyValuePair<int, string>> groups,
            ref int at,
            int start,
            int type,
            int value,
            int count
        )
        {
            Take(groups, ref at, start);
            _data.BitLong(Whole(groups, ref at, type));
            _data.BitDouble(Real(groups, ref at, value));
            _data.BitDouble(Real(groups, ref at, value + 1));
            _data.BitDouble(Real(groups, ref at, value + 2));
            int values = Whole(groups, ref at, count);
            _data.BitShort(values);

            for (int i = 0; i < values; i++)
            {
                _data.BitDouble(Real(groups, ref at, value + 3));
            }
        }

        private void WriteBlockLinearParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock2PtParameter(groups, ref at);
            T(Take(groups, ref at, 305));
            T(Take(groups, ref at, 306));
            _data.BitDouble(Real(groups, ref at, 140));
            WriteParameterValueSet(groups, ref at, 307, 96, 141, 175);
        }

        private void WriteBlockFlipParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock2PtParameter(groups, ref at);

            for (int code = 305; code <= 308; code++)
            {
                T(Take(groups, ref at, code));
            }

            _data.Point3(Point(groups, ref at, 1012));
            string name = Take(groups, ref at, 309);
            _data.BitLong(Whole(groups, ref at, 96));
            T(name);
        }

        private void WriteBlockRotationParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock2PtParameter(groups, ref at);
            string label = Take(groups, ref at, 305);
            string description = Take(groups, ref at, 306);
            _data.Point3(Point(groups, ref at, 1011));
            T(label);
            T(description);
            _data.BitDouble(Real(groups, ref at, 140));
            WriteParameterValueSet(groups, ref at, 307, 96, 141, 175);
        }

        private void WriteBlockXYParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock2PtParameter(groups, ref at);

            for (int code = 305; code <= 308; code++)
            {
                T(Take(groups, ref at, code));
            }

            double x = Real(groups, ref at, 141);
            _data.BitDouble(x);
            _data.BitDouble(Real(groups, ref at, 140));
            WriteParameterValueSet(groups, ref at, 309, 97, 146, 176);
            WriteParameterValueSet(groups, ref at, 410, 96, 142, 175);
        }

        private void WriteBlockPolarParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock2PtParameter(groups, ref at);

            for (int code = 305; code <= 308; code++)
            {
                T(Take(groups, ref at, code));
            }

            _data.BitDouble(Real(groups, ref at, 140));
            WriteParameterValueSet(groups, ref at, 309, 96, 141, 175);
            WriteParameterValueSet(groups, ref at, 410, 97, 145, 176);
        }

        private void WriteBlockAlignmentParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock2PtParameter(groups, ref at);
            _data.Bit(Whole(groups, ref at, 280) != 0);
        }

        private void WriteBlockPointParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock1PtParameter(groups, ref at);
            T(Take(groups, ref at, 303));
            T(Take(groups, ref at, 304));
            _data.Point3(Point(groups, ref at, 1011));
        }

        private void WriteBlockBasePointParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock1PtParameter(groups, ref at);
            _data.Point3(Point(groups, ref at, 1011));
            _data.Point3(Point(groups, ref at, 1012));
        }

        private void WriteBlockLookupParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock1PtParameter(groups, ref at);
            string label = Take(groups, ref at, 303);
            string description = Take(groups, ref at, 304);
            _data.BitLong(Whole(groups, ref at, 94));
            T(label);
            T(description);
        }

        /// <summary>
        /// The entities of a list whose count DXF gives under <paramref name="count"/> and each under <paramref name="code"/>,
        /// those written.
        /// </summary>
        private List<ulong> TakeEntities(List<KeyValuePair<int, string>> groups, ref int at, int count, int code)
        {
            int n = Whole(groups, ref at, count);
            var entities = new List<ulong>();

            for (int i = 0; i < n; i++)
            {
                entities.Add(DwgRecord.Hex(Take(groups, ref at, code)));
            }

            return Written(entities);
        }

        /// <summary>A count and then each entity as a pointer of <paramref name="kind"/>: the count is in the data, the handles in the handle stream.</summary>
        private void WriteEntities(List<ulong> entities, int kind)
        {
            _data.BitLong(entities.Count);

            foreach (ulong entity in entities)
            {
                H(kind, entity);
            }
        }

        private void WriteBlockVisibilityParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlock1PtParameter(groups, ref at);
            _data.Bit(Whole(groups, ref at, 281) != 0);
            T(Take(groups, ref at, 301));
            T(Take(groups, ref at, 302));
            _data.Bit(Whole(groups, ref at, 91) != 0);
            WriteEntities(TakeEntities(groups, ref at, 93, 331), 4);
            int states = Whole(groups, ref at, 92);
            _data.BitLong(states);

            for (int i = 0; i < states; i++)
            {
                T(Take(groups, ref at, 303));
                WriteEntities(TakeEntities(groups, ref at, 94, 332), 4);
                WriteEntities(TakeEntities(groups, ref at, 95, 333), 4);
            }
        }

        /// <summary>A grip that holds nothing past a grip's own data: a rotation, XY, polar, visibility or lookup grip.</summary>
        private void WritePlainGrip()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockGrip(groups, ref at);
        }

        private void WriteBlockFlipGrip()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockGrip(groups, ref at);
            double x = Real(groups, ref at, 140);
            double y = Real(groups, ref at, 141);
            double z = Real(groups, ref at, 142);
            _data.BitLong(Whole(groups, ref at, 93));
            _data.BitDouble(x);
            _data.BitDouble(y);
            _data.BitDouble(z);
        }

        /// <summary>
        /// An action of a block, from its element: the place of its label, the entities it acts on that are written and the
        /// parameters it takes. DXF gives the parameters, then the entities, then the place.
        /// </summary>
        private void WriteBlockAction(List<KeyValuePair<int, string>> groups, ref int at)
        {
            WriteBlockElement(groups, ref at);
            int count = Whole(groups, ref at, 70);
            var parameters = new List<int>();

            for (int i = 0; i < count; i++)
            {
                parameters.Add(Whole(groups, ref at, 91));
            }

            count = Whole(groups, ref at, 71);
            var entities = new List<ulong>();

            for (int i = 0; i < count; i++)
            {
                entities.Add(DwgRecord.Hex(Take(groups, ref at, 330)));
            }

            entities = Written(entities);
            _data.Point3(Point(groups, ref at, 1010));
            _data.BitLong(entities.Count);
            _data.BitLong(parameters.Count);

            foreach (int parameter in parameters)
            {
                _data.BitLong(parameter);
            }

            foreach (ulong entity in entities)
            {
                H(4, entity);
            }
        }

        private void WriteBlockStretchAction()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockAction(groups, ref at);
            WriteEvalConnection(groups, ref at, 92, 301);
            WriteEvalConnection(groups, ref at, 93, 302);
            int corners = Whole(groups, ref at, 72);
            _data.BitLong(corners);

            for (int i = 0; i < corners; i++)
            {
                double x = Real(groups, ref at, 1011);
                _data.RawPoint2(new[] { x, Real(groups, ref at, 1021) });
            }

            int bindings = Whole(groups, ref at, 73);
            var kept = new List<KeyValuePair<ulong, List<int>>>();

            for (int i = 0; i < bindings; i++)
            {
                ulong entity = DwgRecord.Hex(Take(groups, ref at, 331));
                int count = Whole(groups, ref at, 74);
                var points = new List<int>();

                for (int j = 0; j < count; j++)
                {
                    points.Add(Whole(groups, ref at, 94));
                }

                if (_written.Contains(entity))
                {
                    kept.Add(new KeyValuePair<ulong, List<int>>(entity, points));
                }
            }

            _data.BitLong(kept.Count);

            foreach (KeyValuePair<ulong, List<int>> binding in kept)
            {
                H(4, binding.Key);
                _data.BitLong(binding.Value.Count);

                foreach (int point in binding.Value)
                {
                    _data.BitLong(point);
                }
            }

            int nodes = Whole(groups, ref at, 75);
            _data.BitLong(nodes);

            for (int i = 0; i < nodes; i++)
            {
                _data.BitLong(Whole(groups, ref at, 95));
                int count = Whole(groups, ref at, 76);
                _data.BitLong(count);

                for (int j = 0; j < count; j++)
                {
                    _data.BitLong(Whole(groups, ref at, 94));
                }
            }

            _data.BitDouble(Real(groups, ref at, 140));
            _data.BitDouble(Real(groups, ref at, 141));
            _data.RawChar(Whole(groups, ref at, 280));
        }

        /// <summary>
        /// The connections of an action from their groups, the ids from <paramref name="idFrom"/> and the names from
        /// <paramref name="nameFrom"/>, as pairs where <paramref name="paired"/> is true and as all the ids then all the names
        /// where it is not.
        /// </summary>
        private static KeyValuePair<int, string>[] TakeConnections(
            List<KeyValuePair<int, string>> groups,
            ref int at,
            int count,
            int idFrom,
            int nameFrom,
            bool paired
        )
        {
            var ids = new int[count];
            var names = new string[count];

            for (int i = 0; i < count; i++)
            {
                ids[i] = Whole(groups, ref at, idFrom + i);
                names[i] = paired ? Take(groups, ref at, nameFrom + i) : null;
            }

            var connections = new KeyValuePair<int, string>[count];

            for (int i = 0; i < count; i++)
            {
                connections[i] = new KeyValuePair<int, string>(ids[i], names[i] ?? Take(groups, ref at, nameFrom + i));
            }

            return connections;
        }

        /// <summary>Each connection as its id and its name.</summary>
        private void PutConnections(KeyValuePair<int, string>[] connections)
        {
            foreach (KeyValuePair<int, string> connection in connections)
            {
                _data.BitLong(connection.Key);
                T(connection.Value);
            }
        }

        private void WriteBlockActionBasePoint(List<KeyValuePair<int, string>> groups, ref int at)
        {
            WriteBlockAction(groups, ref at);
            KeyValuePair<int, string>[] connections = TakeConnections(groups, ref at, 2, 92, 301, false);
            _data.Point3(Point(groups, ref at, 1011));
            PutConnections(connections);
            _data.Bit(Whole(groups, ref at, 280) != 0);
            _data.Point3(Point(groups, ref at, 1012));
        }

        private void WriteBlockRotateAction()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockActionBasePoint(groups, ref at);
            WriteEvalConnection(groups, ref at, 94, 303);
        }

        private void WriteBlockScaleAction()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockActionBasePoint(groups, ref at);
            PutConnections(TakeConnections(groups, ref at, 3, 94, 303, false));
            _data.RawChar(0);
        }

        private void WriteBlockMoveAction()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockAction(groups, ref at);
            WriteEvalConnection(groups, ref at, 92, 301);
            WriteEvalConnection(groups, ref at, 93, 302);
            _data.BitDouble(Real(groups, ref at, 140));
            _data.BitDouble(Real(groups, ref at, 141));
            _data.RawChar(Whole(groups, ref at, 280));
        }

        private void WriteBlockArrayAction()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockAction(groups, ref at);
            PutConnections(TakeConnections(groups, ref at, 4, 92, 301, false));
            _data.BitDouble(Real(groups, ref at, 140));
            _data.BitDouble(Real(groups, ref at, 141));
        }

        private void WriteBlockFlipAction()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockAction(groups, ref at);
            PutConnections(TakeConnections(groups, ref at, 4, 92, 301, false));
        }

        private void WriteBlockPolarStretchAction()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockAction(groups, ref at);
            PutConnections(TakeConnections(groups, ref at, 6, 92, 301, true));
            int corners = Whole(groups, ref at, 73);
            _data.BitLong(corners);

            for (int i = 0; i < corners; i++)
            {
                double x = Real(groups, ref at, 1011);
                _data.RawPoint2(new[] { x, Real(groups, ref at, 1021) });
            }

            WriteEntities(TakeEntities(groups, ref at, 72, 331), 4);
            int bindings = Whole(groups, ref at, 74);
            var kept = new List<KeyValuePair<ulong, List<int>>>();

            for (int i = 0; i < bindings; i++)
            {
                ulong entity = DwgRecord.Hex(Take(groups, ref at, 332));
                int count = Whole(groups, ref at, 75);
                var points = new List<int>();

                for (int j = 0; j < count; j++)
                {
                    points.Add(Whole(groups, ref at, 76));
                }

                if (_written.Contains(entity))
                {
                    kept.Add(new KeyValuePair<ulong, List<int>>(entity, points));
                }
            }

            _data.BitLong(kept.Count);

            foreach (KeyValuePair<ulong, List<int>> binding in kept)
            {
                H(4, binding.Key);
                _data.BitLong(binding.Value.Count);

                foreach (int point in binding.Value)
                {
                    _data.BitLong(point);
                }
            }

            int nodes = Whole(groups, ref at, 78);
            _data.BitLong(nodes);

            for (int i = 0; i < nodes; i++)
            {
                _data.BitLong(Whole(groups, ref at, 98));
                int count = Whole(groups, ref at, 79);
                _data.BitLong(count);

                for (int j = 0; j < count; j++)
                {
                    _data.BitLong(Whole(groups, ref at, 76));
                }
            }

            double distance = Real(groups, ref at, 141);
            _data.BitDouble(distance);
            _data.BitDouble(Real(groups, ref at, 140));
            _data.BitLong(Whole(groups, ref at, 77));
        }

        private void WriteBlockLookupAction()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockAction(groups, ref at);
            int rows = Whole(groups, ref at, 92);
            int columns = Whole(groups, ref at, 93);
            Take(groups, ref at, 301);
            _data.BitLong(rows);
            _data.BitLong(columns);

            for (long i = 0; i < (long)rows * columns; i++)
            {
                T(Take(groups, ref at, 302));
            }

            for (int i = 0; i < columns; i++)
            {
                Take(groups, ref at, 303);
                _data.BitLong(Whole(groups, ref at, 94));
                _data.BitLong(Whole(groups, ref at, 95));
                _data.BitLong(Whole(groups, ref at, 96));
                _data.Bit(Whole(groups, ref at, 282) != 0);
                T(Take(groups, ref at, 305));
                _data.Bit(Whole(groups, ref at, 281) != 0);
                T(Take(groups, ref at, 304));
            }

            _data.Bit(Whole(groups, ref at, 280) != 0);
        }

        private void WriteBlockRepresentationData()
        {
            DwgGroups data = _record.Of("AcDbBlockRepresentationData");
            _data.BitShort(data.Int(70, 1));
            H(5, data.Handle(340));
        }

        /// <summary>The version of a purge preventer, and the block it keeps: the owner of the dictionary it is in.</summary>
        private void WriteDynamicBlockPurgePreventer()
        {
            _data.BitShort(_record.Of("AcDbDynamicBlockPurgePreventer").Int(70, 1));
            H(5, _drawing.Find(_record.Owner)?.Owner ?? 0);
        }

        /// <summary>
        /// The graph: its two numbers, the nodes (index, flags, id, the expression as a hard owner and four numbers) and the
        /// edges (index, flags, count, the two nodes and five numbers). DXF has no counts: a node starts at a 91, an edge at
        /// the 92 that follows the last node.
        /// </summary>
        private void WriteEvaluationGraph()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            _data.BitLong(Whole(groups, ref at, 96));
            _data.BitLong(Whole(groups, ref at, 97));
            var nodes = new List<int[]>();
            var expressions = new List<ulong>();

            while (at < groups.Count && groups[at].Key == 91)
            {
                var node = new int[7];
                node[0] = Whole(groups, ref at, 91);
                node[1] = Whole(groups, ref at, 93);
                node[2] = Whole(groups, ref at, 95);
                expressions.Add(DwgRecord.Hex(Take(groups, ref at, 360)));

                for (int j = 3; j < 7; j++)
                {
                    node[j] = Whole(groups, ref at, 92);
                }

                nodes.Add(node);
            }

            _data.BitLong(nodes.Count);

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    _data.BitLong(nodes[i][j]);
                }

                H(3, expressions[i]);

                for (int j = 3; j < 7; j++)
                {
                    _data.BitLong(nodes[i][j]);
                }
            }

            var edges = new List<int[]>();

            while (at < groups.Count && groups[at].Key == 92)
            {
                var edge = new int[10];
                edge[0] = Whole(groups, ref at, 92);
                edge[1] = Whole(groups, ref at, 93);
                edge[2] = Whole(groups, ref at, 94);
                edge[3] = Whole(groups, ref at, 91);
                edge[4] = Whole(groups, ref at, 91);

                for (int j = 5; j < 10; j++)
                {
                    edge[j] = Whole(groups, ref at, 92);
                }

                edges.Add(edge);
            }

            _data.BitLong(edges.Count);

            foreach (int[] edge in edges)
            {
                foreach (int number in edge)
                {
                    _data.BitLong(number);
                }
            }
        }
    }
}
