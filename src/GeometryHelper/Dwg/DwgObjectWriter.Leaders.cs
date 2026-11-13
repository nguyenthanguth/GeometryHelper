using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The multileader and its style, the mirror of the reader's: a MULTILEADER's context first, its leaders before its
    /// content though DXF writes them after, then its own properties; each colour the one number of a CMC colour.
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        private void WriteMultiLeader()
        {
            var groups = new List<KeyValuePair<int, string>>(_record.Of("AcDbMLeader").All);
            int open = groups.FindIndex(g => g.Key == 300 && g.Value.Trim() == "CONTEXT_DATA{");
            int close = open < 0 ? -1 : groups.FindIndex(open, g => g.Key == 301 && g.Value.Trim() == "}");
            var head = new Cursor(groups.GetRange(0, open < 0 ? groups.Count : open));
            var context = new List<KeyValuePair<int, string>>();
            var leaders = new List<List<KeyValuePair<int, string>>>();

            if (open >= 0)
            {
                // The leaders of the context, each between 302 LEADER{ and 303 }, apart from the rest of it.
                int end = close < 0 ? groups.Count : close;

                for (int i = open + 1; i < end; i++)
                {
                    if (groups[i].Key == 302 && groups[i].Value.Trim() == "LEADER{")
                    {
                        int last = groups.FindIndex(i, g => g.Key == 303 && g.Value.Trim() == "}");
                        last = last < 0 || last > end ? end - 1 : last;
                        leaders.Add(groups.GetRange(i + 1, last - i - 1));
                        i = last;
                        continue;
                    }

                    context.Add(groups[i]);
                }
            }

            var own = new Cursor(
                close < 0 ? new List<KeyValuePair<int, string>>() : groups.GetRange(close + 1, groups.Count - close - 1)
            );

            if (R2010Plus)
            {
                _data.BitShort(head.Int(270, 2));
            }

            WriteMultiLeaderContext(new Cursor(context), leaders);
            H(5, own.Handle(340));
            _data.BitLong(own.Int(90));
            _data.BitShort(own.Int(170, 1));
            WriteRawColour(own.Int(91, unchecked((int)0xC0000000)));
            H(5, own.Handle(341));
            _data.BitLong(own.Int(171, -2));
            _data.Bit(own.Int(290, 1) != 0);
            _data.Bit(own.Int(291, 1) != 0);
            _data.BitDouble(own.Double(41));
            H(5, own.Handle(342));
            _data.BitDouble(own.Double(42));
            _data.BitShort(own.Int(172, 1));
            H(5, own.Handle(343));
            _data.BitShort(own.Int(173, 1));
            _data.BitShort(own.Int(95, 1));
            _data.BitShort(own.Int(174));
            _data.BitShort(own.Int(175));
            WriteRawColour(own.Int(92, unchecked((int)0xC0000000)));
            _data.Bit(own.Int(292) != 0);
            H(5, own.Handle(344));
            WriteRawColour(own.Int(93, unchecked((int)0xC0000000)));
            _data.Point3(own.At(10) ? own.Point(10) : new[] { 1.0, 1.0, 1.0 });
            _data.BitDouble(own.Double(43));
            _data.BitShort(own.Int(176));
            _data.Bit(own.Int(293) != 0);

            if (!R2010Plus)
            {
                var arrows = new List<KeyValuePair<int, ulong>>();

                while (own.At(94))
                {
                    int index = own.Int(94);
                    arrows.Add(new KeyValuePair<int, ulong>(index, own.Handle(345)));
                }

                _data.BitLong(arrows.Count);

                foreach (KeyValuePair<int, ulong> arrow in arrows)
                {
                    _data.Bit(arrow.Key != 0);
                    H(5, arrow.Value);
                }
            }

            var labels = new List<object[]>();

            while (own.At(330))
            {
                labels.Add(new object[] { own.Handle(330), own.Text(302), own.Int(177), own.Double(44) });
            }

            _data.BitLong(labels.Count);

            foreach (object[] label in labels)
            {
                H(4, (ulong)label[0]);
                T((string)label[1]);
                _data.BitShort((int)label[2]);
                _data.BitDouble((double)label[3]);
            }

            _data.Bit(own.Int(294) != 0);
            _data.BitShort(own.Int(178));
            _data.BitShort(own.Int(179, 1));
            _data.BitDouble(own.Double(45, 1.0));

            if (R2010Plus)
            {
                _data.BitShort(own.Int(271));
                _data.BitShort(own.Int(272, 9));
                _data.BitShort(own.Int(273, 9));
            }

            if (R2013Plus)
            {
                _data.Bit(own.Int(295) != 0);
            }
        }

        /// <summary>The context of a multileader: its leaders, its text or block content, its plane and its gaps.</summary>
        private void WriteMultiLeaderContext(Cursor context, List<List<KeyValuePair<int, string>>> leaders)
        {
            _data.BitLong(leaders.Count);

            foreach (List<KeyValuePair<int, string>> leader in leaders)
            {
                WriteLeaderRoot(leader);
            }

            _data.BitDouble(context.Double(40, 1.0));
            _data.Point3(context.Point(10));
            _data.BitDouble(context.Double(41));
            _data.BitDouble(context.Double(140));
            _data.BitDouble(context.Double(145));
            _data.BitShort(context.Int(174, 1));
            _data.BitShort(context.Int(175, 6));
            _data.BitShort(context.Int(176));
            _data.BitShort(context.Int(177));
            bool text = context.Int(290) != 0;
            _data.Bit(text);

            if (text)
            {
                T(context.Text(304));
                _data.Point3(context.At(11) ? context.Point(11) : new[] { 0.0, 0.0, 1.0 });
                H(5, context.Handle(340));
                _data.Point3(context.Point(12));
                _data.Point3(context.At(13) ? context.Point(13) : new[] { 1.0, 0.0, 0.0 });
                _data.BitDouble(context.Double(42));
                _data.BitDouble(context.Double(43));
                _data.BitDouble(context.Double(44));
                _data.BitDouble(context.Double(45, 1.0));
                _data.BitShort(context.Int(170, 1));
                WriteRawColour(context.Int(90, unchecked((int)0xC0000000)));
                _data.BitShort(context.Int(171, 1));
                _data.BitShort(context.Int(172, 1));
                WriteRawColour(context.Int(91, unchecked((int)0xC0000000)));
                _data.BitDouble(context.Double(141));
                _data.BitLong(context.Int(92));
                _data.Bit(context.Int(291) != 0);
                _data.Bit(context.Int(292) != 0);
                _data.BitShort(context.Int(173));
                _data.Bit(context.Int(293) != 0);
                _data.BitDouble(context.Double(142));
                _data.BitDouble(context.Double(143));
                _data.Bit(context.Int(294) != 0);
                var columns = new List<double>();

                while (context.At(144))
                {
                    columns.Add(context.Double(144));
                }

                _data.BitLong(columns.Count);

                foreach (double column in columns)
                {
                    _data.BitDouble(column);
                }

                _data.Bit(context.Int(295) != 0);
                _data.Bit(false);
                context.Int(296);
            }
            else
            {
                bool block = context.Int(296) != 0;
                _data.Bit(block);

                if (block)
                {
                    H(4, context.Handle(341));
                    _data.Point3(context.At(14) ? context.Point(14) : new[] { 0.0, 0.0, 1.0 });
                    _data.Point3(context.Point(15));
                    _data.Point3(context.At(16) ? context.Point(16) : new[] { 1.0, 1.0, 1.0 });
                    _data.BitDouble(context.Double(46));
                    WriteRawColour(context.Int(93, unchecked((int)0xC0000000)));

                    for (int i = 0; i < 16; i++)
                    {
                        _data.BitDouble(context.Double(47, i % 5 == 0 ? 1.0 : 0.0));
                    }
                }
            }

            _data.Point3(context.Point(110));
            _data.Point3(context.At(111) ? context.Point(111) : new[] { 1.0, 0.0, 0.0 });
            _data.Point3(context.At(112) ? context.Point(112) : new[] { 0.0, 1.0, 0.0 });
            _data.Bit(context.Int(297) != 0);

            if (R2010Plus)
            {
                // DXF writes the bottom gap (272) before the top (273); the file holds the top first.
                int bottom = context.Int(272, 9);
                int top = context.Int(273, 9);
                _data.BitShort(top);
                _data.BitShort(bottom);
            }
        }

        /// <summary>One leader of a multileader: its landing, breaks and lines.</summary>
        private void WriteLeaderRoot(List<KeyValuePair<int, string>> groups)
        {
            var lines = new List<List<KeyValuePair<int, string>>>();
            var rest = new List<KeyValuePair<int, string>>();

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Key == 304 && groups[i].Value.Trim() == "LEADER_LINE{")
                {
                    int last = groups.FindIndex(i, g => g.Key == 305 && g.Value.Trim() == "}");
                    last = last < 0 ? groups.Count : last;
                    lines.Add(groups.GetRange(i + 1, last - i - 1));
                    i = last;
                    continue;
                }

                rest.Add(groups[i]);
            }

            var leader = new Cursor(rest);
            _data.Bit(leader.Int(290, 1) != 0);
            _data.Bit(leader.Int(291, 1) != 0);
            _data.Point3(leader.Point(10));
            _data.Point3(leader.At(11) ? leader.Point(11) : new[] { 1.0, 0.0, 0.0 });
            var breaks = new List<double[]>();

            while (leader.At(12))
            {
                breaks.Add(leader.Point(12));
                breaks.Add(leader.Point(13));
            }

            _data.BitLong(breaks.Count / 2);

            foreach (double[] point in breaks)
            {
                _data.Point3(point);
            }

            _data.BitLong(leader.Int(90));
            _data.BitDouble(leader.Double(40));
            _data.BitLong(lines.Count);

            foreach (List<KeyValuePair<int, string>> line in lines)
            {
                WriteLeaderLine(new Cursor(line));
            }

            if (R2010Plus)
            {
                _data.BitShort(leader.Int(271));
            }
        }

        /// <summary>One line of a leader of a multileader: its points, breaks, index and, from R2010, what it overrides.</summary>
        private void WriteLeaderLine(Cursor line)
        {
            var points = new List<double[]>();

            while (line.At(10))
            {
                points.Add(line.Point(10));
            }

            _data.BitLong(points.Count);

            foreach (double[] point in points)
            {
                _data.Point3(point);
            }

            var breaks = new List<KeyValuePair<int, List<double[]>>>();

            while (line.At(90))
            {
                int index = line.Int(90);
                var pairs = new List<double[]>();

                while (line.At(11))
                {
                    pairs.Add(line.Point(11));
                    pairs.Add(line.Point(12));
                }

                breaks.Add(new KeyValuePair<int, List<double[]>>(index, pairs));
            }

            _data.BitLong(breaks.Count);

            foreach (KeyValuePair<int, List<double[]>> found in breaks)
            {
                _data.BitLong(found.Key);
                _data.BitLong(found.Value.Count / 2);

                foreach (double[] point in found.Value)
                {
                    _data.Point3(point);
                }
            }

            _data.BitLong(line.Int(91));

            if (R2010Plus)
            {
                _data.BitShort(line.Int(170, 1));
                WriteRawColour(line.Int(92, unchecked((int)0xC1000000)));
                H(5, line.Handle(340));
                _data.BitLong(line.Int(171, -2));
                _data.BitDouble(line.Double(40));
                H(5, line.Handle(341));
                _data.BitLong(line.Int(93));
            }
        }

        private void WriteMultiLeaderStyle()
        {
            var style = new Cursor(_record.Of("AcDbMLeaderStyle"));

            if (R2010Plus)
            {
                _data.BitShort(style.Int(179, 2));
            }

            _data.BitShort(style.Int(170, 1));
            _data.BitShort(style.Int(171, 1));
            _data.BitShort(style.Int(172));
            _data.BitLong(style.Int(90, 2));
            _data.BitDouble(style.Double(40));
            _data.BitDouble(style.Double(41));
            _data.BitShort(style.Int(173, 1));
            WriteRawColour(style.Int(91, unchecked((int)0xC0000000)));
            H(5, style.Handle(340));
            _data.BitLong(style.Int(92, -2));
            _data.Bit(style.Int(290, 1) != 0);
            _data.BitDouble(style.Double(42));
            _data.Bit(style.Int(291, 1) != 0);
            _data.BitDouble(style.Double(43));
            T(style.Text(3));
            H(5, style.Handle(341));
            _data.BitDouble(style.Double(44));
            T(style.Text(300));
            H(5, style.Handle(342));
            _data.BitShort(style.Int(174, 1));
            _data.BitShort(style.Int(178, 6));
            _data.BitShort(style.Int(175, 1));
            _data.BitShort(style.Int(176));
            WriteRawColour(style.Int(93, unchecked((int)0xC0000000)));
            _data.BitDouble(style.Double(45));
            _data.Bit(style.Int(292) != 0);
            _data.Bit(style.Int(297) != 0);
            _data.BitDouble(style.Double(46));
            H(5, style.Handle(343));
            WriteRawColour(style.Int(94, unchecked((int)0xC0000000)));
            _data.Point3(new[] { style.Double(47, 1.0), style.Double(49, 1.0), style.Double(140, 1.0) });
            _data.Bit(style.Int(293, 1) != 0);
            _data.BitDouble(style.Double(141));
            _data.Bit(style.Int(294, 1) != 0);
            _data.BitShort(style.Int(177));
            _data.BitDouble(style.Double(142, 1.0));

            // DXF writes whether a property changed (295) before whether the style is annotative (296); the file the other
            // way round.
            int changed = style.Int(295);
            _data.Bit(style.Int(296) != 0);
            _data.Bit(changed != 0);
            _data.BitDouble(style.Double(143));

            if (R2010Plus)
            {
                _data.BitShort(style.Int(271));
                _data.BitShort(style.Int(272, 9));
                _data.BitShort(style.Int(273, 9));
            }

            if (R2013Plus)
            {
                _data.Bit(style.Int(298) != 0);
            }
        }

        /// <summary>
        /// A CMC colour given as its one number (<see cref="DwgColour.Raw"/>): before R2004 the index, from R2004 a bit short
        /// of 0, the number and a byte of 0, no names following.
        /// </summary>
        private void WriteRawColour(int raw)
        {
            if (!R2004Plus)
            {
                int method = (raw >> 24) & 0xFF;
                _data.BitShort(
                    method == 0xC0 ? 256
                    : method == 0xC1 ? 0
                    : raw & 0xFF
                );
                return;
            }

            _data.BitShort(0);
            _data.BitLong(raw);
            _data.RawChar(0);
        }
    }
}
