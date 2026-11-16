using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.TestDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The work of speed17 in the display: a chain (<see cref="DisplayPolyline2"/>) of 72 bytes where it was 80, its four
    /// flags kept in the look it shares with the chains of its style and layer; the LINEs, ARCs, CIRCLEs and polylines a
    /// display reads made no object to keep; and a DWG whose opening is stopped part way. Each is checked against what it
    /// was before, item for item, and the sizes by the bytes allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DisplaySpeed26Tests
    {
        private static string Handle(int i) => (0x100 + i).ToString("X", CultureInfo.InvariantCulture);

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        #region The chain

        [Fact]
        public void AChainOfTwoVertices_AndALongerOne_AreMadeOfSeventyTwoBytesAndTheirArray()
        {
            // The box (32 bytes), the array or the lack of it, the look and the handle, after the header: 72 bytes. The four bools
            // the flags were made 80, and one byte in their place would still be 80, for an object is made of whole words.
            const int count = 50_000;
            var style = new DisplayStyle(0xFF0000, 0.0, null);
            var longer = new double[] { 0, 0, 10, 5, 20, 0, 30, 5 };

            // The looks of each of the flags are made once, before the bytes are counted.
            for (int flags = 0; flags < 16; flags++)
            {
                _ = Chain(style, flags);
            }

            var keep = new DisplayPolyline2[count];
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < count; i++)
            {
                keep[i] = new DisplayPolyline2(0.0, 0.0, 10.0 + i, 5.0, style, "A1", "0");
            }

            long twoVertices = (GC.GetAllocatedBytesForCurrentThread() - before) / count;
            before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < count; i++)
            {
                keep[i] = new DisplayPolyline2(longer, true, style, "A1", "0");
            }

            long fourVertices = (GC.GetAllocatedBytesForCurrentThread() - before) / count;

            GC.KeepAlive(keep);
            Assert.True(twoVertices < 76, "allocated " + twoVertices + " bytes a chain of two vertices");
            Assert.True(fourVertices < 76, "allocated " + fourVertices + " bytes a chain of four vertices");
        }

        /// <summary>A chain made for the flags: closed, sampled and the way the two ends of a chain of two face, each bit as the number says.</summary>
        private static DisplayPolyline2 Chain(DisplayStyle style, int flags)
        {
            bool closed = (flags & 1) != 0;
            bool sampled = (flags & 2) != 0;
            bool flipX = (flags & 4) != 0;
            bool flipY = (flags & 8) != 0;
            DisplayPolyline2 chain = new DisplayPolyline2(
                new[] { flipX ? 10.0 : 0.0, flipY ? 7.0 : 3.0, flipX ? 0.0 : 10.0, flipY ? 3.0 : 7.0 },
                closed,
                style,
                "A1",
                "0"
            );
            chain.IsSampled = sampled;
            return chain;
        }

        [Fact]
        public void EveryChain_ReadsBackItsFlags_ItsVerticesAndItsBox_ToTheBit()
        {
            double nan = double.NaN;
            double[][] shapes =
            {
                new[] { 0.0, 3.0, 10.0, 7.0 },
                new[] { 10.0, 3.0, 0.0, 7.0 },
                new[] { 0.0, 7.0, 10.0, 3.0 },
                new[] { 10.0, 7.0, 0.0, 3.0 },
                new[] { 4.0, 4.0, 4.0, 4.0 },
                new[] { 0.0, -0.0, -0.0, 0.0 },
                new[] { nan, 3.0, 10.0, 7.0 },
                new[] { 0.0, 3.0, 10.0, nan },
                new[] { 0.0, 0.0, 10.0, 0.0, 10.0, 5.0 },
                new[] { 0.0, 0.0, 10.0, 0.0, 10.0, 5.0, 0.0, 5.0 },
                new[] { 1.0, 2.0 },
            };

            foreach (DisplayStyle style in new[] { new DisplayStyle(0xFF0000, 0.0, null), null })
            {
                foreach (double[] xy in shapes)
                {
                    for (int flags = 0; flags < 4; flags++)
                    {
                        bool closed = (flags & 1) != 0;
                        bool sampled = (flags & 2) != 0;
                        string step =
                            string.Join(" ", xy.Select(d => d.ToString("R", CultureInfo.InvariantCulture)))
                            + (closed ? " closed" : " open")
                            + (sampled ? " sampled" : string.Empty);
                        var chain = new DisplayPolyline2((double[])xy.Clone(), closed, style, "A1", "0");

                        // Sampled after it is made, in either order with a look taken by a clip or a flag, as the display sets it.
                        if (sampled)
                        {
                            chain.IsSampled = true;
                        }

                        Assert.True(closed == chain.Closed, step);
                        Assert.True(sampled == chain.IsSampled, step);
                        Assert.Equal(xy.Length / 2, chain.Count);
                        Assert.Equal("A1", chain.Handle);
                        Assert.Equal("0", chain.Layer);
                        Assert.Same(style, chain.Style);

                        IReadOnlyList<double> read = chain.Xy;
                        Assert.Equal(xy.Length, read.Count);

                        for (int i = 0; i < xy.Length; i++)
                        {
                            Assert.True(Bits(xy[i]) == Bits(read[i]), step + " number " + i);
                        }

                        for (int v = 0; v < Math.Min(2, xy.Length / 2); v++)
                        {
                            Assert.True(Bits(xy[2 * v]) == Bits(chain.VertexX(v)), step + " X " + v);
                            Assert.True(Bits(xy[2 * v + 1]) == Bits(chain.VertexY(v)), step + " Y " + v);
                        }

                        DisplayBox box = DisplayBox.Empty;

                        for (int i = 0; i + 1 < xy.Length; i += 2)
                        {
                            box = box.Add(xy[i], xy[i + 1]);
                        }

                        Assert.True(Bits(box.MinX) == Bits(chain.Bounds.MinX), step);
                        Assert.True(Bits(box.MinY) == Bits(chain.Bounds.MinY), step);
                        Assert.True(Bits(box.MaxX) == Bits(chain.Bounds.MaxX), step);
                        Assert.True(Bits(box.MaxY) == Bits(chain.Bounds.MaxY), step);

                        // Turned back and forth, the flag set stays what it was set to and the others stay with it.
                        chain.IsSampled = !sampled;
                        Assert.True(closed == chain.Closed, step);
                        Assert.True(sampled != chain.IsSampled, step);
                        chain.IsSampled = sampled;
                        Assert.True(sampled == chain.IsSampled, step);
                        Assert.Equal(read.Count, chain.Xy.Count);

                        var moved = (DisplayPolyline2)chain.Moved(1.5, -2.5);
                        Assert.True(closed == moved.Closed, step);
                        Assert.True(sampled == moved.IsSampled, step);
                    }
                }
            }
        }

        [Fact]
        public void AChainWhoseLookIsItsOwn_KeepsItsFlags_AndTheChainsOfTheSameLookKeepTheirs()
        {
            var style = new DisplayStyle(0x00FF00, 0.0, null);
            DisplayPolyline2 plain = Chain(style, 4 | 1);
            DisplayPolyline2 sampled = Chain(style, 4 | 2 | 8);
            DisplayPolyline2 set = Chain(style, 2 | 1);

            // A flag the display sets on an item gives it a look of its own, which carries the chain's flags along.
            set.IsFrameInside = true;
            set.IsClipFrame = true;
            set.IsSampled = false;
            sampled.IsNotPlotted = true;

            Assert.True(plain.Closed && !plain.IsSampled);
            Assert.False(plain.IsFrameInside);
            Assert.False(plain.IsClipFrame);
            Assert.True(!set.IsSampled && set.Closed && set.IsFrameInside && set.IsClipFrame);
            Assert.True(sampled.IsSampled && !sampled.Closed && sampled.IsNotPlotted);
            Assert.False(plain.IsNotPlotted);
            Assert.Equal(10.0, plain.VertexX(0));
            Assert.Equal(7.0, sampled.VertexY(0));
            Assert.Equal(0.0, sampled.VertexY(1) - 3.0);
        }

        [Fact]
        public void ChainsMadeAtOnceOnManyThreads_OfOneStyleAndLayer_AllReadBackTheirOwnFlags()
        {
            var style = new DisplayStyle(0x0000FF, 0.0, null);
            var wrong = new List<string>();

            Parallel.For(0, 16, new ParallelOptions { MaxDegreeOfParallelism = 8 }, thread =>
            {
                var random = new Random(thread);

                for (int i = 0; i < 20_000; i++)
                {
                    int flags = random.Next(16);
                    DisplayPolyline2 chain = Chain(style, flags);
                    bool ok =
                        chain.Closed == ((flags & 1) != 0)
                        && chain.IsSampled == ((flags & 2) != 0)
                        && chain.VertexX(0) == ((flags & 4) != 0 ? 10.0 : 0.0)
                        && chain.VertexY(0) == ((flags & 8) != 0 ? 7.0 : 3.0);

                    if (!ok)
                    {
                        lock (wrong)
                        {
                            wrong.Add("thread " + thread + " chain " + i + " flags " + flags);
                        }

                        return;
                    }
                }
            });

            Assert.Empty(wrong);
        }

        #endregion

        #region What a display reads leaves no object of its entities

        private static string Mixed(int count)
        {
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                double x = (i % 30) * 30.0;
                double y = (i / 30) * 30.0;

                switch (i % 6)
                {
                    case 0:
                        text.Append(Line(x, y, x + 20, y + 12, "A", handle: Handle(i)));
                        break;
                    case 1:
                        text.Append(Circle(x, y, 5.0, "B", handle: Handle(i)));
                        break;
                    case 2:
                        text.Append(Arc(x, y, 6.0, 10.0, 200.0, "A", handle: Handle(i)));
                        break;
                    case 3:
                        text.Append(Polyline("B", Handle(i), true, x, y, 0.0, x + 20, y, 0.5, x + 20, y + 20, 0.0));
                        break;
                    case 4:
                        text.Append(Polyline("A", Handle(i), false, x, y, 0.0, x + 20, y + 5, 0.0, x + 30, y, 0.0));
                        break;
                    default:
                        text.Append(Text(x, y, 2.5, "T" + i.ToString(CultureInfo.InvariantCulture), "B", Handle(i)));
                        break;
                }
            }

            return text.ToString();
        }

        [Fact]
        public void ALineAnArcACircleAndAPolyline_AreDrawnItemForItem_WhetherTheirObjectsWereMadeFirstOrNot()
        {
            string text = Mixed(600);
            CadDocument fresh = CadDocument.From(
                Drawing(Layer("A", 1) + Layer("B", 2), string.Empty, text),
                "fresh.dxf"
            );
            CadDocument made = CadDocument.From(Drawing(Layer("A", 1) + Layer("B", 2), string.Empty, text), "made.dxf");

            // Every object of the second is made, and kept, before it is drawn: the display then uses those.
            DxfEntity[] kept = made.ModelSpace.Block.Cast<DxfEntity>().ToArray();
            DisplayList2 drawn = fresh.GetDisplay(fresh.ModelSpace);

            Assert.Equal(600, kept.Length);
            Assert.Equal(
                DisplayPatchTests.Describe(made.GetDisplay(made.ModelSpace)),
                DisplayPatchTests.Describe(drawn)
            );
            Assert.Contains(drawn.Items.OfType<DisplayPolyline2>(), p => p.Count == 3);
            Assert.Contains(drawn.Items.OfType<DisplayArc2>(), a => a.Radius == 6.0);

            // The objects the second document was asked for come back as they were, one for each record, and so do those of
            // the first, made now that it is asked: the display left none to be taken for another.
            DxfEntity[] again = made.ModelSpace.Block.Cast<DxfEntity>().ToArray();
            DxfEntity[] first = fresh.ModelSpace.Block.Cast<DxfEntity>().ToArray();
            DxfEntity[] second = fresh.ModelSpace.Block.Cast<DxfEntity>().ToArray();

            for (int i = 0; i < kept.Length; i++)
            {
                Assert.Same(kept[i], again[i]);
                Assert.Same(first[i], second[i]);
                Assert.Equal(kept[i].GetType(), first[i].GetType());
                Assert.Equal(kept[i].Handle, first[i].Handle);
            }
        }

        [Fact]
        public void ADrawingRedrawnAfterAnEdit_IsDrawnItemForItem_AsOneRedrawnWhoseObjectsWereMade()
        {
            string text = Mixed(300);
            CadDocument fresh = CadDocument.From(
                Drawing(Layer("A", 1) + Layer("B", 2), string.Empty, text),
                "fresh.dxf"
            );
            CadDocument made = CadDocument.From(Drawing(Layer("A", 1) + Layer("B", 2), string.Empty, text), "made.dxf");
            _ = made.ModelSpace.Block.Cast<DxfEntity>().ToArray();
            _ = fresh.GetDisplay(fresh.ModelSpace);
            _ = made.GetDisplay(made.ModelSpace);

            // The same edit in both: the line of the first record moved, and a circle added.
            foreach (CadDocument document in new[] { fresh, made })
            {
                var line = (DxfLine)document.FindEntity(Handle(0));
                line.EndPoint = new GeoPoint3(40.0, 40.0, 0.0);
                document.ModelSpace.Block.AppendEntity(
                    new DxfCircle(new GeoPoint3(5.0, 5.0, 0.0), new GeoVector3(0.0, 0.0, 1.0), 3.0)
                );
                document.Regen();
            }

            Assert.Equal(
                DisplayPatchTests.Describe(made.GetDisplay(made.ModelSpace)),
                DisplayPatchTests.Describe(fresh.GetDisplay(fresh.ModelSpace))
            );
        }

        private static long BytesOfAColdDisplay(string entities, int count)
        {
            CadDocument document = CadDocument.From(
                Drawing(Layer("A", 1) + Layer("B", 2), string.Empty, entities),
                "garbage.dxf"
            );

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            long used = (GC.GetAllocatedBytesForCurrentThread() - before) / count;
            Assert.True(list.Count >= count, "items " + list.Count);
            return used;
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandLines_MakesFewerThanFourHundredBytesALine()
        {
            // 417 bytes a line under .NET Framework in speed16: the line read from its record, its chain (80 bytes), the handle, the
            // object of the record (32) kept by it, the run and the item's place, and the copy of the space's list the walk made.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                double x = (i % 300) * 30.0;
                double y = (i / 300) * 30.0;
                text.Append(Line(x, y, x + 20, y + 12, i % 3 == 0 ? "A" : "B", handle: Handle(i)));
            }

            long used = BytesOfAColdDisplay(text.ToString(), count);

            Assert.True(used < 400, "allocated " + used + " bytes a line");
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandCircles_MakesFewerThanFourHundredAndFortyBytesACircle()
        {
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                text.Append(Circle((i % 300) * 30.0, (i / 300) * 30.0, 5.0, i % 3 == 0 ? "A" : "B", handle: Handle(i)));
            }

            long used = BytesOfAColdDisplay(text.ToString(), count);

            Assert.True(used < 440, "allocated " + used + " bytes a circle");
        }

        #endregion

        #region A DWG stopped part way

        private static string WrittenDwg(int count)
        {
            var writer = new DxfWriter();

            for (int i = 0; i < count; i++)
            {
                double x = (i % 100) * 30.0;
                double y = (i / 100) * 30.0;

                if (i % 3 == 0)
                {
                    writer.Add(new GeoCircle2(new GeoPoint2(x, y), 5.0));
                }
                else
                {
                    writer.Add(new GeoLine2(new GeoPoint2(x, y), new GeoPoint2(x + 20, y + 12)));
                }
            }

            string path = Path.Combine(Path.GetTempPath(), "gh-dwg-stop-" + Guid.NewGuid().ToString("N") + ".dwg");
            DwgWriter.Write(DxfDatabase.Parse(writer.ToString()), path);
            return path;
        }

        /// <summary>A progress that stops its token when the share told reaches a mark, and keeps every share it was told.</summary>
        private sealed class StopsAt : IProgress<double>
        {
            private readonly CancellationTokenSource _source;
            private readonly double _mark;

            public StopsAt(CancellationTokenSource source, double mark)
            {
                _source = source;
                _mark = mark;
            }

            public List<double> Told { get; } = new List<double>();

            public void Report(double value)
            {
                Told.Add(value);

                if (value >= _mark)
                {
                    _source.Cancel();
                }
            }
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.2, 0.25)]
        [InlineData(0.5, 0.55)]
        [InlineData(0.62, 0.67)]
        [InlineData(0.8, 0.85)]
        public void ADwgStoppedWhileItIsRead_IsGivenUpWithinAStepOfWhereItWasStopped(double stopAt, double mustNotPass)
        {
            string path = WrittenDwg(6_000);

            try
            {
                using (var source = new CancellationTokenSource())
                {
                    var progress = new StopsAt(source, stopAt);

                    Assert.Throws<OperationCanceledException>(() => CadDocument.Open(path, progress, source.Token));

                    // Each share is told as its step begins and the token is looked at as the next one does, so the last share
                    // told is the one that stopped it, or the one just after it: never the end of the reading, as it was.
                    Assert.True(
                        progress.Told[progress.Told.Count - 1] <= mustNotPass,
                        "told "
                            + progress.Told[progress.Told.Count - 1].ToString(CultureInfo.InvariantCulture)
                            + " after it was stopped at "
                            + stopAt.ToString(CultureInfo.InvariantCulture)
                    );
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ADwgOpenedWithATokenNotStopped_IsTheDocumentOpenedWithoutOne()
        {
            string path = WrittenDwg(3_000);

            try
            {
                using (var running = new CancellationTokenSource())
                {
                    CadDocument with = CadDocument.Open(path, null, running.Token);
                    CadDocument without = CadDocument.Open(path);

                    Assert.Equal(without.Database.ToText(), with.Database.ToText());
                    Assert.Equal(
                        DisplayPatchTests.Describe(without.GetDisplay(without.ModelSpace)),
                        DisplayPatchTests.Describe(with.GetDisplay(with.ModelSpace))
                    );
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        #endregion
    }
}
