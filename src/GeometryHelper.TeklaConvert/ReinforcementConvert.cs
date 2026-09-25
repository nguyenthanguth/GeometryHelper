using System;
using System.Collections;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using TSM = Tekla.Structures.Model;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert reinforcement between Tekla Structures and GeometryHelper.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There are two ways to read reinforcement, and they answer different questions.
    /// </para>
    /// <para>
    /// <b>As it ends up in the model</b> — <see cref="ToGeoPolylineArc3s(TSM.Reinforcement)"/>. This asks Tekla
    /// for the geometries it worked out, so the hooks, the offsets and the lapping are all settled, and a group
    /// gives one bar for every bar in it. It reads on any reinforcement at all: a single bar, a group, a curved
    /// or circle group, a mesh or a strand. **This is the one to use** unless there is a reason not to.
    /// </para>
    /// <para>
    /// <b>As it was set out</b> — the <c>ToSetOut…</c> methods. These read the points that were typed in and the
    /// radii that go with them, which is what a schedule was written from but not always where the bar ends up.
    /// They are named apart on purpose: had they shared the name above, a call on a variable typed
    /// <c>RebarGroup</c> would have silently taken the set-out while the same call on a variable typed
    /// <c>Reinforcement</c> took the model, which is the sort of difference no one should have to know about.
    /// </para>
    /// <para>
    /// Tekla models in millimetres, so a bending radius is in millimetres and so is everything that comes back.
    /// Nothing is scaled.
    /// </para>
    /// </remarks>
    public static class ReinforcementConvert
    {
        #region As it ends up in the model

        /// <summary>
        /// Converts every bar of a reinforcement, as Tekla works them out.
        /// </summary>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this TSM.Reinforcement reinforcement)
            => reinforcement.ToGeoPolylineArc3s(Tolerance.Global);

        /// <summary>
        /// Converts every bar of a reinforcement, as Tekla works them out, within a tolerance.
        /// </summary>
        /// <param name="reinforcement">The reinforcement: a single bar, any kind of group, a mesh or a strand.</param>
        /// <param name="tolerance">The tolerance; Tekla coordinates run large, so this is worth giving.</param>
        /// <returns>One bar for each geometry the reinforcement holds; a group gives one for every bar in it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="reinforcement"/> is null.</exception>
        /// <remarks>
        /// A geometry that cannot be read as a bar is passed over rather than stopping the rest, and the number
        /// passed over is reported through <c>GeometryHelperLog</c>, because a group of forty bars with one bad
        /// geometry is more useful than an exception.
        /// </remarks>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this TSM.Reinforcement reinforcement, Tolerance tolerance)
        {
            if (reinforcement == null) throw new ArgumentNullException(nameof(reinforcement));

            return Read(reinforcement.GetRebarGeometries(true), tolerance, reinforcement.Identifier.ToString());
        }

        /// <summary>
        /// Converts every bar of every reinforcement of a rebar set, as Tekla works them out.
        /// </summary>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this TSM.RebarSet set) => set.ToGeoPolylineArc3s(Tolerance.Global);

        /// <summary>
        /// Converts every bar of every reinforcement of a rebar set, as Tekla works them out, within a tolerance.
        /// </summary>
        /// <param name="set">The rebar set.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every bar the set holds, in the order its reinforcements come back.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="set"/> is null.</exception>
        /// <remarks>
        /// A <c>RebarSet</c> is not a <c>Reinforcement</c> — it holds them, along with its modifiers and its
        /// leg faces — so it is asked for the reinforcements it made and each of those is read as usual. There
        /// is no set-out reading for a set, because a set is not set out by points: it is set out by leg faces
        /// and guidelines, and what Tekla makes of those is the only honest answer.
        /// </remarks>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this TSM.RebarSet set, Tolerance tolerance)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            var bars = new List<GeoPolylineArc3>();
            TSM.ModelObjectEnumerator reinforcements = set.GetReinforcements();

            if (reinforcements == null)
            {
                return bars.ToArray();
            }

            while (reinforcements.MoveNext())
            {
                if (reinforcements.Current is TSM.Reinforcement reinforcement)
                {
                    bars.AddRange(reinforcement.ToGeoPolylineArc3s(tolerance));
                }
            }

            return bars.ToArray();
        }

        #endregion

        #region As it was set out

        /// <summary>
        /// Converts a single bar from the points it was set out by.
        /// </summary>
        public static GeoPolylineArc3 ToSetOutPolylineArc3(this TSM.SingleRebar rebar) => rebar.ToSetOutPolylineArc3(Tolerance.Global);

        /// <summary>
        /// Converts a single bar from the points it was set out by, within a tolerance.
        /// </summary>
        /// <param name="rebar">The bar.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The bar as it was typed in: straight runs with a tangent arc at every bend.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rebar"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the bar holds no polygon, or fewer than two distinct points.</exception>
        public static GeoPolylineArc3 ToSetOutPolylineArc3(this TSM.SingleRebar rebar, Tolerance tolerance)
        {
            if (rebar == null) throw new ArgumentNullException(nameof(rebar));

            return SetOut(rebar.Polygon, rebar.RadiusValues, nameof(rebar), tolerance);
        }

        /// <summary>
        /// Converts the bars of a group from the points they were set out by.
        /// </summary>
        public static GeoPolylineArc3[] ToSetOutPolylineArc3s(this TSM.RebarGroup group) => group.ToSetOutPolylineArc3s(Tolerance.Global);

        /// <summary>
        /// Converts the bars of a group from the points they were set out by, within a tolerance.
        /// </summary>
        /// <param name="group">The group.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One bar for each polygon the group was set out by, which is not the same as one for every bar in it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> is null.</exception>
        /// <remarks>
        /// A group is set out by one or two polygons — the shape at each end of the run — and Tekla spreads the
        /// bars between them at the spacing asked for. So this gives those one or two shapes and <b>not</b>
        /// every bar of the group; <see cref="ToGeoPolylineArc3s(TSM.Reinforcement)"/> gives every bar.
        /// </remarks>
        public static GeoPolylineArc3[] ToSetOutPolylineArc3s(this TSM.RebarGroup group, Tolerance tolerance)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            var bars = new List<GeoPolylineArc3>();

            if (group.Polygons == null)
            {
                return bars.ToArray();
            }

            int skipped = 0;

            foreach (object item in group.Polygons)
            {
                if (!(item is TSM.Polygon polygon))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    bars.Add(SetOut(polygon, group.RadiusValues, nameof(group), tolerance));
                }
                catch (ArgumentException error)
                {
                    GeometryHelperLog.Warn($"A set-out polygon of a rebar group could not be read as a bar: {error.Message}");
                    skipped++;
                }
            }

            if (skipped > 0)
            {
                GeometryHelperLog.Debug($"Passed over {skipped} set-out polygon(s) of rebar group {group.Identifier}.");
            }

            return bars.ToArray();
        }

        /// <summary>
        /// Converts a curved group from the points it was set out by.
        /// </summary>
        public static GeoPolylineArc3 ToSetOutPolylineArc3(this TSM.CurvedRebarGroup group) => group.ToSetOutPolylineArc3(Tolerance.Global);

        /// <summary>
        /// Converts a curved group from the points it was set out by, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> is null.</exception>
        public static GeoPolylineArc3 ToSetOutPolylineArc3(this TSM.CurvedRebarGroup group, Tolerance tolerance)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            return SetOut(group.Polygon, group.RadiusValues, nameof(group), tolerance);
        }

        /// <summary>
        /// Converts a circle group from the points it was set out by.
        /// </summary>
        public static GeoPolylineArc3 ToSetOutPolylineArc3(this TSM.CircleRebarGroup group) => group.ToSetOutPolylineArc3(Tolerance.Global);

        /// <summary>
        /// Converts a circle group from the points it was set out by, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="group"/> is null.</exception>
        /// <remarks>
        /// The polygon of a circle group sets out where the ring goes, not the ring itself, so what comes back
        /// is that run of points and not a circle. <see cref="ToGeoPolylineArc3s(TSM.Reinforcement)"/> gives the
        /// bars Tekla actually makes of it.
        /// </remarks>
        public static GeoPolylineArc3 ToSetOutPolylineArc3(this TSM.CircleRebarGroup group, Tolerance tolerance)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));

            return SetOut(group.Polygon, group.RadiusValues, nameof(group), tolerance);
        }

        /// <summary>
        /// Converts a strand from the two points it runs between.
        /// </summary>
        public static GeoLine3 ToSetOutLine3(this TSM.RebarStrand strand) => strand.ToSetOutLine3(Tolerance.Global);

        /// <summary>
        /// Converts a strand from the two points it runs between, within a tolerance.
        /// </summary>
        /// <param name="strand">The strand.</param>
        /// <param name="tolerance">The tolerance, which decides when the two ends are in the same place.</param>
        /// <returns>The segment the strand runs along.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="strand"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the strand has no start or no end, or when the two are in the same place.
        /// </exception>
        /// <remarks>
        /// A strand that has not been set out holds both ends at the origin, so refusing that case is worth
        /// the line: a segment of no length would otherwise measure nought and quietly spoil a total.
        /// </remarks>
        public static GeoLine3 ToSetOutLine3(this TSM.RebarStrand strand, Tolerance tolerance)
        {
            if (strand == null) throw new ArgumentNullException(nameof(strand));

            if (strand.StartPoint == null || strand.EndPoint == null)
            {
                throw new ArgumentException("A strand with no start or no end has nothing to run between.", nameof(strand));
            }

            GeoPoint3 start = strand.StartPoint.ToGeoPoint3();
            GeoPoint3 end = strand.EndPoint.ToGeoPoint3();

            if (start.IsEqualTo(end, tolerance))
            {
                throw new ArgumentException("A strand with both ends in the same place has not been set out.", nameof(strand));
            }

            return new GeoLine3(start, end);
        }

        /// <summary>
        /// Gets the outline a mesh was set out by.
        /// </summary>
        public static GeoPolygon3 ToSetOutPolygon3(this TSM.RebarMesh mesh) => mesh.ToSetOutPolygon3(Tolerance.Global);

        /// <summary>
        /// Gets the outline a mesh was set out by, within a tolerance.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="tolerance">The tolerance; the planar threshold decides how flat is flat enough.</param>
        /// <returns>The outline of the sheet, as a flat loop.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mesh"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the mesh holds no polygon, or its points are not flat.</exception>
        /// <remarks>
        /// The polygon of a mesh is the outline of the sheet and <b>not</b> a bar, which is why this is named
        /// for an outline and returns a loop. The bars of the sheet, longitudinal and cross alike, come from
        /// <see cref="ToGeoPolylineArc3s(TSM.Reinforcement)"/>.
        /// </remarks>
        public static GeoPolygon3 ToSetOutPolygon3(this TSM.RebarMesh mesh, Tolerance tolerance)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));

            if (mesh.Polygon == null)
            {
                throw new ArgumentException("A mesh with no polygon has no outline.", nameof(mesh));
            }

            return mesh.Polygon.ToGeoPolygon3(tolerance);
        }

        #endregion

        /// <summary>
        /// Reads one set-out polygon and its radii as a bar.
        /// </summary>
        private static GeoPolylineArc3 SetOut(TSM.Polygon polygon, ArrayList radiusValues, string name, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentException("There is no polygon to be set out by.", name);
            }

            return polygon.ToGeoPolylineArc3(PointConvert.ReadTeklaRadii(radiusValues), tolerance);
        }

        /// <summary>
        /// Reads the geometries Tekla worked out, passing over any that cannot be read as a bar.
        /// </summary>
        private static GeoPolylineArc3[] Read(ArrayList geometries, Tolerance tolerance, string owner)
        {
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
                        bars.Add(geometry.ToGeoPolylineArc3(tolerance));
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
                GeometryHelperLog.Debug($"Passed over {skipped} rebar geometry(ies) of {owner}.");
            }

            return bars.ToArray();
        }
    }
}
