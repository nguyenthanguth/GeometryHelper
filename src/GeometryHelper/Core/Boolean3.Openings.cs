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
        /// an error, as it is for the booleans.
        /// </returns>
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
        /// <param name="failure">What was thrown where the cut could not be worked out; null otherwise.</param>
        /// <returns>true when some material is left; false when the openings take all of it, or the cut cannot be worked out.</returns>
        internal static bool TryCutOpenings(GeoSolid3 solid, IReadOnlyList<GeoSolid3> openings, out GeoSolid3 material, Tolerance tolerance, out Exception failure)
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
                        // cut it: that is what keeps a plate with many holes from becoming a grid.
                        if (cell.GetAabb().CollidesWith(reach, tolerance)
                            && Splition3.TrySplitCell(cell, plane, out GeoSolid3 above, out GeoSolid3 below, tolerance))
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

        #endregion
    }
}
