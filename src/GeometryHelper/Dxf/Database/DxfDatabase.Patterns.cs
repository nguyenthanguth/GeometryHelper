using System;

namespace GeometryHelper.Dxf
{
    /// <summary>The hatch pattern files the drawing's hatches take their lines from, as AutoCAD finds them on its support path.</summary>
    public sealed partial class DxfDatabase
    {
        private string _patternSearchPath = string.Empty;
        private DxfPatterns _patterns;

        /// <summary>
        /// Gets or sets the folders the hatch pattern files (.pat) are looked for in, separated by semicolons and searched in
        /// their order, as AutoCAD looks for them on its support path; empty, as a drawing is opened or made, for none.
        /// </summary>
        /// <value>
        /// <para>
        /// Empty, no pattern file is read: a HATCH given a pattern of AutoCAD's file by <see cref="DxfHatch.SetHatchPattern"/>
        /// holds no lines of it (<see cref="DxfHatch.NumberOfPatternDefinitions"/> 0), its name written for AutoCAD to take the
        /// lines from its own file, and nothing is read from the disk or logged. Given folders, a HATCH of the drawing given a
        /// pattern by <see cref="DxfHatch.SetHatchPattern"/>, or one made in code given it before it is added to the drawing, is
        /// given the lines of the pattern as AutoCAD 2021 gives them (check P4c): from acadiso.pat in a drawing of millimetres
        /// (<see cref="Measurement"/> <see cref="DxfMeasurementValue.Metric"/>, as the library reads a header that gives none,
        /// which AutoCAD 2021 opens as one of inches) and acad.pat in one of inches, or from a file named after the pattern
        /// (<c>GRID.pat</c> for GRID); a pattern of
        /// AutoCAD's file looked for in the drawing's file first and a custom one in its own file first, each file read once from
        /// the first folder that has it, and the type the pattern is written as (<see cref="DxfHatch.PatternType"/>) that of the
        /// file it is found in, its name in capitals, as AutoCAD writes them. Each line is turned by the HATCH's
        /// <see cref="DxfHatch.PatternAngle"/> and scaled by its <see cref="DxfHatch.PatternScale"/> about the origin of its
        /// plane, and written anew from the file when either is set, so that <see cref="DxfHatch.Explode"/> and
        /// <see cref="DxfHatch.GetHatchLinesData"/> draw it: ANSI31 of acadiso.pat, <c>45, 0,0, 0,3.175</c>, is the one line at
        /// 45 degrees through (0, 0), offset (-2.245, 2.245), and a square 30 by 20 of it explodes to 11 LINEs; of acad.pat,
        /// <c>45, 0,0, 0,.125</c>, offset (-0.0884, 0.0884); at scale 2 and angle 30 degrees, at 75 degrees, offset
        /// (-6.134, 1.644). <see cref="DxfHatch.PatternDouble"/> adds no line to such a pattern, as AutoCAD adds none. Every
        /// pattern of AutoCAD 2021's two files, at three scales and angles, gives the lines AutoCAD gives to 1E-13 (918 hatches),
        /// and on squares off the origin AutoCAD draws them, and <see cref="DxfHatch.Explode"/> breaks them, into AutoCAD's own
        /// LINEs, 84 034 of 84 034.
        /// </para>
        /// <para>
        /// A pattern file is read as AutoCAD 2021 reads one: a pattern begins at a line starting <c>*</c>, its name all before
        /// the first comma, and runs to the next; comments from a <c>;</c>, blank lines and the blanks about numbers are passed
        /// over, and the first pattern of a name is taken, whatever its case. A pattern AutoCAD refuses is refused: one with a
        /// line of fewer than five numbers or more than six dashes, or a number that cannot be read, or one of no line. A HATCH
        /// read from a file keeps the lines the file gives it, a pattern made by the user (<c>_USER</c>) is made as before, and
        /// a pattern found in no file, or refused, is left with no lines as with no folders, the log saying so at Debug once a
        /// pattern, and a pattern refused, or a file that cannot be read, at Warn; nothing throws. Each set reads the files anew.
        /// No folder is looked in that is not given, and no file of AutoCAD's is carried by the library: the results follow the
        /// files supplied.
        /// </para>
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the path set is null.</exception>
        public string PatternSearchPath
        {
            get => _patternSearchPath;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                _patternSearchPath = value;
                _patterns = value.Trim().Length == 0 ? null : new DxfPatterns(value);
            }
        }

        /// <summary>The pattern files of <see cref="PatternSearchPath"/>, read as they are first asked for; null where it gives no folder.</summary>
        internal DxfPatterns Patterns => _patterns;
    }
}
