using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// ATTEXT and -ATTEXT: the attributes of the blocks of a drawing written to a file for a spreadsheet or a database, as
    /// AutoCAD 2021 writes them, its prompts and messages as its console gave them. A comma-delimited (CDF) or space-delimited
    /// (SDF) file takes its fields from a template file, one field a line: its name (a block field, BL:NAME, BL:X and so on, or the
    /// tag of an attribute) and its format, C or N, three figures for the width and three for the decimals, C008000 for text of
    /// up to 8 characters, N007002 for a number of up to 7 characters with 2 decimals. A DXF extract (DXX) holds the blocks
    /// and their attributes as the records of a DXF file, and needs no template.
    /// </summary>
    internal static partial class CadCommands
    {
        // The last template named, which the next ATTEXT offers, as AutoCAD keeps it from one ATTEXT to the next.
        private static readonly ConditionalWeakTable<CadCommandSession, StrongBox<string>> LastTemplates =
            new ConditionalWeakTable<CadCommandSession, StrongBox<string>>();

        /// <summary>One line of a template: the field it names, and the width and decimals it gives.</summary>
        private sealed class AttextField
        {
            public AttextField(string name, bool numeric, int width, int decimals, string line)
            {
                Name = name;
                IsNumber = numeric;
                Width = width;
                Decimals = decimals;
                Line = line;
            }

            public string Name { get; }

            public bool IsNumber { get; }

            public int Width { get; }

            public int Decimals { get; }

            public string Line { get; }
        }

        /// <summary>A template read: its fields in order and the delimiter and quote of a CDF file.</summary>
        private sealed class AttextTemplate
        {
            public List<AttextField> Fields { get; } = new List<AttextField>();

            public char Delimiter { get; set; } = ',';

            public char Quote { get; set; } = '\'';
        }

        /// <summary>
        /// ATTEXT: "Enter extraction type or enable object selection [Cdf/Sdf/Dxf/Objects] &lt;C&gt;:"; Objects asks "Select
        /// objects:" and then "Enter attribute extraction type [Cdf/Sdf/Dxf] &lt;C&gt;:". A CDF or SDF extract asks "Enter
        /// template file name:" (the last one named offered; ".txt" added to a name with no extension; "Can't find file." and
        /// asked again where there is none) and a template that holds a line that is no field, a field twice or no field is
        /// told, "** Invalid field specification:", "** Duplicate field specification:" or "** No Attribute fields
        /// specified.", and nothing is written. Then "Enter extract file name &lt;Drawing1&gt;:", ".txt" or, for a DXF extract,
        /// ".dxx" added to a name with no extension and the drawing's folder the home of one with no path; a file named as
        /// the template is refused and asked again, "The template and extract files cannot be the same." A file there already
        /// is written over, as AutoCAD writes over it. The blocks that have attributes are written in the order of the drawing,
        /// model space first and then each layout, the whole drawing or the objects selected; "4 records in extract file."
        /// ("16 objects in extract file." for DXF) ends it. A value that is no number in a numeric field is told, "** Bad numeric
        /// value for field PRICE, record 3", and written as nought; one too long for its field is cut to it and told once a
        /// record, "** Field overflow in record 1". The file is UTF-8 without a mark and its lines end in CR LF; AutoCAD writes
        /// the system's code page.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeExtract(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter extraction type or enable object selection",
                new[] { "Cdf", "Sdf", "Dxf", "Objects" },
                "C"
            );

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string kind = c.Input.Text;
            IReadOnlyList<string> selected = null;

            if (kind == "Objects")
            {
                yield return CadPrompt.ForSelection("Select objects");
                selected = c.Input.Handles ?? Array.Empty<string>();
                yield return CadPrompt.ForKeyword(
                    "Enter attribute extraction type",
                    new[] { "Cdf", "Sdf", "Dxf" },
                    "C"
                );

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                kind = c.Input.Text;
            }

            bool dxf = kind == "Dxf";
            string templatePath = null;
            AttextTemplate template = null;

            if (!dxf)
            {
                string remembered = LastTemplates.TryGetValue(c.Session, out StrongBox<string> box) ? box.Value : null;

                while (templatePath == null)
                {
                    yield return CadPrompt.ForString("Enter template file name", remembered);
                    string typed = Typed(c, remembered ?? string.Empty).Trim().Trim('"');

                    if (typed.Length == 0)
                    {
                        yield break;
                    }

                    string path = ExtractPath(c.Document, typed, ".txt");

                    if (!File.Exists(path))
                    {
                        c.Write("\"" + Path.GetFileName(path) + "\": Can't find file.");
                        continue;
                    }

                    templatePath = path;
                }

                LastTemplates.GetOrCreateValue(c.Session).Value = templatePath;

                try
                {
                    template = ReadTemplate(c, File.ReadAllText(templatePath));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    c.Write("Cannot read \"" + templatePath + "\": " + exception.Message);
                    yield break;
                }

                if (template == null)
                {
                    yield break;
                }
            }

            string title = Path.GetFileNameWithoutExtension(c.Document.Title ?? "Drawing1");
            string extractPath = null;

            while (extractPath == null)
            {
                yield return CadPrompt.ForString("Enter extract file name", title);
                string typed = Typed(c, title).Trim().Trim('"');

                if (typed.Length == 0)
                {
                    yield break;
                }

                string path = ExtractPath(c.Document, typed, dxf ? ".dxx" : ".txt");

                if (
                    templatePath != null
                    && string.Equals(
                        Path.GetFullPath(path),
                        Path.GetFullPath(templatePath),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    c.Write("The template and extract files cannot be the same.");
                    continue;
                }

                extractPath = path;
            }

            List<DxfBlockReference> blocks = ExtractedBlocks(c, selected, out List<int> numbers);
            var lines = new StringBuilder();
            int written;

            if (dxf)
            {
                written = WriteDxfExtract(lines, blocks);
            }
            else
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    lines
                        .Append(ExtractRecord(c, template, blocks[i], numbers[i], i + 1, kind == "Sdf"))
                        .Append("\r\n");
                }

                written = blocks.Count;
            }

            try
            {
                File.WriteAllText(extractPath, lines.ToString(), new UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                c.Write("** Error writing extract file.");
                yield break;
            }

            c.Write(
                written.ToString(CultureInfo.InvariantCulture)
                    + (dxf ? " objects in extract file." : " records in extract file.")
            );
        }

        /// <summary>A file name typed, with the extension given where it has none and the drawing's folder where it has no path.</summary>
        private static string ExtractPath(CadDocument document, string typed, string extension)
        {
            string name = Path.HasExtension(typed) ? typed : typed + extension;
            return Path.IsPathRooted(name) ? name : Path.Combine(Prefix(document), name);
        }

        /// <summary>
        /// The block references that have attributes, in the order of the drawing: those of model space and then of each layout,
        /// or of the objects selected; with the number each has among all the block references looked at, those with no
        /// attributes counted.
        /// </summary>
        private static List<DxfBlockReference> ExtractedBlocks(
            CadCommandContext c,
            IReadOnlyList<string> selected,
            out List<int> numbers
        )
        {
            IEnumerable<DxfEntity> entities;

            if (selected != null)
            {
                entities = selected
                    .Select(h => c.Document.FindEntity(h))
                    .Where(e => e != null)
                    .OrderBy(e => e.Handle.Length)
                    .ThenBy(e => e.Handle, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                entities = c.Document.Spaces.SelectMany(s => s.Block);
            }

            var found = new List<DxfBlockReference>();
            numbers = new List<int>();
            int number = 0;

            foreach (DxfEntity entity in entities)
            {
                if (!(entity is DxfBlockReference insert) || insert.IsErased)
                {
                    continue;
                }

                number++;

                if (insert.AttributeCollection.Any(a => !a.IsErased))
                {
                    found.Add(insert);
                    numbers.Add(number);
                }
            }

            return found;
        }

        #region The template

        /// <summary>
        /// Reads a template. Null, with the fault told as AutoCAD tells it, where a line is no field (no format, a format that is
        /// not C or N and six figures, or a blank line between fields), a field is there twice (letters of either case alike), or
        /// there is none.
        /// </summary>
        private static AttextTemplate ReadTemplate(CadCommandContext c, string text)
        {
            var template = new AttextTemplate();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> lines = text.Replace("\r\n", "\n").Split('\n').ToList();

            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            foreach (string line in lines)
            {
                string[] words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (words.Length >= 2 && words[0].Equals("C:DELIM", StringComparison.OrdinalIgnoreCase))
                {
                    template.Delimiter = words[1][0];
                    continue;
                }

                if (words.Length >= 2 && words[0].Equals("C:QUOTE", StringComparison.OrdinalIgnoreCase))
                {
                    template.Quote = words[1][0];
                    continue;
                }

                if (words.Length < 2 || !IsFormat(words[1]))
                {
                    c.Write("** Invalid field specification:");
                    c.Write(line);
                    return null;
                }

                string format = words[1].ToUpperInvariant();

                if (!names.Add(words[0]))
                {
                    c.Write("** Duplicate field specification:");
                    c.Write(line);
                    return null;
                }

                template.Fields.Add(
                    new AttextField(
                        words[0].ToUpperInvariant(),
                        format[0] == 'N',
                        int.Parse(format.Substring(1, 3), CultureInfo.InvariantCulture),
                        int.Parse(format.Substring(4, 3), CultureInfo.InvariantCulture),
                        line
                    )
                );
            }

            if (template.Fields.Count == 0)
            {
                c.Write("** No Attribute fields specified.");
                return null;
            }

            return template;
        }

        /// <summary>Whether a word is a format: C or N, three figures of width and three of decimals.</summary>
        private static bool IsFormat(string word) =>
            word.Length == 7
            && (word[0] == 'C' || word[0] == 'c' || word[0] == 'N' || word[0] == 'n')
            && word.Substring(1).All(ch => ch >= '0' && ch <= '9');

        #endregion

        #region A record of a CDF or SDF file

        /// <summary>One record of a CDF or SDF file: the fields of the template in order, a field of text cut to its width and a number in a field wide enough, told where it was not.</summary>
        private static string ExtractRecord(
            CadCommandContext c,
            AttextTemplate template,
            DxfBlockReference insert,
            int number,
            int record,
            bool fixedWidth
        )
        {
            var line = new StringBuilder();
            bool overflowed = false;

            for (int i = 0; i < template.Fields.Count; i++)
            {
                AttextField field = template.Fields[i];

                if (i > 0 && !fixedWidth)
                {
                    line.Append(template.Delimiter);
                }

                string value = FieldValue(insert, field, number, out double? numeric);
                string cell;

                if (field.IsNumber)
                {
                    if (!numeric.HasValue)
                    {
                        if (
                            !double.TryParse(
                                value.Trim(),
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out double parsed
                            )
                        )
                        {
                            c.Write(
                                "** Bad numeric value for field "
                                    + field.Name
                                    + ", record "
                                    + record.ToString(CultureInfo.InvariantCulture)
                            );
                            parsed = 0.0;
                        }

                        numeric = parsed;
                    }

                    cell = numeric.Value.ToString(
                        "F" + field.Decimals.ToString(CultureInfo.InvariantCulture),
                        CultureInfo.InvariantCulture
                    );

                    // A number keeps its sign's place in a comma-delimited file, as it fills its width in a fixed one.
                    if (fixedWidth)
                    {
                        cell = cell.Length > field.Width ? cell : cell.PadLeft(field.Width);
                    }
                    else if (cell[0] != '-' && cell.Length < field.Width)
                    {
                        cell = " " + cell;
                    }
                }
                else
                {
                    cell = value;

                    if (fixedWidth && cell.Length < field.Width)
                    {
                        cell = cell.PadRight(field.Width);
                    }
                }

                if (cell.Length > field.Width)
                {
                    cell = cell.Substring(0, field.Width);
                    overflowed = true;
                }

                if (!field.IsNumber && !fixedWidth)
                {
                    line.Append(template.Quote).Append(cell.TrimEnd()).Append(template.Quote);
                }
                else
                {
                    line.Append(cell);
                }
            }

            if (overflowed)
            {
                c.Write("** Field overflow in record " + record.ToString(CultureInfo.InvariantCulture));
            }

            return line.ToString();
        }

        /// <summary>
        /// What a field of the template holds for a block reference: a block field's own value, a number or text, or the value
        /// of the attribute of that tag (the first, letters of either case alike), blank or nought where it has none.
        /// </summary>
        private static string FieldValue(DxfBlockReference insert, AttextField field, int number, out double? numeric)
        {
            numeric = null;

            switch (field.Name)
            {
                case "BL:LEVEL":
                    numeric = 1.0;
                    break;
                case "BL:NAME":
                    return insert.Name;
                case "BL:X":
                    numeric = insert.Position.X;
                    break;
                case "BL:Y":
                    numeric = insert.Position.Y;
                    break;
                case "BL:Z":
                    numeric = insert.Position.Z;
                    break;
                case "BL:NUMBER":
                    numeric = number;
                    break;
                case "BL:HANDLE":
                    return insert.Handle;
                case "BL:LAYER":
                    return insert.Layer;
                case "BL:ORIENT":
                    numeric = Math.Round(insert.Rotation * 180.0 / Math.PI, 9);
                    break;
                case "BL:XSCALE":
                    numeric = insert.ScaleFactors.X;
                    break;
                case "BL:YSCALE":
                    numeric = insert.ScaleFactors.Y;
                    break;
                case "BL:ZSCALE":
                    numeric = insert.ScaleFactors.Z;
                    break;
                case "BL:XEXTRUDE":
                    numeric = insert.Normal.X;
                    break;
                case "BL:YEXTRUDE":
                    numeric = insert.Normal.Y;
                    break;
                case "BL:ZEXTRUDE":
                    numeric = insert.Normal.Z;
                    break;
                default:
                    DxfAttributeReference attribute = insert.AttributeCollection.FirstOrDefault(a =>
                        !a.IsErased && string.Equals(a.Tag, field.Name, StringComparison.OrdinalIgnoreCase)
                    );

                    if (attribute == null)
                    {
                        numeric = 0.0;
                        return string.Empty;
                    }

                    string text = attribute.TextString ?? string.Empty;

                    if (text.Trim().Length == 0)
                    {
                        numeric = 0.0;
                    }

                    return text;
            }

            // A number named by a field of text is written as it would be, to the width.
            return numeric.Value.ToString("R", CultureInfo.InvariantCulture);
        }

        #endregion

        #region A DXF extract

        /// <summary>
        /// The blocks and their attributes as the records of a DXF file, as AutoCAD 2021 writes a DXX file: each INSERT with its
        /// layer, name and insertion point (and its scales, turn and normal where they are not 1, 0 and Z), its ATTRIBs with their
        /// value, tag and flags, and its SEQEND, then EOF. How many records were written.
        /// </summary>
        private static int WriteDxfExtract(StringBuilder text, IReadOnlyList<DxfBlockReference> blocks)
        {
            int objects = 0;

            foreach (DxfBlockReference insert in blocks)
            {
                Group(text, 0, "INSERT");
                Group(text, 5, insert.Handle);
                Group(text, 100, "AcDbEntity");
                Group(text, 8, insert.Layer);
                Group(text, 66, "     1");
                Group(text, 100, "AcDbBlockReference");
                Group(text, 2, insert.Name);
                Group(text, 10, Real(insert.Position.X));
                Group(text, 20, Real(insert.Position.Y));
                Group(text, 30, Real(insert.Position.Z));

                GeoVector3 scale = insert.ScaleFactors;

                if (scale.X != 1.0)
                {
                    Group(text, 41, Real(scale.X));
                }

                if (scale.Y != 1.0)
                {
                    Group(text, 42, Real(scale.Y));
                }

                if (scale.Z != 1.0)
                {
                    Group(text, 43, Real(scale.Z));
                }

                if (insert.Rotation != 0.0)
                {
                    Group(text, 50, Real(Math.Round(insert.Rotation * 180.0 / Math.PI, 9)));
                }

                GeoVector3 normal = insert.Normal;

                if (normal.X != 0.0 || normal.Y != 0.0 || normal.Z != 1.0)
                {
                    Group(text, 210, Real(normal.X));
                    Group(text, 220, Real(normal.Y));
                    Group(text, 230, Real(normal.Z));
                }

                objects++;

                foreach (DxfAttributeReference attribute in insert.AttributeCollection.Where(a => !a.IsErased))
                {
                    int flags =
                        (attribute.Invisible ? 1 : 0)
                        + (attribute.IsConstant ? 2 : 0)
                        + (attribute.IsVerifiable ? 4 : 0)
                        + (attribute.IsPreset ? 8 : 0);
                    Group(text, 0, "ATTRIB");
                    Group(text, 5, attribute.Handle);
                    Group(text, 100, "AcDbEntity");
                    Group(text, 8, attribute.Layer);
                    Group(text, 100, "AcDbAttribute");
                    Group(text, 1, attribute.TextString ?? string.Empty);
                    Group(text, 2, attribute.Tag);
                    Group(text, 70, flags.ToString(CultureInfo.InvariantCulture).PadLeft(6));
                    objects++;
                }

                Group(text, 0, "SEQEND");
                Group(text, 100, "AcDbEntity");
                Group(text, 8, insert.Layer);
                Group(text, 100, "AcDbSequenceEnd");
                objects++;
            }

            Group(text, 0, "EOF");
            return objects;
        }

        private static void Group(StringBuilder text, int code, string value) =>
            text.Append(code.ToString(CultureInfo.InvariantCulture).PadLeft(3))
                .Append("\r\n")
                .Append(value)
                .Append("\r\n");

        /// <summary>A real as a DXF file writes it: the shortest text that reads back as it was, with a decimal point; 10.0, 40.25.</summary>
        private static string Real(double value)
        {
            string text = value.ToString("R", CultureInfo.InvariantCulture);
            return text.IndexOfAny(new[] { '.', 'E' }) < 0 ? text + ".0" : text;
        }

        #endregion
    }
}
