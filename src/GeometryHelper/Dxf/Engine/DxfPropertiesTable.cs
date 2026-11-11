using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A block properties table as AutoCAD 2021 keeps it (BLOCKPROPERTIESTABLE, plugin D26), read from its node's groups: its
    /// property's name (300) and description (301) of AcDbBlockPropertiesTable, its columns (91), each the handle of its
    /// parameter or attribute definition (340), its connection (301), its parameter's node (90), its default and its unmatched
    /// value and its flags (290 constant, 291 editable, 292 removable, 294 an attribute definition's); its rows (92), each
    /// its index (90) and a value for each column; and the table's flags (290 must match).
    /// </summary>
    internal sealed class DxfPropertiesTable
    {
        private const double Turn = 2.0 * Math.PI;

        /// <summary>
        /// The tolerance a cell holds a value within, 1E-7, distances and angles alike: AutoCAD 2021 read the row of 200 at
        /// Width 200.00000001 and 199.9999999 and of 20 000 at 20 000.00000001, and no row at 200.0000002, 200.000001, 199.9999
        /// and 20 000.0000001 (plugin D26b).
        /// </summary>
        internal static readonly Tolerance Held = new Tolerance(1E-7, 1E-7);

        internal string Name { get; private set; }

        internal string Description { get; private set; }

        internal List<Column> Columns { get; } = new List<Column>();

        /// <summary>The rows, each a value for each column: a <see cref="double"/>, a <see cref="string"/>, or null where the cell is empty.</summary>
        internal List<object[]> Rows { get; } = new List<object[]>();

        /// <summary>Whether the block's properties must match a row (the table's 290), as AutoCAD's <c>MustMatch</c>.</summary>
        internal bool MustMatch { get; private set; }

        /// <summary>The table of a node's groups; null where AcDbBlockPropertiesTable cannot be read whole.</summary>
        internal static DxfPropertiesTable Of(IReadOnlyList<DxfTypedValue> groups)
        {
            List<DxfTypedValue> own = DxfDynamicProperties.Section(groups, "AcDbBlockPropertiesTable");
            var table = new DxfPropertiesTable();
            int at = 0;

            if (
                !Next(own, ref at, 90, out _)
                || !Next(own, ref at, 300, out string name)
                || !Next(own, ref at, 301, out string description)
                || !Count(own, ref at, 91, out int columns)
            )
            {
                return null;
            }

            table.Name = DxfReader.NameOf(name);
            table.Description = DxfReader.Decode(description);

            for (int c = 0; c < columns; c++)
            {
                var column = new Column();

                if (
                    !Next(own, ref at, 340, out string handle)
                    || !Next(own, ref at, 170, out _)
                    || !Next(own, ref at, 171, out _)
                    || !Next(own, ref at, 300, out _)
                    || !Next(own, ref at, 301, out string connection)
                    || !Next(own, ref at, 90, out string node)
                    || !Variant(own, ref at, out _)
                    || !Variant(own, ref at, out object unmatched)
                    || !Flag(own, ref at, 290, out bool constant)
                    || !Next(own, ref at, 291, out _)
                    || !Next(own, ref at, 292, out _)
                    || !Next(own, ref at, 293, out _)
                    || !Flag(own, ref at, 294, out bool attribute)
                    || !Next(own, ref at, 302, out _)
                    || !Next(own, ref at, 340, out _)
                )
                {
                    return null;
                }

                column.Handle = handle.Trim();
                column.Connection = connection.Trim();
                column.Node = node.Trim();
                column.Unmatched = unmatched;
                column.IsConstant = constant;
                column.IsAttribute = attribute;
                table.Columns.Add(column);
            }

            if (!Count(own, ref at, 92, out int rows))
            {
                return null;
            }

            for (int r = 0; r < rows; r++)
            {
                var row = new object[columns];

                if (!Next(own, ref at, 90, out _))
                {
                    return null;
                }

                for (int c = 0; c < columns; c++)
                {
                    if (!Variant(own, ref at, out row[c]))
                    {
                        return null;
                    }
                }

                table.Rows.Add(row);
            }

            if (!Next(own, ref at, 93, out _) || !Flag(own, ref at, 290, out bool must))
            {
                return null;
            }

            table.MustMatch = must;
            return table;
        }

        /// <summary>
        /// The index of the first row whose every cell of a parameter's column holds the value the column reads now, as AutoCAD
        /// 2021 read the row of an INSERT (plugin D26): a number, an angle taken into a turn, within <see cref="Held"/>; a text
        /// alike, letters of either case apart. A column of an
        /// attribute definition is not matched: AutoCAD read the row 1 of an INSERT whose attribute was another row's text. -1
        /// where no row holds them, or a value now cannot be read (<paramref name="now"/> gives null).
        /// </summary>
        internal int Row(Func<Column, object> now)
        {
            var values = new object[Columns.Count];

            for (int c = 0; c < Columns.Count; c++)
            {
                if (!Columns[c].IsAttribute && (values[c] = now(Columns[c])) == null)
                {
                    return -1;
                }
            }

            for (int r = 0; r < Rows.Count; r++)
            {
                bool all = true;

                for (int c = 0; c < Columns.Count && all; c++)
                {
                    if (!Columns[c].IsAttribute)
                    {
                        all = Same(Rows[r][c], values[c], Columns[c].Connection == "UpdatedAngle");
                    }
                }

                if (all)
                {
                    return r;
                }
            }

            return -1;
        }

        /// <summary>Whether a cell holds a value: numbers within <see cref="Held"/>, texts alike; an empty cell holds nothing.</summary>
        internal static bool Same(object cell, object value, bool angle)
        {
            if (cell is string text)
            {
                return value is string other && string.Equals(text, other, StringComparison.Ordinal);
            }

            if (!(cell is double number) || !(value is double now))
            {
                return false;
            }

            return angle
                ? Math.Abs(Normal(number) - Normal(now)) <= Held.EqualVector
                : Math.Abs(number - now) <= Held.EqualPoint;
        }

        private static double Normal(double angle)
        {
            double turned = angle % Turn;

            if (turned < 0.0)
            {
                turned += Turn;
            }

            return turned >= Turn ? 0.0 : turned;
        }

        /// <summary>The value of the group at a place, where it is of the code given; the place moved past it.</summary>
        private static bool Next(List<DxfTypedValue> own, ref int at, int code, out string value)
        {
            value = at < own.Count && own[at].TypeCode == code ? own[at].Value ?? string.Empty : null;

            if (value == null)
            {
                return false;
            }

            at++;
            return true;
        }

        private static bool Count(List<DxfTypedValue> own, ref int at, int code, out int count)
        {
            count = 0;
            return Next(own, ref at, code, out string text)
                && int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                && count >= 0;
        }

        private static bool Flag(List<DxfTypedValue> own, ref int at, int code, out bool flag)
        {
            flag = Next(own, ref at, code, out string text) && text.Trim() == "1";
            return text != null;
        }

        /// <summary>
        /// A value as AutoCAD writes one (an AcDbEvalVariant): its type (170), then a real (140) of the type 40, a text (300) of
        /// the type 1; nothing for an empty one (-9999). False for a type of no value the library reads.
        /// </summary>
        private static bool Variant(List<DxfTypedValue> own, ref int at, out object value)
        {
            value = null;

            if (!Next(own, ref at, 170, out string type))
            {
                return false;
            }

            switch (type.Trim())
            {
                case "-9999":
                    return true;
                case "40":
                    if (Next(own, ref at, 140, out string real) && DxfDynamicProperties.Number(real, out double number))
                    {
                        value = number;
                        return true;
                    }

                    return false;
                case "1":
                    if (Next(own, ref at, 300, out string text))
                    {
                        value = DxfReader.Decode(text);
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        /// <summary>A column of the table.</summary>
        internal sealed class Column
        {
            /// <summary>The handle of the column's parameter or attribute definition.</summary>
            internal string Handle { get; set; }

            /// <summary>What the column reads of its parameter: UpdatedDistance, UpdatedDistanceX, UpdatedDistanceY, UpdatedAngle, UserVariable; empty for an attribute definition.</summary>
            internal string Connection { get; set; }

            /// <summary>The id of the node of the column's parameter; 0 for an attribute definition.</summary>
            internal string Node { get; set; }

            /// <summary>The value of the column where no row matches, as a user parameter's Custom; null for none.</summary>
            internal object Unmatched { get; set; }

            internal bool IsConstant { get; set; }

            /// <summary>Whether the column is of an attribute definition (294).</summary>
            internal bool IsAttribute { get; set; }
        }
    }
}
