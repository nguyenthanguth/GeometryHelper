using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper.PlaneGeometry.Geometry;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.CadConvert
{
    /// <summary>
    /// Converts vectors between AutoCAD and GeometryHelper.
    /// </summary>
    public static class VectorConvert
    {
        /// <summary>
        /// Converts a AutoCAD 2D vector to a SolidGeometry 2D vector.
        /// </summary>
        /// <param name="vector">The AutoCAD 2D vector to convert.</param>
        /// <returns>The converted <see cref="GeoVector2"/>.</returns>
        public static GeoVector2 ToGeoVector2(this Vector2d vector) => new GeoVector2(vector.X, vector.Y);

        /// <summary>
        /// Converts a sequence of AutoCAD 2D vectors to a list of SolidGeometry 2D vectors.
        /// </summary>
        /// <param name="vectors">The sequence of AutoCAD 2D vectors to convert.</param>
        /// <returns>A list of converted <see cref="GeoVector2"/>.</returns>
        public static List<GeoVector2> ToGeoVector2(this IEnumerable<Vector2d> vectors) => vectors.Select(ToGeoVector2).ToList();

        /// <summary>
        /// Converts a AutoCAD 3D vector to a SolidGeometry 2D vector (discarding the Z coordinate).
        /// </summary>
        /// <param name="vector">The AutoCAD 3D vector to convert.</param>
        /// <returns>The converted <see cref="GeoVector2"/>.</returns>
        public static GeoVector2 ToGeoVector2(this Vector3d vector) => new GeoVector2(vector.X, vector.Y);

        /// <summary>
        /// Converts a sequence of AutoCAD 3D vectors to a list of SolidGeometry 2D vectors (discarding their Z coordinates).
        /// </summary>
        /// <param name="vectors">The sequence of AutoCAD 3D vectors to convert.</param>
        /// <returns>A list of converted <see cref="GeoVector2"/>.</returns>
        public static List<GeoVector2> ToGeoVector2(this IEnumerable<Vector3d> vectors) => vectors.Select(ToGeoVector2).ToList();

        /// <summary>
        /// Converts a AutoCAD 3D vector to a SolidGeometry 3D vector.
        /// </summary>
        /// <param name="vector">The AutoCAD 3D vector to convert.</param>
        /// <returns>The converted <see cref="GeoVector3"/>.</returns>
        public static GeoVector3 ToGeoVector3(this Vector3d vector) => new GeoVector3(vector.X, vector.Y, vector.Z);

        /// <summary>
        /// Converts a sequence of AutoCAD 3D vectors to a list of SolidGeometry 3D vectors.
        /// </summary>
        /// <param name="vectors">The sequence of AutoCAD 3D vectors to convert.</param>
        /// <returns>A list of converted <see cref="GeoVector3"/>.</returns>
        public static List<GeoVector3> ToGeoVector3(this IEnumerable<Vector3d> vectors) => vectors.Select(ToGeoVector3).ToList();

        /// <summary>
        /// Converts a SolidGeometry 2D vector to a AutoCAD 2D vector.
        /// </summary>
        /// <param name="vector">The SolidGeometry 2D vector to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Vector2d"/>.</returns>
        public static Vector2d ToAcadVector2(this GeoVector2 vector) => new Vector2d(vector.X, vector.Y);

        /// <summary>
        /// Converts a sequence of SolidGeometry 2D vectors to a list of AutoCAD 2D vectors.
        /// </summary>
        /// <param name="vectors">The sequence of SolidGeometry 2D vectors to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Vector2d"/>.</returns>
        public static List<Vector2d> ToAcadVector2(this IEnumerable<GeoVector2> vectors) => vectors.Select(ToAcadVector2).ToList();

        /// <summary>
        /// Converts a SolidGeometry 2D vector to a AutoCAD 3D vector (with Z = 0).
        /// </summary>
        /// <param name="vector">The SolidGeometry 2D vector to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Vector3d"/>.</returns>
        public static Vector3d ToAcadVector3(this GeoVector2 vector) => new Vector3d(vector.X, vector.Y, 0.0);

        /// <summary>
        /// Converts a sequence of SolidGeometry 2D vectors to a list of AutoCAD 3D vectors (with Z = 0).
        /// </summary>
        /// <param name="vectors">The sequence of SolidGeometry 2D vectors to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Vector3d"/>.</returns>
        public static List<Vector3d> ToAcadVector3(this IEnumerable<GeoVector2> vectors) => vectors.Select(ToAcadVector3).ToList();

        /// <summary>
        /// Converts a SolidGeometry 3D vector to a AutoCAD 3D vector.
        /// </summary>
        /// <param name="vector">The SolidGeometry 3D vector to convert.</param>
        /// <returns>The converted AutoCAD <see cref="Vector3d"/>.</returns>
        public static Vector3d ToAcadVector3(this GeoVector3 vector) => new Vector3d(vector.X, vector.Y, vector.Z);

        /// <summary>
        /// Converts a sequence of SolidGeometry 3D vectors to a list of AutoCAD 3D vectors.
        /// </summary>
        /// <param name="vectors">The sequence of SolidGeometry 3D vectors to convert.</param>
        /// <returns>A list of converted AutoCAD <see cref="Vector3d"/>.</returns>
        public static List<Vector3d> ToAcadVector3(this IEnumerable<GeoVector3> vectors) => vectors.Select(ToAcadVector3).ToList();
    }
}
