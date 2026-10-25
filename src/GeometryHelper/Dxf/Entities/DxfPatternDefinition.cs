using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A line of a HATCH's pattern, as AutoCAD's <c>PatternDefinition</c>: the family of parallel lines it draws, at an angle,
    /// through a base point, each the next by an offset, dashed as its dashes say; as the HATCH holds it, scaled and turned with
    /// the pattern (groups 53, 43, 44, 45, 46, 79 and 49).
    /// </summary>
    /// <remarks>
    /// The pattern ANSI31 at the scale 1 is one line at 45 degrees through the origin, each next 3.175 across:
    /// <see cref="Angle"/> pi / 4, <see cref="OffsetX"/> -2.245 and <see cref="OffsetY"/> 2.245, and no dashes, a solid line.
    /// </remarks>
    public readonly struct DxfPatternDefinition
    {
        private static readonly IReadOnlyList<double> NoDashes = Array.AsReadOnly(new double[0]);

        private readonly IReadOnlyList<double> _dashes;

        /// <summary>
        /// Initializes a line of a pattern, as AutoCAD's <c>new PatternDefinition(angle, basePoint, offset, dashes)</c>.
        /// </summary>
        /// <param name="angle">The angle of the lines, in radians from the x axis of the HATCH's plane.</param>
        /// <param name="baseX">The x of the point the first line runs through, in the HATCH's plane.</param>
        /// <param name="baseY">The y of that point.</param>
        /// <param name="offsetX">The x of the step from one line to the next.</param>
        /// <param name="offsetY">The y of that step.</param>
        /// <param name="dashes">The dashes, a length above nought drawn, below it left out, nought a dot; none for a solid line.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a number is not finite.</exception>
        public DxfPatternDefinition(double angle, double baseX, double baseY, double offsetX, double offsetY, IEnumerable<double> dashes)
        {
            Guard.Finite(angle, nameof(angle), "An angle has to be a finite number.");
            Guard.Finite(baseX, nameof(baseX), "A coordinate has to be a finite number.");
            Guard.Finite(baseY, nameof(baseY), "A coordinate has to be a finite number.");
            Guard.Finite(offsetX, nameof(offsetX), "An offset has to be a finite number.");
            Guard.Finite(offsetY, nameof(offsetY), "An offset has to be a finite number.");
            var copied = new List<double>(dashes ?? new double[0]);

            foreach (double dash in copied)
            {
                Guard.Finite(dash, nameof(dashes), "A dash has to be a finite number.");
            }

            Angle = angle;
            BaseX = baseX;
            BaseY = baseY;
            OffsetX = offsetX;
            OffsetY = offsetY;
            _dashes = copied.AsReadOnly();
        }

        /// <summary>Gets the angle of the lines, as AutoCAD's <c>Angle</c>: in radians from the x axis of the HATCH's plane (group 53 in degrees).</summary>
        public double Angle { get; }

        /// <summary>Gets the x of the point the first line runs through (group 43), as AutoCAD's <c>BaseX</c>.</summary>
        public double BaseX { get; }

        /// <summary>Gets the y of the point the first line runs through (group 44), as AutoCAD's <c>BaseY</c>.</summary>
        public double BaseY { get; }

        /// <summary>Gets the x of the step from one line to the next (group 45), as AutoCAD's <c>OffsetX</c>.</summary>
        public double OffsetX { get; }

        /// <summary>Gets the y of the step from one line to the next (group 46), as AutoCAD's <c>OffsetY</c>.</summary>
        public double OffsetY { get; }

        /// <summary>
        /// Gets the dashes of the lines (groups 49), as AutoCAD's <c>GetDashes</c>: a length above nought drawn, below it left out,
        /// nought a dot; none for a solid line.
        /// </summary>
        /// <returns>The dashes, read-only.</returns>
        public IReadOnlyList<double> GetDashes() => _dashes ?? NoDashes;
    }
}
