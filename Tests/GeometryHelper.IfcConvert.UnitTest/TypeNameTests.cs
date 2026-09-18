using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class TypeNameTests
    {
        private static readonly string WallAndBeam = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCWALL('0000000000000000000002',$,'Wall',$,$,#24,#23,$,$);
#26=IFCBEAM('0000000000000000000003',$,'Beam',$,$,#24,#23,$,$);
";

        [Fact]
        public void GetProductCatalog_ReportsSchemaTypeNames()
        {
            IfcTestFile.Run(WallAndBeam, model =>
            {
                var catalog = model.GetProductCatalog();
                Assert.Contains(catalog, m => m.IfcType == "IfcWall");
                Assert.Contains(catalog, m => m.IfcType == "IfcBeam");
            });
        }

        [Theory]
        [InlineData("IfcWall", 1)]
        [InlineData("wall", 1)]
        [InlineData("IfcBeam", 1)]
        [InlineData("IfcBuildingElement", 2)]
        [InlineData("IfcElement", 2)]
        [InlineData("IfcColumn", 0)]
        public void GetSolidsByType_MatchesTypeAndSupertypes(string typeName, int expected)
        {
            IfcTestFile.Run(WallAndBeam, model =>
            {
                Assert.Equal(expected, model.GetSolidsByType(typeName).Count);
            });
        }
    }
}
