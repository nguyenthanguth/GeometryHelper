using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// Where a view is drawn on the sheet and at what scale, for writing what belongs to it: a point of the view at (x, y)
    /// goes to the sheet at <see cref="Origin"/> plus (x, y) divided by <see cref="Scale"/>.
    /// </summary>
    /// <remarks>
    /// The front view V-1 of a beam 3 000 long drawn at 1:20 with its origin at (50, 100) on the sheet puts the beam's far
    /// end, (3000, 0) in the view, at (200, 100) on the sheet.
    /// </remarks>
    public readonly struct ViewPlacement2
    {
        /// <summary>
        /// Initializes the placement of a view on the sheet.
        /// </summary>
        /// <param name="viewId">The id of the view, which the entities written are tagged with; null for none.</param>
        /// <param name="origin">Where the view's origin goes on the sheet, in paper millimetres.</param>
        /// <param name="scale">The scale the view is drawn at: 20 for 1:20.</param>
        /// <exception cref="ArgumentException">Thrown when the origin is not a point of finite numbers.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a finite number above nought.</exception>
        public ViewPlacement2(string viewId, GeoPoint2 origin, double scale)
        {
            if (double.IsNaN(origin.X) || double.IsInfinity(origin.X) || double.IsNaN(origin.Y) || double.IsInfinity(origin.Y))
            {
                throw new ArgumentException("The origin has to be a point of finite numbers.", nameof(origin));
            }

            if (!(scale > 0.0) || double.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "The scale has to be a finite number above nought.");
            }

            ViewId = viewId;
            Origin = origin;
            Scale = scale;
        }

        /// <summary>Gets the id of the view; null where it has none.</summary>
        public string ViewId { get; }

        /// <summary>Gets where the view's origin goes on the sheet, in paper millimetres.</summary>
        public GeoPoint2 Origin { get; }

        /// <summary>Gets the scale the view is drawn at: 20 for 1:20.</summary>
        public double Scale { get; }

        /// <summary>
        /// Takes a point of the view to the sheet.
        /// </summary>
        /// <param name="viewPoint">The point, in view millimetres at 1:1.</param>
        /// <returns>The point on the sheet, in paper millimetres, at the height nought.</returns>
        public GeoPoint3 ToSheet(GeoPoint2 viewPoint) => new GeoPoint3(Origin.X + viewPoint.X / Scale, Origin.Y + viewPoint.Y / Scale, 0.0);

        /// <inheritdoc/>
        public override string ToString() => $"ViewPlacement2({ViewId ?? "(no view)"} at ({Origin.X:0.###}, {Origin.Y:0.###}) 1:{Scale:0.###})";
    }
}
