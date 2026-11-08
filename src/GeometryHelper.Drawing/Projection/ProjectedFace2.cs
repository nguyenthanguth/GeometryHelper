using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// A face a section plane cuts out of one object, in view millimetres at 1:1, for hatching.
    /// </summary>
    public sealed class ProjectedFace2
    {
        /// <summary>
        /// Initializes a section face.
        /// </summary>
        internal ProjectedFace2(string objectId, GeoFace2 face)
        {
            ObjectId = objectId;
            Face = face;
        }

        /// <summary>
        /// Gets the id of the object cut.
        /// </summary>
        public string ObjectId { get; }

        /// <summary>
        /// Gets the face: the outline of the cut, with a hole for each hole the plane passes through.
        /// </summary>
        public GeoFace2 Face { get; }

        /// <inheritdoc/>
        public override string ToString() => $"Face2({ObjectId}, Area: {Face.Area:0.###})";
    }
}
