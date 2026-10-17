using System.Collections.Generic;

namespace GeometryHelper
{
    /// <summary>
    /// Hashes a key packed from two numbers as <c>(a &lt;&lt; 32) | b</c>, such as the two ends of an edge.
    /// </summary>
    /// <remarks>
    /// The default hash of a long folds its two halves together with an exclusive or, so the key of an edge between
    /// points 1 000 and 1 001 hashes to 1, as does the key of every edge from an even point to the next. On a mesh
    /// whose points are numbered in order most edges fall into a few buckets and every look-up walks a long chain: a
    /// sphere of 320 000 triangles took tens of seconds where it should take a fraction of one. The multiply spreads
    /// every bit of the key over the hash. Equality is the key's own, so a dictionary finds and enumerates the same
    /// entries in the same order with this comparer as without it.
    /// </remarks>
    internal sealed class PackedKeys : IEqualityComparer<long>
    {
        /// <summary>Gets the one instance.</summary>
        internal static PackedKeys Instance { get; } = new PackedKeys();

        private PackedKeys()
        {
        }

        /// <inheritdoc/>
        public bool Equals(long x, long y) => x == y;

        /// <inheritdoc/>
        public int GetHashCode(long key) => unchecked((int)(((ulong)key * 0x9E3779B97F4A7C15UL) >> 32));
    }
}
