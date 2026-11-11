using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a hatch is filled with, as the Hatch Type box of AutoCAD's Hatch Creation tab offers it.</summary>
    public enum CadHatchType
    {
        /// <summary>A pattern of a pattern file, as ANSI31.</summary>
        Pattern,

        /// <summary>Lines of one angle and spacing made by the user, crossed where double (_USER).</summary>
        UserDefined,

        /// <summary>A solid fill (SOLID).</summary>
        Solid,

        /// <summary>A gradient of two colours.</summary>
        Gradient,
    }

    /// <summary>
    /// The settings HATCH makes a hatch with and HATCHEDIT changes, as the Hatch Creation tab holds them: what it is filled with,
    /// the pattern, its angle and scale or spacing, the origin, the island style, whether it follows its objects, and the two
    /// colours of a gradient. AutoCAD's own start: ANSI31 at angle nought and scale 1, associative, the Normal island style, a
    /// gradient GR_LINEAR from blue to a pale yellow.
    /// </summary>
    public sealed class CadHatchSettings
    {
        private static readonly string[] GradientNamesOfAutoCad =
        {
            "GR_LINEAR",
            "GR_CYLIN",
            "GR_INVCYL",
            "GR_SPHER",
            "GR_INVSPH",
            "GR_HEMISP",
            "GR_INVHEM",
            "GR_CURVED",
            "GR_INVCUR",
        };
        private static readonly string[] GradientNamesOfDxf =
        {
            "LINEAR",
            "CYLINDER",
            "INVCYLINDER",
            "SPHERICAL",
            "INVSPHERICAL",
            "HEMISPHERICAL",
            "INVHEMISPHERICAL",
            "CURVED",
            "INVCURVED",
        };

        /// <summary>Gets the names of AutoCAD's nine gradients as its dialog gives them: GR_LINEAR, GR_CYLIN, GR_INVCYL, GR_SPHER, GR_INVSPH, GR_HEMISP, GR_INVHEM, GR_CURVED, GR_INVCUR.</summary>
        public static IReadOnlyList<string> GradientNames { get; } = Array.AsReadOnly(GradientNamesOfAutoCad);

        /// <summary>Gets or sets what the hatch is filled with.</summary>
        public CadHatchType Type { get; set; } = CadHatchType.Pattern;

        /// <summary>Gets or sets the name of the pattern, as ANSI31.</summary>
        public string PatternName { get; set; } = "ANSI31";

        /// <summary>
        /// Gets or sets the file the pattern is of: a full path of a .pat file, or <see cref="CadPatternFile.BuiltInName"/>;
        /// null to take it from the presenter's own set, and from a file of the user's support folders only where that set has
        /// no pattern of the name. No file of AutoCAD's is looked for.
        /// </summary>
        public string PatternFile { get; set; }

        /// <summary>Gets or sets the angle of the pattern or the gradient, in degrees.</summary>
        public double Angle { get; set; }

        /// <summary>Gets or sets the scale of a pattern of a file: 2 draws its lines twice as far apart.</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>Gets or sets the spacing of the lines of a pattern made by the user.</summary>
        public double Spacing { get; set; } = 1.0;

        /// <summary>Gets or sets whether the lines of a pattern made by the user are crossed by as many square to them.</summary>
        public bool Double { get; set; }

        /// <summary>Gets or sets the x of the point the pattern is laid from.</summary>
        public double OriginX { get; set; }

        /// <summary>Gets or sets the y of the point the pattern is laid from.</summary>
        public double OriginY { get; set; }

        /// <summary>Gets or sets whether the hatch follows the objects its loops were made of (HPASSOC); true as AutoCAD starts.</summary>
        public bool Associative { get; set; } = true;

        /// <summary>Gets or sets which areas of islands within islands are filled.</summary>
        public DxfHatchStyle IslandStyle { get; set; } = DxfHatchStyle.Normal;

        /// <summary>Gets or sets the gradient, by the name AutoCAD's dialog gives it: GR_LINEAR.</summary>
        public string GradientName { get; set; } = "GR_LINEAR";

        /// <summary>Gets or sets the first colour of a gradient: blue, as GFCLR1 starts.</summary>
        public DxfColor GradientColor1 { get; set; } = DxfColor.FromRgb(0, 0, 255);

        /// <summary>Gets or sets the second colour of a gradient: a pale yellow (255, 255, 153), as GFCLR2 starts.</summary>
        public DxfColor GradientColor2 { get; set; } = DxfColor.FromRgb(255, 255, 153);

        /// <summary>Gets or sets whether a gradient is centred (its shift nought) rather than shifted a quarter towards its end.</summary>
        public bool GradientCentered { get; set; } = true;

        /// <summary>Gets a copy.</summary>
        /// <returns>The copy.</returns>
        public CadHatchSettings Clone() => (CadHatchSettings)MemberwiseClone();

        /// <summary>
        /// Reads the settings of a hatch, as HATCHEDIT shows them: its type, pattern, angle in degrees, scale, spacing, double,
        /// island style and association, and its gradient's name, colours and shift. The origin is not read back: a hatch keeps
        /// it in its lines' base points, and HATCHEDIT lays them again from the origin it is given.
        /// </summary>
        /// <param name="hatch">The hatch.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the hatch is null.</exception>
        public static CadHatchSettings Of(DxfHatch hatch)
        {
            if (hatch == null)
            {
                throw new ArgumentNullException(nameof(hatch));
            }

            var settings = new CadHatchSettings { IslandStyle = hatch.HatchStyle, Associative = hatch.Associative };

            if (hatch.IsGradient)
            {
                settings.Type = CadHatchType.Gradient;
                int known = Array.IndexOf(GradientNamesOfDxf, hatch.GradientName.ToUpperInvariant());
                settings.GradientName = known >= 0 ? GradientNamesOfAutoCad[known] : GradientNamesOfAutoCad[0];
                settings.Angle = hatch.GradientAngle * 180.0 / Math.PI;
                settings.GradientCentered = hatch.GradientShift == 0.0f;
                DxfGradientColor[] colours = hatch.GetGradientColors();

                if (colours.Length > 0)
                {
                    settings.GradientColor1 = colours[0].Color;
                    settings.GradientColor2 = colours[colours.Length - 1].Color;
                }

                return settings;
            }

            if (hatch.IsSolidFill)
            {
                settings.Type = CadHatchType.Solid;
                return settings;
            }

            settings.Angle = hatch.PatternAngle * 180.0 / Math.PI;

            if (hatch.PatternType == DxfHatchPatternType.UserDefined)
            {
                settings.Type = CadHatchType.UserDefined;
                settings.Spacing = hatch.PatternSpace;
                settings.Double = hatch.PatternDouble;
                return settings;
            }

            settings.PatternName = hatch.PatternName;
            settings.Scale = hatch.PatternScale;
            return settings;
        }

        /// <summary>
        /// Makes a drawing of a square hatched with these settings, for a dialog's preview drawn through the presenter's own
        /// display: 25 across times the scale of a pattern, ten spacings for lines of the user's, 25 for a fill; the pattern
        /// found as the source document finds it. A pattern that cannot be found or read leaves the square drawn by its edge.
        /// </summary>
        /// <param name="source">The document whose support folders the pattern is looked for in; null for the presenter's own set alone.</param>
        /// <returns>The drawing.</returns>
        public CadDocument Preview(CadDocument source)
        {
            var database = new DxfDatabase("AC1027");
            CadDocument document = CadDocument.From(database, "preview.dxf");
            document.SupportFolders = source == null ? Array.Empty<string>() : CadSupportFiles.FoldersOf(source);
            double side =
                Type == CadHatchType.Pattern ? 25.0 * Scale
                : Type == CadHatchType.UserDefined ? 10.0 * Spacing
                : 25.0;
            var hatch = new DxfHatch();
            database.ModelSpace.AppendEntity(hatch);

            try
            {
                ApplyTo(hatch, database, document);
            }
            catch (Exception e)
                when (e is ArgumentException
                    || e is InvalidOperationException
                    || e is System.IO.IOException
                    || e is UnauthorizedAccessException
                )
            {
                GeometryHelperLog.Debug(
                    "CadHatchSettings: the preview of " + PatternName + " was not hatched (" + e.Message + ")."
                );
            }

            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[]
                {
                    new GeoPoint2(0, 0),
                    new GeoPoint2(side, 0),
                    new GeoPoint2(side, side),
                    new GeoPoint2(0, side),
                },
                null
            );
            return document;
        }

        /// <summary>The name a gradient of AutoCAD's dialog is written under in a DXF: GR_CYLIN as CYLINDER.</summary>
        internal static string DxfGradientName(string name)
        {
            int known = Array.FindIndex(GradientNamesOfAutoCad, n =>
                string.Equals(n, name?.Trim(), StringComparison.OrdinalIgnoreCase)
            );
            return GradientNamesOfDxf[known < 0 ? 0 : known];
        }

        /// <summary>
        /// Gives a hatch of a drawing these settings, as AutoCAD writes them: a pattern by <see cref="DxfHatch.SetHatchPattern"/>,
        /// its angle and scale, given the lines (<see cref="DxfHatch.SetPatternDefinitions"/>) of the file the user picked, else
        /// of the presenter's own set, laid from the origin, else found in a file of the user's support folders; lines made by
        /// the user; a solid fill; a gradient of two colours. Returns the fault where the pattern is in none of them, the hatch
        /// then drawn by its name alone.
        /// </summary>
        internal string ApplyTo(DxfHatch hatch, DxfDatabase database, CadDocument document)
        {
            hatch.HatchStyle = IslandStyle;

            switch (Type)
            {
                case CadHatchType.Solid:
                    hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
                    return null;
                case CadHatchType.Gradient:
                    hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, DxfGradientName(GradientName));
                    hatch.SetGradientColors(
                        new[] { new DxfGradientColor(GradientColor1, 0.0f), new DxfGradientColor(GradientColor2, 1.0f) }
                    );
                    hatch.GradientOneColorMode = false;
                    hatch.GradientAngle = Angle * Math.PI / 180.0;
                    hatch.GradientShift = GradientCentered ? 0.0f : 0.25f;
                    return null;
                case CadHatchType.UserDefined:
                    hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
                    hatch.PatternAngle = Angle * Math.PI / 180.0;
                    hatch.PatternSpace = Spacing;
                    hatch.PatternDouble = Double;
                    MoveOrigin(hatch);
                    return null;
            }

            string name = (PatternName ?? string.Empty).Trim();
            bool own = string.Equals(PatternFile, CadPatternFile.BuiltInName, StringComparison.OrdinalIgnoreCase);
            CadHatchPattern pattern = null;

            // A pattern of a file the user picked is read from it, as a custom pattern.
            if (PatternFile != null && !own)
            {
                pattern = CadPatternFile
                    .Read(PatternFile)
                    .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            bool custom = pattern != null;

            // Else the presenter's own set, the default; the user's support folders only for a name it has not.
            pattern = pattern ?? CadPatternFile.FindBuiltIn(name);
            string path = own || pattern != null ? string.Empty : document.PatternSearchPath;

            if (!string.Equals(database.PatternSearchPath, path, StringComparison.Ordinal))
            {
                database.PatternSearchPath = path;
            }

            hatch.SetHatchPattern(
                custom ? DxfHatchPatternType.CustomDefined : DxfHatchPatternType.PreDefined,
                name.ToUpperInvariant()
            );
            hatch.PatternAngle = Angle * Math.PI / 180.0;
            hatch.PatternScale = Scale;

            if (pattern == null && hatch.NumberOfPatternDefinitions > 0)
            {
                MoveOrigin(hatch);
                return null;
            }

            if (pattern == null)
            {
                return "The pattern " + name + " is in no pattern file found: AutoCAD draws it from its own.";
            }

            hatch.SetPatternDefinitions(pattern.Place(Angle, Scale, OriginX, OriginY));
            return null;
        }

        /// <summary>
        /// Lays the lines a hatch holds from the origin: each line's base point moved by it. A pattern made by the user keeps the
        /// lines of its spacing, laid from the origin of the plane.
        /// </summary>
        private void MoveOrigin(DxfHatch hatch)
        {
            if ((OriginX == 0.0 && OriginY == 0.0) || hatch.PatternType == DxfHatchPatternType.UserDefined)
            {
                return;
            }

            var lines = new List<DxfPatternDefinition>();

            for (int i = 0; i < hatch.NumberOfPatternDefinitions; i++)
            {
                DxfPatternDefinition line = hatch.GetPatternDefinitionAt(i);
                lines.Add(
                    new DxfPatternDefinition(
                        line.Angle,
                        line.BaseX + OriginX,
                        line.BaseY + OriginY,
                        line.OffsetX,
                        line.OffsetY,
                        line.GetDashes()
                    )
                );
            }

            hatch.SetPatternDefinitions(lines);
        }
    }

    /// <summary>The hatch settings of a document: those HATCH makes the next hatch with, and those of the hatch HATCHEDIT changes.</summary>
    public sealed partial class CadDocument
    {
        private CadHatchSettings _hatchSettings = new CadHatchSettings();

        /// <summary>
        /// Gets or sets the settings HATCH makes the next hatch with, as AutoCAD keeps HPNAME, HPANG, HPSCALE and the rest for
        /// the drawing while it is open: ANSI31 at angle nought and scale 1 until set; the Hatch settings dialog sets them.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        public CadHatchSettings HatchSettings
        {
            get => _hatchSettings;
            set => _hatchSettings = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets or sets the settings of the hatch HATCHEDIT is changing while it asks the window for its dialog: read from the
        /// hatch before, applied by HATCHEDIT after, unless the window sets them to null, as Cancel does. Null at any other time.
        /// </summary>
        public CadHatchSettings HatchBeingEdited { get; set; }
    }
}
