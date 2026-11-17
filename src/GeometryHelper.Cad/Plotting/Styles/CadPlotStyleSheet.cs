using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A plot style table, the content of AutoCAD's .ctb (colour-dependent: 255 styles, one for each colour of AutoCAD's
    /// index) or .stb (named: styles a drawing's objects and layers are given by name, the first of them Normal) file, with
    /// the reader and writer of the file. A plot looks an object's style up in it (<see cref="CadPlotStyleResolver"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file is a text of nested blocks (<c>plot_style{ 0{ name="Color_1 … } }</c>, the lineweight list
    /// <c>custom_lineweight_table{ 0=0.000000 … }</c>), packed as AutoCAD packs its PIA files: the head
    /// <c>PIAFILEVERSION_2.0,CTBVER1,compress</c> (<c>STBVER1</c> for a named table), a line break and <c>pmzlibcodec</c>, three
    /// whole numbers of four bytes (the Adler-32 of the packed stream, the length of the text, the length of the stream),
    /// and the text as a zlib stream. A table read and written again gives the same text, byte for byte
    /// (<see cref="ToText"/>), and the same file where the same runtime deflates it (<see cref="ToBytes"/>); the deflated
    /// bytes of one table differ from AutoCAD's and from another runtime's, which every reader inflates alike.
    /// </para>
    /// <para>
    /// A table's lineweight list holds the millimetres a style's <see cref="CadPlotStyle.LineWeightIndex"/> chooses from, 27
    /// entries as AutoCAD starts it (0, 0.05, 0.09 … 2.11); entry 0 is "Use object lineweight".
    /// </para>
    /// </remarks>
    public sealed class CadPlotStyleSheet
    {
        /// <summary>The number of styles of a colour-dependent table: one for each colour of AutoCAD's index, 1 to 255.</summary>
        public const int ColorCount = 255;

        /// <summary>The name of the first style of a named table, which cannot be removed or renamed.</summary>
        public const string NormalName = "Normal";

        /// <summary>The extension of a colour-dependent plot style table file: .ctb.</summary>
        public const string ColorDependentExtension = ".ctb";

        /// <summary>The extension of a named plot style table file: .stb.</summary>
        public const string NamedExtension = ".stb";

        private const string ColorHead = "PIAFILEVERSION_2.0,CTBVER1,compress\r\npmzlibcodec";
        private const string NamedHead = "PIAFILEVERSION_2.0,STBVER1,compress\r\npmzlibcodec";

        private static readonly double[] StartingLineWeights =
        {
            0.00,
            0.05,
            0.09,
            0.10,
            0.13,
            0.15,
            0.18,
            0.20,
            0.25,
            0.30,
            0.35,
            0.40,
            0.45,
            0.50,
            0.53,
            0.60,
            0.65,
            0.70,
            0.80,
            0.90,
            1.00,
            1.06,
            1.20,
            1.40,
            1.58,
            2.00,
            2.11,
        };

        private readonly List<CadPlotStyle> _styles = new List<CadPlotStyle>();
        private readonly List<double> _lineWeights = new List<double>(StartingLineWeights);

        private CadPlotStyleSheet(bool isNamed)
        {
            IsNamed = isNamed;
        }

        /// <summary>
        /// Makes a colour-dependent table of 255 styles that change nothing, Color_1 to Color_255: a plot with it is as with no
        /// table, as AutoCAD's acad.ctb.
        /// </summary>
        /// <returns>The table, its <see cref="FileName"/> empty.</returns>
        public static CadPlotStyleSheet NewColorDependent()
        {
            var sheet = new CadPlotStyleSheet(false);

            for (int aci = 1; aci <= ColorCount; aci++)
            {
                sheet._styles.Add(new CadPlotStyle("Color_" + aci.ToString(CultureInfo.InvariantCulture)));
            }

            return sheet;
        }

        /// <summary>Makes a named table of one style, Normal, which changes nothing; <see cref="AddStyle"/> adds the others.</summary>
        /// <returns>The table, its <see cref="FileName"/> empty.</returns>
        public static CadPlotStyleSheet NewNamed()
        {
            var sheet = new CadPlotStyleSheet(true);
            sheet._styles.Add(new CadPlotStyle(NormalName) { ColorKind = 0xC3 });
            return sheet;
        }

        /// <summary>Gets whether the table is a named one (.stb) rather than a colour-dependent one (.ctb).</summary>
        public bool IsNamed { get; }

        /// <summary>
        /// Gets or sets the name of the file the table is kept in, as <c>monochrome.ctb</c>, which a drawing's plot settings
        /// name it by; empty for a table not yet saved. <see cref="Load"/> and the folder set it.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Gets or sets the description of the table, one line; empty for none.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the global scale factor is applied to the linetypes that are not ISO's (the editor's Apply global scale factor to non-ISO linetypes); off by default. Kept and not used.</summary>
        public bool ApplyGlobalScaleFactor { get; set; }

        /// <summary>Gets or sets the global scale factor of the table; 1 by default. Kept and not used.</summary>
        public double ScaleFactor { get; set; } = 1.0;

        /// <summary>Gets or sets whether the editor lists lineweights in inches (<c>custom_lineweight_display_units</c> 1) rather than millimetres (0): the table's lineweights are millimetres either way.</summary>
        public bool LineWeightsInInches { get; set; }

        /// <summary>Gets the lineweights the styles choose from, in millimetres: entry 0 is 0, "Use object lineweight".</summary>
        public IReadOnlyList<double> LineWeights => _lineWeights;

        /// <summary>
        /// Gets the styles: for a colour-dependent table the 255 of colours 1 to 255 in that order, for a named one Normal and
        /// then the others in the order they were added.
        /// </summary>
        public IReadOnlyList<CadPlotStyle> Styles => _styles;

        /// <summary>Gets the style of a colour of AutoCAD's index in a colour-dependent table.</summary>
        /// <param name="colorIndex">The index, 1 to 255.</param>
        /// <returns>The style.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table is a named one.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not 1 to 255.</exception>
        public CadPlotStyle StyleForColorIndex(int colorIndex)
        {
            if (IsNamed)
            {
                throw new InvalidOperationException("A named plot style table has no style for a colour index.");
            }

            if (colorIndex < 1 || colorIndex > ColorCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(colorIndex),
                    colorIndex,
                    "A colour of AutoCAD's index is 1 to 255."
                );
            }

            return _styles[colorIndex - 1];
        }

        /// <summary>Gets the style of a name in a named table, letters of either case the same.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The style; null where the table has none of the name, or is a colour-dependent one.</returns>
        public CadPlotStyle StyleNamed(string name)
        {
            if (name == null)
            {
                return null;
            }

            string wanted = name.Trim();
            return _styles.FirstOrDefault(s => string.Equals(s.Name, wanted, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds a style to a named table, as Add Style of the editor does: one that changes nothing, its colour kind a named
        /// table's, last in the list.
        /// </summary>
        /// <param name="name">The name, not one the table has, letters of either case the same, and none of <c>&lt; &gt; / \ " : ; ? * | , = `</c>.</param>
        /// <returns>The style.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table is a colour-dependent one, whose styles are the 255 colours.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is not a good one or the table has it.</exception>
        public CadPlotStyle AddStyle(string name)
        {
            if (!IsNamed)
            {
                throw new InvalidOperationException("A colour-dependent plot style table keeps its 255 styles.");
            }

            string problem = CheckStyleName(name);

            if (problem != null)
            {
                throw new ArgumentException(problem, nameof(name));
            }

            if (StyleNamed(name) != null)
            {
                throw new ArgumentException("The table has a style named " + name.Trim() + " already.", nameof(name));
            }

            var style = new CadPlotStyle(name) { ColorKind = 0xC3, Policy = 5 };
            _styles.Add(style);
            return style;
        }

        /// <summary>Takes a style out of a named table, as Delete Style of the editor does.</summary>
        /// <param name="name">The name, letters of either case the same.</param>
        /// <returns>Whether the table had it.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the table is a colour-dependent one, or the style is Normal.</exception>
        public bool RemoveStyle(string name)
        {
            if (!IsNamed)
            {
                throw new InvalidOperationException("A colour-dependent plot style table keeps its 255 styles.");
            }

            CadPlotStyle style = StyleNamed(name);

            if (style == null)
            {
                return false;
            }

            if (string.Equals(style.Name, NormalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The style Normal cannot be removed.");
            }

            return _styles.Remove(style);
        }

        /// <summary>Gives a style of a named table another name, as editing its name in the editor does.</summary>
        /// <param name="name">The name now, letters of either case the same.</param>
        /// <param name="newName">The new name, as <see cref="AddStyle"/> takes it; a change of letters alone is a rename.</param>
        /// <exception cref="InvalidOperationException">Thrown when the table is a colour-dependent one, or the style is Normal.</exception>
        /// <exception cref="ArgumentException">Thrown when the table has no style of the name, the new name is not a good one or another style has it.</exception>
        public void RenameStyle(string name, string newName)
        {
            if (!IsNamed)
            {
                throw new InvalidOperationException(
                    "The styles of a colour-dependent plot style table are named by colour."
                );
            }

            CadPlotStyle style = StyleNamed(name)
                ?? throw new ArgumentException("The table has no style named " + name + ".", nameof(name));

            if (string.Equals(style.Name, NormalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The style Normal cannot be renamed.");
            }

            string problem = CheckStyleName(newName);

            if (problem != null)
            {
                throw new ArgumentException(problem, nameof(newName));
            }

            CadPlotStyle other = StyleNamed(newName);

            if (other != null && !ReferenceEquals(other, style))
            {
                throw new ArgumentException("The table has a style named " + other.Name + " already.", nameof(newName));
            }

            style.Name = newName;
            style.LocalizedName = style.Name;
        }

        /// <summary>
        /// Says why a name cannot name a plot style, as AutoCAD refuses one: empty, longer than 255 letters, or with a
        /// <c>&lt; &gt; / \ " : ; ? * | , = `</c> in it.
        /// </summary>
        /// <param name="name">The name typed.</param>
        /// <returns>The reason, in a sentence; null where the name is a good one.</returns>
        public static string CheckStyleName(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return "A plot style needs a name.";
            }

            if (trimmed.Length > 255)
            {
                return "A plot style's name is 255 letters at most.";
            }

            if (
                trimmed.IndexOfAny(
                    new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`', '\r', '\n' }
                ) >= 0
            )
            {
                return "A plot style's name may not hold any of < > / \\ \" : ; ? * | , = `.";
            }

            return null;
        }

        /// <summary>Gets the millimetres a style plots at: its entry of <see cref="LineWeights"/>; 0 for "Use object lineweight" or an entry the table lacks.</summary>
        /// <param name="style">The style.</param>
        /// <returns>The lineweight in millimetres, 0 where the object's own is used.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        public double LineWeightOf(CadPlotStyle style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            return style.LineWeightIndex >= 0 && style.LineWeightIndex < _lineWeights.Count
                ? Math.Max(0.0, _lineWeights[style.LineWeightIndex])
                : 0.0;
        }

        /// <summary>
        /// Gets the entry of <see cref="LineWeights"/> that holds a lineweight, to the hundredth of a millimetre; the table
        /// gets a new entry at its end where it has none, as Edit Lineweights adds one.
        /// </summary>
        /// <param name="millimetres">The lineweight, 0 for "Use object lineweight" up to 10 mm.</param>
        /// <returns>The index of the entry.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the lineweight is not 0 to 10.</exception>
        public int LineWeightIndexOf(double millimetres)
        {
            if (!(millimetres >= 0.0) || millimetres > 10.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(millimetres),
                    millimetres,
                    "A lineweight is 0 to 10 millimetres."
                );
            }

            for (int i = 0; i < _lineWeights.Count; i++)
            {
                if (Math.Abs(_lineWeights[i] - millimetres) < 0.005)
                {
                    return i;
                }
            }

            _lineWeights.Add(Math.Round(millimetres, 2));
            return _lineWeights.Count - 1;
        }

        /// <summary>Changes an entry of <see cref="LineWeights"/>, as Edit Lineweights of the editor does; entry 0 stays "Use object lineweight".</summary>
        /// <param name="index">The entry, 1 up.</param>
        /// <param name="millimetres">The lineweight, above nought and up to 10 mm.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the entry is not 1 up to the last or the lineweight is not above 0 and up to 10.</exception>
        public void SetLineWeight(int index, double millimetres)
        {
            if (index < 1 || index >= _lineWeights.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "An entry of the lineweight list, 1 up.");
            }

            if (!(millimetres > 0.0) || millimetres > 10.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(millimetres),
                    millimetres,
                    "A lineweight is above 0 and up to 10 millimetres."
                );
            }

            _lineWeights[index] = Math.Round(millimetres, 2);
        }

        /// <summary>Makes a copy that changes apart from this one.</summary>
        /// <returns>The copy.</returns>
        public CadPlotStyleSheet Clone()
        {
            var copy = new CadPlotStyleSheet(IsNamed)
            {
                FileName = FileName,
                Description = Description,
                ApplyGlobalScaleFactor = ApplyGlobalScaleFactor,
                ScaleFactor = ScaleFactor,
                LineWeightsInInches = LineWeightsInInches,
            };
            copy._lineWeights.Clear();
            copy._lineWeights.AddRange(_lineWeights);
            copy._styles.AddRange(_styles.Select(s => s.Clone()));
            return copy;
        }

        /// <summary>
        /// Gives the text of the file, before it is packed: lines ended by a line feed, blocks indented by a space a level,
        /// numbers with six decimals, the text ended as the file ends it, by a nought byte. A colour-dependent table lists the
        /// colours (<c>aci_table</c>) and says <c>aci_table_available=TRUE</c>.
        /// </summary>
        /// <returns>The text.</returns>
        public string ToText()
        {
            var w = new StringBuilder();
            w.Append("description=\"").Append(OneLine(Description)).Append('\n');
            w.Append("aci_table_available=").Append(IsNamed ? "FALSE" : "TRUE").Append('\n');
            w.Append("scale_factor=").Append(Real(ScaleFactor)).Append('\n');
            w.Append("apply_factor=").Append(ApplyGlobalScaleFactor ? "TRUE" : "FALSE").Append('\n');
            w.Append("custom_lineweight_display_units=").Append(LineWeightsInInches ? "1" : "0").Append('\n');

            if (!IsNamed)
            {
                w.Append("aci_table{\n");

                for (int i = 0; i < _styles.Count; i++)
                {
                    w.Append(' ').Append(i.ToString(CultureInfo.InvariantCulture)).Append("=\"Color_");
                    w.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append('\n');
                }

                w.Append("}\n");
            }

            w.Append("plot_style{\n");

            for (int i = 0; i < _styles.Count; i++)
            {
                WriteStyle(w, i, _styles[i]);
            }

            w.Append("}\n");
            w.Append("custom_lineweight_table{\n");

            for (int i = 0; i < _lineWeights.Count; i++)
            {
                w.Append(' ').Append(i.ToString(CultureInfo.InvariantCulture)).Append('=');
                w.Append(Real(_lineWeights[i])).Append('\n');
            }

            w.Append("}\n");
            w.Append('\0');
            return w.ToString();
        }

        /// <summary>Gives the file: the head, the three numbers and the packed <see cref="ToText"/>, as <see cref="Save"/> writes it.</summary>
        /// <returns>The bytes.</returns>
        public byte[] ToBytes()
        {
            byte[] text = Encoding.UTF8.GetBytes(ToText());
            byte[] stream = Zlib(text);
            byte[] head = Encoding.ASCII.GetBytes(IsNamed ? NamedHead : ColorHead);

            using (var file = new MemoryStream())
            {
                file.Write(head, 0, head.Length);
                file.Write(BitConverter.GetBytes(Adler32(stream)), 0, 4);
                file.Write(BitConverter.GetBytes(text.Length), 0, 4);
                file.Write(BitConverter.GetBytes(stream.Length), 0, 4);
                file.Write(stream, 0, stream.Length);
                return file.ToArray();
            }
        }

        /// <summary>Writes the table to a .ctb or .stb file (<see cref="ToBytes"/>), the folder made where it is missing, and sets <see cref="FileName"/> to the file's name.</summary>
        /// <param name="path">The file, as D:\Plot Styles\site.ctb; a colour-dependent table is a .ctb, a named one a .stb.</param>
        /// <exception cref="ArgumentException">Thrown when the path is empty or its extension is not the table's.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file may not be written.</exception>
        public void Save(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A plot style table needs a path.", nameof(path));
            }

            string extension = IsNamed ? NamedExtension : ColorDependentExtension;

            if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "A " + (IsNamed ? "named" : "colour-dependent") + " plot style table is a " + extension + " file.",
                    nameof(path)
                );
            }

            string folder = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, ToBytes());
            FileName = Path.GetFileName(path);
        }

        /// <summary>Reads a .ctb or .stb file.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The table, its <see cref="FileName"/> the file's name.</returns>
        /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="InvalidDataException">Thrown when the file is not a plot style table: no head, or a damaged stream.</exception>
        public static CadPlotStyleSheet Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A plot style table needs a path.", nameof(path));
            }

            try
            {
                CadPlotStyleSheet sheet = Read(File.ReadAllBytes(path));
                sheet.FileName = Path.GetFileName(path);
                return sheet;
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException(
                    "\"" + Path.GetFileName(path) + "\" is not a plot style table: " + exception.Message,
                    exception
                );
            }
        }

        /// <summary>
        /// Reads a table from the bytes of a file. The head tells a colour-dependent table (<c>CTBVER1</c>) from a named one
        /// (<c>STBVER1</c>); the numbers after it are not checked, the stream is inflated to its end. A block or a line the
        /// reader does not know is passed over, a value that does not read keeps its default, a pen or a screening out of its
        /// range is brought to the range, and a colour-dependent table that lists fewer than 255 styles keeps the default of
        /// the others.
        /// </summary>
        /// <param name="bytes">The file's bytes.</param>
        /// <returns>The table, its <see cref="FileName"/> empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the bytes are null.</exception>
        /// <exception cref="InvalidDataException">Thrown when the bytes are not a plot style table: no head, or a damaged stream.</exception>
        public static CadPlotStyleSheet Read(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            bool named;
            int start;

            if (StartsWith(bytes, ColorHead))
            {
                named = false;
                start = ColorHead.Length;
            }
            else if (StartsWith(bytes, NamedHead))
            {
                named = true;
                start = NamedHead.Length;
            }
            else
            {
                throw new InvalidDataException(
                    "it has no plot style table head (PIAFILEVERSION_2.0,CTBVER1 or STBVER1)."
                );
            }

            start += 12;

            if (bytes.Length < start + 6 || bytes[start] != 0x78)
            {
                throw new InvalidDataException("its packed text is missing.");
            }

            byte[] text;

            try
            {
                using (var input = new MemoryStream(bytes, start + 2, bytes.Length - start - 2))
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    deflate.CopyTo(output);
                    text = output.ToArray();
                }
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException("its data is damaged.");
            }

            // A stream cut short inflates to what there is of it, which is less than the length the head gives.
            if (text.Length < BitConverter.ToInt32(bytes, start - 8))
            {
                throw new InvalidDataException("its data is damaged.");
            }

            return FromText(Decode(text), named);
        }

        /// <summary>Reads a table from its text, as <see cref="ToText"/> gives it and AutoCAD's files hold it.</summary>
        /// <param name="text">The text.</param>
        /// <param name="isNamed">Whether the table is a named one; the head of a file says it, a text does not.</param>
        /// <returns>The table, its <see cref="FileName"/> empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public static CadPlotStyleSheet FromText(string text, bool isNamed)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Block root = Parse(text);
            var sheet = new CadPlotStyleSheet(isNamed);
            sheet.Description = Text(root, "description");
            sheet.ScaleFactor = Number(root, "scale_factor", 1.0);
            sheet.ApplyGlobalScaleFactor = Flag(root, "apply_factor", false);
            sheet.LineWeightsInInches = Whole(root, "custom_lineweight_display_units", 0) != 0;

            Block weights = root.Blocks.FirstOrDefault(b => b.Name == "custom_lineweight_table");

            if (weights != null)
            {
                var read = new SortedDictionary<int, double>();

                foreach (KeyValuePair<string, string> pair in weights.Values)
                {
                    if (
                        int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                        && index < 1000
                        && double.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double mm)
                        && !double.IsNaN(mm)
                        && !double.IsInfinity(mm)
                    )
                    {
                        read[index] = mm;
                    }
                }

                if (read.Count > 0)
                {
                    sheet._lineWeights.Clear();

                    for (int i = 0; i <= read.Keys.Last(); i++)
                    {
                        sheet._lineWeights.Add(read.TryGetValue(i, out double mm) ? mm : 0.0);
                    }
                }
            }

            Block styles = root.Blocks.FirstOrDefault(b => b.Name == "plot_style");
            var found = new SortedDictionary<int, CadPlotStyle>();

            foreach (Block block in styles == null ? new List<Block>() : styles.Blocks)
            {
                if (
                    int.TryParse(block.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                    && index < 100000
                )
                {
                    found[index] = ReadStyle(block, index, isNamed);
                }
            }

            if (isNamed)
            {
                foreach (CadPlotStyle style in found.Values)
                {
                    if (sheet.StyleNamed(style.Name) == null)
                    {
                        sheet._styles.Add(style);
                    }
                }

                if (sheet.StyleNamed(NormalName) == null)
                {
                    sheet._styles.Insert(0, new CadPlotStyle(NormalName) { ColorKind = 0xC3 });
                }
            }
            else
            {
                for (int aci = 1; aci <= ColorCount; aci++)
                {
                    sheet._styles.Add(
                        found.TryGetValue(aci - 1, out CadPlotStyle style)
                            ? style
                            : new CadPlotStyle("Color_" + aci.ToString(CultureInfo.InvariantCulture))
                    );
                }
            }

            return sheet;
        }

        private static CadPlotStyle ReadStyle(Block block, int index, bool named)
        {
            string name = Text(block, "name");

            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            {
                name = named ? "Style_" + index.ToString(CultureInfo.InvariantCulture) : "Color_" + (index + 1);
            }

            var style = new CadPlotStyle(name)
            {
                LocalizedName = Text(block, "localized_name", name.Trim()),
                Description = Text(block, "description"),
                ColorKind = named ? 0xC3 : 0xC2,
                Policy = Whole(block, "color_policy", 1),
                PhysicalPen = Math.Max(0, Math.Min(32, Whole(block, "physical_pen_number", 0))),
                VirtualPen = Math.Max(0, Math.Min(255, Whole(block, "virtual_pen_number", 0))),
                Screening = Math.Max(0, Math.Min(255, Whole(block, "screen", 100))),
                LinePatternSize = Number(block, "linepattern_size", 0.5),
                Linetype = Math.Max(0, Math.Min(CadPlotStyle.ObjectLinetype, Whole(block, "linetype", 31))),
                Adaptive = Flag(block, "adaptive_linetype", true),
                LineWeightIndex = Math.Max(0, Whole(block, "lineweight", 0)),
                Fill = (CadPlotFill)Whole(block, "fill_style", (int)CadPlotFill.Object),
                LineEnd = (CadPlotLineEnd)Whole(block, "end_style", (int)CadPlotLineEnd.Object),
                LineJoin = (CadPlotLineJoin)Whole(block, "join_style", (int)CadPlotLineJoin.Object),
            };
            style.RawColor = Whole(block, "color", -1);
            style.RawModeColor = Whole(block, "mode_color", style.RawColor);

            if (!style.UsesObjectColor && ((style.RawModeColor >> 24) & 0xFF) != 0)
            {
                style.ColorKind = (style.RawModeColor >> 24) & 0xFF;
            }

            return style;
        }

        private static void WriteStyle(StringBuilder w, int index, CadPlotStyle s)
        {
            w.Append(' ').Append(index.ToString(CultureInfo.InvariantCulture)).Append("{\n");
            w.Append("  name=\"").Append(OneLine(s.Name)).Append('\n');
            w.Append("  localized_name=\"").Append(OneLine(s.LocalizedName ?? s.Name)).Append('\n');
            w.Append("  description=\"").Append(OneLine(s.Description)).Append('\n');
            w.Append("  color=").Append(s.RawColor.ToString(CultureInfo.InvariantCulture)).Append('\n');

            if (s.RawColor != -1)
            {
                w.Append("  mode_color=").Append(s.RawModeColor.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            w.Append("  color_policy=").Append(s.Policy.ToString(CultureInfo.InvariantCulture)).Append('\n');
            w.Append("  physical_pen_number=")
                .Append(s.PhysicalPen.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            w.Append("  virtual_pen_number=").Append(s.VirtualPen.ToString(CultureInfo.InvariantCulture)).Append('\n');
            w.Append("  screen=").Append(s.Screening.ToString(CultureInfo.InvariantCulture)).Append('\n');
            w.Append("  linepattern_size=").Append(Real(s.LinePatternSize)).Append('\n');
            w.Append("  linetype=").Append(s.Linetype.ToString(CultureInfo.InvariantCulture)).Append('\n');
            w.Append("  adaptive_linetype=").Append(s.Adaptive ? "TRUE" : "FALSE").Append('\n');
            w.Append("  lineweight=").Append(s.LineWeightIndex.ToString(CultureInfo.InvariantCulture)).Append('\n');
            w.Append("  fill_style=").Append(((int)s.Fill).ToString(CultureInfo.InvariantCulture)).Append('\n');
            w.Append("  end_style=").Append(((int)s.LineEnd).ToString(CultureInfo.InvariantCulture)).Append('\n');
            w.Append("  join_style=").Append(((int)s.LineJoin).ToString(CultureInfo.InvariantCulture)).Append('\n');
            w.Append(" }\n");
        }

        /// <summary>A block of the text: the values of its lines, in order, and the blocks inside it.</summary>
        private sealed class Block
        {
            public string Name = string.Empty;
            public readonly List<KeyValuePair<string, string>> Values = new List<KeyValuePair<string, string>>();
            public readonly List<Block> Blocks = new List<Block>();
        }

        /// <summary>The text as blocks: a line <c>name{</c> opens one, <c>}</c> closes it, <c>name=value</c> is a value of the block it is in.</summary>
        private static Block Parse(string text)
        {
            var root = new Block();
            var open = new Stack<Block>();
            open.Push(root);

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim('\r', '\0', ' ', '\t');

                if (line.Length == 0)
                {
                    continue;
                }

                int equals = line.IndexOf('=');

                if (equals < 0 && line == "}")
                {
                    if (open.Count > 1)
                    {
                        open.Pop();
                    }
                }
                else if (equals < 0 && line.EndsWith("{", StringComparison.Ordinal))
                {
                    var block = new Block { Name = line.Substring(0, line.Length - 1).Trim() };
                    open.Peek().Blocks.Add(block);
                    open.Push(block);
                }
                else if (equals > 0)
                {
                    open.Peek()
                        .Values.Add(
                            new KeyValuePair<string, string>(
                                line.Substring(0, equals).Trim(),
                                Value(line.Substring(equals + 1))
                            )
                        );
                }
            }

            return root;
        }

        /// <summary>A value as the file spells it: a string has a quote before it and none after, a number may have a note after it, "0.0076200000000 (+7.Z+" that is not read.</summary>
        private static string Value(string raw)
        {
            string value = raw.Trim();

            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                return value.Substring(1);
            }

            int space = value.IndexOf(' ');
            return value.EndsWith(")", StringComparison.Ordinal) && space > 0 ? value.Substring(0, space) : value;
        }

        private static string Text(Block block, string key, string fallback = "")
        {
            foreach (KeyValuePair<string, string> pair in block.Values)
            {
                if (pair.Key == key)
                {
                    return pair.Value;
                }
            }

            return fallback;
        }

        private static int Whole(Block block, string key, int fallback)
        {
            string text = Text(block, key, null);
            return
                text != null
                && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }

        private static double Number(Block block, string key, double fallback)
        {
            string text = Text(block, key, null);
            return
                text != null
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && !double.IsNaN(value)
                && !double.IsInfinity(value)
                ? value
                : fallback;
        }

        private static bool Flag(Block block, string key, bool fallback)
        {
            string text = Text(block, key, null);

            if (string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase) ? false : fallback;
        }

        private static string Real(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);

        private static string OneLine(string text) =>
            (text ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace("\0", string.Empty);

        /// <summary>The text of a file: UTF-8 where it is, else Latin-1, which any byte is, and the nought that ends it taken off.</summary>
        private static string Decode(byte[] text)
        {
            int length = text.Length;

            while (length > 0 && text[length - 1] == 0)
            {
                length--;
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(text, 0, length);
            }
            catch (ArgumentException)
            {
                return Encoding.GetEncoding(28591).GetString(text, 0, length);
            }
        }

        private static bool StartsWith(byte[] bytes, string head)
        {
            if (bytes.Length < head.Length)
            {
                return false;
            }

            for (int i = 0; i < head.Length; i++)
            {
                if (bytes[i] != (byte)head[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The text packed as a zlib stream: its head, the deflated text and the Adler-32 of the text, high byte first.</summary>
        private static byte[] Zlib(byte[] text)
        {
            using (var packed = new MemoryStream())
            {
                packed.WriteByte(0x78);
                packed.WriteByte(0x9C);

                using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
                {
                    deflate.Write(text, 0, text.Length);
                }

                uint sum = Adler32(text);
                packed.WriteByte((byte)(sum >> 24));
                packed.WriteByte((byte)(sum >> 16));
                packed.WriteByte((byte)(sum >> 8));
                packed.WriteByte((byte)sum);
                return packed.ToArray();
            }
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1;
            uint b = 0;

            foreach (byte value in data)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }

        /// <inheritdoc/>
        public override string ToString() =>
            string.IsNullOrEmpty(FileName) ? (IsNamed ? "(named)" : "(colour)") : FileName;
    }
}
