using GeometryHelper.SolidGeometry.Geometry;
using System;
using TSG = Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert transformation matrices between Tekla Structures and GeometryHelper.SolidGeometry.
    /// </summary>
    public static class MatrixConvert
    {
        /// <summary>
        /// Converts a Tekla transformation matrix to a SolidGeometry transformation.
        /// </summary>
        /// <param name="matrix">The Tekla transformation matrix to convert.</param>
        /// <returns>The converted <see cref="GeoTransform3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is null.</exception>
        /// <remarks>
        /// Tekla stores four rows of three, the fourth row carrying the translation, and applies it on the
        /// right for row vectors. SolidGeometry stores a full four by four applied on the left for column
        /// vectors, so the two are transposes of each other and the translation moves from the last row to
        /// the last column.
        /// </remarks>
        public static GeoTransform3 ToGeoTransform3(this TSG.Matrix matrix)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }

            double[,] values = new double[4, 4];

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    values[row, column] = matrix[column, row];
                }
            }

            values[0, 3] = matrix[3, 0];
            values[1, 3] = matrix[3, 1];
            values[2, 3] = matrix[3, 2];
            values[3, 3] = 1.0;

            return new GeoTransform3(values);
        }

        /// <summary>
        /// Converts a SolidGeometry transformation to a Tekla transformation matrix.
        /// </summary>
        /// <param name="transform">The SolidGeometry transformation to convert.</param>
        /// <returns>The converted Tekla <see cref="TSG.Matrix"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="transform"/> is null.</exception>
        public static TSG.Matrix ToTeklaMatrix(this GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            TSG.Matrix matrix = new TSG.Matrix();

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    matrix[row, column] = transform[column, row];
                }
            }

            matrix[3, 0] = transform[0, 3];
            matrix[3, 1] = transform[1, 3];
            matrix[3, 2] = transform[2, 3];

            return matrix;
        }
    }
}
