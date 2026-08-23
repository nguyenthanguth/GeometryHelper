using System;
using System.Collections.Generic;
using System.Linq;
using CommonGeometry;
using SolidGeometry.Geometry;
using TSG = Tekla.Structures.Geometry3d;

namespace SolidGeometry.Tekla
{
    /// <summary>
    /// Converts geometry between Tekla Structures and SolidGeometry.
    /// <para>
    /// The two libraries describe the same shapes and disagree only about names and about how much they
    /// insist on. Tekla hands back what its modeller happens to hold; SolidGeometry asks for flatness,
    /// for a closed boundary, and for normals that point out of the body. The conversions here do that
    /// checking rather than trusting it, because a body that only looks right measures wrong later and
    /// says nothing about why.
    /// </para>
    /// <para>
    /// Tekla models in millimetres with coordinates that can run to hundreds of thousands, and a face of
    /// a twelve metre member is rarely flat to the last decimal. The default tolerance is often too tight
    /// for that, so every conversion that needs one takes it, and the overloads without one read
    /// <see cref="Tolerance.Global"/> as everywhere else.
    /// </para>
    /// </summary>
    public static class VectorConvert
    {
        /// <summary>
        /// Converts a Tekla vector to a SolidGeometry vector.
        /// </summary>
        /// <param name="vector">The Tekla vector to convert.</param>
        /// <returns>The converted <see cref="GeoVector3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="vector"/> is null.</exception>
        public static GeoVector3 ToGeoVector3(this TSG.Vector vector)
        {
            if (vector == null)
            {
                throw new ArgumentNullException(nameof(vector));
            }

            return new GeoVector3(vector.X, vector.Y, vector.Z);
        }

        /// <summary>
        /// Converts a SolidGeometry vector to a Tekla vector.
        /// </summary>
        /// <param name="vector">The SolidGeometry vector to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.Vector"/>.</returns>
        public static TSG.Vector ToTeklaVector(this GeoVector3 vector) => new TSG.Vector(vector.X, vector.Y, vector.Z);

        /// <summary>
        /// Converts a sequence of Tekla vectors to a list of SolidGeometry vectors.
        /// </summary>
        /// <param name="vectors">The sequence of Tekla vectors to convert.</param>
        /// <returns>A list of converted <see cref="GeoVector3"/>.</returns>
        public static List<GeoVector3> ToGeoVector3(this IEnumerable<TSG.Vector> vectors) => vectors.Select(ToGeoVector3).ToList();

        /// <summary>
        /// Converts a sequence of SolidGeometry vectors to a list of Tekla vectors.
        /// </summary>
        /// <param name="vectors">The sequence of SolidGeometry vectors to convert.</param>
        /// <returns>A list of converted Tekla <see cref="TSG.Vector"/>.</returns>
        public static List<TSG.Vector> ToTeklaVector(this IEnumerable<GeoVector3> vectors) => vectors.Select(ToTeklaVector).ToList();
    }
}
