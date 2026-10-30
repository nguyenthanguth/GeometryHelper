using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A text's and an MTEXT's text style set by its name or as its record, as AutoCAD's <c>TextStyleName</c> and
    /// <c>TextStyleId</c>; an MTEXT's width set with its columns, as AutoCAD and ezdxf set it; and an object found by its handle,
    /// as AutoCAD's <c>GetObjectId(handle)</c>, in <see cref="DxfFileEzdxfDrawings"/>.
    /// </summary>
    public class DxfTextStyleAndObjectTests
    {
        [Fact]
        public void ATextsStyle_IsSetByItsNameOrItsRecord_WrittenAsTheTableNamesIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var note = new DxfTextStyleTableRecord { Name = "GH_Note" };
            db.TextStyleTable.Add(note);
            DxfDBText text = ByHandle<DxfDBText>(db, "64");
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            DxfAttributeDefinition mark = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();

            text.TextStyleName = "gh_note";
            mtext.TextStyle = note;
            mark.TextStyle = note;

            Assert.Equal("GH_Note", text.TextStyleName);
            Assert.Same(note, text.TextStyle);
            Assert.Same(note, mtext.TextStyle);
            string saved = db.ToText();
            Assert.Contains("\n  7\nGH_Note\n", Records(saved).Single(r => HandleOf(r) == "64"), StringComparison.Ordinal);
            Assert.Contains("\n  7\nGH_Note\n", Records(saved).Single(r => HandleOf(r) == "73"), StringComparison.Ordinal);
            DxfDatabase read = DxfDatabase.Parse(saved);
            Assert.Equal("GH_Note", read.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single().TextStyleName);
            Assert.Equal("GH_Note", ByHandle<DxfMText>(read, "73").TextStyleName);

            // The style it has changes nothing, by its name in other letters or by its record.
            text.TextStyleName = "GH_NOTE";
            text.TextStyle = note;
            mtext.TextStyleName = "GH_Note";
            Assert.Equal(saved, db.ToText());
        }

        [Fact]
        public void AStyleTheDrawingHasNot_OrOfAnotherDrawing_IsRefused_AndOneInNoDrawingIsCheckedWhenAppended()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDatabase other = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var theirs = new DxfTextStyleTableRecord { Name = "GH_Theirs" };
            other.TextStyleTable.Add(theirs);
            DxfDBText text = ByHandle<DxfDBText>(db, "64");
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => text.TextStyleName = "GH_NONE");
            Assert.Throws<ArgumentException>(() => text.TextStyle = theirs);
            Assert.Throws<ArgumentException>(() => ByHandle<DxfMText>(db, "73").TextStyle = other.TextStyleTable["Standard"]);
            Assert.Throws<ArgumentException>(() => text.TextStyle = new DxfTextStyleTableRecord { Name = "Loose" });
            Assert.Throws<ArgumentNullException>(() => text.TextStyle = null);
            Assert.Throws<ArgumentNullException>(() => text.TextStyleName = null);
            Assert.Equal(before, db.ToText());

            // A text in no drawing takes any name, and a record of any drawing; the drawing it is added to checks it.
            var made = new DxfDBText { TextString = "B-12", TextStyle = theirs };
            Assert.Equal("GH_Theirs", made.TextStyleName);
            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(made));
            other.ModelSpace.AppendEntity(made);
            Assert.Same(theirs, made.TextStyle);
        }

        [Fact]
        public void AnMTextOrAnAttributeOfAStyleTheDrawingHasNot_IsRefusedWhenAdded_AsATextIs()
        {
            foreach (string drawing in new[] { DxfFileEzdxfDrawings.R2018, DxfFileEzdxfDrawings.R12 })
            {
                DxfDatabase db = DxfDatabase.Parse(drawing);
                string before = db.ToText();
                var text = new DxfDBText { TextString = "x", TextStyleName = "NOPE" };
                var mtext = new DxfMText { Contents = "y", TextStyleName = "NOPE" };
                var mark = new DxfAttributeReference(new GeoPoint3(10, 19, 0), "B-99", "MARK", null) { TextStyleName = "NOPE" };
                var made = new DxfBlockReference(new GeoPoint3(10, 20, 0), db.BlockTable["BOLT"]);
                made.AttributeCollection.AppendAttribute(new DxfAttributeReference(new GeoPoint3(10, 19, 0), "B-98", "MARK", null) { TextStyleName = "NOPE" });

                // The same words whichever way it is added, the argument named that of the method called.
                DxfBlockReference bolt = db.ModelSpace.OfType<DxfBlockReference>().First(b => b.Name == "BOLT");
                Refused("entity", () => db.ModelSpace.AppendEntity(text));
                Refused("entity", () => db.ModelSpace.AppendEntity(mtext));
                Refused("attribute", () => bolt.AttributeCollection.AppendAttribute(mark));
                Refused("entity", () => db.ModelSpace.AppendEntity(made));
                Assert.False(mtext.IsResident);
                Assert.Equal(before, db.ToText());

                // A style the drawing has is taken, by its name in other letters.
                mtext.TextStyleName = "standard";
                db.ModelSpace.AppendEntity(mtext);
            }
        }

        private static void Refused(string argument, Action adding)
        {
            ArgumentException refused = Assert.Throws<ArgumentException>(adding);
            Assert.StartsWith("The text style NOPE is not in the file's text style table.", refused.Message, StringComparison.Ordinal);
            Assert.Equal(argument, refused.ParamName);
        }

        [Fact]
        public void ColumnsSet_GiveTheMTextTheWidthAutoCadGivesIt_OfAllTheColumnsFromR2018_AndOfAColumnBefore()
        {
            // As AutoCAD 2021 wrote the columns of 09-a (point 9): 41 of R2018 that of all the columns with their gutters.
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                bool r2018 = text == DxfFileEzdxfDrawings.R2018;
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMText mtext = ByHandle<DxfMText>(db, "73");

                mtext.SetStaticColumns(12.5, 2.5, 3);

                Assert.Equal(r2018 ? 42.5 : 12.5, mtext.Width);
                DxfMText read = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
                Assert.Equal(r2018 ? 42.5 : 12.5, read.Width);
                Assert.Equal(12.5, read.ColumnWidth);

                if (r2018)
                {
                    Assert.Contains("101\nEmbedded Object\n 70\n1\n 10\n1\n 20\n0\n 30\n0\n 11\n20\n 21\n40\n 31\n0\n 40\n42.5\n", Records(db.ToText()).Single(r => HandleOf(r) == "73"), StringComparison.Ordinal);
                }

                mtext.SetDynamicColumns(10.0, 2.0, true);
                Assert.Equal(r2018 ? 34.0 : 10.0, mtext.Width);
                mtext.ColumnWidth = 8.0;
                Assert.Equal(r2018 ? 28.0 : 8.0, ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").Width);
            }
        }

        [Fact]
        public void AnObject_IsFoundByItsHandle_WhateverItIs()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            Assert.Same(ByHandle<DxfDBText>(db, "64"), db.GetObject("64"));
            Assert.Same(db.LayerTable["steel"], db.GetObject(db.LayerTable["steel"].Handle.ToLowerInvariant()));
            Assert.Same(db.BlockTable["BOLT"], db.GetObject(db.BlockTable["BOLT"].Handle));
            Assert.Same(db.LayerTable, db.GetObject(db.LayerTable.Handle));
            Assert.Same(db.NamedObjectsDictionary, db.GetObject(db.NamedObjectsDictionary.Handle));
            Assert.IsType<DxfPolylineVertex3d>(db.GetObject("47"));
            Assert.IsType<DxfAttributeReference>(db.GetObject(ByHandle<DxfBlockReference>(db, "65").AttributeCollection.Cast<DxfAttributeReference>().First().Handle));

            Assert.False(db.TryGetObject("FFFFF", out DxfDBObject none));
            Assert.Null(none);
            Assert.False(db.TryGetObject("not a handle", out _));
            Assert.Throws<KeyNotFoundException>(() => db.GetObject("FFFFF"));
            Assert.Throws<ArgumentNullException>(() => db.TryGetObject(null, out _));

            ByHandle<DxfDBText>(db, "64").Erase();
            Assert.False(db.TryGetObject("64", out _));
        }

        [Fact]
        public void AnObjectAddedAfterAQuestion_IsFoundByItsHandle_AndAnApplicationIsNot()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Assert.False(db.TryGetObject("FFFFF", out _));

            var layer = new DxfLayerTableRecord { Name = "GH_Spare" };
            db.LayerTable.Add(layer);
            DxfBlockReference bolt = ByHandle<DxfBlockReference>(db, "65");
            var mark = new DxfAttributeReference(new GeoPoint3(10, 19, 0), "B-99", "MARK", null);
            bolt.AttributeCollection.AppendAttribute(mark);

            Assert.Same(layer, db.GetObject(layer.Handle));
            Assert.Same(mark, db.GetObject(mark.Handle));

            // The table of applications has no class of the library, and is not found.
            string applications = HandleOf(Records(db.ToText()).First(r => r.StartsWith("  0\nTABLE\n  2\nAPPID\n", StringComparison.Ordinal)));
            Assert.False(db.TryGetObject(applications, out _));
        }

        [Fact]
        public void AnErasedRecordOrObject_IsNotFound_AndGivesNull()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var layer = new DxfLayerTableRecord { Name = "GH_Spare" };
            db.LayerTable.Add(layer);
            var group = new DxfGroup("GH_Group", true);
            db.GroupDictionary.SetAt("GH_GROUP", group);
            string[] handles = { layer.Handle, group.Handle };
            Assert.All(handles, h => Assert.NotNull(db.GetObject(h)));

            layer.Erase();
            group.Erase();

            foreach (string handle in handles)
            {
                Assert.False(db.TryGetObject(handle, out DxfDBObject found));
                Assert.Null(found);
            }
        }
    }
}
