using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// What a view, an annotation or a plugin run is tied to in the model: the ids of its objects and the points it
    /// measures to. When the model changes, the drawing works its values and places out again from these.
    /// </summary>
    /// <remarks>
    /// A mark of the plate P-1 links the object P-1 alone; the overall dimension of a beam B-12 3 000 long links B-12 and
    /// the two points (0, 0, 0) and (3000, 0, 0) on it. Links do not change once made, and two are equal when their ids are
    /// the same in the same order and their points the same to the bit (<see cref="DrawingLinkPoint3"/>).
    /// </remarks>
    public sealed class DrawingLinks : IEquatable<DrawingLinks>
    {
        /// <summary>Links to nothing.</summary>
        public static readonly DrawingLinks Empty = new DrawingLinks(null);

        private readonly string[] _objectIds;
        private readonly DrawingLinkPoint3[] _points;

        /// <summary>Makes links to objects and points of the model.</summary>
        /// <param name="objectIds">The ids of the objects, in the order given; null for none.</param>
        /// <param name="points">The points, in the order given; null for none.</param>
        /// <exception cref="ArgumentException">Thrown when an object id is null or empty.</exception>
        public DrawingLinks(IEnumerable<string> objectIds, IEnumerable<DrawingLinkPoint3> points = null)
        {
            var ids = new List<string>();

            if (objectIds != null)
            {
                foreach (string id in objectIds)
                {
                    if (string.IsNullOrEmpty(id))
                    {
                        throw new ArgumentException("A linked object id is not null or empty.", nameof(objectIds));
                    }

                    ids.Add(id);
                }
            }

            _objectIds = ids.ToArray();
            _points = points == null ? new DrawingLinkPoint3[0] : new List<DrawingLinkPoint3>(points).ToArray();
        }

        /// <summary>Gets the ids of the objects linked, in the order given.</summary>
        public IReadOnlyList<string> ObjectIds => _objectIds;

        /// <summary>Gets the points linked, in the order given.</summary>
        public IReadOnlyList<DrawingLinkPoint3> Points => _points;

        /// <summary>Whether these links are the same as others: the same ids and points in the same order.</summary>
        /// <param name="other">The other links.</param>
        /// <returns>Whether they are the same.</returns>
        public bool Equals(DrawingLinks other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || _objectIds.Length != other._objectIds.Length || _points.Length != other._points.Length)
            {
                return false;
            }

            for (int i = 0; i < _objectIds.Length; i++)
            {
                if (!string.Equals(_objectIds[i], other._objectIds[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int i = 0; i < _points.Length; i++)
            {
                if (!_points[i].Equals(other._points[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether an object is links the same as these.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is.</returns>
        public override bool Equals(object obj) => Equals(obj as DrawingLinks);

        /// <summary>Gives a hash code that agrees with <see cref="Equals(DrawingLinks)"/>.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _objectIds.Length;

                foreach (string id in _objectIds)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(id);
                }

                foreach (DrawingLinkPoint3 point in _points)
                {
                    hash = (hash * 397) ^ point.GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>Gives the links as a line for debugging.</summary>
        /// <returns>The line, such as <c>2 objects, 2 points</c>.</returns>
        public override string ToString() => _objectIds.Length + " objects, " + _points.Length + " points";
    }
}
