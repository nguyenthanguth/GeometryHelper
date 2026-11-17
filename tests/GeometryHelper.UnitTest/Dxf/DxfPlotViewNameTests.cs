using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The view a layout or a page setup plots (group 6 with plot type 3), set as AutoCAD's PlotSettingsValidator sets it and
    /// kept through a DXF and a DWG of R2000 and of R2018.
    /// </summary>
    public class DxfPlotViewNameTests
    {
        private static DxfDatabase WithAView(string version)
        {
            var db = new DxfDatabase(version);
            db.ViewTable.Add(
                new DxfViewTableRecord
                {
                    Name = "Stair core",
                    CenterPoint = new GeoPoint2(18450, 6200),
                    Height = 9000,
                    Width = 12000,
                }
            );
            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(db, null);
            plan.PlotType = DxfPlotType.View;
            plan.PlotViewName = "Stair core";
            return db;
        }

        private static DxfDatabase ThroughDwg(DxfDatabase db)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(db, stream);
                return DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(stream.ToArray()), false));
            }
        }

        [Fact]
        public void ANamedViewSetAsWhatToPlot_IsWrittenAsGroupSix_AndReadsBackAfterSaving()
        {
            DxfDatabase db = WithAView("AC1032");
            DxfLayout plan = db.Layouts.Single(l => l.LayoutName == "Plan");
            string record = Records(db.ToText()).Single(r => HandleOf(r) == plan.Handle);

            Assert.Contains("  6\nStair core\n", record, StringComparison.Ordinal);
            Assert.Contains(" 74\n3\n", record, StringComparison.Ordinal);

            DxfLayout read = Opened(Saved(db)).Layouts.Single(l => l.LayoutName == "Plan");
            Assert.Equal(DxfPlotType.View, read.PlotType);
            Assert.Equal("Stair core", read.PlotViewName);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        public void ANamedViewSetAsWhatToPlot_IsKeptByADwg(string version)
        {
            DxfDatabase read = ThroughDwg(WithAView(version));
            DxfLayout plan = read.Layouts.Single(l => l.LayoutName == "Plan");

            Assert.Equal(DxfPlotType.View, plan.PlotType);
            Assert.Equal("Stair core", plan.PlotViewName);
        }

        [Fact]
        public void ANameSetTwice_IsReplaced_AndAnEmptyNameClearsIt()
        {
            DxfDatabase db = WithAView("AC1032");
            DxfLayout plan = db.Layouts.Single(l => l.LayoutName == "Plan");

            plan.PlotViewName = "Entrance";
            Assert.Equal("Entrance", plan.PlotViewName);
            plan.PlotViewName = string.Empty;
            Assert.Equal(string.Empty, plan.PlotViewName);
        }

        [Fact]
        public void ANullName_IsRefused_AndALayoutOutsideADrawing_CannotBeGivenOne()
        {
            DxfDatabase db = WithAView("AC1032");
            DxfLayout plan = db.Layouts.Single(l => l.LayoutName == "Plan");

            Assert.Throws<ArgumentNullException>(() => plan.PlotViewName = null);
            Assert.Throws<InvalidOperationException>(() => new DxfLayout().PlotViewName = "Stair core");
        }
    }
}
