using System.Linq;
using GeometryHelper.IfcConvert.Core;
using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class PhysicalProductFilterTests
    {
        // A wall, an opening element and a space share the same 1 m3 body.
        private static readonly string WallOpeningSpace = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCWALL('0000000000000000000002',$,'Wall',$,$,#24,#23,$,$);
#26=IFCOPENINGELEMENT('0000000000000000000003',$,'Opening',$,$,#24,#23,$,$);
#27=IFCSPACE('0000000000000000000004',$,'Room',$,$,#24,#23,$,.ELEMENT.,.INTERNAL.,$);
";

        [Fact]
        public void ModelWideQueries_ExcludeOpeningsAndSpaces_ByDefault()
        {
            IfcTestFile.Run(WallOpeningSpace, model =>
            {
                Assert.Single(model.GetAllSolids());
                Assert.Single(model.EnumerateSolids());
                Assert.Equal("IfcWall", model.EnumerateGeometries().Single().IfcType);

                // The catalog and explicit type queries still see everything.
                Assert.Equal(3, model.GetProductCatalog().Count);
                Assert.Single(model.GetSolidsByType("IfcSpace"));
                Assert.NotNull(model.GetSolid("0000000000000000000003"));
            });
        }

        [Fact]
        public void ModelWideQueries_IncludeEverything_WhenRequested()
        {
            IfcTestFile.Run(WallOpeningSpace, model =>
            {
                var options = new IfcConvertOptions { IncludeNonPhysicalProducts = true };
                Assert.Equal(3, model.GetAllSolids(options).Count);
            });
        }
    }
}
