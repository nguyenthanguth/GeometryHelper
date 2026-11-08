using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// Which dimensions a view gets and how they are laid out: the rules switched on, the scale the view is drawn at, the
    /// height of the text and the spacing of the tiers on paper, and how values are rounded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sizes on paper are in paper millimetres and become model millimetres in the view times <see cref="Scale"/>: at 1:20
    /// a text 2.5 high takes 50 of the view, and tiers 8 apart stand 160 apart. The default dimensions a part as a
    /// draughtsman does by hand: the overall sizes, a chain through the holes, and the rows of holes with their pitches.
    /// </para>
    /// <para>
    /// The arrows are as long as the text is high; the text stands a quarter of its height off its line, the extension
    /// lines start a quarter of it off their points and run on half of it past their line. Each character of a text is taken
    /// as 0.8 of its height wide when the layout keeps texts apart.
    /// </para>
    /// </remarks>
    public sealed class DimensionSettings
    {
        private double _scale = 1.0;
        private double _textHeight = 2.5;
        private double _tierSpacing = 8.0;
        private double _rounding = 1.0;

        /// <summary>Gets or sets whether the overall length, width and height of the main part are dimensioned; on by default.</summary>
        public bool Overall { get; set; } = true;

        /// <summary>Gets or sets whether a chain runs from end to end through the centres of holes and bolts; on by default.</summary>
        public bool Chain { get; set; } = true;

        /// <summary>Gets or sets whether a line of dimensions runs from the main part's first end to each point; off by default.</summary>
        public bool Cumulative { get; set; }

        /// <summary>Gets or sets whether a line of dimensions runs from the main part's work point to each point; off by default.</summary>
        public bool Ordinate { get; set; }

        /// <summary>
        /// Gets or sets whether each row of holes or bolts is dimensioned by its pitches, "n x p = total", and its distance
        /// to the ends of its part; on by default.
        /// </summary>
        public bool HolesByGroup { get; set; } = true;

        /// <summary>
        /// Gets or sets whether equal neighbours of a chain are merged into one dimension of "n x p = total"; on by default.
        /// The pitches of a row of holes are merged whatever this says.
        /// </summary>
        public bool MergeEqual { get; set; } = true;

        /// <summary>Gets or sets the scale the view is drawn at: 20 for 1:20; 1 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double Scale
        {
            get => _scale;
            set => _scale = Positive(value, nameof(Scale));
        }

        /// <summary>Gets or sets the height of the text on paper, in paper millimetres; 2.5 by default.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double TextHeight
        {
            get => _textHeight;
            set => _textHeight = Positive(value, nameof(TextHeight));
        }

        /// <summary>
        /// Gets or sets the spacing of the tiers on paper, in paper millimetres: the first tier stands this far off the box
        /// of the view's lines, each next one this far further out; 8 by default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double TierSpacing
        {
            get => _tierSpacing;
            set => _tierSpacing = Positive(value, nameof(TierSpacing));
        }

        /// <summary>
        /// Gets or sets what the values written are rounded to, in model millimetres: 1 writes 2 800.4 as 2800, 0.5 writes
        /// it as 2800.5; 1 by default. The value of a dimension keeps every digit; only its text is rounded.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double Rounding
        {
            get => _rounding;
            set => _rounding = Positive(value, nameof(Rounding));
        }

        /// <summary>
        /// The dimensions of the view placed before, which those placed with these settings keep clear of and stand beyond:
        /// a view's own, given to a drawing plugin through its context's settings. Not copied with the settings.
        /// </summary>
        internal IReadOnlyList<DrawingDimension> KeepClearOf { get; set; }

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
