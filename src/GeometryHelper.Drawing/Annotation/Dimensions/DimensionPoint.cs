using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// A point of a view a dimension can measure to: where it lies in the view, the point of the model it is, and the
    /// object it lies on.
    /// </summary>
    /// <remarks>
    /// A plate 300 by 200 by 20 lying from the origin, seen from above, has a corner at (300, 200) in the view; its point in
    /// the model is the corner of the top, (300, 200, 20), the one nearest the viewer of those that fall there. A dimension
    /// works its value out from the points of the model, so it stays right however the view is drawn.
    /// </remarks>
    public readonly struct DimensionPoint
    {
        /// <summary>
        /// Initializes a point of interest of a view.
        /// </summary>
        /// <param name="objectId">The id of the object the point lies on.</param>
        /// <param name="kind">What the point is on its object.</param>
        /// <param name="viewPoint">Where the point lies in the view, in view millimetres at 1:1.</param>
        /// <param name="modelPoint">The point of the model, in model millimetres.</param>
        internal DimensionPoint(string objectId, DimensionPointKind kind, GeoPoint2 viewPoint, GeoPoint3 modelPoint)
        {
            ObjectId = objectId;
            Kind = kind;
            ViewPoint = viewPoint;
            ModelPoint = modelPoint;
        }

        /// <summary>Gets the id of the object the point lies on.</summary>
        public string ObjectId { get; }

        /// <summary>Gets what the point is on its object.</summary>
        public DimensionPointKind Kind { get; }

        /// <summary>Gets where the point lies in the view, in view millimetres at 1:1: X to the right, Y up.</summary>
        public GeoPoint2 ViewPoint { get; }

        /// <summary>Gets the point of the model, in model millimetres; the view's frame takes it to <see cref="ViewPoint"/>.</summary>
        public GeoPoint3 ModelPoint { get; }

        /// <summary>
        /// Gives the point as a link of the drawing document, so that the dimension measuring to it is worked out again when
        /// the model changes.
        /// </summary>
        /// <returns>The link: the object's id and the model point.</returns>
        public DrawingLinkPoint3 ToLink() => new DrawingLinkPoint3(ObjectId, ModelPoint);

        /// <inheritdoc/>
        public override string ToString() => $"{Kind} of {ObjectId} ({ViewPoint.X:0.###}, {ViewPoint.Y:0.###})";
    }
}
