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
    /// What a layout plots and how, set as AutoCAD's PlotSettingsValidator and PlotSettings set it: the plotter's name, the plot
    /// style table, the plot type and window, the scale standard or custom, and the flags of centring, plot styles and
    /// lineweights, written into the part AcDbPlotSettings alone and read back after saving, the limits left as they were.
    /// </summary>
    public class DxfLayoutPlotTests
    {
        [Fact]
        public void AWindowAtOneToTwentyInMonochrome_IsWrittenIntoAcDbPlotSettings_AndReadsBackAfterSaving()
        {
            var db = new DxfDatabase("AC1032");
            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(db, null);
            DxfExtents2d limits = plan.Limits;

            plan.PlotConfigurationName = "GeometryHelper PDF";
            plan.CurrentStyleSheet = "monochrome.ctb";
            plan.PlotType = DxfPlotType.Window;
            plan.PlotWindowArea = new DxfExtents2d(new GeoPoint2(120, 60), new GeoPoint2(7620, 5360));
            plan.CustomPrintScale = new DxfCustomScale(1, 20);
            plan.StdScaleType = DxfStdScaleType.StdScale1To20;
            plan.PlotCentered = true;
            plan.ScaleLineweights = true;
            plan.UseStandardScale = false;

            // The new layout's flags 688 (viewports first, lineweights, plot styles, standard scale): 4 and 64 on, 16 off.
            string record = Records(db.ToText()).Single(r => HandleOf(r) == plan.Handle);
            Assert.Contains("  2\nGeometryHelper PDF\n", record, StringComparison.Ordinal);
            Assert.Contains(
                " 48\n120\n 49\n60\n140\n7620\n141\n5360\n142\n1.0\n143\n20\n 70\n740\n",
                record,
                StringComparison.Ordinal
            );
            Assert.Contains("  7\nmonochrome.ctb\n 75\n23\n", record, StringComparison.Ordinal);
            Assert.Contains("147\n0.05\n", record, StringComparison.Ordinal);
            Assert.Contains(" 74\n4\n", record, StringComparison.Ordinal);
            Assert.Equal(limits, plan.Limits);

            DxfLayout read = Opened(Saved(db)).Layouts.Single(l => l.LayoutName == "Plan");
            Assert.Equal("GeometryHelper PDF", read.PlotConfigurationName);
            Assert.Equal("monochrome.ctb", read.CurrentStyleSheet);
            Assert.Equal(DxfPlotType.Window, read.PlotType);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(120, 60), new GeoPoint2(7620, 5360)), read.PlotWindowArea);
            Assert.Equal(new DxfCustomScale(1, 20), read.CustomPrintScale);
            Assert.Equal(DxfStdScaleType.StdScale1To20, read.StdScaleType);
            Assert.Equal(Bits(0.05), Bits(read.StdScale));
            Assert.True(read.PlotCentered);
            Assert.True(read.ScaleLineweights);
            Assert.False(read.UseStandardScale);
            Assert.True(read.PrintLineweights);
            Assert.True(read.PlotPlotStyles);
            Assert.True(read.DrawViewportsFirst);
            Assert.Equal(limits, read.Limits);
        }

        [Fact]
        public void ValuesAsTheyAre_WriteNothing_AndScaledToFit_KeepsTheStandardScaleNumber()
        {
            var db = new DxfDatabase("AC1032");
            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(db, null);
            string before = db.ToText();

            plan.PlotConfigurationName = "none_device";
            plan.CurrentStyleSheet = string.Empty;
            plan.PlotType = DxfPlotType.Layout;
            plan.PlotWindowArea = new DxfExtents2d(new GeoPoint2(0, 0), new GeoPoint2(0, 0));
            plan.CustomPrintScale = new DxfCustomScale(1, 1);
            plan.StdScaleType = DxfStdScaleType.StdScale1To1;
            plan.PlotCentered = false;
            plan.UseStandardScale = true;
            plan.PlotPlotStyles = true;
            plan.ScaleLineweights = false;
            plan.PrintLineweights = true;
            Assert.Equal(before, db.ToText());

            plan.StdScaleType = DxfStdScaleType.StdScale1To50;
            plan.StdScaleType = DxfStdScaleType.ScaleToFit;
            plan.PrintLineweights = false;
            plan.PlotPlotStyles = false;

            DxfLayout read = DxfDatabase.Parse(db.ToText()).Layouts.Single(l => l.LayoutName == "Plan");
            Assert.Equal(DxfStdScaleType.ScaleToFit, read.StdScaleType);
            Assert.Equal(Bits(0.02), Bits(read.StdScale));
            Assert.False(read.PrintLineweights);
            Assert.False(read.PlotPlotStyles);
            Assert.True(read.UseStandardScale);
        }

        [Fact]
        public void SettingsNotAdded_AreRefused_AsAreValuesOfNoKind_AndNothingIsWritten()
        {
            var loose = new DxfLayout { LayoutName = "Loose" };
            Assert.Throws<InvalidOperationException>(() => loose.PlotConfigurationName = "x");
            Assert.Throws<InvalidOperationException>(() => loose.CurrentStyleSheet = "x");
            Assert.Throws<InvalidOperationException>(() => loose.PlotType = DxfPlotType.Extents);
            Assert.Throws<InvalidOperationException>(() =>
                loose.PlotWindowArea = new DxfExtents2d(new GeoPoint2(0, 0), new GeoPoint2(1, 1))
            );
            Assert.Throws<InvalidOperationException>(() => loose.CustomPrintScale = new DxfCustomScale(1, 2));
            Assert.Throws<InvalidOperationException>(() => loose.StdScaleType = DxfStdScaleType.StdScale1To2);
            Assert.Throws<InvalidOperationException>(() => loose.PlotCentered = true);

            var db = new DxfDatabase("AC1032");
            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(db, null);
            string before = db.ToText();

            Assert.Throws<ArgumentNullException>(() => plan.PlotConfigurationName = null);
            Assert.Throws<ArgumentNullException>(() => plan.CurrentStyleSheet = null);
            Assert.Throws<ArgumentException>(() => plan.PlotType = (DxfPlotType)6);
            Assert.Throws<ArgumentException>(() => plan.StdScaleType = (DxfStdScaleType)34);
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.CustomPrintScale = new DxfCustomScale(0, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                plan.CustomPrintScale = new DxfCustomScale(1, double.PositiveInfinity)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                plan.PlotWindowArea = new DxfExtents2d(new GeoPoint2(double.NaN, 0), new GeoPoint2(1, 1))
            );
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void TheModelLayout_IsGivenItsPlotSettings_ItsOwnFlagsAndLimitsKept()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayout model = db.Layouts.Single(l => l.ModelType || l.LayoutName == "Model");
            DxfExtents2d limits = model.Limits;
            bool wasModel = model.ModelType;

            model.PlotType = DxfPlotType.Extents;
            model.UseStandardScale = true;
            model.StdScaleType = DxfStdScaleType.ScaleToFit;
            model.PlotCentered = true;

            DxfLayout read = DxfDatabase.Parse(db.ToText()).Layouts.Single(l => l.LayoutName == "Model");
            Assert.Equal(DxfPlotType.Extents, read.PlotType);
            Assert.True(read.PlotCentered);
            Assert.True(read.UseStandardScale);
            Assert.Equal(DxfStdScaleType.ScaleToFit, read.StdScaleType);
            Assert.Equal(wasModel, read.ModelType);
            Assert.Equal(limits, read.Limits);

            var changed = new List<string>();
            string[] was = Records(text).Single(r => HandleOf(r) == model.Handle).Split('\n');
            string[] now = Records(db.ToText()).Single(r => HandleOf(r) == model.Handle).Split('\n');
            Assert.Equal(was.Length, now.Length);

            for (int i = 1; i < was.Length; i += 2)
            {
                if (was[i] != now[i])
                {
                    changed.Add(was[i - 1].Trim());
                }
            }

            Assert.All(changed, code => Assert.Contains(code, new[] { "70", "74", "75", "147" }));
        }
    }
}
