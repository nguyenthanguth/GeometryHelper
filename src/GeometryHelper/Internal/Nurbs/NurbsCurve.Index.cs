using System;
using System.Collections.Generic;

namespace GeometryHelper.Internal.Nurbs
{
    /// <summary>
    /// Says how far a region of the question lies from a box: a lower bound on the distance from any point of the box
    /// to whatever is asked about, so that a part of the curve whose box is further than the answer so far is not looked
    /// at. A struct, so that the search written once below is compiled for each question with no call through a delegate.
    /// </summary>
    internal interface IBound
    {
        /// <summary>
        /// Gets the bound for the box whose smallest coordinates start at an offset of one array and largest at the same
        /// offset of the other, Dim numbers each.
        /// </summary>
        double Bound(double[] min, double[] max, int at);
    }

    /// <summary>
    /// What a search does with each piece it reaches, and how far it still looks.
    /// </summary>
    internal interface IVisit
    {
        /// <summary>
        /// Gets how large a bound may be and still be looked at; it may fall as the search goes, and a bound above it is passed over.
        /// </summary>
        double Limit { get; }

        /// <summary>
        /// Takes a piece the search reached, with its bound.
        /// </summary>
        void Piece(int piece, double bound);
    }

    /// <summary>
    /// What a search over the pieces of two curves does with each pair of pieces it reaches, and how far apart it still looks.
    /// </summary>
    internal interface IPairVisit
    {
        /// <summary>
        /// Gets how far apart the boxes of two pieces may be and the pair still be looked at; it may fall as the search goes.
        /// </summary>
        double Limit { get; }

        /// <summary>
        /// Takes a pair of pieces, one of each curve.
        /// </summary>
        void Pair(int first, int second);
    }

    /// <summary>
    /// The boxes of the Bézier pieces of a curve gathered in a tree, so that a question about a small part of the plane
    /// or of space reaches only the pieces near it, and takes time in proportion to their number and the logarithm of the
    /// number of pieces, not to the number of pieces.
    /// <para>
    /// The pieces are grouped as the curve runs, the first two, the next two, and so on, each pair under a box that holds
    /// both, up to one box over the whole: a curve is a chain, so neighbours lie together and the boxes stay tight. The tree
    /// is built with no sorting, so it is the same whenever it is built.
    /// </para>
    /// </summary>
    internal sealed class PieceIndex
    {
        private readonly int _dim;
        private readonly int _count;
        private readonly int _leaves;
        private readonly double[] _min;
        private readonly double[] _max;

        /// <summary>
        /// Builds the tree over the pieces of a curve.
        /// </summary>
        internal PieceIndex(BezierPieces pieces)
        {
            _dim = pieces.Dim;
            _count = pieces.Count;

            int leaves = 1;

            while (leaves < _count)
            {
                leaves <<= 1;
            }

            _leaves = leaves;
            _min = new double[2 * leaves * _dim];
            _max = new double[2 * leaves * _dim];

            for (int i = 0; i < _min.Length; i++)
            {
                _min[i] = double.PositiveInfinity;
                _max[i] = double.NegativeInfinity;
            }

            for (int piece = 0; piece < _count; piece++)
            {
                Array.Copy(pieces.BoxMin, piece * _dim, _min, (leaves + piece) * _dim, _dim);
                Array.Copy(pieces.BoxMax, piece * _dim, _max, (leaves + piece) * _dim, _dim);
            }

            for (int node = leaves - 1; node >= 1; node--)
            {
                for (int k = 0; k < _dim; k++)
                {
                    _min[node * _dim + k] = Math.Min(_min[2 * node * _dim + k], _min[(2 * node + 1) * _dim + k]);
                    _max[node * _dim + k] = Math.Max(_max[2 * node * _dim + k], _max[(2 * node + 1) * _dim + k]);
                }
            }
        }

        /// <summary>The number of pieces under the tree.</summary>
        internal int Count => _count;

        /// <summary>
        /// Gets the smallest coordinates of the box over the whole curve, Dim numbers.
        /// </summary>
        internal void Whole(double[] min, double[] max)
        {
            Array.Copy(_min, _dim, min, 0, _dim);
            Array.Copy(_max, _dim, max, 0, _dim);
        }

        /// <summary>
        /// Adds to a list every piece whose bound is no more than a limit, in no order.
        /// </summary>
        internal void Collect<TBound>(ref TBound region, double limit, List<int> found) where TBound : struct, IBound
        {
            var stack = new int[2 * 40 + 2];
            int top = 0;
            stack[top++] = 1;

            while (top > 0)
            {
                int node = stack[--top];

                if (!(region.Bound(_min, _max, node * _dim) <= limit))
                {
                    continue;
                }

                if (node >= _leaves)
                {
                    if (node - _leaves < _count)
                    {
                        found.Add(node - _leaves);
                    }

                    continue;
                }

                stack[top++] = 2 * node + 1;
                stack[top++] = 2 * node;
            }
        }

