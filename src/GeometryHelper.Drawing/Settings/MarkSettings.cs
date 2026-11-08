using System;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Which parts are marked and how a mark looks: its text is the part's position mark, set on a leader from a point of
    /// the part, in the first view of the drawing.
    /// </summary>
    /// <remarks>
    /// A mark of text height 3.5 set 8 off its part on paper is 70 high and stands 160 off its part in a view at 1:20. Marks
    /// are placed simply for now, up and to the right of their point; placing them clear of lines and of one another is
    /// for the marks of G4.
    /// </remarks>
    public sealed class MarkSettings
    {
        private double _textHeight = 3.5;
        private double _offset = 8.0;

        /// <summary>Gets or sets whether the main part of each assembly is marked; on by default.</summary>
        public bool MainParts { get; set; } = true;

        /// <summary>Gets or sets whether the parts that are not main parts, the fittings, are marked; on by default.</summary>
        public bool Fittings { get; set; } = true;

        /// <summary>Gets or sets the height of the text on paper, in paper millimetres; 3.5 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double TextHeight
        {
            get => _textHeight;
            set => _textHeight = Positive(value, nameof(TextHeight));
        }

        /// <summary>
        /// Gets or sets how far the text stands off the point of its part, up and to the right, in paper millimetres; 8 by default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double Offset
        {
            get => _offset;
            set => _offset = Positive(value, nameof(Offset));
        }

        internal MarkSettings Clone() => new MarkSettings { MainParts = MainParts, Fittings = Fittings, _textHeight = _textHeight, _offset = _offset };

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
