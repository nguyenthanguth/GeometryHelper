using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Common.Geometry;
using Xbim.Ifc4;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters
{
    /// <summary>
    /// Converts faces from xBIM to GeometryHelper.SolidGeometry.
    /// Handles both planar faces (preserving outer boundaries and inner cutout loops)
    /// and non-planar curved faces via triangulation/tessellation.
    /// </summary>
    public static class FaceConvert
    {
        /// <summary>
        /// Reads one face of an xBIM solid into one or more <see cref="GeoFace3"/> instances.
        /// Planar faces produce a single face; non-planar curved faces are tessellated into planar triangular faces.
        /// </summary>
        /// <param name="face">The xBIM face to convert.</param>
        /// <param name="options">Conversion options controlling tolerance, scaling, and tessellation.</param>
        /// <param name="faces">The converted faces when the method returns true.</param>
        /// <returns>true if at least one valid face was converted; otherwise false.</returns>
        public static bool TryReadFaces(this IXbimFace face, IfcConvertOptions options, out List<GeoFace3> faces)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            options = options ?? new IfcConvertOptions();
            Tolerance tolerance = options.Tolerance;
            double scale = options.ScaleFactor;
            faces = new List<GeoFace3>();

            if (face.IsPlanar)
            {
                if (TryReadPlanarFace(face, tolerance, scale, out GeoFace3 planarFace))
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

        private static bool TryReadPlanarFace(IXbimFace face, Tolerance tolerance, double scale, out GeoFace3 result)
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
                        holes.Add(holePolygon);
                    }
                }
            }

            try
            {
                result = holes.Count > 0
                    ? new GeoFace3(boundary, holes, tolerance)
                    : new GeoFace3(boundary);
                return true;
            }
            catch (ArgumentException)
            {
                // If a hole does not lie on the plane of the boundary within tolerance,
                // fall back to using the boundary alone to preserve the surface without leaving a gap.
                result = new GeoFace3(boundary);
                return true;
            }
        }

        private static bool TryTessellateFace(IXbimFace face, IfcConvertOptions options, out List<GeoFace3> resultFaces)
        {
            resultFaces = new List<GeoFace3>();
            Tolerance tolerance = options.Tolerance;
            double scale = options.ScaleFactor;

            IXbimGeometryEngine engine = IfcEngineContext.CurrentEngine;
            SimpleMeshReceiver receiver = new SimpleMeshReceiver();

            try
            {
                engine.Mesh(receiver, face, tolerance.EqualPoint, options.DeflectionTolerance);
            }
            catch
            {
                return false;
            }

            IReadOnlyList<XbimPoint3D> nodes = receiver.Nodes;
            IReadOnlyList<int> indices = receiver.Indices;

            if (nodes == null || indices == null || indices.Count < 3)
            {
                return false;
            }

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

                // Check for degenerate triangle
                if (p0.IsEqualTo(p1, tolerance) || p1.IsEqualTo(p2, tolerance) || p2.IsEqualTo(p0, tolerance))
                {
                    continue;
                }

                if (TryCreatePolygon(new[] { p0, p1, p2 }, tolerance, out GeoPolygon3 triPoly))
                {
                    resultFaces.Add(new GeoFace3(triPoly));
                }
            }

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

        private sealed class SimpleMeshReceiver : IXbimMeshReceiver
        {
            private readonly List<XbimPoint3D> _nodes = new List<XbimPoint3D>();
            private readonly List<int> _indices = new List<int>();

            public IReadOnlyList<XbimPoint3D> Nodes => _nodes;
            public IReadOnlyList<int> Indices => _indices;

            public void BeginUpdate() { }
            public void EndUpdate() { }
            public int AddFace() => 0;

            public int AddNode(int faceId, double x, double y, double z)
            {
                _nodes.Add(new XbimPoint3D(x, y, z));
                return _nodes.Count - 1;
            }

            public int AddNode(int faceId, double x, double y, double z, double nX, double nY, double nZ)
            {
                _nodes.Add(new XbimPoint3D(x, y, z));
                return _nodes.Count - 1;
            }

            public int AddNode(int faceId, double x, double y, double z, double nX, double nY, double nZ, double u, double v)
            {
                _nodes.Add(new XbimPoint3D(x, y, z));
                return _nodes.Count - 1;
            }

            public void AddTriangle(int faceId, int a, int b, int c)
            {
                _indices.Add(a);
                _indices.Add(b);
                _indices.Add(c);
            }

            public void AddQuad(int faceId, int a, int b, int c, int d)
            {
                _indices.Add(a);
                _indices.Add(b);
                _indices.Add(c);

                _indices.Add(a);
                _indices.Add(c);
                _indices.Add(d);
            }

            public SurfaceStyling SurfaceStyling { get; set; }
        }
    }
}
