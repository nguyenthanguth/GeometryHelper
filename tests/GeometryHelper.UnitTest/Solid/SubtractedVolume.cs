using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest
{
    /// <summary>
    /// The volume of a body's material worked out by another road than the library's: one difference per opening, in
    /// turn, each against what the ones before it left.
    /// </summary>
    /// <remarks>
    /// <see cref="GeoSolid3.GetVolume(Tolerance)"/> cuts every opening in at once, as the cells and the cut bodies are
    /// made, so holding one to the other would hold a cut to itself. This was GeoSolid3.GetNetVolume, kept as it was for
    /// the tests that hold a cut to it: an opening that cannot be taken out has its whole volume taken off.
    /// </remarks>
    internal static class SubtractedVolume
    {
        internal static double Of(GeoSolid3 body, Tolerance tolerance)
        {
            if (body.Openings.Count == 0)
            {
                return body.GrossVolume;
            }

            GeoSolid3 remaining = new GeoSolid3(body.Faces);
            double deducted = 0.0;

            foreach (GeoSolid3 opening in body.Openings)
            {
                if (Boolean3.TrySubtract(remaining, opening, out GeoSolid3 cut, tolerance))
                {
                    remaining = cut;
                    continue;
                }

                deducted += opening.GrossVolume;
            }

            return Math.Max(0.0, remaining.GrossVolume - deducted);
        }
    }
}
