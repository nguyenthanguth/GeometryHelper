using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;

namespace GeometryHelper.IfcConvert.Core
{

    /// <summary>
    /// Configuration options for converting IFC geometry to GeometryHelper SolidGeometry.
    /// </summary>
    public sealed class IfcConvertOptions
    {
        /// <summary>
        /// Gets or sets the geometric tolerance used for planar checks, equality, and loop closures.
        /// Defaults to <see cref="Tolerance.Global"/>.
        /// </summary>
        public Tolerance Tolerance { get; set; } = Tolerance.Global;

        /// <summary>
        /// Gets or sets a uniform scale factor applied to coordinates.
        /// Default is 1.0 (no scaling). Set to 1000.0 to convert meters to millimeters.
        /// </summary>
        public double ScaleFactor { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the target coordinate space (Global or Local). Defaults to <see cref="CoordinateSpace.Global"/>.
        /// </summary>
        public CoordinateSpace CoordinateSpace { get; set; } = CoordinateSpace.Global;

        /// <summary>
        /// Gets or sets the target length unit for automatic scaling. Defaults to <see cref="LengthUnit.Original"/>.
        /// </summary>
        public LengthUnit TargetUnit { get; set; } = LengthUnit.Original;

        /// <summary>
        /// Gets or sets whether non-planar (curved) faces should be tessellated into planar triangles.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool TessellateNonPlanarFaces { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum distance between a curved surface and its triangles when tessellating,
        /// in output units (the units of the returned geometry, after <see cref="TargetUnit"/> and <see cref="ScaleFactor"/>).
        /// Smaller values yield smoother curved surfaces at the cost of more triangles.
        /// Zero or negative (the default) uses the deflection xBIM derives for the model from its length unit
        /// (a few millimetres), which gives volumes of round sections within about 0.5 %.
        /// </summary>
        public double DeflectionTolerance { get; set; } = 0.0;

        /// <summary>
        /// Gets or sets whether to subtract voids and openings (IfcRelVoidsElement / IfcOpeningElement) from the host element.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool ApplyVoids { get; set; } = false;

        /// <summary>
        /// Gets or sets whether model-wide queries (<c>GetAllSolids</c>, <c>EnumerateSolids</c>, <c>EnumerateGeometries</c>)
        /// also return products that are not physical elements: openings and voiding features, spatial elements
        /// (site, building, storey, space, zone), annotations, grids, virtual elements and structural analysis items.
        /// Defaults to <c>false</c>, so an opening's or a room's volume is not counted as material.
        /// Queries by GlobalId or by explicit type name are not affected.
        /// </summary>
        public bool IncludeNonPhysicalProducts { get; set; } = false;

        /// <summary>
        /// Gets or sets whether a product's geometry also includes the parts it aggregates (IfcRelAggregates),
        /// recursively. Tekla exports an assembly as an IfcElementAssembly with no body of its own whose plates and
        /// profiles are aggregated parts; with this option its GlobalId returns the whole assembly.
        /// Defaults to <c>false</c>. Model-wide queries visit the parts themselves too, so enabling it there
        /// counts each part twice.
        /// </summary>
        public bool IncludeAggregatedParts { get; set; } = false;

        /// <summary>
        /// Gets the set of product names or wildcard patterns to skip during conversion.
        /// Case-insensitive. Supports '*' wildcard (e.g. "W*" matches "W1", "W2").
        /// </summary>
        public HashSet<string> SkipNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a default conversion options instance.
        /// </summary>
        public IfcConvertOptions()
        {
        }

        /// <summary>
        /// Creates conversion options with specified skip patterns and tolerance.
        /// </summary>
        /// <param name="skipNames">Sequence of product names or patterns to skip.</param>
        /// <param name="tolerance">Geometric tolerance (optional).</param>
        public IfcConvertOptions(IEnumerable<string> skipNames, Tolerance? tolerance = null)
        {
            if (skipNames != null)
            {
                foreach (string name in skipNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        SkipNames.Add(name.Trim());
                    }
                }
            }

            if (tolerance.HasValue)
            {
                Tolerance = tolerance.Value;
            }
        }

        /// <summary>
        /// Creates a copy of these options, including the <see cref="SkipNames"/> patterns.
        /// </summary>
        internal IfcConvertOptions Clone()
        {
            IfcConvertOptions copy = new IfcConvertOptions(SkipNames, Tolerance)
            {
                ScaleFactor = ScaleFactor,
                CoordinateSpace = CoordinateSpace,
                TargetUnit = TargetUnit,
                TessellateNonPlanarFaces = TessellateNonPlanarFaces,
                DeflectionTolerance = DeflectionTolerance,
                ApplyVoids = ApplyVoids,
                IncludeNonPhysicalProducts = IncludeNonPhysicalProducts,
                IncludeAggregatedParts = IncludeAggregatedParts
            };

            return copy;
        }

        /// <summary>
        /// Returns a compact string key encoding all fields that affect geometry conversion output.
        /// Used as part of the geometry cache key in <c>XbimIfcSession</c>.
        /// </summary>
        internal string GetCacheKey()
        {
            // Encode all fields that can change the shape/position of converted geometry
            string skipKey = SkipNames.Count > 0
                ? string.Join(",", System.Linq.Enumerable.OrderBy(SkipNames, s => s))
                : string.Empty;

            return $"{ScaleFactor:R}|{(int)CoordinateSpace}|{(int)TargetUnit}|{ApplyVoids}|{TessellateNonPlanarFaces}|{DeflectionTolerance:R}|{Tolerance.EqualPoint:R}|{Tolerance.EqualVector:R}|{Tolerance.EqualAngleRad:R}|{Tolerance.EqualPlanar:R}|{IncludeAggregatedParts}|{skipKey}";
        }
    }
}
