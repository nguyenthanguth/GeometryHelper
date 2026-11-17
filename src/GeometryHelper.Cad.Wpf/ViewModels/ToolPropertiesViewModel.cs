using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Tool Properties dialog, as AutoCAD's: the name and description of a tool; for a block tool the block, the drawing it
    /// comes from, the scale, the rotation, whether they are asked and whether it is exploded; for a hatch tool the type, pattern,
    /// angle and scale; for a command tool its command line; and for these three the overrides, the layer, colour, linetype and
    /// lineweight the objects it makes are drawn with, each <c>(Current)</c> to use the drawing's own. A line of text has only
    /// its words. The tool is changed by <see cref="Apply"/> alone, so that Cancel leaves it as it was.
    /// </summary>
    public sealed class ToolPropertiesViewModel : ObservableObject
    {
        /// <summary>The choice that leaves an override unset: the object takes the drawing's current layer, colour, linetype or lineweight.</summary>
        public const string Current = "(Current)";

        private static readonly int[] Weights =
        {
            0,
            5,
            9,
            13,
            15,
            18,
            20,
            25,
            30,
            35,
            40,
            50,
            53,
            60,
            70,
            80,
            90,
            100,
            106,
            120,
            140,
            158,
            200,
            211,
        };

        private string _name;
        private string _description;
        private string _command;
        private string _file;
        private string _blockName;
        private string _scale;
        private string _angle;
        private string _spacing;
        private string _hatchType;
        private string _pattern;
        private string _gradient;
        private string _layer;
        private string _color;
        private string _linetype;
        private string _lineWeight;

        /// <summary>Makes the dialog on a tool, with the layers and linetypes of the drawing for its lists.</summary>
        /// <param name="tool">The tool.</param>
        /// <param name="layers">The names of the layers of the drawing shown; null for none.</param>
        /// <param name="linetypes">The names of the linetypes the drawing has loaded; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the tool is null.</exception>
        public ToolPropertiesViewModel(CadTool tool, IEnumerable<string> layers, IEnumerable<string> linetypes)
        {
            Tool = tool ?? throw new ArgumentNullException(nameof(tool));
            Layers = new[] { Current }.Concat(layers ?? Array.Empty<string>()).ToList();
            Linetypes = new[] { Current, "ByLayer", "ByBlock" }
                .Concat(
                    (linetypes ?? Array.Empty<string>()).Where(l =>
                        !string.Equals(l, "ByLayer", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(l, "ByBlock", StringComparison.OrdinalIgnoreCase)
                    )
                )
                .ToList();
            _name = tool.Name;
            _description = tool.Description;
            _command = tool.Command;
            _file = tool.File ?? string.Empty;
            _blockName = tool.BlockName;
            _scale = Number(tool.Scale);
            _angle = Number(tool.Angle);
            _spacing = Number(tool.Spacing);
            PromptForScale = tool.PromptForScale;
            PromptForRotation = tool.PromptForRotation;
            Explode = tool.Explode;
            Associative = tool.Associative;
            _hatchType =
                tool.HatchType == CadHatchType.Solid ? "Solid"
                : tool.HatchType == CadHatchType.Gradient ? "Gradient"
                : tool.HatchType == CadHatchType.UserDefined ? "User defined"
                : "Pattern";
            _pattern = tool.Pattern;
            _gradient = tool.Gradient;
            _layer = tool.Layer ?? Current;
            _color = tool.Color ?? Current;

            if (tool.Layer != null && !Layers.Contains(tool.Layer))
            {
                Layers.Add(tool.Layer);
            }

            _linetype = tool.Linetype ?? Current;

            if (tool.Linetype != null && !Linetypes.Contains(tool.Linetype))
            {
                Linetypes.Add(tool.Linetype);
            }

            _lineWeight = tool.LineWeight.HasValue ? WeightText(tool.LineWeight.Value) : Current;
        }

        /// <summary>Gets the tool the dialog changes when it is applied.</summary>
        public CadTool Tool { get; }

        /// <summary>Gets the title of the dialog.</summary>
        public string Title => Tool.Kind == CadToolKind.Text ? "Text Properties" : "Tool Properties";

        /// <summary>Gets the layers to choose from: <c>(Current)</c>, then the drawing's, in its order.</summary>
        public List<string> Layers { get; }

        /// <summary>Gets the linetypes to choose from: <c>(Current)</c>, ByLayer, ByBlock, then the drawing's.</summary>
        public List<string> Linetypes { get; }

        /// <summary>Gets the colours to choose from: <c>(Current)</c>, ByLayer, ByBlock, then the names red to white and the indexes 1 to 255.</summary>
        public IReadOnlyList<string> Colors { get; } =
            new[] { Current, "ByLayer", "ByBlock", "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        /// <summary>Gets the lineweights to choose from: <c>(Current)</c>, ByLayer, ByBlock, Default, then 0.00 mm to 2.11 mm.</summary>
        public IReadOnlyList<string> LineWeights { get; } =
            new[] { Current, "ByLayer", "ByBlock", "Default" }.Concat(Weights.Select(w => WeightText(w))).ToList();

        /// <summary>Gets the types of hatch to choose from.</summary>
        public IReadOnlyList<string> HatchTypes { get; } = new[] { "Pattern", "User defined", "Solid", "Gradient" };

        /// <summary>Gets the patterns of the presenter's own set, to choose from; a pattern of a support folder can be typed.</summary>
        public IReadOnlyList<string> Patterns { get; } = CadPatternFile.BuiltIn.Select(p => p.Name).ToList();

        /// <summary>Gets the nine gradients of the hatch dialog, to choose from.</summary>
        public IReadOnlyList<string> Gradients => CadHatchSettings.GradientNames;

        /// <summary>Gets whether the tool is a block tool, whose block, scale and rotation are shown.</summary>
        public bool IsBlock => Tool.Kind == CadToolKind.Block;

        /// <summary>Gets whether the tool is a hatch tool, whose pattern, angle and scale are shown.</summary>
        public bool IsHatch => Tool.Kind == CadToolKind.Hatch;

        /// <summary>Gets whether the tool is a command tool, whose command line is shown.</summary>
        public bool IsCommand => Tool.Kind == CadToolKind.Command;

        /// <summary>Gets whether the tool draws, so that it has overrides: a block, a hatch or a command tool.</summary>
        public bool HasOverrides => IsBlock || IsHatch || IsCommand;

        /// <summary>Gets whether the tool is a line of text, which has no description or properties but its words.</summary>
        public bool IsText => Tool.Kind == CadToolKind.Text;

        /// <summary>Gets whether the tool is a hatch tool of the type Pattern, which has a pattern and a scale.</summary>
        public bool ShowsPattern => IsHatch && _hatchType == "Pattern";

        /// <summary>Gets whether the tool is a hatch tool of the type User defined, which has a spacing.</summary>
        public bool ShowsSpacing => IsHatch && _hatchType == "User defined";

        /// <summary>Gets whether the tool is a hatch tool of the type Gradient, which has a gradient.</summary>
        public bool ShowsGradient => IsHatch && _hatchType == "Gradient";

        /// <summary>Gets whether the tool is a hatch tool with an angle: any but a solid fill.</summary>
        public bool ShowsAngle => IsHatch && _hatchType != "Solid";

        /// <summary>Gets or sets the name shown under the tool's icon.</summary>
        public string Name
        {
            get => _name;
            set => Edited(ref _name, value);
        }

        /// <summary>Gets or sets the description shown in the tool's tooltip.</summary>
        public string Description
        {
            get => _description;
            set => Edited(ref _description, value);
        }

        /// <summary>Gets or sets the command line of a command tool.</summary>
        public string Command
        {
            get => _command;
            set => Edited(ref _command, value);
        }

        /// <summary>Gets or sets the drawing a block tool's block is taken from; empty for the drawing shown.</summary>
        public string SourceFile
        {
            get => _file;
            set => Edited(ref _file, value);
        }

        /// <summary>Gets or sets the name of the block in its drawing; empty with a drawing, the whole of it as one block.</summary>
        public string BlockName
        {
            get => _blockName;
            set => Edited(ref _blockName, value);
        }

        /// <summary>Gets or sets the scale as typed: a block's insertion, a hatch pattern.</summary>
        public string Scale
        {
            get => _scale;
            set => Edited(ref _scale, value);
        }

        /// <summary>Gets or sets the rotation of a block, or the angle of a hatch, as typed, in degrees.</summary>
        public string Angle
        {
            get => _angle;
            set => Edited(ref _angle, value);
        }

        /// <summary>Gets or sets the spacing of the lines of a hatch of the type User defined, as typed.</summary>
        public string Spacing
        {
            get => _spacing;
            set => Edited(ref _spacing, value);
        }

        /// <summary>Gets or sets whether a block tool asks the scale on the command line.</summary>
        public bool PromptForScale { get; set; }

        /// <summary>Gets or sets whether a block tool asks the rotation on the command line.</summary>
        public bool PromptForRotation { get; set; }

        /// <summary>Gets or sets whether a block tool explodes what it inserts.</summary>
        public bool Explode { get; set; }

        /// <summary>Gets or sets whether the hatch of a hatch tool follows its boundary.</summary>
        public bool Associative { get; set; }

        /// <summary>Gets or sets the type of a hatch tool: Pattern, User defined, Solid or Gradient.</summary>
        public string HatchType
        {
            get => _hatchType;
            set
            {
                if (Edited(ref _hatchType, value))
                {
                    Raise(nameof(ShowsPattern));
                    Raise(nameof(ShowsSpacing));
                    Raise(nameof(ShowsGradient));
                    Raise(nameof(ShowsAngle));
                }
            }
        }

        /// <summary>Gets or sets the pattern of a hatch tool.</summary>
        public string Pattern
        {
            get => _pattern;
            set => Edited(ref _pattern, value);
        }

        /// <summary>Gets or sets the gradient of a hatch tool.</summary>
        public string Gradient
        {
            get => _gradient;
            set => Edited(ref _gradient, value);
        }

        /// <summary>Gets or sets the layer override, <c>(Current)</c> for none.</summary>
        public string Layer
        {
            get => _layer;
            set => Edited(ref _layer, value);
        }

        /// <summary>Gets or sets the colour override, <c>(Current)</c> for none.</summary>
        public string Color
        {
            get => _color;
            set => Edited(ref _color, value);
        }

        /// <summary>Gets or sets the linetype override, <c>(Current)</c> for none.</summary>
        public string Linetype
        {
            get => _linetype;
            set => Edited(ref _linetype, value);
        }

        /// <summary>Gets or sets the lineweight override, <c>(Current)</c> for none.</summary>
        public string LineWeight
        {
            get => _lineWeight;
            set => Edited(ref _lineWeight, value);
        }

        /// <summary>Gets why the dialog cannot be applied, in a sentence; null where it can.</summary>
        public string Error
        {
            get
            {
                if (Tool.Kind != CadToolKind.Separator && string.IsNullOrWhiteSpace(_name))
                {
                    return "The tool needs a name.";
                }

                if (IsCommand && CadToolRun.Words(_command).Count == 0)
                {
                    return "The command line is empty.";
                }

                if (IsBlock && string.IsNullOrWhiteSpace(_blockName) && string.IsNullOrWhiteSpace(_file))
                {
                    return "A block tool needs a block or a drawing.";
                }

                if ((IsBlock || IsHatch) && !TryPositive(_scale, out _) && (IsBlock || _hatchType == "Pattern"))
                {
                    return "The scale is a number above nought.";
                }

                if ((IsBlock || ShowsAngle) && !TryNumber(_angle, out _))
                {
                    return IsBlock ? "The rotation is a number of degrees." : "The angle is a number of degrees.";
                }

                if (ShowsSpacing && !TryPositive(_spacing, out _))
                {
                    return "The spacing is a number above nought.";
                }

                if (HasOverrides && _color != Current)
                {
                    string refused = CadPropertyEditor.ParseColor(_color, out DxfColor color);

                    if (refused != null || color.IsByColor)
                    {
                        return refused ?? "The colour is ByLayer, ByBlock or 1 to 255.";
                    }
                }

                return null;
            }
        }

        /// <summary>Gets whether the dialog can be applied: <see cref="Error"/> is null.</summary>
        public bool CanOk => Error == null;

        /// <summary>
        /// Writes the dialog into the tool; nothing is written where <see cref="Error"/> says something is wrong.
        /// </summary>
        /// <returns>Whether the tool was changed.</returns>
        public bool Apply()
        {
            if (!CanOk)
            {
                return false;
            }

            Tool.Name = Tool.Kind == CadToolKind.Separator ? string.Empty : _name.Trim();
            Tool.Description = IsText ? string.Empty : (_description ?? string.Empty).Trim();

            if (IsCommand)
            {
                Tool.Command = _command.Trim();
            }

            if (IsBlock)
            {
                Tool.File = string.IsNullOrWhiteSpace(_file) ? null : _file.Trim();
                Tool.BlockName = (_blockName ?? string.Empty).Trim();
                TryPositive(_scale, out double scale);
                Tool.Scale = scale;
                TryNumber(_angle, out double angle);
                Tool.Angle = angle;
                Tool.PromptForScale = PromptForScale;
                Tool.PromptForRotation = PromptForRotation;
                Tool.Explode = Explode;
            }

            if (IsHatch)
            {
                Tool.HatchType =
                    _hatchType == "Solid" ? CadHatchType.Solid
                    : _hatchType == "Gradient" ? CadHatchType.Gradient
                    : _hatchType == "User defined" ? CadHatchType.UserDefined
                    : CadHatchType.Pattern;
                Tool.Pattern = _pattern;
                Tool.Gradient = _gradient;
                Tool.Associative = Associative;

                if (TryPositive(_scale, out double scale))
                {
                    Tool.Scale = scale;
                }

                if (TryNumber(_angle, out double angle))
                {
                    Tool.Angle = angle;
                }

                if (TryPositive(_spacing, out double spacing))
                {
                    Tool.Spacing = spacing;
                }
            }

            if (HasOverrides)
            {
                Tool.Layer = _layer == Current || string.IsNullOrWhiteSpace(_layer) ? null : _layer.Trim();
                Tool.Color = _color == Current ? null : _color.Trim();
                Tool.Linetype = _linetype == Current || string.IsNullOrWhiteSpace(_linetype) ? null : _linetype.Trim();
                Tool.LineWeight = WeightOf(_lineWeight);
            }

            return true;
        }

        private bool Edited(
            ref string field,
            string value,
            [System.Runtime.CompilerServices.CallerMemberName] string name = null
        )
        {
            if (!Set(ref field, value ?? string.Empty, name))
            {
                return false;
            }

            Raise(nameof(Error));
            Raise(nameof(CanOk));
            return true;
        }

        private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private static bool TryNumber(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);

        private static bool TryPositive(string text, out double value) => TryNumber(text, out value) && value > 0.0;

        private static string WeightText(int weight) =>
            (weight / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm";

        private static int? WeightOf(string text)
        {
            switch (text)
            {
                case null:
                case Current:
                case "":
                    return null;
                case "ByLayer":
                    return -1;
                case "ByBlock":
                    return -2;
                case "Default":
                    return -3;
                default:
                    foreach (int weight in Weights)
                    {
                        if (text == WeightText(weight))
                        {
                            return weight;
                        }
                    }

                    return null;
            }
        }
    }
}
