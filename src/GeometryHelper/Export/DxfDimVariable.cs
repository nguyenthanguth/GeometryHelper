using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>What a dimension variable holds, and so how its group is read and written.</summary>
    internal enum DxfDimKind
    {
        /// <summary>A real number (DXF 40 to 49, 140 to 148), 1040 as an override.</summary>
        Real,

        /// <summary>An angle, in degrees in the file and in radians to AutoCAD's .NET, 1040 as an override.</summary>
        Angle,

        /// <summary>A whole number, 1070 as an override.</summary>
        Whole,

        /// <summary>On or off, 1 or 0, 1070 as an override.</summary>
        Flag,

        /// <summary>A text, 1000 as an override.</summary>
        Text,

        /// <summary>A colour of AutoCAD's index, 0 BYBLOCK and 256 BYLAYER, 1070 as an override.</summary>
        Colour,

        /// <summary>A lineweight, 1070 as an override.</summary>
        Weight,

        /// <summary>A letter, written as its number, 1070 as an override.</summary>
        Letter,

        /// <summary>A block, by its BLOCK_RECORD's handle from R2000 on and by its name in a file of R12, 1005 or 1000 as an override.</summary>
        Block,

        /// <summary>A linetype, by its handle, 1005 as an override.</summary>
        Linetype,

        /// <summary>A text style, by its handle, 1005 as an override.</summary>
        TextStyle,
    }

    /// <summary>
    /// A dimension variable as a DIMSTYLE record writes it and as a dimension overrides it in its extended data of ACAD
    /// (DSTYLE): its group, what it holds, the version that brought it and AutoCAD's own value of a style that gives none.
    /// </summary>
    internal sealed class DxfDimVariable
    {
        private static readonly Dictionary<string, DxfDimVariable> ByName = new Dictionary<string, DxfDimVariable>(StringComparer.Ordinal);

        private DxfDimVariable(string name, int code, DxfDimKind kind, int since, string fallback, int r12Code)
        {
            Name = name;
            Code = code;
            Kind = kind;
            Since = since;
            Fallback = fallback;
            R12Code = r12Code;
        }

        /// <summary>The name as AutoCAD's .NET gives it, <c>Dimasz</c>.</summary>
        internal string Name { get; }

        /// <summary>Its group in a DIMSTYLE record from R2000 on, and its code in a dimension's overrides.</summary>
        internal int Code { get; }

        /// <summary>What it holds.</summary>
        internal DxfDimKind Kind { get; }

        /// <summary>AutoCAD's number of the version that brought it: 1009 for R12, 1015 for R2000, 1021 for R2007.</summary>
        internal int Since { get; }

        /// <summary>AutoCAD's own value, as the file writes one, for a style that gives none: that of its drawing template acad.dwt.</summary>
        internal string Fallback { get; }

        /// <summary>Its group in a file of R12 where it is another than <see cref="Code"/>: a block named by 5, 6 or 7; nought where it is the same.</summary>
        internal int R12Code { get; }

        /// <summary>Whether it is the second group 70 of a DIMSTYLE record, DIMTFILLCLR, the first being the record's flags.</summary>
        internal bool Second => Code == 70;

        /// <summary>Whether it points at a record of a table.</summary>
        internal bool IsReference => Kind == DxfDimKind.Block || Kind == DxfDimKind.Linetype || Kind == DxfDimKind.TextStyle;

        /// <summary>
        /// The variables, in the order AutoCAD writes them in a DIMSTYLE record: the 73 of AutoCAD's
        /// <c>DimStyleTableRecord</c> that a DXF file holds.
        /// </summary>
        internal static IReadOnlyList<DxfDimVariable> All { get; } = Array.AsReadOnly(new[]
        {
            Make("Dimpost", 3, DxfDimKind.Text, 1009, string.Empty),
            Make("Dimapost", 4, DxfDimKind.Text, 1009, string.Empty),
            Make("Dimscale", 40, DxfDimKind.Real, 1009, "1.0"),
            Make("Dimasz", 41, DxfDimKind.Real, 1009, "0.18"),
            Make("Dimexo", 42, DxfDimKind.Real, 1009, "0.0625"),
            Make("Dimdli", 43, DxfDimKind.Real, 1009, "0.38"),
            Make("Dimexe", 44, DxfDimKind.Real, 1009, "0.18"),
            Make("Dimrnd", 45, DxfDimKind.Real, 1009, "0.0"),
            Make("Dimdle", 46, DxfDimKind.Real, 1009, "0.0"),
            Make("Dimtp", 47, DxfDimKind.Real, 1009, "0.0"),
            Make("Dimtm", 48, DxfDimKind.Real, 1009, "0.0"),
            Make("Dimfxlen", 49, DxfDimKind.Real, 1021, "1.0"),
            Make("Dimjogang", 50, DxfDimKind.Angle, 1021, "45.0"),
            Make("Dimtxt", 140, DxfDimKind.Real, 1009, "0.18"),
            Make("Dimcen", 141, DxfDimKind.Real, 1009, "0.09"),
            Make("Dimtsz", 142, DxfDimKind.Real, 1009, "0.0"),
            Make("Dimaltf", 143, DxfDimKind.Real, 1009, "25.4"),
            Make("Dimlfac", 144, DxfDimKind.Real, 1009, "1.0"),
            Make("Dimtvp", 145, DxfDimKind.Real, 1009, "0.0"),
            Make("Dimtfac", 146, DxfDimKind.Real, 1009, "1.0"),
            Make("Dimgap", 147, DxfDimKind.Real, 1009, "0.09"),
            Make("Dimaltrnd", 148, DxfDimKind.Real, 1015, "0.0"),
            Make("Dimtfill", 69, DxfDimKind.Whole, 1021, "0"),
            Make("Dimtfillclr", 70, DxfDimKind.Colour, 1021, "0"),
            Make("Dimtol", 71, DxfDimKind.Flag, 1009, "0"),
            Make("Dimlim", 72, DxfDimKind.Flag, 1009, "0"),
            Make("Dimtih", 73, DxfDimKind.Flag, 1009, "1"),
            Make("Dimtoh", 74, DxfDimKind.Flag, 1009, "1"),
            Make("Dimse1", 75, DxfDimKind.Flag, 1009, "0"),
            Make("Dimse2", 76, DxfDimKind.Flag, 1009, "0"),
            Make("Dimtad", 77, DxfDimKind.Whole, 1009, "0"),
            Make("Dimzin", 78, DxfDimKind.Whole, 1009, "0"),
            Make("Dimazin", 79, DxfDimKind.Whole, 1015, "0"),
            Make("Dimarcsym", 90, DxfDimKind.Whole, 1015, "0"),
            Make("Dimalt", 170, DxfDimKind.Flag, 1009, "0"),
            Make("Dimaltd", 171, DxfDimKind.Whole, 1009, "2"),
            Make("Dimtofl", 172, DxfDimKind.Flag, 1009, "0"),
            Make("Dimsah", 173, DxfDimKind.Flag, 1009, "0"),
            Make("Dimtix", 174, DxfDimKind.Flag, 1009, "0"),
            Make("Dimsoxd", 175, DxfDimKind.Flag, 1009, "0"),
            Make("Dimclrd", 176, DxfDimKind.Colour, 1009, "0"),
            Make("Dimclre", 177, DxfDimKind.Colour, 1009, "0"),
            Make("Dimclrt", 178, DxfDimKind.Colour, 1009, "0"),
            Make("Dimadec", 179, DxfDimKind.Whole, 1015, "0"),
            Make("Dimdec", 271, DxfDimKind.Whole, 1015, "4"),
            Make("Dimtdec", 272, DxfDimKind.Whole, 1015, "4"),
            Make("Dimaltu", 273, DxfDimKind.Whole, 1015, "2"),
            Make("Dimalttd", 274, DxfDimKind.Whole, 1015, "2"),
            Make("Dimaunit", 275, DxfDimKind.Whole, 1015, "0"),
            Make("Dimfrac", 276, DxfDimKind.Whole, 1015, "0"),
            Make("Dimlunit", 277, DxfDimKind.Whole, 1015, "2"),
            Make("Dimdsep", 278, DxfDimKind.Letter, 1015, "46"),
            Make("Dimtmove", 279, DxfDimKind.Whole, 1015, "0"),
            Make("Dimjust", 280, DxfDimKind.Whole, 1015, "0"),
            Make("Dimsd1", 281, DxfDimKind.Flag, 1015, "0"),
            Make("Dimsd2", 282, DxfDimKind.Flag, 1015, "0"),
            Make("Dimtolj", 283, DxfDimKind.Whole, 1015, "1"),
            Make("Dimtzin", 284, DxfDimKind.Whole, 1015, "0"),
            Make("Dimaltz", 285, DxfDimKind.Whole, 1015, "0"),
            Make("Dimalttz", 286, DxfDimKind.Whole, 1015, "0"),
            Make("Dimupt", 288, DxfDimKind.Flag, 1015, "0"),
            Make("Dimatfit", 289, DxfDimKind.Whole, 1015, "3"),
            Make("DimfxlenOn", 290, DxfDimKind.Flag, 1021, "0"),
            Make("Dimtxsty", 340, DxfDimKind.TextStyle, 1015, null),
            Make("Dimldrblk", 341, DxfDimKind.Block, 1015, null),
            Make("Dimblk", 342, DxfDimKind.Block, 1009, null, 5),
            Make("Dimblk1", 343, DxfDimKind.Block, 1009, null, 6),
            Make("Dimblk2", 344, DxfDimKind.Block, 1009, null, 7),
            Make("Dimltype", 345, DxfDimKind.Linetype, 1021, null),
            Make("Dimltex1", 346, DxfDimKind.Linetype, 1021, null),
            Make("Dimltex2", 347, DxfDimKind.Linetype, 1021, null),
            Make("Dimlwd", 371, DxfDimKind.Weight, 1015, "-2"),
            Make("Dimlwe", 372, DxfDimKind.Weight, 1015, "-2"),
        });

        /// <summary>
        /// The codes of a DIMSTYLE record's own groups in the order AutoCAD writes them, a 100 where a marker stands, the
        /// record's name and flags first; DIMTFILLCLR, the second 70, has its place after DIMTFILL (69).
        /// </summary>
        internal static IReadOnlyList<int> StyleOrder { get; } = Array.AsReadOnly(new[]
        {
            100, 2, 70, 3, 4, 5, 6, 7, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 140, 141, 142, 143, 144, 145, 146, 147, 148, 69,
            71, 72, 73, 74, 75, 76, 77, 78, 79, 90, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 271, 272, 273, 274, 275, 276,
            277, 278, 279, 280, 281, 282, 283, 284, 285, 286, 288, 289, 290, 340, 341, 342, 343, 344, 345, 346, 347, 371, 372,
        });

        /// <summary>The variable of a name as AutoCAD's .NET gives it.</summary>
        internal static DxfDimVariable Named(string name) => ByName[name];

        /// <summary>The variable whose code a dimension's override names; null for one the library does not know.</summary>
        internal static DxfDimVariable OfCode(int code, bool r12)
        {
            foreach (DxfDimVariable variable in All)
            {
                if (variable.Code == code || (r12 && variable.R12Code != 0 && variable.R12Code == code))
                {
                    return variable;
                }
            }

            return null;
        }

        /// <summary>The group of the variable in a file of a version: its R12 code there where it has one.</summary>
        internal int CodeIn(bool r12) => r12 && R12Code != 0 ? R12Code : Code;

        /// <summary>The code of the group an override of the variable is written with, after its 1070: 1040, 1070, 1000 or 1005.</summary>
        internal int DataCode(bool r12)
        {
            switch (Kind)
            {
                case DxfDimKind.Real:
                case DxfDimKind.Angle:
                    return 1040;
                case DxfDimKind.Text:
                    return 1000;
                case DxfDimKind.Block:
                case DxfDimKind.Linetype:
                case DxfDimKind.TextStyle:
                    return r12 ? 1000 : 1005;
                default:
                    return 1070;
            }
        }

        /// <summary>
        /// The value of a text of the variable, as AutoCAD's .NET gives it: a double, an angle in radians, an int, a bool, a
        /// string, a <see cref="DxfColor"/>, a <see cref="DxfLineWeight"/> or a char; AutoCAD's own value where the text is
        /// null or not one of its kind. Not for a reference.
        /// </summary>
        internal object Read(string text)
        {
            switch (Kind)
            {
                case DxfDimKind.Real:
                    return Numbers.TryRead(text, out double real) && Guard.IsFinite(real) ? real : double.Parse(Fallback, CultureInfo.InvariantCulture);
                case DxfDimKind.Angle:
                    double degrees = Numbers.TryRead(text, out double angle) && Guard.IsFinite(angle) ? angle : double.Parse(Fallback, CultureInfo.InvariantCulture);
                    return degrees * (Math.PI / 180.0);
                case DxfDimKind.Text:
                    return text ?? Fallback;
                case DxfDimKind.Flag:
                    return Whole(text) != 0;
                case DxfDimKind.Colour:
                    return DxfColor.OfIndex(ColourIndex(Whole(text)));
                case DxfDimKind.Weight:
                    return (DxfLineWeight)Whole(text);
                case DxfDimKind.Letter:
                    return (char)Whole(text);
                default:
                    return Whole(text);
            }
        }

        /// <summary>
        /// The text a value of the variable is written as: a number as <see cref="DxfWriter"/> writes one, an angle in degrees,
        /// a flag 1 or 0, a colour its index, a lineweight and a letter their numbers.
        /// </summary>
        internal string Write(object value)
        {
            switch (Kind)
            {
                case DxfDimKind.Real:
                    return Numbers.Exact((double)value + 0.0);
                case DxfDimKind.Angle:
                    return Numbers.Exact(DxfOcsTransform.Degrees((double)value));
                case DxfDimKind.Text:
                    return (string)value;
                case DxfDimKind.Flag:
                    return (bool)value ? "1" : "0";
                case DxfDimKind.Colour:
                    return ((DxfColor)value).ColorIndex.ToString(CultureInfo.InvariantCulture);
                case DxfDimKind.Weight:
                    return ((int)(DxfLineWeight)value).ToString(CultureInfo.InvariantCulture);
                case DxfDimKind.Letter:
                    return ((int)(char)value).ToString(CultureInfo.InvariantCulture);
                default:
                    return ((int)value).ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Whether two values of the variable are the same: numbers to the bit, references the same object.</summary>
        internal bool SameValue(object a, object b)
        {
            if (Kind == DxfDimKind.Real || Kind == DxfDimKind.Angle)
            {
                return BitConverter.DoubleToInt64Bits((double)a) == BitConverter.DoubleToInt64Bits((double)b);
            }

            if (IsReference)
            {
                return ReferenceEquals(a, b) || (a is DxfDBObject x && b is DxfDBObject y && x.Item != null && x.Item == y.Item);
            }

            return Equals(a, b);
        }

        /// <summary>
        /// Checks a value set: of the variable's own kind, a number finite, a text of one line, a colour of the index, BYLAYER or
        /// BYBLOCK, a lineweight one of AutoCAD's, a reference to a record of its own kind.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when a text or a colour is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is not finite, or a lineweight not one of AutoCAD's.</exception>
        /// <exception cref="ArgumentException">Thrown when a text has a line break, a colour is a true colour, or a reference is of another kind of record.</exception>
        internal void Check(object value, string parameter)
        {
            switch (Kind)
            {
                case DxfDimKind.Real:
                case DxfDimKind.Angle:
                    Guard.Finite((double)value, parameter, "The " + Name + " has to be a finite number.");
                    break;
                case DxfDimKind.Text:
                    if (value == null)
                    {
                        throw new ArgumentNullException(parameter);
                    }

                    if (((string)value).IndexOf('\n') >= 0 || ((string)value).IndexOf('\r') >= 0)
                    {
                        throw new ArgumentException("The " + Name + " is one line, with no line break in it.", parameter);
                    }

                    break;
                case DxfDimKind.Colour:
                    if (value == null)
                    {
                        throw new ArgumentNullException(parameter);
                    }

                    var colour = (DxfColor)value;

                    if (!colour.IsByAci && !colour.IsByLayer && !colour.IsByBlock)
                    {
                        throw new ArgumentException("The " + Name + " of a dimension style is a colour of AutoCAD's index, BYLAYER or BYBLOCK: a " + colour.ColorMethod + " is not written.", parameter);
                    }

                    break;
                case DxfDimKind.Weight:
                    if (!Enum.IsDefined(typeof(DxfLineWeight), value))
                    {
                        throw new ArgumentOutOfRangeException(parameter, value, "The " + Name + " is one of AutoCAD's lineweights.");
                    }

                    break;
                case DxfDimKind.Block:
                    if (value != null && !(value is DxfBlockTableRecord))
                    {
                        throw new ArgumentException("The " + Name + " is a block.", parameter);
                    }

                    if (value is DxfBlockTableRecord block && block.IsLayout)
                    {
                        throw new ArgumentException("The " + Name + " is a block of arrows, not a layout: " + block.Name + " is one.", parameter);
                    }

                    break;
                case DxfDimKind.Linetype:
                    if (value != null && !(value is DxfLinetypeTableRecord))
                    {
                        throw new ArgumentException("The " + Name + " is a linetype.", parameter);
                    }

                    break;
                case DxfDimKind.TextStyle:
                    if (value != null && !(value is DxfTextStyleTableRecord))
                    {
                        throw new ArgumentException("The " + Name + " is a text style.", parameter);
                    }

                    break;
            }
        }

        /// <summary>A colour index as AutoCAD reads one: its size, 0 to 256.</summary>
        private static int ColourIndex(int index) => Math.Min(256, Math.Abs(index));

        private int Whole(string text)
        {
            if (int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }

            // A whole number written as a real, as some writers write a 1070, reads as the whole number it is.
            if (Numbers.TryRead(text, out double real) && Guard.IsFinite(real) && real == Math.Floor(real) && Math.Abs(real) <= int.MaxValue)
            {
                return (int)real;
            }

            return int.Parse(Fallback, CultureInfo.InvariantCulture);
        }

        /// <summary>The version said in a message: R12, R2000 or R2007, with AutoCAD's number.</summary>
        internal string SinceSaid => Since >= 1021 ? "R2007 (AC1021)" : Since >= 1015 ? "R2000 (AC1015)" : "R12 (AC1009)";

        private static DxfDimVariable Make(string name, int code, DxfDimKind kind, int since, string fallback, int r12Code = 0)
        {
            var variable = new DxfDimVariable(name, code, kind, since, fallback, r12Code);
            ByName.Add(name, variable);
            return variable;
        }
    }
}
