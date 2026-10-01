#nullable enable
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
    }
}
