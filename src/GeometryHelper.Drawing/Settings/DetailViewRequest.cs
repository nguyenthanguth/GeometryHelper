using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// A detail a drawing is asked for: a rectangle of one of its views, drawn on its own at a larger scale.
    /// </summary>
    /// <remarks>
    /// The rectangle is in the view's own millimetres at 1:1, as its lines are: the end of a beam 3 000 long seen from the
    /// front, from (2 800, -200) to (3 100, 200), enlarged 4 times, is a detail at 1:5 where the front view is at 1:20. A
    /// circle as the crop is not made yet: a detail is cut out by a rectangle.
    /// </remarks>
    public sealed class DetailViewRequest
    {
        /// <summary>Makes the request for a detail.</summary>
        /// <param name="name">The letter or name of the detail, "B" for DETAIL B.</param>
        /// <param name="of">The view the detail is taken from: one along the axes, or the isometric view.</param>
        /// <param name="area">The rectangle of that view drawn, in the view's millimetres at 1:1.</param>
        /// <param name="enlargement">How many times larger than its view the detail is drawn: 4 for a view at 1:20 and its
        /// detail at 1:5; the scale is the nearest of the drawing's scales no smaller.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, the view is a section, a detail or a free view,
        /// or the rectangle has no area.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the enlargement is not a finite number of one or more.</exception>
        public DetailViewRequest(string name, ViewKind of, GeoRectangle2 area, double enlargement)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException("A detail needs a name.", nameof(name));
            }

            if (of == ViewKind.Section || of == ViewKind.Detail || of == ViewKind.Free)
            {
                throw new ArgumentException("A detail is taken from a view along the axes or the isometric view.", nameof(of));
            }

            if (!(area.Width > 0.0) || !(area.Height > 0.0) || double.IsInfinity(area.Width) || double.IsInfinity(area.Height))
            {
                throw new ArgumentException("The rectangle of a detail has a width and a height.", nameof(area));
            }

            if (!(enlargement >= 1.0) || double.IsInfinity(enlargement))
            {
                throw new ArgumentOutOfRangeException(nameof(enlargement), enlargement, "The enlargement has to be a finite number of one or more.");
            }

            Name = name;
            Of = of;
            Area = area;
            Enlargement = enlargement;
        }

        /// <summary>Gets the letter or name of the detail.</summary>
        public string Name { get; }

        /// <summary>Gets the view the detail is taken from.</summary>
        public ViewKind Of { get; }

        /// <summary>Gets the rectangle of that view drawn, in the view's millimetres at 1:1.</summary>
        public GeoRectangle2 Area { get; }

        /// <summary>Gets how many times larger than its view the detail is drawn.</summary>
        public double Enlargement { get; }
    }
}
