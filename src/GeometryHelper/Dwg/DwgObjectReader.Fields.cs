using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <content>The objects of fields and of the draw order: SORTENTSTABLE, FIELDLIST and FIELD.</content>
    internal sealed partial class DwgObjectReader
    {
        /// <summary>
        /// A SORTENTSTABLE, the draw order of a block: its count, then for each entry the sort handle in the data (the handle
        /// the entity is sorted by, which need not name an entity) and the entity in the handles, after the block's own
        /// handle. DXF gives the block, then each entity (331) followed by its sort handle (5).
        /// </summary>
        private void ReadSortEntitiesTable()
        {
            int count = Count(_data.BitLong());
            _object.Add(100, "AcDbSortentsTable");
            _object.Add(330, H());

            for (int i = 0; i < count; i++)
            {
                ulong sort = _data.Handle(_object.Handle);
                _object.Add(331, H());
                _object.Add(5, sort);
            }
        }

        /// <summary>A FIELDLIST, the fields of the drawing: a count, a flag DXF does not give, then each field as a soft pointer.</summary>
        private void ReadFieldList()
        {
            int count = Count(_data.BitLong());
            _data.Bit();
            _object.Add(100, "AcDbIdSet");
            _object.Add(90, count);

            for (int i = 0; i < count; i++)
            {
                _object.Add(330, H());
            }

            _object.Add(100, "AcDbFieldList");
        }

        /// <summary>
        /// A FIELD: its evaluator and its code (DXF breaks a code over 250 characters into pieces under group 3, the rest
        /// under 2), the fields it holds and the objects it names, the format before R2007, its options and state, the error
        /// it ended in, its value, the text it shows and the values it keeps by name. DXF gives the values by name first,
        /// then the value of the field itself as <c>ACFD_FIELD_VALUE</c>, then the text and its length.
        /// </summary>
        private void ReadField()
        {
            _object.Add(100, "AcDbField");
            _object.Add(1, T());
            string code = Encoding.UTF8.GetString(T());

            while (code.Length > 250)
            {
                _object.Add(3, Encoding.UTF8.GetBytes(code.Substring(0, 250)));
                code = code.Substring(250);
            }

            _object.Add(2, Encoding.UTF8.GetBytes(code));
            int children = Count(_data.BitLong());
            _object.Add(90, children);

            for (int i = 0; i < children; i++)
            {
                _object.Add(360, H());
            }

            int objects = Count(_data.BitLong());
            _object.Add(97, objects);

            for (int i = 0; i < objects; i++)
            {
                _object.Add(331, H());
            }

            if (!R2007Plus)
            {
                _object.Add(4, T());
            }

            _object.Add(91, _data.BitLong());
            _object.Add(92, _data.BitLong());
            _object.Add(94, _data.BitLong());
            _object.Add(95, _data.BitLong());
            _object.Add(96, _data.BitLong());
            _object.Add(300, T());
            DwgCellValue own = ReadFieldValue();
            byte[] shown = T();
            int length = _data.BitLong();
            int named = Count(_data.BitLong());
            _object.Add(93, named);
            var values = new List<KeyValuePair<byte[], DwgCellValue>>(named);

            for (int i = 0; i < named; i++)
            {
                values.Add(new KeyValuePair<byte[], DwgCellValue>(T(), ReadFieldValue()));
            }

            // DXF gives the values by name in the order of their names, whichever order the file kept them in.
            var order = new int[named];

            for (int i = 0; i < named; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int result = CompareBytes(values[a].Key, values[b].Key);
                return result != 0 ? result : a.CompareTo(b);
            });

            foreach (int i in order)
            {
                _object.Add(6, values[i].Key);
                AddFieldValue(values[i].Value);
            }

            _object.Add(7, "ACFD_FIELD_VALUE");
            AddFieldValue(own);
            _object.Add(301, shown);
            _object.Add(98, length);
        }

        /// <summary>Two texts compared byte by byte, the shorter first where one begins the other.</summary>
        private static int CompareBytes(byte[] a, byte[] b)
        {
            int shared = Math.Min(a.Length, b.Length);

            for (int i = 0; i < shared; i++)
            {
                if (a[i] != b[i])
                {
                    return a[i] < b[i] ? -1 : 1;
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        /// <summary>
        /// A value of a field (20.4.99): from R2007 its flags (1 where it holds nothing), its type, the value, its unit, its
        /// format and the text it shows; before R2007 the type and the value alone. A date is kept as its bytes.
        /// </summary>
        private DwgCellValue ReadFieldValue()
        {
            var value = new DwgCellValue { Flags = R2007Plus ? _data.BitLong() : 0, Type = 0 };
            value.Type = _data.BitLong();

            if ((value.Flags & 1) == 0)
            {
                switch (value.Type)
                {
                    case 0:
                    case 1:
                        value.Long = _data.BitLong();
                        break;
                    case 2:
                        value.Double = _data.BitDouble();
                        break;
                    case 4:
                    case 512:
                        int size = Count(_data.BitLong());
                        byte[] bytes = _data.Bytes(size);
                        value.Text = R2007Plus
                            ? Encoding.Unicode.GetString(bytes, 0, Math.Max(0, size - 2))
                            : Encoding.UTF8.GetString(bytes, 0, Math.Max(0, size - 1));
                        break;
                    case 8:
                        value.Bytes = _data.Bytes(Count(_data.BitLong()));
                        break;
                    default:
                        throw new FormatException(
                            "The field has a value of the kind " + value.Type + ", which is not read."
                        );
                }
            }

            if (R2007Plus)
            {
                value.Unit = _data.BitLong();
                value.Format = Text();
                value.Shown = Text();
            }

            return value;
        }

        /// <summary>The bit of the options of a DATALINK that says its custom data follow; DXF leaves it out of group 90.</summary>
        internal const int CustomDataFlag = 8;

        /// <summary>
        /// A DATALINK, a link of a table to data outside the drawing: its adapter, description, tooltip and connection, its
        /// options, the time of its last update, the objects it fills, and its custom data, a map from names to values that
        /// the file holds only where the options say so (DXF writes the map, empty, either way).
        /// </summary>
        private void ReadDataLink()
        {
            _object.Add(100, "AcDbDataLink");
            _object.Add(1, T());
            _object.Add(300, T());
            _object.Add(301, T());
            _object.Add(302, T());
            int options = _data.BitLong();
            _object.Add(90, options & ~CustomDataFlag);
            _object.Add(91, _data.BitLong());
            _object.Add(92, _data.BitLong());

            for (int code = 170; code <= 177; code++)
            {
                _object.Add(code, _data.BitShort());
            }

            _object.Add(93, _data.BitLong());
            _object.Add(304, T());
            int targets = Count(_data.BitLong());
            _object.Add(94, targets);

            for (int i = 0; i < targets; i++)
            {
                _object.Add(330, H());
            }

            _object.Add(360, H());
            _object.Add(305, "CUSTOMDATA");
            _object.Add(1, "DATAMAP_BEGIN");
            int items = (options & CustomDataFlag) == 0 ? 0 : Count(_data.BitLong());
            _object.Add(90, items);

            for (int i = 0; i < items; i++)
            {
                _object.Add(300, T());
                _object.Add(301, "DATAMAP_VALUE");
                AddFieldValue(ReadFieldValue());
            }

            _object.Add(309, "DATAMAP_END");
        }

        /// <summary>A value of a field in DXF: its flags (from R2007), type, the value, and from R2007 its unit, format, text and the closing 304.</summary>
        private void AddFieldValue(DwgCellValue value)
        {
            if (R2007Plus)
            {
                _object.Add(93, value.Flags);
            }

            _object.Add(90, value.Type);

            if ((value.Flags & 1) == 0)
            {
                switch (value.Type)
                {
                    case 0:
                    case 1:
                        _object.Add(91, value.Long);
                        break;
                    case 2:
                        _object.Add(140, value.Double);
                        break;
                    case 8:
                        _object.Add(92, value.Bytes.Length);
                        _object.Add(310, Hex(value.Bytes));
                        break;
                    default:
                        _object.Add(1, Encoding.UTF8.GetBytes(value.Text));
                        break;
                }
            }

            if (R2007Plus)
            {
                _object.Add(94, value.Unit);
                _object.Add(300, Encoding.UTF8.GetBytes(value.Format));
                _object.Add(302, Encoding.UTF8.GetBytes(value.Shown));
                _object.Add(304, "ACVALUE_END");
            }
        }
    }
}
