using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of dxfver12, held to give what it gave before: the completed copy of a drawing opened from the bytes it saves as
    /// and its tables found in its records, cells written apart from the record and put in it once it is read, and the setters
    /// of a format, a kind of value, a fill, overrides and a reset on the path of one cell. The speed is held by the bytes
    /// allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed12Tests
    {
        #region The saved bytes

        private static readonly string[] RunsLines =
        {
            "AC1032 seed 1 655BDB830397D7445B19DF82 C3FED5C5EFEC69957695FC08",
            "AC1032 seed 1 again 477C36C01590B1F98B65C0B4 245ECDA1C470888D885ACC1F",
            "AC1032 seed 2 1366B91303825B2DB3E27F42 68A66BC76684C198F0AD5B8C",
            "AC1032 seed 2 again 09F3E843B6CAADF13F5129FA 245ECDA1C470888D885ACC1F",
            "AC1032 seed 3 C1B59AD1FCC7320B35FDB4BD 6842F3CA7827C3B2D18D3754",
            "AC1032 seed 3 again 071C2FDE5AD3E9EF4E776102 245ECDA1C470888D885ACC1F",
            "AC1027 seed 1 D56BD6B43D99EB0C8F976FDA D56BD6B43D99EB0C8F976FDA",
            "AC1027 seed 1 again 522C3F811F50E927ADF6FCB9 245ECDA1C470888D885ACC1F",
            "AC1027 seed 2 B6EA84AAB093335867DDD11A B6EA84AAB093335867DDD11A",
            "AC1027 seed 2 again 0B2E1008D7BE7E6E0016099D 245ECDA1C470888D885ACC1F",
            "AC1027 seed 3 584C8CBB4F30DBA9228640CE 584C8CBB4F30DBA9228640CE",
            "AC1027 seed 3 again 57DA26172053D3392BE855AA 245ECDA1C470888D885ACC1F",
            "AC1021 seed 1 C880FD3E8C6F4848318E326F 2A178B6C87F5F2CDD682902B",
            "AC1021 seed 1 again 0CC2EE9C96D7EA8C0AFAC71B 245ECDA1C470888D885ACC1F",
            "AC1021 seed 2 58C4840CC99158A7276518C0 41E0EB9C21D9764705B373AD",
            "AC1021 seed 2 again EC8FB939A798E040FAE64D98 245ECDA1C470888D885ACC1F",
            "AC1021 seed 3 9D97CCFFAF5F738C7EB43366 8AF63DC3B6BBBEC0AD12837B",
            "AC1021 seed 3 again 05F15EE58FF7FD44A9F75233 245ECDA1C470888D885ACC1F",
            "AC1018 seed 1 95EC7221E7B6A45ABBACE02B BC67E527B476628C009958A0",
            "AC1018 seed 1 again E3D4C233C20DFED4DA8772E2 0730D2A18A91665A503D5199",
            "AC1018 seed 2 70A139B5B4B85FC442C480C3 4C7F7E6BA038433FA0C64901",
            "AC1018 seed 2 again DE136DD1803C1FF251F9E9FB EDEBCE287B798E095C25E8CF",
            "AC1018 seed 3 7EDF3554D7B0599B42301412 9D627699F8DF48ACBD3A3A66",
            "AC1018 seed 3 again F0214FD9977341822B3ABA2F 127B2FC2CDDE9F10A8B8ECF7",
            "AC1015 seed 1 56899951D9AD0FA92631621A A837C6D4BC668FCFB99AD708",
            "AC1015 seed 1 again 0730D2A18A91665A503D5199 0730D2A18A91665A503D5199",
            "AC1015 seed 2 E1CF726E59B6BAFC38C23FFE 7BFB00FDC297C1C2405E600F",
            "AC1015 seed 2 again EDEBCE287B798E095C25E8CF EDEBCE287B798E095C25E8CF",
            "AC1015 seed 3 367E404943B2D0E6F3D1C8DC 30D684F271B82D5B78142976",
            "AC1015 seed 3 again 127B2FC2CDDE9F10A8B8ECF7 127B2FC2CDDE9F10A8B8ECF7",
        };

        private static readonly string[] PlacedLines =
        {
            "placed 0 8209EB3DB9666C4277BE0E32 0AEDA3BEE42394D6606C31D3",
            "placed 1 2E97A75938F769E15819A048 EB57F7CC7F463E2174634ADF",
            "placed 2 058BBA7757DD6A7208220473 FC7CEBC9AD7BEB0A0CA44661",
            "placed 3 A638FC37DBDC07DD293C05DA 71307DFD7A384CDAA58DC568",
            "placed 4 4BDA959D32D2C627EFA83627 8010FAB214F7C8307ADC7A3A",
            "placed 5 DF85F688B4E8F625CBDF81A9 BCE09B81FAF62A212523B6DF",
        };

        [Fact]
        public void RunsOfEditsOfEveryKindOfSetter_SaveToTheBytesTheyDidBefore_InEveryVersionTheyAreMadeIn()
        {
            Assert.Equal(RunsLines, DxfSpeed12Scenarios.Runs());
        }

        [Fact]
        public void TablesOfModelSpacePaperSpaceAndABlock_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(PlacedLines, DxfSpeed12Scenarios.Placed());
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1027")]
        [InlineData("AC1021")]
        [InlineData("AC1018")]
        public void CellsSetInARun_SaveToTheBytesOfCellsSetWithAReadBetweenEach(string version)
        {
            // A read of the table puts into its record whatever was kept apart from it: the same edits, each read after, are the
            // record written cell by cell, as it was before the cells were kept apart.
            string[] saves = new string[2];

            for (int reads = 0; reads < 2; reads++)
            {
                DxfDatabase db = DxfSpeed12Scenarios.Metric(version);
                var table = new DxfTable(new GeoPoint3(0, 0, 0), 8, 6, 8.0, 30.0);
                db.ModelSpace.AppendEntity(table);
                DxfSpeed12Scenarios.Run(db, table, 250, 7, reads == 1);
                saves[reads] =
                    DxfSpeed12Scenarios.Hash(DxfSpeed12Scenarios.Saved(db))
                    + DxfSpeed12Scenarios.Hash(DxfSpeed12Scenarios.Saved(db, "AC1027"))
                    + db.ToText().Length;
            }

            Assert.Equal(saves[1], saves[0]);
        }

        #endregion

        #region Cells kept apart from the record

        private static DxfTable Filled(DxfDatabase db, int rows, int columns)
        {
            var table = new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    table.SetTextString(r, c, "first " + r + "," + c);
                }
            }

            return table;
        }

        [Fact]
        public void CellsSetOneAfterAnother_AreReadBackAsSet_AndTheOnesNotSetAreAsTheyWere()
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 12, 5);

            for (int r = 0; r < 12; r += 2)
            {
                table.SetTextString(r, 2, "second " + r);
                table.SetAlignment(r, 3, DxfCellAlignment.MiddleCenter);
            }

            for (int r = 0; r < 12; r++)
            {
                Assert.Equal(r % 2 == 0 ? "second " + r : "first " + r + ",2", table.TextString(r, 2));
                Assert.Equal("first " + r + ",1", table.TextString(r, 1));
                Assert.Equal("first " + r + ",4", table.TextString(r, 4));
                Assert.Equal(
                    r % 2 == 0 ? (DxfCellAlignment?)DxfCellAlignment.MiddleCenter : null,
                    table.Alignment(r, 3)
                );
            }
        }

        [Fact]
        public void CellsKeptApart_AreInTheText_TheSavedBytes_AndACopyInAnotherDrawing()
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 6, 4);
            table.SetTextString(2, 1, "KEPT APART ONE");
            table.SetTextString(4, 3, "KEPT APART TWO");

            Assert.Contains("KEPT APART ONE", db.ToText());
            Assert.Contains("KEPT APART TWO", Encoding.UTF8.GetString(DxfSpeed12Scenarios.Saved(db)));

            table.SetTextString(3, 0, "KEPT APART THREE");
            DxfDatabase target = DxfSpeed12Scenarios.Metric();
            IReadOnlyList<DxfEntity> copies = db.WblockCloneObjects(new List<DxfEntity> { table }, target.ModelSpace);

            var copy = (DxfTable)Assert.Single(copies);
            Assert.Equal("KEPT APART THREE", copy.TextString(3, 0));
            Assert.Equal("KEPT APART ONE", copy.TextString(2, 1));
        }

        [Fact]
        public void AnotherChangeBetweenTwoCells_ThroughTheTableOrTheEntity_KeepsEveryCellSet()
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 6, 4);

            table.SetTextString(1, 1, "BEFORE THE LAYER");
            table.Layer = "SCHEDULES";
            table.SetTextString(2, 2, "BEFORE THE ROW");
            table.InsertRows(3, 9.0, 1);
            table.SetTextString(3, 0, "IN THE NEW ROW");
            table.SetTextString(4, 3, "AFTER THE ROW");
            table.TransformBy(GeoTransform3.Translation(new GeoVector3(5.0, 0.0, 0.0)));
            table.SetTextString(5, 3, "AFTER THE MOVE");

            Assert.Equal("BEFORE THE LAYER", table.TextString(1, 1));
            Assert.Equal("BEFORE THE ROW", table.TextString(2, 2));
            Assert.Equal("IN THE NEW ROW", table.TextString(3, 0));
            Assert.Equal("AFTER THE ROW", table.TextString(4, 3));
            Assert.Equal("AFTER THE MOVE", table.TextString(5, 3));
            Assert.Equal("SCHEDULES", table.Layer);
            Assert.Equal(7, table.NumRows);
        }

        [Fact]
        public void ACellOutsideTheTable_IsRefusedAsBefore_WhileCellsAreKeptApart()
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 4, 3);
            table.SetTextString(1, 1, "KEPT");

            Assert.Throws<ArgumentOutOfRangeException>(() => table.SetTextString(4, 0, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.SetFormat(0, 3, "%lu2%pr2"));
            Assert.Equal("KEPT", table.TextString(1, 1));
        }

        [Fact]
        public void TheTextOfEveryCellOf4000Cells_SetOneByOne_AllocatesLessThanAGigabyte()
        {
            // Each cell copied the record, 2.2 GB for these 4 000 cells (13.5 GB for 10 000): the bytes allocated do not vary
            // with the machine as a time does.
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 80, 50, 9.0, 25.0);
            db.ModelSpace.AppendEntity(table);
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int r = 0; r < 80; r++)
            {
                for (int c = 0; c < 50; c++)
                {
                    table.SetTextString(r, c, "cell " + r + "," + c);
                }
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal("cell 79,49", table.TextString(79, 49));
            Assert.Equal("cell 40,25", table.TextString(40, 25));
            Assert.True(used < 1_000_000_000L, "allocated " + used);
        }

        #endregion

        #region The setters of a format, a kind of value, a fill, overrides and a reset

        [Fact]
        public void TheFormatKindFillOverridesAndReset_OfEveryCellOf700Cells_AllocateLessThanAGigabyte()
        {
            // Each setter read and wrote every group of the record: 6.7 GB for the 700 cells' formats, as many for their kinds.
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 50, 15);
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int r = 1; r < 50; r++)
            {
                for (int c = 0; c < 14; c++)
                {
                    table.SetValue(r, c, r * 100 + c + 0.5);
                    table.SetFormat(r, c, "%lu2%pr2");
                    table.SetBackgroundColorNone(r, c, false);

                    if (c % 3 == 0)
                    {
                        table.SetDataType(r, c, DxfDataType.Long, DxfUnitType.Unitless);
                    }
                    else if (c % 3 == 1)
                    {
                        table.SetOverrides(r, c, 0, DxfCellProperties.TextHeight | DxfCellProperties.ContentColor);
                    }
                    else
                    {
                        table.ResetValue(r, c);
                    }
                }
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(DxfDataType.Long, table.DataType(1, 0));
            Assert.Equal(DxfDataType.Double, table.DataType(1, 1));
            Assert.Equal("%lu2%pr2", table.Format(1, 1));
            Assert.Equal(string.Empty, table.TextString(1, 2));
            Assert.True(
                table.GetOverrides(2, 1, 0).HasFlag(DxfCellProperties.TextHeight | DxfCellProperties.ContentColor)
            );
            Assert.True(used < 1_000_000_000L, "allocated " + used);
        }

        #endregion

        #region The completed copy

        private static DxfDatabase WithTablesIn(out DxfTable model, out DxfTable paper, out DxfTable inBlock)
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            var block = new DxfBlockTableRecord { Name = "SCHEDULES" };
            db.BlockTable.Add(block);
            model = new DxfTable(new GeoPoint3(0, 0, 0), 3, 2, 9.0, 30.0);
            paper = new DxfTable(new GeoPoint3(100, 0, 0), 4, 3, 9.0, 30.0);
            inBlock = new DxfTable(new GeoPoint3(200, 0, 0), 5, 2, 9.0, 30.0);
            db.ModelSpace.AppendEntity(model);
            db.PaperSpace.AppendEntity(paper);
            block.AppendEntity(inBlock);
            model.SetTextString(1, 0, "MODEL");
            paper.SetTextString(1, 0, "PAPER");
            inBlock.SetTextString(1, 0, "BLOCK");
            return db;
        }

        /// <summary>How many objects of a type the text holds: those whose group 0 names it.</summary>
        private static int Objects(string text, string type) =>
            Regex.Matches(text, @"(?m)^\s*0\r?\n" + type + @"\r?$").Count;

        [Fact]
        public void TablesOfModelSpacePaperSpaceAndABlock_AreEachGivenTheirTableContent_WhenTheDrawingIsSaved()
        {
            DxfDatabase db = WithTablesIn(out _, out _, out _);

            string saved = Encoding.UTF8.GetString(DxfSpeed12Scenarios.Saved(db));
            DxfDatabase read = DxfDatabase.Parse(saved);

            // Each of the three tables, and the library's own text of the saved drawing's objects (a TABLECONTENT each).
            Assert.Equal(3, Objects(saved, "TABLECONTENT"));
            Assert.Equal(3, Objects(saved, "TABLEGEOMETRY"));
            Assert.Equal("PAPER", read.PaperSpace.OfType<DxfTable>().Single().TextString(1, 0));
            Assert.Equal("MODEL", read.ModelSpace.OfType<DxfTable>().Single().TextString(1, 0));
        }

        [Fact]
        public void TheDrawing_IsLeftAsItWas_ByTheSavingOfItsTables()
        {
            DxfDatabase db = WithTablesIn(out DxfTable model, out _, out _);
            string before = db.ToText();

            byte[] first = DxfSpeed12Scenarios.Saved(db);
            byte[] second = DxfSpeed12Scenarios.Saved(db, "AC1027");

            Assert.Equal(before, db.ToText());
            Assert.Equal(2, model.NumColumns);
            Assert.NotEmpty(first);
            Assert.NotEmpty(second);
        }

        [Fact]
        public void TheTablesOfASavedDrawingRead_AreFoundInItsRecords_WithTheirSpaces_AndATableOfABlockIsNot()
        {
            DxfDatabase db = WithTablesIn(out DxfTable model, out DxfTable paper, out DxfTable inBlock);
            DxfFile file = DxfFile.Parse(db.ToText());

            Dictionary<string, DxfItem> found = file.TableEntitiesByHandle(
                new List<string> { model.Handle, paper.Handle, inBlock.Handle, "FFFF", "zz" }
            );

            Assert.Equal(2, found.Count);
            Assert.Equal(4, ((DxfTable)DxfEntity.Of(found[paper.Handle])).NumRows);
            Assert.Equal(3, ((DxfTable)DxfEntity.Of(found[model.Handle])).NumRows);
            Assert.False(found[model.Handle].IsPaper);
            Assert.True(found[paper.Handle].IsPaper);
            Assert.False(found.ContainsKey(inBlock.Handle));
        }

        [Fact]
        public void ATableFoundInTheRecords_ChangedAsAnyEntityIs_IsSavedWithItsChange()
        {
            DxfDatabase db = WithTablesIn(out DxfTable model, out _, out _);
            DxfFile file = DxfFile.Parse(db.ToText());
            DxfItem item = file.TableEntitiesByHandle(new List<string> { model.Handle })[model.Handle];
            var table = (DxfTable)DxfEntity.Of(item);

            table.SetTextString(2, 1, "CHANGED");
            table.SetTextString(0, 0, "AND AGAIN");

            Assert.Equal("CHANGED", table.TextString(2, 1));
            Assert.Contains("AND AGAIN", file.ToText());
        }

        [Fact]
        public void TheFileOpenedFromTheBytesOfADrawing_HoldsTheTextOfTheDrawing_WhateverIsInIt()
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 3, 3);
            table.SetTextString(1, 1, "café Đ A\\PB 𝄞");
            table.SetTextString(2, 2, new string('w', 300));

            DxfFile copy = db.File.CopyFromBytes();

            Assert.NotNull(copy);
            Assert.Equal(db.ToText(), copy.ToText());
            Assert.Equal(db.ToText(), DxfFile.Parse(db.ToText()).ToText());
        }

        [Fact]
        public void TheFileOfADrawingWithAMarkOfUtf8_IsCopiedFromItsText_AndItsTablesAreStillCompleted()
        {
            DxfDatabase db = WithTablesIn(out _, out _, out _);
            byte[] text = Encoding.UTF8.GetBytes(db.ToText());
            byte[] marked = new byte[] { 0xEF, 0xBB, 0xBF }
                .Concat(text)
                .ToArray();

            DxfDatabase read;

            using (var stream = new MemoryStream(marked))
            {
                read = DxfDatabase.Open(stream);
            }

            Assert.Null(read.File.CopyFromBytes());

            read.ModelSpace.OfType<DxfTable>().Single().SetTextString(2, 1, "EDITED");
            string saved = Encoding.UTF8.GetString(DxfSpeed12Scenarios.Saved(read));

            Assert.Contains("TABLECONTENT", saved);
            Assert.Contains("EDITED", saved);
        }

        [Fact]
        public void ATableStyleAndATableWrittenInTheSession_ShareOneCompletion_AndGetTheirMapsOfCellStyles()
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            var style = new DxfTableStyle();
            db.TableStyleDictionary.SetAt("GH_SCHEDULE", style);
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 3, 2, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            table.TableStyle = style;
            table.SetTextString(1, 0, "STYLED");

            string saved = Encoding.UTF8.GetString(DxfSpeed12Scenarios.Saved(db));

            Assert.Contains("ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP", saved);
            Assert.Contains("ACAD_ROUNDTRIP_2008_TABLE_ENTITY", saved);
        }

        #endregion
    }
}
