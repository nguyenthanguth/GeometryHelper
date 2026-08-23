using GeometryHelper.CommonGeometry.Datatype;
using GeometryHelper.PlaneGeometry.Geometry;
using Tekla.Structures.Drawing;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert bounding boxes between Tekla Drawing API and GeometryHelper.PlaneGeometry.
    /// </summary>
    public static class RectangleBoundingBoxConvert
    {
        /// <summary>
        /// Converts a Tekla drawing bounding box to a PlaneGeometry 2D rectangle.
        /// </summary>
        /// <param name="boundingBox">The Tekla drawing bounding box to convert.</param>
        /// <returns>The converted <see cref="GeoRectangle2"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="boundingBox"/> is null.</exception>
        public static GeoRectangle2 ToGeoRectangle2(this RectangleBoundingBox boundingBox)
        {
            if (boundingBox == null)
            {
                throw new System.ArgumentNullException(nameof(boundingBox));
            }

            return new GeoRectangle2(
                boundingBox.GetCenterPoint().ToGeoPoint2(),
                boundingBox.Width,
                boundingBox.Height,
                Angle.ToRadians(boundingBox.AngleToAxis)); // Angle.FromDegrees(boundingBox.AngleToAxis).Radians
        }

        /// <summary>
        /// Converts a PlaneGeometry 2D rectangle to a Tekla drawing bounding box.
        /// </summary>
        /// <param name="boundingBox">The PlaneGeometry 2D rectangle to convert.</param>
        /// <returns>The converted Tekla drawing <see cref="RectangleBoundingBox"/>.</returns>
        public static RectangleBoundingBox ToRectangleBoundingBox(this GeoRectangle2 boundingBox)
        {
            return RectangleBoundingBox.CreateRectangleBoundingBox(
                boundingBox.Center.ToTeklaPoint(),
                boundingBox.Width,
                boundingBox.Height,
                boundingBox.AngleDeg);
        }
    }
}
