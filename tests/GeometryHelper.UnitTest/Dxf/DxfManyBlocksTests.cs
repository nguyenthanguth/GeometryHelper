using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Drawings of many blocks edited in time linear in them: another drawing of 10 000 blocks nested one inside the next
    /// inserted and copied without reading every record of the tables for each entity copied, 3 000 dimensions copied each
    /// with a block of its own and an arrow, 3 000 nested blocks built from the bottom up without walking down them for each
    /// INSERT appended, 3 000 blocks erased one by one without reading every record for each, and every vertex of a polyline
    /// of 10 000 read in the world without reading the whole record for each; what each gives, and refuses, as before. A
    /// drawing copied from is only read, so that several threads copy from it at once.
    /// </summary>
    public class DxfManyBlocksTests
    {
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

        [Fact]
        public void ADrawingOf10000NestedBlocks_IsInsertedAndCopiedInAboutASecond()
        {
            string text = Nest(10000).ToText();
            DxfDatabase source = DxfDatabase.Parse(text);
            DxfDatabase inserted = NewDrawing();
            DxfDatabase copied = NewDrawing();

            // .NET Framework took 11.5 s to insert and 6.5 s to copy, the layer of each entity copied found by reading every
            // record of the tables, 10 000 BLOCK_RECORDs among them; 1.1 s and 0.8 s now.
            var watch = Stopwatch.StartNew();
            inserted.Insert(GeoTransform3.Translation(new GeoVector3(100, 0, 0)), source, true);
            long insert = watch.ElapsedMilliseconds;
            watch.Restart();
            source.WblockCloneObjects(source.ModelSpace.ToList(), copied.ModelSpace);
            long copy = watch.ElapsedMilliseconds;

            Assert.True(insert < 6000, "A drawing of 10 000 nested blocks took " + insert + " ms to insert.");
            Assert.True(copy < 3000, "The INSERT of 10 000 nested blocks took " + copy + " ms to copy.");
            Assert.Equal(text, source.ToText());

            foreach (DxfDatabase db in new[] { inserted, copied })
            {
                DxfDatabase read = DxfDatabase.Parse(db.ToText());
                Assert.Equal("B9999", read.ModelSpace.OfType<DxfBlockReference>().Single().Name);
                Assert.Equal("B9998", read.BlockTable["B9999"].OfType<DxfBlockReference>().Single().Name);
                Assert.IsType<DxfLine>(read.BlockTable["B0"].Single());
            }

            Assert.Equal(new GeoPoint3(100, 0, 0), inserted.ModelSpace.OfType<DxfBlockReference>().Single().Position);
        }

        [Fact]
        public void ADrawingCopiedFromOnEightThreadsAtOnce_IsCopiedByEachAsItIsCopiedAlone()
        {
            // 60 layers, 40 blocks each inserting the one before, and 200 lines and INSERTs of model space: its tables read, then
            // inserted as it is, made a block and copied by WblockCloneObjects, by eight threads at once from one drawing as
            // opened. Each thread finds the same object for each record of a table, and copies what a copy made alone gives.
            DxfDatabase made = NewDrawing();

            for (int i = 0; i < 60; i++)
            {
                made.LayerTable.Add(new DxfLayerTableRecord { Name = "L" + i });
            }

            DxfBlockTableRecord last = null;

            for (int i = 0; i < 40; i++)
            {
                var block = new DxfBlockTableRecord { Name = "S" + i };
                made.BlockTable.Add(block);
                block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, i, 0)) { Layer = "L" + i });

                if (last != null)
                {
                    block.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), last));
                }

                last = block;
            }

            for (int i = 0; i < 200; i++)
            {
                made.ModelSpace.AppendEntity(i % 2 == 0
                    ? new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(i, 1, 0)) { Layer = "L" + (i % 60) }
                    : (DxfEntity)new DxfBlockReference(new GeoPoint3(i, 0, 0), made.BlockTable["S" + (i % 40)]));
            }

            string text = made.ToText();

            string Copy(DxfDatabase source, int way)
            {
                DxfDatabase into = NewDrawing();

                if (way == 0)
                {
                    into.Insert(GeoTransform3.Translation(new GeoVector3(1, 0, 0)), source, true);
                }
                else if (way == 1)
                {
                    into.Insert("OUTER", source, true);
                }
                else
                {
                    source.WblockCloneObjects(source.ModelSpace.ToList(), into.ModelSpace);
                }

                return into.ToText();
            }

            string[] alone = Enumerable.Range(0, 3).Select(way => Copy(DxfDatabase.Parse(text), way)).ToArray();
            int wrong = 0;
            int thrown = 0;
            string first = null;
            var layers = new DxfLayerTableRecord[8];

            for (int round = 0; round < 60; round++)
            {
                DxfDatabase shared = DxfDatabase.Parse(text);
                Array.Clear(layers, 0, layers.Length);

                using (var go = new System.Threading.ManualResetEventSlim(false))
                {
                    var threads = Enumerable.Range(0, 8).Select(t => new System.Threading.Thread(() =>
                    {
                        int way = (t + round) % 3;
                        go.Wait();

                        try
                        {
                            layers[t] = shared.LayerTable.Cast<DxfLayerTableRecord>().Last();

                            if (shared.LinetypeTable.Cast<DxfDBObject>().Count() + shared.TextStyleTable.Cast<DxfDBObject>().Count() == 0 || Copy(shared, way) != alone[way])
                            {
                                System.Threading.Interlocked.Increment(ref wrong);
                            }
                        }
                        catch (Exception e)
                        {
                            System.Threading.Interlocked.Increment(ref thrown);
                            System.Threading.Interlocked.CompareExchange(ref first, e.GetType().Name + ": " + e.Message, null);
                        }
                    })).ToList();

                    threads.ForEach(thread => thread.Start());
                    go.Set();
                    Assert.All(threads, thread => Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "A thread copying did not end."));
                }

                Assert.Equal(text, shared.ToText());
                Assert.All(layers, layer => Assert.Same(layers[0], layer));
            }

            Assert.True(wrong == 0 && thrown == 0, wrong + " copies came out otherwise and " + thrown + " threw, the first " + first);
        }

        [Fact]
        public void ThreeThousandDimensionsEachOfABlockOfItsOwn_AreCopiedInAFractionOfASecond()
        {
            DxfDatabase made = NewDrawing();
            var arrow = new DxfBlockTableRecord { Name = "GH_ARROW" };
            made.BlockTable.Add(arrow);
            arrow.AppendEntity(new DxfLine(new GeoPoint3(-1, -0.5, 0), new GeoPoint3(0, 0, 0)));
            DxfDimStyleTableRecord style = made.DimStyleTable.Cast<DxfDimStyleTableRecord>().First();

            for (int i = 0; i < 3000; i++)
            {
                var drawn = new DxfBlockTableRecord { Name = "GH_DRAWN" + i };
                made.BlockTable.Add(drawn);
                drawn.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
                var dimension = new DxfRotatedDimension(0, new GeoPoint3(i, 0, 0), new GeoPoint3(i + 1, 0, 0), new GeoPoint3(i, 2, 0), string.Empty, style) { DimBlock = drawn };
                made.ModelSpace.AppendEntity(dimension);
                dimension.Dimblk = arrow;
            }

            // Each dimension drawn by a block AutoCAD names itself, *D1 to *D3000, as it gives each dimension one.
            DxfDatabase source = DxfDatabase.Parse(made.ToText().Replace("\nGH_DRAWN", "\n*D"));
            DxfDatabase copied = NewDrawing();

            // .NET Framework took 8.6 s, the objects of the other drawing read again for each pointer, the blocks by name for
            // each arrow and the names *D1 on for each block copied; 0.3 s now.
            var watch = Stopwatch.StartNew();
            source.WblockCloneObjects(source.ModelSpace.ToList(), copied.ModelSpace);
            long copy = watch.ElapsedMilliseconds;

            Assert.True(copy < 1000, "3 000 dimensions took " + copy + " ms to copy.");
            DxfDatabase read = DxfDatabase.Parse(copied.ToText());
            DxfDimension[] dimensions = read.ModelSpace.OfType<DxfDimension>().ToArray();
            Assert.Equal(3000, dimensions.Length);
            Assert.All(dimensions, d => Assert.Equal("GH_ARROW", d.Dimblk.Name));
            Assert.Equal(3000, dimensions.Select(d => d.DimBlock.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal("*D1", dimensions[0].DimBlock.Name);
            Assert.Equal("*D3000", dimensions[2999].DimBlock.Name);
        }

        [Fact]
        public void ThreeThousandNestedBlocksBuiltFromTheBottomUp_AreBuiltInAFractionOfASecond_AndARoundIsStillRefused()
        {
            DxfDatabase db = NewDrawing();
            var blocks = new DxfBlockTableRecord[3000];

            // Each block added and given an INSERT of the one before it, as a nest is built from its bottom: .NET Framework
            // took 6.1 s, each INSERT appended walking down every block below it; 0.14 s now.
            var watch = Stopwatch.StartNew();

            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = new DxfBlockTableRecord { Name = "C" + i };
                db.BlockTable.Add(blocks[i]);

                if (i == 0)
                {
                    blocks[i].AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
                }
                else
                {
                    blocks[i].AppendEntity(new DxfBlockReference(new GeoPoint3(1, 0, 0), blocks[i - 1]));
                }
            }

            long built = watch.ElapsedMilliseconds;
            Assert.True(built < 1000, "3 000 nested blocks took " + built + " ms to build from the bottom up.");

            // The top inserted into the bottom, or a block into itself, would make a round, and is refused as before.
            string text = db.ToText();
            ArgumentException round = Assert.Throws<ArgumentException>(() => blocks[0].AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), blocks[2999])));
            Assert.StartsWith("The INSERT of C2999 would make the block C0 insert itself.", round.Message, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => blocks[5].AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), blocks[5])));
            Assert.Equal(text, db.ToText());
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), blocks[2999]));
            Assert.Equal("C2998", DxfDatabase.Parse(db.ToText()).BlockTable["C2999"].OfType<DxfBlockReference>().Single().Name);
        }

        [Fact]
        public void ThreeThousandBlocksErasedOneByOne_AreErasedInAFractionOfASecond_AndABlockInUseIsStillKept()
        {
            DxfDatabase db = DxfDatabase.Parse(Nest(3000).ToText());

            // B0 is inserted by B1, and B2999 by model space: neither is erased.
            Assert.Throws<InvalidOperationException>(() => db.BlockTable["B0"].Erase());
            Assert.Throws<InvalidOperationException>(() => db.BlockTable["B2999"].Erase());
            db.ModelSpace.Single().Erase();

            // .NET Framework took 26 s, each erasing reading every record for an INSERT of the block, an arrow and a pointer to
            // it, and the blocks by name made again for each lookup; 0.15 s now.
            var watch = Stopwatch.StartNew();

            for (int i = 2999; i >= 0; i--)
            {
                db.BlockTable["B" + i].Erase();
            }

            long erased = watch.ElapsedMilliseconds;
            Assert.True(erased < 1000, "3 000 blocks took " + erased + " ms to erase one by one.");
            Assert.False(db.BlockTable.Has("B0"));
            Assert.DoesNotContain(db.BlockTable, b => !b.IsLayout);
            Assert.DoesNotContain("\nB1500\n", db.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void EveryVertexOfAPolylineOf10000_IsReadInTheWorldInAFractionOfASecond_AsOneByOne()
        {
            DxfDatabase made = NewDrawing();
            var polyline = new DxfPolyline();

            for (int i = 0; i < 10000; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(i * 0.25, (i % 17) * 0.5), i % 7 == 0 ? 0.25 : 0.0, 0, 0);
            }

            polyline.Normal = new GeoVector3(0.0, 0.6, 0.8);
            polyline.Elevation = 2.5;
            made.ModelSpace.AppendEntity(polyline);
            string text = made.ToText();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolyline read = db.ModelSpace.OfType<DxfPolyline>().Single();

            // .NET Framework took 36 s, the normal and the elevation read from the whole record for each vertex; 13 ms now.
            var watch = Stopwatch.StartNew();
            var points = new GeoPoint3[10000];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = read.GetPoint3dAt(i);
            }

            long took = watch.ElapsedMilliseconds;
            Assert.True(took < 1000, "10 000 vertices took " + took + " ms to read in the world.");

            // Each as a polyline read afresh gives it, to the bit.
            foreach (int i in new[] { 0, 1, 4999, 9999 })
            {
                Assert.Equal(DxfModel.Bits(DxfDatabase.Parse(text).ModelSpace.OfType<DxfPolyline>().Single().GetPoint3dAt(i)), DxfModel.Bits(points[i]));
            }

            // Raised and one vertex moved, the polyline is read as it stands: (1, 1) at 4 on the normal (0, 0.6, 0.8), whose
            // plane's x runs along (-1, 0, 0) and y along (0, -0.8, 0.6), is (-1, 1.6, 3.8) in the world, and every other
            // vertex 1.5 further along the normal.
            read.Elevation = 4.0;
            read.SetPointAt(3, new GeoPoint2(1, 1));
            Assert.True(read.GetPoint3dAt(3).IsEqualTo(new GeoPoint3(-1, 1.6, 3.8), Tolerance.Default), read.GetPoint3dAt(3).ToString());
            Assert.True(read.GetPoint3dAt(9999).IsEqualTo(points[9999] + new GeoVector3(0.0, 0.6, 0.8) * 1.5, Tolerance.Default), read.GetPoint3dAt(9999).ToString());
            DxfPolyline saved = DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfPolyline>().Single();
            Assert.Equal(DxfModel.Bits(saved.GetPoint3dAt(3)), DxfModel.Bits(read.GetPoint3dAt(3)));
            Assert.Equal(DxfModel.Bits(saved.GetPoint3dAt(9999)), DxfModel.Bits(read.GetPoint3dAt(9999)));
        }
    }
}
