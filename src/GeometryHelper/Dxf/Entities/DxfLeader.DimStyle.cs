using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The variables of the dimension style a LEADER is drawn with, read and given whole, as AutoCAD's
    /// <c>GetDimstyleData</c> and <c>SetDimstyleData</c> of a <c>Leader</c> read and give them.
    /// </summary>
    public sealed partial class DxfLeader
    {
        /// <summary>
        /// Gets the variables the leader is drawn with, as AutoCAD's <c>GetDimstyleData</c>: a new
        /// <see cref="DxfDimStyleTableRecord"/> in no table, named as its <see cref="DimensionStyleName"/>, each of its 74
        /// variables as the leader reads it, its override (the list DSTYLE of its extended data of ACAD) where it has one, else
        /// its style's, else AutoCAD's own. A leader of the style ISO-25 given an arrow 3.5 long gives <c>Dimasz</c> 3.5 and
        /// <c>Dimtxt</c> 2.5, the style's.
        /// </summary>
        /// <returns>The record, to be read, or given to another leader or a dimension by their <c>SetDimstyleData</c>.</returns>
        public DxfDimStyleTableRecord GetDimstyleData()
        {
            var data = new DxfDimStyleTableRecord { Name = DimensionStyleName };
            StyleReading reading = ReadStyle();

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                data.Keep(variable, reading.Value(variable));
            }

            return data;
        }

        /// <summary>
        /// Gives the leader the variables of a record, as AutoCAD's <c>SetDimstyleData</c>: each that differs from what the
        /// leader reads written as an override, as AutoCAD writes one (<c>1070 41, 1040 3.5</c>), its dimension style left as it
        /// is; those it reads already change nothing, so that the record of its own style writes only where an override differs
        /// from the style. A block, linetype or text style is written by its handle, none by the handle 0; a record that names no
        /// text style leaves the leader's as it is. A variable the file's version has not is passed over where the record holds
        /// AutoCAD's own value of it, and refused where it holds another. A whole number is checked against AutoCAD's range as
        /// <see cref="DxfDatabase.SetDimstyleData"/> checks it, a <c>Dimdec</c> of 9 refused, and a size of the arrow, the text or
        /// the scale below nought. Every value is checked before any is written.
        /// </summary>
        /// <param name="style">The record: of a table, or made in code, as <see cref="GetDimstyleData"/> gives one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the record is null, or holds a null text or colour.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a value of the record is one AutoCAD does not take: a number not finite, a whole number out of its range, a lineweight not one of AutoCAD's.</exception>
        /// <exception cref="ArgumentException">Thrown when a block, linetype or text style of the record is of another drawing,
        /// or the leader is in none; when a colour is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the leader is erased, or the record holds another value than
        /// AutoCAD's own of a variable the leader's file is too old to hold.</exception>
        public void SetDimstyleData(DxfDimStyleTableRecord style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            StyleReading reading = ReadStyle();
            DxfFile file = Item.File;
            var changes = new List<KeyValuePair<int, DxfTypedValue>>();

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                object value = style.Get(variable);

                if ((value == null && variable.Kind == DxfDimKind.TextStyle)
                    || variable.SameValue(value, reading.Value(variable))
                    || (!file.Supports(variable.Since) && (variable.IsReference ? value == null : variable.SameValue(value, variable.Read(null)))))
                {
                    continue;
                }

                variable.Check(value, nameof(style));
                DxfDatabase.CheckDimRange(variable, value, nameof(style));

                if (IsErased)
                {
                    throw new InvalidOperationException("The LEADER " + (Handle ?? string.Empty) + " is erased, and is not changed.");
                }

                file.Requires(variable.Since, variable.Name.ToUpperInvariant(), variable.SinceSaid);
                string text;

                if (!variable.IsReference)
                {
                    text = variable.Write(value);
                }
                else if (value == null)
                {
                    text = "0";
                }
                else if (!IsResident)
                {
                    throw new ArgumentException("The " + variable.Name + " of a leader is a record of its own drawing, and the leader is in none.", nameof(style));
                }
                else
                {
                    text = DxfDimStyleTableRecord.Reference(file, variable, (DxfDBObject)value, false, nameof(style));
                }

                changes.Add(new KeyValuePair<int, DxfTypedValue>(variable.Code, new DxfTypedValue(variable.DataCode(false), text)));
            }

            foreach (KeyValuePair<int, DxfTypedValue> change in changes)
            {
                WriteOverride(change.Key, change.Value);
            }
        }

        /// <summary>The leader's overrides and dimension style, read once for every variable.</summary>
        private StyleReading ReadStyle()
        {
            List<DxfTypedValue> data = Acad(out int open, out int close);
            DxfItem item = Style(DimensionStyleName);
            return new StyleReading(data, open, close, item == null ? null : DxfDimStyleTableRecord.Of(item), NamingDrawing());
        }

        /// <summary>The overrides of a leader and its dimension style, as read.</summary>
        private sealed class StyleReading
        {
            private readonly List<DxfTypedValue> _data;
            private readonly int _open;
            private readonly int _close;
            private readonly DxfDimStyleTableRecord _style;
            private readonly DxfFile _naming;

            internal StyleReading(List<DxfTypedValue> data, int open, int close, DxfDimStyleTableRecord style, DxfFile naming)
            {
                _data = data;
                _open = open;
                _close = close;
                _style = style;
                _naming = naming;
            }

            /// <summary>
            /// The value of a variable as the leader draws with it: its override, a reference the record of its handle in the
            /// drawing the leader names its records in; else its style's; else AutoCAD's own.
            /// </summary>
            internal object Value(DxfDimVariable variable)
            {
                List<DxfTypedValue> pair = Override(_data, _open, _close, variable.Code);

                if (pair != null)
                {
                    return !variable.IsReference ? variable.Read(pair[1].Value) : _naming == null ? null : DxfDimStyleTableRecord.Resolve(_naming, variable, pair[1].Value);
                }

                if (_style != null)
                {
                    return _style.Get(variable);
                }

                return variable.IsReference ? null : variable.Read(null);
            }
        }
    }
}
