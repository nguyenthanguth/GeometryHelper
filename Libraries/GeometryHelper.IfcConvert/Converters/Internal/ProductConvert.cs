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

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Extension methods for converting <see cref="IIfcProduct"/> and <see cref="IIfcMappedItem"/> instances
    /// to <see cref="GeoSolid3"/> bodies and <see cref="IfcProductGeometry"/>.
    /// </summary>
    internal static class ProductConvert
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

            Tolerance tol = (tolerance ?? Tolerance.Global).ForConstruction();

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

        /// <summary>
        /// Transforms a face with <paramref name="tolerance"/>, which is already the construction tolerance
        /// (<see cref="ToleranceExtensions.ForConstruction"/>). False when its boundary degenerates; a hole that
        /// degenerates is dropped from the face.
        /// </summary>
        internal static bool TryTransformFace(GeoFace3 face, GeoTransform3 transform, Tolerance tolerance, out GeoFace3 result)
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
            return product.ToProductGeometry(options, 0);
        }

        // Aggregation trees are shallow in practice; the limit only guards against malformed cyclic files.
        private const int MaxAggregationDepth = 16;

        private static IfcProductGeometry ToProductGeometry(this IIfcProduct product, IfcConvertOptions options, int depth)
        {
            if (product == null)
            {
                return CreateProductGeometry(null, Enumerable.Empty<GeoSolid3>());
            }

            options = options ?? new IfcConvertOptions();

            // Before any geometry is built, so that the products left out cost nothing.
            string name = product.Name?.ToString();
            if (IsNameSkipped(name, options.SkipNames) || !IsNameIncluded(name, options.OnlyNames))
            {
                return CreateProductGeometry(product, Enumerable.Empty<GeoSolid3>());
            }

            if (product.ObjectPlacement == null ||
                (product.Representation == null && !options.IncludeAggregatedParts))
            {
                return CreateProductGeometry(product, Enumerable.Empty<GeoSolid3>());
            }

            List<string> warnings = new List<string>();

            // xBIM throws for placements it does not implement (IfcLinearPlacement, IfcGridPlacement). One such
            // product must not abort a model-wide enumeration, so it is returned empty with the reason.
            XbimMatrix3D placementMatrix;
            try
            {
                placementMatrix = product.ObjectPlacement.ToMatrix3D();
            }
            catch (Exception ex)
            {
                warnings.Add($"#{product.ObjectPlacement.EntityLabel} {IfcTypeNames.GetName(product.ObjectPlacement)}: " +
                    $"The placement cannot be evaluated ({ex.GetType().Name}: {ex.Message}); no geometry was converted.");
                return CreateProductGeometry(product, Enumerable.Empty<GeoSolid3>(), null, null, warnings);
            }

            GeoTransform3 placementTransform = placementMatrix.ToGeoTransform3(options.ScaleFactor);

            List<GeoSolid3> allSolids = null;

            if (product.Representation == null)
            {
                allSolids = new List<GeoSolid3>();
            }
            else if (options.ApplyVoids && product is IIfcElement element && element.HasOpenings != null && element.HasOpenings.Any())
            {
                // Preferred: cut the exact B-rep in the geometry engine. Falls back to the mesh boolean on
                // converted solids when an opening or a host item cannot be handled that way.
                if (VoidConvert.TryCollectCutters(element, placementMatrix, out List<IXbimSolid> cutters))
                {
                    try
                    {
                        // Messages from an attempt that is thrown away would describe geometry that is not returned.
                        List<string> cutWarnings = new List<string>();
                        List<GeoSolid3> cutSolids = CollectBodySolids(product, options, placementTransform, cutters, out bool allCut, cutWarnings);
                        if (allCut)
                        {
                            allSolids = cutSolids;
                            warnings.AddRange(cutWarnings);
                        }
                    }
                    finally
                    {
                        VoidConvert.DisposeAll(cutters);
                    }
                }

                if (allSolids == null)
                {
                    warnings.Add("Openings could not be cut in the geometry engine; the GeoSolid3 boolean fallback was used, " +
                        "which is unreliable on curved or triangulated bodies.");
                    allSolids = CollectBodySolids(product, options, placementTransform, null, out _, warnings);
                    if (allSolids.Count > 0)
                    {
                        allSolids = ApplyElementVoids(element, allSolids, placementTransform, options, warnings);
                    }
                }
            }
            else
            {
                allSolids = CollectBodySolids(product, options, placementTransform, null, out _, warnings);
            }

            if (options.IncludeAggregatedParts && depth < MaxAggregationDepth)
            {
                allSolids.AddRange(CollectAggregatedParts(product, options, placementTransform, depth, warnings));
            }

            allSolids = DropFlatSolids(allSolids, options, warnings);

            for (int i = 0; i < allSolids.Count; i++)
            {
                if (!allSolids[i].IsClosed(options.Tolerance))
                {
                    warnings.Add($"Solid {i + 1} of {allSolids.Count} is not closed; its volume and point containment may be off.");
                }
            }

            return CreateProductGeometry(product, allSolids, null, placementTransform, warnings);
        }

        /// <summary>
        /// Converts the parts a product aggregates (IfcRelAggregates), e.g. the plates and profiles of a Tekla
        /// IfcElementAssembly, which usually has no body of its own. Parts carry their own placements, so they
        /// are converted globally and, for local output, brought into the aggregating product's frame.
        /// </summary>
        private static List<GeoSolid3> CollectAggregatedParts(IIfcProduct product, IfcConvertOptions options,
            GeoTransform3 placementTransform, int depth, List<string> warnings)
        {
            List<GeoSolid3> solids = new List<GeoSolid3>();
            IfcConvertOptions partOptions = options.Clone();
            partOptions.CoordinateSpace = CoordinateSpace.Global;

            GeoTransform3 globalToHost = null;
            if (options.CoordinateSpace == CoordinateSpace.Local && !placementTransform.TryGetInverse(out globalToHost, options.Tolerance))
            {
                warnings.Add("The placement cannot be inverted, so aggregated parts could not be expressed in local coordinates.");
                return solids;
            }

            foreach (IIfcRelAggregates rel in product.IsDecomposedBy.OfType<IIfcRelAggregates>())
            {
                foreach (IIfcProduct part in rel.RelatedObjects.OfType<IIfcProduct>())
                {
                    IfcProductGeometry partGeometry = part.ToProductGeometry(partOptions, depth + 1);
                    foreach (string message in partGeometry.Warnings)
                    {
                        warnings.Add($"Part {part.GlobalId}: {message}");
                    }

                    foreach (GeoSolid3 solid in partGeometry.Solids)
                    {
                        solids.Add(globalToHost != null ? TransformChecked(solid, globalToHost, options, warnings) : solid);
                    }
                }
            }

            return solids;
        }

        /// <summary>
        /// Removes "solids" that enclose no volume, such as four coplanar faces exported as a door leaf's
        /// swing symbol. A body counts as flat when its average thickness (volume / surface area) is within the
        /// point tolerance, which is scale-consistent whatever the output unit.
        /// </summary>
        private static List<GeoSolid3> DropFlatSolids(List<GeoSolid3> solids, IfcConvertOptions options, List<string> warnings)
        {
            List<GeoSolid3> kept = new List<GeoSolid3>(solids.Count);
            int dropped = 0;

            foreach (GeoSolid3 solid in solids)
            {
                double area = solid.Faces.Sum(f => f.Area);
                if (area > 0.0 && solid.Volume <= options.Tolerance.EqualPoint * area)
                {
                    dropped++;
                    continue;
                }

                kept.Add(solid);
            }

            if (dropped > 0)
            {
                warnings.Add($"{dropped} flat body(ies) enclosing no volume were left out.");
            }

            return kept;
        }

        /// <summary>
        /// Transforms a solid and reports faces that could not be rebuilt at their new position.
        /// </summary>
        private static GeoSolid3 TransformChecked(GeoSolid3 solid, GeoTransform3 transform, IfcConvertOptions options, List<string> warnings)
        {
            GeoSolid3 moved = solid.Transform(transform, options.Tolerance);
            int lost = solid.Faces.Count - moved.Faces.Count;
            if (lost > 0)
            {
                warnings?.Add($"{lost} face(s) degenerated when the solid was placed and were left out.");
            }

            return moved;
        }

        /// <summary>
        /// Converts the body representation items of a product, optionally cutting engine opening solids
        /// (in the product's local frame) from each geometric item first.
        /// </summary>
        private static List<GeoSolid3> CollectBodySolids(IIfcProduct product, IfcConvertOptions options,
            GeoTransform3 placementTransform, IReadOnlyList<IXbimSolid> cutters, out bool allCut, List<string> warnings)
        {
            List<GeoSolid3> allSolids = new List<GeoSolid3>();
            allCut = true;

            foreach (IIfcRepresentation representation in SelectBodyRepresentations(product.Representation.Representations))
            {
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
                            List<GeoSolid3> itemSolids = geometricItem.ToGeoSolids(options, cutters, out bool itemCut, warnings);
                            allCut &= itemCut;
                            foreach (GeoSolid3 s in itemSolids)
                            {
                                if (options.CoordinateSpace == CoordinateSpace.Local)
                                {
                                    allSolids.Add(s);
                                }
                                else
                                {
                                    allSolids.Add(TransformChecked(s, placementTransform, options, warnings));
                                }
                            }
                        }
                        else if (item is IIfcMappedItem mappedItem)
                        {
                            // Mapped items are converted from GeoSolid3 pieces, so engine cutters cannot reach them.
                            allCut &= cutters == null || cutters.Count == 0;

                            List<GeoTransform3> initialTransforms = options.CoordinateSpace == CoordinateSpace.Local
                                ? new List<GeoTransform3>()
                                : new List<GeoTransform3> { placementTransform };
                            CollectMappedItemSolids(mappedItem, initialTransforms, options, allSolids, warnings);
                        }
                    }
                    catch (Exception ex)
                    {
                        // A failing item is skipped rather than failing the whole product, but it is reported.
                        warnings.Add($"#{item.EntityLabel} {IfcTypeNames.GetName(item)}: Conversion failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            return allSolids;
        }

        // Representation identifiers (IFC4 "Shape representation identifiers") that never describe the physical body.
        private static readonly HashSet<string> NonBodyIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Axis", "Box", "FootPrint", "Profile", "Annotation", "Clearance", "Reference",
            "SurveyPoints", "Lighting", "CoG", "Surface"
        };

        /// <summary>
        /// Picks the representations that describe the physical body of a product.
        /// Converting every representation would add the 'Box' bounding box, clearance zones, etc. as extra solids.
        /// Preference order: "Body", then "Body-*" variants (e.g. "Body-FallBack"), then "Facetation",
        /// then any representation not known to be non-body (exporters that leave the identifier unset).
        /// </summary>
        internal static List<IIfcRepresentation> SelectBodyRepresentations(IEnumerable<IIfcRepresentation> representations)
        {
            List<IIfcRepresentation> candidates = representations?
                .Where(r => r?.Items != null && r.Items.Any())
                .ToList() ?? new List<IIfcRepresentation>();

            string IdOf(IIfcRepresentation r) => r.RepresentationIdentifier?.ToString() ?? string.Empty;

            List<IIfcRepresentation> selected = candidates.Where(r => string.Equals(IdOf(r), "Body", StringComparison.OrdinalIgnoreCase)).ToList();
            if (selected.Count == 0)
            {
                selected = candidates.Where(r => IdOf(r).StartsWith("Body", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (selected.Count == 0)
            {
                selected = candidates.Where(r => string.Equals(IdOf(r), "Facetation", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (selected.Count == 0)
            {
                selected = candidates.Where(r => !NonBodyIdentifiers.Contains(IdOf(r))).ToList();
            }

            return selected;
        }

        private static IfcProductGeometry CreateProductGeometry(
            IIfcProduct product,
            IEnumerable<GeoSolid3> solids,
            IEnumerable<GeoFace3> openSurfaces = null,
            GeoTransform3 placement = null,
            IEnumerable<string> warnings = null)
        {
            string globalId = product?.GlobalId.ToString() ?? string.Empty;
            string name = product?.Name?.ToString() ?? string.Empty;
            string ifcType = IfcTypeNames.GetName(product);
            string tag = (product as IIfcElement)?.Tag?.ToString() ?? string.Empty;
            return new IfcProductGeometry(globalId, name, ifcType, solids, openSurfaces, placement, tag, warnings);
        }

        private static List<GeoSolid3> ApplyElementVoids(IIfcElement element, List<GeoSolid3> solids, GeoTransform3 hostPlacement,
            IfcConvertOptions options, List<string> warnings)
        {
            List<GeoSolid3> openingSolids = new List<GeoSolid3>();

            // Openings have their own placement (usually relative to the host), so they are always converted
            // in global coordinates and, for local output, brought back into the host's local frame.
            // Name patterns target products the caller asked for, not the openings that cut them.
            IfcConvertOptions openingOptions = options.Clone();
            openingOptions.CoordinateSpace = CoordinateSpace.Global;
            openingOptions.ApplyVoids = false;
            openingOptions.SkipNames.Clear();
            openingOptions.OnlyNames.Clear();

            GeoTransform3 globalToHost = null;
            if (options.CoordinateSpace == CoordinateSpace.Local && !hostPlacement.TryGetInverse(out globalToHost, options.Tolerance))
            {
                warnings.Add("The placement cannot be inverted, so openings were not subtracted.");
                return solids;
            }

            foreach (IIfcRelVoidsElement rel in element.HasOpenings)
            {
                if (rel?.RelatedOpeningElement is IIfcProduct openingProduct)
                {
                    List<GeoSolid3> opening = openingProduct.ToGeoSolids(openingOptions);
                    if (opening.Count == 0)
                    {
                        warnings.Add($"Opening {openingProduct.GlobalId} has no solid to cut with.");
                    }

                    foreach (GeoSolid3 openingSolid in opening)
                    {
                        openingSolids.Add(globalToHost != null ? TransformChecked(openingSolid, globalToHost, options, warnings) : openingSolid);
                    }
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
                    else
                    {
                        warnings.Add("An opening could not be subtracted by the GeoSolid3 boolean.");
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
            List<GeoSolid3> targetSolids,
            List<string> warnings)
        {
            if (mappedItem?.MappingSource?.MappedRepresentation?.Items == null || mappedItem.MappingTarget == null)
            {
                return;
            }

            // The mapped representation is first placed by the map's MappingOrigin, then by the item's MappingTarget
            // (world = Target * Origin * local), as IfcOpenShell does. xBIM's scene builder composes them the other way.
            GeoTransform3 mappingTransform = mappedItem.MappingTarget.ToMatrix3D().ToGeoTransform3(options.ScaleFactor);
            IIfcAxis2Placement mappingOrigin = mappedItem.MappingSource.MappingOrigin;
            if (mappingOrigin != null)
            {
                mappingTransform = mappingTransform * mappingOrigin.ToMatrix3D().ToGeoTransform3(options.ScaleFactor);
            }

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
                        List<GeoSolid3> solids = geometricItem.ToGeoSolids(options, null, out _, warnings);
                        foreach (GeoSolid3 s in solids)
                        {
                            targetSolids.Add(TransformChecked(s, cumulativeTransform, options, warnings));
                        }
                    }
                    else if (item is IIfcMappedItem nestedMappedItem)
                    {
                        List<GeoTransform3> nextOuterTransforms = new List<GeoTransform3>(outerTransforms.Count + 1) { mappingTransform };
                        nextOuterTransforms.AddRange(outerTransforms);
                        CollectMappedItemSolids(nestedMappedItem, nextOuterTransforms, options, targetSolids, warnings);
                    }
                }
                catch (Exception ex)
                {
                    // A failing mapped component is skipped rather than failing the product, but it is reported.
                    warnings.Add($"#{item.EntityLabel} {IfcTypeNames.GetName(item)}: Conversion failed: {ex.GetType().Name}: {ex.Message}");
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

        /// <summary>
        /// Checks whether a product name passes the only-names collection: always when the collection is empty,
        /// otherwise when the name matches one of its patterns. A product with no name matches only "*".
        /// </summary>
        public static bool IsNameIncluded(string productName, ICollection<string> onlyNames)
        {
            if (onlyNames == null || onlyNames.Count == 0)
            {
                return true;
            }

            string name = productName ?? string.Empty;
            foreach (string pattern in onlyNames)
            {
                if (MatchesWildcard(name, pattern))
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
