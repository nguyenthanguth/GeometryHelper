using System;
using System.Collections.Generic;

namespace GeometryHelper.Arranging
{
    /// <summary>
    /// The obstacles a run keeps clear of, held so that those whose boxes overlap a region are found without going over
    /// every one: a tree of boxes, each branch the box around those below it, taken in one obstacle at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What a query finds is what going over every obstacle in the order it was added, and keeping each whose box the
    /// region overlaps, <see cref="Bounds.Overlaps(Bounds)"/>, finds: the same obstacles, in that order. Each is tested
    /// with that very call, and the tree only passes over branches none of whose obstacles could pass it. A branch box
    /// is made from the boxes below it by <see cref="Bounds.Union(Bounds)"/>, which takes each side as it is, so it
    /// holds them exactly, and a region overlapping a box overlaps any box holding it: each of the four comparisons of
    /// the overlap only grows easier to meet. That holds for a region turned inside out by a negative margin as well.
    /// </para>
    /// <para>
    /// An obstacle whose box has a side that is not a number overlaps nothing, as every comparison with it fails, and is
    /// kept out of the tree. The others go in where they enlarge the boxes on the way least, and the tree is kept
    /// balanced as it grows, the two children of a branch never more than one level apart in height, so that its depth
    /// grows with the logarithm of the count whatever the sizes, regions as large as the sheet beside labels 20 wide,
    /// and whatever the order the obstacles come in: 10 000 boxes added one after another along a line, the worst order
    /// for a tree left unbalanced, stand 15 levels deep, and 10 000 lines from a millionth to ten times the sheet long,
    /// 17.
    /// </para>
    /// <para>
    /// Which obstacles come back never follows the shape of the tree: each is numbered in the order it was added, and
    /// those found are put back in that order.
    /// </para>
    /// </remarks>
    internal sealed class ObstacleSpatialIndex
    {
        private const int None = -1;

        private readonly List<Obstacle> _obstacles = new List<Obstacle>();
        private readonly List<int> _stack = new List<int>();
        private Node[] _nodes = new Node[16];
        private int _nodeCount;
        private int _root = None;

        /// <summary>
        /// Initializes a new, empty instance of the <see cref="ObstacleSpatialIndex"/> class.
        /// </summary>
        internal ObstacleSpatialIndex()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObstacleSpatialIndex"/> class holding the obstacles, numbered in
        /// their order.
        /// </summary>
        /// <param name="obstacles">The obstacles.</param>
        internal ObstacleSpatialIndex(IEnumerable<Obstacle> obstacles)
        {
            foreach (Obstacle obstacle in obstacles)
            {
                Add(obstacle);
            }
        }

        /// <summary>Gets how many obstacles have been added.</summary>
        internal int Count => _obstacles.Count;

        /// <summary>
        /// Gets an obstacle by its number, the order it was added in.
        /// </summary>
        /// <param name="ordinal">The number of the obstacle, counted from nought.</param>
        internal Obstacle this[int ordinal] => _obstacles[ordinal];

        /// <summary>
        /// Adds an obstacle, numbered after every one added before it.
        /// </summary>
        /// <param name="obstacle">The obstacle.</param>
        /// <returns>The number of the obstacle.</returns>
        internal int Add(Obstacle obstacle)
        {
            int ordinal = _obstacles.Count;
            _obstacles.Add(obstacle);

            Bounds box = obstacle.Box;
            if (!double.IsNaN(box.MinX) && !double.IsNaN(box.MinY) && !double.IsNaN(box.MaxX) && !double.IsNaN(box.MaxY))
            {
                InsertLeaf(Allocate(box, ordinal));
            }

            return ordinal;
        }

        /// <summary>
        /// Finds the obstacles whose boxes the region overlaps.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <returns>The obstacles, in the order they were added.</returns>
        internal List<Obstacle> Overlapping(Bounds region)
        {
            var found = new List<int>();
            Query(region, found);

            var obstacles = new List<Obstacle>(found.Count);
            foreach (int ordinal in found)
            {
                obstacles.Add(_obstacles[ordinal]);
            }

            return obstacles;
        }

        /// <summary>
        /// Finds the numbers of the obstacles whose boxes the region overlaps.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <param name="found">Cleared, then given the numbers, smallest first.</param>
        internal void Query(Bounds region, List<int> found)
        {
            found.Clear();
            if (_root == None)
            {
                return;
            }

            _stack.Clear();
            _stack.Add(_root);
            while (_stack.Count > 0)
            {
                int index = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);

                if (!region.Overlaps(_nodes[index].Box))
                {
                    continue;
                }

                if (_nodes[index].Left == None)
                {
                    found.Add(_nodes[index].Ordinal);
                }
                else
                {
                    _stack.Add(_nodes[index].Left);
                    _stack.Add(_nodes[index].Right);
                }
            }

            // The numbers are all different, so the order they sort into is the only one.
            found.Sort();
        }

        private int Allocate(Bounds box, int ordinal)
        {
            if (_nodeCount == _nodes.Length)
            {
                Array.Resize(ref _nodes, 2 * _nodes.Length);
            }

            int index = _nodeCount++;
            _nodes[index] = new Node { Box = box, Parent = None, Left = None, Right = None, Height = 0, Ordinal = ordinal };
            return index;
        }

