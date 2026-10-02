using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Core;
using GeometryHelper.Geometry;

namespace GeometryHelper.Meshing
{
    /// <summary>
    /// A body cut into the cells of a grid: blocks, bays, lifts or bars, as the <see cref="CellOptions3"/> divided its axes.
    /// <para>
    /// The grid stands in <see cref="Frame"/>: its axes are the grid's, and its origin the corner where the first cell along
    /// each axis starts. Every cell the body holds any of comes as a <see cref="GeoCell3"/>, a closed body of what the body
    /// holds of it, the whole cell beside it as a box; a cell the body holds in several pieces comes once a piece. The cells
    /// together hold all of the body, less the joints, and do not overlap. They come layer by layer up the grid's Z axis, row
    /// by row along Y within a layer, and along X within a row, the pieces of one cell in turn. The grid is immutable.
    /// </para>
    /// </summary>
    public sealed class GeoCellGrid3
    {
        private readonly GeoCell3[] _cells;
        private readonly double[][] _starts;
        private readonly double[][] _ends;
        private readonly double _joint;
        private readonly Tolerance _tolerance;
        private Dictionary<(int, int, int), int[]> _byIndex;
        private int[][] _adjacent;

        /// <summary>
        /// Initializes a grid from its frame, where its cells start and end along each axis, and the cells, taken as they are.
        /// </summary>
        /// <param name="frame">The grid's axes, its origin where the first cells start.</param>
        /// <param name="starts">Where each cell starts along each axis, measured from the frame's origin.</param>
        /// <param name="ends">Where each cell ends along each axis, measured from the frame's origin.</param>
        /// <param name="cells">The cells, in order.</param>
        /// <param name="joint">The gap between neighbouring cells; nought for none.</param>
        /// <param name="tolerance">The tolerance the cells were cut within.</param>
        internal GeoCellGrid3(GeoCoordinateSystem3 frame, double[][] starts, double[][] ends, GeoCell3[] cells, double joint, Tolerance tolerance)
        {
            Frame = frame;
            _starts = starts;
            _ends = ends;
            _cells = cells;
            _joint = joint;
            _tolerance = tolerance;

            double volume = 0.0;

            foreach (GeoCell3 cell in cells)
            {
                volume += cell.Volume;
            }

            Volume = volume;
        }

        /// <summary>
        /// Gets the grid's frame: its axes are the grid's, and its origin the corner where the first cell along each axis
        /// starts.
        /// </summary>
        public GeoCoordinateSystem3 Frame { get; }

        /// <summary>
        /// Gets how many cells the grid lays along its X axis, whether or not the body holds any of each.
        /// </summary>
        public int CountX => _starts[0].Length;

        /// <summary>
        /// Gets how many cells the grid lays along its Y axis, whether or not the body holds any of each.
        /// </summary>
        public int CountY => _starts[1].Length;

        /// <summary>
        /// Gets how many cells the grid lays along its Z axis, whether or not the body holds any of each.
        /// </summary>
        public int CountZ => _starts[2].Length;

        /// <summary>
        /// Gets the cells the body holds any of, a piece each, in order.
        /// </summary>
        public IReadOnlyList<GeoCell3> Cells => _cells;

        /// <summary>
        /// Gets how many cells, a piece each, the body holds any of.
        /// </summary>
        public int CellCount => _cells.Length;

        /// <summary>
        /// Gets the volume the cells hold together: the body's, less the joints.
        /// </summary>
        public double Volume { get; }

        /// <summary>
        /// Gets the tolerance the cells were cut within, which their faces hold to.
        /// </summary>
        internal Tolerance Tolerance => _tolerance;

        /// <summary>
        /// Gets the pieces of the cell at an index, in turn.
        /// </summary>
        /// <param name="i">The index along the grid's X axis.</param>
        /// <param name="j">The index along the grid's Y axis.</param>
        /// <param name="k">The index along the grid's Z axis.</param>
        /// <returns>The pieces; none when the body holds none of the cell, or there is no cell at the index.</returns>
        public GeoCell3[] GetCellsAt(int i, int j, int k)
        {
            if (!ByIndex().TryGetValue((i, j, k), out int[] indexes))
            {
                return new GeoCell3[0];
            }

            var cells = new GeoCell3[indexes.Length];

            for (int n = 0; n < cells.Length; n++)
            {
                cells[n] = _cells[indexes[n]];
            }

            return cells;
        }

