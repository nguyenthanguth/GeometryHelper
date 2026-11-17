using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.TestDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What the display reads of an INSERT in one pass of its groups, where its block, its point and its transformation were
    /// each read alone: the picture is the same as that of an INSERT whose record holds an extension dictionary, which is
    /// read property by property, and an INSERT edited after it was drawn is drawn with what it has now.
    /// </summary>
    public class DisplayInsertReadTests
    {
        private static string PlateBlock() =>
            Block(
                "PLATE",
                Line(0, 0, 10, 0, "0"),
                Circle(5, 5, 2, "0"),
                Arc(5, 0, 3, 10, 120, "0"),
                Line(0, 0, 0, 4, "0")
            );

        private static CadDocument Plan(bool dictionary)
        {
            // The text of the drawing is made here so that a record may carry its dictionary between its handle and its layer.
            string[] entities =
            {
                Insert("PLATE", 0, 0, 1.0, 0.0, "Steel", handle: "A1"),
                Insert("PLATE", 100, 20, 2.0, 30.0, "Steel", handle: "A2"),
                Insert("PLATE", -40, 7.5, 0.5, 270.0, "Notes", handle: "A3"),
                Insert("plate", 60, 60, -1.5, 15.0, "Notes", handle: "A4"),
                "0\nINSERT\n5\nA5\n8\nSteel\n2\nPLATE\n10\n10\n20\n20\n30\n3\n41\n2\n42\n0.5\n43\n1\n50\n20\n210\n0\n220\n0.6\n230\n0.8\n",
                Insert("NONE", 5, 5, 1.0, 0.0, "Steel", handle: "A6"),
            };

            if (dictionary)
            {
                entities = entities
                    .Select(e => e.Replace("\n8\n", "\n102\n{ACAD_XDICTIONARY\n360\nDEAD\n102\n}\n8\n"))
                    .ToArray();
            }

            return CadDocument.From(
                Drawing(Layer("Steel", 1) + Layer("Notes", 2), PlateBlock(), entities),
                "inserts.dxf"
            );
        }

        private static string Bits(double d) =>
            BitConverter.DoubleToInt64Bits(d).ToString("X16", CultureInfo.InvariantCulture);

        /// <summary>The items of a list: their kind, handle and bounds to the bit, in order.</summary>
        private static List<string> Picture(DisplayList2 list) =>
            list
                .Items.Select(i =>
                    i.GetType().Name
                    + " "
                    + i.Handle
                    + " "
                    + Bits(i.Bounds.MinX)
                    + " "
                    + Bits(i.Bounds.MinY)
                    + " "
                    + Bits(i.Bounds.MaxX)
                    + " "
                    + Bits(i.Bounds.MaxY)
                )
                .ToList();

        [Fact]
        public void InsertsReadInOnePass_AreDrawnAsInsertsReadByTheirProperties_Are()
        {
            DisplayList2 one = Plan(false).GetDisplay(Plan(false).ModelSpace);
            CadDocument other = Plan(true);
            DisplayList2 each = other.GetDisplay(other.ModelSpace);

            Assert.Equal(20, one.Count);
            Assert.Equal(Picture(each), Picture(one));
            Assert.Equal(5, one.Items.Select(i => i.Handle).Distinct().Count());
        }

        [Fact]
        public void AnInsertEditedAfterItWasDrawn_IsDrawnWithItsNewPointTurnAndScale()
        {
            CadDocument document = Plan(false);
            DisplayList2 before = document.GetDisplay(document.ModelSpace);
            DisplayItem2 line = before.Items.First(i => i.Handle == "A1");

            Assert.Equal(0.0, line.Bounds.MinX, 9);
            Assert.Equal(10.0, line.Bounds.MaxX, 9);

            document.ApplyEdit("EDIT", new[] { new CadEditRecord(CadEditRecordKind.Reshaped, "A1") }, s =>
            {
                var insert = (DxfBlockReference)s.Document.FindEntity("A1");
                insert.Position = new GeoPoint3(500, 300, 0);
                insert.Rotation = Math.PI / 2.0;
                insert.ScaleFactors = new GeoVector3(3.0, 3.0, 1.0);
            });
            DisplayList2 after = document.GetDisplay(document.ModelSpace);
            DisplayItem2 turned = after.Items.First(i => i.Handle == "A1");

            // Turned a quarter and scaled by 3 from (500, 300): the block's line, 10 along its x, runs up to 330, and its circle, up to 7 in y, reaches 21 to the left.
            Assert.Equal(500.0 - 3.0 * 7.0, after.Items.Where(i => i.Handle == "A1").Min(i => i.Bounds.MinX), 6);
            Assert.Equal(300.0 + 30.0, after.Items.Where(i => i.Handle == "A1").Max(i => i.Bounds.MaxY), 6);

            // What a REGEN draws of the edited drawing is what the patch drew.
            document.Regen();
            Assert.Equal(Picture(after), Picture(document.GetDisplay(document.ModelSpace)));
            Assert.NotEqual(Picture(before), Picture(after));
            Assert.NotNull(turned);
        }

        [Fact]
        public void AnInsertWithAnAttribute_DrawsItsAttributeAndOneWithNoneDrawsNone()
        {
            string withAttribute =
                "0\nINSERT\n5\nB1\n8\nSteel\n66\n1\n2\nPLATE\n10\n30\n20\n40\n30\n0\n"
                + "0\nATTRIB\n5\nB2\n8\nSteel\n10\n31\n20\n41\n30\n0\n40\n2.5\n1\nVALUE\n2\nTAG\n70\n0\n"
                + "0\nSEQEND\n5\nB3\n8\nSteel\n";
            CadDocument document = CadDocument.From(
                Drawing(
                    Layer("Steel", 1),
                    PlateBlock(),
                    withAttribute,
                    Insert("PLATE", 100, 0, 1.0, 0.0, "Steel", handle: "B4")
                ),
                "attributes.dxf"
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            DisplayText2 text = Assert.IsType<DisplayText2>(Assert.Single(list.Items, i => i is DisplayText2));
            Assert.Equal("VALUE", text.Text);
            Assert.Equal("B1", text.Handle);
            Assert.Equal(4, list.Items.Count(i => i.Handle == "B1" && !(i is DisplayText2)));
            Assert.Equal(4, list.Items.Count(i => i.Handle == "B4"));
        }

        [Fact]
        public void AnInsertInsideABlock_IsDrawnWithTheOtherEntitiesOfItsBlock()
        {
            string outer = Block("OUTER", Insert("PLATE", 10, 0, 1.0, 45.0, "0"), Line(0, 0, 0, 20, "0"));

            CadDocument document = CadDocument.From(
                Drawing(
                    Layer("Steel", 1),
                    PlateBlock() + outer,
                    Insert("OUTER", 0, 0, 1.0, 0.0, "Steel", handle: "C1"),
                    Insert("OUTER", 50, 50, 2.0, 90.0, "Steel", handle: "C2")
                ),
                "nested.dxf"
            );
            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            // Each OUTER is the four entities of its inner PLATE and its own line.
            Assert.Equal(10, list.Count);
            Assert.Equal(5, list.Items.Count(i => i.Handle == "C1"));
            Assert.Equal(5, list.Items.Count(i => i.Handle == "C2"));
        }
    }
}
