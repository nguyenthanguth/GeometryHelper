using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.TestDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A layer switched off is patched into the display list by taking the runs of the entities that read it and nothing else
    /// as empty runs, without drawing the entities again. Each test checks the patched list, its box and the insertion points
    /// of every entity against a drawing that gets the same edits and is regenerated after each of them.
    /// </summary>
    public class DisplayHiddenLayerRunsTests
    {
        private static CadDocument Open(string text) =>
            CadDocument.Open(new MemoryStream(new UTF8Encoding(false).GetBytes(text)), "hidden.dxf");

        private static string Bits(double d) =>
            BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture);

        private static string[] HandlesOf(CadDocument document) =>
            document.ModelSpace.Block.Select(e => e.Handle).Where(h => h != null).ToArray();

        private static List<string> State(CadDocument document)
        {
            DisplayList2 list = document.GetDisplay(document.ModelSpace);
            List<string> state = DisplayPatchTests.Describe(list);
            state.Add(
                "box "
                    + Bits(list.Bounds.MinX)
                    + " "
                    + Bits(list.Bounds.MinY)
                    + " "
                    + Bits(list.Bounds.MaxX)
                    + " "
                    + Bits(list.Bounds.MaxY)
            );

            for (int run = 0; run < list.Runs.Count; run++)
            {
                DisplayRun r = list.Runs[run];
                // The bits of the layers are numbered as the layers were first met, by this list and the one it was patched from.
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

        private static void AssertSameAsRegen(CadDocument patched, CadDocument whole, string step)
        {
            whole.Regen();
            List<string> expected = State(whole);
            List<string> got = State(patched);

            if (expected.Count != got.Count)
            {
                Assert.Fail(step + ": " + got.Count + " lines where a REGEN gives " + expected.Count);
            }

            for (int i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(expected[i], got[i], StringComparison.Ordinal))
                {
                    Assert.Fail(step + ": line " + i + "\n  patched " + got[i] + "\n  regen   " + expected[i]);
                }
            }
        }

        private static void SetOn(CadDocument a, CadDocument b, string layer, bool on)
        {
            Assert.True(CadLayerEditor.SetOn(a, layer, on).Succeeded);
            Assert.True(CadLayerEditor.SetOn(b, layer, on).Succeeded);
        }

        [Fact]
        public void ALayerSwitchedOffAndOn_LeavesTheRunsOfItsEntitiesEmptyAndThenDrawnAgain_LikeARegen()
        {
            string text = BigDrawing.Text(900);
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);

            SetOn(patched, whole, "Steel", false);
            DisplayList2 off = patched.GetDisplay(patched.ModelSpace);
            Assert.True(off.PatchedInPlace);
            Assert.All(off.Items, i => Assert.NotEqual("Steel", i.Layer));
            Assert.Equal(HandlesOf(patched).Length, off.Runs.Count);
            Assert.Contains(off.Runs, r => r.Count == 0 && r.Layers != 0);
            AssertSameAsRegen(patched, whole, "Steel off");

            SetOn(patched, whole, "Notes", false);
            AssertSameAsRegen(patched, whole, "Steel and Notes off");

            SetOn(patched, whole, "Steel", true);
            AssertSameAsRegen(patched, whole, "Steel on, Notes off");

            SetOn(patched, whole, "Notes", true);
            AssertSameAsRegen(patched, whole, "both on");
            Assert.Contains(patched.GetDisplay(patched.ModelSpace).Items, i => i.Layer == "Steel");
        }

        [Fact]
        public void AnInsertOfALayerSwitchedOff_WhoseBlockHoldsOtherLayers_IsEmptyWhole_AndOneWhoseBlockLayerIsOffDrawnWithout()
        {
            string layers = Layer("Steel", 1) + Layer("Notes", 2) + Layer("Bolts", 3);
            string blocks =
                Block("PART", Line(0, 0, 10, 0), Circle(5, 5, 3, "Notes"), Arc(0, 0, 4, 0, 180, "Bolts"))
                + Block("FLAT", Line(0, 0, 30, 0), Circle(2, 2, 1));
            DxfDatabase database = Drawing(
                layers,
                blocks,
                Insert("PART", 0, 0, 1.0, 0.0, "Steel", handle: "B1"),
                Insert("PART", 50, 0, 2.0, 10.0, "Notes", handle: "B2"),
                Insert("FLAT", 0, 50, 1.0, 0.0, "Steel", handle: "B3"),
                Insert("FLAT", 50, 50, 1.0, 20.0, "Bolts", handle: "B4"),
                Line(0, 0, 100, 100, "Steel", handle: "B5")
            );
            var stream = new MemoryStream();
            database.Save(stream);
            string text = Encoding.UTF8.GetString(stream.ToArray());
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);
            AssertSameAsRegen(patched, whole, "opened");

            // B1 reads Steel, Notes and Bolts, B3 reads Steel alone (its entities are on layer 0, which an INSERT's layer gives them).
            SetOn(patched, whole, "Steel", false);
            AssertSameAsRegen(patched, whole, "Steel off");

            SetOn(patched, whole, "Notes", false);
            AssertSameAsRegen(patched, whole, "Steel and Notes off");

            SetOn(patched, whole, "Steel", true);
            AssertSameAsRegen(patched, whole, "Steel on");

            SetOn(patched, whole, "Notes", true);
            SetOn(patched, whole, "Bolts", false);
            AssertSameAsRegen(patched, whole, "Bolts off");

            SetOn(patched, whole, "Bolts", true);
            AssertSameAsRegen(patched, whole, "Bolts on");
        }

        [Fact]
        public void AnEntityOfALayerSwitchedOff_GivenAnotherLayerByTheEdit_IsDrawnThere()
        {
            string text = BigDrawing.Text(400);
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);
            SetOn(patched, whole, "Steel", false);
            AssertSameAsRegen(patched, whole, "Steel off");

            string handle = patched.ModelSpace.Block.OfType<DxfLine>().First(l => l.Layer == "Steel").Handle;

            foreach (CadDocument d in new[] { patched, whole })
            {
                Assert.True(
                    d.ApplyEdit("CHPROP", new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle) }, s =>
                        s.Document.FindEntity(handle).Layer = "Notes"
                    ).Succeeded
                );
            }

            DisplayList2 after = patched.GetDisplay(patched.ModelSpace);
            Assert.Contains(after.Items, i => string.Equals(i.Handle, handle, StringComparison.OrdinalIgnoreCase));
            AssertSameAsRegen(patched, whole, "one entity moved to Notes");

            SetOn(patched, whole, "Steel", true);
            AssertSameAsRegen(patched, whole, "Steel on");
        }

        [Fact]
        public void LayersBeyondTheSixtyThird_ShareABit_ThatIsHiddenWhenAllOfThemAre()
        {
            const int layerCount = 70;
            string layers = string.Concat(Enumerable.Range(0, layerCount).Select(i => Layer("L" + i, 1 + i % 7)));
            var entities = new List<string>();

            for (int i = 0; i < 280; i++)
            {
                entities.Add(Line(i, 0, i + 5, 5 + i % 9, "L" + i % layerCount, handle: (0x100 + i).ToString("X")));
            }

            var stream = new MemoryStream();
            Drawing(layers, string.Empty, entities.ToArray()).Save(stream);
            string text = Encoding.UTF8.GetString(stream.ToArray());
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            patched.GetDisplay(patched.ModelSpace);

            // L65 shares its bit with L63 to L69: switched off alone it hides nothing of the others.
            SetOn(patched, whole, "L65", false);
            AssertSameAsRegen(patched, whole, "L65 off");
            Assert.DoesNotContain(patched.GetDisplay(patched.ModelSpace).Items, i => i.Layer == "L65");
            Assert.Contains(patched.GetDisplay(patched.ModelSpace).Items, i => i.Layer == "L66");

            foreach (int layer in new[] { 63, 64, 66, 67, 68, 69 })
            {
                SetOn(patched, whole, "L" + layer, false);
                AssertSameAsRegen(patched, whole, "L" + layer + " off");
            }

            Assert.DoesNotContain(patched.GetDisplay(patched.ModelSpace).Items, i => i.Layer == "L68");

            SetOn(patched, whole, "L67", true);
            AssertSameAsRegen(patched, whole, "L67 on");
            Assert.Contains(patched.GetDisplay(patched.ModelSpace).Items, i => i.Layer == "L67");
        }

        /// <summary>
        /// A list of more than 131 072 items finds its box again on several threads: the box a layer switched off leaves is the
        /// one a REGEN makes, to the bit, whichever layer holds the entities at the sides.
        /// </summary>
        [Theory]
        [InlineData("Steel")]
        [InlineData("Notes")]
        [InlineData("Bolts")]
        public void ALayerSwitchedOffInALongList_LeavesTheBoxARegenMakes(string layer)
        {
            string text = BigDrawing.Text(90000);
            CadDocument patched = Open(text);
            CadDocument whole = Open(text);
            Assert.True(patched.GetDisplay(patched.ModelSpace).Count > 131072);

            SetOn(patched, whole, layer, false);
            DisplayList2 list = patched.GetDisplay(patched.ModelSpace);
            Assert.True(list.PatchedInPlace);
            AssertBoxAsRegen(patched, whole);

            SetOn(patched, whole, layer, true);
            AssertBoxAsRegen(patched, whole);
        }

        private static void AssertBoxAsRegen(CadDocument patched, CadDocument whole)
        {
            whole.Regen();
            DisplayBox want = whole.GetDisplay(whole.ModelSpace).Bounds;
            DisplayBox got = patched.GetDisplay(patched.ModelSpace).Bounds;

            Assert.Equal(Bits(want.MinX), Bits(got.MinX));
            Assert.Equal(Bits(want.MinY), Bits(got.MinY));
            Assert.Equal(Bits(want.MaxX), Bits(got.MaxX));
            Assert.Equal(Bits(want.MaxY), Bits(got.MaxY));
        }

        /// <summary>
        /// A layer switched off at 300 000 entities is shown in a twelfth of what a REGEN takes or less: the entities of the layer
        /// are not read from their records, drawn and found to have nothing to draw. (The same work took a ninth of a REGEN and
        /// more before; the limit is the ratio, which a busy machine moves less than it moves a time.)
        /// </summary>
        [Fact]
        public void ALayerSwitchedOffAtThreeHundredThousandEntities_IsShownInATwelfthOfARegen()
        {
            CadDocument document = Open(BigDrawing.Text(300000));
            document.GetDisplay(document.ModelSpace);
            double regen = double.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                document.Regen();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                document.GetDisplay(document.ModelSpace);
                regen = Math.Min(regen, watch.Elapsed.TotalSeconds);
            }

            SpeedGuard.AssertWithin(
                regen / 12.0,
                () =>
                {
                    // Each run starts with the layer on and drawn, and switches it off: only the display after that is timed.
                    Assert.True(CadLayerEditor.SetOn(document, "Steel", true).Succeeded);
                    document.GetDisplay(document.ModelSpace);
                    Assert.True(CadLayerEditor.SetOn(document, "Steel", false).Succeeded);
                    GC.Collect();

                    return document;
                },
                d => d.GetDisplay(d.ModelSpace),
                6
            );
        }
    }
}
