using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of speed13 in the core: the object of an entity of a file that was read, the object of a polyline made for it,
    /// and the lists a closed polyline of four vertices is read through. The size of each is held by the bytes allocated, which
    /// do not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed15Tests
    {
        private static DxfFile FileOf(string entities) => DxfFile.Parse(R2000(entities));

        [Fact]
        public void TheObjectOfAnEntityRead_TakesNinetySixBytes()
        {
            // A million entities of a drawing hold a million of these: three flags share the word of the count, and the object is
            // 96 bytes where the three bytes, the two integers and the nine references took 104.
            const int count = 20_000;
            var items = new DxfItem[count];

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < count; i++)
            {
                items[i] = new DxfItem(null, i, 1, "LINE", "*Model_Space");
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(count, items.Length);
            Assert.True(used / count <= 96, "allocated " + used / count + " bytes an item");
        }

        [Fact]
        public void TheFlagsOfAnEntityRead_AreKeptApart_AndSetByTheirOwnName()
        {
            var item = new DxfItem(null, 12, 3, "INSERT", "*Model_Space");

            Assert.Equal(3, item.Count);
            Assert.False(item.IsRemoved);
            Assert.False(item.IsPaper);

            item.IsPaper = true;
            Assert.True(item.IsPaper);
            Assert.False(item.IsRemoved);
            Assert.Equal(3, item.Count);

            item.IsRemoved = true;
            Assert.True(item.IsRemoved);
            Assert.True(item.IsPaper);
            Assert.Equal(3, item.Count);

            item.IsPaper = false;
            Assert.False(item.IsPaper);
            Assert.True(item.IsRemoved);

            item.IsRemoved = false;
            Assert.False(item.IsRemoved);
            Assert.Equal(3, item.Count);
        }

        [Fact]
        public void TheFlagsOfAnEntityRead_SetFromManyThreads_AreNotLost()
        {
            // Each flag is a bit of one word, set by a compare and swap: two threads setting two flags leave both set.
            for (int round = 0; round < 200; round++)
            {
                var item = new DxfItem(null, 0, 5, "LINE", null);
                var threads = new[]
                {
                    new System.Threading.Thread(() => item.IsPaper = true),
                    new System.Threading.Thread(() => item.IsRemoved = true),
                };

                foreach (System.Threading.Thread thread in threads)
                {
                    thread.Start();
                }

                foreach (System.Threading.Thread thread in threads)
                {
                    thread.Join();
                }

                Assert.True(item.IsPaper && item.IsRemoved, "round " + round);
                Assert.Equal(5, item.Count);
            }
        }

        [Fact]
        public void TheObjectOfAPolylineShownAndNeverReadByItsVertices_TakesNoMoreThanSixtyFourBytes()
        {
            // The vertices it was last read by, and the normal it was last placed by, are one object made when they are first asked
            // for: a polyline shown holds one reference where its eight fields took 56 bytes, 200 000 in a drawing of 1 000 000 (48 bytes kept by the object and its slot where 95).
            const int count = 20_000;
            var items = new DxfItem[count];
            var made = new DxfEntity[count];

            for (int i = 0; i < count; i++)
            {
                items[i] = new DxfItem(null, i, 1, "LWPOLYLINE", null);
            }

            long before = GC.GetTotalMemory(true);

            for (int i = 0; i < count; i++)
            {
                made[i] = DxfEntity.Of(items[i]);
            }

            long kept = GC.GetTotalMemory(true) - before;

            Assert.IsType<DxfPolyline>(made[0]);
            Assert.True(kept / count <= 64, "kept " + kept / count + " bytes a polyline");
            GC.KeepAlive(items);
            GC.KeepAlive(made);
        }

        [Fact]
        public void AClosedPolylineOfFourVerticesWithABulge_IsReadFromItsBytes_ThroughFewLists()
        {
            // Read from its bytes, the vertices are kept as they are where none lies on the one before: the lists the reader made
            // of them are the shape's own to change, not copied twice. About 1 370 bytes a read where 1 040 are allocated.
            string text = G(
                0,
                "LWPOLYLINE",
                5,
                "A1",
                8,
                "0",
                100,
                "AcDbPolyline",
                90,
                "4",
                70,
                "1",
                10,
                "0",
                20,
                "0",
                10,
                "20",
                20,
                "0",
                42,
                "0.5",
                10,
                "20",
                20,
                "20",
                10,
                "0",
                20,
                "20"
            );
            DxfFile file = FileOf(text);
            DxfItem item = file.ModelSpace.Single();

            Assert.True(file.TryFastShape(item, out object first));
            Assert.NotNull(first);

            const int reads = 2_000;
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < reads; i++)
            {
                file.TryFastShape(item, out _);
            }

            long used = (GC.GetAllocatedBytesForCurrentThread() - before) / reads;

            Assert.True(used <= 1_150, "allocated " + used + " bytes a read");
        }
    }
}
