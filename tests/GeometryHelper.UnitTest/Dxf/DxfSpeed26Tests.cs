using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of speed17 in the core: the object of a record read for a display, which keeps nothing of it, made and let
    /// go of, one object for one record for every reader that keeps it; the extended data of an object in no file kept
    /// outside it, so that an object of a record is 8 bytes smaller; the walk of a block's records with no copy of its list;
    /// and a DWG stopped as its pages, its objects and its entities are read. Each is held to what it was before, and the
    /// sizes to the bytes allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed26Tests
    {
        private static string Handle(int i) => (0x100 + i).ToString("X", CultureInfo.InvariantCulture);

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static DxfDatabase Mixed(int count)
        {
            var text = new StringBuilder();
            text.Append("0\nSECTION\n2\nHEADER\n9\n$ACADVER\n1\nAC1015\n9\n$HANDSEED\n5\nFFFFFF\n0\nENDSEC\n");
            text.Append("0\nSECTION\n2\nTABLES\n");
            text.Append(
                "0\nTABLE\n2\nLTYPE\n70\n1\n0\nLTYPE\n2\nCONTINUOUS\n70\n0\n3\nSolid line\n72\n65\n73\n0\n40\n0\n0\nENDTAB\n"
            );
            text.Append(
                "0\nTABLE\n2\nLAYER\n70\n1\n0\nLAYER\n2\n0\n70\n0\n62\n7\n6\nCONTINUOUS\n0\nENDTAB\n0\nENDSEC\n"
            );
            text.Append("0\nSECTION\n2\nENTITIES\n");

            for (int i = 0; i < count; i++)
            {
                string head = "5\n" + Handle(i) + "\n8\n0\n";
                string x = Number(i * 3.0);

                switch (i % 5)
                {
                    case 0:
                        text.Append("0\nLINE\n" + head + "10\n" + x + "\n20\n0\n30\n0\n11\n" + x + "\n21\n5\n31\n0\n");
                        break;
                    case 1:
                        text.Append("0\nCIRCLE\n" + head + "10\n" + x + "\n20\n0\n30\n0\n40\n2\n");
                        break;
                    case 2:
                        text.Append("0\nARC\n" + head + "10\n" + x + "\n20\n0\n30\n0\n40\n2\n50\n10\n51\n100\n");
                        break;
                    case 3:
                        text.Append(
                            "0\nLWPOLYLINE\n"
                                + head
                                + "100\nAcDbPolyline\n90\n2\n70\n0\n10\n"
                                + x
                                + "\n20\n0\n10\n"
                                + x
                                + "\n20\n4\n"
                        );
                        break;
                    default:
                        text.Append("0\nTEXT\n" + head + "10\n" + x + "\n20\n0\n30\n0\n40\n2.5\n1\nT\n");
                        break;
                }
            }

            text.Append("0\nENDSEC\n0\nEOF\n");
            return DxfDatabase.Parse(text.ToString());
        }

        #region The object of a record read for a display

        [Fact]
        public void ALineAnArcACircleAndAPolyline_GlancedAt_AreNotHeldByTheirRecords_AndTheNextReaderThatKeepsOneGetsTheOnlyOne()
        {
            DxfDatabase database = Mixed(100);
            DxfItem[] items = database.ModelSpace.WalkedItems(out _, out _, out _).ToArray();
            Assert.Equal(100, items.Length);
            var glance = new DxfEntity.Glance();
            var first = new Dictionary<Type, DxfEntity>();

            foreach (DxfItem item in items)
            {
                DxfEntity read = glance.At(item);
                bool unkept =
                    item.Type == "LINE" || item.Type == "ARC" || item.Type == "CIRCLE" || item.Type == "LWPOLYLINE";

                if (unkept)
                {
                    // Held by no record, and the one of its class every record of it is aimed at in turn.
                    Assert.Null(item.Wrapper);
                    Assert.Equal(item.Handle, read.Handle);
                    Assert.Same(read, glance.At(item));

                    if (first.TryGetValue(read.GetType(), out DxfEntity earlier))
                    {
                        Assert.Same(earlier, read);
                    }
                    else
                    {
                        first[read.GetType()] = read;
                    }
                }
                else
                {
                    // A TEXT, whose object keeps what it read, is made and held as it was.
                    Assert.Same(read, item.Wrapper);
                    Assert.Same(read, glance.At(item));
                }

                // The object a reader that keeps it is given: one for the record, the same on every ask, by every road, and
                // one the glance never gave.
                DxfEntity held = DxfEntity.Of(item);
                Assert.Same(held, item.Wrapper);
                Assert.Same(held, DxfEntity.Of(item));
                Assert.Same(held, glance.At(item));
                Assert.Equal(read.GetType(), held.GetType());

                if (unkept)
                {
                    Assert.NotSame(read, held);
                }
            }

            DxfEntity[] walked = database.ModelSpace.Cast<DxfEntity>().ToArray();

            for (int i = 0; i < items.Length; i++)
            {
                Assert.Same(items[i].Wrapper, walked[i]);
            }
        }

        [Fact]
        public void AnObjectGlancedAt_ReadsWhatTheOneTheRecordHoldsReads_AndIsAimedAtTheNextRecordAsked()
        {
            DxfDatabase database = Mixed(100);
            var glance = new DxfEntity.Glance();
            DxfItem[] items = database.ModelSpace.WalkedItems(out _, out _, out _).ToArray();

            // Each read before the next record is asked for, as the display reads it.
            foreach (DxfItem item in items)
            {
                DxfEntity read = glance.At(item);
                DxfEntity held = DxfEntity.Of(item);

                Assert.Equal(held.Handle, read.Handle);
                Assert.Equal(held.Layer, read.Layer);
                Assert.Equal(held.Visible, read.Visible);
                Assert.Equal(held.LineWeight, read.LineWeight);
                Assert.Equal(held.Linetype, read.Linetype);
                Assert.Equal(held.GeometricExtents.Min.X, read.GeometricExtents.Min.X);
                Assert.Equal(held.GeometricExtents.Max.Y, read.GeometricExtents.Max.Y);
                Assert.Equal(held.GetGeometry().Count(), read.GetGeometry().Count());
            }

            // Two records of one class asked in turn, neither held: the object is the same and reads the second.
            DxfDatabase again = Mixed(100);
            DxfItem[] lines = again.ModelSpace.WalkedItems(out _, out _, out _).Where(i => i.Type == "LINE").ToArray();
            DxfEntity one = glance.At(lines[0]);
            string firstHandle = one.Handle;
            DxfEntity two = glance.At(lines[1]);

            Assert.Same(one, two);
            Assert.NotEqual(firstHandle, one.Handle);
            Assert.Equal(lines[1].Handle, one.Handle);
        }

        [Fact]
        public void TenThousandObjectsOfLines_AreMadeOfTwentyFourBytesEach_WhereTheyWereThirtyTwo()
        {
            // An object of a record: the header, the record and, where it was, a reference to the extended data it kept for an
            // object in no file, which only a table record made in code has.
            const int count = 10_000;
            DxfDatabase database = Mixed(5 * count);
            DxfItem[] lines = database
                .ModelSpace.WalkedItems(out _, out _, out _)
                .Where(item => item.Type == "LINE")
                .ToArray();
            Assert.Equal(count, lines.Length);

            var keep = new DxfEntity[count];
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < count; i++)
            {
                keep[i] = DxfEntity.Of(lines[i]);
            }

            long used = (GC.GetAllocatedBytesForCurrentThread() - before) / count;
            GC.KeepAlive(keep);

            Assert.True(used < 30, "allocated " + used + " bytes an object");
        }

        #endregion

        #region The extended data of an object in no file

        [Fact]
        public void TheExtendedDataOfALayerMadeInCode_IsKeptUntilItIsAdded_ReplacedBySettingAgain_AndWrittenWhenItIs()
        {
            DxfDatabase database = Mixed(5);
            var layer = new DxfLayerTableRecord { Name = "GH_PENDING" };

            Assert.Empty(layer.XData);

            layer.XData = new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "first") };
            Assert.Equal(2, layer.XData.Count);
            Assert.Equal("first", layer.XData[1].Value);

            layer.XData = new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "second") };
            Assert.Equal(2, layer.XData.Count);
            Assert.Equal("second", layer.XData[1].Value);

            // Another layer made in code has none of it.
            Assert.Empty(new DxfLayerTableRecord { Name = "GH_OTHER" }.XData);

            database.LayerTable.Add(layer);
            Assert.Equal(2, layer.XData.Count);
            Assert.Equal("second", layer.XData[1].Value);
            Assert.Equal("second", database.LayerTable["GH_PENDING"].XData[1].Value);

            DxfDatabase again = DxfDatabase.Parse(database.ToText());
            Assert.Equal("second", again.LayerTable["GH_PENDING"].XData[1].Value);
        }

        [Fact]
        public void TheExtendedDataOfManyLayersMadeInCode_AreEachTheirOwn_AndNoneIsLeftOnceTheyAreAdded()
        {
            DxfDatabase database = Mixed(5);
            var layers = new List<DxfLayerTableRecord>();

            for (int i = 0; i < 50; i++)
            {
                var layer = new DxfLayerTableRecord { Name = "GH_" + i.ToString(CultureInfo.InvariantCulture) };
                layer.XData = new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1000, "v" + i) };
                layers.Add(layer);
            }

            foreach (DxfLayerTableRecord layer in layers)
            {
                database.LayerTable.Add(layer);
            }

            for (int i = 0; i < 50; i++)
            {
                Assert.Equal("v" + i, layers[i].XData[1].Value);
            }
        }

        #endregion

        #region The walk of a block's records

        [Fact]
        public void TheRecordsWalkedForADisplay_AreTheEntitiesInTheirOrder_WithNoCopyOfTheList_AndNoneErased()
        {
            DxfDatabase database = Mixed(60);
            DxfBlockTableRecord model = database.ModelSpace;
            string[] handles = model.Cast<DxfEntity>().Select(e => e.Handle).ToArray();

            IReadOnlyList<DxfItem> items = model.WalkedItems(out DxfSpace space, out int version, out bool columns);

            Assert.False(columns);
            Assert.Same(space, items);
            Assert.Equal(handles, items.Select(i => i.Handle).ToArray());
            Assert.Equal(space.Version, version);

            // One erased: it is no longer walked, and the list the walk gave is the space's, so it says so as well.
            model.Cast<DxfEntity>().ElementAt(7).Erase();
            IReadOnlyList<DxfItem> after = model.WalkedItems(out DxfSpace sameSpace, out int newer, out _);

            Assert.Same(space, sameSpace);
            Assert.Equal(59, after.Count);
            Assert.DoesNotContain(handles[7], after.Select(i => i.Handle));
            Assert.Equal(handles.Where((h, i) => i != 7).ToArray(), after.Select(i => i.Handle).ToArray());
            Assert.NotEqual(version, newer);
        }

        [Fact]
        public void ABlockOfNoEntities_AndAFileOfNoModelSpace_WalkNothing()
        {
            DxfDatabase database = Mixed(0);

            Assert.Empty(database.ModelSpace.WalkedItems(out _, out int version, out bool columns));
            Assert.False(columns);
            Assert.Equal(0, version);
        }

        #endregion

        #region A DWG given up

        private static byte[] OneDwg(int count)
        {
            var writer = new DxfWriter();

            for (int i = 0; i < count; i++)
            {
                writer.Add(new GeoLine2(new GeoPoint2(i % 50, i / 50), new GeoPoint2(i % 50 + 0.5, i / 50 + 0.25)));
            }

            return DwgWriter.ToDwg(DxfDatabase.Parse(writer.ToString()).ToText(), DwgWriter.DefaultVersion);
        }

        [Fact]
        public void ADwgReadWithATokenAlreadyStopped_IsGivenUpAsItsContainerIsRead_ByNoneOfTheOtherExceptions()
        {
            byte[] file = OneDwg(500);

            using (var stopped = new CancellationTokenSource())
            {
                stopped.Cancel();

                Assert.Throws<OperationCanceledException>(() => DwgContainer.Read(file, stopped.Token));

                // Not made into a FormatException, which a broken file is told as.
                Assert.Throws<OperationCanceledException>(() => DwgReader.ToDxf(file, null, null, stopped.Token));
            }
        }

        [Fact]
        public void ADwgReadWithATokenNotStopped_IsTheDxfItWasRead_AsOneReadWithNone()
        {
            byte[] file = OneDwg(500);

            using (var running = new CancellationTokenSource())
            {
                Assert.Equal(DwgReader.ToDxf(file), DwgReader.ToDxf(file, null, null, running.Token));
            }
        }

        [Fact]
        public void ADwgStoppedInTheMiddleOfItsObjects_IsGivenUpAtOnce_AsItsSharesToldShow()
        {
            string path = Path.Combine(Path.GetTempPath(), "gh-dwg-stop26-" + Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                File.WriteAllBytes(path, OneDwg(3_000));

                foreach (double mark in new[] { 0.1, 0.3, 0.55, 0.7, 0.85 })
                {
                    using (var source = new CancellationTokenSource())
                    {
                        var told = new List<double>();
                        var progress = new Recorder(told, mark, source);

                        Assert.Throws<OperationCanceledException>(() =>
                            DwgReader.Open(path, null, progress, source.Token)
                        );

                        // The share that stopped it was the last told, or the one just after: the reading did not run on.
                        Assert.True(
                            told[told.Count - 1] < mark + 0.03,
                            "stopped at " + mark + ", told up to " + told[told.Count - 1]
                        );
                    }
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private sealed class Recorder : IProgress<double>
        {
            private readonly List<double> _told;
            private readonly double _mark;
            private readonly CancellationTokenSource _source;

            public Recorder(List<double> told, double mark, CancellationTokenSource source)
            {
                _told = told;
                _mark = mark;
                _source = source;
            }

            public void Report(double value)
            {
                _told.Add(value);

                if (value >= _mark)
                {
                    _source.Cancel();
                }
            }
        }

        #endregion
    }
}
