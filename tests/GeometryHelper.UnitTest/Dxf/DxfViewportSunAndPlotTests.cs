using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The SUN a viewport is lit by, made in code, given by SetSun, set and read back; and whether a viewport is plotted as wires
    /// or as a raster image, by its shading of the plot, its visual style and its render mode. ezdxf 1.4.5 read the probe's
    /// suns back with the values set, owned by their viewports, its recover and audit finding nothing.
    /// </summary>
    public class DxfViewportSunAndPlotTests
    {
        [Fact]
        public void ASunMadeInCode_IsAutoCADsAndEzdxfs()
        {
            var sun = new DxfSun();

            Assert.True(sun.IsOn);
            Assert.Equal(DxfColor.FromRgb(255, 255, 255), sun.SunColor);
            Assert.Equal(Bits(1.0), Bits(sun.Intensity));
            Assert.Equal(new DateTime(2014, 9, 21, 12, 0, 0), sun.DateTime);
            Assert.False(sun.IsDaylightSavingsOn);
            Assert.True(sun.ShadowsOn);
            Assert.Equal(DxfShadowType.RayTraced, sun.ShadowType);
            Assert.Equal(256, sun.ShadowMapSize);
            Assert.Equal(1, sun.ShadowMapSoftness);
            Assert.Null(sun.Handle);
            Assert.Equal("SUN , on", sun.ToString());
        }

        [Fact]
        public void ASunGivenToAViewport_IsWrittenOwnedByIt_AndReadsBackAsSet()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");
            Assert.Null(viewport.Sun);

            var made = new DxfSun
            {
                DateTime = new DateTime(2026, 6, 21, 15, 0, 0, 750),
                Intensity = 1.5,
                SunColor = DxfColor.FromRgb(255, 240, 200),
                IsDaylightSavingsOn = true,
                ShadowType = DxfShadowType.Maps,
                ShadowMapSize = 1024,
                ShadowMapSoftness = 3,
            };
            DxfSun sun = viewport.SetSun(made);

            Assert.Same(made, sun);
            Assert.Same(sun, viewport.Sun);
            Assert.Equal(viewport.Handle, sun.OwnerHandle);
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "40", sun.Handle);
            Assert.Contains("361\n" + sun.Handle + "\n", Records(saved).Single(r => HandleOf(r) == "40"), StringComparison.Ordinal);

            // 21 June 2026 is the Julian day 2461213, 15:00:00.75 the 54 000th second; the true colour 0xFFF0C8 with the index
            // nearest it.
            Assert.Equal(
                "  0\nSUN\n  5\n" + sun.Handle + "\n102\n{ACAD_REACTORS\n330\n40\n102\n}\n330\n40\n100\nAcDbSun\n 90\n1\n290\n1\n 63\n" + sun.SunColor.ColorIndex
                + "\n421\n16773320\n 40\n1.5\n291\n1\n 91\n2461213\n 92\n54000\n292\n1\n 70\n1\n 71\n1024\n280\n3\n",
                Records(saved).Single(r => HandleOf(r) == sun.Handle));

            DxfDatabase again = DxfDatabase.Parse(saved);
            DxfSun read = ByHandle<DxfViewport>(again, "40").Sun;
            Assert.Same(read, again.GetObject(sun.Handle));
            Assert.Equal(new DateTime(2026, 6, 21, 15, 0, 0), read.DateTime);
            Assert.Equal(Bits(1.5), Bits(read.Intensity));
            Assert.Equal(DxfColor.FromRgb(255, 240, 200), read.SunColor);
            Assert.True(read.IsDaylightSavingsOn);
            Assert.Equal(DxfShadowType.Maps, read.ShadowType);
            Assert.Equal(1024, read.ShadowMapSize);
            Assert.Equal(3, read.ShadowMapSoftness);
            Assert.True(read.IsOn);
            Assert.True(read.ShadowsOn);
        }

        [Fact]
        public void ASunOfTheFile_IsSetInPlace_AndSettingWhatItHasChangesNothing()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfSun sun = ByHandle<DxfViewport>(db, "40").SetSun(new DxfSun());
            string before = db.ToText();

            sun.IsOn = sun.IsOn;
            sun.SunColor = sun.SunColor;
            sun.Intensity = sun.Intensity;
            sun.DateTime = sun.DateTime;
            sun.IsDaylightSavingsOn = sun.IsDaylightSavingsOn;
            sun.ShadowsOn = sun.ShadowsOn;
            sun.ShadowType = sun.ShadowType;
            sun.ShadowMapSize = sun.ShadowMapSize;
            sun.ShadowMapSoftness = sun.ShadowMapSoftness;
            sun.DateTime = sun.DateTime.AddMilliseconds(400);
            Assert.Equal(before, db.ToText());

            sun.IsOn = false;
            sun.Intensity = 0.25;
            sun.ShadowsOn = false;
            sun.SunColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 30);
            sun.DateTime = new DateTime(2026, 12, 21, 9, 30, 0);
            OnlyTheseChanged(before, db.ToText(), sun.Handle);

            // The colour of the index written as the 63 alone, the true colour taken out; the others in their places.
            string record = Records(db.ToText()).Single(r => HandleOf(r) == sun.Handle);
            Assert.Contains("100\nAcDbSun\n 90\n1\n290\n0\n 63\n30\n 40\n0.25\n291\n0\n 91\n2461396\n 92\n34200\n", record, StringComparison.Ordinal);
            DxfSun read = (DxfSun)DxfDatabase.Parse(db.ToText()).GetObject(sun.Handle);
            Assert.Equal(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 30), read.SunColor);
            Assert.False(read.IsOn);
            Assert.Equal(new DateTime(2026, 12, 21, 9, 30, 0), read.DateTime);
        }

        [Fact]
        public void ASecondSun_ErasesTheFirst_AndTheViewportErasedTakesItsSunWithIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");
            DxfSun first = viewport.SetSun(new DxfSun());
            DxfSun second = viewport.SetSun(new DxfSun { IsOn = false });

            Assert.True(first.IsErased);
            Assert.False(second.IsErased);
            Assert.Same(second, viewport.Sun);
            Assert.Single(Records(db.ToText()), r => r.StartsWith("  0\nSUN\n", StringComparison.Ordinal));

            viewport.Erase();
            Assert.DoesNotContain(Records(db.ToText()), r => r.StartsWith("  0\nSUN\n", StringComparison.Ordinal));
        }

        [Fact]
        public void TheSunOfAnotherViewport_IsKeptWhenAViewportNamingItIsGivenItsOwn()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfSun shared = ByHandle<DxfViewport>(db, "40").SetSun(new DxfSun());

            // Viewport 41 names the sun viewport 40 owns.
            DxfDatabase again = DxfDatabase.Parse(db.ToText().Replace(" 68\n0\n 69\n4\n", " 68\n0\n 69\n4\n361\n" + shared.Handle + "\n"));
            DxfViewport other = ByHandle<DxfViewport>(again, "41");
            Assert.Equal(shared.Handle, other.Sun.Handle);

            DxfSun own = other.SetSun(new DxfSun { Intensity = 2 });

            Assert.False(again.GetObject(shared.Handle).IsErased);
            Assert.Same(again.GetObject(shared.Handle), ByHandle<DxfViewport>(again, "40").Sun);
            Assert.Same(own, other.Sun);
            Assert.Equal(2, Records(again.ToText()).Count(r => r.StartsWith("  0\nSUN\n", StringComparison.Ordinal)));
        }

        [Fact]
        public void ASunGivenToADrawingThatListsNoClassSun_ListsIt_AsAutoCADDoes()
        {
            const string sunClass = "  0\nCLASS\n  1\nSUN\n  2\nAcDbSun\n  3\nSCENEOE\n 90\n1153\n 91\n0\n280\n0\n281\n0\n";
            string text = DxfObjectsEzdxfDrawings.R2018.Replace(sunClass, string.Empty);
            Assert.DoesNotContain("\nAcDbSun\n", text, StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(text);

            ByHandle<DxfViewport>(db, "40").SetSun(new DxfSun());
            ByHandle<DxfViewport>(db, "41").SetSun(new DxfSun());

            // The class listed once, last of the CLASSES section, its application SCENEOE and its flags 1153 as AutoCAD writes it.
            string saved = db.ToText();
            Assert.Single(Records(saved), r => r == sunClass);
            int classes = saved.IndexOf("\nCLASSES\n", StringComparison.Ordinal);
            Assert.Equal(saved.IndexOf("  0\nENDSEC\n", classes, StringComparison.Ordinal), saved.IndexOf(sunClass, StringComparison.Ordinal) + sunClass.Length);

            // A drawing that lists it is given none again.
            DxfDatabase listed = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            ByHandle<DxfViewport>(listed, "40").SetSun(new DxfSun());
            Assert.Single(Records(listed.ToText()), r => r == sunClass);
        }

        [Fact]
        public void ADateSetOnASunWhoseDayAndTimeAreUnreadable_IsWritten_ThoughItIsTheOneShown()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfSun sun = ByHandle<DxfViewport>(db, "40").SetSun(new DxfSun());
            DxfDatabase again = DxfDatabase.Parse(db.ToText().Replace(" 91\n2456922\n 92\n43200\n", " 91\nnoon\n 92\n\n"));
            DxfSun read = (DxfSun)again.GetObject(sun.Handle);
            Assert.Equal(new DateTime(2014, 9, 21, 12, 0, 0), read.DateTime);

            read.DateTime = new DateTime(2014, 9, 21, 12, 0, 0);

            Assert.Contains(" 91\n2456922\n 92\n43200\n", Records(again.ToText()).Single(r => HandleOf(r) == sun.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void ASunGivenWrongly_OrSetOutsideItsRange_IsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");
            DxfSun sun = viewport.SetSun(new DxfSun());
            string text = db.ToText();

            Assert.Throws<ArgumentNullException>(() => viewport.SetSun(null));
            Assert.Throws<ArgumentException>(() => ByHandle<DxfViewport>(db, "41").SetSun(sun));
            Assert.Throws<InvalidOperationException>(() => ByHandle<DxfViewport>(DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2000), "40").SetSun(new DxfSun()));
            Assert.Throws<InvalidOperationException>(() => new DxfViewport().SetSun(new DxfSun()));
            Assert.Throws<ArgumentOutOfRangeException>(() => sun.ShadowType = DxfShadowType.Sampled);
            Assert.Throws<ArgumentOutOfRangeException>(() => sun.ShadowMapSize = 100);
            Assert.Throws<ArgumentOutOfRangeException>(() => sun.ShadowMapSize = 8192);
            Assert.Throws<ArgumentOutOfRangeException>(() => sun.ShadowMapSoftness = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => sun.ShadowMapSoftness = 11);
            Assert.Throws<ArgumentOutOfRangeException>(() => sun.Intensity = -0.5);
            Assert.Throws<ArgumentOutOfRangeException>(() => sun.Intensity = double.NaN);
            Assert.Throws<ArgumentNullException>(() => sun.SunColor = null);
            Assert.Throws<ArgumentException>(() => sun.SunColor = DxfColor.ByLayer);
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxfSun { ShadowMapSize = 300 });
            Assert.Equal(text, db.ToText());
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void AViewportPlottedAsDisplayed_IsPlottedAsItsRenderModeShowsIt_AndItsShadingOfThePlotOtherwise(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018);
            DxfViewport shown = ByHandle<DxfViewport>(db, "3F");

            // ezdxf: render mode 0, 2D optimized, and no shading of the plot: as wires.
            Assert.True(shown.PlotWireframe);
            Assert.False(shown.PlotAsRaster);

            // The lines behind hidden in the plot, by the flag 0x800 of a viewport with no 170: neither.
            shown.HiddenLinesRemoved = true;
            Assert.False(shown.PlotWireframe);
            Assert.False(shown.PlotAsRaster);

            if (version == "R2018")
            {
                // ezdxf: 170 of 2, hidden: neither, whatever the flag.
                DxfViewport hidden = ByHandle<DxfViewport>(db, "40");
                Assert.False(hidden.PlotWireframe);
                Assert.False(hidden.PlotAsRaster);

                shown.SetShadePlot(DxfShadePlotType.Wireframe, null);
                Assert.True(shown.PlotWireframe);
                shown.SetShadePlot(DxfShadePlotType.Rendered, null);
                Assert.True(shown.PlotAsRaster);
                Assert.False(shown.PlotWireframe);
                shown.SetShadePlot(DxfShadePlotType.RenderPreset, db.NamedObjectsDictionary);
                Assert.True(shown.PlotAsRaster);
            }
        }

        [Theory]
        [InlineData("2", false, false)]
        [InlineData("4", false, true)]
        [InlineData("6", false, true)]
        public void TheRenderModeOfAViewport_SaysHowItIsPlottedAsDisplayed(string mode, bool wires, bool raster)
        {
            // The viewport 1 of Sheet A given a render mode ahead of its own 0: 2 hidden, 4 Gouraud, 6 Gouraud with wires.
            string text = DxfObjectsEzdxfDrawings.R2018.Replace(" 68\n1\n 69\n1\n", " 68\n1\n 69\n1\n281\n" + mode + "\n");
            DxfViewport viewport = ByHandle<DxfViewport>(DxfDatabase.Parse(text), "3F");

            Assert.Equal(wires, viewport.PlotWireframe);
            Assert.Equal(raster, viewport.PlotAsRaster);
        }

        [Fact]
        public void AVisualStyle_SaysHowAViewportIsPlotted_NamedByItsShadingOrShownIn()
        {
            // Three visual styles: 2D wireframe (4), hidden (6) and realistic (8); viewport 41 shown in the realistic one.
            string styles = DxfLines.G(
                0, "VISUALSTYLE", 5, "F01", 330, "A", 100, "AcDbVisualStyle", 2, "2dWireframe", 70, 4,
                0, "VISUALSTYLE", 5, "F02", 330, "A", 100, "AcDbVisualStyle", 2, "Hidden", 70, 6,
                0, "VISUALSTYLE", 5, "F03", 330, "A", 100, "AcDbVisualStyle", 2, "Realistic", 70, 8);
            string text = DxfObjectsEzdxfDrawings.R2018.Replace("  0\nENDSEC\n  0\nEOF\n", styles + "  0\nENDSEC\n  0\nEOF\n")
                .Replace(" 68\n0\n 69\n4\n", " 68\n0\n 69\n4\n348\nF03\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfViewport shown = ByHandle<DxfViewport>(db, "41");

            Assert.True(shown.PlotAsRaster);
            Assert.False(shown.PlotWireframe);

            DxfViewport viewport = ByHandle<DxfViewport>(db, "3F");
            viewport.SetShadePlot(DxfShadePlotType.VisualStyle, db.GetObject("F01"));
            Assert.True(viewport.PlotWireframe);
            viewport.SetShadePlot(DxfShadePlotType.VisualStyle, db.GetObject("F02"));
            Assert.False(viewport.PlotWireframe);
            Assert.False(viewport.PlotAsRaster);
            viewport.SetShadePlot(DxfShadePlotType.VisualStyle, db.GetObject("F03"));
            Assert.True(viewport.PlotAsRaster);

            // A visual style the drawing has not: as displayed, by its render mode 0.
            viewport.SetShadePlot(DxfShadePlotType.VisualStyle, db.NamedObjectsDictionary);
            Assert.True(viewport.PlotWireframe);
        }
    }
}
