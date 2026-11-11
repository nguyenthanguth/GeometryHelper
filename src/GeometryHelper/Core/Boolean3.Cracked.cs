using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Booleans of bodies that close only within the tolerance, worked out again on the bodies welded where they are not
    /// worked out as they came.
    /// </summary>
    /// <remarks>
    /// A body whose faces meet on copies of an edge a hair apart, a crack, closes within the tolerance and no closer. Where
    /// a plane of the other body crosses such a crack within a few thousandths, the cut leaves a rim gap at the plane wider
    /// than it bridges, the cell stays whole, and the boolean can be judged no further: a cracked box dipping 0.088 into an
    /// arm of a cracked U, the cracks 1.7E-5 wide and slanting 0.004 either side of the planes of the other, less the U, was
    /// not worked out. So where a boolean is not worked out, each body that closes within the tolerance and not at the
    /// rounding of its coordinates has its corners welded within the tolerance (see <see cref="Weld3"/>) and its faces in
    /// one plane merged (see <see cref="Merge3.CoplanarFaces(IEnumerable{GeoFace3}, Tolerance)"/>), and the boolean is worked
    /// out once more on what that gives, by the same checks: the box less the U leaves 475.54, as it should. Of 45 000
    /// booleans of such a U and a box, both cracked 1E-5 to 1E-4 wide, at a thousandth, a hundredth and 0.05, 5 788 were not
    /// worked out, and every one is right now. A boolean worked out as the bodies came is as it was, to the bit; so is one
    /// that is still not worked out.
    /// </remarks>
    public static partial class Boolean3
    {
        #region Cracked bodies

        // How many unions, common parts and differences on this thread are being worked out, one inside another; only the
        // outermost is worked out again on the bodies welded.
        [ThreadStatic]
        private static int _operating;

        /// <summary>
        /// Works out a boolean as it comes, and where that is not worked out, once more on the bodies welded closed (see
        /// <see cref="TryWeldCrack"/>), where either closes only within the tolerance; what it gave as it came otherwise.
        /// </summary>
        /// <param name="operation">The boolean, as it is worked out where the bodies stand.</param>
        /// <param name="first">The first body; for a difference, the one material is taken from.</param>
        /// <param name="second">The second body; for a difference, the one taken away.</param>
        /// <param name="result">What the boolean gives.</param>
        /// <param name="tolerance">The tolerance asked.</param>
        /// <param name="outcome">How it came out.</param>
        /// <param name="open">What came out open, for a closing to try.</param>
        private static bool WeldedWhereRefused(
            Operation operation,
            GeoSolid3 first,
            GeoSolid3 second,
            out GeoSolid3 result,
            Tolerance tolerance,
            out BooleanOutcome outcome,
            out GeoSolid3 open
        )
        {
            bool made = Operate(operation, first, second, out result, tolerance, out outcome, out open);

            // Only the outermost boolean is worked out again: one worked out inside another, as the check of it or on the
            // material of its openings, gives what it gave, and the one round it judges that as it did.
            if (made || outcome != BooleanOutcome.NotWorkedOut || _operating > 0)
            {
                return made;
            }

            bool firstWelded = TryWeldCrack(first, tolerance, out GeoSolid3 a);
            bool secondWelded = TryWeldCrack(second, tolerance, out GeoSolid3 b);

            if (!firstWelded && !secondWelded)
            {
                return made;
            }

            if (
                !Operate(
                    operation,
                    a,
                    b,
                    out GeoSolid3 again,
                    tolerance,
                    out BooleanOutcome againOutcome,
                    out GeoSolid3 againOpen
                )
                && againOutcome == BooleanOutcome.NotWorkedOut
            )
            {
                return made;
            }

            GeometryHelperLog.Debug(
                "Boolean3: not worked out as the bodies came, it is worked out on a body closing only within the tolerance welded closed."
            );
            result = again;
            outcome = againOutcome;
            open = againOpen;
            return againOutcome == BooleanOutcome.Made;
        }

        /// <summary>
        /// Works out a boolean, counted as worked out on this thread while it is.
        /// </summary>
        private static bool Operate(
            Operation operation,
            GeoSolid3 first,
            GeoSolid3 second,
            out GeoSolid3 result,
            Tolerance tolerance,
            out BooleanOutcome outcome,
            out GeoSolid3 open
        )
        {
            _operating++;

            try
            {
                return operation(first, second, out result, tolerance, out outcome, out open);
            }
            finally
            {
                _operating--;
            }
        }

        /// <summary>
        /// Gets a body that closes within the tolerance, as <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, and not at
        /// the rounding of its coordinates, a trillionth of their size, with its corners within the tolerance of each other
        /// welded into one (see <see cref="Weld3.TryWeld(GeoSolid3, Tolerance, Tolerance, out GeoSolid3)"/>), and its faces
        /// lying in one plane within the tolerance merged where that leaves it closed and holding its volume within the
        /// tolerance times its area; its openings as they are. false where the body closes so, does not close within the
        /// tolerance, or the weld does not close it.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="tolerance">The tolerance asked.</param>
        /// <param name="welded">The body welded; the body as it is where the method returns false.</param>
        private static bool TryWeldCrack(GeoSolid3 solid, Tolerance tolerance, out GeoSolid3 welded)
        {
            welded = solid;
            Tolerance work = ForWork(tolerance);
            double rounding = Splition3.Rounding(solid);

            if (!(work.EqualPoint > rounding))
            {
                return false;
            }

            var fine = new Tolerance(rounding, work.EqualVector, work.EqualAngleRad, rounding);

            try
            {
                if (
                    solid.ClosesByCorners(fine)
                    || !Shells3.Closes(solid, work)
                    || !Weld3.TryWeld(solid, work, fine, out GeoSolid3 closed)
                )
                {
                    return false;
                }

                welded = closed;
                var merged = new GeoSolid3(Merge3.CoplanarFaces(closed.Faces, work), closed.Openings);

                if (
                    merged.Faces.Count >= 4
                    && merged.ClosesByCorners(fine)
                    && Shells3.Closes(merged, work)
                    && Math.Abs(merged.GetSignedVolume() - solid.GetSignedVolume())
                        <= work.EqualPoint * solid.GrossSurfaceArea
                )
                {
                    welded = merged;
                }

                return true;
            }
            catch (Exception exception) when (IsUnworkable(exception))
            {
                welded = solid;
                return false;
            }
        }

        #endregion
    }
}
