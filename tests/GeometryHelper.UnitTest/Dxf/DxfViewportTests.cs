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
    /// VIEWPORTs read as ezdxf 1.4.5 reads them, from their own groups (R2000, R2018) and from their extended data MVIEW
    /// (R12); set, turned on and off, their layers frozen and thawed, moved and scaled, and appended; ezdxf read the probe's
    /// viewports back, its recover and audit finding nothing.
    /// </summary>
    public class DxfViewportTests
    {
        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void AViewportOfR2000On_IsReadAsEzdxfReadsIt(string version)
        {
            DxfDatabase db = DxfDatabase.Parse(Drawing(version));
            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");

            // ezdxf: center (150, 100, 0), 200 x 150, status 2, id 3, view center (5000, 3000), view height 7500, target
            // (10, 20, 0), direction (0, 0, 1), lens 35, clips 2.5 and -4, snap 30 deg about (5, 7) by (2.5, 2.5), grid (20, 25),
            // twist 15 deg, circle zoom 1000, flags 21280, plot style monochrome.ctb, ucs per viewport 1, ucs (100, 200, 0)
            // x (0, 1, 0) y (-1, 0, 0), elevation 12.5, frozen [hidden].
            Assert.Equal(Bits(new GeoPoint3(150, 100, 0)), Bits(viewport.CenterPoint));
            Assert.Equal(Bits(200.0), Bits(viewport.Width));
            Assert.Equal(Bits(150.0), Bits(viewport.Height));
            Assert.Equal(3, viewport.Number);
            Assert.True(viewport.On);
            Assert.Equal(Bits(5000.0) + Bits(3000.0), Bits(viewport.ViewCenter.X) + Bits(viewport.ViewCenter.Y));
            Assert.Equal(Bits(7500.0), Bits(viewport.ViewHeight));
            Assert.Equal(Bits(new GeoPoint3(10, 20, 0)), Bits(viewport.ViewTarget));
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(viewport.ViewDirection));
            Assert.Equal(DxfOrthographicView.TopView, viewport.ViewOrthographic);
            Assert.Equal(Bits(35.0), Bits(viewport.LensLength));
            Assert.Equal(Bits(2.5), Bits(viewport.FrontClipDistance));
            Assert.Equal(Bits(-4.0), Bits(viewport.BackClipDistance));
            Assert.Equal(15.0 * Math.PI / 180.0, viewport.TwistAngle, 15);
            Assert.Equal(30.0 * Math.PI / 180.0, viewport.SnapAngle, 15);
            Assert.Equal(Bits(5.0) + Bits(7.0), Bits(viewport.SnapBasePoint.X) + Bits(viewport.SnapBasePoint.Y));
            Assert.Equal(Bits(2.5) + Bits(2.5), Bits(viewport.SnapIncrement.X) + Bits(viewport.SnapIncrement.Y));
            Assert.Equal(Bits(20.0) + Bits(25.0), Bits(viewport.GridIncrement.X) + Bits(viewport.GridIncrement.Y));
            Assert.Equal(1000, viewport.CircleSides);
            Assert.Equal(0.02, viewport.CustomScale, 15);
            Assert.Equal(DxfStandardScaleType.Scale1To50, viewport.StandardScale);

            // 21280: 0x4000 locked, 0x1000 isopair top, 0x200 grid, 0x100 snap, 0x20 icon.
            Assert.True(viewport.Locked);
            Assert.Equal(1, viewport.SnapIsoPair);
            Assert.True(viewport.GridOn);
            Assert.True(viewport.SnapOn);
            Assert.True(viewport.UcsIconVisible);
            Assert.False(viewport.UcsIconAtOrigin);
            Assert.False(viewport.PerspectiveOn);
            Assert.False(viewport.FrontClipOn);
            Assert.False(viewport.BackClipOn);
            Assert.True(viewport.FrontClipAtEyeOn);
            Assert.False(viewport.FastZoomOn);
            Assert.False(viewport.SnapIsometric);
            Assert.False(viewport.HiddenLinesRemoved);
            Assert.False(viewport.NonRectClipOn);
            Assert.True(viewport.GridBoundToLimits);

            Assert.Equal("monochrome.ctb", viewport.PlotStyleSheet);
            Assert.Equal("monochrome.ctb", viewport.EffectivePlotStyleSheet);
            Assert.True(viewport.UcsPerViewport);
            viewport.GetUcs(out GeoPoint3 origin, out GeoVector3 x, out GeoVector3 y);
            Assert.Equal(Bits(new GeoPoint3(100, 200, 0)), Bits(origin));
            Assert.Equal(Bits(new GeoVector3(0, 1, 0)), Bits(x));
            Assert.Equal(Bits(new GeoVector3(-1, 0, 0)), Bits(y));
            Assert.Equal(1.0, viewport.Ucs.ZAxis.Z, 15);
            Assert.Equal(DxfOrthographicView.NonOrthoView, viewport.UcsOrthographic);
            Assert.Null(viewport.UcsName);
            Assert.Equal(Bits(12.5), Bits(viewport.Elevation));
            Assert.Equal(new[] { "hidden" }, viewport.GetFrozenLayers().Select(l => l.Name));
            Assert.True(viewport.IsLayerFrozenInViewport(db.LayerTable["hidden"]));
            Assert.False(viewport.IsLayerFrozenInViewport(db.LayerTable["steel"]));
            Assert.Null(viewport.Background);

            // ezdxf writes the groups of R2007 only from R2007: shade plot 2, grid major 4, lighting off of type 1,
            // brightness 10, contrast -5, ambient light 250.
            bool r2018 = version == "R2018";
            Assert.Equal(r2018 ? DxfShadePlotType.Hidden : DxfShadePlotType.AsDisplayed, viewport.ShadePlot);
            Assert.Equal(r2018 ? 4 : 5, viewport.GridMajor);
            Assert.Equal(!r2018, viewport.DefaultLightingOn);
            Assert.Equal(r2018 ? DxfDefaultLightingType.TwoDistantLights : DxfDefaultLightingType.OneDistantLight, viewport.DefaultLightingType);
            Assert.Equal(Bits(r2018 ? 10.0 : 0.0), Bits(viewport.Brightness));
            Assert.Equal(Bits(r2018 ? -5.0 : 0.0), Bits(viewport.Contrast));
            Assert.Equal(250, viewport.AmbientLightColor.ColorIndex);

            DxfViewport off = ByHandle<DxfViewport>(db, "41");
            Assert.False(off.On);
            Assert.Equal(4, off.Number);
            Assert.Equal(1, ByHandle<DxfViewport>(db, "3F").Number);
        }

        [Fact]
        public void AViewportOfR12_IsReadFromItsExtendedDataMview()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "35");

            Assert.Equal(Bits(new GeoPoint3(150, 100, 0)), Bits(viewport.CenterPoint));
            Assert.Equal(Bits(200.0), Bits(viewport.Width));
            Assert.Equal(3, viewport.Number);
            Assert.True(viewport.On);
            Assert.Equal(Bits(5000.0) + Bits(3000.0), Bits(viewport.ViewCenter.X) + Bits(viewport.ViewCenter.Y));
            Assert.Equal(Bits(7500.0), Bits(viewport.ViewHeight));
            Assert.Equal(Bits(new GeoPoint3(10, 20, 0)), Bits(viewport.ViewTarget));
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(viewport.ViewDirection));
            Assert.Equal(Bits(35.0), Bits(viewport.LensLength));
            Assert.Equal(Bits(2.5), Bits(viewport.FrontClipDistance));
            Assert.Equal(Bits(-4.0), Bits(viewport.BackClipDistance));
            Assert.Equal(15.0 * Math.PI / 180.0, viewport.TwistAngle, 15);
            Assert.Equal(30.0 * Math.PI / 180.0, viewport.SnapAngle, 15);
            Assert.Equal(Bits(20.0) + Bits(25.0), Bits(viewport.GridIncrement.X) + Bits(viewport.GridIncrement.Y));
            Assert.Equal(1000, viewport.CircleSides);
            Assert.True(viewport.SnapOn);
            Assert.True(viewport.GridOn);
            Assert.False(viewport.Locked);
            Assert.Equal(new[] { "hidden" }, viewport.GetFrozenLayers().Select(l => l.Name));
            Assert.Equal(string.Empty, viewport.PlotStyleSheet);
            Assert.False(viewport.UcsPerViewport);
            Assert.False(ByHandle<DxfViewport>(db, "36").On);
        }

        [Theory]
        [InlineData("R12")]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void SettingWhatAViewportHas_ChangesNothing(string version)
        {
            SettingWhatItHasChangesNothing<DxfViewport>(Drawing(version), v =>
            {
                v.CenterPoint = v.CenterPoint;
                v.Width = v.Width;
                v.Height = v.Height;
                v.On = v.On;
                v.Locked = v.Locked;
                v.NonRectClipOn = v.NonRectClipOn;
                v.ViewCenter = v.ViewCenter;
                v.ViewHeight = v.ViewHeight;
                v.ViewTarget = v.ViewTarget;
                v.ViewDirection = v.ViewDirection;
                v.TwistAngle = v.TwistAngle;
                v.LensLength = v.LensLength;
                v.FrontClipDistance = v.FrontClipDistance;
                v.BackClipDistance = v.BackClipDistance;
                v.PerspectiveOn = v.PerspectiveOn;
                v.FrontClipOn = v.FrontClipOn;
                v.BackClipOn = v.BackClipOn;
                v.FrontClipAtEyeOn = v.FrontClipAtEyeOn;
                v.UcsFollowModeOn = v.UcsFollowModeOn;
                v.HiddenLinesRemoved = v.HiddenLinesRemoved;
                v.FastZoomOn = v.FastZoomOn;
                v.CircleSides = v.CircleSides;
                v.CustomScale = v.CustomScale;
                v.StandardScale = v.StandardScale;
                v.SnapOn = v.SnapOn;
                v.SnapIsometric = v.SnapIsometric;
                v.SnapIsoPair = v.SnapIsoPair;
                v.SnapAngle = v.SnapAngle;
                v.SnapBasePoint = v.SnapBasePoint;
                v.SnapIncrement = v.SnapIncrement;
                v.GridOn = v.GridOn;
                v.GridIncrement = v.GridIncrement;
                v.GridMajor = v.GridMajor;
                v.GridBoundToLimits = v.GridBoundToLimits;
                v.GridAdaptive = v.GridAdaptive;
                v.GridSubdivisionRestricted = v.GridSubdivisionRestricted;
                v.GridFollow = v.GridFollow;
                v.UcsIconVisible = v.UcsIconVisible;
                v.UcsIconAtOrigin = v.UcsIconAtOrigin;
                v.UcsPerViewport = v.UcsPerViewport;
                v.Ucs = v.Ucs;
                v.Elevation = v.Elevation;
                v.PlotStyleSheet = v.PlotStyleSheet;
                v.ShadePlot = v.ShadePlot;
                v.DefaultLightingOn = v.DefaultLightingOn;
                v.DefaultLightingType = v.DefaultLightingType;
                v.Brightness = v.Brightness;
                v.Contrast = v.Contrast;
                v.AmbientLightColor = v.AmbientLightColor;
                v.FreezeLayersInViewport(v.GetFrozenLayers());
                v.ThawLayersInViewport(new DxfLayerTableRecord[0]);
            });
        }

        [Fact]
        public void TheViewSet_IsWrittenInTheViewportsOwnGroups_AndReadBack()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");

            viewport.CenterPoint = new GeoPoint3(160, 110, 0);
            viewport.Width = 180;
            viewport.ViewCenter = new GeoPoint2(4000, 2500);
            viewport.StandardScale = DxfStandardScaleType.Scale1To100;
            viewport.TwistAngle = Math.PI / 2;
            viewport.SetViewDirection(DxfOrthographicView.FrontView);
            viewport.Locked = false;
            viewport.SnapIsoPair = 2;
            viewport.GridSubdivisionRestricted = false;
            viewport.SetUcs(DxfOrthographicView.RightView);
            viewport.PlotStyleSheet = "acad.ctb";
            viewport.ShadePlot = DxfShadePlotType.Rendered;
            viewport.AmbientLightColor = DxfColor.FromRgb(10, 20, 30);

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "40");
            DxfViewport read = ByHandle<DxfViewport>(DxfDatabase.Parse(saved), "40");
            Assert.Equal(Bits(new GeoPoint3(160, 110, 0)), Bits(read.CenterPoint));
            Assert.Equal(Bits(180.0), Bits(read.Width));
            Assert.Equal(Bits(4000.0), Bits(read.ViewCenter.X));
            Assert.Equal(Bits(15000.0), Bits(read.ViewHeight));
            Assert.Equal(DxfStandardScaleType.Scale1To100, read.StandardScale);
            Assert.Equal(Math.PI / 2, read.TwistAngle, 15);
            Assert.Equal(DxfOrthographicView.FrontView, read.ViewOrthographic);
            Assert.False(read.Locked);
            Assert.Equal(2, read.SnapIsoPair);
            Assert.False(read.GridSubdivisionRestricted);
            Assert.Equal(DxfOrthographicView.RightView, read.UcsOrthographic);
            read.GetUcs(out GeoPoint3 origin, out GeoVector3 x, out GeoVector3 y);
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(origin));
            Assert.Equal(Bits(new GeoVector3(0, 1, 0)), Bits(x));
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(y));
            Assert.Equal("acad.ctb", read.PlotStyleSheet);
            Assert.Equal(DxfShadePlotType.Rendered, read.ShadePlot);
            Assert.Equal(0x0A141E, read.AmbientLightColor.ColorValue);
            Assert.True(read.GridOn);

            Assert.Throws<ArgumentOutOfRangeException>(() => viewport.ViewHeight = 0.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => viewport.StandardScale = DxfStandardScaleType.ScaleToFit);
            Assert.Throws<ArgumentOutOfRangeException>(() => viewport.ShadePlot = DxfShadePlotType.VisualStyle);
            Assert.Throws<ArgumentException>(() => viewport.ViewDirection = new GeoVector3(0, 0, 0));
            Assert.Throws<ArgumentException>(() => viewport.SetUcs(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), new GeoVector3(2, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => viewport.SnapIsoPair = 3);
        }

        [Fact]
        public void TheViewOfAViewportOfR12Set_IsWrittenInItsExtendedDataMview()
        {
            string text = DxfObjectsEzdxfDrawings.R12;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "35");

            viewport.ViewHeight = 6000;
            viewport.ViewCenter = new GeoPoint2(4000, 2500);
            viewport.ViewTarget = new GeoPoint3(1, 2, 3);
            viewport.TwistAngle = 0.0;
            viewport.SnapOn = false;
            viewport.PerspectiveOn = true;
            viewport.UcsIconVisible = true;
            viewport.ThawLayersInViewport(new[] { db.LayerTable["hidden"] });
            viewport.FreezeLayersInViewport(new[] { db.LayerTable["steel"], db.LayerTable["notes"] });
            viewport.Width = 210;

            Assert.Throws<InvalidOperationException>(() => viewport.Locked = true);
            Assert.Throws<InvalidOperationException>(() => viewport.Elevation = 1.0);
            Assert.Throws<InvalidOperationException>(() => viewport.SetUcs(DxfOrthographicView.FrontView));
            viewport.SetUcsToWorld();

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "35");
            DxfViewport read = ByHandle<DxfViewport>(DxfDatabase.Parse(saved), "35");
            Assert.Equal(Bits(6000.0), Bits(read.ViewHeight));
            Assert.Equal(Bits(4000.0) + Bits(2500.0), Bits(read.ViewCenter.X) + Bits(read.ViewCenter.Y));
            Assert.Equal(Bits(new GeoPoint3(1, 2, 3)), Bits(read.ViewTarget));
            Assert.Equal(Bits(0.0), Bits(read.TwistAngle));
            Assert.False(read.SnapOn);
            Assert.True(read.GridOn);
            Assert.True(read.PerspectiveOn);
            Assert.True(read.UcsIconVisible);
            Assert.Equal(new[] { "steel", "notes" }, read.GetFrozenLayers().Select(l => l.Name));
            Assert.Equal(Bits(210.0), Bits(read.Width));
            Assert.Contains("1002\n{\n1003\nsteel\n1003\nnotes\n1002\n}\n1002\n}\n", Records(saved).Single(r => HandleOf(r) == "35"), StringComparison.Ordinal);
        }

        [Fact]
        public void LayersFrozenAndThawed_AreTheViewportsGroups331()
        {
            string text = DxfObjectsEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");
            DxfLayerTableRecord steel = db.LayerTable["steel"];

            viewport.FreezeLayersInViewport(new[] { steel, db.LayerTable["hidden"] });
            Assert.Equal(new[] { "hidden", "steel" }, viewport.GetFrozenLayers().Select(l => l.Name));
            Assert.Contains("331\n30\n331\n" + steel.Handle + "\n 90\n", Records(db.ToText()).Single(r => HandleOf(r) == "40"), StringComparison.Ordinal);

            viewport.ThawLayersInViewport(new[] { steel });
            Assert.Equal(text, db.ToText());

            viewport.ThawAllLayersInViewport();
            Assert.Empty(viewport.GetFrozenLayers());
            Assert.DoesNotContain("331\n", Records(db.ToText()).Single(r => HandleOf(r) == "40"), StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => viewport.FreezeLayersInViewport(new[] { new DxfLayerTableRecord { Name = "new" } }));
            Assert.Throws<ArgumentException>(() => viewport.FreezeLayersInViewport(new[] { DxfDatabase.Parse(text).LayerTable["steel"] }));
            Assert.Throws<ArgumentNullException>(() => viewport.FreezeLayersInViewport(null));
        }

        [Fact]
        public void AViewportTurnedOffAndOn_HasItsStatusAndFlagWritten()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfViewport on = ByHandle<DxfViewport>(db, "40");
            DxfViewport off = ByHandle<DxfViewport>(db, "41");

            off.On = true;
            on.On = false;

            Assert.True(off.On);
            Assert.False(on.On);
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "40", "41");
            Assert.Contains(" 68\n3\n", Records(saved).Single(r => HandleOf(r) == "41"), StringComparison.Ordinal);
            Assert.Contains(" 90\n0\n", Records(saved).Single(r => HandleOf(r) == "41"), StringComparison.Ordinal);
            Assert.Contains(" 68\n0\n", Records(saved).Single(r => HandleOf(r) == "40"), StringComparison.Ordinal);
            Assert.Contains(" 90\n152352\n", Records(saved).Single(r => HandleOf(r) == "40"), StringComparison.Ordinal);
        }

        [Fact]
        public void AViewportMovedAndScaled_KeepsItsView_AndATurnIsRefused()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");

            viewport.TransformBy(GeoTransform3.Translation(new GeoVector3(10, 20, 0)).Multiply(GeoTransform3.Scaling(2.0)));

            Assert.Equal(Bits(new GeoPoint3(310, 220, 0)), Bits(viewport.CenterPoint));
            Assert.Equal(Bits(400.0), Bits(viewport.Width));
            Assert.Equal(Bits(300.0), Bits(viewport.Height));
            Assert.Equal(Bits(7500.0), Bits(viewport.ViewHeight));
            Assert.Equal(0.04, viewport.CustomScale, 15);
            OnlyTheseChanged(text, db.ToText(), "40");

            string moved = db.ToText();
            Assert.Throws<ArgumentException>(() => viewport.TransformBy(GeoTransform3.RotationZ(0.5)));
            Assert.Throws<ArgumentException>(() => viewport.TransformBy(GeoTransform3.Scaling(1.0, 2.0, 1.0)));
            Assert.Equal(moved, db.ToText());
        }

        [Fact]
        public void AViewportMadeInCode_GoesToALayoutOfPaperSpace_WithTheFirstFreeNumber()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfBlockTableRecord sheet = db.Layouts[2].BlockTableRecord;
            var viewport = new DxfViewport { CenterPoint = new GeoPoint3(100, 80, 0), Width = 120, Height = 90, ViewHeight = 4500 };
            viewport.ViewCenter = new GeoPoint2(2000, 1000);

            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(new DxfViewport()));
            Assert.Throws<ArgumentException>(() => DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12).PaperSpace.AppendEntity(new DxfViewport()));
            sheet.AppendEntity(viewport);
            viewport.On = true;

            Assert.Equal(2, viewport.Number);
            Assert.True(viewport.On);
            Assert.Equal(DxfStandardScaleType.Scale1To50, viewport.StandardScale);
            Assert.Equal(sheet.Handle, viewport.OwnerHandle);
            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { 1, 3, 4, 2 }, again.Layouts[2].GetViewports().Select(v => v.Number));
            Assert.True(again.Layouts[2].GetViewports()[3].On);
        }

        [Fact]
        public void AViewportAppended_TakesTheFirstFreeNumber_AndOneTurnedOnBeforeStacksAboveTheOthers()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfBlockTableRecord sheet = db.Layouts[2].BlockTableRecord;
            ByHandle<DxfViewport>(db, "41").Erase();
            var viewport = new DxfViewport { Width = 50, Height = 40 };
            viewport.On = true;

            sheet.AppendEntity(viewport);

            // The layout has 1 and 3: the first free is 2; the statuses are 1 and 2: it is stacked third.
            Assert.Equal(2, viewport.Number);
            Assert.True(viewport.On);
            Assert.Contains(" 68\n3\n 69\n2\n", Records(db.ToText()).Single(r => HandleOf(r) == viewport.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void AViewportErased_TakesItsOverridesOutOfTheLayers()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayerTableRecord steel = db.LayerTable["steel"];

            ByHandle<DxfViewport>(db, "40").Erase();

            // Every XRECORD of overrides named the viewport alone: each goes with its entry.
            Assert.False(steel.HasOverrides);
            Assert.False(db.LayerTable["notes"].HasOverrides);
            Assert.Equal(0, steel.ExtensionDictionary.Count);
            string saved = db.ToText();
            Assert.DoesNotContain("335\n40\n", saved, StringComparison.Ordinal);
            OnlyTheseChanged(text, saved, "40", "42", "43", "44", "45", "46", "47", "48");
        }

        [Fact]
        public void ALayerFrozenByNameInAViewportOfR12_IsNotErased()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12);
            var spare = new DxfLayerTableRecord { Name = "spare" };
            db.LayerTable.Add(spare);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "35");
            viewport.FreezeLayersInViewport(new[] { spare });

            Assert.Throws<InvalidOperationException>(() => spare.Erase());
            viewport.ThawLayersInViewport(new[] { spare });
            spare.Erase();
            Assert.True(spare.IsErased);
        }

        [Fact]
        public void FrozenLayersAskedOfAViewport_CostLittleEachWithManyLayers()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var layers = new List<DxfLayerTableRecord>();

            for (int i = 0; i < 1000; i++)
            {
                var layer = new DxfLayerTableRecord { Name = "L" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                db.LayerTable.Add(layer);
                layers.Add(layer);
            }

            DxfViewport viewport = ByHandle<DxfViewport>(db, "40");
            viewport.FreezeLayersInViewport(layers.Where((l, i) => i % 2 == 0));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int frozen = layers.Count(viewport.IsLayerFrozenInViewport);

            Assert.Equal(500, frozen);
            Assert.Equal(501, viewport.GetFrozenLayers().Count);
            Assert.True(watch.ElapsedMilliseconds < 10000, "1 000 layers took " + watch.ElapsedMilliseconds + " ms.");
        }

        private static string Drawing(string version)
            => version == "R12" ? DxfObjectsEzdxfDrawings.R12 : version == "R2000" ? DxfObjectsEzdxfDrawings.R2000 : DxfObjectsEzdxfDrawings.R2018;
    }
}
