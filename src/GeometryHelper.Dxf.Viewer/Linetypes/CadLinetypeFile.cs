using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What an element of a linetype's pattern carries.</summary>
    public enum CadLinetypeElementKind
    {
        /// <summary>A dash, a gap or a dot alone.</summary>
        Dash,

        /// <summary>A text, in quotes in the file, drawn in a text style.</summary>
        Text,

        /// <summary>A shape of a compiled file of shapes (.shx).</summary>
        Shape,
    }

    /// <summary>How the text or shape of an element is turned, as the letter before its angle in the file says.</summary>
    public enum CadLinetypeRotation
    {
        /// <summary>R: from the line.</summary>
        Relative,

        /// <summary>A: from the x axis, whichever way the line runs.</summary>
        Absolute,

        /// <summary>U: from the line, and never upside down.</summary>
        Upright,
    }

    /// <summary>
    /// An element of a linetype's pattern as a .lin file writes it: a length (above nought a dash, below a gap, nought a dot),
    /// and the text or shape in brackets after it, if any. <c>-2.4,["GAS",STANDARD,S=1.2,R=0.0,X=-1.2,Y=-0.6]</c> is a gap 2.4
    /// long carrying the text GAS in the style STANDARD, 1.2 high, 1.2 back and 0.6 down from the gap's end.
    /// </summary>
    public sealed class CadLinetypeElement
    {
        /// <summary>Makes an element of a length alone.</summary>
        /// <param name="length">Above nought a dash, below a gap, nought a dot.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length is not a finite number.</exception>
        public CadLinetypeElement(double length)
            : this(length, CadLinetypeElementKind.Dash, null, null, 1.0, 0.0, CadLinetypeRotation.Relative, 0.0, 0.0)
        { }

        /// <summary>Makes an element carrying a text or a shape.</summary>
        /// <param name="length">Above nought a dash, below a gap, nought a dot.</param>
        /// <param name="kind">What it carries.</param>
        /// <param name="value">The text, or the shape's name or number; null for a dash.</param>
        /// <param name="style">The text style of a text, or the file of shapes of a shape (ltypeshp.shx); null for a dash.</param>
        /// <param name="scale">The scale (S): the height of a text, the size of a shape.</param>
        /// <param name="rotation">The rotation in degrees.</param>
        /// <param name="rotationKind">What the rotation is measured from.</param>
        /// <param name="x">The offset along the line (X).</param>
        /// <param name="y">The offset across it (Y).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is not finite.</exception>
        /// <exception cref="ArgumentException">Thrown when a text or shape is given no value or style.</exception>
        public CadLinetypeElement(
            double length,
            CadLinetypeElementKind kind,
            string value,
            string style,
            double scale,
            double rotation,
            CadLinetypeRotation rotationKind,
            double x,
            double y
        )
        {
            CadLinetypeFile.Finite(length, nameof(length));
            CadLinetypeFile.Finite(scale, nameof(scale));
            CadLinetypeFile.Finite(rotation, nameof(rotation));
            CadLinetypeFile.Finite(x, nameof(x));
            CadLinetypeFile.Finite(y, nameof(y));

            if (
                kind != CadLinetypeElementKind.Dash
                && (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(style))
            )
            {
                throw new ArgumentException(
                    "A text or a shape names what it draws and its style or file of shapes.",
                    nameof(value)
                );
            }

            Length = length;
            Kind = kind;
            Value = kind == CadLinetypeElementKind.Dash ? null : value;
            Style = kind == CadLinetypeElementKind.Dash ? null : style.Trim();
            Scale = scale;
            Rotation = rotation;
            RotationKind = rotationKind;
            X = x;
            Y = y;
        }

        /// <summary>Gets the length: above nought a dash, below a gap, nought a dot.</summary>
        public double Length { get; }

        /// <summary>Gets what the element carries.</summary>
        public CadLinetypeElementKind Kind { get; }

        /// <summary>Gets the text, or the shape's name or number; null for a dash.</summary>
        public string Value { get; }

        /// <summary>Gets the text style of a text, or the file of shapes of a shape; null for a dash.</summary>
        public string Style { get; }

        /// <summary>Gets the scale (S); 1 where the file gives none.</summary>
        public double Scale { get; }

        /// <summary>Gets the rotation, in degrees.</summary>
        public double Rotation { get; }

        /// <summary>Gets what the rotation is measured from.</summary>
        public CadLinetypeRotation RotationKind { get; }

        /// <summary>Gets the offset along the line (X).</summary>
        public double X { get; }

        /// <summary>Gets the offset across the line (Y).</summary>
        public double Y { get; }

        /// <summary>Gets the element as the file writes it: <c>-2.4,["GAS",STANDARD,S=1.2,R=0,X=-1.2,Y=-0.6]</c>.</summary>
        /// <returns>The text.</returns>
        public override string ToString()
        {
            string length = CadLinetypeFile.Number(Length);

            if (Kind == CadLinetypeElementKind.Dash)
            {
                return length;
            }

            string value = Kind == CadLinetypeElementKind.Text ? "\"" + Value + "\"" : Value;
            char turn =
                RotationKind == CadLinetypeRotation.Absolute ? 'A'
                : RotationKind == CadLinetypeRotation.Upright ? 'U'
                : 'R';
            return length
                + ",["
                + value
                + ","
                + Style
                + ",S="
                + CadLinetypeFile.Number(Scale)
                + ","
                + turn
                + "="
                + CadLinetypeFile.Number(Rotation)
                + ",X="
                + CadLinetypeFile.Number(X)
                + ",Y="
                + CadLinetypeFile.Number(Y)
                + "]";
        }
    }

    /// <summary>
    /// A linetype of a .lin file: its name, its description and the elements of its pattern; or, where its pattern cannot be
    /// read, the fault. DASHED of the presenter's own set is <c>*DASHED,Dashed __ __ __</c> and <c>A,6,-1.5</c>: a dash 6
    /// long and a gap of 1.5, 7.5 in all.
    /// </summary>
    public sealed class CadLinetypeDefinition
    {
        /// <summary>Makes a linetype.</summary>
        /// <param name="name">Its name, as DASHED.</param>
        /// <param name="description">Its description; null for none.</param>
        /// <param name="elements">The elements of its pattern, at least one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the elements are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is blank or there is no element.</exception>
        public CadLinetypeDefinition(string name, string description, IEnumerable<CadLinetypeElement> elements)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Trim().Length == 0)
            {
                throw new ArgumentException("A linetype has a name.", nameof(name));
            }

            var list = new List<CadLinetypeElement>(elements ?? throw new ArgumentNullException(nameof(elements)));

            if (list.Count == 0 || list.Contains(null))
            {
                throw new ArgumentException(
                    "A linetype's pattern has at least one element, none of them null.",
                    nameof(elements)
                );
            }

            Name = name.Trim();
            Description = (description ?? string.Empty).Trim();
            Elements = list.AsReadOnly();
        }

        private CadLinetypeDefinition(string name, string description, string fault)
        {
            Name = name;
            Description = description;
            Elements = Array.Empty<CadLinetypeElement>();
            Fault = fault;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the description: what follows the comma of the name's line.</summary>
        public string Description { get; }

        /// <summary>Gets the elements of the pattern, in order; none where it could not be read.</summary>
        public IReadOnlyList<CadLinetypeElement> Elements { get; }

        /// <summary>Gets why the pattern could not be read; null where it was.</summary>
        public string Fault { get; }

        /// <summary>Gets whether an element carries a text or a shape.</summary>
        public bool IsComplex
        {
            get
            {
                foreach (CadLinetypeElement element in Elements)
                {
                    if (element.Kind != CadLinetypeElementKind.Dash)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Gets the length of the pattern: its lengths together, whatever their sign; 7.5 for a dash of 6 and a gap of 1.5.</summary>
        public double PatternLength
        {
            get
            {
                double sum = 0.0;

                foreach (CadLinetypeElement element in Elements)
                {
                    sum += Math.Abs(element.Length);
                }

                return sum;
            }
        }

        /// <summary>Gets the pattern line as the file writes it: <c>A,6,-1.5</c>.</summary>
        public string PatternText
        {
            get
            {
                var text = new StringBuilder("A");

                foreach (CadLinetypeElement element in Elements)
                {
                    text.Append(',').Append(element);
                }

                return text.ToString();
            }
        }

        /// <summary>Gets the two lines the file writes the linetype in, parted by a line feed: <c>*DASHED,Dashed __ __ __</c> and <c>A,6,-1.5</c>.</summary>
        /// <returns>The text.</returns>
        public override string ToString() =>
            "*" + Name + (Description.Length > 0 ? "," + Description : string.Empty) + "\n" + PatternText;

        internal static CadLinetypeDefinition Faulty(string name, string description, string fault) =>
            new CadLinetypeDefinition(name, description, fault);
    }

    /// <summary>
    /// Reads .lin files into <see cref="CadLinetypeDefinition"/>s as AutoCAD reads them, and carries the presenter's own set
    /// of linetypes (<see cref="BuiltIn"/>), the default. No file of AutoCAD's is carried or looked for: a .lin file is read
    /// only where the user names it or keeps it in a support folder of their own (<see cref="CadSupportFiles"/>).
    /// </summary>
    public static class CadLinetypeFile
    {
        /// <summary>The name the presenter's own set goes by in the dialogs and on the command line: no file has it on disk.</summary>
        public const string BuiltInName = "GeometryHelper.lin";

        /// <summary>
        /// The presenter's own linetypes, in millimetres, the lengths of ISO 128 for a pen of 0.5 mm: a dash 6, a long dash 12,
        /// a short dash 3, a gap 1.5, a dot nought. Each but GAS_LINE comes again at half size (its name and 2: DASHED2, a dash 3
        /// and a gap of 0.75) and at double size (its name and X2: DASHEDX2, a dash 12 and a gap of 3). The ISO 128-20 types 02 to
        /// 15 follow for a pen of 1.0 mm: a dash 12, a long dash 24, a short dash 6, a gap 3, a space 18 and a dot nought, as a
        /// pen draws a dot of half its width.
        /// </summary>
        private const string Own =
            "*DASHED,Dashed __ __ __ __ __ __ __\n"
            + "A,6,-1.5\n"
            + "*DASHED2,Dashed, half size\n"
            + "A,3,-0.75\n"
            + "*DASHEDX2,Dashed, double size\n"
            + "A,12,-3\n"
            + "*HIDDEN,Hidden _ _ _ _ _ _ _ _ _ _ _\n"
            + "A,3,-1.5\n"
            + "*HIDDEN2,Hidden, half size\n"
            + "A,1.5,-0.75\n"
            + "*HIDDENX2,Hidden, double size\n"
            + "A,6,-3\n"
            + "*CENTER,Center ____ _ ____ _ ____ _\n"
            + "A,12,-1.5,3,-1.5\n"
            + "*CENTER2,Center, half size\n"
            + "A,6,-0.75,1.5,-0.75\n"
            + "*CENTERX2,Center, double size\n"
            + "A,24,-3,6,-3\n"
            + "*PHANTOM,Phantom ____ _ _ ____ _ _ ____\n"
            + "A,12,-1.5,3,-1.5,3,-1.5\n"
            + "*PHANTOM2,Phantom, half size\n"
            + "A,6,-0.75,1.5,-0.75,1.5,-0.75\n"
            + "*PHANTOMX2,Phantom, double size\n"
            + "A,24,-3,6,-3,6,-3\n"
            + "*DOT,Dot . . . . . . . . . . . . .\n"
            + "A,0,-1.5\n"
            + "*DOT2,Dot, half size\n"
            + "A,0,-0.75\n"
            + "*DOTX2,Dot, double size\n"
            + "A,0,-3\n"
            + "*DASHDOT,Dash dot __ . __ . __ . __\n"
            + "A,6,-1.5,0,-1.5\n"
            + "*DASHDOT2,Dash dot, half size\n"
            + "A,3,-0.75,0,-0.75\n"
            + "*DASHDOTX2,Dash dot, double size\n"
            + "A,12,-3,0,-3\n"
            + "*DIVIDE,Divide __ . . __ . . __ . .\n"
            + "A,6,-1.5,0,-1.5,0,-1.5\n"
            + "*DIVIDE2,Divide, half size\n"
            + "A,3,-0.75,0,-0.75,0,-0.75\n"
            + "*DIVIDEX2,Divide, double size\n"
            + "A,12,-3,0,-3,0,-3\n"
            + "*BORDER,Border __ __ . __ __ . __ __ .\n"
            + "A,6,-1.5,6,-1.5,0,-1.5\n"
            + "*BORDER2,Border, half size\n"
            + "A,3,-0.75,3,-0.75,0,-0.75\n"
            + "*BORDERX2,Border, double size\n"
            + "A,12,-3,12,-3,0,-3\n"
            +
            // ISO 128-20 for a pen of 1.0 mm (W100); the type's number in ISO 128-2 is the one in the name.
            // 02 dashed: a dash and a gap.
            "*ACAD_ISO02W100,ISO 02 dashed, pen 1.0 mm\n"
            + "A,12,-3\n"
            +
            // 03 dashed spaced: a dash and a space.
            "*ACAD_ISO03W100,ISO 03 dashed spaced, pen 1.0 mm\n"
            + "A,12,-18\n"
            +
            // 04 long-dashed dotted: a long dash, a dot.
            "*ACAD_ISO04W100,ISO 04 long dash dot, pen 1.0 mm\n"
            + "A,24,-3,0,-3\n"
            +
            // 05 long-dashed double-dotted: a long dash, two dots.
            "*ACAD_ISO05W100,ISO 05 long dash two dots, pen 1.0 mm\n"
            + "A,24,-3,0,-3,0,-3\n"
            +
            // 06 long-dashed triplicate-dotted: a long dash, three dots.
            "*ACAD_ISO06W100,ISO 06 long dash three dots, pen 1.0 mm\n"
            + "A,24,-3,0,-3,0,-3,0,-3\n"
            +
            // 07 dotted: a dot and a gap.
            "*ACAD_ISO07W100,ISO 07 dotted, pen 1.0 mm\n"
            + "A,0,-3\n"
            +
            // 08 long-dashed short-dashed: a long dash, a short dash.
            "*ACAD_ISO08W100,ISO 08 long dash short dash, pen 1.0 mm\n"
            + "A,24,-3,6,-3\n"
            +
            // 09 long-dashed double-short-dashed: a long dash, two short dashes.
            "*ACAD_ISO09W100,ISO 09 long dash two short dashes, pen 1.0 mm\n"
            + "A,24,-3,6,-3,6,-3\n"
            +
            // 10 dashed dotted: a dash, a dot.
            "*ACAD_ISO10W100,ISO 10 dash dot, pen 1.0 mm\n"
            + "A,12,-3,0,-3\n"
            +
            // 11 double-dashed dotted: two dashes, a dot.
            "*ACAD_ISO11W100,ISO 11 two dashes dot, pen 1.0 mm\n"
            + "A,12,-3,12,-3,0,-3\n"
            +
            // 12 dashed double-dotted: a dash, two dots.
            "*ACAD_ISO12W100,ISO 12 dash two dots, pen 1.0 mm\n"
            + "A,12,-3,0,-3,0,-3\n"
            +
            // 13 double-dashed double-dotted: two dashes, two dots.
            "*ACAD_ISO13W100,ISO 13 two dashes two dots, pen 1.0 mm\n"
            + "A,12,-3,12,-3,0,-3,0,-3\n"
            +
            // 14 dashed triplicate-dotted: a dash, three dots.
            "*ACAD_ISO14W100,ISO 14 dash three dots, pen 1.0 mm\n"
            + "A,12,-3,0,-3,0,-3,0,-3\n"
            +
            // 15 double-dashed triplicate-dotted: two dashes, three dots.
            "*ACAD_ISO15W100,ISO 15 two dashes three dots, pen 1.0 mm\n"
            + "A,12,-3,12,-3,0,-3,0,-3,0,-3\n"
            + "*GAS_LINE,Gas line ----GAS----GAS----GAS----\n"
            + "A,6,-2.4,[\"GAS\",STANDARD,S=1.2,R=0.0,X=-1.2,Y=-0.6],-3\n";

        private static readonly Lazy<IReadOnlyList<CadLinetypeDefinition>> Mine = new Lazy<
            IReadOnlyList<CadLinetypeDefinition>
        >(() => Parse(Own));

        /// <summary>
        /// Gets the presenter's own 39 linetypes, in millimetres: DASHED, HIDDEN, CENTER, PHANTOM, DOT, DASHDOT, DIVIDE and BORDER,
        /// each with its half size (DASHED2) and double size (DASHEDX2); ACAD_ISO02W100 to ACAD_ISO15W100; and GAS_LINE. Continuous
        /// is not among them, as every drawing has it.
        /// </summary>
        public static IReadOnlyList<CadLinetypeDefinition> BuiltIn => Mine.Value;

        /// <summary>
        /// Reads the linetypes of the text of a .lin file, in its order: each opened by <c>*NAME,description</c>, its pattern on
        /// the next line that is not blank or a comment (<c>;</c>), <c>A,</c> and its elements. A linetype whose pattern cannot be
        /// read is given with its <see cref="CadLinetypeDefinition.Fault"/>, as AutoCAD refuses it alone.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The linetypes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public static IReadOnlyList<CadLinetypeDefinition> Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var linetypes = new List<CadLinetypeDefinition>();
            string name = null;
            string description = null;

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r').Trim();

                if (line.Length == 0 || line[0] == ';')
                {
                    continue;
                }

                if (line[0] == '*')
                {
                    if (name != null)
                    {
                        linetypes.Add(
                            CadLinetypeDefinition.Faulty(name, description, "The linetype " + name + " has no pattern.")
                        );
                    }

                    int comma = line.IndexOf(',');
                    name = (comma < 0 ? line.Substring(1) : line.Substring(1, comma - 1)).Trim();
                    description = comma < 0 ? string.Empty : line.Substring(comma + 1).Trim();
                    continue;
                }

                if (name == null)
                {
                    continue;
                }

                linetypes.Add(Definition(name, description, line));
                name = null;
            }

            if (name != null)
            {
                linetypes.Add(
                    CadLinetypeDefinition.Faulty(name, description, "The linetype " + name + " has no pattern.")
                );
            }

            return linetypes.AsReadOnly();
        }

        /// <summary>
        /// Reads a pattern typed as -LINETYPE Create takes it, <c>A,6,-1.5</c> or <c>6,-1.5</c>, into a linetype of the name and
        /// description given; the fault said where it cannot be read.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="description">The description.</param>
        /// <param name="pattern">The pattern, its <c>A,</c> optional.</param>
        /// <returns>The linetype; its <see cref="CadLinetypeDefinition.Fault"/> set where the pattern cannot be read.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name or the pattern is null.</exception>
        public static CadLinetypeDefinition ParsePattern(string name, string description, string pattern)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            string line = pattern.Trim();

            if (
                !line.StartsWith("A,", StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith("A ", StringComparison.OrdinalIgnoreCase)
            )
            {
                line = "A," + line;
            }

            return Definition(name.Trim(), description ?? string.Empty, line);
        }

        /// <summary>
        /// Reads a .lin file: UTF-8 where its bytes are, else each byte its own character, as AutoCAD's own files are written in
        /// the code page of Windows.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The linetypes.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static IReadOnlyList<CadLinetypeDefinition> Read(string path) =>
            Parse(ReadText(path ?? throw new ArgumentNullException(nameof(path))));

        /// <summary>The text of a file of support: UTF-8 where its bytes are, else Latin-1.</summary>
        internal static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(28591).GetString(bytes);
            }
        }

        /// <summary>Refuses a number that is not finite.</summary>
        internal static void Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    value,
                    "A length, scale, rotation or offset has to be a finite number."
                );
            }
        }

        /// <summary>A number as the file writes it, invariant and as short as reads back the same.</summary>
        internal static string Number(double value) => (value + 0.0).ToString("R", CultureInfo.InvariantCulture);

        /// <summary>The linetype of a name, a description and its pattern line; the fault where the line cannot be read.</summary>
        private static CadLinetypeDefinition Definition(string name, string description, string line)
        {
            List<string> fields = Fields(line, out string fault);

            if (
                fault == null
                && (fields.Count < 2 || !string.Equals(fields[0].Trim(), "A", StringComparison.OrdinalIgnoreCase))
            )
            {
                fault = "The pattern of " + name + " does not start with the alignment A.";
            }

            var elements = new List<CadLinetypeElement>();

            for (int i = 1; fault == null && i < fields.Count; i++)
            {
                string field = fields[i].Trim();

                if (field.StartsWith("[", StringComparison.Ordinal))
                {
                    if (elements.Count == 0 || elements[elements.Count - 1].Kind != CadLinetypeElementKind.Dash)
                    {
                        fault = "The pattern of " + name + " gives a text or shape that follows no length.";
                        break;
                    }

                    CadLinetypeElement complex = Complex(elements[elements.Count - 1].Length, field, out fault);

                    if (complex != null)
                    {
                        elements[elements.Count - 1] = complex;
                    }

                    continue;
                }

                if (!TryNumber(field, out double length))
                {
                    fault = "The pattern of " + name + " has \"" + field + "\", which is not a length.";
                    break;
                }

                elements.Add(new CadLinetypeElement(length));
            }

            if (fault == null && elements.Count == 0)
            {
                fault = "The pattern of " + name + " has no element.";
            }

            if (fault == null && elements.Count > 12)
            {
                fault =
                    "The pattern of "
                    + name
                    + " has "
                    + elements.Count.ToString(CultureInfo.InvariantCulture)
                    + " elements, and AutoCAD takes 12 at most.";
            }

            return fault != null
                ? CadLinetypeDefinition.Faulty(name, description, fault)
                : new CadLinetypeDefinition(name, description, elements);
        }

        /// <summary>The fields of a pattern line parted at its commas, a part in brackets one field with its brackets, a comma in quotes kept.</summary>
        private static List<string> Fields(string line, out string fault)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            int depth = 0;
            fault = null;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (!quoted && c == '[')
                {
                    depth++;
                }
                else if (!quoted && c == ']')
                {
                    depth--;
                }

                if (c == ',' && !quoted && depth == 0)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    continue;
                }

                field.Append(c);
            }

            fields.Add(field.ToString());

            if (quoted || depth != 0)
            {
                fault = "The pattern line has a quote or a bracket not closed.";
            }

            return fields;
        }

        /// <summary>The element a part in brackets makes of a length: its text or shape, its style or file, then S, R, A, U, X and Y.</summary>
        private static CadLinetypeElement Complex(double length, string field, out string fault)
        {
            fault = null;
            string inner = field.Substring(1, field.Length - 2);
            List<string> parts = Fields(inner, out fault);

            if (fault != null || parts.Count < 2)
            {
                fault = fault
                    ?? "A text or shape in brackets names what it draws and its style or file: " + field + ".";
                return null;
            }

            string first = parts[0].Trim();
            bool text = first.Length >= 2 && first[0] == '"' && first[first.Length - 1] == '"';
            string value = text ? first.Substring(1, first.Length - 2) : first;
            string style = parts[1].Trim();
            double scale = 1.0;
            double rotation = 0.0;
            var turn = CadLinetypeRotation.Relative;
            double x = 0.0;
            double y = 0.0;

            for (int i = 2; i < parts.Count; i++)
            {
                string part = parts[i].Trim();
                int equals = part.IndexOf('=');
                string key = equals < 0 ? string.Empty : part.Substring(0, equals).Trim().ToUpperInvariant();
                string number = equals < 0 ? string.Empty : part.Substring(equals + 1).Trim();
                bool read;

                switch (key)
                {
                    case "S":
                        read = TryNumber(number, out scale);
                        break;
                    case "X":
                        read = TryNumber(number, out x);
                        break;
                    case "Y":
                        read = TryNumber(number, out y);
                        break;
                    case "R":
                    case "A":
                    case "U":
                        turn =
                            key == "A" ? CadLinetypeRotation.Absolute
                            : key == "U" ? CadLinetypeRotation.Upright
                            : CadLinetypeRotation.Relative;
                        read = TryAngle(number, out rotation);
                        break;
                    default:
                        read = false;
                        break;
                }

                if (!read)
                {
                    fault =
                        "A text or shape in brackets has \""
                        + part
                        + "\", which is not S=, R=, A=, U=, X= or Y= and a number.";
                    return null;
                }
            }

            if (value.Length == 0 || style.Length == 0)
            {
                fault = "A text or shape in brackets names what it draws and its style or file: " + field + ".";
                return null;
            }

            return new CadLinetypeElement(
                length,
                text ? CadLinetypeElementKind.Text : CadLinetypeElementKind.Shape,
                value,
                style,
                scale,
                rotation,
                turn,
                x,
                y
            );
        }

        /// <summary>An angle in degrees: as written or with d after it, from radians with r, from grads with g.</summary>
        private static bool TryAngle(string text, out double degrees)
        {
            degrees = 0.0;
            string number = text.Trim();
            double factor = 1.0;
            char last = number.Length > 0 ? char.ToLowerInvariant(number[number.Length - 1]) : ' ';

            if (last == 'd' || last == 'r' || last == 'g')
            {
                factor =
                    last == 'r' ? 180.0 / Math.PI
                    : last == 'g' ? 0.9
                    : 1.0;
                number = number.Substring(0, number.Length - 1);
            }

            if (!TryNumber(number, out double value))
            {
                return false;
            }

            degrees = value * factor;
            return true;
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }
}
