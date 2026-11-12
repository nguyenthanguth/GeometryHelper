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
    /// Named page setups (PLOTSETTINGS) made in code and added to ACAD_PLOTSETTINGS, written as AutoCAD 2021 writes one
    /// (checked by dumping a page setup its API made: the class listed with proxy flags 0, group 1 the name, the groups of
    /// AcDbPlotSettings in their order), and copied to and from a layout as AutoCAD's <c>CopyFrom</c> copies them: the name
    /// goes with the rest, the layout stays model space's.
    /// </summary>
    public class DxfPageSetupTests
    {
        /// <summary>A drawing of R2018 made in code with a dictionary of page setups.</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1032");

            if (db.PlotSettingsDictionary == null)
            {
                db.NamedObjectsDictionary.SetAt("ACAD_PLOTSETTINGS", new DxfDBDictionary());
            }

            return db;
        }

        private static DxfPlotSettings Added(DxfDatabase db, string name)
        {
            var setup = new DxfPlotSettings();
            db.PlotSettingsDictionary.SetAt(name, setup);
            return setup;
        }

        [Fact]
        public void APageSetupAdded_IsWrittenAsAPlotSettings_NamedByItsEntry_AndItsClassIsListed()
        {
            DxfDatabase db = Drawing();
            DxfPlotSettings setup = Added(db, "A3 plan");

            Assert.Equal("A3 plan", setup.PlotSettingsName);
            Assert.Same(setup, db.PlotSettingsDictionary["A3 plan"]);
            string text = db.ToText().Replace("\r\n", "\n");
            string record = Records(text).Single(r => HandleOf(r) == setup.Handle);
            Assert.StartsWith("  0\nPLOTSETTINGS\n", record, StringComparison.Ordinal);
            Assert.Contains(
                "100\nAcDbPlotSettings\n  1\nA3 plan\n  2\nnone_device\n",
                record,
                StringComparison.Ordinal
            );
            Assert.Contains(
                "  0\nCLASS\n  1\nPLOTSETTINGS\n  2\nAcDbPlotSettings\n  3\nObjectDBX Classes\n 90\n0\n",
                text,
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void APageSetupAdded_TakesItsSettings_AndReadsBackAfterSaving()
        {
            DxfDatabase db = Drawing();
            DxfPlotSettings setup = Added(db, "A4 upright");
            setup.PlotConfigurationName = "GeometryHelper PDF";
            setup.CanonicalMediaName = "ISO_A4_(210.00_x_297.00_MM)";
            setup.PlotType = DxfPlotType.Window;
            setup.PlotWindowArea = new DxfExtents2d(new GeoPoint2(120, 60), new GeoPoint2(7620, 5360));
            setup.CustomPrintScale = new DxfCustomScale(1, 20);
            setup.CurrentStyleSheet = "monochrome.ctb";

            var again = (DxfPlotSettings)Opened(Saved(db)).PlotSettingsDictionary["A4 upright"];

            Assert.Equal("A4 upright", again.PlotSettingsName);
            Assert.Equal("GeometryHelper PDF", again.PlotConfigurationName);
            Assert.Equal("ISO_A4_(210.00_x_297.00_MM)", again.CanonicalMediaName);
            Assert.Equal(DxfPlotType.Window, again.PlotType);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(120, 60), new GeoPoint2(7620, 5360)), again.PlotWindowArea);
            Assert.Equal(new DxfCustomScale(1, 20), again.CustomPrintScale);
            Assert.Equal("monochrome.ctb", again.CurrentStyleSheet);
        }

        [Fact]
        public void APageSetupAddedUnderANameTaken_ReplacesTheOldOne()
        {
            DxfDatabase db = Drawing();
            Added(db, "Plan").PlotConfigurationName = "first";
            Added(db, "plan").PlotConfigurationName = "second";

            Assert.Single(db.PlotSettingsDictionary);
            Assert.Equal("second", ((DxfPlotSettings)db.PlotSettingsDictionary["Plan"]).PlotConfigurationName);
        }

        [Fact]
        public void CopyFrom_GivesALayoutTheSettingsAndTheNameOfAPageSetup_ButKeepsItModelSpaces()
        {
            DxfDatabase db = Drawing();
            DxfPlotSettings setup = Added(db, "A3 plan");
            setup.PlotConfigurationName = "GeometryHelper PDF";
            setup.CanonicalMediaName = "ISO_A3_(420.00_x_297.00_MM)";
            setup.PlotRotation = DxfPlotRotation.Degrees090;
            setup.PlotType = DxfPlotType.Window;
            setup.PlotWindowArea = new DxfExtents2d(new GeoPoint2(120, 60), new GeoPoint2(7620, 5360));
            setup.CustomPrintScale = new DxfCustomScale(1, 20);
            setup.UseStandardScale = false;
            setup.PlotCentered = true;
            setup.CurrentStyleSheet = "monochrome.ctb";
            DxfLayout model = db.Layouts.Single(l => l.ModelType);

            model.CopyFrom(setup);

            Assert.Equal("A3 plan", model.PlotSettingsName);
            Assert.True(model.ModelType);
            Assert.Equal("GeometryHelper PDF", model.PlotConfigurationName);
            Assert.Equal("ISO_A3_(420.00_x_297.00_MM)", model.CanonicalMediaName);
            Assert.Equal(new GeoPoint2(420, 297), model.PlotPaperSize);
            Assert.Equal(DxfPlotRotation.Degrees090, model.PlotRotation);
            Assert.Equal(DxfPlotType.Window, model.PlotType);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(120, 60), new GeoPoint2(7620, 5360)), model.PlotWindowArea);
            Assert.Equal(new DxfCustomScale(1, 20), model.CustomPrintScale);
            Assert.False(model.UseStandardScale);
            Assert.True(model.PlotCentered);
            Assert.Equal("monochrome.ctb", model.CurrentStyleSheet);
        }

        [Fact]
        public void CopyFrom_MakesAPageSetupOfALayout_TheNameStaysTheEntrys()
        {
            DxfDatabase db = Drawing();
            DxfLayout model = db.Layouts.Single(l => l.ModelType);
            model.PlotConfigurationName = "GeometryHelper PDF";
            model.CanonicalMediaName = "ISO_A2_(594.00_x_420.00_MM)";
            model.PlotType = DxfPlotType.Extents;
            DxfPlotSettings setup = Added(db, "Sheet");

            setup.CopyFrom(model);
            setup.PlotSettingsName = "Sheet";

            Assert.Equal("Sheet", setup.PlotSettingsName);
            Assert.Equal("ISO_A2_(594.00_x_420.00_MM)", setup.CanonicalMediaName);
            Assert.Equal(new GeoPoint2(594, 420), setup.PlotPaperSize);
            Assert.Equal(DxfPlotType.Extents, setup.PlotType);
            Assert.False(setup.ModelType);
        }

        [Fact]
        public void APageSetupMadeInCodeAndNotYetAdded_RefusesToBeCopiedInto()
        {
            DxfDatabase db = Drawing();
            DxfLayout model = db.Layouts.Single(l => l.ModelType);

            Assert.Throws<InvalidOperationException>(() => new DxfPlotSettings().CopyFrom(model));
            Assert.Throws<ArgumentNullException>(() => model.CopyFrom(null));
        }

        [Fact]
        public void APageSetupSavedAsDwg_ReadsBackWithItsNameAndItsSettings()
        {
            DxfDatabase db = Drawing();
            DxfPlotSettings setup = Added(db, "A3 plan");
            setup.PlotConfigurationName = "GeometryHelper PDF";
            setup.CanonicalMediaName = "ISO_A3_(420.00_x_297.00_MM)";
            setup.PlotRotation = DxfPlotRotation.Degrees090;
            setup.PlotType = DxfPlotType.Window;
            setup.PlotWindowArea = new DxfExtents2d(new GeoPoint2(120, 60), new GeoPoint2(7620, 5360));
            setup.CustomPrintScale = new DxfCustomScale(1, 20);
            setup.CurrentStyleSheet = "monochrome.ctb";
            Added(db, "Sheet B").PlotConfigurationName = "Microsoft Print to PDF";

            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(db, stream);
                DxfDatabase again = DwgReader.Open(new MemoryStream(stream.ToArray(), false));

                Assert.Equal(
                    new[] { "A3 plan", "Sheet B" },
                    again.PlotSettingsDictionary.Select(e => e.Key).OrderBy(k => k)
                );
                var read = (DxfPlotSettings)again.PlotSettingsDictionary["A3 plan"];
                Assert.Equal("A3 plan", read.PlotSettingsName);
                Assert.Equal("GeometryHelper PDF", read.PlotConfigurationName);
                Assert.Equal("ISO_A3_(420.00_x_297.00_MM)", read.CanonicalMediaName);
                Assert.Equal(DxfPlotRotation.Degrees090, read.PlotRotation);
                Assert.Equal(DxfPlotType.Window, read.PlotType);
                Assert.Equal(new DxfExtents2d(new GeoPoint2(120, 60), new GeoPoint2(7620, 5360)), read.PlotWindowArea);
                Assert.Equal(new DxfCustomScale(1, 20), read.CustomPrintScale);
                Assert.Equal("monochrome.ctb", read.CurrentStyleSheet);
                Assert.Equal(
                    "Microsoft Print to PDF",
                    ((DxfPlotSettings)again.PlotSettingsDictionary["Sheet B"]).PlotConfigurationName
                );
            }
        }

        [Fact]
        public void ALayoutIsNotAddedToAPageSetupDictionary()
        {
            DxfDatabase db = Drawing();

            Assert.Throws<ArgumentException>(() => db.PlotSettingsDictionary.SetAt("Plan", new DxfLayout()));
        }
    }
}
