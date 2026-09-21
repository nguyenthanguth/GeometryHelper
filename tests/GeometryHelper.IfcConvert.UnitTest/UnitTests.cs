using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class UnitTests
    {
        private const string Guid = "0000000000000000000002";

        // Tekla-style unit assignment: lengths in millimetres, areas in m2, volumes in m3, mass in kg.
        private static string ModelWithUnits(string lengthUnit) => @"
#1=IFCPROJECT('0000000000000000000001',$,'TestProject',$,$,$,$,(#11),#2);
#2=IFCUNITASSIGNMENT((#3,#4,#5,#6));
" + lengthUnit + @"
#4=IFCSIUNIT(*,.AREAUNIT.,$,.SQUARE_METRE.);
#5=IFCSIUNIT(*,.VOLUMEUNIT.,$,.CUBIC_METRE.);
#6=IFCSIUNIT(*,.MASSUNIT.,.KILO.,.GRAM.);
#7=IFCCARTESIANPOINT((0.,0.,0.));
#8=IFCAXIS2PLACEMENT3D(#7,$,$);
#11=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,0.0001,#8,$);
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCBEAM('" + Guid + @"',$,'Beam',$,$,#24,$,$,$);
#40=IFCQUANTITYLENGTH('Length',$,$,6000.,$);
#41=IFCQUANTITYAREA('OuterSurfaceArea',$,$,4.2,$);
#42=IFCQUANTITYVOLUME('NetVolume',$,$,0.05,$);
#43=IFCQUANTITYWEIGHT('NetWeight',$,$,392.5,$);
#44=IFCELEMENTQUANTITY('0000000000000000000010',$,'BaseQuantities',$,$,(#40,#41,#42,#43));
#45=IFCRELDEFINESBYPROPERTIES('0000000000000000000011',$,$,$,(#25),#44);
#50=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);
#51=IFCPROPERTYSINGLEVALUE('Span',$,IFCLENGTHMEASURE(5800.),$);
#52=IFCPROPERTYSINGLEVALUE('Camber',$,IFCLENGTHMEASURE(12.),#50);
#53=IFCPROPERTYSINGLEVALUE('Grade',$,IFCLABEL('S355'),$);
#54=IFCPROPERTYSET('0000000000000000000012',$,'Pset_Custom',$,(#51,#52,#53));
#55=IFCRELDEFINESBYPROPERTIES('0000000000000000000013',$,$,$,(#25),#54);
";

        private static readonly string Millimetres = ModelWithUnits("#3=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);");

        [Theory]
        [InlineData("Length", "mm")]
        [InlineData("OuterSurfaceArea", "m2")]
        [InlineData("NetVolume", "m3")]
        [InlineData("NetWeight", "kg")]
        public void Quantities_UseProjectUnitPerMeasure(string quantity, string unit)
        {
            IfcTestFile.Run(Millimetres, model =>
            {
                Assert.Equal(unit, model.GetProperty(Guid, "BaseQuantities", quantity).Unit);
            });
        }

        [Theory]
        [InlineData("Span", "mm")]   // no unit on the property: project length unit
        [InlineData("Camber", "mm")] // explicit SI unit with MILLI prefix
        [InlineData("Grade", "")]    // not a measure
        public void SingleValues_ReportUnitSymbols(string property, string unit)
        {
            IfcTestFile.Run(Millimetres, model =>
            {
                Assert.Equal(unit, model.GetProperty(Guid, "Pset_Custom", property).Unit);
            });
        }

        [Theory]
        [InlineData("#3=IFCSIUNIT(*,.LENGTHUNIT.,.MILLI.,.METRE.);", "MILLIMETRE")]
        [InlineData("#3=IFCSIUNIT(*,.LENGTHUNIT.,.CENTI.,.METRE.);", "CENTIMETRE")]
        [InlineData("#3=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);", "METRE")]
        [InlineData(@"#3=IFCCONVERSIONBASEDUNIT(#60,.LENGTHUNIT.,'FOOT',#61);
#60=IFCDIMENSIONALEXPONENTS(1,0,0,0,0,0,0);
#61=IFCMEASUREWITHUNIT(IFCLENGTHMEASURE(0.3048),#62);
#62=IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.);", "FOOT")]
        public void OriginalLengthUnit_NamesTheProjectUnit(string lengthUnit, string expected)
        {
            IfcTestFile.Run(ModelWithUnits(lengthUnit), model =>
            {
                Assert.Equal(expected, model.OriginalLengthUnit);
            });
        }
    }
}
