using System.Collections.Generic;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The description of a drawing template, which AutoCAD's Template Options writes into a DWG's AcDb:Template section
    /// before MEASUREMENT: written by the DWG writer and read back by the container, MEASUREMENT kept beside it.
    /// </summary>
    public class DwgTemplateDescriptionTests
    {
        [Fact]
        public void ATemplatesDescription_IsWrittenBeforeMeasurement_AndReadBack()
        {
            var database = new DxfDatabase("AC1032") { Measurement = DxfMeasurementValue.English };

            byte[] bytes = DwgWriter.ToDwg(
                database.ToText(),
                "AC1032",
                out IDictionary<string, int> _,
                null,
                "ISO A1 sheet, workshop"
            );
            DwgContainer container = DwgContainer.Read(bytes);

            Assert.Equal("ISO A1 sheet, workshop", container.TemplateDescription);
            Assert.Equal(0, container.Measurement);
        }

        [Fact]
        public void ADrawingWithNoDescription_ReadsAnEmptyOne_AndItsMeasurement()
        {
            var database = new DxfDatabase("AC1032") { Measurement = DxfMeasurementValue.Metric };

            DwgContainer container = DwgContainer.Read(DwgWriter.ToDwg(database.ToText(), "AC1032"));

            Assert.Equal(string.Empty, container.TemplateDescription);
            Assert.Equal(1, container.Measurement);
        }
    }
}
