using System.Collections.Generic;
using CommonGeometry;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
{
    /// <summary>
    /// Gives every distinct position a stable index, treating positions within tolerance as the same one.
    /// <para>
    /// Matching within a tolerance cannot use a dictionary directly: two points that should match may round
    /// to different keys, and a hash of a coordinate says nothing about which coordinates are near it. The
    /// way round is to lay a grid over space with cells the size of the tolerance, so anything close enough
    /// to match is in the same cell or one of the twenty-six touching it. Searching those twenty-seven
    /// buckets turns matching a vertex from a scan of everything seen so far into a scan of its immediate
    /// neighbourhood, which is what takes welding a surface from quadratic time to linear.
    /// </para>
    /// </summary>
    internal sealed class VertexWelder
    {
        private readonly Dictionary<long, List<int>> _buckets = new Dictionary<long, List<int>>();
        private readonly List<GeoPoint3> _vertices = new List<GeoPoint3>();
        private readonly Tolerance _tolerance;
        private readonly double _cellSize;

        /// <summary>
        /// Initializes a welder matching positions within a tolerance.
        /// </summary>
        public VertexWelder(Tolerance tolerance)
        {
            _tolerance = tolerance;

            // A cell smaller than the tolerance would put matching points more than one cell apart and the
            // neighbour search would miss them. A much larger one would work but would pile unrelated
            // points into the same bucket.
            _cellSize = tolerance.EqualPoint > 0.0 ? tolerance.EqualPoint * 2.0 : 1E-9;
        }

        /// <summary>
        /// Gets how many distinct positions have been seen.
        /// </summary>
        public int Count => _vertices.Count;

        /// <summary>
        /// Gets the index of a position, giving it a new one the first time it is seen.
        /// </summary>
        public int GetIndex(GeoPoint3 point)
        {
            long cellX = ToCell(point.X);
            long cellY = ToCell(point.Y);
            long cellZ = ToCell(point.Z);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!_buckets.TryGetValue(Hash(cellX + dx, cellY + dy, cellZ + dz), out List<int> bucket))
                        {
                            continue;
                        }

                        foreach (int index in bucket)
                        {
                            if (_vertices[index].IsEqualTo(point, _tolerance))
                            {
                                return index;
                            }
                        }
                    }
                }
            }

            int added = _vertices.Count;
            _vertices.Add(point);

            long ownCell = Hash(cellX, cellY, cellZ);

            if (!_buckets.TryGetValue(ownCell, out List<int> own))
            {
                own = new List<int>();
                _buckets[ownCell] = own;
            }

            own.Add(added);

            return added;
        }

        /// <summary>
        /// Gets which grid cell a coordinate falls in.
        /// </summary>
        private long ToCell(double value)
        {
            double scaled = value / _cellSize;

            return (long)(scaled >= 0.0 ? scaled : scaled - 1.0);
        }

        /// <summary>
        /// Mixes three cell indices into one bucket key.
        /// </summary>
        /// <remarks>
        /// The three indices cannot be packed into a long without limiting how far from the origin the
        /// geometry may sit, so they are hashed instead. A collision costs only a few extra comparisons,
        /// because every candidate is checked against the real coordinates before it is accepted.
        /// </remarks>
        private static long Hash(long cellX, long cellY, long cellZ)
        {
            unchecked
            {
                long hash = cellX * 73856093L;
                hash ^= cellY * 19349663L;
                hash ^= cellZ * 83492791L;
                return hash;
            }
        }
    }
}
