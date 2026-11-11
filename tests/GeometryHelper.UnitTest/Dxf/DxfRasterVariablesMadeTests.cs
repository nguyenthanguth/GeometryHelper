using System;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The variables of images made for a drawing with none, as AutoCAD 2021 makes them when IMAGEFRAME is first changed
    /// (probe xref-bind): frames shown, pictures finely, and the unit of the drawing as RASTERVARIABLES numbers it in its 72,
    /// 1 for millimetres (INSUNITS 4), 3 for metres (6), 5 for inches (1), 0 for none.
    /// </summary>
    public class DxfRasterVariablesMadeTests
    {
        [Fact]
        public void TheVariablesOfImages_AreMadeOnce_TakingTheUnitOfTheDrawing_AsAutoCadNumbersItThere()
        {
            var meters = new DxfDatabase("AC1032") { Insunits = DxfUnits.Meters };
            Assert.False(meters.NamedObjectsDictionary.Contains("ACAD_IMAGE_VARS"));

            DxfRasterVariables variables = DxfRasterVariables.GetRasterVariables(meters);

            Assert.Equal(DxfFrameSetting.ImageFrameAbove, variables.ImageFrame);
            Assert.Equal(DxfImageQuality.High, variables.ImageQuality);
            Assert.Equal(DxfUnit.Meter, variables.UserScale);
            string saved = meters.ToText();
            Assert.Contains(" 90\n0\n 70\n1\n 71\n1\n 72\n3\n", saved, StringComparison.Ordinal);
            Assert.Contains("  1\nRASTERVARIABLES\n  2\nAcDbRasterVariables\n", saved, StringComparison.Ordinal);
            Assert.Same(variables, DxfRasterVariables.GetRasterVariables(meters));
            Assert.Equal(saved, meters.ToText());

            Assert.Equal(
                DxfUnit.Millimeter,
                DxfRasterVariables
                    .GetRasterVariables(new DxfDatabase("AC1032") { Insunits = DxfUnits.Millimeters })
                    .UserScale
            );
            Assert.Equal(
                DxfUnit.Inch,
                DxfRasterVariables
                    .GetRasterVariables(new DxfDatabase("AC1032") { Insunits = DxfUnits.Inches })
                    .UserScale
            );
            Assert.Equal(
                DxfUnit.None,
                DxfRasterVariables
                    .GetRasterVariables(new DxfDatabase("AC1032") { Insunits = DxfUnits.Undefined })
                    .UserScale
            );
            Assert.Throws<ArgumentNullException>(() => DxfRasterVariables.GetRasterVariables(null));
        }
    }
}
