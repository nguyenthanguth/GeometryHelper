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
    /// The sheet of a layout set as AutoCAD's PlotSettingsValidator sets it: its name and with it its size, its units, its turn,
    /// its margins and the plot's origin, written into the part AcDbPlotSettings, read back after saving, and the layout's
    /// limits made the sheet about its plotted area again, as ezdxf's page_setup makes them.
    /// </summary>
    public class DxfLayoutSheetTests
    {
        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        public void ALayoutMadeInCode_SetToA2OnItsSide_ReadsBackAfterSaving_OnASheet594By420(string version)
        {
            var db = new DxfDatabase(version);
            var plan = new DxfLayout { LayoutName = "Plan A2" };
            plan.AddToLayoutDictionary(db, null);

            plan.CanonicalMediaName = "ISO_A2_(594.00_x_420.00_MM)";
            plan.PlotPaperUnits = DxfPlotPaperUnit.Millimeters;
            plan.PlotRotation = DxfPlotRotation.Degrees000;
            plan.PlotPaperMargins = new DxfExtents2d(new GeoPoint2(10, 10), new GeoPoint2(10, 10));
            plan.PlotOrigin = new GeoPoint2(0, 0);
            DxfViewport sheet = plan.Initialize();

            // The limits the sheet about its plotted area, 10 in from its corner; the viewport 1 1.1 times them about their middle.
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-10, -10), new GeoPoint2(584, 410)), plan.Limits);
            Assert.Equal(Bits(new GeoPoint3(287, 200, 0)), Bits(sheet.CenterPoint));
            Assert.Equal(Bits(594 * 11.0 / 10.0) + Bits(420 * 11.0 / 10.0), Bits(sheet.Width) + Bits(sheet.Height));

            string record = Records(db.ToText()).Single(r => HandleOf(r) == plan.Handle);
            Assert.Contains(
                "  4\nISO_A2_(594.00_x_420.00_MM)\n  6\n\n 40\n10\n 41\n10\n 42\n10\n 43\n10\n 44\n594\n 45\n420\n",
                record,
                StringComparison.Ordinal
            );
            Assert.Contains(" 10\n-10\n 20\n-10\n 11\n584\n 21\n410\n", record, StringComparison.Ordinal);

            DxfLayout read = Opened(Saved(db)).Layouts.Single(l => l.LayoutName == "Plan A2");
            Assert.Equal("ISO_A2_(594.00_x_420.00_MM)", read.CanonicalMediaName);
            Assert.Equal(Bits(594.0) + Bits(420.0), Bits(read.PlotPaperSize.X) + Bits(read.PlotPaperSize.Y));
            Assert.Equal(DxfPlotPaperUnit.Millimeters, read.PlotPaperUnits);
            Assert.Equal(DxfPlotRotation.Degrees000, read.PlotRotation);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(10, 10), new GeoPoint2(10, 10)), read.PlotPaperMargins);
            Assert.Equal(Bits(0.0) + Bits(0.0), Bits(read.PlotOrigin.X) + Bits(read.PlotOrigin.Y));
            Assert.Equal(plan.Limits, read.Limits);

            // The paper as the viewer's CadSpace.PaperBounds draws it: the sheet placed at minus the margins, 594 by 420.
            DxfExtents2d paper = Paper(read);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-10, -10), new GeoPoint2(584, 410)), paper);
            Assert.Equal(
                Bits(594.0) + Bits(420.0),
                Bits(paper.MaxPoint.X - paper.MinPoint.X) + Bits(paper.MaxPoint.Y - paper.MinPoint.Y)
            );
        }

        [Theory]
        [InlineData("ISO_A0_(1189.00_x_841.00_MM)", 1189.0, 841.0)]
        [InlineData("ISO_A1_(841.00_x_594.00_MM)", 841.0, 594.0)]
        [InlineData("ISO_A2_(594.00_x_420.00_MM)", 594.0, 420.0)]
        [InlineData("ISO_A3_(420.00_x_297.00_MM)", 420.0, 297.0)]
        [InlineData("ISO_A4_(297.00_x_210.00_MM)", 297.0, 210.0)]
        [InlineData("ISO_A0_(841.00_x_1189.00_MM)", 841.0, 1189.0)]
        [InlineData("ISO_A1_(594.00_x_841.00_MM)", 594.0, 841.0)]
        [InlineData("ISO_A2_(420.00_x_594.00_MM)", 420.0, 594.0)]
        [InlineData("ISO_A3_(297.00_x_420.00_MM)", 297.0, 420.0)]
        [InlineData("ISO_A4_(210.00_x_297.00_MM)", 210.0, 297.0)]
        [InlineData("ezdxf_(1000.00_x_500.00_MM)", 1000.0, 500.0)]
        [InlineData("ISO_full_bleed_A3_(420.00_x_297.00_MM)", 420.0, 297.0)]
        [InlineData("GH-Strip.1_(2400.5_x_120.25_MM)", 2400.5, 120.25)]
        public void ASheetNamedWithItsSizeInMillimetres_GivesThatSize_AndLimitsAboutItsPlottedArea(
            string name,
            double width,
            double height
        )
        {
            DxfLayout plan = Added("AC1032", out _);

            plan.CanonicalMediaName = name;

            // The margins of the new layout kept, 7.5 at the sides and 20 above and below.
            Assert.Equal(name, plan.CanonicalMediaName);
            Assert.Equal(Bits(width) + Bits(height), Bits(plan.PlotPaperSize.X) + Bits(plan.PlotPaperSize.Y));
            Assert.Equal(new DxfExtents2d(new GeoPoint2(7.5, 20), new GeoPoint2(7.5, 20)), plan.PlotPaperMargins);
            Assert.Equal(
                new DxfExtents2d(new GeoPoint2(-7.5, -20), new GeoPoint2(width - 7.5, height - 20)),
                plan.Limits
            );
        }

        [Theory]
        [InlineData("A2")]
        [InlineData("ISO_A2")]
        [InlineData("ISO_A2_(594.00_x_420.00_Inches)")]
        [InlineData("ISO A2 (594.00 x 420.00 MM)")]
        [InlineData("_(594.00_x_420.00_MM)")]
        [InlineData("ISO_A2_(0.00_x_420.00_MM)")]
        [InlineData("ISO_A2_(594.00_x_0_MM)")]
        [InlineData("ISO_A2_(-594.00_x_420.00_MM)")]
        [InlineData("ISO_A2_(594,00_x_420,00_MM)")]
        [InlineData("ISO_A2_(594.00_x_420.00_MM) ")]
        [InlineData("ISO_A2_(594.00_x_420.00_MM)\nX")]
        [InlineData("")]
        public void ASheetNamedWithoutItsSizeInMillimetres_IsRefused_AndNothingIsWritten(string name)
        {
            DxfLayout plan = Added("AC1032", out DxfDatabase db);
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => plan.CanonicalMediaName = name);
            Assert.Throws<ArgumentNullException>(() => plan.CanonicalMediaName = null);
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void TheSheetOfALayoutNotAdded_IsRefused_AsAreMarginsThatLeaveNoRoom_AndValuesOfNoKind()
        {
            var loose = new DxfLayout { LayoutName = "Loose" };
            Assert.Throws<InvalidOperationException>(() => loose.CanonicalMediaName = "ISO_A2_(594.00_x_420.00_MM)");
            Assert.Throws<InvalidOperationException>(() =>
                loose.PlotPaperMargins = new DxfExtents2d(new GeoPoint2(1, 1), new GeoPoint2(1, 1))
            );
            Assert.Throws<InvalidOperationException>(() => loose.PlotOrigin = new GeoPoint2(1, 1));
            Assert.Throws<InvalidOperationException>(() => loose.PlotPaperUnits = DxfPlotPaperUnit.Inches);
            Assert.Throws<InvalidOperationException>(() => loose.PlotRotation = DxfPlotRotation.Degrees090);

            DxfLayout plan = Added("AC1032", out DxfDatabase db);
            string before = db.ToText();

            // A3 is 420 by 297: margins of 210 at either side, or 148.5 above and below, leave none.
            Assert.Throws<ArgumentException>(() =>
                plan.PlotPaperMargins = new DxfExtents2d(new GeoPoint2(210, 20), new GeoPoint2(210, 20))
            );
            Assert.Throws<ArgumentException>(() =>
                plan.PlotPaperMargins = new DxfExtents2d(new GeoPoint2(7.5, 148.5), new GeoPoint2(7.5, 148.5))
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                plan.PlotPaperMargins = new DxfExtents2d(new GeoPoint2(-1, 20), new GeoPoint2(7.5, 20))
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                plan.PlotPaperMargins = new DxfExtents2d(new GeoPoint2(7.5, double.NaN), new GeoPoint2(7.5, 20))
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                plan.PlotPaperMargins = new DxfExtents2d(
                    new GeoPoint2(7.5, 20),
                    new GeoPoint2(double.PositiveInfinity, 20)
                )
            );
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.PlotOrigin = new GeoPoint2(double.NaN, 0));
            Assert.Throws<ArgumentException>(() => plan.PlotPaperUnits = (DxfPlotPaperUnit)3);
            Assert.Throws<ArgumentException>(() => plan.PlotRotation = (DxfPlotRotation)4);

            // A sheet of 20 by 30 has no room for margins of 7.5 and 20.
            Assert.Throws<ArgumentException>(() => plan.CanonicalMediaName = "Tiny_(20.00_x_30.00_MM)");
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void TheSheetOfAnEzdxfLayout_SetToA2_ChangesItsLayoutAlone_AndWhatItHasChangesNothing()
        {
            // ezdxf's Sheet A: ezdxf_(420.00_x_297.00_MM), margins 10, limits (-10, -10) to (410, 287).
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayout sheet = db.Layouts[2];

            sheet.CanonicalMediaName = "ezdxf_(420.00_x_297.00_MM)";
            sheet.PlotPaperMargins = new DxfExtents2d(new GeoPoint2(10, 10), new GeoPoint2(10, 10));
            sheet.PlotOrigin = new GeoPoint2(0, 0);
            sheet.PlotPaperUnits = DxfPlotPaperUnit.Millimeters;
            sheet.PlotRotation = DxfPlotRotation.Degrees000;
            Assert.Equal(text, db.ToText());

            sheet.CanonicalMediaName = "ISO_A2_(594.00_x_420.00_MM)";

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, sheet.Handle);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-10, -10), new GeoPoint2(584, 410)), sheet.Limits);

            // The other groups of the record as ezdxf wrote them: only 4, 44, 45, 11 and 21 differ.
            string[] was = Records(text).Single(r => HandleOf(r) == sheet.Handle).Split('\n');
            string[] now = Records(saved).Single(r => HandleOf(r) == sheet.Handle).Split('\n');
            Assert.Equal(was.Length, now.Length);
            var changed = new List<string>();

            for (int i = 1; i < was.Length; i += 2)
            {
                if (was[i] != now[i])
                {
                    changed.Add(was[i - 1].Trim() + "=" + now[i]);
                }
            }

            Assert.Equal(new[] { "4=ISO_A2_(594.00_x_420.00_MM)", "44=594", "45=420", "11=584", "21=410" }, changed);

            DxfLayout read = DxfDatabase.Parse(saved).Layouts[2];
            Assert.Equal(Bits(594.0) + Bits(420.0), Bits(read.PlotPaperSize.X) + Bits(read.PlotPaperSize.Y));
            Assert.Equal("DWG to PDF.pc3", read.PlotConfigurationName);
        }

        [Fact]
        public void TheLimits_FollowTheTurn_TheUnits_AndTheOrigin()
        {
            DxfLayout plan = Added("AC1032", out DxfDatabase db);

            // A3 on its side moved by (5, 0): the corner of the plotted area 12.5 in from the left.
            plan.PlotOrigin = new GeoPoint2(5, 0);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-12.5, -20), new GeoPoint2(407.5, 277)), plan.Limits);
            plan.PlotOrigin = new GeoPoint2(0, 0);

            // In inches, the millimetres of the file divided by 25.4: (-0.295, -0.787) to (16.240, 10.906).
            plan.PlotPaperUnits = DxfPlotPaperUnit.Inches;
            Assert.Equal(
                new DxfExtents2d(
                    new GeoPoint2(-7.5 / 25.4, -20 / 25.4),
                    new GeoPoint2(420 / 25.4 - 7.5 / 25.4, 297 / 25.4 - 20 / 25.4)
                ),
                plan.Limits
            );
            Assert.Equal(Bits(420.0) + Bits(7.5), Bits(plan.PlotPaperSize.X) + Bits(plan.PlotPaperMargins.MinPoint.X));
            plan.PlotPaperUnits = DxfPlotPaperUnit.Millimeters;
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-7.5, -20), new GeoPoint2(412.5, 277)), plan.Limits);

            // An upright A2 turned a quarter lies across, 594 wide and 420 high, its bottom margin 20 at the left.
            plan.CanonicalMediaName = "ISO_A2_(420.00_x_594.00_MM)";
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-7.5, -20), new GeoPoint2(412.5, 574)), plan.Limits);
            plan.PlotRotation = DxfPlotRotation.Degrees090;
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-20, -7.5), new GeoPoint2(574, 412.5)), plan.Limits);
            DxfExtents2d paper = Paper(plan);
            Assert.Equal(plan.Limits, paper);

            plan.PlotRotation = DxfPlotRotation.Degrees180;
            Assert.Equal(new DxfExtents2d(new GeoPoint2(-7.5, -20), new GeoPoint2(412.5, 574)), plan.Limits);

            DxfLayout read = DxfDatabase.Parse(db.ToText()).Layouts.Single(l => l.LayoutName == "Plan");
            Assert.Equal(DxfPlotRotation.Degrees180, read.PlotRotation);
            Assert.Equal(plan.Limits, read.Limits);
        }

        [Fact]
        public void TheModelLayout_KeepsItsLimits_WhenItsSheetIsSet()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayout model = db.Layouts[0];
            DxfExtents2d limits = model.Limits;

            model.PlotPaperMargins = new DxfExtents2d(new GeoPoint2(5, 5), new GeoPoint2(5, 5));

            Assert.Equal(limits, model.Limits);
            Assert.Equal(
                new DxfExtents2d(new GeoPoint2(5, 5), new GeoPoint2(5, 5)),
                DxfDatabase.Parse(db.ToText()).Layouts[0].PlotPaperMargins
            );
        }

        /// <summary>A layout named Plan added to a new drawing, with the sheet of A3 on its side the library gives.</summary>
        private static DxfLayout Added(string version, out DxfDatabase db)
        {
            db = new DxfDatabase(version);
            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(db, null);
            return plan;
        }

        /// <summary>The paper as the viewer's CadSpace.PaperBounds works it out of the plot settings.</summary>
        private static DxfExtents2d Paper(DxfLayout layout)
        {
            GeoPoint2 size = layout.PlotPaperSize;
            double unit = layout.PlotPaperUnits == DxfPlotPaperUnit.Inches ? 1.0 / 25.4 : 1.0;
            DxfExtents2d margins = layout.PlotPaperMargins;
            GeoPoint2 origin = layout.PlotOrigin;
            bool turned =
                layout.PlotRotation == DxfPlotRotation.Degrees090 || layout.PlotRotation == DxfPlotRotation.Degrees270;
            double width = (turned ? size.Y : size.X) * unit;
            double height = (turned ? size.X : size.Y) * unit;
            double left = ((turned ? margins.MinPoint.Y : margins.MinPoint.X) + origin.X) * unit;
            double bottom = ((turned ? margins.MinPoint.X : margins.MinPoint.Y) + origin.Y) * unit;
            return new DxfExtents2d(new GeoPoint2(-left, -bottom), new GeoPoint2(width - left, height - bottom));
        }
    }
}
