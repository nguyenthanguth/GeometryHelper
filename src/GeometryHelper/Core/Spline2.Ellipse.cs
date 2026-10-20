using System;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Core
{
    /// <summary>
    /// An ellipse as a spline's partner: the rim, which is where a quadratic form is nought, so that the places a spline
    /// crosses it are the roots of a polynomial, and which is also a rational spline of four pieces, so that the places
    /// it comes near it are found as for any other curve.
    /// </summary>
    internal sealed class EllipsePartner : QuadricPartner
    {
        private readonly GeoEllipse2 _ellipse;
        private readonly double _scale;

        internal EllipsePartner(GeoEllipse2 ellipse)
            : base(
                ellipse.Center.X,
                ellipse.Center.Y,
                ellipse.MajorAxis.X * ellipse.MajorAxis.X / (ellipse.MajorRadius * ellipse.MajorRadius) + ellipse.MajorAxis.Y * ellipse.MajorAxis.Y / (ellipse.MinorRadius * ellipse.MinorRadius),
                ellipse.MajorAxis.X * ellipse.MajorAxis.Y * (1.0 / (ellipse.MajorRadius * ellipse.MajorRadius) - 1.0 / (ellipse.MinorRadius * ellipse.MinorRadius)),
                ellipse.MajorAxis.Y * ellipse.MajorAxis.Y / (ellipse.MajorRadius * ellipse.MajorRadius) + ellipse.MajorAxis.X * ellipse.MajorAxis.X / (ellipse.MinorRadius * ellipse.MinorRadius),
                1.0)
        {
            _ellipse = ellipse;
            _scale = Math.Max(Math.Abs(ellipse.Center.X), Math.Abs(ellipse.Center.Y)) + ellipse.MajorRadius;
        }

        internal override double Scale => _scale;

        internal override double Extent => 2.0 * _ellipse.MajorRadius;

        internal override double Gap(double px, double py, out double qx, out double qy, out double other)
        {
            GeoPoint2 rim = Ellipse2.GetClosestPointOnBoundary(_ellipse, new GeoPoint2(px, py));
            qx = rim.X;
            qy = rim.Y;
            other = Math.Atan2(rim.Y - Cy, rim.X - Cx);
            double dx = px - qx;
            double dy = py - qy;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal override double Bound(double[] min, double[] max, int at)
        {
            double lx = min[at];

            if (lx > max[at])
            {
                return double.PositiveInfinity;
            }

            GeoVector2 u = _ellipse.MajorAxis;
            double a = _ellipse.MajorRadius;
            double b = _ellipse.MinorRadius;
            double alongLow = double.PositiveInfinity;
            double alongHigh = double.NegativeInfinity;
            double acrossLow = double.PositiveInfinity;
            double acrossHigh = double.NegativeInfinity;

            // The box in the frame where the ellipse is the unit circle: no point of it is nearer the circle than its bound
            // says, and a distance in the plane is at least the smaller radius times the distance in that frame.
            for (int i = 0; i < 4; i++)
            {
                double dx = ((i & 1) == 0 ? lx : max[at]) - Cx;
                double dy = ((i & 2) == 0 ? min[at + 1] : max[at + 1]) - Cy;
                double along = (dx * u.X + dy * u.Y) / a;
                double across = (dy * u.X - dx * u.Y) / b;
                alongLow = Math.Min(alongLow, along);
                alongHigh = Math.Max(alongHigh, along);
                acrossLow = Math.Min(acrossLow, across);
                acrossHigh = Math.Max(acrossHigh, across);
            }

            double gx = alongLow > 0.0 ? alongLow : alongHigh < 0.0 ? -alongHigh : 0.0;
            double gy = acrossLow > 0.0 ? acrossLow : acrossHigh < 0.0 ? -acrossHigh : 0.0;
            double fx = Math.Max(Math.Abs(alongLow), Math.Abs(alongHigh));
            double fy = Math.Max(Math.Abs(acrossLow), Math.Abs(acrossHigh));
            double bound = Math.Max(Math.Sqrt(gx * gx + gy * gy) - 1.0, 1.0 - Math.Sqrt(fx * fx + fy * fy)) * Math.Min(a, b);
            double slack = 1E-13 * (Math.Abs(Cx) + Math.Abs(Cy) + Math.Abs(min[at]) + Math.Abs(max[at]) + Math.Abs(min[at + 1]) + Math.Abs(max[at + 1]) + a);

            return bound - slack > 0.0 ? bound - slack : 0.0;
        }

        internal override void GetBox(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = Cx - _ellipse.MajorRadius;
            maxX = Cx + _ellipse.MajorRadius;
            minY = Cy - _ellipse.MajorRadius;
            maxY = Cy + _ellipse.MajorRadius;
        }

        internal override bool TryGetFrame(double qx, double qy, double other, out double r1x, out double r1y, out double r2x, out double r2y)
        {
            // By the angle of the unit circle the ellipse is made from: the point is the centre plus a cos t times the major axis and
            // b sin t times the minor, so the first derivative is -a sin t times the first and b cos t times the second, and the second
            // is the point's own offset from the centre, turned back.
            GeoVector2 u = _ellipse.MajorAxis;
            double a = _ellipse.MajorRadius;
            double b = _ellipse.MinorRadius;
            double dx = qx - Cx;
            double dy = qy - Cy;
            double along = dx * u.X + dy * u.Y;
            double across = dy * u.X - dx * u.Y;
            double first = -(a / b) * across;
            double second = (b / a) * along;
            r1x = first * u.X - second * u.Y;
            r1y = first * u.Y + second * u.X;
            r2x = -dx;
            r2y = -dy;
            return true;
        }

        internal override void Find(NurbsCurve curve, Hunt hunt)
        {
            // The crossings, and the places the spline stops coming nearer the rim, from the polynomial; then the nearest
            // approaches from the rim as a spline of its own, which finds those the polynomial leaves to rounding.
            base.Find(curve, hunt);

            var rim = new SplinePartner(NurbsCurve.FromEllipse(Cx, Cy, _ellipse.MajorAxis.X, _ellipse.MajorAxis.Y, _ellipse.MajorRadius, _ellipse.MinorRadius))
            {
                Along = this,
                Position = Position
            };

            rim.Find(curve, hunt);
        }
    }
}
