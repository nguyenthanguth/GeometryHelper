using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Cutting a body's openings into it, so that its faces are the surface of its material.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="GeoSolid3"/> keeps its openings as whole bodies subtracted from it, not as holes cut in
    /// its faces: a plate with a bolt hole has a top face that is a whole square, and a separate box where the
    /// hole is. That is the right way to hold a model read from Tekla or IFC, and the wrong thing to hand to
    /// anything that reads the faces as the boundary of the material — a pin through the hole crosses that
    /// whole square and looks like it touches the plate.
    /// </para>
    /// <para>
    /// This turns such a body into one with no openings, whose faces are exactly where the material ends: the
    /// outer faces with the parts over each opening taken away, and the walls of each opening where they run
    /// through the body. It is the boolean machinery with one operand — split the body into cells, keep the
    /// cells that are material, glue them — and <see cref="Containment3"/> already knows which cells are
    /// material, nested openings included.
    /// </para>
    /// <para>
    /// <b>Each opening cuts only the cells near it.</b> The knives are the face planes of an opening, and a
    /// plane is infinite: slicing the whole body by every one of them turns a plate with twenty bolt holes into
    /// a grid of a thousand cells. A cell whose box does not meet an opening's box is wholly outside that
    /// opening already, so it is left alone, and the work grows with the number of openings rather than with
    /// its square.
    /// </para>
    /// </remarks>
    public static partial class Boolean3
    {
        #region Cutting the openings in

        /// <summary>
        /// Turns a body with openings into one without, whose faces are the surface of its material.
        /// </summary>
        public static bool TryCutOpenings(GeoSolid3 solid, out GeoSolid3 material) => TryCutOpenings(solid, out material, Tolerance.Global);

        /// <summary>
        /// Turns a body with openings into one without, whose faces are the surface of its material, within a
        /// tolerance.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="material">
        /// The body with every opening cut into it, or the body itself when it has none; null when the method
        /// returns false.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when some material is left; false when the openings take all of it, which is an outcome and not
        /// an error, as it is for the booleans, or when the cut cannot be worked out, which is logged.
        /// </returns>
        /// <remarks>
        /// The material of a closed body with closed openings is closed as <see cref="GeoSolid3.Validate(Tolerance)"/> reads
        /// it, as a boolean's is, or not given. Where cutting the openings in at once leaves it open, it is welded within the
        /// tolerance, and where that does not close it the openings are taken out one at a time by a difference, which tries
        /// more ways of cutting and keeps only what closes; where one will not come out so, the method returns false. A plate
        /// 300 by 200 by 12 with two blind holes ending 0.015 under its top, turned and far from the origin, came out of the
        /// cut within a hundredth open by four edges 0.015 long; the holes are taken out one at a time, and it closes, holding
        /// 711 120.61 as <see cref="GeoSolid3.GetVolume(Tolerance)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public static bool TryCutOpenings(GeoSolid3 solid, out GeoSolid3 material, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return TryCutOpenings(solid, solid.Openings, out material, tolerance);
        }

        /// <summary>
        /// Cuts some of a body's openings into it and leaves the rest out of the answer.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when some material is left; otherwise, or when the cut cannot be worked out, which is logged, false.</returns>
        /// <remarks>
        /// For a question asked near a probe, only the openings whose box meets the probe's box can change the
        /// answer, so there is no reason to pay for the rest. The caller is the one who knows which those are.
        /// </remarks>
        internal static bool TryCutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance)
        {
            if (TryCutOpenings(solid, openings, out material, tolerance, out Exception failure))
            {
                return true;
            }

            return failure != null && Unworkable("cut of openings", failure, out material);
        }

        /// <summary>
        /// Cuts some of a body's openings into it, handing back a cut that cannot be worked out rather than warning of it.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out, or why it would not close; null otherwise.</param>
        /// <returns>
        /// true when some material is left, closed as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it where the body and
        /// the openings are; false when the openings take all of it, or the cut cannot be worked out so.
        /// </returns>
        internal static bool TryCutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, out Exception failure)
            => TryCutOpenings(solid, openings, out material, tolerance, out failure, out _);

        /// <summary>
        /// Cuts some of a body's openings into it as <see cref="TryCutOpenings(GeoSolid3, IReadOnlyList{GeoSolid3}, out GeoSolid3, Tolerance, out Exception)"/>
        /// does, handing back too what cutting them in at once gave where no closed material could be had.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out, or why it would not close; null otherwise.</param>
        /// <param name="open">What cutting the openings in at once gave, open, where it was left for no closed material; null otherwise.</param>
        private static bool TryCutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, out Exception failure, out GeoSolid3 open)
        {
            open = null;

            if (!TryCutOpeningsAtOnce(solid, openings, out material, tolerance, out failure))
            {
                return false;
            }

            GeoSolid3 atOnce = material;

            try
            {
                if (ClosesOrTakesOut(solid, openings, ref material, tolerance, out failure))
                {
                    return true;
                }
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                failure = exception;
                material = null;
            }

            if (failure != null)
            {
                open = atOnce;
            }

            return false;
        }

        /// <summary>
        /// Gets the material a query or a writer reads of a body for some of its openings: the material
        /// <see cref="TryCutOpenings(GeoSolid3, IReadOnlyList{GeoSolid3}, out GeoSolid3, Tolerance, out Exception)"/> gives, and
        /// where no closed material can be had, what cutting the openings in at once gave, open, as it was read before, with a
        /// warning.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The material; null where the openings take all of it, or the cut cannot be worked out at all, which is logged.</returns>
        internal static GeoSolid3 MaterialToRead(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, Tolerance tolerance)
        {
            if (TryCutOpenings(solid, openings, out GeoSolid3 material, tolerance, out Exception failure, out GeoSolid3 open))
            {
                return material;
            }

            if (open != null)
            {
                GeometryHelperLog.Warn("Boolean3: the openings could not be cut in closed; the material cut in at once, open, is read instead.", failure);
                return open;
            }

            if (failure != null)
            {
                Unworkable("cut of openings", failure, out _);
            }

            return null;
        }

        /// <summary>
        /// Cuts every opening of a body into it at once, as a boolean reads its operands, whether what comes of it closes or
        /// not; a cut that cannot be worked out is logged.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="material">The body with its openings cut in, or the body itself when it has none; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when some material is left; otherwise, or when the cut cannot be worked out, false.</returns>
        /// <remarks>
        /// A boolean closes what it gives itself, and cuts both bodies by every plane of both where cutting one does not, so
        /// it takes the cut as it comes: handed a plate whose twelve openings the tolerance inside its faces were taken out
        /// one at a time, the union of the plate and a box at its corner went that way and came out wound two ways,
        /// 718 137.69 where it holds 722 622.74.
        /// </remarks>
        internal static bool TryCutOpeningsAtOnce(GeoSolid3 solid, out GeoSolid3 material, Tolerance tolerance)
        {
            if (TryCutOpeningsAtOnce(solid, solid.Openings, out material, tolerance, out Exception failure))
            {
                return true;
            }

            return failure != null && Unworkable("cut of openings", failure, out material);
        }

        /// <summary>
        /// Cuts some of a body's openings into it at once, whether what comes of it closes or not, handing back a cut that
        /// cannot be worked out rather than warning of it.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings to cut; the others are not carried over.</param>
        /// <param name="material">The body with those openings cut into it; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out; null otherwise.</param>
        /// <returns>true when some material is left; false when the openings take all of it, or the cut cannot be worked out.</returns>
        internal static bool TryCutOpeningsAtOnce(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            failure = null;

            if (openings.Count == 0)
            {
                material = solid.Openings.Count == 0 ? solid : new GeoSolid3(solid.Faces);
                return true;
            }

            try
            {
                return CutOpenings(solid, openings, out material, tolerance);
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                failure = exception;
                material = null;
                return false;
            }
        }

        /// <summary>
        /// Takes one opening out of a body by a difference, handing back a cut that cannot be worked out rather than
        /// warning of it.
        /// </summary>
        /// <param name="body">The body, without openings.</param>
        /// <param name="opening">The opening; the material inside it, its own openings, stays.</param>
        /// <param name="material">The body less the opening; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">What was thrown where the cut could not be worked out; null otherwise.</param>
        /// <returns>true when some material is left; false when the opening takes all of it, or the cut cannot be worked out.</returns>
        /// <remarks>
        /// The way back where cutting every opening in at once cannot be worked out or leaves the material open: a
        /// difference tries more ways of cutting and keeps the one that closes, and an opening that will not come out
        /// keeps none of the others in.
        /// </remarks>
        internal static bool TryTakeOut(GeoSolid3 body, GeoSolid3 opening, out GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            failure = null;

            try
            {
                if (TakeAway(body, opening, out material, tolerance, out string unworkable))
                {
                    return true;
                }

                if (unworkable != null)
                {
                    failure = new InvalidOperationException($"The opening could not be taken out: cutting it {unworkable}.");
                }

                return false;
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                failure = exception;
                material = null;
                return false;
            }
        }

        private static bool CutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance)
        {
            // The body carrying only the openings being cut, so that a cell is judged against exactly those, and
            // both as the cut works on them, wound outwards and flat; see ForWork and FlatForWork.
            tolerance = ForWork(tolerance);
            GeoSolid3 owner = FlatForWork(new GeoSolid3(solid.Faces, openings), tolerance);
            GeoSolid3 gross = new GeoSolid3(owner.Faces);
            openings = owner.Openings;

            List<GeoSolid3> cells = new List<GeoSolid3> { gross };

            foreach (GeoSolid3 opening in openings)
            {
                GeoAabb3 reach = opening.GetAabb();

                foreach (GeoPlane3 plane in Splition3.CollectFacePlanes(opening, tolerance))
                {
                    List<GeoSolid3> divided = new List<GeoSolid3>(cells.Count + 1);

                    foreach (GeoSolid3 cell in cells)
                    {
                        // A cell out of the opening's reach is wholly outside it, so none of its planes need
                        // cut it: that is what keeps a plate with many holes from becoming a grid. A corner is read
                        // as on a plane within what rounding adds to the tolerance; see Splition3.OnPlaneWithinRounding.
                        if (cell.GetAabb().CollidesWith(reach, tolerance)
                            && Splition3.TrySplitCell(cell, plane, out GeoSolid3 above, out GeoSolid3 below, Splition3.OnPlaneWithinRounding(cell, tolerance)))
                        {
                            divided.Add(above);
                            divided.Add(below);
                        }
                        else
                        {
                            divided.Add(cell);
                        }
                    }

                    cells = divided;
                }
            }

            var owners = new List<int>();
            List<GeoFace3> faces = FacesOfCells(OnePieceEach(cells, tolerance), owner, tolerance, owners);
            return TryGlue(faces, owners, tolerance, out material);
        }

        /// <summary>
        /// Takes the material of a body with its openings cut in at once where it closes as <see cref="GeoSolid3.Validate(Tolerance)"/>
        /// reads it, or where the body or an opening does not; otherwise welds it within the tolerance, and where that does not
        /// close it takes the openings out of the body one at a time by a difference, as a boolean closes what it gives.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="openings">The openings cut.</param>
        /// <param name="material">What cutting them in at once gave; what is taken, or null where none is.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="failure">Why no closed material could be had; null otherwise.</param>
        /// <returns>false where the openings take all of the material, or no closed material could be had.</returns>
        /// <remarks>
        /// The weld is taken only where it moves the volume no further than moving the corners it moves by the tolerance can
        /// (<see cref="Weld3.SealedAsValidated"/>), and leaves no more than the body and no less than the body less every
        /// opening whole, each within the tolerance times the openings' area.
        /// </remarks>
        private static bool ClosesOrTakesOut(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, ref GeoSolid3 material, Tolerance tolerance, out Exception failure)
        {
            failure = null;
            Tolerance work = ForWork(tolerance);

            if (openings.Count == 0 || Shells3.Closes(material, work) || !Shells3.Closes(solid, work))
            {
                return true;
            }

            double gross = Math.Abs(solid.GrossVolume);
            double taken = 0.0;
            double skin = 0.0;

            foreach (GeoSolid3 opening in openings)
            {
                if (!Shells3.Closes(opening, work))
                {
                    return true;
                }

                taken += Math.Abs(opening.GrossVolume);
                skin += opening.GrossSurfaceArea;
            }

            GeoSolid3 welded = Weld3.SealedAsValidated(material, work, out _);
            double slack = work.EqualPoint * skin + 1E-9 * (gross + taken);
            double volume = welded.GrossVolume;

            if (Shells3.Closes(welded, work) && volume <= gross + slack && volume >= gross - taken - slack)
            {
                ClosedByWeld++;
                material = welded;
                return true;
            }

            GeometryHelperLog.Debug("Boolean3: cutting the openings in at once left the material open to Validate, and a weld did not close it; they are taken out one at a time.");
            TakenOutOneAtATime++;
            GeoSolid3 remaining = new GeoSolid3(solid.Faces);

            foreach (GeoSolid3 opening in openings)
            {
                if (TryTakeOut(remaining, opening, out GeoSolid3 left, tolerance, out Exception failed))
                {
                    remaining = left;
                }
                else
                {
                    // Not coming out closed; or taking all that is left, where the cut at once left material, which the two
                    // cannot both be, and is not believed.
                    failure = failed != null
                        ? new InvalidOperationException("Cutting the openings in left the material open, and an opening could not be taken out of it closed.", failed)
                        : new InvalidOperationException("Cutting the openings in left the material open, and taken out one at a time an opening took all that was left of it.");
                    material = null;
                    return false;
                }
            }

            material = remaining;
            return true;
        }

        /// <summary>
        /// A test hook: how many cuts of openings on this thread came out open and were welded closed, so that a test can say
        /// which way a cut went. Nothing in the library reads it.
        /// </summary>
        [ThreadStatic]
        internal static int ClosedByWeld;

        /// <summary>
        /// A test hook: how many cuts of openings on this thread came out open, would not weld closed, and had their openings
        /// taken out one at a time, so that a test can say which way a cut went. Nothing in the library reads it.
        /// </summary>
        [ThreadStatic]
        internal static int TakenOutOneAtATime;

        #endregion
    }
}
