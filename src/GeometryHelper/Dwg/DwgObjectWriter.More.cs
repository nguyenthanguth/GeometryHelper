using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>The entities whose DXF groups come in runs that repeat: viewports, hatches, leaders, multilines; and tolerances.</content>
    internal sealed partial class DwgObjectWriter
    {
        private void WriteLeader()
        {
            DwgGroups leader = _record.Of("AcDbLeader");
            List<double[]> points = Points(leader, 10);
            int annotation = leader.Int(73, 3);
            _data.Bit(false);
            _data.BitShort(annotation);
            _data.BitShort(leader.Int(72));
            _data.BitLong(points.Count);

            foreach (double[] point in points)
            {
                _data.Point3(point);
            }

            // The projected end point, which AutoCAD 2021 keeps as (0, 0, 0).
            _data.Point3(new double[3]);
            _data.Point3(leader.Point(210, 0, 0, 1));
            _data.Point3(leader.Point(211, 1));
            _data.Point3(leader.Point(212));
            _data.Point3(leader.Point(213));

            if (!R2010Plus)
            {
                _data.BitDouble(leader.Double(40));
                _data.BitDouble(leader.Double(41));
            }

            _data.Bit(leader.Int(74) != 0);
            _data.Bit(leader.Int(71, 1) != 0);
            _data.BitShort(0);
            _data.Bit(false);
            _data.Bit(false);
            H(5, leader.Handle(340));
            H(5, _drawing.HandleOf("DIMSTYLE", leader.Text(3, "Standard")));
        }

        private void WriteTolerance()
        {
            DwgGroups tolerance = _record.Of("AcDbFcf");
            _data.Point3(tolerance.Point(10));
            _data.Point3(tolerance.Point(11, 1));
            _data.Point3(tolerance.Point(210, 0, 0, 1));
            T(tolerance.Text(1));
            H(5, _drawing.HandleOf("DIMSTYLE", tolerance.Text(3, "Standard")));
        }

        private void WriteMLine()
        {
            DwgGroups line = _record.Of("AcDbMline");
            var cursor = new Cursor(line);
            int count = line.Int(72);
            int lines = line.Int(73);
            _data.BitDouble(line.Double(40, 1.0));
            _data.RawChar(line.Int(70));
            _data.Point3(line.Point(10));
            _data.Point3(line.Point(210, 0, 0, 1));
            _data.BitShort(line.Int(71));
            _data.RawChar(lines);
            _data.BitShort(count);

            for (int i = 0; i < count; i++)
            {
                cursor.SkipTo(11);
                _data.Point3(cursor.Point(11));
                _data.Point3(cursor.Point(12));
                _data.Point3(cursor.Point(13));

                for (int j = 0; j < lines; j++)
                {
                    foreach (int[] codes in new[] { new[] { 74, 41 }, new[] { 75, 42 } })
                    {
                        int parameters = cursor.Int(codes[0]);
                        _data.BitShort(parameters);

                        for (int k = 0; k < parameters; k++)
                        {
                            _data.BitDouble(cursor.Double(codes[1]));
                        }
                    }
                }
            }

            ulong style = line.Handle(340);
            H(5, style != 0 ? style : EntryOfStyle(line.Text(2, "Standard")));
        }

        /// <summary>An IMAGE or a WIPEOUT: its corner, its pixel vectors, its size, its display, its clip boundary, kept open.</summary>
        private void WriteRasterImage(string subclass)
        {
            DwgGroups image = _record.Of(subclass);
            _data.BitLong(image.Int(90));
            _data.Point3(image.Point(10));
            _data.Point3(image.Point(11, 1));
            _data.Point3(image.Point(12, 0, 1));
            _data.RawPoint2(image.Point(13, 1, 1));
            _data.BitShort(image.Int(70, 7));
            _data.Bit(image.Int(280) != 0);
            _data.RawChar(image.Int(281, 50));
            _data.RawChar(image.Int(282, 50));
            _data.RawChar(image.Int(283));

            if (R2010Plus)
            {
                _data.Bit(image.Int(290) != 0);
            }

            int clipType = image.Int(71, 1);
            List<double[]> vertices = Points(image, 14);
            _data.BitShort(clipType);

            if (clipType == 1)
            {
                _data.RawPoint2(vertices.Count > 0 ? vertices[0] : new double[3]);
                _data.RawPoint2(vertices.Count > 1 ? vertices[1] : new double[3]);
            }
            else if (clipType == 2)
            {
                // DXF closes the polygon with its first vertex again; the file keeps it open.
                int count = vertices.Count;

                if (count > 1 && vertices[0][0] == vertices[count - 1][0] && vertices[0][1] == vertices[count - 1][1])
                {
                    count--;
                }

                _data.BitLong(count);

                for (int i = 0; i < count; i++)
                {
                    _data.RawPoint2(vertices[i]);
                }
            }

            H(5, image.Handle(340));
            H(3, image.Handle(360));
        }

        private void WriteImageDefinition()
        {
            DwgGroups definition = _record.Of("AcDbRasterImageDef");
            _data.BitLong(definition.Int(90));
            _data.RawPoint2(definition.Point(10, 1, 1));
            T(definition.Text(1));
            _data.Bit(definition.Int(280, 1) != 0);
            _data.RawChar(definition.Int(281));
            _data.RawPoint2(definition.Point(11, 1, 1));
        }

        /// <summary>The MLINESTYLE named <paramref name="name"/>: the entry of the dictionary ACAD_MLINESTYLE.</summary>
        private ulong EntryOfStyle(string name)
        {
            DwgRecord style = _drawing.Objects.Find(record =>
                record.Type == "MLINESTYLE"
                && string.Equals(
                    record.Of("AcDbMlineStyle").Text(2).Trim(),
                    name.Trim(),
                    StringComparison.OrdinalIgnoreCase
                )
            );
            return style?.Handle ?? 0;
        }

        private void WriteViewport()
        {
            DwgGroups viewport = _record.Of("AcDbViewport");
            List<string> frozen = viewport.Each(R2004Plus ? 331 : 341);

            if (frozen.Count == 0)
            {
                frozen = viewport.Each(R2004Plus ? 341 : 331);
            }

            List<ulong> layers = frozen.ConvertAll(DwgRecord.Hex).FindAll(handle => _written.Contains(handle));
            _data.Point3(viewport.Point(10));
            _data.BitDouble(viewport.Double(40));
            _data.BitDouble(viewport.Double(41));
            _data.Point3(viewport.Point(17));
            _data.Point3(viewport.Point(16, 0, 0, 1));
            _data.BitDouble(Radians(viewport.Double(51)));
            _data.BitDouble(viewport.Double(45));
            _data.BitDouble(viewport.Double(42, 50.0));
            _data.BitDouble(viewport.Double(43));
            _data.BitDouble(viewport.Double(44));
            _data.BitDouble(Radians(viewport.Double(50)));
            _data.RawPoint2(viewport.Point(12));
            _data.RawPoint2(viewport.Point(13));
            _data.RawPoint2(viewport.Point(14, 10, 10));
            _data.RawPoint2(viewport.Point(15, 10, 10));
            _data.BitShort(viewport.Int(72, 1000));

            if (R2007Plus)
            {
                _data.BitShort(viewport.Int(61, 5));
            }

            _data.BitLong(layers.Count);
            _data.BitLong(viewport.Int(90));
            T(viewport.Text(1));
            _data.RawChar(viewport.Int(281));
            _data.Bit(viewport.Int(74) != 0);
            _data.Bit(viewport.Int(71) != 0);
            _data.Point3(viewport.Point(110));
            _data.Point3(viewport.Point(111, 1));
            _data.Point3(viewport.Point(112, 0, 1));
            _data.BitDouble(viewport.Double(146));
            _data.BitShort(viewport.Int(79));

            if (R2004Plus)
            {
                _data.BitShort(viewport.Int(170));
            }

            if (R2007Plus)
            {
                _data.Bit(viewport.Int(292, 1) != 0);
                _data.RawChar(viewport.Int(282, 1));
                _data.BitDouble(viewport.Double(141));
                _data.BitDouble(viewport.Double(142));
                WriteColour(viewport.Int(63, 250), viewport.Has(421) ? viewport.Int(421) : -1);
            }

            foreach (ulong layer in layers)
            {
                H(4, layer);
            }

            H(5, viewport.Handle(340));

            if (!R2004Plus)
            {
                H(5, 0);
            }

            H(5, viewport.Handle(345));
            H(5, viewport.Handle(346));

            if (R2007Plus)
            {
                H(4, viewport.Handle(332));
                H(5, viewport.Handle(348));
                H(4, viewport.Handle(333));
                H(3, viewport.Handle(361));
            }
        }

        /// <summary>The groups of a run read in order: each value taken where the next group has its code, a default where not.</summary>
        private sealed class Cursor
        {
            private readonly List<KeyValuePair<int, string>> _groups;
            private int _at;

            public Cursor(DwgGroups groups)
            {
                _groups = new List<KeyValuePair<int, string>>(groups.All);
            }

            public bool At(int code) => _at < _groups.Count && _groups[_at].Key == code;

            public string Text(int code, string otherwise = "")
            {
                return At(code) ? _groups[_at++].Value : otherwise;
            }

            public int Int(int code, int otherwise = 0) => At(code) ? DwgRecord.Int(_groups[_at++].Value) : otherwise;

            public double Double(int code, double otherwise = 0.0) =>
                At(code) ? DwgRecord.Double(_groups[_at++].Value) : otherwise;

            /// <summary>A point of the code, the code plus 10 and, where it follows, plus 20.</summary>
            public double[] Point(int code)
            {
                double x = Double(code);
                double y = Double(code + 10);
                return new[] { x, y, Double(code + 20) };
            }

            /// <summary>Skips groups up to the next of <paramref name="code"/>, before any of <paramref name="stops"/>.</summary>
            public void SkipTo(int code, params int[] stops)
            {
                while (_at < _groups.Count && _groups[_at].Key != code && Array.IndexOf(stops, _groups[_at].Key) < 0)
                {
                    _at++;
                }
            }
        }

        private void WriteHatch()
        {
            var hatch = new Cursor(_record.Of("AcDbHatch"));
            DwgGroups all = _record.Of("AcDbHatch");

            if (R2004Plus)
            {
                _data.BitLong(all.Int(450));
                _data.BitLong(all.Int(451));
                _data.BitDouble(all.Double(460));
                _data.BitDouble(all.Double(461));
                _data.BitLong(all.Int(452));
                _data.BitDouble(all.Double(462));
                var gradient = new Cursor(all);
                gradient.SkipTo(453);
                int colours = gradient.Int(453);
                _data.BitLong(colours);

                for (int i = 0; i < colours; i++)
                {
                    gradient.SkipTo(463);
                    _data.BitDouble(gradient.Double(463));
                    int index = gradient.Int(63, 256);
                    WriteColour(index, gradient.At(421) ? gradient.Int(421) : -1);
                }

                T(all.Text(470));
            }

            double[] elevation = hatch.Point(10);
            double[] normal = hatch.Point(210);
            _data.BitDouble(elevation[2]);
            _data.Point3(normal[0] == 0.0 && normal[1] == 0.0 && normal[2] == 0.0 ? ZAxis : normal);
            T(hatch.Text(2));
            bool solid = hatch.Int(70) != 0;
            _data.Bit(solid);
            _data.Bit(hatch.Int(71) != 0);
            int paths = hatch.Int(91);
            _data.BitLong(paths);
            bool derived = false;
            var sources = new List<List<ulong>>();

            for (int i = 0; i < paths; i++)
            {
                hatch.SkipTo(92);
                int flags = hatch.Int(92);
                derived |= (flags & 4) != 0;
                _data.BitLong(flags);

                if ((flags & 2) != 0)
                {
                    bool bulges = hatch.Int(72) != 0;
                    bool closed = hatch.Int(73) != 0;
                    int count = hatch.Int(93);
                    _data.Bit(bulges);
                    _data.Bit(closed);
                    _data.BitLong(count);

                    for (int j = 0; j < count; j++)
                    {
                        double[] point = hatch.Point(10);
                        _data.RawPoint2(point);

                        if (bulges)
                        {
                            _data.BitDouble(hatch.Double(42));
                        }
                    }
                }
                else
                {
                    int edges = hatch.Int(93);
                    _data.BitLong(edges);

                    for (int j = 0; j < edges; j++)
                    {
                        WriteHatchEdge(hatch);
                    }
                }

                hatch.SkipTo(97, 92, 75);
                int count2 = hatch.Int(97);
                var path = new List<ulong>();

                for (int j = 0; j < count2; j++)
                {
                    path.Add(DwgRecord.Hex(hatch.Text(330, "0")));
                }

                path = path.FindAll(handle => _written.Contains(handle));
                _data.BitLong(path.Count);
                sources.Add(path);
            }

            hatch.SkipTo(75);
            _data.BitShort(hatch.Int(75));
            _data.BitShort(hatch.Int(76, 1));

            if (!solid)
            {
                _data.BitDouble(Radians(hatch.Double(52)));
                _data.BitDouble(hatch.Double(41, 1.0));
                _data.Bit(hatch.Int(77) != 0);
                int lines = hatch.Int(78);
                _data.BitShort(lines);

                for (int i = 0; i < lines; i++)
                {
                    _data.BitDouble(Radians(hatch.Double(53)));
                    _data.BitDouble(hatch.Double(43));
                    _data.BitDouble(hatch.Double(44));
                    _data.BitDouble(hatch.Double(45));
                    _data.BitDouble(hatch.Double(46));
                    int dashes = hatch.Int(79);
                    _data.BitShort(dashes);

                    for (int j = 0; j < dashes; j++)
                    {
                        _data.BitDouble(hatch.Double(49));
                    }
                }
            }

            if (derived)
            {
                hatch.SkipTo(47, 98);
                _data.BitDouble(hatch.Double(47));
            }

            hatch.SkipTo(98);
            int seeds = hatch.Int(98);
            _data.BitLong(seeds);

            for (int i = 0; i < seeds; i++)
            {
                _data.RawPoint2(hatch.Point(10));
            }

            foreach (List<ulong> path in sources)
            {
                foreach (ulong source in path)
                {
                    H(4, source);
                }
            }
        }

        private void WriteHatchEdge(Cursor hatch)
        {
            int kind = hatch.Int(72);
            _data.RawChar(kind);

            switch (kind)
            {
                case 1:
                    _data.RawPoint2(hatch.Point(10));
                    _data.RawPoint2(hatch.Point(11));
                    break;
                case 2:
                    _data.RawPoint2(hatch.Point(10));
                    _data.BitDouble(hatch.Double(40));
                    _data.BitDouble(Radians(hatch.Double(50)));
                    _data.BitDouble(Radians(hatch.Double(51)));
                    _data.Bit(hatch.Int(73, 1) != 0);
                    break;
                case 3:
                    _data.RawPoint2(hatch.Point(10));
                    _data.RawPoint2(hatch.Point(11));
                    _data.BitDouble(hatch.Double(40));
                    _data.BitDouble(Radians(hatch.Double(50)));
                    _data.BitDouble(Radians(hatch.Double(51)));
                    _data.Bit(hatch.Int(73, 1) != 0);
                    break;
                case 4:
                    _data.BitLong(hatch.Int(94, 3));
                    bool rational = hatch.Int(73) != 0;
                    _data.Bit(rational);
                    _data.Bit(hatch.Int(74) != 0);
                    int knots = hatch.Int(95);
                    int controls = hatch.Int(96);
                    _data.BitLong(knots);
                    _data.BitLong(controls);

                    for (int i = 0; i < knots; i++)
                    {
                        _data.BitDouble(hatch.Double(40));
                    }

                    for (int i = 0; i < controls; i++)
                    {
                        _data.RawPoint2(hatch.Point(10));

                        if (rational)
                        {
                            _data.BitDouble(hatch.Double(42, 1.0));
                        }
                    }

                    if (R2010Plus)
                    {
                        int fits = hatch.Int(97);
                        _data.BitLong(fits);

                        for (int i = 0; i < fits; i++)
                        {
                            _data.RawPoint2(hatch.Point(11));
                        }

                        if (fits > 0)
                        {
                            _data.RawPoint2(hatch.Point(12));
                            _data.RawPoint2(hatch.Point(13));
                        }
                    }

                    break;
            }
        }
    }
}
