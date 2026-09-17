using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Common.Geometry;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters
{
    /// <summary>
    /// Reads and converts 3D bodies from xBIM to <see cref="GeoSolid3"/>.
    /// <para>
    /// Checks face coplanarity, inner hole loops, and surface orientation.
    /// If the signed volume of the converted solid is negative, the surface is flipped
    /// so that normals point consistently out of the body.
    /// </para>
    /// </summary>
    public static class SolidConvert
    {
        /// <summary>
        /// Reads the axis-aligned bounding box of an xBIM solid as a <see cref="GeoAabb3"/>.
        /// </summary>
        /// <param name="solid">The xBIM solid.</param>
        /// <param name="scale">The coordinate scale factor (default is 1.0).</param>
        /// <returns>The bounding box as a <see cref="GeoAabb3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="solid"/> is null.</exception>
        public static GeoAabb3 ToGeoAabb3(this IXbimSolid solid, double scale = 1.0)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            XbimRect3D box = solid.BoundingBox;
            GeoPoint3 min = new GeoPoint3(box.X * scale, box.Y * scale, box.Z * scale);
            GeoPoint3 max = new GeoPoint3((box.X + box.SizeX) * scale, (box.Y + box.SizeY) * scale, (box.Z + box.SizeZ) * scale);

            return new GeoAabb3(min, max);
        }

        /// <summary>
        /// Tries to convert an xBIM solid to a <see cref="GeoSolid3"/>.
        /// </summary>
        /// <param name="solid">The xBIM solid to convert.</param>
        /// <param name="result">The converted body when successful; otherwise null.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns><c>true</c> if the body was successfully converted into a valid solid of at least 4 faces; otherwise <c>false</c>.</returns>
        public static bool TryToGeoSolid3(this IXbimSolid solid, out GeoSolid3 result, IfcConvertOptions options = null)
        {
            result = null;

            if (solid == null)
            {
                return false;
            }

            options = options ?? new IfcConvertOptions();
            List<GeoFace3> faces = new List<GeoFace3>();

            foreach (IXbimFace face in solid.Faces)
            {
                if (face == null)
                {
                    continue;
                }

                if (face.TryReadFaces(options, out List<GeoFace3> readFaces))
                {
                    faces.AddRange(readFaces);
                }
            }

            if (faces.Count < 4)
            {
                return false;
            }

            try
            {
                GeoSolid3 body = new GeoSolid3(faces);

                // A surface that arrived wound backwards encloses the same volume with negative sign.
                // Flip all faces outward so volume and containment queries behave correctly.
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
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Converts an xBIM geometry object (solid, solid set, or compound geometry) into a list of <see cref="GeoSolid3"/> bodies.
        /// </summary>
        /// <param name="geometryObject">The xBIM geometry object.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>A list of converted <see cref="GeoSolid3"/> bodies.</returns>
        public static List<GeoSolid3> ToGeoSolids(this IXbimGeometryObject geometryObject, IfcConvertOptions options = null)
        {
            List<GeoSolid3> solids = new List<GeoSolid3>();

            if (geometryObject == null)
            {
                return solids;
            }

            options = options ?? new IfcConvertOptions();

            if (geometryObject is IXbimSolid singleSolid)
            {
                if (singleSolid.TryToGeoSolid3(out GeoSolid3 body, options))
                {
                    solids.Add(body);
                }
            }
            else if (geometryObject is IXbimSolidSet solidSet)
            {
                foreach (IXbimSolid solid in solidSet)
                {
                    if (solid.TryToGeoSolid3(out GeoSolid3 body, options))
                    {
                        solids.Add(body);
                    }
                }
            }
            else if (geometryObject is IXbimGeometryObjectSet objectSet)
            {
                foreach (IXbimSolid solid in objectSet.Solids)
                {
                    if (solid.TryToGeoSolid3(out GeoSolid3 body, options))
                    {
                        solids.Add(body);
                    }
                }

                // If no distinct solids exist but the set contains faces that form a closed volume
                if (solids.Count == 0 && objectSet.Faces.Count >= 4)
                {
                    List<GeoFace3> faces = new List<GeoFace3>();
                    foreach (IXbimFace face in objectSet.Faces)
                    {
                        if (face.TryReadFaces(options, out List<GeoFace3> readFaces))
                        {
                            faces.AddRange(readFaces);
                        }
                    }

                    if (faces.Count >= 4)
                    {
                        try
                        {
                            GeoSolid3 body = new GeoSolid3(faces);
                            if (body.GetSignedVolume() < 0.0)
                            {
                                body = new GeoSolid3(faces.Select(f => f.Flip()));
                            }
                            solids.Add(body);
                        }
                        catch (ArgumentException)
                        {
                            // Ignore non-solid face sets
                        }
                    }
                }
            }

            return solids;
        }

        /// <summary>
        /// Converts an IFC geometric representation item into a list of <see cref="GeoSolid3"/> bodies.
        /// </summary>
        /// <param name="item">The IFC geometric representation item.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>A list of converted <see cref="GeoSolid3"/> bodies.</returns>
        public static List<GeoSolid3> ToGeoSolids(this IIfcGeometricRepresentationItem item, IfcConvertOptions options = null)
        {
            List<GeoSolid3> solids = new List<GeoSolid3>();

            if (item == null)
            {
                return solids;
            }

            options = options ?? new IfcConvertOptions();
            IXbimGeometryEngine engine = IfcEngineContext.CurrentEngine;

            try
            {
                using (IXbimGeometryObject geomObj = engine.Create(item))
                {
                    if (geomObj != null)
                    {
                        solids.AddRange(geomObj.ToGeoSolids(options));
                    }
                }
            }
            catch
            {
                // Return whatever succeeded; invalid or degenerate representation items are safely skipped
            }

            return solids;
        }
    }
}
