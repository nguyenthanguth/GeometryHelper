using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using Xbim.Common.Geometry;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Reads the balls of an IFC model, the <c>IfcSphere</c> of its solid geometry, as <see cref="GeoSphere3"/>: a centre and a
    /// radius, where <see cref="SolidConvert"/> gives the same ball as a mesh of triangles inside it.
    /// <para>
    /// An <c>IfcSphere</c> comes as a representation item of a product, as the root of an <c>IfcCsgSolid</c>, or inside a mapped item,
    /// whose mapping transformation (a scaling included, where it is even) is applied to it; each is read. A sphere inside a Boolean result (a
    /// ball cut by a plane or from a box) is not a ball and is not read, and neither is a ball of a product that openings cut when they are
    /// applied, which a ball with a void in it is not.
    /// </para>
    /// </summary>
    internal static class SphereConvert
    {
        // Aggregation trees are shallow in practice; the limit only guards against malformed cyclic files, as for solids.
        private const int MaxAggregationDepth = 16;

        /// <summary>
        /// What reading the balls of a product met that is said once for the call: the items that are not spheres.
        /// </summary>
        private sealed class Tally
        {
            internal int NotSpheres;
        }

        /// <summary>
        /// Gets the sphere an item is: the item itself when it is an <c>IfcSphere</c>, the root of an <c>IfcCsgSolid</c> when that is one,
        /// otherwise null.
        /// </summary>
        private static IIfcSphere SphereOf(IIfcRepresentationItem item) => item as IIfcSphere ?? (item as IIfcCsgSolid)?.TreeRootExpression as IIfcSphere;

        /// <summary>
        /// Tries to read a representation item as a <see cref="GeoSphere3"/>, in the frame of the item.
        /// </summary>
        /// <param name="item">The representation item: an <c>IfcSphere</c>, or an <c>IfcCsgSolid</c> whose root is one.</param>
        /// <param name="result">The ball when the method returns true: its centre the sphere's position, its radius its radius, both scaled by the options.</param>
        /// <param name="options">Optional conversion options; <see cref="IfcConvertOptions.ScaleFactor"/> scales the centre and the radius.</param>
        /// <param name="warnings">Receives a message, prefixed with the item's entity label, for an <c>IfcSphere</c> that cannot be read; null to ignore it.</param>
        /// <returns><c>true</c> when the item is a sphere that could be read; <c>false</c> for any other item, with no message, or for a sphere the file gives no usable radius or position, with one.</returns>
        public static bool TryToGeoSphere3(this IIfcRepresentationItem item, out GeoSphere3 result, IfcConvertOptions options = null,
            ICollection<string> warnings = null)
        {
            IIfcSphere sphere = SphereOf(item);

            if (sphere == null)
            {
                result = default(GeoSphere3);
                return false;
            }

            return TryRead(sphere, out result, options ?? new IfcConvertOptions(), warnings);
        }

        private static bool TryRead(IIfcSphere sphere, out GeoSphere3 result, IfcConvertOptions options, ICollection<string> warnings)
        {
            result = default(GeoSphere3);
            string itemName = $"#{sphere.EntityLabel} {IfcTypeNames.GetName(sphere)}";

            if (sphere.Position == null)
            {
                warnings?.Add($"{itemName}: The sphere has no position and was left out.");
                return false;
            }

            double scale = options.ScaleFactor;
            double radius = sphere.Radius * scale;

            if (!(radius > 0.0) || double.IsInfinity(radius))
            {
                warnings?.Add($"{itemName}: The sphere has no radius above nought and was left out.");
                return false;
            }

            // The sphere's own placement: only where its centre is matters to a ball, however the position is turned.
            XbimMatrix3D position = sphere.Position.ToMatrix3D();
            var centre = new GeoPoint3(position.OffsetX * scale, position.OffsetY * scale, position.OffsetZ * scale);

            if (!centre.IsValid)
            {
                warnings?.Add($"{itemName}: The sphere has a position that is not a number and was left out.");
                return false;
            }

            result = new GeoSphere3(centre, radius);
            return true;
        }

        /// <summary>
        /// Reads the balls of the body of a product: every <c>IfcSphere</c> among its body representation items, at the root of an
        /// <c>IfcCsgSolid</c> among them, or in a mapped item, placed where the options say, and with
        /// <see cref="IfcConvertOptions.IncludeAggregatedParts"/> those of the parts it aggregates.
        /// </summary>
        /// <param name="product">The IFC product.</param>
        /// <param name="options">Optional conversion options; the placement is applied in <see cref="CoordinateSpace.Global"/> and left out in <see cref="CoordinateSpace.Local"/>, as for solids.</param>
        /// <returns>
        /// The balls, in the order of the product's items; none for a product with no ball, or one left out by its name. With
        /// <see cref="IfcConvertOptions.ApplyVoids"/> an element that openings cut gives none of its own balls, a ball with a void in it not being a ball.
        /// </returns>
        public static List<GeoSphere3> ToGeoSpheres(this IIfcProduct product, IfcConvertOptions options = null)
        {
            var spheres = new List<GeoSphere3>();

            if (product == null)
            {
                return spheres;
            }

            var tally = new Tally();
            Collect(product, options ?? new IfcConvertOptions(), 0, spheres, tally);

            if (tally.NotSpheres > 0)
            {
                GeometryHelperLog.Debug($"{product.GlobalId} ({IfcTypeNames.GetName(product)}): {tally.NotSpheres} body item(s) are not a sphere and were left out.");
            }

            return spheres;
        }

        private static void Collect(IIfcProduct product, IfcConvertOptions options, int depth, List<GeoSphere3> spheres, Tally tally)
        {
            if (product == null)
            {
                return;
            }

            // As for solids, a product the options leave out costs nothing.
            string name = product.Name?.ToString();

            if (ProductConvert.IsNameSkipped(name, options.SkipNames) || !ProductConvert.IsNameIncluded(name, options.OnlyNames))
            {
                return;
            }

            if (product.ObjectPlacement == null || (product.Representation == null && !options.IncludeAggregatedParts))
            {
                return;
            }

            GeoTransform3 placement;

            try
            {
                placement = product.ObjectPlacement.ToMatrix3D().ToGeoTransform3(options.ScaleFactor);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                // xBIM throws for placements it does not implement (IfcLinearPlacement, IfcGridPlacement).
                GeometryHelperLog.Warn($"{product.GlobalId} ({IfcTypeNames.GetName(product)}): the placement cannot be evaluated; no ball was read.", ex);
                return;
            }

            if (product.Representation != null)
            {
                var own = new List<GeoSphere3>();
                var outer = options.CoordinateSpace == CoordinateSpace.Local ? new List<GeoTransform3>() : new List<GeoTransform3> { placement };

                foreach (IIfcRepresentation representation in ProductConvert.SelectBodyRepresentations(product.Representation.Representations))
                {
                    foreach (IIfcRepresentationItem item in representation.Items)
                    {
                        if (item != null)
                        {
                            CollectItem(product, item, options, outer, own, tally);
                        }
                    }
                }

                if (own.Count > 0 && options.ApplyVoids && product is IIfcElement element && element.HasOpenings != null && element.HasOpenings.Any())
                {
                    // The openings are cut from the body, and a ball with a void in it is not a ball.
                    GeometryHelperLog.Warn($"{product.GlobalId} ({IfcTypeNames.GetName(product)}): openings cut the element and ApplyVoids is set, so its {own.Count} ball(s) are not balls any more and are left out; read it with ApplyVoids off, or take its solid.");
                }
                else
                {
                    spheres.AddRange(own);
                }
            }

            if (options.IncludeAggregatedParts && depth < MaxAggregationDepth)
            {
                CollectAggregatedParts(product, options, placement, depth, spheres, tally);
            }
        }

        /// <summary>
        /// Reads the balls of an item of a product's body, through the mapped items it holds: the transformations of the mappings, then the
        /// <paramref name="outer"/> ones, innermost first, as <see cref="ProductConvert"/> composes them for solids.
        /// </summary>
        private static void CollectItem(IIfcProduct product, IIfcRepresentationItem item, IfcConvertOptions options, List<GeoTransform3> outer, List<GeoSphere3> into, Tally tally)
        {
            if (item is IIfcMappedItem mappedItem)
            {
                if (mappedItem.MappingSource?.MappedRepresentation?.Items == null || mappedItem.MappingTarget == null)
                {
                    return;
                }

                GeoTransform3 mapping;

                try
                {
                    mapping = ProductConvert.MappingTransformOf(mappedItem, options.ScaleFactor);
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    GeometryHelperLog.Warn($"#{item.EntityLabel} {IfcTypeNames.GetName(item)} of {product.GlobalId}: the mapping cannot be evaluated; its balls were left out.", ex);
                    return;
                }

                var next = new List<GeoTransform3>(outer.Count + 1) { mapping };
                next.AddRange(outer);

                foreach (IIfcRepresentationItem inner in mappedItem.MappingSource.MappedRepresentation.Items)
                {
                    if (inner != null)
                    {
                        CollectItem(product, inner, options, next, into, tally);
                    }
                }

                return;
            }

            IIfcSphere sphere = SphereOf(item);

            if (sphere == null)
            {
                tally.NotSpheres++;
                return;
            }

            var said = new List<string>();

            if (!TryRead(sphere, out GeoSphere3 ball, options, said))
            {
                foreach (string message in said)
                {
                    GeometryHelperLog.Warn($"{product.GlobalId}: {message}");
                }

                return;
            }

            try
            {
                // The innermost transformation first, the product's placement last.
                GeoTransform3 placed = null;

                foreach (GeoTransform3 transform in outer)
                {
                    placed = placed == null ? transform : transform * placed;
                }

                into.Add(placed == null ? ball : ball.TransformBy(placed));
            }
            catch (InvalidOperationException ex)
            {
                // A ball stretched unevenly by a mapping or a placement is an ellipsoid.
                GeometryHelperLog.Warn($"#{sphere.EntityLabel} {IfcTypeNames.GetName(sphere)} of {product.GlobalId}: it is stretched unevenly by its mapping or placement, which makes an ellipsoid of it; it is left out.", ex);
            }
        }

        /// <summary>
        /// Reads the balls of the parts a product aggregates, as <see cref="ProductConvert"/> reads their solids: in global coordinates and, for
        /// local output, brought into the aggregating product's frame.
        /// </summary>
        private static void CollectAggregatedParts(IIfcProduct product, IfcConvertOptions options, GeoTransform3 placement, int depth, List<GeoSphere3> spheres, Tally tally)
        {
            IfcConvertOptions partOptions = options.Clone();
            partOptions.CoordinateSpace = CoordinateSpace.Global;

            GeoTransform3 globalToHost = null;

            if (options.CoordinateSpace == CoordinateSpace.Local && !placement.TryGetInverse(out globalToHost, options.Tolerance))
            {
                GeometryHelperLog.Warn($"{product.GlobalId} ({IfcTypeNames.GetName(product)}): the placement cannot be inverted, so the balls of its aggregated parts could not be expressed in local coordinates.");
                return;
            }

            foreach (IIfcRelAggregates rel in product.IsDecomposedBy.OfType<IIfcRelAggregates>())
            {
                foreach (IIfcProduct part in rel.RelatedObjects.OfType<IIfcProduct>())
                {
                    var partSpheres = new List<GeoSphere3>();
                    Collect(part, partOptions, depth + 1, partSpheres, tally);

                    foreach (GeoSphere3 sphere in partSpheres)
                    {
                        try
                        {
                            spheres.Add(globalToHost != null ? sphere.TransformBy(globalToHost) : sphere);
                        }
                        catch (InvalidOperationException ex)
                        {
                            GeometryHelperLog.Warn($"Part {part.GlobalId} of {product.GlobalId}: the placement of its host stretches a ball unevenly; it is left out.", ex);
                        }
                    }
                }
            }
        }
    }
}
