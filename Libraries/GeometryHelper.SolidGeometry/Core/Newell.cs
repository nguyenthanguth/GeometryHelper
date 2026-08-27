using System.Collections.Generic;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.SolidGeometry.Core
{
    /// <summary>
    /// Computes the area vector of a closed loop of 3D points by Newell's method.
    /// <para>
    /// A polygon in space has no single pair of coordinates to take a shoelace sum over, and picking one
    /// by dropping the axis the polygon leans on least works but breaks down near the changeover. Newell's
    /// method sidesteps the choice: it sums a signed contribution per edge on all three axes at once, and
    /// the resulting vector points along the polygon normal with a length equal to the area. It is also
    /// forgiving of vertices that are not exactly coplanar, which is what makes it the right tool for
    /// deciding whether a nearly flat loop is flat enough.
    /// </para>
    /// </summary>
    internal static class Newell
    {
        /// <summary>
        /// Gets the signed area of a closed loop as a vector normal to it.
        /// </summary>
        /// <param name="vertices">
        /// The vertices of the loop, in order. The loop is treated as closed, so the last vertex is
        /// joined back to the first without needing to be repeated.
        /// </param>
        /// <returns>
        /// A vector along the loop normal whose length is the area of the loop, or the zero vector when
        /// the loop has fewer than three vertices or encloses nothing.
        /// <para>
        /// Note the difference from <see cref="GeoTriangle3.GetAreaVector"/>, which is a bare cross
        /// product and so comes back at twice the area. The halving is applied here.
        /// </para>
        /// </returns>
        public static GeoVector3 GetAreaVector(IReadOnlyList<GeoPoint3> vertices)
        {
            if (vertices == null || vertices.Count < 3)
            {
                return GeoVector3.Zero;
            }

            double nx = 0.0;
            double ny = 0.0;
            double nz = 0.0;

            for (int i = 0; i < vertices.Count; i++)
            {
                GeoPoint3 current = vertices[i];
                GeoPoint3 next = vertices[(i + 1) % vertices.Count];

                nx += (current.Y - next.Y) * (current.Z + next.Z);
                ny += (current.Z - next.Z) * (current.X + next.X);
                nz += (current.X - next.X) * (current.Y + next.Y);
            }

            return new GeoVector3(nx * 0.5, ny * 0.5, nz * 0.5);
        }
    }
}
