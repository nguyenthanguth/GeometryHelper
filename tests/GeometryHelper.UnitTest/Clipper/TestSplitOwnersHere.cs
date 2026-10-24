using System.Threading;
using GeometryHelper.Clipper;
using Xunit;

namespace GeometryHelper.UnitTest.Clipper
{
    /// <summary>
    /// A polytree whose paths split off one another in a ring: Clipper2 2.0.0 looked for the owner of a path among the splits
    /// of a path with no points by recursion, and where those splits led back to a list it was already walking it walked it
    /// again from the start, for ever, until the stack overflowed and took the process with it. The 25 triangles standing on
    /// y = 0 below are what was left of case 194 of seed 1 of the LN9 clip fuzz, 175 of them, with every one that was not
    /// needed for the overflow taken out.
    /// </summary>
    public class TestSplitOwnersHere
    {
        private static Path64 Triangle(long x1, long y1, long x2, long y2, long x3, long y3)
            => new Path64 { new Point64(x1, y1), new Point64(x2, y2), new Point64(x3, y3) };

        private static Paths64 Subject() => new Paths64
        {
            Triangle(278, 0, 304, 0, 290, 38), Triangle(64, 13, 66, 0, 64, 0), Triangle(269, 0, 295, 0, 281, 25),
            Triangle(369, 0, 393, 0, 380, 13), Triangle(295, 0, 320, 0, 305, 8), Triangle(261, 37, 272, 0, 250, 0),
            Triangle(300, 31, 311, 0, 291, 0), Triangle(291, 0, 317, 0, 306, 2), Triangle(263, 2, 271, 0, 259, 0),
            Triangle(287, 36, 296, 0, 283, 0), Triangle(287, 0, 312, 0, 300, 5), Triangle(110, 30, 120, 0, 96, 0),
            Triangle(275, 0, 301, 0, 289, 40), Triangle(195, 0, 213, 0, 204, 18), Triangle(282, 0, 304, 0, 295, 18),
            Triangle(260, 0, 271, 0, 266, 22), Triangle(295, 0, 298, 0, 294, 10), Triangle(45, 40, 61, 0, 31, 0),
        };

        private static Paths64 Clip() => new Paths64
        {
            Triangle(208, 0, 210, 0, 210, 15), Triangle(261, 0, 291, 0, 278, 27), Triangle(49, 31, 56, 0, 40, 0),
            Triangle(268, 0, 298, 0, 282, 16), Triangle(110, 0, 135, 0, 123, 31), Triangle(57, 38, 66, 0, 53, 0),
            Triangle(375, 0, 397, 0, 385, 7),
        };

        [Fact]
        public void PolyTree_SplitsLeadingBackToAListBeingWalked_IsBuiltWithinAStackOfAMegabyte()
        {
            var clipper = new Clipper64();
            clipper.AddSubject(Subject());
            clipper.AddClip(Clip());
            var tree = new PolyTree64();
            bool done = false;

            // On a thread of its own with the stack a thread is given by default, so that the walk is held to it.
            var thread = new Thread(() => done = clipper.Execute(ClipType.Intersection, FillRule.EvenOdd, tree, new Paths64()), 1 << 20);
            thread.Start();
            thread.Join();

            Assert.True(done);

            // The tree holds the same paths as the plain answer: as many, over the same area.
            var plain = new Clipper64();
            plain.AddSubject(Subject());
            plain.AddClip(Clip());
            var closed = new Paths64();
            Assert.True(plain.Execute(ClipType.Intersection, FillRule.EvenOdd, closed));

            Paths64 fromTree = Clipper2.PolyTreeToPaths64(tree);
            Assert.Equal(closed.Count, fromTree.Count);
            Assert.Equal(Clipper2.Area(closed), Clipper2.Area(fromTree));
        }
    }
}
