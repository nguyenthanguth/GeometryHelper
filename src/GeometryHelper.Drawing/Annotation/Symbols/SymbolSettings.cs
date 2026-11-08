using System;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// How the symbols of a view are drawn: the height of their text, how far centre lines run past the rims of holes, and
    /// the standard weld symbols follow.
    /// </summary>
    /// <remarks>
    /// Sizes on paper are in paper millimetres and become model millimetres in the view times the view's scale: at 1:10 the
    /// centre lines of a hole of 22 run 3 past its rim on paper, 30 in the view, so its cross is 82 across.
    /// </remarks>
    public sealed class SymbolSettings
    {
        /// <summary>The name of the layer section marks, detail marks, levels and weld symbols are written on.</summary>
        public const string Layer = "Symbol";

        private double _textHeight = 2.5;
        private double _centreLineExtension = 3.0;
        private WeldStandard _weldStandard = WeldStandard.Iso2553;

        /// <summary>
        /// Gets or sets the height of the text of symbols on paper, in paper millimetres, which sizes the symbols too; 2.5 by
        /// default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double TextHeight
        {
            get => _textHeight;
            set => _textHeight = Positive(value, nameof(TextHeight));
        }

        /// <summary>Gets or sets how far a centre line runs past the rim of its hole, in paper millimetres; 3 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double CentreLineExtension
        {
            get => _centreLineExtension;
            set => _centreLineExtension = Positive(value, nameof(CentreLineExtension));
        }

        /// <summary>
        /// Gets or sets the standard weld symbols are drawn to; <see cref="WeldStandard.Iso2553"/> by default. Which the
        /// drawings use is open question Q3 of the drawing plan, and only ISO 2553 is drawn until it is answered.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not one of <see cref="WeldStandard"/>.</exception>
        public WeldStandard WeldStandard
        {
            get => _weldStandard;
            set => _weldStandard = Enum.IsDefined(typeof(WeldStandard), value) ? value : throw new ArgumentOutOfRangeException(nameof(WeldStandard), value, "The standard has to be one of WeldStandard.");
        }

        /// <summary>
        /// Gets or sets whether the elevations of the objects are marked with level marks (<see cref="LevelMark"/>); off by
        /// default, on in the general arrangement preset.
        /// </summary>
        /// <remarks>
        /// An object is marked where the source gives it the attribute "Elevation" (<see cref="LevelMark.ElevationAttribute"/>),
        /// its level in model millimetres written with a point for decimals: "3600" for a beam whose top is at 3 600. Its
        /// mark stands just right of the upper right corner of its box, at the height of its top, in every view along the
        /// axes whose up is the model's Z, as a front or an end elevation; a plan has none.
        /// </remarks>
        public bool LevelMarks { get; set; }

        internal SymbolSettings Clone() => new SymbolSettings
        {
            _textHeight = _textHeight,
            _centreLineExtension = _centreLineExtension,
            _weldStandard = _weldStandard,
            LevelMarks = LevelMarks,
        };

        private static double Positive(double value, string name)
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, "The value has to be a finite number above nought.");
            }

            return value;
        }
    }
}
