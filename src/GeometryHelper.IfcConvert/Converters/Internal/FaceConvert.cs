using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Core.Internal;
using GeometryHelper.Geometry;
using Xbim.Common.Geometry;
using Xbim.Ifc4;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Converts faces from xBIM to GeometryHelper.
    /// Handles both planar faces (preserving outer boundaries and inner cutout loops, and splitting a face that strays
    /// out of flat into triangles on its own corners) and non-planar curved faces via triangulation/tessellation.
    /// </summary>
    internal static class FaceConvert
    {
        /// <summary>
        /// Reads one face of an xBIM solid into one or more <see cref="GeoFace3"/> instances.
        /// A planar face produces a single face, or triangles on its own corners where it or a hole of it strays out of
        /// flat; non-planar curved faces are tessellated into planar triangular faces.
        /// </summary>
        /// <param name="face">The xBIM face to convert.</param>
        /// <param name="options">Conversion options controlling tolerance, scaling, and tessellation.</param>
        /// <param name="faces">The converted faces when the method returns true.</param>
        /// <param name="warnings">Receives a message for every problem met, or null to ignore them.</param>
        /// <returns>true if at least one valid face was converted; otherwise false.</returns>
        public static bool TryReadFaces(this IXbimFace face, IfcConvertOptions options, out List<GeoFace3> faces,
            ICollection<string> warnings = null)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            options = options ?? new IfcConvertOptions();
            Tolerance tolerance = options.Tolerance.ForConstruction();
            double scale = options.ScaleFactor;
            faces = new List<GeoFace3>();

            if (face.IsPlanar)
            {
                // A face the file calls flat can stray from one plane by more than a polygon allows, and so can a hole
                // of one: Tekla writes coordinates to a tenth of a micron, and a corner rounded the other way leaves a
                // strip 290 long off its plane. Refused, the face left its body open, and a hole dropped left the walls
                // round it open. Read from its loops, such a face comes as triangles on the corners it came with, each
                // flat, which close the body; a face that lies flat comes as one, holes and all.
                if (TryReadLoops(face, tolerance, scale, out faces))
                {
                    return true;
                }

                // Loops that cannot be split, seen along their normal, as a boundary crossing itself would be: the engine's
                // own mesh of the face, and failing that the boundary alone. A face with no area gives neither.
                if (TryTessellateFace(face, options, out faces))
                {
                    return true;
                }

                if (TryReadBoundary(face, tolerance, scale, out GeoFace3 boundary, warnings))
                {
                    faces = new List<GeoFace3> { boundary };
                    return true;
                }

                return false;
            }

            // Non-planar / curved face
            if (options.TessellateNonPlanarFaces)
            {
                return TryTessellateFace(face, options, out faces);
            }

            return false;
        }

        /// <summary>
        /// Reads a face from its loops of corners: one face where they lie flat, triangles on the corners themselves
        /// where they do not (see <see cref="GeoFace3.FromLoops(IEnumerable{GeoPoint3}, IEnumerable{IEnumerable{GeoPoint3}}, Tolerance)"/>).
        /// </summary>
        /// <remarks>
        /// xBIM reports inner wires wound against their boundary; a face holds its holes wound as its boundary is, and
        /// the triangles run round them either way, so the winding of a wire does not matter here.
        /// </remarks>
        private static bool TryReadLoops(IXbimFace face, Tolerance tolerance, double scale, out List<GeoFace3> result)
        {
            result = new List<GeoFace3>();

            if (face.OuterBound == null)
            {
                return false;
            }

            List<GeoPoint3> boundary = face.OuterBound.Points.Select(p => p.ToGeoPoint3(scale)).ToList();
            List<List<GeoPoint3>> holes = face.InnerBounds == null
                ? new List<List<GeoPoint3>>()
                : face.InnerBounds
                    .Where(wire => wire != null)
                    .Select(wire => wire.Points.Select(p => p.ToGeoPoint3(scale)).ToList())
                    .ToList();

            result.AddRange(GeoFace3.FromLoops(boundary, holes, tolerance));
            return result.Count > 0;
        }

        /// <summary>
        /// Reads the boundary of a face alone, its holes dropped, for a face whose loops could not be read whole.
        /// </summary>
        private static bool TryReadBoundary(IXbimFace face, Tolerance tolerance, double scale, out GeoFace3 result,
            ICollection<string> warnings)
        {
            result = null;

            if (face.OuterBound == null ||
                !TryCreatePolygon(face.OuterBound.Points.Select(p => p.ToGeoPoint3(scale)), tolerance, out GeoPolygon3 boundary))
            {
                return false;
            }

            int holes = face.InnerBounds?.Count(wire => wire != null) ?? 0;

            if (holes > 0)
            {
                warnings?.Add($"A face's {holes} hole(s) could not be kept and were dropped; the volume includes them.");
            }

            result = new GeoFace3(boundary);
            return true;
        }

        private static bool TryTessellateFace(IXbimFace face, IfcConvertOptions options, out List<GeoFace3> resultFaces)
        {
            resultFaces = MeshConvert.MeshToFaces(face, options);
            return resultFaces.Count > 0;
        }

        private static bool TryCreatePolygon(IEnumerable<GeoPoint3> points, Tolerance tolerance, out GeoPolygon3 polygon)
        {
            polygon = null;
            try
            {
                polygon = new GeoPolygon3(points, tolerance);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
