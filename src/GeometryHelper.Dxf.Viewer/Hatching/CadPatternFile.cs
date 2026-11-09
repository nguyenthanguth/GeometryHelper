using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A line of a hatch pattern as a .pat file writes it: its angle in degrees, the point it runs through, the step to the
    /// next copy along the line and across it, in the line's own frame, and its dashes. ANSI31 is the one line
    /// <c>45, 0,0, 0,3.175</c>: at 45 degrees through the origin, each copy 3.175 across from the last, solid.
    /// </summary>
    public sealed class CadHatchPatternLine
    {
        /// <summary>Makes a line of a pattern.</summary>
        /// <param name="angle">The angle in degrees.</param>
        /// <param name="baseX">The x of the point it runs through.</param>
        /// <param name="baseY">The y of that point.</param>
        /// <param name="along">The step to the next copy along the line (delta-x).</param>
        /// <param name="across">The step to the next copy across it (delta-y).</param>
        /// <param name="dashes">The dashes, above nought drawn, below left out, nought a dot; none for a solid line.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is not finite.</exception>
        public CadHatchPatternLine(double angle, double baseX, double baseY, double along, double across, IEnumerable<double> dashes)
        {
            CadLinetypeFile.Finite(angle, nameof(angle));
            CadLinetypeFile.Finite(baseX, nameof(baseX));
            CadLinetypeFile.Finite(baseY, nameof(baseY));
            CadLinetypeFile.Finite(along, nameof(along));
            CadLinetypeFile.Finite(across, nameof(across));
            var list = new List<double>(dashes ?? Array.Empty<double>());
            list.ForEach(d => CadLinetypeFile.Finite(d, nameof(dashes)));
            Angle = angle;
            BaseX = baseX;
            BaseY = baseY;
            Along = along;
            Across = across;
            Dashes = list.AsReadOnly();
        }

        /// <summary>Gets the angle in degrees.</summary>
        public double Angle { get; }

        /// <summary>Gets the x of the point the line runs through.</summary>
        public double BaseX { get; }

        /// <summary>Gets the y of that point.</summary>
        public double BaseY { get; }

        /// <summary>Gets the step to the next copy along the line (delta-x).</summary>
        public double Along { get; }

        /// <summary>Gets the step to the next copy across the line (delta-y).</summary>
        public double Across { get; }

        /// <summary>Gets the dashes; none for a solid line.</summary>
        public IReadOnlyList<double> Dashes { get; }
    }

    /// <summary>A hatch pattern of a .pat file: its name, its description and its lines.</summary>
    public sealed class CadHatchPattern
    {
        /// <summary>Makes a pattern.</summary>
        /// <param name="name">The name, as ANSI31.</param>
        /// <param name="description">The description; null for none.</param>
        /// <param name="lines">The lines, at least one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the lines are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is blank or there is no line.</exception>
        public CadHatchPattern(string name, string description, IEnumerable<CadHatchPatternLine> lines)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var list = new List<CadHatchPatternLine>(lines ?? throw new ArgumentNullException(nameof(lines)));

            if (name.Trim().Length == 0 || list.Count == 0 || list.Contains(null))
            {
                throw new ArgumentException("A pattern has a name and at least one line.", nameof(lines));
            }

            Name = name.Trim();
            Description = (description ?? string.Empty).Trim();
            Lines = list.AsReadOnly();
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the description.</summary>
        public string Description { get; }

        /// <summary>Gets the lines.</summary>
        public IReadOnlyList<CadHatchPatternLine> Lines { get; }

        /// <summary>
        /// Gets the lines placed as a HATCH holds them, as AutoCAD 2021 places those of its file: each line's angle turned by
        /// the angle, its base point scaled and turned about the origin and moved by the origin given, its step turned into the
        /// plane by its own angle and scaled, its dashes scaled. ANSI31 at angle nought and scale 1 is the line at 45 degrees
        /// through (0, 0), offset (-2.245, 2.245); at 30 degrees and scale 2, at 75 degrees, offset (-6.134, 1.644).
        /// </summary>
        /// <param name="angle">The angle of the hatch, in degrees.</param>
        /// <param name="scale">The scale of the hatch, above nought.</param>
        /// <param name="originX">The x of the hatch's origin.</param>
        /// <param name="originY">The y of the hatch's origin.</param>
        /// <returns>The lines, for <see cref="DxfHatch.SetPatternDefinitions"/>.</returns>
        public IReadOnlyList<DxfPatternDefinition> Place(double angle, double scale, double originX, double originY)
        {
            var placed = new List<DxfPatternDefinition>(Lines.Count);
            double turn = angle * (Math.PI / 180.0);
            double cos = Math.Cos(turn);
            double sin = Math.Sin(turn);

            foreach (CadHatchPatternLine line in Lines)
            {
                double degrees = (line.Angle + angle) % 360.0;
                degrees = degrees < 0.0 ? degrees + 360.0 : degrees;
                double x = line.BaseX * scale;
                double y = line.BaseY * scale;
                double along = degrees * (Math.PI / 180.0);
                double dx = line.Along * scale;
                double dy = line.Across * scale;
                var dashes = new double[line.Dashes.Count];

                for (int i = 0; i < dashes.Length; i++)
                {
                    dashes[i] = line.Dashes[i] * scale;
                }

                placed.Add(new DxfPatternDefinition(degrees * (Math.PI / 180.0), x * cos - y * sin + originX, x * sin + y * cos + originY,
                    dx * Math.Cos(along) - dy * Math.Sin(along), dx * Math.Sin(along) + dy * Math.Cos(along), dashes));
            }

            return placed.AsReadOnly();
        }
    }

    /// <summary>
    /// Reads .pat files into <see cref="CadHatchPattern"/>s, and carries the presenter's own set of patterns
    /// (<see cref="BuiltIn"/>), written from their geometry in millimetres, the default. No file of AutoCAD's is carried or
    /// looked for: a .pat file is read only where the user picks it or keeps it in a support folder of their own
    /// (<see cref="CadSupportFiles"/>).
    /// </summary>
    public static class CadPatternFile
    {
        /// <summary>The name the presenter's own set goes by: no file has it on disk.</summary>
        public const string BuiltInName = "GeometryHelper.pat";

        // The presenter's own patterns, in millimetres: 3.175 apart for the ANSI ones, 3.175 times the square root of two
        // (4.490128) along the x axis between lines at 45 degrees that far apart.
        private const string Own =
            "*ANSI31,ANSI Iron, Brick, Stone masonry\n45, 0,0, 0,3.175\n" +
            "*ANSI32,ANSI Steel\n45, 0,0, 0,9.525\n45, 4.490128,0, 0,9.525\n" +
            "*ANSI33,ANSI Bronze, Brass, Copper\n45, 0,0, 0,6.35\n45, 4.490128,0, 0,6.35, 3.175,-1.5875\n" +
            "*ANSI34,ANSI Plastic, Rubber\n45, 0,0, 0,19.05\n45, 4.490128,0, 0,19.05\n45, 8.980256,0, 0,19.05\n45, 13.470384,0, 0,19.05\n" +
            "*ANSI37,ANSI Lead, Zinc, Magnesium, Sound/Heat/Elec Insulation\n45, 0,0, 0,3.175\n135, 0,0, 0,3.175\n" +
            "*ANGLE,Angle steel\n0, 0,0, 0,6.985, 5.08,-1.905\n90, 0,0, 0,6.985, 5.08,-1.905\n" +
            "*BRICK,Brick or masonry-type surface\n0, 0,0, 0,6.35\n90, 0,0, 6.35,6.35, 6.35,-6.35\n" +
            "*CROSS,A series of crosses\n0, 0,0, 0,6.35, 3.175,-3.175\n90, 1.5875,-1.5875, 0,6.35, 3.175,-3.175\n" +
            "*DASH,Dashed lines\n0, 0,0, 3.175,3.175, 3.175,-3.175\n" +
            "*DOTS,A series of dots\n0, 0,0, 0.79375,1.5875, 0,-1.5875\n" +
            "*EARTH,Earth or ground (subterranean)\n0, 0,0, 6.35,6.35, 6.35,-6.35\n0, 0,2.38125, 6.35,6.35, 6.35,-6.35\n0, 0,4.7625, 6.35,6.35, 6.35,-6.35\n" +
            "90, 0.79375,6.35, 6.35,6.35, 6.35,-6.35\n90, 3.175,6.35, 6.35,6.35, 6.35,-6.35\n90, 5.55625,6.35, 6.35,6.35, 6.35,-6.35\n" +
            "*HONEY,Honeycomb pattern\n0, -1.5875,2.749630, 4.7625,2.749630, 3.175,-6.35\n120, -1.5875,-2.749630, 4.7625,2.749630, 3.175,-6.35\n" +
            "240, 3.175,0, 4.7625,2.749630, 3.175,-6.35\n" +
            "*LINE,Parallel horizontal lines\n0, 0,0, 0,3.175\n" +
            "*NET,Horizontal / vertical grid\n0, 0,0, 0,3.175\n90, 0,0, 0,3.175\n" +
            "*SQUARE,Small aligned squares\n0, 0,0, 0,6.35, 3.175,-3.175\n0, 0,3.175, 0,6.35, 3.175,-3.175\n90, 0,0, 0,6.35, 3.175,-3.175\n90, 3.175,0, 0,6.35, 3.175,-3.175\n" +
            "*STEEL,Steel material\n45, 0,0, 0,6.35\n45, 0,1.5875, 0,6.35\n" +
            "*ZIGZAG,Staircase effect\n0, 0,0, 3.175,3.175, 3.175,-3.175\n90, 3.175,0, 3.175,-3.175, 3.175,-3.175\n" +

            // The other ANSI section linings, drawn as the standard's symbols read: lines at 45 degrees, 3.175 apart or a multiple.
            "*ANSI35,ANSI fire brick, refractory\n45, 0,0, 0,6.35\n45, 4.490128,0, 0,6.35, 7.9375,-1.5875,0,-1.5875\n" +
            "*ANSI36,ANSI marble, slate, glass\n45, 0,0, 5.55625,3.175, 7.9375,-1.5875,0,-1.5875\n" +
            "*ANSI38,ANSI aluminium\n45, 0,0, 0,3.175\n135, 0,0, 6.35,3.175, 7.9375,-4.7625\n" +

            // Basics. GRASS: a tuft of three strokes on a square of 12.7. HEX: hexagons of side 3.175, a point up, the copies
            // stepped along the lattice (5.499261, 0) and (2.749630, 4.7625) they tile. TRIANG: triangles of side 3.175 on a
            // lattice of 6.35, half of each cell left empty. SOLID is not here: it is a fill, not lines.
            "*GRASS,Grass tufts on a square grid\n90, 0,0, 0,12.7, 3.175,-9.525\n45, 0,0, 8.980256,8.980256, 2.245064,-15.715448\n" +
            "135, 0,0, 8.980256,-8.980256, 2.245064,-15.715448\n" +
            "*GRATE,Grating\n0, 0,0, 0,0.79375\n90, 0,0, 0,3.175\n" +
            "*HEX,Hexagons, a point up\n90, 2.749630,-1.5875, 4.7625,-2.749630, 3.175,-6.35\n30, 0,-3.175, 4.7625,-2.749630, 3.175,-6.35\n" +
            "150, 2.749630,1.5875, 4.7625,2.749630, 3.175,-6.35\n" +
            "*INSUL,Insulation\n0, 0,0, 0,9.525\n0, 0,3.175, 0,9.525, 3.175,-3.175\n0, 0,6.35, 0,9.525, 3.175,-3.175\n" +
            "*NET3,Lines at 0, 60 and 120 degrees\n0, 0,0, 0,3.175\n60, 0,0, 0,3.175\n120, 0,0, 0,3.175\n" +
            "*TRIANG,Triangles apart\n0, 0,0, 3.175,5.499261, 3.175,-3.175\n60, 0,0, -3.175,5.499261, 3.175,-3.175\n" +
            "120, 3.175,0, 3.175,5.499261, 3.175,-3.175\n" +

            // ISO 128-20 for a pen of 1.0 mm, lines 5 apart: a dash 12, a long dash 24, a short dash 6, a gap 3, a space 18, a dot
            // nought. A line takes six dashes at most, so a type of more (06, 13, 14, 15) is two lines on the same line: its
            // dashes, then its dots from where they start.
            "*ISO02W100,ISO 02 dashed, pen 1.0 mm\n0, 0,0, 0,5, 12,-3\n" +
            "*ISO03W100,ISO 03 dashed spaced, pen 1.0 mm\n0, 0,0, 0,5, 12,-18\n" +
            "*ISO04W100,ISO 04 long dash dot, pen 1.0 mm\n0, 0,0, 0,5, 24,-3,0,-3\n" +
            "*ISO05W100,ISO 05 long dash two dots, pen 1.0 mm\n0, 0,0, 0,5, 24,-3,0,-3,0,-3\n" +
            "*ISO06W100,ISO 06 long dash three dots, pen 1.0 mm\n0, 0,0, 0,5, 24,-12\n0, 27,0, 0,5, 0,-3,0,-3,0,-30\n" +
            "*ISO07W100,ISO 07 dotted, pen 1.0 mm\n0, 0,0, 0,5, 0,-3\n" +
            "*ISO08W100,ISO 08 long dash short dash, pen 1.0 mm\n0, 0,0, 0,5, 24,-3,6,-3\n" +
            "*ISO09W100,ISO 09 long dash two short dashes, pen 1.0 mm\n0, 0,0, 0,5, 24,-3,6,-3,6,-3\n" +
            "*ISO10W100,ISO 10 dash dot, pen 1.0 mm\n0, 0,0, 0,5, 12,-3,0,-3\n" +
            "*ISO11W100,ISO 11 two dashes dot, pen 1.0 mm\n0, 0,0, 0,5, 12,-3,12,-3,0,-3\n" +
            "*ISO12W100,ISO 12 dash two dots, pen 1.0 mm\n0, 0,0, 0,5, 12,-3,0,-3,0,-3\n" +
            "*ISO13W100,ISO 13 two dashes two dots, pen 1.0 mm\n0, 0,0, 0,5, 12,-3,12,-9\n0, 30,0, 0,5, 0,-3,0,-33\n" +
            "*ISO14W100,ISO 14 dash three dots, pen 1.0 mm\n0, 0,0, 0,5, 12,-12\n0, 15,0, 0,5, 0,-3,0,-3,0,-18\n" +
            "*ISO15W100,ISO 15 two dashes three dots, pen 1.0 mm\n0, 0,0, 0,5, 12,-3,12,-12\n0, 30,0, 0,5, 0,-3,0,-3,0,-33\n" +

            // The architectural ones, at their real size in millimetres. These are our own drawings under AutoCAD's names: they
            // look like what the name says and do not match AutoCAD's patterns line for line. AR-BRSTD: bricks 200 by 100 in
            // running bond with joints of 10, each joint two lines. AR-HBONE: pavers 200 by 100 in herringbone, turned 45
            // degrees. AR-PARQ1: squares of 150 of five slats each, turned a quarter in turn. AR-CONC: small triangles and short
            // grains on a lattice of 30. AR-RSHKE: rows 250 high of shakes 250 to 350 wide, repeating every three rows.
            "*AR-B816,Concrete block 400 by 200, running bond\n0, 0,0, 0,200\n90, 0,0, 200,200, 200,-200\n" +
            "*AR-BRSTD,Brick 200 by 100, running bond, joints 10\n0, 0,0, 0,110\n0, 0,10, 0,110\n90, 0,10, 110,105, 100,-120\n90, 10,10, 110,105, 100,-120\n" +
            "*AR-CONC,Concrete, small triangles and grains\n0, 5,4, 15,25.980762, 3,-27\n60, 5,4, -15,25.980762, 3,-27\n120, 8,4, 15,25.980762, 3,-27\n" +
            "0, 9,19, 15,25.980762, 2.5,-27.5\n60, 9,19, -15,25.980762, 2.5,-27.5\n120, 11.5,19, 15,25.980762, 2.5,-27.5\n" +
            "30, 18,15, 25.980762,15, 2,-49.961524\n90, 18,15, -25.980762,15, 2,-49.961524\n150, 19.732051,16, 25.980762,15, 2,-49.961524\n" +
            "0, 12,9, 15,25.980762, 0.5,-29.5\n0, 25,21, 15,25.980762, 0.5,-29.5\n0, 3,13, 15,25.980762, 0.5,-29.5\n0, 27,2, 15,25.980762, 0.5,-29.5\n" +
            "*AR-HBONE,Pavers 200 by 100, herringbone\n45, 0,0, 100,100, 200,-200\n45, 212.132034,70.710678, 100,100, 100,-300\n" +
            "135, 0,0, -100,100, 100,-300\n135, 212.132034,70.710678, -100,100, 200,-200\n" +
            "*AR-PARQ1,Parquet, squares of five slats\n0, 0,0, 0,150\n90, 0,0, 0,150\n0, 150,30, 150,150, 150,-150\n0, 150,60, 150,150, 150,-150\n" +
            "0, 150,90, 150,150, 150,-150\n0, 150,120, 150,150, 150,-150\n90, 30,0, 150,150, 150,-150\n90, 60,0, 150,150, 150,-150\n" +
            "90, 90,0, 150,150, 150,-150\n90, 120,0, 150,150, 150,-150\n" +
            "*AR-RROOF,Roofing, long strokes in rows\n0, 0,0, 70,45, 300,-30,120,-20\n0, 40,15, 110,45, 200,-25,150,-15\n0, 90,30, 50,45, 250,-40,90,-30\n" +
            "*AR-RSHKE,Roof shakes, rows 250 high\n0, 0,0, 0,250\n90, 0,0, 0,900, 250,-500\n90, 250,0, 0,900, 250,-500\n90, 600,0, 0,900, 250,-500\n" +
            "90, 120,250, 0,900, 250,-500\n90, 420,250, 0,900, 250,-500\n90, 700,250, 0,900, 250,-500\n" +
            "90, 50,500, 0,900, 250,-500\n90, 330,500, 0,900, 250,-500\n90, 580,500, 0,900, 250,-500\n" +
            "*AR-SAND,Sand, scattered grains\n0, 0,0, 4.1,7.3, 0,-9.7\n0, 2.3,3.1, 5.9,6.7, 0,-11.3\n37, 1.1,5.2, 3.7,8.9, 0,-13.1\n73, 4.4,0.9, 6.3,10.1, 0,-8.3\n";

        private static readonly Lazy<IReadOnlyList<CadHatchPattern>> Mine = new Lazy<IReadOnlyList<CadHatchPattern>>(() => Parse(Own));

        /// <summary>
        /// Gets the presenter's own 48 patterns, in millimetres: ANSI31, ANSI32, ANSI33, ANSI34, ANSI37, ANGLE, BRICK, CROSS, DASH,
        /// DOTS, EARTH, HONEY, LINE, NET, SQUARE, STEEL and ZIGZAG; ANSI35, ANSI36, ANSI38, GRASS, GRATE, HEX, INSUL, NET3 and
        /// TRIANG; ISO02W100 to ISO15W100; and AR-B816, AR-BRSTD, AR-CONC, AR-HBONE, AR-PARQ1, AR-RROOF, AR-RSHKE and AR-SAND,
        /// drawn at their real size (AR-BRSTD a brick 200 by 100 with joints of 10). SOLID is a fill, not a pattern of lines.
        /// </summary>
        public static IReadOnlyList<CadHatchPattern> BuiltIn => Mine.Value;

        /// <summary>
        /// Reads the patterns of the text of a .pat file, as AutoCAD reads them: a pattern begins at a line starting <c>*</c>,
        /// its name all before the first comma, and runs to the next; a comment from a <c>;</c>, blank lines and the blanks about
        /// the numbers passed over; the first pattern of a name kept. A pattern with a line of fewer than five numbers, more than
        /// six dashes or a number that cannot be read, or with no line, is left out, as AutoCAD refuses it.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The patterns, in the order of the file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public static IReadOnlyList<CadHatchPattern> Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var patterns = new List<CadHatchPattern>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string name = null;
            string description = null;
            List<CadHatchPatternLine> lines = null;
            bool broken = false;

            void Keep()
            {
                if (name != null && !broken && lines.Count > 0 && names.Add(name))
                {
                    patterns.Add(new CadHatchPattern(name, description, lines));
                }
            }

            foreach (string raw in text.Split('\n'))
            {
                int comment = raw.IndexOf(';');
                string line = (comment < 0 ? raw : raw.Substring(0, comment)).TrimEnd('\r', ' ', '\t');

                if (line.StartsWith("*", StringComparison.Ordinal))
                {
                    Keep();
                    int comma = line.IndexOf(',');
                    name = (comma < 0 ? line.Substring(1) : line.Substring(1, comma - 1)).Trim();
                    description = comma < 0 ? string.Empty : line.Substring(comma + 1).Trim();
                    lines = new List<CadHatchPatternLine>();
                    broken = name.Length == 0;
                    continue;
                }

                if (name == null || broken || line.Trim().Length == 0)
                {
                    continue;
                }

                CadHatchPatternLine read = Line(line);

                if (read == null)
                {
                    broken = true;
                }
                else
                {
                    lines.Add(read);
                }
            }

            Keep();
            return patterns.AsReadOnly();
        }

        /// <summary>Reads a .pat file: UTF-8 where its bytes are, else each byte its own character.</summary>
        /// <param name="path">The path.</param>
        /// <returns>The patterns.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static IReadOnlyList<CadHatchPattern> Read(string path) => Parse(CadLinetypeFile.ReadText(path ?? throw new ArgumentNullException(nameof(path))));

        /// <summary>A pattern of the presenter's own set by name, letters of either case the same; null where it has none.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The pattern, or null.</returns>
        public static CadHatchPattern FindBuiltIn(string name)
        {
            foreach (CadHatchPattern pattern in BuiltIn)
            {
                if (string.Equals(pattern.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return pattern;
                }
            }

            return null;
        }

        /// <summary>A line of numbers: angle, base point, steps and up to six dashes; null where AutoCAD refuses it.</summary>
        private static CadHatchPatternLine Line(string line)
        {
            var numbers = new List<double>();
            string[] fields = line.Split(',');

            for (int i = 0; i < fields.Length; i++)
            {
                string field = fields[i].Trim();

                if (field.Length == 0)
                {
                    // Commas running together are one; a comma at the end is a dash of nought, as AutoCAD reads them.
                    if (i == fields.Length - 1)
                    {
                        numbers.Add(0.0);
                    }

                    continue;
                }

                int blank = field.IndexOfAny(new[] { ' ', '\t' });

                if (!double.TryParse(blank < 0 ? field : field.Substring(0, blank), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    || double.IsNaN(value) || double.IsInfinity(value))
                {
                    return null;
                }

                numbers.Add(value);
            }

            if (numbers.Count < 5 || numbers.Count > 11)
            {
                return null;
            }

            return new CadHatchPatternLine(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers.GetRange(5, numbers.Count - 5));
        }
    }
}
