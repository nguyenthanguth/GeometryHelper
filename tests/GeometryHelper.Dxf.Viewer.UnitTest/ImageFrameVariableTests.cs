using System;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// IMAGEFRAME in a drawing with no image yet, as AutoCAD 2021 sets it (probe xref-bind): the value it has already makes
    /// nothing; another makes the RASTERVARIABLES of ACAD_IMAGE_VARS, frames 70, pictures finely (71 of 1) and the drawing's
    /// unit (72 of 1 for millimetres), IMAGEFRAME 2 written as 3, frames shown and not plotted.
    /// </summary>
    public class ImageFrameVariableTests
    {
        private static bool HasVariables(CadDocument document) =>
            document.Database.NamedObjectsDictionary.Contains("ACAD_IMAGE_VARS");

        [Fact]
        public void ImageFrameSetInADrawingWithNoImage_MakesTheVariablesOfImages_AsAutoCadMakesThem()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);

            Run(session, "IMAGEFRAME", "1");
            Assert.Contains("Enter new value for IMAGEFRAME <1>: 1", session.History);
            Assert.False(HasVariables(document));

            Run(session, "IMAGEFRAME", "2");
            var variables = (DxfRasterVariables)document.Database.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS");
            Assert.Equal(DxfFrameSetting.ImageFrameOnNoPlot, variables.ImageFrame);
            Assert.Equal(DxfImageQuality.High, variables.ImageQuality);
            string saved = document.Database.ToText();
            Assert.Contains(
                "100\nAcDbRasterVariables\n 90\n0\n 70\n3\n 71\n1\n 72\n1\n",
                saved,
                StringComparison.Ordinal
            );
            Assert.Contains("  1\nRASTERVARIABLES\n  2\nAcDbRasterVariables\n", saved, StringComparison.Ordinal);
            Run(session, "IMAGEFRAME", Enter);
            Assert.Contains("Enter new value for IMAGEFRAME <2>:", session.History);
            AssertReopens(document);

            Run(session, "IMAGEFRAME", "0");
            Assert.Equal(DxfFrameSetting.ImageFrameOff, variables.ImageFrame);

            Assert.True(document.Undo());
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }
    }
}
