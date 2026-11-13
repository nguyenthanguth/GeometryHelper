using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The objects a dynamic block keeps: the evaluation graph and the expressions it owns, and the representation data of
    /// its anonymous blocks. Each is written from the groups of its DXF in the order AutoCAD gives them, the layouts of
    /// <c>DwgObjectReader.DynamicBlocks</c>.
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        /// <summary>The groups of the record's data in order, the subclass markers left out.</summary>
        private List<KeyValuePair<int, string>> Sequence() => _record.Body.FindAll(group => group.Key != 100);

        private static int Whole(List<KeyValuePair<int, string>> groups, ref int at, int code, int otherwise = 0) =>
            DwgRecord.Int(Take(groups, ref at, code, otherwise.ToString()));

        private static double Real(List<KeyValuePair<int, string>> groups, ref int at, int code) =>
            DwgRecord.Double(Take(groups, ref at, code, "0"));

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

        private void WriteBlockLinearParameter()
        {
            List<KeyValuePair<int, string>> groups = Sequence();
            int at = 0;
            WriteBlockElement(groups, ref at);
            _data.Bit(Whole(groups, ref at, 280) != 0);
            _data.Bit(Whole(groups, ref at, 281) != 0);
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
            T(Take(groups, ref at, 305));
            T(Take(groups, ref at, 306));
            _data.BitDouble(Real(groups, ref at, 140));
            Take(groups, ref at, 307);
            _data.BitLong(Whole(groups, ref at, 96));
            _data.BitDouble(Real(groups, ref at, 141));
            _data.BitDouble(Real(groups, ref at, 142));
            _data.BitDouble(Real(groups, ref at, 143));
            int values = Whole(groups, ref at, 175);
            _data.BitShort(values);

            for (int i = 0; i < values; i++)
            {
                _data.BitDouble(Real(groups, ref at, 144));
            }
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
