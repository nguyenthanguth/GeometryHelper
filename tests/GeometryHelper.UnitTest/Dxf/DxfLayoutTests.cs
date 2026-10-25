using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// LAYOUTs read as ezdxf 1.4.5 reads them, their names, tabs, limits, extents, blocks, viewports and plot settings, and
    /// renamed; ezdxf read the probe's renamed layout back, its recover and audit finding nothing.
    /// </summary>
    public class DxfLayoutTests
    {
        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void TheLayoutsOfADrawing_AreReadAsEzdxfReadsThem_InTheOrderOfTheirTabs(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018);

            Assert.Equal(new[] { "Model", "Layout1", "Sheet A" }, db.Layouts.Select(l => l.LayoutName));
            Assert.Equal(new[] { 0, 1, 3 }, db.Layouts.Select(l => l.TabOrder));
            Assert.Equal(new[] { "Model", "Layout1", "Sheet A" }, db.LayoutDictionary.Select(e => e.Key));

            DxfLayout model = db.Layouts[0];
            Assert.True(model.ModelType);
            Assert.True(model.TabSelected);
            Assert.Same(db.ModelSpace, model.BlockTableRecord);
            Assert.Empty(model.GetViewports());

            // ezdxf: limmin (0, 0), limmax (420, 297), extmin 1e+20 above extmax -1e+20: no extents.
            DxfLayout layout1 = db.Layouts[1];
            Assert.False(layout1.ModelType);
            Assert.False(layout1.TabSelected);
            Assert.Same(db.PaperSpace, layout1.BlockTableRecord);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(0, 0), new GeoPoint2(420, 297)), layout1.Limits);
            Assert.True(layout1.Extents.IsEmpty);

            // ezdxf: Sheet A, limits (-10, -10) to (410, 287), its block *Paper_Space0 (3B), its viewports 3F, 40, 41.
            DxfLayout sheet = db.Layouts[2];
            Assert.Equal("3E", sheet.Handle);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-10, -10), new GeoPoint2(410, 287)), sheet.Limits);
            Assert.Equal("*Paper_Space0", sheet.BlockTableRecord.Name);
            Assert.Equal(new[] { "3F", "40", "41" }, sheet.GetViewports().Select(v => v.Handle));
            Assert.Equal(new[] { 1, 3, 4 }, sheet.GetViewports().Select(v => v.Number));
            Assert.Equal("LAYOUT 3E Sheet A, tab 3", sheet.ToString());
        }

        [Fact]
        public void ThePlotSettingsOfALayout_AreReadFromItsPartAcDbPlotSettings()
        {
            DxfLayout sheet = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018).Layouts[2];

            // ezdxf: 2 'DWG to PDF.pc3', 4 'ezdxf_(420.00_x_297.00_MM)', margins 10, paper 420 x 297, 142 0.0078125, 143 12,
            // 70 672, 72 1, 73 0, 74 5, 75 1, 76 0, 77 2, 78 300, 147 1.
            Assert.Equal(string.Empty, sheet.PlotSettingsName);
            Assert.Equal("DWG to PDF.pc3", sheet.PlotConfigurationName);
            Assert.Equal("ezdxf_(420.00_x_297.00_MM)", sheet.CanonicalMediaName);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(10, 10), new GeoPoint2(10, 10)), sheet.PlotPaperMargins);
            Assert.Equal(Bits(420.0), Bits(sheet.PlotPaperSize.X));
            Assert.Equal(Bits(297.0), Bits(sheet.PlotPaperSize.Y));
            Assert.Equal(new DxfCustomScale(0.0078125, 12.0), sheet.CustomPrintScale);
            Assert.Equal(DxfPlotPaperUnit.Millimeters, sheet.PlotPaperUnits);
            Assert.Equal(DxfPlotRotation.Degrees000, sheet.PlotRotation);
            Assert.Equal(DxfPlotType.Layout, sheet.PlotType);
            Assert.Equal(DxfStdScaleType.StdScale1To128InchIs1ft, sheet.StdScaleType);
            Assert.Equal(DxfPlotSettingsShadePlotType.AsDisplayed, sheet.ShadePlot);
            Assert.Equal(DxfShadePlotResLevel.Normal, sheet.ShadePlotResLevel);
            Assert.Equal(300, sheet.ShadePlotCustomDpi);
            Assert.Equal(Bits(1.0), Bits(sheet.StdScale));

            // 672: 32 plot styles, 128 lineweights, 512 viewports first.
            Assert.True(sheet.PlotPlotStyles);
            Assert.True(sheet.PrintLineweights);
            Assert.True(sheet.DrawViewportsFirst);
            Assert.False(sheet.PlotViewportBorders);
            Assert.False(sheet.ShowPlotStyles);
            Assert.False(sheet.PlotCentered);
            Assert.False(sheet.PlotHidden);
            Assert.False(sheet.UseStandardScale);
            Assert.False(sheet.ScaleLineweights);
            Assert.False(sheet.ModelType);
        }

        [Fact]
        public void ALayoutRenamed_HasItsNameAndItsEntryRenamed_AndModelKeepsItsName()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayout sheet = db.Layouts[2];

            sheet.LayoutName = "Sheet A";
            Assert.Equal(text, db.ToText());

            sheet.LayoutName = "Plan 1-50";
            Assert.Equal("Plan 1-50", sheet.LayoutName);
            Assert.Throws<ArgumentException>(() => sheet.LayoutName = "model");
            Assert.Throws<ArgumentException>(() => sheet.LayoutName = "a|b");
            Assert.Throws<InvalidOperationException>(() => db.Layouts[0].LayoutName = "Modelled");

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "D", "3E");
            Assert.Contains("100\nAcDbLayout\n  1\nPlan 1-50\n", Records(saved).Single(r => HandleOf(r) == "3E"), StringComparison.Ordinal);
            Assert.Contains("  1\n\n", Records(saved).Single(r => HandleOf(r) == "3E"), StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(new[] { "Model", "Layout1", "Plan 1-50" }, again.LayoutDictionary.Select(e => e.Key));
            Assert.Same(again.Layouts[2], again.LayoutDictionary["plan 1-50"]);
        }

        [Fact]
        public void ADrawingOfR12_HasNoLayouts()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12);

            Assert.Empty(db.Layouts);
            Assert.Null(db.LayoutDictionary);
            Assert.Null(db.NamedObjectsDictionary);
        }
    }
}
