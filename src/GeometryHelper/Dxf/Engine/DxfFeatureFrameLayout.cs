using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The rows and cells of a feature control frame laid out as AutoCAD 2021 lays them out (probes GHTOL1 and GHTQ2, frames
    /// 0.18 high in Arial): each cell from the first ink of its first letter to the last ink of its last, the letters between
    /// taking their advances, half the text height either side; a cell of no ink taking no room. A letter of AutoCAD's gdt
    /// font in <c>{\Fgdt;...}</c> is a symbol the width of its ink, with room of its own on its left and right: two
    /// positions side by side are 3.19 text heights wide, a diameter sign before 0.1 sets the words 1.5/21 of the height on
    /// from its ink, not 2/21, so that <c>{\Fgdt;n}0.1{\Fgdt;m}</c> is 1.009563 wide at 0.18. Words inked past their pen,
    /// as Arial's A and f, set what follows on from their ink (probe GHTQ3).
    /// </summary>
    internal static class DxfFeatureFrameLayout
    {
        // The letters of the gdt font drawn as symbols: the width of the ink, the room on its left and on its right, in 21sts
        // of the text height, as AutoCAD 2021's console measures each alone, after an A and before one (probe GHTQ2).
        private static readonly Dictionary<char, double[]> Letters = new Dictionary<char, double[]>
        {
            ['j'] = new[] { 31.5, 2.0, 2.0 },
            ['r'] = new[] { 31.5, 2.0, 2.0 },
            ['i'] = new[] { 31.5, 2.0, 2.0 },
            ['f'] = new[] { 30.5, 2.0, 2.0 },
            ['b'] = new[] { 42.0, 2.0, 2.0 },
            ['a'] = new[] { 31.5, 2.0, 2.0 },
            ['g'] = new[] { 42.0, 2.0, 2.0 },
            ['c'] = new[] { 43.5, 2.0, 2.0 },
            ['e'] = new[] { 31.5, 2.0, 2.0 },
            ['u'] = new[] { 42.0, 2.0, 2.0 },
            ['d'] = new[] { 42.0, 2.0, 2.0 },
            ['k'] = new[] { 42.0, 2.0, 2.0 },
            ['h'] = new[] { 31.25, 2.0, 2.25 },
            ['t'] = new[] { 54.25, 2.0, 2.25 },
            ['n'] = new[] { 21.0, 1.5, 1.5 },
            ['m'] = new[] { 31.5, 2.0, 2.0 },
            ['l'] = new[] { 31.5, 2.0, 2.0 },
            ['s'] = new[] { 31.5, 2.0, 2.0 },
            ['p'] = new[] { 31.5, 2.0, 2.0 },
        };

        /// <summary>Whether a letter of the gdt font is drawn as a symbol: the 19 of the symbols, the material conditions, the diameter and the projected zone.</summary>
        internal static bool Has(char code) => Letters.ContainsKey(code);

        /// <summary>The width of a symbol's ink over the text height: 1.5 for the position, 1 for the diameter; nought for a letter that is none.</summary>
        internal static double Ink(char code) => Letters.TryGetValue(code, out double[] l) ? l[0] / 21.0 : 0.0;

        /// <summary>The room on a symbol's left over the text height: 2/21, 1.5/21 for the diameter.</summary>
        internal static double Left(char code) => (Letters.TryGetValue(code, out double[] l) ? l[1] : 2.0) / 21.0;

        /// <summary>The room on a symbol's right over the text height: 2/21, 1.5/21 for the diameter, 2.25/21 for the runouts.</summary>
        internal static double Right(char code) => (Letters.TryGetValue(code, out double[] l) ? l[2] : 2.0) / 21.0;

        /// <summary>The rows of a frame's words, a line feed between them; one row of nothing for no words.</summary>
        internal static string[] Rows(string text) => (text ?? string.Empty).Replace("\r", string.Empty).Split('\n');

        /// <summary>The cells of a row, <c>%%v</c> between them, letters of either case.</summary>
        internal static List<string> Cells(string row)
        {
            var cells = new List<string>();
            int from = 0;

            for (int i = 0; i + 2 < row.Length; i++)
            {
                if (row[i] == '%' && row[i + 1] == '%' && (row[i + 2] == 'v' || row[i + 2] == 'V'))
                {
                    cells.Add(row.Substring(from, i - from));
                    from = i + 3;
                    i += 2;
                }
            }

            cells.Add(row.Substring(from));
            return cells;
        }

        /// <summary>
        /// The pieces of a cell: runs of words, and the letters of the gdt font in <c>{\Fgdt;...}</c> each a symbol of their
        /// own; a letter that is no symbol stays in the words, its font switch with it.
        /// </summary>
        internal static List<KeyValuePair<char?, string>> Pieces(string cell)
        {
            var pieces = new List<KeyValuePair<char?, string>>();
            int i = 0;
            int from = 0;

            while (i < cell.Length)
            {
                int end = cell.IndexOf('}', i);

                if (
                    cell[i] == '{'
                    && end > i
                    && string.Compare(cell, i, "{\\Fgdt;", 0, 7, StringComparison.OrdinalIgnoreCase) == 0
                    && Drawn(cell, i + 7, end)
                )
                {
                    if (i > from)
                    {
                        pieces.Add(new KeyValuePair<char?, string>(null, cell.Substring(from, i - from)));
                    }

                    for (int k = i + 7; k < end; k++)
                    {
                        pieces.Add(new KeyValuePair<char?, string>(cell[k], null));
                    }

                    i = end + 1;
                    from = i;
                    continue;
                }

                i++;
            }

            if (from < cell.Length)
            {
                pieces.Add(new KeyValuePair<char?, string>(null, cell.Substring(from)));
            }

            return pieces;
        }

        private static bool Drawn(string cell, int from, int end)
        {
            for (int k = from; k < end; k++)
            {
                if (!Has(cell[k]))
                {
                    return false;
                }
            }

            return end > from;
        }

        /// <summary>
        /// Lays out a cell at a text height: its width, its ink and the text height, nought for a cell of no ink. Each symbol
        /// is placed by the middle of its ink and each run of words by where its pen starts, from the cell's pen start, into
        /// <paramref name="placed"/>; <paramref name="first"/> is where the ink starts. <paramref name="measure"/> gives the
        /// ink's left and right (NaN for none), the pen's last place and its first, of a run of words; null for words it does
        /// not measure, which take no room.
        /// </summary>
        internal static double Cell(
            string cell,
            double h,
            Func<string, double[]> measure,
            List<KeyValuePair<double, object>> placed,
            out double first
        )
        {
            double pen = 0.0;
            double last = 0.0;
            first = double.NaN;

            foreach (KeyValuePair<char?, string> piece in Pieces(cell))
            {
                if (piece.Key.HasValue)
                {
                    char code = piece.Key.Value;
                    double ink = Ink(code) * h;
                    double start = pen + Left(code) * h;
                    first = double.IsNaN(first) ? start : first;
                    last = start + ink;
                    placed.Add(new KeyValuePair<double, object>(start + ink / 2.0, code));
                    pen = last + Right(code) * h;
                    continue;
                }

                double[] run = measure(piece.Value);

                if (run == null)
                {
                    continue;
                }

                if (!double.IsNaN(run[0]))
                {
                    first = double.IsNaN(first) ? pen + run[0] - run[3] : first;
                    last = pen + run[1] - run[3];
                }

                placed.Add(new KeyValuePair<double, object>(pen, piece.Value));
                pen += run[2] - run[3];

                // Words inked past their pen, as Arial's A and f, set what follows on from their ink (probe GHTQ3).
                pen = double.IsNaN(run[0]) ? pen : Math.Max(pen, last);
            }

            return double.IsNaN(first) ? 0.0 : last - first + h;
        }
    }
}
