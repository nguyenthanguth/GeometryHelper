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
    /// A bar is read as Tekla works it out, never as it was typed in. Asking Tekla for its geometries means
    /// the hooks, the offsets and the lapping are all settled, and a group gives one bar for every bar in it;
    /// the points a bar was set out by show none of that.
    /// </para>
    /// <para>
    /// Tekla sets a bar out by the points it turns at and a bending radius for each turn, and the bar itself
    /// is that polyline with a tangent arc at every bend, so it is shorter than its set-out and does not pass
    /// through its own corners. <see cref="GeoPolylineArc3"/> holds exactly that.
    /// </para>
    /// <para>
    /// Tekla models in millimetres, so everything that comes back is in millimetres. Nothing is scaled.
    /// </para>
    /// </remarks>
    public static class ReinforcementConvert
    {
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
        /// <exception cref="InvalidOperationException">Thrown when Tekla Structures is not running, which it must be for this to be asked at all.</exception>
        /// <remarks>
        /// A geometry that cannot be read as a bar is passed over rather than stopping the rest, and the number
        /// passed over is reported through <c>GeometryHelperLog</c>, because a group of forty bars with one bad
        /// geometry is more useful than an exception.
        /// </remarks>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this TSM.Reinforcement reinforcement, Tolerance tolerance)
        {
            if (reinforcement == null) throw new ArgumentNullException(nameof(reinforcement));

            using (new GlobalWorkPlane())
            {
                return Read(reinforcement, tolerance);
            }
        }

        /// <summary>
        /// Converts every bar of every reinforcement given, as Tekla works them out.
        /// </summary>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this IEnumerable<TSM.Reinforcement> reinforcements)
            => reinforcements.ToGeoPolylineArc3s(Tolerance.Global);

        /// <summary>
        /// Converts every bar of every reinforcement given, as Tekla works them out, within a tolerance.
        /// </summary>
        /// <param name="reinforcements">The reinforcements, in any sequence; an array of them reads here too.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Every bar of every one of them, in the order they were given.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sequence is null.</exception>
        /// <remarks>
        /// Worth using over a loop of single calls rather than only shorter to write: where a Tekla build needs
        /// the work plane turned to global first, this turns it once for the whole lot and puts it back once,
        /// instead of once per reinforcement. A null in the sequence is passed over.
        /// </remarks>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this IEnumerable<TSM.Reinforcement> reinforcements, Tolerance tolerance)
        {
            if (reinforcements == null) throw new ArgumentNullException(nameof(reinforcements));

            var wanted = new List<TSM.Reinforcement>();

            foreach (TSM.Reinforcement reinforcement in reinforcements)
            {
                if (reinforcement != null)
                {
                    wanted.Add(reinforcement);
                }
            }

            // Nothing to read is nothing to do. Worth the check rather than falling through: the work plane
            // is model-wide state and turning it needs a running Tekla, neither of which an empty sequence
            // has any business asking for.
            if (wanted.Count == 0)
            {
                return new GeoPolylineArc3[0];
            }

            var bars = new List<GeoPolylineArc3>();

            using (new GlobalWorkPlane())
            {
                foreach (TSM.Reinforcement reinforcement in wanted)
                {
                    bars.AddRange(Read(reinforcement, tolerance));
                }
            }

            return bars.ToArray();
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
        /// A <c>RebarSet</c> is not a <c>Reinforcement</c> — it holds them, along with its modifiers and its leg
        /// faces — so it is asked for the reinforcements it made and each of those is read as usual.
        /// </remarks>
        public static GeoPolylineArc3[] ToGeoPolylineArc3s(this TSM.RebarSet set, Tolerance tolerance)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));

            return Held(set).ToGeoPolylineArc3s(tolerance);
        }

        /// <summary>
        /// Gets the reinforcements a rebar set made.
        /// </summary>
        private static List<TSM.Reinforcement> Held(TSM.RebarSet set)
        {
            var held = new List<TSM.Reinforcement>();
            TSM.ModelObjectEnumerator reinforcements = set.GetReinforcements();

            if (reinforcements == null)
            {
                return held;
            }

            while (reinforcements.MoveNext())
            {
                if (reinforcements.Current is TSM.Reinforcement reinforcement)
                {
                    held.Add(reinforcement);
                }
            }

            return held;
        }

        /// <summary>
        /// Reads the geometries Tekla worked out for one reinforcement, passing over any that is not a bar.
        /// </summary>
        /// <remarks>
        /// This does no work-plane handling of its own, so that a whole batch can be read inside one swap.
        /// </remarks>
        private static GeoPolylineArc3[] Read(TSM.Reinforcement reinforcement, Tolerance tolerance)
        {
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
                GeometryHelperLog.Debug($"Passed over {skipped} rebar geometry(ies) of {reinforcement.Identifier}.");
            }

            return bars.ToArray();
        }

        /// <summary>
        /// Holds the model in the global work plane for as long as it lives, where that is needed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Tekla Structures 2020 hands back the geometry of a lapped bar in the wrong place unless the current
        /// work plane is the global one, so on that build alone the plane is turned to global first and put
        /// back afterwards. Later builds do not need it, and there the whole thing compiles away to nothing.
        /// </para>
        /// <para>
        /// The plane is model-wide state, so putting it back matters more than setting it: the restore happens
        /// in <see cref="Dispose"/>, which runs whether the read finished or threw.
        /// </para>
        /// </remarks>
        private sealed class GlobalWorkPlane : IDisposable
        {
            private readonly TSM.WorkPlaneHandler _handler;
            private readonly TSM.TransformationPlane _previous;

            internal GlobalWorkPlane()
            {
#if TeklaVersion2020
                var model = new TSM.Model();

                if (!model.GetConnectionStatus())
                {
                    throw new InvalidOperationException("Tekla Structures is not running, or this process cannot connect to it.");
                }

                _handler = model.GetWorkPlaneHandler();
                _previous = _handler.GetCurrentTransformationPlane();

                _handler.SetCurrentTransformationPlane(new TSM.TransformationPlane());
#else
                _handler = null;
                _previous = null;
#endif
            }

            /// <summary>
            /// Puts the work plane back where it was.
            /// </summary>
            public void Dispose()
            {
                if (_handler != null && _previous != null)
                {
                    _handler.SetCurrentTransformationPlane(_previous);
                }
            }
        }
    }
}
