using System;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    public static partial class Intersection3
    {
        /// <summary>The unit roundoff of a double, half the gap between 1 and the next double up.</summary>
        private const double Epsilon = 1.1102230246251565E-16;

        /// <summary>
        /// Gets the line where the plane of a shape meets another plane, found from the shape's centre: its direction the
        /// first normal crossed with the second, its point the one of it nearest the centre.
        /// </summary>
        /// <param name="center">The centre of the shape lying in the first plane.</param>
        /// <param name="normal">The unit normal of the first plane.</param>
        /// <param name="reach">How far the shape reaches across its plane, its diameter.</param>
        /// <param name="plane">The other plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="offset">The point of the line nearest the centre, as an offset from it, lying in the first plane.</param>
        /// <param name="direction">The unit direction of the line.</param>
        /// <param name="rounding">
        /// How far rounding may have moved the line across itself, a bound: a few parts in 1E16 of what each coordinate puts
        /// into the centre's height above the other plane, and of the offset, each over the sine of the angle between the
        /// planes. A plane whose stored point lies far off along the line adds nothing, since its coordinates there put
        /// nothing into the height.
        /// </param>
        /// <returns>false when the two planes are parallel as far as the shape reaches, as for the circle.</returns>
        /// <remarks>
        /// <see cref="TryGetMeetingLine(GeoPlane3, GeoPlane3, double, out GeoRay3, Tolerance)"/> finds the
        /// line from the planes' distances to the world origin, which planes a thousandth of a degree apart and 52 000 from
        /// the origin turn into an error of a millimetre. Here the only distance is the centre's from the other plane,
        /// measured from that plane's own point, and the rounding left is that distance's, divided by the sine of the
        /// angle between the planes.
        /// </remarks>
        internal static bool TryGetMeetingLineFrom(GeoPoint3 center, GeoVector3 normal, double reach, GeoPlane3 plane, Tolerance tolerance, out GeoVector3 offset, out GeoVector3 direction, out double rounding)
        {
            GeoVector3 other = plane.Normal;
            GeoVector3 cross = normal.CrossProduct(other);
            double sine = cross.Length;

            offset = default(GeoVector3);
            direction = default(GeoVector3);
            rounding = 0.0;

            if (sine * reach <= tolerance.EqualPlanar)
            {
                return false;
            }

            // The point C + beta (n2 - (n1.n2) n1) lies in the first plane, and in the second where beta sin^2 is minus
            // the centre's height above it.
            double beta = -HeightAbove(plane, center) / (sine * sine);
            double cosine = normal.DotProduct(other);

            offset = other.Subtract(normal.Multiply(cosine)).Multiply(beta);
            direction = cross.Multiply(1.0 / sine);

            // The height is worked out to its own last digit, but the shapes it is read from were themselves stored to a
            // part in 1E16 of each coordinate, as was the plane built to pass through a point: what each coordinate puts
            // into the height, over the sine. The offset is read along a difference of nearly equal normals whose rounding
            // is a part in 1E16 over the sine again. A plane whose point is stored far along the line adds nothing, since
            // its coordinates there put nothing into the height.
            GeoPoint3 stored = plane.Origin;
            double terms = (Math.Abs(center.X) + Math.Abs(stored.X)) * Math.Abs(other.X)
                + (Math.Abs(center.Y) + Math.Abs(stored.Y)) * Math.Abs(other.Y)
                + (Math.Abs(center.Z) + Math.Abs(stored.Z)) * Math.Abs(other.Z);
            rounding = Epsilon * (terms / sine + 4.0 * offset.Length * (1.0 + 1.0 / sine));

            return true;
        }

        /// <summary>
        /// Gets the height of a point above a plane, worked out to the last digit of the height rather than of the
        /// coordinates it is found from.
        /// </summary>
        /// <remarks>
        /// The height is (P - O)·n, O the plane's stored point. Read plainly, each difference and each product is rounded to
        /// a part in 1E16 of its own size, so a plane whose point is stored 1E5 away gives a height off by 1E-11 however
        /// small the height is, and a line found from it, between planes 1E-5 apart, by 1E-6: two crossings 0.06 apart
        /// were found 0.009 out. Here each difference is split into its rounded value and the part rounding lost, each
        /// product likewise (Dekker's split, as no fused multiply-add is to be had), and the parts summed with what their
        /// sums lose kept, so that only the rounding of the height itself is left.
        /// </remarks>
        internal static double HeightAbove(GeoPlane3 plane, GeoPoint3 point)
        {
            GeoPoint3 origin = plane.Origin;
            GeoVector3 normal = plane.Normal;
            double sum = 0.0;
            double lost = 0.0;

            AddProduct(point.X, -origin.X, normal.X, ref sum, ref lost);
            AddProduct(point.Y, -origin.Y, normal.Y, ref sum, ref lost);
            AddProduct(point.Z, -origin.Z, normal.Z, ref sum, ref lost);

            return sum + lost;
        }

        /// <summary>
        /// Adds (x + y)·n to a sum, keeping in a second sum what the rounding of each step loses.
        /// </summary>
        private static void AddProduct(double x, double y, double n, ref double sum, ref double lost)
        {
            TwoSum(x, y, out double difference, out double differenceLost);
            TwoProduct(difference, n, out double product, out double productLost);
            TwoSum(sum, product, out sum, out double sumLost);

            lost += sumLost + productLost + differenceLost * n;
        }

        /// <summary>
        /// Gets a sum and, exactly, what its rounding lost (Knuth).
        /// </summary>
        private static void TwoSum(double a, double b, out double sum, out double lost)
        {
            sum = a + b;

            double back = sum - a;

            lost = (a - (sum - back)) + (b - back);
        }

        /// <summary>
        /// Gets a product and, exactly, what its rounding lost, by splitting each factor into two halves of 26 bits
        /// (Dekker).
        /// </summary>
        private static void TwoProduct(double a, double b, out double product, out double lost)
        {
            const double Splitter = 134217729.0;

            product = a * b;

            double ca = Splitter * a;
            double aHigh = ca - (ca - a);
            double aLow = a - aHigh;
            double cb = Splitter * b;
            double bHigh = cb - (cb - b);
            double bLow = b - bHigh;

            lost = ((aHigh * bHigh - product) + aHigh * bLow + aLow * bHigh) + aLow * bLow;
        }
    }
}
