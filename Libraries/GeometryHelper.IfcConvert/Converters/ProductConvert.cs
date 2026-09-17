using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.CommonGeometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.SolidGeometry.Geometry;
using Xbim.Common.Geometry;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters
{
    /// <summary>
    /// Extension methods for converting <see cref="IIfcProduct"/> and <see cref="IIfcMappedItem"/> instances
    /// to <see cref="GeoSolid3"/> bodies and <see cref="IfcProductGeometry"/>.
    /// </summary>
    public static class ProductConvert
    {
        /// <summary>
        /// Transforms a <see cref="GeoSolid3"/> by a <see cref="GeoTransform3"/> transformation matrix.
        /// </summary>
        /// <param name="solid">The solid to transform.</param>
        /// <param name="transform">The transformation matrix.</param>
        /// <param name="tolerance">Geometric tolerance (defaults to <see cref="Tolerance.Global"/>).</param>
        /// <returns>A new transformed <see cref="GeoSolid3"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="solid"/> or <paramref name="transform"/> is null.</exception>
        public static GeoSolid3 Transform(this GeoSolid3 solid, GeoTransform3 transform, Tolerance? tolerance = null)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            if (transform == null) throw new ArgumentNullException(nameof(transform));

            Tolerance tol = tolerance ?? Tolerance.Global;

            List<GeoFace3> transformedFaces = new List<GeoFace3>(solid.Faces.Count);
            foreach (GeoFace3 face in solid.Faces)
            {
                if (TryTransformFace(face, transform, tol, out GeoFace3 tf))
                {
                    transformedFaces.Add(tf);
                }
            }

            List<GeoSolid3> transformedOpenings = new List<GeoSolid3>(solid.Openings.Count);
            foreach (GeoSolid3 opening in solid.Openings)
            {
                transformedOpenings.Add(opening.Transform(transform, tol));
            }

            GeoSolid3 result = new GeoSolid3(transformedFaces, transformedOpenings);

            if (result.GetSignedVolume() < 0.0)
            {
                List<GeoFace3> flipped = new List<GeoFace3>(result.Faces.Count);
                foreach (GeoFace3 f in result.Faces)
                {
                    flipped.Add(f.Flip());
                }

                result = new GeoSolid3(flipped, result.Openings);
            }

            return result;
        }

        private static bool TryTransformFace(GeoFace3 face, GeoTransform3 transform, Tolerance tolerance, out GeoFace3 result)
        {
            result = null;

            if (!TryTransformPolygon(face.Boundary, transform, tolerance, out GeoPolygon3 boundary))
            {
                return false;
            }

            List<GeoPolygon3> holes = new List<GeoPolygon3>(face.Holes.Count);
            foreach (GeoPolygon3 hole in face.Holes)
            {
                if (TryTransformPolygon(hole, transform, tolerance, out GeoPolygon3 th))
                {
                    holes.Add(th);
                }
            }

            try
            {
                result = holes.Count > 0 ? new GeoFace3(boundary, holes, tolerance) : new GeoFace3(boundary);
                if (transform.GetDeterminant() < 0.0)
                {
                    result = result.Flip();
                }
                return true;
            }
            catch (ArgumentException)
            {
                result = new GeoFace3(boundary);
                if (transform.GetDeterminant() < 0.0)
                {
                    result = result.Flip();
                }
                return true;
            }
        }

        private static bool TryTransformPolygon(GeoPolygon3 polygon, GeoTransform3 transform, Tolerance tolerance, out GeoPolygon3 result)
        {
            result = null;
            try
            {
                List<GeoPoint3> points = polygon.Vertices.Select(v => transform.Transform(v)).ToList();
                result = new GeoPolygon3(points, tolerance);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Converts an <see cref="IIfcProduct"/> into a sequence of <see cref="GeoSolid3"/> bodies in global coordinates.
        /// </summary>
        /// <param name="product">The IFC product to convert.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>A list of converted <see cref="GeoSolid3"/> bodies in global coordinates.</returns>
        public static List<GeoSolid3> ToGeoSolids(this IIfcProduct product, IfcConvertOptions options = null)
        {
            IfcProductGeometry geometry = product.ToProductGeometry(options);
            return geometry.Solids.ToList();
        }

        /// <summary>
        /// Converts an <see cref="IIfcProduct"/> into complete <see cref="IfcProductGeometry"/>,
        /// including closed solids, open surfaces, and bounding box.
        /// </summary>
        /// <param name="product">The IFC product to convert.</param>
        /// <param name="options">Optional conversion options.</param>
        /// <returns>The complete <see cref="IfcProductGeometry"/>.</returns>
        public static IfcProductGeometry ToProductGeometry(this IIfcProduct product, IfcConvertOptions options = null)
        {
            if (product == null)
            {
                return new IfcProductGeometry(null, Enumerable.Empty<GeoSolid3>());
            }

            options = options ?? new IfcConvertOptions();

            if (IsNameSkipped(product.Name?.ToString(), options.SkipNames))
            {
                return new IfcProductGeometry(product, Enumerable.Empty<GeoSolid3>());
            }

            if (product.Representation == null || product.ObjectPlacement == null)
            {
                return new IfcProductGeometry(product, Enumerable.Empty<GeoSolid3>());
            }

            XbimMatrix3D placementMatrix = product.ObjectPlacement.ToMatrix3D();
            GeoTransform3 placementTransform = placementMatrix.ToGeoTransform3(options.ScaleFactor);

            List<GeoSolid3> allSolids = new List<GeoSolid3>();

            foreach (IIfcRepresentation representation in product.Representation.Representations)
            {
                if (representation?.Items == null)
                {
                    continue;
                }

                foreach (IIfcRepresentationItem item in representation.Items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (item is IIfcGeometricRepresentationItem geometricItem)
                        {
                            List<GeoSolid3> itemSolids = geometricItem.ToGeoSolids(options);
                            foreach (GeoSolid3 s in itemSolids)
                            {
                                allSolids.Add(s.Transform(placementTransform, options.Tolerance));
                            }
                        }
                        else if (item is IIfcMappedItem mappedItem)
                        {
                            CollectMappedItemSolids(mappedItem, new List<GeoTransform3> { placementTransform }, options, allSolids);
                        }
                    }
                    catch
                    {
                        // Safely skip any degenerate representation items without crashing the product conversion
                    }
                }
            }

            // Subtract voids and openings (e.g. window/door cutouts) if enabled
            if (options.ApplyVoids && product is IIfcElement element && element.HasOpenings != null && allSolids.Count > 0)
            {
                allSolids = ApplyElementVoids(element, allSolids, options);
            }

            return new IfcProductGeometry(product, allSolids, null, placementTransform);
        }

        private static List<GeoSolid3> ApplyElementVoids(IIfcElement element, List<GeoSolid3> solids, IfcConvertOptions options)
        {
            List<GeoSolid3> openingSolids = new List<GeoSolid3>();

            foreach (IIfcRelVoidsElement rel in element.HasOpenings)
            {
                if (rel?.RelatedOpeningElement is IIfcProduct openingProduct)
                {
                    IfcConvertOptions openingOptions = new IfcConvertOptions
                    {
                        Tolerance = options.Tolerance,
                        ScaleFactor = options.ScaleFactor,
                        TessellateNonPlanarFaces = options.TessellateNonPlanarFaces,
                        DeflectionTolerance = options.DeflectionTolerance,
                        ApplyVoids = false
                    };

                    List<GeoSolid3> opSolids = openingProduct.ToGeoSolids(openingOptions);
                    openingSolids.AddRange(opSolids);
                }
            }

            if (openingSolids.Count == 0)
            {
                return solids;
            }

            List<GeoSolid3> cutSolids = new List<GeoSolid3>(solids.Count);
            foreach (GeoSolid3 solid in solids)
            {
                GeoSolid3 currentSolid = solid;
                foreach (GeoSolid3 openingSolid in openingSolids)
                {
                    if (currentSolid.TrySubtract(openingSolid, out GeoSolid3 cut, options.Tolerance))
                    {
                        currentSolid = cut;
                    }
                }
                cutSolids.Add(currentSolid);
            }

            return cutSolids;
        }

        /// <summary>
        /// Recursively collects and transforms solids from an <see cref="IIfcMappedItem"/> (e.g. type representations, assemblies).
        /// </summary>
        private static void CollectMappedItemSolids(
            IIfcMappedItem mappedItem,
            List<GeoTransform3> outerTransforms,
            IfcConvertOptions options,
            List<GeoSolid3> targetSolids)
        {
            if (mappedItem?.MappingSource?.MappedRepresentation?.Items == null || mappedItem.MappingTarget == null)
            {
                return;
            }

            XbimMatrix3D mappingMatrix = mappedItem.MappingTarget.ToMatrix3D();
            GeoTransform3 mappingTransform = mappingMatrix.ToGeoTransform3(options.ScaleFactor);

            // Compute cumulative transform for this mapped level: outerTransforms * mappingTransform
            GeoTransform3 cumulativeTransform = mappingTransform;
            foreach (GeoTransform3 outer in outerTransforms)
            {
                cumulativeTransform = outer * cumulativeTransform;
            }

            foreach (IIfcRepresentationItem item in mappedItem.MappingSource.MappedRepresentation.Items)
            {
                if (item == null)
                {
                    continue;
                }

                try
                {
                    if (item is IIfcGeometricRepresentationItem geometricItem)
                    {
                        List<GeoSolid3> solids = geometricItem.ToGeoSolids(options);
                        foreach (GeoSolid3 s in solids)
                        {
                            targetSolids.Add(s.Transform(cumulativeTransform, options.Tolerance));
                        }
                    }
                    else if (item is IIfcMappedItem nestedMappedItem)
                    {
                        List<GeoTransform3> nextOuterTransforms = new List<GeoTransform3>(outerTransforms.Count + 1) { mappingTransform };
                        nextOuterTransforms.AddRange(outerTransforms);
                        CollectMappedItemSolids(nestedMappedItem, nextOuterTransforms, options, targetSolids);
                    }
                }
                catch
                {
                    // Ignore failures in individual mapped components
                }
            }
        }

        /// <summary>
        /// Checks whether a product name matches any pattern in the skip names collection.
        /// Supports '*' wildcard matching (e.g. "W*" matches "W1", "W2").
        /// </summary>
        public static bool IsNameSkipped(string productName, ICollection<string> skipNames)
        {
            if (string.IsNullOrEmpty(productName) || skipNames == null || skipNames.Count == 0)
            {
                return false;
            }

            foreach (string pattern in skipNames)
            {
                if (MatchesWildcard(productName, pattern))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesWildcard(string name, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            if (!pattern.Contains('*'))
            {
                return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
            }

            string regexPattern = "^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$";
            return Regex.IsMatch(name, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}
