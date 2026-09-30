using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using TSS = Tekla.Structures.Solid;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Reads the surface of a Tekla solid into a <see cref="GeoSolid3"/>.
    /// <para>
    /// The two descriptions line up almost exactly. Tekla walks a solid as faces, and each face as loops:
    /// the first loop is its outer edge and any further loop is a hole. That is what a
    /// <see cref="GeoFace3"/> is, so the shape of the conversion is a walk rather than a rebuild. They part
    /// only over flatness: a face Tekla gives out of flat comes as triangles on its own corners.
    /// </para>
    /// <para>
    /// What has to be checked rather than trusted is orientation. GeometryHelper reads volume and
    /// containment from the assumption that face normals point out of the body, and a body whose normals
    /// point the other way measures the same volume but reports every point as being on the wrong side of
    /// it. Each face is therefore turned to agree with the normal Tekla gives it, and the finished body is
    /// turned inside out if its signed volume says the whole surface came in reversed.
    /// </para>
    /// </summary>
    public static class SolidConvert
    {
        /// <summary>
        /// Reads the bounding box a Tekla solid reports.
        /// </summary>
        /// <param name="solid">The Tekla solid to read.</param>
        /// <returns>The bounding box as <see cref="GeoAabb3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="solid"/> is null.</exception>
        public static GeoAabb3 ToGeoAabb3(this TSS.ISolid solid)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return new GeoAabb3(solid.MinimumPoint.ToGeoPoint3(), solid.MaximumPoint.ToGeoPoint3());
        }

        /// <summary>
        /// Converts a Tekla solid, using the default tolerance.
        /// </summary>
        /// <param name="solid">The Tekla solid to convert.</param>
        /// <param name="result">The converted body when the method returns true.</param>
        /// <returns>true if the solid was successfully converted; false otherwise.</returns>
        public static bool TryToGeoSolid3(this TSS.ISolid solid, out GeoSolid3 result)
        {
            return TryToGeoSolid3(solid, out result, Tolerance.Global);
        }

        /// <summary>
        /// Converts a Tekla solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The Tekla solid to read.</param>
        /// <param name="result">The converted body when the method returns true.</param>
        /// <param name="tolerance">
        /// The tolerance; its planar threshold decides how far from flat a face may be and still come as
        /// one face. The default, five hundredths of a millimetre, takes the faces Tekla's own cuts leave a
        /// little out of flat whole; a face further out comes as triangles on its own corners.
        /// </param>
        /// <returns>false when too little survived to make a body of at least four faces.</returns>
        /// <remarks>
        /// Each face is read with <see cref="FaceConvert.TryReadFaces(TSS.Face, Tolerance, out GeoFace3[])"/>: one
        /// face where it lies flat, and triangles on the corners Tekla gave where it does not or where a hole
        /// stands off its plane, so a solid Tekla holds closed comes out closed, whatever the tolerance. Only a
        /// face with no area — fewer than three distinct corners, or all of them in a line — is skipped rather
        /// than thrown on, because one bad face in a large model should not cost the whole conversion; ask
        /// <see cref="GeoSolid3.IsClosed()"/> before trusting a volume.
        /// </remarks>
        public static bool TryToGeoSolid3(this TSS.ISolid solid, out GeoSolid3 result, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            List<GeoFace3> faces = new List<GeoFace3>();

            TSS.FaceEnumerator faceEnumerator = solid.GetFaceEnumerator();

            while (faceEnumerator.MoveNext())
            {
                if (faceEnumerator.Current is TSS.Face face && face.TryReadFaces(tolerance, out GeoFace3[] converted))
                {
                    faces.AddRange(converted);
                }
            }

            return TryAssemble(faces, out result);
        }

        /// <summary>
        /// Makes a body of the faces read from a Tekla solid, turned inside out if the whole surface came in reversed.
        /// </summary>
        /// <param name="faces">The faces, each turned to agree with the normal Tekla gave it.</param>
        /// <param name="result">The body when the method returns true.</param>
        /// <returns>false when fewer than four faces were read.</returns>
        internal static bool TryAssemble(List<GeoFace3> faces, out GeoSolid3 result)
        {
            result = null;

            if (faces.Count < 4)
            {
                return false;
            }

            GeoSolid3 body = new GeoSolid3(faces);

            // A surface that came in wound the other way encloses the same volume with the opposite sign.
            // Turning it over costs one pass and saves every later query from being wrong about which side
            // of the body a point is on.
            if (body.GetSignedVolume() < 0.0)
            {
                List<GeoFace3> flipped = new List<GeoFace3>(faces.Count);

                foreach (GeoFace3 face in faces)
                {
                    flipped.Add(face.Flip());
                }

                body = new GeoSolid3(flipped);
            }

            result = body;
            return true;
        }

        /// <summary>
        /// Converts every solid of a sequence, skipping the ones that cannot be read.
        /// </summary>
        /// <param name="solids">The sequence of Tekla solids to convert.</param>
        /// <param name="tolerance">The tolerance for conversion flat checking.</param>
        /// <returns>An array of successfully converted <see cref="GeoSolid3"/> bodies.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="solids"/> is null.</exception>
        /// <remarks>
        /// A model is walked one part at a time and a single unreadable part should not stop the walk, so
        /// what comes back is what could be read rather than all or nothing.
        /// </remarks>
        public static GeoSolid3[] ToGeoSolids(this IEnumerable<TSS.ISolid> solids, Tolerance tolerance)
        {
            if (solids == null)
            {
                throw new ArgumentNullException(nameof(solids));
            }

            List<GeoSolid3> converted = new List<GeoSolid3>();

            foreach (TSS.ISolid solid in solids)
            {
                if (solid != null && solid.TryToGeoSolid3(out GeoSolid3 body, tolerance))
                {
                    converted.Add(body);
                }
            }

            return converted.ToArray();
        }
    }
}
