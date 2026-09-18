using Xunit;

namespace GeometryHelper.IfcConvert.UnitTest
{
    [Collection("IfcEngine")]
    public class MappedItemTests
    {
        // Body: 1 x 0.5 x 2 box centred on the XY origin (centroid at 0,0,1).
        // Representation map origin is shifted +5 X; the mapping target rotates 90 degrees about Z
        // (Axis1 = +Y) and translates +10 Y.
        // IFC semantics (as implemented by xBIM and IfcOpenShell): world = Target * Origin * local.
        //   (0,0,1) -> origin -> (5,0,1) -> rotate -> (0,5,1) -> translate -> (0,15,1)
        private static readonly string WallWithMappedItem = IfcTestFile.CommonHeader() + @"
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#5,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#40=IFCCARTESIANPOINT((5.,0.,0.));
#41=IFCAXIS2PLACEMENT3D(#40,$,$);
#42=IFCREPRESENTATIONMAP(#41,#22);
#47=IFCCARTESIANPOINT((0.,10.,0.));
#48=IFCDIRECTION((0.,1.,0.));
#49=IFCDIRECTION((-1.,0.,0.));
#43=IFCCARTESIANTRANSFORMATIONOPERATOR3D(#48,#49,#47,$,#9);
#44=IFCMAPPEDITEM(#42,#43);
#45=IFCSHAPEREPRESENTATION(#11,'Body','MappedRepresentation',(#44));
#46=IFCPRODUCTDEFINITIONSHAPE($,$,(#45));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCWALL('0000000000000000000002',$,'Wall',$,$,#24,#46,$,$);
";

        // Off-centre body (centroid 2,0,1); the map origin rotates 90 degrees about Z and shifts +5 X;
        // the target only shifts +10 Y. The three interpretations in the wild give distinct answers:
        //   Target * Origin (IfcOpenShell, this library) -> (5, 12, 1)
        //   Origin * Target (xBIM Xbim3DModelContext)    -> (-5, 2, 1)
        //   Target * Origin^-1 (STEP Part 43 wording)    -> (0, 13, 1)
        private static readonly string WallWithRotatedMappingOrigin = IfcTestFile.CommonHeader() + @"
#19=IFCCARTESIANPOINT((2.,0.));
#18=IFCAXIS2PLACEMENT2D(#19,$);
#20=IFCRECTANGLEPROFILEDEF(.AREA.,$,#18,1.,0.5);
#21=IFCEXTRUDEDAREASOLID(#20,#8,#9,2.);
#22=IFCSHAPEREPRESENTATION(#11,'Body','SweptSolid',(#21));
#40=IFCCARTESIANPOINT((5.,0.,0.));
#39=IFCDIRECTION((0.,1.,0.));
#41=IFCAXIS2PLACEMENT3D(#40,#9,#39);
#42=IFCREPRESENTATIONMAP(#41,#22);
#47=IFCCARTESIANPOINT((0.,10.,0.));
#43=IFCCARTESIANTRANSFORMATIONOPERATOR3D($,$,#47,$,$);
#44=IFCMAPPEDITEM(#42,#43);
#45=IFCSHAPEREPRESENTATION(#11,'Body','MappedRepresentation',(#44));
#46=IFCPRODUCTDEFINITIONSHAPE($,$,(#45));
#24=IFCLOCALPLACEMENT($,#8);
#25=IFCWALL('0000000000000000000002',$,'Wall',$,$,#24,#46,$,$);
";

        [Fact]
        public void MappedItem_RotatedMappingOrigin_IsAppliedBeforeTarget()
        {
            IfcTestFile.Run(WallWithRotatedMappingOrigin, model =>
            {
                var solid = model.GetSolid("0000000000000000000002");
                Assert.NotNull(solid);
                Assert.Equal(5.0, solid.Centroid.X, 3);
                Assert.Equal(12.0, solid.Centroid.Y, 3);
                Assert.Equal(1.0, solid.Centroid.Z, 3);
            });
        }

        [Fact]
        public void MappedItem_AppliesMappingOriginThenTarget()
        {
            IfcTestFile.Run(WallWithMappedItem, model =>
            {
                var solid = model.GetSolid("0000000000000000000002");
                Assert.NotNull(solid);
                Assert.Equal(1.0, solid.Volume, 3);
                Assert.Equal(0.0, solid.Centroid.X, 3);
                Assert.Equal(15.0, solid.Centroid.Y, 3);
                Assert.Equal(1.0, solid.Centroid.Z, 3);
            });
        }
    }
}
