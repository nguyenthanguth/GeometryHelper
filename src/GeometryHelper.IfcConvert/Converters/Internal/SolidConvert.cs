using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Core.Internal;
using GeometryHelper.Geometry;
using Xbim.Common.Geometry;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Reads and converts 3D bodies from xBIM to <see cref="GeoSolid3"/>.
    /// <para>
    /// Checks face coplanarity, inner hole loops, and surface orientation.
    /// If the signed volume of the converted solid is negative, the surface is flipped
    /// so that normals point consistently out of the body.
    /// </para>
    /// </summary>
    internal static class SolidConvert
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
        /// <param name="warnings">Receives a message for every problem met, or null to ignore them.</param>
        /// <returns><c>true</c> if the body was successfully converted into a valid solid of at least 4 faces; otherwise <c>false</c>.</returns>
        public static bool TryToGeoSolid3(this IXbimSolid solid, out GeoSolid3 result, IfcConvertOptions options = null,
            ICollection<string> warnings = null)
        {
            result = null;

            if (solid == null)
            {
                return false;
            }

            options = options ?? new IfcConvertOptions();

            if (options.TessellateNonPlanarFaces && MeshConvert.RequiresMeshing(solid, options.Tolerance.EqualPoint / options.ScaleFactor))
            {
                return MeshConvert.TryMeshSolid(solid, options, out result, warnings);
            }

            List<GeoFace3> faces = new List<GeoFace3>();
            int unreadFaces = 0;

            foreach (IXbimFace face in solid.Faces)
            {
                if (face == null)
                {
                    continue;
                }

                if (face.TryReadFaces(options, out List<GeoFace3> readFaces, warnings))
                {
                    faces.AddRange(readFaces);
                }
                else
                {
                    unreadFaces++;
                }
            }

            if (unreadFaces > 0)
            {
                warnings?.Add($"{unreadFaces} face(s) could not be read as polygons and were left out.");
            }

            if (faces.Count < 4)
            {
                warnings?.Add(unreadFaces > 0
                    ? $"Only {faces.Count} face(s) could be read; a solid needs at least 4."
                    : $"A shell of {faces.Count} face(s) cannot enclose a volume and was left out.");
                return false;
            }

            try
            {
                // Faces may arrive with mixed orientations (boolean results); make them all point outwards.
                result = new GeoSolid3(FaceOrientation.Orient(faces, options.Tolerance));
                return true;
            }
            catch (ArgumentException ex)
            {
                warnings?.Add("The faces do not form a solid: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Converts an xBIM geometry object (solid, solid set, or compound geometry) into a list of <see cref="GeoSolid3"/> bodies.
        /// </summary>
        /// <param name="geometryObject">The xBIM geometry object.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <param name="warnings">Receives a message for every problem met, or null to ignore them.</param>
        /// <returns>A list of converted <see cref="GeoSolid3"/> bodies.</returns>
        public static List<GeoSolid3> ToGeoSolids(this IXbimGeometryObject geometryObject, IfcConvertOptions options = null,
            ICollection<string> warnings = null)
        {
            List<GeoSolid3> solids = new List<GeoSolid3>();

            if (geometryObject == null)
            {
                return solids;
            }

            options = options ?? new IfcConvertOptions();

            if (geometryObject is IXbimSolid singleSolid)
            {
                if (singleSolid.TryToGeoSolid3(out GeoSolid3 body, options, warnings))
                {
                    solids.Add(body);
                }
            }
            else if (geometryObject is IXbimSolidSet solidSet)
            {
                foreach (IXbimSolid solid in solidSet)
                {
                    if (solid.TryToGeoSolid3(out GeoSolid3 body, options, warnings))
                    {
                        solids.Add(body);
                    }
                }
            }
            else if (geometryObject is IXbimGeometryObjectSet objectSet)
            {
                foreach (IXbimSolid solid in objectSet.Solids)
                {
                    if (solid.TryToGeoSolid3(out GeoSolid3 body, options, warnings))
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
                        if (face.TryReadFaces(options, out List<GeoFace3> readFaces, warnings))
                        {
                            faces.AddRange(readFaces);
                        }
                    }

                    if (faces.Count >= 4)
                    {
                        try
                        {
                            solids.Add(new GeoSolid3(FaceOrientation.Orient(faces, options.Tolerance)));
                        }
                        catch (ArgumentException ex)
                        {
                            warnings?.Add("The loose faces do not form a solid: " + ex.Message);
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
            return item.ToGeoSolids(options, null, out _, null);
        }

        /// <summary>
        /// Converts an IFC geometric representation item into <see cref="GeoSolid3"/> bodies,
        /// first cutting the given opening solids from it in the geometry engine.
        /// </summary>
        /// <param name="item">The IFC geometric representation item.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <param name="cutters">Opening solids in the item's frame and model units, or null for none.</param>
        /// <param name="cutApplied">true when there were no cutters or all of them were cut successfully.</param>
        /// <param name="warnings">Receives a message, prefixed with the item's entity label, for every problem met; may be null.</param>
        /// <returns>A list of converted <see cref="GeoSolid3"/> bodies.</returns>
        public static List<GeoSolid3> ToGeoSolids(this IIfcGeometricRepresentationItem item, IfcConvertOptions options,
            IReadOnlyList<IXbimSolid> cutters, out bool cutApplied, ICollection<string> warnings)
        {
            List<GeoSolid3> solids = new List<GeoSolid3>();
            cutApplied = cutters == null || cutters.Count == 0;

            if (item == null)
            {
                return solids;
            }

            options = MeshConvert.ResolveDeflection(options ?? new IfcConvertOptions(), item.Model);
            IXbimGeometryEngine engine = IfcEngineContext.CurrentEngine;

            // Messages from the layers below are collected here and prefixed with the item they belong to.
            List<string> itemWarnings = warnings == null ? null : new List<string>();

            try
            {
                using (IXbimGeometryObject geomObj = engine.Create(item))
                {
                    if (geomObj == null)
                    {
                        itemWarnings?.Add("The geometry engine could not build it.");
                    }
                    else if (!geomObj.IsValid)
                    {
                        // Reading the faces of an invalid shape (an open shell, say) raises an
                        // AccessViolationException inside the native engine, which is not always catchable.
                        itemWarnings?.Add("The geometry engine reports the shape as invalid; it was skipped.");
                    }
                    else if (cutApplied)
                    {
                        solids.AddRange(geomObj.ToGeoSolids(options, itemWarnings));
                    }
                    else
                    {
                        double precision = item.Model?.ModelFactors?.Precision ?? options.Tolerance.EqualPoint / options.ScaleFactor;
                        if (VoidConvert.TryCut(geomObj, cutters, precision, out List<IXbimSolid> cut))
                        {
                            cutApplied = true;

                            // Boolean results are read through the engine's own triangulation only. Walking their
                            // wires face by face (IXbimWire.Points) can fault inside the native engine with an
                            // AccessViolationException, which .NET Framework cannot catch and which ends the process.
                            foreach (IXbimSolid solid in cut)
                            {
                                if (MeshConvert.TryMeshSolid(solid, options, out GeoSolid3 body, itemWarnings))
                                {
                                    solids.Add(body);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Return whatever succeeded; invalid or degenerate representation items are skipped, not fatal.
                itemWarnings?.Add($"Conversion failed: {ex.GetType().Name}: {ex.Message}");
            }

            if (itemWarnings != null)
            {
                if (solids.Count == 0 && itemWarnings.Count == 0 && cutApplied)
                {
                    itemWarnings.Add("It produced no solid.");
                }

                string itemName = $"#{item.EntityLabel} {IfcTypeNames.GetName(item)}";
                foreach (string message in itemWarnings)
                {
                    warnings.Add(itemName + ": " + message);
                }
            }

            return solids;
        }
    }
}
