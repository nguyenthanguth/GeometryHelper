using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Gets or sets whether to subtract voids and openings (IfcRelVoidsElement / IfcOpeningElement) from the host element,
        /// so that a wall comes back with its window openings and a plate with its bolt holes and cuts, as a viewer shows them.
        /// Defaults to <c>false</c>, for speed: every opening costs a boolean operation (on a Tekla steel model, 500 beams took
        /// 29 s with openings cut and 1.5 s without), and cut bodies can come back not closed. Set it to <c>true</c> when the
        /// openings matter.
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
        /// See <see cref="OnlyNames"/> for the opposite: converting only the products named.
        /// <see cref="AddSkipNames(string[])"/> adds names trimmed and without blanks.
        /// </summary>
        public HashSet<string> SkipNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the set of product names or wildcard patterns to convert exclusively. When it holds a pattern, a product
        /// whose name matches none of them comes back empty, as a skipped one does. Empty, the default, converts every
        /// product. <see cref="AddOnlyNames(string[])"/> adds names trimmed and without blanks.
        /// <para>
        /// Matching works as for <see cref="SkipNames"/>: case-insensitive, with the '*' wildcard ("B*" matches "BEAM"
        /// and "Bolt assembly"). A product with no name matches only "*". <see cref="SkipNames"/> still applies, so a
        /// name matching both is skipped.
        /// </para>
        /// <para>
        /// The name is checked before any geometry is built, so products left out cost next to nothing. Reading every
        /// product of a 115 MB Tekla steel model took about 17 minutes, 96 % of it on 5,512 bolts; keeping only the
        /// main members ("BEAM", "GIRDER", "PRD_COLUMN", "COLUMN") took 3.5 s.
        /// </para>
        /// <para>
        /// Like <see cref="SkipNames"/>, it also applies to the parts an assembly aggregates
        /// (<see cref="IncludeAggregatedParts"/>): list the names of the parts wanted as well as the assembly's. It never
        /// applies to the openings that cut a product (<see cref="ApplyVoids"/>).
        /// </para>
        /// </summary>
        public HashSet<string> OnlyNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a default conversion options instance. Set the other options through their properties, and add
        /// names with <see cref="AddSkipNames(string[])"/> and <see cref="AddOnlyNames(string[])"/>.
        /// </summary>
        public IfcConvertOptions()
        {
        }

        /// <summary>
        /// Adds product names or wildcard patterns to <see cref="SkipNames"/>. Blank entries are ignored and the others
        /// trimmed, so names read from a settings file or a text box can be passed as they are.
        /// </summary>
        /// <param name="names">The names or patterns; null adds none.</param>
        /// <returns>These options, so that calls can be chained.</returns>
        public IfcConvertOptions AddSkipNames(params string[] names)
        {
            return AddSkipNames((IEnumerable<string>)names);
        }

        /// <summary>
        /// Adds product names or wildcard patterns to <see cref="SkipNames"/>. Blank entries are ignored and the others
        /// trimmed, so names read from a settings file or a text box can be passed as they are.
        /// </summary>
        /// <param name="names">The names or patterns; null adds none.</param>
        /// <returns>These options, so that calls can be chained.</returns>
        public IfcConvertOptions AddSkipNames(IEnumerable<string> names)
        {
            AddNames(SkipNames, names);
            return this;
        }

        /// <summary>
        /// Adds product names or wildcard patterns to <see cref="OnlyNames"/>. Blank entries are ignored and the others
        /// trimmed, so names read from a settings file or a text box can be passed as they are.
        /// </summary>
        /// <param name="names">The names or patterns; null adds none.</param>
        /// <returns>These options, so that calls can be chained.</returns>
        public IfcConvertOptions AddOnlyNames(params string[] names)
        {
            return AddOnlyNames((IEnumerable<string>)names);
        }

        /// <summary>
        /// Adds product names or wildcard patterns to <see cref="OnlyNames"/>. Blank entries are ignored and the others
        /// trimmed, so names read from a settings file or a text box can be passed as they are.
        /// </summary>
        /// <param name="names">The names or patterns; null adds none.</param>
        /// <returns>These options, so that calls can be chained.</returns>
        public IfcConvertOptions AddOnlyNames(IEnumerable<string> names)
        {
            AddNames(OnlyNames, names);
            return this;
        }

        private static void AddNames(HashSet<string> target, IEnumerable<string> names)
        {
            if (names == null)
            {
                return;
            }

            foreach (string name in names)
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    target.Add(name.Trim());
                }
            }
        }

        /// <summary>
        /// Creates a copy of these options, including the <see cref="SkipNames"/> and <see cref="OnlyNames"/> patterns.
        /// </summary>
        /// <returns>A new instance that can be changed without affecting this one.</returns>
        public IfcConvertOptions Clone()
        {
            IfcConvertOptions copy = new IfcConvertOptions
            {
                Tolerance = Tolerance,
                ScaleFactor = ScaleFactor,
                CoordinateSpace = CoordinateSpace,
                TargetUnit = TargetUnit,
                TessellateNonPlanarFaces = TessellateNonPlanarFaces,
                DeflectionTolerance = DeflectionTolerance,
                ApplyVoids = ApplyVoids,
                IncludeNonPhysicalProducts = IncludeNonPhysicalProducts,
                IncludeAggregatedParts = IncludeAggregatedParts
            };

            // Copied as they are rather than through AddSkipNames / AddOnlyNames, which trim: the copy must match what
            // this matches.
            copy.SkipNames.UnionWith(SkipNames);
            copy.OnlyNames.UnionWith(OnlyNames);
            return copy;
        }

        /// <summary>
        /// Returns a compact string key encoding all fields that affect geometry conversion output.
        /// Used as part of the geometry cache key in <c>XbimIfcSession</c>.
        /// </summary>
        internal string GetCacheKey()
        {
            // Encode all fields that can change the shape/position of converted geometry. The name lists are kept apart
            // by a character no name holds, so that skipping "A" and keeping only "A" never share cached geometry.
            return $"{ScaleFactor:R}|{(int)CoordinateSpace}|{(int)TargetUnit}|{ApplyVoids}|{TessellateNonPlanarFaces}|{DeflectionTolerance:R}|{Tolerance.EqualPoint:R}|{Tolerance.EqualVector:R}|{Tolerance.EqualAngleRad:R}|{Tolerance.EqualPlanar:R}|{IncludeAggregatedParts}|{NamesKey(SkipNames)}\u001E{NamesKey(OnlyNames)}";
        }

        // Sorted and in one case, since matching ignores both order and case; joined by a character names do not hold.
        private static string NamesKey(HashSet<string> names)
        {
            return string.Join("\u001F", names.Select(n => n.ToUpperInvariant()).OrderBy(n => n, StringComparer.Ordinal));
        }
    }
}
