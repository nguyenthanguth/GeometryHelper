using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Clash
{
    /// <summary>
    /// A round bar to check for clashes by its centre line and its radius, without building its body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Checked as a body, a bar is the body <see cref="GeoSolid3.Pipe(GeoPolylineArc3, double, double)"/> builds: a
    /// polygon swept along the centre line, a few hundred faces for a bar with two bends, lying a chord tolerance inside
    /// the round bar it stands for, and a boolean for every part it runs into. Checked by its centre line, a bar runs
    /// into a part where the part comes nearer the centre line than the radius: found exactly on the straight runs and
    /// within the chord tolerance on the bends, and measured by how deep the part reaches into the bar
    /// (<see cref="ClashResult.Depth"/>) and how much of the centre line runs inside it
    /// (<see cref="ClashResult.LengthInside"/>) instead of by a volume, with nothing built.
    /// </para>
    /// <para>
    /// The ends are read rounded. A ball rolled along the centre line reaches a radius past each end, where a real bar
    /// ends flat, so right at an end a part up to a radius beyond it is found to clash: the check errs on the side of
    /// reporting.
    /// </para>
    /// </remarks>
    public sealed class ClashBar
    {
        /// <summary>
        /// Initializes a bar from its centre line and its radius, its bends followed to a thousandth of the radius.
        /// </summary>
        /// <param name="centreLine">The centre line, with its bends as arcs.</param>
        /// <param name="radius">The radius of the bar itself.</param>
        /// <exception cref="ArgumentNullException">Thrown when the centre line is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number.</exception>
        public ClashBar(GeoPolylineArc3 centreLine, double radius)
            : this(centreLine, radius, radius * 1E-3)
        {
        }

        /// <summary>
        /// Initializes a bar from its centre line and its radius, its bends followed to a chord tolerance.
        /// </summary>
        /// <param name="centreLine">The centre line, with its bends as arcs.</param>
        /// <param name="radius">The radius of the bar itself.</param>
        /// <param name="chordTolerance">How far the straight pieces a bend is followed by may stray from it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the centre line is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius or the chord tolerance is not a positive number.</exception>
        public ClashBar(GeoPolylineArc3 centreLine, double radius, double chordTolerance)
        {
            CentreLine = centreLine ?? throw new ArgumentNullException(nameof(centreLine));
            Guard.Positive(radius, nameof(radius), "A radius has to be a positive number.");
            Guard.Positive(chordTolerance, nameof(chordTolerance), "A chord tolerance has to be a positive number.");

            Radius = radius;
            ChordTolerance = chordTolerance;

            IReadOnlyList<GeoPoint3> points = centreLine.ToPolyline3(chordTolerance).Vertices;
            var copy = new GeoPoint3[points.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = points[i];
            }

            Points = copy;
            Box = GeoAabb3.FromPoints(copy).Expand(radius);
        }

        /// <summary>
        /// Initializes a straight-run bar from its centre line and its radius.
        /// </summary>
        /// <param name="centreLine">The centre line.</param>
        /// <param name="radius">The radius of the bar itself.</param>
        /// <exception cref="ArgumentNullException">Thrown when the centre line is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius is not a positive number.</exception>
        public ClashBar(GeoPolyline3 centreLine, double radius)
            : this(new GeoPolylineArc3(centreLine ?? throw new ArgumentNullException(nameof(centreLine))), radius)
        {
        }

        /// <summary>
        /// Gets the centre line, with its bends as arcs.
        /// </summary>
        public GeoPolylineArc3 CentreLine { get; }

        /// <summary>
        /// Gets the radius of the bar itself.
        /// </summary>
        public double Radius { get; }

        /// <summary>
        /// Gets how far the straight pieces the bends are followed by may stray from them.
        /// </summary>
        public double ChordTolerance { get; }

        /// <summary>
        /// Gets the centre line with its bends cut into chords, which is what the check walks.
        /// </summary>
        internal GeoPoint3[] Points { get; }

        /// <summary>
        /// Gets the box round the bar: the box of its centre line grown by the radius.
        /// </summary>
        internal GeoAabb3 Box { get; }
    }
}
