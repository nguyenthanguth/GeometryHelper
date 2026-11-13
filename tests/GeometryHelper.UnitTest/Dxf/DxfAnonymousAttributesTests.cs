using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The attributes of the INSERTs of a dynamic block when its anonymous blocks are updated, as AutoCAD 2021's
    /// <c>UpdateAnonymousBlocks</c> left them (a side database of the panel below, probed in its console on 13 November): an
    /// INSERT that places a *U block is turned to a new *U block holding the new definition, and its ATTRIBs stay where they
    /// were, none made for an ATTDEF that is new, and take the height of their ATTDEF where that changed (TAG 2.5 to 5); an
    /// INSERT that places the block itself is left as it was.
    /// </summary>
    public class DxfAnonymousAttributesTests
    {
        private static string Round(GeoPoint3 p) =>
            "("
            + Math.Round(p.X, 6).ToString(CultureInfo.InvariantCulture)
            + ","
            + Math.Round(p.Y, 6).ToString(CultureInfo.InvariantCulture)
            + ")";

        /// <summary>
        /// GH_PANEL, a rectangle 100 by 50 whose Width stretches its right side and the TAG ATTDEF there (locked, at (95, 20)),
        /// the NOTE ATTDEF at (50, 30), and two INSERTs with their ATTRIBs: the first of Width 150, the second as it is.
        /// </summary>
        private static DxfDatabase Panel(out DxfBlockReference stretched, out DxfBlockReference plain)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine[] lines =
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            var tag = new DxfAttributeDefinition(new GeoPoint3(95, 20, 0), "T1", "TAG", "Tag", null);
            block.AppendEntity(tag);
            tag.LockPositionInBlock = true;
            block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(50, 30, 0), "N1", "NOTE", "Note", null));
            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
            );
            block.AddStretchAction(
                width,
                new GeoPoint2(90, -10),
                new GeoPoint2(110, 60),
                lines.Take(3).Cast<DxfEntity>().Concat(new DxfEntity[] { tag })
            );
            var inserts = new List<DxfBlockReference>();

            foreach (GeoPoint3 at in new[] { new GeoPoint3(1000, 500, 0), new GeoPoint3(2000, 500, 0) })
            {
                var insert = new DxfBlockReference(at, block);
                db.ModelSpace.AppendEntity(insert);
                inserts.Add(insert);

                foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>())
                {
                    var attribute = new DxfAttributeReference();
                    attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                    attribute.TextString = definition.Tag.ToLowerInvariant() + (at.X > 1500 ? "2" : "1");
                    insert.AttributeCollection.AppendAttribute(attribute);
                }
            }

            inserts[0].DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Width").Value = 150.0;
            stretched = inserts[0];
            plain = inserts[1];
            return db;
        }

        private static List<string> Attributes(DxfBlockReference insert) =>
            insert
                .AttributeCollection.Select(a =>
                    a.Tag
                    + "="
                    + a.TextString
                    + " "
                    + Round(a.Position)
                    + " h"
                    + a.Height.ToString(CultureInfo.InvariantCulture)
                )
                .ToList();

        private static List<string> Definitions(DxfBlockTableRecord block) =>
            block
                .OfType<DxfAttributeDefinition>()
                .Select(d =>
                    d.Tag
                    + " "
                    + Round(d.Position)
                    + " h"
                    + d.Height.ToString(CultureInfo.InvariantCulture)
                    + (d.LockPositionInBlock ? " locked" : string.Empty)
                )
                .ToList();

        [Fact]
        public void UpdateAnonymous_LeavesTheAttributesWhereTheyAre_AndGivesThemTheHeightOfTheirDefinition()
        {
            DxfDatabase db = Panel(out DxfBlockReference stretched, out DxfBlockReference plain);
            Assert.Equal(new[] { "TAG=tag1 (1145,520) h2.5", "NOTE=note1 (1050,530) h2.5" }, Attributes(stretched));
            string before = stretched.Name;
            DxfBlockTableRecord dynamic = db.BlockTable["GH_PANEL"];

            // The Block Editor's edit: TAG made 5 high, NOTE moved to (10, 40), an ATTDEF NEW and a circle added.
            List<DxfAttributeDefinition> definitions = dynamic.OfType<DxfAttributeDefinition>().ToList();
            definitions[0].Height = 5.0;
            definitions[1].Position = new GeoPoint3(10, 40, 0);
            dynamic.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(60, 25, 0), "N2", "NEW", "New", null));
            dynamic.AppendEntity(new DxfCircle(new GeoPoint3(50, 25, 0), new GeoVector3(0, 0, 1), 10));

            List<string> made = DxfDynamicBlockRepresentation.UpdateAnonymous(dynamic);

            Assert.Single(made);
            Assert.NotEqual(before, stretched.Name);
            Assert.Equal(made[0], stretched.Name);
            Assert.Equal(new[] { "TAG=tag1 (1145,520) h5", "NOTE=note1 (1050,530) h2.5" }, Attributes(stretched));
            Assert.Equal(
                new[] { "TAG (145,20) h5 locked", "NOTE (10,40) h2.5", "NEW (60,25) h2.5" },
                Definitions(db.BlockTable[stretched.Name])
            );
            Assert.Equal("GH_PANEL", plain.Name);
            Assert.Equal(new[] { "TAG=tag2 (2095,520) h2.5", "NOTE=note2 (2050,530) h2.5" }, Attributes(plain));
        }

        /// <summary>
        /// An ATTRIB that follows its ATTDEF is made anew from the definition as the new *U block holds it, whatever the user gave
        /// it (a height 4, a turn of 1 radian, a place of (1100, 510), colour 5 are the definition's again, here (1140, 515)
        /// for the TAG ATTDEF moved to (90, 15), 5 high and turned 0.3) and keeps its words; the NOTE ATTRIB, whose ATTDEF no
        /// action works on, keeps what the user gave it.
        /// </summary>
        [Fact]
        public void UpdateAnonymous_MakesABoundAttributeAnewFromItsDefinition_KeepingItsWords_AndLeavesTheOthers()
        {
            DxfDatabase db = Panel(out DxfBlockReference stretched, out _);
            DxfBlockTableRecord dynamic = db.BlockTable["GH_PANEL"];
            DxfAttributeReference tag = stretched.AttributeCollection.Single(a => a.Tag == "TAG");
            DxfAttributeReference note = stretched.AttributeCollection.Single(a => a.Tag == "NOTE");
            tag.TextString = "custom";
            tag.Height = 4.0;
            tag.Rotation = 1.0;
            tag.Position = new GeoPoint3(1100, 510, 0);
            tag.ColorIndex = 5;
            note.Height = 4.0;
            note.ColorIndex = 5;

            DxfAttributeDefinition definition = dynamic.OfType<DxfAttributeDefinition>().First(d => d.Tag == "TAG");
            definition.Height = 5.0;
            definition.Rotation = 0.3;
            definition.Position = new GeoPoint3(90, 15, 0);

            DxfDynamicBlockRepresentation.UpdateAnonymous(dynamic);

            Assert.Equal("custom", tag.TextString);
            Assert.Equal(5.0, tag.Height, 12);
            Assert.Equal(0.3, tag.Rotation, 12);
            Assert.Equal(256, tag.ColorIndex);
            Near(new GeoPoint3(1140, 515, 0), tag.Position);
            Assert.Equal(4.0, note.Height, 12);
            Assert.Equal(5, note.ColorIndex);
            Near(new GeoPoint3(1050, 530, 0), note.Position);
        }

        /// <summary>An ATTDEF erased in the block takes no ATTRIB away and leaves its ATTRIB as the user had it, as AutoCAD 2021 left it.</summary>
        [Fact]
        public void UpdateAnonymous_OfAnErasedDefinition_LeavesItsAttributeAsItWas()
        {
            DxfDatabase db = Panel(out DxfBlockReference stretched, out _);
            DxfBlockTableRecord dynamic = db.BlockTable["GH_PANEL"];
            DxfAttributeReference tag = stretched.AttributeCollection.Single(a => a.Tag == "TAG");
            tag.Position = new GeoPoint3(1100, 510, 0);
            dynamic.OfType<DxfAttributeDefinition>().First(d => d.Tag == "TAG").Erase();

            DxfDynamicBlockRepresentation.UpdateAnonymous(dynamic);

            Assert.Equal(2, stretched.AttributeCollection.Count);
            Near(new GeoPoint3(1100, 510, 0), tag.Position);
            Assert.Equal(2.5, tag.Height, 12);
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.Equal(expected.X, actual.X, 6);
            Assert.Equal(expected.Y, actual.Y, 6);
            Assert.Equal(expected.Z, actual.Z, 6);
        }
    }
}
