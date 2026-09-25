using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using TSM = Tekla.Structures.Model;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert the geometry Tekla works out for a reinforcing bar.
    /// </summary>
    /// <remarks>
    /// A <c>RebarGeometry</c> is what <c>Reinforcement.GetRebarGeometries</c> hands back: the shape of one bar
    /// as it ends up in the model, with the radius Tekla settled on for each of its bends. This is the reading
    /// to prefer for anything that has to match the model, because the hooks, the offsets and the lapping are
    /// all resolved by the time a geometry appears, where the points a bar was set out by do not show them.
    /// </remarks>
    public static class RebarGeometryConvert
    {
        /// <summary>
        /// Converts the geometry of a bar to a chain that may curve.
        /// </summary>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSM.RebarGeometry geometry) => geometry.ToGeoPolylineArc3(Tolerance.Global);

        /// <summary>
        /// Converts the geometry of a bar to a chain that may curve, within a tolerance.
        /// </summary>
        /// <param name="geometry">The geometry, as <c>Reinforcement.GetRebarGeometries</c> hands it back.</param>
        /// <param name="tolerance">The tolerance; Tekla coordinates run large, so this is worth giving.</param>
        /// <returns>The bar: straight runs with a tangent arc at every bend.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the geometry holds no shape, or fewer than two distinct points.</exception>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSM.RebarGeometry geometry, Tolerance tolerance)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));

            if (geometry.Shape == null)
            {
                throw new ArgumentException("A rebar geometry with no shape has no points to follow.", nameof(geometry));
            }

            return geometry.Shape.ToGeoPolylineArc3(BendingRadii(geometry), tolerance);
        }

        /// <summary>
        /// Gets the radius of the bar, which is half the diameter Tekla holds.
        /// </summary>
        /// <param name="geometry">The geometry of the bar.</param>
        /// <returns>The radius of the bar itself, in millimetres, not the radius of any of its bends.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="geometry"/> is null.</exception>
        /// <remarks>
        /// Worth a method of its own because <c>Diameter</c> and a bending radius are both lengths about a bar
        /// and are not the same thing at all: this one is how thick the bar is.
        /// </remarks>
        public static double ToBarRadius(this TSM.RebarGeometry geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));

            return geometry.Diameter * 0.5;
        }

        /// <summary>
        /// Reads the bending radii of a geometry, which Tekla holds in an untyped list.
        /// </summary>
        private static List<double> BendingRadii(TSM.RebarGeometry geometry) => PointConvert.ReadTeklaRadii(geometry.BendingRadiuses);
    }
}
