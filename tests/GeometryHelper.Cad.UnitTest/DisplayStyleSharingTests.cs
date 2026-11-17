using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.TestDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The styles the hatches and MTEXTs make of their own, the lines of a pattern, a gradient's middle colour and the runs of an
    /// MTEXT in a colour, are one object for each colour and weight, as the style of a LINE is: a drawing of 100 000 hatches and
    /// MTEXTs held 75 000 styles of 100 bytes each, one for each of them, and holds a few.
    /// </summary>
    public class DisplayStyleSharingTests
    {
        private const string Layers = "0\nLAYER\n2\nNotes\n70\n0\n62\n2\n6\nCONTINUOUS\n";

        private static string MText(string handle, string layer, string words) =>
            Entity(
                "MTEXT",
                layer,
                256,
                handle,
                100,
                "AcDbEntity",
                100,
                "AcDbMText",
                10,
                10.0,
                20,
                20.0,
                30,
                0.0,
                40,
                2.5,
                41,
                60.0,
                71,
                1,
                1,
                words
            );

        /// <summary>A hatch of ANSI31 on a rectangle 40 by 30 with one pattern line at 45 degrees, 2.2 apart.</summary>
        private static string PatternHatch(string handle, string layer, double x) =>
            Entity(
                "HATCH",
                layer,
                256,
                handle,
                100,
                "AcDbEntity",
                100,
                "AcDbHatch",
                10,
                0.0,
                20,
                0.0,
                30,
                0.0,
                210,
                0.0,
                220,
                0.0,
                230,
                1.0,
                2,
                "ANSI31",
                70,
                0,
                71,
                0,
                91,
                1,
                92,
                3,
                72,
                1,
                73,
                1,
                93,
                4,
                10,
                x,
                20,
                0.0,
                10,
                x + 40.0,
                20,
                0.0,
                10,
                x + 40.0,
                20,
                30.0,
                10,
                x,
                20,
                30.0,
                97,
                0,
                75,
                0,
                76,
                1,
                52,
                45.0,
                41,
                1.0,
                77,
                0,
                78,
                1,
                53,
                45.0,
                43,
                0.0,
                44,
                0.0,
                45,
                -2.2,
                46,
                2.2,
                79,
                0,
                47,
                0.1,
                98,
                0
            );

        private static List<DisplayItem2> PartsOf(DisplayList2 list) =>
            list.Items.OfType<DisplayText2>().SelectMany(t => t.Parts).ToList();

        [Fact]
        public void TwoMTextsWithARedRun_ShareTheStyleOfTheirRedRunAndTheStyleOfTheRest()
        {
            DxfDatabase database = Drawing(
                Layers,
                null,
                MText("A1", "Notes", "PLATE {\\C1;RED} AND ROOM"),
                MText("A2", "Notes", "BOLTS {\\C1;RED} AND NUTS")
            );
            var document = CadDocument.From(database, "mtexts.dxf");
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            List<DisplayStyle> red = PartsOf(list).Where(p => p.Style.Rgb == 0xFF0000).Select(p => p.Style).ToList();
            List<DisplayStyle> rest = PartsOf(list).Where(p => p.Style.Rgb != 0xFF0000).Select(p => p.Style).ToList();

            Assert.True(red.Count >= 2);
            Assert.True(rest.Count >= 4);
            Assert.All(red, s => Assert.Same(red[0], s));
            Assert.All(rest, s => Assert.Same(rest[0], s));
            Assert.Equal(0xFF0000, red[0].Rgb);
            Assert.Equal(0xFFFF00, rest[0].Rgb);
            Assert.Null(red[0].Dashes);
        }

        [Fact]
        public void TwoPatternHatchesOnOneLayer_ShareTheStyleOfTheirLines()
        {
            DxfDatabase database = Drawing(
                Layers,
                null,
                PatternHatch("B1", "Notes", 0.0),
                PatternHatch("B2", "Notes", 100.0)
            );
            var document = CadDocument.From(database, "hatches.dxf");
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            List<DisplaySegments2> hatches = list.Items.OfType<DisplaySegments2>().ToList();

            Assert.Equal(2, hatches.Count);
            Assert.Same(hatches[0].Style, hatches[1].Style);
            Assert.Equal(0xFFFF00, hatches[0].Style.Rgb);
            Assert.Null(hatches[0].Style.Dashes);
            Assert.Equal("Notes", hatches[0].Layer);
            Assert.Equal("Notes", hatches[1].Layer);
        }

        [Fact]
        public void AMTextOnOneLayerAndAHatchOnAnother_KeepTheirOwnLayerAndColour()
        {
            string other = "0\nLAYER\n2\nBolts\n70\n0\n62\n3\n6\nCONTINUOUS\n";
            DxfDatabase database = Drawing(
                Layers + other,
                null,
                MText("C1", "Notes", "PLATE"),
                PatternHatch("C2", "Bolts", 100.0),
                MText("C3", "Bolts", "BOLT")
            );
            var document = CadDocument.From(database, "mixed.dxf");
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            DisplayText2 notes = Assert.IsType<DisplayText2>(list.Items.First(i => i.Handle == "C1"));
            DisplayText2 bolts = Assert.IsType<DisplayText2>(list.Items.First(i => i.Handle == "C3"));
            DisplaySegments2 hatch = Assert.IsType<DisplaySegments2>(list.Items.First(i => i.Handle == "C2"));

            Assert.All(notes.Parts, p => Assert.Equal("Notes", p.Layer));
            Assert.All(notes.Parts, p => Assert.Equal(0xFFFF00, p.Style.Rgb));
            Assert.All(bolts.Parts, p => Assert.Equal("Bolts", p.Layer));
            Assert.All(bolts.Parts, p => Assert.Equal(0x00FF00, p.Style.Rgb));
            Assert.Equal("Bolts", hatch.Layer);
            Assert.Equal(0x00FF00, hatch.Style.Rgb);
        }
    }
}
