using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Writes results down to the last bit, for the tests of the sweeps that choose their axis: what they found before
    /// the axis was chosen is held as the SHA-256 of that text.
    /// </summary>
    internal static class SweepAxisText
    {
        internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        internal static void Append(StringBuilder text, GeoPoint3 point)
            => text.Append(Number(point.X)).Append(',').Append(Number(point.Y)).Append(',').Append(Number(point.Z)).Append(' ');

        internal static void Append(StringBuilder text, GeoTriangle3[] triangles)
        {
            text.Append(triangles.Length).Append(':');

            foreach (GeoTriangle3 triangle in triangles)
            {
                Append(text, triangle.A);
                Append(text, triangle.B);
                Append(text, triangle.C);
                text.Append('|');
            }

            text.AppendLine();
        }

        internal static void Append(StringBuilder text, GeoFace3 face)
        {
            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                Append(text, corner);
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                text.Append('/');

                foreach (GeoPoint3 corner in hole.Vertices)
                {
                    Append(text, corner);
                }
            }

            text.Append(';');
        }

        internal static string Hash(StringBuilder text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                return string.Concat(hash.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>
        /// A point with X and Y, or X and Z, swapped as an axis asks, which rounds nothing.
        /// </summary>
        internal static GeoPoint3 Along(char axis, GeoPoint3 p)
        {
            switch (axis)
            {
                case 'X': return p;
                case 'Y': return new GeoPoint3(p.Y, p.X, p.Z);
                default: return new GeoPoint3(p.Z, p.Y, p.X);
            }
        }

        internal static GeoAabb3 Along(char axis, GeoAabb3 box) => new GeoAabb3(Along(axis, box.Min), Along(axis, box.Max));
    }
}
