using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Core.Internal;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Common.Geometry;
using Xbim.Ifc4;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Converts faces from xBIM to GeometryHelper.SolidGeometry.
    /// Handles both planar faces (preserving outer boundaries and inner cutout loops)
    /// and non-planar curved faces via triangulation/tessellation.
    /// </summary>
    internal static class FaceConvert
    {
        /// <summary>
        /// Reads one face of an xBIM solid into one or more <see cref="GeoFace3"/> instances.
        /// Planar faces produce a single face; non-planar curved faces are tessellated into planar triangular faces.
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
                if (TryReadPlanarFace(face, tolerance, scale, out GeoFace3 planarFace, warnings))
                {
                    faces.Add(planarFace);
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

        private static bool TryReadPlanarFace(IXbimFace face, Tolerance tolerance, double scale, out GeoFace3 result,
            ICollection<string> warnings)
        {
            result = null;

            if (face.OuterBound == null)
            {
                return false;
            }

            List<GeoPoint3> outerPoints = face.OuterBound.Points.Select(p => p.ToGeoPoint3(scale)).ToList();

            if (!TryCreatePolygon(outerPoints, tolerance, out GeoPolygon3 boundary))
            {
                return false;
            }

            List<GeoPolygon3> holes = new List<GeoPolygon3>();

            if (face.InnerBounds != null)
            {
                foreach (IXbimWire innerWire in face.InnerBounds)
                {
                    if (innerWire == null) continue;

                    List<GeoPoint3> holePoints = innerWire.Points.Select(p => p.ToGeoPoint3(scale)).ToList();
                    if (TryCreatePolygon(holePoints, tolerance, out GeoPolygon3 holePolygon))
                    {
                        // GeoSolid3 expects holes wound the same way as their boundary (it subtracts their
                        // contribution to reverse them); xBIM reports inner wires wound the opposite way.
                        holes.Add(holePolygon.Normal.DotProduct(boundary.Normal) < 0.0 ? holePolygon.Flip() : holePolygon);
                    }
                }
            }

            try
            {
                result = holes.Count > 0
                    ? new GeoFace3(boundary, holes, tolerance)
                    : new GeoFace3(boundary);
            }
            catch (ArgumentException)
            {
                // If a hole does not lie on the plane of the boundary within tolerance,
                // fall back to using the boundary alone to preserve the surface without leaving a gap.
                warnings?.Add($"A face's {holes.Count} hole(s) are off its plane and were dropped; the volume includes them.");
                result = new GeoFace3(boundary);
            }

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
