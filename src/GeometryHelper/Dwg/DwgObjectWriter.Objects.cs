using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <content>The objects that are neither entities nor tables: dictionaries, records of free data, layouts and the like.</content>
    internal sealed partial class DwgObjectWriter
    {
        private void WriteDictionary(bool withDefault)
        {
            DwgGroups dictionary = _record.Of("AcDbDictionary");
            bool hardOwner = dictionary.Int(280) != 0;
            var names = new List<string>();
            var handles = new List<ulong>();
            string name = null;

            foreach (KeyValuePair<int, string> group in dictionary.All)
            {
                if (group.Key == 3)
                {
                    name = group.Value;
                }
                else if ((group.Key == 350 || group.Key == 360) && name != null)
                {
                    ulong handle = DwgRecord.Hex(group.Value);

                    if (_written.Contains(handle))
                    {
                        names.Add(name);
                        handles.Add(handle);
                    }

                    name = null;
                }
            }

            _data.BitLong(names.Count);
            _data.BitShort(dictionary.Int(281));
            _data.RawChar(hardOwner ? 1 : 0);

            for (int i = 0; i < names.Count; i++)
            {
                T(names[i]);
                H(2, handles[i]);
            }

            if (withDefault)
            {
                H(5, _record.Of("AcDbDictionaryWithDefault").Handle(340));
            }
        }

        private void WriteDictionaryVariable()
        {
            DwgGroups variable = _record.Of("DictionaryVariables");
            _data.RawChar(variable.Int(280));
            T(variable.Text(1));
        }

        private void WriteScale()
        {
            DwgGroups scale = _record.Of("AcDbScale");
            _data.BitShort(scale.Int(70));
            T(scale.Text(300));
            _data.BitDouble(scale.Double(140, 1.0));
            _data.BitDouble(scale.Double(141, 1.0));
            _data.Bit(scale.Int(290) != 0);
        }

        /// <summary>
        /// A MATERIAL, from the groups DXF gives where they differ from a new material's: its name and description, its
        /// colours and maps, and the settings of R2007 (the reader's <c>ReadMaterial</c> gives the defaults).
        /// </summary>
        private void WriteMaterial()
        {
            DwgGroups material = _record.Of("AcDbMaterial");
            T(material.Text(1));
            T(material.Text(2));
            WriteMaterialColour(material, 70, 40, 90);
            WriteMaterialColour(material, 71, 41, 91);
            WriteMaterialMap(material, 42, 72, 3, 73, 74, 75, 43);
            WriteMaterialColour(material, 76, 45, 92);
            WriteMaterialMap(material, 46, 77, 4, 78, 79, 170, 47);
            _data.BitDouble(material.Double(44, 0.5));
            WriteMaterialMap(material, 48, 171, 6, 172, 173, 174, 49);
            _data.BitDouble(material.Double(140, 1.0));
            WriteMaterialMap(material, 141, 175, 7, 176, 177, 178, 142);
            WriteMaterialMap(material, 143, 179, 8, 270, 271, 272, 144);
            _data.BitDouble(material.Double(145, 1.0));
            WriteMaterialMap(material, 146, 273, 9, 274, 275, 276, 147);
            _data.BitDouble(material.Double(148));
            _data.BitDouble(material.Double(149));
            _data.BitDouble(material.Double(468));
            _data.BitLong(material.Int(93));
            _data.BitLong(material.Int(94, 127));
            _data.BitLong(material.Int(282));
        }

        private void WriteMaterialColour(DwgGroups material, int way, int factor, int colour)
        {
            int method = material.Int(way);
            _data.RawChar(method);
            _data.BitDouble(material.Double(factor, 1.0));

            if (method == 1)
            {
                _data.BitLong(material.Int(colour));
            }
        }

        private void WriteMaterialMap(
            DwgGroups material,
            int blend,
            int source,
            int file,
            int projection,
            int tiling,
            int transform,
            int matrix
        )
        {
            _data.BitDouble(material.Double(blend, 1.0));
            _data.RawChar(material.Int(projection, 2));
            _data.RawChar(material.Int(tiling, 1));
            _data.RawChar(material.Int(transform, 1));
            List<string> values = material.Each(matrix);

            for (int i = 0; i < 16; i++)
            {
                _data.BitDouble(
                    values.Count == 16 ? DwgRecord.Double(values[i])
                    : i % 5 == 0 ? 1.0
                    : 0.0
                );
            }

            int from = material.Int(source, source == 273 ? 0 : 1);
            _data.RawChar(from);

            if (from == 1)
            {
                T(material.Text(file));
            }
        }

        /// <summary>
        /// A VISUALSTYLE, its 58 properties given after their count (70 58), each a value and how it is applied (176); of the
        /// form of R2010 the 28 it has and the 30 of its round trip record are put into that form first, as AutoCAD does when
        /// it saves the style as R2013 or later (<see cref="ListedProperties"/>); left out by <see cref="TypeOf"/> where the
        /// record is of another form.
        /// </summary>
        private void WriteVisualStyle()
        {
            DwgGroups style = _record.Of("AcDbVisualStyle");
            var groups = new List<KeyValuePair<int, string>>(style.All);
            DwgRecord roundTrip = RoundTripOfVisualStyle(_record);
            T(style.Text(2));
            _data.BitLong(style.Int(70));
            _data.BitShort(roundTrip != null ? 2 : style.Int(177, 3) - 1);
            _data.Bit(style.Int(291) != 0);
            int at = groups.FindIndex(g => g.Key == 70 && g.Value.Trim() == "58") + 1;

            if (roundTrip != null)
            {
                groups = ListedProperties(groups, roundTrip);
                at = 1;
            }

            for (; at < groups.Count; at++)
            {
                int code = groups[at].Key;
                string value = groups[at].Value;

                switch (code)
                {
                    case 90:
                        _data.BitLong(DwgRecord.Int(value));
                        break;
                    case 40:
                        _data.BitDouble(DwgRecord.Double(value));
                        break;
                    case 62:
                        int index = DwgRecord.Int(value);
                        bool rgb = at + 1 < groups.Count && groups[at + 1].Key == 420;
                        _data.BitShort(0);
                        _data.BitLong(
                            rgb ? unchecked((int)0xC2000000) | (DwgRecord.Int(groups[at + 1].Value) & 0xFFFFFF)
                            : index == 257 ? unchecked((int)0xC8000000)
                            : index == 256 ? unchecked((int)0xC0000000)
                            : index == 0 ? unchecked((int)0xC1000000)
                            : unchecked((int)0xC3000000) | (index & 0xFF)
                        );
                        _data.RawChar(0);
                        break;
                    case 290:
                        _data.Bit(DwgRecord.Int(value) != 0);
                        break;
                    case 1:
                        T(value);
                        break;
                    case 176:
                        _data.BitShort(DwgRecord.Int(value));
                        break;
                }
            }
        }

        /// <summary>Whether a VISUALSTYLE is of the form of R2013 and later, its properties after their count (70 58).</summary>
        private static bool IsListedVisualStyle(DwgRecord record) =>
            new List<KeyValuePair<int, string>>(record.Of("AcDbVisualStyle").All).Exists(g =>
                g.Key == 70 && g.Value.Trim() == "58"
            );

        /// <summary>
        /// The record of a VISUALSTYLE of the form of R2010 that carries the 30 properties R2013 added, the XRECORD
        /// <c>ACAD_XREC_ROUNDTRIP</c> of its extension dictionary, with their values and how each is applied
        /// (<c>RTVSPost2010Prop28</c> to <c>57</c> and their <c>Op</c>); null for a style of another form or with none.
        /// </summary>
        private DwgRecord RoundTripOfVisualStyle(DwgRecord style)
        {
            if (style.Type != "VISUALSTYLE" || IsListedVisualStyle(style) || style.XDictionary == 0)
            {
                return null;
            }

            DwgRecord dictionary = _drawing.Find(style.XDictionary);

            if (dictionary == null || dictionary.Type != "DICTIONARY")
            {
                return null;
            }

            DwgGroups entries = dictionary.Of("AcDbDictionary");
            int at = entries.Each(3).FindIndex(name => name.Trim() == "ACAD_XREC_ROUNDTRIP");
            DwgRecord record = at < 0 ? null : _drawing.Find(DwgRecord.Hex(entries.Each(360)[at]));
            return
                record != null
                && record.Type == "XRECORD"
                && record.Body.Exists(g => g.Value.Trim() == "RTVSPost2010Prop28")
                ? record
                : null;
        }

        /// <summary>
        /// Whether a record is what AutoCAD takes into the visual style when it saves it as R2013 or later: the extension
        /// dictionary of a style of R2010 and its round trip record, which hold the properties R2013 added.
        /// </summary>
        public bool IsRoundTripOfVisualStyle(DwgRecord record)
        {
            DwgRecord owner = _drawing.Find(record.Owner);

            if (record.Type == "XRECORD")
            {
                return owner != null && IsRoundTripOfVisualStyle(owner);
            }

            return record.Type == "DICTIONARY"
                && owner != null
                && owner.XDictionary == record.Handle
                && RoundTripOfVisualStyle(owner) != null;
        }

        /// <summary>
        /// The properties of a VISUALSTYLE of R2010 as the style of R2013 lists them: after their count (70 58) the 28 it has,
        /// each in the group of its kind and followed by how it is applied (176), then the 30 of its round trip record.
        /// </summary>
        private static List<KeyValuePair<int, string>> ListedProperties(
            List<KeyValuePair<int, string>> older,
            DwgRecord roundTrip
        )
        {
            var listed = new List<KeyValuePair<int, string>> { new KeyValuePair<int, string>(70, "58") };
            int[] codes = DwgObjectReader.VisualStyleCodes2010;
            bool applied = older.Exists(g => g.Key == 176);

            // The round trip record: each property after the name RTVSPost2010Prop<number>, in the group of its kind (a colour
            // as its index and its true colour, which is given where it is nought or more), and its Op after RTVSPost2010PropOp.
            var saved = new Dictionary<string, List<KeyValuePair<int, string>>>(StringComparer.Ordinal);
            List<KeyValuePair<int, string>> current = null;

            foreach (KeyValuePair<int, string> group in roundTrip.Of("AcDbXrecord").All)
            {
                if (group.Key == 102)
                {
                    current = new List<KeyValuePair<int, string>>();
                    saved[group.Value.Trim()] = current;
                }
                else if (current != null)
                {
                    current.Add(group);
                }
            }

            for (int i = 0; i < codes.Length; i++)
            {
                int at = older.FindIndex(g => g.Key == codes[i]);
                int next = at + 1;
                char kind = DwgObjectReader.VisualStyleKinds[i];
                string value = at < 0 ? "0" : older[at].Value;

                // A style of R2007 and before gives the opacity and the specular gloss it does not apply (40 and 41) as negative.
                if (
                    (codes[i] == 40 || codes[i] == 41)
                    && !applied
                    && value.TrimStart().StartsWith("-", StringComparison.Ordinal)
                )
                {
                    value = value.Trim().Substring(1);
                }

                if (kind == '6')
                {
                    listed.Add(new KeyValuePair<int, string>(62, value));

                    if (at >= 0 && next < older.Count && older[next].Key == codes[i] + 358)
                    {
                        listed.Add(new KeyValuePair<int, string>(420, older[next].Value));
                        next++;
                    }
                }
                else
                {
                    listed.Add(
                        new KeyValuePair<int, string>(
                            kind == '9' ? 90
                                : kind == '4' ? 40
                                : kind == '2' ? 290
                                : 1,
                            value
                        )
                    );
                }

                // How it is applied: after the property in a style of R2010, else in the round trip record as RTVSPropertyOp<i>.
                listed.Add(
                    new KeyValuePair<int, string>(
                        176,
                        applied
                            ? next < older.Count && older[next].Key == 176
                                ? older[next].Value
                                : "1"
                            : Saved(
                                saved,
                                "RTVSPropertyOp" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                70,
                                "1"
                            )
                    )
                );
            }

            for (int i = codes.Length; i < DwgObjectReader.VisualStyleKinds.Length; i++)
            {
                string number = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string name = "RTVSPost2010Prop" + number;
                char kind = DwgObjectReader.VisualStyleKinds[i];

                if (kind == '6')
                {
                    listed.Add(new KeyValuePair<int, string>(62, Saved(saved, name + "ColorIndex", 90, "256")));
                    string rgb = Saved(saved, name + "ColorRGB", 90, "-1");

                    if (DwgRecord.Int(rgb) >= 0)
                    {
                        listed.Add(new KeyValuePair<int, string>(420, rgb));
                    }
                }
                else
                {
                    listed.Add(
                        kind == '9' ? new KeyValuePair<int, string>(90, Saved(saved, name, 90, "0"))
                        : kind == '4' ? new KeyValuePair<int, string>(40, Saved(saved, name, 140, "0"))
                        : kind == '2' ? new KeyValuePair<int, string>(290, Saved(saved, name, 280, "0"))
                        : new KeyValuePair<int, string>(1, Saved(saved, name, 1, string.Empty))
                    );
                }

                listed.Add(new KeyValuePair<int, string>(176, Saved(saved, "RTVSPost2010PropOp" + number, 70, "1")));
            }

            return listed;
        }

        /// <summary>The first value of <paramref name="code"/> among the groups saved under <paramref name="name"/>; <paramref name="otherwise"/> where there is none.</summary>
        private static string Saved(
            Dictionary<string, List<KeyValuePair<int, string>>> saved,
            string name,
            int code,
            string otherwise
        )
        {
            return saved.TryGetValue(name, out List<KeyValuePair<int, string>> groups)
                ? groups.Find(g => g.Key == code).Value ?? otherwise
                : otherwise;
        }

        private void WriteGroup()
        {
            DwgGroups group = _record.Of("AcDbGroup");
            T(group.Text(300));
            _data.BitShort(group.Int(70));
            _data.BitShort(group.Int(71, 1));
            List<ulong> members = group
                .Each(340)
                .ConvertAll(DwgRecord.Hex)
                .FindAll(handle => _written.Contains(handle));
            _data.BitLong(members.Count);

            foreach (ulong member in members)
            {
                H(5, member);
            }
        }

        private void WriteMLineStyle()
        {
            DwgGroups style = _record.Of("AcDbMlineStyle");
            int dxf = style.Int(70);
            int flags =
                ((dxf & 2) != 0 ? 1 : 0)
                | ((dxf & 1) != 0 ? 2 : 0)
                | (dxf & 16)
                | ((dxf & 64) != 0 ? 32 : 0)
                | ((dxf & 32) != 0 ? 64 : 0)
                | (dxf & 256)
                | ((dxf & 1024) != 0 ? 512 : 0)
                | ((dxf & 512) != 0 ? 1024 : 0);
            T(style.Text(2));
            T(style.Text(3));
            _data.BitShort(flags);

            // The groups before the first element: the fill colour, the angles, the count; then each element from its 49.
            var head = new List<KeyValuePair<int, string>>();
            var elements = new List<List<KeyValuePair<int, string>>>();

            foreach (KeyValuePair<int, string> group in style.All)
            {
                if (group.Key == 49)
                {
                    elements.Add(new List<KeyValuePair<int, string>>());
                }

                (elements.Count > 0 ? elements[elements.Count - 1] : head).Add(group);
            }

            DwgGroups top = new DwgGroups(head, null);
            WriteColour(top.Int(62, 256), top.Has(420) ? top.Int(420) : -1);
            _data.BitDouble(Radians(top.Double(51, 90.0)));
            _data.BitDouble(Radians(top.Double(52, 90.0)));
            _data.RawChar(elements.Count);

            foreach (List<KeyValuePair<int, string>> element in elements)
            {
                var one = new DwgGroups(element, null);
                _data.BitDouble(one.Double(49));
                WriteColour(one.Int(62, 256), one.Has(420) ? one.Int(420) : -1);
                string linetype = one.Text(6, "BYLAYER").Trim();

                if (R2018Plus)
                {
                    H(5, _drawing.HandleOf("LTYPE", linetype));
                }
                else if (
                    linetype.Equals("BYLAYER", StringComparison.OrdinalIgnoreCase)
                    || linetype.Equals("BYBLOCK", StringComparison.OrdinalIgnoreCase)
                )
                {
                    _data.BitShort(linetype.Equals("BYBLOCK", StringComparison.OrdinalIgnoreCase) ? 32766 : 32767);
                }
                else
                {
                    // Before R2018 a linetype is its place among the records of the table after ByBlock and ByLayer.
                    DwgRecord table = _drawing.Table("LTYPE");
                    int index = 0;
                    int place = 0;

                    foreach (DwgRecord record in table?.Records ?? new List<DwgRecord>())
                    {
                        string recordName = record.Of(null).Text(2).Trim();

                        if (
                            recordName.Equals("ByBlock", StringComparison.OrdinalIgnoreCase)
                            || recordName.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            continue;
                        }

                        if (recordName.Equals(linetype, StringComparison.OrdinalIgnoreCase))
                        {
                            place = index;
                        }

                        index++;
                    }

                    _data.BitShort(place);
                }
            }
        }

        /// <summary>
        /// The data of an XRECORD: its groups one after another, each its code as a short and a value whose form the code
        /// decides, the way a binary DXF file writes them; then the cloning flag.
        /// </summary>
        private void WriteXRecord()
        {
            List<KeyValuePair<int, string>> body = _record.Body;
            int start = body.FindIndex(group => group.Key == 100 && group.Value.Trim() == "AcDbXrecord");
            int cloning = 1;
            start = start < 0 ? 0 : start + 1;

            if (start < body.Count && body[start].Key == 280)
            {
                cloning = DwgRecord.Int(body[start].Value);
                start++;
            }

            var values = new System.IO.MemoryStream();

            for (int i = start; i < body.Count; i++)
            {
                int code = body[i].Key;
                string value = body[i].Value;
                var bytes = new List<byte> { (byte)code, (byte)(code >> 8) };

                if ((code >= 310 && code <= 319) || code == 1004)
                {
                    byte[] chunk = Unhex(value);
                    bytes.Add((byte)chunk.Length);
                    bytes.AddRange(chunk);
                }
                else if (
                    (code >= 0 && code <= 9)
                    || (code >= 100 && code <= 105)
                    || (code >= 300 && code <= 309)
                    || (code >= 410 && code <= 419)
                    || (code >= 430 && code <= 439)
                    || (code >= 470 && code <= 479)
                    || (code >= 1000 && code <= 1003)
                )
                {
                    string text = value;

                    if (R2007Plus)
                    {
                        bytes.Add((byte)text.Length);
                        bytes.Add((byte)(text.Length >> 8));
                        bytes.AddRange(Encoding.Unicode.GetBytes(text));
                    }
                    else
                    {
                        byte[] encoded = Encoding.UTF8.GetBytes(text);
                        bytes.Add((byte)encoded.Length);
                        bytes.Add((byte)(encoded.Length >> 8));
                        bytes.Add(30);
                        bytes.AddRange(encoded);
                    }
                }
                else if (code >= 10 && code <= 39)
                {
                    double x = DwgRecord.Double(value);
                    double y =
                        i + 1 < body.Count && body[i + 1].Key == code + 10 ? DwgRecord.Double(body[++i].Value) : 0.0;
                    double z =
                        i + 1 < body.Count && body[i + 1].Key == code + 20 ? DwgRecord.Double(body[++i].Value) : 0.0;
                    bytes.AddRange(BitConverter.GetBytes(x));
                    bytes.AddRange(BitConverter.GetBytes(y));
                    bytes.AddRange(BitConverter.GetBytes(z));
                }
                else if (
                    (code >= 40 && code <= 59)
                    || (code >= 110 && code <= 149)
                    || (code >= 210 && code <= 239)
                    || (code >= 460 && code <= 469)
                    || (code >= 1010 && code <= 1059)
                )
                {
                    bytes.AddRange(BitConverter.GetBytes(DwgRecord.Double(value)));
                }
                else if (
                    (code >= 60 && code <= 79)
                    || (code >= 170 && code <= 179)
                    || (code >= 270 && code <= 279)
                    || (code >= 370 && code <= 389)
                    || (code >= 400 && code <= 409)
                    || (code >= 1060 && code <= 1070)
                )
                {
                    bytes.AddRange(BitConverter.GetBytes((short)DwgRecord.Int(value)));
                }
                else if (
                    (code >= 90 && code <= 99)
                    || (code >= 420 && code <= 429)
                    || (code >= 440 && code <= 459)
                    || code == 1071
                )
                {
                    bytes.AddRange(BitConverter.GetBytes(DwgRecord.Int(value)));
                }
                else if (code >= 160 && code <= 169)
                {
                    long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long wide);
                    bytes.AddRange(BitConverter.GetBytes(wide));
                }
                else if (code >= 280 && code <= 299)
                {
                    bytes.Add((byte)DwgRecord.Int(value));
                }
                else if ((code >= 320 && code <= 369) || (code >= 390 && code <= 399) || (code >= 1005 && code <= 1009))
                {
                    ulong handle = DwgRecord.Hex(value);
                    bytes.AddRange(BitConverter.GetBytes(_written.Contains(handle) ? handle : 0));
                }
                else
                {
                    continue;
                }

                values.Write(bytes.ToArray(), 0, bytes.Count);
            }

            byte[] data = values.ToArray();
            _data.BitLong(data.Length);
            _data.Bytes(data);
            _data.BitShort(cloning);
        }

        /// <summary>Writes a PLOTSETTINGS, a named page setup: the page setup part of a LAYOUT alone.</summary>
        private void WritePlotSettings() => WritePlotPart(_record.Of("AcDbPlotSettings"));

        /// <summary>Writes the part of a LAYOUT or a PLOTSETTINGS that is the page setup, its data and its handles.</summary>
        private void WritePlotPart(DwgGroups plot)
        {
            T(plot.Text(1));
            T(plot.Text(2));
            _data.BitShort(plot.Int(70));

            for (int i = 0; i < 4; i++)
            {
                _data.BitDouble(plot.Double(40 + i));
            }

            _data.BitDouble(plot.Double(44));
            _data.BitDouble(plot.Double(45));
            T(plot.Text(4));
            _data.BitDouble(plot.Double(46));
            _data.BitDouble(plot.Double(47));
            _data.BitShort(plot.Int(72));
            _data.BitShort(plot.Int(73));
            _data.BitShort(plot.Int(74));
            _data.BitDouble(plot.Double(48));
            _data.BitDouble(plot.Double(49));
            _data.BitDouble(plot.Double(140));
            _data.BitDouble(plot.Double(141));

            if (!R2004Plus)
            {
                T(plot.Text(6));
            }

            _data.BitDouble(plot.Double(142, 1.0));
            _data.BitDouble(plot.Double(143, 1.0));
            T(plot.Text(7));
            _data.BitShort(plot.Int(75));
            _data.BitDouble(plot.Double(147, 1.0));
            _data.BitDouble(plot.Double(148));
            _data.BitDouble(plot.Double(149));

            if (R2004Plus)
            {
                _data.BitShort(plot.Int(76));
                _data.BitShort(plot.Int(77, 2));
                _data.BitShort(plot.Int(78, 300));
                H(5, _drawing.HandleOf("VIEW", plot.Text(6)));
            }

            if (R2007Plus)
            {
                H(4, 0);
            }
        }

        private void WriteLayout()
        {
            DwgGroups layout = _record.Of("AcDbLayout");
            WritePlotPart(_record.Of("AcDbPlotSettings"));
            T(layout.Text(1));
            _data.BitLong(layout.Int(71));
            _data.BitShort(layout.Int(70));
            _data.Point3(layout.Point(13));
            _data.RawPoint2(layout.Point(10));
            _data.RawPoint2(layout.Point(11));
            _data.Point3(layout.Point(12));
            _data.Point3(layout.Point(16, 1));
            _data.Point3(layout.Point(17, 0, 1));
            _data.BitDouble(layout.Double(146));
            _data.BitShort(layout.Int(76));
            _data.Point3(layout.Point(14));
            _data.Point3(layout.Point(15));
            ulong block = layout.Handle(330);
            // A layout lists its viewports once one of them has been active (331); AutoCAD's layouts that never were list none.
            List<DwgRecord> viewports = R2004Plus && layout.Has(331) ? ViewportsOf(block) : new List<DwgRecord>();

            if (R2004Plus)
            {
                _data.BitLong(viewports.Count);
            }

            H(4, block);
            H(4, layout.Handle(331));

            if (R2004Plus)
            {
                H(5, 0);
                H(5, 0);

                foreach (DwgRecord viewport in viewports)
                {
                    H(4, viewport.Handle);
                }
            }
        }

        /// <summary>The written viewports a layout's block owns, by their number (group 69), then by handle.</summary>
        private List<DwgRecord> ViewportsOf(ulong block)
        {
            List<DwgRecord> viewports = OwnedBy(block).FindAll(entity => entity.Type == "VIEWPORT");
            viewports.Sort(
                (a, b) =>
                {
                    int byNumber = a.Of("AcDbViewport").Int(69).CompareTo(b.Of("AcDbViewport").Int(69));
                    return byNumber != 0 ? byNumber : a.Handle.CompareTo(b.Handle);
                }
            );
            return viewports;
        }
    }
}
