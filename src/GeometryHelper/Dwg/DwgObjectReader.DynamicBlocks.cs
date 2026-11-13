using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The objects a dynamic block keeps: the evaluation graph and the expressions it owns (a linear parameter, a stretch
    /// action, two linear grips and the components of their locations), and the representation data of its anonymous blocks.
    /// The layouts follow ACadSharp (MIT) and are checked against AutoCAD 2021's own DXF of the same drawings.
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

        private void ReadBlockGripLocationComponent()
        {
            ReadEvalExpression();
            _object.Add(100, "AcDbBlockGripExpr");
            ReadEvalConnection(91, 300);
        }

        /// <summary>
        /// A parameter of two points (AcDbBlock2PtParameter): its flags, both points, the four grips it names, the connections
        /// of its four displacements (each under its own codes) and where the base is.
        /// </summary>
        private void ReadBlock2PtParameter()
        {
            ReadBlockElement();
            int properties = _data.Bit() ? 1 : 0;
            int chain = _data.Bit() ? 1 : 0;
            _object.Add(100, "AcDbBlockParameter");
            _object.Add(280, properties);
            _object.Add(281, chain);
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
            _object.Add(307, new byte[0]);
            _object.Add(96, _data.BitLong());
            _object.Add(141, _data.BitDouble());
            _object.Add(142, _data.BitDouble());
            _object.Add(143, _data.BitDouble());
            int count = Count(_data.BitShort());
            _object.Add(175, count);

            for (int i = 0; i < count; i++)
            {
                _object.Add(144, _data.BitDouble());
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
