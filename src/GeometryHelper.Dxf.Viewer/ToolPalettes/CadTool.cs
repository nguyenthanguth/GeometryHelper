using System;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a tool of a tool palette is, as AutoCAD's tool palettes have them.</summary>
    public enum CadToolKind
    {
        /// <summary>Inserts a block of a drawing: the block's name, the drawing it comes from, its scale and rotation.</summary>
        Block,

        /// <summary>Hatches an area with a pattern, a solid fill or a gradient, at an angle and scale.</summary>
        Hatch,

        /// <summary>Runs a command, with the colour, layer, linetype and lineweight new objects are drawn with.</summary>
        Command,

        /// <summary>A line of text between tools, a heading.</summary>
        Text,

        /// <summary>A rule drawn between tools.</summary>
        Separator,
    }

    /// <summary>
    /// A tool of a tool palette: a block to insert, a hatch to lay, a command to run, or a line of text or a separator that
    /// parts the others. A block tool names the block and the drawing it comes from, its scale, rotation, and whether the
    /// scale or rotation is asked on the command line; a hatch tool the pattern, angle and scale; a command tool the command
    /// line it runs. The three kinds that draw also carry what AutoCAD's Tool Properties calls overrides: the layer, colour,
    /// linetype and lineweight the objects it makes are drawn with, each left null to use the current one. A tool is
    /// made with one of the <c>For</c> methods, or from an object of a drawing with <see cref="FromObject"/>.
    /// </summary>
    public sealed class CadTool
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private double _scale = 1.0;
        private double _spacing = 1.0;

        private CadTool(CadToolKind kind)
        {
            Kind = kind;
        }

        /// <summary>Gets what the tool is; it does not change.</summary>
        public CadToolKind Kind { get; }

        /// <summary>Gets or sets the name shown under the tool's icon; for a text tool the words themselves, for a separator empty.</summary>
        /// <value>Set to null, the name is empty.</value>
        public string Name
        {
            get => _name;
            set => _name = value ?? string.Empty;
        }

        /// <summary>Gets or sets the description shown in the tool's tooltip.</summary>
        /// <value>Set to null, the description is empty.</value>
        public string Description
        {
            get => _description;
            set => _description = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets the icon of a command tool, the name of one of the presenter's own: Line, Polyline, Circle, Arc, Text,
        /// MText, Rectangle, Ellipse, Spline, Point, Move, Copy, Rotate, Scale, Mirror, Offset, Trim, Extend, Fillet, Chamfer,
        /// Stretch, Explode, Erase, Dimension, Aligned, Angular, Radius, Diameter, Leader, Table and the others the ribbon has;
        /// null or a name it has not gives the plain command icon.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Gets or sets the command line a command tool runs: a command's name or alias, then the answers to its first prompts,
        /// parted by spaces or semicolons, as typed (<c>CIRCLE 0,0 50</c>); a leading <c>^C^C</c> and an underscore, as a
        /// menu macro of AutoCAD writes them, are left off.
        /// </summary>
        /// <value>Set to null, the command is empty.</value>
        public string Command
        {
            get => _command;
            set => _command = value ?? string.Empty;
        }

        private string _command = string.Empty;

        /// <summary>
        /// Gets or sets the drawing a block tool's block comes from; null, or the drawing open, for a block the drawing has. A
        /// file that cannot be found falls back to the block of the drawing open of the same name.
        /// </summary>
        public string File { get; set; }

        /// <summary>Gets or sets the name of the block in its drawing; empty, with a <see cref="File"/>, for the whole drawing inserted as one block.</summary>
        /// <value>Set to null, the name is empty.</value>
        public string BlockName
        {
            get => _blockName;
            set => _blockName = value ?? string.Empty;
        }

        private string _blockName = string.Empty;

        /// <summary>Gets or sets the scale of a block tool's insertion, or of a hatch tool's pattern; 1 until set.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not above nought and finite.</exception>
        public double Scale
        {
            get => _scale;
            set => _scale = Positive(value, nameof(value));
        }

        /// <summary>Gets or sets the rotation of a block tool's insertion, or the angle of a hatch tool's pattern or gradient, in degrees; 0 until set.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not finite.</exception>
        public double Angle
        {
            get => _angle;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "An angle is a finite number.");
                }

                _angle = value;
            }
        }

        private double _angle;

        /// <summary>Gets or sets whether a block tool asks the scale on the command line, as AutoCAD's Prompt for scale; false until set.</summary>
        public bool PromptForScale { get; set; }

        /// <summary>Gets or sets whether a block tool asks the rotation on the command line, as AutoCAD's Prompt for rotation; false until set.</summary>
        public bool PromptForRotation { get; set; }

        /// <summary>Gets or sets whether a block tool explodes what it inserts; false until set.</summary>
        public bool Explode { get; set; }

        /// <summary>Gets or sets what a hatch tool fills with: a pattern, lines of the user's, a solid fill or a gradient.</summary>
        public CadHatchType HatchType { get; set; } = CadHatchType.Pattern;

        /// <summary>Gets or sets the pattern of a hatch tool of the type Pattern, as ANSI31; one of the presenter's own, or one a support folder of the drawing has.</summary>
        /// <value>Set to null, ANSI31.</value>
        public string Pattern
        {
            get => _pattern;
            set => _pattern = string.IsNullOrWhiteSpace(value) ? "ANSI31" : value.Trim();
        }

        private string _pattern = "ANSI31";

        /// <summary>Gets or sets the spacing between the lines of a hatch tool of the type UserDefined; 1 until set.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing set is not above nought and finite.</exception>
        public double Spacing
        {
            get => _spacing;
            set => _spacing = Positive(value, nameof(value));
        }

        /// <summary>Gets or sets the gradient of a hatch tool of the type Gradient, by the name AutoCAD's dialog gives it: GR_LINEAR.</summary>
        /// <value>Set to null, GR_LINEAR.</value>
        public string Gradient
        {
            get => _gradient;
            set => _gradient = string.IsNullOrWhiteSpace(value) ? "GR_LINEAR" : value.Trim();
        }

        private string _gradient = "GR_LINEAR";

        /// <summary>Gets or sets whether the hatch a hatch tool makes follows its boundary; true until set.</summary>
        public bool Associative { get; set; } = true;

        /// <summary>Gets or sets the layer the objects the tool makes are on; null for the current layer. A layer the drawing has not is made.</summary>
        public string Layer { get; set; }

        /// <summary>Gets or sets the colour of the objects the tool makes: ByLayer, ByBlock or an index 1 to 255 as text; null for the current colour.</summary>
        public string Color { get; set; }

        /// <summary>Gets or sets the linetype of the objects the tool makes, as ByLayer, ByBlock or the name of one the drawing has; null for the current linetype.</summary>
        public string Linetype { get; set; }

        /// <summary>
        /// Gets or sets the lineweight of the objects the tool makes, in hundredths of a millimetre as DXF writes it (25 is 0.25 mm),
        /// or -1 for ByLayer, -2 for ByBlock, -3 for the default; null for the current lineweight.
        /// </summary>
        public int? LineWeight { get; set; }

        /// <summary>Gets whether the tool sets a layer, colour, linetype or lineweight of its own.</summary>
        public bool HasOverrides => Layer != null || Color != null || Linetype != null || LineWeight != null;

        /// <summary>Gets a copy, with every setting the tool has.</summary>
        /// <returns>The copy.</returns>
        public CadTool Clone() => (CadTool)MemberwiseClone();

        /// <summary>Makes a command tool.</summary>
        /// <param name="name">The name shown under its icon.</param>
        /// <param name="command">The command line it runs: <c>LINE</c>.</param>
        /// <param name="icon">The icon's name; null for the plain command icon.</param>
        /// <returns>The tool.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name or the command is null.</exception>
        public static CadTool ForCommand(string name, string command, string icon = null)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return new CadTool(CadToolKind.Command)
            {
                Name = name,
                Command = command,
                Icon = icon,
            };
        }

        /// <summary>Makes a block tool.</summary>
        /// <param name="name">The name shown under its picture.</param>
        /// <param name="file">The drawing the block is in; null for the drawing open.</param>
        /// <param name="blockName">The block's name in that drawing; empty for the whole drawing as one block.</param>
        /// <returns>The tool.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public static CadTool ForBlock(string name, string file, string blockName)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return new CadTool(CadToolKind.Block)
            {
                Name = name,
                File = string.IsNullOrWhiteSpace(file) ? null : file,
                BlockName = blockName,
            };
        }

        /// <summary>Makes a hatch tool of a pattern of the presenter's own set or a support folder, at angle 0 and scale 1.</summary>
        /// <param name="name">The name shown under its picture.</param>
        /// <param name="pattern">The pattern's name: ANSI31. SOLID makes a solid fill.</param>
        /// <returns>The tool.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name or the pattern is null.</exception>
        public static CadTool ForHatch(string name, string pattern)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            var tool = new CadTool(CadToolKind.Hatch) { Name = name };

            if (string.Equals(pattern.Trim(), "SOLID", StringComparison.OrdinalIgnoreCase))
            {
                tool.HatchType = CadHatchType.Solid;
            }
            else
            {
                tool.Pattern = pattern;
            }

            return tool;
        }

        /// <summary>Makes a line of text to part the tools with.</summary>
        /// <param name="text">The words.</param>
        /// <returns>The tool.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public static CadTool ForText(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new CadTool(CadToolKind.Text) { Name = text };
        }

        /// <summary>Makes a separator, a rule between tools.</summary>
        /// <returns>The tool.</returns>
        public static CadTool ForSeparator() => new CadTool(CadToolKind.Separator);

        /// <summary>
        /// Makes a tool of an object of a drawing, as dragging it onto a palette does in AutoCAD: a block reference becomes a
        /// block tool (its block, the drawing open as source, the scale of the reference), a hatch a hatch tool (its pattern or
        /// fill, angle and scale), and a line, circle, arc, polyline, ellipse, spline, point, text, multiline text or dimension a
        /// command tool that runs the command which draws it. Each takes the object's layer, colour, linetype and lineweight as
        /// its overrides; a true colour, which a drawing's current colour cannot be, is left unset.
        /// </summary>
        /// <param name="document">The document the object is in.</param>
        /// <param name="entity">The object.</param>
        /// <returns>The tool; null for an object no tool draws (a table, a viewport, a block's attribute).</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the entity is null.</exception>
        public static CadTool FromObject(CadDocument document, DxfEntity entity)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            CadTool tool;

            switch (entity)
            {
                case DxfBlockReference insert when entity.GetType() == typeof(DxfBlockReference):
                    tool = ForBlock(insert.Name, document.FileName, insert.Name);
                    tool.Scale = Math.Abs(insert.ScaleFactors.X) > 0.0 ? Math.Abs(insert.ScaleFactors.X) : 1.0;
                    break;
                case DxfHatch hatch:
                    tool = HatchOf(hatch);
                    break;
                default:
                    if (!Draws.TryGetValue(entity.GetType(), out string[] drawn))
                    {
                        return null;
                    }

                    tool = ForCommand(drawn[0], drawn[1], drawn[2]);
                    break;
            }

            tool.Layer = entity.Layer;
            tool.Color =
                entity.Color.IsByLayer ? "ByLayer"
                : entity.Color.IsByBlock ? "ByBlock"
                : entity.Color.ColorMethod == DxfColorMethod.ByAci
                    ? entity.Color.ColorIndex.ToString(CultureInfo.InvariantCulture)
                : null;
            tool.Linetype = entity.Linetype;
            tool.LineWeight = (int)entity.LineWeight;
            return tool;
        }

        private static CadTool HatchOf(DxfHatch hatch)
        {
            CadHatchSettings settings = CadHatchSettings.Of(hatch);
            CadTool tool = ForHatch(
                settings.Type == CadHatchType.Gradient ? settings.GradientName
                    : settings.Type == CadHatchType.Solid ? "Solid"
                    : settings.Type == CadHatchType.UserDefined ? "User defined"
                    : settings.PatternName,
                settings.PatternName
            );
            tool.HatchType = settings.Type;
            tool.Angle = settings.Angle;
            tool.Associative = settings.Associative;
            tool.Gradient = settings.GradientName;

            if (settings.Scale > 0.0 && !double.IsInfinity(settings.Scale))
            {
                tool.Scale = settings.Scale;
            }

            if (settings.Spacing > 0.0 && !double.IsInfinity(settings.Spacing))
            {
                tool.Spacing = settings.Spacing;
            }

            return tool;
        }

        /// <summary>The command tool each kind of object that has one makes: its name, its command and its icon.</summary>
        private static readonly System.Collections.Generic.Dictionary<Type, string[]> Draws =
            new System.Collections.Generic.Dictionary<Type, string[]>
            {
                [typeof(DxfLine)] = new[] { "Line", "LINE", "Line" },
                [typeof(DxfCircle)] = new[] { "Circle", "CIRCLE", "Circle" },
                [typeof(DxfArc)] = new[] { "Arc", "ARC", "Arc" },
                [typeof(DxfPolyline)] = new[] { "Polyline", "PLINE", "Polyline" },
                [typeof(DxfEllipse)] = new[] { "Ellipse", "ELLIPSE", "Ellipse" },
                [typeof(DxfSpline)] = new[] { "Spline", "SPLINE", "Spline" },
                [typeof(DxfDBPoint)] = new[] { "Point", "POINT", "Point" },
                [typeof(DxfDBText)] = new[] { "Text", "TEXT", "Text" },
                [typeof(DxfMText)] = new[] { "Multiline Text", "MTEXT", "MText" },
                [typeof(DxfRotatedDimension)] = new[] { "Linear Dimension", "DIMLINEAR", "Dimension" },
                [typeof(DxfAlignedDimension)] = new[] { "Aligned Dimension", "DIMALIGNED", "Aligned" },
                [typeof(DxfRadialDimension)] = new[] { "Radius Dimension", "DIMRADIUS", "Radius" },
                [typeof(DxfDiametricDimension)] = new[] { "Diameter Dimension", "DIMDIAMETER", "Diameter" },
                [typeof(DxfLineAngularDimension2)] = new[] { "Angular Dimension", "DIMANGULAR", "Angular" },
                [typeof(DxfPoint3AngularDimension)] = new[] { "Angular Dimension", "DIMANGULAR", "Angular" },
                [typeof(DxfOrdinateDimension)] = new[] { "Ordinate Dimension", "DIMORDINATE", "Dimension" },
                [typeof(DxfArcDimension)] = new[] { "Arc Length Dimension", "DIMARC", "Dimension" },
                [typeof(DxfMLeader)] = new[] { "Multileader", "MLEADER", "Leader" },
            };

        private static double Positive(double value, string name)
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    name,
                    value,
                    "A scale or spacing is a finite number above nought."
                );
            }

            return value;
        }
    }
}
