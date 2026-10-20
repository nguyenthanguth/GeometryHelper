using System;

namespace GeometryHelper.Internal.Nurbs
{
    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// Gets how fast the curve has to be going, in drawing units for the whole of its domain, to count as moving: a
        /// trillionth of its size at the speed an even run over the domain would have.
        /// </summary>
        internal double StillSpeed => 1E-12 * Extent * (Degree / (End - Start));

        /// <summary>
        /// Gets the unit direction the curve runs in at a parameter: its derivative, or where that is nought, as at a
        /// cusp or where the first control points coincide, the first of its higher derivatives that is not.
        /// </summary>
        /// <param name="t">The parameter, put inside the domain.</param>
        /// <param name="tangent">Dim numbers to write the direction to.</param>
        /// <returns>false when every derivative to the degree vanishes, which only a curve that stands still does.</returns>
        internal bool TryTangent(double t, double[] tangent)
        {
            double[][] derivatives = Derivatives(t, Degree);
            double rate = Degree / (End - Start);
            double still = 1E-12 * Extent;

            for (int order = 1; order <= Degree; order++)
            {
                still *= rate;
                double square = 0.0;

                for (int k = 0; k < Dim; k++)
                {
                    square += derivatives[order][k] * derivatives[order][k];
                }

                double length = Math.Sqrt(square);

                if (length > still)
                {
                    for (int k = 0; k < Dim; k++)
                    {
                        tangent[k] = derivatives[order][k] / length;
                    }

                    return true;
                }
            }

            return false;
        }
    }
}
