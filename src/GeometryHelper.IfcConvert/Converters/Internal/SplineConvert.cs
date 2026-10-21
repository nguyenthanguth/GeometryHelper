using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Reads the B-spline curves of an IFC model, the <c>IfcBSplineCurveWithKnots</c> and <c>IfcRationalBSplineCurveWithKnots</c>
    /// of a product's shape, as <see cref="GeoSpline3"/>: the degree, the control points, the knots each repeated as often as the file
    /// says, and the weights where the curve is rational.
    /// <para>
    /// Such a curve comes as an item of a representation of the product, the axis of a member above all; among the elements of an
    /// <c>IfcGeometricSet</c> or <c>IfcGeometricCurveSet</c>; as the parent curve of a segment of an <c>IfcCompositeCurve</c>; as the
    /// directrix of an <c>IfcSweptDiskSolid</c>, the path a bar or a pipe is swept along; and inside a mapped item, whose mapping
    /// transformation is applied to it. Each is read, from every representation of the product, once for each place it is met.
    /// An <c>IfcIndexedPolyCurve</c> has no B-spline in it, and a B-spline that is the basis of an <c>IfcTrimmedCurve</c> is only a part of
    /// itself there and is not read; nor is one in the outline of a profile, which lies in a frame of the solid it is swept into.
    /// </para>
    /// </summary>
    internal static class SplineConvert
    {
        // Aggregation trees are shallow in practice; the limit only guards against malformed cyclic files, as for solids.
        private const int MaxAggregationDepth = 16;

        // Composite curves nest in practice no deeper than a few levels; the limit only guards against a malformed cyclic file.
        private const int MaxCurveDepth = 32;

        /// <summary>
        /// What reading the splines of a product met that is said once for the call: the items that hold no B-spline curve.
        /// </summary>
        private sealed class Tally
        {
            internal int Without;
        }

        /// <summary>
        /// Tries to read a B-spline curve with knots as a <see cref="GeoSpline3"/>, in the frame of the item it belongs to.
        /// </summary>
        /// <param name="curve">The curve.</param>
        /// <param name="result">The spline when the method returns true: its control points scaled by the options, its knots and weights as the file gives them.</param>
        /// <param name="options">Optional conversion options; <see cref="IfcConvertOptions.ScaleFactor"/> scales the control points.</param>
        /// <param name="warnings">Receives a message, prefixed with the curve's entity label, for a curve that cannot be read; null to ignore it.</param>
        /// <returns><c>true</c> when the curve could be read; <c>false</c>, with a message, when its numbers do not make a clamped spline.</returns>
        public static bool TryToGeoSpline3(this IIfcBSplineCurveWithKnots curve, out GeoSpline3 result, IfcConvertOptions options = null, ICollection<string> warnings = null)
        {
            result = null;

            if (curve == null)
            {
                return false;
            }

            options = options ?? new IfcConvertOptions();
            string itemName = $"#{curve.EntityLabel} {IfcTypeNames.GetName(curve)}";
            double scale = options.ScaleFactor;

            if (curve.ControlPointsList == null || curve.Knots == null || curve.KnotMultiplicities == null)
            {
                warnings?.Add($"{itemName}: The B-spline curve has no control points or no knots and was left out.");
                return false;
            }

            var points = new List<GeoPoint3>();

            foreach (IIfcCartesianPoint point in curve.ControlPointsList)
            {
                if (point?.Coordinates == null || point.Coordinates.Count < 2)
                {
                    warnings?.Add($"{itemName}: The B-spline curve has a control point that is not a point and was left out.");
                    return false;
                }

                double z = point.Coordinates.Count > 2 ? (double)point.Coordinates[2] : 0.0;
                points.Add(new GeoPoint3(point.Coordinates[0] * scale, point.Coordinates[1] * scale, z * scale));
            }

            // The file gives each distinct knot once with the number of times it is repeated.
            if (curve.Knots.Count != curve.KnotMultiplicities.Count)
            {
                warnings?.Add($"{itemName}: The B-spline curve has {curve.Knots.Count} knots and {curve.KnotMultiplicities.Count} multiplicities and was left out.");
                return false;
            }

            var knots = new List<double>();

            for (int i = 0; i < curve.Knots.Count; i++)
            {
                long times = curve.KnotMultiplicities[i];

                if (times < 1 || times > points.Count + 64)
                {
                    warnings?.Add($"{itemName}: The B-spline curve repeats a knot {times} times and was left out.");
                    return false;
                }

                for (long k = 0; k < times; k++)
                {
                    knots.Add(curve.Knots[i]);
                }
            }

            List<double> weights = null;

            if (curve is IIfcRationalBSplineCurveWithKnots rational && rational.WeightsData != null)
            {
                weights = rational.WeightsData.Select(w => (double)w).ToList();
            }

            long degree = curve.Degree;

            try
            {
                result = new GeoSpline3((int)Math.Max(int.MinValue, Math.Min(int.MaxValue, degree)), points, knots, weights);
                return true;
            }
            catch (ArgumentException ex)
            {
                // A periodic curve, its knots not clamped, among them: GeometryHelper's splines start and end at their end control points.
                warnings?.Add($"{itemName}: The B-spline curve is not one GeometryHelper takes, and was left out: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads the B-spline curves of a product: every <c>IfcBSplineCurveWithKnots</c>, rational or not, among the items of its
        /// representations, in a geometric set, a composite curve or as the directrix of a swept disk, and in a mapped item, placed where
        /// the options say, and with <see cref="IfcConvertOptions.IncludeAggregatedParts"/> those of the parts it aggregates.
        /// </summary>
        /// <param name="product">The IFC product.</param>
        /// <param name="options">Optional conversion options; the placement is applied in <see cref="CoordinateSpace.Global"/> and left out in <see cref="CoordinateSpace.Local"/>, as for solids.</param>
        /// <returns>The splines, in the order of the product's representations and their items; none for a product with none, or one left out by its name.</returns>
        public static List<GeoSpline3> ToGeoSplines(this IIfcProduct product, IfcConvertOptions options = null)
        {
            var splines = new List<GeoSpline3>();

            if (product == null)
            {
                return splines;
            }

            var tally = new Tally();
            Collect(product, options ?? new IfcConvertOptions(), 0, splines, tally);

            if (tally.Without > 0)
            {
                GeometryHelperLog.Debug($"{product.GlobalId} ({IfcTypeNames.GetName(product)}): {tally.Without} representation item(s) hold no B-spline curve and were left out.");
            }

            return splines;
        }

        private static void Collect(IIfcProduct product, IfcConvertOptions options, int depth, List<GeoSpline3> splines, Tally tally)
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
                GeometryHelperLog.Warn($"{product.GlobalId} ({IfcTypeNames.GetName(product)}): the placement cannot be evaluated; no spline was read.", ex);
                return;
            }

            if (product.Representation?.Representations != null)
            {
                var outer = options.CoordinateSpace == CoordinateSpace.Local ? new List<GeoTransform3>() : new List<GeoTransform3> { placement };
                var seen = new HashSet<string>(StringComparer.Ordinal);

                foreach (IIfcRepresentation representation in product.Representation.Representations)
                {
                    if (representation?.Items == null)
                    {
                        continue;
                    }

                    foreach (IIfcRepresentationItem item in representation.Items)
                    {
                        if (item != null)
                        {
                            CollectItem(product, item, options, outer, string.Empty, seen, splines, tally);
                        }
                    }
                }
            }

            if (options.IncludeAggregatedParts && depth < MaxAggregationDepth)
            {
                CollectAggregatedParts(product, options, placement, depth, splines, tally);
            }
        }

        /// <summary>
        /// Reads the splines of an item of a product's representation, through the mapped items it holds: the transformations of the
        /// mappings, then the <paramref name="outer"/> ones, innermost first, as <see cref="ProductConvert"/> composes them for solids. The
        /// <paramref name="path"/> is the labels of the mapped items the item is reached through, which tell one placing of a curve from another,
        /// and <paramref name="seen"/> the curves read under each path, so that a curve met twice in one place, as an axis and as a directrix,
        /// is read once.
        /// </summary>
        private static void CollectItem(IIfcProduct product, IIfcRepresentationItem item, IfcConvertOptions options, List<GeoTransform3> outer, string path, HashSet<string> seen, List<GeoSpline3> into, Tally tally)
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
                    GeometryHelperLog.Warn($"#{item.EntityLabel} {IfcTypeNames.GetName(item)} of {product.GlobalId}: the mapping cannot be evaluated; its splines were left out.", ex);
                    return;
                }

                var next = new List<GeoTransform3>(outer.Count + 1) { mapping };
                next.AddRange(outer);

                foreach (IIfcRepresentationItem inner in mappedItem.MappingSource.MappedRepresentation.Items)
                {
                    if (inner != null)
                    {
                        CollectItem(product, inner, options, next, path + "/" + item.EntityLabel, seen, into, tally);
                    }
                }

                return;
            }

            var curves = new List<Found>();
            CurvesOf(item, curves, 0, null);

            if (curves.Count == 0)
            {
                tally.Without++;
                return;
            }

            foreach (Found found in curves)
            {
                IIfcBSplineCurveWithKnots curve = found.Curve;
                string place = path + "#" + curve.EntityLabel;
                var said = new List<string>();

                if (!curve.TryToGeoSpline3(out GeoSpline3 spline, options, said))
                {
                    // A curve that cannot be read is said once, however often it is met.
                    if (seen.Add(place))
                    {
                        foreach (string message in said)
                        {
                            GeometryHelperLog.Warn($"{product.GlobalId}: {message}");
                        }
                    }

                    continue;
                }

                if (found.Disk != null)
                {
                    spline = Trimmed(product, found, spline);
                }

                // The same part of a curve is read once: the whole curve as an axis and as the directrix of a disk that runs along all of
                // it, however its parameters are written, has one domain of knots.
                if (!seen.Add(place + "@" + Text(spline.DomainStart) + ":" + Text(spline.DomainEnd)))
                {
                    continue;
                }

                try
                {
                    // The innermost transformation first, the product's placement last.
                    GeoTransform3 placed = null;

                    foreach (GeoTransform3 transform in outer)
                    {
                        placed = placed == null ? transform : transform * placed;
                    }

                    into.Add(placed == null ? spline : spline.TransformBy(placed));
                }
                catch (InvalidOperationException ex)
                {
                    // A mapping that squeezes space onto a plane or a line can squeeze a spline onto one point.
                    GeometryHelperLog.Warn($"#{curve.EntityLabel} {IfcTypeNames.GetName(curve)} of {product.GlobalId}: its mapping or placement squeezes it onto one point; it is left out.", ex);
                }
            }
        }

        /// <summary>
        /// A B-spline curve an item holds, and the swept disk it is the directrix of, if it is one, with the parameters the sweep runs between.
        /// </summary>
        private sealed class Found
        {
            internal IIfcBSplineCurveWithKnots Curve;
            internal IIfcSweptDiskSolid Disk;
            internal double? From;
            internal double? To;
            internal bool Directly;
        }

        private static string Text(double? value) => value.HasValue ? value.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

        /// <summary>
        /// Gathers the B-spline curves an item is or holds, in the order the file gives them: the item itself, the elements of a geometric
        /// set, the parent curves of the segments of a composite curve, the directrix of a swept disk.
        /// </summary>
        private static void CurvesOf(IIfcRepresentationItem item, List<Found> curves, int depth, IIfcSweptDiskSolid disk)
        {
            if (item == null || depth > MaxCurveDepth)
            {
                return;
            }

            switch (item)
            {
                case IIfcBSplineCurveWithKnots spline:
                    bool directly = disk?.Directrix != null && disk.Directrix.EntityLabel == spline.EntityLabel;
                    curves.Add(new Found
                    {
                        Curve = spline,
                        Disk = disk,
                        Directly = directly,
                        From = directly && disk.StartParam.HasValue ? (double?)disk.StartParam.Value : null,
                        To = directly && disk.EndParam.HasValue ? (double?)disk.EndParam.Value : null,
                    });
                    break;

                case IIfcGeometricSet set when set.Elements != null:
                    foreach (IIfcGeometricSetSelect element in set.Elements)
                    {
                        CurvesOf(element as IIfcRepresentationItem, curves, depth + 1, disk);
                    }

                    break;

                case IIfcCompositeCurve composite when composite.Segments != null:
                    foreach (IIfcCompositeCurveSegment segment in composite.Segments.OfType<IIfcCompositeCurveSegment>())
                    {
                        CurvesOf(segment.ParentCurve, curves, depth + 1, disk);
                    }

                    break;

                case IIfcSweptDiskSolid sweptDisk:
                    CurvesOf(sweptDisk.Directrix, curves, depth + 1, sweptDisk);
                    break;
            }
        }

        /// <summary>
        /// The part of a directrix a swept disk runs along. The parameter of a B-spline curve is the value of its knots (ISO 10303-42, as
        /// IFC takes it), so the sweep from <c>StartParam</c> to <c>EndParam</c> is the spline cut at those knot values, exactly; where either
        /// is not given the sweep runs from that end of the curve. A pair outside the domain of the knots, or not in order, leaves the whole
        /// curve with a warning, as does a trim of a B-spline that is a segment of a composite directrix, whose parameters are the composite's.
        /// </summary>
        private static GeoSpline3 Trimmed(IIfcProduct product, Found found, GeoSpline3 spline)
        {
            string name = $"#{found.Curve.EntityLabel} {IfcTypeNames.GetName(found.Curve)} of {product.GlobalId}";

            if (!found.Directly)
            {
                if (found.Disk.StartParam.HasValue || found.Disk.EndParam.HasValue)
                {
                    GeometryHelperLog.Warn($"{name}: the swept disk #{found.Disk.EntityLabel} runs between parameters of its composite directrix, which are not read; the whole curve is given.");
                }

                return spline;
            }

            double from = found.From ?? spline.DomainStart;
            double to = found.To ?? spline.DomainEnd;

            if (from == spline.DomainStart && to == spline.DomainEnd)
            {
                return spline;
            }

            if (!(from >= spline.DomainStart && to <= spline.DomainEnd && from < to))
            {
                GeometryHelperLog.Warn($"{name}: the swept disk #{found.Disk.EntityLabel} runs from {Text(from)} to {Text(to)}, which is not inside the curve's domain {Text(spline.DomainStart)} to {Text(spline.DomainEnd)} in order; the whole curve is given.");
                return spline;
            }

            try
            {
                GeoSpline3 part = from > spline.DomainStart ? spline.SplitAtKnot(from)[1] : spline;
                return to < part.DomainEnd ? part.SplitAtKnot(to)[0] : part;
            }
            catch (ArgumentException ex)
            {
                GeometryHelperLog.Warn($"{name}: the part the swept disk #{found.Disk.EntityLabel} runs along, from {Text(from)} to {Text(to)}, cannot be cut out; the whole curve is given.", ex);
                return spline;
            }
        }

        /// <summary>
        /// Reads the splines of the parts a product aggregates, as <see cref="ProductConvert"/> reads their solids: in global coordinates and,
        /// for local output, brought into the aggregating product's frame.
        /// </summary>
        private static void CollectAggregatedParts(IIfcProduct product, IfcConvertOptions options, GeoTransform3 placement, int depth, List<GeoSpline3> splines, Tally tally)
        {
            IfcConvertOptions partOptions = options.Clone();
            partOptions.CoordinateSpace = CoordinateSpace.Global;

            GeoTransform3 globalToHost = null;

            if (options.CoordinateSpace == CoordinateSpace.Local && !placement.TryGetInverse(out globalToHost, options.Tolerance))
            {
                GeometryHelperLog.Warn($"{product.GlobalId} ({IfcTypeNames.GetName(product)}): the placement cannot be inverted, so the splines of its aggregated parts could not be expressed in local coordinates.");
                return;
            }

            foreach (IIfcRelAggregates rel in product.IsDecomposedBy.OfType<IIfcRelAggregates>())
            {
                foreach (IIfcProduct part in rel.RelatedObjects.OfType<IIfcProduct>())
                {
                    var partSplines = new List<GeoSpline3>();
                    Collect(part, partOptions, depth + 1, partSplines, tally);

                    foreach (GeoSpline3 spline in partSplines)
                    {
                        splines.Add(globalToHost != null ? spline.TransformBy(globalToHost) : spline);
                    }
                }
            }
        }
    }
}
