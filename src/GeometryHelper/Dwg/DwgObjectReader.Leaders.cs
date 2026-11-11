using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The multileader and its style. A MULTILEADER keeps its context first (the leaders and the content as drawn) and its
    /// own properties after; DXF writes the context between <c>CONTEXT_DATA{</c> and <c>}</c>, its leaders after its content,
    /// and gives each colour as the one number of a CMC colour (<see cref="DwgColour.Raw"/>).
    /// </content>
    internal sealed partial class DwgObjectReader
    {
        private void ReadMultiLeader()
        {
            int version = R2010Plus ? _data.BitShort() : 0;
            _object.Add(100, "AcDbMLeader");

            if (R2010Plus)
            {
                _object.Add(270, version);
            }

            _object.Add(300, "CONTEXT_DATA{");
            ReadMultiLeaderContext();
            _object.Add(301, "}");
            _object.Add(340, H());
            _object.Add(90, _data.BitLong());
            _object.Add(170, _data.BitShort());
            _object.Add(91, ReadColour().Raw);
            AddHandle(341, H());
            _object.Add(171, _data.BitLong());
            _object.Add(290, _data.Bit() ? 1 : 0);
            _object.Add(291, _data.Bit() ? 1 : 0);
            _object.Add(41, _data.BitDouble());
            AddHandle(342, H());
            _object.Add(42, _data.BitDouble());
            _object.Add(172, _data.BitShort());
            AddHandle(343, H());
            _object.Add(173, _data.BitShort());
            _object.Add(95, _data.BitShort());
            _object.Add(174, _data.BitShort());
            _object.Add(175, _data.BitShort());
            _object.Add(92, ReadColour().Raw);
            _object.Add(292, _data.Bit() ? 1 : 0);
            AddHandle(344, H());
            _object.Add(93, ReadColour().Raw);
            _object.AddPoint(10, _data.Point3());
            _object.Add(43, _data.BitDouble());
            _object.Add(176, _data.BitShort());
            _object.Add(293, _data.Bit() ? 1 : 0);

            if (!R2010Plus)
            {
                int arrows = Count(_data.BitLong());

                for (int i = 0; i < arrows; i++)
                {
                    _object.Add(94, _data.Bit() ? 1 : 0);
                    _object.Add(345, H());
                }
            }

            int labels = Count(_data.BitLong());

            for (int i = 0; i < labels; i++)
            {
                _object.Add(330, H());
                _object.Add(302, T());
                _object.Add(177, _data.BitShort());
                _object.Add(44, _data.BitDouble());
            }

            _object.Add(294, _data.Bit() ? 1 : 0);
            _object.Add(178, _data.BitShort());
            _object.Add(179, _data.BitShort());
            _object.Add(45, _data.BitDouble());

            if (R2010Plus)
            {
                _object.Add(271, _data.BitShort());
                _object.Add(272, _data.BitShort());
                _object.Add(273, _data.BitShort());
            }

            if (R2013Plus)
            {
                _object.Add(295, _data.Bit() ? 1 : 0);
            }
        }

        /// <summary>The context of a multileader: its leaders, read first and written last, and its text or block content.</summary>
        private void ReadMultiLeaderContext()
        {
            var leaders = new List<DwgGroup>();
            int roots = Count(_data.BitLong());

            for (int i = 0; i < roots; i++)
            {
                ReadLeaderRoot(leaders);
            }

            _object.Add(40, _data.BitDouble());
            _object.AddPoint(10, _data.Point3());
            _object.Add(41, _data.BitDouble());
            _object.Add(140, _data.BitDouble());
            _object.Add(145, _data.BitDouble());
            _object.Add(174, _data.BitShort());
            _object.Add(175, _data.BitShort());
            _object.Add(176, _data.BitShort());
            _object.Add(177, _data.BitShort());
            bool text = _data.Bit();
            _object.Add(290, text ? 1 : 0);

            if (text)
            {
                _object.Add(304, T());
                _object.AddPoint(11, _data.Point3());
                _object.Add(340, H());
                _object.AddPoint(12, _data.Point3());
                _object.AddPoint(13, _data.Point3());
                _object.Add(42, _data.BitDouble());
                _object.Add(43, _data.BitDouble());
                _object.Add(44, _data.BitDouble());
                _object.Add(45, _data.BitDouble());
                _object.Add(170, _data.BitShort());
                _object.Add(90, ReadColour().Raw);
                _object.Add(171, _data.BitShort());
                _object.Add(172, _data.BitShort());
                _object.Add(91, ReadColour().Raw);
                _object.Add(141, _data.BitDouble());
                _object.Add(92, _data.BitLong());
                _object.Add(291, _data.Bit() ? 1 : 0);
                _object.Add(292, _data.Bit() ? 1 : 0);
                _object.Add(173, _data.BitShort());
                _object.Add(293, _data.Bit() ? 1 : 0);
                _object.Add(142, _data.BitDouble());
                _object.Add(143, _data.BitDouble());
                _object.Add(294, _data.Bit() ? 1 : 0);
                int columns = Count(_data.BitLong());

                for (int i = 0; i < columns; i++)
                {
                    _object.Add(144, _data.BitDouble());
                }

                _object.Add(295, _data.Bit() ? 1 : 0);
                _data.Bit();
                _object.Add(296, 0);
            }
            else
            {
                bool block = _data.Bit();
                _object.Add(296, block ? 1 : 0);

                if (block)
                {
                    _object.Add(341, H());
                    _object.AddPoint(14, _data.Point3());
                    _object.AddPoint(15, _data.Point3());
                    _object.AddPoint(16, _data.Point3());
                    _object.Add(46, _data.BitDouble());
                    _object.Add(93, ReadColour().Raw);

                    for (int i = 0; i < 16; i++)
                    {
                        _object.Add(47, _data.BitDouble());
                    }
                }
            }

            _object.AddPoint(110, _data.Point3());
            _object.AddPoint(111, _data.Point3());
            _object.AddPoint(112, _data.Point3());
            _object.Add(297, _data.Bit() ? 1 : 0);
            _object.Body.AddRange(leaders);

            if (R2010Plus)
            {
                int top = _data.BitShort();
                int bottom = _data.BitShort();
                _object.Add(272, bottom);
                _object.Add(273, top);
            }
        }

        /// <summary>One leader of a multileader, between <c>LEADER{</c> and <c>}</c>: its landing, breaks and lines.</summary>
        private void ReadLeaderRoot(List<DwgGroup> groups)
        {
            groups.Add(new DwgGroup(302, "LEADER{"));
            groups.Add(new DwgGroup(290, _data.Bit() ? 1 : 0));
            groups.Add(new DwgGroup(291, _data.Bit() ? 1 : 0));
            AddPoint(groups, 10, _data.Point3());
            AddPoint(groups, 11, _data.Point3());
            int breaks = Count(_data.BitLong());

            for (int i = 0; i < breaks; i++)
            {
                AddPoint(groups, 12, _data.Point3());
                AddPoint(groups, 13, _data.Point3());
            }

            groups.Add(new DwgGroup(90, _data.BitLong()));
            groups.Add(new DwgGroup(40, _data.BitDouble()));
            int lines = Count(_data.BitLong());

            for (int i = 0; i < lines; i++)
            {
                ReadLeaderLine(groups);
            }

            if (R2010Plus)
            {
                groups.Add(new DwgGroup(271, _data.BitShort()));
            }

            groups.Add(new DwgGroup(303, "}"));
        }

        /// <summary>One line of a leader of a multileader, between <c>LEADER_LINE{</c> and <c>}</c>.</summary>
        private void ReadLeaderLine(List<DwgGroup> groups)
        {
            groups.Add(new DwgGroup(304, "LEADER_LINE{"));
            int points = Count(_data.BitLong());

            for (int i = 0; i < points; i++)
            {
                AddPoint(groups, 10, _data.Point3());
            }

            int breaks = Count(_data.BitLong());

            for (int i = 0; i < breaks; i++)
            {
                groups.Add(new DwgGroup(90, _data.BitLong()));
                int pairs = Count(_data.BitLong());

                for (int j = 0; j < pairs; j++)
                {
                    AddPoint(groups, 11, _data.Point3());
                    AddPoint(groups, 12, _data.Point3());
                }
            }

            groups.Add(new DwgGroup(91, _data.BitLong()));

            if (R2010Plus)
            {
                // The properties a line may take over from its multileader, which DXF writes only where one is.
                int type = _data.BitShort();
                int colour = ReadColour().Raw;
                ulong linetype = H();
                int weight = _data.BitLong();
                double arrowSize = _data.BitDouble();
                ulong arrow = H();
                int overrides = _data.BitLong();

                if (overrides != 0)
                {
                    groups.Add(new DwgGroup(170, type));
                    groups.Add(new DwgGroup(92, colour));
                    groups.Add(new DwgGroup(340, linetype));
                    groups.Add(new DwgGroup(171, weight));
                    groups.Add(new DwgGroup(40, arrowSize));
                    groups.Add(new DwgGroup(341, arrow));
                    groups.Add(new DwgGroup(93, overrides));
                }
            }

            groups.Add(new DwgGroup(305, "}"));
        }

        private static void AddPoint(List<DwgGroup> groups, int code, double[] point)
        {
            for (int i = 0; i < point.Length; i++)
            {
                groups.Add(new DwgGroup(code + (10 * i), point[i]));
            }
        }

        /// <summary>Adds a handle where it is not 0, which DXF leaves out.</summary>
        private void AddHandle(int code, ulong handle)
        {
            if (handle != 0)
            {
                _object.Add(code, handle);
            }
        }

        private void ReadMultiLeaderStyle()
        {
            int version = R2010Plus ? _data.BitShort() : 0;
            _object.Add(100, "AcDbMLeaderStyle");

            if (R2010Plus)
            {
                _object.Add(179, version);
            }

            _object.Add(170, _data.BitShort());
            _object.Add(171, _data.BitShort());
            _object.Add(172, _data.BitShort());
            _object.Add(90, _data.BitLong());
            _object.Add(40, _data.BitDouble());
            _object.Add(41, _data.BitDouble());
            _object.Add(173, _data.BitShort());
            _object.Add(91, ReadColour().Raw);
            AddHandle(340, H());
            _object.Add(92, _data.BitLong());
            _object.Add(290, _data.Bit() ? 1 : 0);
            _object.Add(42, _data.BitDouble());
            _object.Add(291, _data.Bit() ? 1 : 0);
            _object.Add(43, _data.BitDouble());
            _object.Add(3, T());
            AddHandle(341, H());
            _object.Add(44, _data.BitDouble());
            _object.Add(300, T());
            AddHandle(342, H());
            _object.Add(174, _data.BitShort());
            _object.Add(178, _data.BitShort());
            _object.Add(175, _data.BitShort());
            _object.Add(176, _data.BitShort());
            _object.Add(93, ReadColour().Raw);
            _object.Add(45, _data.BitDouble());
            _object.Add(292, _data.Bit() ? 1 : 0);
            _object.Add(297, _data.Bit() ? 1 : 0);
            _object.Add(46, _data.BitDouble());
            AddHandle(343, H());
            _object.Add(94, ReadColour().Raw);
            double[] scale = _data.Point3();
            _object.Add(47, scale[0]);
            _object.Add(49, scale[1]);
            _object.Add(140, scale[2]);
            _object.Add(293, _data.Bit() ? 1 : 0);
            _object.Add(141, _data.BitDouble());
            _object.Add(294, _data.Bit() ? 1 : 0);
            _object.Add(177, _data.BitShort());
            _object.Add(142, _data.BitDouble());

            // The file holds whether the style is annotative before whether a property changed; DXF writes them the other
            // way round (the Standard style of AutoCAD 2021's R2018 template has the second set and writes 295 1).
            bool annotative = _data.Bit();
            _object.Add(295, _data.Bit() ? 1 : 0);
            _object.Add(296, annotative ? 1 : 0);
            _object.Add(143, _data.BitDouble());

            if (R2010Plus)
            {
                _object.Add(271, _data.BitShort());
                int bottom = _data.BitShort();
                int top = _data.BitShort();
                _object.Add(272, bottom);
                _object.Add(273, top);
            }

            if (R2013Plus)
            {
                _object.Add(298, _data.Bit() ? 1 : 0);
            }
        }
    }
}
