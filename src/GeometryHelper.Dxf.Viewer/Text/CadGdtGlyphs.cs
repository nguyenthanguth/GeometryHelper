using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The symbols of geometric dimensioning and tolerancing the presenter draws for the letters of AutoCAD's gdt font, one set
    /// for the dialogs and the canvas: each a stroke path in a box 24 by 24, y down, as SVG and WPF read one, with round caps
    /// and joins, the arrowheads of the runouts filled, the material conditions a circle with their letter in it. The letter
    /// is the one group 1 of a TOLERANCE carries, as <c>{\Fgdt;j}</c> for the position.
    /// </summary>
    /// <remarks>
    /// The canvas scales the box to four thirds of the text height, so that the ink is the text height tall, and centres it in
    /// the room AutoCAD 2021 gives the letter in the gdt font (<see cref="InkWidth"/>, measured in its console: the position
    /// 1.5 times the text height, the diameter 1, the flatness 2.071).
    /// </remarks>
    public static class CadGdtGlyphs
    {
        private const string Circle = "M12 12m-9.5 0a9.5 9.5 0 1 0 19 0a9.5 9.5 0 1 0-19 0";

        private static readonly Dictionary<char, Glyph> Glyphs = new Dictionary<char, Glyph>
        {
            ['j'] = new Glyph(
                "Position",
                1.5,
                "M12 3v18M3 12h18M12 12m-5.5 0a5.5 5.5 0 1 0 11 0a5.5 5.5 0 1 0-11 0",
                string.Empty,
                null
            ),
            ['r'] = new Glyph(
                "Concentricity",
                1.5,
                "M12 12m-7.5 0a7.5 7.5 0 1 0 15 0a7.5 7.5 0 1 0-15 0M12 12m-3.5 0a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0-7 0",
                string.Empty,
                null
            ),
            ['i'] = new Glyph("Symmetry", 1.5, "M4 12h16M7 7.5h10M7 16.5h10", string.Empty, null),
            ['f'] = new Glyph("Parallelism", 1.452381, "M5 19 11 5M13 19 19 5", string.Empty, null),
            ['b'] = new Glyph("Perpendicularity", 2.0, "M4 19h16M12 19V5", string.Empty, null),
            ['a'] = new Glyph("Angularity", 1.5, "M4 19h16M4 19 18 7", string.Empty, null),
            ['g'] = new Glyph(
                "Cylindricity",
                2.0,
                "M12 12m-5 0a5 5 0 1 0 10 0a5 5 0 1 0-10 0M4 20 10 4M14 20 20 4",
                string.Empty,
                null
            ),
            ['c'] = new Glyph("Flatness", 2.071429, "M3 17h13l5-10H8z", string.Empty, null),
            ['e'] = new Glyph("Circularity", 1.5, "M12 12m-7 0a7 7 0 1 0 14 0a7 7 0 1 0-14 0", string.Empty, null),
            ['u'] = new Glyph("Straightness", 2.0, "M4 12h16", string.Empty, null),
            ['d'] = new Glyph("Profile of a surface", 2.0, "M4 16a8 8 0 0 1 16 0z", string.Empty, null),
            ['k'] = new Glyph("Profile of a line", 2.0, "M4 16a8 8 0 0 1 16 0", string.Empty, null),
            ['h'] = new Glyph("Circular runout", 1.488095, "M7 20 15 7", "M17 3.8 13.3 6 16.7 8z", null),
            ['t'] = new Glyph(
                "Total runout",
                2.583333,
                "M3 20h16M5 20 10 12M12 20 17 12",
                "M12 8.8 8.3 10.9 11.7 13.1zM19 8.8 15.3 10.9 18.7 13.1z",
                null
            ),
            ['n'] = new Glyph(
                "Diameter",
                1.0,
                "M12 12m-6.5 0a6.5 6.5 0 1 0 13 0a6.5 6.5 0 1 0-13 0M4.5 19.5 19.5 4.5",
                string.Empty,
                null
            ),
            ['m'] = new Glyph("Maximum material condition", 1.5, Circle, string.Empty, "M"),
            ['l'] = new Glyph("Least material condition", 1.5, Circle, string.Empty, "L"),
            ['s'] = new Glyph("Regardless of feature size", 1.5, Circle, string.Empty, "S"),
            ['p'] = new Glyph("Projected tolerance zone", 1.5, Circle, string.Empty, "P"),
        };

        /// <summary>
        /// The room AutoCAD 2021 leaves on either side of the ink of a letter of the gdt font, over the text height: 2/21, so
        /// that two positions side by side are 3.19 heights wide.
        /// </summary>
        public const double SideBearing = 2.0 / 21.0;

        /// <summary>Gets whether the gdt font letter is one of the symbols drawn here.</summary>
        /// <param name="code">The letter, as <c>j</c> for the position.</param>
        /// <returns>True for the 19 letters of the symbols, the material conditions, the diameter and the projected zone.</returns>
        public static bool Has(char code) => Glyphs.ContainsKey(code);

        /// <summary>Gets the name of a symbol, as "Position"; empty for a letter that is none.</summary>
        /// <param name="code">The letter.</param>
        /// <returns>The name.</returns>
        public static string NameOf(char code) => Glyphs.TryGetValue(code, out Glyph g) ? g.Name : string.Empty;

        /// <summary>
        /// Gets the width of the ink of a symbol in AutoCAD's gdt font, over the text height, as AutoCAD 2021's console measures
        /// it alone in an MTEXT 1 high: 1.5 for the position, 1 for the diameter; nought for a letter that is none.
        /// </summary>
        /// <param name="code">The letter.</param>
        /// <returns>The width.</returns>
        public static double InkWidth(char code) => Glyphs.TryGetValue(code, out Glyph g) ? g.Ink : 0.0;

        /// <summary>Gets the stroke path of a symbol in its box 24 by 24, as SVG writes one; empty for a letter that is none.</summary>
        /// <param name="code">The letter.</param>
        /// <returns>The path.</returns>
        public static string StrokeOf(char code) => Glyphs.TryGetValue(code, out Glyph g) ? g.Stroke : string.Empty;

        /// <summary>Gets the path of the filled parts of a symbol, the arrowheads of a runout; empty for one that has none.</summary>
        /// <param name="code">The letter.</param>
        /// <returns>The path.</returns>
        public static string FillOf(char code) => Glyphs.TryGetValue(code, out Glyph g) ? g.Fill : string.Empty;

        /// <summary>Gets the letter a symbol holds in its circle, as M for the maximum material condition; null for one that holds none.</summary>
        /// <param name="code">The letter of the gdt font.</param>
        /// <returns>The letter in the circle.</returns>
        public static string LetterOf(char code) => Glyphs.TryGetValue(code, out Glyph g) ? g.Letter : null;

        /// <summary>
        /// The figures of a path as runs of points in its box, y down: each line and arc it draws, an arc broken into pieces of
        /// at most a 24th of a turn, a run that closes ending on its first point. Reads M, L, H, V, A and Z, upper case taken
        /// in the box and lower case from the pen, and a number after a pair of M as a line, as SVG reads them.
        /// </summary>
        internal static List<List<double[]>> Figures(string path)
        {
            var figures = new List<List<double[]>>();
            List<double[]> run = null;
            double x = 0.0;
            double y = 0.0;
            int i = 0;
            char command = 'M';

            while (true)
            {
                Skip(path, ref i);

                if (i >= path.Length)
                {
                    break;
                }

                if (char.IsLetter(path[i]))
                {
                    command = path[i++];

                    if (command == 'z' || command == 'Z')
                    {
                        if (run != null && run.Count > 1)
                        {
                            run.Add(run[0]);
                            x = run[0][0];
                            y = run[0][1];
                        }

                        run = null;
                        continue;
                    }
                }

                bool relative = char.IsLower(command);
                double baseX = relative ? x : 0.0;
                double baseY = relative ? y : 0.0;

                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        x = baseX + Number(path, ref i);
                        y = baseY + Number(path, ref i);
                        run = new List<double[]> { new[] { x, y } };
                        figures.Add(run);
                        command = relative ? 'l' : 'L';
                        break;
                    case 'L':
                        x = baseX + Number(path, ref i);
                        y = baseY + Number(path, ref i);
                        run = Line(figures, run, x, y);
                        break;
                    case 'H':
                        x = baseX + Number(path, ref i);
                        run = Line(figures, run, x, y);
                        break;
                    case 'V':
                        y = baseY + Number(path, ref i);
                        run = Line(figures, run, x, y);
                        break;
                    case 'A':
                    {
                        double rx = Number(path, ref i);
                        Number(path, ref i);
                        Number(path, ref i);
                        bool large = Number(path, ref i) != 0.0;
                        bool sweep = Number(path, ref i) != 0.0;
                        double toX = baseX + Number(path, ref i);
                        double toY = baseY + Number(path, ref i);
                        run = Arc(figures, run, x, y, toX, toY, rx, large, sweep);
                        x = toX;
                        y = toY;
                        break;
                    }

                    default:
                        throw new FormatException("The path has a command the glyphs do not use: " + command + ".");
                }
            }

            figures.RemoveAll(f => f.Count < 2);
            return figures;
        }

        private static List<double[]> Line(List<List<double[]>> figures, List<double[]> run, double x, double y)
        {
            if (run == null)
            {
                run = new List<double[]>();
                figures.Add(run);
            }

            run.Add(new[] { x, y });
            return run;
        }

        /// <summary>An arc of a circle from the pen to a point, as SVG draws one of equal radii, broken into pieces along the run.</summary>
        private static List<double[]> Arc(
            List<List<double[]>> figures,
            List<double[]> run,
            double fromX,
            double fromY,
            double toX,
            double toY,
            double radius,
            bool large,
            bool sweep
        )
        {
            double dx = (toX - fromX) / 2.0;
            double dy = (toY - fromY) / 2.0;
            double half = Math.Sqrt(dx * dx + dy * dy);
            double r = Math.Max(Math.Abs(radius), half);
            double rise = Math.Sqrt(Math.Max(0.0, r * r - half * half));

            // The centre on the side of the chord SVG puts it: on the left of the way where the sweep and the large flag differ.
            double side = large == sweep ? -1.0 : 1.0;
            double cx = fromX + dx;
            double cy = fromY + dy;

            if (half > 0.0)
            {
                cx += side * -dy / half * rise;
                cy += side * dx / half * rise;
            }

            double start = Math.Atan2(fromY - cy, fromX - cx);
            double end = Math.Atan2(toY - cy, toX - cx);
            double turn = end - start;

            // In a box of y down a sweep of 1 runs the way the angles grow.
            if (sweep && turn < 0.0)
            {
                turn += 2.0 * Math.PI;
            }
            else if (!sweep && turn > 0.0)
            {
                turn -= 2.0 * Math.PI;
            }

            int pieces = Math.Max(2, (int)Math.Ceiling(Math.Abs(turn) / (Math.PI / 12.0)));

            for (int k = 1; k <= pieces; k++)
            {
                double angle = start + turn * k / pieces;
                run = Line(figures, run, cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
            }

            return run;
        }

        private static void Skip(string path, ref int i)
        {
            while (i < path.Length && (path[i] == ' ' || path[i] == ','))
            {
                i++;
            }
        }

        private static double Number(string path, ref int i)
        {
            Skip(path, ref i);
            int start = i;

            if (i < path.Length && (path[i] == '-' || path[i] == '+'))
            {
                i++;
            }

            bool dot = false;

            while (i < path.Length && (char.IsDigit(path[i]) || (path[i] == '.' && !dot)))
            {
                dot |= path[i] == '.';
                i++;
            }

            return double.Parse(
                path.Substring(start, i - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture
            );
        }

        private sealed class Glyph
        {
            public Glyph(string name, double ink, string stroke, string fill, string letter)
            {
                Name = name;
                Ink = ink;
                Stroke = stroke;
                Fill = fill;
                Letter = letter;
            }

            public string Name { get; }

            public double Ink { get; }

            public string Stroke { get; }

            public string Fill { get; }

            public string Letter { get; }
        }
    }
}
