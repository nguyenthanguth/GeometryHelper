using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class RepresentationSelectionTests
    {
        // 1 x 0.5 x 2 body plus a larger 3 x 3 x 3 'Box' representation (IfcBoundingBox).
        private static readonly string WallWithBodyAndBox = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#30=IFCCARTESIANPOINT((-1.,-1.,0.));
#31=IFCBOUNDINGBOX(#30,3.,3.,3.);
#32=IFCSHAPEREPRESENTATION(#11,'Box','BoundingBox',(#31));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#32,#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCWALL('0000000000000000000002',$,'Wall',$,$,#24,#23,$,$);
";

        // Exporters that leave RepresentationIdentifier unset must still produce geometry.
        private static readonly string WallWithUnnamedRepresentation = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,$,'SweptSolid',(#21));
#23=IFCPRODUCTDEFINITIONSHAPE($,$,(#22));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCWALL('0000000000000000000002',$,'Wall',$,$,#24,#23,$,$);
";

        [Fact]
        public void BoxRepresentation_IsIgnored_WhenBodyExists()
        {
            IfcTestFile.Run(WallWithBodyAndBox, model =>
            {
                var geom = model.GetGeometry("0000000000000000000002");
                Assert.Single(geom.Solids);
                Assert.Equal(1.0, geom.TotalVolume, 3);
            });
        }

        [Fact]
        public void UnnamedRepresentation_IsStillConverted()
        {
            IfcTestFile.Run(WallWithUnnamedRepresentation, model =>
            {
                var geom = model.GetGeometry("0000000000000000000002");
                Assert.Single(geom.Solids);
                Assert.Equal(1.0, geom.TotalVolume, 3);
            });
        }
    }
}
