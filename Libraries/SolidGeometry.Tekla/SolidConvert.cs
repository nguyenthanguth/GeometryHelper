using System;
using System.Collections.Generic;
using CommonGeometry;
using SolidGeometry.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using TSS = Tekla.Structures.Solid;

namespace SolidGeometry.Tekla
{
    /// <summary>
    /// Reads the surface of a Tekla solid into a <see cref="GeoSolid3"/>.
    /// <para>
    /// The two descriptions line up almost exactly. Tekla walks a solid as faces, and each face as loops:
    /// the first loop is its outer edge and any further loop is a hole. That is what a
    /// <see cref="GeoFace3"/> is, so the shape of the conversion is a walk rather than a rebuild.
    /// </para>
    /// <para>
    /// What has to be checked rather than trusted is orientation. SolidGeometry reads volume and
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
        public static GeoAabb3 ToGeoAabb3(this TSM.Solid solid)
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
        public static bool TryToGeoSolid3(this TSM.Solid solid, out GeoSolid3 result)
        {
            return TryToGeoSolid3(solid, out result, Tolerance.Global);
        }

        /// <summary>
        /// Converts a Tekla solid, within a tolerance.
        /// </summary>
        /// <param name="solid">The Tekla solid to read.</param>
        /// <param name="result">The converted body when the method returns true.</param>
        /// <param name="tolerance">
        /// The tolerance; its planar threshold decides how far from flat a face may be before it is
        /// refused. Tekla models in millimetres over large coordinates, so this usually wants widening.
        /// </param>
        /// <returns>false when too little survived to make a body of at least four faces.</returns>
        /// <remarks>
        /// Faces that cannot be made sense of — fewer than three distinct vertices, all of them in a line,
        /// or not flat within the tolerance — are skipped rather than throwing, because one bad face in a
        /// large model should not cost the whole conversion. The result is then no longer closed, which is
        /// what <see cref="GeoSolid3.IsClosed()"/> is for: ask it before trusting a volume.
        /// </remarks>
        public static bool TryToGeoSolid3(this TSM.Solid solid, out GeoSolid3 result, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            result = null;

            List<GeoFace3> faces = new List<GeoFace3>();

            TSS.FaceEnumerator faceEnumerator = solid.GetFaceEnumerator();

            while (faceEnumerator.MoveNext())
            {
                TSS.Face face = faceEnumerator.Current as TSS.Face;

                if (face == null)
                {
                    continue;
                }

                if (face.TryReadFace(tolerance, out GeoFace3 converted))
                {
                    faces.Add(converted);
                }
            }

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
        public static GeoSolid3[] ToGeoSolids(this IEnumerable<TSM.Solid> solids, Tolerance tolerance)
        {
            if (solids == null)
            {
                throw new ArgumentNullException(nameof(solids));
            }

            List<GeoSolid3> converted = new List<GeoSolid3>();

            foreach (TSM.Solid solid in solids)
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
