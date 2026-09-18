using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class PropertyReadingTests
    {
        private const string Guid = "0000000000000000000002";

        // IFC4 IfcPropertyBoundedValue lists UpperBoundValue before LowerBoundValue.
        // A beam whose type carries Pset_BeamCommon (Span 6000, IsExternal false) and whose occurrence
        // overrides IsExternal and adds LoadBearing. Plus complex, list, bounded and enumerated values.
        private static string Model(string beamType) => IfcTestFile.CommonHeader("..MILLI.,.METRE.") + @"
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCBEAM('" + Guid + @"',$,'Beam',$,$,#24,$,$,$);
#30=IFCPROPERTYSINGLEVALUE('Span',$,IFCPOSITIVELENGTHMEASURE(6000.),$);
#31=IFCPROPERTYSINGLEVALUE('IsExternal',$,IFCBOOLEAN(.F.),$);
#32=IFCPROPERTYSET('0000000000000000000020',$,'Pset_BeamCommon',$,(#30,#31));
" + beamType + @"
#34=IFCRELDEFINESBYTYPE('0000000000000000000022',$,$,$,(#25),#33);
#40=IFCPROPERTYSINGLEVALUE('IsExternal',$,IFCBOOLEAN(.T.),$);
#41=IFCPROPERTYSINGLEVALUE('LoadBearing',$,IFCBOOLEAN(.T.),$);
#42=IFCPROPERTYSET('0000000000000000000023',$,'Pset_BeamCommon',$,(#40,#41));
#43=IFCRELDEFINESBYPROPERTIES('0000000000000000000024',$,$,$,(#25),#42);
#50=IFCPROPERTYSINGLEVALUE('Width',$,IFCLENGTHMEASURE(150.),$);
#51=IFCPROPERTYSINGLEVALUE('Height',$,IFCLENGTHMEASURE(300.),$);
#52=IFCCOMPLEXPROPERTY('Profile',$,'Section',(#50,#51));
#53=IFCPROPERTYLISTVALUE('Holes',$,(IFCLENGTHMEASURE(22.),IFCLENGTHMEASURE(26.)),$);
#54=IFCPROPERTYBOUNDEDVALUE('Temperature',$,IFCTHERMODYNAMICTEMPERATUREMEASURE(300.),IFCTHERMODYNAMICTEMPERATUREMEASURE(250.),$,$);
#55=IFCPROPERTYENUMERATEDVALUE('Finish',$,(IFCLABEL('Galvanized')),$);
#56=IFCPROPERTYSET('0000000000000000000025',$,'Pset_Custom',$,(#52,#53,#54,#55));
#57=IFCRELDEFINESBYPROPERTIES('0000000000000000000026',$,$,$,(#25),#56);
";

        private static readonly string BeamWithType = Model("#33=IFCBEAMTYPE('0000000000000000000021',$,'BeamType',$,$,(#32),$,$,$,.BEAM.);");

        [Theory]
        [InlineData("IFC4")]
        [InlineData("IFC2X3")]
        public void TypePropertySets_AreInheritedAndOverriddenByOccurrence(string schema)
        {
            IfcTestFile.Run(BeamWithType, model =>
            {
                Assert.Equal(schema.Replace("X", "x"), model.SchemaVersion.ToUpperInvariant().Replace("X", "x"));

                var span = model.GetProperty(Guid, "Pset_BeamCommon", "Span");
                Assert.NotNull(span);
                Assert.Equal(6000.0, (double)span.Value);
                Assert.Equal("mm", span.Unit);

                Assert.Equal(true, model.GetProperty(Guid, "Pset_BeamCommon", "IsExternal").Value);
                Assert.Equal(true, model.GetProperty(Guid, "Pset_BeamCommon", "LoadBearing").Value);
            }, schema);
        }

        [Fact]
        public void ComplexProperty_IsFlattenedWithDottedNames()
        {
            IfcTestFile.Run(BeamWithType, model =>
            {
                var width = model.GetProperty(Guid, "Pset_Custom", "Profile.Width");
                Assert.Equal(150.0, (double)width.Value);
                Assert.Equal("mm", width.Unit);
            });
        }

        [Fact]
        public void ListBoundedAndEnumeratedValues_AreReadable()
        {
            IfcTestFile.Run(BeamWithType, model =>
            {
                var holes = model.GetProperty(Guid, "Pset_Custom", "Holes");
                Assert.Equal("22, 26", holes.Value);
                Assert.Equal("mm", holes.Unit);

                Assert.Equal("250..300", model.GetProperty(Guid, "Pset_Custom", "Temperature").Value);
                Assert.Equal("Galvanized", model.GetProperty(Guid, "Pset_Custom", "Finish").Value);
            });
        }
    }
}
