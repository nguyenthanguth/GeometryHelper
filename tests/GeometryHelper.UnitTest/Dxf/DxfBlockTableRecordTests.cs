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
    /// The details of a block: its description, units, explodability, scaling and preview, its INSERTs and anonymous blocks,
    /// renaming it with its INSERTs, erasing it when nothing inserts it, and entities moved into it; ezdxf 1.4.5 read the
    /// probe's BOLT_M20 and GH_KEEP back, its recover and audit finding nothing.
    /// </summary>
    public class DxfBlockTableRecordTests
    {
        [Fact]
        public void ABlocksDescriptionUnitsExplodabilityAndScaling_AreItsBlockAndRecordsGroups_SetAndReadBack()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];

            Assert.Equal(string.Empty, bolt.Comments);
            Assert.Equal(DxfUnits.Undefined, bolt.Units);
            Assert.True(bolt.Explodable);
            Assert.Equal(DxfBlockScaling.Any, bolt.BlockScaling);
            Assert.False(bolt.HasPreviewIcon);

            foreach (DxfBlockTableRecord block in db.BlockTable)
            {
                block.Comments = block.Comments;
                block.Units = block.Units;
                block.Explodable = block.Explodable;
                block.BlockScaling = block.BlockScaling;
                block.PreviewIcon = block.PreviewIcon;
                block.Name = block.Name;
            }

            Assert.Equal(text, db.ToText());

            bolt.Comments = "M20 bolt";
            bolt.Units = DxfUnits.Millimeters;
            bolt.Explodable = false;
            bolt.BlockScaling = DxfBlockScaling.Uniform;
            byte[] picture = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();
            bolt.PreviewIcon = picture;

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, bolt.Handle, "33");
            string record = Records(saved).Single(r => HandleOf(r) == bolt.Handle);
            Assert.Contains(" 70\n4\n280\n0\n281\n1\n310\n", record, StringComparison.Ordinal);
            Assert.Equal(3, record.Split('\n').Count(l => l == "310"));
            Assert.Contains("  1\n\n  4\nM20 bolt\n", Records(saved).Single(r => HandleOf(r) == "33"), StringComparison.Ordinal);
            DxfBlockTableRecord read = DxfDatabase.Parse(saved).BlockTable["BOLT"];
            Assert.Equal("M20 bolt", read.Comments);
            Assert.Equal(DxfUnits.Millimeters, read.Units);
            Assert.False(read.Explodable);
            Assert.Equal(DxfBlockScaling.Uniform, read.BlockScaling);
            Assert.Equal(picture, read.PreviewIcon);

            bolt.PreviewIcon = null;
            bolt.Comments = string.Empty;
            Assert.False(bolt.HasPreviewIcon);
            Assert.DoesNotContain("  4\n", Records(db.ToText()).Single(r => HandleOf(r) == "33"), StringComparison.Ordinal);

            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            Assert.Throws<InvalidOperationException>(() => r2000.BlockTable["BOLT"].Units = DxfUnits.Meters);
            Assert.Throws<InvalidOperationException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).BlockTable["BOLT"].Comments = "x");
            Assert.Throws<ArgumentException>(() => r2000.BlockTable.Add(new DxfBlockTableRecord { Name = "GH_MM", Units = DxfUnits.Millimeters }));
        }

        [Fact]
        public void ABlockMadeInCode_TakesItsDescriptionUnitsAndScaling_IntoTheFile()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var made = new DxfBlockTableRecord { Name = "GH_PLATE", Comments = "a plate", Units = DxfUnits.Meters, Explodable = false, BlockScaling = DxfBlockScaling.Uniform };

            db.BlockTable.Add(made);

            DxfBlockTableRecord read = DxfDatabase.Parse(db.ToText()).BlockTable["GH_PLATE"];
            Assert.Equal("a plate", read.Comments);
            Assert.Equal(DxfUnits.Meters, read.Units);
            Assert.False(read.Explodable);
            Assert.Equal(DxfBlockScaling.Uniform, read.BlockScaling);
        }

        [Fact]
        public void TheInsertsOfABlock_AreItsBlockReferences_DirectAndThroughTheBlocksThatHoldThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];

            // The INSERT 65 and the MINSERT 6A of model space, the INSERT 3C of FRAME and 9A of the layout's block *Paper_Space0;
            // then the INSERT 68 of FRAME.
            Assert.Equal(new[] { "65", "6A", "3C", "9A" }, bolt.GetBlockReferences(true).Select(r => r.Handle));
            Assert.Equal(new[] { "65", "68", "6A", "3C", "9A" }, bolt.GetBlockReferences(false).Select(r => r.Handle));
            Assert.Empty(new DxfBlockTableRecord { Name = "x" }.GetBlockReferences(true));
            Assert.Empty(bolt.GetAnonymousBlocks());
            Assert.False(bolt.IsDynamicBlock);
        }

        [Fact]
        public void TheAnonymousBlocksOfADynamicBlock_AreThoseThatNameItAsTheBlockTheyRepresent()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];
            bolt.XData = new[] { new DxfTypedValue(1001, "AcDbDynamicBlockGUID"), new DxfTypedValue(1000, "{3C8E1F3B-7D44-4C93-9F53-5E1C3C2B1A10}") };
            DxfBlockTableRecord dimension = db.BlockTable["*D1"];
            dimension.XData = new[] { new DxfTypedValue(1001, "AcDbBlockRepBTag"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1005, bolt.Handle) };

            Assert.True(bolt.IsDynamicBlock);
            Assert.Equal(new[] { dimension }, bolt.GetAnonymousBlocks());
            Assert.Empty(db.BlockTable["FRAME"].GetAnonymousBlocks());
        }

        [Fact]
        public void ABlockRenamed_TakesItsInsertsAlong_EveryOtherRecordKeepingItsBytes()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];
            DxfBlockReference insert = ByHandle<DxfBlockReference>(db, "65");

            bolt.Name = "BOLT_M20";

            Assert.Equal("BOLT_M20", insert.Name);
            Assert.Same(bolt, insert.BlockTableRecord);
            Assert.Same(bolt, db.BlockTable["bolt_m20"]);
            Assert.False(db.BlockTable.Has("BOLT"));
            Assert.Equal("BOLT_M20", bolt.Single(e => e is DxfLine).BlockName);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, bolt.Handle, "33", "65", "6A", "3C", "9A");
            DxfDocument read = DxfReader.Parse(saved, new DxfReadOptions { Tolerance = Tolerance.Default });
            Assert.Contains(read.Blocks, b => b.Name == "BOLT_M20");
            Assert.Equal(read.Shapes.Count, DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tolerance.Default }).Shapes.Count);

            Assert.Throws<ArgumentException>(() => bolt.Name = "frame");
            Assert.Throws<InvalidOperationException>(() => db.BlockTable["*D1"].Name = "D1");
            Assert.Throws<InvalidOperationException>(() => db.ModelSpace.Name = "Model");
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            r12.BlockTable["BOLT"].Name = "BOLT_M20";
            Assert.Contains(DxfReader.Parse(r12.ToText(), new DxfReadOptions { Tolerance = Tolerance.Default }).Blocks, b => b.Name == "BOLT_M20");
        }

        [Fact]
        public void ABlockErased_IsItsRecordsTakenOut_WhereNothingInsertsIt()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var spare = new DxfBlockTableRecord { Name = "GH_SPARE", Origin = new GeoPoint3(1, 2, 0) };
                db.BlockTable.Add(spare);
                var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0));
                spare.AppendEntity(line);

                Assert.Throws<InvalidOperationException>(() => db.BlockTable["BOLT"].Erase());
                Assert.Throws<InvalidOperationException>(() => db.ModelSpace.Erase());
                spare.Erase();

                Assert.True(spare.IsErased);
                Assert.True(line.IsErased);
                Assert.False(db.BlockTable.Has("GH_SPARE"));
                Assert.DoesNotContain(db.BlockTable, b => b.Name == "GH_SPARE");
                OnlyTheseChanged(text, db.ToText());
                Assert.Throws<InvalidOperationException>(() => spare.Erase());
            }

            DxfDatabase read = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfBlockTableRecord frame = read.BlockTable["FRAME"];
            ByHandle<DxfBlockReference>(read, "68").Erase();
            frame.Erase();
            Assert.DoesNotContain(DxfReader.Parse(read.ToText(), new DxfReadOptions { Tolerance = Tolerance.Default }).Blocks, b => b.Name == "FRAME");
            Assert.Throws<InvalidOperationException>(() => read.BlockTable["BOLT"].Erase());
        }

        [Fact]
        public void TheLinkedColumnsOfAnMText_AreNotEntitiesOfTheBlock_AsAutoCADHasThemPartOfTheirMText()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            int before = db.ModelSpace.Count;
            var main = new DxfMText();
            var second = new DxfMText();
            var third = new DxfMText();
            db.ModelSpace.AppendEntity(main);
            db.ModelSpace.AppendEntity(second);
            db.ModelSpace.AppendEntity(third);

            // As ezdxf writes the columns of an MTEXT before R2018: the main one lists the others in ACAD_MTEXT_COLUMNS.
            main.XData = new[]
            {
                new DxfTypedValue(1001, "ACAD"), new DxfTypedValue(1000, "ACAD_MTEXT_COLUMNS_BEGIN"), new DxfTypedValue(1070, "47"),
                new DxfTypedValue(1070, "3"), new DxfTypedValue(1005, second.Handle), new DxfTypedValue(1005, third.Handle),
                new DxfTypedValue(1000, "ACAD_MTEXT_COLUMNS_END"),
            };

            Assert.Equal(before + 1, db.ModelSpace.Count);
            Assert.Equal(new[] { main }, db.ModelSpace.OfType<DxfMText>().Where(m => m.Handle == main.Handle || m.Handle == second.Handle || m.Handle == third.Handle));

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(before + 1, again.ModelSpace.Count);
            Assert.DoesNotContain(again.ModelSpace, e => e.Handle == second.Handle || e.Handle == third.Handle);

            // An MTEXT erased takes its columns with it, as AutoCAD erases one text (DxfMText.Erase).
            main.Erase();
            Assert.Equal(before, db.ModelSpace.Count);
            Assert.True(second.IsErased && third.IsErased);
        }

        [Fact]
        public void EntitiesMovedIntoABlock_KeepTheirHandles_AndAreOwnedByIt()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var block = new DxfBlockTableRecord { Name = "GH_KEEP", Origin = new GeoPoint3(1, 2, 0) };
            db.BlockTable.Add(block);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            DxfBlockReference frame = ByHandle<DxfBlockReference>(db, "68");

            block.AssumeOwnershipOf(new DxfEntity[] { line, frame });

            Assert.Equal("3E", line.Handle);
            Assert.Same(block, line.Owner);
            Assert.Equal(block.Handle, line.OwnerHandle);
            Assert.Equal(new[] { "3E", "68" }, block.Select(e => e.Handle));
            Assert.DoesNotContain(db.ModelSpace, e => e.Handle == "3E");
            line.EndPoint = new GeoPoint3(1, 1, 1);

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { "3E", "68" }, again.BlockTable["GH_KEEP"].Select(e => e.Handle));
            Assert.Equal(Bits(new GeoPoint3(1, 1, 1)), Bits(ByHandle<DxfLine>(again, "3E").EndPoint));

            // Back into paper space: its group 67 written, as AutoCAD writes an entity of paper space.
            db.PaperSpace.AssumeOwnershipOf(new[] { line });
            Assert.Contains("100\nAcDbEntity\n 67\n1\n", Records(db.ToText()).Single(r => HandleOf(r) == "3E"), StringComparison.Ordinal);

            DxfBlockReference bolt = ByHandle<DxfBlockReference>(db, "65");
            Assert.Throws<ArgumentException>(() => db.BlockTable["BOLT"].AssumeOwnershipOf(new[] { bolt }));
            Assert.Throws<ArgumentException>(() => block.AssumeOwnershipOf(new[] { new DxfLine() }));
            Assert.Throws<ArgumentNullException>(() => block.AssumeOwnershipOf(null));
        }

        [Fact]
        public void ABlockMadeInCodeAndInsertedInCode_IsRenamedWithItsInsert_AndNotErased()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var block = new DxfBlockTableRecord { Name = "GH_NEW" };
                db.BlockTable.Add(block);
                block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
                var insert = new DxfBlockReference(new GeoPoint3(5, 5, 0), block);
                db.ModelSpace.AppendEntity(insert);

                Assert.Equal(new[] { insert.Handle }, block.GetBlockReferences(true).Select(r => r.Handle));
                block.Name = "GH_NEW2";
                Assert.Equal("GH_NEW2", insert.Name);
                Assert.Throws<InvalidOperationException>(() => block.Erase());
                Assert.Equal("GH_NEW2", ByHandle<DxfBlockReference>(DxfDatabase.Parse(db.ToText()), insert.Handle).Name);
            }
        }

        [Fact]
        public void AnMTextListingItsOwnHandleAmongItsColumns_IsStillAnEntityOfTheBlock_AndEveryColumnIsFoundByHandle()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            int before = db.ModelSpace.Count;
            var main = new DxfMText();
            var second = new DxfMText();
            db.ModelSpace.AppendEntity(main);
            db.ModelSpace.AppendEntity(second);

            main.XData = new[]
            {
                new DxfTypedValue(1001, "ACAD"), new DxfTypedValue(1000, "ACAD_MTEXT_COLUMNS_BEGIN"), new DxfTypedValue(1070, "47"),
                new DxfTypedValue(1070, "2"), new DxfTypedValue(1005, main.Handle), new DxfTypedValue(1005, second.Handle),
                new DxfTypedValue(1000, "ACAD_MTEXT_COLUMNS_END"),
            };

            Assert.Equal(before + 1, db.ModelSpace.Count);
            Assert.Contains(db.ModelSpace, e => e.Handle == main.Handle);
            Assert.DoesNotContain(db.ModelSpace, e => e.Handle == second.Handle);
            Assert.Equal(second.Handle, db.ModelSpace.EntityByHandle(second.Handle).Handle);
            Assert.Equal(before + 2, db.ModelSpace.AllEntities().Count());
            Assert.Null(db.ModelSpace.EntityByHandle("FFFFF"));
        }

        [Fact]
        public void AnEntityMovedToAnotherBlock_LeavesTheDrawOrderOfItsBlock_AndJoinsTheOneOfTheBlockItEnters()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase read = DxfDatabase.Parse(text);
            string space = read.ModelSpace.Handle;
            Assert.DoesNotContain("\n  5\n9B\n", text, StringComparison.Ordinal);

            // A draw order of model space as AutoCAD writes one: the LINE 3E then the CIRCLE 3F, each by its sort handle.
            string sorts = "  0\nSORTENTSTABLE\n  5\n9B\n330\n" + space + "\n100\nAcDbSortentsTable\n330\n" + space
                + "\n331\n3E\n  5\n3E\n331\n3F\n  5\n3F\n";
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(text.Substring(0, end) + sorts + text.Substring(end));
            var block = new DxfBlockTableRecord { Name = "GH_KEEP" };
            db.BlockTable.Add(block);

            block.AssumeOwnershipOf(new DxfEntity[] { ByHandle<DxfLine>(db, "3E") });
            Assert.Equal("  0\nSORTENTSTABLE\n  5\n9B\n330\n" + space + "\n100\nAcDbSortentsTable\n330\n" + space + "\n331\n3F\n  5\n3F\n",
                Records(db.ToText()).Single(r => HandleOf(r) == "9B"));

            db.ModelSpace.AssumeOwnershipOf(new DxfEntity[] { ByHandle<DxfLine>(db, "3E") });
            Assert.Equal("  0\nSORTENTSTABLE\n  5\n9B\n330\n" + space + "\n100\nAcDbSortentsTable\n330\n" + space
                + "\n331\n3F\n  5\n3F\n331\n3E\n  5\n3E\n", Records(db.ToText()).Single(r => HandleOf(r) == "9B"));
        }
    }
}
