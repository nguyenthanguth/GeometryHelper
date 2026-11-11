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

        private void WriteGroup()
        {
            DwgGroups group = _record.Of("AcDbGroup");
            T(group.Text(300));
            _data.BitShort(group.Int(70));
            _data.BitShort(group.Int(71, 1));
            List<ulong> members = group.Each(340).ConvertAll(DwgRecord.Hex).FindAll(handle => _written.Contains(handle));
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
            int flags = ((dxf & 2) != 0 ? 1 : 0) | ((dxf & 1) != 0 ? 2 : 0) | (dxf & 16) | ((dxf & 64) != 0 ? 32 : 0)
                | ((dxf & 32) != 0 ? 64 : 0) | (dxf & 256) | ((dxf & 1024) != 0 ? 512 : 0) | ((dxf & 512) != 0 ? 1024 : 0);
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
                else if (linetype.Equals("BYLAYER", StringComparison.OrdinalIgnoreCase) || linetype.Equals("BYBLOCK", StringComparison.OrdinalIgnoreCase))
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

                        if (recordName.Equals("ByBlock", StringComparison.OrdinalIgnoreCase) || recordName.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
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
                else if ((code >= 0 && code <= 9) || (code >= 100 && code <= 105) || (code >= 300 && code <= 309) || (code >= 410 && code <= 419)
                    || (code >= 430 && code <= 439) || (code >= 470 && code <= 479) || (code >= 1000 && code <= 1003))
                {
                    string text = Unescape(value);

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
                    double y = i + 1 < body.Count && body[i + 1].Key == code + 10 ? DwgRecord.Double(body[++i].Value) : 0.0;
                    double z = i + 1 < body.Count && body[i + 1].Key == code + 20 ? DwgRecord.Double(body[++i].Value) : 0.0;
                    bytes.AddRange(BitConverter.GetBytes(x));
                    bytes.AddRange(BitConverter.GetBytes(y));
                    bytes.AddRange(BitConverter.GetBytes(z));
                }
                else if ((code >= 40 && code <= 59) || (code >= 110 && code <= 149) || (code >= 210 && code <= 239) || (code >= 460 && code <= 469)
                    || (code >= 1010 && code <= 1059))
                {
                    bytes.AddRange(BitConverter.GetBytes(DwgRecord.Double(value)));
                }
                else if ((code >= 60 && code <= 79) || (code >= 170 && code <= 179) || (code >= 270 && code <= 279) || (code >= 370 && code <= 389)
                    || (code >= 400 && code <= 409) || (code >= 1060 && code <= 1070))
                {
                    bytes.AddRange(BitConverter.GetBytes((short)DwgRecord.Int(value)));
                }
                else if ((code >= 90 && code <= 99) || (code >= 420 && code <= 429) || (code >= 440 && code <= 459) || code == 1071)
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

        private void WriteLayout()
        {
            DwgGroups plot = _record.Of("AcDbPlotSettings");
            DwgGroups layout = _record.Of("AcDbLayout");
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
            List<DwgRecord> viewports = R2004Plus ? ViewportsOf(block) : new List<DwgRecord>();

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
            viewports.Sort((a, b) =>
            {
                int byNumber = a.Of("AcDbViewport").Int(69).CompareTo(b.Of("AcDbViewport").Int(69));
                return byNumber != 0 ? byNumber : a.Handle.CompareTo(b.Handle);
            });
            return viewports;
        }
    }
}
