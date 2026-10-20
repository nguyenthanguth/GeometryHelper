using System;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// The nodes and weights of the Gauss-Legendre rule of ten points, which integrates a polynomial of degree 19
    /// exactly, worked out once from the roots of the Legendre polynomial.
    /// </summary>
    internal static class GaussLegendre
    {
        /// <summary>
        /// The number of points of the rule.
        /// </summary>
        internal const int Points = 10;

        /// <summary>
        /// The nodes over [-1, 1], in increasing order.
        /// </summary>
        internal static readonly double[] Nodes = new double[Points];

        /// <summary>
        /// The weights of the nodes; they add up to 2.
        /// </summary>
        internal static readonly double[] Weights = new double[Points];

        /// <summary>
        /// Gets the Legendre polynomial of the degree of the rule and its slope at a point, by the three-term recurrence.
        /// </summary>
        private static double Legendre(double x, out double slope)
        {
            double previous = 1.0;
            double current = x;

            for (int n = 2; n <= Points; n++)
            {
                double next = ((2 * n - 1) * x * current - (n - 1) * previous) / n;
                previous = current;
                current = next;
            }

            slope = Points * (x * current - previous) / (x * x - 1.0);

            return current;
        }

        static GaussLegendre()
        {
            // The roots of the Legendre polynomial of the degree of the rule, by Newton's method from the usual first
            // guess, each polished until a step is lost in rounding. The rule is symmetric, so half of them are
            // worked out and the other half mirrored.
            for (int i = 0; i < (Points + 1) / 2; i++)
            {
                double x = Math.Cos(Math.PI * (i + 0.75) / (Points + 0.5));

                for (int step = 0; step < 100; step++)
                {
                    double change = Legendre(x, out double slope) / slope;
                    x -= change;

                    if (Math.Abs(change) <= 1E-17)
                    {
                        break;
                    }
                }

                // The weight is read at the root as it ended up, with the slope of the polynomial there.
                Legendre(x, out double derivative);
                double weight = 2.0 / ((1.0 - x * x) * derivative * derivative);

                Nodes[i] = -x;
                Nodes[Points - 1 - i] = x;
                Weights[i] = weight;
                Weights[Points - 1 - i] = weight;
            }
        }
    }
}
