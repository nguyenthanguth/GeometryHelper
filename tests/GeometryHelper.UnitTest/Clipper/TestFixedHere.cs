using System;
using System.Collections.Generic;
using GeometryHelper.Clipper;
using Xunit;

namespace GeometryHelper.UnitTest.Clipper
{
    /// <summary>
    /// Faults of Clipper2 2.0.0 mended in this copy, each held by a test that failed before its mending.
    /// </summary>
    public class TestFixedHere
    {
        /// <summary>
        /// With inclusive, a first segment starting on the line through the second counted as touching it even where the
        /// second does not reach: these two lie five units apart.
        /// </summary>
        [Fact]
        public void SegsIntersect_Inclusive_AStartOnTheLineBeyondTheOther_DoesNotTouch()
        {
            Point64 bottom = new Point64(5, 0), right = new Point64(10, 0);

            Assert.False(InternalClipper.SegsIntersect(new Point64(0, 0), new Point64(0, 10), bottom, right, true));

            // what touches still does, and only with inclusive
            Assert.True(InternalClipper.SegsIntersect(new Point64(7, 0), new Point64(7, 10), bottom, right, true));
            Assert.True(InternalClipper.SegsIntersect(new Point64(5, 0), new Point64(5, 10), bottom, right, true));
            Assert.False(InternalClipper.SegsIntersect(new Point64(7, 0), new Point64(7, 10), bottom, right, false));
            Assert.True(InternalClipper.SegsIntersect(new Point64(7, -5), new Point64(7, 5), bottom, right, false));
        }

        /// <summary>
        /// Every pair of segments between the points of a four by four grid, held against orientations worked out
        /// exactly: two segments that are not parallel cross when each has the ends of the other on either side of its
        /// line, and with inclusive touch when an end may lie on that line as well. Parallel segments never count.
        /// </summary>
        [Fact]
        public void SegsIntersect_AgreesWithExactOrientations()
        {
            List<Point64> points = new List<Point64>();

            for (int x = 0; x <= 3; x++)
            {
                for (int y = 0; y <= 3; y++)
                {
                    points.Add(new Point64(x, y));
                }
            }

            int wrong = 0;
            string first = null;

            foreach (Point64 a in points)
            {
                foreach (Point64 b in points)
                {
                    foreach (Point64 c in points)
                    {
                        foreach (Point64 d in points)
                        {
                            if (a == b || c == d)
                            {
                                continue;
                            }

                            foreach (bool inclusive in new[] { false, true })
                            {
                                bool expected = Meet(a, b, c, d, inclusive);

                                if (InternalClipper.SegsIntersect(a, b, c, d, inclusive) != expected)
                                {
                                    wrong++;
                                    first = first ?? $"({a.X},{a.Y})-({b.X},{b.Y}) and ({c.X},{c.Y})-({d.X},{d.Y}), inclusive {inclusive}, should be {expected}";
                                }
                            }
                        }
                    }
                }
            }

            Assert.True(wrong == 0, $"{wrong} pairs answered wrong, the first {first}");
        }

        private static bool Meet(Point64 a, Point64 b, Point64 c, Point64 d, bool inclusive)
        {
            if ((b.X - a.X) * (d.Y - c.Y) - (b.Y - a.Y) * (d.X - c.X) == 0)
            {
                return false;
            }

            int sidesOfC = Side(a, b, c) * Side(a, b, d);
            int sidesOfA = Side(c, d, a) * Side(c, d, b);
            return inclusive ? sidesOfC <= 0 && sidesOfA <= 0 : sidesOfC < 0 && sidesOfA < 0;
        }

        private static int Side(Point64 from, Point64 to, Point64 point)
            => Math.Sign((to.X - from.X) * (point.Y - from.Y) - (to.Y - from.Y) * (point.X - from.X));

        /// <summary>
        /// ClipperD answered true for a sweep that had marked itself failed, with what it had built until then, where
        /// Clipper64 and Clipper2's C++ answer false.
        /// </summary>
        [Fact]
        public void ClipperD_ASweepThatFailed_AnswersFalse_WithNothing()
        {
            Clipper64 integers = new Clipper64();
            integers.AddReuseableData(DataTheSweepFailsOn());
            Assert.False(integers.Execute(ClipType.Difference, FillRule.Negative, new Paths64()));

            PathsD closed = new PathsD(), open = new PathsD();
            Assert.False(new ReusingClipperD(DataTheSweepFailsOn()).Execute(ClipType.Difference, FillRule.Negative, closed, open));
            Assert.Empty(closed);
            Assert.Empty(open);

            PolyTreeD tree = new PolyTreeD();
            Assert.False(new ReusingClipperD(DataTheSweepFailsOn()).Execute(ClipType.Difference, FillRule.Negative, tree, open));
            Assert.Equal(0, tree.Count);
            Assert.Empty(open);
        }

        /// <summary>
        /// A loop crossing itself, made reusable data, with its local minimum at (5,5) then marked as a clip, which no
        /// path added the usual way can give. The sweep cannot pair that minimum's bounds with the rest of the loop and
        /// marks itself failed, without throwing; paths added the usual way have not made it fail once in sixteen
        /// million random clips.
        /// </summary>
        private static ReuseableDataContainer64 DataTheSweepFailsOn()
        {
            ReuseableDataContainer64 data = new ReuseableDataContainer64();
            data.AddPaths(new Paths64 { Clipper2.MakePath(new long[] { 5, 1, 0, 4, 4, 3, 5, 5, 3, 1, 6, 6 }) }, PathType.Subject, false);

            int at = data._minimaList.FindIndex(m => m.vertex.pt == new Point64(5, 5));
            LocalMinima minimum = data._minimaList[at];
            data._minimaList[at] = new LocalMinima(minimum.vertex, PathType.Clip, minimum.isOpen);
            return data;
        }

        /// <summary>
        /// A ClipperD that starts from reusable data, which only Clipper64 takes otherwise.
        /// </summary>
        private sealed class ReusingClipperD : ClipperD
        {
            public ReusingClipperD(ReuseableDataContainer64 data) : base(0)
            {
                AddReuseableData(data);
            }
        }
    }
}