        /// <summary>
        /// Puts a leaf into the tree beside the node it enlarges least, then refits and rebalances the branches above it.
        /// </summary>
        private void InsertLeaf(int leaf)
        {
            if (_root == None)
            {
                _root = leaf;
                return;
            }

            // Walks down to the sibling: at each branch, stopping where a new branch above it costs less than going into
            // either child, the cost being the half perimeter of the boxes made or enlarged.
            Bounds box = _nodes[leaf].Box;
            int index = _root;
            while (_nodes[index].Left != None)
            {
                int left = _nodes[index].Left;
                int right = _nodes[index].Right;

                double area = HalfPerimeter(_nodes[index].Box);
                double combined = HalfPerimeter(_nodes[index].Box.Union(box));
                double cost = 2.0 * combined;
                double inheritance = 2.0 * (combined - area);
                double costLeft = Descent(left, box) + inheritance;
                double costRight = Descent(right, box) + inheritance;

                if (cost < costLeft && cost < costRight)
                {
                    break;
                }

                index = costLeft < costRight ? left : right;
            }

            int sibling = index;
            int oldParent = _nodes[sibling].Parent;
            int branch = Allocate(_nodes[sibling].Box.Union(box), None);
            _nodes[branch].Parent = oldParent;
            _nodes[branch].Left = sibling;
            _nodes[branch].Right = leaf;
            _nodes[branch].Height = _nodes[sibling].Height + 1;
            _nodes[sibling].Parent = branch;
            _nodes[leaf].Parent = branch;

            if (oldParent == None)
            {
                _root = branch;
            }
            else if (_nodes[oldParent].Left == sibling)
            {
                _nodes[oldParent].Left = branch;
            }
            else
            {
                _nodes[oldParent].Right = branch;
            }

            for (index = _nodes[leaf].Parent; index != None; index = _nodes[index].Parent)
            {
                index = Balance(index);
                Refit(index);
            }
        }

        /// <summary>
        /// What going into a child costs: the half perimeter of the leaf and a leaf child together, or what a branch child
        /// grows by.
        /// </summary>
        private double Descent(int child, Bounds box)
        {
            double enlarged = HalfPerimeter(_nodes[child].Box.Union(box));
            return _nodes[child].Left == None ? enlarged : enlarged - HalfPerimeter(_nodes[child].Box);
        }

        /// <summary>
        /// Rotates the higher grandchild up where the two children of a branch differ in height by more than one.
        /// </summary>
        /// <returns>The node now standing where the branch stood.</returns>
        private int Balance(int a)
        {
            if (_nodes[a].Left == None || _nodes[a].Height < 2)
            {
                return a;
            }

            int b = _nodes[a].Left;
            int c = _nodes[a].Right;
            int balance = _nodes[c].Height - _nodes[b].Height;

            if (balance > 1)
            {
                return RotateUp(a, c, b, false);
            }

            if (balance < -1)
            {
                return RotateUp(a, b, c, true);
            }

            return a;
        }

        /// <summary>
        /// Lifts the child <paramref name="up"/> of <paramref name="a"/> into its place. Of the two children of the
        /// lifted node, the higher stays with it and the lower goes to <paramref name="a"/>, in the place the lifted node
        /// left there, beside <paramref name="stay"/>.
        /// </summary>
        /// <param name="a">The branch out of balance.</param>
        /// <param name="up">Its higher child.</param>
        /// <param name="stay">Its other child.</param>
        /// <param name="upIsLeft">Whether the higher child is the left one.</param>
        /// <returns>The lifted node.</returns>
        private int RotateUp(int a, int up, int stay, bool upIsLeft)
        {
            int f = _nodes[up].Left;
            int g = _nodes[up].Right;

            // The lifted node takes the place of a under its parent, and a becomes its left child.
            _nodes[up].Left = a;
            _nodes[up].Parent = _nodes[a].Parent;
            _nodes[a].Parent = up;

            int parent = _nodes[up].Parent;
            if (parent == None)
            {
                _root = up;
            }
            else if (_nodes[parent].Left == a)
            {
                _nodes[parent].Left = up;
            }
            else
            {
                _nodes[parent].Right = up;
            }

            int keep = _nodes[f].Height > _nodes[g].Height ? f : g;
            int give = keep == f ? g : f;

            _nodes[up].Right = keep;
            if (upIsLeft)
            {
                _nodes[a].Left = give;
            }
            else
            {
                _nodes[a].Right = give;
            }

            _nodes[give].Parent = a;

            _nodes[a].Box = _nodes[stay].Box.Union(_nodes[give].Box);
            _nodes[a].Height = 1 + Math.Max(_nodes[stay].Height, _nodes[give].Height);
            _nodes[up].Box = _nodes[a].Box.Union(_nodes[keep].Box);
            _nodes[up].Height = 1 + Math.Max(_nodes[a].Height, _nodes[keep].Height);
            return up;
        }

        private void Refit(int branch)
        {
            int left = _nodes[branch].Left;
            int right = _nodes[branch].Right;
            _nodes[branch].Box = _nodes[left].Box.Union(_nodes[right].Box);
            _nodes[branch].Height = 1 + Math.Max(_nodes[left].Height, _nodes[right].Height);
        }

        private static double HalfPerimeter(Bounds box) => (box.MaxX - box.MinX) + (box.MaxY - box.MinY);

        /// <summary>
        /// A node of the tree: a leaf, one obstacle, with no children, or a branch with two.
        /// </summary>
        private struct Node
        {
            /// <summary>The box of the obstacle, or the box around every obstacle below the branch.</summary>
            internal Bounds Box;

            /// <summary>The branch above, or none at the root.</summary>
            internal int Parent;

            /// <summary>The first child, or none for a leaf.</summary>
            internal int Left;

            /// <summary>The second child, or none for a leaf.</summary>
            internal int Right;

            /// <summary>How many levels lie below: nought for a leaf.</summary>
            internal int Height;

            /// <summary>The number of the obstacle of a leaf.</summary>
            internal int Ordinal;
        }
    }
}
