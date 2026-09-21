using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Core.Internal;
using GeometryHelper.Geometry;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Converts xBIM solids with curved faces or curved edges into closed triangle meshes.
    /// <para>
    /// Reading such solids face by face is lossy: a planar face bounded by a circle (a bolt hole, the cap of a
    /// round column) only exposes the circle's vertices, so its polygon collapses; and curved faces tessellated
    /// one at a time do not share vertices with their planar neighbours, leaving cracks. Meshing the whole solid
    /// in one call lets OpenCascade discretise every shared edge once for both adjacent faces, so the result is
    /// watertight.
    /// </para>
    /// </summary>
    internal static class MeshConvert
    {
        // Maximum angle in radians between the normals of adjacent triangles on a curved surface.
        // Keeps small radii (bolt holes, fillets) round when the linear deflection alone would allow few segments.
        private const double AngularDeflection = 0.25;

        // The native engine can triangulate planar and curved faces of one solid with different edge
        // subdivisions when asked for a very fine deflection, leaving the mesh open along their shared rims.
        // Coarser retries (doubling the deflection) are cheap and in practice close the mesh again.
        private const int MaxMeshAttempts = 3;

        /// <summary>
        /// Checks whether a solid has any curved face or curved edge, and so cannot be read as planar polygons.
        /// </summary>
        /// <param name="solid">The xBIM solid.</param>
        /// <param name="modelTolerance">Length tolerance in model units used to tell a curved edge from a straight one.</param>
        public static bool RequiresMeshing(IXbimSolid solid, double modelTolerance)
        {
            if (!solid.IsPolyhedron)
            {
                return true;
            }

            foreach (IXbimFace face in solid.Faces)
            {
                if (face != null && !face.IsPlanar)
                {
                    return true;
                }
            }

            // A straight edge is exactly as long as the distance between its end vertices;
            // an arc (or a full circle, whose ends coincide) is longer.
            foreach (IXbimEdge edge in solid.Edges)
            {
                if (edge?.EdgeStart == null || edge.EdgeEnd == null)
                {
                    continue;
                }

                double chord = (edge.EdgeEnd.VertexGeometry - edge.EdgeStart.VertexGeometry).Length;
                if (edge.Length - chord > modelTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Meshes a whole xBIM solid into a closed <see cref="GeoSolid3"/> made of triangles.
        /// </summary>
        /// <param name="solid">The xBIM solid.</param>
        /// <param name="options">Conversion options; <see cref="IfcConvertOptions.DeflectionTolerance"/> must already be resolved.</param>
        /// <param name="result">The meshed solid when the method returns true.</param>
        /// <param name="warnings">Receives a message when the solid cannot be triangulated, or null to ignore it.</param>
        public static bool TryMeshSolid(IXbimSolid solid, IfcConvertOptions options, out GeoSolid3 result,
            ICollection<string> warnings = null)
        {
            result = null;
            IfcConvertOptions attemptOptions = options;

            for (int attempt = 0; attempt < MaxMeshAttempts; attempt++)
            {
                if (TryBuildMeshSolid(solid, attemptOptions, out GeoSolid3 body))
                {
                    // Keep the first (finest) mesh as the fallback when no attempt closes.
                    result = result ?? body;
                    if (body.IsClosed(options.Tolerance))
                    {
                        result = body;
                        return true;
                    }
                }

                if (attemptOptions.DeflectionTolerance <= 0.0)
                {
                    break;
                }

                attemptOptions = attemptOptions.Clone();
                attemptOptions.DeflectionTolerance *= 2.0;
            }

            if (result == null)
            {
                warnings?.Add("The geometry engine could not triangulate the solid.");
            }

            return result != null;
        }

        private static bool TryBuildMeshSolid(IXbimSolid solid, IfcConvertOptions options, out GeoSolid3 result)
        {
            result = null;

            List<GeoFace3> faces = MeshToFaces(solid, options);
            if (faces.Count < 4)
            {
                return false;
            }

            try
            {
                result = new GeoSolid3(FaceOrientation.Orient(faces, options.Tolerance));
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns options whose <see cref="IfcConvertOptions.DeflectionTolerance"/> is an explicit value.
        /// The automatic setting (zero or negative) becomes the model's own deflection tolerance
        /// (xBIM <c>ModelFactors.DeflectionTolerance</c>, derived from the model's length unit), in output units.
        /// </summary>
        /// <param name="options">The conversion options.</param>
        /// <param name="model">The model the geometry belongs to, used for its length unit.</param>
        public static IfcConvertOptions ResolveDeflection(IfcConvertOptions options, IModel model)
        {
            if (options.DeflectionTolerance > 0.0 || model?.ModelFactors == null)
            {
                return options;
            }

            IfcConvertOptions resolved = options.Clone();
            resolved.DeflectionTolerance = model.ModelFactors.DeflectionTolerance * options.ScaleFactor;
            return resolved;
        }

        /// <summary>
        /// Meshes any xBIM geometry object (solid or face) and returns its triangles as planar faces,
        /// in output units (scaled by <see cref="IfcConvertOptions.ScaleFactor"/>).
        /// </summary>
        public static List<GeoFace3> MeshToFaces(IXbimGeometryObject geometry, IfcConvertOptions options)
        {
            List<GeoFace3> faces = new List<GeoFace3>();
            Tolerance tolerance = options.Tolerance.ForConstruction();
            double scale = options.ScaleFactor;

            // Tolerances in options are in output units; the engine works in model units.
            // Without a model to resolve the automatic deflection, fall back to 0.1 % of the object's size.
            double precision = tolerance.EqualPoint / scale;
            double deflection = options.DeflectionTolerance > 0.0
                ? options.DeflectionTolerance / scale
                : Math.Max(geometry.BoundingBox.Length() * 0.001, precision);

            // Mesh through CreateShapeGeometry (the path xBIM's own scene builder uses). The receiver-based
            // IXbimGeometryEngine.Mesh throws inside the native engine for curved shapes in this engine build.
            XbimShapeTriangulation triangulation;
            try
            {
                IXbimShapeGeometryData shape = IfcEngineContext.CurrentEngine.CreateShapeGeometry(
                    geometry, precision, deflection, AngularDeflection, XbimGeometryType.PolyhedronBinary);

                using (MemoryStream stream = new MemoryStream(shape.ShapeData))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    triangulation = reader.ReadShapeTriangulation();
                }
            }
            catch
            {
                return faces;
            }

            IList<XbimPoint3D> nodes = triangulation.Vertices;

            foreach (XbimFaceTriangulation faceTriangulation in triangulation.Faces)
            {
                IList<int> indices = faceTriangulation.Indices;

                for (int i = 0; i + 2 < indices.Count; i += 3)
                {
                    int i0 = indices[i];
                    int i1 = indices[i + 1];
                    int i2 = indices[i + 2];

                    if (i0 < 0 || i0 >= nodes.Count ||
                        i1 < 0 || i1 >= nodes.Count ||
                        i2 < 0 || i2 >= nodes.Count)
                    {
                        continue;
                    }

                    GeoPoint3 p0 = nodes[i0].ToGeoPoint3(scale);
                    GeoPoint3 p1 = nodes[i1].ToGeoPoint3(scale);
                    GeoPoint3 p2 = nodes[i2].ToGeoPoint3(scale);

                    if (p0.IsEqualTo(p1, tolerance) || p1.IsEqualTo(p2, tolerance) || p2.IsEqualTo(p0, tolerance))
                    {
                        continue;
                    }

                    try
                    {
                        faces.Add(new GeoFace3(new GeoPolygon3(new[] { p0, p1, p2 }, tolerance)));
                    }
                    catch (ArgumentException)
                    {
                        // Sliver triangle that is collinear within tolerance.
                    }
                }
            }

            return faces;
        }
    }
}
