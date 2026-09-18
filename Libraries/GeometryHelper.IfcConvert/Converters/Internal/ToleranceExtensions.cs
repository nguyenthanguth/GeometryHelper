using GeometryHelper.CommonGeometry;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Tolerance adjustments for building polygons from IFC geometry.
    /// </summary>
    internal static class ToleranceExtensions
    {
        /// <summary>
        /// Returns the tolerance to construct <c>GeoPolygon3</c> instances with.
        /// <para>
        /// <c>GeoPolygon3</c> rejects a loop whose area is below <see cref="Tolerance.EqualVector"/>, reading that
        /// value as an area. With the default 1e-4 and geometry output in metres that threshold is 100 mm2, so real
        /// faces of steel parts (a 10 x 10 mm bar end, the triangles lining a bolt hole) would be dropped silently
        /// and the solid left open. Here the area threshold is tied to the point tolerance instead
        /// (EqualPoint squared), which is scale-consistent with the output unit. All other fields are unchanged.
        /// </para>
        /// </summary>
        public static Tolerance ForConstruction(this Tolerance tolerance)
        {
            double areaThreshold = tolerance.EqualPoint * tolerance.EqualPoint;
            if (areaThreshold >= tolerance.EqualVector)
            {
                return tolerance;
            }

            return new Tolerance(tolerance.EqualPoint, areaThreshold, tolerance.EqualAngleRad, tolerance.EqualPlanar);
        }
    }
}
