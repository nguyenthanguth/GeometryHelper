using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of speed20 in the core: the entities of a DWG written as DXF on several threads, each run of them into a
    /// buffer of its own and the buffers put in order, and the list of blocks made with a set where it was made with a list
    /// that a drawing of thousands of blocks walked 200 million times. The DXF is held to what one thread gave, the speed of
    /// the blocks to how the time grows with their number, which does not depend on the machine as a time does.
    /// </summary>
    public class DxfSpeed30Tests
    {
        private static byte[] WithThreads(int threads, Func<byte[]> make)
        {
            int before = DwgTranscoder.DecodeThreads;
            DwgTranscoder.DecodeThreads = threads;

            try
            {
                return make();
            }
            finally
            {
                DwgTranscoder.DecodeThreads = before;
            }
        }

        private static DxfDatabase Empty() => DxfDatabase.Parse(new DxfWriter().ToString());

        /// <summary>
        /// A DWG of that many sets of entities in model space and paper space, among them the kinds that own others: a
        /// polyline with vertices and its SEQEND, an INSERT with an attribute and its SEQEND, and an INSERT of a block of
        /// two entities.
        /// </summary>
        private static byte[] OneDwgOfOwners(int count)
        {
            DxfDatabase db = Empty();
            var block = new DxfBlockTableRecord { Name = "PAIR" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(5.5, 0, 0)));
            block.AppendEntity(new DxfCircle(new GeoPoint3(2, 2, 0), new GeoVector3(0, 0, 1), 1.25));

            for (int i = 0; i < count; i++)
            {
                double x = i % 50 * 40.0 + 0.125;
                double y = i / 50 * 40.0 + 0.0625;
                DxfBlockTableRecord space = i % 5 == 4 ? db.PaperSpace : db.ModelSpace;

                space.AppendEntity(new DxfLine(new GeoPoint3(x, y, 0), new GeoPoint3(x + 20.3, y + 12.7, 0)));
                space.AppendEntity(
                    new DxfPolyline3d(
                        DxfPoly3dType.SimplePoly,
                        new[]
                        {
                            new GeoPoint3(x, y, 1),
                            new GeoPoint3(x + 10, y + 5.5, 2),
                            new GeoPoint3(x + 20, y, 3),
                        },
                        false
                    )
                );

                var insert = new DxfBlockReference(new GeoPoint3(x, y + 25, 0), block);
                space.AppendEntity(insert);
                insert.AttributeCollection.AppendAttribute(
                    new DxfAttributeReference(
                        new GeoPoint3(x, y + 30, 0),
                        "V" + i.ToString(CultureInfo.InvariantCulture),
                        "TAG",
                        null
                    )
                );
            }

            return DwgWriter.ToDwg(db.ToText(), DwgWriter.DefaultVersion);
        }

        private static int Count(byte[] dxf, string record)
        {
            string text = Encoding.ASCII.GetString(dxf);
            int count = 0;
            int at = 0;

            while ((at = text.IndexOf("\r\n0\r\n" + record + "\r\n", at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += record.Length;
            }

            return count;
        }

        [Fact]
        public void ADwgOfOwners_WrittenOnOneTwoThreeFourAndEightThreads_IsTheSameDxfByteForByte()
        {
            byte[] file = OneDwgOfOwners(1_500);
            byte[] one = WithThreads(1, () => DwgReader.ToDxf(file));

            // The polylines' vertices and the inserts' attributes, each owner closed by a SEQEND, are in it, in both spaces.
            Assert.Equal(1_500, Count(one, "POLYLINE"));
            Assert.Equal(1_500, Count(one, "ATTRIB"));
            Assert.Equal(3_000, Count(one, "SEQEND"));
            Assert.Equal(4_500, Count(one, "VERTEX"));
            Assert.Contains("\r\n67\r\n1\r\n", Encoding.ASCII.GetString(one), StringComparison.Ordinal);

            foreach (int threads in new[] { 2, 3, 4, 8, 0 })
            {
                Assert.Equal(one, WithThreads(threads, () => DwgReader.ToDxf(file)));
            }
        }

        [Fact]
        public void EveryDwgOfTheTests_WrittenOnEightThreads_IsTheSameDxfAsOneThreadWrote()
        {
            // The drawings of AutoCAD in the tests, of every version from R2000, with their layouts, blocks, polylines,
            // inserts and attributes: few of the entities, so that they are written on eight threads by being asked to.
            string[] names = typeof(DwgAnswerKey)
                .Assembly.GetManifestResourceNames()
                .Where(n => n.Contains(".Dwg.Data.") && n.EndsWith(".dwg.gz", StringComparison.Ordinal))
                .Select(n =>
                {
                    string file = n.Substring(n.LastIndexOf(".Dwg.Data.", StringComparison.Ordinal) + 10);
                    return file.Substring(0, file.Length - 7);
                })
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.True(names.Length >= 60, "only " + names.Length + " drawings");

            foreach (string name in names)
            {
                byte[] file = DwgAnswerKey.Dwg(name);
                byte[] one = WithThreads(1, () => DwgReader.ToDxf(file));
                byte[] eight = WithThreads(8, () => DwgReader.ToDxf(file));

                Assert.True(one.SequenceEqual(eight), "the DXF of " + name + " differs on eight threads");
            }
        }

        private sealed class SynchronousProgress : IProgress<double>
        {
            private readonly Action<double> _told;

            internal SynchronousProgress(Action<double> told) => _told = told;

            public void Report(double value) => _told(value);
        }

        [Fact]
        public void ATokenStoppedWhileTheEntitiesAreWrittenOnEightThreads_EndsTheReadingWithAnOperationCanceledException_AndTheNextReadingIsWhole()
        {
            byte[] file = OneDwgOfOwners(3_000);
            byte[] whole = WithThreads(8, () => DwgReader.ToDxf(file));
            var source = new CancellationTokenSource();

            // Stopped from the share told: the objects are read to 0.6 and the DXF is written from there to 1.
            var steps = ProgressSteps.Of(
                new SynchronousProgress(share =>
                {
                    if (share > 0.8)
                    {
                        source.Cancel();
                    }
                })
            );

            Assert.Throws<OperationCanceledException>(() =>
                WithThreads(8, () => DwgReader.ToDxf(file, null, steps, source.Token))
            );

            // Nothing is left in a state the next reading would meet: its DXF is what it was.
            Assert.Equal(whole, WithThreads(8, () => DwgReader.ToDxf(file)));
        }

        [Fact]
        public void TwoWritersOfDxf_OneAppendedToTheOther_GiveTheBytesOfBothInOrder_AndACleared_IsWrittenIntoAgain()
        {
            var first = new DwgDxfWriter(1 << 12);
            var second = new DwgDxfWriter(1 << 12);
            var both = new DwgDxfWriter(1 << 12);

            first.Write(0, "LINE");
            first.Write(10, 1.5);
            second.WriteHandle(5, 0xABC);
            second.Write(8, "L0");

            both.Write(0, "LINE");
            both.Write(10, 1.5);
            both.WriteHandle(5, 0xABC);
            both.Write(8, "L0");

            first.Append(second);

            Assert.Equal(both.ToArray(), first.ToArray());

            second.Clear();
            second.Write(8, "L1");

            Assert.Equal("8\r\nL1\r\n", Encoding.ASCII.GetString(second.ToArray()));

            // A part longer than the room left makes the room, as a write does.
            var small = new DwgDxfWriter(1);
            var long1 = new DwgDxfWriter(1 << 16);

            for (int i = 0; i < 3_000; i++)
            {
                long1.Write(10, i + 0.25);
            }

            small.Append(long1);
            small.Append(long1);

            Assert.Equal(long1.ToArray().Length * 2, small.ToArray().Length);
        }

        /// <summary>A DWG of that many blocks, each of a line and each inserted once in model space.</summary>
        private static byte[] OneDwgOfBlocks(int count)
        {
            DxfDatabase db = Empty();

            for (int i = 0; i < count; i++)
            {
                var block = new DxfBlockTableRecord { Name = "B" + i.ToString(CultureInfo.InvariantCulture) };
                db.BlockTable.Add(block);
                block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(5.5, i, 0)));
                db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(i, 0, 0), block));
            }

            return DwgWriter.ToDwg(db.ToText(), DwgWriter.DefaultVersion);
        }

        private static double Best(byte[] file, int runs)
        {
            double best = double.MaxValue;

            for (int run = 0; run < runs; run++)
            {
                GC.Collect();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                WithThreads(1, () => DwgReader.ToDxf(file));
                best = Math.Min(best, clock.Elapsed.TotalMilliseconds);
            }

            return best;
        }

        [Fact]
        public void ADwgOfSixteenThousandBlocks_IsWrittenInAboutFourTimesTheTimeOfOneOfFour_NotInSixteen()
        {
            // The blocks were listed through a list asked whether it held each one already: 16 000 of them were 128 million
            // comparisons, more than all the rest of the writing, and four times the blocks took eleven times as long.
            byte[] small = OneDwgOfBlocks(4_000);
            byte[] large = OneDwgOfBlocks(16_000);

            WithThreads(1, () => DwgReader.ToDxf(OneDwgOfBlocks(300)));

            double ofSmall = Best(small, 3);
            double ofLarge = Best(large, 3);

            // Linear is 4; the list made it 11 (57 ms and 649 ms), the rest of the reading having its own share of the time.
            Assert.True(
                ofLarge < ofSmall * 6.0,
                "4 000 blocks took " + ofSmall.ToString("F0") + " ms, 16 000 took " + ofLarge.ToString("F0") + " ms"
            );
        }
    }
}
