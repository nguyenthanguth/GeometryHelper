using System;
using GeometryHelper.Clipper;
using Xunit;

namespace GeometryHelper.UnitTest.Clipper
{
    /// <summary>
    /// Cases found here rather than upstream: the area Clipper2 gives on integer input, held against the area of the same
    /// operation resolved a millionth finer, which sampling the fill rule on a grid of points confirms. They keep what
    /// 2.0.0 gets right, so that a change to the engine that gets it wrong shows.
    /// </summary>
    public class TestCasesFoundHere
    {
        /// <summary>
        /// A union of three loops crossing themselves and each other, even-odd. 2.0.0 gives 394 791, 0.09 % short of the
        /// 395 137.7 it should; with upstream's change to FixSelfIntersects after 2.0.0 (4da1564, #1067), which mends
        /// test 16 of Polygons.txt, it gives 399 967.5, 1.2 % over.
        /// </summary>
        [Fact]
        public void UnionOfLoopsCrossingThemselves_KeepsItsArea()
        {
            Paths64 subject = new()
            {
                Clipper2.MakePath(new int[] { 70, 512, 971, 11, 813, 61, 453, 155, 592, 19, 995, 425, 711, 571, 954, 133, 957, 984, 906, 835, 211, 641 }),
            };
            Paths64 clip = new()
            {
                Clipper2.MakePath(new int[] { 664, 429, 84, 124, 943, 942, 402, 430, 373, 234, 874, 479 }),
                Clipper2.MakePath(new int[] { 533, 504, 672, 874, 347, 570, 617, 749 }),
            };
            Clipper64 c64 = new() { PreserveCollinear = false };
            c64.AddSubject(subject);
            c64.AddClip(clip);
            Paths64 solution = new();
            c64.Execute(ClipType.Union, FillRule.EvenOdd, solution);

            const double exact = 395137.7;
            double area = Clipper2.Area(solution);
            Assert.True(Math.Abs(area - exact) < 0.005 * exact, $"area {area}, {exact} exactly");
        }

        /// <summary>
        /// A union whose polytree has three islands in a hole that touches them, each wound as an outer is. With the test
        /// of a closed path written as Clipper2's C++ writes it, which empties rings of two points and very small
        /// triangles in CleanCollinear at once, the tree nested the three as holes of the outer instead: in 600 000
        /// random cases it nested islands so in four, the C# test in none. So the C# test stays.
        /// </summary>
        [Fact]
        public void PolyTreeOfIslandsTouchingTheirHole_NestsThemByTheirWinding()
        {
            Paths64 subject = new()
            {
                Clipper2.MakePath(new int[] { 34, 25, 4, 35, 7, 2, 16, 39, 24, 11, 9, 24, 22, 6, 20, 33 }),
                Clipper2.MakePath(new int[] { 14, 3, 33, 33, 11, 5, 6, 20, 35, 12 }),
                Clipper2.MakePath(new int[] { 7, 12, 4, 34, 9, 7, 14, 11, 6, 19, 30, 22, 19, 15, 32, 20 }),
            };
            Paths64 clip = new()
            {
                Clipper2.MakePath(new int[] { 24, 2, 22, 9, 17, 25, 30, 24, 35, 6, 22, 19, 38, 15, 12, 31 }),
                Clipper2.MakePath(new int[] { 32, 12, 6, 15, 14, 15, 32, 8, 26, 0, 25, 22 }),
                Clipper2.MakePath(new int[] { 18, 20, 26, 10, 36, 33, 30, 25 }),
            };
            Clipper64 c64 = new() { PreserveCollinear = true };
            c64.AddSubject(subject);
            c64.AddClip(clip);
            PolyTree64 tree = new();
            Assert.True(c64.Execute(ClipType.Union, FillRule.EvenOdd, tree));

            Assert.Equal(0, WoundAgainstTheirDepth(tree));
            Assert.Equal(3, AtLevel(tree, 3));
        }

        // Nodes whose winding disagrees with their depth: a hole winds clockwise with Y up, an outer counter-clockwise.
        private static int WoundAgainstTheirDepth(PolyPath64 node)
        {
            int count = node.Polygon != null && (Clipper2.Area(node.Polygon) < 0) != node.IsHole ? 1 : 0;

            for (int i = 0; i < node.Count; i++)
            {
                count += WoundAgainstTheirDepth(node[i]);
            }

            return count;
        }

        private static int AtLevel(PolyPath64 node, int level)
        {
            int count = node.Level == level ? 1 : 0;

            for (int i = 0; i < node.Count; i++)
            {
                count += AtLevel(node[i], level);
            }

            return count;
        }
    }
}
