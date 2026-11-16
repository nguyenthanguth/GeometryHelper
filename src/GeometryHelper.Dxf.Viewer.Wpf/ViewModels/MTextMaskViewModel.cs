using System;
using System.Globalization;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The MTEXT editor's Background Mask dialog, as AutoCAD 2021's: Use background mask, the border offset factor (a multiple
    /// of the text height from 1.0 to 5.0, 1.5 to start with) and the fill colour, the drawing's background colour or one
    /// picked in Select Color. The factor and the colour are greyed while the mask is off, the colour while the drawing's
    /// background is used. OK gives <see cref="Result"/>, which the editor sets as one step of its undo.
    /// </summary>
    public sealed class MTextMaskViewModel : ObservableObject
    {
        private bool _useMask;
        private bool _useDrawingBackground;
        private string _offsetText;
        private DxfColor _color;
        private string _notice = string.Empty;
        private string _faultBox = string.Empty;
        private readonly DelegateCommand _pick;
        private readonly double _originalFactor;

        /// <summary>Opens the dialog on an MTEXT's mask.</summary>
        /// <param name="mask">The mask the MTEXT has; null for none.</param>
        public MTextMaskViewModel(CadMTextMask mask)
        {
            _useMask = mask != null && mask.UseMask;
            _originalFactor = mask?.OffsetFactor ?? CadMTextMask.DefaultOffsetFactor;
            _offsetText = (mask?.OffsetFactor ?? CadMTextMask.DefaultOffsetFactor).ToString(
                "0.0###",
                CultureInfo.InvariantCulture
            );
            _useDrawingBackground = mask != null && mask.UseDrawingBackground;
            _color = mask?.FillColor;
            _pick = new DelegateCommand(_ => ChooseColor(), _ => IsColorEnabled);
        }

        /// <summary>Gets or sets what shows Select Color over a colour and gives back true for OK; null where no window shows it.</summary>
        public Func<SelectColorViewModel, bool> PickColor { get; set; }

        /// <summary>Gets the command of the colour box: Select Color, through <see cref="PickColor"/>.</summary>
        public ICommand PickColorCommand => _pick;

        /// <summary>Gets or sets whether the words stand on a mask (Use background mask).</summary>
        public bool UseMask
        {
            get => _useMask;
            set
            {
                if (Set(ref _useMask, value))
                {
                    Notice = string.Empty;
                    Raise(nameof(IsOffsetEnabled));
                    Raise(nameof(IsDrawingBackgroundEnabled));
                    Raise(nameof(IsColorEnabled));
                    _pick.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets whether the border offset factor can be set: while the mask is on.</summary>
        public bool IsOffsetEnabled => _useMask;

        /// <summary>Gets whether Use drawing background color can be ticked: while the mask is on.</summary>
        public bool IsDrawingBackgroundEnabled => _useMask;

        /// <summary>Gets whether the colour can be picked: while the mask is on and does not take the drawing's background.</summary>
        public bool IsColorEnabled => _useMask && !_useDrawingBackground;

        /// <summary>Gets or sets whether the mask takes the drawing's background colour (Use drawing background color).</summary>
        public bool UseDrawingBackground
        {
            get => _useDrawingBackground;
            set
            {
                if (Set(ref _useDrawingBackground, value))
                {
                    Raise(nameof(IsColorEnabled));
                    _pick.RaiseCanExecuteChanged();
                    Raise(nameof(ColorRgb));
                    Raise(nameof(ColorText));
                }
            }
        }

        /// <summary>Gets or sets the border offset factor as typed.</summary>
        public string OffsetText
        {
            get => _offsetText;
            set
            {
                if (Set(ref _offsetText, value ?? string.Empty))
                {
                    Notice = string.Empty;
                    FaultBox = string.Empty;
                }
            }
        }

        /// <summary>Gets the colour of the mask as 0xRRGGBB, its swatch: the drawing's background (black) while that is used, white for a colour of none.</summary>
        public int ColorRgb => _useDrawingBackground ? 0 : Swatch().ColorRgb;

        /// <summary>Gets the colour of the mask in words: "Index color: 30", "Red, Green, Blue: 10,20,30", "ByLayer" for none, "Drawing background" while that is used.</summary>
        public string ColorText =>
            _useDrawingBackground ? "Drawing background"
            : _color == null ? "ByLayer"
            : Swatch().ColorText;

        /// <summary>Gets the refusal; empty for none.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value);
        }

        /// <summary>Gets the tag of the box at fault, "Offset"; empty for none.</summary>
        public string FaultBox
        {
            get => _faultBox;
            private set => Set(ref _faultBox, value);
        }

        /// <summary>Gets the mask OK took; null before it.</summary>
        public CadMTextMask Result { get; private set; }

        /// <summary>
        /// OK: takes the mask, or refuses a factor that is not a number from 1 to 5, as AutoCAD does ("Enter a value between
        /// 1.0 and 5.0"), naming the box.
        /// </summary>
        /// <returns>Whether the dialog may close.</returns>
        public bool Ok()
        {
            double factor = _originalFactor;

            if (_useMask)
            {
                bool number = double.TryParse(
                    (_offsetText ?? string.Empty).Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out factor
                );

                if (!number || double.IsNaN(factor) || !(factor >= 1.0 && factor <= 5.0))
                {
                    Notice = "The border offset factor must be a number from 1.0 to 5.0.";
                    FaultBox = string.Empty;
                    FaultBox = "Offset";
                    return false;
                }
            }
            else if (
                double.TryParse(_offsetText, NumberStyles.Float, CultureInfo.InvariantCulture, out double kept)
                && kept >= 1.0
                && kept <= 5.0
            )
            {
                // A mask left off keeps the factor it had, when it is a good one.
                factor = kept;
            }

            Result = new CadMTextMask(_useMask, factor, _useDrawingBackground, _color);
            return true;
        }

        private SelectColorViewModel Swatch() => new SelectColorViewModel(_color, false);

        private void ChooseColor()
        {
            var dialog = Swatch();

            if (PickColor == null || !PickColor(dialog) || dialog.Color.IsByLayer || dialog.Color.IsByBlock)
            {
                return;
            }

            _color = dialog.Color;
            Raise(nameof(ColorRgb));
            Raise(nameof(ColorText));
        }
    }
}
