using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// One cell of a body cut into a grid: where it stands in the grid, the whole cell, and the part of it the body holds.
    /// <para>
    /// <see cref="I"/>, <see cref="J"/> and <see cref="K"/> count the cell along the grid's X, Y and Z axes from nought.
    /// A cell the body holds in more than one piece, as a cell across the notch of a U holds two, comes as one
    /// <see cref="GeoCell3"/> a piece, the same indexes on each, <see cref="Piece"/> telling them apart. The cell is immutable.
    /// </para>
    /// </summary>
    public sealed class GeoCell3
    {
        private readonly GeoSolid3 _solid;
        private readonly GeoObb3 _material;
        private GeoSolid3 _built;

        /// <summary>
        /// Initializes a cell from its place in the grid, the whole cell and its material.
        /// </summary>
        /// <param name="i">The cell's index along the grid's X axis.</param>
        /// <param name="j">The cell's index along the grid's Y axis.</param>
        /// <param name="k">The cell's index along the grid's Z axis.</param>
        /// <param name="piece">Which piece of the cell this is.</param>
        /// <param name="box">The whole cell.</param>
        /// <param name="solid">The material, or null when it is a box, given as <paramref name="material"/>.</param>
        /// <param name="material">The material as a box, when it is one; null otherwise.</param>
        /// <param name="whole">Whether the material is the whole cell.</param>
        /// <param name="volume">The volume of the material.</param>
        /// <param name="low">Where along each axis the cut bounding the material from below stands, NaN where the body does.</param>
        /// <param name="high">Where along each axis the cut bounding the material from above stands, NaN where the body does.</param>
        internal GeoCell3(int i, int j, int k, int piece, GeoObb3 box, GeoSolid3 solid, GeoObb3 material, bool whole, double volume, double[] low, double[] high)
        {
            I = i;
            J = j;
            K = k;
            Piece = piece;
            Box = box;
            _solid = solid;
            _material = material;
            IsWhole = whole;
            Volume = volume;
            Low = low;
            High = high;
        }

        /// <summary>
        /// Gets the cell's index along the grid's X axis, from nought.
        /// </summary>
        public int I { get; }

        /// <summary>
        /// Gets the cell's index along the grid's Y axis, from nought.
        /// </summary>
        public int J { get; }

        /// <summary>
        /// Gets the cell's index along the grid's Z axis, from nought.
        /// </summary>
        public int K { get; }

        /// <summary>
        /// Gets which piece of the cell this is, from nought: a cell the body holds in one piece has only piece nought.
        /// </summary>
        public int Piece { get; }

        /// <summary>
        /// Gets the whole cell, where it stands in the grid, whether or not the body fills it.
        /// </summary>
        public GeoObb3 Box { get; }

        /// <summary>
        /// Gets whether the body fills the whole cell, but for the point tolerance.
        /// </summary>
        /// <remarks>
        /// A cell the boundary cuts by no more than the point tolerance counts as whole, as a block a hair short is a whole
        /// block, and keeps the shape it was cut to, so that it stays within the body.
        /// </remarks>
        public bool IsWhole { get; }

        /// <summary>
        /// Gets the volume of the part of the cell the body holds.
        /// </summary>
        public double Volume { get; }

        /// <summary>
        /// Gets the part of the cell the body holds, as a closed body of its own.
        /// </summary>
        /// <remarks>
        /// A cell of a box is a box, and is built as a body only when asked for: a grid of a million of them would otherwise
        /// hold six million faces nobody looks at.
        /// </remarks>
        public GeoSolid3 Solid
        {
            get
            {
                if (_solid != null)
                {
                    return _solid;
                }

                GeoSolid3 built = _built;

                if (built == null)
                {
                    built = Core.CellGrid3.BoxSolid(_material);
                    _built = built;
                }

                return built;
            }
        }

        /// <summary>
        /// The material as a box, when the cell is one of a box; null otherwise.
        /// </summary>
        internal GeoObb3 Material => _material;

        /// <summary>
        /// Where along each of the grid's axes, measured from its origin, the cut bounding the material from below stands;
        /// NaN where the body bounds it.
        /// </summary>
        internal double[] Low { get; }

        /// <summary>
        /// Where along each of the grid's axes, measured from its origin, the cut bounding the material from above stands;
        /// NaN where the body bounds it.
        /// </summary>
        internal double[] High { get; }

        /// <summary>
        /// Describes the cell.
        /// </summary>
        public override string ToString()
            => string.Format(
                CultureInfo.InvariantCulture,
                "Cell3({0}, {1}, {2}{3}, {4}, Volume: {5:0.###})",
                I,
                J,
                K,
                Piece > 0 ? ", piece " + Piece : string.Empty,
                IsWhole ? "whole" : "cut",
                Volume);
    }
}
