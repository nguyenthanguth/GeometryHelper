using System;

namespace GeometryHelper.Internal.Nurbs
{
    internal sealed partial class NurbsCurve
    {
        /// <summary>
        /// Makes the curve of degree two that is exactly an ellipse in the plane: the circle of nine control points carried
        /// by the map that stretches it to the radii along the axes, which carries a rational conic to a rational conic.
        /// </summary>
        /// <param name="cx">The centre.</param>
        /// <param name="cy">The centre.</param>
        /// <param name="ux">The unit major axis.</param>
        /// <param name="uy">The unit major axis.</param>
        /// <param name="a">The major radius.</param>
        /// <param name="b">The minor radius.</param>
        /// <returns>The curve over [0, 1], from the end of the major axis counter-clockwise, closed.</returns>
        internal static NurbsCurve FromEllipse(double cx, double cy, double ux, double uy, double a, double b)
        {
            double[] unitX = { 1.0, 1.0, 0.0, -1.0, -1.0, -1.0, 0.0, 1.0, 1.0 };
            double[] unitY = { 0.0, 1.0, 1.0, 1.0, 0.0, -1.0, -1.0, -1.0, 0.0 };
            double corner = Math.Sqrt(0.5);
            double[] weights = { 1.0, corner, 1.0, corner, 1.0, corner, 1.0, corner, 1.0 };
            double[] knots = { 0.0, 0.0, 0.0, 0.25, 0.25, 0.5, 0.5, 0.75, 0.75, 1.0, 1.0, 1.0 };
            var points = new double[18];

            for (int i = 0; i < 9; i++)
            {
                points[2 * i] = cx + a * unitX[i] * ux - b * unitY[i] * uy;
                points[2 * i + 1] = cy + a * unitX[i] * uy + b * unitY[i] * ux;
            }

            Check(2, points, 2, knots, weights);
            return new NurbsCurve(2, 2, points, knots, weights);
        }
    }
}
