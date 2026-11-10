using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <content>The tables: their control objects and their records.</content>
    internal sealed partial class DwgObjectReader
    {
        /// <summary>
        /// The values of a dimension style in the order of the file, each as its bit code, its DXF group and the value
        /// AutoCAD leaves out of a DXF file.
        /// </summary>
        private const string DimStyleValues =
            "TV 3;TV 4;BD 40 1;BD 41 0.18;BD 42 0.0625;BD 43 0.38;BD 44 0.18;BD 45 0;BD 46 0;BD 47 0;BD 48 0;" +
            "B 71 0;B 72 0;B 73 1;B 74 1;B 75 0;B 76 0;BS 77 0;BS 78 0;BS 79 0;" +
            "BD 140 0.18;BD 141 0.09;BD 142 0;BD 143 25.4;BD 144 1;BD 145 0;BD 146 1;BD 147 0.09;BD 148 0;" +
            "B 170 0;BS 171 2;B 172 0;B 173 0;B 174 0;B 175 0;BS 176 0;BS 177 0;BS 178 0;" +
            "BS 179 0;BS 271 4;BS 272 4;BS 273 2;BS 274 2;BS 275 0;BS 276 0;BS 277 2;BS 278 46;BS 279 0;" +
            "BS 280 0;B 281 0;B 282 0;BS 283 1;BS 284 0;BS 285 0;BS 286 0;B 288 0;BS 289 3;BS 371 -2;BS 372 -2";

        /// <summary>The name and the flags every table record starts with; returns the flags of group 70.</summary>
        private int ReadRecordHead(string subclass)
        {
            _object.IsTableRecord = true;
            _object.RecordName = _data.Text();
            // The bit that says the record is referenced is not carried over: AutoCAD writes DXF without it.
            _data.Bit();
            _data.BitShort();
            int flags = _data.Bit() ? 16 : 0;
            _object.Add(100, subclass);
            _object.Add(2, _object.RecordName);
            return flags;
        }

        private void ReadControl(int type)
        {
            _object.IsControl = true;
            int count = Count(_data.BitLong());
            _object.BlockFlags = count;
            int extra = type == 0x44 ? _data.RawChar() : 0;

            for (int i = 0; i < count; i++)
            {
                _object.Entries.Add(H());
            }

            if (type == 0x44)
            {
                // The table of the dimension styles lists some of its styles a second time: groups 71 and 340 of its head.
                _object.Add(100, "AcDbDimStyleTable");
                _object.Add(71, extra);

                for (int i = 0; i < extra; i++)
                {
                    _object.Add(340, H());
                }
            }

            if (type == 0x30 || type == 0x38)
            {
                // The model space and the paper space of the blocks, ByBlock and ByLayer of the linetypes: DXF lists them first.
                ulong first = H();
                _object.Entries.Insert(0, H());
                _object.Entries.Insert(0, first);
            }
        }

        private void ReadBlockHeader()
        {
            int flags = ReadRecordHead("AcDbBlockTableRecord");
            flags |= _data.Bit() ? 1 : 0;
            flags |= _data.Bit() ? 2 : 0;
            bool reference = _data.Bit();
            bool overlay = _data.Bit();
            flags |= reference ? 4 : 0;
            flags |= overlay ? 8 : 0;
            _data.Bit();
            double[] basePoint = _data.Point3();
            byte[] path = _data.Text();
            int inserts = 0;

            while (_data.RawChar() != 0)
            {
                inserts++;
            }

            byte[] description = _data.Text();
            int preview = Count(_data.BitLong());
            _data.Position += preview * 8L;
            H();
            _object.BlockEntity = H();

            if (!reference && !overlay)
            {
                _object.First = H();
                _object.Last = H();
            }

            _object.EndBlock = H();
            var references = new List<ulong>();

            for (int i = 0; i < inserts; i++)
            {
                references.Add(H());
            }

            _object.Add(340, H());

            if (references.Count > 0)
            {
                _object.Add(102, "{BLKREFS");

                foreach (ulong one in references)
                {
                    _object.Add(331, one);
                }

                _object.Add(102, "}");
            }

            _object.BlockFlags = flags;
            _object.BlockBody = new List<DwgGroup>
            {
                new DwgGroup(2, _object.RecordName),
                new DwgGroup(70, flags),
                new DwgGroup(10, basePoint[0]),
                new DwgGroup(20, basePoint[1]),
                new DwgGroup(30, basePoint[2]),
                new DwgGroup(3, _object.RecordName),
                new DwgGroup(1, path),
            };

            if (description.Length > 0)
            {
                _object.BlockBody.Add(new DwgGroup(4, description));
            }
        }

        private void ReadLayer()
        {
            int flags = ReadRecordHead("AcDbLayerTableRecord");
            int values = _data.BitShort();
            int colour = _data.BitShort();
            H();
            ulong plotStyle = H();
            ulong linetype = H();
            _object.Add(70, flags | ((values & 1) != 0 ? 1 : 0) | ((values & 4) != 0 ? 2 : 0) | ((values & 8) != 0 ? 4 : 0));
            _object.Add(62, (values & 2) != 0 ? -Math.Abs(colour) : colour);
            _object.Add(6, new DwgName(linetype, "Continuous"));

            if ((values & 16) == 0)
            {
                _object.Add(290, 0);
            }

            _object.Add(370, LineWeight((values >> 5) & 0x1F));
            _object.Add(390, plotStyle);
        }

        private void ReadStyle()
        {
            int flags = ReadRecordHead("AcDbTextStyleTableRecord");
            flags |= _data.Bit() ? 1 : 0;
            flags |= _data.Bit() ? 4 : 0;
            _object.Add(70, flags);
            _object.Add(40, _data.BitDouble());
            _object.Add(41, _data.BitDouble());
            _object.Add(50, Degrees(_data.BitDouble()));
            _object.Add(71, _data.RawChar());
            _object.Add(42, _data.BitDouble());
            _object.Add(3, _data.Text());
            _object.Add(4, _data.Text());
        }

        private void ReadLinetype()
        {
            int flags = ReadRecordHead("AcDbLinetypeTableRecord");
            byte[] description = _data.Text();
            double length = _data.BitDouble();
            int alignment = _data.RawChar();
            int dashes = _data.RawChar();
            var values = new double[dashes][];
            var kinds = new int[dashes][];

            for (int i = 0; i < dashes; i++)
            {
                double dash = _data.BitDouble();
                int shape = _data.BitShort();
                double x = _data.RawDouble();
                double y = _data.RawDouble();
                double scale = _data.BitDouble();
                double rotation = _data.BitDouble();
                values[i] = new[] { dash, x, y, scale, rotation };
                kinds[i] = new[] { shape, _data.BitShort() };
            }

            byte[] texts = _data.Bytes(256);
            H();
            _object.Add(70, flags);
            _object.Add(3, description);
            _object.Add(72, alignment);
            _object.Add(73, dashes);
            _object.Add(40, length);

            for (int i = 0; i < dashes; i++)
            {
                ulong style = H();
                int kind = kinds[i][1];
                _object.Add(49, values[i][0]);
                _object.Add(74, kind);

                if (kind == 0)
                {
                    continue;
                }

                _object.Add(75, (kind & 2) != 0 ? 0 : kinds[i][0]);
                _object.Add(340, style);
                _object.Add(46, values[i][3]);
                _object.Add(50, Degrees(values[i][4]));
                _object.Add(44, values[i][1]);
                _object.Add(45, values[i][2]);

                if ((kind & 2) != 0)
                {
                    // The text of a dash stands in the text area at the place the shape number names, up to a zero.
                    int from = Math.Max(0, Math.Min(kinds[i][0], texts.Length));
                    int to = from;

                    while (to < texts.Length && texts[to] != 0)
                    {
                        to++;
                    }

                    var text = new byte[to - from];
                    Array.Copy(texts, from, text, 0, text.Length);
                    _object.Add(9, text);
                }
            }
        }

        /// <summary>The view of a VIEW or a VPORT up to its view mode; returns the view mode of group 71.</summary>
        private int ReadViewMode()
        {
            int mode = _data.Bit() ? 1 : 0;
            mode |= _data.Bit() ? 2 : 0;
            mode |= _data.Bit() ? 4 : 0;
            mode |= _data.Bit() ? 0 : 16;
            return mode;
        }

        private void ReadView()
        {
            int flags = ReadRecordHead("AcDbViewTableRecord");
            double height = _data.BitDouble();
            double width = _data.BitDouble();
            double[] centre = _data.RawPoint2();
            double[] target = _data.Point3();
            double[] direction = _data.Point3();
            double twist = _data.BitDouble();
            double lens = _data.BitDouble();
            double front = _data.BitDouble();
            double back = _data.BitDouble();
            int mode = ReadViewMode();
            int render = _data.RawChar();
            flags |= _data.Bit() ? 1 : 0;
            bool hasUcs = _data.Bit();
            _object.Add(70, flags);
            _object.Add(40, height);
            _object.AddPoint(10, centre);
            _object.Add(41, width);
            _object.AddPoint(11, direction);
            _object.AddPoint(12, target);
            _object.Add(42, lens);
            _object.Add(43, front);
            _object.Add(44, back);
            _object.Add(50, Degrees(twist));
            _object.Add(71, mode);
            _object.Add(281, render);
            _object.Add(72, hasUcs ? 1 : 0);

            if (hasUcs)
            {
                _object.AddPoint(110, _data.Point3());
                _object.AddPoint(111, _data.Point3());
                _object.AddPoint(112, _data.Point3());
                double elevation = _data.BitDouble();
                _object.Add(79, _data.BitShort());
                _object.Add(146, elevation);
                H();
                H();
                ulong named = H();

                if (named != 0)
                {
                    _object.Add(345, named);
                }
            }
        }

        private void ReadUcs()
        {
            int flags = ReadRecordHead("AcDbUCSTableRecord");
            _object.Add(70, flags);
            _object.AddPoint(10, _data.Point3());
            _object.AddPoint(11, _data.Point3());
            _object.AddPoint(12, _data.Point3());
            double elevation = _data.BitDouble();
            _data.BitShort();
            _object.Add(79, 0);
            _object.Add(146, elevation);
        }

        private void ReadVport()
        {
            int flags = ReadRecordHead("AcDbViewportTableRecord");
            double height = _data.BitDouble();
            double width = _data.BitDouble();
            double[] centre = _data.RawPoint2();
            double[] target = _data.Point3();
            double[] direction = _data.Point3();
            double twist = _data.BitDouble();
            double lens = _data.BitDouble();
            double front = _data.BitDouble();
            double back = _data.BitDouble();
            int mode = ReadViewMode();
            int render = _data.RawChar();
            double[] lowerLeft = _data.RawPoint2();
            double[] upperRight = _data.RawPoint2();
            mode |= _data.Bit() ? 8 : 0;
            int circleZoom = _data.BitShort();
            int fastZoom = _data.Bit() ? 1 : 0;
            int icon = _data.Bit() ? 1 : 0;
            icon |= _data.Bit() ? 2 : 0;
            int grid = _data.Bit() ? 1 : 0;
            double[] gridSpacing = _data.RawPoint2();
            int snap = _data.Bit() ? 1 : 0;
            int snapStyle = _data.Bit() ? 1 : 0;
            int isoPair = _data.BitShort();
            double snapRotation = _data.BitDouble();
            double[] snapBase = _data.RawPoint2();
            double[] snapSpacing = _data.RawPoint2();
            _data.Bit();
            int perViewport = _data.Bit() ? 1 : 0;
            double[] origin = _data.Point3();
            double[] xAxis = _data.Point3();
            double[] yAxis = _data.Point3();
            double elevation = _data.BitDouble();
            int ortho = _data.BitShort();
            _object.Add(70, flags);
            _object.AddPoint(10, lowerLeft);
            _object.AddPoint(11, upperRight);
            _object.AddPoint(12, centre);
            _object.AddPoint(13, snapBase);
            _object.AddPoint(14, snapSpacing);
            _object.AddPoint(15, gridSpacing);
            _object.AddPoint(16, direction);
            _object.AddPoint(17, target);
            _object.Add(40, height);
            _object.Add(41, height == 0.0 ? 1.0 : width / height);
            _object.Add(42, lens);
            _object.Add(43, front);
            _object.Add(44, back);
            _object.Add(50, Degrees(snapRotation));
            _object.Add(51, Degrees(twist));
            _object.Add(71, mode);
            _object.Add(72, circleZoom);
            _object.Add(73, fastZoom);
            _object.Add(74, icon);
            _object.Add(75, snap);
            _object.Add(76, grid);
            _object.Add(77, snapStyle);
            _object.Add(78, isoPair);
            _object.Add(281, render);
            _object.Add(65, perViewport);
            _object.AddPoint(110, origin);
            _object.AddPoint(111, xAxis);
            _object.AddPoint(112, yAxis);
            _object.Add(79, ortho);
            _object.Add(146, elevation);
        }

        private void ReadAppId()
        {
            int flags = ReadRecordHead("AcDbRegAppTableRecord");
            _object.Add(70, flags);
        }

        private void ReadDimStyle()
        {
            int flags = ReadRecordHead("AcDbDimStyleTableRecord");
            var groups = new List<DwgGroup>();

            foreach (string entry in DimStyleValues.Split(';'))
            {
                string[] parts = entry.Split(' ');
                int code = int.Parse(parts[1], CultureInfo.InvariantCulture);

                switch (parts[0])
                {
                    case "TV":
                        byte[] text = _data.Text();

                        if (text.Length > 0)
                        {
                            groups.Add(new DwgGroup(code, text));
                        }

                        break;
                    case "BD":
                        double number = _data.BitDouble();

                        if (number != double.Parse(parts[2], CultureInfo.InvariantCulture))
                        {
                            groups.Add(new DwgGroup(code, number));
                        }

                        break;
                    default:
                        int whole = parts[0] == "B" ? (_data.Bit() ? 1 : 0) : _data.BitShort();

                        if (whole != int.Parse(parts[2], CultureInfo.InvariantCulture))
                        {
                            groups.Add(new DwgGroup(code, whole));
                        }

                        break;
                }
            }

            H();

            for (int code = 340; code <= 344; code++)
            {
                ulong handle = H();

                if (handle != 0)
                {
                    groups.Add(new DwgGroup(code, handle));
                }
            }

            // DXF writes the values by their group code; no code comes twice, so the order is full.
            groups.Sort((a, b) => a.Code.CompareTo(b.Code));
            _object.Add(70, flags);
            _object.Body.AddRange(groups);
        }
    }
}
