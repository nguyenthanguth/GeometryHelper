using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest;
using Xunit;
using static GeometryHelper.Cad.UnitTest.TestDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The work of speed14 in the display: the build of a list made whole through less garbage and with the room it needs made
    /// ahead, and a layer switched off patched in pieces on several threads. Each is checked against the entities drawn alone
    /// or a REGEN, item for item, and held by the bytes allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DisplaySpeed16Tests
    {
        private static CadDocument Open(string text) =>
            CadDocument.Open(new MemoryStream(new UTF8Encoding(false).GetBytes(text)), "speed16.dxf");

        private static string Bits(double d) =>
            BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture);

        /// <summary>The items of a list, then its box, as the tests of the patch describe them, and the insertion points of every run.</summary>
        private static List<string> State(DisplayList2 list)
        {
            List<string> state = DisplayPatchTests.Describe(list);

            for (int run = 0; run < list.Runs.Count; run++)
            {
                DisplayRun r = list.Runs[run];
                state.Add(
                    r.Handle
                        + ": "
                        + r.Count
                        + " "
                        + string.Join(" ", list.InsertionPointsOf(r.Handle).Select(p => Bits(p.X) + "," + Bits(p.Y)))
                );
            }

            return state;
        }

        private static void AssertSame(List<string> expected, List<string> got, string step)
        {
            if (expected.Count != got.Count)
            {
                Assert.Fail(step + ": " + got.Count + " lines where " + expected.Count + " are wanted");
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(expected[i], got[i], StringComparison.Ordinal))
                {
                    Assert.Fail(step + ": line " + i + "\n  got    " + got[i] + "\n  wanted " + expected[i]);
                }
            }
        }

        /// <summary>The items each entity of the space gives when it is drawn alone, as a preview draws it, in a list of their own.</summary>
        private static List<string> DrawnAlone(CadDocument document)
        {
            var builder = new DisplayBuilder(document.Database, document);
            var list = new DisplayList2();

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                foreach (DisplayItem2 item in builder.BuildLoose(new[] { entity }))
                {
                    list.Add(item);
                }
            }

            return DisplayPatchTests.Describe(list);
        }

        private static List<string> Items(DisplayList2 list) => DisplayPatchTests.Describe(list);

        [Fact]
        public void ADrawingOfMoreEntitiesThanTheRoomIsJudgedFrom_IsDrawnItemForItem_AsEachEntityDrawnAlone()
        {
            // 12 000 entities of every kind of the usual drawing: the room for the runs, the items and the insertion points is made
            // once, after the first 4 096, from what they showed.
            CadDocument document = Open(BigDrawing.Text(12_000));

            AssertSame(DrawnAlone(document), Items(document.GetDisplay(document.ModelSpace)), "mixed");
        }

        [Fact]
        public void ADrawingWhoseFirstEntitiesAreSmallerThanTheRest_GrowsPastTheRoomMade_ItemForItem()
        {
            // 5 000 lines, each an item, then 3 000 INSERTs of a block of 20 entities, each some twenty: the room made for the first
            // is far short and the list grows as it did.
            string blocks = Block("B", Enumerable.Range(0, 20).Select(k => Line(k, 0, k + 5, 4)).ToArray());
            var entities = new List<string>();

            for (int i = 0; i < 5_000; i++)
            {
                entities.Add(
                    Line(i, 0, i + 3, 7, "0", handle: (0x100 + i).ToString("X", CultureInfo.InvariantCulture))
                );
            }

            for (int i = 0; i < 3_000; i++)
            {
                entities.Add(
                    Insert(
                        "B",
                        i * 3,
                        50,
                        1.0,
                        i % 90,
                        "0",
                        handle: (0x2000 + i).ToString("X", CultureInfo.InvariantCulture)
                    )
                );
            }

            CadDocument document = CadDocument.From(Drawing(Layer("0", 7), blocks, entities.ToArray()), "grow.dxf");
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.True(list.Count > 60_000, "items " + list.Count);
            AssertSame(DrawnAlone(document), Items(list), "growing");
            Assert.Equal(8_000, list.Runs.Count);
        }

        [Fact]
        public void ADrawingWhoseFirstEntitiesAreLargerThanTheRest_IsCutToItsSize_ItemForItem()
        {
            // 3 000 INSERTs, then 6 000 lines: the room made for the first is far more than the list holds, and is given back.
            string blocks = Block("B", Enumerable.Range(0, 20).Select(k => Line(k, 0, k + 5, 4)).ToArray());
            var entities = new List<string>();

            for (int i = 0; i < 3_000; i++)
            {
                entities.Add(
                    Insert(
                        "B",
                        i * 3,
                        50,
                        1.0,
                        i % 90,
                        "0",
                        handle: (0x100 + i).ToString("X", CultureInfo.InvariantCulture)
                    )
                );
            }

            for (int i = 0; i < 6_000; i++)
            {
                entities.Add(
                    Line(i, 0, i + 3, 7, "0", handle: (0x2000 + i).ToString("X", CultureInfo.InvariantCulture))
                );
            }

            CadDocument document = CadDocument.From(Drawing(Layer("0", 7), blocks, entities.ToArray()), "cut.dxf");
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            AssertSame(DrawnAlone(document), Items(list), "cut");
            Assert.Equal(9_000, list.Runs.Count);
        }

        [Fact]
        public void ADrawingRegenerated_GivesTheListItGaveBefore_ItemForItemAndPointForPoint()
        {
            CadDocument document = Open(BigDrawing.Text(12_000));
            List<string> first = State(document.GetDisplay(document.ModelSpace));

            document.Regen();

            AssertSame(first, State(document.GetDisplay(document.ModelSpace)), "regen");
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandEntities_MakesFewerThanEightHundredBytesAnEntity()
        {
            // The usual mix of lines, arcs, circles, polylines, texts and a few INSERTs, the first touch of each record among the
            // bytes: 1 100 an entity drawn before speed14 (the enumerator of each walk of a shape, the lists of each polyline, the
            // arrays a text laid out for its box, the room the lists grew by doubling) and 700 after it. The bytes are those of this
            // thread, which draws alone.
            CadDocument document = Open(BigDrawing.Text(20_000));

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(list.Count > 20_000);
            Assert.True(used / 20_000 < 800, "allocated " + used / 20_000 + " bytes an entity");
        }

        [Fact]
        public void ALayerSwitchedOffAndOnInADrawingOfMoreRunsThanOneThreadTakes_IsPatchedLikeARegen()
        {
            // 200 000 entities: more than the 131 072 runs a patch takes on one thread, so that the runs are taken in pieces on
            // several, the items of each by the count of those kept before it, and the insertion points of the runs dropped
            // taken out at the same time as the records are moved.
            string text = BigDrawing.Text(200_000);
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);

            Assert.True(CadLayerEditor.SetOn(patched, "Steel", false).Succeeded);
            Assert.True(CadLayerEditor.SetOn(whole, "Steel", false).Succeeded);
            DisplayList2 off = patched.GetDisplay(patched.ModelSpace);
            Assert.True(off.PatchedInPlace);
            Assert.All(off.Items, i => Assert.NotEqual("Steel", i.Layer));
            whole.Regen();
            AssertSame(State(whole.GetDisplay(whole.ModelSpace)), State(off), "Steel off");

            Assert.True(CadLayerEditor.SetOn(patched, "Notes", false).Succeeded);
            Assert.True(CadLayerEditor.SetOn(whole, "Notes", false).Succeeded);
            whole.Regen();
            AssertSame(
                State(whole.GetDisplay(whole.ModelSpace)),
                State(patched.GetDisplay(patched.ModelSpace)),
                "Steel and Notes off"
            );

            Assert.True(CadLayerEditor.SetOn(patched, "Steel", true).Succeeded);
            Assert.True(CadLayerEditor.SetOn(whole, "Steel", true).Succeeded);
            Assert.True(CadLayerEditor.SetOn(patched, "Notes", true).Succeeded);
            Assert.True(CadLayerEditor.SetOn(whole, "Notes", true).Succeeded);
            whole.Regen();
            AssertSame(
                State(whole.GetDisplay(whole.ModelSpace)),
                State(patched.GetDisplay(patched.ModelSpace)),
                "both on"
            );
        }

        [Fact]
        public void ALayerSwitchedOffInADrawingOfMoreRunsThanOneThreadTakes_TakesLessThanTheBuildOfTheWholeDoes()
        {
            string text = BigDrawing.Text(200_000);
            CadDocument document = null;

            TimeSpan whole = SpeedGuard.AssertWithin(
                20.0,
                () => document = Open(text),
                d => d.GetDisplay(d.ModelSpace),
                attempts: 1
            );

            // The layer off: each run from a display built and a layer back on, as an edit and a patch.
            TimeSpan off = SpeedGuard.AssertWithin(
                whole.TotalSeconds / 2.0,
                () =>
                {
                    Assert.True(CadLayerEditor.SetOn(document, "Steel", true).Succeeded);
                    document.GetDisplay(document.ModelSpace);
                    Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
                    return document;
                },
                d => d.GetDisplay(d.ModelSpace),
                attempts: 8
            );

            Assert.True(off < whole, "off " + off.TotalMilliseconds + " ms of " + whole.TotalMilliseconds);
        }
    }
}
