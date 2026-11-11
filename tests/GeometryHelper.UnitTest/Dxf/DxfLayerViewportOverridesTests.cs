using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The overrides of a layer in a viewport set and taken out, written as AutoCAD 2021 writes them through its
    /// <c>LayerViewportProperties</c> (probe GhVpl1: XRECORDs of the layer's extension dictionary, a list of 102, 335, the
    /// value and 102 for each viewport), and read back.
    /// </summary>
    public class DxfLayerViewportOverridesTests
    {
        [Fact]
        public void TheOverridesSet_AreWrittenAsAutoCadWritesThem_AndReadBack()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfViewport first = ByHandle<DxfViewport>(db, "40");
            DxfViewport second = ByHandle<DxfViewport>(db, "41");
            DxfLayerTableRecord hidden = db.LayerTable["hidden"];
            Assert.Null(hidden.ExtensionDictionary);

            DxfLayerViewportProperties look = hidden.GetViewportOverrides(first);
            look.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            look.Linetype = "dashed";
            look.LineWeight = DxfLineWeight.LineWeight050;
            look.Transparency = new DxfTransparency(127);
            hidden.GetViewportOverrides(second).Color = DxfColor.FromRgb(10, 20, 30);

            // AutoCAD 2021 (GhVpl1): 420 -1023410175 for the index 1, -1039526882 for 10, 20, 30; the second viewport's list
            // after the first's.
            var color = (DxfXrecord)hidden.ExtensionDictionary.GetAt("ADSK_XREC_LAYER_COLOR_OVR");
            Assert.Equal(
                "102={ADSK_LYR_COLOR_OVERRIDE 335=40 420=-1023410175 102=} 102={ADSK_LYR_COLOR_OVERRIDE 335=41 420=-1039526882 102=}",
                string.Join(" ", color.Data.Select(g => g.TypeCode + "=" + g.Value))
            );
            string dashed = db.LinetypeTable["DASHED"].Handle;
            Assert.Equal(
                "102={ADSK_LYR_LINETYPE_OVERRIDE 335=40 343=" + dashed + " 102=}",
                Text(hidden, "ADSK_XREC_LAYER_LINETYPE_OVR")
            );
            Assert.Equal(
                "102={ADSK_LYR_LINEWT_OVERRIDE 335=40 91=50 102=}",
                Text(hidden, "ADSK_XREC_LAYER_LINEWT_OVR")
            );
            Assert.Equal(
                "102={ADSK_LYR_ALPHA_OVERRIDE 335=40 440=33554559 102=}",
                Text(hidden, "ADSK_XREC_LAYER_ALPHA_OVR")
            );

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            DxfLayerViewportProperties back = read.LayerTable["hidden"]
                .GetViewportOverrides(ByHandle<DxfViewport>(read, "40"));
            Assert.Equal(1, back.Color.ColorIndex);
            Assert.Equal("DASHED", back.Linetype);
            Assert.Equal(DxfLineWeight.LineWeight050, back.LineWeight);
            Assert.Equal(127, back.Transparency.Alpha);
            Assert.Equal(
                0x0A141E,
                read.LayerTable["hidden"].GetViewportOverrides(ByHandle<DxfViewport>(read, "41")).Color.ColorValue
            );
        }

        [Fact]
        public void AnOverrideSetAgain_ReplacesTheValueOfItsViewport_TheOtherKept()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfLayerTableRecord steel = db.LayerTable["steel"];
            DxfViewport first = ByHandle<DxfViewport>(db, "40");

            // ezdxf wrote steel in 40 the colour 5; set 3, the list keeps its place.
            steel.GetViewportOverrides(first).Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);

            Assert.Equal(3, steel.GetViewportOverrides(first).Color.ColorIndex);
            Assert.Single(Text(steel, "ADSK_XREC_LAYER_COLOR_OVR").Split(' '), g =>
                g.StartsWith("335=", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void OneOverrideTakenOut_LeavesTheOthers_AndRemoveViewportOverridesTakesOneViewportsAll()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfLayerTableRecord hidden = db.LayerTable["hidden"];
            DxfViewport first = ByHandle<DxfViewport>(db, "40");
            DxfViewport second = ByHandle<DxfViewport>(db, "41");
            hidden.GetViewportOverrides(first).Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
            hidden.GetViewportOverrides(first).LineWeight = DxfLineWeight.LineWeight030;
            hidden.GetViewportOverrides(second).Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2);

            hidden.GetViewportOverrides(first).RemoveLineWeightOverride();
            Assert.False(hidden.GetViewportOverrides(first).IsLineWeightOverridden);
            Assert.True(hidden.GetViewportOverrides(first).IsColorOverridden);
            Assert.False(hidden.ExtensionDictionary.Contains("ADSK_XREC_LAYER_LINEWT_OVR"));

            hidden.RemoveViewportOverrides(first);
            Assert.False(hidden.HasViewportOverrides(first));
            Assert.True(hidden.HasViewportOverrides(second));
            Assert.Equal(2, hidden.GetViewportOverrides(second).Color.ColorIndex);
        }

        [Fact]
        public void APlotStyleOverride_IsRefusedInADrawingPlottedByColour_AndWrittenByItsEntryInOneOfNamedStyles()
        {
            DxfDatabase byColour = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfLayerViewportProperties look = byColour
                .LayerTable["hidden"]
                .GetViewportOverrides(ByHandle<DxfViewport>(byColour, "40"));
            Assert.Throws<InvalidOperationException>(() => look.PlotStyleName = "Normal");
            Assert.False(look.IsPlotStyleOverridden);
            Assert.Equal("Normal", look.PlotStyleName);

            DxfDatabase named = DxfDatabase.Parse(
                DxfObjectsEzdxfDrawings
                    .R2018.Replace("$PSTYLEMODE\r\n290\r\n1\r\n", "$PSTYLEMODE\r\n290\r\n0\r\n")
                    .Replace("$PSTYLEMODE\n290\n1\n", "$PSTYLEMODE\n290\n0\n")
            );
            Assert.False(named.PlotStyleMode);
            DxfLayerTableRecord hidden = named.LayerTable["hidden"];
            DxfLayerViewportProperties styled = hidden.GetViewportOverrides(ByHandle<DxfViewport>(named, "40"));
            Assert.Throws<ArgumentException>(() => styled.PlotStyleName = "Style 1");

            styled.PlotStyleName = "Normal";

            Assert.True(styled.IsPlotStyleOverridden);
            Assert.Equal("Normal", styled.PlotStyleName);
            Assert.Equal(
                "102={ADSK_LYR_PLOTSTYLE_OVERRIDE 335=40 344=13 102=}",
                Text(hidden, "ADSK_XREC_LAYER_PLOTSTYLE_OVR")
            );
            Assert.True(hidden.HasOverrides);
            hidden.RemoveAllOverrides();
            Assert.False(hidden.HasOverrides);
        }

        [Fact]
        public void WhatALayerDoesNotTake_IsRefused_NothingWritten()
        {
            string text = DxfObjectsEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayerViewportProperties look = db.LayerTable["hidden"]
                .GetViewportOverrides(ByHandle<DxfViewport>(db, "40"));

            Assert.Throws<ArgumentException>(() => look.Color = DxfColor.ByLayer);
            Assert.Throws<ArgumentNullException>(() => look.Color = null);
            Assert.Throws<ArgumentException>(() => look.Linetype = "NOSUCH");
            Assert.Throws<ArgumentException>(() => look.Linetype = "ByLayer");
            Assert.Throws<ArgumentException>(() => look.LineWeight = DxfLineWeight.ByLayer);
            Assert.Throws<ArgumentException>(() => look.Transparency = DxfTransparency.ByLayer);
            Assert.Equal(text, db.ToText());
        }

        private static string Text(DxfLayerTableRecord layer, string key) =>
            string.Join(
                " ",
                ((DxfXrecord)layer.ExtensionDictionary.GetAt(key)).Data.Select(g => g.TypeCode + "=" + g.Value)
            );
    }
}
