using System;
using GeometryHelper.Drawing.Annotation;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Which parts are marked and how a mark looks: what its text shows (<see cref="Content"/>), how high it is and how far
    /// it stands off its object, in the first view of the drawing and the views <see cref="ViewSettings.MarkedViews"/> names.
    /// </summary>
    /// <remarks>
    /// A mark of text height 3.5 set 4 off its part on paper is 70 high and stands 80 off its part in a view at 1:20. Marks
    /// are placed by <see cref="Marker"/>, clear of the view's dimensions and of one another and, where it can be, of the
    /// view's lines, each with a leader to a point on its object.
    /// </remarks>
    public sealed class MarkSettings
    {
        private double _textHeight = 3.5;
        private double _offset = 4.0;
        private MarkContent _content = new MarkContent();

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
        /// Gets or sets how far the text stands off the line of its object it is set beside, in paper millimetres; 4 by
        /// default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number above nought.</exception>
        public double Offset
        {
            get => _offset;
            set => _offset = Positive(value, nameof(Offset));
        }

        /// <summary>
        /// Gets or sets what the text of each kind of mark shows: by default the position mark of a part, "P1", the position
        /// mark of an assembly's main part, "C1", and the count, size and length of a group of bolts, "4 x M20 x 80".
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        public MarkContent Content
        {
            get => _content;
            set => _content = value ?? throw new ArgumentNullException(nameof(Content));
        }

        internal MarkSettings Clone() =>
            new MarkSettings
            {
                MainParts = MainParts,
                Fittings = Fittings,
                _textHeight = _textHeight,
                _offset = _offset,
                _content = new MarkContent
                {
                    Part = _content.Part,
                    Assembly = _content.Assembly,
                    Bolt = _content.Bolt,
                },
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
