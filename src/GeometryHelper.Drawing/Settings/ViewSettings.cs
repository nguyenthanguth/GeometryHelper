using System;
using System.Collections.Generic;
using GeometryHelper.Drawing.Projection;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Which views a drawing has and how each sees the model: the views along the axes and the isometric one, the sections
    /// and details asked for, which views are dimensioned, the margin of a crop, the depth of a section, the smooth angle and
    /// whether hidden lines are drawn.
    /// </summary>
    /// <remarks>
    /// A view with no lines in it is not made. The front view of a plate 300 by 200 seen from above is the plate's outline
    /// and its holes; with <see cref="CropMargin"/> 10 its crop is 320 by 220 about it.
    /// </remarks>
    public sealed class ViewSettings
    {
        private double _cropMargin = 10.0;
        private double _sectionDepth = 50.0;
        private double _smoothAngleRad = ProjectionOptions.DefaultSmoothAngleRad;

        /// <summary>
        /// Gets the views along the axes, and the isometric one, in the order they are made: the front, top and right views
        /// by default. Sections and details are asked for in <see cref="Sections"/> and <see cref="Details"/>; a kind given
        /// twice is made once.
        /// </summary>
        public IList<ViewKind> Kinds { get; } = new List<ViewKind> { ViewKind.Front, ViewKind.Top, ViewKind.Right };

        /// <summary>Gets the sections asked for, in the order they are made; none by default.</summary>
        public IList<SectionViewRequest> Sections { get; } = new List<SectionViewRequest>();

        /// <summary>Gets the details asked for, in the order they are made; none by default.</summary>
        public IList<DetailViewRequest> Details { get; } = new List<DetailViewRequest>();

        /// <summary>
        /// Gets the kinds of view dimensioned by the rules of <see cref="DrawingSettings.Dimensions"/>: the front and top
        /// views and the sections by default. A detail and the isometric view are not dimensioned, whatever this holds.
        /// </summary>
        public IList<ViewKind> Dimensioned { get; } = new List<ViewKind> { ViewKind.Front, ViewKind.Top, ViewKind.Section };

        /// <summary>
        /// Gets the ids of the views dimensioned whatever their kind, as <see cref="Drawing.ReDimension"/> adds them: V-RIGHT
        /// dimensions the right end view of the assembly preset, which <see cref="Dimensioned"/> leaves out; a detail too. None
        /// by default; an id no view has is passed over.
        /// </summary>
        public IList<string> DimensionedViews { get; } = new List<string>();

        /// <summary>
        /// Gets the ids of the views marked besides the first view along the axes, which is always marked, as
        /// <see cref="Drawing.ReMark"/> adds them: V-TOP marks the top view too. None by default; an id no view has is passed
        /// over.
        /// </summary>
        public IList<string> MarkedViews { get; } = new List<string>();

        /// <summary>
        /// Gets or sets how far a view's crop stands off the box of its lines, in model millimetres; 10 by default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number of nought or more.</exception>
        public double CropMargin
        {
            get => _cropMargin;
            set => _cropMargin = NotNegative(value, nameof(CropMargin));
        }

        /// <summary>
        /// Gets or sets how far behind its cut a section sees, in model millimetres; 50 by default, infinity to see all.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is negative or not a number.</exception>
        public double SectionDepth
        {
            get => _sectionDepth;
            set
            {
                if (!(value >= 0.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(SectionDepth), value, "A depth has to be nought or more.");
                }

                _sectionDepth = value;
            }
        }

        /// <summary>
        /// Gets or sets the angle in radians below which the edge between two faces is smooth and drawn only where it is a
        /// silhouette, as <see cref="ProjectionOptions.SmoothAngleRad"/>; 30 degrees by default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not from nought up to a right angle.</exception>
        public double SmoothAngleRad
        {
            get => _smoothAngleRad;
            set
            {
                if (!(value >= 0.0) || !(value <= Math.PI / 2.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(SmoothAngleRad), value, "A smooth angle has to be a number from nought up to a right angle.");
                }

                _smoothAngleRad = value;
            }
        }

        /// <summary>Gets or sets whether hidden lines are drawn; on by default.</summary>
        public bool HiddenLines { get; set; } = true;

        internal ViewSettings Clone()
        {
            var copy = new ViewSettings
            {
                _cropMargin = _cropMargin,
                _sectionDepth = _sectionDepth,
                _smoothAngleRad = _smoothAngleRad,
                HiddenLines = HiddenLines,
            };

            Refill(copy.Kinds, Kinds);
            Refill(copy.Sections, Sections);
            Refill(copy.Details, Details);
            Refill(copy.Dimensioned, Dimensioned);
            Refill(copy.DimensionedViews, DimensionedViews);
            Refill(copy.MarkedViews, MarkedViews);
            return copy;
        }

        internal static void Refill<T>(IList<T> target, IEnumerable<T> source)
        {
            target.Clear();

            foreach (T item in source)
            {
                target.Add(item);
            }
        }

        private static double NotNegative(double value, string name)
        {
            if (!(value >= 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, "The value has to be a finite number of nought or more.");
            }

            return value;
        }
    }
}
