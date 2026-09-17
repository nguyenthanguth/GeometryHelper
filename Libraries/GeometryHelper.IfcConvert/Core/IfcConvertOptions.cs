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
        /// Gets or sets whether non-planar (curved) faces should be tessellated into planar triangles.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool TessellateNonPlanarFaces { get; set; } = true;

        /// <summary>
        /// Gets or sets deflection/tolerance used when tessellating curved geometry.
        /// Smaller values yield smoother curved surfaces at the cost of more triangles.
        /// </summary>
        public double DeflectionTolerance { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets whether to subtract voids and openings (IfcRelVoidsElement / IfcOpeningElement) from the host element.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool ApplyVoids { get; set; } = false;

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
    }
}
