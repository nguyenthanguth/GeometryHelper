using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <content>The tables: their control objects and their records.</content>
    internal sealed partial class DwgObjectWriter
    {
        /// <summary>The values of a dimension style in the order of the file, as <see cref="DwgObjectReader"/> reads them: bit code, DXF group, the value DXF leaves out, the first version.</summary>
        private const string DimStyleValues =
            "TV 3;TV 4;BD 40 1;BD 41 0.18;BD 42 0.0625;BD 43 0.38;BD 44 0.18;BD 45 0;BD 46 0;BD 47 0;BD 48 0;" +
            "BD 49 1 1021;BD 50 0.7853981633974483 1021;BS 69 0 1021;CMC 70 0 1021;" +
            "B 71 0;B 72 0;B 73 1;B 74 1;B 75 0;B 76 0;BS 77 0;BS 78 0;BS 79 0;BS 90 0 1021;" +
            "BD 140 0.18;BD 141 0.09;BD 142 0;BD 143 25.4;BD 144 1;BD 145 0;BD 146 1;BD 147 0.09;BD 148 0;" +
            "B 170 0;BS 171 2;B 172 0;B 173 0;B 174 0;B 175 0;CMC 176 0;CMC 177 0;CMC 178 0;" +
            "BS 179 0;BS 271 4;BS 272 4;BS 273 2;BS 274 2;BS 275 0;BS 276 0;BS 277 2;BS 278 46;BS 279 0;" +
            "BS 280 0;B 281 0;B 282 0;BS 283 1;BS 284 0;BS 285 0;BS 286 0;B 288 0;BS 289 3;B 290 0 1021;" +
            "B 294 0 1024;BD 0 100 1024;TV 0 0 1024;BD 0 100 1024;TV 0 0 1024;BS 371 -2;BS 372 -2";

        /// <summary>The name a DWG keeps for a block: <c>*U</c> or <c>*D</c> alone for an anonymous block, <c>*Paper_Space</c> for that of every layout.</summary>
        private static string StoredBlockName(string name, int flags)
        {
            name = name.Trim();

            if (name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase))
            {
                return name.Substring(0, 12);
            }

            if ((flags & 1) != 0 && name.StartsWith("*", StringComparison.Ordinal) && name.Length > 2)
            {
                return name.Substring(0, 2);
            }

            return name;
        }

        /// <summary>The stored name of the block of the block record <paramref name="header"/>.</summary>
        private string BlockName(DwgRecord header)
        {
            if (header == null)
            {
                return string.Empty;
            }

            DwgRecord[] ends = _drawing.BlockEnds.TryGetValue(header.Handle, out DwgRecord[] found) ? found : null;
            int flags = ends != null ? ends[0].Of("AcDbBlockBegin").Int(70) : 0;
            return StoredBlockName(header.Of(null).Text(2), flags);
        }

        /// <summary>The name and the flags every table record starts with: from R2007 a bit short with 0x100 for a record of an xref.</summary>
        private void WriteRecordHead(string name, int flags)
        {
            T(name);

            if (R2007Plus)
            {
                _data.BitShort((flags & 16) != 0 ? 0x100 : 0);
            }
            else
            {
                _data.Bit(false);
                _data.BitShort(0);
                _data.Bit((flags & 16) != 0);
            }
        }

        /// <summary>The records of a table but the two DXF lists first that the control object names apart: model and paper space, ByBlock and ByLayer.</summary>
        private List<DwgRecord> Entries(int type, out List<ulong> apart)
        {
            apart = new List<ulong>();
            var entries = new List<DwgRecord>();

            foreach (DwgRecord record in _record.Records)
            {
                if (!_written.Contains(record.Handle))
                {
                    continue;
                }

                string name = record.Of(null).Text(2).Trim();

                if ((type == 0x30 && (name.Equals("*Model_Space", StringComparison.OrdinalIgnoreCase) || name.Equals("*Paper_Space", StringComparison.OrdinalIgnoreCase)))
                    || (type == 0x38 && (name.Equals("ByBlock", StringComparison.OrdinalIgnoreCase) || name.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                entries.Add(record);
            }

            if (type == 0x30)
            {
                apart.Add(_modelSpace);
                apart.Add(_paperSpace);
            }
            else if (type == 0x38)
            {
                apart.Add(_drawing.HandleOf("LTYPE", "ByBlock"));
                apart.Add(_drawing.HandleOf("LTYPE", "ByLayer"));
            }

            return entries;
        }

        private void WriteControl(int type)
        {
            List<DwgRecord> entries = Entries(type, out List<ulong> apart);
            _data.BitLong(entries.Count);
            List<string> styles = type == 0x44 ? _record.Of("AcDbDimStyleTable").Each(340) : null;

            if (type == 0x44)
            {
                _data.RawChar(styles.Count);
            }

            foreach (DwgRecord entry in entries)
            {
                H(2, entry.Handle);
            }

            if (styles != null)
            {
                foreach (string style in styles)
                {
                    H(5, DwgRecord.Hex(style));
                }
            }

            foreach (ulong handle in apart)
            {
                H(3, handle);
            }
        }

        private void WriteBlockHeader()
        {
            DwgGroups record = _record.Of(null);
            DwgRecord[] ends = _drawing.BlockEnds.TryGetValue(_record.Handle, out DwgRecord[] found) ? found : new DwgRecord[2];
            DwgGroups begin = ends[0] != null ? ends[0].Of("AcDbBlockBegin") : new DwgGroups(new List<KeyValuePair<int, string>>(), null);
            int flags = begin.Int(70);
            bool reference = (flags & 4) != 0;
            bool overlay = (flags & 8) != 0;
            WriteRecordHead(StoredBlockName(record.Text(2), flags), flags);
            _data.Bit((flags & 1) != 0);
            _data.Bit((flags & 2) != 0);
            _data.Bit(reference);
            _data.Bit(overlay);
            _data.Bit((flags & 32) != 0);
            List<DwgRecord> owned = OwnedBy(_record.Handle);

            if (R2004Plus && !reference && !overlay)
            {
                _data.BitLong(owned.Count);
            }

            _data.Point3(begin.Point(10));
            T(begin.Text(1));
            List<ulong> inserts = new List<string>(record.Each(331)).ConvertAll(DwgRecord.Hex).FindAll(handle => _written.Contains(handle));

            foreach (ulong unused in inserts)
            {
                _data.RawChar(1);
            }

            _data.RawChar(0);
            T(begin.Text(4));
            _data.BitLong(0);

            if (R2007Plus)
            {
                _data.BitShort(record.Int(70));
                _data.Bit(record.Int(280, 1) != 0);
                _data.RawChar(record.Int(281, 1));
            }

            H(5, 0);
            H(3, ends[0]?.Handle ?? 0);

            if (!R2004Plus && !reference && !overlay)
            {
                H(4, owned.Count > 0 ? owned[0].Handle : 0);
                H(4, owned.Count > 0 ? owned[owned.Count - 1].Handle : 0);
            }

            if (R2004Plus && !reference && !overlay)
            {
                foreach (DwgRecord entity in owned)
                {
                    H(3, entity.Handle);
                }
            }

            H(3, ends[1]?.Handle ?? 0);

            foreach (ulong insert in inserts)
            {
                H(4, insert);
            }

            H(5, record.Handle(340));
        }

        /// <summary>The written entities a block record owns, in order.</summary>
        private List<DwgRecord> OwnedBy(ulong block)
        {
            if (!_drawing.Owned.TryGetValue(block, out List<DwgRecord> owned))
            {
                return new List<DwgRecord>();
            }

            return owned.FindAll(entity => _written.Contains(entity.Handle));
        }

        private void WriteLayer()
        {
            DwgGroups layer = _record.Of("AcDbLayerTableRecord");
            int flags = layer.Int(70);
            int colour = layer.Int(62, 7);
            WriteRecordHead(layer.Text(2), flags);
            int values = ((flags & 1) != 0 ? 1 : 0) | (colour < 0 ? 2 : 0) | ((flags & 2) != 0 ? 4 : 0) | ((flags & 4) != 0 ? 8 : 0)
                | (layer.Int(290, 1) != 0 ? 16 : 0) | (LineWeightIndex(layer.Int(370, -3)) << 5);
            _data.BitShort(values);
            WriteColour(Math.Abs(colour), layer.Has(420) ? layer.Int(420) : -1);
            H(5, 0);
            H(5, layer.Handle(390));

            if (R2007Plus)
            {
                H(5, layer.Handle(347));
            }

            H(5, _drawing.HandleOf("LTYPE", layer.Text(6, "Continuous")));

            if (R2013Plus)
            {
                H(5, layer.Handle(348));
            }
        }

        private void WriteStyle()
        {
            DwgGroups style = _record.Of("AcDbTextStyleTableRecord");
            int flags = style.Int(70);
            WriteRecordHead(style.Text(2), flags);
            _data.Bit((flags & 1) != 0);
            _data.Bit((flags & 4) != 0);
            _data.BitDouble(style.Double(40));
            _data.BitDouble(style.Double(41, 1.0));
            _data.BitDouble(Radians(style.Double(50)));
            _data.RawChar(style.Int(71));
            _data.BitDouble(style.Double(42));
            T(style.Text(3));
            T(style.Text(4));
            H(5, 0);
        }

        private void WriteLinetype()
        {
            DwgGroups linetype = _record.Of("AcDbLinetypeTableRecord");
            int flags = linetype.Int(70);
            WriteRecordHead(linetype.Text(2), flags);
            T(linetype.Text(3));
            _data.BitDouble(linetype.Double(40));
            _data.RawChar(linetype.Int(72, 65));

            // The dashes: each starts at its 49, its other groups up to the next.
            var dashes = new List<Dictionary<int, string>>();

            foreach (KeyValuePair<int, string> group in linetype.All)
            {
                if (group.Key == 49)
                {
                    dashes.Add(new Dictionary<int, string>());
                }

                if (dashes.Count > 0 && !dashes[dashes.Count - 1].ContainsKey(group.Key))
                {
                    dashes[dashes.Count - 1][group.Key] = group.Value;
                }
            }

            _data.RawChar(dashes.Count);
            int size = R2007Plus ? 512 : 256;
            var area = new byte[size];
            int areaUsed = 0;
            bool hasText = false;

            foreach (Dictionary<int, string> dash in dashes)
            {
                int kind = Value(dash, 74);
                int shape = Value(dash, 75);

                if ((kind & 2) != 0)
                {
                    hasText = true;
                    string text = dash.TryGetValue(9, out string found) ? found : string.Empty;
                    byte[] bytes = R2007Plus ? Encoding.Unicode.GetBytes(text) : Encoding.UTF8.GetBytes(text);
                    int step = R2007Plus ? 2 : 1;
                    shape = areaUsed;

                    if (areaUsed + bytes.Length + step <= size)
                    {
                        Array.Copy(bytes, 0, area, areaUsed, bytes.Length);
                        areaUsed += bytes.Length + step;
                    }
                }

                _data.BitDouble(Real(dash, 49));
                _data.BitShort(shape);
                _data.RawDouble(Real(dash, 44));
                _data.RawDouble(Real(dash, 45));
                _data.BitDouble(dash.ContainsKey(46) ? Real(dash, 46) : 1.0);
                _data.BitDouble(Radians(Real(dash, 50)));
                _data.BitShort(kind);
            }

            if (!R2007Plus || hasText)
            {
                _data.Bytes(area);
            }

            H(5, 0);

            foreach (Dictionary<int, string> dash in dashes)
            {
                H(5, dash.TryGetValue(340, out string style) ? DwgRecord.Hex(style) : 0);
            }
        }

        private static int Value(Dictionary<int, string> groups, int code) => groups.TryGetValue(code, out string value) ? DwgRecord.Int(value) : 0;

        private static double Real(Dictionary<int, string> groups, int code) => groups.TryGetValue(code, out string value) ? DwgRecord.Double(value) : 0.0;

        /// <summary>The view mode of a VIEW or a VPORT: perspective, front clip, back clip, then the inverse of DXF's bit 16.</summary>
        private void WriteViewMode(int mode)
        {
            _data.Bit((mode & 1) != 0);
            _data.Bit((mode & 2) != 0);
            _data.Bit((mode & 4) != 0);
            _data.Bit((mode & 16) == 0);
        }

        private void WriteView()
        {
            DwgGroups view = _record.Of("AcDbViewTableRecord");
            int flags = view.Int(70);
            WriteRecordHead(view.Text(2), flags);
            _data.BitDouble(view.Double(40));
            _data.BitDouble(view.Double(41));
            _data.RawPoint2(view.Point(10));
            _data.Point3(view.Point(12));
            _data.Point3(view.Point(11, 0, 0, 1));
            _data.BitDouble(Radians(view.Double(50)));
            _data.BitDouble(view.Double(42, 50.0));
            _data.BitDouble(view.Double(43));
            _data.BitDouble(view.Double(44));
            WriteViewMode(view.Int(71));
            _data.RawChar(view.Int(281));

            if (R2007Plus)
            {
                _data.Bit(true);
                _data.RawChar(1);
                _data.BitDouble(0.0);
                _data.BitDouble(0.0);
                WriteColour(250);
            }

            _data.Bit((flags & 1) != 0);
            bool hasUcs = view.Int(72) != 0;
            _data.Bit(hasUcs);

            if (hasUcs)
            {
                _data.Point3(view.Point(110));
                _data.Point3(view.Point(111, 1));
                _data.Point3(view.Point(112, 0, 1));
                _data.BitDouble(view.Double(146));
                _data.BitShort(view.Int(79));
            }

            H(5, 0);

            if (R2007Plus)
            {
                _data.Bit(view.Int(73) != 0);
                H(4, 0);
                H(5, 0);
                H(3, 0);
            }

            if (hasUcs)
            {
                H(5, 0);
                H(5, view.Handle(345));
            }

            if (R2007Plus)
            {
                H(4, 0);
            }
        }

        private void WriteUcs()
        {
            DwgGroups ucs = _record.Of("AcDbUCSTableRecord");
            WriteRecordHead(ucs.Text(2), ucs.Int(70));
            _data.Point3(ucs.Point(10));
            _data.Point3(ucs.Point(11, 1));
            _data.Point3(ucs.Point(12, 0, 1));
            _data.BitDouble(ucs.Double(146));
            _data.BitShort(ucs.Int(79));
            _data.BitShort(0);
            H(5, 0);
            H(5, 0);
            H(5, 0);
        }

        private void WriteVport()
        {
            DwgGroups vport = _record.Of("AcDbViewportTableRecord");
            int mode = vport.Int(71);
            double height = vport.Double(40, 1.0);
            WriteRecordHead(vport.Text(2), vport.Int(70));
            _data.BitDouble(height);
            _data.BitDouble(vport.Double(41, 1.0) * height);
            _data.RawPoint2(vport.Point(12));
            _data.Point3(vport.Point(17));
            _data.Point3(vport.Point(16, 0, 0, 1));
            _data.BitDouble(Radians(vport.Double(51)));
            _data.BitDouble(vport.Double(42, 50.0));
            _data.BitDouble(vport.Double(43));
            _data.BitDouble(vport.Double(44));
            WriteViewMode(mode);
            _data.RawChar(vport.Int(281));

            if (R2007Plus)
            {
                _data.Bit(vport.Int(292, 1) != 0);
                _data.RawChar(vport.Int(282, 1));
                _data.BitDouble(vport.Double(141));
                _data.BitDouble(vport.Double(142));
                WriteColour(vport.Int(63, 250), vport.Has(421) ? vport.Int(421) : -1);
            }

            _data.RawPoint2(vport.Point(10));
            _data.RawPoint2(vport.Point(11, 1, 1));
            _data.Bit((mode & 8) != 0);
            _data.BitShort(vport.Int(72, 1000));
            _data.Bit(vport.Int(73, 1) != 0);
            int icon = vport.Int(74, 3);
            _data.Bit((icon & 1) != 0);
            _data.Bit((icon & 2) != 0);
            _data.Bit(vport.Int(76) != 0);
            _data.RawPoint2(vport.Point(15, 10, 10));
            _data.Bit(vport.Int(75) != 0);
            _data.Bit(vport.Int(77) != 0);
            _data.BitShort(vport.Int(78));
            _data.BitDouble(Radians(vport.Double(50)));
            _data.RawPoint2(vport.Point(13));
            _data.RawPoint2(vport.Point(14, 10, 10));
            _data.Bit(false);
            _data.Bit(vport.Int(65, 1) != 0);
            _data.Point3(vport.Point(110));
            _data.Point3(vport.Point(111, 1));
            _data.Point3(vport.Point(112, 0, 1));
            _data.BitDouble(vport.Double(146));
            _data.BitShort(vport.Int(79));

            if (R2007Plus)
            {
                _data.BitShort(vport.Int(60, 7));
                _data.BitShort(vport.Int(61, 5));
            }

            H(5, 0);

            if (R2007Plus)
            {
                H(4, 0);
                H(5, vport.Handle(348));
                H(3, 0);
            }

            H(5, vport.Handle(345));
            H(5, vport.Handle(346));
        }

        private void WriteAppId()
        {
            DwgGroups app = _record.Of("AcDbRegAppTableRecord");
            WriteRecordHead(app.Text(2), app.Int(70));
            _data.RawChar(0);
            H(5, 0);
        }

        private void WriteDimStyle()
        {
            DwgGroups style = _record.Of("AcDbDimStyleTableRecord");
            WriteRecordHead(style.Text(2), style.Int(70));

            foreach (string entry in DimStyleValues.Split(';'))
            {
                string[] parts = entry.Split(' ');
                int code = int.Parse(parts[1], CultureInfo.InvariantCulture);

                if (parts.Length > 3 && _version < int.Parse(parts[3], CultureInfo.InvariantCulture))
                {
                    continue;
                }

                bool given = code != 0 && style.Has(code);

                switch (parts[0])
                {
                    case "TV":
                        T(given ? style.Text(code) : string.Empty);
                        break;
                    case "BD":
                        _data.BitDouble(given ? style.Double(code) : double.Parse(parts[2], CultureInfo.InvariantCulture));
                        break;
                    case "B":
                        _data.Bit((given ? style.Int(code) : int.Parse(parts[2], CultureInfo.InvariantCulture)) != 0);
                        break;
                    case "CMC":
                        WriteColour(given ? style.Int(code) : int.Parse(parts[2], CultureInfo.InvariantCulture));
                        break;
                    default:
                        _data.BitShort(given ? style.Int(code) : int.Parse(parts[2], CultureInfo.InvariantCulture));
                        break;
                }
            }

            if (R2010Plus)
            {
                // A bit AutoCAD writes after the lineweights, which the specification does not name.
                _data.Bit(false);
            }

            H(5, 0);

            for (int code = 340; code <= (R2007Plus ? 347 : 344); code++)
            {
                H(5, style.Handle(code));
            }
        }
    }
}
