using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>A material condition of the Geometric Tolerance dialog, as AutoCAD's Material Condition dialog offers it.</summary>
    public enum CadMaterialCondition
    {
        /// <summary>None: the box left blank.</summary>
        None,

        /// <summary>M, at maximum material condition (MMC): the gdt letter m.</summary>
        Maximum,

        /// <summary>L, at least material condition (LMC): the gdt letter l.</summary>
        Least,

        /// <summary>S, regardless of feature size (RFS): the gdt letter s.</summary>
        RegardlessOfSize,
    }

    /// <summary>A tolerance of a row of the Geometric Tolerance dialog: the diameter sign, the value and the material condition.</summary>
    public sealed class CadToleranceValue
    {
        /// <summary>Gets or sets whether the value is a diameter, written before it with the gdt letter n.</summary>
        public bool Diameter { get; set; }

        /// <summary>Gets or sets the value as typed, as "0.1"; empty for none.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Gets or sets the material condition written after the value.</summary>
        public CadMaterialCondition Material { get; set; }

        /// <summary>Gets whether the tolerance holds nothing.</summary>
        public bool IsEmpty => !Diameter && string.IsNullOrWhiteSpace(Value) && Material == CadMaterialCondition.None;
    }

    /// <summary>A datum of a row of the Geometric Tolerance dialog: its reference letter and its material condition.</summary>
    public sealed class CadToleranceDatum
    {
        /// <summary>Gets or sets the reference letter as typed, as "A"; empty for none.</summary>
        public string Letter { get; set; } = string.Empty;

        /// <summary>Gets or sets the material condition written after the letter.</summary>
        public CadMaterialCondition Material { get; set; }

        /// <summary>Gets whether the datum holds nothing.</summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Letter) && Material == CadMaterialCondition.None;
    }

    /// <summary>
    /// A row of the Geometric Tolerance dialog: the symbol, by its letter of the gdt font (<c>j</c> for the position, none for
    /// a blank), two tolerances and three datums.
    /// </summary>
    public sealed class CadToleranceRow
    {
        /// <summary>Gets or sets the symbol, its letter of the gdt font as <see cref="CadGdtGlyphs"/> names it; null for none.</summary>
        public char? Symbol { get; set; }

        /// <summary>Gets the first tolerance.</summary>
        public CadToleranceValue Tolerance1 { get; } = new CadToleranceValue();

        /// <summary>Gets the second tolerance.</summary>
        public CadToleranceValue Tolerance2 { get; } = new CadToleranceValue();

        /// <summary>Gets the three datums, the primary first.</summary>
        public IReadOnlyList<CadToleranceDatum> Datums { get; } =
            new[] { new CadToleranceDatum(), new CadToleranceDatum(), new CadToleranceDatum() };

        /// <summary>
        /// Gets the row as group 1 of a TOLERANCE writes it: its six cells with <c>%%v</c> between them, a cell left empty kept
        /// empty, those after the last that holds something left out; empty for a row of nothing. The position of a diameter 0.1
        /// at MMC to A, B and C is <c>{\Fgdt;j}%%v{\Fgdt;n}0.1{\Fgdt;m}%%vA%%vB%%vC</c>.
        /// </summary>
        public string Written
        {
            get
            {
                var cells = new List<string>
                {
                    Symbol.HasValue ? Gdt(Symbol.Value) : string.Empty,
                    Cell(Tolerance1),
                    Cell(Tolerance2),
                };

                foreach (CadToleranceDatum datum in Datums)
                {
                    cells.Add((datum.Letter ?? string.Empty).Trim() + Material(datum.Material));
                }

                while (cells.Count > 0 && cells[cells.Count - 1].Length == 0)
                {
                    cells.RemoveAt(cells.Count - 1);
                }

                return string.Join("%%v", cells);
            }
        }

        private static string Cell(CadToleranceValue value) =>
            (value.Diameter ? Gdt('n') : string.Empty)
            + (value.Value ?? string.Empty).Trim()
            + Material(value.Material);

        internal static string Material(CadMaterialCondition material)
        {
            switch (material)
            {
                case CadMaterialCondition.Maximum:
                    return Gdt('m');
                case CadMaterialCondition.Least:
                    return Gdt('l');
                case CadMaterialCondition.RegardlessOfSize:
                    return Gdt('s');
                default:
                    return string.Empty;
            }
        }

        internal static string Gdt(char code) => "{\\Fgdt;" + code + "}";
    }

    /// <summary>
    /// AutoCAD's Geometric Tolerance dialog, as data the TOLERANCE command, LEADER, QLEADER and TEXTEDIT share with the
    /// window: two rows, the height of a projected zone, its P, and the datum identifier, every box empty when it opens for a
    /// new frame and filled from its words for one edited. OK hands back the words of the frame
    /// (<see cref="Text"/>); a frame of no words is drawn by none.
    /// </summary>
    public sealed class CadToleranceRequest
    {
        internal CadToleranceRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the two rows, the first on top.</summary>
        public IReadOnlyList<CadToleranceRow> Rows { get; } = new[] { new CadToleranceRow(), new CadToleranceRow() };

        /// <summary>Gets or sets the height of a projected tolerance zone, as typed; empty for none.</summary>
        public string Height { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the projected tolerance zone's P is written after the height.</summary>
        public bool ProjectedZone { get; set; }

        /// <summary>Gets or sets the datum identifier, as typed; empty for none.</summary>
        public string DatumIdentifier { get; set; } = string.Empty;

        /// <summary>Gets or sets what the dialog was closed with: OK draws the frame; Cancel until set.</summary>
        public CadDialogAction Action { get; set; } = CadDialogAction.Cancel;

        /// <summary>
        /// Gets the words of the frame, as group 1 of a TOLERANCE holds them: each row that holds something (the two rows, the
        /// height with its P, the datum identifier) a line feed apart; empty where every box is. Both rows of board
        /// GeomToleranceFull with a zone 5 high and the identifier D are
        /// <c>{\Fgdt;j}%%v{\Fgdt;n}0.1{\Fgdt;m}%%vA%%vB%%vC</c>, <c>{\Fgdt;c}%%v0.05</c>, <c>5{\Fgdt;p}</c> and <c>D</c>.
        /// </summary>
        public string Text
        {
            get
            {
                var lines = new List<string>();

                foreach (CadToleranceRow row in Rows)
                {
                    lines.Add(row.Written);
                }

                lines.Add((Height ?? string.Empty).Trim() + (ProjectedZone ? CadToleranceRow.Gdt('p') : string.Empty));
                lines.Add((DatumIdentifier ?? string.Empty).Trim());
                var text = new StringBuilder();

                foreach (string line in lines)
                {
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    if (text.Length > 0)
                    {
                        text.Append('\n');
                    }

                    text.Append(line);
                }

                return text.ToString();
            }
        }

        /// <summary>
        /// Fills the boxes from the words of a frame, as <see cref="Text"/> writes them, so that the dialog opens on the frame
        /// edited, as AutoCAD 2021's TEXTEDIT opens it: each of the first two lines that is a row (it has <c>%%v</c>, or is a
        /// symbol alone) a row, its symbol, tolerances with their diameter sign and material condition, and datums with theirs;
        /// then a line of a number, or one ending in the P of the projected zone, the height; then the datum identifier. Lines
        /// of nothing are passed over, and what reads as none of these is left out. <c>{\Fgdt;j}%%v{\Fgdt;n}0.1{\Fgdt;m}%%vA</c>
        /// and <c>5{\Fgdt;p}</c> read as the position, a diameter 0.1 at MMC to A, and a zone 5 high.
        /// </summary>
        internal void Read(string text)
        {
            var lines = new List<string>();

            foreach (string line in DxfFeatureFrameLayout.Rows(text))
            {
                if (line.Trim().Length > 0)
                {
                    lines.Add(line);
                }
            }

            int at = 0;

            for (int r = 0; r < Rows.Count && at < lines.Count && IsRow(lines[at]); r++, at++)
            {
                ReadRow(lines[at], Rows[r]);
            }

            if (at < lines.Count && IsHeight(lines[at], out string height, out bool zone))
            {
                Height = height;
                ProjectedZone = zone;
                at++;
            }

            if (at < lines.Count)
            {
                DatumIdentifier = lines[at].Trim();
            }
        }

        private static bool IsRow(string line) =>
            line.IndexOf("%%v", StringComparison.OrdinalIgnoreCase) >= 0
            || (Code(line.Trim(), out char symbol) && Symbols.IndexOf(symbol) >= 0);

        private static bool IsHeight(string line, out string height, out bool zone)
        {
            string trimmed = line.Trim();
            zone = trimmed.EndsWith(CadToleranceRow.Gdt('p'), StringComparison.OrdinalIgnoreCase);
            height = zone ? trimmed.Substring(0, trimmed.Length - 9).Trim() : trimmed;
            return zone
                || double.TryParse(
                    height,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _
                );
        }

        private static void ReadRow(string line, CadToleranceRow row)
        {
            List<string> cells = DxfFeatureFrameLayout.Cells(line);
            row.Symbol = Code(cells[0].Trim(), out char symbol) && Symbols.IndexOf(symbol) >= 0 ? symbol : (char?)null;

            for (int t = 0; t < 2 && t + 1 < cells.Count; t++)
            {
                CadToleranceValue value = t == 0 ? row.Tolerance1 : row.Tolerance2;
                string cell = cells[t + 1].Trim();
                value.Diameter = cell.StartsWith(CadToleranceRow.Gdt('n'), StringComparison.OrdinalIgnoreCase);
                cell = value.Diameter ? cell.Substring(9) : cell;
                value.Material = MaterialAtEnd(ref cell);
                value.Value = cell.Trim();
            }

            for (int d = 0; d < row.Datums.Count && d + 3 < cells.Count; d++)
            {
                string cell = cells[d + 3].Trim();
                row.Datums[d].Material = MaterialAtEnd(ref cell);
                row.Datums[d].Letter = cell.Trim();
            }
        }

        // The letters of the symbols of the Symbol dialog.
        private const string Symbols = "jrifbagceudkht";

        /// <summary>The material condition a cell ends with, taken off it; none where it ends with none.</summary>
        private static CadMaterialCondition MaterialAtEnd(ref string cell)
        {
            foreach (
                CadMaterialCondition material in new[]
                {
                    CadMaterialCondition.Maximum,
                    CadMaterialCondition.Least,
                    CadMaterialCondition.RegardlessOfSize,
                }
            )
            {
                string code = CadToleranceRow.Material(material);

                if (cell.EndsWith(code, StringComparison.OrdinalIgnoreCase))
                {
                    cell = cell.Substring(0, cell.Length - code.Length);
                    return material;
                }
            }

            return CadMaterialCondition.None;
        }

        /// <summary>Whether a cell is one letter of the gdt font alone, <c>{\Fgdt;j}</c>, and which.</summary>
        private static bool Code(string cell, out char code)
        {
            code = '\0';

            if (cell.Length != 9 || !cell.StartsWith("{\\Fgdt;", StringComparison.OrdinalIgnoreCase) || cell[8] != '}')
            {
                return false;
            }

            code = cell[7];
            return true;
        }
    }
}
