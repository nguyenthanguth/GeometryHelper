using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A pattern offered by the Hatch dialog: its name, its description and the file it is of.</summary>
    public sealed class HatchPatternChoice
    {
        internal HatchPatternChoice(string name, string description, string file, string source)
        {
            Name = name;
            Description = description ?? string.Empty;
            File = file;
            Source = source;
        }

        /// <summary>Gets the name, as ANSI31.</summary>
        public string Name { get; }

        /// <summary>Gets the description.</summary>
        public string Description { get; }

        /// <summary>Gets the file the hatch takes it from: null for the presenter's own set or a NAME.pat of the user's support folders, else a full path.</summary>
        public string File { get; }

        /// <summary>Gets where it was found, for its tooltip: GeometryHelper.pat or the name of a file of the user's.</summary>
        public string Source { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The Hatch Creation tab of AutoCAD as a dialog, for HATCH's seTtings and for HATCHEDIT: the type (Pattern, User defined,
    /// Solid, Gradient), the pattern with its preview, the angle, scale or spacing, double, ISO pen width, origin, whether the
    /// hatch follows its objects, the island detection style, and a gradient's name, two colours and centring. The preview is
    /// a square hatched by the presenter's own hatch display. <see cref="Settings"/> holds the result once OK is pressed.
    /// </summary>
    public sealed class HatchSettingsViewModel : ObservableObject
    {
        private static readonly string[] TypeNames = { "Pattern", "User defined", "Solid", "Gradient" };
        private static readonly string[] StyleNames = { "Normal", "Outer", "Ignore" };

        private readonly CadDocument _document;
        private HatchPatternChoice _pattern;
        private string _angle;
        private string _scale;
        private string _spacing;
        private string _originX;
        private string _originY;
        private string _isoPenWidth;
        private string _message = string.Empty;
        private ImageSource _preview;

        /// <summary>Makes the dialog over settings, kept as a copy until OK.</summary>
        /// <param name="document">The document whose support folders the patterns are found in.</param>
        /// <param name="settings">The settings shown.</param>
        /// <param name="editing">Whether it edits a hatch (HATCHEDIT) rather than the settings of the next (HATCH).</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the settings are null.</exception>
        public HatchSettingsViewModel(CadDocument document, CadHatchSettings settings, bool editing)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            Settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Clone();
            Title = editing ? "Hatch Editor" : "Hatch Creation";
            Patterns = Find(document);
            _pattern = Patterns.FirstOrDefault(p => string.Equals(p.Name, Settings.PatternName, StringComparison.OrdinalIgnoreCase));
            _angle = Text(Settings.Angle);
            _scale = Text(Settings.Scale);
            _spacing = Text(Settings.Spacing);
            _originX = Text(Settings.OriginX);
            _originY = Text(Settings.OriginY);
            PickColor1Command = new DelegateCommand(_ => PickGradientColor(true));
            PickColor2Command = new DelegateCommand(_ => PickGradientColor(false));
            Refresh();
        }

        /// <summary>Gets the settings as the dialog has them.</summary>
        public CadHatchSettings Settings { get; }

        /// <summary>Gets the title: Hatch Creation, or Hatch Editor for HATCHEDIT.</summary>
        public string Title { get; }

        /// <summary>Gets the types offered: Pattern, User defined, Solid, Gradient.</summary>
        public IReadOnlyList<string> Types => TypeNames;

        /// <summary>Gets or sets the type, by its name.</summary>
        public string Type
        {
            get => TypeNames[(int)Settings.Type];
            set
            {
                int index = Array.IndexOf(TypeNames, value);

                if (index >= 0 && index != (int)Settings.Type)
                {
                    Settings.Type = (CadHatchType)index;
                    Refresh();
                }
            }
        }

        /// <summary>Gets whether a pattern of a file is chosen.</summary>
        public bool IsPattern => Settings.Type == CadHatchType.Pattern;

        /// <summary>Gets whether lines of the user's are chosen.</summary>
        public bool IsUserDefined => Settings.Type == CadHatchType.UserDefined;

        /// <summary>Gets whether a gradient is chosen.</summary>
        public bool IsGradient => Settings.Type == CadHatchType.Gradient;

        /// <summary>Gets whether the type has an angle: all but a solid fill.</summary>
        public bool HasAngle => Settings.Type != CadHatchType.Solid;

        /// <summary>Gets the patterns offered: the presenter's own, then those of the .pat files of the user's support folders, each name once, the presenter's winning.</summary>
        public IReadOnlyList<HatchPatternChoice> Patterns { get; }

        /// <summary>Gets or sets the pattern chosen.</summary>
        public HatchPatternChoice Pattern
        {
            get => _pattern;
            set
            {
                if (value != null && Set(ref _pattern, value))
                {
                    Settings.PatternName = value.Name;
                    Settings.PatternFile = value.File;
                    Raise(nameof(IsIso));
                    Refresh();
                }
            }
        }

        /// <summary>Gets or sets the angle in degrees, as typed.</summary>
        public string Angle
        {
            get => _angle;
            set => Number(ref _angle, value, v => Settings.Angle = v, false, "The angle");
        }

        /// <summary>Gets or sets the scale of a pattern, as typed; above nought.</summary>
        public string Scale
        {
            get => _scale;
            set
            {
                if (Number(ref _scale, value, v => Settings.Scale = v, true, "The scale"))
                {
                    Set(ref _isoPenWidth, null, nameof(IsoPenWidth));
                }
            }
        }

        /// <summary>Gets or sets the spacing of lines of the user's, as typed; above nought.</summary>
        public string Spacing
        {
            get => _spacing;
            set => Number(ref _spacing, value, v => Settings.Spacing = v, true, "The spacing");
        }

        /// <summary>Gets or sets whether lines of the user's are crossed by as many square to them.</summary>
        public bool Double
        {
            get => Settings.Double;
            set
            {
                Settings.Double = value;
                Refresh();
            }
        }

        /// <summary>Gets the ISO pen widths offered.</summary>
        public IReadOnlyList<string> IsoPenWidths => LinetypeManagerViewModel.IsoPenWidths;

        /// <summary>Gets whether the pattern is an ISO one (ACAD_ISO...), drawn for a pen of 1 mm, whose pen width is its scale.</summary>
        public bool IsIso => IsPattern && (Settings.PatternName ?? string.Empty).StartsWith("ACAD_ISO", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets or sets the ISO pen width: chosen, it sets the scale to the width in millimetres.</summary>
        public string IsoPenWidth
        {
            get => _isoPenWidth;
            set
            {
                if (value != null && Set(ref _isoPenWidth, value))
                {
                    _scale = Text(double.Parse(value.Replace("mm", string.Empty).Trim(), CultureInfo.InvariantCulture));
                    Settings.Scale = double.Parse(_scale, CultureInfo.InvariantCulture);
                    Raise(nameof(Scale));
                    Refresh();
                }
            }
        }

        /// <summary>Gets or sets the x of the origin the pattern is laid from, as typed.</summary>
        public string OriginX
        {
            get => _originX;
            set => Number(ref _originX, value, v => Settings.OriginX = v, false, "The origin");
        }

        /// <summary>Gets or sets the y of the origin, as typed.</summary>
        public string OriginY
        {
            get => _originY;
            set => Number(ref _originY, value, v => Settings.OriginY = v, false, "The origin");
        }

        /// <summary>Gets or sets whether the hatch follows the objects its loops are made of.</summary>
        public bool Associative
        {
            get => Settings.Associative;
            set => Settings.Associative = value;
        }

        /// <summary>Gets the island detection styles offered: Normal, Outer, Ignore.</summary>
        public IReadOnlyList<string> IslandStyles => StyleNames;

        /// <summary>Gets or sets the island detection style, by its name.</summary>
        public string IslandStyle
        {
            get => StyleNames[(int)Settings.IslandStyle];
            set
            {
                int index = Array.IndexOf(StyleNames, value);

                if (index >= 0)
                {
                    Settings.IslandStyle = (DxfHatchStyle)index;
                }
            }
        }

        /// <summary>Gets the gradients offered, by AutoCAD's names: GR_LINEAR to GR_INVCUR.</summary>
        public IReadOnlyList<string> GradientNames => CadHatchSettings.GradientNames;

        /// <summary>Gets or sets the gradient.</summary>
        public string GradientName
        {
            get => Settings.GradientName;
            set
            {
                if (value != null && value != Settings.GradientName)
                {
                    Settings.GradientName = value;
                    Refresh();
                }
            }
        }

        /// <summary>Gets the first colour of the gradient as 0xRRGGBB, for its swatch.</summary>
        public int Color1Rgb => Settings.GradientColor1.ColorValue & 0xFFFFFF;

        /// <summary>Gets the second colour of the gradient as 0xRRGGBB.</summary>
        public int Color2Rgb => Settings.GradientColor2.ColorValue & 0xFFFFFF;

        /// <summary>Gets or sets whether the gradient is centred.</summary>
        public bool Centered
        {
            get => Settings.GradientCentered;
            set
            {
                Settings.GradientCentered = value;
                Refresh();
            }
        }

        /// <summary>Gets the command of the first colour's swatch: Select Color, through <see cref="PickColor"/>.</summary>
        public ICommand PickColor1Command { get; }

        /// <summary>Gets the command of the second colour's swatch.</summary>
        public ICommand PickColor2Command { get; }

        /// <summary>Gets or sets how the window shows Select Color: true when OK was pressed.</summary>
        public Func<SelectColorViewModel, bool> PickColor { get; set; }

        /// <summary>Gets the preview: a square hatched with the settings, drawn by the presenter's own display.</summary>
        public ImageSource Preview
        {
            get => _preview;
            private set => Set(ref _preview, value);
        }

        /// <summary>Gets why a value typed was refused; empty where none was.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Draws a square of a drawing made for a preview into a bitmap, through the canvas's own renderer, on the dark of the dialogs.</summary>
        /// <param name="document">The drawing.</param>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <returns>The picture, frozen.</returns>
        internal static BitmapSource Render(CadDocument document, int width, int height)
        {
            var view = new CadView(width, height);
            view.ZoomTo(document.GetDisplay(document.ModelSpace).Bounds, 0.04);

            using (var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(canvas, document, document.ModelSpace, view, new CanvasOptions { Background = 0x1A1E24 });
                canvas.Flush();
                var picture = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Pbgra32, null);
                picture.WritePixels(new Int32Rect(0, 0, width, height), bitmap.GetPixels(), bitmap.ByteCount, bitmap.RowBytes);
                picture.Freeze();
                return picture;
            }
        }

        /// <summary>Draws the preview again, as the settings stand.</summary>
        private void Refresh()
        {
            Raise(nameof(Type));
            Raise(nameof(IsPattern));
            Raise(nameof(IsUserDefined));
            Raise(nameof(IsGradient));
            Raise(nameof(HasAngle));
            Raise(nameof(IsIso));
            Raise(nameof(Color1Rgb));
            Raise(nameof(Color2Rgb));

            try
            {
                Preview = Render(Settings.Preview(_document), 160, 160);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is IOException)
            {
                GeometryHelperLog.Debug("HatchSettingsViewModel: the preview was not drawn (" + e.Message + ").");
                Preview = null;
            }
        }

        private void PickGradientColor(bool first)
        {
            var dialog = new SelectColorViewModel(first ? Settings.GradientColor1 : Settings.GradientColor2, false);

            if (PickColor == null || !PickColor(dialog) || dialog.Color.IsByLayer || dialog.Color.IsByBlock)
            {
                return;
            }

            if (first)
            {
                Settings.GradientColor1 = dialog.Color;
            }
            else
            {
                Settings.GradientColor2 = dialog.Color;
            }

            Refresh();
        }

        private bool Number(ref string field, string value, Action<double> take, bool positive, string what)
        {
            string typed = (value ?? string.Empty).Trim();

            if (!double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number) || (positive && !(number > 0.0)))
            {
                Message = what + (positive ? " is a number above nought." : " is a number.");
                return false;
            }

            Message = string.Empty;
            field = typed;
            take(number);
            Refresh();
            return true;
        }

        /// <summary>The presenter's own patterns, then those of the user's support folders, each name once.</summary>
        private static IReadOnlyList<HatchPatternChoice> Find(CadDocument document)
        {
            var choices = new List<HatchPatternChoice>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(IEnumerable<CadHatchPattern> patterns, Func<CadHatchPattern, string> file, string source)
            {
                foreach (CadHatchPattern pattern in patterns)
                {
                    if (!string.Equals(pattern.Name, "SOLID", StringComparison.OrdinalIgnoreCase) && names.Add(pattern.Name))
                    {
                        choices.Add(new HatchPatternChoice(pattern.Name.ToUpperInvariant(), pattern.Description, file(pattern), source));
                    }
                }
            }

            Add(CadPatternFile.BuiltIn, p => null, CadPatternFile.BuiltInName);

            foreach (string path in CadSupportFiles.List(document, ".pat"))
            {
                string name = Path.GetFileNameWithoutExtension(path);

                // A pattern in a file of its own name is found on the support path, as AutoCAD finds a custom one; any other by its file.
                Add(Read(path), p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) ? null : path, Path.GetFileName(path));
            }

            return choices.AsReadOnly();
        }

        private static IReadOnlyList<CadHatchPattern> Read(string path)
        {
            try
            {
                return CadPatternFile.Read(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                GeometryHelperLog.Debug("HatchSettingsViewModel: " + path + " was not read (" + e.Message + ").");
                return Array.Empty<CadHatchPattern>();
            }
        }

        private static string Text(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
