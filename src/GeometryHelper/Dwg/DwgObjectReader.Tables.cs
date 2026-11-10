using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <content>The tables: their control objects and their records.</content>
    internal sealed partial class DwgObjectReader
    {
        /// <summary>
        /// The values of a dimension style in the order of the file, each as its bit code, its DXF group (0 for one DXF does
        /// not carry), the value AutoCAD leaves out of a DXF file, and where it came later the first version that has it.
        /// A colour (CMC) is a bit short before R2004 and a full colour from R2004.
        /// </summary>
        private const string DimStyleValues =
            "TV 3;TV 4;BD 40 1;BD 41 0.18;BD 42 0.0625;BD 43 0.38;BD 44 0.18;BD 45 0;BD 46 0;BD 47 0;BD 48 0;" +
            "BD 49 1 1021;BD 50 0.7853981633974483 1021;BS 69 0 1021;CMC 70 0 1021;" +
            "B 71 0;B 72 0;B 73 1;B 74 1;B 75 0;B 76 0;BS 77 0;BS 78 0;BS 79 0;BS 90 0 1021;" +
            "BD 140 0.18;BD 141 0.09;BD 142 0;BD 143 25.4;BD 144 1;BD 145 0;BD 146 1;BD 147 0.09;BD 148 0;" +
            "B 170 0;BS 171 2;B 172 0;B 173 0;B 174 0;B 175 0;CMC 176 0;CMC 177 0;CMC 178 0;" +
            "BS 179 0;BS 271 4;BS 272 4;BS 273 2;BS 274 2;BS 275 0;BS 276 0;BS 277 2;BS 278 46;BS 279 0;" +
            "BS 280 0;B 281 0;B 282 0;BS 283 1;BS 284 0;BS 285 0;BS 286 0;B 288 0;BS 289 3;B 290 0 1021;" +
            "B 294 0 1024;BD 0 0 1024;TV 0 0 1024;BD 0 0 1024;TV 0 0 1024;BS 371 -2;BS 372 -2";

        /// <summary>The name and the flags every table record starts with; returns the flags of group 70.</summary>
        private int ReadRecordHead(string subclass)
        {
            _object.IsTableRecord = true;
            _object.RecordName = T();
            int flags;

            if (R2007Plus)
            {
                // From R2007 one bit short: the index of the xref, with 0x100 set for a record that comes from one.
                flags = (_data.BitShort() & 0x100) != 0 ? 16 : 0;
            }
            else
            {
                // The bit that says the record is referenced is not carried over: AutoCAD writes DXF without it.
                _data.Bit();
                _data.BitShort();
                flags = _data.Bit() ? 16 : 0;
            }

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
            int owned = R2004Plus && !reference && !overlay ? Count(_data.BitLong()) : 0;
            double[] basePoint = _data.Point3();
            byte[] path = T();
            int inserts = 0;

            while (_data.RawChar() != 0)
            {
                inserts++;
            }

            byte[] description = T();
            int preview = Count(_data.BitLong());
            _data.Position += preview * 8L;
            int units = 0;
            int explodable = 1;
            int scalable = 1;

            if (R2007Plus)
            {
                units = _data.BitShort();
                explodable = _data.Bit() ? 1 : 0;
                scalable = _data.RawChar() != 0 ? 1 : 0;
            }

            H();
            _object.BlockEntity = H();

            if (!reference && !overlay)
            {
                ReadOwnedHandles(owned);
            }
            else if (R2004Plus)
            {
                _object.Owned = new List<ulong>();
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

            if (R2007Plus)
            {
                _object.Add(70, units);
                _object.Add(280, explodable);
                _object.Add(281, scalable);
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
            DwgColour colour = ReadColour();
            H();
            ulong plotStyle = H();
            ulong material = R2007Plus ? H() : 0;
            ulong linetype = H();
            ulong visualStyle = R2013Plus ? H() : 0;
            _object.Add(70, flags | ((values & 1) != 0 ? 1 : 0) | ((values & 4) != 0 ? 2 : 0) | ((values & 8) != 0 ? 4 : 0));

            // A layer that is off has its colour index negative.
            colour.Index = (values & 2) != 0 ? -Math.Abs(colour.Index) : colour.Index;
            AddColour(_object.Body, colour, false);
            _object.Add(6, new DwgName(linetype, "Continuous"));

            if ((values & 16) == 0)
            {
                _object.Add(290, 0);
            }

            _object.Add(370, LineWeight((values >> 5) & 0x1F));
            _object.Add(390, plotStyle);

            if (R2007Plus)
            {
                _object.Add(347, material);
            }

            if (R2013Plus)
            {
                _object.Add(348, visualStyle);
            }
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
            _object.Add(3, T());
            _object.Add(4, T());
        }

        private void ReadLinetype()
        {
            int flags = ReadRecordHead("AcDbLinetypeTableRecord");
            byte[] description = T();
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

            // The texts of the dashes: 256 bytes of the code page before R2007; from R2007 512 bytes of UTF-16, there only
            // where a dash has a text.
            bool hasText = false;

            for (int i = 0; i < dashes; i++)
            {
                hasText |= (kinds[i][1] & 2) != 0;
            }

            byte[] texts = !R2007Plus ? _data.Bytes(256) : hasText ? _data.Bytes(512) : new byte[0];
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
                    int step = R2007Plus ? 2 : 1;

                    while (to + step <= texts.Length && (texts[to] != 0 || (step == 2 && texts[to + 1] != 0)))
                    {
                        to += step;
                    }

                    var text = new byte[to - from];
                    Array.Copy(texts, from, text, 0, text.Length);
                    _object.Add(9, R2007Plus ? System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.Unicode.GetString(text)) : text);
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

            if (R2007Plus)
            {
                // The lighting of the view, which AutoCAD does not write to DXF.
                _data.Bit();
                _data.RawChar();
                _data.BitDouble();
                _data.BitDouble();
                ReadColour();
            }

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
            }

            H();

            if (R2007Plus)
            {
                _object.Add(73, _data.Bit() ? 1 : 0);
                H();
                H();
                H();
            }

            if (hasUcs)
            {
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
            int defaultLighting = 0;
            int lightingType = 0;
            double brightness = 0.0;
            double contrast = 0.0;
            DwgColour ambient = null;

            if (R2007Plus)
            {
                defaultLighting = _data.Bit() ? 1 : 0;
                lightingType = _data.RawChar();
                brightness = _data.BitDouble();
                contrast = _data.BitDouble();
                ambient = ReadColour();
            }

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
            int gridFlags = R2007Plus ? _data.BitShort() : 0;
            int gridMajor = R2007Plus ? _data.BitShort() : 0;
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

            if (R2007Plus)
            {
                _object.Add(60, gridFlags);
                _object.Add(61, gridMajor);
                _object.Add(292, defaultLighting);
                _object.Add(282, lightingType);
                _object.Add(141, brightness);
                _object.Add(142, contrast);
                AddColour(_object.Body, ambient, false, 63, 421);
            }
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

                if (parts.Length > 3 && _version < int.Parse(parts[3], CultureInfo.InvariantCulture))
                {
                    continue;
                }

                switch (parts[0])
                {
                    case "TV":
                        byte[] text = T();

                        if (text.Length > 0 && code != 0)
                        {
                            groups.Add(new DwgGroup(code, text));
                        }

                        break;
                    case "BD":
                        double number = _data.BitDouble();

                        if (number != double.Parse(parts[2], CultureInfo.InvariantCulture) && code != 0)
                        {
                            groups.Add(new DwgGroup(code, number));
                        }

                        break;
                    default:
                        int whole = parts[0] == "B" ? (_data.Bit() ? 1 : 0) : parts[0] == "CMC" ? ReadColour().Index : _data.BitShort();

                        if (whole != int.Parse(parts[2], CultureInfo.InvariantCulture) && code != 0)
                        {
                            groups.Add(new DwgGroup(code, whole));
                        }

                        break;
                }
            }

            H();

            for (int code = 340; code <= (R2007Plus ? 347 : 344); code++)
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
