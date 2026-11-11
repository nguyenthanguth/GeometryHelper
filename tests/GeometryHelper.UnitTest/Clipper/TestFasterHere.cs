using System.Diagnostics;
using GeometryHelper.Clipper;
using Xunit;

namespace GeometryHelper.UnitTest.Clipper
{
    /// <summary>
    /// Work of Clipper2 2.0.0 that grew with the square of the local minima, maxima or horizontals on one scanline, faster in
    /// this copy with the same paths, each held to a time several times what it takes now and well under what it took.
    /// </summary>
    public class TestFasterHere
    {
        [Fact]
        public void AZigzagOf100000Vertices_IsUnitedInUnderThreeSeconds()
        {
            // Its 50 000 minima lie on one scanline, and each walked the active edge list from its head to its place: six
            // seconds. The walk starts where one to its left stopped.
            var zigzag = new Path64();

            for (int i = 0; i <= 100000; i++)
            {
                zigzag.Add(new Point64(i * 10L, i % 2 == 0 ? 0 : 100));
            }

            zigzag.Add(new Point64(1000000, 200));
            zigzag.Add(new Point64(0, 200));

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(new Paths64 { zigzag }, FillRule.NonZero);
            watch.Stop();

            Assert.Single(union);
            Assert.Equal(Clipper2.Area(zigzag), Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void ACombOf25000Teeth_IsUnitedInUnderThreeSeconds()
        {
            // Each tooth's side ends at a horizontal, with no edge to pair with in the list, and the search for one walked to the
            // list's end: five of its seven seconds; the 50 000 horizontals on its two lines were each tried against every other.
            var comb = new Path64();
            long x = 0;

            for (int i = 0; i < 25000; i++)
            {
                comb.Add(new Point64(x, 0));
                comb.Add(new Point64(x + 10, 0));
                comb.Add(new Point64(x + 10, 100));
                comb.Add(new Point64(x + 20, 100));
                x += 20;
            }

            comb.Add(new Point64(x, 0));
            comb.Add(new Point64(x + 10, 0));
            comb.Add(new Point64(x + 10, 200));
            comb.Add(new Point64(0, 200));

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(new Paths64 { comb }, FillRule.NonZero);
            watch.Stop();

            Assert.Single(union);
            Assert.Equal(Clipper2.Area(comb), Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void ACombOf40000TeethPointingUpShrunkBy1_IsOffsetInUnderThreeSeconds()
        {
            // The union of its raw outline does the horizontals at the tips from the right, and each output path started there
            // looked left along the list of edges for the nearest that adds to another, past every tooth not yet done to the
            // list's head: 18 to 24 seconds. The walk now goes on past the stretch a long walk before it found none in.
            Path64 comb = Comb(40000, true);

            var watch = Stopwatch.StartNew();
            Paths64 offset = Clipper2.InflatePaths(new Paths64 { comb }, -1, JoinType.Miter, EndType.Polygon);
            watch.Stop();

            // 2 400 a tooth and 1 600 more, less the length round it, 416 a tooth and 808 more, and 4 back at the corners.
            Assert.Single(offset);
            Assert.Equal(1984.0 * 40000 + 796, Clipper2.Area(offset[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void ACombOf40000TeethPointingDownGrownBy1_IsOffsetInUnderThreeSeconds()
        {
            // Here the walk from each tooth's tip went past every tooth not yet done to the left side of the back, the nearest
            // edge that adds to an output path: 19 to 26 seconds.
            Path64 comb = Comb(40000, false);

            var watch = Stopwatch.StartNew();
            Paths64 offset = Clipper2.InflatePaths(new Paths64 { comb }, 1, JoinType.Miter, EndType.Polygon);
            watch.Stop();

            Assert.Single(offset);
            Assert.Equal(2816.0 * 40000 + 2412, Clipper2.Area(offset[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void TwentyThousandSquaresSideBySide_AreUnitedInUnderThreeSeconds()
        {
            // Every horizontal segment along the strip's top and bottom walked the whole run of points there to stretch over
            // it, only to find its left end claimed by the first: 17 seconds. The ends of a long run are filed now.
            var squares = new Paths64();

            for (int i = 0; i < 20000; i++)
            {
                squares.Add(
                    new Path64
                    {
                        new Point64(10 * i, 0),
                        new Point64(10 * i + 10, 0),
                        new Point64(10 * i + 10, 10),
                        new Point64(10 * i, 10),
                    }
                );
            }

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(squares, FillRule.NonZero);
            watch.Stop();

            Assert.Single(union);
            Assert.Equal(20000.0 * 100, Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void TwentyRowsOf8000SquaresSharingTheirSides_AreUnitedInUnderThreeSeconds()
        {
            // The path along the shared sides doubles back on itself, and the cleaning walked round it once for each point it
            // took off the end of such a spike: 23 to 25 seconds on .NET Framework, against 0.8 now. It reads only the points
            // whose neighbours have changed now.
            var squares = new Paths64();

            for (int j = 0; j < 20; j++)
            {
                for (int i = 0; i < 8000; i++)
                {
                    squares.Add(
                        new Path64
                        {
                            new Point64(10 * i, 10 * j),
                            new Point64(10 * i + 10, 10 * j),
                            new Point64(10 * i + 10, 10 * j + 10),
                            new Point64(10 * i, 10 * j + 10),
                        }
                    );
                }
            }

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(squares, FillRule.NonZero);
            watch.Stop();

            Assert.Single(union);
            Assert.Equal(160000.0 * 100, Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void FiftyThousandSquaresStandingOnAStrip_AreUnitedInUnderThreeSeconds()
        {
            // Each square's output path was joined into another as the sweep passed it, the paths so emptied making a chain
            // of all those joined before them, and each horizontal segment along the strip's top walked that chain to the
            // path that holds its points: 9.8 seconds on .NET 10 and 9.4 on .NET Framework, against 0.3 now. Each walk
            // points the paths it passes at the end of the chain now.
            var shapes = new Paths64
            {
                new Path64 { new Point64(0, 0), new Point64(1000000, 0), new Point64(1000000, 10), new Point64(0, 10) },
            };

            for (int i = 0; i < 50000; i++)
            {
                shapes.Add(
                    new Path64
                    {
                        new Point64(20 * i, 10),
                        new Point64(20 * i + 10, 10),
                        new Point64(20 * i + 10, 20),
                        new Point64(20 * i, 20),
                    }
                );
            }

            var watch = Stopwatch.StartNew();
            Paths64 union = Clipper2.Union(shapes, FillRule.NonZero);
            watch.Stop();

            // The strip 1 000 000 by 10 and 50 000 squares of 100.
            Assert.Single(union);
            Assert.Equal(15000000.0, Clipper2.Area(union[0]));
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void TwentyThousandTeethWithAHoleAndAnIslandEach_AreUnitedIntoAPolyTreeInUnderThreeSeconds()
        {
            // Each hole was looked for in the outer path by walking round its 80 000 points, and up a chain of the emptied
            // paths of the teeth joined before its own: 33 seconds on .NET 10 and 34 to 40 on .NET Framework, against 0.4 now.
            // Its points are found through the outer path's crossings with their lines now, and the chain is shortened as
            // it is walked.
            const int teeth = 20000;
            var shapes = new Paths64 { Rectangle(0, 0, 20 * teeth, 10) };

            for (int i = 0; i < teeth; i++)
            {
                Path64 hole = Rectangle(20 * i + 2, 20, 20 * i + 8, 30);
                hole.Reverse();
                shapes.Add(Rectangle(20 * i, 10, 20 * i + 10, 40));
                shapes.Add(hole);
                shapes.Add(Rectangle(20 * i + 4, 24, 20 * i + 6, 26));
            }

            var clipper = new Clipper64();
            clipper.AddSubject(shapes);
            var tree = new PolyTree64();
            var watch = Stopwatch.StartNew();
            Assert.True(clipper.Execute(ClipType.Union, FillRule.NonZero, tree));
            watch.Stop();

            // The strip 400 000 by 10 and the teeth 10 by 30, round 20 000 holes 6 by 10 with an island 2 by 2 in each.
            Assert.Single(tree);
            Assert.Equal(teeth * 500.0, Clipper2.Area(tree[0].Polygon));
            Assert.Equal(teeth, tree[0].Count);

            for (int i = 0; i < teeth; i++)
            {
                PolyPath64 hole = tree[0][i];
                Assert.True(hole.IsHole);
                Assert.Equal(-60.0, Clipper2.Area(hole.Polygon));
                Assert.Single(hole);
                Assert.Equal(4.0, Clipper2.Area(hole[0].Polygon));
                Assert.Empty(hole[0]);
            }

            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void HundredThousandSquaresSideBySide_AreUnitedIntoAPolyTreeInUnderThreeSeconds()
        {
            // Each square's output path was given the one to its left as its owner, and made sure first that it did not own
            // that one already, by walking up the chain of owners of all the squares to its left: 20 seconds on .NET 10 and
            // 18 to 27 on .NET Framework, against 0.5 to 0.7 now. A path just made owns none, and is not looked for now.
            var squares = new Paths64();

            for (int i = 0; i < 100000; i++)
            {
                squares.Add(Rectangle(10 * i, 0, 10 * i + 10, 10));
            }

            var clipper = new Clipper64();
            clipper.AddSubject(squares);
            var tree = new PolyTree64();
            var watch = Stopwatch.StartNew();
            Assert.True(clipper.Execute(ClipType.Union, FillRule.NonZero, tree));
            watch.Stop();

            Assert.Single(tree);
            Assert.Equal(100000.0 * 100, Clipper2.Area(tree[0].Polygon));
            Assert.Empty(tree[0]);
            Assert.True(watch.Elapsed.TotalSeconds < 3, watch.Elapsed.ToString());
        }

        [Fact]
        public void AXorOfTwentyThinRectangles_NestsItsTwoHolesInThePathAroundThem()
        {
            // What is left of case 94 of seed 1 of the tile fuzz, with every rectangle not needed taken out. The walk that
            // shortens the chains of owners stops at each emptied path that others split off, as the polytree reads the
            // owners of a path from those splits; walking past them too, both holes came out at the top of the tree, as
            // outer paths of their own.
            var subject = new Paths64
            {
                Rectangle(21, 0, 1610, 1),
                Rectangle(78, 1, 2608, 0),
                Rectangle(81, 0, 2120, 2),
                Rectangle(33, 2, 1157, 0),
                Rectangle(35, 35, 2394, 36),
                Rectangle(111, 0, 1527, 1),
                Rectangle(39, 0, 1387, 1),
                Rectangle(150, 50, 1320, 52),
            };
            var clip = new Paths64
            {
                Rectangle(0, 0, 1130, 2),
                Rectangle(24, 8, 1584, 10),
                Rectangle(36, 0, 2579, 3),
                Rectangle(34, 0, 1679, 2),
                Rectangle(23, 23, 1861, 24),
                Rectangle(62, 0, 2171, 2),
                Rectangle(34, 0, 1110, 2),
                Rectangle(108, 0, 2869, 1),
                Rectangle(114, 0, 1289, 3),
                Rectangle(82, 0, 1379, 1),
                Rectangle(129, 1, 1775, 0),
                Rectangle(153, 51, 1822, 53),
            };

            var clipper = new Clipper64 { PreserveCollinear = false };
            clipper.AddSubject(subject);
            clipper.AddClip(clip);
            var tree = new PolyTree64();
            Assert.True(clipper.Execute(ClipType.Xor, FillRule.EvenOdd, tree));

            // 14 outer paths at the top, one of them holding both holes, and every hole wound against the outer paths.
            Assert.Equal(14, tree.Count);
            int holes = 0;

            for (int i = 0; i < tree.Count; i++)
            {
                PolyPath64 outer = tree[i];
                Assert.False(outer.IsHole);
                Assert.True(Clipper2.IsPositive(outer.Polygon));

                for (int j = 0; j < outer.Count; j++)
                {
                    PolyPath64 hole = outer[j];
                    Assert.True(hole.IsHole);
                    Assert.False(Clipper2.IsPositive(hole.Polygon));
                    Assert.Empty(hole);
                    Assert.All(hole.Polygon, p =>
                        Assert.NotEqual(PointInPolygonResult.IsOutside, Clipper2.PointInPolygon(p, outer.Polygon))
                    );
                    holes++;
                }
            }

            Assert.Equal(2, holes);
        }

        [Fact]
        public void AXorOfACombAndTwoZigzags_LeavesThePieceBesideAToothAnOuterPathOfItsOwn()
        {
            // What is left of case 1273 of seed 1 of the clip fuzz. Points are found against a path of 64 points or more
            // through its crossings with their line, those near a point worked out exactly; judged by the doubles alone, the
            // corners of the piece on the side of a tooth were found inside the path round it, not on it, and the piece came
            // out as a hole of that path.
            var subject = new Paths64
            {
                Path(1364, -72, 1364, 0, 891, -144, 891, 0, 888, 0, 888, -72, 883, 0, 879, -144, 693, 0),
                Path(607, -49, 591, -9, 591, -49, 585, -49, 580, -9, 536, -9, 530, -49),
            };
            var clip = new Paths64
            {
                Path(
                    503,
                    0,
                    503,
                    -35,
                    506,
                    -35,
                    506,
                    0,
                    509,
                    0,
                    509,
                    -35,
                    510,
                    -35,
                    510,
                    0,
                    512,
                    0,
                    512,
                    -68,
                    515,
                    -68,
                    515,
                    0,
                    516,
                    0,
                    516,
                    -68,
                    519,
                    -68,
                    519,
                    0,
                    520,
                    0,
                    520,
                    -136,
                    522,
                    -136,
                    522,
                    0,
                    525,
                    0,
                    525,
                    -136,
                    527,
                    -136,
                    527,
                    0,
                    529,
                    0,
                    529,
                    -136,
                    533,
                    0,
                    536,
                    -35,
                    536,
                    0,
                    539,
                    0,
                    539,
                    -68,
                    580,
                    0,
                    580,
                    -68,
                    583,
                    0,
                    583,
                    -136,
                    591,
                    -136,
                    591,
                    0,
                    593,
                    0,
                    596,
                    -136,
                    596,
                    0,
                    599,
                    0,
                    600,
                    -68,
                    600,
                    0,
                    601,
                    0,
                    604,
                    -68,
                    604,
                    0,
                    606,
                    0,
                    606,
                    -35,
                    607,
                    -35,
                    607,
                    0,
                    610,
                    0,
                    610,
                    68
                ),
            };

            var clipper = new Clipper64 { PreserveCollinear = false };
            clipper.AddSubject(subject);
            clipper.AddClip(clip);
            var tree = new PolyTree64();
            Assert.True(clipper.Execute(ClipType.Xor, FillRule.EvenOdd, tree));

            // 16 outer paths side by side, the largest of 64 points, and no holes.
            Assert.Equal(16, tree.Count);
            int largest = 0;

            for (int i = 0; i < tree.Count; i++)
            {
                Assert.True(Clipper2.IsPositive(tree[i].Polygon));
                Assert.Empty(tree[i]);
                largest = System.Math.Max(largest, tree[i].Polygon.Count);
            }

            Assert.Equal(64, largest);
        }

        [Fact]
        public void AXorOf14ThinStrips_NestsTheHoleAlongTheEdgeOfThePathAroundIt()
        {
            // What is left of case 226 of seed 1 of the tile fuzz. The spans of a path of 64 points or more along a line, its
            // edges along it and its vertices on it, find a point on them on the path; without them, the hole's corners on
            // the path's edge along y 0 were judged by the crossings alone, and the hole came out at the top of the tree, as
            // an outer path of its own.
            var subject = new Paths64
            {
                Rectangle(0, 3, 1417, 0),
                Rectangle(6, 0, 1531, 3),
                Path(9, 3, 2722, 3, 9, 6),
                Rectangle(10, 0, 1067, 1),
                Rectangle(39, 1, 2473, 0),
                Rectangle(45, 0, 1525, 3),
                Rectangle(48, 0, 1628, 3),
                Rectangle(57, 1, 2888, 0),
                Rectangle(21, 0, 2364, 2),
                Rectangle(26, 0, 2096, 2),
                Rectangle(84, 1, 2524, 0),
                Rectangle(58, 2, 1677, 0),
                Rectangle(68, 0, 2576, 2),
            };
            var clip = new Paths64 { Rectangle(6, 0, 2684, 1) };

            var clipper = new Clipper64();
            clipper.AddSubject(subject);
            clipper.AddClip(clip);
            var tree = new PolyTree64();
            Assert.True(clipper.Execute(ClipType.Xor, FillRule.EvenOdd, tree));

            // 4 outer paths at the top, the one of 64 points holding the hole 51 by 1, wound against it and not outside it.
            Assert.Equal(4, tree.Count);
            int holes = 0;

            for (int i = 0; i < tree.Count; i++)
            {
                PolyPath64 outer = tree[i];
                Assert.False(outer.IsHole);
                Assert.True(Clipper2.IsPositive(outer.Polygon));

                for (int j = 0; j < outer.Count; j++)
                {
                    PolyPath64 hole = outer[j];
                    Assert.Equal(64, outer.Polygon.Count);
                    Assert.True(hole.IsHole);
                    Assert.Equal(-51.0, Clipper2.Area(hole.Polygon));
                    Assert.Empty(hole);
                    Assert.All(hole.Polygon, p =>
                        Assert.NotEqual(PointInPolygonResult.IsOutside, Clipper2.PointInPolygon(p, outer.Polygon))
                    );
                    holes++;
                }
            }

            Assert.Equal(1, holes);
        }

        /// <summary>
        /// The path through the points, given x and y in turn.
        /// </summary>
        private static Path64 Path(params long[] coordinates)
        {
            var path = new Path64();

            for (int i = 0; i < coordinates.Length; i += 2)
            {
                path.Add(new Point64(coordinates[i], coordinates[i + 1]));
            }

            return path;
        }

        /// <summary>
        /// The path round the rectangle from the first corner to the other, along x first.
        /// </summary>
        private static Path64 Rectangle(long x1, long y1, long x2, long y2) =>
            new Path64 { new Point64(x1, y1), new Point64(x2, y1), new Point64(x2, y2), new Point64(x1, y2) };

        /// <summary>
        /// A comb along X: teeth 4 wide and 200 long, 4 apart, on a back 200 deep, pointing down from it, or up when mirrored.
        /// </summary>
        private static Path64 Comb(int teeth, bool up)
        {
            var comb = new Path64 { new Point64(0, 0) };
            long x = 0;

            for (int i = 0; i < teeth; i++)
            {
                comb.Add(new Point64(x + 4, 0));
                comb.Add(new Point64(x + 4, 200));
                comb.Add(new Point64(x + 8, 200));
                comb.Add(new Point64(x + 8, 0));
                x += 8;
            }

            comb.Add(new Point64(x + 4, 0));
            comb.Add(new Point64(x + 4, 400));
            comb.Add(new Point64(0, 400));

            if (up)
            {
                var mirrored = new Path64();

                for (int i = comb.Count - 1; i >= 0; i--)
                {
                    mirrored.Add(new Point64(comb[i].X, -comb[i].Y));
                }

                comb = mirrored;
            }

            return comb;
        }
    }
}
