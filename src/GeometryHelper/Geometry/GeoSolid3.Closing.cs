using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    public sealed partial class GeoSolid3
    {
        #region Closing

        /// <summary>
        /// Closes this body where it is open, with the least change that does it, and says what was changed; see
        /// <see cref="SolidClosingOptions"/>.
        /// </summary>
        /// <param name="closed">The body closed: this body itself where it was valid already; null when the method returns false.</param>
        /// <param name="options">How far apart corners may stand and still be made one, and which holes may be filled.</param>
        /// <param name="report">Each change made, in the order it was made, or why the body could not be closed and where.</param>
        /// <returns>
        /// true when the body comes out valid within the options' tolerance, as <see cref="Validate(Tolerance)"/> reads it:
        /// closed, wound alike and outwards, and enclosing a volume; otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the options are null.</exception>
        /// <remarks>
        /// <para>
        /// Each step is taken only where the ones before it did not close the body. Faces with no area, a face given twice,
        /// the two of a sheet lying back to back inside the body, and a face lying back to back on another with every edge of
        /// it open are dropped, and faces turned over so that each shell is wound alike and each closed shell inside no other
        /// outwards, a shell inside another keeping its winding against it, a block or a cavity. An open edge past a fin
        /// stops the closing. Corners standing apart across a gap are made one within the point tolerance, then twice it, four
        /// times, and so on up to <see cref="SolidClosingOptions.MaxGap"/>, and where that closes nothing, the same again with
        /// corners standing off an edge left open put on it; no reach is taken that runs a ring out to a corner and straight
        /// back, or lays a face back to back with another. What is left open is followed round into loops, and filled where
        /// the options allow: a flat hole by one face on the body's own corners, the loops in its plane inside it the face's
        /// holes, and one a little out of flat by the triangles of least area across it on its own corners. No fill is taken
        /// that lies back to back with a face of the body or crosses one, or a fill taken before it, an edge of either through
        /// the inside of the other, and the way a shell faces is read again once it is closed. What comes of it is checked:
        /// valid within the tolerance, and the volume the welds moved no more than the reach times the area they touched.
        /// </para>
        /// <para>
        /// Nothing is made up. Where closing the body is not certain, as where an open edge runs past a fin, a gap is wider
        /// than the options allow or a hole can be filled more than one way, the method returns false and the report says
        /// why and where. The body is not changed: its openings are carried over as they are, and a shell of it closed
        /// already is left as it was.
        /// </para>
        /// <code>
        /// var options = new SolidClosingOptions(Tolerance.Default, 0.01, maxHoleArea: 50000.0);
        ///
        /// if (part.TryClose(out GeoSolid3 closed, options, out SolidClosing3 report))
        /// {
        ///     double volume = closed.GetVolume(Tolerance.Default);
        /// }
        /// else
        /// {
        ///     Console.WriteLine(report); // SolidClosing3[not closed, HoleTooLarge at (1250, 300, 2700)]
        /// }
        /// </code>
        /// </remarks>
        public bool TryClose(out GeoSolid3 closed, SolidClosingOptions options, out SolidClosing3 report)
            => Closing3.TryClose(this, out closed, options, out report);

        /// <summary>
        /// Closes this body where corners standing apart, or a corner standing off an edge, are no further apart than a gap,
        /// filling no hole.
        /// </summary>
        /// <param name="closed">The body closed: this body itself where it was valid already; null when the method returns false.</param>
        /// <param name="tolerance">The tolerance the body is judged closed within.</param>
        /// <param name="maxGap">How far apart corners may stand and still be made one; see <see cref="SolidClosingOptions.MaxGap"/>.</param>
        /// <returns>true when the body comes out valid within the tolerance; otherwise false.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the gap is not a finite number above nought.</exception>
        /// <remarks>
        /// This is <see cref="TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/> with options that fill no
        /// hole, <see cref="FillStrategy.None"/>, and the report not kept.
        /// </remarks>
        public bool TryClose(out GeoSolid3 closed, Tolerance tolerance, double maxGap)
            => TryClose(out closed, new SolidClosingOptions(tolerance, maxGap, 0.0, 0.0, FillStrategy.None), out _);

        #endregion
    }
}
