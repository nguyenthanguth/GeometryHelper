using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Cutting a curved chain or loop by a box, or by several cutters at once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A chain could be cut by one body and a <see cref="GeoPolyline3"/> could be cut by a box or by a whole
    /// array of them, which is what a bar is really asked: not "does this one opening cut you" but "what is
    /// left of you once every opening on the drawing has had its say".
    /// </para>
    /// <para>
    /// Nothing new is worked out. Every cutter is asked where it crosses the chain, all the places are cut at
    /// once, and each piece is then filed by where its <b>middle</b> falls — never by an end, because every end
    /// is on a surface by construction and a surface belongs to neither side. A piece is inside when it is
    /// inside <b>any</b> cutter, so overlapping cutters behave as the one region they cover.
    /// </para>
    /// <para>
    /// A null cutter inside an array is passed over rather than throwing, as the straight version does: an array
    /// gathered from a model has gaps in it, and the caller is asking what cuts, not what exists.
    /// </para>
    /// </remarks>
    internal static partial class ArcChain3
    {

        /// <summary>
        /// Cuts a chain where it crosses a box, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoObb3 cutter, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(chain, cutter, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a chain where it crosses a box, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="cutter">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the chain.</param>
        /// <param name="outside">The pieces outside it, in order along the chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A box is asked as itself and not as the body it bounds, which is the same answer by a shorter road.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolylineArc3 chain,
            GeoObb3 cutter,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            var cuts = new List<GeoPoint3>(GetIntersections(chain, cutter, tolerance));

            return Sorted(AsChains(CutRuns(chain.GetEdges(), cuts, tolerance)), point => cutter.Contains(point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Cuts a chain where it crosses a box, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoAabb3 cutter, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(chain, cutter, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a chain where it crosses a box, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="cutter">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the chain.</param>
        /// <param name="outside">The pieces outside it, in order along the chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A box is asked as itself and not as the body it bounds, which is the same answer by a shorter road.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolylineArc3 chain,
            GeoAabb3 cutter,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            var cuts = new List<GeoPoint3>(GetIntersections(chain, cutter, tolerance));

            return Sorted(AsChains(CutRuns(chain.GetEdges(), cuts, tolerance)), point => cutter.Contains(point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Cuts a chain where it crosses several bodies, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoSolid3[] cutters, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(chain, cutters, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a chain where it crosses several bodies, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="cutters">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the chain.</param>
        /// <param name="outside">The pieces outside it, in order along the chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A piece is inside when it is inside any one of them, so overlapping cutters behave as the one region they cover, and a null in the array is passed over.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolylineArc3 chain,
            GeoSolid3[] cutters,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            var cuts = new List<GeoPoint3>(CrossingsOfAll(chain.GetEdges(), cutters, one => one == null, (edge, one) => edge.GetIntersections(one, tolerance), tolerance));

            return Sorted(AsChains(CutRuns(chain.GetEdges(), cuts, tolerance)), point => InsideAny(cutters, point, one => one == null, (body, at) => Containment3.Contains(body, at, tolerance)), out inside, out outside);
        }

        /// <summary>
        /// Cuts a chain where it crosses several boxes, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoObb3[] cutters, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(chain, cutters, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a chain where it crosses several boxes, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="cutters">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the chain.</param>
        /// <param name="outside">The pieces outside it, in order along the chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A piece is inside when it is inside any one of them, so overlapping cutters behave as the one region they cover, and a null in the array is passed over.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolylineArc3 chain,
            GeoObb3[] cutters,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            var cuts = new List<GeoPoint3>(CrossingsOfAll(chain.GetEdges(), cutters, one => one == null, (edge, one) => edge.GetIntersections(one, tolerance), tolerance));

            return Sorted(AsChains(CutRuns(chain.GetEdges(), cuts, tolerance)), point => InsideAny(cutters, point, one => one == null, (box, at) => box.Contains(at, tolerance)), out inside, out outside);
        }

        /// <summary>
        /// Cuts a chain where it crosses several boxes, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolylineArc3 chain, GeoAabb3[] cutters, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(chain, cutters, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a chain where it crosses several boxes, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="cutters">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the chain.</param>
        /// <param name="outside">The pieces outside it, in order along the chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the chain came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A piece is inside when it is inside any one of them, so overlapping cutters behave as the one region they cover, and a null in the array is passed over.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolylineArc3 chain,
            GeoAabb3[] cutters,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            var cuts = new List<GeoPoint3>(CrossingsOfAll(chain.GetEdges(), cutters, one => one.IsEmpty, (edge, one) => edge.GetIntersections(one, tolerance), tolerance));

            return Sorted(AsChains(CutRuns(chain.GetEdges(), cuts, tolerance)), point => InsideAny(cutters, point, one => one.IsEmpty, (box, at) => box.Contains(at, tolerance)), out inside, out outside);
        }

        /// <summary>
        /// Cuts a loop where it crosses a box, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoObb3 cutter, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(loop, cutter, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a loop where it crosses a box, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="cutter">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the loop.</param>
        /// <param name="outside">The pieces outside it, in order along the loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the loop came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A box is asked as itself and not as the body it bounds, which is the same answer by a shorter road.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolygonArc3 loop,
            GeoObb3 cutter,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            var cuts = new List<GeoPoint3>(GetIntersections(loop, cutter, tolerance));

            return Sorted(CutLoop(loop, cuts, tolerance), point => cutter.Contains(point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Cuts a loop where it crosses a box, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoAabb3 cutter, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(loop, cutter, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a loop where it crosses a box, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="cutter">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the loop.</param>
        /// <param name="outside">The pieces outside it, in order along the loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the loop came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A box is asked as itself and not as the body it bounds, which is the same answer by a shorter road.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolygonArc3 loop,
            GeoAabb3 cutter,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (cutter == null)
            {
                throw new ArgumentNullException(nameof(cutter));
            }

            var cuts = new List<GeoPoint3>(GetIntersections(loop, cutter, tolerance));

            return Sorted(CutLoop(loop, cuts, tolerance), point => cutter.Contains(point, tolerance), out inside, out outside);
        }

        /// <summary>
        /// Cuts a loop where it crosses several bodies, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoSolid3[] cutters, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(loop, cutters, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a loop where it crosses several bodies, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="cutters">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the loop.</param>
        /// <param name="outside">The pieces outside it, in order along the loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the loop came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A piece is inside when it is inside any one of them, so overlapping cutters behave as the one region they cover, and a null in the array is passed over.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolygonArc3 loop,
            GeoSolid3[] cutters,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            var cuts = new List<GeoPoint3>(CrossingsOfAll(loop.GetEdges(), cutters, one => one == null, (edge, one) => edge.GetIntersections(one, tolerance), tolerance));

            return Sorted(CutLoop(loop, cuts, tolerance), point => InsideAny(cutters, point, one => one == null, (body, at) => Containment3.Contains(body, at, tolerance)), out inside, out outside);
        }

        /// <summary>
        /// Cuts a loop where it crosses several boxes, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoObb3[] cutters, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(loop, cutters, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a loop where it crosses several boxes, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="cutters">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the loop.</param>
        /// <param name="outside">The pieces outside it, in order along the loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the loop came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A piece is inside when it is inside any one of them, so overlapping cutters behave as the one region they cover, and a null in the array is passed over.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolygonArc3 loop,
            GeoObb3[] cutters,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            var cuts = new List<GeoPoint3>(CrossingsOfAll(loop.GetEdges(), cutters, one => one == null, (edge, one) => edge.GetIntersections(one, tolerance), tolerance));

            return Sorted(CutLoop(loop, cuts, tolerance), point => InsideAny(cutters, point, one => one == null, (box, at) => box.Contains(at, tolerance)), out inside, out outside);
        }

        /// <summary>
        /// Cuts a loop where it crosses several boxes, telling what is in from what is out.
        /// </summary>
        public static bool TrySplitBy(GeoPolygonArc3 loop, GeoAabb3[] cutters, out GeoPolylineArc3[] inside, out GeoPolylineArc3[] outside)
            => TrySplitBy(loop, cutters, out inside, out outside, Tolerance.Global);

        /// <summary>
        /// Cuts a loop where it crosses several boxes, telling what is in from what is out, within a tolerance.
        /// </summary>
        /// <param name="loop">The loop.</param>
        /// <param name="cutters">What to cut against.</param>
        /// <param name="inside">The pieces within the material, in order along the loop.</param>
        /// <param name="outside">The pieces outside it, in order along the loop.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the loop came apart; otherwise, false, and it lands whole in one list or the other.</returns>
        /// <remarks>
        /// Which side a piece is on is settled at the middle of that piece and never at an end, because every end
        /// is on a surface by construction and the surface belongs to neither side. A piece is inside when it is inside any one of them, so overlapping cutters behave as the one region they cover, and a null in the array is passed over.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop or what it is cut against is null.</exception>
        public static bool TrySplitBy(
            GeoPolygonArc3 loop,
            GeoAabb3[] cutters,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside,
            Tolerance tolerance)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            if (cutters == null)
            {
                throw new ArgumentNullException(nameof(cutters));
            }

            var cuts = new List<GeoPoint3>(CrossingsOfAll(loop.GetEdges(), cutters, one => one.IsEmpty, (edge, one) => edge.GetIntersections(one, tolerance), tolerance));

            return Sorted(CutLoop(loop, cuts, tolerance), point => InsideAny(cutters, point, one => one.IsEmpty, (box, at) => box.Contains(at, tolerance)), out inside, out outside);
        }

        /// <summary>
        /// Cuts a chain at a set of distances measured along it.
        /// </summary>
        public static bool SplitAtDistances(GeoPolylineArc3 chain, IEnumerable<double> distances, out GeoPolylineArc3[] pieces)
            => SplitAtDistances(chain, distances, out pieces, Tolerance.Global);

        /// <summary>
        /// Cuts a chain at a set of distances measured along it, within a tolerance.
        /// </summary>
        /// <param name="chain">The chain.</param>
        /// <param name="distances">How far along to cut, measured along the arcs; anything off the chain is passed over.</param>
        /// <param name="pieces">The pieces, in order along the chain.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when any cut was made; otherwise, false, and the chain comes back whole.</returns>
        /// <remarks>
        /// A loop could be cut at several distances at once and a chain could only be cut at one, though cutting
        /// a bar into a schedule of lengths is the usual way round.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain or the list of distances is null.</exception>
        public static bool SplitAtDistances(GeoPolylineArc3 chain, IEnumerable<double> distances, out GeoPolylineArc3[] pieces, Tolerance tolerance)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (distances == null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            var at = new List<GeoPoint3>();
            double length = chain.Length;

            foreach (double distance in distances)
            {
                if (!double.IsNaN(distance) && distance > 0.0 && distance < length)
                {
                    at.Add(chain.GetPointAtDistance(distance));
                }
            }

            return TryCut(chain, at, tolerance, out pieces);
        }

        /// <summary>
        /// Files each piece by where its middle falls, joining neighbours that landed on the same side.
        /// </summary>
        /// <remarks>
        /// The joining is the point. Several cutters name several surfaces, and two of them can sit inside the
        /// one region they cover between them — the face where two overlapping openings meet is a surface, and
        /// the chain is cut there, but it never leaves the material. Handing those back separately would report
        /// three pieces of bar inside one opening. So a run of neighbours on the same side becomes one piece, and
        /// what the method returns is whether there was more than one <i>run</i>, which is the same rule
        /// <c>Splition3</c> keeps for a straight chain.
        /// </remarks>
        private static bool Sorted(
            GeoPolylineArc3[] pieces,
            Func<GeoPoint3, bool> isInside,
            out GeoPolylineArc3[] inside,
            out GeoPolylineArc3[] outside)
        {
            var within = new List<GeoPolylineArc3>();
            var without = new List<GeoPolylineArc3>();
            int runs = 0;
            int index = 0;

            while (index < pieces.Length)
            {
                bool state = isInside(Middle(pieces[index]));
                var edges = new List<GeoEdge3>();

                while (index < pieces.Length && isInside(Middle(pieces[index])) == state)
                {
                    edges.AddRange(pieces[index].GetEdges());
                    index++;
                }

                (state ? within : without).Add(new GeoPolylineArc3(edges));
                runs++;
            }

            inside = within.ToArray();
            outside = without.ToArray();

            return runs > 1;
        }

        /// <summary>
        /// The point halfway along a piece, which speaks for the whole of it because the piece was cut at every
        /// surface it crosses.
        /// </summary>
        private static GeoPoint3 Middle(GeoPolylineArc3 piece) => piece.GetPointAtDistance(piece.Length / 2.0);

        /// <summary>
        /// Gathers where every cutter of an array crosses a run of edges, naming each place once.
        /// </summary>
        private static GeoPoint3[] CrossingsOfAll<T>(
            IEnumerable<GeoEdge3> edges,
            T[] cutters,
            Func<T, bool> skip,
            Func<GeoEdge3, T, GeoPoint3[]> of,
            Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoEdge3 edge in edges)
            {
                foreach (T cutter in cutters)
                {
                    if (skip(cutter))
                    {
                        continue;
                    }

                    foreach (GeoPoint3 crossing in of(edge, cutter))
                    {
                        Arc3.AddOnce(found, crossing, tolerance);
                    }
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Determines whether a point is inside any cutter of an array, passing over the gaps in it.
        /// </summary>
        /// <remarks>
        /// What counts as a gap is the caller's to say, because the cutters are not all reference types: a null
        /// body and an empty box both cut nothing, and only the caller knows which it is holding.
        /// </remarks>
        private static bool InsideAny<T>(T[] cutters, GeoPoint3 point, Func<T, bool> skip, Func<T, GeoPoint3, bool> holds)
        {
            foreach (T cutter in cutters)
            {
                if (!skip(cutter) && holds(cutter, point))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