        /// <summary>
        /// Gets the whole cell at an index, where it stands in the grid, whether or not the body holds any of it.
        /// </summary>
        /// <param name="i">The index along the grid's X axis.</param>
        /// <param name="j">The index along the grid's Y axis.</param>
        /// <param name="k">The index along the grid's Z axis.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the grid lays no cell at the index.</exception>
        public GeoObb3 GetBox(int i, int j, int k)
        {
            if (i < 0 || i >= CountX)
            {
                throw new ArgumentOutOfRangeException(nameof(i), i, "The grid lays no cell there.");
            }

            if (j < 0 || j >= CountY)
            {
                throw new ArgumentOutOfRangeException(nameof(j), j, "The grid lays no cell there.");
            }

            if (k < 0 || k >= CountZ)
            {
                throw new ArgumentOutOfRangeException(nameof(k), k, "The grid lays no cell there.");
            }

            return BoxOf(Frame, _starts[0][i], _ends[0][i], _starts[1][j], _ends[1][j], _starts[2][k], _ends[2][k]);
        }

        /// <summary>
        /// Gets the cells that share part of a face with a cell: its neighbours along the grid's axes, where no joint stands
        /// between and their material meets over some area.
        /// </summary>
        /// <param name="index">The cell, by its place in <see cref="Cells"/>.</param>
        /// <returns>The neighbours, by their places in <see cref="Cells"/>, in ascending order; none with joints.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such cell.</exception>
        /// <remarks>
        /// Two pieces of one cell never share a face, and pieces of neighbouring cells only where they meet: across the notch
        /// of a U, the piece of one arm meets only the cell beside it in that arm. A neighbour is found where it meets the
        /// cell, not by its indexes: a cut moved onto a corner can leave the next cell along an axis empty and the one after
        /// meeting this one, and the slabs and bars, each cut to its own corners, can leave a cell meeting one beside the
        /// next.
        /// </remarks>
        public int[] GetAdjacentCells(int index)
        {
            if (index < 0 || index >= _cells.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "There is no such cell.");
            }

            if (_adjacent == null)
            {
                _adjacent = CellContact3.Adjacency(_cells, _starts, _ends, Frame, _joint, _tolerance);
            }

            return (int[])_adjacent[index].Clone();
        }

        /// <summary>
        /// Gets the material of every cell, in the order of <see cref="Cells"/>.
        /// </summary>
        public GeoSolid3[] GetSolids()
        {
            var solids = new GeoSolid3[_cells.Length];

            for (int i = 0; i < solids.Length; i++)
            {
                solids[i] = _cells[i].Solid;
            }

            return solids;
        }

        /// <summary>
        /// Describes the grid.
        /// </summary>
        public override string ToString()
            => string.Format(CultureInfo.InvariantCulture, "GeoCellGrid3[{0} x {1} x {2}, Cells: {3}, Volume: {4}]", CountX, CountY, CountZ, CellCount, Volume);

        /// <summary>
        /// The box between two places along each axis of a frame, measured from its origin.
        /// </summary>
        internal static GeoObb3 BoxOf(GeoCoordinateSystem3 frame, double x0, double x1, double y0, double y1, double z0, double z1)
        {
            GeoPoint3 center = frame.ToGlobal(new GeoPoint3(0.5 * (x0 + x1), 0.5 * (y0 + y1), 0.5 * (z0 + z1)));
            return new GeoObb3(frame.WithOrigin(center), x1 - x0, y1 - y0, z1 - z0);
        }

        private Dictionary<(int, int, int), int[]> ByIndex()
        {
            if (_byIndex != null)
            {
                return _byIndex;
            }

            var gathered = new Dictionary<(int, int, int), List<int>>();

            for (int n = 0; n < _cells.Length; n++)
            {
                (int, int, int) key = (_cells[n].I, _cells[n].J, _cells[n].K);

                if (!gathered.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>(1);
                    gathered.Add(key, list);
                }

                list.Add(n);
            }

            var byIndex = new Dictionary<(int, int, int), int[]>(gathered.Count);

            foreach (KeyValuePair<(int, int, int), List<int>> pair in gathered)
            {
                byIndex.Add(pair.Key, pair.Value.ToArray());
            }

            _byIndex = byIndex;
            return byIndex;
        }
    }
}
