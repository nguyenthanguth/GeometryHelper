using System;
using System.Collections;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Converts reinforcement between Tekla Structures and GeometryHelper.
    /// <para>
    /// Tekla sets a bar out the way a schedule does: the points it turns at, and a bending radius for each
    /// turn. The bar itself is not that polyline — it is that polyline with a tangent arc at every bend, so
    /// it is shorter than its set-out and it does not pass through its own corners. A
    /// <see cref="GeoPolylineArc3"/> holds exactly that, and the arithmetic to get there is
    /// <see cref="GeometryHelper.Core.Corner3"/>.
    /// </para>
    /// <para>
    /// The geometry is separated from the Tekla plumbing on purpose. Everything that turns points and radii
    /// into a bar works on <c>Tekla.Structures.Geometry3d</c> types, which need no running Tekla and are
    /// covered by the tests; only the overloads taking a <c>Reinforcement</c> or a <c>RebarGeometry</c> reach
    /// into <c>Tekla.Structures.Model</c>, and those cannot be exercised without the modeller.
    /// </para>
    /// <para>
    /// Tekla models in millimetres, so a bending radius is in millimetres and so is everything that comes
    /// back. Nothing is scaled.
    /// </para>
    /// </summary>
    public static class RebarConvert
    {
        /// <summary>
        /// Builds a bar from the points it turns at and the radius of each turn.
        /// </summary>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this IList<TSG.Point> points, IList<double> bendingRadii)
            => ToGeoPolylineArc3(points, bendingRadii, Tolerance.Global);

        /// <summary>
        /// Builds a bar from the points it turns at and the radius of each turn, within a tolerance.
        /// </summary>
        /// <param name="points">The points the bar is set out by, in order; at least two.</param>
        /// <param name="bendingRadii">
        /// A radius for each bend, in order, the first belonging to the first point that turns. A list as
        /// long as the points is read the same way, with the entries for the two ends ignored, because
        /// nothing turns at the end of a bar. A radius of nought, or a short list, leaves that bend square.
        /// </param>
        /// <param name="tolerance">The tolerance; Tekla coordinates run large, so this is worth giving.</param>
        /// <returns>The bar: straight runs with a tangent arc at every bend that had room for one.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the points are null.</exception>
        /// <exception cref="ArgumentException">Thrown when fewer than two distinct points are given.</exception>
        /// <remarks>
        /// A bend with too little straight run either side of it to fit its radius is left square rather
        /// than forced, and where two bends want more of the run between them than it is long, the one
        /// taking more of it gives way. Both are reported through <c>GeometryHelperLog</c> at debug level.
        /// </remarks>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this IList<TSG.Point> points, IList<double> bendingRadii, Tolerance tolerance)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));

            var vertices = new List<GeoPoint3>(points.Count);

            foreach (TSG.Point point in points)
            {
                if (point == null)
                {
                    throw new ArgumentException("A bar cannot be set out through a point that is not there.", nameof(points));
                }

                vertices.Add(PointConvert.ToGeoPoint3(point));
            }

            var chain = new GeoPolyline3(vertices);

            if (bendingRadii == null || bendingRadii.Count == 0)
            {
                return new GeoPolylineArc3(chain);
            }

            return chain.Fillet(ByVertex(bendingRadii, chain.VertexCount), tolerance);
        }

        /// <summary>
        /// Builds a bar from the shape Tekla worked out for it and the radius of each turn.
        /// </summary>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSG.PolyLine shape, IList<double> bendingRadii)
            => ToGeoPolylineArc3(shape, bendingRadii, Tolerance.Global);

        /// <summary>
        /// Builds a bar from the shape Tekla worked out for it and the radius of each turn, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the shape is null.</exception>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSG.PolyLine shape, IList<double> bendingRadii, Tolerance tolerance)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));

            return ToGeoPolylineArc3(Points(shape.Points, nameof(shape)), bendingRadii, tolerance);
        }

        /// <summary>
        /// Builds a bar from one of the geometries Tekla works out for a reinforcement.
        /// </summary>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSM.RebarGeometry geometry) => ToGeoPolylineArc3(geometry, Tolerance.Global);

        /// <summary>
        /// Builds a bar from one of the geometries Tekla works out for a reinforcement, within a tolerance.
        /// </summary>
        /// <param name="geometry">The geometry, as <c>Reinforcement.GetRebarGeometries</c> hands it back.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The bar, bent to the radii Tekla worked out for it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the geometry is null.</exception>
        /// <remarks>
        /// This is the one to prefer for anything that has to match the model: Tekla has already settled the
        /// hooks, the offsets and the lapping by the time it hands a geometry back, where the set-out points
        /// on the reinforcement itself have not.
        /// </remarks>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSM.RebarGeometry geometry, Tolerance tolerance)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));

            return ToGeoPolylineArc3(geometry.Shape, Radii(geometry.BendingRadiuses), tolerance);
        }

        /// <summary>
        /// Builds a bar from a single reinforcing bar as it is set out.
        /// </summary>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSM.SingleRebar rebar) => ToGeoPolylineArc3(rebar, Tolerance.Global);

        /// <summary>
        /// Builds a bar from a single reinforcing bar as it is set out, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the bar is null.</exception>
        /// <remarks>
        /// This reads the points the bar was set out by, which is not always where the bar ends up: hooks and
        /// offsets are settled later. <see cref="ToGeoPolylineArc3(TSM.RebarGeometry)"/> is the one to use
        /// where the answer has to match the model.
        /// </remarks>
        public static GeoPolylineArc3 ToGeoPolylineArc3(this TSM.SingleRebar rebar, Tolerance tolerance)
        {
            if (rebar == null) throw new ArgumentNullException(nameof(rebar));

            if (rebar.Polygon == null)
            {
                throw new ArgumentException("A bar with no polygon has no points to be set out by.", nameof(rebar));
            }

            return ToGeoPolylineArc3(Points(rebar.Polygon.Points, nameof(rebar)), Radii(rebar.RadiusValues), tolerance);
        }

        /// <summary>
        /// Builds every bar of a reinforcement, as Tekla works them out.
        /// </summary>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this TSM.Reinforcement reinforcement) => ToGeoPolylineArc3s(reinforcement, Tolerance.Global);

        /// <summary>
        /// Builds every bar of a reinforcement, as Tekla works them out, within a tolerance.
        /// </summary>
        /// <param name="reinforcement">The reinforcement: a single bar, a group, a mesh or a circle group.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One bar for each geometry the reinforcement holds; a group gives one for every bar in it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the reinforcement is null.</exception>
        /// <remarks>
        /// A geometry that cannot be read as a bar is passed over rather than stopping the rest, and the
        /// number passed over is reported through <c>GeometryHelperLog</c>, because a group of forty bars
        /// with one bad geometry is more useful than an exception.
        /// </remarks>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this TSM.Reinforcement reinforcement, Tolerance tolerance)
        {
            if (reinforcement == null) throw new ArgumentNullException(nameof(reinforcement));

            ArrayList geometries = reinforcement.GetRebarGeometries(true);
            var bars = new List<GeoPolylineArc3>();
            int skipped = 0;

            if (geometries != null)
            {
                foreach (object item in geometries)
                {
                    if (!(item is TSM.RebarGeometry geometry))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        bars.Add(ToGeoPolylineArc3(geometry, tolerance));
                    }
                    catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
                    {
                        // One bar that cannot be read should not cost the other thirty-nine.
                        GeometryHelperLog.Warn($"A rebar geometry could not be read as a bar: {error.Message}");
                        skipped++;
                    }
                }
            }

            if (skipped > 0)
            {
                GeometryHelperLog.Debug($"Passed over {skipped} rebar geometry(ies) of {reinforcement.Identifier}.");
            }

            return bars.ToArray();
        }

        /// <summary>
        /// Gets the radius wanted at each vertex, from a list of radii given one per bend.
        /// </summary>
        /// <remarks>
        /// Tekla gives a radius for each bend, and a bar of n points has n - 2 of them. GeometryHelper reads
        /// a radius at each vertex, so the first bend belongs to the second point. A list already as long as
        /// the points is taken as it is, because that is the layout GeometryHelper uses and a caller who has
        /// built one should not have it shifted underneath them.
        /// </remarks>
        private static double[] ByVertex(IList<double> bendingRadii, int vertexCount)
        {
            if (bendingRadii.Count >= vertexCount)
            {
                var asGiven = new double[bendingRadii.Count];

                for (int i = 0; i < bendingRadii.Count; i++)
                {
                    asGiven[i] = bendingRadii[i];
                }

                return asGiven;
            }

            var byVertex = new double[vertexCount];

            for (int i = 0; i < bendingRadii.Count && i + 1 < vertexCount; i++)
            {
                byVertex[i + 1] = bendingRadii[i];
            }

            return byVertex;
        }

        /// <summary>
        /// Reads a Tekla list of points, which is untyped.
        /// </summary>
        private static IList<TSG.Point> Points(ArrayList points, string name)
        {
            if (points == null)
            {
                throw new ArgumentException("A bar with no points cannot be set out.", name);
            }

            var kept = new List<TSG.Point>(points.Count);

            foreach (object item in points)
            {
                if (item is TSG.Point point)
                {
                    kept.Add(point);
                }
            }

            return kept;
        }

        /// <summary>
        /// Reads a Tekla list of bending radii, which is untyped and may hold nothing at all.
        /// </summary>
        private static IList<double> Radii(ArrayList radii)
        {
            var kept = new List<double>();

            if (radii == null)
            {
                return kept;
            }

            foreach (object item in radii)
            {
                // A radius that is not a number leaves that bend square rather than stopping the bar.
                kept.Add(item is double radius && !double.IsNaN(radius) && radius > 0.0 ? radius : 0.0);
            }

            return kept;
        }
    }
}
