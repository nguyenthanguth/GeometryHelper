using SolidGeometry.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TSG = Tekla.Structures.Geometry3d;

namespace SolidGeometry.Tekla
{
    /// <summary>
    /// Provides extension methods to convert bounding boxes (AABB) between Tekla Structures and SolidGeometry.
    /// </summary>
    public static class AabbConvert
    {
        /// <summary>
        /// Converts a Tekla bounding box to a SolidGeometry bounding box.
        /// </summary>
        /// <param name="box">The Tekla bounding box to convert.</param>
        /// <returns>The converted <see cref="GeoAabb3"/> bounding box.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="box"/> is null.</exception>
        public static GeoAabb3 ToGeoAabb3(this TSG.AABB box)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return new GeoAabb3(box.MinPoint.ToGeoPoint3(), box.MaxPoint.ToGeoPoint3());
        }

        /// <summary>
        /// Converts a SolidGeometry bounding box to a Tekla bounding box.
        /// </summary>
        /// <param name="box">The SolidGeometry bounding box to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.AABB"/> bounding box.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty, which Tekla cannot express.</exception>
        public static TSG.AABB ToTeklaAabb(this GeoAabb3 box)
        {
            if (box.IsEmpty)
            {
                throw new InvalidOperationException("An empty bounding box has no corners to hand to Tekla.");
            }

            return new TSG.AABB(box.Min.ToTeklaPoint(), box.Max.ToTeklaPoint());
        }

        /// <summary>
        /// Converts a sequence of Tekla bounding boxes to a list of SolidGeometry bounding boxes.
        /// </summary>
        /// <param name="boxes">The sequence of Tekla bounding boxes to convert.</param>
        /// <returns>A list of converted <see cref="GeoAabb3"/> bounding boxes.</returns>
        public static List<GeoAabb3> ToGeoAabb3(this IEnumerable<TSG.AABB> boxes) => boxes.Select(ToGeoAabb3).ToList();

        /// <summary>
        /// Converts a sequence of SolidGeometry bounding boxes to a list of Tekla bounding boxes.
        /// </summary>
        /// <param name="boxes">The sequence of SolidGeometry bounding boxes to convert.</param>
        /// <returns>A list of converted Tekla <see cref="TSG.AABB"/> bounding boxes.</returns>
        public static List<TSG.AABB> ToTeklaAabb(this IEnumerable<GeoAabb3> boxes) => boxes.Select(ToTeklaAabb).ToList();
    }
}
