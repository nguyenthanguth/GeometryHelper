using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A value a FIELD keeps, as AutoCAD's <c>AcValue</c>: its type, the value, the unit, the format it was shown by and the
    /// words it was shown as. A value with nothing in it (<see cref="Flags"/> 1) is of no type.
    /// </summary>
    internal sealed class DxfFieldValue
    {
        /// <summary>The flags of R2007 and later: 1 holds nothing, 2 a value kept by name, 3 the value of a text field, 4 a value that was shown.</summary>
        internal int Flags = 1;

        /// <summary>The type: 0 unknown, 1 long, 2 double, 4 string, 8 date, 16 point, 32 point in space, 64 object.</summary>
        internal int Type;

        internal int Long;
        internal double Double;
        internal string Text = string.Empty;

        /// <summary>A date, in the time of the place it was made.</summary>
        internal DateTime Date;

        internal double X;
        internal double Y;
        internal double Z;

        /// <summary>The handle of the object of a value of the type 64.</summary>
        internal string Handle = "0";

        internal int Unit;
        internal string Format = string.Empty;
        internal string Shown = string.Empty;

        /// <summary>A value that holds nothing, shown by a format.</summary>
        internal static DxfFieldValue None(string format = "") => new DxfFieldValue { Format = format };

        /// <summary>A value kept by name: a text, as the variable of an AcVar field.</summary>
        internal static DxfFieldValue Named(string text) =>
            new DxfFieldValue
            {
                Flags = 2,
                Type = 4,
                Text = text,
            };

        /// <summary>A value kept by name: the object of a field of an object's property, by its handle.</summary>
        internal static DxfFieldValue Object(string handle) =>
            new DxfFieldValue
            {
                Flags = 2,
                Type = 64,
                Handle = handle,
            };

        /// <summary>The checksum of the words of a text field: a double kept by name.</summary>
        internal static DxfFieldValue Checksum(double sum) =>
            new DxfFieldValue
            {
                Flags = 2,
                Type = 2,
                Double = sum,
            };
    }

    /// <summary>
    /// A field and the fields it holds, described for <see cref="DxfField"/> to write as FIELD objects in the layout of the
    /// file's version, as AutoCAD 2021 writes them: a text field (<c>_text</c>) holding the code of the words with each field as
    /// <c>%&lt;\_FldIdx 0&gt;%</c> and the fields it holds as its children, each child of its evaluator (AcVar, AcObjProp, AcDiesel,
    /// AcExpr), its code, the objects it reads, when it is evaluated, its state and the value it came to. The code that makes
    /// or evaluates a field builds one; the field written is the one it names.
    /// </summary>
    internal sealed class DxfFieldDraft
    {
        /// <summary>When AutoCAD evaluates a field of every kind but the date: on open, save, plot, transmit, regen and on demand.</summary>
        internal const int Automatic = 63;

        /// <summary>When AutoCAD evaluates the date: on demand.</summary>
        internal const int OnDemand = 32;

        /// <summary>The state of a field made and not evaluated: initialised, modified.</summary>
        internal const int Fresh = 5;

        internal string Evaluator = string.Empty;

        /// <summary>The code, the fields it holds as <c>%&lt;\_FldIdx n&gt;%</c> and the objects it reads as <c>%&lt;\_ObjIdx n&gt;%</c>.</summary>
        internal string Code = string.Empty;

        /// <summary>The format, which a file older than R2007 keeps apart (group 4).</summary>
        internal string Format = string.Empty;

        internal List<DxfFieldDraft> Children = new List<DxfFieldDraft>();

        /// <summary>The handles of the objects the field reads, in the order of its <c>%&lt;\_ObjIdx n&gt;%</c>.</summary>
        internal List<string> Objects = new List<string>();

        internal int Option = Automatic;
        internal int Filing;
        internal int State = Fresh;

        /// <summary>1 not evaluated yet, 2 success, 4 evaluator not found, 8 syntax error, 16 invalid code, 32 invalid context, 64 other error.</summary>
        internal int Status = 1;

        internal int Error;
        internal string Message = string.Empty;

        /// <summary>The values kept by name, in the order of their names.</summary>
        internal List<KeyValuePair<string, DxfFieldValue>> Data = new List<KeyValuePair<string, DxfFieldValue>>();

        /// <summary>The value the field came to.</summary>
        internal DxfFieldValue Value = DxfFieldValue.None();

        /// <summary>The words the field shows.</summary>
        internal string Shown = string.Empty;

        /// <summary>The field made in code that this draft writes, which is bound to its record after; null for one that is not.</summary>
        internal DxfField Made;
    }
}
