using GeometryHelper.Geometry;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// A position a label is tried at: where its centre goes, and how much further off its leader its side asks it to
    /// stand than the other side does.
    /// </summary>
    internal readonly struct Candidate
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Candidate"/> struct.
        /// </summary>
        /// <param name="centre">Where the centre of the label goes.</param>
        /// <param name="surplus">How much further off the first row of this side stands than the first row of the other.</param>
        internal Candidate(GeoPoint2 centre, double surplus)
        {
            Centre = centre;
            Surplus = surplus;
        }

        /// <summary>Gets where the centre of the label goes.</summary>
        internal GeoPoint2 Centre { get; }

        /// <summary>
        /// Gets how much further off the leader the first row of this side stands than the first row of the other: the
        /// gap this side asks for beyond the gap of the other. Nought on the side with the smaller gap, and on both
        /// sides when their gaps are the same.
        /// </summary>
        internal double Surplus { get; }
    }
}
