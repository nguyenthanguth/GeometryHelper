using System;
using System.Collections.Generic;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dwg
{
    /// <content>The objects of fields and of the draw order: SORTENTSTABLE, FIELDLIST and FIELD.</content>
    internal sealed partial class DwgObjectWriter
    {
        /// <summary>
        /// A SORTENTSTABLE from its DXF: the block, then each entity (331) followed by its sort handle (5). The sort handles
        /// come first, in the data, and the block and the entities in the handles; an entity that is not written is left out
        /// with its sort handle.
        /// </summary>
        private void WriteSortEntitiesTable()
        {
            DwgGroups table = _record.Of("AcDbSortentsTable");
            var entities = new List<ulong>();
            var sorts = new List<ulong>();
            ulong entity = 0;
            bool named = false;

            foreach (KeyValuePair<int, string> group in table.All)
            {
                if (group.Key == 331)
                {
                    entity = DwgRecord.Hex(group.Value);
                    named = true;
                }
                else if (group.Key == 5 && named)
                {
                    named = false;

                    if (_written.Contains(entity))
                    {
                        entities.Add(entity);
                        sorts.Add(DwgRecord.Hex(group.Value));
                    }
                }
            }

            _data.BitLong(entities.Count);

            foreach (ulong sort in sorts)
            {
                _data.Handle(0, sort);
            }

            H(4, table.Handle(330));

            foreach (ulong item in entities)
            {
                H(4, item);
            }
        }

        /// <summary>A FIELDLIST from its DXF: the count of its fields, a flag AutoCAD leaves clear and each field as a soft pointer.</summary>
        private void WriteFieldList()
        {
            List<ulong> fields = _record
                .Of("AcDbIdSet")
                .Each(330)
                .ConvertAll(DwgRecord.Hex)
                .FindAll(handle => _written.Contains(handle));
            _data.BitLong(fields.Count);
            _data.Bit(false);

            foreach (ulong field in fields)
            {
                H(4, field);
            }
        }

        /// <summary>
        /// A FIELD from its DXF, written as R2007 and later write it: its evaluator and code (the pieces under 3 first),
        /// the fields it holds and the objects it names, its options, state and error, the values it keeps by name and its
        /// own value, which DXF gives last. A DXF of AutoCAD 2004 and before gives the format apart and the values without
        /// flags, unit and text; they are put into the form of R2007, as AutoCAD does where it saves such a drawing as 2018.
        /// </summary>
        private void WriteField()
        {
            var groups = new List<KeyValuePair<int, string>>(_record.Of("AcDbField").All);
            int at = 0;
            string evaluator = Take(groups, ref at, 1);
            var code = new StringBuilder();

            while (at < groups.Count && groups[at].Key == 3)
            {
                code.Append(groups[at++].Value);
            }

            code.Append(Take(groups, ref at, 2));
            var children = new List<ulong>();
            var objects = new List<ulong>();
            int count = DwgRecord.Int(Take(groups, ref at, 90, "0"));

            for (int i = 0; i < count && at < groups.Count && groups[at].Key == 360; i++)
            {
                children.Add(DwgRecord.Hex(groups[at++].Value));
            }

            count = DwgRecord.Int(Take(groups, ref at, 97, "0"));

            for (int i = 0; i < count && at < groups.Count && groups[at].Key == 331; i++)
            {
                objects.Add(DwgRecord.Hex(groups[at++].Value));
            }

            string format = Take(groups, ref at, 4);
            int evaluation = DwgRecord.Int(Take(groups, ref at, 91, "0"));
            int filing = DwgRecord.Int(Take(groups, ref at, 92, "0"));
            int state = DwgRecord.Int(Take(groups, ref at, 94, "0"));
            int status = DwgRecord.Int(Take(groups, ref at, 95, "0"));
            int error = DwgRecord.Int(Take(groups, ref at, 96, "0"));
            string message = Take(groups, ref at, 300);
            int named = DwgRecord.Int(Take(groups, ref at, 93, "0"));
            var values = new List<KeyValuePair<string, DwgCellValue>>();

            for (int i = 0; i < named && at < groups.Count && groups[at].Key == 6; i++)
            {
                string key = groups[at++].Value;
                values.Add(new KeyValuePair<string, DwgCellValue>(key, TakeFieldValue(groups, ref at, false)));
            }

            Take(groups, ref at, 7);
            DwgCellValue own = TakeFieldValue(groups, ref at, true);
            string shown = Take(groups, ref at, 301);
            int length = DwgRecord.Int(Take(groups, ref at, 98, shown.Length.ToString()));

            if (own.Old)
            {
                // The format and the text the value shows belong to the value from R2007.
                own.Format = format;
                own.Shown = shown;
            }

            children = children.FindAll(handle => _written.Contains(handle));
            objects = objects.FindAll(handle => _written.Contains(handle));
            T(evaluator);
            T(code.ToString());
            _data.BitLong(children.Count);

            foreach (ulong child in children)
            {
                H(3, child);
            }

            _data.BitLong(objects.Count);

            foreach (ulong item in objects)
            {
                H(4, item);
            }

            _data.BitLong(evaluation);
            _data.BitLong(filing);
            _data.BitLong(state);
            _data.BitLong(status);
            _data.BitLong(error);
            T(message);
            WriteFieldValue(own);
            T(shown);
            _data.BitLong(length);
            _data.BitLong(values.Count);

            foreach (KeyValuePair<string, DwgCellValue> value in values)
            {
                T(value.Key);
                WriteFieldValue(value.Value);
            }
        }

        /// <summary>
        /// A DATALINK from its DXF: its adapter, description, tooltip and connection, its options (with the bit that says the
        /// custom data follow set where the map holds a value), its three whole numbers, the eight numbers of its last update,
        /// the objects it fills that are written, the hard owner (360) and the custom data, a map from names to values.
        /// </summary>
        private void WriteDataLink()
        {
            var groups = new List<KeyValuePair<int, string>>(_record.Of("AcDbDataLink").All);
            int at = 0;
            string adapter = Take(groups, ref at, 1);
            string description = Take(groups, ref at, 300);
            string tooltip = Take(groups, ref at, 301);
            string connection = Take(groups, ref at, 302);
            int options = DwgRecord.Int(Take(groups, ref at, 90, "0"));
            int update = DwgRecord.Int(Take(groups, ref at, 91, "0"));
            int status = DwgRecord.Int(Take(groups, ref at, 92, "0"));
            var time = new int[8];

            for (int i = 0; i < time.Length; i++)
            {
                time[i] = DwgRecord.Int(Take(groups, ref at, 170 + i, "0"));
            }

            int flags = DwgRecord.Int(Take(groups, ref at, 93, "0"));
            string text = Take(groups, ref at, 304);
            int count = DwgRecord.Int(Take(groups, ref at, 94, "0"));
            var targets = new List<ulong>();

            for (int i = 0; i < count && at < groups.Count && groups[at].Key == 330; i++)
            {
                targets.Add(DwgRecord.Hex(groups[at++].Value));
            }

            ulong owner = DwgRecord.Hex(Take(groups, ref at, 360, "0"));
            Take(groups, ref at, 305);
            Take(groups, ref at, 1);
            int items = DwgRecord.Int(Take(groups, ref at, 90, "0"));
            var values = new List<KeyValuePair<string, DwgCellValue>>();

            for (int i = 0; i < items && at < groups.Count && groups[at].Key == 300; i++)
            {
                string key = groups[at++].Value;
                Take(groups, ref at, 301);
                values.Add(new KeyValuePair<string, DwgCellValue>(key, TakeFieldValue(groups, ref at, false)));
            }

            targets = targets.FindAll(handle => _written.Contains(handle));
            targets.Sort();
            T(adapter);
            T(description);
            T(tooltip);
            T(connection);
            _data.BitLong(
                values.Count > 0 ? options | DwgObjectReader.CustomDataFlag : options & ~DwgObjectReader.CustomDataFlag
            );
            _data.BitLong(update);
            _data.BitLong(status);

            foreach (int part in time)
            {
                _data.BitShort(part);
            }

            _data.BitLong(flags);
            T(text);
            _data.BitLong(targets.Count);

            foreach (ulong target in targets)
            {
                H(4, target);
            }

            H(3, owner);

            if (values.Count > 0)
            {
                _data.BitLong(values.Count);

                foreach (KeyValuePair<string, DwgCellValue> value in values)
                {
                    T(value.Key);
                    WriteFieldValue(value.Value);
                }
            }
        }

        /// <summary>The value of the next group where it is <paramref name="code"/>, taken, with the caret form of a control character read as the character; <paramref name="otherwise"/> where it is another.</summary>
        private static string Take(List<KeyValuePair<int, string>> groups, ref int at, int code, string otherwise = "")
        {
            if (at < groups.Count && groups[at].Key == code)
            {
                return DxfReader.Carets(groups[at++].Value);
            }

            return otherwise;
        }

        /// <summary>
        /// A value of a field from its groups: from R2007 its flags (93), its type (90), the value, its unit (94), format (300),
        /// text (302) and 304; before R2007 the type and the value. A value of the old form is given flags as AutoCAD gives
        /// one it brings up to R2007: 2 for a value kept by name, 3 for an own value that holds nothing and 4 for one that
        /// holds a value.
        /// </summary>
        private static DwgCellValue TakeFieldValue(List<KeyValuePair<int, string>> groups, ref int at, bool own)
        {
            var value = new DwgCellValue();
            value.Old = !(at < groups.Count && groups[at].Key == 93);

            if (!value.Old)
            {
                value.Flags = DwgRecord.Int(groups[at++].Value);
            }

            value.Type = DwgRecord.Int(Take(groups, ref at, 90, "0"));

            switch (value.Type)
            {
                case 0:
                case 1:
                    value.Long = DwgRecord.Int(Take(groups, ref at, 91, "0"));
                    break;
                case 2:
                    value.Double = DwgRecord.Double(Take(groups, ref at, 140, "0"));
                    break;
                case 8:
                    Take(groups, ref at, 92, "0");
                    value.Bytes = Unhex(Take(groups, ref at, 310));
                    break;
                default:
                    value.Text = Take(groups, ref at, 1);
                    break;
            }

            if (value.Old)
            {
                value.Flags =
                    !own ? 2
                    : value.Type == 0 && value.Long == 0 ? 3
                    : 4;
            }
            else
            {
                value.Unit = DwgRecord.Int(Take(groups, ref at, 94, "0"));
                value.Format = Take(groups, ref at, 300);
                value.Shown = Take(groups, ref at, 302);
                Take(groups, ref at, 304);
            }

            return value;
        }

        /// <summary>A value of a field (20.4.99): its flags, its type and, unless flag 1 says it holds nothing, the value; its unit, format and text.</summary>
        private void WriteFieldValue(DwgCellValue value)
        {
            _data.BitLong(value.Flags);
            _data.BitLong(value.Type);

            if ((value.Flags & 1) == 0)
            {
                switch (value.Type)
                {
                    case 0:
                    case 1:
                        _data.BitLong(value.Long);
                        break;
                    case 2:
                        _data.BitDouble(value.Double);
                        break;
                    case 8:
                        _data.BitLong(value.Bytes.Length);
                        _data.Bytes(value.Bytes);
                        break;
                    default:
                        // A string as UTF-16 with its closing zero, counted in bytes.
                        byte[] bytes = Encoding.Unicode.GetBytes(value.Text + "\0");
                        _data.BitLong(bytes.Length);
                        _data.Bytes(bytes);
                        break;
                }
            }

            _data.BitLong(value.Unit);
            T(value.Format);
            T(value.Shown);
        }
    }
}
