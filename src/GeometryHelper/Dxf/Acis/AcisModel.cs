using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dwg;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The form of ACIS data AutoCAD 2021 writes for a version of DXF: the SAT of R2000 (ACIS 4.00) and of R2004, R2007 and
    /// R2010 (ASM 208.00, 212.00 and 215.00), as text AutoCAD gives each character but a space as 0x9F less it; and the SAB of
    /// R2013 and R2018 (ASM 218.00 and 223.00), binary, which a DXF holds in the section ACDSDATA. The versions differ in a few
    /// fields of the records of a planar body: R2000 has no history numbers, no parameters on an edge and no header record;
    /// R2007 gives a vertex a number and the binary form a coedge one.
    /// </summary>
    internal sealed class AcisFormat
    {
        private AcisFormat(int version, int flags, bool binary, bool history, bool vertexNumber, bool coedgeNumber)
        {
            Version = version;
            Flags = flags;
            Binary = binary;
            History = history;
            VertexNumber = vertexNumber;
            CoedgeNumber = coedgeNumber;
        }

        /// <summary>The version of the data, as the head of the SAT and of the SAB gives it: 400, 20800, 21200, 21500, 21800 or 22300.</summary>
        internal int Version { get; }

        /// <summary>The flags of the head, which AutoCAD sets by version: 0, 0, 26, 24, 12 and 4.</summary>
        internal int Flags { get; }

        /// <summary>Whether the data is SAB, binary, rather than SAT, text.</summary>
        internal bool Binary { get; }

        /// <summary>Whether the records carry a history number and a history pointer after their attribute, and the data has its header record, and an edge its parameters and its convexity: all but R2000.</summary>
        internal bool History { get; }

        /// <summary>Whether a vertex carries a number telling at which end of its edge it stands: from R2007.</summary>
        internal bool VertexNumber { get; }

        /// <summary>Whether a coedge carries a number before its parameter curve: in the SAB.</summary>
        internal bool CoedgeNumber { get; }

        /// <summary>The number of records at the top of the data: the header and the body, or the body alone.</summary>
        internal int Roots => History ? 2 : 1;

        /// <summary>The form of the data of a DXF of the version; null where no form is known (before R2000).</summary>
        /// <param name="acadVer">The value of <c>$ACADVER</c>, such as <c>AC1032</c>.</param>
        internal static AcisFormat For(string acadVer)
        {
            switch (acadVer?.Trim())
            {
                case "AC1015":
                    return new AcisFormat(400, 0, false, false, false, false);
                case "AC1018":
                    return new AcisFormat(20800, 0, false, true, false, false);
                case "AC1021":
                    return new AcisFormat(21200, 26, false, true, true, false);
                case "AC1024":
                    return new AcisFormat(21500, 24, false, true, true, false);
                case "AC1027":
                    return new AcisFormat(21800, 12, true, true, true, true);
                case "AC1032":
                    return new AcisFormat(22300, 4, true, true, true, true);
                default:
                    return null;
            }
        }
    }

    /// <summary>A point or a direction of ACIS data, kept as plain numbers.</summary>
    internal readonly struct AcisVec
    {
        internal AcisVec(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal double X { get; }

        internal double Y { get; }

        internal double Z { get; }

        internal double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        internal AcisVec Unit
        {
            get
            {
                double length = Length;
                return length > 0.0 ? new AcisVec(X / length, Y / length, Z / length) : this;
            }
        }

        public static AcisVec operator +(AcisVec a, AcisVec b) => new AcisVec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static AcisVec operator -(AcisVec a, AcisVec b) => new AcisVec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static AcisVec operator -(AcisVec a) => new AcisVec(-a.X, -a.Y, -a.Z);

        public static AcisVec operator *(AcisVec a, double s) => new AcisVec(a.X * s, a.Y * s, a.Z * s);

        internal double Dot(AcisVec b) => X * b.X + Y * b.Y + Z * b.Z;

        internal AcisVec Cross(AcisVec b) => new AcisVec(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);

        internal double DistanceTo(AcisVec b) => (this - b).Length;
    }

    /// <summary>One record of ACIS data: its name and its values, the pointers among them to other records.</summary>
    internal sealed class AcisRecord
    {
        internal AcisRecord(string name)
        {
            Name = name;
        }

        /// <summary>The name, its parts joined by hyphens as SAT writes it: <c>plane-surface</c>.</summary>
        internal string Name { get; }

        internal List<AcisField> Fields { get; } = new List<AcisField>();

        /// <summary>The place of the record in the data, from nought; -1 until it is numbered.</summary>
        internal int Index { get; set; } = -1;

        internal AcisRecord Pointer(AcisRecord target)
        {
            Fields.Add(new AcisField('p', target, 0, 0.0, default(AcisVec), null, null, null));
            return this;
        }

        internal AcisRecord Whole(long value)
        {
            Fields.Add(new AcisField('i', null, value, 0.0, default(AcisVec), null, null, null));
            return this;
        }

        internal AcisRecord Real(double value)
        {
            Fields.Add(new AcisField('d', null, 0, value, default(AcisVec), null, null, null));
            return this;
        }

        internal AcisRecord Position(AcisVec value)
        {
            Fields.Add(new AcisField('P', null, 0, 0.0, value, null, null, null));
            return this;
        }

        internal AcisRecord Direction(AcisVec value)
        {
            Fields.Add(new AcisField('V', null, 0, 0.0, value, null, null, null));
            return this;
        }

        /// <summary>A flag with the two words SAT gives it, the first for false: <c>forward</c> and <c>reversed</c>.</summary>
        internal AcisRecord Flag(bool second, string first, string secondWord)
        {
            Fields.Add(new AcisField('b', null, second ? 1 : 0, 0.0, default(AcisVec), first, secondWord, null));
            return this;
        }

        internal AcisRecord Text(string value)
        {
            Fields.Add(new AcisField('s', null, 0, 0.0, default(AcisVec), null, null, value));
            return this;
        }

        /// <summary>A bound of a curve or a surface: <c>I</c> where it has none, else <c>F</c> and its value.</summary>
        internal AcisRecord Bound(double? value)
        {
            Fields.Add(
                new AcisField('B', null, value.HasValue ? 1 : 0, value ?? 0.0, default(AcisVec), null, null, null)
            );
            return this;
        }
    }

    /// <summary>A value of a record of ACIS data, of the kind its first letter gives.</summary>
    internal readonly struct AcisField
    {
        /// <summary>p a pointer, i a whole number, d a real, P a position, V a direction, b a flag, s a text, B a bound.</summary>
        internal AcisField(
            char kind,
            AcisRecord target,
            long whole,
            double real,
            AcisVec triple,
            string first,
            string second,
            string text
        )
        {
            Kind = kind;
            Target = target;
            Whole = whole;
            Real = real;
            Triple = triple;
            First = first;
            Second = second;
            Text = text;
        }

        internal char Kind { get; }

        internal AcisRecord Target { get; }

        internal long Whole { get; }

        internal double Real { get; }

        internal AcisVec Triple { get; }

        internal string First { get; }

        internal string Second { get; }

        internal string Text { get; }
    }

    /// <summary>
    /// Writes the records of a body as SAT, text, or as SAB, binary, after numbering them as ASM does: the roots first, then
    /// each record in turn gives a number to the records its pointers name that have none, in the order of the pointers.
    /// </summary>
    internal static class AcisWriter
    {
        private const string Product = "Autodesk AutoCAD";
        private const string Modeler = "ASM 226.0.0.65535 NT";

        /// <summary>Numbers the records reached from the roots, in the order ASM numbers them; the records, in that order.</summary>
        internal static List<AcisRecord> Number(IReadOnlyList<AcisRecord> roots)
        {
            var numbered = new List<AcisRecord>();

            foreach (AcisRecord root in roots)
            {
                root.Index = numbered.Count;
                numbered.Add(root);
            }

            for (int i = 0; i < numbered.Count; i++)
            {
                foreach (AcisField field in numbered[i].Fields)
                {
                    if (field.Kind == 'p' && field.Target != null && field.Target.Index < 0)
                    {
                        field.Target.Index = numbered.Count;
                        numbered.Add(field.Target);
                    }
                }
            }

            return numbered;
        }

        /// <summary>
        /// The SAT of the records as AutoCAD writes it in DXF, its lines in a list: the head, the two lines of product and
        /// tolerances, then a line for each record ended by a hash.
        /// </summary>
        internal static List<string> Sat(IReadOnlyList<AcisRecord> records, AcisFormat format)
        {
            var lines = new List<string>();
            string counted = records.Count.ToString(CultureInfo.InvariantCulture);
            lines.Add(
                format.Version.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + counted
                    + " "
                    + format.Roots.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + format.Flags.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + new string(' ', Math.Max(0, 11 - counted.Length))
            );
            lines.Add(
                Product.Length.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + Product
                    + " "
                    + Modeler.Length.ToString(CultureInfo.InvariantCulture)
                    + " "
                    + Modeler
                    + " 0  "
            );
            lines.Add(DwgSat.Number(25.4) + " " + DwgSat.Number(1E-06) + " " + DwgSat.Number(1E-10) + " ");

            foreach (AcisRecord record in records)
            {
                var text = new StringBuilder(record.Name).Append(' ');

                foreach (AcisField field in record.Fields)
                {
                    switch (field.Kind)
                    {
                        case 'p':
                            text.Append('$')
                                .Append(
                                    (field.Target == null ? -1 : field.Target.Index).ToString(
                                        CultureInfo.InvariantCulture
                                    )
                                );
                            break;
                        case 'i':
                            text.Append(field.Whole.ToString(CultureInfo.InvariantCulture));
                            break;
                        case 'd':
                            text.Append(DwgSat.Number(Plain(field.Real)));
                            break;
                        case 'P':
                        case 'V':
                            text.Append(DwgSat.Number(Plain(field.Triple.X)))
                                .Append(' ')
                                .Append(DwgSat.Number(Plain(field.Triple.Y)))
                                .Append(' ')
                                .Append(DwgSat.Number(Plain(field.Triple.Z)));
                            break;
                        case 'b':
                            text.Append(field.Whole != 0 ? field.Second : field.First);
                            break;
                        case 's':
                            text.Append('@')
                                .Append(field.Text.Length.ToString(CultureInfo.InvariantCulture))
                                .Append(' ')
                                .Append(field.Text);
                            break;
                        case 'B':
                            text.Append(field.Whole != 0 ? "F " + DwgSat.Number(Plain(field.Real)) : "I");
                            break;
                    }

                    text.Append(' ');
                }

                lines.Add(text.Append('#').ToString());
            }

            return lines;
        }

        /// <summary>
        /// The SAB of the records as AutoCAD writes it into the data store of a DXF: the head of four whole numbers (the version,
        /// nought for the count, the roots and the flags), the product, the modeler, the date and the tolerances, a record
        /// each ended by its tag, and the name of the end of the data without one.
        /// </summary>
        /// <param name="records">The numbered records.</param>
        /// <param name="format">The form of the data.</param>
        /// <param name="date">The date as C writes it, such as <c>Fri Nov 13 08:05:36 2026</c>.</param>
        internal static byte[] Sab(IReadOnlyList<AcisRecord> records, AcisFormat format, string date)
        {
            var bytes = new List<byte>(
                Encoding.ASCII.GetBytes(format.Version >= 22000 ? "ASM BinaryFile4" : "ACIS BinaryFile")
            );
            Whole(bytes, format.Version);
            Whole(bytes, 0);
            Whole(bytes, format.Roots);
            Whole(bytes, format.Flags);
            Text(bytes, Product);
            Text(bytes, Modeler);
            Text(bytes, date);
            Real(bytes, 25.4);
            Real(bytes, 1E-06);
            Real(bytes, 1E-10);

            foreach (AcisRecord record in records)
            {
                Name(bytes, record.Name);

                foreach (AcisField field in record.Fields)
                {
                    switch (field.Kind)
                    {
                        case 'p':
                            bytes.Add(0x0C);
                            bytes.AddRange(BitConverter.GetBytes(field.Target == null ? -1 : field.Target.Index));
                            break;
                        case 'i':
                            bytes.Add(0x04);
                            bytes.AddRange(BitConverter.GetBytes((int)field.Whole));
                            break;
                        case 'd':
                            Real(bytes, Plain(field.Real));
                            break;
                        case 'P':
                        case 'V':
                            bytes.Add(field.Kind == 'P' ? (byte)0x13 : (byte)0x14);
                            bytes.AddRange(BitConverter.GetBytes(Plain(field.Triple.X)));
                            bytes.AddRange(BitConverter.GetBytes(Plain(field.Triple.Y)));
                            bytes.AddRange(BitConverter.GetBytes(Plain(field.Triple.Z)));
                            break;
                        case 'b':
                            bytes.Add(field.Whole != 0 ? (byte)0x0A : (byte)0x0B);
                            break;
                        case 's':
                            Text(bytes, field.Text);
                            break;
                        case 'B':
                            bytes.Add(field.Whole != 0 ? (byte)0x0A : (byte)0x0B);

                            if (field.Whole != 0)
                            {
                                Real(bytes, Plain(field.Real));
                            }

                            break;
                    }
                }

                bytes.Add(0x11);
            }

            Name(bytes, "End-of-ASM-data");
            return bytes.ToArray();
        }

        /// <summary>A real as written: nought with no sign, as a value reached by turning a vector over leaves minus nought.</summary>
        private static double Plain(double value) => value == 0.0 ? 0.0 : value;

        private static void Whole(List<byte> bytes, int value) => bytes.AddRange(BitConverter.GetBytes(value));

        private static void Real(List<byte> bytes, double value)
        {
            bytes.Add(0x06);
            bytes.AddRange(BitConverter.GetBytes(value));
        }

        private static void Text(List<byte> bytes, string value)
        {
            bytes.Add(0x07);
            bytes.Add((byte)value.Length);
            bytes.AddRange(Encoding.ASCII.GetBytes(value));
        }

        /// <summary>The name of a record or of the end of the data: each part but the last under the tag 0x0E, the last under 0x0D.</summary>
        private static void Name(List<byte> bytes, string name)
        {
            string[] parts = name.Split('-');

            for (int i = 0; i < parts.Length; i++)
            {
                bytes.Add(i < parts.Length - 1 ? (byte)0x0E : (byte)0x0D);
                bytes.Add((byte)parts[i].Length);
                bytes.AddRange(Encoding.ASCII.GetBytes(parts[i]));
            }
        }
    }
}
