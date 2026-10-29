using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Combines solids: the union of two bodies, the part they share, and one taken out of the other.
    /// <para>
    /// The method is the same one <c>Splition3</c> uses to cut a plate against a body, carried up a
    /// dimension. One body is divided by the planes of the other's faces that come near it, which leaves
    /// cells that are each wholly inside or wholly outside the other body, since the surface of a body never
    /// leaves the planes of its own faces. The cells wanted for the operation are then glued back together:
    /// a face shared by two kept cells appears twice, once each way round, and dropping both leaves exactly
    /// the outer skin.
    /// </para>
    /// <para>
    /// Dividing A by the planes of B already lays a face along every part of the surface of B that runs
    /// through A. That is why an intersection is just the cells of A that fall inside B, and a difference
    /// just the cells of A that fall outside it: the walls of the cavity are already there, and adding the
    /// faces of B on top of them would describe the same surface twice.
    /// </para>
    /// <para>
    /// So every operation cuts one body only, the one fewer planes cut, and never by its own faces: they bound
    /// it already, and cutting by them is what took a bent bar apart into thousands of cells, since every
    /// plane of a bend runs on through the rest of the bar. A union keeps the part of the cut body beyond the
    /// other and the other whole; a difference cutting the body taken away keeps the body taken from whole,
    /// and the part of the other within it turned inside out. Where the whole body and the cells meet, their
    /// faces lie back to back in one plane, cut differently, and cancel by the area they share. Where a plane
    /// crosses a cell and leaves it whole, the other body is cut instead, and failing that both are cut by
    /// every plane of both, as they once always were.
    /// </para>
    /// <para>
    /// Every operation reports <c>false</c> when the answer is nothing at all — two bodies that do not
    /// touch have no shared part, and a body wholly swallowed by another leaves nothing behind. That is
    /// an outcome rather than a failure, which is why it comes back as <c>false</c> with no result rather
    /// than as an exception or an empty body.
    /// </para>
    /// <para>
    /// An opening on either body is honoured. A union or a difference cuts the openings into both bodies
    /// first (<see cref="TryCutOpenings(GeoSolid3, out GeoSolid3, Tolerance)"/>), so their faces are where
    /// their material ends; an intersection adds the planes of its own openings near the other to its knives,
    /// so no cell straddles a wall, and drops the cells filling them as not being material. Either way the
    /// result carries the cavity as real geometry rather than as an opening of its own. Two bodies too far
    /// apart to meet are the exception — nothing is cut there, and each keeps the openings it came with.
    /// </para>
    /// <para>
    /// A result of two closed bodies is closed as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, or not made: one
    /// whose corners close it, two copies of an edge about the tolerance apart taken for one, which
    /// <c>Validate</c> reads as open, is welded, or worked out another way, and where it stays open the operation reports
    /// <c>false</c>, logged, as one it could not work out.
    /// </para>
    /// <para>
    /// A void, a chip or a slab no thicker than the point tolerance is within the tolerance of nothing and changes
    /// nothing; one thicker than the tolerance and a trillionth of the size of the coordinates of the two bodies is cut
    /// however thin: a cube of 100 less a box 40 by 30 and 0.0005 thick inside it is the cube, and less one 0.0015 thick
    /// holds 999 998.2. One exactly the tolerance thick, on a turned body that thick to a few bits either way, is read as
    /// no thicker, the same way by the common part and both differences; a union of the two can hold anything from the two
    /// less it to the two.
    /// </para>
    /// <para>
    /// A union of two bodies lying face to face within the tolerance of each other and apart, welded closed, is taken where
    /// it holds the two and the gap filled: two cubes of 100 side by side 0.01 apart, turned, joined within a hundredth, hold
    /// 2 000 000. What two bodies exactly the tolerance apart join into still depends on where they lie: turned another way
    /// the two cubes hold 1 999 933.3, part of the gap.
    /// </para>
    /// </summary>
    public static partial class Boolean3
    {
        /// <summary>
        /// Joins two solids into one, using the default tolerance.
        /// </summary>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result)
        {
            return TryUnion(first, second, out result, Tolerance.Global);
        }

        /// <summary>
        /// Joins two solids into one, within a tolerance.
        /// </summary>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="result">The combined body.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the two could not be combined into a closed body, which is logged.</returns>
        /// <remarks>
        /// Two bodies that do not touch still combine: the result is one solid carrying both shells, which
        /// measures and answers containment correctly because each shell is closed and wound outwards.
        /// </remarks>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
            => TryUnion(first, second, out result, tolerance, out _);

        /// <summary>
        /// Joins two solids into one, saying how it came out, using the default tolerance.
        /// </summary>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, out BooleanOutcome outcome)
            => TryUnion(first, second, out result, Tolerance.Global, out outcome);

        /// <summary>
        /// Joins two solids into one, within a tolerance, saying how it came out.
        /// </summary>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="result">The combined body.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="outcome">
        /// <see cref="BooleanOutcome.Made"/> with the result, or <see cref="BooleanOutcome.NotWorkedOut"/>: two bodies
        /// always leave a union.
        /// </param>
        /// <returns>false when the two could not be combined into a closed body, which is logged.</returns>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out BooleanOutcome outcome)
            => Union(first, second, out result, tolerance, out outcome, out _);

        /// <summary>
        /// Works out the union as <see cref="TryUnion(GeoSolid3, GeoSolid3, out GeoSolid3, Tolerance, out BooleanOutcome)"/> does, and
        /// hands back what came out open where two closed bodies gave nothing closed, for a closing to try.
        /// </summary>
        private static bool Union(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out BooleanOutcome outcome, out GeoSolid3 open)
        {
            Guard(first, second);
            open = null;

            try
            {
                if (Unite(first, second, out result, tolerance, out string unworkable, out open))
                {
                    // Wound against itself where the two are wound alike, the union is worked out again with every cell no
                    // thicker than the tolerance lying within the other body against its face left out, and taken so where that
                    // closes wound alike and holds a volume a union of the two can; see FacesOfCells and HoldsTheTwo. A beam whose
                    // end ran exactly a thousandth into a column held the slab of its end within the column, the column whole
                    // beside it, 100.96 cubic millimetres over their exact union and 66.35 over the two. Where it does not,
                    // the union as it came is kept only where it holds such a volume itself, as before such a result was
                    // turned down; see CombineCuttingOne. Otherwise it is not worked out: a turned cube of 100 and a block
                    // 40 by 40 by 50 running exactly 0.001 into its top at the corner came out so, 1 080 003.21, more than the
                    // two, 1 080 001.6.
                    if (!Shells3.ClosesAlike(result.Faces, tolerance) && Shells3.ClosesAlike(first.Faces, tolerance) && Shells3.ClosesAlike(second.Faces, tolerance))
                    {
                        if (Unite(first, second, out GeoSolid3 flush, tolerance, out _, out _, null, true)
                            && Shells3.Closes(flush, tolerance) && Shells3.ClosesAlike(flush.Faces, tolerance) && HoldsTheTwo(flush, first, second, tolerance, true))
                        {
                            result = flush;
                        }
                        else if (!HoldsTheTwo(result, first, second, tolerance, false))
                        {
                            GeometryHelperLog.Warn("A solid union could not be worked out: it came out wound against itself, holding a volume no union of the two can; it is reported as not made.");
                            result = null;
                            outcome = BooleanOutcome.NotWorkedOut;
                            return false;
                        }
                    }

                    outcome = BooleanOutcome.Made;
                    return true;
                }

                // Two bodies always leave a union, so none is a union not worked out.
                GeometryHelperLog.Warn($"A solid union could not be worked out: {unworkable ?? "it glued into no body"}; it is reported as not made.");
                outcome = BooleanOutcome.NotWorkedOut;
                return false;
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                outcome = BooleanOutcome.NotWorkedOut;
                return Unworkable("union", exception, out result);
            }
        }

        /// <summary>
        /// Whether a union, worked out again or wound against itself, holds a volume a union of the two can: no less than the
        /// larger, no more than both, and the two less their common part within the tolerance times the area of that part's skin, as
        /// <see cref="AddsUp"/> reads it, or, sharing nothing at the tolerance, the two less no more than the tolerance times
        /// the area where they lie face to face, where it was worked out again; and a hair for the rounding.
        /// </summary>
        /// <param name="union">The union.</param>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="again">
        /// Whether the union was worked out again with the cells no thicker than the tolerance left out, where sharing nothing
        /// it can hold the two less such a slab; one as it came, wound against itself, holds the two then.
        /// </param>
        /// <remarks>
        /// Taken unchecked, the union of a turned cube of 100 and a block 40 by 40 by 50 running 0.001 into its top at the
        /// corner held 1 080 002.0, more than the two, 1 080 001.6, where their union holds 1 080 001.2: a cell standing beside
        /// the block's slab along the cube's edge, not on its face, was left out with it.
        /// </remarks>
        private static bool HoldsTheTwo(GeoSolid3 union, GeoSolid3 first, GeoSolid3 second, Tolerance tolerance, bool again)
        {
            double v = VolumeOf(union, tolerance);
            double v1 = VolumeOf(first, tolerance);
            double v2 = VolumeOf(second, tolerance);
            double rounding = 1E-9 * (Math.Abs(v1) + Math.Abs(v2));

            if (v < Math.Max(v1, v2) - rounding || v > v1 + v2 + rounding)
            {
                return false;
            }

            var checking = new OpenPath { Checking = true };

            if (!Share(first, second, out GeoSolid3 common, tolerance, out string why, out _, checking, true))
            {
                // Nothing in common at the tolerance: an overlap exactly the tolerance deep reads so, and the union holds anything
                // from the two less that slab to the two, the slab no more than the tolerance times where the two lie face to
                // face. Two cubes of 100 overlapping by a hundredth, turned, joined within a hundredth into 1 999 950.
                if (why != null || !again)
                {
                    return why == null && Math.Abs(v - (v1 + v2)) <= rounding;
                }

                // A slab exactly the tolerance thick is that thick to a few bits either way; see Bridged.
                double contact = 0.0;
                Tolerance flush = Splition3.OnPlaneWithinRounding(first, Splition3.OnPlaneWithinRounding(second, tolerance));

                if (TryGetContact(first, second, out GeoFace3[] patches, flush))
                {
                    foreach (GeoFace3 patch in patches)
                    {
                        contact += patch.Area;
                    }
                }

                return v >= v1 + v2 - tolerance.EqualPoint * contact - rounding;
            }

            return Math.Abs(v - (v1 + v2 - VolumeOf(common, tolerance))) <= tolerance.EqualPoint * common.GrossSurfaceArea + rounding;
        }

        private static bool Unite(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out string unworkable)
            => Unite(first, second, out result, tolerance, out unworkable, out _);

        private static bool Unite(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out string unworkable, out GeoSolid3 open, OpenPath path = null, bool flush = false)
        {
            result = null;
            unworkable = null;
            open = null;
            path = path ?? new OpenPath();

            if (!first.GetAabb().CollidesWith(second.GetAabb(), tolerance))
            {
                // Nothing to resolve: the two shells simply sit side by side. Neither body reaches the
                // other, so whatever each has carved out of itself is still carved out of the pair. Each is
                // wound outwards, or one wound inwards would take its volume off the other's.
                first = first.TurnOutwards();
                second = second.TurnOutwards();

                List<GeoFace3> apart = new List<GeoFace3>(first.Faces);
                apart.AddRange(second.Faces);

                List<GeoSolid3> carved = new List<GeoSolid3>(first.Openings);
                carved.AddRange(second.Openings);

                result = new GeoSolid3(apart, carved);
                return true;
            }

            tolerance = ForWork(tolerance);
            first = FlatForWork(first, tolerance);
            second = FlatForWork(second, tolerance);

            // One body cut, beyond the other, and the other whole.
            GeoSolid3 openCut = null;
            int fewest = 0;
            bool sealedOnce = false;

            if (TryCutOpeningsAtOnce(first, out GeoSolid3 a, tolerance) && TryCutOpeningsAtOnce(second, out GeoSolid3 b, tolerance))
            {
                bool? found = CombineCuttingOne(a, b, true, tolerance, out result, out _, out fewest, flush);

                // A union one body cut gave of a volume no union can have is worked out by cutting both instead.
                if (result != null && Implausible("union", result, a, b, tolerance) is string why)
                {
                    GeometryHelperLog.Debug($"Boolean3: cutting one body {why}; both are cut by every plane of both instead.");
                    found = null;
                    result = null;
                }

                if (found.HasValue)
                {
                    result = Weld3.Sealed(result, tolerance);

                    if (!found.Value || ClosesOrSeals("union", ref result, a, b, tolerance, path))
                    {
                        return found.Value && AddsUp("union", ref result, a, b, tolerance, path, out unworkable, ref open);
                    }

                    GeometryHelperLog.Debug("Boolean3: cutting one body, the union came out open to Validate; both are cut by every plane of both instead.");
                    open = result;
                    sealedOnce = true;
                }

                openCut = result;
            }

            List<GeoPlane3> planes = SharedPlanes(first, second, tolerance);

            // Sealed once already where it came out open after sealing, it is not sealed again.
            if (!sealedOnce && IsHeavy(planes, fewest, openCut) && TakesOpenCut("union", openCut, first, second, tolerance, path, out result))
            {
                return true;
            }

            List<GeoFace3> kept = new List<GeoFace3>();
            var owners = new List<int>();

            // All of the first body, and only the part of the second that reaches beyond it: the shared
            // region belongs to the union once, and it is already carried by the first.
            var firstStuck = new List<GeoPlane3>();
            List<GeoSolid3> firstCells = SplitIntoCells(first, planes, tolerance, out bool firstClean, firstStuck);
            kept.AddRange(FacesOfCells(firstCells, first, tolerance, owners));

            var stuck = new List<GeoPlane3>();
            List<GeoSolid3> cells = SplitIntoCells(second, planes, tolerance, out bool clean, stuck);
            bool misjudged = !clean && Misjudged(cells, stuck, second, first, false, tolerance);
            kept.AddRange(FacesOfCells(cells, second, first, false, tolerance, owners, firstCells.Count, flush));

            if (!GlueOrKeep(kept, owners, openCut, misjudged, tolerance, out result))
            {
                return false;
            }

            // A cell of the second judged apart across a plane, with no open result to check it by, is checked by the union
            // cut the other way round, the second whole and the cells of the first beyond it: a cracked U and a cracked box
            // standing over its notch, the box's cells judged so by the U's planes, counted the 15.31 the two share twice.
            // Judged so that way too, both ways can count the same part twice, and the union is no answer: a box dipping
            // 0.088 into an arm of a cracked U, the U's cells judged so by the box's planes and the box's by the U's, held
            // 3 145.93 joined to the U either way where the two hold 3 143.38.
            if (misjudged && openCut == null)
            {
                if (!firstClean && Misjudged(firstCells, firstStuck, first, second, false, tolerance))
                {
                    unworkable = "a cell a plane could not cut is judged apart across it whichever body's cells are kept";
                    result = null;
                    return false;
                }

                var otherOwners = new List<int>();
                List<GeoFace3> otherWay = FacesOfCells(cells, second, tolerance, otherOwners);
                otherWay.AddRange(FacesOfCells(firstCells, first, second, false, tolerance, otherOwners, cells.Count, flush));

                if (!TryGlue(otherWay, otherOwners, tolerance, out GeoSolid3 other)
                    || Math.Abs(result.GrossVolume - other.GrossVolume) > tolerance.EqualPoint * result.GrossSurfaceArea)
                {
                    unworkable = "a cell a plane could not cut is judged apart across it, and the union cut the other way round holds another volume";
                    result = null;
                    return false;
                }
            }

            unworkable = Implausible("union", result, first, second, tolerance);

            if (unworkable != null)
            {
                result = null;
                return false;
            }

            result = Weld3.Sealed(result, tolerance);

            if (!ClosesOrSeals("union", ref result, first, second, tolerance, path))
            {
                unworkable = OpenWhy;
                open = open ?? result;
                result = null;
                return false;
            }

            return AddsUp("union", ref result, first, second, tolerance, path, out unworkable, ref open);
        }

        /// <summary>
        /// Gets the part two solids have in common, using the default tolerance.
        /// </summary>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result)
        {
            return TryIntersect(first, second, out result, Tolerance.Global);
        }

        /// <summary>
        /// Gets the part two solids have in common, within a tolerance.
        /// </summary>
        /// <returns>
        /// false when the two bodies share no volume, or when it cannot be worked out, which is logged: of two closed bodies,
        /// a common part closed as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, or none.
        /// </returns>
        /// <remarks>
        /// Only one body is cut, and only by the planes of the other that come near it and of its own openings:
        /// those are what leave every cell wholly inside or wholly outside the other, and wholly material or wholly
        /// carved out. Its own outer faces bound it already and need not cut it. On a bent bar they would: every
        /// plane of a bend runs on through the rest of the bar, and a bar with a hook came out in thousands of
        /// cells, twenty seconds against a plate. The body cut is the one fewer planes cut, so a bar crossing a
        /// beam is cut by a handful of the beam's planes rather than the beam by the hundreds of the bar's.
        /// </remarks>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
            => TryIntersect(first, second, out result, tolerance, out _);

        /// <summary>
        /// Gets the part two solids have in common, saying how it came out, using the default tolerance.
        /// </summary>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, out BooleanOutcome outcome)
            => TryIntersect(first, second, out result, Tolerance.Global, out outcome);

        /// <summary>
        /// Gets the part two solids have in common, within a tolerance, saying how it came out.
        /// </summary>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="result">The part they share.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="outcome">
        /// <see cref="BooleanOutcome.Made"/> with the result, <see cref="BooleanOutcome.Empty"/> where the two share no
        /// volume, or no more than a slab the tolerance thick, or <see cref="BooleanOutcome.NotWorkedOut"/> where it could not
        /// be worked out, which is logged.
        /// </param>
        /// <returns>
        /// false when the two bodies share no volume, or share no more than a slab the tolerance thick as
        /// <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, or when it cannot be worked out.
        /// </returns>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out BooleanOutcome outcome)
            => Intersect(first, second, out result, tolerance, out outcome, out _);

        /// <summary>
        /// Works out the intersection as <see cref="TryIntersect(GeoSolid3, GeoSolid3, out GeoSolid3, Tolerance, out BooleanOutcome)"/> does, and
        /// hands back what came out open where two closed bodies gave nothing closed, for a closing to try.
        /// </summary>
        private static bool Intersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out BooleanOutcome outcome, out GeoSolid3 open)
        {
            Guard(first, second);
            open = null;

            try
            {
                if (Share(first, second, out result, tolerance, out string unworkable, out open))
                {
                    if (IsNothing("intersection", ref result, first, second, tolerance))
                    {
                        outcome = BooleanOutcome.Empty;
                        return false;
                    }

                    outcome = BooleanOutcome.Made;
                    return true;
                }

                if (unworkable != null)
                {
                    GeometryHelperLog.Warn($"A solid intersection could not be worked out: {unworkable}; it is reported as not made.");
                    outcome = BooleanOutcome.NotWorkedOut;
                    return false;
                }

                outcome = BooleanOutcome.Empty;
                return false;
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                outcome = BooleanOutcome.NotWorkedOut;
                return Unworkable("intersection", exception, out result);
            }
        }

        private static bool Share(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out string unworkable)
            => Share(first, second, out result, tolerance, out unworkable, out _);

        private static bool Share(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance, out string unworkable, out GeoSolid3 open, OpenPath path = null, bool flush = false)
        {
            result = null;
            unworkable = null;
            open = null;
            path = path ?? new OpenPath();

            if (!first.GetAabb().CollidesWith(second.GetAabb(), tolerance))
            {
                return false;
            }

            tolerance = ForWork(tolerance);
            first = FlatForWork(first, tolerance);
            second = FlatForWork(second, tolerance);

            List<GeoPlane3> cuttingFirst = KnivesFor(first, second, tolerance);
            List<GeoPlane3> cuttingSecond = KnivesFor(second, first, tolerance);
            bool firstIsCut = cuttingFirst.Count <= cuttingSecond.Count;

            GeoSolid3 cut = firstIsCut ? first : second;
            GeoSolid3 other = firstIsCut ? second : first;
            List<GeoPlane3> knives = firstIsCut ? cuttingFirst : cuttingSecond;
            List<GeoPlane3> otherKnives = firstIsCut ? cuttingSecond : cuttingFirst;

            List<GeoFace3> kept = CellsInside(cut, other, knives, tolerance, out bool clean, out bool misjudged, out List<int> owners, flush);
            bool otherWayAffordable = otherKnives.Count <= 2 * knives.Count + 16;

            // A plane can still cross a cell and leave it whole where the cut does not close — a body running
            // through itself, say — and the cell is then judged by one point for both sides of the plane. Cut the
            // other way, the trouble falls elsewhere, so the other body is cut before that is settled for, when that
            // costs about the same: a bar's hundreds of planes would cut a beam into thousands of cells again.
            // Slivers were the common cause, and the cut keeps those now; see LoopAssembly.ForPieces. Where the one
            // point misjudged part of the cell, the other body is cut whatever it costs; see Misjudged. Cut cleanly
            // neither way, the other way is taken where its cells are judged alike on both sides of every plane that could
            // not cut them and this way's are not, as a difference takes it. Of two bars bent twice and crossing, cut within
            // a thousandth, the second was cut first, by the planes of the first, which crossed 33 of its cells and could
            // not cut them, and the common part lost an eighth; the first cut by the planes of the second left 7 cells so,
            // each judged alike, and the common part came out as it is.
            if (!clean && (otherWayAffordable || misjudged))
            {
                List<GeoFace3> otherWay = CellsInside(other, cut, otherKnives, tolerance, out bool otherClean, out bool otherMisjudged, out List<int> otherOwners, flush);

                if (otherClean || (misjudged && !otherMisjudged))
                {
                    kept = otherWay;
                    owners = otherOwners;
                }
                else if (misjudged && otherMisjudged)
                {
                    // Judged apart across a plane whichever body is cut, what is kept is no answer: a cracked U and a box
                    // dipping into its notch share 26.95, and the box cut by the U's planes kept 2.02.
                    unworkable = "a cell a plane could not cut is judged apart across it whichever body is cut";
                    return false;
                }

                misjudged = !otherClean && (misjudged || otherMisjudged);
            }

            if (!TryGlue(kept, owners, tolerance, out result))
            {
                // Nothing glued where a cell was judged apart across a plane, whichever way was kept, is no answer, not nothing
                // in common: a cracked U and a box standing in its notch, each misjudged cut by the other's planes, share 38.14
                // and gave nothing; another, the box's cut misjudged and the U's taken instead, shared 26.95 and the U's cut
                // kept nothing.
                if (misjudged)
                {
                    unworkable = "a cell a plane could not cut is judged apart across it, and what is kept glues into nothing";
                }

                return false;
            }

            // Cut cleanly and still open, two closed bodies can close cut the other way round, as a difference can;
            // see CombineCuttingOne.
            if (clean && otherWayAffordable && !result.ClosesByCorners(tolerance) && first.ClosesByCorners(tolerance) && second.ClosesByCorners(tolerance)
                && TryGlue(CellsInside(other, cut, otherKnives, tolerance, out bool closedClean, out _, out List<int> closedOwners, flush), closedOwners, tolerance, out GeoSolid3 otherResult)
                && closedClean && otherResult.ClosesByCorners(tolerance))
            {
                result = otherResult;
            }

            // A common part of a volume no intersection can have is worked out the other way round, whatever it costs.
            if (Implausible("intersection", result, first, second, tolerance) is string why)
            {
                GeometryHelperLog.Debug($"Boolean3: cutting one body {why}; the other is cut instead.");

                if (TryGlue(CellsInside(other, cut, otherKnives, tolerance, out _, out _, out List<int> otherWayOwners, flush), otherWayOwners, tolerance, out GeoSolid3 otherWay)
                    && Implausible("intersection", otherWay, first, second, tolerance) == null)
                {
                    result = Weld3.Sealed(otherWay, tolerance);

                    if (ClosesOrSeals("intersection", ref result, first, second, tolerance, path))
                    {
                        return AddsUp("intersection", ref result, first, second, tolerance, path, out unworkable, ref open);
                    }

                    why = OpenWhy;
                    open = result;
                }

                unworkable = why;
                result = null;
                return false;
            }

            result = Weld3.Sealed(result, tolerance);

            if (ClosesOrSeals("intersection", ref result, first, second, tolerance, path))
            {
                return AddsUp("intersection", ref result, first, second, tolerance, path, out unworkable, ref open);
            }

            // Open to Validate, the other body is cut instead, whatever it costs.
            GeometryHelperLog.Debug("Boolean3: the common part came out open to Validate; the other body is cut instead.");
            open = result;

            if (TryGlue(CellsInside(other, cut, otherKnives, tolerance, out _, out _, out List<int> openOwners, flush), openOwners, tolerance, out GeoSolid3 cutOtherWay)
                && Implausible("intersection", cutOtherWay, first, second, tolerance) == null)
            {
                result = Weld3.Sealed(cutOtherWay, tolerance);

                if (ClosesOrSeals("intersection", ref result, first, second, tolerance, path))
                {
                    return AddsUp("intersection", ref result, first, second, tolerance, path, out unworkable, ref open);
                }
            }

            unworkable = OpenWhy;
            result = null;
            return false;
        }

        /// <summary>
        /// Collects the faces of the cells of one body that lie inside another, the body cut by the planes given.
        /// </summary>
        /// <param name="body">The body to cut.</param>
        /// <param name="other">The body whose inside is wanted.</param>
        /// <param name="knives">The planes to cut by; see <see cref="KnivesFor"/>.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="clean">false when a plane crossed a cell and still left it whole.</param>
        /// <param name="misjudged">Whether such a cell holds material on the two sides of the plane judged apart; see <see cref="Misjudged"/>.</param>
        /// <param name="owners">The place of the cell of each face.</param>
        /// <param name="flush">
        /// Whether a cell no thicker than the tolerance lying within the other body against its face is taken as within it, as a
        /// union worked out again reads it; see <see cref="HoldsTheTwo"/>.
        /// </param>
        private static List<GeoFace3> CellsInside(GeoSolid3 body, GeoSolid3 other, List<GeoPlane3> knives, Tolerance tolerance, out bool clean, out bool misjudged, out List<int> owners, bool flush = false)
        {
            var stuck = new List<GeoPlane3>();
            List<GeoSolid3> cells = SplitIntoCells(body, knives, tolerance, out clean, stuck);
            misjudged = !clean && Misjudged(cells, stuck, body, other, true, tolerance);
            owners = new List<int>();
            return FacesOfCells(cells, body, other, true, tolerance, owners, 0, flush);
        }

        /// <summary>
        /// Takes one solid out of another, using the default tolerance.
        /// </summary>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result)
        {
            return TrySubtract(subject, tool, out result, Tolerance.Global);
        }

        /// <summary>
        /// Takes one solid out of another, within a tolerance.
        /// </summary>
        /// <param name="subject">The body to cut material from.</param>
        /// <param name="tool">The body to remove.</param>
        /// <param name="result">What is left of the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// false when nothing is left, because the tool swallowed the subject whole, or when it cannot be worked out,
        /// which is logged: of two closed bodies, what is left closed as <see cref="GeoSolid3.Validate(Tolerance)"/> reads
        /// it, or not at all.
        /// </returns>
        /// <remarks>
        /// The walls of the cavity are not taken from the tool; they are already there. Dividing the
        /// subject by the planes of the tool lays a face along every part of the surface of the tool that
        /// passes through it, so keeping the cells that fall outside the tool keeps those faces with them.
        /// A tool that misses the subject changes nothing, and the subject comes back unaltered.
        /// </remarks>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance)
            => TrySubtract(subject, tool, out result, tolerance, out _);

        /// <summary>
        /// Takes one solid out of another, saying how it came out, using the default tolerance.
        /// </summary>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, out BooleanOutcome outcome)
            => TrySubtract(subject, tool, out result, Tolerance.Global, out outcome);

        /// <summary>
        /// Takes one solid out of another, within a tolerance, saying how it came out.
        /// </summary>
        /// <param name="subject">The body to cut material from.</param>
        /// <param name="tool">The body to remove.</param>
        /// <param name="result">What is left of the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="outcome">
        /// <see cref="BooleanOutcome.Made"/> with what is left, <see cref="BooleanOutcome.Empty"/> where the tool took the
        /// whole subject, or left no more than a slab the tolerance thick, or <see cref="BooleanOutcome.NotWorkedOut"/> where
        /// it could not be worked out, which is logged: nothing left takes a net volume to nought, where no answer is a step
        /// to work out some other way. A plate 10 by 10 and 0.0018 thick less a box over all but 0.0018 of its width leaves a
        /// rod 0.0018 square, which encloses nothing at a tolerance of a thousandth as
        /// <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, and is empty.
        /// </param>
        /// <returns>false when nothing is left, no more than a slab the tolerance thick among it, or when it cannot be worked out.</returns>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance, out BooleanOutcome outcome)
            => Subtract(subject, tool, out result, tolerance, out outcome, out _);

        /// <summary>
        /// Works out the difference as <see cref="TrySubtract(GeoSolid3, GeoSolid3, out GeoSolid3, Tolerance, out BooleanOutcome)"/> does, and
        /// hands back what came out open where two closed bodies gave nothing closed, for a closing to try.
        /// </summary>
        private static bool Subtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance, out BooleanOutcome outcome, out GeoSolid3 open)
        {
            Guard(subject, tool);
            open = null;

            try
            {
                if (TakeAway(subject, tool, out result, tolerance, out string unworkable, out open))
                {
                    if (IsNothing("difference", ref result, subject, tool, tolerance))
                    {
                        outcome = BooleanOutcome.Empty;
                        return false;
                    }

                    outcome = BooleanOutcome.Made;
                    return true;
                }

                if (unworkable != null)
                {
                    GeometryHelperLog.Warn($"A solid difference could not be worked out: {unworkable}; it is reported as not made.");
                    outcome = BooleanOutcome.NotWorkedOut;
                    return false;
                }

                outcome = BooleanOutcome.Empty;
                return false;
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                outcome = BooleanOutcome.NotWorkedOut;
                return Unworkable("difference", exception, out result);
            }
        }

        private static bool TakeAway(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance, out string unworkable)
            => TakeAway(subject, tool, out result, tolerance, out unworkable, out _);

        private static bool TakeAway(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance, out string unworkable, out GeoSolid3 open, OpenPath path = null)
        {
            result = null;
            unworkable = null;
            open = null;
            path = path ?? new OpenPath();

            if (!subject.GetAabb().CollidesWith(tool.GetAabb(), tolerance))
            {
                result = subject;
                return true;
            }

            GeoSolid3 given = subject;
            tolerance = ForWork(tolerance);
            subject = FlatForWork(subject, tolerance);
            tool = FlatForWork(tool, tolerance);

            // One body cut: the subject beyond the tool, or the subject whole less the tool within it.
            GeoSolid3 openCut = null;
            int fewest = 0;
            bool sealedOnce = false;

            if (TryCutOpeningsAtOnce(subject, out GeoSolid3 a, tolerance) && TryCutOpeningsAtOnce(tool, out GeoSolid3 b, tolerance))
            {
                bool? found = CombineCuttingOne(a, b, false, tolerance, out result, out bool untouched, out fewest);

                if (untouched)
                {
                    // A tool with no material within the subject's takes none of it, as one too far away to meet it
                    // takes none.
                    result = given;
                    return true;
                }

                // What is left of one body cut, of a volume no difference can leave, is worked out by cutting both instead.
                if (result != null && Implausible("difference", result, a, b, tolerance) is string why)
                {
                    GeometryHelperLog.Debug($"Boolean3: cutting one body {why}; both are cut by every plane of both instead.");
                    found = null;
                    result = null;
                }

                if (found.HasValue)
                {
                    result = Weld3.Sealed(result, tolerance);

                    if (!found.Value || ClosesOrSeals("difference", ref result, a, b, tolerance, path))
                    {
                        return found.Value && AddsUp("difference", ref result, a, b, tolerance, path, out unworkable, ref open);
                    }

                    GeometryHelperLog.Debug("Boolean3: cutting one body, the difference came out open to Validate; both are cut by every plane of both instead.");
                    open = result;
                    sealedOnce = true;
                }

                openCut = result;
            }

            List<GeoPlane3> planes = SharedPlanes(subject, tool, tolerance);

            // Sealed once already where it came out open after sealing, it is not sealed again.
            if (!sealedOnce && IsHeavy(planes, fewest, openCut) && TakesOpenCut("difference", openCut, subject, tool, tolerance, path, out result))
            {
                return true;
            }

            var stuck = new List<GeoPlane3>();
            List<GeoSolid3> cells = SplitIntoCells(subject, planes, tolerance, out bool clean, stuck);
            bool misjudged = !clean && Misjudged(cells, stuck, subject, tool, false, tolerance);
            var owners = new List<int>();
            List<GeoFace3> kept = FacesOfCells(cells, subject, tool, false, tolerance, owners);

            if (!GlueOrKeep(kept, owners, openCut, misjudged, tolerance, out result))
            {
                return false;
            }

            unworkable = Implausible("difference", result, subject, tool, tolerance);

            if (unworkable != null)
            {
                result = null;
                return false;
            }

            result = Weld3.Sealed(result, tolerance);

            if (!ClosesOrSeals("difference", ref result, subject, tool, tolerance, path))
            {
                unworkable = OpenWhy;
                open = open ?? result;
                result = null;
                return false;
            }

            return AddsUp("difference", ref result, subject, tool, tolerance, path, out unworkable, ref open);
        }

        /// <summary>
        /// Determines whether what a difference or a common part of two closed bodies that meet made, closed, encloses no more
        /// than a slab the tolerance thick, as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, and nothing else is wrong
        /// with it: nothing at the tolerance, which the boolean reports as nothing left, the result taken away.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A slab no thicker than the tolerance is within it of nothing, and was handed back all the same: a cube of 100 and a
        /// box 40 by 30 and a thousandth thick inside it, both turned, had the box as their common part, 1.2 cubic millimetres
        /// that <c>Validate</c> reads as enclosing nothing. The issues <c>Validate</c> may find of such a slab besides are its
        /// own: faces as thin as it (<see cref="SolidIssueKind.SliverFace"/>), edges as short, and edges three faces or more
        /// share. Any other, a face wound against the rest or one off flat, leaves the result as it was made.
        /// </para>
        /// <para>
        /// Not asked: two bodies apart, the first handed back as it came, an open result, a result of a body that does not
        /// close, whose faces enclose nothing to speak of, and what is left of a body that itself encloses no more than such a
        /// slab, which is as it was: a sheet of four faces less a box crossing it was a sheet before and stays one, and a plate
        /// 0.0005 thick less a box is what is left of the plate, whether or not the box reaches it.
        /// </para>
        /// </remarks>
        internal static bool IsNothing(string operation, ref GeoSolid3 result, GeoSolid3 first, GeoSolid3 second, Tolerance tolerance)
        {
            if (result == null || ReferenceEquals(result, first) || !first.GetAabb().CollidesWith(second.GetAabb(), tolerance)
                || !SolidValidation3.HasNoVolume(result, tolerance) || operation == "difference" && SolidValidation3.HasNoVolume(first, tolerance)
                || !Shells3.Closes(result, tolerance) || !Shells3.Closes(first, tolerance) || !Shells3.Closes(second, tolerance))
            {
                return false;
            }

            foreach (SolidIssue3 issue in result.ValidateForWork(tolerance).Issues)
            {
                if (issue.Kind != SolidIssueKind.NoVolume && issue.Kind != SolidIssueKind.SliverFace
                    && issue.Kind != SolidIssueKind.ShortEdge && issue.Kind != SolidIssueKind.NonManifoldEdge)
                {
                    return false;
                }
            }

            GeometryHelperLog.Debug($"Boolean3: the {operation} left encloses no more than a slab the tolerance thick, {result.GrossVolume:R}; it is reported as nothing left.");
            result = null;
            return true;
        }

        /// <summary>
        /// Why a boolean of two closed bodies is reported as not made where what it gave stays open.
        /// </summary>
        private const string OpenWhy = "it came out open, as Validate reads it, whichever way it was cut";

        /// <summary>
        /// Determines whether what a boolean of two bodies gave closes as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it,
        /// where the two do: closed as it is, or sealed so (<see cref="Weld3.SealedAsValidated"/>), which it is then made.
        /// </summary>
        /// <param name="operation">"difference", "union" or "intersection", whose bounds the sealed body has to hold (<see cref="Implausible"/>).</param>
        /// <param name="result">What the boolean gave, sealed where that closes it.</param>
        /// <param name="first">The first body; for a difference, the one material is taken from.</param>
        /// <param name="second">The second body; for a difference, the one taken away.</param>
        /// <param name="tolerance">The tolerance of the work.</param>
        /// <param name="path">Marked where what they gave did not close as it was, with how far the weld may have moved it.</param>
        /// <returns>false where the two close and what they gave does not, for the boolean to try another way or say so.</returns>
        /// <remarks>
        /// <see cref="GeoSolid3.ClosesByCorners(Tolerance)"/> matches the ends of the edges first and calls two copies of an edge about
        /// the tolerance apart one edge, which <c>Validate</c>, matching them along a line, reads as two, each open: a box 100 by
        /// 60 by 40 less a cube 1.3 across a hair the tolerance off three of its faces, turned and far from the origin, came out
        /// of the difference so, closed to the one and open by two stretches to the other, and the boolean never tried another
        /// way. A body two such bodies give that closes to both is the same to the bit, read once more.
        /// </remarks>
        private static bool ClosesOrSeals(string operation, ref GeoSolid3 result, GeoSolid3 first, GeoSolid3 second, Tolerance tolerance, OpenPath path)
        {
            path.Moved = 0.0;

            if (result == null || Shells3.Closes(result, tolerance) || !Shells3.Closes(first, tolerance) || !Shells3.Closes(second, tolerance))
            {
                return true;
            }

            path.Taken = true;

            // A weld may move the faces by up to four tolerances, and is taken only where what it leaves is a volume the
            // operation can give: the box less a cube 0.34 across welded closed held 0.25 cubic millimetres less than either
            // reading of the cube.
            GeoSolid3 sealedResult = Weld3.SealedAsValidated(result, tolerance, out double moved);

            if (!Shells3.Closes(sealedResult, tolerance) || Implausible(operation, sealedResult, first, second, tolerance) != null)
            {
                return false;
            }

            result = sealedResult;
            path.Moved = moved;
            return true;
        }

        /// <summary>
        /// Whether a boolean of two closed bodies came out open to <see cref="GeoSolid3.Validate(Tolerance)"/> on the way to
        /// its result, and how far the weld that closed the result moved its volume.
        /// </summary>
        private sealed class OpenPath
        {
            /// <summary>A result of the two came out open to <c>Validate</c>, and was welded or worked out another way.</summary>
            public bool Taken;

            /// <summary>How far the weld that closed the result moved its volume; nought where it closed unwelded.</summary>
            public double Moved;

            /// <summary>Whether the boolean is worked out only to check another's volume, and so is not checked itself.</summary>
            public bool Checking;
        }

        /// <summary>
        /// Takes what a boolean of two closed bodies gave where it closed as it was, and checks the volume of one that came
        /// out open on the way, welded or worked out another way, against the volumes of the two and of their common part, or
        /// of their union for a common part; false, not worked out, where it does not add up.
        /// </summary>
        /// <param name="operation">"difference", "union" or "intersection".</param>
        /// <param name="result">What the boolean gave; null where it is not taken.</param>
        /// <param name="first">The first body; for a difference, the one material is taken from.</param>
        /// <param name="second">The second body; for a difference, the one taken away.</param>
        /// <param name="tolerance">The tolerance of the work.</param>
        /// <param name="path">How the result was come to.</param>
        /// <param name="unworkable">Why it is not taken; null where it is.</param>
        /// <param name="open">What came out open, for a closing: the result where it is not taken, unless one is there already.</param>
        /// <remarks>
        /// <para>
        /// A union holds the two less their common part, a difference the first less the common part, and a common part the
        /// two less their union. The bodies a boolean gives at a tolerance have their faces within the tolerance of where
        /// they belong, and the faces the result and the common part, or the union, can place apart are those along the
        /// common part's skin: the two readings differ by no more than the tolerance times the area of that skin. A weld that
        /// closed the result moved it by as much again (<see cref="Weld3.SealedAsValidated"/>), no more than moving its
        /// corners can. Further off than both, and a hair for the rounding, the result counts some of the material twice or
        /// not at all, and is not taken: the union of a box 100 by 60 by 40 and a cube 1.25 across standing out through
        /// three of its faces by a hair more than the tolerance came out open and, closed, held 240 001.2065, the cube
        /// counted whole, where the two less their common part hold 240 000.0032 within 0.028.
        /// </para>
        /// <para>
        /// The common part, or the union for a common part, is worked out by the same boolean at the same tolerance; where
        /// that one comes out open on the way too, or cannot be worked out, the result is not taken either. A result that
        /// closes as it was is taken as it is, unchecked, at no cost.
        /// </para>
        /// </remarks>
        private static bool AddsUp(string operation, ref GeoSolid3 result, GeoSolid3 first, GeoSolid3 second, Tolerance tolerance, OpenPath path, out string unworkable, ref GeoSolid3 open)
        {
            unworkable = null;

            if (!path.Taken || path.Checking || result == null)
            {
                return true;
            }

            var checking = new OpenPath { Checking = true };
            GeoSolid3 other;
            string otherWhy;
            bool made = operation == "intersection"
                ? Unite(first, second, out other, tolerance, out otherWhy, out _, checking)
                : Share(first, second, out other, tolerance, out otherWhy, out _, checking);
            string against = operation == "intersection" ? "union" : "common part";

            if (!checking.Taken && (made || otherWhy == null))
            {
                double v = VolumeOf(result, tolerance);
                double v1 = VolumeOf(first, tolerance);
                double v2 = VolumeOf(second, tolerance);
                double vOther = made ? VolumeOf(other, tolerance) : 0.0;
                double skin = operation == "intersection" ? result.GrossSurfaceArea : (made ? other.GrossSurfaceArea : 0.0);
                double expected = operation == "difference" ? v1 - vOther : v1 + v2 - vOther;
                double bound = path.Moved + tolerance.EqualPoint * skin + 1E-9 * (Math.Abs(v1) + Math.Abs(v2));

                if (Math.Abs(v - expected) <= bound)
                {
                    return true;
                }

                // A union of two bodies lying face to face a gap within the tolerance apart joins them across it: welded
                // closed, it holds the two and the gap filled over where they face each other.
                double bridged = operation == "union" ? Bridged(first, second, tolerance) : 0.0;

                if (bridged > 0.0 && Math.Abs(v - (expected + bridged)) <= bound)
                {
                    return true;
                }

                unworkable = $"it came out open on the way, and holds {v:R} where the two less their {against} hold {expected:R}, within {bound:R}";
            }
            else
            {
                unworkable = $"it came out open on the way, and the {against} its volume is checked by {(made ? "came out open on the way too" : "could not be worked out")}";
            }

            GeometryHelperLog.Debug($"Boolean3: the {operation} {unworkable}; it is reported as not made.");
            open = open ?? result;
            result = null;
            return false;
        }

        /// <summary>
        /// Gets the volume a union fills bridging two bodies that lie face to face within the tolerance of each other and
        /// apart: over each patch where a face of one lies against a face of the other, its area times how far the two
        /// stand apart over it; nought where either carries openings.
        /// </summary>
        /// <remarks>
        /// Two cubes of 100, the second's face 0.001 clear of the first's, joined within a thousandth, came out open by the
        /// gap round the face and, welded closed, held 2 000 000: the two, 1 999 990, and the gap, 0.001 by 10 000 square
        /// millimetres. Checked against the two less their common part only, it was not taken.
        /// </remarks>
        private static double Bridged(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance)
        {
            if (first.Openings.Count > 0 || second.Openings.Count > 0)
            {
                return 0.0;
            }

            double bridged = 0.0;
            GeoAabb3 secondBox = second.GetAabb();

            // A gap exactly the tolerance wide is that wide to a few bits either way.
            Tolerance gap = Splition3.OnPlaneWithinRounding(first, Splition3.OnPlaneWithinRounding(second, tolerance));

            foreach (GeoFace3 face in first.Faces)
            {
                GeoAabb3 faceBox = face.GetAabb();

                if (!faceBox.CollidesWith(secondBox, tolerance))
                {
                    continue;
                }

                GeoVector3 normal = face.Boundary.Normal;
                GeoPlane3 plane = face.GetPlane();

                foreach (GeoFace3 other in second.Faces)
                {
                    if (normal.DotProduct(other.Boundary.Normal) >= 0.0 || !faceBox.CollidesWith(other.GetAabb(), tolerance) || !LiesIn(plane, other, gap))
                    {
                        continue;
                    }

                    // The patch lies on this face's plane, and the other's stands off it outwards where the two are apart.
                    GeoPlane3 across = other.GetPlane();

                    foreach (GeoFace3 patch in Intersect(face, other, gap))
                    {
                        bridged += patch.Area * Math.Max(0.0, across.SignedDistanceTo(patch.Centroid));
                    }
                }
            }

            return bridged;
        }

        /// <summary>
        /// Gets the volume of a body as the check of a boolean reads it: its faces', or its material's where it carries openings.
        /// </summary>
        private static double VolumeOf(GeoSolid3 solid, Tolerance tolerance)
            => solid.Openings.Count == 0 ? solid.GrossVolume : solid.GetVolume(tolerance);

        /// <summary>
        /// Says why the volume of what a boolean of two bodies gave is one the operation cannot give; null where it can.
        /// </summary>
        /// <param name="operation">"difference", "union" or "intersection".</param>
        /// <param name="result">What the boolean gave.</param>
        /// <param name="first">The first body; for a difference, the one material is taken from.</param>
        /// <param name="second">The second body; for a difference, the one taken away.</param>
        /// <param name="tolerance">The tolerance of the work.</param>
        /// <remarks>
        /// <para>
        /// A difference leaves no more than the body it was taken from, and no less than that less the whole of the body
        /// taken away; a union holds the larger of the two and no more than both; a common part holds no more than the
        /// smaller. Each within the tolerance times the area it can move across: the tool's for a difference, the smaller
        /// body's otherwise, where the corners of the two stand within the tolerance of each other and are made one, and a
        /// hair more for the rounding. A slab from a model less a wall that only touched it once came back 1 172 944 cubic
        /// millimetres larger than it was, where the wall's area allows 188 650.
        /// </para>
        /// <para>
        /// The volumes are the faces', which bound the material from above. A body still carrying openings, as the
        /// bodies cut by every plane of both can, holds less than its faces, so no bound from below is asked of it.
        /// </para>
        /// </remarks>
        internal static string Implausible(string operation, GeoSolid3 result, GeoSolid3 first, GeoSolid3 second, Tolerance tolerance)
        {
            double v = result.GrossVolume;
            double v1 = first.GrossVolume;
            double v2 = second.GrossVolume;
            double rounding = 1E-9 * (Math.Abs(v1) + Math.Abs(v2));
            bool whole = first.Openings.Count == 0 && second.Openings.Count == 0;
            double low, high, slack;

            switch (operation)
            {
                case "difference":
                    slack = tolerance.EqualPoint * second.GrossSurfaceArea + rounding;
                    low = whole ? v1 - v2 - slack : double.NegativeInfinity;
                    high = v1 + slack;
                    break;

                case "union":
                    slack = tolerance.EqualPoint * Math.Min(first.GrossSurfaceArea, second.GrossSurfaceArea) + rounding;
                    low = whole ? Math.Max(v1, v2) - slack : double.NegativeInfinity;
                    high = v1 + v2 + slack;
                    break;

                default:
                    slack = tolerance.EqualPoint * Math.Min(first.GrossSurfaceArea, second.GrossSurfaceArea) + rounding;
                    low = -slack;
                    high = Math.Min(v1, v2) + slack;
                    break;
            }

            if (v > high)
            {
                return $"gave a volume of {v:R} where a {operation} of bodies of {v1:R} and {v2:R} holds no more than {high:R}";
            }

            if (v < low)
            {
                return $"gave a volume of {v:R} where a {operation} of bodies of {v1:R} and {v2:R} holds no less than {low:R}";
            }

            return null;
        }

        /// <summary>
        /// Glues the cells of both bodies cut by every plane of both, unless that does no better than an open result
        /// already found by cutting one of them, which is then kept, as it was before cutting both was tried.
        /// </summary>
        /// <param name="kept">The faces of the cells kept.</param>
        /// <param name="owners">The place of the cell of each face; see <see cref="TryGlue(List{GeoFace3}, List{int}, Tolerance, out GeoSolid3)"/>.</param>
        /// <param name="openCut">The open result cutting one body gave, or null.</param>
        /// <param name="misjudged">
        /// Whether a cell a plane could not cut is judged apart across it (see <see cref="Misjudged"/>), so that what the
        /// cells glue into, closed or not, can hold the wrong volume. The open result is taken then where it welds closed,
        /// and otherwise kept where the cells glue into another volume than it holds, beyond the tolerance times its area.
        /// A slab of a Tekla model less a tool beneath it, cut one body at a time, came out open either way, the tool cut
        /// holding what it should; cut by every plane of both, the slab kept a cell the tool's top could not cut, judged
        /// within the tool, and closed three litres short. Taken wherever there was one, though, the open result left a cut
        /// skipped on eleven parts of that model cut one after another by the parts they meet, where it would not weld closed
        /// and the cells had glued into what they should; so taken, ten of the eleven take every cut.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="result">The result.</param>
        internal static bool GlueOrKeep(List<GeoFace3> kept, List<int> owners, GeoSolid3 openCut, bool misjudged, Tolerance tolerance, out GeoSolid3 result)
        {
            if (misjudged && openCut != null)
            {
                // The open result welded closed is taken; see Weld3.Sealed, which keeps a weld only where it holds the
                // volume.
                GeoSolid3 sealedCut = Weld3.Sealed(openCut, tolerance);

                if (sealedCut.ClosesByCorners(tolerance))
                {
                    GeometryHelperLog.Debug("Boolean3: cut by every plane of both, a cell a plane could not cut is judged apart across it; the open result of cutting one body, welded closed, is taken.");
                    result = sealedCut;
                    return true;
                }
            }

            if (TryGlue(kept, owners, tolerance, out result) && (openCut == null || result.ClosesByCorners(tolerance)))
            {
                // Misjudged, the cells glued closed are taken where they hold what the open result does, within the
                // tolerance times its area: a cell can be judged apart across a plane by a hair of it beyond the plane.
                if (!misjudged || openCut == null || Math.Abs(result.GrossVolume - openCut.GrossVolume) <= tolerance.EqualPoint * openCut.GrossSurfaceArea)
                {
                    return true;
                }

                GeometryHelperLog.Debug("Boolean3: cut by every plane of both, a cell a plane could not cut is judged apart across it, and what the cells glue into holds another volume than the open result of cutting one body; the open result is kept.");
            }

            result = openCut;
            return openCut != null;
        }

        /// <summary>
        /// Whether cutting both bodies by every plane of both costs far more than cutting one of them did, where that gave a
        /// result that came out open: more planes than twice those near the body fewer come near, and sixteen, as
        /// <see cref="CombineCuttingOne"/> reads the other way round as too dear.
        /// </summary>
        private static bool IsHeavy(List<GeoPlane3> planes, int fewest, GeoSolid3 openCut)
            => openCut != null && planes.Count > 2 * fewest + 16;

        /// <summary>
        /// Takes the open result of cutting one body, sealed, where cutting both by every plane of both would cost far more
        /// (<see cref="IsHeavy"/>): only where it holds a volume the operation can give, closes as
        /// <see cref="GeoSolid3.Validate(Tolerance)"/> reads it sealed, and adds up against the common part, or the union for a
        /// common part (<see cref="AddsUp"/>); false otherwise, for the boolean to cut both as it did.
        /// </summary>
        /// <remarks>
        /// A beam 2 000 by 200 by 300 less a bar 16 across bent through a right angle in forty steps, its end 0.001 short of
        /// the beam's floor, came out open cut the bar's way, holding what it should to a hundredth, and cut by the 664
        /// planes of both ran for minutes into tens of thousands of cells; the bar's end, the tolerance off the floor,
        /// sealed onto it, the difference takes 119 941 220 cubic millimetres in under a second.
        /// </remarks>
        private static bool TakesOpenCut(string operation, GeoSolid3 openCut, GeoSolid3 first, GeoSolid3 second, Tolerance tolerance, OpenPath path, out GeoSolid3 result)
        {
            result = null;

            if (Implausible(operation, openCut, first, second, tolerance) != null)
            {
                return false;
            }

            bool taken = path.Taken;
            GeoSolid3 sealedCut = Weld3.Sealed(openCut, tolerance);

            if (ClosesOrSeals(operation, ref sealedCut, first, second, tolerance, path) && Shells3.Closes(sealedCut, tolerance))
            {
                // It came out open on the way, so its volume is checked whether the weld closed it or not.
                path.Taken = true;
                GeoSolid3 none = null;

                if (AddsUp(operation, ref sealedCut, first, second, tolerance, path, out _, ref none))
                {
                    GeometryHelperLog.Debug($"Boolean3: cutting one body, the {operation} came out open, and sealed it adds up; both are not cut by every plane of both, which would cost far more.");
                    result = sealedCut;
                    return true;
                }
            }

            path.Taken = taken;
            path.Moved = 0.0;
            return false;
        }

        /// <summary>
        /// Joins two bodies without openings, or takes the second out of the first, cutting one of them only.
        /// </summary>
        /// <param name="a">The first body; for a difference, the one material is taken from.</param>
        /// <param name="b">The second body; for a difference, the one taken away.</param>
        /// <param name="union">true for the union, false for <paramref name="a"/> less <paramref name="b"/>.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="result">The result, when there is one.</param>
        /// <param name="untouched">
        /// For a difference, true when a clean cut found no material of either body within the other: the second takes
        /// nothing from the first, and <paramref name="result"/> is left null for the caller to hand the first back as
        /// it came.
        /// </param>
        /// <param name="fewest">How many planes of the other come near the body fewer come near, of the two.</param>
        /// <param name="flush">
        /// For a union worked out again, whether a cell no thicker than the tolerance lying within the other body against its
        /// face is left out; see <see cref="FacesOfCells(List{GeoSolid3}, GeoSolid3, GeoSolid3, bool, Tolerance, out bool, List{int}, int, bool)"/>.
        /// </param>
        /// <returns>
        /// Whether anything is left, as the public methods report it; null when neither body could be cut cleanly, or
        /// two closed bodies came out open whichever was cut, for the caller to cut both by every plane of both
        /// instead. An open result found on the way is then left in <paramref name="result"/>.
        /// </returns>
        /// <remarks>
        /// The body cut is the one fewer planes of the other come near, as for an intersection, but a difference cuts the
        /// body material is taken from where that costs about the same as cutting the other. A union keeps the
        /// cells of it beyond the other, and the other whole. For a difference, cutting the body material is taken
        /// from keeps its cells beyond the other; cutting the body taken away keeps the other whole and the cells
        /// within it turned inside out, which are the walls of the cavity and take away the part of the whole
        /// body's faces it covers. Where the whole body meets the cells, the two lie back to back in one plane,
        /// each cut its own way, and <see cref="CancelBackToBack(List{GeoFace3}, Tolerance)"/> takes from both the area they share.
        /// <para>
        /// A difference that finds nothing of the one body within the other takes nothing, and is not glued back
        /// together from the cells at all. Glued, it gave back only what the cutting rounded: two slabs side by side
        /// within a hundredth of a millimetre of each other, the larger cut along seventy metres by planes of the
        /// smaller a few thousandths off its own faces, came back twenty thousand cubic millimetres larger than it was.
        /// </para>
        /// </remarks>
        private static bool? CombineCuttingOne(GeoSolid3 a, GeoSolid3 b, bool union, Tolerance tolerance, out GeoSolid3 result, out bool untouched, out int fewest, bool flush = false)
        {
            result = null;
            untouched = false;

            List<GeoPlane3> cuttingA = PlanesNear(b, a.GetAabb(), tolerance);
            List<GeoPlane3> cuttingB = PlanesNear(a, b.GetAabb(), tolerance);
            fewest = Math.Min(cuttingA.Count, cuttingB.Count);

            // A difference cuts the body material is taken from first where that costs about the same: its cells beyond
            // the other are the result as they are, where the body taken away cut leaves the other whole, its faces less
            // those of the cells lying against them, worked out in the plane. Two walls from a model, the second over the
            // last 1.8 m of the first and 0.014 to the side, which a tolerance of 0.05 takes for nothing, gave the first's
            // new end the second's sides, its own sides their own, and its floor a needle 0.014 wide reaching back past the
            // end: closed within 0.05 only, and 255 000 cubic millimetres short of the wall left.
            bool aFirst = union ? cuttingA.Count <= cuttingB.Count : cuttingA.Count <= 2 * cuttingB.Count + 16;
            GeoSolid3 open = null;
            bool misjudged = false;
            bool anyClean = false;

            // A cut that left a cell whole where a plane crossed it, its cells judged alike on both sides of every such plane
            // (see Misjudged), is taken where neither body cuts cleanly, rather than cutting both by every plane of both. A
            // slab from a model whose outline ran down to a needle, its tip 0.0136 past the face of a wall the slab otherwise
            // stood clear of, could be cut cleanly neither way; cut by every plane of both, it came out open and 1 172 944
            // cubic millimetres larger than it was, where nothing of either lay in the other. Both ways are taken in turn, as
            // clean ones are: a slab of a Tekla model less another slab it meets, within a thousandth, came out open by 22
            // edges the first way and closed the other.
            List<(List<GeoSolid3> Cells, bool CutA)> consistent = null;

            // A result closed with faces wound against each other is kept while the other way round is tried; see Settle.
            GeoSolid3 crossed = null;

            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool cutA = aFirst == (attempt == 0);
                List<GeoPlane3> knives = cutA ? cuttingA : cuttingB;

                // The other way round only when it costs about the same: a bar's hundreds of planes would cut a
                // beam into thousands of cells again. Or whatever it costs, where the first way misjudged a cell it
                // could not cut; see Misjudged. Or where the first way came out open, when both would be cut by every
                // plane of both next: a slab cut cleanly by the 49 planes of another slab it meets came out open, and cut
                // by the 305 planes of both, into 50 033 cells, open again, where the other slab cut by the 234 planes of
                // the first, into 3 833 cells, closed.
                if (attempt == 1 && knives.Count > 2 * (cutA ? cuttingB : cuttingA).Count + 16 && !misjudged && open == null)
                {
                    break;
                }

                GeoSolid3 cut = cutA ? a : b;
                GeoSolid3 whole = cutA ? b : a;
                bool within = !union && !cutA;

                var stuck = new List<GeoPlane3>();
                List<GeoSolid3> cells = SplitIntoCells(cut, knives, tolerance, out bool clean, stuck);

                if (!clean)
                {
                    misjudged = Misjudged(cells, stuck, cut, whole, within, tolerance);

                    if (!misjudged)
                    {
                        (consistent = consistent ?? new List<(List<GeoSolid3>, bool)>(2)).Add((cells, cutA));
                    }

                    continue;
                }

                anyClean = true;
                bool? settled = Settle(cells, cutA, a, b, union, flush, tolerance, ref open, ref crossed, out result, out untouched);

                if (settled.HasValue)
                {
                    return settled.Value;
                }
            }

            // Once the first has found material of either body within the other, open or wound against itself, the second
            // is taken only where it closes: one finding nothing, or nothing glued, says less than the one before.
            if (!anyClean && consistent != null)
            {
                foreach ((List<GeoSolid3> cells, bool cutA) in consistent)
                {
                    bool found = open != null || crossed != null;
                    bool? settled = Settle(cells, cutA, a, b, union, flush, tolerance, ref open, ref crossed, out result, out untouched);

                    if (settled.HasValue && (!found || settled.Value && !untouched))
                    {
                        return settled.Value;
                    }

                    untouched = false;
                }
            }

            // Wound against itself and done no better the other way round, the result is as it was before such a result
            // was turned down: on the parts of a Tekla model most come of slivers and hold their volume, and cutting both
            // bodies by every plane of both instead took minutes and came out open.
            if (crossed != null)
            {
                result = crossed;
                untouched = false;
                return true;
            }

            result = open;
            return null;
        }

        /// <summary>
        /// Keeps the wanted cells of the body one way of a union or a difference cut, and glues them, with the other body
        /// where the result takes it whole; see <see cref="CombineCuttingOne"/>.
        /// </summary>
        /// <param name="cells">The cells of the body cut.</param>
        /// <param name="cutA">Whether the body cut is <paramref name="a"/>.</param>
        /// <param name="a">The first body; for a difference, the one material is taken from.</param>
        /// <param name="b">The second body; for a difference, the one taken away.</param>
        /// <param name="union">true for the union, false for <paramref name="a"/> less <paramref name="b"/>.</param>
        /// <param name="flush">
        /// For a union worked out again, whether a cell no thicker than the tolerance lying within the other body against its
        /// face is left out.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="open">An open result found before, or this one where it comes out open and none was.</param>
        /// <param name="crossed">
        /// A result closed with faces wound against each other found before, or this one where it comes out so and none was.
        /// </param>
        /// <param name="result">The result, when there is one.</param>
        /// <param name="untouched">For a difference, true when no material of the body cut lies within the other.</param>
        /// <returns>
        /// Whether anything is left; null when two closed bodies came out open this way, or closed with faces wound against
        /// each other.
        /// </returns>
        private static bool? Settle(List<GeoSolid3> cells, bool cutA, GeoSolid3 a, GeoSolid3 b, bool union, bool flush, Tolerance tolerance, ref GeoSolid3 open, ref GeoSolid3 crossed, out GeoSolid3 result, out bool untouched)
        {
            result = null;
            untouched = false;

            GeoSolid3 cut = cutA ? a : b;
            GeoSolid3 whole = cutA ? b : a;
            bool within = !union && !cutA;

            var owners = new List<int>();
            List<GeoFace3> kept = FacesOfCells(cells, cut, whole, within, tolerance, out bool anyInside, owners, 0, union && flush);

            if (!union && !anyInside)
            {
                untouched = true;
                return true;
            }

            if (within)
            {
                for (int i = 0; i < kept.Count; i++)
                {
                    kept[i] = kept[i].Flip();
                }
            }

            if (union || within)
            {
                kept.AddRange(whole.Faces);
                Own(owners, -1, whole.Faces.Count);
            }

            if (!TryGlue(kept, owners, tolerance, out result))
            {
                return false;
            }

            // Cut cleanly and still open, two closed bodies can close cut the other way round, or both cut: a beam
            // turned a hundredth of a degree off the axes, less an opening, came out open with the beam cut and
            // closed with the opening cut. The open one is kept in case nothing does better.
            if (!result.ClosesByCorners(tolerance))
            {
                if (!a.ClosesByCorners(tolerance) || !b.ClosesByCorners(tolerance))
                {
                    return true;
                }

                open = open ?? result;
                return null;
            }

            // Closed with faces wound against each other, which bodies wound alike cannot give, the other way round is tried
            // too, and this one kept in case it does no better: a beam from an IFC model less an opening whose side lay
            // against the web within the tolerance, cut within a thousandth, kept the web twice over a strip 6.6 wide, its
            // two copies facing the same way, and the next opening cut from it came out open, and its common part with the
            // beam as nothing; cut the other way round, it closes wound alike.
            if (Shells3.ClosesAlike(result.Faces, tolerance) || !Shells3.ClosesAlike(a.Faces, tolerance) || !Shells3.ClosesAlike(b.Faces, tolerance))
            {
                return true;
            }

            crossed = crossed ?? result;
            return null;
        }

        #region Machinery

        /// <summary>
        /// Whether an exception is one a boolean that cannot be worked out throws: a shape the work built refused by its
        /// constructor, or a direction asked of a vector with none. A boolean that tries says so by returning false.
        /// </summary>
        /// <remarks>
        /// Thrown out of a boolean, one of these took down whatever asked for it: the conversion of a whole IFC model,
        /// when the GeoSolid3 boolean cut the openings of one beam in it. The known causes are fixed; this keeps an
        /// unknown one to the boolean it happens in, and says so in the log.
        /// </remarks>
        internal static bool IsUnworkable(Exception exception)
            => exception is ArgumentException || exception is InvalidOperationException;

        /// <summary>
        /// Reports a boolean that could not be worked out: false, nothing made, and a warning with what was thrown.
        /// </summary>
        private static bool Unworkable(string operation, Exception exception, out GeoSolid3 result)
        {
            GeometryHelperLog.Warn($"A solid {operation} could not be worked out and is reported as not made.", exception);
            result = null;
            return false;
        }

        /// <summary>
        /// Rejects null arguments for every operation in one place.
        /// </summary>
        private static void Guard(GeoSolid3 first, GeoSolid3 second)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }
        }

        /// <summary>
        /// Gets the tolerance a boolean cuts and glues within: the one given, with a planar threshold no wider than
        /// the point one.
        /// </summary>
        /// <remarks>
        /// The cut and the glue have to agree. A plane is taken to leave a cell whole when no corner of the cell
        /// stands off it by more than the planar tolerance, so the cell can stand past it by as much, and the glue
        /// matches corners within the point tolerance. A planar tolerance wider than the point one leaves more
        /// than the glue can close: two faces of a bent bar half a degree apart, whose far corners come within
        /// five hundredths of each other's planes, came out open at a planar tolerance of five hundredths and a
        /// point tolerance of one.
        /// </remarks>
        internal static Tolerance ForWork(Tolerance tolerance)
        {
            return tolerance.EqualPlanar <= tolerance.EqualPoint
                ? tolerance
                : new Tolerance(tolerance.EqualPoint, tolerance.EqualVector, tolerance.EqualAngleRad, tolerance.EqualPoint);
        }

        /// <summary>
        /// Gets a body as a boolean works on it: wound outwards, its openings too, and every face flat within the planar
        /// tolerance of the work.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A boolean reads which way the faces are wound: the faces a cut lays across a cell face out of it, the cells
        /// are kept by the side of the other body they fall on, and two faces meet back to back where they face apart.
        /// A body wound inwards came back in cells with its own faces facing in and the new ones out, a tool wound
        /// inwards was taken for material, and a plate less a duct so wound held more than the plate. Turned the right
        /// way out first, either is the body it is.
        /// </para>
        /// <para>
        /// A face can be flat only to the wider planar tolerance a caller allowed: a modeller's cut leaves some a
        /// few hundredths out, as Tekla Structures left the top face of a notched beam 0.04 mm out at one corner.
        /// Cut at the tighter tolerance of the work, a piece of such a face is refused as not flat and leaves a
        /// hole. It is split into triangles on its own corners first instead, each exactly flat, which keeps the
        /// body closed and moves nothing. A body wound outwards with every face flat enough comes back as it is.
        /// </para>
        /// </remarks>
        internal static GeoSolid3 FlatForWork(GeoSolid3 solid, Tolerance work) => Flat(solid.TurnOutwards(), work);

        /// <summary>
        /// Gets a body with every face flat within the planar tolerance of the work, its openings too.
        /// </summary>
        private static GeoSolid3 Flat(GeoSolid3 solid, Tolerance work)
        {
            List<GeoFace3> faces = null;

            for (int i = 0; i < solid.Faces.Count; i++)
            {
                GeoFace3 face = solid.Faces[i];

                if (IsFlatWithin(face, work.EqualPlanar))
                {
                    faces?.Add(face);
                    continue;
                }

                if (faces == null)
                {
                    faces = new List<GeoFace3>(solid.Faces.Count + 4);

                    for (int j = 0; j < i; j++)
                    {
                        faces.Add(solid.Faces[j]);
                    }
                }

                if (!EarClipping.TryTriangulate(face, work, out GeoTriangle3[] triangles))
                {
                    // Nothing better to offer: the face goes in as it is, and the cut does what it can with it.
                    faces.Add(face);
                    continue;
                }

                foreach (GeoTriangle3 triangle in triangles)
                {
                    try
                    {
                        faces.Add(triangle.ToFace3(work));
                    }
                    catch (ArgumentException)
                    {
                        // A triangle with no area covers nothing, and its neighbours meet along its edges without it.
                    }
                }
            }

            List<GeoSolid3> openings = null;

            for (int i = 0; i < solid.Openings.Count; i++)
            {
                GeoSolid3 flat = Flat(solid.Openings[i], work);

                if (openings == null && !ReferenceEquals(flat, solid.Openings[i]))
                {
                    openings = new List<GeoSolid3>(solid.Openings.Count);

                    for (int j = 0; j < i; j++)
                    {
                        openings.Add(solid.Openings[j]);
                    }
                }

                openings?.Add(flat);
            }

            if (faces == null && openings == null)
            {
                return solid;
            }

            return new GeoSolid3(faces ?? new List<GeoFace3>(solid.Faces), openings ?? new List<GeoSolid3>(solid.Openings));
        }

        /// <summary>
        /// Determines whether every corner of a face, holes and all, lies within a distance of its plane.
        /// </summary>
        private static bool IsFlatWithin(GeoFace3 face, double planar)
        {
            GeoPlane3 plane = face.GetPlane();

            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                if (Math.Abs(plane.SignedDistanceTo(corner)) > planar)
                {
                    return false;
                }
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                foreach (GeoPoint3 corner in hole.Vertices)
                {
                    if (Math.Abs(plane.SignedDistanceTo(corner)) > planar)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Collects the face planes of both bodies, with duplicates removed.
        /// </summary>
        /// <remarks>
        /// Both bodies are cut by this one set rather than each by the other. That costs a few more cells
        /// and buys the thing that makes gluing work: where the two bodies meet, both sides have been
        /// carved by the same knives, so the two faces at the interface are the same polygon and cancel.
        /// </remarks>
        private static List<GeoPlane3> SharedPlanes(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance)
        {
            // One plane of the two only where they stand together over both bodies, which both are cut by them.
            GeoAabb3 region = first.GetAabb().Union(second.GetAabb());
            var planes = new List<GeoPlane3>();
            Splition3.CollectFacePlanes(first, region, tolerance, planes);
            Splition3.CollectFacePlanes(second, region, tolerance, planes);
            return planes;
        }

        /// <summary>
        /// Gets the planes that cut a body into cells each wholly inside or wholly outside another, and each wholly
        /// material or wholly carved out: those of the other's faces and openings that come near the body, and
        /// those of the body's own openings that come near the other.
        /// </summary>
        /// <remarks>
        /// Near the body, the surface of the other lies on the planes of its faces that come near the body, so a
        /// cell crossing none of them does not cross that surface, and one point inside the cell says which side of
        /// it the whole cell is on. The body's own openings need cutting along only where the other is: a cell
        /// anywhere else is outside the other, and dropped whatever it holds.
        /// </remarks>
        private static List<GeoPlane3> KnivesFor(GeoSolid3 body, GeoSolid3 other, Tolerance tolerance)
        {
            var planes = new List<GeoPlane3>();

            AddPlanesNear(other, body.GetAabb(), planes, tolerance);

            foreach (GeoSolid3 opening in body.Openings)
            {
                AddPlanesNear(opening, other.GetAabb(), planes, tolerance);
            }

            return planes;
        }

        /// <summary>
        /// Gets the distinct planes of the faces of a body, and of its openings, that come near a box.
        /// </summary>
        private static List<GeoPlane3> PlanesNear(GeoSolid3 solid, GeoAabb3 box, Tolerance tolerance)
        {
            var planes = new List<GeoPlane3>();
            AddPlanesNear(solid, box, planes, tolerance);
            return planes;
        }

        /// <summary>
        /// Adds to a list the planes not already in it of the faces of a body, and of its openings, that come near
        /// a box: not one plane over the box with one in it; see <see cref="Splition3.IsOnePlaneOver"/>.
        /// </summary>
        private static void AddPlanesNear(GeoSolid3 solid, GeoAabb3 box, List<GeoPlane3> planes, Tolerance tolerance)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                if (face.GetAabb().CollidesWith(box, tolerance))
                {
                    Splition3.AddPlane(face.GetPlane(), box, tolerance, planes);
                }
            }

            foreach (GeoSolid3 opening in solid.Openings)
            {
                AddPlanesNear(opening, box, planes, tolerance);
            }
        }

        /// <summary>
        /// Divides a body by a set of planes, so that no piece straddles any of them.
        /// </summary>
        /// <remarks>
        /// A plane that does not actually cut a cell leaves it alone, so the number of pieces grows only
        /// with the number of planes that really pass through the body rather than with how many were
        /// offered.
        /// </remarks>
        private static List<GeoSolid3> SplitIntoCells(GeoSolid3 subject, List<GeoPlane3> planes, Tolerance tolerance)
            => SplitIntoCells(subject, planes, tolerance, out _);

        /// <summary>
        /// Divides a body by a set of planes, saying whether every plane that crossed a piece divided it.
        /// </summary>
        /// <param name="subject">The body to divide.</param>
        /// <param name="planes">The planes to divide it by.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="clean">false when a plane had corners of a piece beyond the tolerance on both sides and still left it whole.</param>
        private static List<GeoSolid3> SplitIntoCells(GeoSolid3 subject, List<GeoPlane3> planes, Tolerance tolerance, out bool clean)
            => SplitIntoCells(subject, planes, tolerance, out clean, null);

        /// <summary>
        /// Divides a body by a set of planes, saying whether every plane that crossed a piece divided it, and which did not.
        /// </summary>
        /// <param name="subject">The body to divide.</param>
        /// <param name="planes">The planes to divide it by.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="clean">false when a plane had corners of a piece beyond the tolerance on both sides and still left it whole.</param>
        /// <param name="stuck">The planes that did so, each once; null where they are not wanted.</param>
        private static List<GeoSolid3> SplitIntoCells(GeoSolid3 subject, List<GeoPlane3> planes, Tolerance tolerance, out bool clean, List<GeoPlane3> stuck)
        {
            clean = true;

            // The gross boundary, with whatever the body has carved out of it left behind. An opening is
            // a region to be classified, not a property the pieces should inherit; the planes bounding it
            // are among the knives, so the cells it covers come out separately and are dropped later.
            List<GeoSolid3> cells = new List<GeoSolid3> { new GeoSolid3(subject.Faces) };

            foreach (GeoPlane3 plane in planes)
            {
                List<GeoSolid3> divided = new List<GeoSolid3>();

                foreach (GeoSolid3 cell in cells)
                {
                    if (Splition3.TrySplitCell(cell, plane, out GeoSolid3 above, out GeoSolid3 below, tolerance))
                    {
                        divided.Add(above);
                        divided.Add(below);
                    }
                    else if (!Crosses(cell, plane, tolerance))
                    {
                        divided.Add(cell);
                    }
                    else
                    {
                        // A cell can be in pieces: a plane before took the middle out of a bent bar and left its two
                        // ends as one cell. A plane passing between the pieces crosses none of them, so there is no rim
                        // to cap and nothing to cut, though there are corners on both sides of it. Each piece goes to
                        // its own side, or is cut; only a piece the plane crosses and cannot cut is left unclean.
                        foreach (GeoSolid3 piece in Shells3.Split(cell, tolerance))
                        {
                            if (Splition3.TrySplitCell(piece, plane, out GeoSolid3 pieceAbove, out GeoSolid3 pieceBelow, tolerance))
                            {
                                divided.Add(pieceAbove);
                                divided.Add(pieceBelow);
                            }
                            else
                            {
                                divided.Add(piece);

                                if ((stuck != null || clean) && Crosses(piece, plane, tolerance) && !IsSliver(piece, tolerance))
                                {
                                    clean = false;

                                    if (stuck != null && !stuck.Contains(plane))
                                    {
                                        stuck.Add(plane);
                                    }
                                }
                            }
                        }
                    }
                }

                cells = divided;
            }

            return OnePieceEach(cells, tolerance);
        }

        /// <summary>
        /// Checks whether a piece is thinner than twice the point tolerance, so that no point of it is further than that
        /// from its skin.
        /// </summary>
        /// <remarks>
        /// A plane crossing such a piece may leave nothing on one side wide enough to be a polygon, and fail to cut it.
        /// That leaves it no worse judged than it is anyway, by the middle of its thickness; a cut counted unclean for it
        /// was thrown away whole. A slab cut by the planes of its neighbour kept a wedge a hundredth of a millimetre at
        /// its widest, three metres long, which two planes crossed and could not cut, and the difference went on to cut
        /// both slabs by every plane of both: sixteen seconds, and sixty-nine thousand cubic millimetres more than the
        /// two share; the other way round, it came out open.
        /// </remarks>
        private static bool IsSliver(GeoSolid3 piece, Tolerance tolerance) => !TryGetInteriorPoint(piece, tolerance, out _);

        /// <summary>
        /// Whether a cell a plane crossed and could not cut holds material on its two sides that would be judged apart:
        /// kept on one side of the plane and not on the other.
        /// </summary>
        /// <param name="cells">The cells.</param>
        /// <param name="stuck">The planes that crossed a cell and could not cut it.</param>
        /// <param name="owner">The body the cells were cut from.</param>
        /// <param name="against">The body deciding inside from outside.</param>
        /// <param name="wantInside">true where the cells within that body are kept, false where those beyond it are.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// Such a cell is judged by one point of it, which is right for both sides where the other body lies the same way
        /// to both. Where it does not, part of the cell is misjudged, and the answer with it: a slab wrapping the corner of
        /// another was cut by the other's planes into a cell holding the tip of the wedge the two share and the part of the
        /// slab beyond the other's end, joined at the corner, where the other's end could not cut it; judged by a point
        /// beyond the end, the cell was outside the other, and the common part lost 6.4 m of the wedge.
        /// <para>
        /// A point either side of each plane alone misses a cell the other body takes an edge or a corner of, every such
        /// point lying beyond it. A cell of a body that closes is read too as <see cref="JudgedWhole"/> reads a piece of a
        /// body cut for its openings: by the middles of its parts either side of each plane, of the four two planes make,
        /// and of the eight three make that meet within the other body's box. A cube 10 across whose top, bottom and one
        /// side are each two faces meeting along a crack 0.0001 wide, less a box x from -4.1 to 4.993, y from -3.04 to
        /// 5.008 and z from 4.9986 to 11.03, none of whose planes would cut it, was one cell read beyond the box by every
        /// such point: taken whole, the difference held 1 000 cubic millimetres where it holds 874.94.
        /// </para>
        /// <para>
        /// The middle of a part is read only where it lies in the material of the cell, out of the body's openings: a part
        /// of a cell that is not convex can have its middle outside it. A U 30 across with a notch 10 wide, none of the
        /// planes of a box standing in the notch cutting it, had the middle of its part above the box's floor, its two arms,
        /// in the notch at (15, 5, 7.25), read as in the box, and was taken for misjudged where the box touches none of its
        /// material. Nor is a cell read where the other body does not close, having no inside to be part in.
        /// </para>
        /// <para>
        /// Where one plane crosses a cell, or two, or three that meet at a point within the other body's box, or three of which
        /// two are parallel, the parts all of them make are read too, each middle wherever it lies (see
        /// <see cref="EveryPlanePartsApart"/>). A box dipping 0.088 into an arm of a U,
        /// its sides and its floor crossing the U uncut, takes 2.55 between its sides and over its floor that no one or two
        /// of its planes part from the rest; the parts they do part from it held the box's piece with more of the U beyond the
        /// box, or had their middles in the notch, and the U was left whole: 2 667.83 cubic millimetres, trusted, where
        /// 2 665.28 is left.
        /// </para>
        /// </remarks>
        internal static bool Misjudged(List<GeoSolid3> cells, List<GeoPlane3> stuck, GeoSolid3 owner, GeoSolid3 against, bool wantInside, Tolerance tolerance)
        {
            foreach (GeoSolid3 cell in cells)
            {
                foreach (GeoPlane3 plane in stuck)
                {
                    if (!Crosses(cell, plane, tolerance))
                    {
                        continue;
                    }

                    bool? above = KeptBeside(cell, plane, 1.0, owner, against, wantInside, tolerance);
                    bool? below = KeptBeside(cell, plane, -1.0, owner, against, wantInside, tolerance);

                    if (above.HasValue && below.HasValue && above.Value != below.Value)
                    {
                        return true;
                    }
                }
            }

            // Then by the middles of its parts, as a piece cut for openings is read, each where it lies in the material of the
            // cell; the cells of a body that does not close hold no volume to weigh, nor a body that does not close an inside.
            if (!Shells3.Closes(owner, tolerance) || !Shells3.Closes(against, tolerance))
            {
                return false;
            }

            bool OutOfOpenings(GeoSolid3 cell, GeoPoint3 middle)
            {
                foreach (GeoSolid3 opening in owner.Openings)
                {
                    if (Containment3.Locate(opening, middle, tolerance) == PointLocation.Inside)
                    {
                        return false;
                    }
                }

                return true;
            }

            bool InMaterial(GeoSolid3 cell, GeoPoint3 middle)
                => Containment3.Locate(cell, middle, tolerance) == PointLocation.Inside && OutOfOpenings(cell, middle);

            var crossing = new List<(GeoPlane3 Plane, GeoSolid3 Opening)>();
            var one = new List<GeoSolid3>(1);

            foreach (GeoSolid3 cell in cells)
            {
                crossing.Clear();

                foreach (GeoPlane3 plane in stuck)
                {
                    if (Crosses(cell, plane, tolerance))
                    {
                        crossing.Add((plane, against));
                    }
                }

                one.Clear();
                one.Add(cell);

                if (crossing.Count > 0 && (JudgedWhole(one, crossing, tolerance, InMaterial) || EveryPlanePartsApart(cell, crossing, OutOfOpenings, tolerance)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether, of the parts every plane crossing a cell makes of it, where no more than three do, one holding more than the
        /// tolerance times the cell's area has its middle within the other body and another beyond it, each middle read where
        /// it lies out of the openings, in the cell's material or not.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <param name="crossing">The planes crossing it that could not cut it, each with the other body.</param>
        /// <param name="read">Whether a middle is read, given the cell and the middle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// The cell lies to one side of every other plane of the other body that comes near it, and a part of it bounded by
        /// every plane crossing it to one side of those too, so no face of the other body runs between the part and its middle:
        /// the middle, outside the material where the cell is not convex, lies within the other body where the part does.
        /// Not read: more than three planes, three meeting at a point outside the other body's box (see
        /// <see cref="EighthsPart"/>), three meeting along one line, and three parallel.
        /// </remarks>
        private static bool EveryPlanePartsApart(GeoSolid3 cell, List<(GeoPlane3 Plane, GeoSolid3 Opening)> crossing, Func<GeoSolid3, GeoPoint3, bool> read, Tolerance tolerance)
        {
            GeoSolid3 against = crossing[0].Opening;

            switch (crossing.Count)
            {
                case 1:
                    return Straddles(cell, crossing[0].Plane, tolerance, out GeoPoint3 aboveMiddle, out GeoPoint3 belowMiddle)
                        && IsIn(against, aboveMiddle, tolerance) != IsIn(against, belowMiddle, tolerance)
                        && read(cell, aboveMiddle) && read(cell, belowMiddle);
                case 2:
                    return QuartersPart(cell, crossing[0].Plane, crossing[1].Plane, against, tolerance, read);
                case 3:
                    return EighthsPart(cell, crossing[0].Plane, crossing[1].Plane, crossing[2].Plane, against, tolerance, read)
                        || SlabsPart(cell, crossing[0].Plane, crossing[1].Plane, crossing[2].Plane, against, tolerance, read);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the material of a cell on one side of a plane is kept, judged by a point of it there; null where none is
        /// found.
        /// </summary>
        private static bool? KeptBeside(GeoSolid3 cell, GeoPlane3 plane, double side, GeoSolid3 owner, GeoSolid3 against, bool wantInside, Tolerance tolerance)
        {
            if (!TryGetPointBeside(cell, plane, side, tolerance, out GeoPoint3 point))
            {
                return null;
            }

            foreach (GeoSolid3 opening in owner.Openings)
            {
                if (Containment3.Locate(opening, point, tolerance) == PointLocation.Inside)
                {
                    return false;
                }
            }

            return (Containment3.Locate(against, point, tolerance) == PointLocation.Inside) == wantInside;
        }

        /// <summary>
        /// Finds a point inside a body on one side of a plane, further from it than the planar tolerance: the middle of what
        /// lies on that side of the body's thickness under one of its largest surface triangles there.
        /// </summary>
        /// <remarks>
        /// The middle of the whole thickness can lie on the other side: a slab of a Tekla model less a tool whose sloping top
        /// a plane of the slab's could not cut had 131 millimetres of the slab above the plane under the triangles of the
        /// slab's top, and the middle of the slab under each of them below it. With no point found above, the cell along the
        /// plane, judged by a point within the tool, was not found misjudged, and the difference took it whole: 18 litres
        /// beyond the tool. Where the thickness under a triangle runs across the plane, its middle is taken as far as the plane.
        /// </remarks>
        internal static bool TryGetPointBeside(GeoSolid3 solid, GeoPlane3 plane, double side, Tolerance tolerance, out GeoPoint3 point)
        {
            point = GeoPoint3.Origin;
            int tried = 0;

            foreach (GeoTriangle3 triangle in LargestFirst(solid.TriangulateQuietly(tolerance)))
            {
                if (side * plane.SignedDistanceTo(triangle.Centroid) <= tolerance.EqualPlanar)
                {
                    continue;
                }

                if (TryGetMidpointUnder(solid, triangle, tolerance, plane, side, out GeoPoint3 candidate, out _) && side * plane.SignedDistanceTo(candidate) > tolerance.EqualPlanar)
                {
                    point = candidate;
                    return true;
                }

                if (++tried == 8)
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// The triangles of a mesh, the largest first.
        /// </summary>
        private static IEnumerable<GeoTriangle3> LargestFirst(GeoTriangle3[] mesh)
        {
            var areas = new double[mesh.Length];
            var order = new int[mesh.Length];

            for (int i = 0; i < mesh.Length; i++)
            {
                areas[i] = -mesh[i].GetAreaVector().Length;
                order[i] = i;
            }

            Array.Sort(areas, order);

            foreach (int i in order)
            {
                yield return mesh[i];
            }
        }

        /// <summary>
        /// Checks whether a plane has corners of a body beyond the tolerance on both sides of it.
        /// </summary>
        private static bool Crosses(GeoSolid3 solid, GeoPlane3 plane, Tolerance tolerance)
        {
            bool above = false, below = false;

            foreach (GeoFace3 face in solid.Faces)
            {
                foreach (GeoPoint3 corner in face.Boundary.Vertices)
                {
                    double distance = plane.SignedDistanceTo(corner);

                    above = above || distance > tolerance.EqualPlanar;
                    below = below || distance < -tolerance.EqualPlanar;

                    if (above && below)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Separates every cell into the pieces of material that do not touch.
        /// </summary>
        /// <remarks>
        /// A cell is judged by one point of it, which is only safe when the cell is one piece. A plane lying
        /// exactly along the wall of a hole does not cut the strip either side of the hole — there is no
        /// material on the far side of it to cut — so that strip comes out as one cell in two pieces, and a
        /// single point decides for both. Subtracting a box that overlapped an existing hole once threw away a
        /// whole block of material nowhere near the box that way.
        /// </remarks>
        private static List<GeoSolid3> OnePieceEach(List<GeoSolid3> cells, Tolerance tolerance)
        {
            var pieces = new List<GeoSolid3>(cells.Count);

            foreach (GeoSolid3 cell in cells)
            {
                pieces.AddRange(Shells3.Split(cell, tolerance));
            }

            return pieces;
        }

        /// <summary>
        /// Collects the faces of every cell that is material of the body it came from.
        /// </summary>
        /// <param name="cells">The cells to sort.</param>
        /// <param name="owner">The body the cells were cut from.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="owners">Where given, receives for each face collected the place of its cell, plus <paramref name="offset"/>; see <see cref="TryGlue(List{GeoFace3}, List{int}, Tolerance, out GeoSolid3)"/>.</param>
        /// <param name="offset">What the places of the cells start from.</param>
        private static List<GeoFace3> FacesOfCells(List<GeoSolid3> cells, GeoSolid3 owner, Tolerance tolerance, List<int> owners = null, int offset = 0)
        {
            List<GeoFace3> faces = new List<GeoFace3>();
            double rounding = Splition3.Rounding(owner);

            for (int c = 0; c < cells.Count; c++)
            {
                if (IsMaterial(cells[c], owner, tolerance, rounding, out _, out _))
                {
                    faces.AddRange(cells[c].Faces);
                    Own(owners, offset + c, cells[c].Faces.Count);
                }
            }

            return faces;
        }

        /// <summary>
        /// Adds to the owners of faces collected, where they are wanted, one owner for so many faces.
        /// </summary>
        private static void Own(List<int> owners, int owner, int faces)
        {
            if (owners == null)
            {
                return;
            }

            for (int k = 0; k < faces; k++)
            {
                owners.Add(owner);
            }
        }

        /// <summary>
        /// Collects the faces of the cells that are material of their own body and lie on the wanted side
        /// of another one.
        /// </summary>
        /// <param name="cells">The cells to sort.</param>
        /// <param name="owner">The body the cells were cut from.</param>
        /// <param name="against">The body deciding inside from outside.</param>
        /// <param name="wantInside">true to keep the cells within that body, false to keep those beyond it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="owners">Where given, receives for each face collected the place of its cell, plus <paramref name="offset"/>.</param>
        /// <param name="offset">What the places of the cells start from.</param>
        /// <param name="union">Whether the cells kept are joined to the other body whole; see the overload saying whether any lies within it.</param>
        private static List<GeoFace3> FacesOfCells(List<GeoSolid3> cells, GeoSolid3 owner, GeoSolid3 against, bool wantInside, Tolerance tolerance, List<int> owners = null, int offset = 0, bool union = false)
            => FacesOfCells(cells, owner, against, wantInside, tolerance, out _, owners, offset, union);

        /// <summary>
        /// Collects the faces of the cells that are material of their own body and lie on the wanted side of another
        /// one, saying whether any of that material lies within the other.
        /// </summary>
        /// <param name="cells">The cells to sort.</param>
        /// <param name="owner">The body the cells were cut from.</param>
        /// <param name="against">The body deciding inside from outside.</param>
        /// <param name="wantInside">true to keep the cells within that body, false to keep those beyond it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="anyInside">Whether a cell of material lies within <paramref name="against"/>.</param>
        /// <param name="owners">Where given, receives for each face collected the place of its cell, plus <paramref name="offset"/>.</param>
        /// <param name="offset">What the places of the cells start from.</param>
        /// <param name="union">
        /// Whether the cells kept are joined to the other body whole, as a union that came out wound against itself is worked
        /// out again: a cell no thicker than the tolerance, read on the other's skin, is then taken as within it where its
        /// side lying nearest flush on a face of the other faces the same way, so that it is not held twice. A beam whose end
        /// ran exactly a thousandth into a column, joined to it within a thousandth, kept the slab of its end within the
        /// column, the column whole beside it, and came out wound against itself, 101 cubic millimetres more than the two
        /// less their common part. Only then: read so by every union, a chip exactly the tolerance deep is cut by the union
        /// where the common part and the differences read it as nothing; see <see cref="IsMaterial"/>.
        /// </param>
        private static List<GeoFace3> FacesOfCells(List<GeoSolid3> cells, GeoSolid3 owner, GeoSolid3 against, bool wantInside, Tolerance tolerance, out bool anyInside, List<int> owners = null, int offset = 0, bool union = false)
        {
            List<GeoFace3> faces = new List<GeoFace3>();
            anyInside = false;

            // The rounding of both bodies, whichever is cut: a chip is the same chip to every operation of the two.
            double rounding = Math.Max(Splition3.Rounding(owner), Splition3.Rounding(against));

            for (int c = 0; c < cells.Count; c++)
            {
                if (!IsMaterial(cells[c], owner, tolerance, rounding, out GeoPoint3 sample, out Tolerance? fine))
                {
                    continue;
                }

                PointLocation location = LocateSample(against, sample, tolerance, fine);
                bool within = location == PointLocation.Inside
                    || (union && location == PointLocation.OnSide && !fine.HasValue && LiesFlushAlike(cells[c], against, tolerance));
                anyInside |= within;

                if (within == wantInside)
                {
                    faces.AddRange(cells[c].Faces);
                    Own(owners, offset + c, cells[c].Faces.Count);
                }
            }

            return faces;
        }

        /// <summary>
        /// Whether the face of a cell lying nearest flush on a face of another body, over some area, faces the same way as
        /// that face: the cell's material there is on the side of the other's.
        /// </summary>
        /// <remarks>
        /// A cell no thicker than the tolerance lies within it of a face of the other with both its sides: a slab inside the
        /// top of a box has its top on the box's, facing the same way, and its floor the slab's thickness beneath, facing the
        /// other way; one on top of the box has its floor on the box's top, facing the other way, and its top above. The side
        /// lying nearer says which.
        /// </remarks>
        private static bool LiesFlushAlike(GeoSolid3 cell, GeoSolid3 other, Tolerance tolerance)
        {
            GeoAabb3 otherBox = other.GetAabb();
            double alike = double.MaxValue, against = double.MaxValue;

            foreach (GeoFace3 face in cell.Faces)
            {
                GeoAabb3 faceBox = face.GetAabb();

                if (!faceBox.CollidesWith(otherBox, tolerance))
                {
                    continue;
                }

                GeoVector3 normal = face.Boundary.Normal;

                foreach (GeoFace3 flush in other.Faces)
                {
                    double facing = normal.DotProduct(flush.Boundary.Normal);
                    GeoPlane3 plane = flush.GetPlane();

                    if (facing == 0.0 || !faceBox.CollidesWith(flush.GetAabb(), tolerance) || !LiesIn(plane, face, tolerance))
                    {
                        continue;
                    }

                    double off = 0.0;

                    foreach (GeoPoint3 corner in face.Boundary.Vertices)
                    {
                        off = Math.Max(off, Math.Abs(plane.SignedDistanceTo(corner)));
                    }

                    if (off >= (facing > 0.0 ? alike : against))
                    {
                        continue;
                    }

                    foreach (GeoFace3 patch in Intersect(flush, face, tolerance))
                    {
                        if (IsWide(patch, tolerance))
                        {
                            if (facing > 0.0)
                            {
                                alike = off;
                            }
                            else
                            {
                                against = off;
                            }

                            break;
                        }
                    }
                }
            }

            // A side found on the far side only, the near one lying within the tolerance of the other's face and no closer, is
            // no more than the cell standing the tolerance off it: a sliver along an edge the two share reads so.
            return alike < against && (alike <= Math.Max(Splition3.Rounding(cell), Splition3.Rounding(other)) || against < double.MaxValue);
        }

        /// <summary>
        /// Whether a patch of two faces is wider on average than the point tolerance: twice its area over its perimeter. A
        /// sliver along an edge the two share, the tolerance wide or less, is no face lying on another.
        /// </summary>
        private static bool IsWide(GeoFace3 patch, Tolerance tolerance)
        {
            double perimeter = 0.0;
            IReadOnlyList<GeoPoint3> corners = patch.Boundary.Vertices;

            for (int i = 0; i < corners.Count; i++)
            {
                perimeter += corners[i].DistanceTo(corners[(i + 1) % corners.Count]);
            }

            return perimeter > 0.0 && 2.0 * patch.Area / perimeter > tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a cell holds material of the body it was cut from, and finds a point inside it.
        /// </summary>
        /// <remarks>
        /// The cells come from the gross boundary, so one of them can be filling an opening. Every plane
        /// bounding an opening is among the knives, so no cell straddles the wall of one and a single
        /// sample settles the whole cell.
        /// <para>
        /// Dropping such a cell is what carves the opening into the result. The face between a cell that
        /// is kept and one that is dropped is traversed once rather than twice, so it survives the gluing
        /// and becomes the wall of the cavity — which is why the result needs no openings of its own.
        /// </para>
        /// </remarks>
        /// <param name="cell">The cell.</param>
        /// <param name="owner">The body the cell was cut from.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="rounding">
        /// What rounding the coordinates of the bodies of the operation can add to the tolerance (<see cref="Splition3.Rounding"/>):
        /// a cell no thicker than the two together is within the tolerance of nothing.
        /// </param>
        /// <param name="sample">A point inside the cell.</param>
        /// <param name="fine">
        /// The tolerance the sample is read within where the tolerance given reads it on the skin of another body; null where
        /// that reading stands. See <see cref="LocateSample"/>.
        /// </param>
        private static bool IsMaterial(GeoSolid3 cell, GeoSolid3 owner, Tolerance tolerance, double rounding, out GeoPoint3 sample, out Tolerance? fine)
        {
            fine = null;

            // A cell thinner than twice the point tolerance has no point further than that from its own skin, so the
            // search finds none in it; it is material all the same, and the middle of its thickness places it. Taken
            // for empty, a sliver a plane left along a face took its share of the body away with it: of two slabs
            // side by side, a difference took thirty-eight thousand cubic millimetres more than the two share.
            // The middle of a cell thicker than the tolerance and thinner than twice it lies within the tolerance of both its
            // sides, and so of the skin of a body the cell lies within as of one it lies beyond; it is read within a quarter
            // of its thickness, as it was found. Read within the tolerance, a void 0.0015 thick inside a body, a box taken out
            // of it, was on the box's skin and not in it, and the difference left the body whole. A cell no thicker than the
            // tolerance, and what rounding the coordinates of the two bodies can add, is within the tolerance of nothing, and
            // reads as it did: a chip exactly the tolerance deep on a turned body is that deep to a few bits either way, and
            // read finer in some of its cells and not in others, it left a difference wound two ways, and took from a column
            // less a beam lying the tolerance into it 30 cubic millimetres more than the two share. Read by the rounding of
            // each cell's own corners, a chip 0.0010000001 deep at the corner of a turned cube was cut while one as deep along
            // its edge was not, and of another chip the common part was cut where the difference was not, each from cells of
            // its own body: the rounding is the bodies', so that a chip reads one way to every operation. A point found further
            // than the tolerance inside its cell is read within half of it where the tolerance reads it on the other body's
            // skin: in a void 0.002 thick it is the tolerance from both sides, more than it to the cell and no more than it to
            // the box.
            if (TryGetInteriorPoint(cell, tolerance, out sample))
            {
                double half = tolerance.EqualPoint / 2.0;
                fine = new Tolerance(half, Math.Min(tolerance.EqualVector, half), tolerance.EqualAngleRad, half);
            }
            else
            {
                if (!TryGetMidpointOfThinBody(cell, tolerance, out sample, out double depth))
                {
                    return false;
                }

                if (depth > tolerance.EqualPoint + rounding)
                {
                    double quarter = depth / 4.0;
                    fine = new Tolerance(quarter, Math.Min(tolerance.EqualVector, quarter), tolerance.EqualAngleRad, quarter);
                }
            }

            // Every cell was cut from the body, so with nothing carved out of it there is no way for one
            // to be anything but material, and the test is skipped rather than paid for.
            if (owner.Openings.Count == 0)
            {
                return true;
            }

            // Only an opening takes material away. Asked of the whole body, the middle of a sliver along its skin lies
            // within the tolerance of a face and reads as on it rather than in it.
            foreach (GeoSolid3 opening in owner.Openings)
            {
                if (LocateSample(opening, sample, tolerance, fine) == PointLocation.Inside)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Locates the point a cell is judged by against another body: within the tolerance, and where that reads it on the
        /// body's skin, within the finer tolerance the cell's thickness allows, where it has one.
        /// </summary>
        private static PointLocation LocateSample(GeoSolid3 body, GeoPoint3 sample, Tolerance tolerance, Tolerance? fine)
        {
            PointLocation location = Containment3.Locate(body, sample, tolerance);
            return location == PointLocation.OnSide && fine.HasValue ? Containment3.Locate(body, sample, fine.Value) : location;
        }

        /// <summary>
        /// Finds a point strictly inside a body.
        /// </summary>
        /// <remarks>
        /// The centroid of a concave body can fall outside it, so it is checked rather than trusted. When
        /// it fails, the search steps a little way inwards from the middle of each surface triangle, along
        /// the inward normal. The step shrinks on each round because a body can be thinner in one place
        /// than the first step assumes, and a step that overshoots comes out the far side.
        /// </remarks>
        internal static bool TryGetInteriorPoint(GeoSolid3 solid, Tolerance tolerance, out GeoPoint3 point)
        {
            point = GeoPoint3.Origin;

            GeoPoint3 centroid = solid.GrossCentroid;

            if (Containment3.Locate(solid, centroid, tolerance) == PointLocation.Inside)
            {
                point = centroid;
                return true;
            }

            double reach = solid.GetAabb().Diagonal.Length;

            if (reach <= 0.0)
            {
                return false;
            }

            GeoTriangle3[] mesh = solid.TriangulateQuietly(tolerance);

            for (double fraction = 1E-2; fraction >= 1E-5; fraction *= 0.1)
            {
                foreach (GeoTriangle3 triangle in mesh)
                {
                    if (triangle.IsDegenerateByArea(tolerance))
                    {
                        continue;
                    }

                    // The direction from the area the tolerance just let through: the triangle's own Normal is found at
                    // the default tolerance, and threw for a triangle finer than that but not than this one.
                    GeoVector3 area = triangle.GetAreaVector();
                    GeoPoint3 candidate = triangle.Centroid.Subtract(area.Multiply(reach * fraction / area.Length));

                    if (Containment3.Locate(solid, candidate, tolerance) == PointLocation.Inside)
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Finds a point inside a body too thin for <see cref="TryGetInteriorPoint"/> to find one: the middle of its
        /// thickness under the middle of one of its largest surface triangles.
        /// </summary>
        /// <remarks>
        /// A ray from inside a triangle of a closed skin, run along the inward normal, is in the body from the triangle to
        /// where it next meets the skin, so halfway there it is as deep in the body as that ray goes. The point is checked
        /// all the same, within a tolerance a quarter of that depth: a skin wound the wrong way somewhere sends the ray out
        /// of the body instead. The depth is handed back with the point.
        /// </remarks>
        internal static bool TryGetMidpointOfThinBody(GeoSolid3 solid, Tolerance tolerance, out GeoPoint3 point, out double depth)
        {
            point = GeoPoint3.Origin;
            depth = 0.0;
            int tried = 0;

            foreach (GeoTriangle3 triangle in LargestFirst(solid.TriangulateQuietly(tolerance)))
            {
                if (TryGetMidpointUnder(solid, triangle, tolerance, out point, out depth))
                {
                    return true;
                }

                if (++tried == 8)
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the middle of a body's thickness under a triangle of its skin, along the triangle's inward normal.
        /// </summary>
        private static bool TryGetMidpointUnder(GeoSolid3 solid, GeoTriangle3 triangle, Tolerance tolerance, out GeoPoint3 point, out double depth)
            => TryGetMidpointUnder(solid, triangle, tolerance, null, 0.0, out point, out depth);

        /// <summary>
        /// Finds the middle of a body's thickness under a triangle of its skin, along the triangle's inward normal, as far as
        /// a plane where one is given and the thickness runs across it from the side the triangle is on.
        /// </summary>
        private static bool TryGetMidpointUnder(GeoSolid3 solid, GeoTriangle3 triangle, Tolerance tolerance, GeoPlane3? plane, double side, out GeoPoint3 point, out double thickness)
        {
            point = GeoPoint3.Origin;
            thickness = 0.0;
            GeoVector3 area = triangle.GetAreaVector();

            if (area.Length <= 0.0)
            {
                return false;
            }

            // The skin itself is where the ray starts; a crossing that near it is the ray leaving its own triangle.
            double start = 1E-9 * (1.0 + solid.GetAabb().Diagonal.Length);
            GeoVector3 inward = area.Multiply(-1.0 / area.Length);
            var ray = new GeoRay3(triangle.Centroid, inward);
            Tolerance holding = Intersection3.Holding(Intersection3.Rounding(solid.GetAabb(), ray.Origin), tolerance);
            double depth = double.MaxValue;

            foreach (GeoFace3 face in solid.Faces)
            {
                if (Intersection3.TryCrossFace(ray, face, out GeoPoint3 hit, tolerance, holding, true))
                {
                    double along = triangle.Centroid.GetVectorTo(hit).DotProduct(inward);

                    if (along > start && along < depth)
                    {
                        depth = along;
                    }
                }
            }

            if (depth == double.MaxValue)
            {
                return false;
            }

            if (plane.HasValue)
            {
                double toward = -side * plane.Value.Normal.DotProduct(inward);

                if (toward > 0.0)
                {
                    depth = Math.Min(depth, side * plane.Value.SignedDistanceTo(triangle.Centroid) / toward);
                }
            }

            GeoPoint3 candidate = triangle.Centroid.Add(inward.Multiply(depth / 2.0));
            double fine = depth / 4.0;
            var within = new Tolerance(fine, Math.Min(tolerance.EqualVector, fine), tolerance.EqualAngleRad, fine);

            if (Containment3.Locate(solid, candidate, within) != PointLocation.Inside)
            {
                return false;
            }

            point = candidate;
            thickness = depth;
            return true;
        }

        /// <summary>
        /// Glues a collection of cell faces into one closed body.
        /// </summary>
        /// <remarks>
        /// A face between two cells that were both kept is interior to the result, and it appears twice
        /// among the collected faces, once each way round. Dropping both leaves exactly the outer skin —
        /// when the two copies match vertex for vertex, which they need not; see
        /// <see cref="CancelBackToBack(List{GeoFace3}, Tolerance)"/> for the pairs that do not. The survivors are then merged where they
        /// are coplanar and touching, which undoes the subdivision the cutting introduced.
        /// <para>
        /// Taking from two faces the area they share is worked out in the plane, where a piece of either is dropped as a seam
        /// when it is no wider on average than the point tolerance, and two edges within the tolerance of each other leave a
        /// needle between them; the faces beside keep their edges all the same, and a skin closed before it is open after.
        /// Each sliver it is open by, no wider than four point tolerances, is closed by a face across it, where that closes
        /// the skin; see <see cref="LoopAssembly.TryCloseSlivers"/>.
        /// </para>
        /// </remarks>
        private static bool TryGlue(List<GeoFace3> faces, Tolerance tolerance, out GeoSolid3 result)
            => TryGlue(faces, null, tolerance, out result);

        /// <summary>
        /// Glues a collection of cell faces into one closed body, knowing which closed piece each face came from: the
        /// faces of one piece are never taken for back to back with each other; see <see cref="CancelBackToBack(List{GeoFace3}, List{int}, Tolerance)"/>.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="owners">For each face, the cell or the body whole it came from; null where that is not known.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="result">The body.</param>
        private static bool TryGlue(List<GeoFace3> faces, List<int> owners, Tolerance tolerance, out GeoSolid3 result)
        {
            result = null;

            List<GeoFace3> paired = DropMatchedPairs(faces, owners, tolerance, out List<int> pairedOwners);
            List<GeoFace3> skin = CancelBackToBack(paired, pairedOwners, tolerance);

            if (skin.Count < 4)
            {
                return false;
            }

            result = Merge3.CoplanarFaces(new GeoSolid3(skin), tolerance);

            if (result.ClosesByCorners(tolerance))
            {
                return true;
            }

            // Merging the faces of a plane joins their outlines within the tolerance, where corners a hair apart are one point
            // to the merged face and two to the faces beside it: the skin of the common part of two bent bars, closed, came
            // out open by edges a hundredth long. A merge that opens a closed skin is not made, where the skin is not too
            // many faces to be a result: a slab with holes less another, cut into cells, left a skin of 42 590 faces where
            // the merge gave 270, and every question asked of it after, and every cut, took seconds.
            if (skin.Count > Math.Max(4 * result.Faces.Count, 2048))
            {
                return true;
            }

            var glued = new GeoSolid3(skin);

            if (glued.ClosesByCorners(tolerance))
            {
                result = glued;
                return true;
            }

            // The skin open where the faces were closed before the cancelling is closed across the slivers it left.
            if (!ReferenceEquals(skin, paired) && TryCloseBackToBackGaps(skin, paired, tolerance, out GeoSolid3 healed))
            {
                GeoSolid3 merged = Merge3.CoplanarFaces(healed, tolerance);
                result = merged.ClosesByCorners(tolerance) ? merged : healed;
            }

            return true;
        }

        /// <summary>
        /// Closes the slivers taking faces lying back to back from each other left a skin open by, where the faces were closed
        /// before and the slivers close them again.
        /// </summary>
        /// <remarks>
        /// A union of an I 11 long and a prism of twelve sides passing the corner of its flange came out open twice. Taken
        /// from the end of the I, the prism's face there left a corner of it 0.0006 square millimetres across, every side of
        /// it longer than the point tolerance, which the clipping took for a seam and dropped, while the faces beside it kept
        /// their edges round it. And a corner of the prism stood 0.002 off the flange's edge, so that taken from the flange's
        /// side, its face left a needle 5.9 long beside that edge, on the piece of the side kept. Each is a sliver, and a face
        /// across each closes the union.
        /// </remarks>
        private static bool TryCloseBackToBackGaps(List<GeoFace3> skin, List<GeoFace3> before, Tolerance tolerance, out GeoSolid3 closed)
        {
            closed = null;

            if (!new GeoSolid3(before).ClosesByCorners(tolerance)
                || !LoopAssembly.TryCloseSlivers(skin, tolerance, LoopAssembly.ForPieces(tolerance), out List<GeoFace3> slivers)
                || slivers.Count == 0)
            {
                return false;
            }

            var faces = new List<GeoFace3>(skin);
            faces.AddRange(slivers);
            closed = new GeoSolid3(faces);

            return closed.ClosesByCorners(tolerance);
        }

        /// <summary>
        /// Drops each face together with the first later face that is it turned over, vertex for vertex: the two
        /// sides of a wall between two cells that were both kept.
        /// </summary>
        /// <remarks>
        /// Two such faces have the same corners, so the corners of their boxes lie within the tolerance of each
        /// other. The faces are filed by the low corner of their box, and each is compared only with the later
        /// faces filed near its own — in the order, and by the test, that comparing it with every later face would
        /// use, so the same pairs are dropped without comparing every face with every other.
        /// </remarks>
        internal static List<GeoFace3> DropMatchedPairs(List<GeoFace3> faces, Tolerance tolerance)
            => DropMatchedPairs(faces, null, tolerance, out _);

        /// <summary>
        /// Drops matched pairs as <see cref="DropMatchedPairs(List{GeoFace3}, Tolerance)"/> does, giving the owners of the
        /// faces kept, where the owners of those given are known.
        /// </summary>
        private static List<GeoFace3> DropMatchedPairs(List<GeoFace3> faces, List<int> owners, Tolerance tolerance, out List<int> keptOwners)
        {
            int count = faces.Count;
            bool[] dropped = new bool[count];
            var boxes = new GeoAabb3[count];
            var corners = new PointGrid(tolerance);

            for (int j = 0; j < count; j++)
            {
                boxes[j] = faces[j].GetAabb();
                corners.Add(boxes[j].Min, j);
            }

            var near = new List<int>();

            for (int i = 0; i < count; i++)
            {
                if (dropped[i])
                {
                    continue;
                }

                corners.Near(boxes[i].Min, near);

                foreach (int j in near)
                {
                    if (j <= i || dropped[j] || faces[j].Boundary.VertexCount != faces[i].Boundary.VertexCount)
                    {
                        continue;
                    }

                    if (faces[i].Boundary.IsEqualTo(faces[j].Boundary.Flip(), tolerance))
                    {
                        dropped[i] = true;
                        dropped[j] = true;
                        break;
                    }
                }
            }

            var kept = new List<GeoFace3>(count);
            keptOwners = owners == null ? null : new List<int>(count);

            for (int i = 0; i < count; i++)
            {
                if (!dropped[i])
                {
                    kept.Add(faces[i]);
                    keptOwners?.Add(owners[i]);
                }
            }

            return kept;
        }

        /// <summary>
        /// Takes from the faces still lying back to back in one plane the area they share.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The two copies of a face between kept cells need not match vertex for vertex. A cut that reaches the
        /// cell on one side and not the one on the other leaves one copy in two pieces, or with a point along its
        /// edge that the other copy lacks, and the exact match lets both through. They would stand inside the
        /// body as a sheet of no thickness: the volume never notices, since the two cancel, but everything that
        /// reads the boundary does — a point a millimetre from the sheet measured a millimetre to the boundary,
        /// the surface mesh carried the sheet, and splitting the body into pieces failed on it.
        /// </para>
        /// <para>
        /// Whatever two faces lying back to back share is inside the body however it was cut, so it goes from
        /// both, and what is left of either is boundary.
        /// </para>
        /// <para>
        /// Two faces lie back to back when either lies in the plane of the other, and each is cut in its own plane,
        /// the other laid out in it (<see cref="SubtractLaidOut"/>): a small face a hair out of a long one's plane
        /// lies in it, but the long one's far end stands off the small one's by more than the tolerance.
        /// </para>
        /// <para>
        /// What a face shares goes with one face lying against it, the nearest, and not with each. A cell thinner than the
        /// tolerance lies against its neighbour on one side and against its own far side on the other, both within the
        /// tolerance of it: the thin end of a wedge a cutter's face left on a slab, where the face crossed the slab's own
        /// at 5E-5 rad, lay against the slab beyond the cutter's face, in that face's plane, and against the slab's wall,
        /// a few hundredths off it. Taken from each, the wedge's face took the wall with it as well as the slab's face against it, and
        /// the slab was left open by a wall 750 long and 400 high. A face lying against two that overlap each other is
        /// paired with the nearer, the one in its own plane where there is one, and a pair is cancelled only where each
        /// takes the other.
        /// </para>
        /// </remarks>
        internal static List<GeoFace3> CancelBackToBack(List<GeoFace3> faces, Tolerance tolerance)
            => CancelBackToBack(faces, null, tolerance);

        /// <summary>
        /// Takes from the faces still lying back to back in one plane the area they share, as
        /// <see cref="CancelBackToBack(List{GeoFace3}, Tolerance)"/> does, but for two faces of one closed piece.
        /// </summary>
        /// <param name="faces">The faces.</param>
        /// <param name="owners">For each face, the cell or the body whole it came from; null where that is not known.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// A cell is closed, and two of its faces facing apart within the tolerance of each other are its two sides where it
        /// is thinner than that, not a wall inside the result: taken from each other, they leave the cell's edges round the
        /// hole. A block whose low step's top was out of flat by 0.0012, read as triangles, less a box whose top lay along
        /// it, was cut by the box's top into cells a hair thick between that plane and the triangles, and each such cell's
        /// top and bottom were taken for back to back with each other, 37 000 square millimetres more of the top than of
        /// what lay under it: the difference came out open.
        /// </remarks>
        internal static List<GeoFace3> CancelBackToBack(List<GeoFace3> faces, List<int> owners, Tolerance tolerance)
        {
            int count = faces.Count;
            var normals = new GeoVector3[count];
            var boxes = new GeoAabb3[count];
            var planes = new GeoPlane3[count];
            var order = new int[count];
            var lows = new double[count];

            for (int i = 0; i < count; i++)
            {
                normals[i] = faces[i].Boundary.Normal;
                boxes[i] = faces[i].GetAabb();
                planes[i] = faces[i].GetPlane();
            }

            double speck = tolerance.EqualPoint * tolerance.EqualPoint;

            // Two faces lying against each other have boxes that meet, so they overlap along every axis: sorted by where
            // their boxes start along one, each face need only be tried against those starting before its box ends. The
            // axis is the one that leaves the fewest to try, since along X the long sides of a column standing along it
            // are each tried against all the others; the pairs that meet are the same along any. The pairs found are then
            // taken in the order trying every pair would have met them, since the order a face's partners are listed in is
            // the order they are taken away from it.
            double reach = 2.0 * tolerance.EqualPoint;
            int axis = SweepAxis(boxes, order, lows, reach);
            List<(int, int)> pairs = null;

            for (int a = 0; a < count; a++)
            {
                int first = order[a];

                for (int b = a + 1; b < count && lows[b] <= Along(boxes[first].Max, axis) + reach; b++)
                {
                    int i = Math.Min(first, order[b]), j = Math.Max(first, order[b]);

                    if (normals[i].DotProduct(normals[j]) >= 0.0 || !boxes[i].CollidesWith(boxes[j], tolerance)
                        || owners != null && owners[i] == owners[j])
                    {
                        continue;
                    }

                    // Asked both ways round: a small face a hair out of a long one's plane lies in it, while the long
                    // one's far end stands off the small one's, and asked one way the pair was found or missed by which
                    // of the two came first. What they share is measured in the plane of the one the other lies in, unless
                    // they lie apart in it, when they share nothing.
                    bool jInI = LiesIn(planes[i], faces[j], tolerance);

                    if (!jInI && !LiesIn(planes[j], faces[i], tolerance) || ApartInPlane(faces[i], faces[j], tolerance)
                        || AreaOf(jInI ? Intersect(faces[i], faces[j], tolerance) : Intersect(faces[j], faces[i], tolerance)) <= speck)
                    {
                        continue;
                    }

                    (pairs = pairs ?? new List<(int, int)>()).Add((i, j));
                }
            }

            if (pairs == null)
            {
                return faces;
            }

            pairs.Sort();
            var against = new List<int>[count];

            foreach ((int i, int j) in pairs)
            {
                (against[i] = against[i] ?? new List<int>()).Add(j);
                (against[j] = against[j] ?? new List<int>()).Add(i);
            }

            PairNearestOnly(faces, planes, against, tolerance);

            var kept = new List<GeoFace3>(count);

            for (int i = 0; i < count; i++)
            {
                if (against[i] == null)
                {
                    kept.Add(faces[i]);
                    continue;
                }

                var left = new List<GeoFace3> { faces[i] };

                foreach (int j in against[i])
                {
                    var rest = new List<GeoFace3>();

                    foreach (GeoFace3 piece in left)
                    {
                        rest.AddRange(SubtractLaidOut(piece, faces[j], tolerance));
                    }

                    left = rest;
                }

                foreach (GeoFace3 piece in left)
                {
                    if (piece.Area > speck)
                    {
                        kept.Add(piece);
                    }
                }
            }

            return kept;
        }

        /// <summary>
        /// Keeps of the faces lying back to back with each face those it shares area with first: where two of them overlap
        /// each other, the nearer, and a pair only where each keeps the other; see <see cref="CancelBackToBack(List{GeoFace3}, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The faces against one face that overlap no other against it, as the two pieces a cut left of one copy, are all
        /// kept, in the order they came, so that a face is cut as it was.
        /// </remarks>
        private static void PairNearestOnly(List<GeoFace3> faces, GeoPlane3[] planes, List<int>[] against, Tolerance tolerance)
        {
            int count = faces.Count;
            var keeps = new HashSet<int>[count];
            bool any = false;
            double speck = tolerance.EqualPoint * tolerance.EqualPoint;

            for (int i = 0; i < count; i++)
            {
                if (against[i] == null || against[i].Count < 2)
                {
                    continue;
                }

                // Nearest first: how far the two planes stand apart over the two faces, a face's middle from the other's
                // plane each way round; ties as they came.
                GeoPoint3 middle = faces[i].Centroid;
                List<int> partners = against[i];
                var apart = new double[partners.Count];
                var order = new int[partners.Count];

                for (int k = 0; k < partners.Count; k++)
                {
                    int j = partners[k];
                    apart[k] = Math.Abs(planes[i].SignedDistanceTo(faces[j].Centroid)) + Math.Abs(planes[j].SignedDistanceTo(middle));
                    order[k] = k;
                }

                Array.Sort(order, (x, y) => apart[x] != apart[y] ? apart[x].CompareTo(apart[y]) : x.CompareTo(y));

                var taken = new List<int>();

                foreach (int k in order)
                {
                    int j = partners[k];
                    bool overlaps = false;

                    foreach (int t in taken)
                    {
                        // Two lying apart in the plane share nothing.
                        if (!ApartInPlane(faces[t], faces[j], tolerance)
                            && (LiesIn(planes[t], faces[j], tolerance) && AreaOf(Intersect(faces[t], faces[j], tolerance)) > speck
                            || LiesIn(planes[j], faces[t], tolerance) && AreaOf(Intersect(faces[j], faces[t], tolerance)) > speck))
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (!overlaps)
                    {
                        taken.Add(j);
                    }
                }

                if (taken.Count < against[i].Count)
                {
                    keeps[i] = new HashSet<int>(taken);
                    any = true;
                }
            }

            if (!any)
            {
                return;
            }

            // A pair stands only where each face keeps the other, in the order the faces came.
            for (int i = 0; i < count; i++)
            {
                if (against[i] == null)
                {
                    continue;
                }

                var paired = new List<int>(against[i].Count);

                foreach (int j in against[i])
                {
                    if ((keeps[i] == null || keeps[i].Contains(j)) && (keeps[j] == null || keeps[j].Contains(i)))
                    {
                        paired.Add(j);
                    }
                }

                against[i] = paired.Count == 0 ? null : paired;
            }
        }

        /// <summary>
        /// Puts boxes in order of where they start along the axis on which the fewest pairs start before the other ends,
        /// X first where two axes tie.
        /// </summary>
        /// <param name="boxes">The boxes.</param>
        /// <param name="order">Filled with the boxes' numbers in that order.</param>
        /// <param name="lows">Filled with where each starts along the axis, in that order.</param>
        /// <param name="reach">How far past a box's end another may start and still be tried.</param>
        /// <returns>The axis: 0 for X, 1 for Y, 2 for Z.</returns>
        private static int SweepAxis(GeoAabb3[] boxes, int[] order, double[] lows, double reach)
        {
            int count = boxes.Length;
            int best = 0;
            long fewest = long.MaxValue;

            for (int axis = 0; axis < 3; axis++)
            {
                Order(boxes, order, lows, axis);
                long tried = 0;

                for (int a = 0; a < count; a++)
                {
                    double end = Along(boxes[order[a]].Max, axis) + reach;
                    int low = a + 1;
                    int high = count;

                    // The first after this one that starts past its end.
                    while (low < high)
                    {
                        int middle = low + (high - low) / 2;

                        if (lows[middle] <= end)
                        {
                            low = middle + 1;
                        }
                        else
                        {
                            high = middle;
                        }
                    }

                    tried += low - a - 1;
                }

                if (tried < fewest)
                {
                    fewest = tried;
                    best = axis;
                }
            }

            Order(boxes, order, lows, best);
            return best;
        }

        private static void Order(GeoAabb3[] boxes, int[] order, double[] lows, int axis)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                order[i] = i;
                lows[i] = Along(boxes[i].Min, axis);
            }

            Array.Sort(lows, order);
        }

        private static double Along(GeoPoint3 point, int axis) => axis == 0 ? point.X : axis == 1 ? point.Y : point.Z;

        /// <summary>
        /// Determines whether two faces lying in about one plane stand further apart in it than four times the tolerance,
        /// read across the sides of either: what they share then has no area, and need not be worked out.
        /// </summary>
        /// <remarks>
        /// Each side of a face's outline, turned square to it in the face's plane, is a way to read the two across, and
        /// two outlines whose corners do not overlap that way are apart; holes lie inside their outlines. Of the 7 880 pairs
        /// of faces in one plane laid out and shared when a plate's twelve blind holes were cut at 0.01, 1 840 lay apart,
        /// and were laid out only to find nothing there.
        /// </remarks>
        private static bool ApartInPlane(GeoFace3 one, GeoFace3 other, Tolerance tolerance)
        {
            double largest = 0.0;

            foreach (GeoPoint3 point in one.Boundary.Vertices)
            {
                largest = Math.Max(largest, Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z))));
            }

            foreach (GeoPoint3 point in other.Boundary.Vertices)
            {
                largest = Math.Max(largest, Math.Max(Math.Abs(point.X), Math.Max(Math.Abs(point.Y), Math.Abs(point.Z))));
            }

            double margin = 4.0 * tolerance.EqualPoint + 1E-9 * (1.0 + largest);

            return Parted(one, other, margin) || Parted(other, one, margin);
        }

        /// <summary>
        /// Determines whether the corners of two outlines stand further apart than a margin across a side of the first.
        /// </summary>
        private static bool Parted(GeoFace3 by, GeoFace3 other, double margin)
        {
            IReadOnlyList<GeoPoint3> outline = by.Boundary.Vertices;
            IReadOnlyList<GeoPoint3> corners = other.Boundary.Vertices;
            GeoVector3 normal = by.Boundary.Normal;

            for (int k = 0; k < outline.Count; k++)
            {
                GeoVector3 across = outline[k].GetVectorTo(outline[(k + 1) % outline.Count]).CrossProduct(normal);
                double length = across.Length;

                if (!(length > 0.0))
                {
                    continue;
                }

                across = across.Multiply(1.0 / length);
                Span(outline, across, out double low, out double high);
                Span(corners, across, out double otherLow, out double otherHigh);

                if (otherLow > high + margin || low > otherHigh + margin)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Span(IReadOnlyList<GeoPoint3> points, GeoVector3 along, out double low, out double high)
        {
            low = double.PositiveInfinity;
            high = double.NegativeInfinity;

            foreach (GeoPoint3 point in points)
            {
                double at = along.X * point.X + along.Y * point.Y + along.Z * point.Z;

                low = Math.Min(low, at);
                high = Math.Max(high, at);
            }
        }

        private static double AreaOf(GeoFace3[] faces)
        {
            double area = 0.0;

            foreach (GeoFace3 face in faces)
            {
                area += face.Area;
            }

            return area;
        }

        #endregion
    }
}
