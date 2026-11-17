using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The mass properties of regions lying flat on the XY plane, as AutoCAD 2021's MASSPROP gives them for a selection of
    /// regions: the area and perimeter, the box, the centroid, the moments of inertia and the product of inertia about the
    /// origin, the radii of gyration, and the principal moments and their directions about the centroid. The integrals are those
    /// of Green's theorem over the boundary of each face, taken by Gauss-Legendre quadrature in pieces of at most 30 degrees,
    /// so a circle's area and moments are exact to about 1E-12 of themselves; a hole is a loop that runs the other way round
    /// and takes away what its outer loop adds.
    /// </summary>
    internal sealed class CadMassProperties
    {
        // Gauss-Legendre, eight points: nodes on -1 to 1 and their weights.
        private static readonly double[] Nodes =
        {
            -0.9602898564975363,
            -0.7966664774136267,
            -0.5255324099163290,
            -0.1834346424956498,
            0.1834346424956498,
            0.5255324099163290,
            0.7966664774136267,
            0.9602898564975363,
        };

        private static readonly double[] Weights =
        {
            0.1012285362903763,
            0.2223810344533745,
            0.3137066458778873,
            0.3626837833783620,
            0.3626837833783620,
            0.3137066458778873,
            0.2223810344533745,
            0.1012285362903763,
        };

        private CadMassProperties() { }

        /// <summary>Gets the area: that of the outer loops less that of the holes.</summary>
        internal double Area { get; private set; }

        /// <summary>Gets the length of every loop, holes too.</summary>
        internal double Perimeter { get; private set; }

        /// <summary>Gets the least corner of the box of the regions.</summary>
        internal GeoPoint2 BoxMin { get; private set; }

        /// <summary>Gets the greatest corner of the box of the regions.</summary>
        internal GeoPoint2 BoxMax { get; private set; }

        /// <summary>Gets the centroid.</summary>
        internal GeoPoint2 Centroid { get; private set; }

        /// <summary>Gets the moment of inertia about the X axis, the integral of y squared over the area.</summary>
        internal double MomentX { get; private set; }

        /// <summary>Gets the moment of inertia about the Y axis, the integral of x squared over the area.</summary>
        internal double MomentY { get; private set; }

        /// <summary>Gets the product of inertia XY as AutoCAD gives it: the integral of x times y, with the sign changed.</summary>
        internal double ProductXY { get; private set; }

        /// <summary>Gets the radius of gyration about the X axis.</summary>
        internal double RadiusX { get; private set; }

        /// <summary>Gets the radius of gyration about the Y axis.</summary>
        internal double RadiusY { get; private set; }

        /// <summary>Gets the principal moment I about the centroid.</summary>
        internal double PrincipalI { get; private set; }

        /// <summary>Gets the direction of the axis the principal moment I is about, a unit vector whose angle is between -45 and 45 degrees.</summary>
        internal GeoVector2 DirectionI { get; private set; }

        /// <summary>Gets the principal moment J about the centroid.</summary>
        internal double PrincipalJ { get; private set; }

        /// <summary>Gets the direction of the axis the principal moment J is about: that of I turned a quarter turn counter-clockwise.</summary>
        internal GeoVector2 DirectionJ { get; private set; }

        /// <summary>
        /// Works out the properties of regions.
        /// </summary>
        /// <param name="shapes">The regions, flat on the XY plane.</param>
        /// <returns>The properties; null where the regions have no area.</returns>
        internal static CadMassProperties Of(IEnumerable<DxfRegionShape> shapes)
        {
            double area = 0.0,
                perimeter = 0.0,
                sx = 0.0,
                sy = 0.0,
                ixx = 0.0,
                iyy = 0.0,
                pxy = 0.0;
            double minX = double.PositiveInfinity,
                minY = double.PositiveInfinity,
                maxX = double.NegativeInfinity,
                maxY = double.NegativeInfinity;

            foreach (DxfRegionShape shape in shapes)
            {
                foreach (DxfRegionLoop loop in shape.Loops)
                {
                    double[] sums = LoopIntegrals(loop.Curves);

                    // An outer loop adds its area and a hole takes its area away, whichever way the curves were found to run.
                    if ((sums[0] < 0.0) != loop.IsHole)
                    {
                        for (int i = 0; i < sums.Length; i++)
                        {
                            sums[i] = -sums[i];
                        }
                    }

                    area += sums[0];
                    sx += sums[1];
                    sy += sums[2];
                    ixx += sums[4];
                    iyy += sums[3];
                    pxy += sums[5];
                    perimeter += loop.Perimeter;

                    foreach (DxfCurve curve in loop.Curves)
                    {
                        Extend(curve, ref minX, ref minY, ref maxX, ref maxY);
                    }
                }
            }

            if (!(area > 0.0))
            {
                return null;
            }

            double cx = sx / area;
            double cy = sy / area;

            // About the centroid: the moment about the X axis and about the Y axis, and the integral of the product.
            double ixc = ixx - area * cy * cy;
            double iyc = iyy - area * cx * cx;
            double pc = pxy - area * cx * cy;
            double turn =
                Math.Abs(iyc - ixc) <= 1E-12 * Math.Max(Math.Abs(iyc), Math.Abs(ixc))
                    ? (Math.Abs(pc) <= 1E-12 * Math.Max(Math.Abs(iyc), 1.0) ? 0.0 : Math.Sign(pc) * Math.PI / 4.0)
                    : 0.5 * Math.Atan(2.0 * pc / (iyc - ixc));
            double c = Math.Cos(turn);
            double s = Math.Sin(turn);
            return new CadMassProperties
            {
                Area = area,
                Perimeter = perimeter,
                BoxMin = new GeoPoint2(minX, minY),
                BoxMax = new GeoPoint2(maxX, maxY),
                Centroid = new GeoPoint2(cx, cy),
                MomentX = ixx,
                MomentY = iyy,
                ProductXY = -pxy,
                RadiusX = Math.Sqrt(ixx / area),
                RadiusY = Math.Sqrt(iyy / area),
                PrincipalI = ixc * c * c + iyc * s * s - 2.0 * pc * s * c,
                DirectionI = new GeoVector2(c, s),
                PrincipalJ = ixc * s * s + iyc * c * c + 2.0 * pc * s * c,
                DirectionJ = new GeoVector2(-s, c),
            };
        }

        /// <summary>
        /// The report as MASSPROP writes it on the screen, one string to a line: the heading, the area and perimeter, the box, the
        /// centroid, the moments and product of inertia, the radii of gyration, and the principal moments with their directions.
        /// </summary>
        /// <returns>The lines.</returns>
        internal List<string> Lines()
        {
            return new List<string>
            {
                " ----------------   REGIONS   ----------------",
                "Area:                    " + Four(Area),
                "Perimeter:               " + Four(Perimeter),
                "Bounding box:         X: " + Four(BoxMin.X) + "  --  " + Four(BoxMax.X),
                "                      Y: " + Four(BoxMin.Y) + "  --  " + Four(BoxMax.Y),
                "Centroid:             X: " + Four(Centroid.X),
                "                      Y: " + Four(Centroid.Y),
                "Moments of inertia:   X: " + Four(MomentX),
                "                      Y: " + Four(MomentY),
                "Product of inertia:  XY: " + Four(ProductXY),
                "Radii of gyration:    X: " + Four(RadiusX),
                "                      Y: " + Four(RadiusY),
                "Principal moments and X-Y directions about centroid:",
                "                      I: "
                    + Four(PrincipalI)
                    + " along ["
                    + Four(DirectionI.X)
                    + " "
                    + Four(DirectionI.Y)
                    + "]",
                "                      J: "
                    + Four(PrincipalJ)
                    + " along ["
                    + Four(DirectionJ.X)
                    + " "
                    + Four(DirectionJ.Y)
                    + "]",
            };
        }

        /// <summary>
        /// The lines of the file MASSPROP writes, as AutoCAD writes it: an empty line, the heading, an empty line, then the report
        /// with the spaces AutoCAD leaves at the end of its first lines, each ending in a carriage return and a line feed.
        /// </summary>
        /// <returns>The text.</returns>
        internal string FileText()
        {
            List<string> lines = Lines();
            var text = new System.Text.StringBuilder("\r\n");

            for (int i = 0; i < lines.Count; i++)
            {
                int trailing =
                    i < 7 || i == 10 || i == 11 ? 1
                    : i >= 12 ? 0
                    : 2;
                text.Append(lines[i]).Append(' ', trailing).Append("\r\n");

                if (i == 0)
                {
                    text.Append("\r\n");
                }
            }

            return text.ToString();
        }

        /// <summary>A number to four places, a negative zero as zero: 4166666.6667.</summary>
        private static string Four(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.0000", CultureInfo.InvariantCulture);

        /// <summary>
        /// The integrals over the area a loop bounds, taken round its boundary with the curves in the order they run: the area, the
        /// integrals of x, of y, of x squared, of y squared and of x times y; the sign says which way round the curves were
        /// taken.
        /// </summary>
        private static double[] LoopIntegrals(IReadOnlyList<DxfCurve> curves)
        {
            var sums = new double[6];

            // The way round the loop: each curve is taken from the end that meets the one before it; the first from the end that
            // meets the second.
            GeoPoint3 at;
            bool forward;

            if (curves.Count == 1)
            {
                forward = true;
                at = curves[0].EndPoint;
            }
            else
            {
                GeoPoint3 start = curves[0].StartPoint;
                GeoPoint3 end = curves[0].EndPoint;
                double toEnd = Math.Min(end.DistanceTo(curves[1].StartPoint), end.DistanceTo(curves[1].EndPoint));
                double toStart = Math.Min(start.DistanceTo(curves[1].StartPoint), start.DistanceTo(curves[1].EndPoint));
                forward = toEnd <= toStart;
                at = forward ? end : start;
            }

            Integrate(curves[0], forward, sums);

            for (int i = 1; i < curves.Count; i++)
            {
                forward = at.DistanceTo(curves[i].StartPoint) <= at.DistanceTo(curves[i].EndPoint);
                at = forward ? curves[i].EndPoint : curves[i].StartPoint;
                Integrate(curves[i], forward, sums);
            }

            return sums;
        }

        /// <summary>Adds the boundary integrals of one curve to the sums, taken the way the curve is drawn or against it.</summary>
        private static void Integrate(DxfCurve curve, bool forward, double[] sums)
        {
            double t0 = curve.StartParam;
            double t1 = curve.EndParam;
            bool line = curve is DxfLine;
            int pieces = line ? 1 : Math.Max(1, (int)Math.Ceiling(Math.Abs(t1 - t0) / (Math.PI / 6.0) - 1E-9));
            double sign = forward ? 1.0 : -1.0;
            GeoVector3 straight = line ? (curve.EndPoint - curve.StartPoint) / Math.Abs(t1 - t0) : default(GeoVector3);

            for (int p = 0; p < pieces; p++)
            {
                double a = t0 + (t1 - t0) * p / pieces;
                double b = t0 + (t1 - t0) * (p + 1) / pieces;
                double half = (b - a) / 2.0;
                double mid = (a + b) / 2.0;

                for (int k = 0; k < Nodes.Length; k++)
                {
                    double t = mid + half * Nodes[k];
                    GeoPoint3 point = curve.GetPointAtParameter(t);
                    double dx;
                    double dy;

                    if (line)
                    {
                        dx = straight.X;
                        dy = straight.Y;
                    }
                    else
                    {
                        // The slope by the five-point stencil, a step that keeps the four points on the curve's own span.
                        double h = Math.Min(1E-3, 0.4 * half * (1.0 + Nodes[0]));
                        GeoPoint3 a2 = curve.GetPointAtParameter(t + 2.0 * h);
                        GeoPoint3 a1 = curve.GetPointAtParameter(t + h);
                        GeoPoint3 b1 = curve.GetPointAtParameter(t - h);
                        GeoPoint3 b2 = curve.GetPointAtParameter(t - 2.0 * h);
                        dx = (-a2.X + 8.0 * a1.X - 8.0 * b1.X + b2.X) / (12.0 * h);
                        dy = (-a2.Y + 8.0 * a1.Y - 8.0 * b1.Y + b2.Y) / (12.0 * h);
                    }

                    double w = Weights[k] * half * sign;
                    double x = point.X;
                    double y = point.Y;
                    sums[0] += 0.5 * (x * dy - y * dx) * w;
                    sums[1] += 0.5 * x * x * dy * w;
                    sums[2] -= 0.5 * y * y * dx * w;
                    sums[3] += x * x * x * dy / 3.0 * w;
                    sums[4] -= y * y * y * dx / 3.0 * w;
                    sums[5] += 0.5 * x * x * y * dy * w;
                }
            }
        }

        /// <summary>Takes the box of a curve into the box so far: its ends, points along it, and the places an arc reaches furthest in X and Y.</summary>
        private static void Extend(DxfCurve curve, ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            var points = new List<GeoPoint3> { curve.StartPoint, curve.EndPoint };

            if (!(curve is DxfLine))
            {
                double t0 = curve.StartParam;
                double t1 = curve.EndParam;
                int samples = Math.Max(8, (int)Math.Ceiling(Math.Abs(t1 - t0) / (2.0 * Math.PI) * 720.0));

                for (int i = 1; i < samples; i++)
                {
                    points.Add(curve.GetPointAtParameter(t0 + (t1 - t0) * i / samples));
                }

                // The quarter turns of a circle or an arc, where it reaches furthest along an axis.
                if (curve is DxfArc || curve is DxfCircle)
                {
                    double lo = Math.Min(t0, t1);
                    double hi = Math.Max(t0, t1);

                    for (
                        double quarter = Math.Ceiling(lo / (Math.PI / 2.0)) * (Math.PI / 2.0);
                        quarter <= hi;
                        quarter += Math.PI / 2.0
                    )
                    {
                        points.Add(curve.GetPointAtParameter(quarter));
                    }
                }
            }

            foreach (GeoPoint3 point in points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }
    }
}
