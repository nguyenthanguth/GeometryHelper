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
    /// The work of speed15 in the display: the layer of an entity found among the few the builder drew last (by the string its
    /// record gave), the curves nearly every drawing is made of sent to their shapes at once, and the words of a TEXT cleaned and
    /// trimmed with no builder and no array made. Each is checked against the entities drawn alone, by a builder of its own for
    /// each, item for item, and held by the bytes allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DisplaySpeed20Tests
    {
        private static string Handle(int i) => (0x100 + i).ToString("X", CultureInfo.InvariantCulture);

        /// <summary>The items each entity of the space gives when it is drawn alone by a builder made for it, which has met no layer before.</summary>
        private static List<string> DrawnAloneByNewBuilders(CadDocument document)
        {
            var list = new DisplayList2();

            foreach (DxfEntity entity in document.ModelSpace.Block)
            {
                var builder = new DisplayBuilder(document.Database, document);

                foreach (DisplayItem2 item in builder.BuildLoose(new[] { entity }))
                {
                    list.Add(item);
                }
            }

            return DisplayPatchTests.Describe(list);
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

        /// <summary>Twelve layers, one switched off, and two blocks: one whose entities are on layer 0 and one with entities of a layer of their own.</summary>
        private static CadDocument TwelveLayers(int count, out int[] perLayer)
        {
            string layers = string.Concat(
                Enumerable
                    .Range(0, 12)
                    .Select(k => Layer("L" + k.ToString("00", CultureInfo.InvariantCulture), 1 + k, off: k == 5))
            );
            string blocks =
                Block("BK", Line(0, 0, 5, 5), Circle(2, 2, 1), Text(1, 1, 1, "A%%dB"))
                + Block("BL", Line(0, 0, 3, 9, "L03"), Arc(2, 2, 3, 10, 200, "L07"), Line(1, 1, 4, 4));
            var entities = new List<string>();
            perLayer = new int[12];

            for (int i = 0; i < count; i++)
            {
                // Neighbours never share a layer, and more layers than the builder keeps slots for take turns.
                int k = (i * 5 + i / 7) % 12;
                string layer = i % 41 == 0 ? "0" : "L" + k.ToString("00", CultureInfo.InvariantCulture);
                double x = (i % 100) * 30.0;
                double y = (i / 100) * 30.0;
                string h = Handle(i);

                switch ((i * 7) % 9)
                {
                    case 0:
                        entities.Add(Line(x, y, x + 20, y + 12, layer, handle: h));
                        break;
                    case 1:
                        entities.Add(Arc(x + 10, y + 10, 8, 30, 250, layer, h));
                        break;
                    case 2:
                        entities.Add(Circle(x + 15, y + 15, 9, layer, handle: h));
                        break;
                    case 3:
                        entities.Add(
                            Polyline(layer, h, true, x, y, 0.0, x + 20, y, 0.5, x + 20, y + 20, 0.0, x, y + 20, 0.0)
                        );
                        break;
                    case 4:
                        entities.Add(Polyline(layer, h, false, x, y, 0.0, x + 20, y, -0.3, x + 20, y + 20, 0.0));
                        break;
                    case 5:
                        entities.Add(Text(x + 2, y + 2, 2.5, i % 3 == 0 ? "%%uN%%d  %%c10%%p1" : "T" + i, layer, h));
                        break;
                    case 6:
                        entities.Add(Insert("BK", x + 5, y + 5, 1.0, i * 7 % 360, layer, handle: h));
                        break;
                    case 7:
                        entities.Add(Insert("BL", x + 5, y + 5, 1.0, i * 11 % 360, layer, handle: h));
                        break;
                    default:
                        entities.Add(Text(x + 2, y + 2, 2.5, "  ", layer, h));
                        break;
                }
            }

            return CadDocument.From(Drawing(layers, blocks, entities.ToArray()), "twelve.dxf");
        }

        [Fact]
        public void EntitiesAlternatingAmongTwelveLayers_AreDrawnItemForItem_AsEachDrawnByABuilderThatMetNoLayerBefore()
        {
            CadDocument document = TwelveLayers(3_000, out _);

            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.True(list.Count > 3_000, "items " + list.Count);
            AssertSame(DrawnAloneByNewBuilders(document), DisplayPatchTests.Describe(list), "twelve layers");
        }

        [Fact]
        public void AnEntityOnALayerSwitchedOff_IsNotDrawn_AndAnINSERTTakesItsLayerForTheEntitiesOfLayerZero()
        {
            CadDocument document = TwelveLayers(3_000, out _);

            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.DoesNotContain(list.Items, i => i.Layer == "L05");

            // The entities of BK are on layer 0, so each INSERT's items stand on the INSERT's layer; those of BL on the layer of their own.
            foreach (DisplayRun run in list.Runs)
            {
                DxfEntity entity = document
                    .Database.ModelSpace.Cast<DxfEntity>()
                    .FirstOrDefault(e => e.Handle == run.Handle);

                if (entity is DxfBlockReference reference && reference.Layer != "L05" && run.Count > 0)
                {
                    string[] layers = list
                        .Items.Skip(run.Start)
                        .Take(run.Count)
                        .Select(i => i.Layer)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(l => l, StringComparer.Ordinal)
                        .ToArray();

                    if (reference.BlockTableRecord.Name == "BK")
                    {
                        Assert.Equal(new[] { reference.Layer }, layers);
                    }
                }
            }
        }

        [Fact]
        public void LayersSwitchedOffAndOnAmongTwelve_AreDrawnByAPatchAsARegenDrawsThem()
        {
            string text;

            using (var stream = new MemoryStream())
            {
                TwelveLayers(4_000, out _).Database.Save(stream);
                text = Encoding.UTF8.GetString(stream.ToArray());
            }

            CadDocument patched = CadDocument.Open(new MemoryStream(new UTF8Encoding(false).GetBytes(text)), "p.dxf");
            CadDocument whole = CadDocument.Open(new MemoryStream(new UTF8Encoding(false).GetBytes(text)), "w.dxf");
            patched.GetDisplay(patched.ModelSpace);

            foreach (
                (string layer, bool on) in new[]
                {
                    ("L03", false),
                    ("L08", false),
                    ("L03", true),
                    ("L05", true),
                    ("L08", true),
                }
            )
            {
                Assert.True(CadLayerEditor.SetOn(patched, layer, on).Succeeded);
                Assert.True(CadLayerEditor.SetOn(whole, layer, on).Succeeded);
                whole.Regen();

                AssertSame(
                    DisplayPatchTests.Describe(whole.GetDisplay(whole.ModelSpace)),
                    DisplayPatchTests.Describe(patched.GetDisplay(patched.ModelSpace)),
                    layer + (on ? " on" : " off")
                );
            }
        }

        [Fact]
        public void LinesArcsCirclesAndPolylinesOfAWidthOrNone_AreDrawnItemForItem_AsEachDrawnByABuilderOfItsOwn()
        {
            // The kinds sent to their shapes at once, and a polyline with a width, which the cases below them still draw as a band.
            var entities = new List<string>();

            for (int i = 0; i < 200; i++)
            {
                double x = i * 30.0;
                entities.Add(Line(x, 0, x + 20, 12, "0", handle: Handle(i * 6)));
                entities.Add(Arc(x + 10, 10, 8, 30, 250, "0", Handle(i * 6 + 1)));
                entities.Add(Circle(x + 15, 15, 9, "0", handle: Handle(i * 6 + 2)));
                entities.Add(
                    Polyline("0", Handle(i * 6 + 3), true, x, 0, 0.0, x + 20, 0, 0.5, x + 20, 20, 0.0, x, 20, 0.0)
                );
                entities.Add(Polyline("0", Handle(i * 6 + 4), false, x, 0, 0.0, x + 20, 0, 0.0, x + 20, 20, 0.0));
                entities.Add(WidePolyline("0", Handle(i * 6 + 5), x, 40.0, 3.0));
            }

            CadDocument document = CadDocument.From(
                Drawing(string.Empty, string.Empty, entities.ToArray()),
                "plain.dxf"
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.True(list.Count > 1_200, "items " + list.Count);
            Assert.Contains(list.Items, i => i is DisplayFill2);
            AssertSame(DrawnAloneByNewBuilders(document), DisplayPatchTests.Describe(list), "plain");
        }

        /// <summary>A polyline of two segments with a width of its own on every vertex: drawn as a band, not as a line.</summary>
        private static string WidePolyline(string layer, string handle, double x, double y, double width) =>
            "0\nLWPOLYLINE\n5\n"
            + handle
            + "\n8\n"
            + layer
            + "\n100\nAcDbEntity\n100\nAcDbPolyline\n90\n3\n70\n0\n43\n"
            + Number(width)
            + "\n10\n"
            + Number(x)
            + "\n20\n"
            + Number(y)
            + "\n10\n"
            + Number(x + 20)
            + "\n20\n"
            + Number(y)
            + "\n10\n"
            + Number(x + 20)
            + "\n20\n"
            + Number(y + 20)
            + "\n";

        #region The words of a text

        private static List<string> WordsOf(params string[] values)
        {
            CadDocument document = CadDocument.From(
                Drawing(
                    string.Empty,
                    string.Empty,
                    values.Select((v, i) => Text(i * 40.0, 0.0, 2.5, v, "0", Handle(i))).ToArray()
                ),
                "words.dxf"
            );

            return document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Select(t => t.Text).ToList();
        }

        [Fact]
        public void TheCodesOfADegreeAPlusMinusAndADiameterSign_BecomeTheSignsInTheWordsOfATextOfAnyCase()
        {
            Assert.Equal(
                new[] { "A°B", "1±0.5", "Ø10", "°±Ø", "100Ø", "50°", "°X" },
                WordsOf("A%%dB", "1%%p0.5", "%%c10", "%%D%%P%%C", "100%%c", "50%%d", "%%dX")
            );
        }

        [Fact]
        public void ACodeThatIsNotOne_AndAPercentPairAtTheEnd_StayInTheWords()
        {
            // %%z is no code; "A%%" ends before the letter a code needs; "%%" alone stays; a single % stays.
            Assert.Equal(new[] { "A%%zB", "A%%", "%%", "5% off" }, WordsOf("A%%zB", "A%%", "%%", "5% off"));
        }

        [Fact]
        public void TheSpacesAtEitherEndOfAText_AreKept_AndATextOfNothingButSpacesIsNotDrawn()
        {
            // The words keep the spaces AutoCAD justifies them by; a text of spaces only gives no item.
            Assert.Equal(new[] { "  AB  ", "AB" }, WordsOf("  AB  ", "   ", "AB", "", " "));
        }

        [Fact]
        public void TheCodesOfAUnderlineAThroughLineAndAnOverline_LeaveTheWordsAndMarkTheirLetters()
        {
            CadDocument document = CadDocument.From(
                Drawing(string.Empty, string.Empty, Text(0.0, 0.0, 2.5, "%%uAB%%uC %%d%%kD", "0", "A1")),
                "marks.dxf"
            );

            DisplayText2 text = Assert.Single(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>());

            Assert.Equal("ABC °D", text.Text);
        }

        #endregion

        #region Garbage

        private static long BytesOfAColdDisplay(string entities, int count, out DisplayList2 list)
        {
            CadDocument document = CadDocument.From(
                Drawing(Layer("A", 1) + Layer("B", 2), string.Empty, entities),
                "garbage.dxf"
            );

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            list = document.GetDisplay(document.ModelSpace);
            return (GC.GetAllocatedBytesForCurrentThread() - before) / count;
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandTexts_MakesFewerThanSixHundredAndFiftyBytesAText()
        {
            // The TEXT kept as the display needs it, the words cleaned and trimmed, the item: 707 bytes a text under .NET
            // Framework before speed15 (the builder and its buffer, the array of each Trim, the words made again), 547 after.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                text.Append(
                    Text(
                        (i % 300) * 30.0,
                        (i / 300) * 30.0,
                        2.5,
                        "T" + i.ToString(CultureInfo.InvariantCulture),
                        i % 3 == 0 ? "A" : "B",
                        Handle(i)
                    )
                );
            }

            long used = BytesOfAColdDisplay(text.ToString(), count, out DisplayList2 list);

            Assert.Equal(count, list.Items.OfType<DisplayText2>().Count());
            Assert.True(used < 650, "allocated " + used + " bytes a text");
        }

        [Fact]
        public void AColdDisplayOfTwentyThousandClosedPolylinesWithABulge_MakesFewerThanTwelveHundredBytesAPolyline()
        {
            // A closed polyline with an arc is a loop of arcs in space: the walk of its vertices, the planes tested over them and
            // the area it is asked for made 1 324 bytes a polyline under .NET Framework before speed15, and 1 060 after it.
            const int count = 20_000;
            var text = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                double x = (i % 300) * 30.0;
                double y = (i / 300) * 30.0;
                text.Append(
                    Polyline("0", Handle(i), true, x, y, 0.0, x + 20, y, 0.5, x + 20, y + 20, 0.0, x, y + 20, 0.0)
                );
            }

            long used = BytesOfAColdDisplay(text.ToString(), count, out DisplayList2 list);

            Assert.True(list.Count >= count, "items " + list.Count);
            Assert.True(used < 1_200, "allocated " + used + " bytes a polyline");
        }

        [Fact]
        public void ARegenOfLinesOnThreeLayersInTurn_TakesAboutAsLongAsOfLinesOnOne()
        {
            // The layer of an entity is found among the few the builder drew last, by the string its record gave: the lines on
            // three layers in turn cost the lookup of a name in three tables each, a fifth of a REGEN of 200 000 lines before
            // speed15 (25 ms where 21), and a twelfth after it (18 ms where 17).
            const int count = 200_000;
            string layers = Layer("Steel", 1) + Layer("Notes", 2) + Layer("Bolts", 3);
            string one = string.Concat(
                Enumerable.Range(0, count).Select(i => Line(i, 0, i + 5, 4, "Steel", handle: Handle(i)))
            );
            string three = string.Concat(
                Enumerable
                    .Range(0, count)
                    .Select(i => Line(i, 0, i + 5, 4, new[] { "Steel", "Notes", "Bolts" }[i % 3], handle: Handle(i)))
            );

            CadDocument Regenerated(string entities)
            {
                CadDocument document = CadDocument.From(Drawing(layers, string.Empty, entities), "layers.dxf");
                document.GetDisplay(document.ModelSpace);
                document.Regen();
                return document;
            }

            // The best of five regens of the lines on one layer, then the lines on three within an eighth of it.
            TimeSpan onOne = TimeSpan.MaxValue;

            for (int i = 0; i < 5; i++)
            {
                TimeSpan took = SpeedGuard.AssertWithin(
                    60.0,
                    () => Regenerated(one),
                    d => d.GetDisplay(d.ModelSpace),
                    attempts: 1
                );
                onOne = took < onOne ? took : onOne;
            }

            SpeedGuard.AssertWithin(
                onOne.TotalSeconds * 1.12,
                () => Regenerated(three),
                d => d.GetDisplay(d.ModelSpace),
                attempts: 10
            );
        }

        #endregion
    }
}