        /// <summary>
        /// Reaches the pieces whose bound is within the limit of a visit, the nearer child of each box first, so that the
        /// limit falls early and most of the tree is passed over.
        /// </summary>
        internal void Search<TBound, TVisit>(ref TBound region, ref TVisit visit)
            where TBound : struct, IBound
            where TVisit : struct, IVisit
        {
            var nodes = new int[2 * 40 + 2];
            var bounds = new double[2 * 40 + 2];
            int top = 0;
            nodes[top] = 1;
            bounds[top++] = region.Bound(_min, _max, _dim);

            while (top > 0)
            {
                top--;
                int node = nodes[top];
                double bound = bounds[top];

                if (bound > visit.Limit)
                {
                    continue;
                }

                if (node >= _leaves)
                {
                    // The leaves past the last piece are empty, and their bound is infinite.
                    if (node - _leaves < _count)
                    {
                        visit.Piece(node - _leaves, bound);
                    }

                    continue;
                }

                double left = region.Bound(_min, _max, 2 * node * _dim);
                double right = region.Bound(_min, _max, (2 * node + 1) * _dim);

                // The nearer is pushed last, so it is taken first; on equal bounds the left, the earlier piece.
                if (right < left)
                {
                    nodes[top] = 2 * node;
                    bounds[top++] = left;
                    nodes[top] = 2 * node + 1;
                    bounds[top++] = right;
                }
                else
                {
                    nodes[top] = 2 * node + 1;
                    bounds[top++] = right;
                    nodes[top] = 2 * node;
                    bounds[top++] = left;
                }
            }
        }

        /// <summary>
        /// Reaches every pair of pieces, one of this curve and one of another, whose boxes are no further apart than the
        /// limit of a visit, by going down both trees together and passing over every pair of boxes that is too far apart
        /// along with all the pieces under it.
        /// </summary>
        internal void Pairs<TVisit>(PieceIndex other, ref TVisit visit) where TVisit : struct, IPairVisit
        {
            var stack = new int[4 * 40 + 4];
            int top = 0;
            stack[top++] = 1;
            stack[top++] = 1;

            while (top > 0)
            {
                int b = stack[--top];
                int a = stack[--top];

                if (!(Gap(other, a, b) <= visit.Limit))
                {
                    continue;
                }

                bool aLeaf = a >= _leaves;
                bool bLeaf = b >= other._leaves;

                if (aLeaf && bLeaf)
                {
                    if (a - _leaves < _count && b - other._leaves < other._count)
                    {
                        visit.Pair(a - _leaves, b - other._leaves);
                    }

                    continue;
                }

                if (!aLeaf && (bLeaf || Size(a) >= other.Size(b)))
                {
                    stack[top++] = 2 * a + 1;
                    stack[top++] = b;
                    stack[top++] = 2 * a;
                    stack[top++] = b;
                }
                else
                {
                    stack[top++] = a;
                    stack[top++] = 2 * b + 1;
                    stack[top++] = a;
                    stack[top++] = 2 * b;
                }
            }
        }

        /// <summary>
        /// Gets the distance between the box of a node and the box of a node of another tree; infinite for an empty box.
        /// </summary>
        private double Gap(PieceIndex other, int a, int b)
        {
            if (_min[a * _dim] > _max[a * _dim] || other._min[b * _dim] > other._max[b * _dim])
            {
                return double.PositiveInfinity;
            }

            double square = 0.0;

            for (int k = 0; k < _dim; k++)
            {
                double below = other._min[b * _dim + k] - _max[a * _dim + k];
                double above = _min[a * _dim + k] - other._max[b * _dim + k];
                double gap = below > 0.0 ? below : above > 0.0 ? above : 0.0;
                square += gap * gap;
            }

            return Math.Sqrt(square);
        }

        /// <summary>
        /// Gets the longest side of the box of a node.
        /// </summary>
        private double Size(int node)
        {
            double size = double.NegativeInfinity;

            for (int k = 0; k < _dim; k++)
            {
                size = Math.Max(size, _max[node * _dim + k] - _min[node * _dim + k]);
            }

            return size;
        }
    }

    /// <summary>
    /// The bound of a point: how far it is from a box.
    /// </summary>
    internal readonly struct PointBound : IBound
    {
        private readonly double[] _query;

        internal PointBound(double[] query)
        {
            _query = query;
        }

        public double Bound(double[] min, double[] max, int at)
        {
            double square = 0.0;

            for (int k = 0; k < _query.Length; k++)
            {
                double low = min[at + k];
                double high = max[at + k];
                double gap = _query[k] < low ? low - _query[k] : _query[k] > high ? _query[k] - high : 0.0;
                square += gap * gap;
            }

            return Math.Sqrt(square);
        }
    }

    internal sealed partial class NurbsCurve
    {
        private volatile PieceIndex _index;

        /// <summary>
        /// Gets the tree over the boxes of the curve's pieces, worked out once.
        /// </summary>
        internal PieceIndex Index
        {
            get
            {
                PieceIndex index = _index;

                if (index == null)
                {
                    index = new PieceIndex(Pieces);
                    _index = index;
                }

                return index;
            }
        }
    }
}
