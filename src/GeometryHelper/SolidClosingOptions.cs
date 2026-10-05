using System;
using System.Globalization;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// How <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/> closes an open body:
    /// within which tolerance it is judged closed, how far apart the corners across a gap may stand and still be made
    /// one, and which holes may be filled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A body is closed with the least change that does it, and nothing is made up where the change is not certain.
    /// Corners standing apart across a gap no wider than <see cref="MaxGap"/> are made one, and a corner standing that near
    /// an edge of another face left open is put on it: neither moves the surface further than the gap, and no face is
    /// folded back onto another. A hole is filled only where it encloses no more than
    /// <see cref="MaxHoleArea"/>: by one face where it is flat, and by the triangles of least area across it where it is
    /// out of flat by no more than <see cref="MaxOffFlat"/>, as <see cref="Fill"/> allows. No hole is filled unless asked:
    /// the largest hole is nought by default.
    /// </para>
    /// <para>
    /// The gap is a question of how the body was made. A boolean cut within a hundredth closes within a hundredth and no
    /// closer: read within a thousandth, the default tolerance, it can be open by copies of an edge up to a hundredth
    /// apart, which a gap of a hundredth reaches. A face left out is another matter, and no weld closes it: a box 100 by
    /// 200 by 300 with its top left out is closed by a face of 20 000 square units, and only where the largest hole is at
    /// least that.
    /// </para>
    /// <para>
    /// The options are immutable, so one instance can be shared between threads and kept as a setting.
    /// </para>
    /// </remarks>
    public sealed class SolidClosingOptions : IEquatable<SolidClosingOptions>
    {
        /// <summary>
        /// Initializes the options.
        /// </summary>
        /// <param name="tolerance">The tolerance the body is judged closed within, as <see cref="GeoSolid3.Validate(Tolerance)"/> judges it.</param>
        /// <param name="maxGap">
        /// How far apart the corners across a gap may stand and still be made one, and how far a corner may stand off an edge
        /// and still be put on it; see <see cref="MaxGap"/>.
        /// </param>
        /// <param name="maxHoleArea">
        /// The most a hole may enclose and still be filled; see <see cref="MaxHoleArea"/>. Nought fills none, and
        /// <see cref="double.PositiveInfinity"/> fills any.
        /// </param>
        /// <param name="maxOffFlat">
        /// How far out of flat a hole may stand and still be filled, by triangles; see <see cref="MaxOffFlat"/>. Nought
        /// fills only the flat ones.
        /// </param>
        /// <param name="fill">Which holes are filled; see <see cref="FillStrategy"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the gap is not a finite number above nought, the largest hole is negative or NaN, the distance out of
        /// flat is negative, NaN or infinite, or the strategy is not one of <see cref="FillStrategy"/>.
        /// </exception>
        public SolidClosingOptions(Tolerance tolerance, double maxGap, double maxHoleArea = 0.0, double maxOffFlat = 0.0, FillStrategy fill = FillStrategy.WhenUnambiguous)
        {
            Guard.Positive(maxGap, nameof(maxGap), "A gap has to be a finite number above nought.");

            // Infinity is the one value no other check lets through, and here it means no limit.
            if (!(maxHoleArea >= 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(maxHoleArea), maxHoleArea, "The largest hole has to be a number, and cannot be negative.");
            }

            Guard.NonNegative(maxOffFlat, nameof(maxOffFlat), "How far out of flat a hole may stand has to be a number, and cannot be negative.");

            if (fill < FillStrategy.None || fill > FillStrategy.MinArea)
            {
                throw new ArgumentOutOfRangeException(nameof(fill), fill, "Unknown way of filling a hole.");
            }

            Tolerance = tolerance;
            MaxGap = maxGap;
            MaxHoleArea = maxHoleArea;
            MaxOffFlat = maxOffFlat;
            Fill = fill;
        }

        /// <summary>
        /// Gets the tolerance the body is judged closed within: the result is valid within it, as
        /// <see cref="GeoSolid3.Validate(Tolerance)"/> reads it, and corners within it of each other are one.
        /// </summary>
        public Tolerance Tolerance { get; }

        /// <summary>
        /// Gets how far apart the corners across a gap may stand and still be made one, and how far a corner may stand off an
        /// edge and still be put on it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The corners of the edges left open are welded within the point tolerance first, then within twice it, four times,
        /// and so on while below this, and within this last, the first reach that closes the body taken: a corner moves no
        /// further than it has to. The corners of edges the body closes along stay where they are, and two corners of one face
        /// are never made one, so that a slot cut thinner than this stays a slot. Only where no reach of welds alone closes
        /// the body is a corner standing within the reach of an edge left open of another face, between its ends, put on that
        /// edge, through the same reaches again: where the crack between them lies in the plane of the corner's face, the
        /// corner is moved onto the edge within that plane, and otherwise the edge is bent through the corner where it stands.
        /// </para>
        /// <para>
        /// A corner is put within the reach of an edge as the edge was, not of the pieces another corner put on it leaves: a
        /// crack bowed 0.008 off an edge through five corners closes within a gap of 0.008, and not within one of 0.005. A
        /// reach that would leave a ring running out to a corner and straight back, or a face lying back to back with
        /// another, is not taken. What the faces moved sweep of the volume is held to the reach times their area.
        /// </para>
        /// </remarks>
        public double MaxGap { get; }

        /// <summary>
        /// Gets the most a hole may enclose and still be filled; nought fills none, and
        /// <see cref="double.PositiveInfinity"/> any.
        /// </summary>
        /// <remarks>
        /// A flat hole is filled by one face, the loops lying in its plane inside it taken as holes of that face, and its
        /// area, holes taken away, is what is held to this. A hole out of flat is held to it by the area of the triangles
        /// across it. A hole larger is not filled, and the report says <see cref="ClosingFailure.HoleTooLarge"/>.
        /// </remarks>
        public double MaxHoleArea { get; }

        /// <summary>
        /// Gets how far out of flat a hole may stand and still be filled, by the triangles of least area across it; nought
        /// fills only the holes flat within the planar tolerance.
        /// </summary>
        /// <remarks>
        /// A hole further out of flat is not filled, and the report says <see cref="ClosingFailure.HoleOffFlat"/>. A hole
        /// within it can still be filled more than one way, the ways closing different volumes; which is taken, if any, is
        /// <see cref="Fill"/>'s.
        /// </remarks>
        public double MaxOffFlat { get; }

        /// <summary>
        /// Gets which holes are filled: none, those that can be filled one way only, or every one that may be.
        /// </summary>
        public FillStrategy Fill { get; }

        /// <inheritdoc/>
        public bool Equals(SolidClosingOptions other)
        {
            return other != null
                && Tolerance.Equals(other.Tolerance)
                && MaxGap.Equals(other.MaxGap)
                && MaxHoleArea.Equals(other.MaxHoleArea)
                && MaxOffFlat.Equals(other.MaxOffFlat)
                && Fill == other.Fill;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SolidClosingOptions other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Tolerance.GetHashCode();
                hash = hash * 397 ^ MaxGap.GetHashCode();
                hash = hash * 397 ^ MaxHoleArea.GetHashCode();
                hash = hash * 397 ^ MaxOffFlat.GetHashCode();
                hash = hash * 397 ^ (int)Fill;
                return hash;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "(Tolerance: {0}, MaxGap: {1}, MaxHoleArea: {2}, MaxOffFlat: {3}, Fill: {4})",
                Tolerance,
                MaxGap,
                double.IsPositiveInfinity(MaxHoleArea) ? "no limit" : MaxHoleArea.ToString(CultureInfo.InvariantCulture),
                MaxOffFlat,
                Fill);
        }
    }
}
