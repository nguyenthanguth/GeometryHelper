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
    /// Layouts made in code, added to a drawing by AddToLayoutDictionary with their LAYOUT, block and entry, and given the
    /// viewport of their sheet by Initialize; and the viewports of the layout a drawing shows, by GetViewports. ezdxf 1.4.5 read
    /// the probe's drawings back, the new layout among its layouts with its viewports 1 and 2, its recover and audit finding
    /// nothing the drawings did not have before.
    /// </summary>
    public class DxfNewLayoutTests
    {
        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ALayoutMadeInCode_IsAddedWithItsBlockAndItsEntry_OnTheTabAfterTheLast(string version)
        {
            string text = Drawing(version);
            DxfDatabase db = DxfDatabase.Parse(text);
            var plan = new DxfLayout { LayoutName = "Plan 1-50" };

            Assert.Equal("Plan 1-50", plan.LayoutName);
            Assert.Null(plan.Handle);
            Assert.Null(plan.BlockTableRecord);
            Assert.False(plan.TabSelected);
            Assert.Empty(plan.GetViewports());

            plan.AddToLayoutDictionary(db, null);

            // Model, Layout1 and Sheet A on tab 3, its block *Paper_Space0: the new one is on tab 4, its block *Paper_Space1.
            Assert.Equal(new[] { "Model", "Layout1", "Sheet A", "Plan 1-50" }, db.Layouts.Select(l => l.LayoutName));
            Assert.Same(plan, db.Layouts[3]);
            Assert.Same(plan, db.LayoutDictionary["plan 1-50"]);
            Assert.Equal(4, plan.TabOrder);
            Assert.False(plan.TabSelected);
            Assert.False(plan.ModelType);
            DxfBlockTableRecord block = plan.BlockTableRecord;
            Assert.Equal("*Paper_Space1", block.Name);
            Assert.True(block.IsLayout);
            Assert.Contains(block, db.BlockTable);
            Assert.Equal(db.LayoutDictionary.Handle, plan.OwnerHandle);
            Assert.Empty(plan.GetViewports());

            // The sheet of A3 on its side with no plotter: margins 7.5 and 20, limits the sheet about its plotted area.
            Assert.Equal(string.Empty, plan.PlotSettingsName);
            Assert.Equal("none_device", plan.PlotConfigurationName);
            Assert.Equal("ISO_A3_(420.00_x_297.00_MM)", plan.CanonicalMediaName);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(7.5, 20), new GeoPoint2(7.5, 20)), plan.PlotPaperMargins);
            Assert.Equal(Bits(420.0) + Bits(297.0), Bits(plan.PlotPaperSize.X) + Bits(plan.PlotPaperSize.Y));
            Assert.Equal(DxfPlotPaperUnit.Millimeters, plan.PlotPaperUnits);
            Assert.Equal(DxfPlotType.Layout, plan.PlotType);
            Assert.Equal(DxfStdScaleType.StdScale1To1, plan.StdScaleType);
            Assert.Equal(new DxfCustomScale(1.0, 1.0), plan.CustomPrintScale);
            Assert.True(plan.DrawViewportsFirst);
            Assert.True(plan.PrintLineweights);
            Assert.True(plan.PlotPlotStyles);
            Assert.True(plan.UseStandardScale);
            Assert.False(plan.PlotViewportBorders);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-7.5, -20), new GeoPoint2(412.5, 277)), plan.Limits);
            Assert.True(plan.Extents.IsEmpty);

            // The records the change wrote: the layout dictionary and the block table's count changed; the BLOCK_RECORD,
            // BLOCK, ENDBLK and LAYOUT added, the BLOCK_RECORD naming the layout and the layout the BLOCK_RECORD.
            string saved = db.ToText();
            Differ(text, saved, out List<string> gone, out List<string> come);
            Assert.Equal(new[] { db.LayoutDictionary.Handle, db.BlockTable.Handle }.OrderBy(h => h, StringComparer.Ordinal), gone.Where(r => !r.Contains("\nHEADER\n")).Select(HandleOf).OrderBy(h => h, StringComparer.Ordinal));
            Assert.Equal(new[] { "BLOCK", "BLOCK_RECORD", "DICTIONARY", "ENDBLK", "LAYOUT", "TABLE" }, come.Where(r => !r.Contains("\nHEADER\n")).Select(r => r.Split('\n')[1]).OrderBy(t => t, StringComparer.Ordinal));
            string record = Records(saved).Single(r => HandleOf(r) == block.Handle);
            Assert.Contains("  2\n*Paper_Space1\n340\n" + plan.Handle + "\n", record, StringComparison.Ordinal);
            string layout = Records(saved).Single(r => HandleOf(r) == plan.Handle);
            Assert.Contains("100\nAcDbLayout\n  1\nPlan 1-50\n 70\n1\n 71\n4\n", layout, StringComparison.Ordinal);
            Assert.EndsWith(" 76\n1\n330\n" + block.Handle + "\n", layout, StringComparison.Ordinal);
            Assert.Contains("  3\nPlan 1-50\n350\n" + plan.Handle + "\n", Records(saved).Single(r => HandleOf(r) == db.LayoutDictionary.Handle), StringComparison.Ordinal);

            DxfDatabase again = DxfDatabase.Parse(saved);
            DxfLayout read = again.Layouts[3];
            Assert.Equal("Plan 1-50", read.LayoutName);
            Assert.Equal(4, read.TabOrder);
            Assert.Equal("*Paper_Space1", read.BlockTableRecord.Name);
            Assert.Equal("ISO_A3_(420.00_x_297.00_MM)", read.CanonicalMediaName);
            Assert.Equal(plan.Limits, read.Limits);
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ALayoutInitialized_IsGivenItsViewport1_AboutTheMiddleOfItsSheet_AndOnlyOnce(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(Drawing(version));
            var plan = new DxfLayout { LayoutName = "Plan 1-50" };
            plan.AddToLayoutDictionary(db, null);

            DxfViewport sheet = plan.Initialize();

            // Limits (-7.5, -20) to (412.5, 277): about (202.5, 128.5), 1.1 times 420 by 297, showing the sheet at 1:1.
            Assert.Equal(1, sheet.Number);
            Assert.True(sheet.On);
            Assert.Equal(plan.BlockTableRecord.Handle, sheet.OwnerHandle);
            Assert.Equal(Bits(new GeoPoint3(202.5, 128.5, 0)), Bits(sheet.CenterPoint));
            Assert.Equal(Bits(462.0), Bits(sheet.Width));
            Assert.Equal(Bits(326.7), Bits(sheet.Height));
            Assert.Equal(Bits(202.5) + Bits(128.5), Bits(sheet.ViewCenter.X) + Bits(sheet.ViewCenter.Y));
            Assert.Equal(Bits(326.7), Bits(sheet.ViewHeight));
            Assert.Equal(Bits(1.0), Bits(sheet.CustomScale));
            Assert.True(sheet.PlotWireframe);
            Assert.Equal(new[] { sheet }, plan.GetViewports());

            // Its status and id 1 and AutoCAD's flags 557 088; the layout's last active viewport names it.
            string saved = db.ToText();
            string record = Records(saved).Single(r => HandleOf(r) == sheet.Handle);
            Assert.Contains(" 68\n1\n 69\n1\n", record, StringComparison.Ordinal);
            Assert.Contains(" 90\n557088\n", record, StringComparison.Ordinal);
            Assert.EndsWith("330\n" + plan.BlockTableRecord.Handle + "\n331\n" + sheet.Handle + "\n", Records(saved).Single(r => HandleOf(r) == plan.Handle), StringComparison.Ordinal);

            // Initialized again, nothing is written; a viewport appended after it is the layout's 2.
            Assert.Same(sheet, plan.Initialize());
            Assert.Equal(saved, db.ToText());
            var detail = new DxfViewport { CenterPoint = new GeoPoint3(200, 130, 0), Width = 300, Height = 200, ViewHeight = 10000 };
            plan.BlockTableRecord.AppendEntity(detail);
            Assert.Equal(-1, detail.Number);
            detail.On = true;
            Assert.Equal(2, detail.Number);

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { 1, 2 }, again.Layouts[3].GetViewports().Select(v => v.Number));
            Assert.True(again.Layouts[3].GetViewports()[0].On);
            Assert.Equal(Bits(326.7), Bits(again.Layouts[3].GetViewports()[0].Height));
        }

        [Fact]
        public void ALayoutOfTheDrawingWithNoViewport1_IsGivenOne_AndOneThatHasItIsGivenNothing()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            // Sheet A has its viewport 1 (3F): it is the one given, and nothing is written.
            Assert.Equal("3F", db.Layouts[2].Initialize().Handle);
            Assert.Equal(text, db.ToText());

            // Layout1, of paper space, has none: one about the middle of its limits (0, 0) to (420, 297), in the ENTITIES section.
            DxfLayout layout1 = db.Layouts[1];
            DxfViewport sheet = layout1.Initialize();
            Assert.Same(db.PaperSpace, sheet.Owner);
            Assert.Equal(Bits(new GeoPoint3(210, 148.5, 0)), Bits(sheet.CenterPoint));
            Assert.Equal(Bits(462.0) + Bits(326.7), Bits(sheet.Width) + Bits(sheet.Height));
            Assert.Equal(new[] { 1 }, DxfDatabase.Parse(db.ToText()).Layouts[1].GetViewports().Select(v => v.Number));

            Assert.Throws<InvalidOperationException>(() => db.Layouts[0].Initialize());
            Assert.Throws<InvalidOperationException>(() => new DxfLayout { LayoutName = "Loose" }.Initialize());
        }

        [Fact]
        public void ABlockOfPaperSpaceNoLayoutHas_IsTakenAsTheNewLayoutsBlock_AndOthersAreRefused()
        {
            // Sheet A taken out of the layout dictionary, its block *Paper_Space0 naming no layout (340 of 0).
            string text = DxfObjectsEzdxfDrawings.R2018.Replace("*Paper_Space0\n340\n3E\n", "*Paper_Space0\n340\n0\n").Replace("  3\nSheet A\n350\n3E\n", string.Empty);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfBlockTableRecord free = db.BlockTable["*Paper_Space0"];
            Assert.Equal(new[] { "Model", "Layout1" }, db.Layouts.Select(l => l.LayoutName));

            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "Plan" }.AddToLayoutDictionary(db, db.ModelSpace));
            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "Plan" }.AddToLayoutDictionary(db, db.PaperSpace));
            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "Plan" }.AddToLayoutDictionary(db, DxfDatabase.Parse(text).BlockTable["*Paper_Space0"]));
            Assert.Equal(text, db.ToText());

            // A block of a name of its own is not a layout's.
            DxfDatabase other = DxfDatabase.Parse(text);
            other.BlockTable.Add(new DxfBlockTableRecord { Name = "TITLE" });
            string titled = other.ToText();
            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "Plan" }.AddToLayoutDictionary(other, other.BlockTable["TITLE"]));
            Assert.Equal(titled, other.ToText());

            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(db, free);

            // The block keeps its viewports, the one of id 1 among them, and names the new layout; no block is made.
            Assert.Same(free, plan.BlockTableRecord);
            Assert.Equal(2, plan.TabOrder);
            Assert.Equal(new[] { "3F", "40", "41" }, plan.GetViewports().Select(v => v.Handle));
            Assert.Equal("3F", plan.Initialize().Handle);
            Assert.DoesNotContain("*Paper_Space1", db.ToText(), StringComparison.Ordinal);
            Assert.Contains("*Paper_Space0\n340\n" + plan.Handle + "\n", Records(db.ToText()).Single(r => HandleOf(r) == "3B"), StringComparison.Ordinal);

            // Now a layout's, it is not taken again.
            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "Plan 2" }.AddToLayoutDictionary(db, free));
        }

        [Fact]
        public void ALayoutNotNamed_NamedAsAnother_AddedTwice_OrToADrawingWithNoLayouts_IsRefused()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Throws<ArgumentNullException>(() => new DxfLayout { LayoutName = "Plan" }.AddToLayoutDictionary(null, null));
            Assert.Throws<InvalidOperationException>(() => new DxfLayout().AddToLayoutDictionary(db, null));
            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "layout1" }.AddToLayoutDictionary(db, null));
            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "model" }.AddToLayoutDictionary(db, null));
            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "a|b" });
            Assert.Throws<ArgumentNullException>(() => new DxfLayout { LayoutName = null });
            Assert.Throws<InvalidOperationException>(() => new DxfLayout { LayoutName = "Plan" }.AddToLayoutDictionary(DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12), null));
            Assert.Throws<InvalidOperationException>(() => db.Layouts[1].AddToLayoutDictionary(db, null));
            Assert.Equal(text, db.ToText());

            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(db, null);
            Assert.Throws<InvalidOperationException>(() => plan.AddToLayoutDictionary(db, null));

            // Renamed once added, as any layout is.
            plan.LayoutName = "Plan 1-100";
            Assert.Same(plan, db.LayoutDictionary["Plan 1-100"]);
        }

        [Fact]
        public void TheViewportsOfTheLayoutShown_AreThoseOfPaperSpace_AndNoneWhereModelSpaceIsShown()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfViewport sheet = db.Layouts[1].Initialize();
            var detail = new DxfViewport { CenterPoint = new GeoPoint3(150, 100, 0), Width = 200, Height = 150, ViewHeight = 7500 };
            db.PaperSpace.AppendEntity(detail);

            // $TILEMODE 1: model space is shown, its viewports the VPORT table's, which have no class.
            Assert.True(db.TileMode);
            Assert.Empty(db.GetViewports(true));

            // Paper space shown: Layout1's viewport 1 and the one 200 by 150, not those of Sheet A.
            db.TileMode = false;
            Assert.Equal(new[] { sheet, detail }, db.GetViewports(true));
            Assert.Equal(db.Layouts[1].GetViewports(), db.GetViewports(true));
            Assert.Empty(db.GetViewports(false));

            // A viewport of R12, in paper space, kept in its extended data MVIEW.
            DxfDatabase r12 = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12);
            r12.TileMode = false;
            Assert.Equal(r12.PaperSpace.OfType<DxfViewport>().Select(v => v.Handle), r12.GetViewports(true).Select(v => v.Handle));
            Assert.NotEmpty(r12.GetViewports(true));
        }

        [Fact]
        public void ALayoutWithViewportsButNoViewport1_IsGivenOneAheadOfThem_TheirStatusesMovedUp()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfLayout layout1 = db.Layouts[1];
            var detail = new DxfViewport { CenterPoint = new GeoPoint3(100, 80, 0), Width = 50, Height = 40, ViewHeight = 400 };
            db.PaperSpace.AppendEntity(detail);
            detail.On = true;
            Assert.Equal(2, detail.Number);
            Assert.Contains(" 68\n1\n 69\n2\n", Records(db.ToText()).Single(r => HandleOf(r) == detail.Handle), StringComparison.Ordinal);

            DxfViewport sheet = layout1.Initialize();

            // The first VIEWPORT of the layout is the one AutoCAD and BricsCAD take for its sheet: viewport 1 comes ahead of the
            // 50 by 40, stacked first, and the 50 by 40 moves up to status 2; each keeps its handle.
            Assert.Equal(new[] { sheet, detail }, layout1.GetViewports());
            Assert.Equal(new[] { 1, 2 }, layout1.GetViewports().Select(v => v.Number));
            string saved = db.ToText();
            Assert.Contains(" 68\n1\n 69\n1\n", Records(saved).Single(r => HandleOf(r) == sheet.Handle), StringComparison.Ordinal);
            Assert.Contains(" 68\n2\n 69\n2\n", Records(saved).Single(r => HandleOf(r) == detail.Handle), StringComparison.Ordinal);
            Assert.True(saved.IndexOf("\n  5\n" + sheet.Handle + "\n", StringComparison.Ordinal) < saved.IndexOf("\n  5\n" + detail.Handle + "\n", StringComparison.Ordinal));

            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(new[] { sheet.Handle, detail.Handle }, again.Layouts[1].GetViewports().Select(v => v.Handle));
            Assert.True(again.Layouts[1].GetViewports().All(v => v.On));
        }

        [Fact]
        public void TheBlockOfANewLayout_IsTheFirstNumberOfPaperSpaceTheDrawingHasNot()
        {
            // Sheet A's block named *Paper_Space2: 0 and 1 are free, then 3.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018.Replace("*Paper_Space0", "*Paper_Space2"));
            Assert.Equal("*Paper_Space2", db.Layouts[2].BlockTableRecord.Name);
            var names = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                var layout = new DxfLayout { LayoutName = "Plan " + i };
                layout.AddToLayoutDictionary(db, null);
                names.Add(layout.BlockTableRecord.Name);
            }

            Assert.Equal(new[] { "*Paper_Space0", "*Paper_Space1", "*Paper_Space3" }, names);
            Assert.Equal(new[] { 4, 5, 6 }, db.Layouts.Skip(3).Select(l => l.TabOrder));
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        public void ALayoutMadeInCode_IsAddedAndInitialized_InADrawingOfR2004ToR2013(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\n" + version + "\n"));
            Assert.Equal(version, db.Version);
            var plan = new DxfLayout { LayoutName = "Plan 1-50" };

            plan.AddToLayoutDictionary(db, null);
            DxfViewport sheet = plan.Initialize();

            // The BLOCK_RECORD has its units, explodability and scaling (70, 280, 281) from R2007 on, as AutoCAD writes it.
            string record = Records(db.ToText()).Single(r => HandleOf(r) == plan.BlockTableRecord.Handle);
            Assert.Equal(version != "AC1018", record.Contains("\n 70\n0\n280\n1\n281\n0\n"));
            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal("Plan 1-50", again.Layouts[3].LayoutName);
            Assert.Equal("*Paper_Space1", again.Layouts[3].BlockTableRecord.Name);
            Assert.Equal(new[] { sheet.Handle }, again.Layouts[3].GetViewports().Select(v => v.Handle));
        }

        [Fact]
        public void AnEntityAppendedToTheBlockOfAnotherLayout_IsInPaperSpace_AsAutoCADAndEzdxfWriteIt()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfBlockTableRecord sheetA = db.Layouts[2].BlockTableRecord;
            db.BlockTable.Add(new DxfBlockTableRecord { Name = "TITLE" });
            var inSheet = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var inBlock = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var inModel = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            var moved = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0));
            sheetA.AppendEntity(inSheet);
            db.BlockTable["TITLE"].AppendEntity(inBlock);
            db.ModelSpace.AppendEntity(inModel);
            db.ModelSpace.AppendEntity(moved);
            sheetA.AssumeOwnershipOf(new[] { moved });

            // *Paper_Space0 is Sheet A's: its entities are in paper space (67 of 1), as the viewports ezdxf wrote there are; a
            // block's and model space's are not. A copy of one that has its 67 has it once.
            string saved = db.ToText();
            Assert.Contains("100\nAcDbEntity\n 67\n1\n  8\n0\n", Records(saved).Single(r => HandleOf(r) == inSheet.Handle), StringComparison.Ordinal);
            Assert.Contains("100\nAcDbEntity\n 67\n1\n", Records(saved).Single(r => HandleOf(r) == moved.Handle), StringComparison.Ordinal);
            Assert.DoesNotContain(" 67\n", Records(saved).Single(r => HandleOf(r) == inBlock.Handle), StringComparison.Ordinal);
            Assert.DoesNotContain(" 67\n", Records(saved).Single(r => HandleOf(r) == inModel.Handle), StringComparison.Ordinal);
            DxfEntity copy = ByHandle<DxfViewport>(db, "41").Clone();
            sheetA.AppendEntity(copy);
            string copied = Records(db.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.Equal(copied.IndexOf(" 67\n", StringComparison.Ordinal), copied.LastIndexOf(" 67\n", StringComparison.Ordinal));
            Assert.Contains(" 67\n1\n", copied, StringComparison.Ordinal);
        }

        [Fact]
        public void ABlockOfPaperSpaceWhoseRecordGivesNoGroup340_IsFree_AndIsGivenOne()
        {
            // An unused *Paper_Space7: its BLOCK_RECORD with no 340, its BLOCK and ENDBLK.
            string text = DxfObjectsEzdxfDrawings.R2018
                .Replace("*Paper_Space0\n340\n3E\n 70\n0\n280\n1\n281\n0\n", "*Paper_Space0\n340\n3E\n 70\n0\n280\n1\n281\n0\n"
                    + DxfLines.G(0, "BLOCK_RECORD", 5, "F07", 330, "9", 100, "AcDbSymbolTableRecord", 100, "AcDbBlockTableRecord", 2, "*Paper_Space7", 70, 0, 280, 1, 281, 0))
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n", DxfLines.G(
                    0, "BLOCK", 5, "F08", 330, "F07", 100, "AcDbEntity", 8, "0", 100, "AcDbBlockBegin", 2, "*Paper_Space7", 70, 0, 10, "0.0", 20, "0.0", 30, "0.0", 3, "*Paper_Space7", 1, string.Empty,
                    0, "ENDBLK", 5, "F09", 330, "F07", 100, "AcDbEntity", 8, "0", 100, "AcDbBlockEnd") + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfBlockTableRecord free = db.BlockTable["*Paper_Space7"];
            Assert.DoesNotContain("\n340\n", Records(text).Single(r => HandleOf(r) == "F07"), StringComparison.Ordinal);

            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(db, free);

            // The block is the layout's, its BLOCK_RECORD given the 340 that names it after its name, where AutoCAD writes it.
            Assert.Same(free, plan.BlockTableRecord);
            Assert.Contains("  2\n*Paper_Space7\n340\n" + plan.Handle + "\n 70\n0\n", Records(db.ToText()).Single(r => HandleOf(r) == "F07"), StringComparison.Ordinal);
            Assert.Equal("*Paper_Space7", DxfDatabase.Parse(db.ToText()).Layouts.Single(l => l.LayoutName == "Plan").BlockTableRecord.Name);
            Assert.Throws<ArgumentException>(() => new DxfLayout { LayoutName = "Plan 2" }.AddToLayoutDictionary(db, free));
        }

        [Fact]
        public void TheViewportsMovedBehindANewViewport1_KeepTheirPlacesInTheBlocksSortTable()
        {
            // Sheet A's viewport 1 renumbered 5, so that it has none of id 1; a SORTENTSTABLE of its block giving 3F the sort
            // handle 99 and 40 the sort handle 98.
            string table = DxfLines.G(0, "SORTENTSTABLE", 5, "F10", 330, "A", 100, "AcDbSortentsTable", 330, "3B", 331, "3F", 5, "99", 331, "40", 5, "98");
            string text = DxfObjectsEzdxfDrawings.R2018.Replace(" 68\n1\n 69\n1\n", " 68\n1\n 69\n5\n").Replace("  0\nENDSEC\n  0\nEOF\n", table + "  0\nENDSEC\n  0\nEOF\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayout sheet = db.Layouts[2];

            DxfViewport first = sheet.Initialize();

            Assert.Equal(new[] { first.Handle, "3F", "40", "41" }, sheet.GetViewports().Select(v => v.Handle));
            Assert.Equal(table, Records(db.ToText()).Single(r => HandleOf(r) == "F10"));
        }

        [Fact]
        public void AnEntityWithFollowersMovedIntoPaperSpaceOrALayoutsBlock_HasEachFollowerInPaperSpaceToo()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfBlockTableRecord sheetA = db.Layouts[2].BlockTableRecord;
            db.BlockTable.Add(new DxfBlockTableRecord { Name = "TAG" });
            var points = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0) };
            var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0, false, 0, 0, null);
            var insert = new DxfBlockReference(new GeoPoint3(10, 20, 0), db.BlockTable["TAG"]);
            var shown = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0, false, 0, 0, null);
            var appended = new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0, false, 0, 0, null);
            db.ModelSpace.AppendEntity(polyline);
            db.ModelSpace.AppendEntity(insert);
            insert.AttributeCollection.AppendAttribute(new DxfAttributeReference(new GeoPoint3(10, 19, 0), "B-99", "MARK", null));
            db.ModelSpace.AppendEntity(shown);
            sheetA.AppendEntity(appended);

            sheetA.AssumeOwnershipOf(new DxfEntity[] { polyline, insert });
            db.PaperSpace.AssumeOwnershipOf(new DxfEntity[] { shown });

            // Every VERTEX, ATTRIB and SEQEND in paper space as its entity is (67 of 1), as AutoCAD and ezdxf write them and as
            // those of one appended there are.
            List<string> records = Records(db.ToText());

            foreach (DxfEntity entity in new DxfEntity[] { polyline, insert, shown, appended })
            {
                int at = records.FindIndex(r => HandleOf(r) == entity.Handle);
                int end = records.FindIndex(at, r => r.StartsWith("  0\nSEQEND\n", StringComparison.Ordinal));
                Assert.Equal(entity is DxfBlockReference ? 3 : 5, end - at + 1);
                Assert.All(records.GetRange(at, end - at + 1), r => Assert.Contains("100\nAcDbEntity\n 67\n1\n", r, StringComparison.Ordinal));
            }
        }

        private static string Drawing(string version) => version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018;
    }
}
