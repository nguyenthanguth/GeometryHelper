using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The logic of AutoCAD's Select Color dialog, with no window: the Index Color tab, the 255 colours of AutoCAD's index as
    /// AutoCAD lays them out (the standard colours 1 to 9, the grid of 10 to 249 by hue and shade, the greys 250 to 255) with
    /// ByLayer and ByBlock where the caller offers them, and the True Color tab, red, green and blue, hue, saturation and
    /// luminance, and the hexadecimal of a true colour, shown in the HSL model (a field of hue and saturation picked by a
    /// click, a luminance bar) or the RGB model (a bar for each of red, green and blue). The colour each swatch shows is the one the canvas draws, read from
    /// <see cref="DxfColor"/>; picking index 30 gives orange, 255, 127, 0, and typing red, green and blue 10, 20, 30 gives
    /// the true colour 10,20,30. The colour the dialog was opened on is shown beside the one chosen.
    /// </summary>
    public sealed class SelectColorViewModel : ObservableObject
    {
        /// <summary>The names of AutoCAD's indices 1 to 7.</summary>
        private static readonly string[] Names = { null, "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        // The shades of a hue from the top row of the grid to the bottom: the full colour first, darker down to the middle,
        // then the paler shades, the palest at the bottom.
        private static readonly int[] Shades = { 0, 2, 4, 6, 8, 9, 7, 5, 3, 1 };

        // The colour models of the True Color tab, and the size of the inside of its field and bar, in pixels.
        private const string Hsl = "HSL";
        private const string RgbModel = "RGB";
        private const int FieldWidth = 218;
        private const int FieldHeight = 198;

        private readonly List<AciSwatchViewModel> _swatches = new List<AciSwatchViewModel>();
        private DxfColor _color;
        private bool _trueColorTab;
        private int _red;
        private int _green;
        private int _blue;
        private int _hue;
        private int _saturation;
        private int _luminance;
        private string _model = Hsl;

        /// <summary>Makes the dialog's logic on a colour.</summary>
        /// <param name="current">The colour it opens on, shown as the old one; null for White (7).</param>
        /// <param name="offersByLayerAndByBlock">Whether ByLayer and ByBlock can be chosen, as for an entity; not for a layer.</param>
        public SelectColorViewModel(DxfColor current, bool offersByLayerAndByBlock)
        {
            OffersByLayerAndByBlock = offersByLayerAndByBlock;
            Original = current ?? DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7);

            if (!offersByLayerAndByBlock && (Original.IsByLayer || Original.IsByBlock))
            {
                Original = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7);
            }

            for (int index = 1; index <= 255; index++)
            {
                _swatches.Add(new AciSwatchViewModel(index));
            }

            StandardColors = _swatches.Take(9).ToList();
            GridColors = Shades.SelectMany(shade => Enumerable.Range(1, 24).Select(hue => _swatches[hue * 10 + shade - 1])).ToList();
            GreyColors = _swatches.Skip(249).ToList();
            PickIndexCommand = new DelegateCommand(p => PickIndex(Convert.ToInt32(p, CultureInfo.InvariantCulture)));
            ByLayerCommand = new DelegateCommand(_ => Choose(DxfColor.ByLayer, false), _ => OffersByLayerAndByBlock);
            ByBlockCommand = new DelegateCommand(_ => Choose(DxfColor.ByBlock, false), _ => OffersByLayerAndByBlock);
            _trueColorTab = Original.IsByColor;
            Choose(Original, false);
        }

        /// <summary>Gets the colour the dialog was opened on.</summary>
        public DxfColor Original { get; }

        /// <summary>Gets the old colour as 0xRRGGBB, its swatch; white for ByLayer and ByBlock.</summary>
        public int OriginalRgb => Swatch(Original);

        /// <summary>Gets the old colour in words, as the new one is given.</summary>
        public string OriginalText => Describe(Original);

        /// <summary>Gets or sets the colour chosen, which OK gives back.</summary>
        public DxfColor Color
        {
            get => _color;
            set => Choose(value ?? throw new ArgumentNullException(nameof(value)), false);
        }

        /// <summary>Gets the colour chosen as 0xRRGGBB, its swatch; white for ByLayer and ByBlock.</summary>
        public int ColorRgb => Swatch(_color);

        /// <summary>Gets the colour chosen in words: "Index color: 30", "Red, Green, Blue: 10,20,30", "ByLayer".</summary>
        public string ColorText => Describe(_color);

        /// <summary>Gets whether ByLayer and ByBlock can be chosen.</summary>
        public bool OffersByLayerAndByBlock { get; }

        /// <summary>Gets or sets whether the True Color tab is shown rather than Index Color; it opens on True Color for a true colour.</summary>
        public bool IsTrueColorTab
        {
            get => _trueColorTab;
            set
            {
                if (Set(ref _trueColorTab, value))
                {
                    Raise(nameof(IsIndexColorTab));
                }
            }
        }

        /// <summary>Gets or sets whether the Index Color tab is shown.</summary>
        public bool IsIndexColorTab
        {
            get => !_trueColorTab;
            set => IsTrueColorTab = !value;
        }

        /// <summary>Gets AutoCAD's standard colours, indices 1 to 9, in a row.</summary>
        public IReadOnlyList<AciSwatchViewModel> StandardColors { get; }

        /// <summary>
        /// Gets the colours 10 to 249 as AutoCAD's grid lays them, 24 to a row, row by row: a column per hue (10, 20, ... 240)
        /// and a row per shade, the full colours at the top (10, 20, ...), darker down to the middle (18), then the pale shades
        /// (19, 17, ...) to the palest at the bottom (11, 21, ...).
        /// </summary>
        public IReadOnlyList<AciSwatchViewModel> GridColors { get; }

        /// <summary>Gets the greys, indices 250 to 255.</summary>
        public IReadOnlyList<AciSwatchViewModel> GreyColors { get; }

        /// <summary>Gets the command that picks a colour of the index, given its number.</summary>
        public ICommand PickIndexCommand { get; }

        /// <summary>Gets the command that picks ByLayer, where offered.</summary>
        public ICommand ByLayerCommand { get; }

        /// <summary>Gets the command that picks ByBlock, where offered.</summary>
        public ICommand ByBlockCommand { get; }

        /// <summary>
        /// Gets or sets the colour as the Color box of the Index Color tab writes it: Red to White, a number 1 to 255, ByLayer,
        /// ByBlock, or red, green and blue as 10,20,30. Text that names no colour is left and the box shows the colour chosen.
        /// </summary>
        public string ColorBoxText
        {
            get => Text(_color);
            set
            {
                if (TryParse(value, out DxfColor color) && (OffersByLayerAndByBlock || (!color.IsByLayer && !color.IsByBlock)))
                {
                    Choose(color, false);
                }
                else
                {
                    Raise(nameof(ColorBoxText));
                }
            }
        }

        /// <summary>Gets or sets the red of the True Color tab, 0 to 255; setting it makes the colour a true colour.</summary>
        public int Red
        {
            get => _red;
            set => SetRgb(value, _green, _blue);
        }

        /// <summary>Gets or sets the green, 0 to 255.</summary>
        public int Green
        {
            get => _green;
            set => SetRgb(_red, value, _blue);
        }

        /// <summary>Gets or sets the blue, 0 to 255.</summary>
        public int Blue
        {
            get => _blue;
            set => SetRgb(_red, _green, value);
        }

        /// <summary>Gets or sets the hue, 0 to 360 degrees, as AutoCAD's HSL colour model gives it.</summary>
        public int Hue
        {
            get => _hue;
            set => SetHsl(value, _saturation, _luminance);
        }

        /// <summary>Gets or sets the saturation, 0 to 100 per cent.</summary>
        public int Saturation
        {
            get => _saturation;
            set => SetHsl(_hue, value, _luminance);
        }

        /// <summary>Gets or sets the luminance, 0 to 100 per cent.</summary>
        public int Luminance
        {
            get => _luminance;
            set => SetHsl(_hue, _saturation, value);
        }

        /// <summary>Gets or sets the red, green and blue as hexadecimal, #0A141E; text that is not six hexadecimal digits is left.</summary>
        public string Hex
        {
            get => "#" + ((_red << 16) | (_green << 8) | _blue).ToString("X6", CultureInfo.InvariantCulture);
            set
            {
                string typed = (value ?? string.Empty).Trim().TrimStart('#');

                if (typed.Length == 6 && int.TryParse(typed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                {
                    SetRgb(rgb >> 16, (rgb >> 8) & 0xFF, rgb & 0xFF);
                }
                else
                {
                    Raise(nameof(Hex));
                }
            }
        }

        /// <summary>Gets the colour models of the True Color tab, as its Color model list offers them: HSL and RGB.</summary>
        public IReadOnlyList<string> ColorModels { get; } = new[] { Hsl, RgbModel };

        /// <summary>
        /// Gets or sets the colour model the True Color tab shows: HSL, the field of hue and saturation with the luminance bar
        /// beside it, or RGB, a bar for each of red, green and blue. Another name is left. The colour is kept either way.
        /// </summary>
        public string ColorModel
        {
            get => _model;
            set
            {
                if (ColorModels.Contains(value) && Set(ref _model, value))
                {
                    Raise(nameof(IsHslModel));
                    Raise(nameof(IsRgbModel));
                }
            }
        }

        /// <summary>Gets whether the True Color tab shows the HSL model.</summary>
        public bool IsHslModel => _model == Hsl;

        /// <summary>Gets whether the True Color tab shows the RGB model.</summary>
        public bool IsRgbModel => _model == RgbModel;

        /// <summary>
        /// Gets or sets the colour as the Color box of the True Color tab writes it, red, green and blue: "255,127,0". Text that
        /// is not three numbers 0 to 255 is left and the box shows the colour chosen.
        /// </summary>
        public string TrueColorText
        {
            get => _red.ToString(CultureInfo.InvariantCulture) + "," + _green.ToString(CultureInfo.InvariantCulture) + "," + _blue.ToString(CultureInfo.InvariantCulture);
            set
            {
                string[] parts = (value ?? string.Empty).Split(',');

                if (parts.Length == 3 && byte.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                    && byte.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                    && byte.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
                {
                    SetRgb(r, g, b);
                }

                Raise(nameof(TrueColorText));
            }
        }

        /// <summary>Gets where the hue lies across the field, 0 at its left (0 degrees) to 1 at its right (360), for its marker.</summary>
        public double HueAcross => _hue / 360.0;

        /// <summary>Gets where the saturation lies down the field, 0 at its top (100 %) to 1 at its foot (0 %), for its marker.</summary>
        public double SaturationDown => 1.0 - _saturation / 100.0;

        /// <summary>Gets where the luminance lies down its bar, 0 at its top (100 %, white) to 1 at its foot (0 %, black), for its marker.</summary>
        public double LuminanceDown => 1.0 - _luminance / 100.0;

        /// <summary>Gets where the red lies along its bar, 0 at its left to 1 at its right (255).</summary>
        public double RedAlong => _red / 255.0;

        /// <summary>Gets where the green lies along its bar.</summary>
        public double GreenAlong => _green / 255.0;

        /// <summary>Gets where the blue lies along its bar.</summary>
        public double BlueAlong => _blue / 255.0;

        /// <summary>
        /// Gets the field of the HSL model: hue from left to right, 0 to 360 degrees, saturation from the top, 100 %, to the
        /// foot, 0 %, at a luminance of 50 %, as AutoCAD's. Drawn once for all the dialogs.
        /// </summary>
        public ImageSource HueSaturationField => ColorFieldBitmaps.Field(FieldWidth, FieldHeight);

        /// <summary>Gets the luminance bar of the hue and saturation picked: white at its top, their colour at 50 %, black at its foot.</summary>
        public ImageSource LuminanceBar => ColorFieldBitmaps.Luminance(_hue, _saturation, FieldHeight);

        /// <summary>Gets the bar of red: from no red to full red, the green and blue as they are.</summary>
        public ImageSource RedBar => ColorFieldBitmaps.Channel(0, _green, _blue);

        /// <summary>Gets the bar of green: from no green to full green, the red and blue as they are.</summary>
        public ImageSource GreenBar => ColorFieldBitmaps.Channel(1, _red, _blue);

        /// <summary>Gets the bar of blue: from no blue to full blue, the red and green as they are.</summary>
        public ImageSource BlueBar => ColorFieldBitmaps.Channel(2, _red, _green);

        /// <summary>
        /// Picks the hue and the saturation where the field of the HSL model is clicked or dragged, the luminance kept: the hue
        /// from the left edge, 0, to the right, 360; the saturation from the top, 100, to the foot, 0. A point outside the field
        /// is taken to its edge. A field 220 wide and 200 high clicked at 55, 50 picks hue 90 and saturation 75.
        /// </summary>
        /// <param name="x">Where across the field, from its left edge.</param>
        /// <param name="y">Where down the field, from its top edge.</param>
        /// <param name="width">The width of the field.</param>
        /// <param name="height">The height of the field.</param>
        public void PickField(double x, double y, double width, double height)
        {
            if (!(width > 0.0) || !(height > 0.0))
            {
                return;
            }

            SetHsl((int)Math.Round(Fraction(x / width) * 360.0), (int)Math.Round((1.0 - Fraction(y / height)) * 100.0), _luminance);
        }

        /// <summary>
        /// Picks the luminance where its bar is clicked or dragged, the hue and saturation kept: from the top, 100, to the foot,
        /// 0. A bar 200 high clicked at 50 picks 75.
        /// </summary>
        /// <param name="y">Where down the bar, from its top edge.</param>
        /// <param name="height">The height of the bar.</param>
        public void PickLuminance(double y, double height)
        {
            if (height > 0.0)
            {
                SetHsl(_hue, _saturation, (int)Math.Round((1.0 - Fraction(y / height)) * 100.0));
            }
        }

        /// <summary>
        /// Picks red, green or blue where its bar of the RGB model is clicked or dragged, the other two kept: from the left edge,
        /// 0, to the right, 255.
        /// </summary>
        /// <param name="channel">0 red, 1 green, 2 blue.</param>
        /// <param name="x">Where along the bar, from its left edge.</param>
        /// <param name="width">The width of the bar.</param>
        public void PickChannel(int channel, double x, double width)
        {
            if (!(width > 0.0) || channel < 0 || channel > 2)
            {
                return;
            }

            int value = (int)Math.Round(Fraction(x / width) * 255.0);
            SetRgb(channel == 0 ? value : _red, channel == 1 ? value : _green, channel == 2 ? value : _blue);
        }

        private static double Fraction(double value) => double.IsNaN(value) ? 0.0 : Math.Max(0.0, Math.Min(1.0, value));

        /// <summary>Picks a colour of the index, 1 to 255; another number is left.</summary>
        /// <param name="index">The index.</param>
        public void PickIndex(int index)
        {
            if (index >= 1 && index <= 255)
            {
                Choose(DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index), false);
            }
        }

        /// <summary>The name of an index as AutoCAD gives it: Red to White for 1 to 7, else the number.</summary>
        internal static string Text(DxfColor color)
        {
            if (color.IsByLayer)
            {
                return "ByLayer";
            }

            if (color.IsByBlock)
            {
                return "ByBlock";
            }

            if (color.IsByColor)
            {
                return color.Red.ToString(CultureInfo.InvariantCulture) + "," + color.Green.ToString(CultureInfo.InvariantCulture) + "," + color.Blue.ToString(CultureInfo.InvariantCulture);
            }

            return color.ColorIndex >= 1 && color.ColorIndex <= 7 ? Names[color.ColorIndex] : color.ColorIndex.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A colour as Properties and the Color box write it: ByLayer, ByBlock, Red to White, 1 to 255, or 10,20,30.</summary>
        internal static bool TryParse(string text, out DxfColor color)
        {
            color = null;
            string typed = (text ?? string.Empty).Trim();

            if (string.Equals(typed, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                color = DxfColor.ByLayer;
            }
            else if (string.Equals(typed, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                color = DxfColor.ByBlock;
            }
            else if (Array.FindIndex(Names, n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase)) is int named && named > 0)
            {
                color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)named);
            }
            else if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                color = index >= 1 && index <= 255 ? DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index) : null;
            }
            else
            {
                string[] parts = typed.Split(',');

                if (parts.Length == 3 && byte.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                    && byte.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                    && byte.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
                {
                    color = DxfColor.FromRgb(r, g, b);
                }
            }

            return color != null;
        }

        private static string Describe(DxfColor color)
        {
            if (color.IsByLayer || color.IsByBlock)
            {
                return Text(color);
            }

            return color.IsByColor ? "Red, Green, Blue: " + Text(color) : "Index color: " + color.ColorIndex.ToString(CultureInfo.InvariantCulture);
        }

        private static int Swatch(DxfColor color) => color.IsByLayer || color.IsByBlock ? 0xFFFFFF : color.ColorValue & 0xFFFFFF;

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static DxfColor Rgb(int red, int green, int blue) => DxfColor.FromRgb((byte)Clamp(red, 0, 255), (byte)Clamp(green, 0, 255), (byte)Clamp(blue, 0, 255));

        /// <summary>
        /// The true colour of a red, a green and a blue typed; the same as shown is left alone, so that a box or a slider giving
        /// back what it was given does not make a colour of the index a true colour.
        /// </summary>
        private void SetRgb(int red, int green, int blue)
        {
            red = Clamp(red, 0, 255);
            green = Clamp(green, 0, 255);
            blue = Clamp(blue, 0, 255);

            if (red != _red || green != _green || blue != _blue)
            {
                Choose(Rgb(red, green, blue), false);
            }
        }

        /// <summary>The true colour of a hue, a saturation and a luminance typed, kept as typed; the same as shown is left alone.</summary>
        private void SetHsl(int hue, int saturation, int luminance)
        {
            hue = Clamp(hue, 0, 360);
            saturation = Clamp(saturation, 0, 100);
            luminance = Clamp(luminance, 0, 100);

            if (hue == _hue && saturation == _saturation && luminance == _luminance)
            {
                return;
            }

            _hue = hue;
            _saturation = saturation;
            _luminance = luminance;
            ChooseHsl();
        }

        /// <summary>The true colour of the hue, saturation and luminance shown, which are kept as typed.</summary>
        private void ChooseHsl()
        {
            int rgb = HslToRgb(_hue, _saturation, _luminance);
            Choose(Rgb(rgb >> 16, (rgb >> 8) & 0xFF, rgb & 0xFF), true);
        }

        /// <summary>
        /// The red, green and blue of a hue (degrees), a saturation and a luminance (0 to 100) as 0xRRGGBB, as AutoCAD's HSL
        /// model gives them: hue 30, saturation 100 and luminance 50 are 255,128,0.
        /// </summary>
        internal static int HslToRgb(double hue, double saturation, double luminance)
        {
            double h = hue % 360 / 60.0;
            double l = luminance / 100.0;
            double c = (1.0 - Math.Abs(2.0 * l - 1.0)) * (saturation / 100.0);
            double x = c * (1.0 - Math.Abs(h % 2.0 - 1.0));
            double m = l - c / 2.0;
            double r = h < 1 ? c : h < 2 ? x : h < 4 ? 0.0 : h < 5 ? x : c;
            double g = h < 1 ? x : h < 3 ? c : h < 4 ? x : 0.0;
            double b = h < 2 ? 0.0 : h < 3 ? x : h < 5 ? c : x;
            return (Clamp((int)Math.Round((r + m) * 255.0), 0, 255) << 16) | (Clamp((int)Math.Round((g + m) * 255.0), 0, 255) << 8) | Clamp((int)Math.Round((b + m) * 255.0), 0, 255);
        }

        /// <summary>Makes a colour the one chosen and shows it on both tabs; the hue, saturation and luminance kept where they were typed.</summary>
        private void Choose(DxfColor color, bool keepHsl)
        {
            _color = color;

            if (!color.IsByLayer && !color.IsByBlock)
            {
                _red = color.Red;
                _green = color.Green;
                _blue = color.Blue;

                if (!keepHsl)
                {
                    double r = _red / 255.0;
                    double g = _green / 255.0;
                    double b = _blue / 255.0;
                    double max = Math.Max(r, Math.Max(g, b));
                    double min = Math.Min(r, Math.Min(g, b));
                    double delta = max - min;
                    double l = (max + min) / 2.0;
                    double hue = delta <= 0.0 ? 0.0 : max == r ? 60.0 * (((g - b) / delta % 6.0 + 6.0) % 6.0) : max == g ? 60.0 * ((b - r) / delta + 2.0) : 60.0 * ((r - g) / delta + 4.0);
                    _hue = (int)Math.Round(hue) % 360;
                    _saturation = delta <= 0.0 ? 0 : (int)Math.Round(100.0 * delta / (1.0 - Math.Abs(2.0 * l - 1.0)));
                    _luminance = (int)Math.Round(100.0 * l);
                }
            }

            foreach (AciSwatchViewModel swatch in _swatches)
            {
                swatch.IsSelected = color.IsByAci && swatch.Index == color.ColorIndex;
            }

            foreach (string name in new[] { nameof(Color), nameof(ColorRgb), nameof(ColorText), nameof(ColorBoxText), nameof(Red), nameof(Green), nameof(Blue), nameof(Hue), nameof(Saturation), nameof(Luminance), nameof(Hex),
                nameof(TrueColorText), nameof(HueAcross), nameof(SaturationDown), nameof(LuminanceDown), nameof(RedAlong), nameof(GreenAlong), nameof(BlueAlong),
                nameof(LuminanceBar), nameof(RedBar), nameof(GreenBar), nameof(BlueBar) })
            {
                Raise(name);
            }
        }
    }

    /// <summary>One colour of AutoCAD's index as the Select Color dialog shows it: its number, its red, green and blue, and whether it is chosen.</summary>
    public sealed class AciSwatchViewModel : ObservableObject
    {
        private bool _isSelected;

        internal AciSwatchViewModel(int index)
        {
            Index = index;
            Rgb = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index).ColorValue & 0xFFFFFF;
        }

        /// <summary>Gets the index, 1 to 255.</summary>
        public int Index { get; }

        /// <summary>Gets the colour as 0xRRGGBB: the very colour the canvas draws for the index, 0xFF7F00 for 30.</summary>
        public int Rgb { get; }

        /// <summary>Gets what hovering shows: "Index color: 30 · 255,127,0".</summary>
        public string ToolTip => "Index color: " + Index.ToString(CultureInfo.InvariantCulture) + " · " + ((Rgb >> 16) & 0xFF).ToString(CultureInfo.InvariantCulture) + ","
            + ((Rgb >> 8) & 0xFF).ToString(CultureInfo.InvariantCulture) + "," + (Rgb & 0xFF).ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets whether it is the colour chosen, drawn with a white frame.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            internal set => Set(ref _isSelected, value);
        }

        /// <inheritdoc/>
        public override string ToString() => Index.ToString(CultureInfo.InvariantCulture);
    }
}
