using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Blocks nested deep, 3 000 and 6 000 one inside the next: the walks down them (an INSERT appended, moved into a block or
    /// given another block, the box of an INSERT moved, the size of what an INSERT places as the drawing is saved, another
    /// drawing of them inserted) each on a stack of its own, so that none overflows the thread's, which they did from 3 000
    /// on .NET 10 and from 4 500 on .NET Framework; another drawing of 3 000 of them inserted in a second, not in 47; a round
    /// of INSERTs still refused, and an INSERT of an erased block too; blocks looked up by name from several threads at once;
    /// and the arrows the overrides of a dimension of R12 name by name renamed with their block. ezdxf 1.4.5 read the probe's
    /// drawings back, its recover and audit finding nothing.
    /// </summary>
    public class DxfNestedBlocksTests
    {
        private const int Depth = 3000;

        // Deep enough to overflow a thread of 1 MB on .NET Framework 4.8, had the walks used its stack: from 4 500 they did.
        private const int Deep = 6000;

        private static DxfDatabase NewDrawing() => DxfDatabase.Parse(new DxfWriter().ToString());

        /// <summary>
        /// B0 holds a line from (0, 0, 0) to (10, 0, 0); each block after it an INSERT of the one before at (1, 0, 0); model
        /// space an INSERT of the last at the origin. Filled from the last block down, so that no INSERT appended is walked
        /// deeper than one block.
        /// </summary>
        private static DxfDatabase Nest(int depth)
        {
            DxfDatabase db = NewDrawing();
            var blocks = new DxfBlockTableRecord[depth];

            for (int i = 0; i < depth; i++)
            {
                blocks[i] = new DxfBlockTableRecord { Name = "B" + i };
                db.BlockTable.Add(blocks[i]);
            }

            blocks[0].AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));

            for (int i = depth - 1; i > 0; i--)
            {
                blocks[i].AppendEntity(new DxfBlockReference(new GeoPoint3(1, 0, 0), blocks[i - 1]));
            }

            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), blocks[depth - 1]));
            return db;
        }

        /// <summary>
        /// Runs on a thread of 1 MB of stack, the default of .NET Framework's threads, whatever stack the runner's own threads
        /// have; what it throws is thrown here.
        /// </summary>
        private static void OnAThreadOf1MB(Action body)
        {
            Exception thrown = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    body();
                }
                catch (Exception e)
                {
                    thrown = e;
                }
            }, 1024 * 1024);

            thread.IsBackground = true;
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromMinutes(2)), "The walks of 6 000 nested blocks did not end in 2 minutes.");

            if (thrown != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(thrown).Throw();
            }
        }

        [Fact]
        public void BlocksNested6000Deep_AreWalkedToTheBottom_AndARoundThroughThemIsStillRefused() => OnAThreadOf1MB(() =>
        {
            DxfDatabase db = Nest(Deep);
            DxfBlockTableRecord bottom = db.BlockTable["B0"];
            DxfBlockTableRecord top = db.BlockTable["B" + (Deep - 1)];
            DxfBlockReference placed = db.ModelSpace.OfType<DxfBlockReference>().Single();
            string text = db.ToText();

            // An INSERT of the top block appended to the bottom one, the model space INSERT moved into it, and an INSERT of it
            // given the top block: each would make B0 insert itself through 6 000 blocks, and each is refused.
            Assert.Throws<ArgumentException>(() => bottom.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), top)));
            Assert.Throws<ArgumentException>(() => bottom.AssumeOwnershipOf(new DxfEntity[] { placed }));
            Assert.Equal(text, db.ToText());
            var leaf = new DxfBlockTableRecord { Name = "LEAF" };
            db.BlockTable.Add(leaf);
            var turned = new DxfBlockReference(new GeoPoint3(0, 0, 0), leaf);
            bottom.AppendEntity(turned);
            Assert.Throws<ArgumentException>(() => turned.BlockTableRecord = top);
            Assert.Same(leaf, turned.BlockTableRecord);
            turned.Erase();

            // A block of its own takes the model space INSERT, whose 6 000 blocks insert none of it.
            var keep = new DxfBlockTableRecord { Name = "KEEP" };
            db.BlockTable.Add(keep);
            keep.AssumeOwnershipOf(new DxfEntity[] { placed });
            Assert.Same(keep, placed.Owner);
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), keep));

            // Each block puts the one it inserts 1 further along x: B0's line comes 5 999 along, moved 10 up.
            GeoAabb3 box = db.ModelSpace.OfType<DxfBlockReference>().Single().GeometryExtentsBestFit(GeoTransform3.Translation(new GeoVector3(0, 10, 0)));
            Assert.True(box.Min.IsEqualTo(new GeoPoint3(5999, 10, 0), Tolerance.Default), box.ToString());
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(6009, 10, 0), Tolerance.Default), box.ToString());

            // Saved, the INSERT's size is worked out down the 6 000 blocks; the reader places 64 of them, as it says.
            string saved = db.ToText();
            Assert.Equal(Deep, DxfDatabase.Parse(saved).BlockTable.Count(b => b.Name.StartsWith("B", StringComparison.Ordinal)));
            Assert.Contains("1 INSERT nested more than 64 deep was not placed.", DxfReader.Parse(saved, new DxfReadOptions { Tolerance = Tolerance.Default }).Warnings);
        });

        [Fact]
        public void ADrawingOf3000BlocksNestedOneInsideTheNext_IsInsertedInSeconds()
        {
            DxfDatabase source = Nest(Depth);
            string sourceText = source.ToText();
            DxfDatabase db = NewDrawing();
            DxfDatabase plate = NewDrawing();

            // .NET Framework took 47 s for each, each INSERT copied walked down every block below it, twice, and the check of
            // each copy walked them again; 1.0 s now.
            var watch = Stopwatch.StartNew();
            db.Insert(GeoTransform3.Translation(new GeoVector3(100, 0, 0)), source, true);
            long moved = watch.ElapsedMilliseconds;
            watch.Restart();
            plate.Insert("PLATE", source, true);
            long made = watch.ElapsedMilliseconds;

            Assert.True(moved < 10000, "A drawing of 3 000 nested blocks took " + moved + " ms to insert.");
            Assert.True(made < 10000, "A drawing of 3 000 nested blocks took " + made + " ms to make a block of.");
            Assert.Equal(sourceText, source.ToText());

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new GeoPoint3(100, 0, 0), read.ModelSpace.OfType<DxfBlockReference>().Single().Position);
            Assert.Equal("B" + (Depth - 1), read.ModelSpace.OfType<DxfBlockReference>().Single().Name);
            Assert.Equal("B" + (Depth - 2), read.BlockTable["B" + (Depth - 1)].OfType<DxfBlockReference>().Single().Name);
            Assert.IsType<DxfLine>(read.BlockTable["B0"].Single());
            Assert.Equal("B" + (Depth - 1), DxfDatabase.Parse(plate.ToText()).BlockTable["PLATE"].OfType<DxfBlockReference>().Single().Name);
        }

        [Fact]
        public void ADrawingWhoseBlocksWouldComeToInsertThemselvesThroughAnInsertOfABlockThisOneHasNot_IsStillRefused()
        {
            // This drawing's P holds an INSERT of X, a block it has not; the other's X inserts P. Merged, X would insert itself
            // through this P, as before.
            const string Head = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  0\nENDSEC\n  0\nSECTION\n  2\nBLOCKS\n";
            const string Line = "  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 30\n0\n 11\n1\n 21\n0\n 31\n0\n";
            string Insert(string name) => "  0\nINSERT\n  8\n0\n  2\n" + name + "\n 10\n0\n 20\n0\n 30\n0\n";
            string Block(string name, string entities) => "  0\nBLOCK\n  8\n0\n  2\n" + name + "\n 70\n0\n 10\n0\n 20\n0\n 30\n0\n" + entities + "  0\nENDBLK\n  8\n0\n";
            string Drawing(string blocks, string entities) => Head + blocks + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n" + entities + "  0\nENDSEC\n  0\nEOF\n";
            string source = Drawing(Block("P", Line) + Block("X", Insert("P")), Insert("X"));

            DxfDatabase db = DxfDatabase.Parse(Drawing(Block("P", Line + Insert("X")), string.Empty));
            string before = db.ToText();
            ArgumentException refused = Assert.Throws<ArgumentException>(() => db.Insert(GeoTransform3.Identity, DxfDatabase.Parse(source), true));
            Assert.Contains("The block X the copies bring would insert itself", refused.Message, StringComparison.Ordinal);

            // Refused before anything is written: X does not come, empty, as it came before it was refused.
            Assert.False(db.BlockTable.Has("X"));
            Assert.Equal(before, db.ToText());

            // Without the INSERT of X, the same drawing comes whole.
            DxfDatabase clean = DxfDatabase.Parse(Drawing(Block("P", Line + Line), string.Empty));
            clean.Insert(GeoTransform3.Identity, DxfDatabase.Parse(source), true);
            Assert.Equal("P", clean.BlockTable["X"].OfType<DxfBlockReference>().Single().Name);
            Assert.Equal("X", clean.ModelSpace.OfType<DxfBlockReference>().Single().Name);
        }

        [Fact]
        public void AnInsertOfAnErasedBlock_IsRefused_AsOneOfABlockTheDrawingHasNot()
        {
            // M erased, an INSERT made of it before is no INSERT of the drawing's: appended or given it, it is refused, as one of
            // a block the drawing has not. Appended, it would have stood for the M another drawing's merge brings next, which
            // inserts P, so that M would insert itself through P unseen.
            DxfDatabase db = NewDrawing();
            var m = new DxfBlockTableRecord { Name = "M" };
            db.BlockTable.Add(m);
            m.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            var p = new DxfBlockTableRecord { Name = "P" };
            db.BlockTable.Add(p);
            p.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            var made = new DxfBlockReference(new GeoPoint3(0, 0, 0), m);
            m.Erase();

            ArgumentException refused = Assert.Throws<ArgumentException>(() => p.AppendEntity(made));
            Assert.Contains("has no block M", refused.Message, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), m)));
            var placed = new DxfBlockReference(new GeoPoint3(0, 0, 0), p);
            db.ModelSpace.AppendEntity(placed);
            Assert.Throws<ArgumentException>(() => placed.BlockTableRecord = m);
            Assert.Same(p, placed.BlockTableRecord);
            placed.Erase();
            Assert.False(made.IsResident);
            Assert.DoesNotContain(db.ModelSpace, e => e is DxfBlockReference);

            // The other drawing's M, inserting P, comes with no round, as P holds no INSERT.
            DxfDatabase other = NewDrawing();
            var otherP = new DxfBlockTableRecord { Name = "P" };
            other.BlockTable.Add(otherP);
            otherP.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            var otherM = new DxfBlockTableRecord { Name = "M" };
            other.BlockTable.Add(otherM);
            otherM.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), otherP));
            db.Insert(GeoTransform3.Identity, other, true);
            Assert.Equal("P", db.BlockTable["M"].OfType<DxfBlockReference>().Single().Name);
            Assert.Empty(db.BlockTable["P"].OfType<DxfBlockReference>());
        }

        [Fact]
        public void BlocksOfADrawing_AreFoundByNameFromFourThreadsAtOnce()
        {
            // The blocks by name are made at the first lookup, which four threads ask for together, again and again: each finds
            // every block it asks for, and none throws.
            DxfDatabase source = NewDrawing();

            for (int i = 0; i < 3000; i++)
            {
                source.BlockTable.Add(new DxfBlockTableRecord { Name = "K" + i });
            }

            string text = source.ToText();
            int misses = 0;
            int throws = 0;
            var watch = Stopwatch.StartNew();

            for (int round = 0; round < 40; round++)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                Assert.True(db.BlockTable.Count() > 3000);

                using (var go = new System.Threading.ManualResetEventSlim(false))
                {
                    var threads = new List<System.Threading.Thread>();

                    for (int t = 0; t < 4; t++)
                    {
                        int seed = t;
                        var thread = new System.Threading.Thread(() =>
                        {
                            go.Wait();

                            try
                            {
                                for (int k = 0; k < 50; k++)
                                {
                                    if (!db.BlockTable.Has("K" + (((2999 - k * 37 * (seed + 1)) % 3000) + 3000) % 3000))
                                    {
                                        System.Threading.Interlocked.Increment(ref misses);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                System.Threading.Interlocked.Increment(ref throws);
                            }
                        });
                        thread.Start();
                        threads.Add(thread);
                    }

                    go.Set();

                    foreach (System.Threading.Thread thread in threads)
                    {
                        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A thread looking blocks up did not end.");
                    }
                }
            }

            Assert.Equal(0, misses);
            Assert.Equal(0, throws);
            Assert.True(watch.ElapsedMilliseconds < 30000, "40 rounds of lookups took " + watch.ElapsedMilliseconds + " ms.");
        }

        [Fact]
        public void ADrawingOf3000NestedBlocksTheBottomOfWhichADrawingOfR12CannotHold_IsRefusedWhole()
        {
            DxfDatabase source = Nest(Depth);
            source.BlockTable["B0"].AppendEntity(new DxfSpline(3, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0), new GeoPoint3(2, 0, 0), new GeoPoint3(3, 1, 0) },
                new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0 }, new double[0], 0.0, 0.0));
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            string text = r12.ToText();

            // B0 is checked once, not once for each of the 2 999 blocks above it, and its SPLINE refuses the whole.
            Assert.Throws<ArgumentException>(() => r12.Insert(GeoTransform3.Identity, source, true));
            Assert.Equal(text, r12.ToText());
        }

        [Fact]
        public void ABlockRenamedInADrawingOfR12_TakesAlongTheArrowsTheOverridesOfADimensionNameByName()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R12);
            var arrows = new List<DxfBlockTableRecord>();

            foreach (string name in new[] { "GH_DOT", "GH_TICK", "GH_OPEN" })
            {
                var arrow = new DxfBlockTableRecord { Name = name };
                db.BlockTable.Add(arrow);
                arrow.AppendEntity(new DxfLine(new GeoPoint3(-1, -0.5, 0), new GeoPoint3(0, 0, 0)));
                arrows.Add(arrow);
            }

            DxfDimension dimension = db.ModelSpace.OfType<DxfDimension>().First();
            dimension.Dimblk = arrows[0];
            dimension.Dimblk1 = arrows[1];
            dimension.Dimblk2 = arrows[2];
            string text = db.ToText();

            foreach (DxfBlockTableRecord arrow in arrows)
            {
                arrow.Name = arrow.Name + "_R12";
            }

            // DIMBLK, DIMBLK1 and DIMBLK2 are 1070 5, 6 and 7 of ACAD's DSTYLE, each then 1000 the block's name.
            string saved = db.ToText();
            string record = Records(saved).Single(r => HandleOf(r) == dimension.Handle);
            Assert.Contains("1070\n5\n1000\nGH_DOT_R12\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n6\n1000\nGH_TICK_R12\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n7\n1000\nGH_OPEN_R12\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\nGH_DOT\n", saved, StringComparison.Ordinal);
            Assert.DoesNotContain("\nGH_TICK\n", saved, StringComparison.Ordinal);
            Assert.DoesNotContain("\nGH_OPEN\n", saved, StringComparison.Ordinal);

            // Only the dimension and the three blocks are written anew.
            Differ(text, saved, out List<string> gone, out List<string> come);
            Assert.Equal(4, come.Count(r => !r.Contains("\nHEADER\n")));
            Assert.Equal(4, gone.Count(r => !r.Contains("\nHEADER\n")));
            Assert.All(come.Where(r => !r.Contains("\nHEADER\n")), r => Assert.Contains("_R12\n", r, StringComparison.Ordinal));

            DxfDimension read = DxfDatabase.Parse(saved).ModelSpace.OfType<DxfDimension>().First(d => d.Handle == dimension.Handle);
            Assert.Equal(new[] { "GH_DOT_R12", "GH_TICK_R12", "GH_OPEN_R12" }, new[] { read.Dimblk.Name, read.Dimblk1.Name, read.Dimblk2.Name });
            Assert.Throws<InvalidOperationException>(() => arrows[0].Erase());

            // A drawing of R2000 names the arrows by their handles (1005), which renaming leaves as they are.
            DxfDatabase later = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            var dot = new DxfBlockTableRecord { Name = "GH_DOT" };
            later.BlockTable.Add(dot);
            dot.AppendEntity(new DxfLine(new GeoPoint3(-1, -0.5, 0), new GeoPoint3(0, 0, 0)));
            DxfDimension dimensioned = later.ModelSpace.OfType<DxfDimension>().First();
            dimensioned.Dimblk = dot;
            string before = Records(later.ToText()).Single(r => HandleOf(r) == dimensioned.Handle);

            dot.Name = "GH_DOT_R2000";

            Assert.Equal(before, Records(later.ToText()).Single(r => HandleOf(r) == dimensioned.Handle));
            Assert.Equal("GH_DOT_R2000", dimensioned.Dimblk.Name);
        }
    }
}
